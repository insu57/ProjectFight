using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerStatusHUD : MonoBehaviour
{
    [Header("플레이어")]
    private PlayerManager _playerManager;

    [Header("레벨")]
    [SerializeField] private TMP_Text _levelText;

    [Header("막대 채움 이미지")]
    [SerializeField] private Image _healthFill;
    [SerializeField] private Image _energyFill;
    [SerializeField] private Image _experienceFill;

    private int _displayedLevel = -1;

    private void Awake()
    {
        _playerManager = FindFirstObjectByType<PlayerManager>();
    }

    private void OnEnable()
    {
        if (!_playerManager)
        {
            return;
        }

        _displayedLevel = -1;
        _playerManager.PlayerStatsChanged += Refresh;

        // HUD가 꺼져 있는 동안 변경된 값도 즉시 반영한다.
        Refresh(_playerManager.CurrentPlayerStats);
    }

    private void Start()
    {
        // 최초 OnEnable이 PlayerStat.Awake보다 먼저 실행된 경우를 보정한다.
        if (_playerManager)
        {
            Refresh(_playerManager.CurrentPlayerStats);
        }
    }

    private void OnDisable()
    {
        if (_playerManager)
        {
            _playerManager.PlayerStatsChanged -= Refresh;
        }
    }

    private void Refresh(PlayerStatSnapshot stats)
    {
        // 강인도 등 다른 스탯만 바뀌었을 때 레벨 텍스트는 갱신하지 않는다.
        if (_levelText && _displayedLevel != stats.Level)
        {
            _displayedLevel = stats.Level;
            _levelText.SetText("Lv.\n{0}", stats.Level);
        }

        SetFill(_healthFill, stats.CurrentHealth, stats.MaxHealth);
        SetFill(_energyFill, stats.CurrentEnergy, stats.MaxEnergy);
        SetFill(_experienceFill, stats.CurrentExp, stats.TargetExp);
    }

    private static void SetFill(Image image, float current, float maximum)
    {
        if (!image)
        {
            return;
        }

        // 최대치가 0이면 빈 막대로 표시하고, 표시 비율은 0~1로 제한한다.
        float ratio = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;

        if (image.fillAmount != ratio)
        {
            image.fillAmount = ratio;
        }
    }
}
