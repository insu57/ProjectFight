using System;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    private PlayerControl _playerControl;
    private PlayerMove _playerMove;
    private PlayerAnimation _playerAnimation;
    private PlayerCamera _playerCamera;
    private PlayerLockOn _playerLockOn;

    public event Action<LockOnTarget> LockOnTargetChanged;

    private void Awake()
    {
        TryGetComponent(out _playerControl);
        TryGetComponent(out _playerMove);
        TryGetComponent(out _playerAnimation);
        TryGetComponent(out _playerCamera);
        TryGetComponent(out _playerLockOn);
    }

    private void OnEnable()
    {
        if (_playerControl)
        {
            _playerControl.LockOnPressed += HandleLockOnPressed;
        }
    }

    private void OnDisable()
    {
        if (_playerControl)
        {
            _playerControl.LockOnPressed -= HandleLockOnPressed;
        }
    }


    private void Update()
    {
        if (!_playerControl || !_playerMove || !_playerCamera) return;

        _playerCamera.SetLookInput(_playerControl.LookInput, _playerControl.IsLookInputDelta);
        _playerMove.Move(_playerControl.MoveInput, _playerCamera.YawRotation, Time.deltaTime);

        if (_playerAnimation)
        {
            _playerAnimation.SetMovement(_playerMove.LocalMoveDirection, Time.deltaTime);
        }
    }

    private void HandleLockOnPressed()
    {
        if (!_playerLockOn) return;

        _playerLockOn.TryAcquireTarget();
        LockOnTargetChanged?.Invoke(_playerLockOn.CurrentTarget);
    }
}
