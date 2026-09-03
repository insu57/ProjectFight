using System;
using UnityEngine;

[Serializable]
public struct EnemyPatternStep
{
    [field: SerializeField] public AttackDefinition Attack { get; private set; }
    [field: SerializeField, Min(0f)] public float DelayBeforeAttack { get; private set; }
    [field: SerializeField, Min(0f)] public float DelayAfterAttack { get; private set; }
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
