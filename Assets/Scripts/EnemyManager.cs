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
        if (_enemyAnimation)
        {
            _enemyAnimation.ClearHitStop();
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
            // 이동, 판단, 패턴 진행과 쿨다운을 함께 정지한다.
            return;
        }

        float deltaTime = Time.deltaTime;
        _patternExecutor.UpdateCooldowns(deltaTime);

        if (_enemyState.ActionState == EnemyActionState.Dead)
        {
            return;
        }

        if (_enemyState.ActionState == EnemyActionState.Attacking)
        {
            UpdatePattern(deltaTime);
            return;
        }

        if (!_enemyState.CanDecide || !_target)
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
            _enemyState.SetActionState(EnemyActionState.Attacking);
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
        if (damageInfo.Damage <= 0f) return;

        // 유효한 피격을 확정했으므로 양쪽에 같은 정지 시간을 적용한다.
        damageInfo.ApplyHitStop(this);

        // 체력 감소와 기존 Died 이벤트 처리를 재사용한다.
        TakeDamage(damageInfo.Damage);
    }

    private void UpdatePattern(float deltaTime)
    {
        bool isAttackFinished = _enemyAnimation.IsAttackFinished();
        _patternExecutor.UpdateExecution(deltaTime, isAttackFinished, out AttackDefinition attackToPlay, out bool patternCompleted);

        bool startedAttack = false;

        if (attackToPlay)
        {
            if (!_enemyAnimation.TryPlayAttack(attackToPlay))
            {
                if (_attackHitbox)
                {
                    _attackHitbox.EndAttack();
                }

                _patternExecutor.CancelCurrentPattern(true);
                _enemyState.SetActionState(EnemyActionState.Free);
                UpdateStationary(deltaTime);
                return;
            }

            if (_attackHitbox)
            {
                _attackHitbox.BeginAttack(attackToPlay);
            }

            startedAttack = true;
        }

        float normalizedTime = 0f;

        // 새 공격 요청 프레임에는 이전 애니메이션의 진행도를 사용하지 않는다.
        bool hasNormalizedTime = !startedAttack
            && _patternExecutor.IsAttackPlaying
            && _enemyAnimation.TryGetAttackNormalizedTime(out normalizedTime);

        if (hasNormalizedTime && _patternExecutor.TryGetAttackMoveDisplacement(normalizedTime, out Vector2 localDisplacement))
        {
            _enemyMove.MoveAttack(localDisplacement, deltaTime);
        }
        else
        {
            if (_target && !_patternExecutor.IsAttackPlaying)
            {
                _enemyMove.FaceTarget(_target.position, deltaTime);
            }

            _enemyMove.MoveStationary(deltaTime);
        }

        _enemyAnimation.SetMoveAmount(0f);

        if (_attackHitbox)
        {
            if (_patternExecutor.IsAttackPlaying)
            {
                _attackHitbox.UpdateHitbox(hasNormalizedTime, normalizedTime);
            }
            else
            {
                // 공격 후 대기 구간에 들어간 시점부터 박스를 끈다.
                _attackHitbox.EndAttack();
            }
        }

        if (patternCompleted)
        {
            _enemyAnimation.CompleteAttack();
            _enemyState.SetActionState(EnemyActionState.Free);
        }
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
        _enemyAnimation?.CompleteAttack();
        _enemyState?.SetBehaviorState(EnemyBehaviorState.Idle);
        _enemyState?.SetActionState(EnemyActionState.Dead);
    }
}
