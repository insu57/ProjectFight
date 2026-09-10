using System;
using UnityEngine;

public enum EnemyPatternStepType
{
    Attack = 0,
    Guard = 1,
    CircleTarget = 2
}

public enum EnemyCircleDirection
{
    Random = 0,
    Left = 1,
    Right = 2
}

[Serializable]
public struct EnemyPatternStep
{
    // 기본값 0을 공격으로 유지하여 기존 패턴 에셋을 보존한다.
    [field: SerializeField] public EnemyPatternStepType Type { get; private set; }
    [field: SerializeField] public AttackDefinition Attack { get; private set; }
    [field: SerializeField, Min(0f)] public float DelayBeforeAttack { get; private set; }
    [field: SerializeField, Min(0f)] public float DelayAfterAttack { get; private set; }

    // 가드와 경계 단계는 공격 설정 대신 유지 시간을 사용한다.
    [field: SerializeField, Min(0f)] public float Duration { get; private set; }
    [field: SerializeField, Min(0f)] public float CircleSpeed { get; private set; }
    [field: SerializeField, Min(0f)] public float CircleDistance { get; private set; }
    [field: SerializeField, Min(0f)] public float CircleDistanceTolerance { get; private set; }
    [field: SerializeField] public EnemyCircleDirection CircleDirection { get; private set; }
}

[CreateAssetMenu(fileName = "EnemyPatternDefinition", menuName = "Enemy/Pattern Definition")]
public sealed class EnemyPatternDefinition : ScriptableObject
{
    [field: SerializeField] public int PatternId { get; private set; }
    [field: SerializeField] public string PatternName { get; private set; }
    [field: SerializeField, Min(0f)] public float MinimumRange { get; private set; }
    [field: SerializeField, Min(0f)] public float MaximumRange { get; private set; } = 2.5f;
    [field: SerializeField, Min(0f)] public float Cooldown { get; private set; } = 1f;
    [field: SerializeField, Min(0.01f)] public float SelectionWeight { get; private set; } = 1f;
    [field: SerializeField] public EnemyPatternStep[] Steps { get; private set; } = Array.Empty<EnemyPatternStep>();

    public bool HasSteps => Steps != null && Steps.Length > 0;

    private void OnValidate()
    {
        MinimumRange = Mathf.Max(0f, MinimumRange);
        MaximumRange = Mathf.Max(MinimumRange, MaximumRange);
        Cooldown = Mathf.Max(0f, Cooldown);
        SelectionWeight = Mathf.Max(0.01f, SelectionWeight);
    }
}
