using System;
using UnityEngine;

public enum PlayerActionState
{
    Free,
    Dodging,
    Attacking,
    Guarding,
    Stunned,
    Dead
}

public enum PlayerLocomotionState
{
    Idle,
    Moving,
    Sprinting
}

[DisallowMultipleComponent]
public sealed class PlayerState : MonoBehaviour
{
    public PlayerActionState ActionState { get; private set; } = PlayerActionState.Free;
    public PlayerLocomotionState LocomotionState { get; private set; } = PlayerLocomotionState.Idle;

    public event Action<PlayerActionState> ActionStateChanged;
    public event Action<PlayerLocomotionState> LocomotionStateChanged;

    public bool CanMove => ActionState == PlayerActionState.Free;
    public bool CanDodge => ActionState == PlayerActionState.Free;
    public bool CanGuard => ActionState == PlayerActionState.Free;
    public bool CanUseNormalAttack => ActionState == PlayerActionState.Free;
    public bool CanUseDodgeAttack => ActionState == PlayerActionState.Dodging;
    public bool CanUseSprintAttack => ActionState == PlayerActionState.Free && LocomotionState == PlayerLocomotionState.Sprinting;

    public void SetActionState(PlayerActionState actionState)
    {
        if (ActionState == actionState) return;

        ActionState = actionState;
        ActionStateChanged?.Invoke(actionState);
    }

    public void SetLocomotionState(PlayerLocomotionState locomotionState)
    {
        if (LocomotionState == locomotionState) return;

        LocomotionState = locomotionState;
        LocomotionStateChanged?.Invoke(locomotionState);
    }
}
