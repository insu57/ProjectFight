using UnityEditor.Build;
using UnityEditor.Build.Reporting;

internal sealed class PlayerProgressionBuildValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        // 자동 임포트가 누락되어도 빌드 전 다시 변환하고, 잘못된 원본은 빌드를 중단한다.
        if (!PlayerProgressionCsvImporter.TryImportAll(out string error)) throw new BuildFailedException(error);
    }
}
