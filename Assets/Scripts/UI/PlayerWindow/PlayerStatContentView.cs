using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerStatContentView : MonoBehaviour
{
    private PlayerManager _playerManager;

    [Header("기본 정보")]
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private TMP_Text _experienceText;

    [Header("전투 능력치")]
    [SerializeField] private TMP_Text _healthText;
    [SerializeField] private TMP_Text _attackPowerText;
    [SerializeField] private TMP_Text _defensePowerText;
    [SerializeField] private TMP_Text _energyText;

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

        _playerManager.PlayerStatsChanged += Refresh;

        // 비활성화 중 놓친 이벤트가 있어도 창을 열 때 최신 상태를 표시한다.
        Refresh(_playerManager.CurrentPlayerStats);
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
        if (_levelText)
        {
            _levelText.SetText("{0}", stats.Level);
        }

        if (_experienceText)
        {
            _experienceText.SetText("{0} / {1}", stats.CurrentExp, stats.TargetExp);
        }

        if (_healthText)
        {
            _healthText.SetText("{0} / {1}", stats.CurrentHealth, stats.MaxHealth);
        }

        if (_attackPowerText)
        {
            _attackPowerText.SetText(stats.AttackPower.ToString("0.##"));
        }

        if (_defensePowerText)
        {
            _defensePowerText.SetText(stats.DefensePower.ToString("0.##"));
        }

        if (_energyText)
        {
            _energyText.SetText($"{stats.CurrentEnergy:0.##} / {stats.MaxEnergy:0.##}");
        }
        //GC할당 문제
    }
}
