using System.Collections.Generic;
using UnityEngine;

public enum StageNodeGroup
{
    Fixed,
    Main,
    Branch
}

public enum StageEdgeType
{
    Normal,
    Loop
}

public sealed class StageNode
{
    public StageNodeGroup Group { get; }
    public RoomDefinition RoomData { get; private set; }
    // 방의 논리적인 격자 좌표로, 물리적인 월드 좌표와 분리해서 관리한다.
    public Vector2Int GridPosition { get; }
    public Vector3 Position { get; set; }
    public Room RoomInstance { get; set; }

    public StageNode(StageNodeGroup group, Vector2Int gridPosition, RoomDefinition roomData = null)
    {
        Group = group;
        GridPosition = gridPosition;
        RoomData = roomData;
    }

    public void SetRoomData(RoomDefinition roomData)
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
    public StageEdgeType Type { get; }

    public StageEdge(StageNode from, StageNode to, DoorDirection fromDirection, DoorDirection toDirection, StageEdgeType type)
    {
        From = from;
        To = to;
        FromDirection = fromDirection;
        ToDirection = toDirection;
        Type = type;
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

    public StageNode AddNode(StageNodeGroup group, Vector2Int gridPosition, RoomDefinition roomData = null)
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

    public bool HasEdge(StageNode first, StageNode second)
    {
        for (int index = 0; index < _edges.Count; index++)
        {
            StageEdge edge = _edges[index];

            if (ReferenceEquals(edge.From, first) && ReferenceEquals(edge.To, second) ||
                ReferenceEquals(edge.From, second) && ReferenceEquals(edge.To, first))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsDirectionUsed(StageNode node, DoorDirection direction)
    {
        for (int index = 0; index < _edges.Count; index++)
        {
            StageEdge edge = _edges[index];

            if (ReferenceEquals(edge.From, node) && edge.FromDirection == direction ||
                ReferenceEquals(edge.To, node) && edge.ToDirection == direction)
            {
                return true;
            }
        }

        return false;
    }

    public int CountEdges(StageNode node, StageEdgeType type)
    {
        int count = 0;

        for (int index = 0; index < _edges.Count; index++)
        {
            StageEdge edge = _edges[index];

            if (edge.Type == type && (ReferenceEquals(edge.From, node) || ReferenceEquals(edge.To, node)))
            {
                count++;
            }
        }

        return count;
    }

    public int GetShortestDistance(StageNode from, StageNode to, int[] queue, int[] distances)
    {
        //BFS로 최단거리 계산
        int fromIndex = _nodes.IndexOf(from);
        int toIndex = _nodes.IndexOf(to);

        if (fromIndex < 0 || toIndex < 0 || queue.Length < _nodes.Count || distances.Length < _nodes.Count)
        {
            return -1;
        }

        for (int index = 0; index < _nodes.Count; index++)
        {
            distances[index] = -1;
        }

        int readIndex = 0;
        int writeIndex = 0;

        queue[writeIndex++] = fromIndex;
        distances[fromIndex] = 0;

        while (readIndex < writeIndex)
        {
            int currentIndex = queue[readIndex++];

            if (currentIndex == toIndex)
            {
                return distances[currentIndex];
            }

            StageNode currentNode = _nodes[currentIndex];

            for (int edgeIndex = 0; edgeIndex < _edges.Count; edgeIndex++)
            {
                StageEdge edge = _edges[edgeIndex];
                StageNode neighbor = null;

                if (ReferenceEquals(edge.From, currentNode))
                {
                    neighbor = edge.To;
                }
                else if (ReferenceEquals(edge.To, currentNode))
                {
                    neighbor = edge.From;
                }

                if (neighbor == null)
                {
                    continue;
                }

                int neighborIndex = _nodes.IndexOf(neighbor);

                if (neighborIndex < 0 || distances[neighborIndex] >= 0)
                {
                    continue;
                }

                distances[neighborIndex] = distances[currentIndex] + 1;
                queue[writeIndex++] = neighborIndex;
            }
        }

        return -1;
    }

    public void AddEdge(StageNode from, StageNode to, DoorDirection fromDirection, DoorDirection toDirection, StageEdgeType type = StageEdgeType.Normal)
    {
        _edges.Add(new StageEdge(from, to, fromDirection, toDirection, type));
    }

    public static Vector2Int GetGridOffset(DoorDirection direction)
    {
        return direction switch
        {
            DoorDirection.North => Vector2Int.up,
            DoorDirection.East => Vector2Int.right,
            DoorDirection.South => Vector2Int.down,
            DoorDirection.West => Vector2Int.left,
            _ => Vector2Int.zero
        };
    }

    public static DoorDirection GetOppositeDirection(DoorDirection direction)
    {
        return direction switch
        {
            DoorDirection.North => DoorDirection.South,
            DoorDirection.East => DoorDirection.West,
            DoorDirection.South => DoorDirection.North,
            DoorDirection.West => DoorDirection.East,
            _ => direction
        };
    }

    public static DoorDirection GetRandomTurnDirection(DoorDirection currentDirection)
    {
        return currentDirection switch
        {
            DoorDirection.North => Random.value < 0.5f ? DoorDirection.East : DoorDirection.West,
            DoorDirection.East => DoorDirection.North,
            DoorDirection.West => DoorDirection.North,
            _ => DoorDirection.North
        };
    }
}
