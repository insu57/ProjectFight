using System.Collections.Generic;
using UnityEngine;

// 공격 기본 수치에 더할 값과 전체 배율을 함께 복사한다. 피해와 타격 회복량은 같은 합산 순서를 사용한다.
public readonly struct AttackDamageModifier
{
    public float AdditiveValue { get; }
    public float Multiplier { get; }

    public AttackDamageModifier(float additiveValue, float multiplier)
    {
        // 음수 Flat도 공격 기본 피해에 반영되도록 합산 전에는 추가 수치를 제한하지 않는다.
        AdditiveValue = additiveValue;
        Multiplier = Mathf.Max(0f, multiplier);
    }

    public float ApplyTo(float baseDamage)
    {
        return Mathf.Max(0f, Mathf.Max(0f, baseDamage) + AdditiveValue) * Multiplier;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public sealed class AttackHitbox : MonoBehaviour
{
    [SerializeField] private LayerMask _targetLayers;

    // 공격마다 재사용한다. 초기 용량을 넘으면 확장 시 할당이 발생할 수 있다.
    private readonly HashSet<int> _hitOwnerIds = new HashSet<int>(16);

    private BoxCollider _hitbox;
    private MonoBehaviour _owner;
    private AttackDefinition _currentAttack;
    private float _damage;
    private float _poiseDamage;
    private float _guardPoiseDamage;
    private float _energyRecoveryOnHit;

    private void Awake()
    {
        _hitbox = GetComponent<BoxCollider>();
        _hitbox.isTrigger = true;
        _hitbox.enabled = false;

        // 공격 박스는 물리 힘을 받지 않고 캐릭터의 자식으로 움직인다.
        Rigidbody hitboxBody = GetComponent<Rigidbody>();
        hitboxBody.isKinematic = true;
        hitboxBody.useGravity = false;
    }

    private void OnDisable()
    {
        EndAttack();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // 겹친 상태에서 다음 공격이 시작되는 경우도 검사한다.
        TryHit(other);
    }

    public void Initialize(MonoBehaviour owner)
    {
        _owner = owner;
    }

    public void BeginAttack(AttackDefinition attackDefinition, float attackPower = 0f, float poiseDamage = 0f, float guardPoiseDamage = 0f)
    {
        // 적 등 기존 호출은 추가 피해만 전달하므로 배율 1로 기존 동작을 유지한다.
        BeginAttack(attackDefinition, new AttackDamageModifier(Mathf.Max(0f, attackPower), 1f),
            new AttackDamageModifier(Mathf.Max(0f, poiseDamage), 1f), new AttackDamageModifier(Mathf.Max(0f, guardPoiseDamage), 1f));
    }

    public void BeginAttack(AttackDefinition attackDefinition, AttackDamageModifier attackPower,
        AttackDamageModifier poiseDamage, AttackDamageModifier guardPoiseDamage, float energyRecoveryOnHit = 0f)
    {
        // 콤보 전환 시 이전 공격의 박스를 먼저 끈다.
        EndAttack();

        if (!isActiveAndEnabled || !_hitbox || !_owner || !attackDefinition) return;

        _currentAttack = attackDefinition;

        // 공유 에셋은 유지하고 공격 기본 피해까지 합산한 뒤 각 피해의 배율을 한 번만 적용한다.
        _damage = attackPower.ApplyTo(attackDefinition.BaseDamage);
        _poiseDamage = poiseDamage.ApplyTo(attackDefinition.BasePoiseDamage);
        _guardPoiseDamage = guardPoiseDamage.ApplyTo(attackDefinition.BaseGuardPoiseDamage);
        // PlayerManager에서 확정한 회복량을 복사해 공격 도중 장비나 스탯이 변해도 유지한다.
        _energyRecoveryOnHit = Mathf.Max(0f, energyRecoveryOnHit);
        _hitbox.size = attackDefinition.HitboxSize;
        _hitbox.center = attackDefinition.HitboxOffset;
    }

    public void UpdateHitbox(bool hasNormalizedTime, float normalizedTime)
    {
        if (!_hitbox) return;

        // 애니메이션 진행도를 확인할 수 없으면 판정을 비활성화한다.
        bool shouldEnable = isActiveAndEnabled
            && _owner
            && _owner.isActiveAndEnabled
            && _currentAttack != null
            && hasNormalizedTime
            && normalizedTime >= _currentAttack.HitStartNormalizedTime
            && normalizedTime < _currentAttack.HitEndNormalizedTime;

        if (_hitbox.enabled != shouldEnable)
        {
            _hitbox.enabled = shouldEnable;
        }
    }

    public void EndAttack()
    {
        _currentAttack = null;
        _damage = 0f;
        _poiseDamage = 0f;
        _guardPoiseDamage = 0f;
        _energyRecoveryOnHit = 0f;
        _hitOwnerIds.Clear();

        if (_hitbox)
        {
            _hitbox.enabled = false;
        }
    }

    private void TryHit(Collider other)
    {
        if (!isActiveAndEnabled || !_hitbox || !_hitbox.enabled) return;
        if (!_owner || !_owner.isActiveAndEnabled || !_currentAttack) return;
        if (_damage <= 0f && _poiseDamage <= 0f && _guardPoiseDamage <= 0f) return;
        if ((_targetLayers.value & (1 << other.gameObject.layer)) == 0) return;
        if (!other.TryGetComponent(out Hurtbox hurtbox)) return;
        if (!hurtbox.CanReceiveDamage || hurtbox.Owner == _owner) return;

        // 가드로 막은 공격도 기록하여 같은 공격이 다시 적용되지 않게 한다.
        int ownerId = hurtbox.Owner.GetInstanceID();

        if (!_hitOwnerIds.Add(ownerId)) return;

        DamageInfo damageInfo = new DamageInfo(_owner, _damage, _currentAttack.HitStopDuration,
            _poiseDamage, _guardPoiseDamage, _energyRecoveryOnHit);
        hurtbox.ReceiveDamage(damageInfo);
    }
}
