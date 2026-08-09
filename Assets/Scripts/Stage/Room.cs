using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    [field: SerializeField] public Door[] Doors { get; private set; }
    [field: SerializeField] public Transform CorridorParent { get; private set; }

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
