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
            "OW_LakeFish"
        };

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

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }
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
