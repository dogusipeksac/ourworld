using System;
using UnityEngine;

namespace OurWorld.Vehicle
{
    /// <summary>
    /// Procedural engine sound synthesizer using OnAudioFilterRead (harmonics of firing frequency + filtered noise)
    /// paired with a second AudioSource playing procedurally generated tyre skid noise based on rear sideways slip.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class EngineAudio : MonoBehaviour
    {
        [SerializeField] private SedanVehicleController vehicle;
        [SerializeField] private AudioSource engineAudioSource;
        [SerializeField] private AudioSource skidAudioSource;

        [Header("Mix")]
        [SerializeField] private float engineMasterGain = 0.28f;
        [SerializeField] private float skidSlipThreshold = 0.3f;
        [SerializeField] private float skidMaxVolume = 0.45f;

        // Thread-safe snapshots copied in Update() for OnAudioFilterRead()
        private volatile float _rpm = 850f;
        private volatile float _throttle;
        private volatile float _speedKmh;
        private volatile int _roadSurfaceCode; // 0=Asphalt, 1=Cobblestone, 2=OffroadGrass, 3=Sand
        private volatile bool _isRoadWet;
        private volatile bool _isDriving;
        private volatile bool _isPaused;
        private volatile float _sampleRate = 48000f;

        // Audio-thread synthesis state
        private double _phase;
        private double _cobblePhase;
        private float _noiseLp;
        private float _roadLp;
        private float _windLp1;
        private float _windLp2;
        private float _wetHpPrev;
        private uint _rngState = 2463534242u;

        private AudioClip _silentCarrierClip;
        private AudioClip _skidNoiseClip;
        private static AudioClip _sharedIgnitionCrankClip;
        private static AudioClip _sharedGearShiftClip;
        private AudioSource _oneShotSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedClips()
        {
            _sharedIgnitionCrankClip = null;
            _sharedGearShiftClip = null;
        }

        public void Configure(SedanVehicleController targetVehicle, AudioSource engineSrc, AudioSource skidSrc)
        {
            vehicle = targetVehicle;
            engineAudioSource = engineSrc;
            skidAudioSource = skidSrc;
        }

        private void Awake()
        {
            if (vehicle == null) vehicle = GetComponent<SedanVehicleController>();
            if (engineAudioSource == null) engineAudioSource = GetComponent<AudioSource>();

            int sr = AudioSettings.outputSampleRate;
            if (sr <= 0) sr = 48000;
            _sampleRate = sr;

            // Attach a silent looping carrier clip so OnAudioFilterRead is guaranteed to run on Unity 6
            if (engineAudioSource != null)
            {
                engineAudioSource.spatialBlend = 0.6f;
                engineAudioSource.loop = true;
                engineAudioSource.playOnAwake = true;
                if (engineAudioSource.clip == null)
                {
                    _silentCarrierClip = AudioClip.Create("EngineSynthCarrier", sr, 1, sr, false);
                    engineAudioSource.clip = _silentCarrierClip;
                }
                if (!engineAudioSource.isPlaying)
                {
                    engineAudioSource.Play();
                }
            }

            // Build procedural band-limited tyre skid noise clip for the second AudioSource
            if (skidAudioSource == null)
            {
                Transform skidChild = transform.Find("SkidAudio");
                if (skidChild != null) skidAudioSource = skidChild.GetComponent<AudioSource>();
            }

            if (skidAudioSource != null)
            {
                skidAudioSource.spatialBlend = 0.6f;
                skidAudioSource.loop = true;
                skidAudioSource.playOnAwake = true;
                skidAudioSource.volume = 0f;
                _skidNoiseClip = BuildProceduralSkidClip(sr);
                skidAudioSource.clip = _skidNoiseClip;
                if (!skidAudioSource.isPlaying)
                {
                    skidAudioSource.Play();
                }
            }

            // Place one-shot AudioSource on a dedicated child GameObject so the vehicle root keeps
            // strictly ONE AudioSource for EngineAudio.OnAudioFilterRead (prevents Unity's multi-AudioSource filter error).
            Transform oneShotChild = transform.Find("EngineOneShotAudio");
            GameObject oneShotObj = oneShotChild != null ? oneShotChild.gameObject : new GameObject("EngineOneShotAudio");
            if (oneShotChild == null)
            {
                oneShotObj.transform.SetParent(transform, false);
            }
            _oneShotSource = oneShotObj.GetComponent<AudioSource>();
            if (_oneShotSource == null)
            {
                _oneShotSource = oneShotObj.AddComponent<AudioSource>();
            }
            _oneShotSource.spatialBlend = 0.35f;
            _oneShotSource.playOnAwake = false;
            _oneShotSource.loop = false;
            if (_sharedIgnitionCrankClip == null) _sharedIgnitionCrankClip = BuildIgnitionCrankClip(22050);
            if (_sharedGearShiftClip == null) _sharedGearShiftClip = BuildGearShiftClip(22050);
        }

        private void OnDestroy()
        {
            if (_silentCarrierClip != null) Destroy(_silentCarrierClip);
            if (_skidNoiseClip != null) Destroy(_skidNoiseClip);
        }

        public void PlayIgnitionStart()
        {
            if (_oneShotSource == null || _sharedIgnitionCrankClip == null) return;
            _oneShotSource.pitch = UnityEngine.Random.Range(0.96f, 1.05f);
            _oneShotSource.PlayOneShot(_sharedIgnitionCrankClip, 0.34f);
        }

        public void PlayGearShiftClick(bool upshift)
        {
            if (_oneShotSource == null || _sharedGearShiftClip == null) return;
            _oneShotSource.pitch = upshift ? UnityEngine.Random.Range(1.02f, 1.12f) : UnityEngine.Random.Range(0.84f, 0.94f);
            _oneShotSource.PlayOneShot(_sharedGearShiftClip, upshift ? 0.22f : 0.16f);
        }

        private static AudioClip BuildIgnitionCrankClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.48f);
            float[] d = new float[n];
            System.Random rng = new System.Random(4193);
            double ph = 0.0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float s = 0f;
                if (t < 0.30f)
                {
                    // 3 rhythmic starter motor compression pulses
                    float pulse = 0.45f + 0.55f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 10f)), 2f);
                    ph += (2.0 * Math.PI * (145f + pulse * 48f)) / sr;
                    float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += 0.22f * (w - lp);
                    s = ((float)(Math.Sin(ph) * 0.55 + Math.Sin(ph * 2.0) * 0.25) + lp * 0.35f) * pulse * 0.68f;
                }
                else
                {
                    // Warm combustion catch bark settling into idle
                    float u = (t - 0.30f) / 0.18f;
                    float freq = Mathf.Lerp(118f, 64f, u);
                    ph += (2.0 * Math.PI * freq) / sr;
                    float env = Mathf.Sin(Mathf.Clamp01(u / 0.18f) * Mathf.PI * 0.5f) * (1f - u * 0.65f);
                    float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += 0.14f * (w - lp);
                    s = ((float)(Math.Sin(ph) * 0.62 + Math.Sin(ph * 2.0) * 0.30) + lp * 0.38f) * env * 0.85f;
                }
                d[i] = Mathf.Clamp(s, -1f, 1f);
            }
            int fade = Mathf.Max(1, Mathf.RoundToInt(sr * 0.005f));
            for (int i = 0; i < fade; i++) { float g = i / (float)fade; d[i] *= g; d[n - 1 - i] *= g; }
            AudioClip clip = AudioClip.Create("SFX_IgnitionCrank", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static AudioClip BuildGearShiftClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.14f);
            float[] d = new float[n];
            System.Random rng = new System.Random(4271);
            double ph = 0.0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = Mathf.Sin(Mathf.Clamp01(u / 0.12f) * Mathf.PI * 0.5f) * Mathf.Exp(-u * 6.5f);
                ph += (2.0 * Math.PI * Mathf.Lerp(210f, 92f, u)) / sr;
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.24f * (w - lp);
                float flutter = 0.72f + 0.28f * Mathf.Sin(u * 42f);
                d[i] = Mathf.Clamp(((float)Math.Sin(ph) * 0.42f + lp * 0.68f) * flutter * env, -1f, 1f);
            }
            int fade = Mathf.Max(1, Mathf.RoundToInt(sr * 0.005f));
            for (int i = 0; i < fade; i++) { float g = i / (float)fade; d[i] *= g; d[n - 1 - i] *= g; }
            AudioClip clip = AudioClip.Create("SFX_GearShift", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static AudioClip BuildProceduralSkidClip(int sampleRate)
        {
            int length = sampleRate * 2;
            float[] samples = new float[length];
            System.Random rng = new System.Random(7731);

            float lp = 0f;
            float hp = 0f;
            float prev = 0f;
            double squealPhase1 = 0.0;
            double squealPhase2 = 0.0;

            for (int i = 0; i < length; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Band-pass shape around tyre screech frequencies
                lp += 0.24f * (white - lp);
                hp = 0.86f * (hp + lp - prev);
                prev = lp;

                squealPhase1 += (2.0 * Math.PI * 1040.0) / sampleRate;
                squealPhase2 += (2.0 * Math.PI * 1490.0) / sampleRate;
                float tone = (float)(Math.Sin(squealPhase1) * 0.22 + Math.Sin(squealPhase2) * 0.14);

                float val = (hp * 0.72f + tone * (0.45f + 0.55f * Math.Abs(hp)));
                samples[i] = Mathf.Clamp(val, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ProceduralTyreSkid", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void Update()
        {
            if (vehicle == null)
            {
                _isDriving = false;
                return;
            }

            bool driving = vehicle.IsPlayerDriving;
            _isDriving = driving;
            _isPaused = Time.timeScale <= 0.001f || AudioListener.pause;
            _rpm = Mathf.Clamp(vehicle.EngineRpm, 600f, 7500f);
            _throttle = Mathf.Clamp01(vehicle.CurrentThrottle);
            _speedKmh = Mathf.Clamp(vehicle.SpeedKmh, 0f, 220f);
            _roadSurfaceCode = (int)vehicle.CurrentRoadSurface;
            _isRoadWet = vehicle.IsRoadWet;

            if (skidAudioSource != null)
            {
                float targetSkidVol = 0f;
                if (driving && vehicle.SpeedKmh > 6f)
                {
                    float slip = vehicle.MaxRearSidewaysSlip;
                    if (slip > skidSlipThreshold)
                    {
                        targetSkidVol = Mathf.Clamp01((slip - skidSlipThreshold) / 0.5f) * skidMaxVolume;
                    }
                }

                skidAudioSource.volume = Mathf.Lerp(skidAudioSource.volume, targetSkidVol, 1f - Mathf.Exp(-14f * Time.deltaTime));
                skidAudioSource.pitch = Mathf.Lerp(0.88f, 1.18f, Mathf.InverseLerp(10f, 90f, vehicle.SpeedKmh));
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null || data.Length == 0 || channels <= 0)
            {
                return;
            }

            if (!_isDriving || _isPaused)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = 0f;
                }
                return;
            }

            float rpm = _rpm;
            float throttle = _throttle;
            float speedKmh = _speedKmh;
            int surface = _roadSurfaceCode;
            bool wetRoad = _isRoadWet;
            float sr = _sampleRate;
            if (sr < 8000f) sr = 48000f;

            // Firing frequency for 4-cylinder 4-stroke engine: rpm / 60 * 2
            double firingFreq = (rpm / 60.0) * 2.0;
            double phaseStep = (2.0 * Math.PI * firingFreq) / sr;

            float volume = (0.25f + 0.5f * throttle) * engineMasterGain;
            float noiseCutoff = 0.04f + throttle * 0.14f;
            float noiseMix = 0.12f + throttle * 0.18f;

            // Speed-scaled tire rolling road texture + wet spray hiss + aerodynamic cabin wind rush
            float speedNorm = Math.Min(1f, speedKmh / 110f);
            float roadGain = speedKmh > 3f ? Math.Min(1f, (speedKmh - 3f) / 65f) * engineMasterGain * 0.34f : 0f;
            float windGain = speedKmh > 46f ? Math.Min(1f, (speedKmh - 46f) / 85f) * engineMasterGain * 0.32f : 0f;
            float wetGain = (wetRoad && speedKmh > 10f) ? Math.Min(1f, (speedKmh - 10f) / 70f) * engineMasterGain * 0.24f : 0f;
            double cobbleStep = (2.0 * Math.PI * (14.0 + speedNorm * 42.0)) / sr;
            float roadCut = surface == 2 || surface == 3 ? 0.12f : (surface == 1 ? 0.075f : 0.032f);

            int frameCount = data.Length / channels;
            for (int f = 0; f < frameCount; f++)
            {
                _phase += phaseStep;
                if (_phase > 2.0 * Math.PI * 1000.0)
                {
                    _phase -= 2.0 * Math.PI * 1000.0;
                }

                double p = _phase;
                // Sum of sub-harmonic + firing fundamental + upper harmonics
                float h05 = (float)Math.Sin(p * 0.5) * 0.30f;
                float h1 = (float)Math.Sin(p) * 0.52f;
                float h2 = (float)Math.Sin(p * 2.0) * (0.28f + 0.14f * throttle);
                float h3 = (float)Math.Sin(p * 3.0) * (0.14f + 0.12f * throttle);
                float h4 = (float)Math.Sin(p * 4.0) * (0.06f + 0.08f * throttle);

                // Thread-safe xorshift PRNG for filtered combustion/exhaust noise + road/wind texture
                uint x = _rngState;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _rngState = x;
                float white = ((x & 0xFFFF) / 32768f) - 1f;
                _noiseLp += noiseCutoff * (white - _noiseLp);

                float roadSample = 0f;
                if (roadGain > 0.0005f)
                {
                    _roadLp += roadCut * (white - _roadLp);
                    if (surface == 1)
                    {
                        // Textured Istanbul kilit parke cobblestone rumble
                        _cobblePhase += cobbleStep;
                        if (_cobblePhase > 2.0 * Math.PI * 1000.0) _cobblePhase -= 2.0 * Math.PI * 1000.0;
                        float cobbleMod = 0.65f + 0.35f * (float)Math.Sin(_cobblePhase);
                        roadSample = (_roadLp * 1.45f * cobbleMod + (float)Math.Sin(_cobblePhase) * 0.12f) * roadGain;
                    }
                    else if (surface == 2 || surface == 3)
                    {
                        // Off-road grass/sand crunch & suspension rumble
                        roadSample = _roadLp * 1.55f * roadGain;
                    }
                    else
                    {
                        // Smooth warm asphalt rolling hum
                        roadSample = _roadLp * 0.85f * roadGain;
                    }
                }

                float wetSample = 0f;
                if (wetGain > 0.0005f)
                {
                    float hp = white - _wetHpPrev * 0.72f;
                    _wetHpPrev = white;
                    wetSample = hp * 0.42f * wetGain;
                }

                float windSample = 0f;
                if (windGain > 0.0005f)
                {
                    _windLp1 += 0.055f * (white - _windLp1);
                    _windLp2 += 0.015f * (_windLp1 - _windLp2);
                    windSample = (_windLp1 - _windLp2 * 0.65f) * 1.35f * windGain;
                }

                float sample = (h05 + h1 + h2 + h3 + h4 + _noiseLp * noiseMix) * volume
                             + roadSample + wetSample + windSample;
                // Gentle soft clip
                sample = sample / (1f + Math.Abs(sample));

                int baseIdx = f * channels;
                for (int c = 0; c < channels; c++)
                {
                    data[baseIdx + c] = sample;
                }
            }
        }
    }
}
