using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
public sealed class SkillNodeDefinition : ScriptableObject, ISerializationCallbackReceiver
{
    [field: SerializeField] public int NodeID { get; private set; }
    [field: SerializeField] public string NodeName { get; private set; }
    [field: SerializeField] public Sprite NodeIcon { get; private set; }
    [field: SerializeField] public string NodeDescription { get; private set; }
    [field: SerializeField] public SkillTreeNodeType NodeType { get; private set; }
    [SerializeField] private List<int> _requiredNodeIds = new();
    public IReadOnlyList<int> RequiredNodeIds => _requiredNodeIds;
    // 이전 SO의 단일 부모를 읽어 목록으로 옮긴다. 이전 필드는 다시 편집하지 않는다.
    [SerializeField, HideInInspector, FormerlySerializedAs("<RequiredNodeId>k__BackingField")] private int _legacyRequiredNodeId = -1;
    [field: SerializeField] public SkillTreeEffectType EffectType { get; private set; } = SkillTreeEffectType.None;
    [field: SerializeField] public int EffectValue { get; private set; }
    [field: SerializeField] public int RequiredPoints { get; private set; }
    [field: SerializeField] public int DisplayOrder { get; private set; }

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        _requiredNodeIds ??= new List<int>();
        if (_legacyRequiredNodeId < 0) return;
        if (!_requiredNodeIds.Contains(_legacyRequiredNodeId)) _requiredNodeIds.Add(_legacyRequiredNodeId);
        _legacyRequiredNodeId = -1;
    }
}
