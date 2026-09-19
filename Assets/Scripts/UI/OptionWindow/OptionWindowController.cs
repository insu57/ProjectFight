using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum OptionWindowClosedCancelAction
{
    OpenWindow,
    RequestBack
}

public enum OptionTabType
{
    Audio,
    Graphics
}

[DisallowMultipleComponent]
public sealed class OptionWindowController : TabbedWindowController<OptionTabType>
{
    [Serializable]
    private sealed class TabBinding : IWindowTabBinding<OptionTabType>
    {
        [SerializeField] private OptionTabType _tabType;
        [SerializeField] private WindowTabButton _button;
        [SerializeField] private GameObject _content;

        public OptionTabType TabType => _tabType;
        public WindowTabButton Button => _button;
        public GameObject Content => _content;
    }

    [Header("Cancel")]
    [SerializeField] private OptionWindowClosedCancelAction _closedCancelAction;

    [Header("Tabs")]
    [SerializeField] private OptionTabType _initialTab = OptionTabType.Audio;
    [SerializeField] private TabBinding[] _tabBindings = Array.Empty<TabBinding>();

    [Header("Exit")]
    [SerializeField] private Button _exitButton;

    [Header("Stage")]
    [SerializeField] private bool _pauseGameplayWhileOpen;

    [Header("Title")]
    [SerializeField] private UnityEvent _backRequested;

    protected override bool PauseGameplayWhileOpen => _pauseGameplayWhileOpen;
    protected override OptionTabType InitialTab => _initialTab;
    protected override int TabBindingCount => _tabBindings?.Length ?? 0;

    protected override IWindowTabBinding<OptionTabType> GetTabBinding(int index)
    {
        return _tabBindings[index];
    }

    protected override void OnTabbedWindowEnabled()
    {
        SubscribeExitButton();
    }

    protected override void OnTabbedWindowDisabled()
    {
        UnsubscribeExitButton();
    }

    public void RequestBackIfConfigured()
    {
        if (!IsOpen && _closedCancelAction == OptionWindowClosedCancelAction.RequestBack)
        {
            // 플레이어가 없는 타이틀에서는 기존 뒤로 가기 이벤트를 전달한다.
            _backRequested?.Invoke();
        }
    }

    private void SubscribeExitButton()
    {
        if (_exitButton)
        {
            _exitButton.onClick.AddListener(ExitGame);
        }
    }

    private void UnsubscribeExitButton()
    {
        if (_exitButton)
        {
            _exitButton.onClick.RemoveListener(ExitGame);
        }
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
