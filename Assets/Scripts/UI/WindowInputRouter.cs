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
    [SerializeField] private QuickslotRegistrationView _quickslotRegistration;

    [Header("Input")]
    [SerializeField] private InputActionReference _cancelAction;
    [SerializeField] private InputActionReference _tabLeftAction;
    [SerializeField] private InputActionReference _tabRightAction;

    private InputAction _cancelInputAction;
    private InputAction _tabLeftInputAction;
    private InputAction _tabRightInputAction;
    private bool _isInitialized;
    private bool _enabledCancelAction;
    private bool _enabledTabLeftAction;
    private bool _enabledTabRightAction;
    private bool _cancelRequested;
    private bool _tabLeftRequested;
    private bool _tabRightRequested;
    private bool _playerWindowRequested;
    private bool _playerTabRequested;
    private PlayerTabType _requestedPlayerTab;
    private bool _optionWindowRequested;
    private bool _storeWindowRequested;
    private bool _mapTeleportCompleted;
    private Store _pendingStore;
    private PlayerManager _pendingStorePlayer;
    private Action<Store, PlayerManager> _storeInteractionHandler;
    private Action<InputAction.CallbackContext> _tabInputHandler;

    private void Awake()
    {
        _playerManager = FindFirstObjectByType<PlayerManager>();
        _stageManager = FindFirstObjectByType<StageManager>();
        _playerWindow = GetComponentInChildren<PlayerWindowController>(true);
        _optionWindow = GetComponentInChildren<OptionWindowController>(true);
        _storeWindow = GetComponentInChildren<StoreWindowController>(true);
        _skillConfirmation = GetComponentInChildren<SkillUnlockConfirmationView>(true);

        bool hasRequiredReferences = true;
        hasRequiredReferences &= ValidateRequiredReference(_playerManager, nameof(_playerManager));
        hasRequiredReferences &= ValidateRequiredReference(_stageManager, nameof(_stageManager));
        hasRequiredReferences &= ValidateRequiredReference(_playerWindow, nameof(_playerWindow));
        hasRequiredReferences &= ValidateRequiredReference(_optionWindow, nameof(_optionWindow));
        hasRequiredReferences &= ValidateRequiredReference(_storeWindow, nameof(_storeWindow));
        hasRequiredReferences &= ValidateRequiredReference(_skillConfirmation, nameof(_skillConfirmation));
        hasRequiredReferences &= ValidateRequiredReference(_quickslotRegistration, nameof(_quickslotRegistration));
        hasRequiredReferences &= ValidateRequiredAction(_cancelAction, nameof(_cancelAction), out _cancelInputAction);
        hasRequiredReferences &= ValidateRequiredAction(_tabLeftAction, nameof(_tabLeftAction), out _tabLeftInputAction);
        hasRequiredReferences &= ValidateRequiredAction(_tabRightAction, nameof(_tabRightAction), out _tabRightInputAction);

        if (!hasRequiredReferences)
        {
            // 필수 참조가 빠진 라우터는 입력을 구독하지 않도록 초기화 단계에서 멈춘다.
            enabled = false;
            return;
        }

        _tabInputHandler = HandleTabInput;
        _storeInteractionHandler = RequestStoreWindow;
        _isInitialized = true;
    }

    private bool ValidateRequiredReference(UnityEngine.Object reference, string referenceName)
    {
        if (reference) return true;

        Debug.LogError($"WindowInputRouter 필수 참조가 없습니다: {referenceName}", this);
        return false;
    }

    private bool ValidateRequiredAction(InputActionReference actionReference, string referenceName, out InputAction action)
    {
        action = actionReference ? actionReference.action : null;
        if (action != null) return true;

        Debug.LogError($"WindowInputRouter 필수 입력 액션이 없습니다: {referenceName}", this);
        return false;
    }

    private void OnEnable()
    {
        if (!_isInitialized) return;

        SetTabActionEnabled(_tabLeftInputAction, true, ref _enabledTabLeftAction);
        SetTabActionEnabled(_tabRightInputAction, true, ref _enabledTabRightAction);
        _stageManager.StoreInteractionRequested += _storeInteractionHandler;
        _stageManager.MapTeleportCompleted += HandleMapTeleportCompleted;
        _playerManager.PlayerWindowRequested += RequestPlayerWindow;
        _playerManager.PlayerTabRequested += RequestPlayerTab;
        _playerManager.OptionWindowRequested += RequestOptionWindow;
        _cancelInputAction.performed += HandleCancelInput;

        // 직접 활성화한 경우에만 비활성화 책임을 가진다.
        _enabledCancelAction = !_cancelInputAction.enabled;
        if (_enabledCancelAction) _cancelInputAction.Enable();
    }

    private void OnDisable()
    {
        if (!_isInitialized) return;

        SetTabActionEnabled(_tabLeftInputAction, false, ref _enabledTabLeftAction);
        SetTabActionEnabled(_tabRightInputAction, false, ref _enabledTabRightAction);
        _stageManager.StoreInteractionRequested -= _storeInteractionHandler;
        _stageManager.MapTeleportCompleted -= HandleMapTeleportCompleted;
        _playerManager.PlayerWindowRequested -= RequestPlayerWindow;
        _playerManager.PlayerTabRequested -= RequestPlayerTab;
        _playerManager.OptionWindowRequested -= RequestOptionWindow;
        _cancelInputAction.performed -= HandleCancelInput;
        if (_enabledCancelAction) _cancelInputAction.Disable();

        _enabledCancelAction = false;
        ClearRequests();
        _quickslotRegistration.Hide();

        // Cancel을 받을 라우터가 비활성화되면 상점창의 입력 차단도 함께 해제한다.
        if (_storeWindow.IsOpen)
        {
            _storeWindow.CloseWindow();
        }
    }

    private void SetTabActionEnabled(InputAction action, bool isEnabled, ref bool enabledByRouter)
    {
        if (isEnabled)
        {
            action.performed += _tabInputHandler;
            // 다른 UI가 이미 활성화한 액션은 라우터를 끌 때 비활성화하지 않는다.
            enabledByRouter = !action.enabled;
            if (enabledByRouter) action.Enable();
        }
        else
        {
            action.performed -= _tabInputHandler;
            if (enabledByRouter) action.Disable();
            enabledByRouter = false;
        }
    }

    private void HandleTabInput(InputAction.CallbackContext context)
    {
        if (context.action == _tabLeftInputAction) _tabLeftRequested = true;
        if (context.action == _tabRightInputAction) _tabRightRequested = true;
    }

    private void HandleCancelInput(InputAction.CallbackContext context)
    {
        _cancelRequested = true;
    }

    private void RequestPlayerWindow()
    {
        _playerWindowRequested = true;
    }

    private void RequestPlayerTab(PlayerTabType tabType)
    {
        _requestedPlayerTab = tabType;
        _playerTabRequested = true;
    }

    private void RequestOptionWindow()
    {
        _optionWindowRequested = true;
    }

    private void RequestStoreWindow(Store store, PlayerManager player)
    {
        if (!isActiveAndEnabled || !_storeWindow.isActiveAndEnabled || !store || !store.isActiveAndEnabled
            || !player || !player.isActiveAndEnabled || player != _playerManager)
        {
            return;
        }

        // 상점 요청도 Cancel 및 단축키와 함께 프레임당 한 번만 처리한다.
        _pendingStore = store;
        _pendingStorePlayer = player;
        _storeWindowRequested = true;
    }

    private void HandleMapTeleportCompleted()
    {
        _mapTeleportCompleted = true;
    }

    private void LateUpdate()
    {
        if (_mapTeleportCompleted)
        {
            // 클릭과 같은 프레임의 다른 창 요청을 버리고 이동 후 지도 창을 닫는다.
            ClearRequests();
            _playerWindow.CloseWindow();
            return;
        }

        if (!_cancelRequested && !_playerWindowRequested && !_playerTabRequested && !_optionWindowRequested && !_storeWindowRequested
            && !_tabLeftRequested && !_tabRightRequested)
        {
            return;
        }

        bool cancelRequested = _cancelRequested;
        bool playerWindowRequested = _playerWindowRequested;
        bool playerTabRequested = _playerTabRequested;
        PlayerTabType requestedPlayerTab = _requestedPlayerTab;
        bool optionWindowRequested = _optionWindowRequested;
        bool storeWindowRequested = _storeWindowRequested;
        // 같은 프레임에 양쪽을 누르면 전환하지 않고, 반복 입력도 프레임당 한 번만 처리한다.
        int tabDirection = (_tabRightRequested ? 1 : 0) - (_tabLeftRequested ? 1 : 0);
        Store pendingStore = _pendingStore;
        PlayerManager pendingStorePlayer = _pendingStorePlayer;
        ClearRequests();

        // 같은 프레임의 Cancel과 창 열기 요청 중 취소 처리를 우선한다.
        if (cancelRequested && TryCancelTopWindow())
        {
            return;
        }

        // 모달 패널이 떠 있는 동안에는 다른 창 단축키를 처리하지 않는다.
        if (_skillConfirmation.IsOpen || _quickslotRegistration.IsOpen)
        {
            return;
        }

        // 상점창이 입력과 일시정지를 소유하는 동안 다른 창은 열지 않는다.
        if (_storeWindow.IsOpen)
        {
            return;
        }

        if (storeWindowRequested)
        {
            // 닫힌 상태에서 동시에 들어온 Cancel도 상점 열기보다 우선한다.
            if (!cancelRequested && !_playerWindow.IsOpen && !_optionWindow.IsOpen)
            {
                _storeWindow.OpenStore(pendingStore, pendingStorePlayer);
            }

            return;
        }

        if (optionWindowRequested)
        {
            if (!_playerWindow.IsOpen)
            {
                _optionWindow.ToggleWindow();
            }

            return;
        }

        if (playerTabRequested)
        {
            if (!_optionWindow.IsOpen)
            {
                _playerWindow.ShowTab(requestedPlayerTab);
            }

            return;
        }

        if (playerWindowRequested)
        {
            if (!_optionWindow.IsOpen)
            {
                _playerWindow.ToggleWindow();
            }

            return;
        }

        if (tabDirection != 0 && _playerWindow.IsOpen && !_optionWindow.IsOpen)
        {
            _playerWindow.SelectAdjacentTab(tabDirection);
        }
    }

    private bool TryCancelTopWindow()
    {
        // B/○/Escape 한 번으로 등록 패널과 플레이어 창이 함께 닫히지 않도록 먼저 소비한다.
        if (_quickslotRegistration.IsOpen)
        {
            _quickslotRegistration.Cancel();
            return true;
        }

        if (_skillConfirmation.IsOpen)
        {
            _skillConfirmation.Cancel();
            return true;
        }

        if (_storeWindow.IsOpen)
        {
            _storeWindow.CloseWindow();
            return true;
        }

        if (_optionWindow.IsOpen)
        {
            _optionWindow.CloseWindow();
            return true;
        }

        if (_playerWindow.IsOpen)
        {
            _playerWindow.CloseWindow();
            return true;
        }

        return false;
    }

    private void ClearRequests()
    {
        _cancelRequested = false;
        _tabLeftRequested = false;
        _tabRightRequested = false;
        _playerWindowRequested = false;
        _playerTabRequested = false;
        _optionWindowRequested = false;
        _storeWindowRequested = false;
        _mapTeleportCompleted = false;
        _pendingStore = null;
        _pendingStorePlayer = null;
    }
}
