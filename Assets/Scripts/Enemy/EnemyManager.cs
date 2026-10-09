using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyState))]
[RequireComponent(typeof(EnemyStat))]
[RequireComponent(typeof(EnemyBrain))]
[RequireComponent(typeof(EnemyMove))]
[RequireComponent(typeof(EnemyAnimation))]
[RequireComponent(typeof(EnemyPatternExecutor))]
public sealed class EnemyManager : PoolableObject, IHitStopReceiver
{
    private Transform _target;
    private AttackHitbox _attackHitbox;
    private Hurtbox _hurtbox;
    private LockOnTarget _lockOnTarget;
    private bool _lockOnEnabledBeforeDeath;
    private bool _deathPresentationStarted;
    private PlayerManager _rewardRecipient;
    [field: SerializeField] private EnemyDefinition _enemyDefinition;
    [Header("이동 중 피격")]
    [SerializeField, Min(0f)] private float _movementHitPauseDuration = 0.2f;
    // 씬에 직접 배치한 적은 Inspector에서 방을 지정하고, 풀 스폰은 SpawnManager가 전달한다.
    [SerializeField] private Room _sceneNavigationRoom;
    private EnemyDefinition _defaultEnemyDefinition;
    private EnemyState _enemyState;
    private EnemyStat _enemyStat;
    private EnemyBrain _enemyBrain;
    private EnemyMove _enemyMove;
    private EnemyAnimation _enemyAnimation;
    private EnemyPatternExecutor _patternExecutor;
    private bool _hasStarted;
    private float _circleBlockedTime;
    private float _movementHitPauseRemaining;
    // 캐릭터마다 한 번 생성하고 피격 시 재사용한다.
    private readonly PoiseReactionRuntime _poiseReaction = new();

    // 풀 재사용 전의 방 기록이 같은 인스턴스의 새 생명주기를 참조하지 않도록 구분한다.
    public uint SpawnVersion { get; private set; }
    public bool IsDead => _enemyStat && _enemyStat.IsDead;
    public event Action<EnemyManager> Died;

    private bool IsInPoiseReaction => _enemyState.ActionState == EnemyActionState.Stunned || _enemyState.ActionState == EnemyActionState.GuardBroken;

    private void Awake()
    {
        _defaultEnemyDefinition = _enemyDefinition;
        TryGetComponent(out _enemyState);
        TryGetComponent(out _enemyStat);
        TryGetComponent(out _enemyBrain);
        TryGetComponent(out _enemyMove);
        TryGetComponent(out _enemyAnimation);
        TryGetComponent(out _patternExecutor);

        _attackHitbox = GetComponentInChildren<AttackHitbox>();
        _hurtbox = GetComponentInChildren<Hurtbox>();
        _lockOnTarget = GetComponentInChildren<LockOnTarget>(true);

        if (_attackHitbox)
        {
            _attackHitbox.Initialize(this);
        }

        if (_hurtbox)
        {
            _hurtbox.Initialize(this);
        }
    }

    private void OnEnable()
    {
        _enemyMove?.SetNavigationRoom(_sceneNavigationRoom);

        if (_hurtbox)
        {
            _hurtbox.DamageRequested += HandleDamageRequested;
        }

        if (_enemyStat)
        {
            _enemyStat.Died += HandleDied;
        }
    }

    private void Start()
    {
        if (_hasStarted)
        {
            return;
        }

        _hasStarted = true;

        if (!_enemyDefinition)
        {
            return;
        }

        ApplyDefinitionStats();
        _enemyStat.ResetForSpawn();
        _patternExecutor.SetPatterns(_enemyDefinition.Patterns);
    }

    protected override void OnPoolSpawned()
    {
        SpawnVersion++;
        // 사망 시 제외했던 피격과 락온을 이 적의 초기 설정으로 복구한다.
        if (_deathPresentationStarted && _lockOnTarget) _lockOnTarget.enabled = _lockOnEnabledBeforeDeath;
        _deathPresentationStarted = false;
        _rewardRecipient = null;
        if (_hurtbox) _hurtbox.SetDamageEnabled(true);
        _hasStarted = true;
        _target = null;
        _circleBlockedTime = 0f;
        _movementHitPauseRemaining = 0f;
        _poiseReaction.Reset();
        _attackHitbox?.EndAttack();

        if (_enemyDefinition)
        {
            ApplyDefinitionStats();
            _patternExecutor.SetPatterns(_enemyDefinition.Patterns);
        }
        else
        {
            _patternExecutor.ResetForPool();
        }

        _enemyMove.ResetForSpawn();
        _enemyAnimation.ResetForSpawn();

        _enemyState.SetBehaviorState(EnemyBehaviorState.Idle);
        _enemyState.SetActionState(EnemyActionState.Free);
        _enemyStat.ResetForSpawn();
    }

    protected override void OnPoolDespawned()
    {
        _target = null;

        // 이전 스폰에서 예약한 작업이 재사용 이후 실행되지 않도록 정리한다.
        CancelInvoke();
        StopAllCoroutines();

        // 공격·패턴·히트스탑과 이벤트 구독은 기존 OnDisable에서 정리한다.
    }

    private void OnDisable()
    {
        // 풀 반환 후 이전 방의 참조와 경로를 사용하지 않는다.
        _enemyMove?.SetNavigationRoom(null);
        _rewardRecipient = null;
        _patternExecutor?.CancelCurrentPattern(false);
        _poiseReaction.Reset();
        _circleBlockedTime = 0f;
        _movementHitPauseRemaining = 0f;

        if (_enemyAnimation)
        {
            _enemyAnimation.ClearHitStop();
            _enemyAnimation.CancelGuard();
            _enemyAnimation.CompleteAttack();
        }

        // 재활성화 시 종료된 패턴이나 기절 상태가 남지 않게 한다.
        if (_enemyState && _enemyState.ActionState != EnemyActionState.Dead)
        {
            _enemyState.SetActionState(EnemyActionState.Free);
        }

        if (_hurtbox)
        {
            _hurtbox.DamageRequested -= HandleDamageRequested;
        }

        if (_attackHitbox)
        {
            _attackHitbox.EndAttack();
        }

        if (_enemyStat)
        {
            _enemyStat.Died -= HandleDied;
        }
    }

    private void Update()
    {
        if (_enemyStat && _enemyStat.IsDead && !_deathPresentationStarted) HandleDied();
        if (!isActiveAndEnabled) return;
        if (_enemyState && _enemyState.ActionState == EnemyActionState.Dead)
        {
            // 사망 중에는 AI와 쿨다운을 갱신하지 않고 연출 종료만 확인한다.
            if (!_enemyAnimation || _enemyAnimation.IsDeathFinished()) FinishDeath();
            return;
        }

        // 공격 상태를 벗어나면 활성 판정이 남지 않도록 정리한다.
        if (_attackHitbox && (!_enemyState || _enemyState.ActionState != EnemyActionState.Attacking))
        {
            _attackHitbox.EndAttack();
        }

        if (!_enemyState || !_enemyBrain || !_enemyMove || !_enemyAnimation || !_patternExecutor)
        {
            return;
        }

        _enemyAnimation.UpdateHitStop();

        if (_enemyAnimation.IsHitStopped)
        {
            // 이동, 판단, 패턴 진행, 쿨다운과 강인도 회복을 함께 정지한다.
            return;
        }

        float deltaTime = Time.deltaTime;
        // 히트스톱 이후부터 정지 시간을 소비하며 마지막 정지 프레임도 이동하지 않는다.
        bool isMovementHitPaused = _movementHitPauseRemaining > 0f;
        _movementHitPauseRemaining -= deltaTime;
        _patternExecutor.UpdateCooldowns(deltaTime);

        if (_enemyStat)
        {
            _enemyStat.UpdatePoiseRecovery(deltaTime);
        }

        if (IsInPoiseReaction)
        {
            UpdatePoiseReaction(deltaTime);
            return;
        }

        // 대기와 가드 종료 모션까지 실행 중인 패턴이 소유한다.
        if (_patternExecutor.IsExecuting)
        {
            UpdatePattern(deltaTime, isMovementHitPaused);
            return;
        }

        if (!_enemyState.CanDecide || !_target || !_target.gameObject.activeInHierarchy)
        {
            UpdateStationary(deltaTime);
            return;
        }

        Vector3 targetOffset = _target.position - transform.position;
        targetOffset.y = 0f;
        float targetSqrDistance = targetOffset.sqrMagnitude;

        if (!_enemyBrain.CanDetectTarget(targetSqrDistance))
        {
            _enemyState.SetBehaviorState(EnemyBehaviorState.Idle);
            UpdateStationary(deltaTime);
            return;
        }

        if (_enemyState.CanAttack && _patternExecutor.TryBeginPattern(targetSqrDistance))
        {
            _enemyState.SetBehaviorState(EnemyBehaviorState.Combat);
            _enemyState.SetActionState(EnemyActionState.PatternWaiting);
            _enemyMove.FaceTarget(_target.position, deltaTime);
            UpdateStationary(deltaTime);
            return;
        }

        if (_enemyState.CanMove && _enemyBrain.ShouldChaseTarget(targetSqrDistance))
        {
            _enemyState.SetBehaviorState(EnemyBehaviorState.Chasing);
            if (isMovementHitPaused)
            {
                _enemyMove.FaceTarget(_target.position, deltaTime);
                UpdateStationary(deltaTime);
                return;
            }

            _enemyMove.MoveTowards(_target.position, deltaTime);
            _enemyAnimation.SetMoveAmount(_enemyMove.MoveAmount);
            return;
        }

        _enemyState.SetBehaviorState(EnemyBehaviorState.Combat);
        _enemyMove.FaceTarget(_target.position, deltaTime);
        UpdateStationary(deltaTime);
    }

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    public void SetEnemyDefinition(EnemyDefinition enemyDefinition)
    {
        _enemyDefinition = enemyDefinition;

        // Start 이전에 전달된 설정은 Start에서 초기화한다.
        if (!_hasStarted || !_enemyDefinition || !_enemyStat) return;

        ApplyDefinitionStats();
    }

    public void InitializeSpawn(EnemyDefinition enemyDefinition, Transform target, Room room = null)
    {
        // 편성에 별도 Definition이 없으면 프리팹의 원래 설정을 사용한다.
        _enemyDefinition = enemyDefinition ? enemyDefinition : _defaultEnemyDefinition;
        if (_enemyDefinition)
        {
            ApplyDefinitionStats();
        }

        // 실행 중 스탯 변경과 달리 새 스폰에서는 패턴, 체력, 강인도까지 초기화한다.
        _patternExecutor.SetPatterns(_enemyDefinition ? _enemyDefinition.Patterns : null);
        _enemyStat.ResetForSpawn();
        _target = target;
        _enemyMove.SetNavigationRoom(room);
    }

    private void ApplyDefinitionStats()
    {
        _enemyStat.InitializeStats(_enemyDefinition);
        _enemyMove.SetMoveSpeed(_enemyStat.MoveSpeed);
    }

    public void TakeDamage(float damage)
    {
        // 기존 직접 피해 요청은 처치자 정보가 없는 피해로 처리한다.
        TakeDamage(damage, null);
    }

    private void TakeDamage(float damage, MonoBehaviour attacker)
    {
        if (!_enemyStat || _enemyStat.IsDead || damage <= 0f) return;

        // 이번 체력 피해의 사망 이벤트에서만 처치자를 참조한다. 이전 타격의 보상 권한은 남기지 않는다.
        _rewardRecipient = attacker as PlayerManager;
        _enemyStat.TakeDamage(damage);
        _rewardRecipient = null;
    }

    public void BeginHitStop(float duration)
    {
        if (!isActiveAndEnabled || !_enemyAnimation) return;

        _enemyAnimation.BeginHitStop(duration);
    }

    private void HandleDamageRequested(DamageInfo damageInfo)
    {
        if (!isActiveAndEnabled || !_enemyStat || !_enemyState) return;
        if (_enemyStat.IsDead || _enemyState.ActionState == EnemyActionState.Dead) return;
        if (CanBlock(damageInfo))
        {
            if (damageInfo.Damage <= 0f && damageInfo.GuardPoiseDamage <= 0f) return;

            damageInfo.ApplyHitStop(this);
            _enemyStat.TakeGuardPoiseDamage(damageInfo.GuardPoiseDamage);

            if (damageInfo.GuardPoiseDamage > 0f && _enemyStat.CurrentGuardPoise <= 0f)
            {
                BeginPoiseReaction(true, damageInfo.Attacker);
            }

            return;
        }

        // 일반 피격에는 가드 강인도 피해만 있는 공격을 적용하지 않는다.
        if (damageInfo.Damage <= 0f && damageInfo.PoiseDamage <= 0f) return;

        damageInfo.ApplyHitStop(this);

        _enemyStat.TakePoiseDamage(damageInfo.PoiseDamage);

        // 피격 시점의 방어력을 체력 피해에만 적용하고 기존 Died 이벤트 처리를 재사용한다.
        float damage = CombatUtility.CalculateDamageAfterDefense(damageInfo.Damage, _enemyStat.DefensePower);
        TakeDamage(damage, damageInfo.Attacker);

        // 사망은 기절보다 우선한다.
        if (!_enemyStat.IsDead && damageInfo.PoiseDamage > 0f && _enemyStat.CurrentPoise <= 0f)
        {
            BeginPoiseReaction(false, damageInfo.Attacker);
        }

        TryBeginMovementHitPause();
    }

    private void TryBeginMovementHitPause()
    {
        // 연타로 정지 시간을 연장하지 않고, 공격·가드·강인도 경직은 기존 처리를 따른다.
        if (_enemyStat.IsDead || IsInPoiseReaction || _movementHitPauseRemaining > 0f) return;
        if (!_enemyMove || !_enemyAnimation || _enemyMove.MoveAmount <= 0f) return;

        bool isChasing = _enemyState.CanMove && _enemyState.BehaviorState == EnemyBehaviorState.Chasing;
        bool isCircling = _enemyState.ActionState == EnemyActionState.Circling && _patternExecutor && _patternExecutor.IsCircling;
        if (!isChasing && !isCircling) return;

        _movementHitPauseRemaining = _movementHitPauseDuration;
        _enemyAnimation.StopMovement();
    }

    private bool CanBlock(DamageInfo damageInfo)
    {
        if (!_enemyState || !_patternExecutor) return false;
        if (_enemyState.ActionState != EnemyActionState.Guarding) return false;

        // 패턴에서 가드가 실제로 유지되는 구간에만 방어한다.
        if (!_patternExecutor.IsGuarding || !damageInfo.Attacker) return false;

        return CombatUtility.IsAttackerInFront(transform, damageInfo.Attacker.transform);
    }

    private void UpdatePattern(float deltaTime, bool isMovementHitPaused)
    {
        if (!_target || !_target.gameObject.activeInHierarchy)
        {
            CancelPatternToFree();
            UpdateStationary(deltaTime);
            return;
        }

        EnemyPatternStep step = _patternExecutor.CurrentStep;
        bool animationFinished = step.Type switch
        {
            EnemyPatternStepType.Attack => _enemyAnimation.IsAttackFinished(),
            EnemyPatternStepType.Guard => _enemyAnimation.IsGuardFinished(),
            _ => true
        };
        _patternExecutor.UpdateExecution(deltaTime, animationFinished, out bool stepStarted, out bool stepEnded, out bool patternCompleted);

        if (stepEnded)
        {
            _attackHitbox?.EndAttack();
            if (step.Type == EnemyPatternStepType.Guard)
            {
                _enemyAnimation.EndGuard();
            }
            else if (step.Type == EnemyPatternStepType.Attack)
            {
                _enemyAnimation.CompleteAttack();
            }

            _enemyState.SetActionState(EnemyActionState.PatternWaiting);
        }

        if (patternCompleted)
        {
            _enemyState.SetActionState(EnemyActionState.Free);
            UpdateStationary(deltaTime);
            return;
        }

        if (stepStarted && !BeginPatternStep(_patternExecutor.CurrentStep))
        {
            CancelPatternToFree();
            UpdateStationary(deltaTime);
            return;
        }

        if (_patternExecutor.IsAttackPlaying)
        {
            float normalizedTime = 0f;
            // 새 공격 요청 프레임에는 이전 공격의 진행도를 사용하지 않는다.
            bool hasNormalizedTime = !stepStarted && _enemyAnimation.TryGetAttackNormalizedTime(out normalizedTime);
            if (hasNormalizedTime && _patternExecutor.TryGetAttackMoveDisplacement(normalizedTime, out Vector2 displacement))
            {
                _enemyMove.MoveAttack(displacement, deltaTime);
            }
            else
            {
                _enemyMove.MoveStationary(deltaTime);
            }

            _enemyAnimation.SetMoveAmount(0f);
            _attackHitbox?.UpdateHitbox(hasNormalizedTime, normalizedTime);
            return;
        }

        _attackHitbox?.EndAttack();
        if (_patternExecutor.IsCircling)
        {
            if (isMovementHitPaused)
            {
                // Circle 시간은 위에서 갱신하되 의도적인 정지를 장애물 막힘으로 세지 않는다.
                _circleBlockedTime = 0f;
                _enemyMove.FaceTarget(_target.position, deltaTime);
                UpdateStationary(deltaTime);
                return;
            }

            step = _patternExecutor.CurrentStep;
            Vector2 movement = _enemyMove.MoveCircleTarget(_target.position, step.CircleSpeed, step.CircleDistance, step.CircleDistanceTolerance, _patternExecutor.CircleDirectionSign, deltaTime);
            _enemyAnimation.SetMovement(movement, deltaTime);

            // 이동을 요청했는데 일정 시간 거의 움직이지 못하면 패턴을 종료한다.
            if (step.CircleSpeed > 0f && movement.sqrMagnitude < 0.0001f)
            {
                _circleBlockedTime += deltaTime;
                if (_circleBlockedTime >= 0.5f)
                {
                    CancelPatternToFree();
                }
            }
            else
            {
                _circleBlockedTime = 0f;
            }

            return;
        }

        _enemyMove.FaceTarget(_target.position, deltaTime);
        UpdateStationary(deltaTime);
    }

    private bool BeginPatternStep(EnemyPatternStep step)
    {
        _circleBlockedTime = 0f;
        switch (step.Type)
        {
            case EnemyPatternStepType.Attack:
                if (!_enemyAnimation.TryPlayAttack(step.Attack, _enemyStat.AttackSpeed)) return false;
                _enemyState.SetActionState(EnemyActionState.Attacking);
                _attackHitbox?.BeginAttack(step.Attack, _enemyStat.AttackPower);
                return true;
            case EnemyPatternStepType.Guard:
                if (!_enemyAnimation.TryBeginGuard()) return false;
                _enemyState.SetActionState(EnemyActionState.Guarding);
                return true;
            case EnemyPatternStepType.CircleTarget:
                _enemyState.SetActionState(EnemyActionState.Circling);
                return true;
            default:
                return false;
        }
    }

    private void CancelPatternToFree()
    {
        _attackHitbox?.EndAttack();
        _patternExecutor.CancelCurrentPattern(true);
        _enemyAnimation.CancelGuard();
        _circleBlockedTime = 0f;

        // 강제 상태의 모션과 행동 상태를 일반 패턴 종료 처리로 덮어쓰지 않는다.
        if (_enemyState.ActionState == EnemyActionState.Dead || IsInPoiseReaction) return;

        _enemyAnimation.CompleteAttack();
        _enemyState.SetActionState(EnemyActionState.Free);
    }

    private void BeginPoiseReaction(bool isGuardBreak, MonoBehaviour attacker)
    {
        if (_enemyStat.IsDead || _enemyState.ActionState == EnemyActionState.Dead || IsInPoiseReaction) return;

        // 경직 시작 시점의 방향만 전달하며, 반응 도중에는 모션을 바꾸지 않는다.
        bool isHitFromBehind = !isGuardBreak && CombatUtility.IsAttackerBehind(transform, attacker ? attacker.transform : null);

        _attackHitbox?.EndAttack();
        _patternExecutor.CancelCurrentPattern(true);
        _enemyAnimation.CancelGuard();
        _circleBlockedTime = 0f;
        _movementHitPauseRemaining = 0f;
        float duration = isGuardBreak ? _enemyStat.GuardBreakDuration : _enemyStat.StaggerDuration;
        float moveDistance = isGuardBreak ? _enemyStat.GuardBreakMoveDistance : _enemyStat.StaggerMoveDistance;
        _poiseReaction.Begin(transform, attacker ? attacker.transform : null, duration, moveDistance);
        _enemyState.SetActionState(isGuardBreak ? EnemyActionState.GuardBroken : EnemyActionState.Stunned);

        // 애니메이션 설정이 누락되어도 행동 제한과 복귀 타이머는 유지한다.
        if (!_enemyAnimation.TryPlayPoiseReaction(isGuardBreak, isHitFromBehind))
        {
            _enemyAnimation.CompleteAttack();
            Debug.LogWarning("적 Animator에 Stagger 또는 GuardBreak 상태가 없습니다.", this);
        }
    }

    private void UpdatePoiseReaction(float deltaTime)
    {
        Vector3 displacement = _poiseReaction.Advance(deltaTime);
        _enemyMove.MovePoiseReaction(displacement, deltaTime);
        _enemyAnimation.SetMoveAmount(0f);

        if (_poiseReaction.IsRunning) return;
        // Free로 전환하기 전에 현재 반응 종류에 맞는 강인도를 복구한다.
        _enemyStat.RestorePoiseAfterReaction(_enemyState.ActionState == EnemyActionState.GuardBroken);

        _enemyAnimation.CompleteAttack();
        _enemyState.SetActionState(EnemyActionState.Free);
    }

    private void UpdateStationary(float deltaTime)
    {
        _enemyMove.MoveStationary(deltaTime);
        _enemyAnimation.SetMoveAmount(0f);
    }

    private void HandleDied()
    {
        if (_deathPresentationStarted) return;
        _deathPresentationStarted = true;
        // 상태 변경이나 풀 반환으로 참조가 정리되기 전에 이번 생명주기의 보상을 확보한다.
        PlayerManager rewardRecipient = _rewardRecipient;
        _rewardRecipient = null;
        // Definition이 없는 적도 사망 처리를 마치며, 지급할 보상만 0으로 처리한다.
        int experienceReward = _enemyDefinition ? _enemyDefinition.ExperienceReward : 0;
        int moneyReward = rewardRecipient && _enemyDefinition ? _enemyDefinition.GetMoneyReward() : 0;

        if (_enemyState != null)
        {
            _enemyState.SetActionState(EnemyActionState.Dead);
        }

        if (_enemyState != null)
        {
            _enemyState.SetBehaviorState(EnemyBehaviorState.Idle);
        }

        _target = null;
        if (_hurtbox) _hurtbox.SetDamageEnabled(false);
        if (_lockOnTarget)
        {
            _lockOnEnabledBeforeDeath = _lockOnTarget.enabled;
            _lockOnTarget.enabled = false;
        }

        if (_attackHitbox)
        {
            _attackHitbox.EndAttack();
        }

        if (_patternExecutor)
        {
            _patternExecutor.CancelCurrentPattern(false);
        }

        _poiseReaction.Reset();
        _circleBlockedTime = 0f;
        _movementHitPauseRemaining = 0f;
        // 사망 중복 방지 이후에 지급하므로 추가 피격이나 사망 알림으로 보상이 중복되지 않는다.
        if (rewardRecipient)
        {
            rewardRecipient.AddExperience(experienceReward);
            rewardRecipient.AddMoney(moneyReward);
        }
        // 생존 적 수는 사망 연출과 풀 반환을 기다리지 않고 실제 사망 시점에 감소한다.
        Died?.Invoke(this);
        if (!_enemyAnimation || !_enemyAnimation.TryPlayDeath())
        {
            Debug.LogWarning("적 Animator의 Dead 상태 또는 사망 클립이 없어 사망 연출을 종료합니다.", this);
            FinishDeath();
        }
    }

    private void FinishDeath()
    {
        // 씬에 직접 배치되어 풀 소유자가 없는 적도 연출 후 비활성화한다.
        if (!ReturnToPool()) gameObject.SetActive(false);
    }
}
