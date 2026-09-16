using System;
using UnityEngine;

public readonly struct PlayerStatSnapshot
{
    public int Level { get; }
    public int CurrentExp { get; }
    public int TargetExp { get; }
    public float MaxHealth { get; }
    public float CurrentHealth { get; }
    public float AttackPower { get; }
    public float DefensePower { get; }
    public float MaxEnergy { get; }
    public float CurrentEnergy { get; }

    public float MaxPoise { get; }
    public float PoiseRecoveryDelay { get; }
    public float PoiseRecoveryRate { get; }
    public float StaggerDuration { get; }
    public float MaxGuardPoise { get; }
    public float GuardPoiseRecoveryDelay { get; }
    public float GuardPoiseRecoveryRate { get; }
    public float GuardBreakDuration { get; }
    public float CurrentPoise { get; }
    public float CurrentGuardPoise { get; }
    public float StaggerMoveDistance { get; }
    public float GuardBreakMoveDistance { get; }

    public PlayerStatSnapshot(int level, int currentExp, int targetExp, float maxHealth, float currentHealth, float attackPower, float defensePower, float maxEnergy, float currentEnergy, float maxPoise, float poiseRecoveryDelay, float poiseRecoveryRate, float staggerDuration, float maxGuardPoise, float guardPoiseRecoveryDelay, float guardPoiseRecoveryRate, float guardBreakDuration, float currentPoise, float currentGuardPoise, float staggerMoveDistance, float guardBreakMoveDistance)
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
        MaxPoise = maxPoise;
        PoiseRecoveryDelay = poiseRecoveryDelay;
        PoiseRecoveryRate = poiseRecoveryRate;
        StaggerDuration = staggerDuration;
        MaxGuardPoise = maxGuardPoise;
        GuardPoiseRecoveryDelay = guardPoiseRecoveryDelay;
        GuardPoiseRecoveryRate = guardPoiseRecoveryRate;
        GuardBreakDuration = guardBreakDuration;
        CurrentPoise = currentPoise;
        CurrentGuardPoise = currentGuardPoise;
        StaggerMoveDistance = Mathf.Max(0f, staggerMoveDistance);
        GuardBreakMoveDistance = Mathf.Max(0f, guardBreakMoveDistance);
    }
}

[DisallowMultipleComponent]
public class PlayerStat : MonoBehaviour
{
    [SerializeField, Min(1)] private int _level = 1;
    [SerializeField, Min(0)] private int _currentExp;
    [SerializeField, Min(1)] private int _targetExp = 100; // 추후 계산식 적용
    // 체력은 소수점 피해와 회복량을 그대로 보존한다.
    [SerializeField, Min(1f)] private float _maxHealth = 100f;
    [SerializeField, Min(0f)] private float _currentHealth = 100f;

    [SerializeField, Min(0f)] private float _baseAttackPower;
    [SerializeField, Min(0f)] private float _baseDefensePower;
    [SerializeField, Min(0f)] private float _baseMaxEnergy = 100f;
    [SerializeField, Min(0f)] private float _currentEnergy;

    // 일반 강인도의 최대치, 회복 설정과 소진 시 경직 시간이다.
    [SerializeField, Min(0f)] private float _maxPoise = 100f;
    [SerializeField, Min(0f)] private float _poiseRecoveryDelay = 3f;
    [SerializeField, Min(0f)] private float _poiseRecoveryRate = 20f;
    [SerializeField, Min(0f)] private float _staggerDuration = 0.5f;
    // 경직 시간 동안 밀려나는 총 거리이다.
    [SerializeField, Min(0f)] private float _staggerMoveDistance = 0.5f;

    // 가드 강인도는 별도의 회복 설정과 가드 브레이크 시간을 사용한다.
    [SerializeField, Min(0f)] private float _maxGuardPoise = 100f;
    [SerializeField, Min(0f)] private float _guardPoiseRecoveryDelay = 3f;
    [SerializeField, Min(0f)] private float _guardPoiseRecoveryRate = 20f;
    [SerializeField, Min(0f)] private float _guardBreakDuration = 1.5f;
    // 가드 브레이크 시간 동안 밀려나는 총 거리이다.
    [SerializeField, Min(0f)] private float _guardBreakMoveDistance = 1f;

    public event Action<PlayerStatSnapshot> StatsChanged;

    public float CurrentPoise { get; private set; }
    public float CurrentGuardPoise { get; private set; }

    // 두 강인도는 마지막으로 피해를 받은 시점부터 각각 회복을 기다린다.
    private float _poiseRecoveryRemaining;
    private float _guardPoiseRecoveryRemaining;

    public PlayerStatSnapshot CurrentStats => new(_level, _currentExp, _targetExp, _maxHealth, _currentHealth, _baseAttackPower, _baseDefensePower, _baseMaxEnergy, _currentEnergy, _maxPoise, _poiseRecoveryDelay, _poiseRecoveryRate, _staggerDuration, _maxGuardPoise, _guardPoiseRecoveryDelay, _guardPoiseRecoveryRate, _guardBreakDuration, CurrentPoise, CurrentGuardPoise, _staggerMoveDistance, _guardBreakMoveDistance);

    private void Awake()
    {
        _currentHealth = _maxHealth;
        _currentEnergy = 0f;

        CurrentPoise = Mathf.Max(0f, _maxPoise);
        CurrentGuardPoise = Mathf.Max(0f, _maxGuardPoise);
        _poiseRecoveryRemaining = 0f;
        _guardPoiseRecoveryRemaining = 0f;
    }

    public void TakePoiseDamage(float amount)
    {
        if (amount <= 0f || _currentHealth <= 0f) return;

        // 이미 0이어도 추가 강인도 피해를 받으면 회복 대기를 다시 시작한다.
        _poiseRecoveryRemaining = Mathf.Max(0f, _poiseRecoveryDelay);

        float nextPoise = Mathf.Max(0f, CurrentPoise - amount);

        if (nextPoise == CurrentPoise) return;

        CurrentPoise = nextPoise;
        NotifyStatsChanged();
    }

    public void TakeGuardPoiseDamage(float amount)
    {
        if (amount <= 0f || _currentHealth <= 0f) return;

        _guardPoiseRecoveryRemaining = Mathf.Max(0f, _guardPoiseRecoveryDelay);

        float nextGuardPoise = Mathf.Max(0f, CurrentGuardPoise - amount);

        if (nextGuardPoise == CurrentGuardPoise) return;

        CurrentGuardPoise = nextGuardPoise;
        NotifyStatsChanged();
    }

    public void UpdatePoiseRecovery(float deltaTime)
    {
        if (deltaTime <= 0f || _currentHealth <= 0f) return;

        float nextPoise = RecoverPoise(CurrentPoise, _maxPoise, _poiseRecoveryRate, ref _poiseRecoveryRemaining, deltaTime);
        float nextGuardPoise = RecoverPoise(CurrentGuardPoise, _maxGuardPoise, _guardPoiseRecoveryRate, ref _guardPoiseRecoveryRemaining, deltaTime);

        if (nextPoise == CurrentPoise && nextGuardPoise == CurrentGuardPoise) return;

        CurrentPoise = nextPoise;
        CurrentGuardPoise = nextGuardPoise;

        // 두 수치가 같은 프레임에 회복되어도 변경 이벤트는 한 번만 보낸다.
        NotifyStatsChanged();
    }

    private static float RecoverPoise(float current, float maximum, float recoveryRate, ref float recoveryRemaining, float deltaTime)
    {
        // 이번 프레임 중 대기 시간에 사용한 부분을 회복 시간에서 제외한다.
        float waitTime = Mathf.Min(recoveryRemaining, deltaTime);
        recoveryRemaining -= waitTime;

        float recoveryTime = deltaTime - waitTime;
        float recoveryAmount = Mathf.Max(0f, recoveryRate) * recoveryTime;

        return Mathf.Clamp(current + recoveryAmount, 0f, Mathf.Max(0f, maximum));
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

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || _currentHealth <= 0f)
        {
            return;
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        NotifyStatsChanged();
    }

    public void RestoreHealth(float amount)
    {
        if (amount <= 0f || _currentHealth >= _maxHealth)
        {
            return;
        }

        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        NotifyStatsChanged();
    }

    public void SetMaxHealth(float maxHealth)
    {
        float clampedMaxHealth = Mathf.Max(1f, maxHealth);

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
