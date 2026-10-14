using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private WindowTabButton _playerWindowSkillTreeTabButton;
    [SerializeField] private RectTransform _treeContainer;
    [SerializeField] private RectTransform _connectionLayer;
    [SerializeField] private RectTransform _nodeLayer;
    [SerializeField] private SkillTreeNodeView _nodePrefab;
    [SerializeField] private SkillTreeConnectionView _connectionPrefab;
    [SerializeField] private SkillDescriptionView _skillDescriptionView;
    [SerializeField] private SkillUnlockConfirmationView _unlockConfirmationView;
    [SerializeField] private TMP_Text _availablePointsText;

    [Header("배치")]
    [SerializeField, Min(1f)] private float _horizontalSpacing = 240f;
    [SerializeField, Min(1f)] private float _verticalSpacing = 160f;
    [SerializeField, Min(0f)] private float _horizontalPadding = 100f;
    [SerializeField, Min(0f)] private float _verticalPadding = 100f;
    [SerializeField] private Vector2 _nodeSize = new(120f, 120f);

    private readonly Dictionary<int, SkillTreeNodeView> _nodeViews = new();
    private readonly Dictionary<int, Vector2> _nodePositions = new();
    private readonly List<ConnectionBinding> _connections = new();

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

        if (_unlockConfirmationView)
        {
            _unlockConfirmationView.Confirmed += HandleNodeUnlockConfirmed;
        }

        BuildIfNeeded();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (_playerWindowSkillTreeTabButton) _playerWindowSkillTreeTabButton.SetDownNavigationTarget(null);

        if (_playerManager)
        {
            _playerManager.SkillPointsChanged -= HandleSkillPointsChanged;
            _playerManager.SkillTreeNodeUnlocked -= HandleSkillTreeNodeUnlocked;
        }

        if (_unlockConfirmationView)
        {
            _unlockConfirmationView.Confirmed -= HandleNodeUnlockConfirmed;

            // 탭을 전환하거나 플레이어 창을 닫을 때 진행 중인 확인 상태를 초기화한다.
            _unlockConfirmationView.Hide();
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
            xPosition = _horizontalPadding + _nodeSize.x * 0.5f + _nextLeafIndex * _horizontalSpacing;
            _nextLeafIndex++;
        }
        else
        {
            float firstChildX = CalculateNodePosition(children[0], depth + 1);
            float lastChildX = firstChildX;

            for (int i = 1; i < children.Count; i++)
            {
                lastChildX = CalculateNodePosition(children[i], depth + 1); //재귀적으로 계산
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

        // 스킬 트리 콘텐츠를 Viewport 상단 중앙에 배치한다.
        _treeContainer.anchorMin = new Vector2(0.5f, 1f);
        _treeContainer.anchorMax = new Vector2(0.5f, 1f);
        _treeContainer.pivot = new Vector2(0.5f, 1f);
        _treeContainer.anchoredPosition = Vector2.zero;
        _treeContainer.sizeDelta = new Vector2(width, height);
    }

    private void CreateNodes(SkillTreeNodeRuntime node)
    {
        SkillTreeNodeView nodeView = Instantiate(_nodePrefab, _nodeLayer);
        nodeView.Bind(node);
        nodeView.SetPosition(_nodePositions[node.NodeID]);
        nodeView.Selected += HandleNodeSelected;
        nodeView.UnlockRequested += HandleNodeUnlockRequested;

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

        if (_availablePointsText)
        {
            // TMP의 숫자 포맷 오버로드를 사용해 문자열 할당을 줄인다.
            _availablePointsText.SetText("Skill Points: {0}", availablePoints);
        }

        Button entryButton = null;
        Vector2 entryPosition = Vector2.zero;

        foreach (KeyValuePair<int, SkillTreeNodeView> pair in _nodeViews)
        {
            pair.Value.Refresh(availablePoints);

            if (!_playerWindowSkillTreeTabButton || !runtime.TryGetNode(pair.Key, out SkillTreeNodeRuntime node) || !node.IsUnlocked) continue;

            Button button = pair.Value.Button;
            if (!button || !button.IsActive() || !button.IsInteractable()) continue;

            Vector2 position = _nodePositions[pair.Key];

            // 깊을수록 Y가 작아지는 기존 배치를 사용하고, 같은 깊이에서는 가장 오른쪽 노드를 우선한다.
            if (!entryButton || position.y < entryPosition.y || position.y == entryPosition.y && position.x > entryPosition.x)
            {
                entryButton = button;
                entryPosition = position;
            }
        }

        if (_playerWindowSkillTreeTabButton) _playerWindowSkillTreeTabButton.SetDownNavigationTarget(entryButton);

        for (int i = 0; i < _connections.Count; i++)
        {
            _connections[i].View.Refresh();
        }

        if (_skillDescriptionView)
        {
            // 선택 중인 노드의 비용 상태도 잔여 포인트와 해금 결과에 맞춰 갱신한다.
            _skillDescriptionView.Refresh(availablePoints);
        }
    }

    private void HandleNodeSelected(int nodeId)
    {
        SkillTreeRuntime runtime = _playerManager ? _playerManager.SkillTreeRuntime : null;

        if (runtime == null || !_skillDescriptionView)
        {
            return;
        }

        if (runtime.TryGetNode(nodeId, out SkillTreeNodeRuntime node))
        {
            // 현재 선택된 노드의 정의 정보를 설명 영역에 표시한다.
            _skillDescriptionView.Show(node, runtime.AvailablePoints);
        }
    }

    private void HandleNodeUnlockRequested(int nodeId)
    {
        SkillTreeRuntime runtime = _playerManager ? _playerManager.SkillTreeRuntime : null;

        if (runtime == null || !_unlockConfirmationView || !runtime.TryGetNode(nodeId, out SkillTreeNodeRuntime node))
        {
            return;
        }

        // 포인트 부족, 선행 노드 잠김, 이미 해금된 노드에는 확인 팝업을 표시하지 않는다.
        if (!node.CanUnlock(runtime.AvailablePoints))
        {
            return;
        }

        _unlockConfirmationView.Show(node);
    }

    private void HandleNodeUnlockConfirmed(int nodeId)
    {
        SkillTreeRuntime runtime = _playerManager ? _playerManager.SkillTreeRuntime : null;

        if (runtime == null || !runtime.TryGetNode(nodeId, out SkillTreeNodeRuntime node))
        {
            return;
        }

        // 팝업이 열린 사이 포인트나 선행 노드 상태가 바뀌었을 가능성에 대비해 다시 검사한다.
        if (!node.CanUnlock(runtime.AvailablePoints))
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
