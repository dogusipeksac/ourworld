using System;
using System.Collections.Generic;
using OurWorld.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OurWorld.World
{
    /// <summary>
    /// Runtime Day/Night Cycle (default 1 real minute = 1 game hour, starting at 09:00).
    /// Smoothly updates sun/moon direction/colour/intensity, trilight ambient bounce, fog colour/density,
    /// skybox exposure, and URP post-processing night exposure/shadow lift at <= 10 Hz for low mobile cost,
    /// and toggles all NightLight markers between 18:30 and 06:30.
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
        private float _baseDayFogDensity = 0.0020f;
        private float _baseDayReflectionIntensity = 1.15f;

        // Cached runtime-instanced URP Global Volume overrides (prevents ACES + Contrast dark crush at night!)
        private Volume _globalVolume;
        private ColorAdjustments _volColorAdj;
        private Vignette _volVignette;
        private LiftGammaGain _volLiftGammaGain;
        private Bloom _volBloom;
        private float _dayPostExposure = 0.08f;
        private float _dayContrast = 12f;
        private float _daySaturation = 8f;
        private Color _dayColorFilter = new Color(1.0f, 0.97f, 0.93f);
        private float _dayVignetteIntensity = 0.20f;
        private float _dayBloomThreshold = 1.05f;
        private float _dayBloomIntensity = 0.45f;

        public float CurrentHour => _currentHour;
        public bool IsNight => _currentHour >= 18.5f || _currentHour < 6.5f;
        public float NightFactor { get; private set; }
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

        // High-angle silver-blue Istanbul moonlight shining downward into narrow streets, courtyards, and farms
        // so every sidewalk, building facade, tree, animal, and player stays crisp and clearly readable at night
        // while golden streetlamps and warm apartment windows stand out with rich contrast against a deep indigo sky.
        private static readonly AtmosphereKey[] Keys =
        {
            new AtmosphereKey { Hour = 0.0f,  SunPitch = 46f, SunColor = new Color(0.68f, 0.78f, 1.00f), SunIntensity = 0.78f, AmbientIntensity = 1.06f, FogColor = new Color(0.13f, 0.19f, 0.32f), SkyExposure = 0.44f },
            new AtmosphereKey { Hour = 5.2f,  SunPitch = 34f, SunColor = new Color(0.72f, 0.79f, 0.98f), SunIntensity = 0.82f, AmbientIntensity = 1.06f, FogColor = new Color(0.20f, 0.25f, 0.38f), SkyExposure = 0.50f },
            new AtmosphereKey { Hour = 6.5f,  SunPitch = 16f, SunColor = new Color(1.00f, 0.74f, 0.52f), SunIntensity = 1.32f, AmbientIntensity = 1.02f, FogColor = new Color(0.66f, 0.54f, 0.50f), SkyExposure = 0.92f },
            new AtmosphereKey { Hour = 9.0f,  SunPitch = 36f, SunColor = new Color(1.00f, 0.94f, 0.84f), SunIntensity = 1.85f, AmbientIntensity = 1.15f, FogColor = new Color(0.64f, 0.72f, 0.80f), SkyExposure = 1.12f },
            new AtmosphereKey { Hour = 12.5f, SunPitch = 56f, SunColor = new Color(1.00f, 0.97f, 0.90f), SunIntensity = 2.05f, AmbientIntensity = 1.24f, FogColor = new Color(0.68f, 0.77f, 0.85f), SkyExposure = 1.20f },
            new AtmosphereKey { Hour = 16.8f, SunPitch = 28f, SunColor = new Color(1.00f, 0.86f, 0.66f), SunIntensity = 1.95f, AmbientIntensity = 1.12f, FogColor = new Color(0.62f, 0.58f, 0.58f), SkyExposure = 1.08f },
            new AtmosphereKey { Hour = 18.6f, SunPitch = 18f, SunColor = new Color(1.00f, 0.66f, 0.42f), SunIntensity = 1.25f, AmbientIntensity = 1.02f, FogColor = new Color(0.46f, 0.38f, 0.48f), SkyExposure = 0.82f },
            new AtmosphereKey { Hour = 19.6f, SunPitch = 38f, SunColor = new Color(0.70f, 0.78f, 0.98f), SunIntensity = 0.82f, AmbientIntensity = 1.06f, FogColor = new Color(0.17f, 0.23f, 0.36f), SkyExposure = 0.48f },
            new AtmosphereKey { Hour = 20.8f, SunPitch = 46f, SunColor = new Color(0.68f, 0.78f, 1.00f), SunIntensity = 0.78f, AmbientIntensity = 1.06f, FogColor = new Color(0.13f, 0.19f, 0.32f), SkyExposure = 0.44f },
            new AtmosphereKey { Hour = 24.0f, SunPitch = 46f, SunColor = new Color(0.68f, 0.78f, 1.00f), SunIntensity = 0.78f, AmbientIntensity = 1.06f, FogColor = new Color(0.13f, 0.19f, 0.32f), SkyExposure = 0.44f }
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

            _baseDayFogDensity = RenderSettings.fogDensity > 0f ? RenderSettings.fogDensity : 0.0020f;
            _baseDayReflectionIntensity = RenderSettings.reflectionIntensity > 0f ? RenderSettings.reflectionIntensity : 1.15f;
            CacheNightVolumeOverrides();

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
            if (OurWorld.UI.MainMenuController.IsMenuActive) return;

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

        private void CacheNightVolumeOverrides()
        {
            Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Exclude);
            for (int i = 0; i < volumes.Length; i++)
            {
                if (volumes[i] != null && volumes[i].isGlobal && volumes[i].sharedProfile != null)
                {
                    _globalVolume = volumes[i];
                    break;
                }
            }

            if (_globalVolume == null) return;

            // Access .profile to instantiate a safe runtime clone (never modifies the disk VolumeProfile asset)
            VolumeProfile profile = _globalVolume.profile;
            if (profile == null) return;

            if (!profile.TryGet(out _volColorAdj))
            {
                _volColorAdj = profile.Add<ColorAdjustments>(false);
            }
            if (_volColorAdj != null)
            {
                if (_volColorAdj.postExposure.overrideState) _dayPostExposure = _volColorAdj.postExposure.value;
                if (_volColorAdj.contrast.overrideState) _dayContrast = _volColorAdj.contrast.value;
                if (_volColorAdj.saturation.overrideState) _daySaturation = _volColorAdj.saturation.value;
                if (_volColorAdj.colorFilter.overrideState) _dayColorFilter = _volColorAdj.colorFilter.value;
            }

            if (!profile.TryGet(out _volVignette))
            {
                _volVignette = profile.Add<Vignette>(false);
            }
            if (_volVignette != null && _volVignette.intensity.overrideState)
            {
                _dayVignetteIntensity = _volVignette.intensity.value;
            }

            if (!profile.TryGet(out _volLiftGammaGain))
            {
                _volLiftGammaGain = profile.Add<LiftGammaGain>(false);
            }

            if (profile.TryGet(out _volBloom) && _volBloom != null)
            {
                if (_volBloom.threshold.overrideState) _dayBloomThreshold = _volBloom.threshold.value;
                if (_volBloom.intensity.overrideState) _dayBloomIntensity = _volBloom.intensity.value;
            }
        }

        private void ApplyNightVolumeGrading(float nightFactor)
        {
            if (_volColorAdj != null)
            {
                _volColorAdj.postExposure.Override(Mathf.Lerp(_dayPostExposure, 0.32f, nightFactor));
                _volColorAdj.contrast.Override(Mathf.Lerp(_dayContrast, 4f, nightFactor));
                _volColorAdj.saturation.Override(Mathf.Lerp(_daySaturation, 14f, nightFactor));
                _volColorAdj.colorFilter.Override(Color.Lerp(_dayColorFilter, new Color(0.96f, 0.98f, 1.00f), nightFactor));
            }

            if (_volVignette != null)
            {
                _volVignette.intensity.Override(Mathf.Lerp(_dayVignetteIntensity, 0.08f, nightFactor));
            }

            if (_volLiftGammaGain != null)
            {
                _volLiftGammaGain.active = true;
                // Gentle gamma midtone lift + 1% shadow floor prevents ACES black crush without milky haze
                _volLiftGammaGain.lift.Override(Vector4.Lerp(new Vector4(1f, 1f, 1f, 0f), new Vector4(1.00f, 1.01f, 1.04f, 0.010f), nightFactor));
                _volLiftGammaGain.gamma.Override(Vector4.Lerp(new Vector4(1f, 1f, 1f, 0f), new Vector4(1.01f, 1.02f, 1.04f, 0.045f), nightFactor));
            }

            if (_volBloom != null)
            {
                _volBloom.threshold.Override(Mathf.Lerp(_dayBloomThreshold, 0.88f, nightFactor));
                _volBloom.intensity.Override(Mathf.Lerp(_dayBloomIntensity, 0.62f, nightFactor));
            }
        }

        private void ApplyLightingImmediate(bool forceNightToggle)
        {
            EvaluateAtmosphere(_currentHour, out AtmosphereKey sample);

            // Compute smooth 0..1 evening/night factor (ramps in 18:15-19:30, out 05:30-06:45)
            if (_currentHour >= 19.5f || _currentHour < 5.5f)
            {
                NightFactor = 1f;
            }
            else if (_currentHour >= 18.25f)
            {
                NightFactor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(18.25f, 19.5f, _currentHour));
            }
            else if (_currentHour < 6.75f)
            {
                NightFactor = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5.5f, 6.75f, _currentHour));
            }
            else
            {
                NightFactor = 0f;
            }

            if (_sunLight != null)
            {
                float azimuth = sunAzimuthYaw + (_currentHour / 24f) * 36f;
                _sunLight.transform.rotation = Quaternion.Euler(sample.SunPitch, azimuth, 0f);
                _sunLight.color = sample.SunColor;
                _sunLight.intensity = sample.SunIntensity;
                _sunLight.shadowStrength = Mathf.Lerp(0.76f, 0.22f, NightFactor);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            float dayFactor = 1f - NightFactor;
            // Calibrated for URP Linear color space: crisp deep-indigo night ambience with clear readable surfaces
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.36f, 0.45f, 0.65f), new Color(0.62f, 0.74f, 0.88f), dayFactor) * sample.AmbientIntensity;
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.34f, 0.35f, 0.45f), new Color(0.54f, 0.53f, 0.50f), dayFactor) * sample.AmbientIntensity;
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.24f, 0.25f, 0.33f), new Color(0.28f, 0.24f, 0.20f), dayFactor) * sample.AmbientIntensity;
            RenderSettings.ambientIntensity = sample.AmbientIntensity;
            RenderSettings.fogColor = sample.FogColor;
            RenderSettings.fogDensity = Mathf.Lerp(_baseDayFogDensity, 0.00115f, NightFactor);
            RenderSettings.reflectionIntensity = Mathf.Lerp(_baseDayReflectionIntensity, 0.46f, NightFactor);

            if (_runtimeSkybox != null && _runtimeSkybox.HasProperty(ExposureId))
            {
                _runtimeSkybox.SetFloat(ExposureId, sample.SkyExposure);
            }

            ApplyNightVolumeGrading(NightFactor);

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
