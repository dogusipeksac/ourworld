using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Living Istanbul · Hamidiye Mahallesi spatial soundscape & unified world wind field:
    /// - Unified dynamic wind vector (Direction, Strength01, GustFactor) driving foliage sway, steam, and rain drift
    /// - Continuous zero-asset procedural ambient bed: daytime city/breeze murmur, nocturnal summer crickets,
    ///   cozy acoustically muffled indoor/mall room tone, and underwater low-pass pressure rumble
    /// - Context-aware 3D spatial one-shots: morning/park songbirds, coastal Bosphorus seagulls (martı),
    ///   distant ferry horn (vapur düdüğü), neighborhood fountain trickle, speed-of-sound thunder rumbles,
    ///   and authentic vehicle dashboard blinker relay ticks.
    /// </summary>
    public sealed class IstanbulLivingSoundscape : MonoBehaviour
    {
        public static IstanbulLivingSoundscape Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            WindDirection = new Vector3(0.86f, 0f, 0.51f);
            WindStrength01 = 0.25f;
            GustFactor = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_IstanbulLivingSoundscape");
            go.AddComponent<IstanbulLivingSoundscape>();
        }

        public static Vector3 WindDirection { get; private set; } = new Vector3(0.86f, 0f, 0.51f);
        public static float WindStrength01 { get; private set; } = 0.25f;
        public static float GustFactor { get; private set; }

        // Known Istanbul Hamidiye fountain coordinates for proximity water trickle ambience
        private static readonly Vector3[] FountainSpots =
        {
            new Vector3(4.5f, 0.5f, 16.5f),
            new Vector3(-18.5f, 0.5f, 12.0f),
            new Vector3(-158.0f, 0.5f, -6.5f),
            new Vector3(16.0f, 0.5f, -28.0f)
        };

        private AudioSource _bedSource;
        private AudioClip _silentCarrierClip;
        private AudioSource _spatialEmitter;
        private AudioSource _ui2DEmitter;

        private AudioClip _birdClip;
        private AudioClip _seagullClip;
        private AudioClip _ferryHornClip;
        private AudioClip _fountainClip;
        private AudioClip _thunderClip;
        private AudioClip _blinkerTickClip;
        private AudioClip _blinkerTockClip;

        private float _birdTimer = 6.5f;
        private float _seagullTimer = 14f;
        private float _ferryTimer = 46f;
        private float _fountainTimer = 1.5f;

        // Audio-thread snapshots (volatile, zero Unity API calls inside OnAudioFilterRead)
        private volatile float _sampleRate = 48000f;
        private volatile bool _isPaused;
        private volatile float _windGain;
        private volatile float _cityGain;
        private volatile float _cricketGain;
        private volatile float _indoorGain;
        private volatile float _underwaterGain;

        // Audio-thread synthesis state
        private uint _rngState = 987654321u;
        private float _lpCity;
        private float _lpWind1;
        private float _lpWind2;
        private float _lpIndoor;
        private float _lpUnderwater;
        private double _gustPhase;
        private double _cricketCarrierPhase;
        private double _cricketPulsePhase;
        private double _cricketChirpGatePhase;
        private double _indoorHumPhase;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            int sr = AudioSettings.outputSampleRate;
            if (sr <= 0) sr = 48000;
            _sampleRate = sr;

            _bedSource = gameObject.AddComponent<AudioSource>();
            _bedSource.spatialBlend = 0f;
            _bedSource.loop = true;
            _bedSource.playOnAwake = true;
            _bedSource.priority = 180;
            _bedSource.volume = 1f;
            _silentCarrierClip = AudioClip.Create("IstanbulAmbienceCarrier", sr, 1, sr, false);
            _bedSource.clip = _silentCarrierClip;
            _bedSource.Play();

            GameObject spGo = new GameObject("SpatialAmbienceEmitter");
            spGo.transform.SetParent(transform, false);
            _spatialEmitter = spGo.AddComponent<AudioSource>();
            _spatialEmitter.spatialBlend = 0.85f;
            _spatialEmitter.rolloffMode = AudioRolloffMode.Linear;
            _spatialEmitter.minDistance = 6f;
            _spatialEmitter.maxDistance = 65f;
            _spatialEmitter.dopplerLevel = 0f;
            _spatialEmitter.playOnAwake = false;
            _spatialEmitter.priority = 160;

            GameObject uiGo = new GameObject("SubtleRealism2DEmitter");
            uiGo.transform.SetParent(transform, false);
            _ui2DEmitter = uiGo.AddComponent<AudioSource>();
            _ui2DEmitter.spatialBlend = 0f;
            _ui2DEmitter.playOnAwake = false;
            _ui2DEmitter.priority = 150;

            const int clipSr = 22050;
            _birdClip = SynthSongbirdClip(clipSr);
            _seagullClip = SynthSeagullClip(clipSr);
            _ferryHornClip = SynthFerryHornClip(clipSr);
            _fountainClip = SynthFountainTrickleClip(clipSr);
            _thunderClip = SynthThunderClip(clipSr);
            _blinkerTickClip = SynthBlinkerRelayClip(clipSr, highTick: true);
            _blinkerTockClip = SynthBlinkerRelayClip(clipSr, highTick: false);
        }

        private void OnDestroy()
        {
            if (_silentCarrierClip != null) Destroy(_silentCarrierClip);
            if (_birdClip != null) Destroy(_birdClip);
            if (_seagullClip != null) Destroy(_seagullClip);
            if (_ferryHornClip != null) Destroy(_ferryHornClip);
            if (_fountainClip != null) Destroy(_fountainClip);
            if (_thunderClip != null) Destroy(_thunderClip);
            if (_blinkerTickClip != null) Destroy(_blinkerTickClip);
            if (_blinkerTockClip != null) Destroy(_blinkerTockClip);
            if (Instance == this) Instance = null;
        }

        public static void PlayThunderAfterDelay(float delaySec, float intensity01 = 0.78f)
        {
            if (Instance == null || !Instance.isActiveAndEnabled) return;
            Instance.StartCoroutine(Instance.DelayedThunderRoutine(Mathf.Clamp(delaySec, 0.35f, 4.5f), Mathf.Clamp01(intensity01)));
        }

        public static void PlayBlinkerClick(bool onTick)
        {
            if (Instance == null || Instance._ui2DEmitter == null) return;
            AudioClip clip = onTick ? Instance._blinkerTickClip : Instance._blinkerTockClip;
            if (clip == null) return;
            Instance._ui2DEmitter.pitch = onTick ? UnityEngine.Random.Range(0.98f, 1.03f) : UnityEngine.Random.Range(0.95f, 1.01f);
            Instance._ui2DEmitter.PlayOneShot(clip, 0.14f);
        }

        private IEnumerator DelayedThunderRoutine(float delay, float intensity01)
        {
            yield return new WaitForSeconds(delay);
            if (_ui2DEmitter == null || _thunderClip == null) yield break;
            if (OurWorld.UI.MainMenuController.IsMenuActive) yield break;

            bool sheltered = IsPlayerShelteredNow();
            float vol = Mathf.Lerp(0.24f, 0.56f, intensity01) * (sheltered ? 0.45f : 1.0f);
            _ui2DEmitter.pitch = UnityEngine.Random.Range(0.84f, 1.08f);
            _ui2DEmitter.PlayOneShot(_thunderClip, vol);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool menuOrPaused = OurWorld.UI.MainMenuController.IsMenuActive || Time.timeScale <= 0.001f || AudioListener.pause;
            _isPaused = menuOrPaused;
            if (menuOrPaused || dt <= 0f) return;

            // 1. Unified Istanbul Wind Field
            NeighborhoodWeatherState weather = NeighborhoodWeatherSystem.Instance != null
                ? NeighborhoodWeatherSystem.Instance.CurrentWeather
                : NeighborhoodWeatherState.Clear;

            float baseWind = weather == NeighborhoodWeatherState.SummerShower ? 0.86f
                : (weather == NeighborhoodWeatherState.BreezyOvercast ? 0.58f : 0.24f);
            float gustNoise = Mathf.PerlinNoise(Time.time * 0.23f, 3.7f);
            GustFactor = Mathf.Clamp01((gustNoise - 0.38f) / 0.62f);
            float targetWind = Mathf.Clamp01(baseWind + GustFactor * (weather == NeighborhoodWeatherState.Clear ? 0.16f : 0.30f));
            WindStrength01 = Mathf.Lerp(WindStrength01, targetWind, 1f - Mathf.Exp(-1.8f * dt));

            float windYawDeg = 28f + (Mathf.PerlinNoise(Time.time * 0.045f, 11.2f) - 0.5f) * 38f;
            float rad = windYawDeg * Mathf.Deg2Rad;
            WindDirection = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)).normalized;

            // 2. Context & Location Sampling
            PlayerModeController mode = PlayerModeController.Instance;
            Vector3 playerPos = mode != null ? mode.ActiveWorldPosition : Vector3.zero;
            bool underwater = mode != null && mode.CurrentMode == ControlMode.OnFoot &&
                              mode.FootController != null && mode.FootController.IsUnderwater;
            bool sheltered = IsPlayerShelteredNow();
            bool inCar = mode != null && mode.IsDriving;
            float hour = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentHour : 10f;
            float nightFactor = DayNightCycle.Instance != null ? DayNightCycle.Instance.NightFactor : 0f;

            bool nearNature = playerPos.x < -70f || playerPos.z < -65f || playerPos.z > 75f ||
                              (playerPos.x > -35f && playerPos.x < 10f && playerPos.z > -40f && playerPos.z < 5f);
            bool nearCoastOrLake = playerPos.x > 16f || playerPos.x < -125f || playerPos.z > 130f;

            // 3. Smooth Ambient Bed Gains (whisper-quiet & warm — zero harsh wind static while walking!)
            float outdoorMul = underwater ? 0f : (sheltered ? 0.14f : (inCar ? 0.22f : 1.0f));
            float stormExtra = weather == NeighborhoodWeatherState.SummerShower ? 0.0055f
                : (weather == NeighborhoodWeatherState.BreezyOvercast ? 0.0022f : 0f);
            float targetWindGain = (0.0012f + WindStrength01 * 0.0028f + stormExtra) * outdoorMul;
            float targetCityGain = Mathf.Lerp(0.0045f, 0.0018f, nightFactor) * (nearNature ? 0.50f : 1.0f) * outdoorMul;
            float cricketSeason = weather == NeighborhoodWeatherState.SummerShower ? 0.12f : 1.0f;
            float targetCricketGain = nightFactor * cricketSeason * (nearNature ? 0.022f : 0.012f) * outdoorMul;
            float targetIndoorGain = (!underwater && sheltered) ? 0.008f : 0f;
            float targetUnderwaterGain = underwater ? 0.075f : 0f;

            float blend = 1f - Mathf.Exp(-3.5f * dt);
            _windGain = Mathf.Lerp(_windGain, targetWindGain, blend);
            _cityGain = Mathf.Lerp(_cityGain, targetCityGain, blend);
            _cricketGain = Mathf.Lerp(_cricketGain, targetCricketGain, blend);
            _indoorGain = Mathf.Lerp(_indoorGain, targetIndoorGain, blend);
            _underwaterGain = Mathf.Lerp(_underwaterGain, targetUnderwaterGain, blend);

            if (underwater || _spatialEmitter == null) return;

            // 4. Spatial One-Shot Wildlife & Istanbul Maritime Layer
            bool isRaining = weather == NeighborhoodWeatherState.SummerShower;
            bool isDaytimeBirds = hour >= 5.5f && hour <= 18.2f && !isRaining;
            if (isDaytimeBirds && !sheltered)
            {
                _birdTimer -= dt;
                if (_birdTimer <= 0f)
                {
                    _birdTimer = nearNature ? UnityEngine.Random.Range(6.5f, 13.5f) : UnityEngine.Random.Range(12f, 22f);
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float dist = UnityEngine.Random.Range(9f, 22f);
                    _spatialEmitter.transform.position = playerPos + new Vector3(Mathf.Cos(ang) * dist, UnityEngine.Random.Range(4.5f, 9.5f), Mathf.Sin(ang) * dist);
                    _spatialEmitter.pitch = UnityEngine.Random.Range(0.88f, 1.18f);
                    _spatialEmitter.PlayOneShot(_birdClip, inCar ? 0.08f : (nearNature ? 0.22f : 0.15f));
                }
            }

            if (nearCoastOrLake && !sheltered && hour >= 6.0f && hour <= 20.0f && !isRaining)
            {
                _seagullTimer -= dt;
                if (_seagullTimer <= 0f)
                {
                    _seagullTimer = UnityEngine.Random.Range(16f, 32f);
                    Vector3 seaDir = playerPos.x < -110f ? Vector3.left : Vector3.right;
                    _spatialEmitter.transform.position = playerPos + seaDir * UnityEngine.Random.Range(14f, 28f) + Vector3.up * UnityEngine.Random.Range(10f, 18f);
                    _spatialEmitter.pitch = UnityEngine.Random.Range(0.90f, 1.12f);
                    _spatialEmitter.PlayOneShot(_seagullClip, inCar ? 0.08f : 0.20f);
                }
            }

            if (!sheltered && playerPos.x > -45f)
            {
                _ferryTimer -= dt;
                if (_ferryTimer <= 0f)
                {
                    _ferryTimer = UnityEngine.Random.Range(68f, 120f);
                    _spatialEmitter.transform.position = playerPos + new Vector3(44f, 5f, UnityEngine.Random.Range(-18f, 18f));
                    _spatialEmitter.pitch = UnityEngine.Random.Range(0.92f, 1.06f);
                    _spatialEmitter.PlayOneShot(_ferryHornClip, inCar ? 0.10f : 0.24f);
                }
            }

            if (!sheltered && !inCar)
            {
                _fountainTimer -= dt;
                if (_fountainTimer <= 0f)
                {
                    _fountainTimer = 1.85f;
                    for (int i = 0; i < FountainSpots.Length; i++)
                    {
                        float dSq = (playerPos - FountainSpots[i]).sqrMagnitude;
                        if (dSq <= 11.5f * 11.5f)
                        {
                            _spatialEmitter.transform.position = FountainSpots[i] + Vector3.up * 0.9f;
                            _spatialEmitter.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
                            _spatialEmitter.PlayOneShot(_fountainClip, 0.24f);
                            break;
                        }
                    }
                }
            }
        }

        private static bool IsPlayerShelteredNow()
        {
            if (InteriorZone.IsPlayerInsideAny) return true;
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null) return false;
            return NeighborhoodWeatherSystem.IsPositionSheltered(mode.ActiveWorldPosition);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null || data.Length == 0 || channels <= 0) return;

            if (_isPaused)
            {
                for (int i = 0; i < data.Length; i++) data[i] = 0f;
                return;
            }

            float sr = _sampleRate;
            if (sr < 8000f) sr = 48000f;

            float windG = _windGain;
            float cityG = _cityGain;
            float cricketG = _cricketGain;
            float indoorG = _indoorGain;
            float uwG = _underwaterGain;

            double gustStep = (2.0 * Math.PI * 0.16) / sr;
            double cricketCarStep = (2.0 * Math.PI * 4250.0) / sr;
            double cricketPulseStep = (2.0 * Math.PI * 26.0) / sr;
            double cricketGateStep = (2.0 * Math.PI * 1.15) / sr;
            double indoorHumStep = (2.0 * Math.PI * 96.0) / sr;

            int frames = data.Length / channels;
            for (int f = 0; f < frames; f++)
            {
                uint x = _rngState;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _rngState = x;
                float white = ((x & 0xFFFF) / 32768f) - 1f;

                // Soft brown-filtered city murmur
                _lpCity += 0.0045f * (white - _lpCity);

                // Warm, deeply low-passed gentle air breeze (zero high-frequency white-noise hiss)
                _gustPhase += gustStep;
                if (_gustPhase > Math.PI * 200.0) _gustPhase -= Math.PI * 200.0;
                float gustMod = 0.68f + 0.32f * (float)Math.Sin(_gustPhase);
                float windCut = 0.0045f + 0.0055f * gustMod;
                _lpWind1 += windCut * (white - _lpWind1);
                _lpWind2 += 0.018f * (_lpWind1 - _lpWind2);
                float windSample = _lpWind2 * gustMod;

                // Gentle nocturnal Istanbul summer crickets
                float cricketSample = 0f;
                if (cricketG > 0.0005f)
                {
                    _cricketCarrierPhase += cricketCarStep;
                    _cricketPulsePhase += cricketPulseStep;
                    _cricketChirpGatePhase += cricketGateStep;
                    if (_cricketCarrierPhase > Math.PI * 200.0) _cricketCarrierPhase -= Math.PI * 200.0;
                    if (_cricketPulsePhase > Math.PI * 200.0) _cricketPulsePhase -= Math.PI * 200.0;
                    if (_cricketChirpGatePhase > Math.PI * 200.0) _cricketChirpGatePhase -= Math.PI * 200.0;

                    float gate = (float)Math.Sin(_cricketChirpGatePhase);
                    if (gate > 0.38f)
                    {
                        float pulse = Math.Max(0f, (float)Math.Sin(_cricketPulsePhase));
                        float env = (gate - 0.38f) * 1.61f * pulse;
                        cricketSample = (float)Math.Sin(_cricketCarrierPhase) * env;
                    }
                }

                // Cozy indoor room tone
                float indoorSample = 0f;
                if (indoorG > 0.0005f)
                {
                    _indoorHumPhase += indoorHumStep;
                    if (_indoorHumPhase > Math.PI * 200.0) _indoorHumPhase -= Math.PI * 200.0;
                    _lpIndoor += 0.008f * (white - _lpIndoor);
                    indoorSample = _lpIndoor * 0.72f + (float)Math.Sin(_indoorHumPhase) * 0.12f;
                }

                // Deep muffled underwater rumble
                float uwSample = 0f;
                if (uwG > 0.0005f)
                {
                    _lpUnderwater += 0.0032f * (white - _lpUnderwater);
                    uwSample = _lpUnderwater * 1.85f;
                }

                float sample = windSample * windG
                             + _lpCity * cityG
                             + cricketSample * cricketG
                             + indoorSample * indoorG
                             + uwSample * uwG;

                if (sample > 0.85f) sample = 0.85f;
                else if (sample < -0.85f) sample = -0.85f;

                int idx = f * channels;
                for (int c = 0; c < channels; c++)
                {
                    data[idx + c] = sample;
                }
            }
        }

        // =====================================================================
        // PROCEDURAL ISTANBUL SOUND SYNTHESIZERS (5 ms fade-in/out, zero clicks)
        // =====================================================================
        private static AudioClip SynthSongbirdClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.62f);
            float[] d = new float[n];
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float s = 0f;
                // Three delicate rising/falling sparrow notes
                if (t < 0.14f)
                {
                    float u = t / 0.14f;
                    float f = Mathf.Lerp(2900f, 4150f, u) + Mathf.Sin(t * 90f) * 140f;
                    ph += (2.0 * Math.PI * f) / sr;
                    s = (float)Math.Sin(ph) * Mathf.Sin(u * Mathf.PI);
                }
                else if (t >= 0.21f && t < 0.36f)
                {
                    float u = (t - 0.21f) / 0.15f;
                    float f = Mathf.Lerp(3400f, 4650f, Mathf.Sin(u * Mathf.PI * 0.75f));
                    ph += (2.0 * Math.PI * f) / sr;
                    s = (float)Math.Sin(ph) * Mathf.Sin(u * Mathf.PI) * 0.9f;
                }
                else if (t >= 0.43f && t < 0.59f)
                {
                    float u = (t - 0.43f) / 0.16f;
                    float f = Mathf.Lerp(4300f, 3150f, u);
                    ph += (2.0 * Math.PI * f) / sr;
                    s = (float)Math.Sin(ph) * Mathf.Sin(u * Mathf.PI) * 0.78f;
                }
                d[i] = Mathf.Clamp(s * 0.52f, -1f, 1f);
            }
            ApplyEdgeFades(d, sr);
            AudioClip clip = AudioClip.Create("SFX_IstanbulSongbird", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static AudioClip SynthSeagullClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.92f);
            float[] d = new float[n];
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float s = 0f;
                // Classic two-cry Bosphorus martı call
                if (t < 0.34f)
                {
                    float u = t / 0.34f;
                    float f = Mathf.Lerp(1780f, 1080f, u * u) + Mathf.Sin(t * 58f) * 45f;
                    ph += (2.0 * Math.PI * f) / sr;
                    float raw = (float)(Math.Sin(ph) * 0.65 + Math.Sin(ph * 2.0) * 0.26 + Math.Sin(ph * 3.0) * 0.12);
                    s = raw * Mathf.Sin(u * Mathf.PI);
                }
                else if (t >= 0.46f && t < 0.86f)
                {
                    float u = (t - 0.46f) / 0.40f;
                    float f = Mathf.Lerp(1620f, 960f, u);
                    ph += (2.0 * Math.PI * f) / sr;
                    float raw = (float)(Math.Sin(ph) * 0.62 + Math.Sin(ph * 2.0) * 0.28);
                    s = raw * Mathf.Sin(u * Mathf.PI) * 0.82f;
                }
                d[i] = Mathf.Clamp(s * 0.48f, -1f, 1f);
            }
            ApplyEdgeFades(d, sr);
            AudioClip clip = AudioClip.Create("SFX_BosphorusSeagull", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static AudioClip SynthFerryHornClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 2.15f);
            float[] d = new float[n];
            double ph1 = 0.0, ph2 = 0.0;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float attack = Mathf.Clamp01(u / 0.14f);
                float release = 1f - Mathf.Clamp01((u - 0.68f) / 0.32f);
                float env = attack * release;
                // Iconic warm dual-tone Istanbul Şehir Hatları ferry horn (146 Hz + 184 Hz)
                ph1 += (2.0 * Math.PI * 146.8) / sr;
                ph2 += (2.0 * Math.PI * 185.0) / sr;
                float tone = (float)(
                    Math.Sin(ph1) * 0.46 + Math.Sin(ph1 * 2.0) * 0.24 + Math.Sin(ph1 * 3.0) * 0.12 +
                    Math.Sin(ph2) * 0.34 + Math.Sin(ph2 * 2.0) * 0.16);
                d[i] = Mathf.Clamp(tone * env * 0.50f, -1f, 1f);
            }
            ApplyEdgeFades(d, sr);
            AudioClip clip = AudioClip.Create("SFX_IstanbulFerryHorn", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static AudioClip SynthFountainTrickleClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 1.90f);
            float[] d = new float[n];
            System.Random rng = new System.Random(4812);
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = Mathf.Sin(u * Mathf.PI);
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += 0.28f * (w - lp1);
                lp2 += 0.07f * (lp1 - lp2);
                float shimmer = (lp1 - lp2) * (0.72f + 0.28f * Mathf.Sin(u * 46f));
                d[i] = Mathf.Clamp(shimmer * env * 0.55f, -1f, 1f);
            }
            ApplyEdgeFades(d, sr);
            AudioClip clip = AudioClip.Create("SFX_FountainTrickle", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static AudioClip SynthThunderClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 3.40f);
            float[] d = new float[n];
            System.Random rng = new System.Random(9081);
            float lpDeep = 0f;
            float lpMid = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float attack = Mathf.Clamp01(u / 0.06f);
                float rumbleWaves = 0.65f + 0.35f * Mathf.Sin(u * 19f) * Mathf.Cos(u * 7.5f);
                float decay = Mathf.Exp(-u * 1.95f);
                float crack = Mathf.Exp(-u * 11f) * 0.45f;
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lpDeep += 0.014f * (w - lpDeep);
                lpMid += 0.075f * (w - lpMid);
                float s = (lpDeep * 2.6f * rumbleWaves + lpMid * crack) * attack * decay;
                d[i] = Mathf.Clamp(s * 0.92f, -1f, 1f);
            }
            ApplyEdgeFades(d, sr);
            AudioClip clip = AudioClip.Create("SFX_SummerThunder", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static AudioClip SynthBlinkerRelayClip(int sr, bool highTick)
        {
            int n = Mathf.RoundToInt(sr * 0.028f);
            float[] d = new float[n];
            System.Random rng = new System.Random(highTick ? 3011 : 3019);
            double ph = 0.0;
            float freq = highTick ? 1680f : 1220f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = Mathf.Exp(-u * 16f);
                ph += (2.0 * Math.PI * freq) / sr;
                float click = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-u * 26f) * 0.45f;
                d[i] = Mathf.Clamp(((float)Math.Sin(ph) * 0.62f + click) * env, -1f, 1f);
            }
            ApplyEdgeFades(d, sr, 0.002f);
            AudioClip clip = AudioClip.Create(highTick ? "SFX_BlinkerTick" : "SFX_BlinkerTock", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static void ApplyEdgeFades(float[] samples, int sr, float fadeSeconds = 0.005f)
        {
            int fadeSamples = Mathf.Min(samples.Length / 4, Mathf.Max(1, Mathf.RoundToInt(sr * fadeSeconds)));
            for (int i = 0; i < fadeSamples; i++)
            {
                float g = i / (float)fadeSamples;
                samples[i] *= g;
                samples[samples.Length - 1 - i] *= g;
            }
        }
    }
}
