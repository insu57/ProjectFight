using System;
using UnityEngine;

public readonly struct DamageInfo
{
    public MonoBehaviour Attacker { get; }
    public float Damage { get; }
    public float HitStopDuration { get; }
    public float PoiseDamage { get; }
    public float GuardPoiseDamage { get; }

    public bool HasDamage => Damage > 0f || PoiseDamage > 0f || GuardPoiseDamage > 0f;

    public DamageInfo(MonoBehaviour attacker, float damage, float hitStopDuration = 0f, float poiseDamage = 0f, float guardPoiseDamage = 0f)
    {
        Attacker = attacker;
        Damage = Mathf.Max(0f, damage);
        HitStopDuration = hitStopDuration;
        PoiseDamage = Mathf.Max(0f, poiseDamage);
        GuardPoiseDamage = Mathf.Max(0f, guardPoiseDamage);
    }

    public void ApplyHitStop(IHitStopReceiver target)
    {
        // 유효한 피격으로 확정한 Manager에서만 호출한다.
        target?.BeginHitStop(HitStopDuration);

        if (Attacker && Attacker.isActiveAndEnabled && Attacker is IHitStopReceiver attacker)
        {
            attacker.BeginHitStop(HitStopDuration);
        }
    }
}


[DisallowMultipleComponent]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class Hurtbox : MonoBehaviour
{
    public event Action<DamageInfo> DamageRequested;

    public MonoBehaviour Owner { get; private set; }

    // 소유 Manager가 회피 등으로 피격 허용 여부를 제어한다.
    private bool _isDamageEnabled = true;

    public bool CanReceiveDamage => isActiveAndEnabled
        && _isDamageEnabled
        && Owner
        && Owner.isActiveAndEnabled
        && DamageRequested != null;

    private void Awake()
    {
        // 이동용 Collider와 분리된 피격 전용 영역으로 사용한다.
        GetComponent<CapsuleCollider>().isTrigger = true;
    }

    public void Initialize(MonoBehaviour owner) //추후 인터페이스로?
    {
        // 여러 피격 부위가 생겨도 같은 Manager를 소유자로 사용한다.
        Owner = owner;
    }

    public void SetDamageEnabled(bool isEnabled)
    {
        _isDamageEnabled = isEnabled;
    }

    public void ReceiveDamage(DamageInfo damageInfo)
    {
        if (!CanReceiveDamage || !damageInfo.HasDamage) return;
        if (damageInfo.Attacker == Owner) return;

        DamageRequested?.Invoke(damageInfo);
    }
}
