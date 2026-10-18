using System.Collections.Generic;
using UnityEngine;

// 공유 SO에는 상태를 쓰지 않고, 플레이어별 활성 여부와 아이템별 발동 대기 시간을 보관한다.
public sealed class PassiveItemEffectRuntime
{
    private struct RegisteredEffect
    {
        public ItemDefinition Item;
        public PassiveItemEffectData Effect;
        public ItemType SourceType;
        public int SourceIndex;
        public int EffectIndex;
        public bool IsActive;
        public double NextTriggerTime;
    }

    private readonly List<RegisteredEffect> _effects = new(8);
    private readonly List<StatModifier> _statModifiers = new(8);
    private double _elapsedTime;

    public int EffectCount => _effects.Count;
    public IReadOnlyList<StatModifier> StatModifiers => _statModifiers;

    public void Synchronize(IReadOnlyList<ItemDefinition> relicItems, IReadOnlyList<ItemDefinition> equippedItems)
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            RegisteredEffect entry = _effects[i];
            entry.IsActive = false;
            _effects[i] = entry;
        }

        RegisterItems(relicItems, ItemType.Relic, 0);
        // 장착 배열의 None 칸은 효과 출처로 사용하지 않는다.
        RegisterItems(equippedItems, ItemType.Equipment, (int)EquipmentSlot.Head);
    }

    private void RegisterItems(IReadOnlyList<ItemDefinition> items, ItemType sourceType, int firstIndex)
    {
        if (items == null) return;

        for (int i = firstIndex; i < items.Count; i++)
        {
            ItemDefinition item = items[i];
            if (!item || item.ItemType != sourceType) continue;

            IReadOnlyList<PassiveItemEffectData> effects = item.PassiveEffects;
            for (int j = 0; j < effects.Count; j++)
            {
                PassiveItemEffectData effect = effects[j];
                if (effect is not ConditionalStatEffectData && effect is not TriggeredEffectData) continue;

                int existingIndex = FindEffect(item, effect, sourceType, i, j);
                if (existingIndex >= 0)
                {
                    // 다른 아이템 변경과 장비 재장착으로 이미 시작한 쿨다운이 초기화되지 않는다.
                    RegisteredEffect entry = _effects[existingIndex];
                    entry.IsActive = true;
                    _effects[existingIndex] = entry;
                }
                else
                {
                    _effects.Add(new RegisteredEffect { Item = item, Effect = effect, SourceType = sourceType,
                        SourceIndex = i, EffectIndex = j, IsActive = true });
                }
            }
        }
    }

    private int FindEffect(ItemDefinition item, PassiveItemEffectData effect, ItemType sourceType, int sourceIndex, int effectIndex)
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            RegisteredEffect entry = _effects[i];
            // 유물은 보유 목록의 각 칸을 구분하므로 같은 SO를 여러 개 보유해도 별도로 발동한다.
            if (entry.Item == item && ReferenceEquals(entry.Effect, effect) && entry.SourceType == sourceType
                && entry.SourceIndex == sourceIndex && entry.EffectIndex == effectIndex) return i;
        }
        return -1;
    }

    public void RebuildConditionalStatModifiers(PlayerStatSnapshot stats)
    {
        _statModifiers.Clear();
        if (stats.CurrentHealth <= 0f) return;

        for (int i = 0; i < _effects.Count; i++)
        {
            RegisteredEffect entry = _effects[i];
            if (!entry.IsActive || !entry.Item || entry.Effect is not ConditionalStatEffectData conditional) continue;
            if (!AreConditionsMet(conditional.Conditions, stats)) continue;

            IReadOnlyList<StatModifier> modifiers = conditional.StatModifiers;
            for (int j = 0; j < modifiers.Count; j++) _statModifiers.Add(modifiers[j]);
        }
    }

    public bool TryGetTriggeredEffect(int index, ItemEffectTrigger trigger, PlayerStatSnapshot stats, out ItemDefinition item,
        out TriggeredEffectData effect)
    {
        item = null;
        effect = null;
        if (index < 0 || index >= _effects.Count || stats.CurrentHealth <= 0f) return false;

        RegisteredEffect entry = _effects[index];
        if (!entry.IsActive || !entry.Item || entry.Effect is not TriggeredEffectData triggered || triggered.Trigger != trigger) return false;
        if (!IsFinite(triggered.Chance) || triggered.Chance <= 0f || triggered.Chance > 1f) return false;
        if (!IsFinite(triggered.Cooldown) || triggered.Cooldown < 0f || _elapsedTime < entry.NextTriggerTime) return false;
        if (!AreConditionsMet(triggered.Conditions, stats)) return false;

        item = entry.Item;
        effect = triggered;
        return true;
    }

    public bool TryStartTriggeredEffect(int index, TriggeredEffectData expectedEffect)
    {
        if (index < 0 || index >= _effects.Count || expectedEffect == null) return false;
        if (!IsFinite(expectedEffect.Chance) || expectedEffect.Chance <= 0f || expectedEffect.Chance > 1f
            || !IsFinite(expectedEffect.Cooldown) || expectedEffect.Cooldown < 0f) return false;
        RegisteredEffect entry = _effects[index];
        if (!entry.IsActive || !entry.Item || !ReferenceEquals(entry.Effect, expectedEffect) || _elapsedTime < entry.NextTriggerTime) return false;
        if (expectedEffect.Chance < 1f && Random.value >= expectedEffect.Chance) return false;

        // 실행 알림이 다른 이벤트를 일으키기 전에 쿨다운을 확정한다.
        entry.NextTriggerTime = _elapsedTime + expectedEffect.Cooldown;
        _effects[index] = entry;
        return true;
    }

    public void Advance(float deltaTime)
    {
        // Manager가 전달한 게임 시간만 사용하므로 일시정지 중에는 쿨다운도 멈춘다.
        if (IsFinite(deltaTime) && deltaTime > 0f) _elapsedTime += deltaTime;
    }

    private static bool AreConditionsMet(IReadOnlyList<ItemEffectCondition> conditions, PlayerStatSnapshot stats)
    {
        for (int i = 0; i < conditions.Count; i++)
        {
            ItemEffectCondition condition = conditions[i];
            float current;
            float maximum;
            switch (condition.Type)
            {
                case ItemEffectConditionType.HealthRatio:
                    current = stats.CurrentHealth;
                    maximum = stats.MaxHealth;
                    break;
                case ItemEffectConditionType.EnergyRatio:
                    current = stats.CurrentEnergy;
                    maximum = stats.MaxEnergy;
                    break;
                default:
                    return false;
            }

            float threshold = condition.Threshold;
            if (!IsFinite(current) || !IsFinite(maximum) || maximum < 0f || !IsFinite(threshold)) return false;
            // 최대 에너지가 0인 캐릭터의 현재 에너지 비율은 0으로 취급한다.
            float ratio = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            bool isMet = condition.Comparison switch
            {
                ItemEffectComparison.LessOrEqual => ratio <= threshold,
                ItemEffectComparison.GreaterOrEqual => ratio >= threshold,
                ItemEffectComparison.Less => ratio < threshold,
                ItemEffectComparison.Greater => ratio > threshold,
                _ => false
            };
            // 빈 목록은 항상 충족하며 여러 조건은 모두 충족해야 한다.
            if (!isMet) return false;
        }
        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public void Clear()
    {
        _effects.Clear();
        _statModifiers.Clear();
        _elapsedTime = 0d;
    }
}
