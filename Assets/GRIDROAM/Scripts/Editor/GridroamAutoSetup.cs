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
        private const string CurrentBuildVersion = "v26_hardened_audit_jobs_save_audio";

        static GridroamAutoSetup()
        {
            EditorApplication.delayCall += EnsurePrototypeReady;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += EnsurePrototypeReady;
            }
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

            Scene active = SceneManager.GetActiveScene();
            string builtVersion = EditorPrefs.GetString(VersionKey, string.Empty);
            bool needsRebuild = !File.Exists(GridroamPrototypeSetup.TestScenePath) || builtVersion != CurrentBuildVersion;

            if (needsRebuild)
            {
                if (active.IsValid() && active.isDirty)
                {
                    Debug.LogWarning($"[GRIDROAM] Açık sahnede ('{active.name}') kaydedilmemiş değişiklikler olduğu için {CurrentBuildVersion} otomatik kurulumu ertelendi. Sahneyi kaydettikten sonra 'GRIDROAM -> 1. Build Sultanbeyli Selman Sokağı' menüsünden yeniden oluşturabilirsin.");
                    return;
                }

                try
                {
                    Debug.Log($"[GRIDROAM] Sahne {CurrentBuildVersion} sürümüne güncelleniyor (Sultanbeyli · Selman Sokağı · Görevler 1-5 · Yan İşler · Hz. Osman Camii)...");
                    GridroamPrototypeSetup.BuildFullPrototype(showDialog: false);
                    if (File.Exists(GridroamPrototypeSetup.TestScenePath))
                    {
                        EditorPrefs.SetString(VersionKey, CurrentBuildVersion);
                        Debug.Log($"[GRIDROAM] Sahne {CurrentBuildVersion} sürümü başarıyla üretildi ve doğrulandı.");
                    }
                    else
                    {
                        Debug.LogError($"[GRIDROAM] Sahne üretiminden sonra '{GridroamPrototypeSetup.TestScenePath}' bulunamadı; sürüm anahtarı işaretlenmedi.");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[GRIDROAM] Sahne üretimi ({CurrentBuildVersion}) başarısız oldu; sürüm anahtarı işaretlenmedi.\n{ex}");
                }
                return;
            }

            if (!active.isDirty &&
                (string.IsNullOrEmpty(active.path) || active.path == "Assets/Scenes/SampleScene.unity"))
            {
                EditorSceneManager.OpenScene(GridroamPrototypeSetup.TestScenePath, OpenSceneMode.Single);
                Debug.Log("[GRIDROAM] GRIDROAM_GarageHarbor_Test.unity otomatik açıldı — Play'e basarak oynayabilirsin!");
            }
        }
    }
}
