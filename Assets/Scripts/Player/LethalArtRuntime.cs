using System;
using UnityEngine;

// 입력·로그·에너지 소모 없이 선택 결과만 반환하여 이후 HUD에서도 같은 판정을 사용한다.
public sealed class LethalArtRuntime
{
    private readonly LethalArtDefinition[] _definitions;
    // 플레이어 조건을 통과한 후보를 같은 배열에 다시 채워 적별 조회에서 재사용한다.
    private readonly LethalArtDefinition[] _playerCandidates;
    private int _playerCandidateCount;
    private bool _hasCachedPlayerStats;
    private float _cachedHealth;
    private float _cachedMaxHealth;
    private float _cachedEnergy;
    private float _cachedSearchDistance;

    public LethalArtRuntime(LethalArtDefinition[] definitions)
    {
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));

        _definitions = new LethalArtDefinition[definitions.Length];
        _playerCandidates = new LethalArtDefinition[definitions.Length];
        Array.Copy(definitions, _definitions, definitions.Length);
        // 입력마다 정렬하거나 후보 목록을 새로 만들지 않는다.
        Array.Sort(_definitions, ComparePriority);
    }

    public bool TryGetSearchDistance(in PlayerStatSnapshot playerStats, out float searchDistance)
    {
        RefreshPlayerCandidatesIfNeeded(playerStats);
        searchDistance = _cachedSearchDistance;

        // 에너지나 플레이어 체력에서 전부 탈락하면 물리 탐색 자체를 생략한다.
        return _playerCandidateCount > 0;
    }

    public bool TrySelect(in PlayerStatSnapshot playerStats, float enemyHealth, float enemyMaxHealth, float distanceSquared, bool isEnemyGuarding, bool isEnemyStunned, bool isEnemyGuardBroken, out LethalArtDefinition selected)
    {
        selected = null;
        if (!IsAlive(enemyHealth, enemyMaxHealth) || !IsFinite(distanceSquared) || distanceSquared < 0f) return false;

        RefreshPlayerCandidatesIfNeeded(playerStats);
        for (int i = 0; i < _playerCandidateCount; i++)
        {
            LethalArtDefinition definition = _playerCandidates[i];
            if (!definition || !definition.Attack) continue;
            if (distanceSquared > definition.ProximityDistance * definition.ProximityDistance) continue;
            if (!MatchesHealth(definition.EnemyHealthCondition, enemyHealth, enemyMaxHealth)) continue;
            // 적 상태는 캐시하지 않고 조회할 때마다 검사해 가드·스턴·브레이크 전환을 즉시 반영한다.
            if (!MatchesRequirement(definition.EnemyGuardRequirement, isEnemyGuarding)) continue;
            if (!MatchesRequirement(definition.EnemyStunRequirement, isEnemyStunned)) continue;
            if (!MatchesRequirement(definition.EnemyGuardBreakRequirement, isEnemyGuardBroken)) continue;

            // 같은 대상의 체력·거리·상태 조건을 모두 통과한 필살기만 반환한다.
            selected = definition;
            return true;
        }

        return false;
    }

    private void RefreshPlayerCandidatesIfNeeded(in PlayerStatSnapshot playerStats)
    {
        // 입력과 UI가 같은 수치를 조회하면 후보 필터링과 최대 범위 계산을 반복하지 않는다.
        if (_hasCachedPlayerStats && _cachedHealth.Equals(playerStats.CurrentHealth)
            && _cachedMaxHealth.Equals(playerStats.MaxHealth) && _cachedEnergy.Equals(playerStats.CurrentEnergy)) return;

        int previousCount = _playerCandidateCount;
        _playerCandidateCount = 0;
        _cachedSearchDistance = 0f;
        // SO의 플레이어 조건 설정은 실행 중 고정된 데이터로 취급한다.
        for (int i = 0; i < _definitions.Length; i++)
        {
            LethalArtDefinition definition = _definitions[i];
            if (!CanUseForPlayer(definition, playerStats)) continue;

            // 원래 정렬 순서를 유지하여 적별 판정에서도 우선순위가 보존된다.
            _playerCandidates[_playerCandidateCount++] = definition;
            _cachedSearchDistance = Mathf.Max(_cachedSearchDistance, definition.ProximityDistance);
        }

        if (previousCount > _playerCandidateCount) Array.Clear(_playerCandidates, _playerCandidateCount, previousCount - _playerCandidateCount);
        _cachedHealth = playerStats.CurrentHealth;
        _cachedMaxHealth = playerStats.MaxHealth;
        _cachedEnergy = playerStats.CurrentEnergy;
        _hasCachedPlayerStats = true;
    }

    public static bool ShouldReplaceSelection(LethalArtDefinition candidate, float candidateDistanceSquared,
        LethalArtDefinition current, float currentDistanceSquared)
    {
        if (!candidate) return false;
        if (!current) return true;

        int priorityComparison = ComparePriority(candidate, current);
        return priorityComparison < 0 || (priorityComparison == 0 && candidateDistanceSquared < currentDistanceSquared);
    }

    private static bool CanUseForPlayer(LethalArtDefinition definition, in PlayerStatSnapshot playerStats)
    {
        // 실행할 공격이 없는 정의는 입력과 HUD의 사용 가능 후보에서 함께 제외한다.
        if (!definition || !definition.Attack || !IsAlive(playerStats.CurrentHealth, playerStats.MaxHealth)) return false;
        if (!IsFinite(playerStats.CurrentEnergy) || !IsFinite(definition.EnergyCost) || definition.EnergyCost < 0f) return false;
        if (playerStats.CurrentEnergy < definition.EnergyCost) return false;
        if (!IsFinite(definition.ProximityDistance) || definition.ProximityDistance < 0f) return false;

        return MatchesHealth(definition.PlayerHealthCondition, playerStats.CurrentHealth, playerStats.MaxHealth);
    }

    private static bool MatchesHealth(LethalArtHealthCondition condition, float health, float maxHealth)
    {
        if (!condition.Enabled) return true;

        // 정규화된 비율끼리 비교하여 30/100과 0.3처럼 같은 경계값을 일관되게 판정한다.
        float ratio = health / maxHealth;
        return ratio >= condition.MinimumRatio && ratio <= condition.MaximumRatio;
    }

    private static bool MatchesRequirement(LethalArtConditionRequirement requirement, bool isActive)
    {
        return requirement switch
        {
            LethalArtConditionRequirement.Any => true,
            LethalArtConditionRequirement.Required => isActive,
            LethalArtConditionRequirement.Forbidden => !isActive,
            _ => false
        };
    }

    private static bool IsAlive(float health, float maxHealth)
    {
        return IsFinite(health) && IsFinite(maxHealth) && health > 0f && maxHealth > 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static int ComparePriority(LethalArtDefinition left, LethalArtDefinition right)
    {
        if (left == right) return 0;
        if (!left) return 1;
        if (!right) return -1;

        int priorityComparison = right.Priority.CompareTo(left.Priority);
        if (priorityComparison != 0) return priorityComparison;
        return left.LethalArtId.CompareTo(right.LethalArtId);
    }
}
