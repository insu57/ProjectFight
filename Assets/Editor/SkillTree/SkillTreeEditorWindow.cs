using System;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SkillTreeEditorWindow : EditorWindow
{
    private const string LayoutPath = "Assets/Editor/SkillTree/SkillTreeEditorWindow.uxml";
    [SerializeField] private SkillTreeDefinition _tree;
    [SerializeField] private SkillNodeDefinition _selected;
    [SerializeField] private SkillTreeView _previewSource;
    private readonly SkillTreeEditorModel _model = new();
    private SkillTreeGraphCanvas _canvas;
    private ScrollView _inspector;
    private ScrollView _issues;
    private VisualElement _message;
    private VisualElement _editToolbar;
    private Label _validationSummary;
    private ObjectField _treeField;
    private SerializedObject _selectedObject;
    private bool _refreshQueued;
    private bool _refreshInspector;
    private int _lastHash;

    [MenuItem("Tools/ProjectFight/Skill Tree Editor")]
    public static void OpenWindow()
    {
        Open(Selection.activeObject as SkillTreeDefinition);
    }

    public static void Open(SkillTreeDefinition tree)
    {
        SkillTreeEditorWindow window = GetWindow<SkillTreeEditorWindow>();
        window.titleContent = new GUIContent("스킬 트리 설계");
        window.minSize = new Vector2(900f, 600f);
        if (tree) window.SetTree(tree);
        window.Show();
    }

    [OnOpenAsset]
    private static bool OnOpenAsset(int instanceId, int line)
    {
        if (EditorUtility.EntityIdToObject(instanceId) is not SkillTreeDefinition tree) return false;
        Open(tree);
        return true;
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.projectChanged += OnProjectChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.projectChanged -= OnProjectChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        ReleaseInspector();
    }

    public void CreateGUI()
    {
        rootVisualElement.Clear();
        VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
        if (!layout)
        {
            rootVisualElement.Add(new HelpBox($"레이아웃 파일을 찾을 수 없습니다: {LayoutPath}", HelpBoxMessageType.Error));
            return;
        }
        layout.CloneTree(rootVisualElement);
        _treeField = new ObjectField("스킬 트리") { objectType = typeof(SkillTreeDefinition), allowSceneObjects = false, value = _tree };
        _treeField.AddToClassList("skill-tree-field");
        _treeField.RegisterValueChangedCallback(evt => SetTree(evt.newValue as SkillTreeDefinition));
        VisualElement toolbar = rootVisualElement.Q("toolbar");
        toolbar.Add(_treeField);
        _editToolbar = new VisualElement();
        _editToolbar.AddToClassList("skill-actions");
        toolbar.Add(_editToolbar);
        AddButton(_editToolbar, "새 트리", CreateTree);
        AddButton(_editToolbar, "노드 추가", () => Run(() => { _selected = SkillTreeEditorActions.CreateNode(_tree, _selected ? _selected : _model.FindUnique(_tree.StartNodeID)); }, true));
        AddButton(_editToolbar, "저장", () => Run(() => SkillTreeEditorActions.Save(_tree), false));
        AddButton(toolbar, "전체 보기", () => _canvas.FrameAll());
        AddButton(toolbar, "검증", () => Refresh(false));

        VisualElement preview = rootVisualElement.Q("previewToolbar");
        ObjectField source = new("배치 기준 (선택)") { objectType = typeof(SkillTreeView), allowSceneObjects = true, value = _previewSource, tooltip = "SkillTreeView를 지정하면 해당 컴포넌트의 노드 크기·간격을 사용합니다. 비어 있으면 기본값을 사용합니다." };
        source.AddToClassList("skill-tree-field");
        source.RegisterValueChangedCallback(evt => { _previewSource = evt.newValue as SkillTreeView; Refresh(false); _canvas.schedule.Execute(_canvas.FrameAll); });
        preview.Add(source);
        Label previewHint = new("구조·배치 미리보기 / 자동 배치");
        previewHint.AddToClassList("skill-hint");
        preview.Add(previewHint);

        _canvas = new SkillTreeGraphCanvas();
        _canvas.Selected += Select;
        _canvas.Connected += (parent, child) => Run(() => SkillTreeEditorActions.Connect(_tree, parent, child), true);
        _canvas.RegisterRequested += node => Run(() => { SkillTreeEditorActions.Register(_tree, node); _selected = node; }, true);
        _canvas.DeleteRequested += RemoveSelected;
        rootVisualElement.Q("canvasHost").Add(_canvas);
        _inspector = rootVisualElement.Q<ScrollView>("inspector");
        _issues = rootVisualElement.Q<ScrollView>("issues");
        _validationSummary = rootVisualElement.Q<Label>("validationSummary");
        _message = rootVisualElement.Q("message");
        Refresh(true);
        _canvas.schedule.Execute(_canvas.FrameAll);
    }

    private void SetTree(SkillTreeDefinition tree)
    {
        _tree = tree;
        _selected = null;
        _treeField?.SetValueWithoutNotify(tree);
        _message?.Clear();
        if (_canvas == null) return;
        Refresh(true);
        _canvas.schedule.Execute(_canvas.FrameAll);
    }

    private void Select(SkillNodeDefinition node)
    {
        if (_selected == node) return;
        _selected = node;
        _canvas.Select(node);
        BuildInspector();
    }

    private void Refresh(bool inspector)
    {
        if (_canvas == null) return;
        _refreshQueued = false;
        SkillTreeLayoutSettings settings = ReadSettings();
        _model.Rebuild(_tree, settings);
        if (!_model.Nodes.Contains(_selected)) { _selected = null; inspector = true; }
        _canvas.Editable = _tree && !EditorApplication.isPlayingOrWillChangePlaymode;
        _editToolbar.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
        _canvas.Rebuild(_tree, _model, settings, _selected);
        _issues.Clear();
        _validationSummary.text = !_tree ? "트리를 선택하거나 새로 만드세요." : _model.IsValid ? $"검증 통과 · {_model.Nodes.Count}개 노드 · 인게임 자동 배치 적용" : $"{_model.Issues.Count}개 문제 · 수정할 수 있도록 임시 격자에 표시 중";
        foreach (SkillTreeEditorModel.Issue issue in _model.Issues)
        {
            Button button = new(() => { if (issue.Node) Select(issue.Node); }) { text = issue.Message };
            button.AddToClassList("skill-issue");
            _issues.Add(button);
        }
        if (inspector) BuildInspector();
        _lastHash = GetDataHash();
    }

    private SkillTreeLayoutSettings ReadSettings()
    {
        if (!_previewSource) return SkillTreeLayoutSettings.Default;
        using SerializedObject source = new(_previewSource);
        return new SkillTreeLayoutSettings(Mathf.Max(1f, source.FindProperty("_horizontalSpacing").floatValue), Mathf.Max(1f, source.FindProperty("_verticalSpacing").floatValue), Mathf.Max(0f, source.FindProperty("_horizontalPadding").floatValue), Mathf.Max(0f, source.FindProperty("_verticalPadding").floatValue), Vector2.Max(Vector2.one, source.FindProperty("_nodeSize").vector2Value));
    }

    private void BuildInspector()
    {
        if (_inspector == null) return;
        ReleaseInspector();
        _inspector.Clear();
        if (!_tree)
        {
            _inspector.Add(new HelpBox("SkillTreeDefinition을 선택하세요. 기존 SO를 더블클릭해도 이 창을 열 수 있습니다.", HelpBoxMessageType.Info));
            return;
        }

        VisualElement editing = new();
        editing.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
        _inspector.Add(editing);
        ObjectField existing = new("기존 노드 등록") { objectType = typeof(SkillNodeDefinition), allowSceneObjects = false };
        existing.RegisterValueChangedCallback(evt => { if (evt.newValue is SkillNodeDefinition node) Run(() => { SkillTreeEditorActions.Register(_tree, node); _selected = node; }, true); });
        editing.Add(existing);
        AddButton(editing, "빈 참조 / 같은 SO 중복 등록 정리", () => Run(() => SkillTreeEditorActions.CleanEntries(_tree), true));
        if (!_selected)
        {
            editing.Add(new HelpBox("노드를 선택해 속성을 편집하세요. Project의 노드 SO를 캔버스에 드롭하여 등록할 수도 있습니다.", HelpBoxMessageType.Info));
            return;
        }

        SkillNodeDefinition node = _selected;
        Label heading = new($"노드: {node.name}");
        heading.AddToClassList("skill-heading");
        editing.Add(heading);
        AddButton(editing, "Project에서 SO 찾기", () => EditorGUIUtility.PingObject(node));
        editing.Add(new HelpBox("SO 속성을 직접 편집합니다. 같은 SO를 공유하는 다른 트리에도 영향을 줍니다. ID 변경 시 선행 ID 갱신은 현재 트리에만 적용됩니다.", HelpBoxMessageType.Info));
        IntegerField id = new("Node ID") { value = node.NodeID, isDelayed = true };
        id.RegisterValueChangedCallback(evt => Run(() => SkillTreeEditorActions.RenameId(_tree, node, evt.newValue), true));
        editing.Add(id);
        editing.Add(new Label("선행 노드 (하나만 해금하면 충족)"));
        foreach (int parentId in node.RequiredNodeIds)
        {
            VisualElement row = new();
            row.AddToClassList("skill-actions");
            SkillNodeDefinition required = _model.FindUnique(parentId);
            row.Add(new Label(required ? $"{parentId}: {required.NodeName}" : $"{parentId}: 잘못된 참조"));
            AddButton(row, "연결 제거", () => Run(() => SkillTreeEditorActions.Disconnect(node, parentId), true));
            editing.Add(row);
        }
        ObjectField parent = new("부모 추가") { objectType = typeof(SkillNodeDefinition), allowSceneObjects = false };
        parent.RegisterValueChangedCallback(evt => Run(() => { if (evt.newValue is SkillNodeDefinition required) SkillTreeEditorActions.Connect(_tree, required, node); }, true));
        parent.SetEnabled(node.NodeID != _tree.StartNodeID);
        editing.Add(parent);

        _selectedObject = new SerializedObject(node);
        VisualElement properties = new();
        editing.Add(properties);
        AddProperty(properties, nameof(node.NodeName), "이름");
        AddProperty(properties, nameof(node.NodeIcon), "아이콘");
        AddProperty(properties, nameof(node.NodeDescription), "설명");
        AddProperty(properties, nameof(node.NodeType), "노드 종류");
        AddProperty(properties, nameof(node.RequiredPoints), "필요 포인트");
        AddProperty(properties, nameof(node.EffectType), "효과 종류");
        AddProperty(properties, nameof(node.EffectValue), "효과 값");
        AddProperty(properties, nameof(node.DisplayOrder), "형제 표시 순서");
        properties.Bind(_selectedObject);
        // 초기 바인딩 알림으로 그래프를 재생성하면 진행 중인 연결 드래그가 끊긴다.
        properties.RegisterCallback<SerializedPropertyChangeEvent>(_ => { if (GetDataHash() != _lastHash) QueueRefresh(false); });

        VisualElement actions = new();
        actions.AddToClassList("skill-actions");
        editing.Add(actions);
        AddButton(actions, "시작 노드 지정", () => Run(() => SkillTreeEditorActions.SetStart(_tree, node), true));
        AddButton(actions, "모든 부모 연결 해제", () => Run(() => SkillTreeEditorActions.Disconnect(node), true));
        AddButton(actions, "형제 ←", () => Run(() => SkillTreeEditorActions.MoveSibling(_tree, node, -1), true));
        AddButton(actions, "형제 →", () => Run(() => SkillTreeEditorActions.MoveSibling(_tree, node, 1), true));
        AddButton(actions, "자식 추가", () => Run(() => { _selected = SkillTreeEditorActions.CreateNode(_tree, node); }, true));
        AddButton(actions, "트리에서 제거", RemoveSelected);
        editing.Add(new HelpBox("제거 시 SO 자산은 보존됩니다. 자식의 다른 부모 연결은 유지됩니다. 편집은 Ctrl+Z / Ctrl+Y로 되돌릴 수 있습니다.", HelpBoxMessageType.Info));
    }

    private void AddProperty(VisualElement target, string property, string label)
    {
        target.Add(new PropertyField(_selectedObject.FindProperty(SkillTreeEditorActions.Field(property)), label));
    }

    private void ReleaseInspector()
    {
        _inspector?.Unbind();
        _selectedObject?.Dispose();
        _selectedObject = null;
    }

    private static void AddButton(VisualElement target, string text, Action action) => target.Add(new Button(action) { text = text });

    private void Run(Action action, bool inspector)
    {
        if (!_tree || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { action(); _message.Clear(); }
        catch (InvalidOperationException exception) { _message.Clear(); _message.Add(new HelpBox(exception.Message, HelpBoxMessageType.Warning)); }
        // 이벤트 전달 중 바인딩 요소를 제거하지 않도록 다음 UI 갱신에서 다시 구성한다.
        QueueRefresh(inspector);
    }

    private void RemoveSelected()
    {
        if (!_selected) return;
        Run(() => { SkillTreeEditorActions.Remove(_tree, _selected); _selected = null; }, true);
    }

    private void CreateTree()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string path = EditorUtility.SaveFilePanelInProject("스킬 트리 생성", "SkillTreeDefinition", "asset", "새 트리 SO를 저장할 위치를 선택하세요.");
        if (string.IsNullOrEmpty(path)) return;
        SkillTreeDefinition tree = CreateInstance<SkillTreeDefinition>();
        AssetDatabase.CreateAsset(tree, AssetDatabase.GenerateUniqueAssetPath(path));
        SkillTreeEditorActions.CreateNode(tree, null);
        SkillTreeEditorActions.Save(tree);
        SetTree(tree);
    }

    private void QueueRefresh(bool inspector)
    {
        _refreshInspector |= inspector;
        if (_refreshQueued || _canvas == null) return;
        _refreshQueued = true;
        rootVisualElement.schedule.Execute(() => { bool rebuildInspector = _refreshInspector; _refreshInspector = false; Refresh(rebuildInspector); });
    }

    private void OnUndoRedo() => QueueRefresh(true);
    private void OnProjectChanged() => QueueRefresh(true);
    private void OnPlayModeChanged(PlayModeStateChange state) => QueueRefresh(true);

    private void OnInspectorUpdate()
    {
        // 다른 Inspector의 변경도 반영한다. 변경이 없으면 그래프나 바인딩을 재생성하지 않는다.
        if (_canvas != null && !_refreshQueued && GetDataHash() != _lastHash) QueueRefresh(true);
    }

    private int GetDataHash()
    {
        unchecked
        {
            int hash = _tree ? EditorUtility.GetDirtyCount(_tree) + _tree.GetInstanceID() : 0;
            if (_tree && _tree.Nodes != null) foreach (SkillNodeDefinition node in _tree.Nodes) hash = hash * 31 + (node ? node.GetInstanceID() + EditorUtility.GetDirtyCount(node) : 0);
            return hash * 31 + (_previewSource ? _previewSource.GetInstanceID() + EditorUtility.GetDirtyCount(_previewSource) : 0);
        }
    }
}
