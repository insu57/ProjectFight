using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyBrain : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _detectionRange = 15f;
    [SerializeField, Min(0f)] private float _stoppingDistance = 1.5f;

    public bool CanDetectTarget(float targetSqrDistance)
    {
        return targetSqrDistance <= _detectionRange * _detectionRange;
    }

    public bool ShouldChaseTarget(float targetSqrDistance)
    {
        return targetSqrDistance > _stoppingDistance * _stoppingDistance;
    }
}
