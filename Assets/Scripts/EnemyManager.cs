using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyState))]
[RequireComponent(typeof(EnemyStat))]
[RequireComponent(typeof(EnemyBrain))]
[RequireComponent(typeof(EnemyMove))]
[RequireComponent(typeof(EnemyAnimation))]
[RequireComponent(typeof(EnemyPatternExecutor))]
public sealed class EnemyManager : MonoBehaviour
{
    private Transform _target;
    [field: SerializeField] private EnemyDefinition _enemyDefinition;
    private EnemyState _enemyState;
    private EnemyStat _enemyStat;
    private EnemyBrain _enemyBrain;
    private EnemyMove _enemyMove;
    private EnemyAnimation _enemyAnimation;
    private EnemyPatternExecutor _patternExecutor;

    private void Awake()
    {
        TryGetComponent(out _enemyState);
        TryGetComponent(out _enemyStat);
        TryGetComponent(out _enemyBrain);
        TryGetComponent(out _enemyMove);
        TryGetComponent(out _enemyAnimation);
        TryGetComponent(out _patternExecutor);
    }

    private void OnEnable()
    {
        if (_enemyStat)
        {
            _enemyStat.Died += HandleDied;
        }
    }

    private void Start()
    {
        _patternExecutor.SetPatterns(_enemyDefinition.Patterns);
    }

    private void OnDisable()
    {
        if (_enemyStat)
        {
            _enemyStat.Died -= HandleDied;
        }
    }

    private void Update()
    {
        if (!_enemyState || !_enemyBrain || !_enemyMove || !_enemyAnimation || !_patternExecutor)
        {
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
    }

    public void TakeDamage(float damage)
    {
        if (_enemyStat)
        {
            _enemyStat.TakeDamage(damage);
        }
    }

    private void UpdatePattern(float deltaTime)
    {
        bool isAttackFinished = _enemyAnimation.IsAttackFinished();
        _patternExecutor.UpdateExecution(deltaTime, isAttackFinished, out AttackDefinition attackToPlay, out bool patternCompleted);

        if (attackToPlay && !_enemyAnimation.TryPlayAttack(attackToPlay))
        {
            _patternExecutor.CancelCurrentPattern(true);
            _enemyState.SetActionState(EnemyActionState.Free);
            UpdateStationary(deltaTime);
            return;
        }

        if (_enemyAnimation.TryGetAttackNormalizedTime(out float normalizedTime)
            && _patternExecutor.TryGetAttackMoveDisplacement(normalizedTime, out Vector2 localDisplacement))
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
        _patternExecutor?.CancelCurrentPattern(false);
        _enemyAnimation?.CompleteAttack();
        _enemyState?.SetBehaviorState(EnemyBehaviorState.Idle);
        _enemyState?.SetActionState(EnemyActionState.Dead);
    }
}
