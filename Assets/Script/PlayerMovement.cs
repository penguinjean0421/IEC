using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    private float moveSpeed;
    public float walkSpeed;
    public float sprintSpeed;
    public float slideSpeed;
    public float wallrunSpeed;
    public float swingSpeed;
    public float dashSpeed;
    public float dashSpeedChangeFactor;
    private float speedChangeFactor;

    public Vector3 currentGravity = Vector3.down;
    private float gravityForce = 9.8f * 2;
    public bool isGraviting = true;

    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;

    public float speedIncreaseMultiplier;
    public float slopeIncreaseMultiplier;
    public float groundDrag;

    public float jumpForce;
    public float jumpCooldown;
    public float airMultiplier;
    bool readyToJump;

    [Header("Crouching")]
    public float crouchSpeed;
    public float crouchYScale;
    private float startYScale;

    [Header("Keybinds")]
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl;

    [Header("Ground Check")]
    public float playerHeight;
    public LayerMask whatIsGround;
    bool grounded;

    [Header("Slope Handle")]
    public float maxSlopeAngel;
    private RaycastHit slopeHit;
    private bool exitingSlope;

    [Header("Camera Effects")]
    public PlayerCam cam;
    public float grappleFov = 95f;

    [Header("Basic References")]
    public Transform orientation;

    float horizontalInput;
    float verticalInput;
    Vector3 moveDirection;
    Rigidbody rb;

    public MovementState state;
    public enum MovementState
    {
        freeze, walking, sprinting, wallrunning, crouching, dashing, sliding, grappling, swinging, air,
    }
    
    public bool swinging;
    public bool dashing;
    public bool sliding;
    public bool wallrunning;
    public bool crouching;          
    public bool freeze;
    public bool activeGrapple;
    public bool grappling;

    public bool isTransitioning = false;
    public float lastGravityChangeTime = -1f;

    private Quaternion fixedWallRotation;
    private Coroutine speedCoroutine;
    private Coroutine rotationCoroutine;
    private MovementState lastState;
    private bool keepMomentum;

    // 🌟 [새롭게 추가된 낙하 속도 제어 시스템]
    [Header("Fall Speed Limits")]
    public bool isDroppingFromGrapple = false;
    [Tooltip("일반적으로 높은 곳에서 떨어질 때의 최고 속도 (뚝뚝 끊김 방지용)")]
    public float maxFallSpeed = 25f; 
    [Tooltip("그래플링이 끝난 직후 떨어질 때의 최고 속도 (더 천천히 떨어지도록 설정)")]
    public float maxGrappleFallSpeed = 10f; 

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = false; 

        readyToJump = true;
        startYScale = transform.localScale.y;
    }

    private void Update()
    {
        if (isTransitioning) return; 
        
        grounded = Physics.Raycast(transform.position, currentGravity.normalized, playerHeight * 0.5f + 0.2f, whatIsGround);
        
        if (grounded) 
        {
            rb.linearDamping = groundDrag;
            isDroppingFromGrapple = false; // 땅에 닿으면 그래플 추락 상태 해제

            Dashing dashingScript = GetComponent<Dashing>();
            if (dashingScript != null) dashingScript.ResetDashCount();
        }
        else 
        { 
            // 🌟 Damping을 원상 복구(0)하여, 대시나 점프 시 앞으로 날아가는 관성이 방해받지 않도록 합니다.
            rb.linearDamping = 0f; 
        }

        MyInput();
        SpeedControl();
        StateHandler();
    }

    private void FixedUpdate()
    {
        MovePlayer();

        if (!dashing && !wallrunning && !freeze)
        {
            rb.AddForce(currentGravity * gravityForce, ForceMode.Acceleration);
        }
    }

    private void MyInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        if (Input.GetButton("Jump") && readyToJump && grounded)
        {
            readyToJump = false;
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown);
        }

        if (Input.GetKeyDown(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(currentGravity.normalized * 5f, ForceMode.Impulse);
        }

        if (Input.GetKeyUp(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
            rb.AddForce(currentGravity.normalized * 5f, ForceMode.Impulse);
        }
    }

    private void StateHandler()
    {
        if (swinging) state = MovementState.swinging;
        else if (grappling) state = MovementState.grappling;
        else if (freeze) { state = MovementState.freeze; moveSpeed = 0; rb.linearVelocity = Vector3.zero; }
        else if (dashing) { state = MovementState.dashing; desiredMoveSpeed = dashSpeed; speedChangeFactor = dashSpeedChangeFactor; }
        else if (wallrunning) { state = MovementState.wallrunning; desiredMoveSpeed = wallrunSpeed; }
        else if (sliding)
        {
            state = MovementState.sliding;
            if (OnSlope() && rb.linearVelocity.y < 0.1f) desiredMoveSpeed = slideSpeed;
            else desiredMoveSpeed = sprintSpeed;
        }
        else if (Input.GetKey(crouchKey)) { state = MovementState.crouching; desiredMoveSpeed = crouchSpeed; }
        else if (grounded && Input.GetKey(sprintKey)) { state = MovementState.sprinting; desiredMoveSpeed = sprintSpeed; }
        else if (grounded) { state = MovementState.walking; desiredMoveSpeed = walkSpeed; }
        else state = MovementState.air;

        bool desiredMoveSpeedHasChanged = desiredMoveSpeed != lastDesiredMoveSpeed;
        if (lastState == MovementState.dashing) keepMomentum = true;

        if (desiredMoveSpeedHasChanged)
        {
            if (keepMomentum)
            {
                if (speedCoroutine != null) StopCoroutine(speedCoroutine);
                speedCoroutine = StartCoroutine(SmoothlyLerpMoveSpeed());
            }
            else
            {
                if (speedCoroutine != null) StopCoroutine(speedCoroutine);
                moveSpeed = desiredMoveSpeed;
            }
        }
    }

    private IEnumerator SmoothlyLerpMoveSpeed()
    {
        float time = 0;
        float difference = Mathf.Abs(desiredMoveSpeed - moveSpeed);
        float startValue = moveSpeed;
        float boostFactor = speedChangeFactor;

        while (time < difference)
        {
            moveSpeed = Mathf.Lerp(startValue, desiredMoveSpeed, time / difference);
            time += Time.deltaTime * boostFactor;
            yield return null;
        }

        moveSpeed = desiredMoveSpeed;
        speedChangeFactor = 1f;
        keepMomentum = false;
    }

    private void MovePlayer()
    {
        if (state == MovementState.dashing || state == MovementState.grappling || state == MovementState.swinging) return;

        Vector3 camForward = cam.camHolder.forward;
        Vector3 camRight = cam.camHolder.right;

        Vector3 forwardOnWall = Vector3.ProjectOnPlane(camForward, currentGravity).normalized;
        Vector3 rightOnWall = Vector3.ProjectOnPlane(camRight, currentGravity).normalized;

        moveDirection = (forwardOnWall * verticalInput) + (rightOnWall * horizontalInput);

        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection(moveDirection) * moveSpeed * 20f, ForceMode.Force);
            if (rb.linearVelocity.y > 0)
                rb.AddForce(currentGravity.normalized * -80f, ForceMode.Force); 
        }
        else if (grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        else if (!grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);

        isGraviting = !OnSlope() && !dashing;
    }

    private void SpeedControl()
    {
        if (activeGrapple || dashing || swinging) return;

        float targetMaxSpeed = moveSpeed;

        if (OnSlope() && !exitingSlope)
        {
            if (rb.linearVelocity.magnitude > targetMaxSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * targetMaxSpeed;
        }
        else
        {
            // 🌟 1. 수평(가로) 속도 제한 원상 복구 
            // 벽타기, 대시, 슬라이딩 속도감이 너무 빨라지지 않도록 예전 칼 제어 방식으로 롤백합니다!
            Vector3 flatVel = Vector3.ProjectOnPlane(rb.linearVelocity, currentGravity);

            if (flatVel.magnitude > targetMaxSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * targetMaxSpeed;
                Vector3 gravityVel = rb.linearVelocity - flatVel;
                rb.linearVelocity = limitedVel + gravityVel;
            }

            // 🌟 2. 수직(낙하) 속도 제한 신규 적용
            // 위에서 떨어질 때 속도가 무한정 오르는 것을 막아 '뚝뚝 끊김(Jittering)'을 방지하고 그래플 낙하를 조절합니다.
            Vector3 gravityDir = currentGravity.normalized;
            float fallSpeed = Vector3.Dot(rb.linearVelocity, gravityDir); // 중력 방향(아래)으로의 속도

            // 그래플 직후면 더 낮은 한계 속도(10f) 적용, 평소엔 기본 낙하 한계(25f) 적용
            float limitFallSpeed = isDroppingFromGrapple ? maxGrappleFallSpeed : maxFallSpeed;

            // 떨어지는 속도가 한계를 초과하면 강제로 잘라냅니다. (위로 점프할 때는 영향 없음)
            if (fallSpeed > limitFallSpeed)
            {
                Vector3 nonFallVel = rb.linearVelocity - (gravityDir * fallSpeed);
                rb.linearVelocity = nonFallVel + (gravityDir * limitFallSpeed);
            }
        }
    }

    private void Jump()
    {
        exitingSlope = true;
        Vector3 flatVel = Vector3.ProjectOnPlane(rb.linearVelocity, currentGravity);
        rb.linearVelocity = flatVel;
        rb.AddForce(-currentGravity * jumpForce, ForceMode.Impulse);
    }

    private void ResetJump()
    {
        readyToJump = true;
        exitingSlope = false;
    }

    private bool enableMovementOnNextTouch;
    public void JumpToPosition(Vector3 targetPosition, float trajectoryHeight)
    {
        activeGrapple = true;
        velocityToSet = CalculateJumpVelocity(transform.position, targetPosition, trajectoryHeight);
        SetVelocity(); 
        Invoke(nameof(ResetRestrictions), 3f);
    }

    private Vector3 velocityToSet;
    private void SetVelocity()
    {
        enableMovementOnNextTouch = true;
        rb.useGravity = false;
        rb.linearDamping = 0f; 
        rb.linearVelocity = velocityToSet;
        cam.DoFov(grappleFov);
    }

    public void ResetRestrictions()
    {
        activeGrapple = false;
        cam.DoFov(85f);
    }

    public bool OnSlope()
    {
        if (Physics.Raycast(transform.position, currentGravity.normalized, out slopeHit, playerHeight * 0.5f + 0.3f))
        {
            float angle = Vector3.Angle(-currentGravity.normalized, slopeHit.normal);
            return angle < maxSlopeAngel && angle != 0;
        }
        return false;
    }

    public Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }
    
    private Vector3 CalculateJumpVelocity(Vector3 startPoint, Vector3 endPoint, float trajectoryHeight)
    {
        Vector3 gravityUp = -currentGravity.normalized;
        float gravity = -gravityForce;                                                     
        float displacementY = Vector3.Dot(endPoint - startPoint, gravityUp);               
        Vector3 displacementXZ = Vector3.ProjectOnPlane(endPoint - startPoint, gravityUp); 

        if (trajectoryHeight < 0.1f) trajectoryHeight = 0.1f;
        if (trajectoryHeight < displacementY + 1.0f) trajectoryHeight = displacementY + 1.0f; 

        float time = Mathf.Sqrt(Mathf.Abs(-2 * trajectoryHeight / gravity)) 
                   + Mathf.Sqrt(Mathf.Abs(2 * (displacementY - trajectoryHeight) / gravity));
        
        if (time < 0.01f) time = 0.01f;

        Vector3 velocityY = gravityUp * Mathf.Sqrt(Mathf.Abs(-2 * gravity * trajectoryHeight));      
        Vector3 velocityXZ = displacementXZ / time;

        return velocityXZ + velocityY;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (enableMovementOnNextTouch)
        {
            enableMovementOnNextTouch = false;
            ResetRestrictions();
            GetComponent<Grappling>().StopGrapple();
        }

        if (collision.gameObject.CompareTag("GravityWall"))
        {
            if (Time.time - lastGravityChangeTime < 0.5f) return;

            Vector3 wallNormal = collision.contacts[0].normal;
            Vector3 contactPoint = collision.contacts[0].point;

            if (currentGravity != -wallNormal)
            {
                currentGravity = -wallNormal;
                lastGravityChangeTime = Time.time; 
                rb.linearVelocity = Vector3.zero; 

                if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
                rotationCoroutine = StartCoroutine(RotatePlayerToSurface(wallNormal, contactPoint));
            }
        }
    }

    private IEnumerator RotatePlayerToSurface(Vector3 targetUp, Vector3 contactPoint)
    {
        isTransitioning = true;
        rb.isKinematic = true; 

        wallrunning = false;
        cam.DoFov(80f);
        cam.DoTilt(0f);

        cam.enabled = false; 

        Behaviour cineBrain = cam.GetComponent("CinemachineBrain") as Behaviour;
        if (cineBrain != null) cineBrain.enabled = false;

        Quaternion startBodyRot = transform.rotation;
        Vector3 startPosition = transform.position;
        Quaternion startCamRot = cam.camHolder.rotation; 

        float currentDist = Vector3.Dot(transform.position - contactPoint, targetUp);
        float desiredDist = playerHeight * 0.5f + 0.1f; 
        Vector3 targetPosition = transform.position + targetUp * (desiredDist - currentDist);

        Vector3 currentForward = cam.camHolder.forward;
        Vector3 newForward = Vector3.ProjectOnPlane(currentForward, targetUp).normalized;
        if (newForward.sqrMagnitude < 0.01f) 
            newForward = Vector3.ProjectOnPlane(transform.up, targetUp).normalized;

        fixedWallRotation = Quaternion.LookRotation(newForward, targetUp);
        
        Quaternion targetCamRot;
        if (Mathf.Abs(Vector3.Dot(currentForward, targetUp)) > 0.99f) {
            targetCamRot = fixedWallRotation;
        } else {
            targetCamRot = Quaternion.LookRotation(currentForward, targetUp);
        }

        float time = 0f;
        float duration = 0.35f;

        while (time < 1f)
        {
            time += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(startPosition, targetPosition, time); 
            transform.rotation = Quaternion.Slerp(startBodyRot, fixedWallRotation, time);
            cam.camHolder.rotation = Quaternion.Slerp(startCamRot, targetCamRot, time);
            yield return null;
        }

        transform.position = targetPosition; 
        transform.rotation = fixedWallRotation;
        cam.camHolder.rotation = targetCamRot;

        if (targetUp == Vector3.up) 
        {
            Vector3 finalCamForward = targetCamRot * Vector3.forward;
            float finalYaw = Mathf.Atan2(finalCamForward.x, finalCamForward.z) * Mathf.Rad2Deg;
            float finalPitch = -Mathf.Asin(finalCamForward.y) * Mathf.Rad2Deg;

            cam.yRotation = finalYaw / 2f; 
            cam.xRotation = finalPitch;
        }
        else 
        {
            cam.xRotation = 0f;
            cam.yRotation = 0f;
        }

        if (cineBrain != null) cineBrain.enabled = true; 
        cam.enabled = true; 
        rb.isKinematic = false; 
        isTransitioning = false;
    }

    private void LateUpdate()
    {
        if (!isTransitioning && currentGravity != Vector3.down)
        {
            transform.rotation = fixedWallRotation;
        }
    }
}

// using System.Collections;
// using UnityEngine;


// public class PlayerMovement : MonoBehaviour
// {
//     [Header("Movement")]
//     private float moveSpeed;
//     public float walkSpeed;
//     public float sprintSpeed;
//     public float slideSpeed;
//     public float wallrunSpeed;

//     public float swingSpeed;

//     public float dashSpeed;

//     public float dashSpeedChangeFactor;

//     private float speedChangeFactor;

//     public Vector3 currentGravity = Vector3.down;
//     private float gravityForce = 9.8f * 2;
//     public bool isGraviting = true;

//     private float desiredMoveSpeed;
//     private float lastDesiredMoveSpeed;

//     public float speedIncreaseMultiplier;
//     public float slopeIncreaseMultiplier;

//     public float groundDrag;

//     public float jumpForce;
//     public float jumpCooldown;
//     public float airMultiplier;
//     bool readyToJump;

//     [Header("Crouching")]
//     public float crouchSpeed;
//     public float crouchYScale;
//     private float startYScale;

//     [Header("Keybinds")]
//     // public KeyCode jumpKey = KeyCode.Jump;
//     public KeyCode sprintKey = KeyCode.LeftShift;
//     public KeyCode crouchKey = KeyCode.LeftControl;


//     [Header("Ground Check")]
//     public float playerHeight;
//     public LayerMask whatIsGround;
//     bool grounded;

//     [Header("Slope Handle")]
//     public float maxSlopeAngel;
//     private RaycastHit slopeHit;
//     private bool exitingSlope;

//     [Header("Camera Effects")]
//     public PlayerCam cam;
//     public float grappleFov = 95f;



//     [Header("Basic References")]
//     public Transform orientation;

//     float horizontalInput;
//     float verticalInput;

//     Vector3 moveDirection;

//     Rigidbody rb;

//     public MovementState state;
//     public enum MovementState
//     {
//         freeze,
//         walking,
//         sprinting,
//         wallrunning,
//         crouching,
//         dashing,
//         sliding,
//         grappling,
//         swinging,
//         air,
//     }
//     public bool swinging;

//     public bool dashing;

//     public bool sliding;
//     public bool wallrunning;

//     public bool crouching;          

//     public bool freeze;

//     public bool activeGrapple;

//     public bool grappling;

//     public bool isTransitioning = false;
//     public float lastGravityChangeTime = -1f;

//     private Quaternion fixedWallRotation;

//     private Coroutine speedCoroutine;
//     private Coroutine rotationCoroutine;

//     private MovementState lastState;
//     private bool keepMomentum;

//     private void Start()
//     {
//         rb = GetComponent<Rigidbody>();
//         rb.freezeRotation = true;
        
//         rb.useGravity = false; 

//         readyToJump = true;
//         startYScale = transform.localScale.y;
//     }

//     private void Update()
//     {
//         // 🌟 수정됨: 중력 변환 중에는 모든 움직임 및 입력 억제!
//         if (isTransitioning) return; 
        
//         //ground check
//         grounded = Physics.Raycast(transform.position, currentGravity.normalized, playerHeight * 0.5f + 0.2f, whatIsGround);
//         Debug.DrawRay(transform.position, currentGravity.normalized * (playerHeight * 0.5f + 0.2f), Color.red);
//         //handle drag
//         if (grounded) rb.linearDamping = groundDrag;
//         else { rb.linearDamping = 0; }

//         MyInput();
//         SpeedControl();
//         StateHandler();
//     }

//     private void FixedUpdate()
//     {
//         MovePlayer();

//         if (!dashing && !wallrunning && !freeze)
//         {
//             rb.AddForce(currentGravity * gravityForce, ForceMode.Acceleration);
//         }
//     }

//     private void MyInput()
//     {
//         horizontalInput = Input.GetAxisRaw("Horizontal");
//         verticalInput = Input.GetAxisRaw("Vertical");

//         //When to Jump
//         if (Input.GetButton("Jump") && readyToJump && grounded)
//         {
//             readyToJump = false;
//             Jump();
//             Invoke(nameof(ResetJump), jumpCooldown);
//         }

//         // start crouch
//         if (Input.GetKeyDown(crouchKey))
//         {
//             transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
//             rb.AddForce(currentGravity.normalized * 5f, ForceMode.Impulse);
//         }

//         // stop crouch
//         if (Input.GetKeyUp(crouchKey))
//         {
//             transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
//             rb.AddForce(currentGravity.normalized * 5f, ForceMode.Impulse);
//         }
//     }

//     private void StateHandler()
//     {
//         if (swinging)
//         {
//             state = MovementState.swinging;
//         }
//         else if (grappling)
//         {
//             state = MovementState.grappling;
//         }
//         else if (freeze)
//         {
//             state = MovementState.freeze;
//             moveSpeed = 0;
//             rb.linearVelocity = Vector3.zero;
//         }
        
//         // Mode - Dashing
//         else if (dashing)
//         {
//             state = MovementState.dashing;
//             desiredMoveSpeed = dashSpeed;
//             speedChangeFactor = dashSpeedChangeFactor;
//         }
//         // Mode - Wallrunning
//         else if (wallrunning)
//         {
//             state = MovementState.wallrunning;
//             desiredMoveSpeed = wallrunSpeed;
//         }
        

//         // Mode - Sliding
//         else if (sliding)
//         {
//             state = MovementState.sliding;

//             if (OnSlope() && rb.linearVelocity.y < 0.1f)
//             {
//                 desiredMoveSpeed = slideSpeed;
//             }

//             else
//             {
//                 desiredMoveSpeed = sprintSpeed;
//             }
//         }

//         // Mode - Crouching
//         else if (Input.GetKey(crouchKey))
//         {
//             state = MovementState.crouching;
//             desiredMoveSpeed = crouchSpeed;
//         }

//         // Mode - Sprinting
//         else if (grounded && Input.GetKey(sprintKey))
//         {
//             state = MovementState.sprinting;
//             desiredMoveSpeed = sprintSpeed;
//         }

//         // Mode - Walking
//         else if (grounded)
//         {
//             state = MovementState.walking;
//             desiredMoveSpeed = walkSpeed;
//         }

//         // Mode - Air,
//         else
//         {
//             state = MovementState.air;
//         }

//         bool desiredMoveSpeedHasChanged = desiredMoveSpeed != lastDesiredMoveSpeed;
//         if (lastState == MovementState.dashing) keepMomentum = true;

//         if (desiredMoveSpeedHasChanged)
//         {
//             if (keepMomentum)
//             {
//                 if (speedCoroutine != null) StopCoroutine(speedCoroutine);
//                 speedCoroutine = StartCoroutine(SmoothlyLerpMoveSpeed());
//             }
//             else
//             {
//                 if (speedCoroutine != null) StopCoroutine(speedCoroutine);
//                 moveSpeed = desiredMoveSpeed;
//             }
//         }
//     }

//     private IEnumerator SmoothlyLerpMoveSpeed()
//     {
//         float time = 0;
//         float difference = Mathf.Abs(desiredMoveSpeed - moveSpeed);
//         float startValue = moveSpeed;

//         float boostFactor = speedChangeFactor;

//         while (time < difference)
//         {
//             moveSpeed = Mathf.Lerp(startValue, desiredMoveSpeed, time / difference);

//             time += Time.deltaTime * boostFactor;

//             yield return null;
//         }

//         moveSpeed = desiredMoveSpeed;
//         speedChangeFactor = 1f;
//         keepMomentum = false;
//     }


//     private void MovePlayer()
//     {
//         if (state == MovementState.dashing || state == MovementState.grappling || state == MovementState.swinging) return;

//         Vector3 camForward = cam.camHolder.forward;
//         Vector3 camRight = cam.camHolder.right;

//         Vector3 forwardOnWall = Vector3.ProjectOnPlane(camForward, currentGravity).normalized;
//         Vector3 rightOnWall = Vector3.ProjectOnPlane(camRight, currentGravity).normalized;

//         moveDirection = (forwardOnWall * verticalInput) + (rightOnWall * horizontalInput);

//         // on slope
//         if (OnSlope() && !exitingSlope)
//         {
//             rb.AddForce(GetSlopeMoveDirection(moveDirection) * moveSpeed * 20f, ForceMode.Force);

//             if (rb.linearVelocity.y > 0)
//                 rb.AddForce(currentGravity.normalized * -80f, ForceMode.Force); 
//         }

//         // on ground
//         else if (grounded)
//             rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);

//         // in air
//         else if (!grounded)
//             rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);

//         // turn gravity off while on slope
//         isGraviting = !OnSlope() && !dashing;
//     }


//     private void SpeedControl()
//     {
//         if (activeGrapple || dashing || swinging) return;

//         float targetMaxSpeed = moveSpeed;

//         if (OnSlope() && !exitingSlope)
//         {
//             if (rb.linearVelocity.magnitude > targetMaxSpeed)
//                 rb.linearVelocity = rb.linearVelocity.normalized * targetMaxSpeed;
//         }
//         else
//         {
//             Vector3 flatVel = Vector3.ProjectOnPlane(rb.linearVelocity, currentGravity);

//             if (flatVel.magnitude > targetMaxSpeed)
//             {
//                 Vector3 limitedVel = flatVel.normalized * targetMaxSpeed;
//                 Vector3 gravityVel = rb.linearVelocity - flatVel;
//                 rb.linearVelocity = limitedVel + gravityVel;
//             }
//         }
//     }

//     private void Jump()
//     {
//         exitingSlope = true;

//         Vector3 flatVel = Vector3.ProjectOnPlane(rb.linearVelocity, currentGravity);
//         rb.linearVelocity = flatVel;

//         rb.AddForce(-currentGravity * jumpForce, ForceMode.Impulse);
//     }

//     private void ResetJump()
//     {
//         readyToJump = true;
//         exitingSlope = false;
//     }

//     private bool enableMovementOnNextTouch;
//     public void JumpToPosition(Vector3 targetPosition, float trajectoryHeight)
//     {
//         activeGrapple = true;

//         velocityToSet = CalculateJumpVelocity(transform.position, targetPosition, trajectoryHeight);
//         SetVelocity(); 

//         Invoke(nameof(ResetRestrictions), 3f);
//     }

//     private Vector3 velocityToSet;
//     private void SetVelocity()
//     {
//         enableMovementOnNextTouch = true;
        
//         rb.useGravity = false;
//         rb.linearDamping = 0f; 

//         rb.linearVelocity = velocityToSet;

//         cam.DoFov(grappleFov);
//     }

//     public void ResetRestrictions()
//     {
//         activeGrapple = false;
//         cam.DoFov(85f);
//     }


//     public bool OnSlope()
//     {
//         if (Physics.Raycast(transform.position, currentGravity.normalized, out slopeHit, playerHeight * 0.5f + 0.3f))
//         {
//             float angle = Vector3.Angle(-currentGravity.normalized, slopeHit.normal);
//             return angle < maxSlopeAngel && angle != 0;
//         }
//         return false;
//     }

//     public Vector3 GetSlopeMoveDirection(Vector3 direction)
//     {
//         return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
//     }

    
//     private Vector3 CalculateJumpVelocity(Vector3 startPoint, Vector3 endPoint, float trajectoryHeight)
//     {
//         Vector3 gravityUp = -currentGravity.normalized;
//         float gravity = -gravityForce;                                                     
//         float displacementY = Vector3.Dot(endPoint - startPoint, gravityUp);               
//         Vector3 displacementXZ = Vector3.ProjectOnPlane(endPoint - startPoint, gravityUp); 

//         if (trajectoryHeight < displacementY + 1.0f) 
//         {
//             trajectoryHeight = displacementY + 1.0f; 
//         }

//         float time = Mathf.Sqrt(-2 * trajectoryHeight / gravity) + Mathf.Sqrt(2 * (displacementY - trajectoryHeight) / gravity);
//         Vector3 velocityY = gravityUp * Mathf.Sqrt(-2 * gravity * trajectoryHeight);      
//         Vector3 velocityXZ = displacementXZ / time;

//         return velocityXZ + velocityY;
//     }


//     private void OnCollisionEnter(Collision collision)
//     {
//         if (enableMovementOnNextTouch)
//         {
//             enableMovementOnNextTouch = false;
//             ResetRestrictions();
//             GetComponent<Grappling>().StopGrapple();
//         }

//         if (collision.gameObject.CompareTag("GravityWall"))
//         {
//             if (Time.time - lastGravityChangeTime < 0.5f) return;

//             Vector3 wallNormal = collision.contacts[0].normal;
//             Vector3 contactPoint = collision.contacts[0].point;

//             if (currentGravity != -wallNormal)
//             {
//                 currentGravity = -wallNormal;
//                 lastGravityChangeTime = Time.time; 
                
//                 rb.linearVelocity = Vector3.zero; 

//                 if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
//                 rotationCoroutine = StartCoroutine(RotatePlayerToSurface(wallNormal, contactPoint));
//             }
//         }
//     }


//     private IEnumerator RotatePlayerToSurface(Vector3 targetUp, Vector3 contactPoint)
//     {
//         isTransitioning = true;
//         rb.isKinematic = true; 

//         // 🌟 WallRunning 간섭 완벽 차단
//         wallrunning = false;
//         cam.DoFov(80f);
//         cam.DoTilt(0f);

//         cam.enabled = false; 

//         Behaviour cineBrain = cam.GetComponent("CinemachineBrain") as Behaviour;
//         if (cineBrain != null) cineBrain.enabled = false;

//         Quaternion startBodyRot = transform.rotation;
//         Vector3 startPosition = transform.position;
//         Quaternion startCamRot = cam.camHolder.rotation; 

//         float currentDist = Vector3.Dot(transform.position - contactPoint, targetUp);
//         float desiredDist = playerHeight * 0.5f + 0.1f; 
//         Vector3 targetPosition = transform.position + targetUp * (desiredDist - currentDist);

//         Vector3 currentForward = cam.camHolder.forward;
//         Vector3 newForward = Vector3.ProjectOnPlane(currentForward, targetUp).normalized;
//         if (newForward.sqrMagnitude < 0.01f) 
//         {
//             newForward = Vector3.ProjectOnPlane(transform.up, targetUp).normalized;
//         }

//         fixedWallRotation = Quaternion.LookRotation(newForward, targetUp);
        
//         Quaternion targetCamRot;
//         if (Mathf.Abs(Vector3.Dot(currentForward, targetUp)) > 0.99f) {
//             targetCamRot = fixedWallRotation;
//         } else {
//             targetCamRot = Quaternion.LookRotation(currentForward, targetUp);
//         }

//         float time = 0f;
//         float duration = 0.35f;

//         while (time < 1f)
//         {
//             time += Time.deltaTime / duration;
            
//             transform.position = Vector3.Lerp(startPosition, targetPosition, time); 
//             transform.rotation = Quaternion.Slerp(startBodyRot, fixedWallRotation, time);
            
//             // 트랜지션 중에는 강제로 카메라의 월드 각도를 돌려줍니다.
//             cam.camHolder.rotation = Quaternion.Slerp(startCamRot, targetCamRot, time);

//             yield return null;
//         }

//         transform.position = targetPosition; 
//         transform.rotation = fixedWallRotation;
//         cam.camHolder.rotation = targetCamRot;

//         if (targetUp == Vector3.up) 
//         {
//             Vector3 finalCamForward = targetCamRot * Vector3.forward;
//             float finalYaw = Mathf.Atan2(finalCamForward.x, finalCamForward.z) * Mathf.Rad2Deg;
//             float finalPitch = -Mathf.Asin(finalCamForward.y) * Mathf.Rad2Deg;

//             // 🌟 [최종 마법의 코드] 이중 회전(Double Yaw) 버그 완벽 보정!
//             // 유저님의 하이어라키 구조상 yRotation이 player와 camHolder에 두 번 들어가서 실제 화면은 2배로 돌아갑니다.
//             // 따라서 우리가 구한 '진짜 월드 각도(finalYaw)'의 절반(/2f)만 넣어주어야 화면이 튀지 않고 딱 맞게 떨어집니다!
//             cam.yRotation = finalYaw / 2f;
            
//             // xRotation(위아래 고개)은 camHolder에만 적용되므로 그대로 넣어줍니다.
//             cam.xRotation = finalPitch;
//         }
//         else 
//         {
//             // 벽에 붙었을 때는 로컬 좌표계를 0으로 초기화
//             cam.xRotation = 0f;
//             cam.yRotation = 0f;
//         }

//         if (cineBrain != null) cineBrain.enabled = true; 
//         cam.enabled = true; 

//         rb.isKinematic = false; 
//         isTransitioning = false;
//     }

//     private void LateUpdate()
//     {
//         if (!isTransitioning && currentGravity != Vector3.down)
//         {
//             transform.rotation = fixedWallRotation;
//         }
//     }
// }
