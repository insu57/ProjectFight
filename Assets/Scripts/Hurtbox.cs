using System;
using UnityEngine;

public readonly struct DamageInfo
{
    public MonoBehaviour Attacker { get; }
    public float Damage { get; }
    public float HitStopDuration { get; }

    public DamageInfo(MonoBehaviour attacker, float damage, float hitStopDuration = 0f)
    {
        Attacker = attacker;
        Damage = damage;
        HitStopDuration = hitStopDuration;
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

    public bool CanReceiveDamage => isActiveAndEnabled
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

    public void ReceiveDamage(DamageInfo damageInfo)
    {
        if (!CanReceiveDamage || damageInfo.Damage <= 0f) return;
        if (damageInfo.Attacker == Owner) return;

        DamageRequested?.Invoke(damageInfo);
    }
}
