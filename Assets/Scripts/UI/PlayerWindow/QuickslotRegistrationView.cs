using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class QuickslotRegistrationView : MonoBehaviour, IPointerDownHandler
{
    [Serializable]
    private sealed class SlotBinding
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;

        public Button Button => _button;

        public void Refresh(ItemDefinition item)
        {
            if (_icon)
            {
                _icon.sprite = item ? item.ItemIcon : null;
                _icon.enabled = _icon.sprite;
            }
        }
    }

    [SerializeField] private GameObject _panel;
    [SerializeField, Tooltip("등록 패널을 포함하지 않는 플레이어 창 본문의 CanvasGroup입니다. 창 탭도 함께 포함하세요.")]
    private CanvasGroup _backgroundGroup;
    [SerializeField] private TMP_Text _selectedItemNameText;
    [SerializeField, Tooltip("배열 순서가 퀵슬롯 1~4입니다.")]
    private SlotBinding[] _slots = new SlotBinding[PlayerQuickslot.SlotCount];
    [SerializeField] private Button _unregisterButton;
    [SerializeField] private Button _cancelButton;

    private UnityAction[] _slotHandlers;
    private PlayerManager _player;
    private ItemDefinition _pendingItem;
    private InventoryItemSlotView _sourceSlot;
    private bool _blockedBackground;
    private bool _previousInteractable;
    private bool _previousBlocksRaycasts;

    public bool IsOpen => _panel && _panel.activeSelf;

    private void Awake()
    {
        // 리스너는 한 번 만들고 패널을 열고 닫을 때 재사용한다.
        _slotHandlers = new UnityAction[] { RegisterSlot1, RegisterSlot2, RegisterSlot3, RegisterSlot4 };
        Hide();
    }

    private void OnEnable()
    {
        for (int i = 0; i < Mathf.Min(_slots?.Length ?? 0, PlayerQuickslot.SlotCount); i++)
        {
            if (_slots[i]?.Button) _slots[i].Button.onClick.AddListener(_slotHandlers[i]);
        }

        if (_unregisterButton) _unregisterButton.onClick.AddListener(Unregister);
        if (_cancelButton) _cancelButton.onClick.AddListener(Cancel);
    }

    private void OnDisable()
    {
        for (int i = 0; i < Mathf.Min(_slots?.Length ?? 0, PlayerQuickslot.SlotCount); i++)
        {
            if (_slots[i]?.Button) _slots[i].Button.onClick.RemoveListener(_slotHandlers[i]);
        }

        if (_unregisterButton) _unregisterButton.onClick.RemoveListener(Unregister);
        if (_cancelButton) _cancelButton.onClick.RemoveListener(Cancel);
        Hide();
    }

    public bool Show(PlayerManager player, ItemDefinition item, InventoryItemSlotView sourceSlot)
    {
        if (!isActiveAndEnabled || IsOpen || !player || !player.CanRegisterQuickslot(item)
            || !sourceSlot || !sourceSlot.isActiveAndEnabled || !sourceSlot.IsSpawned || sourceSlot.Item != item) return false;

        if (!HasValidBindings())
        {
            Debug.LogWarning("퀵슬롯 등록 패널의 Panel, 배경 CanvasGroup, 1~4번 버튼, 등록 해제 및 취소 버튼을 연결하세요. " +
                "Panel은 배경 CanvasGroup 밖에 두세요.", this);
            return false;
        }

        _player = player;
        _pendingItem = item;
        _sourceSlot = sourceSlot;
        _player.QuickslotsChanged += Refresh;

        // 등록 중에는 뒤쪽의 아이템과 창 탭이 마우스/패드 입력을 받지 않도록 한다.
        _previousInteractable = _backgroundGroup.interactable;
        _previousBlocksRaycasts = _backgroundGroup.blocksRaycasts;
        _blockedBackground = true;
        _backgroundGroup.interactable = false;
        _backgroundGroup.blocksRaycasts = false;

        if (_selectedItemNameText)
        {
            _selectedItemNameText.SetText(string.IsNullOrEmpty(item.ItemName) ? item.name : item.ItemName);
        }

        _panel.SetActive(true);
        Refresh();
        int registeredSlot = _player.GetQuickslotNumber(item);
        FocusButton(registeredSlot > 0 ? _slots[registeredSlot - 1].Button : _slots[0].Button);
        return true;
    }

    private bool HasValidBindings()
    {
        if (!_panel || _panel == gameObject || !_backgroundGroup || _panel.transform.IsChildOf(_backgroundGroup.transform)
            || !_unregisterButton || !_cancelButton || _slots?.Length != PlayerQuickslot.SlotCount) return false;

        for (int i = 0; i < PlayerQuickslot.SlotCount; i++)
        {
            if (_slots[i] == null || !_slots[i].Button || !_slots[i].Button.transform.IsChildOf(_panel.transform)) return false;
        }

        return _unregisterButton.transform.IsChildOf(_panel.transform) && _cancelButton.transform.IsChildOf(_panel.transform);
    }

    public void Hide()
    {
        if (_player) _player.QuickslotsChanged -= Refresh;
        _player = null;
        _pendingItem = null;
        _sourceSlot = null;

        // 외부에서 창/탭이 닫히는 경로에서도 기존 CanvasGroup 상태를 정확히 복구한다.
        if (_blockedBackground && _backgroundGroup)
        {
            _backgroundGroup.interactable = _previousInteractable;
            _backgroundGroup.blocksRaycasts = _previousBlocksRaycasts;
        }

        _blockedBackground = false;
        // 강제 종료 시 비활성 패널의 버튼이 EventSystem 선택으로 남지 않도록 정리한다.
        GameObject selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (_panel && selected && selected.transform.IsChildOf(_panel.transform)) EventSystem.current.SetSelectedGameObject(null);
        if (_panel && _panel.activeSelf) _panel.SetActive(false);
    }

    public void Cancel()
    {
        if (IsOpen) Close();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsOpen) FocusEntryPoint();
    }

    private void LateUpdate()
    {
        if (!IsOpen) return;
        if (!IsPendingItemValid())
        {
            Close();
            return;
        }

        GameObject selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (!selected || !selected.transform.IsChildOf(_panel.transform)
            || !selected.TryGetComponent(out Button button) || !button.IsActive() || !button.IsInteractable()) FocusEntryPoint();
    }

    private bool IsPendingItemValid()
    {
        // 풀 반환이나 재바인딩으로 선택 항목이 바뀌면 오래된 패널 입력을 취소한다.
        return _player && _player.CanRegisterQuickslot(_pendingItem) && _sourceSlot && _sourceSlot.IsSpawned
            && _sourceSlot.isActiveAndEnabled && _sourceSlot.Item == _pendingItem;
    }

    private void RegisterSlot1() { Register(1); }
    private void RegisterSlot2() { Register(2); }
    private void RegisterSlot3() { Register(3); }
    private void RegisterSlot4() { Register(4); }

    private void Register(int slotNumber)
    {
        // 패널에서 같은 칸을 고르는 것은 등록 유지이며 해제는 별도 버튼으로만 처리한다.
        if (IsOpen && IsPendingItemValid() && _player.TryRegisterQuickslot(slotNumber, _pendingItem)) Close();
    }

    private void Unregister()
    {
        if (IsOpen && IsPendingItemValid() && _player.TryUnregisterQuickslot(_pendingItem)) Close();
    }

    private void Close()
    {
        InventoryItemSlotView sourceSlot = _sourceSlot;
        ItemDefinition item = _pendingItem;
        Hide();

        // 패널이 열려 있는 동안 슬롯이 재사용됐다면 다른 아이템으로 포커스를 복원하지 않는다.
        if (sourceSlot && sourceSlot.IsSpawned && sourceSlot.isActiveAndEnabled && sourceSlot.Item == item
            && sourceSlot.Button && sourceSlot.Button.IsActive() && sourceSlot.Button.IsInteractable()) FocusButton(sourceSlot.Button);
    }

    private void Refresh()
    {
        if (!IsOpen || !_player) return;
        for (int i = 0; i < PlayerQuickslot.SlotCount; i++)
        {
            _slots[i].Refresh(_player.GetQuickslotItem(i + 1));
        }

        _unregisterButton.interactable = _player.GetQuickslotNumber(_pendingItem) > 0;
        for (int i = 0; i < PlayerQuickslot.SlotCount + 2; i++)
        {
            Button button = GetButton(i);
            // 명시적 연결만 사용해 방향 입력이 등록 패널 밖으로 나가지 않도록 한다.
            Button previous = FindNavigationButton(i, -1);
            Button next = FindNavigationButton(i, 1);
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = previous,
                selectOnDown = next, selectOnLeft = previous, selectOnRight = next };
        }
    }

    private Button GetButton(int index)
    {
        if (index < PlayerQuickslot.SlotCount) return _slots[index].Button;
        return index == PlayerQuickslot.SlotCount ? _unregisterButton : _cancelButton;
    }

    private Button FindNavigationButton(int start, int direction)
    {
        int count = PlayerQuickslot.SlotCount + 2;
        for (int offset = 1; offset <= count; offset++)
        {
            int index = (start + direction * offset + count) % count;
            Button candidate = GetButton(index);
            if (candidate.IsActive() && candidate.IsInteractable()) return candidate;
        }

        return null;
    }

    private void FocusEntryPoint()
    {
        for (int i = 0; i < PlayerQuickslot.SlotCount + 2; i++)
        {
            Button button = GetButton(i);
            if (button.IsActive() && button.IsInteractable())
            {
                FocusButton(button);
                return;
            }
        }
    }

    private static void FocusButton(Button button)
    {
        if (EventSystem.current && button) EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}
