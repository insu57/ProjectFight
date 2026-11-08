using UnityEngine;

[DisallowMultipleComponent]
public sealed class DroppedItem : PoolableObject
{
    // 획득 전에는 개별 아이템 정보 대신 프리팹에 지정된 타입만 표현한다.
    [field: SerializeField] public ItemType ItemType { get; private set; }
    [field: SerializeField] public ParticleSystem ItemEffect { get; private set; }
    [SerializeField] private DroppedItemInteractionTrigger _interactionTrigger;

    // 개별 아이템을 획득할 때 사용할 희귀도만 드랍 시점에 확정한다.
    public ItemRarity Rarity { get; private set; }

    private ParticleSystem[] _itemEffects;
    private ParticleSystem.MinMaxGradient[] _defaultStartColors;
    private ItemDropManager _dropManager;
    private ItemDefinition _resolvedItem;
    private bool _isInitialized;
    private bool _isPickingUp;
    private uint _spawnVersion;

    public bool CanPickUp => isActiveAndEnabled && _isInitialized && !_isPickingUp;

    private void Awake()
    {
        if (_interactionTrigger) _interactionTrigger.Initialize(this);
        if (!ItemEffect) return;

        // 자식 파티클과 원래 투명도는 최초 한 번만 보관하여 풀 재사용 시 할당을 피한다.
        _itemEffects = ItemEffect.GetComponentsInChildren<ParticleSystem>(true);
        _defaultStartColors = new ParticleSystem.MinMaxGradient[_itemEffects.Length];
        for (int index = 0; index < _itemEffects.Length; index++)
        {
            _defaultStartColors[index] = _itemEffects[index].main.startColor;
        }
    }

    public void Initialize(ItemRarity rarity, ItemDropManager dropManager)
    {
        Rarity = rarity;
        _dropManager = dropManager;
        _isInitialized = dropManager;
        if (!ItemEffect || _itemEffects == null) return;

        // 활성화 직후 자동 재생된 입자와 이전 드랍의 색상을 먼저 지운다.
        ItemEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Color rarityColor = ItemDisplayUtility.GetRarityColor(rarity);
        for (int index = 0; index < _itemEffects.Length; index++)
        {
            ParticleSystem.MainModule main = _itemEffects[index].main;
            ParticleSystem.MinMaxGradient defaultColor = _defaultStartColors[index];
            Color minimumColor = rarityColor;
            Color maximumColor = rarityColor;
            minimumColor.a = defaultColor.colorMin.a;
            maximumColor.a = defaultColor.colorMax.a;
            // 수명에 따른 페이드와 시작 투명도 범위는 프리팹의 기존 설정을 유지한다.
            main.startColor = defaultColor.mode == ParticleSystemGradientMode.TwoColors
                ? new ParticleSystem.MinMaxGradient(minimumColor, maximumColor)
                : new ParticleSystem.MinMaxGradient(maximumColor);
        }

        ItemEffect.Play(true);
    }

    public bool TryPickUp(PlayerManager player)
    {
        if (!CanPickUp || !player || !player.isActiveAndEnabled || !_dropManager || !_dropManager.isActiveAndEnabled) return false;

        // 최초 시도에서 정한 아이템은 보유량 초과로 실패해도 유지하여 재시도로 다시 추첨하지 않는다.
        if (!_resolvedItem && !_dropManager.TryResolveItem(ItemType, Rarity, out _resolvedItem)) return false;

        uint spawnVersion = _spawnVersion;
        _isPickingUp = true;
        try
        {
            // 인벤토리와 UI 알림은 플레이어 Manager를 통해서만 처리한다.
            if (!player.TryPickUpItem(_resolvedItem)) return false;

            // 획득 알림에서 스테이지가 바뀌거나 이 인스턴스를 재사용했으면 새 드랍은 건드리지 않는다.
            if (this && _spawnVersion == spawnVersion && _isInitialized)
            {
                _isInitialized = false;
                if (!ReturnToPool()) gameObject.SetActive(false);
            }
            return true;
        }
        finally
        {
            if (this && _spawnVersion == spawnVersion) _isPickingUp = false;
        }
    }

    protected override void OnPoolSpawned()
    {
        _spawnVersion++;
        ResetDrop();
    }

    protected override void OnPoolDespawned()
    {
        ResetDrop();
    }

    private void ResetDrop()
    {
        if (_dropManager) _dropManager.UnregisterDroppedItem(this);
        _dropManager = null;
        _resolvedItem = null;
        _isInitialized = false;
        _isPickingUp = false;
        Rarity = ItemRarity.Common;
        if (ItemEffect) ItemEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
