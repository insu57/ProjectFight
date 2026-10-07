using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class WindowInputRouter : MonoBehaviour
{
    [Header("References")]
    private StageManager _stageManager;
    private StoreWindowController _storeWindow;
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
    private bool _storeWindowRequested;
    private Store _pendingStore;
    private PlayerManager _pendingStorePlayer;
    private Action<Store, PlayerManager> _storeInteractionHandler;

    private void Awake()
    {
        _playerManager = FindFirstObjectByType<PlayerManager>();
        _stageManager = FindFirstObjectByType<StageManager>();
        _playerWindow = GetComponentInChildren<PlayerWindowController>();
        _optionWindow = GetComponentInChildren<OptionWindowController>();
        _storeWindow = GetComponentInChildren<StoreWindowController>();
        _skillConfirmation = GetComponentInChildren<SkillUnlockConfirmationView>();
    }

    private void OnEnable()
    {
        if (_stageManager)
        {
            _storeInteractionHandler ??= RequestStoreWindow;
            _stageManager.StoreInteractionRequested += _storeInteractionHandler;
        }

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
        if (_stageManager)
        {
            _stageManager.StoreInteractionRequested -= _storeInteractionHandler;
        }

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

        // Cancel을 받을 라우터가 비활성화되면 상점창의 입력 차단도 함께 해제한다.
        if (_storeWindow && _storeWindow.IsOpen)
        {
            _storeWindow.CloseWindow();
        }
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

    private void RequestStoreWindow(Store store, PlayerManager player)
    {
        if (!isActiveAndEnabled || !_storeWindow || !_storeWindow.isActiveAndEnabled || !store || !store.isActiveAndEnabled
            || !player || !player.isActiveAndEnabled || player != _playerManager)
        {
            return;
        }

        // 상점 요청도 Cancel 및 단축키와 함께 프레임당 한 번만 처리한다.
        _pendingStore = store;
        _pendingStorePlayer = player;
        _storeWindowRequested = true;
    }

    private void LateUpdate()
    {
        if (!_cancelRequested && !_playerWindowRequested && !_optionWindowRequested && !_storeWindowRequested)
        {
            return;
        }

        bool cancelRequested = _cancelRequested;
        bool playerWindowRequested = _playerWindowRequested;
        bool optionWindowRequested = _optionWindowRequested;
        bool storeWindowRequested = _storeWindowRequested;
        Store pendingStore = _pendingStore;
        PlayerManager pendingStorePlayer = _pendingStorePlayer;
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

        // 상점창이 입력과 일시정지를 소유하는 동안 다른 창은 열지 않는다.
        if (_storeWindow && _storeWindow.IsOpen)
        {
            return;
        }

        if (storeWindowRequested)
        {
            // 닫힌 상태에서 동시에 들어온 Cancel도 상점 열기보다 우선한다.
            if (!cancelRequested && _storeWindow && (!_playerWindow || !_playerWindow.IsOpen) && (!_optionWindow || !_optionWindow.IsOpen))
            {
                _storeWindow.OpenStore(pendingStore, pendingStorePlayer);
            }

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

        if (_storeWindow && _storeWindow.IsOpen)
        {
            _storeWindow.CloseWindow();
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
        _storeWindowRequested = false;
        _pendingStore = null;
        _pendingStorePlayer = null;
    }
}
