using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerStatContentView : MonoBehaviour
{
    private PlayerManager _playerManager;

    [Header("기본 정보")]
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private TMP_Text _experienceText;
    [SerializeField] private TMPro.TMP_Text _moneyText;

    [Header("전투 능력치")]
    [SerializeField] private TMP_Text _healthText;
    [SerializeField] private TMP_Text _attackPowerText;
    [SerializeField] private TMP_Text _defensePowerText;
    [SerializeField] private TMP_Text _energyText;

    // int 최대 금액의 10자리까지 한 번 만든 버퍼를 재사용한다.
    private readonly char[] _moneyCharacters = new char[10];
    private int _displayedMoney = -1;

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

        _displayedMoney = -1;
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

        RefreshMoney(stats.Money);

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

    private void RefreshMoney(int money)
    {
        if (!_moneyText || _displayedMoney == money)
        {
            return;
        }

        // 문자열 생성이나 float 변환 없이 정수 금액을 정확하게 표시한다.
        int remaining = Mathf.Max(0, money);
        int start = _moneyCharacters.Length;
        do
        {
            _moneyCharacters[--start] = (char)('0' + remaining % 10);
            remaining /= 10;
        } while (remaining > 0);

        _moneyText.SetCharArray(_moneyCharacters, start, _moneyCharacters.Length - start);
        _displayedMoney = money;
    }
}
