using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class StageMapView : MonoBehaviour, IScrollHandler, IDragHandler
{
    [Header("References")]
    [SerializeField] private RectTransform _mapRoot;
    [SerializeField] private StageMapRoomView _roomPrefab;
    [SerializeField] private Image _corridorPrefab;

    [Header("Map Layout")]
    [SerializeField, Min(1f)] private float _cellSpacing = 100f;
    [SerializeField, Min(1f)] private float _roomSize = 40f;
    [SerializeField, Min(1f)] private float _corridorWidth = 8f;
    [SerializeField, Min(0f)] private float _padding = 24f;

    [Header("Interaction")]
    [SerializeField] private bool _allowInteraction = true;

    [Header("Zoom")]
    [SerializeField, Min(0.01f)] private float _minZoom = 1f;
    [SerializeField, Min(0.01f)] private float _maxZoom = 3f;
    [SerializeField, Min(0.01f)] private float _zoomStep = 0.15f;

    [Header("Gamepad")]
    [SerializeField] private InputActionReference _panAction;
    [SerializeField] private InputActionReference _zoomInAction;
    [SerializeField] private InputActionReference _zoomOutAction;
    [SerializeField, Min(0f)] private float _gamepadPanSpeed = 600f;
    [SerializeField, Min(0f)] private float _gamepadZoomSpeed = 1f;

    private readonly List<StageMapRoomView> _roomPool = new();
    private readonly List<RectTransform> _corridorPool = new();

    private StageManager _stageManager;

    // 한 간선이 사용하는 마지막 복도 이미지 다음 인덱스를 저장한다.
    private readonly List<int> _edgeCorridorEnds = new();
    private RectTransform _viewport;
    private StageGraph _displayedGraph;
    private Vector2 _mapCenter;
    private Vector2 _mapSize;
    private float _spacing;
    private float _roomLength;
    private float _lineWidth;
    private float _fitScale = 1f;
    private float _zoom = 1f;
    private int _usedCorridors;

    private void Awake()
    {
        _viewport = (RectTransform)transform;
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

        if (!Mathf.Approximately(zoomInput, 0f))
        {
            ChangeZoom(zoomInput * _gamepadZoomSpeed * deltaTime);
        }

        Vector2 panInput = _panAction ? _panAction.action.ReadValue<Vector2>() : Vector2.zero;

        if (panInput.sqrMagnitude > 0f)
        {
            // 스틱 방향으로 지도를 움직이고 기존 이동 범위를 적용한다.
            _mapRoot.anchoredPosition += panInput * _gamepadPanSpeed * deltaTime;
            ClampMapPosition();
        }
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

        if (!_mapRoot || Mathf.Approximately(eventData.scrollDelta.y, 0f))
        {
            return;
        }

        float step = Mathf.Max(0.01f, _zoomStep);
        ChangeZoom(Mathf.Sign(eventData.scrollDelta.y) * step);

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
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position, eventData.pressEventCamera,
            out Vector2 currentPosition))
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position - eventData.delta, eventData.pressEventCamera,
            out Vector2 previousPosition))
        {
            return;
        }

        _mapRoot.anchoredPosition += currentPosition - previousPosition;
        ClampMapPosition();
        eventData.Use();
    }

    private void RefreshMap(StageGraph graph)
    {
        if (ReferenceEquals(_displayedGraph, graph))
        {
            // 방문 상태나 현재 방만 달라졌다면 이미지 배치와 사용자의 줌·이동을 유지한다.
            RefreshExplorationVisibility();
            FitMap();
            return;
        }

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

        _roomLength = Mathf.Max(1f, _roomSize);
        _lineWidth = Mathf.Clamp(_corridorWidth, 1f, _roomLength);
        _spacing = Mathf.Max(_cellSpacing, _roomLength + _lineWidth);

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
            Vector2 position = GetMapPosition(graph.Nodes[index]);

            // 루트를 배치해 기본 이미지와 자식 표시 이미지가 함께 이동하도록 한다.
            SetRect((RectTransform)room.transform, position, Vector2.one * _roomLength, 0f);
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
        Vector2 minimum = (Vector2)graph.Nodes[0].GridPosition * _spacing;
        Vector2 maximum = minimum;

        for (int index = 1; index < graph.Nodes.Count; index++)
        {
            Vector2 position = (Vector2)graph.Nodes[index].GridPosition * _spacing;
            minimum = Vector2.Min(minimum, position);
            maximum = Vector2.Max(maximum, position);
        }

        _mapCenter = (minimum + maximum) * 0.5f;

        // 가장 바깥쪽 방의 윤곽까지 포함해 전체 크기를 계산한다.
        _mapSize = maximum - minimum + Vector2.one * _roomLength;
    }

    private Vector2 GetMapPosition(StageNode node)
    {
        return (Vector2)node.GridPosition * _spacing - _mapCenter;
    }

    private void DrawEdge(StageEdge edge)
    {
        Vector2 from = GetMapPosition(edge.From);
        Vector2 to = GetMapPosition(edge.To);
        float halfRoom = _roomLength * 0.5f;

        // 복도는 방 중심이 아닌, 출입 방향에 해당하는 변에서 시작한다.
        Vector2 start = from + (Vector2)StageGraph.GetGridOffset(edge.FromDirection) * halfRoom;
        Vector2 end = to + (Vector2)StageGraph.GetGridOffset(edge.ToDirection) * halfRoom;

        if (edge.ToDirection == StageGraph.GetOppositeDirection(edge.FromDirection))
        {
            DrawSegment(start, end);
            return;
        }

        // 기존 그래프의 ㄱ자 연결 규칙과 같은 방향으로 꺾는다.
        bool startsHorizontal = edge.FromDirection == DoorDirection.East || edge.FromDirection == DoorDirection.West;
        Vector2 corner = startsHorizontal ? new Vector2(to.x, from.y) : new Vector2(from.x, to.y);

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

    private void ChangeZoom(float delta)
    {
        float maximum = Mathf.Max(_minZoom, _maxZoom);
        float nextZoom = Mathf.Clamp(_zoom + delta, _minZoom, maximum);

        // 한계 배율에서는 불필요한 Transform 갱신을 생략한다.
        if (Mathf.Approximately(_zoom, nextZoom))
        {
            return;
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
        if (!_viewport || !_mapRoot || _mapSize.x <= 0f || _mapSize.y <= 0f)
        {
            return;
        }

        float scale = _fitScale * _zoom;
        Vector2 availableSize = _viewport.rect.size - _padding * 2f * Vector2.one;
        Vector2 scaledMapSize = _mapSize * scale;

        // 화면보다 작은 축은 중앙에 고정하고, 큰 축은 지도 가장자리까지만 이동한다.
        Vector2 movementLimit = Vector2.Max(Vector2.zero, scaledMapSize - availableSize) * 0.5f;
        Vector2 position = _mapRoot.anchoredPosition;

        position.x = Mathf.Clamp(position.x, -movementLimit.x, movementLimit.x);
        position.y = Mathf.Clamp(position.y, -movementLimit.y, movementLimit.y);
        _mapRoot.anchoredPosition = position;
    }
}
