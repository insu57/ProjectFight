using UnityEngine;

public class StageManager : MonoBehaviour
{
    // 현재 스테이지 생성에 사용할 정의.
    [field: SerializeField] private StageDefinition _stageDefinition;
    //현재 스테이지 진행 정보...

    private MapGenerator _mapGenerator;
    private StageMapView _stageMapView;

    private void Awake()
    {
        if (!TryGetComponent(out _mapGenerator))
        {
            Debug.LogWarning("MapGenerator: NULL", this);
            return;
        }

        // 지도 탭이 닫혀 있어도 찾아서 생성기를 전달한다.
        _stageMapView = FindFirstObjectByType<StageMapView>(FindObjectsInactive.Include);

        if (_stageMapView)
        {
            _stageMapView.SetMapGenerator(_mapGenerator);
        }
        else
        {
            Debug.LogWarning("StageMapView: NULL", this);
        }
    }


    private void Start()
    {
        _mapGenerator.GenerateStage(_stageDefinition);
    }
}
