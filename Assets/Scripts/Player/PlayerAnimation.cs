using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAnimation : CharacterAnimation
{
    private static readonly int _moveXHash = Animator.StringToHash("MoveX");
    private static readonly int _moveYHash = Animator.StringToHash("MoveY");
    private static readonly int _dodgeXHash = Animator.StringToHash("DodgeX");
    private static readonly int _dodgeYHash = Animator.StringToHash("DodgeY");
    private static readonly int _dodgeHash = Animator.StringToHash("Dodge");
    private static readonly int _isSprintingHash = Animator.StringToHash("IsSprinting");
    private static readonly int _isGuardingHash = Animator.StringToHash("IsGuarding");
    private static readonly int _guardTagHash = Animator.StringToHash("Guard");

    [SerializeField, Min(0f)] private float _movementDampTime = 0.1f;
    [SerializeField, Min(0f)] private float _movementThreshold = 0.001f;

    private bool _isGuardRequested;

    public void SetMovement(Vector2 localMoveDirection, float deltaTime)
    {
        if (!CharacterAnimator)
        {
            return;
        }

        float movementThresholdSqr = _movementThreshold * _movementThreshold;
        bool hasMoveDirection = localMoveDirection.sqrMagnitude > movementThresholdSqr;

        if (!hasMoveDirection)
        {
            localMoveDirection = Vector2.zero;

            float currentMoveX = CharacterAnimator.GetFloat(_moveXHash);
            float currentMoveY = CharacterAnimator.GetFloat(_moveYHash);

            // 감쇠된 이동 값이 임계값 이하라면 정확하게 0으로 고정한다.
            if (currentMoveX * currentMoveX + currentMoveY * currentMoveY <= movementThresholdSqr)
            {
                CharacterAnimator.SetFloat(_moveXHash, 0f);
                CharacterAnimator.SetFloat(_moveYHash, 0f);
                return;
            }
        }

        CharacterAnimator.SetFloat(_moveXHash, localMoveDirection.x, _movementDampTime, deltaTime);
        CharacterAnimator.SetFloat(_moveYHash, localMoveDirection.y, _movementDampTime, deltaTime);
    }

    public void PlayAttack(AttackDefinition attackDefinition)
    {
        TryPlayAttack(attackDefinition);
    }

    public void PlayDodge(Vector2 dodgeDirection)
    {
        if (!CharacterAnimator)
        {
            return;
        }

        CharacterAnimator.SetFloat(_dodgeXHash, dodgeDirection.x);
        CharacterAnimator.SetFloat(_dodgeYHash, dodgeDirection.y);
        CharacterAnimator.SetTrigger(_dodgeHash);
    }

    public void SetGuarding(bool isGuarding)
    {
        _isGuardRequested = isGuarding;

        if (CharacterAnimator)
        {
            CharacterAnimator.SetBool(_isGuardingHash, isGuarding);
        }
    }

    public bool IsGuardFinished()
    {
        if (!CharacterAnimator || _isGuardRequested)
        {
            return false;
        }

        if (CharacterAnimator.IsInTransition(0))
        {
            AnimatorStateInfo nextStateInfo = CharacterAnimator.GetNextAnimatorStateInfo(0);

            // 가드 진입 또는 퇴출 과정에 가드 상태가 포함되어 있으면 아직 종료하지 않는다.
            if (nextStateInfo.tagHash == _guardTagHash)
            {
                return false;
            }
        }

        AnimatorStateInfo currentStateInfo = CharacterAnimator.GetCurrentAnimatorStateInfo(0);
        return currentStateInfo.tagHash != _guardTagHash;
    }

    public void SetSprinting(bool isSprinting)
    {
        if (CharacterAnimator)
        {
            CharacterAnimator.SetBool(_isSprintingHash, isSprinting);
        }
    }
}
