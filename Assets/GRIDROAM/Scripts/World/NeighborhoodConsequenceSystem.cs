using System;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    public enum NeighborhoodOffenseKind
    {
        AssaultCitizen = 0,
        AssaultStaffOrAuthority = 1,
        HarmAnimal = 2,
        VehicleDamage = 3,
        PropertyVandalism = 4,
        HitPedestrianWithCar = 5,
        TrafficViolation = 6,
        CarTheft = 7,
        NoiseDisturbance = 8
    }

    /// <summary>
    /// Universal consequence, witness escalation, world-surface impact feedback, and
    /// fallback humanoid/animal reaction coordinator:
    /// - Every punch against ANY living creature or ANY world object produces realistic
    ///   physical, audio, and visual feedback (metal clang, wood thud, glass clink, leaf burst,
    ///   flying crates, slammed doors, bruised knuckles on heavy concrete hits).
    /// - Every harmful act (assaulting citizens/staff, harming dogs/cats/farm animals,
    ///   smashing cars, vandalizing ATMs/vending machines/shop windows) has a tangible price:
    ///   Reputation XP loss, Zabıta / Security fines, and escalating heat interventions.
    /// </summary>
    public sealed class NeighborhoodConsequenceSystem : MonoBehaviour
    {
        public static NeighborhoodConsequenceSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private AudioSource _sfx;
        private AudioClip _dogBarkClip;
        private AudioClip _catHissClip;
        private AudioClip _farmAlarmClip;
        private AudioClip _metalClangClip;
        private AudioClip _woodThudClip;
        private AudioClip _glassClinkClip;
        private AudioClip _stoneThumpClip;
        private AudioClip _whistleClip;

        private float _heatScore;
        public float CurrentHeat => _heatScore;
        private float _lastOffenseTime = -99f;
        private float _lastVandalFineTime = -99f;
        private float _lastCarFineTime = -99f;
        private float _lastHumanoidHitTime = -99f;
        private float _lastKnuckleHurtTime = -99f;
        private int _carSmashCount;
        private int _glassHitCount;

        public static NeighborhoodConsequenceSystem EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_NeighborhoodConsequenceSystem");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<NeighborhoodConsequenceSystem>();
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
            BuildAudioBank();
        }

        private void OnDestroy()
        {
            if (_dogBarkClip != null) Destroy(_dogBarkClip);
            if (_catHissClip != null) Destroy(_catHissClip);
            if (_farmAlarmClip != null) Destroy(_farmAlarmClip);
            if (_metalClangClip != null) Destroy(_metalClangClip);
            if (_woodThudClip != null) Destroy(_woodThudClip);
            if (_glassClinkClip != null) Destroy(_glassClinkClip);
            if (_stoneThumpClip != null) Destroy(_stoneThumpClip);
            if (_whistleClip != null) Destroy(_whistleClip);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_heatScore > 0f && Time.time - _lastOffenseTime > 12f)
            {
                _heatScore = Mathf.Max(0f, _heatScore - Time.deltaTime * 0.045f);
            }
            if (_carSmashCount > 0 && Time.time - _lastOffenseTime > 30f) _carSmashCount = 0;
            if (_glassHitCount > 0 && Time.time - _lastOffenseTime > 30f) _glassHitCount = 0;
        }

        public static void CoolDownHeatOnRecovery()
        {
            if (Instance != null)
            {
                Instance._heatScore = 0f;
                Instance._carSmashCount = 0;
                Instance._glassHitCount = 0;
            }
        }

        // =====================================================================
        // AUDIO TRIGGERS
        // =====================================================================
        public void PlayDogBark(float vol = 0.72f) => PlayClip(_dogBarkClip, vol, UnityEngine.Random.Range(0.92f, 1.08f));
        public void PlayCatHiss(float vol = 0.62f) => PlayClip(_catHissClip, vol, UnityEngine.Random.Range(0.94f, 1.12f));
        public void PlayFarmAlarm(float vol = 0.65f) => PlayClip(_farmAlarmClip, vol, UnityEngine.Random.Range(0.86f, 1.10f));
        public void PlayMetalClang(float vol = 0.58f) => PlayClip(_metalClangClip, vol, UnityEngine.Random.Range(0.90f, 1.12f));
        public void PlayWoodThud(float vol = 0.54f) => PlayClip(_woodThudClip, vol, UnityEngine.Random.Range(0.88f, 1.08f));
        public void PlayGlassClink(float vol = 0.62f) => PlayClip(_glassClinkClip, vol, UnityEngine.Random.Range(0.95f, 1.15f));
        public void PlayStoneThump(float vol = 0.50f) => PlayClip(_stoneThumpClip, vol, UnityEngine.Random.Range(0.85f, 1.05f));
        public void PlayZabitaWhistle(float vol = 0.66f) => PlayClip(_whistleClip, vol, 1.0f);

        private void PlayClip(AudioClip clip, float vol, float pitch)
        {
            if (_sfx == null || clip == null) return;
            _sfx.pitch = pitch;
            _sfx.PlayOneShot(clip, vol);
        }

        // =====================================================================
        // CONSEQUENCE & ESCALATING HEAT TRACKER
        // =====================================================================
        public void ReportOffense(
            NeighborhoodOffenseKind kind,
            Vector3 worldPos,
            string targetLabel,
            int repPenalty,
            int immediateFineTl = 0,
            float heatDelta = 1.0f)
        {
            _lastOffenseTime = Time.time;
            _heatScore = Mathf.Clamp(_heatScore + heatDelta, 0f, 8f);

            if (repPenalty > 0)
            {
                NeighborhoodReputation.DeductReputation(repPenalty);
            }

            bool en = LocalizationService.IsEnglish;
            PlayerModeController mode = PlayerModeController.Instance;

            int paidImmediate = ApplyFine(immediateFineTl, worldPos);
            if (paidImmediate > 0 && mode != null)
            {
                string target = string.IsNullOrEmpty(targetLabel) ? (en ? "Property" : "Çevre") : targetLabel;
                mode.RaiseStatusMessage(en
                    ? $"🚨 Consequence ({target}): -{paidImmediate} ₺ fine · -{repPenalty} Reputation!"
                    : $"🚨 Ceza ({target}): -{paidImmediate} ₺ para cezası · -{repPenalty} Saygınlık!");
            }

            // Escalating Zabıta / Neighborhood Security intervention for repeated chaos
            if (_heatScore >= 5.2f)
            {
                _heatScore = 1.2f;
                PlayZabitaWhistle(0.75f);
                int heavyFine = ApplyFine(95, worldPos);
                NeighborhoodReputation.DeductReputation(20);
                string finePartEn = heavyFine > 0 ? $"-{heavyFine} ₺ fine · " : string.Empty;
                string finePartTr = heavyFine > 0 ? $"-{heavyFine} ₺ · " : string.Empty;
                mode?.RaiseStatusMessage(en
                    ? $"🚨 MUNICIPAL SECURITY INTERVENED! Repeated disturbance: {finePartEn}-20 Reputation!"
                    : $"🚨 MAHALLE ZABITASI MÜDAHALE ETTİ! Sürekli taşkınlık cezası: {finePartTr}-20 Saygınlık!");
                if (PlayerNeedsController.Instance != null && mode != null && mode.CurrentMode == ControlMode.OnFoot)
                {
                    PlayerNeedsController.Instance.TakeDamage(
                        18f,
                        worldPos + Vector3.forward * 1.5f,
                        en ? "Neighborhood Security" : "Mahalle Zabıtası",
                        heavyHit: true);
                }
            }
            else if (_heatScore >= 3.0f && Time.time - _lastVandalFineTime > 12f)
            {
                _lastVandalFineTime = Time.time;
                PlayZabitaWhistle(0.62f);
                int fine = ApplyFine(45, worldPos);
                NeighborhoodReputation.DeductReputation(10);
                mode?.RaiseStatusMessage(fine > 0
                    ? (en
                        ? $"🚨 Neighborhood Security fined you -{fine} ₺ for causing trouble! (-10 Reputation)"
                        : $"🚨 Mahalle güvenliği taşkınlık nedeniyle -{fine} ₺ ceza kesti! (-10 Saygınlık)")
                    : (en
                        ? "🚨 Neighborhood Security issued an official warning! (-10 Reputation)"
                        : "🚨 Mahalle güvenliği taşkınlık nedeniyle resmi ihtar kesti! (-10 Saygınlık)"));
            }
            else if (kind == NeighborhoodOffenseKind.VehicleDamage)
            {
                _carSmashCount++;
                if (_carSmashCount >= 2 && Time.time - _lastCarFineTime > 14f)
                {
                    _lastCarFineTime = Time.time;
                    _carSmashCount = 0;
                    PlayZabitaWhistle(0.58f);
                    int carFine = ApplyFine(50, worldPos);
                    mode?.RaiseStatusMessage(carFine > 0
                        ? (en
                            ? $"🚨 Car owner reported vandalism! Vehicle damage fine: -{carFine} ₺"
                            : $"🚨 Araç sahibi şikayet etti! Hasar tazminatı: -{carFine} ₺")
                        : (en
                            ? "🚨 Car owner reported vandalism to Neighborhood Security!"
                            : "🚨 Araç sahibi hasar nedeniyle mahalle güvenliğine şikayet etti!"));
                }
            }
        }

        private static int ApplyFine(int amount, Vector3 worldPos)
        {
            if (amount <= 0 || SaveManager.Instance == null || SaveManager.Instance.Data == null) return 0;
            int actual = Mathf.Min(amount, SaveManager.Instance.Data.money);
            if (actual > 0)
            {
                SaveManager.Instance.TrySpend(actual);
                Vector3 popPos = PlayerModeController.Instance != null
                    ? PlayerModeController.Instance.ActiveWorldPosition + Vector3.up * 2.45f
                    : worldPos + Vector3.up * 1.8f;
                VisualEffectsManager.SpawnFloatingText(popPos, $"-{actual} ₺", new Color(1.0f, 0.30f, 0.24f), scale: 1.05f, duration: 1.15f);
            }
            return actual;
        }

        // =====================================================================
        // UNIVERSAL FALLBACK: HIT ANY OTHER HUMANOID NPC (AVM STAFF, STORY NPCs,
        // INSTRUCTOR USTA HALUK, DEALERSHIP KEMAL BEY, ETC.)
        // =====================================================================
        public bool TryPunchAnyOtherHumanoid(
            Vector3 strikeCenter,
            float radius,
            Vector3 attackerPos,
            Vector3 forward,
            int comboStep,
            PlayerFootController attackerFoot)
        {
            if (Time.time - _lastHumanoidHitTime < 0.28f) return false;

            ProceduralHumanoid[] all = FindObjectsByType<ProceduralHumanoid>(FindObjectsInactive.Exclude);
            ProceduralHumanoid best = null;
            float bestDistSq = (radius + 0.65f) * (radius + 0.65f);

            for (int i = 0; i < all.Length; i++)
            {
                ProceduralHumanoid h = all[i];
                if (h == null || !h.gameObject.activeInHierarchy) continue;
                if (attackerFoot != null && (h == attackerFoot.Humanoid || h.transform.IsChildOf(attackerFoot.transform))) continue;
                // Skip NPCs already managed by TownPedestrian, LivingNeighborhoodPopulator, or MallShoppers
                if (h.GetComponentInParent<TownPedestrian>() != null) continue;
                string hName = h.gameObject.name;
                if (hName.Contains("StoreSecurityGuard") ||
                    hName.StartsWith("LivingNPC_", StringComparison.OrdinalIgnoreCase) ||
                    hName.StartsWith("MallShopper_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Vector3 pos = h.transform.position + Vector3.up * 1.05f;
                if (Mathf.Abs(pos.y - strikeCenter.y) > 1.85f) continue;
                Vector3 d = pos - strikeCenter;
                d.y = 0f;
                float dSq = d.sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = h;
                }
            }

            if (best == null) return false;

            _lastHumanoidHitTime = Time.time;
            best.PlayHitReact();

            Vector3 faceDir = attackerPos - best.transform.position;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude > 0.01f)
            {
                best.transform.rotation = Quaternion.LookRotation(faceDir.normalized, Vector3.up);
            }

            bool en = LocalizationService.IsEnglish;
            string rawName = best.gameObject.name;
            bool isMallStaff = rawName.StartsWith("MallNPC_", StringComparison.OrdinalIgnoreCase);
            bool isToughAuthority = rawName.Contains("Kerem") || rawName.Contains("Haluk") ||
                                    rawName.Contains("Kemal") || rawName.Contains("Hasan") ||
                                    rawName.Contains("Tarik") || rawName.Contains("Tarık") ||
                                    rawName.Contains("Mechanic") || rawName.Contains("Usta");

            string displayName = ExtractNpcDisplayName(best.gameObject, en);

            if (isMallStaff)
            {
                PlayZabitaWhistle(0.68f);
                string shout = en
                    ? "Security!! Someone just attacked store staff!"
                    : "Güvenlik!! Mağaza personeline saldırıyor!!";
                NpcSpeechBubbleManager.Say(best.transform, displayName, shout, 3.2f, NpcBubbleMood.Alert, 2.0f);
                ReportOffense(NeighborhoodOffenseKind.AssaultStaffOrAuthority, best.transform.position, displayName, repPenalty: 18, immediateFineTl: 60, heatDelta: 1.6f);
                return true;
            }

            if (isToughAuthority)
            {
                best.PlayCombatMove(comboStep >= 2 ? 2 : 0);
                string reply = en
                    ? "Hey! Watch who you swing at — take that!"
                    : "Hey! Kime el kaldırıyorsun sen — kendine gel!";
                NpcSpeechBubbleManager.Say(best.transform, displayName, reply, 3.2f, NpcBubbleMood.Alert, 2.0f);
                PlayerNeedsController.Instance?.TakeDamage(15f, best.transform.position, displayName, heavyHit: comboStep >= 2);
                ReportOffense(NeighborhoodOffenseKind.AssaultStaffOrAuthority, best.transform.position, displayName, repPenalty: 15, immediateFineTl: 0, heatDelta: 1.4f);
                return true;
            }

            string shock = en
                ? "Deniz?! What on earth are you doing?!"
                : "Deniz?! Ne yapıyorsun sen, yakıştı mı hiç?!";
            NpcSpeechBubbleManager.Say(best.transform, displayName, shock, 3.2f, NpcBubbleMood.Alert, 2.0f);
            PlayerModeController.Instance?.RaiseStatusMessage($"{displayName}: {shock}");
            ReportOffense(NeighborhoodOffenseKind.AssaultCitizen, best.transform.position, displayName, repPenalty: 15, immediateFineTl: 0, heatDelta: 1.25f);
            return true;
        }

        private static string ExtractNpcDisplayName(GameObject go, bool en)
        {
            if (go == null) return en ? "Neighbor" : "Komşu";
            string n = go.name;
            if (n.StartsWith("MallNPC_", StringComparison.OrdinalIgnoreCase))
            {
                int second = n.IndexOf('_', 8);
                if (second >= 0 && second + 1 < n.Length) return n.Substring(second + 1);
            }
            if (n.StartsWith("NPC_", StringComparison.OrdinalIgnoreCase))
            {
                return n.Substring(4).Replace('_', ' ');
            }
            if (n.StartsWith("SportSpectator_", StringComparison.OrdinalIgnoreCase)) return en ? "Match Spectator" : "Maç Seyircisi";
            if (n.Contains("Haluk")) return "Usta Haluk";
            if (n.Contains("OtoGaleri") || n.Contains("Manager")) return "Kemal Bey";
            return en ? "Neighbor" : "Mahalle Sakini";
        }

        // =====================================================================
        // UNIVERSAL WORLD OBJECT / SURFACE PUNCH FEEDBACK & CONSEQUENCES
        // =====================================================================
        public bool HandleWorldObjectHit(
            Collider hitCol,
            Vector3 hitPoint,
            Vector3 strikeDir,
            bool heavy,
            PlayerFootController player)
        {
            if (hitCol == null) return false;
            bool en = LocalizationService.IsEnglish;
            PlayerModeController mode = PlayerModeController.Instance;

            // 1. Carryable physical street props (Damacana, Crate, Cone, Box) -> launch with physics!
            CarryableProp prop = hitCol.GetComponentInParent<CarryableProp>();
            if (prop != null)
            {
                prop.ApplyPunchImpulse(strikeDir, heavy);
                PlayWoodThud(heavy ? 0.65f : 0.48f);
                VisualEffectsManager.SpawnCombatImpact(hitPoint, strikeDir, heavy, new Color(0.92f, 0.78f, 0.46f));
                VisualEffectsManager.SpawnFootDust(hitPoint, heavyLanding: heavy);
                return true;
            }

            // 1b. Dynamic world street props (Trash Cans, Wooden Crates, Oil Barrels, Tool Carts, Tyres) -> physically send tumbling!
            DynamicWorldProp dynProp = hitCol.GetComponentInParent<DynamicWorldProp>();
            if (dynProp != null)
            {
                Vector3 punchImpulse = (strikeDir.normalized * (heavy ? 7.8f : 4.6f) + Vector3.up * (heavy ? 2.4f : 1.15f)) *
                                       Mathf.Clamp(dynProp.MassKg * 0.48f, 5f, 24f);
                dynProp.ApplyPunchOrCrashImpulse(punchImpulse, hitPoint, heavy);
                VisualEffectsManager.SpawnCombatImpact(hitPoint, strikeDir, heavy, new Color(0.92f, 0.80f, 0.50f));
                NpcWorldAwareness.NotifyPropImpact(hitPoint, player != null ? player.transform.position : hitPoint, dynProp.gameObject.name);
                return true;
            }

            // 2. Hinged doors -> slam open with loud wooden bang!
            DoorInteractable door = hitCol.GetComponentInParent<DoorInteractable>();
            if (door != null)
            {
                PlayWoodThud(0.68f);
                VisualEffectsManager.SpawnCombatImpact(hitPoint, strikeDir, heavy, new Color(0.84f, 0.66f, 0.42f));
                if (!door.IsOpen && player != null)
                {
                    door.Toggle(player.transform.position, player.Humanoid);
                }
                return true;
            }

            // 3. Interactable ATM or Vending Machine -> sparks, clang + Vandalism fine!
            InteractableTrigger trig = hitCol.GetComponentInParent<InteractableTrigger>();
            if (trig != null && (trig.Kind == InteractableKind.AtmTerminal || trig.Kind == InteractableKind.VendingMachine))
            {
                PlayMetalClang(0.70f);
                VisualEffectsManager.SpawnCombatImpact(hitPoint, -strikeDir, heavy: true, new Color(0.28f, 0.92f, 1.0f));
                DayNightCycle.Instance?.PulseImpactChromatic(0.14f);
                string label = trig.Kind == InteractableKind.AtmTerminal
                    ? "ATM"
                    : (en ? "Vending Machine" : "İçecek Otomatı");
                ReportOffense(NeighborhoodOffenseKind.PropertyVandalism, hitPoint, label, repPenalty: 12, immediateFineTl: 45, heatDelta: 1.5f);
                return true;
            }

            string lowerName = GetHierarchyToken(hitCol.transform);

            // 4. Glass / Shop Windows / Storefront Vitrins -> clink + glint sparks (+ Vandalism consequence on shop vitrins!)
            if (ContainsAny(lowerName, "glass", "vitrin", "window", "winw", "wine", "mirror", "showcase"))
            {
                PlayGlassClink(heavy ? 0.74f : 0.56f);
                VisualEffectsManager.SpawnCombatImpact(hitPoint, -strikeDir, heavy, new Color(0.76f, 0.94f, 1.0f));
                bool isHomeBalconyRailing = ContainsAny(lowerName, "railing", "terrace", "balcony", "handrail");
                if (!isHomeBalconyRailing)
                {
                    _glassHitCount++;
                    int fine = _glassHitCount >= 2 ? 50 : 0;
                    if (fine > 0) _glassHitCount = 0;
                    ReportOffense(
                        NeighborhoodOffenseKind.PropertyVandalism,
                        hitPoint,
                        en ? "Shop Window" : "Dükkân Vitrini",
                        repPenalty: 10,
                        immediateFineTl: fine,
                        heatDelta: 1.1f);
                    if (fine == 0)
                    {
                        mode?.RaiseStatusMessage(en
                            ? "⚠️ Don't smash shop windows! (-10 Reputation)"
                            : "⚠️ Dükkân camlarına vurma! (-10 Saygınlık)");
                    }
                }
                return true;
            }

            // 5. Trees / Foliage / Bushes -> wood thud + burst of falling leaves!
            if (ContainsAny(lowerName, "tree", "cypress", "plane", "pine", "blossom", "canopy", "trunk", "bush", "foliage", "planter"))
            {
                PlayWoodThud(heavy ? 0.58f : 0.42f);
                bool isBlossom = lowerName.Contains("blossom") || lowerName.Contains("flower");
                Color leafCol = isBlossom ? new Color(0.98f, 0.64f, 0.78f) : new Color(0.32f, 0.74f, 0.30f);
                VisualEffectsManager.SpawnCombatImpact(hitPoint, -strikeDir, false, leafCol);
                GameFeelAudio.SpawnBurst(hitPoint + Vector3.up * 0.9f, leafCol, count: heavy ? 14 : 8, spread: 0.75f);
                return true;
            }

            // 6. Metal Poles, Streetlamps, Fences, Dumpsters, Signs, Gym/Sports Frames -> bright sparks + metallic clang!
            if (ContainsAny(lowerName, "lamp", "pole", "post", "fence", "dumpster", "steel", "metal", "gate", "sign", "hoop", "goal", "bin", "trash", "lift", "eas_"))
            {
                PlayMetalClang(heavy ? 0.68f : 0.50f);
                VisualEffectsManager.SpawnCombatImpact(hitPoint, -strikeDir, heavy, new Color(1.0f, 0.80f, 0.28f));
                ApplyHardWallKnuckleCheck(heavy, hitPoint, en);
                return true;
            }

            // 7. Wooden Benches, Tables, Counters, Sheds, Crates -> warm wood knock + dust!
            if (ContainsAny(lowerName, "bench", "table", "chair", "wood", "counter", "desk", "crate", "coop", "barn", "gazebo", "pier", "stall", "stand", "shelf", "bed"))
            {
                PlayWoodThud(heavy ? 0.62f : 0.46f);
                VisualEffectsManager.SpawnCombatImpact(hitPoint, -strikeDir, false, new Color(0.84f, 0.68f, 0.44f));
                return true;
            }

            // 8. Solid Concrete / Brick / Stone Walls, Buildings, Curbs -> stone thump + dust + heavy-punch bruised knuckles!
            PlayStoneThump(heavy ? 0.60f : 0.44f);
            VisualEffectsManager.SpawnCombatImpact(hitPoint, -strikeDir, false, new Color(0.82f, 0.78f, 0.72f));
            VisualEffectsManager.SpawnFootDust(hitPoint, heavyLanding: heavy);
            ApplyHardWallKnuckleCheck(heavy, hitPoint, en);
            return true;
        }

        private void ApplyHardWallKnuckleCheck(bool heavy, Vector3 hitPoint, bool en)
        {
            if (!heavy || Time.time - _lastKnuckleHurtTime < 2.2f) return;
            _lastKnuckleHurtTime = Time.time;
            if (PlayerNeedsController.Instance != null && PlayerNeedsController.Instance.Health > 8f)
            {
                PlayerNeedsController.Instance.TakeDamage(
                    3f,
                    hitPoint,
                    en ? "Hard Wall (Bruised Knuckles)" : "Sert Duvar (Elin Acıdı)",
                    heavyHit: false);
            }
        }

        private static string GetHierarchyToken(Transform t)
        {
            string s = t != null ? t.name : string.Empty;
            if (t != null && t.parent != null)
            {
                s += " " + t.parent.name;
                if (t.parent.parent != null) s += " " + t.parent.parent.name;
            }
            return s.ToLowerInvariant();
        }

        private static bool ContainsAny(string haystack, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (haystack.Contains(needles[i])) return true;
            }
            return false;
        }

        // =====================================================================
        // PROCEDURAL AUDIO SYNTHESIS (ZERO EXTERNAL ASSETS, MOBILE SAFE)
        // =====================================================================
        private void BuildAudioBank()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.spatialBlend = 0f;
            _sfx.playOnAwake = false;

            const int sr = 22050;
            _dogBarkClip = SynthDogBark(sr);
            _catHissClip = SynthCatHiss(sr);
            _farmAlarmClip = SynthFarmBleat(sr);
            _metalClangClip = SynthMetalClang(sr);
            _woodThudClip = SynthWoodThud(sr);
            _glassClinkClip = SynthGlassClink(sr);
            _stoneThumpClip = SynthStoneThump(sr);
            _whistleClip = SynthWhistle(sr);
        }

        private static AudioClip SynthDogBark(int sr)
        {
            // Double sharp aggressive dog bark ("HAV! HAV!")
            int n = Mathf.RoundToInt(sr * 0.42f);
            float[] d = new float[n];
            System.Random rng = new System.Random(1903);
            float lp = 0f;
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float local = t < 0.18f ? t : (t > 0.22f ? t - 0.22f : -1f);
                if (local < 0f || local > 0.16f) continue;
                float u = local / 0.16f;
                float env = Mathf.Sin(u * Mathf.PI) * Mathf.Exp(-u * 2.2f);
                float freq = Mathf.Lerp(320f, 155f, u);
                ph += (2.0 * Math.PI * freq) / sr;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.28f * (white - lp);
                float vocal = (float)(Math.Sin(ph) * 0.65 + Math.Sin(ph * 2.0) * 0.35 + Math.Sin(ph * 3.0) * 0.18);
                d[i] = Mathf.Clamp((vocal * 0.62f + lp * 0.48f) * env, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("SFX_AngryDogBark", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        private static AudioClip SynthCatHiss(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.28f);
            float[] d = new float[n];
            System.Random rng = new System.Random(2044);
            float prev = 0f;
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = Mathf.Sin(Mathf.Clamp01(u / 0.18f) * Mathf.PI * 0.5f) * Mathf.Exp(-u * 3.2f);
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float hp = white - prev * 0.72f;
                prev = white;
                ph += (2.0 * Math.PI * Mathf.Lerp(1180f, 760f, u)) / sr;
                d[i] = Mathf.Clamp((hp * 0.55f + (float)Math.Sin(ph) * 0.22f) * env, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("SFX_CatHiss", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        private static AudioClip SynthFarmBleat(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.34f);
            float[] d = new float[n];
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = Mathf.Sin(u * Mathf.PI) * Mathf.Exp(-u * 1.4f);
                float vib = Mathf.Sin(u * 38f) * 26f;
                float freq = Mathf.Lerp(260f, 195f, u) + vib;
                ph += (2.0 * Math.PI * freq) / sr;
                float s = (float)(Math.Sin(ph) * 0.55 + Math.Sin(ph * 2.0) * 0.35 + Math.Sin(ph * 3.0) * 0.22);
                d[i] = Mathf.Clamp(s * env * 0.72f, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("SFX_FarmAlarm", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        private static AudioClip SynthMetalClang(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.32f);
            float[] d = new float[n];
            float[] freqs = { 620f, 1185f, 1890f, 2740f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float u = i / (float)n;
                float env = Mathf.Exp(-u * 7.5f);
                float sum = 0f;
                for (int k = 0; k < freqs.Length; k++)
                {
                    sum += Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * (1f / (k + 1.1f));
                }
                d[i] = Mathf.Clamp(sum * env * 0.52f, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("SFX_MetalClang", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        private static AudioClip SynthWoodThud(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.14f);
            float[] d = new float[n];
            System.Random rng = new System.Random(3311);
            float lp = 0f;
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = Mathf.Exp(-u * 13f);
                ph += (2.0 * Math.PI * Mathf.Lerp(210f, 88f, u)) / sr;
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.30f * (w - lp);
                d[i] = Mathf.Clamp(((float)Math.Sin(ph) * 0.72f + lp * 0.38f) * env, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("SFX_WoodThud", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        private static AudioClip SynthGlassClink(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.24f);
            float[] d = new float[n];
            float[] freqs = { 2120f, 3410f, 4890f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float u = i / (float)n;
                float env = Mathf.Exp(-u * 10f);
                float sum = 0f;
                for (int k = 0; k < freqs.Length; k++)
                {
                    sum += Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * (0.55f / (k + 1f));
                }
                d[i] = Mathf.Clamp(sum * env, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("SFX_GlassClink", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        private static AudioClip SynthStoneThump(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.12f);
            float[] d = new float[n];
            System.Random rng = new System.Random(4422);
            float lp = 0f;
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float env = Mathf.Exp(-u * 15f);
                ph += (2.0 * Math.PI * Mathf.Lerp(120f, 44f, u)) / sr;
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.20f * (w - lp);
                d[i] = Mathf.Clamp(((float)Math.Sin(ph) * 0.78f + lp * 0.45f) * env, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("SFX_StoneThump", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        private static AudioClip SynthWhistle(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.48f);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float u = i / (float)n;
                float env = Mathf.Sin(Mathf.Clamp01(u / 0.1f) * Mathf.PI * 0.5f) * (1f - Mathf.Clamp01((u - 0.85f) / 0.15f));
                float trill = 1f + 0.055f * Mathf.Sin(2f * Mathf.PI * 36f * t);
                float tone = Mathf.Sin(2f * Mathf.PI * 2650f * trill * t) * 0.58f
                           + Mathf.Sin(2f * Mathf.PI * 2920f * trill * t) * 0.28f;
                d[i] = Mathf.Clamp(tone * env * 0.55f, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("SFX_ZabitaWhistle", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
