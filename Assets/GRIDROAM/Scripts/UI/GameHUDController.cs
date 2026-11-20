using System.Collections;
using Gridroam.Core;
using Gridroam.Input;
using Gridroam.Missions;
using Gridroam.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// Controls the entire GRIDROAM HUD: On-Foot / Driving layouts, Mission Objective,
    /// Countdown Timer, Tail Warning bar, Dialogue panel, Fail/Retry modal, Graphics toggle,
    /// and Performance telemetry overlay.
    /// </summary>
    public class GameHUDController : MonoBehaviour
    {
        [Header("Mode Containers")]
        [SerializeField] private GameObject onFootControlsRoot;
        [SerializeField] private GameObject drivingControlsRoot;

        [Header("On-Foot Controls")]
        [SerializeField] private Button interactButton;
        [SerializeField] private Text interactButtonLabel;

        [Header("Driving Controls")]
        [SerializeField] private HoldButton steerLeftButton;
        [SerializeField] private HoldButton steerRightButton;
        [SerializeField] private HoldButton gasButton;
        [SerializeField] private HoldButton brakeButton;
        [SerializeField] private Button exitCarButton;
        [SerializeField] private Text exitCarLabel;
        [SerializeField] private Text speedText;

        [Header("Mission HUD")]
        [SerializeField] private Text missionTitleText;
        [SerializeField] private Text missionObjectiveText;
        [SerializeField] private GameObject timerContainer;
        [SerializeField] private Text timerText;
        [SerializeField] private GameObject tailWarningContainer;
        [SerializeField] private Text tailWarningText;
        [SerializeField] private Image tailWarningBackground;
        [SerializeField] private Text statusToastText;
        [SerializeField] private Text editorHintText;

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

        private Coroutine _toastRoutine;
        private Coroutine _bannerRoutine;

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

        private void Start()
        {
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
                interactButton.onClick.AddListener(() => InputHub.Instance?.TriggerTouchInteract());
            }

            if (exitCarButton != null)
            {
                exitCarButton.onClick.AddListener(() => InputHub.Instance?.TriggerTouchExitVehicle());
            }

            if (dialogueContinueButton != null)
            {
                dialogueContinueButton.onClick.AddListener(() => InputHub.Instance?.TriggerTouchContinueDialogue());
            }

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(() =>
                {
                    if (failedPanel != null) failedPanel.SetActive(false);
                    MissionManager.Instance?.RetryCurrentMission();
                });
            }

            if (graphicsTierButton != null)
            {
                graphicsTierButton.onClick.AddListener(() => GraphicsProfileManager.Instance?.CycleTier());
            }

            if (resetSaveButton != null)
            {
                resetSaveButton.onClick.AddListener(() =>
                {
                    if (failedPanel != null) failedPanel.SetActive(false);
                    MissionManager.Instance?.ResetProgressAndRestart();
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

            if (steerLeftButton != null) steerLeftButton.OnStateChanged += pressed => InputHub.Instance?.SetTouchSteerLeft(pressed);
            if (steerRightButton != null) steerRightButton.OnStateChanged += pressed => InputHub.Instance?.SetTouchSteerRight(pressed);
            if (gasButton != null) gasButton.OnStateChanged += pressed => InputHub.Instance?.SetTouchGas(pressed);
            if (brakeButton != null) brakeButton.OnStateChanged += pressed => InputHub.Instance?.SetTouchBrake(pressed);
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
                InputHub.Instance.OnCycleGraphicsPressed += () => GraphicsProfileManager.Instance?.CycleTier();
                InputHub.Instance.OnExportPerfReportPressed += HandleExportPerfReport;
            }

            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged += _ => RefreshStaticLabels();
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

            if (GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged -= HandleGraphicsTierChanged;
            }

            if (PerformanceMonitor.Instance != null)
            {
                PerformanceMonitor.Instance.OnTelemetryUpdated -= HandleTelemetryUpdated;
            }
        }

        private void SyncInitialState()
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (failedPanel != null) failedPanel.SetActive(false);
            if (bannerPanel != null) bannerPanel.SetActive(false);
            if (timerContainer != null) timerContainer.SetActive(false);
            if (tailWarningContainer != null) tailWarningContainer.SetActive(false);
            if (statusToastText != null) statusToastText.gameObject.SetActive(false);

            if (PlayerModeController.Instance != null)
            {
                HandleModeChanged(PlayerModeController.Instance.CurrentMode);
                HandleInteractableChanged(PlayerModeController.Instance.NearestInteractable);
            }

            if (MissionManager.Instance != null)
            {
                HandleObjectiveChanged(MissionManager.Instance.ActiveMission, MissionManager.Instance.CurrentStep);
            }

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
            if (exitCarLabel != null) exitCarLabel.text = LocalizationService.Tr("ui.btn.exit_car", "EXIT CAR");
            if (dialogueContinueLabel != null) dialogueContinueLabel.text = LocalizationService.Tr("ui.btn.continue", "CONTINUE");
            if (failedTitleText != null) failedTitleText.text = LocalizationService.Tr("ui.hud.mission_failed", "MISSION FAILED");
            if (retryButtonLabel != null) retryButtonLabel.text = LocalizationService.Tr("ui.btn.retry", "RETRY MISSION");
            if (resetSaveLabel != null) resetSaveLabel.text = LocalizationService.Tr("ui.btn.reset_save", "RESET");
        }

        private void Update()
        {
            MissionManager mm = MissionManager.Instance;
            if (mm != null && timerContainer != null && timerText != null)
            {
                bool showTimer = mm.HasActiveTimer && !mm.IsFailed;
                if (timerContainer.activeSelf != showTimer)
                {
                    timerContainer.SetActive(showTimer);
                }

                if (showTimer)
                {
                    int totalSec = Mathf.CeilToInt(Mathf.Max(0f, mm.RemainingTime));
                    int mins = totalSec / 60;
                    int secs = totalSec % 60;
                    timerText.text = $"{mins:00}:{secs:00}";
                    timerText.color = mm.RemainingTime <= 15f
                        ? new Color(1f, 0.34f, 0.30f, 1f)
                        : Color.white;
                }
            }

            PlayerModeController player = PlayerModeController.Instance;
            if (player != null && player.IsDriving && player.PlayerSedan != null && speedText != null)
            {
                int kmh = Mathf.RoundToInt(player.PlayerSedan.SpeedKmh);
                speedText.text = $"{kmh} {LocalizationService.Tr("ui.hud.speed_unit", "KM/H")}";
            }
        }

        private void HandleModeChanged(ControlMode mode)
        {
            bool driving = mode == ControlMode.Driving;
            if (onFootControlsRoot != null) onFootControlsRoot.SetActive(!driving);
            if (drivingControlsRoot != null) drivingControlsRoot.SetActive(driving);

            if (editorHintText != null)
            {
                editorHintText.text = driving
                    ? LocalizationService.Tr("ui.hint.controls_drive")
                    : LocalizationService.Tr("ui.hint.controls_foot");
            }
        }

        private void HandleInteractableChanged(InteractableTrigger trigger)
        {
            if (interactButton == null) return;

            bool hasTrigger = trigger != null;
            interactButton.gameObject.SetActive(hasTrigger);
            if (hasTrigger && interactButtonLabel != null)
            {
                interactButtonLabel.text = trigger.GetLocalizedPrompt();
            }
        }

        private void HandleObjectiveChanged(MissionData mission, MissionStepData step)
        {
            if (failedPanel != null) failedPanel.SetActive(false);
            if (tailWarningContainer != null) tailWarningContainer.SetActive(false);

            if (mission == null || step == null)
            {
                if (missionTitleText != null) missionTitleText.text = LocalizationService.Tr("ui.hud.freeroam_title", "FREE ROAM");
                if (missionObjectiveText != null) missionObjectiveText.text = LocalizationService.Tr("ui.hud.freeroam_objective");
                return;
            }

            if (missionTitleText != null) missionTitleText.text = LocalizationService.Tr(mission.titleKey, mission.missionId);
            if (missionObjectiveText != null) missionObjectiveText.text = LocalizationService.Tr(step.objectiveKey, string.Empty);
        }

        private void HandleDialogueLineShown(string speaker, string body)
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(true);
            if (dialogueSpeakerText != null) dialogueSpeakerText.text = speaker;
            if (dialogueBodyText != null) dialogueBodyText.text = body;
        }

        private void HandleDialogueClosed()
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
        }

        private void HandleMissionCompletedBanner(string missionTitle)
        {
            if (bannerPanel == null || bannerText == null) return;
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(ShowBannerRoutine(
                $"{LocalizationService.Tr("ui.hud.mission_complete", "MISSION COMPLETE")}\n{missionTitle}"));
        }

        private void HandleMissionFailed(string reason)
        {
            if (failedPanel != null) failedPanel.SetActive(true);
            if (failedReasonText != null) failedReasonText.text = reason;
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
                    if (tailWarningBackground != null) tailWarningBackground.color = new Color(0.75f, 0.16f, 0.14f, 0.88f);
                    break;
                case TailWarningState.TooFar:
                    tailWarningText.text = $"{LocalizationService.Tr("ui.hud.tail_too_far")} ({Mathf.RoundToInt(distance)}m)";
                    if (tailWarningBackground != null) tailWarningBackground.color = new Color(0.82f, 0.45f, 0.10f, 0.88f);
                    break;
                default:
                    tailWarningText.text = $"{LocalizationService.Tr("ui.hud.tail_good")} ({Mathf.RoundToInt(distance)}m)";
                    if (tailWarningBackground != null) tailWarningBackground.color = new Color(0.14f, 0.46f, 0.28f, 0.78f);
                    break;
            }
        }

        private void HandleGraphicsTierChanged(GraphicsTier tier)
        {
            if (graphicsTierLabel != null)
            {
                graphicsTierLabel.text = tier == GraphicsTier.Low ? "GFX: LOW" : "GFX: MED";
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
            ShowToast("Perf report saved (gridroam_perf_log.txt)");
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
            yield return new WaitForSeconds(2.8f);
            statusToastText.gameObject.SetActive(false);
        }

        private IEnumerator ShowBannerRoutine(string message)
        {
            bannerText.text = message;
            bannerPanel.SetActive(true);
            yield return new WaitForSeconds(2.5f);
            bannerPanel.SetActive(false);
        }
    }
}
