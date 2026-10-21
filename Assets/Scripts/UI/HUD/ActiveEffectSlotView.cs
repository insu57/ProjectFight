using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ActiveEffectSlotView : PoolableObject
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _remainingTimeText;

    // int 최대 초의 열 자리와 '초'를 담는 버퍼를 재사용한다.
    private readonly char[] _timeCharacters = new char[11];
    private int _displayedSeconds = -1;

    private void Awake()
    {
        // 전투 HUD가 마우스 입력을 가로채지 않도록 표시 요소의 Raycast를 끈다.
        if (_icon) _icon.raycastTarget = false;
        if (_remainingTimeText) _remainingTimeText.raycastTarget = false;
    }

    protected override void OnPoolSpawned()
    {
        ResetDisplay();
    }

    protected override void OnPoolDespawned()
    {
        // 다른 HUD에 재대여되어도 이전 효과의 아이콘과 잔여시간을 남기지 않는다.
        ResetDisplay();
    }

    public void Refresh(Sprite icon, float remainingTime)
    {
        if (!(remainingTime > 0f))
        {
            Hide();
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (_icon)
        {
            if (_icon.sprite != icon) _icon.sprite = icon;
            _icon.enabled = icon;
        }

        // 올림하여 효과가 살아 있는 동안 0초로 보이지 않게 하고, 숫자가 바뀔 때만 TMP를 갱신한다.
        int seconds = remainingTime >= int.MaxValue ? int.MaxValue : Mathf.Max(1, Mathf.CeilToInt(remainingTime));
        if (!_remainingTimeText || _displayedSeconds == seconds) return;

        int start = _timeCharacters.Length;
        _timeCharacters[--start] = '초';
        int remaining = seconds;
        do
        {
            _timeCharacters[--start] = (char)('0' + remaining % 10);
            remaining /= 10;
        } while (remaining > 0);

        _remainingTimeText.SetCharArray(_timeCharacters, start, _timeCharacters.Length - start);
        _displayedSeconds = seconds;
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
        ResetDisplay();
    }

    private void ResetDisplay()
    {
        _displayedSeconds = -1;
        if (_icon)
        {
            _icon.sprite = null;
            _icon.enabled = false;
        }

        if (_remainingTimeText) _remainingTimeText.SetText(string.Empty);
    }
}
