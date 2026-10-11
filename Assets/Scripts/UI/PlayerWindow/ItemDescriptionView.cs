using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ItemDescriptionView : MonoBehaviour
{
    [SerializeField, Tooltip("상세 표시 요소를 묶은 자식 루트입니다. ItemDescriptionView는 이 루트 바깥에 둡니다.")] private GameObject _contentRoot;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _border;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _rarityText;
    [SerializeField] private TMP_Text _descriptionText;

    [Header("타입 라벨")]
    [SerializeField, Tooltip("아이템 타입 라벨의 배경과 텍스트를 포함하는 루트입니다.")]
    private GameObject _itemTypeLabel;
    [SerializeField] private TMP_Text _itemTypeText;

    [SerializeField, Tooltip("장비 타입 라벨의 배경과 텍스트를 포함하는 루트입니다.")]
    private GameObject _equipmentTypeLabel;
    [SerializeField] private TMP_Text _equipmentTypeText;

    [Header("소모품 수량")]
    [SerializeField, Tooltip("Content Root 아래에서 배경과 수량 텍스트를 포함하는 라벨 루트입니다.")] private GameObject _consumableCountLabel;
    [SerializeField] private TMP_Text _consumableCountText;

    [Header("타입별 정보 패널")]
    [SerializeField, Tooltip("선택 아이템의 Content Root와 분리된 장비 정보 패널 루트입니다.")] private GameObject _equipmentInfoPanel;
    [SerializeField, Tooltip("선택 아이템의 Content Root와 분리된 소모품 정보 패널 루트입니다.")] private GameObject _consumableInfoPanel;

    public void ShowTypePanel(ItemType itemType)
    {
        // 아이템 선택 여부와 관계없이 현재 탭의 패널만 표시하며 유물은 둘 다 숨긴다.
        bool isEquipment = itemType == ItemType.Equipment;
        if (_equipmentInfoPanel && _equipmentInfoPanel.activeSelf != isEquipment)
        {
            _equipmentInfoPanel.SetActive(isEquipment);
        }

        bool isConsumable = itemType == ItemType.Consumable;
        if (_consumableInfoPanel && _consumableInfoPanel.activeSelf != isConsumable)
        {
            _consumableInfoPanel.SetActive(isConsumable);
        }
    }

    public void Show(ItemDefinition item, int itemCount)
    {
        if (!item)
        {
            Hide();
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

        // 테두리와 희귀도 문구는 공통 유틸리티의 같은 등급 색상을 사용한다.
        Color rarityColor = ItemDisplayUtility.GetRarityColor(item.Rarity);
        if (_border)
        {
            _border.color = rarityColor;
        }

        if (_rarityText)
        {
            _rarityText.SetText(ItemDisplayUtility.GetRarityText(item.Rarity));
            _rarityText.color = rarityColor;
        }

        if (_itemTypeText)
        {
            _itemTypeText.SetText(ItemDisplayUtility.GetItemTypeText(item.ItemType));
        }

        if (_itemTypeLabel && !_itemTypeLabel.activeSelf)
        {
            _itemTypeLabel.SetActive(true);
        }

        bool isEquipment = item.ItemType == ItemType.Equipment;
        if (_equipmentTypeText)
        {
            _equipmentTypeText.SetText(isEquipment ? ItemDisplayUtility.GetEquipmentSlotText(item.EquipmentSlot) : string.Empty);
        }

        // 장비가 아니면 텍스트뿐 아니라 라벨 배경까지 함께 숨긴다.
        if (_equipmentTypeLabel && _equipmentTypeLabel.activeSelf != isEquipment)
        {
            _equipmentTypeLabel.SetActive(isEquipment);
        }

        if (_descriptionText)
        {
            _descriptionText.SetText(item.ItemDescription);
        }

        bool isConsumable = item.ItemType == ItemType.Consumable;
        if (_consumableCountText)
        {
            if (isConsumable)
            {
                // TMP의 숫자 포맷으로 보유 개수를 표시하여 문자열 생성 할당을 피한다.
                _consumableCountText.SetText("{0}개", Mathf.Max(0, itemCount));
            }
            else
            {
                _consumableCountText.SetText(string.Empty);
            }
        }

        // 장비와 유물은 수량 텍스트뿐 아니라 배경까지 함께 숨긴다.
        if (_consumableCountLabel && _consumableCountLabel.activeSelf != isConsumable)
        {
            _consumableCountLabel.SetActive(isConsumable);
        }

        // 모든 표시 값을 갱신한 뒤 열어 이전 아이템 정보가 노출되지 않도록 한다.
        if (_contentRoot && !_contentRoot.activeSelf)
        {
            _contentRoot.SetActive(true);
        }
    }

    public void Hide()
    {
        // 선택 아이템의 내용만 숨기고 타입별 정보 패널은 현재 탭에 맞춰 유지한다.
        if (_contentRoot && _contentRoot.activeSelf)
        {
            _contentRoot.SetActive(false);
        }
    }
}
