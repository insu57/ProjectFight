using System;
using UnityEngine;

public readonly struct PlayerStatSnapshot
{
    public int Level { get; }
    public int CurrentExp { get; }
    public int TargetExp { get; }
    public int MaxHealth { get; }
    public int CurrentHealth { get; }
    public float AttackPower { get; }
    public float DefensePower { get; }
    public float MaxEnergy { get; }
    public float CurrentEnergy { get; }

    public PlayerStatSnapshot(int level, int currentExp, int targetExp, int maxHealth, int currentHealth, float attackPower, float defensePower, float maxEnergy, float currentEnergy)
    {
        Level = level;
        CurrentExp = currentExp;
        TargetExp = targetExp;
        MaxHealth = maxHealth;
        CurrentHealth = currentHealth;
        AttackPower = attackPower;
        DefensePower = defensePower;
        MaxEnergy = maxEnergy;
        CurrentEnergy = currentEnergy;
    }
}

[DisallowMultipleComponent]
public class PlayerStat : MonoBehaviour
{
    [SerializeField, Min(1)] private int _level = 1;
    [SerializeField, Min(0)] private int _currentExp;
    [SerializeField, Min(1)] private int _targetExp = 100; // 추후 계산식 적용
    [SerializeField, Min(1)] private int _maxHealth = 100;
    [SerializeField, Min(0)] private int _currentHealth = 100;
    // 가드 브레이크?

    [SerializeField, Min(0f)] private float _baseAttackPower;
    [SerializeField, Min(0f)] private float _baseDefensePower;
    [SerializeField, Min(0f)] private float _baseMaxEnergy = 100f;
    [SerializeField, Min(0f)] private float _currentEnergy;

    public event Action<PlayerStatSnapshot> StatsChanged;

    public PlayerStatSnapshot CurrentStats => new(_level, _currentExp, _targetExp, _maxHealth, _currentHealth,
        _baseAttackPower, _baseDefensePower, _baseMaxEnergy, _currentEnergy);

    private void Awake()
    {
        _currentHealth = _maxHealth;
        _currentEnergy = 0f;
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _currentExp += amount;
        NotifyStatsChanged();
    }

    public void SetLevel(int level, int targetExp) //수정필.
    {
        int clampedLevel = Mathf.Max(1, level);
        int clampedTargetExp = Mathf.Max(1, targetExp);

        if (_level == clampedLevel && _targetExp == clampedTargetExp)
        {
            return;
        }

        _level = clampedLevel;
        _targetExp = clampedTargetExp;
        _currentExp = Mathf.Min(_currentExp, _targetExp);
        NotifyStatsChanged();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || _currentHealth <= 0)
        {
            return;
        }

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        NotifyStatsChanged();
    }

    public void RestoreHealth(int amount)
    {
        if (amount <= 0 || _currentHealth >= _maxHealth)
        {
            return;
        }

        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        NotifyStatsChanged();
    }

    public void SetMaxHealth(int maxHealth)
    {
        int clampedMaxHealth = Mathf.Max(1, maxHealth);

        if (_maxHealth == clampedMaxHealth)
        {
            return;
        }

        _maxHealth = clampedMaxHealth;
        _currentHealth = Mathf.Min(_currentHealth, _maxHealth);
        NotifyStatsChanged();
    }

    public bool TryConsumeEnergy(float amount)
    {
        if (amount <= 0f || _currentEnergy < amount)
        {
            return false;
        }

        _currentEnergy -= amount;
        NotifyStatsChanged();
        return true;
    }

    public void RestoreEnergy(float amount)
    {
        if (amount <= 0f || _currentEnergy >= _baseMaxEnergy)
        {
            return;
        }

        _currentEnergy = Mathf.Min(_baseMaxEnergy, _currentEnergy + amount);
        NotifyStatsChanged();
    }

    public void SetBaseStats(float attackPower, float defensePower, float maxEnergy) //수정필.
    {
        float clampedAttackPower = Mathf.Max(0f, attackPower);
        float clampedDefensePower = Mathf.Max(0f, defensePower);
        float clampedMaxEnergy = Mathf.Max(0f, maxEnergy);

        if (Mathf.Approximately(_baseAttackPower, clampedAttackPower) && Mathf.Approximately(_baseDefensePower, clampedDefensePower)
            && Mathf.Approximately(_baseMaxEnergy, clampedMaxEnergy))
        {
            return;
        }

        _baseAttackPower = clampedAttackPower;
        _baseDefensePower = clampedDefensePower;
        _baseMaxEnergy = clampedMaxEnergy;
        _currentEnergy = Mathf.Min(_currentEnergy, _baseMaxEnergy);
        NotifyStatsChanged();
    }

    private void NotifyStatsChanged()
    {
        // 외부에는 변경 가능한 PlayerStat 대신 현재 값의 스냅샷만 전달한다.
        StatsChanged?.Invoke(CurrentStats);
    }
}
