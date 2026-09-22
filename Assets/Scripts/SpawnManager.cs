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
    public EnemyManager SpawnEnemy(EnemyManager prefab, Vector3 position, Quaternion rotation)
    {
        PoolingManager poolingManager = PoolingManager.Instance;

        if (!poolingManager || !Player)
        {
            return null;
        }

        EnemyManager enemy = poolingManager.Spawn(prefab, position, rotation);

        if (enemy)
        {
            enemy.SetTarget(Player.transform);
        }

        return enemy;
    }

    private void Start()
    {
        foreach (EnemyManager enemy in EnemyList)
        {
            enemy.SetTarget(Player.transform);
        }
        
    }

}
