using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float _moveSpeed = 5f;
    [SerializeField, Min(0f)] private float _rotationSpeed = 720f;

    [Header("Gravity")]
    [SerializeField] private float _gravity = -25f;
    [SerializeField] private float _groundedVerticalVelocity = -2f;

    private CharacterController _characterController;
    private float _verticalVelocity;

    public float MoveAmount { get; private set; }

    private void Awake()
    {
        TryGetComponent(out _characterController);
    }

    public void Move(Vector2 moveInput, Quaternion cameraRotation, float deltaTime)
    {
        Vector3 inputDirection = Vector3.ClampMagnitude(new Vector3(moveInput.x, 0f, moveInput.y), 1f);
        Vector3 moveDirection = cameraRotation * inputDirection;

        MoveAmount = inputDirection.magnitude;

        Rotate(moveDirection, deltaTime);
        ApplyGravity(deltaTime);

        moveDirection *= _moveSpeed;
        moveDirection.y = _verticalVelocity;

        _characterController.Move(moveDirection * deltaTime);
    }

    private void Rotate(Vector3 moveDirection, float deltaTime)
    {
        if (moveDirection.sqrMagnitude <= 0f) return;

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
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
