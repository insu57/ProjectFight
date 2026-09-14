using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPatternExecutor : MonoBehaviour
{
    private enum ExecutionPhase
    {
        BeforeStep,
        Active,
        AfterStep
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
    public bool IsAttackPlaying => IsExecuting && _phase == ExecutionPhase.Active && CurrentStep.Type == EnemyPatternStepType.Attack;
    public bool IsGuarding => IsExecuting && _phase == ExecutionPhase.Active && CurrentStep.Type == EnemyPatternStepType.Guard;
    public bool IsCircling => IsExecuting && _phase == ExecutionPhase.Active && CurrentStep.Type == EnemyPatternStepType.CircleTarget;
    public EnemyPatternStep CurrentStep => IsExecuting ? _currentPattern.Steps[_currentStepIndex] : default;
    public float CircleDirectionSign { get; private set; }

    private void Awake()
    {
        SetPatterns(_patterns);
    }

    public void SetPatterns(EnemyPatternDefinition[] patterns)
    {
        CancelCurrentPattern(false);
        _patterns = patterns ?? Array.Empty<EnemyPatternDefinition>();
        _remainingCooldowns = new float[_patterns.Length];
    }

    public void UpdateCooldowns(float deltaTime)
    {
        if (_remainingCooldowns == null) return;
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
        IsExecuting = true;
        PrepareCurrentStep();
        return true;
    }

    private void PrepareCurrentStep()
    {
        EnemyPatternStep step = CurrentStep;
        _phase = ExecutionPhase.BeforeStep;
        _currentAttack = null;
        _previousAttackMoveProgress = 0f;
        _remainingDelay = step.Type == EnemyPatternStepType.Attack ? Mathf.Max(0f, step.DelayBeforeAttack) : 0f;
        CircleDirectionSign = 0f;

        if (step.Type != EnemyPatternStepType.CircleTarget) return;

        // 방향은 단계 시작 시 한 번만 정하고 개별 적의 실행 상태에 보관한다.
        CircleDirectionSign = step.CircleDirection switch
        {
            EnemyCircleDirection.Left => -1f,
            EnemyCircleDirection.Right => 1f,
            _ => UnityEngine.Random.value < 0.5f ? -1f : 1f
        };
    }

    public void UpdateExecution(float deltaTime, bool isAnimationFinished, out bool stepStarted, out bool stepEnded, out bool patternCompleted)
    {
        stepStarted = false;
        stepEnded = false;
        patternCompleted = false;

        if (!IsExecuting || deltaTime <= 0f)
        {
            return;
        }

        EnemyPatternStep step = CurrentStep;

        if (_phase == ExecutionPhase.BeforeStep)
        {
            _remainingDelay -= deltaTime;

            if (_remainingDelay <= 0f)
            {
                _currentAttack = step.Type == EnemyPatternStepType.Attack ? step.Attack : null;
                _previousAttackMoveProgress = 0f;
                _phase = ExecutionPhase.Active;
                _remainingDelay = Mathf.Max(0f, step.Duration);
                stepStarted = true;
            }

            return;
        }

        if (_phase == ExecutionPhase.Active)
        {
            if (step.Type == EnemyPatternStepType.Attack)
            {
                if (!isAnimationFinished) return;
            }
            else
            {
                _remainingDelay -= deltaTime;
                if (_remainingDelay > 0f) return;
            }

            _currentAttack = null;
            _phase = ExecutionPhase.AfterStep;
            _remainingDelay = step.Type == EnemyPatternStepType.Attack ? Mathf.Max(0f, step.DelayAfterAttack) : 0f;
            stepEnded = true;
            return;
        }

        // 방어 판정 해제 후에도 가드 종료 모션이 끝날 때까지 패턴을 유지한다.
        if (step.Type == EnemyPatternStepType.Guard && !isAnimationFinished) return;

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

        PrepareCurrentStep();
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

        if (!pattern || !pattern.HasSteps || _remainingCooldowns[patternIndex] > 0f) //유효확인
        {
            return false;
        }

        float minimumRangeSqr = pattern.MinimumRange * pattern.MinimumRange;
        float maximumRangeSqr = pattern.MaximumRange * pattern.MaximumRange;//거리체크
        if (targetSqrDistance < minimumRangeSqr || targetSqrDistance > maximumRangeSqr) return false;

        // 공격 설정이 없는 단계를 선택하여 완료를 영원히 기다리는 상황을 막는다.
        for (int i = 0; i < pattern.Steps.Length; i++)
        {
            EnemyPatternStep step = pattern.Steps[i];
            if (step.Type == EnemyPatternStepType.Attack)
            {
                if (!step.Attack) return false;
            }
        }

        return true;
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
        CircleDirectionSign = 0f;
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
