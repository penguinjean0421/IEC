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

    [Header("Dash Limits")]
    public int maxDashes = 1; // 🌟 최대 연속 대시 횟수
    private int dashesLeft;

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
        dashesLeft = maxDashes; // 초기화
    }

    private void Update()
    {
        if (pm != null && pm.isTransitioning) return;

        // 🌟 남은 대시 횟수가 있을 때만 발동
        if (Input.GetKeyDown(dashKey) && dashesLeft > 0 && dashCdTimer <= 0)
            Dash();

        if (dashCdTimer > 0)
            dashCdTimer -= Time.deltaTime;
    }

    private void Dash()
    {
        if (dashCdTimer > 0) return;
        else dashCdTimer = dashCd;

        dashesLeft--; // 🌟 대시 사용 시 횟수 차감
        pm.dashing = true;

        // 대시 중에는 그래플 낙하 감속을 꺼줍니다.
        pm.isDroppingFromGrapple = false; 

        cam.DoFov(dashFov);

        Transform forwardT;

        if (useCameraForward)
            forwardT = playerCam; 
        else
            forwardT = orientation; 

        Vector3 direction = GetDirection(forwardT);
        Vector3 forceToApply = direction * dashForce;

        if (disableGravity)
            pm.isGraviting = false;

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

    private Vector3 GetDirection(Transform forwardT)
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 forwardOnWall = Vector3.ProjectOnPlane(forwardT.forward, pm.currentGravity).normalized;
        Vector3 rightOnWall = Vector3.ProjectOnPlane(forwardT.right, pm.currentGravity).normalized;

        Vector3 direction = new Vector3();

        if (allowAllDirections)
            direction = (forwardOnWall * verticalInput) + (rightOnWall * horizontalInput);
        else
            direction = forwardOnWall;

        if (verticalInput == 0 && horizontalInput == 0)
            direction = forwardOnWall;

        return direction.normalized;
    }

    // 🌟 외부에서 대시 횟수를 다시 채워줄 때 호출하는 함수
    public void ResetDashCount()
    {
        dashesLeft = maxDashes;
    }
}

// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class Dashing : MonoBehaviour
// {
//     [Header("References")]
//     public Transform orientation;
//     public Transform playerCam;
//     private Rigidbody rb;
//     private PlayerMovement pm;

//     [Header("Dashing")]
//     public float dashForce;

//     public float dashDuration;

//     [Header("CameraEffects")]
//     public PlayerCam cam;
//     public float dashFov;

//     [Header("Settings")]
//     public bool useCameraForward = true;
//     public bool allowAllDirections = true;
//     public bool disableGravity = false;
//     public bool resetVel = true;

//     [Header("Cooldown")]
//     public float dashCd;
//     private float dashCdTimer;

//     [Header("Input")]
//     public KeyCode dashKey = KeyCode.E;

//     private void Start()
//     {
//         rb = GetComponent<Rigidbody>();
//         pm = GetComponent<PlayerMovement>();
//     }

//     private void Update()
//     {
//         // 🌟 수정됨: 중력 변환으로 화면이 돌아가는 도중 대시가 나가는 것을 차단!
//         if (pm != null && pm.isTransitioning) return;

//         if (Input.GetKeyDown(dashKey))
//             Dash();

//         if (dashCdTimer > 0)
//             dashCdTimer -= Time.deltaTime;
//     }

//     private void Dash()
//     {
//         if (dashCdTimer > 0) return;
//         else dashCdTimer = dashCd;

//         pm.dashing = true;

//         cam.DoFov(dashFov);

//         Transform forwardT;

//         if (useCameraForward)
//             forwardT = playerCam; /// where you're looking
//         else
//             forwardT = orientation; /// where you're facing (no up or down)

//         Vector3 direction = GetDirection(forwardT);

//         Vector3 forceToApply = direction * dashForce;

//         if (disableGravity)
//             pm.isGraviting = false;

//         delayedForceToApply = forceToApply;
//         Invoke(nameof(DelayedDashForce), 0.025f);

//         Invoke(nameof(ResetDash), dashDuration);
//     }

//     private Vector3 delayedForceToApply;
//     private void DelayedDashForce()
//     {
//         if (resetVel)
//             rb.linearVelocity = Vector3.zero;

//         rb.AddForce(delayedForceToApply, ForceMode.Impulse);
//     }

//     private void ResetDash()
//     {
//         pm.dashing = false;

//         cam.DoFov(80f);

//         if (disableGravity)
//             pm.isGraviting = true;
//     }

//     private Vector3 GetDirection(Transform forwardT)
//     {
//         float horizontalInput = Input.GetAxisRaw("Horizontal");
//         float verticalInput = Input.GetAxisRaw("Vertical");

//         // 🌟 수정됨: 3D 절대 방향이 아닌, 현재 중력(벽면) 기준 평면으로 투영시켜 완벽한 축을 찾습니다.
//         Vector3 forwardOnWall = Vector3.ProjectOnPlane(forwardT.forward, pm.currentGravity).normalized;
//         Vector3 rightOnWall = Vector3.ProjectOnPlane(forwardT.right, pm.currentGravity).normalized;

//         Vector3 direction = new Vector3();

//         if (allowAllDirections)
//             direction = (forwardOnWall * verticalInput) + (rightOnWall * horizontalInput);
//         else
//             direction = forwardOnWall;

//         if (verticalInput == 0 && horizontalInput == 0)
//             direction = forwardOnWall;

//         return direction.normalized;
//     }
// }
