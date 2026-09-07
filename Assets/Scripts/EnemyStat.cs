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

    public float MaxGuardPoise { get; private set; }
    public float GuardPoiseRecoveryDelay { get; private set; }
    public float GuardPoiseRecoveryRate { get; private set; }
    public float GuardBreakDuration { get; private set; }

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

        MaxGuardPoise = Mathf.Max(0f, definition.MaxGuardPoise);
        GuardPoiseRecoveryDelay = Mathf.Max(0f, definition.GuardPoiseRecoveryDelay);
        GuardPoiseRecoveryRate = Mathf.Max(0f, definition.GuardPoiseRecoveryRate);
        GuardBreakDuration = Mathf.Max(0f, definition.GuardBreakDuration);
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
