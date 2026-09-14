using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerMove : MonoBehaviour
{
    private enum FacingMode
    {
        MoveDirection,
        ReferenceDirection
    }

    [Header("Movement")]
    [SerializeField, Min(0f)] private float _moveSpeed = 5f;
    [SerializeField, Min(0f)] private float _rotationSpeed = 720f;

    [Header("Sprint")]
    [SerializeField, Min(0f)] private float _sprintSpeed = 8f;

    [Header("Dodge")]
    [SerializeField, Min(0f)] private float _dodgeSpeed = 8f;
    [SerializeField, Min(0f)] private float _dodgeDuration = 0.4f;

    [Header("Gravity")]
    [SerializeField] private float _gravity = -25f;
    [SerializeField] private float _groundedVerticalVelocity = -2f;

    private CharacterController _characterController;
    private Vector3 _dodgeWorldDirection;
    private float _verticalVelocity;
    private float _dodgeElapsedTime;

    public float MoveAmount { get; private set; }
    public Vector2 LocalMoveDirection { get; private set; }
    public Vector2 DodgeLocalDirection { get; private set; }

    private void Awake()
    {
        TryGetComponent(out _characterController);
    }

    public void Move(Vector2 moveInput, Quaternion cameraRotation, float deltaTime)
    {
        MoveInternal(moveInput, cameraRotation, FacingMode.MoveDirection, _moveSpeed, deltaTime);
    }

    public void MoveSprint(Vector2 moveInput, Quaternion cameraRotation, float deltaTime)
    {
        // 달리기는 락온 여부와 관계없이 카메라 기준으로 이동하고 이동 방향을 바라본다.
        MoveInternal(moveInput, cameraRotation, FacingMode.MoveDirection, _sprintSpeed, deltaTime);
    }

    public void MoveLockedOn(Vector2 moveInput, Vector3 targetPosition, float deltaTime)
    {
        Vector3 targetDirection = targetPosition - transform.position;
        targetDirection.y = 0f;

        Quaternion targetRotation = targetDirection.sqrMagnitude > 0f ? Quaternion.LookRotation(targetDirection) : transform.rotation;
        MoveInternal(moveInput, targetRotation, FacingMode.ReferenceDirection, _moveSpeed, deltaTime);
    }

    public void MoveAttack(Vector2 localDisplacement, float deltaTime)
    {
        // 공격 데이터의 X는 좌우, Y는 전후 방향의 실제 이동 거리로 적용한다.
        Vector3 displacement = transform.right * localDisplacement.x + transform.forward * localDisplacement.y;

        ApplyGravity(deltaTime);
        displacement.y = _verticalVelocity * deltaTime;

        _characterController.Move(displacement);
    }

    public void MovePoiseReaction(Vector3 worldDisplacement, float deltaTime)
    {
        if (deltaTime <= 0f) return;

        // 피격 이동을 걷기 애니메이션의 이동 입력으로 사용하지 않는다.
        MoveAmount = 0f;
        LocalMoveDirection = Vector2.zero;

        // 수평 이동은 피격 설정을 사용하고 수직 이동은 기존 중력을 유지한다.
        ApplyGravity(deltaTime);
        worldDisplacement.y = _verticalVelocity * deltaTime;
        _characterController.Move(worldDisplacement);
    }

    public void MoveStationary(float deltaTime)
    {
        MoveAmount = 0f;
        LocalMoveDirection = Vector2.zero;

        ApplyGravity(deltaTime);

        Vector3 displacement = Vector3.up * (_verticalVelocity * deltaTime);
        _characterController.Move(displacement);
    }

    public void BeginDodge(Vector2 moveInput, Quaternion cameraRotation)
    {
        BeginDodgeInternal(moveInput, cameraRotation);
    }

    public void BeginLockedOnDodge(Vector2 moveInput, Vector3 targetPosition)
    {
        Vector3 targetDirection = targetPosition - transform.position;
        targetDirection.y = 0f;

        Quaternion referenceRotation = targetDirection.sqrMagnitude > 0f ? Quaternion.LookRotation(targetDirection) : transform.rotation;
        BeginDodgeInternal(moveInput, referenceRotation);
    }

    public bool UpdateDodge(float deltaTime)
    {
        if (_dodgeDuration <= 0f)
        {
            EndDodge();
            return true;
        }

        float remainingTime = _dodgeDuration - _dodgeElapsedTime;
        float dodgeDeltaTime = Mathf.Min(deltaTime, remainingTime);

        ApplyGravity(deltaTime);

        Vector3 displacement = _dodgeWorldDirection * (_dodgeSpeed * dodgeDeltaTime);
        displacement.y = _verticalVelocity * deltaTime;
        _characterController.Move(displacement);

        _dodgeElapsedTime += deltaTime;

        if (_dodgeElapsedTime < _dodgeDuration) return false;

        EndDodge();
        return true;
    }

    public void EndDodge()
    {
        _dodgeElapsedTime = 0f;
    }

    private void MoveInternal(Vector2 moveInput, Quaternion referenceRotation, FacingMode facingMode, float moveSpeed, float deltaTime)
    {
        Vector3 inputDirection = Vector3.ClampMagnitude(new Vector3(moveInput.x, 0f, moveInput.y), 1f);
        Vector3 moveDirection = referenceRotation * inputDirection;

        MoveAmount = inputDirection.magnitude;

        // 락온 중에는 이동 방향과 관계없이 현재 타깃을 계속 바라본다.
        Rotate(facingMode == FacingMode.ReferenceDirection ? referenceRotation * Vector3.forward : moveDirection, deltaTime);

        // 회전 중에는 측면 모션을 섞고, 회전이 끝나면 전진 모션으로 수렴시킨다.
        Vector3 localMoveDirection = transform.InverseTransformDirection(moveDirection);
        LocalMoveDirection = new Vector2(localMoveDirection.x, localMoveDirection.z);

        ApplyGravity(deltaTime);

        moveDirection *= moveSpeed;
        moveDirection.y = _verticalVelocity;

        _characterController.Move(moveDirection * deltaTime);
    }

    private void BeginDodgeInternal(Vector2 moveInput, Quaternion referenceRotation)
    {
        Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y);

        if (inputDirection.sqrMagnitude <= 0f)
        {
            // 이동 입력이 없다면 현재 캐릭터의 전방으로 회피한다.
            _dodgeWorldDirection = transform.forward;
            _dodgeWorldDirection.y = 0f;
            _dodgeWorldDirection.Normalize();
            DodgeLocalDirection = Vector2.up;
        }
        else
        {
            _dodgeWorldDirection = referenceRotation * inputDirection.normalized;
            _dodgeWorldDirection.y = 0f;
            _dodgeWorldDirection.Normalize();

            Vector3 localDodgeDirection = transform.InverseTransformDirection(_dodgeWorldDirection);
            DodgeLocalDirection = new Vector2(localDodgeDirection.x, localDodgeDirection.z).normalized;
        }

        _dodgeElapsedTime = 0f;
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
