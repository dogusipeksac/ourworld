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
        private AudioClip _milkSquirtClip;
        private AudioClip _shearSnipClip;
        private AudioClip _eggPickupClip;
        private AudioClip _whistleClip;
        private AudioClip _crowdCheerClip;
        private AudioClip _goalpostClangClip;
        private AudioClip _volleyBlockClip;
        private AudioClip _slideWhooshClip;
        private AudioClip _gunPistolClip;
        private AudioClip _gunSuppressedClip;
        private AudioClip _gunShotgunClip;
        private AudioClip _gunCarbineClip;
        private AudioClip _gunSniperClip;
        private AudioClip _steelPlateClip;
        private AudioClip _clayShatterClip;
        private AudioClip _paperHitClip;
        private AudioClip _reloadClickClip;
        private AudioClip _emptyClickClip;
        private AudioClip _rangeBuzzerClip;
        private Coroutine _hitstopRoutine;
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
            _milkSquirtClip = BuildMilkSquirtClip(sr);
            _shearSnipClip = BuildShearSnipClip(sr);
            _eggPickupClip = BuildEggPickupClip(sr);
            _whistleClip = BuildRefereeWhistleClip(sr);
            _crowdCheerClip = BuildCrowdCheerClip(sr);
            _goalpostClangClip = BuildGoalpostClangClip(sr);
            _volleyBlockClip = BuildVolleyBlockClip(sr);
            _slideWhooshClip = BuildSlideWhooshClip(sr);
            _gunPistolClip = BuildGunshotClip(sr, 0.24f, 0.30f, 165f, 48f, 1.0f, 9101, "SFX_GunPistol");
            _gunSuppressedClip = BuildSuppressedShotClip(sr);
            _gunShotgunClip = BuildGunshotClip(sr, 0.44f, 0.45f, 120f, 34f, 1.35f, 9102, "SFX_GunShotgun");
            _gunCarbineClip = BuildGunshotClip(sr, 0.22f, 0.24f, 220f, 62f, 0.95f, 9103, "SFX_GunCarbine");
            _gunSniperClip = BuildGunshotClip(sr, 0.62f, 0.50f, 138f, 28f, 1.45f, 9104, "SFX_GunSniper");
            _steelPlateClip = BuildSteelPlatePingClip(sr);
            _clayShatterClip = BuildClayShatterClip(sr);
            _paperHitClip = BuildPaperHitClip(sr);
            _reloadClickClip = BuildReloadClickClip(sr);
            _emptyClickClip = BuildEmptyClickClip(sr);
            _rangeBuzzerClip = BuildRangeBuzzerClip(sr);

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
            if (_milkSquirtClip != null) Destroy(_milkSquirtClip);
            if (_shearSnipClip != null) Destroy(_shearSnipClip);
            if (_eggPickupClip != null) Destroy(_eggPickupClip);
            if (_whistleClip != null) Destroy(_whistleClip);
            if (_crowdCheerClip != null) Destroy(_crowdCheerClip);
            if (_goalpostClangClip != null) Destroy(_goalpostClangClip);
            if (_volleyBlockClip != null) Destroy(_volleyBlockClip);
            if (_slideWhooshClip != null) Destroy(_slideWhooshClip);
            if (_gunPistolClip != null) Destroy(_gunPistolClip);
            if (_gunSuppressedClip != null) Destroy(_gunSuppressedClip);
            if (_gunShotgunClip != null) Destroy(_gunShotgunClip);
            if (_gunCarbineClip != null) Destroy(_gunCarbineClip);
            if (_gunSniperClip != null) Destroy(_gunSniperClip);
            if (_steelPlateClip != null) Destroy(_steelPlateClip);
            if (_clayShatterClip != null) Destroy(_clayShatterClip);
            if (_paperHitClip != null) Destroy(_paperHitClip);
            if (_reloadClickClip != null) Destroy(_reloadClickClip);
            if (_emptyClickClip != null) Destroy(_emptyClickClip);
            if (_rangeBuzzerClip != null) Destroy(_rangeBuzzerClip);
            if (_hitstopRoutine != null) Time.timeScale = 1f;
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

        public static void PlayMilkSquirt(float fill01 = 0.5f)
        {
            GameFeelAudio inst = EnsureInstance();
            // Pail resonance pitch gently rises as the milk bucket fills up!
            float p = Mathf.Lerp(0.88f, 1.24f, Mathf.Clamp01(fill01));
            inst.PlayOneShotPitched(inst._milkSquirtClip, 0.34f, p * 0.96f, p * 1.04f);
        }

        public static void PlayShearSnip()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._shearSnipClip, 0.30f, 0.90f, 1.14f);
        }

        public static void PlayEggPickup()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._eggPickupClip, 0.28f, 0.94f, 1.08f);
        }

        public static void PlayRefereeWhistle(bool doubleBlast = false)
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._whistleClip, 0.28f, 0.98f, 1.03f);
            if (doubleBlast && inst.isActiveAndEnabled)
            {
                inst.StartCoroutine(inst.DelayedWhistleRoutine(0.24f));
            }
        }

        public static void PlayCrowdCheer(bool bigCheer = true)
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._crowdCheerClip, bigCheer ? 0.34f : 0.22f, bigCheer ? 0.96f : 1.04f, bigCheer ? 1.05f : 1.12f);
        }

        public static void PlayGoalpostClang()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._goalpostClangClip, 0.42f, 0.95f, 1.05f);
        }

        public static void PlayVolleyBlockThud()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._volleyBlockClip, 0.44f, 0.93f, 1.07f);
        }

        public static void PlaySlideWhoosh()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._slideWhooshClip, 0.34f, 0.92f, 1.08f);
        }

        public static void PlayGunshotPistol(bool suppressed = false)
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(suppressed ? inst._gunSuppressedClip : inst._gunPistolClip, suppressed ? 0.36f : 0.56f, 0.96f, 1.05f);
        }

        public static void PlayGunshotShotgun()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._gunShotgunClip, 0.66f, 0.94f, 1.04f);
        }

        public static void PlayGunshotCarbine(bool muzzleBrake = false)
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._gunCarbineClip, muzzleBrake ? 0.52f : 0.58f, 0.95f, 1.06f);
        }

        public static void PlayGunshotSniper()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._gunSniperClip, 0.70f, 0.96f, 1.03f);
        }

        public static void PlaySteelPlatePing(int combo = 1)
        {
            GameFeelAudio inst = EnsureInstance();
            float p = Mathf.Clamp(0.95f + (combo - 1) * 0.055f, 0.95f, 1.42f);
            inst.PlayOneShotPitched(inst._steelPlateClip, 0.52f, p, p + 0.03f);
        }

        public static void PlayClayShatter()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._clayShatterClip, 0.46f, 0.92f, 1.12f);
        }

        public static void PlayBulletImpactPaper(bool bullseye = false)
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._paperHitClip, 0.36f, 0.94f, 1.08f);
            if (bullseye) inst.PlayOneShotPitched(inst._coinClip, 0.34f, 1.16f, 1.24f);
        }

        public static void PlayReloadClick(bool complete = false)
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._reloadClickClip, 0.42f, complete ? 1.12f : 0.88f, complete ? 1.18f : 0.94f);
        }

        public static void PlayEmptyClick()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._emptyClickClip, 0.38f, 0.97f, 1.04f);
        }

        public static void PlayRangeBuzzer()
        {
            GameFeelAudio inst = EnsureInstance();
            inst.PlayOneShotPitched(inst._rangeBuzzerClip, 0.38f, 0.99f, 1.01f);
        }

        public static void TriggerHitstop(float unscaledSeconds = 0.042f)
        {
            GameFeelAudio inst = EnsureInstance();
            if (!inst.isActiveAndEnabled) return;
            if (inst._hitstopRoutine != null) inst.StopCoroutine(inst._hitstopRoutine);
            inst._hitstopRoutine = inst.StartCoroutine(inst.HitstopRoutine(Mathf.Clamp(unscaledSeconds, 0.015f, 0.075f)));
        }

        private IEnumerator DelayedWhistleRoutine(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            PlayOneShotPitched(_whistleClip, 0.26f, 1.02f, 1.07f);
        }

        private IEnumerator HitstopRoutine(float duration)
        {
            Time.timeScale = 0.08f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1f;
            _hitstopRoutine = null;
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

        private static AudioClip BuildMilkSquirtClip(int sr)
        {
            // Crisp liquid stream into metal pail: bandpass liquid hiss + soft tin resonance
            int len = Mathf.RoundToInt(sr * 0.19f);
            float[] data = new float[len];
            System.Random rng = new System.Random(6142);
            float lp = 0f;
            float hp = 0f;
            double ph = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.22f * (white - lp);
                hp += 0.06f * (lp - hp);
                float jet = (lp - hp) * 1.45f;

                float tinFreq = Mathf.Lerp(560f, 410f, t);
                ph += (2.0 * Math.PI * tinFreq) / sr;
                float tin = (float)Math.Sin(ph) * 0.22f * Mathf.Exp(-t * 7f);

                float env = Mathf.Sin(Mathf.Clamp01(t / 0.18f) * Mathf.PI * 0.5f) *
                            Mathf.Pow(1f - t, 0.85f);
                data[i] = Mathf.Clamp((jet * 0.65f + tin) * env * 0.62f, -0.95f, 0.95f);
            }
            ApplyFade(data, sr, 4f);
            AudioClip clip = AudioClip.Create("SFX_MilkSquirt", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildShearSnipClip(int sr)
        {
            // Authentic metallic double-blade sheep-shears "snip-snip" slide + crisp blade click
            int len = Mathf.RoundToInt(sr * 0.12f);
            float[] data = new float[len];
            System.Random rng = new System.Random(8391);
            float prev = 0f;
            double p1 = 0.0, p2 = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float high = white - prev * 0.72f;
                prev = white;

                p1 += (2.0 * Math.PI * Mathf.Lerp(2650f, 3400f, t)) / sr;
                p2 += (2.0 * Math.PI * 4280.0) / sr;
                float metal = (float)(Math.Sin(p1) * 0.28 + Math.Sin(p2) * 0.16);

                // Quick blade scrape followed by crisp tip-snap around t=0.65
                float scrapeEnv = Mathf.Exp(-t * 10f);
                float snapEnv = Mathf.Exp(-Mathf.Abs(t - 0.62f) * 42f);
                float s = (high * 0.55f * scrapeEnv + metal * (0.45f * scrapeEnv + 0.85f * snapEnv));
                data[i] = Mathf.Clamp(s * 0.58f, -0.95f, 0.95f);
            }
            ApplyFade(data, sr, 3f);
            AudioClip clip = AudioClip.Create("SFX_ShearSnip", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildEggPickupClip(int sr)
        {
            // Warm gentle organic pop/nestle when lifting an egg or placing it into straw basket
            int len = Mathf.RoundToInt(sr * 0.13f);
            float[] data = new float[len];
            double ph = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float freq = Mathf.Lerp(320f, 640f, Mathf.SmoothStep(0f, 1f, t));
                ph += (2.0 * Math.PI * freq) / sr;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.16f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 14f);
                data[i] = (float)Math.Sin(ph) * env * 0.48f;
            }
            ApplyFade(data, sr, 3f);
            AudioClip clip = AudioClip.Create("SFX_EggPickup", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildRefereeWhistleClip(int sr)
        {
            // Authentic sports pea-whistle with dual harmonic resonance & 36 Hz pea trill
            int len = Mathf.RoundToInt(sr * 0.28f);
            float[] data = new float[len];
            double p1 = 0.0, p2 = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float trill = 0.72f + 0.28f * Mathf.Sin(t * 2f * Mathf.PI * 10.5f);
                float f1 = 2740f + 95f * Mathf.Sin(t * 2f * Mathf.PI * 10.5f);
                float f2 = f1 * 1.062f;
                p1 += (2.0 * Math.PI * f1) / sr;
                p2 += (2.0 * Math.PI * f2) / sr;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.12f) * Mathf.PI * 0.5f) *
                            Mathf.Clamp01((1f - t) / 0.18f);
                data[i] = Mathf.Clamp(((float)Math.Sin(p1) * 0.52f + (float)Math.Sin(p2) * 0.26f) * trill * env * 0.56f, -0.95f, 0.95f);
            }
            ApplyFade(data, sr, 4f);
            AudioClip clip = AudioClip.Create("SFX_RefereeWhistle", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildCrowdCheerClip(int sr)
        {
            // Warm cheering neighborhood stadium swell + rhythmic clap shimmer
            int len = Mathf.RoundToInt(sr * 1.15f);
            float[] data = new float[len];
            System.Random rng = new System.Random(9421);
            float lp1 = 0f, lp2 = 0f, hp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += 0.18f * (white - lp1);
                lp2 += 0.06f * (lp1 - lp2);
                hp += 0.015f * (lp2 - hp);
                float voiceBand = (lp2 - hp) * 2.4f;
                float claps = (lp1 - lp2) * (0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * 26f)));
                float swell = Mathf.Sin(Mathf.Clamp01(t / 0.22f) * Mathf.PI * 0.5f) * Mathf.Pow(1f - t, 0.9f);
                data[i] = Mathf.Clamp((voiceBand * 0.65f + claps * 0.35f) * swell * 0.62f, -0.95f, 0.95f);
            }
            ApplyFade(data, sr, 8f);
            AudioClip clip = AudioClip.Create("SFX_CrowdCheer", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildGoalpostClangClip(int sr)
        {
            // Ringing metallic steel goalpost "DANG!" impact with inharmonic overtones
            int len = Mathf.RoundToInt(sr * 0.42f);
            float[] data = new float[len];
            double p1 = 0.0, p2 = 0.0, p3 = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                p1 += (2.0 * Math.PI * 525.0) / sr;
                p2 += (2.0 * Math.PI * 1190.0) / sr;
                p3 += (2.0 * Math.PI * 1945.0) / sr;
                float s = (float)(Math.Sin(p1) * 0.55 * Math.Exp(-t * 6.5) +
                                  Math.Sin(p2) * 0.38 * Math.Exp(-t * 9.5) +
                                  Math.Sin(p3) * 0.24 * Math.Exp(-t * 14.0));
                data[i] = Mathf.Clamp(s * 0.78f, -0.95f, 0.95f);
            }
            ApplyFade(data, sr, 2f);
            AudioClip clip = AudioClip.Create("SFX_GoalpostClang", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildVolleyBlockClip(int sr)
        {
            // Punchy two-hand roof block thud + tight palm slap
            int len = Mathf.RoundToInt(sr * 0.16f);
            float[] data = new float[len];
            System.Random rng = new System.Random(3347);
            float lp = 0f;
            double ph = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float freq = Mathf.Lerp(185f, 58f, t);
                ph += (2.0 * Math.PI * freq) / sr;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.35f * (white - lp);
                float body = (float)Math.Sin(ph) * Mathf.Exp(-t * 12f) * 0.78f;
                float slap = lp * Mathf.Exp(-t * 22f) * 0.55f;
                data[i] = Mathf.Clamp((body + slap) * 0.85f, -0.95f, 0.95f);
            }
            ApplyFade(data, sr, 2f);
            AudioClip clip = AudioClip.Create("SFX_VolleyBlock", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildSlideWhooshClip(int sr)
        {
            // Gritty turf / beach sand sliding whoosh for tackles and volleyball dive saves
            int len = Mathf.RoundToInt(sr * 0.34f);
            float[] data = new float[len];
            System.Random rng = new System.Random(7192);
            float lp = 0f, hp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += Mathf.Lerp(0.32f, 0.10f, t) * (white - lp);
                hp += 0.04f * (lp - hp);
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.15f) * Mathf.PI * 0.5f) * Mathf.Pow(1f - t, 1.2f);
                data[i] = Mathf.Clamp((lp - hp) * env * 1.15f, -0.95f, 0.95f);
            }
            ApplyFade(data, sr, 4f);
            AudioClip clip = AudioClip.Create("SFX_SlideWhoosh", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildGunshotClip(int sr, float duration, float tailMix, float punchStartHz, float punchEndHz, float gain, int seed, string clipName)
        {
            int len = Mathf.RoundToInt(sr * duration);
            float[] data = new float[len];
            System.Random rng = new System.Random(seed);
            float lp = 0f;
            float tailLp = 0f;
            double ph = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Crisp supersonic muzzle crack (first 12ms)
                float crackEnv = Mathf.Exp(-t * 48f);
                // Punchy chamber pressure thump
                float freq = Mathf.Lerp(punchStartHz, punchEndHz, Mathf.Pow(t, 0.45f));
                ph += (2.0 * Math.PI * freq) / sr;
                float punch = (float)Math.Sin(ph) * Mathf.Exp(-t * 16f) * 0.72f;
                // Mid blast body + outdoor range echo tail
                lp += 0.24f * (white - lp);
                tailLp += 0.06f * (white - tailLp);
                float body = lp * Mathf.Exp(-t * 11f) * 0.78f;
                float echo = tailLp * Mathf.Exp(-t * 4.2f) * tailMix;
                float raw = (white * crackEnv * 0.95f + punch + body + echo) * gain;
                data[i] = Mathf.Clamp((float)Math.Tanh(raw * 1.35f) * 0.92f, -0.96f, 0.96f);
            }
            ApplyFade(data, sr, 2f);
            AudioClip clip = AudioClip.Create(clipName, len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildSuppressedShotClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.14f);
            float[] data = new float[len];
            System.Random rng = new System.Random(9110);
            float lp = 0f;
            double ph = 0.0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.14f * (white - lp);
                float freq = Mathf.Lerp(320f, 110f, t);
                ph += (2.0 * Math.PI * freq) / sr;
                float puff = (white - lp) * Mathf.Exp(-t * 28f) * 0.55f;
                float bolt = (float)Math.Sin(ph) * Mathf.Exp(-t * 24f) * 0.45f;
                data[i] = Mathf.Clamp((puff + bolt) * 0.82f, -0.9f, 0.9f);
            }
            ApplyFade(data, sr, 2f);
            AudioClip clip = AudioClip.Create("SFX_GunSuppressed", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildSteelPlatePingClip(int sr)
        {
            // Bright metallic AR500 IPSC steel target "TINK!" ring
            int len = Mathf.RoundToInt(sr * 0.48f);
            float[] data = new float[len];
            System.Random rng = new System.Random(9120);
            double p1 = 0, p2 = 0, p3 = 0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                p1 += (2.0 * Math.PI * 1480.0) / sr;
                p2 += (2.0 * Math.PI * 2890.0) / sr;
                p3 += (2.0 * Math.PI * 4120.0) / sr;
                float impact = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 90f) * 0.55f;
                float ring = ((float)Math.Sin(p1) * 0.48f + (float)Math.Sin(p2) * 0.34f + (float)Math.Sin(p3) * 0.18f) * Mathf.Exp(-t * 7.2f);
                data[i] = Mathf.Clamp((impact + ring) * 0.85f, -0.95f, 0.95f);
            }
            ApplyFade(data, sr, 2f);
            AudioClip clip = AudioClip.Create("SFX_SteelPlatePing", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildClayShatterClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.28f);
            float[] data = new float[len];
            System.Random rng = new System.Random(9130);
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.35f * (white - lp);
                float crackle = ((i % 190) < 35 ? 1.3f : 0.6f) * (white - lp);
                float env = Mathf.Exp(-t * 11f);
                data[i] = Mathf.Clamp(crackle * env * 0.85f, -0.92f, 0.92f);
            }
            ApplyFade(data, sr, 2f);
            AudioClip clip = AudioClip.Create("SFX_ClayShatter", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildPaperHitClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.09f);
            float[] data = new float[len];
            System.Random rng = new System.Random(9140);
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.25f * (white - lp);
                data[i] = Mathf.Clamp(lp * Mathf.Exp(-t * 32f) * 0.9f, -0.9f, 0.9f);
            }
            ApplyFade(data, sr, 2f);
            AudioClip clip = AudioClip.Create("SFX_PaperHit", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildReloadClickClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.14f);
            float[] data = new float[len];
            System.Random rng = new System.Random(9150);
            double ph = 0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float pulse1 = Mathf.Exp(-Mathf.Abs(t - 0.12f) * 90f);
                float pulse2 = Mathf.Exp(-Mathf.Abs(t - 0.56f) * 75f);
                ph += (2.0 * Math.PI * (t < 0.4f ? 1150.0 : 1680.0)) / sr;
                float metallic = (float)Math.Sin(ph) * 0.5f + (float)(rng.NextDouble() * 2.0 - 1.0) * 0.35f;
                data[i] = Mathf.Clamp(metallic * (pulse1 + pulse2) * 0.72f, -0.9f, 0.9f);
            }
            ApplyFade(data, sr, 2f);
            AudioClip clip = AudioClip.Create("SFX_ReloadClick", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildEmptyClickClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.06f);
            float[] data = new float[len];
            double ph = 0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                ph += (2.0 * Math.PI * 2400.0) / sr;
                data[i] = Mathf.Clamp((float)Math.Sin(ph) * Mathf.Exp(-t * 55f) * 0.55f, -0.8f, 0.8f);
            }
            ApplyFade(data, sr, 1f);
            AudioClip clip = AudioClip.Create("SFX_EmptyClick", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildRangeBuzzerClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.32f);
            float[] data = new float[len];
            double ph = 0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                ph += (2.0 * Math.PI * 1920.0) / sr;
                float sq = Math.Sin(ph) >= 0 ? 0.55f : -0.55f;
                float env = t < 0.85f ? 1f : Mathf.InverseLerp(1f, 0.85f, t);
                data[i] = (sq * 0.45f + (float)Math.Sin(ph) * 0.45f) * env * 0.52f;
            }
            ApplyFade(data, sr, 4f);
            AudioClip clip = AudioClip.Create("SFX_RangeBuzzer", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
