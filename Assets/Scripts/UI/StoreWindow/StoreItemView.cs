using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class StoreItemView : MonoBehaviour
{
    [SerializeField] private Button _button;
    [Tooltip("아이콘·테두리·텍스트·가격을 묶은 표시용 자식 부모를 연결한다.")]
    [SerializeField] private GameObject _contentRoot;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _rarityOutline;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _typeText;
    [SerializeField] private TMP_Text _rarityText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text _itemPriceTxt;

    private UnityAction _clickHandler;

    // 상품을 다시 표시할 때 효과·설명과 숫자 포맷 버퍼를 재사용한다.
    private readonly StringBuilder _descriptionBuilder = new(256);
    private readonly char[] _descriptionNumberBuffer = new char[64];

    public ItemDefinition Item { get; private set; }
    public Button Button => _button;
    public event Action<StoreItemView> PurchaseRequested;

    private void Awake()
    {
        if (!_button)
        {
            TryGetComponent(out _button);
        }
    }

    private void OnEnable()
    {
        if (!_button) return;

        // 마우스 클릭과 UI.Submit은 같은 Button 이벤트로 전달하며 콜백을 재사용한다.
        _clickHandler ??= HandleClick;
        _button.onClick.AddListener(_clickHandler);
    }

    private void OnDisable()
    {
        if (_button && _clickHandler != null)
        {
            _button.onClick.RemoveListener(_clickHandler);
        }
    }

    private void HandleClick()
    {
        if (!isActiveAndEnabled || !Item || !_button || !_button.isActiveAndEnabled || !_button.IsInteractable()) return;

        // 뷰는 플레이어를 직접 찾지 않고 자신의 구매 요청을 상점 컨트롤러에 전달한다.
        PurchaseRequested?.Invoke(this);
    }

    public void Bind(ItemDefinition item)
    {
        // 사전 배치한 UI를 재사용하며 공유 SO의 내용만 읽어 표시한다.
        Item = item;

        if (_button)
        {
            _button.interactable = item;
        }

        if (!item && _contentRoot)
        {
            // 상품 루트와 고정 칸은 유지하고 표시용 자식만 숨겨 빈 상태로 처리한다.
            _contentRoot.SetActive(false);
            return;
        }

        if (_icon)
        {
            _icon.sprite = item ? item.ItemIcon : null;
            _icon.enabled = _icon.sprite;
        }

        if (_rarityOutline)
        {
            _rarityOutline.color = item ? ItemDisplayUtility.GetRarityColor(item.Rarity) : Color.white;
        }

        if (_nameText)
        {
            _nameText.SetText(item ? (string.IsNullOrEmpty(item.ItemName) ? item.name : item.ItemName) : string.Empty);
        }

        if (_typeText)
        {
            _typeText.SetText(item ? ItemDisplayUtility.GetItemTypeText(item.ItemType) : string.Empty);
        }

        if (_rarityText)
        {
            _rarityText.SetText(item ? ItemDisplayUtility.GetRarityText(item.Rarity) : string.Empty);
        }

        if (_descriptionText)
        {
            ItemDisplayUtility.BuildItemDescription(_descriptionBuilder, item, _descriptionNumberBuffer);
            if (!_descriptionText.richText) _descriptionText.richText = true;
            _descriptionText.SetText(_descriptionBuilder);
        }

        if (_itemPriceTxt)
        {
            if (item)
            {
                _itemPriceTxt.SetText("{0}", item.BasePrice);
            }
            else
            {
                // 창을 닫거나 상품이 없을 때 가격 참조도 안전하게 비운다.
                _itemPriceTxt.SetText(string.Empty);
            }
        }

        // 새 상품의 내용을 모두 갱신한 다음 표시하여 이전 상품 내용이 다시 보이지 않게 한다.
        // 부모가 연결되지 않은 기존 뷰는 위의 개별 표시 비우기 처리를 그대로 사용한다.
        if (_contentRoot)
        {
            _contentRoot.SetActive(true);
        }
    }
}
