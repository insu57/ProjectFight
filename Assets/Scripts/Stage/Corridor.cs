using UnityEngine;

public static class Corridor
{
    private const float _moduleLength = 1f;
    private const float _halfModuleLength = 0.5f;
    private const float _cornerHalfSize = 1.5f;
    private const float _distanceTolerance = 0.01f;
    private const float _directionTolerance = 0.999f;

    public static bool TryGenerate(GameObject normalModule, GameObject halfModule, GameObject cornerModule,
        Door fromDoor, Door toDoor, Transform corridorParent)
    {
        if (normalModule == null || fromDoor == null || toDoor == null || corridorParent == null)
        {
            Debug.LogError("복도를 생성하는 데 필요한 참조가 설정되지 않았습니다.");
            return false;
        }

        if (Mathf.Abs(fromDoor.transform.position.y - toDoor.transform.position.y) > _distanceTolerance)
        {
            Debug.LogError("서로 다른 높이에 있는 문은 현재 복도 방식으로 연결할 수 없습니다.");
            return false;
        }

        if (Door.AreOpposite(fromDoor.Direction, toDoor.Direction))
        {
            return TryGenerateStraight(normalModule, halfModule, fromDoor, toDoor, corridorParent);
        }

        if (IsPerpendicular(fromDoor.Direction, toDoor.Direction))
        {
            return TryGenerateCorner(normalModule, halfModule, cornerModule, fromDoor, toDoor, corridorParent);
        }

        Debug.LogError($"지원하지 않는 문 방향 조합입니다: {fromDoor.Direction}, {toDoor.Direction}");
        return false;
    }

    private static bool TryGenerateStraight(GameObject normalModule, GameObject halfModule,
        Door fromDoor, Door toDoor, Transform corridorParent)
    {
        Vector3 fromPosition = fromDoor.transform.position;
        Vector3 toPosition = toDoor.transform.position;
        Vector3 corridorDirection = toPosition - fromPosition;
        float distance = corridorDirection.magnitude;

        if (distance <= _distanceTolerance)
        {
            Debug.LogError("직선 복도를 생성하기에는 문 사이 거리가 너무 짧습니다.");
            return false;
        }

        corridorDirection /= distance;

        Vector3 fromDirection = GetWorldDirection(fromDoor.Direction);
        Vector3 toDirection = GetWorldDirection(toDoor.Direction);

        // 각 문의 바깥 방향이 상대 문을 향하는 경우에만 직선으로 연결한다.
        if (Vector3.Dot(corridorDirection, fromDirection) < _directionTolerance ||
            Vector3.Dot(-corridorDirection, toDirection) < _directionTolerance)
        {
            Debug.LogError("두 문이 마주 보고 있지 않아 직선 복도를 생성할 수 없습니다.");
            return false;
        }

        if (!TryGetModuleLayout(distance, out int normalModuleCount, out bool requiresHalfModule))
        {
            Debug.LogError($"문 사이 거리({distance})를 1유닛 및 0.5유닛 복도 모듈로 채울 수 없습니다.");
            return false;
        }

        if (requiresHalfModule && halfModule == null)
        {
            Debug.LogError("0.5유닛 복도 모듈이 필요하지만 설정되지 않았습니다.");
            return false;
        }

        GenerateStraightSegment(normalModule, halfModule, fromPosition, corridorDirection,
            normalModuleCount, requiresHalfModule, corridorParent);
        return true;
    }

    private static bool TryGenerateCorner(GameObject normalModule, GameObject halfModule, GameObject cornerModule,
        Door fromDoor, Door toDoor, Transform corridorParent)
    {
        if (cornerModule == null)
        {
            Debug.LogError("모퉁이 복도 모듈이 설정되지 않았습니다.");
            return false;
        }

        Vector3 fromPosition = fromDoor.transform.position;
        Vector3 toPosition = toDoor.transform.position;
        Vector3 fromDirection = GetWorldDirection(fromDoor.Direction);
        Vector3 toDirection = GetWorldDirection(toDoor.Direction);

        if (!TryFindCorner(fromPosition, fromDirection, toPosition, toDirection,
            out Vector3 cornerPosition, out float fromDistance, out float toDistance))
        {
            Debug.LogError("두 문의 바깥 진행 방향에서 유효한 모퉁이 위치를 찾지 못했습니다.");
            return false;
        }

        // 3×3 모퉁이가 중심에서 각 방향으로 1.5유닛을 차지한다.
        float fromStraightLength = fromDistance - _cornerHalfSize;
        float toStraightLength = toDistance - _cornerHalfSize;

        if (!TryGetModuleLayout(fromStraightLength, out int fromNormalModuleCount, out bool fromRequiresHalfModule) ||
            !TryGetModuleLayout(toStraightLength, out int toNormalModuleCount, out bool toRequiresHalfModule))
        {
            Debug.LogError($"모퉁이까지의 직선 구간을 1유닛 및 0.5유닛 모듈로 채울 수 없습니다: {fromStraightLength}, {toStraightLength}");
            return false;
        }

        if ((fromRequiresHalfModule || toRequiresHalfModule) && halfModule == null)
        {
            Debug.LogError("모퉁이 연결에 0.5유닛 복도 모듈이 필요하지만 설정되지 않았습니다.");
            return false;
        }

        GenerateStraightSegment(normalModule, halfModule, fromPosition, fromDirection,
            fromNormalModuleCount, fromRequiresHalfModule, corridorParent);
        GenerateStraightSegment(normalModule, halfModule, toPosition, toDirection,
            toNormalModuleCount, toRequiresHalfModule, corridorParent);

        Quaternion cornerRotation = GetCornerRotation(fromDoor.Direction, toDoor.Direction);
        Object.Instantiate(cornerModule, cornerPosition, cornerRotation, corridorParent);
        return true;
    }

    private static bool TryFindCorner(Vector3 fromPosition, Vector3 fromDirection, Vector3 toPosition, Vector3 toDirection,
        out Vector3 cornerPosition, out float fromDistance, out float toDistance)
    {
        Vector3 positionDifference = toPosition - fromPosition;
        float denominator = CrossXZ(fromDirection, toDirection);

        cornerPosition = default;
        fromDistance = 0f;
        toDistance = 0f;

        if (Mathf.Abs(denominator) <= _distanceTolerance) //평행인지 검사
        {
            return false;
        }

        fromDistance = CrossXZ(positionDifference, toDirection) / denominator;
        toDistance = CrossXZ(positionDifference, fromDirection) / denominator;

        // 모퉁이 중심은 양쪽 문에서 바깥 방향으로 최소 1.5유닛 떨어져야 한다.
        if (fromDistance < _cornerHalfSize - _distanceTolerance ||
            toDistance < _cornerHalfSize - _distanceTolerance)
        {
            return false;
        }

        Vector3 fromCornerPosition = fromPosition + fromDirection * fromDistance;
        Vector3 toCornerPosition = toPosition + toDirection * toDistance;

        if ((fromCornerPosition - toCornerPosition).sqrMagnitude > _distanceTolerance * _distanceTolerance)
        {
            return false;
        }

        cornerPosition = (fromCornerPosition + toCornerPosition) * 0.5f;
        return true;
    }

    private static bool TryGetModuleLayout(float length, out int normalModuleCount, out bool requiresHalfModule)
    {
        normalModuleCount = 0;
        requiresHalfModule = false;

        if (length < -_distanceTolerance)
        {
            return false;
        }

        float clampedLength = Mathf.Max(0f, length);
        int halfUnitCount = Mathf.RoundToInt(clampedLength / _halfModuleLength);

        if (Mathf.Abs(clampedLength - halfUnitCount * _halfModuleLength) > _distanceTolerance)
        {
            return false;
        }

        normalModuleCount = halfUnitCount / 2;
        requiresHalfModule = halfUnitCount % 2 != 0;
        return true;
    }

    private static void GenerateStraightSegment(GameObject normalModule, GameObject halfModule, Vector3 startPosition, Vector3 direction,
        int normalModuleCount, bool requiresHalfModule, Transform corridorParent)
    {
        var rotation = Quaternion.LookRotation(direction, Vector3.up);

        for (int index = 0; index < normalModuleCount; index++)
        {
            // 길이 1 모듈의 중심이 직선 구간을 빈틈없이 채우도록 0.5유닛부터 배치한다.
            Vector3 modulePosition = startPosition + direction * ((index + 0.5f) * _moduleLength);
            Object.Instantiate(normalModule, modulePosition, rotation, corridorParent);
        }

        if (!requiresHalfModule)
        {
            return;
        }

        // Half 모듈의 중심을 마지막 일반 모듈 이후의 0.25유닛 위치에 맞춘다.
        float halfModuleCenterDistance = normalModuleCount * _moduleLength + _halfModuleLength * 0.5f;
        Vector3 halfModulePosition = startPosition + direction * halfModuleCenterDistance;
        Object.Instantiate(halfModule, halfModulePosition, rotation, corridorParent);
    }

    private static bool IsPerpendicular(DoorDirection first, DoorDirection second)
    {
        return Mathf.Abs(Vector3.Dot(GetWorldDirection(first), GetWorldDirection(second))) <= _distanceTolerance;
    }

    private static float CrossXZ(Vector3 first, Vector3 second)
    {
        return first.x * second.z - first.z * second.x; //외적계산
    }

    private static Vector3 GetWorldDirection(DoorDirection direction)
    {
        return direction switch
        {
            DoorDirection.North => Vector3.forward,
            DoorDirection.East => Vector3.right,
            DoorDirection.South => Vector3.back,
            DoorDirection.West => Vector3.left,
            _ => Vector3.zero
        };
    }

    private static Quaternion GetCornerRotation(DoorDirection first, DoorDirection second)
    {
        // 모퉁이 복도 모듈의 기본 출입구가 South + East 방향인 것을 기준으로 한다.
        if (ContainsDirections(first, second, DoorDirection.North, DoorDirection.West))
        {
            return Quaternion.identity;
        }

        if (ContainsDirections(first, second, DoorDirection.North, DoorDirection.East))
        {
            return Quaternion.Euler(0f, 90f, 0f);
        }

        if (ContainsDirections(first, second, DoorDirection.South, DoorDirection.East))
        {
            return Quaternion.Euler(0f, 180f, 0f);
        }

        return Quaternion.Euler(0f, 270f, 0f);
    }

    private static bool ContainsDirections(DoorDirection first, DoorDirection second,
        DoorDirection expectedFirst, DoorDirection expectedSecond)
    {
        return first == expectedFirst && second == expectedSecond ||
               first == expectedSecond && second == expectedFirst;
    }
}
