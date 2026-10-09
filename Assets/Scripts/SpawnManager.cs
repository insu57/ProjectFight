using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public PlayerManager Player { get; private set; }
    [field: SerializeField] public List<EnemyManager> EnemyList { get; private set; } = new();
    //직렬화->테스트용

    [Header("전투방 스폰")]
    [SerializeField] private LayerMask _characterLayers = (1 << 6) | (1 << 7);
    [SerializeField, Min(0f)] private float _spawnClearance = 0.1f;
    [SerializeField, Min(1)] private int _maxSpawnPositionAttempts = 30;
    [SerializeField, Min(1)] private int _maxEnemyAttemptsPerUpdate = 8;
    [SerializeField, Min(0.01f)] private float _spawnRetryInterval = 0.25f;

    public int MaxEnemyAttemptsPerUpdate => _maxEnemyAttemptsPerUpdate;
    public float SpawnRetryInterval => _spawnRetryInterval;

    private struct SpawnReservation
    {
        public Vector3 Bottom;
        public Vector3 Top;
        public float Radius;
    }

    // 같은 배치에서 아직 물리 쿼리에 반영되지 않은 새 적의 공간도 예약한다.
    private readonly List<SpawnReservation> _spawnReservations = new(8);

    // 스테이지 준비 시 재사용하여 프리팹 중복 제거용 컬렉션의 반복 할당을 피한다.
    private readonly HashSet<EnemyManager> _preparedEnemyPrefabs = new();

    public void Initialize(PlayerManager player)
    {
        // 플레이어 참조는 StageManager가 주입하며 자체 씬 검색은 하지 않는다.
        Player = player;
    }

    private void Start()
    {
        // 플레이어 주입이나 테스트 목록 설정이 누락된 경우 타깃 지정을 건너뛴다.
        if (!Player || EnemyList == null)
        {
            return;
        }

        foreach (EnemyManager enemy in EnemyList)//테스트 시
        {
            if (!enemy) continue;

            enemy.SetTarget(Player.transform);
        }

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
        return SpawnEnemy(prefab, null, position, rotation);
    }

    private EnemyManager SpawnEnemy(EnemyManager prefab, EnemyDefinition definition, Vector3 position, Quaternion rotation, Room room = null)
    {
        PoolingManager poolingManager = PoolingManager.Instance;

        if (!poolingManager || !Player)
        {
            return null;
        }

        EnemyManager enemy = poolingManager.Spawn(prefab, position, rotation);

        if (enemy)
        {
            // 스폰을 요청한 방의 이동 정보는 EnemyManager를 통해 전달한다.
            enemy.InitializeSpawn(definition, Player.transform, room);
        }

        return enemy;
    }

    public void BeginSpawnBatch()
    {
        _spawnReservations.Clear();
        // 배치를 시작할 때만 동기화해 같은 프레임의 이동과 이전 배치 활성화를 정확히 검사한다.
        Physics.SyncTransforms();
    }

    public bool TrySpawnEnemy(Room room, EncounterEnemyEntry entry, out EnemyManager enemy)
    {
        enemy = null;
        PoolingManager poolingManager = PoolingManager.Instance;
        if (!isActiveAndEnabled || !room || entry == null || !entry.Prefab ||
            !Player || !poolingManager || !poolingManager.isActiveAndEnabled) return false;
        if (!entry.Prefab.TryGetComponent(out CharacterController controller) || !controller.enabled) return false;

        // 풀의 부모 스케일까지 반영한 이동용 캡슐을 사용한다. Hurtbox Trigger는 검사하지 않는다.
        Vector3 scale = Vector3.Scale(entry.Prefab.transform.localScale, poolingManager.transform.lossyScale);
        float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(controller.height * Mathf.Abs(scale.y), radius * 2f);
        if (radius <= 0f || height <= 0f) return false;

        float queryRadius = radius + Mathf.Max(0f, _spawnClearance);
        Quaternion rotation = Quaternion.Euler(0f, room.transform.eulerAngles.y, 0f);
        Vector3 centerOffset = rotation * Vector3.Scale(controller.center, scale);
        float sphereOffset = height * 0.5f - radius;

        for (int attempt = 0; attempt < Mathf.Max(1, _maxSpawnPositionAttempts); attempt++)
        {
            if (!room.TryGetRandomSpawnPosition(queryRadius, out Vector3 footPosition)) return false;
            if (!room.TryValidateSpawnPosition(footPosition, queryRadius, out footPosition)) continue;

            Vector3 center = footPosition + Vector3.up * (height * 0.5f);
            Vector3 bottom = center - Vector3.up * sphereOffset;
            Vector3 top = center + Vector3.up * sphereOffset;
            if (Physics.CheckCapsule(bottom, top, queryRadius, _characterLayers, QueryTriggerInteraction.Ignore)) continue;
            if (OverlapsSpawnReservation(bottom, top, queryRadius)) continue;

            // CharacterController.center가 치우친 프리팹도 발과 몸의 중심을 영역 안에 맞춘다.
            enemy = SpawnEnemy(entry.Prefab, entry.Definition, center - centerOffset, rotation, room);
            if (!enemy) return false;

            _spawnReservations.Add(new SpawnReservation { Bottom = bottom, Top = top, Radius = queryRadius });
            return true;
        }

        return false;
    }

    private bool OverlapsSpawnReservation(Vector3 bottom, Vector3 top, float radius)
    {
        for (int index = 0; index < _spawnReservations.Count; index++)
        {
            SpawnReservation reservation = _spawnReservations[index];
            float deltaX = bottom.x - reservation.Bottom.x;
            float deltaZ = bottom.z - reservation.Bottom.z;
            // 수직 캡슐의 축 사이 최소 거리로 검사하여 다른 높이의 영역도 구분한다.
            float deltaY = Mathf.Max(0f, Mathf.Max(bottom.y - reservation.Top.y, reservation.Bottom.y - top.y));
            float combinedRadius = radius + reservation.Radius;
            if (deltaX * deltaX + deltaZ * deltaZ + deltaY * deltaY <= combinedRadius * combinedRadius) return true;
        }

        return false;
    }

}
