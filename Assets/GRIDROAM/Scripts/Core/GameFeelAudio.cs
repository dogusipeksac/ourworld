using System;
using System.Collections;
using UnityEngine;

namespace OurWorld.Core
{
    /// <summary>
    /// Procedural sound-effects & visual juice manager following Unity craft-game-floors:
    /// synthesizes crisp PCM clips at runtime (with 5 ms fade envelopes, zero clicks, zero asset bloat)
    /// and spawns lightweight URP sparkle / water / gold burst feedback for every world interaction.
    /// </summary>
    public class GameFeelAudio : MonoBehaviour
    {
        public static GameFeelAudio Instance { get; private set; }
        public static event Action OnCarHornPlayed;

        private AudioSource _sfx2D;
        private AudioClip _coinClip;
        private AudioClip _milestoneClip;
        private AudioClip _uiClickClip;
        private AudioClip _waterClip;
        private AudioClip _catMeowClip;
        private AudioClip _carHornClip;
        private AudioClip _prayerChimeClip;
        private AudioClip _doorLatchClip;
        private Material _burstMat;
        private float _lastHornTime = -99f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public static GameFeelAudio EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_GameFeelAudio");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameFeelAudio>();
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

            _sfx2D = gameObject.AddComponent<AudioSource>();
            _sfx2D.spatialBlend = 0f;
            _sfx2D.playOnAwake = false;
            _sfx2D.loop = false;

            int sr = AudioSettings.outputSampleRate;
            if (sr <= 0) sr = 44100;

            _coinClip = BuildCoinClip(sr);
            _milestoneClip = BuildMilestoneClip(sr);
            _uiClickClip = BuildUiClickClip(sr);
            _waterClip = BuildWaterClip(sr);
            _catMeowClip = BuildCatMeowClip(sr);
            _carHornClip = BuildCarHornClip(sr);
            _prayerChimeClip = BuildPrayerChimeClip(sr);
            _doorLatchClip = BuildDoorLatchClip(sr);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Sprites/Default");
            if (urpLit != null)
            {
                _burstMat = new Material(urpLit);
                _burstMat.EnableKeyword("_EMISSION");
            }
        }

        private void OnDestroy()
        {
            if (_coinClip != null) Destroy(_coinClip);
            if (_milestoneClip != null) Destroy(_milestoneClip);
            if (_uiClickClip != null) Destroy(_uiClickClip);
            if (_waterClip != null) Destroy(_waterClip);
            if (_catMeowClip != null) Destroy(_catMeowClip);
            if (_carHornClip != null) Destroy(_carHornClip);
            if (_prayerChimeClip != null) Destroy(_prayerChimeClip);
            if (_doorLatchClip != null) Destroy(_doorLatchClip);
            if (_burstMat != null) Destroy(_burstMat);
            if (Instance == this) Instance = null;
        }

        public static void PlayCoinChime()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._coinClip, 0.34f, 0.94f, 1.07f);
        }

        public static void PlayMilestoneFanfare()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._milestoneClip, 0.36f, 0.98f, 1.02f);
        }

        public static void PlayUiClick()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._uiClickClip, 0.22f, 0.95f, 1.08f);
        }

        public static void PlayWaterSplash()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._waterClip, 0.30f, 0.90f, 1.12f);
        }

        public static void PlayCatMeow()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._catMeowClip, 0.36f, 0.88f, 1.16f);
        }

        public static bool PlayCarHorn()
        {
            GameFeelAudio inst = EnsureInstance();
            if (Time.time - inst._lastHornTime < 0.35f) return false;
            inst._lastHornTime = Time.time;
            inst.PlayOneShotPitched(inst._carHornClip, 0.42f, 0.97f, 1.03f);
            OnCarHornPlayed?.Invoke();
            return true;
        }

        public static void PlayPrayerChime()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._prayerChimeClip, 0.28f, 0.99f, 1.01f);
        }

        public static void PlayDoorLatch()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._doorLatchClip, 0.26f, 0.92f, 1.08f);
        }

        public static void SpawnBurst(Vector3 worldPos, Color color, int count = 10, float spread = 0.55f)
        {
            GameFeelAudio inst = EnsureInstance();
            inst.StartCoroutine(inst.BurstRoutine(worldPos, color, Mathf.Clamp(count, 4, 24), spread));
        }

        private void PlayOneShotPitched(AudioClip clip, float volume, float minPitch, float maxPitch)
        {
            if (_sfx2D == null || clip == null) return;
            _sfx2D.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            _sfx2D.PlayOneShot(clip, volume);
        }

        private IEnumerator BurstRoutine(Vector3 origin, Color color, int count, float spread)
        {
            Transform[] sparks = new Transform[count];
            Vector3[] velocities = new Vector3[count];
            float[] baseScales = new float[count];

            Material mat = _burstMat != null ? new Material(_burstMat) : null;
            if (mat != null)
            {
                if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
                if (mat.HasProperty(EmissionColorId)) mat.SetColor(EmissionColorId, color * 2.4f);
            }

            for (int i = 0; i < count; i++)
            {
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p.name = "FeelSpark";
                Collider c = p.GetComponent<Collider>();
                if (c != null) Destroy(c);
                Renderer r = p.GetComponent<Renderer>();
                if (r != null && mat != null) r.sharedMaterial = mat;

                float sc = UnityEngine.Random.Range(0.055f, 0.11f);
                baseScales[i] = sc;
                p.transform.position = origin + UnityEngine.Random.insideUnitSphere * (spread * 0.35f);
                p.transform.localScale = Vector3.one * sc;

                float angle = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.25f, 0.25f);
                float horiz = UnityEngine.Random.Range(spread * 0.9f, spread * 2.1f);
                velocities[i] = new Vector3(Mathf.Cos(angle) * horiz, UnityEngine.Random.Range(1.2f, 2.6f), Mathf.Sin(angle) * horiz);
                sparks[i] = p.transform;
            }

            const float duration = 0.65f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);
                float scaleFactor = Mathf.Sin(t * Mathf.PI);

                for (int i = 0; i < count; i++)
                {
                    if (sparks[i] == null) continue;
                    velocities[i].y -= 3.6f * dt;
                    sparks[i].position += velocities[i] * dt * (1f - easeOut * 0.45f);
                    sparks[i].localScale = Vector3.one * (baseScales[i] * Mathf.Max(0.01f, scaleFactor));
                }
                yield return null;
            }

            for (int i = 0; i < count; i++)
            {
                if (sparks[i] != null) Destroy(sparks[i].gameObject);
            }
            if (mat != null) Destroy(mat);
        }

        private static void ApplyFade(float[] data, int sr, float fadeMs = 5f)
        {
            int fadeSamples = Mathf.Min(data.Length / 4, Mathf.Max(8, Mathf.RoundToInt(sr * (fadeMs * 0.001f))));
            for (int i = 0; i < fadeSamples; i++)
            {
                float g = i / (float)fadeSamples;
                data[i] *= g;
                data[data.Length - 1 - i] *= g;
            }
        }

        private static AudioClip BuildCoinClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.16f);
            float[] data = new float[len];
            double phase = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float freq = t < 0.35f ? 1046.5f : 1568.0f;
                phase += (2.0 * Math.PI * freq) / sr;
                float env = Mathf.Exp(-t * 6.5f);
                data[i] = (float)(Math.Sin(phase) * 0.65 + Math.Sin(phase * 2.0) * 0.22) * env * 0.6f;
            }
            ApplyFade(data, sr);
            AudioClip clip = AudioClip.Create("SFX_CoinChime", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildMilestoneClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.48f);
            float[] data = new float[len];
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int n = 0; n < notes.Length; n++)
            {
                int start = Mathf.RoundToInt(sr * (n * 0.095f));
                int noteLen = Mathf.RoundToInt(sr * 0.22f);
                double ph = 0.0;
                for (int i = 0; i < noteLen && start + i < len; i++)
                {
                    float nt = i / (float)noteLen;
                    ph += (2.0 * Math.PI * notes[n]) / sr;
                    float env = Mathf.Sin(Mathf.Min(1f, nt * 8f) * Mathf.PI * 0.5f) * Mathf.Exp(-nt * 4.2f);
                    data[start + i] += (float)(Math.Sin(ph) * 0.6 + Math.Sin(ph * 2.0) * 0.18) * env * 0.35f;
                }
            }
            ApplyFade(data, sr);
            AudioClip clip = AudioClip.Create("SFX_MilestoneFanfare", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildUiClickClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.025f);
            float[] data = new float[len];
            System.Random rng = new System.Random(101);
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.35f * (white - lp);
                data[i] = lp * Mathf.Exp(-t * 12f) * 0.5f;
            }
            ApplyFade(data, sr, 2f);
            AudioClip clip = AudioClip.Create("SFX_UiClick", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildWaterClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.42f);
            float[] data = new float[len];
            System.Random rng = new System.Random(404);
            float lp = 0f;
            double bubPh = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.16f * (white - lp);
                float bubFreq = 460f + Mathf.Sin(t * Mathf.PI * 6f) * 180f;
                bubPh += (2.0 * Math.PI * bubFreq) / sr;
                float env = Mathf.Sin(t * Mathf.PI);
                data[i] = (lp * 0.45f + (float)Math.Sin(bubPh) * 0.14f) * env * 0.55f;
            }
            ApplyFade(data, sr);
            AudioClip clip = AudioClip.Create("SFX_WaterSplash", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildCatMeowClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.34f);
            float[] data = new float[len];
            double ph = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                // Gentle rising-then-falling feline meow contour (~620 -> 960 -> 540 Hz)
                float contour = t < 0.32f
                    ? Mathf.Lerp(620f, 960f, t / 0.32f)
                    : Mathf.Lerp(960f, 520f, (t - 0.32f) / 0.68f);
                ph += (2.0 * Math.PI * contour) / sr;
                float env = Mathf.Sin(Mathf.Pow(t, 0.72f) * Mathf.PI);
                float sample = (float)(Math.Sin(ph) * 0.58 + Math.Sin(ph * 2.0) * 0.26 + Math.Sin(ph * 3.0) * 0.10);
                data[i] = sample * env * 0.48f;
            }
            ApplyFade(data, sr, 8f);
            AudioClip clip = AudioClip.Create("SFX_CatMeow", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildCarHornClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.36f);
            float[] data = new float[len];
            double ph1 = 0.0;
            double ph2 = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                ph1 += (2.0 * Math.PI * 410.0) / sr;
                ph2 += (2.0 * Math.PI * 515.0) / sr;
                float wave = (float)(
                    Math.Sin(ph1) * 0.45 + Math.Sin(ph1 * 2.0) * 0.22 + Math.Sin(ph1 * 3.0) * 0.12 +
                    Math.Sin(ph2) * 0.42 + Math.Sin(ph2 * 2.0) * 0.18);
                float env = t < 0.08f ? t / 0.08f : (t > 0.82f ? (1f - t) / 0.18f : 1f);
                data[i] = Mathf.Clamp(wave * 0.72f, -0.95f, 0.95f) * env * 0.50f;
            }
            ApplyFade(data, sr, 6f);
            AudioClip clip = AudioClip.Create("SFX_CarHorn", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildPrayerChimeClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.85f);
            float[] data = new float[len];
            double p1 = 0.0, p2 = 0.0, p3 = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                p1 += (2.0 * Math.PI * 293.66) / sr; // D4
                p2 += (2.0 * Math.PI * 440.00) / sr; // A4
                p3 += (2.0 * Math.PI * 587.33) / sr; // D5
                float env = Mathf.Sin(Mathf.Min(1f, t * 5f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 2.8f);
                data[i] = (float)(Math.Sin(p1) * 0.42 + Math.Sin(p2) * 0.34 + Math.Sin(p3) * 0.18) * env * 0.42f;
            }
            ApplyFade(data, sr, 12f);
            AudioClip clip = AudioClip.Create("SFX_PrayerChime", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildDoorLatchClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.09f);
            float[] data = new float[len];
            double ph = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float freq = Mathf.Lerp(210f, 95f, t);
                ph += (2.0 * Math.PI * freq) / sr;
                data[i] = (float)Math.Sin(ph) * Mathf.Exp(-t * 14f) * 0.55f;
            }
            ApplyFade(data, sr, 3f);
            AudioClip clip = AudioClip.Create("SFX_DoorLatch", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
