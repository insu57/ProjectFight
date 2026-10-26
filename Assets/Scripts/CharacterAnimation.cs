using UnityEngine;

[DisallowMultipleComponent]
public abstract class CharacterAnimation : MonoBehaviour
{
    private const string AttackTagName = "Attack";
    private static readonly int _attackSpeedHash = Animator.StringToHash("AttackSpeed");
    private static readonly int _deadHash = Animator.StringToHash("Base Layer.Dead");

    [Header("공통 공격 애니메이션")]
    [SerializeField] private string _locomotionStateName = "Base Layer.Locomotion";
    [SerializeField, Min(0f)] private float _attackExitTransitionDuration = 0.1f;

    private int _attackTagHash;
    private int _locomotionStateHash;
    private bool _isAttackRequested;
    private bool _hasEnteredAttackState;
    private float _currentAttackExitTransitionDuration = -1f;
    private SoundDefinition _pendingAttackSound;
    private float _attackSoundNormalizedTime;
    private double _hitStopEndTime;
    private float _speedBeforeHitStop;
    private bool _isDeathPlaying;
    private AnimatorCullingMode _cullingBeforeDeath;

    public bool IsHitStopped { get; private set; }

    // Animator는 캐릭터와 수명을 공유하는 필수 참조이며 Awake에서 검증한다.
    protected Animator CharacterAnimator { get; private set; }

    protected virtual void Awake()
    {
        CharacterAnimator = GetComponentInChildren<Animator>();
        // Animator는 필수 구성 요소이므로 프리팹 설정 누락을 초기화 시 즉시 알린다.
        if (!CharacterAnimator)
        {
            throw new MissingComponentException($"{name}의 {GetType().Name}에는 자신 또는 자식 오브젝트의 Animator가 필요합니다.");
        }

        _attackTagHash = Animator.StringToHash(AttackTagName);
        _locomotionStateHash = Animator.StringToHash(_locomotionStateName);
    }

    public void BeginHitStop(float duration)
    {
        if (_isDeathPlaying) return;
        if (!isActiveAndEnabled) return;
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

        // 히트스탑 이전 재생 속도를 복원한다.
        CharacterAnimator.speed = _speedBeforeHitStop;

        IsHitStopped = false;
        _hitStopEndTime = 0d;
    }

    protected virtual void OnDisable()
    {
        // 비활성화 후 재사용할 때 정지 상태가 남지 않게 한다.
        ClearHitStop();
        ResetDeathState();
        _pendingAttackSound = null;
    }

    public virtual bool TryPlayDeath()
    {
        ClearHitStop();
        ResetAttackState();
        if (!isActiveAndEnabled || !CharacterAnimator.HasState(0, _deadHash)) return false;

        // 화면 밖에서도 사망 모션이 진행되어 적이 풀로 반환될 수 있어야 한다.
        if (!_isDeathPlaying) _cullingBeforeDeath = CharacterAnimator.cullingMode;
        _isDeathPlaying = true;
        CharacterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        CharacterAnimator.Play(_deadHash, 0, 0f);
        CharacterAnimator.Update(0f);

        // 상태만 있고 Motion이 비어 있는 설정은 재생 실패로 처리한다.
        if (CharacterAnimator.GetCurrentAnimatorClipInfoCount(0) > 0) return true;
        ResetDeathState();
        return false;
    }

    public bool IsDeathFinished()
    {
        if (!_isDeathPlaying || !isActiveAndEnabled) return true;

        AnimatorStateInfo state = CharacterAnimator.GetCurrentAnimatorStateInfo(0);
        return state.fullPathHash != _deadHash || state.normalizedTime >= 1f;
    }

    private void ResetDeathState()
    {
        if (_isDeathPlaying) CharacterAnimator.cullingMode = _cullingBeforeDeath;
        _isDeathPlaying = false;
    }

    public bool TryPlayAttack(AttackDefinition attackDefinition, float attackSpeedMultiplier = 1f, float transitionDurationOverride = -1f)
    {
        // 새 요청이 실패하더라도 이전 공격의 미재생 요청을 이어받지 않는다.
        _pendingAttackSound = null;
        if (!attackDefinition)
        {
            return false;
        }

        // 공격 상태 전용 배속을 사용해 히트스탑의 Animator.speed 정지/복원과 분리한다.
        float attackSpeed = attackDefinition.AnimationSpeed * attackSpeedMultiplier;

        // 연계 블렌딩은 진입할 공격의 설정을 사용하고, 미지정이면 기존 진입 시간을 유지한다.
        float transitionDuration = _isAttackRequested && attackDefinition.ComboTransitionDuration >= 0f
            ? attackDefinition.ComboTransitionDuration : attackDefinition.TransitionDuration;

        // 필살기처럼 기존 공격을 즉시 교체하는 요청만 블렌딩 시간을 덮어쓴다.
        if (transitionDurationOverride >= 0f) transitionDuration = transitionDurationOverride;

        CharacterAnimator.SetFloat(_attackSpeedHash, attackSpeed);
        if (transitionDurationOverride == 0f)
        {
            // 같은 프레임의 이동·판정 갱신이 이전 공격의 진행도를 읽지 않도록 즉시 평가한다.
            CharacterAnimator.Play(attackDefinition.AnimationStateHash, 0, 0f);
            CharacterAnimator.Update(0f);
        }
        else CharacterAnimator.CrossFadeInFixedTime(attackDefinition.AnimationStateHash, transitionDuration, 0, 0f);

        _isAttackRequested = true;
        _hasEnteredAttackState = false;
        _currentAttackExitTransitionDuration = attackDefinition.ExitTransitionDuration;
        _pendingAttackSound = attackDefinition.AttackSound;
        _attackSoundNormalizedTime = attackDefinition.AttackSoundNormalizedTime;

        return true;
    }

    public bool TryConsumeAttackSound(bool hasNormalizedTime, float normalizedTime, out SoundDefinition sound)
    {
        sound = null;
        if (!isActiveAndEnabled || IsHitStopped || !_pendingAttackSound) return false;
        // 0은 상태 진입을 기다리지 않고 재생하며, 지연음은 진행도가 지정 시점을 지난 최초 갱신에서 소비한다.
        if (_attackSoundNormalizedTime > 0f && (!hasNormalizedTime || !(normalizedTime >= _attackSoundNormalizedTime))) return false;

        sound = _pendingAttackSound;
        // 풀 상한 등으로 재생이 거절되어도 같은 공격음을 반복 요청하지 않는다.
        _pendingAttackSound = null;
        return true;
    }

    public bool TryGetAttackNormalizedTime(out float normalizedTime)
    {
        normalizedTime = 0f;

        if (!_isAttackRequested)
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
        if (!_isAttackRequested)
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
        ResetDeathState();
        ClearHitStop();
        ResetAttackState();

        // 이전 공격·기절 모션과 섞이지 않도록 기본 상태로 즉시 전환한다.
        if (isActiveAndEnabled)
        {
            CharacterAnimator.Play(_locomotionStateHash, 0, 0f);
        }
    }

    public void CompleteAttack()
    {
        // 비활성화 중에는 애니메이션 전환을 요청하지 않는다.
        if (isActiveAndEnabled)
        {
            // 공격별 복귀 시간이 없거나 경직 복귀처럼 공격 문맥이 없으면 공통 설정을 사용한다.
            float transitionDuration = _currentAttackExitTransitionDuration >= 0f
                ? _currentAttackExitTransitionDuration : _attackExitTransitionDuration;
            CharacterAnimator.CrossFadeInFixedTime(_locomotionStateHash, transitionDuration, 0);
        }

        // 활성 여부와 관계없이 공격 실행 상태는 초기화한다.
        ResetAttackState();
    }

    protected void ResetAttackState()
    {
        _isAttackRequested = false;
        _hasEnteredAttackState = false;
        _currentAttackExitTransitionDuration = -1f;
        _pendingAttackSound = null;
        _attackSoundNormalizedTime = 0f;

        // 공격 종료·중단과 풀 재사용 시 공격 배속을 복원한다.
        CharacterAnimator.SetFloat(_attackSpeedHash, 1f);
    }
}
