using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryView : MonoBehaviour
{
    [SerializeField] private InventoryTabController _tabController;
    [SerializeField] private InventoryGridView[] _grids = Array.Empty<InventoryGridView>();
    [SerializeField] private ItemDescriptionView _descriptionView;

    [Header("슬롯 탐색")]
    [SerializeField] private Button _playerWindowInventoryTabButton;

    private Action<InventoryGridView> _slotsChangedHandler;
    private Action<ItemType> _inventoryChangedHandler;
    private PlayerManager _playerManager;

    private void Awake()
    {
        // 기존 플레이어 창의 다른 View와 동일하게 플레이어 참조를 한 번만 찾는다.
        _playerManager = FindFirstObjectByType<PlayerManager>();
    }

    private void OnEnable()
    {
        _slotsChangedHandler ??= HandleSlotsChanged;
        _inventoryChangedHandler ??= RefreshInventory;

        if (_tabController)
        {
            _tabController.ItemTypeChanged += HandleItemTypeChanged;
        }

        for (int i = 0; i < (_grids?.Length ?? 0); i++)
        {
            if (_grids[i])
            {
                _grids[i].ItemSelected += HandleItemSelected;
                _grids[i].SlotsChanged += _slotsChangedHandler;
            }
        }

        ConfigureNavigationTargets();

        if (_playerManager)
        {
            _playerManager.InventoryChanged += _inventoryChangedHandler;
        }

        // 창이 닫힌 동안 획득한 아이템도 재개방 시 최신 목록으로 표시한다.
        RefreshAllInventory();
        RefreshTabGridNavigation(GetCurrentGrid(), true);
    }

    private void OnDisable()
    {
        if (_playerManager)
        {
            _playerManager.InventoryChanged -= _inventoryChangedHandler;
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
        // 공통 하단 설명에 이전 타입의 선택이 남지 않도록 함께 초기화한다.
        ClearSelection();
        ConfigureNavigationTargets();
        RefreshTabGridNavigation(GetCurrentGrid(), true);
    }

    private void HandleSlotsChanged(InventoryGridView grid)
    {
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

        if (!_descriptionView)
        {
            return;
        }

        if (!item)
        {
            _descriptionView.Clear();
            return;
        }

        _descriptionView.Show(item);
    }

    private void ClearSelection()
    {
        for (int i = 0; i < (_grids?.Length ?? 0); i++)
        {
            if (_grids[i])
            {
                _grids[i].ClearSelection();
            }
        }

        if (_descriptionView)
        {
            _descriptionView.Clear();
        }
    }
}
