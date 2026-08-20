using System;
using UnityEngine;
public enum PlayerTabType
{
    SkillTree,
    Inventory
}

[DisallowMultipleComponent]
public sealed class PlayerWindowController : MonoBehaviour
{
    

    [Serializable]
    private sealed class TabBinding
    {
        [SerializeField] private PlayerWindowTabButton _button;
        [SerializeField] private GameObject _content;

        public PlayerWindowTabButton Button => _button;
        public GameObject Content => _content;
    }

    [SerializeField] private PlayerTabType _initialTab = PlayerTabType.Inventory;
    [SerializeField] private TabBinding[] _tabBindings;
    private PlayerManager _playerManager;
    [SerializeField] private GameObject _windowPanel;

    private PlayerTabType _currentTab;
    private bool _hasSelectedTab;

    public PlayerTabType CurrentTab => _currentTab;
    public bool IsOpen => _windowPanel && _windowPanel.activeSelf;

    public event Action<PlayerTabType> TabChanged;

    private void Awake()
    {
        _playerManager = FindFirstObjectByType<PlayerManager>();
    }

    private void OnEnable()
    {
        if (_playerManager)
        {
            _playerManager.PlayerWindowRequested += ToggleWindow;
        }

        SubscribeTabButtons();
        SelectTab(_hasSelectedTab ? _currentTab : _initialTab);
    }

    private void OnDisable()
    {
        if (_playerManager)
        {
            _playerManager.PlayerWindowRequested -= ToggleWindow;
        }

        UnsubscribeTabButtons();
    }

    public void ToggleWindow()
    {
        Debug.Log("???");
        SetWindowOpen(!IsOpen);
    }

    public void SetWindowOpen(bool isOpen)
    {
        if (!_windowPanel || _windowPanel.activeSelf == isOpen)
        {
            return;
        }

        // 입력을 계속 받기 위해 Controller는 유지하고 표시 패널만 활성화한다.
        _windowPanel.SetActive(isOpen);
    }

    public void SelectTab(PlayerTabType tabType)
    {
        if (!ContainsTab(tabType))
        {
            Debug.LogWarning($"등록되지 않은 플레이어 창 탭입니다: {tabType}", this);
            return;
        }

        for (int i = 0; i < _tabBindings.Length; i++)
        {
            TabBinding binding = _tabBindings[i];

            if (binding == null || !binding.Button)
            {
                continue;
            }

            bool isSelected = binding.Button.TabType == tabType;
            binding.Button.SetSelected(isSelected);

            if (binding.Content && binding.Content.activeSelf != isSelected)
            {
                binding.Content.SetActive(isSelected);
            }
        }

        if (_hasSelectedTab && _currentTab == tabType)
        {
            return;
        }

        _currentTab = tabType;
        _hasSelectedTab = true;
        TabChanged?.Invoke(_currentTab);
    }

    private bool ContainsTab(PlayerTabType tabType)
    {
        for (int i = 0; i < _tabBindings.Length; i++)
        {
            TabBinding binding = _tabBindings[i];

            if (binding != null && binding.Button && binding.Button.TabType == tabType)
            {
                return true;
            }
        }

        return false;
    }

    private void SubscribeTabButtons()
    {
        for (int i = 0; i < _tabBindings.Length; i++)
        {
            PlayerWindowTabButton button = _tabBindings[i]?.Button;

            if (button)
            {
                // 버튼과 콘텐츠 사이의 호출은 창 Controller가 중개한다.
                button.Selected += SelectTab;
            }
        }
    }

    private void UnsubscribeTabButtons()
    {
        for (int i = 0; i < _tabBindings.Length; i++)
        {
            PlayerWindowTabButton button = _tabBindings[i]?.Button;

            if (button)
            {
                button.Selected -= SelectTab;
            }
        }
    }
}
