using System;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    // 현재 스테이지 생성에 사용할 정의.
    [field: SerializeField] private StageDefinition _stageDefinition;
    //현재 스테이지 진행 정보...

    private MapGenerator _mapGenerator;
    private PlayerManager _playerManager;

    // 현재 생성된 방만 등록해 이전 스테이지의 알림을 무시한다.
    private readonly Dictionary<Room, StageNode> _roomNodes = new();
    private readonly HashSet<StageNode> _visitedRooms = new();

    public StageGraph CurrentGraph { get; private set; }

    // 그래프 생성 또는 새로운 방 방문 시 지도 갱신을 요청한다.
    public event Action<StageGraph> MapChanged;

    public event Action<StageNode, Door> RoomEntered;

    private void Awake()
    {
        if (!TryGetComponent(out _mapGenerator))
        {
            Debug.LogWarning("MapGenerator: NULL", this);
            return;
        }

        _playerManager = FindFirstObjectByType<PlayerManager>();

        // 닫힌 지도 탭까지 포함해 최초 초기화 시에만 찾는다.
        StageMapView[] mapViews = FindObjectsByType<StageMapView>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // 지도는 매니저를 통해 그래프와 방문 상태를 함께 전달받는다.
        for (int index = 0; index < mapViews.Length; index++)
        {
            StageMapView view = mapViews[index];

            if (view)
            {
                view.SetStageManager(this);
            }
        }
    }


    private void OnEnable()
    {
        if (!_mapGenerator)
        {
            return;
        }

        _mapGenerator.GraphChanged += HandleGraphChanged;
        HandleGraphChanged(_mapGenerator.CurrentGraph);
    }

    private void OnDisable()
    {
        if (_mapGenerator)
        {
            _mapGenerator.GraphChanged -= HandleGraphChanged;
        }

        _roomNodes.Clear();
    }

    private void HandleGraphChanged(StageGraph graph)
    {
        // 매니저를 다시 활성화한 경우에는 같은 그래프의 방문 기록을 유지한다.
        bool graphChanged = !ReferenceEquals(CurrentGraph, graph);

        if (graphChanged)
        {
            CurrentGraph = graph;
            _visitedRooms.Clear();
        }

        _roomNodes.Clear();

        if (graph == null)
        {
            MapChanged?.Invoke(null);
            return;
        }

        for (int index = 0; index < graph.Nodes.Count; index++)
        {
            StageNode node = graph.Nodes[index];
            Room room = node.RoomInstance;

            if (!room)
            {
                continue;
            }

            _roomNodes[room] = node;
            room.Initialize(this);

            // 새 스테이지의 시작 방은 처음부터 지도에 공개한다.
            if (graphChanged && node.RoomData && node.RoomData.RoomType == RoomType.Start)
            {
                _visitedRooms.Add(node);
            }
        }

        MapChanged?.Invoke(graph);
    }

    public bool IsVisited(StageNode node)
    {
        return node != null && _visitedRooms.Contains(node);
    }

    public void NotifyRoomEntered(Room room, Door entrance, PlayerManager player)
    {
        if (!isActiveAndEnabled || !_playerManager || player != _playerManager || !room || !entrance)
        {
            return;
        }

        if (!_roomNodes.TryGetValue(room, out StageNode node))
        {
            return;
        }

        // 최초 방문에서만 공개 상태가 달라진다.
        if (_visitedRooms.Add(node))
        {
            MapChanged?.Invoke(CurrentGraph);
        }

        // 재입장도 별도의 입장 사건이므로 기존 이벤트는 계속 전달한다.
        RoomEntered?.Invoke(node, entrance);
    }

    public void RegisterPlayerPlacement(Room room, PlayerManager player)
    {
        if (!isActiveAndEnabled || !_playerManager || player != _playerManager || !room)
        {
            return;
        }

        if (!_roomNodes.TryGetValue(room, out StageNode node))
        {
            return;
        }

        // 스폰·순간이동 담당자가 실제 배치한 방을 전달한다.
        if (_visitedRooms.Add(node))
        {
            MapChanged?.Invoke(CurrentGraph);
        }
    }

    private void Start()
    {
        if (_mapGenerator)
        {
            _mapGenerator.GenerateStage(_stageDefinition);
        }
    }
}
