using System;
using UnityEngine;
using UnityEngine.EventSystems;

public enum PlayerTabType
{
    SkillTree,
    Inventory
}

[DisallowMultipleComponent]
public sealed class PlayerWindowController : MonoBehaviour, ICancelHandler
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
    private float _timeScaleBeforeOpen = 1f;
    private bool _isTimeScalePaused;

    public PlayerTabType CurrentTab => _currentTab;
    public bool IsOpen => _windowPanel && _windowPanel.activeSelf;

    public event Action<PlayerTabType> TabChanged;

    private void Awake()
    {
        _playerManager = FindFirstObjectByType<PlayerManager>();
        _windowPanel.SetActive(false);
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

        if (IsOpen)
        {
            SetWindowOpen(false);
        }
    }

    public void ToggleWindow()
    {
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
        _playerManager?.SetGameplayInputEnabled(!isOpen);

        if (isOpen)
        {
            // 창을 닫을 때 원래 게임 속도로 돌아갈 수 있도록 기존 값을 보관한다.
            _timeScaleBeforeOpen = Time.timeScale;
            Time.timeScale = 0f;
            _isTimeScalePaused = true;
            FocusCurrentTab();
            return;
        }


        EventSystem.current?.SetSelectedGameObject(null);
        RestoreTimeScale();
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (IsOpen)
        {
            // UI Cancel 입력으로 플레이어 창을 닫는다.
            SetWindowOpen(false);
        }
    }

    private void FocusCurrentTab()
    {
        if (!EventSystem.current)
        {
            return;
        }

        for (int i = 0; i < _tabBindings.Length; i++)
        {
            TabBinding binding = _tabBindings[i];

            if (binding != null && binding.Button && binding.Button.TabType == _currentTab)
            {
                // 키보드와 게임패드 탐색이 현재 탭에서 시작되도록 한다.
                EventSystem.current.SetSelectedGameObject(binding.Button.gameObject);
                return;
            }
        }
    }

    private void RestoreTimeScale()
    {
        if (!_isTimeScalePaused)
        {
            return;
        }

        Time.timeScale = _timeScaleBeforeOpen;
        _isTimeScalePaused = false;
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
