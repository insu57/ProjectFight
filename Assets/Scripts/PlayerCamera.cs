using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCamera : MonoBehaviour
{
    [Header("Camera Target")]
    [SerializeField] private Transform _cameraTarget;

    [Header("Sensitivity")]
    [SerializeField, Min(0f)] private float _pointerSensitivity = 0.1f;
    [SerializeField, Min(0f)] private float _stickRotationSpeed = 180f;

    [Header("Vertical Rotation")]
    [SerializeField, Range(-90f, 0f)] private float _minimumPitch = -40f;
    [SerializeField, Range(0f, 90f)] private float _maximumPitch = 70f;

    private Vector2 _lookInput;
    private float _yaw;
    private float _pitch;
    private bool _isLookInputDelta;
    private bool _canRotate = true;

    public Quaternion YawRotation => Quaternion.Euler(0f, _yaw, 0f);

    private void Awake()
    {
        Vector3 targetEulerAngles = _cameraTarget.eulerAngles;
        _yaw = targetEulerAngles.y;
        _pitch = Mathf.DeltaAngle(0f, targetEulerAngles.x);
    }

    private void LateUpdate()
    {
        UpdateRotation();
    }

    public void SetLookInput(Vector2 lookInput, bool isLookInputDelta)
    {
        _lookInput = lookInput;
        _isLookInputDelta = isLookInputDelta;
    }

    public void SetRotationEnabled(bool canRotate)
    {
        _canRotate = canRotate;
    }

    private void UpdateRotation()
    {
        if (_canRotate)
        {
            float rotationScale = _isLookInputDelta ? _pointerSensitivity : _stickRotationSpeed * Time.deltaTime;
            _yaw = Mathf.Repeat(_yaw + _lookInput.x * rotationScale, 360f);
            _pitch = Mathf.Clamp(_pitch - _lookInput.y * rotationScale, _minimumPitch, _maximumPitch);
        }

        // 월드 회전을 사용해 카메라 회전이 플레이어 본체의 회전에 영향을 주지 않도록 한다.
        _cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }
}
