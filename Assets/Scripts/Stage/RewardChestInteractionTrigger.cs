using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class RewardChestInteractionTrigger : MonoBehaviour, IInteractable
{
    [Header("상호작용 안내")]
    [SerializeField] private string _interactionText = "상자 열기";

    private BoxCollider _range;
    private CharacterController _trackedController;
    private PlayerManager _trackedPlayer;

    public RewardChest Chest { get; private set; }
    public Vector3 InteractionPosition => Chest ? Chest.transform.position : transform.position;
    public string InteractionText => _interactionText;

    private void Awake()
    {
        _range = GetComponent<BoxCollider>();
    }

    public void Initialize(RewardChest chest)
    {
        ClearTracking();
        Chest = chest;
    }

    private void OnTriggerEnter(Collider other)
    {
        TrackPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // 범위 안에서 재활성화되거나 소유자가 초기화된 경우에도 등록을 복구한다.
        TrackPlayer(other);
    }

    private void TrackPlayer(Collider other)
    {
        if (!Chest || !Chest.CanOpen || other is not CharacterController controller)
        {
            return;
        }

        if (controller != _trackedController)
        {
            if (!controller.TryGetComponent(out PlayerManager player))
            {
                return;
            }

            ClearTracking();
            _trackedController = controller;
            _trackedPlayer = player;
        }

        if (_trackedPlayer)
        {
            _trackedPlayer.RegisterInteraction(this);
        }
    }

    public bool IsPlayerInRange(PlayerManager player)
    {
        if (!isActiveAndEnabled || !Chest || !Chest.isActiveAndEnabled || !_trackedController || !_trackedController.enabled)
        {
            return false;
        }

        if (!player || !player.isActiveAndEnabled || player != _trackedPlayer)
        {
            return false;
        }

        // 실제 Box 범위를 재검사해 텔레포트나 놓친 Exit 뒤의 오래된 등록을 거른다.
        Vector3 playerCenter = _trackedController.transform.TransformPoint(_trackedController.center);
        Vector3 localCenter = transform.InverseTransformPoint(playerCenter) - _range.center;
        Vector3 halfSize = _range.size * 0.5f;
        return Mathf.Abs(localCenter.x) <= halfSize.x && Mathf.Abs(localCenter.y) <= halfSize.y && Mathf.Abs(localCenter.z) <= halfSize.z;
    }

    public bool CanInteract(PlayerManager player)
    {
        return IsPlayerInRange(player) && Chest.CanOpen;
    }

    public bool TryInteract(PlayerManager player)
    {
        // 입력 직전의 범위와 개방 가능 상태를 다시 확인한다.
        if (!CanInteract(player) || !Chest.TryOpen(player))
        {
            return false;
        }

        // 열린 상자는 즉시 후보에서 빼고 다른 대상의 안내로 갱신한다.
        ClearTracking();
        return true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == _trackedController)
        {
            ClearTracking();
        }
    }

    private void OnDisable()
    {
        ClearTracking();
    }

    private void ClearTracking()
    {
        if (_trackedPlayer)
        {
            _trackedPlayer.UnregisterInteraction(this);
        }

        _trackedController = null;
        _trackedPlayer = null;
    }
}
