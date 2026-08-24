using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerSkillTree : MonoBehaviour
{
    [SerializeField] private SkillTreeDefinition _definition;
    [SerializeField, Min(0)] private int _initialPoints;

    public SkillTreeRuntime Runtime { get; private set; }

    private void Awake()
    {
        if (!_definition)
        {
            Debug.LogError("PlayerSkillTree에 SkillTreeDefinition이 지정되지 않았습니다.", this);
            return;
        }

        Runtime = SkillTreeRuntime.Create(_definition, _initialPoints);
    }

    public SkillTreeUnlockResult TryUnlockNode(int nodeId)
    {
        if (Runtime == null)
        {
            return SkillTreeUnlockResult.RuntimeNotInitialized;
        }

        return Runtime.TryUnlockNode(nodeId);
    }

    public void AddPoints(int amount)
    {
        if (Runtime == null)
        {
            Debug.LogError("스킬 트리 런타임이 생성되지 않았습니다.", this);
            return;
        }

        Runtime.AddPoints(amount);
    }
}
