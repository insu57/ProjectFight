using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EquipmentPanelView : MonoBehaviour
{
    [Serializable]
    public sealed class EquipmentSlotBinding
    {
        [SerializeField] private EquipmentSlot _equipmentSlot;
        [SerializeField] private Image _icon;

        private Sprite _emptySprite;
        private bool _emptyIconEnabled;
        private bool _hasCachedAppearance;

        public EquipmentSlot EquipmentSlot => _equipmentSlot;

        public void Refresh(ItemDefinition item)
        {
            if (!_icon) return;

            if (!_hasCachedAppearance)
            {
                // 장착 해제 시 Inspector에서 지정한 부위 아이콘과 표시 상태로 복원한다.
                _emptySprite = _icon.sprite;
                _emptyIconEnabled = _icon.enabled;
                _hasCachedAppearance = true;
            }

            bool hasItemIcon = item && item.ItemIcon;
            Sprite sprite = hasItemIcon ? item.ItemIcon : _emptySprite;
            if (_icon.sprite != sprite)
            {
                _icon.sprite = sprite;
            }

            _icon.enabled = hasItemIcon || _emptyIconEnabled;
        }
    }

    [Header("부위별 장착 아이콘")]
    [SerializeField] private EquipmentSlotBinding[] _slotBindings = Array.Empty<EquipmentSlotBinding>();

    [Header("선택한 장비의 상호작용 안내")]
    [SerializeField, Tooltip("EquipmentPanel의 Key처럼 키 아이콘과 문구를 함께 포함한 자식 루트입니다.")]
    private GameObject _interactionRoot;
    [SerializeField] private TMP_Text _interactionText;

    public void Refresh(PlayerManager player, ItemDefinition selectedItem, int inventoryIndex)
    {
        // 장착 데이터와 선택은 InventoryView가 전달하므로 패널에서 플레이어를 찾거나 입력을 처리하지 않는다.
        for (int i = 0; i < (_slotBindings?.Length ?? 0); i++)
        {
            EquipmentSlotBinding binding = _slotBindings[i];
            if (binding != null)
            {
                binding.Refresh(player ? player.GetEquippedItem(binding.EquipmentSlot) : null);
            }
        }

        bool canInteract = player && player.CanToggleEquipment(inventoryIndex, selectedItem);
        if (_interactionText)
        {
            string actionText = canInteract ? (player.IsEquipmentEquipped(inventoryIndex, selectedItem) ? "장비해제" : "장비") : string.Empty;
            _interactionText.SetText(actionText);
        }

        // 선택이 없어도 부위별 장착 아이콘은 유지하고 다음 동작을 알리는 Key 영역만 숨긴다.
        if (_interactionRoot && _interactionRoot.activeSelf != canInteract)
        {
            _interactionRoot.SetActive(canInteract);
        }
    }
}
