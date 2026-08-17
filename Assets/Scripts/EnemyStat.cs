using UnityEngine;

public class EnemyStat : MonoBehaviour
{
    [field: SerializeField] private int _maxHealth = 100;
    [field: SerializeField] private int _currentHealth;

    private void Awake()
    {
        _maxHealth = _currentHealth;
    }
}
