using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class DoorInteractionTrigger : MonoBehaviour
{
    private BoxCollider _range;
    private CharacterController _trackedController;
    private PlayerManager _trackedPlayer;

    public Door Door { get; private set; }

    private void Awake()
    {
        _range = GetComponent<BoxCollider>();
        _range.isTrigger = true;

    }

    public void Initialize(Door door)
    {
        ClearTracking();
        // 소유 문이 직접 주입하므로 계층 검색이 필요하지 않다.
        Door = door;
    }

    private void OnTriggerEnter(Collider other)
    {
        TrackPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // 범위 안에서 플레이어가 재활성화되거나 문이 초기화되어도 후보를 복구한다.
        TrackPlayer(other);
    }

    private void TrackPlayer(Collider other)
    {
        if (!Door || !Door.IsConnected || other is not CharacterController controller) return;

        if (controller != _trackedController)
        {
            if (!controller.TryGetComponent(out PlayerManager player)) return;

            ClearTracking();
            _trackedController = controller;
            _trackedPlayer = player;
        }

        if (_trackedPlayer)
        {
            _trackedPlayer.RegisterDoorInteraction(this);
        }
    }

    public bool IsPlayerInRange(PlayerManager player)
    {
        if ( !player || !player.isActiveAndEnabled || player != _trackedPlayer) return false;

        // 문 앞뒤를 같은 Box 범위로 검사한다. 텔레포트 직후의 오래된 등록도 거른다.
        Vector3 playerCenter = _trackedController.transform.TransformPoint(_trackedController.center);
        Vector3 localCenter = transform.InverseTransformPoint(playerCenter) - _range.center;
        Vector3 halfSize = _range.size * 0.5f;
        return Mathf.Abs(localCenter.x) <= halfSize.x && Mathf.Abs(localCenter.y) <= halfSize.y && Mathf.Abs(localCenter.z) <= halfSize.z;
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
            _trackedPlayer.UnregisterDoorInteraction(this);
        }

        _trackedController = null;
        _trackedPlayer = null;
    }
}
