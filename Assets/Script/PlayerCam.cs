using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class PlayerCam : MonoBehaviour
{
    public float sensX;
    public float sensY;

    public Transform orientation;
    public Transform camHolder;

    public Transform player;

    public PlayerMovement pm;

    public float xRotation;
    public float yRotation;

    // ✅ 중력 기준으로 계산한 월드 시선 방향 (Grappling 등 외부에서 참조)
    public Vector3 WorldAimDirection { get; private set; }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (pm != null && pm.isTransitioning) return;

        // get mouse input
        float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sensX;
        float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sensY;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // 1. 카메라는 위아래(X)와 좌우(Y) 모두 똑같이 적용
        camHolder.localRotation = Quaternion.Euler(xRotation, yRotation, 0);

        // 2. 방향 기준과 플레이어 그래픽도 좌우(Y)를 강제 고정
        if (pm != null && !pm.wallrunning)
        {
            orientation.localRotation = Quaternion.Euler(0, yRotation, 0);
            player.localRotation = Quaternion.Euler(0, yRotation, 0);
        }

        // ✅ 현재 중력 기준 월드 시선 방향 직접 계산
        if (pm != null)
        {
            Vector3 up = -pm.currentGravity.normalized;

            // up 벡터와 거의 평행하지 않은 기준 벡터 선택 (안전한 Cross 계산을 위해)
            Vector3 reference = (Mathf.Abs(Vector3.Dot(up, Vector3.forward)) < 0.9f)
                ? Vector3.forward
                : Vector3.right;

            // ✅ 올바른 Cross 순서: Cross(up, reference) → right
            Vector3 right = Vector3.Cross(up, reference).normalized;

            // ✅ 올바른 Cross 순서: Cross(right, up) → forward  (이전 코드에서 둘 다 반대였음)
            Vector3 forward = Vector3.Cross(right, up).normalized;

            // yRotation: up축 기준 수평 회전
            Quaternion yRot = Quaternion.AngleAxis(yRotation, up);
            forward = yRot * forward;
            right = yRot * right;

            // xRotation: right축 기준 수직 회전
            Quaternion xRot = Quaternion.AngleAxis(xRotation, right);
            WorldAimDirection = xRot * forward;
        }
    }

    public void DoFov(float endValue)
    {
        GetComponent<Camera>().DOFieldOfView(endValue, 0.25f);
    }

    public void DoTilt(float zTilt)
    {
        transform.DOLocalRotate(new Vector3(0, 0, zTilt), 0.25f);
    }
}
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using DG.Tweening;

// public class PlayerCam : MonoBehaviour
// {
//     public float sensX;
//     public float sensY;

//     public Transform orientation;
//     public Transform camHolder;

//     public Transform player;

//     public  PlayerMovement pm;

//     public float xRotation;
//     public float yRotation;

//     private void Start()
//     {
//         Cursor.lockState = CursorLockMode.Locked;
//         Cursor.visible = false;
//     }

//     private void Update()
//     {
//         if (pm != null && pm.isTransitioning) return; // 코루틴 도중 간섭 차단

//         float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sensX;
//         float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sensY;

//         yRotation += mouseX;
//         xRotation -= mouseY;
//         xRotation = Mathf.Clamp(xRotation, -90f, 90f);

//         // 🌟 1. 월드 회전(rotation)을 부모 기준(localRotation)으로 바꿔야 누운 상태가 유지됩니다!
//         camHolder.localRotation = Quaternion.Euler(xRotation, yRotation, 0);
//         orientation.localRotation = Quaternion.Euler(0, yRotation, 0);

//         // 🚨 2. (가장 중요) 아래처럼 player(루트 오브젝트)를 돌리는 코드가 있다면 무조건 지우세요!!!
//         // player.rotation = Quaternion.Euler(0, yRotation, 0); <- 캡슐이 눕지 못하게 막는 최악의 범인!
//         // player.localRotation = Quaternion.Euler(0, yRotation, 0); <- 이것도 지우세요!
        
//         // (선택) 만약 캐릭터 그래픽(PlayerObj)을 돌려야 한다면 localRotation을 씁니다.
//         // if (playerObj != null) playerObj.localRotation = Quaternion.Euler(0, yRotation, 0);
//     }

//     public void DoFov(float endValue)
//     {
//         GetComponent<Camera>().DOFieldOfView(endValue, 0.25f);
//     }

//     public void DoTilt(float zTilt)
//     {
//         transform.DOLocalRotate(new Vector3(0, 0, zTilt), 0.25f);
//     }
// }