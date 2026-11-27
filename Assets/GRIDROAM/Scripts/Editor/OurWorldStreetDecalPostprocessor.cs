using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OurWorld.Editor
{
    /// <summary>
    /// Enforces mobile-ready texture import settings exclusively for the Street Surface Decal Atlas
    /// under Assets/GRIDROAM/Resources/OurWorldStreetDecals/:
    /// - Albedo (*_Albedo*): sRGB, alphaIsTransparency, max 1024, mipmaps on, ASTC 6x6 on iOS & Android.
    /// - Normal (*_Normal*): NormalMap, linear, max 1024, mipmaps on, ASTC 5x5 on iOS & Android.
    /// </summary>
    public sealed class OurWorldStreetDecalPostprocessor : AssetPostprocessor
    {
        private const string DecalFolderPrefix = "Assets/GRIDROAM/Resources/OurWorldStreetDecals/";
        private const int MaxTextureSize = 1024;

        private void OnPreprocessTexture()
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            string normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith(DecalFolderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null)
            {
                return;
            }

            string fileName = Path.GetFileName(normalized);
            bool isNormal = fileName.IndexOf("_Normal", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isNormal)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = false;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
            }

            importer.maxTextureSize = MaxTextureSize;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Compressed;

            TextureImporterFormat astcFormat = isNormal
                ? TextureImporterFormat.ASTC_5x5
                : TextureImporterFormat.ASTC_6x6;

            ApplyMobileAstcOverride(importer, "iPhone", astcFormat);
            ApplyMobileAstcOverride(importer, "Android", astcFormat);
        }

        private static void ApplyMobileAstcOverride(
            TextureImporter importer,
            string platformName,
            TextureImporterFormat format)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
            settings.name = platformName;
            settings.overridden = true;
            settings.maxTextureSize = MaxTextureSize;
            settings.format = format;
            settings.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(settings);
        }

        [MenuItem("OurWorld/Art/Reimport Street Decals")]
        public static void ReimportStreetDecals()
        {
            string folder = DecalFolderPrefix.TrimEnd('/');
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!string.IsNullOrEmpty(path))
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"[OurWorldStreetDecalPostprocessor] Reimported {guids.Length} street decal texture(s).");
        }
    }
}
