using System.Collections;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Jobs;
using OurWorld.Missions;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Controls the entire GRIDROAM HUD:
    /// - Calm modern dark translucent styling with 0.2 s CanvasGroup panel transitions
    /// - Top-left Objective Card (fades to 50% alpha after 10 s unchanged) + separate "İŞ: ..." job line & Cancel Job
    /// - Top-right Clock ("14:35"), Money ("₺ 250") with +/- pop animation, Energy bar & Tired badge
    /// - Bottom-centre/right "E · Otur" Interact pill with 0.15 s fade/slide
    /// - Circular radial Speedometer + gear badge + Fuel bar while driving
    /// - Ergonomic Sprint, Handbrake, and Phone controls
    /// </summary>
    public class GameHUDController : MonoBehaviour
    {
        [Header("Mode Containers")]
        [SerializeField] private GameObject onFootControlsRoot;
        [SerializeField] private GameObject drivingControlsRoot;

        [Header("On-Foot & Shared Controls")]
        [SerializeField] private Button interactButton;
        [SerializeField] private Text interactButtonLabel;
        [SerializeField] private HoldButton sprintButton;
        [SerializeField] private Text sprintButtonLabel;
        [SerializeField] private Button phoneButton;
        [SerializeField] private Text phoneButtonLabel;

        [Header("Driving Controls")]
        [SerializeField] private HoldButton steerLeftButton;
        [SerializeField] private HoldButton steerRightButton;
        [SerializeField] private HoldButton gasButton;
        [SerializeField] private HoldButton brakeButton;
        [SerializeField] private HoldButton handbrakeButton;
        [SerializeField] private Text handbrakeLabel;
        [SerializeField] private Button exitCarButton;
        [SerializeField] private Text exitCarLabel;
        [SerializeField] private Text speedText;
        [SerializeField] private Text gearText;
        [SerializeField] private Image speedRingFillImage;
        [SerializeField] private Image fuelFillImage;

        [Header("Mission & Side-Job HUD")]
        [SerializeField] private CanvasGroup objectiveCardCanvasGroup;
        [SerializeField] private Text missionTitleText;
        [SerializeField] private Text missionObjectiveText;
        [SerializeField] private Text jobObjectiveText;
        [SerializeField] private Button cancelJobButton;
        [SerializeField] private Text cancelJobButtonLabel;
        [SerializeField] private GameObject timerContainer;
        [SerializeField] private Text timerText;
        [SerializeField] private GameObject tailWarningContainer;
        [SerializeField] private Text tailWarningText;
        [SerializeField] private Image tailWarningBackground;
        [SerializeField] private Text statusToastText;
        [SerializeField] private Text editorHintText;

        [Header("Economy, Clock & Energy")]
        [SerializeField] private Text clockText;
        [SerializeField] private RectTransform moneyPillRect;
        [SerializeField] private Text moneyText;
        [SerializeField] private Text moneyDeltaPopText;
        [SerializeField] private Image energyFillImage;
        [SerializeField] private GameObject tiredBadgeObject;
        [SerializeField] private Text tiredBadgeText;

        [Header("Dialogue Overlay")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private Text dialogueSpeakerText;
        [SerializeField] private Text dialogueBodyText;
        [SerializeField] private Button dialogueContinueButton;
        [SerializeField] private Text dialogueContinueLabel;

        [Header("Mission Failed / Complete Overlays")]
        [SerializeField] private GameObject failedPanel;
        [SerializeField] private Text failedTitleText;
        [SerializeField] private Text failedReasonText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Text retryButtonLabel;
        [SerializeField] private GameObject bannerPanel;
        [SerializeField] private Text bannerText;

        [Header("Top-Right Utility & Telemetry")]
        [SerializeField] private Button graphicsTierButton;
        [SerializeField] private Text graphicsTierLabel;
        [SerializeField] private Button resetSaveButton;
        [SerializeField] private Text resetSaveLabel;
        [SerializeField] private GameObject telemetryPanel;
        [SerializeField] private Text telemetryText;

        private static readonly Color OffWhiteColor = OurWorldUiKit.TextPrimary;
        private static readonly Color AccentTealColor = OurWorldUiKit.AccentTeal;
        private static readonly Color DangerRedColor = OurWorldUiKit.AccentCoral;

        private Coroutine _toastRoutine;
        private Coroutine _bannerRoutine;
        private Coroutine _moneyPopRoutine;
        private Coroutine _dlgAnimRoutine;
        private Coroutine _failAnimRoutine;
        private Coroutine _hudFadeRoutine;
        private CanvasGroup _hudRootGroup;
        private Text _tennisScoreText;
        private int _tennisHudRevision = -1;
        private GameObject _sportMeterRoot;
        private GameObject _sportPrimaryTrackObj;
        private RectTransform _sportPrimaryFillRect;
        private Image _sportPrimaryFillImg;
        private RectTransform _sportSweetSpotRect;
        private Text _sportPrimaryLabel;
        private GameObject _sportStaminaTrackObj;
        private RectTransform _sportStaminaFillRect;
        private Image _sportStaminaFillImg;
        private GameObject _sportCalloutObj;
        private Image _sportCalloutBg;
        private Text _sportCalloutText;
        private Text _attackBtnLabel;
        private Text _jumpBtnLabel;
        private GameObject _jumpBtnObject;
        private Image _healthFillImage;
        private Text _healthLabelText;
        private Image _hungerFillImage;
        private Text _hungerLabelText;
        private Button _backpackHudButton;
        private Image _backpackHudBg;
        private Text _backpackHudLabel;
        private Image _damageVignetteImage;
        private Texture2D _damageVignetteTex;
        private Sprite _damageVignetteSprite;
        private float _damageFlashAlpha;
        private float _currentHealth = 100f;
        private float _currentHunger = 88f;

        private const string PrefLargeSubtitlesKey = "ow_subtitle_large";
        private int _baseDialogueSpeakerSize;
        private int _baseDialogueBodySize;
        private int _baseToastSize;

        public static bool LargeSubtitles
        {
            get => PlayerPrefs.GetInt(PrefLargeSubtitlesKey, 0) != 0;
            private set
            {
                PlayerPrefs.SetInt(PrefLargeSubtitlesKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public bool IsTelemetryVisible => telemetryPanel != null && telemetryPanel.activeSelf;

        public void SetLargeSubtitles(bool large)
        {
            LargeSubtitles = large;
            ApplySubtitleSize();
        }

        public void ApplySubtitleSize()
        {
            bool large = LargeSubtitles;
            if (dialogueSpeakerText != null)
            {
                if (_baseDialogueSpeakerSize <= 0) _baseDialogueSpeakerSize = Mathf.Max(18, dialogueSpeakerText.fontSize);
                dialogueSpeakerText.fontSize = large ? Mathf.RoundToInt(_baseDialogueSpeakerSize * 1.22f) : _baseDialogueSpeakerSize;
            }
            if (dialogueBodyText != null)
            {
                if (_baseDialogueBodySize <= 0) _baseDialogueBodySize = Mathf.Max(18, dialogueBodyText.fontSize);
                int target = large ? Mathf.RoundToInt(_baseDialogueBodySize * 1.26f) : _baseDialogueBodySize;
                dialogueBodyText.fontSize = target;
                dialogueBodyText.resizeTextForBestFit = true;
                dialogueBodyText.resizeTextMinSize = Mathf.Max(14, _baseDialogueBodySize - 4);
                dialogueBodyText.resizeTextMaxSize = target;
            }
            if (statusToastText != null)
            {
                if (_baseToastSize <= 0) _baseToastSize = Mathf.Max(16, statusToastText.fontSize);
                int target = large ? Mathf.RoundToInt(_baseToastSize * 1.20f) : _baseToastSize;
                statusToastText.fontSize = target;
            }
        }

        public void SetTelemetryVisible(bool visible)
        {
            if (telemetryPanel != null)
            {
                telemetryPanel.SetActive(visible);
            }
        }

        public void SetLegacyTopRightUtilityVisible(bool visible)
        {
            if (graphicsTierButton != null) graphicsTierButton.gameObject.SetActive(visible);
            if (resetSaveButton != null) resetSaveButton.gameObject.SetActive(visible);
        }

        public void SetInteractBaseY(float baseY)
        {
            _interactBaseY = baseY;
        }

        public void SetHudVisible(bool visible, float fadeSeconds = 0.25f)
        {
            if (_hudRootGroup == null)
            {
                Transform safeArea = transform.Find("SafeAreaRoot");
                GameObject targetGo = safeArea != null ? safeArea.gameObject : gameObject;
                _hudRootGroup = EnsureCanvasGroup(targetGo);
            }

            if (_hudFadeRoutine != null)
            {
                StopCoroutine(_hudFadeRoutine);
                _hudFadeRoutine = null;
            }

            if (fadeSeconds <= 0.01f || !isActiveAndEnabled)
            {
                _hudRootGroup.alpha = visible ? 1f : 0f;
                _hudRootGroup.interactable = visible;
                _hudRootGroup.blocksRaycasts = visible;
                return;
            }

            _hudFadeRoutine = StartCoroutine(FadeHudRoutine(visible, fadeSeconds));
        }

        private IEnumerator FadeHudRoutine(bool visible, float duration)
        {
            if (_hudRootGroup == null) yield break;
            if (!visible)
            {
                _hudRootGroup.interactable = false;
                _hudRootGroup.blocksRaycasts = false;
            }

            float start = _hudRootGroup.alpha;
            float end = visible ? 1f : 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _hudRootGroup.alpha = Mathf.Lerp(start, end, t);
                yield return null;
            }

            _hudRootGroup.alpha = end;
            _hudRootGroup.interactable = visible;
            _hudRootGroup.blocksRaycasts = visible;
            _hudFadeRoutine = null;
        }

        // Interact prompt 0.15 s fade/slide state
        private CanvasGroup _interactCanvasGroup;
        private RectTransform _interactRect;
        private float _interactBaseY = 64f;
        private bool _interactDesiredVisible;
        private float _interactAnimT;

        // Objective card 10 s idle fade state
        private float _objectiveUnchangedSeconds;
        private string _lastObjectiveSnapshot = string.Empty;

        public void WireReferences(
            GameObject onFootRoot,
            GameObject drivingRoot,
            Button interactBtn,
            Text interactLbl,
            HoldButton steerL,
            HoldButton steerR,
            HoldButton gasBtn,
            HoldButton brakeBtn,
            Button exitBtn,
            Text exitLbl,
            Text speedLbl,
            Text titleLbl,
            Text objectiveLbl,
            GameObject timerBox,
            Text timerLbl,
            GameObject tailBox,
            Text tailLbl,
            Image tailBg,
            Text toastLbl,
            Text hintLbl,
            GameObject dlgPanel,
            Text dlgSpeaker,
            Text dlgBody,
            Button dlgContinue,
            Text dlgContinueLbl,
            GameObject failBox,
            Text failTitle,
            Text failReason,
            Button retryBtn,
            Text retryLbl,
            GameObject bnrPanel,
            Text bnrText,
            Button gfxBtn,
            Text gfxLbl,
            Button resetBtn,
            Text resetLbl,
            GameObject perfPanel,
            Text perfLbl)
        {
            onFootControlsRoot = onFootRoot;
            drivingControlsRoot = drivingRoot;
            interactButton = interactBtn;
            interactButtonLabel = interactLbl;
            steerLeftButton = steerL;
            steerRightButton = steerR;
            gasButton = gasBtn;
            brakeButton = brakeBtn;
            exitCarButton = exitBtn;
            exitCarLabel = exitLbl;
            speedText = speedLbl;
            missionTitleText = titleLbl;
            missionObjectiveText = objectiveLbl;
            timerContainer = timerBox;
            timerText = timerLbl;
            tailWarningContainer = tailBox;
            tailWarningText = tailLbl;
            tailWarningBackground = tailBg;
            statusToastText = toastLbl;
            editorHintText = hintLbl;
            dialoguePanel = dlgPanel;
            dialogueSpeakerText = dlgSpeaker;
            dialogueBodyText = dlgBody;
            dialogueContinueButton = dlgContinue;
            dialogueContinueLabel = dlgContinueLbl;
            failedPanel = failBox;
            failedTitleText = failTitle;
            failedReasonText = failReason;
            retryButton = retryBtn;
            retryButtonLabel = retryLbl;
            bannerPanel = bnrPanel;
            bannerText = bnrText;
            graphicsTierButton = gfxBtn;
            graphicsTierLabel = gfxLbl;
            resetSaveButton = resetBtn;
            resetSaveLabel = resetLbl;
            telemetryPanel = perfPanel;
            telemetryText = perfLbl;
        }

        public void WireOpenWorldReferences(
            CanvasGroup objCardGroup,
            Text jobLineLbl,
            Button btnCancelJob,
            Text lblCancelJob,
            Text clockLbl,
            RectTransform moneyRect,
            Text moneyLbl,
            Text moneyDeltaLbl,
            Image energyFill,
            GameObject tiredBadge,
            Text tiredLbl,
            HoldButton sprintBtn,
            Text sprintLbl,
            HoldButton handbrakeBtn,
            Text handbrakeLbl,
            Button btnPhone,
            Text lblPhone,
            Image speedRingImg,
            Text gearLbl,
            Image fuelFill)
        {
            objectiveCardCanvasGroup = objCardGroup;
            jobObjectiveText = jobLineLbl;
            cancelJobButton = btnCancelJob;
            cancelJobButtonLabel = lblCancelJob;
            clockText = clockLbl;
            moneyPillRect = moneyRect;
            moneyText = moneyLbl;
            moneyDeltaPopText = moneyDeltaLbl;
            energyFillImage = energyFill;
            tiredBadgeObject = tiredBadge;
            tiredBadgeText = tiredLbl;
            sprintButton = sprintBtn;
            sprintButtonLabel = sprintLbl;
            handbrakeButton = handbrakeBtn;
            handbrakeLabel = handbrakeLbl;
            phoneButton = btnPhone;
            phoneButtonLabel = lblPhone;
            speedRingFillImage = speedRingImg;
            gearText = gearLbl;
            fuelFillImage = fuelFill;
        }

        private void Start()
        {
            if (interactButton != null)
            {
                _interactRect = interactButton.GetComponent<RectTransform>();
                if (_interactRect != null)
                {
                    _interactBaseY = _interactRect.anchoredPosition.y;
                }
                _interactCanvasGroup = interactButton.GetComponent<CanvasGroup>();
                if (_interactCanvasGroup == null)
                {
                    _interactCanvasGroup = interactButton.gameObject.AddComponent<CanvasGroup>();
                }
            }

            BuildRuntimeHealthAndDamageHud();
            BindButtons();
            SubscribeEvents();
            RefreshStaticLabels();
            SyncInitialState();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (_damageVignetteSprite != null) Destroy(_damageVignetteSprite);
            if (_damageVignetteTex != null) Destroy(_damageVignetteTex);
        }

        private void BindButtons()
        {
            if (interactButton != null)
            {
                interactButton.onClick.AddListener(() =>
                {
                    if (InputHub.Instance != null) InputHub.Instance.TriggerTouchInteract();
                });
            }

            if (exitCarButton != null)
            {
                exitCarButton.onClick.AddListener(() =>
                {
                    if (InputHub.Instance != null) InputHub.Instance.TriggerTouchExitVehicle();
                });
            }

            if (phoneButton != null)
            {
                phoneButton.onClick.AddListener(() =>
                {
                    if (InputHub.Instance != null) InputHub.Instance.TriggerTouchPhone();
                });
            }

            if (cancelJobButton != null)
            {
                cancelJobButton.onClick.AddListener(() =>
                {
                    if (JobManager.Instance != null) JobManager.Instance.CancelActiveJob(silent: false);
                });
            }

            if (dialogueContinueButton != null)
            {
                dialogueContinueButton.onClick.AddListener(() =>
                {
                    if (InputHub.Instance != null) InputHub.Instance.TriggerTouchContinueDialogue();
                });
            }

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(() =>
                {
                    AnimateOverlayPanel(failedPanel, opening: false, ref _failAnimRoutine);
                    if (MissionManager.Instance != null) MissionManager.Instance.RetryCurrentMission();
                });
            }

            if (graphicsTierButton != null)
            {
                graphicsTierButton.onClick.AddListener(() =>
                {
                    if (GraphicsProfileManager.Instance != null) GraphicsProfileManager.Instance.CycleTier();
                });
            }

            if (resetSaveButton != null)
            {
                resetSaveButton.onClick.AddListener(() =>
                {
                    if (failedPanel != null) failedPanel.SetActive(false);
                    if (JobManager.Instance != null && JobManager.Instance.HasActiveJob)
                    {
                        JobManager.Instance.CancelActiveJob(silent: true);
                    }
                    if (MissionManager.Instance != null) MissionManager.Instance.ResetProgressAndRestart();
                });
            }

            if (telemetryPanel != null)
            {
                Button perfTap = telemetryPanel.GetComponent<Button>();
                if (perfTap != null)
                {
                    perfTap.onClick.AddListener(HandleExportPerfReport);
                }
            }

            if (steerLeftButton != null) steerLeftButton.OnStateChanged += pressed => { if (InputHub.Instance != null) InputHub.Instance.SetTouchSteerLeft(pressed); };
            if (steerRightButton != null) steerRightButton.OnStateChanged += pressed => { if (InputHub.Instance != null) InputHub.Instance.SetTouchSteerRight(pressed); };
            if (gasButton != null) gasButton.OnStateChanged += pressed => { if (InputHub.Instance != null) InputHub.Instance.SetTouchGas(pressed); };
            if (brakeButton != null) brakeButton.OnStateChanged += pressed => { if (InputHub.Instance != null) InputHub.Instance.SetTouchBrake(pressed); };
            if (handbrakeButton != null) handbrakeButton.OnStateChanged += pressed => { if (InputHub.Instance != null) InputHub.Instance.SetTouchHandbrake(pressed); };
            if (sprintButton != null) sprintButton.OnStateChanged += pressed => { if (InputHub.Instance != null) InputHub.Instance.SetTouchSprint(pressed); };
        }

        private void SubscribeEvents()
        {
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnModeChanged += HandleModeChanged;
                PlayerModeController.Instance.OnInteractableChanged += HandleInteractableChanged;
                PlayerModeController.Instance.OnStatusMessage += ShowToast;
            }

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.OnObjectiveChanged += HandleObjectiveChanged;
                MissionManager.Instance.OnDialogueLineShown += HandleDialogueLineShown;
                MissionManager.Instance.OnDialogueClosed += HandleDialogueClosed;
                MissionManager.Instance.OnMissionCompletedBanner += HandleMissionCompletedBanner;
                MissionManager.Instance.OnMissionFailed += HandleMissionFailed;
                MissionManager.Instance.OnTailStatusChanged += HandleTailStatusChanged;
            }

            if (JobManager.Instance != null)
            {
                JobManager.Instance.OnJobStateChanged += HandleJobStateChanged;
            }

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnMoneyChanged += HandleMoneyChanged;
            }

            if (PlayerNeedsController.Instance != null)
            {
                PlayerNeedsController.Instance.OnEnergyChanged += HandleEnergyChanged;
                PlayerNeedsController.Instance.OnHealthChanged += HandleHealthChanged;
                PlayerNeedsController.Instance.OnHungerChanged += HandleHungerChanged;
            }

            PlayerBackpackInventory.EnsureInstance().OnInventoryChanged += RefreshBackpackHudButton;

            if (GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged += HandleGraphicsTierChanged;
            }

            if (PerformanceMonitor.Instance != null)
            {
                PerformanceMonitor.Instance.OnTelemetryUpdated += HandleTelemetryUpdated;
            }

            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnTogglePerfOverlayPressed += ToggleTelemetryPanel;
                InputHub.Instance.OnCycleGraphicsPressed += HandleCycleGraphicsShortcut;
                InputHub.Instance.OnExportPerfReportPressed += HandleExportPerfReport;
            }

            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged += HandleLanguageChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnModeChanged -= HandleModeChanged;
                PlayerModeController.Instance.OnInteractableChanged -= HandleInteractableChanged;
                PlayerModeController.Instance.OnStatusMessage -= ShowToast;
            }

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.OnObjectiveChanged -= HandleObjectiveChanged;
                MissionManager.Instance.OnDialogueLineShown -= HandleDialogueLineShown;
                MissionManager.Instance.OnDialogueClosed -= HandleDialogueClosed;
                MissionManager.Instance.OnMissionCompletedBanner -= HandleMissionCompletedBanner;
                MissionManager.Instance.OnMissionFailed -= HandleMissionFailed;
                MissionManager.Instance.OnTailStatusChanged -= HandleTailStatusChanged;
            }

            if (JobManager.Instance != null)
            {
                JobManager.Instance.OnJobStateChanged -= HandleJobStateChanged;
            }

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnMoneyChanged -= HandleMoneyChanged;
            }

            if (PlayerNeedsController.Instance != null)
            {
                PlayerNeedsController.Instance.OnEnergyChanged -= HandleEnergyChanged;
                PlayerNeedsController.Instance.OnHealthChanged -= HandleHealthChanged;
                PlayerNeedsController.Instance.OnHungerChanged -= HandleHungerChanged;
            }

            if (PlayerBackpackInventory.Instance != null)
            {
                PlayerBackpackInventory.Instance.OnInventoryChanged -= RefreshBackpackHudButton;
            }

            if (GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged -= HandleGraphicsTierChanged;
            }

            if (PerformanceMonitor.Instance != null)
            {
                PerformanceMonitor.Instance.OnTelemetryUpdated -= HandleTelemetryUpdated;
            }

            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnTogglePerfOverlayPressed -= ToggleTelemetryPanel;
                InputHub.Instance.OnCycleGraphicsPressed -= HandleCycleGraphicsShortcut;
                InputHub.Instance.OnExportPerfReportPressed -= HandleExportPerfReport;
            }

            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged -= HandleLanguageChanged;
            }
        }

        private void HandleCycleGraphicsShortcut()
        {
            if (GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.CycleTier();
            }
        }

        private void HandleLanguageChanged(string _)
        {
            _tennisHudRevision = -1;
            RefreshStaticLabels();
            if (MissionManager.Instance != null)
            {
                HandleObjectiveChanged(MissionManager.Instance.ActiveMission, MissionManager.Instance.CurrentStep);
            }
            HandleJobStateChanged();
        }

        private void SyncInitialState()
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (failedPanel != null) failedPanel.SetActive(false);
            if (bannerPanel != null) bannerPanel.SetActive(false);
            if (timerContainer != null) timerContainer.SetActive(false);
            if (tailWarningContainer != null) tailWarningContainer.SetActive(false);
            if (statusToastText != null) statusToastText.gameObject.SetActive(false);
            if (moneyDeltaPopText != null) moneyDeltaPopText.gameObject.SetActive(false);
            if (telemetryPanel != null) telemetryPanel.SetActive(false);

            if (SaveManager.Instance != null && moneyText != null)
            {
                moneyText.text = $"₺ {SaveManager.Instance.Data.money}";
            }

            if (PlayerNeedsController.Instance != null)
            {
                HandleEnergyChanged(PlayerNeedsController.Instance.Energy, PlayerNeedsController.Instance.IsTired);
                HandleHealthChanged(PlayerNeedsController.Instance.Health, 0f);
                HandleHungerChanged(PlayerNeedsController.Instance.Hunger, PlayerNeedsController.Instance.IsHungry);
            }
            RefreshBackpackHudButton();

            if (PlayerModeController.Instance != null)
            {
                HandleModeChanged(PlayerModeController.Instance.CurrentMode);
                HandleInteractableChanged(PlayerModeController.Instance.NearestInteractable);
            }

            if (MissionManager.Instance != null)
            {
                HandleObjectiveChanged(MissionManager.Instance.ActiveMission, MissionManager.Instance.CurrentStep);
            }

            HandleJobStateChanged();

            if (GraphicsProfileManager.Instance != null)
            {
                HandleGraphicsTierChanged(GraphicsProfileManager.Instance.CurrentTier);
            }

            if (editorHintText != null)
            {
#if UNITY_EDITOR
                editorHintText.gameObject.SetActive(true);
#else
                editorHintText.gameObject.SetActive(false);
#endif
            }
        }

        private void RefreshStaticLabels()
        {
            if (exitCarLabel != null)
            {
                bool cycling = PlayerModeController.Instance != null &&
                               PlayerModeController.Instance.CurrentMode == ControlMode.Cycling;
                exitCarLabel.text = cycling ? LocalizationService.Tr(RidingMotorcycle ? "ui.btn.exit_motorcycle" : "ui.btn.exit_bike") :
                    LocalizationService.Tr("ui.btn.exit_car", "İn");
            }
            if (dialogueContinueLabel != null) dialogueContinueLabel.text = LocalizationService.Tr("ui.btn.continue", "Devam");
            if (failedTitleText != null) failedTitleText.text = LocalizationService.Tr("ui.hud.mission_failed", "Görev Başarısız");
            if (retryButtonLabel != null) retryButtonLabel.text = LocalizationService.Tr("ui.btn.retry", "Tekrar Dene");
            if (resetSaveLabel != null) resetSaveLabel.text = LocalizationService.Tr("ui.btn.reset_save", "Sıfırla");
            if (sprintButtonLabel != null) sprintButtonLabel.text = LocalizationService.Tr("ui.btn.sprint", "Koş");
            if (_hungerLabelText != null) _hungerLabelText.text = LocalizationService.IsEnglish ? "Food" : "Tok";
            RefreshBackpackHudButton();
            if (handbrakeLabel != null) handbrakeLabel.text = LocalizationService.Tr("ui.btn.handbrake", "El Freni");
            if (phoneButtonLabel != null)
            {
                string rawPhone = LocalizationService.Tr("ui.btn.phone", "Rehber (M)");
                phoneButtonLabel.text = Application.isMobilePlatform
                    ? rawPhone.Replace(" (M)", string.Empty)
                    : rawPhone;
            }
            if (cancelJobButtonLabel != null) cancelJobButtonLabel.text = LocalizationService.Tr("ui.btn.cancel", "İptal");
            if (tiredBadgeText != null) tiredBadgeText.text = LocalizationService.Tr("ui.needs.tired_badge", "Yorgun");

            if (GraphicsProfileManager.Instance != null)
            {
                HandleGraphicsTierChanged(GraphicsProfileManager.Instance.CurrentTier);
            }

            if (PlayerModeController.Instance != null)
            {
                UpdateInteractPromptLabel(PlayerModeController.Instance.NearestInteractable);
            }
        }

        private void Update()
        {
            float unscaledDt = Time.unscaledDeltaTime;

            // 1. Game Clock
            if (clockText != null && DayNightCycle.Instance != null)
            {
                clockText.text = DayNightCycle.Instance.GetFormattedTime();
            }

            // 1b. Red damage vignette decay + low-HP heartbeat pulse
            if (_damageVignetteImage != null)
            {
                if (_damageFlashAlpha > 0f)
                {
                    _damageFlashAlpha = Mathf.Max(0f, _damageFlashAlpha - unscaledDt * 1.45f);
                }
                float lowPulse = _currentHealth <= 30f && _currentHealth > 0f
                    ? (0.14f + Mathf.Sin(Time.unscaledTime * 5.2f) * 0.08f) * Mathf.InverseLerp(30f, 5f, _currentHealth)
                    : 0f;
                float finalAlpha = Mathf.Clamp01(Mathf.Max(_damageFlashAlpha, lowPulse));
                bool active = finalAlpha > 0.005f;
                if (_damageVignetteImage.gameObject.activeSelf != active)
                {
                    _damageVignetteImage.gameObject.SetActive(active);
                }
                if (active)
                {
                    _damageVignetteImage.color = new Color(0.86f, 0.08f, 0.10f, finalAlpha);
                }
            }

            // 2. Countdown Timer (Story Mission timer or Side-Job timer)
            MissionManager mm = MissionManager.Instance;
            JobManager jm = JobManager.Instance;

            bool showMissionTimer = mm != null && mm.HasActiveTimer && !mm.IsFailed;
            bool showJobTimer = !showMissionTimer && jm != null && jm.HasJobTimer;
            bool showAnyTimer = showMissionTimer || showJobTimer;

            if (timerContainer != null && timerText != null)
            {
                if (timerContainer.activeSelf != showAnyTimer)
                {
                    timerContainer.SetActive(showAnyTimer);
                }

                if (showAnyTimer)
                {
                    float rem = showMissionTimer ? mm.RemainingTime : jm.JobRemainingTime;
                    int totalSec = Mathf.CeilToInt(Mathf.Max(0f, rem));
                    int mins = totalSec / 60;
                    int secs = totalSec % 60;
                    timerText.text = $"{mins:00}:{secs:00}";
                    timerText.color = rem <= 15f ? DangerRedColor : OffWhiteColor;
                }
            }

            // 3. Circular Speedometer + Gear + Fuel Bar while driving
            PlayerModeController player = PlayerModeController.Instance;
            if (player != null && player.CurrentBicycle != null)
            {
                float kmh = player.CurrentBicycle.SpeedKmh;
                if (speedText != null) speedText.text = Mathf.RoundToInt(kmh).ToString();
                if (speedRingFillImage != null) speedRingFillImage.fillAmount = Mathf.Clamp01(kmh / player.CurrentBicycle.MaxSpeedKmh) * 0.75f;
            }
            else if (player != null && player.IsDriving && player.CurrentVehicle != null)
            {
                var veh = player.CurrentVehicle;
                float kmhFloat = veh.SpeedKmh;
                int kmh = Mathf.RoundToInt(kmhFloat);
                string gear = veh.CurrentGear < 0 ? "R" : (veh.CurrentGear == 0 ? "N" : $"D{veh.CurrentGear}");

                if (speedText != null)
                {
                    if (gearText != null)
                    {
                        speedText.text = kmh.ToString();
                        gearText.text = gear;
                    }
                    else
                    {
                        speedText.text = $"{kmh} {LocalizationService.Tr("ui.hud.speed_unit", "km/s")}  {gear}";
                    }
                }

                if (speedRingFillImage != null)
                {
                    float targetFill = Mathf.Clamp01(kmhFloat / 150f) * 0.75f;
                    speedRingFillImage.fillAmount = Mathf.Lerp(speedRingFillImage.fillAmount, targetFill, 1f - Mathf.Exp(-14f * Time.deltaTime));
                }

                if (fuelFillImage != null)
                {
                    float f = Mathf.Clamp01(veh.Fuel);
                    fuelFillImage.fillAmount = f;
                    fuelFillImage.color = f < 0.18f ? DangerRedColor : AccentTealColor;
                }
            }

            if (player != null && _tennisScoreText == null && player.FootController != null && player.FootController.IsSwimming)
            {
                SetTennisScoreVisible(true);
                SetTennisScoreVisible(false);
            }

            if (player != null && _tennisScoreText != null)
            {
                if (player.CurrentTennisCourt != null)
                {
                    TennisCourt court = player.CurrentTennisCourt;
                    if (_tennisHudRevision != court.HudRevision)
                    {
                        _tennisScoreText.text = court.HudText();
                        _tennisScoreText.color = court.AcceptsHit
                            ? new Color(0.54f, 0.98f, 0.90f, 1f)
                            : new Color(0.95f, 0.97f, 1.0f, 1f);
                        CacheSportActionButtons();
                        if (_attackBtnLabel != null) _attackBtnLabel.text = court.PrimaryButtonLabel();
                        if (_jumpBtnObject != null) _jumpBtnObject.SetActive(court.HasSecondaryAction);
                        if (_jumpBtnLabel != null && court.HasSecondaryAction) _jumpBtnLabel.text = court.SecondaryButtonLabel();
                        _tennisHudRevision = court.HudRevision;
                    }
                    UpdateSportMeterVisuals(court.GetHudMeterState());
                }
                else if (player.CurrentArenaSport != null)
                {
                    IPlayableSportArena arena = player.CurrentArenaSport;
                    if (_tennisHudRevision != arena.HudRevision)
                    {
                        _tennisScoreText.text = arena.HudText();
                        _tennisScoreText.color = arena.AcceptsPrimaryAction
                            ? new Color(0.54f, 0.98f, 0.90f, 1f)
                            : new Color(0.95f, 0.97f, 1.0f, 1f);
                        CacheSportActionButtons();
                        if (_attackBtnLabel != null) _attackBtnLabel.text = arena.PrimaryButtonLabel();
                        if (_jumpBtnObject != null) _jumpBtnObject.SetActive(arena.HasSecondaryAction);
                        if (_jumpBtnLabel != null && arena.HasSecondaryAction) _jumpBtnLabel.text = arena.SecondaryButtonLabel();
                        _tennisHudRevision = arena.HudRevision;
                    }
                    UpdateSportMeterVisuals(arena.GetHudMeterState());
                }
                else if (player.FootController != null && player.FootController.IsSwimming)
                {
                    bool lowOxy = player.FootController.SwimOxygen01 < 0.30f;
                    var swimState = new SportHudMeterState
                    {
                        ShowPrimaryBar = player.FootController.IsUnderwater || player.FootController.SwimOxygen01 < 0.99f,
                        PrimaryFill01 = player.FootController.SwimOxygen01,
                        PrimaryColor = lowOxy ? DangerRedColor : new Color(0.24f, 0.88f, 0.98f),
                        PrimaryLabel = lowOxy ? "NEFES AZALIYOR · YÜZEYE ÇIK!" : "OKSİJEN (Dalış: Atla Basılı Tut · Stil: Vur)",
                        ShowSweetSpot = false,
                        ShowStaminaBar = false
                    };
                    UpdateSportMeterVisuals(swimState);
                }
                else if (_sportMeterRoot != null && _sportMeterRoot.activeSelf)
                {
                    _sportMeterRoot.SetActive(false);
                }
            }

            // 4. Interact prompt pill 0.15 s fade + vertical slide
            UpdateInteractPillAnimation(unscaledDt);

            // 5. Objective card 10 s idle fade to 50% alpha
            UpdateObjectiveCardIdleFade(unscaledDt);
        }

        private void UpdateInteractPillAnimation(float dt)
        {
            if (interactButton == null || _interactCanvasGroup == null || dt <= 0f)
            {
                return;
            }

            float target = _interactDesiredVisible ? 1f : 0f;
            if (Mathf.Approximately(_interactAnimT, target))
            {
                if (!_interactDesiredVisible && interactButton.gameObject.activeSelf)
                {
                    interactButton.gameObject.SetActive(false);
                }
                return;
            }

            if (_interactDesiredVisible && !interactButton.gameObject.activeSelf)
            {
                interactButton.gameObject.SetActive(true);
            }

            _interactAnimT = Mathf.MoveTowards(_interactAnimT, target, dt / 0.15f);
            float smooth = Mathf.SmoothStep(0f, 1f, _interactAnimT);
            _interactCanvasGroup.alpha = smooth;
            _interactCanvasGroup.blocksRaycasts = _interactDesiredVisible;

            if (_interactRect != null)
            {
                Vector2 pos = _interactRect.anchoredPosition;
                pos.y = Mathf.Lerp(_interactBaseY - 14f, _interactBaseY, smooth);
                _interactRect.anchoredPosition = pos;
            }

            if (!_interactDesiredVisible && _interactAnimT <= 0.001f)
            {
                interactButton.gameObject.SetActive(false);
            }
        }

        private void UpdateObjectiveCardIdleFade(float dt)
        {
            if (objectiveCardCanvasGroup == null || dt <= 0f) return;

            string mTitle = missionTitleText != null ? missionTitleText.text : string.Empty;
            string mObj = missionObjectiveText != null ? missionObjectiveText.text : string.Empty;
            string jObj = (jobObjectiveText != null && jobObjectiveText.gameObject.activeSelf) ? jobObjectiveText.text : string.Empty;
            string snapshot = string.Concat(mTitle, "|", mObj, "|", jObj);

            if (!string.Equals(snapshot, _lastObjectiveSnapshot, System.StringComparison.Ordinal))
            {
                _lastObjectiveSnapshot = snapshot;
                _objectiveUnchangedSeconds = 0f;
            }
            else
            {
                _objectiveUnchangedSeconds += dt;
            }

            float targetAlpha = _objectiveUnchangedSeconds >= 10f ? 0.50f : 1.0f;
            objectiveCardCanvasGroup.alpha = Mathf.Lerp(objectiveCardCanvasGroup.alpha, targetAlpha, 1f - Mathf.Exp(-4f * dt));
        }

        private void HandleMoneyChanged(int newMoney)
        {
            if (moneyText != null)
            {
                moneyText.text = $"₺ {newMoney}";
            }

            int delta = SaveManager.Instance != null ? SaveManager.Instance.LastMoneyDelta : 0;
            if (delta != 0 && isActiveAndEnabled)
            {
                if (_moneyPopRoutine != null) StopCoroutine(_moneyPopRoutine);
                _moneyPopRoutine = StartCoroutine(MoneyPopRoutine(delta));
            }
        }

        private IEnumerator MoneyPopRoutine(int delta)
        {
            if (moneyDeltaPopText != null)
            {
                moneyDeltaPopText.text = delta > 0 ? $"+{delta} ₺" : $"{delta} ₺";
                moneyDeltaPopText.color = delta > 0 ? AccentTealColor : DangerRedColor;
                moneyDeltaPopText.gameObject.SetActive(true);
            }

            RectTransform deltaRt = moneyDeltaPopText != null ? moneyDeltaPopText.rectTransform : null;
            Vector2 startAnchored = new Vector2(-28f, -74f);
            const float duration = 0.85f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (moneyPillRect != null)
                {
                    float punch = t < 0.28f ? Mathf.Sin((t / 0.28f) * Mathf.PI) * 0.14f : 0f;
                    moneyPillRect.localScale = new Vector3(1f + punch, 1f + punch, 1f);
                }

                if (deltaRt != null && moneyDeltaPopText != null)
                {
                    deltaRt.anchoredPosition = startAnchored + new Vector2(0f, 18f * t);
                    Color c = moneyDeltaPopText.color;
                    c.a = 1f - Mathf.SmoothStep(0.35f, 1f, t);
                    moneyDeltaPopText.color = c;
                }

                yield return null;
            }

            if (moneyPillRect != null) moneyPillRect.localScale = Vector3.one;
            if (moneyDeltaPopText != null) moneyDeltaPopText.gameObject.SetActive(false);
            _moneyPopRoutine = null;
        }

        private void HandleEnergyChanged(float energy, bool isTired)
        {
            if (energyFillImage != null)
            {
                energyFillImage.fillAmount = Mathf.Clamp01(energy / 100f);
                energyFillImage.color = isTired ? DangerRedColor : AccentTealColor;
            }

            if (tiredBadgeObject != null)
            {
                tiredBadgeObject.SetActive(isTired);
            }
        }

        private void HandleHealthChanged(float health, float delta)
        {
            _currentHealth = Mathf.Clamp(health, 0f, 100f);
            if (_healthFillImage != null)
            {
                _healthFillImage.fillAmount = Mathf.Clamp01(_currentHealth / 100f);
                _healthFillImage.color = _currentHealth <= 30f
                    ? new Color(1.00f, 0.20f, 0.22f, 1f)
                    : new Color(0.90f, 0.24f, 0.28f, 1f);
            }
            if (_healthLabelText != null)
            {
                _healthLabelText.text = $"♥ {Mathf.CeilToInt(_currentHealth)}";
            }
            if (delta < -0.5f)
            {
                float kick = Mathf.Clamp(Mathf.Abs(delta) / 28f, 0.28f, 0.68f);
                _damageFlashAlpha = Mathf.Max(_damageFlashAlpha, kick);
            }
        }

        private void HandleHungerChanged(float hunger, bool isHungry)
        {
            _currentHunger = Mathf.Clamp(hunger, 0f, 100f);
            if (_hungerFillImage != null)
            {
                _hungerFillImage.fillAmount = Mathf.Clamp01(_currentHunger / 100f);
                _hungerFillImage.color = isHungry
                    ? new Color(0.98f, 0.36f, 0.22f, 1f)
                    : new Color(0.98f, 0.68f, 0.18f, 1f);
            }
            RefreshBackpackHudButton();
        }

        private void RefreshBackpackHudButton()
        {
            if (_backpackHudLabel == null) return;
            bool en = LocalizationService.IsEnglish;
            int count = PlayerBackpackInventory.Instance != null ? PlayerBackpackInventory.Instance.TotalItemCount : 0;
            bool hungryWithFood = _currentHunger < 35f && count > 0;
            string prefix = en ? "Bag" : "Çanta";
            _backpackHudLabel.text = count > 0 ? $"{prefix} ({count})" : prefix;
            _backpackHudLabel.color = hungryWithFood ? new Color(1.0f, 0.86f, 0.32f, 1f) : OurWorldUiKit.TextPrimary;
            if (_backpackHudBg != null)
            {
                _backpackHudBg.color = hungryWithFood
                    ? new Color(0.24f, 0.15f, 0.05f, 0.94f)
                    : new Color(0.055f, 0.082f, 0.130f, 0.92f);
            }
        }

        private void BuildRuntimeHealthAndDamageHud()
        {
            // 1. Add sleek crimson Health Bar + amber Hunger (Tokluk) Bar right above Energy bar inside StatusCard_TopRight
            if (energyFillImage != null && energyFillImage.transform.parent != null)
            {
                RectTransform energyTrackRt = energyFillImage.transform.parent.GetComponent<RectTransform>();
                Transform statusCard = energyTrackRt != null ? energyTrackRt.parent : null;
                if (statusCard != null && statusCard.Find("HealthTrack") == null)
                {
                    RectTransform cardRt = statusCard.GetComponent<RectTransform>();
                    if (cardRt != null)
                    {
                        cardRt.anchoredPosition = new Vector2(-24f, -14f);
                        cardRt.sizeDelta = new Vector2(336f, 92f);
                    }

                    // Stack all 3 compact horizontal bars neatly: Energy (y=6), Hunger/Tokluk (y=18), Health (y=30)
                    energyTrackRt.anchoredPosition = new Vector2(58f, 6f);
                    energyTrackRt.sizeDelta = new Vector2(260f, 8f);

                    Font uiFont = moneyText != null ? moneyText.font : OurWorldUiKit.DefaultFont;
                    Image energyTrackImg = energyTrackRt.GetComponent<Image>();

                    // Subtle "⚡" tag left of Energy bar
                    GameObject enLblGo = new GameObject("EnergyIconLabel", typeof(RectTransform), typeof(Text));
                    enLblGo.transform.SetParent(statusCard, false);
                    RectTransform enLblRt = enLblGo.GetComponent<RectTransform>();
                    enLblRt.anchorMin = Vector2.zero;
                    enLblRt.anchorMax = Vector2.zero;
                    enLblRt.pivot = Vector2.zero;
                    enLblRt.anchoredPosition = new Vector2(18f, 2f);
                    enLblRt.sizeDelta = new Vector2(36f, 15f);
                    Text enTxt = enLblGo.GetComponent<Text>();
                    enTxt.font = uiFont;
                    enTxt.text = "⚡";
                    enTxt.fontSize = 11;
                    enTxt.fontStyle = FontStyle.Bold;
                    enTxt.alignment = TextAnchor.MiddleLeft;
                    enTxt.color = AccentTealColor;
                    enTxt.raycastTarget = false;

                    // Compact "Tok" / "Food" label + warm-amber Hunger Track
                    GameObject huLblGo = new GameObject("HungerLabel", typeof(RectTransform), typeof(Text));
                    huLblGo.transform.SetParent(statusCard, false);
                    RectTransform huLblRt = huLblGo.GetComponent<RectTransform>();
                    huLblRt.anchorMin = Vector2.zero;
                    huLblRt.anchorMax = Vector2.zero;
                    huLblRt.pivot = Vector2.zero;
                    huLblRt.anchoredPosition = new Vector2(16f, 13f);
                    huLblRt.sizeDelta = new Vector2(38f, 15f);
                    _hungerLabelText = huLblGo.GetComponent<Text>();
                    _hungerLabelText.font = uiFont;
                    _hungerLabelText.text = LocalizationService.IsEnglish ? "Food" : "Tok";
                    _hungerLabelText.fontSize = 11;
                    _hungerLabelText.fontStyle = FontStyle.Bold;
                    _hungerLabelText.alignment = TextAnchor.MiddleLeft;
                    _hungerLabelText.color = new Color(0.98f, 0.72f, 0.24f, 1f);
                    _hungerLabelText.raycastTarget = false;

                    GameObject huTrack = new GameObject("HungerTrack", typeof(RectTransform), typeof(Image));
                    huTrack.transform.SetParent(statusCard, false);
                    RectTransform huTrackRt = huTrack.GetComponent<RectTransform>();
                    huTrackRt.anchorMin = Vector2.zero;
                    huTrackRt.anchorMax = Vector2.zero;
                    huTrackRt.pivot = Vector2.zero;
                    huTrackRt.anchoredPosition = new Vector2(58f, 18f);
                    huTrackRt.sizeDelta = new Vector2(260f, 8f);
                    Image huTrackImg = huTrack.GetComponent<Image>();
                    if (energyTrackImg != null)
                    {
                        huTrackImg.sprite = energyTrackImg.sprite;
                        huTrackImg.type = energyTrackImg.type;
                    }
                    huTrackImg.color = new Color(1f, 1f, 1f, 0.14f);
                    huTrackImg.raycastTarget = false;

                    GameObject huFill = new GameObject("HungerFill", typeof(RectTransform), typeof(Image));
                    huFill.transform.SetParent(huTrack.transform, false);
                    RectTransform huFillRt = huFill.GetComponent<RectTransform>();
                    huFillRt.anchorMin = Vector2.zero;
                    huFillRt.anchorMax = Vector2.one;
                    huFillRt.offsetMin = Vector2.zero;
                    huFillRt.offsetMax = Vector2.zero;
                    _hungerFillImage = huFill.GetComponent<Image>();
                    _hungerFillImage.sprite = energyFillImage.sprite;
                    _hungerFillImage.type = Image.Type.Filled;
                    _hungerFillImage.fillMethod = Image.FillMethod.Horizontal;
                    _hungerFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                    _hungerFillImage.fillAmount = 0.88f;
                    _hungerFillImage.color = new Color(0.98f, 0.68f, 0.18f, 1f);
                    _hungerFillImage.raycastTarget = false;

                    // Compact "♥ 100" label left of Health bar
                    GameObject hpLblGo = new GameObject("HealthLabel", typeof(RectTransform), typeof(Text));
                    hpLblGo.transform.SetParent(statusCard, false);
                    RectTransform hpLblRt = hpLblGo.GetComponent<RectTransform>();
                    hpLblRt.anchorMin = Vector2.zero;
                    hpLblRt.anchorMax = Vector2.zero;
                    hpLblRt.pivot = Vector2.zero;
                    hpLblRt.anchoredPosition = new Vector2(16f, 24f);
                    hpLblRt.sizeDelta = new Vector2(38f, 16f);
                    _healthLabelText = hpLblGo.GetComponent<Text>();
                    _healthLabelText.font = uiFont;
                    _healthLabelText.text = "♥ 100";
                    _healthLabelText.fontSize = 11;
                    _healthLabelText.fontStyle = FontStyle.Bold;
                    _healthLabelText.alignment = TextAnchor.MiddleLeft;
                    _healthLabelText.color = new Color(0.98f, 0.36f, 0.38f, 1f);
                    _healthLabelText.raycastTarget = false;

                    // HealthTrack (background)
                    GameObject hpTrack = new GameObject("HealthTrack", typeof(RectTransform), typeof(Image));
                    hpTrack.transform.SetParent(statusCard, false);
                    RectTransform hpTrackRt = hpTrack.GetComponent<RectTransform>();
                    hpTrackRt.anchorMin = Vector2.zero;
                    hpTrackRt.anchorMax = Vector2.zero;
                    hpTrackRt.pivot = Vector2.zero;
                    hpTrackRt.anchoredPosition = new Vector2(58f, 30f);
                    hpTrackRt.sizeDelta = new Vector2(260f, 9f);
                    Image hpTrackImg = hpTrack.GetComponent<Image>();
                    if (energyTrackImg != null)
                    {
                        hpTrackImg.sprite = energyTrackImg.sprite;
                        hpTrackImg.type = energyTrackImg.type;
                    }
                    hpTrackImg.color = new Color(1f, 1f, 1f, 0.14f);
                    hpTrackImg.raycastTarget = false;

                    // HealthFill (crimson horizontal fill)
                    GameObject hpFill = new GameObject("HealthFill", typeof(RectTransform), typeof(Image));
                    hpFill.transform.SetParent(hpTrack.transform, false);
                    RectTransform hpFillRt = hpFill.GetComponent<RectTransform>();
                    hpFillRt.anchorMin = Vector2.zero;
                    hpFillRt.anchorMax = Vector2.one;
                    hpFillRt.offsetMin = Vector2.zero;
                    hpFillRt.offsetMax = Vector2.zero;
                    _healthFillImage = hpFill.GetComponent<Image>();
                    _healthFillImage.sprite = energyFillImage.sprite;
                    _healthFillImage.type = Image.Type.Filled;
                    _healthFillImage.fillMethod = Image.FillMethod.Horizontal;
                    _healthFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                    _healthFillImage.fillAmount = 1f;
                    _healthFillImage.color = new Color(0.90f, 0.24f, 0.28f, 1f);
                    _healthFillImage.raycastTarget = false;
                }
            }

            // 1b. 1-Tap Backpack ("Çanta") HUD Button aligned cleanly right next to "Menü" under StatusCard_TopRight!
            Transform safeArea = transform.Find("SafeAreaRoot") ?? transform;
            if (safeArea != null && safeArea.Find("Btn_Backpack") == null)
            {
                _backpackHudButton = OurWorldUiKit.CreatePillButton(
                    "Btn_Backpack",
                    safeArea,
                    LocalizationService.IsEnglish ? "Bag" : "Çanta",
                    22,
                    new Color(0.055f, 0.082f, 0.130f, 0.92f),
                    OurWorldUiKit.TextPrimary,
                    OurWorldUiKit.BorderSubtle,
                    out _backpackHudLabel,
                    () => BackpackPanelController.EnsureInstance().Toggle());
                _backpackHudBg = _backpackHudButton.GetComponent<Image>();
                RectTransform brt = (RectTransform)_backpackHudButton.transform;
                brt.anchorMin = new Vector2(1f, 1f);
                brt.anchorMax = new Vector2(1f, 1f);
                brt.pivot = new Vector2(1f, 1f);
                brt.anchoredPosition = new Vector2(-210f, -114f);
                brt.sizeDelta = new Vector2(150f, 70f);
                RefreshBackpackHudButton();
            }

            // 2. Full-screen peripheral red Damage Vignette overlay (transparent center, crimson edges)
            if (_damageVignetteImage == null)
            {
                const int sz = 128;
                _damageVignetteTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
                _damageVignetteTex.wrapMode = TextureWrapMode.Clamp;
                Color[] pix = new Color[sz * sz];
                for (int y = 0; y < sz; y++)
                {
                    float ny = (y / (float)(sz - 1)) * 2f - 1f;
                    for (int x = 0; x < sz; x++)
                    {
                        float nx = (x / (float)(sz - 1)) * 2f - 1f;
                        float dist = Mathf.Sqrt(nx * nx + ny * ny);
                        float edge = Mathf.SmoothStep(0.58f, 1.32f, dist);
                        pix[y * sz + x] = new Color(1f, 1f, 1f, edge);
                    }
                }
                _damageVignetteTex.SetPixels(pix);
                _damageVignetteTex.Apply();
                _damageVignetteSprite = Sprite.Create(_damageVignetteTex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));

                GameObject vigGo = new GameObject("DamageVignetteOverlay", typeof(RectTransform), typeof(Image));
                vigGo.transform.SetParent(transform, false);
                vigGo.transform.SetAsFirstSibling();
                RectTransform vrt = vigGo.GetComponent<RectTransform>();
                vrt.anchorMin = Vector2.zero;
                vrt.anchorMax = Vector2.one;
                vrt.offsetMin = Vector2.zero;
                vrt.offsetMax = Vector2.zero;
                _damageVignetteImage = vigGo.GetComponent<Image>();
                _damageVignetteImage.sprite = _damageVignetteSprite;
                _damageVignetteImage.color = new Color(0.86f, 0.08f, 0.10f, 0f);
                _damageVignetteImage.raycastTarget = false;
                vigGo.SetActive(false);
            }
        }

        private void HandleJobStateChanged()
        {
            JobManager jm = JobManager.Instance;
            bool hasJob = jm != null && jm.HasActiveJob && !string.IsNullOrEmpty(jm.CurrentJobHudText);

            if (jobObjectiveText != null)
            {
                jobObjectiveText.gameObject.SetActive(hasJob);
                if (hasJob)
                {
                    jobObjectiveText.text = jm.CurrentJobHudText;
                }
            }

            if (cancelJobButton != null)
            {
                cancelJobButton.gameObject.SetActive(hasJob);
            }

            _objectiveUnchangedSeconds = 0f;
        }

        private static bool RidingMotorcycle => PlayerModeController.Instance?.CurrentBicycle?.IsMotorcycle == true;

        private void CacheSportActionButtons()
        {
            if (onFootControlsRoot == null) return;
            if (_attackBtnLabel == null)
            {
                Transform atk = onFootControlsRoot.transform.Find("Btn_Attack");
                if (atk != null) _attackBtnLabel = atk.GetComponentInChildren<Text>(true);
            }
            if (_jumpBtnObject == null)
            {
                Transform jmp = onFootControlsRoot.transform.Find("Btn_Jump");
                if (jmp != null)
                {
                    _jumpBtnObject = jmp.gameObject;
                    _jumpBtnLabel = jmp.GetComponentInChildren<Text>(true);
                }
            }
        }

        private void HandleModeChanged(ControlMode mode)
        {
            bool driving = mode == ControlMode.Driving;
            bool cycling = mode == ControlMode.Cycling;
            bool tennis = mode == ControlMode.Tennis;
            bool arenaSport = mode == ControlMode.ArenaSport;
            bool inMatchSport = tennis || arenaSport;

            if (onFootControlsRoot != null) onFootControlsRoot.SetActive(!driving && !cycling);
            if (drivingControlsRoot != null) drivingControlsRoot.SetActive(driving || cycling);
            if (gearText != null) gearText.gameObject.SetActive(!cycling);
            if (fuelFillImage != null) fuelFillImage.transform.parent.gameObject.SetActive(!cycling);
            if (handbrakeButton != null) handbrakeButton.gameObject.SetActive(!cycling);
            if (exitCarLabel != null)
            {
                exitCarLabel.text = cycling ? LocalizationService.Tr(RidingMotorcycle ? "ui.btn.exit_motorcycle" : "ui.btn.exit_bike") :
                    LocalizationService.Tr("ui.btn.exit_car", "İn");
            }
            if (sprintButton != null) sprintButton.gameObject.SetActive(!tennis);
            if (_backpackHudButton != null) _backpackHudButton.gameObject.SetActive(!inMatchSport);

            CacheSportActionButtons();
            if (_jumpBtnObject != null)
            {
                bool showJump = (tennis && (PlayerModeController.Instance?.CurrentTennisCourt?.HasSecondaryAction ?? false))
                    || (!tennis && (!arenaSport || (PlayerModeController.Instance?.CurrentArenaSport?.HasSecondaryAction ?? false)));
                _jumpBtnObject.SetActive(showJump);
            }

            if (!inMatchSport)
            {
                if (_attackBtnLabel != null) _attackBtnLabel.text = LocalizationService.Tr("ui.btn.attack", "Vur");
                if (_jumpBtnLabel != null) _jumpBtnLabel.text = LocalizationService.Tr("ui.btn.jump", "Atla");
            }

            SetTennisScoreVisible(inMatchSport);

            if (PlayerModeController.Instance != null)
            {
                UpdateInteractPromptLabel(PlayerModeController.Instance.NearestInteractable);
            }

            HandleJobStateChanged();

            if (editorHintText != null)
            {
                string hintKey = mode switch
                {
                    ControlMode.Driving => "ui.hint.controls_drive",
                    ControlMode.Cycling => RidingMotorcycle ? "vehicle.motorcycle.controls" : "sports.bicycle.controls",
                    ControlMode.Tennis => "sports.tennis.controls",
                    ControlMode.ArenaSport => PlayerModeController.Instance?.CurrentArenaSport?.ControlsHintKey() ?? "sports.football.controls",
                    _ => "ui.hint.controls_foot"
                };
                editorHintText.text = LocalizationService.Tr(hintKey);
            }
        }

        private void SetTennisScoreVisible(bool visible)
        {
            if (visible && _tennisScoreText == null && interactButtonLabel != null && interactButton != null)
            {
                Transform uiParent = interactButton.transform.parent;
                Font uiFont = interactButtonLabel.font;

                GameObject panel = new GameObject("TennisScore", typeof(RectTransform), typeof(Image));
                RectTransform frame = (RectTransform)panel.transform;
                frame.SetParent(uiParent, false);
                frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 1f);
                frame.pivot = new Vector2(0.5f, 1f);
                frame.anchoredPosition = new Vector2(0f, -14f);
                frame.sizeDelta = new Vector2(660f, 92f);
                Image background = panel.GetComponent<Image>();
                OurWorldUiKit.ApplyRounded(background, OurWorldUiKit.SubCardSprite, OurWorldUiKit.CardBg);
                background.raycastTarget = false;
                OurWorldUiKit.AddCardDropShadow(frame, spread: 18f, offsetY: -8f, alpha: 0.48f);
                OurWorldUiKit.AddSoftTopGlow(frame, OurWorldUiKit.AccentTeal, heightPx: 54f, alpha: 0.14f);
                OurWorldUiKit.AddHairlineBorder(frame, OurWorldUiKit.SubCardOutlineSprite, OurWorldUiKit.BorderSubtle);

                GameObject label = new GameObject("Score and instructions", typeof(RectTransform), typeof(Text));
                RectTransform textRect = (RectTransform)label.transform;
                textRect.SetParent(frame, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(16f, 6f);
                textRect.offsetMax = new Vector2(-16f, -6f);
                _tennisScoreText = label.GetComponent<Text>();
                _tennisScoreText.font = uiFont;
                _tennisScoreText.fontSize = 21;
                _tennisScoreText.fontStyle = FontStyle.Bold;
                _tennisScoreText.alignment = TextAnchor.MiddleCenter;
                _tennisScoreText.color = new Color(0.95f, 0.97f, 1.0f, 1f);
                _tennisScoreText.raycastTarget = false;
                Outline scoreOut = label.AddComponent<Outline>();
                scoreOut.effectColor = new Color(0.02f, 0.04f, 0.07f, 0.85f);
                scoreOut.effectDistance = new Vector2(1.1f, -1.1f);

                // Dynamic Sports Meter Strip (Power / Timing Sweet-Spot + Stamina + Highlight Callout Badge)
                _sportMeterRoot = new GameObject("SportDynamicMeterRoot", typeof(RectTransform));
                RectTransform mRoot = (RectTransform)_sportMeterRoot.transform;
                mRoot.SetParent(uiParent, false);
                mRoot.anchorMin = mRoot.anchorMax = new Vector2(0.5f, 1f);
                mRoot.pivot = new Vector2(0.5f, 1f);
                mRoot.anchoredPosition = new Vector2(0f, -112f);
                mRoot.sizeDelta = new Vector2(460f, 84f);

                // 1. Primary Power / Timing Bar Track
                _sportPrimaryTrackObj = new GameObject("PrimaryMeterTrack", typeof(RectTransform), typeof(Image));
                RectTransform pTrack = (RectTransform)_sportPrimaryTrackObj.transform;
                pTrack.SetParent(mRoot, false);
                pTrack.anchorMin = new Vector2(0f, 1f);
                pTrack.anchorMax = new Vector2(1f, 1f);
                pTrack.pivot = new Vector2(0.5f, 1f);
                pTrack.anchoredPosition = Vector2.zero;
                pTrack.sizeDelta = new Vector2(0f, 24f);
                Image pTrackImg = _sportPrimaryTrackObj.GetComponent<Image>();
                pTrackImg.color = new Color(0.05f, 0.08f, 0.12f, 0.88f);
                pTrackImg.raycastTarget = false;

                // Sweet-Spot Golden Zone highlight inside track
                GameObject sweetGo = new GameObject("SweetSpotZone", typeof(RectTransform), typeof(Image));
                _sportSweetSpotRect = (RectTransform)sweetGo.transform;
                _sportSweetSpotRect.SetParent(pTrack, false);
                _sportSweetSpotRect.anchorMin = new Vector2(0.55f, 0f);
                _sportSweetSpotRect.anchorMax = new Vector2(0.85f, 1f);
                _sportSweetSpotRect.offsetMin = Vector2.zero;
                _sportSweetSpotRect.offsetMax = Vector2.zero;
                Image sweetImg = sweetGo.GetComponent<Image>();
                sweetImg.color = new Color(1.0f, 0.86f, 0.22f, 0.34f);
                sweetImg.raycastTarget = false;

                // Primary Fill Bar
                GameObject pFillGo = new GameObject("PrimaryMeterFill", typeof(RectTransform), typeof(Image));
                _sportPrimaryFillRect = (RectTransform)pFillGo.transform;
                _sportPrimaryFillRect.SetParent(pTrack, false);
                _sportPrimaryFillRect.anchorMin = Vector2.zero;
                _sportPrimaryFillRect.anchorMax = new Vector2(0f, 1f);
                _sportPrimaryFillRect.offsetMin = new Vector2(2f, 2f);
                _sportPrimaryFillRect.offsetMax = new Vector2(-2f, -2f);
                _sportPrimaryFillImg = pFillGo.GetComponent<Image>();
                _sportPrimaryFillImg.color = AccentTealColor;
                _sportPrimaryFillImg.raycastTarget = false;

                // Primary Label centered over bar
                GameObject pLblGo = new GameObject("PrimaryMeterLabel", typeof(RectTransform), typeof(Text));
                RectTransform pLblRt = (RectTransform)pLblGo.transform;
                pLblRt.SetParent(pTrack, false);
                pLblRt.anchorMin = Vector2.zero;
                pLblRt.anchorMax = Vector2.one;
                pLblRt.offsetMin = Vector2.zero;
                pLblRt.offsetMax = Vector2.zero;
                _sportPrimaryLabel = pLblGo.GetComponent<Text>();
                _sportPrimaryLabel.font = uiFont;
                _sportPrimaryLabel.fontSize = 15;
                _sportPrimaryLabel.fontStyle = FontStyle.Bold;
                _sportPrimaryLabel.alignment = TextAnchor.MiddleCenter;
                _sportPrimaryLabel.color = Color.white;
                _sportPrimaryLabel.raycastTarget = false;
                Outline pLblOut = pLblGo.AddComponent<Outline>();
                pLblOut.effectColor = new Color(0.03f, 0.05f, 0.09f, 0.96f);
                pLblOut.effectDistance = new Vector2(1.5f, -1.5f);
                Shadow pLblShd = pLblGo.AddComponent<Shadow>();
                pLblShd.effectColor = new Color(0f, 0f, 0f, 0.85f);
                pLblShd.effectDistance = new Vector2(0f, -1.4f);

                // 2. Slim Stamina Bar right underneath
                _sportStaminaTrackObj = new GameObject("StaminaTrack", typeof(RectTransform), typeof(Image));
                RectTransform sTrack = (RectTransform)_sportStaminaTrackObj.transform;
                sTrack.SetParent(mRoot, false);
                sTrack.anchorMin = new Vector2(0.12f, 1f);
                sTrack.anchorMax = new Vector2(0.88f, 1f);
                sTrack.pivot = new Vector2(0.5f, 1f);
                sTrack.anchoredPosition = new Vector2(0f, -29f);
                sTrack.sizeDelta = new Vector2(0f, 8f);
                Image sTrackImg = _sportStaminaTrackObj.GetComponent<Image>();
                sTrackImg.color = new Color(0.05f, 0.08f, 0.12f, 0.76f);
                sTrackImg.raycastTarget = false;

                GameObject sFillGo = new GameObject("StaminaFill", typeof(RectTransform), typeof(Image));
                _sportStaminaFillRect = (RectTransform)sFillGo.transform;
                _sportStaminaFillRect.SetParent(sTrack, false);
                _sportStaminaFillRect.anchorMin = Vector2.zero;
                _sportStaminaFillRect.anchorMax = new Vector2(1f, 1f);
                _sportStaminaFillRect.offsetMin = new Vector2(1f, 1f);
                _sportStaminaFillRect.offsetMax = new Vector2(-1f, -1f);
                _sportStaminaFillImg = sFillGo.GetComponent<Image>();
                _sportStaminaFillImg.color = new Color(0.24f, 0.90f, 0.68f, 0.95f);
                _sportStaminaFillImg.raycastTarget = false;

                // 3. Crisp Callout Badge Pill ("⚡ 90'A FALSOLU FÜZE!", "🧱 DUVAR BLOK SAYISI!")
                _sportCalloutObj = new GameObject("SportCalloutBadge", typeof(RectTransform), typeof(Image));
                RectTransform cRt = (RectTransform)_sportCalloutObj.transform;
                cRt.SetParent(mRoot, false);
                cRt.anchorMin = new Vector2(0.5f, 1f);
                cRt.anchorMax = new Vector2(0.5f, 1f);
                cRt.pivot = new Vector2(0.5f, 1f);
                cRt.anchoredPosition = new Vector2(0f, -43f);
                cRt.sizeDelta = new Vector2(440f, 34f);
                _sportCalloutBg = _sportCalloutObj.GetComponent<Image>();
                OurWorldUiKit.ApplyRounded(_sportCalloutBg, OurWorldUiKit.PillSprite, OurWorldUiKit.CardBg);
                _sportCalloutBg.raycastTarget = false;
                OurWorldUiKit.AddHairlineBorder(cRt, OurWorldUiKit.PillOutlineSprite,
                    new Color(OurWorldUiKit.AccentGold.r, OurWorldUiKit.AccentGold.g, OurWorldUiKit.AccentGold.b, 0.40f));

                GameObject cTxtGo = new GameObject("CalloutText", typeof(RectTransform), typeof(Text));
                RectTransform cTxtRt = (RectTransform)cTxtGo.transform;
                cTxtRt.SetParent(cRt, false);
                cTxtRt.anchorMin = Vector2.zero;
                cTxtRt.anchorMax = Vector2.one;
                cTxtRt.offsetMin = new Vector2(10f, 2f);
                cTxtRt.offsetMax = new Vector2(-10f, -2f);
                _sportCalloutText = cTxtGo.GetComponent<Text>();
                _sportCalloutText.font = uiFont;
                _sportCalloutText.fontSize = 18;
                _sportCalloutText.fontStyle = FontStyle.Bold;
                _sportCalloutText.alignment = TextAnchor.MiddleCenter;
                _sportCalloutText.color = Color.white;
                _sportCalloutText.raycastTarget = false;
                Outline cTxtOut = cTxtGo.AddComponent<Outline>();
                cTxtOut.effectColor = new Color(0.02f, 0.04f, 0.08f, 0.92f);
                cTxtOut.effectDistance = new Vector2(1.2f, -1.2f);

                _sportMeterRoot.SetActive(false);
            }

            if (_tennisScoreText != null)
            {
                _tennisScoreText.transform.parent.gameObject.SetActive(visible);
                _tennisHudRevision = -1;
            }
            if (_sportMeterRoot != null && !visible)
            {
                _sportMeterRoot.SetActive(false);
            }
        }

        private void UpdateSportMeterVisuals(SportHudMeterState state)
        {
            if (_sportMeterRoot == null) return;
            bool hasCallout = !string.IsNullOrEmpty(state.CalloutBadge);
            bool anyVisible = state.ShowPrimaryBar || state.ShowStaminaBar || hasCallout;
            if (_sportMeterRoot.activeSelf != anyVisible) _sportMeterRoot.SetActive(anyVisible);
            if (!anyVisible) return;

            if (_sportPrimaryTrackObj != null)
            {
                if (_sportPrimaryTrackObj.activeSelf != state.ShowPrimaryBar)
                    _sportPrimaryTrackObj.SetActive(state.ShowPrimaryBar);
                if (state.ShowPrimaryBar)
                {
                    float fill = Mathf.Clamp01(state.PrimaryFill01);
                    if (_sportPrimaryFillRect != null) _sportPrimaryFillRect.anchorMax = new Vector2(fill, 1f);
                    if (_sportPrimaryFillImg != null) _sportPrimaryFillImg.color = state.PrimaryColor;
                    if (_sportPrimaryLabel != null) _sportPrimaryLabel.text = state.PrimaryLabel ?? string.Empty;
                    if (_sportSweetSpotRect != null)
                    {
                        _sportSweetSpotRect.gameObject.SetActive(state.ShowSweetSpot);
                        if (state.ShowSweetSpot)
                        {
                            _sportSweetSpotRect.anchorMin = new Vector2(Mathf.Clamp01(state.SweetSpotMin01), 0f);
                            _sportSweetSpotRect.anchorMax = new Vector2(Mathf.Clamp01(state.SweetSpotMax01), 1f);
                        }
                    }
                }
            }

            if (_sportStaminaTrackObj != null)
            {
                if (_sportStaminaTrackObj.activeSelf != state.ShowStaminaBar)
                    _sportStaminaTrackObj.SetActive(state.ShowStaminaBar);
                if (state.ShowStaminaBar)
                {
                    float sFill = Mathf.Clamp01(state.StaminaFill01);
                    if (_sportStaminaFillRect != null) _sportStaminaFillRect.anchorMax = new Vector2(sFill, 1f);
                    if (_sportStaminaFillImg != null)
                    {
                        _sportStaminaFillImg.color = sFill < 0.25f
                            ? new Color(0.96f, 0.45f, 0.22f, 0.95f)
                            : new Color(0.24f, 0.92f, 0.68f, 0.95f);
                    }
                }
            }

            if (_sportCalloutObj != null)
            {
                if (_sportCalloutObj.activeSelf != hasCallout) _sportCalloutObj.SetActive(hasCallout);
                if (hasCallout && _sportCalloutText != null)
                {
                    _sportCalloutText.text = state.CalloutBadge;
                    _sportCalloutText.color = state.CalloutColor;
                }
            }
        }

        private void HandleInteractableChanged(InteractableTrigger trigger)
        {
            UpdateInteractPromptLabel(trigger);
        }

        private void UpdateInteractPromptLabel(InteractableTrigger trigger)
        {
            if (interactButton == null) return;

            ControlMode mode = PlayerModeController.Instance != null
                ? PlayerModeController.Instance.CurrentMode : ControlMode.OnFoot;
            if (mode == ControlMode.Seated || mode == ControlMode.Tennis || mode == ControlMode.ArenaSport)
            {
                _interactDesiredVisible = true;
                if (!interactButton.gameObject.activeSelf) interactButton.gameObject.SetActive(true);
                if (interactButtonLabel != null)
                {
                    string key = mode == ControlMode.Tennis
                        ? "ui.btn.leave_tennis"
                        : (mode == ControlMode.ArenaSport ? "ui.btn.leave_sport" : "ui.btn.stand");
                    string localized = LocalizationService.Tr(key);
                    interactButtonLabel.text = Application.isMobilePlatform ? localized : $"E · {localized}";
                    if (_interactRect != null)
                    {
                        float minW = ResponsiveTabletLayout.IsTabletLayoutActive ? 340f : 364f;
                        float desiredW = Mathf.Clamp(interactButtonLabel.text.Length * 16f + 86f, minW, 560f);
                        _interactRect.sizeDelta = new Vector2(desiredW, _interactRect.sizeDelta.y);
                    }
                }
                return;
            }

            bool hasTrigger = trigger != null;
            _interactDesiredVisible = hasTrigger;
            if (hasTrigger)
            {
                if (!interactButton.gameObject.activeSelf) interactButton.gameObject.SetActive(true);
                if (interactButtonLabel != null)
                {
                    string prompt = trigger.GetLocalizedPrompt();
                    interactButtonLabel.text = Application.isMobilePlatform ? prompt : $"E · {prompt}";
                    if (_interactRect != null)
                    {
                        float minW = ResponsiveTabletLayout.IsTabletLayoutActive ? 340f : 364f;
                        float desiredW = Mathf.Clamp(interactButtonLabel.text.Length * 16f + 86f, minW, 560f);
                        _interactRect.sizeDelta = new Vector2(desiredW, _interactRect.sizeDelta.y);
                    }
                }
            }
        }

        private void HandleObjectiveChanged(MissionData mission, MissionStepData step)
        {
            if (failedPanel != null) failedPanel.SetActive(false);
            if (tailWarningContainer != null) tailWarningContainer.SetActive(false);
            _objectiveUnchangedSeconds = 0f;

            if (mission == null || step == null)
            {
                if (missionTitleText != null) missionTitleText.text = LocalizationService.Tr("ui.hud.freeroam_title", "Serbest Dolaşım");
                if (missionObjectiveText != null) missionObjectiveText.text = LocalizationService.Tr("ui.hud.freeroam_objective");
                return;
            }

            if (missionTitleText != null)
            {
                string rawTitle = LocalizationService.Tr(mission.titleKey, mission.missionId);
                MissionManager mm = MissionManager.Instance;
                missionTitleText.resizeTextForBestFit = true;
                missionTitleText.resizeTextMinSize = 14;
                missionTitleText.resizeTextMaxSize = Mathf.Max(18, missionTitleText.fontSize);
                if (mm != null && mm.ActiveMissionIndex > 0 && mm.TotalMissionCount > 0)
                {
                    missionTitleText.text = $"{mm.ActiveMissionIndex}/{mm.TotalMissionCount} · {rawTitle}";
                }
                else
                {
                    missionTitleText.text = rawTitle;
                }
            }
            if (missionObjectiveText != null)
            {
                string rawObj = LocalizationService.Tr(step.objectiveKey, string.Empty);
                MissionManager mm = MissionManager.Instance;
                if (mm != null && mm.CurrentStepNumber > 0 && mm.ActiveMissionStepCount > 1)
                {
                    missionObjectiveText.text = $"({mm.CurrentStepNumber}/{mm.ActiveMissionStepCount}) {rawObj}";
                }
                else
                {
                    missionObjectiveText.text = rawObj;
                }
            }
        }

        private void HandleDialogueLineShown(string speaker, string body)
        {
            ApplySubtitleSize();
            if (dialogueSpeakerText != null) dialogueSpeakerText.text = speaker;
            if (dialogueBodyText != null) dialogueBodyText.text = body;
            if (dialoguePanel != null && !dialoguePanel.activeSelf)
            {
                AnimateOverlayPanel(dialoguePanel, opening: true, ref _dlgAnimRoutine);
            }
        }

        private void HandleDialogueClosed()
        {
            if (dialoguePanel != null && dialoguePanel.activeSelf)
            {
                AnimateOverlayPanel(dialoguePanel, opening: false, ref _dlgAnimRoutine);
            }
        }

        private void HandleMissionCompletedBanner(string missionTitle)
        {
            if (bannerPanel == null || bannerText == null) return;
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(ShowBannerRoutine(
                $"{LocalizationService.Tr("ui.hud.mission_complete", "Görev Tamamlandı")}\n{missionTitle}"));
        }

        private void HandleMissionFailed(string reason)
        {
            if (failedReasonText != null) failedReasonText.text = reason;
            AnimateOverlayPanel(failedPanel, opening: true, ref _failAnimRoutine);
        }

        private void HandleTailStatusChanged(TailWarningState state, float distance)
        {
            if (tailWarningContainer == null || tailWarningText == null) return;

            bool show = state != TailWarningState.None;
            tailWarningContainer.SetActive(show);
            if (!show) return;

            switch (state)
            {
                case TailWarningState.TooClose:
                    tailWarningText.text = $"{LocalizationService.Tr("ui.hud.tail_too_close")} ({Mathf.RoundToInt(distance)}m)";
                    if (tailWarningBackground != null) tailWarningBackground.color = DangerRedColor;
                    break;
                case TailWarningState.TooFar:
                    tailWarningText.text = $"{LocalizationService.Tr("ui.hud.tail_too_far")} ({Mathf.RoundToInt(distance)}m)";
                    if (tailWarningBackground != null) tailWarningBackground.color = new Color(0.76f, 0.48f, 0.18f, 0.85f);
                    break;
                default:
                    tailWarningText.text = $"{LocalizationService.Tr("ui.hud.tail_good")} ({Mathf.RoundToInt(distance)}m)";
                    if (tailWarningBackground != null) tailWarningBackground.color = new Color(0.18f, 0.46f, 0.42f, 0.80f);
                    break;
            }
        }

        private void HandleGraphicsTierChanged(GraphicsTier tier)
        {
            if (graphicsTierLabel != null)
            {
                graphicsTierLabel.text = tier == GraphicsTier.Low
                    ? LocalizationService.Tr("ui.btn.gfx_low", "Kalite: Düşük")
                    : LocalizationService.Tr("ui.btn.gfx_med", "Kalite: Orta");
            }
        }

        private void HandleTelemetryUpdated(string summary)
        {
            if (telemetryText != null)
            {
                telemetryText.text = summary;
            }
        }

        private void ToggleTelemetryPanel()
        {
            if (telemetryPanel != null)
            {
                telemetryPanel.SetActive(!telemetryPanel.activeSelf);
            }
        }

        private void HandleExportPerfReport()
        {
            if (PerformanceMonitor.Instance == null) return;
            PerformanceMonitor.Instance.ExportBenchmarkReport();
            ShowToast(LocalizationService.Tr("ui.hud.perf_saved", "Performans raporu kaydedildi (ourworld_perf_log.txt)"));
        }

        private void ShowToast(string message)
        {
            if (statusToastText == null) return;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            statusToastText.text = message;
            statusToastText.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(2.8f);
            statusToastText.gameObject.SetActive(false);
        }

        private IEnumerator ShowBannerRoutine(string message)
        {
            bannerText.text = message;
            CanvasGroup cg = EnsureCanvasGroup(bannerPanel);
            RectTransform rt = bannerPanel.GetComponent<RectTransform>();
            bannerPanel.SetActive(true);

            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.2f));
                cg.alpha = k;
                if (rt != null) rt.localScale = Vector3.Lerp(Vector3.one * 0.94f, Vector3.one, k);
                yield return null;
            }
            cg.alpha = 1f;
            if (rt != null) rt.localScale = Vector3.one;

            yield return new WaitForSecondsRealtime(2.2f);

            t = 0f;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / 0.2f));
                cg.alpha = k;
                if (rt != null) rt.localScale = Vector3.Lerp(Vector3.one * 0.94f, Vector3.one, k);
                yield return null;
            }
            bannerPanel.SetActive(false);
        }

        private void AnimateOverlayPanel(GameObject panel, bool opening, ref Coroutine routineSlot)
        {
            if (panel == null) return;
            if (routineSlot != null) StopCoroutine(routineSlot);
            routineSlot = StartCoroutine(PanelFadeScaleRoutine(panel, opening));
        }

        private static IEnumerator PanelFadeScaleRoutine(GameObject panel, bool opening)
        {
            CanvasGroup cg = EnsureCanvasGroup(panel);
            RectTransform rt = panel.GetComponent<RectTransform>();

            if (opening)
            {
                panel.SetActive(true);
                cg.alpha = 0f;
            }

            const float duration = 0.20f;
            float startAlpha = cg.alpha;
            float endAlpha = opening ? 1f : 0f;
            Vector3 startScale = opening ? Vector3.one * 0.94f : Vector3.one;
            Vector3 endScale = opening ? Vector3.one : Vector3.one * 0.94f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                cg.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                if (rt != null) rt.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }

            cg.alpha = endAlpha;
            if (rt != null) rt.localScale = opening ? Vector3.one : endScale;
            if (!opening)
            {
                panel.SetActive(false);
            }
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = go.AddComponent<CanvasGroup>();
            }
            return cg;
        }
    }
}
