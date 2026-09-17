using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 데모 빌드 진입점. Editor 메뉴와 명령줄 양쪽에서 부른다.
///
/// 명령줄:
///   Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod DemoBuild.BuildWindows -logFile Logs/DemoBuild.log
///   Unity.exe -batchmode -quit -projectPath . -buildTarget OSXUniversal -executeMethod DemoBuild.BuildMac -logFile Logs/DemoBuildMac.log
///
/// Mac 빌드는 Windows에서도 만들 수 있다(Mono 백엔드). Unity Hub의 Mac Build Support(Mono) 모듈이 필요하다.
/// 서명·공증은 Mac에서만 되므로 배포본은 서명 없이 나간다 — 첫 실행 안내는 README에 있다.
///
/// 개발 빌드가 아니므로 F1 디버그 패널·치트 단축키(DEVELOPMENT_BUILD)는 들어가지 않는다.
/// 씬 목록은 Build Settings(EditorBuildSettings)를 그대로 쓴다.
/// </summary>
public static class DemoBuild
{
    public const string OutputDirectory = "Builds/NeverTheLast_Demo_Win64";
    public const string ExecutableName = "NeverTheLast.exe";

    public const string MacOutputDirectory = "Builds/NeverTheLast_Demo_Mac";
    public const string MacAppName = "NeverTheLast.app";

    [MenuItem("Tools/NeverTheLast/Build Windows Demo")]
    public static void BuildWindows()
    {
        Build(OutputDirectory, ExecutableName, BuildTarget.StandaloneWindows64);
    }

    [MenuItem("Tools/NeverTheLast/Build Mac Demo")]
    public static void BuildMac()
    {
        // 인텔과 애플 실리콘을 한 앱에 담는다. 이 설정 클래스는 Mac 모듈이 깔려 있을 때만 있으므로
        // 리플렉션으로 건드린다 — 직접 참조하면 모듈이 없는 PC에서 에디터 스크립트 컴파일이 깨진다.
        SetMacArchitecture("x64ARM64");
        Build(MacOutputDirectory, MacAppName, BuildTarget.StandaloneOSX);
    }

    private static void Build(string outputDirectory, string productName, BuildTarget target)
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new InvalidOperationException("[DemoBuild] Build Settings에 켜진 씬이 없습니다.");

        if (Directory.Exists(outputDirectory)) Directory.Delete(outputDirectory, true);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outputDirectory, productName),
            target = target,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"[DemoBuild] {target} 결과 {summary.result} · 오류 {summary.totalErrors} · 경고 {summary.totalWarnings} · " +
                  $"{summary.totalSize / (1024f * 1024f):0.0}MB · {summary.totalTime}");

        if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
            EditorApplication.Exit(1);
    }

    private static void SetMacArchitecture(string architectureName)
    {
        Type settings = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("UnityEditor.OSXStandalone.UserBuildSettings"))
            .FirstOrDefault(type => type != null);
        PropertyInfo property = settings?.GetProperty("architecture", BindingFlags.Public | BindingFlags.Static);
        if (property == null)
        {
            Debug.LogWarning("[DemoBuild] Mac 아키텍처 설정을 찾지 못했습니다. Mac Build Support 모듈이 설치되어 있는지 확인하세요.");
            return;
        }

        object value = Enum.Parse(property.PropertyType, architectureName);
        property.SetValue(null, value);
        Debug.Log($"[DemoBuild] Mac 아키텍처 {property.GetValue(null)}");
    }
}
