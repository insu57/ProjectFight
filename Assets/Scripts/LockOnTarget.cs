using UnityEngine;

[DisallowMultipleComponent]
public sealed class LockOnTarget : MonoBehaviour
{
    [SerializeField] private Transform _aimPoint;

    public Vector3 AimPosition => _aimPoint ? _aimPoint.position : transform.position;
}
