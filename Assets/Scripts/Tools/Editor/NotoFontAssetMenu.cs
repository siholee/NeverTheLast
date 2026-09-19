using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>NotoSansKR weight 400 정적 TTF로 동적 TMP SDF 에셋을 만든다.</summary>
public static class NotoFontAssetMenu
{
    private const string SourcePath = "Assets/Fonts/NotoSansKR-Regular.ttf";
    private const string OutputPath = "Assets/Resources/Font/NotoSansKR-Regular SDF.asset";

    [MenuItem("Tools/NeverTheLast/UI Review/Generate NotoSansKR Regular SDF %&F10")]
    public static void Generate()
    {
        AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport |
                                              ImportAssetOptions.ForceUpdate);

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath) != null)
        {
            Debug.Log($"[UI 폰트] Regular SDF가 이미 있습니다: {OutputPath}");
            return;
        }

        Font source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
        if (source == null)
        {
            Debug.LogError($"[UI 폰트] 정적 Regular TTF를 읽지 못했습니다: {SourcePath}");
            return;
        }

        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 60, 5, GlyphRenderMode.SDFAA,
            4096, 4096, AtlasPopulationMode.Dynamic, true);
        if (font == null)
        {
            Debug.LogError("[UI 폰트] TMP Font Asset 생성에 실패했습니다.");
            return;
        }

        font.name = "NotoSansKR-Regular SDF";
        AssetDatabase.CreateAsset(font, OutputPath);

        Texture2D[] atlases = font.atlasTextures;
        if (atlases != null)
        {
            foreach (Texture2D atlas in atlases)
                if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, font);
        }
        if (font.material != null) AssetDatabase.AddObjectToAsset(font.material, font);

        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"[UI 폰트] NotoSansKR Regular 동적 SDF 생성 완료: {OutputPath}");
    }
}
