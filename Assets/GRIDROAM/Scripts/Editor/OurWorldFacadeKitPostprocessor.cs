using System;
using UnityEditor;
using UnityEngine;

namespace OurWorld.Editor
{
    /// <summary>
    /// Automatically configures import settings for the Neighborhood Facade Detail Kit
    /// under Assets/GRIDROAM/Resources/OurWorldFacadeKit/:
    /// - FBX models: no materials, readable meshes for runtime CombineMeshes, no colliders,
    ///   no animations, imported normals.
    /// - Palette atlas textures: Bilinear filtering, mipmaps enabled, max 256px,
    ///   sRGB on for Albedo and off (linear + FromInput alpha) for MetalSmooth, ASTC 6x6 on mobile.
    /// </summary>
    public sealed class OurWorldFacadeKitPostprocessor : AssetPostprocessor
    {
        private const string KitFolderPrefix = "Assets/GRIDROAM/Resources/OurWorldFacadeKit/";

        private bool IsKitAsset()
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            string normalized = assetPath.Replace('\\', '/');
            return normalized.StartsWith(KitFolderPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private void OnPreprocessModel()
        {
            if (!IsKitAsset())
            {
                return;
            }

            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null)
            {
                return;
            }

            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = true;
            importer.addCollider = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
        }

        private void OnPreprocessTexture()
        {
            if (!IsKitAsset())
            {
                return;
            }

            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null)
            {
                return;
            }

            string normalized = assetPath.Replace('\\', '/');
            bool isMetalSmooth = normalized.IndexOf("MetalSmooth", StringComparison.OrdinalIgnoreCase) >= 0;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = !isMetalSmooth;
            importer.alphaSource = isMetalSmooth
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Compressed;

            ApplyMobileAstcOverride(importer, "iPhone");
            ApplyMobileAstcOverride(importer, "Android");
        }

        private static void ApplyMobileAstcOverride(TextureImporter importer, string platformName)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
            settings.overridden = true;
            settings.maxTextureSize = 256;
            settings.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(settings);
        }

        [MenuItem("OurWorld/Art/Reimport Facade Detail Kit")]
        public static void ReimportFacadeDetailKit()
        {
            string folder = KitFolderPrefix.TrimEnd('/');
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
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

            Debug.Log($"[OurWorldFacadeKitPostprocessor] Reimported {guids.Length} facade kit assets.");
        }
    }
}
