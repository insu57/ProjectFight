using System;
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

    private SkillTreeNodeRuntime _node;

    private void Awake()
    {
        Clear();
    }

    public void Show(SkillTreeNodeRuntime node, int availablePoints)
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
            _descriptionTxt.SetText(definition.NodeDescription);
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
