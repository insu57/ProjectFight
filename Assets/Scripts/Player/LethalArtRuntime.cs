using System;
using System.Collections.Generic;
using UnityEngine;

// 입력·로그·에너지 소모 없이 선택 결과만 반환하여 이후 HUD에서도 같은 판정을 사용한다.
public sealed class LethalArtRuntime
{
    private readonly LethalArtDefinition[] _definitions;
    // 정렬된 정의와 같은 인덱스에 플레이어별 해금 상태를 보관하고 공유 SO는 변경하지 않는다.
    private readonly bool[] _unlockedDefinitions;
    // 플레이어 조건을 통과한 후보를 같은 배열에 다시 채워 적별 조회에서 재사용한다.
    private readonly LethalArtDefinition[] _playerCandidates;
    private int _playerCandidateCount;
    private bool _hasCachedPlayerConditions;
    private float _cachedHealth;
    private float _cachedMaxHealth;
    private float _cachedEnergy;
    private bool _cachedIsPlayerSprinting;
    private float _cachedSearchDistance;

    public LethalArtRuntime(LethalArtDefinition[] definitions)
    {
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));

        _definitions = new LethalArtDefinition[definitions.Length];
        _unlockedDefinitions = new bool[definitions.Length];
        _playerCandidates = new LethalArtDefinition[definitions.Length];
        Array.Copy(definitions, _definitions, definitions.Length);
        // 입력마다 정렬하거나 후보 목록을 새로 만들지 않는다.
        Array.Sort(_definitions, ComparePriority);
    }

    public bool TryGetDefinition(int lethalArtId, out LethalArtDefinition definition)
    {
        // 스킬 설명은 해금·에너지·발동 조건과 무관하게 이미 로드한 전체 정의를 조회한다.
        for (int i = 0; i < _definitions.Length; i++)
        {
            LethalArtDefinition candidate = _definitions[i];
            if (!candidate || candidate.LethalArtId != lethalArtId) continue;
            definition = candidate;
            return true;
        }

        definition = null;
        return false;
    }

    public void SetUnlockedLethalArtIds(IReadOnlyList<int> unlockedIds)
    {
        bool changed = false;
        for (int i = 0; i < _definitions.Length; i++)
        {
            LethalArtDefinition definition = _definitions[i];
            bool isUnlocked = false;
            if (definition && unlockedIds != null)
            {
                for (int j = 0; j < unlockedIds.Count; j++)
                {
                    if (unlockedIds[j] != definition.LethalArtId) continue;
                    isUnlocked = true;
                    break;
                }
            }

            // 같은 ID의 기본 해금·스킬 해금이 겹쳐도 정의 하나의 상태로만 반영한다.
            if (_unlockedDefinitions[i] == isUnlocked) continue;
            _unlockedDefinitions[i] = isUnlocked;
            changed = true;
        }

        // 체력·에너지 등이 그대로여도 새 해금과 해제는 다음 입력·HUD 조회에 즉시 반영한다.
        if (changed) _hasCachedPlayerConditions = false;
    }

    public bool TryGetSearchDistance(in LethalArtPlayerContext player, out float searchDistance)
    {
        RefreshPlayerCandidatesIfNeeded(in player.Stats, player.IsSprinting);
        searchDistance = _cachedSearchDistance;

        // 해금·에너지·체력·달리기 조건에서 전부 탈락하면 물리 탐색 자체를 생략한다.
        return _playerCandidateCount > 0;
    }

    public bool TrySelect(in LethalArtPlayerContext player, in LethalArtTargetContext target, out LethalArtDefinition selected)
    {
        selected = null;
        if (!IsAlive(target.Health, target.MaxHealth) || !IsFinite(target.DistanceSquared) || target.DistanceSquared < 0f) return false;

        RefreshPlayerCandidatesIfNeeded(in player.Stats, player.IsSprinting);
        for (int i = 0; i < _playerCandidateCount; i++)
        {
            LethalArtDefinition definition = _playerCandidates[i];
            if (!definition || !definition.Attack) continue;
            if (target.DistanceSquared > definition.ProximityDistance * definition.ProximityDistance) continue;
            if (!MatchesHealth(definition.EnemyHealthCondition, target.Health, target.MaxHealth)) continue;
            // 적 상태는 캐시하지 않고 조회할 때마다 검사해 가드·스턴·브레이크 전환을 즉시 반영한다.
            if (!MatchesRequirement(definition.EnemyGuardRequirement, target.IsGuarding)) continue;
            if (!MatchesRequirement(definition.EnemyStunRequirement, target.IsStunned)) continue;
            if (!MatchesRequirement(definition.EnemyGuardBreakRequirement, target.IsGuardBroken)) continue;
            // 벽 조건에서 탈락해도 다음 우선순위 후보를 계속 검사한다.
            if (!MatchesNearbyWall(definition, player.Position, target.Position)) continue;

            // 같은 대상의 체력·거리·상태 조건을 모두 통과한 필살기만 반환한다.
            selected = definition;
            return true;
        }

        return false;
    }

    private void RefreshPlayerCandidatesIfNeeded(in PlayerStatSnapshot playerStats, bool isPlayerSprinting)
    {
        // 달리기 시작·종료도 캐시에 반영하며, 같은 조건의 입력과 UI 조회는 후보를 재사용한다.
        if (_hasCachedPlayerConditions && _cachedHealth.Equals(playerStats.CurrentHealth)
            && _cachedMaxHealth.Equals(playerStats.MaxHealth) && _cachedEnergy.Equals(playerStats.CurrentEnergy)
            && _cachedIsPlayerSprinting == isPlayerSprinting) return;

        int previousCount = _playerCandidateCount;
        _playerCandidateCount = 0;
        _cachedSearchDistance = 0f;
        // SO의 플레이어 조건 설정은 실행 중 고정된 데이터로 취급한다.
        for (int i = 0; i < _definitions.Length; i++)
        {
            LethalArtDefinition definition = _definitions[i];
            if (!_unlockedDefinitions[i] || !CanUseForPlayer(definition, playerStats, isPlayerSprinting)) continue;

            // 원래 정렬 순서를 유지하여 적별 판정에서도 우선순위가 보존된다.
            _playerCandidates[_playerCandidateCount++] = definition;
            _cachedSearchDistance = Mathf.Max(_cachedSearchDistance, definition.ProximityDistance);
        }

        if (previousCount > _playerCandidateCount) Array.Clear(_playerCandidates, _playerCandidateCount, previousCount - _playerCandidateCount);
        _cachedHealth = playerStats.CurrentHealth;
        _cachedMaxHealth = playerStats.MaxHealth;
        _cachedEnergy = playerStats.CurrentEnergy;
        _cachedIsPlayerSprinting = isPlayerSprinting;
        _hasCachedPlayerConditions = true;
    }

    public static bool ShouldReplaceSelection(LethalArtDefinition candidate, float candidateDistanceSquared,
        LethalArtDefinition current, float currentDistanceSquared)
    {
        if (!candidate) return false;
        if (!current) return true;

        int priorityComparison = ComparePriority(candidate, current);
        return priorityComparison < 0 || (priorityComparison == 0 && candidateDistanceSquared < currentDistanceSquared);
    }

    private static bool CanUseForPlayer(LethalArtDefinition definition, in PlayerStatSnapshot playerStats, bool isPlayerSprinting)
    {
        // 실행할 공격이 없는 정의는 입력과 HUD의 사용 가능 후보에서 함께 제외한다.
        if (!definition || !definition.Attack || !IsAlive(playerStats.CurrentHealth, playerStats.MaxHealth)) return false;
        if (!IsFinite(playerStats.CurrentEnergy) || !IsFinite(definition.EnergyCost) || definition.EnergyCost < 0f) return false;
        if (playerStats.CurrentEnergy < definition.EnergyCost) return false;
        if (!IsFinite(definition.ProximityDistance) || definition.ProximityDistance < 0f) return false;
        if (!MatchesRequirement(definition.PlayerSprintRequirement, isPlayerSprinting)) return false;

        return MatchesHealth(definition.PlayerHealthCondition, playerStats.CurrentHealth, playerStats.MaxHealth);
    }

    private static bool MatchesNearbyWall(LethalArtDefinition definition, Vector3 playerPosition, Vector3 targetEnemyPosition)
    {
        if (definition.NearbyWallRequirement == LethalArtConditionRequirement.Any) return true;
        // Wall 레이어 누락을 '벽 없음'으로 해석해 Forbidden 조건이 통과하지 않게 한다.
        int wallLayerMask = GameLayerUtility.WallMask;
        if (wallLayerMask == 0) return false;

        Vector3 origin;
        switch (definition.WallProximityOrigin)
        {
            case LethalArtProximityOrigin.Player:
                origin = playerPosition;
                break;
            case LethalArtProximityOrigin.TargetEnemy:
                origin = targetEnemyPosition;
                break;
            default:
                return false;
        }

        // 이동·대상 변경을 즉시 반영하도록 체력·에너지 후보 캐시와 별개로 조회한다.
        bool hasWall = Physics.CheckSphere(origin, definition.ProximityDistance, wallLayerMask, QueryTriggerInteraction.Ignore);
        return MatchesRequirement(definition.NearbyWallRequirement, hasWall);
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
