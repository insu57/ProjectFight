using System.Collections.Generic;
using UnityEngine;

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

    public void BeginAttack(AttackDefinition attackDefinition)
    {
        // 콤보 전환 시 이전 공격의 박스를 먼저 끈다.
        EndAttack();

        if (!isActiveAndEnabled || !_hitbox || !_owner || !attackDefinition) return;

        _currentAttack = attackDefinition;

        // 이번 공격의 피해량을 실행 정보로 보관한다.
        _damage = Mathf.Max(0f, attackDefinition.BaseDamage);
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
        _hitOwnerIds.Clear();

        if (_hitbox)
        {
            _hitbox.enabled = false;
        }
    }

    private void TryHit(Collider other)
    {
        if (!isActiveAndEnabled || !_hitbox || !_hitbox.enabled) return;
        if (!_owner || !_owner.isActiveAndEnabled || !_currentAttack || _damage <= 0f) return;
        if ((_targetLayers.value & (1 << other.gameObject.layer)) == 0) return;
        if (!other.TryGetComponent(out Hurtbox hurtbox)) return;
        if (!hurtbox.CanReceiveDamage || hurtbox.Owner == _owner) return;

        // Collider가 여러 개여도 한 공격에서 캐릭터당 한 번만 요청한다.
        int ownerId = hurtbox.Owner.GetInstanceID();

        if (!_hitOwnerIds.Add(ownerId)) return;

        DamageInfo damageInfo = new DamageInfo(_owner, _damage, _currentAttack.HitStopDuration);
        hurtbox.ReceiveDamage(damageInfo);
    }
}
