#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Tools.Editor
{
    [InitializeOnLoad]
    public static class ReleaseBuildEditor
    {
        [Serializable] private sealed class Request { public bool includeTemporaryVoices; }
        [Serializable] private sealed class Result { public bool success; public string startedAtUtc; public string finishedAtUtc; public string message; public string version; public ulong bytes; public int errors; public int warnings; public bool includeTemporaryVoices; public int temporaryAudioAssets; }
        private static bool _running;
        private static double _nextPoll;
        private static readonly string Folder=Path.GetFullPath("Library/NeverTheLastRelease");
        /// <summary>출시 출력물·버전 설정을 건드리지 않는 별도 Player QA 빌드. 기본은 개발 빌드다.</summary>
        public static BuildReport BuildDevelopmentQA(string outputPath, bool development = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play 모드를 닫은 뒤 QA Player를 빌드합니다.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("저장하지 않은 씬을 보존하기 위해 QA 빌드를 중단했습니다.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
            using (new PublicBuildAssetScope(true))
                return BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                });
        }
        // 임시 음원 포함 배포를 명시적으로 요청한 경우의 CLI 진입점.
        public static void BuildWindowsWithTemporaryVoices()
        {
            Directory.CreateDirectory(Folder);
            using (new PublicBuildAssetScope(true)) DemoBuild.BuildWindows();
            if (TemporaryAudioCount(DemoBuild.LastReport) == 0)
                throw new InvalidOperationException("임시 음원 포함 요청인데 BuildReport에 음원이 없습니다.");
        }
        private static int TemporaryAudioCount(BuildReport report) => report == null ? 0 : report.packedAssets
            .SelectMany(p=>p.contents).Select(c=>c.sourceAssetPath).Distinct()
            .Count(p=>p.Contains("/Audio/Sfx/Hsr/")&&!p.EndsWith(".meta",StringComparison.OrdinalIgnoreCase));

        /// <summary>별도 저장 식별자를 사용하는 창 없는 QA 빌드. 원래 PlayerSettings 바이트를 복구한다.</summary>
        public static void BuildBatchQA()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("배치 전용 QA 빌드입니다.");
            string output=Environment.GetEnvironmentVariable("NTL_QA_PLAYER_PATH");
            if(string.IsNullOrWhiteSpace(output))throw new InvalidOperationException("QA Player 출력 경로가 필요합니다.");
            string settings=Path.GetFullPath("ProjectSettings/ProjectSettings.asset");byte[] original=File.ReadAllBytes(settings);
            string product=PlayerSettings.productName;var result=new Result{startedAtUtc=DateTime.UtcNow.ToString("O"),version=PlayerSettings.bundleVersion,includeTemporaryVoices=true};
            try
            {
                PlayerSettings.productName="NeverTheLast_QA_20261015";
                var report=BuildDevelopmentQA(Path.GetFullPath(output));
                result.errors=report.summary.totalErrors;result.warnings=report.summary.totalWarnings;result.bytes=report.summary.totalSize;
                result.temporaryAudioAssets=TemporaryAudioCount(report);
                result.success=report.summary.result==BuildResult.Succeeded&&result.temporaryAudioAssets>0;
                result.message=result.success?"격리된 QA Player 빌드 완료":"QA Player 빌드 또는 임시 음원 검증 실패";
            }
            catch(Exception error){result.success=false;result.message=error.ToString();}
            finally
            {
                try {PlayerSettings.productName=product;AssetDatabase.SaveAssets();File.WriteAllBytes(settings,original);}
                catch(Exception error){result.success=false;result.message+="\n설정 복구 실패: "+error;}
                result.finishedAtUtc=DateTime.UtcNow.ToString("O");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)),"build-result.json"),JsonUtility.ToJson(result,true));
            }
            EditorApplication.Exit(result.success?0:1);
        }
        static ReleaseBuildEditor() { EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (_running || EditorApplication.timeSinceStartup<_nextPoll) return;
            _nextPoll=EditorApplication.timeSinceStartup+1;
            string request=Path.Combine(Folder,"build-request.json");
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            _running=true;
            var result=new Result {startedAtUtc=DateTime.UtcNow.ToString("O"),version=PlayerSettings.bundleVersion};
            try
            {
                result.includeTemporaryVoices=JsonUtility.FromJson<Request>(File.ReadAllText(request))?.includeTemporaryVoices==true;
                for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                    if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("저장하지 않은 씬이 있어 빌드를 중단했습니다.");
                PlayerSettings.bundleVersion=result.version; AssetDatabase.SaveAssets();
                using (new PublicBuildAssetScope(result.includeTemporaryVoices)) DemoBuild.BuildWindows();
                BuildReport report=DemoBuild.LastReport;
                result.success=report!=null && report.summary.result==BuildResult.Succeeded;
                if(report!=null) {result.errors=report.summary.totalErrors;result.warnings=report.summary.totalWarnings;result.bytes=report.summary.totalSize;}
                result.temporaryAudioAssets=TemporaryAudioCount(report);
                if(result.includeTemporaryVoices && result.temporaryAudioAssets==0) throw new InvalidOperationException("임시 음원 포함 요청인데 BuildReport에 음원이 없습니다.");
                if(!result.includeTemporaryVoices && result.temporaryAudioAssets>0) throw new InvalidOperationException("임시 음원 제외 요청인데 BuildReport에 음원이 남아 있습니다.");
                result.message=result.success?(result.includeTemporaryVoices?"Windows 빌드 완료 (사용자 요청: 임시 음원 포함)":"공개 Windows 빌드 완료 (HSR 임시 음원 제외)"):"BuildReport 실패";
            }
            catch(Exception e) {result.success=false;result.message=e.ToString();Debug.LogException(e);}
            finally
            {
                result.finishedAtUtc=DateTime.UtcNow.ToString("O");
                File.WriteAllText(Path.Combine(Folder,"build-result.json"),JsonUtility.ToJson(result,true));
                File.Delete(request);_running=false;
            }
        }
        private sealed class PublicBuildAssetScope : IDisposable
        {
            private readonly string _audio=Path.GetFullPath("Assets/Resources/Audio/Sfx/Hsr");
            private readonly string _backup=Path.Combine(Folder,"private-audio");
            private readonly string _catalog=Path.GetFullPath("Assets/Resources/Data/95_sfx_cues.yaml");
            private byte[] _original;
            private bool _moved, _metaMoved, _legacyMoved, _legacyMetaMoved;
            private readonly string _legacy=Path.GetFullPath("Assets/Resources/Sprite/Materials/Portrait_Yuuka.mat");
            private readonly string _legacyBackup=Path.Combine(Folder,"unused-Portrait_Yuuka.mat");
            public PublicBuildAssetScope(bool includeTemporaryVoices)
            {
                if (!_audio.StartsWith(Path.GetFullPath("Assets")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("잘못된 음원 경로");
                Directory.CreateDirectory(Folder);
                if(!includeTemporaryVoices && File.Exists(Path.Combine(Folder,"95_sfx_cues.original.yaml")))
                    throw new InvalidOperationException("이전 카탈로그 백업이 남아 있습니다. 원본을 확인·복구한 뒤 제외 빌드를 다시 시작하세요.");
                if(Directory.Exists(_backup) || File.Exists(_backup+".meta")) throw new InvalidOperationException("이전 빌드의 음원 백업이 남아 있습니다. 원본을 먼저 복구해야 합니다.");
                if(File.Exists(_legacyBackup) || File.Exists(_legacyBackup+".meta")) throw new InvalidOperationException("구형 머티리얼 백업을 먼저 복구해야 합니다.");
                try
                {
                    if(!includeTemporaryVoices)
                    {
                        _original=File.ReadAllBytes(_catalog);
                        File.WriteAllBytes(Path.Combine(Folder,"95_sfx_cues.original.yaml"),_original);
                        if(Directory.Exists(_audio)) {Directory.Move(_audio,_backup);_moved=true;}
                        if(File.Exists(_audio+".meta")) {File.Move(_audio+".meta",_backup+".meta");_metaMoved=true;}
                        string filtered=Regex.Replace(File.ReadAllText(_catalog),@"clips:\s*\[([^\]]*)\]",m=>"clips: ["+string.Join(", ",m.Groups[1].Value.Split(',').Select(c=>c.Trim()).Where(c=>c.Length>0 && !c.Trim('"','\'').StartsWith("Hsr/",StringComparison.OrdinalIgnoreCase)))+"]");
                        filtered=Regex.Replace(filtered,@"(?im)^[ \t]*-[ \t]*['"" ]?Hsr/[^\r\n]*\r?\n","");
                        File.WriteAllText(_catalog,filtered,new System.Text.UTF8Encoding(false));
                    }
                    // 현행 씬/코드에서 참조하지 않는 구형 Resources 머티리얼이 URP/Lit 전체 변형을 끌어온다.
                    // 빌드에서만 제외하며 원본과 GUID는 완료/실패 모두 복구한다.
                    if(File.Exists(_legacy)) {File.Move(_legacy,_legacyBackup);_legacyMoved=true;}
                    if(File.Exists(_legacy+".meta")) {File.Move(_legacy+".meta",_legacyBackup+".meta");_legacyMetaMoved=true;}
                    AssetDatabase.Refresh();
                    Debug.Log(includeTemporaryVoices?"[Release05] 사용자 요청에 따라 임시 음원을 포함합니다.":"[Release05] 공개 배포에서 HSR 임시 음원을 제외했습니다. 빌드 후 원본을 복구합니다.");
                }
                catch {Dispose();throw;}
            }
            public void Dispose()
            {
                var failures=new List<Exception>();
                void Restore(Action action) { try { action(); } catch(Exception error) { failures.Add(error); } }
                // 한 복구가 실패해도 나머지 원본과 GUID의 복구를 계속한다.
                if(_original!=null) Restore(()=>{File.WriteAllBytes(_catalog,_original);_original=null;File.Delete(Path.Combine(Folder,"95_sfx_cues.original.yaml"));});
                if(_moved) Restore(()=>{Directory.Move(_backup,_audio);_moved=false;});
                if(_metaMoved) Restore(()=>{File.Move(_backup+".meta",_audio+".meta");_metaMoved=false;});
                if(_legacyMoved) Restore(()=>{File.Move(_legacyBackup,_legacy);_legacyMoved=false;});
                if(_legacyMetaMoved) Restore(()=>{File.Move(_legacyBackup+".meta",_legacy+".meta");_legacyMetaMoved=false;});
                Restore(()=>AssetDatabase.Refresh());
                if(failures.Count>0) throw new AggregateException("빌드 원본 복구가 일부 실패했습니다. 보존된 Library/NeverTheLastRelease 백업을 확인하세요.",failures);
            }
        }
    }
}
#endif
