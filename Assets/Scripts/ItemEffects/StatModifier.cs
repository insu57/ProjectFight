using System;
using UnityEngine;

// 현재 체력과 에너지는 회복 효과로 변경하고, 이 목록은 계산에 사용하는 스탯만 구분한다.
public enum ItemStatType
{
    AttackPower = 0,
    DefensePower = 1,
    MaxHealth = 2,
    MaxEnergy = 3,
    MaxPoise = 4,
    PoiseRecoveryDelay = 5,
    PoiseRecoveryRate = 6,
    StaggerDuration = 7,
    MaxGuardPoise = 8,
    GuardPoiseRecoveryDelay = 9,
    GuardPoiseRecoveryRate = 10,
    GuardBreakDuration = 11,
    StaggerMoveDistance = 12,
    GuardBreakMoveDistance = 13
}

public enum StatModifierOperation
{
    FlatAdd = 0,
    PercentAdd = 1
}

[Serializable]
public struct StatModifier
{
    [SerializeField] private ItemStatType _stat;
    [SerializeField] private StatModifierOperation _operation;
    [SerializeField, Tooltip("FlatAdd는 고정값, PercentAdd는 비율입니다. 0.2는 20% 증가이며 음수는 감소입니다.")]
    private float _value;

    public ItemStatType Stat => _stat;
    public StatModifierOperation Operation => _operation;
    public float Value => _value;

    // 최종값은 (레벨 기본값 + FlatAdd 합계) * (1 + PercentAdd 합계)로 계산한다.
    public StatModifier(ItemStatType stat, StatModifierOperation operation, float value)
    {
        _stat = stat;
        _operation = operation;
        _value = value;
    }
}
