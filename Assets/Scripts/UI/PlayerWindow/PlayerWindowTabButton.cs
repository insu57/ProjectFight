using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class PlayerWindowTabButton : MonoBehaviour
{
    [SerializeField] private PlayerTabType _tabType;
    [SerializeField] private Button _button;
    [SerializeField] private GameObject _selectedVisual;

    public PlayerTabType TabType => _tabType;

    public event Action<PlayerTabType> Selected;

    private void Awake()
    {
        if (!_button)
        {
            TryGetComponent(out _button);
        }
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

    public void SetSelected(bool isSelected)
    {
        if (_button)
        {
            // 선택된 탭은 다시 누르지 못하게 하여 불필요한 선택 요청을 막는다.
            _button.interactable = !isSelected;
        }

        if (_selectedVisual && _selectedVisual.activeSelf != isSelected)
        {
            _selectedVisual.SetActive(isSelected);
        }
    }

    private void HandleClick()
    {
        Selected?.Invoke(_tabType);
    }
}
