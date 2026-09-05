using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAnimation : CharacterAnimation
{
    private static readonly int _moveXHash = Animator.StringToHash("MoveX");
    private static readonly int _moveYHash = Animator.StringToHash("MoveY");

    [SerializeField, Min(0f)] private float _movementDampTime = 0.1f;

    public void SetMoveAmount(float moveAmount)
    {
        if (!CharacterAnimator)
        {
            return;
        }

        // 적은 바라보는 방향으로 이동하므로 좌우 값은 0, 전진 값은 이동량으로 전달한다.
        CharacterAnimator.SetFloat(_moveXHash, 0f, _movementDampTime, Time.deltaTime);
        CharacterAnimator.SetFloat(_moveYHash, moveAmount, _movementDampTime, Time.deltaTime);
    }
}
