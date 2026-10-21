using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ActiveEffectHUD : MonoBehaviour
{
    private PlayerManager _playerManager;

    [Header("슬롯 생성 및 재사용")]
    [SerializeField] private RectTransform _slotRoot;
    [SerializeField, Tooltip("Project 창의 ActiveEffectSlotView 프리팹을 지정합니다.")]
    private ActiveEffectSlotView _slotPrefab;
    [SerializeField, Min(0)] private int _prewarmSlotCount = 8;

    [Header("지속효과 아이콘")]
    [SerializeField, Tooltip("효과 종류와 스탯별 아이콘을 등록한 공유 에셋을 지정합니다.")]
    private ActiveEffectIconDefinition _iconDefinition;

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
    }

    private void Start()
    {
        if (!_slotRoot || !_slotPrefab) return;

        // 모든 Awake가 끝난 뒤 준비해 PoolingManager의 초기화 순서에 의존하지 않는다.
        PoolingManager poolingManager = PoolingManager.Instance;
        if (!poolingManager || !poolingManager.isActiveAndEnabled)
        {
            Debug.LogWarning("활성 PoolingManager가 없어 지속효과 슬롯을 준비할 수 없습니다.", this);
            return;
        }

        poolingManager.Prewarm(_slotPrefab, Mathf.Max(0, _prewarmSlotCount));
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
        ReleaseSlotsFrom(0);
    }

    private void Refresh()
    {
        _activeEffects.Clear();
        if (_playerManager) _playerManager.GetActiveItemEffects(_activeEffects);
        if (!_slotRoot || !_slotPrefab) return;

        ReleaseSlotsFrom(_activeEffects.Count);
        EnsureSlotCount(_activeEffects.Count);
        for (int i = 0; i < _slots.Count; i++)
        {
            ActiveItemEffectSnapshot effect = _activeEffects[i];
            _slots[i].Refresh(GetIcon(effect), effect.RemainingTime);
        }
    }

    private void EnsureSlotCount(int count)
    {
        if (_slots.Count >= count) return;

        PoolingManager poolingManager = PoolingManager.Instance;
        if (!poolingManager || !poolingManager.isActiveAndEnabled) return;

        // 표시 중인 슬롯만 보관하며 효과 수가 늘었을 때 부족한 수만 대여한다.
        while (_slots.Count < count)
        {
            ActiveEffectSlotView slot = poolingManager.Spawn(_slotPrefab, _slotRoot);
            if (!slot) break;
            _slots.Add(slot);
        }
    }

    private void ReleaseSlotsFrom(int startIndex)
    {
        // 뒤에서 반환해 남아 있는 슬롯의 순서를 유지하고 목록 이동 비용을 피한다.
        for (int i = _slots.Count - 1; i >= startIndex; i--)
        {
            ActiveEffectSlotView slot = _slots[i];
            _slots.RemoveAt(i);
            if (slot && !slot.ReturnToPool()) slot.Hide();
        }
    }

    private Sprite GetIcon(ActiveItemEffectSnapshot effect)
    {
        if (_iconDefinition) return _iconDefinition.GetIcon(effect);
        // 공유 설정이 누락되어도 원본 아이템 아이콘으로 효과를 표시한다.
        return effect.SourceItem ? effect.SourceItem.ItemIcon : null;
    }
}
