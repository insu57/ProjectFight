using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    private PlayerControl _playerControl;
    private PlayerMove _playerMove;
    private PlayerAnimation _playerAnimation;
    private PlayerCamera _playerCamera;

    private void Awake()
    {
        TryGetComponent(out _playerControl);
        TryGetComponent(out _playerMove);
        TryGetComponent(out _playerAnimation);
        TryGetComponent(out _playerCamera);
    }


    private void Update()
    {
        if (!_playerControl || !_playerMove || !_playerCamera) return;

        _playerCamera.SetLookInput(_playerControl.LookInput, _playerControl.IsLookInputDelta);
        _playerMove.Move(_playerControl.MoveInput, _playerCamera.YawRotation, Time.deltaTime);
    }

}
