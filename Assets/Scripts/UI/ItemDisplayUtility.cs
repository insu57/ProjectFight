using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// 아이템 표시 문구와 등급 색상을 한 곳에 정의하여 여러 UI에서 같은 기준을 사용한다.
public static class ItemDisplayUtility
{
    public static void BuildItemDescription(StringBuilder builder, ItemDefinition item, char[] numberBuffer)
    {
        // 뷰가 보관한 버퍼를 비워 상점과 인벤토리에 같은 형식의 설명을 만든다.
        builder.Clear();
        if (!item) return;

        AppendStatModifiersDescription(builder, item.StatModifiers, numberBuffer);
        if (item.ItemType == ItemType.Consumable) AppendItemEffectsDescription(builder, item.UseEffects, numberBuffer);
        // 유물 획득 효과는 보유 스탯과 함께 표시하되, 일회성 발동 시점을 각 줄에 명시한다.
        else if (item.ItemType == ItemType.Relic) AppendItemEffectsDescription(builder, item.AcquireEffects, numberBuffer, "획득 시 ");
        if (builder.Length > 0)
        {
            // 효과에만 밝은 노란색을 적용하고 닫는 태그 뒤에는 TMP의 기본 글자색을 사용한다.
            builder.Insert(0, "<color=#FFE082>");
            builder.Append("</color>");
        }

        if (!string.IsNullOrWhiteSpace(item.ItemDescription))
        {
            // 효과와 플레이버 텍스트 사이에 빈 줄을 하나 두고 설명 시작을 점으로 구분한다.
            if (builder.Length > 0) builder.Append("\n\n");
            builder.Append("• ").Append(item.ItemDescription);
        }
    }

    public static void AppendStatModifiersDescription(StringBuilder builder, IReadOnlyList<StatModifier> modifiers, char[] numberBuffer, float duration = 0f, string prefix = null)
    {
        if (modifiers == null) return;

        for (int i = 0; i < modifiers.Count; i++)
        {
            StatModifier modifier = modifiers[i];
            string statName = GetStatNameText(modifier.Stat);
            if (string.IsNullOrEmpty(statName) || modifier.Value == 0f || float.IsNaN(modifier.Value) || float.IsInfinity(modifier.Value)) continue;
            if (modifier.Operation != StatModifierOperation.FlatAdd && modifier.Operation != StatModifierOperation.PercentAdd) continue;

            if (builder.Length > 0) builder.Append('\n');
            builder.Append(prefix);
            if (duration > 0f)
            {
                // 여러 스탯을 보정하는 효과도 각 줄에서 유지 시간을 확인할 수 있게 한다.
                AppendDescriptionNumber(builder, duration, numberBuffer);
                builder.Append("초 동안 ");
            }
            builder.Append(statName).Append(' ');
            if (modifier.Value > 0f) builder.Append('+');

            // 비율 표기는 0.2를 20%로 바꾸며 큰 float 값도 배율 변환 중 넘치지 않게 한다.
            bool isPercent = modifier.Operation == StatModifierOperation.PercentAdd;
            double displayValue = isPercent ? (double)modifier.Value * 100d : modifier.Value;
            AppendDescriptionNumber(builder, displayValue, numberBuffer);
            builder.Append(isPercent ? "%" : GetFlatStatUnit(modifier.Stat));
        }
    }

    private static void AppendItemEffectsDescription(StringBuilder builder, IReadOnlyList<ItemEffectData> effects, char[] numberBuffer, string prefix = null)
    {
        if (effects == null) return;

        // 사용·획득 효과는 같은 수치 표기를 사용하며 공유 데이터를 변경하지 않고 등록 순서대로 설명한다.
        for (int i = 0; i < effects.Count; i++)
        {
            switch (effects[i])
            {
                case RestoreHealthEffectData health:
                    AppendRecoveryDescription(builder, "체력", health.Amount, numberBuffer, prefix: prefix);
                    break;
                case RestoreEnergyEffectData energy:
                    AppendRecoveryDescription(builder, "에너지", energy.Amount, numberBuffer, prefix: prefix);
                    break;
                case AddSkillPointsEffectData points:
                    AppendGrantedAmountDescription(builder, "스킬 포인트", points.Amount, numberBuffer, prefix);
                    break;
                case AddMoneyEffectData money:
                    AppendGrantedAmountDescription(builder, "돈", money.Amount, numberBuffer, prefix);
                    break;
                case AddExperienceEffectData experience:
                    AppendGrantedAmountDescription(builder, "경험치", experience.Amount, numberBuffer, prefix);
                    break;
                // 시간 효과는 실제 시작 가능 판정과 맞춰 잘못된 시간·간격·대상의 설명을 건너뛴다.
                case RecoveryOverTimeEffectData recovery when RecoveryOverTimeRuntime.CanStart(recovery):
                    AppendRecoveryDescription(builder, recovery.Target == ItemRecoveryTarget.Health ? "체력" : "에너지", recovery.Amount, numberBuffer, recovery.Duration, recovery.Interval, prefix);
                    break;
                case TimedStatModifierEffectData timed when TimedStatModifierRuntime.CanStart(timed):
                    AppendStatModifiersDescription(builder, timed.StatModifiers, numberBuffer, timed.Duration, prefix);
                    break;
            }
        }
    }

    private static void AppendRecoveryDescription(StringBuilder builder, string targetName, ItemRecoveryAmount amount, char[] numberBuffer, float duration = 0f, float interval = 0f, string prefix = null)
    {
        if (amount.Value <= 0f || float.IsNaN(amount.Value) || float.IsInfinity(amount.Value)) return;
        if (amount.Mode != ItemRecoveryMode.Fixed && amount.Mode != ItemRecoveryMode.MaxRatio) return;

        if (builder.Length > 0) builder.Append('\n');
        builder.Append(prefix);
        if (duration > 0f)
        {
            AppendDescriptionNumber(builder, duration, numberBuffer);
            builder.Append("초 동안 ");
            AppendDescriptionNumber(builder, interval, numberBuffer);
            builder.Append("초마다 ");
        }

        bool isPercent = amount.Mode == ItemRecoveryMode.MaxRatio;
        if (isPercent) builder.Append("최대 ");
        builder.Append(targetName).Append(isPercent ? "의 " : " ");
        // 비율 회복은 실행 경로와 동일하게 최대 100%로 제한해 실제 회복량과 설명을 맞춘다.
        AppendDescriptionNumber(builder, isPercent ? Math.Min(1d, amount.Value) * 100d : amount.Value, numberBuffer);
        builder.Append(isPercent ? "% 회복" : " 회복");
    }

    private static void AppendGrantedAmountDescription(StringBuilder builder, string amountName, int amount, char[] numberBuffer, string prefix = null)
    {
        if (amount <= 0) return;

        if (builder.Length > 0) builder.Append('\n');
        builder.Append(prefix);
        builder.Append(amountName).Append(" +");
        AppendDescriptionNumber(builder, amount, numberBuffer);
    }

    private static void AppendDescriptionNumber(StringBuilder builder, double value, char[] numberBuffer)
    {
        // 스탯·회복량·시간·지급량 모두 같은 숫자 버퍼를 사용해 포맷 문자열 할당을 피한다.
        if (value.TryFormat(numberBuffer.AsSpan(), out int written, "0.##", CultureInfo.InvariantCulture))
        {
            builder.Append(numberBuffer, 0, written);
        }
        else
        {
            // 호출자가 충분한 숫자 버퍼를 주지 않아도 표시 문구를 누락하지 않는다.
            builder.Append(value.ToString("0.##", CultureInfo.InvariantCulture));
        }
    }

    private static string GetStatNameText(ItemStatType stat)
    {
        return stat switch
        {
            ItemStatType.AttackPower => "공격력",
            ItemStatType.DefensePower => "방어력",
            ItemStatType.MaxHealth => "최대 체력",
            ItemStatType.MaxEnergy => "최대 에너지",
            ItemStatType.MaxPoise => "최대 강인도",
            ItemStatType.PoiseRecoveryDelay => "강인도 회복 대기시간",
            ItemStatType.PoiseRecoveryRate => "강인도 회복량",
            ItemStatType.StaggerDuration => "경직 시간",
            ItemStatType.MaxGuardPoise => "최대 가드 강인도",
            ItemStatType.GuardPoiseRecoveryDelay => "가드 강인도 회복 대기시간",
            ItemStatType.GuardPoiseRecoveryRate => "가드 강인도 회복량",
            ItemStatType.GuardBreakDuration => "가드 브레이크 시간",
            ItemStatType.StaggerMoveDistance => "경직 밀림 거리",
            ItemStatType.GuardBreakMoveDistance => "가드 브레이크 밀림 거리",
            ItemStatType.MaxDodgeCount => "최대 회피 횟수",
            ItemStatType.MoveSpeedMultiplier => "이동 속도 배율",
            ItemStatType.PoiseDamage => "강인도 피해",
            ItemStatType.GuardPoiseDamage => "가드 강인도 피해",
            ItemStatType.EnergyRecoveryOnHit => "타격 시 에너지 회복",
            ItemStatType.EnergyRecoveryOnGuard => "가드 시 에너지 회복",
            ItemStatType.EnergyRecoveryOnDamageTaken => "피격 시 에너지 회복",
            _ => string.Empty
        };
    }

    private static string GetFlatStatUnit(ItemStatType stat)
    {
        return stat switch
        {
            ItemStatType.PoiseRecoveryDelay or ItemStatType.GuardPoiseRecoveryDelay
            or ItemStatType.StaggerDuration or ItemStatType.GuardBreakDuration => "초",
            ItemStatType.PoiseRecoveryRate or ItemStatType.GuardPoiseRecoveryRate => "/초",
            ItemStatType.StaggerMoveDistance or ItemStatType.GuardBreakMoveDistance => "m",
            ItemStatType.MaxDodgeCount => "회",
            _ => string.Empty
        };
    }

    public static string GetRarityText(ItemRarity rarity)
    {
        // enum.ToString이나 문자열 포맷 대신 고정 문구를 사용한다.
        return rarity switch
        {
            ItemRarity.Common => "일반",
            ItemRarity.Uncommon => "고급",
            ItemRarity.Rare => "희귀",
            ItemRarity.Epic => "영웅",
            ItemRarity.Legendary => "전설",
            _ => string.Empty
        };
    }

    public static string GetItemTypeText(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Equipment => "장비",
            ItemType.Consumable => "소모품",
            ItemType.Relic => "유물",
            _ => string.Empty
        };
    }

    public static string GetEquipmentSlotText(EquipmentSlot equipmentSlot)
    {
        // 고정 문구로 장비 부위를 표시하여 enum 문자열 변환 할당을 피한다.
        return equipmentSlot switch
        {
            EquipmentSlot.None => "미지정",
            EquipmentSlot.Head => "머리",
            EquipmentSlot.UpperBody => "상의",
            EquipmentSlot.LowerBody => "하의",
            EquipmentSlot.Gloves => "장갑",
            EquipmentSlot.Shoes => "신발",
            _ => string.Empty
        };
    }

    public static Color GetPriceColor(bool canAfford)
    {
        // 지불 가능한 가격과 금액이 부족한 가격의 표시 색상을 공통으로 관리한다.
        return canAfford ? Color.black : Color.red;
    }

    public static Color GetRarityColor(ItemRarity rarity)
    {
        // Color32는 값 형식이므로 색상 변환 시 별도 객체를 할당하지 않는다.
        return rarity switch
        {
            ItemRarity.Common => Color.white,
            ItemRarity.Uncommon => new Color32(76, 175, 80, 255),
            ItemRarity.Rare => new Color32(66, 165, 245, 255),
            ItemRarity.Epic => new Color32(171, 71, 188, 255),
            ItemRarity.Legendary => new Color32(255, 167, 38, 255),
            _ => Color.white
        };
    }
}
