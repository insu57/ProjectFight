using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class DoorInteractionTrigger : InteractionTriggerBase
{
    [Header("상호작용 안내")]
    [SerializeField] private string _interactionText = "상호작용"; //개선필요.

    public Door Door { get; private set; }
    public override string InteractionText => _interactionText;
    protected override MonoBehaviour InteractionOwner => Door;
    // 잠긴 문도 후보로 유지하여 잠금이 해제되면 안내와 실행을 다시 허용한다.
    protected override bool CanTrackPlayer => Door && Door.IsConnected;
    protected override bool CanExecuteInteraction => Door && Door.CanOpen;

    public void Initialize(Door door)
    {
        ClearTracking();
        // 소유 문이 직접 주입하므로 계층 검색이 필요하지 않다.
        Door = door;
    }

    protected override void ConfigureTriggerComponents()
    {
        Range.isTrigger = true;
    }

    protected override bool TryExecuteInteraction(PlayerManager player)
    {
        return Door.TryOpen();
    }
}
