using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StoreWindowController : WindowControllerBase
{
    private const string MaxStackMessage = "이미 최대 보유 수량(MaxStack)입니다.";

    [Header("Button")]
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _rerollBtn;
    [SerializeField] private TMP_Text _rerollPriceTxt;

    [Header("고정 상품 뷰 (4개)")]
    // 사전에 배치한 뷰를 순서대로 연결하며 창을 열 때 표시 데이터만 교체한다.
    [SerializeField] private StoreItemView[] _itemViews = new StoreItemView[Store.ItemCount];

    [Header("구매 알림")]
    [SerializeField] private GameObject _purchaseMessageRoot;
    [SerializeField] private TMP_Text _purchaseMessageText;
    [SerializeField, Min(0.01f)] private float _purchaseMessageDuration = 1.5f;

    private UnityAction _closeWindowHandler;
    private UnityAction _rerollHandler;
    private Action<StoreItemView> _purchaseRequestedHandler;
    // 정수 비용의 10자리 버퍼를 재사용해 가격 갱신마다 문자열을 생성하지 않는다.
    private readonly char[] _rerollPriceCharacters = new char[10];
    private int _displayedRerollPrice = -1;
    private float _remainingPurchaseMessageTime;

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
        ClearPurchaseMessage();
        if (_purchaseMessageText) _purchaseMessageText.raycastTarget = false;

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

        _purchaseRequestedHandler ??= HandlePurchaseRequested;
        if (_itemViews == null) return;

        for (int index = 0; index < _itemViews.Length; index++)
        {
            if (_itemViews[index])
            {
                _itemViews[index].PurchaseRequested += _purchaseRequestedHandler;
            }
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

        if (_itemViews != null && _purchaseRequestedHandler != null)
        {
            for (int index = 0; index < _itemViews.Length; index++)
            {
                if (_itemViews[index])
                {
                    _itemViews[index].PurchaseRequested -= _purchaseRequestedHandler;
                }
            }
        }

        ClearItemViews();
        ClearRerollPrice();
        ClearPurchaseMessage();
        CurrentStore = null;
    }

    protected override void OnWindowOpened()
    {
        ClearPurchaseMessage();
        RefreshItemViews();
        //ConfigureNavigation();
        RefreshRerollPrice();

        // 기본 클래스의 포커스 지정은 상품 Bind보다 먼저 실행되므로, 사용 가능한 상품을 갱신한 뒤 다시 선택한다.
        FocusWindow();
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
        ClearPurchaseMessage();
        CurrentStore = null;
    }

    private void HandleReroll()
    {
        if (!IsOpen || !CurrentStore || !PlayerManager) return;

        bool rerolled = CurrentStore.TryReroll(PlayerManager);
        if (!IsOpen) return;

        if (rerolled)
        {
            ClearPurchaseMessage();
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

    private void HandlePurchaseRequested(StoreItemView view)
    {
        if (!isActiveAndEnabled || !IsOpen || !CurrentStore || !PlayerManager || !view || _itemViews == null) return;

        for (int index = 0; index < _itemViews.Length && index < Store.ItemCount; index++)
        {
            if (_itemViews[index] != view) continue;

            Store store = CurrentStore;
            ItemDefinition item = view.Item;
            if (!store.isActiveAndEnabled || !PlayerManager.isActiveAndEnabled || !item || store.Items[index] != item) return;

            // 최대 수량이면 상점 상품과 잔액을 건드리지 않고 짧은 안내만 표시한다.
            if (PlayerManager.IsConsumableStackFull(item))
            {
                ShowMaxStackMessage();
                return;
            }

            bool purchased = store.TryPurchaseItem(index, item, PlayerManager);
            // 획득 알림에서 창을 닫거나 다른 상점으로 바꿨다면 이전 상점의 UI를 갱신하지 않는다.
            if (!IsOpen || CurrentStore != store) return;

            if (purchased)
            {
                ClearPurchaseMessage();
                RefreshItemViews();
                FocusWindow();
            }

            RefreshRerollPrice();
            return;
        }
    }

    private void ShowMaxStackMessage()
    {
        if (!_purchaseMessageRoot || !_purchaseMessageText) return;

        // TMP 부모 전체를 표시하고 반복 실패에는 같은 알림의 표시 시간만 갱신한다.
        _purchaseMessageText.SetText(MaxStackMessage);
        _remainingPurchaseMessageTime = Mathf.Max(0.01f, _purchaseMessageDuration);
        if (!_purchaseMessageRoot.activeSelf) _purchaseMessageRoot.SetActive(true);
    }

    private void Update()
    {
        if (_remainingPurchaseMessageTime <= 0f) return;

        // 상점은 게임 시간을 멈추므로 실제 경과 시간으로 알림을 숨긴다.
        _remainingPurchaseMessageTime -= Time.unscaledDeltaTime;
        if (_remainingPurchaseMessageTime <= 0f) ClearPurchaseMessage();
    }

    private void ClearPurchaseMessage()
    {
        _remainingPurchaseMessageTime = 0f;
        // 텍스트 내용 대신 부모의 활성 상태로 알림 전체를 함께 숨긴다.
        if (_purchaseMessageRoot && _purchaseMessageRoot.activeSelf) _purchaseMessageRoot.SetActive(false);
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
