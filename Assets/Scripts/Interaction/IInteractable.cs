using UnityEngine;

// MonoBehaviour가 구현하며, 후보 선택과 입력 실행은 PlayerManager가 중개한다.
public interface IInteractable
{
    public Vector3 InteractionPosition { get; }
    public string InteractionText { get; }

    // 일시적으로 실행 불가능해도 범위 안에 있는 후보는 유지한다.
    public bool IsPlayerInRange(PlayerManager player);
    public bool CanInteract(PlayerManager player);
    public bool TryInteract(PlayerManager player);
}
