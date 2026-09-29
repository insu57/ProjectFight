using System;
using UnityEngine;

[Serializable]
public sealed class EncounterEnemyEntry
{
    // 기존 SpawnManager에서 바로 사용할 수 있는 적 프리팹을 지정한다.
    [field: SerializeField] public EnemyManager Prefab { get; private set; }
    [field: SerializeField, Min(1)] public int Count { get; private set; } = 1;
}

[CreateAssetMenu(fileName = "EncounterDefinition", menuName = "Scriptable Objects/EncounterDefinition")]
public sealed class EncounterDefinition : ScriptableObject
{
    // 공유하는 편성 원본만 저장하며, 생존 적 수와 클리어 여부는 방별 런타임에서 관리한다.
    [field: SerializeField] public EncounterEnemyEntry[] Enemies { get; private set; } = Array.Empty<EncounterEnemyEntry>();
}
