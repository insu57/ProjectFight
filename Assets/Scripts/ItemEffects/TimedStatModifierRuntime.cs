using System.Collections.Generic;

// 공유 아이템 데이터와 분리하여 플레이어마다 적용 당시의 보정과 만료 시점을 보관한다.
public sealed class TimedStatModifierRuntime
{
    private const int ItemStatCount = (int)ItemStatType.EnergyRecoveryOnDamageTaken + 1;
    private const double TimeTolerance = 0.000001;

    private struct ActiveModifier
    {
        public ItemDefinition Item;
        public ItemEffectStackingPolicy StackingPolicy;
        public StatModifier Modifier;
        public double EndTime;
    }

    private readonly List<ActiveModifier> _activeModifiers = new(4);
    private readonly List<StatModifier> _statModifiers = new(4);
    private double _elapsedTime;

    public bool HasEffects => _activeModifiers.Count > 0;
    public IReadOnlyList<StatModifier> StatModifiers => _statModifiers;

    public static bool CanStart(TimedStatModifierEffectData effect)
    {
        if (effect == null || !IsFinitePositive(effect.Duration) || effect.Duration < 0.01f) return false;
        if (effect.StackingPolicy != ItemEffectStackingPolicy.RefreshDuration
            && effect.StackingPolicy != ItemEffectStackingPolicy.Independent) return false;

        IReadOnlyList<StatModifier> modifiers = effect.StatModifiers;
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (IsApplicableModifier(modifiers[i])) return true;
        }

        return false;
    }

    public bool TryStart(ItemDefinition item, int effectIndex, TimedStatModifierEffectData effect)
    {
        if (!item || effectIndex < 0 || !CanStart(effect)) return false;

        IReadOnlyList<StatModifier> modifiers = effect.StatModifiers;
        if (effect.StackingPolicy == ItemEffectStackingPolicy.RefreshDuration)
        {
            for (int i = _activeModifiers.Count - 1; i >= 0; i--)
            {
                ActiveModifier active = _activeModifiers[i];
                if (active.StackingPolicy != ItemEffectStackingPolicy.RefreshDuration
                    || !HasApplicableStat(modifiers, active.Modifier.Stat)) continue;

                // 새 효과와 겹치는 스탯의 기존 갱신형 보정만 제거하고 다른 스탯의 종료 시점은 유지한다.
                _activeModifiers.RemoveAt(i);
                _statModifiers.RemoveAt(i);
            }
        }

        for (int i = 0; i < modifiers.Count; i++)
        {
            StatModifier modifier = modifiers[i];
            if (!IsApplicableModifier(modifier)) continue;

            // 기존 묶음을 먼저 제거해 같은 스탯의 FlatAdd·PercentAdd가 새 묶음 안에서 서로 덮어쓰지 않게 한다.
            _activeModifiers.Add(new ActiveModifier { Item = item, StackingPolicy = effect.StackingPolicy,
                Modifier = modifier, EndTime = _elapsedTime + effect.Duration });
            _statModifiers.Add(modifier);
        }

        return true;
    }

    public bool Advance(float deltaTime)
    {
        if (!HasEffects) return false;
        // 게임 시간만 누적하므로 일시정지 중에는 지속시간도 멈춘다.
        if (IsFinitePositive(deltaTime)) _elapsedTime += deltaTime;

        bool changed = false;
        for (int i = _activeModifiers.Count - 1; i >= 0; i--)
        {
            ActiveModifier active = _activeModifiers[i];
            if (active.Item && active.EndTime > _elapsedTime + TimeTolerance) continue;

            // 같은 프레임에 끝난 보정을 모두 제거한 뒤 Manager가 한 번만 재계산한다.
            _activeModifiers.RemoveAt(i);
            _statModifiers.RemoveAt(i);
            changed = true;
        }

        if (!HasEffects) _elapsedTime = 0d;
        return changed;
    }

    public bool Clear()
    {
        bool changed = HasEffects;
        // 알림 전에 상태를 비우고 용량은 재사용하므로 해제 중 재진입해도 중복 제거하지 않는다.
        _activeModifiers.Clear();
        _statModifiers.Clear();
        _elapsedTime = 0d;
        return changed;
    }

    private static bool HasApplicableStat(IReadOnlyList<StatModifier> modifiers, ItemStatType stat)
    {
        for (int i = 0; i < modifiers.Count; i++)
        {
            StatModifier modifier = modifiers[i];
            if (modifier.Stat == stat && IsApplicableModifier(modifier)) return true;
        }

        return false;
    }

    private static bool IsApplicableModifier(StatModifier modifier)
    {
        int statIndex = (int)modifier.Stat;
        return statIndex >= 0 && statIndex < ItemStatCount && modifier.Value != 0f && !float.IsNaN(modifier.Value)
            && !float.IsInfinity(modifier.Value) && (modifier.Operation == StatModifierOperation.FlatAdd
            || modifier.Operation == StatModifierOperation.PercentAdd);
    }

    private static bool IsFinitePositive(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
