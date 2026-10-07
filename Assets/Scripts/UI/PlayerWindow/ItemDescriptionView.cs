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
            _rarityText.SetText(ItemDisplayUtility.GetRarityText(item.Rarity));
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
}
