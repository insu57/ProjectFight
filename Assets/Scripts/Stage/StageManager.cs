using UnityEngine;

public class StageManager : MonoBehaviour
{
    //temp
    [field: SerializeField] private StageDefinition _stageDefinition;
    //현재 스테이지 진행 정보...

    private MapGenerator _mapGenerator;

    private void Awake()
    {
        if(!TryGetComponent(out _mapGenerator))
        {
            Debug.LogWarning("MapGenerator: NULL");
        }
    }


    private void Start()
    {
        _mapGenerator.GenerateStage(_stageDefinition);
    }
}
