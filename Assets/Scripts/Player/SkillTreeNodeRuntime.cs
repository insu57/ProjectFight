using System.Collections.Generic;

public sealed class SkillTreeNodeRuntime
{
    private readonly List<SkillTreeNodeRuntime> _nextNodes = new();
    private readonly List<SkillTreeNodeRuntime> _requiredNodes = new();

    public SkillNodeDefinition Definition { get; }
    public IReadOnlyList<SkillTreeNodeRuntime> RequiredNodes => _requiredNodes;
    public IReadOnlyList<SkillTreeNodeRuntime> NextNodes => _nextNodes;
    public bool IsUnlocked { get; private set; }

    public int NodeID => Definition.NodeID;
    public bool IsStartNode => Definition.NodeType == SkillTreeNodeType.Start;

    internal SkillTreeNodeRuntime(SkillNodeDefinition definition)
    {
        Definition = definition;
    }

    public bool CanUnlock(int availablePoints)
    {
        if (IsStartNode || IsUnlocked)
        {
            return false;
        }

        if (!HasUnlockedRequiredNode)
        {
            return false;
        }

        return availablePoints >= Definition.RequiredPoints;
    }

    // 여러 선행 경로 중 하나만 해금되어도 진입할 수 있다. 조회 중에는 할당하지 않는다.
    public bool HasUnlockedRequiredNode
    {
        get
        {
            for (int i = 0; i < _requiredNodes.Count; i++)
            {
                if (_requiredNodes[i].IsUnlocked)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal void AddRequiredNode(SkillTreeNodeRuntime requiredNode)
    {
        _requiredNodes.Add(requiredNode);
        requiredNode._nextNodes.Add(this);
    }

    internal void SortNextNodes()
    {
        _nextNodes.Sort(CompareDisplayOrder);
    }

    internal void Unlock()
    {
        IsUnlocked = true;
    }

    private static int CompareDisplayOrder(SkillTreeNodeRuntime left, SkillTreeNodeRuntime right)
    {
        int orderComparison = left.Definition.DisplayOrder.CompareTo(right.Definition.DisplayOrder);

        if (orderComparison != 0)
        {
            return orderComparison;
        }

        return left.NodeID.CompareTo(right.NodeID);
    }
}
