using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    private Vector3 lastCheckpointPos;
    private CheckPoint lastCheckpoint;

    void Awake()
    {
        lastCheckpointPos = transform.position;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("CheckPoint"))
        {
            CheckPoint currentCP = other.GetComponent<CheckPoint>();

            // 기존 색상 변경
            if (lastCheckpoint != null) lastCheckpoint.SetActive(false);

            // 현재 위치 저장 및 색상 변경
            lastCheckpointPos = other.transform.position;
            lastCheckpoint = currentCP;
            lastCheckpoint.SetActive(true);

            Debug.Log($"체크포인트 저장: {lastCheckpointPos}");
        }
    }

    public void ReSpawn()
    {
        transform.position = lastCheckpointPos;
    }
}