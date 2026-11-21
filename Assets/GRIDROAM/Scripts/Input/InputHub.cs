using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gridroam.Input
{
    /// <summary>
    /// Combines mobile UGUI touch controls (floating joystick, camera touch zone, sprint,
    /// simultaneous steering + gas/brake/handbrake, phone) with Unity 6 Input System keyboard & mouse.
    /// </summary>
    public class InputHub : MonoBehaviour
    {
        public static InputHub Instance { get; private set; }

        [Header("Sensitivity")]
        [SerializeField] private float mouseLookSensitivity = 0.22f;
        [SerializeField] private float touchLookSensitivity = 0.28f;
        [SerializeField] private float keyLookSpeed = 110f;

        // Touch / UI virtual inputs
        private Vector2 _touchMove;
        private Vector2 _touchLookAccumulator;
        private float _touchSteerLeft;
        private float _touchSteerRight;
        private float _touchGas;
        private float _touchBrake;
        private bool _touchHandbrake;
        private bool _touchSprint;
        private bool _touchInteractQueued;
        private bool _touchExitVehicleQueued;
        private bool _touchContinueDialogueQueued;
        private bool _touchPhoneQueued;
        private bool _touchAttackQueued;

        public Vector2 MoveInput { get; private set; }
        public bool SprintHeld { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public float DriveSteer { get; private set; }
        public float DriveThrottle { get; private set; }
        public float DriveBrake { get; private set; }
        public bool HandbrakeHeld { get; private set; }
        public bool IsLookingManually { get; private set; }

        public event Action OnInteractPressed;
        public event Action OnAttackPressed;
        public event Action OnExitVehiclePressed;
        public event Action OnContinueDialoguePressed;
        public event Action OnRetryPressed;
        public event Action OnTogglePhonePressed;
        public event Action OnTogglePerfOverlayPressed;
        public event Action OnCycleGraphicsPressed;
        public event Action OnExportPerfReportPressed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            // --- On-foot movement (WASD + Touch Joystick) ---
            Vector2 kbMove = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) kbMove.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) kbMove.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) kbMove.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) kbMove.x -= 1f;
            }

            Vector2 combinedMove = _touchMove.sqrMagnitude > 0.001f ? _touchMove : kbMove;
            if (combinedMove.sqrMagnitude > 1f)
            {
                combinedMove.Normalize();
            }
            MoveInput = combinedMove;

            SprintHeld = (kb != null && kb.leftShiftKey.isPressed) || _touchSprint || _touchMove.magnitude > 0.88f;

            // --- Camera look (Right-side touch drag + RMB drag in Editor + Q/Alt+E fallback) ---
            Vector2 look = _touchLookAccumulator * touchLookSensitivity;
            bool manualLook = _touchLookAccumulator.sqrMagnitude > 0.0001f;
            _touchLookAccumulator = Vector2.zero;

            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 mouseDelta = mouse.delta.ReadValue();
                look += mouseDelta * mouseLookSensitivity;
                if (mouseDelta.sqrMagnitude > 0.0001f)
                {
                    manualLook = true;
                }
            }

            if (kb != null)
            {
                float yawKey = 0f;
                if (kb.qKey.isPressed) yawKey -= 1f;
                if (kb.eKey.isPressed && kb.leftAltKey.isPressed) yawKey += 1f;
                if (Mathf.Abs(yawKey) > 0.01f)
                {
                    look.x += yawKey * keyLookSpeed * Time.deltaTime;
                    manualLook = true;
                }
            }

            LookDelta = look;
            IsLookingManually = manualLook;

            // --- Driving controls (Simultaneous steering + throttle/brake + handbrake) ---
            float kbSteer = 0f;
            float kbGas = 0f;
            float kbBrake = 0f;
            bool kbHandbrake = false;

            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) kbSteer -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) kbSteer += 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) kbGas = 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) kbBrake = 1f;
                kbHandbrake = kb.spaceKey.isPressed;
            }

            float touchSteer = _touchSteerRight - _touchSteerLeft;
            DriveSteer = Mathf.Clamp(Mathf.Abs(touchSteer) > 0.01f ? touchSteer : kbSteer, -1f, 1f);
            DriveThrottle = Mathf.Clamp01(Mathf.Max(_touchGas, kbGas));
            DriveBrake = Mathf.Clamp01(Mathf.Max(_touchBrake, kbBrake));
            HandbrakeHeld = kbHandbrake || _touchHandbrake;

            // --- Discrete actions ---
            bool interact = _touchInteractQueued || (kb != null && kb.eKey.wasPressedThisFrame && !kb.leftAltKey.isPressed);
            bool exitVehicle = _touchExitVehicleQueued || (kb != null && (kb.fKey.wasPressedThisFrame || (kb.eKey.wasPressedThisFrame && !kb.leftAltKey.isPressed)));
            bool continueDlg = _touchContinueDialogueQueued || (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame));
            bool togglePhone = _touchPhoneQueued || (kb != null && kb.mKey.wasPressedThisFrame);

            bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool lmbAttack = mouse != null && mouse.leftButton.wasPressedThisFrame && !pointerOverUi;
            bool attack = _touchAttackQueued || (kb != null && kb.vKey.wasPressedThisFrame) || lmbAttack;

            _touchInteractQueued = false;
            _touchExitVehicleQueued = false;
            _touchContinueDialogueQueued = false;
            _touchPhoneQueued = false;
            _touchAttackQueued = false;

            if (togglePhone)
            {
                OnTogglePhonePressed?.Invoke();
            }

            if (continueDlg)
            {
                OnContinueDialoguePressed?.Invoke();
            }

            if (interact)
            {
                OnInteractPressed?.Invoke();
            }

            if (attack)
            {
                OnAttackPressed?.Invoke();
            }

            if (exitVehicle)
            {
                OnExitVehiclePressed?.Invoke();
            }

            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame) OnRetryPressed?.Invoke();
                if (kb.f1Key.wasPressedThisFrame) OnTogglePerfOverlayPressed?.Invoke();
                if (kb.f2Key.wasPressedThisFrame) OnCycleGraphicsPressed?.Invoke();
                if (kb.f3Key.wasPressedThisFrame) OnExportPerfReportPressed?.Invoke();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ClearAllTouchInputs();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                ClearAllTouchInputs();
            }
        }

        public void ClearAllTouchInputs()
        {
            _touchMove = Vector2.zero;
            _touchLookAccumulator = Vector2.zero;
            _touchSprint = false;
            ClearDrivingTouchInputs();
        }

        // --- Hooks called by Mobile UGUI Controls ---
        public void SetTouchMove(Vector2 value) => _touchMove = Vector2.ClampMagnitude(value, 1f);
        public void AddTouchLookDelta(Vector2 pixelDelta) => _touchLookAccumulator += pixelDelta;
        public void SetTouchSteerLeft(bool pressed) => _touchSteerLeft = pressed ? 1f : 0f;
        public void SetTouchSteerRight(bool pressed) => _touchSteerRight = pressed ? 1f : 0f;
        public void SetTouchGas(bool pressed) => _touchGas = pressed ? 1f : 0f;
        public void SetTouchBrake(bool pressed) => _touchBrake = pressed ? 1f : 0f;
        public void SetTouchHandbrake(bool pressed) => _touchHandbrake = pressed;
        public void SetTouchSprint(bool pressed) => _touchSprint = pressed;
        public void TriggerTouchInteract() => _touchInteractQueued = true;
        public void TriggerTouchAttack() => _touchAttackQueued = true;
        public void TriggerTouchExitVehicle() => _touchExitVehicleQueued = true;
        public void TriggerTouchContinueDialogue() => _touchContinueDialogueQueued = true;
        public void TriggerTouchPhone() => _touchPhoneQueued = true;

        public void ClearDrivingTouchInputs()
        {
            _touchSteerLeft = 0f;
            _touchSteerRight = 0f;
            _touchGas = 0f;
            _touchBrake = 0f;
            _touchHandbrake = false;
        }
    }
}
