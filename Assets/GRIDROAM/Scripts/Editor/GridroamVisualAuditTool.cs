using System;
using System.Diagnostics;
using System.IO;
using Gridroam.World;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Gridroam.Editor
{
    /// <summary>
    /// Deterministic Before/After visual & Low/Medium URP Editor benchmark harness for Selman Sokağı.
    /// Never modifies user gameplay logic; restores scene lighting & camera after capture.
    /// </summary>
    public static class GridroamVisualAuditTool
    {
        private const string ArtifactDir = "/Users/dogusipeksac/.gemini/antigravity/brain/2dd72233-235e-4c47-8c3c-78844f1a7f7f";
        private const string LowUrpPath = "Assets/GRIDROAM/Settings/URP_Mobile_Low.asset";
        private const string MedUrpPath = "Assets/GRIDROAM/Settings/URP_Mobile_Medium.asset";

        private static readonly Vector3 HeroBakkalCamPos = new Vector3(-2.0f, 2.10f, -2.6f);
        private static readonly Vector3 HeroBakkalLookAt = new Vector3(14.5f, 1.85f, 6.2f);

        private static readonly Vector3 HeroSpawnCamPos = new Vector3(-70.0f, 2.10f, -2.6f);
        private static readonly Vector3 HeroSpawnLookAt = new Vector3(-48.0f, 1.80f, 5.4f);

        public static void RunBeforeAuditBatchmode()
        {
            try
            {
                RunAudit("before");
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }

        public static void RunAfterAuditBatchmode()
        {
            try
            {
                // Rebuild scene cleanly with latest GridroamPrototypeSetup changes before measuring After!
                GridroamPrototypeSetup.BuildFullPrototype(showDialog: false);
                RunAudit("after");
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }

        private static void RunAudit(string phase)
        {
            Directory.CreateDirectory(ArtifactDir);
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || active.path != GridroamPrototypeSetup.TestScenePath)
            {
                EditorSceneManager.OpenScene(GridroamPrototypeSetup.TestScenePath, OpenSceneMode.Single);
            }

            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("[VisualAudit] Main Camera not found.");
                return;
            }

            Vector3 origPos = cam.transform.position;
            Quaternion origRot = cam.transform.rotation;
            float origFov = cam.fieldOfView;
            RenderPipelineAsset origRp = QualitySettings.renderPipeline;

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            bool[] origCanvas = new bool[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                origCanvas[i] = canvases[i] != null && canvases[i].enabled;
                if (canvases[i] != null && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvases[i].enabled = false;
                }
            }

            // Hide inactive-at-runtime ObjectiveBeacon sitting at (0,0,0) in Edit Mode
            Gridroam.Missions.ObjectiveBeacon[] beacons = Object.FindObjectsByType<Gridroam.Missions.ObjectiveBeacon>(FindObjectsInactive.Include);
            bool[] origBeacon = new bool[beacons.Length];
            for (int i = 0; i < beacons.Length; i++)
            {
                origBeacon[i] = beacons[i] != null && beacons[i].gameObject.activeSelf;
                if (beacons[i] != null) beacons[i].gameObject.SetActive(false);
            }

            Light sun = FindSunLight();
            NightLight[] nightLights = Object.FindObjectsByType<NightLight>(FindObjectsInactive.Include);
            Quaternion origSunRot = sun != null ? sun.transform.rotation : Quaternion.identity;
            float origSunInt = sun != null ? sun.intensity : 1.8f;
            Color origSunCol = sun != null ? sun.color : Color.white;
            Color origSky = RenderSettings.ambientSkyColor;
            Color origEq = RenderSettings.ambientEquatorColor;
            Color origFog = RenderSettings.fogColor;
            bool[] origNight = new bool[nightLights.Length];
            for (int i = 0; i < nightLights.Length; i++)
            {
                Light l = nightLights[i] != null ? nightLights[i].GetComponent<Light>() : null;
                origNight[i] = l != null && l.enabled;
            }

            UniversalRenderPipelineAsset lowUrp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(LowUrpPath);
            UniversalRenderPipelineAsset medUrp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MedUrpPath);

            try
            {
                // 1. Capture Medium-profile Day & Night Before/After views from identical locked cameras
                ApplyUrp(medUrp, cam, renderScale: 0.90f, shadowDist: 45f, postFx: true);

                ApplyLighting(false, sun, nightLights);
                // Warm up URP shader variants & reflection probe before first capture
                CaptureView(cam, HeroBakkalCamPos, HeroBakkalLookAt, 56f, 320, 180, null);
                CaptureView(cam, HeroBakkalCamPos, HeroBakkalLookAt, 56f, 1600, 900, $"audit_{phase}_bakkal_day.png");
                CaptureView(cam, HeroSpawnCamPos, HeroSpawnLookAt, 56f, 1600, 900, $"audit_{phase}_spawn_day.png");

                ApplyLighting(true, sun, nightLights);
                CaptureView(cam, HeroBakkalCamPos, HeroBakkalLookAt, 56f, 320, 180, null);
                CaptureView(cam, HeroBakkalCamPos, HeroBakkalLookAt, 56f, 1600, 900, $"audit_{phase}_bakkal_night.png");

                // 2. Benchmark short driving route down Selman Sokağı (Low vs Medium URP)
                ApplyLighting(false, sun, nightLights);
                string reportLow = BenchmarkDrivingRoute(phase, "LOW", lowUrp, cam, 0.75f, 26f, false);
                string reportMed = BenchmarkDrivingRoute(phase, "MEDIUM", medUrp, cam, 0.90f, 45f, true);

                string summary = $"{reportLow}\n{reportMed}\n";
                File.WriteAllText(Path.Combine(ArtifactDir, $"audit_{phase}_perf.txt"), summary);
                Debug.Log($"[VisualAudit] === {phase.ToUpperInvariant()} SUMMARY ===\n{summary}");
            }
            finally
            {
                cam.transform.position = origPos;
                cam.transform.rotation = origRot;
                cam.fieldOfView = origFov;
                QualitySettings.renderPipeline = origRp;
                GraphicsSettings.defaultRenderPipeline = origRp;

                if (sun != null)
                {
                    sun.transform.rotation = origSunRot;
                    sun.intensity = origSunInt;
                    sun.color = origSunCol;
                }
                RenderSettings.ambientSkyColor = origSky;
                RenderSettings.ambientEquatorColor = origEq;
                RenderSettings.fogColor = origFog;
                for (int i = 0; i < nightLights.Length; i++)
                {
                    Light l = nightLights[i] != null ? nightLights[i].GetComponent<Light>() : null;
                    if (l != null) l.enabled = origNight[i];
                }
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] != null) canvases[i].enabled = origCanvas[i];
                }
                for (int i = 0; i < beacons.Length; i++)
                {
                    if (beacons[i] != null) beacons[i].gameObject.SetActive(origBeacon[i]);
                }
            }
        }

        private static string BenchmarkDrivingRoute(
            string phase,
            string tierName,
            UniversalRenderPipelineAsset urp,
            Camera cam,
            float renderScale,
            float shadowDist,
            bool postFx)
        {
            ApplyUrp(urp, cam, renderScale, shadowDist, postFx);

            const int w = 1280;
            const int h = 720;
            const int warmup = 6;
            const int steps = 60;

            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.fieldOfView = 58f;

            using ProfilerRecorder drawCallsRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using ProfilerRecorder setPassRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using ProfilerRecorder trisRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            using ProfilerRecorder allocRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");

            // Warmup renders
            for (int i = 0; i < warmup; i++)
            {
                cam.transform.position = new Vector3(-68f, 2.35f, -1.6f);
                cam.transform.rotation = Quaternion.Euler(5f, 88f, 0f);
                cam.Render();
            }

            double totalMs = 0.0;
            double maxMs = 0.0;
            double[] samples = new double[steps];
            long sumDraws = 0;
            long sumSetPass = 0;
            long sumTris = 0;

            Stopwatch sw = new Stopwatch();
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1);
                float x = Mathf.Lerp(-68f, 82f, t);
                float z = -1.6f + Mathf.Sin(t * Mathf.PI * 2f) * 0.45f;
                cam.transform.position = new Vector3(x, 2.35f, z);
                cam.transform.rotation = Quaternion.Euler(5.5f, 86f + Mathf.Sin(t * Mathf.PI * 3f) * 4f, 0f);

                sw.Restart();
                cam.Render();
                sw.Stop();

                double ms = sw.Elapsed.TotalMilliseconds;
                samples[i] = ms;
                totalMs += ms;
                if (ms > maxMs) maxMs = ms;

                if (drawCallsRec.Valid) sumDraws += drawCallsRec.LastValue;
                if (setPassRec.Valid) sumSetPass += setPassRec.LastValue;
                if (trisRec.Valid) sumTris += trisRec.LastValue;
            }

            Array.Sort(samples);
            double p95Ms = samples[Mathf.Clamp((int)(steps * 0.95f), 0, steps - 1)];
            double avgMs = totalMs / steps;
            double allocMb = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024.0 * 1024.0);
            double resMb = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024.0 * 1024.0);
            MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
            int shadowCasters = 0;
            int activeRenderers = 0;
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null && allRenderers[i].enabled)
                {
                    activeRenderers++;
                    if (allRenderers[i].shadowCastingMode != ShadowCastingMode.Off) shadowCasters++;
                }
            }
            Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            int shadowLights = 0;
            for (int i = 0; i < allLights.Length; i++)
            {
                if (allLights[i] != null && allLights[i].enabled && allLights[i].shadows != LightShadows.None) shadowLights++;
            }

            cam.targetTexture = prevTarget;
            Object.DestroyImmediate(rt);

            return $"[{phase.ToUpperInvariant()}][{tierName}] EditorDriveRoute(60f,1280x720): Avg={avgMs:0.00}ms | P95={p95Ms:0.00}ms | Max={maxMs:0.00}ms | Alloc={allocMb:0.0}MB | Res={resMb:0.0}MB | Renderers={activeRenderers} | ShadowCasters={shadowCasters} | ShadowLights={shadowLights}";
        }

        private static void ApplyUrp(UniversalRenderPipelineAsset urp, Camera cam, float renderScale, float shadowDist, bool postFx)
        {
            if (urp != null)
            {
                QualitySettings.renderPipeline = urp;
                GraphicsSettings.defaultRenderPipeline = urp;
                urp.renderScale = renderScale;
                urp.shadowDistance = shadowDist;
            }
            UniversalAdditionalCameraData uacd = cam.GetUniversalAdditionalCameraData();
            if (uacd != null) uacd.renderPostProcessing = postFx;
        }

        private static void ApplyLighting(bool night, Light sun, NightLight[] nightLights)
        {
            if (night)
            {
                if (sun != null)
                {
                    sun.transform.rotation = Quaternion.Euler(12f, -138f, 0f);
                    sun.intensity = 0.35f;
                    sun.color = new Color(0.50f, 0.60f, 0.88f);
                }
                RenderSettings.ambientSkyColor = new Color(0.11f, 0.15f, 0.26f);
                RenderSettings.ambientEquatorColor = new Color(0.20f, 0.19f, 0.28f);
                RenderSettings.fogColor = new Color(0.10f, 0.13f, 0.22f);
            }
            else
            {
                if (sun != null)
                {
                    sun.transform.rotation = Quaternion.Euler(36f, -38f, 0f);
                    sun.intensity = 1.90f;
                    sun.color = new Color(1.0f, 0.94f, 0.84f);
                }
                RenderSettings.ambientSkyColor = new Color(0.56f, 0.70f, 0.86f);
                RenderSettings.ambientEquatorColor = new Color(0.74f, 0.71f, 0.66f);
                RenderSettings.fogColor = new Color(0.76f, 0.82f, 0.88f);
            }

            for (int i = 0; i < nightLights.Length; i++)
            {
                Light l = nightLights[i] != null ? nightLights[i].GetComponent<Light>() : null;
                if (l != null) l.enabled = night;
            }
        }

        private static Light FindSunLight()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional) return lights[i];
            }
            return null;
        }

        private static void CaptureView(Camera cam, Vector3 pos, Vector3 lookAt, float fov, int w, int h, string fileName)
        {
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation((lookAt - pos).normalized, Vector3.up);
            cam.fieldOfView = fov;

            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);

            RenderTexture prevTarget = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;

            if (!string.IsNullOrEmpty(fileName))
            {
                string path = Path.Combine(ArtifactDir, fileName);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[VisualAudit] Saved {path}");
            }

            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
