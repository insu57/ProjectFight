using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class WindowTabButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, IMoveHandler
{
    [field: SerializeField] private Button _button;
    public Button Button => _button;

    [SerializeField] private GameObject _selectedVisual;
    [SerializeField] private Graphic _selectedVisualGraphic;
    [SerializeField] private Color _selectedColor = Color.white;
    [SerializeField] private Color _focusedColor = new Color(1f, 0.75f, 0.2f, 1f);

    private bool _isSelected;
    private bool _isFocused;
    private bool _isPointerOver;
    private Selectable _downNavigationTarget;

    public event Action<WindowTabButton> Selected;

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
        _downNavigationTarget = null;
        RefreshVisual();
    }

    public void SetDownNavigationTarget(Selectable target)
    {
        // 대상은 활성 화면의 View가 전달하며 null이면 버튼의 기존 Navigation을 사용한다.
        _downNavigationTarget = target;
    }

    public void OnMove(AxisEventData eventData)
    {
        if (eventData == null || eventData.moveDir != MoveDirection.Down || !isActiveAndEnabled || !_button
            || !_button.IsActive() || !_button.IsInteractable() || _button.navigation.mode == Navigation.Mode.None
            || !_downNavigationTarget || !_downNavigationTarget.IsActive() || !_downNavigationTarget.IsInteractable()) return;

        // Button과 이 컴포넌트가 같은 Move 이벤트를 받으므로 처리 순서와 관계없이 기본 아래 탐색을 막는다.
        eventData.moveDir = MoveDirection.None;
        eventData.selectedObject = _downNavigationTarget.gameObject;
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
        // 탭의 의미는 창 컨트롤러가 관리하고 버튼은 자신이 선택됐다는 사실만 전달한다.
        Selected?.Invoke(this);
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
