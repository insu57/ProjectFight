using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemEffectTrigger
{
    EnemyKilled = 0,
    AttackHit = 1,
    DamageTaken = 2,
    GuardSucceeded = 3,
    RoomCleared = 4
}

public enum ItemRecoveryTarget
{
    [InspectorName("체력")] Health = 0,
    [InspectorName("에너지")] Energy = 1
}

// 동일 아이템의 동일 효과 항목을 다시 시작할 때 사용할 정책이며, 실행 상태는 런타임에서 관리한다.
public enum ItemEffectStackingPolicy
{
    [InspectorName("지속시간 갱신")] RefreshDuration = 0,
    [InspectorName("개별 중첩")] Independent = 1
}

// Inspector의 효과 선택 목록에 표시할 이름이다. 새 효과 클래스에도 사용할 수 있다.
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ItemEffectNameAttribute : Attribute
{
    public string Name { get; }

    public ItemEffectNameAttribute(string name)
    {
        Name = name;
    }
}

// 사용·획득·이벤트 발동 시 시작할 효과 데이터이다. 시간 효과도 설정만 저장하며 실행 상태와 플레이어 참조는 보관하지 않는다.
[Serializable]
public abstract class ItemEffectData
{
    public abstract void Validate();
}

// 장비 착용 또는 유물 보유 중 활성화되는 패시브 효과를 사용·획득 시 시작하는 효과와 구분한다.
[Serializable]
public abstract class PassiveItemEffectData
{
    public abstract void Validate();
}

[Serializable, ItemEffectName("체력 회복")]
public sealed class RestoreHealthEffectData : ItemEffectData
{
    [SerializeField] private ItemRecoveryAmount _amount = new(ItemRecoveryMode.Fixed, 50f);

    public ItemRecoveryAmount Amount => _amount;

    public RestoreHealthEffectData() { }

    public RestoreHealthEffectData(ItemRecoveryAmount amount)
    {
        _amount = amount;
    }

    public override void Validate()
    {
        _amount.Validate();
    }
}

[Serializable, ItemEffectName("에너지 회복")]
public sealed class RestoreEnergyEffectData : ItemEffectData
{
    [SerializeField] private ItemRecoveryAmount _amount = new(ItemRecoveryMode.Fixed, 30f);

    public ItemRecoveryAmount Amount => _amount;

    public RestoreEnergyEffectData() { }

    public RestoreEnergyEffectData(ItemRecoveryAmount amount)
    {
        _amount = amount;
    }

    public override void Validate()
    {
        _amount.Validate();
    }
}

[Serializable, ItemEffectName("지속 회복")]
public sealed class RecoveryOverTimeEffectData : ItemEffectData
{
    [SerializeField] private ItemRecoveryTarget _target;

    [SerializeField, Min(0.01f), Tooltip("회복 효과가 유지되는 시간(초)입니다. 종료 시점까지 도달한 회복 간격만 적용합니다.")]
    private float _duration = 10f;

    [SerializeField, Min(0.01f), Tooltip("회복 간격(초)입니다. 첫 회복은 이 시간이 지난 뒤 발생하며, 지속시간을 초과할 수 없습니다.")]
    private float _interval = 1f;

    [SerializeField, Tooltip("매 회복 시점에 적용할 회복량입니다. MaxRatio는 해당 시점의 최대 체력 또는 최대 에너지 비율입니다.")]
    private ItemRecoveryAmount _amount = new(ItemRecoveryMode.Fixed, 5f);

    [SerializeField, Tooltip("동일 아이템의 동일 효과를 재사용할 때 지속시간을 갱신하거나 각 사용을 별도로 중첩합니다.")]
    private ItemEffectStackingPolicy _stackingPolicy = ItemEffectStackingPolicy.RefreshDuration;

    public ItemRecoveryTarget Target => _target;
    public float Duration => _duration;
    public float Interval => _interval;
    public ItemRecoveryAmount Amount => _amount;
    public ItemEffectStackingPolicy StackingPolicy => _stackingPolicy;

    public RecoveryOverTimeEffectData() { }

    public RecoveryOverTimeEffectData(ItemRecoveryTarget target, float duration, float interval,
        ItemRecoveryAmount amount, ItemEffectStackingPolicy stackingPolicy = ItemEffectStackingPolicy.RefreshDuration)
    {
        _target = target;
        _duration = duration;
        _interval = interval;
        _amount = amount;
        _stackingPolicy = stackingPolicy;
        Validate();
    }

    public override void Validate()
    {
        // 양수인 지속시간과 종료 전까지 최소 한 번 회복할 수 있는 간격을 보장한다.
        _duration = Mathf.Max(0.01f, _duration);
        _interval = Mathf.Clamp(_interval, 0.01f, _duration);
        _amount.Validate();
    }
}

[Serializable, ItemEffectName("시간 제한 스탯 보정")]
public sealed class TimedStatModifierEffectData : ItemEffectData
{
    [SerializeField, Min(0.01f), Tooltip("스탯 보정이 유지되는 시간(초)입니다. 시작할 때 적용하고 만료될 때 이 효과의 보정만 제거합니다.")]
    private float _duration = 20f;

    [SerializeField, Tooltip("기본 스탯과 별도로 합산할 보정입니다. FlatAdd는 고정값, PercentAdd는 비율입니다.")]
    private StatModifier[] _statModifiers = Array.Empty<StatModifier>();

    [SerializeField, Tooltip("동일 아이템의 동일 효과를 재사용할 때 지속시간을 갱신하거나 각 사용을 별도로 중첩합니다.")]
    private ItemEffectStackingPolicy _stackingPolicy = ItemEffectStackingPolicy.RefreshDuration;

    public float Duration => _duration;
    public IReadOnlyList<StatModifier> StatModifiers => _statModifiers ?? Array.Empty<StatModifier>();
    public ItemEffectStackingPolicy StackingPolicy => _stackingPolicy;

    public TimedStatModifierEffectData() { }

    public TimedStatModifierEffectData(float duration, StatModifier[] statModifiers, ItemEffectStackingPolicy stackingPolicy = ItemEffectStackingPolicy.RefreshDuration)
    {
        _duration = duration;
        _statModifiers = statModifiers ?? Array.Empty<StatModifier>();
        _stackingPolicy = stackingPolicy;
        Validate();
    }

    public override void Validate()
    {
        // 남은 시간과 적용 중인 보정은 이 공유 데이터에 저장하지 않는다.
        _duration = Mathf.Max(0.01f, _duration);
    }
}

[Serializable, ItemEffectName("스킬 포인트 지급")]
public sealed class AddSkillPointsEffectData : ItemEffectData
{
    [SerializeField, Min(0)] private int _amount = 1;

    public int Amount => _amount;

    public AddSkillPointsEffectData() { }

    public AddSkillPointsEffectData(int amount)
    {
        _amount = amount;
    }

    public override void Validate()
    {
        _amount = Mathf.Max(0, _amount);
    }
}

[Serializable, ItemEffectName("돈 지급")]
public sealed class AddMoneyEffectData : ItemEffectData
{
    [SerializeField, Min(0)] private int _amount;

    public int Amount => _amount;

    public AddMoneyEffectData() { }

    public AddMoneyEffectData(int amount)
    {
        _amount = amount;
    }

    public override void Validate()
    {
        _amount = Mathf.Max(0, _amount);
    }
}

[Serializable, ItemEffectName("조건부 스탯 보정")]
public sealed class ConditionalStatEffectData : PassiveItemEffectData
{
    // 모든 조건을 만족하는 동안만 보정하며, 빈 조건 목록은 항상 충족으로 취급한다.
    [SerializeField, Tooltip("모든 조건을 만족해야 합니다. 빈 목록은 항상 충족으로 취급합니다.")]
    private ItemEffectCondition[] _conditions = Array.Empty<ItemEffectCondition>();

    [SerializeField] private StatModifier[] _statModifiers = Array.Empty<StatModifier>();

    public IReadOnlyList<ItemEffectCondition> Conditions => _conditions ?? Array.Empty<ItemEffectCondition>();
    public IReadOnlyList<StatModifier> StatModifiers => _statModifiers ?? Array.Empty<StatModifier>();

    public ConditionalStatEffectData() { }

    public ConditionalStatEffectData(ItemEffectCondition[] conditions, StatModifier[] statModifiers)
    {
        _conditions = conditions ?? Array.Empty<ItemEffectCondition>();
        _statModifiers = statModifiers ?? Array.Empty<StatModifier>();
    }

    public override void Validate()
    {
        if (_conditions == null) return;
        for (int i = 0; i < _conditions.Length; i++) _conditions[i].Validate();
    }
}

[Serializable, ItemEffectName("이벤트 발동 효과")]
public sealed class TriggeredEffectData : PassiveItemEffectData
{
    [SerializeField] private ItemEffectTrigger _trigger;

    [SerializeField, Tooltip("이벤트 발생 시 모든 조건을 만족해야 합니다. 빈 목록은 조건 없이 발동합니다.")]
    private ItemEffectCondition[] _conditions = Array.Empty<ItemEffectCondition>();

    [SerializeField, Range(0f, 1f), Tooltip("조건을 충족한 이벤트의 발동 확률입니다. 1은 항상 발동합니다.")]
    private float _chance = 1f;

    [SerializeField, Min(0f), Tooltip("발동 후 다시 발동할 수 있을 때까지의 시간(초)입니다.")]
    private float _cooldown;

    // 발동 시 시작할 효과의 수치도 이 아이템 안에 저장하며, 별도 효과 SO를 참조하지 않는다.
    [SerializeReference] private ItemEffectData[] _effects = Array.Empty<ItemEffectData>();

    public ItemEffectTrigger Trigger => _trigger;
    public IReadOnlyList<ItemEffectCondition> Conditions => _conditions ?? Array.Empty<ItemEffectCondition>();
    public float Chance => _chance;
    public float Cooldown => _cooldown;
    public IReadOnlyList<ItemEffectData> Effects => _effects ?? Array.Empty<ItemEffectData>();

    public TriggeredEffectData() { }

    public TriggeredEffectData(ItemEffectTrigger trigger, ItemEffectCondition[] conditions
        , float chance, float cooldown, ItemEffectData[] effects)
    {
        _trigger = trigger;
        _conditions = conditions ?? Array.Empty<ItemEffectCondition>();
        _chance = chance;
        _cooldown = cooldown;
        _effects = effects ?? Array.Empty<ItemEffectData>();
    }

    public override void Validate()
    {
        _chance = Mathf.Clamp01(_chance);
        _cooldown = Mathf.Max(0f, _cooldown);
        if (_conditions != null)
        {
            for (int i = 0; i < _conditions.Length; i++) _conditions[i].Validate();
        }
        if (_effects != null)
        {
            for (int i = 0; i < _effects.Length; i++) _effects[i]?.Validate();
        }
    }
}
