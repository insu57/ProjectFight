using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

internal static class PlayerProgressionCsvParser
{
    private static readonly string[] ColumnNames = { "Level", "TargetExp", "SkillPointsOnReach", "MaxHealth", "AttackPower", "DefensePower", "MaxEnergy" };

    public static bool TryParse(string csv, out PlayerLevelData[] levels, out string error)
    {
        levels = Array.Empty<PlayerLevelData>();
        error = null;

        if (string.IsNullOrWhiteSpace(csv))
        {
            error = "CSV가 비어 있습니다.";
            return false;
        }

        var entries = new List<PlayerLevelData>();
        var fields = new List<string>(ColumnNames.Length);
        var fieldBuffer = new StringBuilder();
        bool hasHeader = false;
        int lineNumber = 0;
        int lastDataLine = 0;

        using (var reader = new StringReader(csv.TrimStart('\uFEFF')))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (!TryReadFields(line, fields, fieldBuffer, out error))
                {
                    error = $"{lineNumber}행: {error}";
                    return false;
                }

                if (fields.Count != ColumnNames.Length)
                {
                    error = $"{lineNumber}행: 열이 {ColumnNames.Length}개여야 하지만 {fields.Count}개입니다.";
                    return false;
                }

                if (!hasHeader)
                {
                    for (int i = 0; i < ColumnNames.Length; i++)
                    {
                        if (fields[i] == ColumnNames[i]) continue;
                        error = $"{lineNumber}행 {i + 1}열: 헤더는 '{ColumnNames[i]}'이어야 합니다.";
                        return false;
                    }

                    hasHeader = true;
                    continue;
                }

                if (!TryReadInt(fields, 0, lineNumber, out int level, out error) || !TryReadInt(fields, 1, lineNumber, out int targetExp, out error) || !TryReadInt(fields, 2, lineNumber, out int points, out error)) return false;
                if (!TryReadFloat(fields, 3, lineNumber, out float maxHealth, out error) || !TryReadFloat(fields, 4, lineNumber, out float attack, out error) || !TryReadFloat(fields, 5, lineNumber, out float defense, out error) || !TryReadFloat(fields, 6, lineNumber, out float maxEnergy, out error)) return false;

                if (level != entries.Count + 1)
                {
                    error = $"{lineNumber}행 'Level': 레벨은 1부터 연속된 오름차순이어야 합니다. 예상값은 {entries.Count + 1}입니다.";
                    return false;
                }

                if (maxHealth <= 0f)
                {
                    error = $"{lineNumber}행 'MaxHealth': 최대 체력은 0보다 커야 합니다.";
                    return false;
                }

                if (entries.Count > 0 && entries[entries.Count - 1].TargetExp == 0)
                {
                    error = $"{lastDataLine}행 'TargetExp': 요구 경험치 0은 마지막 레벨에서만 사용할 수 있습니다.";
                    return false;
                }

                entries.Add(new PlayerLevelData(level, targetExp, points, maxHealth, attack, defense, maxEnergy));
                lastDataLine = lineNumber;
            }
        }

        if (entries.Count == 0)
        {
            error = "CSV에 레벨 데이터가 없습니다.";
            return false;
        }

        if (entries[entries.Count - 1].TargetExp != 0)
        {
            error = $"{lastDataLine}행 'TargetExp': 마지막 레벨은 최대 레벨이므로 요구 경험치가 0이어야 합니다.";
            return false;
        }

        // 모든 행의 검증이 끝난 뒤에만 저장할 데이터를 반환한다.
        levels = entries.ToArray();
        return true;
    }

    private static bool TryReadInt(List<string> fields, int column, int lineNumber, out int value, out string error)
    {
        error = null;
        if (int.TryParse(fields[column], NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0) return true;

        error = $"{lineNumber}행 '{ColumnNames[column]}': 0 이상의 정수가 필요합니다. 입력값: '{fields[column]}'.";
        return false;
    }

    private static bool TryReadFloat(List<string> fields, int column, int lineNumber, out float value, out string error)
    {
        error = null;
        if (float.TryParse(fields[column], NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value)) return true;

        error = $"{lineNumber}행 '{ColumnNames[column]}': 0 이상의 유한한 숫자가 필요합니다. 소수점은 '.'을 사용합니다. 입력값: '{fields[column]}'.";
        return false;
    }

    private static bool TryReadFields(string line, List<string> fields, StringBuilder buffer, out string error)
    {
        fields.Clear();
        buffer.Clear();
        error = null;
        bool inQuotes = false;
        bool closedQuotes = false;

        // 숫자 셀을 따옴표로 감싼 CSV도 읽되 셀 안의 줄바꿈은 허용하지 않는다.
        for (int i = 0; i < line.Length; i++)
        {
            char character = line[i];
            if (inQuotes)
            {
                if (character != '"') buffer.Append(character);
                else if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    buffer.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                    closedQuotes = true;
                }

                continue;
            }

            if (character == ',')
            {
                fields.Add(buffer.ToString().Trim());
                buffer.Clear();
                closedQuotes = false;
                continue;
            }

            if (closedQuotes)
            {
                if (char.IsWhiteSpace(character)) continue;
                error = $"{fields.Count + 1}열: 닫는 따옴표 뒤에는 쉼표나 공백만 올 수 있습니다.";
                return false;
            }

            if (character == '"')
            {
                for (int j = 0; j < buffer.Length; j++)
                {
                    if (char.IsWhiteSpace(buffer[j])) continue;
                    error = $"{fields.Count + 1}열: 따옴표는 셀의 시작에서만 사용할 수 있습니다.";
                    return false;
                }

                buffer.Clear();
                inQuotes = true;
            }
            else buffer.Append(character);
        }

        if (inQuotes)
        {
            error = $"{fields.Count + 1}열: 따옴표가 닫히지 않았습니다. 셀 안의 줄바꿈은 지원하지 않습니다.";
            return false;
        }

        fields.Add(buffer.ToString().Trim());
        return true;
    }
}
