using System;
using System.Diagnostics;
using System.IO;
using OurWorld.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace OurWorld.Editor
{
    /// <summary>
    /// OurWorld Store Screenshot Studio (ported from Kube 2026 StoreScreenshotStudio):
    /// Captures 6 crisp 1920x1080 landscape shots directly from the live Hamidiye Mahallesi scene
    /// into `release/store_screenshots/raw/` and optionally invokes `generate_store_posters.py`
    /// to render all Apple App Store & Google Play phone/tablet posters in TR & EN!
    /// </summary>
    public static class OurWorldStoreScreenshotStudio
    {
        private static string RawOutDir =>
            Path.Combine(Directory.GetCurrentDirectory(), "release", "store_screenshots", "raw");

        private static string PosterScriptPath =>
            Path.Combine(Directory.GetCurrentDirectory(), "release", "store_screenshots", "generate_store_posters.py");

        private struct ShotSpec
        {
            public string FileName;
            public Vector3 Position;
            public Vector3 LookAt;
            public float FieldOfView;
            public bool NightMood;
        }

        private static readonly ShotSpec[] Shots =
        {
            new ShotSpec
            {
                FileName = "shot1_fatih_saat_kulesi.png",
                Position = new Vector3(-66.0f, 3.2f, -3.2f),
                LookAt = new Vector3(-22.0f, 2.2f, 1.4f),
                FieldOfView = 56f,
                NightMood = false
            },
            new ShotSpec
            {
                FileName = "shot2_selman_driving.png",
                Position = new Vector3(-28.0f, 3.0f, -2.5f),
                LookAt = new Vector3(16.0f, 2.0f, 1.5f),
                FieldOfView = 55f,
                NightMood = false
            },
            new ShotSpec
            {
                FileName = "shot3_hz_osman_camii.png",
                Position = new Vector3(68.0f, 6.2f, -15.0f),
                LookAt = new Vector3(82.0f, 6.6f, -43.5f),
                FieldOfView = 52f,
                NightMood = false
            },
            new ShotSpec
            {
                FileName = "shot4_petrol_aziziye_avenues.png",
                Position = new Vector3(-26.0f, 9.2f, -24.0f),
                LookAt = new Vector3(-14.0f, 1.2f, -44.0f),
                FieldOfView = 54f,
                NightMood = false
            },
            new ShotSpec
            {
                FileName = "shot5_street_football_life.png",
                Position = new Vector3(28.0f, 8.8f, -24.0f),
                LookAt = new Vector3(44.0f, 1.2f, -44.0f),
                FieldOfView = 55f,
                NightMood = false
            },
            new ShotSpec
            {
                FileName = "shot6_night_istanbul_lights.png",
                Position = new Vector3(-24.0f, 4.4f, -3.5f),
                LookAt = new Vector3(22.0f, 2.4f, 1.2f),
                FieldOfView = 56f,
                NightMood = true
            }
        };

        [MenuItem("OurWorld/Store/1. Capture Raw Store Screenshots (6 Shots, 1920x1080)", priority = 30)]
        public static void CaptureAllInEditor()
        {
            int count = CaptureAllInternal();
            EditorUtility.DisplayDialog(
                "OurWorld Store Screenshot Studio",
                $"{count}/{Shots.Length} ham 1920x1080 mağaza ekran görüntüsü kaydedildi:\n{RawOutDir}\n\nŞimdi 'OurWorld -> Store -> 2. Generate App Store & Play Store Posters (TR + EN)' ile tüm mağaza afişlerini üretebilirsin.",
                "Tamam");
        }

        [MenuItem("OurWorld/Store/2. Generate App Store & Play Store Posters (TR + EN)", priority = 31)]
        public static void GeneratePostersMenu()
        {
            if (!Directory.Exists(RawOutDir) || Directory.GetFiles(RawOutDir, "shot*.png").Length < Shots.Length)
            {
                CaptureAllInternal();
            }

            bool okTr = RunPosterGenerator("tr");
            bool okEn = RunPosterGenerator("en");
            string outRoot = Path.Combine(Directory.GetCurrentDirectory(), "release", "store_screenshots");
            EditorUtility.DisplayDialog(
                "OurWorld Store Poster Studio",
                (okTr && okEn)
                    ? $"Tüm Türkçe ve İngilizce App Store + Google Play görselleri üretildi!\n\nKlasör:\n{outRoot}"
                    : "Poster üretimi sırasında uyarı/hata oluştu — detaylar için Unity Console'a bak.",
                "Tamam");
        }

        /// <summary>Headless batchmode entry point: -executeMethod OurWorld.Editor.OurWorldStoreScreenshotStudio.CaptureAllBatchmode</summary>
        public static void CaptureAllBatchmode()
        {
            try
            {
                CaptureAllInternal();
                RunPosterGenerator("tr");
                RunPosterGenerator("en");
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }

        private static int CaptureAllInternal()
        {
            Debug.Log("[OurWorldStoreScreenshotStudio] === START CAPTURE ALL (6 SHOTS) ===");

            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || active.path != OurWorldPrototypeSetup.TestScenePath)
            {
                if (File.Exists(OurWorldPrototypeSetup.TestScenePath))
                {
                    EditorSceneManager.OpenScene(OurWorldPrototypeSetup.TestScenePath, OpenSceneMode.Single);
                }
            }

            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("[OurWorldStoreScreenshotStudio] Main Camera bulunamadı!");
                return 0;
            }

            // Save original camera & sun state so the user's scene stays untouched
            Vector3 origPos = cam.transform.position;
            Quaternion origRot = cam.transform.rotation;
            float origFov = cam.fieldOfView;

            Light sun = FindSunLight();
            Quaternion origSunRot = sun != null ? sun.transform.rotation : Quaternion.identity;
            float origSunIntensity = sun != null ? sun.intensity : 1.5f;
            Color origSunColor = sun != null ? sun.color : Color.white;
            Color origAmbSky = RenderSettings.ambientSkyColor;
            Color origAmbEq = RenderSettings.ambientEquatorColor;
            Color origFogCol = RenderSettings.fogColor;
            NightLight[] nightLights = Object.FindObjectsByType<NightLight>(FindObjectsInactive.Include);
            bool[] origLightStates = new bool[nightLights.Length];
            for (int i = 0; i < nightLights.Length; i++)
            {
                Light l = nightLights[i] != null ? nightLights[i].GetComponent<Light>() : null;
                origLightStates[i] = l != null && l.enabled;
            }

            // Hide runtime HUD canvases during clean 3D beauty captures
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            bool[] origCanvasStates = new bool[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                origCanvasStates[i] = canvases[i] != null && canvases[i].enabled;
                if (canvases[i] != null && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvases[i].enabled = false;
                }
            }

            int captured = 0;
            try
            {
                Directory.CreateDirectory(RawOutDir);

                for (int i = 0; i < Shots.Length; i++)
                {
                    ShotSpec shot = Shots[i];
                    ApplyLightingMood(shot.NightMood, sun, nightLights);

                    cam.transform.position = shot.Position;
                    cam.transform.rotation = Quaternion.LookRotation((shot.LookAt - shot.Position).normalized, Vector3.up);
                    cam.fieldOfView = shot.FieldOfView;

                    SaveRender(cam, 1920, 1080, shot.FileName);
                    captured++;
                }
            }
            finally
            {
                // Restore exact original scene state
                cam.transform.position = origPos;
                cam.transform.rotation = origRot;
                cam.fieldOfView = origFov;

                if (sun != null)
                {
                    sun.transform.rotation = origSunRot;
                    sun.intensity = origSunIntensity;
                    sun.color = origSunColor;
                }
                RenderSettings.ambientSkyColor = origAmbSky;
                RenderSettings.ambientEquatorColor = origAmbEq;
                RenderSettings.fogColor = origFogCol;

                for (int i = 0; i < nightLights.Length; i++)
                {
                    Light l = nightLights[i] != null ? nightLights[i].GetComponent<Light>() : null;
                    if (l != null) l.enabled = origLightStates[i];
                }
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] != null) canvases[i].enabled = origCanvasStates[i];
                }
            }

            Debug.Log($"[OurWorldStoreScreenshotStudio] === CAPTURED {captured}/{Shots.Length} SHOTS ===");
            return captured;
        }

        private static Light FindSunLight()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    return lights[i];
                }
            }
            return null;
        }

        private static void ApplyLightingMood(bool nightMood, Light sun, NightLight[] nightLights)
        {
            if (nightMood)
            {
                if (sun != null)
                {
                    sun.transform.rotation = Quaternion.Euler(38f, -138f, 0f);
                    sun.intensity = 0.62f;
                    sun.color = new Color(0.60f, 0.72f, 0.96f);
                }
                RenderSettings.ambientSkyColor = new Color(0.32f, 0.40f, 0.56f);
                RenderSettings.ambientEquatorColor = new Color(0.28f, 0.27f, 0.34f);
                RenderSettings.fogColor = new Color(0.14f, 0.19f, 0.30f);
            }
            else
            {
                if (sun != null)
                {
                    sun.transform.rotation = Quaternion.Euler(34f, -42f, 0f);
                    sun.intensity = 1.95f;
                    sun.color = new Color(1.0f, 0.93f, 0.80f);
                }
                RenderSettings.ambientSkyColor = new Color(0.58f, 0.72f, 0.88f);
                RenderSettings.ambientEquatorColor = new Color(0.76f, 0.72f, 0.66f);
                RenderSettings.fogColor = new Color(0.78f, 0.84f, 0.90f);
            }

            for (int i = 0; i < nightLights.Length; i++)
            {
                Light l = nightLights[i] != null ? nightLights[i].GetComponent<Light>() : null;
                if (l != null) l.enabled = nightMood;
            }
        }

        private static void SaveRender(Camera cam, int width, int height, string filename)
        {
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            RenderTexture prevTarget = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;

            string path = Path.Combine(RawOutDir, filename);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"[OurWorldStoreScreenshotStudio] Saved: {path} ({width}x{height})");

            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        private static bool RunPosterGenerator(string lang)
        {
            if (!File.Exists(PosterScriptPath))
            {
                Debug.LogError($"[OurWorldStoreScreenshotStudio] Poster script bulunamadı: {PosterScriptPath}");
                return false;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("python3", $"\"{PosterScriptPath}\" --lang {lang}")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (Process proc = Process.Start(psi))
                {
                    if (proc == null) return false;
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(180000);
                    if (!string.IsNullOrWhiteSpace(stdout)) Debug.Log(stdout);
                    if (proc.ExitCode != 0)
                    {
                        Debug.LogError($"[OurWorldStoreScreenshotStudio] Poster generator ({lang}) failed (code {proc.ExitCode}):\n{stderr}");
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OurWorldStoreScreenshotStudio] python3 çalıştırılamadı: {ex.Message}");
                return false;
            }
        }
    }
}
