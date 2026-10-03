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
    // CSV에서 변환한 성장표이며 플레이어의 현재 상태는 이 컴포넌트에 보관한다.
    [SerializeField] private PlayerProgressionDefinition _progressionDefinition;
    [SerializeField, Min(1)] private int _level = 1;
    [SerializeField, Min(0)] private int _currentExp;
    [SerializeField, Min(0)] private int _targetExp = 100;
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
        if (TryGetProgressionLevel(_level, out PlayerLevelData levelData))
        {
            ApplyLevelData(levelData, false);
            _currentExp = _targetExp > 0 ? Mathf.Clamp(_currentExp, 0, _targetExp - 1) : 0;
        }

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

    public void RestorePoiseAfterReaction(bool isGuardBreak)
    {
        if (_currentHealth <= 0f) return;

        // 종료된 반응에 해당하는 강인도와 회복 대기만 초기화한다.
        if (isGuardBreak)
        {
            CurrentGuardPoise = Mathf.Max(0f, _maxGuardPoise);
            _guardPoiseRecoveryRemaining = 0f;
        }
        else
        {
            CurrentPoise = Mathf.Max(0f, _maxPoise);
            _poiseRecoveryRemaining = 0f;
        }

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

        if (TryGetProgressionLevel(_level, out PlayerLevelData currentLevel))
        {
            AddProgressionExperience(amount, currentLevel);
            return;
        }

        _targetExp = Mathf.Max(1, _targetExp);

        // 성장표가 연결되지 않은 기존 플레이어는 고정 요구 경험치 동작을 유지한다.
        // 합산은 long으로 처리하여 큰 보상에서도 int 경험치가 음수로 넘치지 않게 한다.
        long totalExperience = (long)_currentExp + amount;
        long gainedLevels = totalExperience / _targetExp;
        _level = (int)Math.Min(int.MaxValue, (long)Mathf.Max(1, _level) + gainedLevels);
        _currentExp = (int)(totalExperience % _targetExp);
        NotifyStatsChanged();
    }

    private void AddProgressionExperience(int amount, PlayerLevelData currentLevel)
    {
        if (currentLevel.Level >= _progressionDefinition.MaxLevel || currentLevel.TargetExp <= 0) return;

        // 각 레벨의 요구 경험치가 다르므로 차례대로 차감하고 다음 레벨의 스탯을 적용한다.
        long totalExperience = (long)Mathf.Max(0, _currentExp) + amount;
        _targetExp = currentLevel.TargetExp;

        while (_targetExp > 0 && totalExperience >= _targetExp && _level < _progressionDefinition.MaxLevel)
        {
            if (!_progressionDefinition.TryGetLevel(_level + 1, out PlayerLevelData nextLevel)) break;

            totalExperience -= _targetExp;
            ApplyLevelData(nextLevel, true);
        }

        // 최대 레벨에서는 경험치를 남기지 않고 더 이상 레벨업하지 않는다.
        _currentExp = _level >= _progressionDefinition.MaxLevel ? 0 : (int)Math.Min(totalExperience, int.MaxValue);
        // 여러 레벨을 한 번에 올려도 완성된 최종 스냅샷만 한 번 전달한다.
        NotifyStatsChanged();
    }

    private bool TryGetProgressionLevel(int level, out PlayerLevelData data)
    {
        if (!_progressionDefinition || _progressionDefinition.MaxLevel <= 0)
        {
            data = default;
            return false;
        }

        int clampedLevel = Mathf.Clamp(level, 1, _progressionDefinition.MaxLevel);
        return _progressionDefinition.TryGetLevel(clampedLevel, out data);
    }

    private void ApplyLevelData(PlayerLevelData data, bool restoreHealthIncrease)
    {
        float healthIncrease = Mathf.Max(0f, data.MaxHealth - _maxHealth);
        _level = data.Level;
        _targetExp = data.TargetExp;
        _maxHealth = data.MaxHealth;
        _baseAttackPower = data.AttackPower;
        _baseDefensePower = data.DefensePower;
        _baseMaxEnergy = data.MaxEnergy;

        // 레벨업으로 늘어난 체력만 보충하고, 사망한 플레이어는 되살리지 않는다.
        if (restoreHealthIncrease && _currentHealth > 0f) _currentHealth = Mathf.Min(_maxHealth, _currentHealth + healthIncrease);
        else _currentHealth = Mathf.Min(_currentHealth, _maxHealth);

        _currentEnergy = Mathf.Min(_currentEnergy, _baseMaxEnergy);
    }

    public void SetLevel(int level, int targetExp)
    {
        int clampedLevel = Mathf.Max(1, level);
        int clampedTargetExp = Mathf.Max(1, targetExp);
        bool hasProgressionData = TryGetProgressionLevel(level, out PlayerLevelData levelData);

        // 레벨 직접 지정 시에도 연결된 성장표의 레벨 범위와 요구 경험치를 사용한다.
        if (hasProgressionData)
        {
            clampedLevel = levelData.Level;
            clampedTargetExp = levelData.TargetExp;
        }

        if (_level == clampedLevel && _targetExp == clampedTargetExp)
        {
            return;
        }

        if (hasProgressionData) ApplyLevelData(levelData, false);
        else
        {
            _level = clampedLevel;
            _targetExp = clampedTargetExp;
        }

        _currentExp = hasProgressionData ? (_targetExp > 0 ? Mathf.Clamp(_currentExp, 0, _targetExp - 1) : 0) : Mathf.Min(_currentExp, _targetExp);
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
