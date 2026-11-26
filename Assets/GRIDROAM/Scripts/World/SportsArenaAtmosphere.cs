using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Living neighborhood sports complex atmosphere & stadium feel manager:
    /// - Animates fluttering Halı Saha corner flags in the Istanbul breeze
    /// - Drives night-time stadium floodlight glow panels & soft volumetric light cones synced with DayNightCycle
    /// - Manages live sideline spectators seated on bleachers/benches who track the match ball, cheer,
    ///   jump/wave, and shout localized Turkish/English reactions on goals, 3-pointers, spikes, blocks & aces
    /// - Animates brief elastic net-ripple pulses when a soccer goal or basketball swish lands
    /// - Synthesizes crisp procedural basketball swish, rim-clank, and leather kick SFX + turf/sand/clay VFX.
    /// </summary>
    public sealed class SportsArenaAtmosphere : MonoBehaviour
    {
        public static SportsArenaAtmosphere Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private struct FlagEntry
        {
            public Transform Pennant;
            public Quaternion BaseLocalRot;
            public float Phase;
        }

        private struct SpectatorEntry
        {
            public ProceduralHumanoid Humanoid;
            public string Name;
            public string SportKind;
            public Transform WatchBall;
            public Vector3 CourtCenter;
            public bool Seated;
            public float CheerTimer;
            public float IdlePhase;
        }

        private struct NetPulseEntry
        {
            public Transform NetRoot;
            public Vector3 BaseScale;
            public Vector3 WorldCenter;
            public float Timer;
        }

        private readonly List<FlagEntry> _flags = new List<FlagEntry>(8);
        private readonly List<SpectatorEntry> _spectators = new List<SpectatorEntry>(12);
        private readonly List<NetPulseEntry> _nets = new List<NetPulseEntry>(8);
        private readonly List<GameObject> _nightCones = new List<GameObject>(16);
        private readonly List<Material> _floodLensMats = new List<Material>(8);

        private AudioSource _sfx;
        private AudioClip _swishClip;
        private AudioClip _rimClankClip;
        private AudioClip _leatherKickClip;

        private bool _lastNightState;
        private bool _nightInitialized;
        private float _lastBubbleTime = -10f;

        private static readonly string[] FootballCheersTr = { "GOOOOL!!", "HELAL BE!!", "DOKSANA GİTTİ!!", "ŞAHANE VURUŞ!!" };
        private static readonly string[] FootballCheersEn = { "GOAAAL!!", "WHAT A STRIKE!!", "TOP CORNER!!", "BRILLIANT!!" };
        private static readonly string[] BasketCheersTr = { "ÜÇLÜK GELDİ!!", "DELİKSİZ GEÇTİ!!", "HELAL OLSUN!!", "BÜYÜK ŞUT!!" };
        private static readonly string[] BasketCheersEn = { "SWISH!!", "DOWNTOWN THREE!!", "NOTHING BUT NET!!", "CLUTCH SHOT!!" };
        private static readonly string[] VolleyCheersTr = { "EFSANE SMAÇ!!", "DUVAR BLOK!!", "HARİKA SAYI!!", "BRAVO!!" };
        private static readonly string[] VolleyCheersEn = { "MONSTER SPIKE!!", "ROOF BLOCK!!", "WHAT A POINT!!", "GREAT PLAY!!" };
        private static readonly string[] TennisCheersTr = { "ACE!!", "HARİKA PUAN!!", "KÖŞEYE ATTI!!", "BRAVO DENİZ!!" };
        private static readonly string[] TennisCheersEn = { "ACE!!", "CLEAN WINNER!!", "RIGHT ON THE LINE!!", "BRAVO!!" };

        private void Awake()
        {
            Instance = this;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;

            int sr = AudioSettings.outputSampleRate;
            if (sr <= 0) sr = 44100;
            _swishClip = BuildSwishClip(sr);
            _rimClankClip = BuildRimClankClip(sr);
            _leatherKickClip = BuildLeatherKickClip(sr);
        }

        private void OnDestroy()
        {
            if (_swishClip != null) Destroy(_swishClip);
            if (_rimClankClip != null) Destroy(_rimClankClip);
            if (_leatherKickClip != null) Destroy(_leatherKickClip);
            if (Instance == this) Instance = null;
        }

        public void RegisterFlag(Transform pennant, float phase)
        {
            if (pennant == null) return;
            _flags.Add(new FlagEntry
            {
                Pennant = pennant,
                BaseLocalRot = pennant.localRotation,
                Phase = phase
            });
        }

        public void RegisterFloodlightVisual(Material lensMat, GameObject nightCone)
        {
            if (lensMat != null && !_floodLensMats.Contains(lensMat))
            {
                _floodLensMats.Add(lensMat);
            }
            if (nightCone != null)
            {
                _nightCones.Add(nightCone);
            }
        }

        public void RegisterNetTarget(Transform netRoot, Vector3 worldCenter)
        {
            if (netRoot == null) return;
            _nets.Add(new NetPulseEntry
            {
                NetRoot = netRoot,
                BaseScale = netRoot.localScale,
                WorldCenter = worldCenter,
                Timer = 0f
            });
        }

        public void RegisterSpectator(
            ProceduralHumanoid humanoid,
            string displayName,
            string sportKind,
            Transform watchBall,
            Vector3 courtCenter,
            bool seated,
            float? seatFloorY = null)
        {
            if (humanoid == null) return;
            humanoid.SetAutoRandomWave(false);
            if (seated)
            {
                humanoid.SetSitting(true, driving: false);
                if (seatFloorY.HasValue)
                {
                    humanoid.SetSeatContact(seatFloorY.Value, 1f);
                }
            }
            else
            {
                humanoid.SetLocomotion(0f, true, 0f, humanoid.transform.forward, sampleGround: false);
            }

            _spectators.Add(new SpectatorEntry
            {
                Humanoid = humanoid,
                Name = displayName,
                SportKind = sportKind,
                WatchBall = watchBall,
                CourtCenter = courtCenter,
                Seated = seated,
                CheerTimer = 0f,
                IdlePhase = Random.Range(0f, Mathf.PI * 2f)
            });
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float t = Time.time;

            // 1. Sync stadium floodlight intensity & night cones with DayNightCycle
            bool isNight = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
            if (!_nightInitialized || isNight != _lastNightState)
            {
                _nightInitialized = true;
                _lastNightState = isNight;
                ApplyFloodlightNightState(isNight);
            }

            Vector3 viewerPos = PlayerModeController.Instance != null
                ? PlayerModeController.Instance.ActiveWorldPosition
                : Vector3.zero;

            // 2. Flutter Halı Saha corner flags smoothly
            for (int i = 0; i < _flags.Count; i++)
            {
                FlagEntry f = _flags[i];
                if (f.Pennant == null) continue;
                if ((f.Pennant.position - viewerPos).sqrMagnitude > 75f * 75f) continue;

                float wave = Mathf.Sin(t * 4.4f + f.Phase) * 14f + Mathf.Sin(t * 9.2f + f.Phase * 1.7f) * 5f;
                float tilt = Mathf.Cos(t * 3.1f + f.Phase) * 4.5f;
                f.Pennant.localRotation = f.BaseLocalRot * Quaternion.Euler(tilt, wave, 0f);
            }

            // 3. Elastic net ripple decay after goals / hoop swishes
            for (int i = 0; i < _nets.Count; i++)
            {
                NetPulseEntry n = _nets[i];
                if (n.Timer <= 0f || n.NetRoot == null) continue;
                n.Timer -= dt;
                if (n.Timer <= 0f)
                {
                    n.Timer = 0f;
                    n.NetRoot.localScale = n.BaseScale;
                }
                else
                {
                    float u = n.Timer / 0.48f;
                    float ripple = Mathf.Sin(u * Mathf.PI * 5f) * 0.14f * u;
                    n.NetRoot.localScale = new Vector3(
                        n.BaseScale.x * (1f + ripple * 0.7f),
                        n.BaseScale.y * (1f - ripple * 0.55f),
                        n.BaseScale.z * (1f + ripple * 1.15f));
                }
                _nets[i] = n;
            }

            // 4. Sideline spectators: follow the ball & cheer on big plays (distance-culled)
            for (int i = 0; i < _spectators.Count; i++)
            {
                SpectatorEntry s = _spectators[i];
                if (s.Humanoid == null || !s.Humanoid.gameObject.activeInHierarchy) continue;

                float distSq = (s.Humanoid.transform.position - viewerPos).sqrMagnitude;
                if (distSq > 62f * 62f) continue;

                if (s.CheerTimer > 0f)
                {
                    s.CheerTimer -= dt;
                    _spectators[i] = s;
                }

                if (s.WatchBall != null)
                {
                    s.Humanoid.SetLookAtPoint(true, s.WatchBall.position + Vector3.up * 0.25f);
                }
            }
        }

        private void ApplyFloodlightNightState(bool isNight)
        {
            Color lensEmission = isNight
                ? new Color(2.4f, 2.25f, 1.85f)
                : new Color(0.28f, 0.28f, 0.25f);

            for (int i = 0; i < _floodLensMats.Count; i++)
            {
                Material m = _floodLensMats[i];
                if (m == null) continue;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", lensEmission);
            }

            for (int i = 0; i < _nightCones.Count; i++)
            {
                if (_nightCones[i] != null)
                {
                    _nightCones[i].SetActive(isNight);
                }
            }
        }

        // =====================================================================
        // PUBLIC MATCH REALISM HOOKS
        // =====================================================================

        public static void CelebrateEvent(Vector3 courtCenter, string sportKind, bool playerSide, bool bigPlay)
        {
            if (Instance == null) return;
            Instance.TriggerSpectatorCelebration(courtCenter, sportKind, playerSide, bigPlay);
        }

        public static void PulseNearestNet(Vector3 impactWorldPos, float maxDist = 4.5f)
        {
            if (Instance == null) return;
            float maxSq = maxDist * maxDist;
            int bestIdx = -1;
            float bestSq = maxSq;
            for (int i = 0; i < Instance._nets.Count; i++)
            {
                var n = Instance._nets[i];
                if (n.NetRoot == null) continue;
                float dSq = (n.WorldCenter - impactWorldPos).sqrMagnitude;
                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    bestIdx = i;
                }
            }
            if (bestIdx >= 0)
            {
                var n = Instance._nets[bestIdx];
                n.Timer = 0.48f;
                Instance._nets[bestIdx] = n;
            }
        }

        public static void PlayBasketballSwish(float vol = 0.56f)
        {
            if (Instance == null || Instance._sfx == null || Instance._swishClip == null) return;
            Instance._sfx.pitch = Random.Range(0.95f, 1.07f);
            Instance._sfx.PlayOneShot(Instance._swishClip, vol);
        }

        public static void PlayBasketballRimClank(float vol = 0.50f)
        {
            if (Instance == null || Instance._sfx == null || Instance._rimClankClip == null) return;
            Instance._sfx.pitch = Random.Range(0.92f, 1.08f);
            Instance._sfx.PlayOneShot(Instance._rimClankClip, vol);
        }

        public static void PlayLeatherKick(float vol = 0.54f)
        {
            if (Instance == null || Instance._sfx == null || Instance._leatherKickClip == null) return;
            Instance._sfx.pitch = Random.Range(0.90f, 1.08f);
            Instance._sfx.PlayOneShot(Instance._leatherKickClip, vol);
        }

        public static void SpawnTurfKickPuff(Vector3 worldPos, bool heavy = false)
        {
            GameFeelAudio.SpawnBurst(worldPos + Vector3.up * 0.08f, new Color(0.18f, 0.52f, 0.22f), count: heavy ? 10 : 5, spread: heavy ? 0.52f : 0.28f);
            GameFeelAudio.SpawnBurst(worldPos + Vector3.up * 0.05f, new Color(0.14f, 0.14f, 0.14f), count: heavy ? 7 : 3, spread: heavy ? 0.42f : 0.22f);
        }

        public static void SpawnSandSplash(Vector3 worldPos, bool heavy = false)
        {
            GameFeelAudio.SpawnBurst(worldPos + Vector3.up * 0.08f, new Color(0.88f, 0.76f, 0.52f), count: heavy ? 12 : 6, spread: heavy ? 0.58f : 0.30f);
        }

        public static void SpawnClayPuff(Vector3 worldPos)
        {
            GameFeelAudio.SpawnBurst(worldPos + Vector3.up * 0.06f, new Color(0.76f, 0.38f, 0.24f), count: 5, spread: 0.24f);
        }

        private void TriggerSpectatorCelebration(Vector3 courtCenter, string sportKind, bool playerSide, bool bigPlay)
        {
            bool en = LocalizationService.IsEnglish;
            string[] lines = sportKind switch
            {
                "football" => en ? FootballCheersEn : FootballCheersTr,
                "basketball" => en ? BasketCheersEn : BasketCheersTr,
                "volleyball" => en ? VolleyCheersEn : VolleyCheersTr,
                _ => en ? TennisCheersEn : TennisCheersTr
            };

            bool bubbleShown = false;
            bool allowBubble = Time.time - _lastBubbleTime >= 1.6f;

            for (int i = 0; i < _spectators.Count; i++)
            {
                SpectatorEntry s = _spectators[i];
                if (s.Humanoid == null || !s.Humanoid.gameObject.activeInHierarchy) continue;
                if (s.SportKind != sportKind && (s.CourtCenter - courtCenter).sqrMagnitude > 22f * 22f) continue;

                s.CheerTimer = bigPlay ? 1.85f : 1.15f;
                if (s.Seated)
                {
                    // Stay cleanly seated on the bleacher plank while fist-pumping overhead (never clip/stand inside planks!)
                    s.Humanoid.PlaySeatedCheer(bigPlay ? 1.75f : 1.05f);
                }
                else if (bigPlay)
                {
                    s.Humanoid.PlaySportAnim(SportAnimKind.VictoryJump, 0.78f);
                }
                else
                {
                    s.Humanoid.PlayWave();
                }

                if (allowBubble && !bubbleShown && (playerSide || bigPlay))
                {
                    bubbleShown = true;
                    _lastBubbleTime = Time.time;
                    string msg = lines[Random.Range(0, lines.Length)];
                    NpcSpeechBubbleManager.Say(s.Humanoid.transform, s.Name, msg, 2.2f, NpcBubbleMood.Friendly, 1.95f);
                }

                _spectators[i] = s;
            }
        }

        // =====================================================================
        // PROCEDURAL SPORTS REALISM AUDIO CLIPS
        // =====================================================================

        private static AudioClip BuildSwishClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.26f);
            float[] data = new float[len];
            System.Random rng = new System.Random(4109);
            float lp = 0f;
            float hpPrev = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float u = i / (float)len;
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Band-pass filtered crisp nylon net swish
                lp += 0.34f * (n - lp);
                float bp = lp - hpPrev * 0.65f;
                hpPrev = lp;
                float env = Mathf.Sin(Mathf.Clamp01(u * 1.15f) * Mathf.PI) * Mathf.Exp(-4.2f * t);
                float whip = Mathf.Sin(2f * Mathf.PI * (1450f - 620f * u) * t) * 0.14f * Mathf.Exp(-18f * t);
                data[i] = Mathf.Clamp((bp * 0.95f + whip) * env * 1.25f, -0.95f, 0.95f);
            }
            AudioClip clip = AudioClip.Create("SFX_BasketballSwish", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildRimClankClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.30f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float ring1 = Mathf.Sin(2f * Mathf.PI * 415f * t) * Mathf.Exp(-14f * t);
                float ring2 = Mathf.Sin(2f * Mathf.PI * 742f * t) * 0.55f * Mathf.Exp(-20f * t);
                float ring3 = Mathf.Sin(2f * Mathf.PI * 1260f * t) * 0.28f * Mathf.Exp(-32f * t);
                float thud = Mathf.Sin(2f * Mathf.PI * 135f * t) * 0.45f * Mathf.Exp(-26f * t);
                float atk = Mathf.Min(1f, i / 45f);
                data[i] = Mathf.Clamp((ring1 + ring2 + ring3 + thud) * atk * 0.68f, -0.95f, 0.95f);
            }
            AudioClip clip = AudioClip.Create("SFX_BasketballRimClank", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildLeatherKickClip(int sr)
        {
            int len = Mathf.RoundToInt(sr * 0.18f);
            float[] data = new float[len];
            System.Random rng = new System.Random(7731);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float freq = Mathf.Lerp(158f, 62f, Mathf.Clamp01(t / 0.12f));
                float punch = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-24f * t);
                float slap = ((float)rng.NextDouble() * 2f - 1f) * 0.30f * Mathf.Exp(-85f * t);
                float atk = Mathf.Min(1f, i / 35f);
                data[i] = Mathf.Clamp((punch * 0.85f + slap) * atk, -0.95f, 0.95f);
            }
            AudioClip clip = AudioClip.Create("SFX_LeatherSoccerKick", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
