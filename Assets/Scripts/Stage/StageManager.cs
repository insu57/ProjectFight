using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class StageManager : MonoBehaviour
{
    private const string ItemDefinitionResourcePath = "ItemDefinition";
    private const int ItemTypeCount = (int)ItemType.Relic + 1;
    private const int ItemRarityCount = (int)ItemRarity.Legendary + 1;

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
    private readonly HashSet<Room> _clearedEncounterRooms = new();
    private readonly List<Room> _pendingRoomClearNotifications = new(4);
    // 런타임에 추가한 항목만 추적하여 스테이지 갱신 시 사전 지정 상자를 보존한다.
    private readonly List<RewardChest> _runtimeRewardChests = new();
    private readonly List<Store> _runtimeStores = new();
    // 실제 구독 대상을 따로 추적하여 목록이 수정되어도 구독을 빠짐없이 해제한다.
    private readonly HashSet<Store> _subscribedStores = new();
    // 소모품을 제외한 공용 후보를 한 번 준비하고 모든 상자의 추첨에 재사용한다.
    private readonly List<ItemDefinition> _rewardItemCandidates = new();
    // 후보 수는 로드할 때만 집계하여 상자 추첨마다 임시 목록을 만들지 않는다.
    private readonly int[] _rewardItemCountsByRarity = new int[(int)ItemRarity.Legendary + 1];
    // 상점은 소모품까지 포함하며 종류·희귀도별 후보 수와 추첨 버퍼를 재사용한다.
    private readonly List<ItemDefinition> _storeItemCandidates = new();
    private readonly int[] _storeItemCountsByTypeAndRarity = new int[ItemTypeCount * ItemRarityCount];
    private readonly ItemDefinition[] _storeItemSelection = new ItemDefinition[Store.ItemCount];
    // Money·획득 알림에서 구매나 리롤을 다시 요청해도 상품 변경과 결제가 겹치지 않게 한다.
    private bool _isUpdatingStore;

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
        // 그래프가 없는 테스트 씬에서도 사전 지정한 상자와 상점에 아이템을 주입하도록 먼저 읽는다.
        LoadItems();

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
        _pendingRoomClearNotifications.Clear();
        foreach (KeyValuePair<Room, RoomEncounterRuntime> entry in _roomEncounters)
        {
            entry.Value.Tick(deltaTime);
            // 생존 적이 잠시 0이어도 다음 그룹이나 미스폰 적이 남아 있으면 문을 잠근다.
            if (entry.Key) entry.Key.SetDoorOpeningBlocked(!entry.Value.IsComplete);
            if (entry.Key && entry.Value.IsComplete && _clearedEncounterRooms.Add(entry.Key)) _pendingRoomClearNotifications.Add(entry.Key);
        }
        // 패시브 알림에서 스테이지가 변경되어도 Dictionary 순회가 중단되지 않게 밖에서 전달한다.
        for (int i = 0; i < _pendingRoomClearNotifications.Count; i++)
        {
            Room room = _pendingRoomClearNotifications[i];
            if (room && _roomEncounters.ContainsKey(room) && _playerManager) _playerManager.NotifyRoomCleared();
        }
        _pendingRoomClearNotifications.Clear();
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
            // 스테이지 정리 시 이전 전투의 잠금이 방에 남지 않도록 해제한다.
            if (entry.Key) entry.Key.SetDoorOpeningBlocked(false);
        }

        _roomEncounters.Clear();
        _clearedEncounterRooms.Clear();
        _pendingRoomClearNotifications.Clear();
    }

    private void LoadItems()
    {
        ItemDefinition[] definitions = Resources.LoadAll<ItemDefinition>(ItemDefinitionResourcePath);
        _rewardItemCandidates.Clear();
        _rewardItemCandidates.Capacity = definitions.Length;
        Array.Clear(_rewardItemCountsByRarity, 0, _rewardItemCountsByRarity.Length);
        _storeItemCandidates.Clear();
        _storeItemCandidates.Capacity = definitions.Length;
        Array.Clear(_storeItemCountsByTypeAndRarity, 0, _storeItemCountsByTypeAndRarity.Length);

        for (int index = 0; index < definitions.Length; index++)
        {
            ItemDefinition definition = definitions[index];
            if (!definition)
            {
                continue;
            }

            int typeIndex = (int)definition.ItemType;
            int rarityIndex = (int)definition.Rarity;
            if (typeIndex >= 0 && typeIndex < ItemTypeCount && rarityIndex >= 0 && rarityIndex < ItemRarityCount)
            {
                _storeItemCandidates.Add(definition);
                _storeItemCountsByTypeAndRarity[typeIndex * ItemRarityCount + rarityIndex]++;
            }

            // 보상 상자는 기존처럼 장비와 유물만 사용한다.
            if (definition.ItemType != ItemType.Equipment && definition.ItemType != ItemType.Relic) continue;
            _rewardItemCandidates.Add(definition);
            if (rarityIndex >= 0 && rarityIndex < _rewardItemCountsByRarity.Length)
            {
                _rewardItemCountsByRarity[rarityIndex]++;
            }
        }

        if (_rewardItemCandidates.Count == 0)
        {
            Debug.LogWarning("Resources/ItemDefinition에 보상 상자용 장비 또는 유물 아이템이 없습니다.", this);
        }

        if (_storeItemCandidates.Count == 0)
        {
            Debug.LogWarning("Resources/ItemDefinition에 상점용 아이템이 없습니다.", this);
        }
    }

    private void AssignStoreItems(Store store)
    {
        if (!store || store.HasAssignedItems || _storeItemCandidates.Count == 0) return;

        if (!TryFillStoreItemSelection())
        {
            Debug.LogWarning("상점에 사용할 아이템 후보와 양수 종류·희귀도 가중치가 없습니다.", store);
            return;
        }

        store.SetItems(_storeItemSelection);
    }

    public bool TryRerollStore(Store store, PlayerManager player)
    {
        if (!CanUseStore(store, player)) return false;

        int price = store.RerollPrice;
        if (player.CurrentPlayerStats.Money < price) return false;

        _isUpdatingStore = true;
        try
        {
            // 추첨을 먼저 완료해 후보가 없는 경우 기존 상품과 잔액, 누적 비용을 모두 보존한다.
            if (!TryFillStoreItemSelection() || !player.TrySpendMoney(price)) return false;

            store.SetRerolledItems(_storeItemSelection);
            return true;
        }
        finally
        {
            _isUpdatingStore = false;
        }
    }

    public bool TryPurchaseStoreItem(Store store, int itemIndex, ItemDefinition expectedItem, PlayerManager player)
    {
        if (!CanUseStore(store, player) || itemIndex < 0 || itemIndex >= Store.ItemCount) return false;

        ItemDefinition item = store.Items[itemIndex];
        // UI가 표시한 상품과 현재 칸이 일치할 때만 결제하여 빈 칸이나 이전 목록을 구매하지 않는다.
        if (!item || item != expectedItem) return false;

        _isUpdatingStore = true;
        try
        {
            // 결제·획득 알림에서도 해당 칸은 이미 품절로 보이며, 결제 실패에는 원래 상품을 복구한다.
            store.SetItem(itemIndex, null);
            if (player.TryPurchaseItem(item)) return true;

            store.SetItem(itemIndex, item);
            return false;
        }
        finally
        {
            _isUpdatingStore = false;
        }
    }

    private bool CanUseStore(Store store, PlayerManager player)
    {
        if (_isUpdatingStore || !isActiveAndEnabled || !store || !store.isActiveAndEnabled
            || !player || !player.isActiveAndEnabled || _stores == null || !_stores.Contains(store)) return false;

        return !_playerManager || player == _playerManager;
    }

    private bool TryFillStoreItemSelection()
    {
        if (_storeItemCandidates.Count == 0) return false;

        // 각 칸을 독립적으로 추첨해 중복을 허용하고, 완성된 목록만 상점에 복사한다.
        for (int index = 0; index < _storeItemSelection.Length; index++)
        {
            if (!TrySelectStoreItem(out _storeItemSelection[index]))
            {
                return false;
            }
        }

        return true;
    }

    private bool TrySelectStoreItem(out ItemDefinition item)
    {
        item = null;
        if (_storeItemCandidates.Count == 0) return false;

        // 스테이지 정의가 없는 테스트 씬에서는 유효한 아이템 전체를 균등 추첨한다.
        if (!_stageDefinition)
        {
            item = _storeItemCandidates[UnityEngine.Random.Range(0, _storeItemCandidates.Count)];
            return true;
        }

        if (!TrySelectStoreItemType(out ItemType itemType) || !TrySelectStoreItemRarity(itemType, out ItemRarity rarity)) return false;

        // 종류와 희귀도를 정한 뒤 해당 묶음 안에서만 균등 선택한다.
        int itemIndex = UnityEngine.Random.Range(0, _storeItemCountsByTypeAndRarity[(int)itemType * ItemRarityCount + (int)rarity]);
        for (int index = 0; index < _storeItemCandidates.Count; index++)
        {
            ItemDefinition candidate = _storeItemCandidates[index];
            if (candidate.ItemType != itemType || candidate.Rarity != rarity) continue;
            if (itemIndex-- != 0) continue;

            item = candidate;
            return true;
        }

        return false;
    }

    private float GetEligibleStoreItemTypeWeight(int typeIndex)
    {
        float weight = _stageDefinition.GetStoreItemTypeWeight((ItemType)typeIndex);
        if (weight <= 0f) return 0f;

        // 실제 후보와 양수 희귀도 가중치가 있는 종류만 추첨에 참여한다.
        for (int rarityIndex = 0; rarityIndex < ItemRarityCount; rarityIndex++)
        {
            if (_storeItemCountsByTypeAndRarity[typeIndex * ItemRarityCount + rarityIndex] > 0
                && _stageDefinition.GetStoreRarityWeight((ItemRarity)rarityIndex) > 0f)
            {
                return weight;
            }
        }

        return 0f;
    }

    private bool TrySelectStoreItemType(out ItemType itemType)
    {
        itemType = default;
        // 큰 float 가중치 여러 개를 합해도 넘치지 않도록 누적값은 double로 계산한다.
        double totalWeight = 0d;
        for (int index = 0; index < ItemTypeCount; index++)
        {
            totalWeight += GetEligibleStoreItemTypeWeight(index);
        }

        if (totalWeight <= 0d) return false;

        double randomWeight = UnityEngine.Random.value * totalWeight;
        for (int index = 0; index < ItemTypeCount; index++)
        {
            float weight = GetEligibleStoreItemTypeWeight(index);
            if (weight <= 0f) continue;

            itemType = (ItemType)index;
            if (randomWeight < weight) return true;
            randomWeight -= weight;
        }

        // Random.value가 1이거나 누적 오차가 있어도 마지막 유효 종류를 선택한다.
        return true;
    }

    private bool TrySelectStoreItemRarity(ItemType itemType, out ItemRarity rarity)
    {
        rarity = default;
        int typeOffset = (int)itemType * ItemRarityCount;
        double totalWeight = 0d;
        for (int index = 0; index < ItemRarityCount; index++)
        {
            if (_storeItemCountsByTypeAndRarity[typeOffset + index] == 0) continue;
            totalWeight += _stageDefinition.GetStoreRarityWeight((ItemRarity)index);
        }

        if (totalWeight <= 0d) return false;

        double randomWeight = UnityEngine.Random.value * totalWeight;
        for (int index = 0; index < ItemRarityCount; index++)
        {
            if (_storeItemCountsByTypeAndRarity[typeOffset + index] == 0) continue;
            float weight = _stageDefinition.GetStoreRarityWeight((ItemRarity)index);
            if (weight <= 0f) continue;

            rarity = (ItemRarity)index;
            if (randomWeight < weight) return true;
            randomWeight -= weight;
        }

        // 마지막 유효 희귀도를 보존하여 상한값과 누적 오차를 처리한다.
        return true;
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

    private void ResetStores(bool resetItems = false)
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

            if (resetItems)
            {
                // 새 그래프의 사전 배치 상점만 상품을 갱신하고 같은 그래프의 재등록은 유지한다.
                store.ClearItems();
            }

            SubscribeStore(store, resetItems);
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

    private void SubscribeStore(Store store, bool resetRerollPrice = false)
    {
        if (!_subscribedStores.Add(store)) return;
        store.Initialize(this, _stageDefinition ? _stageDefinition.BaseRerollPrice : 0, _stageDefinition ? _stageDefinition.RerollPriceIncrease : 0, resetRerollPrice);
        AssignStoreItems(store);
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
        ResetStores(graphChanged);

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
        // 첫 스폰이나 다음 그룹의 대기 조건을 처리하기 전에 상호작용부터 차단한다.
        room.SetDoorOpeningBlocked(true);
        // 첫 그룹의 조건을 즉시 확인하되, 이후 대기와 재시도는 Update에서 이어간다.
        encounter.Tick(0f);
        room.SetDoorOpeningBlocked(!encounter.IsComplete);
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
