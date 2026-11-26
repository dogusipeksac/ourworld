using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.UI;
using OurWorld.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace OurWorld.World
{
    /// <summary>
    /// GTA 6-grade 1-to-5 Star Wanted Level & Live Turkish Police / Zabıta Pursuit System:
    /// - Escalating 1..5 Star Wanted HUD (top-right flashing Red/Blue stars + Evading progress bar).
    /// - Real pursuing Turkish Police Interceptor Cruisers (155 POLİS white-and-blue livery, animated
    ///   flashing Red/Blue roof strobe lightbars, procedural Doppler-scaled two-tone European sirens,
    ///   obstacle-aware road chase AI, stuck-reverse recovery & PIT intercept maneuvers).
    /// - Active Foot Police Officers deploying from cruisers to chase, fight, or BUST ("YAKALANDIN!")
    ///   on-foot suspects (who can also be dodged or knocked out in fair combat!).
    /// - Stealth & Line-of-Sight Evasion (crouching/hiding behind buildings breaks sight -> Evading timer -> Escaped!).
    /// - Pay'n'Spray instant star clearing when repainting your vehicle at No:73 Oto Tamir & Modifiye!
    /// </summary>
    public sealed class PoliceWantedSystem : MonoBehaviour
    {
        /// <summary>
        /// Master switch for the Police Wanted pursuit system. Currently disabled per user preference
        /// so street combat focuses cleanly on knocking out NPCs ("Bayıltma") without police chases.
        /// Flip to true anytime to re-enable the full 1-5 Star Turkish Police Interceptor system.
        /// </summary>
        public static readonly bool PoliceSystemEnabled = false;

        public static PoliceWantedSystem Instance { get; private set; }

        private sealed class PoliceCruiser
        {
            public GameObject Root;
            public Rigidbody Rb;
            public Transform[] Wheels;
            public Renderer RedStrobe;
            public Renderer BlueStrobe;
            public Light StrobeLight;
            public AudioSource SirenSource;
            public float Speed;
            public float StuckTimer;
            public float ReverseTimer;
            public float RamCooldown;
            public Vector3 LastPos;
            public bool OfficerDeployed;
        }

        private sealed class PoliceOfficer
        {
            public GameObject Root;
            public Rigidbody Rb;
            public ProceduralHumanoid Humanoid;
            public float Health;
            public float HitCooldown;
            public float KnockdownTimer;
            public float PunchCooldown;
            public float PendingPunchTimer;
            public Vector3 StumbleVel;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private readonly List<PoliceCruiser> _cruisers = new List<PoliceCruiser>(4);
        private readonly List<PoliceOfficer> _officers = new List<PoliceOfficer>(4);
        private readonly List<Vector3> _radarBlips = new List<Vector3>(8);
        private readonly List<Material> _ownedMats = new List<Material>(16);

        private Material _bodyWhiteMat;
        private Material _stripeBlueMat;
        private Material _glassDarkMat;
        private Material _wheelDarkMat;
        private Material _redLitMat;
        private Material _redDimMat;
        private Material _blueLitMat;
        private Material _blueDimMat;
        private Material _officerUniformMat;
        private Material _goldBadgeMat;
        private AudioClip _sirenClip;

        // Wanted State
        private int _wantedLevel;
        private float _heatAccum;
        private float _evadeTimer;
        private float _evadeDuration = 15f;
        private bool _inPoliceSight;
        private float _bustProgress;
        private bool _isBustingRoutine;
        private float _spawnCheckTimer;
        private float _strobeClock;
        private float _sightCheckTimer;

        // Top-Right GTA 6 Wanted HUD
        private GameObject _hudRoot;
        private Text[] _starTexts = new Text[5];
        private Text _statusBadgeText;
        private Image _evadeBarFill;
        private GameObject _evadeBarBg;
        private GameObject _bustBarBg;
        private Image _bustBarFill;
        private Text _bustLabel;
        private GameObject _bustedBanner;
        private Text _bustedBannerTitle;
        private Text _bustedBannerSub;
        private CanvasGroup _bustedBannerGroup;

        public int WantedLevel => _wantedLevel;
        public bool IsWanted => _wantedLevel > 0;
        public bool IsEvading => _wantedLevel > 0 && !_inPoliceSight;
        public bool IsInPoliceSight => _wantedLevel > 0 && _inPoliceSight;
        public bool IsBusting => _isBustingRoutine;
        public bool WasLastClearByBusted { get; private set; }
        public IReadOnlyList<Vector3> ActivePolicePositions => _radarBlips;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!PoliceSystemEnabled) return;
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;

            GameObject go = new GameObject("OurWorld_PoliceWantedSystem");
            go.AddComponent<PoliceWantedSystem>();
        }

        public static PoliceWantedSystem EnsureInstance()
        {
            if (!PoliceSystemEnabled) return null;
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_PoliceWantedSystem");
            return go.AddComponent<PoliceWantedSystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildMaterialsAndAudio();
        }

        private IEnumerator Start()
        {
            yield return null;
            OurWorldUiKit.EnsureInitialized();
            BuildWantedHud();
        }

        private void OnDestroy()
        {
            DespawnAllUnits();
            if (_sirenClip != null) Destroy(_sirenClip);
            for (int i = 0; i < _ownedMats.Count; i++)
            {
                if (_ownedMats[i] != null) Destroy(_ownedMats[i]);
            }
            _ownedMats.Clear();
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        // PUBLIC API (OFFENSE HEAT, PAY'N'SPRAY CLEAR, OFFICER COMBAT, BUST)
        // =====================================================================
        public void AddCrimeHeat(float heatDelta, Vector3 crimePos, string reasonLabel = null)
        {
            if (!PoliceSystemEnabled || _isBustingRoutine) return;
            _heatAccum = Mathf.Clamp(_heatAccum + Mathf.Max(0.2f, heatDelta), 0f, 16f);

            int targetStars = _heatAccum >= 11.5f ? 5
                : _heatAccum >= 8.2f ? 4
                : _heatAccum >= 5.2f ? 3
                : _heatAccum >= 2.8f ? 2
                : (_heatAccum >= 1.15f ? 1 : 0);

            if (targetStars > _wantedLevel)
            {
                SetWantedLevel(targetStars, announce: true);
            }
            else if (_wantedLevel > 0)
            {
                // Committing another crime while wanted immediately refreshes police lock!
                _inPoliceSight = true;
                _evadeTimer = 0f;
            }
        }

        public void SetWantedLevel(int stars, bool announce = true)
        {
            if (!PoliceSystemEnabled) stars = 0;
            stars = Mathf.Clamp(stars, 0, 5);
            int prev = _wantedLevel;
            _wantedLevel = stars;

            if (stars <= 0)
            {
                _heatAccum = 0f;
                _evadeTimer = 0f;
                _bustProgress = 0f;
                _inPoliceSight = false;
                DespawnAllUnits();
                RefreshHudVisibility();
                return;
            }

            _evadeDuration = 12f + stars * 2.8f;
            _evadeTimer = 0f;
            _inPoliceSight = true;
            WasLastClearByBusted = false;

            if (announce && stars > prev)
            {
                bool en = LocalizationService.IsEnglish;
                string starStr = new string('★', stars);
                NeighborhoodConsequenceSystem.EnsureInstance().PlayZabitaWhistle(0.68f);
                PlayerModeController.Instance?.RaiseStatusMessage(en
                    ? $"🚨 WANTED LEVEL {starStr} — Police patrol units are pursuing! Hide or respray your car!"
                    : $"🚨 ARANMA SEVİYESİ {starStr} — Polis devriyesi peşinde! Saklan veya garajda arabanı boyat!");
            }

            RefreshHudVisibility();
        }

        public void ClearWantedFromRespray()
        {
            if (_wantedLevel <= 0) return;
            int oldStars = _wantedLevel;
            SetWantedLevel(0, announce: false);
            NeighborhoodConsequenceSystem.CoolDownHeatOnRecovery();
            GameFeelAudio.PlayMilestoneFanfare();
            bool en = LocalizationService.IsEnglish;
            PlayerModeController.Instance?.RaiseStatusMessage(en
                ? $"✨ Pay'n'Spray! Fresh coat of paint cleared your {oldStars}-Star Wanted Level!"
                : $"✨ Araç boyandı! {oldStars} yıldızlı polis araması tamamen silindi!");
        }

        public void ClearWantedQuietly()
        {
            SetWantedLevel(0, announce: false);
        }

        public bool TriggerBustedIfWanted()
        {
            if (_wantedLevel <= 0 || _isBustingRoutine) return false;
            StartCoroutine(BustedSequenceRoutine());
            return true;
        }

        public bool TryReceivePlayerPunch(Vector3 strikeCenter, float radius, Vector3 attackerPos, Vector3 punchDir, int comboStep)
        {
            if (_officers.Count == 0) return false;
            PoliceOfficer best = null;
            float maxR = radius + 0.58f;
            float bestSq = maxR * maxR;

            for (int i = 0; i < _officers.Count; i++)
            {
                PoliceOfficer o = _officers[i];
                if (o == null || o.Root == null || o.HitCooldown > 0f || o.KnockdownTimer > 0f) continue;
                Vector3 d = (o.Root.transform.position + Vector3.up * 1.0f) - strikeCenter;
                if (Mathf.Abs(d.y) > 1.8f) continue;
                float sq = d.sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = o;
                }
            }

            if (best == null) return false;

            Vector3 knockDir = punchDir;
            knockDir.y = 0f;
            if (knockDir.sqrMagnitude < 0.01f) knockDir = best.Root.transform.position - attackerPos;
            knockDir.y = 0f;
            knockDir = knockDir.sqrMagnitude > 0.001f ? knockDir.normalized : Vector3.forward;

            float dmg = comboStep >= 2 ? 50f : (comboStep == 1 ? 35f : 30f);
            best.Health = Mathf.Max(0f, best.Health - dmg);
            bool en = LocalizationService.IsEnglish;

            // Fighting back resets bust progress & adds heat!
            _bustProgress = Mathf.Max(0f, _bustProgress - 0.55f);
            AddCrimeHeat(1.4f, best.Root.transform.position, "Officer");

            if (best.Health <= 0f)
            {
                best.KnockdownTimer = 18.0f;
                best.HitCooldown = 1.2f;
                best.PendingPunchTimer = 0f;
                best.StumbleVel = knockDir * 7.6f;
                best.Root.transform.rotation = Quaternion.LookRotation(-knockDir, Vector3.up);
                best.Humanoid?.PlayKnockdown(18.0f);

                VisualEffectsManager.SpawnFloatingText(
                    best.Root.transform.position + Vector3.up * 1.65f,
                    en ? "💫 OFFICER DOWN!" : "💫 POLİS BAYILDI!",
                    new Color(1.0f, 0.86f, 0.22f), scale: 1.18f, duration: 1.4f);
                GameFeelAudio.SpawnBurst(best.Root.transform.position + Vector3.up * 1.25f, new Color(1.0f, 0.88f, 0.26f), count: 14, spread: 0.65f);
                GameFeelAudio.TriggerHitstop(0.068f, slowScale: 0.18f);
                NpcSpeechBubbleManager.Say(best.Root.transform, en ? "Police Officer" : "Polis Memuru",
                    en ? "😵 Officer down... send backup!" : "😵 Merkez... destek ekip gönderin...", 3.2f, NpcBubbleMood.Alert, 2.08f);
            }
            else
            {
                best.HitCooldown = 0.24f;
                best.StumbleVel = knockDir * 4.2f;
                best.Humanoid?.PlayHitReact();
                VisualEffectsManager.SpawnFloatingText(
                    best.Root.transform.position + Vector3.up * 1.55f,
                    $"-{Mathf.RoundToInt(dmg)}",
                    new Color(1.0f, 0.65f, 0.24f), scale: 0.90f, duration: 0.65f);
                NpcSpeechBubbleManager.Say(best.Root.transform, en ? "Police Officer" : "Polis Memuru",
                    en ? "Resisting arrest! Back down!" : "Polise mukavemet! Teslim ol!", 2.5f, NpcBubbleMood.Alert, 2.08f);
            }

            return true;
        }

        // =====================================================================
        // UPDATE LOOP — PURSUIT AI, SIRENS, EVASION, BUSTING & STAR HUD
        // =====================================================================
        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_wantedLevel <= 0 || _isBustingRoutine)
            {
                if (_radarBlips.Count > 0) _radarBlips.Clear();
                return;
            }

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsModalLocked || MainMenuController.IsMenuActive)
            {
                // Mute active cruiser sirens cleanly during dialogue, phone, or menu pause!
                for (int i = 0; i < _cruisers.Count; i++)
                {
                    if (_cruisers[i]?.SirenSource != null) _cruisers[i].SirenSource.volume = 0f;
                }
                return;
            }

            Vector3 playerPos = mode.ActiveWorldPosition;
            bool playerDriving = mode.IsDriving;
            bool playerCrouching = !playerDriving && mode.FootController != null && mode.FootController.IsCrouching;

            _strobeClock += dt * 7.5f;
            bool redPhase = Mathf.FloorToInt(_strobeClock) % 2 == 0;

            // 1. Maintain target number of cruisers based on Wanted Stars & graphics tier
            _spawnCheckTimer -= dt;
            if (_spawnCheckTimer <= 0f)
            {
                _spawnCheckTimer = 2.4f;
                bool lowTier = GraphicsProfileManager.Instance != null &&
                               GraphicsProfileManager.Instance.CurrentTier == OurWorld.Core.GraphicsTier.Low;
                int desiredCruisers = _wantedLevel >= 4 ? (lowTier ? 2 : 3) : (_wantedLevel >= 2 ? 2 : 1);
                if (_cruisers.Count < desiredCruisers)
                {
                    TrySpawnPoliceCruiser(playerPos);
                }
            }

            // 2. Update cruisers & officers + rebuild radar blips
            _radarBlips.Clear();
            UpdateCruisers(dt, playerPos, playerDriving, redPhase);
            UpdateOfficers(dt, playerPos, playerDriving);

            // 3. Evaluate Line-of-Sight & Evasion!
            _sightCheckTimer -= dt;
            if (_sightCheckTimer <= 0f)
            {
                _sightCheckTimer = 0.45f;
                float detectDist = playerCrouching ? 24f : (playerDriving ? 56f : 44f);
                _inPoliceSight = CheckAnyPoliceSeesPlayer(playerPos, detectDist);
            }

            if (_inPoliceSight)
            {
                _evadeTimer = Mathf.Max(0f, _evadeTimer - dt * 2.2f);
            }
            else
            {
                _evadeTimer += dt;
                if (_evadeTimer >= _evadeDuration)
                {
                    CompleteEscape();
                    return;
                }
            }

            // 4. Update HUD Star Flash & Evasion / Bust Bars
            UpdateHudAnimations(redPhase);
        }

        private void CompleteEscape()
        {
            int escapedStars = _wantedLevel;
            SetWantedLevel(0, announce: false);
            NeighborhoodConsequenceSystem.CoolDownHeatOnRecovery();
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                SaveManager.Instance.Data.pursuitsEscapedCount++;
                SaveManager.Instance.Save();
            }
            int bonusRep = 20 + escapedStars * 15;
            NeighborhoodReputation.AddReputation(bonusRep);
            GameFeelAudio.PlayMilestoneFanfare();

            bool en = LocalizationService.IsEnglish;
            PlayerModeController.Instance?.RaiseStatusMessage(en
                ? $"🕶️ ESCAPED {escapedStars}-Star Police Pursuit! (+{bonusRep} Street Rep)"
                : $"🕶️ {escapedStars} Yıldızlı Polis Takibinden Kurtuldun! (+{bonusRep} Sokak Saygınlığı)");
        }

        private bool CheckAnyPoliceSeesPlayer(Vector3 playerPos, float maxDist)
        {
            Vector3 playerEye = playerPos + Vector3.up * 1.3f;
            float maxDistSq = maxDist * maxDist;

            for (int i = 0; i < _cruisers.Count; i++)
            {
                PoliceCruiser c = _cruisers[i];
                if (c == null || c.Root == null) continue;
                Vector3 cPos = c.Root.transform.position;
                Vector3 d = playerPos - cPos;
                d.y = 0f;
                if (d.sqrMagnitude > maxDistSq) continue;
                if (!Physics.Linecast(cPos + Vector3.up * 1.4f, playerEye, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                    hit.transform.IsChildOf(c.Root.transform) ||
                    (PlayerModeController.Instance?.FootController != null && hit.transform.IsChildOf(PlayerModeController.Instance.FootController.transform)) ||
                    (PlayerModeController.Instance?.CurrentVehicle != null && hit.transform.IsChildOf(PlayerModeController.Instance.CurrentVehicle.transform)))
                {
                    return true;
                }
            }

            for (int i = 0; i < _officers.Count; i++)
            {
                PoliceOfficer o = _officers[i];
                if (o == null || o.Root == null || o.KnockdownTimer > 0f) continue;
                Vector3 oPos = o.Root.transform.position;
                Vector3 d = playerPos - oPos;
                d.y = 0f;
                if (d.sqrMagnitude > maxDistSq) continue;
                if (!Physics.Linecast(oPos + Vector3.up * 1.45f, playerEye, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                    hit.transform.IsChildOf(o.Root.transform) ||
                    (PlayerModeController.Instance?.FootController != null && hit.transform.IsChildOf(PlayerModeController.Instance.FootController.transform)))
                {
                    return true;
                }
            }

            return false;
        }

        // =====================================================================
        // PURSUING POLICE CRUISERS (TURKISH 155 POLİS INTERCEPTORS)
        // =====================================================================
        private void UpdateCruisers(float dt, Vector3 playerPos, bool playerDriving, bool redPhase)
        {
            float maxSpeed = 14.5f + _wantedLevel * 2.4f;

            for (int i = _cruisers.Count - 1; i >= 0; i--)
            {
                PoliceCruiser c = _cruisers[i];
                if (c == null || c.Root == null)
                {
                    _cruisers.RemoveAt(i);
                    continue;
                }

                Transform tr = c.Root.transform;
                Vector3 pos = tr.position;
                _radarBlips.Add(pos);

                // Animate flashing Red/Blue strobe roof bar!
                if (c.RedStrobe != null) c.RedStrobe.sharedMaterial = redPhase ? _redLitMat : _redDimMat;
                if (c.BlueStrobe != null) c.BlueStrobe.sharedMaterial = redPhase ? _blueDimMat : _blueLitMat;
                if (c.StrobeLight != null)
                {
                    bool near = (pos - playerPos).sqrMagnitude < 65f * 65f;
                    c.StrobeLight.enabled = near;
                    if (near) c.StrobeLight.color = redPhase ? new Color(1.0f, 0.14f, 0.14f) : new Color(0.14f, 0.45f, 1.0f);
                }

                // Distance-scaled European police siren volume
                float dist = Vector3.Distance(pos, playerPos);
                if (c.SirenSource != null)
                {
                    float sirenVol = Mathf.Clamp01(1f - (dist / 82f)) * 0.22f;
                    c.SirenSource.volume = sirenVol;
                    c.SirenSource.pitch = 0.96f + (i * 0.04f) + Mathf.Clamp(c.Speed * 0.004f, 0f, 0.08f);
                }

                // Recycle cruiser if player outran it by >115m
                if (dist > 118f)
                {
                    Destroy(c.Root);
                    _cruisers.RemoveAt(i);
                    continue;
                }

                if (c.RamCooldown > 0f) c.RamCooldown -= dt;

                // Stop close to on-foot player & deploy chasing officer!
                float desiredStopDist = playerDriving ? 6.2f : 11.5f;
                Vector3 toTarget = playerPos - pos;
                toTarget.y = 0f;

                if (dist <= desiredStopDist + 3.5f && !playerDriving && !c.OfficerDeployed && _officers.Count < 3)
                {
                    c.OfficerDeployed = true;
                    SpawnFootOfficer(pos + tr.right * 2.2f);
                }

                // Stuck reverse escape
                if (c.ReverseTimer > 0f)
                {
                    c.ReverseTimer -= dt;
                    c.Speed = Mathf.Lerp(c.Speed, -5.5f, 1f - Mathf.Exp(-5f * dt));
                    tr.Rotate(0f, (i % 2 == 0 ? 55f : -55f) * dt, 0f);
                }
                else if (dist <= desiredStopDist)
                {
                    c.Speed = Mathf.Lerp(c.Speed, 0f, 1f - Mathf.Exp(-6.5f * dt));
                }
                else
                {
                    // Smart obstacle-avoiding pursuit steering (3-ray forward fan!)
                    Vector3 desiredDir = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : tr.forward;
                    Vector3 steerDir = ComputeCruiserSteering(tr, desiredDir);
                    Quaternion targetRot = Quaternion.LookRotation(steerDir, Vector3.up);
                    float turnRate = Mathf.Lerp(3.2f, 5.2f, Mathf.Clamp01(c.Speed / 12f));
                    tr.rotation = Quaternion.Slerp(tr.rotation, targetRot, 1f - Mathf.Exp(-turnRate * dt));

                    float targetSpd = dist < 16f ? Mathf.Min(maxSpeed, 11.5f) : maxSpeed;
                    c.Speed = Mathf.Lerp(c.Speed, targetSpd, 1f - Mathf.Exp(-2.4f * dt));
                }

                Vector3 move = tr.forward * (c.Speed * dt);
                // Solid building/wall check before stepping forward
                RaycastHit hit = default;
                bool blockedAhead = c.Speed > 0.5f &&
                    Physics.BoxCast(pos + Vector3.up * 0.9f, new Vector3(0.92f, 0.55f, 0.8f), tr.forward, out hit,
                        tr.rotation, Mathf.Abs(c.Speed * dt) + 1.85f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    !hit.transform.IsChildOf(tr);

                if (blockedAhead)
                {
                    // If it hit the player's car -> realistic PIT bump!
                    SedanVehicleController pCar = hit.collider.GetComponentInParent<SedanVehicleController>();
                    if (pCar != null && pCar.IsPlayerDriving && c.RamCooldown <= 0f)
                    {
                        c.RamCooldown = 1.6f;
                        Rigidbody pRb = pCar.GetComponent<Rigidbody>();
                        if (pRb != null)
                        {
                            pRb.AddForce((tr.forward + tr.right * 0.35f) * 3200f, ForceMode.Impulse);
                        }
                        NeighborhoodConsequenceSystem.EnsureInstance().PlayMetalClang(0.55f);
                        GameFeelAudio.SpawnBurst(hit.point, new Color(1.0f, 0.78f, 0.24f), count: 8, spread: 0.4f);
                    }
                    c.Speed *= 0.2f;
                    c.StuckTimer += dt * 2.5f;
                }
                else
                {
                    Vector3 nextPos = pos + move;
                    nextPos.x = Mathf.Clamp(nextPos.x, -412f, 412f);
                    nextPos.z = Mathf.Clamp(nextPos.z, -412f, 412f);
                    if (Physics.Raycast(new Vector3(nextPos.x, nextPos.y + 2.5f, nextPos.z), Vector3.down, out RaycastHit gHit, 6f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && gHit.normal.y > 0.65f)
                    {
                        nextPos.y = Mathf.Lerp(nextPos.y, gHit.point.y + 0.04f, 1f - Mathf.Exp(-14f * dt));
                    }
                    tr.position = nextPos;
                }

                // Spin cruiser wheels realistically
                if (c.Wheels != null)
                {
                    float spinDeg = (c.Speed * dt / 0.34f) * Mathf.Rad2Deg;
                    for (int w = 0; w < c.Wheels.Length; w++)
                    {
                        if (c.Wheels[w] != null) c.Wheels[w].Rotate(spinDeg, 0f, 0f, Space.Self);
                    }
                }

                // Stuck check -> short reverse & steer out
                c.StuckTimer += dt;
                if (c.StuckTimer >= 2.2f)
                {
                    if ((tr.position - c.LastPos).sqrMagnitude < 1.2f && dist > desiredStopDist + 2f)
                    {
                        c.ReverseTimer = 1.15f;
                    }
                    c.LastPos = tr.position;
                    c.StuckTimer = 0f;
                }
            }
        }

        private static Vector3 ComputeCruiserSteering(Transform tr, Vector3 desiredDir)
        {
            Vector3 origin = tr.position + Vector3.up * 0.9f;
            if (!Physics.SphereCast(origin, 0.85f, desiredDir, out RaycastHit hit, 8.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                hit.transform.IsChildOf(tr) ||
                hit.collider.GetComponentInParent<PlayerFootController>() != null)
            {
                return desiredDir;
            }

            float[] angles = { 35f, -35f, 70f, -70f };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 cand = Quaternion.Euler(0f, angles[i], 0f) * desiredDir;
                if (!Physics.SphereCast(origin, 0.80f, cand, out RaycastHit h2, 7.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                    h2.transform.IsChildOf(tr))
                {
                    return cand;
                }
            }
            return desiredDir;
        }

        private void TrySpawnPoliceCruiser(Vector3 playerPos)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float dist = UnityEngine.Random.Range(38f, 58f);
                Vector3 cand = new Vector3(
                    Mathf.Clamp(playerPos.x + Mathf.Cos(ang) * dist, -400f, 400f),
                    0.08f,
                    Mathf.Clamp(playerPos.z + Mathf.Sin(ang) * dist, -400f, 400f));

                // Avoid spawning inside Sultanbeyli Lake
                if (cand.x >= -226f && cand.x <= -168f && cand.z >= -26f && cand.z <= 26f) continue;

                if (Physics.Raycast(cand + Vector3.up * 16f, Vector3.down, out RaycastHit gHit, 22f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (gHit.point.y > 1.5f || gHit.normal.y < 0.78f) continue;
                    cand.y = gHit.point.y + 0.04f;
                }

                Vector3 facePlayer = playerPos - cand;
                facePlayer.y = 0f;
                Quaternion rot = facePlayer.sqrMagnitude > 0.1f ? Quaternion.LookRotation(facePlayer.normalized, Vector3.up) : Quaternion.identity;

                if (Physics.CheckBox(cand + Vector3.up * 1.15f, new Vector3(1.15f, 0.75f, 2.35f), rot,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                BuildPoliceCruiserObject(cand, rot);
                return;
            }
        }

        private void BuildPoliceCruiserObject(Vector3 pos, Quaternion rot)
        {
            GameObject root = new GameObject($"PoliceCruiser_{_cruisers.Count + 1:00}");
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(pos, rot);

            // Solid Collider + Kinematic Rigidbody
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.78f, 0f);
            col.size = new Vector3(1.92f, 1.38f, 4.35f);
            Rigidbody rb = WorldPhysicsSolidifier.EnsureKinematicRigidbody(root);

            // Turkish 155 POLİS Sedan — Real sculpted Blender Car FBX (OW_CustomerCar) with glossy clearcoat livery + fallback!
            GameObject fbxCar = OurWorldModelLibrary.InstantiateModel("OW_CustomerCar", root.transform, Vector3.zero, Quaternion.identity, Vector3.one, removeColliders: true);
            Transform[] wheels = new Transform[4];
            if (fbxCar != null)
            {
                // Apply glossy Turkish Police White body + blue hood/side livery stripes over real Blender bodywork
                Renderer[] carRends = fbxCar.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < carRends.Length; r++)
                {
                    Material[] ms = carRends[r].sharedMaterials;
                    bool mod = false;
                    for (int m = 0; m < ms.Length; m++)
                    {
                        if (ms[m] == null) continue;
                        string mn = ms[m].name;
                        if (mn.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mn.IndexOf("Paint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mn.IndexOf("Car", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            ms[m] = _bodyWhiteMat;
                            mod = true;
                        }
                        else if (mn.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 mn.IndexOf("Window", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            ms[m] = _glassDarkMat;
                            mod = true;
                        }
                    }
                    if (mod) carRends[r].sharedMaterials = ms;
                }
                CreateCarPart("BlueStripeL", root.transform, new Vector3(-0.96f, 0.62f, 0f), new Vector3(0.03f, 0.22f, 3.65f), _stripeBlueMat);
                CreateCarPart("BlueStripeR", root.transform, new Vector3( 0.96f, 0.62f, 0f), new Vector3(0.03f, 0.22f, 3.65f), _stripeBlueMat);
                CreateCarPart("HoodStripe", root.transform, new Vector3(0f, 0.94f, 1.32f), new Vector3(1.32f, 0.03f, 1.05f), _stripeBlueMat);
            }
            else
            {
                // Clean geometric fallback if OW_CustomerCar resource is unavailable
                CreateCarPart("Chassis", root.transform, new Vector3(0f, 0.56f, 0f), new Vector3(1.88f, 0.62f, 4.28f), _bodyWhiteMat);
                CreateCarPart("BlueStripeL", root.transform, new Vector3(-0.955f, 0.58f, 0f), new Vector3(0.03f, 0.24f, 4.10f), _stripeBlueMat);
                CreateCarPart("BlueStripeR", root.transform, new Vector3( 0.955f, 0.58f, 0f), new Vector3(0.03f, 0.24f, 4.10f), _stripeBlueMat);
                CreateCarPart("HoodStripe", root.transform, new Vector3(0f, 0.88f, 1.35f), new Vector3(1.45f, 0.03f, 1.15f), _stripeBlueMat);
                CreateCarPart("Cabin", root.transform, new Vector3(0f, 1.16f, -0.22f), new Vector3(1.66f, 0.58f, 2.18f), _bodyWhiteMat);
                CreateCarPart("Windshield", root.transform, new Vector3(0f, 1.16f, 0.88f), new Vector3(1.52f, 0.46f, 0.08f), _glassDarkMat);
                CreateCarPart("RearGlass", root.transform, new Vector3(0f, 1.16f, -1.32f), new Vector3(1.52f, 0.46f, 0.08f), _glassDarkMat);

                Vector3[] wPos = { new(-0.88f, 0.34f, 1.32f), new(0.88f, 0.34f, 1.32f), new(-0.88f, 0.34f, -1.30f), new(0.88f, 0.34f, -1.30f) };
                for (int w = 0; w < 4; w++)
                {
                    GameObject wh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    wh.name = $"Wheel_{w}";
                    wh.transform.SetParent(root.transform, false);
                    wh.transform.localPosition = wPos[w];
                    wh.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    wh.transform.localScale = new Vector3(0.68f, 0.14f, 0.68f);
                    Destroy(wh.GetComponent<Collider>());
                    MeshRenderer mr = wh.GetComponent<MeshRenderer>();
                    if (mr != null) { mr.sharedMaterial = _wheelDarkMat; mr.shadowCastingMode = ShadowCastingMode.Off; }
                    wheels[w] = wh.transform;
                }
            }

            // Roof Flashing Red/Blue Lightbar!
            CreateCarPart("LightbarBase", root.transform, new Vector3(0f, 1.54f, -0.15f), new Vector3(1.22f, 0.08f, 0.28f), _wheelDarkMat);
            Renderer redR = CreateCarPart("StrobeRed", root.transform, new Vector3(-0.34f, 1.62f, -0.15f), new Vector3(0.46f, 0.14f, 0.24f), _redLitMat);
            Renderer blueR = CreateCarPart("StrobeBlue", root.transform, new Vector3( 0.34f, 1.62f, -0.15f), new Vector3(0.46f, 0.14f, 0.24f), _blueLitMat);

            GameObject lightGo = new GameObject("StrobePointLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.08f, -0.15f);
            Light strobePt = lightGo.AddComponent<Light>();
            strobePt.type = LightType.Point;
            strobePt.range = 16f;
            strobePt.intensity = 22f;
            strobePt.shadows = LightShadows.None;

            // Dedicated child AudioSource for looping two-tone Police Siren
            GameObject sirenGo = new GameObject("PoliceSirenAudio");
            sirenGo.transform.SetParent(root.transform, false);
            AudioSource siren = sirenGo.AddComponent<AudioSource>();
            siren.clip = _sirenClip;
            siren.loop = true;
            siren.spatialBlend = 0.72f;
            siren.minDistance = 6f;
            siren.maxDistance = 85f;
            siren.volume = 0.18f;
            siren.Play();

            _cruisers.Add(new PoliceCruiser
            {
                Root = root,
                Rb = rb,
                Wheels = wheels,
                RedStrobe = redR,
                BlueStrobe = blueR,
                StrobeLight = strobePt,
                SirenSource = siren,
                LastPos = pos
            });
        }

        private static Renderer CreateCarPart(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = name;
            p.transform.SetParent(parent, false);
            p.transform.localPosition = localPos;
            p.transform.localScale = scale;
            Collider c = p.GetComponent<Collider>();
            if (c != null) Destroy(c);
            MeshRenderer mr = p.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = name == "Chassis" || name == "Cabin" ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            return mr;
        }

        // =====================================================================
        // PURSUING FOOT POLICE OFFICERS + BUSTING MECHANIC
        // =====================================================================
        private void SpawnFootOfficer(Vector3 spawnPos)
        {
            ProceduralHumanoid template = PlayerModeController.Instance?.FootController?.Humanoid;
            if (template == null) return;

            ProceduralHumanoid hum = Instantiate(template, transform);
            hum.name = $"PoliceOfficer_{_officers.Count + 1:00}";
            hum.transform.position = spawnPos;
            hum.transform.localScale = Vector3.one * 1.03f;

            Collider[] oldCols = hum.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < oldCols.Length; c++) Destroy(oldCols[c]);

            CapsuleCollider cap = hum.gameObject.AddComponent<CapsuleCollider>();
            cap.radius = 0.30f;
            cap.height = 1.76f;
            cap.center = new Vector3(0f, 0.88f, 0f);
            Rigidbody rb = WorldPhysicsSolidifier.EnsureKinematicRigidbody(hum.gameObject);

            // Style in crisp Turkish Police Navy Uniform + Peaked Cap + Badge
            Renderer[] rends = hum.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rends.Length; r++)
            {
                Material[] ms = rends[r].sharedMaterials;
                bool changed = false;
                for (int m = 0; m < ms.Length; m++)
                {
                    if (ms[m] != null && (ms[m].name.Contains("MAT_Char_Shirt") || ms[m].name.Contains("MAT_Char_Pants")))
                    {
                        ms[m] = _officerUniformMat;
                        changed = true;
                    }
                }
                if (changed) rends[r].sharedMaterials = ms;
            }

            if (hum.HeadPivot != null)
            {
                CreateCarPart("PolisCap", hum.HeadPivot, new Vector3(0f, 0.19f, 0.01f), new Vector3(0.25f, 0.09f, 0.27f), _officerUniformMat);
                CreateCarPart("PolisBadge", hum.HeadPivot, new Vector3(0f, 0.21f, 0.14f), new Vector3(0.05f, 0.05f, 0.02f), _goldBadgeMat);
            }

            bool en = LocalizationService.IsEnglish;
            NpcSpeechBubbleManager.Say(hum.transform, en ? "Police Officer" : "Polis Memuru",
                en ? "Freeze! Istanbul Police — hands up!" : "Dur! Polis! Teslim ol!", 3.0f, NpcBubbleMood.Alert, 2.08f);

            _officers.Add(new PoliceOfficer
            {
                Root = hum.gameObject,
                Rb = rb,
                Humanoid = hum,
                Health = 125f
            });
        }

        private void UpdateOfficers(float dt, Vector3 playerPos, bool playerDriving)
        {
            bool anyCloseBuster = false;

            for (int i = _officers.Count - 1; i >= 0; i--)
            {
                PoliceOfficer o = _officers[i];
                if (o == null || o.Root == null)
                {
                    _officers.RemoveAt(i);
                    continue;
                }

                Transform tr = o.Root.transform;
                Vector3 pos = tr.position;
                if (o.HitCooldown > 0f) o.HitCooldown -= dt;
                if (o.PunchCooldown > 0f) o.PunchCooldown -= dt;

                float dist = Vector3.Distance(pos, playerPos);
                if (dist > 85f)
                {
                    Destroy(o.Root);
                    _officers.RemoveAt(i);
                    continue;
                }

                if (o.KnockdownTimer > 0f)
                {
                    o.KnockdownTimer -= dt;
                    if (o.StumbleVel.sqrMagnitude > 0.02f)
                    {
                        Vector3 dRef = tr.forward;
                        NpcSmartNavigation.MoveActorSafely(tr, o.Rb, o.StumbleVel * dt, ref dRef, false, 0.30f);
                        o.StumbleVel = Vector3.MoveTowards(o.StumbleVel, Vector3.zero, 11f * dt);
                    }
                    o.Humanoid?.SetLocomotion(0f, true, 0f);
                    if (o.KnockdownTimer <= 0f) o.Health = 125f;
                    continue;
                }

                _radarBlips.Add(pos);

                if (o.StumbleVel.sqrMagnitude > 0.02f)
                {
                    Vector3 dRef = tr.forward;
                    NpcSmartNavigation.MoveActorSafely(tr, o.Rb, o.StumbleVel * dt, ref dRef, false, 0.30f);
                    o.StumbleVel = Vector3.MoveTowards(o.StumbleVel, Vector3.zero, 11f * dt);
                }

                Vector3 toP = playerPos - pos;
                toP.y = 0f;
                float flatDist = toP.magnitude;
                Vector3 dir = flatDist > 0.05f ? toP / flatDist : tr.forward;
                tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-12f * dt));

                if (o.PendingPunchTimer > 0f)
                {
                    o.PendingPunchTimer -= dt;
                    o.Humanoid?.SetLocomotion(0f, true, 0f);
                    if (o.PendingPunchTimer <= 0f && flatDist <= 2.0f && !playerDriving)
                    {
                        bool en = LocalizationService.IsEnglish;
                        PlayerNeedsController.Instance?.TakeDamage(14f, pos, en ? "Police Officer" : "Polis Memuru", heavyHit: true);
                    }
                    continue;
                }

                if (flatDist > 1.55f)
                {
                    float chaseSpd = 4.65f + _wantedLevel * 0.18f;
                    Vector3 steer = NpcSmartNavigation.ComputeSmartSteering(tr, dir, 2.0f, 0.30f, avoidPlayer: false);
                    Vector3 pRef = steer;
                    NpcSmartNavigation.MoveActorSafely(tr, o.Rb, steer * (chaseSpd * dt), ref pRef, false, 0.30f);
                    o.Humanoid?.SetLocomotion(chaseSpd, true, 0f, steer);
                }
                else
                {
                    o.Humanoid?.SetLocomotion(0f, true, 0f);
                    if (!playerDriving)
                    {
                        anyCloseBuster = true;
                        if (_wantedLevel >= 2 && o.PunchCooldown <= 0f)
                        {
                            o.PunchCooldown = 1.45f;
                            o.PendingPunchTimer = 0.24f;
                            o.Humanoid?.PlayPunch();
                        }
                    }
                }
            }

            // GTA Busting meter when trapped closely on foot (Dodge Roll or knock officer out to break free!)
            if (anyCloseBuster && !playerDriving)
            {
                PlayerFootController foot = PlayerModeController.Instance?.FootController;
                float speed = foot != null ? foot.CurrentSpeed : 0f;
                float fillRate = speed < 2.2f ? 0.42f : 0.14f;
                _bustProgress = Mathf.Clamp01(_bustProgress + dt * fillRate);
                if (_bustProgress >= 1f)
                {
                    StartCoroutine(BustedSequenceRoutine());
                }
            }
            else
            {
                _bustProgress = Mathf.MoveTowards(_bustProgress, 0f, dt * 0.55f);
            }
        }

        private IEnumerator BustedSequenceRoutine()
        {
            _isBustingRoutine = true;
            int stars = Mathf.Max(1, _wantedLevel);
            int bailFine = stars * 80;
            bool en = LocalizationService.IsEnglish;

            PlayerModeController mode = PlayerModeController.Instance;
            mode?.SetModalLock(this, true);
            GameFeelAudio.TriggerHitstop(0.35f, slowScale: 0.22f);
            NeighborhoodConsequenceSystem.EnsureInstance().PlayZabitaWhistle(0.85f);

            if (_bustedBanner != null)
            {
                _bustedBanner.SetActive(true);
                if (_bustedBannerTitle != null) _bustedBannerTitle.text = en ? "BUSTED!" : "YAKALANDIN!";
                if (_bustedBannerSub != null) _bustedBannerSub.text = en
                    ? $"Sultanbeyli Police Station · Bail: -{bailFine} ₺"
                    : $"Sultanbeyli Polis Merkezi · Kefalet: -{bailFine} ₺";
                if (_bustedBannerGroup != null) _bustedBannerGroup.alpha = 1f;
            }

            yield return new WaitForSecondsRealtime(2.1f);

            SaveManager sm = SaveManager.Instance;
            if (sm != null && sm.Data != null)
            {
                int deduct = Mathf.Min(sm.Data.money, bailFine);
                if (deduct > 0) sm.TrySpend(deduct);
            }

            // Release outside Abdurrahmangazi / Kent Meydanı or Hamidiye Plaza cleanly!
            WasLastClearByBusted = true;
            SetWantedLevel(0, announce: false);
            NeighborhoodConsequenceSystem.CoolDownHeatOnRecovery();
            TownPedestrian.CalmAllAngryCitizens();
            LivingNeighborhoodPopulator.CalmAllAngryNpcs();
            IstanbulDiverseCitizensPopulator.CalmAllHostileCitizens();

            if (mode != null)
            {
                if (mode.CurrentMode != ControlMode.OnFoot) mode.SetOnFootMode(snapCamera: true);
                mode.FootController?.TeleportTo(new Vector3(172f, 0.14f, 184f), Quaternion.Euler(0f, 180f, 0f));
                PlayerNeedsController.Instance?.RestoreHealth(100f);
                mode.SetModalLock(this, false);
                mode.RaiseStatusMessage(en
                    ? $"🚓 Released from Sultanbeyli Police Station (-{bailFine} ₺ bail)."
                    : $"🚓 Sultanbeyli Polis Merkezi'nden serbest bırakıldın (-{bailFine} ₺ kefalet).");
            }

            if (_bustedBanner != null) _bustedBanner.SetActive(false);
            _isBustingRoutine = false;
        }

        private void DespawnAllUnits()
        {
            for (int i = 0; i < _cruisers.Count; i++)
            {
                if (_cruisers[i]?.Root != null) Destroy(_cruisers[i].Root);
            }
            _cruisers.Clear();

            for (int i = 0; i < _officers.Count; i++)
            {
                if (_officers[i]?.Root != null) Destroy(_officers[i].Root);
            }
            _officers.Clear();
            _radarBlips.Clear();
        }

        // =====================================================================
        // GTA 6 TOP-RIGHT WANTED STAR HUD & BUSTED OVERLAY
        // =====================================================================
        private void BuildWantedHud()
        {
            GameObject canvasGo = new GameObject("GTA6_WantedHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            Canvas c = canvasGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 148;
            CanvasScaler cs = canvasGo.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;

            // Top-Right Wanted Stars Pill (positioned cleanly below HUD top-right buttons)
            _hudRoot = new GameObject("WantedStarsPanel", typeof(RectTransform), typeof(Image));
            _hudRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform rt = (RectTransform)_hudRoot.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-28f, -268f);
            rt.sizeDelta = new Vector2(268f, 92f);
            _hudRoot.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.13f, 0.84f);

            // 5 Stars Row
            for (int i = 0; i < 5; i++)
            {
                GameObject sGo = new GameObject($"Star_{i}", typeof(RectTransform), typeof(Text));
                sGo.transform.SetParent(_hudRoot.transform, false);
                RectTransform srt = (RectTransform)sGo.transform;
                srt.anchorMin = new Vector2(0f, 1f);
                srt.anchorMax = new Vector2(0f, 1f);
                srt.pivot = new Vector2(0f, 1f);
                srt.anchoredPosition = new Vector2(16f + i * 48f, -6f);
                srt.sizeDelta = new Vector2(46f, 48f);
                Text t = sGo.GetComponent<Text>();
                t.font = OurWorldUiKit.DefaultFont;
                t.text = "★";
                t.fontSize = 40;
                t.alignment = TextAnchor.MiddleCenter;
                t.color = new Color(1f, 1f, 1f, 0.22f);
                _starTexts[i] = t;
            }

            // Status Subtitle ("POLİS TAKİBİNDE" / "İZİ KAYBETTİRİLİYOR")
            GameObject stGo = new GameObject("WantedStatus", typeof(RectTransform), typeof(Text));
            stGo.transform.SetParent(_hudRoot.transform, false);
            RectTransform strt = (RectTransform)stGo.transform;
            strt.anchorMin = new Vector2(0f, 0f);
            strt.anchorMax = new Vector2(1f, 0f);
            strt.pivot = new Vector2(0.5f, 0f);
            strt.anchoredPosition = new Vector2(0f, 18f);
            strt.sizeDelta = new Vector2(-16f, 26f);
            _statusBadgeText = stGo.GetComponent<Text>();
            _statusBadgeText.font = OurWorldUiKit.DefaultFont;
            _statusBadgeText.fontSize = 16;
            _statusBadgeText.fontStyle = FontStyle.Bold;
            _statusBadgeText.alignment = TextAnchor.MiddleCenter;
            _statusBadgeText.color = Color.white;

            // Evasion Progress Bar
            _evadeBarBg = new GameObject("EvadeBarBg", typeof(RectTransform), typeof(Image));
            _evadeBarBg.transform.SetParent(_hudRoot.transform, false);
            RectTransform ebrt = (RectTransform)_evadeBarBg.transform;
            ebrt.anchorMin = new Vector2(0f, 0f);
            ebrt.anchorMax = new Vector2(1f, 0f);
            ebrt.pivot = new Vector2(0.5f, 0f);
            ebrt.anchoredPosition = new Vector2(0f, 6f);
            ebrt.sizeDelta = new Vector2(-24f, 8f);
            _evadeBarBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);

            GameObject fillGo = new GameObject("EvadeBarFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(_evadeBarBg.transform, false);
            RectTransform frt = (RectTransform)fillGo.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(0f, 1f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            _evadeBarFill = fillGo.GetComponent<Image>();
            _evadeBarFill.color = new Color(0.24f, 0.88f, 0.96f, 1f);

            // Center-Bottom Bust Warning Meter ("YAKALANIYORSUN!")
            _bustBarBg = new GameObject("BustWarningBar", typeof(RectTransform), typeof(Image));
            _bustBarBg.transform.SetParent(canvasGo.transform, false);
            RectTransform brt = (RectTransform)_bustBarBg.transform;
            brt.anchorMin = new Vector2(0.5f, 0.25f);
            brt.anchorMax = new Vector2(0.5f, 0.25f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(320f, 38f);
            _bustBarBg.GetComponent<Image>().color = new Color(0.10f, 0.04f, 0.06f, 0.88f);

            GameObject bFill = new GameObject("BustFill", typeof(RectTransform), typeof(Image));
            bFill.transform.SetParent(_bustBarBg.transform, false);
            RectTransform bfrt = (RectTransform)bFill.transform;
            bfrt.anchorMin = Vector2.zero;
            bfrt.anchorMax = new Vector2(0f, 1f);
            bfrt.offsetMin = Vector2.zero;
            bfrt.offsetMax = Vector2.zero;
            _bustBarFill = bFill.GetComponent<Image>();
            _bustBarFill.color = new Color(0.92f, 0.18f, 0.22f, 0.92f);

            GameObject bLbl = new GameObject("BustLbl", typeof(RectTransform), typeof(Text));
            bLbl.transform.SetParent(_bustBarBg.transform, false);
            RectTransform blrt = (RectTransform)bLbl.transform;
            blrt.anchorMin = Vector2.zero;
            blrt.anchorMax = Vector2.one;
            blrt.offsetMin = Vector2.zero;
            blrt.offsetMax = Vector2.zero;
            _bustLabel = bLbl.GetComponent<Text>();
            _bustLabel.font = OurWorldUiKit.DefaultFont;
            _bustLabel.fontSize = 18;
            _bustLabel.fontStyle = FontStyle.Bold;
            _bustLabel.alignment = TextAnchor.MiddleCenter;
            _bustLabel.color = Color.white;
            _bustBarBg.SetActive(false);

            // Iconic GTA "YAKALANDIN! / BUSTED!" Full-Width Banner
            _bustedBanner = new GameObject("BustedBanner", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _bustedBanner.transform.SetParent(canvasGo.transform, false);
            RectTransform banRt = (RectTransform)_bustedBanner.transform;
            banRt.anchorMin = new Vector2(0f, 0.36f);
            banRt.anchorMax = new Vector2(1f, 0.64f);
            banRt.offsetMin = Vector2.zero;
            banRt.offsetMax = Vector2.zero;
            _bustedBanner.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.11f, 0.88f);
            _bustedBannerGroup = _bustedBanner.GetComponent<CanvasGroup>();

            GameObject btGo = new GameObject("BustedTitle", typeof(RectTransform), typeof(Text));
            btGo.transform.SetParent(_bustedBanner.transform, false);
            RectTransform btrt = (RectTransform)btGo.transform;
            btrt.anchorMin = new Vector2(0f, 0.42f);
            btrt.anchorMax = new Vector2(1f, 0.92f);
            btrt.offsetMin = Vector2.zero;
            btrt.offsetMax = Vector2.zero;
            _bustedBannerTitle = btGo.GetComponent<Text>();
            _bustedBannerTitle.font = OurWorldUiKit.DefaultFont;
            _bustedBannerTitle.fontSize = 74;
            _bustedBannerTitle.fontStyle = FontStyle.Bold;
            _bustedBannerTitle.alignment = TextAnchor.MiddleCenter;
            _bustedBannerTitle.color = new Color(0.24f, 0.66f, 1.0f, 1f);

            GameObject bsGo = new GameObject("BustedSub", typeof(RectTransform), typeof(Text));
            bsGo.transform.SetParent(_bustedBanner.transform, false);
            RectTransform bsrt = (RectTransform)bsGo.transform;
            bsrt.anchorMin = new Vector2(0f, 0.10f);
            bsrt.anchorMax = new Vector2(1f, 0.44f);
            bsrt.offsetMin = Vector2.zero;
            bsrt.offsetMax = Vector2.zero;
            _bustedBannerSub = bsGo.GetComponent<Text>();
            _bustedBannerSub.font = OurWorldUiKit.DefaultFont;
            _bustedBannerSub.fontSize = 26;
            _bustedBannerSub.alignment = TextAnchor.MiddleCenter;
            _bustedBannerSub.color = new Color(0.95f, 0.96f, 0.98f, 0.92f);

            _bustedBanner.SetActive(false);
            RefreshHudVisibility();
        }

        private void RefreshHudVisibility()
        {
            if (_hudRoot != null) _hudRoot.SetActive(_wantedLevel > 0);
            if (_bustBarBg != null && _wantedLevel <= 0) _bustBarBg.SetActive(false);
        }

        private void UpdateHudAnimations(bool redPhase)
        {
            if (_hudRoot == null) return;
            bool photoHide = MobileDrivingAndPhotoHud.Instance != null && MobileDrivingAndPhotoHud.Instance.IsPhotoModeActive;
            bool show = _wantedLevel > 0 && !photoHide && !MainMenuController.IsMenuActive;
            if (_hudRoot.activeSelf != show) _hudRoot.SetActive(show);
            if (!show) return;

            Color activeCol;
            if (_inPoliceSight)
            {
                activeCol = redPhase ? new Color(1.0f, 0.22f, 0.24f, 1f) : new Color(0.26f, 0.64f, 1.0f, 1f);
            }
            else
            {
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5.2f);
                activeCol = new Color(0.96f, 0.98f, 1f, pulse);
            }

            for (int i = 0; i < 5; i++)
            {
                if (_starTexts[i] == null) continue;
                _starTexts[i].color = i < _wantedLevel ? activeCol : new Color(1f, 1f, 1f, 0.16f);
            }

            bool en = LocalizationService.IsEnglish;
            if (_statusBadgeText != null)
            {
                _statusBadgeText.text = _inPoliceSight
                    ? (en ? "POLICE PURSUIT!" : "POLİS TAKİBİNDE!")
                    : (en ? "EVADING · STAY HIDDEN" : "İZİ KAYBETTİR · SAKLAN");
                _statusBadgeText.color = _inPoliceSight ? activeCol : new Color(0.32f, 0.92f, 0.96f, 1f);
            }

            if (_evadeBarBg != null && _evadeBarFill != null)
            {
                _evadeBarBg.SetActive(!_inPoliceSight);
                if (!_inPoliceSight)
                {
                    float frac = Mathf.Clamp01(_evadeTimer / Mathf.Max(1f, _evadeDuration));
                    _evadeBarFill.rectTransform.anchorMax = new Vector2(frac, 1f);
                }
            }

            if (_bustBarBg != null && _bustBarFill != null)
            {
                bool showBust = _bustProgress > 0.05f;
                if (_bustBarBg.activeSelf != showBust) _bustBarBg.SetActive(showBust);
                if (showBust)
                {
                    _bustBarFill.rectTransform.anchorMax = new Vector2(_bustProgress, 1f);
                    if (_bustLabel != null) _bustLabel.text = en ? "🚨 BEING BUSTED — DODGE!" : "🚨 YAKALANIYORSUN — KAÇ!";
                }
            }
        }

        // =====================================================================
        // MATERIALS & PROCEDURAL TWO-TONE EUROPEAN POLICE SIREN
        // =====================================================================
        private Material MakeMat(Shader sh, Color col, float smooth = 0.32f, Color? emission = null)
        {
            Material m = new Material(sh);
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, col);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, col);
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, smooth);
            if (emission.HasValue && m.HasProperty(EmissionColorId))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionColorId, emission.Value);
            }
            _ownedMats.Add(m);
            return m;
        }

        private void BuildMaterialsAndAudio()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (lit != null)
            {
                _bodyWhiteMat = MakeMat(lit, new Color(0.95f, 0.96f, 0.98f), 0.72f);
                _stripeBlueMat = MakeMat(lit, new Color(0.08f, 0.28f, 0.74f), 0.65f);
                _glassDarkMat = MakeMat(lit, new Color(0.07f, 0.10f, 0.15f), 0.88f);
                _wheelDarkMat = MakeMat(lit, new Color(0.11f, 0.12f, 0.14f), 0.25f);
                _redLitMat = MakeMat(lit, new Color(1.0f, 0.12f, 0.14f), 0.85f, new Color(1.0f, 0.12f, 0.14f) * 3.5f);
                _redDimMat = MakeMat(lit, new Color(0.42f, 0.06f, 0.07f), 0.65f);
                _blueLitMat = MakeMat(lit, new Color(0.12f, 0.44f, 1.0f), 0.85f, new Color(0.12f, 0.44f, 1.0f) * 3.5f);
                _blueDimMat = MakeMat(lit, new Color(0.06f, 0.16f, 0.42f), 0.65f);
                _officerUniformMat = MakeMat(lit, new Color(0.07f, 0.15f, 0.30f), 0.24f);
                _goldBadgeMat = MakeMat(lit, new Color(0.96f, 0.78f, 0.22f), 0.75f);
            }
            _sirenClip = BuildTurkishPoliceSirenClip(22050);
        }

        private static AudioClip BuildTurkishPoliceSirenClip(int sr)
        {
            // Classic Istanbul Police Hi-Lo / Yelp two-tone alternating siren (clean 1.4s seamless loop)
            int count = Mathf.RoundToInt(sr * 1.40f);
            float[] data = new float[count];
            double phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sr;
                bool hiTone = (t % 0.70f) < 0.35f;
                float freq = hiTone ? 880f : 660f;
                // Smooth frequency glide between tones
                float localU = (t % 0.35f) / 0.35f;
                if (localU < 0.08f)
                {
                    float fromF = hiTone ? 660f : 880f;
                    freq = Mathf.Lerp(fromF, freq, localU / 0.08f);
                }
                phase += (2.0 * Math.PI * freq) / sr;
                float s = (float)(Math.Sin(phase) * 0.65 + Math.Sin(phase * 2.0) * 0.18 + Math.Sin(phase * 3.0) * 0.08);
                data[i] = Mathf.Clamp(s * 0.60f, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("TurkishPoliceSiren", count, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
