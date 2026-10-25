using UnityEngine;

// 프로젝트 공통 물리 쿼리 마스크를 캐싱하고 외부에는 읽기 전용으로 제공한다.
public static class GameLayerUtility
{
    public static int EnemyMask { get; private set; }
    public static int WallMask { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // 씬 컴포넌트의 Awake 전에 준비하며, Play Mode 재진입 시에도 설정을 다시 읽는다.
        EnemyMask = LayerMask.GetMask("Enemy");
        WallMask = LayerMask.GetMask("Wall");
    }
}
