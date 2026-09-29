using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAnimation : CharacterAnimation
{
    private static readonly int _moveXHash = Animator.StringToHash("MoveX");
    private static readonly int _moveYHash = Animator.StringToHash("MoveY");
    private static readonly int _isGuardingHash = Animator.StringToHash("IsGuarding");
    private static readonly int _guardTagHash = Animator.StringToHash("Guard");
    private static readonly int _guardStartHash = Animator.StringToHash("Base Layer.GuardStart");
    private static readonly int _guardEndHash = Animator.StringToHash("Base Layer.GuardEnd");
    private static readonly int _staggerHash = Animator.StringToHash("Base Layer.Stagger");
    private static readonly int _staggerBackHash = Animator.StringToHash("Base Layer.StaggerBack");
    private static readonly int _guardBreakHash = Animator.StringToHash("Base Layer.GuardBreak");

    [SerializeField, Min(0f)] private float _movementDampTime = 0.1f;

    private bool _isGuardRequested;
    private int _guardExitRequestFrame;

    public void ResetForSpawn()
    {
        _isGuardRequested = false;
        _guardExitRequestFrame = -1;

        if (CharacterAnimator)
        {
            CharacterAnimator.SetBool(_isGuardingHash, false);
            CharacterAnimator.SetFloat(_moveXHash, 0f);
            CharacterAnimator.SetFloat(_moveYHash, 0f);
        }

        ResetLocomotionForPool();
    }

    public void SetMoveAmount(float moveAmount)
    {
        SetMovement(new Vector2(0f, moveAmount), Time.deltaTime);
    }

    public override bool TryPlayDeath()
    {
        CancelGuard();
        if (CharacterAnimator)
        {
            CharacterAnimator.SetFloat(_moveXHash, 0f);
            CharacterAnimator.SetFloat(_moveYHash, 0f);
        }

        return base.TryPlayDeath();
    }

    public void SetMovement(Vector2 localMovement, float deltaTime)
    {
        if (!CharacterAnimator) return;

        // 경계 이동은 플레이어를 바라보면서 옆이나 뒤로 걸을 수 있다.
        CharacterAnimator.SetFloat(_moveXHash, localMovement.x, _movementDampTime, deltaTime);
        CharacterAnimator.SetFloat(_moveYHash, localMovement.y, _movementDampTime, deltaTime);
    }

    public bool TryBeginGuard()
    {
        if (!CharacterAnimator || !CharacterAnimator.HasState(0, _guardStartHash) || !CharacterAnimator.HasState(0, _guardEndHash)) return false;

        ResetAttackState();
        _isGuardRequested = true;
        CharacterAnimator.SetBool(_isGuardingHash, true);
        CharacterAnimator.CrossFadeInFixedTime(_guardStartHash, 0.05f, 0, 0f);
        return true;
    }

    public void EndGuard()
    {
        _isGuardRequested = false;
        _guardExitRequestFrame = Time.frameCount;

        if (!CharacterAnimator) return;

        CharacterAnimator.SetBool(_isGuardingHash, false);
        CharacterAnimator.CrossFadeInFixedTime(_guardEndHash, 0.05f, 0, 0f);
    }

    public bool IsGuardFinished()
    {
        if (_isGuardRequested || Time.frameCount <= _guardExitRequestFrame) return false;
        if (!CharacterAnimator) return true;

        if (CharacterAnimator.IsInTransition(0))
        {
            if (CharacterAnimator.GetNextAnimatorStateInfo(0).tagHash == _guardTagHash) return false;
        }

        return CharacterAnimator.GetCurrentAnimatorStateInfo(0).tagHash != _guardTagHash;
    }

    public void CancelGuard()
    {
        // 강제 중단에서는 종료 모션을 생략하고 다음 피격 모션으로 전환한다.
        _isGuardRequested = false;

        if (CharacterAnimator)
        {
            CharacterAnimator.SetBool(_isGuardingHash, false);
        }
    }

    public bool TryPlayPoiseReaction(bool isGuardBreak, bool isHitFromBehind)
    {
        if (!CharacterAnimator) return false;

        int stateHash = isGuardBreak ? _guardBreakHash : _staggerHash;
        // 후방 상태가 없는 기존 Controller는 전방 경직으로 대체한다.
        if (!isGuardBreak && isHitFromBehind && CharacterAnimator.HasState(0, _staggerBackHash))
        {
            stateHash = _staggerBackHash;
        }

        if (!CharacterAnimator.HasState(0, stateHash)) return false;

        CancelGuard();
        ResetAttackState();

        // 지정 시간의 시작과 모션 시작을 맞추기 위해 즉시 전환한다.
        CharacterAnimator.Play(stateHash, 0, 0f);
        return true;
    }
}
