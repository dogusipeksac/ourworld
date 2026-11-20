using System;
using Gridroam.CameraSystem;
using Gridroam.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gridroam.Player
{
    /// <summary>
    /// Third-person on-foot movement using CharacterController.
    /// Drives ProceduralHumanoid locomotion, head-look IK-lite toward nearby interactables,
    /// procedural footstep & landing audio, and energy-based tired sprint speed reduction.
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
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private ProceduralHumanoid humanoid;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("IsGrounded");
        private static AudioClip _sharedFootstepClip;

        private CharacterController _controller;
        private ThirdPersonCameraRig _cameraRig;
        private AudioSource _footstepAudio;
        private float _verticalVelocity;
        private float _currentSpeed;
        private Vector3 _moveDirection = Vector3.forward;
        private bool _movementEnabled = true;

        public Transform VisualRoot => visualRoot;
        public ProceduralHumanoid Humanoid => humanoid;
        public float CurrentSpeed => _currentSpeed;

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

            EnsureFootstepAudio();
        }

        private void OnEnable()
        {
            if (humanoid != null)
            {
                humanoid.OnFootstepContact += HandleFootstepContact;
                humanoid.OnLanded += HandleLanded;
            }
        }

        private void OnDisable()
        {
            if (humanoid != null)
            {
                humanoid.OnFootstepContact -= HandleFootstepContact;
                humanoid.OnLanded -= HandleLanded;
            }
        }

        private void Start()
        {
            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
        }

        private void EnsureFootstepAudio()
        {
            _footstepAudio = GetComponent<AudioSource>();
            if (_footstepAudio == null)
            {
                _footstepAudio = gameObject.AddComponent<AudioSource>();
            }

            _footstepAudio.playOnAwake = false;
            _footstepAudio.loop = false;
            _footstepAudio.spatialBlend = 0.25f;

            if (_sharedFootstepClip == null)
            {
                _sharedFootstepClip = BuildProceduralFootstepClip(44100);
            }
        }

        private static AudioClip BuildProceduralFootstepClip(int sampleRate)
        {
            int samplesCount = Mathf.RoundToInt(sampleRate * 0.055f); // 55 ms subtle thump/click
            float[] data = new float[samplesCount];
            System.Random rng = new System.Random(9021);
            float lp = 0f;
            double phase = 0.0;

            for (int i = 0; i < samplesCount; i++)
            {
                float t = (float)i / samplesCount;
                float env = Mathf.Exp(-t * 9.5f);
                float freq = Mathf.Lerp(135f, 52f, t);
                phase += (2.0 * Math.PI * freq) / sampleRate;

                float thump = (float)Math.Sin(phase) * 0.68f;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.32f * (white - lp);
                float click = lp * Mathf.Exp(-t * 18f) * 0.42f;

                data[i] = Mathf.Clamp((thump + click) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ProceduralFootstep", samplesCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void HandleFootstepContact(float runBlend)
        {
            if (_footstepAudio == null || _sharedFootstepClip == null || !_controller.enabled)
            {
                return;
            }

            float vol = Mathf.Lerp(0.10f, 0.24f, Mathf.Clamp01(runBlend));
            _footstepAudio.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
            _footstepAudio.PlayOneShot(_sharedFootstepClip, vol);
        }

        private void HandleLanded()
        {
            if (_footstepAudio == null || _sharedFootstepClip == null || !_controller.enabled)
            {
                return;
            }

            _footstepAudio.pitch = 0.84f;
            _footstepAudio.PlayOneShot(_sharedFootstepClip, 0.30f);
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
            if (_controller == null) _controller = GetComponent<CharacterController>();
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

        public void SetSeatedState(bool seated)
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(true);
            }

            _currentSpeed = 0f;
            _verticalVelocity = 0f;

            if (seated)
            {
                _movementEnabled = false;
                _controller.enabled = false;
                enabled = false;
                if (humanoid != null)
                {
                    humanoid.SetLookAtPoint(false, Vector3.zero);
                    humanoid.SetLocomotion(0f, true, 0f);
                }
            }
            else
            {
                _controller.enabled = true;
                enabled = true;
            }
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
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

            // Safety net: never allow falling below ground level
            if (transform.position.y < -2.0f)
            {
                Vector3 safePos = new Vector3(
                    Mathf.Clamp(transform.position.x, -95f, 95f),
                    0.25f,
                    Mathf.Clamp(transform.position.z, -36f, 194f));
                TeleportTo(safePos, transform.rotation);
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

            Vector3 desiredDir = forward * moveInput.y + right * moveInput.x;
            float inputMag = Mathf.Clamp01(desiredDir.magnitude);
            if (inputMag > 0.01f)
            {
                desiredDir.Normalize();
                _moveDirection = desiredDir;
                Quaternion targetRot = Quaternion.LookRotation(_moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotationSpeed * dt));
            }

            bool isTired = PlayerNeedsController.Instance != null && PlayerNeedsController.Instance.IsTired;
            float effectiveRunSpeed = isTired ? (runSpeed * 0.75f) : runSpeed;
            float targetSpeed = (sprint ? effectiveRunSpeed : walkSpeed) * inputMag;
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
                InteractableTrigger lookTrig = PlayerModeController.Instance != null
                    ? PlayerModeController.Instance.ClosestInteractableWithin4m
                    : null;
                if (lookTrig != null)
                {
                    humanoid.SetLookAtPoint(true, lookTrig.transform.position + Vector3.up * 1.2f);
                }
                else
                {
                    humanoid.SetLookAtPoint(false, Vector3.zero);
                }

                humanoid.SetLocomotion(_currentSpeed, _controller.isGrounded, _verticalVelocity);
            }
        }
    }
}
