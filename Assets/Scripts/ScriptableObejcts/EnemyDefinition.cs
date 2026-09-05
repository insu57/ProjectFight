using UnityEngine;


[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Enemy/EnemyDefinition")]
public class EnemyDefinition : ScriptableObject
{
    [field: SerializeField] public string EnemyName { get; private set; }
    [field: SerializeField] public int EnemyID { get; private set; }
    [field: SerializeField] public GameObject Model { get; private set; }
    [field: SerializeField] public EnemyPatternDefinition[] Patterns { get; private set; }
}
