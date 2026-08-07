using UnityEngine;


public enum RoomType
{
    Start,
    Battle, Reward , 
    Boss, Next
}

[CreateAssetMenu(fileName = "RoomDefinition", menuName = "Scriptable Objects/RoomDefinition")]
public class RoomDefinition : ScriptableObject
{
    [field: SerializeField] public Room RoomPrefab { get; private set; }
    [field: SerializeField] public int StageID { get; private set; } = 0;
    [field: SerializeField] public RoomType RoomType { get; private set; }
    [field: SerializeField] public Vector2 RoomSpacing { get; private set; } = new(10, 10);
    
}
