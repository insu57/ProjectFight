using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControl : MonoBehaviour, InputSystem_Actions.IPlayerActions
{
    private enum SprintInputState
    {
        Released,
        Pressed,
        Held
    }

    [SerializeField, Min(0f)] private float _sprintHoldDuration = 0.2f;

    private InputSystem_Actions _inputActions;
    private float _sprintPressedTime;
    private SprintInputState _sprintInputState;
    private bool _isGameplayInputEnabled = true;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool IsLookInputDelta { get; private set; }
    public bool IsSprintRequested => _sprintInputState == SprintInputState.Held;
    public bool IsGuardRequested { get; private set; }
    public event Action JumpPressed;
    public event Action LockOnPressed;
    public event Action DodgePressed;
    public event Action<AttackInputType> AttackPressed;
    public event Action<bool> GuardChanged;
    public event Action PlayerWindowPressed;
    public event Action<PlayerTabType> QuickPlayerTabPressed;
    public event Action OptionWindowPressed;
    public event Action InteractPressed;
    public event Action<int> QuickslotPressed;

    private void Awake()
    {
        _inputActions = new InputSystem_Actions();
        _inputActions.Player.SetCallbacks(this);
    }

    private void OnEnable()
    {
        _inputActions.Player.Enable();

        if (!_isGameplayInputEnabled)
        {
            SetGameplayActionsEnabled(false);
        }

        RefreshCursorState();
    }

    private void OnDisable()
    {
        _sprintInputState = SprintInputState.Released;
        IsGuardRequested = false;
        _inputActions.Player.Disable();

        // 플레이 종료나 씬 전환 후에는 이 컴포넌트가 설정한 커서 제한을 해제한다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            // 운영체제나 에디터가 변경한 커서 상태를 현재 입력 모드에 맞게 복구한다.
            RefreshCursorState();
        }
    }

    private void Update()
    {
        if (_sprintInputState != SprintInputState.Pressed) return;
        if (Time.time - _sprintPressedTime < _sprintHoldDuration) return;

        _sprintInputState = SprintInputState.Held;
    }

    private void OnDestroy()
    {
        _inputActions.Dispose();
    }

    public void SetGameplayInputEnabled(bool isEnabled)
    {
        if (_isGameplayInputEnabled == isEnabled)
        {
            return;
        }

        _isGameplayInputEnabled = isEnabled;
        SetGameplayActionsEnabled(isEnabled);
        RefreshCursorState();

        if (isEnabled)
        {
            return;
        }

        // 창이 열린 동안 기존 입력값이 플레이어 동작에 남지 않도록 초기화한다.
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        IsLookInputDelta = false;
        _sprintInputState = SprintInputState.Released;
        IsGuardRequested = false;
    }

    private void RefreshCursorState()
    {
        if (!isActiveAndEnabled || !Application.isFocused) return;

        // 입력 모드와 무관하게 창 내부 제한을 유지하고 UI 입력 중에만 포인터를 표시한다.
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = !_isGameplayInputEnabled;
    }

    private void SetGameplayActionsEnabled(bool isEnabled)
    {
        if (isEnabled)
        {
            _inputActions.Player.Move.Enable();
            _inputActions.Player.Look.Enable();
            _inputActions.Player.Jump.Enable();
            _inputActions.Player.Sprint.Enable();
            _inputActions.Player.LockOn.Enable();
            _inputActions.Player.LightAttack.Enable();
            _inputActions.Player.Guard.Enable();
            _inputActions.Player.HeavyAttack.Enable();
            _inputActions.Player.GrabAttack.Enable();
            _inputActions.Player.Interact.Enable();
            _inputActions.Player.QuickSlot1.Enable();
            _inputActions.Player.QuickSlot2.Enable();
            _inputActions.Player.QuickSlot3.Enable();
            _inputActions.Player.QuickSlot4.Enable();
            return;
        }

        // 창을 다시 닫을 수 있도록 PlayerWindow 입력은 비활성화하지 않는다.
        _inputActions.Player.Move.Disable();
        _inputActions.Player.Look.Disable();
        _inputActions.Player.Jump.Disable();
        _inputActions.Player.Sprint.Disable();
        _inputActions.Player.LockOn.Disable();
        _inputActions.Player.LightAttack.Disable();
        _inputActions.Player.Guard.Disable();
        _inputActions.Player.HeavyAttack.Disable();
        _inputActions.Player.GrabAttack.Disable();
        _inputActions.Player.Interact.Disable();
        _inputActions.Player.QuickSlot1.Disable();
        _inputActions.Player.QuickSlot2.Disable();
        _inputActions.Player.QuickSlot3.Disable();
        _inputActions.Player.QuickSlot4.Disable();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        LookInput = context.ReadValue<Vector2>();
        IsLookInputDelta = context.control.device is Pointer;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            JumpPressed?.Invoke();
        }
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            _sprintPressedTime = Time.time;
            _sprintInputState = SprintInputState.Pressed;

            // 길게 누를지와 관계없이 입력 순간에는 즉시 회피한다.
            DodgePressed?.Invoke();
            return;
        }
        //홀드 시 달리기 지속
        if (!context.canceled) return;

        _sprintInputState = SprintInputState.Released;
    }

    public void OnLockOn(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            LockOnPressed?.Invoke();
        }
    }

    public void OnLightAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // 입력 종류만 전달하고 실제 공격 가능 여부와 실행은 PlayerManager가 판단한다.
            AttackPressed?.Invoke(AttackInputType.Light);
        }
    }

    public void OnGuard(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            IsGuardRequested = true;
            GuardChanged?.Invoke(true);
            return;
        }

        if (!context.canceled) return;

        IsGuardRequested = false;
        GuardChanged?.Invoke(false);
    }

    public void OnHeavyAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            AttackPressed?.Invoke(AttackInputType.Heavy);
        }
    }

    public void OnGrabAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            AttackPressed?.Invoke(AttackInputType.Grab);
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            InteractPressed?.Invoke();
        }
    }

    public void OnQuickSlot1(InputAction.CallbackContext context)
    {
        HandleQuickslotInput(context, 1);
    }

    public void OnQuickSlot2(InputAction.CallbackContext context)
    {
        HandleQuickslotInput(context, 2);
    }

    public void OnQuickSlot3(InputAction.CallbackContext context)
    {
        HandleQuickslotInput(context, 3);
    }

    public void OnQuickSlot4(InputAction.CallbackContext context)
    {
        HandleQuickslotInput(context, 4);
    }

    private void HandleQuickslotInput(InputAction.CallbackContext context, int slotNumber)
    {
        if (!isActiveAndEnabled || !_isGameplayInputEnabled || !context.performed) return;

        // 입력 단계에서는 슬롯 번호만 알리고 보유 확인과 효과 실행은 Manager가 중개한다.
        QuickslotPressed?.Invoke(slotNumber);
    }

    public void OnPlayerWindow(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            PlayerWindowPressed?.Invoke();
        }
    }

    public void OnQuickInven(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            QuickPlayerTabPressed?.Invoke(PlayerTabType.Inventory);
        }
    }

    public void OnQuickSkillTree(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            QuickPlayerTabPressed?.Invoke(PlayerTabType.SkillTree);
        }
    }

    public void OnQuickMap(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            QuickPlayerTabPressed?.Invoke(PlayerTabType.Map);
        }
    }

    public void OnOptionWindow(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OptionWindowPressed?.Invoke();
        }
    }
}
