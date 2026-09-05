using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public PlayerManager Player { get; private set; }
    [field: SerializeField] public List<EnemyManager> EnemyList { get; private set; } = new();
    //직렬화->테스트용

    private void Awake()
    {
        Player = FindFirstObjectByType<PlayerManager>();
    }
    private void Start()
    {
        foreach (EnemyManager enemy in EnemyList)
        {
            enemy.SetTarget(Player.transform);
        }
        
    }

}
