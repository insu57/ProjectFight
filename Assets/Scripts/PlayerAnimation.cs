using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAnimation : MonoBehaviour
{
    private static readonly int _moveXHash = Animator.StringToHash("MoveX");
    private static readonly int _moveYHash = Animator.StringToHash("MoveY");
    private static readonly int _dodgeXHash = Animator.StringToHash("DodgeX");
    private static readonly int _dodgeYHash = Animator.StringToHash("DodgeY");
    private static readonly int _dodgeHash = Animator.StringToHash("Dodge");
    private static readonly int _isSprintingHash = Animator.StringToHash("IsSprinting");

    [SerializeField, Min(0f)] private float _movementDampTime = 0.1f;

    private Animator _playerAnimator;

    private void Awake()
    {
        _playerAnimator = GetComponentInChildren<Animator>();
    }

    public void SetMovement(Vector2 localMoveDirection, float deltaTime)
    {
        if (!_playerAnimator) return;

        _playerAnimator.SetFloat(_moveXHash, localMoveDirection.x, _movementDampTime, deltaTime);
        _playerAnimator.SetFloat(_moveYHash, localMoveDirection.y, _movementDampTime, deltaTime);
    }

    public void PlayDodge(Vector2 dodgeDirection)
    {
        if (!_playerAnimator) return;

        // 방향값을 먼저 고정한 뒤 회피 상태로 전환한다.
        _playerAnimator.SetFloat(_dodgeXHash, dodgeDirection.x);
        _playerAnimator.SetFloat(_dodgeYHash, dodgeDirection.y);
        _playerAnimator.SetTrigger(_dodgeHash);
    }

    public void SetSprinting(bool isSprinting)
    {
        if (!_playerAnimator) return;

        _playerAnimator.SetBool(_isSprintingHash, isSprinting);
    }
}
