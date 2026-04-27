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
        // 🌟 1. 핵심 호환성: 중력이 변환(회전) 중일 때는 월런이 개입하지 않고 얌전히 기다립니다!
        if (pm != null && pm.isTransitioning) return; 

        CheckForWall();
        StateMachine();
    }

    private void FixedUpdate()
    {
        // 🌟 중력 변환 중일 때는 월런 물리 연산 대기
        if (pm != null && pm.isTransitioning) return; 

        if (pm.wallrunning)
            WallRunningMovement();
    }

    private void CheckForWall()
    {
        // 1. 현재 바닥(중력)의 위쪽 방향
        Vector3 gravityUp = pm.currentGravity == Vector3.down ? Vector3.up : -pm.currentGravity.normalized;
        
        // 2. 카메라가 바라보는 앞(forward)을 현재 바닥에 평평하게 눕힙니다 (위아래 쳐다보는 각도 무시)
        Vector3 flatForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, gravityUp).normalized;
        
        // 🌟 [최종 해결] 위쪽(Up)과 앞쪽(Forward)을 교차(Cross)시키면 수학적으로 '완벽하게 평행한 오른쪽'이 무조건 나옵니다!
        Vector3 trueRight = Vector3.Cross(gravityUp, flatForward).normalized;

        wallRight = Physics.Raycast(transform.position, trueRight, out rightWallhit, wallCheckDistance, whatIsWall);
        wallLeft = Physics.Raycast(transform.position, -trueRight, out leftWallhit, wallCheckDistance, whatIsWall);

        // 시각화: 이제 천장이든 벽이든, 레이캐스트가 바닥과 완벽하게 평행한 십자가(─ ─) 모양으로 뻗어나갑니다.
        Debug.DrawRay(transform.position, trueRight * wallCheckDistance, wallRight ? Color.green : Color.red);
        Debug.DrawRay(transform.position, -trueRight * wallCheckDistance, wallLeft ? Color.green : Color.red);
    }

    // 🌟 2. 하드코딩된 Vector3.down 대신, 현재 플레이어의 진짜 중력 방향을 기준으로 바닥을 체크합니다.
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

    // 🌟 3. 무조건 Y축을 0으로 만드는 코드 대신, 현재 '중력 방향'의 속도만 깔끔하게 제거하는 수학 공식을 씁니다.
    private void RemoveGravityVelocity()
    {
        Vector3 gravityDir = pm.currentGravity.normalized;
        rb.linearVelocity = rb.linearVelocity - Vector3.Project(rb.linearVelocity, gravityDir);
    }

    private void StartWallRun()
    {
        pm.wallrunning = true;
        RemoveGravityVelocity(); // 가변 중력에 맞춘 속도 초기화

        cam.DoFov(90f);
        if (wallLeft) cam.DoTilt(-5f);
        if (wallRight) cam.DoTilt(5f);
    }

    private void WallRunningMovement()
    {
        pm.isGraviting = useGravity;
        Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;
        Vector3 wallForward = Vector3.Cross(wallNormal, transform.up);

        if ((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude)
            wallForward = -wallForward;

        rb.AddForce(wallForward * wallRunForce, ForceMode.Force);
        rb.AddForce(-wallNormal * 100, ForceMode.Force);

        if (!pm.isGraviting) 
        {
            RemoveGravityVelocity(); // 가변 중력에 맞춘 떨어짐 방지
        }
        else
        {
            RemoveGravityVelocity(); 
        }

        Quaternion targetRotation = Quaternion.LookRotation(wallForward, -pm.currentGravity.normalized);
        orientation.rotation = Quaternion.Slerp(orientation.rotation, targetRotation, Time.deltaTime * 15f);
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

        RemoveGravityVelocity(); // 가변 중력에 맞춘 월점프
        rb.AddForce(forceToApply, ForceMode.Impulse);
    }

    private void DropWall() => ExitRoutine();

    private void ExitRoutine()
    {
        exitingWall = true;
        exitWallTimer = exitWallTime;
    }
}