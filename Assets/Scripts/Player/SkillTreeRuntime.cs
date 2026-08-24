using System;
using System.Collections.Generic;

public enum SkillTreeUnlockResult
{
    Success,
    RuntimeNotInitialized,
    NodeNotFound,
    StartNode,
    AlreadyUnlocked,
    RequiredNodeLocked,
    NotEnoughPoints
}

public sealed class SkillTreeRuntime
{
    private readonly Dictionary<int, SkillTreeNodeRuntime> _nodes;

    public SkillTreeNodeRuntime StartNode { get; }
    public int AvailablePoints { get; private set; }
    public IReadOnlyDictionary<int, SkillTreeNodeRuntime> Nodes => _nodes;

    public event Action<int> AvailablePointsChanged;
    public event Action<SkillTreeNodeRuntime> NodeUnlocked;

    private SkillTreeRuntime(Dictionary<int, SkillTreeNodeRuntime> nodes, SkillTreeNodeRuntime startNode, int initialPoints)
    {
        _nodes = nodes;
        StartNode = startNode;
        AvailablePoints = initialPoints;

        // 시작 노드는 비용이나 효과 없이 최초부터 해금한다.
        StartNode.Unlock();
    }

    public static SkillTreeRuntime Create(SkillTreeDefinition definition, int initialPoints)
    {
        if (!definition) throw new ArgumentNullException(nameof(definition));
        if (initialPoints < 0) throw new ArgumentOutOfRangeException(nameof(initialPoints), "초기 스킬 포인트는 0 이상이어야 합니다.");

        SkillNodeDefinition[] definitions = definition.Nodes;

        if (definitions == null || definitions.Length == 0)
        {
            throw new InvalidOperationException($"스킬 트리 '{definition.name}'에 노드가 없습니다.");
        }

        Dictionary<int, SkillTreeNodeRuntime> nodes = CreateRuntimeNodes(definition, definitions);

        if (!nodes.TryGetValue(definition.StartNodeID, out SkillTreeNodeRuntime startNode))
        {
            throw new InvalidOperationException($"스킬 트리 '{definition.name}'에서 StartNodeID {definition.StartNodeID}를 찾을 수 없습니다.");
        }

        ValidateStartNode(definition, startNode);
        ConnectNodes(definition, definitions, nodes);
        SortNextNodes(nodes);
        ValidateAllNodesReachable(definition, nodes, startNode);

        return new SkillTreeRuntime(nodes, startNode, initialPoints);
    }

    public bool TryGetNode(int nodeId, out SkillTreeNodeRuntime node)
    {
        return _nodes.TryGetValue(nodeId, out node);
    }

    public SkillTreeUnlockResult TryUnlockNode(int nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out SkillTreeNodeRuntime node))
        {
            return SkillTreeUnlockResult.NodeNotFound;
        }

        if (node.IsStartNode)
        {
            return SkillTreeUnlockResult.StartNode;
        }

        if (node.IsUnlocked)
        {
            return SkillTreeUnlockResult.AlreadyUnlocked;
        }

        if (node.RequiredNode == null || !node.RequiredNode.IsUnlocked)
        {
            return SkillTreeUnlockResult.RequiredNodeLocked;
        }

        if (AvailablePoints < node.Definition.RequiredPoints)
        {
            return SkillTreeUnlockResult.NotEnoughPoints;
        }

        AvailablePoints -= node.Definition.RequiredPoints;
        node.Unlock();

        AvailablePointsChanged?.Invoke(AvailablePoints);
        NodeUnlocked?.Invoke(node);
        return SkillTreeUnlockResult.Success;
    }

    public void AddPoints(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "추가할 스킬 포인트는 0보다 커야 합니다.");

        AvailablePoints += amount;
        AvailablePointsChanged?.Invoke(AvailablePoints);
    }

    private static Dictionary<int, SkillTreeNodeRuntime> CreateRuntimeNodes(SkillTreeDefinition treeDefinition, SkillNodeDefinition[] definitions)
    {
        var nodes = new Dictionary<int, SkillTreeNodeRuntime>(definitions.Length);

        for (int i = 0; i < definitions.Length; i++)
        {
            SkillNodeDefinition definition = definitions[i];

            if (!definition)
            {
                throw new InvalidOperationException($"스킬 트리 '{treeDefinition.name}'의 {i}번 위치에 비어 있는 노드가 있습니다.");
            }

            if (definition.RequiredPoints < 0)
            {
                throw new InvalidOperationException($"노드 '{definition.NodeName}'의 요구 포인트가 음수입니다.");
            }

            if (!nodes.TryAdd(definition.NodeID, new SkillTreeNodeRuntime(definition)))
            {
                throw new InvalidOperationException($"스킬 트리 '{treeDefinition.name}'에 중복된 NodeID가 있습니다: {definition.NodeID}");
            }
        }

        return nodes;
    }

    private static void ValidateStartNode(SkillTreeDefinition treeDefinition, SkillTreeNodeRuntime startNode)
    {
        if (startNode.Definition.NodeType != SkillTreeNodeType.Start)
        {
            throw new InvalidOperationException($"스킬 트리 '{treeDefinition.name}'의 시작 노드는 Start 타입이어야 합니다.");
        }

        if (startNode.Definition.RequiredNodeId >= 0)
        {
            throw new InvalidOperationException($"시작 노드 '{startNode.Definition.NodeName}'는 선행 노드를 가질 수 없습니다.");
        }

        if (startNode.Definition.RequiredPoints != 0)
        {
            throw new InvalidOperationException($"시작 노드 '{startNode.Definition.NodeName}'의 요구 포인트는 0이어야 합니다.");
        }
    }

    private static void ConnectNodes(SkillTreeDefinition treeDefinition, SkillNodeDefinition[] definitions, Dictionary<int, SkillTreeNodeRuntime> nodes)
    {
        for (int i = 0; i < definitions.Length; i++)
        {
            SkillNodeDefinition definition = definitions[i];
            SkillTreeNodeRuntime node = nodes[definition.NodeID];

            if (node.IsStartNode)
            {
                continue;
            }

            if (definition.RequiredNodeId == definition.NodeID)
            {
                throw new InvalidOperationException($"노드 '{definition.NodeName}'가 자기 자신을 선행 노드로 지정하고 있습니다.");
            }

            if (!nodes.TryGetValue(definition.RequiredNodeId, out SkillTreeNodeRuntime requiredNode))
            {
                throw new InvalidOperationException($"스킬 트리 '{treeDefinition.name}'의 노드 '{definition.NodeName}'가 존재하지 않는 선행 노드 ID {definition.RequiredNodeId}를 참조합니다.");
            }

            node.SetRequiredNode(requiredNode);
        }
    }

    private static void ValidateAllNodesReachable(SkillTreeDefinition treeDefinition, Dictionary<int, SkillTreeNodeRuntime> nodes, SkillTreeNodeRuntime startNode)
    {
        var visitedNodeIds = new HashSet<int>();
        var pendingNodes = new Stack<SkillTreeNodeRuntime>();

        pendingNodes.Push(startNode);

        while (pendingNodes.Count > 0)
        {
            SkillTreeNodeRuntime currentNode = pendingNodes.Pop();

            if (!visitedNodeIds.Add(currentNode.NodeID))
            {
                continue;
            }

            IReadOnlyList<SkillTreeNodeRuntime> nextNodes = currentNode.NextNodes;

            for (int i = 0; i < nextNodes.Count; i++)
            {
                pendingNodes.Push(nextNodes[i]);
            }
        }

        if (visitedNodeIds.Count != nodes.Count)
        {
            throw new InvalidOperationException($"스킬 트리 '{treeDefinition.name}'에 시작 노드에서 도달할 수 없는 노드 또는 순환 연결이 있습니다.");
        }
    }

    private static void SortNextNodes(Dictionary<int, SkillTreeNodeRuntime> nodes)
    {
        foreach (KeyValuePair<int, SkillTreeNodeRuntime> pair in nodes)
        {
            pair.Value.SortNextNodes();
        }
    }
}
