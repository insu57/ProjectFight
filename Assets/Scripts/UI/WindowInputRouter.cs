using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class WindowInputRouter : MonoBehaviour
{
    [Header("References")]
    private PlayerManager _playerManager;
    private PlayerWindowController _playerWindow;
    private OptionWindowController _optionWindow;
    private SkillUnlockConfirmationView _skillConfirmation;

    [Header("Input")]
    [SerializeField] private InputActionReference _cancelAction;

    private bool _enabledCancelAction;
    private bool _cancelRequested;
    private bool _playerWindowRequested;
    private bool _optionWindowRequested;

    private void Awake()
    {
        _playerManager = FindFirstObjectByType<PlayerManager>();
        _playerWindow = GetComponentInChildren<PlayerWindowController>();
        _optionWindow = GetComponentInChildren<OptionWindowController>();
        _skillConfirmation = GetComponent<SkillUnlockConfirmationView>();
    }

    private void OnEnable()
    {
        if (_playerManager)
        {
            _playerManager.PlayerWindowRequested += RequestPlayerWindow;
            _playerManager.OptionWindowRequested += RequestOptionWindow;
        }

        if (_cancelAction && _cancelAction.action != null)
        {
            InputAction action = _cancelAction.action;
            action.performed += HandleCancelInput;

            // 직접 활성화한 경우에만 비활성화 책임을 가진다.
            _enabledCancelAction = !action.enabled;

            if (_enabledCancelAction)
            {
                action.Enable();
            }
        }
    }

    private void OnDisable()
    {
        if (_playerManager)
        {
            _playerManager.PlayerWindowRequested -= RequestPlayerWindow;
            _playerManager.OptionWindowRequested -= RequestOptionWindow;
        }

        if (_cancelAction && _cancelAction.action != null)
        {
            InputAction action = _cancelAction.action;
            action.performed -= HandleCancelInput;

            if (_enabledCancelAction)
            {
                action.Disable();
            }
        }

        _enabledCancelAction = false;
        ClearRequests();
    }

    private void HandleCancelInput(InputAction.CallbackContext context)
    {
        _cancelRequested = true;
    }

    private void RequestPlayerWindow()
    {
        _playerWindowRequested = true;
    }

    private void RequestOptionWindow()
    {
        _optionWindowRequested = true;
    }

    private void LateUpdate()
    {
        if (!_cancelRequested && !_playerWindowRequested && !_optionWindowRequested)
        {
            return;
        }

        bool cancelRequested = _cancelRequested;
        bool playerWindowRequested = _playerWindowRequested;
        bool optionWindowRequested = _optionWindowRequested;
        ClearRequests();

        // 같은 프레임의 Cancel과 창 열기 요청 중 취소 처리를 우선한다.
        if (cancelRequested && TryCancelTopWindow())
        {
            return;
        }

        // 확인창이 떠 있는 동안에는 다른 창 단축키를 처리하지 않는다.
        if (_skillConfirmation && _skillConfirmation.IsOpen)
        {
            return;
        }

        if (optionWindowRequested)
        {
            if (_optionWindow && (!_playerWindow || !_playerWindow.IsOpen))
            {
                _optionWindow.ToggleWindow();
            }

            return;
        }

        if (playerWindowRequested)
        {
            if (_playerWindow && (!_optionWindow || !_optionWindow.IsOpen))
            {
                _playerWindow.ToggleWindow();
            }

            return;
        }

        // 플레이어가 없는 타이틀에서는 기존 뒤로 가기 동작을 유지한다.
        if (cancelRequested && !_playerManager && _optionWindow)
        {
            _optionWindow.RequestBackIfConfigured();
        }
    }

    private bool TryCancelTopWindow()
    {
        if (_skillConfirmation && _skillConfirmation.IsOpen)
        {
            _skillConfirmation.Cancel();
            return true;
        }

        if (_optionWindow && _optionWindow.IsOpen)
        {
            _optionWindow.CloseWindow();
            return true;
        }

        if (_playerWindow && _playerWindow.IsOpen)
        {
            _playerWindow.CloseWindow();
            return true;
        }

        return false;
    }

    private void ClearRequests()
    {
        _cancelRequested = false;
        _playerWindowRequested = false;
        _optionWindowRequested = false;
    }
}
