using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControl : MonoBehaviour, InputSystem_Actions.IPlayerActions
{
    [SerializeField, Min(0f)] private float _sprintHoldDuration = 0.2f;

    private InputSystem_Actions _inputActions;
    private float _sprintPressedTime;
    private bool _isSprintPressed;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool IsLookInputDelta { get; private set; }
    public bool IsSprinting { get; private set; }
    public event Action JumpPressed;
    public event Action LockOnPressed;
    public event Action DodgePressed;

    private void Awake()
    {
        _inputActions = new InputSystem_Actions();
        _inputActions.Player.SetCallbacks(this);
    }

    private void OnEnable()
    {
        _inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        _isSprintPressed = false;
        IsSprinting = false;
        _inputActions.Player.Disable();
    }

    private void Update()
    {
        if (!_isSprintPressed || IsSprinting) return;
        if (Time.time - _sprintPressedTime < _sprintHoldDuration) return;

        IsSprinting = true;
    }

    private void OnDestroy()
    {
        _inputActions.Dispose();
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
            _isSprintPressed = true;

            // 길게 누를지와 관계없이 입력 순간에는 즉시 회피한다.
            DodgePressed?.Invoke();
            return;
        }

        if (!context.canceled) return;

        _isSprintPressed = false;
        IsSprinting = false;
    }

    public void OnLockOn(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            LockOnPressed?.Invoke();
        }
    }
}
