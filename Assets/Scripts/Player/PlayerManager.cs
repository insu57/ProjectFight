using System;
using System.Collections.Generic;
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
    private PlayerQuickslot _playerQuickslot;
    private SkillTreeRuntime _subscribedSkillTreeRuntime;
    private bool _hasPendingGuardChange;
    // 범위 안의 후보는 유지하고, 표시와 입력에 사용할 가장 가까운 유효 대상을 공유한다.
    private readonly List<IInteractable> _interactionCandidates = new(4);
    // 타입별 목록을 재사용하며 같은 장비·유물도 획득한 수만큼 별도 항목으로 보관한다.
    private readonly List<ItemDefinition> _equipmentItems = new(16);
    // 소모품 목록은 최초 획득 순서의 Definition만 보관하고 실제 수량은 Dictionary에서 관리한다.
    private readonly List<ItemDefinition> _consumableItems = new(16);
    private readonly Dictionary<ItemDefinition, int> _consumableItemCounts = new(16);
    private readonly List<ItemDefinition> _relicItems = new(16);

    // 장비 목록은 획득 순서로 추가만 하므로 그 인덱스로 같은 Definition의 개별 보유 장비를 구분한다.
    // None, 머리, 상의, 하의, 장갑, 신발 순서이며 -1은 해당 부위에 장착한 장비가 없다는 뜻이다.
    private readonly int[] _equippedEquipmentIndices = { -1, -1, -1, -1, -1, -1 };
    private IInteractable _currentInteraction;
    private bool _isGameplayInputEnabled = true;
    // 캐릭터마다 한 번 생성하고 피격 시 재사용한다.
    private readonly PoiseReactionRuntime _poiseReaction = new();

    private bool IsHitStopped => _playerAnimation && _playerAnimation.IsHitStopped;
    private bool IsInPoiseReaction => _playerState && (_playerState.ActionState == PlayerActionState.Stunned
        || _playerState.ActionState == PlayerActionState.GuardBroken);

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

    public SkillTreeRuntime SkillTreeRuntime => _playerSkillTree ? _playerSkillTree.Runtime : null;
    public PlayerStatSnapshot CurrentPlayerStats => _playerStat ? _playerStat.CurrentStats : default;
    public string CurrentInteractionText { get; private set; } = string.Empty;

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
        TryGetComponent(out _playerQuickslot);

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
            _playerControl.OptionWindowPressed += HandleOptionWindowPressed;
            _playerControl.InteractPressed += HandleInteractPressed;
        }

        if (_playerStat)
        {
            _playerStat.StatsChanged += HandlePlayerStatsChanged;
        }

        if (_playerQuickslot)
        {
            _playerQuickslot.Changed += HandleQuickslotsChanged;
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
        _interactionCandidates.Clear();
        SetInteractionTarget(null);

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
            _playerControl.OptionWindowPressed -= HandleOptionWindowPressed;
            _playerControl.InteractPressed -= HandleInteractPressed;
        }

        if (_playerStat)
        {
            _playerStat.StatsChanged -= HandlePlayerStatsChanged;
        }

        if (_playerQuickslot)
        {
            _playerQuickslot.Changed -= HandleQuickslotsChanged;
        }

        UnsubscribeSkillTreeRuntime();
    }

    public void BeginHitStop(float duration)
    {
        if (!isActiveAndEnabled || !_playerAnimation) return;

        // 하위 컴포넌트 호출은 PlayerManager가 중개한다.
        _playerAnimation.BeginHitStop(duration);
        RefreshInteractionTarget();
    }

    private void HandlePlayerActionStateChanged(PlayerActionState actionState)
    {
        RefreshInteractionTarget();
        if (!_hurtbox) return;

        // 사망 후에는 추가 타격과 히트스탑을 받지 않는다.
        _hurtbox.SetDamageEnabled(actionState != PlayerActionState.Dodging && actionState != PlayerActionState.Dead);
    }

    private bool CanBlock(DamageInfo damageInfo)
    {
        if (!_playerState || !_playerControl) return false;
        if (_playerState.ActionState != PlayerActionState.Guarding) return false;

        // 가드 종료 모션 중에는 공격을 막지 않는다.
        if (!_playerControl.IsGuardRequested || !damageInfo.Attacker) return false;

        return CombatUtility.IsAttackerInFront(transform, damageInfo.Attacker.transform);
    }

    private void HandleDamageRequested(DamageInfo damageInfo)
    {
        if (!isActiveAndEnabled || !_playerStat || !_playerState) return;
        if (_playerState.ActionState == PlayerActionState.Dead) return;
        if (_playerStat.CurrentStats.CurrentHealth <= 0f) return;
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

            return;
        }

        HandleDied();
    }

    private void HandleDied()
    {
        if (!_playerState || _playerState.ActionState == PlayerActionState.Dead) return;

        // 체력 변경 알림과 피격 경로가 겹쳐도 사망은 한 번만 처리한다.
        _playerState.SetActionState(PlayerActionState.Dead);
        _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
        _poiseReaction.Reset();
        _hasPendingGuardChange = false;
        _playerMove?.EndDodge();

        if (_attackHitbox)
        {
            _attackHitbox.EndAttack();
        }

        if (_playerAttack)
        {
            _playerAttack.EndAttack();
        }

        if (_playerLockOn)
        {
            _playerLockOn.ReleaseTarget();
            NotifyLockOnTargetChanged();
        }

        _playerCamera?.ClearLockOnTarget();
        if (!_playerAnimation || !_playerAnimation.TryPlayDeath())
        {
            Debug.LogWarning("플레이어 Animator의 Dead 상태 또는 사망 클립이 없습니다.", this);
        }
    }

    private void BeginPoiseReaction(bool isGuardBreak, MonoBehaviour attacker)
    {
        if (!_playerState || !_playerStat) return;
        if (_playerState.ActionState == PlayerActionState.Dead || IsInPoiseReaction) return;

        PlayerStatSnapshot stats = _playerStat.CurrentStats;
        if (stats.CurrentHealth <= 0f) return;

        // 경직 시작 시점의 방향만 전달하며, 반응 도중에는 모션을 바꾸지 않는다.
        bool isHitFromBehind = !isGuardBreak && CombatUtility.IsAttackerBehind(transform, attacker ? attacker.transform : null);

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
        float duration = isGuardBreak ? stats.GuardBreakDuration : stats.StaggerDuration;
        float moveDistance = isGuardBreak ? stats.GuardBreakMoveDistance : stats.StaggerMoveDistance;
        _poiseReaction.Begin(transform, attacker?.transform, duration, moveDistance);

        _playerState.SetLocomotionState(PlayerLocomotionState.Idle);
        _playerState.SetActionState(isGuardBreak ? PlayerActionState.GuardBroken : PlayerActionState.Stunned);

        // 애니메이션이 누락되어도 행동 제한과 복귀 타이머는 유지한다.
        if (_playerAnimation && _playerAnimation.TryPlayPoiseReaction(isGuardBreak, isHitFromBehind)) return;

        if (_playerAnimation)
        {
            _playerAnimation.CompletePoiseReaction();
        }

        Debug.LogWarning("플레이어 Animator 또는 Stagger/GuardBreak 상태가 없습니다.", this);
    }

    private void UpdatePoiseReaction(float deltaTime)
    {
        Vector3 displacement = _poiseReaction.Advance(deltaTime);

        if (_playerMove)
        {
            _playerMove.MovePoiseReaction(displacement, deltaTime);
        }

        if (_playerAnimation)
        {
            _playerAnimation.SetSprinting(false);
            _playerAnimation.SetMovement(Vector2.zero, deltaTime);
        }

        if (_poiseReaction.IsRunning) return;

        // Free로 전환하기 전에 현재 반응 종류에 맞는 강인도를 복구한다.
        _playerStat.RestorePoiseAfterReaction(_playerState.ActionState == PlayerActionState.GuardBroken);

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
        // 직접 체력 변경이나 재활성화 시에도 사망 상태를 보장한다.
        if (_playerStat && _playerStat.CurrentStats.CurrentHealth <= 0f) HandleDied();
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

        // Look 입력은 계속 전달하고 사망 후 캐릭터 행동 갱신만 차단한다.
        if (_playerState.ActionState == PlayerActionState.Dead)
        {
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

    private void LateUpdate()
    {
        // 이동과 문 개방 갱신 뒤 다시 판정하므로 입력이 없어도 안내가 숨겨지거나 복구된다.
        RefreshInteractionTarget();
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
        _isGameplayInputEnabled = isEnabled;
        if (_playerControl)
        {
            // UI의 입력 제한 요청을 PlayerControl로 중개한다.
            _playerControl.SetGameplayInputEnabled(isEnabled);
        }

        RefreshInteractionTarget();
    }

    public SkillTreeUnlockResult TryUnlockSkillTreeNode(int nodeId)
    {
        if (!_playerSkillTree)
        {
            return SkillTreeUnlockResult.RuntimeNotInitialized;
        }

        return _playerSkillTree.TryUnlockNode(nodeId);
    }

    public void AddExperience(int amount)
    {
        if (!_playerStat) return;

        // 처치 보상은 Manager를 통해 전달하고 기존 스탯 변경 알림으로 UI를 갱신한다.
        _playerStat.AddExperience(amount);
    }

    public void AddMoney(int amount)
    {
        if (!_playerStat) return;

        // 외부 보상 요청은 Manager를 거쳐 PlayerStat의 보유 금액에 반영한다.
        _playerStat.AddMoney(amount);
    }

    public bool TrySpendMoney(int amount)
    {
        if (!isActiveAndEnabled || !_playerStat) return false;

        // 상점 등 외부 시스템은 하위 PlayerStat을 직접 참조하지 않고 Manager로 비용을 지불한다.
        return _playerStat.TrySpendMoney(amount);
    }

    public IReadOnlyList<ItemDefinition> GetInventoryItems(ItemType itemType)
    {
        // 조회 때 필터링 목록을 할당하지 않고 해당 타입의 보유 목록을 그대로 전달한다.
        IReadOnlyList<ItemDefinition> items = GetInventoryList(itemType);
        return items ?? Array.Empty<ItemDefinition>();
    }

    public int GetConsumableCount(ItemDefinition item)
    {
        // 소모품 목록의 길이는 종류 수이므로 수량은 공유 Definition을 키로 바로 조회한다.
        return item && item.ItemType == ItemType.Consumable && _consumableItemCounts.TryGetValue(item, out int count) ? count : 0;
    }

    public ItemDefinition GetQuickslotItem(int slotNumber)
    {
        return _playerQuickslot ? _playerQuickslot.GetItem(slotNumber) : null;
    }

    public int GetQuickslotNumber(ItemDefinition item)
    {
        return _playerQuickslot ? _playerQuickslot.GetSlotNumber(item) : 0;
    }

    public bool CanRegisterQuickslot(ItemDefinition item)
    {
        // UI가 미보유 항목이나 확인용 목록을 등록하지 못하도록 소유 여부를 Manager에서 검증한다.
        return isActiveAndEnabled && _playerQuickslot && _playerQuickslot.isActiveAndEnabled && GetConsumableCount(item) > 0;
    }

    public bool TryRegisterQuickslot(int slotNumber, ItemDefinition item, bool toggleIfSame = false)
    {
        return CanRegisterQuickslot(item) && _playerQuickslot.TryRegister(slotNumber, item, toggleIfSame);
    }

    public bool TryUnregisterQuickslot(ItemDefinition item)
    {
        // 수량이 0이 된 항목도 등록 해제는 가능하게 하며 등록 정보만 변경한다.
        return isActiveAndEnabled && _playerQuickslot && _playerQuickslot.TryUnregister(item);
    }

    private void HandleQuickslotsChanged()
    {
        QuickslotsChanged?.Invoke();
    }

    public ItemDefinition GetEquippedItem(EquipmentSlot equipmentSlot)
    {
        int slotIndex = (int)equipmentSlot;
        if (slotIndex <= 0 || slotIndex >= _equippedEquipmentIndices.Length) return null;

        int inventoryIndex = _equippedEquipmentIndices[slotIndex];
        ItemDefinition item = inventoryIndex >= 0 && inventoryIndex < _equipmentItems.Count ? _equipmentItems[inventoryIndex] : null;
        return item && item.ItemType == ItemType.Equipment && item.EquipmentSlot == equipmentSlot ? item : null;
    }

    public bool CanToggleEquipment(int inventoryIndex, ItemDefinition expectedItem)
    {
        if (!expectedItem || expectedItem.ItemType != ItemType.Equipment || inventoryIndex < 0
            || inventoryIndex >= _equipmentItems.Count || _equipmentItems[inventoryIndex] != expectedItem) return false;

        // 미보유 항목, 오래된 슬롯 바인딩과 부위 미지정 장비는 장착 대상으로 사용하지 않는다.
        int slotIndex = (int)expectedItem.EquipmentSlot;
        return isActiveAndEnabled && slotIndex > 0 && slotIndex < _equippedEquipmentIndices.Length;
    }

    public bool IsEquipmentEquipped(int inventoryIndex, ItemDefinition expectedItem)
    {
        if (!expectedItem || expectedItem.ItemType != ItemType.Equipment || inventoryIndex < 0
            || inventoryIndex >= _equipmentItems.Count || _equipmentItems[inventoryIndex] != expectedItem) return false;

        int slotIndex = (int)expectedItem.EquipmentSlot;
        return slotIndex > 0 && slotIndex < _equippedEquipmentIndices.Length && _equippedEquipmentIndices[slotIndex] == inventoryIndex;
    }

    public bool TryToggleEquipment(int inventoryIndex, ItemDefinition expectedItem)
    {
        if (!CanToggleEquipment(inventoryIndex, expectedItem)) return false;

        EquipmentSlot equipmentSlot = expectedItem.EquipmentSlot;
        int slotIndex = (int)equipmentSlot;
        // 같은 보유 항목은 해제하고 다른 항목은 교체한다. 인벤토리와 스탯은 변경하지 않는다.
        _equippedEquipmentIndices[slotIndex] = _equippedEquipmentIndices[slotIndex] == inventoryIndex ? -1 : inventoryIndex;
        EquipmentChanged?.Invoke(equipmentSlot);
        return true;
    }

    private List<ItemDefinition> GetInventoryList(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Equipment => _equipmentItems,
            ItemType.Consumable => _consumableItems,
            ItemType.Relic => _relicItems,
            _ => null
        };
    }

    public bool TryAddItem(ItemDefinition item)
    {
        if (!isActiveAndEnabled || !item) return false;

        ItemType itemType = item.ItemType;
        List<ItemDefinition> items = GetInventoryList(itemType);
        if (items == null) return false;

        AddInventoryItem(item, itemType, items);
        return true;
    }

    public bool TryPurchaseItem(ItemDefinition item)
    {
        if (!isActiveAndEnabled || !_playerStat || !item || item.BasePrice < 0) return false;

        ItemType itemType = item.ItemType;
        List<ItemDefinition> items = GetInventoryList(itemType);
        // 추가 가능한 목록을 결제 전에 확정하여 잘못된 종류에는 비용을 청구하지 않는다.
        if (items == null || !TrySpendMoney(item.BasePrice)) return false;

        // Money 알림에서 활성 상태가 바뀌어도 이미 결제한 아이템은 확정한 목록에 추가한다.
        AddInventoryItem(item, itemType, items);
        return true;
    }

    private void AddInventoryItem(ItemDefinition item, ItemType itemType, List<ItemDefinition> items)
    {
        if (itemType == ItemType.Consumable)
        {
            // 기존 소모품은 수량만 늘려 같은 Definition이 목록과 그리드에 중복되지 않도록 한다.
            bool alreadyOwned = _consumableItemCounts.TryGetValue(item, out int count);
            _consumableItemCounts[item] = count + 1;
            if (!alreadyOwned)
            {
                items.Add(item);
            }
        }
        else
        {
            // 장비와 유물은 같은 공유 SO라도 획득한 수만큼 개별 항목을 유지한다.
            items.Add(item);
        }

        InventoryChanged?.Invoke(itemType);
        // 수량과 목록이 반영된 후 획득 알림을 보내 기존 HUD와 구매 흐름을 유지한다.
        ItemAcquired?.Invoke(item);
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
        if (!_playerState || _playerState.ActionState == PlayerActionState.Dead) return;
        if (!_playerLockOn) return;

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
            // 스탯은 Manager가 중개하며 콤보의 각 공격 시작 시 현재 공격력을 전달한다.
            float attackPower = _playerStat ? _playerStat.CurrentStats.AttackPower : 0f;
            _attackHitbox.BeginAttack(attackDefinition, attackPower);
        }

        _playerAnimation.PlayAttack(attackDefinition);
    }

    private void HandleGuardChanged(bool isGuarding)
    {
        if (!_playerState || !_playerAnimation) return;
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
        if (stats.CurrentHealth <= 0f) HandleDied();
        // 플레이어 하위 컴포넌트의 변경 알림을 UI가 직접 구독하지 않도록 중개한다.
        PlayerStatsChanged?.Invoke(stats);
    }

    private void HandleSkillTreeNodeUnlocked(SkillTreeNodeRuntime node)
    {
        SkillTreeNodeUnlocked?.Invoke(node);
    }
}
