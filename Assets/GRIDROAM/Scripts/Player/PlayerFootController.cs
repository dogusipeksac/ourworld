using Gridroam.CameraSystem;
using Gridroam.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gridroam.Player
{
    /// <summary>
    /// Third-person on-foot movement using CharacterController.
    /// Drives both standard Animator parameters ("Speed", "IsGrounded") and ProceduralHumanoid.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerFootController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3.4f;
        [SerializeField] private float runSpeed = 6.2f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float deceleration = 14f;
        [SerializeField] private float jumpHeight = 1.0f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float gravity = -20f;

        [Header("Visual & Mixamo Animator Slot")]
        [Tooltip("Replace child placeholder geometry with a Mixamo Humanoid model + Animator (Idle/Walk/Run).")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private ProceduralHumanoid humanoid;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("IsGrounded");

        private CharacterController _controller;
        private ThirdPersonCameraRig _cameraRig;
        private float _verticalVelocity;
        private float _currentSpeed;
        private Vector3 _moveDirection = Vector3.forward;
        private bool _movementEnabled = true;

        public Transform VisualRoot => visualRoot;

        public void ConfigureVisuals(Transform root, Animator animator = null, ProceduralHumanoid proceduralHumanoid = null)
        {
            visualRoot = root;
            characterAnimator = animator;
            if (proceduralHumanoid != null)
            {
                humanoid = proceduralHumanoid;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (characterAnimator == null && visualRoot != null)
            {
                characterAnimator = visualRoot.GetComponentInChildren<Animator>();
            }

            if (humanoid == null)
            {
                humanoid = GetComponentInChildren<ProceduralHumanoid>();
            }
        }

        private void Start()
        {
            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
        }

        public void SetMovementEnabled(bool enabledState)
        {
            _movementEnabled = enabledState;
            if (!enabledState)
            {
                _currentSpeed = 0f;
                if (characterAnimator != null)
                {
                    characterAnimator.SetFloat(SpeedHash, 0f);
                }
                if (humanoid != null)
                {
                    humanoid.SetLocomotion(0f, _controller != null && _controller.isGrounded, _verticalVelocity);
                }
            }
        }

        public void SetCharacterActive(bool active)
        {
            _controller.enabled = active;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(active);
            }
            if (!active)
            {
                _currentSpeed = 0f;
            }
            enabled = active;
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _verticalVelocity = 0f;
            _currentSpeed = 0f;
            _moveDirection = rotation * Vector3.forward;
            _controller.enabled = wasEnabled;
        }

        private void Update()
        {
            if (!_controller.enabled)
            {
                return;
            }

            if (_cameraRig == null)
            {
                _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            }

            float dt = Time.deltaTime;

            Vector2 moveInput = (_movementEnabled && InputHub.Instance != null)
                ? InputHub.Instance.MoveInput
                : Vector2.zero;
            bool sprint = _movementEnabled && InputHub.Instance != null && InputHub.Instance.SprintHeld;

            Vector3 forward = _cameraRig != null ? _cameraRig.FlatForward : Vector3.forward;
            Vector3 right = _cameraRig != null ? _cameraRig.FlatRight : Vector3.right;

            Vector3 desiredDir = (forward * moveInput.y + right * moveInput.x);
            float inputMag = Mathf.Clamp01(desiredDir.magnitude);
            if (inputMag > 0.01f)
            {
                desiredDir.Normalize();
                _moveDirection = desiredDir;
                Quaternion targetRot = Quaternion.LookRotation(_moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotationSpeed * dt));
            }

            float targetSpeed = (sprint ? runSpeed : walkSpeed) * inputMag;
            float rate = targetSpeed > _currentSpeed ? acceleration : deceleration;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, rate * dt);

            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }

                bool jumpPressed = _movementEnabled &&
                                   Keyboard.current != null &&
                                   Keyboard.current.spaceKey.wasPressedThisFrame;
                if (jumpPressed)
                {
                    _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
                }
            }
            else
            {
                _verticalVelocity += gravity * dt;
            }

            Vector3 velocity = _moveDirection * _currentSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * dt);

            if (characterAnimator != null)
            {
                characterAnimator.SetFloat(SpeedHash, _currentSpeed, 0.08f, dt);
                characterAnimator.SetBool(GroundedHash, _controller.isGrounded);
            }

            if (humanoid != null)
            {
                humanoid.SetLocomotion(_currentSpeed, _controller.isGrounded, _verticalVelocity);
            }
        }
    }
}
