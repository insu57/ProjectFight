using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class PlayerWindowTabButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private PlayerTabType _tabType;
    [SerializeField] private Button _button;
    [SerializeField] private GameObject _selectedVisual;
    [SerializeField] private Graphic _selectedVisualGraphic;
    [SerializeField] private Color _selectedColor = Color.white;
    [SerializeField] private Color _focusedColor = new Color(1f, 0.75f, 0.2f, 1f);

    private bool _isSelected;
    private bool _isFocused;
    private bool _isPointerOver;

    public PlayerTabType TabType => _tabType;

    public event Action<PlayerTabType> Selected;

    private void Awake()
    {
        if (!_button)
        {
            TryGetComponent(out _button);
        }

        if (!_selectedVisualGraphic && _selectedVisual)
        {
            _selectedVisual.TryGetComponent(out _selectedVisualGraphic);
        }
    }

    private void OnEnable()
    {
        if (_button)
        {
            _button.onClick.AddListener(HandleClick);
        }

        RefreshVisual();
    }

    private void OnDisable()
    {
        if (_button)
        {
            _button.onClick.RemoveListener(HandleClick);
        }

        _isFocused = false;
        _isPointerOver = false;
        RefreshVisual();
    }

    public void SetSelected(bool isSelected)
    {
        if (_isSelected == isSelected)
        {
            return;
        }

        _isSelected = isSelected;
        RefreshVisual();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _isFocused = true;
        RefreshVisual();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isFocused = false;
        RefreshVisual();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;
        RefreshVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;
        RefreshVisual();
    }

    private void HandleClick()
    {
        Selected?.Invoke(_tabType);
    }

    private void RefreshVisual()
    {
        if (!_selectedVisual)
        {
            return;
        }

        // 키보드·패드 포커스와 포인터 호버를 동일한 포커스 표현으로 처리한다.
        bool hasFocus = _isFocused || _isPointerOver;
        bool shouldShow = _isSelected || hasFocus;

        if (_selectedVisual.activeSelf != shouldShow)
        {
            _selectedVisual.SetActive(shouldShow);
        }

        if (_selectedVisualGraphic)
        {
            // 선택과 포커스가 겹치면 현재 조작 위치를 나타내는 포커스 색상을 우선한다.
            _selectedVisualGraphic.color = hasFocus ? _focusedColor : _selectedColor;
        }
    }
}
