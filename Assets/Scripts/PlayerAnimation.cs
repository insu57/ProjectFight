using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAnimation : MonoBehaviour
{
    private static readonly int _moveXHash = Animator.StringToHash("MoveX");
    private static readonly int _moveYHash = Animator.StringToHash("MoveY");

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
}
