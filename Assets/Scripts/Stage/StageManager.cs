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
    private SpawnManager _spawnManager;

    // 현재 생성된 방만 등록해 이전 스테이지의 알림을 무시한다.
    private readonly Dictionary<Room, StageNode> _roomNodes = new();
    private readonly HashSet<StageNode> _visitedRooms = new();
    // 방문 기록과 전투 시작 기록을 분리해 재입장이나 순간이동에도 편성을 다시 뽑지 않는다.
    private readonly Dictionary<Room, RoomEncounterRuntime> _roomEncounters = new();

    public StageGraph CurrentGraph { get; private set; }

    // 마지막으로 입장하거나 배치된 방을 현재 위치로 사용한다.
    public StageNode CurrentRoom { get; private set; }

    // 그래프 생성, 새로운 방 방문 또는 현재 방 변경 시 지도 갱신을 요청한다.
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

        // 같은 오브젝트의 스폰 담당 컴포넌트에 스테이지가 사용하는 플레이어를 전달한다.
        if (TryGetComponent(out _spawnManager))
        {
            _spawnManager.Initialize(_playerManager);
        }

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

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        // Dictionary의 값 형식 열거자를 사용하여 매 프레임 임시 컬렉션을 만들지 않는다.
        foreach (KeyValuePair<Room, RoomEncounterRuntime> entry in _roomEncounters)
        {
            entry.Value.Tick(deltaTime);
        }
    }

    private void OnDestroy()
    {
        ClearEncounters();
    }

    private void ClearEncounters()
    {
        foreach (KeyValuePair<Room, RoomEncounterRuntime> entry in _roomEncounters)
        {
            entry.Value.Clear();
        }

        _roomEncounters.Clear();
    }

    private void HandleGraphChanged(StageGraph graph)
    {
        // 매니저를 다시 활성화한 경우에는 같은 그래프의 방문 기록과 현재 방을 유지한다.
        bool graphChanged = !ReferenceEquals(CurrentGraph, graph);

        if (graphChanged)
        {
            ClearEncounters();
            CurrentGraph = graph;
            CurrentRoom = null;
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

                // 현재 씬의 시작 배치에 맞춰 최초 위치를 시작 방으로 지정한다.
                if (_playerManager && CurrentRoom == null)
                {
                    CurrentRoom = node;
                }
            }
        }

        MapChanged?.Invoke(graph);
    }

    public bool TryGetPlayerPosition(out Vector3 position)
    {
        position = default;
        if (!_playerManager)
        {
            return false;
        }

        // 지도는 플레이어 하위 컴포넌트를 직접 참조하지 않고 매니저를 통해 위치를 읽는다.
        position = _playerManager.transform.position;
        return true;
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

        UpdatePlayerRoom(node);

        TryStartBattleEncounter(node);

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
        UpdatePlayerRoom(node);
        TryStartBattleEncounter(node);
    }

    private void TryStartBattleEncounter(StageNode node)
    {
        if (!node.RoomData || node.RoomData.RoomType != RoomType.Battle || !node.RoomInstance || !_spawnManager) return;
        Room room = node.RoomInstance;
        if (_roomEncounters.ContainsKey(room)) return;

        EncounterDefinition[] candidates = _stageDefinition ? _stageDefinition.BattleEncounters : null;
        EncounterDefinition selected = null;
        int validCandidateCount = 0;
        if (candidates != null)
        {
            // 후보 배열에는 순서나 가중치를 부여하지 않고 유효한 항목을 균등 무작위 선택한다.
            for (int index = 0; index < candidates.Length; index++)
            {
                EncounterDefinition candidate = candidates[index];
                if (!HasSpawnableEnemies(candidate)) continue;

                validCandidateCount++;
                if (UnityEngine.Random.Range(0, validCandidateCount) == 0) selected = candidate;
            }
        }

        if (!selected)
        {
            Debug.LogWarning("전투방에 사용할 유효한 BattleEncounters 편성이 없습니다.", room);
            return;
        }

        RoomEncounterRuntime encounter = new(room, _spawnManager, selected);
        _roomEncounters.Add(room, encounter);
        // 첫 그룹의 조건을 즉시 확인하되, 이후 대기와 재시도는 Update에서 이어간다.
        encounter.Tick(0f);
    }

    private static bool HasSpawnableEnemies(EncounterDefinition encounter)
    {
        if (!encounter || encounter.Groups == null) return false;
        for (int groupIndex = 0; groupIndex < encounter.Groups.Length; groupIndex++)
        {
            EncounterEnemyEntry[] entries = encounter.Groups[groupIndex]?.Enemies;
            if (entries == null) continue;

            for (int index = 0; index < entries.Length; index++)
            {
                EncounterEnemyEntry entry = entries[index];
                if (entry != null && entry.Prefab && entry.Count > 0) return true;
            }
        }

        return false;
    }

    private void UpdatePlayerRoom(StageNode node)
    {
        bool firstVisit = _visitedRooms.Add(node);
        bool roomChanged = !ReferenceEquals(CurrentRoom, node);

        // 지도 갱신 전에 현재 방을 저장해 모든 지도에 동일한 위치를 전달한다.
        CurrentRoom = node;

        // 이미 방문한 방으로 돌아왔을 때도 위치 강조를 갱신한다.
        if (firstVisit || roomChanged)
        {
            MapChanged?.Invoke(CurrentGraph);
        }
    }

    private void Start()
    {
        // 모든 매니저의 Awake가 끝난 뒤, 방 생성 전에 일반 적의 비활성 풀을 준비한다.
        if (_spawnManager)
        {
            _spawnManager.PrepareEnemyPools(_stageDefinition);
        }
        else if (_stageDefinition)
        {
            Debug.LogWarning("SpawnManager가 없어 일반 적의 사전 풀 준비를 건너뜁니다.", this);
        }

        if (_mapGenerator)
        {
            _mapGenerator.GenerateStage(_stageDefinition);
        }
    }
}
