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

    [Header("Lock On")]
    [SerializeField, Min(0f)] private float _lockOnRotationSpeed = 720f;
    [SerializeField, Range(-30f, 30f)] private float _lockOnPitchOffset = 10f;

    private Vector2 _lookInput;
    private Vector3 _lockOnTargetPosition;
    private float _yaw;
    private float _pitch;
    private bool _isLookInputDelta;
    private bool _canRotate = true;
    private bool _hasLockOnTarget;

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

    public void SetLockOnTarget(Vector3 targetPosition)
    {
        _lockOnTargetPosition = targetPosition;
        _hasLockOnTarget = true;
    }

    public void ClearLockOnTarget()
    {
        _hasLockOnTarget = false;
    }

    private void UpdateRotation()
    {
        if (_canRotate)
        {
            if (_hasLockOnTarget)
            {
                UpdateLockOnRotation();
            }
            else
            {
                UpdateFreeRotation();
            }
        }

        // 월드 회전을 사용해 카메라 회전이 플레이어 본체의 회전에 영향을 주지 않도록 한다.
        _cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private void UpdateFreeRotation()
    {
        float rotationScale = _isLookInputDelta ? _pointerSensitivity : _stickRotationSpeed * Time.deltaTime;
        _yaw = Mathf.Repeat(_yaw + _lookInput.x * rotationScale, 360f);
        _pitch = Mathf.Clamp(_pitch - _lookInput.y * rotationScale, _minimumPitch, _maximumPitch);
    }

    private void UpdateLockOnRotation()
    {
        Vector3 targetDirection = _lockOnTargetPosition - _cameraTarget.position;

        if (targetDirection.sqrMagnitude <= 0f) return;

        float horizontalDistance = Mathf.Sqrt(targetDirection.x * targetDirection.x + targetDirection.z * targetDirection.z);
        float targetYaw = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;
        float targetPitch = -Mathf.Atan2(targetDirection.y, horizontalDistance) * Mathf.Rad2Deg;
        targetPitch += _lockOnPitchOffset;

        // 락온 전환 시 카메라가 순간적으로 꺾이지 않도록 타깃 방향으로 회전시킨다.
        _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, _lockOnRotationSpeed * Time.deltaTime);
        _pitch = Mathf.MoveTowards(_pitch, Mathf.Clamp(targetPitch, _minimumPitch, _maximumPitch), _lockOnRotationSpeed * Time.deltaTime);
    }
}
