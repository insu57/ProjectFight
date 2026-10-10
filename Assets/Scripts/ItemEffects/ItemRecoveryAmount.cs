using System;
using UnityEngine;

public enum ItemRecoveryMode
{
    Fixed = 0,
    MaxRatio = 1
}

// 체력과 에너지 회복에서 같은 수량 표현을 사용한다. 최대값은 실행할 때 플레이어에서 조회한다.
[Serializable]
public struct ItemRecoveryAmount
{
    [SerializeField] private ItemRecoveryMode _mode;
    [SerializeField, Min(0f), Tooltip("Fixed는 고정 회복량, MaxRatio는 최대값의 비율입니다. 0.2는 최대값의 20%입니다.")]
    private float _value;

    public ItemRecoveryMode Mode => _mode;
    public float Value => _value;

    public ItemRecoveryAmount(ItemRecoveryMode mode, float value)
    {
        _mode = mode;
        _value = Mathf.Max(0f, value);
    }

    // 내부 데이터에는 Unity의 OnValidate가 호출되지 않으므로 소유 아이템에서 검증한다.
    public void Validate()
    {
        _value = Mathf.Max(0f, _value);
    }
}
