using Gridroam.CameraSystem;
using Gridroam.Input;
using UnityEngine;

namespace Gridroam.Player
{
    /// <summary>
    /// Third-person on-foot movement using CharacterController.
    /// Drives standard Animator parameters ("Speed", "IsGrounded") so Adobe Mixamo
    /// Humanoid characters with Idle/Walk/Run blend trees plug in directly.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerFootController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3.4f;
        [SerializeField] private float runSpeed = 6.2f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float gravity = -20f;

        [Header("Visual & Mixamo Animator Slot")]
        [Tooltip("Replace child placeholder geometry with a Mixamo Humanoid model + Animator (Idle/Walk/Run).")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator characterAnimator;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("IsGrounded");

        private CharacterController _controller;
        private ThirdPersonCameraRig _cameraRig;
        private float _verticalVelocity;
        private bool _movementEnabled = true;

        public Transform VisualRoot => visualRoot;

        public void ConfigureVisuals(Transform root, Animator animator = null)
        {
            visualRoot = root;
            characterAnimator = animator;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (characterAnimator == null && visualRoot != null)
            {
                characterAnimator = visualRoot.GetComponentInChildren<Animator>();
            }
        }

        private void Start()
        {
            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
        }

        public void SetMovementEnabled(bool enabledState)
        {
            _movementEnabled = enabledState;
            if (!enabledState && characterAnimator != null)
            {
                characterAnimator.SetFloat(SpeedHash, 0f);
            }
        }

        public void SetCharacterActive(bool active)
        {
            _controller.enabled = active;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(active);
            }
            enabled = active;
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _verticalVelocity = 0f;
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

            Vector2 moveInput = (_movementEnabled && InputHub.Instance != null)
                ? InputHub.Instance.MoveInput
                : Vector2.zero;
            bool sprint = _movementEnabled && InputHub.Instance != null && InputHub.Instance.SprintHeld;

            Vector3 forward = _cameraRig != null ? _cameraRig.FlatForward : Vector3.forward;
            Vector3 right = _cameraRig != null ? _cameraRig.FlatRight : Vector3.right;

            Vector3 moveDir = (forward * moveInput.y + right * moveInput.x);
            float inputMag = Mathf.Clamp01(moveDir.magnitude);
            if (inputMag > 0.01f)
            {
                moveDir.Normalize();
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
            }

            float targetSpeed = sprint ? runSpeed : walkSpeed;
            float currentSpeed = targetSpeed * inputMag;

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 velocity = moveDir * currentSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            if (characterAnimator != null)
            {
                characterAnimator.SetFloat(SpeedHash, currentSpeed, 0.08f, Time.deltaTime);
                characterAnimator.SetBool(GroundedHash, _controller.isGrounded);
            }
        }
    }
}
