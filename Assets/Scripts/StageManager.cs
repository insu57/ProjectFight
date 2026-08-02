using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;


public class StageManager : MonoBehaviour
{
    //temp
    [field: SerializeField] private RoomDef _startRoomData;
    [field: SerializeField] private List<RoomDef> _battleRoomData;
    [field: SerializeField] private List<RoomDef> _rewardRoomData;
    [field: SerializeField] private RoomDef _bossRoomData;
    [field: SerializeField] private RoomDef _nextRoomData;
    [field: SerializeField] private int _normalRoomDepth = 3; //Battle, Reward
    [field: SerializeField] private int _guranteedRewardRoomCount = 1;

    private Room _startRoom;
    private readonly List<Room> _battleRooms = new();
    private readonly List<Room> _rewardRooms = new();
    private Room _bossRoom;
    private Room _nextRoom;
    
    [field: SerializeField] private float _roomDistance = 5f;
    [SerializeField] private GameObject _corridorModule;

    private void Start()
    {
        //InitRoomData();
        GenerateStage();    
    }

    private void InitRoomData()
    {
       //
    }

    private void GenerateStage()
    {
        _startRoom = Instantiate(_startRoomData.RoomPrefab);
        _startRoom.transform.position = Vector3.zero;
        Vector3 spawnPos = Vector3.zero;
        Room previousRoom = _startRoom;

        int normalRoomCount = Mathf.Max(0, _normalRoomDepth);
        int rewardRoomCount = Mathf.Clamp(_guranteedRewardRoomCount, 0, normalRoomCount);
        //범위제한

        if (normalRoomCount > rewardRoomCount && _battleRoomData.Count == 0)
        {
            Debug.LogError("BattleRoomData가 비어 있어 스테이지를 생성할 수 없습니다.", this);
            return;
        }

        if (rewardRoomCount > 0 && _rewardRoomData.Count == 0)
        {
            Debug.LogError("RewardRoomData가 비어 있어 보상 방을 생성할 수 없습니다.", this);
            return;
        }

        int remainingRewardRoomCount = rewardRoomCount;

        for (int depth = 0; depth < normalRoomCount; depth++)
        {
            int remainingRoomCount = normalRoomCount - depth;

            // 남은 칸 수에 비례해 선택하여 보상 방의 위치는 무작위로, 개수는 정확히 보장한다.
            bool isRewardRoom = Random.Range(0, remainingRoomCount) < remainingRewardRoomCount;

            List<RoomDef> roomDataList = isRewardRoom ? _rewardRoomData : _battleRoomData;
            RoomDef roomData = roomDataList[Random.Range(0, roomDataList.Count)];

            Room room = Instantiate(roomData.RoomPrefab);
            spawnPos.z += roomData.RoomSpacing.y + _roomDistance;
            room.transform.position = spawnPos;

            // 현재 일렬 배치에서는 이전 방의 북쪽 문과 다음 방의 남쪽 문을 연결한다. -> 수정예정.
            TryConnectRooms(previousRoom, DoorDirection.North, room, DoorDirection.South);
            previousRoom = room;

            //보상방인지
            if (isRewardRoom)
            {
                _rewardRooms.Add(room);
                remainingRewardRoomCount--;
            }
            else
            {
                _battleRooms.Add(room);
            }
        }

        spawnPos.z += _bossRoomData.RoomSpacing.y + _roomDistance;
        _bossRoom = Instantiate(_bossRoomData.RoomPrefab);
        _bossRoom.transform.position = spawnPos;
        TryConnectRooms(previousRoom, DoorDirection.North, _bossRoom, DoorDirection.South);
        previousRoom = _bossRoom;

        spawnPos.z += _nextRoomData.RoomSpacing.y + _roomDistance;
        _nextRoom = Instantiate(_nextRoomData.RoomPrefab);
        _nextRoom.transform.position = spawnPos;
        TryConnectRooms(previousRoom, DoorDirection.North, _nextRoom, DoorDirection.South);

        //거리관련 수정필요(동서남북도 가능하도록)
    }

    private bool TryConnectRooms(Room fromRoom, DoorDirection fromDirection, Room toRoom, DoorDirection toDirection)
    {
        if (!fromRoom.TryGetAvailableDoor(fromDirection, out Door fromDoor) || //가능여부 확인
            !toRoom.TryGetAvailableDoor(toDirection, out Door toDoor))
        {
            Debug.LogError(
                $"{fromRoom.name} 또는 {toRoom.name}에서 연결 가능한 문을 찾지 못했습니다.",
                this);
            return false;
        }

        if (!fromDoor.TryConnect(toDoor))
        {
            return false;
        }

        if (Corridor.TryGenerate(_corridorModule, fromDoor, toDoor, fromRoom.CorridorParent)) //복도 생성
        {
            return true;
        }

        // 복도 생성 실패 시 문만 열린 연결이 남지 않도록 원래 상태로 되돌린다.
        fromDoor.Disconnect();
        return false;
    }
}
