using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    [field: SerializeField] public Door[] Doors { get; private set; }
    [field: SerializeField] public Transform CorridorParent { get; private set; }

    private StageManager _stageManager;

    public void Initialize(StageManager stageManager)
    {
        _stageManager = stageManager;

        if (Doors == null)
        {
            return;
        }

        // 프리팹에 등록된 문을 통해 입장 Trigger까지 소유 관계를 전달한다.
        for (int index = 0; index < Doors.Length; index++)
        {
            if (Doors[index])
            {
                Doors[index].Initialize(this);
            }
        }
    }

    public void NotifyPlayerEntered(Door entrance, PlayerManager player)
    {
        if (!isActiveAndEnabled || !_stageManager || !entrance || !player)
        {
            return;
        }

        // 방은 입장 사실만 전달하고, 스테이지 진행 판단은 매니저가 담당한다.
        _stageManager.NotifyRoomEntered(this, entrance, player);
    }

    public bool TryGetAvailableDoor(DoorDirection direction, out Door door) //해당 방향 문 탐색
    {
        for (int index = 0; index < Doors.Length; index++)
        {
            Door candidate = Doors[index];
            if (candidate != null && candidate.Direction == direction && !candidate.IsConnected) //문 방향, 연결여부 체크
            {
                door = candidate;
                return true;
            }
        }

        door = null;
        return false;
    }
}
