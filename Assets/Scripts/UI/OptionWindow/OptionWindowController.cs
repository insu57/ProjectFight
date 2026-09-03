using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
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
    [SerializeField] private InputActionReference _cancelAction;
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

    private bool _enabledCancelAction;

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
        SubscribeCancelAction();
    }

    protected override void OnTabbedWindowDisabled()
    {
        UnsubscribeExitButton();
        UnsubscribeCancelAction();
    }

    private void SubscribeCancelAction()
    {
        if (!_cancelAction)
        {
            return;
        }

        InputAction cancelAction = _cancelAction.action;
        cancelAction.performed += HandleCancel;

        if (!cancelAction.enabled)
        {
            cancelAction.Enable();
            _enabledCancelAction = true;
        }
    }

    private void UnsubscribeCancelAction()
    {
        if (!_cancelAction)
        {
            return;
        }

        InputAction cancelAction = _cancelAction.action;
        cancelAction.performed -= HandleCancel;

        if (_enabledCancelAction)
        {
            cancelAction.Disable();
            _enabledCancelAction = false;
        }
    }

    private void HandleCancel(InputAction.CallbackContext context)
    {
        if (IsOpen)
        {
            CloseWindow();
            return;
        }

        if (_closedCancelAction == OptionWindowClosedCancelAction.OpenWindow)
        {
            OpenWindow();
            return;
        }

        // 타이틀에서는 현재 메뉴가 처리할 수 있도록 뒤로 가기를 요청한다.
        _backRequested?.Invoke();
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
