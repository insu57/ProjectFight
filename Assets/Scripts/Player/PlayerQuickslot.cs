using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerQuickslot : MonoBehaviour
{
    public const int SlotCount = 4;

    // 보유 수량은 PlayerInventory가 관리하고 Manager가 등록 가능 여부를 중개한다. 각 칸은 공유 Definition만 참조한다.
    [SerializeField] private ItemDefinition[] _items = new ItemDefinition[SlotCount];

    public event Action Changed;

    private void Awake()
    {
        ValidateSlots();
    }

    private void OnValidate()
    {
        ValidateSlots();
    }

    public ItemDefinition GetItem(int slotNumber)
    {
        return slotNumber >= 1 && slotNumber <= SlotCount ? _items[slotNumber - 1] : null;
    }

    public int GetSlotNumber(ItemDefinition item)
    {
        if (!item || item.ItemType != ItemType.Consumable) return 0;

        for (int i = 0; i < SlotCount; i++)
        {
            if (_items[i] == item) return i + 1;
        }

        return 0;
    }

    public bool TryRegister(int slotNumber, ItemDefinition item, bool toggleIfSame = false)
    {
        if (!isActiveAndEnabled || slotNumber < 1 || slotNumber > SlotCount || !item || item.ItemType != ItemType.Consumable) return false;

        int targetIndex = slotNumber - 1;
        if (_items[targetIndex] == item)
        {
            // 같은 칸 재입력 해제는 키보드 등록에서만 요청한다. 패널의 칸 선택은 등록을 유지한다.
            if (toggleIfSame)
            {
                _items[targetIndex] = null;
                Changed?.Invoke();
            }

            return true;
        }

        int previousSlot = GetSlotNumber(item);
        if (previousSlot > 0)
        {
            // 맞교환하지 않고 기존 칸을 비운 뒤 목적지의 아이템을 교체한다.
            _items[previousSlot - 1] = null;
        }

        _items[targetIndex] = item;
        // 이동으로 두 칸이 바뀌더라도 완성된 상태를 한 번만 알린다.
        Changed?.Invoke();
        return true;
    }

    public bool TryUnregister(ItemDefinition item)
    {
        if (!isActiveAndEnabled) return false;
        int slotNumber = GetSlotNumber(item);
        if (slotNumber == 0) return false;

        _items[slotNumber - 1] = null;
        Changed?.Invoke();
        return true;
    }

    private void ValidateSlots()
    {
        // Inspector에서 배열 크기를 바꾸거나 다른 타입을 넣어도 고정 4칸과 중복 금지를 유지한다.
        if (_items == null || _items.Length != SlotCount) Array.Resize(ref _items, SlotCount);
        for (int i = 0; i < SlotCount; i++)
        {
            ItemDefinition item = _items[i];
            if (!item || item.ItemType != ItemType.Consumable || GetSlotNumber(item) != i + 1) _items[i] = null;
        }
    }
}
