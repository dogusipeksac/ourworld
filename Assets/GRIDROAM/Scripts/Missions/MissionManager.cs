using System;
using System.Collections;
using System.Collections.Generic;
using Gridroam.Core;
using Gridroam.Input;
using Gridroam.Player;
using Gridroam.Vehicle;
using UnityEngine;

namespace Gridroam.Missions
{
    public enum TailWarningState
    {
        None,
        Safe,
        TooClose,
        TooFar
    }

    /// <summary>
    /// Data-driven mission runner supporting sequential steps, NPC dialogues, timers,
    /// vehicle enter/exit checks, tail distance monitoring, failure/retry, local save, and Free Roam.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        [Header("Data")]
        [SerializeField] private MissionCatalog missionCatalog;

        [Header("Scene Visuals")]
        [SerializeField] private ObjectiveBeacon objectiveBeacon;

        [Header("Timing")]
        [SerializeField] private float missionCompleteBannerSeconds = 2.6f;

        private MissionData _activeMission;
        private int _currentStepIndex = -1;
        private WorldAnchor _currentTargetAnchor;
        private bool _isFailed;
        private bool _isInFreeRoam;

        // Timer state
        private bool _hasActiveTimer;
        private float _remainingTime;

        // Tail state (ready for Mission 4)
        private float _tailGraceTimer;
        private TailWarningState _tailWarningState = TailWarningState.None;

        // Dialogue state
        private DialogueData _activeDialogue;
        private int _dialogueLineIndex;
        private Action _onDialogueCompleted;
        private bool _dialogueWaitingForChoice;

        public MissionData ActiveMission => _activeMission;
        public MissionStepData CurrentStep =>
            (_activeMission != null && _currentStepIndex >= 0 && _currentStepIndex < _activeMission.steps.Count)
                ? _activeMission.steps[_currentStepIndex]
                : null;
        public WorldAnchor CurrentTargetAnchor => _currentTargetAnchor;
        public bool IsFailed => _isFailed;
        public bool IsInFreeRoam => _isInFreeRoam;
        public bool IsDialoguePlaying => _activeDialogue != null;
        public bool HasActiveTimer => _hasActiveTimer;
        public float RemainingTime => _remainingTime;
        public TailWarningState CurrentTailWarning => _tailWarningState;

        public event Action<MissionData, MissionStepData> OnObjectiveChanged;
        public event Action<string, string> OnDialogueLineShown;
        public event Action<DialogueData> OnDialogueChoicesShown;
        public event Action OnDialogueClosed;
        public event Action<string> OnMissionCompletedBanner;
        public event Action<string> OnMissionFailed;
        public event Action OnEnteredFreeRoam;
        public event Action<TailWarningState, float> OnTailStatusChanged;

        public void Configure(MissionCatalog catalog, ObjectiveBeacon beacon)
        {
            missionCatalog = catalog;
            objectiveBeacon = beacon;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted += HandleInteracted;
                PlayerModeController.Instance.OnEnteredVehicle += HandleEnteredVehicle;
                PlayerModeController.Instance.OnExitedVehicle += HandleExitedVehicle;
            }

            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnContinueDialoguePressed += AdvanceDialogue;
                InputHub.Instance.OnRetryPressed += HandleRetryShortcut;
            }

            StartFromSavedProgress();
        }

        private void OnDestroy()
        {
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted -= HandleInteracted;
                PlayerModeController.Instance.OnEnteredVehicle -= HandleEnteredVehicle;
                PlayerModeController.Instance.OnExitedVehicle -= HandleExitedVehicle;
            }

            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnContinueDialoguePressed -= AdvanceDialogue;
                InputHub.Instance.OnRetryPressed -= HandleRetryShortcut;
            }
        }

        public void StartFromSavedProgress()
        {
            if (missionCatalog == null || missionCatalog.missions.Count == 0)
            {
                EnterFreeRoam();
                return;
            }

            SaveData save = SaveManager.Instance != null ? SaveManager.Instance.Data : new SaveData();
            MissionData nextMission = null;

            if (!string.IsNullOrEmpty(save.nextMissionId))
            {
                nextMission = missionCatalog.GetById(save.nextMissionId);
                if (nextMission != null && save.completedMissionIds.Contains(nextMission.missionId))
                {
                    nextMission = null;
                }
            }

            if (nextMission == null)
            {
                nextMission = missionCatalog.GetFirstUncompleted(save.completedMissionIds);
            }

            if (nextMission != null)
            {
                StartMission(nextMission);
            }
            else
            {
                EnterFreeRoam();
            }
        }

        public void StartMission(MissionData mission)
        {
            if (mission == null || mission.steps == null || mission.steps.Count == 0)
            {
                EnterFreeRoam();
                return;
            }

            StopAllCoroutines();
            _activeMission = mission;
            _currentStepIndex = -1;
            _isFailed = false;
            _isInFreeRoam = false;
            _hasActiveTimer = false;
            _tailWarningState = TailWarningState.None;

            AdvanceToNextStep();
        }

        public void EnterFreeRoam()
        {
            _activeMission = null;
            _currentStepIndex = -1;
            _currentTargetAnchor = null;
            _isFailed = false;
            _isInFreeRoam = true;
            _hasActiveTimer = false;
            _tailWarningState = TailWarningState.None;

            if (objectiveBeacon != null)
            {
                objectiveBeacon.SetVisible(false);
            }

            OnEnteredFreeRoam?.Invoke();
            OnObjectiveChanged?.Invoke(null, null);
        }

        public void RetryCurrentMission()
        {
            if (_activeMission == null)
            {
                return;
            }

            MissionData missionToRetry = _activeMission;
            _isFailed = false;
            CloseDialogueImmediate();

            // Reset player & vehicle to safe retry anchors.
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode != null)
            {
                WorldAnchor vehicleAnchor = WorldAnchorRegistry.Get(missionToRetry.retryVehicleAnchorId);
                if (vehicleAnchor != null && mode.PlayerSedan != null)
                {
                    mode.PlayerSedan.TeleportAndReset(vehicleAnchor.transform.position, vehicleAnchor.transform.rotation);
                    if (mode.PlayerSedan.Fuel < 0.25f)
                    {
                        mode.PlayerSedan.SetFuel(0.6f);
                    }
                }

                WorldAnchor playerAnchor = WorldAnchorRegistry.Get(missionToRetry.retryPlayerAnchorId);
                if (playerAnchor != null && mode.FootController != null)
                {
                    mode.FootController.TeleportTo(playerAnchor.transform.position, playerAnchor.transform.rotation);
                }

                if (missionToRetry.retryStartInVehicle && mode.HasVehicleKeys)
                {
                    mode.ForceVehicleMode(snapCamera: true);
                }
                else
                {
                    mode.SetOnFootMode(snapCamera: true);
                }
            }

            StartMission(missionToRetry);
        }

        public void ResetProgressAndRestart()
        {
            CloseDialogueImmediate();
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.ResetSave();
            }

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode != null)
            {
                WorldAnchor vehicleAnchor = WorldAnchorRegistry.Get("spawn_garage_sedan");
                if (vehicleAnchor != null && mode.PlayerSedan != null)
                {
                    mode.PlayerSedan.TeleportAndReset(vehicleAnchor.transform.position, vehicleAnchor.transform.rotation);
                    mode.PlayerSedan.SetFuel(0.8f);
                }

                WorldAnchor playerAnchor = WorldAnchorRegistry.Get("spawn_garage_foot");
                if (playerAnchor != null && mode.FootController != null)
                {
                    mode.FootController.TeleportTo(playerAnchor.transform.position, playerAnchor.transform.rotation);
                }

                mode.SetOnFootMode(snapCamera: true);
            }

            StartFromSavedProgress();
        }

        private void HandleRetryShortcut()
        {
            if (_isFailed)
            {
                RetryCurrentMission();
            }
        }

        private void AdvanceToNextStep()
        {
            if (_activeMission == null)
            {
                return;
            }

            _currentStepIndex++;
            _hasActiveTimer = false;
            _tailGraceTimer = 0f;
            _tailWarningState = TailWarningState.None;

            if (_currentStepIndex >= _activeMission.steps.Count)
            {
                CompleteActiveMission();
                return;
            }

            MissionStepData step = _activeMission.steps[_currentStepIndex];
            _currentTargetAnchor = WorldAnchorRegistry.Get(step.targetAnchorId);

            if (step.timeLimitSeconds > 0f)
            {
                _hasActiveTimer = true;
                _remainingTime = step.timeLimitSeconds;
            }

            UpdateBeaconForCurrentStep();
            OnObjectiveChanged?.Invoke(_activeMission, step);
        }

        private void UpdateBeaconForCurrentStep()
        {
            if (objectiveBeacon == null)
            {
                return;
            }

            MissionStepData step = CurrentStep;
            if (step == null || _currentTargetAnchor == null)
            {
                objectiveBeacon.SetVisible(false);
                return;
            }

            float radius = step.triggerRadius > 0f ? step.triggerRadius : _currentTargetAnchor.DefaultRadius;
            objectiveBeacon.SetTarget(_currentTargetAnchor.transform.position, radius);
        }

        private void Update()
        {
            MissionStepData step = CurrentStep;
            if (_activeMission == null || step == null || _isFailed || IsDialoguePlaying)
            {
                return;
            }

            if (_hasActiveTimer)
            {
                _remainingTime -= Time.deltaTime;
                if (_remainingTime <= 0f)
                {
                    _remainingTime = 0f;
                    FailActiveMission("ui.fail.timeout");
                    return;
                }
            }

            if (_currentTargetAnchor == null && !string.IsNullOrEmpty(step.targetAnchorId))
            {
                _currentTargetAnchor = WorldAnchorRegistry.Get(step.targetAnchorId);
                UpdateBeaconForCurrentStep();
            }

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || _currentTargetAnchor == null)
            {
                return;
            }

            // Follow moving anchors (like vehicles) with the 3D beacon.
            if (objectiveBeacon != null && objectiveBeacon.gameObject.activeSelf)
            {
                objectiveBeacon.transform.position = _currentTargetAnchor.transform.position;
            }

            Vector3 playerPos = mode.ActiveWorldPosition;
            float dist = Vector3.Distance(playerPos, _currentTargetAnchor.transform.position);

            if (step.stepType == MissionStepType.ReachLocation || step.stepType == MissionStepType.DeliverToZone)
            {
                bool modeMatches = (!step.requiresVehicle || mode.IsDriving) && (!step.requiresOnFoot || !mode.IsDriving);
                float radius = step.triggerRadius > 0f ? step.triggerRadius : _currentTargetAnchor.DefaultRadius;
                if (modeMatches && dist <= radius)
                {
                    CompleteCurrentStep(step);
                }
            }
            else if (step.stepType == MissionStepType.TailVehicle)
            {
                EvaluateTailStep(step, dist);
            }
        }

        private void EvaluateTailStep(MissionStepData step, float distance)
        {
            TailWarningState state = TailWarningState.Safe;
            if (distance < step.tailMinDistance)
            {
                state = TailWarningState.TooClose;
            }
            else if (distance > step.tailMaxDistance)
            {
                state = TailWarningState.TooFar;
            }

            _tailWarningState = state;
            OnTailStatusChanged?.Invoke(state, distance);

            if (state == TailWarningState.Safe)
            {
                _tailGraceTimer = Mathf.Max(0f, _tailGraceTimer - Time.deltaTime * 1.5f);
            }
            else
            {
                _tailGraceTimer += Time.deltaTime;
                if (_tailGraceTimer >= step.tailGraceSeconds)
                {
                    string reason = state == TailWarningState.TooClose ? "ui.fail.spotted" : "ui.fail.lost_target";
                    FailActiveMission(reason);
                }
            }
        }

        private void HandleInteracted(InteractableTrigger trigger)
        {
            MissionStepData step = CurrentStep;
            if (step == null || _isFailed || IsDialoguePlaying || trigger == null)
            {
                return;
            }

            bool isPrimaryMatch = string.Equals(trigger.InteractableId, step.targetAnchorId, StringComparison.OrdinalIgnoreCase);
            bool isSecondaryMatch = !string.IsNullOrEmpty(step.secondaryTargetAnchorId) &&
                                    string.Equals(trigger.InteractableId, step.secondaryTargetAnchorId, StringComparison.OrdinalIgnoreCase);

            if (!isPrimaryMatch && !isSecondaryMatch)
            {
                return;
            }

            if (step.stepType == MissionStepType.TalkToNpc || step.stepType == MissionStepType.InspectTarget)
            {
                // Hide picked-up mission packages when collected
                if (trigger.Kind == InteractableKind.PickupPackage ||
                    string.Equals(trigger.InteractableId, "tail_dropped_package", StringComparison.OrdinalIgnoreCase))
                {
                    trigger.gameObject.SetActive(false);
                }

                if (SaveManager.Instance != null)
                {
                    string choiceToRecord = isSecondaryMatch ? step.secondaryChoiceId : step.primaryChoiceId;
                    if (!string.IsNullOrEmpty(choiceToRecord))
                    {
                        SaveManager.Instance.Data.lastEndingChoice = choiceToRecord;
                        SaveManager.Instance.Save();
                    }
                }

                DialogueData chosenDialogue = (isSecondaryMatch && step.secondaryDialogueAsset != null)
                    ? step.secondaryDialogueAsset
                    : step.dialogueAsset;
                CompleteCurrentStepWithDialogue(step, chosenDialogue);
            }
        }

        public void NotifyTailTargetArrived()
        {
            MissionStepData step = CurrentStep;
            if (step == null || _isFailed || IsDialoguePlaying)
            {
                return;
            }

            if (step.stepType == MissionStepType.TailVehicle)
            {
                _tailWarningState = TailWarningState.None;
                OnTailStatusChanged?.Invoke(TailWarningState.None, 0f);
                CompleteCurrentStep(step);
            }
        }

        private void HandleEnteredVehicle(SedanVehicleController sedan)
        {
            MissionStepData step = CurrentStep;
            if (step == null || _isFailed || IsDialoguePlaying)
            {
                return;
            }

            if (step.stepType == MissionStepType.EnterVehicle)
            {
                CompleteCurrentStep(step);
            }
        }

        private void HandleExitedVehicle(SedanVehicleController sedan)
        {
            MissionStepData step = CurrentStep;
            if (step == null || _isFailed || IsDialoguePlaying)
            {
                return;
            }

            if (step.stepType == MissionStepType.ExitVehicle)
            {
                CompleteCurrentStep(step);
            }
        }

        private void CompleteCurrentStep(MissionStepData step)
        {
            CompleteCurrentStepWithDialogue(step, step != null ? step.dialogueAsset : null);
        }

        private void CompleteCurrentStepWithDialogue(MissionStepData step, DialogueData dialogue)
        {
            if (step != null && step.unlocksVehicleKeys && SaveManager.Instance != null)
            {
                SaveManager.Instance.SetVehicleKeysUnlocked(true);
            }

            if (dialogue != null && dialogue.lines != null && dialogue.lines.Count > 0)
            {
                StartDialogue(dialogue, AdvanceToNextStep);
            }
            else
            {
                AdvanceToNextStep();
            }
        }

        private void CompleteActiveMission()
        {
            MissionData finished = _activeMission;
            _hasActiveTimer = false;
            _tailWarningState = TailWarningState.None;

            if (objectiveBeacon != null)
            {
                objectiveBeacon.SetVisible(false);
            }

            if (SaveManager.Instance != null)
            {
                if (finished.rewardMoney > 0)
                {
                    SaveManager.Instance.AddMoney(finished.rewardMoney);
                }
                if (finished.missionId == "m14_hamidiye_zaferi")
                {
                    SaveManager.Instance.Data.chapterThreeCompleted = true;
                }
                SaveManager.Instance.MarkMissionCompleted(finished.missionId, finished.nextMissionId);
            }

            // Award Neighborhood Reputation XP & celebratory feedback
            int repReward = finished.missionId == "m14_hamidiye_zaferi" ? 200 : 60;
            NeighborhoodReputation.AddReputation(repReward);
            if (finished.missionId == "m14_hamidiye_zaferi")
            {
                GameFeelAudio.PlayMilestoneFanfare();
                if (PlayerModeController.Instance != null)
                {
                    GameFeelAudio.SpawnBurst(
                        PlayerModeController.Instance.ActiveWorldPosition + Vector3.up * 1.6f,
                        new Color(1.0f, 0.85f, 0.22f),
                        count: 24,
                        spread: 1.1f);
                }
            }

            string title = LocalizationService.Tr(finished.titleKey, finished.missionId);
            OnMissionCompletedBanner?.Invoke(title);

            StartCoroutine(PostMissionTransitionRoutine(finished));
        }

        private IEnumerator PostMissionTransitionRoutine(MissionData finished)
        {
            yield return new WaitForSeconds(missionCompleteBannerSeconds);

            MissionData next = missionCatalog != null ? missionCatalog.GetById(finished.nextMissionId) : null;
            if (finished.autoStartNextMission && next != null)
            {
                StartMission(next);
            }
            else
            {
                EnterFreeRoam();
            }
        }

        private void FailActiveMission(string reasonKey)
        {
            if (_isFailed)
            {
                return;
            }

            _isFailed = true;
            _hasActiveTimer = false;
            if (objectiveBeacon != null)
            {
                objectiveBeacon.SetVisible(false);
            }

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetDialogueActive(true);
            }

            OnMissionFailed?.Invoke(LocalizationService.Tr(reasonKey));
        }

        // --- Dialogue Playback ---
        public void StartDialogue(DialogueData data, Action onComplete)
        {
            _activeDialogue = data;
            _dialogueLineIndex = 0;
            _onDialogueCompleted = onComplete;
            _dialogueWaitingForChoice = false;

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetDialogueActive(true);
            }

            ShowCurrentDialogueLine();
        }

        /// <summary>
        /// Displays an immediate single-line or multi-line conversation with any street NPC
        /// using the main HUD dialogue panel without affecting active story mission state.
        /// </summary>
        public void ShowQuickNpcDialogue(string speakerName, string bodyText, Action onCompleted = null)
        {
            if (IsDialoguePlaying || _isFailed)
            {
                return;
            }

            DialogueData temp = ScriptableObject.CreateInstance<DialogueData>();
            temp.lines = new List<DialogueLine>
            {
                new DialogueLine { speakerKey = speakerName, textKey = bodyText }
            };
            temp.choices = new List<DialogueChoice>();
            StartDialogue(temp, () =>
            {
                Destroy(temp);
                onCompleted?.Invoke();
            });
        }

        public void AdvanceDialogue()
        {
            if (_activeDialogue == null || _dialogueWaitingForChoice)
            {
                return;
            }

            _dialogueLineIndex++;
            if (_dialogueLineIndex < _activeDialogue.lines.Count)
            {
                ShowCurrentDialogueLine();
            }
            else if (_activeDialogue.choices != null && _activeDialogue.choices.Count > 0)
            {
                _dialogueWaitingForChoice = true;
                OnDialogueChoicesShown?.Invoke(_activeDialogue);
            }
            else
            {
                FinishDialogue();
            }
        }

        public void SelectDialogueChoice(DialogueChoice choice)
        {
            if (_activeDialogue == null || choice == null)
            {
                return;
            }

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Data.lastEndingChoice = choice.choiceId;
                SaveManager.Instance.Save();
            }

            _dialogueWaitingForChoice = false;
            if (choice.followUpDialogue != null && choice.followUpDialogue.lines.Count > 0)
            {
                _activeDialogue = choice.followUpDialogue;
                _dialogueLineIndex = 0;
                ShowCurrentDialogueLine();
            }
            else
            {
                FinishDialogue();
            }
        }

        private void ShowCurrentDialogueLine()
        {
            DialogueLine line = _activeDialogue.lines[_dialogueLineIndex];
            string speaker = string.IsNullOrEmpty(line.speakerKey)
                ? string.Empty
                : LocalizationService.Tr(line.speakerKey, line.speakerKey);
            string text = LocalizationService.Tr(line.textKey, line.textKey);
            OnDialogueLineShown?.Invoke(speaker, text);
        }

        private void FinishDialogue()
        {
            Action callback = _onDialogueCompleted;
            CloseDialogueImmediate();
            callback?.Invoke();
        }

        private void CloseDialogueImmediate()
        {
            _activeDialogue = null;
            _dialogueLineIndex = 0;
            _onDialogueCompleted = null;
            _dialogueWaitingForChoice = false;

            if (PlayerModeController.Instance != null && !_isFailed)
            {
                PlayerModeController.Instance.SetDialogueActive(false);
            }

            OnDialogueClosed?.Invoke();
        }
    }
}
