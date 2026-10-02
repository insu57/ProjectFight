using System;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InteractionPromptHUD : MonoBehaviour
{
    [Header("플레이어")]
    private PlayerManager _playerManager;

    [Header("상호작용 안내")]
    [SerializeField] private TMP_Text _promptText;
    [SerializeField] private string _keyLabel = "F";

    private string _displayedInteractionText;

    private void Awake()
    {
        _playerManager = FindFirstObjectByType<PlayerManager>();
    }

    private void OnEnable()
    {
        _displayedInteractionText = null;
        if (_playerManager)
        {
            _playerManager.InteractionPromptChanged += Refresh;
        }

        // HUD가 비활성화되어 있던 동안의 상태도 즉시 반영한다.
        Refresh(_playerManager ? _playerManager.CurrentInteractionText : string.Empty);
    }

    private void OnDisable()
    {
        if (_playerManager)
        {
            _playerManager.InteractionPromptChanged -= Refresh;
        }

        if (_promptText)
        {
            _promptText.enabled = false;
        }
    }

    private void Refresh(string interactionText)
    {
        if (!_promptText) return;

        // 텍스트 표시만 끄므로 HUD 컴포넌트의 이벤트 구독은 유지된다.
        bool isVisible = !string.IsNullOrEmpty(interactionText);
        _promptText.enabled = isVisible;
        if (!isVisible || string.Equals(_displayedInteractionText, interactionText, StringComparison.Ordinal)) return;

        _displayedInteractionText = interactionText;
        // 표시 문구가 바뀔 때만 문자열을 생성하며 매 프레임 포맷하지 않는다.
        _promptText.SetText(string.IsNullOrEmpty(_keyLabel) ? interactionText : string.Concat(_keyLabel, " : ", interactionText));
    }
}
