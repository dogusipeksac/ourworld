using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OurWorld.Editor
{
    /// <summary>
    /// Enforces mobile-ready model and texture import settings exclusively under
    /// Assets/GRIDROAM/Resources/OurWorldTrees/:
    /// - Models (TR_*.fbx): materialImportMode None, generate colliders off, imported custom
    ///   spherical normals, and LODGroup transition thresholds LOD0 35%, LOD1 10%, LOD2 1.5% then cull.
    /// - Albedo/Billboard/Grass textures: alphaIsTransparency on, mipmap on, mipMapsPreserveCoverage on
    ///   (alphaTestReferenceValue 0.5), max 1024 (billboard & grass 512), iOS & Android ASTC 6x6.
    /// - Normal map (*_Normal*): NormalMap, linear, mipmap on, max 1024, iOS & Android ASTC 5x5.
    /// </summary>
    public sealed class OurWorldTreeAssetPostprocessor : AssetPostprocessor
    {
        private const string TreesFolderPrefix = "Assets/GRIDROAM/Resources/OurWorldTrees/";

        private const float Lod0ScreenHeight = 0.35f;
        private const float Lod1ScreenHeight = 0.10f;
        private const float Lod2ScreenHeight = 0.015f;

        private bool IsTreeAsset()
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            string normalized = assetPath.Replace('\\', '/');
            return normalized.StartsWith(TreesFolderPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private void OnPreprocessModel()
        {
            if (!IsTreeAsset())
            {
                return;
            }

            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null)
            {
                return;
            }

            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!IsTreeAsset() || root == null)
            {
                return;
            }

            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup != null)
            {
                LOD[] lods = lodGroup.GetLODs();
                if (lods != null && lods.Length >= 3)
                {
                    lods[0].screenRelativeTransitionHeight = Lod0ScreenHeight;
                    lods[1].screenRelativeTransitionHeight = Lod1ScreenHeight;
                    lods[2].screenRelativeTransitionHeight = Lod2ScreenHeight;
                    lodGroup.SetLODs(lods);
                    lodGroup.RecalculateBounds();
                }
                return;
            }

            // Fallback: if Unity did not auto-create a LODGroup on a multi-LOD tree FBX, build it here.
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            List<Renderer> lod0 = new List<Renderer>(4);
            List<Renderer> lod1 = new List<Renderer>(4);
            List<Renderer> lod2 = new List<Renderer>(2);

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer mr = renderers[i];
                if (mr == null) continue;
                string n = mr.gameObject.name;
                if (n.EndsWith("_LOD0", StringComparison.OrdinalIgnoreCase)) lod0.Add(mr);
                else if (n.EndsWith("_LOD1", StringComparison.OrdinalIgnoreCase)) lod1.Add(mr);
                else if (n.EndsWith("_LOD2", StringComparison.OrdinalIgnoreCase)) lod2.Add(mr);
            }

            if (lod0.Count > 0 && lod1.Count > 0 && lod2.Count > 0)
            {
                lodGroup = root.AddComponent<LODGroup>();
                LOD[] lods =
                {
                    new LOD(Lod0ScreenHeight, lod0.ToArray()),
                    new LOD(Lod1ScreenHeight, lod1.ToArray()),
                    new LOD(Lod2ScreenHeight, lod2.ToArray())
                };
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
            }
        }

        private void OnPreprocessTexture()
        {
            if (!IsTreeAsset())
            {
                return;
            }

            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null)
            {
                return;
            }

            string normalized = assetPath.Replace('\\', '/');
            string fileName = Path.GetFileName(normalized);
            bool isNormal = fileName.IndexOf("_Normal", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isBillboard = fileName.IndexOf("Billboard", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isGrass = fileName.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0;

            int maxSize = (isBillboard || isGrass) ? 512 : 1024;

            if (isNormal)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = false;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.5f;
            }

            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;

            TextureImporterFormat astcFormat = isNormal
                ? TextureImporterFormat.ASTC_5x5
                : TextureImporterFormat.ASTC_6x6;

            ApplyMobileAstcOverride(importer, "iPhone", astcFormat, maxSize);
            ApplyMobileAstcOverride(importer, "Android", astcFormat, maxSize);
        }

        private static void ApplyMobileAstcOverride(
            TextureImporter importer,
            string platformName,
            TextureImporterFormat format,
            int maxSize)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
            settings.name = platformName;
            settings.overridden = true;
            settings.maxTextureSize = maxSize;
            settings.format = format;
            settings.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(settings);
        }

        [MenuItem("OurWorld/Art/Reimport Realistic Trees")]
        public static void ReimportRealisticTrees()
        {
            string folder = TreesFolderPrefix.TrimEnd('/');
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

            Debug.Log($"[OurWorldTreeAssetPostprocessor] Reimported {guids.Length} realistic tree/grass assets.");
        }
    }
}
