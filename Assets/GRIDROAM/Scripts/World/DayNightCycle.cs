using System;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
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
        private static readonly int TexAId = Shader.PropertyToID("_TexA");
        private static readonly int TexBId = Shader.PropertyToID("_TexB");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int RotationId = Shader.PropertyToID("_Rotation");
        private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
        private static readonly int HorizonStrengthId = Shader.PropertyToID("_HorizonStrength");

        private enum SkyPhase
        {
            None = -1,
            Night = 0,
            Dawn = 1,
            Day = 2,
            Sunset = 3
        }

        [Header("Clock")]
        [SerializeField] private float realSecondsPerGameHour = 60f;
        [SerializeField] private float startHour = 9.0f;
        [SerializeField] private float lightingUpdateInterval = 0.12f; // ~8.3 Hz (<= 10 times/sec)
        [SerializeField] private float sunAzimuthYaw = -52f;
        [SerializeField] private float panoramaYawOffset = 0f;

        private Light _sunLight;
        private Material _runtimeSkybox;
        private bool _usePanoramicSky;
        private Texture2D _skyDawnTex;
        private Texture2D _skyDayTex;
        private Texture2D _skySunsetTex;
        private Texture2D _skyNightTex;
        private Texture2D _lastTexA;
        private Texture2D _lastTexB;
        private float _lastBlend = -1f;
        private float _lastRotation = -999f;
        private float _lastHorizonStrength = -1f;
        private Color _lastTint = new Color(-1f, -1f, -1f, -1f);
        private Color _lastHorizonColor = new Color(-1f, -1f, -1f, -1f);
        private SkyPhase _lastSkyPhase = SkyPhase.None;
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
        private WhiteBalance _volWhiteBalance;
        private ChromaticAberration _volChromatic;
        private float _impactChromaticPulse;
        private float _dayPostExposure = 0.10f;
        private float _dayContrast = 14f;
        private float _daySaturation = 12f;
        private Color _dayColorFilter = new Color(1.0f, 0.98f, 0.95f);
        private float _dayVignetteIntensity = 0.18f;
        private float _dayBloomThreshold = 1.00f;
        private float _dayBloomIntensity = 0.48f;

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

            Shader panoShader = Resources.Load<Shader>("OurWorldSky/OurWorldSkyboxBlend");
            if (panoShader == null)
            {
                panoShader = Shader.Find("OurWorld/Skybox/PanoramicBlend");
            }
            _skyDawnTex = Resources.Load<Texture2D>("OurWorldSky/Sky_Dawn");
            _skyDayTex = Resources.Load<Texture2D>("OurWorldSky/Sky_Day");
            _skySunsetTex = Resources.Load<Texture2D>("OurWorldSky/Sky_Sunset");
            _skyNightTex = Resources.Load<Texture2D>("OurWorldSky/Sky_Night");

            if (panoShader != null &&
                _skyDawnTex != null &&
                _skyDayTex != null &&
                _skySunsetTex != null &&
                _skyNightTex != null)
            {
                _runtimeSkybox = new Material(panoShader);
                RenderSettings.skybox = _runtimeSkybox;
                _usePanoramicSky = true;
            }
            else if (RenderSettings.skybox != null)
            {
                _runtimeSkybox = new Material(RenderSettings.skybox);
                RenderSettings.skybox = _runtimeSkybox;
                _usePanoramicSky = false;
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

        public void PulseImpactChromatic(float intensity = 0.24f)
        {
            _impactChromaticPulse = Mathf.Max(_impactChromaticPulse, Mathf.Clamp(intensity, 0.05f, 0.45f));
            if (_volChromatic != null)
            {
                _volChromatic.active = true;
                _volChromatic.intensity.Override(_impactChromaticPulse);
            }
        }

        private void Update()
        {
            if (OurWorld.UI.MainMenuController.IsMenuActive) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_impactChromaticPulse > 0f)
            {
                _impactChromaticPulse = Mathf.Max(0f, _impactChromaticPulse - Time.unscaledDeltaTime * 1.15f);
                if (_volChromatic != null)
                {
                    _volChromatic.intensity.Override(_impactChromaticPulse);
                }
            }

            // Responsive per-frame lighting & vignette updates during lightning flashes or low-HP heartbeat pulses
            float lightningNow = NeighborhoodWeatherSystem.Instance != null ? NeighborhoodWeatherSystem.Instance.LightningFlash01 : 0f;
            float hpPulseNow = PlayerPhysiologyFeel.LowHealthPulse01;
            if (lightningNow > 0.01f || hpPulseNow > 0.01f)
            {
                ApplyLightingImmediate(forceNightToggle: false);
            }

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
            else
            {
                _volBloom = profile.Add<Bloom>(true);
            }

            if (!profile.TryGet(out Tonemapping tone) || tone == null)
            {
                tone = profile.Add<Tonemapping>(true);
            }
            if (tone != null)
            {
                tone.active = true;
                tone.mode.Override(TonemappingMode.ACES);
            }

            if (!profile.TryGet(out _volWhiteBalance))
            {
                _volWhiteBalance = profile.Add<WhiteBalance>(true);
            }
            if (!profile.TryGet(out _volChromatic))
            {
                _volChromatic = profile.Add<ChromaticAberration>(true);
                _volChromatic.intensity.Override(0f);
            }
        }

        private void ApplyNightVolumeGrading(float nightFactor)
        {
            float golden = IstanbulAtmosphereVFX.ComputeGoldenHourFactor(_currentHour) * (1f - nightFactor * 0.55f);
            NeighborhoodWeatherState weather = NeighborhoodWeatherSystem.Instance != null
                ? NeighborhoodWeatherSystem.Instance.CurrentWeather
                : NeighborhoodWeatherState.Clear;
            float rainMood = weather == NeighborhoodWeatherState.SummerShower ? 1f
                : (weather == NeighborhoodWeatherState.BreezyOvercast ? 0.5f : 0f);
            bool underwater = PlayerModeController.Instance != null &&
                              PlayerModeController.Instance.CurrentMode == ControlMode.OnFoot &&
                              PlayerModeController.Instance.FootController != null &&
                              PlayerModeController.Instance.FootController.IsUnderwater;
            float hpPulse = PlayerPhysiologyFeel.LowHealthPulse01;
            float fatigue = PlayerPhysiologyFeel.FatigueMood01;
            float lightning = NeighborhoodWeatherSystem.Instance != null ? NeighborhoodWeatherSystem.Instance.LightningFlash01 : 0f;

            if (_volColorAdj != null)
            {
                float dayExp = Mathf.Lerp(_dayPostExposure, _dayPostExposure - 0.08f, rainMood);
                float dayCon = Mathf.Lerp(Mathf.Max(_dayContrast, 14f), 9f, rainMood);
                float daySat = Mathf.Lerp(Mathf.Lerp(Mathf.Max(_daySaturation, 12f), 19f, golden), 4f, rainMood);
                Color dayTint = Color.Lerp(Color.Lerp(_dayColorFilter, new Color(1.00f, 0.94f, 0.85f), golden), new Color(0.92f, 0.96f, 1.00f), rainMood);

                float finalExp = Mathf.Lerp(dayExp, 0.30f, nightFactor) + lightning * 0.42f - (underwater ? 0.16f : 0f);
                float finalCon = Mathf.Lerp(dayCon, 6f, nightFactor) + (underwater ? 4f : 0f);
                float finalSat = Mathf.Lerp(daySat, 16f, nightFactor) - fatigue * 24f + (underwater ? 8f : 0f);
                Color finalTint = Color.Lerp(dayTint, new Color(0.95f, 0.97f, 1.00f), nightFactor);
                if (underwater)
                {
                    finalTint = Color.Lerp(finalTint, new Color(0.56f, 0.88f, 0.96f), 0.62f);
                }

                _volColorAdj.postExposure.Override(finalExp);
                _volColorAdj.contrast.Override(finalCon);
                _volColorAdj.saturation.Override(finalSat);
                _volColorAdj.colorFilter.Override(finalTint);
            }

            if (_volWhiteBalance != null)
            {
                _volWhiteBalance.active = true;
                float temp = Mathf.Lerp(Mathf.Lerp(5f, 20f, golden), -14f, rainMood);
                temp = Mathf.Lerp(temp, -6f, nightFactor);
                if (underwater) temp = -26f;
                _volWhiteBalance.temperature.Override(temp);
                _volWhiteBalance.tint.Override(Mathf.Lerp(2f, 6f, golden) * (1f - nightFactor));
            }

            if (_volVignette != null)
            {
                float baseVig = Mathf.Lerp(Mathf.Lerp(_dayVignetteIntensity, 0.24f, golden), 0.10f, nightFactor);
                if (underwater) baseVig = Mathf.Max(baseVig, 0.34f);
                float finalVig = Mathf.Clamp(baseVig + hpPulse * 0.26f + fatigue * 0.08f, 0f, 0.52f);
                _volVignette.intensity.Override(finalVig);
                Color vigCol = underwater
                    ? new Color(0.04f, 0.22f, 0.30f)
                    : Color.Lerp(Color.black, new Color(0.46f, 0.04f, 0.06f), Mathf.Clamp01(hpPulse * 1.15f));
                _volVignette.color.Override(vigCol);
            }

            if (_volLiftGammaGain != null)
            {
                _volLiftGammaGain.active = true;
                // Gentle warm golden-hour gain & crisp readable night shadow lift
                _volLiftGammaGain.lift.Override(Vector4.Lerp(new Vector4(1f, 1f, 1f, 0f), new Vector4(1.00f, 1.01f, 1.04f, 0.010f), nightFactor));
                _volLiftGammaGain.gamma.Override(Vector4.Lerp(new Vector4(1f, 1f, 1f, 0f), new Vector4(1.01f, 1.02f, 1.04f, 0.042f), nightFactor));
                _volLiftGammaGain.gain.Override(Vector4.Lerp(new Vector4(1f, 1f, 1f, 0f), new Vector4(1.04f, 1.01f, 0.96f, 0.02f), golden));
            }

            if (_volBloom != null)
            {
                _volBloom.active = true;
                float dayThr = Mathf.Lerp(_dayBloomThreshold, 0.88f, golden);
                float dayInt = Mathf.Lerp(Mathf.Lerp(_dayBloomIntensity, 0.74f, golden), 0.34f, rainMood);
                _volBloom.threshold.Override(Mathf.Lerp(dayThr, 0.80f, nightFactor));
                _volBloom.intensity.Override(Mathf.Lerp(dayInt, 0.78f, nightFactor));
                _volBloom.scatter.Override(Mathf.Lerp(Mathf.Lerp(0.66f, 0.74f, golden), 0.74f, nightFactor));
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

            float lightningFlash = NeighborhoodWeatherSystem.Instance != null ? NeighborhoodWeatherSystem.Instance.LightningFlash01 : 0f;
            bool isUnderwater = PlayerModeController.Instance != null &&
                                PlayerModeController.Instance.CurrentMode == ControlMode.OnFoot &&
                                PlayerModeController.Instance.FootController != null &&
                                PlayerModeController.Instance.FootController.IsUnderwater;

            if (_sunLight != null)
            {
                float azimuth = sunAzimuthYaw + (_currentHour / 24f) * 36f;
                _sunLight.transform.rotation = Quaternion.Euler(sample.SunPitch, azimuth, 0f);
                _sunLight.color = Color.Lerp(sample.SunColor, new Color(0.86f, 0.92f, 1.0f), lightningFlash * 0.72f);
                _sunLight.intensity = sample.SunIntensity + lightningFlash * 1.45f;
                _sunLight.shadowStrength = Mathf.Lerp(0.76f, 0.22f, NightFactor);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            float dayFactor = 1f - NightFactor;
            // Calibrated for URP Linear color space: crisp deep-indigo night ambience with clear readable surfaces
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.36f, 0.45f, 0.65f), new Color(0.62f, 0.74f, 0.88f), dayFactor) * sample.AmbientIntensity;
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.34f, 0.35f, 0.45f), new Color(0.54f, 0.53f, 0.50f), dayFactor) * sample.AmbientIntensity;
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.24f, 0.25f, 0.33f), new Color(0.28f, 0.24f, 0.20f), dayFactor) * sample.AmbientIntensity;
            RenderSettings.ambientIntensity = sample.AmbientIntensity + lightningFlash * 0.35f;
            float rainMistMul = NeighborhoodWeatherSystem.Instance != null &&
                                NeighborhoodWeatherSystem.Instance.CurrentWeather == NeighborhoodWeatherState.SummerShower
                ? 1.26f : 1.0f;
            if (isUnderwater)
            {
                RenderSettings.fogColor = new Color(0.08f, 0.38f, 0.48f);
                RenderSettings.fogDensity = 0.044f;
            }
            else
            {
                RenderSettings.fogColor = Color.Lerp(sample.FogColor, new Color(0.74f, 0.82f, 0.94f), lightningFlash * 0.45f);
                RenderSettings.fogDensity = Mathf.Lerp(_baseDayFogDensity, 0.00115f, NightFactor) * rainMistMul;
            }
            RenderSettings.reflectionIntensity = Mathf.Lerp(_baseDayReflectionIntensity, 0.46f, NightFactor);

            if (_runtimeSkybox != null && _runtimeSkybox.HasProperty(ExposureId))
            {
                float skyExposure = sample.SkyExposure;
                if (_usePanoramicSky)
                {
                    // Panoramic textures already bake day/night luminance; compensate sample.SkyExposure
                    // (~1.1 day -> ~1.0, ~0.44 night -> ~0.90) so the night sky stays rich navy instead of pitch black.
                    float panoExposureMul = Mathf.Lerp(0.92f, 2.05f, NightFactor);
                    skyExposure *= panoExposureMul;
                }
                _runtimeSkybox.SetFloat(ExposureId, skyExposure + lightningFlash * 0.65f);
            }

            if (_usePanoramicSky)
            {
                UpdatePanoramicSky();
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

        private void UpdatePanoramicSky()
        {
            if (!_usePanoramicSky || _runtimeSkybox == null) return;

            float lightElevDeg = _sunLight != null
                ? Mathf.Asin(Mathf.Clamp(-_sunLight.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg
                : 0f;
            // _sunLight stays at positive pitch (34..46 deg) at night for readable moonlight and >= 16 deg at dawn/sunset;
            // combine with solar clock elevation so Night / Dawn / Day / Sunset panoramas transition accurately.
            float clockSolarElevDeg = Mathf.Cos((_currentHour - 12.5f) * (Mathf.PI / 12f)) * 44f + 2f;
            float e = Mathf.Min(lightElevDeg, clockSolarElevDeg);

            bool isMorning = _currentHour < 12f;
            Texture2D twilightTex = isMorning ? _skyDawnTex : _skySunsetTex;
            SkyPhase twilightPhase = isMorning ? SkyPhase.Dawn : SkyPhase.Sunset;

            Texture2D texA;
            Texture2D texB;
            float blend;
            SkyPhase phase;

            if (e < -8f)
            {
                texA = _skyNightTex;
                texB = _skyNightTex;
                blend = 0f;
                phase = SkyPhase.Night;
            }
            else if (e < 2f)
            {
                texA = _skyNightTex;
                texB = twilightTex;
                blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-8f, 2f, e));
                phase = blend >= 0.5f ? twilightPhase : SkyPhase.Night;
            }
            else if (e <= 14f)
            {
                texA = twilightTex;
                texB = _skyDayTex;
                blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 14f, e));
                phase = blend >= 0.5f ? SkyPhase.Day : twilightPhase;
            }
            else
            {
                texA = _skyDayTex;
                texB = _skyDayTex;
                blend = 0f;
                phase = SkyPhase.Day;
            }

            if (texA != _lastTexA)
            {
                _runtimeSkybox.SetTexture(TexAId, texA);
                _lastTexA = texA;
            }

            if (texB != _lastTexB)
            {
                _runtimeSkybox.SetTexture(TexBId, texB);
                _lastTexB = texB;
            }

            if (Mathf.Abs(blend - _lastBlend) > 0.002f)
            {
                _runtimeSkybox.SetFloat(BlendId, blend);
                _lastBlend = blend;
            }

            if (_sunLight != null)
            {
                float yaw = _sunLight.transform.rotation.eulerAngles.y;
                float rotation = Mathf.Repeat(yaw - 180f + panoramaYawOffset, 360f);
                if (_lastRotation < -900f || Mathf.Abs(Mathf.DeltaAngle(_lastRotation, rotation)) > 0.05f)
                {
                    _runtimeSkybox.SetFloat(RotationId, rotation);
                    _lastRotation = rotation;
                }
            }

            NeighborhoodWeatherState weather = NeighborhoodWeatherSystem.Instance != null
                ? NeighborhoodWeatherSystem.Instance.CurrentWeather
                : NeighborhoodWeatherState.Clear;
            float rain01 = weather == NeighborhoodWeatherState.SummerShower ? 1f
                : (weather == NeighborhoodWeatherState.BreezyOvercast ? 0.5f : 0f);

            Color tint = Color.Lerp(Color.white, new Color(0.62f, 0.66f, 0.72f, 1f), rain01);
            if (!ColorsNearlyEqual(tint, _lastTint))
            {
                _runtimeSkybox.SetColor(TintId, tint);
                _lastTint = tint;
            }

            Color horizonColor = RenderSettings.fogColor;
            if (!ColorsNearlyEqual(horizonColor, _lastHorizonColor))
            {
                _runtimeSkybox.SetColor(HorizonColorId, horizonColor);
                _lastHorizonColor = horizonColor;
            }

            float horizonStrength = 0.25f + rain01 * 0.5f;
            if (Mathf.Abs(horizonStrength - _lastHorizonStrength) > 0.002f)
            {
                _runtimeSkybox.SetFloat(HorizonStrengthId, horizonStrength);
                _lastHorizonStrength = horizonStrength;
            }

            if (phase != _lastSkyPhase)
            {
                _lastSkyPhase = phase;
                DynamicGI.UpdateEnvironment();
            }
        }

        private static bool ColorsNearlyEqual(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) <= 0.002f &&
                   Mathf.Abs(a.g - b.g) <= 0.002f &&
                   Mathf.Abs(a.b - b.b) <= 0.002f &&
                   Mathf.Abs(a.a - b.a) <= 0.002f;
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
