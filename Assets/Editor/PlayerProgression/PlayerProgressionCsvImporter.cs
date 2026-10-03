using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

internal static class PlayerProgressionCsvImporter
{
    public const string SourceFolder = "Assets/Data/PlayerProgression";
    private static readonly Encoding CsvEncoding = new UTF8Encoding(false, true);

    public static bool IsSourceCsv(string path)
    {
        return !string.IsNullOrEmpty(path) && path.StartsWith(SourceFolder + "/", StringComparison.Ordinal)
            && path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
    }

    [MenuItem("Tools/Player Progression/CSV 다시 변환")]
    private static void RebuildAll()
    {
        if (!TryImportAll(out string error)) Debug.LogError(error);
        else Debug.Log("플레이어 성장 CSV를 SO 에셋으로 변환했습니다.");
    }

    public static bool TryImportAll(out string error)
    {
        error = null;
        if (!AssetDatabase.IsValidFolder(SourceFolder)) return true;

        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { SourceFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string csvPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (IsSourceCsv(csvPath) && !TryImport(csvPath, out error)) return false;
        }

        // 원본이 삭제되거나 관리 폴더 밖으로 이동한 SO의 배포를 방지한다.
        guids = AssetDatabase.FindAssets("t:PlayerProgressionDefinition", new[] { SourceFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            var definition = AssetDatabase.LoadAssetAtPath<PlayerProgressionDefinition>(assetPath);
            string csvPath = definition && !string.IsNullOrEmpty(definition.SourceCsvGuid)
                ? AssetDatabase.GUIDToAssetPath(definition.SourceCsvGuid) : string.Empty;
            if (IsSourceCsv(csvPath) && File.Exists(csvPath)) continue;

            error = $"{assetPath}: 원본 성장 CSV를 찾을 수 없습니다. 원본을 복원하거나 사용하지 않는 생성 에셋을 제거하세요.";
            return false;
        }

        return true;
    }

    public static bool TryImport(string csvPath, out string error)
    {
        error = null;
        if (!IsSourceCsv(csvPath))
        {
            error = $"{csvPath}: 성장 CSV는 '{SourceFolder}' 아래에 있어야 합니다.";
            return false;
        }

        try
        {
            string csv = File.ReadAllText(csvPath, CsvEncoding);
            if (!PlayerProgressionCsvParser.TryParse(csv, out PlayerLevelData[] levels, out error))
            {
                error = $"{csvPath}: {error}";
                return false;
            }

            string sourceGuid = AssetDatabase.AssetPathToGUID(csvPath);
            if (string.IsNullOrEmpty(sourceGuid))
            {
                error = $"{csvPath}: CSV가 아직 임포트되지 않았습니다. Unity의 Asset Refresh 후 다시 변환하세요.";
                return false;
            }

            string sourceHash = Hash128.Compute(csv).ToString();
            string assetPath = Path.ChangeExtension(csvPath, ".asset");
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var definition = existing as PlayerProgressionDefinition;

            if ((existing && !definition) || (!existing && File.Exists(assetPath)) || (definition && definition.SourceCsvGuid != sourceGuid))
            {
                error = $"{assetPath}: 다른 에셋이 같은 경로를 사용하고 있어 덮어쓰지 않았습니다.";
                return false;
            }

            if (!definition && !TryFindMovedDefinition(sourceGuid, assetPath, out definition, out error)) return false;
            if (definition && definition.name == Path.GetFileNameWithoutExtension(csvPath)
                && HasMatchingData(definition, levels, sourceGuid, sourceHash)) return true;

            // 파싱 실패 시 기존 SO는 그대로 유지하고, 정상 데이터만 동일 에셋에 반영한다.
            if (!definition)
            {
                definition = ScriptableObject.CreateInstance<PlayerProgressionDefinition>();
                definition.name = Path.GetFileNameWithoutExtension(csvPath);
                definition.SetImportedData(levels, sourceGuid, sourceHash);
                AssetDatabase.CreateAsset(definition, assetPath);
            }
            else
            {
                definition.name = Path.GetFileNameWithoutExtension(csvPath);
                definition.SetImportedData(levels, sourceGuid, sourceHash);
                EditorUtility.SetDirty(definition);
            }

            AssetDatabase.SaveAssetIfDirty(definition);
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is DecoderFallbackException)
        {
            error = $"{csvPath}: UTF-8 CSV를 읽거나 에셋을 저장하지 못했습니다. {exception.Message}";
            return false;
        }
    }

    private static bool TryFindMovedDefinition(string sourceGuid, string destination, out PlayerProgressionDefinition definition, out string error)
    {
        definition = null;
        error = null;
        string[] guids = AssetDatabase.FindAssets("t:PlayerProgressionDefinition", new[] { SourceFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            var candidate = AssetDatabase.LoadAssetAtPath<PlayerProgressionDefinition>(assetPath);
            if (!candidate || candidate.SourceCsvGuid != sourceGuid) continue;

            // CSV의 이름/폴더가 바뀌어도 기존 SO의 GUID와 연결된 참조를 유지한다.
            string moveError = AssetDatabase.MoveAsset(assetPath, destination);
            if (!string.IsNullOrEmpty(moveError))
            {
                error = $"{assetPath}: 성장 SO 이동에 실패했습니다. {moveError}";
                return false;
            }

            definition = candidate;
            return true;
        }

        return true;
    }

    private static bool HasMatchingData(PlayerProgressionDefinition definition, PlayerLevelData[] levels, string sourceGuid, string sourceHash)
    {
        if (definition.SourceCsvGuid != sourceGuid || definition.SourceCsvHash != sourceHash || definition.MaxLevel != levels.Length) return false;

        for (int i = 0; i < levels.Length; i++)
        {
            definition.TryGetLevel(i + 1, out PlayerLevelData current);
            PlayerLevelData expected = levels[i];
            if (current.Level != expected.Level || current.TargetExp != expected.TargetExp || current.SkillPointsOnReach != expected.SkillPointsOnReach || current.MaxHealth != expected.MaxHealth || current.AttackPower != expected.AttackPower || current.DefensePower != expected.DefensePower || current.MaxEnergy != expected.MaxEnergy) return false;
        }

        return true;
    }
}

internal sealed class PlayerProgressionCsvPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
        string[] movedFromAssetPaths, bool didDomainReload)
    {
        if (didDomainReload)
        {
            if (!PlayerProgressionCsvImporter.TryImportAll(out string error)) Debug.LogError(error);
            return;
        }

        // SO 저장으로 다시 호출될 때는 CSV 경로만 처리하여 재귀 임포트를 막는다.
        ImportCsvPaths(importedAssets);
        ImportCsvPaths(movedAssets);
    }

    private static void ImportCsvPaths(string[] paths)
    {
        for (int i = 0; i < paths.Length; i++)
        {
            if (!PlayerProgressionCsvImporter.IsSourceCsv(paths[i])) continue;
            if (!PlayerProgressionCsvImporter.TryImport(paths[i], out string error)) Debug.LogError(error);
        }
    }
}
