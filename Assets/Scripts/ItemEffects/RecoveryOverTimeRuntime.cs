using System;
using System.Collections.Generic;

// 공유 효과 데이터와 분리하여 각 플레이어의 회복 시점과 종료 시점을 보관한다.
public sealed class RecoveryOverTimeRuntime
{
    // float 설정값과 프레임 시간의 누적 오차로 종료 시점의 회복이 누락되지 않도록 한다.
    private const double TimeTolerance = 0.000001;

    private struct ActiveRecovery
    {
        public ItemDefinition Item;
        public ItemEffectStackingPolicy StackingPolicy;
        public ItemRecoveryTarget Target;
        public ItemRecoveryAmount Amount;
        public double Interval;
        public double NextRecoveryTime;
        public double EndTime;
    }

    private readonly List<ActiveRecovery> _effects = new(4);
    private double _elapsedTime;

    public bool HasEffects => _effects.Count > 0;

    public void AppendActiveEffects(List<ActiveItemEffectSnapshot> results)
    {
        // HUD가 보유한 목록에 값 형식으로 추가하여 조회할 때마다 배열을 만들지 않는다.
        for (int i = 0; i < _effects.Count; i++)
        {
            ActiveRecovery active = _effects[i];
            double remainingTime = active.EndTime - _elapsedTime;
            if (!active.Item || remainingTime <= TimeTolerance) continue;

            results.Add(new ActiveItemEffectSnapshot(active.Item, active.Target, (float)remainingTime));
        }
    }

    public static bool CanStart(RecoveryOverTimeEffectData effect)
    {
        if (effect == null || !IsFinitePositive(effect.Duration) || !IsFinitePositive(effect.Interval)
            || effect.Interval > effect.Duration) return false;
        if (effect.Duration < 0.01f || effect.Interval < 0.01f || !IsFinitePositive(effect.Amount.Value)) return false;

        return (effect.Target == ItemRecoveryTarget.Health || effect.Target == ItemRecoveryTarget.Energy)
            && (effect.Amount.Mode == ItemRecoveryMode.Fixed || effect.Amount.Mode == ItemRecoveryMode.MaxRatio)
            && (effect.StackingPolicy == ItemEffectStackingPolicy.RefreshDuration
            || effect.StackingPolicy == ItemEffectStackingPolicy.Independent);
    }

    public bool TryStart(ItemDefinition item, int effectIndex, RecoveryOverTimeEffectData effect)
    {
        if (!item || effectIndex < 0 || !CanStart(effect)) return false;

        // 갱신 시에도 회복 방식·간격·첫 회복 시점을 모두 새 아이템의 설정으로 교체한다.
        ActiveRecovery next = new()
        {
            Item = item,
            StackingPolicy = effect.StackingPolicy,
            Target = effect.Target,
            Amount = effect.Amount,
            Interval = effect.Interval,
            NextRecoveryTime = _elapsedTime + effect.Interval,
            EndTime = _elapsedTime + effect.Duration
        };

        if (effect.StackingPolicy == ItemEffectStackingPolicy.RefreshDuration)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                ActiveRecovery active = _effects[i];
                if (active.StackingPolicy != ItemEffectStackingPolicy.RefreshDuration || active.Target != effect.Target) continue;

                // 체력·에너지별 갱신형 효과만 교체하고 개별 중첩 효과는 별도로 유지한다.
                _effects[i] = next;
                return true;
            }
        }

        // 개별 중첩 또는 해당 대상의 첫 갱신형 효과는 새 실행 상태로 추가한다.
        _effects.Add(next);
        return true;
    }

    public void Advance(float deltaTime)
    {
        if (!HasEffects || !IsFinitePositive(deltaTime)) return;

        // Manager가 전달한 게임 시간만 누적하므로 일시정지 중에는 진행하지 않는다.
        _elapsedTime += deltaTime;
    }

    public bool TryTakeRecovery(out ItemRecoveryTarget target, out ItemRecoveryAmount amount)
    {
        target = default;
        amount = default;

        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            ActiveRecovery active = _effects[i];
            if (!active.Item)
            {
                _effects.RemoveAt(i);
                continue;
            }

            double recoveryUntil = Math.Min(_elapsedTime, active.EndTime);
            if (active.NextRecoveryTime <= recoveryUntil + TimeTolerance)
            {
                target = active.Target;
                amount = active.Amount;
                active.NextRecoveryTime += active.Interval;

                // 회복 알림에서 상태가 바뀌어도 이전 값을 덮어쓰지 않도록 다음 회복 시점을 먼저 확정한다.
                if (_elapsedTime + TimeTolerance >= active.EndTime && active.NextRecoveryTime > active.EndTime + TimeTolerance)
                {
                    _effects.RemoveAt(i);
                }
                else
                {
                    _effects[i] = active;
                }

                return true;
            }

            if (_elapsedTime + TimeTolerance >= active.EndTime)
            {
                _effects.RemoveAt(i);
            }
        }

        return false;
    }

    public void Clear()
    {
        // 보유 아이템 참조는 해제하고 목록 용량은 다음 사용을 위해 재사용한다.
        _effects.Clear();
        _elapsedTime = 0d;
    }

    private static bool IsFinitePositive(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
