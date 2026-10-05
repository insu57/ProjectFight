using UnityEngine;

// 대상별 실행 규칙은 파생 클래스에 두고 플레이어 추적과 범위 검사는 한 곳에서 처리한다.
public abstract class InteractionTriggerBase : MonoBehaviour, IInteractable
{
    private CharacterController _trackedController;
    private PlayerManager _trackedPlayer;

    protected BoxCollider Range { get; private set; }
    protected abstract MonoBehaviour InteractionOwner { get; }
    protected abstract bool CanTrackPlayer { get; }
    protected abstract bool CanExecuteInteraction { get; }
    protected virtual bool IsOwnerValidInRange => true;

    public Vector3 InteractionPosition
    {
        get
        {
            MonoBehaviour owner = InteractionOwner;
            return owner ? owner.transform.position : transform.position;
        }
    }

    // 기존 컴포넌트의 직렬화 문구 필드는 파생 클래스에 그대로 유지한다.
    public abstract string InteractionText { get; }

    protected void Awake()
    {
        Range = GetComponent<BoxCollider>();
        ConfigureTriggerComponents();
    }

    protected virtual void ConfigureTriggerComponents()
    {
    }

    protected void OnTriggerEnter(Collider other)
    {
        TrackPlayer(other);
    }

    protected void OnTriggerStay(Collider other)
    {
        // 재활성화나 소유자 재초기화 뒤에도 범위 안의 후보 등록을 복구한다.
        TrackPlayer(other);
    }

    private void TrackPlayer(Collider other)
    {
        if (!isActiveAndEnabled || !Range || !Range.enabled || !CanTrackPlayer || other is not CharacterController controller)
        {
            return;
        }

        if (controller != _trackedController)
        {
            if (!controller.TryGetComponent(out PlayerManager player))
            {
                return;
            }

            ClearTracking();
            _trackedController = controller;
            _trackedPlayer = player;
        }

        if (_trackedPlayer)
        {
            _trackedPlayer.RegisterInteraction(this);
        }
    }

    public bool IsPlayerInRange(PlayerManager player)
    {
        if (!isActiveAndEnabled || !Range || !Range.enabled || !IsOwnerValidInRange || !_trackedController || !_trackedController.enabled)
        {
            return false;
        }

        if (!player || !player.isActiveAndEnabled || player != _trackedPlayer)
        {
            return false;
        }

        // 실제 Box 범위를 재검사해 텔레포트나 놓친 Exit 뒤의 오래된 후보를 거른다.
        Vector3 playerCenter = _trackedController.transform.TransformPoint(_trackedController.center);
        Vector3 localCenter = transform.InverseTransformPoint(playerCenter) - Range.center;
        Vector3 halfSize = Range.size * 0.5f;
        return Mathf.Abs(localCenter.x) <= halfSize.x && Mathf.Abs(localCenter.y) <= halfSize.y && Mathf.Abs(localCenter.z) <= halfSize.z;
    }

    public bool CanInteract(PlayerManager player)
    {
        return IsPlayerInRange(player) && CanExecuteInteraction;
    }

    public bool TryInteract(PlayerManager player)
    {
        // 안내에 사용한 범위와 대상 상태를 실행 직전에도 같은 경로로 확인한다.
        if (!CanInteract(player) || !TryExecuteInteraction(player))
        {
            return false;
        }

        OnInteractionSucceeded();
        return true;
    }

    protected abstract bool TryExecuteInteraction(PlayerManager player);

    protected virtual void OnInteractionSucceeded()
    {
    }

    protected void OnTriggerExit(Collider other)
    {
        if (other == _trackedController)
        {
            ClearTracking();
        }
    }

    protected void OnDisable()
    {
        ClearTracking();
    }

    protected void ClearTracking()
    {
        if (_trackedPlayer)
        {
            _trackedPlayer.UnregisterInteraction(this);
        }

        _trackedController = null;
        _trackedPlayer = null;
    }
}
