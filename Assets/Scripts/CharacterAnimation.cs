using UnityEngine;

[DisallowMultipleComponent]
public abstract class CharacterAnimation : MonoBehaviour
{
    private const string AttackTagName = "Attack";

    [Header("공통 공격 애니메이션")]
    [SerializeField] private string _locomotionStateName = "Base Layer.Locomotion";
    [SerializeField, Min(0f)] private float _attackExitTransitionDuration = 0.1f;

    private int _attackTagHash;
    private int _locomotionStateHash;
    private bool _isAttackRequested;
    private bool _hasEnteredAttackState;

    protected Animator CharacterAnimator { get; private set; }

    protected virtual void Awake()
    {
        CharacterAnimator = GetComponentInChildren<Animator>();
        _attackTagHash = Animator.StringToHash(AttackTagName);
        _locomotionStateHash = Animator.StringToHash(_locomotionStateName);
    }

    public bool TryPlayAttack(AttackDefinition attackDefinition)
    {
        if (!CharacterAnimator || !attackDefinition)
        {
            return false;
        }

        CharacterAnimator.CrossFadeInFixedTime(attackDefinition.AnimationStateHash, attackDefinition.TransitionDuration, 0);
        _isAttackRequested = true;
        _hasEnteredAttackState = false;
        return true;
    }

    public bool TryGetAttackNormalizedTime(out float normalizedTime)
    {
        normalizedTime = 0f;

        if (!CharacterAnimator || !_isAttackRequested)
        {
            return false;
        }

        if (CharacterAnimator.IsInTransition(0))
        {
            AnimatorStateInfo nextStateInfo = CharacterAnimator.GetNextAnimatorStateInfo(0);

            if (nextStateInfo.tagHash == _attackTagHash)
            {
                _hasEnteredAttackState = true;
                normalizedTime = nextStateInfo.normalizedTime;
                return true;
            }
        }

        AnimatorStateInfo currentStateInfo = CharacterAnimator.GetCurrentAnimatorStateInfo(0);

        if (currentStateInfo.tagHash != _attackTagHash)
        {
            return false;
        }

        _hasEnteredAttackState = true;
        normalizedTime = currentStateInfo.normalizedTime;
        return true;
    }

    public bool IsAttackFinished()
    {
        if (!CharacterAnimator || !_isAttackRequested)
        {
            return false;
        }

        bool isInTransition = CharacterAnimator.IsInTransition(0);

        if (isInTransition)
        {
            AnimatorStateInfo nextStateInfo = CharacterAnimator.GetNextAnimatorStateInfo(0);

            // 다른 공격 상태로 전환 중이면 현재 공격이 종료된 것으로 처리하지 않는다.
            if (nextStateInfo.tagHash == _attackTagHash)
            {
                _hasEnteredAttackState = true;
                return false;
            }
        }

        AnimatorStateInfo currentStateInfo = CharacterAnimator.GetCurrentAnimatorStateInfo(0);

        if (currentStateInfo.tagHash == _attackTagHash)
        {
            _hasEnteredAttackState = true;
            return !isInTransition && currentStateInfo.normalizedTime >= 1f;
        }

        return _hasEnteredAttackState;
    }

    public void CompleteAttack()
    {
        if (CharacterAnimator)
        {
            CharacterAnimator.CrossFadeInFixedTime(_locomotionStateHash, _attackExitTransitionDuration, 0);
        }

        ResetAttackState();
    }

    protected void ResetAttackState()
    {
        _isAttackRequested = false;
        _hasEnteredAttackState = false;
    }
}
