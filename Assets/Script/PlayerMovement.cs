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
    // public KeyCode jumpKey = KeyCode.Jump;
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
        freeze,
        walking,
        sprinting,
        wallrunning,
        crouching,
        dashing,
        sliding,
        grappling,
        swinging,
        air,
    }
    public bool swinging;

    public bool dashing;

    public bool sliding;
    public bool wallrunning;

    public bool crouching;          //이거 영상에 나오던데? 왜 없었지 냠냠

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

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        
        // 🌟 마법의 한 줄: 유니티의 기본 지구 중력을 꺼버립니다!
        // 이제 오직 우리가 만든 'currentGravity'만이 플레이어를 당깁니다.
        rb.useGravity = false; 

        readyToJump = true;
        startYScale = transform.localScale.y;
    }

    private void Update()
    {
        
        //ground check
        grounded = Physics.Raycast(transform.position, currentGravity.normalized, playerHeight * 0.5f + 0.2f, whatIsGround);
        Debug.DrawRay(transform.position, currentGravity.normalized * (playerHeight * 0.5f + 0.2f), Color.red);
        //handle drag
        if (grounded) rb.linearDamping = groundDrag;
        else { rb.linearDamping = 0; }


        MyInput();
        SpeedControl();
        StateHandler();
    }

    private void FixedUpdate()
    {
        MovePlayer();

        if (!dashing || !wallrunning || !grappling || !freeze)
        {
            rb.AddForce(currentGravity * gravityForce, ForceMode.Acceleration);
        }
    }

    private void MyInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        //When to Jump
        if (Input.GetButton("Jump") && readyToJump && grounded)
        {
            readyToJump = false;
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown);
        }

        // start crouch
        if (Input.GetKeyDown(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(currentGravity.normalized * 5f, ForceMode.Impulse);
        }

        // stop crouch
        if (Input.GetKeyUp(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
            rb.AddForce(currentGravity.normalized * 5f, ForceMode.Impulse);
        }
    }

    private void StateHandler()
    {
        if (swinging)
        {
            state = MovementState.swinging;
        }
        else if (grappling)
        {
            state = MovementState.grappling;
        }
        else if (freeze)
        {
            state = MovementState.freeze;
            moveSpeed = 0;
            rb.linearVelocity = Vector3.zero;
        }
        
        // Mode - Dashing
        else if (dashing)
        {
            state = MovementState.dashing;
            desiredMoveSpeed = dashSpeed;
            speedChangeFactor = dashSpeedChangeFactor;
        }
        // Mode - Wallrunning
        else if (wallrunning)
        {
            state = MovementState.wallrunning;
            desiredMoveSpeed = wallrunSpeed;
        }
        

        // Mode - Sliding
        else if (sliding)
        {
            state = MovementState.sliding;

            if (OnSlope() && rb.linearVelocity.y < 0.1f)
            {
                desiredMoveSpeed = slideSpeed;
            }

            else
            {
                desiredMoveSpeed = sprintSpeed;
            }
        }

        // Mode - Crouching
        else if (Input.GetKey(crouchKey))
        {
            state = MovementState.crouching;
            desiredMoveSpeed = crouchSpeed;
        }

        // Mode - Sprinting
        else if (grounded && Input.GetKey(sprintKey))
        {
            state = MovementState.sprinting;
            desiredMoveSpeed = sprintSpeed;
        }

        // Mode - Walking
        else if (grounded)
        {
            state = MovementState.walking;
            desiredMoveSpeed = walkSpeed;
        }

        // Mode - Air,
        else
        {
            state = MovementState.air;
        }

        bool desiredMoveSpeedHasChanged = desiredMoveSpeed != lastDesiredMoveSpeed;
        if (lastState == MovementState.dashing) keepMomentum = true;

        if (desiredMoveSpeedHasChanged)
        {
            if (keepMomentum)
            {
                // 무식한 StopAllCoroutines(); 삭제!!!
                if (speedCoroutine != null) StopCoroutine(speedCoroutine);
                speedCoroutine = StartCoroutine(SmoothlyLerpMoveSpeed());
            }
            else
            {
                // 무식한 StopAllCoroutines(); 삭제!!!
                if (speedCoroutine != null) StopCoroutine(speedCoroutine);
                moveSpeed = desiredMoveSpeed;
            }
        }
    }

    private IEnumerator SmoothlyLerpMoveSpeed()
    {
        // smoothly lerp movementSpeed to desired value
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

        // ⚠️ 기존 코드 삭제 (orientation만 믿고 가는 방식 폐기)
        // moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        // --- 🌟 새로운 완벽한 가변 중력 이동 로직 ---
        // 1. 내 카메라(눈)가 실제로 바라보고 있는 진짜 앞쪽과 오른쪽 벡터를 가져옵니다.
        Vector3 camForward = cam.camHolder.forward;
        Vector3 camRight = cam.camHolder.right;

        // 2. 그 시야 방향을, 현재 서 있는 벽면(currentGravity)에 납작하게 투영(ProjectOnPlane)시킵니다.
        // 이렇게 하면 내가 하늘을 보든 땅을 보든, 벽을 타고 매끄럽게 전진하는 방향만 순수하게 뽑혀 나옵니다.
        Vector3 forwardOnWall = Vector3.ProjectOnPlane(camForward, currentGravity).normalized;
        Vector3 rightOnWall = Vector3.ProjectOnPlane(camRight, currentGravity).normalized;

        // 3. 투영된 완벽한 방향을 기준으로 W,A,S,D 입력을 곱해 최종 이동 방향을 정합니다.
        moveDirection = (forwardOnWall * verticalInput) + (rightOnWall * horizontalInput);
        // ------------------------------------------

        // on slope
        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection(moveDirection) * moveSpeed * 20f, ForceMode.Force);

            if (rb.linearVelocity.y > 0)
                rb.AddForce(currentGravity.normalized * -80f, ForceMode.Force); // (차후 이 down도 currentGravity로 변경 필요)
        }

        // on ground
        else if (grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);

        // in air
        else if (!grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);

        // turn gravity off while on slope
        isGraviting = !OnSlope() && !dashing;
    }


    private void SpeedControl()
{
    // 1. 예외 상태: 속도 제한을 완전히 풀어야 하는 경우 (그래플링, 대시 등)
    if (activeGrapple || dashing || swinging) return;

    // 2. 현재 이동 목표 속도(moveSpeed)를 기준으로 잡되, 
    // 공중에서는 조금 더 여유를 주고 싶다면 별도의 multiplier를 곱할 수 있음
    float targetMaxSpeed = moveSpeed;

    // 3. 경사로 로직 (기존 유지 - 3D 벡터 전체 제한)
    if (OnSlope() && !exitingSlope)
    {
        if (rb.linearVelocity.magnitude > targetMaxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * targetMaxSpeed;
    }
        // 4. 일반 지면 및 공중 (수평 속도 XZ만 제한)
        else
        {
            // 1. 현재 속도를 중력 평면(현재 바닥/벽)에 투영하여 수평 속도(flatVel)만 추출
            Vector3 flatVel = Vector3.ProjectOnPlane(rb.linearVelocity, currentGravity);

            if (flatVel.magnitude > targetMaxSpeed)
            {
                // 2. 수평 속도의 방향은 유지한 채 길이(속력)만 최대치로 제한
                Vector3 limitedVel = flatVel.normalized * targetMaxSpeed;

                // 3. 원래 전체 속도에서 방금 구했던 투영 전 수평 속도를 빼면, 
                // 순수하게 '중력 방향(추락 또는 점프)'의 속도만 남게 됨
                Vector3 gravityVel = rb.linearVelocity - flatVel;

                // 4. 제한된 수평 속도와 원래의 중력 속도를 더하여 최종 속도 적용
                rb.linearVelocity = limitedVel + gravityVel;
            }
        }
    }

    private void Jump()
    {
        exitingSlope = true;

        // 수정: 강제로 y를 0으로 만드는 것(월드 기준)을 삭제하고, 
        // 현재 중력 평면에 투영한 수평 속도만 남겨서 '현재 바닥 기준'의 점프 전 속도를 만듭니다.
        Vector3 flatVel = Vector3.ProjectOnPlane(rb.linearVelocity, currentGravity);
        rb.linearVelocity = flatVel;

        // 수정 완료: 현재의 '위(transform.up)'를 기준으로 점프력 추가
        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);

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
        Invoke(nameof(SetVelocity), 0.1f);

        Invoke(nameof(ResetRestrictions), 3f);
    }

    private Vector3 velocityToSet;
    private void SetVelocity()
    {
        enableMovementOnNextTouch = true;
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
        // Vector3.down 대신 현재 중력 방향(currentGravity.normalized)을 향해 레이저를 쏩니다.
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
        float gravity = Physics.gravity.y;
        float displacementY = endPoint.y - startPoint.y;
        Vector3 displacementXZ = new Vector3(endPoint.x - startPoint.x, 0, endPoint.z - startPoint.z);

        float time = Mathf.Sqrt(-2 * trajectoryHeight / gravity) + Mathf.Sqrt(2 * (displacementY - trajectoryHeight) / gravity);
        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * gravity * trajectoryHeight);
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
            // 🌟 핵심 방어막: 중력이 변한 지 0.5초가 안 지났으면 다리가 바닥을 긁어도 무시합니다!
            if (Time.time - lastGravityChangeTime < 0.5f) return;

            Vector3 wallNormal = collision.contacts[0].normal;
            Vector3 contactPoint = collision.contacts[0].point;

            if (currentGravity != -wallNormal)
            {
                currentGravity = -wallNormal;
                lastGravityChangeTime = Time.time; // 중력이 변한 시간 저장
                
                rb.linearVelocity = Vector3.zero; // 날아오던 관성 완전히 정지

                if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
                rotationCoroutine = StartCoroutine(RotatePlayerToSurface(wallNormal, contactPoint));
            }
        }
    }


    private IEnumerator RotatePlayerToSurface(Vector3 targetUp, Vector3 contactPoint)
    {
        isTransitioning = true;
        rb.isKinematic = true; 

        Behaviour cineBrain = cam.GetComponent("CinemachineBrain") as Behaviour;
        if (cineBrain != null) cineBrain.enabled = false;

        Quaternion startRotation = transform.rotation;
        Vector3 startPosition = transform.position;

        float currentDist = Vector3.Dot(transform.position - contactPoint, targetUp);
        float desiredDist = playerHeight * 0.5f + 0.1f; 
        Vector3 targetPosition = transform.position + targetUp * (desiredDist - currentDist);

        Vector3 currentForward = cam.camHolder.forward;
        Vector3 newForward = Vector3.ProjectOnPlane(currentForward, targetUp).normalized;
        if (newForward.sqrMagnitude < 0.01f) 
        {
            newForward = Vector3.ProjectOnPlane(transform.up, targetUp).normalized;
        }

        // 🌟 핵심 마법: 매 프레임 계산하지 않도록, 가장 완벽한 목표 회전값을 계산해서 '영구 박제' 합니다.
        fixedWallRotation = Quaternion.LookRotation(newForward, targetUp);

        float startX = cam.xRotation;
        float startY = cam.yRotation;
        float time = 0f;
        float duration = 0.35f;

        while (time < 1f)
        {
            time += Time.deltaTime / duration;
            
            // 🌟 박제해둔 fixedWallRotation으로 부드럽게 Slerp 합니다.
            transform.rotation = Quaternion.Slerp(startRotation, fixedWallRotation, time);
            transform.position = Vector3.Lerp(startPosition, targetPosition, time); 

            cam.xRotation = Mathf.Lerp(startX, 0f, time);
            cam.yRotation = Mathf.Lerp(startY, 0f, time);
            yield return null;
        }

        // 🌟 종료 시에도 박제된 값으로 딱 떨어지게 맞춥니다. (오차 방지)
        transform.rotation = fixedWallRotation;
        transform.position = targetPosition; 
        cam.xRotation = 0f;
        cam.yRotation = 0f;

        if (cineBrain != null) cineBrain.enabled = true; 

        rb.isKinematic = false; 
        isTransitioning = false;
    }

    // 🌟 수정된 LateUpdate: 카메라 방향을 보지 않고, 박제된 값으로 몸통만 고정합니다!
    private void LateUpdate()
    {
        // 중력이 바뀌어 벽에 붙은 상태이고, 회전이 끝났다면
        if (!isTransitioning && currentGravity != Vector3.down)
        {
            // 다른 훼방꾼 스크립트들이 몸통을 일으켜 세우려고 발악해도, 
            // 가장 마지막 순간에 무조건 캡슐을 90도로 예쁘게 고정시켜 버립니다!
            transform.rotation = fixedWallRotation;
        }
    }


}