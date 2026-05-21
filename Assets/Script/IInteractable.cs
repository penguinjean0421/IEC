public interface IInteractable
{
    // 플레이어가 상호작용 버튼을 눌렀을 때 호출
    void Interact(InteractionDetector player);

    // 플레이어가 근처에 왔을 때 하이라이트 켜기/끄기
    void TargetHighlight(bool isLook);
}