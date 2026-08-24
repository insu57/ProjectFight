using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SkillTreeConnectionView : MonoBehaviour
{
    [SerializeField] private Image _lineImage;
    [SerializeField] private Color _lockedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color _availableColor = new Color(0.8f, 0.65f, 0.2f, 1f);
    [SerializeField] private Color _unlockedColor = Color.white;
    [SerializeField, Min(1f)] private float _lineThickness = 6f;

    private RectTransform _rectTransform;
    private SkillTreeNodeRuntime _fromNode;
    private SkillTreeNodeRuntime _toNode;

    private void Awake()
    {
        TryGetComponent(out _rectTransform);
    }

    public void Bind(SkillTreeNodeRuntime fromNode, SkillTreeNodeRuntime toNode)
    {
        _fromNode = fromNode;
        _toNode = toNode;
    }

    public void SetPositions(Vector2 startPosition, Vector2 endPosition)
    {
        Vector2 direction = endPosition - startPosition;
        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        _rectTransform.anchorMin = new Vector2(0f, 1f);
        _rectTransform.anchorMax = new Vector2(0f, 1f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.anchoredPosition = (startPosition + endPosition) * 0.5f;
        _rectTransform.sizeDelta = new Vector2(distance, _lineThickness);
        _rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void Refresh()
    {
        if (!_lineImage || _fromNode == null || _toNode == null)
        {
            return;
        }

        if (_toNode.IsUnlocked)
        {
            _lineImage.color = _unlockedColor;
            return;
        }

        _lineImage.color = _fromNode.IsUnlocked ? _availableColor : _lockedColor;
    }
}
