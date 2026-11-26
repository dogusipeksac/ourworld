using System;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Missions;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Calm, mobile-responsive interactive conversation & smart neighbor-favor overlay for ALL
    /// Hamidiye Mahallesi NPCs (TownPedestrian citizens and runtime LivingNeighborhoodPopulator neighbors):
    /// - Displays NPC identity, role, street context, and live Friendship Tier (Tanıdık -> Komşu -> Yakın Dost).
    /// - Offers 4 smart context-driven interactive choices (touch + keyboard 1-4 / ESC):
    ///     1. Deep contextual neighborhood chat & gossip (boosts friendship + animates NPC)
    ///     2. Smart directions that pin real landmarks (including Havadis Sk. & Kadife Sk.) on the minimap/compass
    ///     3. Dynamic action: Apologize (clears grudge), Complete/Accept Neighbor Mini-Favor (+50-55 TL + Rep),
    ///        Warm Tea Treat (+18 Energy when tired), or Companion Walk (NPC walks alongside Deniz!)
    ///     4. Friendly wave & parting bubble.
    /// </summary>
    public sealed class SmartNpcInteractionPanel : MonoBehaviour
    {
        public static SmartNpcInteractionPanel Instance { get; private set; }

        private enum DynamicActionMode
        {
            StartFavor,
            DeliverFavorHere,
            ClaimVisitedFavor,
            ApologizeGrudge,
            AcceptTeaTreat,
            StartCompanionWalk,
            StopCompanionWalk
        }

        private sealed class NeighborFavorState
        {
            public string RequesterName;
            public string TargetNpcName;
            public string TargetLandmarkTr;
            public string TargetLandmarkEn;
            public Vector3 TargetPosition;
            public bool IsRelayToNpc;
            public bool VisitedSpot;
            public int RewardMoney;
        }

        private struct LandmarkGuide
        {
            public string NameTr;
            public string NameEn;
            public string HintTr;
            public string HintEn;
            public Vector3 Position;
        }

        private static readonly LandmarkGuide[] Landmarks =
        {
            new LandmarkGuide
            {
                NameTr = "Hamidiye Atlas AVM (5 Katlı)",
                NameEn = "Hamidiye Atlas Mall (5 Levels)",
                HintTr = "Petrol Yolu ile Seyyid Kutup köşesinde 5 katlı Hamidiye Atlas AVM açıldı! Yemek katı, moda butiği, sinema ve teras kafe var — haritana işaretledim!",
                HintEn = "The 5-level Hamidiye Atlas Mall is open at Petrol Yolu & Seyyid Kutup with fashion, food court, cinema & rooftop terrace! Pinned on your map!",
                Position = new Vector3(-116.0f, 0.2f, -56.0f)
            },
            new LandmarkGuide
            {
                NameTr = "Havadis Sokağı · Taş Fırın",
                NameEn = "Havadis Street · Stone Bakery",
                HintTr = "Fatih Bulvarı'nın hemen kuzeyinde, Havadis Sokağı'nda Haydar Usta'nın taş fırını var. Haritana işaretledim!",
                HintEn = "Just north of Fatih Blvd on Havadis Street is Haydar's stone bakery. I pinned it on your map!",
                Position = new Vector3(-24.0f, 0.2f, 186.0f)
            },
            new LandmarkGuide
            {
                NameTr = "Kadife Sokağı · Mahalle Kıraathanesi",
                NameEn = "Kadife Street · Tea House",
                HintTr = "Zübeyde Hanım'ın güneyinde, Kadife Sokağı'ndaki Sadık Abi'nin kıraathanesini haritana işaretledim!",
                HintEn = "South of Zübeyde Hanım Ave on Kadife Street is Sadık's neighborhood tea house. Pinned on your map!",
                Position = new Vector3(-16.0f, 0.2f, -186.0f)
            },
            new LandmarkGuide
            {
                NameTr = "Fatih Bulvarı · Saat Kulesi Meydanı",
                NameEn = "Fatih Blvd · Clock Tower Plaza",
                HintTr = "Sultanbeyli'nin kalbi Saat Kulesi Meydanı'nı haritana işaretledim; taksi durağı ve kafeler orada!",
                HintEn = "I pinned Clock Tower Plaza on Fatih Blvd — great cafes and the taxi stand are right there!",
                Position = new Vector3(0.0f, 0.2f, 136.0f)
            },
            new LandmarkGuide
            {
                NameTr = "Selman Bakkal & Manav (No:58)",
                NameEn = "Selman Market & Grocer (No:58)",
                HintTr = "Hüseyin Abi'nin bakkalı Selman Sokağı No:58'de; taze meyve, dondurma ve sıcak tost bulursun!",
                HintEn = "Hüseyin's market at No:58 Selman Street has fresh produce, ice cream, and warm toast!",
                Position = new Vector3(6.5f, 0.2f, 4.5f)
            },
            new LandmarkGuide
            {
                NameTr = "Hamidiye Açık Yüzme Havuzu",
                NameEn = "Hamidiye Outdoor Pool",
                HintTr = "Serinlemek istersen Hamidiye Açık Yüzme Havuzu hemen güneyde! Pusulana işaretledim.",
                HintEn = "If you want to cool off, Hamidiye Outdoor Pool is just south. Pinned to your compass!",
                Position = new Vector3(16.0f, 0.2f, -31.0f)
            },
            new LandmarkGuide
            {
                NameTr = "Aydos Çiftliği & Yeşil Vadi",
                NameEn = "Aydos Farm & Green Valley",
                HintTr = "Selman Sokağı'nın doğu ucundan çıkınca Aydos Çiftliği'ne varırsın; inekler, keçiler ve bostan orada!",
                HintEn = "Head out the east end of Selman Street to reach Aydos Farm with animals and crop plots!",
                Position = new Vector3(156.0f, 0.2f, 0.0f)
            },
            new LandmarkGuide
            {
                NameTr = "Sultanbeyli Göleti & Balık İskelesi",
                NameEn = "Sultanbeyli Lake & Fishing Pier",
                HintTr = "Batıdaki Gölet Yürüyüş Yolu'ndan Sultanbeyli Göleti'ne inip balık tutabilirsin. Haritada işaretli!",
                HintEn = "Head west along Lakeside Promenade to fish at Sultanbeyli Lake. Marked on your map!",
                Position = new Vector3(-166.0f, 0.2f, 0.0f)
            },
            new LandmarkGuide
            {
                NameTr = "Oto Tamir & Modifiye (No:73)",
                NameEn = "Auto Repair & Tuning (No:73)",
                HintTr = "Arabanı tamir ettirmek veya modifiye etmek için Selman Sokağı No:73'ü haritana işaretledim!",
                HintEn = "I pinned Auto Repair No:73 on Selman Street if you want to repair or tune your car!",
                Position = new Vector3(24.5f, 0.2f, 4.5f)
            }
        };

        private static readonly Dictionary<string, int> SessionLivingRapport =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> TreatedThisSession =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static NeighborFavorState _activeFavor;

        private Canvas _canvas;
        private GameObject _panelRoot;
        private CanvasGroup _canvasGroup;
        private RectTransform _cardShellRect;
        private Image _topGlow;
        private Image _cardBorder;
        private Image _medallionBg;
        private Image _medallionRing;
        private Image _medallionIcon;
        private Image _friendshipDot;
        private Image _friendshipPillBg;
        private Image _friendshipPillBorder;
        private Text _nameText;
        private Text _roleBadgeText;
        private Text _friendshipBadgeText;
        private Text _bodyText;
        private Text _favorBannerText;
        private GameObject _favorBannerGo;
        private readonly Button[] _choiceButtons = new Button[4];
        private readonly Text[] _choiceLabels = new Text[4];
        private readonly Image[] _choiceBgs = new Image[4];
        private readonly Image[] _choiceBorders = new Image[4];
        private readonly Image[] _choiceDots = new Image[4];
        private float _openAnimT = 1f;

        // Current conversation target (either a TownPedestrian OR a LivingNeighborhoodPopulator actor)
        private TownPedestrian _activeCitizen;
        private Transform _activeLivingTransform;
        private ProceduralHumanoid _activeLivingHumanoid;
        private string _activeName = string.Empty;
        private string _activeRole = string.Empty;
        private string[] _activeLivingLines;
        private int _livingLineCursor;
        private int _directionCursor;
        private DynamicActionMode _currentDynamicMode;
        private int _openedFrame = -1;
        private float _spotCheckTimer;
        private static readonly HashSet<string> DailyQuestCreditedNpcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string _creditedNpcDate = string.Empty;

        private static void RecordUniqueNpcChatForDailyQuest(string npcName)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (!string.Equals(_creditedNpcDate, today, StringComparison.Ordinal))
            {
                _creditedNpcDate = today;
                DailyQuestCreditedNpcs.Clear();
            }
            string key = string.IsNullOrWhiteSpace(npcName) ? "neighbor" : npcName.Trim();
            if (DailyQuestCreditedNpcs.Add(key))
            {
                DailyQuestAndAchievementSystem.RecordNpcChatOrArcade();
            }
        }

        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        private static bool IsEnglish =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        public static SmartNpcInteractionPanel EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_SmartNpcInteractionPanel");
            return go.AddComponent<SmartNpcInteractionPanel>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildUi();
            _panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (IsOpen && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
            if (Instance == this) Instance = null;
        }

        public void OpenForTownPedestrian(TownPedestrian citizen, Vector3 playerPos)
        {
            if (citizen == null) return;
            if (MissionManager.Instance != null && MissionManager.Instance.IsDialoguePlaying) return;

            _activeCitizen = citizen;
            _activeLivingTransform = null;
            _activeLivingHumanoid = null;
            _activeName = citizen.CitizenName;
            _activeRole = citizen.CitizenRole;
            _activeLivingLines = null;

            bool isEn = IsEnglish;
            string opening = citizen.HasGrudge
                ? (isEn
                    ? "Hey! I haven't forgotten what you did earlier... What do you want?"
                    : "Hey! Az önce yaptığını unutmadım... Ne istiyorsun?")
                : citizen.GetSmartGreetingLine(isEn);

            RecordUniqueNpcChatForDailyQuest(_activeName);
            ShowPanel(opening);
        }

        public void OpenForLivingNpc(
            Transform npcTransform,
            ProceduralHumanoid humanoid,
            string displayName,
            string roleLabelTr,
            string roleLabelEn,
            string[] lines,
            Vector3 playerPos)
        {
            if (npcTransform == null) return;
            if (MissionManager.Instance != null && MissionManager.Instance.IsDialoguePlaying) return;

            _activeCitizen = null;
            _activeLivingTransform = npcTransform;
            _activeLivingHumanoid = humanoid;
            _activeName = string.IsNullOrEmpty(displayName) ? (IsEnglish ? "Neighbor" : "Mahalleli") : displayName;
            _activeRole = IsEnglish ? roleLabelEn : roleLabelTr;
            _activeLivingLines = lines;

            Vector3 toP = playerPos - npcTransform.position;
            toP.y = 0f;
            if (toP.sqrMagnitude > 0.04f && humanoid != null && !humanoid.IsSitting)
            {
                npcTransform.rotation = Quaternion.LookRotation(toP.normalized, Vector3.up);
            }
            if (humanoid != null)
            {
                humanoid.SetLookAtPoint(true, playerPos + Vector3.up * 1.55f);
                humanoid.SetTalking(true, 3.6f);
            }

            string opening = BuildLivingNpcSmartGreeting(IsEnglish);
            RecordUniqueNpcChatForDailyQuest(_activeName);
            ShowPanel(opening);
        }

        private void ShowPanel(string initialMessage)
        {
            _openedFrame = Time.frameCount;
            _openAnimT = 0f;
            _panelRoot.SetActive(true);
            _canvasGroup.alpha = 0f;
            if (_cardShellRect != null) _cardShellRect.localScale = Vector3.one * 0.94f;

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, true);
            }

            GameFeelAudio.PlayUiClick();
            RefreshUiContent(initialMessage);
        }

        public void Close(bool withWave = false)
        {
            if (!IsOpen) return;

            if (withWave)
            {
                bool isEn = IsEnglish;
                string bye = isEn ? "See you around, Deniz!" : "Görüşürüz Deniz, kendine iyi bak!";
                if (_activeCitizen != null)
                {
                    _activeCitizen.TriggerFriendlyEmote(celebrate: false);
                    NpcSpeechBubbleManager.Say(_activeCitizen.transform, _activeName, bye, 2.8f, NpcBubbleMood.Friendly);
                }
                else if (_activeLivingTransform != null)
                {
                    if (_activeLivingHumanoid != null && !_activeLivingHumanoid.IsSitting)
                    {
                        _activeLivingHumanoid.PlayWave();
                    }
                    NpcSpeechBubbleManager.Say(_activeLivingTransform, _activeName, bye, 2.8f, NpcBubbleMood.Friendly, 2.08f);
                }
                PlayerModeController.Instance?.FootController?.Humanoid?.PlayWave();
            }

            _panelRoot.SetActive(false);
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
            GameFeelAudio.PlayUiClick();
        }

        private void Update()
        {
            // Track spontaneous landmark-visit favors even while panel is closed!
            if (_activeFavor != null && !_activeFavor.IsRelayToNpc && !_activeFavor.VisitedSpot &&
                PlayerModeController.Instance != null)
            {
                _spotCheckTimer -= Time.deltaTime;
                if (_spotCheckTimer <= 0f)
                {
                    _spotCheckTimer = 0.5f;
                    Vector3 d = PlayerModeController.Instance.ActiveWorldPosition - _activeFavor.TargetPosition;
                    d.y = 0f;
                    if (d.sqrMagnitude <= 11f * 11f)
                    {
                        _activeFavor.VisitedSpot = true;
                        GameFeelAudio.PlayCoinChime();
                        string msg = IsEnglish
                            ? $"Checked {_activeFavor.TargetLandmarkEn}! Return to {_activeFavor.RequesterName} for your reward."
                            : $"{_activeFavor.TargetLandmarkTr} kontrol edildi! Ödül için {_activeFavor.RequesterName} yanına dön.";
                        PlayerModeController.Instance.RaiseStatusMessage(msg);
                    }
                }
            }

            if (!IsOpen) return;

            // Smooth WhatsNewOverlay-style entrance fade + gentle scale settle
            if (_openAnimT < 1f)
            {
                _openAnimT = Mathf.MoveTowards(_openAnimT, 1f, Time.unscaledDeltaTime * 5.8f);
                float ease = 1f - Mathf.Pow(1f - _openAnimT, 3f);
                if (_canvasGroup != null) _canvasGroup.alpha = ease;
                if (_cardShellRect != null) _cardShellRect.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, ease);
            }

            if (Time.frameCount == _openedFrame) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { Close(withWave: false); return; }
            if (kb.digit1Key.wasPressedThisFrame) OnChoiceClicked(0);
            else if (kb.digit2Key.wasPressedThisFrame) OnChoiceClicked(1);
            else if (kb.digit3Key.wasPressedThisFrame) OnChoiceClicked(2);
            else if (kb.digit4Key.wasPressedThisFrame) OnChoiceClicked(3);
        }

        private void RefreshUiContent(string bodyMessage)
        {
            bool isEn = IsEnglish;
            _nameText.text = _activeName;

            StreetNameTracker tracker = FindAnyObjectByType<StreetNameTracker>();
            string streetTag = tracker != null && !string.IsNullOrEmpty(tracker.CurrentStreetName)
                ? $"  ·  {tracker.CurrentStreetName}"
                : string.Empty;
            _roleBadgeText.text = $"{_activeRole}{streetTag}";

            int tier = GetCurrentFriendshipTier();
            bool hasGrudge = _activeCitizen != null && _activeCitizen.HasGrudge;
            _friendshipBadgeText.text = FormatFriendshipBadge(tier, hasGrudge, isEn);

            Color moodAccent = hasGrudge ? OurWorldUiKit.AccentCoral : (tier >= 2 ? OurWorldUiKit.AccentGold : OurWorldUiKit.AccentTeal);
            if (_topGlow != null) _topGlow.color = new Color(moodAccent.r, moodAccent.g, moodAccent.b, 0.16f);
            if (_cardBorder != null) _cardBorder.color = Color.Lerp(OurWorldUiKit.BorderSubtle, moodAccent, 0.24f);
            if (_medallionBg != null) _medallionBg.color = new Color(moodAccent.r, moodAccent.g, moodAccent.b, 0.16f);
            if (_medallionRing != null) _medallionRing.color = new Color(moodAccent.r, moodAccent.g, moodAccent.b, 0.52f);
            if (_medallionIcon != null) _medallionIcon.color = moodAccent;
            if (_friendshipDot != null) _friendshipDot.color = moodAccent;
            if (_friendshipPillBg != null) _friendshipPillBg.color = new Color(moodAccent.r, moodAccent.g, moodAccent.b, 0.13f);
            if (_friendshipPillBorder != null) _friendshipPillBorder.color = new Color(moodAccent.r, moodAccent.g, moodAccent.b, 0.36f);
            if (_friendshipBadgeText != null) _friendshipBadgeText.color = hasGrudge ? OurWorldUiKit.AccentCoral : OurWorldUiKit.AccentGold;

            _bodyText.text = bodyMessage;

            // Active favor banner
            if (_activeFavor != null)
            {
                _favorBannerGo.SetActive(true);
                if (_activeFavor.IsRelayToNpc)
                {
                    _favorBannerText.text = isEn
                        ? $"Active Favor: Deliver {_activeFavor.RequesterName}'s greeting to {_activeFavor.TargetNpcName} (+{_activeFavor.RewardMoney} ₺)"
                        : $"Aktif Komşu İyiliği: {_activeFavor.RequesterName} selamını {_activeFavor.TargetNpcName} kişisine ilet (+{_activeFavor.RewardMoney} ₺)";
                }
                else if (_activeFavor.VisitedSpot)
                {
                    _favorBannerText.text = isEn
                        ? $"Favor Ready: Tell {_activeFavor.RequesterName} you checked {_activeFavor.TargetLandmarkEn} (+{_activeFavor.RewardMoney} ₺)"
                        : $"İyilik Hazır: {_activeFavor.RequesterName} kişisine haber ver (+{_activeFavor.RewardMoney} ₺)";
                }
                else
                {
                    _favorBannerText.text = isEn
                        ? $"Active Favor: Visit {_activeFavor.TargetLandmarkEn} for {_activeFavor.RequesterName} (+{_activeFavor.RewardMoney} ₺)"
                        : $"Aktif Komşu İyiliği: {_activeFavor.RequesterName} için {_activeFavor.TargetLandmarkTr} noktasına uğra (+{_activeFavor.RewardMoney} ₺)";
                }
            }
            else
            {
                _favorBannerGo.SetActive(false);
            }

            // Button 1: Chat / Local News
            StyleChoiceSlot(0,
                isEn ? "1. Chat & Local News" : "1. Sohbet Et · Mahalle Haberi",
                OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextPrimary, OurWorldUiKit.AccentTeal);

            // Button 2: Smart Directions & Pin on Map
            StyleChoiceSlot(1,
                isEn ? "2. Directions & Pin on Map" : "2. Yol Tarifi · Haritada İşaretle",
                OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextPrimary, OurWorldUiKit.AccentSky);

            // Button 3: Dynamic Smart Action (flagship highlighted CTA pill!)
            _currentDynamicMode = ResolveDynamicActionMode();
            switch (_currentDynamicMode)
            {
                case DynamicActionMode.ApologizeGrudge:
                    StyleChoiceSlot(2,
                        isEn ? "3. Apologize & Make Peace" : "3. Özür Dile · Barış",
                        OurWorldUiKit.AccentCoral, Color.clear, OurWorldUiKit.InkDark, OurWorldUiKit.InkDark);
                    break;
                case DynamicActionMode.DeliverFavorHere:
                    StyleChoiceSlot(2,
                        isEn ? $"3. Deliver Greeting (+{_activeFavor.RewardMoney} ₺)" : $"3. Selamı İlet (+{_activeFavor.RewardMoney} ₺)",
                        OurWorldUiKit.AccentGold, Color.clear, OurWorldUiKit.InkDark, OurWorldUiKit.InkDark);
                    break;
                case DynamicActionMode.ClaimVisitedFavor:
                    StyleChoiceSlot(2,
                        isEn ? $"3. Report Back (+{_activeFavor.RewardMoney} ₺)" : $"3. Haberi Ver (+{_activeFavor.RewardMoney} ₺)",
                        OurWorldUiKit.AccentGold, Color.clear, OurWorldUiKit.InkDark, OurWorldUiKit.InkDark);
                    break;
                case DynamicActionMode.AcceptTeaTreat:
                    StyleChoiceSlot(2,
                        isEn ? "3. Accept Warm Tea (+18 Energy)" : "3. Sıcak Çay İkramı (+18 Enerji)",
                        OurWorldUiKit.AccentTeal, Color.clear, OurWorldUiKit.InkDark, OurWorldUiKit.InkDark);
                    break;
                case DynamicActionMode.StopCompanionWalk:
                    StyleChoiceSlot(2,
                        isEn ? "3. End Walk Together" : "3. Birlikte Yürüyüşü Bitir",
                        new Color(0.38f, 0.16f, 0.20f, 0.96f), new Color(OurWorldUiKit.AccentCoral.r, OurWorldUiKit.AccentCoral.g, OurWorldUiKit.AccentCoral.b, 0.45f), OurWorldUiKit.TextPrimary, OurWorldUiKit.AccentCoral);
                    break;
                case DynamicActionMode.StartCompanionWalk:
                    StyleChoiceSlot(2,
                        isEn ? "3. Walk Together (Companion)" : "3. Beraber Yürüyelim (Eşlik)",
                        OurWorldUiKit.AccentTeal, Color.clear, OurWorldUiKit.InkDark, OurWorldUiKit.InkDark);
                    break;
                default:
                    StyleChoiceSlot(2,
                        isEn ? "3. Offer Neighbor Favor (+50 ₺)" : "3. Komşu İyiliği İste (+50 ₺)",
                        OurWorldUiKit.SurfaceActive, new Color(OurWorldUiKit.AccentTeal.r, OurWorldUiKit.AccentTeal.g, OurWorldUiKit.AccentTeal.b, 0.48f), OurWorldUiKit.TextPrimary, OurWorldUiKit.AccentGold);
                    break;
            }

            // Button 4: Wave & Close
            StyleChoiceSlot(3,
                isEn ? "4. Wave & Goodbye" : "4. Selamlaş & Görüşürüz",
                new Color(0.10f, 0.13f, 0.19f, 0.92f), OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextSecondary, OurWorldUiKit.TextMuted);
        }

        private void StyleChoiceSlot(int index, string text, Color bg, Color border, Color labelCol, Color dotCol)
        {
            _choiceLabels[index].text = text;
            _choiceLabels[index].color = labelCol;
            _choiceBgs[index].color = bg;
            if (_choiceBorders[index] != null) _choiceBorders[index].color = border;
            if (_choiceDots[index] != null) _choiceDots[index].color = dotCol;
            UiButtonFeedback fb = _choiceButtons[index].GetComponent<UiButtonFeedback>();
            if (fb != null) fb.Configure(_choiceButtons[index], _choiceBgs[index]);
        }

        private DynamicActionMode ResolveDynamicActionMode()
        {
            if (_activeCitizen != null && _activeCitizen.HasGrudge)
            {
                return DynamicActionMode.ApologizeGrudge;
            }

            if (_activeFavor != null)
            {
                if (_activeFavor.IsRelayToNpc &&
                    string.Equals(_activeFavor.TargetNpcName, _activeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DynamicActionMode.DeliverFavorHere;
                }
                if (!_activeFavor.IsRelayToNpc && _activeFavor.VisitedSpot &&
                    string.Equals(_activeFavor.RequesterName, _activeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DynamicActionMode.ClaimVisitedFavor;
                }
            }

            if (_activeCitizen != null && _activeCitizen.IsFollowingPlayer)
            {
                return DynamicActionMode.StopCompanionWalk;
            }

            if (PlayerNeedsController.Instance != null &&
                PlayerNeedsController.Instance.Energy < 75f &&
                !TreatedThisSession.Contains(_activeName))
            {
                return DynamicActionMode.AcceptTeaTreat;
            }

            if (_activeCitizen != null && _activeCitizen.CanWalkWithPlayer && _activeCitizen.FriendshipTier >= 1)
            {
                return DynamicActionMode.StartCompanionWalk;
            }

            return DynamicActionMode.StartFavor;
        }

        private void OnChoiceClicked(int idx)
        {
            if (!IsOpen) return;
            GameFeelAudio.PlayUiClick();
            bool isEn = IsEnglish;

            if (idx == 0)
            {
                HandleChatChoice(isEn);
            }
            else if (idx == 1)
            {
                HandleDirectionsChoice(isEn);
            }
            else if (idx == 2)
            {
                HandleDynamicActionChoice(isEn);
            }
            else
            {
                Close(withWave: true);
            }
        }

        private void HandleChatChoice(bool isEn)
        {
            if (_activeCitizen != null && _activeCitizen.HasGrudge)
            {
                RefreshUiContent(isEn
                    ? "I'm still upset with you. Apologize first if you want to chat!"
                    : "Sana hâlâ kırgınım. Sohbet etmek istiyorsan önce özür dile!");
                return;
            }

            AddCurrentNpcFriendship(15);
            string reply;
            if (_activeCitizen != null)
            {
                _activeCitizen.TriggerFriendlyEmote(celebrate: false);
                reply = _activeCitizen.GetNextPersonalChatLine(isEn);
                NpcSpeechBubbleManager.Say(_activeCitizen.transform, _activeName, reply, 3.6f, NpcBubbleMood.Friendly);
            }
            else
            {
                if (_activeLivingHumanoid != null) _activeLivingHumanoid.SetTalking(true, 3.2f);
                reply = GetNextLivingChatLine(isEn);
                if (_activeLivingTransform != null)
                {
                    NpcSpeechBubbleManager.Say(_activeLivingTransform, _activeName, reply, 3.6f, NpcBubbleMood.Friendly, 2.08f);
                }
            }

            RefreshUiContent(reply);
        }

        private void HandleDirectionsChoice(bool isEn)
        {
            Vector3 playerPos = PlayerModeController.Instance != null
                ? PlayerModeController.Instance.ActiveWorldPosition
                : Vector3.zero;

            // Pick a landmark that isn't right under the player's feet, cycling cleanly on repeated taps
            LandmarkGuide chosen = Landmarks[_directionCursor % Landmarks.Length];
            for (int tries = 0; tries < Landmarks.Length; tries++)
            {
                LandmarkGuide cand = Landmarks[(_directionCursor + tries) % Landmarks.Length];
                if ((cand.Position - playerPos).sqrMagnitude > 22f * 22f)
                {
                    chosen = cand;
                    _directionCursor = (_directionCursor + tries + 1) % Landmarks.Length;
                    break;
                }
            }

            string label = isEn ? chosen.NameEn : chosen.NameTr;
            string hint = isEn ? chosen.HintEn : chosen.HintTr;

            PhonePanelController.Instance?.SetCustomWaypoint(chosen.Position, label);
            AddCurrentNpcFriendship(8);
            if (_activeCitizen != null) _activeCitizen.TriggerFriendlyEmote(celebrate: false);
            else if (_activeLivingHumanoid != null && !_activeLivingHumanoid.IsSitting) _activeLivingHumanoid.PlayWave();

            RefreshUiContent(hint);
        }

        private void HandleDynamicActionChoice(bool isEn)
        {
            switch (_currentDynamicMode)
            {
                case DynamicActionMode.ApologizeGrudge:
                {
                    _activeCitizen?.ClearGrudgeAndMakePeace();
                    GameFeelAudio.SpawnBurst(
                        (_activeCitizen != null ? _activeCitizen.transform.position : Vector3.zero) + Vector3.up * 1.6f,
                        new Color(0.32f, 0.92f, 0.78f), 10, 0.45f);
                    RefreshUiContent(isEn
                        ? "Alright, Deniz... apology accepted. Let's stay good neighbors!"
                        : "Tamam Deniz, gönlümü aldın. Komşuluk hakkı var, barıştık gitti!");
                    break;
                }

                case DynamicActionMode.DeliverFavorHere:
                {
                    int reward = _activeFavor != null ? _activeFavor.RewardMoney : 55;
                    string from = _activeFavor != null ? _activeFavor.RequesterName : (isEn ? "our neighbor" : "komşumuz");
                    _activeFavor = null;
                    PhonePanelController.Instance?.ClearCustomWaypoint();
                    SaveManager.Instance?.AddMoney(reward);
                    NeighborhoodReputation.AddReputation(15, isEn ? $"Neighbor Favor (+{reward} ₺)" : $"Komşu Selamı (+{reward} ₺)");
                    AddCurrentNpcFriendship(25);
                    GameFeelAudio.PlayCoinChime();
                    Vector3 burstPos = _activeCitizen != null
                        ? _activeCitizen.transform.position
                        : (_activeLivingTransform != null ? _activeLivingTransform.position : Vector3.zero);
                    GameFeelAudio.SpawnBurst(burstPos + Vector3.up * 1.6f, new Color(0.98f, 0.82f, 0.24f), 14, 0.55f);
                    _activeCitizen?.TriggerFriendlyEmote(celebrate: true);

                    RefreshUiContent(isEn
                        ? $"Oh, greetings from {from}! That made my day — here's a small thank-you! (+{reward} ₺, +15 Rep)"
                        : $"Vay, {from} benden selam mı yollamış! Çok mutlu oldum Deniz, al şu gönül hediyesini! (+{reward} ₺, +15 İtibar)");
                    break;
                }

                case DynamicActionMode.ClaimVisitedFavor:
                {
                    int reward = _activeFavor != null ? _activeFavor.RewardMoney : 50;
                    string spot = _activeFavor != null
                        ? (isEn ? _activeFavor.TargetLandmarkEn : _activeFavor.TargetLandmarkTr)
                        : string.Empty;
                    _activeFavor = null;
                    PhonePanelController.Instance?.ClearCustomWaypoint();
                    SaveManager.Instance?.AddMoney(reward);
                    NeighborhoodReputation.AddReputation(12, isEn ? $"Neighbor Errand (+{reward} ₺)" : $"Komşu İyiliği (+{reward} ₺)");
                    AddCurrentNpcFriendship(25);
                    GameFeelAudio.PlayCoinChime();
                    _activeCitizen?.TriggerFriendlyEmote(celebrate: true);

                    RefreshUiContent(isEn
                        ? $"Thanks for checking {spot} for me, Deniz! You're the best neighbor in Hamidiye. (+{reward} ₺, +12 Rep)"
                        : $"{spot} için uğrayıp haber getirdiğin için sağ ol Deniz! Hamidiye'nin en iyi komşususun. (+{reward} ₺, +12 İtibar)");
                    break;
                }

                case DynamicActionMode.AcceptTeaTreat:
                {
                    TreatedThisSession.Add(_activeName);
                    PlayerNeedsController.Instance?.RestoreEnergy(18f);
                    AddCurrentNpcFriendship(15);
                    GameFeelAudio.PlayCoinChime();
                    _activeCitizen?.TriggerFriendlyEmote(celebrate: false);
                    RefreshUiContent(isEn
                        ? "Here's a steaming cup of Turkish tea on the house! Feel refreshed, Deniz. (+18 Energy)"
                        : "Buyur komşu, taze demlenmiş ince belli çay benden! Enerjin yerine gelsin. (+18 Enerji)");
                    break;
                }

                case DynamicActionMode.StartCompanionWalk:
                {
                    if (_activeCitizen != null)
                    {
                        _activeCitizen.StartFollowingPlayer(45f);
                        PlayerModeController.Instance?.RaiseStatusMessage(isEn
                            ? $"{_activeName} is walking with you through Hamidiye!"
                            : $"{_activeName} mahalle yürüyüşünde sana eşlik ediyor!");
                        Close(withWave: false);
                    }
                    break;
                }

                case DynamicActionMode.StopCompanionWalk:
                {
                    if (_activeCitizen != null)
                    {
                        _activeCitizen.StopFollowingPlayer(sayGoodbye: false);
                        RefreshUiContent(isEn
                            ? "That was a lovely walk! Call me anytime you want company."
                            : "Çok güzel yürüdük Deniz! Ne zaman istersen yine birlikte dolaşırız.");
                    }
                    break;
                }

                default:
                {
                    StartSpontaneousFavor(isEn);
                    break;
                }
            }
        }

        private void StartSpontaneousFavor(bool isEn)
        {
            // Try to pick a real TownPedestrian on a different part of the map for a Greeting Relay!
            TownPedestrian relayTarget = null;
            Vector3 myPos = _activeCitizen != null
                ? _activeCitizen.transform.position
                : (_activeLivingTransform != null ? _activeLivingTransform.position : Vector3.zero);

            List<TownPedestrian> candidates = new List<TownPedestrian>();
            for (int i = 0; i < TownPedestrian.ActiveCitizens.Count; i++)
            {
                TownPedestrian cand = TownPedestrian.ActiveCitizens[i];
                if (cand == null || cand == _activeCitizen || !cand.gameObject.activeInHierarchy) continue;
                if (string.Equals(cand.CitizenName, _activeName, StringComparison.OrdinalIgnoreCase)) continue;
                float dSq = (cand.transform.position - myPos).sqrMagnitude;
                if (dSq >= 25f * 25f && dSq <= 260f * 260f)
                {
                    candidates.Add(cand);
                }
            }

            if (candidates.Count > 0 && UnityEngine.Random.value < 0.65f)
            {
                relayTarget = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                _activeFavor = new NeighborFavorState
                {
                    RequesterName = _activeName,
                    TargetNpcName = relayTarget.CitizenName,
                    TargetPosition = relayTarget.transform.position,
                    IsRelayToNpc = true,
                    VisitedSpot = false,
                    RewardMoney = 55
                };
                PhonePanelController.Instance?.SetCustomWaypoint(relayTarget.transform.position, relayTarget.CitizenName);
                AddCurrentNpcFriendship(10);
                RefreshUiContent(isEn
                    ? $"Could you say hello to {relayTarget.CitizenName} ({relayTarget.CitizenRole}) for me? I marked them on your compass! (+55 ₺)"
                    : $"{relayTarget.CitizenName} ({relayTarget.CitizenRole}) yanına uğrayıp benden selam iletir misin? Pusulana işaretledim! (+55 ₺)");
                return;
            }

            // Otherwise, assign a fun neighborhood spot-check errand!
            LandmarkGuide spot = Landmarks[UnityEngine.Random.Range(0, Landmarks.Length)];
            for (int tries = 0; tries < 5; tries++)
            {
                LandmarkGuide cand = Landmarks[UnityEngine.Random.Range(0, Landmarks.Length)];
                if ((cand.Position - myPos).sqrMagnitude > 30f * 30f)
                {
                    spot = cand;
                    break;
                }
            }

            _activeFavor = new NeighborFavorState
            {
                RequesterName = _activeName,
                TargetLandmarkTr = spot.NameTr,
                TargetLandmarkEn = spot.NameEn,
                TargetPosition = spot.Position,
                IsRelayToNpc = false,
                VisitedSpot = false,
                RewardMoney = 50
            };
            PhonePanelController.Instance?.SetCustomWaypoint(spot.Position, isEn ? spot.NameEn : spot.NameTr);
            AddCurrentNpcFriendship(10);
            RefreshUiContent(isEn
                ? $"Would you swing by {spot.NameEn} and see how lively it is, then let me know? Marked on your map! (+50 ₺)"
                : $"{spot.NameTr} tarafına bir uğrayıp durum nasıl bakıverir misin, sonra bana haber ver! Haritana işaretledim. (+50 ₺)");
        }

        private string BuildLivingNpcSmartGreeting(bool isEn)
        {
            if (NeighborhoodWeatherSystem.Instance != null &&
                NeighborhoodWeatherSystem.Instance.CurrentWeather == NeighborhoodWeatherState.SummerShower)
            {
                return isEn
                    ? "What a sudden Istanbul shower! Good thing Hamidiye's tea houses are always warm."
                    : "Ne güzel İstanbul yaz yağmuru bastırdı! Çay ocakları şimdi sıcacıktır.";
            }

            float h = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentHour : 12f;
            int tier = GetCurrentFriendshipTier();
            if (tier >= 2)
            {
                return isEn
                    ? $"Hey my good friend Deniz! Always brightens my day seeing you in Hamidiye."
                    : $"Vay canım komşum Deniz! Seni mahallede görünce günüm güzelleşiyor.";
            }

            if (isEn)
            {
                return h < 11.5f
                    ? "Good morning Deniz! Have you tried the fresh pastry on Havadis Street yet?"
                    : h < 17.5f
                        ? "Hello neighbor! Hamidiye feels bigger and livelier every single day."
                        : "Lovely evening in Sultanbeyli! The street lamps on Kadife and Havadis Streets look cozy.";
            }

            return h < 11.5f
                ? "Günaydın Deniz! Yeni açılan Havadis Sokağı'ndaki taş fırına uğradın mı?"
                : h < 17.5f
                    ? "Selam komşu! Hem Havadis Sokağı hem Kadife Sokağı eklenince mahallemiz iyice şenlendi."
                    : "İyi akşamlar Deniz! Sokak lambaları yanınca Kadife Sokağı ve Selman Sokağı harika görünüyor.";
        }

        private string GetNextLivingChatLine(bool isEn)
        {
            if (!isEn && _activeLivingLines != null && _activeLivingLines.Length > 0)
            {
                string line = _activeLivingLines[_livingLineCursor % _activeLivingLines.Length];
                _livingLineCursor++;
                return line;
            }

            string[] enPool =
            {
                "Havadis Street to the north has an amazing stone bakery and cozy enterable homes!",
                "Kadife Street down south has a classic Turkish tea house and a flower stand.",
                "Every home's entrance hallway on Selman Street was tidied up — so much more spacious now!",
                "If you need directions anywhere in Hamidiye, just ask and I'll pin it on your compass."
            };
            string enLine = enPool[_livingLineCursor % enPool.Length];
            _livingLineCursor++;
            return enLine;
        }

        private int GetCurrentFriendshipTier()
        {
            if (_activeCitizen != null) return _activeCitizen.FriendshipTier;
            int pts = SessionLivingRapport.TryGetValue(_activeName, out int v) ? v : 0;
            return pts >= 60 ? 3 : (pts >= 30 ? 2 : (pts >= 10 ? 1 : 0));
        }

        private void AddCurrentNpcFriendship(int delta)
        {
            if (_activeCitizen != null)
            {
                _activeCitizen.AddFriendship(delta);
                return;
            }
            int cur = SessionLivingRapport.TryGetValue(_activeName, out int v) ? v : 0;
            SessionLivingRapport[_activeName] = Mathf.Clamp(cur + delta, 0, 100);
        }

        private static string FormatFriendshipBadge(int tier, bool hasGrudge, bool isEn)
        {
            if (hasGrudge) return isEn ? "Mood: Upset" : "Ruh Hali: Kırgın";
            switch (tier)
            {
                case 3: return isEn ? "★ Close Friend" : "★ Yakın Dost";
                case 2: return isEn ? "● Good Neighbor" : "● İyi Komşu";
                case 1: return isEn ? "◐ Familiar" : "◐ Tanıdık";
                default: return isEn ? "○ Neighbor" : "○ Komşu";
            }
        }

        private void BuildUi()
        {
            OurWorldUiKit.EnsureInitialized();

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 145;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            Font uiFont = OurWorldUiKit.DefaultFont;

            _panelRoot = new GameObject("NpcInteractionRoot", typeof(RectTransform), typeof(CanvasGroup));
            _panelRoot.transform.SetParent(transform, false);
            _canvasGroup = _panelRoot.GetComponent<CanvasGroup>();
            RectTransform rootRt = (RectTransform)_panelRoot.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Subtle translucent scrim at bottom so the player can still see the NPC talking!
            GameObject scrim = new GameObject("BottomScrim", typeof(RectTransform), typeof(Image));
            scrim.transform.SetParent(_panelRoot.transform, false);
            RectTransform srt = (RectTransform)scrim.transform;
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 0.56f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            Image scrimImg = scrim.GetComponent<Image>();
            scrimImg.sprite = OurWorldUiKit.TopGlowSprite;
            scrimImg.color = new Color(0.02f, 0.035f, 0.065f, 0.62f);
            srt.localRotation = Quaternion.Euler(0f, 0f, 180f);

            // Flagship WhatsNew-style elevated rounded card centered near bottom
            GameObject card = new GameObject("DialogueCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(_panelRoot.transform, false);
            _cardShellRect = (RectTransform)card.transform;
            _cardShellRect.anchorMin = new Vector2(0.5f, 0f);
            _cardShellRect.anchorMax = new Vector2(0.5f, 0f);
            _cardShellRect.pivot = new Vector2(0.5f, 0f);
            _cardShellRect.anchoredPosition = new Vector2(0f, 34f);
            _cardShellRect.sizeDelta = new Vector2(1090f, 434f);

            OurWorldUiKit.ApplyRounded(card.GetComponent<Image>(), OurWorldUiKit.CardSprite, OurWorldUiKit.CardBg);
            OurWorldUiKit.AddCardDropShadow(card.transform, spread: 28f, offsetY: -12f, alpha: 0.60f);
            _topGlow = OurWorldUiKit.AddSoftTopGlow(card.transform, OurWorldUiKit.AccentTeal, heightPx: 165f, alpha: 0.16f);
            _cardBorder = OurWorldUiKit.AddHairlineBorder(card.transform, OurWorldUiKit.CardOutlineSprite, OurWorldUiKit.BorderSubtle);

            // Header Left: Circular Icon Medallion + Name + Role/Street
            GameObject medGo = new GameObject("NpcMedallion", typeof(RectTransform), typeof(Image));
            medGo.transform.SetParent(card.transform, false);
            RectTransform medRt = (RectTransform)medGo.transform;
            medRt.anchorMin = medRt.anchorMax = new Vector2(0f, 1f);
            medRt.pivot = new Vector2(0f, 1f);
            medRt.anchoredPosition = new Vector2(30f, -20f);
            medRt.sizeDelta = new Vector2(62f, 62f);
            _medallionBg = medGo.GetComponent<Image>();
            _medallionBg.sprite = OurWorldUiKit.CircleSprite;
            _medallionBg.color = new Color(OurWorldUiKit.AccentTeal.r, OurWorldUiKit.AccentTeal.g, OurWorldUiKit.AccentTeal.b, 0.16f);
            _medallionBg.raycastTarget = false;
            _medallionRing = OurWorldUiKit.AddHairlineBorder(medGo.transform, OurWorldUiKit.CircleOutlineSprite,
                new Color(OurWorldUiKit.AccentTeal.r, OurWorldUiKit.AccentTeal.g, OurWorldUiKit.AccentTeal.b, 0.52f));
            _medallionIcon = OurWorldUiKit.AddIcon("MedallionIcon", medGo.transform, OurWorldUiKit.IconCommunity, 30f, OurWorldUiKit.AccentTeal);

            _nameText = CreateLabel("NpcName", card.transform, uiFont, 30, FontStyle.Bold, OurWorldUiKit.TextPrimary, TextAnchor.MiddleLeft);
            SetRect(_nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0.65f, 1f), new Vector2(106f, -18f), new Vector2(0f, 38f));

            _roleBadgeText = CreateLabel("NpcRole", card.transform, uiFont, 18, FontStyle.Bold, OurWorldUiKit.TextSecondary, TextAnchor.MiddleLeft);
            SetRect(_roleBadgeText.rectTransform, new Vector2(0f, 1f), new Vector2(0.68f, 1f), new Vector2(106f, -54f), new Vector2(0f, 26f));

            // Header Right: Capsule Friendship Pill with live dot + Crisp Circular Close Button
            GameObject badgeBg = new GameObject("FriendshipPill", typeof(RectTransform), typeof(Image));
            badgeBg.transform.SetParent(card.transform, false);
            RectTransform brt = (RectTransform)badgeBg.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-88f, -28f);
            brt.sizeDelta = new Vector2(226f, 40f);
            _friendshipPillBg = OurWorldUiKit.ApplyRounded(badgeBg.GetComponent<Image>(), OurWorldUiKit.PillSprite,
                new Color(OurWorldUiKit.AccentGold.r, OurWorldUiKit.AccentGold.g, OurWorldUiKit.AccentGold.b, 0.13f));
            _friendshipPillBorder = OurWorldUiKit.AddHairlineBorder(badgeBg.transform, OurWorldUiKit.PillOutlineSprite,
                new Color(OurWorldUiKit.AccentGold.r, OurWorldUiKit.AccentGold.g, OurWorldUiKit.AccentGold.b, 0.36f));

            GameObject dotGo = new GameObject("LiveDot", typeof(RectTransform), typeof(Image));
            dotGo.transform.SetParent(badgeBg.transform, false);
            RectTransform drt = (RectTransform)dotGo.transform;
            drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = new Vector2(20f, 0f);
            drt.sizeDelta = new Vector2(9f, 9f);
            _friendshipDot = dotGo.GetComponent<Image>();
            _friendshipDot.sprite = OurWorldUiKit.CircleSprite;
            _friendshipDot.color = OurWorldUiKit.AccentGold;
            _friendshipDot.raycastTarget = false;

            _friendshipBadgeText = CreateLabel("FriendshipLabel", badgeBg.transform, uiFont, 17, FontStyle.Bold, OurWorldUiKit.AccentGold, TextAnchor.MiddleCenter);
            _friendshipBadgeText.rectTransform.anchorMin = Vector2.zero;
            _friendshipBadgeText.rectTransform.anchorMax = Vector2.one;
            _friendshipBadgeText.rectTransform.offsetMin = new Vector2(28f, 0f);
            _friendshipBadgeText.rectTransform.offsetMax = new Vector2(-12f, 0f);

            Button closeBtn = OurWorldUiKit.CreateCircularIconButton(
                "Btn_Close", card.transform, OurWorldUiKit.IconClose,
                diameter: 44f, iconSize: 17f,
                bgColor: new Color(0.14f, 0.19f, 0.27f, 0.92f),
                iconColor: OurWorldUiKit.TextSecondary,
                borderColor: OurWorldUiKit.BorderSubtle,
                onClick: () => Close(withWave: false));
            RectTransform xrt = (RectTransform)closeBtn.transform;
            xrt.anchorMin = xrt.anchorMax = new Vector2(1f, 1f);
            xrt.pivot = new Vector2(1f, 1f);
            xrt.anchoredPosition = new Vector2(-28f, -26f);

            // Header divider line
            OurWorldUiKit.AddDividerLine(card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(30f, -94f), new Vector2(-30f, -92.5f),
                OurWorldUiKit.Divider);

            // Active Favor Pill Banner
            _favorBannerGo = new GameObject("FavorBanner", typeof(RectTransform), typeof(Image));
            _favorBannerGo.transform.SetParent(card.transform, false);
            RectTransform fbrt = (RectTransform)_favorBannerGo.transform;
            fbrt.anchorMin = new Vector2(0f, 1f);
            fbrt.anchorMax = new Vector2(1f, 1f);
            fbrt.pivot = new Vector2(0.5f, 1f);
            fbrt.anchoredPosition = new Vector2(0f, -104f);
            fbrt.sizeDelta = new Vector2(-60f, 36f);
            OurWorldUiKit.ApplyRounded(_favorBannerGo.GetComponent<Image>(), OurWorldUiKit.PillSprite,
                new Color(OurWorldUiKit.AccentGold.r, OurWorldUiKit.AccentGold.g, OurWorldUiKit.AccentGold.b, 0.14f));
            OurWorldUiKit.AddHairlineBorder(_favorBannerGo.transform, OurWorldUiKit.PillOutlineSprite,
                new Color(OurWorldUiKit.AccentGold.r, OurWorldUiKit.AccentGold.g, OurWorldUiKit.AccentGold.b, 0.36f));
            OurWorldUiKit.AddIcon("FavorStar", _favorBannerGo.transform, OurWorldUiKit.IconStar, 18f, OurWorldUiKit.AccentGold, new Vector2(-490f, 0f));
            _favorBannerText = CreateLabel("FavorText", _favorBannerGo.transform, uiFont, 17, FontStyle.Bold, OurWorldUiKit.AccentGold, TextAnchor.MiddleLeft);
            _favorBannerText.rectTransform.anchorMin = Vector2.zero;
            _favorBannerText.rectTransform.anchorMax = Vector2.one;
            _favorBannerText.rectTransform.offsetMin = new Vector2(44f, 0f);
            _favorBannerText.rectTransform.offsetMax = new Vector2(-18f, 0f);
            _favorBannerGo.SetActive(false);

            // Spoken dialogue body
            _bodyText = CreateLabel("DialogueBody", card.transform, uiFont, 23, FontStyle.Normal, OurWorldUiKit.TextPrimary, TextAnchor.UpperLeft);
            RectTransform bodyRt = _bodyText.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = new Vector2(0f, -148f);
            bodyRt.sizeDelta = new Vector2(-64f, 92f);
            _bodyText.lineSpacing = 1.26f;
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bodyText.verticalOverflow = VerticalWrapMode.Truncate;

            // Subtle divider above choice buttons
            OurWorldUiKit.AddDividerLine(card.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(30f, 172f), new Vector2(-30f, 173.5f),
                OurWorldUiKit.Divider);

            // 2x2 Rounded Flagship Choice Grid at bottom of card
            GameObject gridGo = new GameObject("ChoiceGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(card.transform, false);
            RectTransform grt = (RectTransform)gridGo.transform;
            grt.anchorMin = new Vector2(0f, 0f);
            grt.anchorMax = new Vector2(1f, 0f);
            grt.pivot = new Vector2(0.5f, 0f);
            grt.anchoredPosition = new Vector2(0f, 24f);
            grt.sizeDelta = new Vector2(-60f, 136f);

            GridLayoutGroup glg = gridGo.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(506f, 60f);
            glg.spacing = new Vector2(18f, 14f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            for (int i = 0; i < 4; i++)
            {
                int choiceIndex = i;
                GameObject btnGo = new GameObject($"ChoiceBtn_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(gridGo.transform, false);
                Image img = OurWorldUiKit.ApplyRounded(btnGo.GetComponent<Image>(), OurWorldUiKit.PillSprite, OurWorldUiKit.SurfaceBg);
                Image border = OurWorldUiKit.AddHairlineBorder(btnGo.transform, OurWorldUiKit.PillOutlineSprite, OurWorldUiKit.BorderSubtle);

                GameObject cDotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                cDotGo.transform.SetParent(btnGo.transform, false);
                RectTransform cdrt = (RectTransform)cDotGo.transform;
                cdrt.anchorMin = cdrt.anchorMax = new Vector2(0f, 0.5f);
                cdrt.pivot = new Vector2(0.5f, 0.5f);
                cdrt.anchoredPosition = new Vector2(24f, 0f);
                cdrt.sizeDelta = new Vector2(9f, 9f);
                Image cDot = cDotGo.GetComponent<Image>();
                cDot.sprite = OurWorldUiKit.CircleSprite;
                cDot.color = OurWorldUiKit.AccentTeal;
                cDot.raycastTarget = false;

                Button btn = btnGo.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => OnChoiceClicked(choiceIndex));

                UiButtonFeedback fb = btnGo.AddComponent<UiButtonFeedback>();
                fb.Configure(btn, img);

                Text lbl = CreateLabel("Label", btnGo.transform, uiFont, 20, FontStyle.Bold, OurWorldUiKit.TextPrimary, TextAnchor.MiddleLeft);
                lbl.rectTransform.anchorMin = Vector2.zero;
                lbl.rectTransform.anchorMax = Vector2.one;
                lbl.rectTransform.offsetMin = new Vector2(42f, 4f);
                lbl.rectTransform.offsetMax = new Vector2(-18f, -4f);

                _choiceButtons[i] = btn;
                _choiceLabels[i] = lbl;
                _choiceBgs[i] = img;
                _choiceBorders[i] = border;
                _choiceDots[i] = cDot;
            }
        }

        private static Text CreateLabel(string name, Transform parent, Font font, int size, FontStyle style, Color color, TextAnchor align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
