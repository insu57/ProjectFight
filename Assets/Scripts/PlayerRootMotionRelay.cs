using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerRootMotionRelay : MonoBehaviour
{
    private Animator _animator;
    private PlayerManager _playerManager;
    /*
    private void Awake()
    {
        TryGetComponent(out _animator);
        _playerManager = GetComponentInParent<PlayerManager>();
    }

    private void OnAnimatorMove()
    {
        if (!_animator || !_playerManager) return;

        // Animator가 계산한 이동량을 PlayerManager를 통해 실제 플레이어에 전달한다.
        _playerManager.ApplyDodgeRootMotion(_animator.deltaPosition, Time.deltaTime);
    }

    public void NotifyDodgeStateExited()
    {
        if (_playerManager)
        {
            _playerManager.NotifyDodgeStateExited();
        }
    }*/
}
