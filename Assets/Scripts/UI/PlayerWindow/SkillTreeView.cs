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

        SkillTreeLayoutSettings settings = new(_horizontalSpacing, _verticalSpacing, _horizontalPadding, _verticalPadding, _nodeSize);
        Vector2 contentSize = SkillTreeLayoutUtility.Calculate(runtime.StartNode, settings, _nodePositions);
        ResizeContainer(contentSize);
        // 합류 노드는 한 번만 만들고, 각 부모의 연결선은 모두 만든다.
        foreach (SkillTreeNodeRuntime node in runtime.Nodes.Values)
        {
            CreateNodes(node);
            CreateConnections(node);
        }

        _isBuilt = true;
    }

    private void ResizeContainer(Vector2 contentSize)
    {
        // 스킬 트리 콘텐츠를 Viewport 상단 중앙에 배치한다.
        _treeContainer.anchorMin = new Vector2(0.5f, 1f);
        _treeContainer.anchorMax = new Vector2(0.5f, 1f);
        _treeContainer.pivot = new Vector2(0.5f, 1f);
        _treeContainer.anchoredPosition = Vector2.zero;
        _treeContainer.sizeDelta = contentSize;
    }

    private void CreateNodes(SkillTreeNodeRuntime node)
    {
        SkillTreeNodeView nodeView = Instantiate(_nodePrefab, _nodeLayer);
        nodeView.Bind(node);
        nodeView.SetPosition(_nodePositions[node.NodeID]);
        nodeView.Selected += HandleNodeSelected;
        nodeView.UnlockRequested += HandleNodeUnlockRequested;

        _nodeViews.Add(node.NodeID, nodeView);

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
