using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dashing : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform playerCam;
    private Rigidbody rb;
    private PlayerMovement pm;

    [Header("Dashing")]
    public float dashForce;

    public float dashDuration;

    [Header("CameraEffects")]
    public PlayerCam cam;
    public float dashFov;

    [Header("Settings")]
    public bool useCameraForward = true;
    public bool allowAllDirections = true;
    public bool disableGravity = false;
    public bool resetVel = true;

    [Header("Cooldown")]
    public float dashCd;
    private float dashCdTimer;

    [Header("Input")]
    public KeyCode dashKey = KeyCode.E;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        pm = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(dashKey))
            Dash();

        if (dashCdTimer > 0)
            dashCdTimer -= Time.deltaTime;
    }

    // private void Dash()
    // {
    //     if (dashCdTimer > 0) return;
    //     else dashCdTimer = dashCd;

    //     pm.dashing = true;

    //     cam.DoFov(dashFov);

    //     Transform forwardT;

    //     if (useCameraForward)
    //         forwardT = playerCam; /// where you're looking
    //     else
    //         forwardT = orientation; /// where you're facing (no up or down)

    //     Vector3 direction = GetDirection(forwardT);

    //     Vector3 forceToApply = direction * dashForce;

    //     if (disableGravity)
    //         pm.isGraviting = false;

    //     delayedForceToApply = forceToApply;
    //     Invoke(nameof(DelayedDashForce), 0.025f);

    //     Invoke(nameof(ResetDash), dashDuration);
    // }

    private void Dash()
    {
        if (dashCdTimer > 0) return;
        else dashCdTimer = dashCd;

        pm.dashing = true;
        cam.DoFov(dashFov);

        // 1. 기준 트랜스폼 설정 (useCameraForward가 true면 카메라 시선 기준)
        Transform forwardT = useCameraForward ? playerCam : orientation;

        // 2. 방향 계산
        Vector3 direction = GetDirection(forwardT);

        // 3. 힘 계산 및 적용
        Vector3 forceToApply = direction * dashForce;

        if (disableGravity)
            pm.isGraviting = false; // pm의 중력 변수명 오타 주의 (isGraviting -> isGravity 등 확인 필요)

        delayedForceToApply = forceToApply;
        Invoke(nameof(DelayedDashForce), 0.025f);
        Invoke(nameof(ResetDash), dashDuration);
    }

    private Vector3 delayedForceToApply;
    private void DelayedDashForce()
    {
        if (resetVel)
            rb.linearVelocity = Vector3.zero;

        rb.AddForce(delayedForceToApply, ForceMode.Impulse);
    }

    private void ResetDash()
    {
        pm.dashing = false;

        cam.DoFov(80f);

        if (disableGravity)
            pm.isGraviting = true;
    }

    // private Vector3 GetDirection(Transform forwardT)
    // {
    //     float horizontalInput = Input.GetAxisRaw("Horizontal");
    //     float verticalInput = Input.GetAxisRaw("Vertical");

    //     Vector3 direction = new Vector3();

    //     if (allowAllDirections)
    //         direction = forwardT.forward * verticalInput + forwardT.right * horizontalInput;
    //     else
    //         direction = forwardT.forward;

    //     if (verticalInput == 0 && horizontalInput == 0)
    //         direction = forwardT.forward;

    //     return direction.normalized;
    // }

    private Vector3 GetDirection(Transform forwardT)
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 direction = Vector3.zero;

        if (allowAllDirections)
        {
            // 사용자의 입력(WASD)에 따라 방향 결정
            direction = forwardT.forward * verticalInput + forwardT.right * horizontalInput;
        }

        // 입력이 없거나 allowAllDirections가 false면 그냥 정면(시선 방향)
        if (direction == Vector3.zero || !allowAllDirections)
        {
            direction = forwardT.forward;
        }

        direction.y = 0;
        return direction.normalized;
    }

}