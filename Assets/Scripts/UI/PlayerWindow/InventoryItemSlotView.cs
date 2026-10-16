using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class InventoryItemSlotView : PoolableObject, IPointerEnterHandler, ISelectHandler, IPointerClickHandler, ISubmitHandler
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private GameObject _selectedVisual;

    [Header("Equipment")]
    [SerializeField] private GameObject _equippedVisual;
    [SerializeField] private Image _equipmentTypeVisual;
    [SerializeField] private Sprite _headSprite;
    [SerializeField] private Sprite _upperSprite;
    [SerializeField] private Sprite _lowerSprite;
    [SerializeField] private Sprite _glovesSprite;
    [SerializeField] private Sprite _shoesSprite;

    [Header("QuickSlot")]
    [SerializeField] private GameObject _quickSlotVisualParent;
    [SerializeField] private Image _quickSlotVisual;
    [SerializeField] private Sprite _quickSlot01Sprite;
    [SerializeField] private Sprite _quickSlot02Sprite;
    [SerializeField] private Sprite _quickSlot03Sprite;
    [SerializeField] private Sprite _quickSlot04Sprite;
    private ItemDefinition _firstClickItem;
    private int _firstClickPointerId;

    public ItemDefinition Item { get; private set; }
    public int InventoryIndex { get; private set; } = -1;
    public Button Button => _button;

    public event Action<InventoryItemSlotView> Selected;
    public event Action<InventoryItemSlotView> Interacted;

    private void Awake()
    {
        if (!_button)
        {
            TryGetComponent(out _button);
        }

        if (_button)
        {
            // 재대여마다 UnityEvent 리스너를 만들지 않고 인스턴스 수명 동안 한 번 연결한다.
            _button.onClick.AddListener(HandleClick);
        }
    }

    private void OnDestroy()
    {
        if (_button)
        {
            _button.onClick.RemoveListener(HandleClick);
        }
    }

    private void OnDisable()
    {
        ResetDoubleClick();
        SetSelected(false);
        SetEquipped(false);
        SetQuickslotNumber(0);
    }

    protected override void OnPoolSpawned()
    {
        Bind(null);
    }

    protected override void OnPoolDespawned()
    {
        if (EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // 다른 타입의 그리드가 재대여해도 이전 데이터와 구독이 따라가지 않도록 초기화한다.
        Bind(null);
        Selected = null;
        Interacted = null;
    }

    public void Bind(ItemDefinition item, int inventoryIndex = -1)
    {
        // 재대여나 목록 갱신 사이의 클릭이 새 아이템의 더블클릭으로 이어지지 않게 한다.
        ResetDoubleClick();
        // 표시할 공유 데이터만 보관하며 보유 수량이나 장착 상태를 SO에 기록하지 않는다.
        Item = item;
        InventoryIndex = item ? inventoryIndex : -1;
        SetSelected(false);
        SetEquipped(false);
        SetQuickslotNumber(0);

        if (_button)
        {
            _button.interactable = item;

            if (!item)
            {
                // 풀에서 재사용할 때 이전 그리드의 이웃이나 탭 버튼 참조를 남기지 않는다.
                SetNavigation(null, null, null, null);
            }
        }

        if (_icon)
        {
            _icon.sprite = item ? item.ItemIcon : null;
            _icon.enabled = _icon.sprite;
        }

        RefreshEquipmentTypeVisual(item);
    }

    private void RefreshEquipmentTypeVisual(ItemDefinition item)
    {
        if (!_equipmentTypeVisual)
        {
            return;
        }

        // 풀에서 다른 아이템으로 재사용될 때 이전 장비 부위 아이콘을 남기지 않는다.
        Sprite equipmentTypeSprite = null;
        if (item && item.ItemType == ItemType.Equipment)
        {
            equipmentTypeSprite = item.EquipmentSlot switch
            {
                EquipmentSlot.Head => _headSprite,
                EquipmentSlot.UpperBody => _upperSprite,
                EquipmentSlot.LowerBody => _lowerSprite,
                EquipmentSlot.Gloves => _glovesSprite,
                EquipmentSlot.Shoes => _shoesSprite,
                _ => null
            };
        }

        bool showVisual = equipmentTypeSprite;
        _equipmentTypeVisual.sprite = equipmentTypeSprite;
        _equipmentTypeVisual.enabled = showVisual;
        if (_equipmentTypeVisual.gameObject.activeSelf != showVisual)
        {
            _equipmentTypeVisual.gameObject.SetActive(showVisual);
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (_selectedVisual && _selectedVisual.activeSelf != isSelected)
        {
            _selectedVisual.SetActive(isSelected);
        }
    }

    public void SetEquipped(bool isEquipped)
    {
        // 풀 재사용 시 다른 타입에 장착 표시가 남지 않도록 장비 항목에만 표시한다.
        bool showEquipped = isEquipped && Item && Item.ItemType == ItemType.Equipment;
        if (_equippedVisual && _equippedVisual.activeSelf != showEquipped)
        {
            _equippedVisual.SetActive(showEquipped);
        }
    }

    public void SetQuickslotNumber(int slotNumber)
    {
        Sprite sprite = Item && Item.ItemType == ItemType.Consumable ? slotNumber switch
        {
            1 => _quickSlot01Sprite,
            2 => _quickSlot02Sprite,
            3 => _quickSlot03Sprite,
            4 => _quickSlot04Sprite,
            _ => null
        } : null;

        // 번호 아이콘은 기존 Image의 스프라이트만 바꾸고 배경을 포함한 부모 전체의 표시를 제어한다.
        if (_quickSlotVisual) _quickSlotVisual.sprite = sprite;
        bool showVisual = _quickSlotVisual && sprite;

        if (_quickSlotVisualParent && _quickSlotVisualParent.activeSelf != showVisual)
        {
            _quickSlotVisualParent.SetActive(showVisual);
        }
    }

    public void SetNavigation(Selectable up, Selectable down, Selectable left, Selectable right)
    {
        if (!_button)
        {
            return;
        }

        // 자동 거리 탐색을 끄고 그리드가 지정한 방향별 대상만 선택한다.
        _button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down,
            selectOnLeft = left, selectOnRight = right };
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanInteract()) return;

        // 호버한 슬롯과 Submit을 받을 실제 UI 포커스를 일치시킨다.
        _button.Select();
        NotifySelected();
    }

    public void OnSelect(BaseEventData eventData)
    {
        // 마우스와 키보드·패드 탐색이 같은 아이템 설명 경로를 사용한다.
        NotifySelected();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left || !CanInteract())
        {
            ResetDoubleClick();
            return;
        }

        if (eventData.clickCount == 1)
        {
            _firstClickItem = Item;
            _firstClickPointerId = eventData.pointerId;
            return;
        }

        // 같은 포인터로 같은 아이템을 두 번 클릭했을 때만 한 번 실행한다.
        bool isDoubleClick = eventData.clickCount == 2 && _firstClickItem == Item && _firstClickPointerId == eventData.pointerId;
        ResetDoubleClick();

        if (isDoubleClick)
        {
            Interact();
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        ResetDoubleClick();
        EventSystem eventSystem = EventSystem.current;

        // 호버·클릭·Navigation으로 실제 UI 포커스를 받은 슬롯에서만 처리한다.
        if (!eventSystem || !_button || eventSystem.currentSelectedGameObject != _button.gameObject)
        {
            return;
        }

        Interact();
    }

    private void HandleClick()
    {
        // Button.onClick은 단일 클릭과 Submit에서도 호출되므로 선택만 처리한다.
        NotifySelected();
    }

    private bool CanInteract()
    {
        return isActiveAndEnabled && Item && _button && _button.IsActive() && _button.IsInteractable();
    }

    private void Interact()
    {
        if (!CanInteract())
        {
            return;
        }

        ItemDefinition item = Item;
        int inventoryIndex = InventoryIndex;
        NotifySelected();

        // 선택 콜백이 슬롯을 반환하거나 다시 바인딩했다면 이전 입력을 취소한다.
        if (!CanInteract() || Item != item || InventoryIndex != inventoryIndex)
        {
            return;
        }

        // 장착과 사용은 소유 그리드와 InventoryView를 통해 PlayerManager에 전달한다.
        switch (item.ItemType)
        {
            case ItemType.Equipment:
            case ItemType.Consumable:
                Interacted?.Invoke(this);
                break;
        }
    }

    private void ResetDoubleClick()
    {
        _firstClickItem = null;
        _firstClickPointerId = 0;
    }

    private void NotifySelected()
    {
        if (CanInteract())
        {
            Selected?.Invoke(this);
        }
    }
}
