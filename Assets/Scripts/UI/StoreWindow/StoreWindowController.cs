using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StoreWindowController : WindowControllerBase
{
    [Header("Button")]
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _rerollBtn;
    [SerializeField] private TMP_Text _rerollPriceTxt;

    [Header("고정 상품 뷰 (4개)")]
    // 사전에 배치한 뷰를 순서대로 연결하며 창을 열 때 표시 데이터만 교체한다.
    [SerializeField] private StoreItemView[] _itemViews = new StoreItemView[Store.ItemCount];

    private UnityAction _closeWindowHandler;
    private UnityAction _rerollHandler;
    // 정수 비용의 10자리 버퍼를 재사용해 가격 갱신마다 문자열을 생성하지 않는다.
    private readonly char[] _rerollPriceCharacters = new char[10];
    private int _displayedRerollPrice = -1;

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

        if (_rerollBtn)
        {
            _rerollHandler ??= HandleReroll;
            _rerollBtn.onClick.AddListener(_rerollHandler);
        }

        if (PlayerManager)
        {
            PlayerManager.PlayerStatsChanged += HandlePlayerStatsChanged;
        }
    }

    protected override void OnWindowControllerDisabled()
    {
        if (_closeButton && _closeWindowHandler != null)
        {
            _closeButton.onClick.RemoveListener(_closeWindowHandler);
        }

        if (_rerollBtn && _rerollHandler != null)
        {
            _rerollBtn.onClick.RemoveListener(_rerollHandler);
        }

        if (PlayerManager)
        {
            PlayerManager.PlayerStatsChanged -= HandlePlayerStatsChanged;
        }

        ClearItemViews();
        ClearRerollPrice();
        CurrentStore = null;
    }

    protected override void OnWindowOpened()
    {
        RefreshItemViews();
        //ConfigureNavigation();
        RefreshRerollPrice();

        // 기본 클래스의 포커스 지정은 상품 Bind보다 먼저 실행되므로, 사용 가능한 상품을 갱신한 뒤 다시 선택한다.
        FocusWindow();
    }

    private void ConfigureNavigation()
    {
        ConfigureButtonNavigation(_closeButton);
        ConfigureButtonNavigation(_rerollBtn);
        if (_itemViews == null) return;

        for (int index = 0; index < _itemViews.Length; index++)
        {
            if (_itemViews[index])
            {
                ConfigureButtonNavigation(_itemViews[index].Button);
            }
        }
    }

    private static void ConfigureButtonNavigation(Button button)
    {
        if (!button) return;

        // 방향 이동은 Unity의 자동 탐색에 맡기고, 선택 상태도 기존 마우스 Hover 색상으로 표시한다.
        //Navigation navigation = button.navigation;
        //navigation.mode = Navigation.Mode.Automatic;
        //button.navigation = navigation;

        //ColorBlock colors = button.colors;
        //colors.selectedColor = colors.highlightedColor;
        //button.colors = colors;
    }

    private void RefreshItemViews()
    {
        if (_itemViews == null) return;

        for (int index = 0; index < _itemViews.Length; index++)
        {
            StoreItemView view = _itemViews[index];
            if (!view) continue;

            view.Bind(CurrentStore && index < Store.ItemCount ? CurrentStore.Items[index] : null);
        }
    }

    protected override void OnWindowClosed()
    {
        ClearItemViews();
        ClearRerollPrice();
        CurrentStore = null;
    }

    private void HandleReroll()
    {
        if (!IsOpen || !CurrentStore || !PlayerManager) return;

        bool rerolled = CurrentStore.TryReroll(PlayerManager);
        if (!IsOpen) return;

        if (rerolled)
        {
            RefreshItemViews();
        }

        RefreshRerollPrice();
    }

    private void HandlePlayerStatsChanged(PlayerStatSnapshot stats)
    {
        if (IsOpen)
        {
            // 다른 경로로 Money가 바뀌어도 열린 상점의 지불 가능 색상을 즉시 갱신한다.
            RefreshRerollPrice(stats.Money);
        }
    }

    private void RefreshRerollPrice()
    {
        RefreshRerollPrice(PlayerManager ? PlayerManager.CurrentPlayerStats.Money : 0);
    }

    private void RefreshRerollPrice(int money)
    {
        if (!_rerollPriceTxt || !CurrentStore) return;

        int price = CurrentStore.RerollPrice;
        if (_displayedRerollPrice != price)
        {
            int remaining = price;
            int start = _rerollPriceCharacters.Length;
            do
            {
                _rerollPriceCharacters[--start] = (char)('0' + remaining % 10);
                remaining /= 10;
            } while (remaining > 0);

            _rerollPriceTxt.SetCharArray(_rerollPriceCharacters, start, _rerollPriceCharacters.Length - start);
            _displayedRerollPrice = price;
        }

        _rerollPriceTxt.color = ItemDisplayUtility.GetPriceColor(money >= price);
    }

    private void ClearRerollPrice()
    {
        _displayedRerollPrice = -1;
        if (_rerollPriceTxt)
        {
            _rerollPriceTxt.SetText(string.Empty);
        }
    }

    private void ClearItemViews()
    {
        if (_itemViews == null) return;

        for (int index = 0; index < _itemViews.Length; index++)
        {
            if (_itemViews[index])
            {
                // 오브젝트는 유지하고 이전 상점의 아이템 참조와 표시만 비운다.
                _itemViews[index].Bind(null);
            }
        }
    }

    protected override void FocusWindow()
    {
        EventSystem eventSystem = EventSystem.current;
        if (!eventSystem) return;

        // 창을 열거나 Navigate로 선택을 복구할 때, 표시 순서상 첫 사용 가능한 상품을 우선한다.
        if (_itemViews != null)
        {
            for (int index = 0; index < _itemViews.Length; index++)
            {
                StoreItemView view = _itemViews[index];
                if (view && view.Item && TryFocusButton(view.Button, eventSystem)) return;
            }
        }

        if (!TryFocusButton(_rerollBtn, eventSystem))
        {
            TryFocusButton(_closeButton, eventSystem);
        }
    }

    private static bool TryFocusButton(Button button, EventSystem eventSystem)
    {
        if (!button || !button.isActiveAndEnabled || !button.IsInteractable()) return false;

        eventSystem.SetSelectedGameObject(button.gameObject);
        return true;
    }
}
