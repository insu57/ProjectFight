using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class RewardChestInteractionTrigger : InteractionTriggerBase
{
    [Header("상호작용 안내")]
    [SerializeField] private string _interactionText = "상자 열기";

    public RewardChest Chest { get; private set; }
    public override string InteractionText => _interactionText;
    protected override MonoBehaviour InteractionOwner => Chest;
    protected override bool CanTrackPlayer => Chest && Chest.CanOpen;
    protected override bool CanExecuteInteraction => Chest && Chest.CanOpen;
    protected override bool IsOwnerValidInRange => Chest && Chest.isActiveAndEnabled;

    public void Initialize(RewardChest chest)
    {
        ClearTracking();
        Chest = chest;
    }

    protected override bool TryExecuteInteraction(PlayerManager player)
    {
        return Chest.TryOpen(player);
    }

    protected override void OnInteractionSucceeded()
    {
        // 열린 상자는 즉시 후보에서 빼고 다른 대상의 안내로 갱신한다.
        ClearTracking();
    }
}
