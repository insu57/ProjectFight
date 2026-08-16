using UnityEngine;

public enum PlayerAttackType
{
    Light,
    Heavy,
    Grab
}

[DisallowMultipleComponent]
public sealed class PlayerAttack : MonoBehaviour
{
    public PlayerAttackType CurrentAttackType { get; private set; }
    public bool IsAttacking { get; private set; }

    public void BeginAttack(PlayerAttackType attackType)
    {
        CurrentAttackType = attackType;
        IsAttacking = true;
    }

    public void EndAttack()
    {
        IsAttacking = false;
    }
}
