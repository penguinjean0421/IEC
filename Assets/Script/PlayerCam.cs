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

    public  PlayerMovement pm;

    public float xRotation;
    public float yRotation;

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

        // 지웠던 yRotation을 다시 살려서 마우스 입력값을 누적시킵니다.
        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // 1. 카메라는 위아래(X)와 좌우(Y) 모두 똑같이 적용
        camHolder.localRotation = Quaternion.Euler(xRotation, yRotation, 0);

        // 2. 방향 기준과 플레이어 그래픽도 좌우(Y)를 "카메라와 완전히 똑같은 yRotation 값"으로 강제 고정!
        // 이렇게 하면 카메라와 이동 축이 절대 어긋나지 않습니다.
        if (pm != null && !pm.wallrunning)
        {
            orientation.localRotation = Quaternion.Euler(0, yRotation, 0);
            player.localRotation = Quaternion.Euler(0, yRotation, 0);
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