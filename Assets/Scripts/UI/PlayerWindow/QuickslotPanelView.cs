using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class QuickslotPanelView : MonoBehaviour
{
    [Serializable]
    public sealed class QuickslotSlotBinding
    {
        [SerializeField, Range(1, PlayerQuickslot.SlotCount)] private int _slotNumber = 1;
        [SerializeField] private Image _icon;

        private Sprite _emptySprite;
        private bool _emptyIconEnabled;
        private bool _hasCachedAppearance;

        // 직렬화된 값이 0이어도 조회에는 항상 유효한 1~4번 슬롯 번호를 사용한다.
        public int SlotNumber => Mathf.Clamp(_slotNumber, 1, PlayerQuickslot.SlotCount);

        public void Validate()
        {
            _slotNumber = SlotNumber;
        }

        public void Refresh(ItemDefinition item)
        {
            if (!_icon) return;

            if (!_hasCachedAppearance)
            {
                // 등록 해제 시 Inspector에서 지정한 빈 칸의 스프라이트와 표시 상태로 복원한다.
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

    [Header("퀵슬롯별 등록 아이콘")]
    [SerializeField] private QuickslotSlotBinding[] _slotBindings = Array.Empty<QuickslotSlotBinding>();

    private void OnValidate()
    {
        // Inspector에 저장되는 번호도 보정하며 이미 올바른 2~4번 연결은 그대로 유지한다.
        for (int i = 0; i < (_slotBindings?.Length ?? 0); i++)
        {
            _slotBindings[i]?.Validate();
        }
    }

    public void Refresh(PlayerManager player)
    {
        // 인벤토리와 HUD의 상위 UI가 갱신을 중개하며 현재 선택과 관계없이 등록 상태를 표시한다.
        for (int i = 0; i < (_slotBindings?.Length ?? 0); i++)
        {
            QuickslotSlotBinding binding = _slotBindings[i];
            if (binding != null)
            {
                binding.Refresh(player ? player.GetQuickslotItem(binding.SlotNumber) : null);
            }
        }
    }
}
