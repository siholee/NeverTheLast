using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Core;
using Managers;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 사용자 저장과 분리된 새 여정으로 UI를 살펴보고, 같은 위치에 비교용 스크린샷을 남긴다.
/// 자동 진행이나 게임 규칙 변경은 하지 않는다.
/// </summary>
public static class UIReviewMenu
{
    private const string Pending = "NTL.UIReview.Pending";

    [MenuItem("Tools/NeverTheLast/UI Review/Start isolated journey %#F5")]
    public static void StartIsolatedJourney()
    {
        if (EditorApplication.isPlaying)
        {
            StartIsolatedJourneyInPlayMode();
            return;
        }

        // SubsystemRegistration에서 DebugMode가 한 번 초기화된 뒤, AfterSceneLoad에서 세션을 켠다.
        // 이 콜백은 MonoBehaviour.Start보다 먼저 실행되므로 GameManager의 새 런 삭제/저장도 격리된다.
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LaunchPendingReview()
    {
        if (!SessionState.GetBool(Pending, false)) return;

        DebugMode.BeginSession();
        GameStartIntent.Current = GameStartIntent.Intent.NewGame;

        if (SceneManager.GetActiveScene().name == SceneNames.Game)
        {
            SessionState.SetBool(Pending, false);
            return;
        }

        SceneManager.sceneLoaded -= FinishGameSceneLoad;
        SceneManager.sceneLoaded += FinishGameSceneLoad;
        SceneManager.LoadScene(SceneNames.Game);
    }

    private static void FinishGameSceneLoad(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneNames.Game) return;
        SceneManager.sceneLoaded -= FinishGameSceneLoad;
        SessionState.SetBool(Pending, false);
    }

    private static void StartIsolatedJourneyInPlayMode()
    {
        DebugMode.BeginSession();
        GameStartIntent.Current = GameStartIntent.Intent.NewGame;
        SceneManager.LoadScene(SceneNames.Game);
    }

    [MenuItem("Tools/NeverTheLast/UI Review/Capture current screen %&F12")]
    public static void CaptureCurrentScreen()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[UI 검토] 스크린샷은 Play Mode의 Game 화면에서 캡처할 수 있습니다.");
            return;
        }

        string directory = Path.GetFullPath(Path.Combine("Logs", "UIReview"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"screen {DateTime.Now:yyyyMMdd-HHmmss}.png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[UI 검토] 스크린샷 저장 예약: {path}");
    }

    [MenuItem("Tools/NeverTheLast/UI Review/Dump text diagnostics %#F10")]
    public static void DumpTextDiagnostics()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[UI 검토] 텍스트 진단은 Play Mode에서 실행해 주세요.");
            return;
        }

        string directory = Path.GetFullPath(Path.Combine("Logs", "UIReview"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"text diagnostic {DateTime.Now:yyyyMMdd-HHmmss}.txt");
        var output = new StringBuilder(32768);
        output.AppendLine($"NeverTheLast TMP runtime diagnostic / {DateTime.Now:O}");
        output.AppendLine($"scene={SceneManager.GetActiveScene().name} screen={Screen.width}x{Screen.height} " +
                          $"colorSpace={QualitySettings.activeColorSpace} uiVertexGamma={Shader.GetGlobalInt("_UIVertexColorAlwaysGammaSpace")}");

        TMP_Text[] all = Resources.FindObjectsOfTypeAll<TMP_Text>();
        var fonts = new HashSet<TMP_FontAsset>();
        int activeCount = 0;
        foreach (TMP_Text label in all)
        {
            if (label == null || !label.gameObject.scene.IsValid() || !label.gameObject.activeInHierarchy ||
                !label.enabled) continue;
            activeCount++;
            if (label.font != null) fonts.Add(label.font);
            AppendTextDiagnostic(output, label);
        }

        output.Insert(0, $"activeTMP={activeCount}\n");
        output.AppendLine();
        output.AppendLine("=== FONT ASSETS ===");
        foreach (TMP_FontAsset font in fonts) AppendFontDiagnostic(output, font);

        File.WriteAllText(path, output.ToString(), Encoding.UTF8);
        Debug.Log($"[UI 검토] 텍스트 진단 저장: {path}");
    }

    private static void AppendTextDiagnostic(StringBuilder output, TMP_Text label)
    {
        try
        {
            label.ForceMeshUpdate(false, false);
            Material shared = label.fontSharedMaterial;
            Material rendered = label.materialForRendering;
            output.AppendLine();
            output.AppendLine($"[{HierarchyPath(label.transform)}]");
            output.AppendLine($"type={label.GetType().Name} name={label.name} text={Escape(label.text)}");
            output.AppendLine($"color={ColorText(label.color)} alpha={label.alpha:0.###} " +
                              $"fontSize={label.fontSize:0.###} renderedSize={label.fontSize:0.###} " +
                              $"fontStyle={label.fontStyle} fontWeight={label.fontWeight}");
            output.AppendLine($"font={label.font?.name ?? "<null>"} sharedMaterial={shared?.name ?? "<null>"} " +
                              $"sharedShader={shared?.shader?.name ?? "<null>"}");
            output.AppendLine($"renderMaterial={rendered?.name ?? "<null>"} " +
                              $"renderShader={rendered?.shader?.name ?? "<null>"}");
            AppendMaterialDiagnostic(output, "shared", shared);
            if (rendered != shared) AppendMaterialDiagnostic(output, "rendered", rendered);
            output.AppendLine($"mesh={MeshColorSample(label)}");
        }
        catch (Exception exception)
        {
            output.AppendLine($"[ERROR {label.name}] {exception}");
        }
    }

    private static void AppendMaterialDiagnostic(StringBuilder output, string prefix, Material material)
    {
        if (material == null) return;
        output.Append($"{prefix}: keywords=[{string.Join(",", material.shaderKeywords)}]");
        foreach (string property in new[]
                 {
                     "_FaceColor", "_OutlineColor", "_UnderlayColor", "_FaceDilate", "_OutlineWidth",
                     "_OutlineSoftness", "_WeightNormal", "_WeightBold", "_ScaleRatioA", "_ScaleRatioB",
                     "_ScaleRatioC", "_GradientScale", "_Sharpness", "_StencilComp", "_ColorMask"
                 })
        {
            if (!material.HasProperty(property)) continue;
            if (property.EndsWith("Color", StringComparison.Ordinal))
                output.Append($" {property}={ColorText(material.GetColor(property))}");
            else
                output.Append($" {property}={material.GetFloat(property):0.###}");
        }
        output.AppendLine();
    }

    private static string MeshColorSample(TMP_Text label)
    {
        TMP_TextInfo info = label.textInfo;
        var samples = new List<string>(8);
        int vertices = 0;
        for (int m = 0; m < info.meshInfo.Length; m++)
        {
            Color32[] colors = info.meshInfo[m].colors32;
            int count = info.meshInfo[m].mesh != null ? info.meshInfo[m].mesh.vertexCount : 0;
            vertices += count;
            for (int i = 0; i < count && samples.Count < 8; i++)
                samples.Add(ColorText(colors[i]));
        }
        return $"characters={info.characterCount} vertices={vertices} vertexColors=[{string.Join(",", samples)}]";
    }

    private static void AppendFontDiagnostic(StringBuilder output, TMP_FontAsset font)
    {
        output.AppendLine();
        output.AppendLine($"font={font.name} face={font.faceInfo.familyName}/{font.faceInfo.styleName} " +
                          $"pointSize={font.faceInfo.pointSize} scale={font.faceInfo.scale:0.###}");
        output.AppendLine($"population={font.atlasPopulationMode} renderMode={font.atlasRenderMode} " +
                          $"atlas={font.atlasWidth}x{font.atlasHeight} padding={font.atlasPadding} count={font.atlasTextureCount}");
        Texture2D[] atlases = font.atlasTextures;
        for (int i = 0; atlases != null && i < atlases.Length; i++)
        {
            Texture2D atlas = atlases[i];
            output.AppendLine(atlas == null
                ? $"atlas[{i}]=<null>"
                : $"atlas[{i}]={atlas.name} {atlas.width}x{atlas.height} format={atlas.format} filter={atlas.filterMode}");
        }
        AppendMaterialDiagnostic(output, "fontMaterial", font.material);
    }

    private static string HierarchyPath(Transform transform)
    {
        var parts = new List<string>();
        while (transform != null)
        {
            parts.Add(transform.name);
            transform = transform.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string Escape(string value)
        => (value ?? "").Replace("\r", "\\r").Replace("\n", "\\n");

    private static string ColorText(Color color)
        => $"({color.r:0.###},{color.g:0.###},{color.b:0.###},{color.a:0.###})/#{ColorUtility.ToHtmlStringRGBA(color)}";

    private static string ColorText(Color32 color)
        => $"({color.r},{color.g},{color.b},{color.a})/#{ColorUtility.ToHtmlStringRGBA(color)}";

    [MenuItem("Tools/NeverTheLast/UI Review/Game View/1920 x 1080")]
    private static void SetGameView1920() => SetGameViewSize(1920, 1080);

    [MenuItem("Tools/NeverTheLast/UI Review/Game View/2340 x 1080")]
    private static void SetGameView2340() => SetGameViewSize(2340, 1080);

    [MenuItem("Tools/NeverTheLast/UI Review/Game View/1440 x 1080")]
    private static void SetGameView1440() => SetGameViewSize(1440, 1080);

    /// <summary>Unity의 공개 API에 없는 Game View 고정 해상도를 버전 차이를 방어하며 선택한다.</summary>
    private static void SetGameViewSize(int width, int height)
    {
        try
        {
            Assembly editorAssembly = typeof(Editor).Assembly;
            Type gameViewType = editorAssembly.GetType("UnityEditor.GameView", true);
            Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes", true);
            Type sizeType = editorAssembly.GetType("UnityEditor.GameViewSize", true);
            Type sizeKindType = editorAssembly.GetType("UnityEditor.GameViewSizeType", true);
            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);

            object sizes = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            object groupType = sizesType.GetProperty("currentGroupType", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(sizes);
            MethodInfo getGroup = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.Instance);
            object group = getGroup?.Invoke(sizes, new[] { groupType });
            if (group == null) throw new MissingMemberException("GameViewSizes.GetGroup");

            Type groupClass = group.GetType();
            string label = $"UI Review {width}x{height}";
            string[] displayTexts = groupClass.GetMethod("GetDisplayTexts")?.Invoke(group, null) as string[];
            int index = FindSize(displayTexts, width, height);

            if (index < 0)
            {
                object fixedResolution = Enum.Parse(sizeKindType, "FixedResolution");
                object size = Activator.CreateInstance(sizeType, fixedResolution, width, height, label);
                groupClass.GetMethod("AddCustomSize")?.Invoke(group, new[] { size });
                displayTexts = groupClass.GetMethod("GetDisplayTexts")?.Invoke(group, null) as string[];
                index = FindSize(displayTexts, width, height);
            }

            if (index < 0) throw new InvalidOperationException($"{width}x{height} Game View 크기를 찾지 못했습니다.");

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            PropertyInfo selected = gameViewType.GetProperty("selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (selected == null) throw new MissingMemberException("GameView.selectedSizeIndex");
            selected.SetValue(gameView, index);
            gameView.Repaint();
            Debug.Log($"[UI 검토] Game View: {width} x {height}");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[UI 검토] 이 Unity 버전에서는 Game View 크기를 자동 설정하지 못했습니다: {exception.Message}");
        }
    }

    private static int FindSize(string[] labels, int width, int height)
    {
        if (labels == null) return -1;
        string compact = $"{width}x{height}";
        string spaced = $"{width} x {height}";
        for (int i = 0; i < labels.Length; i++)
        {
            string text = labels[i] ?? "";
            if (text.Contains(compact) || text.Contains(spaced)) return i;
        }
        return -1;
    }
}
