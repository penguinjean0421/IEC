using UnityEngine;

public class WallRunning : MonoBehaviour
{
    [Header("Wallrunning")]
    public LayerMask whatIsWall;
    public LayerMask whatIsGround;
    public float wallRunForce;
    public float wallJumpUpForce;
    public float wallJumpSideForce;

    [Header("Input")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode dropKey = KeyCode.LeftControl;
    private float horizontalInput;
    private float verticalInput;

    [Header("Detection")]
    public float wallCheckDistance;
    public float minJumpHeight;
    private RaycastHit leftWallhit;
    private RaycastHit rightWallhit;
    private bool wallLeft;
    private bool wallRight;

    [Header("Exiting")]
    private bool exitingWall;
    public float exitWallTime;
    private float exitWallTimer;

    [Header("Gravity")]
    public bool useGravity;
    public float gravityCounterForce;

    [Header("References")]
    public Transform orientation;
    public PlayerCam cam;
    private PlayerMovement pm;
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        pm = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        CheckForWall();
        StateMachine();
    }

    private void FixedUpdate()
    {
        if (pm.wallrunning)
            WallRunningMovement();
    }

    private void CheckForWall()
    {
        wallRight = Physics.Raycast(transform.position, orientation.right, out rightWallhit, wallCheckDistance, whatIsWall);
        wallLeft = Physics.Raycast(transform.position, -orientation.right, out leftWallhit, wallCheckDistance, whatIsWall);
    }

    private bool AboveGround() => !Physics.Raycast(transform.position, Vector3.down, minJumpHeight, whatIsGround);

    private void StateMachine()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // State 1 - Wallrunning (조건: 벽 접촉 + 전진 입력 + 공중 + 탈출 중 아님)
        if((wallLeft || wallRight) && verticalInput > 0 && AboveGround() && !exitingWall)
        {
            if (!pm.wallrunning) StartWallRun();

            if (Input.GetKeyDown(jumpKey)) WallJump();
            if (Input.GetKeyDown(dropKey)) DropWall();
        }
        // State 2 - Exiting
        else if (exitingWall)
        {
            if (pm.wallrunning) StopWallRun();

            if (exitWallTimer > 0) exitWallTimer -= Time.deltaTime;
            if (exitWallTimer <= 0) exitingWall = false;
        }
        // State 3 - None (벽에서 떨어지거나 전진 입력을 뗐을 때)
        else
        {
            if (pm.wallrunning) StopWallRun();
        }
    }

    private void StartWallRun()
    {
        pm.wallrunning = true;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        cam.DoFov(90f);
        if (wallLeft) cam.DoTilt(-5f);
        if (wallRight) cam.DoTilt(5f);
    }

    private void WallRunningMovement()
    {
        rb.useGravity = useGravity;
        Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;
        Vector3 wallForward = Vector3.Cross(wallNormal, transform.up);

        if ((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude)
            wallForward = -wallForward;

        rb.AddForce(wallForward * wallRunForce, ForceMode.Force);
        rb.AddForce(-wallNormal * 100, ForceMode.Force);

        // --- 수정된 부분: 중력 상쇄(힘) 대신 Y축 속도를 0으로 강제 고정 ---
        if (!useGravity) // Inspector에서 useGravity를 끄거나, 아래 로직으로 강제 고정
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
        else
        {
            // useGravity가 체크되어 있더라도 벽타기 중에는 떨어지지 않게 묶음
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z); 
            // (만약 벽타기 중 약간씩 떨어지는 연출이 필요하다면 0f 대신 -1f 같은 작은 음수 값 지정 가능)
        }
        // -------------------------------------------------------------
    }

    private void StopWallRun()
    {
        pm.wallrunning = false;
        cam.DoFov(80f);
        cam.DoTilt(0f);
    }

    private void WallJump()
    {
        ExitRoutine();
        Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;
        Vector3 forceToApply = transform.up * wallJumpUpForce + wallNormal * wallJumpSideForce;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(forceToApply, ForceMode.Impulse);
    }

    private void DropWall() => ExitRoutine();

    private void ExitRoutine()
    {
        exitingWall = true;
        exitWallTimer = exitWallTime;
    }
}