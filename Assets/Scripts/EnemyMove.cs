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

    public void MoveStationary(float deltaTime)
    {
        ApplyGravity(deltaTime);
        _characterController.Move(Vector3.up * (_verticalVelocity * deltaTime));
        MoveAmount = 0f;
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
