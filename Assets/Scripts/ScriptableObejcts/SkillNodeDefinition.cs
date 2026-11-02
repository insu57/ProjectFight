using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillTreeNodeType
{
    Main = 0,
    Sub = 1,
    Start = 2
}

public enum SkillTreeEffectType
{
    // SO에 저장된 효과 번호를 유지한다.
    StatModify = 7,
    PassiveUnlock = 4,
    LethalArtUnlock = 5,
    None = 6,
    ComboUnlock = 8
}

[CreateAssetMenu(fileName = "SkillNodeDefinition", menuName = "Scriptable Objects/SkillNodeDefinition")]
public sealed class SkillNodeDefinition : ScriptableObject
{
    [field: SerializeField] public int NodeID { get; private set; }
    [field: SerializeField] public string NodeName { get; private set; }
    [field: SerializeField] public Sprite NodeIcon { get; private set; }
    [field: SerializeField] public string NodeDescription { get; private set; }
    [field: SerializeField] public SkillTreeNodeType NodeType { get; private set; }

    // 선행 조건은 여러 부모를 지원하는 ID 목록으로만 관리한다.
    [SerializeField] private List<int> _requiredNodeIds = new();
    // 직렬화된 목록이 null이어도 임시 컬렉션 없이 빈 선행 조건으로 조회한다.
    public IReadOnlyList<int> RequiredNodeIds => _requiredNodeIds != null ? _requiredNodeIds : Array.Empty<int>();
    [field: SerializeField] public SkillTreeEffectType EffectType { get; private set; } = SkillTreeEffectType.None;

    [field: SerializeField, Tooltip("ComboUnlock은 해금할 ComboDefinition의 ComboID를, " +
        "LethalArtUnlock은 LethalArtDefinition의 LethalArtId를 사용합니다. " +
        "PassiveUnlock은 패시브 해금용 정수 값이며, 스탯 변경은 별도의 스탯 보정 목록에서 설정합니다.")]
    public int EffectValue { get; private set; }
    // 한 노드에서 여러 스탯의 고정값·비율 보정을 설정하며 공유 데이터를 복사하지 않고 조회한다.
    [SerializeField] private StatModifier[] _statModifiers = Array.Empty<StatModifier>();
    public IReadOnlyList<StatModifier> StatModifiers => _statModifiers ?? Array.Empty<StatModifier>();
    [field: SerializeField] public int RequiredPoints { get; private set; }
    [field: SerializeField] public int DisplayOrder { get; private set; }
}
