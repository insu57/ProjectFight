using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class Room : MonoBehaviour
{
    [field: SerializeField] public Door[] Doors { get; private set; }
    [field: SerializeField] public Transform CorridorParent { get; private set; }
    [field: SerializeField] public BoxCollider[] SpawnAreas { get; private set; }
    // 보상방 프리팹이 소유한 상자를 직접 지정하고 런타임 계층 검색은 하지 않는다.
    [field: SerializeField] public RewardChest RewardChest { get; private set; }
    // 상점방 프리팹이 소유한 상점도 같은 방식으로 직접 지정한다.
    [field: SerializeField] public Store Store { get; private set; }

    [field: Header("방 이동 영역")]
    [field: SerializeField] public NavMeshSurface NavigationSurface { get; private set; }
    [SerializeField, Min(0.01f)] private float _navigationSampleDistance = 0.5f;

    public bool UsesNavigation => NavigationSurface;

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

    public void SetDoorOpeningBlocked(bool isBlocked)
    {
        if (Doors == null) return;

        // 전투 상태는 StageManager가 판단하고, 방은 소유한 문에만 잠금을 전달한다.
        for (int index = 0; index < Doors.Length; index++)
        {
            Door door = Doors[index];
            if (door && door.IsOpeningBlocked != isBlocked) door.SetOpeningBlocked(isBlocked);
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

    public bool TryGetRandomSpawnPosition(float radius, out Vector3 position)
    {
        position = default;
        if (SpawnAreas == null) return false;

        BoxCollider selectedArea = null;
        int validAreaCount = 0;
        float clearance = Mathf.Max(0f, radius);

        // 유효한 바닥 영역 중 하나를 균등하게 고르며 임시 후보 배열은 만들지 않는다.
        for (int index = 0; index < SpawnAreas.Length; index++)
        {
            BoxCollider area = SpawnAreas[index];
            if (!area || !area.enabled || !area.gameObject.activeInHierarchy) continue;

            Vector3 scale = area.transform.lossyScale;
            float scaleX = Mathf.Abs(scale.x);
            float scaleZ = Mathf.Abs(scale.z);
            if (scaleX <= 0.0001f || scaleZ <= 0.0001f) continue;

            // 수평 바닥의 Y축 회전과 스케일을 지원한다. 기울어진 영역에는 수직 캐릭터를 배치하지 않는다.
            if (Vector3.Dot(area.transform.up, Vector3.up) < 0.999f) continue;
            if (area.size.x * scaleX <= clearance * 2f || area.size.z * scaleZ <= clearance * 2f) continue;

            validAreaCount++;
            if (Random.Range(0, validAreaCount) == 0) selectedArea = area;
        }

        if (!selectedArea) return false;

        Transform areaTransform = selectedArea.transform;
        Vector3 halfSize = selectedArea.size * 0.5f;
        Vector3 areaScale = areaTransform.lossyScale;
        float halfX = halfSize.x - clearance / Mathf.Abs(areaScale.x);
        float halfZ = halfSize.z - clearance / Mathf.Abs(areaScale.z);
        Vector3 localPosition = selectedArea.center;
        localPosition.x += Random.Range(-halfX, halfX);
        localPosition.z += Random.Range(-halfZ, halfZ);

        // SpawnAreas는 몸 전체를 감싸는 볼륨이 아니라 바닥 범위다. 아래 면을 발의 높이로 사용한다.
        localPosition.y -= halfSize.y;
        position = areaTransform.TransformPoint(localPosition);
        return true;
    }

    public bool TryGetNavigationFilter(out NavMeshQueryFilter filter)
    {
        filter = default;
        if (!NavigationSurface || !NavigationSurface.isActiveAndEnabled || !NavigationSurface.navMeshData) return false;

        // 방의 Surface와 같은 Agent Type으로만 경로와 위치를 조회한다.
        filter.agentTypeID = NavigationSurface.agentTypeID;
        filter.areaMask = NavMesh.AllAreas;
        return true;
    }

    public bool ContainsNavigationPosition(Vector3 position)
    {
        if (!NavigationSurface || !NavigationSurface.navMeshData) return false;

        // NavMesh 데이터는 위치와 회전만 적용되므로 Transform의 스케일은 사용하지 않는다.
        Transform surfaceTransform = NavigationSurface.transform;
        Vector3 localPosition = Quaternion.Inverse(surfaceTransform.rotation) * (position - surfaceTransform.position);
        Bounds bounds = NavigationSurface.navMeshData.sourceBounds;
        Vector3 minimum = bounds.min;
        Vector3 maximum = bounds.max;
        float heightTolerance = Mathf.Max(0.01f, _navigationSampleDistance);
        return localPosition.x >= minimum.x && localPosition.x <= maximum.x && localPosition.z >= minimum.z
            && localPosition.z <= maximum.z && localPosition.y >= minimum.y - heightTolerance && localPosition.y <= maximum.y + heightTolerance;
    }

    public bool TrySampleNavigationPosition(Vector3 candidate, out Vector3 position)
    {
        position = default;
        if (!TryGetNavigationFilter(out NavMeshQueryFilter filter)) return false;
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, _navigationSampleDistance, filter)) return false;
        if (!ContainsNavigationPosition(hit.position)) return false;

        position = hit.position;
        return true;
    }

    public bool TryValidateSpawnPosition(Vector3 candidate, float radius, out Vector3 position)
    {
        position = candidate;
        // Surface를 등록하지 않은 기존 방은 기존 스폰 방식을 유지한다.
        if (!UsesNavigation) return true;
        if (!TrySampleNavigationPosition(candidate, out Vector3 sampledPosition) || SpawnAreas == null) return false;

        float clearance = Mathf.Max(0f, radius);
        for (int index = 0; index < SpawnAreas.Length; index++)
        {
            BoxCollider area = SpawnAreas[index];
            if (!area || !area.enabled || !area.gameObject.activeInHierarchy) continue;

            Vector3 scale = area.transform.lossyScale;
            float scaleX = Mathf.Abs(scale.x);
            float scaleZ = Mathf.Abs(scale.z);
            if (scaleX <= 0.0001f || scaleZ <= 0.0001f || Vector3.Dot(area.transform.up, Vector3.up) < 0.999f) continue;

            Vector3 halfSize = area.size * 0.5f;
            float halfX = halfSize.x - clearance / scaleX;
            float halfZ = halfSize.z - clearance / scaleZ;
            if (halfX <= 0f || halfZ <= 0f) continue;

            Vector3 original = area.transform.InverseTransformPoint(candidate) - area.center;
            Vector3 sampled = area.transform.InverseTransformPoint(sampledPosition) - area.center;
            // 보정으로 다른 영역이나 영역 경계 밖으로 옮겨진 위치는 재시도한다.
            if (Mathf.Abs(original.x) > halfX + 0.0001f || Mathf.Abs(original.z) > halfZ + 0.0001f
                || Mathf.Abs(sampled.x) > halfX + 0.0001f || Mathf.Abs(sampled.z) > halfZ + 0.0001f) continue;

            position = sampledPosition;
            return true;
        }

        return false;
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
