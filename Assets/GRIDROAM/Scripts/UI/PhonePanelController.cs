using System.Collections;
using Gridroam.Core;
using Gridroam.Input;
using Gridroam.Jobs;
using Gridroam.Missions;
using Gridroam.Player;
using Gridroam.Vehicle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gridroam.UI
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

        public const float ViewWorldMinX = -148f;
        public const float ViewWorldMinZ = -42f;
        public const float ViewWorldSpan = 310f;

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

        private static readonly Color ActiveTabColor = new Color(0.31f, 0.70f, 0.66f, 0.90f);
        private static readonly Color InactiveTabColor = new Color(0.11f, 0.14f, 0.19f, 0.75f);
        private static readonly Vector3 GasStationWorldPos = new Vector3(11.8f, 0.2f, 226.5f);

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

            // Do not open phone over active story dialogue or mission failure modal
            MissionManager mm = MissionManager.Instance;
            if (mm != null && (mm.IsDialoguePlaying || mm.IsFailed))
            {
                return;
            }

            Open();
        }

        public void Open()
        {
            if (panelRoot == null) return;

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

        public void SelectTab(int tabIdx)
        {
            _activeTabIndex = Mathf.Clamp(tabIdx, 0, 2);
            if (mapTabRoot != null) mapTabRoot.SetActive(_activeTabIndex == 0);
            if (jobsTabRoot != null) jobsTabRoot.SetActive(_activeTabIndex == 1);
            if (statsTabRoot != null) statsTabRoot.SetActive(_activeTabIndex == 2);

            SetTabButtonStyle(tabMapButton, _activeTabIndex == 0);
            SetTabButtonStyle(tabJobsButton, _activeTabIndex == 1);
            SetTabButtonStyle(tabStatsButton, _activeTabIndex == 2);

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
                Mathf.Clamp(worldPos.x, -76f, 36f),
                0.2f,
                Mathf.Clamp(worldPos.z, -46f, 294f));

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

        private Vector2 WorldToViewportAnchored(Vector3 worldPos)
        {
            if (mapViewportRect == null) return Vector2.zero;
            Rect r = mapViewportRect.rect;
            float nx = Mathf.Clamp01((worldPos.x - ViewWorldMinX) / ViewWorldSpan);
            float ny = Mathf.Clamp01((worldPos.z - ViewWorldMinZ) / ViewWorldSpan);
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
                mapGasStationMarker.anchoredPosition = WorldToViewportAnchored(GasStationWorldPos);
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

            if (mapCarMarkers != null)
            {
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
        }

        public void RefreshAllTexts()
        {
            if (headerTitleText != null) headerTitleText.text = LocalizationService.Tr("ui.phone.title", "Kasaba Rehberi");
            if (tabMapLabel != null) tabMapLabel.text = LocalizationService.Tr("ui.phone.tab_map", "Harita");
            if (tabJobsLabel != null) tabJobsLabel.text = LocalizationService.Tr("ui.phone.tab_jobs", "İşler");
            if (tabStatsLabel != null) tabStatsLabel.text = LocalizationService.Tr("ui.phone.tab_stats", "İstatistik");
            if (closeLabel != null) closeLabel.text = LocalizationService.Tr("ui.btn.close", "Kapat");

            if (languageLabel != null)
            {
                string code = LocalizationService.Instance != null ? LocalizationService.Instance.CurrentLanguage.ToUpperInvariant() : "TR";
                languageLabel.text = $"Dil: {code}";
            }

            if (clearWaypointLabel != null) clearWaypointLabel.text = LocalizationService.Tr("ui.phone.clear_wp", "İşareti Kaldır");
            if (mapHintLabel != null) mapHintLabel.text = LocalizationService.Tr("ui.phone.map_hint", "Haritaya dokunarak özel hedef işaretleyebilirsin.");
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
                "Ana Cadde Pizzacı · Araçla evlere sıcak teslimat (40 ₺ + bonus)",
                "Meydan Taksi Durağı · Yolcu al ve hedefe götür (mesafe + temiz sürüş)",
                "Saat Kulesi Önü · Yaya olarak 10 çöp topla (parça başı 5 ₺)",
                "Konut Mahallesi Girişi · İstenen evi seçilen renge boya (60 ₺)"
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
            float energy = PlayerNeedsController.Instance != null ? PlayerNeedsController.Instance.Energy : (d != null ? d.energy : 80f);
            int pizzas = d != null ? d.deliveriesDone : 0;
            int taxis = d != null ? d.taxiFaresDone : 0;
            int painted = d != null ? d.housesPaintedCount : 0;
            int litter = d != null ? d.litterCollectedCount : 0;
            float playSec = d != null ? d.totalPlayTimeSeconds : 0f;

            int hours = Mathf.FloorToInt(playSec / 3600f);
            int mins = Mathf.FloorToInt((playSec % 3600f) / 60f);
            int secs = Mathf.FloorToInt(playSec % 60f);
            string timeFormatted = hours > 0 ? $"{hours}s {mins:00}dk" : $"{mins:00}dk {secs:00}sn";

            string[] nameKeys =
            {
                "ui.stats.money",
                "ui.stats.energy",
                "ui.stats.deliveries",
                "ui.stats.taxis",
                "ui.stats.painted",
                "ui.stats.litter",
                "ui.stats.playtime"
            };
            string[] nameFallbacks =
            {
                "Bakiye",
                "Enerji",
                "Teslim Edilen Pizza",
                "Taksi Yolculuğu",
                "Boyanan Ev",
                "Toplanan Çöp",
                "Oynama Süresi"
            };
            string[] values =
            {
                $"₺ {money}",
                $"%{Mathf.RoundToInt(energy)}",
                pizzas.ToString(),
                taxis.ToString(),
                painted.ToString(),
                litter.ToString(),
                timeFormatted
            };

            for (int i = 0; i < 7; i++)
            {
                if (statNameTexts != null && i < statNameTexts.Length && statNameTexts[i] != null)
                {
                    statNameTexts[i].text = LocalizationService.Tr(nameKeys[i], nameFallbacks[i]);
                }
                if (statValueTexts != null && i < statValueTexts.Length && statValueTexts[i] != null)
                {
                    statValueTexts[i].text = values[i];
                }
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
