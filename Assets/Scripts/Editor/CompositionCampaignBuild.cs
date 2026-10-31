#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Tools.Editor
{
    public static class CompositionCampaignBuild
    {
        public static void BuildIntentStudy()
            => BuildAt(Environment.GetEnvironmentVariable("NTL_PARTY_STUDY_BUILD") ?? throw new InvalidOperationException("측정 빌드 경로가 없습니다."), false, "NeverTheLast Party Study 20261027");
        public static void BuildBaselineStudy()
            => BuildAt(Environment.GetEnvironmentVariable("NTL_PARTY_STUDY_BUILD") ?? throw new InvalidOperationException("측정 빌드 경로가 없습니다."), false, "NeverTheLast Baseline Study 20261030");
        public static void BuildBaselineStudyScripts()
            => BuildAt(Environment.GetEnvironmentVariable("NTL_PARTY_STUDY_BUILD") ?? throw new InvalidOperationException("측정 빌드 경로가 없습니다."), true, "NeverTheLast Baseline Study 20261030");
        public static void BuildShi200Study()
            => BuildAt(Environment.GetEnvironmentVariable("NTL_PARTY_STUDY_BUILD") ?? throw new InvalidOperationException("측정 빌드 경로가 없습니다."), false, "NeverTheLast Shi200 Study 20261030");
        public static void Build()
            => BuildAt("Logs/CompositionCampaign20261016/Player", true);

        public static void BuildAstolfo()
            => BuildAt("Logs/Astolfo20261016/Player", false);

        public static void BuildAstolfoScripts()
            => BuildAt("Logs/Astolfo20261016/Player", true);

        public static void BuildCounterBalance()
            => BuildAt("Logs/CounterBalance20261016/Player", false);

        public static void BuildCounterBalanceScripts()
            => BuildAt("Logs/CounterBalance20261016/Player", true);

        public static void BuildCounterIntrinsicScripts()
            => BuildAt("Logs/CounterIntrinsic20261016/Player", true);

        public static void BuildDifficultyScripts()
            => BuildAt("Logs/Difficulty20261017/Player", true);

        public static void BuildDifficultyV2Scripts()
            => BuildAt("Logs/Difficulty20261017V2/Player", true);

        public static void BuildDotRoster() => BuildAt("Logs/DotRoster20261017/Initial/Player", false);
        public static void BuildDotRosterScripts() => BuildAt("Logs/DotRoster20261017/Initial/Player", true);
        public static void BuildDotRosterTuned() => BuildAt("Logs/DotRoster20261017/Tuned/Player", false);
        public static void BuildDotRosterTunedScripts() => BuildAt("Logs/DotRoster20261017/Tuned/Player", true);
        public static void BuildDotDiagnosisScripts() => BuildAt("Logs/DotDiagnosis20261017/Player", true);

        public static void BuildDotPolicyScripts() => BuildAt("Logs/DotDiagnosis20261017/Policy/Player", true);

        public static void BuildDotRewardBalance() => BuildAt("Logs/DotDiagnosis20261017/RewardBalance/Player", false);
        public static void BuildDotRewardBalanceScripts() => BuildAt("Logs/DotDiagnosis20261017/RewardBalance/Player", true);

        public static void BuildDotRegressionScripts() => BuildAt("Logs/DotDiagnosis20261017/Regression/Player", true);

        public static void BuildSecretRewards() => BuildAt("Logs/SecretRewards20261017/Player", false);
        public static void BuildSecretRewardsScripts() => BuildAt("Logs/SecretRewards20261017/Player", true);
        public static void BuildDotRewardRetestScripts() => BuildAt("Logs/DotRewardsRetest20261017/Player", true);

        private static void BuildAt(string output, bool scriptsOnly, string productName = "NeverTheLast Composition QA")
        {
            string settings = Path.GetFullPath("ProjectSettings/ProjectSettings.asset");
            byte[] originalBytes = File.ReadAllBytes(settings);
            string originalName = PlayerSettings.productName;
            var target = NamedBuildTarget.Standalone;
            var backend = PlayerSettings.GetScriptingBackend(target);
            string root = Path.GetFullPath(output);
            Directory.CreateDirectory(root);
            try
            {
                PlayerSettings.productName = productName;
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.Mono2x);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = Path.Combine(root, "CompositionQA.exe"),
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development | (scriptsOnly ? BuildOptions.BuildScriptsOnly : BuildOptions.None),
                });
                File.WriteAllText(Path.Combine(root, "build-summary.json"), JsonUtility.ToJson(new Summary
                { result = report.summary.result.ToString(), errors = report.summary.totalErrors, warnings = report.summary.totalWarnings,
                    bytes = report.summary.totalSize, seconds = report.summary.totalTime.TotalSeconds }, true));
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("조합 측정 Player 빌드 실패");
            }
            finally
            {
                PlayerSettings.productName = originalName;
                PlayerSettings.SetScriptingBackend(target, backend);
                // 메모리의 dirty 설정을 먼저 저장하지 않으면 Editor 종료가 복구 바이트를 다시 덮어쓴다.
                AssetDatabase.SaveAssets();
                File.WriteAllBytes(settings, originalBytes);
            }
        }
        [Serializable] private sealed class Summary
        { public string result; public int errors, warnings; public ulong bytes; public double seconds; }
    }
}
#endif
