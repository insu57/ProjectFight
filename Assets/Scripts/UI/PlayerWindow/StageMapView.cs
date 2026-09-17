using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class StageMapView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform _mapRoot;
    [SerializeField] private Image _roomPrefab;
    [SerializeField] private Image _corridorPrefab;

    [Header("Map Layout")]
    [SerializeField, Min(1f)] private float _cellSpacing = 100f;
    [SerializeField, Min(1f)] private float _roomSize = 40f;
    [SerializeField, Min(1f)] private float _corridorWidth = 8f;
    [SerializeField, Min(0f)] private float _padding = 24f;

    private readonly List<RectTransform> _roomPool = new();
    private readonly List<RectTransform> _corridorPool = new();

    private MapGenerator _mapGenerator;
    private RectTransform _viewport;
    private StageGraph _displayedGraph;
    private Vector2 _mapCenter;
    private Vector2 _mapSize;
    private float _spacing;
    private float _roomLength;
    private float _lineWidth;
    private int _usedCorridors;

    private void Awake()
    {
        _viewport = (RectTransform)transform;
    }

    private void OnEnable()
    {
        BindMapGenerator();
    }

    public void SetMapGenerator(MapGenerator mapGenerator)
    {
        if (_mapGenerator == mapGenerator)
        {
            return;
        }

        if (_mapGenerator)
        {
            _mapGenerator.GraphChanged -= RefreshMap;
        }

        _mapGenerator = mapGenerator;

        // 비활성 상태에서는 참조만 저장하고, 탭을 열 때 연결한다.
        if (isActiveAndEnabled)
        {
            RefreshMap(null);
            BindMapGenerator();
        }
    }

    private void BindMapGenerator()
    {
        // StageManager의 Awake보다 먼저 활성화되면 할당을 기다린다.
        if (!_mapGenerator)
        {
            return;
        }

        if (!_mapRoot || !_roomPrefab || !_corridorPrefab)
        {
            Debug.LogError("지도 생성에 필요한 참조가 설정되지 않았습니다.", this);
            return;
        }

        if (_mapRoot.parent != transform)
        {
            Debug.LogError("MapRoot는 StageMapView의 바로 아래 자식이어야 합니다.", this);
            return;
        }

        // 다른 오브젝트의 Awake에서 먼저 호출돼도 초기화되도록 한다.
        if (!_viewport)
        {
            _viewport = (RectTransform)transform;
        }

        // 할당과 활성화 순서에 관계없이 중복 구독을 방지한다.
        _mapGenerator.GraphChanged -= RefreshMap;
        _mapGenerator.GraphChanged += RefreshMap;

        // 지도 탭이 닫혀 있을 때 생성된 그래프도 처음 열면 표시한다.
        RefreshMap(_mapGenerator.CurrentGraph);
    }

    private void OnDisable()
    {
        if (_mapGenerator)
        {
            _mapGenerator.GraphChanged -= RefreshMap;
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        FitMap();
    }

    private void RefreshMap(StageGraph graph)
    {
        if (ReferenceEquals(_displayedGraph, graph))
        {
            FitMap();
            return;
        }

        _displayedGraph = graph;
        _usedCorridors = 0;

        if (graph == null || graph.Nodes.Count == 0)
        {
            HideUnused(_roomPool, 0);
            HideUnused(_corridorPool, 0);
            _mapSize = Vector2.zero;
            return;
        }

        _roomLength = Mathf.Max(1f, _roomSize);
        _lineWidth = Mathf.Clamp(_corridorWidth, 1f, _roomLength);
        _spacing = Mathf.Max(_cellSpacing, _roomLength + _lineWidth);

        CalculateBounds(graph);

        _mapRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _mapRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _mapRoot.pivot = new Vector2(0.5f, 0.5f);
        _mapRoot.anchoredPosition3D = Vector3.zero;
        _mapRoot.sizeDelta = _mapSize;
        _mapRoot.localRotation = Quaternion.identity;

        // 복도를 먼저 배치하고 방을 위에 그려 접합부를 덮는다.
        for (int index = 0; index < graph.Edges.Count; index++)
        {
            DrawEdge(graph.Edges[index]);
        }

        for (int index = 0; index < graph.Nodes.Count; index++)
        {
            RectTransform room = GetImage(_roomPool, index, _roomPrefab);
            Vector2 position = GetMapPosition(graph.Nodes[index]);

            SetRect(room, position, Vector2.one * _roomLength, 0f);
        }

        HideUnused(_roomPool, graph.Nodes.Count);
        HideUnused(_corridorPool, _usedCorridors);
        FitMap();
    }

    private void CalculateBounds(StageGraph graph)
    {
        Vector2 minimum = (Vector2)graph.Nodes[0].GridPosition * _spacing;
        Vector2 maximum = minimum;

        for (int index = 1; index < graph.Nodes.Count; index++)
        {
            Vector2 position = (Vector2)graph.Nodes[index].GridPosition * _spacing;
            minimum = Vector2.Min(minimum, position);
            maximum = Vector2.Max(maximum, position);
        }

        _mapCenter = (minimum + maximum) * 0.5f;

        // 가장 바깥쪽 방의 윤곽까지 포함해 전체 크기를 계산한다.
        _mapSize = maximum - minimum + Vector2.one * _roomLength;
    }

    private Vector2 GetMapPosition(StageNode node)
    {
        return (Vector2)node.GridPosition * _spacing - _mapCenter;
    }

    private void DrawEdge(StageEdge edge)
    {
        Vector2 from = GetMapPosition(edge.From);
        Vector2 to = GetMapPosition(edge.To);
        float halfRoom = _roomLength * 0.5f;

        // 복도는 방 중심이 아닌, 출입 방향에 해당하는 변에서 시작한다.
        Vector2 start = from + (Vector2)StageGraph.GetGridOffset(edge.FromDirection) * halfRoom;
        Vector2 end = to + (Vector2)StageGraph.GetGridOffset(edge.ToDirection) * halfRoom;

        if (edge.ToDirection == StageGraph.GetOppositeDirection(edge.FromDirection))
        {
            DrawSegment(start, end);
            return;
        }

        // 기존 그래프의 ㄱ자 연결 규칙과 같은 방향으로 꺾는다.
        bool startsHorizontal = edge.FromDirection == DoorDirection.East || edge.FromDirection == DoorDirection.West;
        Vector2 corner = startsHorizontal ? new Vector2(to.x, from.y) : new Vector2(from.x, to.y);

        DrawSegment(start, corner);
        DrawSegment(corner, end);
    }

    private void DrawSegment(Vector2 start, Vector2 end)
    {
        Vector2 direction = end - start;
        float length = direction.magnitude;

        if (length <= 0.001f)
        {
            return;
        }

        RectTransform corridor = GetImage(_corridorPool, _usedCorridors, _corridorPrefab);
        _usedCorridors++;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 선 두께만큼 겹쳐 ㄱ자 접합부에 틈이 생기지 않도록 한다.
        Vector2 size = new Vector2(length + _lineWidth, _lineWidth);
        SetRect(corridor, (start + end) * 0.5f, size, angle);
    }

    private RectTransform GetImage(List<RectTransform> pool, int index, Image prefab)
    {
        if (index >= pool.Count)
        {
            Image image = Instantiate(prefab, _mapRoot, false);
            image.raycastTarget = false;
            image.preserveAspect = false;
            pool.Add(image.rectTransform);
        }

        RectTransform item = pool[index];

        if (!item.gameObject.activeSelf)
        {
            item.gameObject.SetActive(true);
        }

        return item;
    }

    private static void SetRect(RectTransform item, Vector2 position, Vector2 size, float angle)
    {
        item.anchorMin = new Vector2(0.5f, 0.5f);
        item.anchorMax = new Vector2(0.5f, 0.5f);
        item.pivot = new Vector2(0.5f, 0.5f);
        item.anchoredPosition3D = new Vector3(position.x, position.y, 0f);
        item.sizeDelta = size;
        item.localRotation = Quaternion.Euler(0f, 0f, angle);
        item.localScale = Vector3.one;
        item.SetAsLastSibling();
    }

    private static void HideUnused(List<RectTransform> pool, int usedCount)
    {
        for (int index = usedCount; index < pool.Count; index++)
        {
            if (pool[index].gameObject.activeSelf)
            {
                pool[index].gameObject.SetActive(false);
            }
        }
    }

    private void FitMap()
    {
        if (!_viewport || !_mapRoot || _mapSize.x <= 0f || _mapSize.y <= 0f)
        {
            return;
        }

        Vector2 available = _viewport.rect.size - Vector2.one * Mathf.Max(0f, _padding) * 2f;

        if (available.x <= 0f || available.y <= 0f)
        {
            return;
        }

        float scale = Mathf.Min(available.x / _mapSize.x, available.y / _mapSize.y);

        // 작은 지도는 원래 크기를 유지하고, 큰 지도만 패널에 맞춰 축소한다.
        _mapRoot.localScale = Vector3.one * Mathf.Min(1f, scale);
    }
}
