using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class SkillTreeGraphCanvas : VisualElement
{
    private sealed class NodeCard : VisualElement
    {
        public readonly SkillNodeDefinition Node;

        public NodeCard(SkillNodeDefinition node, Vector2 position, Vector2 size, bool start, bool invalid)
        {
            Node = node;
            AddToClassList("skill-node");
            EnableInClassList("skill-node--start", start);
            EnableInClassList("skill-node--sub", node.NodeType == SkillTreeNodeType.Sub);
            EnableInClassList("skill-node--invalid", invalid);
            style.left = position.x - size.x * 0.5f;
            style.top = -position.y - size.y * 0.5f;
            style.width = size.x;
            style.height = size.y;
            tooltip = $"{node.NodeName} (ID {node.NodeID})\n{node.NodeDescription}\n필요 포인트: {node.RequiredPoints} / 표시 순서: {node.DisplayOrder}";
            if (!start) Add(Port("input", "선행 노드 연결 받기"));
            Label id = new($"{node.NodeID} · {node.NodeType}");
            id.AddToClassList("skill-node-id");
            Add(id);
            Image icon = new() { sprite = node.NodeIcon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            icon.AddToClassList("skill-node-icon");
            Add(icon);
            Label title = new(string.IsNullOrWhiteSpace(node.NodeName) ? node.name : node.NodeName);
            title.AddToClassList("skill-node-title");
            Add(title);
            Add(new Label(start ? "시작" : $"{node.RequiredPoints} SP"));
            Add(Port("output", "이 점을 자식 노드로 드래그하여 연결"));
        }

        private static VisualElement Port(string name, string tip)
        {
            VisualElement port = new() { name = name, tooltip = tip };
            port.AddToClassList("skill-port");
            port.AddToClassList($"skill-port--{name}");
            return port;
        }
    }

    private sealed class CanvasManipulator : PointerManipulator
    {
        private readonly SkillTreeGraphCanvas _canvas;
        private int _pointer = -1;
        private Vector2 _lastPosition;
        private bool _panning;

        public CanvasManipulator(SkillTreeGraphCanvas canvas) { _canvas = canvas; }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove);
            target.RegisterCallback<PointerUpEvent>(OnUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            target.RegisterCallback<WheelEvent>(OnWheel);
            target.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove);
            target.UnregisterCallback<PointerUpEvent>(OnUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            target.UnregisterCallback<WheelEvent>(OnWheel);
            target.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void OnDown(PointerDownEvent evt)
        {
            if (_pointer >= 0 || evt.button != 0 && evt.button != 2) return;
            VisualElement hit = evt.target as VisualElement;
            NodeCard card = FindCard(hit);
            target.Focus();
            if (evt.button == 0 && card != null)
            {
                _canvas.Selected?.Invoke(card.Node);
                if (hit.name != "output" || !_canvas.Editable) return;
                _canvas._connecting = card.Node;
                _canvas._connectionEnd = _canvas._world.WorldToLocal(evt.position);
            }
            else _panning = true;
            _pointer = evt.pointerId;
            _lastPosition = evt.position;
            target.CapturePointer(_pointer);
            evt.StopPropagation();
        }

        private void OnMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _pointer) return;
            Vector2 current = evt.position;
            if (_panning) { _canvas._pan += current - _lastPosition; _canvas.ApplyTransform(); }
            else { _canvas._connectionEnd = _canvas._world.WorldToLocal(current); _canvas._edges.MarkDirtyRepaint(); }
            _lastPosition = current;
            evt.StopPropagation();
        }

        private void OnUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _pointer) return;
            SkillNodeDefinition parent = _canvas._connecting;
            NodeCard child = FindCard(target.panel.Pick(evt.position));
            Cancel();
            if (parent && child != null && _canvas.Editable) _canvas.Connected?.Invoke(parent, child.Node);
            evt.StopPropagation();
        }

        private void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            _pointer = -1;
            _panning = false;
            _canvas._connecting = null;
            _canvas._edges.MarkDirtyRepaint();
        }

        private void Cancel()
        {
            int pointer = _pointer;
            _pointer = -1;
            _panning = false;
            _canvas._connecting = null;
            if (pointer >= 0) target.ReleasePointer(pointer);
            _canvas._edges.MarkDirtyRepaint();
        }

        private void OnWheel(WheelEvent evt)
        {
            Vector2 cursor = target.WorldToLocal(evt.mousePosition);
            float nextZoom = Mathf.Clamp(_canvas._zoom * Mathf.Exp(-evt.delta.y * 0.06f), 0.2f, 2f);
            _canvas._pan = cursor - (cursor - _canvas._pan) * (nextZoom / _canvas._zoom);
            _canvas._zoom = nextZoom;
            _canvas.ApplyTransform();
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape) { Cancel(); evt.StopPropagation(); }
            else if (evt.keyCode == KeyCode.F) { _canvas.FrameAll(); evt.StopPropagation(); }
            else if (evt.keyCode == KeyCode.Delete && _canvas.Editable) { _canvas.DeleteRequested?.Invoke(); evt.StopPropagation(); }
        }
    }

    private readonly VisualElement _world = new() { name = "graphWorld", usageHints = UsageHints.DynamicTransform };
    private readonly VisualElement _edges = new() { pickingMode = PickingMode.Ignore };
    private readonly VisualElement _nodes = new() { pickingMode = PickingMode.Ignore };
    private readonly List<NodeCard> _cards = new();
    private SkillTreeEditorModel _model;
    private Vector2 _nodeSize;
    private Vector2 _pan = new(30f, 30f);
    private float _zoom = 1f;
    private SkillNodeDefinition _connecting;
    private Vector2 _connectionEnd;
    public bool Editable { get; set; } = true;
    public event Action<SkillNodeDefinition> Selected;
    public event Action<SkillNodeDefinition, SkillNodeDefinition> Connected;
    public event Action<SkillNodeDefinition> RegisterRequested;
    public event Action DeleteRequested;

    public SkillTreeGraphCanvas()
    {
        AddToClassList("skill-canvas");
        focusable = true;
        _world.AddToClassList("skill-world");
        _world.style.transformOrigin = new TransformOrigin(0, 0, 0);
        _edges.style.position = Position.Absolute;
        _nodes.style.position = Position.Absolute;
        _world.Add(_edges);
        _world.Add(_nodes);
        Add(_world);
        _edges.generateVisualContent += DrawEdges;
        this.AddManipulator(new CanvasManipulator(this));
        RegisterCallback<DragUpdatedEvent>(evt =>
        {
            if (!Editable) return;
            foreach (UnityEngine.Object item in DragAndDrop.objectReferences) if (item is SkillNodeDefinition) { DragAndDrop.visualMode = DragAndDropVisualMode.Link; evt.StopPropagation(); break; }
        });
        RegisterCallback<DragPerformEvent>(evt =>
        {
            if (!Editable) return;
            DragAndDrop.AcceptDrag();
            foreach (UnityEngine.Object item in DragAndDrop.objectReferences) if (item is SkillNodeDefinition node) RegisterRequested?.Invoke(node);
            evt.StopPropagation();
        });
        ApplyTransform();
    }

    public void Rebuild(SkillTreeDefinition tree, SkillTreeEditorModel model, SkillTreeLayoutSettings settings, SkillNodeDefinition selected)
    {
        _model = model;
        _nodeSize = settings.NodeSize;
        _connecting = null;
        _nodes.Clear();
        _cards.Clear();
        Vector2 size = model.ContentSize;
        _world.style.width = _edges.style.width = _nodes.style.width = size.x;
        _world.style.height = _edges.style.height = _nodes.style.height = size.y;
        foreach (SkillNodeDefinition node in model.Nodes)
        {
            NodeCard card = new(node, model.Positions[node], _nodeSize, tree && node.NodeID == tree.StartNodeID, model.InvalidNodes.Contains(node));
            _cards.Add(card);
            _nodes.Add(card);
        }
        Select(selected);
        _edges.MarkDirtyRepaint();
    }

    public void Select(SkillNodeDefinition selected)
    {
        foreach (NodeCard card in _cards) card.EnableInClassList("skill-node--selected", card.Node == selected);
    }

    public void FrameAll()
    {
        if (_model == null || contentRect.width <= 0 || contentRect.height <= 0) return;
        Vector2 size = _model.ContentSize;
        _zoom = Mathf.Clamp(Mathf.Min((contentRect.width - 60f) / Mathf.Max(1f, size.x), (contentRect.height - 60f) / Mathf.Max(1f, size.y)), 0.2f, 1.2f);
        _pan = (contentRect.size - size * _zoom) * 0.5f;
        ApplyTransform();
    }

    private void ApplyTransform()
    {
        _world.style.translate = new Translate(_pan.x, _pan.y, 0);
        _world.style.scale = new Scale(new Vector3(_zoom, _zoom, 1));
    }

    private void DrawEdges(MeshGenerationContext context)
    {
        if (_model == null) return;
        Painter2D painter = context.painter2D;
        painter.lineWidth = 3f;
        foreach (SkillNodeDefinition child in _model.Nodes)
        {
            for (int i = 0; i < child.RequiredNodeIds.Count; i++)
            {
                SkillNodeDefinition parent = _model.FindUnique(child.RequiredNodeIds[i]);
                if (!parent || parent == child) continue;
                painter.strokeColor = _model.InvalidNodes.Contains(child) ? new Color(0.95f, 0.42f, 0.35f) : new Color(0.45f, 0.65f, 0.84f);
                DrawConnection(painter, PortPosition(parent, true), PortPosition(child, false));
            }
        }
        if (_connecting)
        {
            painter.strokeColor = new Color(0.35f, 0.85f, 1f);
            DrawConnection(painter, PortPosition(_connecting, true), _connectionEnd);
        }
    }

    private Vector2 PortPosition(SkillNodeDefinition node, bool output)
    {
        Vector2 position = _model.Positions[node];
        return new Vector2(position.x, -position.y + (output ? 0.5f : -0.5f) * _nodeSize.y);
    }

    private static void DrawConnection(Painter2D painter, Vector2 from, Vector2 to)
    {
        float bend = Mathf.Max(30f, Mathf.Abs(to.y - from.y) * 0.5f);
        painter.BeginPath();
        painter.MoveTo(from);
        painter.BezierCurveTo(from + Vector2.up * bend, to - Vector2.up * bend, to);
        painter.Stroke();
    }

    private static NodeCard FindCard(VisualElement element)
    {
        while (element != null) { if (element is NodeCard card) return card; element = element.parent; }
        return null;
    }
}
