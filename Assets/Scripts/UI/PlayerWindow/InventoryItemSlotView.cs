using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class InventoryItemSlotView : PoolableObject, IPointerEnterHandler, ISelectHandler
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    //[SerializeField] private TMP_Text _nameText;
    [SerializeField] private GameObject _selectedVisual;

    public ItemDefinition Item { get; private set; }
    public Button Button => _button;

    public event Action<InventoryItemSlotView> Selected;

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
        SetSelected(false);
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
    }

    public void Bind(ItemDefinition item)
    {
        // 표시할 공유 데이터만 보관하며 보유 수량이나 장착 상태를 SO에 기록하지 않는다.
        Item = item;
        SetSelected(false);

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

        /*if (_nameText)
        {
            _nameText.SetText(item ? (string.IsNullOrEmpty(item.ItemName) ? item.name : item.ItemName) : string.Empty);
        }*/
    }

    public void SetSelected(bool isSelected)
    {
        if (_selectedVisual && _selectedVisual.activeSelf != isSelected)
        {
            _selectedVisual.SetActive(isSelected);
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
        NotifySelected();
    }

    public void OnSelect(BaseEventData eventData)
    {
        // 마우스와 키보드·패드 탐색이 같은 아이템 설명 경로를 사용한다.
        NotifySelected();
    }

    private void HandleClick()
    {
        NotifySelected();
    }

    private void NotifySelected()
    {
        if (isActiveAndEnabled && Item && _button && _button.interactable)
        {
            Selected?.Invoke(this);
        }
    }
}
