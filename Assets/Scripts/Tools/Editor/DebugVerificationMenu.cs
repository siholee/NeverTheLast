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
        SessionState.SetBool(Pending, false);
        // GameManager.Start()의 StartRun/DeleteSave보다 먼저 저장을 격리한다.
        DebugMode.BeginSession();
        DebugVerification.StartSuite();
    }
}
