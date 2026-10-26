using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(SkillTreeDefinition))]
internal sealed class SkillTreeDefinitionEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new();
        root.Add(new Button(() => SkillTreeEditorWindow.Open((SkillTreeDefinition)target)) { text = "스킬 트리 설계 창 열기" });
        InspectorElement.FillDefaultInspector(root, serializedObject, this);
        return root;
    }
}
