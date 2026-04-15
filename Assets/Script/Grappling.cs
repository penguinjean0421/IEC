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
public float waveAmplitude = 0.1f; // 파동의 높낮이
public float waveFrequency = 15f;  // 파동을 더 촘촘하게 (속도감 상승)
public float animationSpeed = 20f;  // 1보다 높을수록 애니메이션이 빨리 끝남
public AnimationCurve waveCurve;
private float animationTimer;



    private bool grappling;

    private void Start()
    {
        pm = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(grappleKey)) StartGrapple();

        if (grapplingCdTimer > 0)
            grapplingCdTimer -= Time.deltaTime;
    }

    private void LateUpdate()
    {
         if (grappling)
            DrawRopeAnimated();
    }

    private void StartGrapple()
    {
        if (grapplingCdTimer > 0) return;

        // [전처리] 레이캐스트로 미리 타격 여부 확인
        RaycastHit hit;
        bool hitSomething = Physics.Raycast(cam.position, cam.forward, out hit, maxGrappleDistance, whatIsGrappleable);

        // 1. 벽에 맞지 않았다면 아무 일도 하지 않고 즉시 종료
        if (!hitSomething) 
        {
            Debug.Log("No Target: Grapple Canceled");
            return; 
        }

        // 2. 여기서부터는 '성공'이 확정된 상태에서만 실행됨
        animationTimer = 0f;
        grappling = true;
        
        // 상태 전환 및 물리 정지
        pm.state = PlayerMovement.MovementState.grappling;
        pm.freeze = true;

        // 타격 지점 저장 및 실행 예약
        Debug.Log("We hit: " + hit.collider.name);
        grapplePoint = hit.point;
        Invoke(nameof(ExecuteGrapple), grappleDelayTime);

        // 라인 렌더러 활성화
        lr.enabled = true;
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

        // [핵심] 그래플링이 정상적으로 종료되는 이 시점에만 쿨다운을 할당함
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

    // 배속(animationSpeed)을 곱해 타이머를 빠르게 진행시킴
    animationTimer += Time.deltaTime * animationSpeed;
    lr.positionCount = animationQuality;

    // 현재 애니메이션이 곡선의 어디쯤 와있는지 계산 (0 ~ 1)
    float curveValue = waveCurve.Evaluate(animationTimer);

    for (int i = 0; i < animationQuality; i++)
    {
        float delta = i / (float)(animationQuality - 1);
        
        // i가 커질수록(벽에 가까울수록) 파동을 줄이려면 i/quality를 활용 가능
        // 여기서는 전체 진폭에 곡선 값을 곱해 시간이 지나면 사라지게 함
        Vector3 offset = cam.up * Mathf.Sin(delta * waveFrequency) * waveAmplitude * curveValue;
        
        Vector3 targetPos = Vector3.Lerp(gunTip.position, grapplePoint, delta) + offset;
        
        lr.SetPosition(i, targetPos);
    }
        }

}
