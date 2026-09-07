// 캐릭터 종류와 관계없이 Manager에 히트스탑을 요청한다.
public interface IHitStopReceiver
{
    public void BeginHitStop(float duration);
}
