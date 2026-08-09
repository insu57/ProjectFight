using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private Animator _playerAnimator;

    private void Awake()
    {
        _playerAnimator = GetComponentInChildren<Animator>();
    }
}
