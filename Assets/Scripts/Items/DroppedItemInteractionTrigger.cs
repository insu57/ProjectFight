using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class DroppedItemInteractionTrigger : InteractionTriggerBase
{
    [SerializeField] private string _interactionText = "아이템 획득";

    public DroppedItem Item { get; private set; }
    public override string InteractionText => _interactionText;
    protected override MonoBehaviour InteractionOwner => Item;
    protected override bool CanTrackPlayer => Item && Item.CanPickUp;
    protected override bool CanExecuteInteraction => Item && Item.CanPickUp;
    protected override bool IsOwnerValidInRange => Range.isTrigger && Item && Item.isActiveAndEnabled;

    public void Initialize(DroppedItem item)
    {
        ClearTracking();
        Item = item;
    }

    protected override void ConfigureTriggerComponents()
    {
        Range.isTrigger = true;
        // 획득 범위는 바닥 아이템을 밀거나 떨어뜨리지 않는 고정 Trigger로 사용한다.
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    protected override bool TryExecuteInteraction(PlayerManager player)
    {
        // 최대 보유량으로 실패하면 후보와 드랍을 유지하여 소모한 뒤 다시 획득할 수 있다.
        return Item.TryPickUp(player);
    }

    protected override void OnInteractionSucceeded()
    {
        ClearTracking();
    }
}
