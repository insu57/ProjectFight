using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ActiveEffectHUD : MonoBehaviour
{
    [Serializable]
    private struct StatIconBinding
    {
        [SerializeField] private ItemStatType _stat;
        [SerializeField] private Sprite _icon;

        public ItemStatType Stat => _stat;
        public Sprite Icon => _icon;
    }

    private PlayerManager _playerManager;

    [Header("슬롯 생성 및 재사용")]
    [SerializeField] private RectTransform _slotRoot;
    [SerializeField, Tooltip("Project 창의 ActiveEffectSlotView 프리팹을 지정합니다.")]
    private ActiveEffectSlotView _slotPrefab;
    [SerializeField, Min(0)] private int _prewarmSlotCount = 8;

    [Header("지속 회복 아이콘")]
    [SerializeField] private Sprite _healthRecoveryIcon;
    [SerializeField] private Sprite _energyRecoveryIcon;

    [Header("스탯별 강화 아이콘")]
    [SerializeField] private StatIconBinding[] _statIcons = Array.Empty<StatIconBinding>();
    [SerializeField, Tooltip("효과 아이콘과 원본 아이템 아이콘이 없을 때 사용할 아이콘입니다.")]
    private Sprite _fallbackIcon;

    private readonly List<ActiveItemEffectSnapshot> _activeEffects = new(8);
    private readonly List<ActiveEffectSlotView> _slots = new(8);

    private void Awake()
    {
        if (!_slotRoot || !_slotPrefab)
        {
            Debug.LogWarning("ActiveEffectHUD의 Slot Root와 Slot Prefab을 지정해야 합니다.", this);
            return;
        }

        _playerManager = FindFirstObjectByType<PlayerManager>();

        EnsureSlotCount(Mathf.Max(0, _prewarmSlotCount));
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void LateUpdate()
    {
        // Manager의 효과 갱신이 끝난 뒤 실제 잔여시간을 읽으므로 별도 타이머가 어긋나지 않는다.
        Refresh();
    }

    private void OnDisable()
    {
        _activeEffects.Clear();
        HideSlotsFrom(0);
    }

    private void Refresh()
    {
        _activeEffects.Clear();
        if (_playerManager) _playerManager.GetActiveItemEffects(_activeEffects);
        if (!_slotRoot || !_slotPrefab) return;

        EnsureSlotCount(_activeEffects.Count);
        for (int i = 0; i < _activeEffects.Count; i++)
        {
            ActiveItemEffectSnapshot effect = _activeEffects[i];
            _slots[i].Refresh(GetIcon(effect), effect.RemainingTime);
        }

        HideSlotsFrom(_activeEffects.Count);
    }

    private void EnsureSlotCount(int count)
    {
        // 준비한 수를 초과할 때만 생성하고, 만료된 슬롯은 파괴하지 않고 다음 효과에 재사용한다.
        while (_slots.Count < count)
        {
            ActiveEffectSlotView slot = Instantiate(_slotPrefab, _slotRoot, false);
            slot.Hide();
            _slots.Add(slot);
        }
    }

    private void HideSlotsFrom(int startIndex)
    {
        for (int i = startIndex; i < _slots.Count; i++)
        {
            if (_slots[i]) _slots[i].Hide();
        }
    }

    private Sprite GetIcon(ActiveItemEffectSnapshot effect)
    {
        Sprite icon = null;
        if (effect.Kind == ActiveItemEffectKind.RecoveryOverTime)
        {
            icon = effect.RecoveryTarget == ItemRecoveryTarget.Health ? _healthRecoveryIcon : _energyRecoveryIcon;
        }
        else
        {
            for (int i = 0; i < (_statIcons?.Length ?? 0); i++)
            {
                StatIconBinding binding = _statIcons[i];
                if (binding.Stat != effect.Stat || !binding.Icon) continue;
                icon = binding.Icon;
                break;
            }
        }

        // 아직 전용 아이콘이 없는 효과도 원본 아이템 아이콘으로 표시한다.
        if (!icon && effect.SourceItem) icon = effect.SourceItem.ItemIcon;
        return icon ? icon : _fallbackIcon;
    }
}
