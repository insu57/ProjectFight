using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ItemDescriptionView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _rarityText;
    [SerializeField] private TMP_Text _descriptionText;

    public void Show(ItemDefinition item)
    {
        if (!item)
        {
            Clear();
            return;
        }

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
            // 고정 문구를 사용하여 설명을 갱신할 때 enum 문자열을 새로 만들지 않는다.
            _rarityText.SetText(GetRarityText(item.Rarity));
        }

        if (_descriptionText)
        {
            _descriptionText.SetText(item.ItemDescription);
        }
    }

    public void Clear()
    {
        if (_icon)
        {
            _icon.sprite = null;
            _icon.enabled = false;
        }

        if (_nameText)
        {
            _nameText.SetText(string.Empty);
        }

        if (_rarityText)
        {
            _rarityText.SetText(string.Empty);
        }

        if (_descriptionText)
        {
            _descriptionText.SetText(string.Empty);
        }
    }

    private static string GetRarityText(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => "일반",
            ItemRarity.Uncommon => "고급",
            ItemRarity.Rare => "희귀",
            ItemRarity.Epic => "영웅",
            ItemRarity.Legendary => "전설",
            _ => string.Empty
        };
    }
}
