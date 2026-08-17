using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAnimation : MonoBehaviour
{
    private const string LocomotionStateName = "Base Layer.Locomotion";

    private static readonly int _moveXHash = Animator.StringToHash("MoveX");
    private static readonly int _moveYHash = Animator.StringToHash("MoveY");
    private static readonly int _dodgeXHash = Animator.StringToHash("DodgeX");
    private static readonly int _dodgeYHash = Animator.StringToHash("DodgeY");
    private static readonly int _dodgeHash = Animator.StringToHash("Dodge");
    private static readonly int _isSprintingHash = Animator.StringToHash("IsSprinting");
    private static readonly int _attackTagHash = Animator.StringToHash("Attack");
    private static readonly int _locomotionStateHash = Animator.StringToHash(LocomotionStateName);

    [SerializeField, Min(0f)] private float _movementDampTime = 0.1f;
    [SerializeField, Min(0f)] private float _movementThreshold = 0.001f;
    [SerializeField, Min(0f)] private float _attackExitTransitionDuration = 0.1f;

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

    public void PlayAttack(AttackDefinition attackDefinition)
    {
        if (!_playerAnimator || !attackDefinition)
        {
            return;
        }

        // 콤보 그래프가 선택한 공격 State로 직접 전환한다.
        _playerAnimator.CrossFadeInFixedTime(attackDefinition.AnimationStateHash, attackDefinition.TransitionDuration, 0);

        _isAttackRequested = true;
        _hasEnteredAttackState = false;
    }

    public bool TryGetAttackNormalizedTime(out float normalizedTime)
    {
        normalizedTime = 0f;

        if (!_playerAnimator || !_isAttackRequested)
        {
            return false;
        }

        if (_playerAnimator.IsInTransition(0))
        {
            AnimatorStateInfo nextStateInfo = _playerAnimator.GetNextAnimatorStateInfo(0);

            if (nextStateInfo.tagHash == _attackTagHash)
            {
                _hasEnteredAttackState = true;
                normalizedTime = nextStateInfo.normalizedTime;
                return true;
            }
        }

        AnimatorStateInfo currentStateInfo = _playerAnimator.GetCurrentAnimatorStateInfo(0);

        if (currentStateInfo.tagHash != _attackTagHash)
        {
            return false;
        }

        _hasEnteredAttackState = true;
        normalizedTime = currentStateInfo.normalizedTime;
        return true;
    }

    public bool IsAttackFinished()
    {
        if (!_playerAnimator || !_isAttackRequested)
        {
            return false;
        }

        bool isInTransition = _playerAnimator.IsInTransition(0);

        if (isInTransition)
        {
            AnimatorStateInfo nextStateInfo = _playerAnimator.GetNextAnimatorStateInfo(0);

            // 다른 공격으로 전환 중이면 현재 공격이 끝난 것으로 처리하지 않는다.
            if (nextStateInfo.tagHash == _attackTagHash)
            {
                _hasEnteredAttackState = true;
                return false;
            }
        }

        AnimatorStateInfo currentStateInfo = _playerAnimator.GetCurrentAnimatorStateInfo(0);
        bool isAttackState = currentStateInfo.tagHash == _attackTagHash;

        if (isAttackState)
        {
            _hasEnteredAttackState = true;
            return !isInTransition && currentStateInfo.normalizedTime >= 1f;
        }

        return _hasEnteredAttackState;
    }

    public void CompleteAttack()
    {
        if (_playerAnimator)
        {
            // 공격 State에 별도 종료 Transition이 없어도 이동 상태로 돌아가게 한다.
            _playerAnimator.CrossFadeInFixedTime(_locomotionStateHash, _attackExitTransitionDuration, 0);
        }

        _isAttackRequested = false;
        _hasEnteredAttackState = false;
    }

    public void SetSprinting(bool isSprinting)
    {
        if (!_playerAnimator) return;

        _playerAnimator.SetBool(_isSprintingHash, isSprinting);
    }
}
