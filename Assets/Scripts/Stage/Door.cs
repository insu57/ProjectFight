using UnityEngine;

public enum DoorDirection
{
    North,
    East,
    South,
    West
}

public class Door : MonoBehaviour
{
    [field: SerializeField] public DoorDirection Direction { get; private set; }

    [SerializeField] private GameObject _normalWall;
    [SerializeField] private GameObject _doorWall;

    [Header("입장 감지")]
    [SerializeField] private RoomEntranceTrigger _entranceTrigger;

    [Header("문 상호작용")]
    [SerializeField] private DoorInteractionTrigger _interactionTrigger;

    [Header("슬라이드 문 (Door의 로컬 X축 기준)")]
    [SerializeField] private Transform _leftLeaf;
    [SerializeField] private Transform _rightLeaf;
    [SerializeField, Min(0.01f)] private float _slideDistance = 1.75f;
    [SerializeField, Min(0.01f)] private float _openDuration = 0.6f;

    private Room _room;
    private Vector3 _leftClosedPosition;
    private Vector3 _rightClosedPosition;
    private Vector3 _leftOpenPosition;
    private Vector3 _rightOpenPosition;
    private float _openProgress;
    private bool _leafPositionsCached;

    public Door ConnectedDoor { get; private set; }
    public bool IsConnected => ConnectedDoor != null;
    public bool IsOpening { get; private set; }
    public bool IsOpen { get; private set; }
    public bool IsOpeningBlocked { get; private set; }
    public bool CanOpen => isActiveAndEnabled && IsConnected && !IsOpening && !IsOpen && !IsOpeningBlocked && _leftLeaf && _rightLeaf;

    private void Awake()
    {
        CacheLeafPositions();
        // 스테이지를 다시 생성하더라도 소켓은 항상 닫힌 상태에서 시작한다.
        SetWallState(false);
    }

    public void Initialize(Room room)
    {
        _room = room;

        // 계층 검색 대신 프리팹에 지정한 Trigger에 소유 문을 전달한다.
        if (_entranceTrigger)
        {
            _entranceTrigger.Initialize(this);
        }

        if (_interactionTrigger)
        {
            _interactionTrigger.Initialize(this);
        }
    }

    public void SetOpeningBlocked(bool isBlocked)
    {
        // 추후 방의 남은 적 상태를 전달한다. 이미 시작한 개방이나 열린 문은 되돌리지 않는다.
        IsOpeningBlocked = isBlocked;
    }

    public bool TryOpen()
    {
        if (!CanOpen) return false;
        Debug.Log("TryOpen");
        CacheLeafPositions();
        IsOpening = true;
        return true;
    }

    private void Update()
    {
        if (!IsOpening || !IsConnected || !_leftLeaf || !_rightLeaf) return;

        // 코루틴이나 Tween 객체를 만들지 않고 개방 중에만 위치를 갱신한다.
        _openProgress = Mathf.Min(1f, _openProgress + Time.deltaTime / _openDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, _openProgress);
        _leftLeaf.localPosition = Vector3.LerpUnclamped(_leftClosedPosition, _leftOpenPosition, easedProgress);
        _rightLeaf.localPosition = Vector3.LerpUnclamped(_rightClosedPosition, _rightOpenPosition, easedProgress);

        if (_openProgress < 1f) return;

        IsOpening = false;
        IsOpen = true;
    }

    private void CacheLeafPositions()
    {
        if (_leafPositionsCached || !_leftLeaf || !_rightLeaf) return;

        _leftClosedPosition = _leftLeaf.localPosition;
        _rightClosedPosition = _rightLeaf.localPosition;

        // 문짝 부모의 회전/스케일과 무관하게 Door의 좌우 방향으로 같은 거리만큼 연다.
        Vector3 displacement = transform.TransformVector(Vector3.right * _slideDistance);
        _leftOpenPosition = _leftClosedPosition - (_leftLeaf.parent ? _leftLeaf.parent.InverseTransformVector(displacement) : displacement);
        _rightOpenPosition = _rightClosedPosition + (_rightLeaf.parent ? _rightLeaf.parent.InverseTransformVector(displacement) : displacement);
        _leafPositionsCached = true;
    }

    public void NotifyPlayerEntered(PlayerManager player)
    {
        if (!isActiveAndEnabled || !IsConnected || !_room || !player)
        {
            return;
        }

        // 입장한 문 정보와 함께 소유 방에 알린다.
        _room.NotifyPlayerEntered(this, player);
    }

    public bool TryConnect(Door otherDoor) //연결 시도
    {
        if (otherDoor == null || otherDoor == this || IsConnected || otherDoor.IsConnected)
        {
            return false;
        }

        ConnectedDoor = otherDoor;
        otherDoor.ConnectedDoor = this;
        SetWallState(true);
        otherDoor.SetWallState(true);
        return true;
    }

    public void Disconnect() //연결해제
    {
        if (ConnectedDoor == null)
        {
            SetWallState(false);
            return;
        }

        Door other = ConnectedDoor;
        ConnectedDoor = null;
        other.ConnectedDoor = null;
        SetWallState(false);
        other.SetWallState(false);
    }

    public static bool AreOpposite(DoorDirection first, DoorDirection second)
    {
        return first switch
        {
            DoorDirection.North => second == DoorDirection.South,
            DoorDirection.East => second == DoorDirection.West,
            DoorDirection.South => second == DoorDirection.North,
            DoorDirection.West => second == DoorDirection.East,
            _ => false
        };
    }

    private void SetWallState(bool isConnected) //일반 벽 / 문 벽 설정
    {
        if (_normalWall != null)
        {
            _normalWall.SetActive(!isConnected);
        }

        if (_doorWall != null)
        {
            _doorWall.SetActive(isConnected);
        }

        if (_interactionTrigger)
        {
            _interactionTrigger.gameObject.SetActive(isConnected);
        }
    }
}
