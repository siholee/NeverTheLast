#if UNITY_EDITOR
using System;
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
        // 임시 음원 포함 배포를 명시적으로 요청한 경우의 CLI 진입점.
        public static void BuildWindowsWithTemporaryVoices()
        {
            Directory.CreateDirectory(Folder);
            using (new PublicBuildAssetScope(true)) DemoBuild.BuildWindows();
        }
        static ReleaseBuildEditor() { EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (_running || EditorApplication.timeSinceStartup<_nextPoll) return;
            _nextPoll=EditorApplication.timeSinceStartup+1;
            string request=Path.Combine(Folder,"build-request.json");
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            _running=true;
            var result=new Result {startedAtUtc=DateTime.UtcNow.ToString("O"),version="0.5.0"};
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
                if(report!=null) result.temporaryAudioAssets=report.packedAssets.SelectMany(p=>p.contents).Select(c=>c.sourceAssetPath).Distinct().Count(p=>p.Contains("/Audio/Sfx/Hsr/") && !p.EndsWith(".meta",StringComparison.OrdinalIgnoreCase));
                if(result.includeTemporaryVoices && result.temporaryAudioAssets==0) throw new InvalidOperationException("임시 음원 포함 요청인데 BuildReport에 음원이 없습니다.");
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
                if(_original!=null) {File.WriteAllBytes(_catalog,_original);_original=null;}
                if(_moved) {Directory.Move(_backup,_audio);_moved=false;}
                if(_metaMoved) {File.Move(_backup+".meta",_audio+".meta");_metaMoved=false;}
                if(_legacyMoved) {File.Move(_legacyBackup,_legacy);_legacyMoved=false;}
                if(_legacyMetaMoved) {File.Move(_legacyBackup+".meta",_legacy+".meta");_legacyMetaMoved=false;}
                AssetDatabase.Refresh();
            }
        }
    }
}
#endif
