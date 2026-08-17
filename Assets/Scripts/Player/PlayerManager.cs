using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerManager : MonoBehaviour
{
    private PlayerControl _playerControl;
    private PlayerState _playerState;
    private PlayerMove _playerMove;
    private PlayerDodge _playerDodge;
    private PlayerAttack _playerAttack;
    private PlayerAnimation _playerAnimation;
    private PlayerCamera _playerCamera;
    private PlayerLockOn _playerLockOn;

    public event Action<LockOnTarget> LockOnTargetChanged;

    private void Awake()
    {
        TryGetComponent(out _playerControl);
        TryGetComponent(out _playerState);
        TryGetComponent(out _playerMove);
        TryGetComponent(out _playerDodge);
        TryGetComponent(out _playerAttack);
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
            _playerControl.AttackPressed += HandleAttackPressed;
        }
    }

    private void OnDisable()
    {
        if (_playerControl)
        {
            _playerControl.LockOnPressed -= HandleLockOnPressed;
            _playerControl.DodgePressed -= HandleDodgePressed;
            _playerControl.AttackPressed -= HandleAttackPressed;
        }
    }
    private void Update()
    {
        if (_playerDodge)
        {
            _playerDodge.UpdateRecharge(Time.deltaTime);
        }

        if (_playerLockOn && _playerLockOn.TryReleaseInvalidTarget())
        {
            // 락온 타깃이 유효하지 않게 되면 변경 사실을 전달한다.
            NotifyLockOnTargetChanged();
        }

        if (!_playerControl || !_playerState || !_playerMove || !_playerCamera) return;

        _playerCamera.SetLookInput(_playerControl.LookInput, _playerControl.IsLookInputDelta);

        LockOnTarget lockOnTarget = _playerLockOn.CurrentTarget;

        if (lockOnTarget)
        {
            _playerCamera.SetLockOnTarget(lockOnTarget.AimPosition);
        }
        else
        {
            _playerCamera.ClearLockOnTarget();
        }

        UpdateLocomotionState();

        if (_playerState.ActionState == PlayerActionState.Dodging)
        {
            if (_playerMove.UpdateDodge(Time.deltaTime))
            {
                // 회피 이동이 끝나면 다음 행동을 받을 수 있는 상태로 복귀한다.
                _playerState.SetActionState(PlayerActionState.Free);
            }
        }
        else if (_playerState.ActionState == PlayerActionState.Attacking)
        {
            UpdateAttack();
        }
        else if (_playerState.CanMove && _playerState.LocomotionState == PlayerLocomotionState.Sprinting)
        {
            // 달리기는 락온 상태에서도 카메라 기준 자유 이동을 사용한다.
            _playerMove.MoveSprint(_playerControl.MoveInput, _playerCamera.YawRotation, Time.deltaTime);
        }
        else if (_playerState.CanMove && lockOnTarget)
        {
            _playerMove.MoveLockedOn(_playerControl.MoveInput, lockOnTarget.AimPosition, Time.deltaTime);
        }
        else if (_playerState.CanMove)
        {
            _playerMove.Move(_playerControl.MoveInput, _playerCamera.YawRotation, Time.deltaTime);
        }

        if (_playerAnimation)
        {
            _playerAnimation.SetSprinting(_playerState.LocomotionState == PlayerLocomotionState.Sprinting);
            _playerAnimation.SetMovement(_playerMove.LocalMoveDirection, Time.deltaTime);
        }
    }

    public void SetMaxDodgeCount(int maxDodgeCount)
    {
        if (_playerDodge)
        {
            // 스킬 시스템은 PlayerManager를 통해 최대 회피 횟수를 변경한다.
            _playerDodge.SetMaxDodgeCount(maxDodgeCount);
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
        if (!_playerControl || !_playerState || !_playerMove || !_playerDodge || !_playerAnimation || !_playerCamera) return;
        if (!_playerState.CanDodge || !_playerDodge.CanDodge) return;

        LockOnTarget lockOnTarget = _playerLockOn ? _playerLockOn.CurrentTarget : null;

        if (lockOnTarget)
        {
            _playerMove.BeginLockedOnDodge(_playerControl.MoveInput, lockOnTarget.AimPosition);
        }
        else
        {
            _playerMove.BeginDodge(_playerControl.MoveInput, _playerCamera.YawRotation);
        }

        // 회피 상태로 전환할 때 회피 횟수를 소비하고 일반 이동 상태를 해제한다.
        _playerDodge.ConsumeDodge();
        _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
        _playerState.SetActionState(PlayerActionState.Dodging);
        _playerAnimation.PlayDodge(_playerMove.DodgeLocalDirection);
    }

    private void HandleAttackPressed(AttackInputType inputType)
    {
        if (!_playerState || !_playerAttack || !_playerAnimation) return;

        if (_playerState.ActionState == PlayerActionState.Attacking)
        {
            QueueComboInput(inputType);
            return;
        }

        if (!_playerState.CanUseNormalAttack) return;
        if (!_playerAttack.TryBeginAttack(inputType, out AttackDefinition attackDefinition)) return;

        _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
        _playerState.SetActionState(PlayerActionState.Attacking);
        _playerAnimation.PlayAttack(attackDefinition);
    }

    private void QueueComboInput(AttackInputType inputType)
    {
        if (!_playerAnimation.TryGetAttackNormalizedTime(out float normalizedTime)) return;

        _playerAttack.TryQueueNextAttack(inputType, normalizedTime);
    }

    private void UpdateAttack()
    {
        if (_playerAnimation.TryGetAttackNormalizedTime(out float normalizedTime)
            && _playerAttack.TryConsumeQueuedAttack(normalizedTime, out AttackDefinition nextAttack))
        {
            _playerAnimation.PlayAttack(nextAttack);
            return;
        }

        if (!_playerAnimation.IsAttackFinished()) return;

        _playerAttack.EndAttack();
        _playerAnimation.CompleteAttack();
        _playerState.SetActionState(PlayerActionState.Free);
    }

    private void UpdateLocomotionState()
    {
        if (!_playerState.CanMove || _playerControl.MoveInput.sqrMagnitude <= 0.01f)
        {
            _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
            return;
        }

        if (_playerControl.IsSprintRequested)
        {
            _playerState.SetLocomotionState(PlayerLocomotionState.Sprinting);
            return;
        }

        _playerState.SetLocomotionState(PlayerLocomotionState.Moving);
    }

    private void NotifyLockOnTargetChanged()
    {
        LockOnTargetChanged?.Invoke(_playerLockOn.CurrentTarget);
    }
}
