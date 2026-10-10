using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum ItemType
{
    Equipment = 0,
    Consumable = 1,
    Relic = 2
}

public enum EquipmentSlot
{
    // 기존 장비 SO도 부위가 지정되지 않은 상태(None)로 유지한다.
    None = 0,
    Head = 1,       // 머리
    UpperBody = 2,  // 상의
    LowerBody = 3,  // 하의
    Gloves = 4,     // 장갑
    Shoes = 5      // 신발
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
    // 장비의 고정 장착 부위이며 실제 착용 상태는 런타임에서 관리한다.
    [field: SerializeField] public EquipmentSlot EquipmentSlot { get; private set; } = EquipmentSlot.None;
    [field: SerializeField] public ItemRarity Rarity { get; private set; } = ItemRarity.Common;
    // 개별 아이템 1개의 기준 가격이며 보유 금액과 같은 정수 단위를 사용한다.
    [field: SerializeField, Min(0)] public int BasePrice { get; private set; }

    // 한 묶음의 최대 수량이다. 장비와 유물은 같은 종류라도 각각 별도 항목으로 보유한다.
    [SerializeField, Min(1)] private int _maxStackCount = 1;
    public int MaxStackCount => ItemType == ItemType.Consumable ? _maxStackCount : 1;

    // 장비는 착용 중, 유물은 보유 중 적용할 스탯 보정이다. 기본 스탯과 별도로 합산한다.
    [Header("아이템 효과")]

    [SerializeField, Tooltip("장비 착용 중 또는 유물 보유 중 적용할 스탯 보정입니다.")]
    private StatModifier[] _statModifiers = Array.Empty<StatModifier>();

    // 사용과 획득은 서로 다른 시작 시점이며, 시간 효과의 진행 상태는 플레이어 런타임에서 관리한다.
    [SerializeReference, Tooltip("사용에 성공했을 때 시작할 효과입니다. 즉시 효과와 시간 효과의 종류 및 수치를 이 아이템 안에서 설정합니다.")]
    private ItemEffectData[] _useEffectData = Array.Empty<ItemEffectData>();

    [SerializeReference, Tooltip("실제 획득이 확정됐을 때 한 번 시작할 효과입니다.")]
    private ItemEffectData[] _acquireEffectData = Array.Empty<ItemEffectData>();

    [SerializeReference, Tooltip("장비 착용 중 또는 유물 보유 중 활성화할 조건 및 이벤트 효과입니다.")]
    private PassiveItemEffectData[] _passiveEffectData = Array.Empty<PassiveItemEffectData>();


    // 공유 배열을 복사하지 않고 조회한다. 기존 SO에 효과가 없으면 빈 목록을 반환한다.
    public IReadOnlyList<StatModifier> StatModifiers => _statModifiers ?? Array.Empty<StatModifier>();
    public IReadOnlyList<ItemEffectData> UseEffects => _useEffectData ?? Array.Empty<ItemEffectData>();
    public IReadOnlyList<ItemEffectData> AcquireEffects => _acquireEffectData ?? Array.Empty<ItemEffectData>();
    public IReadOnlyList<PassiveItemEffectData> PassiveEffects => _passiveEffectData ?? Array.Empty<PassiveItemEffectData>();

    private void OnValidate()
    {
        // 소모품과 유물에는 장비용 장착 부위를 저장하지 않는다.
        if (ItemType != ItemType.Equipment)
        {
            EquipmentSlot = EquipmentSlot.None;
        }

        // Inspector에 저장되는 값도 종류별 중첩 규칙에 맞춰 보정한다.
        _maxStackCount = MaxStackCount;
        // 기본 가격은 음수로 저장되지 않도록 보정한다.
        BasePrice = Mathf.Max(0, BasePrice);
        ValidateEffects();
    }

    private void ValidateEffects()
    {
        // 일반 클래스에는 Unity 콜백이 없으므로 소유 아이템에서 추가 할당 없이 검증한다.
        if (_useEffectData != null)
        {
            for (int i = 0; i < _useEffectData.Length; i++) _useEffectData[i]?.Validate();
        }
        if (_acquireEffectData != null)
        {
            for (int i = 0; i < _acquireEffectData.Length; i++) _acquireEffectData[i]?.Validate();
        }
        if (_passiveEffectData != null)
        {
            for (int i = 0; i < _passiveEffectData.Length; i++) _passiveEffectData[i]?.Validate();
        }
    }
}
