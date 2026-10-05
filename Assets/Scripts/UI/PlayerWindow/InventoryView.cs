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

    private void OnEnable()
    {
        _slotsChangedHandler ??= HandleSlotsChanged;

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

        ClearSelection();
        ConfigureNavigationTargets();
        RefreshTabGridNavigation(true);
    }

    private void OnDisable()
    {
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

    private void HandleItemTypeChanged(ItemType itemType)
    {
        // 공통 하단 설명에 이전 타입의 선택이 남지 않도록 함께 초기화한다.
        ClearSelection();
        ConfigureNavigationTargets();
        RefreshTabGridNavigation(true);
    }

    private void HandleSlotsChanged(InventoryGridView grid)
    {
        RefreshTabGridNavigation(true);
    }

    private void RefreshTabGridNavigation(bool refreshNavigation)
    {
        if (!isActiveAndEnabled || !_tabController)
        {
            return;
        }

        Button target = null;

        for (int i = 0; i < (_grids?.Length ?? 0); i++)
        {
            InventoryGridView grid = _grids[i];

            if (grid && grid.isActiveAndEnabled && grid.ItemType == _tabController.CurrentItemType)
            {
                target = grid.GetNavigationEntryPoint();
                break;
            }
        }

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
        RefreshTabGridNavigation(false);

        if (!_descriptionView)
        {
            return;
        }

        if (!item)
        {
            _descriptionView.Clear();
            return;
        }

        if (grid && grid.isActiveAndEnabled && (!_tabController || grid.ItemType == _tabController.CurrentItemType))
        {
            _descriptionView.Show(item);
        }
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
