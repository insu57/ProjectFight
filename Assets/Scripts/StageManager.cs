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
    [field: SerializeField] private int _mainRoomDepth = 4; //Battle, Reward

    [Header("Guranteed Reward Room Count")]
    [field: SerializeField] private int _guaranteedMainRewardRoomCount = 1;
    [field: SerializeField] private int _guaranteedBranchRewardRoomCount = 1;

    [Header("Branch")]
    [field: SerializeField] private int _minBranchCount = 1;
    [field: SerializeField] private int _maxBranchCount = 2;
    [field: SerializeField] private int _minBranchDepth = 1;
    [field: SerializeField] private int _maxBranchDepth = 2;
    [field: SerializeField] private int _maxBranchPerNode = 1;


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
        if (!TryGenerateLinearGraph(out StageGraph graph))
        {
            return;
        }

        LayoutGraph(graph);
        InstantiateRooms(graph);
        ConnectGraph(graph);
    }

    private bool TryGenerateLinearGraph(out StageGraph graph)
    {
        graph = null;

        if (_startRoomData == null || _startRoomData.RoomPrefab == null ||
            _bossRoomData == null || _bossRoomData.RoomPrefab == null ||
            _nextRoomData == null || _nextRoomData.RoomPrefab == null)
        {
            Debug.LogError("시작, 보스 또는 다음 스테이지 방 데이터가 올바르게 설정되지 않았습니다.", this);
            return false;
        }

        int normalRoomCount = Mathf.Max(0, _mainRoomDepth);
        int maxBranchCount = Mathf.Max(0, Mathf.Max(_minBranchCount, _maxBranchCount));
        int maxBranchDepth = Mathf.Max(1, _maxBranchDepth);
        int graphCapacity = normalRoomCount + 3 + maxBranchCount * maxBranchDepth;

        // 실제 오브젝트를 만들기 전에 스테이지의 논리적인 연결 구조를 완성한다.
        graph = new StageGraph(graphCapacity);
        StageNode previousNode = graph.AddNode(StageNodeGroup.Fixed, _startRoomData);
        var branchRootCandidates = new List<StageNode>(normalRoomCount);

        for (int depth = 0; depth < normalRoomCount; depth++)
        {
            // 방 종류는 전체 그래프를 완성한 뒤 인접 제한과 보상방 수를 함께 검사해 결정한다.
            StageNode node = graph.AddNode(StageNodeGroup.Main);
            graph.AddEdge(previousNode, node, DoorDirection.North, DoorDirection.South);
            previousNode = node;

            // 메인 경로의 전투방과 보상방 모두 분기의 시작점이 될 수 있다.
            branchRootCandidates.Add(node);
        }

        StageNode bossNode = graph.AddNode(StageNodeGroup.Fixed, _bossRoomData);
        graph.AddEdge(previousNode, bossNode, DoorDirection.North, DoorDirection.South);

        StageNode nextNode = graph.AddNode(StageNodeGroup.Fixed, _nextRoomData);
        graph.AddEdge(bossNode, nextNode, DoorDirection.North, DoorDirection.South);

        // 메인 경로를 먼저 완성해 배치 단계에서 모든 분기 부모의 위치가 먼저 결정되도록 한다.
        if (!TryAddBranches(graph, branchRootCandidates))
        {
            graph = null;
            return false;
        }

        if (!TryAssignNormalRoomData(graph))
        {
            graph = null;
            return false;
        }

        return true;
    }

    private bool TryAddBranches(StageGraph graph, List<StageNode> branchRootCandidates)
    {
        if (branchRootCandidates.Count == 0)
        {
            return true;
        }

        int minBranchCount = Mathf.Max(0, _minBranchCount);
        int maxBranchCount = Mathf.Max(minBranchCount, _maxBranchCount);
        int branchCount = Mathf.Min(Random.Range(minBranchCount, maxBranchCount + 1), branchRootCandidates.Count);

        if (branchCount == 0)
        {
            return true;
        }

        int minBranchDepth = Mathf.Max(1, _minBranchDepth);
        int maxBranchDepth = Mathf.Max(minBranchDepth, _maxBranchDepth);

        ShuffleNodes(branchRootCandidates);

        for (int branchIndex = 0; branchIndex < branchCount; branchIndex++)
        {
            StageNode previousNode = branchRootCandidates[branchIndex];

            DoorDirection branchDirection = Random.value < 0.5f
                ? DoorDirection.East : DoorDirection.West;

            DoorDirection entranceDirection = branchDirection == DoorDirection.East
                ? DoorDirection.West : DoorDirection.East;

            int branchDepth = Random.Range(minBranchDepth, maxBranchDepth + 1);

            for (int depth = 0; depth < branchDepth; depth++)
            {
                StageNode branchNode = graph.AddNode(StageNodeGroup.Branch);
                graph.AddEdge(previousNode, branchNode, branchDirection, entranceDirection);
                previousNode = branchNode;
            }
        }

        return true;
    }

    private bool TryAssignNormalRoomData(StageGraph graph)
    {
        var normalNodes = new List<StageNode>(graph.Nodes.Count);
        var mainNodes = new List<StageNode>(graph.Nodes.Count);
        var branchNodes = new List<StageNode>(graph.Nodes.Count);

        for (int index = 0; index < graph.Nodes.Count; index++)
        {
            StageNode node = graph.Nodes[index];

            if (node.Group == StageNodeGroup.Fixed)
            {
                continue;
            }

            normalNodes.Add(node);

            if (node.Group == StageNodeGroup.Main)
            {
                mainNodes.Add(node);
            }
            else
            {
                branchNodes.Add(node);
            }
        }

        var rewardCandidates = new List<StageNode>(normalNodes.Count);

        // 시작방 다음과 보스방 직전의 일반방은 항상 전투방으로 남긴다.
        for (int index = 1; index < mainNodes.Count - 1; index++)
        {
            rewardCandidates.Add(mainNodes[index]);
        }

        // 분기 일반방은 위치와 관계없이 모두 보상방 후보가 된다.
        rewardCandidates.AddRange(branchNodes);

        int availableMainRewardCount = Mathf.Max(0, mainNodes.Count - 2);
        int mainRewardRoomCount = Mathf.Max(0, _guaranteedMainRewardRoomCount);
        int branchRewardRoomCount = Mathf.Max(0, _guaranteedBranchRewardRoomCount);

        if (mainRewardRoomCount > availableMainRewardCount ||
            branchRewardRoomCount > branchNodes.Count)
        {
            Debug.LogError("끝 일반방 제한으로 인해 요청한 보상방 수를 배치할 수 없습니다.", this);
            return false;
        }

        ShuffleNodes(rewardCandidates);
        var rewardNodes = new HashSet<StageNode>();

        if (!TrySelectRewardNodes(graph, rewardCandidates, 0, mainRewardRoomCount,
                branchRewardRoomCount, rewardNodes))
        {
            Debug.LogError("요청한 보상방 수와 인접 제한을 동시에 만족할 수 없습니다.", this);
            return false;
        }

        for (int index = 0; index < normalNodes.Count; index++)
        {
            StageNode node = normalNodes[index];

            List<RoomDef> roomDataList = rewardNodes.Contains(node)
                ? _rewardRoomData : _battleRoomData;

            if (!TryGetRandomRoomData(roomDataList, out RoomDef roomData))
            {
                return false;
            }

            node.SetRoomData(roomData);
        }

        return true;
    }

    private bool TrySelectRewardNodes(StageGraph graph, List<StageNode> candidates, int candidateIndex,
        int remainingMainRewardCount, int remainingBranchRewardCount, HashSet<StageNode> rewardNodes)
    {
        //백트래킹으로 체크
        if (remainingMainRewardCount == 0 && remainingBranchRewardCount == 0) //남은 보상방 없음
        {
            return true;
        }

        if (candidateIndex >= candidates.Count ||
            CountRemainingNodes(candidates, candidateIndex, StageNodeGroup.Main) < remainingMainRewardCount ||
            CountRemainingNodes(candidates, candidateIndex, StageNodeGroup.Branch) < remainingBranchRewardCount)
        {
            return false; //후보 부족으로 실패(보상방 보장수 달성 불가)
        }

        StageNode node = candidates[candidateIndex];
        int remainingRewardCount = node.Group == StageNodeGroup.Main
            ? remainingMainRewardCount : remainingBranchRewardCount;

        if (remainingRewardCount > 0 && !HasRewardNeighbor(graph, node, rewardNodes))
        {
            rewardNodes.Add(node);

            int nextMainRewardCount = remainingMainRewardCount - (node.Group == StageNodeGroup.Main ? 1 : 0);
            int nextBranchRewardCount = remainingBranchRewardCount - (node.Group == StageNodeGroup.Branch ? 1 : 0);

            if (TrySelectRewardNodes(graph, candidates, candidateIndex + 1,
                nextMainRewardCount, nextBranchRewardCount, rewardNodes))
            {
                return true;
            }

            rewardNodes.Remove(node);
        }

        return TrySelectRewardNodes(graph, candidates, candidateIndex + 1,
             remainingMainRewardCount, remainingBranchRewardCount, rewardNodes);
    }

    private static int CountRemainingNodes(List<StageNode> candidates, int startIndex, StageNodeGroup group)
    {
        int count = 0;

        for (int index = startIndex; index < candidates.Count; index++)
        {
            if (candidates[index].Group == group)
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasRewardNeighbor(StageGraph graph, StageNode node, HashSet<StageNode> rewardNodes)
    {
        for (int index = 0; index < graph.Edges.Count; index++)
        {
            StageEdge edge = graph.Edges[index];

            if (edge.From == node && rewardNodes.Contains(edge.To) ||
                edge.To == node && rewardNodes.Contains(edge.From))
            {
                return true;
            }
        }

        return false;
    }

    private static void ShuffleNodes(List<StageNode> candidates)
    {
        // 별도의 임시 목록을 만들지 않고 후보 목록 자체를 무작위로 섞는다.
        for (int index = candidates.Count - 1; index > 0; index--)
        {
            int randomIndex = Random.Range(0, index + 1);
            StageNode temporary = candidates[index];
            candidates[index] = candidates[randomIndex];
            candidates[randomIndex] = temporary;
        }
    }

    private bool TryGetRandomRoomData(List<RoomDef> roomDataList, out RoomDef roomData)
    {
        roomData = null;

        if (roomDataList == null || roomDataList.Count == 0)
        {
            Debug.LogError("일반 방에 사용할 방 데이터 목록이 비어 있습니다.", this);
            return false;
        }

        roomData = roomDataList[Random.Range(0, roomDataList.Count)];

        if (roomData == null || roomData.RoomPrefab == null)
        {
            Debug.LogError("일반 방 목록에 유효하지 않은 RoomDef가 포함되어 있습니다.", this);
            roomData = null;
            return false;
        }

        return true;
    }

    private void LayoutGraph(StageGraph graph)
    {
        graph.Nodes[0].Position = Vector3.zero;

        for (int index = 0; index < graph.Edges.Count; index++)
        {
            StageEdge edge = graph.Edges[index];
            edge.To.Position = CalculateConnectedPosition(edge);
        }
    }

    private Vector3 CalculateConnectedPosition(StageEdge edge)
    {
        Vector2 fromSpacing = edge.From.RoomData.RoomSpacing;
        Vector2 toSpacing = edge.To.RoomData.RoomSpacing;

        // 두 방 크기의 절반씩과 복도 길이를 더해 방 중심 사이의 거리를 구한다.
        return edge.FromDirection switch
        {
            DoorDirection.North => edge.From.Position +
                Vector3.forward * ((fromSpacing.y + toSpacing.y) * 0.5f + _roomDistance),
            DoorDirection.East => edge.From.Position +
                Vector3.right * ((fromSpacing.x + toSpacing.x) * 0.5f + _roomDistance),
            DoorDirection.South => edge.From.Position +
                Vector3.back * ((fromSpacing.y + toSpacing.y) * 0.5f + _roomDistance),
            DoorDirection.West => edge.From.Position +
                Vector3.left * ((fromSpacing.x + toSpacing.x) * 0.5f + _roomDistance),
            _ => edge.From.Position
        };
    }

    private void InstantiateRooms(StageGraph graph)
    {
        _battleRooms.Clear();
        _rewardRooms.Clear();

        for (int index = 0; index < graph.Nodes.Count; index++)
        {
            StageNode node = graph.Nodes[index];
            Room room = Instantiate(node.RoomData.RoomPrefab, node.Position, Quaternion.identity);
            node.RoomInstance = room;

            switch (node.RoomData.RoomType)
            {
                case RoomType.Start:
                    _startRoom = room;
                    break;
                case RoomType.Battle:
                    _battleRooms.Add(room);
                    break;
                case RoomType.Reward:
                    _rewardRooms.Add(room);
                    break;
                case RoomType.Boss:
                    _bossRoom = room;
                    break;
                case RoomType.Next:
                    _nextRoom = room;
                    break;
            }
        }
    }

    private void ConnectGraph(StageGraph graph)
    {
        for (int index = 0; index < graph.Edges.Count; index++)
        {
            StageEdge edge = graph.Edges[index];

            if (!TryConnectRooms(edge.From.RoomInstance, edge.FromDirection, edge.To.RoomInstance, edge.ToDirection))
            {
                Debug.LogError($"스테이지 그래프의 {index}번 간선을 연결하지 못했습니다.", this);
                return;
            }
        }
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
