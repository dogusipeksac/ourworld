using System;
using System.Collections.Generic;
using Gridroam.Core;
using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Runtime Day/Night Cycle (default 1 real minute = 1 game hour, starting at 09:00).
    /// Smoothly updates sun direction/colour/intensity, ambient intensity, fog colour, and skybox exposure
    /// at <= 10 Hz for low mobile cost, and toggles all NightLight markers between 20:00 and 06:00.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        private static readonly HashSet<NightLight> RegisteredLights = new HashSet<NightLight>();
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        [Header("Clock")]
        [SerializeField] private float realSecondsPerGameHour = 60f;
        [SerializeField] private float startHour = 9.0f;
        [SerializeField] private float lightingUpdateInterval = 0.12f; // ~8.3 Hz (<= 10 times/sec)
        [SerializeField] private float sunAzimuthYaw = -52f;

        private Light _sunLight;
        private Material _runtimeSkybox;
        private float _currentHour = 9.0f;
        private float _updateTimer;
        private bool _lastNightState;
        private bool _nightStateInitialized;

        public float CurrentHour => _currentHour;
        public bool IsNight => _currentHour >= 20f || _currentHour < 6f;
        public float RealSecondsPerGameHour => Mathf.Max(5f, realSecondsPerGameHour);

        public event Action<bool> OnNightStateChanged;

        private struct AtmosphereKey
        {
            public float Hour;
            public float SunPitch;
            public Color SunColor;
            public float SunIntensity;
            public float AmbientIntensity;
            public Color FogColor;
            public float SkyExposure;
        }

        private static readonly AtmosphereKey[] Keys =
        {
            new AtmosphereKey { Hour = 0.0f,  SunPitch = -35f, SunColor = new Color(0.46f, 0.58f, 0.82f), SunIntensity = 0.24f, AmbientIntensity = 0.62f, FogColor = new Color(0.09f, 0.12f, 0.20f), SkyExposure = 0.42f },
            new AtmosphereKey { Hour = 5.2f,  SunPitch = -8f,  SunColor = new Color(0.54f, 0.56f, 0.78f), SunIntensity = 0.30f, AmbientIntensity = 0.64f, FogColor = new Color(0.18f, 0.20f, 0.30f), SkyExposure = 0.48f },
            new AtmosphereKey { Hour = 6.5f,  SunPitch = 12f,  SunColor = new Color(1.00f, 0.70f, 0.48f), SunIntensity = 1.25f, AmbientIntensity = 0.90f, FogColor = new Color(0.66f, 0.54f, 0.50f), SkyExposure = 0.88f },
            new AtmosphereKey { Hour = 9.0f,  SunPitch = 36f,  SunColor = new Color(1.00f, 0.94f, 0.84f), SunIntensity = 1.85f, AmbientIntensity = 1.15f, FogColor = new Color(0.64f, 0.72f, 0.80f), SkyExposure = 1.12f },
            new AtmosphereKey { Hour = 12.5f, SunPitch = 56f,  SunColor = new Color(1.00f, 0.97f, 0.90f), SunIntensity = 2.05f, AmbientIntensity = 1.24f, FogColor = new Color(0.68f, 0.77f, 0.85f), SkyExposure = 1.20f },
            new AtmosphereKey { Hour = 16.8f, SunPitch = 28f,  SunColor = new Color(1.00f, 0.86f, 0.66f), SunIntensity = 1.95f, AmbientIntensity = 1.12f, FogColor = new Color(0.62f, 0.58f, 0.58f), SkyExposure = 1.08f },
            new AtmosphereKey { Hour = 18.8f, SunPitch = 11f,  SunColor = new Color(1.00f, 0.56f, 0.32f), SunIntensity = 1.32f, AmbientIntensity = 0.86f, FogColor = new Color(0.52f, 0.38f, 0.42f), SkyExposure = 0.84f },
            new AtmosphereKey { Hour = 20.5f, SunPitch = -14f, SunColor = new Color(0.48f, 0.58f, 0.82f), SunIntensity = 0.26f, AmbientIntensity = 0.64f, FogColor = new Color(0.11f, 0.14f, 0.22f), SkyExposure = 0.45f },
            new AtmosphereKey { Hour = 24.0f, SunPitch = -35f, SunColor = new Color(0.46f, 0.58f, 0.82f), SunIntensity = 0.24f, AmbientIntensity = 0.62f, FogColor = new Color(0.09f, 0.12f, 0.20f), SkyExposure = 0.42f }
        };

        public static void RegisterNightLight(NightLight lightMarker)
        {
            if (lightMarker != null)
            {
                RegisteredLights.Add(lightMarker);
            }
        }

        public static void UnregisterNightLight(NightLight lightMarker)
        {
            if (lightMarker != null)
            {
                RegisteredLights.Remove(lightMarker);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _currentHour = Mathf.Repeat(startHour, 24f);
        }

        private void Start()
        {
            _sunLight = RenderSettings.sun;
            if (_sunLight == null)
            {
                Light[] lights = FindObjectsByType<Light>();
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null && lights[i].type == LightType.Directional)
                    {
                        _sunLight = lights[i];
                        RenderSettings.sun = _sunLight;
                        break;
                    }
                }
            }

            if (RenderSettings.skybox != null)
            {
                _runtimeSkybox = new Material(RenderSettings.skybox);
                RenderSettings.skybox = _runtimeSkybox;
            }

            // Ensure any NightLights placed in scene before Awake are registered
            NightLight[] existing = FindObjectsByType<NightLight>(FindObjectsInactive.Include);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null)
                {
                    RegisteredLights.Add(existing[i]);
                }
            }

            if (SaveManager.Instance != null)
            {
                _currentHour = Mathf.Repeat(SaveManager.Instance.Data.timeOfDayHours, 24f);
                SaveManager.Instance.OnBeforeSave += HandleBeforeSave;
                SaveManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            }

            ApplyLightingImmediate(forceNightToggle: true);
        }

        private void OnDestroy()
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnBeforeSave -= HandleBeforeSave;
                SaveManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            }

            if (_runtimeSkybox != null)
            {
                Destroy(_runtimeSkybox);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleBeforeSave(SaveData data)
        {
            if (data != null)
            {
                data.timeOfDayHours = _currentHour;
            }
        }

        private void HandleSaveLoaded(SaveData data)
        {
            if (data != null)
            {
                SetTimeOfDay(data.timeOfDayHours);
            }
        }

        public void SetTimeOfDay(float hour)
        {
            _currentHour = Mathf.Repeat(hour, 24f);
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                SaveManager.Instance.Data.timeOfDayHours = _currentHour;
            }
            ApplyLightingImmediate(forceNightToggle: true);
        }

        public void AdvanceHours(float hours)
        {
            SetTimeOfDay(_currentHour + hours);
        }

        public string GetFormattedTime()
        {
            int totalMinutes = Mathf.FloorToInt(_currentHour * 60f) % (24 * 60);
            if (totalMinutes < 0) totalMinutes += 24 * 60;
            int hh = totalMinutes / 60;
            int mm = totalMinutes % 60;
            return $"{hh:00}:{mm:00}";
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float hoursDelta = dt / Mathf.Max(5f, realSecondsPerGameHour);
            _currentHour = Mathf.Repeat(_currentHour + hoursDelta, 24f);

            _updateTimer += dt;
            if (_updateTimer >= Mathf.Max(0.10f, lightingUpdateInterval))
            {
                _updateTimer = 0f;
                ApplyLightingImmediate(forceNightToggle: false);
            }
        }

        private void ApplyLightingImmediate(bool forceNightToggle)
        {
            EvaluateAtmosphere(_currentHour, out AtmosphereKey sample);

            if (_sunLight != null)
            {
                float azimuth = sunAzimuthYaw + (_currentHour / 24f) * 36f;
                _sunLight.transform.rotation = Quaternion.Euler(sample.SunPitch, azimuth, 0f);
                _sunLight.color = sample.SunColor;
                _sunLight.intensity = sample.SunIntensity;
                _sunLight.shadowStrength = Mathf.Lerp(0.18f, 0.78f, Mathf.InverseLerp(0.2f, 1.2f, sample.SunIntensity));
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            float dayFactor = Mathf.InverseLerp(0.24f, 1.85f, sample.SunIntensity);
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.18f, 0.24f, 0.38f), new Color(0.62f, 0.74f, 0.88f), dayFactor) * sample.AmbientIntensity;
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.16f, 0.17f, 0.22f), new Color(0.54f, 0.53f, 0.50f), dayFactor) * sample.AmbientIntensity;
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.10f, 0.09f, 0.08f), new Color(0.28f, 0.24f, 0.20f), dayFactor) * sample.AmbientIntensity;
            RenderSettings.ambientIntensity = sample.AmbientIntensity;
            RenderSettings.fogColor = sample.FogColor;

            if (_runtimeSkybox != null && _runtimeSkybox.HasProperty(ExposureId))
            {
                _runtimeSkybox.SetFloat(ExposureId, sample.SkyExposure);
            }

            bool nightNow = IsNight;
            if (!_nightStateInitialized || forceNightToggle || nightNow != _lastNightState)
            {
                _nightStateInitialized = true;
                _lastNightState = nightNow;
                ApplyAllNightLights(nightNow, forceNightToggle);
                OnNightStateChanged?.Invoke(nightNow);
            }
        }

        private static void ApplyAllNightLights(bool isNight, bool force)
        {
            foreach (NightLight nl in RegisteredLights)
            {
                if (nl != null)
                {
                    nl.ApplyNightState(isNight, force);
                }
            }
        }

        private static void EvaluateAtmosphere(float hour, out AtmosphereKey result)
        {
            for (int i = 0; i < Keys.Length - 1; i++)
            {
                AtmosphereKey a = Keys[i];
                AtmosphereKey b = Keys[i + 1];
                if (hour >= a.Hour && hour <= b.Hour)
                {
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.Hour, b.Hour, hour));
                    result = new AtmosphereKey
                    {
                        Hour = hour,
                        SunPitch = Mathf.Lerp(a.SunPitch, b.SunPitch, t),
                        SunColor = Color.Lerp(a.SunColor, b.SunColor, t),
                        SunIntensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, t),
                        AmbientIntensity = Mathf.Lerp(a.AmbientIntensity, b.AmbientIntensity, t),
                        FogColor = Color.Lerp(a.FogColor, b.FogColor, t),
                        SkyExposure = Mathf.Lerp(a.SkyExposure, b.SkyExposure, t)
                    };
                    return;
                }
            }

            result = Keys[0];
        }
    }
}
