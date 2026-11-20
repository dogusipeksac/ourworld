using System;
using Gridroam.CameraSystem;
using Gridroam.Core;
using Gridroam.Input;
using Gridroam.Vehicle;
using UnityEngine;

namespace Gridroam.Player
{
    public enum ControlMode
    {
        OnFoot,
        Driving
    }

    /// <summary>
    /// Coordinates On-Foot <-> Driving mode transitions, vehicle key checks,
    /// and contextual world interactions (talking to NPCs, entering/exiting sedan, inspecting).
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

        private InteractableTrigger[] _cachedInteractables;
        private InteractableTrigger _nearestInteractable;
        private bool _dialogueActive;
        private int _dialogueClosedFrame = -1;

        public ControlMode CurrentMode { get; private set; } = ControlMode.OnFoot;
        public bool IsDriving => CurrentMode == ControlMode.Driving;
        public bool HasVehicleKeys => SaveManager.Instance != null && SaveManager.Instance.Data.hasVehicleKeys;
        public PlayerFootController FootController => footController;
        public SedanVehicleController PlayerSedan => playerSedan;
        public InteractableTrigger NearestInteractable => _nearestInteractable;

        public Vector3 ActiveWorldPosition =>
            IsDriving && playerSedan != null
                ? playerSedan.transform.position
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

            SetOnFootMode(snapCamera: true);
        }

        private void OnDestroy()
        {
            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnInteractPressed -= HandleInteractPressed;
                InputHub.Instance.OnExitVehiclePressed -= HandleExitVehiclePressed;
            }
        }

        public void RefreshInteractablesCache()
        {
            _cachedInteractables = FindObjectsByType<InteractableTrigger>();
        }

        public void SetDialogueActive(bool active)
        {
            if (_dialogueActive && !active)
            {
                _dialogueClosedFrame = Time.frameCount;
            }

            _dialogueActive = active;
            if (footController != null)
            {
                footController.SetMovementEnabled(!active && CurrentMode == ControlMode.OnFoot);
            }

            if (playerSedan != null)
            {
                playerSedan.SetInputLocked(active);
            }
        }

        private void Update()
        {
            if (CurrentMode != ControlMode.OnFoot || _dialogueActive || footController == null)
            {
                SetNearestInteractable(null);
                return;
            }

            if (_cachedInteractables == null || _cachedInteractables.Length == 0)
            {
                RefreshInteractablesCache();
            }

            Vector3 playerPos = footController.transform.position;
            InteractableTrigger best = null;
            float bestDistSq = float.MaxValue;

            foreach (InteractableTrigger trigger in _cachedInteractables)
            {
                if (trigger == null || !trigger.isActiveAndEnabled) continue;

                float radius = trigger.InteractionRadius;
                float distSq = (trigger.transform.position - playerPos).sqrMagnitude;
                if (distSq <= radius * radius && distSq < bestDistSq)
                {
                    best = trigger;
                    bestDistSq = distSq;
                }
            }

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
            if (_dialogueActive || Time.frameCount == _dialogueClosedFrame || CurrentMode != ControlMode.OnFoot || _nearestInteractable == null)
            {
                return;
            }

            if (_nearestInteractable.Kind == InteractableKind.PlayerVehicle)
            {
                TryEnterVehicle();
                return;
            }

            OnInteracted?.Invoke(_nearestInteractable);
        }

        private void HandleExitVehiclePressed()
        {
            if (_dialogueActive || Time.frameCount == _dialogueClosedFrame || CurrentMode != ControlMode.Driving)
            {
                return;
            }

            TryExitVehicle();
        }

        public void TryEnterVehicle()
        {
            if (playerSedan == null) return;

            if (!HasVehicleKeys)
            {
                OnStatusMessage?.Invoke(LocalizationService.Tr("ui.hud.keys_locked"));
                return;
            }

            CurrentMode = ControlMode.Driving;
            SetNearestInteractable(null);

            if (footController != null)
            {
                footController.SetCharacterActive(false);
            }

            playerSedan.SetPlayerDriving(true);
            playerSedan.SetInputLocked(_dialogueActive);

            if (cameraRig != null)
            {
                cameraRig.SetTarget(playerSedan.transform, drivingMode: true, snapBehindTarget: false);
            }

            OnModeChanged?.Invoke(CurrentMode);
            OnEnteredVehicle?.Invoke(playerSedan);
        }

        public void TryExitVehicle()
        {
            if (playerSedan == null || footController == null) return;

            if (playerSedan.SpeedKmh > vehicleExitMaxSpeedKmh)
            {
                return;
            }

            Vector3 exitPos = playerSedan.GetSafeExitPosition();
            Quaternion exitRot = Quaternion.LookRotation(playerSedan.transform.forward, Vector3.up);

            playerSedan.SetPlayerDriving(false);
            footController.TeleportTo(exitPos, exitRot);
            footController.SetCharacterActive(true);
            footController.SetMovementEnabled(!_dialogueActive);

            CurrentMode = ControlMode.OnFoot;
            if (cameraRig != null)
            {
                cameraRig.SetTarget(footController.transform, drivingMode: false, snapBehindTarget: false);
            }

            OnModeChanged?.Invoke(CurrentMode);
            OnExitedVehicle?.Invoke(playerSedan);
        }

        public void SetOnFootMode(bool snapCamera)
        {
            CurrentMode = ControlMode.OnFoot;
            if (playerSedan != null)
            {
                playerSedan.SetPlayerDriving(false);
            }

            if (footController != null)
            {
                footController.SetCharacterActive(true);
                footController.SetMovementEnabled(!_dialogueActive);
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

            CurrentMode = ControlMode.Driving;
            if (footController != null)
            {
                footController.SetCharacterActive(false);
            }

            playerSedan.SetPlayerDriving(true);
            playerSedan.SetInputLocked(_dialogueActive);

            if (cameraRig != null)
            {
                cameraRig.SetTarget(playerSedan.transform, drivingMode: true, snapBehindTarget: snapCamera);
            }

            OnModeChanged?.Invoke(CurrentMode);
        }
    }
}
