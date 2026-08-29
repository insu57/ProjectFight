using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SkillTreeNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("기본 UI")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _focusImage;

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
    private bool _isFocused;
    private bool _isPointerOver;

    public int NodeID => _node != null ? _node.NodeID : -1;

    public event Action<int> Selected;
    public event Action<int> UnlockRequested;

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

        RefreshFocusVisual();
    }

    private void OnDisable()
    {
        if (_button)
        {
            _button.onClick.RemoveListener(HandleClick);
        }

        _isFocused = false;
        _isPointerOver = false;
        RefreshFocusVisual();
    }

    public void Bind(SkillTreeNodeRuntime node)
    {
        _node = node;

        if (_icon)
        {
            _icon.sprite = node.Definition.NodeIcon;
            _icon.enabled = node.Definition.NodeIcon;
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

    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;
        RefreshFocusVisual();
        NotifySelected();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;
        RefreshFocusVisual();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _isFocused = true;
        RefreshFocusVisual();
        NotifySelected();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isFocused = false;
        RefreshFocusVisual();
    }

    private void RefreshFocusVisual()
    {
        if (!_focusImage)
        {
            return;
        }

        // 키보드·패드 선택 또는 포인터 호버 중이면 포커스 이미지를 표시한다.
        bool shouldShow = _isFocused || _isPointerOver;

        if (_focusImage.enabled != shouldShow)
        {
            _focusImage.enabled = shouldShow;
        }
    }

    private void NotifySelected()
    {
        if (_node != null)
        {
            Selected?.Invoke(_node.NodeID);
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
            UnlockRequested?.Invoke(_node.NodeID);
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
