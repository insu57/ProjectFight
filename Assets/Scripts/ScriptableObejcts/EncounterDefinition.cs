using System;
using UnityEngine;

public enum EncounterEnemyCountScope
{
    PreviousGroup = 0,
    EntireEncounter = 1
}

public enum EncounterConditionCombineMode
{
    All = 0,
    Any = 1
}

[Serializable]
public sealed class EncounterEnemyEntry
{
    // 기존 SpawnManager에서 바로 사용할 수 있는 적 프리팹을 지정한다.
    [field: SerializeField] public EnemyManager Prefab { get; private set; }
    // 같은 프리팹에 적용할 스탯과 행동 패턴을 편성별로 지정한다.
    [field: SerializeField] public EnemyDefinition Definition { get; private set; }
    [field: SerializeField, Min(1)] public int Count { get; private set; } = 1;
}

[Serializable]
public sealed class EncounterSpawnCondition
{
    // 사용 중인 조건만 평가하며, 두 조건을 모두 끄면 즉시 스폰한다.
    [field: SerializeField, Tooltip("경과 시간 조건을 사용합니다.")]
    public bool UseTime { get; private set; }
    // 첫 그룹은 전투 시작, 이후 그룹은 바로 이전 그룹의 스폰 시점을 시간 기준으로 삼는다.
    [field: SerializeField, Min(0f), Tooltip("이전 그룹 스폰 후 필요한 경과 시간입니다. 첫 그룹은 전투 시작부터 계산합니다.")]
    public float RequiredSeconds { get; private set; }

    [field: SerializeField, Tooltip("생존 적 수 조건을 사용합니다.")]
    public bool UseEnemyCount { get; private set; }
    [field: SerializeField, Min(0), Tooltip("생존 적 수가 이 값 이하이면 조건을 만족합니다. 0이면 전멸을 기다립니다.")]
    public int MaxAliveEnemies { get; private set; }
    // 시간 조건으로 그룹이 겹쳐도 이전 그룹과 전투 전체의 생존 적 수를 구분할 수 있다.
    [field: SerializeField, Tooltip("PreviousGroup은 바로 이전 그룹, EntireEncounter는 지금까지 스폰한 모든 그룹을 확인합니다.")]
    public EncounterEnemyCountScope EnemyCountScope { get; private set; } = EncounterEnemyCountScope.EntireEncounter;

    [field: SerializeField, Tooltip("All은 사용 중인 조건을 모두, Any는 하나 이상 만족하면 스폰합니다.")]
    public EncounterConditionCombineMode CombineMode { get; private set; } = EncounterConditionCombineMode.All;
}

[Serializable]
public sealed class EncounterGroup
{
    [field: SerializeField] public EncounterEnemyEntry[] Enemies { get; private set; } = Array.Empty<EncounterEnemyEntry>();
    // 다음 그룹이 등장하는 조건은 해당 그룹에 저장하여 편성과 함께 설정한다.
    [field: SerializeField] public EncounterSpawnCondition SpawnCondition { get; private set; } = new();
}

[CreateAssetMenu(fileName = "EncounterDefinition", menuName = "Scriptable Objects/EncounterDefinition")]
public sealed class EncounterDefinition : ScriptableObject
{
    // 배열 순서대로 진행할 그룹의 설정만 저장하며, 시간과 생존 적 수는 방별 런타임에서 관리한다.
    [field: SerializeField, Tooltip("배열 순서대로 스폰할 그룹과 각 그룹의 시작 조건입니다.")] public EncounterGroup[] Groups { get; private set; } = Array.Empty<EncounterGroup>();
}
