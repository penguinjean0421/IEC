using UnityEngine;

public class DeadZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 체크포인트에서 부활
            other.GetComponent<PlayerRespawn>().ReSpawn();
        }
    }
}