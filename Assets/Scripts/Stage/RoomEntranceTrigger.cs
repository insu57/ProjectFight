using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class RoomEntranceTrigger : MonoBehaviour
{
    [Header("입장 판정")]
    [SerializeField, Min(0.01f)] private float _outsideDistance = 0.1f;
    [SerializeField, Min(0.01f)] private float _enterDistance = 0.8f;

    private Door _door;
    private CharacterController _trackedController;
    private PlayerManager _trackedPlayer;
    private bool _approachedFromOutside;

    public void Initialize(Door door)
    {
        // Awake 순서에 의존하지 않고 소유 문에서 명시적으로 연결한다.
        _door = door;
        ClearTracking();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || !_door || !_door.IsConnected)
        {
            return;
        }

        // 공격 판정이나 Hurtbox가 아닌 실제 이동용 콜라이더만 처리한다.
        if (other is not CharacterController controller)
        {
            return;
        }

        if (!controller.TryGetComponent(out PlayerManager player))
        {
            return;
        }

        _trackedController = controller;
        _trackedPlayer = player;
        _approachedFromOutside = false;

        CheckEntry();
    }

    private void OnTriggerStay(Collider other)
    {
        if (other == _trackedController)
        {
            CheckEntry();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != _trackedController)
        {
            return;
        }

        // 마지막 Stay 이후 안쪽으로 벗어난 경우도 최종 위치로 확인한다.
        CheckEntry();
        ClearTracking();
    }

    private void OnDisable()
    {
        ClearTracking();
    }

    private void CheckEntry()
    {
        if (!isActiveAndEnabled || !_trackedPlayer || !_trackedPlayer.isActiveAndEnabled
            || !_door || !_door.isActiveAndEnabled || !_door.IsConnected)
        {
            return;
        }

        // Door의 정면은 복도를 향하므로 반대 방향이 방 안쪽이다.
        Vector3 inwardDirection = -_door.transform.forward;

        // 입장 기준선의 위치는 Trigger 오브젝트의 원점을 사용한다.
        float distance = Vector3.Dot(_trackedPlayer.transform.position - transform.position, inwardDirection);

        if (distance <= -_outsideDistance)
        {
            _approachedFromOutside = true;
            return;
        }

        if (!_approachedFromOutside || distance < _enterDistance)
        {
            return;
        }

        // 같은 통과에서 중복 호출되지 않도록 알림 전에 해제한다.
        _approachedFromOutside = false;
        _door.NotifyPlayerEntered(_trackedPlayer);
    }

    private void ClearTracking()
    {
        _trackedController = null;
        _trackedPlayer = null;
        _approachedFromOutside = false;
    }
}
