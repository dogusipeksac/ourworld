using System;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace OurWorld.Core
{
    /// <summary>
    /// Tracks rolling FPS, min FPS, frame time, allocated/reserved memory, and device details
    /// for Android & iOS 30 FPS Garage-Harbor performance validation.
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        public static PerformanceMonitor Instance { get; private set; }

        [SerializeField] private float sampleWindowSeconds = 0.5f;

        public float CurrentFps { get; private set; }
        public float MinFps { get; private set; } = float.MaxValue;
        public float AverageFrameTimeMs { get; private set; }
        public float AllocatedMemoryMb { get; private set; }
        public float ReservedMemoryMb { get; private set; }

        private int _frameCount;
        private float _elapsed;
        private float _sessionTime;
        private int _totalFrames;
        private float _worstFrameDelta;

        public event Action<string> OnTelemetryUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0.00001f)
            {
                return;
            }

            _frameCount++;
            _totalFrames++;
            _elapsed += dt;
            _sessionTime += dt;
            if (dt > _worstFrameDelta)
            {
                _worstFrameDelta = dt;
            }

            if (_elapsed >= sampleWindowSeconds)
            {
                CurrentFps = _frameCount / _elapsed;
                AverageFrameTimeMs = (_elapsed / _frameCount) * 1000f;
                if (_sessionTime > 2f && CurrentFps < MinFps)
                {
                    MinFps = CurrentFps;
                }

                AllocatedMemoryMb = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
                ReservedMemoryMb = Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f);

                _frameCount = 0;
                _elapsed = 0f;

                OnTelemetryUpdated?.Invoke(BuildCompactOverlayText());
            }
        }

        public string BuildCompactOverlayText()
        {
            string tier = GraphicsProfileManager.Instance != null
                ? GraphicsProfileManager.Instance.CurrentTier.ToString().ToUpperInvariant()
                : "DEF";
            float minDisplay = MinFps < float.MaxValue ? MinFps : CurrentFps;
            return $"{CurrentFps:0.0} FPS (min {minDisplay:0.0}) | {AverageFrameTimeMs:0.0} ms | {AllocatedMemoryMb:0} MB | {tier}";
        }

        public string BuildBenchmarkReport()
        {
            float avgSessionFps = _sessionTime > 0.1f ? _totalFrames / _sessionTime : CurrentFps;
            string tier = GraphicsProfileManager.Instance != null
                ? GraphicsProfileManager.Instance.CurrentTier.ToString()
                : "Default";
            float minDisplay = MinFps < float.MaxValue ? MinFps : CurrentFps;
            Rect safe = Screen.safeArea;
            return $"[OurWorld Perf Report] Platform={Application.platform} | Device={SystemInfo.deviceModel} | " +
                   $"OS={SystemInfo.operatingSystem} | GPU={SystemInfo.graphicsDeviceName} | " +
                   $"Res={Screen.width}x{Screen.height} | SafeArea=({safe.x:0},{safe.y:0},{safe.width:0}x{safe.height:0}) | " +
                   $"Profile={tier} | SessionSec={_sessionTime:0.0} | AvgFPS={avgSessionFps:0.0} | " +
                   $"MinWindowFPS={minDisplay:0.0} | WorstFrameMs={_worstFrameDelta * 1000f:0.0} | " +
                   $"AllocMB={AllocatedMemoryMb:0.0} | ReservedMB={ReservedMemoryMb:0.0}";
        }

        public string ExportBenchmarkReport()
        {
            string report = BuildBenchmarkReport();
            Debug.Log(report);
            try
            {
                string filePath = Path.Combine(Application.persistentDataPath, "ourworld_perf_log.txt");
                string line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z {report}{Environment.NewLine}";
                File.AppendAllText(filePath, line);
                return filePath;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PerformanceMonitor] Could not write perf log: {ex.Message}");
                return string.Empty;
            }
        }

        public void ResetSessionStats()
        {
            MinFps = float.MaxValue;
            _sessionTime = 0f;
            _totalFrames = 0;
            _worstFrameDelta = 0f;
        }
    }
}
