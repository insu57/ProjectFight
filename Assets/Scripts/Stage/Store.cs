using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Store : MonoBehaviour
{
    public const int ItemCount = 4;

    [Header("상점 상호작용")]
    [SerializeField] private StoreInteractionTrigger _interactionTrigger;

    private bool _isRequestingInteraction;
    private StageManager _stageManager;
    private bool _isInitialized;
    private bool _hasRerollSettings;
    // 상품 데이터는 UI와 분리해 보관하여 창을 다시 열어도 같은 목록을 표시한다.
    private readonly ItemDefinition[] _items = new ItemDefinition[ItemCount];

    public bool CanInteract => isActiveAndEnabled && _isInitialized && !_isRequestingInteraction;
    public bool HasItems
    {
        get
        {
            for (int index = 0; index < _items.Length; index++)
            {
                if (_items[index]) return true;
            }

            return false;
        }
    }
    // 전부 판매된 목록도 준비된 목록이므로 같은 스테이지의 재등록에서 재추첨하지 않는다.
    public bool HasAssignedItems { get; private set; }
    public IReadOnlyList<ItemDefinition> Items => _items;
    public int BaseRerollPrice { get; private set; }
    public int RerollPriceIncrease { get; private set; }
    public int RerollPrice { get; private set; }
    // 사용 기록은 상점에 남겨 창 재개방이나 효과 재장착으로 무료 횟수가 복구되지 않게 한다.
    public int UsedFreeRerollCount { get; private set; }

    // 상점과 이용 플레이어를 전달하여 이후 상점창이 같은 요청을 받을 수 있게 한다.
    public event Action<Store, PlayerManager> InteractionRequested;

    private void Awake()
    {
        // 프리팹에서 지정한 Trigger에 소유 상점을 주입하고 계층 검색은 하지 않는다.
        if (_interactionTrigger)
        {
            _interactionTrigger.Initialize(this);
        }
    }

    public void Initialize(StageManager stageManager, int baseRerollPrice, int rerollPriceIncrease, bool resetRerollPrice = false)
    {
        if (!stageManager)
        {
            // 주입 실패 후에도 남아 있는 이전 스테이지 참조로 상점을 이용하지 않도록 한다.
            _stageManager = null;
            _isInitialized = false;
            Debug.LogError("상점 초기화 실패: StageManager 참조가 없습니다.", this);
            return;
        }

        bool managerChanged = _stageManager != stageManager;
        _stageManager = stageManager;
        BaseRerollPrice = Mathf.Max(0, baseRerollPrice);
        RerollPriceIncrease = Mathf.Max(0, rerollPriceIncrease);

        // 같은 그래프의 재등록에서는 누적 비용을 유지하고 최초 주입과 새 스테이지에서만 초기화한다.
        if (!_hasRerollSettings || managerChanged || resetRerollPrice)
        {
            RerollPrice = BaseRerollPrice;
            UsedFreeRerollCount = 0;
        }

        _hasRerollSettings = true;
        _isInitialized = true;
    }

    public bool TryReroll(PlayerManager player)
    {
        return _isInitialized && _stageManager.TryRerollStore(this, player);
    }

    public bool TryPurchaseItem(int itemIndex, ItemDefinition expectedItem, PlayerManager player)
    {
        return _isInitialized && _stageManager.TryPurchaseStoreItem(this, itemIndex, expectedItem, player);
    }

    internal void SetItem(int itemIndex, ItemDefinition item)
    {
        if (itemIndex < 0 || itemIndex >= _items.Length) return;

        _items[itemIndex] = item;
    }

    public int GetRemainingFreeRerolls(PlayerStatSnapshot stats)
    {
        return Mathf.Max(0, stats.FreeRerollCount - UsedFreeRerollCount);
    }

    public void SetRerolledItems(IReadOnlyList<ItemDefinition> items, bool isFreeReroll = false)
    {
        SetItems(items);
        // 추첨이 성공한 무료 리롤만 소비하고 유료 리롤의 누적 가격은 유지한다.
        if (isFreeReroll)
        {
            if (UsedFreeRerollCount < int.MaxValue) UsedFreeRerollCount++;
            return;
        }

        // 반복 리롤로 금액이 int 범위를 넘더라도 음수 비용이 되지 않도록 상한을 유지한다.
        RerollPrice = (int)Math.Min(int.MaxValue, (long)RerollPrice + RerollPriceIncrease);
    }

    public void SetItems(IReadOnlyList<ItemDefinition> items)
    {
        int itemCount = items?.Count ?? 0;
        for (int index = 0; index < _items.Length; index++)
        {
            // 매니저의 공용 추첨 버퍼를 참조하지 않고 상점 자신의 고정 배열로 복사한다.
            _items[index] = index < itemCount ? items[index] : null;
        }

        HasAssignedItems = true;
    }

    public void ClearItems()
    {
        Array.Clear(_items, 0, _items.Length);
        HasAssignedItems = false;
    }

    public bool TryInteract(PlayerManager player)
    {
        if (!CanInteract || !player || !player.isActiveAndEnabled)
        {
            return false;
        }

        // 이벤트 처리 중의 재진입만 막고 요청이 끝나면 다시 이용할 수 있게 한다.
        _isRequestingInteraction = true;
        try
        {
            Debug.Log("상점 상호작용 테스트: 상점창 열기 요청", this);
            InteractionRequested?.Invoke(this, player);
            return true;
        }
        finally
        {
            _isRequestingInteraction = false;
        }
    }
}
