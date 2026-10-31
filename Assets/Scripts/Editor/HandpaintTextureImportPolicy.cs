#if UNITY_EDITOR
using UnityEditor;

/// <summary>런타임 복사본도 구매 팩의 단일 RGBA 시트 설정을 그대로 쓰게 고정한다.</summary>
public sealed class HandpaintTextureImportPolicy : AssetPostprocessor
{
    private const string Root = "Assets/Resources/VfxTex/Handpaint/";
    private const string SignatureRoot="Assets/Resources/VfxTex/Signature/";

    private void OnPreprocessTexture()
    {
        bool signature=assetPath.StartsWith(SignatureRoot,System.StringComparison.Ordinal);
        if (!signature && !assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
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
        // 단일 접점 원화는 화면상 수십 픽셀이다. 원본을 보존하되 런타임 메모리는 512px로 제한한다.
        importer.maxTextureSize = signature ? assetPath.EndsWith("_16.png",System.StringComparison.Ordinal) ? 1024 : 512 : 2048;
        if(signature)ConfigureSignature(importer,importer.maxTextureSize);
    }

    internal static void ConfigureSignature(TextureImporter importer,int maximum)
    {
            // 매트에서 알파를 얻는 가는 붓선은 낮은 품질 압축의 블록/회색 번짐을 피한다.
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.compressionQuality=100;
            var defaults=importer.GetPlatformTextureSettings("DefaultTexturePlatform");
            defaults.maxTextureSize=maximum;
            defaults.compressionQuality=100;defaults.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(defaults);
            var desktop=importer.GetPlatformTextureSettings("Standalone");
            desktop.name="Standalone";desktop.overridden=true;desktop.maxTextureSize=maximum;
            desktop.format=TextureImporterFormat.BC7;desktop.compressionQuality=100;
            desktop.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(desktop);
    }
}

/// <summary>새 연속 원화가 이전 단일 원화 임포트 캐시(512px)에 남아 있으면 해당 폴더만 갱신한다.</summary>
[InitializeOnLoad]
internal static class SignatureTextureImportBootstrap
{
    static SignatureTextureImportBootstrap()
    {
        EditorApplication.delayCall+=Ensure;
        EditorApplication.update+=Ensure;
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.update+=Ensure;};
    }
    private static void Ensure()
    {
        const string root="Assets/Resources/VfxTex/Signature";
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        EditorApplication.update-=Ensure;
        if(!AssetDatabase.IsValidFolder(root))return;
        string directory=System.IO.Path.Combine(UnityEngine.Application.dataPath,"Resources/VfxTex/Signature");
        // 새 Sprite의 타입 검색 인덱스가 아직 준비되지 않은 초기 리로드에서도 파일 경로로 확인한다.
        foreach(string file in System.IO.Directory.EnumerateFiles(directory,"*.png"))
        {
            string path=root+"/"+System.IO.Path.GetFileName(file);
            if(AssetImporter.GetAtPath(path) is not TextureImporter importer)continue;
            int expected=path.EndsWith("_16.png",System.StringComparison.Ordinal)?1024:512;
            var defaults=importer.GetPlatformTextureSettings("DefaultTexturePlatform");
            var desktop=importer.GetPlatformTextureSettings("Standalone");
            if(defaults.maxTextureSize==expected&&desktop.overridden&&desktop.maxTextureSize==expected&&desktop.format==TextureImporterFormat.BC7)continue;
            HandpaintTextureImportPolicy.ConfigureSignature(importer,expected);
            importer.SaveAndReimport();
            UnityEngine.Debug.Log($"[연속 원화 임포트] {path}: {expected}px/BC7");
        }
    }
}
#endif
