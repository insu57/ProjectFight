public enum ActiveItemEffectKind
{
    RecoveryOverTime = 0,
    TimedStatModifier = 1
}

// HUD에는 실행 상태의 복사본만 전달하고, 지속시간의 진행과 해제는 효과 런타임이 소유한다.
public readonly struct ActiveItemEffectSnapshot
{
    public ItemDefinition SourceItem { get; }
    public ActiveItemEffectKind Kind { get; }
    public ItemRecoveryTarget RecoveryTarget { get; }
    public ItemStatType Stat { get; }
    public float RemainingTime { get; }

    public ActiveItemEffectSnapshot(ItemDefinition sourceItem, ItemRecoveryTarget recoveryTarget, float remainingTime)
    {
        SourceItem = sourceItem;
        Kind = ActiveItemEffectKind.RecoveryOverTime;
        RecoveryTarget = recoveryTarget;
        Stat = default;
        RemainingTime = remainingTime;
    }

    public ActiveItemEffectSnapshot(ItemDefinition sourceItem, ItemStatType stat, float remainingTime)
    {
        SourceItem = sourceItem;
        Kind = ActiveItemEffectKind.TimedStatModifier;
        RecoveryTarget = default;
        Stat = stat;
        RemainingTime = remainingTime;
    }
}
