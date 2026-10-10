using System;
using UnityEngine;

public enum ItemEffectConditionType
{
    HealthRatio = 0,
    EnergyRatio = 1
}

public enum ItemEffectComparison
{
    LessOrEqual = 0,
    GreaterOrEqual = 1,
    Less = 2,
    Greater = 3
}

// 조건 평가에 필요한 데이터만 보관한다. 현재 비율은 PlayerManager를 통해 전달받는다.
[Serializable]
public struct ItemEffectCondition
{
    [SerializeField] private ItemEffectConditionType _type;
    [SerializeField] private ItemEffectComparison _comparison;
    [SerializeField, Range(0f, 1f), Tooltip("현재값 / 최대값과 비교할 기준입니다. 0.3은 30%입니다.")] private float _threshold;

    public ItemEffectConditionType Type => _type;
    public ItemEffectComparison Comparison => _comparison;
    public float Threshold => Mathf.Clamp01(_threshold);

    public ItemEffectCondition(ItemEffectConditionType type, ItemEffectComparison comparison, float threshold)
    {
        _type = type;
        _comparison = comparison;
        _threshold = Mathf.Clamp01(threshold);
    }

    // 일반 데이터의 조건 값도 소유 아이템의 OnValidate에서 직렬화된 값 자체를 보정한다.
    public void Validate()
    {
        _threshold = Mathf.Clamp01(_threshold);
    }
}
