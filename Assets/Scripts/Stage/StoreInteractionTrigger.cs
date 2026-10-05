using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class StoreInteractionTrigger : InteractionTriggerBase
{
    [Header("상호작용 안내")]
    [SerializeField] private string _interactionText = "상점 이용";

    public Store Store { get; private set; }
    public override string InteractionText => _interactionText;
    protected override MonoBehaviour InteractionOwner => Store;
    protected override bool CanTrackPlayer => Store && Store.CanInteract;
    protected override bool CanExecuteInteraction => Store && Store.CanInteract;
    protected override bool IsOwnerValidInRange => Range.isTrigger && Store && Store.isActiveAndEnabled;

    public void Initialize(Store store)
    {
        ClearTracking();
        Store = store;
    }

    protected override void ConfigureTriggerComponents()
    {
        Range.isTrigger = true;
        // 상호작용 범위는 물리 힘이나 중력으로 이동하지 않는 Trigger로 사용한다.
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    protected override bool TryExecuteInteraction(PlayerManager player)
    {
        // 성공 후 후보를 유지하여 다음 입력으로 상점을 다시 이용할 수 있게 한다.
        return Store.TryInteract(player);
    }
}
