using System.IO;
using Core;
using Managers;
using Managers.UI.DevTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Tools 메뉴 또는 -executeMethod LavoisierVerificationMenu.RunBatch로 실행.</summary>
[InitializeOnLoad]
public static class LavoisierVerificationMenu
{
    private const string Pending = "NTL.LavoisierVerification.Pending";
    private const string Batch = "NTL.LavoisierVerification.Batch";
    private const string Started = "NTL.LavoisierVerification.Started";
    private static readonly string ReportPath = Path.GetFullPath("Logs/LavoisierVerification.json");

    static LavoisierVerificationMenu() => EditorApplication.update += Poll;

    public static void RunBatch()
    {
        SessionState.SetBool(Batch, true);
        Run();
    }

    [MenuItem("Tools/NeverTheLast/Run Lavoisier Verification")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            DebugVerification.StartSuite(lavoisierOnly: true);
            return;
        }
        ImportArt();
        if (File.Exists(ReportPath)) File.Delete(ReportPath);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        SessionState.SetBool(Pending, true);
        SessionState.SetBool(Started, false);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/NeverTheLast/Import Lavoisier Art")]
    public static void ImportArt()
    {
        ConfigureArt("Assets/Resources/Sprite/Standings/Allies/LAVOISIER_STANDING.png");
        ConfigureArt("Assets/Resources/Sprite/Portraits/Allies/LAVOISIER_PORTRAIT.png");
        AssetDatabase.SaveAssets();
    }

    // 초상화는 전용 1024×1024 원화로 바뀌었다. 예전처럼 입상에서 잘라 쓰는 Multiple 영역을 두면
    // 영역이 원화 밖으로 나가 스프라이트가 통째로 사라지므로, 다른 캐릭터와 같은 Single로 둔다.
    private static void ConfigureArt(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.spritePixelsPerUnit = 100;
        importer.SaveAndReimport();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        DebugMode.BeginSession();
        GameStartIntent.Current = GameStartIntent.Intent.DirectStart;
        SessionState.SetBool(Started, true);
        DebugVerification.StartSuite(lavoisierOnly: true);
    }

    private static void Poll()
    {
        if (!SessionState.GetBool(Batch, false) || !SessionState.GetBool(Started, false) || !File.Exists(ReportPath)) return;
        var report = JsonUtility.FromJson<DebugVerification.Report>(File.ReadAllText(ReportPath));
        SessionState.SetBool(Batch, false);
        SessionState.SetBool(Started, false);
        EditorApplication.Exit(report.completed && report.failed == 0 ? 0 : 1);
    }
}
