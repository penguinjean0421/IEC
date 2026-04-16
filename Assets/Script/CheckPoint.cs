using UnityEngine;

public class CheckPoint : MonoBehaviour
{
    public Color defaultColor = Color.white;
    private Vector3 vectorPoint; // 이거 데드존으로 전송해야지
    private Renderer lastCheckpointRenderer;

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("CheckPoint"))
        {
            /* 이전에 저장된 체크포인트가 있다면 색상을 원래대로 복구*/
            if (lastCheckpointRenderer != null) { lastCheckpointRenderer.material.color = defaultColor; }

            /* 현재 체크포인트 위치 저장*/
            vectorPoint = other.transform.position;
            Debug.Log($"Check Point : {vectorPoint}");

            /*현재 체크포인트의 렌더러를 가져와서 녹색으로 변경*/
            Renderer currentRenderer = other.gameObject.GetComponent<Renderer>();
            currentRenderer.material.color = Color.green;

            /* 4. 이제 이 체크포인트가 마지막(이전) 체크포인트로 설정 */
            lastCheckpointRenderer = currentRenderer;
        }
    }

    public void ReSpawnPlayer(GameObject player)
    {
        player.transform.position = vectorPoint;
    }

}