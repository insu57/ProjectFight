using UnityEngine;

[CreateAssetMenu(fileName = "SkillTreeDefinition", menuName = "Scriptable Objects/SkillTreeDefinition")]
public sealed class SkillTreeDefinition : ScriptableObject
{
    [field: SerializeField] public int StartNodeID { get; private set; } = 0;
    [field: SerializeField] public SkillNodeDefinition[] Nodes { get; private set; }
}
