using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyStat : MonoBehaviour
{
    [SerializeField, Min(1f)] private float _baseMaxHealth = 100f;
    [SerializeField, Min(0f)] private float _currentHealth;

    public event Action<float, float> HealthChanged;
    public event Action Died;

    public float MaxHealth => _baseMaxHealth;
    public float CurrentHealth => _currentHealth;
    public bool IsDead => _currentHealth <= 0f;

    public float MaxPoise { get; private set; }
    public float PoiseRecoveryDelay { get; private set; }
    public float PoiseRecoveryRate { get; private set; }
    public float StaggerDuration { get; private set; }
    public float StaggerMoveDistance { get; private set; }

    public float MaxGuardPoise { get; private set; }
    public float GuardPoiseRecoveryDelay { get; private set; }
    public float GuardPoiseRecoveryRate { get; private set; }
    public float GuardBreakDuration { get; private set; }
    public float GuardBreakMoveDistance { get; private set; }

    [field: SerializeField] public float CurrentPoise { get; private set; }
    [field: SerializeField] public float CurrentGuardPoise { get; private set; }

    // 일반 강인도와 가드 강인도의 회복 대기를 독립적으로 관리한다.
    private float _poiseRecoveryRemaining;
    private float _guardPoiseRecoveryRemaining;

    private void Awake()
    {
        _currentHealth = _baseMaxHealth;
    }

    public void InitializePoiseStats(EnemyDefinition definition)
    {
        if (!definition) return;

        // 공유 에셋의 설정을 개별 적의 런타임 스탯으로 복사한다.
        MaxPoise = Mathf.Max(0f, definition.MaxPoise);
        PoiseRecoveryDelay = Mathf.Max(0f, definition.PoiseRecoveryDelay);
        PoiseRecoveryRate = Mathf.Max(0f, definition.PoiseRecoveryRate);
        StaggerDuration = Mathf.Max(0f, definition.StaggerDuration);
        StaggerMoveDistance = Mathf.Max(0f, definition.StaggerMoveDistance);

        MaxGuardPoise = Mathf.Max(0f, definition.MaxGuardPoise);
        GuardPoiseRecoveryDelay = Mathf.Max(0f, definition.GuardPoiseRecoveryDelay);
        GuardPoiseRecoveryRate = Mathf.Max(0f, definition.GuardPoiseRecoveryRate);
        GuardBreakDuration = Mathf.Max(0f, definition.GuardBreakDuration);
        GuardBreakMoveDistance = Mathf.Max(0f, definition.GuardBreakMoveDistance);

        // 적 설정을 초기화할 때 현재 강인도도 최대치로 시작한다.
        CurrentPoise = MaxPoise;
        CurrentGuardPoise = MaxGuardPoise;
        _poiseRecoveryRemaining = 0f;
        _guardPoiseRecoveryRemaining = 0f;
    }

    public void TakePoiseDamage(float amount)
    {
        if (amount <= 0f || IsDead) return;

        _poiseRecoveryRemaining = PoiseRecoveryDelay;
        CurrentPoise = Mathf.Max(0f, CurrentPoise - amount);
    }

    public void TakeGuardPoiseDamage(float amount)
    {
        if (amount <= 0f || IsDead) return;

        _guardPoiseRecoveryRemaining = GuardPoiseRecoveryDelay;
        CurrentGuardPoise = Mathf.Max(0f, CurrentGuardPoise - amount);
    }

    public void UpdatePoiseRecovery(float deltaTime)
    {
        if (deltaTime <= 0f || IsDead) return;

        CurrentPoise = RecoverPoise(CurrentPoise, MaxPoise, PoiseRecoveryRate, ref _poiseRecoveryRemaining, deltaTime);
        CurrentGuardPoise = RecoverPoise(CurrentGuardPoise, MaxGuardPoise, GuardPoiseRecoveryRate, ref _guardPoiseRecoveryRemaining, deltaTime);
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

    public void TakeDamage(float damage)
    {
        if (damage <= 0f || IsDead)
        {
            return;
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        HealthChanged?.Invoke(_currentHealth, _baseMaxHealth);

        if (IsDead)
        {
            Died?.Invoke();
        }
    }

    public void RestoreHealth(float amount)
    {
        if (amount <= 0f || IsDead || _currentHealth >= _baseMaxHealth)
        {
            return;
        }

        _currentHealth = Mathf.Min(_baseMaxHealth, _currentHealth + amount);
        HealthChanged?.Invoke(_currentHealth, _baseMaxHealth);
    }
}
