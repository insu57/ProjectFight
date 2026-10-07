using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ItemAcquisitionHUD : MonoBehaviour
{
    [Header("알림 표시")]
    [SerializeField] private CanvasGroup _notificationPanel;
    [SerializeField, Min(0.01f)] private float _displayDuration = 1.5f;

    [Header("아이템 정보")]
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _rarityText;
    [SerializeField] private TMP_Text _itemTypeText;

    // 짧은 연속 획득에는 준비한 큐를 재사용하고 같은 아이템도 획득 순서대로 각각 표시한다.
    private readonly Queue<ItemDefinition> _pendingItems = new(4);
    private Action<ItemDefinition> _itemAcquiredHandler;
    private PlayerManager _playerManager;
    private float _remainingDisplayTime;
    private bool _isShowing;

    private void Awake()
    {
        // 기존 HUD와 동일하게 플레이어 참조를 한 번만 찾고 Manager의 획득 이벤트를 구독한다.
        _playerManager = FindFirstObjectByType<PlayerManager>();
    }

    private void OnEnable()
    {
        _itemAcquiredHandler ??= HandleItemAcquired;
        ClearNotifications();

        if (!_notificationPanel)
        {
            Debug.LogWarning("ItemAcquisitionHUD의 Notification Panel을 지정해야 합니다.", this);
            return;
        }

        if (_playerManager)
        {
            _playerManager.ItemAcquired += _itemAcquiredHandler;
        }
    }

    private void OnDisable()
    {
        if (_playerManager)
        {
            _playerManager.ItemAcquired -= _itemAcquiredHandler;
        }

        ClearNotifications();
    }

    private void HandleItemAcquired(ItemDefinition item)
    {
        if (!isActiveAndEnabled || !item) return;

        if (_isShowing)
        {
            _pendingItems.Enqueue(item);
            return;
        }

        ShowItem(item);
    }

    private void Update()
    {
        if (!_isShowing) return;

        // 플레이어 창이 게임 시간을 멈추더라도 획득 알림의 표시 시간은 정상적으로 흐른다.
        _remainingDisplayTime -= Time.unscaledDeltaTime;
        if (_remainingDisplayTime > 0f) return;

        while (_pendingItems.Count > 0)
        {
            ItemDefinition item = _pendingItems.Dequeue();

            if (item)
            {
                ShowItem(item);
                return;
            }
        }

        HideNotification();
    }

    private void ShowItem(ItemDefinition item)
    {
        if (_icon)
        {
            _icon.sprite = item.ItemIcon;
            _icon.enabled = item.ItemIcon;
        }

        if (_nameText)
        {
            _nameText.SetText(string.IsNullOrEmpty(item.ItemName) ? item.name : item.ItemName);
        }

        if (_rarityText)
        {
            _rarityText.SetText(ItemDisplayUtility.GetRarityText(item.Rarity));
            // 큐의 다음 아이템을 표시할 때도 해당 등급의 색상으로 갱신한다.
            _rarityText.color = ItemDisplayUtility.GetRarityColor(item.Rarity);
        }

        if (_itemTypeText)
        {
            _itemTypeText.SetText(ItemDisplayUtility.GetItemTypeText(item.ItemType));
        }

        _remainingDisplayTime = Mathf.Max(0.01f, _displayDuration);
        _isShowing = true;

        if (_notificationPanel)
        {
            _notificationPanel.alpha = 1f;
        }
    }

    private void ClearNotifications()
    {
        _pendingItems.Clear();
        HideNotification();
    }

    private void HideNotification()
    {
        _isShowing = false;
        _remainingDisplayTime = 0f;

        if (_notificationPanel)
        {
            // 컴포넌트와 GameObject는 활성 상태를 유지해 다음 획득 알림도 계속 수신한다.
            _notificationPanel.alpha = 0f;
            _notificationPanel.interactable = false;
            _notificationPanel.blocksRaycasts = false;
        }

        if (_icon)
        {
            _icon.sprite = null;
            _icon.enabled = false;
        }

        if (_nameText) _nameText.SetText(string.Empty);
        if (_rarityText) _rarityText.SetText(string.Empty);
        if (_itemTypeText) _itemTypeText.SetText(string.Empty);
    }
}
