using System.Collections;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Jobs;
using OurWorld.Missions;
using OurWorld.Player;
using OurWorld.Vehicle;
using OurWorld.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Full-screen minimal Phone / Town Guide overlay (HUD button or M key):
    /// - MAP tab: TownMap.png with live Player arrow, Car icons, Job icons, Gas Station icon, and tap-to-pin Waypoint
    /// - JOBS tab: 4 open-world Side Jobs with Turkish titles/locations (tap -> sets waypoint) + Cancel Active Job
    /// - STATS tab: Money, Energy, Pizza Deliveries, Taxi Fares, Houses Painted, Litter Collected, Play Time
    /// Locks player movement while open and animates open/close with 0.2 s CanvasGroup fade + scale (unscaled time).
    /// </summary>
    public class PhonePanelController : MonoBehaviour
    {
        public static PhonePanelController Instance { get; private set; }

        public const float ViewWorldMinX = -230f;
        public const float ViewWorldMinZ = -223f;
        public const float ViewWorldSpan = 450f;

        [Header("Root & Tabs")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text headerTitleText;
        [SerializeField] private Button tabMapButton;
        [SerializeField] private Text tabMapLabel;
        [SerializeField] private Button tabJobsButton;
        [SerializeField] private Text tabJobsLabel;
        [SerializeField] private Button tabStatsButton;
        [SerializeField] private Text tabStatsLabel;
        [SerializeField] private Button languageButton;
        [SerializeField] private Text languageLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text closeLabel;

        [Header("Tab Content Roots")]
        [SerializeField] private GameObject mapTabRoot;
        [SerializeField] private GameObject jobsTabRoot;
        [SerializeField] private GameObject statsTabRoot;

        [Header("Map Tab Elements")]
        [SerializeField] private RawImage townMapRawImage;
        [SerializeField] private RectTransform mapViewportRect;
        [SerializeField] private RectTransform mapPlayerArrow;
        [SerializeField] private RectTransform mapWaypointMarker;
        [SerializeField] private RectTransform mapGasStationMarker;
        [SerializeField] private RectTransform[] mapJobMarkers = new RectTransform[4];
        [SerializeField] private RectTransform[] mapCarMarkers = new RectTransform[8];
        [SerializeField] private Button clearWaypointButton;
        [SerializeField] private Text clearWaypointLabel;
        [SerializeField] private Text mapHintLabel;
        [SerializeField] private Text mapLegendText;

        [Header("Jobs Tab Elements")]
        [SerializeField] private Button[] jobSelectButtons = new Button[4];
        [SerializeField] private Text[] jobRowTitleTexts = new Text[4];
        [SerializeField] private Text[] jobRowDescTexts = new Text[4];
        [SerializeField] private Text[] jobRowPinBtnLabels = new Text[4];
        [SerializeField] private Button cancelActiveJobButton;
        [SerializeField] private Text cancelActiveJobLabel;

        [Header("Stats Tab Elements")]
        [SerializeField] private Text[] statNameTexts = new Text[7];
        [SerializeField] private Text[] statValueTexts = new Text[7];

        // Runtime-built "Öneri / Ideas" tab (player feedback -> Firebase)
        private FeedbackTabView _feedbackView;
        private Button _tabFeedbackButton;
        private Text _tabFeedbackLabel;

        // Runtime-built Map Landmark Pins + Fast Travel / Car Valet bar
        private readonly System.Collections.Generic.List<(RectTransform rt, Text lbl, string tr, string en, Vector3 pos)> _mapLandmarkPins
            = new System.Collections.Generic.List<(RectTransform, Text, string, string, Vector3)>();
        private Button _fastTravelBtn;
        private Text _fastTravelLabel;
        private Button _callCarBtn;
        private Text _callCarLabel;
        private Text _statsDailyBtnLabel;
        private Text _statsStyleBtnLabel;

        private static readonly Color ActiveTabColor = OurWorldUiKit.AccentTeal;
        private static readonly Color InactiveTabColor = OurWorldUiKit.SurfaceBg;
        private static readonly Vector3 DefaultGasStationWorldPos = new Vector3(-84.5f, 0.04f, 12.0f);
        private Vector3 _resolvedGasStationPos = DefaultGasStationWorldPos;

        private SedanVehicleController[] _cachedVehicles;
        private int _activeTabIndex;
        private int _openedFrame = -1;
        private Coroutine _animRoutine;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
        public bool HasCustomWaypoint { get; private set; }
        public Vector3 CustomWaypointPosition { get; private set; }

        public void WireReferences(
            GameObject root,
            CanvasGroup group,
            Text headerTitle,
            Button btnMap, Text lblMap,
            Button btnJobs, Text lblJobs,
            Button btnStats, Text lblStats,
            Button btnLang, Text lblLang,
            Button btnClose, Text lblClose,
            GameObject mapRoot, GameObject jobsRoot, GameObject statsRoot,
            RawImage mapRaw, RectTransform viewportRect,
            RectTransform playerIcon, RectTransform waypointIcon, RectTransform gasIcon,
            RectTransform[] jobIcons, RectTransform[] carIcons,
            Button btnClearWp, Text lblClearWp, Text mapHint, Text mapLegend,
            Button[] jobBtns, Text[] jobTitles, Text[] jobDescs, Text[] jobPinLbls,
            Button btnCancelJob, Text lblCancelJob,
            Text[] statNames, Text[] statValues)
        {
            panelRoot = root;
            canvasGroup = group;
            headerTitleText = headerTitle;
            tabMapButton = btnMap; tabMapLabel = lblMap;
            tabJobsButton = btnJobs; tabJobsLabel = lblJobs;
            tabStatsButton = btnStats; tabStatsLabel = lblStats;
            languageButton = btnLang; languageLabel = lblLang;
            closeButton = btnClose; closeLabel = lblClose;
            mapTabRoot = mapRoot; jobsTabRoot = jobsRoot; statsTabRoot = statsRoot;
            townMapRawImage = mapRaw; mapViewportRect = viewportRect;
            mapPlayerArrow = playerIcon; mapWaypointMarker = waypointIcon; mapGasStationMarker = gasIcon;
            mapJobMarkers = jobIcons; mapCarMarkers = carIcons;
            clearWaypointButton = btnClearWp; clearWaypointLabel = lblClearWp;
            mapHintLabel = mapHint; mapLegendText = mapLegend;
            jobSelectButtons = jobBtns; jobRowTitleTexts = jobTitles; jobRowDescTexts = jobDescs; jobRowPinBtnLabels = jobPinLbls;
            cancelActiveJobButton = btnCancelJob; cancelActiveJobLabel = lblCancelJob;
            statNameTexts = statNames; statValueTexts = statValues;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _cachedVehicles = FindObjectsByType<SedanVehicleController>(FindObjectsInactive.Include);

            // Resolve real FuelPump world position from the active Sultanbeyli scene
            InteractableTrigger[] allTriggers = FindObjectsByType<InteractableTrigger>(FindObjectsInactive.Include);
            for (int i = 0; i < allTriggers.Length; i++)
            {
                if (allTriggers[i] != null && allTriggers[i].Kind == InteractableKind.FuelPump)
                {
                    _resolvedGasStationPos = allTriggers[i].transform.position;
                    break;
                }
            }

            if (townMapRawImage != null)
            {
                float u0 = (ViewWorldMinX - MinimapController.MapWorldMinX) / MinimapController.MapWorldSpan;
                float v0 = (ViewWorldMinZ - MinimapController.MapWorldMinZ) / MinimapController.MapWorldSpan;
                float uvSpan = ViewWorldSpan / MinimapController.MapWorldSpan;
                townMapRawImage.uvRect = new Rect(u0, v0, uvSpan, uvSpan);

                // Allow clicking directly on the map to place a waypoint
                EventTrigger trigger = townMapRawImage.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = townMapRawImage.gameObject.AddComponent<EventTrigger>();
                }
                EventTrigger.Entry clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                clickEntry.callback.AddListener(data => HandleMapClicked((PointerEventData)data));
                trigger.triggers.Add(clickEntry);
            }

            if (tabMapButton != null) tabMapButton.onClick.AddListener(() => SelectTab(0));
            if (tabJobsButton != null) tabJobsButton.onClick.AddListener(() => SelectTab(1));
            if (tabStatsButton != null) tabStatsButton.onClick.AddListener(() => SelectTab(2));
            BuildFeedbackTab();
            BuildMapLandmarkPinsAndFastTravel();
            BuildStatsQuickActions();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (languageButton != null)
            {
                languageButton.onClick.AddListener(() =>
                {
                    if (LocalizationService.Instance != null)
                    {
                        LocalizationService.Instance.ToggleLanguage();
                    }
                });
            }
            if (clearWaypointButton != null)
            {
                clearWaypointButton.onClick.AddListener(ClearCustomWaypoint);
            }
            if (cancelActiveJobButton != null)
            {
                cancelActiveJobButton.onClick.AddListener(() =>
                {
                    if (JobManager.Instance != null)
                    {
                        JobManager.Instance.CancelActiveJob(silent: false);
                        RefreshJobsTab();
                    }
                });
            }

            if (jobSelectButtons != null)
            {
                JobType[] types = { JobType.PizzaDelivery, JobType.Taxi, JobType.StreetCleanup, JobType.HousePainting };
                for (int i = 0; i < jobSelectButtons.Length && i < types.Length; i++)
                {
                    JobType captured = types[i];
                    if (jobSelectButtons[i] != null)
                    {
                        jobSelectButtons[i].onClick.AddListener(() => PinJobLocation(captured));
                    }
                }
            }

            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnTogglePhonePressed += TogglePhone;
            }

            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged += HandleLanguageChanged;
            }

            SelectTab(0);
            RefreshAllTexts();

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnTogglePhonePressed -= TogglePhone;
            }
            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged -= HandleLanguageChanged;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleLanguageChanged(string _)
        {
            RefreshAllTexts();
        }

        public void TogglePhone()
        {
            if (IsOpen)
            {
                Close();
                return;
            }

            // Do not open phone over Main Menu, Pause Menu, active story dialogue, mission failure, or any other open modal
            if (MainMenuController.IsMenuActive) return;
            if (PlayerModeController.Instance != null && PlayerModeController.Instance.IsModalLocked) return;
            MissionManager mm = MissionManager.Instance;
            if (mm != null && (mm.IsDialoguePlaying || mm.IsFailed))
            {
                return;
            }

            Open();
        }

        public void OpenFeedbackTab()
        {
            if (!IsOpen) Open();
            SelectTab(3);
        }

        public void Open()
        {
            if (panelRoot == null) return;

            _cachedVehicles = FindObjectsByType<SedanVehicleController>(FindObjectsInactive.Include);
            _openedFrame = Time.frameCount;
            RefreshAllTexts();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, true);
            }

            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimateRoutine(opening: true));
        }

        public void Close()
        {
            if (panelRoot == null || !panelRoot.activeSelf) return;

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }

            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimateRoutine(opening: false));
        }

        /// <summary>
        /// Adds a 4th "Öneri" tab next to the existing ones by cloning the Stats tab button,
        /// and re-spaces the tab row so it fits between the title and the language button.
        /// </summary>
        private void BuildFeedbackTab()
        {
            if (_feedbackView != null || panelRoot == null || tabStatsButton == null) return;

            GameObject clone = Instantiate(tabStatsButton.gameObject, tabStatsButton.transform.parent);
            clone.name = "Btn_Tab_Feedback";
            _tabFeedbackButton = clone.GetComponent<Button>();
            _tabFeedbackButton.onClick = new Button.ButtonClickedEvent();
            _tabFeedbackButton.onClick.AddListener(() => SelectTab(3));
            _tabFeedbackLabel = clone.GetComponentInChildren<Text>(true);

            Button[] row = { tabMapButton, tabJobsButton, tabStatsButton, _tabFeedbackButton };
            const float width = 140f;
            const float spacing = 150f;
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] == null) continue;
                RectTransform rt = (RectTransform)row[i].transform;
                rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
                rt.anchoredPosition = new Vector2((i - 1.5f) * spacing, rt.anchoredPosition.y);
            }

            if (headerTitleText != null)
            {
                RectTransform hrt = headerTitleText.rectTransform;
                hrt.sizeDelta = new Vector2(Mathf.Min(hrt.sizeDelta.x, 290f), hrt.sizeDelta.y);
                headerTitleText.resizeTextForBestFit = true;
                headerTitleText.resizeTextMinSize = 20;
                headerTitleText.resizeTextMaxSize = headerTitleText.fontSize;
            }

            Font font = tabStatsLabel != null ? tabStatsLabel.font : null;
            Image btnImg = tabStatsButton.GetComponent<Image>();
            _feedbackView = FeedbackTabView.Create(panelRoot.transform, font, btnImg != null ? btnImg.sprite : null);
        }

        public void SelectTab(int tabIdx)
        {
            _activeTabIndex = Mathf.Clamp(tabIdx, 0, _feedbackView != null ? 3 : 2);
            if (mapTabRoot != null) mapTabRoot.SetActive(_activeTabIndex == 0);
            if (jobsTabRoot != null) jobsTabRoot.SetActive(_activeTabIndex == 1);
            if (statsTabRoot != null) statsTabRoot.SetActive(_activeTabIndex == 2);
            if (_feedbackView != null) _feedbackView.gameObject.SetActive(_activeTabIndex == 3);

            SetTabButtonStyle(tabMapButton, _activeTabIndex == 0);
            SetTabButtonStyle(tabJobsButton, _activeTabIndex == 1);
            SetTabButtonStyle(tabStatsButton, _activeTabIndex == 2);
            SetTabButtonStyle(_tabFeedbackButton, _activeTabIndex == 3);

            RefreshAllTexts();
        }

        private static void SetTabButtonStyle(Button btn, bool selected)
        {
            if (btn == null) return;
            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = selected ? ActiveTabColor : InactiveTabColor;
            }
            Text lbl = btn.GetComponentInChildren<Text>(true);
            if (lbl != null)
            {
                lbl.color = selected ? OurWorldUiKit.InkDark : OurWorldUiKit.TextSecondary;
            }
            Transform borderTf = btn.transform.Find("HairlineBorder");
            if (borderTf != null)
            {
                Image borderImg = borderTf.GetComponent<Image>();
                if (borderImg != null) borderImg.color = selected ? Color.clear : OurWorldUiKit.BorderSubtle;
            }
            UiButtonFeedback fb = btn.GetComponent<UiButtonFeedback>();
            if (fb != null)
            {
                fb.Configure(btn, img);
            }
        }

        public void SetCustomWaypoint(Vector3 worldPos, string label = null)
        {
            HasCustomWaypoint = true;
            CustomWaypointPosition = new Vector3(
                Mathf.Clamp(worldPos.x, -415f, 415f),
                0.2f,
                Mathf.Clamp(worldPos.z, -415f, 415f));

            if (!string.IsNullOrEmpty(label) && PlayerModeController.Instance != null)
            {
                string fmt = LocalizationService.Tr("ui.phone.waypoint_set_named", "Hedef işaretlendi: {0}");
                PlayerModeController.Instance.RaiseStatusMessage(string.Format(fmt, label));
            }

            if (clearWaypointButton != null)
            {
                clearWaypointButton.interactable = true;
            }
        }

        public void ClearCustomWaypoint()
        {
            HasCustomWaypoint = false;
            if (clearWaypointButton != null)
            {
                clearWaypointButton.interactable = false;
            }
        }

        private void PinJobLocation(JobType type)
        {
            if (JobManager.Instance == null) return;
            Vector3 pos = JobManager.Instance.GetJobBoardPosition(type);
            string jobTitle = GetJobTitle(type);
            SetCustomWaypoint(pos, jobTitle);
            Close();
        }

        private void HandleMapClicked(PointerEventData eventData)
        {
            if (mapViewportRect == null || eventData == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mapViewportRect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                Rect r = mapViewportRect.rect;
                if (r.width <= 1f || r.height <= 1f) return;

                float nx = Mathf.Clamp01((local.x - r.xMin) / r.width);
                float ny = Mathf.Clamp01((local.y - r.yMin) / r.height);
                float wx = ViewWorldMinX + nx * ViewWorldSpan;
                float wz = ViewWorldMinZ + ny * ViewWorldSpan;
                SetCustomWaypoint(new Vector3(wx, 0.2f, wz));
            }
        }

        private Vector2 WorldToViewportAnchored(Vector3 worldPos, string markerDebugName = null)
        {
            if (mapViewportRect == null) return Vector2.zero;
            Rect r = mapViewportRect.rect;
            float rawNx = (worldPos.x - ViewWorldMinX) / ViewWorldSpan;
            float rawNy = (worldPos.z - ViewWorldMinZ) / ViewWorldSpan;
            // Outer Sultanbeyli districts clamp neatly to the corresponding compass rim of the Hamidiye map view
            float nx = Mathf.Clamp(rawNx, 0.04f, 0.96f);
            float ny = Mathf.Clamp(rawNy, 0.04f, 0.96f);
            return new Vector2((nx - 0.5f) * r.width, (ny - 0.5f) * r.height);
        }

        private void Update()
        {
            // Auto-clear custom waypoint when player arrives within 8.5 m
            if (HasCustomWaypoint && PlayerModeController.Instance != null)
            {
                Vector3 diff = PlayerModeController.Instance.ActiveWorldPosition - CustomWaypointPosition;
                diff.y = 0f;
                if (diff.sqrMagnitude <= 72f)
                {
                    ClearCustomWaypoint();
                }
            }

            if (!IsOpen) return;

            if (Time.frameCount != _openedFrame && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (_activeTabIndex == 0)
            {
                UpdateMapMarkers();
            }
            else if (_activeTabIndex == 2)
            {
                RefreshStatsTab();
            }
        }

        private void UpdateMapMarkers()
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode != null && mapPlayerArrow != null)
            {
                mapPlayerArrow.anchoredPosition = WorldToViewportAnchored(mode.ActiveWorldPosition);
                float yaw = 0f;
                if (mode.IsDriving && mode.CurrentVehicle != null)
                {
                    yaw = mode.CurrentVehicle.transform.eulerAngles.y;
                }
                else if (mode.FootController != null)
                {
                    yaw = mode.FootController.transform.eulerAngles.y;
                }
                mapPlayerArrow.localEulerAngles = new Vector3(0f, 0f, -yaw);
            }

            if (mapGasStationMarker != null)
            {
                mapGasStationMarker.anchoredPosition = WorldToViewportAnchored(_resolvedGasStationPos, "FuelPump");
            }

            if (JobManager.Instance != null && mapJobMarkers != null)
            {
                JobType[] types = { JobType.PizzaDelivery, JobType.Taxi, JobType.StreetCleanup, JobType.HousePainting };
                for (int i = 0; i < mapJobMarkers.Length && i < types.Length; i++)
                {
                    if (mapJobMarkers[i] == null) continue;
                    Vector3 jPos = JobManager.Instance.GetJobBoardPosition(types[i]);
                    mapJobMarkers[i].anchoredPosition = WorldToViewportAnchored(jPos);
                }
            }

            if (_cachedVehicles == null || _cachedVehicles.Length == 0)
            {
                _cachedVehicles = FindObjectsByType<SedanVehicleController>(FindObjectsInactive.Include);
            }

            if (mapCarMarkers != null && mapCarMarkers.Length > 0)
            {
                // Dynamically expand marker pool so every drivable car across the entire expanded map is visible!
                if (_cachedVehicles != null && _cachedVehicles.Length > mapCarMarkers.Length && mapCarMarkers[0] != null)
                {
                    RectTransform template = mapCarMarkers[0];
                    RectTransform[] expanded = new RectTransform[_cachedVehicles.Length];
                    for (int i = 0; i < mapCarMarkers.Length; i++) expanded[i] = mapCarMarkers[i];
                    for (int i = mapCarMarkers.Length; i < expanded.Length; i++)
                    {
                        RectTransform clone = Instantiate(template, template.parent);
                        clone.name = $"MapCarMarker_{i + 1:00}";
                        expanded[i] = clone;
                    }
                    mapCarMarkers = expanded;
                }

                for (int i = 0; i < mapCarMarkers.Length; i++)
                {
                    RectTransform marker = mapCarMarkers[i];
                    if (marker == null) continue;

                    if (_cachedVehicles != null && i < _cachedVehicles.Length && _cachedVehicles[i] != null && _cachedVehicles[i].gameObject.activeInHierarchy)
                    {
                        if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
                        marker.anchoredPosition = WorldToViewportAnchored(_cachedVehicles[i].transform.position);
                    }
                    else if (marker.gameObject.activeSelf)
                    {
                        marker.gameObject.SetActive(false);
                    }
                }
            }

            if (mapWaypointMarker != null)
            {
                if (ObjectiveWaypointIndicator.TryGetActiveNavigationTarget(out Vector3 wpWorld, out _))
                {
                    if (!mapWaypointMarker.gameObject.activeSelf) mapWaypointMarker.gameObject.SetActive(true);
                    mapWaypointMarker.anchoredPosition = WorldToViewportAnchored(wpWorld);
                    float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.12f;
                    mapWaypointMarker.localScale = new Vector3(pulse, pulse, 1f);
                }
                else if (mapWaypointMarker.gameObject.activeSelf)
                {
                    mapWaypointMarker.gameObject.SetActive(false);
                }
            }

            if (clearWaypointButton != null)
            {
                clearWaypointButton.interactable = HasCustomWaypoint;
            }
            if (_fastTravelBtn != null)
            {
                _fastTravelBtn.interactable = HasCustomWaypoint;
            }
            for (int i = 0; i < _mapLandmarkPins.Count; i++)
            {
                var pin = _mapLandmarkPins[i];
                if (pin.rt != null) pin.rt.anchoredPosition = WorldToViewportAnchored(pin.pos);
            }
        }

        private void BuildMapLandmarkPinsAndFastTravel()
        {
            if (mapViewportRect == null || mapTabRoot == null) return;
            OurWorldUiKit.EnsureInitialized();

            // Clickable landmark badges directly on the Phone Map (covers all runtime-added regions!)
            (string tr, string en, Vector3 pos, Color col)[] landmarks =
            {
                ("Atlas AVM",  "Atlas Mall",   new Vector3(-116f, 0.2f, -58f), new Color(0.97f, 0.75f, 0.24f)),
                ("Oto Galeri", "Auto Gallery", new Vector3(-115f, 0.2f,  56f), new Color(0.24f, 0.90f, 0.84f)),
                ("Çiftlik",    "Aydos Farm",   new Vector3( 148f, 0.2f,   0f), new Color(0.32f, 0.80f, 0.44f)),
                ("Gölet",      "Lake Pier",    new Vector3(-164f, 0.2f,   0f), new Color(0.22f, 0.76f, 0.96f)),
                ("Saat Kulesi","Clock Tower",  new Vector3(   0f, 0.2f, 136f), new Color(0.96f, 0.56f, 0.28f)),
                ("Havadis Sk.","Havadis St.",  new Vector3( -16f, 0.2f, 186f), new Color(0.14f, 0.78f, 0.70f)),
                ("Kadife Sk.", "Kadife St.",   new Vector3( -12f, 0.2f,-186f), new Color(0.14f, 0.78f, 0.70f)),
                ("Poligon",    "Range",        new Vector3(  92f, 0.2f,-214f), new Color(0.96f, 0.68f, 0.20f)),
                ("Spor",       "Sports",       new Vector3( -14f, 0.2f, -29f), new Color(0.92f, 0.44f, 0.36f)),
                ("Evim No:48", "Home No:48",   new Vector3( -62f, 0.2f,   4f), new Color(0.82f, 0.88f, 0.96f)),
                ("Kent Meyd.", "City Square",  new Vector3( 172f, 0.2f, 184f), new Color(0.26f, 0.72f, 0.98f)),
                ("M5 Metro",   "M5 Metro",     new Vector3(  -6f, 0.2f, 266f), new Color(0.95f, 0.30f, 0.32f)),
                ("Hasanpaşa",  "Hasanpaşa",    new Vector3(-174f, 0.2f, 188f), new Color(0.32f, 0.86f, 0.56f)),
                ("Semt Pazarı","Bazaar",       new Vector3(-176f, 0.2f,-174f), new Color(0.98f, 0.64f, 0.22f)),
                ("Mimar Sinan","Mimar Sinan",  new Vector3( -52f, 0.2f,-268f), new Color(0.72f, 0.56f, 0.96f)),
                ("Aydos Kale", "Aydos Castle", new Vector3( 236f, 0.2f,-138f), new Color(0.92f, 0.80f, 0.42f))
            };

            for (int i = 0; i < landmarks.Length; i++)
            {
                var lm = landmarks[i];
                Button pinBtn = OurWorldUiKit.CreatePillButton(
                    $"MapLandmarkPin_{i}", mapViewportRect, lm.tr, 13,
                    new Color(0.05f, 0.08f, 0.13f, 0.90f), lm.col, lm.col, out Text pinLbl, () =>
                    {
                        SetCustomWaypoint(lm.pos);
                        GameFeelAudio.PlayUiClick();
                    });
                RectTransform prt = (RectTransform)pinBtn.transform;
                prt.anchorMin = new Vector2(0.5f, 0.5f);
                prt.anchorMax = new Vector2(0.5f, 0.5f);
                prt.pivot = new Vector2(0.5f, 0.5f);
                prt.sizeDelta = new Vector2(102f, 24f);
                pinLbl.rectTransform.offsetMin = new Vector2(6f, 0f);
                pinLbl.rectTransform.offsetMax = new Vector2(-6f, 0f);
                _mapLandmarkPins.Add((prt, pinLbl, lm.tr, lm.en, lm.pos));
            }

            // Bottom utility bar on Map tab: Fast Travel (Taxi 25 TL) + Summon Personal Car (Valet 15 TL)
            GameObject actionBar = new GameObject("MapTravelValetBar", typeof(RectTransform));
            actionBar.transform.SetParent(mapTabRoot.transform, false);
            RectTransform art = (RectTransform)actionBar.transform;
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(0.68f, 0f);
            art.pivot = new Vector2(0f, 0f);
            art.anchoredPosition = new Vector2(18f, 14f);
            art.sizeDelta = new Vector2(-18f, 44f);

            HorizontalLayoutGroup hlg = actionBar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            _fastTravelBtn = OurWorldUiKit.CreatePillButton("Btn_FastTravelTaxi", actionBar.transform,
                "Taksiyle Git (25 ₺)", 16, OurWorldUiKit.SurfaceActive, OurWorldUiKit.AccentGold, OurWorldUiKit.AccentTeal,
                out _fastTravelLabel, HandleFastTravelToWaypoint);

            _callCarBtn = OurWorldUiKit.CreatePillButton("Btn_CallMyCar", actionBar.transform,
                "Arabamı Çağır · Vale (15 ₺)", 16, OurWorldUiKit.SurfaceSoft, OurWorldUiKit.TextPrimary, OurWorldUiKit.BorderSubtle,
                out _callCarLabel, HandleCallPersonalCar);
        }

        private void BuildStatsQuickActions()
        {
            if (statsTabRoot == null) return;
            OurWorldUiKit.EnsureInitialized();

            GameObject bar = new GameObject("StatsQuickActionsBar", typeof(RectTransform));
            bar.transform.SetParent(statsTabRoot.transform, false);
            RectTransform brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 14f);
            brt.sizeDelta = new Vector2(-48f, 48f);

            HorizontalLayoutGroup hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            OurWorldUiKit.CreatePillButton("Btn_OpenDailyQuests", bar.transform,
                "★ Günlük Görevler & Başarımlar", 17, OurWorldUiKit.AccentGold, OurWorldUiKit.InkDark, Color.clear,
                out _statsDailyBtnLabel, () =>
                {
                    GameFeelAudio.PlayUiClick();
                    Close();
                    DailyQuestsOverlay.EnsureInstance().Show(0);
                });

            OurWorldUiKit.CreatePillButton("Btn_OpenStyleStudio", bar.transform,
                "Stil & Aksesuarlar (Gözlük/Şapka)", 17, OurWorldUiKit.SurfaceActive, OurWorldUiKit.TextPrimary, OurWorldUiKit.AccentTeal,
                out _statsStyleBtnLabel, () =>
                {
                    GameFeelAudio.PlayUiClick();
                    Close();
                    BarberAndStylePanel.Instance?.Show();
                });
        }

        private void HandleFastTravelToWaypoint()
        {
            if (!HasCustomWaypoint) return;
            PlayerModeController mode = PlayerModeController.Instance;
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            if (mode == null || mode.FootController == null) return;

            if (PlayerShoppingCart.HasActiveItems)
            {
                GameFeelAudio.PlayUiClick();
                mode.RaiseStatusMessage(isEn
                    ? "Pay at the register or return unpaid items before calling a taxi!"
                    : "Taksi çağırmadan önce elindeki / denediğin ürünleri kasada öde veya stanta bırak!");
                return;
            }

            if ((mode.CurrentMode != ControlMode.OnFoot && mode.CurrentMode != ControlMode.Driving) ||
                MosquePrayerInteractable.IsPlayerPraying ||
                PlayerActionAnimator.IsBusyOn(mode))
            {
                GameFeelAudio.PlayUiClick();
                mode.RaiseStatusMessage(isEn
                    ? "Finish your current activity (stand up / exit sport or bike) before taking a taxi!"
                    : "Taksi çağırmadan önce mevcut aktiviteyi bitir (kalk, bisikletten veya maçtan çık)!");
                return;
            }

            bool isDriving = mode.IsDriving && mode.CurrentVehicle != null;
            if (!TryResolveSafeLandingPosition(CustomWaypointPosition, isDriving, out Vector3 safeDest))
            {
                GameFeelAudio.PlayUiClick();
                mode.RaiseStatusMessage(isEn
                    ? "That pin is inside a building or obstacle — tap a road or landmark badge!"
                    : "İşaretli nokta bina veya engel içinde — haritada bir yola ya da bölge rozetine dokun!");
                return;
            }

            SaveManager sm = SaveManager.Instance;
            if (sm != null && !sm.TrySpend(25))
            {
                GameFeelAudio.PlayUiClick();
                mode.RaiseStatusMessage(isEn ? "Fast Travel Taxi requires 25 ₺!" : "Taksiyle hızlı seyahat için 25 ₺ gerekli!");
                return;
            }

            CarryableProp.DropHeldPropIfAny();
            StreetCatController.CancelActivePet();

            if (isDriving)
            {
                mode.CurrentVehicle.TeleportAndReset(safeDest + Vector3.up * 0.22f, mode.CurrentVehicle.transform.rotation);
            }
            else
            {
                mode.FootController.TeleportTo(safeDest, mode.FootController.transform.rotation);
            }

            ClearCustomWaypoint();
            GameFeelAudio.PlayCoinChime();
            mode.RaiseStatusMessage(isEn ? "Arrived via Fast Travel Taxi (-25 ₺)" : "Taksiyle hedefe ulaştın (-25 ₺)");
            Close();
        }

        private static bool TryResolveSafeLandingPosition(Vector3 rawTarget, bool forVehicle, out Vector3 safePos)
        {
            Vector3 probe = new Vector3(Mathf.Clamp(rawTarget.x, -412f, 412f), 0.25f, Mathf.Clamp(rawTarget.z, -412f, 412f));

            // Snap well-known enclosed/water footprints safely to their open entrances
            if (probe.x <= -166f && probe.x >= -226f && probe.z >= -46f && probe.z <= 46f)
            {
                probe = new Vector3(-163.5f, 0.22f, 0f); // Sultanbeyli Lake Pier
            }
            else if (probe.x >= -135f && probe.x <= -97f && probe.z >= -52f && probe.z <= -16f)
            {
                probe = new Vector3(-116f, 0.22f, -58f); // Hamidiye Atlas AVM Entrance Plaza
            }
            else if (probe.x >= -129f && probe.x <= -101f && probe.z >= 59f && probe.z <= 82f)
            {
                probe = new Vector3(-115f, 0.25f, 54.5f); // Hamidiye Oto Galeri Delivery Bay
            }
            else if (probe.x >= 66f && probe.x <= 138f && probe.z >= -342f && probe.z <= -216f)
            {
                probe = new Vector3(92f, 0.22f, -210f); // Aydos Shooting Range Parking Pad
            }
            else if (probe.x >= 244f && probe.x <= 284f && probe.z >= -138f && probe.z <= -98f)
            {
                probe = new Vector3(236f, 0.22f, -138f); // Historic Aydos Castle Trailhead
            }

            float[] radii = { 0f, 3.2f, 6.5f, 10.5f };
            for (int rIdx = 0; rIdx < radii.Length; rIdx++)
            {
                float rad = radii[rIdx];
                int samples = rad <= 0.1f ? 1 : 8;
                for (int s = 0; s < samples; s++)
                {
                    float ang = (s / (float)samples) * Mathf.PI * 2f;
                    Vector3 cand = new Vector3(
                        Mathf.Clamp(probe.x + Mathf.Cos(ang) * rad, -412f, 412f),
                        0.25f,
                        Mathf.Clamp(probe.z + Mathf.Sin(ang) * rad, -412f, 412f));

                    // Skip water interior
                    if (cand.x <= -166f && cand.x >= -226f && cand.z >= -46f && cand.z <= 46f) continue;

                    // Raycast down from sky: reject rooftops (y > 1.35m) so we never land on roofs or inside high geometry
                    float groundY = 0.18f;
                    if (Physics.Raycast(new Vector3(cand.x, 32f, cand.z), Vector3.down, out RaycastHit hit, 36f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.point.y > 1.35f || hit.point.y < -0.4f) continue;
                        groundY = Mathf.Max(0.12f, hit.point.y + 0.08f);
                    }

                    Vector3 testPos = new Vector3(cand.x, groundY, cand.z);
                    bool blocked = forVehicle
                        ? Physics.CheckBox(testPos + Vector3.up * 1.15f, new Vector3(1.1f, 0.75f, 2.2f), Quaternion.identity,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        : Physics.CheckCapsule(testPos + Vector3.up * 0.45f, testPos + Vector3.up * 1.65f, 0.36f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

                    if (!blocked)
                    {
                        safePos = testPos;
                        return true;
                    }
                }
            }

            safePos = probe;
            return false;
        }

        private void HandleCallPersonalCar()
        {
            PlayerModeController mode = PlayerModeController.Instance;
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            if (mode == null || mode.FootController == null) return;

            if (mode.IsDriving)
            {
                mode.RaiseStatusMessage(isEn ? "You are already inside a vehicle!" : "Zaten bir aracın içindesin!");
                return;
            }
            if (mode.CurrentMode != ControlMode.OnFoot || MosquePrayerInteractable.IsPlayerPraying)
            {
                mode.RaiseStatusMessage(isEn ? "Stand on foot outside to call your car!" : "Arabanı çağırmak için dışarıda ayakta olmalısın!");
                return;
            }

            // Do not summon a car indoors, inside Atlas AVM, or on Oto Galeri showroom deck
            Vector3 p = mode.ActiveWorldPosition;
            bool inMall = p.x >= -134f && p.x <= -98f && p.z >= -51f && p.z <= -17f;
            bool inShowroom = p.x >= -128f && p.x <= -102f && p.z >= 64f && p.z <= 80f;
            if (InteriorZone.IsPlayerInsideAny || inMall || inShowroom)
            {
                mode.RaiseStatusMessage(isEn ? "Step outside to summon your car!" : "Arabanı çağırmak için dışarı çık!");
                return;
            }

            SedanVehicleController targetCar = mode.PlayerSedan;
            if (targetCar == null && _cachedVehicles != null)
            {
                for (int i = 0; i < _cachedVehicles.Length; i++)
                {
                    if (_cachedVehicles[i] != null && _cachedVehicles[i].name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        targetCar = _cachedVehicles[i];
                        break;
                    }
                }
                if (targetCar == null && _cachedVehicles.Length > 0) targetCar = _cachedVehicles[0];
            }
            if (targetCar == null)
            {
                mode.RaiseStatusMessage(isEn ? "No car available to summon." : "Çağrılacak araç bulunamadı.");
                return;
            }

            // Find an unobstructed spot near the player so the car never spawns inside a wall
            Transform ft = mode.FootController.transform;
            Vector3[] candidates =
            {
                p + ft.forward * 4.8f,
                p + ft.right * 4.8f,
                p - ft.right * 4.8f,
                p - ft.forward * 4.8f,
                p + ft.forward * 7.5f
            };
            Vector3 spawnPos = Vector3.zero;
            bool foundSpot = false;
            Quaternion spawnRot = Quaternion.Euler(0f, ft.eulerAngles.y, 0f);
            for (int i = 0; i < candidates.Length; i++)
            {
                float cx = Mathf.Clamp(candidates[i].x, -412f, 412f);
                float cz = Mathf.Clamp(candidates[i].z, -412f, 412f);
                float groundY = 0.25f;
                if (Physics.Raycast(new Vector3(cx, p.y + 4.0f, cz), Vector3.down, out RaycastHit gHit, 8.0f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && gHit.normal.y > 0.72f)
                {
                    groundY = gHit.point.y + 0.28f;
                }
                Vector3 c = new Vector3(cx, groundY, cz);
                if (!Physics.CheckBox(c + Vector3.up * 1.1f, new Vector3(1.1f, 0.72f, 2.2f), spawnRot,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    spawnPos = c;
                    foundSpot = true;
                    break;
                }
            }
            if (!foundSpot)
            {
                GameFeelAudio.PlayUiClick();
                mode.RaiseStatusMessage(isEn ? "Too tight here — step onto the road to call Valet!" : "Burası çok dar — vale için sokağa veya açık alana çık!");
                return;
            }

            SaveManager sm = SaveManager.Instance;
            if (sm != null && !sm.TrySpend(15))
            {
                GameFeelAudio.PlayUiClick();
                mode.RaiseStatusMessage(isEn ? "Car Valet costs 15 ₺!" : "Vale hizmeti 15 ₺!");
                return;
            }

            targetCar.TeleportAndReset(spawnPos, spawnRot);
            GameFeelAudio.PlayCoinChime();
            mode.RaiseStatusMessage(isEn ? "Valet delivered your car right ahead! (-15 ₺)" : "Vale arabanı hemen önüne getirdi! (-15 ₺)");
            Close();
        }

        public void RefreshAllTexts()
        {
            if (headerTitleText != null) headerTitleText.text = LocalizationService.Tr("ui.phone.title", "Kasaba Rehberi");
            if (tabMapLabel != null) tabMapLabel.text = LocalizationService.Tr("ui.phone.tab_map", "Harita");
            if (tabJobsLabel != null) tabJobsLabel.text = LocalizationService.Tr("ui.phone.tab_jobs", "İşler");
            if (tabStatsLabel != null) tabStatsLabel.text = LocalizationService.Tr("ui.phone.tab_stats", "İstatistik");
            if (_tabFeedbackLabel != null) _tabFeedbackLabel.text = LocalizationService.Tr("ui.tab.feedback", "Öneri");
            if (_feedbackView != null) _feedbackView.RefreshTexts();
            if (closeLabel != null) closeLabel.text = LocalizationService.Tr("ui.btn.close", "Kapat");

            if (languageLabel != null)
            {
                string code = LocalizationService.Instance != null ? LocalizationService.Instance.CurrentLanguage.ToUpperInvariant() : "TR";
                string prefix = LocalizationService.Tr("ui.phone.lang_prefix", "Dil");
                languageLabel.text = $"{prefix}: {code}";
            }

            if (clearWaypointLabel != null) clearWaypointLabel.text = LocalizationService.Tr("ui.phone.clear_wp", "İşareti Kaldır");
            if (mapHintLabel != null) mapHintLabel.text = LocalizationService.Tr("ui.phone.map_hint", "Haritaya dokunarak özel hedef işaretleyebilirsin.");

            bool isEnMap = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            for (int i = 0; i < _mapLandmarkPins.Count; i++)
            {
                var pin = _mapLandmarkPins[i];
                if (pin.lbl != null) pin.lbl.text = isEnMap ? pin.en : pin.tr;
            }
            if (_fastTravelLabel != null) _fastTravelLabel.text = isEnMap ? "Taxi Fast Travel (25 ₺)" : "Taksiyle Git (25 ₺)";
            if (_callCarLabel != null) _callCarLabel.text = isEnMap ? "Call My Car · Valet (15 ₺)" : "Arabamı Çağır · Vale (15 ₺)";
            if (mapLegendText != null)
            {
                mapLegendText.text = LocalizationService.Tr(
                    "ui.phone.legend",
                    "▲ Konumun\n● Sürülebilir Araçlar\n★ Ek İş Başlangıç Noktaları\n⛽ Sahil Akaryakıt İstasyonu\n◎ Aktif Hedef / Rota");
            }

            RefreshJobsTab();
            RefreshStatsTab();
            if (_activeTabIndex == 0)
            {
                UpdateMapMarkers();
            }
        }

        private void RefreshJobsTab()
        {
            JobType[] types = { JobType.PizzaDelivery, JobType.Taxi, JobType.StreetCleanup, JobType.HousePainting };
            string[] descKeys =
            {
                "job.pizza.desc",
                "job.taxi.desc",
                "job.cleanup.desc",
                "job.paint.desc"
            };
            string[] descFallbacks =
            {
                "Selman Bakkal No:58 · Araçla mahalleye sıcak teslimat (40 ₺ + bonus)",
                "Fatih Bulvarı · Saat Kulesi Taksi Durağı · 8 caddede yolcu taşı",
                "Petrol Yolu Cd. · Cadde ve okul çevresinde 10 çöp topla (parça başı 5 ₺)",
                "Zübeyde Hanım Cd. · İstenen evi seçilen renge boya (60 ₺)"
            };

            string pinLabel = LocalizationService.Tr("ui.phone.pin_job", "Konum İşaretle");

            for (int i = 0; i < 4; i++)
            {
                if (jobRowTitleTexts != null && i < jobRowTitleTexts.Length && jobRowTitleTexts[i] != null)
                {
                    jobRowTitleTexts[i].text = GetJobTitle(types[i]);
                }
                if (jobRowDescTexts != null && i < jobRowDescTexts.Length && jobRowDescTexts[i] != null)
                {
                    jobRowDescTexts[i].text = LocalizationService.Tr(descKeys[i], descFallbacks[i]);
                }
                if (jobRowPinBtnLabels != null && i < jobRowPinBtnLabels.Length && jobRowPinBtnLabels[i] != null)
                {
                    jobRowPinBtnLabels[i].text = pinLabel;
                }
            }

            bool hasActive = JobManager.Instance != null && JobManager.Instance.HasActiveJob;
            if (cancelActiveJobButton != null)
            {
                cancelActiveJobButton.gameObject.SetActive(hasActive);
            }
            if (cancelActiveJobLabel != null)
            {
                cancelActiveJobLabel.text = LocalizationService.Tr("ui.btn.cancel_job", "Aktif İşi İptal Et");
            }
        }

        private void RefreshStatsTab()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            int money = d != null ? d.money : 250;
            int pizzas = d != null ? d.deliveriesDone : 0;
            int taxis = d != null ? d.taxiFaresDone : 0;
            int painted = d != null ? d.housesPaintedCount : 0;
            int litter = d != null ? d.litterCollectedCount : 0;
            int missionsDone = d != null && d.completedMissionIds != null ? d.completedMissionIds.Count : 0;
            int landmarks = LandmarkDiscoverySystem.DiscoveredCount;
            int memories = NeighborhoodCollectibleManager.CollectedCount;
            string rankTitle = NeighborhoodReputation.GetRankTitle();
            int repXp = NeighborhoodReputation.CurrentXp;
            float playSec = d != null ? d.totalPlayTimeSeconds : 0f;

            int hours = Mathf.FloorToInt(playSec / 3600f);
            int mins = Mathf.FloorToInt((playSec % 3600f) / 60f);
            int secs = Mathf.FloorToInt(playSec % 60f);
            string timeFormatted = hours > 0 ? $"{hours}s {mins:00}dk" : $"{mins:00}dk {secs:00}sn";

            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            string[] labels = isEn
                ? new[] { "Balance", "Reputation", "Story Progress", "Explore & Memories", "Pizza & Taxi", "Paint & Cleanup", "Play Time" }
                : new[] { "Bakiye", "Mahalle Saygınlığı", "Hikâye İlerlemesi", "Keşif & Hatıralar", "Pizza & Taksi", "Boyama & Temizlik", "Oynama Süresi" };
            int totalMissions = MissionManager.Instance != null && MissionManager.Instance.Catalog != null && MissionManager.Instance.Catalog.missions != null
                ? MissionManager.Instance.Catalog.missions.Count
                : 26;
            string[] values =
            {
                $"₺ {money}",
                $"{rankTitle} ({repXp} XP)",
                $"{missionsDone}/{totalMissions}",
                $"{landmarks}/{LandmarkDiscoverySystem.TotalCount}  ·  {memories}/{NeighborhoodCollectibleManager.TotalCount}",
                $"{pizzas}  ·  {taxis}",
                $"{painted}  ·  {litter}",
                timeFormatted
            };

            for (int i = 0; i < 7; i++)
            {
                if (statNameTexts != null && i < statNameTexts.Length && statNameTexts[i] != null)
                {
                    statNameTexts[i].text = labels[i];
                }
                if (statValueTexts != null && i < statValueTexts.Length && statValueTexts[i] != null)
                {
                    statValueTexts[i].text = values[i];
                }
            }

            if (_statsDailyBtnLabel != null)
            {
                int ready = DailyQuestAndAchievementSystem.GetUnclaimedReadyCount();
                string badge = ready > 0 ? $" ({ready}!)" : string.Empty;
                _statsDailyBtnLabel.text = (isEn ? "★ Daily Quests & Achievements" : "★ Günlük Görevler & Başarımlar") + badge;
            }
            if (_statsStyleBtnLabel != null)
            {
                _statsStyleBtnLabel.text = isEn ? "Style & 3D Accessories" : "Stil & Aksesuarlar (Gözlük/Şapka)";
            }
        }

        private static string GetJobTitle(JobType type)
        {
            switch (type)
            {
                case JobType.PizzaDelivery:
                    return LocalizationService.Tr("job.pizza.name", "Pizza Teslimatı");
                case JobType.Taxi:
                    return LocalizationService.Tr("job.taxi.name", "Taksi");
                case JobType.StreetCleanup:
                    return LocalizationService.Tr("job.cleanup.name", "Sokak Temizliği");
                case JobType.HousePainting:
                    return LocalizationService.Tr("job.paint.name", "Ev Boyama İşi");
                default:
                    return string.Empty;
            }
        }

        private IEnumerator AnimateRoutine(bool opening)
        {
            if (panelRoot == null) yield break;

            RectTransform rt = panelRoot.GetComponent<RectTransform>();
            if (opening)
            {
                panelRoot.SetActive(true);
            }

            const float duration = 0.20f;
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : (opening ? 0f : 1f);
            float endAlpha = opening ? 1f : 0f;
            Vector3 startScale = opening ? Vector3.one * 0.95f : Vector3.one;
            Vector3 endScale = opening ? Vector3.one : Vector3.one * 0.95f;

            if (opening && canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            if (rt != null)
            {
                rt.localScale = startScale;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                }
                if (rt != null)
                {
                    rt.localScale = Vector3.Lerp(startScale, endScale, t);
                }
                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = endAlpha;
            }
            if (rt != null)
            {
                rt.localScale = opening ? Vector3.one : endScale;
            }
            if (!opening)
            {
                panelRoot.SetActive(false);
            }

            _animRoutine = null;
        }
    }
}
