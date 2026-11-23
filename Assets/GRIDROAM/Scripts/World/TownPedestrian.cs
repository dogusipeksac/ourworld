using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Missions;
using OurWorld.Player;
using OurWorld.UI;
using OurWorld.Vehicle;
using UnityEngine;

namespace OurWorld.World
{
    public enum CitizenBrainState
    {
        Patrol = 0,
        BenchBreak = 1,
        ChattingWithNeighbor = 2,
        WatchingInterest = 3,
        RainHurry = 4,
        ConfrontPlayer = 5,
        Fleeing = 6,
        KnockedDown = 7
    }

    /// <summary>
    /// Intelligent, socially aware Hamidiye Mahallesi citizen:
    /// - Personal-space & obstacle local avoidance (walks smoothly around the player and neighbors)
    /// - Autonomous 2-turn neighbor-to-neighbor sidewalk conversations with overhead speech bubbles
    /// - Weather & time-of-day reactions (rain jog with head-shielding IK, brisk night walk)
    /// - Curiosity micro-behaviors (cooing at street cats, cheering at nearby sports courts)
    /// - Crowd Witness AI (timid citizens flee fights, brave citizens confront & fight back, horn sidestep)
    /// - Personal memory (grudges after attacks, friendly rapport, reputation-aware contextual TR/EN dialogue)
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class TownPedestrian : MonoBehaviour
    {
        public static readonly List<TownPedestrian> ActiveCitizens = new List<TownPedestrian>(48);
        private static readonly RaycastHit[] WallHits = new RaycastHit[8];

        [SerializeField] private Vector3[] waypoints;
        [SerializeField] private SeatInteractable[] candidateSeats;
        [SerializeField] private float walkSpeed = 1.3f;
        [SerializeField] private float turnSpeed = 6.5f;
        [SerializeField] private ProceduralHumanoid humanoid;

        [Header("Citizen Identity & Social Interaction")]
        [SerializeField] private string citizenName = "Mahalleli";
        [SerializeField] private string citizenRole = "Selman Sokağı Sakini";
        [SerializeField] private string vendorShopId = string.Empty;
        [SerializeField] private bool isBrave;
        [SerializeField] private string[] chatLines;

        private static readonly string[] DefaultChatLinesTr =
        {
            "Hoş geldin komşu! Hamidiye Mahallesi Selman Sokağı bugün pek huzurlu.",
            "73 numaradaki Oto Tamir'e uğra, aracında hasar varsa hemen hallediyorlar.",
            "58 numaradaki Selman Bakkal'da sabah ekmeği ve sıcak tost bulabilirsin.",
            "Gürsu Sokağı'ndaki tenis, halı saha, basketbol ve voleybol sahaları bugün çok hareketli!",
            "Hafta sonu Sultanbeyli Gölet'e ya da Aydos Ormanı'na yürüyüşe çıkıyoruz.",
            "Fatih Bulvarı'ndaki Saat Kulesi akşam ışıklarıyla harika görünüyor!"
        };

        private static readonly string[] DefaultChatLinesEn =
        {
            "Welcome neighbor! Selman Street in Hamidiye is peaceful today.",
            "Stop by Auto Repair No:73 if your sedan ever needs a tune-up.",
            "Selman Market at No:58 always has fresh morning bread and warm toast.",
            "The tennis, football, basketball, and volleyball courts are lively today!",
            "On weekends everyone walks over toward Aydos pine trails.",
            "Clock Tower Plaza on Fatih Boulevard looks stunning under evening lights!"
        };

        private static readonly string[] HitReactionsTr =
        {
            "{0}: Hey! Ne yapıyorsun sen?!",
            "{0}: Delirdin mi, çekil başımdan!",
            "{0}: İmdat! Biri yardım etsin!",
            "{0}: Ne derdin var senin?! Polis çağıracağım!",
            "{0}: Dokunma bana! Uzak dur!"
        };

        private static readonly string[] HitReactionsEn =
        {
            "{0}: Hey! What are you doing?!",
            "{0}: Back off! Leave me alone!",
            "{0}: Help! Somebody stop them!",
            "{0}: Watch yourself! I'm calling for help!"
        };

        private static readonly (string aTr, string bTr, string aEn, string bEn)[] NeighborConversations =
        {
            ("Selam komşu, bakkaldan mı geliyorsun?", "Aynen, sıcak ekmek yeni çıktı!", "Hey neighbor, back from the market?", "Yep, warm bread just came out!"),
            ("Halı sahada bugün kıran kırana maç var!", "Akşam biz de tenis kortuna inelim mi?", "Intense football match on the pitch today!", "Want to hit the tennis court later?"),
            ("Hava bugün yürüyüş için harika değil mi?", "Valla öyle, bir de çay bahçesine uğrayalım.", "Great weather for a stroll, right?", "Totally, let's grab tea at the park!"),
            ("Saat Kulesi tarafı bugün bayağı kalabalık.", "Esnafın yüzü gülüyor, mahalle canlandı!", "Clock Tower Plaza is bustling today.", "The whole neighborhood feels alive!"),
            ("No:48'deki Deniz mahalleye çok sahip çıktı.", "Allah razı olsun, herkes ondan bahsediyor!", "Deniz from No:48 really stepped up for us.", "Everyone on the street is talking about it!"),
            ("Akşam yemeğine ne pişirsek acaba?", "Bakkaldan taze sebze al, mis gibi olur.", "What should we cook for dinner tonight?", "Fresh veggies from Selman Market never fail.")
        };

        private static readonly Vector3[] SportArenaCenters =
        {
            new Vector3(-41f, 0f, -42.2f), // Tennis
            new Vector3(-14f, 0f, -44.0f), // Football
            new Vector3( 41f, 0f, -44.0f), // Basketball
            new Vector3( 59f, 0f, -44.0f)  // Volleyball
        };

        private int _waypointIndex;
        private int _waypointStep = 1;
        private int _chatCursor;
        private float _idleTimer;
        private float _midWalkIdleCooldown = 12f;
        private float _currentSpeed;

        // Bench sitting state
        private SeatInteractable _occupiedSeat;
        private Vector3 _preSitPosition;
        private Quaternion _preSitRotation = Quaternion.identity;
        private float _sitTimer;

        // Smart Brain State & Social Memory
        private CitizenBrainState _brainState = CitizenBrainState.Patrol;
        private float _brainThinkTimer;
        private float _playerGreetCooldown;
        private float _socialChatCooldown;
        private float _curiosityCooldown;
        private float _rainSpeechCooldown;
        private float _grudgeTimer;
        private int _timesTalked;
        private bool _giftedPlayer;

        // Neighbor-to-neighbor conversation state
        private TownPedestrian _chatPartner;
        private float _chatTimer;
        private float _chatReplyDelay;
        private string _pendingChatReply;

        // Curiosity watching state (cat or sport)
        private Vector3 _watchTargetPos;
        private float _watchTimer;
        private bool _watchingCat;

        // Brave confrontation & horn sidestep state
        private float _confrontTimer;
        private float _confrontPunchCooldown;
        private Vector3 _sidestepVelocity;
        private float _sidestepTimer;

        // Startled / combat / vehicle-impact ballistic state
        private float _startledCooldown;
        private float _hitCooldown;
        private float _vehicleHitCooldown;
        private float _knockdownTimer;
        private int _hitCount;
        private float _fleeTimer;
        private Vector3 _fleeDirection = Vector3.forward;
        private Vector3 _stumbleVelocity;
        private Vector3 _airVelocity;
        private bool _isAirborne;
        private bool _droppedPocketMoney;

        public CitizenBrainState CurrentBrainState => _brainState;
        public string CitizenName => citizenName;
        public bool IsBusyOrPanicking =>
            _occupiedSeat != null || _knockdownTimer > 0f || _isAirborne || _fleeTimer > 0f ||
            _brainState == CitizenBrainState.ChattingWithNeighbor || _brainState == CitizenBrainState.ConfrontPlayer;

        private static bool IsEnglish =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        public void ConfigureCitizen(string displayName, string role, string[] lines, bool brave = false, string shopId = "")
        {
            citizenName = string.IsNullOrEmpty(displayName) ? "Mahalleli" : displayName;
            citizenRole = string.IsNullOrEmpty(role) ? "Hamidiye Sakini" : role;
            chatLines = lines;
            isBrave = brave;
            vendorShopId = shopId ?? string.Empty;
        }

        public void TalkToPlayer(Vector3 playerWorldPos)
        {
            EndNeighborChat();
            _watchTimer = 0f;
            _confrontTimer = 0f;
            _fleeTimer = 0f;
            _idleTimer = Mathf.Max(_idleTimer, 3.6f);

            Vector3 toPlayer = playerWorldPos - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f && _occupiedSeat == null)
            {
                transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            }

            bool isEn = IsEnglish;
            string speakerHeader = string.IsNullOrEmpty(citizenRole)
                ? citizenName
                : $"{citizenName} ({citizenRole})";

            // Grudge memory: remembers recent assault!
            if (_grudgeTimer > 0f)
            {
                if (humanoid != null) humanoid.PlayStartled(0.8f);
                string grudgeLine = isEn
                    ? "Stay away from me! I haven't forgotten what you just did."
                    : "Benden uzak dur! Az önce yaptığını unutmadım.";
                NpcSpeechBubbleManager.Say(transform, citizenName, grudgeLine, 3.6f, NpcBubbleMood.Alert);
                if (PlayerModeController.Instance != null)
                {
                    PlayerModeController.Instance.RaiseStatusMessage($"{citizenName}: {grudgeLine}");
                }
                return;
            }

            if (humanoid != null)
            {
                humanoid.PlayWave();
                humanoid.SetLookAtPoint(true, playerWorldPos + Vector3.up * 1.55f);
            }

            if (!string.IsNullOrEmpty(vendorShopId) &&
                PlayerShoppingCart.HasActiveItems &&
                PlayerModeController.Instance != null)
            {
                PlayerShoppingCart cart = PlayerShoppingCart.GetOrCreate(PlayerModeController.Instance);
                if (cart != null)
                {
                    cart.Checkout(PlayerModeController.Instance, transform);
                    return;
                }
            }

            _timesTalked++;
            string line = BuildSmartContextualDialogue(isEn);

            // Friendly neighbor warm treat/tip on repeat rapport!
            if (_timesTalked >= 2 && !_giftedPlayer && string.IsNullOrEmpty(vendorShopId) &&
                (NeighborhoodReputation.CurrentRankIndex >= 1 || _timesTalked >= 3))
            {
                _giftedPlayer = true;
                if (PlayerNeedsController.Instance != null && PlayerNeedsController.Instance.Energy < 85f)
                {
                    PlayerNeedsController.Instance.RestoreEnergy(12f);
                    line += isEn ? " Here, have a warm cup of tea! (+12 Energy)" : " Al bakalım, benden bir bardak sıcak çay! (+12 Enerji)";
                }
                else if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.AddMoney(15);
                    line += isEn ? " Found a lucky tip for our favorite neighbor! (+15 ₺)" : " Mahallemize emeğin büyük, al şu harçlığı! (+15 ₺)";
                }
                GameFeelAudio.PlayCoinChime();
                GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 1.5f, new Color(0.28f, 0.92f, 0.80f), count: 8, spread: 0.45f);
            }

            NpcSpeechBubbleManager.Say(transform, citizenName, line, 4.0f, NpcBubbleMood.Friendly);

            if (MissionManager.Instance != null && !MissionManager.Instance.IsDialoguePlaying)
            {
                MissionManager.Instance.ShowQuickNpcDialogue(speakerHeader, line);
            }
            else if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage($"{citizenName}: {line}");
            }
        }

        private string BuildSmartContextualDialogue(bool isEn)
        {
            // 1. Weather commentary during rain!
            if (NeighborhoodWeatherSystem.Instance != null &&
                NeighborhoodWeatherSystem.Instance.CurrentWeather == NeighborhoodWeatherState.SummerShower)
            {
                return isEn
                    ? "Classic Istanbul summer shower! Hope you don't catch a cold, Deniz."
                    : "Tam İstanbul yaz yağmuru bastırdı! Islanma komşu, hemen bir saçağa geç.";
            }

            // 2. Story / Legacy Choice reactivity on repeat talk or 35% chance
            SaveData save = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (_timesTalked >= 2 || Random.value < 0.45f)
            {
                if (save != null && save.lastEndingChoice == "legacy_youth_academy")
                {
                    return isEn
                        ? "The new Youth Sports Academy you founded is packed with smiling kids!"
                        : "Kurduğun Hamidiye Gençlik & Spor Akademisi çocuklarla dolup taşıyor, sağ olasın Deniz!";
                }
                if (save != null && save.lastEndingChoice == "legacy_green_park")
                {
                    return isEn
                        ? "Everyone loves the new Culture House & Pine Park you chose for Selman Street!"
                        : "Selman Sokağı Kültür Evi ve Çam Parkı mahallemize nefes aldırdı, eline sağlık Deniz!";
                }

                MissionData activeMission = MissionManager.Instance != null ? MissionManager.Instance.ActiveMission : null;
                if (activeMission != null && Random.value < 0.55f)
                {
                    string mId = activeMission.missionId;
                    if (mId.StartsWith("m16"))
                    {
                        return isEn
                            ? "Rooting for you in the Great Hamidiye Sports Cup! Crush it on the courts!"
                            : "Büyük Hamidiye Spor Kupası'nda seni tutuyoruz Deniz! Sahalarda başarılar!";
                    }
                    if (mId.StartsWith("m17"))
                    {
                        return isEn
                            ? "The lights flickered on Mürşit Ave — hope you and Master Hasan fix the transformer!"
                            : "Mürşit Caddesi'nde ışıklar gidip geldi, Usta Hasan'la trafoyu kurtaracağınıza eminiz!";
                    }
                    if (mId.StartsWith("m18"))
                    {
                        return isEn
                            ? "Heard the Sunset Rally is kicking off across all 8 avenues! Drive fast and clean!"
                            : "Sekiz Cadde Büyük Rallisi başlamış! Dedenin sedanının tozunu attır Deniz!";
                    }
                    if (mId.StartsWith("m19"))
                    {
                        return isEn
                            ? "Journalist Defne was asking around about Kaya Holding's shady nighttime drops..."
                            : "Gazeteci Defne bu gece Kaya Holding'in gizli hamlesini araştırıyormuş, dikkatli ol!";
                    }
                }

                // Reputation rank praise!
                int rank = NeighborhoodReputation.CurrentRankIndex;
                if (rank >= 3)
                {
                    string rankTitle = NeighborhoodReputation.GetRankTitle(rank);
                    return isEn
                        ? $"Wait — it's {rankTitle}! Everyone in Hamidiye knows your name, Deniz!"
                        : $"Vay, {rankTitle} Deniz gelmiş! Bütün Hamidiye seninle gurur duyuyor!";
                }

                // Time-of-day contextual line
                float h = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentHour : 12f;
                if (h >= 20.5f || h < 5.5f)
                {
                    return isEn
                        ? "Quiet night in Sultanbeyli. The street lamps along Selman Street look cozy."
                        : "Sultanbeyli'de sakin bir gece. Sokak lambalarının altında mahalle pek huzurlu.";
                }
                if (h >= 17.5f && h < 20.5f)
                {
                    return isEn
                        ? "Golden hour hits the mosque domes and balconies just right this evening."
                        : "Gün batımı Hz. Osman Camii'nin kubbelerine ve balkonlara ne güzel vuruyor!";
                }
            }

            // 3. Citizen's personal line pool (fall back to EN defaults if English mode)
            if (isEn)
            {
                string line = DefaultChatLinesEn[_chatCursor % DefaultChatLinesEn.Length];
                _chatCursor++;
                return line;
            }

            string[] pool = (chatLines != null && chatLines.Length > 0) ? chatLines : DefaultChatLinesTr;
            string trLine = pool[_chatCursor % pool.Length];
            _chatCursor++;
            return trLine;
        }

        public void OnPunchedByPlayer(Vector3 attackerPos, Vector3 punchDir, int comboStep = 1)
        {
            if (_hitCooldown > 0f || _knockdownTimer > 0f) return;

            EndNeighborChat();
            ReleaseBenchIfSeated();
            ClearDualHandPose();
            _watchTimer = 0f;
            _grudgeTimer = 60f;

            Vector3 knockDir = punchDir;
            knockDir.y = 0f;
            if (knockDir.sqrMagnitude < 0.01f)
            {
                knockDir = transform.position - attackerPos;
                knockDir.y = 0f;
            }
            knockDir = knockDir.sqrMagnitude > 0.001f ? knockDir.normalized : Vector3.forward;

            _hitCount++;
            _idleTimer = 0f;

            bool triggerKnockdown = comboStep >= 2 || _hitCount >= 3;
            bool isEn = IsEnglish;
            string react;
            string bubbleText;

            if (triggerKnockdown)
            {
                _hitCooldown = 0.85f;
                _knockdownTimer = 2.65f;
                _hitCount = 0;
                _stumbleVelocity = knockDir * 7.4f;
                transform.rotation = Quaternion.LookRotation(-knockDir, Vector3.up);

                if (humanoid != null) humanoid.PlayKnockdown();

                bubbleText = isEn ? "Oof! I'm down!" : "Ah! Yere serildim!";
                react = isEn ? $"{citizenName}: Knocked down!" : $"{citizenName}: Ah! Yere serildi!";
            }
            else
            {
                _hitCooldown = 0.22f;
                _stumbleVelocity = knockDir * (comboStep == 0 ? 3.8f : 5.6f);

                if (humanoid != null) humanoid.PlayHitReact();

                string[] pool = isEn ? HitReactionsEn : HitReactionsTr;
                react = string.Format(pool[Random.Range(0, pool.Length)], citizenName);
                int colon = react.IndexOf(':');
                bubbleText = colon >= 0 && colon + 1 < react.Length ? react.Substring(colon + 1).Trim() : react;
            }

            if (!_droppedPocketMoney && string.IsNullOrEmpty(vendorShopId) && Random.value < 0.45f && SaveManager.Instance != null)
            {
                _droppedPocketMoney = true;
                int coins = Random.Range(10, 21);
                SaveManager.Instance.AddMoney(coins);
                react += isEn ? $" (+{coins} ₺ dropped)" : $" (+{coins} ₺ düştü)";
            }

            if (isBrave && !triggerKnockdown)
            {
                // Brave citizen stands ground, confronts, and punches back!
                transform.rotation = Quaternion.LookRotation(-knockDir, Vector3.up);
                _confrontTimer = 7.0f;
                _confrontPunchCooldown = 0.9f;
                _brainState = CitizenBrainState.ConfrontPlayer;
                if (humanoid != null) humanoid.PlayCombatMove(1);
                if (PlayerNeedsController.Instance != null)
                {
                    PlayerNeedsController.Instance.ConsumeEnergy(6f);
                }
                bubbleText = isEn ? "You hit me?! Take that!" : "Bana mı vurdun?! Al sana!";
                react = isEn ? $"{citizenName}: {bubbleText} (-6 Energy)" : $"{citizenName}: {bubbleText} (-6 Enerji)";
            }
            else
            {
                _fleeTimer = 6.5f;
                _fleeDirection = knockDir;
                _brainState = triggerKnockdown ? CitizenBrainState.KnockedDown : CitizenBrainState.Fleeing;
            }

            NpcSpeechBubbleManager.Say(transform, citizenName, bubbleText, 3.2f, NpcBubbleMood.Alert);
            NpcWorldAwareness.NotifyViolence(transform.position, attackerPos, this);

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage(react);
            }
        }

        public void OnHitByVehicle(Vector3 carPos, Vector3 carVelocity, float carSpeedKmh)
        {
            if (_vehicleHitCooldown > 0f) return;
            _vehicleHitCooldown = 0.70f;
            _hitCooldown = 1.0f;

            EndNeighborChat();
            ReleaseBenchIfSeated();
            ClearDualHandPose();
            _watchTimer = 0f;
            _confrontTimer = 0f;
            _grudgeTimer = 75f;

            Vector3 impactDir = carVelocity;
            impactDir.y = 0f;
            Vector3 awayFromCar = transform.position - carPos;
            awayFromCar.y = 0f;
            if (impactDir.sqrMagnitude < 0.04f)
            {
                impactDir = awayFromCar;
            }
            else if (awayFromCar.sqrMagnitude > 0.01f)
            {
                impactDir = (impactDir.normalized * 0.72f + awayFromCar.normalized * 0.28f);
            }
            impactDir = impactDir.sqrMagnitude > 0.001f ? impactDir.normalized : Vector3.forward;

            float horizSpeed = Mathf.Clamp(carSpeedKmh * 0.34f, 5.5f, 18.5f);
            float upSpeed = Mathf.Clamp(carSpeedKmh * 0.14f, 2.4f, 7.6f);

            _airVelocity = impactDir * horizSpeed + Vector3.up * upSpeed;
            _isAirborne = true;
            _stumbleVelocity = Vector3.zero;
            _idleTimer = 0f;

            float downDuration = Mathf.Clamp(3.4f + carSpeedKmh * 0.045f, 3.6f, 6.0f);
            _knockdownTimer = downDuration;
            _fleeTimer = 7.5f;
            _fleeDirection = impactDir;
            _brainState = CitizenBrainState.KnockedDown;

            transform.rotation = Quaternion.LookRotation(-impactDir, Vector3.up);
            if (humanoid != null) humanoid.PlayKnockdown(downDuration);

            bool isEn = IsEnglish;
            string bubbleText = isEn ? "Watch the road!!" : "Önüne baksana!!";
            NpcSpeechBubbleManager.Say(transform, citizenName, bubbleText, 3.4f, NpcBubbleMood.Alert);
            NpcWorldAwareness.NotifyViolence(transform.position, carPos, this);

            string msg = isEn
                ? $"{citizenName} was knocked down by the car!"
                : (carSpeedKmh >= 38f
                    ? $"{citizenName} şiddetli çarpışmayla havaya savruldu!"
                    : $"{citizenName} araba çarpmasıyla yere serildi!");

            if (!_droppedPocketMoney && string.IsNullOrEmpty(vendorShopId) && Random.value < 0.50f && SaveManager.Instance != null)
            {
                _droppedPocketMoney = true;
                int coins = Random.Range(15, 31);
                SaveManager.Instance.AddMoney(coins);
                msg += isEn ? $" (+{coins} ₺ dropped)" : $" (+{coins} ₺ düştü)";
            }

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage(msg);
            }
        }

        public void Configure(Vector3 a, Vector3 b, ProceduralHumanoid hum, float speed = 1.3f, bool startHeadingToB = true)
        {
            waypoints = new[] { a, b };
            humanoid = hum;
            walkSpeed = speed;
            _waypointIndex = startHeadingToB ? 1 : 0;
            _waypointStep = startHeadingToB ? 1 : -1;
        }

        public void ConfigureSchedule(
            Vector3[] routePoints,
            SeatInteractable[] nearbySeats,
            ProceduralHumanoid hum,
            float speed = 1.3f,
            int startIndex = 0)
        {
            waypoints = routePoints;
            candidateSeats = nearbySeats;
            humanoid = hum;
            walkSpeed = speed;
            if (waypoints != null && waypoints.Length > 0)
            {
                _waypointIndex = Mathf.Clamp(startIndex, 0, waypoints.Length - 1);
            }
        }

        private void Awake()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (humanoid == null)
            {
                humanoid = GetComponentInChildren<ProceduralHumanoid>();
            }

            _midWalkIdleCooldown = Random.Range(9f, 20f);
            _brainThinkTimer = Random.Range(0.15f, 0.55f);
            _playerGreetCooldown = Random.Range(4f, 12f);
            _socialChatCooldown = Random.Range(6f, 18f);
            _curiosityCooldown = Random.Range(8f, 20f);
        }

        private void OnEnable()
        {
            if (!ActiveCitizens.Contains(this)) ActiveCitizens.Add(this);
            NpcWorldAwareness.OnViolenceOccurred += HandleViolenceWitnessed;
            NpcWorldAwareness.OnHornHeard += HandleHornHeard;
            NpcWorldAwareness.OnSportHighlight += HandleSportHighlight;
        }

        private void OnDisable()
        {
            ActiveCitizens.Remove(this);
            NpcWorldAwareness.OnViolenceOccurred -= HandleViolenceWitnessed;
            NpcWorldAwareness.OnHornHeard -= HandleHornHeard;
            NpcWorldAwareness.OnSportHighlight -= HandleSportHighlight;
            EndNeighborChat();
            ReleaseBenchIfSeated();
        }

        private void HandleViolenceWitnessed(Vector3 incidentPos, Vector3 attackerPos, TownPedestrian victim)
        {
            if (victim == this || _knockdownTimer > 0f || _isAirborne) return;
            Vector3 delta = transform.position - incidentPos;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist > 16f) return;

            EndNeighborChat();
            ReleaseBenchIfSeated();
            ClearDualHandPose();
            _watchTimer = 0f;

            bool isEn = IsEnglish;
            if (isBrave && dist <= 13f)
            {
                _confrontTimer = 6.5f;
                _confrontPunchCooldown = 1.1f;
                _brainState = CitizenBrainState.ConfrontPlayer;
                string shout = isEn ? "Hey! Don't cause trouble in our neighborhood!" : "Hey! Mahallede olay çıkarma, dur bakalım!";
                NpcSpeechBubbleManager.Say(transform, citizenName, shout, 3.4f, NpcBubbleMood.Alert);
            }
            else
            {
                Vector3 away = transform.position - attackerPos;
                away.y = 0f;
                _fleeDirection = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
                _fleeTimer = Random.Range(4.8f, 7.2f);
                _brainState = CitizenBrainState.Fleeing;
                if (humanoid != null) humanoid.PlayStartled(0.85f);
                string panic = isEn ? "Whoa! A fight — get back!" : "Ayy! Kavga var, uzaklaşın!";
                NpcSpeechBubbleManager.Say(transform, citizenName, panic, 3.0f, NpcBubbleMood.Alert);
            }
        }

        private void HandleHornHeard(Vector3 carPos, Vector3 carForward)
        {
            if (_knockdownTimer > 0f || _isAirborne || _fleeTimer > 0f || _occupiedSeat != null) return;
            Vector3 toMe = transform.position - carPos;
            toMe.y = 0f;
            float dist = toMe.magnitude;
            if (dist > 13.5f) return;

            EndNeighborChat();
            if (humanoid != null && _startledCooldown <= 0f)
            {
                humanoid.PlayStartled(0.9f);
                _startledCooldown = 2.5f;
            }

            // Smartly sidestep perpendicular away from the honking car's lane!
            Vector3 rightOfCar = Vector3.Cross(Vector3.up, carForward).normalized;
            float sideSign = Vector3.Dot(toMe, rightOfCar) >= 0f ? 1f : -1f;
            _sidestepVelocity = rightOfCar * (sideSign * 2.8f);
            _sidestepTimer = 0.85f;
            _idleTimer = Mathf.Max(_idleTimer, 1.4f);

            if (toMe.sqrMagnitude > 0.04f)
            {
                transform.rotation = Quaternion.LookRotation((-toMe).normalized, Vector3.up);
            }

            if (dist <= 11.5f)
            {
                bool isEn = IsEnglish;
                string[] hornTr = { "Tamam tamam, geçiyoruz!", "Hey yavaş ol şoför!", "Korna çalma komşu, duyduk!" };
                string[] hornEn = { "Alright, alright, moving!", "Easy on the horn, driver!", "Whoa, watch where you're going!" };
                string text = isEn ? hornEn[Random.Range(0, hornEn.Length)] : hornTr[Random.Range(0, hornTr.Length)];
                NpcSpeechBubbleManager.Say(transform, citizenName, text, 2.8f, NpcBubbleMood.Alert);
            }
        }

        private void HandleSportHighlight(Vector3 arenaPos)
        {
            if (IsBusyOrPanicking) return;
            Vector3 delta = arenaPos - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 26f * 26f) return;

            _watchTargetPos = arenaPos;
            _watchTimer = 3.2f;
            _watchingCat = false;
            _brainState = CitizenBrainState.WatchingInterest;
            if (humanoid != null) humanoid.PlaySportAnim(SportAnimKind.VictoryJump, 0.62f);

            bool isEn = IsEnglish;
            string cheer = isEn ? "What a victory! Go Deniz!" : "Helal olsun Deniz! Müthiş maç!";
            NpcSpeechBubbleManager.Say(transform, citizenName, cheer, 3.2f, NpcBubbleMood.Weather);
        }

        private void ReleaseBenchIfSeated()
        {
            if (_occupiedSeat != null)
            {
                _occupiedSeat.SetOccupied(false);
                _occupiedSeat = null;
            }
            if (humanoid != null && humanoid.IsSitting)
            {
                humanoid.SetSitting(false, driving: false);
            }
        }

        private void ClearDualHandPose()
        {
            if (humanoid != null)
            {
                humanoid.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f);
            }
        }

        private void EndNeighborChat()
        {
            if (_chatPartner != null)
            {
                TownPedestrian other = _chatPartner;
                _chatPartner = null;
                if (other._chatPartner == this)
                {
                    other._chatPartner = null;
                    other._chatTimer = 0f;
                    other._pendingChatReply = null;
                    if (other._brainState == CitizenBrainState.ChattingWithNeighbor)
                    {
                        other._brainState = CitizenBrainState.Patrol;
                    }
                }
            }
            _chatTimer = 0f;
            _pendingChatReply = null;
            if (_brainState == CitizenBrainState.ChattingWithNeighbor)
            {
                _brainState = CitizenBrainState.Patrol;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            if (_startledCooldown > 0f) _startledCooldown -= dt;
            if (_hitCooldown > 0f) _hitCooldown -= dt;
            if (_vehicleHitCooldown > 0f) _vehicleHitCooldown -= dt;
            if (_playerGreetCooldown > 0f) _playerGreetCooldown -= dt;
            if (_socialChatCooldown > 0f) _socialChatCooldown -= dt;
            if (_curiosityCooldown > 0f) _curiosityCooldown -= dt;
            if (_rainSpeechCooldown > 0f) _rainSpeechCooldown -= dt;
            if (_grudgeTimer > 0f) _grudgeTimer -= dt;

            // 1. Seated bench break (stand up early if rain starts!)
            if (_occupiedSeat != null)
            {
                _brainState = CitizenBrainState.BenchBreak;
                bool raining = IsRaining();
                _sitTimer -= dt;
                if (_sitTimer <= 0f || raining)
                {
                    _occupiedSeat.SetOccupied(false);
                    _occupiedSeat = null;
                    transform.SetPositionAndRotation(_preSitPosition, _preSitRotation);
                    if (humanoid != null) humanoid.SetSitting(false, driving: false);
                    _idleTimer = raining ? 0f : 0.6f;
                    _brainState = raining ? CitizenBrainState.RainHurry : CitizenBrainState.Patrol;
                }
                else
                {
                    UpdateHeadEyeContact();
                }
                return;
            }

            // 2. Ballistic air-launch after vehicle strike
            if (_isAirborne)
            {
                _brainState = CitizenBrainState.KnockedDown;
                _airVelocity.y -= 21.0f * dt;
                Vector3 nextAir = transform.position + _airVelocity * dt;
                nextAir.x = Mathf.Clamp(nextAir.x, -165f, 165f);
                nextAir.z = Mathf.Clamp(nextAir.z, -165f, 165f);

                if (nextAir.y <= 0.02f)
                {
                    nextAir.y = 0.02f;
                    if (_airVelocity.y < -3.6f)
                    {
                        _airVelocity.y = -_airVelocity.y * 0.28f;
                        _airVelocity.x *= 0.62f;
                        _airVelocity.z *= 0.62f;
                    }
                    else
                    {
                        _isAirborne = false;
                        _stumbleVelocity = new Vector3(_airVelocity.x * 0.55f, 0f, _airVelocity.z * 0.55f);
                        _airVelocity = Vector3.zero;
                    }
                }

                transform.position = nextAir;
                if (_knockdownTimer > 0f) _knockdownTimer -= dt;
                if (humanoid != null) humanoid.SetLocomotion(0f, grounded: false, verticalVelocity: _airVelocity.y);
                return;
            }

            // 3. Stumble / knockback slide
            if (_stumbleVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 dummyDir = _stumbleVelocity.normalized;
                MoveSafely(_stumbleVelocity * dt, ref dummyDir, false);
                _stumbleVelocity = Vector3.MoveTowards(_stumbleVelocity, Vector3.zero, (_knockdownTimer > 0f ? 9.5f : 11f) * dt);
            }

            // 4. Knocked down recovery
            if (_knockdownTimer > 0f)
            {
                _brainState = CitizenBrainState.KnockedDown;
                _knockdownTimer -= dt;
                if (humanoid != null) humanoid.SetLocomotion(0f, grounded: true, verticalVelocity: 0f);
                return;
            }

            // 5. Panic flee sprint (wall-aware steering so fleeing citizens slide around buildings!)
            if (_fleeTimer > 0f)
            {
                _brainState = CitizenBrainState.Fleeing;
                _fleeTimer -= dt;
                ClearDualHandPose();
                if (_fleeDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion fleeRot = Quaternion.LookRotation(_fleeDirection, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, fleeRot, 1f - Mathf.Exp(-10f * dt));
                    MoveSafely(transform.forward * (4.6f * dt), ref _fleeDirection, true);
                }
                if (humanoid != null) humanoid.SetLocomotion(4.6f, grounded: true, verticalVelocity: 0f);
                if (_fleeTimer <= 0f) _brainState = CitizenBrainState.Patrol;
                return;
            }

            // 6. Horn sidestep dodge (wall-safe)
            if (_sidestepTimer > 0f)
            {
                _sidestepTimer -= dt;
                Vector3 sidestepDir = _sidestepVelocity.sqrMagnitude > 0.001f ? _sidestepVelocity.normalized : transform.right;
                MoveSafely(_sidestepVelocity * dt, ref sidestepDir, false);
                if (humanoid != null) humanoid.SetLocomotion(2.6f, grounded: true, verticalVelocity: 0f, _sidestepVelocity.normalized);
                return;
            }

            // 7. Brave Citizen Confrontation!
            if (_confrontTimer > 0f)
            {
                UpdateBraveConfrontation(dt);
                return;
            }

            // 8. Autonomous Neighbor-to-Neighbor Sidewalk Conversation!
            if (_brainState == CitizenBrainState.ChattingWithNeighbor)
            {
                UpdateNeighborChat(dt);
                return;
            }

            // 9. Curious World Watching (Cat petting / Sports cheering!)
            if (_watchTimer > 0f)
            {
                UpdateWatchingInterest(dt);
                return;
            }

            // Periodic Smart Brain Evaluation (staggered, distance-culled within 48 m)
            _brainThinkTimer -= dt;
            if (_brainThinkTimer <= 0f)
            {
                _brainThinkTimer = Random.Range(0.28f, 0.44f);
                EvaluateSmartBrain();
            }

            // Check approaching player vehicle for yield / startled pose
            bool yieldingToCar = EvaluateApproachingCar();
            bool isRaining = IsRaining();
            bool isNight = IsNightTime();
            _brainState = isRaining ? CitizenBrainState.RainHurry : CitizenBrainState.Patrol;

            // Rain head-shield IK pose while hurrying in rain (bent elbow across forehead + brisk forward lean)
            if (isRaining && humanoid != null)
            {
                Vector3 shieldHand = transform.position + Vector3.up * 1.56f + transform.forward * 0.25f + transform.right * 0.04f;
                humanoid.SetDualHandAction(true, Vector3.zero, shieldHand, 0f, 0.78f, 14f, 14f, 0.02f);
            }
            else
            {
                ClearDualHandPose();
            }

            UpdateHeadEyeContact();

            if (waypoints == null || waypoints.Length < 2)
            {
                if (humanoid != null) humanoid.SetLocomotion(0f, grounded: true, verticalVelocity: 0f);
                return;
            }

            float desiredSpeed = 0f;
            float effectiveWalkSpeed = isRaining ? Mathf.Max(walkSpeed * 1.9f, 2.55f) : (isNight ? walkSpeed * 1.25f : walkSpeed);

            if (yieldingToCar)
            {
                desiredSpeed = 0f;
            }
            else if (_idleTimer > 0f && !isRaining)
            {
                _idleTimer -= dt;
                desiredSpeed = 0f;
            }
            else
            {
                if (!isRaining && !isNight)
                {
                    _midWalkIdleCooldown -= dt;
                    if (_midWalkIdleCooldown <= 0f)
                    {
                        _midWalkIdleCooldown = Random.Range(14f, 28f);
                        _idleTimer = Random.Range(2.5f, 5.0f);
                    }
                }

                if (_idleTimer <= 0f || isRaining)
                {
                    Vector3 target = waypoints[_waypointIndex];
                    Vector3 toTarget = target - transform.position;
                    toTarget.y = 0f;
                    float dist = toTarget.magnitude;

                    if (dist < 0.48f)
                    {
                        AdvanceScheduleWaypoint(isRaining, isNight);
                    }
                    else
                    {
                        Vector3 baseDir = toTarget / dist;
                        Vector3 smartDir = ApplyPersonalSpaceAvoidance(baseDir);
                        Quaternion targetRot = Quaternion.LookRotation(smartDir, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-turnSpeed * dt));

                        float facingDot = Mathf.Clamp01(Vector3.Dot(transform.forward, smartDir));
                        desiredSpeed = effectiveWalkSpeed * Mathf.Lerp(0.35f, 1f, facingDot);
                        Vector3 moveDir = transform.forward;
                        MoveSafely(moveDir * (_currentSpeed * dt), ref moveDir, false);
                    }
                }
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, desiredSpeed, 4.8f * dt);

            if (humanoid != null)
            {
                humanoid.SetLocomotion(_currentSpeed, grounded: true, verticalVelocity: 0f);
            }
        }

        private void MoveSafely(Vector3 displacement, ref Vector3 persistentDir, bool steerPersistentDir)
        {
            displacement.y = 0f;
            float dist = displacement.magnitude;
            if (dist < 0.0001f) return;

            Vector3 dir = displacement / dist;
            Vector3 castOrigin = transform.position + Vector3.up * 0.92f;
            float checkDist = dist + 0.42f;
            int hitCount = Physics.SphereCastNonAlloc(castOrigin, 0.34f, dir, WallHits, checkDist, ~0, QueryTriggerInteraction.Ignore);

            bool blocked = false;
            RaycastHit closest = default;
            float bestDist = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit h = WallHits[i];
                if (h.collider == null) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.normal.y > 0.62f) continue; // Ignore flat ground & curbs
                if (h.collider.GetComponentInParent<TownPedestrian>() != null) continue;
                if (h.collider.GetComponentInParent<PlayerFootController>() != null) continue;

                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    closest = h;
                    blocked = true;
                }
            }

            Vector3 finalMove = displacement;
            if (blocked)
            {
                Vector3 flatNormal = closest.normal;
                flatNormal.y = 0f;
                if (flatNormal.sqrMagnitude > 0.001f)
                {
                    flatNormal.Normalize();
                    Vector3 slideDir = Vector3.ProjectOnPlane(dir, flatNormal);
                    if (slideDir.sqrMagnitude < 0.04f)
                    {
                        Vector3 rightTangent = Vector3.Cross(Vector3.up, flatNormal).normalized;
                        slideDir = Vector3.Dot(rightTangent, transform.right) >= 0f ? rightTangent : -rightTangent;
                    }
                    else
                    {
                        slideDir.Normalize();
                    }

                    // Verify slide direction isn't also immediately blocked by a corner
                    int slideHits = Physics.SphereCastNonAlloc(castOrigin, 0.30f, slideDir, WallHits, dist + 0.30f, ~0, QueryTriggerInteraction.Ignore);
                    bool slideBlocked = false;
                    for (int i = 0; i < slideHits; i++)
                    {
                        RaycastHit sh = WallHits[i];
                        if (sh.collider == null || sh.collider.transform.IsChildOf(transform) || sh.normal.y > 0.62f) continue;
                        if (sh.collider.GetComponentInParent<TownPedestrian>() != null || sh.collider.GetComponentInParent<PlayerFootController>() != null) continue;
                        slideBlocked = true;
                        break;
                    }

                    if (!slideBlocked)
                    {
                        finalMove = slideDir * dist;
                        if (steerPersistentDir)
                        {
                            persistentDir = slideDir;
                        }
                    }
                    else
                    {
                        // Cornered: reverse smoothly away from the wall
                        finalMove = flatNormal * (dist * 0.5f);
                        if (steerPersistentDir)
                        {
                            persistentDir = flatNormal;
                        }
                    }
                }
                else
                {
                    finalMove = Vector3.zero;
                }
            }

            Vector3 nextPos = transform.position + finalMove;
            nextPos.x = Mathf.Clamp(nextPos.x, -165f, 165f);
            nextPos.z = Mathf.Clamp(nextPos.z, -165f, 165f);
            transform.position = nextPos;
        }

        private Vector3 ApplyPersonalSpaceAvoidance(Vector3 desiredDir)
        {
            Vector3 steer = desiredDir;

            // Smoothly step around the player on foot so NPCs never walk straight through Deniz!
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode != null && !mode.IsDriving)
            {
                Vector3 awayPlayer = transform.position - mode.ActiveWorldPosition;
                awayPlayer.y = 0f;
                float pDistSq = awayPlayer.sqrMagnitude;
                if (pDistSq > 0.001f && pDistSq < 1.85f * 1.85f)
                {
                    float pDist = Mathf.Sqrt(pDistSq);
                    float weight = Mathf.InverseLerp(1.85f, 0.55f, pDist) * 1.45f;
                    steer += (awayPlayer / pDist) * weight;
                }
            }

            // Smoothly step around neighboring pedestrians!
            for (int i = 0; i < ActiveCitizens.Count; i++)
            {
                TownPedestrian other = ActiveCitizens[i];
                if (other == null || other == this) continue;
                Vector3 away = transform.position - other.transform.position;
                away.y = 0f;
                float dSq = away.sqrMagnitude;
                if (dSq > 0.001f && dSq < 1.35f * 1.35f)
                {
                    float d = Mathf.Sqrt(dSq);
                    float w = Mathf.InverseLerp(1.35f, 0.45f, d) * 1.25f;
                    steer += (away / d) * w;
                }
            }

            steer.y = 0f;
            return steer.sqrMagnitude > 0.001f ? steer.normalized : desiredDir;
        }

        private void UpdateHeadEyeContact()
        {
            if (humanoid == null) return;
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null)
            {
                humanoid.SetLookAtPoint(false, Vector3.zero);
                return;
            }

            Vector3 pPos = mode.ActiveWorldPosition;
            Vector3 toP = pPos - transform.position;
            toP.y = 0f;
            if (toP.sqrMagnitude <= 8.5f * 8.5f)
            {
                humanoid.SetLookAtPoint(true, pPos + Vector3.up * 1.55f);
            }
            else
            {
                humanoid.SetLookAtPoint(false, Vector3.zero);
            }
        }

        private void EvaluateSmartBrain()
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsModalLocked) return;

            Vector3 pPos = mode.ActiveWorldPosition;
            Vector3 toPlayer = pPos - transform.position;
            toPlayer.y = 0f;
            float playerDistSq = toPlayer.sqrMagnitude;
            if (playerDistSq > 48f * 48f) return; // Distance-cull heavy social brain beyond 48 m

            bool isEn = IsEnglish;

            // 1. Spontaneous Rain Reaction Bubble!
            if (IsRaining() && _rainSpeechCooldown <= 0f && playerDistSq <= 18f * 18f && Random.value < 0.16f)
            {
                _rainSpeechCooldown = Random.Range(28f, 45f);
                string rainLine = isEn ? "Whoa, sudden rain! Better hurry!" : "Eyvah yağmur bastırdı, şemsiye de yok!";
                NpcSpeechBubbleManager.Say(transform, citizenName, rainLine, 3.0f, NpcBubbleMood.Weather);
                return;
            }

            // 2. Spontaneous Reputation / Time Greeting to Player passing close by!
            if (_playerGreetCooldown <= 0f && _grudgeTimer <= 0f && !mode.IsDriving && playerDistSq <= 6.2f * 6.2f)
            {
                _playerGreetCooldown = Random.Range(24f, 42f);
                if (Random.value < 0.45f)
                {
                    if (humanoid != null) humanoid.PlayWave();
                    string greet = BuildPassingGreeting(isEn);
                    NpcSpeechBubbleManager.Say(transform, citizenName, greet, 3.2f, NpcBubbleMood.Friendly);
                    return;
                }
            }

            // 3. Autonomous Neighbor-to-Neighbor Conversation!
            if (_socialChatCooldown <= 0f && !IsRaining() && !IsNightTime() && playerDistSq <= 30f * 30f && Random.value < 0.22f)
            {
                for (int i = 0; i < ActiveCitizens.Count; i++)
                {
                    TownPedestrian other = ActiveCitizens[i];
                    if (other == null || other == this || other.IsBusyOrPanicking || other._socialChatCooldown > 0f) continue;
                    Vector3 d = other.transform.position - transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude >= 1.1f * 1.1f && d.sqrMagnitude <= 3.3f * 3.3f)
                    {
                        StartNeighborConversation(other);
                        return;
                    }
                }
            }

            // 4. Curious World Micro-Behaviors (Petting street cats / Watching nearby sports!)
            if (_curiosityCooldown <= 0f && !IsRaining() && !IsNightTime() && playerDistSq <= 26f * 26f && Random.value < 0.20f)
            {
                // Check nearby sports arenas
                for (int i = 0; i < SportArenaCenters.Length; i++)
                {
                    Vector3 d = SportArenaCenters[i] - transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude <= 17f * 17f)
                    {
                        _curiosityCooldown = Random.Range(28f, 45f);
                        _watchTargetPos = SportArenaCenters[i];
                        _watchTimer = Random.Range(4.0f, 6.2f);
                        _watchingCat = false;
                        _brainState = CitizenBrainState.WatchingInterest;
                        if (humanoid != null) humanoid.PlayWave();
                        string watchMsg = isEn ? "Great match going on over there!" : "Saha bugün çok heyecanlı, güzel maç!";
                        NpcSpeechBubbleManager.Say(transform, citizenName, watchMsg, 3.2f, NpcBubbleMood.Weather);
                        return;
                    }
                }

                // Check nearby street cats
                StreetCatController cat = FindNearestStreetCat(4.8f);
                if (cat != null)
                {
                    _curiosityCooldown = Random.Range(30f, 48f);
                    _watchTargetPos = cat.transform.position;
                    _watchTimer = Random.Range(3.6f, 5.2f);
                    _watchingCat = true;
                    _brainState = CitizenBrainState.WatchingInterest;
                    string catMsg = isEn ? "Aww, pspsps! Cute little kitty!" : "Pisi pisi, gel bakalım ne tatlısın sen!";
                    NpcSpeechBubbleManager.Say(transform, citizenName, catMsg, 3.2f, NpcBubbleMood.Weather);
                }
            }
        }

        private string BuildPassingGreeting(bool isEn)
        {
            int rank = NeighborhoodReputation.CurrentRankIndex;
            if (rank >= 3)
            {
                return isEn ? "Hey legend! Proud of you, Deniz!" : "Selam efsane! Hamidiye seninle gurur duyuyor!";
            }
            if (rank >= 1)
            {
                return isEn ? "Hey Deniz, good to see you!" : "Selam Deniz, kolay gelsin komşu!";
            }

            float h = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentHour : 12f;
            if (isEn)
            {
                return h < 11.5f ? "Good morning, neighbor!" : (h < 18f ? "Lovely day in Hamidiye!" : "Good evening, neighbor!");
            }
            return h < 11.5f ? "Günaydın komşu!" : (h < 18f ? "Hayırlı günler komşu!" : "İyi akşamlar komşu!");
        }

        private void StartNeighborConversation(TownPedestrian other)
        {
            float duration = Random.Range(5.4f, 7.2f);
            _chatPartner = other;
            other._chatPartner = this;
            _brainState = CitizenBrainState.ChattingWithNeighbor;
            other._brainState = CitizenBrainState.ChattingWithNeighbor;
            _chatTimer = duration;
            other._chatTimer = duration;
            _socialChatCooldown = Random.Range(32f, 52f);
            other._socialChatCooldown = Random.Range(32f, 52f);
            _currentSpeed = 0f;
            other._currentSpeed = 0f;

            var conv = NeighborConversations[Random.Range(0, NeighborConversations.Length)];
            bool isEn = IsEnglish;
            string lineA = isEn ? conv.aEn : conv.aTr;
            string lineB = isEn ? conv.bEn : conv.bTr;

            if (humanoid != null) humanoid.PlayWave();
            NpcSpeechBubbleManager.Say(transform, citizenName, lineA, 3.0f, NpcBubbleMood.Friendly);

            other._pendingChatReply = lineB;
            other._chatReplyDelay = 2.1f;
        }

        private void UpdateNeighborChat(float dt)
        {
            _chatTimer -= dt;
            if (_chatPartner == null || _chatTimer <= 0f || IsRaining())
            {
                EndNeighborChat();
                return;
            }

            Vector3 toPartner = _chatPartner.transform.position - transform.position;
            toPartner.y = 0f;
            if (toPartner.sqrMagnitude > 0.01f)
            {
                Quaternion rot = Quaternion.LookRotation(toPartner.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-7f * dt));
            }

            if (humanoid != null)
            {
                humanoid.SetLocomotion(0f, true, 0f);
                humanoid.SetLookAtPoint(true, _chatPartner.transform.position + Vector3.up * 1.55f);
            }

            if (!string.IsNullOrEmpty(_pendingChatReply))
            {
                _chatReplyDelay -= dt;
                if (_chatReplyDelay <= 0f)
                {
                    if (humanoid != null) humanoid.PlayWave();
                    NpcSpeechBubbleManager.Say(transform, citizenName, _pendingChatReply, 3.2f, NpcBubbleMood.Friendly);
                    _pendingChatReply = null;
                }
            }
        }

        private void UpdateWatchingInterest(float dt)
        {
            _watchTimer -= dt;
            Vector3 toTarget = _watchTargetPos - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTarget.normalized, Vector3.up), 1f - Mathf.Exp(-7f * dt));
            }

            if (humanoid != null)
            {
                humanoid.SetLocomotion(0f, true, 0f);
                humanoid.SetLookAtPoint(true, _watchTargetPos + Vector3.up * (_watchingCat ? 0.35f : 1.5f));
                if (_watchingCat)
                {
                    Vector3 reach = transform.position + transform.forward * 0.45f + Vector3.up * 0.55f;
                    humanoid.SetDualHandAction(true, Vector3.zero, reach, 0f, 0.85f, 24f, 26f, 0.14f);
                }
            }

            if (_watchTimer <= 0f)
            {
                ClearDualHandPose();
                _brainState = CitizenBrainState.Patrol;
            }
        }

        private void UpdateBraveConfrontation(float dt)
        {
            _confrontTimer -= dt;
            if (_confrontPunchCooldown > 0f) _confrontPunchCooldown -= dt;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || _confrontTimer <= 0f)
            {
                _confrontTimer = 0f;
                _brainState = CitizenBrainState.Patrol;
                return;
            }

            Vector3 toPlayer = mode.ActiveWorldPosition - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist > 16f || mode.IsDriving)
            {
                _confrontTimer = 0f;
                _brainState = CitizenBrainState.Patrol;
                return;
            }

            if (toPlayer.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer.normalized, Vector3.up), 1f - Mathf.Exp(-10f * dt));
            }

            float spd = 0f;
            if (dist > 1.75f)
            {
                spd = 3.1f;
                Vector3 chaseDir = transform.forward;
                MoveSafely(chaseDir * (spd * dt), ref chaseDir, false);
            }
            else if (_confrontPunchCooldown <= 0f)
            {
                _confrontPunchCooldown = 2.0f;
                if (humanoid != null) humanoid.PlayCombatMove(1);
                if (PlayerNeedsController.Instance != null) PlayerNeedsController.Instance.ConsumeEnergy(5f);
                string warn = IsEnglish ? "Back off right now!" : "Geri çekil dedim sana!";
                NpcSpeechBubbleManager.Say(transform, citizenName, warn, 2.5f, NpcBubbleMood.Alert);
            }

            if (humanoid != null)
            {
                humanoid.SetLocomotion(spd, true, 0f);
                humanoid.SetLookAtPoint(true, mode.ActiveWorldPosition + Vector3.up * 1.55f);
            }
        }

        private StreetCatController FindNearestStreetCat(float maxRange)
        {
            StreetCatController[] cats = FindObjectsByType<StreetCatController>(FindObjectsInactive.Exclude);
            float maxSq = maxRange * maxRange;
            for (int i = 0; i < cats.Length; i++)
            {
                if (cats[i] == null) continue;
                Vector3 d = cats[i].transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude <= maxSq) return cats[i];
            }
            return null;
        }

        private static bool IsRaining()
        {
            return NeighborhoodWeatherSystem.Instance != null &&
                   NeighborhoodWeatherSystem.Instance.CurrentWeather == NeighborhoodWeatherState.SummerShower;
        }

        private static bool IsNightTime()
        {
            if (DayNightCycle.Instance == null) return false;
            float h = DayNightCycle.Instance.CurrentHour;
            return h >= 20.5f || h < 5.5f;
        }

        private void AdvanceScheduleWaypoint(bool isRaining, bool isNight)
        {
            // Sit on a nearby unoccupied bench only during pleasant daytime weather
            if (!isRaining && !isNight && candidateSeats != null && candidateSeats.Length > 0 && Random.value < 0.35f)
            {
                for (int i = 0; i < candidateSeats.Length; i++)
                {
                    SeatInteractable seat = candidateSeats[i];
                    if (seat == null || seat.IsOccupied) continue;

                    float distSq = (seat.SitPoint.position - transform.position).sqrMagnitude;
                    if (distSq <= 42f)
                    {
                        _preSitPosition = transform.position;
                        _preSitRotation = transform.rotation;
                        _occupiedSeat = seat;
                        _occupiedSeat.SetOccupied(true);
                        _sitTimer = Random.Range(10f, 16f);
                        _currentSpeed = 0f;
                        transform.SetPositionAndRotation(seat.SitPoint.position, seat.SitPoint.rotation);
                        if (humanoid != null)
                        {
                            humanoid.SetLocomotion(0f, true, 0f);
                            humanoid.SetSitting(true, driving: false);
                        }
                        return;
                    }
                }
            }

            _idleTimer = isRaining ? 0.1f : (isNight ? 0.5f : (Random.value < 0.65f ? Random.Range(2.5f, 5.2f) : Random.Range(0.7f, 1.5f)));

            if (waypoints.Length == 2)
            {
                _waypointIndex = 1 - _waypointIndex;
            }
            else
            {
                int next = _waypointIndex + _waypointStep;
                if (next >= waypoints.Length)
                {
                    _waypointStep = -1;
                    next = waypoints.Length - 2;
                }
                else if (next < 0)
                {
                    _waypointStep = 1;
                    next = 1;
                }
                _waypointIndex = Mathf.Clamp(next, 0, waypoints.Length - 1);
            }
        }

        private bool EvaluateApproachingCar()
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || !mode.IsDriving || mode.CurrentVehicle == null)
            {
                return false;
            }

            SedanVehicleController car = mode.CurrentVehicle;
            Vector3 toPed = transform.position - car.transform.position;
            toPed.y = 0f;
            float dist = toPed.magnitude;
            if (dist > 6.5f) return false;

            float carKmh = car.SpeedKmh;
            if (carKmh >= 18f) return false;

            Vector3 toCar = -toPed;
            bool inFrontOfPed = dist <= 3.6f && Vector3.Dot(transform.forward, toCar.normalized) > 0.35f;
            bool carHeadingTowardPed = dist <= 4.0f && carKmh > 2.5f && Vector3.Dot(car.transform.forward, toPed.normalized) > 0.65f;

            return inFrontOfPed || carHeadingTowardPed;
        }
    }
}
