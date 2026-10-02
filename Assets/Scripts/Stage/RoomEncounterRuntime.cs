using System;
using System.Collections.Generic;
using UnityEngine;

// ScriptableObject는 설정만 공유하고, 그룹 진행과 생존 적은 각 방이 독립적으로 기록한다.
internal sealed class RoomEncounterRuntime
{
    private struct TrackedEnemy
    {
        public EnemyManager Enemy;
        public uint SpawnVersion;
        public int GroupIndex;
        public bool IsAlive;
    }

    private readonly Room _room;
    private readonly SpawnManager _spawnManager;
    private readonly EncounterGroup[] _groups;
    private readonly int[] _groupAliveCounts;
    private readonly int[] _remainingEntryCounts;
    private readonly List<TrackedEnemy> _enemies = new(8);
    private readonly Action<EnemyManager> _enemyDiedHandler;
    private int _nextGroupIndex;
    private int _spawningGroupIndex = -1;
    private int _aliveEnemyCount;
    private int _pendingEnemyCount;
    private float _elapsedSeconds;
    private float _lastGroupSpawnTime;
    private float _retryRemaining;
    private bool _groupHasSpawned;

    public bool IsComplete => _nextGroupIndex >= _groups.Length && _pendingEnemyCount == 0 && _aliveEnemyCount == 0;

    public RoomEncounterRuntime(Room room, SpawnManager spawnManager, EncounterDefinition encounter)
    {
        _room = room;
        _spawnManager = spawnManager;
        _groups = encounter.Groups ?? Array.Empty<EncounterGroup>();
        _groupAliveCounts = new int[_groups.Length];
        _enemyDiedHandler = HandleEnemyDied;

        int maximumEntryCount = 0;
        for (int index = 0; index < _groups.Length; index++)
        {
            maximumEntryCount = Math.Max(maximumEntryCount, _groups[index]?.Enemies?.Length ?? 0);
        }

        // 다음 그룹에서도 남은 수량 배열을 재사용한다.
        _remainingEntryCounts = new int[maximumEntryCount];
    }

    public void Tick(float deltaTime)
    {
        PruneEnemies();
        if (!_room || !_spawnManager || IsComplete) return;

        _elapsedSeconds += Math.Max(0f, deltaTime);
        _retryRemaining -= Math.Max(0f, deltaTime);
        int attemptBudget = _spawnManager.MaxEnemyAttemptsPerUpdate;

        while (attemptBudget > 0)
        {
            if (_spawningGroupIndex >= 0)
            {
                if (_retryRemaining > 0f) return;
                SpawnPendingEnemies(ref attemptBudget);

                if (_pendingEnemyCount > 0)
                {
                    _retryRemaining = _spawnManager.SpawnRetryInterval;
                    return;
                }

                _spawningGroupIndex = -1;
                if (attemptBudget <= 0) return;
            }

            if (_nextGroupIndex >= _groups.Length || !IsNextGroupReady()) return;
            BeginNextGroup();
        }
    }

    private bool IsNextGroupReady()
    {
        EncounterSpawnCondition condition = _groups[_nextGroupIndex]?.SpawnCondition;
        if (condition == null || !condition.UseTime && !condition.UseEnemyCount) return true;

        bool timeReady = _elapsedSeconds - _lastGroupSpawnTime >= Math.Max(0f, condition.RequiredSeconds);
        int aliveCount = condition.EnemyCountScope == EncounterEnemyCountScope.PreviousGroup
            ? (_nextGroupIndex > 0 ? _groupAliveCounts[_nextGroupIndex - 1] : 0)
            : _aliveEnemyCount;
        bool countReady = aliveCount <= Math.Max(0, condition.MaxAliveEnemies);

        if (!condition.UseTime) return countReady;
        if (!condition.UseEnemyCount) return timeReady;
        return condition.CombineMode == EncounterConditionCombineMode.Any ? timeReady || countReady : timeReady && countReady;
    }

    private void BeginNextGroup()
    {
        _spawningGroupIndex = _nextGroupIndex++;
        _pendingEnemyCount = 0;
        _retryRemaining = 0f;
        _groupHasSpawned = false;
        EncounterEnemyEntry[] entries = _groups[_spawningGroupIndex]?.Enemies;
        if (entries != null)
        {
            for (int index = 0; index < entries.Length; index++)
            {
                EncounterEnemyEntry entry = entries[index];
                int count = entry != null && entry.Prefab ? Math.Max(0, entry.Count) : 0;
                _remainingEntryCounts[index] = count;
                _pendingEnemyCount += count;
            }
        }

        // 빈 그룹도 하나의 진행 단계로 취급하지만, 대기 적이 있는 그룹의 시간은 실제 등장 시점부터 잰다.
        if (_pendingEnemyCount == 0) _lastGroupSpawnTime = _elapsedSeconds;
    }

    private void SpawnPendingEnemies(ref int attemptBudget)
    {
        EncounterEnemyEntry[] entries = _groups[_spawningGroupIndex]?.Enemies;
        if (entries == null || _pendingEnemyCount == 0) return;

        _spawnManager.BeginSpawnBatch();
        for (int index = 0; index < entries.Length && attemptBudget > 0; index++)
        {
            while (_remainingEntryCounts[index] > 0 && attemptBudget > 0)
            {
                attemptBudget--;
                if (!_spawnManager.TrySpawnEnemy(_room, entries[index], out EnemyManager enemy)) break;

                if (!_groupHasSpawned)
                {
                    _groupHasSpawned = true;
                    _lastGroupSpawnTime = _elapsedSeconds;
                }

                _remainingEntryCounts[index]--;
                _pendingEnemyCount--;
                _aliveEnemyCount++;
                _groupAliveCounts[_spawningGroupIndex]++;
                _enemies.Add(new TrackedEnemy { Enemy = enemy, SpawnVersion = enemy.SpawnVersion,
                    GroupIndex = _spawningGroupIndex, IsAlive = true });
                enemy.Died += _enemyDiedHandler;
            }
        }

        // 배치가 일부만 등장했을 때는 나머지 수량을 유지한다. 다음 그룹이나 전투 완료로 넘어가지 않는다.
    }

    private void HandleEnemyDied(EnemyManager enemy)
    {
        for (int index = _enemies.Count - 1; index >= 0; index--)
        {
            TrackedEnemy tracked = _enemies[index];
            if (tracked.Enemy == enemy && tracked.SpawnVersion == enemy.SpawnVersion && tracked.IsAlive)
            {
                MarkEnemyDead(index, tracked);
                return;
            }
        }
    }

    private void MarkEnemyDead(int index, TrackedEnemy tracked)
    {
        _aliveEnemyCount--;
        _groupAliveCounts[tracked.GroupIndex]--;
        tracked.IsAlive = false;
        // 네이티브 오브젝트가 이미 파괴되어도 관리 객체의 이벤트 구독은 해제할 수 있다.
        if (!ReferenceEquals(tracked.Enemy, null)) tracked.Enemy.Died -= _enemyDiedHandler;
        _enemies[index] = tracked;
    }

    private void PruneEnemies()
    {
        for (int index = _enemies.Count - 1; index >= 0; index--)
        {
            TrackedEnemy tracked = _enemies[index];
            EnemyManager enemy = tracked.Enemy;
            bool sameSpawn = enemy && enemy.SpawnVersion == tracked.SpawnVersion && enemy.IsSpawned;
            if (sameSpawn && tracked.IsAlive && enemy.IsDead)
            {
                MarkEnemyDead(index, tracked);
            }
            else if (!sameSpawn)
            {
                // 외부에서 반환하거나 파괴한 적도 생존 수에 남기지 않는다.
                if (tracked.IsAlive) MarkEnemyDead(index, tracked);
                _enemies.RemoveAt(index);
            }
        }
    }

    public void Clear()
    {
        for (int index = 0; index < _enemies.Count; index++)
        {
            TrackedEnemy tracked = _enemies[index];
            EnemyManager enemy = tracked.Enemy;
            if (ReferenceEquals(enemy, null)) continue;
            enemy.Died -= _enemyDiedHandler;
            if (!enemy || enemy.SpawnVersion != tracked.SpawnVersion) continue;
            // 사망 연출 중인 적도 정리하되, 다른 방에 재사용된 인스턴스는 반환하지 않는다.
            if (enemy.IsSpawned) enemy.ReturnToPool();
        }

        _enemies.Clear();
        _aliveEnemyCount = 0;
        _pendingEnemyCount = 0;
        _spawningGroupIndex = -1;
        _nextGroupIndex = _groups.Length;
        Array.Clear(_groupAliveCounts, 0, _groupAliveCounts.Length);
    }
}
