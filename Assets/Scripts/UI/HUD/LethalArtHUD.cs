using UnityEngine;

[DisallowMultipleComponent]
public sealed class LethalArtHUD : MonoBehaviour
{
    private PlayerManager _playerManager;

    [Header("필살기 사용 가능 표시")]
    [SerializeField, Tooltip("이미지 등을 포함한 표시 오브젝트입니다. 이 컴포넌트는 계속 활성화되는 HUD 부모에 부착합니다.")]
    private GameObject _readyObject;

    private void Awake()
    {
        _playerManager = FindFirstObjectByType<PlayerManager>();

        // 자신이나 부모를 끄면 다시 사용 가능해져도 HUD가 갱신되지 않으므로 잘못된 연결을 차단한다.
        if (_readyObject && transform.IsChildOf(_readyObject.transform))
        {
            Debug.LogWarning("LethalArtHUD의 Ready Object에는 이 컴포넌트 자신이나 부모가 아닌 표시 오브젝트를 지정해야 합니다.", this);
            _readyObject = null;
        }
    }

    private void OnEnable()
    {
        // HUD가 비활성화된 동안 달라진 사용 가능 여부도 즉시 반영한다.
        Refresh();
    }

    private void LateUpdate()
    {
        // 이동과 상태 갱신 뒤 강공 입력과 같은 판정으로 표시하며, 조회만으로 필살기를 실행하지 않는다.
        Refresh();
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    private void Refresh()
    {
        if (!_readyObject) return;

        bool isAvailable = _playerManager && _playerManager.TryGetAvailableLethalArt(out _, out _);
        SetVisible(isAvailable);
    }

    private void SetVisible(bool isVisible)
    {
        // 같은 상태에서는 SetActive를 반복하지 않아 불필요한 UI 활성화 처리를 피한다.
        if (!_readyObject || _readyObject.activeSelf == isVisible) return;

        _readyObject.SetActive(isVisible);
    }
}
