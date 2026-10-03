#if UNITY_EDITOR
using UnityEditor;

/// <summary>런타임 복사본도 구매 팩의 단일 RGBA 시트 설정을 그대로 쓰게 고정한다.</summary>
public sealed class HandpaintTextureImportPolicy : AssetPostprocessor
{
    private const string Root = "Assets/Resources/VfxTex/Handpaint/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.filterMode = UnityEngine.FilterMode.Bilinear;
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.maxTextureSize = 2048;
    }
}
#endif
