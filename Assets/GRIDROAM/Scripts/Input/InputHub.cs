using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace OurWorld.Input
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
        [SerializeField] private float touchLookSensitivity = 0.22f;
        [SerializeField] private float keyLookSpeed = 110f;

        private const string PrefSensitivityKey = "ow_cam_sens";
        public float SensitivityMultiplier { get; private set; } = 1f;

        public void SetSensitivityMultiplier(float multiplier)
        {
            SensitivityMultiplier = Mathf.Clamp(multiplier, 0.5f, 1.8f);
            PlayerPrefs.SetFloat(PrefSensitivityKey, SensitivityMultiplier);
            PlayerPrefs.Save();
        }

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
        private bool _touchJumpQueued;
        private bool _touchAttackHeld;
        private bool _touchJumpHeld;
        private bool _prevAttackHeld;
        private bool _prevJumpHeld;
        private float _attackHoldSeconds;
        private float _jumpHoldSeconds;

        public Vector2 MoveInput { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool AttackHeld { get; private set; }
        public float AttackHoldSeconds => _attackHoldSeconds;
        public bool JumpHeld { get; private set; }
        public float JumpHoldSeconds => _jumpHoldSeconds;
        public Vector2 LookDelta { get; private set; }
        public float DriveSteer { get; private set; }
        public float DriveThrottle { get; private set; }
        public float DriveBrake { get; private set; }
        public bool HandbrakeHeld { get; private set; }
        public bool IsLookingManually { get; private set; }

        public event Action OnInteractPressed;
        public event Action OnAttackPressed;
        public event Action<float> OnAttackReleased;
        public event Action OnJumpPressed;
        public event Action<float> OnJumpReleased;
        public event Action OnExitVehiclePressed;
        public event Action OnContinueDialoguePressed;
        public event Action OnRetryPressed;
        public event Action OnTogglePhonePressed;
        public event Action OnTogglePerfOverlayPressed;
        public event Action OnCycleGraphicsPressed;
        public event Action OnExportPerfReportPressed;
        public event Action OnMenuTogglePressed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            SensitivityMultiplier = Mathf.Clamp(PlayerPrefs.GetFloat(PrefSensitivityKey, 1f), 0.5f, 1.8f);
        }

        private static bool IsTypingInUiField()
        {
            EventSystem es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return false;
            UnityEngine.UI.InputField field = es.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>();
            return field != null && field.isFocused;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (IsTypingInUiField())
            {
                kb = null; // Ignore keyboard shortcuts (M, E, WASD...) while typing into a UI text field.
            }

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                OnMenuTogglePressed?.Invoke();
            }

            if (OurWorld.UI.MainMenuController.IsMenuActive)
            {
                MoveInput = Vector2.zero;
                SprintHeld = false;
                AttackHeld = false;
                JumpHeld = false;
                _prevAttackHeld = false;
                _prevJumpHeld = false;
                _attackHoldSeconds = 0f;
                _jumpHoldSeconds = 0f;
                _touchAttackHeld = false;
                _touchJumpHeld = false;
                LookDelta = Vector2.zero;
                DriveSteer = 0f;
                DriveThrottle = 0f;
                DriveBrake = 0f;
                HandbrakeHeld = false;
                IsLookingManually = false;
                _touchLookAccumulator = Vector2.zero;
                _touchInteractQueued = false;
                _touchExitVehicleQueued = false;
                _touchContinueDialogueQueued = false;
                _touchPhoneQueued = false;
                _touchAttackQueued = false;
                _touchJumpQueued = false;
                return;
            }

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

            SprintHeld = (kb != null && kb.leftShiftKey.isPressed) || _touchSprint;

            // --- Camera look (Right-side touch drag + RMB drag in Editor + Q/Alt+E fallback) ---
            Vector2 look = _touchLookAccumulator * (touchLookSensitivity * SensitivityMultiplier);
            bool manualLook = _touchLookAccumulator.sqrMagnitude > 0.0001f;
            _touchLookAccumulator = Vector2.zero;

            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 mouseDelta = mouse.delta.ReadValue();
                look += mouseDelta * (mouseLookSensitivity * SensitivityMultiplier);
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
            bool lmbAttackDown = mouse != null && mouse.leftButton.wasPressedThisFrame && !pointerOverUi;
            bool lmbAttackHeld = mouse != null && mouse.leftButton.isPressed && (!pointerOverUi || _prevAttackHeld);
            bool kbAttackHeld = kb != null && kb.vKey.isPressed;
            bool kbJumpHeld = kb != null && kb.spaceKey.isPressed;

            bool attackHeldNow = _touchAttackHeld || kbAttackHeld || lmbAttackHeld;
            bool jumpHeldNow = _touchJumpHeld || kbJumpHeld;

            bool attackPressed = _touchAttackQueued || (kb != null && kb.vKey.wasPressedThisFrame) || lmbAttackDown || (attackHeldNow && !_prevAttackHeld);
            bool jumpPressed = _touchJumpQueued || (kb != null && kb.spaceKey.wasPressedThisFrame) || (jumpHeldNow && !_prevJumpHeld);

            bool attackReleased = !attackHeldNow && _prevAttackHeld;
            bool jumpReleased = !jumpHeldNow && _prevJumpHeld;
            float releasedAttackDuration = _attackHoldSeconds;
            float releasedJumpDuration = _jumpHoldSeconds;

            AttackHeld = attackHeldNow;
            JumpHeld = jumpHeldNow;
            if (attackHeldNow)
            {
                _attackHoldSeconds = _prevAttackHeld ? (_attackHoldSeconds + Time.unscaledDeltaTime) : 0f;
            }
            else
            {
                _attackHoldSeconds = 0f;
            }
            if (jumpHeldNow)
            {
                _jumpHoldSeconds = _prevJumpHeld ? (_jumpHoldSeconds + Time.unscaledDeltaTime) : 0f;
            }
            else
            {
                _jumpHoldSeconds = 0f;
            }
            _prevAttackHeld = attackHeldNow;
            _prevJumpHeld = jumpHeldNow;

            _touchInteractQueued = false;
            _touchExitVehicleQueued = false;
            _touchContinueDialogueQueued = false;
            _touchPhoneQueued = false;
            _touchAttackQueued = false;
            _touchJumpQueued = false;

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

            if (attackPressed)
            {
                OnAttackPressed?.Invoke();
            }

            if (attackReleased)
            {
                OnAttackReleased?.Invoke(releasedAttackDuration);
            }

            if (jumpPressed)
            {
                OnJumpPressed?.Invoke();
            }

            if (jumpReleased)
            {
                OnJumpReleased?.Invoke(releasedJumpDuration);
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
            _touchAttackHeld = false;
            _touchJumpHeld = false;
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
        public void SetTouchAttackHeld(bool pressed) => _touchAttackHeld = pressed;
        public void SetTouchJumpHeld(bool pressed) => _touchJumpHeld = pressed;
        public void TriggerTouchInteract() => _touchInteractQueued = true;
        public void TriggerTouchAttack() => _touchAttackQueued = true;
        public void TriggerTouchJump() => _touchJumpQueued = true;
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
