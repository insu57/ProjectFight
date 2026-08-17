using UnityEngine;

public class PlayerStat : MonoBehaviour
{
    [field: SerializeField] private int _maxHealth = 100;
    [field: SerializeField] private int _currentHealth = 100;
    //가드 브레이크?


    private void Awake()
    {
        _currentHealth = _maxHealth;
    }
}
