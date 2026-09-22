using System;
using UnityEngine;

public enum EnemyBehaviorState
{
    Idle,
    Chasing,
    Combat
}

public enum EnemyActionState
{
    Free,
    Attacking,
    Stunned,
    Dead,
    PatternWaiting,
    Guarding,
    Circling,
    GuardBroken
}

[DisallowMultipleComponent]
public sealed class EnemyState : MonoBehaviour
{
    public EnemyBehaviorState BehaviorState { get; private set; } = EnemyBehaviorState.Idle;
    public EnemyActionState ActionState { get; private set; } = EnemyActionState.Free;

    public event Action<EnemyBehaviorState> BehaviorStateChanged;
    public event Action<EnemyActionState> ActionStateChanged;

    public bool CanDecide => ActionState == EnemyActionState.Free;
    public bool CanMove => ActionState == EnemyActionState.Free;
    public bool CanAttack => ActionState == EnemyActionState.Free;

    public void SetBehaviorState(EnemyBehaviorState behaviorState)
    {
        if (BehaviorState == behaviorState)
        {
            return;
        }

        BehaviorState = behaviorState;
        BehaviorStateChanged?.Invoke(behaviorState);
    }

    public void SetActionState(EnemyActionState actionState)
    {
        if (ActionState == actionState)
        {
            return;
        }

        ActionState = actionState;
        ActionStateChanged?.Invoke(actionState);
    }
}
