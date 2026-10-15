#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tools.Editor
{
    /// <summary>
    /// 외부 도구가 request.json 하나만 써도 에디터가 새 컴파일을 반영한 뒤 Play·캡처·종료까지 맡는다.
    /// 좌표 클릭은 에디터 레이아웃에 묶이므로 요청 폴더만 IPC로 사용한다.
    /// </summary>
    [InitializeOnLoad]
    public static class VerifyHarnessEditor
    {
        private const string RequestFile = "request.json";
        private const string ResultFile = "result.json";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const double PollInterval = 0.5d;
        private const string BatchExitKey = "NTL.Verify.BatchExit";
        private const string BatchReadyKey = "NTL.Verify.BatchReady";

        /// <summary>창 없이 현재 요청을 실제 Play에서 검사한 뒤 결과 코드로 종료한다(-quit 사용 안 함).</summary>
        public static void RunBatchRequest()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("배치 전용 검증 진입점입니다.");
            SessionState.SetBool(BatchExitKey, true);
            SessionState.SetBool(BatchReadyKey, false);
            // 첫 임포트의 검색 인덱스 초기화 등 에디터 delayCall이 Play 진입보다 먼저 끝나게 한다.
            EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
            {
                SessionState.SetBool(BatchReadyKey, true);
                RunPendingRequestNow();
            };
        }

        private static double _nextPoll;
        private static long _observedLength = -1;
        private static DateTime _observedWriteTimeUtc;
        private static FileSystemWatcher _requestWatcher;
        private static volatile bool _requestChanged;

        static VerifyHarnessEditor()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Application.logMessageReceived -= OnLogMessage;
            Application.logMessageReceived += OnLogMessage;
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeRequestWatcher;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeRequestWatcher;
            ConfigureRequestWatcher();
            EditorApplication.delayCall -= ResumePendingRequestAfterReload;
            EditorApplication.delayCall += ResumePendingRequestAfterReload;
        }

        /// <summary>
        /// 일부 Windows 환경에서는 도메인 리로드 직후 최소화됐던 에디터의 update가 다시 돌기 전까지
        /// FileSystemWatcher 알림과 0.5초 폴링이 모두 지연된다. delayCall 경로에서 대기 요청을 한 번
        /// 직접 재개해, 이미 받은 refresh 세션이 창 포커스와 무관하게 Play 진입까지 이어지게 한다.
        /// </summary>
        private static void ResumePendingRequestAfterReload()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall -= ResumePendingRequestAfterReload;
                EditorApplication.delayCall += ResumePendingRequestAfterReload;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            string requestPath = Path.Combine(RequestDirectory, RequestFile);
            if (!File.Exists(requestPath)) return;

            string state = SessionState.GetString(VerifyHarnessSession.StateKey, "");
            if (state == "refresh")
            {
                Debug.Log("[시각 검증] 도메인 리로드 뒤 대기 중인 요청을 재개한다.");
                StartPlayMode();
                return;
            }

            if (state == "entering" || state == "playing" || !string.IsNullOrEmpty(state))
            {
                Debug.LogWarning($"[시각 검증] 도메인 리로드 뒤 남은 '{state}' 세션을 초기화한다.");
                ClearSession();
            }

            // 이 시점에는 파일 쓰기가 끝난 요청만 존재한다. 안정화 폴링 한 차례를 대신해
            // 현재 길이와 시각을 기준으로 잡고 즉시 수락한다.
            var info = new FileInfo(requestPath);
            _observedLength = info.Length;
            _observedWriteTimeUtc = info.LastWriteTimeUtc;
            TryAcceptRequest();
        }

        [MenuItem("Tools/NeverTheLast/Run Pending Visual Verification")]
        private static void RunPendingRequestNow()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                Debug.LogWarning("[시각 검증] Play 전환·컴파일·임포트가 끝난 뒤 다시 실행해야 한다.");
                return;
            }

            string requestPath = Path.Combine(RequestDirectory, RequestFile);
            if (!File.Exists(requestPath))
            {
                Debug.LogWarning($"[시각 검증] 대기 요청이 없다: {requestPath}");
                return;
            }

            // 메뉴는 자동 감시가 중간 상태에 걸렸을 때의 수동 복구점이다. request.json은 보존하고
            // SessionState만 초기화해 동일 요청을 정상 수락 경로로 다시 통과시킨다.
            ClearSession();
            var info = new FileInfo(requestPath);
            _observedLength = info.Length;
            _observedWriteTimeUtc = info.LastWriteTimeUtc;
            Debug.Log("[시각 검증] 대기 요청을 수동 복구 경로로 다시 받는다.");
            TryAcceptRequest();
        }

        private static string RequestDirectory
        {
            get
            {
                string overridden = Environment.GetEnvironmentVariable("NTL_VERIFY_DIR");
                if (!string.IsNullOrWhiteSpace(overridden)) return Path.GetFullPath(overridden);

                string local = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NeverTheLastVerify");
                if (File.Exists(Path.Combine(local, RequestFile))) return local;

                // 일부 열린 Windows 에디터 프로세스에서는 LocalAppData의 새 파일이 같은 절대 경로로도
                // 보이지 않았다. 프로젝트 Library는 AssetDatabase 대상이 아니면서 에디터와 외부 도구가
                // 함께 볼 수 있으므로, 이곳의 요청이 있을 때만 보조 IPC 경로로 사용한다.
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string projectIpc = Path.Combine(project, "Library", "NeverTheLastVerify");
                return File.Exists(Path.Combine(projectIpc, RequestFile)) ? projectIpc : local;
            }
        }

        private static void Poll()
        {
            if (Application.isBatchMode && !SessionState.GetBool(BatchReadyKey, false)) return;
            bool requestedWake = _requestChanged;
            _requestChanged = false;
            if (!requestedWake && EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + PollInterval;

            string state = SessionState.GetString(VerifyHarnessSession.StateKey, "");
            // entering/playing은 PlayModeStateChange로만 정리된다. 에디터가 그 콜백 전에
            // 도메인 리로드되거나 강제 중단되면 Edit Mode인데도 중간 상태가 영구히 남아
            // 새 request.json을 보지 못한다. 실제 Play 전환 중이 아닐 때만 세션을 버리고
            // 요청 파일은 그대로 둬 다음 폴링에서 처음부터 다시 받는다.
            if ((state == "entering" || state == "playing") &&
                !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning($"[시각 검증] Edit Mode에 남은 오래된 '{state}' 세션을 복구한다.");
                ClearSession();
                _observedLength = -1;
                TryAcceptRequest();
                return;
            }

            // 버전 교체나 중단된 실험 코드가 남긴 알 수 없는 값도 요청 감시 전체를 막지 않게 한다.
            if (!string.IsNullOrEmpty(state) && state != "refresh" && state != "entering" && state != "playing")
            {
                Debug.LogWarning($"[시각 검증] 알 수 없는 세션 상태 '{state}'를 지우고 요청을 다시 받는다.");
                ClearSession();
                _observedLength = -1;
                TryAcceptRequest();
                return;
            }

            if (string.IsNullOrEmpty(state))
            {
                TryAcceptRequest();
                return;
            }

            if (state != "refresh" || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            StartPlayMode();
        }

        private static void TryAcceptRequest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
                return;

            string directory = RequestDirectory;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, RequestFile);
            if (!File.Exists(path))
            {
                _observedLength = -1;
                return;
            }

            var info = new FileInfo(path);
            // 쓰는 도중의 반쪽 JSON을 실패 처리하지 않도록 두 번의 폴링에서 크기와 시각이 같아야 읽는다.
            if (_observedLength != info.Length || _observedWriteTimeUtc != info.LastWriteTimeUtc)
            {
                _observedLength = info.Length;
                _observedWriteTimeUtc = info.LastWriteTimeUtc;
                return;
            }

            string json;
            VerifyRequest request;
            try
            {
                json = File.ReadAllText(path);
                request = JsonUtility.FromJson<VerifyRequest>(json);
            }
            catch
            {
                return;
            }

            bool hasObjects = request?.scenarios != null && request.scenarios.Any(item => item != null && !string.IsNullOrWhiteSpace(item.name));
            bool hasNames = request?.scenarioNames != null && request.scenarioNames.Any(item => !string.IsNullOrWhiteSpace(item));
            if (!hasObjects && !hasNames)
            {
                WriteImmediateFailure(directory, "request.json에 scenarios 또는 scenarioNames가 없다.");
                return;
            }

            string runtimeResult = Path.Combine(directory, VerifyHarnessSession.RuntimeResultFile);
            if (File.Exists(runtimeResult)) File.Delete(runtimeResult);

            string originalPlayScene = EditorSceneManagerBridge.GetPlayModeStartScenePath();
            string originalEditorScenes = EditorSceneManagerBridge.CaptureSceneSetup();
            SessionState.SetString(VerifyHarnessSession.RequestJsonKey, json);
            SessionState.SetString(VerifyHarnessSession.RequestDirectoryKey, directory);
            SessionState.SetString(VerifyHarnessSession.OriginalPlaySceneKey, originalPlayScene ?? "");
            SessionState.SetString(VerifyHarnessSession.OriginalEditorSceneSetupKey, originalEditorScenes);
            SessionState.SetString(VerifyHarnessSession.ConsoleJsonKey,
                JsonUtility.ToJson(new VerifyLogCollection()));
            SessionState.SetString(VerifyHarnessSession.EditorWarningsJsonKey,
                JsonUtility.ToJson(new VerifyStringCollection()));
            SessionState.SetString(VerifyHarnessSession.StateKey, "refresh");

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static void StartPlayMode()
        {
            var warnings = ReadWarnings();
            try
            {
                if (EditorSceneManagerBridge.HasUnsavedSceneChanges(out string dirtyScene))
                {
                    warnings.entries.Add($"저장되지 않은 씬 변경을 보호하기 위해 요청을 중단했다: {dirtyScene}");
                    SessionState.SetString(VerifyHarnessSession.EditorWarningsJsonKey, JsonUtility.ToJson(warnings));
                    FinalizeResult("저장되지 않은 씬 변경이 있어 기준 씬을 열지 않았다.");
                    return;
                }

                EditorSceneManagerBridge.OpenBaselineScene(MainMenuScenePath);
                VerifyRequest request = JsonUtility.FromJson<VerifyRequest>(
                    SessionState.GetString(VerifyHarnessSession.RequestJsonKey, "")) ?? new VerifyRequest();
                int width = request.captureWidth > 0 ? request.captureWidth : 1920;
                int height = request.captureHeight > 0 ? request.captureHeight : 1080;
                string warning = null;
                string sizeReport = Application.isBatchMode ? "배치 기술 검사: 화면·캡처 판정 제외" : GameViewResolution.TrySelect(width, height, out warning);
                if (!string.IsNullOrWhiteSpace(warning)) warnings.entries.Add(warning);
                else if (!string.IsNullOrWhiteSpace(sizeReport)) Debug.Log($"[시각 검증] Game 뷰 {sizeReport}");

                EditorSceneManagerBridge.SetPlayModeStartScene(MainMenuScenePath);
                SessionState.SetString(VerifyHarnessSession.EditorWarningsJsonKey, JsonUtility.ToJson(warnings));
                SessionState.SetString(VerifyHarnessSession.StateKey, "entering");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                warnings.entries.Add($"Play 모드 진입 실패: {exception.Message}");
                SessionState.SetString(VerifyHarnessSession.EditorWarningsJsonKey, JsonUtility.ToJson(warnings));
                FinalizeResult("Play 모드에 들어가지 못했다.");
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            string state = SessionState.GetString(VerifyHarnessSession.StateKey, "");
            if (string.IsNullOrEmpty(state)) return;

            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetString(VerifyHarnessSession.StateKey, "playing");
            }
            else if (change == PlayModeStateChange.EnteredEditMode && state is "entering" or "playing")
            {
                FinalizeResult(null);
            }
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!EditorApplication.isPlaying || SessionState.GetString(VerifyHarnessSession.StateKey, "") != "playing")
                return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;

            VerifyLogCollection logs = ReadLogs();
            logs.entries.Add(new VerifyLogRecord
            {
                type = type.ToString(),
                message = condition,
                stackTrace = stackTrace,
            });
            SessionState.SetString(VerifyHarnessSession.ConsoleJsonKey, JsonUtility.ToJson(logs));
        }

        private static void FinalizeResult(string fallbackMessage)
        {
            string directory = SessionState.GetString(VerifyHarnessSession.RequestDirectoryKey, RequestDirectory);
            Directory.CreateDirectory(directory);
            string runtimePath = Path.Combine(directory, VerifyHarnessSession.RuntimeResultFile);
            VerifyRunResult result = null;

            if (File.Exists(runtimePath))
            {
                try
                {
                    result = JsonUtility.FromJson<VerifyRunResult>(File.ReadAllText(runtimePath));
                }
                catch (Exception exception)
                {
                    fallbackMessage = $"런타임 결과를 읽지 못했다: {exception.Message}";
                }
            }

            result ??= new VerifyRunResult
            {
                success = false,
                startedAtUtc = DateTime.UtcNow.ToString("O"),
                finishedAtUtc = DateTime.UtcNow.ToString("O"),
                message = fallbackMessage ?? "Play 모드가 결과를 남기지 않고 종료됐다.",
            };

            VerifyLogCollection logs = ReadLogs();
            VerifyStringCollection warnings = ReadWarnings();
            result.consoleExceptions ??= new List<VerifyLogRecord>();
            result.consoleExceptions.AddRange(logs.entries ?? new List<VerifyLogRecord>());
            result.warnings ??= new List<string>();
            result.warnings.AddRange(warnings.entries ?? new List<string>());
            if (result.consoleExceptions.Count > 0)
            {
                result.success = false;
                result.message = $"{result.message} Play 중 Error/Exception {result.consoleExceptions.Count}건.";
            }

            result.finishedAtUtc = string.IsNullOrWhiteSpace(result.finishedAtUtc)
                ? DateTime.UtcNow.ToString("O")
                : result.finishedAtUtc;

            EditorSceneManagerBridge.RestorePlayModeStartScene(
                SessionState.GetString(VerifyHarnessSession.OriginalPlaySceneKey, ""));
            try
            {
                EditorSceneManagerBridge.RestoreSceneSetup(
                    SessionState.GetString(VerifyHarnessSession.OriginalEditorSceneSetupKey, ""));
            }
            catch (Exception exception)
            {
                result.warnings.Add($"원래 에디터 씬 구성을 복원하지 못했다: {exception.Message}");
            }

            File.WriteAllText(Path.Combine(directory, ResultFile), JsonUtility.ToJson(result, true));
            string requestPath = Path.Combine(directory, RequestFile);
            if (File.Exists(requestPath)) File.Delete(requestPath);
            if (File.Exists(runtimePath)) File.Delete(runtimePath);

            ClearSession();
            _observedLength = -1;
            AssetDatabase.Refresh();
            if (SessionState.GetBool(BatchExitKey, false)) EditorApplication.Exit(result.success ? 0 : 1);
        }

        private static VerifyLogCollection ReadLogs()
            => JsonUtility.FromJson<VerifyLogCollection>(
                   SessionState.GetString(VerifyHarnessSession.ConsoleJsonKey, ""))
               ?? new VerifyLogCollection();

        private static VerifyStringCollection ReadWarnings()
            => JsonUtility.FromJson<VerifyStringCollection>(
                   SessionState.GetString(VerifyHarnessSession.EditorWarningsJsonKey, ""))
               ?? new VerifyStringCollection();

        private static void WriteImmediateFailure(string directory, string message)
        {
            var result = new VerifyRunResult
            {
                success = false,
                startedAtUtc = DateTime.UtcNow.ToString("O"),
                finishedAtUtc = DateTime.UtcNow.ToString("O"),
                message = message,
            };
            File.WriteAllText(Path.Combine(directory, ResultFile), JsonUtility.ToJson(result, true));
            string request = Path.Combine(directory, RequestFile);
            if (File.Exists(request)) File.Delete(request);
            _observedLength = -1;
        }

        private static void ClearSession()
        {
            SessionState.EraseString(VerifyHarnessSession.StateKey);
            SessionState.EraseString(VerifyHarnessSession.RequestJsonKey);
            SessionState.EraseString(VerifyHarnessSession.RequestDirectoryKey);
            SessionState.EraseString(VerifyHarnessSession.OriginalPlaySceneKey);
            SessionState.EraseString(VerifyHarnessSession.OriginalEditorSceneSetupKey);
            SessionState.EraseString(VerifyHarnessSession.ConsoleJsonKey);
            SessionState.EraseString(VerifyHarnessSession.EditorWarningsJsonKey);
        }

        private static class EditorSceneManagerBridge
        {
            [Serializable]
            private sealed class SceneSetupSnapshot
            {
                public List<SceneSetupEntry> scenes = new();
            }

            [Serializable]
            private sealed class SceneSetupEntry
            {
                public string path;
                public bool isLoaded;
                public bool isActive;
            }

            public static string GetPlayModeStartScenePath()
                => EditorSceneManager.playModeStartScene != null
                    ? AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)
                    : "";

            public static void SetPlayModeStartScene(string path)
            {
                SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (scene == null) throw new FileNotFoundException("검증 시작 씬을 찾을 수 없다.", path);
                EditorSceneManager.playModeStartScene = scene;
            }

            public static bool HasUnsavedSceneChanges(out string sceneName)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.isDirty) continue;
                    sceneName = string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path;
                    return true;
                }

                sceneName = "";
                return false;
            }

            public static string CaptureSceneSetup()
            {
                var snapshot = new SceneSetupSnapshot();
                foreach (SceneSetup scene in EditorSceneManager.GetSceneManagerSetup())
                {
                    snapshot.scenes.Add(new SceneSetupEntry
                    {
                        path = scene.path,
                        isLoaded = scene.isLoaded,
                        isActive = scene.isActive,
                    });
                }
                return JsonUtility.ToJson(snapshot);
            }

            public static void OpenBaselineScene(string path)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    throw new FileNotFoundException("검증 기준 씬을 찾을 수 없다.", path);
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }

            public static void RestoreSceneSetup(string json)
            {
                if (string.IsNullOrWhiteSpace(json)) return;
                SceneSetupSnapshot snapshot = JsonUtility.FromJson<SceneSetupSnapshot>(json);
                if (snapshot?.scenes == null || snapshot.scenes.Count == 0) return;
                SceneSetup[] setup = snapshot.scenes
                    .Where(scene => !string.IsNullOrWhiteSpace(scene.path))
                    .Select(scene => new SceneSetup
                    {
                        path = scene.path,
                        isLoaded = scene.isLoaded,
                        isActive = scene.isActive,
                    })
                    .ToArray();
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            public static void RestorePlayModeStartScene(string path)
            {
                EditorSceneManager.playModeStartScene = string.IsNullOrWhiteSpace(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            }
        }

        private static void ConfigureRequestWatcher()
        {
            DisposeRequestWatcher();
            try
            {
                Directory.CreateDirectory(RequestDirectory);
                _requestWatcher = new FileSystemWatcher(RequestDirectory, RequestFile)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true,
                };
                _requestWatcher.Created += OnRequestFileChanged;
                _requestWatcher.Changed += OnRequestFileChanged;
                _requestWatcher.Renamed += OnRequestFileChanged;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[시각 검증] 요청 파일 감시기를 시작하지 못해 주기 폴링만 사용한다: {exception.Message}");
            }
        }

        private static void OnRequestFileChanged(object sender, FileSystemEventArgs args)
        {
            _requestChanged = true;
            try
            {
                // 백그라운드 에디터도 다음 update를 받도록 플레이어 루프를 깨운다.
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch
            {
                // 스레드 경계에서 깨우지 못해도 기존 0.5초 폴링이 요청을 처리한다.
            }
        }

        private static void DisposeRequestWatcher()
        {
            if (_requestWatcher == null) return;
            _requestWatcher.EnableRaisingEvents = false;
            _requestWatcher.Dispose();
            _requestWatcher = null;
        }

        private static class GameViewResolution
        {
            public static string TrySelect(int requestedWidth, int requestedHeight, out string warning)
            {
                warning = null;
                int targetWidth = Mathf.Clamp(requestedWidth, 640, 3840);
                int targetHeight = Mathf.Clamp(requestedHeight, 480, 2160);
                string label = $"NTL Verify {targetWidth}x{targetHeight}";
                try
                {
                    Assembly editorAssembly = typeof(EditorWindow).Assembly;
                    Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
                    Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                    object sizes = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)
                        ?.GetValue(null);
                    Type groupType = editorAssembly.GetType("UnityEditor.GameViewSizeGroupType");
                    object standalone = Enum.Parse(groupType, "Standalone");
                    object group = sizesType.GetMethod("GetGroup")?.Invoke(sizes, new[] { standalone });
                    if (group == null) throw new MissingMethodException("GameViewSizes.GetGroup");

                    Type concreteGroupType = group.GetType();
                    MethodInfo totalMethod = concreteGroupType.GetMethod("GetTotalCount");
                    MethodInfo getSizeMethod = concreteGroupType.GetMethod("GetGameViewSize");
                    int total = (int)totalMethod.Invoke(group, null);
                    int selected = -1;
                    for (int i = 0; i < total; i++)
                    {
                        object size = getSizeMethod.Invoke(group, new object[] { i });
                        int width = ReadInt(size, "width");
                        int height = ReadInt(size, "height");
                        string displayText = ReadString(size, "displayText");
                        if (width == targetWidth && height == targetHeight &&
                            (displayText == label || selected < 0)) selected = i;
                    }

                    if (selected < 0)
                    {
                        Type sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
                        Type sizeKindType = editorAssembly.GetType("UnityEditor.GameViewSizeType");
                        object fixedResolution = Enum.Parse(sizeKindType, "FixedResolution");
                        object custom = Activator.CreateInstance(sizeType, fixedResolution,
                            targetWidth, targetHeight, label);
                        concreteGroupType.GetMethod("AddCustomSize")?.Invoke(group, new[] { custom });
                        selected = (int)totalMethod.Invoke(group, null) - 1;
                    }

                    Type gameViewType = editorAssembly.GetType("UnityEditor.GameView");
                    // 이미 있는 Game 뷰만 갱신한다. MCP 검증 때문에 사용자의 창 포커스를 가져오지 않는다.
                    EditorWindow gameView = Resources.FindObjectsOfTypeAll(gameViewType).Cast<EditorWindow>().FirstOrDefault();
                    if (gameView == null) throw new InvalidOperationException("열린 Game 뷰가 없어 창을 만들지 않고 현재 크기를 유지합니다.");
                    PropertyInfo selectedProperty = gameViewType.GetProperty("selectedSizeIndex",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (selectedProperty == null) throw new MissingMemberException("GameView.selectedSizeIndex");
                    selectedProperty.SetValue(gameView, selected);
                    gameView.Repaint();
                    return $"해상도를 {targetWidth}×{targetHeight}으로 고정했다.";
                }
                catch (Exception exception)
                {
                    warning = $"Game 뷰 {targetWidth}×{targetHeight} 고정 실패 — 현재 크기로 계속한다: {exception.GetBaseException().Message}";
                    return null;
                }
            }

            private static int ReadInt(object instance, string name)
            {
                Type type = instance.GetType();
                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null) return Convert.ToInt32(property.GetValue(instance));
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return field != null ? Convert.ToInt32(field.GetValue(instance)) : 0;
            }

            private static string ReadString(object instance, string name)
            {
                Type type = instance.GetType();
                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null) return property.GetValue(instance)?.ToString() ?? "";
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return field?.GetValue(instance)?.ToString() ?? "";
            }
        }
    }
}
#endif
