using UnityEngine;

[CreateAssetMenu(fileName = "AttackDefinition", menuName = "Scriptable Objects/AttackDefinition")]
public class AttackDefinition : ScriptableObject
{
    [field: SerializeField] public int AttackId { get; private set; }
    [field: SerializeField] public string AttackName { get; private set; }
    [field: SerializeField] public string AnimationStateName { get; private set; }
    [field: SerializeField] public float BaseDamage { get; private set; }
    [field: SerializeField, Min(0f)] public float TransitionDuration { get; private set; } = 0.1f;
    [field: SerializeField, Range(0f, 1f)] public float ComboInputStartNormalizedTime { get; private set; } = 0.2f;
    [field: SerializeField, Range(0f, 1f)] public float ComboTransitionNormalizedTime { get; private set; } = 0.55f;
    [field: SerializeField, Range(0f, 1f)] public float ComboInputEndNormalizedTime { get; private set; } = 0.8f;
    [field: SerializeField] public Vector2 AttackMoveDistance { get; private set; }
    [field: SerializeField, Range(0f, 1f)] public float MoveStartNormalizedTime { get; private set; }
    [field: SerializeField, Range(0f, 1f)] public float MoveEndNormalizedTime { get; private set; } = 1f;
    [field: SerializeField] public AnimationCurve MoveCurve { get; private set; } = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public int AnimationStateHash { get; private set; }

    private void OnEnable()
    {
        RefreshAnimationStateHash();
    }

    private void OnValidate()
    {
        // 입력 허용 시작, 연계 실행, 입력 허용 종료 순서가 뒤집히지 않도록 보정한다.
        ComboInputStartNormalizedTime = Mathf.Clamp01(ComboInputStartNormalizedTime);
        ComboInputEndNormalizedTime = Mathf.Clamp(ComboInputEndNormalizedTime, ComboInputStartNormalizedTime, 1f);
        ComboTransitionNormalizedTime = Mathf.Clamp(ComboTransitionNormalizedTime, ComboInputStartNormalizedTime, ComboInputEndNormalizedTime);

        // 공격 이동 구간은 시작 시점보다 종료 시점이 빠르지 않도록 보정한다.
        MoveStartNormalizedTime = Mathf.Clamp01(MoveStartNormalizedTime);
        MoveEndNormalizedTime = Mathf.Clamp(MoveEndNormalizedTime, MoveStartNormalizedTime, 1f);
        RefreshAnimationStateHash();
    }

    private void RefreshAnimationStateHash()
    {
        AnimationStateHash = Animator.StringToHash(AnimationStateName);
    }
}
