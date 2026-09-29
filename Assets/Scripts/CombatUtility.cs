using UnityEngine;

public static class CombatUtility
{
    public static bool IsAttackerBehind(Transform defender, Transform attacker)
    {
        if (!defender || !attacker) return false;

        Vector3 toAttacker = attacker.position - defender.position;
        toAttacker.y = 0f;
        Vector3 forward = defender.forward;
        forward.y = 0f;

        // 방향을 판단할 수 없거나 정확한 측면이면 기존 전방 경직을 사용한다.
        if (toAttacker.sqrMagnitude <= 0.0001f || forward.sqrMagnitude <= 0.0001f) return false;

        return Vector3.Dot(forward, toAttacker) < 0f;
    }

    public static bool IsAttackerInFront(Transform defender, Transform attacker)
    {
        if (!defender || !attacker) return false;

        Vector3 toAttacker = attacker.position - defender.position;
        toAttacker.y = 0f;
        Vector3 forward = defender.forward;
        forward.y = 0f;

        // 수평 방향을 판단할 수 없으면 방어하지 않는다.
        if (toAttacker.sqrMagnitude <= 0.0001f || forward.sqrMagnitude <= 0.0001f) return false;

        // 정규화 없이 전방 여부만 판단하며, 정확한 측면은 제외한다.
        return Vector3.Dot(forward, toAttacker) > 0f;
    }
}
