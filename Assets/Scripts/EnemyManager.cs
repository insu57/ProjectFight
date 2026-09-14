using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyState))]
[RequireComponent(typeof(EnemyStat))]
[RequireComponent(typeof(EnemyBrain))]
[RequireComponent(typeof(EnemyMove))]
[RequireComponent(typeof(EnemyAnimation))]
[RequireComponent(typeof(EnemyPatternExecutor))]
public sealed class EnemyManager : MonoBehaviour, IHitStopReceiver
{
    private Transform _target;
    private AttackHitbox _attackHitbox;
    private Hurtbox _hurtbox;
    [field: SerializeField] private EnemyDefinition _enemyDefinition;
    private EnemyState _enemyState;
    private EnemyStat _enemyStat;
    private EnemyBrain _enemyBrain;
    private EnemyMove _enemyMove;
    private EnemyAnimation _enemyAnimation;
    private EnemyPatternExecutor _patternExecutor;
    private bool _hasStarted;
    private float _circleBlockedTime;
    private float _poiseReactionRemaining;
    // 반응 시작 시 결정한 월드 방향과 속도를 유지한다.
    private Vector3 _poiseReactionVelocity;

    private bool IsInPoiseReaction => _enemyState.ActionState == EnemyActionState.Stunned || _enemyState.ActionState == EnemyActionState.GuardBroken;

    private void Awake()
    {
        TryGetComponent(out _enemyState);
        TryGetComponent(out _enemyStat);
        TryGetComponent(out _enemyBrain);
        TryGetComponent(out _enemyMove);
        TryGetComponent(out _enemyAnimation);
        TryGetComponent(out _patternExecutor);

        _attackHitbox = GetComponentInChildren<AttackHitbox>();
        _hurtbox = GetComponentInChildren<Hurtbox>();

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
        _hasStarted = true;

        if (!_enemyDefinition) return;

        _enemyStat.InitializePoiseStats(_enemyDefinition);
        _patternExecutor.SetPatterns(_enemyDefinition.Patterns);
    }

    private void OnDisable()
    {
        _patternExecutor?.CancelCurrentPattern(false);
        _poiseReactionRemaining = 0f;
        _poiseReactionVelocity = Vector3.zero;
        _circleBlockedTime = 0f;

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
        _patternExecutor.UpdateCooldowns(deltaTime);

        if (_enemyState.ActionState == EnemyActionState.Dead)
        {
            return;
        }

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
            UpdatePattern(deltaTime);
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

        _enemyStat.InitializePoiseStats(_enemyDefinition);
    }

    public void TakeDamage(float damage)
    {
        if (_enemyStat)
        {
            _enemyStat.TakeDamage(damage);
        }
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

        // 체력 감소와 기존 Died 이벤트 처리를 재사용한다.
        TakeDamage(damageInfo.Damage);

        // 사망은 기절보다 우선한다.
        if (!_enemyStat.IsDead && damageInfo.PoiseDamage > 0f && _enemyStat.CurrentPoise <= 0f)
        {
            BeginPoiseReaction(false, damageInfo.Attacker);
        }
    }

    private bool CanBlock(DamageInfo damageInfo)
    {
        if (_enemyState.ActionState != EnemyActionState.Guarding) return false;
        if (!_patternExecutor.IsGuarding || !damageInfo.Attacker) return false;

        Vector3 toAttacker = damageInfo.Attacker.transform.position - transform.position;
        toAttacker.y = 0f;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (toAttacker.sqrMagnitude <= 0.0001f || forward.sqrMagnitude <= 0.0001f) return false;

        // 플레이어 가드와 동일하게 전방 180도만 방어하며 종료 모션 중에는 막지 않는다.
        return Vector3.Dot(forward, toAttacker) > 0f;
    }

    private void UpdatePattern(float deltaTime)
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
            step = _patternExecutor.CurrentStep;
            Vector2 movement = _enemyMove.MoveCircleTarget(_target.position, step.CircleSpeed, step.CircleDistance,
                step.CircleDistanceTolerance, _patternExecutor.CircleDirectionSign, deltaTime);
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
                if (!_enemyAnimation.TryPlayAttack(step.Attack)) return false;
                _enemyState.SetActionState(EnemyActionState.Attacking);
                _attackHitbox?.BeginAttack(step.Attack);
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

        _attackHitbox?.EndAttack();
        _patternExecutor.CancelCurrentPattern(true);
        _enemyAnimation.CancelGuard();
        _circleBlockedTime = 0f;
        _poiseReactionRemaining = Mathf.Max(0f, isGuardBreak ? _enemyStat.GuardBreakDuration : _enemyStat.StaggerDuration);

        float moveDistance = isGuardBreak ? _enemyStat.GuardBreakMoveDistance : _enemyStat.StaggerMoveDistance;

        // 공격자 위치에서 피격자 위치로 향하는 방향으로 밀려난다.
        Vector3 pushDirection = attacker ? transform.position - attacker.transform.position : -transform.forward;
        pushDirection.y = 0f;

        // 두 위치가 겹쳐 방향을 구할 수 없으면 피격자의 뒤쪽을 사용한다.
        if (pushDirection.sqrMagnitude <= 0.0001f)
        {
            pushDirection = -transform.forward;
            pushDirection.y = 0f;
        }

        pushDirection.Normalize();

        // 시작 순간의 방향과 속도를 저장하여 공격자가 움직여도 그대로 유지한다.
        _poiseReactionVelocity = _poiseReactionRemaining > 0f ? pushDirection * (Mathf.Max(0f, moveDistance) / _poiseReactionRemaining) : Vector3.zero;
        _enemyState.SetActionState(isGuardBreak ? EnemyActionState.GuardBroken : EnemyActionState.Stunned);

        // 애니메이션 설정이 누락되어도 행동 제한과 복귀 타이머는 유지한다.
        if (!_enemyAnimation.TryPlayPoiseReaction(isGuardBreak))
        {
            _enemyAnimation.CompleteAttack();
            Debug.LogWarning("적 Animator에 Stagger 또는 GuardBreak 상태가 없습니다.", this);
        }
    }

    private void UpdatePoiseReaction(float deltaTime)
    {
        // 마지막 프레임에서도 남은 반응 시간만큼만 이동한다.
        float movementDeltaTime = Mathf.Min(deltaTime, _poiseReactionRemaining);
        _enemyMove.MovePoiseReaction(_poiseReactionVelocity * movementDeltaTime, deltaTime);
        _enemyAnimation.SetMoveAmount(0f);

        _poiseReactionRemaining = Mathf.Max(0f, _poiseReactionRemaining - deltaTime);
        if (_poiseReactionRemaining > 0f) return;

        _poiseReactionVelocity = Vector3.zero;
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
        if (_attackHitbox)
        {
            _attackHitbox.EndAttack();
        }

        _patternExecutor?.CancelCurrentPattern(false);
        _poiseReactionRemaining = 0f;
        _poiseReactionVelocity = Vector3.zero;
        _circleBlockedTime = 0f;
        _enemyAnimation?.CancelGuard();
        _enemyAnimation?.CompleteAttack();
        _enemyState?.SetBehaviorState(EnemyBehaviorState.Idle);
        _enemyState?.SetActionState(EnemyActionState.Dead);
    }
}
