using System.Collections.Generic;
using UnityEngine;

public enum StageNodeGroup
{
    Fixed,
    Main,
    Branch
}

public sealed class StageNode
{
    public StageNodeGroup Group { get; }
    public RoomDef RoomData { get; private set; }
    // 방의 논리적인 격자 좌표로, 물리적인 월드 좌표와 분리해서 관리한다.
    public Vector2Int GridPosition { get; }
    public Vector3 Position { get; set; }
    public Room RoomInstance { get; set; }

    public StageNode(StageNodeGroup group, Vector2Int gridPosition, RoomDef roomData = null)
    {
        Group = group;
        GridPosition = gridPosition;
        RoomData = roomData;
    }

    public void SetRoomData(RoomDef roomData)
    {
        RoomData = roomData;
    }
}

public readonly struct StageEdge
{
    public StageNode From { get; }
    public StageNode To { get; }
    public DoorDirection FromDirection { get; }
    public DoorDirection ToDirection { get; }

    public StageEdge( StageNode from, StageNode to, DoorDirection fromDirection, DoorDirection toDirection)
    {
        From = from;
        To = to;
        FromDirection = fromDirection;
        ToDirection = toDirection;
    }
}

public sealed class StageGraph
{
    private readonly List<StageNode> _nodes;
    private readonly List<StageEdge> _edges;

    public IReadOnlyList<StageNode> Nodes => _nodes;
    public IReadOnlyList<StageEdge> Edges => _edges;

    public StageGraph(int nodeCapacity)
    {
        _nodes = new List<StageNode>(nodeCapacity);
        _edges = new List<StageEdge>(Mathf.Max(0, nodeCapacity - 1));
    }

    public StageNode AddNode(StageNodeGroup group, Vector2Int gridPosition, RoomDef roomData = null)
    {
        var node = new StageNode(group, gridPosition, roomData);
        _nodes.Add(node);
        return node;
    }

    public bool ContainsNode(Vector2Int gridPosition)
    {
        for (int index = 0; index < _nodes.Count; index++)
        {
            if (_nodes[index].GridPosition == gridPosition)
            {
                return true;
            }
        }

        return false;
    }

    public void AddEdge(StageNode from,StageNode to, DoorDirection fromDirection, DoorDirection toDirection)

    {
        _edges.Add(new StageEdge(from, to, fromDirection, toDirection));
    }
}
