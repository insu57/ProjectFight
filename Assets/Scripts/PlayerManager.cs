using System;
using UnityEngine;

[DisallowMultipleComponent]
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
            _playerControl.DodgePressed += HandleDodgePressed;
        }
    }

    private void OnDisable()
    {
        if (_playerControl)
        {
            _playerControl.LockOnPressed -= HandleLockOnPressed;
            _playerControl.DodgePressed -= HandleDodgePressed;
        }
    }


    private void Update()
    {
        if (_playerLockOn && _playerLockOn.TryReleaseInvalidTarget())
        {
            NotifyLockOnTargetChanged();
        }

        if (!_playerControl || !_playerMove || !_playerCamera) return;

        _playerCamera.SetLookInput(_playerControl.LookInput, _playerControl.IsLookInputDelta);

        LockOnTarget lockOnTarget = _playerLockOn ? _playerLockOn.CurrentTarget : null;

        if (lockOnTarget)
        {
            _playerCamera.SetLockOnTarget(lockOnTarget.AimPosition);
        }
        else
        {
            _playerCamera.ClearLockOnTarget();
        }

        bool isSprinting = _playerControl.IsSprinting && _playerControl.MoveInput.sqrMagnitude > 0.01f;

        if (_playerMove.IsDodging)
        {
            _playerMove.UpdateDodge(Time.deltaTime);
        }
        else if (isSprinting)
        {
            // 달리기는 락온 상태에서도 카메라 기준 자유 이동을 사용한다.
            _playerMove.MoveSprint(_playerControl.MoveInput, _playerCamera.YawRotation, Time.deltaTime);
        }
        else if (lockOnTarget)
        {
            _playerMove.MoveLockedOn(_playerControl.MoveInput, lockOnTarget.AimPosition, Time.deltaTime);
        }
        else
        {
            _playerMove.Move(_playerControl.MoveInput, _playerCamera.YawRotation, Time.deltaTime);
        }

        if (_playerAnimation)
        {
            _playerAnimation.SetSprinting(isSprinting);
            _playerAnimation.SetMovement(_playerMove.LocalMoveDirection, Time.deltaTime);
        }
    }

    private void HandleLockOnPressed()
    {
        if (!_playerLockOn) return;

        _playerLockOn.ToggleTarget();
        NotifyLockOnTargetChanged();
    }

    private void HandleDodgePressed()
    {
        if (!_playerMove || !_playerAnimation || !_playerCamera) return;

        LockOnTarget lockOnTarget = _playerLockOn ? _playerLockOn.CurrentTarget : null;
        bool didBeginDodge = lockOnTarget
            ? _playerMove.TryBeginLockedOnDodge(_playerControl.MoveInput, lockOnTarget.AimPosition)
            : _playerMove.TryBeginDodge(_playerControl.MoveInput, _playerCamera.YawRotation);

        if (didBeginDodge)
        {
            _playerAnimation.PlayDodge(_playerMove.DodgeLocalDirection);
        }
    }

    private void NotifyLockOnTargetChanged()
    {
        LockOnTargetChanged?.Invoke(_playerLockOn.CurrentTarget);
    }
}
