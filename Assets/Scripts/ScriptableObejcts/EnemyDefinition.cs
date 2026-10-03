using UnityEngine;


[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Enemy/EnemyDefinition")]
public class EnemyDefinition : ScriptableObject
{
    [field: SerializeField] public string EnemyName { get; private set; }
    [field: SerializeField] public int EnemyID { get; private set; }
    [field: SerializeField] public EnemyPatternDefinition[] Patterns { get; private set; }

    // 이 적을 처치한 플레이어에게 한 번 지급하는 경험치이다.
    [field: SerializeField, Min(0)] public int ExperienceReward { get; private set; } = 10;

    [Header("Base Stats")]
    // 적 종류의 기본값이며 현재 체력 등 런타임 상태는 EnemyStat에서 관리한다.
    [field: SerializeField, Min(1f)] public float MaxHealth { get; private set; } = 100f;
    [field: SerializeField, Min(0f)] public float AttackPower { get; private set; }
    [field: SerializeField, Min(0f)] public float DefensePower { get; private set; }
    [field: SerializeField, Min(0f)] public float MoveSpeed { get; private set; } = 3.5f;
    // 공격별 AnimationSpeed에 곱하는 배율이다. 이동과 피격 모션에는 적용하지 않는다.
    [field: SerializeField, Min(0.01f)] public float AttackSpeed { get; private set; } = 1f;

    // 적 종류별 일반 강인도 설정이다.
    [field: SerializeField, Min(0f)] public float MaxPoise { get; private set; } = 100f;
    [field: SerializeField, Min(0f)] public float PoiseRecoveryDelay { get; private set; } = 3f;
    [field: SerializeField, Min(0f)] public float PoiseRecoveryRate { get; private set; } = 20f;
    [field: SerializeField, Min(0f)] public float StaggerDuration { get; private set; } = 0.5f;
    // 경직 시간 동안 밀려나는 총 거리이다.
    [field: SerializeField, Min(0f)] public float StaggerMoveDistance { get; private set; } = 0.5f;

    // 적 종류별 가드 강인도 설정이다.
    [field: SerializeField, Min(0f)] public float MaxGuardPoise { get; private set; } = 100f;
    [field: SerializeField, Min(0f)] public float GuardPoiseRecoveryDelay { get; private set; } = 3f;
    [field: SerializeField, Min(0f)] public float GuardPoiseRecoveryRate { get; private set; } = 20f;
    [field: SerializeField, Min(0f)] public float GuardBreakDuration { get; private set; } = 1.5f;
    // 가드 브레이크 시간 동안 밀려나는 총 거리이다.
    [field: SerializeField, Min(0f)] public float GuardBreakMoveDistance { get; private set; } = 1f;
}
