using System;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    //temp
    [field: SerializeField] private RoomDef _startRoomData;
    [field: SerializeField] private List<RoomDef> _battleRoomData;
    [field: SerializeField] private List<RoomDef> _rewardRoomData;
    [field: SerializeField] private RoomDef _bossRoomData;
    [field: SerializeField] private RoomDef _nextRoomData;

    private Room _startRoom;
    private List<Room> _battleRooms;
    private List<Room> _rewardRooms;
    private Room _bossRoom;
    private Room _nextRoom;
    
    [field: SerializeField] private float _roomDistance = 5f;

    private void Start()
    {
        InitRoomData();
        GenerateStage();    
    }

    private void InitRoomData()
    {
        _startRoom = Instantiate(_startRoomData.RoomPrefab);
        _startRoom.transform.position = Vector3.zero;
        Vector3 spawnPos = Vector3.zero;

        foreach (RoomDef roomData in _battleRoomData) 
        {
            Room room = Instantiate(roomData.RoomPrefab);
            spawnPos.y += roomData.RoomSpacing.y / 2 + _roomDistance;

        }

    }

    private void GenerateStage()
    {

    }
}
