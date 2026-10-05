using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryTabController : MonoBehaviour
{
    [Serializable]
    private sealed class TabBinding : IWindowTabBinding<ItemType>
    {
        [SerializeField] private ItemType _itemType;
        [SerializeField] private WindowTabButton _button;
        [SerializeField] private GameObject _content;

        private Navigation _originalNavigation;
        private bool _hasNavigationOverride;

        public ItemType TabType => _itemType;
        public WindowTabButton Button => _button;
        public GameObject Content => _content;

        public void SetGridNavigationTarget(Selectable target, bool refreshNavigation)
        {
            if (!_button || !_button.TryGetComponent(out Button button))
            {
                return;
            }

            bool shouldRefresh = !_hasNavigationOverride || refreshNavigation;

            if (!_hasNavigationOverride)
            {
                _originalNavigation = button.navigation;
                _hasNavigationOverride = true;
            }

            Navigation navigation = button.navigation;

            if (shouldRefresh)
            {
                // Automatic은 한 방향만 지정할 수 없어 나머지 방향의 기존 탐색 결과를 보존한다.
                button.navigation = _originalNavigation;
                navigation = _originalNavigation;

                if (navigation.mode != Navigation.Mode.Explicit)
                {
                    navigation.selectOnUp = button.FindSelectableOnUp();
                    navigation.selectOnDown = button.FindSelectableOnDown();
                    navigation.selectOnRight = button.FindSelectableOnRight();
                }
            }

            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnLeft = target;
            button.navigation = navigation;
        }

        public void RestoreNavigation()
        {
            if (!_hasNavigationOverride)
            {
                return;
            }

            if (_button && _button.TryGetComponent(out Button button))
            {
                button.navigation = _originalNavigation;
            }

            _hasNavigationOverride = false;
        }
    }

    [Header("Inventory Tabs")]
    [SerializeField] private ItemType _initialItemType = ItemType.Equipment;
    [SerializeField] private TabBinding[] _tabBindings = Array.Empty<TabBinding>();

    private ItemType _currentItemType;
    private bool _hasSelectedItemType;

    private int TabBindingCount => _tabBindings?.Length ?? 0;
    public ItemType CurrentItemType => _hasSelectedItemType ? _currentItemType : _initialItemType;

    public event Action<ItemType> ItemTypeChanged;

    public Button GetTabButton(ItemType itemType)
    {
        for (int i = 0; i < TabBindingCount; i++)
        {
            TabBinding binding = _tabBindings[i];

            if (binding != null && binding.TabType == itemType && binding.Button && binding.Content && binding.Button.TryGetComponent(out Button button))
            {
                return button;
            }
        }

        return null;
    }

    private void OnEnable()
    {
        SubscribeTabButtons();
        // 상위 인벤토리 탭이나 플레이어 창을 다시 열어도 마지막 타입을 유지한다.
        SelectItemType(CurrentItemType);
    }

    private void OnDisable()
    {
        UnsubscribeTabButtons();
        ClearGridNavigationTarget();
    }

    public void SetGridNavigationTarget(Selectable target, bool refreshNavigation)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        // 어떤 타입 탭에 포커스가 있어도 현재 표시 중인 그리드로 복귀한다.
        for (int i = 0; i < TabBindingCount; i++)
        {
            _tabBindings[i]?.SetGridNavigationTarget(target, refreshNavigation);
        }
    }

    public void ClearGridNavigationTarget()
    {
        for (int i = 0; i < TabBindingCount; i++)
        {
            _tabBindings[i]?.RestoreNavigation();
        }
    }

    public void SelectItemType(ItemType itemType)
    {
        for (int i = 0; i < TabBindingCount; i++)
        {
            TabBinding binding = _tabBindings[i];

            if (binding != null && binding.Button && binding.Content && binding.TabType == itemType)
            {
                SelectBinding(binding);
                return;
            }
        }

        Debug.LogWarning($"등록되지 않은 인벤토리 탭입니다: {itemType}", this);
    }

    private void SelectBinding(TabBinding selectedBinding)
    {
        for (int i = 0; i < TabBindingCount; i++)
        {
            TabBinding binding = _tabBindings[i];

            if (binding == null)
            {
                continue;
            }

            // Inspector의 타입 값이 중복되더라도 실제 선택한 연결 하나만 표시한다.
            bool isSelected = ReferenceEquals(binding, selectedBinding);

            if (binding.Button)
            {
                binding.Button.SetSelected(isSelected);
            }

            if (binding.Content && binding.Content.activeSelf != isSelected)
            {
                binding.Content.SetActive(isSelected);
            }
        }

        ItemType selectedType = selectedBinding.TabType;
        bool hasChanged = !_hasSelectedItemType || _currentItemType != selectedType;
        _currentItemType = selectedType;
        _hasSelectedItemType = true;

        if (hasChanged)
        {
            // 공통 설명 영역도 현재 아이템 타입에 맞춰 선택 상태를 갱신한다.
            ItemTypeChanged?.Invoke(_currentItemType);
        }
    }

    private void SubscribeTabButtons()
    {
        for (int i = 0; i < TabBindingCount; i++)
        {
            WindowTabButton button = _tabBindings[i]?.Button;

            if (button)
            {
                button.Selected += HandleTabSelected;
            }
        }
    }

    private void UnsubscribeTabButtons()
    {
        for (int i = 0; i < TabBindingCount; i++)
        {
            WindowTabButton button = _tabBindings[i]?.Button;

            if (button)
            {
                button.Selected -= HandleTabSelected;
            }
        }
    }

    private void HandleTabSelected(WindowTabButton selectedButton)
    {
        for (int i = 0; i < TabBindingCount; i++)
        {
            TabBinding binding = _tabBindings[i];

            if (binding != null && binding.Button == selectedButton && binding.Content)
            {
                SelectBinding(binding);
                return;
            }
        }
    }
}
