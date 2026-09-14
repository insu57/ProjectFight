using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerManager : MonoBehaviour, IHitStopReceiver
{
    private PlayerControl _playerControl;
    private PlayerState _playerState;
    private PlayerStat _playerStat;
    private PlayerMove _playerMove;
    private PlayerDodge _playerDodge;
    private PlayerAttack _playerAttack;
    private AttackHitbox _attackHitbox;
    private Hurtbox _hurtbox;
    private PlayerAnimation _playerAnimation;
    private PlayerCamera _playerCamera;
    private PlayerLockOn _playerLockOn;
    private PlayerSkillTree _playerSkillTree;
    private SkillTreeRuntime _subscribedSkillTreeRuntime;
    private bool _hasPendingGuardChange;
    private float _poiseReactionRemaining;
    // 반응 시작 시 결정한 월드 방향과 속도를 유지한다.
    private Vector3 _poiseReactionVelocity;

    private bool IsHitStopped => _playerAnimation && _playerAnimation.IsHitStopped;
    private bool IsInPoiseReaction => _playerState && (_playerState.ActionState == PlayerActionState.Stunned || _playerState.ActionState == PlayerActionState.GuardBroken);

    public event Action<LockOnTarget> LockOnTargetChanged;
    public event Action PlayerWindowRequested;
    public event Action<PlayerStatSnapshot> PlayerStatsChanged;
    public event Action<int> SkillPointsChanged;
    public event Action<SkillTreeNodeRuntime> SkillTreeNodeUnlocked;

    public SkillTreeRuntime SkillTreeRuntime => _playerSkillTree ? _playerSkillTree.Runtime : null;
    public PlayerStatSnapshot CurrentPlayerStats => _playerStat ? _playerStat.CurrentStats : default;

    private void Awake()
    {
        TryGetComponent(out _playerControl);
        TryGetComponent(out _playerState);
        TryGetComponent(out _playerStat);
        TryGetComponent(out _playerMove);
        TryGetComponent(out _playerDodge);
        TryGetComponent(out _playerAttack);
        TryGetComponent(out _playerAnimation);
        TryGetComponent(out _playerCamera);
        TryGetComponent(out _playerLockOn);
        TryGetComponent(out _playerSkillTree);

        _attackHitbox = GetComponentInChildren<AttackHitbox>();
        _hurtbox = GetComponentInChildren<Hurtbox>();

        if (_attackHitbox)
        {
            _attackHitbox.Initialize(this);
        }

        if (_hurtbox)
        {
            _hurtbox.Initialize(this);
        }
    }

    private void OnEnable()
    {
        if (_hurtbox)
        {
            _hurtbox.DamageRequested += HandleDamageRequested;
        }

        if (_playerState)
        {
            _playerState.ActionStateChanged += HandlePlayerActionStateChanged;

            // 재활성화 시에도 현재 회피 상태에 맞춰 피격 허용 여부를 동기화한다.
            HandlePlayerActionStateChanged(_playerState.ActionState);
        }

        if (_playerControl)
        {
            _playerControl.LockOnPressed += HandleLockOnPressed;
            _playerControl.DodgePressed += HandleDodgePressed;
            _playerControl.AttackPressed += HandleAttackPressed;
            _playerControl.GuardChanged += HandleGuardChanged;
            _playerControl.PlayerWindowPressed += HandlePlayerWindowPressed;
        }

        if (_playerStat)
        {
            _playerStat.StatsChanged += HandlePlayerStatsChanged;
        }

        SubscribeSkillTreeRuntime();
    }

    private void Start()
    {
        // 모든 컴포넌트의 Awake가 끝난 뒤 생성된 스킬 트리 Runtime 구독을 보장한다.
        SubscribeSkillTreeRuntime();
    }

    private void OnDisable()
    {
        _hasPendingGuardChange = false;

        if (_playerAnimation)
        {
            _playerAnimation.ClearHitStop();
        }

        if (_hurtbox)
        {
            _hurtbox.DamageRequested -= HandleDamageRequested;

            // Manager 비활성화 동안에는 피해 수신을 차단한다.
            _hurtbox.SetDamageEnabled(false);
        }

        if (_playerState)
        {
            _playerState.ActionStateChanged -= HandlePlayerActionStateChanged;
        }

        if (_attackHitbox)
        {
            _attackHitbox.EndAttack();
        }

        if (_playerControl)
        {
            _playerControl.LockOnPressed -= HandleLockOnPressed;
            _playerControl.DodgePressed -= HandleDodgePressed;
            _playerControl.AttackPressed -= HandleAttackPressed;
            _playerControl.GuardChanged -= HandleGuardChanged;
            _playerControl.PlayerWindowPressed -= HandlePlayerWindowPressed;
        }

        if (_playerStat)
        {
            _playerStat.StatsChanged -= HandlePlayerStatsChanged;
        }

        UnsubscribeSkillTreeRuntime();
    }

    public void BeginHitStop(float duration)
    {
        if (!isActiveAndEnabled || !_playerAnimation) return;

        // 하위 컴포넌트 호출은 PlayerManager가 중개한다.
        _playerAnimation.BeginHitStop(duration);
    }

    private void HandlePlayerActionStateChanged(PlayerActionState actionState)
    {
        if (!_hurtbox) return;

        // 회피 상태에서 벗어나면 종료 원인과 관계없이 피격을 다시 허용한다.
        _hurtbox.SetDamageEnabled(actionState != PlayerActionState.Dodging);
    }

    private bool CanBlock(DamageInfo damageInfo)
    {
        if (!_playerState || !_playerControl) return false;
        if (_playerState.ActionState != PlayerActionState.Guarding) return false;

        // 가드 종료 모션 중에는 Guarding 상태여도 공격을 막지 않는다.
        if (!_playerControl.IsGuardRequested) return false;
        if (!damageInfo.Attacker) return false;

        // 높이 차이를 제외하고 피격 순간 공격자의 위치로 방향을 판단한다.
        Vector3 toAttacker = damageInfo.Attacker.transform.position - transform.position;
        toAttacker.y = 0f;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        // 수평 방향을 판단할 수 없는 경우에는 일반 피격으로 처리한다.
        if (toAttacker.sqrMagnitude <= 0.0001f || forward.sqrMagnitude <= 0.0001f) return false;

        // 내적의 부호만 사용하므로 정규화 없이 전방 180도를 판정한다.
        // 정확히 측면인 경계와 후방의 공격은 막지 않는다.
        return Vector3.Dot(forward, toAttacker) > 0f;
    }

    private void HandleDamageRequested(DamageInfo damageInfo)
    {
        if (!isActiveAndEnabled || !_playerStat || !_playerState) return;
        if (_playerState.ActionState == PlayerActionState.Dead) return;
        if (_playerStat.CurrentStats.CurrentHealth <= 0) return;
        if (!damageInfo.HasDamage) return;

        if (CanBlock(damageInfo))
        {
            // 가드 시 실제로 적용할 체력 피해나 가드 강인도 피해가 있는지 확인한다.
            if (damageInfo.Damage <= 0f && damageInfo.GuardPoiseDamage <= 0f) return;

            damageInfo.ApplyHitStop(this);

            // 체력과 일반 강인도는 유지하고 가드 강인도만 감소시킨다.
            _playerStat.TakeGuardPoiseDamage(damageInfo.GuardPoiseDamage);

            if (damageInfo.GuardPoiseDamage > 0f && _playerStat.CurrentGuardPoise <= 0f)
            {
                BeginPoiseReaction(true, damageInfo.Attacker);
            }

            return;
        }

        // 가드하지 않은 대상에게 가드 강인도 피해만 있는 공격은 적용하지 않는다.
        if (damageInfo.Damage <= 0f && damageInfo.PoiseDamage <= 0f) return;

        damageInfo.ApplyHitStop(this);

        // 강인도를 먼저 처리하여 치명타에서도 해당 공격의 감소량을 반영한다.
        _playerStat.TakePoiseDamage(damageInfo.PoiseDamage);

        // 현재 플레이어 체력 타입에 맞춰 양수 피해의 소수점을 올림한다.
        int damage = Mathf.CeilToInt(damageInfo.Damage);
        _playerStat.TakeDamage(damage);

        // 치명적인 공격에서는 경직보다 기존 사망 처리를 우선한다.
        if (_playerStat.CurrentStats.CurrentHealth > 0)
        {
            if (damageInfo.PoiseDamage > 0f && _playerStat.CurrentPoise <= 0f)
            {
                BeginPoiseReaction(false, damageInfo.Attacker);
            }

            return;
        }

        _poiseReactionRemaining = 0f;
        _poiseReactionVelocity = Vector3.zero;
        _hasPendingGuardChange = false;

        // 사망 시 진행 중인 공격과 가드를 종료하고 행동을 막는다.
        if (_attackHitbox)
        {
            _attackHitbox.EndAttack();
        }

        if (_playerAttack)
        {
            _playerAttack.EndAttack();
        }

        if (_playerAnimation)
        {
            _playerAnimation.SetGuarding(false);
            _playerAnimation.CompleteAttack();
        }

        _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
        _playerState.SetActionState(PlayerActionState.Dead);
    }

    private void BeginPoiseReaction(bool isGuardBreak, MonoBehaviour attacker)
    {
        if (!_playerState || !_playerStat) return;
        if (_playerState.ActionState == PlayerActionState.Dead || IsInPoiseReaction) return;

        PlayerStatSnapshot stats = _playerStat.CurrentStats;
        if (stats.CurrentHealth <= 0) return;

        // 공격 판정과 콤보 예약을 함께 취소한다.
        if (_attackHitbox)
        {
            _attackHitbox.EndAttack();
        }

        if (_playerAttack)
        {
            _playerAttack.EndAttack();
        }

        // 회피 이동을 중단하되 이미 소비한 회피 횟수는 반환하지 않는다.
        if (_playerMove)
        {
            _playerMove.EndDodge();
        }

        _hasPendingGuardChange = false;
        _poiseReactionRemaining = Mathf.Max(0f, isGuardBreak ? stats.GuardBreakDuration : stats.StaggerDuration);

        float moveDistance = isGuardBreak ? stats.GuardBreakMoveDistance : stats.StaggerMoveDistance;

        // 공격자 위치에서 피격자 위치로 향하는 방향으로 밀려난다.
        Vector3 pushDirection = attacker ? transform.position - attacker.transform.position : -transform.forward;
        pushDirection.y = 0f;

        // 두 위치가 겹쳐 방향을 구할 수 없으면 피격자의 뒤쪽을 사용한다.
        if (pushDirection.sqrMagnitude <= 0.0001f)
        {
            pushDirection = -transform.forward;
            pushDirection.y = 0f;
        }

        pushDirection.Normalize();

        // 시작 순간의 방향과 속도를 저장하여 공격자가 움직여도 그대로 유지한다.
        _poiseReactionVelocity = _poiseReactionRemaining > 0f ? pushDirection * (Mathf.Max(0f, moveDistance) / _poiseReactionRemaining) : Vector3.zero;

        _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
        _playerState.SetActionState(isGuardBreak ? PlayerActionState.GuardBroken : PlayerActionState.Stunned);

        // 애니메이션이 누락되어도 행동 제한과 복귀 타이머는 유지한다.
        if (_playerAnimation && _playerAnimation.TryPlayPoiseReaction(isGuardBreak)) return;

        if (_playerAnimation)
        {
            _playerAnimation.CompletePoiseReaction();
        }

        Debug.LogWarning("플레이어 Animator 또는 Stagger/GuardBreak 상태가 없습니다.", this);
    }

    private void UpdatePoiseReaction(float deltaTime)
    {
        // 마지막 프레임에서는 남은 반응 시간만큼만 이동하여 거리 초과를 막는다.
        float movementDeltaTime = Mathf.Min(deltaTime, _poiseReactionRemaining);

        if (_playerMove)
        {
            _playerMove.MovePoiseReaction(_poiseReactionVelocity * movementDeltaTime, deltaTime);
        }

        if (_playerAnimation)
        {
            _playerAnimation.SetSprinting(false);
            _playerAnimation.SetMovement(Vector2.zero, deltaTime);
        }

        _poiseReactionRemaining = Mathf.Max(0f, _poiseReactionRemaining - deltaTime);
        if (_poiseReactionRemaining > 0f) return;

        _poiseReactionVelocity = Vector3.zero;

        if (_playerAnimation)
        {
            _playerAnimation.CompletePoiseReaction();
        }

        _playerState.SetActionState(PlayerActionState.Free);
    }

    private void SubscribeSkillTreeRuntime()
    {
        SkillTreeRuntime skillTreeRuntime = SkillTreeRuntime;

        if (skillTreeRuntime == null || skillTreeRuntime == _subscribedSkillTreeRuntime)
        {
            return;
        }

        UnsubscribeSkillTreeRuntime();

        skillTreeRuntime.AvailablePointsChanged += HandleSkillPointsChanged;
        skillTreeRuntime.NodeUnlocked += HandleSkillTreeNodeUnlocked;
        _subscribedSkillTreeRuntime = skillTreeRuntime;
    }

    private void UnsubscribeSkillTreeRuntime()
    {
        if (_subscribedSkillTreeRuntime == null)
        {
            return;
        }

        _subscribedSkillTreeRuntime.AvailablePointsChanged -= HandleSkillPointsChanged;
        _subscribedSkillTreeRuntime.NodeUnlocked -= HandleSkillTreeNodeUnlocked;
        _subscribedSkillTreeRuntime = null;
    }

    private void Update()
    {
        if (_playerAnimation)
        {
            _playerAnimation.UpdateHitStop();
        }

        // 히트스톱과 피격 반응 중에는 회복량과 회복 대기 시간 모두 진행하지 않는다.
        if (_playerStat && _playerState && _playerState.ActionState != PlayerActionState.Dead && !IsHitStopped && !IsInPoiseReaction)
        {
            _playerStat.UpdatePoiseRecovery(Time.deltaTime);
        }

        if (_playerDodge && !IsHitStopped)
        {
            _playerDodge.UpdateRecharge(Time.deltaTime);
        }

        if (_playerLockOn && _playerLockOn.TryReleaseInvalidTarget())
        {
            // 락온 타깃이 유효하지 않게 되면 변경 사실을 전달한다.
            NotifyLockOnTargetChanged();
        }

        // 공격이 취소되거나 다른 행동 상태로 바뀌면 판정을 종료한다.
        if (_attackHitbox && (!_playerState || _playerState.ActionState != PlayerActionState.Attacking))
        {
            _attackHitbox.EndAttack();
        }

        if (!_playerControl || !_playerState || !_playerMove || !_playerCamera) return;

        _playerCamera.SetLookInput(_playerControl.LookInput, _playerControl.IsLookInputDelta);

        LockOnTarget lockOnTarget = _playerLockOn.CurrentTarget;

        if (lockOnTarget)
        {
            _playerCamera.SetLockOnTarget(lockOnTarget.AimPosition);
        }
        else
        {
            _playerCamera.ClearLockOnTarget();
        }

        // 카메라 입력 전달은 유지하고 캐릭터 동작만 정지한다.
        if (IsHitStopped) return;

        // 피격 반응을 먼저 처리하여 기존 이동·공격·가드 갱신을 막는다.
        if (IsInPoiseReaction)
        {
            UpdatePoiseReaction(Time.deltaTime);
            return;
        }

        // 정지 중 바뀐 가드 입력을 해제 후 반영한다.
        if (_hasPendingGuardChange)
        {
            _hasPendingGuardChange = false;
            HandleGuardChanged(_playerControl.IsGuardRequested);
        }

        UpdateLocomotionState();

        if (_playerState.ActionState == PlayerActionState.Dodging)
        {
            if (_playerMove.UpdateDodge(Time.deltaTime))
            {
                // 회피 이동이 끝나면 다음 행동을 받을 수 있는 상태로 복귀한다.
                _playerState.SetActionState(PlayerActionState.Free);
            }
        }
        else if (_playerState.ActionState == PlayerActionState.Attacking)
        {
            UpdateAttack();
        }
        else if (_playerState.ActionState == PlayerActionState.Guarding)
        {
            UpdateGuard();
        }
        else if (_playerState.CanMove && _playerState.LocomotionState == PlayerLocomotionState.Sprinting)
        {
            // 달리기는 락온 상태에서도 카메라 기준 자유 이동을 사용한다.
            _playerMove.MoveSprint(_playerControl.MoveInput, _playerCamera.YawRotation, Time.deltaTime);
        }
        else if (_playerState.CanMove && lockOnTarget)
        {
            _playerMove.MoveLockedOn(_playerControl.MoveInput, lockOnTarget.AimPosition, Time.deltaTime);
        }
        else if (_playerState.CanMove)
        {
            _playerMove.Move(_playerControl.MoveInput, _playerCamera.YawRotation, Time.deltaTime);
        }

        if (_playerAnimation)
        {
            _playerAnimation.SetSprinting(_playerState.LocomotionState == PlayerLocomotionState.Sprinting);
            _playerAnimation.SetMovement(_playerMove.LocalMoveDirection, Time.deltaTime);
        }
    }

    public void SetMaxDodgeCount(int maxDodgeCount)
    {
        if (_playerDodge)
        {
            // 스킬 시스템은 PlayerManager를 통해 최대 회피 횟수를 변경한다.
            _playerDodge.SetMaxDodgeCount(maxDodgeCount);
        }
    }

    public void SetGameplayInputEnabled(bool isEnabled)
    {
        if (_playerControl)
        {
            // UI의 입력 제한 요청을 PlayerControl로 중개한다.
            _playerControl.SetGameplayInputEnabled(isEnabled);
        }
    }

    public SkillTreeUnlockResult TryUnlockSkillTreeNode(int nodeId)
    {
        if (!_playerSkillTree)
        {
            return SkillTreeUnlockResult.RuntimeNotInitialized;
        }

        return _playerSkillTree.TryUnlockNode(nodeId);
    }

    public void AddSkillPoints(int amount)
    {
        if (_playerSkillTree)
        {
            // 외부 시스템의 포인트 지급 요청을 PlayerSkillTree로 중개한다.
            _playerSkillTree.AddPoints(amount);
        }
    }

    private void HandleLockOnPressed()
    {
        if (!_playerLockOn) return;

        _playerLockOn.ToggleTarget();
        NotifyLockOnTargetChanged();
    }

    private void HandlePlayerWindowPressed()
    {
        // PlayerControl의 창 열기 요청을 UI가 받을 수 있도록 중개한다.
        PlayerWindowRequested?.Invoke();
    }

    private void HandleDodgePressed()
    {
        // 히트스탑 중에는 새 회피를 시작하지 않는다.
        if (IsHitStopped) return;

        if (!_playerControl || !_playerState || !_playerMove || !_playerDodge || !_playerAnimation || !_playerCamera) return;
        if (!_playerState.CanDodge || !_playerDodge.CanDodge) return;

        LockOnTarget lockOnTarget = _playerLockOn ? _playerLockOn.CurrentTarget : null;

        if (lockOnTarget)
        {
            _playerMove.BeginLockedOnDodge(_playerControl.MoveInput, lockOnTarget.AimPosition);
        }
        else
        {
            _playerMove.BeginDodge(_playerControl.MoveInput, _playerCamera.YawRotation);
        }

        // 회피 상태로 전환할 때 회피 횟수를 소비하고 일반 이동 상태를 해제한다.
        _playerDodge.ConsumeDodge();
        _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
        _playerState.SetActionState(PlayerActionState.Dodging);
        _playerAnimation.PlayDodge(_playerMove.DodgeLocalDirection);
    }

    private void HandleAttackPressed(AttackInputType inputType)
    {
        if (!_playerState || !_playerAttack || !_playerAnimation) return;

        if (_playerState.ActionState == PlayerActionState.Attacking)
        {
            // 정지 중에도 기존 콤보 입력 허용 구간에서는 예약할 수 있다.
            QueueComboInput(inputType);
            return;
        }

        if (IsHitStopped) return;
        if (!_playerState.CanUseNormalAttack) return;
        if (!_playerAttack.TryBeginAttack(inputType, out AttackDefinition attackDefinition)) return;

        _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
        _playerState.SetActionState(PlayerActionState.Attacking);
        BeginAttackPresentation(attackDefinition);
    }

    private void BeginAttackPresentation(AttackDefinition attackDefinition)
    {
        // 공격 설정을 판정 컴포넌트에 전달한 뒤 애니메이션을 시작한다.
        if (_attackHitbox)
        {
            _attackHitbox.BeginAttack(attackDefinition);
        }

        _playerAnimation.PlayAttack(attackDefinition);
    }

    private void HandleGuardChanged(bool isGuarding)
    {
        if (!_playerState || !_playerAnimation) return;

        // 반응 중 가드 입력은 예약하지 않는다.
        if (IsInPoiseReaction) return;

        if (IsHitStopped)
        {
            // 정지 중에는 상태를 바꾸지 않고 해제 후 최종 입력 상태를 반영한다.
            _hasPendingGuardChange = true;
            return;
        }

        if (isGuarding)
        {
            if (!_playerState.CanGuard) return;

            // 가드를 시작하면 일반 이동을 해제하고 가드 진입 모션을 요청한다.
            _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
            _playerState.SetActionState(PlayerActionState.Guarding);
            _playerAnimation.SetGuarding(true);
            return;
        }

        if (_playerState.ActionState != PlayerActionState.Guarding) return;

        // 상태는 유지하여 탈출 모션이 끝날 때까지 다른 행동이 개입하지 않게 한다.
        _playerAnimation.SetGuarding(false);
    }

    private void QueueComboInput(AttackInputType inputType)
    {
        if (!_playerAnimation.TryGetAttackNormalizedTime(out float normalizedTime)) return;

        _playerAttack.TryQueueNextAttack(inputType, normalizedTime);
    }

    private void UpdateAttack()
    {
        bool hasNormalizedTime = _playerAnimation.TryGetAttackNormalizedTime(out float normalizedTime);

        if (hasNormalizedTime && _playerAttack.TryGetAttackMoveDisplacement(normalizedTime, out Vector2 localDisplacement))
        {
            // PlayerManager가 공격 진행 정보를 이동 컴포넌트로 전달한다.
            _playerMove.MoveAttack(localDisplacement, Time.deltaTime);
        }
        else
        {
            // 이동 구간 밖이나 공격 State 진입 전에도 중력은 계속 적용한다.
            _playerMove.MoveAttack(Vector2.zero, Time.deltaTime);
        }

        if (hasNormalizedTime && _playerAttack.TryConsumeQueuedAttack(normalizedTime, out AttackDefinition nextAttack))
        {
            // 다음 공격으로 전환하면서 이전 박스를 끄고 새 범위를 적용한다.
            BeginAttackPresentation(nextAttack);
            return;
        }

        if (_playerAnimation.IsAttackFinished())
        {
            if (_attackHitbox)
            {
                _attackHitbox.EndAttack();
            }

            _playerAttack.EndAttack();
            _playerAnimation.CompleteAttack();
            _playerState.SetActionState(PlayerActionState.Free);
            return;
        }

        if (_attackHitbox)
        {
            _attackHitbox.UpdateHitbox(hasNormalizedTime, normalizedTime);
        }
    }

    private void UpdateGuard()
    {
        // 가드 중 이동은 막되 CharacterController의 중력 처리는 유지한다.
        //추후 가드 이동 추가
        _playerMove.MoveStationary(Time.deltaTime);

        if (_playerControl.IsGuardRequested) return;
        if (!_playerAnimation.IsGuardFinished()) return;

        _playerState.SetActionState(PlayerActionState.Free);
    }

    private void UpdateLocomotionState()
    {
        if (!_playerState.CanMove || _playerControl.MoveInput.sqrMagnitude <= 0.01f)
        {
            _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
            return;
        }

        if (_playerControl.IsSprintRequested)
        {
            _playerState.SetLocomotionState(PlayerLocomotionState.Sprinting);
            return;
        }

        _playerState.SetLocomotionState(PlayerLocomotionState.Moving);
    }

    private void NotifyLockOnTargetChanged()
    {
        LockOnTargetChanged?.Invoke(_playerLockOn.CurrentTarget);
    }

    private void HandleSkillPointsChanged(int availablePoints)
    {
        SkillPointsChanged?.Invoke(availablePoints);
    }

    private void HandlePlayerStatsChanged(PlayerStatSnapshot stats)
    {
        // 플레이어 하위 컴포넌트의 변경 알림을 UI가 직접 구독하지 않도록 중개한다.
        PlayerStatsChanged?.Invoke(stats);
    }

    private void HandleSkillTreeNodeUnlocked(SkillTreeNodeRuntime node)
    {
        SkillTreeNodeUnlocked?.Invoke(node);
    }
}
