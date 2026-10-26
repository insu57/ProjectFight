using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class SkillTreeEditorActions
{
    internal const string ParentsField = "_requiredNodeIds";

    internal static string Field(string name) => $"<{name}>k__BackingField";

    private static void SetInt(UnityEngine.Object target, string property, int value)
    {
        using SerializedObject serialized = new(target);
        serialized.FindProperty(Field(property)).intValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static int Begin(string name)
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(name);
        return Undo.GetCurrentGroup();
    }

    public static SkillNodeDefinition CreateNode(SkillTreeDefinition tree, SkillNodeDefinition parent)
    {
        if (!AssetDatabase.Contains(tree)) throw new InvalidOperationException("먼저 트리를 프로젝트의 SO 자산으로 저장하세요.");
        HashSet<int> ids = new();
        int highest = -1;
        if (tree.Nodes != null) foreach (SkillNodeDefinition node in tree.Nodes) if (node) { ids.Add(node.NodeID); highest = Math.Max(highest, node.NodeID); }
        int id = highest < int.MaxValue ? highest + 1 : 0;
        while (ids.Contains(id)) id++;
        bool isStart = ids.Count == 0;
        int group = Begin("스킬 노드 생성");
        SkillNodeDefinition created = ScriptableObject.CreateInstance<SkillNodeDefinition>();
        created.name = $"SkillNode_{id}";
        // 새 노드는 트리의 하위 자산으로 보관한다. 기존 외부 SO 참조도 그대로 지원한다.
        AssetDatabase.AddObjectToAsset(created, tree);
        Undo.RegisterCreatedObjectUndo(created, "스킬 노드 생성");
        Undo.RegisterCompleteObjectUndo(created, "스킬 노드 초기화");
        using (SerializedObject serialized = new(created))
        {
            serialized.FindProperty(Field(nameof(created.NodeID))).intValue = id;
            serialized.FindProperty(Field(nameof(created.NodeName))).stringValue = isStart ? "시작" : "새 스킬";
            serialized.FindProperty(Field(nameof(created.NodeType))).intValue = (int)(isStart ? SkillTreeNodeType.Start : SkillTreeNodeType.Main);
            SerializedProperty parents = serialized.FindProperty(ParentsField);
            parents.arraySize = isStart || !parent ? 0 : 1;
            if (parents.arraySize != 0) parents.GetArrayElementAtIndex(0).intValue = parent.NodeID;
            serialized.FindProperty(Field(nameof(created.RequiredPoints))).intValue = isStart ? 0 : 1;
            serialized.FindProperty(Field(nameof(created.EffectType))).intValue = (int)SkillTreeEffectType.None;
            serialized.ApplyModifiedProperties();
        }
        Register(tree, created);
        if (isStart) SetInt(tree, nameof(tree.StartNodeID), id);
        Undo.CollapseUndoOperations(group);
        // 새 하위 자산의 파일 ID를 확정하여 Undo/Redo와 재가져오기에서도 참조를 유지한다.
        AssetDatabase.SaveAssetIfDirty(tree);
        return created;
    }

    public static void Register(SkillTreeDefinition tree, SkillNodeDefinition node)
    {
        if (!node || !AssetDatabase.Contains(node)) throw new InvalidOperationException("프로젝트에 저장된 SkillNodeDefinition을 선택하세요.");
        if (tree.Nodes != null) foreach (SkillNodeDefinition existing in tree.Nodes)
        {
            if (existing == node) throw new InvalidOperationException("이미 등록된 노드입니다.");
            if (existing && existing.NodeID == node.NodeID) throw new InvalidOperationException($"NodeID {node.NodeID}가 이미 사용 중입니다. SO의 ID를 먼저 변경하세요.");
        }
        using SerializedObject serialized = new(tree);
        SerializedProperty nodes = serialized.FindProperty(Field(nameof(tree.Nodes)));
        int index = nodes.arraySize++;
        nodes.GetArrayElementAtIndex(index).objectReferenceValue = node;
        serialized.ApplyModifiedProperties();
    }

    public static void Connect(SkillTreeDefinition tree, SkillNodeDefinition parent, SkillNodeDefinition child)
    {
        if (!parent || !child || parent == child) throw new InvalidOperationException("서로 다른 부모와 자식 노드를 선택하세요.");
        if (child.NodeID == tree.StartNodeID) throw new InvalidOperationException("시작 노드에는 부모를 연결할 수 없습니다.");
        Dictionary<int, SkillNodeDefinition> nodes = new();
        if (tree.Nodes != null) foreach (SkillNodeDefinition node in tree.Nodes)
        {
            if (node && !nodes.TryAdd(node.NodeID, node)) throw new InvalidOperationException("중복 ID 또는 중복 등록을 먼저 수정하세요.");
        }
        if (!nodes.ContainsValue(parent) || !nodes.ContainsValue(child)) throw new InvalidOperationException("현재 트리에 등록된 노드끼리만 연결할 수 있습니다.");
        if (parent.NodeID < 0 || child.NodeID < 0) throw new InvalidOperationException("음수 NodeID를 먼저 수정하세요.");
        if (HasParent(child, parent.NodeID)) throw new InvalidOperationException("이미 연결된 부모입니다.");
        HashSet<SkillNodeDefinition> visited = new();
        Stack<SkillNodeDefinition> pending = new();
        pending.Push(parent);
        while (pending.Count > 0)
        {
            SkillNodeDefinition current = pending.Pop();
            if (current == child) throw new InvalidOperationException("순환 연결이 생기므로 연결할 수 없습니다.");
            if (!visited.Add(current)) continue;
            foreach (int parentId in current.RequiredNodeIds) if (nodes.TryGetValue(parentId, out SkillNodeDefinition ancestor)) pending.Push(ancestor);
        }
        using SerializedObject serialized = new(child);
        SerializedProperty parents = serialized.FindProperty(ParentsField);
        int index = parents.arraySize++;
        parents.GetArrayElementAtIndex(index).intValue = parent.NodeID;
        serialized.ApplyModifiedProperties();
    }

    public static bool HasParent(SkillNodeDefinition node, int parentId)
    {
        foreach (int id in node.RequiredNodeIds) if (id == parentId) return true;
        return false;
    }

    public static void Disconnect(SkillNodeDefinition node)
    {
        using SerializedObject serialized = new(node);
        serialized.FindProperty(ParentsField).ClearArray();
        serialized.ApplyModifiedProperties();
    }

    public static void Disconnect(SkillNodeDefinition node, int parentId)
    {
        using SerializedObject serialized = new(node);
        SerializedProperty parents = serialized.FindProperty(ParentsField);
        for (int i = parents.arraySize - 1; i >= 0; i--) if (parents.GetArrayElementAtIndex(i).intValue == parentId) parents.DeleteArrayElementAtIndex(i);
        serialized.ApplyModifiedProperties();
    }

    private static void ReplaceParentId(SkillNodeDefinition node, int previousId, int newId)
    {
        using SerializedObject serialized = new(node);
        SerializedProperty parents = serialized.FindProperty(ParentsField);
        for (int i = 0; i < parents.arraySize; i++) if (parents.GetArrayElementAtIndex(i).intValue == previousId) parents.GetArrayElementAtIndex(i).intValue = newId;
        serialized.ApplyModifiedProperties();
    }

    public static void RenameId(SkillTreeDefinition tree, SkillNodeDefinition node, int newId)
    {
        if (newId < 0) throw new InvalidOperationException("NodeID는 0 이상이어야 합니다.");
        int previousId = node.NodeID;
        if (previousId == newId) return;
        int previousCount = 0;
        foreach (SkillNodeDefinition other in tree.Nodes)
        {
            if (!other) continue;
            if (other != node && other.NodeID == newId) throw new InvalidOperationException("이미 사용 중인 NodeID입니다.");
            if (other.NodeID == previousId) previousCount++;
        }
        int group = Begin("스킬 노드 ID 변경");
        SetInt(node, nameof(node.NodeID), newId);
        // 중복 ID를 복구 중이라면 어느 SO의 자식인지 알 수 없으므로 참조를 임의로 옮기지 않는다.
        if (previousCount == 1)
        {
            if (tree.StartNodeID == previousId) SetInt(tree, nameof(tree.StartNodeID), newId);
            foreach (SkillNodeDefinition other in tree.Nodes) if (other && HasParent(other, previousId)) ReplaceParentId(other, previousId, newId);
        }
        Undo.CollapseUndoOperations(group);
    }

    public static void SetStart(SkillTreeDefinition tree, SkillNodeDefinition node)
    {
        if (node.NodeID < 0) throw new InvalidOperationException("시작 노드의 ID는 0 이상이어야 합니다.");
        foreach (SkillNodeDefinition other in tree.Nodes) if (other && other != node && other.NodeID == node.NodeID) throw new InvalidOperationException("중복 ID를 먼저 수정하세요.");
        int group = Begin("시작 노드 지정");
        foreach (SkillNodeDefinition other in tree.Nodes) if (other && other != node && other.NodeType == SkillTreeNodeType.Start) SetInt(other, nameof(other.NodeType), (int)SkillTreeNodeType.Main);
        SetInt(tree, nameof(tree.StartNodeID), node.NodeID);
        SetInt(node, nameof(node.NodeType), (int)SkillTreeNodeType.Start);
        Disconnect(node);
        SetInt(node, nameof(node.RequiredPoints), 0);
        Undo.CollapseUndoOperations(group);
    }

    public static void Remove(SkillTreeDefinition tree, SkillNodeDefinition node)
    {
        int group = Begin("트리에서 스킬 노드 제거");
        int count = 0;
        foreach (SkillNodeDefinition other in tree.Nodes) if (other && other.NodeID == node.NodeID) count++;
        if (count == 1)
        {
            foreach (SkillNodeDefinition other in tree.Nodes) if (other && other != node && HasParent(other, node.NodeID)) Disconnect(other, node.NodeID);
            if (tree.StartNodeID == node.NodeID) SetInt(tree, nameof(tree.StartNodeID), -1);
        }
        RemoveEntries(tree, node, false);
        // 다른 트리에서 공유할 수 있는 SO 자체는 삭제하지 않는다. 자식의 다른 부모 연결은 유지한다.
        Undo.CollapseUndoOperations(group);
    }

    public static void CleanEntries(SkillTreeDefinition tree) => RemoveEntries(tree, null, true);

    private static void RemoveEntries(SkillTreeDefinition tree, SkillNodeDefinition removed, bool clean)
    {
        HashSet<UnityEngine.Object> seen = new();
        using SerializedObject serialized = new(tree);
        SerializedProperty nodes = serialized.FindProperty(Field(nameof(tree.Nodes)));
        for (int i = nodes.arraySize - 1; i >= 0; i--)
        {
            UnityEngine.Object value = nodes.GetArrayElementAtIndex(i).objectReferenceValue;
            if (clean ? value && seen.Add(value) : value != removed) continue;
            nodes.GetArrayElementAtIndex(i).objectReferenceValue = null;
            nodes.DeleteArrayElementAtIndex(i);
        }
        serialized.ApplyModifiedProperties();
    }

    private static bool ShareParent(SkillNodeDefinition left, SkillNodeDefinition right)
    {
        foreach (int parentId in left.RequiredNodeIds) if (HasParent(right, parentId)) return true;
        return left == right;
    }

    public static void MoveSibling(SkillTreeDefinition tree, SkillNodeDefinition node, int direction)
    {
        List<SkillNodeDefinition> siblings = new();
        foreach (SkillNodeDefinition other in tree.Nodes) if (other && ShareParent(other, node) && !siblings.Contains(other)) siblings.Add(other);
        siblings.Sort((a, b) => a.DisplayOrder != b.DisplayOrder ? a.DisplayOrder.CompareTo(b.DisplayOrder) : a.NodeID.CompareTo(b.NodeID));
        int index = siblings.IndexOf(node);
        int next = index + direction;
        if (index < 0 || next < 0 || next >= siblings.Count) return;
        (siblings[index], siblings[next]) = (siblings[next], siblings[index]);
        int group = Begin("형제 노드 순서 변경");
        for (int i = 0; i < siblings.Count; i++) SetInt(siblings[i], nameof(node.DisplayOrder), i);
        Undo.CollapseUndoOperations(group);
    }

    public static void Save(SkillTreeDefinition tree)
    {
        // 외부 SO와 하위 자산을 모두 저장하되, 무관한 프로젝트 자산은 저장하지 않는다.
        if (tree.Nodes != null) foreach (SkillNodeDefinition node in tree.Nodes) if (node) AssetDatabase.SaveAssetIfDirty(node);
        AssetDatabase.SaveAssetIfDirty(tree);
    }
}
