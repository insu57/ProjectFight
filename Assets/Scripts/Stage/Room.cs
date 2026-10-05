using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    [field: SerializeField] public Door[] Doors { get; private set; }
    [field: SerializeField] public Transform CorridorParent { get; private set; }
    [field: SerializeField] public BoxCollider[] SpawnAreas { get; private set; }
    // 보상방 프리팹이 소유한 상자를 직접 지정하고 런타임 계층 검색은 하지 않는다.
    [field: SerializeField] public RewardChest RewardChest { get; private set; }
    // 상점방 프리팹이 소유한 상점도 같은 방식으로 직접 지정한다.
    [field: SerializeField] public Store Store { get; private set; }

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
