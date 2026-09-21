using System;
using System.IO;
using Core;
using Managers.UI.DevTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 밸런스 시뮬레이터 실행. Tools 메뉴 또는
/// <c>-executeMethod BalanceSimMenu.RunBatch -simRuns 10 -simStage 30 -simSeed 1000 -simScale 20</c>로 돌린다.
/// 배치에서는 <see cref="BalanceSim.DoneMarker"/>가 생기면 에디터를 닫는다. 결과는 Logs/BalanceSim/.
/// </summary>
[InitializeOnLoad]
public static class BalanceSimMenu
{
    private const string Pending = "NTL.BalanceSim.Pending";
    private const string Batch = "NTL.BalanceSim.Batch";
    private const string Started = "NTL.BalanceSim.Started";
    private const string Runs = "NTL.BalanceSim.Runs";
    private const string Stage = "NTL.BalanceSim.Stage";
    private const string Seed = "NTL.BalanceSim.Seed";
    private const string Scale = "NTL.BalanceSim.Scale";
    private const string LineupKey = "NTL.BalanceSim.Lineup";
    private const string FrontKey = "NTL.BalanceSim.Front";
    private const string SabahProbeKey = "NTL.BalanceSim.SabahProbe";
    private const string InvincibleKey = "NTL.BalanceSim.Invincible";

    static BalanceSimMenu() => EditorApplication.update += Poll;

    public static void RunBatch()
    {
        SessionState.SetBool(Batch, true);
        SessionState.SetInt(Runs, ArgInt("-simRuns", 5));
        SessionState.SetInt(Stage, ArgInt("-simStage", 30));
        SessionState.SetInt(Seed, ArgInt("-simSeed", 1000));
        SessionState.SetFloat(Scale, ArgInt("-simScale", 20));
        // -simLineup 3,20,83,6,81 -simFront 3,20,83 — 첫 번째가 메인. 없으면 수르트 입문 편성.
        SessionState.SetString(LineupKey, ArgString("-simLineup"));
        SessionState.SetString(FrontKey, ArgString("-simFront"));
        SessionState.SetBool(SabahProbeKey, ArgFlag("-simSabahProbe"));
        // -simInvincible — 아군 무적. 완주해야 열리는 것을 확인할 때만 쓴다(밸런스 측정용 아님).
        SessionState.SetBool(InvincibleKey, ArgFlag("-simInvincible"));
        Launch();
    }

    [MenuItem("Tools/NeverTheLast/Run Balance Sim (5 runs)")]
    public static void RunFromMenu()
    {
        SessionState.SetInt(Runs, 5);
        SessionState.SetInt(Stage, 30);
        SessionState.SetInt(Seed, 1000);
        SessionState.SetFloat(Scale, 20f);
        SessionState.SetString(LineupKey, "");
        SessionState.SetString(FrontKey, "");
        SessionState.SetBool(SabahProbeKey, false);
        SessionState.SetBool(InvincibleKey, false);
        Launch();
    }

    private static void Launch()
    {
        if (EditorApplication.isPlaying) return;
        // 자기 산출물만 지운다. 예전에는 폴더를 통째로 비워서, -logFile을 이 폴더로 잡으면
        // Unity가 자기 로그를 지우려다 IOException으로 죽었다.
        string dir = BalanceSim.ReportDir;
        if (Directory.Exists(dir))
        {
            foreach (string file in Directory.GetFiles(dir, "run_*.json")) File.Delete(file);
            foreach (string name in new[] { "summary.txt", "DONE" })
            {
                string path = Path.Combine(dir, name);
                if (File.Exists(path)) File.Delete(path);
            }
        }
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        SessionState.SetBool(Pending, true);
        SessionState.SetBool(Started, false);
        EditorApplication.isPlaying = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Begin()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        // GameManager.Start()의 저장 삭제·생성보다 먼저 저장을 격리한다.
        DebugMode.BeginSession();
        GameStartIntent.Current = GameStartIntent.Intent.DirectStart;
        SessionState.SetBool(Started, true);
        BalanceSim.Begin(
            SessionState.GetInt(Runs, 5),
            SessionState.GetInt(Stage, 30),
            SessionState.GetInt(Seed, 1000),
            SessionState.GetFloat(Scale, 20f),
            SessionState.GetString(LineupKey, ""),
            SessionState.GetString(FrontKey, ""),
            SessionState.GetBool(SabahProbeKey, false),
            SessionState.GetBool(InvincibleKey, false));
    }

    private static void Poll()
    {
        if (!SessionState.GetBool(Batch, false) || !SessionState.GetBool(Started, false)) return;
        if (!File.Exists(BalanceSim.DoneMarker)) return;
        SessionState.SetBool(Batch, false);
        SessionState.SetBool(Started, false);
        EditorApplication.Exit(0);
    }

    private static string ArgString(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
    }

    private static int ArgInt(string name, int fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int value) ? value : fallback;
    }

    private static bool ArgFlag(string name)
        => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
}
