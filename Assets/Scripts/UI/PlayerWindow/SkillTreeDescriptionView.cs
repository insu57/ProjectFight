using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SkillTreeDescriptionView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descriptionTxt;
    [SerializeField] private TMP_Text _unlockCostTxt;
    [SerializeField] private Color _enoughPointsColor = Color.green;
    [SerializeField] private Color _notEnoughPointsColor = Color.red;

    // 노드 선택이 바뀔 때 이름·설명용 버퍼와 숫자 버퍼를 재사용하여 결합 문자열 할당을 피한다.
    private readonly StringBuilder _descriptionBuilder = new(256);
    private readonly char[] _descriptionNumberBuffer = new char[64];
    private SkillTreeNodeRuntime _node;

    private void Awake()
    {
        Clear();
    }

    public void Show(SkillTreeNodeRuntime node, int availablePoints, LethalArtDefinition lethalArtDefinition = null, ComboDefinition comboDefinition = null)
    {
        if (node == null)
        {
            Clear();
            return;
        }

        _node = node;
        SkillNodeDefinition definition = node.Definition;

        if (_icon)
        {
            _icon.sprite = definition.NodeIcon;
            _icon.enabled = definition.NodeIcon;
        }

        if (_nameText)
        {
            if (definition.EffectType == SkillTreeEffectType.LethalArtUnlock && lethalArtDefinition && !string.IsNullOrWhiteSpace(lethalArtDefinition.LethalArtName))
            {
                // 제목을 먼저 복사한 뒤 같은 버퍼를 본문에 재사용하여 별도 결합 문자열을 만들지 않는다.
                _descriptionBuilder.Clear();
                _descriptionBuilder.Append(definition.NodeName).Append(" : ").Append(lethalArtDefinition.LethalArtName);
                _nameText.SetText(_descriptionBuilder);
            }
            else
            {
                _nameText.SetText(definition.NodeName);
            }
        }

        if (_descriptionTxt)
        {
            _descriptionBuilder.Clear();
            if (definition.EffectType == SkillTreeEffectType.StatModify)
            {
                // 스탯 이름·부호·비율·단위는 아이템 설명과 같은 포매터를 사용한다.
                ItemDisplayUtility.AppendStatModifiersDescription(_descriptionBuilder, definition.StatModifiers, _descriptionNumberBuffer);
            }
            else if (definition.EffectType == SkillTreeEffectType.LethalArtUnlock)
            {
                LethalArtDisplayUtility.AppendUnlockDescription(_descriptionBuilder, lethalArtDefinition, _descriptionNumberBuffer);
            }
            else if (definition.EffectType == SkillTreeEffectType.ComboUnlock)
            {
                AppendComboInputDescription(comboDefinition);
            }

            if (_descriptionBuilder.Length > 0)
            {
                _descriptionBuilder.Insert(0, "<color=#FFE082>");
                _descriptionBuilder.Append("</color>");
            }

            if (!string.IsNullOrWhiteSpace(definition.NodeDescription))
            {
                // 효과 뒤에 빈 줄을 두고 SO의 일반 설명은 기존 아이템과 같은 기호로 구분한다.
                if (_descriptionBuilder.Length > 0) _descriptionBuilder.Append("\n\n");
                _descriptionBuilder.Append("• ").Append(definition.NodeDescription);
            }

            if (!_descriptionTxt.richText) _descriptionTxt.richText = true;
            _descriptionTxt.SetText(_descriptionBuilder);
        }

        RefreshUnlockCost(availablePoints);
    }

    public void Refresh(int availablePoints)
    {
        if (_node != null)
        {
            RefreshUnlockCost(availablePoints);
        }
    }

    private void AppendComboInputDescription(ComboDefinition definition)
    {
        if (!definition || definition.ComboSteps == null || definition.ComboSteps.Length == 0)
        {
            _descriptionBuilder.Append("콤보 정보를 찾을 수 없습니다.");
            return;
        }

        // 콤보 이름 대신 실제 단계의 입력 순서를 표시하며 기존 설명 버퍼를 재사용한다.
        _descriptionBuilder.Append("공격 입력: ");
        ComboStep[] steps = definition.ComboSteps;
        for (int i = 0; i < steps.Length; i++)
        {
            if (i > 0) _descriptionBuilder.Append(" → ");
            string inputName = steps[i].AttackInputType switch
            {
                AttackInputType.Light => "약공격",
                AttackInputType.Heavy => "강공격",
                AttackInputType.Grab => "잡기",
                _ => "알 수 없는 입력"
            };
            _descriptionBuilder.Append(inputName);
        }
    }

    private void RefreshUnlockCost(int availablePoints)
    {
        if (!_unlockCostTxt)
        {
            return;
        }

        bool shouldShow = _node != null && !_node.IsStartNode && !_node.IsUnlocked;
        SetActiveIfChanged(_unlockCostTxt.gameObject, shouldShow);

        if (!shouldShow)
        {
            return;
        }

        int requiredPoints = _node.Definition.RequiredPoints;

        _unlockCostTxt.SetText("Unlock Cost: {0}", requiredPoints);
        _unlockCostTxt.color = availablePoints >= requiredPoints ? _enoughPointsColor : _notEnoughPointsColor;
    }

    private void Clear()
    {
        _node = null;
        _descriptionBuilder.Clear();

        if (_icon)
        {
            _icon.sprite = null;
            _icon.enabled = false;
        }

        if (_nameText)
        {
            _nameText.SetText(string.Empty);
        }

        if (_descriptionTxt)
        {
            _descriptionTxt.SetText(string.Empty);
        }

        if (_unlockCostTxt)
        {
            _unlockCostTxt.SetText(string.Empty);
            SetActiveIfChanged(_unlockCostTxt.gameObject, false);
        }
    }

    private static void SetActiveIfChanged(GameObject target, bool isActive)
    {
        if (target && target.activeSelf != isActive)
        {
            target.SetActive(isActive);
        }
    }
}
