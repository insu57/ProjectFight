using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ActiveEffectIcons", menuName = "UI/Active Effect Icon Definition")]
public sealed class ActiveEffectIconDefinition : ScriptableObject
{
    [Serializable]
    private struct StatIconBinding
    {
        [SerializeField] private ItemStatType _stat;
        [SerializeField] private Sprite _icon;

        public ItemStatType Stat => _stat;
        public Sprite Icon => _icon;
    }

    [Header("지속 회복 아이콘")]
    [SerializeField] private Sprite _healthRecoveryIcon;
    [SerializeField] private Sprite _energyRecoveryIcon;

    [Header("스탯별 강화 아이콘")]
    [SerializeField] private StatIconBinding[] _statIcons = Array.Empty<StatIconBinding>();
    [SerializeField, Tooltip("효과 아이콘과 원본 아이템 아이콘이 없을 때 사용할 아이콘입니다.")]
    private Sprite _fallbackIcon;

    private Dictionary<ItemStatType, Sprite> _statIconCache;

    private void OnEnable()
    {
        _statIconCache = CreateStatIconCache();
    }

    private void OnValidate()
    {
        // 검증 콜백에서는 Unity 오브젝트에 접근하지 않고 다음 조회 시 캐시를 재구성한다.
        _statIconCache = null;
    }

    public Sprite GetIcon(ActiveItemEffectSnapshot effect)
    {
        Sprite icon = null;
        if (effect.Kind == ActiveItemEffectKind.RecoveryOverTime)
        {
            icon = effect.RecoveryTarget == ItemRecoveryTarget.Health ? _healthRecoveryIcon : _energyRecoveryIcon;
        }
        else
        {
            // 같은 SO를 참조하는 모든 HUD가 하나의 캐시를 공유한다.
            Dictionary<ItemStatType, Sprite> cache = _statIconCache;
            if (cache == null)
            {
                cache = CreateStatIconCache();
                _statIconCache = cache;
            }

            cache.TryGetValue(effect.Stat, out icon);
        }

        // 전용 아이콘이 없는 효과는 현재 원본 아이템의 아이콘을 우선 사용한다.
        if (!icon && effect.SourceItem) icon = effect.SourceItem.ItemIcon;
        return icon ? icon : _fallbackIcon;
    }

    private Dictionary<ItemStatType, Sprite> CreateStatIconCache()
    {
        StatIconBinding[] bindings = _statIcons;
        int count = bindings?.Length ?? 0;
        Dictionary<ItemStatType, Sprite> cache = new(count);
        for (int i = 0; i < count; i++)
        {
            StatIconBinding binding = bindings[i];
            // 중복 스탯은 먼저 등록된 유효한 아이콘을 유지한다.
            if (binding.Icon) cache.TryAdd(binding.Stat, binding.Icon);
        }

        return cache;
    }
}
