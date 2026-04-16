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

    float xRotation;
    float yRotation;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // get mouse input
        float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sensX;
        float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sensY;

        yRotation += mouseX;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        camHolder.localRotation = Quaternion.Euler(xRotation, yRotation, 0);


        // 2. 월러닝 중이 아닐 때만 몸통과 방향을 좌우(Yaw)로 회전시킵니다.
        if (pm != null && !pm.wallrunning)
        {
            // 로컬 회전(localRotation)을 사용하여, 중력으로 인해 몸이 기울어져도 그 기울기를 유지한 채 좌우를 봅니다.
            orientation.localRotation = Quaternion.Euler(0, yRotation, 0);
            player.localRotation = Quaternion.Euler(0, yRotation, 0);
        }

        // rotate cam and orientation
        //camHolder.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        //// --- 수정된 부분: 월러닝 중이 아닐 때만 물리적 방향(몸통)을 업데이트 ---
        //if (pm != null && !pm.wallrunning)
        //{
        //    orientation.rotation = Quaternion.Euler(0, yRotation, 0);
        //    player.rotation = Quaternion.Euler(0, yRotation, 0);
        //}
        // -------------------------------------------------------------
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