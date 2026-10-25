using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStat))]
[RequireComponent(typeof(PlayerDodge))]
[RequireComponent(typeof(PlayerState))]
[RequireComponent(typeof(PlayerControl))]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerQuickslot))]
[RequireComponent(typeof(PlayerAnimation))]
[RequireComponent(typeof(PlayerAttack))]
[RequireComponent(typeof(PlayerLockOn))]
[RequireComponent(typeof(PlayerCamera))]
[RequireComponent(typeof(PlayerSkillTree))]

public class PlayerManager : MonoBehaviour, IHitStopReceiver, IAttackHitReceiver, IPlayerItemEffectHost
{
    private const int EquipmentSlotCount = (int)EquipmentSlot.Shoes + 1;
    private const string LethalArtResourcePath = "LethalArtDefinition";

    [Header("발소리")]
    [SerializeField] private SoundDefinition _footstepSound;
    [SerializeField, Min(0.1f), Tooltip("걷기 중 발소리 한 번마다 이동할 수평 거리입니다.")]
    private float _walkFootstepDistance = 1.8f;
    [SerializeField, Min(0.1f), Tooltip("달리기 중 발소리 한 번마다 이동할 수평 거리입니다.")]
    private float _sprintFootstepDistance = 2.5f;

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
    private PlayerQuickslot _playerQuickslot;
    private PlayerInventory _playerInventory;
    // None은 비워 두고 실제 부위별 장착 장비를 같은 배열에 갱신하여 전달한다.
    private readonly ItemDefinition[] _equippedItemsForStats = new ItemDefinition[EquipmentSlotCount];
    private SkillTreeRuntime _subscribedSkillTreeRuntime;
    private bool _hasPendingGuardChange;
    // 능력치와 인벤토리 알림에서 같은 사용 요청이 재진입하지 않도록 한다.
    private bool _isUsingItem;
    // 범위 안의 후보는 유지하고, 표시와 입력에 사용할 가장 가까운 유효 대상을 공유한다.
    private readonly List<IInteractable> _interactionCandidates = new(4);
    private IInteractable _currentInteraction;
    private bool _isGameplayInputEnabled = true;
    // 캐릭터마다 한 번 생성하고 피격 시 재사용한다.
    private readonly PoiseReactionRuntime _poiseReaction = new();
    // 효과 실행 상태는 일반 C# 실행기가 소유하고 컴포넌트 접근은 이 Manager가 중개한다.
    private PlayerItemEffects _itemEffects;
    private LethalArtRuntime _lethalArtRuntime;
    // 에너지 변경 알림 중 같은 입력이 재진입하여 필살기를 중복 결제하지 않게 한다.
    private bool _isStartingLethalArt;
    // 근처 적의 중복 콜라이더와 검색 버퍼는 강공 입력 사이에도 재사용한다.
    private Collider[] _lethalArtCandidateBuffer = new Collider[32];
    private readonly HashSet<EnemyManager> _lethalArtCheckedEnemies = new(32);
    // HUD의 최초 OnEnable은 Manager.Awake보다 빠를 수 있으므로 캐싱 전 조회만 구분한다.
    private bool _hasCachedPlayerComponents;
    private bool _hasStarted;
    private float _distanceSinceFootstep;

    private bool IsHitStopped => _playerAnimation.IsHitStopped;
    private bool IsInPoiseReaction => _playerState.ActionState == PlayerActionState.Stunned
        || _playerState.ActionState == PlayerActionState.GuardBroken;

    public event Action<LockOnTarget> LockOnTargetChanged;
    public event Action PlayerWindowRequested;
    public event Action OptionWindowRequested;
    public event Action<PlayerStatSnapshot> PlayerStatsChanged;
    public event Action<int> SkillPointsChanged;
    public event Action<SkillTreeNodeRuntime> SkillTreeNodeUnlocked;
    public event Action<string> InteractionPromptChanged;
    public event Action<ItemType> InventoryChanged;
    public event Action<ItemDefinition> ItemAcquired;
    public event Action<EquipmentSlot> EquipmentChanged;
    public event Action QuickslotsChanged;

    public SkillTreeRuntime SkillTreeRuntime => _hasCachedPlayerComponents ? _playerSkillTree.Runtime : null;
    public PlayerStatSnapshot CurrentPlayerStats => _hasCachedPlayerComponents ? _playerStat.CurrentStats : default;
    public string CurrentInteractionText { get; private set; } = string.Empty;

    public void GetActiveItemEffects(List<ActiveItemEffectSnapshot> results)
    {
        if (results == null) throw new ArgumentNullException(nameof(results));

        // 표시 목록은 호출자가 재사용하고, UI의 실행기 직접 참조는 Manager가 대신 중개한다.
        results.Clear();
        _itemEffects?.AppendActiveEffects(results);
    }

    private void Awake()
    {
        _itemEffects = new PlayerItemEffects(this);
        _lethalArtRuntime = new LethalArtRuntime(Resources.LoadAll<LethalArtDefinition>(LethalArtResourcePath));
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
        TryGetComponent(out _playerQuickslot);
        TryGetComponent(out _playerInventory);
        // RequireComponent로 보장한 참조는 이후 존재 검사 없이 사용한다.
        _hasCachedPlayerComponents = true;

        // 아이템 효과를 적용하기 전에 원래 설정을 한 번만 기본 회피 횟수로 전달한다.
        _playerStat.SetBaseMaxDodgeCount(_playerDodge.MaxDodgeCount);

        // 결제 알림 중 Manager가 비활성화되어도 확정된 획득 알림은 전달하므로 수명 동안 구독한다.
        _playerInventory.InventoryChanged += HandleInventoryChanged;
        _playerInventory.ItemAcquired += HandleItemAcquired;
        _playerInventory.EquipmentChanged += HandleEquipmentChanged;

        _attackHitbox = GetComponentInChildren<AttackHitbox>();
        _hurtbox = GetComponentInChildren<Hurtbox>();

        _attackHitbox.Initialize(this);

        _hurtbox.Initialize(this);
    }

    private void OnEnable()
    {
        _hurtbox.DamageRequested += HandleDamageRequested;

        _playerState.ActionStateChanged += HandlePlayerActionStateChanged;

        // 재활성화 시에도 현재 회피 상태에 맞춰 피격 허용 여부를 동기화한다.
        HandlePlayerActionStateChanged(_playerState.ActionState);

        _playerControl.LockOnPressed += HandleLockOnPressed;
        _playerControl.DodgePressed += HandleDodgePressed;
        _playerControl.AttackPressed += HandleAttackPressed;
        _playerControl.GuardChanged += HandleGuardChanged;
        _playerControl.PlayerWindowPressed += HandlePlayerWindowPressed;
        _playerControl.OptionWindowPressed += HandleOptionWindowPressed;
        _playerControl.InteractPressed += HandleInteractPressed;
        _playerControl.QuickslotPressed += HandleQuickslotPressed;

        _playerStat.StatsChanged += HandlePlayerStatsChanged;
        // 비활성화 중 변경된 값도 재활성화 시 실제 이동·회피 컴포넌트와 맞춘다.
        ApplyMovementStats(_playerStat.CurrentStats);

        _playerQuickslot.Changed += HandleQuickslotsChanged;

        SubscribeSkillTreeRuntime();
        if (_hasStarted) RefreshItemStatModifiers();
    }

    private void Start()
    {
        _hasStarted = true;
        // 모든 컴포넌트의 Awake가 끝난 뒤 생성된 스킬 트리 Runtime 구독을 보장한다.
        SubscribeSkillTreeRuntime();
        // 초기 보유 목록도 모든 컴포넌트의 Awake가 끝난 뒤 스탯과 동기화한다.
        RefreshItemStatModifiers();
    }

    private void OnDisable()
    {
        _itemEffects?.Clear();
        _hasPendingGuardChange = false;
        _distanceSinceFootstep = 0f;
        _interactionCandidates.Clear();
        SetInteractionTarget(null);

        _playerAnimation.ClearHitStop();

        _hurtbox.DamageRequested -= HandleDamageRequested;

        // Manager 비활성화 동안에는 피해 수신을 차단한다.
        _hurtbox.SetDamageEnabled(false);

        _playerState.ActionStateChanged -= HandlePlayerActionStateChanged;

        _attackHitbox.EndAttack();

        _playerControl.LockOnPressed -= HandleLockOnPressed;
        _playerControl.DodgePressed -= HandleDodgePressed;
        _playerControl.AttackPressed -= HandleAttackPressed;
        _playerControl.GuardChanged -= HandleGuardChanged;
        _playerControl.PlayerWindowPressed -= HandlePlayerWindowPressed;
        _playerControl.OptionWindowPressed -= HandleOptionWindowPressed;
        _playerControl.InteractPressed -= HandleInteractPressed;
        _playerControl.QuickslotPressed -= HandleQuickslotPressed;

        _playerStat.StatsChanged -= HandlePlayerStatsChanged;

        _playerQuickslot.Changed -= HandleQuickslotsChanged;

        UnsubscribeSkillTreeRuntime();
    }

    private void OnDestroy()
    {
        _playerInventory.InventoryChanged -= HandleInventoryChanged;
        _playerInventory.ItemAcquired -= HandleItemAcquired;
        _playerInventory.EquipmentChanged -= HandleEquipmentChanged;
    }

    public void BeginHitStop(float duration)
    {
        if (!isActiveAndEnabled) return;

        // 하위 컴포넌트 호출은 PlayerManager가 중개한다.
        _playerAnimation.BeginHitStop(duration);
        RefreshInteractionTarget();
    }

    public void OnAttackHit(float energyRecoveryOnHit, bool wasBlocked)
    {
        if (!isActiveAndEnabled || _playerStat.CurrentStats.CurrentHealth <= 0f) return;
        if (_playerState.ActionState == PlayerActionState.Dead) return;
        PlayerStatSnapshot stats = _playerStat.CurrentStats;
        if (energyRecoveryOnHit > 0f && !float.IsNaN(energyRecoveryOnHit) && !float.IsInfinity(energyRecoveryOnHit))
        {
            // 확정된 일반 타격은 전량, 가드로 막힌 타격은 최종 회복량의 50%를 회복한다.
            _playerStat.RestoreEnergy(energyRecoveryOnHit * (wasBlocked ? 0.5f : 1f));
        }
        // 기본 회복량이 0이어도 확정된 타격(가드에 막힌 타격 포함)은 패시브를 발동한다.
        _itemEffects?.Trigger(ItemEffectTrigger.AttackHit, stats);
    }

    public void NotifyEnemyKilled()
    {
        // EnemyManager의 사망 중복 방지와 처치자 판정이 끝난 뒤에만 전달받는다.
        _itemEffects?.Trigger(ItemEffectTrigger.EnemyKilled, CurrentPlayerStats);
    }

    public void NotifyRoomCleared()
    {
        // 전투방 전체 편성이 끝났다는 StageManager의 확정 알림만 사용한다.
        _itemEffects?.Trigger(ItemEffectTrigger.RoomCleared, CurrentPlayerStats);
    }

    private void HandlePlayerActionStateChanged(PlayerActionState actionState)
    {
        if (actionState == PlayerActionState.Dead)
        {
            _itemEffects?.Clear();
        }
        RefreshInteractionTarget();

        // 사망 후에는 추가 타격과 히트스탑을 받지 않는다.
        _hurtbox.SetDamageEnabled(actionState != PlayerActionState.Dodging && actionState != PlayerActionState.Dead);
    }

    private bool CanBlock(DamageInfo damageInfo)
    {
        if (_playerState.ActionState != PlayerActionState.Guarding) return false;

        // 가드 종료 모션 중에는 공격을 막지 않는다.
        if (!_playerControl.IsGuardRequested || !damageInfo.Attacker) return false;

        return CombatUtility.IsAttackerInFront(transform, damageInfo.Attacker.transform);
    }

    private void HandleDamageRequested(DamageInfo damageInfo)
    {
        if (!isActiveAndEnabled) return;
        if (_playerState.ActionState == PlayerActionState.Dead) return;
        // 피격 직전의 기본값·아이템 보정이 반영된 회복량을 해당 타격 전체에서 사용한다.
        PlayerStatSnapshot stats = _playerStat.CurrentStats;
        if (stats.CurrentHealth <= 0f) return;
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

            // 가드 브레이크도 가드로 막은 타격이므로 가드 회복량만 적용한다.
            PlayerStatSnapshot guardStats = _playerStat.CurrentStats;
            RestoreEnergyOnReceivedHit(stats.EnergyRecoveryOnGuard);
            _itemEffects?.Trigger(ItemEffectTrigger.GuardSucceeded, guardStats);
            damageInfo.ConfirmHit(true);
            return;
        }

        // 가드하지 않은 대상에게 가드 강인도 피해만 있는 공격은 적용하지 않는다.
        if (damageInfo.Damage <= 0f && damageInfo.PoiseDamage <= 0f) return;

        damageInfo.ApplyHitStop(this);
        // 일반 명중이 확정된 위치에서 재생하며, 치명타 이후의 비활성화나 위치 변경보다 먼저 요청한다.
        if (damageInfo.HitSound)
        {
            AudioManager audioManager = AudioManager.Instance;
            if (audioManager) audioManager.PlaySfx(damageInfo.HitSound, transform.position);
        }

        // 강인도를 먼저 처리하여 치명타에서도 해당 공격의 감소량을 반영한다.
        _playerStat.TakePoiseDamage(damageInfo.PoiseDamage);

        // 피격 시점의 방어력을 체력 피해에만 적용한다.
        float damage = CombatUtility.CalculateDamageAfterDefense(damageInfo.Damage, _playerStat.CurrentStats.DefensePower);
        _playerStat.TakeDamage(damage);

        // 치명적인 공격에서는 경직보다 기존 사망 처리를 우선한다.
        if (_playerStat.CurrentStats.CurrentHealth > 0f)
        {
            if (damageInfo.PoiseDamage > 0f && _playerStat.CurrentPoise <= 0f)
            {
                BeginPoiseReaction(false, damageInfo.Attacker);
            }

            // 일반 피격에서 살아남은 경우에만 피격 회복량을 적용한다.
            PlayerStatSnapshot damagedStats = _playerStat.CurrentStats;
            RestoreEnergyOnReceivedHit(stats.EnergyRecoveryOnDamageTaken);
            _itemEffects?.Trigger(ItemEffectTrigger.DamageTaken, damagedStats);
            damageInfo.ConfirmHit(false);
            return;
        }

        HandleDied();
        damageInfo.ConfirmHit(false);
    }

    private void RestoreEnergyOnReceivedHit(float recoveryAmount)
    {
        if (recoveryAmount <= 0f || float.IsNaN(recoveryAmount) || float.IsInfinity(recoveryAmount)) return;
        // 피해·경직 알림 중 사망하거나 비활성화된 경우에도 회복을 지급하지 않는다.
        if (!isActiveAndEnabled || _playerStat.CurrentStats.CurrentHealth <= 0f) return;
        if (_playerState.ActionState == PlayerActionState.Dead) return;

        _playerStat.RestoreEnergy(recoveryAmount);
    }

    private void HandleDied()
    {
        _itemEffects?.Clear();
        if (_playerState.ActionState == PlayerActionState.Dead) return;

        // 체력 변경 알림과 피격 경로가 겹쳐도 사망은 한 번만 처리한다.
        _playerState.SetActionState(PlayerActionState.Dead);
        _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
        _poiseReaction.Reset();
        _hasPendingGuardChange = false;
        _playerMove.EndDodge();

        _attackHitbox.EndAttack();

        _playerAttack.EndAttack();

        _playerLockOn.ReleaseTarget();
        NotifyLockOnTargetChanged();

        _playerCamera.ClearLockOnTarget();
        if (!_playerAnimation.TryPlayDeath())
        {
            Debug.LogWarning("플레이어 Animator의 Dead 상태 또는 사망 클립이 없습니다.", this);
        }
    }

    private void BeginPoiseReaction(bool isGuardBreak, MonoBehaviour attacker)
    {
        if (_playerState.ActionState == PlayerActionState.Dead || IsInPoiseReaction) return;

        PlayerStatSnapshot stats = _playerStat.CurrentStats;
        if (stats.CurrentHealth <= 0f) return;

        // 경직 시작 시점의 방향만 전달하며, 반응 도중에는 모션을 바꾸지 않는다.
        bool isHitFromBehind = !isGuardBreak && CombatUtility.IsAttackerBehind(transform, attacker ? attacker.transform : null);

        // 공격 판정과 콤보 예약을 함께 취소한다.
        _attackHitbox.EndAttack();

        _playerAttack.EndAttack();

        // 회피 이동을 중단하되 이미 소비한 회피 횟수는 반환하지 않는다.
        _playerMove.EndDodge();

        _hasPendingGuardChange = false;
        float duration = isGuardBreak ? stats.GuardBreakDuration : stats.StaggerDuration;
        float moveDistance = isGuardBreak ? stats.GuardBreakMoveDistance : stats.StaggerMoveDistance;
        _poiseReaction.Begin(transform, attacker?.transform, duration, moveDistance);

        _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
        _playerState.SetActionState(isGuardBreak ? PlayerActionState.GuardBroken : PlayerActionState.Stunned);

        // 애니메이션이 누락되어도 행동 제한과 복귀 타이머는 유지한다.
        if (_playerAnimation.TryPlayPoiseReaction(isGuardBreak, isHitFromBehind))
        {
            Debug.LogWarning("플레이어 Animator 또는 Stagger/GuardBreak 상태가 없습니다.", this);
            return;
        }

        _playerAnimation.CompletePoiseReaction();
    }

    private void UpdatePoiseReaction(float deltaTime)
    {
        Vector3 displacement = _poiseReaction.Advance(deltaTime);

        _playerMove.MovePoiseReaction(displacement, deltaTime);

        _playerAnimation.SetSprinting(false);
        _playerAnimation.SetMovement(Vector2.zero, deltaTime);

        if (_poiseReaction.IsRunning) return;

        // Free로 전환하기 전에 현재 반응 종류에 맞는 강인도를 복구한다.
        _playerStat.RestorePoiseAfterReaction(_playerState.ActionState == PlayerActionState.GuardBroken);

        _playerAnimation.CompletePoiseReaction();

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
        // 직접 체력 변경이나 재활성화 시에도 사망 상태를 보장한다.
        if (_playerStat.CurrentStats.CurrentHealth <= 0f)
        {
            HandleDied();
        }

        _itemEffects?.Tick(Time.deltaTime);

        _playerAnimation.UpdateHitStop();

        // 히트스톱과 피격 반응 중에는 회복량과 회복 대기 시간 모두 진행하지 않는다.
        if (_playerState.ActionState != PlayerActionState.Dead && !IsHitStopped && !IsInPoiseReaction)
        {
            _playerStat.UpdatePoiseRecovery(Time.deltaTime);
        }

        if (!IsHitStopped)
        {
            _playerDodge.UpdateRecharge(Time.deltaTime);
        }

        if (_playerLockOn.TryReleaseInvalidTarget())
        {
            // 락온 타깃이 유효하지 않게 되면 변경 사실을 전달한다.
            NotifyLockOnTargetChanged();
        }

        // 공격이 취소되거나 다른 행동 상태로 바뀌면 판정을 종료한다.
        if (_playerState.ActionState != PlayerActionState.Attacking)
        {
            _attackHitbox.EndAttack();
        }

        _playerCamera.SetLookInput(_playerControl.LookInput, _playerControl.IsLookInputDelta);

        // Look 입력은 계속 전달하고 사망 후 캐릭터 행동 갱신만 차단한다.
        if (_playerState.ActionState == PlayerActionState.Dead)
        {
            _distanceSinceFootstep = 0f;
            _playerCamera.ClearLockOnTarget();
            return;
        }

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
        if (IsHitStopped)
        {
            _distanceSinceFootstep = 0f;
            return;
        }

        // 피격 반응을 먼저 처리하여 기존 이동·공격·가드 갱신을 막는다.
        if (IsInPoiseReaction)
        {
            _distanceSinceFootstep = 0f;
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

        // 이번 프레임의 일반 이동 거리만 전달하여 행동 종료 시 이전 이동 거리를 사용하지 않는다.
        float footstepMoveDistance = 0f;
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
            footstepMoveDistance = _playerMove.HorizontalMoveDistance;
        }
        else if (_playerState.CanMove && lockOnTarget)
        {
            _playerMove.MoveLockedOn(_playerControl.MoveInput, lockOnTarget.AimPosition, Time.deltaTime);
            footstepMoveDistance = _playerMove.HorizontalMoveDistance;
        }
        else if (_playerState.CanMove)
        {
            _playerMove.Move(_playerControl.MoveInput, _playerCamera.YawRotation, Time.deltaTime);
            footstepMoveDistance = _playerMove.HorizontalMoveDistance;
        }

        UpdateFootsteps(footstepMoveDistance);

        _playerAnimation.SetSprinting(_playerState.LocomotionState == PlayerLocomotionState.Sprinting);
        _playerAnimation.SetMovement(_playerMove.LocalMoveDirection, Time.deltaTime);
    }

    private void LateUpdate()
    {
        // 이동과 문 개방 갱신 뒤 다시 판정하므로 입력이 없어도 안내가 숨겨지거나 복구된다.
        RefreshInteractionTarget();
    }

    public void SetMaxDodgeCount(int maxDodgeCount)
    {
        if (!_hasCachedPlayerComponents) return;

        // 스킬 등의 변경은 기본값에 반영하여 기존 아이템 보정이 유지되게 한다.
        _playerStat.SetBaseMaxDodgeCount(maxDodgeCount);
        ApplyMovementStats(_playerStat.CurrentStats);
    }

    public void SetGameplayInputEnabled(bool isEnabled)
    {
        _isGameplayInputEnabled = isEnabled;
        if (_hasCachedPlayerComponents)
        {
            // UI의 입력 제한 요청을 PlayerControl로 중개한다.
            _playerControl.SetGameplayInputEnabled(isEnabled);
        }

        RefreshInteractionTarget();
    }

    public SkillTreeUnlockResult TryUnlockSkillTreeNode(int nodeId)
    {
        if (!_hasCachedPlayerComponents) return SkillTreeUnlockResult.RuntimeNotInitialized;

        return _playerSkillTree.TryUnlockNode(nodeId);
    }

    public void AddExperience(int amount)
    {
        // 처치 보상과 아이템 경험치는 같은 성장 경로로 전달하고 기존 스탯 변경 알림으로 UI를 갱신한다.
        _playerStat.AddExperience(amount);
    }

    public void AddMoney(int amount)
    {
        // 외부 보상 요청은 Manager를 거쳐 PlayerStat의 보유 금액에 반영한다.
        _playerStat.AddMoney(amount);
    }

    public bool TrySpendMoney(int amount)
    {
        if (!isActiveAndEnabled || !_hasCachedPlayerComponents) return false;

        // 상점 등 외부 시스템은 하위 PlayerStat을 직접 참조하지 않고 Manager로 비용을 지불한다.
        return _playerStat.TrySpendMoney(amount);
    }

    public IReadOnlyList<ItemDefinition> GetInventoryItems(ItemType itemType)
    {
        // 보유 데이터는 Inventory가 소유하고 외부 조회는 기존 Manager API로 중개한다.
        return _hasCachedPlayerComponents ? _playerInventory.GetInventoryItems(itemType) : Array.Empty<ItemDefinition>();
    }

    public int GetConsumableCount(ItemDefinition item)
    {
        return _hasCachedPlayerComponents ? _playerInventory.GetConsumableCount(item) : 0;
    }

    public bool IsConsumableStackFull(ItemDefinition item)
    {
        // 상점 UI는 하위 Inventory를 직접 참조하지 않고 Manager를 통해 구매 제한을 조회한다.
        return _hasCachedPlayerComponents && _playerInventory.IsConsumableStackFull(item);
    }

    public bool TryUseItem(int inventoryIndex, ItemDefinition expectedItem)
    {
        if (_isUsingItem || !isActiveAndEnabled || !_hasCachedPlayerComponents || !expectedItem
            || expectedItem.ItemType != ItemType.Consumable) return false;
        if (_playerStat.CurrentStats.CurrentHealth <= 0f
            || _playerState.ActionState == PlayerActionState.Dead) return false;

        IReadOnlyList<ItemEffectData> effects = expectedItem.UseEffects;
        // 즉시 실행하거나 시간 효과를 시작할 수 있는 효과가 없으면 수량을 차감하지 않는다.
        PlayerInventory inventory = _playerInventory;
        if (_itemEffects == null || !_itemEffects.CanApplyAny(effects)
            || !inventory.TryConsumeConsumable(inventoryIndex, expectedItem)) return false;

        _isUsingItem = true;
        try
        {
            _itemEffects.Apply(expectedItem, effects);
        }
        finally
        {
            try
            {
                // 슬롯 재대여는 효과 실행 후에 알리고 알림 중에도 중복 사용을 막는다.
                inventory.NotifyConsumableCountChanged();
            }
            finally
            {
                _isUsingItem = false;
            }
        }

        return true;
    }

    public ItemDefinition GetQuickslotItem(int slotNumber)
    {
        return _hasCachedPlayerComponents ? _playerQuickslot.GetItem(slotNumber) : null;
    }

    public bool TryUseQuickslot(int slotNumber)
    {
        if (!isActiveAndEnabled || !_isGameplayInputEnabled || !_hasCachedPlayerComponents || !_playerQuickslot.isActiveAndEnabled) return false;

        ItemDefinition item = _playerQuickslot.GetItem(slotNumber);
        if (!item || GetConsumableCount(item) <= 0) return false;

        // 소모로 목록 인덱스가 바뀔 수 있으므로 등록된 Definition의 현재 보유 위치를 다시 확인한다.
        IReadOnlyList<ItemDefinition> items = GetInventoryItems(ItemType.Consumable);
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == item) return TryUseItem(i, item);
        }

        return false;
    }

    private void HandleQuickslotPressed(int slotNumber)
    {
        TryUseQuickslot(slotNumber);
    }

    public int GetQuickslotNumber(ItemDefinition item)
    {
        return _hasCachedPlayerComponents ? _playerQuickslot.GetSlotNumber(item) : 0;
    }

    public bool CanRegisterQuickslot(ItemDefinition item)
    {
        // UI가 미보유 항목이나 확인용 목록을 등록하지 못하도록 소유 여부를 Manager에서 검증한다.
        return isActiveAndEnabled && _hasCachedPlayerComponents && _playerQuickslot.isActiveAndEnabled && GetConsumableCount(item) > 0;
    }

    public bool TryRegisterQuickslot(int slotNumber, ItemDefinition item, bool toggleIfSame = false)
    {
        return CanRegisterQuickslot(item) && _playerQuickslot.TryRegister(slotNumber, item, toggleIfSame);
    }

    public bool TryUnregisterQuickslot(ItemDefinition item)
    {
        // 수량이 0이 된 항목도 등록 해제는 가능하게 하며 등록 정보만 변경한다.
        return isActiveAndEnabled && _hasCachedPlayerComponents && _playerQuickslot.TryUnregister(item);
    }

    private void HandleQuickslotsChanged()
    {
        QuickslotsChanged?.Invoke();
    }

    public ItemDefinition GetEquippedItem(EquipmentSlot equipmentSlot)
    {
        return _hasCachedPlayerComponents ? _playerInventory.GetEquippedItem(equipmentSlot) : null;
    }

    public bool CanToggleEquipment(int inventoryIndex, ItemDefinition expectedItem)
    {
        return isActiveAndEnabled && _hasCachedPlayerComponents && _playerInventory.CanToggleEquipment(inventoryIndex, expectedItem);
    }

    public bool IsEquipmentEquipped(int inventoryIndex, ItemDefinition expectedItem)
    {
        return _hasCachedPlayerComponents && _playerInventory.IsEquipmentEquipped(inventoryIndex, expectedItem);
    }

    public bool TryToggleEquipment(int inventoryIndex, ItemDefinition expectedItem)
    {
        return isActiveAndEnabled && _hasCachedPlayerComponents && _playerInventory.TryToggleEquipment(inventoryIndex, expectedItem);
    }

    public bool TryAddItem(ItemDefinition item)
    {
        return isActiveAndEnabled && _hasCachedPlayerComponents && _playerInventory.TryAddItem(item);
    }

    public bool TryPurchaseItem(ItemDefinition item)
    {
        if (!isActiveAndEnabled || !_hasCachedPlayerComponents || !item || item.BasePrice < 0) return false;

        // 최대 보유 수량을 포함해 추가 가능 여부를 먼저 확인한 뒤 결제한다. Inventory는 PlayerStat을 직접 참조하지 않는다.
        PlayerInventory inventory = _playerInventory;
        if (!inventory.TryPrepareItemAddition(item, out ItemType itemType) || !TrySpendMoney(item.BasePrice)) return false;

        // Money 알림에서 활성 상태가 바뀌어도 이미 결제한 아이템은 확정한 목록에 추가한다.
        inventory.CommitItemAddition(item, itemType);
        return true;
    }

    private void HandleInventoryChanged(ItemType itemType)
    {
        // 외부에 보유 변경을 알리기 전에 장착 장비와 유물의 최종 스탯을 먼저 반영한다.
        if (itemType == ItemType.Relic) RefreshItemStatModifiers();

        if (itemType == ItemType.Consumable)
        {
            // 마지막 소모품을 사용한 경로와 관계없이 등록을 해제해 HUD와 인벤토리의 아이콘을 함께 비운다.
            for (int slotNumber = 1; slotNumber <= PlayerQuickslot.SlotCount; slotNumber++)
            {
                ItemDefinition item = _playerQuickslot.GetItem(slotNumber);
                if (item && GetConsumableCount(item) <= 0) _playerQuickslot.TryUnregister(item);
            }
        }

        // 기존 UI와 HUD가 하위 컴포넌트를 직접 구독하지 않도록 변경 알림을 중개한다.
        InventoryChanged?.Invoke(itemType);
    }

    private void RefreshItemStatModifiers()
    {
        // 장착·보유 데이터 조회는 Manager가 중개하고 효과의 동기화 규칙은 실행기가 관리한다.
        for (int slotIndex = (int)EquipmentSlot.Head; slotIndex < _equippedItemsForStats.Length; slotIndex++)
        {
            _equippedItemsForStats[slotIndex] = _playerInventory.GetEquippedItem((EquipmentSlot)slotIndex);
        }
        _itemEffects.SynchronizeSources(_playerInventory.GetInventoryItems(ItemType.Relic), _equippedItemsForStats);

        // 보유·장착 알림은 비활성화 중에도 받으므로 StatsChanged 구독이 없을 때 직접 동기화한다.
        if (!isActiveAndEnabled) ApplyMovementStats(_playerStat.CurrentStats);
        else HandlePlayerStatsChanged(_playerStat.CurrentStats);
    }

    // 실행기는 이 계약만 사용하며 Stat·SkillTree 등 하위 컴포넌트에는 직접 접근하지 않는다.
    bool IPlayerItemEffectHost.CanRunItemEffects => isActiveAndEnabled && _hasCachedPlayerComponents
        && _playerStat.CurrentStats.CurrentHealth > 0f && _playerState.ActionState != PlayerActionState.Dead;

    int IPlayerItemEffectHost.RemainingSkillPointCapacity
    {
        get
        {
            SkillTreeRuntime runtime = SkillTreeRuntime;
            return runtime != null ? int.MaxValue - runtime.AvailablePoints : 0;
        }
    }

    void IPlayerItemEffectHost.RestoreHealth(float amount)
    {
        _playerStat.RestoreHealth(amount);
    }

    void IPlayerItemEffectHost.RestoreEnergy(float amount)
    {
        _playerStat.RestoreEnergy(amount);
    }

    void IPlayerItemEffectHost.RebuildItemStatModifiers(IReadOnlyList<ItemDefinition> relicItems, IReadOnlyList<ItemDefinition> equippedItems, IReadOnlyList<StatModifier> passiveModifiers)
    {
        _playerStat.RebuildItemStatModifiers(relicItems, equippedItems, passiveModifiers);
    }

    void IPlayerItemEffectHost.RebuildTimedStatModifiers(IReadOnlyList<StatModifier> modifiers)
    {
        _playerStat.RebuildTimedStatModifiers(modifiers);
    }

    void IPlayerItemEffectHost.RebuildPassiveStatModifiers(IReadOnlyList<StatModifier> modifiers)
    {
        _playerStat.RebuildPassiveStatModifiers(modifiers);
    }

    private void HandleItemAcquired(ItemDefinition item)
    {
        if (item && item.ItemType == ItemType.Relic)
        {
            // 보유 스탯이 반영된 뒤 실제 획득마다 한 번 실행한다. 같은 유물의 추가 획득도 별도로 발동한다.
            _itemEffects?.Apply(item, item.AcquireEffects);
        }

        // 획득 효과까지 반영한 상태를 HUD 등 외부 구독자에게 전달한다.
        ItemAcquired?.Invoke(item);
    }

    private void HandleEquipmentChanged(EquipmentSlot equipmentSlot)
    {
        // 장착·해제·교체 결과를 스탯에 반영한 뒤 UI 등에 장비 변경을 알린다.
        RefreshItemStatModifiers();
        EquipmentChanged?.Invoke(equipmentSlot);
    }

    public void AddSkillPoints(int amount)
    {
        if (!_hasCachedPlayerComponents) return;

        // 외부 시스템의 포인트 지급 요청을 PlayerSkillTree로 중개한다.
        _playerSkillTree.AddPoints(amount);
    }

    private void HandleLockOnPressed()
    {
        if (_playerState.ActionState == PlayerActionState.Dead) return;

        _playerLockOn.ToggleTarget();
        NotifyLockOnTargetChanged();
    }

    public void RegisterInteraction(IInteractable interaction)
    {
        if (!isActiveAndEnabled || !IsInteractionActive(interaction) || _interactionCandidates.Contains(interaction))
            return;

        _interactionCandidates.Add(interaction);
        RefreshInteractionTarget();
    }

    public void UnregisterInteraction(IInteractable interaction)
    {
        if (_interactionCandidates.Remove(interaction))
        {
            RefreshInteractionTarget();
        }
    }

    private void RefreshInteractionTarget()
    {
        bool canInteract = isActiveAndEnabled && _isGameplayInputEnabled;

        IInteractable nearestInteraction = null;
        float nearestDistance = float.PositiveInfinity;

        for (int index = _interactionCandidates.Count - 1; index >= 0; index--)
        {
            IInteractable interaction = _interactionCandidates[index];

            // 인터페이스 참조에서도 Unity의 파괴된 오브젝트와 놓친 범위 이탈을 검사한다.
            if (!IsInteractionActive(interaction) || !interaction.IsPlayerInRange(this))
            {
                _interactionCandidates.RemoveAt(index);
                continue;
            }

            if (!canInteract || !interaction.CanInteract(this)) continue;

            float distance = (interaction.InteractionPosition - transform.position).sqrMagnitude;
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearestInteraction = interaction;
        }

        SetInteractionTarget(nearestInteraction);
    }

    private static bool IsInteractionActive(IInteractable interaction)//개선??
    {
        return interaction is MonoBehaviour component && component && component.isActiveAndEnabled;
    }

    private void SetInteractionTarget(IInteractable interaction)//?
    {
        _currentInteraction = interaction;
        string interactionText = interaction != null ? interaction.InteractionText : string.Empty;
        if (interaction != null && string.IsNullOrEmpty(interactionText)) interactionText = "상호작용";
        if (string.Equals(CurrentInteractionText, interactionText, StringComparison.Ordinal)) return;

        CurrentInteractionText = interactionText;
        InteractionPromptChanged?.Invoke(interactionText);
    }

    private void HandleInteractPressed()
    {
        // 입력 시에도 동일한 선택기를 사용하고, 실행 직후 변경된 가능 여부를 UI에 반영한다.
        RefreshInteractionTarget();
        if (_currentInteraction == null) return;

        _currentInteraction.TryInteract(this);
        RefreshInteractionTarget();
    }

    private void HandleOptionWindowPressed()
    {
        // 하위 입력 컴포넌트의 요청을 UI 입력 조정자에게 중개한다.
        OptionWindowRequested?.Invoke();
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

        if (!_playerState.CanDodge || !_playerDodge.CanDodge) return;

        LockOnTarget lockOnTarget = _playerLockOn.CurrentTarget;

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
        if (_isStartingLethalArt) return;

        // 일반 공격의 콤보 입력 구간이나 히트스톱보다 필살기 전환을 먼저 처리한다.
        if (inputType == AttackInputType.Heavy && TryGetAvailableLethalArt(out LethalArtDefinition lethalArt, out _))
        {
            TryStartLethalArt(lethalArt);
            return;
        }

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

    private bool CanStartLethalArt()
    {
        if (!isActiveAndEnabled || !_isGameplayInputEnabled) return false;
        if (_playerAttack.IsLethalArtActive) return false;

        // 일반 공격 중의 히트스톱은 전환할 수 있지만 회피·가드·피격 반응 등은 기존 제한을 유지한다.
        if (_playerState.ActionState == PlayerActionState.Attacking) return _playerAttack.IsAttacking;
        return _playerState.CanUseNormalAttack && !IsHitStopped;
    }

    private bool TryStartLethalArt(LethalArtDefinition lethalArt)
    {
        if (_isStartingLethalArt || !lethalArt || !lethalArt.Attack || !CanStartLethalArt()) return false;

        _isStartingLethalArt = true;
        try
        {
            // 결제에 실패하면 진행 중인 일반 공격·콤보 예약·히트박스는 그대로 유지한다.
            if (!_playerStat.TryConsumeEnergy(lethalArt.EnergyCost)) return false;

            // 스탯 알림에서 사망이나 비활성화가 발생한 경우에는 전환하지 않고 비용을 돌려준다.
            if (!CanStartLethalArt() || !_playerAttack.TryBeginLethalArt(lethalArt.Attack))
            {
                _playerStat.RestoreEnergy(lethalArt.EnergyCost);
                return false;
            }

            _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
            _playerState.SetActionState(PlayerActionState.Attacking);
            // 공통 시작 처리에서 이전 히트박스를 종료하고 필살기 판정으로 교체한다.
            BeginAttackPresentation(lethalArt.Attack, true);
            return true;
        }
        finally
        {
            _isStartingLethalArt = false;
        }
    }

    // UI에서도 로그·에너지 소모 없이 동일한 필살기와 대상을 조회할 수 있다.
    public bool TryGetAvailableLethalArt(out LethalArtDefinition lethalArt, out EnemyManager targetEnemy)//비용은...
    {
        lethalArt = null;
        targetEnemy = null;
        if (_isStartingLethalArt || _lethalArtRuntime == null || !CanStartLethalArt()) return false;

        PlayerStatSnapshot stats = _playerStat.CurrentStats;
        // 탐색 범위와 모든 대상의 판정에 사용할 플레이어 정보를 조회 시작 시 한 번만 묶는다.
        LethalArtPlayerContext player = new(in stats, transform.position, _playerState.LocomotionState == PlayerLocomotionState.Sprinting);
        if (!_lethalArtRuntime.TryGetSearchDistance(in player, out float searchDistance)) return false;

        LockOnTarget lockOnTarget = _playerLockOn.CurrentTarget;
        EnemyManager lockedEnemy = lockOnTarget ? lockOnTarget.EnemyManager : null;
        if (lockedEnemy && TrySelectLethalArtForTarget(lockedEnemy, in player, out lethalArt, out _))
        {
            targetEnemy = lockedEnemy;
            return true;
        }

        if (GameLayerUtility.EnemyMask == 0) return false;
        int candidateCount = FindLethalArtCandidates(player.Position, searchDistance);
        _lethalArtCheckedEnemies.Clear();
        if (lockedEnemy) _lethalArtCheckedEnemies.Add(lockedEnemy);
        float bestDistanceSquared = float.PositiveInfinity;
        for (int i = 0; i < candidateCount; i++)
        {
            Collider candidateCollider = _lethalArtCandidateBuffer[i];
            if (!candidateCollider) continue;

            EnemyManager enemy = candidateCollider.GetComponentInParent<EnemyManager>();
            if (!enemy || !_lethalArtCheckedEnemies.Add(enemy)) continue;
            if (!TrySelectLethalArtForTarget(enemy, in player, out LethalArtDefinition candidate, out float distanceSquared)) continue;
            // 같은 필살기를 사용할 수 있는 대상끼리는 더 가까운 적을 선택한다.
            if (!LethalArtRuntime.ShouldReplaceSelection(candidate, distanceSquared, lethalArt, bestDistanceSquared)) continue;

            lethalArt = candidate;
            targetEnemy = enemy;
            bestDistanceSquared = distanceSquared;
        }

        Array.Clear(_lethalArtCandidateBuffer, 0, candidateCount);
        _lethalArtCheckedEnemies.Clear();
        return lethalArt;
    }

    private bool TrySelectLethalArtForTarget(EnemyManager enemy, in LethalArtPlayerContext player, out LethalArtDefinition lethalArt, out float distanceSquared)
    {
        lethalArt = null;
        distanceSquared = 0f;
        if (!enemy.TryGetLethalArtTargetInfo(player.Position, out LethalArtTargetContext target)) return false;

        // 입력과 HUD가 동일한 대상 적의 현재 체력·거리·상태를 기준으로 판단한다.
        distanceSquared = target.DistanceSquared;
        return _lethalArtRuntime.TrySelect(in player, in target, out lethalArt);
    }

    private int FindLethalArtCandidates(Vector3 origin, float searchDistance)
    {
        int count = Physics.OverlapSphereNonAlloc(origin, searchDistance, _lethalArtCandidateBuffer, GameLayerUtility.EnemyMask, QueryTriggerInteraction.Collide);
        while (count == _lethalArtCandidateBuffer.Length)
        {
            // 밀집 상황에서도 후보를 누락하지 않으며, 확장한 버퍼는 다음 입력에서 그대로 사용한다.
            _lethalArtCandidateBuffer = new Collider[_lethalArtCandidateBuffer.Length * 2];
            count = Physics.OverlapSphereNonAlloc(origin, searchDistance, _lethalArtCandidateBuffer, GameLayerUtility.EnemyMask, QueryTriggerInteraction.Collide);
        }

        return count;
    }

    private void BeginAttackPresentation(AttackDefinition attackDefinition, bool isLethalArt = false)
    {
        // 각 공격·콤보 단계 시작 시 같은 스냅샷으로 피해와 재생 배율을 확정한다.
        PlayerStatSnapshot stats = _playerStat.CurrentStats;
        if (_attackHitbox)
        {
            _attackHitbox.BeginAttack(attackDefinition, stats.AttackPowerModifier, stats.PoiseDamageModifier,
                stats.GuardPoiseDamageModifier, stats.CalculateEnergyRecoveryOnHit(attackDefinition.BaseEnergyRecoveryOnHit));
        }

        // AnimationSpeed와의 곱셈은 공통 애니메이션에서 처리하며, 공격 중 스탯 변경은 다음 공격부터 반영한다.
        if (isLethalArt) _playerAnimation.PlayLethalArt(attackDefinition, stats.AttackSpeedMultiplier);
        else _playerAnimation.PlayAttack(attackDefinition, stats.AttackSpeedMultiplier);
        TryPlayAttackSound(false, 0f);
    }

    private void TryPlayAttackSound(bool hasNormalizedTime, float normalizedTime)
    {
        if (!_playerAnimation.TryConsumeAttackSound(hasNormalizedTime, normalizedTime, out SoundDefinition sound)) return;

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager) audioManager.PlaySfx(sound, transform.position);
    }

    private void HandleGuardChanged(bool isGuarding)
    {
        if (_playerState.ActionState == PlayerActionState.Dead) return;

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
        // 콤보 전환이나 공격 종료가 요청을 초기화하기 전에 이번 공격의 재생 시점을 확인한다.
        TryPlayAttackSound(hasNormalizedTime, normalizedTime);

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

    private void UpdateFootsteps(float horizontalMoveDistance)
    {
        // 정지, 벽에 막힌 미세 이동, 공중 및 일반 이동 이외의 행동은 보폭 누적도 초기화한다.
        if (!_footstepSound || !_isGameplayInputEnabled || !_playerMove.isActiveAndEnabled || !_playerMove.IsGrounded
            || !_playerState.CanMove || _playerState.LocomotionState == PlayerLocomotionState.Idle
            || Time.deltaTime <= 0f || horizontalMoveDistance <= 0.001f)
        {
            _distanceSinceFootstep = 0f;
            return;
        }

        float stepDistance = _playerState.LocomotionState == PlayerLocomotionState.Sprinting ? _sprintFootstepDistance : _walkFootstepDistance;
        stepDistance = Mathf.Max(0.1f, stepDistance);
        _distanceSinceFootstep += horizontalMoveDistance;
        if (_distanceSinceFootstep < stepDistance) return;

        // 프레임 지연으로 여러 보폭이 쌓여도 한 프레임에 한 번만 요청하고 재생 실패를 나중에 몰아서 재시도하지 않는다.
        _distanceSinceFootstep %= stepDistance;
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager) audioManager.PlayFootstep(_footstepSound, transform.position, isPlayer: true);
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
        // 조건부 스탯 계산 중 발생한 알림은 바깥 처리에서 최종 스냅샷으로 한 번만 전달한다.
        if (_itemEffects != null && _itemEffects.IsRefreshingStats) return;
        if (stats.CurrentHealth <= 0f)
        {
            HandleDied();
            // 사망 중 시간제 보정 해제가 추가 알림을 발생시켜도 이전 버프 수치로 UI를 덮어쓰지 않는다.
            stats = _playerStat.CurrentStats;
        }
        else
        {
            _itemEffects?.RefreshConditionalStats(stats);
            stats = _playerStat.CurrentStats;
        }
        // UI 등에 알리기 전에 계산된 최종값을 하위 컴포넌트에 먼저 전달한다.
        ApplyMovementStats(stats);
        // 플레이어 하위 컴포넌트의 변경 알림을 UI가 직접 구독하지 않도록 중개한다.
        PlayerStatsChanged?.Invoke(stats);
    }

    private void ApplyMovementStats(PlayerStatSnapshot stats)
    {
        _playerDodge.SetMaxDodgeCount(stats.MaxDodgeCount);
        _playerMove.SetSpeedMultiplier(stats.MoveSpeedMultiplier);
    }

    private void HandleSkillTreeNodeUnlocked(SkillTreeNodeRuntime node)
    {
        SkillTreeNodeUnlocked?.Invoke(node);
    }
}
