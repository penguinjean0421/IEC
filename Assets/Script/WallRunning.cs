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
    private Vector3 lastWallNormal; 

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

        Vector3 gravityUp = pm.currentGravity == Vector3.down ? Vector3.up : -pm.currentGravity.normalized;
        Vector3 flatForward = Vector3.ProjectOnPlane(orientation.forward, gravityUp).normalized;
        Vector3 trueRight = Vector3.Cross(gravityUp, flatForward).normalized;

        Vector3[] rightDirs = { trueRight, (trueRight + flatForward).normalized, (trueRight - flatForward).normalized };
        foreach (Vector3 dir in rightDirs)
        {
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, wallCheckDistance, whatIsWall))
            {
                wallRight = true;
                rightWallhit = hit;
                break; 
            }
        }

        Vector3[] leftDirs = { -trueRight, (-trueRight + flatForward).normalized, (-trueRight - flatForward).normalized };
        foreach (Vector3 dir in leftDirs)
        {
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, wallCheckDistance, whatIsWall))
            {
                wallLeft = true;
                leftWallhit = hit;
                break;
            }
        }
    }

    private bool AboveGround() => !Physics.Raycast(transform.position, pm.currentGravity.normalized, minJumpHeight, whatIsGround);

    private void StateMachine()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        if (!AboveGround()) lastWallNormal = Vector3.zero;

        Vector3 currentWallNormal = Vector3.zero;
        if (wallRight) currentWallNormal = rightWallhit.normal;
        else if (wallLeft) currentWallNormal = leftWallhit.normal;

        bool isNewWall = lastWallNormal == Vector3.zero || Vector3.Angle(currentWallNormal, lastWallNormal) > 10f;

        if((wallLeft || wallRight) && verticalInput > 0 && AboveGround() && !exitingWall && isNewWall)
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

        // 🌟 벽을 타기 시작하면 대시 횟수 재장전!
        Dashing dashingScript = GetComponent<Dashing>();
        if (dashingScript != null) dashingScript.ResetDashCount();

        cam.DoFov(90f);
        if (wallLeft) cam.DoTilt(-5f);
        if (wallRight) cam.DoTilt(5f);
    }

    private void WallRunningMovement()
    {
        pm.isGraviting = useGravity;
        Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;
        Vector3 gravityUp = pm.currentGravity == Vector3.down ? Vector3.up : -pm.currentGravity.normalized;
        Vector3 wallForward = Vector3.Cross(wallNormal, gravityUp);

        if ((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude)
            wallForward = -wallForward;

        rb.AddForce(wallForward * wallRunForce, ForceMode.Force);
        rb.AddForce(-wallNormal * 100, ForceMode.Force);

        RemoveGravityVelocity();

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
        lastWallNormal = wallNormal;

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

// using UnityEngine;

// public class WallRunning : MonoBehaviour
// {
//     [Header("Wallrunning")]
//     public LayerMask whatIsWall;
//     public LayerMask whatIsGround;
//     public float wallRunForce;
//     public float wallJumpUpForce;
//     public float wallJumpSideForce;

//     [Header("Input")]
//     public KeyCode jumpKey = KeyCode.Space;
//     public KeyCode dropKey = KeyCode.LeftControl;
//     private float horizontalInput;
//     private float verticalInput;

//     [Header("Detection")]
//     public float wallCheckDistance;
//     public float minJumpHeight;
//     private RaycastHit leftWallhit;
//     private RaycastHit rightWallhit;
//     private bool wallLeft;
//     private bool wallRight;
    
//     // 🌟 방금 점프한 벽의 방향을 기억할 변수 추가
//     private Vector3 lastWallNormal; 

//     [Header("Exiting")]
//     private bool exitingWall;
//     public float exitWallTime;
//     private float exitWallTimer;

//     [Header("Gravity")]
//     public bool useGravity;
//     public float gravityCounterForce;

//     [Header("References")]
//     public Transform orientation;
//     public PlayerCam cam;
//     private PlayerMovement pm;
//     private Rigidbody rb;

//     private void Start()
//     {
//         rb = GetComponent<Rigidbody>();
//         pm = GetComponent<PlayerMovement>();
//     }

//     private void Update()
//     {
//         if (pm != null && pm.isTransitioning) return; 

//         CheckForWall();
//         StateMachine();
//     }

//     private void FixedUpdate()
//     {
//         if (pm != null && pm.isTransitioning) return; 

//         if (pm.wallrunning)
//             WallRunningMovement();
//     }

//     private void CheckForWall()
//     {
//         wallRight = false;
//         wallLeft = false;

//         // 현재 중력(바닥) 기준으로 흔들리지 않는 절대 축 생성
//         Vector3 gravityUp = pm.currentGravity == Vector3.down ? Vector3.up : -pm.currentGravity.normalized;
//         Vector3 flatForward = Vector3.ProjectOnPlane(orientation.forward, gravityUp).normalized;
//         Vector3 trueRight = Vector3.Cross(gravityUp, flatForward).normalized;

//         // 오른쪽 벽 부채꼴 레이캐스트
//         Vector3[] rightDirs = { 
//             trueRight, 
//             (trueRight + flatForward).normalized, 
//             (trueRight - flatForward).normalized  
//         };

//         foreach (Vector3 dir in rightDirs)
//         {
//             if (Physics.Raycast(transform.position, dir, out RaycastHit hit, wallCheckDistance, whatIsWall))
//             {
//                 wallRight = true;
//                 rightWallhit = hit;
//                 Debug.DrawRay(transform.position, dir * wallCheckDistance, Color.green);
//                 break; 
//             }
//             else Debug.DrawRay(transform.position, dir * wallCheckDistance, Color.red);
//         }

//         // 왼쪽 벽 부채꼴 레이캐스트
//         Vector3[] leftDirs = { 
//             -trueRight, 
//             (-trueRight + flatForward).normalized, 
//             (-trueRight - flatForward).normalized 
//         };

//         foreach (Vector3 dir in leftDirs)
//         {
//             if (Physics.Raycast(transform.position, dir, out RaycastHit hit, wallCheckDistance, whatIsWall))
//             {
//                 wallLeft = true;
//                 leftWallhit = hit;
//                 Debug.DrawRay(transform.position, dir * wallCheckDistance, Color.green);
//                 break;
//             }
//             else Debug.DrawRay(transform.position, dir * wallCheckDistance, Color.red);
//         }
//     }

//     private bool AboveGround() => !Physics.Raycast(transform.position, pm.currentGravity.normalized, minJumpHeight, whatIsGround);

//     private void StateMachine()
//     {
//         horizontalInput = Input.GetAxisRaw("Horizontal");
//         verticalInput = Input.GetAxisRaw("Vertical");

//         // 🌟 땅에 닿으면 '방금 탄 벽' 기억을 초기화해서 다시 탈 수 있게 해줍니다.
//         if (!AboveGround())
//         {
//             lastWallNormal = Vector3.zero;
//         }

//         // 현재 감지된 벽의 수직 방향(Normal) 가져오기
//         Vector3 currentWallNormal = Vector3.zero;
//         if (wallRight) currentWallNormal = rightWallhit.normal;
//         else if (wallLeft) currentWallNormal = leftWallhit.normal;

//         // 🌟 방금 점프했던 벽과 각도가 10도 이상 차이 나거나(다른 벽), 바닥을 밟고 새로 뛰는 경우만 true!
//         bool isNewWall = lastWallNormal == Vector3.zero || Vector3.Angle(currentWallNormal, lastWallNormal) > 10f;

//         // 🌟 isNewWall 조건이 충족되어야만 벽을 탑니다!
//         if((wallLeft || wallRight) && verticalInput > 0 && AboveGround() && !exitingWall && isNewWall)
//         {
//             if (!pm.wallrunning) StartWallRun();

//             if (Input.GetKeyDown(jumpKey)) WallJump();
//             if (Input.GetKeyDown(dropKey)) DropWall();
//         }
//         else if (exitingWall)
//         {
//             if (pm.wallrunning) StopWallRun();

//             if (exitWallTimer > 0) exitWallTimer -= Time.deltaTime;
//             if (exitWallTimer <= 0) exitingWall = false;
//         }
//         else
//         {
//             if (pm.wallrunning) StopWallRun();
//         }
//     }

//     private void RemoveGravityVelocity()
//     {
//         Vector3 gravityDir = pm.currentGravity.normalized;
//         rb.linearVelocity = rb.linearVelocity - Vector3.Project(rb.linearVelocity, gravityDir);
//     }

//     private void StartWallRun()
//     {
//         pm.wallrunning = true;
//         RemoveGravityVelocity();

//         cam.DoFov(90f);
//         if (wallLeft) cam.DoTilt(-5f);
//         if (wallRight) cam.DoTilt(5f);
//     }

//     private void WallRunningMovement()
//     {
//         pm.isGraviting = useGravity;
//         Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;
        
//         Vector3 gravityUp = pm.currentGravity == Vector3.down ? Vector3.up : -pm.currentGravity.normalized;
//         Vector3 wallForward = Vector3.Cross(wallNormal, gravityUp);

//         if ((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude)
//             wallForward = -wallForward;

//         rb.AddForce(wallForward * wallRunForce, ForceMode.Force);
//         rb.AddForce(-wallNormal * 100, ForceMode.Force);

//         if (!pm.isGraviting) 
//         {
//             RemoveGravityVelocity();
//         }
//         else
//         {
//             RemoveGravityVelocity(); 
//         }

//         Quaternion targetRotation = Quaternion.LookRotation(wallForward, gravityUp);
//         orientation.rotation = Quaternion.Slerp(orientation.rotation, targetRotation, Time.deltaTime * 15f);
//     }

//     private void StopWallRun()
//     {
//         pm.wallrunning = false;
//         cam.DoFov(80f);
//         cam.DoTilt(0f);

//         if (pm.currentGravity == Vector3.down) 
//         {
//             float playerY = pm.transform.localEulerAngles.y;
//             float camY = cam.yRotation;
            
//             float diff = Mathf.DeltaAngle(playerY, camY);
            
//             cam.yRotation = playerY + (diff / 2f);
//         }
//     }
    
//     private void WallJump()
//     {
//         ExitRoutine();
//         Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;
        
//         // 🌟 벽 점프를 할 때, 방금 발로 찬 벽의 방향(Normal)을 기억해둡니다!
//         lastWallNormal = wallNormal;

//         Vector3 forceToApply = transform.up * wallJumpUpForce + wallNormal * wallJumpSideForce;

//         RemoveGravityVelocity();
//         rb.AddForce(forceToApply, ForceMode.Impulse);
//     }

//     private void DropWall() => ExitRoutine();

//     private void ExitRoutine()
//     {
//         exitingWall = true;
//         exitWallTimer = exitWallTime;
//     }
// }
