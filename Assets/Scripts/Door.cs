﻿using UnityEngine;

public enum DoorDirection
{
    North,
    East,
    South,
    West
}

public class Door : MonoBehaviour
{
    [field: SerializeField] public DoorDirection Direction { get; private set; }

    [SerializeField] private GameObject _normalWall;
    [SerializeField] private GameObject _doorWall;

    public Door ConnectedDoor { get; private set; }
    public bool IsConnected => ConnectedDoor != null;

    private void Awake()
    {
        // 스테이지를 다시 생성하더라도 소켓은 항상 닫힌 상태에서 시작한다.
        SetWallState(false);
    }

    public bool TryConnect(Door otherDoor) //연결 시도
    {
        if (otherDoor == null || otherDoor == this || IsConnected || otherDoor.IsConnected)
        {
            return false;
        }

        ConnectedDoor = otherDoor;
        otherDoor.ConnectedDoor = this;
        SetWallState(true);
        otherDoor.SetWallState(true);
        return true;
    }

    public void Disconnect() //연결해제
    {
        if (ConnectedDoor == null)
        {
            SetWallState(false);
            return;
        }

        Door other = ConnectedDoor;
        ConnectedDoor = null;
        other.ConnectedDoor = null;
        SetWallState(false);
        other.SetWallState(false);
    }

    public static bool AreOpposite(DoorDirection first, DoorDirection second)
    {
        return first switch
        {
            DoorDirection.North => second == DoorDirection.South,
            DoorDirection.East => second == DoorDirection.West,
            DoorDirection.South => second == DoorDirection.North,
            DoorDirection.West => second == DoorDirection.East,
            _ => false
        };
    }

    private void SetWallState(bool isConnected) //일반 벽 / 문 벽 설정
    {
        if (_normalWall != null)
        {
            _normalWall.SetActive(!isConnected);
        }

        if (_doorWall != null)
        {
            _doorWall.SetActive(isConnected);
        }
    }
}
