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
        ConfigureArt("Assets/Resources/Sprite/Standings/Allies/LAVOISIER_STANDING.png", false);
        ConfigureArt("Assets/Resources/Sprite/Portraits/Allies/LAVOISIER_PORTRAIT.png", true);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureArt(string path, bool portrait)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = portrait ? SpriteImportMode.Multiple : SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.spritePixelsPerUnit = 100;
        if (portrait)
        {
#pragma warning disable CS0618
            // 픽셀 원본은 보존하고 Unity의 스프라이트 영역만 지정한다.
            importer.spritesheet = new[] { new SpriteMetaData
            {
                name = "LAVOISIER_PORTRAIT", rect = new Rect(230, 976, 560, 560),
                alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f),
            }};
#pragma warning restore CS0618
        }
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
