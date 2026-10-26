using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInventory : MonoBehaviour
{
    // 타입별 목록을 재사용하며 같은 장비·유물도 획득한 수만큼 별도 항목으로 보관한다.
    private readonly List<ItemDefinition> _equipmentItems = new(16);
    // 소모품 목록은 최초 획득 순서의 Definition만 보관하고 실제 수량은 Dictionary에서 관리한다.
    private readonly List<ItemDefinition> _consumableItems = new(16);
    private readonly Dictionary<ItemDefinition, int> _consumableItemCounts = new(16);
    private readonly List<ItemDefinition> _relicItems = new(16);

    // 장비 목록은 획득 순서로 추가만 하므로 인덱스로 같은 Definition의 개별 보유 장비를 구분한다.
    // None, 머리, 상의, 하의, 장갑, 신발 순서이며 -1은 해당 부위에 장착한 장비가 없다는 뜻이다.
    private readonly int[] _equippedEquipmentIndices = { -1, -1, -1, -1, -1, -1 };

    public event Action<ItemType> InventoryChanged;
    public event Action<ItemDefinition> ItemAcquired;
    public event Action<EquipmentSlot> EquipmentChanged;

    public IReadOnlyList<ItemDefinition> GetInventoryItems(ItemType itemType)
    {
        // 조회 때 필터링 목록을 할당하지 않고 해당 타입의 보유 목록을 그대로 전달한다.
        IReadOnlyList<ItemDefinition> items = GetInventoryList(itemType);
        return items ?? Array.Empty<ItemDefinition>();
    }

    public int GetConsumableCount(ItemDefinition item)
    {
        // 소모품 목록의 길이는 종류 수이므로 수량은 공유 Definition을 키로 바로 조회한다.
        return item && item.ItemType == ItemType.Consumable && _consumableItemCounts.TryGetValue(item, out int count) ? count : 0;
    }

    public bool IsConsumableStackFull(ItemDefinition item)
    {
        // 장비와 유물은 개별 항목으로 보관하며 소모품에만 종류별 보유 상한을 적용한다.
        return item && item.ItemType == ItemType.Consumable && GetConsumableCount(item) >= Mathf.Max(1, item.MaxStackCount);
    }

    internal bool TryConsumeConsumable(int inventoryIndex, ItemDefinition expectedItem)
    {
        if (!isActiveAndEnabled || !expectedItem || expectedItem.ItemType != ItemType.Consumable
            || inventoryIndex < 0 || inventoryIndex >= _consumableItems.Count || _consumableItems[inventoryIndex] != expectedItem) return false;
        if (!_consumableItemCounts.TryGetValue(expectedItem, out int count) || count <= 0) return false;

        // 효과 알림보다 먼저 1개를 확보해 같은 보유분이 중복 사용되지 않도록 한다.
        if (count > 1)
        {
            _consumableItemCounts[expectedItem] = count - 1;
        }
        else
        {
            _consumableItemCounts.Remove(expectedItem);
            _consumableItems.RemoveAt(inventoryIndex);
        }

        return true;
    }

    internal void NotifyConsumableCountChanged()
    {
        // Manager가 효과 실행까지 마친 뒤 목록과 능력치가 모두 반영된 상태를 UI에 전달한다.
        InventoryChanged?.Invoke(ItemType.Consumable);
    }

    public ItemDefinition GetEquippedItem(EquipmentSlot equipmentSlot)
    {
        int slotIndex = (int)equipmentSlot;
        if (slotIndex <= 0 || slotIndex >= _equippedEquipmentIndices.Length) return null;

        int inventoryIndex = _equippedEquipmentIndices[slotIndex];
        ItemDefinition item = inventoryIndex >= 0 && inventoryIndex < _equipmentItems.Count ? _equipmentItems[inventoryIndex] : null;
        return item && item.ItemType == ItemType.Equipment && item.EquipmentSlot == equipmentSlot ? item : null;
    }

    public bool CanToggleEquipment(int inventoryIndex, ItemDefinition expectedItem)
    {
        if (!expectedItem || expectedItem.ItemType != ItemType.Equipment || inventoryIndex < 0
            || inventoryIndex >= _equipmentItems.Count || _equipmentItems[inventoryIndex] != expectedItem) return false;

        // 미보유 항목, 오래된 슬롯 바인딩과 부위 미지정 장비는 장착 대상으로 사용하지 않는다.
        int slotIndex = (int)expectedItem.EquipmentSlot;
        return isActiveAndEnabled && slotIndex > 0 && slotIndex < _equippedEquipmentIndices.Length;
    }

    public bool IsEquipmentEquipped(int inventoryIndex, ItemDefinition expectedItem)
    {
        if (!expectedItem || expectedItem.ItemType != ItemType.Equipment || inventoryIndex < 0
            || inventoryIndex >= _equipmentItems.Count || _equipmentItems[inventoryIndex] != expectedItem) return false;

        int slotIndex = (int)expectedItem.EquipmentSlot;
        return slotIndex > 0 && slotIndex < _equippedEquipmentIndices.Length && _equippedEquipmentIndices[slotIndex] == inventoryIndex;
    }

    public bool TryToggleEquipment(int inventoryIndex, ItemDefinition expectedItem)
    {
        if (!CanToggleEquipment(inventoryIndex, expectedItem)) return false;

        EquipmentSlot equipmentSlot = expectedItem.EquipmentSlot;
        int slotIndex = (int)equipmentSlot;
        // 같은 보유 항목은 해제하고 다른 항목은 교체한다. 보유 목록과 스탯은 변경하지 않는다.
        _equippedEquipmentIndices[slotIndex] = _equippedEquipmentIndices[slotIndex] == inventoryIndex ? -1 : inventoryIndex;
        EquipmentChanged?.Invoke(equipmentSlot);
        return true;
    }

    public bool TryAddItem(ItemDefinition item)
    {
        if (!TryPrepareItemAddition(item, out ItemType itemType)) return false;

        CommitItemAddition(item, itemType);
        return true;
    }

    internal bool TryPrepareItemAddition(ItemDefinition item, out ItemType itemType)
    {
        itemType = item ? item.ItemType : default;
        // 구매를 중개하는 Manager도 결제 전에 최대 수량을 포함한 추가 가능 여부와 종류를 확정한다.
        return isActiveAndEnabled && item && GetInventoryList(itemType) != null && !IsConsumableStackFull(item);
    }

    internal void CommitItemAddition(ItemDefinition item, ItemType itemType)
    {
        // 사전에 검증한 획득을 확정한다. 결제 중 비활성화되어도 이미 지불한 아이템은 추가한다.
        List<ItemDefinition> items = GetInventoryList(itemType);
        if (itemType == ItemType.Consumable)
        {
            // 기존 소모품은 수량만 늘려 같은 Definition이 목록과 그리드에 중복되지 않도록 한다.
            bool alreadyOwned = _consumableItemCounts.TryGetValue(item, out int count);
            _consumableItemCounts[item] = count + 1;
            if (!alreadyOwned)
            {
                items.Add(item);
            }
        }
        else
        {
            // 장비와 유물은 같은 공유 SO라도 획득한 수만큼 개별 항목을 유지한다.
            items.Add(item);
        }

        InventoryChanged?.Invoke(itemType);
        // 수량과 목록이 반영된 후 획득 알림을 보내 기존 HUD와 구매 흐름을 유지한다.
        ItemAcquired?.Invoke(item);
    }

    private List<ItemDefinition> GetInventoryList(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Equipment => _equipmentItems,
            ItemType.Consumable => _consumableItems,
            ItemType.Relic => _relicItems,
            _ => null
        };
    }
}
