using UnityEngine;


[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Enemy/EnemyDefinition")]
public class EnemyDefinition : ScriptableObject
{
    [field: SerializeField] public string EnemyName { get; private set; }
    [field: SerializeField] public int EnemyID { get; private set; }
    [field: SerializeField] public GameObject Model { get; private set; }
    [field: SerializeField] public EnemyPatternDefinition[] Patterns { get; private set; }

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
