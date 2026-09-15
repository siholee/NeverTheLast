using Core;
using Managers;
using Managers.UI.DevTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>현재 열려 있는 Editor의 Play Mode에서 실제 게임 코드를 검증한다.</summary>
public static class DebugVerificationMenu
{
    private const string Pending = "NTL.DebugVerification.Pending";
    private const string Integration = "NTL.DebugVerification.Integration";
    private const string Campaign = "NTL.DebugVerification.Campaign";

    [MenuItem("Tools/NeverTheLast/Run Campaign Verification %F8")]
    public static void RunCampaign()
    {
        if (EditorApplication.isPlaying)
        {
            if (!DebugMode.SuiteRunning) DebugVerification.StartSuite(true, true);
            return;
        }
        SessionState.SetBool(Campaign, true);
        RunIntegration();
    }

    [MenuItem("Tools/NeverTheLast/Run Integration Verification %F7")]
    public static void RunIntegration()
    {
        if (EditorApplication.isPlaying)
        {
            if (!DebugMode.SuiteRunning) DebugVerification.StartSuite(true);
            return;
        }
        SessionState.SetBool(Integration, true);
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/NeverTheLast/Run Debug Verification %F6")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            if (!DebugMode.SuiteRunning) DebugVerification.StartSuite();
            return;
        }
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LaunchPending()
    {
        if (!SessionState.GetBool(Pending, false)) return;

        // Edit Mode에서 단축키를 누르면 현재 열려 있던 씬으로 Play Mode가 시작된다.
        // MainMenu에는 GameManager가 없으므로 그대로 검증을 시작하면 30초 동안 기다린 뒤
        // 초기화 실패로 끝난다. 요청 플래그를 유지한 채 실제 게임 씬을 먼저 연 다음,
        // 이 콜백이 다시 불렸을 때 스위트를 시작한다.
        if (SceneManager.GetActiveScene().name != SceneNames.Game)
        {
            SceneManager.sceneLoaded -= LaunchAfterGameSceneLoaded;
            SceneManager.sceneLoaded += LaunchAfterGameSceneLoaded;
            GameStartIntent.Current = GameStartIntent.Intent.DirectStart;
            SceneManager.LoadScene(SceneNames.Game);
            return;
        }

        StartPendingSuite();
    }

    private static void LaunchAfterGameSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneNames.Game || !SessionState.GetBool(Pending, false)) return;

        SceneManager.sceneLoaded -= LaunchAfterGameSceneLoaded;
        StartPendingSuite();
    }

    private static void StartPendingSuite()
    {
        SessionState.SetBool(Pending, false);
        // GameManager.Start()의 StartRun/DeleteSave보다 먼저 저장을 격리한다.
        DebugMode.BeginSession();
        bool integration = SessionState.GetBool(Integration, false);
        SessionState.SetBool(Integration, false);
        bool campaign = SessionState.GetBool(Campaign, false);
        SessionState.SetBool(Campaign, false);
        DebugVerification.StartSuite(integration, campaign);
    }
}
