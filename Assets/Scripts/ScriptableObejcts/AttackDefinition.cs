using UnityEngine;

[CreateAssetMenu(fileName = "AttackDefinition", menuName = "Scriptable Objects/AttackDefinition")]
public class AttackDefinition : ScriptableObject
{
    [field: SerializeField] public int AttackId { get; private set; }
    [field: SerializeField] public string AttackName { get; private set; }
    [field: SerializeField] public string AnimationStateName { get; private set; }
    // 공격 고유의 기본 배속이며, 캐릭터의 공격속도 배율은 재생할 때 별도로 곱한다.
    [field: SerializeField, Min(0.01f)] public float AnimationSpeed { get; private set; } = 1f;
    [field: SerializeField] public float BaseDamage { get; private set; }
    // 공격자 스탯과 아이템 보정을 적용하기 전의 강인도 감소량이다.
    [field: SerializeField, Min(0f)] public float BasePoiseDamage { get; private set; }
    [field: SerializeField, Min(0f)] public float BaseGuardPoiseDamage { get; private set; }
    // 일반 타격에서 회복할 기본 에너지이며, 가드에 막히면 최종 회복량의 절반을 회복한다.
    [field: SerializeField, Min(0f)] public float BaseEnergyRecoveryOnHit { get; private set; }
    // 타격 성공 시 공격자와 피격자에게 동일하게 적용할 정지 시간이다.
    [field: SerializeField, Min(0f)] public float HitStopDuration { get; private set; }
    [field: SerializeField, Min(0f)] public float TransitionDuration { get; private set; } = 0.1f;
    [field: SerializeField, Range(0f, 1f)] public float ComboInputStartNormalizedTime { get; private set; } = 0.2f;
    [field: SerializeField, Range(0f, 1f)] public float ComboTransitionNormalizedTime { get; private set; } = 0.55f;
    [field: SerializeField, Range(0f, 1f)] public float ComboInputEndNormalizedTime { get; private set; } = 0.8f;
    [field: SerializeField] public Vector2 AttackMoveDistance { get; private set; }
    [field: SerializeField, Range(0f, 1f)] public float MoveStartNormalizedTime { get; private set; }
    [field: SerializeField, Range(0f, 1f)] public float MoveEndNormalizedTime { get; private set; } = 1f;
    [field: SerializeField] public AnimationCurve MoveCurve { get; private set; } = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    // 공격별 판정 범위와 활성 구간을 정의한다.
    [field: SerializeField] public Vector3 HitboxSize { get; private set; } = Vector3.one;
    [field: SerializeField] public Vector3 HitboxOffset { get; private set; } = new Vector3(0f, 1f, 1f);
    [field: SerializeField, Range(0f, 1f)] public float HitStartNormalizedTime { get; private set; } = 0.2f;
    [field: SerializeField, Range(0f, 1f)] public float HitEndNormalizedTime { get; private set; } = 0.4f;

    public int AnimationStateHash { get; private set; }

    private void OnEnable()
    {
        RefreshAnimationStateHash();
    }

    private void OnValidate()
    {
        AnimationSpeed = Mathf.Max(0.01f, AnimationSpeed);
        BaseEnergyRecoveryOnHit = float.IsNaN(BaseEnergyRecoveryOnHit) || float.IsInfinity(BaseEnergyRecoveryOnHit)
            ? 0f : Mathf.Max(0f, BaseEnergyRecoveryOnHit);

        // 입력 허용 시작, 연계 실행, 입력 허용 종료 순서가 뒤집히지 않도록 보정한다.
        ComboInputStartNormalizedTime = Mathf.Clamp01(ComboInputStartNormalizedTime);
        ComboInputEndNormalizedTime = Mathf.Clamp(ComboInputEndNormalizedTime, ComboInputStartNormalizedTime, 1f);
        ComboTransitionNormalizedTime = Mathf.Clamp(ComboTransitionNormalizedTime, ComboInputStartNormalizedTime, ComboInputEndNormalizedTime);

        // 공격 이동 구간은 시작 시점보다 종료 시점이 빠르지 않도록 보정한다.
        MoveStartNormalizedTime = Mathf.Clamp01(MoveStartNormalizedTime);
        MoveEndNormalizedTime = Mathf.Clamp(MoveEndNormalizedTime, MoveStartNormalizedTime, 1f);

        // 박스 크기는 양수로 유지하고, 판정 종료가 시작보다 빠르지 않도록 보정한다.
        HitboxSize = Vector3.Max(HitboxSize, Vector3.one * 0.01f);
        HitStartNormalizedTime = Mathf.Clamp01(HitStartNormalizedTime);
        HitEndNormalizedTime = Mathf.Clamp(HitEndNormalizedTime, HitStartNormalizedTime, 1f);
        RefreshAnimationStateHash();
    }

    private void RefreshAnimationStateHash()
    {
        AnimationStateHash = Animator.StringToHash(AnimationStateName);
    }
}
