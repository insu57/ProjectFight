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
    private static readonly int _attackHash = Animator.StringToHash("Attack");
    private static readonly int _attackTypeHash = Animator.StringToHash("AttackType");
    private static readonly int _attackTagHash = Animator.StringToHash("Attack");

    [SerializeField, Min(0f)] private float _movementDampTime = 0.1f;
    [SerializeField, Min(0f)] private float _movementThreshold = 0.001f;

    private Animator _playerAnimator;
    private bool _isAttackRequested;
    private bool _hasEnteredAttackState;

    private void Awake()
    {
        _playerAnimator = GetComponentInChildren<Animator>();
    }

    public void SetMovement(Vector2 localMoveDirection, float deltaTime)
    {
        if (!_playerAnimator) return;

        float movementThresholdSqr = _movementThreshold * _movementThreshold;
        bool hasMoveDirection = localMoveDirection.sqrMagnitude > movementThresholdSqr;

        // 미세한 입력값이 Animator의 이동 방향으로 전달되지 않도록 제거한다.
        if (!hasMoveDirection)
        {
            localMoveDirection = Vector2.zero;

            float currentMoveX = _playerAnimator.GetFloat(_moveXHash);
            float currentMoveY = _playerAnimator.GetFloat(_moveYHash);

            // 감쇠로 남은 극소값은 정확히 0으로 고정한다.
            if (currentMoveX * currentMoveX + currentMoveY * currentMoveY <= movementThresholdSqr)
            {
                _playerAnimator.SetFloat(_moveXHash, 0f);
                _playerAnimator.SetFloat(_moveYHash, 0f);
                return;
            }
        }

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

    public void PlayAttack(PlayerAttackType attackType)
    {
        if (!_playerAnimator) return;

        // Animator가 공격 종류에 맞는 상태를 선택하도록 타입을 먼저 전달한다.
        _playerAnimator.SetInteger(_attackTypeHash, (int)attackType);
        _playerAnimator.ResetTrigger(_attackHash);
        _playerAnimator.SetTrigger(_attackHash);

        _isAttackRequested = true;
        _hasEnteredAttackState = false;
    }
    //개선 및 수정 필요
    public bool IsAttackFinished()
    {
        if (!_playerAnimator || !_isAttackRequested) return false;

        AnimatorStateInfo stateInfo = _playerAnimator.GetCurrentAnimatorStateInfo(0);
        bool isAttackState = stateInfo.tagHash == _attackTagHash;

        if (isAttackState)
        {
            _hasEnteredAttackState = true;

            // 전환 중이 아닐 때 클립이 끝까지 재생되었는지 확인한다.
            return !_playerAnimator.IsInTransition(0) && stateInfo.normalizedTime >= 1f;
        }

        // 공격 상태에 진입한 뒤 다른 상태로 전환됐다면 공격이 종료된 것이다.
        return _hasEnteredAttackState;
    }

    public void CompleteAttack()
    {
        _isAttackRequested = false;
        _hasEnteredAttackState = false;
    }

    public void SetSprinting(bool isSprinting)
    {
        if (!_playerAnimator) return;

        _playerAnimator.SetBool(_isSprintingHash, isSprinting);
    }
}
