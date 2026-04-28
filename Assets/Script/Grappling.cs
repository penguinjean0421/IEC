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

        // ✅ 현재 중력 기준 '위' 방향 계산 (중력 반전 시에도 올바른 방향 사용)
        Vector3 gravityUp = -pm.currentGravity.normalized;

        // ✅ 월드 Y 대신 현재 중력 기준으로 '아래 1f' 지점 계산
        Vector3 lowestPoint = transform.position - gravityUp * 1f;

        // ✅ 월드 .y 차이 대신, 중력 up 방향으로의 실제 높이 차이를 내적(Dot)으로 계산
        float grapplePointRelativeYPos = Vector3.Dot(grapplePoint - lowestPoint, gravityUp);
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