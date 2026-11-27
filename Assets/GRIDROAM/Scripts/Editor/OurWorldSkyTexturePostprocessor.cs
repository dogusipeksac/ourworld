using UnityEditor;
using UnityEngine;

namespace OurWorld.Editor
{
    /// <summary>
    /// Enforces mobile-ready panoramic skybox import settings (2048x1024 sRGB, Repeat-U / Clamp-V,
    /// no mipmaps, Bilinear, iOS & Android ASTC 6x6) for textures under Assets/GRIDROAM/Resources/OurWorldSky/.
    /// </summary>
    public sealed class OurWorldSkyTexturePostprocessor : AssetPostprocessor
    {
        private const string SkyFolderPrefix = "Assets/GRIDROAM/Resources/OurWorldSky/";

        private void OnPreprocessTexture()
        {
            if (string.IsNullOrEmpty(assetPath)) return;

            string normalizedPath = assetPath.Replace('\\', '/');
            if (!normalizedPath.StartsWith(SkyFolderPrefix)) return;

            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;

            ApplyMobileAstcOverride(importer, "iPhone");
            ApplyMobileAstcOverride(importer, "Android");
        }

        private static void ApplyMobileAstcOverride(TextureImporter importer, string platformName)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
            settings.name = platformName;
            settings.overridden = true;
            settings.maxTextureSize = 2048;
            settings.format = TextureImporterFormat.ASTC_6x6;
            settings.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(settings);
        }

        [MenuItem("OurWorld/Sky/Reimport Sky Panoramas")]
        public static void ReimportSkyPanoramas()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/GRIDROAM/Resources/OurWorldSky" });
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"[OurWorldSkyTexturePostprocessor] Reimported {guids.Length} sky panorama texture(s).");
        }
    }
}
