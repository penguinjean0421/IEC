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

    private Vector3 currentGravity = Vector3.down;
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

    private MovementState lastState;
    private bool keepMomentum;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
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
                StopAllCoroutines();
                StartCoroutine(SmoothlyLerpMoveSpeed());
            }
            else
            {
                StopAllCoroutines();
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

        // calculate movement direction
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        // on slope
        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection(moveDirection) * moveSpeed * 20f, ForceMode.Force);

            if (rb.linearVelocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
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

        //reset y vel
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

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
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
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
            Vector3 wallNormal = collision.contacts[0].normal;
            currentGravity = -wallNormal;

            StartCoroutine(RotatePlayerToNewGravity(wallNormal));

            StopCoroutine(nameof(RotatePlayerToSurface)); 
            StartCoroutine(RotatePlayerToSurface(wallNormal));
        }

    }


    IEnumerator RotatePlayerToNewGravity(Vector3 newUp)
    {
        // 플레이어의 현재 회전값
        Quaternion startRotation = transform.rotation;

        // 현재 위쪽(transform.up)을 새로운 위쪽(newUp = 벽의 법선 벡터)으로 맞추는 회전값 계산
        // 기존의 시야 방향(Forward)을 최대한 유지하기 위해 현재 회전값을 곱해줌
        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, newUp) * transform.rotation;

        float time = 0f;
        float duration = 0.5f; // 시점이 돌아가는 데 걸리는 시간 (멀미 방지용 보간)

        while (time < 1f)
        {
            time += Time.deltaTime / duration;
            // 구면 선형 보간(Slerp)으로 부드러운 회전 적용
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, time);
            yield return null;
        }

        transform.rotation = targetRotation; // 오차 보정을 위해 최종값 강제 삽입
    }

    private IEnumerator RotatePlayerToSurface(Vector3 targetUp)
    {
        // 1. 현재 회전 상태 저장
        Quaternion startRotation = transform.rotation;

        // 2. 목표 회전 상태 계산: 현재의 '위쪽(transform.up)'을 '목표 위쪽(targetUp)'으로 맞춤
        // * transform.rotation을 곱해주는 이유는 현재 바라보고 있는 시야(앞쪽)를 최대한 유지하기 위함입니다.
        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, targetUp) * transform.rotation;

        float time = 0f;
        float duration = 0.4f; // 회전하는 데 걸리는 시간 (멀미가 나면 늘리고, 답답하면 줄이세요)

        // 3. duration 시간 동안 부드럽게(Slerp) 회전
        while (time < 1f)
        {
            time += Time.deltaTime / duration;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, time);
            yield return null;
        }

        // 4. 오차 보정을 위해 마지막에 정확한 값으로 강제 고정
        transform.rotation = targetRotation;
    }

}