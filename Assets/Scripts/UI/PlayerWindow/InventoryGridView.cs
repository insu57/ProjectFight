using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryGridView : MonoBehaviour
{
    private const float NavigationPositionTolerance = 0.01f;

    [Header("그리드")]
    [SerializeField] private ItemType _itemType;
    [SerializeField] private RectTransform _content;
    [SerializeField] private InventoryItemSlotView _slotPrefab;

    [Header("UI 확인용 아이템")]
    [SerializeField] private ItemDefinition[] _previewItems = Array.Empty<ItemDefinition>();

    private readonly List<InventoryItemSlotView> _slots = new(16);
    private readonly List<Vector2> _navigationSlotPositions = new(16);
    private readonly HashSet<ItemDefinition> _displayedConsumables = new(16);
    private InventoryItemSlotView _selectedSlot;
    private IReadOnlyList<ItemDefinition> _items;
    private Action<InventoryItemSlotView> _slotSelectedHandler;
    private Action<InventoryItemSlotView> _slotInteractedHandler;
    private bool _hasItems;
    private bool _hasStarted;
    private Selectable _upNavigationTarget;
    private Selectable _rightNavigationTarget;
    private Vector2 _navigationContentSize;

    public ItemType ItemType => _itemType;
    public InventoryItemSlotView SelectedSlot => _selectedSlot;
    public event Action<InventoryGridView, ItemDefinition> ItemSelected;
    public event Action<InventoryGridView, InventoryItemSlotView> ItemInteracted;
    public event Action<InventoryGridView> SlotsChanged;

    public Button GetNavigationEntryPoint()
    {
        if (!isActiveAndEnabled)
        {
            return null;
        }

        // 그리드에서 탭으로 나갔다가 돌아오면 마지막으로 확인한 슬롯을 우선한다.
        if (IsUsableSlot(_selectedSlot))
        {
            return _selectedSlot.Button;
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            if (IsUsableSlot(_slots[i]))
            {
                return _slots[i].Button;
            }
        }

        return null;
    }

    private bool IsUsableSlot(InventoryItemSlotView slot)
    {
        return slot && slot.IsSpawned && slot.Item && slot.transform.parent == _content;
    }

    public void RefreshEquipmentIndicators(PlayerManager player)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            InventoryItemSlotView slot = _slots[i];
            if (IsUsableSlot(slot))
            {
                slot.SetEquipped(player && player.IsEquipmentEquipped(slot.InventoryIndex, slot.Item));
            }
        }
    }

    private void OnEnable()
    {
        if (_hasStarted)
        {
            RefreshItems();
        }
    }

    private void Start()
    {
        // 모든 매니저의 Awake가 끝난 뒤 최초 대여를 시작해 싱글톤 초기화 순서에 의존하지 않는다.
        _hasStarted = true;
        RefreshItems();
    }

    private void OnDisable()
    {
        ReleaseSlots();
    }

    private void OnDestroy()
    {
        ReleaseSlots();
    }

    private void LateUpdate()
    {
        if (!_content || _slots.Count == 0)
        {
            return;
        }

        bool contentSizeChanged = _navigationContentSize != _content.rect.size;
        if (contentSizeChanged || HaveSlotPositionsChanged())
        {
            // Canvas의 지연 배치로 슬롯 위치만 바뀐 경우에도 실제 이웃으로 연결을 갱신한다.
            RebuildNavigation(contentSizeChanged);
            SlotsChanged?.Invoke(this);
        }
    }

    private bool HaveSlotPositionsChanged()
    {
        if (_navigationSlotPositions.Count != _slots.Count)
        {
            return true;
        }

        // 기존 위치 목록을 재사용해 매 프레임 할당이나 전체 방향 연결 재계산을 피한다.
        for (int i = 0; i < _slots.Count; i++)
        {
            Vector2 position = _slots[i] ? GetSlotPosition(_slots[i]) : Vector2.zero;
            if ((position - _navigationSlotPositions[i]).sqrMagnitude > NavigationPositionTolerance * NavigationPositionTolerance)
            {
                return true;
            }
        }

        return false;
    }

    public void SetNavigationTargets(Selectable upTarget, Selectable rightTarget)
    {
        if (_upNavigationTarget == upTarget && _rightNavigationTarget == rightTarget)
        {
            return;
        }

        _upNavigationTarget = upTarget;
        _rightNavigationTarget = rightTarget;

        if (isActiveAndEnabled && _slots.Count > 0)
        {
            RebuildNavigation(false);
        }
    }

    public void SetItems(IReadOnlyList<ItemDefinition> items)
    {
        // 닫힌 UI에는 슬롯을 대여하지 않고 마지막 목록을 보관해 다음 활성화 때 표시한다.
        _items = items;
        _hasItems = true;

        if (_hasStarted && isActiveAndEnabled)
        {
            RefreshItems();
        }
    }

    private void RefreshItems() //비용?
    {
        ReleaseSlots();

        if (!_content || !_slotPrefab)
        {
            Debug.LogWarning("InventoryGridView의 Content와 Slot Prefab을 지정해야 합니다.", this);
            return;
        }

        PoolingManager poolingManager = PoolingManager.Instance;

        if (!poolingManager || !poolingManager.isActiveAndEnabled)
        {
            Debug.LogWarning("활성 PoolingManager가 없어 인벤토리 슬롯을 표시할 수 없습니다.", this);
            return;
        }

        _slotSelectedHandler ??= HandleSlotSelected;
        _slotInteractedHandler ??= HandleSlotInteracted;
        IReadOnlyList<ItemDefinition> items = _hasItems ? _items : _previewItems;
        int itemCount = items?.Count ?? 0;

        for (int i = 0; i < itemCount; i++)
        {
            ItemDefinition item = items[i];

            if (!item || item.ItemType != _itemType)
            {
                continue;
            }

            // 확인용 목록에도 같은 소모품이 반복될 수 있어 재사용하는 집합으로 중복 슬롯을 막는다.
            if (_itemType == ItemType.Consumable && !_displayedConsumables.Add(item))
            {
                continue;
            }

            InventoryItemSlotView slot = poolingManager.Spawn(_slotPrefab, _content);

            if (!slot)
            {
                Debug.LogWarning("인벤토리 슬롯을 대여할 수 없습니다. Content와 PoolingManager의 활성 상태를 확인하세요.", this);
                break;
            }

            slot.Selected += _slotSelectedHandler;
            slot.Interacted += _slotInteractedHandler;
            _slots.Add(slot);
            // 실제 보유 목록의 인덱스만 장착 항목의 식별자로 전달하고 확인용 목록은 -1로 구분한다.
            slot.Bind(item, _hasItems ? i : -1);
        }

        RebuildNavigation(true);
        SlotsChanged?.Invoke(this);
    }

    private void RebuildNavigation(bool rebuildLayout)
    {
        if (!_content || _slots.Count == 0)
        {
            return;
        }

        if (rebuildLayout)
        {
            // 대여 직후의 프리팹 위치 대신 GridLayoutGroup이 계산한 실제 배치를 사용한다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        _navigationContentSize = _content.rect.size;
        _navigationSlotPositions.Clear();
        if (_navigationSlotPositions.Capacity < _slots.Count)
        {
            _navigationSlotPositions.Capacity = _slots.Capacity;
        }

        float topPosition = float.NegativeInfinity;

        for (int i = 0; i < _slots.Count; i++)
        {
            // 한 번 읽은 배치를 모든 방향 탐색에서 공유해 동일한 위치로 연결을 계산한다.
            Vector2 position = _slots[i] ? GetSlotPosition(_slots[i]) : Vector2.zero;
            _navigationSlotPositions.Add(position);
            if (_slots[i])
            {
                topPosition = Mathf.Max(topPosition, position.y);
            }
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            InventoryItemSlotView slot = _slots[i];

            if (!slot || !slot.Button)
            {
                continue;
            }

            Vector2 position = _navigationSlotPositions[i];
            Selectable up = null, down = null, left = null, right = null;
            float upDistance = float.PositiveInfinity, downDistance = float.PositiveInfinity,
                leftDistance = float.PositiveInfinity, rightDistance = float.PositiveInfinity;

            for (int j = 0; j < _slots.Count; j++)
            {
                InventoryItemSlotView candidate = _slots[j];

                if (i == j || !candidate || !candidate.Button)
                {
                    continue;
                }

                Vector2 difference = _navigationSlotPositions[j] - position;

                if (Mathf.Abs(difference.x) <= NavigationPositionTolerance)
                {
                    if (difference.y > NavigationPositionTolerance && difference.y < upDistance)
                    {
                        up = candidate.Button;
                        upDistance = difference.y;
                    }
                    else if (difference.y < -NavigationPositionTolerance && -difference.y < downDistance)
                    {
                        down = candidate.Button;
                        downDistance = -difference.y;
                    }
                }

                if (Mathf.Abs(difference.y) <= NavigationPositionTolerance)
                {
                    if (difference.x > NavigationPositionTolerance && difference.x < rightDistance)
                    {
                        right = candidate.Button;
                        rightDistance = difference.x;
                    }
                    else if (difference.x < -NavigationPositionTolerance && -difference.x < leftDistance)
                    {
                        left = candidate.Button;
                        leftDistance = -difference.x;
                    }
                }
            }

            // 최상단은 플레이어 윈도우로, 각 행의 마지막 실제 슬롯은 인벤토리 타입 탭으로 이동한다.
            if (Mathf.Abs(position.y - topPosition) <= NavigationPositionTolerance)
            {
                up = _upNavigationTarget;
            }

            slot.SetNavigation(up, down, left, right ? right : _rightNavigationTarget);
        }
    }

    private static Vector2 GetSlotPosition(InventoryItemSlotView slot)
    {
        return slot.transform is RectTransform rectTransform ? rectTransform.anchoredPosition : (Vector2)slot.transform.localPosition;
    }

    public void ClearSelection()
    {
        if (!_selectedSlot)
        {
            return;
        }

        _selectedSlot.SetSelected(false);
        _selectedSlot = null;
        ItemSelected?.Invoke(this, null);
    }

    private void ReleaseSlots()
    {
        bool hadSlots = _slots.Count > 0;
        ClearSelection();

        for (int i = 0; i < _slots.Count; i++)
        {
            InventoryItemSlotView slot = _slots[i];

            if (!slot)
            {
                continue;
            }

            slot.Selected -= _slotSelectedHandler;
            slot.Interacted -= _slotInteractedHandler;

            if (slot.IsSpawned && slot.transform.parent == _content)
            {
                slot.ReturnToPool();
            }
        }

        // 활성 그리드만 슬롯을 보유하여 세 아이템 타입이 같은 프리팹 풀을 공유한다.
        _slots.Clear();
        _navigationSlotPositions.Clear();
        _displayedConsumables.Clear();

        if (hadSlots)
        {
            // 풀로 반환한 슬롯을 타입 탭의 복귀 대상으로 계속 참조하지 않도록 알린다.
            SlotsChanged?.Invoke(this);
        }
    }

    private void HandleSlotSelected(InventoryItemSlotView slot)
    {
        if (!isActiveAndEnabled || !slot || !slot.Item || !_slots.Contains(slot) || _selectedSlot == slot)
        {
            return;
        }

        if (_selectedSlot)
        {
            _selectedSlot.SetSelected(false);
        }

        // 같은 SO의 아이템이 여러 칸에 있어도 실제 선택한 슬롯 하나만 표시한다.
        _selectedSlot = slot;
        slot.SetSelected(true);
        ItemSelected?.Invoke(this, slot.Item);
    }

    private void HandleSlotInteracted(InventoryItemSlotView slot)
    {
        if (!isActiveAndEnabled || !IsUsableSlot(slot) || !_slots.Contains(slot)) return;

        // 슬롯은 플레이어를 직접 찾지 않고 현재 소유 그리드를 통해 상위 View로 요청한다.
        ItemInteracted?.Invoke(this, slot);
    }
}
