using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ItemDropManager : Singleton<ItemDropManager>
{
    private const int ItemTypeCount = (int)ItemType.Relic + 1;
    private const int ItemRarityCount = (int)ItemRarity.Legendary + 1;
    private const string ItemResourcePath = "ItemDefinition";

    [Header("타입별 드랍 프리팹")]
    [SerializeField] private DroppedItem _equipmentPrefab;
    [SerializeField] private DroppedItem _consumablePrefab;
    [SerializeField] private DroppedItem _relicPrefab;

    // 스테이지가 바뀔 때 바닥에 남은 아이템도 공용 풀로 반환한다.
    private readonly List<DroppedItem> _droppedItems = new(32);
    // 타입·희귀도별 후보는 한 번만 나누어 보관하고 획득 시 임시 목록을 만들지 않는다.
    private readonly List<ItemDefinition>[] _itemCandidates = new List<ItemDefinition>[ItemTypeCount * ItemRarityCount];
    private MapGenerator _mapGenerator;

    protected override void OnSingletonAwake()
    {
        TryGetComponent(out _mapGenerator);
        ItemDefinition[] definitions = Resources.LoadAll<ItemDefinition>(ItemResourcePath);
        for (int index = 0; index < definitions.Length; index++)
        {
            ItemDefinition item = definitions[index];
            if (!item) continue;

            int typeIndex = (int)item.ItemType;
            int rarityIndex = (int)item.Rarity;
            if (typeIndex < 0 || typeIndex >= ItemTypeCount || rarityIndex < 0 || rarityIndex >= ItemRarityCount) continue;

            int candidateIndex = typeIndex * ItemRarityCount + rarityIndex;
            _itemCandidates[candidateIndex] ??= new List<ItemDefinition>();
            _itemCandidates[candidateIndex].Add(item);
        }
    }

    private void OnEnable()
    {
        if (_mapGenerator) _mapGenerator.GraphChanged += HandleGraphChanged;
    }

    private void OnDisable()
    {
        if (_mapGenerator) _mapGenerator.GraphChanged -= HandleGraphChanged;
        ClearDroppedItems();
    }

    public bool TryDropItem(EnemyDefinition definition, Vector3 position)
    {
        PoolingManager pool = PoolingManager.Instance;
        if (!isActiveAndEnabled || !definition || !pool || !pool.isActiveAndEnabled) return false;

        // 0%와 NaN은 제외하며, 100%는 난수의 상한값과 관계없이 항상 드랍한다.
        float chance = Mathf.Clamp(definition.ItemDropChance, 0f, 100f);
        if (!(chance > 0f) || (chance < 100f && Random.value * 100f >= chance)) return false;
        if (!TrySelectItemType(definition, out ItemType itemType) || !TrySelectRarity(definition, itemType, out ItemRarity rarity)) return false;

        DroppedItem prefab = GetPrefab(itemType);
        DroppedItem droppedItem = pool.Spawn(prefab, position, prefab.transform.rotation);
        if (!droppedItem) return false;

        // 타입은 프리팹이 표현하고 희귀도는 이번 스폰의 파티클 색상에 전달한다.
        droppedItem.Initialize(rarity, this);
        _droppedItems.Add(droppedItem);
        return true;
    }

    public bool TryResolveItem(ItemType itemType, ItemRarity rarity, out ItemDefinition item)
    {
        item = null;
        int typeIndex = (int)itemType;
        int rarityIndex = (int)rarity;
        if (!isActiveAndEnabled || typeIndex < 0 || typeIndex >= ItemTypeCount || rarityIndex < 0 || rarityIndex >= ItemRarityCount) return false;

        List<ItemDefinition> candidates = _itemCandidates[typeIndex * ItemRarityCount + rarityIndex];
        if (candidates == null || candidates.Count == 0) return false;

        item = candidates[Random.Range(0, candidates.Count)];
        return item;
    }

    internal void UnregisterDroppedItem(DroppedItem item)
    {
        // 획득 후 반환된 인스턴스가 목록에 누적되거나 재사용 시 중복 등록되지 않도록 제거한다.
        _droppedItems.Remove(item);
    }

    private DroppedItem GetPrefab(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Equipment => _equipmentPrefab,
            ItemType.Consumable => _consumablePrefab,
            ItemType.Relic => _relicPrefab,
            _ => null
        };
    }

    private float GetItemTypeWeight(EnemyDefinition definition, int typeIndex)
    {
        ItemType itemType = (ItemType)typeIndex;
        DroppedItem prefab = GetPrefab(itemType);
        if (!prefab || prefab.ItemType != itemType) return 0f;

        // 실제 획득할 후보와 양수 희귀도 가중치가 있는 타입만 드랍한다.
        for (int rarityIndex = 0; rarityIndex < ItemRarityCount; rarityIndex++)
        {
            if (HasItemCandidates(typeIndex, rarityIndex) && definition.GetDropRarityWeight((ItemRarity)rarityIndex) > 0f)
                return definition.GetDropItemTypeWeight(itemType);
        }
        return 0f;
    }

    private bool HasItemCandidates(int typeIndex, int rarityIndex)
    {
        List<ItemDefinition> candidates = _itemCandidates[typeIndex * ItemRarityCount + rarityIndex];
        return candidates != null && candidates.Count > 0;
    }

    private bool TrySelectItemType(EnemyDefinition definition, out ItemType itemType)
    {
        itemType = default;
        // 큰 가중치를 여러 개 더해도 넘치지 않도록 합계만 double로 계산한다.
        double totalWeight = 0d;
        for (int index = 0; index < ItemTypeCount; index++)
        {
            totalWeight += GetItemTypeWeight(definition, index);
        }
        if (totalWeight <= 0d) return false;

        double randomWeight = Random.value * totalWeight;
        for (int index = 0; index < ItemTypeCount; index++)
        {
            float weight = GetItemTypeWeight(definition, index);
            if (weight <= 0f) continue;

            itemType = (ItemType)index;
            if (randomWeight < weight) return true;
            randomWeight -= weight;
        }

        // Random.value가 1이거나 누적 오차가 있어도 마지막 유효 타입을 선택한다.
        return true;
    }

    private bool TrySelectRarity(EnemyDefinition definition, ItemType itemType, out ItemRarity rarity)
    {
        rarity = default;
        int typeIndex = (int)itemType;
        double totalWeight = 0d;
        for (int index = 0; index < ItemRarityCount; index++)
        {
            if (!HasItemCandidates(typeIndex, index)) continue;
            totalWeight += definition.GetDropRarityWeight((ItemRarity)index);
        }
        if (totalWeight <= 0d) return false;

        double randomWeight = Random.value * totalWeight;
        for (int index = 0; index < ItemRarityCount; index++)
        {
            if (!HasItemCandidates(typeIndex, index)) continue;
            float weight = definition.GetDropRarityWeight((ItemRarity)index);
            if (weight <= 0f) continue;

            rarity = (ItemRarity)index;
            if (randomWeight < weight) return true;
            randomWeight -= weight;
        }

        return true;
    }

    private void HandleGraphChanged(StageGraph graph)
    {
        ClearDroppedItems();
    }

    public void ClearDroppedItems()
    {
        while (_droppedItems.Count > 0)
        {
            int index = _droppedItems.Count - 1;
            DroppedItem item = _droppedItems[index];
            // 반환 콜백에서도 등록 해제가 가능하도록 먼저 목록에서 제거한다.
            _droppedItems.RemoveAt(index);
            if (item && item.IsSpawned) item.ReturnToPool();
        }
    }
}
