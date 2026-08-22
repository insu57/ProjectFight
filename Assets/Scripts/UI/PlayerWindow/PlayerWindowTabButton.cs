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
