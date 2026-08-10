using UnityEngine;

[DisallowMultipleComponent]
public sealed class LockOnIndicatorUI : MonoBehaviour
{
    [SerializeField] private PlayerManager _playerManager;
    [SerializeField] private RectTransform _indicator;
    private Camera _viewCamera;

    private RectTransform _canvasRect;
    private LockOnTarget _target;

    private void Awake()
    {
        _canvasRect = transform as RectTransform;

        _viewCamera = Camera.main;

        SetIndicatorVisible(false);
    }

    private void OnEnable()
    {
        if (_playerManager)
        {
            _playerManager.LockOnTargetChanged += SetTarget;
        }
    }

    private void OnDisable()
    {
        if (_playerManager)
        {
            _playerManager.LockOnTargetChanged -= SetTarget;
        }

        _target = null;
        SetIndicatorVisible(false);
    }

    private void LateUpdate()
    {
        if (!_target || !_viewCamera || !_indicator)
        {
            SetIndicatorVisible(false);
            return;
        }

        Vector3 screenPosition = _viewCamera.WorldToScreenPoint(_target.AimPosition);

        if (screenPosition.z <= 0f)
        {
            SetIndicatorVisible(false);
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPosition, null, out Vector2 anchoredPosition))
        {
            SetIndicatorVisible(false);
            return;
        }

        _indicator.anchoredPosition = anchoredPosition;
        SetIndicatorVisible(true);
    }

    public void SetTarget(LockOnTarget target)
    {
        _target = target;
        SetIndicatorVisible(_target);
    }

    private void SetIndicatorVisible(bool isVisible)
    {
        if (_indicator && _indicator.gameObject.activeSelf != isVisible)
        {
            _indicator.gameObject.SetActive(isVisible);
        }
    }
}
