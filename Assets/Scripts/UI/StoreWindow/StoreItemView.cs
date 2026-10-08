using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class StoreItemView : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _rarityOutline;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _typeText;
    [SerializeField] private TMP_Text _rarityText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text _itemPriceTxt;

    public ItemDefinition Item { get; private set; }
    public Button Button => _button;

    private void Awake()
    {
        if (!_button)
        {
            TryGetComponent(out _button);
        }
    }

    public void Bind(ItemDefinition item)
    {
        // 사전 배치한 UI를 재사용하며 공유 SO의 내용만 읽어 표시한다.
        Item = item;

        if (_button)
        {
            _button.interactable = item;
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
            _descriptionText.SetText(item ? item.ItemDescription : string.Empty);
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
    }
}
