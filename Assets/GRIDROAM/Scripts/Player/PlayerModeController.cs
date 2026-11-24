using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Jobs;
using OurWorld.UI;
using OurWorld.Vehicle;
using OurWorld.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OurWorld.Player
{
    public enum ControlMode
    {
        OnFoot,
        Driving,
        Seated,
        Cycling,
        Tennis,
        ArenaSport
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
        private BicycleController _currentBicycle;
        private TennisCourt _currentTennisCourt;
        private IPlayableSportArena _currentArenaSport;
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
        public BicycleController CurrentBicycle => _currentBicycle;
        public TennisCourt CurrentTennisCourt => _currentTennisCourt;
        public IPlayableSportArena CurrentArenaSport => _currentArenaSport;
        public InteractableTrigger NearestInteractable => _nearestInteractable;
        public InteractableTrigger ClosestInteractableWithin4m => _closestWithin4m;

        public Vector3 ActiveWorldPosition =>
            IsDriving && _currentVehicle != null
                ? _currentVehicle.transform.position
                : _currentBicycle != null ? _currentBicycle.transform.position
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

            if (footController != null) NeighborhoodSports.Install(gameObject.scene, footController.Humanoid);
            RefreshInteractablesCache();

            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnInteractPressed += HandleInteractPressed;
                InputHub.Instance.OnExitVehiclePressed += HandleExitVehiclePressed;
                InputHub.Instance.OnAttackPressed += HandleSportAction;
                InputHub.Instance.OnAttackReleased += HandleSportActionReleased;
                InputHub.Instance.OnJumpPressed += HandleSportSecondaryAction;
                InputHub.Instance.OnJumpReleased += HandleSportSecondaryActionReleased;
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
                InputHub.Instance.OnAttackPressed -= HandleSportAction;
                InputHub.Instance.OnAttackReleased -= HandleSportActionReleased;
                InputHub.Instance.OnJumpPressed -= HandleSportSecondaryAction;
                InputHub.Instance.OnJumpReleased -= HandleSportSecondaryActionReleased;
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
            EnforceSingleHomeBedPolicy();
        }

        private void EnforceSingleHomeBedPolicy()
        {
            if (_cachedInteractables == null) return;
            for (int i = 0; i < _cachedInteractables.Length; i++)
            {
                InteractableTrigger trig = _cachedInteractables[i];
                if (trig == null || trig.Kind != InteractableKind.Bed) continue;

                if (IsPlayerHomeBed(trig))
                {
                    trig.enabled = true;
                    trig.SetCustomPrompt("Kendi Yatağında Uyu (Evim · No:48)");
                }
                else
                {
                    // Only the player's own home (No:48 Selman Sokağı) has a sleepable bed!
                    trig.enabled = false;
                }
            }
        }

        public static bool IsPlayerHomeBed(InteractableTrigger trig)
        {
            if (trig == null) return false;
            if (string.Equals(trig.InteractableId, "bed_player_home_48", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            Transform cur = trig.transform;
            while (cur != null)
            {
                if (cur.name == "Selman_No48_HeroHouse") return true;
                cur = cur.parent;
            }
            Vector3 homeCenter = new Vector3(-68.0f, 0f, 9.55f);
            Vector3 d = trig.transform.position - homeCenter;
            d.y = 0f;
            return d.sqrMagnitude <= 10.5f * 10.5f;
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
                float yaw = footController != null ? footController.transform.eulerAngles.y : 0f;
                if (_currentBicycle != null && _currentBicycle.TryGetDismount(out Vector3 dismount))
                {
                    fp = dismount;
                }
                if (_currentTennisCourt != null)
                {
                    fp = _currentTennisCourt.ReturnPosition;
                    yaw = _currentTennisCourt.ReturnRotation.eulerAngles.y;
                }
                if (_currentArenaSport != null)
                {
                    fp = _currentArenaSport.ReturnPosition;
                    yaw = _currentArenaSport.ReturnRotation.eulerAngles.y;
                }
                data.playerPosX = fp.x;
                data.playerPosY = fp.y;
                data.playerPosZ = fp.z;
                data.playerRotY = yaw;
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
                    bool carPosValid = data.savedVehiclePosX >= -248f && data.savedVehiclePosX <= 248f &&
                                       data.savedVehiclePosZ >= -248f && data.savedVehiclePosZ <= 248f &&
                                       data.savedVehiclePosY >= -0.5f && data.savedVehiclePosY <= 12f;
                    Vector3 vPos = carPosValid
                        ? new Vector3(data.savedVehiclePosX, Mathf.Max(0.24f, data.savedVehiclePosY), data.savedVehiclePosZ)
                        : targetCar.transform.position;
                    Quaternion vRot = carPosValid ? Quaternion.Euler(0f, data.savedVehicleRotY, 0f) : targetCar.transform.rotation;
                    targetCar.TeleportAndReset(vPos, vRot);
                    targetCar.SetFuel(data.savedVehicleFuel);
                    TryEnterVehicle(targetCar, snapCamera: true);
                    return;
                }
            }

            if (footController != null)
            {
                bool footPosValid = data.playerPosX >= -248f && data.playerPosX <= 248f &&
                                    data.playerPosZ >= -248f && data.playerPosZ <= 248f &&
                                    data.playerPosY >= -0.5f && data.playerPosY <= 12f;
                Vector3 fPos = footPosValid
                    ? new Vector3(data.playerPosX, Mathf.Max(0.10f, data.playerPosY), data.playerPosZ)
                    : new Vector3(-68.0f, 0.12f, -2.2f);
                Quaternion fRot = footPosValid ? Quaternion.Euler(0f, data.playerRotY, 0f) : Quaternion.identity;
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

        private int _fastTravelIndex;

        private static readonly (string label, Vector3 pos, float yaw)[] FastTravelSpots =
        {
            ("No:48 Selman Sokağı · Benim Evim",    new Vector3( -62.0f, 0.12f,    4.5f),   0f),
            ("Aydos Yeşil Vadi · Çiftlik & Bostan", new Vector3( 136.0f, 0.12f,    0.0f),  90f),
            ("Sultanbeyli Göleti & Balıkçılık",     new Vector3(-164.0f, 0.12f,    0.0f), -90f),
            ("Aydos Tazyikli Oto Yıkama",           new Vector3( 124.0f, 0.12f,   -4.5f), 180f),
            ("No:73 Oto Tamir & Modifiye",          new Vector3(  32.0f, 0.12f,    4.2f),   0f),
            ("Hamidiye Halı Saha · Futbol",         new Vector3( -14.0f, 0.12f,  -29.0f), 180f),
            ("Hamidiye Basketbol & Voleybol",       new Vector3(  49.0f, 0.12f,  -31.0f), 180f),
            ("No:58 Selman Bakkal & ATM",           new Vector3(   6.5f, 0.12f,    4.5f),   0f),
            ("Hz. Osman Camii Avlusu",              new Vector3(  82.0f, 0.12f,  -24.0f), 180f),
            ("Fatih Bulvarı · Saat Kulesi",         new Vector3(   0.0f, 0.18f,  134.0f),   0f),
            ("Petrol Yolu Caddesi",                 new Vector3( -18.0f, 0.12f,  -78.0f),  90f),
            ("Aziziye Cd. · İlkokul",               new Vector3( -16.0f, 0.12f, -114.0f), 180f)
        };

        private void Update()
        {
            if (!IsModalLocked && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame && DayNightCycle.Instance != null)
            {
                float h = DayNightCycle.Instance.CurrentHour;
                float nextHour;
                string label;
                if (h < 11.5f) { nextHour = 13.0f; label = "Öğle (13:00 · Güneşli İstanbul)"; }
                else if (h < 17.5f) { nextHour = 18.75f; label = "Gün Batımı (18:45 · Altın Saat)"; }
                else if (h < 20.5f) { nextHour = 21.5f; label = "İstanbul Gecesi (21:30 · Sokak Lambaları)"; }
                else { nextHour = 9.0f; label = "Sabah (09:00 · Taze Sultanbeyli Sabahı)"; }

                DayNightCycle.Instance.SetTimeOfDay(nextHour);
                GameFeelAudio.PlayUiClick();
                RaiseStatusMessage($"Saat değiştirildi: {label}");
            }

            // Quick Fast-Travel (G key on foot) between Selman Sokağı's 5 iconic spots!
            if (!IsModalLocked && CurrentMode == ControlMode.OnFoot && !MosquePrayerInteractable.IsPlayerPraying &&
                footController != null && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
            {
                CarryableProp.DropHeldPropIfAny();
                StreetCatController.CancelActivePet();
                var spot = FastTravelSpots[_fastTravelIndex % FastTravelSpots.Length];
                _fastTravelIndex = (_fastTravelIndex + 1) % FastTravelSpots.Length;
                footController.TeleportTo(spot.pos, Quaternion.Euler(0f, spot.yaw, 0f));
                GameFeelAudio.PlayUiClick();
                GameFeelAudio.SpawnBurst(spot.pos + Vector3.up * 1.1f, new Color(0.32f, 0.84f, 0.80f), count: 10, spread: 0.55f);
                RaiseStatusMessage($"Hızlı Geçiş: {spot.label}");
            }

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
                    if (trigger.Kind != InteractableKind.FuelPump &&
                        trigger.Kind != InteractableKind.JobBoard &&
                        trigger.Kind != InteractableKind.CarWashStation &&
                        trigger.Kind != InteractableKind.GarageModBoard)
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
            Vector3 playerChest = playerPos + Vector3.up * 1.15f;
            Vector3 playerFwd = footController.transform.forward;
            playerFwd.y = 0f;
            if (playerFwd.sqrMagnitude > 0.001f) playerFwd.Normalize();

            InteractableTrigger best = null;
            float bestScore = float.MaxValue;
            InteractableTrigger bestLook4m = null;
            float bestLookDistSq = 16.0f; // 4.0 m squared

            foreach (InteractableTrigger trigger in _cachedInteractables)
            {
                if (trigger == null || !trigger.isActiveAndEnabled) continue;
                // FuelPump is only interacted with while driving a vehicle
                if (trigger.Kind == InteractableKind.FuelPump) continue;

                Vector3 trigPos = trigger.transform.position;
                Vector3 delta = trigPos - playerPos;
                // Ignore interactables on another floor (> 2.3 m vertical gap)
                if (Mathf.Abs(delta.y) > 2.3f) continue;

                float distSq = delta.sqrMagnitude;
                if (distSq <= bestLookDistSq)
                {
                    bestLook4m = trigger;
                    bestLookDistSq = distSq;
                }

                float radius = trigger.InteractionRadius;
                if (distSq <= radius * radius)
                {
                    // Prevent selecting triggers behind solid walls (except doors, vehicles & outdoor sports arenas)
                    bool checkLos = trigger.Kind != InteractableKind.Door &&
                                    trigger.Kind != InteractableKind.PlayerVehicle &&
                                    trigger.Kind != InteractableKind.TownVehicle &&
                                    trigger.Kind != InteractableKind.PaintableHouse &&
                                    trigger.Kind != InteractableKind.Tennis &&
                                    trigger.Kind != InteractableKind.SportsArena &&
                                    trigger.Kind != InteractableKind.FarmPlot &&
                                    trigger.Kind != InteractableKind.FarmAnimal &&
                                    trigger.Kind != InteractableKind.FarmMarket &&
                                    trigger.Kind != InteractableKind.FarmBoard &&
                                    trigger.Kind != InteractableKind.GarageRepairBay &&
                                    trigger.Kind != InteractableKind.GarageModBoard &&
                                    trigger.Kind != InteractableKind.FishingPier &&
                                    trigger.Kind != InteractableKind.CarWashStation &&
                                    trigger.Kind != InteractableKind.HomeDecorBoard &&
                                    trigger.Kind != InteractableKind.HomeMangal;
                    if (checkLos && distSq > 0.45f)
                    {
                        float probeUp = trigger.Kind == InteractableKind.ShopItem ? 0.12f : 0.85f;
                        Vector3 trigProbe = trigPos + Vector3.up * probeUp;
                        if (Physics.Linecast(playerChest, trigProbe, out RaycastHit wallHit, ~0, QueryTriggerInteraction.Ignore))
                        {
                            Transform hitTr = wallHit.transform;
                            if (hitTr != null &&
                                hitTr != footController.transform &&
                                !hitTr.IsChildOf(footController.transform) &&
                                hitTr != trigger.transform &&
                                !hitTr.IsChildOf(trigger.transform) &&
                                !trigger.transform.IsChildOf(hitTr))
                            {
                                continue;
                            }
                        }
                    }

                    // Facing-weighted score: prefers interactables in front of player when two overlap
                    Vector3 flatDir = new Vector3(delta.x, 0f, delta.z);
                    float facingBonus = 0f;
                    if (flatDir.sqrMagnitude > 0.04f)
                    {
                        float dot = Vector3.Dot(playerFwd, flatDir.normalized);
                        float weight = trigger.Kind == InteractableKind.ShopItem ? 0.85f : 0.55f;
                        facingBonus = (1f - dot) * weight;
                    }
                    float score = Mathf.Sqrt(distSq) + facingBonus;
                    if (score < bestScore)
                    {
                        best = trigger;
                        bestScore = score;
                    }
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

            if (CurrentMode == ControlMode.Tennis)
            {
                _interactConsumedFrame = Time.frameCount;
                ExitTennis();
                return;
            }

            if (CurrentMode == ControlMode.ArenaSport)
            {
                _interactConsumedFrame = Time.frameCount;
                ExitArenaSport();
                return;
            }

            if (MosquePrayerInteractable.IsPlayerPraying)
            {
                _interactConsumedFrame = Time.frameCount;
                MosquePrayerInteractable.CancelActivePrayerIfAny();
                return;
            }

            if (CurrentMode == ControlMode.OnFoot && CarryableProp.CurrentlyHeld != null)
            {
                _interactConsumedFrame = Time.frameCount;
                CarryableProp.CurrentlyHeld.ToggleCarry(this);
                return;
            }

            if (CurrentMode == ControlMode.OnFoot && StreetCatController.ActivePet != null)
            {
                StreetCatController.CancelActivePet();
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

                if (_nearestInteractable.Kind == InteractableKind.CarWashStation)
                {
                    _interactConsumedFrame = Time.frameCount;
                    if (CarWashStationController.Instance != null)
                    {
                        CarWashStationController.Instance.StartWash(this);
                    }
                    return;
                }

                if (_nearestInteractable.Kind == InteractableKind.GarageModBoard)
                {
                    _interactConsumedFrame = Time.frameCount;
                    AutoGaragePanelController.EnsureInstance().Open(initialTab: 1);
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
                case InteractableKind.Bicycle:
                case InteractableKind.Motorcycle:
                {
                    BicycleController bicycle = _nearestInteractable.GetComponentInParent<BicycleController>();
                    if (bicycle == null || footController == null) return;
                    _currentBicycle = bicycle;
                    bicycle.Mount(footController);
                    SetSportMode(ControlMode.Cycling, bicycle.transform);
                    return;
                }

                case InteractableKind.Tennis:
                {
                    TennisCourt court = _nearestInteractable.GetComponentInParent<TennisCourt>();
                    if (court == null || footController == null) return;
                    StartTennisMatch(court);
                    return;
                }

                case InteractableKind.SportsArena:
                {
                    IPlayableSportArena arena = _nearestInteractable.GetComponentInParent<IPlayableSportArena>();
                    if (arena == null || footController == null) return;
                    StartArenaSport(arena);
                    return;
                }

                case InteractableKind.PlayerVehicle:
                case InteractableKind.TownVehicle:
                {
                    SedanVehicleController vehicle = _nearestInteractable.GetComponentInParent<SedanVehicleController>();
                    if (vehicle == null && _nearestInteractable.Kind == InteractableKind.TownVehicle && AmbientTrafficManager.Instance != null)
                    {
                        vehicle = AmbientTrafficManager.Instance.TryHijackTrafficCar(_nearestInteractable.gameObject);
                    }
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
                        door.Toggle(ActiveWorldPosition, footController != null ? footController.Humanoid : null);
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

                case InteractableKind.ShopItem:
                {
                    PhysicalShopItem shopItem = _nearestInteractable.GetComponentInParent<PhysicalShopItem>();
                    if (shopItem != null)
                    {
                        shopItem.Use(this);
                    }
                    return;
                }

                case InteractableKind.ShopCounter:
                {
                    if (PlayerShoppingCart.HasActiveItems)
                    {
                        PlayerShoppingCart cart = PlayerShoppingCart.GetOrCreate(this);
                        if (cart != null)
                        {
                            cart.Checkout(this, _nearestInteractable.transform);
                        }
                    }
                    else if (FarmEconomyManager.GetTotalProduceCount() > 0)
                    {
                        FarmEconomyManager.SellAllProduce(_nearestInteractable.transform.position);
                    }
                    else
                    {
                        GameFeelAudio.PlayUiClick();
                        RaiseStatusMessage("Raflardan almak istediğin ürüne yaklaşıp eline al, ya da çiftlik/bostan mahsullerini getirip kasada sat!");
                    }
                    return;
                }

                case InteractableKind.Bed:
                {
                    if (!IsPlayerHomeBed(_nearestInteractable))
                    {
                        RaiseStatusMessage("Burası komşunun evi! Sadece No:48 Selman Sokağı'ndaki kendi evinde uyuyabilirsin.");
                        return;
                    }
                    if (PlayerNeedsController.Instance != null)
                    {
                        PlayerNeedsController.Instance.TrySleepInBed(_nearestInteractable.transform);
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
                        if (footController != null && footController.Humanoid != null)
                        {
                            footController.Humanoid.PlayWave();
                        }
                        citizen.TalkToPlayer(ActiveWorldPosition);
                    }
                    return;
                }

                case InteractableKind.HomeAppliance:
                {
                    HomeApplianceInteractable appliance = _nearestInteractable.GetComponentInParent<HomeApplianceInteractable>();
                    if (appliance != null)
                    {
                        appliance.Use(this);
                    }
                    return;
                }

                case InteractableKind.MosquePrayer:
                {
                    MosquePrayerInteractable prayer = _nearestInteractable.GetComponentInParent<MosquePrayerInteractable>();
                    if (prayer != null)
                    {
                        prayer.Interact(this);
                    }
                    return;
                }

                case InteractableKind.StreetCat:
                {
                    StreetCatController cat = _nearestInteractable.GetComponentInParent<StreetCatController>();
                    if (cat != null)
                    {
                        cat.Pet(this);
                    }
                    return;
                }

                case InteractableKind.Football:
                {
                    StreetFootball ball = _nearestInteractable.GetComponentInParent<StreetFootball>();
                    if (ball != null)
                    {
                        ball.KickFromPlayer(this, 1.05f);
                    }
                    return;
                }

                case InteractableKind.CarryableProp:
                {
                    CarryableProp prop = _nearestInteractable.GetComponentInParent<CarryableProp>();
                    if (prop != null)
                    {
                        prop.ToggleCarry(this);
                    }
                    return;
                }

                case InteractableKind.AtmTerminal:
                {
                    AtmTerminalInteractable atm = _nearestInteractable.GetComponentInParent<AtmTerminalInteractable>();
                    if (atm != null)
                    {
                        atm.Use(this);
                    }
                    return;
                }

                case InteractableKind.FarmPlot:
                {
                    FarmPlotInteractable plot = _nearestInteractable.GetComponentInParent<FarmPlotInteractable>();
                    if (plot != null)
                    {
                        plot.Interact(this);
                    }
                    return;
                }

                case InteractableKind.FarmAnimal:
                {
                    FarmAnimalController animal = _nearestInteractable.GetComponentInParent<FarmAnimalController>();
                    if (animal != null)
                    {
                        animal.Interact(this);
                    }
                    return;
                }

                case InteractableKind.FarmMarket:
                {
                    if (FarmEconomyManager.GetTotalProduceCount() > 0)
                    {
                        FarmEconomyManager.SellAllProduce(_nearestInteractable.transform.position);
                    }
                    else
                    {
                        FarmPanelController.EnsureInstance().Open();
                    }
                    return;
                }

                case InteractableKind.FarmBoard:
                {
                    FarmPanelController.EnsureInstance().Open();
                    return;
                }

                case InteractableKind.GarageRepairBay:
                {
                    AutoGaragePanelController.EnsureInstance().Open(initialTab: 0);
                    return;
                }

                case InteractableKind.GarageModBoard:
                {
                    AutoGaragePanelController.EnsureInstance().Open(initialTab: 1);
                    return;
                }

                case InteractableKind.FishingPier:
                {
                    if (LakeFishingController.Instance != null)
                    {
                        LakeFishingController.Instance.BeginFishing(this);
                    }
                    return;
                }

                case InteractableKind.CarWashStation:
                {
                    if (CarWashStationController.Instance != null)
                    {
                        CarWashStationController.Instance.StartWash(this);
                    }
                    return;
                }

                case InteractableKind.HomeDecorBoard:
                {
                    HomeDecorPanelController.EnsureInstance().Open();
                    return;
                }

                case InteractableKind.HomeMangal:
                {
                    if (HomeDecorManager.Instance != null)
                    {
                        HomeDecorManager.Instance.InteractWithBackyardMangal(this);
                    }
                    return;
                }

                case InteractableKind.InspectObject:
                {
                    InteractableTrigger inspected = _nearestInteractable;
                    PlayerActionAnimator.Play(this, inspected.transform, PlayerActionKind.InspectReach, onClimax: () =>
                    {
                        if (inspected == null) return;
                        if (!string.IsNullOrEmpty(inspected.InteractableId) &&
                            inspected.InteractableId.StartsWith("mailbox", StringComparison.OrdinalIgnoreCase))
                        {
                            RaiseStatusMessage(LocalizationService.Tr("ui.mailbox.inspect", "Hamidiye Mahallesi Postası — Kutuda birkaç kartpostal var."));
                        }
                        OnInteracted?.Invoke(inspected);
                    });
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
                Time.frameCount == _interactConsumedFrame)
            {
                return;
            }

            switch (CurrentMode)
            {
                case ControlMode.Driving:
                    TryExitVehicle();
                    break;
                case ControlMode.Cycling:
                    ExitBicycle();
                    break;
                case ControlMode.Tennis:
                    ExitTennis();
                    break;
                case ControlMode.ArenaSport:
                    ExitArenaSport();
                    break;
            }
        }

        public void StartTennisMatch(TennisCourt court, Vector3? exitPositionOverride = null, Quaternion? exitRotationOverride = null)
        {
            if (court == null || footController == null) return;
            PrepareForDirectSportEntry(exitPositionOverride, exitRotationOverride);
            _currentTennisCourt = court;
            court.Begin(footController);
            SetSportMode(ControlMode.Tennis, footController.transform);
        }

        public void StartArenaSport(IPlayableSportArena arena, Vector3? exitPositionOverride = null, Quaternion? exitRotationOverride = null)
        {
            if (arena == null || footController == null) return;
            PrepareForDirectSportEntry(exitPositionOverride, exitRotationOverride);
            _currentArenaSport = arena;
            arena.BeginPlayerMatch(footController);
            SetSportMode(ControlMode.ArenaSport, footController.transform);
        }

        private void PrepareForDirectSportEntry(Vector3? exitPositionOverride, Quaternion? exitRotationOverride)
        {
            if (CurrentMode != ControlMode.OnFoot)
            {
                SetOnFootMode(snapCamera: false);
            }
            CarryableProp.DropHeldPropIfAny();
            StreetCatController.CancelActivePet();
            MosquePrayerInteractable.CancelActivePrayerIfAny();
            if (exitPositionOverride.HasValue)
            {
                Quaternion rot = exitRotationOverride ?? Quaternion.identity;
                footController.TeleportTo(exitPositionOverride.Value, rot);
            }
        }

        private void SetSportMode(ControlMode mode, Transform cameraTarget)
        {
            CurrentMode = mode;
            _closestWithin4m = null;
            SetNearestInteractable(null);
            if (cameraRig != null)
            {
                cameraRig.SetSeatedMode(false);
                cameraRig.SetTarget(cameraTarget, drivingMode: mode == ControlMode.Cycling, snapBehindTarget: true);
            }
            OnModeChanged?.Invoke(mode);
        }

        private void HandleSportAction()
        {
            if (IsModalLocked ||
                Time.frameCount == _interactConsumedFrame ||
                Time.frameCount == _dialogueClosedFrame)
            {
                return;
            }

            if (_currentTennisCourt != null)
            {
                _currentTennisCourt.Hit();
                return;
            }

            if (_currentArenaSport != null)
            {
                _currentArenaSport.TriggerPrimaryAction();
            }
        }

        private void HandleSportSecondaryAction()
        {
            if (IsModalLocked ||
                Time.frameCount == _interactConsumedFrame ||
                Time.frameCount == _dialogueClosedFrame)
            {
                return;
            }

            if (_currentTennisCourt != null)
            {
                _currentTennisCourt.TriggerSecondaryAction();
                return;
            }

            if (_currentArenaSport != null)
            {
                _currentArenaSport.TriggerSecondaryAction();
            }
        }

        private void HandleSportActionReleased(float holdSeconds)
        {
            if (IsModalLocked) return;
            if (_currentTennisCourt != null)
            {
                _currentTennisCourt.TriggerPrimaryRelease(holdSeconds);
                return;
            }
            if (_currentArenaSport != null)
            {
                _currentArenaSport.TriggerPrimaryRelease(holdSeconds);
            }
        }

        private void HandleSportSecondaryActionReleased(float holdSeconds)
        {
            if (IsModalLocked) return;
            if (_currentTennisCourt != null)
            {
                _currentTennisCourt.TriggerSecondaryRelease(holdSeconds);
                return;
            }
            if (_currentArenaSport != null)
            {
                _currentArenaSport.TriggerSecondaryRelease(holdSeconds);
            }
        }

        private void ExitBicycle()
        {
            if (_currentBicycle == null || _currentBicycle.IsTransitioning) return;
            if (_currentBicycle.SpeedKmh > 5f)
            {
                RaiseStatusMessage(LocalizationService.Tr(_currentBicycle.IsMotorcycle
                    ? "vehicle.motorcycle.slow_down" : "sports.bicycle.slow_down"));
                return;
            }
            if (!_currentBicycle.TryGetDismount(out Vector3 position))
            {
                RaiseStatusMessage(LocalizationService.Tr("sports.bicycle.no_space"));
                return;
            }

            Quaternion rotation = _currentBicycle.transform.rotation;
            if (_currentBicycle.IsMotorcycle)
            {
                _currentBicycle.Dismount(position, rotation, () => FinishBicycleExit(position, rotation));
                return;
            }
            FinishBicycleExit(position, rotation);
        }

        private void FinishBicycleExit(Vector3 position, Quaternion rotation)
        {
            ClearActiveSport();
            footController.TeleportTo(position, rotation);
            SetSportMode(ControlMode.OnFoot, footController.transform);
            footController.SetMovementEnabled(!IsModalLocked);
        }

        private void ExitTennis()
        {
            if (_currentTennisCourt == null) return;
            Vector3 position = _currentTennisCourt.ReturnPosition;
            Quaternion rotation = _currentTennisCourt.ReturnRotation;
            ClearActiveSport();
            footController.TeleportTo(position, rotation);
            SetSportMode(ControlMode.OnFoot, footController.transform);
            footController.SetMovementEnabled(!IsModalLocked);
        }

        private void ExitArenaSport()
        {
            if (_currentArenaSport == null) return;
            Vector3 position = _currentArenaSport.ReturnPosition;
            Quaternion rotation = _currentArenaSport.ReturnRotation;
            ClearActiveSport();
            footController.TeleportTo(position, rotation);
            SetSportMode(ControlMode.OnFoot, footController.transform);
            footController.SetMovementEnabled(!IsModalLocked);
        }

        private void ClearActiveSport()
        {
            if (_currentBicycle != null) _currentBicycle.Release();
            if (_currentTennisCourt != null) _currentTennisCourt.End();
            if (_currentArenaSport != null) _currentArenaSport.EndPlayerMatch();
            _currentBicycle = null;
            _currentTennisCourt = null;
            _currentArenaSport = null;
            if (cameraRig != null) cameraRig.SetSportCameraOverride(false);
        }

        public void TryEnterVehicle()
        {
            TryEnterVehicle(playerSedan, snapCamera: false);
        }

        public void TryEnterVehicle(SedanVehicleController v, bool snapCamera = false)
        {
            if (v == null) return;

            ClearActiveSport();
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
                RaiseStatusMessage("Araçtan inmek için önce yavaşla!");
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
                footController.Humanoid.SetSeatContact(SeatFloor(seat.SitPoint), 0f);
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
            float floor = _currentSeat != null ? SeatFloor(_currentSeat.SitPoint) : _seatApproachPos.y;
            float duration = Mathf.Max(0.54f, seatTransitionSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                footController.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPos, targetPos, t),
                    Quaternion.Slerp(startRot, targetRot, Mathf.SmoothStep(0f, 1f, elapsed / (duration * 0.66f))));
                footController.Humanoid?.SetSeatContact(floor, t);
                yield return null;
            }

            footController.transform.SetPositionAndRotation(targetPos, targetRot);
            footController.Humanoid?.SetSeatContact(floor, 1f);
            _isSeatTransitioning = false;
            _seatRoutine = null;
        }

        private IEnumerator StandUpRoutine(Vector3 targetPos, Quaternion targetRot)
        {
            _isSeatTransitioning = true;
            Vector3 startPos = footController.transform.position;
            Quaternion startRot = footController.transform.rotation;
            float floor = _currentSeat != null ? SeatFloor(_currentSeat.SitPoint) : targetPos.y;
            float duration = Mathf.Max(0.51f, seatTransitionSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                footController.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPos, targetPos, t),
                    Quaternion.Slerp(startRot, targetRot, t));
                footController.Humanoid?.SetSeatContact(floor, 1f - t);
                yield return null;
            }

            if (_currentSeat != null)
            {
                _currentSeat.SetOccupied(false);
                _currentSeat = null;
            }

            footController.SetSeatedState(false);
            footController.Humanoid?.SetSitting(false, driving: false);
            footController.TeleportTo(targetPos, targetRot);
            footController.SetMovementEnabled(!IsModalLocked);

            _isSeatTransitioning = false;
            _seatRoutine = null;
            CurrentMode = ControlMode.OnFoot;
            OnModeChanged?.Invoke(CurrentMode);
        }

        private float SeatFloor(Transform seat)
        {
            Vector3 origin = seat.position + seat.forward * 0.47f + Vector3.up * 0.2f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.35f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) && hit.point.y < seat.position.y - 0.15f)
                return hit.point.y;
            return _seatApproachPos.y;
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
            ClearActiveSport();
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

            ClearActiveSport();
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
