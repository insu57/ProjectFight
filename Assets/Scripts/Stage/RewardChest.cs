using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RewardChest : MonoBehaviour
{
    [Header("상자 상호작용")]
    [SerializeField] private RewardChestInteractionTrigger _interactionTrigger;

    [Header("상자 뚜껑")]
    [SerializeField] private Transform _chestTop;
    [SerializeField, Min(0.01f)] private float _openDuration = 0.6f;

    private const float OpenXAngle = -140f;
    private float _openProgress;

    public bool IsOpen { get; private set; }
    public bool IsOpening { get; private set; }
    public bool CanOpen => isActiveAndEnabled && !IsOpen && _chestTop;
    [field:SerializeField] public ItemDefinition RewardItem { get; private set; }

    // 후속 보상 처리가 상자를 연 플레이어를 받을 수 있도록 전달한다.
    public event Action<PlayerManager> Opened;

    private void Awake()
    {
        // 이 모델의 닫힌 로컬 회전은 0도이다. 프리팹에 저장된 열린 자세도 초기화한다.
        if (_chestTop)
        {
            _chestTop.localRotation = Quaternion.identity;
        }

        // 상자가 지정한 Trigger에 소유자를 주입하고 계층 검색은 하지 않는다.
        if (_interactionTrigger)
        {
            _interactionTrigger.Initialize(this);
        }
    }

    public void SetRewardItem(ItemDefinition item)
    {
        // 재등록이나 재활성화에도 이미 배정된 보상과 열린 상자의 결과를 유지한다.
        if (!item || RewardItem || IsOpen)
        {
            return;
        }

        RewardItem = item;
    }

    public bool TryOpen(PlayerManager player)
    {
        if (!CanOpen || !player || !player.isActiveAndEnabled)
        {
            return false;
        }

        // 인벤토리 변경 이벤트에서 다시 호출되어도 보상 지급과 개방 알림은 한 번만 발생한다.
        IsOpen = true;
        IsOpening = true;
        _openProgress = 0f;

        // 실제 보유 목록의 변경과 UI 알림은 상자를 연 플레이어의 Manager가 담당한다.
        if (RewardItem)
        {
            player.TryAddItem(RewardItem);
        }
        else
        {
            Debug.LogWarning("보상 상자에 배정된 아이템이 없습니다.", this);
        }

        Opened?.Invoke(player);
        return true;
    }

    private void Update()
    {
        if (!IsOpening || !_chestTop)
        {
            return;
        }

        // 개방 중에만 갱신하며 코루틴이나 Tween 객체를 생성하지 않는다.
        float duration = Mathf.Max(0.01f, _openDuration);
        _openProgress = Mathf.Min(1f, _openProgress + Time.deltaTime / duration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, _openProgress);
        _chestTop.localRotation = Quaternion.Euler(OpenXAngle * easedProgress, 0f, 0f);

        if (_openProgress >= 1f)
        {
            IsOpening = false;
        }
    }
}
