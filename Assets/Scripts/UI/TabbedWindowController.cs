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
