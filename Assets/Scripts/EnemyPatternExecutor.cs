using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPatternExecutor : MonoBehaviour
{
    private enum ExecutionPhase
    {
        DelayBeforeAttack,
        Attacking,
        DelayAfterAttack
    }

    [SerializeField] private EnemyPatternDefinition[] _patterns = Array.Empty<EnemyPatternDefinition>();

    private float[] _remainingCooldowns;
    private EnemyPatternDefinition _currentPattern;
    private AttackDefinition _currentAttack;
    private ExecutionPhase _phase;
    private int _currentPatternIndex = -1;
    private int _currentStepIndex;
    private float _remainingDelay;
    private float _previousAttackMoveProgress;

    public bool IsExecuting { get; private set; }
    public bool IsAttackPlaying => IsExecuting && _phase == ExecutionPhase.Attacking;

    private void Awake()
    {
        _remainingCooldowns = new float[_patterns.Length];
    }

    public void UpdateCooldowns(float deltaTime)
    {
        for (int i = 0; i < _remainingCooldowns.Length; i++)
        {
            if (_remainingCooldowns[i] > 0f)
            {
                _remainingCooldowns[i] = Mathf.Max(0f, _remainingCooldowns[i] - deltaTime);
            }
        }
    }

    public bool TryBeginPattern(float targetSqrDistance)
    {
        if (IsExecuting || !TrySelectPatternIndex(targetSqrDistance, out int patternIndex))
        {
            return false;
        }

        _currentPatternIndex = patternIndex;
        _currentPattern = _patterns[patternIndex];
        _currentStepIndex = 0;
        _phase = ExecutionPhase.DelayBeforeAttack;
        _remainingDelay = _currentPattern.Steps[0].DelayBeforeAttack;
        _previousAttackMoveProgress = 0f;
        IsExecuting = true;
        return true;
    }

    public void UpdateExecution(float deltaTime, bool isAttackFinished, out AttackDefinition attackToPlay, out bool patternCompleted)
    {
        attackToPlay = null;
        patternCompleted = false;

        if (!IsExecuting)
        {
            return;
        }

        EnemyPatternStep step = _currentPattern.Steps[_currentStepIndex];

        if (_phase == ExecutionPhase.DelayBeforeAttack)
        {
            _remainingDelay -= deltaTime;

            if (_remainingDelay <= 0f)
            {
                _currentAttack = step.Attack;
                _previousAttackMoveProgress = 0f;
                _phase = ExecutionPhase.Attacking;
                attackToPlay = _currentAttack;
            }

            return;
        }

        if (_phase == ExecutionPhase.Attacking)
        {
            if (!isAttackFinished)
            {
                return;
            }

            _currentAttack = null;
            _phase = ExecutionPhase.DelayAfterAttack;
            _remainingDelay = step.DelayAfterAttack;
            return;
        }

        _remainingDelay -= deltaTime;

        if (_remainingDelay > 0f)
        {
            return;
        }

        _currentStepIndex++;

        if (_currentStepIndex >= _currentPattern.Steps.Length)
        {
            CompletePattern();
            patternCompleted = true;
            return;
        }

        _phase = ExecutionPhase.DelayBeforeAttack;
        _remainingDelay = _currentPattern.Steps[_currentStepIndex].DelayBeforeAttack;
    }

    public bool TryGetAttackMoveDisplacement(float normalizedTime, out Vector2 localDisplacement)
    {
        localDisplacement = Vector2.zero;

        if (!IsAttackPlaying || !_currentAttack)
        {
            return false;
        }

        float moveProgress = EvaluateAttackMoveProgress(_currentAttack, normalizedTime);
        float progressDelta = Mathf.Max(0f, moveProgress - _previousAttackMoveProgress);
        _previousAttackMoveProgress = moveProgress;
        localDisplacement = _currentAttack.AttackMoveDistance * progressDelta;
        return true;
    }

    public void CancelCurrentPattern(bool startCooldown)
    {
        if (!IsExecuting)
        {
            return;
        }

        if (startCooldown && _currentPatternIndex >= 0)
        {
            _remainingCooldowns[_currentPatternIndex] = _currentPattern.Cooldown;
        }

        ClearCurrentPattern();
    }

    private bool TrySelectPatternIndex(float targetSqrDistance, out int selectedIndex)
    {
        selectedIndex = -1;
        float totalWeight = 0f;

        for (int i = 0; i < _patterns.Length; i++)
        {
            if (IsPatternAvailable(i, targetSqrDistance))
            {
                totalWeight += _patterns[i].SelectionWeight;
            }
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        float randomWeight = UnityEngine.Random.value * totalWeight;

        for (int i = 0; i < _patterns.Length; i++)
        {
            if (!IsPatternAvailable(i, targetSqrDistance))
            {
                continue;
            }

            randomWeight -= _patterns[i].SelectionWeight;

            if (randomWeight <= 0f)
            {
                selectedIndex = i;
                return true;
            }
        }

        return false;
    }

    private bool IsPatternAvailable(int patternIndex, float targetSqrDistance)
    {
        EnemyPatternDefinition pattern = _patterns[patternIndex];

        if (!pattern || !pattern.HasSteps || _remainingCooldowns[patternIndex] > 0f)
        {
            return false;
        }

        float minimumRangeSqr = pattern.MinimumRange * pattern.MinimumRange;
        float maximumRangeSqr = pattern.MaximumRange * pattern.MaximumRange;
        return targetSqrDistance >= minimumRangeSqr && targetSqrDistance <= maximumRangeSqr;
    }

    private void CompletePattern()
    {
        _remainingCooldowns[_currentPatternIndex] = _currentPattern.Cooldown;
        ClearCurrentPattern();
    }

    private void ClearCurrentPattern()
    {
        _currentPattern = null;
        _currentAttack = null;
        _currentPatternIndex = -1;
        _currentStepIndex = 0;
        _remainingDelay = 0f;
        _previousAttackMoveProgress = 0f;
        IsExecuting = false;
    }

    private static float EvaluateAttackMoveProgress(AttackDefinition attackDefinition, float normalizedTime)
    {
        float moveStartTime = attackDefinition.MoveStartNormalizedTime;
        float moveEndTime = attackDefinition.MoveEndNormalizedTime;

        if (normalizedTime <= moveStartTime)
        {
            return 0f;
        }

        if (normalizedTime >= moveEndTime || moveEndTime <= moveStartTime)
        {
            return 1f;
        }

        float moveNormalizedTime = Mathf.InverseLerp(moveStartTime, moveEndTime, normalizedTime);
        AnimationCurve moveCurve = attackDefinition.MoveCurve;
        return moveCurve != null ? moveCurve.Evaluate(moveNormalizedTime) : moveNormalizedTime;
    }
}
