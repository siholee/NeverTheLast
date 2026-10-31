using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    /// <summary>SD는 원본 캔버스를 유지하는 Center 스프라이트다. 발 보정은 표시 리그가 맡는다.</summary>
    public sealed class SdTextureImportPolicy : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Sprite/SD/", System.StringComparison.Ordinal) ||
                assetImporter is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(.5f, .5f);
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteAlignment = (int)SpriteAlignment.Center;
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.maxTextureSize = 1024;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            importer.crunchedCompression = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            foreach (string platform in new[] { "DefaultTexturePlatform", "Standalone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform;
                settings.overridden = platform == "Standalone";
                settings.maxTextureSize = 1024;
                settings.format = settings.overridden ? TextureImporterFormat.BC7 : TextureImporterFormat.Automatic;
                settings.textureCompression = TextureImporterCompression.CompressedHQ;
                settings.compressionQuality = 100;
                settings.crunchedCompression = false;
                importer.SetPlatformTextureSettings(settings);
            }
        }
    }
}
