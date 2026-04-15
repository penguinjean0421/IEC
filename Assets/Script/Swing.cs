using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Swing : MonoBehaviour
{
    [Header("References")]
    public LineRenderer lr;
    public Transform gunTip, cam, player;
    public LayerMask whatIsSwingable;
    public PlayerMovement pm;

    [Header("Swinging")]
    private float maxSwingDistance = 25f;
    private Vector3 swingPoint;
    private SpringJoint joint;

    [Header("OdmGear (Destiny Style)")]
    public Transform orientation;
    public Rigidbody rb;
    public float horizontalThrustForce = 1000f; // 접선(A/D) 가속력
    public float forwardThrustForce = 2000f;    // 중심 견인력
    
    [Header("Prediction")]
    public RaycastHit predictionHit;
    public float predictionSphereCastRadius;
    public Transform predictionPoint;

    private RaycastHit sphereCastHit;
    private RaycastHit raycastHit;

    [Header("Input")]
    public KeyCode swingKey = KeyCode.Mouse0;

    private void Update()
    {
        if (Input.GetKeyDown(swingKey)) StartSwing();
        if (Input.GetKeyUp(swingKey)) StopSwing();

        CheckForSwingPoints();

        // 조작 로직 교체
        if (joint != null) HandleDestinySwing();
    }

    private void LateUpdate()
    {
        DrawRope();
    }

    private void CheckForSwingPoints()
    {
        if (joint != null) return;

        bool hasSphereHit = Physics.SphereCast(cam.position, predictionSphereCastRadius, cam.forward, 
                                out sphereCastHit, maxSwingDistance, whatIsSwingable, QueryTriggerInteraction.Ignore);

        bool hasRayHit = Physics.Raycast(cam.position, cam.forward, 
                                out raycastHit, maxSwingDistance, whatIsSwingable, QueryTriggerInteraction.Ignore);
        Vector3 realHitPoint;

        if (raycastHit.point != Vector3.zero)
            realHitPoint = raycastHit.point;
        else if (sphereCastHit.point != Vector3.zero)
            realHitPoint = sphereCastHit.point;
        else
            realHitPoint = Vector3.zero;

        if (realHitPoint != Vector3.zero)
        {
            predictionPoint.gameObject.SetActive(true);
            predictionPoint.position = realHitPoint;
        }
        else
        {
            predictionPoint.gameObject.SetActive(false);
        }

        predictionHit = raycastHit.point == Vector3.zero ? sphereCastHit : raycastHit;
    }

    private void StartSwing()
    {
        if (predictionHit.point == Vector3.zero) return;

        if(GetComponent<Grappling>() != null)
            GetComponent<Grappling>().StopGrapple();
        
        pm.ResetRestrictions();
        pm.swinging = true;

        swingPoint = predictionHit.point;
        joint = player.gameObject.AddComponent<SpringJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedAnchor = swingPoint;

        // 1. 현재 거리 측정
        float actualDistance = Vector3.Distance(player.position, swingPoint);

        // 2. 바깥으로 나가는 것만 막고(max), 안으로 들어오는 건 허용(min=0)
        joint.maxDistance = actualDistance;
        joint.minDistance = 0f;

        // 3. 유연하지만 강하게 당겨지는 탄성 세팅
        joint.spring = 1500f;
        joint.damper = 100f;
        joint.massScale = 5f;

        lr.positionCount = 2;
        currentGrapplePosition = gunTip.position;
    }

    public void StopSwing()
{
    pm.swinging = false;
    lr.positionCount = 0;

    if (joint != null)
    {
        Destroy(joint);
        joint = null;

        // 폭발적 Y축 점프 해결: 거대한 forwardThrustForce 참조를 끊고, 작은 부스트 값으로 독립
        float releaseBoost = 10f; // 필요 없다면 0으로 두어 순수 관성만 유지 가능
        rb.AddForce(cam.forward * releaseBoost, ForceMode.Impulse);
    }
}

    private void HandleDestinySwing()
{
    Vector3 ropeDir = (swingPoint - transform.position).normalized;

    // 1. 윈치(Winch) 견인 로직: 길이를 강제로 줄여 무조건 끌려가게 만듦
    float pullSpeed = 15f; // 기둥으로 당겨지는 속도 (테스트하며 조절)
    joint.maxDistance -= pullSpeed * Time.deltaTime;
    if(joint.maxDistance < 0) joint.maxDistance = 0; // 최소치 방어

    // 기본 당김 가속력 (데스티니의 부드러운 전진 가속)
    rb.AddForce(ropeDir * forwardThrustForce * Time.deltaTime, ForceMode.Acceleration);

    // 2. A/D 가속 부스트 (방향 반전 완벽 해결)
    float horizontalInput = Input.GetAxisRaw("Horizontal");
    if (Mathf.Abs(horizontalInput) > 0.1f)
    {
        // 카메라 우측 축(cam.right)을 밧줄 평면에 투영하여 직관적인 좌/우 조향 축 생성
        Vector3 lateralDir = Vector3.ProjectOnPlane(cam.right, ropeDir).normalized;
        
        rb.AddForce(lateralDir * horizontalInput * horizontalThrustForce * Time.deltaTime, ForceMode.Acceleration);
    }
}

    private Vector3 currentGrapplePosition;

    private void DrawRope()
    {
        if (!joint) return;

        currentGrapplePosition = 
            Vector3.Lerp(currentGrapplePosition, swingPoint, Time.deltaTime * 8f);

        lr.SetPosition(0, gunTip.position);
        lr.SetPosition(1, currentGrapplePosition);
    }
}