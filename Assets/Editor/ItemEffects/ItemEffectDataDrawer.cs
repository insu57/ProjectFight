using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(ItemEffectData), true)]
internal sealed class ItemEffectDataDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        return ItemEffectDataDrawerUI.Create(property, ItemEffectDataDrawerUI.ImmediateTypes);
    }
}

[CustomPropertyDrawer(typeof(PassiveItemEffectData), true)]
internal sealed class PassiveItemEffectDataDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        return ItemEffectDataDrawerUI.Create(property, ItemEffectDataDrawerUI.PassiveTypes);
    }
}

internal static class ItemEffectDataDrawerUI
{
    // 타입 검색과 표시 이름 구성은 도메인마다 한 번만 수행한다.
    public static readonly EffectTypes ImmediateTypes = new(typeof(ItemEffectData));
    public static readonly EffectTypes PassiveTypes = new(typeof(PassiveItemEffectData));

    public static VisualElement Create(SerializedProperty property, EffectTypes types)
    {
        if (property.serializedObject.isEditingMultipleObjects)
            return new HelpBox("효과 설정은 아이템을 하나씩 선택하여 편집하세요.", HelpBoxMessageType.Info);

        SerializedObject serializedObject = property.serializedObject;
        string propertyPath = property.propertyPath;
        Foldout root = new() { text = property.displayName, value = property.isExpanded || property.managedReferenceValue == null };
        PopupField<string> selector = new("효과 종류", types.Names, 0);
        selector.AddToClassList(BaseField<string>.alignedFieldUssClassName);
        VisualElement fields = new();
        root.Add(selector);
        root.Add(fields);
        long previousId = long.MinValue;
        Type previousType = null;

        void Refresh(bool force)
        {
            SerializedProperty current = serializedObject.FindProperty(propertyPath);
            if (current == null) return;
            Type currentType = current.managedReferenceValue?.GetType();
            long currentId = current.managedReferenceId;
            // 수치 입력 중에는 컨트롤을 다시 생성하지 않고, 타입 교체 및 Undo/Redo만 반영한다.
            if (!force && currentId == previousId && currentType == previousType) return;
            previousId = currentId;
            previousType = currentType;
            int selectedIndex = currentType == null ? 0 : types.Types.IndexOf(currentType);
            selector.SetValueWithoutNotify(types.Names[Math.Max(0, selectedIndex)]);
            fields.Unbind();
            fields.Clear();
            if (currentType == null) return;

            SerializedProperty child = current.Copy();
            SerializedProperty end = current.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                if (child.depth == current.depth + 1) fields.Add(new PropertyField(child.Copy()));
                enterChildren = false;
            }
            fields.Bind(serializedObject);
        }

        selector.RegisterValueChangedCallback(evt =>
        {
            int index = types.Names.IndexOf(evt.newValue);
            if (index < 0) return;
            serializedObject.Update();
            SerializedProperty current = serializedObject.FindProperty(propertyPath);
            if (current == null) return;
            // SerializedProperty를 통해 교체하여 Undo/Redo와 에셋 저장을 기존 Inspector와 일치시킨다.
            current.managedReferenceValue = index == 0 ? null : Activator.CreateInstance(types.Types[index]);
            serializedObject.ApplyModifiedProperties();
            Refresh(true);
        });
        root.RegisterValueChangedCallback(evt =>
        {
            if (evt.target != root) return;
            SerializedProperty current = serializedObject.FindProperty(propertyPath);
            if (current != null) current.isExpanded = evt.newValue;
        });
        root.TrackPropertyValue(property, _ => Refresh(false));
        Refresh(true);
        return root;
    }

    internal sealed class EffectTypes
    {
        public readonly List<Type> Types = new() { null };
        public readonly List<string> Names = new() { "없음" };

        public EffectTypes(Type baseType)
        {
            List<Type> candidates = new();
            foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (!type.IsAbstract && !type.ContainsGenericParameters && type.IsDefined(typeof(SerializableAttribute), false)
                    && type.GetConstructor(Type.EmptyTypes) != null) candidates.Add(type);
            }
            candidates.Sort((left, right) => string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));
            for (int i = 0; i < candidates.Count; i++)
            {
                Type type = candidates[i];
                var displayName = Attribute.GetCustomAttribute(type, typeof(ItemEffectNameAttribute)) as ItemEffectNameAttribute;
                Types.Add(type);
                Names.Add(displayName?.Name ?? ObjectNames.NicifyVariableName(type.Name));
            }
        }
    }
}
