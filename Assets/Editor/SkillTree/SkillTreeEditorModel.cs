using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class SkillTreeEditorModel
{
    internal readonly struct Issue
    {
        public readonly SkillNodeDefinition Node;
        public readonly string Message;

        public Issue(SkillNodeDefinition node, string message)
        {
            Node = node;
            Message = message;
        }
    }

    public readonly List<SkillNodeDefinition> Nodes = new();
    public readonly List<Issue> Issues = new();
    public readonly Dictionary<SkillNodeDefinition, Vector2> Positions = new();
    public readonly HashSet<SkillNodeDefinition> InvalidNodes = new();
    private readonly Dictionary<int, SkillNodeDefinition> _byId = new();
    private readonly HashSet<int> _duplicateIds = new();
    private readonly Dictionary<int, Vector2> _runtimePositions = new();
    public Vector2 ContentSize { get; private set; }
    public bool IsValid => Issues.Count == 0;

    public SkillNodeDefinition FindUnique(int id)
    {
        return !_duplicateIds.Contains(id) && _byId.TryGetValue(id, out SkillNodeDefinition node) ? node : null;
    }

    public void Rebuild(SkillTreeDefinition tree, SkillTreeLayoutSettings settings)
    {
        Nodes.Clear();
        Issues.Clear();
        Positions.Clear();
        InvalidNodes.Clear();
        _byId.Clear();
        _duplicateIds.Clear();
        ContentSize = Vector2.one;
        if (!tree) return;

        SkillNodeDefinition[] definitions = tree.Nodes;
        if (definitions == null || definitions.Length == 0) AddIssue(null, "노드가 없습니다. '노드 추가'로 시작 노드를 만드세요.");
        else
        {
            HashSet<SkillNodeDefinition> registered = new();
            for (int i = 0; i < definitions.Length; i++)
            {
                SkillNodeDefinition node = definitions[i];
                if (!node) { AddIssue(null, $"Nodes[{i}]의 SO 참조가 비어 있습니다."); continue; }
                if (!registered.Add(node)) { AddIssue(node, "같은 SO가 여러 번 등록되어 있습니다."); continue; }
                Nodes.Add(node);
                if (!_byId.TryAdd(node.NodeID, node))
                {
                    _duplicateIds.Add(node.NodeID);
                    AddIssue(node, $"NodeID {node.NodeID}가 중복됩니다.");
                    InvalidNodes.Add(_byId[node.NodeID]);
                }
                if (node.NodeID < 0) AddIssue(node, "NodeID는 0 이상이어야 합니다. 음수는 부모 없음 표시로 사용합니다.");
                if (node.RequiredPoints < 0) AddIssue(node, "필요 포인트는 0 이상이어야 합니다.");
            }
        }

        SkillNodeDefinition start = FindUnique(tree.StartNodeID);
        if (!start) AddIssue(null, $"시작 ID {tree.StartNodeID}에 해당하는 유일한 노드가 없습니다.");
        foreach (SkillNodeDefinition node in Nodes)
        {
            if (node == start)
            {
                if (node.NodeType != SkillTreeNodeType.Start || node.RequiredNodeIds.Count != 0 || node.RequiredPoints != 0) AddIssue(node, "시작 노드는 Start 타입, 부모 없음, 필요 포인트 0이어야 합니다.");
                continue;
            }
            if (node.NodeType == SkillTreeNodeType.Start) AddIssue(node, "Start 타입은 지정된 시작 노드에만 사용할 수 있습니다.");
            if (node.RequiredNodeIds.Count == 0) AddIssue(node, "선행 노드가 없습니다.");
            HashSet<int> parents = new();
            foreach (int parentId in node.RequiredNodeIds)
            {
                if (!parents.Add(parentId)) AddIssue(node, $"선행 ID {parentId}가 중복됩니다.");
                if (parentId == node.NodeID) AddIssue(node, "자기 자신을 선행 노드로 지정할 수 없습니다.");
                else if (!FindUnique(parentId)) AddIssue(node, $"선행 ID {parentId}가 없거나 중복됩니다.");
            }
        }

        if (IsValid)
        {
            try
            {
                SkillTreeRuntime runtime = SkillTreeRuntime.Create(tree, 0);
                ContentSize = SkillTreeLayoutUtility.Calculate(runtime.StartNode, settings, _runtimePositions);
                foreach (SkillNodeDefinition node in Nodes) Positions.Add(node, _runtimePositions[node.NodeID]);
                return;
            }
            catch (ArgumentException exception) { AddIssue(null, exception.Message); }
            catch (InvalidOperationException exception) { AddIssue(null, exception.Message); }
        }

        // 잘못된 연결도 화면에서 수정할 수 있도록 모든 SO를 임시 격자에 표시한다.
        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(Nodes.Count)));
        for (int i = 0; i < Nodes.Count; i++) Positions.Add(Nodes[i], new Vector2(settings.HorizontalPadding + settings.NodeSize.x * 0.5f + i % columns * settings.HorizontalSpacing, -(settings.VerticalPadding + i / columns * settings.VerticalSpacing)));
        ContentSize = new Vector2(settings.HorizontalPadding * 2f + (columns - 1) * settings.HorizontalSpacing + settings.NodeSize.x, settings.VerticalPadding * 2f + Mathf.Max(0, (Nodes.Count - 1) / columns) * settings.VerticalSpacing + settings.NodeSize.y);
    }

    private void AddIssue(SkillNodeDefinition node, string message)
    {
        Issues.Add(new Issue(node, node ? $"[{node.NodeID}] {node.NodeName}: {message}" : message));
        if (node) InvalidNodes.Add(node);
    }
}
