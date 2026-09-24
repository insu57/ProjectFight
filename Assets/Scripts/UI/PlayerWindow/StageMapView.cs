using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class StageMapView : MonoBehaviour, IScrollHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerDownHandler, IPointerClickHandler, ISubmitHandler, IMoveHandler, IDeselectHandler
{
    [Header("References")]
    [SerializeField] private RectTransform _mapRoot;
    [SerializeField] private StageMapRoomView _roomPrefab;
    [SerializeField] private Image _corridorPrefab;

    [Header("Map Layout")]
    // 월드 1유닛을 지도 1유닛으로 사용하며, 화면 배율은 MapRoot에서 적용한다.
    [SerializeField, Min(0.01f)] private float _corridorWorldWidth = 2f;
    [SerializeField, Min(0f)] private float _padding = 24f;

    [Header("Interaction")]
    [SerializeField] private bool _allowInteraction = true;
    [SerializeField, Min(0f)] private float _panPadding = 48f;
    [SerializeField] private UnityEngine.UI.Image _gamepadCursor;

    [Header("Zoom")]
    [SerializeField, Min(0.01f)] private float _minZoom = 3f;
    [SerializeField, Min(0.01f)] private float _maxZoom = 6f;
    [SerializeField, Min(0.01f)] private float _zoomStep = 0.15f;

    [Header("Gamepad")]
    [SerializeField] private InputActionReference _panAction;
    [SerializeField] private InputActionReference _zoomInAction;
    [SerializeField] private InputActionReference _zoomOutAction;
    [SerializeField, Min(0f)] private float _gamepadPanSpeed = 600f;
    [SerializeField, Min(0f)] private float _gamepadZoomSpeed = 1f;
    [SerializeField, Range(0.01f, 1f)] private float _gamepadInputThreshold = 0.15f;

    private readonly List<StageMapRoomView> _roomPool = new();
    private readonly List<RectTransform> _corridorPool = new();

    private StageManager _stageManager;

    // 한 간선이 사용하는 마지막 복도 이미지 다음 인덱스를 저장한다.
    private readonly List<int> _edgeCorridorEnds = new();
    private RectTransform _viewport;
    private StageGraph _displayedGraph;
    private Vector2 _mapCenter;
    private Vector2 _mapSize;
    private float _lineWidth;
    private float _fitScale = 1f;
    private float _zoom = 1f;
    private int _usedCorridors;
    private bool _usingGamepad;
    private bool _pointerInside;
    private bool _suppressClick;
    private bool _selectionDirty = true;
    private bool _selectionSuspended;
    private Vector2 _pointerScreenPosition;
    private Camera _pointerCamera;
    private GameObject _previousSelection;
    private StageMapRoomView _hoveredRoomView;

    public StageNode HoveredRoom { get; private set; }

    // 실제 이동 정책과 실행은 이 선택 확정 알림을 받는 담당자가 처리한다.
    public event System.Action<StageNode> RoomConfirmed;

    private void Awake()
    {
        _viewport = (RectTransform)transform;
        if (_gamepadCursor)
        {
            _gamepadCursor.raycastTarget = false;
            _gamepadCursor.enabled = false;
        }
    }

    private void OnEnable()
    {
        // 표시 전용 미니맵은 지도 조작 액션을 활성화하지 않는다.
        if (_allowInteraction)
        {
            _panAction?.action.Enable();
            _zoomInAction?.action.Enable();
            _zoomOutAction?.action.Enable();
        }

        BindStageManager();
        _selectionSuspended = false;
        _selectionDirty = true;
    }

    private void Update()
    {
        if (!_allowInteraction)
        {
            return;
        }

        if (!_viewport || !_mapRoot || _mapSize.x <= 0f || _mapSize.y <= 0f)
        {
            return;
        }

        // 지도 창이 게임을 일시 정지해도 입력에 따른 이동과 줌은 계속 처리한다.
        float deltaTime = Time.unscaledDeltaTime;
        float zoomIn = _zoomInAction ? _zoomInAction.action.ReadValue<float>() : 0f;
        float zoomOut = _zoomOutAction ? _zoomOutAction.action.ReadValue<float>() : 0f;
        float zoomInput = zoomIn - zoomOut;
        Vector2 panInput = _panAction ? _panAction.action.ReadValue<Vector2>() : Vector2.zero;
        bool hasPan = panInput.sqrMagnitude > _gamepadInputThreshold * _gamepadInputThreshold;
        bool hasZoom = Mathf.Abs(zoomInput) > _gamepadInputThreshold;

        if ((hasPan || hasZoom) && TryFocusGamepad())
        {
            if (hasZoom)
            {
                // 패드 줌도 화면 중앙 커서 아래의 지도 지점을 유지한다.
                ChangeZoom(zoomInput * _gamepadZoomSpeed * deltaTime, Vector2.zero);
            }

            if (hasPan)
            {
                _mapRoot.anchoredPosition += panInput * _gamepadPanSpeed * deltaTime;
                ClampMapPosition();
            }
        }

        // 연결 상태가 아닌 실제 포인터 움직임으로 입력 방식을 전환한다.
        if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude >= 1f)
        {
            UseMouse();
        }
    }

    private void LateUpdate()
    {
        if (!_allowInteraction)
        {
            // 플레이어 이동 이후에 중심을 맞춰 방 안과 복도에서도 연속적으로 추적한다.
            CenterOnPlayer();
        }
        else
        {
            RefreshHoveredRoom();
        }
    }

    private bool TryFocusGamepad()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem && eventSystem.currentSelectedGameObject != gameObject)
        {
            GameObject selected = eventSystem.currentSelectedGameObject;
            // 다른 입력창이나 확인창이 포커스를 가진 동안 지도 입력이 가로채지 않는다.
            if (selected && !selected.TryGetComponent<WindowTabButton>(out _))
            {
                return false;
            }

            _previousSelection = selected;
            eventSystem.SetSelectedGameObject(gameObject);
        }

        _usingGamepad = true;
        _selectionSuspended = false;
        _selectionDirty = true;
        if (_gamepadCursor)
        {
            _gamepadCursor.enabled = true;
        }

        return true;
    }

    private void UseMouse()
    {
        _usingGamepad = false;
        _selectionSuspended = false;
        _selectionDirty = true;
        if (_gamepadCursor)
        {
            _gamepadCursor.enabled = false;
        }
    }

    private void RememberPointer(PointerEventData eventData)
    {
        _pointerScreenPosition = eventData.position;
        _pointerCamera = eventData.enterEventCamera;
        _selectionDirty = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;
        RememberPointer(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
        _selectionDirty = true;
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        RememberPointer(eventData);
        if (eventData.delta.sqrMagnitude >= 1f)
        {
            UseMouse();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_allowInteraction)
        {
            return;
        }

        _suppressClick = false;
        RememberPointer(eventData);
        UseMouse();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_allowInteraction || _suppressClick || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        RememberPointer(eventData);
        UseMouse();
        ConfirmHoveredRoom();
        eventData.Use();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!_allowInteraction || !TryFocusGamepad())
        {
            return;
        }

        ConfirmHoveredRoom();
        eventData.Use();
    }

    public void OnMove(AxisEventData eventData)
    {
        // 왼쪽 스틱/방향키로 탭 탐색에 복귀할 수 있도록 이전 UI 포커스를 돌려준다.
        if (_previousSelection && _previousSelection.activeInHierarchy && EventSystem.current)
        {
            GameObject previous = _previousSelection;
            EventSystem.current.SetSelectedGameObject(previous);
            ExecuteEvents.Execute(previous, eventData, ExecuteEvents.moveHandler);
        }
    }

    public void OnDeselect(BaseEventData eventData)
    {
        // 탭이나 다른 UI로 이동한 뒤 정지된 마우스 위치가 다시 선택되지 않도록 한다.
        _selectionSuspended = true;
        _usingGamepad = false;
        _selectionDirty = true;
        if (_gamepadCursor)
        {
            _gamepadCursor.enabled = false;
        }

        SetHoveredRoom(null, null);
    }

    private void ConfirmHoveredRoom()
    {
        _selectionDirty = true;
        RefreshHoveredRoom();
        if (HoveredRoom != null && _stageManager && _stageManager.IsVisited(HoveredRoom))
        {
            RoomConfirmed?.Invoke(HoveredRoom);
        }
    }

    private void RefreshHoveredRoom()
    {
        if (!_selectionDirty)
        {
            return;
        }

        _selectionDirty = false;
        if (!isActiveAndEnabled || !_allowInteraction || _selectionSuspended || !_stageManager || _displayedGraph == null || !ReferenceEquals(_displayedGraph, _stageManager.CurrentGraph) || !_viewport || !_mapRoot || _mapSize.x <= 0f || _mapSize.y <= 0f)
        {
            SetHoveredRoom(null, null);
            return;
        }

        Vector2 viewportPosition = _viewport.rect.center;
        if (!_usingGamepad && (!_pointerInside || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, _pointerScreenPosition, _pointerCamera, out viewportPosition) || !_viewport.rect.Contains(viewportPosition)))
        {
            SetHoveredRoom(null, null);
            return;
        }

        // 입력이나 지도 배치가 달라졌을 때만 풀의 방을 검사해 매 프레임 검색과 할당을 피한다.
        Vector3 worldPosition = _viewport.TransformPoint(viewportPosition);
        for (int index = _displayedGraph.Nodes.Count - 1; index >= 0; index--)
        {
            StageNode node = _displayedGraph.Nodes[index];
            StageMapRoomView view = _roomPool[index];
            if (!view.gameObject.activeInHierarchy || !_stageManager.IsVisited(node))
            {
                continue;
            }

            RectTransform roomRect = (RectTransform)view.transform;
            if (roomRect.rect.Contains(roomRect.InverseTransformPoint(worldPosition)))
            {
                SetHoveredRoom(node, view);
                return;
            }
        }

        SetHoveredRoom(null, null);
    }

    private void SetHoveredRoom(StageNode node, StageMapRoomView view)
    {
        if (ReferenceEquals(HoveredRoom, node) && _hoveredRoomView == view)
        {
            return;
        }

        if (_hoveredRoomView)
        {
            _hoveredRoomView.SetTeleportVisible(false);
        }

        HoveredRoom = node;
        _hoveredRoomView = view;
        if (_hoveredRoomView)
        {
            _hoveredRoomView.SetTeleportVisible(true);
        }
    }

    private void CenterOnPlayer()
    {
        if (!_stageManager || !_viewport || !_mapRoot || _mapSize.x <= 0f || _mapSize.y <= 0f)
        {
            return;
        }

        if (!_stageManager.TryGetPlayerPosition(out Vector3 position))
        {
            return;
        }

        _mapRoot.anchoredPosition = -WorldToMapPosition(position) * (_fitScale * _zoom);
    }

    public void SetStageManager(StageManager stageManager)
    {
        if (_stageManager == stageManager)
        {
            return;
        }

        if (_stageManager)
        {
            _stageManager.MapChanged -= RefreshMap;
        }

        _stageManager = stageManager;

        // 비활성 상태에서는 참조만 저장하고, 탭을 열 때 연결한다.
        if (isActiveAndEnabled)
        {
            RefreshMap(null);
            BindStageManager();
        }
    }

    private void BindStageManager()
    {
        // StageManager의 Awake보다 먼저 활성화되면 할당을 기다린다.
        if (!_stageManager)
        {
            return;
        }

        if (!_mapRoot || !_roomPrefab || !_corridorPrefab)
        {
            Debug.LogError("지도 생성에 필요한 참조가 설정되지 않았습니다.", this);
            return;
        }

        if (_mapRoot.parent != transform)
        {
            Debug.LogError("MapRoot는 StageMapView의 바로 아래 자식이어야 합니다.", this);
            return;
        }

        // 다른 오브젝트의 Awake에서 먼저 호출돼도 초기화되도록 한다.
        if (!_viewport)
        {
            _viewport = (RectTransform)transform;
        }

        // 할당과 활성화 순서에 관계없이 중복 구독을 방지한다.
        _stageManager.MapChanged -= RefreshMap;
        _stageManager.MapChanged += RefreshMap;

        // 지도 탭이 닫혀 있을 때 생성된 그래프도 처음 열면 표시한다.
        RefreshMap(_stageManager.CurrentGraph);
    }

    private void OnDisable()
    {
        SetHoveredRoom(null, null);
        UseMouse();
        _pointerInside = false;
        _suppressClick = false;
        if (EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(_previousSelection && _previousSelection.activeInHierarchy ? _previousSelection : null);
        }

        _previousSelection = null;

        // 미니맵을 숨길 때 다른 지도에서 사용하는 액션에 영향을 주지 않는다.
        if (_allowInteraction)
        {
            _panAction?.action.Disable();
            _zoomInAction?.action.Disable();
            _zoomOutAction?.action.Disable();
        }

        if (_stageManager)
        {
            _stageManager.MapChanged -= RefreshMap;
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        FitMap();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (!_allowInteraction)
        {
            return;
        }

        if (!_viewport || !_mapRoot || _mapSize.x <= 0f || _mapSize.y <= 0f || Mathf.Approximately(eventData.scrollDelta.y, 0f))
        {
            return;
        }

        // 휠 입력은 현재 포인터가 가리키는 Canvas 카메라로 좌표를 변환한다.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position, eventData.enterEventCamera, out Vector2 pointerPosition))
        {
            return;
        }

        RememberPointer(eventData);
        UseMouse();
        float step = Mathf.Max(0.01f, _zoomStep);
        // MapRoot의 중앙 앵커 기준으로 바꿔 뷰포트 피벗 위치와 무관하게 계산한다.
        ChangeZoom(Mathf.Sign(eventData.scrollDelta.y) * step, pointerPosition - _viewport.rect.center);

        // 상위 UI가 같은 스크롤 입력을 중복 처리하지 않도록 소비한다.
        eventData.Use();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_allowInteraction)
        {
            return;
        }

        if (!_viewport || !_mapRoot || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        // 화면 이동량을 뷰포트 좌표로 변환해 Canvas 배율에 맞춰 지도를 이동한다.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position, eventData.pressEventCamera, out Vector2 currentPosition))
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position - eventData.delta, eventData.pressEventCamera, out Vector2 previousPosition))
        {
            return;
        }

        _suppressClick = true;
        RememberPointer(eventData);
        UseMouse();
        _mapRoot.anchoredPosition += currentPosition - previousPosition;
        ClampMapPosition();
        eventData.Use();
    }

    private void RefreshMap(StageGraph graph)
    {
        _selectionDirty = true;
        if (ReferenceEquals(_displayedGraph, graph))
        {
            // 방문 상태나 현재 방만 달라졌다면 이미지 배치와 사용자의 줌·이동을 유지한다.
            RefreshExplorationVisibility();
            FitMap();
            return;
        }

        SetHoveredRoom(null, null);
        _displayedGraph = graph;
        _zoom = Mathf.Clamp(1f, _minZoom, Mathf.Max(_minZoom, _maxZoom));
        _usedCorridors = 0;
        _edgeCorridorEnds.Clear();

        if (graph == null || graph.Nodes.Count == 0)
        {
            HideUnused(_roomPool, 0);
            HideUnused(_corridorPool, 0);
            _mapSize = Vector2.zero;
            return;
        }

        _lineWidth = Mathf.Max(0.01f, _corridorWorldWidth);

        CalculateBounds(graph);

        _mapRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _mapRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _mapRoot.pivot = new Vector2(0.5f, 0.5f);
        _mapRoot.anchoredPosition3D = Vector3.zero;
        _mapRoot.sizeDelta = _mapSize;
        _mapRoot.localRotation = Quaternion.identity;

        // 복도를 먼저 배치하고 방을 위에 그려 접합부를 덮는다.
        for (int index = 0; index < graph.Edges.Count; index++)
        {
            DrawEdge(graph.Edges[index]);
            // ㄱ자 복도처럼 여러 이미지를 사용하는 간선의 범위도 기록한다.
            _edgeCorridorEnds.Add(_usedCorridors);
        }

        for (int index = 0; index < graph.Nodes.Count; index++)
        {
            StageMapRoomView room = GetRoom(index);
            GetRoomBounds(graph.Nodes[index], out Vector2 minimum, out Vector2 maximum);
            Vector2 position = (minimum + maximum) * 0.5f - _mapCenter;

            // 루트를 배치해 기본 이미지와 자식 표시 이미지가 함께 이동하도록 한다.
            SetRect((RectTransform)room.transform, position, maximum - minimum, 0f);
        }

        HideUnused(_roomPool, graph.Nodes.Count);
        HideUnused(_corridorPool, _usedCorridors);
        RefreshExplorationVisibility();
        FitMap();
    }

    private void RefreshExplorationVisibility()
    {
        if (!_stageManager || _displayedGraph == null)
        {
            return;
        }

        StageNode currentRoom = _stageManager.CurrentRoom;

        for (int index = 0; index < _displayedGraph.Nodes.Count; index++)
        {
            StageNode node = _displayedGraph.Nodes[index];
            StageMapRoomView room = _roomPool[index];
            bool visible = _stageManager.IsVisited(node);

            SetMapItemVisible(room, visible);

            // 현재 방만 강조하고 다른 방의 이전 강조는 해제한다.
            room.SetCurrentLocation(visible && ReferenceEquals(node, currentRoom));
        }

        int corridorStart = 0;

        for (int index = 0; index < _displayedGraph.Edges.Count; index++)
        {
            StageEdge edge = _displayedGraph.Edges[index];

            // 연결된 방 중 한쪽만 방문했어도 전체 연결 복도를 공개한다.
            bool visible = _stageManager.IsVisited(edge.From) || _stageManager.IsVisited(edge.To);
            int corridorEnd = _edgeCorridorEnds[index];

            for (int corridorIndex = corridorStart; corridorIndex < corridorEnd; corridorIndex++)
            {
                SetMapItemVisible(_corridorPool[corridorIndex], visible);
            }

            corridorStart = corridorEnd;
        }
    }

    private static void SetMapItemVisible(Component item, bool visible)
    {
        // 이미 원하는 상태인 오브젝트는 다시 활성화하거나 비활성화하지 않는다.
        if (item.gameObject.activeSelf != visible)
        {
            item.gameObject.SetActive(visible);
        }
    }

    private void CalculateBounds(StageGraph graph)
    {
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int index = 0; index < graph.Nodes.Count; index++)
        {
            GetRoomBounds(graph.Nodes[index], out Vector2 roomMinimum, out Vector2 roomMaximum);
            minimum = Vector2.Min(minimum, roomMinimum);
            maximum = Vector2.Max(maximum, roomMaximum);
        }

        // 방 외곽을 벗어나는 복도와 모퉁이도 화면 맞춤 범위에 포함한다.
        Vector2 halfWidth = Vector2.one * (_lineWidth * 0.5f);
        for (int index = 0; index < graph.Edges.Count; index++)
        {
            StageEdge edge = graph.Edges[index];
            if (!TryGetCorridorEndpoints(edge, out Vector2 start, out Vector2 end))
            {
                continue;
            }

            Vector2 edgeMin = Vector2.Min(start, end);
            Vector2 edgeMax = Vector2.Max(start, end);
            minimum = Vector2.Min(minimum, edgeMin - halfWidth);
            maximum = Vector2.Max(maximum, edgeMax + halfWidth);
        }

        _mapCenter = (minimum + maximum) * 0.5f;
        _mapSize = maximum - minimum;
    }

    private static void GetRoomBounds(StageNode node, out Vector2 minimum, out Vector2 maximum)
    {
        Vector2 center = new Vector2(node.Position.x, node.Position.z);
        Vector2 halfSize = node.RoomData.RoomSpacing * 0.5f;
        minimum = center - halfSize;
        maximum = center + halfSize;

        Room room = node.RoomInstance;
        if (!room || room.Doors == null)
        {
            return;
        }

        float west = float.PositiveInfinity;
        float east = float.NegativeInfinity;
        float south = float.PositiveInfinity;
        float north = float.NegativeInfinity;

        // 연결 여부와 무관하게 등록된 문으로 방 경계를 구해 배치 여백이 복도를 가리지 않도록 한다.
        for (int index = 0; index < room.Doors.Length; index++)
        {
            Door door = room.Doors[index];
            if (!door)
            {
                continue;
            }

            Vector3 position = door.transform.position;
            switch (door.Direction)
            {
                case DoorDirection.West:
                    west = Mathf.Min(west, position.x);
                    break;
                case DoorDirection.East:
                    east = Mathf.Max(east, position.x);
                    break;
                case DoorDirection.South:
                    south = Mathf.Min(south, position.z);
                    break;
                case DoorDirection.North:
                    north = Mathf.Max(north, position.z);
                    break;
            }
        }

        // 양쪽 문이 있고 유효한 폭을 얻은 축만 교체하며, 나머지 축은 RoomSpacing을 유지한다.
        if (east > west)
        {
            minimum.x = west;
            maximum.x = east;
        }

        if (north > south)
        {
            minimum.y = south;
            maximum.y = north;
        }
    }

    private Vector2 WorldToMapPosition(Vector3 position)
    {
        return new Vector2(position.x, position.z) - _mapCenter;
    }

    private static bool TryGetCorridorEndpoints(StageEdge edge, out Vector2 start, out Vector2 end)
    {
        start = default;
        end = default;
        Room room = edge.From.RoomInstance;
        Room targetRoom = edge.To.RoomInstance;
        if (!room || !targetRoom || room.Doors == null || targetRoom.Doors == null)
        {
            return false;
        }

        // 이미 연결된 문을 읽으므로 연결 가능한 문을 찾는 생성용 메서드는 사용하지 않는다.
        for (int index = 0; index < room.Doors.Length; index++)
        {
            Door door = room.Doors[index];
            if (!door || door.Direction != edge.FromDirection || !door.ConnectedDoor || door.ConnectedDoor.Direction != edge.ToDirection)
            {
                continue;
            }

            for (int targetIndex = 0; targetIndex < targetRoom.Doors.Length; targetIndex++)
            {
                if (targetRoom.Doors[targetIndex] != door.ConnectedDoor)
                {
                    continue;
                }

                Vector3 fromPosition = door.transform.position;
                Vector3 toPosition = door.ConnectedDoor.transform.position;
                start = new Vector2(fromPosition.x, fromPosition.z);
                end = new Vector2(toPosition.x, toPosition.z);
                return true;
            }
        }

        return false;
    }

    private void DrawEdge(StageEdge edge)
    {
        if (!TryGetCorridorEndpoints(edge, out Vector2 start, out Vector2 end))
        {
            return;
        }

        start -= _mapCenter;
        end -= _mapCenter;
        if (edge.ToDirection == StageGraph.GetOppositeDirection(edge.FromDirection))
        {
            DrawSegment(start, end);
            return;
        }

        // 실제 복도와 동일하게 두 문에서 뻗은 축의 교차점을 모퉁이로 사용한다.
        bool startsHorizontal = edge.FromDirection == DoorDirection.East || edge.FromDirection == DoorDirection.West;
        Vector2 corner = startsHorizontal ? new Vector2(end.x, start.y) : new Vector2(start.x, end.y);
        DrawSegment(start, corner);
        DrawSegment(corner, end);
    }

    private void DrawSegment(Vector2 start, Vector2 end)
    {
        Vector2 direction = end - start;
        float length = direction.magnitude;

        if (length <= 0.001f)
        {
            return;
        }

        RectTransform corridor = GetImage(_corridorPool, _usedCorridors, _corridorPrefab);
        _usedCorridors++;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 선 두께만큼 겹쳐 ㄱ자 접합부에 틈이 생기지 않도록 한다.
        Vector2 size = new Vector2(length + _lineWidth, _lineWidth);
        SetRect(corridor, (start + end) * 0.5f, size, angle);
    }

    private StageMapRoomView GetRoom(int index)
    {
        if (index >= _roomPool.Count)
        {
            StageMapRoomView room = Instantiate(_roomPrefab, _mapRoot, false);
            room.RoomImage.preserveAspect = false;
            _roomPool.Add(room);
        }

        StageMapRoomView item = _roomPool[index];
        SetMapItemVisible(item, true);

        // 새 그래프의 방으로 재사용할 때 이전 강조 표시를 초기화한다.
        item.ResetIndicators();
        return item;
    }

    private RectTransform GetImage(List<RectTransform> pool, int index, Image prefab)
    {
        if (index >= pool.Count)
        {
            Image image = Instantiate(prefab, _mapRoot, false);
            image.raycastTarget = false;
            image.preserveAspect = false;
            pool.Add(image.rectTransform);
        }

        RectTransform item = pool[index];

        if (!item.gameObject.activeSelf)
        {
            item.gameObject.SetActive(true);
        }

        return item;
    }

    private static void SetRect(RectTransform item, Vector2 position, Vector2 size, float angle)
    {
        item.anchorMin = new Vector2(0.5f, 0.5f);
        item.anchorMax = new Vector2(0.5f, 0.5f);
        item.pivot = new Vector2(0.5f, 0.5f);
        item.anchoredPosition3D = new Vector3(position.x, position.y, 0f);
        item.sizeDelta = size;
        item.localRotation = Quaternion.Euler(0f, 0f, angle);
        item.localScale = Vector3.one;
        item.SetAsLastSibling();
    }

    private static void HideUnused<T>(List<T> pool, int usedCount) where T : Component
    {
        for (int index = usedCount; index < pool.Count; index++)
        {
            if (pool[index].gameObject.activeSelf)
            {
                pool[index].gameObject.SetActive(false);
            }
        }
    }

    private void FitMap()
    {
        if (!_viewport || !_mapRoot || _mapSize.x <= 0f || _mapSize.y <= 0f)
        {
            return;
        }

        Vector2 available = _viewport.rect.size - Vector2.one * Mathf.Max(0f, _padding) * 2f;

        if (available.x <= 0f || available.y <= 0f)
        {
            return;
        }

        float scale = Mathf.Min(available.x / _mapSize.x, available.y / _mapSize.y);

        // 작은 지도는 원래 크기를 유지하고, 큰 지도만 패널에 맞춰 축소한다.
        _fitScale = Mathf.Min(1f, scale);
        ApplyZoom();
    }

    private void ChangeZoom(float delta, Vector2? zoomCenter = null)
    {
        float maximum = Mathf.Max(_minZoom, _maxZoom);
        float nextZoom = Mathf.Clamp(_zoom + delta, _minZoom, maximum);

        // 한계 배율에서는 불필요한 Transform 갱신을 생략한다.
        if (Mathf.Approximately(_zoom, nextZoom))
        {
            return;
        }

        if (zoomCenter.HasValue && _mapRoot)
        {
            // 포인터에서 지도 원점까지의 거리를 배율에 맞춰 보정해 같은 지도 지점을 유지한다.
            Vector2 center = zoomCenter.Value;
            _mapRoot.anchoredPosition = center + (_mapRoot.anchoredPosition - center) * (nextZoom / _zoom);
        }

        _zoom = nextZoom;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        if (_mapRoot)
        {
            _mapRoot.localScale = Vector3.one * (_fitScale * _zoom);
            ClampMapPosition();
        }
    }

    private void ClampMapPosition()
    {
        _selectionDirty = true;
        // 미니맵은 지도 가장자리에서도 플레이어가 중앙에 있도록 이동 범위를 제한하지 않는다.
        if (!_allowInteraction)
        {
            CenterOnPlayer();
            return;
        }

        if (!_viewport || !_mapRoot || _mapSize.x <= 0f || _mapSize.y <= 0f)
        {
            return;
        }

        float scale = _fitScale * _zoom;
        Vector2 availableSize = _viewport.rect.size - _padding * 2f * Vector2.one;
        Vector2 scaledMapSize = _mapSize * scale;

        // 입력 장치를 전환해도 위치가 튀지 않도록 양쪽 모두 가장자리 방을 중앙까지 가져올 수 있게 한다.
        Vector2 movementLimit = Vector2.Max(Vector2.zero, scaledMapSize - availableSize) * 0.5f + Vector2.one * Mathf.Max(0f, _panPadding);
        movementLimit = Vector2.Max(movementLimit, scaledMapSize * 0.5f);
        Vector2 position = _mapRoot.anchoredPosition;

        position.x = Mathf.Clamp(position.x, -movementLimit.x, movementLimit.x);
        position.y = Mathf.Clamp(position.y, -movementLimit.y, movementLimit.y);
        _mapRoot.anchoredPosition = position;
    }
}
