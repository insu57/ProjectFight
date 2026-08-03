﻿using UnityEngine;

public static class Corridor
{
    private const float _moduleLength = 1f;
    private const float _distanceTolerance = 0.01f;

    public static bool TryGenerate( GameObject corridorModule, Door fromDoor, Door toDoor, Transform corridorParent)
    {
        if (corridorModule == null || fromDoor == null || toDoor == null || corridorParent == null)
        {
            Debug.LogError("복도를 생성하는 데 필요한 참조가 설정되지 않았습니다.");
            return false;
        }

        Vector3 fromPosition = fromDoor.transform.position;
        Vector3 direction = toDoor.transform.position - fromPosition;
        float distance = direction.magnitude;
        int moduleCount = Mathf.RoundToInt(distance / _moduleLength);

        // 1유닛 모듈만 사용하므로 문 사이 거리가 정수 유닛일 때만 빈틈 없이 생성한다.
        if (moduleCount <= 0 || Mathf.Abs(distance - moduleCount * _moduleLength) > _distanceTolerance)
        {
            Debug.LogError($"문 사이 거리({distance})를 1유닛 복도 모듈로 채울 수 없습니다.");
            return false;
        }

        direction /= distance; //정규화
        var rotation = Quaternion.LookRotation(direction, Vector3.up);

        for (int index = 0; index < moduleCount; index++)
        {
            // 피벗이 바닥 중앙이므로 각 모듈의 중심을 문 사이 구간의 중앙에 맞춘다.
            Vector3 modulePosition = fromPosition + direction * ((index + 0.5f) * _moduleLength);
            Object.Instantiate(corridorModule, modulePosition, rotation, corridorParent);
        }

        return true;
    }
}
