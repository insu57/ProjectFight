using System;
using UnityEngine;

public enum PlayerTabType
{
    SkillTree,
    Inventory,
    Map // 전체 지도 탭
}

[DisallowMultipleComponent]
public sealed class PlayerWindowController : TabbedWindowController<PlayerTabType>
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

    public void ShowTab(PlayerTabType tabType)
    {
        SelectTab(tabType);

        if (IsOpen)
        {
            // 창이 열린 상태에서 단축키로 전환하면 UI 탐색 포커스도 해당 탭에 맞춘다.
            FocusWindow();
            return;
        }

        OpenWindow();
    }

    protected override IWindowTabBinding<PlayerTabType> GetTabBinding(int index)
    {
        return _tabBindings[index];
    }
}
