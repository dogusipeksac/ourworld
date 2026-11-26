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
        KnockedDown = 7,
        FollowingPlayer = 8
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
            ("Akşam yemeğine ne pişirsek acaba?", "Bakkaldan taze sebze al, mis gibi olur.", "What should we cook for dinner tonight?", "Fresh veggies from Selman Market never fail."),
            ("Havadis Sokağı'ndaki yeni taş fırını gördün mü?", "Evet, sıcak börek kokusu Fatih Bulvarı'na kadar geliyor!", "Seen the new stone bakery on Havadis Street?", "Yes, fresh pastry aroma reaches all the way to Fatih Blvd!"),
            ("Kadife Sokağı'ndaki kıraathane çok güzel olmuş.", "Akşam Sadık Abi'nin çayına gidelim mutlaka!", "The tea house on Kadife Street looks so cozy.", "Let's grab tea at Sadık's place tonight!")
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

        private enum SeatPhase
        {
            None = 0,
            Approaching = 1,
            Turning = 2,
            SittingDown = 3,
            Seated = 4,
            StandingUp = 5
        }

        // Bench sitting state
        private SeatInteractable _occupiedSeat;
        private SeatPhase _seatPhase;
        private float _seatPhaseTimer;
        private float _seatStandUpDuration = 0.85f;
        private Vector3 _seatApproachPoint;
        private Vector3 _seatStandPoint;
        private float _seatFloorY;
        private Vector3 _seatMoveFrom;
        private Quaternion _seatRotFrom = Quaternion.identity;
        private Vector3 _preSitPosition;
        private Quaternion _preSitRotation = Quaternion.identity;
        private float _sitTimer;

        // Smooth stationary turn state (greetings, conversations, watching, confrontation)
        private Quaternion _faceTarget = Quaternion.identity;
        private float _faceTurnTimer;

        // Smart Brain State & Social Memory
        private CitizenBrainState _brainState = CitizenBrainState.Patrol;
        private float _brainThinkTimer;
        private float _playerGreetCooldown;
        private float _socialChatCooldown;
        private float _curiosityCooldown;
        private float _rainSpeechCooldown;
        private float _grudgeTimer;
        private int _timesTalked;
        private int _friendshipPoints;
        private bool _giftedPlayer;

        // Smart Companion Follow state ("Beraber Yürüyelim")
        private float _followPlayerTimer;
        private float _followCommentCooldown;
        private string _lastFollowStreet = string.Empty;
        private StreetNameTracker _cachedStreetTracker;

        // Neighbor-to-neighbor conversation state
        private TownPedestrian _chatPartner;
        private float _chatTimer;
        private float _chatReplyDelay;
        private string _pendingChatReply;

        // Curiosity watching state (cat or sport)
        private Vector3 _watchTargetPos;
        private float _watchTimer;
        private bool _watchingCat;

        // Brave confrontation, vehicle yield & horn sidestep state
        private float _confrontTimer;
        private float _confrontPunchCooldown;
        private Vector3 _sidestepDir = Vector3.right;
        private float _sidestepPeakSpeed;
        private float _sidestepDuration = 0.85f;
        private float _sidestepTimer;
        private float _carSidestepCooldown;

        // Rain head-shield smooth blend state
        private float _rainShieldWeight;
        private bool _rainShieldActive;

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
        public string CitizenRole => citizenRole;
        public string VendorShopId => vendorShopId;
        public bool HasGrudge => _grudgeTimer > 0f;
        public bool IsFollowingPlayer => _followPlayerTimer > 0f;
        public bool CanWalkWithPlayer => waypoints != null && waypoints.Length >= 2;
        public int FriendshipPoints => _friendshipPoints;
        public int FriendshipTier => _friendshipPoints >= 60 ? 3 : (_friendshipPoints >= 30 ? 2 : (_friendshipPoints >= 10 ? 1 : 0));
        public bool IsBusyOrPanicking =>
            _occupiedSeat != null || _knockdownTimer > 0f || _isAirborne || _fleeTimer > 0f ||
            _followPlayerTimer > 0f ||
            _brainState == CitizenBrainState.ChattingWithNeighbor || _brainState == CitizenBrainState.ConfrontPlayer;

        public void AddFriendship(int delta)
        {
            _friendshipPoints = Mathf.Clamp(_friendshipPoints + delta, 0, 100);
        }

        public void ClearGrudgeAndMakePeace()
        {
            _grudgeTimer = 0f;
            _confrontTimer = 0f;
            _fleeTimer = 0f;
            _hitCount = 0;
            _friendshipPoints = Mathf.Max(_friendshipPoints, 10);
            if (_brainState == CitizenBrainState.ConfrontPlayer || _brainState == CitizenBrainState.Fleeing)
            {
                _brainState = CitizenBrainState.Patrol;
            }
            if (humanoid != null) humanoid.PlayWave();
        }

        public void StartFollowingPlayer(float duration = 45f)
        {
            if (!CanWalkWithPlayer) return;
            EndNeighborChat();
            ReleaseBenchIfSeated();
            _watchTimer = 0f;
            _confrontTimer = 0f;
            _fleeTimer = 0f;
            _idleTimer = 0f;
            _followPlayerTimer = Mathf.Max(15f, duration);
            _followCommentCooldown = 4.0f;
            _brainState = CitizenBrainState.FollowingPlayer;
            if (_cachedStreetTracker == null) _cachedStreetTracker = FindAnyObjectByType<StreetNameTracker>();
            _lastFollowStreet = _cachedStreetTracker != null ? _cachedStreetTracker.CurrentStreetName : string.Empty;

            if (humanoid != null) humanoid.PlayWave();
            string msg = IsEnglish
                ? "Awesome, let's stroll through Hamidiye together!"
                : "Harika, hadi mahalleyi beraber turlayalım!";
            NpcSpeechBubbleManager.Say(transform, citizenName, msg, 3.4f, NpcBubbleMood.Friendly);
        }

        public void StopFollowingPlayer(bool sayGoodbye = true)
        {
            if (_followPlayerTimer <= 0f) return;
            _followPlayerTimer = 0f;
            _brainState = CitizenBrainState.Patrol;
            if (sayGoodbye)
            {
                if (humanoid != null) humanoid.PlayWave();
                string msg = IsEnglish
                    ? "Great walk! Catch you later, Deniz!"
                    : "Çok keyifli yürüyüştü Deniz, görüşürüz!";
                NpcSpeechBubbleManager.Say(transform, citizenName, msg, 3.2f, NpcBubbleMood.Friendly);
            }
        }

        public void TriggerFriendlyEmote(bool celebrate = false)
        {
            if (humanoid == null) return;
            humanoid.SetTalking(true, 3.2f);
            if (celebrate) humanoid.PlaySportAnim(SportAnimKind.VictoryJump, 0.65f);
            else humanoid.PlayWave();
        }

        public string GetSmartGreetingLine(bool isEn)
        {
            return BuildSmartContextualDialogue(isEn);
        }

        public string GetNextPersonalChatLine(bool isEn)
        {
            _timesTalked++;
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
                BeginSmoothFace(toPlayer);
            }

            // Keep immediate shopping-cart checkout fast-path for vendors!
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

            if (humanoid != null)
            {
                humanoid.SetTalking(true, 3.8f);
                humanoid.SetLookAtPoint(true, playerWorldPos + Vector3.up * 1.55f);
            }

            // Open the full interactive smart-choice panel when story dialogue isn't occupying the HUD!
            if (MissionManager.Instance == null || !MissionManager.Instance.IsDialoguePlaying)
            {
                AddFriendship(_grudgeTimer > 0f ? 0 : 8);
                SmartNpcInteractionPanel.EnsureInstance().OpenForTownPedestrian(this, playerWorldPos);
                return;
            }

            // Fallback during active story dialogue: quick overhead bubble + status toast
            bool isEn = IsEnglish;
            if (_grudgeTimer > 0f)
            {
                if (humanoid != null) humanoid.PlayStartled(0.8f);
                string grudgeLine = isEn
                    ? "Stay away from me! I haven't forgotten what you just did."
                    : "Benden uzak dur! Az önce yaptığını unutmadım.";
                NpcSpeechBubbleManager.Say(transform, citizenName, grudgeLine, 3.6f, NpcBubbleMood.Alert);
                PlayerModeController.Instance?.RaiseStatusMessage($"{citizenName}: {grudgeLine}");
                return;
            }

            _timesTalked++;
            string line = BuildSmartContextualDialogue(isEn);
            NpcSpeechBubbleManager.Say(transform, citizenName, line, 4.0f, NpcBubbleMood.Friendly);
            PlayerModeController.Instance?.RaiseStatusMessage($"{citizenName}: {line}");
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
                BeginSmoothFace(-knockDir);
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
            _sidestepDir = rightOfCar * sideSign;
            _sidestepPeakSpeed = 3.2f;
            _sidestepDuration = 0.85f;
            _sidestepTimer = 0.85f;
            _carSidestepCooldown = 1.2f;
            _idleTimer = Mathf.Max(_idleTimer, 1.4f);

            if (toMe.sqrMagnitude > 0.04f)
            {
                BeginSmoothFace(-toMe);
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
            BeginSmoothFace(arenaPos - transform.position);
            if (humanoid != null) humanoid.PlaySportAnim(SportAnimKind.VictoryJump, 0.62f);

            bool isEn = IsEnglish;
            string cheer = isEn ? "What a victory! Go Deniz!" : "Helal olsun Deniz! Müthiş maç!";
            NpcSpeechBubbleManager.Say(transform, citizenName, cheer, 3.2f, NpcBubbleMood.Weather);
        }

        private static Vector3 FlatForward(Transform t)
        {
            Vector3 f = t != null ? t.forward : Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }

        private static float ComputeSeatFloor(Transform sitPoint, float fallbackY)
        {
            if (sitPoint == null) return fallbackY;
            Vector3 origin = sitPoint.position + sitPoint.forward * 0.47f + Vector3.up * 0.2f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.point.y < sitPoint.position.y - 0.15f)
            {
                return hit.point.y;
            }
            return fallbackY;
        }

        private void BeginSmoothFace(Vector3 worldDir, float duration = 0.35f)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude <= 0.001f) return;
            _faceTarget = Quaternion.LookRotation(worldDir.normalized, Vector3.up);
            _faceTurnTimer = Mathf.Max(_faceTurnTimer, duration);
        }

        private bool UpdateSmoothFace(float dt)
        {
            if (_faceTurnTimer <= 0f) return false;
            _faceTurnTimer = Mathf.Max(0f, _faceTurnTimer - dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, _faceTarget, 1f - Mathf.Exp(-12f * dt));
            return Quaternion.Angle(transform.rotation, _faceTarget) > 4f;
        }

        private void ReleaseBenchIfSeated()
        {
            if (_occupiedSeat != null)
            {
                if ((_seatPhase == SeatPhase.Seated || _seatPhase == SeatPhase.SittingDown || _seatPhase == SeatPhase.StandingUp) &&
                    _occupiedSeat.SitPoint != null)
                {
                    Vector3 standPos = _occupiedSeat.SitPoint.position + FlatForward(_occupiedSeat.SitPoint) * 0.6f;
                    standPos.y = _preSitPosition.y;
                    transform.position = standPos;
                }
                _occupiedSeat.SetOccupied(false);
                _occupiedSeat = null;
            }
            _seatPhase = SeatPhase.None;
            _seatPhaseTimer = 0f;
            if (humanoid != null && humanoid.IsSitting)
            {
                humanoid.SetSitting(false, driving: false);
            }
        }

        private void ClearDualHandPose()
        {
            _rainShieldWeight = 0f;
            _rainShieldActive = false;
            if (humanoid != null)
            {
                humanoid.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f);
            }
        }

        private void UpdateRainShieldPose(float dt, bool wantShield)
        {
            if (humanoid == null) return;
            float rate = wantShield ? 3.0f : 3.6f;
            _rainShieldWeight = Mathf.MoveTowards(_rainShieldWeight, wantShield ? 1f : 0f, rate * dt);
            if (_rainShieldWeight > 0.001f)
            {
                float scaleY = humanoid.transform.lossyScale.y;
                Vector3 headBase = humanoid.HeadTransform != null
                    ? humanoid.HeadTransform.position
                    : transform.position + Vector3.up * (1.56f * scaleY);
                Vector3 shieldHand = headBase
                    + transform.up * (0.21f * scaleY)
                    + transform.forward * (0.14f * scaleY)
                    + transform.right * (0.05f * scaleY);
                float w = Mathf.SmoothStep(0f, 1f, _rainShieldWeight);
                humanoid.SetDualHandAction(true, Vector3.zero, shieldHand, 0f, w * 0.84f, w * 11f, w * 9f, w * 0.015f);
                _rainShieldActive = true;
            }
            else if (_rainShieldActive)
            {
                ClearDualHandPose();
            }
        }

        private void UpdateVehicleSidestep(float dt)
        {
            if (_rainShieldActive) UpdateRainShieldPose(dt, false);
            _sidestepTimer = Mathf.Max(0f, _sidestepTimer - dt);
            float u = 1f - Mathf.Clamp01(_sidestepTimer / Mathf.Max(0.1f, _sidestepDuration));
            float env = Mathf.Sin(u * Mathf.PI);
            float stepSpd = _sidestepPeakSpeed * env;
            bool turning = UpdateSmoothFace(dt);

            Vector3 dir = _sidestepDir.sqrMagnitude > 0.001f ? _sidestepDir : transform.right;
            if (stepSpd > 0.01f)
            {
                Vector3 moveRef = dir;
                MoveSafely(dir * (stepSpd * dt), ref moveRef, false);
            }
            _currentSpeed = stepSpd;
            if (humanoid != null)
            {
                float locoSpeed = turning && stepSpd < 0.45f ? 0.5f : stepSpd;
                humanoid.SetLocomotion(locoSpeed, grounded: true, verticalVelocity: 0f, dir);
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
                    if (other.humanoid != null) other.humanoid.SetTalking(false);
                }
            }
            _chatTimer = 0f;
            _pendingChatReply = null;
            if (humanoid != null) humanoid.SetTalking(false);
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
            if (_carSidestepCooldown > 0f) _carSidestepCooldown -= dt;
            if (_playerGreetCooldown > 0f) _playerGreetCooldown -= dt;
            if (_socialChatCooldown > 0f) _socialChatCooldown -= dt;
            if (_curiosityCooldown > 0f) _curiosityCooldown -= dt;
            if (_rainSpeechCooldown > 0f) _rainSpeechCooldown -= dt;
            if (_grudgeTimer > 0f) _grudgeTimer -= dt;

            // 1. Seated bench state machine (walk to bench -> turn -> sit -> seated -> stand up smoothly)
            if (_occupiedSeat != null)
            {
                if (_rainShieldActive) UpdateRainShieldPose(dt, false);
                UpdateBenchSeatStateMachine(dt);
                return;
            }

            // 2. Ballistic air-launch after vehicle strike
            if (_isAirborne)
            {
                _brainState = CitizenBrainState.KnockedDown;
                _airVelocity.y -= 21.0f * dt;
                Vector3 nextAir = transform.position + _airVelocity * dt;
                nextAir.x = Mathf.Clamp(nextAir.x, -245f, 245f);
                nextAir.z = Mathf.Clamp(nextAir.z, -245f, 245f);

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

            // 6. Vehicle yield / horn sidestep dodge (smooth eased turn + wall-safe side-step)
            if (_sidestepTimer > 0f)
            {
                UpdateVehicleSidestep(dt);
                return;
            }

            // 7. Brave Citizen Confrontation!
            if (_confrontTimer > 0f)
            {
                if (_rainShieldActive) UpdateRainShieldPose(dt, false);
                UpdateBraveConfrontation(dt);
                return;
            }

            // 8. Autonomous Neighbor-to-Neighbor Sidewalk Conversation!
            if (_brainState == CitizenBrainState.ChattingWithNeighbor)
            {
                if (_rainShieldActive) UpdateRainShieldPose(dt, false);
                UpdateNeighborChat(dt);
                return;
            }

            // 9. Curious World Watching (Cat petting / Sports cheering!)
            if (_watchTimer > 0f)
            {
                if (_rainShieldActive && !_watchingCat) UpdateRainShieldPose(dt, false);
                UpdateWatchingInterest(dt);
                return;
            }

            // 9b. Smart Companion Walk-Along ("Beraber Yürüyelim")!
            if (_followPlayerTimer > 0f)
            {
                UpdateFollowingPlayer(dt);
                return;
            }

            // Periodic Smart Brain Evaluation (staggered, distance-culled within 48 m)
            _brainThinkTimer -= dt;
            if (_brainThinkTimer <= 0f)
            {
                _brainThinkTimer = Random.Range(0.28f, 0.44f);
                EvaluateSmartBrain();
            }

            // Check approaching player vehicle for yield / smooth side-step / startled pose
            bool yieldingToCar = EvaluateApproachingCar();
            if (_sidestepTimer > 0f)
            {
                UpdateVehicleSidestep(dt);
                return;
            }

            bool isRaining = IsRaining();
            bool isNight = IsNightTime();
            _brainState = isRaining ? CitizenBrainState.RainHurry : CitizenBrainState.Patrol;

            UpdateHeadEyeContact();

            if (waypoints == null || waypoints.Length < 2)
            {
                UpdateRainShieldPose(dt, false);
                bool turningIdle = UpdateSmoothFace(dt);
                if (humanoid != null) humanoid.SetLocomotion(turningIdle ? 0.5f : 0f, grounded: true, verticalVelocity: 0f);
                return;
            }

            float desiredSpeed = 0f;
            float effectiveWalkSpeed = isRaining ? Mathf.Max(walkSpeed * 1.9f, 2.55f) : (isNight ? walkSpeed * 1.25f : walkSpeed);
            bool turningInPlace = false;

            if (yieldingToCar)
            {
                desiredSpeed = 0f;
                turningInPlace = UpdateSmoothFace(dt);
            }
            else if (_idleTimer > 0f && !isRaining)
            {
                _idleTimer -= dt;
                desiredSpeed = 0f;
                turningInPlace = UpdateSmoothFace(dt);
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
                    _faceTurnTimer = 0f;
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

            // Smoothly blend single-hand overhead rain shield in while hurrying in rain, and release smoothly when stopping or rain ends!
            bool wantRainShield = isRaining && !yieldingToCar && _occupiedSeat == null && _currentSpeed > 0.35f;
            UpdateRainShieldPose(dt, wantRainShield);

            if (humanoid != null)
            {
                humanoid.SetLocomotion(turningInPlace ? Mathf.Max(_currentSpeed, 0.5f) : _currentSpeed, grounded: true, verticalVelocity: 0f);
            }
        }

        private void UpdateBenchSeatStateMachine(float dt)
        {
            if (_occupiedSeat == null || _occupiedSeat.SitPoint == null)
            {
                ReleaseBenchIfSeated();
                return;
            }

            _brainState = CitizenBrainState.BenchBreak;
            bool raining = IsRaining();
            Transform sitPoint = _occupiedSeat.SitPoint;

            // Fallback to instant behaviour if humanoid rig is absent
            if (humanoid == null)
            {
                _sitTimer -= dt;
                if (_sitTimer <= 0f || raining)
                {
                    _occupiedSeat.SetOccupied(false);
                    _occupiedSeat = null;
                    _seatPhase = SeatPhase.None;
                    transform.SetPositionAndRotation(_preSitPosition, _preSitRotation);
                    _idleTimer = raining ? 0f : 0.6f;
                    _brainState = raining ? CitizenBrainState.RainHurry : CitizenBrainState.Patrol;
                }
                return;
            }

            Vector3 seatFwd = FlatForward(sitPoint);

            switch (_seatPhase)
            {
                case SeatPhase.Approaching:
                {
                    if (raining)
                    {
                        _occupiedSeat.SetOccupied(false);
                        _occupiedSeat = null;
                        _seatPhase = SeatPhase.None;
                        _seatPhaseTimer = 0f;
                        _idleTimer = 0f;
                        _brainState = CitizenBrainState.RainHurry;
                        return;
                    }

                    _seatPhaseTimer += dt;
                    Vector3 toApproach = _seatApproachPoint - transform.position;
                    toApproach.y = 0f;
                    float dist = toApproach.magnitude;

                    if (dist < 0.12f || _seatPhaseTimer >= 6f)
                    {
                        if (_seatPhaseTimer >= 6f && dist >= 0.12f)
                        {
                            transform.position = _seatApproachPoint;
                        }
                        _seatPhase = SeatPhase.Turning;
                        _seatPhaseTimer = 0f;
                        _seatRotFrom = transform.rotation;
                        _currentSpeed = 0f;
                        humanoid.SetLocomotion(0.6f, true, 0f);
                    }
                    else
                    {
                        Vector3 dir = toApproach / dist;
                        Quaternion walkRot = Quaternion.LookRotation(dir, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, walkRot, 1f - Mathf.Exp(-turnSpeed * dt));
                        float facingDot = Mathf.Clamp01(Vector3.Dot(transform.forward, dir));
                        float desiredWalk = walkSpeed * Mathf.Lerp(0.45f, 1f, facingDot);
                        _currentSpeed = Mathf.MoveTowards(_currentSpeed, desiredWalk, 5.2f * dt);
                        float stepDist = Mathf.Min(dist, _currentSpeed * dt);
                        Vector3 moveDir = dir;
                        MoveSafely(dir * stepDist, ref moveDir, false);
                        humanoid.SetLocomotion(_currentSpeed, true, 0f, dir);
                    }

                    UpdateHeadEyeContact();
                    break;
                }

                case SeatPhase.Turning:
                {
                    if (raining)
                    {
                        _occupiedSeat.SetOccupied(false);
                        _occupiedSeat = null;
                        _seatPhase = SeatPhase.None;
                        _seatPhaseTimer = 0f;
                        _idleTimer = 0f;
                        _brainState = CitizenBrainState.RainHurry;
                        return;
                    }

                    _seatPhaseTimer += dt;
                    float t = Mathf.Clamp01(_seatPhaseTimer / 0.4f);
                    Quaternion targetRot = Quaternion.LookRotation(seatFwd, Vector3.up);
                    transform.rotation = Quaternion.Slerp(_seatRotFrom, targetRot, Mathf.SmoothStep(0f, 1f, t));
                    humanoid.SetLocomotion(0.6f * (1f - t), true, 0f);

                    if (t >= 1f)
                    {
                        transform.rotation = targetRot;
                        _seatMoveFrom = transform.position;
                        _seatRotFrom = transform.rotation;
                        _seatPhase = SeatPhase.SittingDown;
                        _seatPhaseTimer = 0f;
                        humanoid.BeginSeatTransition(_seatFloorY, true, 0.9f);
                    }
                    break;
                }

                case SeatPhase.SittingDown:
                {
                    _seatPhaseTimer += dt;
                    float posT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_seatPhaseTimer / 0.9f));
                    float rotT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_seatPhaseTimer / (0.9f * 0.66f)));
                    transform.SetPositionAndRotation(
                        Vector3.Lerp(_seatMoveFrom, sitPoint.position, posT),
                        Quaternion.Slerp(_seatRotFrom, sitPoint.rotation, rotT));
                    humanoid.SetLocomotion(0f, true, 0f);

                    if (_seatPhaseTimer >= 0.9f)
                    {
                        transform.SetPositionAndRotation(sitPoint.position, sitPoint.rotation);
                        _seatPhase = SeatPhase.Seated;
                        _seatPhaseTimer = 0f;
                    }
                    break;
                }

                case SeatPhase.Seated:
                {
                    _sitTimer -= dt;
                    if (_sitTimer <= 0f || raining)
                    {
                        _seatStandPoint = sitPoint.position + seatFwd * 0.6f;
                        _seatStandPoint.y = _preSitPosition.y;
                        _seatMoveFrom = transform.position;
                        _seatStandUpDuration = raining ? 0.6f : 0.85f;
                        _seatPhase = SeatPhase.StandingUp;
                        _seatPhaseTimer = 0f;
                        humanoid.BeginSeatTransition(_seatFloorY, false, _seatStandUpDuration);
                    }
                    else
                    {
                        UpdateHeadEyeContact();
                    }
                    break;
                }

                case SeatPhase.StandingUp:
                {
                    _seatPhaseTimer += dt;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_seatPhaseTimer / Mathf.Max(0.1f, _seatStandUpDuration)));
                    transform.position = Vector3.Lerp(_seatMoveFrom, _seatStandPoint, t);
                    humanoid.SetLocomotion(0f, true, 0f);

                    if (_seatPhaseTimer >= _seatStandUpDuration &&
                        (!humanoid.IsSeatTransitionActive || _seatPhaseTimer >= _seatStandUpDuration + 0.5f))
                    {
                        transform.position = _seatStandPoint;
                        _occupiedSeat.SetOccupied(false);
                        _occupiedSeat = null;
                        _seatPhase = SeatPhase.None;
                        _seatPhaseTimer = 0f;
                        if (humanoid.IsSitting) humanoid.SetSitting(false, driving: false);
                        _idleTimer = raining ? 0f : 0.6f;
                        _brainState = raining ? CitizenBrainState.RainHurry : CitizenBrainState.Patrol;
                    }
                    break;
                }

                default:
                    ReleaseBenchIfSeated();
                    break;
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
            nextPos.x = Mathf.Clamp(nextPos.x, -245f, 245f);
            nextPos.z = Mathf.Clamp(nextPos.z, -245f, 245f);
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

            if (humanoid != null) humanoid.SetTalking(true, 3.0f);
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
            bool turning = false;
            if (toPartner.sqrMagnitude > 0.01f)
            {
                BeginSmoothFace(toPartner, 0.25f);
                turning = UpdateSmoothFace(dt);
            }

            if (humanoid != null)
            {
                humanoid.SetLocomotion(turning ? 0.5f : 0f, true, 0f);
                humanoid.SetLookAtPoint(true, _chatPartner.transform.position + Vector3.up * 1.55f);
            }

            if (!string.IsNullOrEmpty(_pendingChatReply))
            {
                _chatReplyDelay -= dt;
                if (_chatReplyDelay <= 0f)
                {
                    if (humanoid != null) humanoid.SetTalking(true, 3.2f);
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
            bool turning = false;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                BeginSmoothFace(toTarget, 0.25f);
                turning = UpdateSmoothFace(dt);
            }

            if (humanoid != null)
            {
                humanoid.SetLocomotion(turning ? 0.5f : 0f, true, 0f);
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

        private void UpdateFollowingPlayer(float dt)
        {
            _brainState = CitizenBrainState.FollowingPlayer;
            _followPlayerTimer -= dt;
            if (_followCommentCooldown > 0f) _followCommentCooldown -= dt;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsDriving || _followPlayerTimer <= 0f || _grudgeTimer > 0f)
            {
                StopFollowingPlayer(sayGoodbye: _grudgeTimer <= 0f);
                return;
            }

            bool isRaining = IsRaining();
            UpdateRainShieldPose(dt, isRaining);

            Vector3 pPos = mode.ActiveWorldPosition;
            Transform pTf = mode.FootController != null ? mode.FootController.transform : null;
            Vector3 offset = pTf != null
                ? (-pTf.forward * 1.45f + pTf.right * 1.35f)
                : new Vector3(1.5f, 0f, -1.2f);
            Vector3 desiredPos = pPos + offset;
            desiredPos.y = transform.position.y;

            Vector3 toGoal = desiredPos - transform.position;
            toGoal.y = 0f;
            float dist = toGoal.magnitude;

            if (dist > 36f)
            {
                // Too far away — gracefully end companion walk
                StopFollowingPlayer(sayGoodbye: false);
                return;
            }

            float targetSpd = 0f;
            Vector3 moveDir = transform.forward;
            if (dist > 1.35f)
            {
                Vector3 dir = toGoal / dist;
                Vector3 smartDir = ApplyPersonalSpaceAvoidance(dir);
                Quaternion targetRot = Quaternion.LookRotation(smartDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-8.5f * dt));

                targetSpd = dist > 5.0f ? 3.9f : Mathf.Lerp(1.45f, 3.1f, Mathf.InverseLerp(1.35f, 5.0f, dist));
                moveDir = transform.forward;
                MoveSafely(moveDir * (targetSpd * dt), ref moveDir, false);
            }
            else
            {
                // Face player politely when Deniz pauses
                Vector3 toP = pPos - transform.position;
                toP.y = 0f;
                if (toP.sqrMagnitude > 0.04f)
                {
                    Quaternion lookRot = Quaternion.LookRotation(toP.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 1f - Mathf.Exp(-6.0f * dt));
                }
            }

            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpd, 1f - Mathf.Exp(-10f * dt));
            if (humanoid != null)
            {
                humanoid.SetLocomotion(_currentSpeed, grounded: true, verticalVelocity: 0f, moveDir);
                humanoid.SetLookAtPoint(true, pPos + Vector3.up * 1.55f);
            }

            // Smart street-change commentary while walking together!
            if (_followCommentCooldown <= 0f && _cachedStreetTracker != null)
            {
                string curStreet = _cachedStreetTracker.CurrentStreetName;
                if (!string.IsNullOrEmpty(curStreet) &&
                    !string.Equals(curStreet, _lastFollowStreet, System.StringComparison.Ordinal))
                {
                    _lastFollowStreet = curStreet;
                    _followCommentCooldown = 9.0f;
                    string comment = IsEnglish
                        ? $"Nice! We just reached {curStreet}!"
                        : $"Harika! {curStreet} tarafına geldik, burası çok canlı!";
                    NpcSpeechBubbleManager.Say(transform, citizenName, comment, 3.4f, NpcBubbleMood.Friendly);
                }
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

            bool turning = false;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                BeginSmoothFace(toPlayer, 0.25f);
                turning = UpdateSmoothFace(dt);
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
                humanoid.SetLocomotion(spd > 0f ? spd : (turning ? 0.5f : 0f), true, 0f);
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
                    if (seat == null || seat.IsOccupied || seat.SitPoint == null) continue;

                    float distSq = (seat.SitPoint.position - transform.position).sqrMagnitude;
                    if (distSq <= 42f)
                    {
                        _preSitPosition = transform.position;
                        _preSitRotation = transform.rotation;
                        _occupiedSeat = seat;
                        _occupiedSeat.SetOccupied(true);
                        _sitTimer = Random.Range(10f, 16f);
                        _faceTurnTimer = 0f;

                        if (humanoid == null)
                        {
                            _currentSpeed = 0f;
                            _seatPhase = SeatPhase.Seated;
                            transform.SetPositionAndRotation(seat.SitPoint.position, seat.SitPoint.rotation);
                        }
                        else
                        {
                            _seatApproachPoint = seat.SitPoint.position + FlatForward(seat.SitPoint) * 0.55f;
                            _seatApproachPoint.y = transform.position.y;
                            _seatFloorY = ComputeSeatFloor(seat.SitPoint, transform.position.y);
                            _seatPhase = SeatPhase.Approaching;
                            _seatPhaseTimer = 0f;
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
            if (dist > 13.5f || dist < 0.05f) return false;

            float carKmh = car.SpeedKmh;
            Vector3 carMoveDir = car.ForwardSpeedMs < -0.5f ? -car.transform.forward : car.transform.forward;
            carMoveDir.y = 0f;
            if (carMoveDir.sqrMagnitude < 0.001f) carMoveDir = Vector3.forward;
            else carMoveDir.Normalize();

            Vector3 carRight = Vector3.Cross(Vector3.up, carMoveDir).normalized;
            float forwardDist = Vector3.Dot(toPed, carMoveDir);
            float lateralOffset = Vector3.Dot(toPed, carRight);
            float absLateral = Mathf.Abs(lateralOffset);

            float detectRange = Mathf.Clamp(4.8f + carKmh * 0.16f, 4.8f, 12.5f);
            bool approachingInPath = carKmh > 2.2f && forwardDist > 0.35f && forwardDist <= detectRange && absLateral <= 2.65f;
            bool blockingInFront = dist <= 3.6f && Vector3.Dot(transform.forward, -toPed / dist) > 0.35f;

            if (!approachingInPath && !blockingInFront) return false;

            // Smoothly turn and watch the approaching vehicle (never snap or teleport!)
            BeginSmoothFace(-toPed, 0.35f);
            if (humanoid != null)
            {
                humanoid.SetLookAtPoint(true, car.transform.position + Vector3.up * 1.25f);
            }

            if (approachingInPath)
            {
                // Startled arms-up pose ONLY when the vehicle is fast and close
                bool fastAndClose = carKmh >= 18f && dist <= 7.5f && absLateral <= 2.15f;
                if (fastAndClose && humanoid != null && _startledCooldown <= 0f)
                {
                    humanoid.PlayStartled(0.85f);
                    _startledCooldown = 3.0f;
                }

                // Smoothly side-step perpendicular out of the vehicle's lane
                if (absLateral < 2.35f && _carSidestepCooldown <= 0f)
                {
                    float sideSign = absLateral > 0.12f
                        ? (lateralOffset >= 0f ? 1f : -1f)
                        : (Vector3.Dot(transform.right, carRight) >= 0f ? 1f : -1f);
                    _sidestepDir = carRight * sideSign;
                    _sidestepDuration = fastAndClose ? 0.65f : 0.80f;
                    _sidestepPeakSpeed = fastAndClose ? 3.2f : 1.85f;
                    _sidestepTimer = _sidestepDuration;
                    _carSidestepCooldown = 1.4f;
                }
            }

            return true;
        }
    }
}
