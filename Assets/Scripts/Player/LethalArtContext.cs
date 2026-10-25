using UnityEngine;

// 한 번의 사용 가능 조회에서 탐색 범위와 모든 적 후보가 같은 플레이어 정보를 사용한다.
public readonly struct LethalArtPlayerContext
{
    public readonly PlayerStatSnapshot Stats;
    public readonly Vector3 Position;
    public readonly bool IsSprinting;

    public LethalArtPlayerContext(in PlayerStatSnapshot stats, Vector3 position, bool isSprinting)
    {
        Stats = stats;
        Position = position;
        IsSprinting = isSprinting;
    }
}

// EnemyManager가 검증한 동일한 적의 체력·거리·상태를 값으로 묶어 전달한다.
public readonly struct LethalArtTargetContext
{
    public readonly Vector3 Position;
    public readonly float Health;
    public readonly float MaxHealth;
    public readonly float DistanceSquared;
    public readonly bool IsGuarding;
    public readonly bool IsStunned;
    public readonly bool IsGuardBroken;

    public LethalArtTargetContext(Vector3 position, float health, float maxHealth, float distanceSquared, bool isGuarding,
        bool isStunned, bool isGuardBroken)
    {
        Position = position;
        Health = health;
        MaxHealth = maxHealth;
        DistanceSquared = distanceSquared;
        IsGuarding = isGuarding;
        IsStunned = isStunned;
        IsGuardBroken = isGuardBroken;
    }
}
