using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillTreeView : MonoBehaviour
{
    private sealed class ConnectionBinding
    {
        public SkillTreeConnectionView View { get; }
        public SkillTreeNodeRuntime FromNode { get; }
        public SkillTreeNodeRuntime ToNode { get; }

        public ConnectionBinding(SkillTreeConnectionView view, SkillTreeNodeRuntime fromNode, SkillTreeNodeRuntime toNode)
        {
            View = view;
            FromNode = fromNode;
            ToNode = toNode;
        }
    }

    private PlayerManager _playerManager;

    [Header("참조")]
    [SerializeField] private RectTransform _treeContainer;
    [SerializeField] private RectTransform _connectionLayer;
    [SerializeField] private RectTransform _nodeLayer;
    [SerializeField] private SkillTreeNodeView _nodePrefab;
    [SerializeField] private SkillTreeConnectionView _connectionPrefab;

    [Header("배치")]
    [SerializeField, Min(1f)] private float _horizontalSpacing = 240f;
    [SerializeField, Min(1f)] private float _verticalSpacing = 160f;
    [SerializeField, Min(0f)] private float _horizontalPadding = 100f;
    [SerializeField, Min(0f)] private float _verticalPadding = 100f;
    [SerializeField] private Vector2 _nodeSize = new Vector2(120f, 120f);

    private readonly Dictionary<int, SkillTreeNodeView> _nodeViews = new Dictionary<int, SkillTreeNodeView>();
    private readonly Dictionary<int, Vector2> _nodePositions = new Dictionary<int, Vector2>();
    private readonly List<ConnectionBinding> _connections = new List<ConnectionBinding>();

    private bool _isBuilt;
    private int _nextLeafIndex;
    private int _maximumDepth;

    private void Awake()
    {
        _playerManager = FindFirstObjectByType<PlayerManager>();
    }

    private void OnEnable()
    {
        if (_playerManager)
        {
            _playerManager.SkillPointsChanged += HandleSkillPointsChanged;
            _playerManager.SkillTreeNodeUnlocked += HandleSkillTreeNodeUnlocked;
        }

        BuildIfNeeded();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (_playerManager)
        {
            _playerManager.SkillPointsChanged -= HandleSkillPointsChanged;
            _playerManager.SkillTreeNodeUnlocked -= HandleSkillTreeNodeUnlocked;
        }
    }

    private void BuildIfNeeded()
    {
        if (_isBuilt)
        {
            return;
        }

        SkillTreeRuntime runtime = _playerManager ? _playerManager.SkillTreeRuntime : null;

        if (runtime == null)
        {
            Debug.LogWarning("표시할 스킬 트리 런타임이 없습니다.", this);
            return;
        }

        if (!_treeContainer || !_connectionLayer || !_nodeLayer || !_nodePrefab || !_connectionPrefab)
        {
            Debug.LogError("SkillTreeView의 UI 참조가 완성되지 않았습니다.", this);
            return;
        }

        _nextLeafIndex = 0;
        _maximumDepth = 0;

        CalculateNodePosition(runtime.StartNode, 0);
        ResizeContainer();
        CreateConnections(runtime.StartNode);
        CreateNodes(runtime.StartNode);

        _isBuilt = true;
    }

    private float CalculateNodePosition(SkillTreeNodeRuntime node, int depth)
    {
        if (depth > _maximumDepth)
        {
            _maximumDepth = depth;
        }

        IReadOnlyList<SkillTreeNodeRuntime> children = node.NextNodes;
        float xPosition;

        if (children.Count == 0)
        {
            xPosition = _horizontalPadding + _nextLeafIndex * _horizontalSpacing;
            _nextLeafIndex++;
        }
        else
        {
            float firstChildX = CalculateNodePosition(children[0], depth + 1);
            float lastChildX = firstChildX;

            for (int i = 1; i < children.Count; i++)
            {
                lastChildX = CalculateNodePosition(children[i], depth + 1);
            }

            // 부모를 첫 번째 자식과 마지막 자식이 차지하는 영역의 중앙에 배치한다.
            xPosition = (firstChildX + lastChildX) * 0.5f;
        }

        float yPosition = -(_verticalPadding + depth * _verticalSpacing);
        _nodePositions.Add(node.NodeID, new Vector2(xPosition, yPosition));
        return xPosition;
    }

    private void ResizeContainer()
    {
        int leafCount = Mathf.Max(1, _nextLeafIndex);
        float width = _horizontalPadding * 2f + (leafCount - 1) * _horizontalSpacing + _nodeSize.x;
        float height = _verticalPadding * 2f + _maximumDepth * _verticalSpacing + _nodeSize.y;

        _treeContainer.anchorMin = new Vector2(0f, 1f);
        _treeContainer.anchorMax = new Vector2(0f, 1f);
        _treeContainer.pivot = new Vector2(0f, 1f);
        _treeContainer.sizeDelta = new Vector2(width, height);
    }

    private void CreateNodes(SkillTreeNodeRuntime node)
    {
        SkillTreeNodeView nodeView = Instantiate(_nodePrefab, _nodeLayer);
        nodeView.Bind(node);
        nodeView.SetPosition(_nodePositions[node.NodeID]);
        nodeView.Selected += HandleNodeSelected;

        _nodeViews.Add(node.NodeID, nodeView);

        IReadOnlyList<SkillTreeNodeRuntime> children = node.NextNodes;

        for (int i = 0; i < children.Count; i++)
        {
            CreateNodes(children[i]);
        }
    }

    private void CreateConnections(SkillTreeNodeRuntime fromNode)
    {
        IReadOnlyList<SkillTreeNodeRuntime> children = fromNode.NextNodes;

        for (int i = 0; i < children.Count; i++)
        {
            SkillTreeNodeRuntime toNode = children[i];
            SkillTreeConnectionView connectionView = Instantiate(_connectionPrefab, _connectionLayer);

            connectionView.Bind(fromNode, toNode);
            connectionView.SetPositions(_nodePositions[fromNode.NodeID], _nodePositions[toNode.NodeID]);

            _connections.Add(new ConnectionBinding(connectionView, fromNode, toNode));
            CreateConnections(toNode);
        }
    }

    private void RefreshAll()
    {
        SkillTreeRuntime runtime = _playerManager ? _playerManager.SkillTreeRuntime : null;

        if (!_isBuilt || runtime == null)
        {
            return;
        }

        int availablePoints = runtime.AvailablePoints;

        foreach (KeyValuePair<int, SkillTreeNodeView> pair in _nodeViews)
        {
            pair.Value.Refresh(availablePoints);
        }

        for (int i = 0; i < _connections.Count; i++)
        {
            _connections[i].View.Refresh();
        }
    }

    private void HandleNodeSelected(int nodeId)
    {
        if (!_playerManager)
        {
            return;
        }

        SkillTreeUnlockResult result = _playerManager.TryUnlockSkillTreeNode(nodeId);

        if (result != SkillTreeUnlockResult.Success)
        {
            Debug.LogWarning($"스킬 노드를 해금하지 못했습니다. NodeID: {nodeId}, Result: {result}", this);
        }
    }

    private void HandleSkillPointsChanged(int availablePoints)
    {
        RefreshAll();
    }

    private void HandleSkillTreeNodeUnlocked(SkillTreeNodeRuntime node)
    {
        RefreshAll();
    }
}
