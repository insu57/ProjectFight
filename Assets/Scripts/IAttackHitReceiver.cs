// 피격을 확정한 Manager가 공격자에게 결과를 전달한다. 회복량은 공격 시작 시 계산된 값이다.
public interface IAttackHitReceiver
{
    public void OnAttackHit(float energyRecoveryOnHit, bool wasBlocked);
}
