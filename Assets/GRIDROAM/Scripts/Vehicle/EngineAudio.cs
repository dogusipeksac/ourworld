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
        private volatile bool _isDriving;
        private volatile bool _isPaused;
        private volatile float _sampleRate = 48000f;

        // Audio-thread synthesis state
        private double _phase;
        private float _noiseLp;
        private uint _rngState = 2463534242u;

        private AudioClip _silentCarrierClip;
        private AudioClip _skidNoiseClip;

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
        }

        private void OnDestroy()
        {
            if (_silentCarrierClip != null) Destroy(_silentCarrierClip);
            if (_skidNoiseClip != null) Destroy(_skidNoiseClip);
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
            float sr = _sampleRate;
            if (sr < 8000f) sr = 48000f;

            // Firing frequency for 4-cylinder 4-stroke engine: rpm / 60 * 2
            double firingFreq = (rpm / 60.0) * 2.0;
            double phaseStep = (2.0 * Math.PI * firingFreq) / sr;

            float volume = (0.25f + 0.5f * throttle) * engineMasterGain;
            float noiseCutoff = 0.04f + throttle * 0.14f;
            float noiseMix = 0.12f + throttle * 0.18f;

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

                // Thread-safe xorshift PRNG for filtered combustion/exhaust noise
                uint x = _rngState;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _rngState = x;
                float white = ((x & 0xFFFF) / 32768f) - 1f;
                _noiseLp += noiseCutoff * (white - _noiseLp);

                float sample = (h05 + h1 + h2 + h3 + h4 + _noiseLp * noiseMix) * volume;
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
