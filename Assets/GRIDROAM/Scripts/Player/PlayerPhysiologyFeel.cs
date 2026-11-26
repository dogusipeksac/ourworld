using System;
using OurWorld.Core;
using OurWorld.World;
using UnityEngine;

namespace OurWorld.Player
{
    /// <summary>
    /// Visceral human physiology feedback layer for Deniz:
    /// - Sprint exertion tracking with natural catching-breath exhales when stopping after sustained running
    /// - Low-health rhythmic heartbeat (lub-dub) accelerating below 34 HP + smooth crimson vignette pulse feed
    /// - Occasional subtle stomach grumble when hungry (< 28%) so needs are felt, not just read on a bar
    /// - Fatigue / low-HP sensory feed read cleanly by DayNightCycle's URP post-processing pipeline.
    /// </summary>
    public sealed class PlayerPhysiologyFeel : MonoBehaviour
    {
        public static PlayerPhysiologyFeel Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Exertion01 = 0f;
            LowHealthPulse01 = 0f;
            FatigueMood01 = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_PlayerPhysiologyFeel");
            go.AddComponent<PlayerPhysiologyFeel>();
        }

        public static float Exertion01 { get; private set; }
        public static float LowHealthPulse01 { get; private set; }
        public static float FatigueMood01 { get; private set; }

        private AudioSource _bodyAudio;
        private AudioClip _heartbeatClip;
        private AudioClip _exhaleClip;
        private AudioClip _stomachClip;

        private float _heartbeatTimer;
        private float _breathTimer = 1.2f;
        private float _stomachTimer = 24f;
        private int _recoveryBreathsPending;
        private bool _wasHeavySprinting;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _bodyAudio = gameObject.AddComponent<AudioSource>();
            _bodyAudio.spatialBlend = 0f;
            _bodyAudio.playOnAwake = false;
            _bodyAudio.loop = false;
            _bodyAudio.priority = 110;

            const int sr = 22050;
            _heartbeatClip = SynthHeartbeatClip(sr);
            _exhaleClip = SynthBreathExhaleClip(sr);
            _stomachClip = SynthStomachRumbleClip(sr);
        }

        private void OnDestroy()
        {
            if (_heartbeatClip != null) Destroy(_heartbeatClip);
            if (_exhaleClip != null) Destroy(_exhaleClip);
            if (_stomachClip != null) Destroy(_stomachClip);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (OurWorld.UI.MainMenuController.IsMenuActive) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            PlayerModeController mode = PlayerModeController.Instance;
            PlayerNeedsController needs = PlayerNeedsController.Instance;
            if (mode == null || needs == null || needs.IsKnockedOut)
            {
                LowHealthPulse01 = Mathf.MoveTowards(LowHealthPulse01, 0f, dt * 3f);
                FatigueMood01 = Mathf.MoveTowards(FatigueMood01, 0f, dt * 2f);
                return;
            }

            // 1. Sprint & Swim Exertion + Gentle Recovery Breathing ONLY after prolonged hard sprinting (never noisy while walking!)
            bool onFoot = mode.CurrentMode == ControlMode.OnFoot && mode.FootController != null;
            bool sprintingNow = onFoot && !mode.FootController.IsSwimming && mode.FootController.CurrentSpeed > 3.90f;
            bool swimmingHard = onFoot && mode.FootController.IsSwimming && mode.FootController.CurrentSpeed > 2.4f;
            bool underwater = onFoot && mode.FootController.IsUnderwater;

            if (sprintingNow || swimmingHard)
            {
                Exertion01 = Mathf.Min(1f, Exertion01 + dt * (sprintingNow ? 0.062f : 0.095f));
                if (Exertion01 >= 0.82f) _wasHeavySprinting = true;
            }
            else
            {
                if (_wasHeavySprinting && Exertion01 >= 0.76f)
                {
                    _recoveryBreathsPending = 1;
                    _breathTimer = Mathf.Min(_breathTimer, 0.45f);
                }
                _wasHeavySprinting = false;
                Exertion01 = Mathf.Max(0f, Exertion01 - dt * 0.18f);
            }

            if (!underwater && !sprintingNow && _recoveryBreathsPending > 0)
            {
                _breathTimer -= dt;
                if (_breathTimer <= 0f && _bodyAudio != null && _exhaleClip != null)
                {
                    _breathTimer = UnityEngine.Random.Range(1.8f, 2.3f);
                    float vol = Mathf.Lerp(0.020f, 0.038f, Exertion01);
                    _bodyAudio.pitch = UnityEngine.Random.Range(0.94f, 1.04f);
                    _bodyAudio.PlayOneShot(_exhaleClip, vol);
                    _recoveryBreathsPending--;
                }
            }

            // 2. Low-Health Heartbeat & Crimson Edge Pulse (< 34 HP)
            float hp = needs.Health;
            if (hp < 34f && hp > 0.5f)
            {
                float severity = Mathf.InverseLerp(34f, 6f, hp);
                float interval = Mathf.Lerp(0.92f, 0.52f, severity); // 65 BPM -> 115 BPM
                _heartbeatTimer -= dt;
                if (_heartbeatTimer <= 0f)
                {
                    _heartbeatTimer = interval;
                    LowHealthPulse01 = Mathf.Clamp01(0.45f + severity * 0.55f);
                    if (_bodyAudio != null && _heartbeatClip != null)
                    {
                        _bodyAudio.pitch = Mathf.Lerp(0.94f, 1.12f, severity);
                        _bodyAudio.PlayOneShot(_heartbeatClip, Mathf.Lerp(0.18f, 0.42f, severity));
                    }
                }
                else
                {
                    LowHealthPulse01 = Mathf.MoveTowards(LowHealthPulse01, severity * 0.28f, dt * 1.65f);
                }
            }
            else
            {
                LowHealthPulse01 = Mathf.MoveTowards(LowHealthPulse01, 0f, dt * 2.2f);
                _heartbeatTimer = 0.25f;
            }

            // 3. Subtle Fatigue / Exhaustion Sensory Tint Feed
            float tiredSeverity = needs.IsTired ? Mathf.InverseLerp(20f, 4f, needs.Energy) * 0.65f : 0f;
            float hpSeverity = hp < 34f ? Mathf.InverseLerp(34f, 8f, hp) : 0f;
            float targetFatigue = Mathf.Clamp01(Mathf.Max(tiredSeverity, hpSeverity));
            FatigueMood01 = Mathf.Lerp(FatigueMood01, targetFatigue, 1f - Mathf.Exp(-3.0f * dt));

            // 4. Hunger Stomach Rumble (< 28% Hunger)
            if (needs.IsHungry && !underwater)
            {
                _stomachTimer -= dt;
                if (_stomachTimer <= 0f && _bodyAudio != null && _stomachClip != null)
                {
                    _stomachTimer = needs.IsStarving ? UnityEngine.Random.Range(22f, 34f) : UnityEngine.Random.Range(34f, 52f);
                    _bodyAudio.pitch = UnityEngine.Random.Range(0.88f, 1.08f);
                    _bodyAudio.PlayOneShot(_stomachClip, needs.IsStarving ? 0.24f : 0.16f);
                }
            }
            else
            {
                _stomachTimer = Mathf.Min(_stomachTimer, 18f);
            }
        }

        // =====================================================================
        // PROCEDURAL HUMAN BODY AUDIO SYNTHESIS (5 ms fade, warm & subtle)
        // =====================================================================
        private static AudioClip SynthHeartbeatClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.42f);
            float[] d = new float[n];
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float s = 0f;
                // Lub (0.00 - 0.11s)
                if (t < 0.11f)
                {
                    float u = t / 0.11f;
                    float f = Mathf.Lerp(64f, 38f, u);
                    ph += (2.0 * Math.PI * f) / sr;
                    s = (float)Math.Sin(ph) * Mathf.Sin(u * Mathf.PI) * 0.92f;
                }
                // Dub (0.16 - 0.28s)
                else if (t >= 0.16f && t < 0.28f)
                {
                    float u = (t - 0.16f) / 0.12f;
                    float f = Mathf.Lerp(78f, 44f, u);
                    ph += (2.0 * Math.PI * f) / sr;
                    s = (float)Math.Sin(ph) * Mathf.Sin(u * Mathf.PI) * 0.68f;
                }
                d[i] = Mathf.Clamp(s, -1f, 1f);
            }
            ApplyEdgeFades(d, sr);
            AudioClip clip = AudioClip.Create("SFX_PlayerHeartbeat", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static AudioClip SynthBreathExhaleClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.48f);
            float[] d = new float[n];
            System.Random rng = new System.Random(6204);
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = Mathf.Sin(Mathf.Clamp01(u / 0.22f) * Mathf.PI * 0.5f) * Mathf.Exp(-Mathf.Max(0f, u - 0.18f) * 3.6f);
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += 0.16f * (w - lp1);
                lp2 += 0.045f * (lp1 - lp2);
                float breath = (lp1 - lp2 * 0.72f) * 1.55f;
                d[i] = Mathf.Clamp(breath * env * 0.68f, -1f, 1f);
            }
            ApplyEdgeFades(d, sr);
            AudioClip clip = AudioClip.Create("SFX_PlayerBreathExhale", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static AudioClip SynthStomachRumbleClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.62f);
            float[] d = new float[n];
            System.Random rng = new System.Random(7391);
            double ph = 0.0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = Mathf.Sin(u * Mathf.PI) * (0.65f + 0.35f * Mathf.Sin(u * 26f));
                float freq = Mathf.Lerp(92f, 54f, u) + Mathf.Sin(u * 38f) * 14f;
                ph += (2.0 * Math.PI * freq) / sr;
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.03f * (w - lp);
                float s = ((float)Math.Sin(ph) * 0.55f + lp * 0.85f) * env;
                d[i] = Mathf.Clamp(s * 0.72f, -1f, 1f);
            }
            ApplyEdgeFades(d, sr);
            AudioClip clip = AudioClip.Create("SFX_StomachRumble", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static void ApplyEdgeFades(float[] samples, int sr)
        {
            int fade = Mathf.Min(samples.Length / 4, Mathf.Max(1, Mathf.RoundToInt(sr * 0.005f)));
            for (int i = 0; i < fade; i++)
            {
                float g = i / (float)fade;
                samples[i] *= g;
                samples[samples.Length - 1 - i] *= g;
            }
        }
    }
}
