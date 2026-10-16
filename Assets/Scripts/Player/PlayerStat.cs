using System;
using System.Collections.Generic;
using UnityEngine;

// 생성 시 필드 이름으로 값을 채우는 내부 자료구조이며, 외부에는 읽기 전용 스냅샷만 전달한다.
internal struct PlayerStatSnapshotData
{
    public int Level;
    public int CurrentExp;
    public int TargetExp;
    public float MaxHealth;
    public float CurrentHealth;
    public float AttackPower;
    public float DefensePower;
    public float PoiseDamage;
    public float GuardPoiseDamage;
    public AttackDamageModifier AttackPowerModifier;
    public AttackDamageModifier PoiseDamageModifier;
    public AttackDamageModifier GuardPoiseDamageModifier;
    public float MaxEnergy;
    public float CurrentEnergy;

    public float MaxPoise;
    public float PoiseRecoveryDelay;
    public float PoiseRecoveryRate;
    public float StaggerDuration;
    public float MaxGuardPoise;
    public float GuardPoiseRecoveryDelay;
    public float GuardPoiseRecoveryRate;
    public float GuardBreakDuration;
    public float CurrentPoise;
    public float CurrentGuardPoise;
    public float StaggerMoveDistance;
    public float GuardBreakMoveDistance;
    public int Money;
    public int MaxDodgeCount;
    public float MoveSpeedMultiplier;
}

public readonly struct PlayerStatSnapshot
{
    // 생성용 구조체를 값으로 복사하므로 이후 원본이나 PlayerStat이 바뀌어도 이 스냅샷은 유지된다.
    private readonly PlayerStatSnapshotData _data;

    public int Level => _data.Level;
    public int CurrentExp => _data.CurrentExp;
    public int TargetExp => _data.TargetExp;
    public float MaxHealth => _data.MaxHealth;
    public float CurrentHealth => _data.CurrentHealth;
    public float AttackPower => _data.AttackPower;
    public float DefensePower => _data.DefensePower;
    public float PoiseDamage => _data.PoiseDamage;
    public float GuardPoiseDamage => _data.GuardPoiseDamage;
    // 표시용 최종 스탯과 별도로 공격 기본 피해까지 배율을 적용할 계산값을 제공한다.
    public AttackDamageModifier AttackPowerModifier => _data.AttackPowerModifier;
    public AttackDamageModifier PoiseDamageModifier => _data.PoiseDamageModifier;
    public AttackDamageModifier GuardPoiseDamageModifier => _data.GuardPoiseDamageModifier;
    public float MaxEnergy => _data.MaxEnergy;
    public float CurrentEnergy => _data.CurrentEnergy;

    public float MaxPoise => _data.MaxPoise;
    public float PoiseRecoveryDelay => _data.PoiseRecoveryDelay;
    public float PoiseRecoveryRate => _data.PoiseRecoveryRate;
    public float StaggerDuration => _data.StaggerDuration;
    public float MaxGuardPoise => _data.MaxGuardPoise;
    public float GuardPoiseRecoveryDelay => _data.GuardPoiseRecoveryDelay;
    public float GuardPoiseRecoveryRate => _data.GuardPoiseRecoveryRate;
    public float GuardBreakDuration => _data.GuardBreakDuration;
    public float CurrentPoise => _data.CurrentPoise;
    public float CurrentGuardPoise => _data.CurrentGuardPoise;
    public float StaggerMoveDistance => _data.StaggerMoveDistance;
    public float GuardBreakMoveDistance => _data.GuardBreakMoveDistance;
    public int Money => _data.Money;
    public int MaxDodgeCount => _data.MaxDodgeCount;
    public float MoveSpeedMultiplier => _data.MoveSpeedMultiplier;

    internal PlayerStatSnapshot(PlayerStatSnapshotData data)
    {
        // 기존 생성자의 값 제한 규칙은 복사본에 적용하여 그대로 유지한다.
        data.StaggerMoveDistance = Mathf.Max(0f, data.StaggerMoveDistance);
        data.GuardBreakMoveDistance = Mathf.Max(0f, data.GuardBreakMoveDistance);
        data.MaxDodgeCount = Mathf.Max(1, data.MaxDodgeCount);
        data.MoveSpeedMultiplier = Mathf.Max(0f, data.MoveSpeedMultiplier);
        _data = data;
    }
}

[DisallowMultipleComponent]
public class PlayerStat : MonoBehaviour
{
    // 스탯 목록은 형식 초기화 때 한 번만 준비하고 계산 시에는 배열을 순회한다.
    private static readonly ItemStatType[] StatTypes = (ItemStatType[])Enum.GetValues(typeof(ItemStatType));
    private static readonly int ItemStatCount = (int)StatTypes[StatTypes.Length - 1] + 1;

    private struct StatModifierTotals
    {
        public float FlatAdd;
        public float PercentAdd;
    }

    // CSV에서 변환한 성장표이며 플레이어의 현재 상태는 이 컴포넌트에 보관한다.
    [SerializeField] private PlayerProgressionDefinition _progressionDefinition;
    [SerializeField, Min(1)] private int _level = 1;
    [SerializeField, Min(0)] private int _currentExp;
    [SerializeField, Min(0)] private int _targetExp = 100;
    // 보유 금액은 성장표와 별도로 유지하며 처치 보상으로 누적한다.
    [SerializeField, Min(0)] private int _money;
    // 기존 직렬화 필드명은 유지하며 최대치와 회복 설정 등은 아이템 보정 전 기본값으로 보관한다.
    // 체력은 소수점 피해와 회복량을 그대로 보존한다.
    [SerializeField, Min(1f)] private float _maxHealth = 100f;
    [SerializeField, Min(0f)] private float _currentHealth = 100f;

    [SerializeField, Min(0f)] private float _baseAttackPower;
    [SerializeField, Min(0f)] private float _baseDefensePower;
    // 각 공격의 기본 강인도 피해에 더할 기본값이며 PercentAdd는 공격 기본 피해까지 합산한 뒤 적용한다.
    [SerializeField, Min(0f)] private float _basePoiseDamage;
    [SerializeField, Min(0f)] private float _baseGuardPoiseDamage;
    [SerializeField, Min(0f)] private float _baseMaxEnergy = 100f;
    [SerializeField, Min(0f)] private float _currentEnergy;

    // Inspector의 PlayerDodge 설정을 Manager가 최초에 전달하며, 이후 변경도 기본값으로 관리한다.
    private int _baseMaxDodgeCount = 1;

    // 일반 강인도의 최대치, 회복 설정과 소진 시 경직 시간이다.
    [SerializeField, Min(0f)] private float _maxPoise = 100f;
    [SerializeField, Min(0f)] private float _poiseRecoveryDelay = 3f;
    [SerializeField, Min(0f)] private float _poiseRecoveryRate = 20f;
    [SerializeField, Min(0f)] private float _staggerDuration = 0.5f;
    // 경직 시간 동안 밀려나는 총 거리이다.
    [SerializeField, Min(0f)] private float _staggerMoveDistance = 0.5f;

    // 가드 강인도는 별도의 회복 설정과 가드 브레이크 시간을 사용한다.
    [SerializeField, Min(0f)] private float _maxGuardPoise = 100f;
    [SerializeField, Min(0f)] private float _guardPoiseRecoveryDelay = 3f;
    [SerializeField, Min(0f)] private float _guardPoiseRecoveryRate = 20f;
    [SerializeField, Min(0f)] private float _guardBreakDuration = 1.5f;
    // 가드 브레이크 시간 동안 밀려나는 총 거리이다.
    [SerializeField, Min(0f)] private float _guardBreakMoveDistance = 1f;

    public event Action<PlayerStatSnapshot> StatsChanged;

    public float CurrentPoise { get; private set; }
    public float CurrentGuardPoise { get; private set; }
    public int Money => _money;

    // 두 강인도는 마지막으로 피해를 받은 시점부터 각각 회복을 기다린다.
    private float _poiseRecoveryRemaining;
    private float _guardPoiseRecoveryRemaining;

    // 보정 합계와 최종값 배열은 한 번 생성해 재사용하고, 조회할 때는 계산된 값만 읽는다.
    private readonly StatModifierTotals[] _relicStatModifiers = new StatModifierTotals[ItemStatCount];
    private readonly StatModifierTotals[] _equipmentStatModifiers = new StatModifierTotals[ItemStatCount];
    private readonly StatModifierTotals[] _timedStatModifiers = new StatModifierTotals[ItemStatCount];
    private readonly StatModifierTotals[] _modifierTotalsBuffer = new StatModifierTotals[ItemStatCount];
    [SerializeField] private float[] _finalStats = new float[ItemStatCount];
    private AttackDamageModifier _attackPowerModifier;
    private AttackDamageModifier _poiseDamageModifier;
    private AttackDamageModifier _guardPoiseDamageModifier;
    private bool _hasCalculatedStats;

    // 저장된 계산 배열이 이전 스탯 개수여도 최초 조회에서 다시 준비한다.
    private bool HasCalculatedStats => _hasCalculatedStats && _finalStats != null && _finalStats.Length == ItemStatCount;

    public PlayerStatSnapshot CurrentStats => CreateSnapshot();

    private PlayerStatSnapshot CreateSnapshot()
    {
        // 최초 조회에서는 현재량 제한까지 끝낸 뒤 모든 필드를 한 시점의 값으로 복사한다.
        if (!HasCalculatedStats) RecalculateStats();

        PlayerStatSnapshotData data = new()
        {
            Level = _level,
            CurrentExp = _currentExp,
            TargetExp = _targetExp,
            MaxHealth = GetFinalStat(ItemStatType.MaxHealth),
            CurrentHealth = _currentHealth,
            AttackPower = GetFinalStat(ItemStatType.AttackPower),
            DefensePower = GetFinalStat(ItemStatType.DefensePower),
            PoiseDamage = GetFinalStat(ItemStatType.PoiseDamage),
            GuardPoiseDamage = GetFinalStat(ItemStatType.GuardPoiseDamage),
            AttackPowerModifier = _attackPowerModifier,
            PoiseDamageModifier = _poiseDamageModifier,
            GuardPoiseDamageModifier = _guardPoiseDamageModifier,
            MaxEnergy = GetFinalStat(ItemStatType.MaxEnergy),
            CurrentEnergy = _currentEnergy,
            MaxPoise = GetFinalStat(ItemStatType.MaxPoise),
            PoiseRecoveryDelay = GetFinalStat(ItemStatType.PoiseRecoveryDelay),
            PoiseRecoveryRate = GetFinalStat(ItemStatType.PoiseRecoveryRate),
            StaggerDuration = GetFinalStat(ItemStatType.StaggerDuration),
            MaxGuardPoise = GetFinalStat(ItemStatType.MaxGuardPoise),
            GuardPoiseRecoveryDelay = GetFinalStat(ItemStatType.GuardPoiseRecoveryDelay),
            GuardPoiseRecoveryRate = GetFinalStat(ItemStatType.GuardPoiseRecoveryRate),
            GuardBreakDuration = GetFinalStat(ItemStatType.GuardBreakDuration),
            CurrentPoise = this.CurrentPoise,
            CurrentGuardPoise = this.CurrentGuardPoise,
            StaggerMoveDistance = GetFinalStat(ItemStatType.StaggerMoveDistance),
            GuardBreakMoveDistance = GetFinalStat(ItemStatType.GuardBreakMoveDistance),
            Money = _money,
            MaxDodgeCount = GetFinalMaxDodgeCount(),
            MoveSpeedMultiplier = GetFinalStat(ItemStatType.MoveSpeedMultiplier)
        };

        return new PlayerStatSnapshot(data);
    }

    private void Awake()
    {
        if (TryGetProgressionLevel(_level, out PlayerLevelData levelData))
        {
            ApplyLevelData(levelData, false);
            _currentExp = _targetExp > 0 ? Mathf.Clamp(_currentExp, 0, _targetExp - 1) : 0;
        }

        RecalculateStats();
        _currentHealth = GetFinalStat(ItemStatType.MaxHealth);
        _currentEnergy = 0f;
        _money = Mathf.Max(0, _money);

        CurrentPoise = GetFinalStat(ItemStatType.MaxPoise);
        CurrentGuardPoise = GetFinalStat(ItemStatType.MaxGuardPoise);
        _poiseRecoveryRemaining = 0f;
        _guardPoiseRecoveryRemaining = 0f;
    }

    public void RebuildRelicStatModifiers(IReadOnlyList<ItemDefinition> relicItems)
    {
        if (RebuildStatModifierTotals(relicItems, ItemType.Relic, _relicStatModifiers) && RecalculateStats()) NotifyStatsChanged();
    }

    public void SetBaseMaxDodgeCount(int maxDodgeCount)
    {
        int clampedMaxDodgeCount = Mathf.Max(1, maxDodgeCount);
        if (_baseMaxDodgeCount == clampedMaxDodgeCount) return;

        _baseMaxDodgeCount = clampedMaxDodgeCount;
        if (RecalculateStats()) NotifyStatsChanged();
    }

    public void RebuildItemStatModifiers(IReadOnlyList<ItemDefinition> relicItems, IReadOnlyList<ItemDefinition> equippedItems)
    {
        // 두 출처의 합계를 모두 준비한 뒤 한 번 계산하여 중간 최대치로 현재량이 제한되지 않게 한다.
        bool modifiersChanged = RebuildStatModifierTotals(relicItems, ItemType.Relic, _relicStatModifiers);
        modifiersChanged |= RebuildStatModifierTotals(equippedItems, ItemType.Equipment, _equipmentStatModifiers);
        if (modifiersChanged && RecalculateStats())
        {
            NotifyStatsChanged();
        }
    }

    public void RebuildTimedStatModifiers(IReadOnlyList<StatModifier> modifiers)
    {
        if (!HasCalculatedStats)
        {
            RecalculateStats();
        }

        // 버퍼에서 새 합계를 완성한 뒤 비교하므로 시간만 갱신한 버프는 재계산하지 않는다.
        Array.Clear(_modifierTotalsBuffer, 0, _modifierTotalsBuffer.Length);
        if (modifiers != null)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                AccumulateStatModifier(modifiers[i], _modifierTotalsBuffer);
            }
        }

        if (CommitModifierTotals(_timedStatModifiers) && RecalculateStats())
        {
            NotifyStatsChanged();
        }
    }

    private bool RebuildStatModifierTotals(IReadOnlyList<ItemDefinition> items, ItemType itemType, StatModifierTotals[] totals)
    {
        // Awake보다 먼저 요청되어도 기존 기본값을 기준으로 변경 여부를 비교한다.
        if (!HasCalculatedStats) RecalculateStats();

        // 유물은 보유 목록, 장비는 Manager가 선별한 장착 목록에서 다시 집계한다.
        Array.Clear(_modifierTotalsBuffer, 0, _modifierTotalsBuffer.Length);
        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                ItemDefinition item = items[i];
                if (!item || item.ItemType != itemType) continue;

                IReadOnlyList<StatModifier> modifiers = item.StatModifiers;
                for (int j = 0; j < modifiers.Count; j++)
                {
                    AccumulateStatModifier(modifiers[j], _modifierTotalsBuffer);
                }
            }
        }

        return CommitModifierTotals(totals);
    }

    private bool CommitModifierTotals(StatModifierTotals[] totals)
    {
        bool changed = false;
        for (int i = 0; i < totals.Length; i++)
        {
            StatModifierTotals next = _modifierTotalsBuffer[i];
            if (totals[i].FlatAdd == next.FlatAdd && totals[i].PercentAdd == next.PercentAdd) continue;

            totals[i] = next;
            changed = true;
        }

        return changed;
    }

    private static void AccumulateStatModifier(StatModifier modifier, StatModifierTotals[] totals)
    {
        int statIndex = (int)modifier.Stat;
        if (statIndex < 0 || statIndex >= ItemStatCount || float.IsNaN(modifier.Value) || float.IsInfinity(modifier.Value)) return;

        if (modifier.Operation == StatModifierOperation.FlatAdd) totals[statIndex].FlatAdd += modifier.Value;
        else if (modifier.Operation == StatModifierOperation.PercentAdd) totals[statIndex].PercentAdd += modifier.Value;
    }

    private float GetFinalStat(ItemStatType stat)
    {
        // 컴포넌트의 Awake 순서와 관계없이 최초 조회에서도 유효한 스탯을 반환한다.
        if (!HasCalculatedStats) RecalculateStats();
        return _finalStats[(int)stat];
    }

    private int GetFinalMaxDodgeCount()
    {
        float maxDodgeCount = GetFinalStat(ItemStatType.MaxDodgeCount);
        // float의 정수 상한 반올림이나 큰 보정이 int 변환에서 음수가 되지 않게 제한한다.
        return maxDodgeCount >= int.MaxValue ? int.MaxValue : (int)maxDodgeCount;
    }

    private bool RecalculateStats()
    {
        bool changed = RecalculateFinalStats();
        _hasCalculatedStats = true;
        // 모든 최대치를 확정한 뒤 현재량을 한 번만 제한해 중간 값으로 현재량이 줄어들지 않게 한다.
        changed |= ClampCurrentResources();
        return changed;
    }

    private bool RecalculateFinalStats()
    {
        // Inspector에 저장된 캐시는 파생 값이므로 스탯 종류가 늘었을 때 한 번만 재생성한다.
        if (_finalStats == null || _finalStats.Length != ItemStatCount) _finalStats = new float[ItemStatCount];

        bool changed = false;
        for (int i = 0; i < StatTypes.Length; i++)
        {
            ItemStatType stat = StatTypes[i];
            float baseValue = GetBaseStatValue(stat);
            // 피해 스탯은 표시값이 같아도 공격 고유 피해에 적용할 배율 변경을 따로 감지한다.
            switch (stat)
            {
                case ItemStatType.AttackPower:
                    changed |= SetFinalDamageStat(stat, baseValue, ref _attackPowerModifier);
                    break;
                case ItemStatType.PoiseDamage:
                    changed |= SetFinalDamageStat(stat, baseValue, ref _poiseDamageModifier);
                    break;
                case ItemStatType.GuardPoiseDamage:
                    changed |= SetFinalDamageStat(stat, baseValue, ref _guardPoiseDamageModifier);
                    break;
                default:
                    changed |= SetFinalStat(stat, baseValue);
                    break;
            }
        }

        return changed;
    }

    private float GetBaseStatValue(ItemStatType stat)
    {
        return stat switch
        {
            ItemStatType.AttackPower => _baseAttackPower,
            ItemStatType.DefensePower => _baseDefensePower,
            ItemStatType.MaxHealth => _maxHealth,
            ItemStatType.MaxEnergy => _baseMaxEnergy,
            ItemStatType.MaxPoise => _maxPoise,
            ItemStatType.PoiseRecoveryDelay => _poiseRecoveryDelay,
            ItemStatType.PoiseRecoveryRate => _poiseRecoveryRate,
            ItemStatType.StaggerDuration => _staggerDuration,
            ItemStatType.MaxGuardPoise => _maxGuardPoise,
            ItemStatType.GuardPoiseRecoveryDelay => _guardPoiseRecoveryDelay,
            ItemStatType.GuardPoiseRecoveryRate => _guardPoiseRecoveryRate,
            ItemStatType.GuardBreakDuration => _guardBreakDuration,
            ItemStatType.StaggerMoveDistance => _staggerMoveDistance,
            ItemStatType.GuardBreakMoveDistance => _guardBreakMoveDistance,
            ItemStatType.MaxDodgeCount => _baseMaxDodgeCount,
            ItemStatType.MoveSpeedMultiplier => 1f,
            ItemStatType.PoiseDamage => _basePoiseDamage,
            ItemStatType.GuardPoiseDamage => _baseGuardPoiseDamage,
            // 새 스탯의 기본값 설정을 빠뜨리면 조용히 0으로 처리하지 않고 알린다.
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "스탯 기본값을 지정해야 합니다.")
        };
    }

    private bool ClampCurrentResources()
    {
        // 보유·장착·시간제 효과는 현재량을 회복시키지 않는다. 최대치가 줄어든 경우에만 범위 안으로 제한한다.
        float nextHealth = Mathf.Clamp(_currentHealth, 0f, _finalStats[(int)ItemStatType.MaxHealth]);
        float nextEnergy = Mathf.Clamp(_currentEnergy, 0f, _finalStats[(int)ItemStatType.MaxEnergy]);
        float nextPoise = Mathf.Clamp(CurrentPoise, 0f, _finalStats[(int)ItemStatType.MaxPoise]);
        float nextGuardPoise = Mathf.Clamp(CurrentGuardPoise, 0f, _finalStats[(int)ItemStatType.MaxGuardPoise]);
        bool changed = _currentHealth != nextHealth || _currentEnergy != nextEnergy
            || CurrentPoise != nextPoise || CurrentGuardPoise != nextGuardPoise;
        _currentHealth = nextHealth;
        _currentEnergy = nextEnergy;
        CurrentPoise = nextPoise;
        CurrentGuardPoise = nextGuardPoise;
        return changed;
    }

    private void GetStatCalculation(ItemStatType stat, float baseValue, out float valueBeforeMultiplier, out float multiplier)
    {
        int statIndex = (int)stat;
        StatModifierTotals relicModifiers = _relicStatModifiers[statIndex];
        StatModifierTotals equipmentModifiers = _equipmentStatModifiers[statIndex];
        StatModifierTotals timedModifiers = _timedStatModifiers[statIndex];
        float flatAdd = relicModifiers.FlatAdd + equipmentModifiers.FlatAdd + timedModifiers.FlatAdd;
        float percentAdd = relicModifiers.PercentAdd + equipmentModifiers.PercentAdd + timedModifiers.PercentAdd;
        // 비율은 0.2를 20%로 더하며, -100% 아래에서도 배율이 음수로 뒤집히지 않게 한다.
        multiplier = Mathf.Max(0f, 1f + percentAdd);
        valueBeforeMultiplier = baseValue + flatAdd;
    }

    private bool SetFinalDamageStat(ItemStatType stat, float baseValue, ref AttackDamageModifier damageModifier)
    {
        GetStatCalculation(stat, baseValue, out float valueBeforeMultiplier, out float multiplier);
        // 추가 스탯이 0이어도 배율은 공격 기본 피해를 바꾸므로 계산값의 변경도 알린다.
        bool changed = damageModifier.AdditiveValue != valueBeforeMultiplier || damageModifier.Multiplier != multiplier;
        damageModifier = new AttackDamageModifier(valueBeforeMultiplier, multiplier);
        float finalValue = Mathf.Max(0f, valueBeforeMultiplier * multiplier);
        int statIndex = (int)stat;
        changed |= _finalStats[statIndex] != finalValue;
        _finalStats[statIndex] = finalValue;
        return changed;
    }

    private bool SetFinalStat(ItemStatType stat, float baseValue)
    {
        GetStatCalculation(stat, baseValue, out float valueBeforeMultiplier, out float multiplier);
        float minimumValue = stat == ItemStatType.MaxHealth || stat == ItemStatType.MaxDodgeCount ? 1f : 0f;
        float finalValue = Mathf.Max(minimumValue, valueBeforeMultiplier * multiplier);
        // 회피 횟수는 계산을 모두 마친 뒤 소수점을 내리며 최소 1회를 유지한다.
        if (stat == ItemStatType.MaxDodgeCount) finalValue = Mathf.Floor(finalValue);
        int statIndex = (int)stat;
        if (_finalStats[statIndex] == finalValue) return false;

        _finalStats[statIndex] = finalValue;
        return true;
    }

    public void TakePoiseDamage(float amount)
    {
        if (amount <= 0f || _currentHealth <= 0f) return;

        // 이미 0이어도 추가 강인도 피해를 받으면 회복 대기를 다시 시작한다.
        _poiseRecoveryRemaining = GetFinalStat(ItemStatType.PoiseRecoveryDelay);

        float nextPoise = Mathf.Max(0f, CurrentPoise - amount);

        if (nextPoise == CurrentPoise) return;

        CurrentPoise = nextPoise;
        NotifyStatsChanged();
    }

    public void TakeGuardPoiseDamage(float amount)
    {
        if (amount <= 0f || _currentHealth <= 0f) return;

        _guardPoiseRecoveryRemaining = GetFinalStat(ItemStatType.GuardPoiseRecoveryDelay);

        float nextGuardPoise = Mathf.Max(0f, CurrentGuardPoise - amount);

        if (nextGuardPoise == CurrentGuardPoise) return;

        CurrentGuardPoise = nextGuardPoise;
        NotifyStatsChanged();
    }

    public void RestorePoiseAfterReaction(bool isGuardBreak)
    {
        if (_currentHealth <= 0f) return;

        // 종료된 반응에 해당하는 강인도와 회복 대기만 초기화한다.
        if (isGuardBreak)
        {
            CurrentGuardPoise = GetFinalStat(ItemStatType.MaxGuardPoise);
            _guardPoiseRecoveryRemaining = 0f;
        }
        else
        {
            CurrentPoise = GetFinalStat(ItemStatType.MaxPoise);
            _poiseRecoveryRemaining = 0f;
        }

        NotifyStatsChanged();
    }

    public void UpdatePoiseRecovery(float deltaTime)
    {
        if (deltaTime <= 0f || _currentHealth <= 0f) return;

        float nextPoise = RecoverPoise(CurrentPoise, GetFinalStat(ItemStatType.MaxPoise),
            GetFinalStat(ItemStatType.PoiseRecoveryRate), ref _poiseRecoveryRemaining, deltaTime);
        float nextGuardPoise = RecoverPoise(CurrentGuardPoise, GetFinalStat(ItemStatType.MaxGuardPoise),
            GetFinalStat(ItemStatType.GuardPoiseRecoveryRate), ref _guardPoiseRecoveryRemaining, deltaTime);

        if (nextPoise == CurrentPoise && nextGuardPoise == CurrentGuardPoise) return;

        CurrentPoise = nextPoise;
        CurrentGuardPoise = nextGuardPoise;

        // 두 수치가 같은 프레임에 회복되어도 변경 이벤트는 한 번만 보낸다.
        NotifyStatsChanged();
    }

    private static float RecoverPoise(float current, float maximum, float recoveryRate, ref float recoveryRemaining, float deltaTime)
    {
        // 이번 프레임 중 대기 시간에 사용한 부분을 회복 시간에서 제외한다.
        float waitTime = Mathf.Min(recoveryRemaining, deltaTime);
        recoveryRemaining -= waitTime;

        float recoveryTime = deltaTime - waitTime;
        float recoveryAmount = Mathf.Max(0f, recoveryRate) * recoveryTime;

        return Mathf.Clamp(current + recoveryAmount, 0f, Mathf.Max(0f, maximum));
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0 || _money == int.MaxValue) return;

        // 큰 보상에서도 음수로 넘치지 않게 int 상한까지만 누적한다.
        _money = (int)Math.Min(int.MaxValue, (long)_money + amount);
        NotifyStatsChanged();
    }

    public bool TrySpendMoney(int amount)
    {
        if (amount < 0 || _money < amount) return false;
        if (amount == 0) return true;

        // 잔액 검사와 차감을 한 경로에서 처리하고 실제 금액이 변할 때만 UI에 알린다.
        _money -= amount;
        NotifyStatsChanged();
        return true;
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (TryGetProgressionLevel(_level, out PlayerLevelData currentLevel))
        {
            AddProgressionExperience(amount, currentLevel);
            return;
        }

        _targetExp = Mathf.Max(1, _targetExp);

        // 성장표가 연결되지 않은 기존 플레이어는 고정 요구 경험치 동작을 유지한다.
        // 합산은 long으로 처리하여 큰 보상에서도 int 경험치가 음수로 넘치지 않게 한다.
        long totalExperience = (long)_currentExp + amount;
        long gainedLevels = totalExperience / _targetExp;
        _level = (int)Math.Min(int.MaxValue, (long)Mathf.Max(1, _level) + gainedLevels);
        _currentExp = (int)(totalExperience % _targetExp);
        NotifyStatsChanged();
    }

    private void AddProgressionExperience(int amount, PlayerLevelData currentLevel)
    {
        if (currentLevel.Level >= _progressionDefinition.MaxLevel || currentLevel.TargetExp <= 0) return;

        // 각 레벨의 요구 경험치가 다르므로 차례대로 차감하고 다음 레벨의 스탯을 적용한다.
        long totalExperience = (long)Mathf.Max(0, _currentExp) + amount;
        _targetExp = currentLevel.TargetExp;

        while (_targetExp > 0 && totalExperience >= _targetExp && _level < _progressionDefinition.MaxLevel)
        {
            if (!_progressionDefinition.TryGetLevel(_level + 1, out PlayerLevelData nextLevel)) break;

            totalExperience -= _targetExp;
            ApplyLevelData(nextLevel, true);
        }

        // 최대 레벨에서는 경험치를 남기지 않고 더 이상 레벨업하지 않는다.
        _currentExp = _level >= _progressionDefinition.MaxLevel ? 0 : (int)Math.Min(totalExperience, int.MaxValue);
        // 여러 레벨을 한 번에 올려도 완성된 최종 스냅샷만 한 번 전달한다.
        NotifyStatsChanged();
    }

    private bool TryGetProgressionLevel(int level, out PlayerLevelData data)
    {
        if (!_progressionDefinition || _progressionDefinition.MaxLevel <= 0)
        {
            data = default;
            return false;
        }

        int clampedLevel = Mathf.Clamp(level, 1, _progressionDefinition.MaxLevel);
        return _progressionDefinition.TryGetLevel(clampedLevel, out data);
    }

    private void ApplyLevelData(PlayerLevelData data, bool restoreHealthIncrease)
    {
        float previousMaxHealth = GetFinalStat(ItemStatType.MaxHealth);
        _level = data.Level;
        _targetExp = data.TargetExp;
        _maxHealth = data.MaxHealth;
        _baseAttackPower = data.AttackPower;
        _baseDefensePower = data.DefensePower;
        _baseMaxEnergy = data.MaxEnergy;
        RecalculateStats();

        // 아이템 보정을 유지한 최종 최대 체력의 레벨업 증가분만 보충하고, 사망자는 되살리지 않는다.
        float maxHealth = GetFinalStat(ItemStatType.MaxHealth);
        float healthIncrease = Mathf.Max(0f, maxHealth - previousMaxHealth);
        if (restoreHealthIncrease && _currentHealth > 0f) _currentHealth = Mathf.Min(maxHealth, _currentHealth + healthIncrease);
    }

    public void SetLevel(int level, int targetExp)
    {
        int clampedLevel = Mathf.Max(1, level);
        int clampedTargetExp = Mathf.Max(1, targetExp);
        bool hasProgressionData = TryGetProgressionLevel(level, out PlayerLevelData levelData);

        // 레벨 직접 지정 시에도 연결된 성장표의 레벨 범위와 요구 경험치를 사용한다.
        if (hasProgressionData)
        {
            clampedLevel = levelData.Level;
            clampedTargetExp = levelData.TargetExp;
        }

        if (_level == clampedLevel && _targetExp == clampedTargetExp)
        {
            return;
        }

        if (hasProgressionData) ApplyLevelData(levelData, false);
        else
        {
            _level = clampedLevel;
            _targetExp = clampedTargetExp;
        }

        _currentExp = hasProgressionData ? (_targetExp > 0 ?
            Mathf.Clamp(_currentExp, 0, _targetExp - 1) : 0) : Mathf.Min(_currentExp, _targetExp);
        NotifyStatsChanged();
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || _currentHealth <= 0f)
        {
            return;
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        NotifyStatsChanged();
    }

    public void RestoreHealth(float amount)
    {
        float maxHealth = GetFinalStat(ItemStatType.MaxHealth);
        if (amount <= 0f || _currentHealth >= maxHealth)
        {
            return;
        }

        _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
        NotifyStatsChanged();
    }

    public void SetMaxHealth(float maxHealth)
    {
        float clampedMaxHealth = Mathf.Max(1f, maxHealth);

        if (_maxHealth == clampedMaxHealth)
        {
            return;
        }

        _maxHealth = clampedMaxHealth;
        if (RecalculateStats()) NotifyStatsChanged();
    }

    public bool TryConsumeEnergy(float amount)
    {
        if (amount <= 0f || _currentEnergy < amount)
        {
            return false;
        }

        _currentEnergy -= amount;
        NotifyStatsChanged();
        return true;
    }

    public void RestoreEnergy(float amount)
    {
        float maxEnergy = GetFinalStat(ItemStatType.MaxEnergy);
        if (amount <= 0f || _currentEnergy >= maxEnergy)
        {
            return;
        }

        _currentEnergy = Mathf.Min(maxEnergy, _currentEnergy + amount);
        NotifyStatsChanged();
    }

    public void SetBaseStats(float attackPower, float defensePower, float maxEnergy)
    {
        float clampedAttackPower = Mathf.Max(0f, attackPower);
        float clampedDefensePower = Mathf.Max(0f, defensePower);
        float clampedMaxEnergy = Mathf.Max(0f, maxEnergy);

        if (Mathf.Approximately(_baseAttackPower, clampedAttackPower) && Mathf.Approximately(_baseDefensePower, clampedDefensePower)
            && Mathf.Approximately(_baseMaxEnergy, clampedMaxEnergy))
        {
            return;
        }

        _baseAttackPower = clampedAttackPower;
        _baseDefensePower = clampedDefensePower;
        _baseMaxEnergy = clampedMaxEnergy;
        if (RecalculateStats()) NotifyStatsChanged();
    }

    private void NotifyStatsChanged()
    {
        // 외부에는 변경 가능한 PlayerStat 대신 현재 값의 스냅샷만 전달한다.
        StatsChanged?.Invoke(CurrentStats);
    }
}
