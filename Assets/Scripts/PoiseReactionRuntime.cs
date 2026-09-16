using UnityEngine;

// 각 Manager가 독립적으로 보유하는 피격 반응 실행 상태이다.
public sealed class PoiseReactionRuntime
{
    private float _remainingTime;
    private Vector3 _velocity;

    public bool IsRunning => _remainingTime > 0f;

    public void Begin(Transform defender, Transform attacker, float duration, float moveDistance)
    {
        Reset();
        if (!defender) return;

        _remainingTime = Mathf.Max(0f, duration);
        Vector3 pushDirection = attacker ? defender.position - attacker.position : -defender.forward;
        pushDirection.y = 0f;

        // 위치가 겹치면 피격자의 뒤쪽으로 밀려난다.
        if (pushDirection.sqrMagnitude <= 0.0001f)
        {
            pushDirection = -defender.forward;
            pushDirection.y = 0f;
        }

        pushDirection.Normalize();

        // 시작 순간의 속도를 유지하여 공격자의 이후 이동에 영향받지 않는다.
        _velocity = _remainingTime > 0f ? pushDirection * (Mathf.Max(0f, moveDistance) / _remainingTime) : Vector3.zero;
    }

    public Vector3 Advance(float deltaTime)
    {
        if (deltaTime <= 0f || !IsRunning) return Vector3.zero;

        // 종료 프레임에는 남은 시간만큼만 이동한다.
        float movementTime = Mathf.Min(deltaTime, _remainingTime);
        Vector3 displacement = _velocity * movementTime;
        _remainingTime = Mathf.Max(0f, _remainingTime - deltaTime);

        if (!IsRunning)
        {
            _velocity = Vector3.zero;
        }

        return displacement;
    }

    public void Reset()
    {
        _remainingTime = 0f;
        _velocity = Vector3.zero;
    }
}
