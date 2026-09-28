using UnityEngine;

[DisallowMultipleComponent]
public abstract class CharacterAnimation : MonoBehaviour
{
    private const string AttackTagName = "Attack";
    private static readonly int _attackSpeedHash = Animator.StringToHash("AttackSpeed");

    [Header("공통 공격 애니메이션")]
    [SerializeField] private string _locomotionStateName = "Base Layer.Locomotion";
    [SerializeField, Min(0f)] private float _attackExitTransitionDuration = 0.1f;

    private int _attackTagHash;
    private int _locomotionStateHash;
    private bool _isAttackRequested;
    private bool _hasEnteredAttackState;
    private double _hitStopEndTime;
    private float _speedBeforeHitStop;

    public bool IsHitStopped { get; private set; }

    protected Animator CharacterAnimator { get; private set; }

    protected virtual void Awake()
    {
        CharacterAnimator = GetComponentInChildren<Animator>();
        _attackTagHash = Animator.StringToHash(AttackTagName);
        _locomotionStateHash = Animator.StringToHash(_locomotionStateName);
    }

    public void BeginHitStop(float duration)
    {
        if (!isActiveAndEnabled || !CharacterAnimator) return;
        if (duration <= 0f) return;

        double endTime = Time.unscaledTimeAsDouble + duration;

        if (IsHitStopped)
        {
            // 중복 타격은 시간을 합산하지 않고 종료 시점만 연장한다.
            if (endTime > _hitStopEndTime)
            {
                _hitStopEndTime = endTime;
            }

            return;
        }

        _speedBeforeHitStop = CharacterAnimator.speed;
        _hitStopEndTime = endTime;
        IsHitStopped = true;
        CharacterAnimator.speed = 0f;
    }

    public void UpdateHitStop()
    {
        if (!IsHitStopped) return;
        if (Time.unscaledTimeAsDouble < _hitStopEndTime) return;

        ClearHitStop();
    }

    public void ClearHitStop()
    {
        if (!IsHitStopped) return;

        if (CharacterAnimator)
        {
            // 히트스탑 이전 재생 속도를 복원한다.
            CharacterAnimator.speed = _speedBeforeHitStop;
        }

        IsHitStopped = false;
        _hitStopEndTime = 0d;
    }

    protected virtual void OnDisable()
    {
        // 비활성화 후 재사용할 때 정지 상태가 남지 않게 한다.
        ClearHitStop();
    }

    public bool TryPlayAttack(AttackDefinition attackDefinition, float attackSpeedMultiplier = 1f)
    {
        if (!CharacterAnimator || !attackDefinition)
        {
            return false;
        }

        // 공격 상태 전용 배속을 사용해 히트스탑의 Animator.speed 정지/복원과 분리한다.
        float attackSpeed = attackDefinition.AnimationSpeed * attackSpeedMultiplier;
        CharacterAnimator.SetFloat(_attackSpeedHash, attackSpeed);
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

    protected void ResetLocomotionForPool()
    {
        ClearHitStop();
        ResetAttackState();

        // 이전 공격·기절 모션과 섞이지 않도록 기본 상태로 즉시 전환한다.
        if (CharacterAnimator && CharacterAnimator.isActiveAndEnabled)
        {
            CharacterAnimator.Play(_locomotionStateHash, 0, 0f);
        }
    }

    public void CompleteAttack()
    {
        // 비활성화 중에는 애니메이션 전환을 요청하지 않는다.
        if (CharacterAnimator && CharacterAnimator.isActiveAndEnabled)
        {
            CharacterAnimator.CrossFadeInFixedTime(_locomotionStateHash, _attackExitTransitionDuration, 0);
        }

        // 활성 여부와 관계없이 공격 실행 상태는 초기화한다.
        ResetAttackState();
    }

    protected void ResetAttackState()
    {
        _isAttackRequested = false;
        _hasEnteredAttackState = false;

        // 공격 종료, 피격 중단, 풀 재사용 시 이전 공격의 배속을 정리한다.
        if (CharacterAnimator)
        {
            CharacterAnimator.SetFloat(_attackSpeedHash, 1f);
        }
    }
}
