using UnityEngine;


public enum RoomType
{
    Start,
    Battle, Reward , 
    Boss, Next
}

[CreateAssetMenu(fileName = "RoomDef", menuName = "Scriptable Objects/RoomDef")]
public class RoomDef : ScriptableObject
{
    [field: SerializeField] public Room RoomPrefab { get; private set; }
    [field: SerializeField] public int StageID { get; private set; } = 0;
    [field: SerializeField] public RoomType RoomType { get; private set; }
    [field: SerializeField] public Vector2 RoomSpacing { get; private set; } = new(10, 10);
    
}
