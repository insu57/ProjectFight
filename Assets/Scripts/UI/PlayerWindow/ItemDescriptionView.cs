using System.Text;
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
    [SerializeField, Tooltip("아이콘과 이름은 고정하고 설명 텍스트만 움직이는 세로 스크롤입니다.")] private ScrollRect _descriptionScrollRect;

    // 선택 변경과 수량 갱신 시 버퍼를 재사용해 효과·설명 결합 문자열을 별도로 만들지 않는다.
    private readonly StringBuilder _descriptionBuilder = new(256);
    private readonly char[] _descriptionNumberBuffer = new char[64];
    private ItemDefinition _shownItem;

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

        // 같은 아이템의 수량 갱신은 읽던 위치를 유지하고, 선택이 바뀔 때만 초기화한다.
        bool itemChanged = _shownItem != item;
        _shownItem = item;

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
            ItemDisplayUtility.BuildItemDescription(_descriptionBuilder, item, _descriptionNumberBuffer);
            if (!_descriptionText.richText) _descriptionText.richText = true;
            _descriptionText.SetText(_descriptionBuilder);
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

        if (itemChanged) ResetDescriptionScroll();
    }

    public void Hide()
    {
        _shownItem = null;
        if (_descriptionScrollRect) _descriptionScrollRect.StopMovement();

        // 선택 아이템의 내용만 숨기고 타입별 정보 패널은 현재 탭에 맞춰 유지한다.
        if (_contentRoot && _contentRoot.activeSelf)
        {
            _contentRoot.SetActive(false);
        }
    }

    private void ResetDescriptionScroll()
    {
        if (!_descriptionScrollRect) return;

        _descriptionScrollRect.StopMovement();
        // 새 설명의 높이를 먼저 반영해야 긴 설명에서 짧은 설명으로 바뀌어도 상단이 맞는다.
        if (_descriptionScrollRect.content) LayoutRebuilder.ForceRebuildLayoutImmediate(_descriptionScrollRect.content);
        _descriptionScrollRect.verticalNormalizedPosition = 1f;
    }
}
