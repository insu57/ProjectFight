using UnityEngine;

[CreateAssetMenu(fileName = "StageDefinition", menuName = "Scriptable Objects/StageDefinition")]
public class StageDefinition : ScriptableObject
{
    [field: SerializeField] public int StageID { get; private set; } = 0;
    [Header("RoomDefinition")]
    [field: SerializeField] public RoomDefinition StartRoomDef { get; private set; }
    [field: SerializeField] public RoomDefinition BossRoomDef { get; private set; }
    [field: SerializeField] public RoomDefinition NextRoomDef { get; private set; }
    [field: SerializeField] public RoomDefinition[] BattleRoomsDef { get; private set; }
    [field: SerializeField] public RoomDefinition[] RewardRoomsDef { get; private set; }
    [field: SerializeField] public RoomDefinition[] StoreRoomsDef { get; private set; }
    //추가: 상점 등 ...

    [Header("Modules")]
    [field: SerializeField] public float RoomDistance { get; private set; } = 5f;
    [field: SerializeField] public GameObject NormalCorridorModule { get; private set; }
    [field: SerializeField] public GameObject HalfCorridorModule { get; private set; }
    [field: SerializeField] public GameObject CornerCorridorModule { get; private set; }

    [Header("Main Path")]
    [field: SerializeField] public int MainRoomDepth { get; private set; } = 2; //Battle, Reward
    [field: SerializeField] public int MinMainTurnCount { get; private set; } = 4;
    [field: SerializeField] public int MaxMainTurnCount { get; private set; } = 5;
    //[field: SerializeField] private int _maxMainPathGenerationAttempts = 30;

    [Header("Guranteed Reward Room Count")]
    [field: SerializeField] public int GuaranteedMainRewardRoomCount { get; private set; } = 1;
    [field: SerializeField] public int GuaranteedBranchRewardRoomCount { get; private set; } = 1;

    [Header("Branch")]
    [field: SerializeField] public int MinBranchCount { get; private set; } = 2;
    [field: SerializeField] public int MaxBranchCount { get; private set; } = 3;
    [field: SerializeField] public int MinBranchDepth { get; private set; } = 1;
    [field: SerializeField] public int MaxBranchDepth { get; private set; } = 3;
    [field: SerializeField] public int MaxBranchPerNode { get; private set; } = 2;

    [Header("Loop")]
    [field: SerializeField] public int MinLoopCount { get; private set; } = 3;
    [field: SerializeField] public int MaxLoopCount { get; private set; } = 3;
    [field: SerializeField] public int MinLoopGraphDistance { get; private set; } = 3;
    [field: SerializeField] public int MaxLoopPerNode { get; private set; } = 2;
    [field: SerializeField] public int MaxStraightLoopGridDistance { get; private set; } = 2;
}
