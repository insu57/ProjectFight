using System;
using UnityEngine;

public enum LethalArtConditionRequirement
{
    Any = 0,
    Required = 1,
    Forbidden = 2
}

// 벽뿐 아니라 이후 조건 물체의 근접 판정에서도 같은 기준을 재사용한다.
public enum LethalArtProximityOrigin
{
    Player = 0,
    TargetEnemy = 1
}

[Serializable]
public struct LethalArtHealthCondition
{
    [field: SerializeField, Tooltip("끄면 이 체력 조건을 검사하지 않습니다.")]
    public bool Enabled { get; private set; }
    [field: SerializeField, Range(0f, 1f), Tooltip("허용할 최소 체력 비율입니다. 0.3은 최대 체력의 30%이며 경계를 포함합니다.")]
    public float MinimumRatio { get; private set; }
    [field: SerializeField, Range(0f, 1f), Tooltip("허용할 최대 체력 비율입니다. 0.3은 최대 체력의 30%이며 경계를 포함합니다.")]
    public float MaximumRatio { get; private set; }

    public LethalArtHealthCondition(bool enabled, float minimumRatio, float maximumRatio)
    {
        Enabled = enabled;
        MinimumRatio = minimumRatio;
        MaximumRatio = maximumRatio;
        Validate();
    }

    // 소유 SO가 직렬화된 구조체 필드를 직접 검증하여 보정 결과를 보존한다.
    public void Validate()
    {
        MinimumRatio = float.IsNaN(MinimumRatio) || float.IsInfinity(MinimumRatio) ? 0f : Mathf.Clamp01(MinimumRatio);
        MaximumRatio = float.IsNaN(MaximumRatio) || float.IsInfinity(MaximumRatio) ? 1f : Mathf.Clamp(MaximumRatio, MinimumRatio, 1f);
    }
}

// 강공 입력을 대체하는 필살기의 공유 설정이다. 해금 여부와 시전 대상 등 실행 상태는 SO에 저장하지 않는다.
[CreateAssetMenu(fileName = "LethalArtDefinition", menuName = "Scriptable Objects/LethalArtDefinition")]
public sealed class LethalArtDefinition : ScriptableObject
{
    [field: Header("기본 정보")]
    [field: SerializeField, Min(0)] public int LethalArtId { get; private set; }
    [field: SerializeField] public string LethalArtName { get; private set; }
    [field: SerializeField, TextArea] public string Description { get; private set; }
    [field: SerializeField] public Sprite Icon { get; private set; }

    [field: Header("발동 설정")]
    [field: SerializeField, Min(0f), Tooltip("시전 확정 시 소모할 에너지입니다. 후보 판정만으로는 소모하지 않습니다.")]
    public float EnergyCost { get; private set; }
    [field: SerializeField, Tooltip("해금·에너지·조건을 만족한 후보 중 큰 값을 우선합니다. 동률은 LethalArtId가 작은 순서로 선택합니다.")]
    public int Priority { get; private set; }
    [field: SerializeField, Tooltip("피해량, 공격 애니메이션, 이동 및 히트박스 설정을 재사용할 공격입니다.")]
    public AttackDefinition Attack { get; private set; }
    [field: SerializeField, Min(0f), Tooltip("적·벽·조건 물체의 콜라이더 표면까지 가까운지 판단하는 공통 거리(미터)입니다. " +
        "적은 플레이어를, 벽은 Wall Proximity Origin을 기준으로 삼습니다.")]
    public float ProximityDistance { get; private set; } = 2f;

    // 활성화된 조건은 모두 충족해야 한다. 적 관련 조건은 동일한 시전 대상 적을 기준으로 평가한다.
    [Header("체력 조건")]
    [SerializeField] private LethalArtHealthCondition _playerHealthCondition = new(false, 0f, 1f);
    [SerializeField] private LethalArtHealthCondition _enemyHealthCondition = new(false, 0f, 1f);

    [field: Header("상태 조건")]
    [field: SerializeField, Tooltip("Any: 무관, Required: 대상 적이 가드 중, Forbidden: 대상 적이 가드 중이 아님. " +
        "GuardBroken은 가드 중에 포함하지 않습니다.")]
    public LethalArtConditionRequirement EnemyGuardRequirement { get; private set; }
    // 기절과 가드브레이크는 각각 Stunned와 GuardBroken 상태로 구분한다.
    [field: SerializeField, Tooltip("Any: 무관, Required: 대상 적이 기절(Stunned) 중, Forbidden: 대상 적이 기절 중이 아님.")]
    public LethalArtConditionRequirement EnemyStunRequirement { get; private set; }
    [field: SerializeField, Tooltip("Any: 무관, Required: 대상 적이 가드브레이크(GuardBroken) 중, Forbidden: 대상 적이 가드브레이크 중이 아님.")]
    public LethalArtConditionRequirement EnemyGuardBreakRequirement { get; private set; }
    [field: SerializeField, Tooltip("Any: 무관, Required: 플레이어가 달리는 중, Forbidden: 플레이어가 달리지 않는 중.")]
    public LethalArtConditionRequirement PlayerSprintRequirement { get; private set; }

    [field: Header("벽 조건")]
    [field: SerializeField, Tooltip("Any: 무관, Required: 근처에 맵 벽이 있음, " +
        "Forbidden: 근처에 맵 벽이 없음. 바닥·소품 등은 벽으로 취급하지 않습니다.")]
    public LethalArtConditionRequirement NearbyWallRequirement { get; private set; }
    [field: SerializeField, Tooltip("공통 근접 거리 안에 맵 벽이 있는지 검사할 기준입니다. " +
        "TargetEnemy는 동일한 시전 대상 적을 사용합니다.")]
    public LethalArtProximityOrigin WallProximityOrigin { get; private set; }

    public LethalArtHealthCondition PlayerHealthCondition => _playerHealthCondition;
    public LethalArtHealthCondition EnemyHealthCondition => _enemyHealthCondition;

    private void OnValidate()
    {
        LethalArtId = Mathf.Max(0, LethalArtId);
        EnergyCost = NormalizeNonNegative(EnergyCost);
        ProximityDistance = NormalizeNonNegative(ProximityDistance);
        _playerHealthCondition.Validate();
        _enemyHealthCondition.Validate();
    }

    private static float NormalizeNonNegative(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }
}
