using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryView : MonoBehaviour
{
    [SerializeField] private InventoryTabController _tabController;
    [SerializeField] private InventoryGridView[] _grids = Array.Empty<InventoryGridView>();
    [SerializeField] private ItemDescriptionView _descriptionView;
    [SerializeField] private EquipmentPanelView _equipmentPanelView;

    [Header("슬롯 탐색")]
    [SerializeField] private Button _playerWindowInventoryTabButton;

    private Action<InventoryGridView> _slotsChangedHandler;
    private Action<ItemType> _inventoryChangedHandler;
    private Action<EquipmentSlot> _equipmentChangedHandler;
    private PlayerManager _playerManager;
    private InventoryItemSlotView _selectedItemSlot;

    private void Awake()
    {
        // 기존 플레이어 창의 다른 View와 동일하게 플레이어 참조를 한 번만 찾는다.
        _playerManager = FindFirstObjectByType<PlayerManager>();
    }

    private void OnEnable()
    {
        _slotsChangedHandler ??= HandleSlotsChanged;
        _inventoryChangedHandler ??= RefreshInventory;
        _equipmentChangedHandler ??= HandleEquipmentChanged;

        if (_tabController)
        {
            _tabController.ItemTypeChanged += HandleItemTypeChanged;
        }

        for (int i = 0; i < (_grids?.Length ?? 0); i++)
        {
            if (_grids[i])
            {
                _grids[i].ItemSelected += HandleItemSelected;
                _grids[i].ItemInteracted += HandleItemInteracted;
                _grids[i].SlotsChanged += _slotsChangedHandler;
            }
        }

        ConfigureNavigationTargets();

        if (_playerManager)
        {
            _playerManager.InventoryChanged += _inventoryChangedHandler;
            _playerManager.EquipmentChanged += _equipmentChangedHandler;
        }

        // 창이 닫힌 동안 획득한 아이템도 재개방 시 최신 목록으로 표시한다.
        RefreshAllInventory();

        if (_tabController && _descriptionView)
        {
            // 활성화 순서 때문에 탭 변경 이벤트를 놓쳐도 재개방 시 현재 탭과 맞춘다.
            _descriptionView.ShowTypePanel(_tabController.CurrentItemType);
        }

        RefreshTabGridNavigation(GetCurrentGrid(), true);
        RefreshEquipmentState();
    }

    private void OnDisable()
    {
        if (_playerManager)
        {
            _playerManager.InventoryChanged -= _inventoryChangedHandler;
            _playerManager.EquipmentChanged -= _equipmentChangedHandler;
        }

        if (_tabController)
        {
            _tabController.ItemTypeChanged -= HandleItemTypeChanged;
        }

        for (int i = 0; i < (_grids?.Length ?? 0); i++)
        {
            if (_grids[i])
            {
                _grids[i].ItemSelected -= HandleItemSelected;
                _grids[i].ItemInteracted -= HandleItemInteracted;
                _grids[i].SlotsChanged -= _slotsChangedHandler;
            }
        }

        ClearSelection();

        if (_tabController)
        {
            _tabController.ClearGridNavigationTarget();
        }
    }

    private void RefreshAllInventory()
    {
        ClearSelection();

        for (int i = 0; i < (_grids?.Length ?? 0); i++)
        {
            InventoryGridView grid = _grids[i];

            if (grid)
            {
                // 재개방 시에는 비활성 타입까지 최신 보유 목록을 연결하고 확인용 목록을 대체한다.
                grid.SetItems(_playerManager ? _playerManager.GetInventoryItems(grid.ItemType) : Array.Empty<ItemDefinition>());
            }
        }
    }

    private void RefreshInventory(ItemType itemType)
    {
        if (!_playerManager)
        {
            return;
        }

        for (int i = 0; i < (_grids?.Length ?? 0); i++)
        {
            InventoryGridView grid = _grids[i];

            if (grid && grid.ItemType == itemType)
            {
                // 다른 타입의 슬롯과 선택은 유지하고 변경된 타입만 기존 풀링 흐름으로 갱신한다.
                grid.SetItems(_playerManager.GetInventoryItems(itemType));
            }
        }
    }

    private void HandleItemTypeChanged(ItemType itemType)
    {
        // 타입 전환 시 이전 선택을 해제하고 공통 하단 설명을 숨긴다.
        ClearSelection();

        if (_descriptionView)
        {
            _descriptionView.ShowTypePanel(itemType);
        }

        ConfigureNavigationTargets();
        RefreshTabGridNavigation(GetCurrentGrid(), true);
    }

    private void HandleSlotsChanged(InventoryGridView grid)
    {
        if (grid && grid.ItemType == ItemType.Equipment)
        {
            // 장비 목록 갱신과 탭 재개방으로 다시 대여한 슬롯에도 기존 장착 상태를 표시한다.
            grid.RefreshEquipmentIndicators(_playerManager);
        }

        if (grid && (!_tabController || grid.ItemType == _tabController.CurrentItemType))
        {
            RefreshTabGridNavigation(grid, true);
        }
    }

    private InventoryGridView GetCurrentGrid()
    {
        if (!_tabController)
        {
            return null;
        }

        // 창을 열거나 타입을 전환할 때만 현재 활성 그리드를 찾는다.
        for (int i = 0; i < (_grids?.Length ?? 0); i++)
        {
            InventoryGridView grid = _grids[i];

            if (grid && grid.isActiveAndEnabled && grid.ItemType == _tabController.CurrentItemType)
            {
                return grid;
            }
        }

        return null;
    }

    private void RefreshTabGridNavigation(InventoryGridView grid, bool refreshNavigation)
    {
        if (!isActiveAndEnabled || !_tabController)
        {
            return;
        }

        // 슬롯 이벤트에서는 전달받은 그리드를 바로 사용해 전체 배열의 반복 검색을 피한다.
        Button target = grid && grid.isActiveAndEnabled && grid.ItemType == _tabController.CurrentItemType
            ? grid.GetNavigationEntryPoint() : null;

        // 빈 그리드는 왼쪽 대상을 null로 지정해 다른 영역으로 자동 이동하지 않는다.
        _tabController.SetGridNavigationTarget(target, refreshNavigation);
    }

    private void ConfigureNavigationTargets()
    {
        for (int i = 0; i < (_grids?.Length ?? 0); i++)
        {
            InventoryGridView grid = _grids[i];

            if (grid)
            {
                // 배열 순서 대신 ItemType으로 해당 그리드의 오른쪽 이동 대상을 찾는다.
                Button itemTypeTabButton = _tabController ? _tabController.GetTabButton(grid.ItemType) : null;
                grid.SetNavigationTargets(_playerWindowInventoryTabButton, itemTypeTabButton);
            }
        }
    }

    private void HandleItemSelected(InventoryGridView grid, ItemDefinition item)
    {
        // 비활성/다른 타입 그리드의 선택 해제가 현재 타입의 설명과 탐색 대상을 지우지 않도록 한다.
        if (!grid || !grid.isActiveAndEnabled || (_tabController && grid.ItemType != _tabController.CurrentItemType))
        {
            return;
        }

        RefreshTabGridNavigation(grid, false);
        _selectedItemSlot = item ? grid.SelectedSlot : null;
        RefreshEquipmentPanel();

        if (!_descriptionView)
        {
            return;
        }

        if (!item)
        {
            _descriptionView.Hide();
            return;
        }

        _descriptionView.Show(item, GetConsumableCount(item));
    }

    private void HandleItemInteracted(InventoryGridView grid, InventoryItemSlotView slot)
    {
        if (!_playerManager || !grid || !grid.isActiveAndEnabled || !slot
            || (_tabController && grid.ItemType != _tabController.CurrentItemType)) return;

        // 보유 항목의 인덱스와 현재 Definition을 함께 검증해 다른 복사본이나 오래된 입력을 구분한다.
        _playerManager.TryToggleEquipment(slot.InventoryIndex, slot.Item);
    }

    private void HandleEquipmentChanged(EquipmentSlot equipmentSlot)
    {
        // 장착은 목록을 다시 대여하지 않고 표시만 바꿔 포커스와 현재 선택을 유지한다.
        RefreshEquipmentState();
    }

    private void RefreshEquipmentState()
    {
        for (int i = 0; i < (_grids?.Length ?? 0); i++)
        {
            InventoryGridView grid = _grids[i];
            if (grid && grid.isActiveAndEnabled && grid.ItemType == ItemType.Equipment)
            {
                grid.RefreshEquipmentIndicators(_playerManager);
            }
        }

        RefreshEquipmentPanel();
    }

    private void RefreshEquipmentPanel()
    {
        if (_equipmentPanelView)
        {
            ItemDefinition item = _selectedItemSlot ? _selectedItemSlot.Item : null;
            int inventoryIndex = _selectedItemSlot ? _selectedItemSlot.InventoryIndex : -1;
            _equipmentPanelView.Refresh(_playerManager, item, inventoryIndex);
        }
    }

    private int GetConsumableCount(ItemDefinition item)
    {
        // 그리드의 소모품 슬롯 수와 관계없이 Manager가 관리하는 실제 보유 수량을 표시한다.
        return _playerManager ? _playerManager.GetConsumableCount(item) : 0;
    }

    private void ClearSelection()
    {
        _selectedItemSlot = null;
        for (int i = 0; i < (_grids?.Length ?? 0); i++)
        {
            if (_grids[i])
            {
                _grids[i].ClearSelection();
            }
        }

        if (_descriptionView)
        {
            _descriptionView.Hide();
        }

        RefreshEquipmentPanel();
    }
}
