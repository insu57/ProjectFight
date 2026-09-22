using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    public static T Instance { get; private set; }

    protected void Awake()
    {
        if (Instance && !ReferenceEquals(Instance, this))
        {
            // 중복 매니저는 초기화하지 않고 해당 컴포넌트만 제거한다.
            enabled = false;
            Destroy(this);
            return;
        }

        Instance = (T)this;
        OnSingletonAwake();
    }

    protected void OnDestroy()
    {
        // 중복 컴포넌트의 제거가 기존 인스턴스에 영향을 주지 않도록 한다.
        if (!ReferenceEquals(Instance, this))
        {
            return;
        }

        Instance = null;
        OnSingletonDestroyed();
    }

    // 파생 매니저의 초기화는 싱글톤 등록 이후 실행한다.
    protected virtual void OnSingletonAwake()
    {
    }

    // 등록된 매니저가 파괴될 때 필요한 정리를 수행한다.
    protected virtual void OnSingletonDestroyed()
    {
    }
}
