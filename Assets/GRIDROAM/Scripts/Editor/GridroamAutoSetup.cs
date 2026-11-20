using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gridroam.Editor
{
    /// <summary>
    /// Automatically generates/upgrades and opens GRIDROAM_GarageHarbor_Test.unity on Editor load
    /// so pressing Play launches directly into the latest realistic GRIDROAM open world.
    /// </summary>
    [InitializeOnLoad]
    public static class GridroamAutoSetup
    {
        private const string VersionKey = "GRIDROAM_SceneBuildVersion";
        private const string CurrentBuildVersion = "v7_open_reachable_terrain";

        static GridroamAutoSetup()
        {
            EditorApplication.delayCall += EnsurePrototypeReady;
        }

        private static void EnsurePrototypeReady()
        {
            if (Application.isBatchMode ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            string builtVersion = EditorPrefs.GetString(VersionKey, string.Empty);
            bool needsRebuild = !File.Exists(GridroamPrototypeSetup.TestScenePath) || builtVersion != CurrentBuildVersion;

            if (needsRebuild)
            {
                EditorPrefs.SetString(VersionKey, CurrentBuildVersion);
                Debug.Log($"[GRIDROAM] Upgrading scene to {CurrentBuildVersion} (open-world side jobs, economy, day/night cycle, shops & gas station, phone map & minimap, Turkish localization, calm modern HUD)...");
                GridroamPrototypeSetup.BuildFullPrototype(showDialog: false);
                return;
            }

            Scene active = SceneManager.GetActiveScene();
            if (!active.isDirty &&
                (string.IsNullOrEmpty(active.path) || active.path == "Assets/Scenes/SampleScene.unity"))
            {
                EditorSceneManager.OpenScene(GridroamPrototypeSetup.TestScenePath, OpenSceneMode.Single);
                Debug.Log("[GRIDROAM] Opened GRIDROAM_GarageHarbor_Test.unity automatically — press Play to start!");
            }
        }
    }
}
