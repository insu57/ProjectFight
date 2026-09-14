using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class EnemyMove : MonoBehaviour
{
    [Header("이동")]
    [SerializeField, Min(0f)] private float _moveSpeed = 3.5f;
    [SerializeField, Min(0f)] private float _rotationSpeed = 540f;

    [Header("중력")]
    [SerializeField] private float _gravity = -25f;
    [SerializeField] private float _groundedVerticalVelocity = -2f;

    private CharacterController _characterController;
    private float _verticalVelocity;

    public float MoveAmount { get; private set; }

    private void Awake()
    {
        TryGetComponent(out _characterController);
    }

    public void MoveTowards(Vector3 targetPosition, float deltaTime)
    {
        Vector3 moveDirection = targetPosition - transform.position;
        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            MoveStationary(deltaTime);
            return;
        }

        moveDirection.Normalize();
        RotateTowards(moveDirection, deltaTime);
        ApplyGravity(deltaTime);

        Vector3 displacement = moveDirection * (_moveSpeed * deltaTime);
        displacement.y = _verticalVelocity * deltaTime;
        _characterController.Move(displacement);
        MoveAmount = 1f;
    }

    public void MoveAttack(Vector2 localDisplacement, float deltaTime)
    {
        Vector3 displacement = transform.right * localDisplacement.x + transform.forward * localDisplacement.y;
        ApplyGravity(deltaTime);
        displacement.y = _verticalVelocity * deltaTime;
        _characterController.Move(displacement);
        MoveAmount = 0f;
    }

    public void MovePoiseReaction(Vector3 worldDisplacement, float deltaTime)
    {
        if (deltaTime <= 0f) return;

        // 피격 이동을 걷기 애니메이션의 이동 입력으로 사용하지 않는다.
        MoveAmount = 0f;

        // 수평 이동은 피격 설정을 사용하고 수직 이동은 기존 중력을 유지한다.
        ApplyGravity(deltaTime);
        worldDisplacement.y = _verticalVelocity * deltaTime;
        _characterController.Move(worldDisplacement);
    }

    public void MoveStationary(float deltaTime)
    {
        ApplyGravity(deltaTime);
        _characterController.Move(Vector3.up * (_verticalVelocity * deltaTime));
        MoveAmount = 0f;
    }

    public Vector2 MoveCircleTarget(Vector3 targetPosition, float speed, float desiredDistance, float distanceTolerance,
        float directionSign, float deltaTime)
    {
        if (deltaTime <= 0f) return Vector2.zero;

        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            MoveStationary(deltaTime);
            return Vector2.zero;
        }

        float distance = toTarget.magnitude;
        Vector3 radialDirection = toTarget / distance;
        Vector3 tangentDirection = Vector3.Cross(Vector3.up, radialDirection) * directionSign;

        // 목표 거리 근처에서는 앞뒤 보정을 생략하여 이동 방향의 떨림을 막는다.
        float distanceError = distance - Mathf.Max(0f, desiredDistance);
        float tolerance = Mathf.Max(0f, distanceTolerance);
        float radialAmount = 0f;
        if (Mathf.Abs(distanceError) > tolerance)
        {
            radialAmount = Mathf.Sign(distanceError) * Mathf.Clamp01(Mathf.Abs(distanceError) - tolerance);
        }

        Vector3 moveDirection = Vector3.ClampMagnitude(tangentDirection + radialDirection * radialAmount, 1f);
        FaceTarget(targetPosition, deltaTime);
        ApplyGravity(deltaTime);

        Vector3 previousPosition = transform.position;
        Vector3 displacement = moveDirection * (Mathf.Max(0f, speed) * deltaTime);
        displacement.y = _verticalVelocity * deltaTime;
        _characterController.Move(displacement);

        // 충돌로 막힌 경우를 포함한 실제 이동량을 애니메이션과 막힘 판정에 전달한다.
        Vector3 actualVelocity = (transform.position - previousPosition) / deltaTime;
        actualVelocity.y = 0f;
        Vector3 localVelocity = transform.InverseTransformDirection(actualVelocity);
        float referenceSpeed = Mathf.Max(0.01f, _moveSpeed);
        Vector2 localMovement = new Vector2(localVelocity.x, localVelocity.z) / referenceSpeed;
        MoveAmount = Mathf.Clamp01(localMovement.magnitude);
        return Vector2.ClampMagnitude(localMovement, 1f);
    }

    public void FaceTarget(Vector3 targetPosition, float deltaTime)
    {
        Vector3 targetDirection = targetPosition - transform.position;
        targetDirection.y = 0f;

        if (targetDirection.sqrMagnitude > 0.0001f)
        {
            RotateTowards(targetDirection.normalized, deltaTime);
        }
    }

    private void RotateTowards(Vector3 direction, float deltaTime)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * deltaTime);
    }

    private void ApplyGravity(float deltaTime)
    {
        if (_characterController.isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = _groundedVerticalVelocity;
            return;
        }

        _verticalVelocity += _gravity * deltaTime;
    }
}
