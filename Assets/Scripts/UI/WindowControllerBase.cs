using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public abstract class WindowControllerBase : MonoBehaviour
{
    [Header("Window")]
    [SerializeField] private GameObject _windowPanel;

    [Header("Navigation")]
    [SerializeField] private InputActionReference _navigateAction;

    private float _timeScaleBeforeOpen = 1f;
    private bool _isTimeScalePaused;

    protected PlayerManager PlayerManager { get; private set; }
    protected abstract bool PauseGameplayWhileOpen { get; }

    public bool IsOpen => _windowPanel && _windowPanel.activeSelf;

    protected virtual void Awake()
    {
        PlayerManager = FindFirstObjectByType<PlayerManager>();

        if (_windowPanel)
        {
            _windowPanel.SetActive(false);
        }
    }

    protected virtual void OnEnable()
    {
        if (_navigateAction)
        {
            _navigateAction.action.performed += HandleNavigate;
        }

        OnWindowControllerEnabled();
    }

    protected virtual void OnDisable()
    {
        OnWindowControllerDisabled();

        if (_navigateAction)
        {
            _navigateAction.action.performed -= HandleNavigate;
        }

        if (IsOpen)
        {
            SetWindowOpen(false);
        }
    }

    public void OpenWindow()
    {
        SetWindowOpen(true);
    }

    public void CloseWindow()
    {
        SetWindowOpen(false);
    }

    public void ToggleWindow()
    {
        SetWindowOpen(!IsOpen);
    }

    public void SetWindowOpen(bool isOpen)
    {
        if (!_windowPanel || IsOpen == isOpen)
        {
            return;
        }

        _windowPanel.SetActive(isOpen);

        if (isOpen)
        {
            BeginWindowState();
            FocusWindow();
            OnWindowOpened();
            return;
        }

        EndWindowState();
        OnWindowClosed();
    }

    protected abstract void FocusWindow();

    protected virtual void OnWindowControllerEnabled()
    {
    }

    protected virtual void OnWindowControllerDisabled()
    {
    }

    protected virtual void OnWindowOpened()
    {
    }

    protected virtual void OnWindowClosed()
    {
    }

    private void HandleNavigate(InputAction.CallbackContext context)
    {
        if (!IsOpen || !EventSystem.current || EventSystem.current.currentSelectedGameObject)
        {
            return;
        }

        // 선택된 UI가 사라졌다면 현재 창의 기본 탐색 지점으로 포커스를 복구한다.
        FocusWindow();
    }

    private void BeginWindowState()
    {
        if (!PauseGameplayWhileOpen)
        {
            return;
        }

        if (PlayerManager)
        {
            PlayerManager.SetGameplayInputEnabled(false);
        }
        
        _timeScaleBeforeOpen = Time.timeScale;
        Time.timeScale = 0f;
        _isTimeScalePaused = true;
    }

    private void EndWindowState()
    {
        EventSystem.current?.SetSelectedGameObject(null);

        if (!PauseGameplayWhileOpen)
        {
            return;
        }

        if (PlayerManager)
        {
            PlayerManager.SetGameplayInputEnabled(true);
        }
       
        RestoreTimeScale();
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
}
