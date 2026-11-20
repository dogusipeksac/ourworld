using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gridroam.Editor
{
    /// <summary>
    /// Automatically generates and opens GRIDROAM_GarageHarbor_Test.unity on first Editor load
    /// so pressing Play in Unity launches directly into GRIDROAM (Missions 1-2) with zero manual setup.
    /// </summary>
    [InitializeOnLoad]
    public static class GridroamAutoSetup
    {
        private const string SessionKey = "GRIDROAM_AutoSetupCompleted";

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

            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);

            if (!File.Exists(GridroamPrototypeSetup.TestScenePath))
            {
                Debug.Log("[GRIDROAM] First-time setup detected. Building and opening Garage-Harbor prototype scene automatically...");
                GridroamPrototypeSetup.BuildFullPrototype(showDialog: false);
                return;
            }

            // If Unity is still on the untouched template SampleScene, switch directly to GRIDROAM's test scene.
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
