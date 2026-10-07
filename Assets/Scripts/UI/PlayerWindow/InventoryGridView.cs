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
    private InventoryItemSlotView _selectedSlot;
    private IReadOnlyList<ItemDefinition> _items;
    private Action<InventoryItemSlotView> _slotSelectedHandler;
    private bool _hasItems;
    private bool _hasStarted;
    private Selectable _upNavigationTarget;
    private Selectable _rightNavigationTarget;
    private Vector2 _navigationContentSize;

    public ItemType ItemType => _itemType;
    public event Action<InventoryGridView, ItemDefinition> ItemSelected;
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
        if (_content && _slots.Count > 0 && _navigationContentSize != _content.rect.size)
        {
            // 패널 너비가 바뀌어 열 수가 달라진 경우에만 연결을 다시 계산한다.
            RebuildNavigation(true);
            SlotsChanged?.Invoke(this);
        }
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
        IReadOnlyList<ItemDefinition> items = _hasItems ? _items : _previewItems;
        int itemCount = items?.Count ?? 0;

        for (int i = 0; i < itemCount; i++)
        {
            ItemDefinition item = items[i];

            if (!item || item.ItemType != _itemType)
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
            _slots.Add(slot);
            slot.Bind(item);
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
        float topPosition = float.NegativeInfinity;

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i])
            {
                topPosition = Mathf.Max(topPosition, GetSlotPosition(_slots[i]).y);
            }
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            InventoryItemSlotView slot = _slots[i];

            if (!slot || !slot.Button)
            {
                continue;
            }

            Vector2 position = GetSlotPosition(slot);
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

                Vector2 difference = GetSlotPosition(candidate) - position;

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

            if (slot.IsSpawned && slot.transform.parent == _content)
            {
                slot.ReturnToPool();
            }
        }

        // 활성 그리드만 슬롯을 보유하여 세 아이템 타입이 같은 프리팹 풀을 공유한다.
        _slots.Clear();

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
}
