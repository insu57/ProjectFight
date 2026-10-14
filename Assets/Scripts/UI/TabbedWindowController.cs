using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public interface IWindowTabBinding<TTab>
{
    public TTab TabType { get; }
    public WindowTabButton Button { get; }
    public GameObject Content { get; }
}

public abstract class TabbedWindowController<TTab> : WindowControllerBase where TTab : struct, Enum
{
    private TTab _currentTab;
    private bool _hasSelectedTab;
    private IWindowTabBinding<TTab> _selectedBinding;

    protected abstract TTab InitialTab { get; }
    protected abstract int TabBindingCount { get; }

    public TTab CurrentTab => _currentTab;

    public event Action<TTab> TabChanged;

    protected sealed override void OnWindowControllerEnabled()
    {
        SubscribeTabButtons();
        SelectTab(_hasSelectedTab ? _currentTab : InitialTab);
        OnTabbedWindowEnabled();
    }

    protected sealed override void OnWindowControllerDisabled()
    {
        OnTabbedWindowDisabled();
        UnsubscribeTabButtons();
    }

    public void SelectTab(TTab tabType)
    {
        for (int i = 0; i < TabBindingCount; i++)
        {
            IWindowTabBinding<TTab> binding = GetTabBinding(i);

            if (binding != null && binding.Button && EqualityComparer<TTab>.Default.Equals(binding.TabType, tabType))
            {
                SelectBinding(binding);
                return;
            }
        }

        Debug.LogWarning($"등록되지 않은 창 탭입니다: {tabType}", this);
    }

    public void SelectAdjacentTab(int direction)
    {
        int count = TabBindingCount;
        if (!isActiveAndEnabled || !IsOpen || direction == 0 || count == 0) return;

        int currentIndex = -1;
        for (int i = 0; i < count; i++)
        {
            if (ReferenceEquals(GetTabBinding(i), _selectedBinding))
            {
                currentIndex = i;
                break;
            }
        }

        int step = direction > 0 ? 1 : -1;
        if (currentIndex < 0) currentIndex = step > 0 ? count - 1 : 0;
        // enum 값 대신 Inspector 등록 순서로 순환하고 사용할 수 없는 탭은 건너뛴다.
        for (int offset = 1; offset <= count; offset++)
        {
            IWindowTabBinding<TTab> binding = GetTabBinding((currentIndex + step * offset + count) % count);
            if (binding == null || !binding.Content || !binding.Button || !binding.Button.isActiveAndEnabled
                || !binding.Button.Button || !binding.Button.Button.IsActive() || !binding.Button.Button.IsInteractable()) continue;

            SelectBinding(binding);
            // 하위 콘텐츠의 선택 위치와 관계없이 전환한 상위 탭 버튼으로 실제 포커스를 옮긴다.
            if (IsOpen && EventSystem.current && binding.Button) EventSystem.current.SetSelectedGameObject(binding.Button.gameObject);
            return;
        }
    }

    protected override void FocusWindow()
    {
        if (!EventSystem.current)
        {
            return;
        }

        for (int i = 0; i < TabBindingCount; i++)
        {
            IWindowTabBinding<TTab> binding = GetTabBinding(i);

            if (binding != null && binding.Button && EqualityComparer<TTab>.Default.Equals(binding.TabType, _currentTab))
            {
                EventSystem.current.SetSelectedGameObject(binding.Button.gameObject);
                return;
            }
        }
    }

    protected abstract IWindowTabBinding<TTab> GetTabBinding(int index);

    protected virtual void OnTabbedWindowEnabled()
    {
    }

    protected virtual void OnTabbedWindowDisabled()
    {
    }

    private void SelectBinding(IWindowTabBinding<TTab> selectedBinding)
    {
        // 같은 enum 값이 여러 바인딩에 있어도 실제 선택한 바인딩의 위치를 기억한다.
        _selectedBinding = selectedBinding;
        for (int i = 0; i < TabBindingCount; i++)
        {
            IWindowTabBinding<TTab> binding = GetTabBinding(i);

            if (binding == null || !binding.Button)
            {
                continue;
            }

            bool isSelected = ReferenceEquals(binding, selectedBinding);
            binding.Button.SetSelected(isSelected);

            if (binding.Content && binding.Content.activeSelf != isSelected)
            {
                binding.Content.SetActive(isSelected);
            }
        }

        TTab selectedTab = selectedBinding.TabType;

        if (_hasSelectedTab && EqualityComparer<TTab>.Default.Equals(_currentTab, selectedTab))
        {
            return;
        }

        _currentTab = selectedTab;
        _hasSelectedTab = true;
        TabChanged?.Invoke(_currentTab);
    }

    private void SubscribeTabButtons()
    {
        for (int i = 0; i < TabBindingCount; i++)
        {
            WindowTabButton button = GetTabBinding(i)?.Button;

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
            WindowTabButton button = GetTabBinding(i)?.Button;

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
            IWindowTabBinding<TTab> binding = GetTabBinding(i);

            if (binding != null && binding.Button == selectedButton)
            {
                SelectBinding(binding);
                return;
            }
        }
    }
}
