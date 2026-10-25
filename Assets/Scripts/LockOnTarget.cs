using UnityEngine;

[DisallowMultipleComponent]
public sealed class LockOnTarget : MonoBehaviour
{
    [SerializeField] private Transform _aimPoint;

    public Vector3 AimPosition => _aimPoint ? _aimPoint.position : transform.position;
    public EnemyManager EnemyManager { get; private set; }

    public void Initialize(EnemyManager enemyManager)
    {
        // 소유 적이 주입한 참조를 보관해 조회할 때마다 부모 계층을 검색하지 않는다.
        EnemyManager = enemyManager;
    }
}
