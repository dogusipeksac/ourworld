using System;
using UnityEditor;
using UnityEngine;

namespace OurWorld.EditorTools
{
    /// <summary>
    /// Automatically configures every commercial-safe CC0 / Blender MCP FBX asset under
    /// Assets/GRIDROAM/Resources/OurWorldLife/ with Generic skeletal rigging and seamless
    /// looping AnimationClips (Idle, Walk, Eating, Gallop, Happy, Swim, Dabble).
    /// </summary>
    public sealed class OurWorldLifeModelImporter : AssetPostprocessor
    {
        private static readonly string[] RiggedModelNames =
        {
            "OW_Cow",
            "OW_Sheep",
            "OW_ShepherdDog",
            "OW_Chicken",
            "OW_PondDuck",
            "OW_LakeFish",
            "OW_StreetCat",
            "OW_StreetDog",
            "OW_Pigeon",
            "OW_LakeSwan",
            "OW_FarmGoat"
        };

        private bool IsOurWorldThemeTexture =>
            !string.IsNullOrEmpty(assetPath) &&
            assetPath.IndexOf("Assets/GRIDROAM/Resources/OurWorldTheme/Textures/", StringComparison.OrdinalIgnoreCase) >= 0;

        private void OnPreprocessTexture()
        {
            if (!IsOurWorldThemeTexture) return;
            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null) return;

            bool isNormal = assetPath.IndexOf("_normal.", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isFoliageAlpha = !isNormal && assetPath.IndexOf("foliage_", StringComparison.OrdinalIgnoreCase) >= 0;
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !isNormal;
            importer.wrapMode = isFoliageAlpha ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.mipmapEnabled = true;
            if (isFoliageAlpha)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.40f;
            }
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }

        private bool IsOurWorldLifeFbx =>
            !string.IsNullOrEmpty(assetPath) &&
            assetPath.IndexOf("Assets/GRIDROAM/Resources/OurWorldLife/", StringComparison.OrdinalIgnoreCase) >= 0 &&
            assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

        private bool IsRiggedAnimalFbx()
        {
            for (int i = 0; i < RiggedModelNames.Length; i++)
            {
                if (assetPath.IndexOf(RiggedModelNames[i] + ".fbx", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private void OnPreprocessModel()
        {
            if (!IsOurWorldLifeFbx) return;
            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null) return;

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.meshCompression = ModelImporterMeshCompression.Medium;

            if (IsRiggedAnimalFbx())
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!IsOurWorldLifeFbx || !IsRiggedAnimalFbx()) return;
            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null) return;

            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            ModelImporterClipAnimation[] clips = importer.clipAnimations;

            // If saved clipAnimations reference stale takeNames that no longer exist in the FBX,
            // seamlessly refresh from defaultClipAnimations so SkeletalClipPlayer NEVER loses a clip!
            bool staleTakes = clips == null || clips.Length == 0;
            if (!staleTakes && defaults != null && defaults.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    bool found = false;
                    string tn = clips[i] != null ? clips[i].takeName : null;
                    for (int d = 0; d < defaults.Length; d++)
                    {
                        if (defaults[d] != null && string.Equals(defaults[d].takeName, tn, StringComparison.Ordinal))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found) { staleTakes = true; break; }
                }
            }
            if (staleTakes) clips = defaults;
            if (clips == null || clips.Length == 0) return;

            for (int i = 0; i < clips.Length; i++)
            {
                ModelImporterClipAnimation clip = clips[i];
                if (clip == null) continue;
                // Clean Quaternius/Blender prefixes like "AnimalArmature|Walk" -> "Walk"
                if (!string.IsNullOrEmpty(clip.name) && clip.name.Contains("|"))
                {
                    int bar = clip.name.LastIndexOf('|');
                    string clean = clip.name.Substring(bar + 1);
                    if (!string.IsNullOrEmpty(clean)) clip.name = clean;
                }
                clip.loopTime = true;
                clip.loopPose = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
