using System.Collections.Generic;

public sealed class SkillTreeNodeRuntime
{
    private readonly List<SkillTreeNodeRuntime> _nextNodes = new();

    public SkillNodeDefinition Definition { get; }
    public SkillTreeNodeRuntime RequiredNode { get; private set; }
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

        if (RequiredNode == null || !RequiredNode.IsUnlocked)
        {
            return false;
        }

        return availablePoints >= Definition.RequiredPoints;
    }

    internal void SetRequiredNode(SkillTreeNodeRuntime requiredNode)
    {
        RequiredNode = requiredNode;
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
