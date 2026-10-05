using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Store : MonoBehaviour
{
    [Header("상점 상호작용")]
    [SerializeField] private StoreInteractionTrigger _interactionTrigger;

    private bool _isRequestingInteraction;

    public bool CanInteract => isActiveAndEnabled && !_isRequestingInteraction;

    // 상점과 이용 플레이어를 전달하여 이후 상점창이 같은 요청을 받을 수 있게 한다.
    public event Action<Store, PlayerManager> InteractionRequested;

    private void Awake()
    {
        // 프리팹에서 지정한 Trigger에 소유 상점을 주입하고 계층 검색은 하지 않는다.
        if (_interactionTrigger)
        {
            _interactionTrigger.Initialize(this);
        }
    }

    public bool TryInteract(PlayerManager player)
    {
        if (!CanInteract || !player || !player.isActiveAndEnabled)
        {
            return false;
        }

        // 이벤트 처리 중의 재진입만 막고 요청이 끝나면 다시 이용할 수 있게 한다.
        _isRequestingInteraction = true;
        try
        {
            Debug.Log("상점 상호작용 테스트: 상점창 열기 요청", this);
            InteractionRequested?.Invoke(this, player);
            return true;
        }
        finally
        {
            _isRequestingInteraction = false;
        }
    }
}
