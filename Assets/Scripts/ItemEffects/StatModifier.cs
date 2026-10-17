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
    GuardBreakMoveDistance = 13,
    // 기존 아이템 SO의 스탯 번호를 유지하고 새 항목은 뒤에 추가한다.
    MaxDodgeCount = 14,
    // 일반·락온 이동과 달리기의 각 기본 속도에 공통으로 곱할 배율이다. 기본값은 1이다.
    MoveSpeedMultiplier = 15,
    // FlatAdd는 공격 고유의 강인도 피해에 더하고, PercentAdd는 합산한 전체 피해에 적용한다. 기본값은 0이다.
    PoiseDamage = 16,
    GuardPoiseDamage = 17,
    // 공격별 기본 에너지 회복량에 더하며, 비율 보정은 기본 회복량까지 포함한 합계에 적용한다.
    EnergyRecoveryOnHit = 18,
    // 가드와 일반 피격의 회복량은 별도 스탯으로 관리하여 각각 보정한다.
    EnergyRecoveryOnGuard = 19,
    EnergyRecoveryOnDamageTaken = 20
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

    // 표시용 최종값은 (기본값 + FlatAdd 합계) * (1 + PercentAdd 합계)로 계산한다.
    // 공격력·일반/가드 강인도 피해는 실제 타격 시 AttackDefinition의 기본 피해도 더한 뒤 배율을 적용한다.
    public StatModifier(ItemStatType stat, StatModifierOperation operation, float value)
    {
        _stat = stat;
        _operation = operation;
        _value = value;
    }
}
