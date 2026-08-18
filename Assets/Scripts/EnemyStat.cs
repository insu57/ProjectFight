using UnityEngine;

public class EnemyStat : MonoBehaviour
{
    [field: SerializeField] private float _baseMaxHealth = 100;
    [field: SerializeField] private float _currentHealth;

    private void Awake()
    {
        _baseMaxHealth = _currentHealth;
      
    }
}
