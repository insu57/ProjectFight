using UnityEngine;

public class PlayerStat : MonoBehaviour
{
    private const float FloatMax = float.MaxValue;
    [field: SerializeField] private int _maxHealth = 100;
    [field: SerializeField] private int _currentHealth = 100;
    //가드 브레이크?

    [field: SerializeField] private float _baseAttackPower = 0;
    [field: SerializeField] private float _baseDefensePower = 0;
    [field: SerializeField] private float _baseMaxEnergy = 100;
    [field: SerializeField] private float _currentEnergy = 0;


    private void Awake()
    {
        _currentHealth = _maxHealth;
        _currentEnergy = 0;
    }
}
