using System;
using System.Collections.Generic;
using UnityEngine;

// 아이템 효과의 실행 규칙과 상태를 소유하며, 실제 플레이어 변경은 주입된 Manager가 중개한다.
public sealed class PlayerItemEffects
{
    private readonly IPlayerItemEffectHost _host;
    private readonly RecoveryOverTimeRuntime _recoveryOverTime = new();
    private readonly TimedStatModifierRuntime _timedStatModifiers = new();
    private readonly PassiveItemEffectRuntime _passiveEffects = new();
    private readonly Queue<PassiveTriggerEvent> _pendingPassiveTriggers = new(4);
    private bool _isProcessingPassiveTrigger;

    private readonly struct PassiveTriggerEvent
    {
        public readonly ItemEffectTrigger Trigger;
        public readonly PlayerStatSnapshot Stats;

        public PassiveTriggerEvent(ItemEffectTrigger trigger, PlayerStatSnapshot stats)
        {
            Trigger = trigger;
            Stats = stats;
        }
    }

    // Manager는 보정 재계산 중의 중첩 알림을 건너뛰고 바깥 처리에서 최종 스냅샷을 전달한다.
    public bool IsRefreshingStats { get; private set; }

    public PlayerItemEffects(IPlayerItemEffectHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public void AppendActiveEffects(List<ActiveItemEffectSnapshot> results)
    {
        // 조회는 시간을 진행시키지 않으며, 사망·비활성화 중에는 남아 있는 상태도 표시하지 않는다.
        if (!_host.CanRunItemEffects) return;
        _recoveryOverTime.AppendActiveEffects(results);
        _timedStatModifiers.AppendActiveEffects(results);
    }

    public bool CanApplyAny(IReadOnlyList<ItemEffectData> effects)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            if (CanApply(effects[i])) return true;
        }
        return false;
    }

    private bool CanApply(ItemEffectData effect)
    {
        // 사용·획득·패시브는 같은 판정을 공유한다. 컴포넌트가 없으면 기본 스냅샷의 체력이 0이다.
        PlayerStatSnapshot stats = _host.CurrentPlayerStats;
        if (stats.CurrentHealth <= 0f) return false;

        return effect switch
        {
            RestoreHealthEffectData health => GetRecoveryAmount(health.Amount, stats.MaxHealth) > 0f,
            RestoreEnergyEffectData energy => GetRecoveryAmount(energy.Amount, stats.MaxEnergy) > 0f,
            AddSkillPointsEffectData points => points.Amount > 0 && _host.RemainingSkillPointCapacity > 0,
            AddMoneyEffectData money => money.Amount > 0 && stats.Money < int.MaxValue,
            AddExperienceEffectData experience => experience.Amount > 0 && stats.TargetExp > 0,
            RecoveryOverTimeEffectData recovery => _host.CanRunItemEffects && RecoveryOverTimeRuntime.CanStart(recovery),
            TimedStatModifierEffectData timed => _host.CanRunItemEffects && TimedStatModifierRuntime.CanStart(timed),
            _ => false
        };
    }

    public void Apply(ItemDefinition item, IReadOnlyList<ItemEffectData> effects, bool requiresActivePlayer = false)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            // 패시브 실행 중 비활성화되면 같은 묶음의 남은 즉시 효과도 중단한다.
            if (requiresActivePlayer && !_host.CanRunItemEffects) break;
            ItemEffectData effect = effects[i];
            // 각 효과 직전에 다시 조회하여 앞 효과가 바꾼 최대치와 지급 한도를 반영한다.
            if (!CanApply(effect)) continue;

            switch (effect)
            {
                case RestoreHealthEffectData health:
                    _host.RestoreHealth(GetRecoveryAmount(health.Amount, _host.CurrentPlayerStats.MaxHealth));
                    break;
                case RestoreEnergyEffectData energy:
                    _host.RestoreEnergy(GetRecoveryAmount(energy.Amount, _host.CurrentPlayerStats.MaxEnergy));
                    break;
                case AddSkillPointsEffectData points:
                    _host.AddSkillPoints(Math.Min(points.Amount, _host.RemainingSkillPointCapacity));
                    break;
                case AddMoneyEffectData money:
                    _host.AddMoney(money.Amount);
                    break;
                case AddExperienceEffectData experience:
                    _host.AddExperience(experience.Amount);
                    break;
                case RecoveryOverTimeEffectData recovery:
                    _recoveryOverTime.TryStart(item, i, recovery);
                    break;
                case TimedStatModifierEffectData timed:
                    if (_timedStatModifiers.TryStart(item, i, timed)) _host.RebuildTimedStatModifiers(_timedStatModifiers.StatModifiers);
                    break;
            }
        }
    }

    public void Tick(float deltaTime)
    {
        if (_host.CanRunItemEffects) _passiveEffects.Advance(deltaTime);
        // 만료된 최대치 보정을 먼저 제거한 다음 이번 프레임의 비율 회복량을 계산한다.
        UpdateTimedStatModifiers(deltaTime);
        UpdateRecoveryOverTime(deltaTime);
    }

    private void UpdateTimedStatModifiers(float deltaTime)
    {
        if (!_timedStatModifiers.HasEffects) return;
        if (!_host.CanRunItemEffects)
        {
            ClearTimedStatModifiers();
            return;
        }
        if (_timedStatModifiers.Advance(deltaTime)) _host.RebuildTimedStatModifiers(_timedStatModifiers.StatModifiers);
    }

    private void ClearTimedStatModifiers()
    {
        // 보정 알림에서 사망·비활성화가 재진입해도 실행 목록을 먼저 비워 한 번만 해제한다.
        if (_timedStatModifiers.Clear()) _host.RebuildTimedStatModifiers(_timedStatModifiers.StatModifiers);
    }

    private void UpdateRecoveryOverTime(float deltaTime)
    {
        if (!_recoveryOverTime.HasEffects) return;
        if (!_host.CanRunItemEffects)
        {
            _recoveryOverTime.Clear();
            return;
        }

        _recoveryOverTime.Advance(deltaTime);
        while (_recoveryOverTime.TryTakeRecovery(out ItemRecoveryTarget target, out ItemRecoveryAmount amount))
        {
            // 앞 회복의 알림에서 사망하거나 비활성화되면 남은 회복도 중단한다.
            if (!_host.CanRunItemEffects)
            {
                _recoveryOverTime.Clear();
                break;
            }
            PlayerStatSnapshot stats = _host.CurrentPlayerStats;
            switch (target)
            {
                case ItemRecoveryTarget.Health:
                    _host.RestoreHealth(GetRecoveryAmount(amount, stats.MaxHealth));
                    break;
                case ItemRecoveryTarget.Energy:
                    _host.RestoreEnergy(GetRecoveryAmount(amount, stats.MaxEnergy));
                    break;
            }
        }
    }

    private static float GetRecoveryAmount(ItemRecoveryAmount amount, float maximum)
    {
        if (amount.Value <= 0f || float.IsNaN(amount.Value) || float.IsInfinity(amount.Value)) return 0f;

        // 비율 회복은 실행 시점의 최종 최대치를 사용한다.
        return amount.Mode switch
        {
            ItemRecoveryMode.Fixed => amount.Value,
            ItemRecoveryMode.MaxRatio => maximum * Mathf.Min(1f, amount.Value),
            _ => 0f
        };
    }

    public void SynchronizeSources(IReadOnlyList<ItemDefinition> relicItems, IReadOnlyList<ItemDefinition> equippedItems)
    {
        bool wasRefreshing = IsRefreshingStats;
        IsRefreshingStats = true;
        try
        {
            if (_host.CanRunItemEffects)
            {
                _passiveEffects.Synchronize(relicItems, equippedItems);
                _passiveEffects.RebuildConditionalStatModifiers(_host.CurrentPlayerStats);
            }
            else
            {
                _passiveEffects.Clear();
            }
            // 모든 출처의 보정을 함께 전달하여 장비 교체 중의 임시 최대치로 현재량이 제한되지 않게 한다.
            _host.RebuildItemStatModifiers(relicItems, equippedItems, _passiveEffects.StatModifiers);
        }
        finally
        {
            IsRefreshingStats = wasRefreshing;
        }
    }

    public void RefreshConditionalStats(PlayerStatSnapshot stats)
    {
        if (IsRefreshingStats) return;

        IsRefreshingStats = true;
        try
        {
            // 변경 시점의 스냅샷으로 한 번 판정해 최대치 보정이 자기 조건을 재귀 호출하지 않게 한다.
            _passiveEffects.RebuildConditionalStatModifiers(_host.CanRunItemEffects ? stats : default);
            _host.RebuildItemPassiveStatModifiers(_passiveEffects.StatModifiers);
        }
        finally
        {
            IsRefreshingStats = false;
        }
    }

    private void ClearPassiveEffects()
    {
        _passiveEffects.Clear();
        _pendingPassiveTriggers.Clear();
        bool wasRefreshing = IsRefreshingStats;
        IsRefreshingStats = true;
        try
        {
            _host.RebuildItemPassiveStatModifiers(_passiveEffects.StatModifiers);
        }
        finally
        {
            IsRefreshingStats = wasRefreshing;
        }
    }

    public void Trigger(ItemEffectTrigger trigger, PlayerStatSnapshot stats)
    {
        if (!_host.CanRunItemEffects) return;

        // 전투 이벤트가 효과 알림 중 재진입해도 같은 큐에서 순서대로 처리한다.
        _pendingPassiveTriggers.Enqueue(new PassiveTriggerEvent(trigger, stats));
        if (_isProcessingPassiveTrigger) return;

        _isProcessingPassiveTrigger = true;
        try
        {
            while (_pendingPassiveTriggers.Count > 0 && _host.CanRunItemEffects)
            {
                PassiveTriggerEvent pending = _pendingPassiveTriggers.Dequeue();
                // 실행 중 새로 획득한 패시브는 다음 이벤트부터 참여한다.
                int effectCount = _passiveEffects.EffectCount;
                for (int i = 0; i < effectCount && _host.CanRunItemEffects; i++)
                {
                    if (!_passiveEffects.TryGetTriggeredEffect(i, pending.Trigger, pending.Stats,
                        out ItemDefinition item, out TriggeredEffectData effect)) continue;
                    if (!CanApplyAny(effect.Effects) || !_passiveEffects.TryStartTriggeredEffect(i, effect)) continue;
                    Apply(item, effect.Effects, true);
                }
            }
        }
        finally
        {
            _isProcessingPassiveTrigger = false;
            _pendingPassiveTriggers.Clear();
        }
    }

    public void Clear()
    {
        ClearPassiveEffects();
        _recoveryOverTime.Clear();
        ClearTimedStatModifiers();
    }
}
