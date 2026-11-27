using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OurWorld.Editor
{
    /// <summary>
    /// Enforces consistent mobile PBR texture import settings exclusively under Assets/GRIDROAM/Art/Textures/
    /// (excluding Weathering/ and RealismPBR/):
    /// - *_NormalGL* -> NormalMap, ASTC 5x5 on iOS & Android
    /// - *_Roughness*, *_AmbientOcclusion*, *_Metalness*, *_MetalSmooth* -> Linear (sRGB false), ASTC 6x6
    /// - Color / Detail textures -> sRGB true, ASTC 6x6
    /// - Ground/road/paving folders (Asphalt012, PavingStones070, Concrete034, Ground037, Grass001) -> anisoLevel 4
    /// - All matched textures -> maxTextureSize 1024, mipmapEnabled true
    /// </summary>
    public sealed class OurWorldPbrTexturePostprocessor : AssetPostprocessor
    {
        private const string TexturesPrefix = "Assets/GRIDROAM/Art/Textures/";
        private const int MaxTextureSize = 1024;

        private static readonly string[] ExcludedSubfolders =
        {
            "Weathering",
            "RealismPBR"
        };

        private static readonly string[] HighAnisoSubfolders =
        {
            "Asphalt012",
            "PavingStones070",
            "Concrete034",
            "Ground037",
            "Grass001"
        };

        private void OnPreprocessTexture()
        {
            if (string.IsNullOrEmpty(assetPath)) return;

            string normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith(TexturesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string relative = normalized.Substring(TexturesPrefix.Length);
            int slash = relative.IndexOf('/');
            string subfolder = slash > 0 ? relative.Substring(0, slash) : string.Empty;

            for (int i = 0; i < ExcludedSubfolders.Length; i++)
            {
                if (string.Equals(subfolder, ExcludedSubfolders[i], StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null) return;

            string fileName = Path.GetFileName(normalized);
            bool isNormal = fileName.IndexOf("_NormalGL", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isMetalSmooth = !isNormal && fileName.IndexOf("_MetalSmooth", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isLinearMask = !isNormal && (
                isMetalSmooth ||
                fileName.IndexOf("_Roughness", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("_AmbientOcclusion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("_Metalness", StringComparison.OrdinalIgnoreCase) >= 0);

            if (isNormal)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
            }
            else if (isLinearMask)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                if (isMetalSmooth)
                {
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.alphaIsTransparency = false;
                }
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
            }

            importer.maxTextureSize = MaxTextureSize;
            importer.mipmapEnabled = true;

            if (IsHighAnisoFolder(subfolder))
            {
                importer.anisoLevel = 4;
            }

            TextureImporterFormat astcFormat = isNormal
                ? TextureImporterFormat.ASTC_5x5
                : TextureImporterFormat.ASTC_6x6;

            ApplyPlatformOverride(importer, "iPhone", astcFormat, MaxTextureSize);
            ApplyPlatformOverride(importer, "Android", astcFormat, MaxTextureSize);
        }

        private static bool IsHighAnisoFolder(string subfolder)
        {
            if (string.IsNullOrEmpty(subfolder)) return false;
            for (int i = 0; i < HighAnisoSubfolders.Length; i++)
            {
                if (string.Equals(subfolder, HighAnisoSubfolders[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ApplyPlatformOverride(
            TextureImporter importer,
            string platformName,
            TextureImporterFormat format,
            int maxSize)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
            settings.overridden = true;
            settings.maxTextureSize = maxSize;
            settings.format = format;
            settings.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
