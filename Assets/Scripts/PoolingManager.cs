using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PoolingManager : Singleton<PoolingManager>
{
    [Serializable]
    private struct InitialPool
    {
        public PoolableObject Prefab;

        [Min(0)]
        public int Count;
    }

    [SerializeField] private InitialPool[] _initialPools = Array.Empty<InitialPool>();

    private readonly Dictionary<PoolableObject, Stack<PoolableObject>> _pools = new();
    // Canvas 등 매니저 계층 밖으로 대여한 오브젝트도 매니저 종료 시 정리한다.
    private readonly HashSet<PoolableObject> _parentedInstances = new();

    private Transform _objectsRoot;
    private Transform _inactiveRoot;
    private bool _isClosing;

    protected override void OnSingletonAwake()
    {
        _objectsRoot = new GameObject("PooledObjects").transform;
        _objectsRoot.SetParent(transform, false);

        _inactiveRoot = new GameObject("Inactive").transform;
        _inactiveRoot.SetParent(_objectsRoot, false);
        _inactiveRoot.gameObject.SetActive(false);

        for (int index = 0; index < _initialPools.Length; index++)
        {
            InitialPool setting = _initialPools[index];
            Prewarm(setting.Prefab, setting.Count);
        }
    }

    public void Prewarm(PoolableObject prefab, int count)
    {
        if (_isClosing || !prefab || !_inactiveRoot || count <= 0)
        {
            return;
        }

        Stack<PoolableObject> pool = GetOrCreatePool(prefab);

        // 사용 가능한 대기 오브젝트를 지정한 개수까지 확보한다.
        while (pool.Count < count)
        {
            pool.Push(CreateInstance(prefab));
        }
    }

    public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : PoolableObject
    {
        if (_isClosing || !isActiveAndEnabled || !prefab || !_objectsRoot || !_objectsRoot.gameObject.activeInHierarchy)
        {
            return null;
        }

        PoolableObject instance = TakeInstance(prefab);

        // 비활성 상태에서 배치해 이전 위치에서 활성화되는 것을 방지한다.
        Transform instanceTransform = instance.transform;
        instanceTransform.SetParent(_objectsRoot, false);
        instanceTransform.localScale = prefab.transform.localScale;
        instanceTransform.SetPositionAndRotation(position, rotation);

        return ActivateInstance<T>(instance);
    }

    public T Spawn<T>(T prefab, Transform parent) where T : PoolableObject
    {
        if (_isClosing || !isActiveAndEnabled || !prefab || !parent || !parent.gameObject.activeInHierarchy || !_objectsRoot || !_objectsRoot.gameObject.activeInHierarchy)
        {
            return null;
        }

        PoolableObject instance = TakeInstance(prefab);
        Transform instanceTransform = instance.transform;

        // UI의 Awake·OnEnable이 실행되기 전에 실제 Content 부모와 로컬 배치를 복구한다.
        instanceTransform.SetParent(parent, false);
        instanceTransform.localPosition = prefab.transform.localPosition;
        instanceTransform.localRotation = prefab.transform.localRotation;
        instanceTransform.localScale = prefab.transform.localScale;

        if (instanceTransform is RectTransform rect && prefab.transform is RectTransform prefabRect)
        {
            rect.anchorMin = prefabRect.anchorMin;
            rect.anchorMax = prefabRect.anchorMax;
            rect.pivot = prefabRect.pivot;
            rect.sizeDelta = prefabRect.sizeDelta;
            rect.anchoredPosition3D = prefabRect.anchoredPosition3D;
        }

        _parentedInstances.Add(instance);
        return ActivateInstance<T>(instance);
    }

    public bool Despawn(PoolableObject instance)
    {
        if (_isClosing || !instance || instance.PoolOwner != this || !instance.IsSpawned)
        {
            return false;
        }

        if (!_pools.TryGetValue(instance.SourcePrefab, out Stack<PoolableObject> pool))
        {
            return false;
        }

        // 반환 콜백이나 OnDisable에서 다시 반환하더라도 중복 적재하지 않는다.
        instance.IsSpawned = false;
        _parentedInstances.Remove(instance);
        instance.NotifyPoolDespawned();

        if (!instance)
        {
            return true;
        }

        instance.gameObject.SetActive(false);
        instance.transform.SetParent(_inactiveRoot, false);
        pool.Push(instance);
        return true;
    }

    private PoolableObject TakeInstance(PoolableObject prefab)
    {
        Stack<PoolableObject> pool = GetOrCreatePool(prefab);
        PoolableObject instance = null;

        // 외부에서 파괴된 항목이 남아 있으면 건너뛴다.
        while (pool.Count > 0 && !instance)
        {
            instance = pool.Pop();
        }

        return instance ? instance : CreateInstance(prefab);
    }

    private static T ActivateInstance<T>(PoolableObject instance) where T : PoolableObject
    {
        instance.IsSpawned = true;
        instance.gameObject.SetActive(true);

        if (instance && instance.IsSpawned)
        {
            instance.NotifyPoolSpawned();
        }

        return instance && instance.IsSpawned ? (T)instance : null;
    }

    private Stack<PoolableObject> GetOrCreatePool(PoolableObject prefab)
    {
        if (!_pools.TryGetValue(prefab, out Stack<PoolableObject> pool))
        {
            pool = new Stack<PoolableObject>();
            _pools.Add(prefab, pool);
        }

        return pool;
    }

    private PoolableObject CreateInstance(PoolableObject prefab)
    {
        // 비활성 부모 아래에서 생성해 실제 스폰 전 활성화를 방지한다.
        PoolableObject instance = Instantiate(prefab, _inactiveRoot, false);
        instance.gameObject.SetActive(false);
        instance.BindPool(this, prefab);
        return instance;
    }

    protected override void OnSingletonDestroyed()
    {
        _isClosing = true;
        _pools.Clear();

        foreach (PoolableObject instance in _parentedInstances)
        {
            if (instance)
            {
                Destroy(instance.gameObject);
            }
        }

        _parentedInstances.Clear();

        // 활성 오브젝트와 대기 오브젝트 모두 매니저의 수명을 따른다.
        if (_objectsRoot)
        {
            Destroy(_objectsRoot.gameObject);
        }
    }
}
