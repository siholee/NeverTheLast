using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>메이플스토리 Light/Bold 원본으로 동적 TMP SDF 에셋을 만들고 Noto 폴백을 연결한다.</summary>
public static class MaplestoryFontAssetMenu
{
    private const string LightSourcePath = "Assets/Fonts/Maplestory Light.ttf";
    private const string BoldSourcePath = "Assets/Fonts/Maplestory Bold.ttf";
    private const string LightOutputPath = "Assets/Resources/Font/Maplestory Light SDF.asset";
    private const string BoldOutputPath = "Assets/Resources/Font/Maplestory Bold SDF.asset";
    private const string NotoFallbackPath = "Assets/Resources/Font/NotoSansKR-Regular SDF.asset";

    [MenuItem("Tools/NeverTheLast/UI Review/Generate Maplestory Font Assets %&F11")]
    public static void Generate()
    {
        AssetDatabase.ImportAsset(LightSourcePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(BoldSourcePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        TMP_FontAsset light = CreateOrLoad(LightSourcePath, LightOutputPath, "Maplestory Light SDF");
        TMP_FontAsset bold = CreateOrLoad(BoldSourcePath, BoldOutputPath, "Maplestory Bold SDF");
        if (light == null || bold == null) return;

        TMP_FontAsset noto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NotoFallbackPath);
        light.fallbackFontAssetTable = noto != null
            ? new List<TMP_FontAsset> { noto }
            : new List<TMP_FontAsset>();

        TMP_FontWeightPair[] weights = light.fontWeightTable;
        if (weights != null && weights.Length > 6)
        {
            TMP_FontWeightPair pair = weights[6]; // 700 / Bold
            pair.regularTypeface = bold;
            weights[6] = pair;
        }

        EditorUtility.SetDirty(light);
        AssetDatabase.SaveAssets();
        Debug.Log($"[UI 폰트] 메이플스토리 Light/Bold SDF 생성 및 Noto 폴백 연결 완료: {LightOutputPath}");
    }

    private static TMP_FontAsset CreateOrLoad(string sourcePath, string outputPath, string assetName)
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath);
        if (existing != null) return existing;

        Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null)
        {
            Debug.LogError($"[UI 폰트] TTF를 읽지 못했습니다: {sourcePath}");
            return null;
        }

        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 60, 5, GlyphRenderMode.SDFAA,
            4096, 4096, AtlasPopulationMode.Dynamic, true);
        if (font == null)
        {
            Debug.LogError($"[UI 폰트] TMP Font Asset 생성에 실패했습니다: {sourcePath}");
            return null;
        }

        font.name = assetName;
        AssetDatabase.CreateAsset(font, outputPath);
        if (font.atlasTextures != null)
            foreach (Texture2D atlas in font.atlasTextures)
                if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, font);
        if (font.material != null) AssetDatabase.AddObjectToAsset(font.material, font);
        return font;
    }
}
