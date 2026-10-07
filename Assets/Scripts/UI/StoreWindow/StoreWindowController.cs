using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StoreWindowController : WindowControllerBase
{
    [Header("Close")]
    [SerializeField] private Button _closeButton;

    private UnityAction _closeWindowHandler;

    public Store CurrentStore { get; private set; }

    // 기존 플레이어 창과 같은 입력 차단 및 일시정지 처리를 재사용한다.
    protected override bool PauseGameplayWhileOpen => true;

    public void OpenStore(Store store, PlayerManager player)
    {
        if (!isActiveAndEnabled || IsOpen || !store || !store.isActiveAndEnabled || !player || !player.isActiveAndEnabled || player != PlayerManager)
        {
            return;
        }

        CurrentStore = store;
        OpenWindow();

        // 패널이 연결되지 않아 열기에 실패한 경우 상점 참조도 남기지 않는다.
        if (!IsOpen)
        {
            CurrentStore = null;
        }
    }

    protected override void OnWindowControllerEnabled()
    {
        if (_closeButton)
        {
            _closeWindowHandler ??= CloseWindow;
            _closeButton.onClick.AddListener(_closeWindowHandler);
        }
    }

    protected override void OnWindowControllerDisabled()
    {
        if (_closeButton && _closeWindowHandler != null)
        {
            _closeButton.onClick.RemoveListener(_closeWindowHandler);
        }

        CurrentStore = null;
    }

    protected override void OnWindowClosed()
    {
        CurrentStore = null;
    }

    protected override void FocusWindow()
    {
        if (EventSystem.current && _closeButton && _closeButton.isActiveAndEnabled && _closeButton.IsInteractable())
        {
            EventSystem.current.SetSelectedGameObject(_closeButton.gameObject);
        }
    }
}
