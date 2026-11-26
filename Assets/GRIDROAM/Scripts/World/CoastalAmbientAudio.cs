using System;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Procedural low-volume coastal wind & sea surf ambience positioned along the seawall
    /// using OnAudioFilterRead filtered noise (zero audio asset size).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CoastalAmbientAudio : MonoBehaviour
    {
        [SerializeField] private float seawallX = 35.5f;
        [SerializeField] private float minZ = -24f;
        [SerializeField] private float maxZ = 278f;
        [SerializeField] private float masterGain = 0.014f;

        private AudioSource _source;
        private AudioClip _silentCarrierClip;

        // Audio-thread state
        private volatile float _sampleRate = 48000f;
        private volatile float _gain = 0.014f;
        private uint _rngState = 1357924681u;
        private double _wavePhase;
        private double _gustPhase;
        private float _lpSurf;
        private float _lpWind;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            int sr = AudioSettings.outputSampleRate;
            if (sr <= 0) sr = 48000;
            _sampleRate = sr;
            _gain = Mathf.Min(masterGain, 0.016f);

            if (_source != null)
            {
                // True 3D spatial attenuation so coastal surf NEVER bleeds inland as global 2D wind noise!
                _source.spatialBlend = 0.98f;
                _source.rolloffMode = AudioRolloffMode.Linear;
                _source.minDistance = 6f;
                _source.maxDistance = 36f;
                _source.loop = true;
                _source.playOnAwake = true;
                _source.volume = 1f;

                if (_source.clip == null)
                {
                    _silentCarrierClip = AudioClip.Create("CoastalSeaCarrier", sr, 1, sr, false);
                    _source.clip = _silentCarrierClip;
                }

                if (!_source.isPlaying)
                {
                    _source.Play();
                }
            }
        }

        private void OnDestroy()
        {
            if (_silentCarrierClip != null)
            {
                Destroy(_silentCarrierClip);
            }
        }

        private void Update()
        {
            _gain = Mathf.Min(masterGain, 0.016f);
            if (PlayerModeController.Instance != null)
            {
                Vector3 playerPos = PlayerModeController.Instance.ActiveWorldPosition;
                float clampedZ = Mathf.Clamp(playerPos.z, minZ, maxZ);
                transform.position = new Vector3(seawallX, 1.5f, clampedZ);
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null || data.Length == 0 || channels <= 0)
            {
                return;
            }

            float sr = _sampleRate;
            if (sr < 8000f) sr = 48000f;

            double waveStep = (2.0 * Math.PI * 0.14) / sr;
            double gustStep = (2.0 * Math.PI * 0.055) / sr;
            float gain = _gain;

            int frames = data.Length / channels;
            for (int f = 0; f < frames; f++)
            {
                _wavePhase += waveStep;
                if (_wavePhase > Math.PI * 200.0) _wavePhase -= Math.PI * 200.0;

                _gustPhase += gustStep;
                if (_gustPhase > Math.PI * 200.0) _gustPhase -= Math.PI * 200.0;

                uint x = _rngState;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _rngState = x;
                float white = ((x & 0xFFFF) / 32768f) - 1f;

                float waveEnv = 0.55f + 0.45f * (float)Math.Sin(_wavePhase);
                float surfCutoff = 0.005f + 0.011f * waveEnv;
                _lpSurf += surfCutoff * (white - _lpSurf);

                float gustEnv = 0.65f + 0.35f * (float)Math.Sin(_gustPhase);
                _lpWind += 0.003f * (white - _lpWind);

                float sample = (_lpSurf * waveEnv * 0.75f + _lpWind * gustEnv * 0.16f) * gain;
                int baseIdx = f * channels;
                for (int c = 0; c < channels; c++)
                {
                    data[baseIdx + c] = sample;
                }
            }
        }
    }
}
