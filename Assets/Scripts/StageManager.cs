using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;


public class StageManager : MonoBehaviour
{
    private static readonly DoorDirection[] _branchDirections =
    {
        DoorDirection.North,
        DoorDirection.East,
        DoorDirection.South,
        DoorDirection.West
    };

    //temp
    [field: SerializeField] private RoomDef _startRoomData;
    [field: SerializeField] private List<RoomDef> _battleRoomData;
    [field: SerializeField] private List<RoomDef> _rewardRoomData;
    [field: SerializeField] private RoomDef _bossRoomData;
    [field: SerializeField] private RoomDef _nextRoomData;

    [Header("Main Path")]
    [field: SerializeField] private int _mainRoomDepth = 3; //Battle, Reward
    [field: SerializeField] private int _minMainTurnCount = 2;
    [field: SerializeField] private int _maxMainTurnCount = 3;
    [field: SerializeField] private int _maxMainPathGenerationAttempts = 30;

    [Header("Guranteed Reward Room Count")]
    [field: SerializeField] private int _guaranteedMainRewardRoomCount = 1;
    [field: SerializeField] private int _guaranteedBranchRewardRoomCount = 1;

    [Header("Branch")]
    [field: SerializeField] private int _minBranchCount = 1;
    [field: SerializeField] private int _maxBranchCount = 2;
    [field: SerializeField] private int _minBranchDepth = 1;
    [field: SerializeField] private int _maxBranchDepth = 2;
    [field: SerializeField] private int _maxBranchPerNode = 2;

    [Header("Loop")]
    [field: SerializeField] private int _minLoopCount = 0;
    [field: SerializeField] private int _maxLoopCount = 2;
    [field: SerializeField] private int _minLoopGraphDistance = 3;
    [field: SerializeField] private int _maxLoopPerNode = 1;

    private enum LoopPathType
    {
        Straight,
        Corner
    }

    private readonly struct LoopCandidate
    {
        public StageNode From { get; }
        public StageNode To { get; }
        public DoorDirection FromDirection { get; }
        public DoorDirection ToDirection { get; }
        public LoopPathType PathType { get; }
        public Vector2Int CornerGridPosition { get; }

        public LoopCandidate(StageNode from, StageNode to, DoorDirection fromDirection, DoorDirection toDirection,
            LoopPathType pathType, Vector2Int cornerGridPosition)
        {
            From = from;
            To = to;
            FromDirection = fromDirection;
            ToDirection = toDirection;
            PathType = pathType;
            CornerGridPosition = cornerGridPosition;
        }
    }

    private Room _startRoom;
    private readonly List<Room> _battleRooms = new();
    private readonly List<Room> _rewardRooms = new();
    private Room _bossRoom;
    private Room _nextRoom;
    
    [field: SerializeField] private float _roomDistance = 5f;
    [field: SerializeField] private GameObject _normalCorridorModule;
    [field: SerializeField] private GameObject _halfCorridorModule;
    [field: SerializeField] private GameObject _cornerCorridorModule;


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

        int minTurnCount = Mathf.Max(0, _minMainTurnCount);
        int maxTurnCount = Mathf.Max(minTurnCount, _maxMainTurnCount);
        int targetTurnCount = Random.Range(minTurnCount, maxTurnCount + 1);

        // 굴절마다 일반방 하나를 추가해 기본 깊이와 굴절용 경로 길이를 함께 확보한다.
        int normalRoomCount = Mathf.Max(0, _mainRoomDepth) + targetTurnCount;

        if (!TryCreateMainPathPlan(normalRoomCount, targetTurnCount, out List<Vector2Int> mainPositions,
            out List<DoorDirection> mainDirections, out Vector2Int nextRoomPosition))
        {
            Debug.LogError($"굴절 {targetTurnCount}회를 만족하는 메인 경로를 생성하지 못했습니다.", this);
            return false;
        }

        // 실제 오브젝트를 만들기 전에 스테이지의 논리적인 연결 구조를 완성한다.
        int maxBranchCount = Mathf.Max(0, Mathf.Max(_minBranchCount, _maxBranchCount));
        int maxBranchDepth = Mathf.Max(1, _maxBranchDepth);
        int graphCapacity = normalRoomCount + 3 + maxBranchCount * maxBranchDepth;
        graph = new StageGraph(graphCapacity);
        StageNode previousNode = graph.AddNode(StageNodeGroup.Fixed, mainPositions[0], _startRoomData);
        var branchRootCandidates = new List<StageNode>(normalRoomCount);

        for (int depth = 0; depth < normalRoomCount; depth++)
        {
            // 방 종류는 전체 그래프를 완성한 뒤 인접 제한과 보상방 수를 함께 검사해 결정한다.
            StageNode node = graph.AddNode(StageNodeGroup.Main, mainPositions[depth + 1]);
            DoorDirection direction = mainDirections[depth];
            graph.AddEdge(previousNode, node, direction, GetOppositeDirection(direction));
            branchRootCandidates.Add(node);
            previousNode = node;
        }

        DoorDirection bossDirection = mainDirections[normalRoomCount];
        StageNode bossNode = graph.AddNode(StageNodeGroup.Fixed, mainPositions[normalRoomCount + 1], _bossRoomData);
        graph.AddEdge(previousNode, bossNode, bossDirection, GetOppositeDirection(bossDirection));

        // 보스방은 일반방 하나와 다음 방 하나에만 연결한다.
        StageNode nextNode = graph.AddNode(StageNodeGroup.Fixed, nextRoomPosition, _nextRoomData);
        graph.AddEdge(bossNode, nextNode, bossDirection, GetOppositeDirection(bossDirection));

        if (!TryAddBranches(graph, branchRootCandidates))
        {
            graph = null;
            return false;
        }

        if (!TryAddLoops(graph))
        {
            Debug.LogWarning($"Fail: TryAddLoops {_minLoopCount}");
            //graph = null;
            //return false;
        }

        if (!TryAssignNormalRoomData(graph))
        {
            graph = null;
            return false;
        }

        return true;
    }

    private bool TryCreateMainPathPlan(int normalRoomCount, int targetTurnCount, out List<Vector2Int> positions,
        out List<DoorDirection> directions, out Vector2Int nextRoomPosition)
    {
        int connectionCount = normalRoomCount + 1;
        int maximumPossibleTurnCount = Mathf.Max(0, connectionCount - 1);

        positions = null;
        directions = null;
        nextRoomPosition = default;

        if (targetTurnCount > maximumPossibleTurnCount)
        {
            return false;
        }

        var plannedPositions = new List<Vector2Int>(normalRoomCount + 2);
        var plannedDirections = new List<DoorDirection>(connectionCount);
        var occupiedPositions = new HashSet<Vector2Int>();
        var turnFlags = new bool[connectionCount];
        var turnCandidateIndices = new int[maximumPossibleTurnCount];
        int attemptCount = Mathf.Max(1, _maxMainPathGenerationAttempts);

        for (int attempt = 0; attempt < attemptCount; attempt++)
        {
            plannedPositions.Clear();
            plannedDirections.Clear();
            occupiedPositions.Clear();

            for (int index = 0; index < turnFlags.Length; index++)
            {
                turnFlags[index] = false;
            }

            for (int index = 0; index < turnCandidateIndices.Length; index++)
            {
                // 첫 연결은 북쪽으로 고정하므로 두 번째 연결부터 굴절 후보가 된다.
                turnCandidateIndices[index] = index + 1;
            }

            ShuffleIndices(turnCandidateIndices);

            for (int index = 0; index < targetTurnCount; index++)
            {
                turnFlags[turnCandidateIndices[index]] = true;
            }

            Vector2Int currentPosition = Vector2Int.zero;
            DoorDirection currentDirection = DoorDirection.North;
            bool pathCompleted = true;

            plannedPositions.Add(currentPosition);
            occupiedPositions.Add(currentPosition);

            for (int connectionIndex = 0; connectionIndex < connectionCount; connectionIndex++)
            {
                if (turnFlags[connectionIndex])
                {
                    currentDirection = GetRandomTurnDirection(currentDirection);
                }

                Vector2Int nextPosition = currentPosition + GetGridOffset(currentDirection);

                // 직전 방을 포함해 기존 메인 경로 좌표로 돌아가는 진행은 허용하지 않는다.
                if (!occupiedPositions.Add(nextPosition))//HashSet에 이미 원소를 추가하면 false 반환
                {
                    pathCompleted = false;
                    break;
                }

                plannedDirections.Add(currentDirection);
                plannedPositions.Add(nextPosition);
                currentPosition = nextPosition;
            }

            if (!pathCompleted)
            {
                continue;
            }

            // 다음 방은 보스방을 통과한 뒤 마지막 진행 방향으로 이어지게 한다.
            Vector2Int candidateNextRoomPosition = currentPosition + GetGridOffset(currentDirection);

            if (occupiedPositions.Contains(candidateNextRoomPosition))
            {
                continue;
            }

            positions = plannedPositions;
            directions = plannedDirections;
            nextRoomPosition = candidateNextRoomPosition;
            return true;
        }

        return false;
    }

    private static DoorDirection GetRandomTurnDirection(DoorDirection currentDirection)
    {
        return currentDirection switch
        {
            DoorDirection.North => Random.value < 0.5f ? DoorDirection.East : DoorDirection.West,
            DoorDirection.East => DoorDirection.North,
            DoorDirection.West => DoorDirection.North,
            _ => DoorDirection.North
        };
    }

    private static Vector2Int GetGridOffset(DoorDirection direction)
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

    private static DoorDirection GetOppositeDirection(DoorDirection direction)
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

    private static void ShuffleIndices(int[] indices)
    {
        for (int index = indices.Length - 1; index > 0; index--)
        {
            int randomIndex = Random.Range(0, index + 1);
            int temporary = indices[index];
            indices[index] = indices[randomIndex];
            indices[randomIndex] = temporary;
        }
    }

    private bool TryAddBranches(StageGraph graph, List<StageNode> branchRootCandidates)
    {
        int minBranchCount = Mathf.Max(0, _minBranchCount);
        int maxBranchPerNode = Mathf.Clamp(_maxBranchPerNode, 0, _branchDirections.Length);

        if (branchRootCandidates.Count == 0 || maxBranchPerNode == 0)
        {
            return minBranchCount == 0;
        }

        int maxBranchCount = Mathf.Max(minBranchCount, _maxBranchCount);
        int maximumPossibleBranchCount = branchRootCandidates.Count * maxBranchPerNode;
        int targetBranchCount = Mathf.Min(Random.Range(minBranchCount, maxBranchCount + 1), maximumPossibleBranchCount);

        if (targetBranchCount == 0)
        {
            return true;
        }

        int minBranchDepth = Mathf.Max(1, _minBranchDepth);
        int maxBranchDepth = Mathf.Max(minBranchDepth, _maxBranchDepth);
        var availableDirections = new DoorDirection[_branchDirections.Length];

        ShuffleNodes(branchRootCandidates);

        int createdBranchCount = 0;

        // 모든 후보 노드에 branch를 하나씩 배치한 뒤 남은 branch가 있으면 다시 순회한다.
        for (int branchIndex = 0; branchIndex < maxBranchPerNode && createdBranchCount < targetBranchCount; branchIndex++)
        {
            for (int rootIndex = 0; rootIndex < branchRootCandidates.Count && createdBranchCount < targetBranchCount; rootIndex++)
            {
                StageNode branchRoot = branchRootCandidates[rootIndex];
                int branchDepth = Random.Range(minBranchDepth, maxBranchDepth + 1);
                int availableDirectionCount = FindAvailableBranchDirections(graph, branchRoot.GridPosition, branchDepth, availableDirections);

                if (availableDirectionCount == 0)
                {
                    continue;
                }

                DoorDirection branchDirection = availableDirections[Random.Range(0, availableDirectionCount)];
                AddBranch(graph, branchRoot, branchDirection, branchDepth);
                createdBranchCount++;
            }
        }

        return createdBranchCount == targetBranchCount;
    }

    private static int FindAvailableBranchDirections(StageGraph graph, Vector2Int rootPosition, int branchDepth, DoorDirection[] results)
    {
        int resultCount = 0;

        for (int directionIndex = 0; directionIndex < _branchDirections.Length; directionIndex++)
        {
            DoorDirection direction = _branchDirections[directionIndex];

            if (!IsBranchPathAvailable(graph, rootPosition, direction, branchDepth))
            {
                continue;
            }

            results[resultCount] = direction;
            resultCount++;
        }

        return resultCount;
    }

    private static bool IsBranchPathAvailable(StageGraph graph, Vector2Int rootPosition, DoorDirection direction, int branchDepth)
    {
        Vector2Int offset = GetGridOffset(direction);
        Vector2Int position = rootPosition;

        for (int depth = 0; depth < branchDepth; depth++)
        {
            position += offset;

            if (graph.ContainsNode(position))
            {
                return false;
            }
        }

        return true;
    }

    private static void AddBranch(StageGraph graph, StageNode branchRoot, DoorDirection direction, int branchDepth)
    {
        Vector2Int offset = GetGridOffset(direction);
        DoorDirection entranceDirection = GetOppositeDirection(direction);
        Vector2Int position = branchRoot.GridPosition;
        StageNode previousNode = branchRoot;

        for (int depth = 0; depth < branchDepth; depth++)
        {
            position += offset;

            StageNode branchNode = graph.AddNode(StageNodeGroup.Branch, position);
            graph.AddEdge(previousNode, branchNode, direction, entranceDirection);
            previousNode = branchNode;
        }
    }

    private bool TryAddLoops(StageGraph graph)
    {
        int minLoopCount = Mathf.Max(0, _minLoopCount);
        int maxLoopCount = Mathf.Max(minLoopCount, _maxLoopCount);
        int targetLoopCount = Random.Range(minLoopCount, maxLoopCount + 1);

        if (targetLoopCount == 0)
        {
            return true;
        }

        var candidates = new List<LoopCandidate>(graph.Nodes.Count * 4);
        var occupiedCornerPositions = new HashSet<Vector2Int>();
        var shortestPathQueue = new int[graph.Nodes.Count];
        var shortestPathDistances = new int[graph.Nodes.Count];

        CollectLoopCandidates(graph, candidates);//루프 후보자 계산
        ShuffleLoopCandidates(candidates);

        int createdLoopCount = 0;

        for (int candidateIndex = 0; candidateIndex < candidates.Count && createdLoopCount < targetLoopCount; candidateIndex++)
        {
            LoopCandidate candidate = candidates[candidateIndex];

            if (!IsLoopCandidateAvailable(graph, candidate, occupiedCornerPositions, shortestPathQueue, shortestPathDistances))
            {
                continue;
            }

            graph.AddEdge(candidate.From, candidate.To, candidate.FromDirection, candidate.ToDirection, StageEdgeType.Loop);

            if (candidate.PathType == LoopPathType.Corner)
            {
                occupiedCornerPositions.Add(candidate.CornerGridPosition);
            }

            createdLoopCount++;
        }

        return createdLoopCount >= minLoopCount;
    }

    private static void CollectLoopCandidates(StageGraph graph, List<LoopCandidate> results)
    {
        for (int fromIndex = 0; fromIndex < graph.Nodes.Count; fromIndex++)
        {
            StageNode from = graph.Nodes[fromIndex];

            if (from.Group == StageNodeGroup.Fixed)
            {
                continue;
            }

            for (int toIndex = fromIndex + 1; toIndex < graph.Nodes.Count; toIndex++)
            {
                StageNode to = graph.Nodes[toIndex];

                if (to.Group == StageNodeGroup.Fixed || graph.HasEdge(from, to))
                {
                    continue;
                }

                Vector2Int offset = to.GridPosition - from.GridPosition;
                int absoluteX = Mathf.Abs(offset.x);
                int absoluteY = Mathf.Abs(offset.y);

                if (absoluteX + absoluteY == 1)
                {
                    AddStraightLoopCandidate(graph, from, to, offset, results);
                    continue;
                }

                if (absoluteX == 1 && absoluteY == 1)
                {
                    AddCornerLoopCandidates(graph, from, to, offset, results);
                }
            }
        }
    }

    private static void AddStraightLoopCandidate(StageGraph graph, StageNode from, StageNode to, Vector2Int offset, List<LoopCandidate> results)
    {
        DoorDirection fromDirection;

        if (offset.x > 0)
        {
            fromDirection = DoorDirection.East;
        }
        else if (offset.x < 0)
        {
            fromDirection = DoorDirection.West;
        }
        else if (offset.y > 0)
        {
            fromDirection = DoorDirection.North;
        }
        else
        {
            fromDirection = DoorDirection.South;
        }

        DoorDirection toDirection = GetOppositeDirection(fromDirection);

        if (graph.IsDirectionUsed(from, fromDirection) || graph.IsDirectionUsed(to, toDirection))
        {
            return;
        }

        results.Add(new LoopCandidate(from, to, fromDirection, toDirection, LoopPathType.Straight, default));
    }

    private static void AddCornerLoopCandidates(StageGraph graph, StageNode from, StageNode to, Vector2Int offset, List<LoopCandidate> results)
    {
        DoorDirection verticalFromDirection = offset.y > 0 ? DoorDirection.North : DoorDirection.South;
        DoorDirection horizontalToDirection = offset.x > 0 ? DoorDirection.West : DoorDirection.East;
        Vector2Int verticalCornerPosition = new Vector2Int(from.GridPosition.x, to.GridPosition.y);

        TryAddCornerLoopCandidate(graph, from, to, verticalFromDirection, horizontalToDirection, verticalCornerPosition, results);

        DoorDirection horizontalFromDirection = offset.x > 0 ? DoorDirection.East : DoorDirection.West;
        DoorDirection verticalToDirection = offset.y > 0 ? DoorDirection.South : DoorDirection.North;
        Vector2Int horizontalCornerPosition = new Vector2Int(to.GridPosition.x, from.GridPosition.y);

        TryAddCornerLoopCandidate(graph, from, to, horizontalFromDirection, verticalToDirection, horizontalCornerPosition, results);
    }

    private static void TryAddCornerLoopCandidate(StageGraph graph, StageNode from, StageNode to, DoorDirection fromDirection, DoorDirection toDirection, Vector2Int cornerGridPosition, List<LoopCandidate> results)
    {
        // 코너 모듈이 차지할 격자에 방이 있으면 해당 ㄱ자 경로를 사용할 수 없다.
        if (graph.ContainsNode(cornerGridPosition))
        {
            return;
        }

        if (graph.IsDirectionUsed(from, fromDirection) || graph.IsDirectionUsed(to, toDirection))
        {
            return;
        }

        results.Add(new LoopCandidate(from, to, fromDirection, toDirection, LoopPathType.Corner, cornerGridPosition));
    }

    private bool IsLoopCandidateAvailable(StageGraph graph, LoopCandidate candidate, HashSet<Vector2Int> occupiedCornerPositions, int[] shortestPathQueue, int[] shortestPathDistances)
    {
        if (candidate.From.Group == StageNodeGroup.Fixed || candidate.To.Group == StageNodeGroup.Fixed)
        {
            return false;
        }

        if (graph.HasEdge(candidate.From, candidate.To))
        {
            return false;
        }

        if (graph.IsDirectionUsed(candidate.From, candidate.FromDirection) ||
            graph.IsDirectionUsed(candidate.To, candidate.ToDirection))
        {
            return false;
        }

        int maxLoopPerNode = Mathf.Max(0, _maxLoopPerNode);

        if (graph.CountEdges(candidate.From, StageEdgeType.Loop) >= maxLoopPerNode ||
            graph.CountEdges(candidate.To, StageEdgeType.Loop) >= maxLoopPerNode)
        {
            return false;
        }

        if (candidate.PathType == LoopPathType.Corner &&
            (graph.ContainsNode(candidate.CornerGridPosition) || occupiedCornerPositions.Contains(candidate.CornerGridPosition)))
        {
            return false;
        }

        int minimumGraphDistance = Mathf.Max(2, _minLoopGraphDistance);
        int graphDistance = graph.GetShortestDistance(candidate.From, candidate.To, shortestPathQueue, shortestPathDistances);
        return graphDistance >= minimumGraphDistance;
    }

    private static void ShuffleLoopCandidates(List<LoopCandidate> candidates)
    {
        for (int index = candidates.Count - 1; index > 0; index--)
        {
            int randomIndex = Random.Range(0, index + 1);
            LoopCandidate temporary = candidates[index];
            candidates[index] = candidates[randomIndex];
            candidates[randomIndex] = temporary;
        }
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
        Vector2 cellSize = CalculateCellSize(graph);

        for (int index = 0; index < graph.Nodes.Count; index++)
        {
            StageNode node = graph.Nodes[index];
            node.Position = new Vector3(node.GridPosition.x * cellSize.x, 0f, node.GridPosition.y * cellSize.y);
        }
    }

    private Vector2 CalculateCellSize(StageGraph graph)
    {
        float maximumWidth = 0f;
        float maximumDepth = 0f;

        for (int index = 0; index < graph.Nodes.Count; index++)
        {
            //if (graph.Nodes[index].Group == StageNodeGroup.Fixed) continue;
            Vector2 roomSpacing = graph.Nodes[index].RoomData.RoomSpacing;
            maximumWidth = Mathf.Max(maximumWidth, roomSpacing.x);
            maximumDepth = Mathf.Max(maximumDepth, roomSpacing.y);
        }

        // 가장 큰 방을 기준으로 모든 격자 칸이 겹치지 않을 크기를 확보한다.
        return new Vector2(maximumWidth + _roomDistance, maximumDepth + _roomDistance);
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

        if (Corridor.TryGenerate(_normalCorridorModule, _halfCorridorModule, _cornerCorridorModule,
            fromDoor, toDoor, fromRoom.CorridorParent)) //복도 생성
        {
            return true;
        }

        // 복도 생성 실패 시 문만 열린 연결이 남지 않도록 원래 상태로 되돌린다.
        fromDoor.Disconnect();
        return false;
    }
}
