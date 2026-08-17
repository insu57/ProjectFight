using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerDodge : MonoBehaviour
{
    [Header("Dodge Count")]
    [SerializeField, Min(1)] private int _maxDodgeCount = 1;

    [Header("Recharge")]
    [SerializeField, Min(0f)] private float _rechargeDelay = 1f;

    private int _remainingDodgeCount;
    private float _rechargeElapsedTime;

    public int MaxDodgeCount => _maxDodgeCount;
    public int RemainingDodgeCount => _remainingDodgeCount;
    public bool CanDodge => _remainingDodgeCount > 0;

    private void Awake()
    {
        _remainingDodgeCount = _maxDodgeCount;
    }

    public void UpdateRecharge(float deltaTime)
    {
        // 횟수가 모두 남아 있다면 재충전할 필요가 없다.
        if (_remainingDodgeCount >= _maxDodgeCount) return;

        if (_rechargeDelay <= 0f)
        {
            RechargeAll();
            return;
        }

        _rechargeElapsedTime += deltaTime;

        if (_rechargeElapsedTime >= _rechargeDelay)
        {
            RechargeAll();
        }
    }

    public void ConsumeDodge()
    {
        if (!CanDodge) return;

        _remainingDodgeCount--;
        _rechargeElapsedTime = 0f;
    }

    public void SetMaxDodgeCount(int maxDodgeCount)
    {
        maxDodgeCount = Mathf.Max(1, maxDodgeCount);

        if (_maxDodgeCount == maxDodgeCount) return;

        // 이미 소비한 횟수는 유지하면서 최대 횟수 변경분을 반영한다.
        int consumedDodgeCount = _maxDodgeCount - _remainingDodgeCount;

        _maxDodgeCount = maxDodgeCount;
        _remainingDodgeCount = Mathf.Clamp(_maxDodgeCount - consumedDodgeCount, 0, _maxDodgeCount);

        if (_remainingDodgeCount >= _maxDodgeCount)
        {
            _rechargeElapsedTime = 0f;
        }
    }

    public void RechargeAll()
    {
        _remainingDodgeCount = _maxDodgeCount;
        _rechargeElapsedTime = 0f;
    }
}
