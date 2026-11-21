using System;
using System.Collections;
using System.Collections.Generic;
using Gridroam.CameraSystem;
using Gridroam.Core;
using Gridroam.Input;
using Gridroam.Jobs;
using Gridroam.UI;
using Gridroam.Vehicle;
using Gridroam.World;
using UnityEngine;

namespace Gridroam.Player
{
    public enum ControlMode
    {
        OnFoot,
        Driving,
        Seated
    }

    /// <summary>
    /// Coordinates On-Foot <-> Driving <-> Seated mode transitions, vehicle key checks,
    /// multi-car driving, doors, sitting on furniture/benches, house painting, side jobs,
    /// shops, beds, fuel pumps, vending machines, modal input locks, and save/load state restoration.
    /// </summary>
    public class PlayerModeController : MonoBehaviour
    {
        public static PlayerModeController Instance { get; private set; }

        [Header("Core References")]
        [SerializeField] private PlayerFootController footController;
        [SerializeField] private SedanVehicleController playerSedan;
        [SerializeField] private ThirdPersonCameraRig cameraRig;

        [Header("Interaction")]
        [SerializeField] private float vehicleExitMaxSpeedKmh = 18f;
        [SerializeField] private float seatTransitionSeconds = 0.35f;

        private readonly HashSet<object> _modalLocks = new HashSet<object>();

        private SedanVehicleController _currentVehicle;
        private SeatInteractable _currentSeat;
        private Vector3 _seatApproachPos;
        private Quaternion _seatApproachRot = Quaternion.identity;
        private float _seatedEnteredTime;
        private bool _isSeatTransitioning;
        private Coroutine _seatRoutine;

        private InteractableTrigger[] _cachedInteractables;
        private InteractableTrigger _nearestInteractable;
        private InteractableTrigger _closestWithin4m;
        private bool _dialogueActive;
        private int _dialogueClosedFrame = -1;
        private int _interactConsumedFrame = -1;

        public ControlMode CurrentMode { get; private set; } = ControlMode.OnFoot;
        public bool IsDriving => CurrentMode == ControlMode.Driving;
        public bool IsSeated => CurrentMode == ControlMode.Seated;
        public bool IsModalLocked => _dialogueActive || _modalLocks.Count > 0;
        public bool HasVehicleKeys => SaveManager.Instance != null && SaveManager.Instance.Data.hasVehicleKeys;
        public PlayerFootController FootController => footController;
        public SedanVehicleController PlayerSedan => playerSedan;
        public SedanVehicleController CurrentVehicle => _currentVehicle;
        public InteractableTrigger NearestInteractable => _nearestInteractable;
        public InteractableTrigger ClosestInteractableWithin4m => _closestWithin4m;

        public Vector3 ActiveWorldPosition =>
            IsDriving && _currentVehicle != null
                ? _currentVehicle.transform.position
                : (footController != null ? footController.transform.position : transform.position);

        public event Action<ControlMode> OnModeChanged;
        public event Action<InteractableTrigger> OnInteractableChanged;
        public event Action<InteractableTrigger> OnInteracted;
        public event Action<SedanVehicleController> OnEnteredVehicle;
        public event Action<SedanVehicleController> OnExitedVehicle;
        public event Action<string> OnStatusMessage;

        public void ConfigureReferences(PlayerFootController foot, SedanVehicleController sedan, ThirdPersonCameraRig rig)
        {
            footController = foot;
            playerSedan = sedan;
            cameraRig = rig;
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
            if (footController == null) footController = FindAnyObjectByType<PlayerFootController>();
            if (playerSedan == null) playerSedan = FindAnyObjectByType<SedanVehicleController>();
            if (cameraRig == null) cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();

            RefreshInteractablesCache();

            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnInteractPressed += HandleInteractPressed;
                InputHub.Instance.OnExitVehiclePressed += HandleExitVehiclePressed;
            }

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnBeforeSave += HandleBeforeSave;
            }

            if (PlayerNeedsController.Instance != null)
            {
                PlayerNeedsController.Instance.OnNeedsMessage += RaiseStatusMessage;
            }

            RestoreSavedPlayerAndVehicleState();
        }

        private void OnDestroy()
        {
            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnInteractPressed -= HandleInteractPressed;
                InputHub.Instance.OnExitVehiclePressed -= HandleExitVehiclePressed;
            }

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnBeforeSave -= HandleBeforeSave;
            }

            if (PlayerNeedsController.Instance != null)
            {
                PlayerNeedsController.Instance.OnNeedsMessage -= RaiseStatusMessage;
            }
        }

        public void RaiseStatusMessage(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                OnStatusMessage?.Invoke(message);
            }
        }

        public void RefreshInteractablesCache()
        {
            _cachedInteractables = FindObjectsByType<InteractableTrigger>(FindObjectsInactive.Include);
        }

        public void SetModalLock(object source, bool locked)
        {
            if (source == null) return;

            bool wasLocked = IsModalLocked;
            if (locked)
            {
                _modalLocks.Add(source);
            }
            else
            {
                _modalLocks.Remove(source);
            }

            bool nowLocked = IsModalLocked;
            if (wasLocked && !nowLocked)
            {
                _dialogueClosedFrame = Time.frameCount;
            }

            ApplyLockToControllers(nowLocked);
        }

        public void SetDialogueActive(bool active)
        {
            if (_dialogueActive && !active)
            {
                _dialogueClosedFrame = Time.frameCount;
            }

            _dialogueActive = active;
            ApplyLockToControllers(IsModalLocked);
        }

        private void ApplyLockToControllers(bool locked)
        {
            if (footController != null)
            {
                footController.SetMovementEnabled(!locked && CurrentMode == ControlMode.OnFoot);
            }

            if (_currentVehicle != null)
            {
                _currentVehicle.SetInputLocked(locked);
            }
            if (playerSedan != null && playerSedan != _currentVehicle)
            {
                playerSedan.SetInputLocked(locked);
            }
        }

        private void HandleBeforeSave(SaveData data)
        {
            if (data == null) return;

            if (playerSedan != null)
            {
                data.missionSedanFuel = playerSedan.Fuel;
            }

            data.hasSavedPlayerState = true;
            if (IsDriving && _currentVehicle != null)
            {
                data.savedInVehicle = true;
                data.savedVehicleId = _currentVehicle.VehicleSaveId;
                Vector3 vp = _currentVehicle.transform.position;
                data.savedVehiclePosX = vp.x;
                data.savedVehiclePosY = vp.y;
                data.savedVehiclePosZ = vp.z;
                data.savedVehicleRotY = _currentVehicle.transform.eulerAngles.y;
                data.savedVehicleFuel = _currentVehicle.Fuel;

                Vector3 safeExit = _currentVehicle.GetSafeExitPosition();
                data.playerPosX = safeExit.x;
                data.playerPosY = safeExit.y;
                data.playerPosZ = safeExit.z;
                data.playerRotY = _currentVehicle.transform.eulerAngles.y;
            }
            else
            {
                data.savedInVehicle = false;
                data.savedVehicleId = string.Empty;
                Vector3 fp = IsSeated
                    ? _seatApproachPos
                    : (footController != null ? footController.transform.position : transform.position);
                data.playerPosX = fp.x;
                data.playerPosY = fp.y;
                data.playerPosZ = fp.z;
                data.playerRotY = footController != null ? footController.transform.eulerAngles.y : 0f;
            }
        }

        private void RestoreSavedPlayerAndVehicleState()
        {
            SaveData data = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (playerSedan != null && data != null)
            {
                playerSedan.SetFuel(data.missionSedanFuel);
            }

            if (data == null || !data.hasSavedPlayerState)
            {
                SetOnFootMode(snapCamera: true);
                return;
            }

            if (data.savedInVehicle && !string.IsNullOrEmpty(data.savedVehicleId))
            {
                SedanVehicleController targetCar = FindVehicleBySaveId(data.savedVehicleId);
                if (targetCar != null && (targetCar != playerSedan || HasVehicleKeys))
                {
                    Vector3 vPos = new Vector3(
                        Mathf.Clamp(data.savedVehiclePosX, -95f, 95f),
                        Mathf.Max(0.24f, data.savedVehiclePosY),
                        Mathf.Clamp(data.savedVehiclePosZ, -36f, 194f));
                    Quaternion vRot = Quaternion.Euler(0f, data.savedVehicleRotY, 0f);
                    targetCar.TeleportAndReset(vPos, vRot);
                    targetCar.SetFuel(data.savedVehicleFuel);
                    TryEnterVehicle(targetCar, snapCamera: true);
                    return;
                }
            }

            if (footController != null)
            {
                Vector3 fPos = new Vector3(
                    Mathf.Clamp(data.playerPosX, -95f, 95f),
                    Mathf.Max(0.10f, data.playerPosY),
                    Mathf.Clamp(data.playerPosZ, -36f, 194f));
                Quaternion fRot = Quaternion.Euler(0f, data.playerRotY, 0f);
                footController.TeleportTo(fPos, fRot);
            }

            SetOnFootMode(snapCamera: true);
        }

        private SedanVehicleController FindVehicleBySaveId(string saveId)
        {
            if (string.IsNullOrEmpty(saveId)) return null;
            SedanVehicleController[] cars = FindObjectsByType<SedanVehicleController>(FindObjectsInactive.Include);
            for (int i = 0; i < cars.Length; i++)
            {
                SedanVehicleController c = cars[i];
                if (c != null && string.Equals(c.VehicleSaveId, saveId, StringComparison.OrdinalIgnoreCase))
                {
                    return c;
                }
            }
            return null;
        }

        private void Update()
        {
            if (CurrentMode == ControlMode.Seated)
            {
                _closestWithin4m = null;
                SetNearestInteractable(null);
                if (!IsModalLocked &&
                    !_isSeatTransitioning &&
                    Time.time - _seatedEnteredTime > 0.40f &&
                    InputHub.Instance != null &&
                    InputHub.Instance.MoveInput.magnitude > 0.5f)
                {
                    TryStandUp();
                }
                return;
            }

            if (IsModalLocked || _isSeatTransitioning)
            {
                _closestWithin4m = null;
                SetNearestInteractable(null);
                return;
            }

            if (_cachedInteractables == null || _cachedInteractables.Length == 0)
            {
                RefreshInteractablesCache();
            }

            // While driving slowly, detect FuelPump and JobBoard triggers so player can refuel or take driving jobs
            if (CurrentMode == ControlMode.Driving)
            {
                _closestWithin4m = null;
                if (_currentVehicle == null || _currentVehicle.SpeedKmh > 8f)
                {
                    SetNearestInteractable(null);
                    return;
                }

                Vector3 carPos = _currentVehicle.transform.position;
                InteractableTrigger bestDrivingTrig = null;
                float bestDriveDistSq = float.MaxValue;

                foreach (InteractableTrigger trigger in _cachedInteractables)
                {
                    if (trigger == null || !trigger.isActiveAndEnabled) continue;
                    if (trigger.Kind != InteractableKind.FuelPump && trigger.Kind != InteractableKind.JobBoard)
                    {
                        continue;
                    }

                    float radius = Mathf.Max(4.2f, trigger.InteractionRadius);
                    float distSq = (trigger.transform.position - carPos).sqrMagnitude;
                    if (distSq <= radius * radius && distSq < bestDriveDistSq)
                    {
                        bestDrivingTrig = trigger;
                        bestDriveDistSq = distSq;
                    }
                }

                SetNearestInteractable(bestDrivingTrig);
                return;
            }

            if (CurrentMode != ControlMode.OnFoot || footController == null)
            {
                _closestWithin4m = null;
                SetNearestInteractable(null);
                return;
            }

            Vector3 playerPos = footController.transform.position;
            InteractableTrigger best = null;
            float bestDistSq = float.MaxValue;
            InteractableTrigger bestLook4m = null;
            float bestLookDistSq = 16.0f; // 4.0 m squared

            foreach (InteractableTrigger trigger in _cachedInteractables)
            {
                if (trigger == null || !trigger.isActiveAndEnabled) continue;
                // FuelPump is only interacted with while driving a vehicle
                if (trigger.Kind == InteractableKind.FuelPump) continue;

                float distSq = (trigger.transform.position - playerPos).sqrMagnitude;
                if (distSq <= bestLookDistSq)
                {
                    bestLook4m = trigger;
                    bestLookDistSq = distSq;
                }

                float radius = trigger.InteractionRadius;
                if (distSq <= radius * radius && distSq < bestDistSq)
                {
                    best = trigger;
                    bestDistSq = distSq;
                }
            }

            _closestWithin4m = bestLook4m;
            SetNearestInteractable(best);
        }

        private void SetNearestInteractable(InteractableTrigger next)
        {
            if (_nearestInteractable == next) return;
            _nearestInteractable = next;
            OnInteractableChanged?.Invoke(_nearestInteractable);
        }

        private void HandleInteractPressed()
        {
            if (IsModalLocked || Time.frameCount == _dialogueClosedFrame || _isSeatTransitioning)
            {
                return;
            }

            if (CurrentMode == ControlMode.Seated)
            {
                _interactConsumedFrame = Time.frameCount;
                TryStandUp();
                return;
            }

            if (CurrentMode == ControlMode.Driving)
            {
                if (_nearestInteractable == null) return;

                if (_nearestInteractable.Kind == InteractableKind.FuelPump)
                {
                    _interactConsumedFrame = Time.frameCount;
                    if (PlayerNeedsController.Instance != null)
                    {
                        PlayerNeedsController.Instance.TryRefuelVehicle(_currentVehicle);
                    }
                    return;
                }

                if (_nearestInteractable.Kind == InteractableKind.JobBoard)
                {
                    _interactConsumedFrame = Time.frameCount;
                    JobBoardPoint board = _nearestInteractable.GetComponentInParent<JobBoardPoint>();
                    if (board != null && JobManager.Instance != null)
                    {
                        JobManager.Instance.InteractWithJobBoard(board);
                    }
                    return;
                }

                return;
            }

            if (CurrentMode != ControlMode.OnFoot || _nearestInteractable == null)
            {
                return;
            }

            _interactConsumedFrame = Time.frameCount;

            switch (_nearestInteractable.Kind)
            {
                case InteractableKind.PlayerVehicle:
                case InteractableKind.TownVehicle:
                {
                    SedanVehicleController vehicle = _nearestInteractable.GetComponentInParent<SedanVehicleController>();
                    if (vehicle == null && _nearestInteractable.Kind == InteractableKind.PlayerVehicle)
                    {
                        vehicle = playerSedan;
                    }
                    TryEnterVehicle(vehicle);
                    return;
                }

                case InteractableKind.Door:
                {
                    DoorInteractable door = _nearestInteractable.GetComponentInParent<DoorInteractable>();
                    if (door != null)
                    {
                        door.Toggle(ActiveWorldPosition);
                    }
                    return;
                }

                case InteractableKind.Seat:
                {
                    SeatInteractable seat = _nearestInteractable.GetComponentInParent<SeatInteractable>();
                    if (seat != null && !seat.IsOccupied)
                    {
                        TrySitOnSeat(seat);
                    }
                    return;
                }

                case InteractableKind.PaintableHouse:
                {
                    PaintableHouse house = _nearestInteractable.GetComponentInParent<PaintableHouse>();
                    if (house != null && PaintPalettePanel.Instance != null)
                    {
                        PaintPalettePanel.Instance.Open(house);
                    }
                    return;
                }

                case InteractableKind.JobBoard:
                {
                    JobBoardPoint board = _nearestInteractable.GetComponentInParent<JobBoardPoint>();
                    if (board != null && JobManager.Instance != null)
                    {
                        JobManager.Instance.InteractWithJobBoard(board);
                    }
                    return;
                }

                case InteractableKind.ShopCounter:
                {
                    if (ShopMenuPanel.Instance != null)
                    {
                        ShopMenuPanel.Instance.Open(_nearestInteractable.InteractableId);
                    }
                    return;
                }

                case InteractableKind.Bed:
                {
                    if (PlayerNeedsController.Instance != null)
                    {
                        PlayerNeedsController.Instance.TrySleepInBed();
                    }
                    return;
                }

                case InteractableKind.VendingMachine:
                {
                    if (PlayerNeedsController.Instance != null)
                    {
                        PlayerNeedsController.Instance.TryBuyVendingDrink();
                    }
                    return;
                }

                case InteractableKind.TownCitizen:
                {
                    TownPedestrian citizen = _nearestInteractable.GetComponentInParent<TownPedestrian>();
                    if (citizen != null)
                    {
                        citizen.TalkToPlayer(ActiveWorldPosition);
                    }
                    return;
                }

                case InteractableKind.InspectObject:
                {
                    if (!string.IsNullOrEmpty(_nearestInteractable.InteractableId) &&
                        _nearestInteractable.InteractableId.StartsWith("mailbox", StringComparison.OrdinalIgnoreCase))
                    {
                        RaiseStatusMessage(LocalizationService.Tr("ui.mailbox.inspect", "Liman Kasabası Postası — Kutuda birkaç kartpostal var."));
                    }
                    OnInteracted?.Invoke(_nearestInteractable);
                    return;
                }

                default:
                    OnInteracted?.Invoke(_nearestInteractable);
                    break;
            }
        }

        private void HandleExitVehiclePressed()
        {
            if (IsModalLocked ||
                Time.frameCount == _dialogueClosedFrame ||
                Time.frameCount == _interactConsumedFrame ||
                CurrentMode != ControlMode.Driving)
            {
                return;
            }

            TryExitVehicle();
        }

        public void TryEnterVehicle()
        {
            TryEnterVehicle(playerSedan, snapCamera: false);
        }

        public void TryEnterVehicle(SedanVehicleController v, bool snapCamera = false)
        {
            if (v == null) return;

            InteractableTrigger trig = v.GetComponent<InteractableTrigger>();
            bool requiresKeys = (trig != null && trig.Kind == InteractableKind.PlayerVehicle) ||
                                (trig == null && v == playerSedan);

            if (requiresKeys && !HasVehicleKeys)
            {
                OnStatusMessage?.Invoke(LocalizationService.Tr("ui.hud.keys_locked"));
                return;
            }

            ClearActiveSeatImmediate();

            if (_currentVehicle != null && _currentVehicle != v)
            {
                _currentVehicle.SetPlayerDriving(false);
            }

            _currentVehicle = v;
            CurrentMode = ControlMode.Driving;
            _closestWithin4m = null;
            SetNearestInteractable(null);

            if (footController != null)
            {
                footController.SetCharacterActive(false);
            }

            _currentVehicle.SetPlayerDriving(true);
            _currentVehicle.SetInputLocked(IsModalLocked);

            if (cameraRig != null)
            {
                cameraRig.SetSeatedMode(false);
                cameraRig.SetTarget(_currentVehicle.transform, drivingMode: true, snapBehindTarget: snapCamera);
            }

            OnModeChanged?.Invoke(CurrentMode);
            OnEnteredVehicle?.Invoke(_currentVehicle);
        }

        public void TryExitVehicle()
        {
            SedanVehicleController activeVeh = _currentVehicle != null ? _currentVehicle : playerSedan;
            if (activeVeh == null || footController == null) return;

            if (activeVeh.SpeedKmh > vehicleExitMaxSpeedKmh)
            {
                return;
            }

            Vector3 exitPos = activeVeh.GetSafeExitPosition();
            Quaternion exitRot = Quaternion.LookRotation(activeVeh.transform.forward, Vector3.up);

            activeVeh.SetPlayerDriving(false);
            _currentVehicle = null;
            SetNearestInteractable(null);

            footController.SetCharacterActive(true);
            footController.TeleportTo(exitPos, exitRot);
            footController.SetMovementEnabled(!IsModalLocked);

            CurrentMode = ControlMode.OnFoot;
            if (cameraRig != null)
            {
                cameraRig.SetSeatedMode(false);
                cameraRig.SetTarget(footController.transform, drivingMode: false, snapBehindTarget: false);
            }

            OnModeChanged?.Invoke(CurrentMode);
            OnExitedVehicle?.Invoke(activeVeh);
        }

        public void TrySitOnSeat(SeatInteractable seat)
        {
            if (footController == null || seat == null || seat.IsOccupied || _isSeatTransitioning)
            {
                return;
            }

            _seatApproachPos = footController.transform.position;
            _seatApproachRot = footController.transform.rotation;
            _currentSeat = seat;
            _currentSeat.SetOccupied(true);
            _seatedEnteredTime = Time.time;

            CurrentMode = ControlMode.Seated;
            _closestWithin4m = null;
            SetNearestInteractable(null);

            footController.SetSeatedState(true);
            if (footController.Humanoid != null)
            {
                footController.Humanoid.SetSitting(true, driving: false);
            }

            if (cameraRig != null)
            {
                cameraRig.SetSeatedMode(true);
            }

            if (_seatRoutine != null)
            {
                StopCoroutine(_seatRoutine);
            }
            _seatRoutine = StartCoroutine(SitBlendRoutine(seat.SitPoint.position, seat.SitPoint.rotation));

            OnModeChanged?.Invoke(CurrentMode);
        }

        public void TryStandUp()
        {
            if (CurrentMode != ControlMode.Seated || _isSeatTransitioning || footController == null)
            {
                return;
            }

            Transform sitPt = _currentSeat != null ? _currentSeat.SitPoint : footController.transform;
            Vector3 flatForward = sitPt.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.001f)
            {
                flatForward = Vector3.forward;
            }
            flatForward.Normalize();

            Quaternion standRot = Quaternion.LookRotation(flatForward, Vector3.up);
            Vector3 candidate = sitPt.position + flatForward * 0.6f;
            candidate.y = _seatApproachPos.y;

            Vector3 rayOrigin = new Vector3(candidate.x, sitPt.position.y + 0.45f, candidate.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit floorHit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (floorHit.point.y < sitPt.position.y - 0.15f)
                {
                    candidate.y = floorHit.point.y + 0.02f;
                }
            }

            Vector3 capBottom = candidate + Vector3.up * 0.46f;
            Vector3 capTop = candidate + Vector3.up * 1.42f;
            bool capsuleFree = !Physics.CheckCapsule(capBottom, capTop, 0.34f, ~0, QueryTriggerInteraction.Ignore);
            Vector3 finalPos = capsuleFree ? candidate : _seatApproachPos;

            if (footController.Humanoid != null)
            {
                footController.Humanoid.SetSitting(false, driving: false);
            }
            if (cameraRig != null)
            {
                cameraRig.SetSeatedMode(false);
            }

            if (_seatRoutine != null)
            {
                StopCoroutine(_seatRoutine);
            }
            _seatRoutine = StartCoroutine(StandUpRoutine(finalPos, standRot));
        }

        private IEnumerator SitBlendRoutine(Vector3 targetPos, Quaternion targetRot)
        {
            _isSeatTransitioning = true;
            Vector3 startPos = footController.transform.position;
            Quaternion startRot = footController.transform.rotation;
            float duration = Mathf.Max(0.05f, seatTransitionSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                footController.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPos, targetPos, t),
                    Quaternion.Slerp(startRot, targetRot, t));
                yield return null;
            }

            footController.transform.SetPositionAndRotation(targetPos, targetRot);
            _isSeatTransitioning = false;
            _seatRoutine = null;
        }

        private IEnumerator StandUpRoutine(Vector3 targetPos, Quaternion targetRot)
        {
            _isSeatTransitioning = true;
            Vector3 startPos = footController.transform.position;
            Quaternion startRot = footController.transform.rotation;
            float duration = Mathf.Max(0.05f, seatTransitionSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                footController.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPos, targetPos, t),
                    Quaternion.Slerp(startRot, targetRot, t));
                yield return null;
            }

            if (_currentSeat != null)
            {
                _currentSeat.SetOccupied(false);
                _currentSeat = null;
            }

            footController.SetSeatedState(false);
            footController.TeleportTo(targetPos, targetRot);
            footController.SetMovementEnabled(!IsModalLocked);

            _isSeatTransitioning = false;
            _seatRoutine = null;
            CurrentMode = ControlMode.OnFoot;
            OnModeChanged?.Invoke(CurrentMode);
        }

        private void ClearActiveSeatImmediate()
        {
            if (_seatRoutine != null)
            {
                StopCoroutine(_seatRoutine);
                _seatRoutine = null;
            }

            _isSeatTransitioning = false;

            if (_currentSeat != null)
            {
                _currentSeat.SetOccupied(false);
                _currentSeat = null;
            }

            if (footController != null && footController.Humanoid != null)
            {
                footController.Humanoid.SetSitting(false, driving: false);
            }

            if (cameraRig != null)
            {
                cameraRig.SetSeatedMode(false);
            }
        }

        public void SetOnFootMode(bool snapCamera)
        {
            ClearActiveSeatImmediate();
            CurrentMode = ControlMode.OnFoot;

            if (_currentVehicle != null)
            {
                _currentVehicle.SetPlayerDriving(false);
                _currentVehicle = null;
            }
            if (playerSedan != null)
            {
                playerSedan.SetPlayerDriving(false);
            }

            if (footController != null)
            {
                footController.SetCharacterActive(true);
                footController.SetMovementEnabled(!IsModalLocked);
                if (cameraRig != null)
                {
                    cameraRig.SetTarget(footController.transform, drivingMode: false, snapBehindTarget: snapCamera);
                }
            }

            OnModeChanged?.Invoke(CurrentMode);
        }

        public void ForceVehicleMode(bool snapCamera)
        {
            if (playerSedan == null) return;

            ClearActiveSeatImmediate();

            if (_currentVehicle != null && _currentVehicle != playerSedan)
            {
                _currentVehicle.SetPlayerDriving(false);
            }

            _currentVehicle = playerSedan;
            CurrentMode = ControlMode.Driving;

            if (footController != null)
            {
                footController.SetCharacterActive(false);
            }

            _currentVehicle.SetPlayerDriving(true);
            _currentVehicle.SetInputLocked(IsModalLocked);

            if (cameraRig != null)
            {
                cameraRig.SetTarget(_currentVehicle.transform, drivingMode: true, snapBehindTarget: snapCamera);
            }

            OnModeChanged?.Invoke(CurrentMode);
        }
    }
}
