using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// 전투 카드 원화는 PC에서 BC7 고품질로 압축하고 축소 렌더용 밉 체인을 유지한다.
    /// 런타임 사본이 저알파 블록 잔재를 정리하므로 RGBA32의 큰 메모리 비용 없이 흰 머리카락 경계를 보존한다.
    /// </summary>
    public sealed class PortraitTextureImportPolicy : AssetPostprocessor
    {
        internal const string Root = "Assets/Resources/Sprite/Portraits/";
        internal const uint PolicyVersion = 1501;

        public override uint GetVersion() => PolicyVersion;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
            if (assetImporter is not TextureImporter importer) return;

            Apply(importer);
        }

        internal static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            importer.compressionQuality = 100;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 1;

            ApplyDefaultPlatform(importer);
            ApplyStandalonePlatform(importer);
        }

        private static void ApplyDefaultPlatform(TextureImporter importer)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("DefaultTexturePlatform");
            settings.name = "DefaultTexturePlatform";
            settings.overridden = false;
            settings.format = TextureImporterFormat.Automatic;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 100;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
        }

        private static void ApplyStandalonePlatform(TextureImporter importer)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("Standalone");
            settings.name = "Standalone";
            settings.overridden = true;
            settings.format = TextureImporterFormat.BC7;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 100;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
        }
    }

    /// <summary>규칙 도입 전 원화도 Edit 모드에서 한 번 강제 재임포트한다.</summary>
    [InitializeOnLoad]
    internal static class PortraitTextureImportBootstrap
    {
        private const string PreferenceKey = "NeverTheLast.PortraitImportPolicy.Version";

        static PortraitTextureImportBootstrap()
        {
            EditorApplication.delayCall += ReimportExistingPortraits;
        }

        private static void ReimportExistingPortraits()
        {
            if (EditorPrefs.GetInt(PreferenceKey, 0) >= PortraitTextureImportPolicy.PolicyVersion) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ReimportExistingPortraits;
                return;
            }

            // 재임포트가 도메인 리로드를 일으켜도 반복하지 않고, 다음 에디터 실행도 건너뛴다.
            // 정책을 바꿀 때 PolicyVersion을 올리면 기존 원화에만 다시 한 번 적용된다.
            EditorPrefs.SetInt(PreferenceKey, (int)PortraitTextureImportPolicy.PolicyVersion);
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitTextureImportPolicy.Root });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            Debug.Log($"[PortraitTextureImportPolicy] 원화 {guids.Length}개를 Standalone BC7+밉맵으로 재임포트했다.");
        }
    }
}
