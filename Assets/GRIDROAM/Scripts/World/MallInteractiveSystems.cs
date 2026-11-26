using System.Collections;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.UI;
using OurWorld.Vehicle;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Gentle conveyor-style escalator boost when walking on a mall escalator ramp.
    /// Automatically determines ascent or descent based on the player's facing direction!
    /// </summary>
    public sealed class MallEscalatorZone : MonoBehaviour
    {
        public Vector3 UpDirection = Vector3.forward;
        public float AssistSpeed = 1.65f;

        private void OnTriggerStay(Collider other)
        {
            if (other == null) return;
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.CurrentMode != ControlMode.OnFoot || mode.IsModalLocked) return;

            CharacterController cc = other.GetComponentInParent<CharacterController>();
            if (cc == null || !cc.enabled || mode.FootController == null) return;
            if (cc.transform != mode.FootController.transform) return;

            Vector3 flatForward = mode.FootController.transform.forward;
            flatForward.y = 0f;
            Vector3 flatUp = new Vector3(UpDirection.x, 0f, UpDirection.z);
            float dirSign = Vector3.Dot(flatForward.normalized, flatUp.normalized) >= -0.15f ? 1f : -1f;

            cc.Move(UpDirection * (dirSign * AssistSpeed * Time.deltaTime));
        }
    }

    /// <summary>
    /// Working 5-level panoramic glass elevator inside Hamidiye Atlas AVM.
    /// Smoothly animates the illuminated glass cabin between levels 0..4 and carries the player safely.
    /// </summary>
    public sealed class MallElevatorController : MonoBehaviour
    {
        public Transform CabinTransform;
        public float[] FloorHeights = { 0f, 4.6f, 9.2f, 13.8f, 18.4f };
        public Vector3 LandingOffsetLocal = new Vector3(0f, 0.08f, 2.55f);

        private int _currentFloor;
        private bool _isMoving;

        public int CurrentFloor => _currentFloor;
        public bool IsMoving => _isMoving;

        private void OnDestroy()
        {
            if (_isMoving && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
        }

        public void OpenFloorSelector(int calledFromFloor)
        {
            if (_isMoving) return;
            int floor = Mathf.Clamp(calledFromFloor, 0, FloorHeights.Length - 1);
            MallUiPanels.EnsureInstance().ShowElevatorModal(floor, target => TravelToFloor(floor, target));
        }

        public void TravelToFloor(int startFloor, int targetFloor)
        {
            if (_isMoving) return;
            targetFloor = Mathf.Clamp(targetFloor, 0, FloorHeights.Length - 1);
            if (targetFloor == startFloor && _currentFloor == targetFloor)
            {
                bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
                PlayerModeController.Instance?.RaiseStatusMessage(isEn
                    ? $"You are already on Floor {targetFloor}."
                    : $"Zaten {targetFloor}. kattasın.");
                return;
            }
            StartCoroutine(ElevatorRideRoutine(startFloor, targetFloor));
        }

        private IEnumerator ElevatorRideRoutine(int startFloor, int targetFloor)
        {
            _isMoving = true;
            PlayerModeController mode = PlayerModeController.Instance;
            PlayerFootController foot = mode != null ? mode.FootController : null;
            bool carryPlayer = foot != null && mode.CurrentMode == ControlMode.OnFoot;

            if (mode != null) mode.SetModalLock(this, true);
            if (foot != null) foot.SetMovementEnabled(false);

            GameFeelAudio.PlayUiClick();

            float startY = FloorHeights[Mathf.Clamp(startFloor, 0, FloorHeights.Length - 1)];
            float endY = FloorHeights[targetFloor];
            float distFloors = Mathf.Abs(targetFloor - startFloor);
            float duration = Mathf.Clamp(0.65f + distFloors * 0.45f, 0.75f, 2.35f);

            Vector3 cabinBase = CabinTransform != null ? CabinTransform.position : transform.position;
            Quaternion faceAtrium = Quaternion.Euler(0f, 180f, 0f);

            if (CabinTransform != null) CabinTransform.position = new Vector3(cabinBase.x, startY, cabinBase.z);
            if (carryPlayer) foot.TeleportTo(new Vector3(cabinBase.x, startY + 0.14f, cabinBase.z), faceAtrium);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / duration);
                float eased = u * u * (3f - 2f * u);
                float curY = Mathf.Lerp(startY, endY, eased);

                if (CabinTransform != null) CabinTransform.position = new Vector3(cabinBase.x, curY, cabinBase.z);
                if (carryPlayer) foot.TeleportTo(new Vector3(cabinBase.x, curY + 0.14f, cabinBase.z), faceAtrium);
                yield return null;
            }

            _currentFloor = targetFloor;
            if (CabinTransform != null) CabinTransform.position = new Vector3(cabinBase.x, endY, cabinBase.z);

            if (carryPlayer)
            {
                Vector3 exitWorld = new Vector3(cabinBase.x + LandingOffsetLocal.x, endY + LandingOffsetLocal.y, cabinBase.z + LandingOffsetLocal.z);
                foot.TeleportTo(exitWorld, faceAtrium);
                foot.SetMovementEnabled(true);
            }

            if (mode != null) mode.SetModalLock(this, false);
            GameFeelAudio.PlayCoinChime();

            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            string[] trNames = { "Zemin Kat (Market & Kafeler)", "1. Kat (Moda & Teknoloji)", "2. Kat (Yemek Katı)", "3. Kat (CineAtlas & JoyPark)", "4. Kat (Ennjoy Teras Kafe)" };
            string[] enNames = { "Ground Floor (Market & Cafes)", "1st Floor (Fashion & Tech)", "2nd Floor (Food Court)", "3rd Floor (CineAtlas & JoyPark)", "4th Floor (Rooftop Lounge)" };
            mode?.RaiseStatusMessage(isEn ? $"Arrived: {enNames[targetFloor]}" : $"Ulaşıldı: {trNames[targetFloor]}");

            _isMoving = false;
        }
    }

    public sealed class MallStrollingShopper
    {
        public string DisplayName;
        public Transform Root;
        public ProceduralHumanoid Humanoid;
        public Vector3[] LocalWaypoints;
        public int TargetIdx;
        public float Speed;
        public float PauseTimer;
        public bool IsFighter;
        public float HitCooldown;
        public float FightTimer;
        public float PunchCooldown;
        public float PendingPunchTimer;
        public bool PendingPunchHeavy;
        public int PunchCombo;
    }

    public sealed class MallNpcProfile
    {
        public string DisplayName;
        public string RoleTr;
        public string RoleEn;
        public string[] Lines;
    }

    /// <summary>
    /// Central interaction dispatcher, animated mall shopper controller, cinema screen animator,
    /// and distance-based interior culling manager for Hamidiye Atlas AVM.
    /// </summary>
    public sealed class HamidiyeMallInteractionHandler : MonoBehaviour
    {
        public static HamidiyeMallInteractionHandler ActiveInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMallStatics() => ActiveInstance = null;

        public Vector3 MallCenter;
        public GameObject InteriorRoot;
        public Transform FountainSpinner;
        public Transform[] EscalatorStepStrips;
        public MallElevatorController Elevator;
        public Material CinemaScreenMaterial;
        public Vector3 CinemaSeatWorldPos;
        public float InteriorActiveRadius = 60f;

        public readonly List<MallStrollingShopper> Strollers = new List<MallStrollingShopper>(16);
        public readonly Dictionary<string, MallNpcProfile> NpcProfiles = new Dictionary<string, MallNpcProfile>(32);

        private Dictionary<string, MallShopCatalog> _catalogs;
        private bool _subscribed;
        private bool _interiorActive = true;
        private float _cullTimer = 0.25f;
        private float _lastArcadePlayTime = -20f;
        private float _lastTelescopeXpTime = -99f;

        private void Awake()
        {
            ActiveInstance = this;
            BuildAllAtlasparkCatalogs();
        }

        private void Start()
        {
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted += HandleInteracted;
                _subscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this) ActiveInstance = null;
            TurkishCarRadio.SetMallAmbienceActive(false);
            if (_subscribed && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted -= HandleInteracted;
            }
        }

        private void Update()
        {
            _cullTimer -= Time.deltaTime;
            if (_cullTimer <= 0f)
            {
                _cullTimer = 0.45f;
                UpdateInteriorCulling();
            }

            if (!_interiorActive) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (FountainSpinner != null)
            {
                FountainSpinner.Rotate(0f, 38f * dt, 0f, Space.Self);
            }

            if (EscalatorStepStrips != null)
            {
                float wave = Mathf.Repeat(Time.time * 0.85f, 1f);
                for (int i = 0; i < EscalatorStepStrips.Length; i++)
                {
                    if (EscalatorStepStrips[i] == null) continue;
                    Vector3 lp = EscalatorStepStrips[i].localPosition;
                    lp.y = 0.08f + Mathf.Sin((wave + i * 0.25f) * Mathf.PI * 2f) * 0.012f;
                    EscalatorStepStrips[i].localPosition = lp;
                }
            }

            // Gently animate the CineAtlas 3D screen glow so the theater always feels alive!
            if (CinemaScreenMaterial != null && CinemaScreenMaterial.HasProperty("_EmissionColor"))
            {
                float h = Mathf.Repeat(Time.time * 0.055f, 1f);
                Color glow = Color.HSVToRGB(0.56f + Mathf.Sin(h * Mathf.PI * 2f) * 0.14f, 0.55f, 0.92f);
                CinemaScreenMaterial.SetColor("_EmissionColor", glow * 1.35f);
            }

            UpdateStrollingShoppers(dt);
        }

        public static void CalmAllShoppers()
        {
            if (ActiveInstance == null) return;
            for (int i = 0; i < ActiveInstance.Strollers.Count; i++)
            {
                MallStrollingShopper s = ActiveInstance.Strollers[i];
                if (s == null) continue;
                s.FightTimer = 0f;
                s.PendingPunchTimer = 0f;
            }
        }

        public bool TryReceivePlayerPunch(Vector3 strikeCenter, float radius, Vector3 attackerPos, Vector3 punchDir, int comboStep)
        {
            if (!_interiorActive) return false;
            MallStrollingShopper best = null;
            float maxR = radius + 0.55f;
            float bestSq = maxR * maxR;
            for (int i = 0; i < Strollers.Count; i++)
            {
                MallStrollingShopper s = Strollers[i];
                if (s == null || s.Root == null || s.Humanoid == null || s.HitCooldown > 0f) continue;
                Vector3 p = s.Root.position;
                if (Mathf.Abs(p.y - attackerPos.y) > 1.8f) continue;
                float dSq = ((p + Vector3.up * 1.0f) - strikeCenter).sqrMagnitude;
                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    best = s;
                }
            }
            if (best == null) return false;

            best.HitCooldown = 0.35f;
            best.Humanoid.PlayHitReact();
            bool en = LocalizationService.IsEnglish;
            string who = string.IsNullOrEmpty(best.DisplayName) ? (en ? "Mall Shopper" : "AVM Ziyaretçisi") : best.DisplayName;
            if (best.IsFighter)
            {
                best.FightTimer = 10f;
                best.PunchCooldown = 0.55f;
                best.PendingPunchTimer = 0f;
                string line = en ? "Hey! Don't touch me in the mall!" : "Hey! AVM'nin ortasında kime vuruyorsun sen?!";
                NpcSpeechBubbleManager.Say(best.Root, who, line, 2.8f, NpcBubbleMood.Alert, 2.0f);
                PlayerModeController.Instance?.RaiseStatusMessage($"{who}: {line}");
            }
            else
            {
                best.PauseTimer = 0f;
                string line = en ? "Security! Help!" : "Güvenlik! İmdat!";
                NpcSpeechBubbleManager.Say(best.Root, who, line, 2.8f, NpcBubbleMood.Alert, 2.0f);
                PlayerModeController.Instance?.RaiseStatusMessage($"{who}: {line}");
            }
            NeighborhoodConsequenceSystem.EnsureInstance().ReportOffense(
                NeighborhoodOffenseKind.AssaultCitizen,
                best.Root.position,
                who,
                repPenalty: 12,
                immediateFineTl: 0,
                heatDelta: 1.15f);
            return true;
        }

        private void UpdateStrollingShoppers(float dt)
        {
            bool modalOpen = PlayerModeController.Instance != null && PlayerModeController.Instance.IsModalLocked;
            bool playerKo = PlayerNeedsController.Instance != null && PlayerNeedsController.Instance.IsKnockedOut;
            Vector3 playerPos = PlayerModeController.Instance != null ? PlayerModeController.Instance.ActiveWorldPosition : Vector3.one * 9999f;

            for (int i = 0; i < Strollers.Count; i++)
            {
                MallStrollingShopper s = Strollers[i];
                if (s == null || s.Root == null || s.LocalWaypoints == null || s.LocalWaypoints.Length < 2) continue;

                if (s.HitCooldown > 0f) s.HitCooldown -= dt;

                // Retaliating mall shopper fights back!
                if (s.FightTimer > 0f)
                {
                    s.FightTimer -= dt;
                    if (s.PunchCooldown > 0f) s.PunchCooldown -= dt;
                    Vector3 toP = playerPos - s.Root.position;
                    bool sameFloor = Mathf.Abs(toP.y) < 2.2f;
                    toP.y = 0f;
                    float distP = toP.magnitude;

                    if (modalOpen || playerKo || !sameFloor || distP > 14f)
                    {
                        s.FightTimer = 0f;
                        s.PendingPunchTimer = 0f;
                    }
                    else
                    {
                        if (toP.sqrMagnitude > 0.01f)
                        {
                            s.Root.rotation = Quaternion.Slerp(s.Root.rotation, Quaternion.LookRotation(toP.normalized, Vector3.up), dt * 10f);
                        }
                        if (s.PendingPunchTimer > 0f)
                        {
                            s.PendingPunchTimer -= dt;
                            if (s.PendingPunchTimer <= 0f && distP <= 2.25f)
                            {
                                float dmg = s.PendingPunchHeavy ? 18f : 13f;
                                PlayerNeedsController.Instance?.TakeDamage(dmg, s.Root.position, s.DisplayName, s.PendingPunchHeavy);
                            }
                        }
                        if (distP > 1.65f)
                        {
                            Vector3 step = toP.normalized * (3.2f * dt);
                            if (!Physics.Raycast(s.Root.position + Vector3.up * 0.9f, toP.normalized, 0.8f, ~0, QueryTriggerInteraction.Ignore))
                            {
                                s.Root.position += step;
                                s.Humanoid?.SetLocomotion(3.2f, true, 0f);
                            }
                            else
                            {
                                s.Humanoid?.SetLocomotion(0f, true, 0f);
                            }
                        }
                        else
                        {
                            s.Humanoid?.SetLocomotion(0f, true, 0f);
                            if (s.PunchCooldown <= 0f && s.PendingPunchTimer <= 0f)
                            {
                                int combo = s.PunchCombo % 3;
                                s.PunchCombo++;
                                s.PendingPunchHeavy = combo == 2;
                                s.PunchCooldown = s.PendingPunchHeavy ? 1.4f : 1.05f;
                                s.PendingPunchTimer = 0.16f;
                                s.Humanoid?.PlayCombatMove(combo);
                            }
                        }
                        continue;
                    }
                }

                // Stop politely when player talks nearby or modal is open
                Vector3 toPlayer = playerPos - s.Root.position;
                bool playerClose = Mathf.Abs(toPlayer.y) < 2.0f && (toPlayer.x * toPlayer.x + toPlayer.z * toPlayer.z) < 2.2f;
                if (modalOpen || playerClose || s.PauseTimer > 0f)
                {
                    if (s.PauseTimer > 0f) s.PauseTimer -= dt;
                    s.Humanoid?.SetLocomotion(0f, true, 0f);
                    continue;
                }

                Vector3 target = s.LocalWaypoints[s.TargetIdx];
                Vector3 delta = target - s.Root.localPosition;
                delta.y = 0f;
                float dist = delta.magnitude;
                if (dist < 0.35f)
                {
                    s.TargetIdx = (s.TargetIdx + 1) % s.LocalWaypoints.Length;
                    s.PauseTimer = Random.Range(1.2f, 3.2f); // Pause to window-shop!
                    s.Humanoid?.SetLocomotion(0f, true, 0f);
                    continue;
                }

                Vector3 dir = delta / dist;
                s.Root.localPosition += dir * (s.Speed * dt);
                Quaternion desired = Quaternion.LookRotation(dir, Vector3.up);
                s.Root.localRotation = Quaternion.Slerp(s.Root.localRotation, desired, dt * 6f);
                s.Humanoid?.SetLocomotion(s.Speed * 0.55f, true, 0f);
            }
        }

        private void UpdateInteriorCulling()
        {
            if (InteriorRoot == null || PlayerModeController.Instance == null) return;
            Vector3 p = PlayerModeController.Instance.ActiveWorldPosition;
            float dx = p.x - MallCenter.x;
            float dz = p.z - MallCenter.z;
            float distSq = dx * dx + dz * dz;

            bool insideMall = Mathf.Abs(dx) < 17.5f && Mathf.Abs(dz) < 16.5f && p.y >= -0.5f && p.y < 23.0f;
            TurkishCarRadio.SetMallAmbienceActive(insideMall);

            float thresh = _interiorActive ? (InteriorActiveRadius + 8f) : InteriorActiveRadius;
            bool shouldBeActive = distSq <= thresh * thresh;
            if (shouldBeActive != _interiorActive)
            {
                _interiorActive = shouldBeActive;
                InteriorRoot.SetActive(_interiorActive);
            }
        }

        private void HandleInteracted(InteractableTrigger trig)
        {
            if (trig == null || string.IsNullOrEmpty(trig.InteractableId)) return;
            string id = trig.InteractableId;
            if (!id.StartsWith("mall_", System.StringComparison.Ordinal)) return;

            PlayerModeController mode = PlayerModeController.Instance;
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

            // 1. Panoramic Elevator Call Buttons
            if (id.StartsWith("mall_elevator_", System.StringComparison.Ordinal))
            {
                int floor = 0;
                int.TryParse(id.Substring("mall_elevator_".Length), out floor);
                Elevator?.OpenFloorSelector(floor);
                return;
            }

            // 2. Smart Interactive Mall Staff & Strolling Visitors
            if (id.StartsWith("mall_npc_", System.StringComparison.Ordinal))
            {
                MallNpcProfile prof = NpcProfiles.TryGetValue(id, out MallNpcProfile found)
                    ? found
                    : new MallNpcProfile
                    {
                        DisplayName = isEn ? "Atlas Mall Visitor" : "Atlas AVM Ziyaretçisi",
                        RoleTr = "Hamidiye Atlas AVM",
                        RoleEn = "Hamidiye Atlas Mall",
                        Lines = new[] { "Atlas AVM'nin içi çok güzel olmuş, her katta gezilecek mağaza var!" }
                    };

                ProceduralHumanoid hum = trig.GetComponentInParent<ProceduralHumanoid>();
                Vector3 playerPos = mode != null ? mode.ActiveWorldPosition : trig.transform.position;
                SmartNpcInteractionPanel.EnsureInstance().OpenForLivingNpc(
                    trig.transform, hum, prof.DisplayName, prof.RoleTr, prof.RoleEn, prof.Lines, playerPos);
                return;
            }

            // 3. CineAtlas Movie Theater Ticket Selector + Live Screening!
            if (id == "mall_cinema_watch")
            {
                MallUiPanels.EnsureInstance().ShowCinemaModal(movieIdx => BeginCinemaExperience(movieIdx, isEn));
                return;
            }

            // 4. Ground Floor Info Desk
            if (id == "mall_info_desk")
            {
                GameFeelAudio.PlayUiClick();
                mode?.RaiseStatusMessage(isEn
                    ? "Atlaspark Guide: 0 Market & StarBean · 1 Waikiki, TeknoCell, D&M · 2 Food Court · 3 CineAtlas & JoyPark · 4 Rooftop!"
                    : "Atlas AVM Rehberi: 0 5M Market & StarBean · 1 Waikiki, Mavi, TeknoCell, Karaca, D&M · 2 Yemek · 3 CineAtlas & JoyPark · 4 Teras!");
                return;
            }

            // 7. JoyPark 3rd-Floor Arcade Machines (balanced odds + 10s cooldown to prevent infinite cash/XP exploit)
            if (id.StartsWith("mall_arcade_", System.StringComparison.Ordinal))
            {
                if (Time.time - _lastArcadePlayTime < 10f)
                {
                    int waitSec = Mathf.CeilToInt(10f - (Time.time - _lastArcadePlayTime));
                    mode?.RaiseStatusMessage(isEn ? $"Machine resetting ({waitSec}s)!" : $"Oyun makinesi hazırlanıyor ({waitSec} sn)!");
                    return;
                }
                _lastArcadePlayTime = Time.time;

                SaveManager sm = SaveManager.Instance;
                if (sm != null && !sm.TrySpend(10))
                {
                    mode?.RaiseStatusMessage(isEn ? "Need 10 ₺ JoyPark token!" : "JoyPark oyun jetonu 10 ₺!");
                    return;
                }

                int roll = Random.Range(0, 100);
                int score, prize, xp;
                if (roll >= 95) { score = Random.Range(9200, 9990); prize = 32; xp = 16; }
                else if (roll >= 80) { score = Random.Range(7400, 9199); prize = 18; xp = 10; }
                else if (roll >= 45) { score = Random.Range(4600, 7399); prize = 10; xp = 6; }
                else { score = Random.Range(1400, 4599); prize = 4; xp = 3; }

                sm?.AddMoney(prize);
                NeighborhoodReputation.AddReputation(xp);
                DailyQuestAndAchievementSystem.RecordNpcChatOrArcade();
                if (prize >= 18) GameFeelAudio.PlayMilestoneFanfare();
                else GameFeelAudio.PlayUiClick();
                GameFeelAudio.SpawnBurst(trig.transform.position + Vector3.up * 1.4f, OurWorldUiKit.AccentGold, 10, 0.55f);
                mode?.RaiseStatusMessage(isEn
                    ? $"JoyPark Score: {score} pts! Prize Ticket +{prize} ₺ (+{xp} XP)"
                    : $"JoyPark Skoru: {score} puan! Ödül Bileti +{prize} ₺ (+{xp} XP)");
                return;
            }

            // 8. 4th Floor Rooftop Panoramic Binoculars (60s XP cooldown to prevent spam)
            if (id == "mall_telescope")
            {
                if (Time.time - _lastTelescopeXpTime >= 60f)
                {
                    _lastTelescopeXpTime = Time.time;
                    GameFeelAudio.PlayCoinChime();
                    NeighborhoodReputation.AddReputation(8);
                }
                else
                {
                    GameFeelAudio.PlayUiClick();
                }
                mode?.RaiseStatusMessage(isEn
                    ? "Rooftop Panorama: Sultanbeyli Lake shines west, Fatih Clock Tower north, and Aydos Farm hills east!"
                    : "Çatı Dürbünü: Batıda Sultanbeyli Göleti, kuzeyde Fatih Saat Kulesi, doğuda Aydos Çiftliği tepeleri görünüyor!");
            }
        }

        private void BeginCinemaExperience(int movieIdx, bool isEn)
        {
            PlayerModeController mode = PlayerModeController.Instance;
            PlayerFootController foot = mode != null ? mode.FootController : null;
            float cinemaFloorY = CinemaSeatWorldPos.y - 0.54f;
            if (foot != null && CinemaSeatWorldPos.sqrMagnitude > 1f)
            {
                foot.SetSeatedState(true);
                foot.transform.SetPositionAndRotation(CinemaSeatWorldPos, Quaternion.Euler(0f, 180f, 0f));
                if (foot.Humanoid != null)
                {
                    foot.Humanoid.SetSitting(true, driving: false);
                    foot.Humanoid.SetSeatContact(cinemaFloorY, 1f);
                }
            }

            MallUiPanels.EnsureInstance().StartCinemaScreening(movieIdx, () =>
            {
                if (foot != null)
                {
                    foot.Humanoid?.SetSitting(false, driving: false);
                    foot.SetSeatedState(false);
                    foot.TeleportTo(new Vector3(CinemaSeatWorldPos.x, cinemaFloorY + 0.06f, CinemaSeatWorldPos.z + 1.45f), Quaternion.identity);
                    foot.SetMovementEnabled(mode == null || !mode.IsModalLocked);
                }

                float energy = movieIdx == 1 ? 50f : 45f;
                int xp = movieIdx == 1 ? 20 : (movieIdx == 2 ? 18 : 15);
                DayNightCycle.Instance?.AdvanceHours(1.5f);
                PlayerNeedsController.Instance?.RestoreEnergy(energy);
                NeighborhoodReputation.AddReputation(xp);
                DailyQuestAndAchievementSystem.RecordNpcChatOrArcade();
                GameFeelAudio.PlayMilestoneFanfare();
                mode?.RaiseStatusMessage(isEn
                    ? $"Movie finished at CineAtlas! (+{Mathf.RoundToInt(energy)} Energy · +{xp} XP · +1.5h)"
                    : $"CineAtlas seansı bitti! (+{Mathf.RoundToInt(energy)} Enerji · +{xp} Saygınlık · +1.5 saat)");
            });
        }

        // =====================================================================
        // ATLASPARK-INSPIRED INTERACTIVE STORE CATALOGS (16 UNIQUE VENUES!)
        // =====================================================================
        private void BuildAllAtlasparkCatalogs()
        {
            _catalogs = new Dictionary<string, MallShopCatalog>(20);

            // FLOOR 0 — 5M Atlas Market (Migros inspired)
            _catalogs["mall_shop_5m_market"] = new MallShopCatalog
            {
                ShopId = "mall_5m",
                TitleTr = "5M Atlas Market · Hipermarket",
                TitleEn = "5M Atlas Market · Hypermarket",
                SubtitleTr = "Zemin Kat · Taze meyve, içecek, atıştırmalık ve bahçe tohumu",
                SubtitleEn = "Ground Floor · Fresh produce, cold drinks, snacks & garden seeds",
                AccentColor = new Color(0.95f, 0.50f, 0.14f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Soğuk Maden Suyu", NameEn = "Sparkling Water", DescTr = "+16 Enerji · Ferahlatıcı", DescEn = "+16 Energy · Refreshing", Price = 10, EnergyRestore = 16f, ConsumeAnim = ConsumeAnimKind.DrinkWater, SwatchColor = new Color(0.24f, 0.78f, 0.92f) },
                    new MallShopItemDef { NameTr = "Taze Meyve Sepeti", NameEn = "Fresh Fruit Basket", DescTr = "+38 Enerji · +8 Saygınlık", DescEn = "+38 Energy · +8 Reputation", Price = 32, EnergyRestore = 38f, ReputationXp = 8, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.92f, 0.36f, 0.24f) },
                    new MallShopItemDef { NameTr = "Bostan & Çiftlik Paketi", NameEn = "Farm Produce Pack", DescTr = "+2 Domates & +2 Çilek · +10 XP", DescEn = "+2 Tomato & +2 Strawberry · +10 XP", Price = 45, ReputationXp = 10, SpecialUpgradeId = "seed_pack", SwatchColor = new Color(0.26f, 0.72f, 0.34f) },
                    new MallShopItemDef { NameTr = "Aile Piknik Kumanyası", NameEn = "Family Picnic Box", DescTr = "+65 Enerji · +12 Saygınlık", DescEn = "+65 Energy · +12 Reputation", Price = 55, EnergyRestore = 65f, ReputationXp = 12, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.95f, 0.70f, 0.20f) }
                }
            };

            // FLOOR 0 — StarBean Coffee (Starbucks inspired)
            _catalogs["mall_shop_starbean"] = new MallShopCatalog
            {
                ShopId = "mall_starbean",
                TitleTr = "StarBean Coffee · Kahve Evi",
                TitleEn = "StarBean Coffee House",
                SubtitleTr = "Zemin Kat · Kahveni al, koltuklara oturup yudumla!",
                SubtitleEn = "Ground Floor · Order fresh coffee & relax in the lounge!",
                AccentColor = new Color(0.08f, 0.48f, 0.30f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Caramel Macchiato", NameEn = "Caramel Macchiato", DescTr = "+34 Enerji · Sıcak Bardak", DescEn = "+34 Energy · Hot Cup", Price = 28, EnergyRestore = 34f, ReputationXp = 4, ConsumeAnim = ConsumeAnimKind.DrinkHotCup, SwatchColor = new Color(0.76f, 0.50f, 0.24f) },
                    new MallShopItemDef { NameTr = "Iced White Mocha", NameEn = "Iced White Mocha", DescTr = "+30 Enerji · Buzlu Kahve", DescEn = "+30 Energy · Iced Coffee", Price = 26, EnergyRestore = 30f, ReputationXp = 4, ConsumeAnim = ConsumeAnimKind.DrinkWater, SwatchColor = new Color(0.18f, 0.62f, 0.44f) },
                    new MallShopItemDef { NameTr = "San Sebastian Cheesecake", NameEn = "San Sebastian Cheesecake", DescTr = "+44 Enerji · Akışkan Çikolatalı", DescEn = "+44 Energy · Warm Chocolate", Price = 36, EnergyRestore = 44f, ReputationXp = 6, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.92f, 0.74f, 0.38f) },
                    new MallShopItemDef { NameTr = "Taze Filtre Kahve", NameEn = "Fresh Filter Coffee", DescTr = "+24 Enerji · Sabah Klasiği", DescEn = "+24 Energy · Morning Roast", Price = 20, EnergyRestore = 24f, ConsumeAnim = ConsumeAnimKind.DrinkHotCup, SwatchColor = new Color(0.36f, 0.22f, 0.14f) }
                }
            };

            // FLOOR 0 — Kahve Diyarı (Kahve Dünyası inspired)
            _catalogs["mall_shop_kahve_diyari"] = new MallShopCatalog
            {
                ShopId = "mall_kahvediyari",
                TitleTr = "Kahve Diyarı · Kahve & Çikolata",
                TitleEn = "Kahve Diyarı · Turkish Coffee & Chocolate",
                SubtitleTr = "Zemin Kat · Bol köpüklü Türk kahvesi, lokum ve el yapımı çikolata",
                SubtitleEn = "Ground Floor · Authentic Turkish coffee, lokum & artisan chocolate",
                AccentColor = new Color(0.46f, 0.24f, 0.14f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Orta Türk Kahvesi & Lokum", NameEn = "Turkish Coffee & Lokum", DescTr = "+32 Enerji · +6 Saygınlık", DescEn = "+32 Energy · +6 Reputation", Price = 22, EnergyRestore = 32f, ReputationXp = 6, ConsumeAnim = ConsumeAnimKind.DrinkHotCup, SwatchColor = new Color(0.62f, 0.38f, 0.20f) },
                    new MallShopItemDef { NameTr = "Antep Fıstıklı Çikolata", NameEn = "Pistachio Chocolate Box", DescTr = "+36 Enerji · +8 Saygınlık", DescEn = "+36 Energy · +8 Reputation", Price = 30, EnergyRestore = 36f, ReputationXp = 8, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.34f, 0.62f, 0.28f) },
                    new MallShopItemDef { NameTr = "Sıcak Çikolata", NameEn = "Rich Hot Chocolate", DescTr = "+30 Enerji · Kış Favorisi", DescEn = "+30 Energy · Cozy Classic", Price = 25, EnergyRestore = 30f, ConsumeAnim = ConsumeAnimKind.DrinkHotCup, SwatchColor = new Color(0.48f, 0.24f, 0.16f) },
                    new MallShopItemDef { NameTr = "Fıstıklı Dondurma Külahı", NameEn = "Pistachio Ice Cream Cone", DescTr = "+26 Enerji · Serinletici", DescEn = "+26 Energy · Cool Treat", Price = 20, EnergyRestore = 26f, ConsumeAnim = ConsumeAnimKind.EatIceCream, SwatchColor = new Color(0.58f, 0.84f, 0.44f) }
                }
            };

            // FLOOR 0 — Gratis Beauty & Kozmetik
            _catalogs["mall_shop_gratis"] = new MallShopCatalog
            {
                ShopId = "mall_gratis",
                TitleTr = "Gratis Beauty · Kozmetik & Bakım",
                TitleEn = "Gratis Beauty & Personal Care",
                SubtitleTr = "Zemin Kat · Parfüm, cilt bakımı ve mahalle karizma ürünleri",
                SubtitleEn = "Ground Floor · Perfumes, skincare & neighborhood charm",
                AccentColor = new Color(0.66f, 0.26f, 0.68f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "İstanbul Esintisi Parfüm", NameEn = "Istanbul Breeze Perfume", DescTr = "+25 Saygınlık · +15 Enerji", DescEn = "+25 Reputation · +15 Energy", Price = 55, EnergyRestore = 15f, ReputationXp = 25, SwatchColor = new Color(0.76f, 0.34f, 0.78f) },
                    new MallShopItemDef { NameTr = "Ferahlatıcı Bakım Seti", NameEn = "Fresh Skincare Kit", DescTr = "+20 Enerji · +14 Saygınlık", DescEn = "+20 Energy · +14 Reputation", Price = 38, EnergyRestore = 20f, ReputationXp = 14, SwatchColor = new Color(0.32f, 0.78f, 0.72f) },
                    new MallShopItemDef { NameTr = "Nane & Limon Kolonya", NameEn = "Classic Lemon Cologne", DescTr = "+12 Enerji · +10 Saygınlık", DescEn = "+12 Energy · +10 Reputation", Price = 20, EnergyRestore = 12f, ReputationXp = 10, SwatchColor = new Color(0.95f, 0.82f, 0.26f) },
                    new MallShopItemDef { NameTr = "Hediye Kozmetik Kutusu", NameEn = "Deluxe Gift Box", DescTr = "+38 Saygınlık · Mahalle Yıldızı", DescEn = "+38 Reputation · Local Star", Price = 75, ReputationXp = 38, SwatchColor = new Color(0.92f, 0.42f, 0.56f) }
                }
            };

            // FLOOR 0 — Atasun Optik & Aksesuar
            _catalogs["mall_shop_optik"] = new MallShopCatalog
            {
                ShopId = "mall_optik",
                TitleTr = "Atlas Optik & Saat",
                TitleEn = "Atlas Optics & Watches",
                SubtitleTr = "Zemin Kat · Güneş gözlüğü ve şık kol saatleri",
                SubtitleEn = "Ground Floor · Designer sunglasses & wristwatches",
                AccentColor = new Color(0.16f, 0.52f, 0.78f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Polarize Güneş Gözlüğü", NameEn = "Polarized Sunglasses", DescTr = "3D Gözlük Takılır! · +28 Saygınlık", DescEn = "Equips 3D Sunglasses! · +28 Reputation", Price = 60, ReputationXp = 28, SpecialUpgradeId = "sunglasses", SwatchColor = new Color(0.18f, 0.56f, 0.84f) },
                    new MallShopItemDef { NameTr = "Klasik Çelik Saat", NameEn = "Classic Steel Watch", DescTr = "+45 Saygınlık · Prestij", DescEn = "+45 Reputation · Prestige", Price = 95, ReputationXp = 45, SwatchColor = new Color(0.86f, 0.70f, 0.26f) }
                }
            };

            // FLOOR 0 — Eczane Atlas
            _catalogs["mall_shop_eczane"] = new MallShopCatalog
            {
                ShopId = "mall_eczane",
                TitleTr = "Eczane Atlas · Sağlık & Vitamin",
                TitleEn = "Atlas Pharmacy & Wellness",
                SubtitleTr = "Zemin Kat · Anında enerji yenileyen vitamin ve sağlık destekleri",
                SubtitleEn = "Ground Floor · Instant energy vitamins & wellness packs",
                AccentColor = new Color(0.86f, 0.18f, 0.20f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "C Vitamini & Elektrolit", NameEn = "Vitamin C & Electrolytes", DescTr = "+55 Enerji · Anında Canlanma", DescEn = "+55 Energy · Fast Boost", Price = 25, EnergyRestore = 55f, ConsumeAnim = ConsumeAnimKind.DrinkWater, SwatchColor = new Color(0.96f, 0.62f, 0.16f) },
                    new MallShopItemDef { NameTr = "Tam Enerji Multivitamin", NameEn = "Full-Recovery Multivitamin", DescTr = "+100 Tam Enerji Yenileme!", DescEn = "+100 Full Energy Restore!", Price = 42, EnergyRestore = 100f, ConsumeAnim = ConsumeAnimKind.DrinkWater, SwatchColor = new Color(0.88f, 0.22f, 0.24f) }
                }
            };

            // FLOOR 0 — Mado & Waffle Stop Kiosk
            _catalogs["mall_shop_waffle"] = new MallShopCatalog
            {
                ShopId = "mall_waffle",
                TitleTr = "Mado & Waffle Stop · Atrium",
                TitleEn = "Mado & Waffle Stop · Atrium",
                SubtitleTr = "Zemin Kat · Maraş dondurması ve bol meyveli sıcak waffle",
                SubtitleEn = "Ground Floor · Traditional Maraş ice cream & Belgian waffles",
                AccentColor = new Color(0.92f, 0.44f, 0.56f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Maraş Kesme Dondurma", NameEn = "Maraş Traditional Ice Cream", DescTr = "+30 Enerji · 3 Top", DescEn = "+30 Energy · 3 Scoops", Price = 24, EnergyRestore = 30f, ConsumeAnim = ConsumeAnimKind.EatIceCream, SwatchColor = new Color(0.96f, 0.88f, 0.72f) },
                    new MallShopItemDef { NameTr = "Çilekli & Fıstıklı Waffle", NameEn = "Strawberry Pistachio Waffle", DescTr = "+46 Enerji · +6 Saygınlık", DescEn = "+46 Energy · +6 Reputation", Price = 38, EnergyRestore = 46f, ReputationXp = 6, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.90f, 0.34f, 0.44f) }
                }
            };

            // FLOOR 1 — Mavi Denim & Kotonica (Includes permanent Running Shoes upgrade!)
            _catalogs["mall_shop_mavi_koton"] = new MallShopCatalog
            {
                ShopId = "mall_mavi",
                TitleTr = "Mavi Denim & Kotonica · Sokak Modası",
                TitleEn = "Mavi Denim & Kotonica · Streetwear",
                SubtitleTr = "1. Kat · Denim koleksiyonu ve kalıcı Koşu Ayakkabısı yükseltmesi",
                SubtitleEn = "1st Floor · Denim collection & permanent Running Shoes upgrade",
                AccentColor = new Color(0.14f, 0.36f, 0.72f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Pro Koşu Ayakkabısı (Kalıcı)", NameEn = "Pro Running Shoes (Permanent)", DescTr = "Kalıcı hızlı koşu yükseltmesi! · +30 XP", DescEn = "Permanent sprint upgrade! · +30 XP", Price = 120, ReputationXp = 30, SpecialUpgradeId = "running_shoes", SwatchColor = new Color(0.14f, 0.78f, 0.70f) },
                    new MallShopItemDef { NameTr = "İstanbul Denim Kombin", NameEn = "Istanbul Denim Outfit", DescTr = "+32 Saygınlık · Yeni Sezon", DescEn = "+32 Reputation · New Season", Price = 65, ReputationXp = 32, SwatchColor = new Color(0.18f, 0.38f, 0.76f) },
                    new MallShopItemDef { NameTr = "Spor Şapka & Çanta", NameEn = "Street Cap & Backpack", DescTr = "3D Şapka Takılır! · +20 Saygınlık · +15 Enerji", DescEn = "Equips 3D Cap! · +20 Reputation · +15 Energy", Price = 45, EnergyRestore = 15f, ReputationXp = 20, SpecialUpgradeId = "street_cap", SwatchColor = new Color(0.86f, 0.56f, 0.18f) },
                    new MallShopItemDef { NameTr = "Kışlık Kaşkol & Eldiven", NameEn = "Winter Scarf & Gloves", DescTr = "+18 Saygınlık · +10 Enerji", DescEn = "+18 Reputation · +10 Energy", Price = 35, EnergyRestore = 10f, ReputationXp = 18, SwatchColor = new Color(0.68f, 0.22f, 0.26f) }
                }
            };

            // FLOOR 1 — TeknoCell & Elektronik (Includes permanent Turbo Kit!)
            _catalogs["mall_shop_teknocell"] = new MallShopCatalog
            {
                ShopId = "mall_teknocell",
                TitleTr = "TeknoCell · Elektronik & Akıllı Cihazlar",
                TitleEn = "TeknoCell · Electronics & Gadgets",
                SubtitleTr = "1. Kat · Telefon, kulaklık, konsol ve araç performans çipi",
                SubtitleEn = "1st Floor · Smartphones, audio, consoles & car ECU Turbo Kit",
                AccentColor = new Color(0.96f, 0.76f, 0.12f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Araç Turbo ECU Kiti (Kalıcı)", NameEn = "Vehicle Turbo ECU Kit (Perm)", DescTr = "Tüm araçlara kalıcı turbo hız! · +35 XP", DescEn = "Permanent turbo boost for cars! · +35 XP", Price = 180, ReputationXp = 35, SpecialUpgradeId = "turbo_kit", SwatchColor = new Color(0.96f, 0.42f, 0.24f) },
                    new MallShopItemDef { NameTr = "Kablosuz Pro Kulaklık", NameEn = "Wireless Pro Earbuds", DescTr = "+28 Saygınlık · +15 Enerji", DescEn = "+28 Reputation · +15 Energy", Price = 70, EnergyRestore = 15f, ReputationXp = 28, SwatchColor = new Color(0.24f, 0.72f, 0.96f) },
                    new MallShopItemDef { NameTr = "Akıllı Spor Saat", NameEn = "Smart Fitness Watch", DescTr = "+34 Saygınlık · +20 Enerji", DescEn = "+34 Reputation · +20 Energy", Price = 85, EnergyRestore = 20f, ReputationXp = 34, SwatchColor = new Color(0.14f, 0.80f, 0.68f) },
                    new MallShopItemDef { NameTr = "Taşınabilir Oyun Konsolu", NameEn = "Handheld Gaming Console", DescTr = "+48 Saygınlık · +25 Enerji", DescEn = "+48 Reputation · +25 Energy", Price = 125, EnergyRestore = 25f, ReputationXp = 48, SwatchColor = new Color(0.96f, 0.76f, 0.18f) }
                }
            };

            // FLOOR 1 — Karaca Home & English Living
            _catalogs["mall_shop_karaca"] = new MallShopCatalog
            {
                ShopId = "mall_karaca",
                TitleTr = "Karaca Home & English Living",
                TitleEn = "Karaca Home & English Living",
                SubtitleTr = "1. Kat · Ev dekorasyonu, porselen fincan ve çay takımları",
                SubtitleEn = "1st Floor · Home decor, porcelain coffee sets & cozy textiles",
                AccentColor = new Color(0.84f, 0.62f, 0.56f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "İnce Belli Çay Takımı", NameEn = "Turkish Tea Glass Set", DescTr = "+24 Saygınlık · No:48 Evine Yakışır", DescEn = "+24 Reputation · Great for Home", Price = 50, ReputationXp = 24, SwatchColor = new Color(0.88f, 0.66f, 0.24f) },
                    new MallShopItemDef { NameTr = "Porselen Kahve Fincanı", NameEn = "Porcelain Coffee Set", DescTr = "+30 Saygınlık · Altın Yaldızlı", DescEn = "+30 Reputation · Gold Trim", Price = 65, ReputationXp = 30, SwatchColor = new Color(0.86f, 0.56f, 0.62f) },
                    new MallShopItemDef { NameTr = "Yumuşak Ev Battaniyesi", NameEn = "Cozy Woven Throw", DescTr = "+20 Enerji · +20 Saygınlık", DescEn = "+20 Energy · +20 Reputation", Price = 48, EnergyRestore = 20f, ReputationXp = 20, SwatchColor = new Color(0.56f, 0.74f, 0.78f) },
                    new MallShopItemDef { NameTr = "Akıllı Kahve Makinesi", NameEn = "Turkish Coffee Maker", DescTr = "+45 Saygınlık · +25 Enerji", DescEn = "+45 Reputation · +25 Energy", Price = 110, EnergyRestore = 25f, ReputationXp = 45, SwatchColor = new Color(0.28f, 0.30f, 0.36f) }
                }
            };

            // FLOOR 1 — D&M Kitap, Müzik & Kültür (D&R inspired)
            _catalogs["mall_shop_dm_kitap"] = new MallShopCatalog
            {
                ShopId = "mall_dm",
                TitleTr = "D&M · Kitap, Müzik & Kültür",
                TitleEn = "D&M · Books, Vinyl & Culture",
                SubtitleTr = "1. Kat · Romanlar, İstanbul rehberleri ve nostaljik plaklar",
                SubtitleEn = "1st Floor · Novels, Istanbul guides & classic vinyl records",
                AccentColor = new Color(0.20f, 0.44f, 0.76f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "İstanbul Mahalle Tarihi", NameEn = "History of Istanbul", DescTr = "+22 Saygınlık · +15 Enerji", DescEn = "+22 Reputation · +15 Energy", Price = 35, EnergyRestore = 15f, ReputationXp = 22, SwatchColor = new Color(0.24f, 0.52f, 0.84f) },
                    new MallShopItemDef { NameTr = "Anadolu Rock Nostalji Plağı", NameEn = "Anatolian Rock Vinyl", DescTr = "+35 Saygınlık · +20 Enerji", DescEn = "+35 Reputation · +20 Energy", Price = 65, EnergyRestore = 20f, ReputationXp = 35, SwatchColor = new Color(0.88f, 0.56f, 0.16f) },
                    new MallShopItemDef { NameTr = "Satranç & Tavla Seti", NameEn = "Backgammon & Chess Set", DescTr = "+28 Saygınlık · Kıraathane Klasiği", DescEn = "+28 Reputation · Tea House Classic", Price = 55, ReputationXp = 28, SwatchColor = new Color(0.58f, 0.38f, 0.22f) },
                    new MallShopItemDef { NameTr = "Çizim & Defter Seti", NameEn = "Sketchbook & Pen Set", DescTr = "+16 Saygınlık · +12 Enerji", DescEn = "+16 Reputation · +12 Energy", Price = 28, EnergyRestore = 12f, ReputationXp = 16, SwatchColor = new Color(0.18f, 0.68f, 0.60f) }
                }
            };

            // FLOOR 2 — Kral Burger (Burger King inspired)
            _catalogs["mall_food_burger"] = new MallShopCatalog
            {
                ShopId = "mall_burger",
                TitleTr = "Kral Burger · Yemek Katı",
                TitleEn = "Kral Burger · Food Court",
                SubtitleTr = "2. Kat · Alevde ızgara burgerler, çıtır patates ve soğuk içecek",
                SubtitleEn = "2nd Floor · Flame-grilled burgers, crispy fries & cold drinks",
                AccentColor = new Color(0.92f, 0.32f, 0.16f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Kral Double Menü", NameEn = "Double King Meal", DescTr = "+62 Enerji · Patates + İçecek", DescEn = "+62 Energy · Fries + Drink", Price = 48, EnergyRestore = 62f, ReputationXp = 6, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.92f, 0.32f, 0.16f) },
                    new MallShopItemDef { NameTr = "Çıtır Tavuk Burger Menü", NameEn = "Crispy Chicken Meal", DescTr = "+48 Enerji · Sıcak Menü", DescEn = "+48 Energy · Hot Meal", Price = 38, EnergyRestore = 48f, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.95f, 0.66f, 0.18f) },
                    new MallShopItemDef { NameTr = "Soğan Halkası & Patates", NameEn = "Onion Rings & Fries", DescTr = "+28 Enerji · Atıştırmalık", DescEn = "+28 Energy · Snack", Price = 22, EnergyRestore = 28f, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.88f, 0.72f, 0.24f) },
                    new MallShopItemDef { NameTr = "Çikolatalı Sundae", NameEn = "Chocolate Sundae", DescTr = "+24 Enerji · Tatlı", DescEn = "+24 Energy · Dessert", Price = 18, EnergyRestore = 24f, ConsumeAnim = ConsumeAnimKind.EatIceCream, SwatchColor = new Color(0.46f, 0.26f, 0.18f) }
                }
            };

            // FLOOR 2 — Tavuk Diyarı (Tavuk Dünyası inspired)
            _catalogs["mall_food_tavuk"] = new MallShopCatalog
            {
                ShopId = "mall_tavuk",
                TitleTr = "Tavuk Diyarı · Şefin Tavası",
                TitleEn = "Tavuk Diyarı · Chef's Skillet",
                SubtitleTr = "2. Kat · Kekikli tavuk, kremalı makarna ve Akdeniz salatası",
                SubtitleEn = "2nd Floor · Herb-marinated chicken, creamy pasta & fresh salad",
                AccentColor = new Color(0.90f, 0.56f, 0.12f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Kekiklim Şef Tabağı", NameEn = "Thyme Chef's Plate", DescTr = "+66 Enerji · Makarna + Salata", DescEn = "+66 Energy · Pasta + Salad", Price = 52, EnergyRestore = 66f, ReputationXp = 8, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.90f, 0.58f, 0.14f) },
                    new MallShopItemDef { NameTr = "Köri Soslu Tavuk Tava", NameEn = "Curry Chicken Skillet", DescTr = "+60 Enerji · Sıcak Tava", DescEn = "+60 Energy · Sizzling Pan", Price = 48, EnergyRestore = 60f, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.96f, 0.76f, 0.20f) },
                    new MallShopItemDef { NameTr = "Barbekü Soslu Tavuk", NameEn = "BBQ Grilled Chicken", DescTr = "+56 Enerji · Doyurucu", DescEn = "+56 Energy · Hearty", Price = 46, EnergyRestore = 56f, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.76f, 0.26f, 0.18f) },
                    new MallShopItemDef { NameTr = "Ev Yapımı Limonata", NameEn = "Homemade Mint Lemonade", DescTr = "+22 Enerji · Buz Gibi", DescEn = "+22 Energy · Ice Cold", Price = 16, EnergyRestore = 22f, ConsumeAnim = ConsumeAnimKind.DrinkWater, SwatchColor = new Color(0.95f, 0.88f, 0.26f) }
                }
            };

            // FLOOR 2 — Bereket Döner
            _catalogs["mall_food_doner"] = new MallShopCatalog
            {
                ShopId = "mall_doner",
                TitleTr = "Bereket Döner · Yaprak Döner",
                TitleEn = "Bereket Döner · Turkish Grill",
                SubtitleTr = "2. Kat · Tereyağlı İskender, yaprak dürüm ve yayık ayran",
                SubtitleEn = "2nd Floor · Sizzling İskender, döner wraps & cold ayran",
                AccentColor = new Color(0.80f, 0.18f, 0.18f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Tereyağlı İskender & Ayran", NameEn = "Butter İskender & Ayran", DescTr = "+72 Enerji · +10 Saygınlık", DescEn = "+72 Energy · +10 Reputation", Price = 58, EnergyRestore = 72f, ReputationXp = 10, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.82f, 0.20f, 0.18f) },
                    new MallShopItemDef { NameTr = "Yaprak Et Döner Dürüm", NameEn = "Beef Döner Wrap", DescTr = "+52 Enerji · Bol Malzemeli", DescEn = "+52 Energy · Fresh Wrap", Price = 42, EnergyRestore = 52f, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.86f, 0.48f, 0.20f) },
                    new MallShopItemDef { NameTr = "Pilav Üstü Döner", NameEn = "Döner Over Butter Rice", DescTr = "+60 Enerji · Geleneksel", DescEn = "+60 Energy · Classic", Price = 48, EnergyRestore = 60f, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.92f, 0.84f, 0.62f) },
                    new MallShopItemDef { NameTr = "Köpüklü Yayık Ayran", NameEn = "Frothy Cold Ayran", DescTr = "+20 Enerji · Ferah", DescEn = "+20 Energy · Refreshing", Price = 14, EnergyRestore = 20f, ConsumeAnim = ConsumeAnimKind.DrinkWater, SwatchColor = new Color(0.94f, 0.96f, 0.98f) }
                }
            };

            // FLOOR 2 — Pizza Lazza
            _catalogs["mall_food_pizza"] = new MallShopCatalog
            {
                ShopId = "mall_pizza",
                TitleTr = "Pizza Lazza · Taş Fırın Pizza",
                TitleEn = "Pizza Lazza · Stone-Oven Pizza",
                SubtitleTr = "2. Kat · İnce hamurlu sıcak pizzalar ve sufle",
                SubtitleEn = "2nd Floor · Hot stone-baked pizzas & warm chocolate soufflé",
                AccentColor = new Color(0.18f, 0.56f, 0.28f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Karışık Sultan Pizza", NameEn = "Supreme Turkish Pizza", DescTr = "+64 Enerji · Bol Malzemeli", DescEn = "+64 Energy · Loaded", Price = 50, EnergyRestore = 64f, ReputationXp = 6, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.86f, 0.26f, 0.20f) },
                    new MallShopItemDef { NameTr = "Margarita & Sarımsaklı Ekmek", NameEn = "Margherita & Garlic Bread", DescTr = "+48 Enerji · Fesleğenli", DescEn = "+48 Energy · Fresh Basil", Price = 38, EnergyRestore = 48f, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.24f, 0.66f, 0.34f) },
                    new MallShopItemDef { NameTr = "Sıcak Çikolatalı Sufle", NameEn = "Warm Chocolate Soufflé", DescTr = "+34 Enerji · Tatlı Keyfi", DescEn = "+34 Energy · Sweet Treat", Price = 26, EnergyRestore = 34f, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.46f, 0.24f, 0.16f) },
                    new MallShopItemDef { NameTr = "Buzlu İçecek", NameEn = "Chilled Soft Drink", DescTr = "+16 Enerji", DescEn = "+16 Energy", Price = 12, EnergyRestore = 16f, ConsumeAnim = ConsumeAnimKind.DrinkWater, SwatchColor = new Color(0.24f, 0.62f, 0.90f) }
                }
            };

            // FLOOR 2 — Cajun Corner & Kovada Pilav
            _catalogs["mall_food_cajun"] = new MallShopCatalog
            {
                ShopId = "mall_cajun",
                TitleTr = "Cajun Corner & Kovada Pilav",
                TitleEn = "Cajun Corner & Pilav Bowl",
                SubtitleTr = "2. Kat · Çıtır cajun tavuk sepeti ve nohutlu tavuk pilav",
                SubtitleEn = "2nd Floor · Crispy cajun chicken buckets & chickpea rice bowls",
                AccentColor = new Color(0.92f, 0.46f, 0.14f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Çıtır Cajun Tavuk Sepeti", NameEn = "Crispy Cajun Chicken Basket", DescTr = "+56 Enerji · Baharatlı Soslu", DescEn = "+56 Energy · Spiced Dip", Price = 44, EnergyRestore = 56f, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.92f, 0.46f, 0.14f) },
                    new MallShopItemDef { NameTr = "Kovada Nohutlu Tavuk Pilav", NameEn = "Chickpea Chicken Rice Bowl", DescTr = "+54 Enerji · Turşu + Ayran", DescEn = "+54 Energy · Pickles + Ayran", Price = 38, EnergyRestore = 54f, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.94f, 0.82f, 0.42f) }
                }
            };

            // FLOOR 2 — Sütlüce Kadayıf & Alaçatı Muhallebi
            _catalogs["mall_food_baklava"] = new MallShopCatalog
            {
                ShopId = "mall_baklava",
                TitleTr = "Sütlüce Kadayıf & Alaçatı Muhallebi",
                TitleEn = "Sütlüce Kadayıf & Alaçatı Sweets",
                SubtitleTr = "2. Kat · Sıcak künefe, fıstıklı kadayıf, sakızlı muhallebi ve çay",
                SubtitleEn = "2nd Floor · Hot künefe, pistachio kadayıf, mastic pudding & tea",
                AccentColor = new Color(0.14f, 0.64f, 0.60f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Sıcak Antep Fıstıklı Künefe", NameEn = "Hot Pistachio Künefe", DescTr = "+52 Enerji · +10 Saygınlık", DescEn = "+52 Energy · +10 Reputation", Price = 42, EnergyRestore = 52f, ReputationXp = 10, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.90f, 0.66f, 0.18f) },
                    new MallShopItemDef { NameTr = "Fıstıklı Kadayıf & Kaymak", NameEn = "Pistachio Kadayıf & Cream", DescTr = "+46 Enerji · +8 Saygınlık", DescEn = "+46 Energy · +8 Reputation", Price = 36, EnergyRestore = 46f, ReputationXp = 8, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.42f, 0.72f, 0.28f) },
                    new MallShopItemDef { NameTr = "Damla Sakızlı Muhallebi", NameEn = "Alaçatı Mastic Pudding", DescTr = "+36 Enerji · Hafif Sütlü Tatlı", DescEn = "+36 Energy · Light Milk Dessert", Price = 28, EnergyRestore = 36f, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.94f, 0.92f, 0.86f) },
                    new MallShopItemDef { NameTr = "İnce Belli Demli Çay", NameEn = "Fresh Brewed Turkish Tea", DescTr = "+18 Enerji · Tavşan Kanı", DescEn = "+18 Energy · Fresh Tea", Price = 10, EnergyRestore = 18f, ConsumeAnim = ConsumeAnimKind.DrinkHotCup, SwatchColor = new Color(0.76f, 0.24f, 0.16f) }
                }
            };

            // FLOOR 4 — Ennjoy Lounge & Teras Kafe
            _catalogs["mall_food_terrace"] = new MallShopCatalog
            {
                ShopId = "mall_terrace",
                TitleTr = "Ennjoy Lounge · Açık Çatı Teras Kafe",
                TitleEn = "Ennjoy Lounge · Rooftop Sky Cafe",
                SubtitleTr = "4. Kat Teras · Sultanbeyli ve Aydos manzarasına karşı kahve & serpme keyfi",
                SubtitleEn = "4th Floor Roof · Coffee, brunch & desserts overlooking Sultanbeyli",
                AccentColor = new Color(0.96f, 0.72f, 0.22f),
                Items = new[]
                {
                    new MallShopItemDef { NameTr = "Teras Signature Latte", NameEn = "Rooftop Signature Latte", DescTr = "+38 Enerji · +8 Saygınlık", DescEn = "+38 Energy · +8 Reputation", Price = 30, EnergyRestore = 38f, ReputationXp = 8, ConsumeAnim = ConsumeAnimKind.DrinkHotCup, SwatchColor = new Color(0.84f, 0.58f, 0.26f) },
                    new MallShopItemDef { NameTr = "Aydos Serpme Kahvaltı", NameEn = "Aydos Rooftop Brunch", DescTr = "+85 Enerji · +18 Saygınlık", DescEn = "+85 Energy · +18 Reputation", Price = 68, EnergyRestore = 85f, ReputationXp = 18, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.96f, 0.74f, 0.24f) },
                    new MallShopItemDef { NameTr = "Buzlu Çilekli Limonata", NameEn = "Iced Strawberry Lemonade", DescTr = "+30 Enerji · Gün Batımı İçeceği", DescEn = "+30 Energy · Sunset Cooler", Price = 24, EnergyRestore = 30f, ConsumeAnim = ConsumeAnimKind.DrinkWater, SwatchColor = new Color(0.92f, 0.34f, 0.44f) },
                    new MallShopItemDef { NameTr = "Teras Meyve & Waffle Tabağı", NameEn = "Terrace Fruit & Waffle Platter", DescTr = "+52 Enerji · +10 Saygınlık", DescEn = "+52 Energy · +10 Reputation", Price = 44, EnergyRestore = 52f, ReputationXp = 10, ConsumeAnim = ConsumeAnimKind.EatFood, SwatchColor = new Color(0.24f, 0.74f, 0.66f) }
                }
            };
        }
    }
}
