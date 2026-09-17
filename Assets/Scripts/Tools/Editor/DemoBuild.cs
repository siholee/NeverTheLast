using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 데모 빌드 진입점. Editor 메뉴와 명령줄 양쪽에서 부른다.
///
/// 명령줄:
///   Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod DemoBuild.BuildWindows -logFile Logs/DemoBuild.log
///
/// 개발 빌드가 아니므로 F1 디버그 패널·치트 단축키(DEVELOPMENT_BUILD)는 들어가지 않는다.
/// 씬 목록은 Build Settings(EditorBuildSettings)를 그대로 쓴다.
/// </summary>
public static class DemoBuild
{
    public const string OutputDirectory = "Builds/NeverTheLast_Demo_Win64";
    public const string ExecutableName = "NeverTheLast.exe";

    [MenuItem("Tools/NeverTheLast/Build Windows Demo")]
    public static void BuildWindows()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new InvalidOperationException("[DemoBuild] Build Settings에 켜진 씬이 없습니다.");

        if (Directory.Exists(OutputDirectory)) Directory.Delete(OutputDirectory, true);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(OutputDirectory, ExecutableName),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"[DemoBuild] 결과 {summary.result} · 오류 {summary.totalErrors} · 경고 {summary.totalWarnings} · " +
                  $"{summary.totalSize / (1024f * 1024f):0.0}MB · {summary.totalTime}");

        if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
            EditorApplication.Exit(1);
    }
}
