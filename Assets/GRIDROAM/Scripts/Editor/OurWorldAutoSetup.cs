using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OurWorld.Editor
{
    /// <summary>
    /// Automatically generates/upgrades and opens GRIDROAM_GarageHarbor_Test.unity on Editor load
    /// so pressing Play launches directly into the latest realistic GRIDROAM open world.
    /// </summary>
    [InitializeOnLoad]
    public static class OurWorldAutoSetup
    {
        private const string VersionKey = "GRIDROAM_SceneBuildVersion";
        private const string CurrentBuildVersion = "v34_fix_pool_and_park_world_positions";

        static OurWorldAutoSetup()
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
            bool needsRebuild = !File.Exists(OurWorldPrototypeSetup.TestScenePath) || builtVersion != CurrentBuildVersion;

            if (needsRebuild)
            {
                if (active.IsValid() && active.isDirty)
                {
                    Debug.LogWarning($"[OurWorld] Açık sahnede ('{active.name}') kaydedilmemiş değişiklikler olduğu için {CurrentBuildVersion} otomatik kurulumu ertelendi. Sahneyi kaydettikten sonra 'OurWorld -> 1. Build Sultanbeyli Selman Sokağı' menüsünden yeniden oluşturabilirsin.");
                    return;
                }

                try
                {
                    Debug.Log($"[OurWorld] Sahne {CurrentBuildVersion} sürümüne güncelleniyor (Sultanbeyli · Selman Sokağı · Görevler 1-5 · Yan İşler · Hz. Osman Camii)...");
                    OurWorldPrototypeSetup.BuildFullPrototype(showDialog: false);
                    if (File.Exists(OurWorldPrototypeSetup.TestScenePath))
                    {
                        EditorPrefs.SetString(VersionKey, CurrentBuildVersion);
                        Debug.Log($"[OurWorld] Sahne {CurrentBuildVersion} sürümü başarıyla üretildi ve doğrulandı.");
                    }
                    else
                    {
                        Debug.LogError($"[OurWorld] Sahne üretiminden sonra '{OurWorldPrototypeSetup.TestScenePath}' bulunamadı; sürüm anahtarı işaretlenmedi.");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[OurWorld] Sahne üretimi ({CurrentBuildVersion}) başarısız oldu; sürüm anahtarı işaretlenmedi.\n{ex}");
                }
                return;
            }

            if (!active.isDirty &&
                (string.IsNullOrEmpty(active.path) || active.path == "Assets/Scenes/SampleScene.unity"))
            {
                EditorSceneManager.OpenScene(OurWorldPrototypeSetup.TestScenePath, OpenSceneMode.Single);
                Debug.Log("[OurWorld] GRIDROAM_GarageHarbor_Test.unity otomatik açıldı — Play'e basarak oynayabilirsin!");
            }
        }
    }
}
