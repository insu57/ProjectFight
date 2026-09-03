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

    private void Awake()
    {
        _currentHealth = _baseMaxHealth;
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
