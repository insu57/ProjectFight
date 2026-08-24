using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SkillTreeNodeView : MonoBehaviour
{
    [Header("기본 UI")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _costText;

    [Header("노드 타입")]
    [SerializeField] private GameObject _startVisual;
    [SerializeField] private GameObject _mainVisual;
    [SerializeField] private GameObject _subVisual;

    [Header("해금 상태")]
    [SerializeField] private GameObject _lockedVisual;
    [SerializeField] private GameObject _availableVisual;
    [SerializeField] private GameObject _unlockedVisual;

    private RectTransform _rectTransform;
    private SkillTreeNodeRuntime _node;

    public int NodeID => _node != null ? _node.NodeID : -1;

    public event Action<int> Selected;

    private void Awake()
    {
        TryGetComponent(out _rectTransform);
    }

    private void OnEnable()
    {
        if (_button)
        {
            _button.onClick.AddListener(HandleClick);
        }
    }

    private void OnDisable()
    {
        if (_button)
        {
            _button.onClick.RemoveListener(HandleClick);
        }
    }

    public void Bind(SkillTreeNodeRuntime node)
    {
        _node = node;

        if (_icon)
        {
            _icon.sprite = node.Definition.NodeIcon;
            _icon.enabled = node.Definition.NodeIcon;
        }

        
        if (_nameText)
        {
            _nameText.SetText(node.Definition.NodeName);
        }

        if (_costText)
        {
            _costText.SetText(node.IsStartNode ? string.Empty : node.Definition.RequiredPoints.ToString());
        }

        SetTypeVisual(node.Definition.NodeType);
    }

    public void SetPosition(Vector2 anchoredPosition)
    {
        _rectTransform.anchorMin = new Vector2(0f, 1f);
        _rectTransform.anchorMax = new Vector2(0f, 1f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.anchoredPosition = anchoredPosition;
    }

    public void Refresh(int availablePoints)
    {
        if (_node == null)
        {
            return;
        }

        bool isUnlocked = _node.IsUnlocked;
        bool canUnlock = _node.CanUnlock(availablePoints);

        SetActiveIfChanged(_lockedVisual, !isUnlocked && !canUnlock);
        SetActiveIfChanged(_availableVisual, !isUnlocked && canUnlock);
        SetActiveIfChanged(_unlockedVisual, isUnlocked);

        if (_button)
        {
            _button.interactable = canUnlock;
        }
    }

    private void SetTypeVisual(SkillTreeNodeType nodeType)
    {
        SetActiveIfChanged(_startVisual, nodeType == SkillTreeNodeType.Start);
        SetActiveIfChanged(_mainVisual, nodeType == SkillTreeNodeType.Main);
        SetActiveIfChanged(_subVisual, nodeType == SkillTreeNodeType.Sub);
    }

    private void HandleClick()
    {
        if (_node != null)
        {
            Selected?.Invoke(_node.NodeID);
        }
    }

    private static void SetActiveIfChanged(GameObject target, bool isActive)
    {
        if (target && target.activeSelf != isActive)
        {
            target.SetActive(isActive);
        }
    }
}
