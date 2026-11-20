using System.Collections;
using Gridroam.Core;
using Gridroam.Input;
using Gridroam.Jobs;
using Gridroam.Missions;
using Gridroam.Player;
using Gridroam.World;
using UnityEngine;
using UnityEngine.UI;

namespace Gridroam.UI
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

        private static readonly Color OffWhiteColor = new Color(0.929f, 0.937f, 0.949f, 1f);
        private static readonly Color AccentTealColor = new Color(0.310f, 0.702f, 0.663f, 1f);
        private static readonly Color DangerRedColor = new Color(0.851f, 0.325f, 0.310f, 1f);

        private Coroutine _toastRoutine;
        private Coroutine _bannerRoutine;
        private Coroutine _moneyPopRoutine;
        private Coroutine _dlgAnimRoutine;
        private Coroutine _failAnimRoutine;

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

            BindButtons();
            SubscribeEvents();
            RefreshStaticLabels();
            SyncInitialState();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
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
            }

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
            }

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
            if (exitCarLabel != null) exitCarLabel.text = LocalizationService.Tr("ui.btn.exit_car", "İn");
            if (dialogueContinueLabel != null) dialogueContinueLabel.text = LocalizationService.Tr("ui.btn.continue", "Devam");
            if (failedTitleText != null) failedTitleText.text = LocalizationService.Tr("ui.hud.mission_failed", "Görev Başarısız");
            if (retryButtonLabel != null) retryButtonLabel.text = LocalizationService.Tr("ui.btn.retry", "Tekrar Dene");
            if (resetSaveLabel != null) resetSaveLabel.text = LocalizationService.Tr("ui.btn.reset_save", "Sıfırla");
            if (sprintButtonLabel != null) sprintButtonLabel.text = LocalizationService.Tr("ui.btn.sprint", "Koş");
            if (handbrakeLabel != null) handbrakeLabel.text = LocalizationService.Tr("ui.btn.handbrake", "El Freni");
            if (phoneButtonLabel != null) phoneButtonLabel.text = LocalizationService.Tr("ui.btn.phone", "Rehber (M)");
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
            if (player != null && player.IsDriving && player.CurrentVehicle != null)
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

        private void HandleModeChanged(ControlMode mode)
        {
            bool driving = mode == ControlMode.Driving;
            if (onFootControlsRoot != null) onFootControlsRoot.SetActive(!driving);
            if (drivingControlsRoot != null) drivingControlsRoot.SetActive(driving);

            if (PlayerModeController.Instance != null)
            {
                UpdateInteractPromptLabel(PlayerModeController.Instance.NearestInteractable);
            }

            HandleJobStateChanged();

            if (editorHintText != null)
            {
                editorHintText.text = driving
                    ? LocalizationService.Tr("ui.hint.controls_drive")
                    : LocalizationService.Tr("ui.hint.controls_foot");
            }
        }

        private void HandleInteractableChanged(InteractableTrigger trigger)
        {
            UpdateInteractPromptLabel(trigger);
        }

        private void UpdateInteractPromptLabel(InteractableTrigger trigger)
        {
            if (interactButton == null) return;

            if (PlayerModeController.Instance != null && PlayerModeController.Instance.CurrentMode == ControlMode.Seated)
            {
                _interactDesiredVisible = true;
                if (!interactButton.gameObject.activeSelf) interactButton.gameObject.SetActive(true);
                if (interactButtonLabel != null)
                {
                    interactButtonLabel.text = $"E · {LocalizationService.Tr("ui.btn.stand", "Kalk")}";
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
                    interactButtonLabel.text = $"E · {trigger.GetLocalizedPrompt()}";
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

            if (missionTitleText != null) missionTitleText.text = LocalizationService.Tr(mission.titleKey, mission.missionId);
            if (missionObjectiveText != null) missionObjectiveText.text = LocalizationService.Tr(step.objectiveKey, string.Empty);
        }

        private void HandleDialogueLineShown(string speaker, string body)
        {
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
            ShowToast(LocalizationService.Tr("ui.hud.perf_saved", "Performans raporu kaydedildi (gridroam_perf_log.txt)"));
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
