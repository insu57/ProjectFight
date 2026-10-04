using UnityEngine;

public enum ItemType
{
    Equipment = 0,
    Consumable = 1,
    Relic = 2
}

public enum ItemRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4
}

[CreateAssetMenu(fileName = "ItemDefinition", menuName = "Items/ItemDefinition")]
public sealed class ItemDefinition : ScriptableObject
{
    // 아이템 종류의 공유 데이터이며 개별 보유 아이템의 식별자와 수량은 런타임에서 관리한다.
    [field: SerializeField, Min(0)] public int ItemID { get; private set; }
    [field: SerializeField] public string ItemName { get; private set; }
    [field: SerializeField, TextArea] public string ItemDescription { get; private set; }
    [field: SerializeField] public Sprite ItemIcon { get; private set; }
    [field: SerializeField] public ItemType ItemType { get; private set; }
    [field: SerializeField] public ItemRarity Rarity { get; private set; } = ItemRarity.Common;
    // 개별 아이템 1개의 기준 가격이며 보유 금액과 같은 정수 단위를 사용한다.
    [field: SerializeField, Min(0)] public int BasePrice { get; private set; }

    // 한 묶음의 최대 수량이다. 장비와 유물은 같은 종류라도 각각 별도 항목으로 보유한다.
    [SerializeField, Min(1)] private int _maxStackCount = 1;
    public int MaxStackCount => ItemType == ItemType.Consumable ? _maxStackCount : 1;

    private void OnValidate()
    {
        // Inspector에 저장되는 값도 종류별 중첩 규칙에 맞춰 보정한다.
        _maxStackCount = MaxStackCount;
        // 기본 가격은 음수로 저장되지 않도록 보정한다.
        BasePrice = Mathf.Max(0, BasePrice);
    }
}
