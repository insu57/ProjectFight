using System;
using UnityEngine;
using UnityEngine.EventSystems;

public enum PlayerTabType
{
    SkillTree,
    Inventory
}

[DisallowMultipleComponent]
public sealed class PlayerWindowController : TabbedWindowController<PlayerTabType>, ICancelHandler
{
    [Serializable]
    private sealed class TabBinding : IWindowTabBinding<PlayerTabType>
    {
        [SerializeField] private PlayerTabType _tabType;
        [SerializeField] private WindowTabButton _button;
        [SerializeField] private GameObject _content;

        public PlayerTabType TabType => _tabType;
        public WindowTabButton Button => _button;
        public GameObject Content => _content;
    }

    [Header("Tabs")]
    [SerializeField] private PlayerTabType _initialTab = PlayerTabType.Inventory;
    [SerializeField] private TabBinding[] _tabBindings = Array.Empty<TabBinding>();

    protected override bool PauseGameplayWhileOpen => true;
    protected override PlayerTabType InitialTab => _initialTab;
    protected override int TabBindingCount => _tabBindings?.Length ?? 0;

    public void OnCancel(BaseEventData eventData)
    {
        if (IsOpen)
        {
            CloseWindow();
        }
    }

    protected override IWindowTabBinding<PlayerTabType> GetTabBinding(int index)
    {
        return _tabBindings[index];
    }

    protected override void OnTabbedWindowEnabled()
    {
        if (PlayerManager)
        {
            PlayerManager.PlayerWindowRequested += ToggleWindow;
        }
    }

    protected override void OnTabbedWindowDisabled()
    {
        if (PlayerManager)
        {
            PlayerManager.PlayerWindowRequested -= ToggleWindow;
        }
    }
}
