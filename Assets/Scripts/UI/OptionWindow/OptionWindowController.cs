using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
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
public sealed class OptionWindowController : MonoBehaviour
{
    [Serializable]
    private sealed class TabBinding
    {
        [SerializeField] private OptionTabType _tabType;
        [SerializeField] private WindowTabButton _button;
        [SerializeField] private GameObject _content;

        public OptionTabType TabType => _tabType;
        public WindowTabButton Button => _button;
        public GameObject Content => _content;
    }

    [Header("Window")]
    [SerializeField] private GameObject _windowPanel;

    [Header("Cancel")]
    [SerializeField] private InputActionReference _cancelAction;
    [SerializeField] private OptionWindowClosedCancelAction _closedCancelAction;

    [Header("Navigation")]
    [SerializeField] private InputActionReference _navigateAction;

    [Header("Tabs")]
    [SerializeField] private OptionTabType _initialTab = OptionTabType.Audio;
    [SerializeField] private TabBinding[] _tabBindings;

    [Header("Exit")]
    [SerializeField] private Button _exitButton;

    [Header("Stage")]
    [SerializeField] private bool _pauseGameplayWhileOpen;
    private PlayerManager _playerManager;

    [Header("Title")]
    [SerializeField] private UnityEvent _backRequested;

    private float _timeScaleBeforeOpen = 1f;
    private bool _isTimeScalePaused;
    private bool _enabledCancelAction;
    private OptionTabType _currentTab;
    private bool _hasSelectedTab;

    public bool IsOpen => _windowPanel && _windowPanel.activeSelf;
    public OptionTabType CurrentTab => _currentTab;

    public event Action<OptionTabType> TabChanged;

    private void Awake()
    {
        if (_windowPanel)
        {
            _windowPanel.SetActive(false);
        }

        _playerManager = FindFirstObjectByType<PlayerManager>();
    }

    private void OnEnable()
    {
        SubscribeTabButtons();
        SubscribeExitButton();
        SelectTab(_hasSelectedTab ? _currentTab : _initialTab);

        if (_navigateAction)
        {
            _navigateAction.action.performed += HandleNavigate;
        }

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

    private void OnDisable()
    {
        UnsubscribeTabButtons();
        UnsubscribeExitButton();

        if (_navigateAction)
        {
            _navigateAction.action.performed -= HandleNavigate;
        }

        if (_cancelAction)
        {
            InputAction cancelAction = _cancelAction.action;
            cancelAction.performed -= HandleCancel;

            if (_enabledCancelAction)
            {
                cancelAction.Disable();
                _enabledCancelAction = false;
            }
        }

        if (IsOpen)
        {
            SetWindowOpen(false);
        }
    }

    public void OpenWindow()
    {
        SetWindowOpen(true);
    }

    public void CloseWindow()
    {
        SetWindowOpen(false);
    }

    public void SelectTab(OptionTabType tabType)
    {
        for (int i = 0; i < _tabBindings.Length; i++)
        {
            TabBinding binding = _tabBindings[i];

            if (binding != null && binding.Button && binding.TabType == tabType)
            {
                SelectBinding(binding);
                return;
            }
        }

        Debug.LogWarning($"등록되지 않은 옵션 창 탭입니다: {tabType}", this);
    }

    private void SelectBinding(TabBinding selectedBinding)
    {
        for (int i = 0; i < _tabBindings.Length; i++)
        {
            TabBinding binding = _tabBindings[i];

            if (binding == null || !binding.Button)
            {
                continue;
            }

            bool isSelected = binding == selectedBinding;
            binding.Button.SetSelected(isSelected);

            if (binding.Content && binding.Content.activeSelf != isSelected)
            {
                binding.Content.SetActive(isSelected);
            }
        }

        OptionTabType selectedTab = selectedBinding.TabType;

        if (_hasSelectedTab && _currentTab == selectedTab)
        {
            return;
        }

        _currentTab = selectedTab;
        _hasSelectedTab = true;
        TabChanged?.Invoke(_currentTab);
    }

    public void SetWindowOpen(bool isOpen)
    {
        if (!_windowPanel || IsOpen == isOpen)
        {
            return;
        }

        _windowPanel.SetActive(isOpen);

        if (isOpen)
        {
            BeginWindowState();
            FocusCurrentTab();
            return;
        }

        EndWindowState();
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

        // 타이틀에서는 현재 메뉴가 처리할 뒤로가기 동작을 요청한다.
        _backRequested?.Invoke();
    }

    private void HandleNavigate(InputAction.CallbackContext context)
    {
        if (!IsOpen || !EventSystem.current || EventSystem.current.currentSelectedGameObject)
        {
            return;
        }

        // 배경 클릭으로 선택이 사라졌다면 현재 탭 버튼부터 탐색을 다시 시작한다.
        FocusCurrentTab();
    }

    private void SubscribeTabButtons()
    {
        for (int i = 0; i < _tabBindings.Length; i++)
        {
            WindowTabButton button = _tabBindings[i]?.Button;

            if (button)
            {
                // 탭 버튼과 콘텐츠 사이의 호출은 옵션 창 컨트롤러가 중개한다.
                button.Selected += HandleTabSelected;
            }
        }
    }

    private void UnsubscribeTabButtons()
    {
        for (int i = 0; i < _tabBindings.Length; i++)
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
        for (int i = 0; i < _tabBindings.Length; i++)
        {
            TabBinding binding = _tabBindings[i];

            if (binding != null && binding.Button == selectedButton)
            {
                SelectBinding(binding);
                return;
            }
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
        // 에디터에서는 Application.Quit()이 동작하지 않으므로 플레이 모드를 종료한다.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BeginWindowState()
    {
        if (!_pauseGameplayWhileOpen)
        {
            return;
        }

        _playerManager?.SetGameplayInputEnabled(false);
        _timeScaleBeforeOpen = Time.timeScale;
        Time.timeScale = 0f;
        _isTimeScalePaused = true;
    }

    private void EndWindowState()
    {
        EventSystem.current?.SetSelectedGameObject(null);

        if (!_pauseGameplayWhileOpen)
        {
            return;
        }

        _playerManager?.SetGameplayInputEnabled(true);
        RestoreTimeScale();
    }

    private void FocusCurrentTab()
    {
        if (!EventSystem.current)
        {
            return;
        }

        for (int i = 0; i < _tabBindings.Length; i++)
        {
            TabBinding binding = _tabBindings[i];

            if (binding != null && binding.Button && binding.TabType == _currentTab)
            {
                EventSystem.current.SetSelectedGameObject(binding.Button.gameObject);
                return;
            }
        }
    }

    private void RestoreTimeScale()
    {
        if (!_isTimeScalePaused)
        {
            return;
        }

        Time.timeScale = _timeScaleBeforeOpen;
        _isTimeScalePaused = false;
    }
}
