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

    [Header("보유 금액")]
    [SerializeField] private TMPro.TMP_Text _moneyText;

    [Header("막대 채움 이미지")]
    [SerializeField] private Image _healthFill;
    [SerializeField] private Image _energyFill;
    [SerializeField] private Image _experienceFill;

    [Header("막대 너비 기준")]
    [SerializeField, Min(1f)] private float _barReferenceStat = 100f;

    [Header("체력 막대 크기")]
    [Tooltip("체력 채움 이미지와 배경을 함께 늘릴 RectTransform입니다.")]
    [SerializeField] private RectTransform _healthBarRoot;
    [SerializeField, Min(0f)] private float _healthBaseWidth = 200f;
    [Tooltip("0이면 최대 표시 너비를 제한하지 않습니다.")]
    [SerializeField, Min(0f)] private float _healthMaxWidth;

    [Header("에너지 막대 크기")]
    [Tooltip("에너지 채움 이미지와 배경을 함께 늘릴 RectTransform입니다.")]
    [SerializeField] private RectTransform _energyBarRoot;
    [SerializeField, Min(0f)] private float _energyBaseWidth = 200f;
    [Tooltip("0이면 최대 표시 너비를 제한하지 않습니다.")]
    [SerializeField, Min(0f)] private float _energyMaxWidth;

    [Header("퀵슬롯")]
    [SerializeField] private QuickslotPanelView _quickslotPanelView;

    private int _displayedLevel = -1;
    // int 최대 금액의 10자리까지 한 번 만든 버퍼를 재사용한다.
    private readonly char[] _moneyCharacters = new char[10];
    private int _displayedMoney = -1;
    private float _displayedHealthWidth = -1f;
    private float _displayedEnergyWidth = -1f;

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
        _displayedMoney = -1;
        _displayedHealthWidth = -1f;
        _displayedEnergyWidth = -1f;
        _playerManager.PlayerStatsChanged += Refresh;
        _playerManager.QuickslotsChanged += RefreshQuickslots;

        // HUD가 꺼져 있는 동안 변경된 값도 즉시 반영한다.
        Refresh(_playerManager.CurrentPlayerStats);
        RefreshQuickslots();
    }

    private void Start()
    {
        // 최초 OnEnable이 PlayerStat.Awake보다 먼저 실행된 경우를 보정한다.
        if (_playerManager)
        {
            Refresh(_playerManager.CurrentPlayerStats);
            // PlayerManager와 PlayerQuickslot의 Awake가 모두 끝난 뒤 최초 등록 상태를 다시 확인한다.
            RefreshQuickslots();
        }
    }

    private void OnDisable()
    {
        if (_playerManager)
        {
            _playerManager.PlayerStatsChanged -= Refresh;
            _playerManager.QuickslotsChanged -= RefreshQuickslots;
        }
    }

    private void RefreshQuickslots()
    {
        // 인벤토리 창의 활성 상태와 관계없이 HUD용 공통 View에 현재 등록 아이콘을 전달한다.
        if (_quickslotPanelView) _quickslotPanelView.Refresh(_playerManager);
    }

    private void Refresh(PlayerStatSnapshot stats)
    {
        // 강인도 등 다른 스탯만 바뀌었을 때 레벨 텍스트는 갱신하지 않는다.
        if (_levelText && _displayedLevel != stats.Level)
        {
            _displayedLevel = stats.Level;
            _levelText.SetText("Lv.\n{0}", stats.Level);
        }

        RefreshMoney(stats.Money);

        // 장비와 유물이 반영된 최종 최대치로 전체 길이를 정하고, 현재량은 채움 비율로 표시한다.
        SetBarWidth(_healthBarRoot, stats.MaxHealth, _barReferenceStat, _healthBaseWidth, _healthMaxWidth, ref _displayedHealthWidth);
        SetBarWidth(_energyBarRoot, stats.MaxEnergy, _barReferenceStat, _energyBaseWidth, _energyMaxWidth, ref _displayedEnergyWidth);

        SetFill(_healthFill, stats.CurrentHealth, stats.MaxHealth);
        SetFill(_energyFill, stats.CurrentEnergy, stats.MaxEnergy);
        SetFill(_experienceFill, stats.CurrentExp, stats.TargetExp);
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

    private static void SetBarWidth(RectTransform barRoot, float maximum, float referenceStat, float baseWidth,
        float maxWidth, ref float displayedWidth)
    {
        if (!barRoot) return;

        // 기준 너비에서 매번 계산해 재활성화나 최대치 변경으로 확대 비율이 누적되지 않게 한다.
        float width = Mathf.Max(0f, baseWidth) * (Mathf.Max(0f, maximum) / Mathf.Max(1f, referenceStat));
        if (maxWidth > 0f) width = Mathf.Min(width, maxWidth);

        // 현재량만 바뀌거나 표시 상한에 머무르면 RectTransform을 다시 갱신하지 않는다.
        if (displayedWidth == width) return;

        barRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        displayedWidth = width;
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
