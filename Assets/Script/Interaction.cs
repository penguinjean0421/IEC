using UnityEngine;

public class Interaction : MonoBehaviour
{
    public InteractionDetector detector { get; private set; }
    private void Awake()
    {
        detector = GetComponent<InteractionDetector>();
    }
    public void InteractWithTarget()
    {
        // 감지기에서 가장 가까운 상호작용 대상을 가져옴
        IInteractable target = detector.GetCurrentTarget();
        if (target == null) return;

        // 상호작용 실행 (자신을 넘겨주어 인벤토리 등을 참조할 수 있게 함)
        target.Interact(detector);
    }
}