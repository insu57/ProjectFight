using System;
using System.Globalization;
using System.Text;

// 필살기 SO의 설정만 읽어 발동 조건을 설명하며, 해금 상태와 현재 전투 상태는 변경하지 않는다.
public static class LethalArtDisplayUtility
{
    public static void AppendUnlockDescription(StringBuilder builder, LethalArtDefinition definition, char[] numberBuffer)
    {
        if (builder.Length > 0) builder.Append('\n');
        if (!definition)
        {
            builder.Append("필살기 정보를 찾을 수 없습니다.");
            return;
        }

        // 해금 안내 제목 없이 실제 발동 조건부터 표시한다.
        builder.Append("소모 에너지 ");
        AppendNumber(builder, definition.EnergyCost, numberBuffer);
        builder.Append("\n대상 적과의 거리 ");
        AppendNumber(builder, definition.ProximityDistance, numberBuffer);
        builder.Append("m 이내");

        AppendHealthCondition(builder, "플레이어", definition.PlayerHealthCondition, numberBuffer);
        AppendHealthCondition(builder, "대상 적", definition.EnemyHealthCondition, numberBuffer);
        AppendRequirement(builder, definition.EnemyGuardRequirement, "대상 적이 가드 중일 때", "대상 적이 가드 중이 아닐 때");
        AppendRequirement(builder, definition.EnemyStunRequirement, "대상 적이 기절 상태일 때", "대상 적이 기절 상태가 아닐 때");
        AppendRequirement(builder, definition.EnemyGuardBreakRequirement, "대상 적이 가드 브레이크 상태일 때", "대상 적이 가드 브레이크 상태가 아닐 때");
        AppendRequirement(builder, definition.PlayerSprintRequirement, "플레이어가 달리는 중일 때", "플레이어가 달리는 중이 아닐 때");
        AppendWallCondition(builder, definition, numberBuffer);
    }

    private static void AppendHealthCondition(StringBuilder builder, string subject, LethalArtHealthCondition condition, char[] numberBuffer)
    {
        if (!condition.Enabled) return;

        builder.Append('\n').Append(subject).Append(" 체력 ");
        // 런타임과 같이 경계를 포함하며, 단일 경계·동일 비율·범위 조건을 구분한다.
        if (condition.MinimumRatio == condition.MaximumRatio)
        {
            AppendNumber(builder, (double)condition.MinimumRatio * 100d, numberBuffer);
            builder.Append("%일 때");
        }
        else if (condition.MinimumRatio <= 0f && condition.MaximumRatio < 1f)
        {
            AppendNumber(builder, (double)condition.MaximumRatio * 100d, numberBuffer);
            builder.Append("% 이하");
        }
        else if (condition.MinimumRatio > 0f && condition.MaximumRatio >= 1f)
        {
            AppendNumber(builder, (double)condition.MinimumRatio * 100d, numberBuffer);
            builder.Append("% 이상");
        }
        else
        {
            AppendNumber(builder, (double)condition.MinimumRatio * 100d, numberBuffer);
            builder.Append("% 이상 ");
            AppendNumber(builder, (double)condition.MaximumRatio * 100d, numberBuffer);
            builder.Append("% 이하");
        }
    }

    private static void AppendRequirement(StringBuilder builder, LethalArtConditionRequirement requirement, string requiredText, string forbiddenText)
    {
        if (requirement == LethalArtConditionRequirement.Required) builder.Append('\n').Append(requiredText);
        else if (requirement == LethalArtConditionRequirement.Forbidden) builder.Append('\n').Append(forbiddenText);
    }

    private static void AppendWallCondition(StringBuilder builder, LethalArtDefinition definition, char[] numberBuffer)
    {
        LethalArtConditionRequirement requirement = definition.NearbyWallRequirement;
        if (requirement != LethalArtConditionRequirement.Required && requirement != LethalArtConditionRequirement.Forbidden) return;

        string origin = definition.WallProximityOrigin switch
        {
            LethalArtProximityOrigin.Player => "플레이어 기준 ",
            LethalArtProximityOrigin.TargetEnemy => "대상 적 기준 ",
            _ => null
        };
        if (origin == null) return;

        // 벽과 대상 적의 거리 조건은 SO의 공통 근접 거리를 그대로 사용한다.
        builder.Append('\n').Append(origin);
        AppendNumber(builder, definition.ProximityDistance, numberBuffer);
        builder.Append(requirement == LethalArtConditionRequirement.Required ? "m 이내에 벽 필요" : "m 이내에 벽 없음");
    }

    private static void AppendNumber(StringBuilder builder, double value, char[] numberBuffer)
    {
        // 뷰가 보관한 숫자 버퍼로 아이템 설명과 같은 형식을 사용하여 문자열 할당을 피한다.
        if (value.TryFormat(numberBuffer.AsSpan(), out int written, "0.##", CultureInfo.InvariantCulture)) builder.Append(numberBuffer, 0, written);
        else builder.Append(value.ToString("0.##", CultureInfo.InvariantCulture));
    }
}
