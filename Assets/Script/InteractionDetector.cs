using System.Collections.Generic;
using UnityEngine;

public class InteractionDetector : MonoBehaviour
{
    public bool IsInTrigger => currentTriggers.Count > 0;

    // 현재 플레이어 주변에 있는 모든 트리거 리스트
    private List<Collider> currentTriggers = new List<Collider>();

    // 현재 가장 가까운 상호작용 대상 (인터페이스로 관리)
    private IInteractable currentTarget = null;
    public Collider CurrentTrigger { get; private set; } // 플레이어 회전용 좌표 제공

    private void FixedUpdate()
    {
        UpdateNearestTrigger();
    }

    // 외부(Player_Controller)에서 현재 타겟이 무엇인지 확인할 수 있도록 제공
    public IInteractable GetCurrentTarget()
    {
        return currentTarget;
    }

    #region 트리거 감지
    private void OnTriggerEnter(Collider other)
    {
        if (other != null && other.GetComponent<IInteractable>() != null && !currentTriggers.Contains(other))
        {
            currentTriggers.Add(other);
            UpdateNearestTrigger();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other != null && other.GetComponent<IInteractable>() != null && !currentTriggers.Contains(other))
        {
            currentTriggers.Add(other);
        }
        UpdateNearestTrigger();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null)
        {
            currentTriggers.Remove(other);

            // 트리거 존을 완전히 벗어나는 가구는 하이라이트를 즉시 꺼줍니다.
            IInteractable interactable = other.GetComponent<IInteractable>();
            if (interactable != null)
            {
                interactable.TargetHighlight(false);
            }
        }

        UpdateNearestTrigger();
    }
    #endregion

    #region 가장 가까운 타겟 계산 및 하이라이트 토글
    private void UpdateNearestTrigger()
    {
        // Null이거나 비활성화된 오브젝트 정리
        currentTriggers.RemoveAll(trigger => trigger == null || !trigger.gameObject.activeInHierarchy);

        if (currentTriggers.Count == 0)
        {
            // 주변에 아무것도 없으면 기존 타겟의 하이라이트를 끄고 비웁니다.
            if (currentTarget != null) currentTarget.TargetHighlight(false);
            currentTarget = null;
            CurrentTrigger = null;
            return;
        }

        float shortestDistance = float.MaxValue;
        Collider nearestTrigger = null;

        // 1. 가장 가까운 트리거 찾기
        foreach (Collider trigger in currentTriggers)
        {
            if (trigger != null && trigger.gameObject.activeInHierarchy)
            {
                float distance = (trigger.transform.position - transform.position).sqrMagnitude;
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    nearestTrigger = trigger;
                }
            }
        }

        // 2. 타겟 변경 및 하이라이트 교체 처리
        if (nearestTrigger != null)
        {
            IInteractable nextTarget = nearestTrigger.GetComponent<IInteractable>();

            // 가장 가까운 타겟이 이전과 바뀌었을 때만 하이라이트 신호를 보냅니다.
            if (nextTarget != currentTarget)
            {
                // 이전 타겟 하이라이트 끄기
                if (currentTarget != null) currentTarget.TargetHighlight(false);

                // 새 타겟 하이라이트 켜기
                if (nextTarget != null) nextTarget.TargetHighlight(true);

                currentTarget = nextTarget;
                CurrentTrigger = nearestTrigger;
            }
        }
    }
    #endregion
}