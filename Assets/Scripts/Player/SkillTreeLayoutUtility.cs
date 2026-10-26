using System.Collections.Generic;
using UnityEngine;

public readonly struct SkillTreeLayoutSettings
{
    public readonly float HorizontalSpacing;
    public readonly float VerticalSpacing;
    public readonly float HorizontalPadding;
    public readonly float VerticalPadding;
    public readonly Vector2 NodeSize;

    public static SkillTreeLayoutSettings Default => new(240f, 160f, 100f, 100f, new Vector2(120f, 120f));

    public SkillTreeLayoutSettings(float horizontalSpacing, float verticalSpacing, float horizontalPadding, float verticalPadding, Vector2 nodeSize)
    {
        HorizontalSpacing = horizontalSpacing;
        VerticalSpacing = verticalSpacing;
        HorizontalPadding = horizontalPadding;
        VerticalPadding = verticalPadding;
        NodeSize = nodeSize;
    }
}

public static class SkillTreeLayoutUtility
{
    // 인게임과 에디터가 동일한 좌표를 사용한다. UI 상단을 기준으로 아래로 갈수록 Y가 감소한다.
    public static Vector2 Calculate(SkillTreeNodeRuntime root, SkillTreeLayoutSettings settings, Dictionary<int, Vector2> positions)
    {
        positions.Clear();
        // 단일 부모 트리는 기존 배치를 유지한다. 합류가 있는 그래프만 깊이별로 배치한다.
        List<SkillTreeNodeRuntime> nodes = new();
        HashSet<int> visited = new();
        Queue<SkillTreeNodeRuntime> pending = new();
        bool hasMerge = false;
        pending.Enqueue(root);
        while (pending.Count > 0)
        {
            SkillTreeNodeRuntime node = pending.Dequeue();
            if (!visited.Add(node.NodeID)) continue;
            nodes.Add(node);
            hasMerge |= node.RequiredNodes.Count > 1;
            foreach (SkillTreeNodeRuntime child in node.NextNodes) pending.Enqueue(child);
        }
        if (hasMerge) return CalculateGraph(nodes, root, settings, positions);
        int nextLeafIndex = 0;
        int maximumDepth = 0;
        CalculateNode(root, 0, settings, positions, ref nextLeafIndex, ref maximumDepth);
        float width = settings.HorizontalPadding * 2f + (Mathf.Max(1, nextLeafIndex) - 1) * settings.HorizontalSpacing + settings.NodeSize.x;
        float height = settings.VerticalPadding * 2f + maximumDepth * settings.VerticalSpacing + settings.NodeSize.y;
        return new Vector2(width, height);
    }

    private static Vector2 CalculateGraph(List<SkillTreeNodeRuntime> nodes, SkillTreeNodeRuntime root, SkillTreeLayoutSettings settings,
        Dictionary<int, Vector2> positions)
    {
        Dictionary<int, int> remaining = new(nodes.Count);
        Dictionary<int, int> depths = new(nodes.Count);
        List<List<SkillTreeNodeRuntime>> layers = new();
        Queue<SkillTreeNodeRuntime> pending = new();
        foreach (SkillTreeNodeRuntime node in nodes) { remaining.Add(node.NodeID, node.RequiredNodes.Count); depths.Add(node.NodeID, 0); }
        pending.Enqueue(root);
        int maxCount = 1;
        while (pending.Count > 0)
        {
            SkillTreeNodeRuntime node = pending.Dequeue();
            int depth = depths[node.NodeID];
            while (layers.Count <= depth) layers.Add(new List<SkillTreeNodeRuntime>());
            layers[depth].Add(node);
            maxCount = Mathf.Max(maxCount, layers[depth].Count);
            foreach (SkillTreeNodeRuntime child in node.NextNodes)
            {
                // 가장 깊은 부모보다 한 단계 아래에 두어 모든 연결이 아래로 향하게 한다.
                depths[child.NodeID] = Mathf.Max(depths[child.NodeID], depth + 1);
                if (--remaining[child.NodeID] == 0) pending.Enqueue(child);
            }
        }
        float width = settings.HorizontalPadding * 2f + (maxCount - 1) * settings.HorizontalSpacing + settings.NodeSize.x;
        for (int depth = 0; depth < layers.Count; depth++)
        {
            List<SkillTreeNodeRuntime> layer = layers[depth];
            layer.Sort(CompareNodes);
            float left = (width - (layer.Count - 1) * settings.HorizontalSpacing) * 0.5f;
            for (int i = 0; i < layer.Count; i++)
            {
                positions.Add(layer[i].NodeID, new Vector2(left + i * settings.HorizontalSpacing,
                    -(settings.VerticalPadding + depth * settings.VerticalSpacing)));
            }
        }
        return new Vector2(width, settings.VerticalPadding * 2f + (layers.Count - 1) * settings.VerticalSpacing + settings.NodeSize.y);
    }

    private static int CompareNodes(SkillTreeNodeRuntime left, SkillTreeNodeRuntime right)
    {
        int order = left.Definition.DisplayOrder.CompareTo(right.Definition.DisplayOrder);
        return order != 0 ? order : left.NodeID.CompareTo(right.NodeID);
    }

    private static float CalculateNode(SkillTreeNodeRuntime node, int depth, SkillTreeLayoutSettings settings,
        Dictionary<int, Vector2> positions, ref int nextLeafIndex, ref int maximumDepth)
    {
        maximumDepth = Mathf.Max(maximumDepth, depth);
        IReadOnlyList<SkillTreeNodeRuntime> children = node.NextNodes;
        float x;

        if (children.Count == 0)
        {
            x = settings.HorizontalPadding + settings.NodeSize.x * 0.5f + nextLeafIndex * settings.HorizontalSpacing;
            nextLeafIndex++;
        }
        else
        {
            float first = CalculateNode(children[0], depth + 1, settings, positions, ref nextLeafIndex, ref maximumDepth);
            float last = first;

            for (int i = 1; i < children.Count; i++)
            {
                last = CalculateNode(children[i], depth + 1, settings, positions, ref nextLeafIndex, ref maximumDepth);
            }
            // 부모는 첫 자식과 마지막 자식의 중앙에 배치한다.
            x = (first + last) * 0.5f;
        }

        positions.Add(node.NodeID, new Vector2(x, -(settings.VerticalPadding + depth * settings.VerticalSpacing)));
        return x;
    }
}
