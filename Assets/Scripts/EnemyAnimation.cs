using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAnimation : CharacterAnimation
{
    private static readonly int _moveAmountHash = Animator.StringToHash("MoveAmount");

    [SerializeField, Min(0f)] private float _movementDampTime = 0.1f;

    public void SetMoveAmount(float moveAmount)
    {
        if (!CharacterAnimator)
        {
            return;
        }

        CharacterAnimator.SetFloat(_moveAmountHash, moveAmount, _movementDampTime, Time.deltaTime);
    }
}
