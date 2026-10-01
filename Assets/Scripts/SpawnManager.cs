using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public PlayerManager Player { get; private set; }
    [field: SerializeField] public List<EnemyManager> EnemyList { get; private set; } = new();
    //직렬화->테스트용

    // 스테이지 준비 시 재사용하여 프리팹 중복 제거용 컬렉션의 반복 할당을 피한다.
    private readonly HashSet<EnemyManager> _preparedEnemyPrefabs = new();

    private void Awake()
    {
        Player = FindFirstObjectByType<PlayerManager>();
    }

    public void PrepareEnemyPools(StageDefinition stageDefinition)
    {
        _preparedEnemyPrefabs.Clear();

        if (!stageDefinition || stageDefinition.EnemyPrewarmCountPerPrefab <= 0)
        {
            return;
        }

        EncounterDefinition[] encounters = stageDefinition.BattleEncounters;
        if (encounters == null || encounters.Length == 0)
        {
            return;
        }

        PoolingManager poolingManager = PoolingManager.Instance;
        if (!poolingManager || !poolingManager.isActiveAndEnabled)
        {
            Debug.LogWarning("활성 PoolingManager가 없어 일반 적의 사전 풀 준비를 건너뜁니다.", this);
            return;
        }

        // 일반 전투 후보의 모든 그룹을 확인하며 BossEncounter는 준비 대상에 포함하지 않는다.
        for (int encounterIndex = 0; encounterIndex < encounters.Length; encounterIndex++)
        {
            EncounterDefinition encounter = encounters[encounterIndex];
            if (!encounter || encounter.Groups == null) continue;

            EncounterGroup[] groups = encounter.Groups;
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                EncounterEnemyEntry[] enemies = groups[groupIndex]?.Enemies;
                if (enemies == null) continue;

                for (int enemyIndex = 0; enemyIndex < enemies.Length; enemyIndex++)
                {
                    EnemyManager prefab = enemies[enemyIndex]?.Prefab;
                    if (!prefab || !_preparedEnemyPrefabs.Add(prefab)) continue;

                    // Definition이나 편성 수량이 달라도 같은 프리팹은 고정 수량으로 한 번만 준비한다.
                    poolingManager.Prewarm(prefab, stageDefinition.EnemyPrewarmCountPerPrefab);
                }
            }
        }

        // 컬렉션 용량은 유지하고 이전 스테이지의 프리팹 참조만 해제한다.
        _preparedEnemyPrefabs.Clear();
    }

    public EnemyManager SpawnEnemy(EnemyManager prefab, Vector3 position, Quaternion rotation)
    {
        PoolingManager poolingManager = PoolingManager.Instance;

        if (!poolingManager || !Player)
        {
            return null;
        }

        EnemyManager enemy = poolingManager.Spawn(prefab, position, rotation);

        if (enemy)
        {
            enemy.SetTarget(Player.transform);
        }

        return enemy;
    }

    private void Start()
    {
        foreach (EnemyManager enemy in EnemyList)
        {
            enemy.SetTarget(Player.transform);
        }
        
    }

}
