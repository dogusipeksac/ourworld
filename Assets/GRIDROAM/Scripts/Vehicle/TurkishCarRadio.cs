using System;
using OurWorld.Core;
using UnityEngine;

namespace OurWorld.Vehicle
{
    /// <summary>
    /// Procedural Turkish Car Radio (R key while driving) + soft Hamidiye Atlas AVM Lounge Ambience:
    /// Synthesizes 3 distinct zero-asset musical radio stations live via OnAudioFilterRead:
    ///   0 = Kapalı (Off)
    ///   1 = 92.4 Sultanbeyli FM (Anadolu Hicaz Bağlama Lo-Fi Groove)
    ///   2 = 98.8 Boğaz Synth (Gece Sürüşü Retro Beat)
    ///   3 = 104.2 Mahalle Chill (Yavaş İstanbul Akorları)
    /// Also provides subtle warm jazz-lounge ambient chords while walking inside Atlas AVM!
    /// </summary>
    public class TurkishCarRadio : MonoBehaviour
    {
        private const string PrefMusicVolumeKey = "OurWorld_MusicVolume";
        public static TurkishCarRadio Instance { get; private set; }

        private static float _musicVolume = -1f;
        public static float MusicVolume
        {
            get
            {
                if (_musicVolume < 0f)
                {
                    _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefMusicVolumeKey, 0.85f));
                }
                return _musicVolume;
            }
        }

        public static void SetMusicVolume(float value)
        {
            _musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefMusicVolumeKey, _musicVolume);
            if (Instance != null)
            {
                Instance._musicVolScale = _musicVolume;
            }
        }

        private static readonly string[] StationNamesTr =
        {
            "Radyo Kapalı",
            "92.4 Sultanbeyli FM · Anadolu Hicaz",
            "98.8 Boğaz Synth · Gece Sürüşü",
            "104.2 Mahalle Chill · İstanbul Akorları"
        };

        private static readonly string[] StationNamesEn =
        {
            "Radio Off",
            "92.4 Sultanbeyli FM · Anatolian Hicaz",
            "98.8 Bosphorus Synth · Night Drive",
            "104.2 Mahalle Chill · Istanbul Chords"
        };

        // Hicaz makam scale frequencies (D4, Eb4, F#4, G4, A4, Bb4, C5, D5)
        private static readonly double[] HicazScale = { 293.66, 311.13, 369.99, 392.00, 440.00, 466.16, 523.25, 587.33 };
        private static readonly int[] HicazPattern = { 0, 2, 3, 4, 3, 2, 1, 0, 4, 5, 4, 3, 2, 3, 1, 0 };

        // Night synth bassline (A2, C3, D3, E3, G3)
        private static readonly double[] SynthBass = { 110.0, 110.0, 130.81, 146.83, 164.81, 146.83, 130.81, 98.0 };

        // Chill chord roots (A3, F3, C4, G3)
        private static readonly double[] ChillRoots = { 220.0, 174.61, 261.63, 196.0 };

        // Soft Mall Lounge progression (C4, A3, F3, G3)
        private static readonly double[] MallLoungeRoots = { 261.63, 220.0, 174.61, 196.0 };

        private AudioSource _source;
        private AudioClip _carrier;
        private volatile int _stationIndex;
        private volatile bool _isDrivingActive;
        private volatile bool _isMallAmbienceActive;
        private volatile float _musicVolScale = 0.85f;
        private volatile float _sampleRate = 44100f;

        // Audio-thread synth state
        private double _beatClock;
        private double _melPhase;
        private double _bassPhase;
        private double _padPhase1;
        private double _padPhase2;
        private float _smoothGain;

        public static TurkishCarRadio EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_TurkishCarRadio");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<TurkishCarRadio>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _musicVolScale = MusicVolume;

            int sr = AudioSettings.outputSampleRate;
            if (sr <= 0) sr = 44100;
            _sampleRate = sr;

            _source = gameObject.AddComponent<AudioSource>();
            _source.spatialBlend = 0f;
            _source.loop = true;
            _source.playOnAwake = true;
            _source.volume = 1f;
            _carrier = AudioClip.Create("TurkishRadioCarrier", sr, 1, sr, false);
            _source.clip = _carrier;
            _source.Play();
        }

        private void OnDestroy()
        {
            if (_carrier != null) Destroy(_carrier);
            if (Instance == this) Instance = null;
        }

        public static void SetDrivingActive(bool driving)
        {
            TurkishCarRadio inst = EnsureInstance();
            inst._isDrivingActive = driving;
        }

        public static void SetMallAmbienceActive(bool insideMall)
        {
            if (!insideMall && Instance == null) return;
            TurkishCarRadio inst = EnsureInstance();
            inst._isMallAmbienceActive = insideMall;
        }

        public static string CycleStation()
        {
            TurkishCarRadio inst = EnsureInstance();
            inst._stationIndex = (inst._stationIndex + 1) % StationNamesTr.Length;
            return LocalizationService.IsEnglish ? StationNamesEn[inst._stationIndex] : StationNamesTr[inst._stationIndex];
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null || data.Length == 0 || channels <= 0) return;

            int station = _stationIndex;
            bool playingCarRadio = _isDrivingActive && station > 0;
            bool playingMallLounge = !playingCarRadio && _isMallAmbienceActive;
            float vol = _musicVolScale;
            float targetGain = playingCarRadio ? (0.14f * vol) : (playingMallLounge ? (0.048f * vol) : 0f);
            float sr = _sampleRate < 8000f ? 44100f : _sampleRate;
            int frames = data.Length / channels;

            for (int f = 0; f < frames; f++)
            {
                _smoothGain += (targetGain - _smoothGain) * 0.0015f;
                if (_smoothGain < 0.0002f && targetGain <= 0f)
                {
                    int idx0 = f * channels;
                    for (int c = 0; c < channels; c++) data[idx0 + c] = 0f;
                    continue;
                }

                _beatClock += 1.0 / sr;
                float sample = 0f;

                if (playingMallLounge)
                {
                    // Gentle warm Hamidiye Atlas AVM atrium lounge chords
                    double barDur = 3.2;
                    int bar = (int)(_beatClock / barDur) % MallLoungeRoots.Length;
                    double barFrac = (_beatClock % barDur) / barDur;
                    double root = MallLoungeRoots[bar];
                    double third = (bar == 1) ? root * 1.1892 : root * 1.2599; // Am7 vs Cmaj7/Fmaj7/G6
                    double fifth = root * 1.4983;

                    _padPhase1 += (2.0 * Math.PI * root) / sr;
                    _padPhase2 += (2.0 * Math.PI * third) / sr;
                    _melPhase += (2.0 * Math.PI * fifth) / sr;
                    double bell = Math.Sin(_melPhase * 2.0) * Math.Exp(-barFrac * 3.8) * 0.14;
                    double swell = 0.50 + 0.50 * Math.Sin(barFrac * Math.PI);
                    sample = (float)((Math.Sin(_padPhase1) * 0.42 + Math.Sin(_padPhase2) * 0.30 + Math.Sin(_melPhase) * 0.24) * swell + bell);
                }
                else if (station == 1)
                {
                    // 92.4 Sultanbeyli FM: Anadolu Hicaz plucked baglama melody + warm drone
                    double stepDur = 0.28;
                    int step = (int)(_beatClock / stepDur) % HicazPattern.Length;
                    double stepFrac = (_beatClock % stepDur) / stepDur;
                    double freq = HicazScale[HicazPattern[step]];

                    _melPhase += (2.0 * Math.PI * freq) / sr;
                    _bassPhase += (2.0 * Math.PI * 146.83) / sr; // D3 drone
                    double pluckEnv = Math.Exp(-stepFrac * 5.2);
                    double mel = (Math.Sin(_melPhase) * 0.65 + Math.Sin(_melPhase * 2.0) * 0.28 + Math.Sin(_melPhase * 3.0) * 0.12) * pluckEnv;
                    double drone = Math.Sin(_bassPhase) * 0.22;
                    sample = (float)(mel * 0.62 + drone);
                }
                else if (station == 2)
                {
                    // 98.8 Boğaz Synth: Night cruising retro synth pulse
                    double stepDur = 0.24;
                    int step = (int)(_beatClock / stepDur) % SynthBass.Length;
                    double stepFrac = (_beatClock % stepDur) / stepDur;
                    double bFreq = SynthBass[step];

                    _bassPhase += (2.0 * Math.PI * bFreq) / sr;
                    _melPhase += (2.0 * Math.PI * (bFreq * 3.0)) / sr;
                    double sawApprox = Math.Sin(_bassPhase) * 0.6 + Math.Sin(_bassPhase * 2.0) * 0.3 + Math.Sin(_bassPhase * 3.0) * 0.15;
                    double pulseEnv = Math.Exp(-stepFrac * 3.6);
                    double kickEnv = (step % 2 == 0) ? Math.Exp(-stepFrac * 18.0) : 0.0;
                    sample = (float)(sawApprox * pulseEnv * 0.45 + Math.Sin(_melPhase) * 0.18 * (1.0 - stepFrac) + Math.Sin(_bassPhase * 0.5) * kickEnv * 0.55);
                }
                else if (station == 3)
                {
                    // 104.2 Mahalle Chill: Warm slow Istanbul evening Rhodes chords
                    double barDur = 2.4;
                    int bar = (int)(_beatClock / barDur) % ChillRoots.Length;
                    double barFrac = (_beatClock % barDur) / barDur;
                    double root = ChillRoots[bar];

                    _padPhase1 += (2.0 * Math.PI * root) / sr;
                    _padPhase2 += (2.0 * Math.PI * (root * 1.25)) / sr; // Major 3rd
                    _melPhase += (2.0 * Math.PI * (root * 1.5)) / sr;  // Perfect 5th
                    double swell = Math.Sin(barFrac * Math.PI);
                    sample = (float)((Math.Sin(_padPhase1) * 0.42 + Math.Sin(_padPhase2) * 0.32 + Math.Sin(_melPhase) * 0.26) * (0.45 + 0.55 * swell));
                }

                float outSample = sample * _smoothGain;
                int baseIdx = f * channels;
                for (int c = 0; c < channels; c++)
                {
                    data[baseIdx + c] = outSample;
                }
            }
        }
    }
}
