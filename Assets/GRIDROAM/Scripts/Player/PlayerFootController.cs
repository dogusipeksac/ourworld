using System;
using System.Collections;
using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Vehicle;
using OurWorld.World;
using UnityEngine;

namespace OurWorld.Player
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
        [SerializeField] private float walkSpeed = 2.25f;
        [SerializeField] private float runSpeed = 4.25f;
        [SerializeField] private float acceleration = 8.0f;
        [SerializeField] private float deceleration = 11.5f;
        [SerializeField] private float jumpHeight = 1.0f;
        [SerializeField] private float rotationSpeed = 9.5f;
        [SerializeField] private float gravity = -20f;

        [Header("Visual & Mixamo Animator Slot")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private ProceduralHumanoid humanoid;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color[] JacketColors =
        {
            new Color(0.16f, 0.27f, 0.42f, 1f), // 0: Klasik Lacivert
            new Color(0.48f, 0.12f, 0.16f, 1f), // 1: Bordo Deri
            new Color(0.24f, 0.36f, 0.20f, 1f), // 2: Zeytin Parka
            new Color(0.14f, 0.15f, 0.18f, 1f)  // 3: Antrasit Sokak
        };

        private static AudioClip _sharedFootstepClip;
        private static AudioClip _sharedPunchHitClip;
        private static AudioClip _sharedHeavyHitClip;
        private static AudioClip _sharedPunchWhooshClip;
        private static AudioClip _sharedSplashClip;

        private CharacterController _controller;
        private ThirdPersonCameraRig _cameraRig;
        private AudioSource _footstepAudio;
        private float _verticalVelocity;
        private float _currentSpeed;
        private float _punchCooldown;
        private int _comboStep;
        private float _comboResetTimer;
        private Vector3 _combatLungeVelocity;
        private bool _isVaulting;
        private Coroutine _vaultRoutine;
        private Coroutine _hitstopRoutine;
        private bool _isSwimming;
        private bool _isUnderwater;
        private bool _swimBreaststroke;
        private float _swimOxygen01 = 1f;
        private float _bubbleTimer;
        private float _waterSurfaceY;
        private Vector3 _moveDirection = Vector3.forward;
        private bool _movementEnabled = true;
        private float _lastGroundCheckY;
        private bool _hasLastGroundY;
        private float _groundSampleHoldTimer;
        private float _slopeProbeTimer;

        public Transform VisualRoot => visualRoot;
        public ProceduralHumanoid Humanoid => humanoid;
        public float CurrentSpeed => _currentSpeed;
        public bool IsSwimming => _isSwimming;
        public bool IsUnderwater => _isUnderwater;
        public float SwimOxygen01 => _swimOxygen01;

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
                if (_isSwimming)
                {
                    humanoid.SetSwimming(false, 0f);
                }
            }
            _isSwimming = false;
            _isVaulting = false;
            if (_hitstopRoutine != null)
            {
                StopCoroutine(_hitstopRoutine);
                _hitstopRoutine = null;
                Time.timeScale = 1f;
            }
        }

        private void Start()
        {
            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnAttackPressed += HandleAttackPressed;
                InputHub.Instance.OnJumpPressed += HandleJumpOrVaultPressed;
            }
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                ApplyJacketColor(SaveManager.Instance.Data.playerJacketColorIndex);
            }
        }

        private void OnDestroy()
        {
            if (Time.timeScale < 0.99f)
            {
                Time.timeScale = 1f;
            }
            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnAttackPressed -= HandleAttackPressed;
                InputHub.Instance.OnJumpPressed -= HandleJumpOrVaultPressed;
            }
        }

        public void ApplyJacketColor(int colorIndex)
        {
            if (visualRoot == null) return;
            int idx = Mathf.Clamp(colorIndex, 0, JacketColors.Length - 1);
            Color col = JacketColors[idx];
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                Material[] mats = r.materials;
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != null && mats[m].name.Contains("MAT_Char_Shirt"))
                    {
                        if (mats[m].HasProperty(BaseColorId))
                        {
                            mats[m].SetColor(BaseColorId, col);
                            changed = true;
                        }
                    }
                }
                if (changed) r.materials = mats;
            }
        }

        private void HandleAttackPressed()
        {
            if (!_movementEnabled || _controller == null || !_controller.enabled || _punchCooldown > 0f || _isVaulting || _isSwimming)
            {
                return;
            }

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.CurrentMode != ControlMode.OnFoot || mode.IsModalLocked)
            {
                return;
            }

            // If carrying a physical street prop (Damacana / crate / cone), hurl it forward with two-handed throw!
            if (CarryableProp.IsHoldingProp)
            {
                _punchCooldown = 0.48f;
                CarryableProp.TryThrowHeldProp(transform.forward);
                mode.RaiseStatusMessage("Eşyayı fırlattın!");
                return;
            }
            CarryableProp.DropHeldPropIfAny();
            StreetCatController.CancelActivePet();

            // If standing next to a street football, unleash a curved power shot!
            Collider[] ballProbe = Physics.OverlapSphere(transform.position + transform.forward * 0.9f + Vector3.up * 0.45f, 1.35f);
            for (int b = 0; b < ballProbe.Length; b++)
            {
                if (ballProbe[b] == null) continue;
                StreetFootball ball = ballProbe[b].GetComponentInParent<StreetFootball>();
                if (ball != null)
                {
                    _punchCooldown = 0.36f;
                    ball.KickFromPlayer(mode, 1.15f);
                    return;
                }
            }

            int step = _comboResetTimer > 0f ? _comboStep : 0;
            _comboStep = (step + 1) % 3;
            _comboResetTimer = 1.25f;
            _punchCooldown = step == 2 ? 0.56f : (step == 1 ? 0.36f : 0.28f);

            if (humanoid != null)
            {
                humanoid.PlayCombatMove(step);
            }

            // Natural forward weight transfer during strike
            _combatLungeVelocity = transform.forward * (step == 2 ? 3.8f : (step == 1 ? 2.6f : 1.9f));

            if (_footstepAudio != null && _sharedPunchWhooshClip != null)
            {
                _footstepAudio.pitch = step == 2 ? 0.84f : (step == 1 ? 0.96f : 1.12f);
                _footstepAudio.PlayOneShot(_sharedPunchWhooshClip, 0.28f);
            }

            StartCoroutine(ResolveCombatHitRoutine(step));
        }

        private IEnumerator ResolveCombatHitRoutine(int step)
        {
            float hitDelay = step == 2 ? 0.14f : (step == 1 ? 0.12f : 0.09f);
            yield return new WaitForSeconds(hitDelay);

            if (!enabled || _controller == null || !_controller.enabled)
            {
                yield break;
            }

            float reach = step == 2 ? 1.28f : 1.08f;
            float radius = step == 2 ? 1.25f : 1.05f;
            Vector3 strikeCenter = transform.position + transform.forward * reach + Vector3.up * 1.05f;
            Collider[] hits = Physics.OverlapSphere(strikeCenter, radius);

            TownPedestrian hitPed = null;
            VehicleDamageSystem hitCar = null;
            Collider hitCarCol = null;
            float bestDistSq = float.MaxValue;
            float bestCarDistSq = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null) continue;
                TownPedestrian ped = hits[i].GetComponentInParent<TownPedestrian>();
                if (ped != null)
                {
                    float dSq = (ped.transform.position - transform.position).sqrMagnitude;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        hitPed = ped;
                    }
                    continue;
                }

                VehicleDamageSystem carDmg = hits[i].GetComponentInParent<VehicleDamageSystem>();
                if (carDmg != null)
                {
                    Vector3 pt = hits[i].ClosestPoint(strikeCenter);
                    float dSq = (pt - strikeCenter).sqrMagnitude;
                    if (dSq < bestCarDistSq)
                    {
                        bestCarDistSq = dSq;
                        hitCar = carDmg;
                        hitCarCol = hits[i];
                    }
                }
            }

            if (hitPed != null)
            {
                hitPed.OnPunchedByPlayer(transform.position, transform.forward, step);

                if (_footstepAudio != null)
                {
                    AudioClip clip = step >= 1 ? _sharedHeavyHitClip : _sharedPunchHitClip;
                    if (clip != null)
                    {
                        _footstepAudio.pitch = step == 2
                            ? UnityEngine.Random.Range(0.80f, 0.92f)
                            : UnityEngine.Random.Range(0.94f, 1.08f);
                        _footstepAudio.PlayOneShot(clip, step == 2 ? 0.68f : 0.52f);
                    }
                }

                if (_cameraRig != null)
                {
                    float intensity = step == 2 ? 0.22f : (step == 1 ? 0.14f : 0.08f);
                    float duration = step == 2 ? 0.22f : 0.14f;
                    _cameraRig.AddShake(intensity, duration);
                }

                if (_hitstopRoutine != null)
                {
                    StopCoroutine(_hitstopRoutine);
                    Time.timeScale = 1f;
                }
                _hitstopRoutine = StartCoroutine(MicroHitstopRoutine(step >= 1 ? 0.048f : 0.028f));
            }
            else if (hitCar != null)
            {
                Vector3 impactPt = hitCarCol != null ? hitCarCol.ClosestPoint(strikeCenter) : strikeCenter;
                hitCar.ApplyMeleeHit(impactPt, transform.forward, step);

                if (_cameraRig != null)
                {
                    float intensity = step == 2 ? 0.20f : (step == 1 ? 0.12f : 0.07f);
                    _cameraRig.AddShake(intensity, step == 2 ? 0.18f : 0.12f);
                }

                if (_hitstopRoutine != null)
                {
                    StopCoroutine(_hitstopRoutine);
                    Time.timeScale = 1f;
                }
                _hitstopRoutine = StartCoroutine(MicroHitstopRoutine(step >= 1 ? 0.040f : 0.024f));
            }
        }

        private IEnumerator MicroHitstopRoutine(float unscaledSeconds)
        {
            Time.timeScale = 0.22f;
            yield return new WaitForSecondsRealtime(unscaledSeconds);
            Time.timeScale = 1f;
            _hitstopRoutine = null;
        }

        private void HandleJumpOrVaultPressed()
        {
            if (!_movementEnabled || _controller == null || !_controller.enabled || _isVaulting)
            {
                return;
            }

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.CurrentMode != ControlMode.OnFoot || mode.IsModalLocked)
            {
                return;
            }

            // Swimming: climb/vault cleanly onto adjacent pool deck or hop up out of water
            if (_isSwimming)
            {
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                fwd.Normalize();

                Vector3 probeOrigin = transform.position + fwd * 1.05f + Vector3.up * 1.95f;
                if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit deckHit, 2.2f, ~0, QueryTriggerInteraction.Ignore) &&
                    deckHit.point.y >= _waterSurfaceY - 0.15f &&
                    deckHit.point.y <= _waterSurfaceY + 0.65f)
                {
                    Vector3 landPos = deckHit.point + Vector3.up * 0.04f;
                    Vector3 peakPos = Vector3.Lerp(transform.position, landPos, 0.55f);
                    peakPos.y = landPos.y + 0.45f;
                    StartVault(peakPos, landPos);
                    return;
                }

                _verticalVelocity = 5.2f;
                return;
            }

            if (!_controller.isGrounded)
            {
                return;
            }

            StreetCatController.CancelActivePet();
            if (TryDetectVaultObstacle(out Vector3 vaultPeak, out Vector3 vaultLand, out Vector3 vaultSupport))
            {
                CarryableProp.DropHeldPropIfAny();
                StartVault(vaultPeak, vaultLand, vaultSupport);
                return;
            }
            if (!CarryableProp.IsHoldingProp) CarryableProp.DropHeldPropIfAny();

            _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
        }

        private bool TryDetectVaultObstacle(out Vector3 peakPos, out Vector3 landPos, out Vector3 support)
        {
            peakPos = Vector3.zero;
            landPos = Vector3.zero;
            support = Vector3.zero;

            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) return false;
            fwd.Normalize();

            Vector3 waistOrigin = transform.position + Vector3.up * 0.62f;
            if (!Physics.Raycast(waistOrigin, fwd, out RaycastHit lowHit, 1.18f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (lowHit.normal.y > 0.68f ||
                lowHit.collider.GetComponentInParent<TownPedestrian>() != null ||
                lowHit.collider.GetComponentInParent<PlayerFootController>() != null)
            {
                return false;
            }

            // Ensure head space is clear (don't try to vault through full-height building walls)
            Vector3 headOrigin = transform.position + Vector3.up * 1.65f;
            if (Physics.Raycast(headOrigin, fwd, 1.45f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // Find top surface of the obstacle right beyond the impact face
            Vector3 topProbe = lowHit.point + fwd * 0.36f + Vector3.up * 1.40f;
            if (!Physics.Raycast(topProbe, Vector3.down, out RaycastHit topHit, 1.45f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            float obstacleHeight = topHit.point.y - transform.position.y;
            if (obstacleHeight < 0.32f || obstacleHeight > 1.48f)
            {
                return false;
            }

            // Find landing surface on far side (either over thin fence/hedge/car hood or climbing onto a raised deck)
            Vector3 farProbe = lowHit.point + fwd * 1.28f + Vector3.up * 1.65f;
            if (Physics.Raycast(farProbe, Vector3.down, out RaycastHit farHit, 3.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                landPos = farHit.point + Vector3.up * 0.04f;
            }
            else
            {
                landPos = topHit.point + fwd * 0.35f + Vector3.up * 0.04f;
            }

            Vector3 capBottom = landPos + Vector3.up * 0.45f;
            Vector3 capTop = landPos + Vector3.up * 1.45f;
            if (Physics.CheckCapsule(capBottom, capTop, 0.28f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            peakPos = Vector3.Lerp(transform.position, landPos, 0.50f);
            float baseHeight = Mathf.Max(transform.position.y, landPos.y);
            peakPos.y = baseHeight + 0.27f + Mathf.Max(0f, topHit.point.y - baseHeight) * 0.63f;
            support = topHit.point + Vector3.up * 0.028f;
            return true;
        }

        private void StartVault(Vector3 peakPos, Vector3 landPos, Vector3? support = null)
        {
            if (_vaultRoutine != null)
            {
                StopCoroutine(_vaultRoutine);
            }
            _vaultRoutine = StartCoroutine(VaultRoutine(transform.position, peakPos, landPos, support));
        }

        private IEnumerator VaultRoutine(Vector3 startPos, Vector3 peakPos, Vector3 landPos, Vector3? support)
        {
            _isVaulting = true;
            _verticalVelocity = 0f;

            if (_isSwimming)
            {
                _isSwimming = false;
                if (humanoid != null) humanoid.SetSwimming(false, 0f);
            }

            if (humanoid != null)
            {
                humanoid.PlayVault(support);
            }

            if (_footstepAudio != null && _sharedPunchWhooshClip != null)
            {
                _footstepAudio.pitch = 1.18f;
                _footstepAudio.PlayOneShot(_sharedPunchWhooshClip, 0.22f);
            }

            _controller.enabled = false;
            const float duration = 0.42f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 pos = Vector3.Lerp(startPos, landPos, Mathf.SmoothStep(0f, 1f, t));
                float lift = Mathf.Max(0f, peakPos.y - (startPos.y + landPos.y) * 0.5f);
                pos.y += lift * Mathf.Sin(t * Mathf.PI);
                transform.position = pos;
                yield return null;
            }

            transform.position = landPos;
            _controller.enabled = true;
            _isVaulting = false;
            _vaultRoutine = null;

            if (humanoid != null) humanoid.PlayLanding();
            else HandleLanded();
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
            if (_sharedPunchHitClip == null)
            {
                _sharedPunchHitClip = BuildProceduralPunchClip(44100, isHit: true, heavy: false);
            }
            if (_sharedHeavyHitClip == null)
            {
                _sharedHeavyHitClip = BuildProceduralPunchClip(44100, isHit: true, heavy: true);
            }
            if (_sharedPunchWhooshClip == null)
            {
                _sharedPunchWhooshClip = BuildProceduralPunchClip(44100, isHit: false, heavy: false);
            }
            if (_sharedSplashClip == null)
            {
                _sharedSplashClip = BuildProceduralSplashClip(44100);
            }
        }

        private static AudioClip BuildProceduralPunchClip(int sampleRate, bool isHit, bool heavy)
        {
            int count = Mathf.RoundToInt(sampleRate * (heavy ? 0.16f : (isHit ? 0.11f : 0.09f)));
            float[] data = new float[count];
            System.Random rng = new System.Random(heavy ? 55123 : (isHit ? 31337 : 42424));
            float lp = 0f;
            double phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float env = isHit ? Mathf.Exp(-t * (heavy ? 10.5f : 14f)) : Mathf.Sin(t * Mathf.PI) * 0.6f;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (isHit ? 0.45f : 0.18f) * (white - lp);
                if (isHit)
                {
                    float freq = Mathf.Lerp(heavy ? 125f : 160f, heavy ? 34f : 45f, t);
                    phase += (2.0 * Math.PI * freq) / sampleRate;
                    float crack = heavy ? white * Mathf.Exp(-t * 32f) * 0.45f : 0f;
                    data[i] = Mathf.Clamp(((float)Math.Sin(phase) * (heavy ? 0.88f : 0.78f) + lp * 0.55f + crack) * env, -1f, 1f);
                }
                else
                {
                    data[i] = Mathf.Clamp(lp * env * 0.8f, -1f, 1f);
                }
            }
            AudioClip clip = AudioClip.Create(heavy ? "HeavyHit" : (isHit ? "PunchHit" : "PunchWhoosh"), count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildProceduralSplashClip(int sampleRate)
        {
            int count = Mathf.RoundToInt(sampleRate * 0.38f);
            float[] data = new float[count];
            System.Random rng = new System.Random(77889);
            float lp1 = 0f;
            float lp2 = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float attack = Mathf.Clamp01(t * 28f);
                float env = attack * Mathf.Exp(-t * 5.2f);
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += 0.24f * (white - lp1);
                lp2 += 0.08f * (lp1 - lp2);
                float bubbles = Mathf.Sin(t * 140f) * 0.15f * lp2;
                data[i] = Mathf.Clamp((lp1 * 0.68f + bubbles) * env, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("WaterSplash", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
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
            if (!active)
            {
                CarryableProp.DropHeldPropIfAny();
                StreetCatController.CancelActivePet();
            }
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
            if (seated)
            {
                CarryableProp.DropHeldPropIfAny();
                StreetCatController.CancelActivePet();
            }
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
            _lastGroundCheckY = position.y;
            _hasLastGroundY = true;
            _groundSampleHoldTimer = 0f;
            _controller.enabled = wasEnabled;
        }

        private void Update()
        {
            if (!_controller.enabled || _isVaulting)
            {
                return;
            }

            // Safety net: never allow falling below Bosphorus seabed (-3.5m)
            if (transform.position.y < -3.5f)
            {
                Vector3 safePos = new Vector3(
                    Mathf.Clamp(transform.position.x, -242f, 242f),
                    0.25f,
                    Mathf.Clamp(transform.position.z, -242f, 242f));
                TeleportTo(safePos, transform.rotation);
                return;
            }

            if (_cameraRig == null)
            {
                _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            }

            float dt = Time.deltaTime;
            if (_punchCooldown > 0f)
            {
                _punchCooldown -= dt;
            }
            if (_comboResetTimer > 0f)
            {
                _comboResetTimer -= dt;
            }

            Vector2 moveInput = (_movementEnabled && InputHub.Instance != null)
                ? InputHub.Instance.MoveInput
                : Vector2.zero;
            bool sprint = _movementEnabled && InputHub.Instance != null && InputHub.Instance.SprintHeld;

            Vector3 forward = _cameraRig != null ? _cameraRig.FlatForward : Vector3.forward;
            Vector3 right = _cameraRig != null ? _cameraRig.FlatRight : Vector3.right;

            Vector3 desiredDir = forward * moveInput.y + right * moveInput.x;
            float inputMag = Mathf.Clamp01(desiredDir.magnitude);
            float pickupSpeed = 0f;
            Vector3 desiredPickupDirection = Vector3.zero;
            CarryableProp carry = CarryableProp.CurrentlyHeld;
            bool pickingUp = carry != null && carry.TryGetPickupMotion(this,
                _movementEnabled && _controller.isGrounded && !_isSwimming, desiredDir,
                out desiredPickupDirection, out pickupSpeed);
            bool autoApproach = pickingUp;
            if (!pickingUp && StreetCatController.ActivePet != null)
            {
                autoApproach = StreetCatController.ActivePet.TryGetPetMotion(this,
                    _movementEnabled && _controller.isGrounded && !_isSwimming, desiredDir,
                    out desiredPickupDirection, out pickupSpeed);
            }
            if (autoApproach) desiredDir = desiredPickupDirection;
            if (desiredDir.sqrMagnitude > 0.0001f)
            {
                desiredDir.Normalize();
                _moveDirection = desiredDir;
                Quaternion targetRot = Quaternion.LookRotation(_moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-(_isSwimming ? 7.5f : rotationSpeed) * dt));
            }

            // Check swimmable water zones (e.g. Çınarlı Açık Yüzme Havuzu)
            bool inWaterZone = WaterZone.TrySampleWater(transform.position, out float surfaceY, out float floorY);
            bool shouldSwim = inWaterZone && (surfaceY - transform.position.y) >= (_isSwimming ? 0.45f : 0.72f);

            if (shouldSwim && !_isSwimming)
            {
                CarryableProp.DropHeldPropIfAny();
                _isSwimming = true;
                _waterSurfaceY = surfaceY;
                _verticalVelocity *= 0.25f;
                if (_footstepAudio != null && _sharedSplashClip != null)
                {
                    _footstepAudio.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
                    _footstepAudio.PlayOneShot(_sharedSplashClip, 0.48f);
                }
            }
            else if (!shouldSwim && _isSwimming)
            {
                _isSwimming = false;
                _isUnderwater = false;
                _swimOxygen01 = 1f;
                if (humanoid != null)
                {
                    humanoid.SetSwimming(false, 0f, false);
                }
            }

            if (_isSwimming)
            {
                _waterSurfaceY = surfaceY;
                // Secondary/Attack tap toggles Breaststroke vs Freestyle; holding Jump dives underwater!
                bool diveHeld = InputHub.Instance != null && InputHub.Instance.JumpHeld && _swimOxygen01 > 0.08f;
                if (InputHub.Instance != null && InputHub.Instance.AttackHeld && InputHub.Instance.AttackHoldSeconds < 0.04f)
                {
                    _swimBreaststroke = !_swimBreaststroke;
                    PlayerModeController.Instance?.RaiseStatusMessage(_swimBreaststroke
                        ? "Yüzme Stili: Kurbağalama (Düşük yorgunluk)"
                        : "Yüzme Stili: Serbest Stil (Yüksek hız)");
                }

                _isUnderwater = diveHeld && (surfaceY - floorY) >= 1.15f;
                if (_isUnderwater)
                {
                    _swimOxygen01 = Mathf.Max(0f, _swimOxygen01 - dt * 0.11f); // ~9 seconds underwater breath
                    _bubbleTimer -= dt;
                    if (_bubbleTimer <= 0f)
                    {
                        _bubbleTimer = 0.28f;
                        GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.95f, new Color(0.76f, 0.96f, 1.0f), count: 3, spread: 0.18f);
                    }
                }
                else
                {
                    _swimOxygen01 = Mathf.Min(1f, _swimOxygen01 + dt * 0.38f);
                }

                float baseSwim = _swimBreaststroke ? 2.55f : 2.95f;
                float sprintSwim = _swimBreaststroke ? 3.75f : 4.45f;
                float swimTargetSpeed = (sprint ? sprintSwim : baseSwim) * inputMag;
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, swimTargetSpeed, 6.8f * dt);

                // Buoyancy holds near surface OR glides underwater when diving!
                float surfaceBodyY = Mathf.Lerp(surfaceY - 1.12f, surfaceY - 0.88f, Mathf.Clamp01(_currentSpeed / 2.2f));
                float desiredBodyY = _isUnderwater ? Mathf.Max(floorY + 0.12f, surfaceY - 1.55f) : Mathf.Max(floorY + 0.08f, surfaceBodyY);
                float heightErr = desiredBodyY - transform.position.y;
                _verticalVelocity = Mathf.Lerp(_verticalVelocity, Mathf.Clamp(heightErr * 6.5f, -3.5f, 3.8f), 1f - Mathf.Exp(-8f * dt));

                Vector3 swimVelocity = _moveDirection * _currentSpeed + Vector3.up * _verticalVelocity;
                _controller.Move(swimVelocity * dt);

                if (humanoid != null)
                {
                    humanoid.SetLookAtPoint(false, Vector3.zero);
                    humanoid.SetSwimming(true, _currentSpeed, _swimBreaststroke);
                }
                return;
            }

            bool isTired = PlayerNeedsController.Instance != null && PlayerNeedsController.Instance.IsTired;
            bool hasShoes = SaveManager.Instance != null && SaveManager.Instance.Data != null && SaveManager.Instance.Data.hasRunningShoes;
            float baseRun = hasShoes ? (runSpeed * 1.22f) : runSpeed;
            float effectiveRunSpeed = isTired ? (baseRun * 0.75f) : baseRun;
            float targetSpeed = autoApproach ? pickupSpeed : (sprint ? effectiveRunSpeed : walkSpeed) * inputMag;
            float rate = targetSpeed > _currentSpeed ? acceleration : deceleration;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, rate * dt);

            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }
            }
            else
            {
                _verticalVelocity += gravity * dt;
            }

            Vector3 velocity = _moveDirection * _currentSpeed + _combatLungeVelocity + Vector3.up * _verticalVelocity;
            _combatLungeVelocity = Vector3.MoveTowards(_combatLungeVelocity, Vector3.zero, 16f * dt);
            Vector3 start = transform.position;
            _controller.Move(velocity * dt);
            Vector3 travel = Vector3.ProjectOnPlane(transform.position - start, Vector3.up);
            float actualSpeed = dt > 0f ? travel.magnitude / dt : 0f;

            if (characterAnimator != null)
            {
                characterAnimator.SetFloat(SpeedHash, actualSpeed, 0.08f, dt);
                characterAnimator.SetBool(GroundedHash, _controller.isGrounded);
            }

            if (humanoid != null)
            {
                if (StreetCatController.ActivePet != null)
                {
                    humanoid.SetLookAtPoint(true, StreetCatController.ActivePet.transform.position + Vector3.up * 0.28f);
                }
                else
                {
                    InteractableTrigger lookTrig = PlayerModeController.Instance != null
                        ? PlayerModeController.Instance.ClosestInteractableWithin4m
                        : null;
                    if (lookTrig != null)
                    {
                        float lookY = (lookTrig.Kind == InteractableKind.StreetCat || lookTrig.Kind == InteractableKind.CarryableProp)
                            ? 0.32f
                            : 1.2f;
                        humanoid.SetLookAtPoint(true, lookTrig.transform.position + Vector3.up * lookY);
                    }
                    else
                    {
                        humanoid.SetLookAtPoint(false, Vector3.zero);
                    }
                }

                if (!_hasLastGroundY)
                {
                    _lastGroundCheckY = transform.position.y;
                    _hasLastGroundY = true;
                }
                if (_controller.isGrounded && Mathf.Abs(transform.position.y - _lastGroundCheckY) > 0.003f)
                {
                    _groundSampleHoldTimer = 0.85f;
                }
                _lastGroundCheckY = transform.position.y;
                if (_groundSampleHoldTimer > 0f)
                {
                    _groundSampleHoldTimer = Mathf.Max(0f, _groundSampleHoldTimer - dt);
                }
                if (_controller.isGrounded && actualSpeed > 0.08f && _groundSampleHoldTimer <= 0f)
                {
                    _slopeProbeTimer -= dt;
                    if (_slopeProbeTimer <= 0f)
                    {
                        _slopeProbeTimer = 0.14f;
                        Vector3 probeDir = travel.sqrMagnitude > 0.0001f ? travel.normalized : transform.forward;
                        Vector3 probeOrigin = transform.position + probeDir * 0.38f + Vector3.up * 0.38f;
                        if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit stepHit, 0.80f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                            stepHit.collider.transform.root != transform.root &&
                            (Mathf.Abs(stepHit.point.y - transform.position.y) > 0.02f || stepHit.normal.y < 0.985f))
                        {
                            _groundSampleHoldTimer = 0.85f;
                        }
                    }
                }

                bool sampleGround = _controller.isGrounded &&
                                    (_groundSampleHoldTimer > 0f || humanoid.HasUnevenGroundSample);
                humanoid.SetLocomotion(actualSpeed, _controller.isGrounded, _verticalVelocity, travel.normalized, sampleGround: sampleGround);
            }
        }
    }
}
