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
        if (pm != null && pm.isTransitioning) return; 

        CheckForWall();
        StateMachine();
    }

    private void FixedUpdate()
    {
        if (pm != null && pm.isTransitioning) return; 

        if (pm.wallrunning)
            WallRunningMovement();
    }

    private void CheckForWall()
    {
        wallRight = false;
        wallLeft = false;

        // 🌟 현재 중력(바닥) 기준으로 흔들리지 않는 절대 축 생성
        Vector3 gravityUp = pm.currentGravity == Vector3.down ? Vector3.up : -pm.currentGravity.normalized;
        Vector3 flatForward = Vector3.ProjectOnPlane(orientation.forward, gravityUp).normalized;
        Vector3 trueRight = Vector3.Cross(gravityUp, flatForward).normalized;

        // 🌟 [핵심 해결] SphereCast의 '모서리 곡선' 버그를 버리고,
        // 얇은 Raycast 3가닥을 부채꼴(옆, 앞대각선, 뒤대각선)로 넓게 쏴서 180도 판정을 만듭니다!
        Vector3[] rightDirs = { 
            trueRight, 
            (trueRight + flatForward).normalized, // 앞쪽 45도
            (trueRight - flatForward).normalized  // 뒤쪽 45도
        };

        foreach (Vector3 dir in rightDirs)
        {
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, wallCheckDistance, whatIsWall))
            {
                wallRight = true;
                rightWallhit = hit;
                Debug.DrawRay(transform.position, dir * wallCheckDistance, Color.green);
                break; // 한 가닥이라도 닿으면 즉시 월런 인정!
            }
            else Debug.DrawRay(transform.position, dir * wallCheckDistance, Color.red);
        }

        // 🌟 왼쪽 벽 감지도 동일하게 부채꼴 3가닥 발사
        Vector3[] leftDirs = { 
            -trueRight, 
            (-trueRight + flatForward).normalized, 
            (-trueRight - flatForward).normalized 
        };

        foreach (Vector3 dir in leftDirs)
        {
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, wallCheckDistance, whatIsWall))
            {
                wallLeft = true;
                leftWallhit = hit;
                Debug.DrawRay(transform.position, dir * wallCheckDistance, Color.green);
                break;
            }
            else Debug.DrawRay(transform.position, dir * wallCheckDistance, Color.red);
        }
    }

    private bool AboveGround() => !Physics.Raycast(transform.position, pm.currentGravity.normalized, minJumpHeight, whatIsGround);

    private void StateMachine()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        if((wallLeft || wallRight) && verticalInput > 0 && AboveGround() && !exitingWall)
        {
            if (!pm.wallrunning) StartWallRun();

            if (Input.GetKeyDown(jumpKey)) WallJump();
            if (Input.GetKeyDown(dropKey)) DropWall();
        }
        else if (exitingWall)
        {
            if (pm.wallrunning) StopWallRun();

            if (exitWallTimer > 0) exitWallTimer -= Time.deltaTime;
            if (exitWallTimer <= 0) exitingWall = false;
        }
        else
        {
            if (pm.wallrunning) StopWallRun();
        }
    }

    private void RemoveGravityVelocity()
    {
        Vector3 gravityDir = pm.currentGravity.normalized;
        rb.linearVelocity = rb.linearVelocity - Vector3.Project(rb.linearVelocity, gravityDir);
    }

    private void StartWallRun()
    {
        pm.wallrunning = true;
        RemoveGravityVelocity();

        cam.DoFov(90f);
        if (wallLeft) cam.DoTilt(-5f);
        if (wallRight) cam.DoTilt(5f);
    }

    private void WallRunningMovement()
    {
        pm.isGraviting = useGravity;
        Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;
        
        // 🌟 [수정 4] 흔들리는 transform.up 대신 절대축인 gravityUp을 사용하여 진행방향 솟구침 완벽 방지!
        Vector3 gravityUp = pm.currentGravity == Vector3.down ? Vector3.up : -pm.currentGravity.normalized;
        Vector3 wallForward = Vector3.Cross(wallNormal, gravityUp);

        if ((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude)
            wallForward = -wallForward;

        rb.AddForce(wallForward * wallRunForce, ForceMode.Force);
        rb.AddForce(-wallNormal * 100, ForceMode.Force);

        if (!pm.isGraviting) 
        {
            RemoveGravityVelocity();
        }
        else
        {
            RemoveGravityVelocity(); 
        }

        // ✅ 월런 중 orientation을 벽 이동 방향으로 맞춰줍니다.
        Quaternion targetRotation = Quaternion.LookRotation(wallForward, gravityUp);
        orientation.rotation = Quaternion.Slerp(orientation.rotation, targetRotation, Time.deltaTime * 15f);
    }

    private void StopWallRun()
    {
        pm.wallrunning = false;
        cam.DoFov(80f);
        cam.DoTilt(0f);

        if (pm.currentGravity == Vector3.down) 
        {
            float playerY = pm.transform.localEulerAngles.y;
            float camY = cam.yRotation;
            
            float diff = Mathf.DeltaAngle(playerY, camY);
            
            cam.yRotation = playerY + (diff / 2f);
        }
    }
    private void WallJump()
    {
        ExitRoutine();
        Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;
        Vector3 forceToApply = transform.up * wallJumpUpForce + wallNormal * wallJumpSideForce;

        RemoveGravityVelocity();
        rb.AddForce(forceToApply, ForceMode.Impulse);
    }

    private void DropWall() => ExitRoutine();

    private void ExitRoutine()
    {
        exitingWall = true;
        exitWallTimer = exitWallTime;
    }
}