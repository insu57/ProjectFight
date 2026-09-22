using UnityEngine;

public abstract class PoolableObject : MonoBehaviour
{
    internal PoolingManager PoolOwner { get; private set; }
    internal PoolableObject SourcePrefab { get; private set; }

    public bool IsSpawned { get; internal set; }

    internal void BindPool(PoolingManager owner, PoolableObject prefab)
    {
        PoolOwner = owner;
        SourcePrefab = prefab;
    }

    public bool ReturnToPool()
    {
        // 생성한 풀링 매니저로 반환한다.
        return PoolOwner && PoolOwner.Despawn(this);
    }

    internal void NotifyPoolSpawned()
    {
        OnPoolSpawned();
    }

    internal void NotifyPoolDespawned()
    {
        OnPoolDespawned();
    }

    // 활성화 이후 호출하므로 최초 Awake의 컴포넌트 참조를 사용할 수 있다.
    protected virtual void OnPoolSpawned()
    {
    }

    // 비활성화 전에 호출한다.
    protected virtual void OnPoolDespawned()
    {
    }
}
