using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class StageManager : MonoBehaviour
{
    private const string ItemDefinitionResourcePath = "ItemDefinition";

    // 현재 스테이지 생성에 사용할 정의.
    [field: SerializeField] private StageDefinition _stageDefinition;
    //현재 스테이지 진행 정보...

    [Header("보상 상자")]
    // Inspector의 사전 지정 상자와 생성된 보상방의 상자를 같은 목록에서 관리한다.
    [SerializeField] private List<RewardChest> _rewardChests = new();

    [Header("상점")]
    // Inspector의 사전 지정 상점과 생성된 상점방의 상점을 같은 목록에서 관리한다.
    [SerializeField] private List<Store> _stores = new();

    private MapGenerator _mapGenerator;
    private PlayerManager _playerManager;
    private SpawnManager _spawnManager;

    // 현재 생성된 방만 등록해 이전 스테이지의 알림을 무시한다.
    private readonly Dictionary<Room, StageNode> _roomNodes = new();
    private readonly HashSet<StageNode> _visitedRooms = new();
    // 방문 기록과 전투 시작 기록을 분리해 재입장이나 순간이동에도 편성을 다시 뽑지 않는다.
    private readonly Dictionary<Room, RoomEncounterRuntime> _roomEncounters = new();
    // 런타임에 추가한 항목만 추적하여 스테이지 갱신 시 사전 지정 상자를 보존한다.
    private readonly List<RewardChest> _runtimeRewardChests = new();
    private readonly List<Store> _runtimeStores = new();
    // 실제 구독 대상을 따로 추적하여 목록이 수정되어도 구독을 빠짐없이 해제한다.
    private readonly HashSet<Store> _subscribedStores = new();
    // 소모품을 제외한 공용 후보를 한 번 준비하고 모든 상자의 추첨에 재사용한다.
    private readonly List<ItemDefinition> _rewardItemCandidates = new();
    // 후보 수는 로드할 때만 집계하여 상자 추첨마다 임시 목록을 만들지 않는다.
    private readonly int[] _rewardItemCountsByRarity = new int[(int)ItemRarity.Legendary + 1];

    public IReadOnlyList<RewardChest> RewardChests => _rewardChests;
    public IReadOnlyList<Store> Stores => _stores;

    public StageGraph CurrentGraph { get; private set; }

    // 마지막으로 입장하거나 배치된 방을 현재 위치로 사용한다.
    public StageNode CurrentRoom { get; private set; }

    // 그래프 생성, 새로운 방 방문 또는 현재 방 변경 시 지도 갱신을 요청한다.
    public event Action<StageGraph> MapChanged;

    public event Action<StageNode, Door> RoomEntered;

    // 추후 상점창은 개별 상점을 찾지 않고 이 이벤트로 상점과 플레이어를 전달받는다.
    public event Action<Store, PlayerManager> StoreInteractionRequested;

    private void Awake()
    {
        // 그래프가 없는 테스트 씬에서도 사전 지정한 상자에 보상을 주입할 수 있게 먼저 읽는다.
        LoadRewardItems();

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
            // 그래프가 없는 테스트 씬에서도 Inspector에 지정한 상자는 등록한다.
            ResetRewardChests();
            ResetStores();
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
        ClearRuntimeRewardChests();
        UnsubscribeStores();
        ClearRuntimeStores();
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

    private void LoadRewardItems()
    {
        ItemDefinition[] definitions = Resources.LoadAll<ItemDefinition>(ItemDefinitionResourcePath);
        _rewardItemCandidates.Clear();
        _rewardItemCandidates.Capacity = definitions.Length;
        Array.Clear(_rewardItemCountsByRarity, 0, _rewardItemCountsByRarity.Length);

        for (int index = 0; index < definitions.Length; index++)
        {
            ItemDefinition definition = definitions[index];
            if (!definition || (definition.ItemType != ItemType.Equipment && definition.ItemType != ItemType.Relic))
            {
                continue;
            }

            _rewardItemCandidates.Add(definition);
            int rarityIndex = (int)definition.Rarity;
            if (rarityIndex >= 0 && rarityIndex < _rewardItemCountsByRarity.Length)
            {
                _rewardItemCountsByRarity[rarityIndex]++;
            }
        }

        if (_rewardItemCandidates.Count == 0)
        {
            Debug.LogWarning("Resources/ItemDefinition에 보상 상자용 장비 또는 유물 아이템이 없습니다.", this);
        }
    }

    private void AssignRewardItem(RewardChest chest)
    {
        if (!chest || chest.IsOpen || chest.RewardItem || _rewardItemCandidates.Count == 0)
        {
            return;
        }

        // 스테이지 정의가 없는 테스트 씬에서는 기존 아이템별 균등 추첨을 유지한다.
        if (!_stageDefinition)
        {
            int index = UnityEngine.Random.Range(0, _rewardItemCandidates.Count);
            chest.SetRewardItem(_rewardItemCandidates[index]);
            return;
        }

        if (!TrySelectRewardItemRarity(out ItemRarity rarity))
        {
            Debug.LogWarning("보상 상자에 사용할 아이템 후보와 양수 가중치가 있는 희귀도가 없습니다.", this);
            return;
        }

        // 선택된 희귀도 안에서만 균등 추첨하므로 등급별 아이템 개수가 희귀도 확률을 바꾸지 않는다.
        int itemIndex = UnityEngine.Random.Range(0, _rewardItemCountsByRarity[(int)rarity]);
        for (int index = 0; index < _rewardItemCandidates.Count; index++)
        {
            ItemDefinition candidate = _rewardItemCandidates[index];
            if (candidate.Rarity != rarity) continue;
            if (itemIndex-- == 0)
            {
                chest.SetRewardItem(candidate);
                return;
            }
        }
    }

    private bool TrySelectRewardItemRarity(out ItemRarity rarity)
    {
        rarity = default;
        // 여러 큰 float 가중치의 합도 넘치지 않도록 누적값만 double로 계산한다.
        double totalWeight = 0d;
        for (int index = 0; index < _rewardItemCountsByRarity.Length; index++)
        {
            if (_rewardItemCountsByRarity[index] == 0) continue;
            totalWeight += _stageDefinition.GetRewardChestRarityWeight((ItemRarity)index);
        }

        if (totalWeight <= 0d) return false;

        double randomWeight = UnityEngine.Random.value * totalWeight;
        for (int index = 0; index < _rewardItemCountsByRarity.Length; index++)
        {
            if (_rewardItemCountsByRarity[index] == 0) continue;
            float weight = _stageDefinition.GetRewardChestRarityWeight((ItemRarity)index);
            if (weight <= 0f) continue;

            rarity = (ItemRarity)index;
            if (randomWeight < weight) return true;
            randomWeight -= weight;
        }

        // Random.value가 1이거나 누적 오차가 있어도 마지막 유효한 희귀도를 선택한다.
        return true;
    }

    private void ResetRewardChests()
    {
        ClearRuntimeRewardChests();
        if (_rewardChests == null)
        {
            _rewardChests = new();
            return;
        }

        // 사전 지정한 목록에서 빈 참조와 중복만 제거하고 첫 번째 항목은 유지한다.
        for (int index = _rewardChests.Count - 1; index >= 0; index--)
        {
            RewardChest chest = _rewardChests[index];
            if (!chest || _rewardChests.IndexOf(chest) != index)
            {
                _rewardChests.RemoveAt(index);
                continue;
            }

            AssignRewardItem(chest);
        }
    }

    private void ClearRuntimeRewardChests()
    {
        for (int index = 0; index < _runtimeRewardChests.Count; index++)
        {
            _rewardChests?.Remove(_runtimeRewardChests[index]);
        }

        _runtimeRewardChests.Clear();
    }

    private void RegisterRewardChest(RewardChest chest)
    {
        // 사전 지정된 상자는 런타임 추가 항목으로 취급하지 않고 중복 등록을 막는다.
        if (!chest || _rewardChests.Contains(chest))
        {
            return;
        }

        _rewardChests.Add(chest);
        _runtimeRewardChests.Add(chest);
        AssignRewardItem(chest);
    }

    private void ResetStores()
    {
        UnsubscribeStores();
        ClearRuntimeStores();
        if (_stores == null)
        {
            _stores = new();
            return;
        }

        // 사전 지정한 상점은 유지하고 빈 참조와 중복만 정리한다.
        for (int index = _stores.Count - 1; index >= 0; index--)
        {
            Store store = _stores[index];
            if (!store || _stores.IndexOf(store) != index)
            {
                _stores.RemoveAt(index);
                continue;
            }

            SubscribeStore(store);
        }
    }

    private void ClearRuntimeStores()
    {
        for (int index = 0; index < _runtimeStores.Count; index++)
        {
            Store store = _runtimeStores[index];
            if (_subscribedStores.Remove(store) && store)
            {
                store.InteractionRequested -= HandleStoreInteractionRequested;
            }

            _stores?.Remove(store);
        }

        _runtimeStores.Clear();
    }

    private void RegisterStore(Store store)
    {
        // Inspector에서 이미 지정한 상점은 런타임 항목에 다시 추가하지 않는다.
        if (!store || _stores.Contains(store))
        {
            return;
        }

        _stores.Add(store);
        _runtimeStores.Add(store);
        SubscribeStore(store);
    }

    private void SubscribeStore(Store store)
    {
        if (!_subscribedStores.Add(store)) return;
        store.InteractionRequested += HandleStoreInteractionRequested;
    }

    private void UnsubscribeStores()
    {
        foreach (Store store in _subscribedStores)
        {
            if (store)
            {
                store.InteractionRequested -= HandleStoreInteractionRequested;
            }
        }

        _subscribedStores.Clear();
    }

    private void HandleStoreInteractionRequested(Store store, PlayerManager player)
    {
        // 이전 스테이지의 상점이나 비활성화된 매니저의 요청은 UI로 전달하지 않는다.
        if (!isActiveAndEnabled || !store || !store.isActiveAndEnabled || !player || !player.isActiveAndEnabled || _stores == null || !_stores.Contains(store)) return;
        if (_playerManager && player != _playerManager) return;

        StoreInteractionRequested?.Invoke(store, player);
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
        ResetRewardChests();
        ResetStores();

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

            if (node.RoomData && node.RoomData.RoomType == RoomType.Reward)
            {
                RegisterRewardChest(room.RewardChest);
            }
            else if (node.RoomData && node.RoomData.RoomType == RoomType.Store)
            {
                RegisterStore(room.Store);
            }

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
