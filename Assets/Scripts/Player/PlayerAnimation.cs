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
    private static readonly int _staggerHash = Animator.StringToHash("Base Layer.Stagger");
    private static readonly int _guardBreakHash = Animator.StringToHash("Base Layer.GuardBreak");

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

    public void PlayAttack(AttackDefinition attackDefinition, float attackSpeedMultiplier = 1f)
    {
        // 추후 PlayerManager가 전달하는 패시브 배율을 공통 공격 재생에 반영한다.
        TryPlayAttack(attackDefinition, attackSpeedMultiplier);
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

    public bool TryPlayPoiseReaction(bool isGuardBreak)
    {
        // 피격 모션으로 전환하기 전에 기존 행동의 애니메이션 요청을 정리한다.
        SetGuarding(false);
        ResetAttackState();

        if (!CharacterAnimator) return false;

        CharacterAnimator.ResetTrigger(_dodgeHash);
        CharacterAnimator.SetBool(_isSprintingHash, false);
        CharacterAnimator.SetFloat(_moveXHash, 0f);
        CharacterAnimator.SetFloat(_moveYHash, 0f);

        int stateHash = isGuardBreak ? _guardBreakHash : _staggerHash;
        if (!CharacterAnimator.HasState(0, stateHash)) return false;

        // 반응 타이머와 모션 시작을 맞추기 위해 즉시 전환한다.
        CharacterAnimator.Play(stateHash, 0, 0f);
        return true;
    }

    public void CompletePoiseReaction()
    {
        // 기존 공통 메서드의 Locomotion 복귀 처리를 재사용한다.
        CompleteAttack();
    }

    public void SetSprinting(bool isSprinting)
    {
        if (CharacterAnimator)
        {
            CharacterAnimator.SetBool(_isSprintingHash, isSprinting);
        }
    }
}
