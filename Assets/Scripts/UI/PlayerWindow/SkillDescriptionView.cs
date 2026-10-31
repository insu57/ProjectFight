using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SkillDescriptionView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descriptionTxt;
    [SerializeField] private TMP_Text _unlockCostTxt;
    [SerializeField] private Color _enoughPointsColor = Color.green;
    [SerializeField] private Color _notEnoughPointsColor = Color.red;

    // 노드 선택이 바뀔 때 설명과 숫자 버퍼를 재사용하여 결합 문자열 할당을 피한다.
    private readonly StringBuilder _descriptionBuilder = new(256);
    private readonly char[] _descriptionNumberBuffer = new char[64];
    private SkillTreeNodeRuntime _node;

    private void Awake()
    {
        Clear();
    }

    public void Show(SkillTreeNodeRuntime node, int availablePoints, LethalArtDefinition lethalArtDefinition = null)
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
            _nameText.SetText(definition.NodeName);
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
