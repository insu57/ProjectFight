using System.Collections.Generic;

// 효과 실행기는 하위 컴포넌트를 직접 참조하지 않고 Manager에 필요한 조회와 변경만 요청한다.
public interface IPlayerItemEffectHost
{
    public bool CanRunItemEffects { get; }
    public PlayerStatSnapshot CurrentPlayerStats { get; }
    public int RemainingSkillPointCapacity { get; }

    public void RestoreHealth(float amount);
    public void RestoreEnergy(float amount);
    public void AddSkillPoints(int amount);
    public void AddMoney(int amount);
    public void AddExperience(int amount);
    public void RebuildItemStatModifiers(IReadOnlyList<ItemDefinition> relicItems, IReadOnlyList<ItemDefinition> equippedItems,
        IReadOnlyList<StatModifier> itemPassiveModifiers);
    public void RebuildTimedStatModifiers(IReadOnlyList<StatModifier> modifiers);
    public void RebuildItemPassiveStatModifiers(IReadOnlyList<StatModifier> modifiers);
}
