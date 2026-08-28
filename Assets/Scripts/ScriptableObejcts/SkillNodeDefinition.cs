using UnityEngine;

public enum SkillTreeNodeType
{
    Main = 0,
    Sub = 1,
    Start = 2
}

public enum SkillTreeEffectType
{
    MaxHPAdd = 0,
    AttackAdd = 1,
    DefenseAdd = 2,
    MaxEnergyAdd = 3,
    PassiveUnlock = 4,
    ActiveUnlock = 5,
    None = 6
}

[CreateAssetMenu(fileName = "SkillNodeDefinition", menuName = "Scriptable Objects/SkillNodeDefinition")]
public sealed class SkillNodeDefinition : ScriptableObject
{
    [field: SerializeField] public int NodeID { get; private set; }
    [field: SerializeField] public string NodeName { get; private set; }
    [field: SerializeField] public Sprite NodeIcon { get; private set; }
    [field: SerializeField] public string NodeDescription { get; private set; }
    [field: SerializeField] public SkillTreeNodeType NodeType { get; private set; }
    [field: SerializeField] public int RequiredNodeId { get; private set; } = -1;
    [field: SerializeField] public SkillTreeEffectType EffectType { get; private set; } = SkillTreeEffectType.None;
    [field: SerializeField] public int EffectValue { get; private set; }
    [field: SerializeField] public int RequiredPoints { get; private set; }
    [field: SerializeField] public int DisplayOrder { get; private set; }
}
