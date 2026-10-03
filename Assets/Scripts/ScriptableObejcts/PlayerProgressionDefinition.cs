using System;
using UnityEngine;

// CSV의 한 행이다. 스탯은 증가량이 아니라 해당 레벨의 기본값을 보관한다.
[Serializable]
public struct PlayerLevelData
{
    [SerializeField, Min(1)] private int _level;
    [SerializeField, Min(0)] private int _targetExp;
    [SerializeField, Min(0)] private int _skillPointsOnReach;
    [SerializeField, Min(0f)] private float _maxHealth;
    [SerializeField, Min(0f)] private float _attackPower;
    [SerializeField, Min(0f)] private float _defensePower;
    [SerializeField, Min(0f)] private float _maxEnergy;

    public int Level => _level;
    public int TargetExp => _targetExp;
    public int SkillPointsOnReach => _skillPointsOnReach;
    public float MaxHealth => _maxHealth;
    public float AttackPower => _attackPower;
    public float DefensePower => _defensePower;
    public float MaxEnergy => _maxEnergy;

    public PlayerLevelData(int level, int targetExp, int skillPointsOnReach, float maxHealth, float attackPower,
        float defensePower, float maxEnergy)
    {
        _level = level;
        _targetExp = targetExp;
        _skillPointsOnReach = skillPointsOnReach;
        _maxHealth = maxHealth;
        _attackPower = attackPower;
        _defensePower = defensePower;
        _maxEnergy = maxEnergy;
    }
}

public sealed class PlayerProgressionDefinition : ScriptableObject
{
    [SerializeField] private PlayerLevelData[] _levels = Array.Empty<PlayerLevelData>();

    public int MaxLevel => _levels == null ? 0 : _levels.Length;

    public bool TryGetLevel(int level, out PlayerLevelData data)
    {
        // 1부터 연속된 레벨만 임포트하므로 검색이나 추가 할당 없이 조회한다.
        if (_levels == null || level < 1 || level > _levels.Length)
        {
            data = default;
            return false;
        }

        data = _levels[level - 1];
        return true;
    }

#if UNITY_EDITOR
    // CSV 자체를 참조하지 않고 편집용 식별 정보만 저장한다.
    [SerializeField, HideInInspector] private string _sourceCsvGuid;
    [SerializeField, HideInInspector] private string _sourceCsvHash;

    public string SourceCsvGuid => _sourceCsvGuid;
    public string SourceCsvHash => _sourceCsvHash;

    public void SetImportedData(PlayerLevelData[] levels, string sourceCsvGuid, string sourceCsvHash)
    {
        _levels = levels;
        _sourceCsvGuid = sourceCsvGuid;
        _sourceCsvHash = sourceCsvHash;
    }
#endif
}
