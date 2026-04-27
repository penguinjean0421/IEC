using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grappling : MonoBehaviour
{
    [Header("References")]
    private PlayerMovement pm;
    public Transform cam;
    public Transform gunTip;
    public LayerMask whatIsGrappleable;
    public LineRenderer lr;

    [Header("Grappling")]
    public float maxGrappleDistance;
    public float grappleDelayTime;
    public float overshootYAxis;

    private Vector3 grapplePoint;

    [Header("Cooldown")]
    public float grapplingCd;
    private float grapplingCdTimer;

    [Header("Input")]
    public KeyCode grappleKey = KeyCode.Mouse1;

    [Header("Rope Animation")]
    public int animationQuality = 50;
    public float waveAmplitude = 0.1f;
    public float waveFrequency = 15f;
    public float animationSpeed = 20f;
    public AnimationCurve waveCurve;
    private float animationTimer;

    private bool grappling;

    private void Start()
    {
        pm = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        // ✅ 핵심: 입력 감지만 여기서 하고, 실제 레이캐스트는 코루틴으로 넘김
        // Cinemachine이 LateUpdate에서 카메라를 갱신하므로
        // Update 타이밍에 Camera.main을 읽으면 한 프레임 늦은 방향이 나옴
        if (Input.GetKeyDown(grappleKey) && grapplingCdTimer <= 0)
            StartCoroutine(TryGrapple());

        if (grapplingCdTimer > 0)
            grapplingCdTimer -= Time.deltaTime;
    }

    // ✅ WaitForEndOfFrame: 모든 LateUpdate + Cinemachine 갱신이 끝난 뒤 실행
    // 이 시점의 Camera.main은 실제 렌더링된 화면과 100% 일치함
    private IEnumerator TryGrapple()
    {
        yield return new WaitForEndOfFrame();
        StartGrapple();
    }

    private void LateUpdate()
    {
        if (grappling)
            DrawRopeAnimated();
    }

    private void StartGrapple()
    {
        if (grapplingCdTimer > 0) return;

        // Cinemachine 갱신 완료 후 호출되므로 Camera.main이 정확함
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        RaycastHit hit;
        bool hitSomething = Physics.Raycast(ray.origin, ray.direction, out hit, maxGrappleDistance, whatIsGrappleable);

        Debug.DrawRay(ray.origin, ray.direction * maxGrappleDistance, hitSomething ? Color.green : Color.red, 2f);

        if (!hitSomething)
        {
            Debug.Log("No Target: Grapple Canceled");
            return;
        }

        animationTimer = 0f;
        grappling = true;

        pm.state = PlayerMovement.MovementState.grappling;
        pm.freeze = true;

        Debug.Log("We hit: " + hit.collider.name);
        grapplePoint = hit.point;
        Invoke(nameof(ExecuteGrapple), grappleDelayTime);

        lr.enabled = true;
        lr.positionCount = animationQuality;
        lr.SetPosition(1, grapplePoint);
    }

    private void ExecuteGrapple()
    {
        pm.freeze = false;
        pm.grappling = true;

        Vector3 lowestPoint = new Vector3(transform.position.x, transform.position.y - 1f, transform.position.z);

        float grapplePointRelativeYPos = grapplePoint.y - lowestPoint.y;
        float highestPointOnArc = grapplePointRelativeYPos + overshootYAxis;

        if (grapplePointRelativeYPos < 0) highestPointOnArc = overshootYAxis;

        pm.JumpToPosition(grapplePoint, highestPointOnArc);

        Invoke(nameof(StopGrapple), 1f);
    }

    public void StopGrapple()
    {
        pm.freeze = false;
        pm.grappling = false;
        grappling = false;

        grapplingCdTimer = grapplingCd;

        lr.enabled = false;
        pm.state = PlayerMovement.MovementState.air;
    }

    public bool IsGrappling()
    {
        return grappling;
    }

    public Vector3 GetGrapplePoint()
    {
        return grapplePoint;
    }

    void DrawRopeAnimated()
    {
        if (!grappling) return;

        animationTimer += Time.deltaTime * animationSpeed;
        lr.positionCount = animationQuality;

        float curveValue = waveCurve.Evaluate(animationTimer);

        for (int i = 0; i < animationQuality; i++)
        {
            float delta = i / (float)(animationQuality - 1);

            Vector3 offset = cam.up * Mathf.Sin(delta * waveFrequency) * waveAmplitude * curveValue;

            Vector3 targetPos = Vector3.Lerp(gunTip.position, grapplePoint, delta) + offset;

            lr.SetPosition(i, targetPos);
        }
    }
}
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class Grappling : MonoBehaviour
// {
//     [Header("References")]
//     private PlayerMovement pm;
//     public Transform cam;
//     public Transform gunTip;
//     public LayerMask whatIsGrappleable;
//     public LineRenderer lr;

//     [Header("Grappling")]
//     public float maxGrappleDistance;
//     public float grappleDelayTime;
//     public float overshootYAxis;

//     private Vector3 grapplePoint;

//     [Header("Cooldown")]
//     public float grapplingCd;
//     private float grapplingCdTimer;

//     [Header("Input")]
//     public KeyCode grappleKey = KeyCode.Mouse1;

//     [Header("Rope Animation")]
// public int animationQuality = 50;
// public float waveAmplitude = 0.1f; // 파동의 높낮이
// public float waveFrequency = 15f;  // 파동을 더 촘촘하게 (속도감 상승)
// public float animationSpeed = 20f;  // 1보다 높을수록 애니메이션이 빨리 끝남
// public AnimationCurve waveCurve;
// private float animationTimer;



//     private bool grappling;

//     private void Start()
//     {
//         pm = GetComponent<PlayerMovement>();
//     }

//     private void Update()
//     {
//         if (Input.GetKeyDown(grappleKey)) StartGrapple();

//         if (grapplingCdTimer > 0)
//             grapplingCdTimer -= Time.deltaTime;
//     }

//     private void LateUpdate()
//     {
//         if (grappling)
//             DrawRopeAnimated();
//     }

    
//     private void StartGrapple()
//     {
//     if (grapplingCdTimer > 0) return;

//     // ✅ 최종 확실한 방법: 실제 렌더링하는 카메라로 화면 정중앙 레이
//     Camera playerCamera = pm.cam.GetComponent<Camera>();
//     Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

//     RaycastHit hit;
//     bool hitSomething = Physics.Raycast(ray.origin, ray.direction, out hit, maxGrappleDistance, whatIsGrappleable);

//     Debug.DrawRay(ray.origin, ray.direction * maxGrappleDistance, hitSomething ? Color.green : Color.red, 2f);

//     if (!hitSomething)
//     {
//         Debug.Log("No Target: Grapple Canceled");
//         return;
//     }

//     animationTimer = 0f;
//     grappling = true;
//     pm.state = PlayerMovement.MovementState.grappling;
//     pm.freeze = true;

//     Debug.Log("We hit: " + hit.collider.name);
//     grapplePoint = hit.point;
//     Invoke(nameof(ExecuteGrapple), grappleDelayTime);

//     lr.enabled = true;
//     lr.positionCount = animationQuality;
//     lr.SetPosition(1, grapplePoint);
// }

//     private void ExecuteGrapple()
//     {
//         pm.freeze = false;
//         pm.grappling = true;

//         Vector3 lowestPoint = new Vector3(transform.position.x, transform.position.y - 1f, transform.position.z);

//         float grapplePointRelativeYPos = grapplePoint.y - lowestPoint.y;
//         float highestPointOnArc = grapplePointRelativeYPos + overshootYAxis;

//         if (grapplePointRelativeYPos < 0) highestPointOnArc = overshootYAxis;

//         pm.JumpToPosition(grapplePoint, highestPointOnArc);

//         Invoke(nameof(StopGrapple), 1f);
//     }

//     public void StopGrapple()
//     {
//         pm.freeze = false;
//         pm.grappling = false;
//         grappling = false;

//         // [핵심] 그래플링이 정상적으로 종료되는 이 시점에만 쿨다운을 할당함
//         grapplingCdTimer = grapplingCd;

//         lr.enabled = false;
//         pm.state = PlayerMovement.MovementState.air;
//     }

//     public bool IsGrappling()
//     {
//         return grappling;
//     }

//     public Vector3 GetGrapplePoint()
//     {
//         return grapplePoint;
//     }

//     void DrawRopeAnimated()
//         {
//     if (!grappling) return;

//     // 배속(animationSpeed)을 곱해 타이머를 빠르게 진행시킴
//     animationTimer += Time.deltaTime * animationSpeed;
//     lr.positionCount = animationQuality;

//     // 현재 애니메이션이 곡선의 어디쯤 와있는지 계산 (0 ~ 1)
//     float curveValue = waveCurve.Evaluate(animationTimer);

//     for (int i = 0; i < animationQuality; i++)
//     {
//         float delta = i / (float)(animationQuality - 1);
        
//         // i가 커질수록(벽에 가까울수록) 파동을 줄이려면 i/quality를 활용 가능
//         // 여기서는 전체 진폭에 곡선 값을 곱해 시간이 지나면 사라지게 함
//         Vector3 offset = cam.up * Mathf.Sin(delta * waveFrequency) * waveAmplitude * curveValue;
        
//         Vector3 targetPos = Vector3.Lerp(gunTip.position, grapplePoint, delta) + offset;
        
//         lr.SetPosition(i, targetPos);
//     }
//         }

// }
