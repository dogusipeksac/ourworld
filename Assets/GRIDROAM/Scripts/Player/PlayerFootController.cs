using System;
using System.Collections;
using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Missions;
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
        private static readonly int CrouchingHash = Animator.StringToHash("IsCrouching");
        private static readonly int SwimmingHash = Animator.StringToHash("IsSwimming");
        private static readonly int PunchTrigHash = Animator.StringToHash("Punch");
        private static readonly int DodgeRollTrigHash = Animator.StringToHash("DodgeRoll");
        private static readonly int JumpTrigHash = Animator.StringToHash("Jump");
        private static readonly int HitReactTrigHash = Animator.StringToHash("HitReact");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color[] JacketColors =
        {
            new Color(0.16f, 0.27f, 0.42f, 1f), // 0: Klasik Lacivert
            new Color(0.48f, 0.12f, 0.16f, 1f), // 1: Bordo Deri
            new Color(0.24f, 0.36f, 0.20f, 1f), // 2: Zeytin Parka
            new Color(0.14f, 0.15f, 0.18f, 1f), // 3: Antrasit Sokak
            new Color(0.10f, 0.56f, 0.58f, 1f), // 4: İstanbul Turkuazı
            new Color(0.78f, 0.54f, 0.14f, 1f), // 5: Safran Hardal
            new Color(0.84f, 0.79f, 0.69f, 1f), // 6: Krem Keten
            new Color(0.34f, 0.18f, 0.44f, 1f)  // 7: Mürdüm Kadife
        };

        public static int JacketColorCount => JacketColors.Length;
        public static Color GetJacketColor(int index) => JacketColors[Mathf.Clamp(index, 0, JacketColors.Length - 1)];

        private static readonly Color[] PantsColors =
        {
            new Color(0.13f, 0.13f, 0.16f, 1f), // 0: Antrasit Klasik
            new Color(0.16f, 0.25f, 0.40f, 1f), // 1: Koyu Denim
            new Color(0.46f, 0.40f, 0.30f, 1f), // 2: Haki Keten
            new Color(0.24f, 0.30f, 0.21f, 1f), // 3: Zeytin Kargo
            new Color(0.36f, 0.15f, 0.18f, 1f), // 4: Bordo Chino
            new Color(0.80f, 0.76f, 0.68f, 1f)  // 5: Krem Yazlık
        };

        private static readonly Color[] HairColors =
        {
            new Color(0.09f, 0.07f, 0.06f, 1f), // 0: Gece Siyahı
            new Color(0.26f, 0.15f, 0.08f, 1f), // 1: Kestane Kahve
            new Color(0.60f, 0.46f, 0.24f, 1f), // 2: Karamel Sarı
            new Color(0.56f, 0.58f, 0.62f, 1f), // 3: Gümüş Kır
            new Color(0.52f, 0.18f, 0.12f, 1f), // 4: Bakır Kızıl
            new Color(0.12f, 0.24f, 0.44f, 1f)  // 5: Gece Mavisi
        };

        public static int PantsColorCount => PantsColors.Length;
        public static Color GetPantsColor(int index) => PantsColors[Mathf.Clamp(index, 0, PantsColors.Length - 1)];
        public static int HairColorCount => HairColors.Length;
        public static Color GetHairColor(int index) => HairColors[Mathf.Clamp(index, 0, HairColors.Length - 1)];

        private GameObject _sunglassesObj;
        private GameObject _streetCapObj;

        private static AudioClip _sharedFootstepClip;
        private static AudioClip _sharedFootstepGrassClip;
        private static AudioClip _sharedFootstepSandClip;
        private static AudioClip _sharedFootstepWoodClip;
        private static AudioClip _sharedFootstepIndoorClip;
        private static AudioClip _sharedFootstepWetClip;
        private static AudioClip _sharedPunchHitClip;
        private static AudioClip _sharedHeavyHitClip;
        private static AudioClip _sharedPunchWhooshClip;
        private static AudioClip _sharedSplashClip;

        private enum GroundSurfaceKind
        {
            Stone,
            Grass,
            Sand,
            Wood,
            Indoor
        }

        private CharacterController _controller;
        private float _nextCamRigLookupTime;
        private ThirdPersonCameraRig _cameraRig;
        private AudioSource _footstepAudio;
        private float _verticalVelocity;
        private float _currentSpeed;
        private Vector3 _planarVelocity;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _pivotDustCooldown;
        private float _rainWetness01;
        private bool _warnedRainSoaked;
        private int _wetShoeStepsRemaining;
        private bool _nextFootprintRight;
        private float _drownTickTimer;
        private GroundSurfaceKind _groundSurface = GroundSurfaceKind.Stone;
        private float _punchCooldown;
        private int _comboStep;
        private float _comboResetTimer;
        private Vector3 _combatLungeVelocity;
        private Vector3 _hitStumbleVelocity;
        private bool _wasGrounded = true;
        private float _airPeakY;
        private bool _isVaulting;
        private Coroutine _vaultRoutine;
        private bool _isSwimming;
        private bool _isUnderwater;
        private bool _swimBreaststroke;
        private float _swimOxygen01 = 1f;
        private float _bubbleTimer;
        private float _swimSplashTimer;
        private float _waterSurfaceY;
        private Vector3 _moveDirection = Vector3.forward;
        private bool _movementEnabled = true;
        private float _lastGroundCheckY;
        private bool _hasLastGroundY;
        private float _groundSampleHoldTimer;
        private float _slopeProbeTimer;

        // GTA 6 Tactical Movement: Stealth Crouch, Sprint-Slide, Combat Dodge Roll & Vehicle Bail-Out
        private bool _isCrouching;
        private float _sprintSlideTimer;
        private Vector3 _sprintSlideDir;
        private float _dodgeRollTimer;
        private float _dodgeCooldown;
        private Vector3 _dodgeRollDir;
        private float _bailOutTimer;
        private float _standingControllerHeight = 1.80f;
        private float _standingControllerCenterY = 0.90f;
        private readonly System.Collections.Generic.List<Material> _ownedAccessoryMats = new System.Collections.Generic.List<Material>(4);
        private readonly System.Collections.Generic.Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode> _fpSavedShadows
            = new System.Collections.Generic.Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode>(24);
        private bool _isVisualHiddenForFirstPerson;

        public Transform VisualRoot => visualRoot;
        public ProceduralHumanoid Humanoid => humanoid;
        public float CurrentSpeed => _currentSpeed;
        public bool IsSwimming => _isSwimming;
        public bool IsUnderwater => _isUnderwater;
        public float SwimOxygen01 => _swimOxygen01;
        public float RainWetness01 => _rainWetness01;
        public bool IsRainSoaked => _rainWetness01 > 0.50f;
        public bool IsCrouching => _isCrouching;
        public bool IsSprintSliding => _sprintSlideTimer > 0f;
        public bool IsDodgeInvulnerable => _dodgeRollTimer > 0.06f;

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
            if (_vaultRoutine != null)
            {
                StopCoroutine(_vaultRoutine);
                _vaultRoutine = null;
            }
            _isSwimming = false;
            _isVaulting = false;
            _planarVelocity = Vector3.zero;
            _currentSpeed = 0f;
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            if (_cameraRig != null)
            {
                _cameraRig.SetSprintMotion(0f);
            }
        }

        private void Start()
        {
            if (_controller != null)
            {
                _standingControllerHeight = _controller.height;
                _standingControllerCenterY = _controller.center.y;
            }
            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnAttackPressed += HandleAttackPressed;
                InputHub.Instance.OnJumpPressed += HandleJumpOrVaultPressed;
                InputHub.Instance.OnCrouchSlidePressed += ToggleCrouchOrSlide;
                InputHub.Instance.OnDodgeRollPressed += TriggerDodgeRoll;
            }
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                ApplySavedAppearance();
            }
        }

        public void ApplySavedAppearance()
        {
            SaveData data = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (data == null) return;
            ApplyJacketColor(data.playerJacketColorIndex);
            ApplyPantsColor(data.playerPantsColorIndex);
            ApplyHairColor(data.playerHairColorIndex);
            ApplyHeadAccessories(data.ownsSunglasses && data.equipSunglasses, data.ownsStreetCap && data.equipStreetCap);
        }

        private void TrySetMixamoAnimatorBool(int hash, bool val)
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null) return;
            var ps = characterAnimator.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].nameHash == hash && ps[i].type == AnimatorControllerParameterType.Bool)
                {
                    characterAnimator.SetBool(hash, val);
                    return;
                }
            }
        }

        private void TryTriggerMixamoAnimator(int hash)
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null) return;
            var ps = characterAnimator.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].nameHash == hash && ps[i].type == AnimatorControllerParameterType.Trigger)
                {
                    characterAnimator.SetTrigger(hash);
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnAttackPressed -= HandleAttackPressed;
                InputHub.Instance.OnJumpPressed -= HandleJumpOrVaultPressed;
                InputHub.Instance.OnCrouchSlidePressed -= ToggleCrouchOrSlide;
                InputHub.Instance.OnDodgeRollPressed -= TriggerDodgeRoll;
            }
            for (int i = 0; i < _ownedAccessoryMats.Count; i++)
            {
                if (_ownedAccessoryMats[i] != null) Destroy(_ownedAccessoryMats[i]);
            }
            _ownedAccessoryMats.Clear();
        }

        public void ToggleCrouchOrSlide()
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (!_movementEnabled || _isSwimming || _isVaulting || _dodgeRollTimer > 0f || _bailOutTimer > 0f) return;
            if (mode == null || mode.CurrentMode != ControlMode.OnFoot || mode.IsModalLocked) return;

            // Sprinting + C triggers a high-momentum GTA Sprint-Slide!
            if (!_isCrouching && _controller != null && _controller.isGrounded && _currentSpeed >= 3.85f && _sprintSlideTimer <= 0f)
            {
                _sprintSlideTimer = 0.56f;
                _sprintSlideDir = _moveDirection.sqrMagnitude > 0.01f ? _moveDirection.normalized : transform.forward;
                _isCrouching = true;
                humanoid?.SetSprintSlide(true);
                humanoid?.SetCrouch(true);
                SampleGroundSurface();
                VisualEffectsManager.SpawnFootDust(transform.position, heavyLanding: true, tintOverride: GetSurfaceDustTint(_groundSurface));
                if (_footstepAudio != null && _sharedPunchWhooshClip != null)
                {
                    _footstepAudio.pitch = 0.92f;
                    _footstepAudio.PlayOneShot(_sharedPunchWhooshClip, 0.30f);
                }
                _cameraRig?.AddLandingDip(0.55f);
                return;
            }

            SetCrouching(!_isCrouching);
        }

        public void SetCrouching(bool crouch)
        {
            if (_isCrouching == crouch) return;
            if (!crouch && _controller != null)
            {
                // Verify headroom before standing up inside low spaces
                Vector3 headProbe = transform.position + Vector3.up * 1.15f;
                if (Physics.SphereCast(headProbe, 0.22f, Vector3.up, out _, 0.65f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    return;
                }
            }
            _isCrouching = crouch;
            humanoid?.SetCrouch(_isCrouching);
            if (_controller != null)
            {
                _controller.height = _isCrouching ? Mathf.Min(1.22f, _standingControllerHeight * 0.68f) : _standingControllerHeight;
                Vector3 c = _controller.center;
                c.y = _isCrouching ? _controller.height * 0.5f : _standingControllerCenterY;
                _controller.center = c;
            }
            GameFeelAudio.PlayUiClick();
        }

        public void TriggerDodgeRoll()
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (!_movementEnabled || _isSwimming || _isVaulting || _dodgeRollTimer > 0f || _dodgeCooldown > 0f || _bailOutTimer > 0f) return;
            if (mode == null || mode.CurrentMode != ControlMode.OnFoot || mode.IsModalLocked) return;
            if (_controller == null || !_controller.isGrounded) return;

            CarryableProp.DropHeldPropIfAny();
            StreetCatController.CancelActivePet();

            Vector2 inp = InputHub.Instance != null ? InputHub.Instance.MoveInput : Vector2.zero;
            Vector3 fwd = _cameraRig != null ? _cameraRig.FlatForward : transform.forward;
            Vector3 right = _cameraRig != null ? _cameraRig.FlatRight : transform.right;
            Vector3 rollDir = fwd * inp.y + right * inp.x;
            if (rollDir.sqrMagnitude < 0.04f) rollDir = transform.forward;
            rollDir.y = 0f;
            rollDir.Normalize();

            _dodgeRollDir = rollDir;
            _dodgeRollTimer = 0.48f;
            _dodgeCooldown = 0.85f;
            _sprintSlideTimer = 0f;
            humanoid?.SetSprintSlide(false);
            transform.rotation = Quaternion.LookRotation(rollDir, Vector3.up);
            humanoid?.PlayDodgeRoll(0.48f);
            TryTriggerMixamoAnimator(DodgeRollTrigHash);

            SampleGroundSurface();
            VisualEffectsManager.SpawnFootDust(transform.position, heavyLanding: false, tintOverride: GetSurfaceDustTint(_groundSurface));
            if (_footstepAudio != null && _sharedPunchWhooshClip != null)
            {
                _footstepAudio.pitch = 1.18f;
                _footstepAudio.PlayOneShot(_sharedPunchWhooshClip, 0.34f);
            }
            _cameraRig?.AddFovImpulse(2.2f);
            _cameraRig?.AddLandingDip(0.35f);
        }

        public void TriggerVehicleBailOut(Vector3 carVelocity, float speedKmh)
        {
            SetCrouching(false);
            _sprintSlideTimer = 0f;
            _dodgeRollTimer = 0f;
            _bailOutTimer = 1.55f;

            Vector3 flatVel = new Vector3(carVelocity.x, 0f, carVelocity.z);
            float launchSpd = Mathf.Clamp(flatVel.magnitude * 0.72f, 5.5f, 13.5f);
            Vector3 dir = flatVel.sqrMagnitude > 0.1f ? flatVel.normalized : transform.forward;
            _hitStumbleVelocity = dir * launchSpd;
            _planarVelocity = Vector3.zero;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            humanoid?.PlayKnockdown(1.55f);

            VisualEffectsManager.SpawnFootDust(transform.position, heavyLanding: true);
            _cameraRig?.AddShake(0.28f, 0.35f);
            _cameraRig?.AddFovImpulse(4.5f);

            bool en = LocalizationService.IsEnglish;
            PlayerModeController.Instance?.RaiseStatusMessage(en
                ? $"💥 Bailed out at {Mathf.RoundToInt(speedKmh)} km/h!"
                : $"💥 {Mathf.RoundToInt(speedKmh)} km/h hızda hareketli araçtan atladın!");

            if (speedKmh >= 55f)
            {
                float scrapeDmg = Mathf.Clamp((speedKmh - 45f) * 0.42f, 8f, 38f);
                PlayerNeedsController.Instance?.TakeDamage(scrapeDmg, transform.position - dir, en ? "Vehicle Bail-Out" : "Hareketli Araçtan Atlama", heavyHit: speedKmh >= 85f);
            }
        }

        public void ApplyJacketColor(int colorIndex)
        {
            ApplyMaterialTint("MAT_Char_Shirt", JacketColors[Mathf.Clamp(colorIndex, 0, JacketColors.Length - 1)]);
        }

        public void ApplyPantsColor(int colorIndex)
        {
            ApplyMaterialTint("MAT_Char_Pants", PantsColors[Mathf.Clamp(colorIndex, 0, PantsColors.Length - 1)]);
        }

        public void ApplyHairColor(int colorIndex)
        {
            ApplyMaterialTint("MAT_Char_Hair", HairColors[Mathf.Clamp(colorIndex, 0, HairColors.Length - 1)]);
        }

        private void ApplyMaterialTint(string materialToken, Color col)
        {
            if (visualRoot == null) return;
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                Material[] mats = r.materials;
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != null && mats[m].name.Contains(materialToken))
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

        public void ApplyHeadAccessories(bool showSunglasses, bool showCap)
        {
            Transform headTf = humanoid != null ? humanoid.HeadPivot : null;
            if (headTf == null && visualRoot != null)
            {
                foreach (Transform t in visualRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Head") { headTf = t; break; }
                }
            }
            if (headTf == null) return;

            if (showSunglasses && _sunglassesObj == null)
            {
                _sunglassesObj = BuildSunglassesRig(headTf);
            }
            if (_sunglassesObj != null) _sunglassesObj.SetActive(showSunglasses);

            if (showCap && _streetCapObj == null)
            {
                _streetCapObj = BuildStreetCapRig(headTf);
            }
            if (_streetCapObj != null) _streetCapObj.SetActive(showCap);
            if (_isVisualHiddenForFirstPerson)
            {
                ApplyFirstPersonVisualHiding(true);
            }
        }

        private void LateUpdate()
        {
            PlayerModeController mode = PlayerModeController.Instance;
            bool wantFpHide = _cameraRig != null &&
                              _cameraRig.IsFirstPerson &&
                              !_cameraRig.ExternalCameraControl &&
                              !_cameraRig.IsAimOrSportOverride &&
                              !OurWorld.UI.MainMenuController.IsMenuActive &&
                              mode != null &&
                              (mode.CurrentMode == ControlMode.OnFoot || mode.CurrentMode == ControlMode.Seated || mode.CurrentMode == ControlMode.Cycling);

            if (wantFpHide != _isVisualHiddenForFirstPerson)
            {
                ApplyFirstPersonVisualHiding(wantFpHide);
            }
        }

        private void ApplyFirstPersonVisualHiding(bool hide)
        {
            _isVisualHiddenForFirstPerson = hide;
            if (visualRoot == null) return;

            Renderer[] rends = visualRoot.GetComponentsInChildren<Renderer>(true);
            if (hide)
            {
                for (int i = 0; i < rends.Length; i++)
                {
                    Renderer r = rends[i];
                    if (r == null) continue;
                    if (!_fpSavedShadows.ContainsKey(r))
                    {
                        _fpSavedShadows[r] = r.shadowCastingMode;
                    }
                    if (_fpSavedShadows[r] == UnityEngine.Rendering.ShadowCastingMode.Off)
                    {
                        r.enabled = false;
                    }
                    else
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    }
                }
            }
            else
            {
                for (int i = 0; i < rends.Length; i++)
                {
                    Renderer r = rends[i];
                    if (r == null) continue;
                    if (_fpSavedShadows.TryGetValue(r, out UnityEngine.Rendering.ShadowCastingMode orig))
                    {
                        r.shadowCastingMode = orig;
                    }
                    r.enabled = true;
                }
                _fpSavedShadows.Clear();
            }
        }

        private GameObject BuildSunglassesRig(Transform headTf)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material lensMat = new Material(sh);
            _ownedAccessoryMats.Add(lensMat);
            if (lensMat.HasProperty(BaseColorId)) lensMat.SetColor(BaseColorId, new Color(0.05f, 0.08f, 0.14f, 1f));
            if (lensMat.HasProperty("_Smoothness")) lensMat.SetFloat("_Smoothness", 0.92f);
            if (lensMat.HasProperty("_Metallic")) lensMat.SetFloat("_Metallic", 0.45f);

            Material frameMat = new Material(sh);
            _ownedAccessoryMats.Add(frameMat);
            if (frameMat.HasProperty(BaseColorId)) frameMat.SetColor(BaseColorId, new Color(0.86f, 0.70f, 0.26f, 1f));
            if (frameMat.HasProperty("_Smoothness")) frameMat.SetFloat("_Smoothness", 0.85f);
            if (frameMat.HasProperty("_Metallic")) frameMat.SetFloat("_Metallic", 0.80f);

            GameObject root = new GameObject("Accessory_Sunglasses");
            root.transform.SetParent(headTf, false);
            root.transform.localPosition = new Vector3(0f, 0.122f, 0.105f);
            root.transform.localRotation = Quaternion.identity;

            CreateAccessoryPart("LensL", PrimitiveType.Cube, root.transform, new Vector3(-0.044f, 0f, 0.008f), new Vector3(0.068f, 0.038f, 0.014f), lensMat);
            CreateAccessoryPart("LensR", PrimitiveType.Cube, root.transform, new Vector3(0.044f, 0f, 0.008f), new Vector3(0.068f, 0.038f, 0.014f), lensMat);
            CreateAccessoryPart("Bridge", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.008f, 0.010f), new Vector3(0.034f, 0.009f, 0.012f), frameMat);
            CreateAccessoryPart("TopBar", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.020f, 0.009f), new Vector3(0.158f, 0.008f, 0.012f), frameMat);
            CreateAccessoryPart("ArmL", PrimitiveType.Cube, root.transform, new Vector3(-0.082f, 0.004f, -0.056f), new Vector3(0.008f, 0.010f, 0.132f), frameMat);
            CreateAccessoryPart("ArmR", PrimitiveType.Cube, root.transform, new Vector3(0.082f, 0.004f, -0.056f), new Vector3(0.008f, 0.010f, 0.132f), frameMat);
            return root;
        }

        private GameObject BuildStreetCapRig(Transform headTf)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material capMat = new Material(sh);
            _ownedAccessoryMats.Add(capMat);
            if (capMat.HasProperty(BaseColorId)) capMat.SetColor(BaseColorId, new Color(0.10f, 0.24f, 0.48f, 1f));
            if (capMat.HasProperty("_Smoothness")) capMat.SetFloat("_Smoothness", 0.25f);

            Material accentMat = new Material(sh);
            _ownedAccessoryMats.Add(accentMat);
            if (accentMat.HasProperty(BaseColorId)) accentMat.SetColor(BaseColorId, new Color(0.95f, 0.72f, 0.20f, 1f));
            if (accentMat.HasProperty("_Smoothness")) accentMat.SetFloat("_Smoothness", 0.35f);

            GameObject root = new GameObject("Accessory_StreetCap");
            root.transform.SetParent(headTf, false);
            root.transform.localPosition = new Vector3(0f, 0.175f, 0.008f);
            root.transform.localRotation = Quaternion.Euler(-6f, 0f, 0f);

            CreateAccessoryPart("Crown", PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.010f, -0.012f), new Vector3(0.248f, 0.145f, 0.252f), capMat);
            CreateAccessoryPart("Visor", PrimitiveType.Cube, root.transform, new Vector3(0f, -0.014f, 0.140f), new Vector3(0.195f, 0.014f, 0.115f), capMat, Quaternion.Euler(10f, 0f, 0f));
            CreateAccessoryPart("Emblem", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.028f, 0.112f), new Vector3(0.056f, 0.026f, 0.012f), accentMat);
            CreateAccessoryPart("TopButton", PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.082f, -0.012f), new Vector3(0.026f, 0.014f, 0.026f), accentMat);
            return root;
        }

        private static void CreateAccessoryPart(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (rot.HasValue) go.transform.localRotation = rot.Value;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void HandleAttackPressed()
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

            // Event-driven swimming stroke toggle (zero frame-rate double-toggle bug!)
            if (_isSwimming)
            {
                _swimBreaststroke = !_swimBreaststroke;
                bool en = LocalizationService.IsEnglish;
                mode.RaiseStatusMessage(_swimBreaststroke
                    ? (en ? "Swim Style: Breaststroke (Low stamina drain)" : "Yüzme Stili: Kurbağalama (Düşük yorgunluk)")
                    : (en ? "Swim Style: Freestyle (High speed)" : "Yüzme Stili: Serbest Stil (Yüksek hız)"));
                return;
            }

            if (_punchCooldown > 0f)
            {
                return;
            }

            // If carrying a physical street prop (Damacana / crate / cone), hurl it forward with two-handed throw!
            if (CarryableProp.IsHoldingProp)
            {
                _punchCooldown = 0.48f;
                CarryableProp.TryThrowHeldProp(transform.forward);
                bool isEnThrow = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
                mode.RaiseStatusMessage(isEnThrow ? "Threw the item!" : "Eşyayı fırlattın!");
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
            TryTriggerMixamoAnimator(PunchTrigHash);

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
            float radius = step == 2 ? 1.35f : 1.15f;
            Vector3 strikeCenter = transform.position + transform.forward * reach + Vector3.up * 0.90f;
            Collider[] hits = Physics.OverlapSphere(strikeCenter, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            TownPedestrian hitPed = null;
            VehicleDamageSystem hitCar = null;
            Collider hitCarCol = null;
            Collider hitPriorityPropCol = null;
            Vector3 hitPriorityPropPt = strikeCenter;
            float bestPriorityDistSq = float.MaxValue;
            Collider hitWorldCol = null;
            Vector3 hitWorldPt = strikeCenter;
            float bestDistSq = float.MaxValue;
            float bestCarDistSq = float.MaxValue;
            float bestWorldDistSq = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i];
                if (col == null) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

                TownPedestrian ped = col.GetComponentInParent<TownPedestrian>();
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

                VehicleDamageSystem carDmg = col.GetComponentInParent<VehicleDamageSystem>();
                if (carDmg != null)
                {
                    Vector3 pt = GetSafeClosestPoint(col, strikeCenter);
                    float dSq = (pt - strikeCenter).sqrMagnitude;
                    if (dSq < bestCarDistSq)
                    {
                        bestCarDistSq = dSq;
                        hitCar = carDmg;
                        hitCarCol = col;
                    }
                    continue;
                }

                // Ignore colliders belonging to farm/street animals here (handled by dedicated living-creature chain)
                if (col.GetComponentInParent<FarmAnimalController>() != null ||
                    col.GetComponentInParent<StreetCatController>() != null)
                {
                    continue;
                }

                Vector3 wPt = GetSafeClosestPoint(col, strikeCenter);
                float wDistSq = (wPt - strikeCenter).sqrMagnitude;

                // Prioritize interactive physical props (crates, damacana, doors, ATMs, vending machines) over adjacent walls
                bool isPriorityProp = col.GetComponentInParent<CarryableProp>() != null ||
                                      col.GetComponentInParent<DoorInteractable>() != null;
                if (isPriorityProp)
                {
                    if (wDistSq < bestPriorityDistSq)
                    {
                        bestPriorityDistSq = wDistSq;
                        hitPriorityPropCol = col;
                        hitPriorityPropPt = wPt;
                    }
                    continue;
                }

                // Skip flat ground/road/sidewalk/floor/water slabs underneath the player's feet
                if (wPt.y < transform.position.y + 0.18f || col.bounds.max.y <= transform.position.y + 0.22f)
                {
                    continue;
                }
                string nLow = col.gameObject.name.ToLowerInvariant();
                if (nLow.Contains("ground") || nLow.Contains("bedrock") || nLow.Contains("valleyfloor") ||
                    nLow.Contains("asphalt") || nLow.Contains("sidewalk") || nLow.Contains("lawn") ||
                    nLow.Contains("lakebed") || nLow.Contains("meadow") || nLow.Contains("water") ||
                    nLow.Contains("terrain") || nLow.Contains("pavement") || nLow.Contains("shore") ||
                    (nLow.Contains("floor") && !nLow.Contains("wall")))
                {
                    continue;
                }

                if (wDistSq < bestWorldDistSq)
                {
                    bestWorldDistSq = wDistSq;
                    hitWorldCol = col;
                    hitWorldPt = wPt;
                }
            }

            if (hitPriorityPropCol != null)
            {
                hitWorldCol = hitPriorityPropCol;
                hitWorldPt = hitPriorityPropPt;
            }

            bool hitLiving = false;
            if (hitPed != null)
            {
                hitPed.OnPunchedByPlayer(transform.position, transform.forward, step);
                hitLiving = true;
            }
            if (!hitLiving && LivingNeighborhoodPopulator.Instance != null)
            {
                hitLiving = LivingNeighborhoodPopulator.Instance.TryReceivePlayerPunch(
                    strikeCenter, radius, transform.position, transform.forward, step);
            }
            if (!hitLiving && IstanbulDiverseCitizensPopulator.Instance != null)
            {
                hitLiving = IstanbulDiverseCitizensPopulator.Instance.TryReceivePlayerPunch(
                    strikeCenter, radius, transform.position, transform.forward, step);
            }
            if (!hitLiving && PoliceWantedSystem.Instance != null)
            {
                hitLiving = PoliceWantedSystem.Instance.TryReceivePlayerPunch(
                    strikeCenter, radius, transform.position, transform.forward, step);
            }
            if (!hitLiving && GtaSandboxActivities.Instance != null)
            {
                hitLiving = GtaSandboxActivities.Instance.TryReceivePlayerPunch(
                    strikeCenter, radius, transform.position, transform.forward, step);
            }
            if (!hitLiving && ChapterSevenEightStoryExpansion.Instance != null)
            {
                hitLiving = ChapterSevenEightStoryExpansion.Instance.TryReceivePlayerPunch(
                    strikeCenter, radius, transform.position, transform.forward, step);
            }
            if (!hitLiving)
            {
                hitLiving = StreetCatController.TryReceivePlayerPunch(
                    strikeCenter, radius, transform.position, transform.forward, step);
            }
            if (!hitLiving)
            {
                hitLiving = FarmAnimalController.TryReceivePlayerPunch(
                    strikeCenter, radius, transform.position, transform.forward, step);
            }
            if (!hitLiving && WorldExpansionAndFarmBuilder.Instance != null)
            {
                hitLiving = WorldExpansionAndFarmBuilder.Instance.TryReceivePlayerPunch(
                    strikeCenter, radius, transform.position, transform.forward, step);
            }
            if (!hitLiving && HamidiyeMallInteractionHandler.ActiveInstance != null)
            {
                hitLiving = HamidiyeMallInteractionHandler.ActiveInstance.TryReceivePlayerPunch(
                    strikeCenter, radius, transform.position, transform.forward, step);
            }
            if (!hitLiving)
            {
                hitLiving = NeighborhoodConsequenceSystem.EnsureInstance().TryPunchAnyOtherHumanoid(
                    strikeCenter, radius, transform.position, transform.forward, step, this);
            }

            bool heavy = step >= 2;
            if (hitLiving)
            {
                VisualEffectsManager.SpawnCombatImpact(
                    strikeCenter,
                    transform.forward,
                    heavy,
                    heavy ? new Color(1.0f, 0.48f, 0.16f) : new Color(1.0f, 0.88f, 0.36f));
                DayNightCycle.Instance?.PulseImpactChromatic(heavy ? 0.22f : 0.08f);
                if (heavy)
                {
                    bool en = LocalizationService.IsEnglish;
                    VisualEffectsManager.SpawnFloatingText(
                        strikeCenter + Vector3.up * 0.45f,
                        en ? "HEAVY HIT!" : "SERT DARBE!",
                        new Color(1.0f, 0.78f, 0.22f),
                        scale: 1.05f,
                        duration: 0.85f);
                }

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
                    if (heavy) _cameraRig.AddFovImpulse(-1.6f);
                }

                GameFeelAudio.TriggerHitstop(step >= 1 ? 0.048f : 0.028f, slowScale: 0.22f);
            }
            else if (hitCar != null)
            {
                Vector3 impactPt = hitCarCol != null ? GetSafeClosestPoint(hitCarCol, strikeCenter) : strikeCenter;
                hitCar.ApplyMeleeHit(impactPt, transform.forward, step);
                VisualEffectsManager.SpawnCombatImpact(impactPt, -transform.forward, heavy, new Color(1.0f, 0.78f, 0.26f));
                DayNightCycle.Instance?.PulseImpactChromatic(heavy ? 0.18f : 0.07f);

                NeighborhoodConsequenceSystem conseq = NeighborhoodConsequenceSystem.EnsureInstance();
                conseq.PlayMetalClang(heavy ? 0.70f : 0.52f);
                conseq.ReportOffense(
                    NeighborhoodOffenseKind.VehicleDamage,
                    impactPt,
                    LocalizationService.IsEnglish ? "Parked Vehicle" : "Park Halindeki Araç",
                    repPenalty: heavy ? 10 : 6,
                    immediateFineTl: 0,
                    heatDelta: heavy ? 0.9f : 0.55f);

                if (_cameraRig != null)
                {
                    float intensity = step == 2 ? 0.20f : (step == 1 ? 0.12f : 0.07f);
                    _cameraRig.AddShake(intensity, step == 2 ? 0.18f : 0.12f);
                    if (heavy) _cameraRig.AddFovImpulse(-1.3f);
                }

                GameFeelAudio.TriggerHitstop(step >= 1 ? 0.040f : 0.024f, slowScale: 0.22f);
            }
            else if (hitWorldCol != null)
            {
                NeighborhoodConsequenceSystem.EnsureInstance().HandleWorldObjectHit(
                    hitWorldCol, hitWorldPt, transform.forward, heavy, this);

                if (_cameraRig != null)
                {
                    _cameraRig.AddShake(heavy ? 0.13f : 0.06f, heavy ? 0.14f : 0.09f);
                }
                if (heavy)
                {
                    GameFeelAudio.TriggerHitstop(0.026f, slowScale: 0.24f);
                }
            }
        }

        private static Vector3 GetSafeClosestPoint(Collider col, Vector3 point)
        {
            if (col == null) return point;
            if (col is MeshCollider mc && !mc.convex)
            {
                return col.bounds.ClosestPoint(point);
            }
            return col.ClosestPoint(point);
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

            // Buffer jump/vault for 0.14s so taps just before touchdown or during stair grounded flicker ALWAYS fire!
            _jumpBufferTimer = 0.14f;
            TryConsumeBufferedJumpOrVault();
        }

        private bool TryConsumeBufferedJumpOrVault()
        {
            if (_jumpBufferTimer <= 0f || _isVaulting || _isSwimming || _controller == null || !_controller.enabled)
            {
                return false;
            }
            if (!_controller.isGrounded && _coyoteTimer <= 0f)
            {
                return false;
            }
            if (_verticalVelocity > 0.5f)
            {
                return false;
            }

            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;

            StreetCatController.CancelActivePet();
            if (TryDetectVaultObstacle(out Vector3 vaultPeak, out Vector3 vaultLand, out Vector3 vaultSupport))
            {
                CarryableProp.DropHeldPropIfAny();
                StartVault(vaultPeak, vaultLand, vaultSupport);
                return true;
            }
            if (!CarryableProp.IsHoldingProp) CarryableProp.DropHeldPropIfAny();

            _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            TryTriggerMixamoAnimator(JumpTrigHash);
            _wasGrounded = false;
            _airPeakY = transform.position.y;
            SampleGroundSurface();
            if (_footstepAudio != null && _sharedFootstepClip != null)
            {
                _footstepAudio.pitch = UnityEngine.Random.Range(1.06f, 1.18f);
                _footstepAudio.PlayOneShot(_sharedFootstepClip, 0.18f);
            }
            VisualEffectsManager.SpawnFootDust(transform.position, heavyLanding: false, tintOverride: GetSurfaceDustTint(_groundSurface));
            return true;
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
            _wasGrounded = true;
            _airPeakY = landPos.y;
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            _planarVelocity = transform.forward * Mathf.Min(_currentSpeed, walkSpeed * 0.85f);
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
            if (_sharedFootstepGrassClip == null)
            {
                _sharedFootstepGrassClip = BuildGrassFootstepClip(44100);
            }
            if (_sharedFootstepSandClip == null)
            {
                _sharedFootstepSandClip = BuildSandFootstepClip(44100);
            }
            if (_sharedFootstepWoodClip == null)
            {
                _sharedFootstepWoodClip = BuildWoodFootstepClip(44100);
            }
            if (_sharedFootstepIndoorClip == null)
            {
                _sharedFootstepIndoorClip = BuildIndoorFootstepClip(44100);
            }
            if (_sharedFootstepWetClip == null)
            {
                _sharedFootstepWetClip = BuildWetFootstepClip(44100);
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

        private static AudioClip BuildGrassFootstepClip(int sampleRate)
        {
            int n = Mathf.RoundToInt(sampleRate * 0.085f);
            float[] data = new float[n];
            System.Random rng = new System.Random(9031);
            float lp1 = 0f, lp2 = 0f;
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.16f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 6.5f);
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += 0.34f * (w - lp1);
                lp2 += 0.09f * (lp1 - lp2);
                float rustle = (lp1 - lp2 * 0.8f) * 0.92f;
                ph += (2.0 * Math.PI * Mathf.Lerp(88f, 42f, t)) / sampleRate;
                float softThump = (float)Math.Sin(ph) * Mathf.Exp(-t * 12f) * 0.36f;
                data[i] = Mathf.Clamp((rustle + softThump) * env, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("Footstep_Grass", n, 1, sampleRate, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip BuildSandFootstepClip(int sampleRate)
        {
            int n = Mathf.RoundToInt(sampleRate * 0.105f);
            float[] data = new float[n];
            System.Random rng = new System.Random(9049);
            float lp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.20f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 5.2f);
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.52f * (w - lp);
                float crunch = (lp - prev * 0.55f) * (0.72f + 0.28f * Mathf.Sin(t * 95f));
                prev = lp;
                data[i] = Mathf.Clamp(crunch * env * 0.88f, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("Footstep_Sand", n, 1, sampleRate, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip BuildWoodFootstepClip(int sampleRate)
        {
            int n = Mathf.RoundToInt(sampleRate * 0.075f);
            float[] data = new float[n];
            System.Random rng = new System.Random(9067);
            double ph1 = 0.0, ph2 = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = Mathf.Exp(-t * 10.5f);
                // Hollow timber board resonance (175 Hz + 312 Hz)
                ph1 += (2.0 * Math.PI * Mathf.Lerp(192f, 148f, t)) / sampleRate;
                ph2 += (2.0 * Math.PI * 318.0) / sampleRate;
                float knock = (float)(Math.Sin(ph1) * 0.62 + Math.Sin(ph2) * 0.34);
                float tap = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 28f) * 0.32f;
                data[i] = Mathf.Clamp((knock + tap) * env, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("Footstep_Wood", n, 1, sampleRate, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip BuildIndoorFootstepClip(int sampleRate)
        {
            int n = Mathf.RoundToInt(sampleRate * 0.068f);
            float[] data = new float[n];
            System.Random rng = new System.Random(9091);
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float clickEnv = Mathf.Exp(-t * 22f);
                float tailEnv = Mathf.Exp(-t * 9f) * 0.22f;
                ph += (2.0 * Math.PI * Mathf.Lerp(620f, 260f, t)) / sampleRate;
                float heel = (float)Math.Sin(ph) * clickEnv * 0.58f;
                float crisp = (float)(rng.NextDouble() * 2.0 - 1.0) * (clickEnv * 0.52f + tailEnv);
                data[i] = Mathf.Clamp(heel + crisp, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("Footstep_Indoor", n, 1, sampleRate, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip BuildWetFootstepClip(int sampleRate)
        {
            int n = Mathf.RoundToInt(sampleRate * 0.115f);
            float[] data = new float[n];
            System.Random rng = new System.Random(9109);
            float lp1 = 0f, lp2 = 0f;
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.14f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 6.2f);
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += 0.36f * (w - lp1);
                lp2 += 0.11f * (lp1 - lp2);
                ph += (2.0 * Math.PI * Mathf.Lerp(420f, 160f, t)) / sampleRate;
                float squelch = (lp1 - lp2 * 0.6f) * 0.92f + (float)Math.Sin(ph) * Mathf.Exp(-t * 16f) * 0.32f;
                data[i] = Mathf.Clamp(squelch * env, -1f, 1f);
            }
            AudioClip c = AudioClip.Create("Footstep_Wet", n, 1, sampleRate, false);
            c.SetData(data, 0);
            return c;
        }

        private void SampleGroundSurface()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.25f, Vector3.down, out RaycastHit hit, 0.72f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider != null && hit.collider.transform.root != transform.root)
            {
                string n = hit.collider.gameObject.name.ToLowerInvariant();
                if (n.Contains("sand") || n.Contains("beach") || n.Contains("volley") || n.Contains("shore"))
                {
                    _groundSurface = GroundSurfaceKind.Sand;
                    return;
                }
                if (n.Contains("turf") || n.Contains("grass") || n.Contains("lawn") || n.Contains("meadow") ||
                    n.Contains("pitch") || n.Contains("field") || n.Contains("park") || n.Contains("farm") || n.Contains("soil"))
                {
                    _groundSurface = GroundSurfaceKind.Grass;
                    return;
                }
                if (n.Contains("wood") || n.Contains("plank") || n.Contains("pier") || n.Contains("deck") ||
                    n.Contains("porch") || n.Contains("dock") || n.Contains("bench") || n.Contains("bleacher"))
                {
                    _groundSurface = GroundSurfaceKind.Wood;
                    return;
                }
                if (n.Contains("tile") || n.Contains("marble") || n.Contains("mall") || n.Contains("carpet") ||
                    n.Contains("floor") || n.Contains("interior") || n.Contains("shop") || n.Contains("house"))
                {
                    _groundSurface = GroundSurfaceKind.Indoor;
                    return;
                }
            }
            _groundSurface = GroundSurfaceKind.Stone;
        }

        private static Color GetSurfaceDustTint(GroundSurfaceKind surface)
        {
            switch (surface)
            {
                case GroundSurfaceKind.Grass: return new Color(0.56f, 0.64f, 0.44f, 1f);
                case GroundSurfaceKind.Sand: return new Color(0.92f, 0.84f, 0.64f, 1f);
                case GroundSurfaceKind.Wood: return new Color(0.76f, 0.66f, 0.54f, 1f);
                case GroundSurfaceKind.Indoor: return new Color(0.88f, 0.89f, 0.92f, 1f);
                default: return new Color(0.84f, 0.80f, 0.72f, 1f);
            }
        }

        private void HandleFootstepContact(float runBlend)
        {
            if (_footstepAudio == null || _sharedFootstepClip == null || !_controller.enabled)
            {
                return;
            }

            SampleGroundSurface();
            bool inPuddle = _groundSurface != GroundSurfaceKind.Indoor && IstanbulThemeOverhaul.IsPositionInPuddle(transform.position);
            float roadWet = NeighborhoodWeatherSystem.Instance != null ? NeighborhoodWeatherSystem.Instance.Wetness01 : 0f;
            bool wetGround = inPuddle || (_groundSurface != GroundSurfaceKind.Indoor && (_rainWetness01 > 0.35f || roadWet > 0.52f));

            if (inPuddle || _rainWetness01 > 0.55f)
            {
                _wetShoeStepsRemaining = Mathf.Max(_wetShoeStepsRemaining, inPuddle ? 9 : 6);
            }

            AudioClip clip = _sharedFootstepClip;
            float pitchBase = 1.0f;
            float volMult = 1.0f;
            if (inPuddle && _sharedFootstepWetClip != null)
            {
                clip = _sharedFootstepWetClip;
                pitchBase = 1.02f;
                volMult = 1.18f;
            }
            else if (wetGround && _sharedFootstepWetClip != null)
            {
                clip = _sharedFootstepWetClip;
                pitchBase = 0.94f;
                volMult = 0.92f;
            }
            else
            {
                switch (_groundSurface)
                {
                    case GroundSurfaceKind.Grass:
                        clip = _sharedFootstepGrassClip ?? _sharedFootstepClip;
                        pitchBase = 0.98f;
                        volMult = 0.86f;
                        break;
                    case GroundSurfaceKind.Sand:
                        clip = _sharedFootstepSandClip ?? _sharedFootstepClip;
                        pitchBase = 1.00f;
                        volMult = 0.92f;
                        break;
                    case GroundSurfaceKind.Wood:
                        clip = _sharedFootstepWoodClip ?? _sharedFootstepClip;
                        pitchBase = 0.98f;
                        volMult = 1.08f;
                        break;
                    case GroundSurfaceKind.Indoor:
                        clip = _sharedFootstepIndoorClip ?? _sharedFootstepClip;
                        pitchBase = 1.02f;
                        volMult = 0.84f;
                        break;
                }
            }

            float vol = Mathf.Lerp(0.11f, 0.26f, Mathf.Clamp01(runBlend)) * volMult * (_isCrouching ? 0.35f : 1.0f);
            _footstepAudio.pitch = pitchBase * UnityEngine.Random.Range(0.92f, 1.08f);
            _footstepAudio.PlayOneShot(clip, vol);

            if (!_isSwimming && _controller.isGrounded)
            {
                _nextFootprintRight = !_nextFootprintRight;
                if (inPuddle || (wetGround && runBlend > 0.22f))
                {
                    VisualEffectsManager.SpawnWaterSplash(transform.position + Vector3.up * 0.04f, bigDive: false);
                }
                else if (runBlend > 0.50f && _groundSurface != GroundSurfaceKind.Indoor)
                {
                    VisualEffectsManager.SpawnFootDust(transform.position, heavyLanding: false, tintOverride: GetSurfaceDustTint(_groundSurface));
                }

                // Leave crisp fading footprints on sand or when shoes are wet on dry pavements / marble floors!
                if (_groundSurface == GroundSurfaceKind.Sand)
                {
                    WorldRealismAtmosphere.SpawnFootprint(transform.position, transform.forward, _nextFootprintRight, isWet: false, alpha: 0.42f);
                }
                else if (_wetShoeStepsRemaining > 0 && !inPuddle)
                {
                    float wetAlpha = Mathf.Lerp(0.14f, 0.46f, _wetShoeStepsRemaining / 9f);
                    WorldRealismAtmosphere.SpawnFootprint(transform.position, transform.forward, _nextFootprintRight, isWet: true, alpha: wetAlpha);
                    _wetShoeStepsRemaining--;
                }
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit == null || hit.collider == null || _isSwimming || _isVaulting) return;

            // 1. Physical CharacterController -> Dynamic Rigidbody pushing (crates, trash cans, cones, barrels, tyres, damacanas, footballs)
            Rigidbody body = hit.collider.attachedRigidbody;
            if (body != null && !body.isKinematic && hit.moveDirection.y > -0.35f)
            {
                Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
                if (pushDir.sqrMagnitude > 0.001f)
                {
                    pushDir.Normalize();
                    // Never shove parked cars (mass > 150 kg) on foot — only realistic movable props!
                    if (body.mass <= 120f)
                    {
                        float pushSpd = Mathf.Clamp(_currentSpeed * 0.92f, 0.85f, 4.6f);
                        float massFactor = Mathf.Clamp(22f / Mathf.Max(4f, body.mass), 0.35f, 1.35f);
                        body.WakeUp();
                        body.AddForceAtPosition(pushDir * (pushSpd * massFactor * body.mass * 0.36f), hit.point, ForceMode.Impulse);
                    }
                }
            }

            // 2. If hitting a kinematic-until-touched DynamicWorldProp, wake & nudge it realistically!
            if (_currentSpeed >= 1.4f)
            {
                DynamicWorldProp dynProp = hit.collider.GetComponentInParent<DynamicWorldProp>();
                if (dynProp != null && dynProp.Body != null && dynProp.Body.isKinematic)
                {
                    Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
                    if (pushDir.sqrMagnitude > 0.001f)
                    {
                        dynProp.ApplyNudge(pushDir.normalized * Mathf.Clamp(_currentSpeed, 1.5f, 4.5f), hit.point);
                    }
                }
            }

            if (_currentSpeed < 2.85f) return;

            TownPedestrian ped = hit.collider.GetComponentInParent<TownPedestrian>();
            if (ped != null)
            {
                ped.OnBumpedByPlayer(transform.position, _planarVelocity);
                return;
            }

            LivingNpcColliderBridge livingNpc = hit.collider.GetComponentInParent<LivingNpcColliderBridge>();
            if (livingNpc != null)
            {
                livingNpc.NotifyBumpedByPlayer(transform.position, _planarVelocity);
                return;
            }

            MallShopperColliderBridge shopper = hit.collider.GetComponentInParent<MallShopperColliderBridge>();
            if (shopper != null)
            {
                shopper.NotifyBumpedByPlayer(transform.position, _planarVelocity);
            }
        }

        private void HandleLanded()
        {
            if (_footstepAudio == null || _sharedFootstepClip == null || !_controller.enabled)
            {
                return;
            }

            SampleGroundSurface();
            float impact = humanoid != null ? humanoid.LastLandImpact01 : 0.35f;
            float surfacePitch = _groundSurface == GroundSurfaceKind.Grass ? 0.86f
                : (_groundSurface == GroundSurfaceKind.Sand ? 0.80f
                : (_groundSurface == GroundSurfaceKind.Indoor ? 1.06f : 1.0f));
            _footstepAudio.pitch = Mathf.Lerp(0.92f, 0.76f, impact) * surfacePitch;
            _footstepAudio.PlayOneShot(_sharedFootstepClip, Mathf.Lerp(0.24f, 0.44f, impact));

            if (_cameraRig != null)
            {
                _cameraRig.AddLandingDip(impact);
                if (impact >= 0.65f)
                {
                    _cameraRig.AddShake(Mathf.Lerp(0.05f, 0.14f, (impact - 0.65f) / 0.35f), 0.13f);
                }
            }

            if (!_isSwimming)
            {
                VisualEffectsManager.SpawnFootDust(transform.position, heavyLanding: true, tintOverride: GetSurfaceDustTint(_groundSurface));
            }
        }

        public void SetMovementEnabled(bool enabledState)
        {
            _movementEnabled = enabledState;
            if (!enabledState)
            {
                _currentSpeed = 0f;
                _planarVelocity = Vector3.zero;
                _jumpBufferTimer = 0f;
                if (_cameraRig != null) _cameraRig.SetSprintMotion(0f);
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
                _planarVelocity = Vector3.zero;
                _coyoteTimer = 0f;
                _jumpBufferTimer = 0f;
                if (_cameraRig != null) _cameraRig.SetSprintMotion(0f);
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
            _planarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;

            if (seated)
            {
                _movementEnabled = false;
                _controller.enabled = false;
                enabled = false;
                if (_cameraRig != null) _cameraRig.SetSprintMotion(0f);
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

        /// <summary>
        /// Applies realistic physical recoil, knockback stumble, impact SFX, and camera punch
        /// when an NPC hits the player or the player suffers heavy impact / knockout.
        /// </summary>
        public void ApplyCombatHitImpact(Vector3 knockbackDir, bool heavyHit, bool knockout = false)
        {
            Vector3 flat = knockbackDir;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = -transform.forward;
            else flat.Normalize();

            _hitStumbleVelocity = flat * (knockout ? 6.2f : (heavyHit ? 5.2f : 3.6f));
            _combatLungeVelocity = Vector3.zero;

            if (humanoid != null)
            {
                if (knockout) humanoid.PlayKnockdown(2.8f);
                else humanoid.PlayHitReact();
            }
            TryTriggerMixamoAnimator(HitReactTrigHash);

            if (_footstepAudio != null)
            {
                AudioClip clip = (heavyHit || knockout) ? _sharedHeavyHitClip : _sharedPunchHitClip;
                if (clip != null)
                {
                    _footstepAudio.pitch = knockout ? 0.78f : (heavyHit ? 0.86f : UnityEngine.Random.Range(0.92f, 1.06f));
                    _footstepAudio.PlayOneShot(clip, knockout ? 0.78f : (heavyHit ? 0.68f : 0.54f));
                }
            }

            if (_cameraRig != null)
            {
                _cameraRig.AddShake(knockout ? 0.34f : (heavyHit ? 0.24f : 0.14f), knockout ? 0.36f : (heavyHit ? 0.22f : 0.14f));
            }

            VisualEffectsManager.SpawnCombatImpact(
                transform.position + Vector3.up * 1.25f,
                flat,
                heavyHit || knockout,
                new Color(0.98f, 0.18f, 0.22f));
            DayNightCycle.Instance?.PulseImpactChromatic(knockout ? 0.38f : (heavyHit ? 0.26f : 0.14f));
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            // Prevent unpaid Try-On garments or shelf items from ever leaking out of a store via long-distance teleport!
            if (PlayerShoppingCart.HasActiveItems &&
                !StoreAntiTheftSystem.IsCatchingActive &&
                (position - transform.position).sqrMagnitude > 14f * 14f)
            {
                PlayerShoppingCart.ClearAndRevertOnTeleport();
            }

            if (_controller == null) _controller = GetComponent<CharacterController>();
            // Cancel any in-flight parkour vault so TeleportTo never leaves CharacterController disabled!
            bool wasVaulting = _isVaulting;
            if (_vaultRoutine != null)
            {
                StopCoroutine(_vaultRoutine);
                _vaultRoutine = null;
            }
            _isVaulting = false;
            bool wasEnabled = wasVaulting ? enabled : _controller.enabled;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _verticalVelocity = 0f;
            _currentSpeed = 0f;
            _planarVelocity = Vector3.zero;
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            _combatLungeVelocity = Vector3.zero;
            _hitStumbleVelocity = Vector3.zero;
            _wasGrounded = true;
            _airPeakY = position.y;
            _moveDirection = rotation * Vector3.forward;
            _lastGroundCheckY = position.y;
            _hasLastGroundY = true;
            _groundSampleHoldTimer = 0f;
            _controller.enabled = wasEnabled;
            if (_cameraRig != null) _cameraRig.SetSprintMotion(0f);
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
                    Mathf.Clamp(transform.position.x, -416f, 416f),
                    0.25f,
                    Mathf.Clamp(transform.position.z, -416f, 416f));
                TeleportTo(safePos, transform.rotation);
                return;
            }

            if (_cameraRig == null && Time.time >= _nextCamRigLookupTime)
            {
                _nextCamRigLookupTime = Time.time + 1f;
                _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            }
            if (_cameraRig != null)
            {
                _cameraRig.SetCrouchEyeMode(_isCrouching || _sprintSlideTimer > 0f);
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
            if (_dodgeCooldown > 0f) _dodgeCooldown -= dt;

            // Active Vehicle Bail-Out Tumble
            if (_bailOutTimer > 0f)
            {
                _bailOutTimer -= dt;
                _verticalVelocity += gravity * dt;
                if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
                Vector3 bailMove = _hitStumbleVelocity + Vector3.up * _verticalVelocity;
                _hitStumbleVelocity = Vector3.MoveTowards(_hitStumbleVelocity, Vector3.zero, 8.8f * dt);
                _controller.Move(bailMove * dt);
                humanoid?.SetLocomotion(0f, _controller.isGrounded, _verticalVelocity);
                return;
            }

            // Active GTA 6 Combat Dodge Roll (directional invulnerable roll!)
            if (_dodgeRollTimer > 0f)
            {
                _dodgeRollTimer -= dt;
                _verticalVelocity += gravity * dt;
                if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
                float rollSpd = Mathf.Lerp(4.2f, 7.6f, Mathf.Clamp01(_dodgeRollTimer / 0.48f));
                _controller.Move((_dodgeRollDir * rollSpd + Vector3.up * _verticalVelocity) * dt);
                humanoid?.SetLocomotion(0f, _controller.isGrounded, _verticalVelocity);
                return;
            }

            // Active GTA 6 Sprint-Slide (high momentum slide -> smoothly enters stealth crouch!)
            if (_sprintSlideTimer > 0f)
            {
                _sprintSlideTimer -= dt;
                _verticalVelocity += gravity * dt;
                if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
                float slideSpd = Mathf.Lerp(2.4f, 7.8f, Mathf.Clamp01(_sprintSlideTimer / 0.56f));
                _controller.Move((_sprintSlideDir * slideSpd + Vector3.up * _verticalVelocity) * dt);
                humanoid?.SetLocomotion(0f, _controller.isGrounded, _verticalVelocity);
                if (_sprintSlideTimer <= 0f)
                {
                    humanoid?.SetSprintSlide(false);
                }
                return;
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
                if (_cameraRig != null && _cameraRig.IsFirstPerson && !autoApproach)
                {
                    Quaternion fpFace = Quaternion.Euler(0f, _cameraRig.Yaw, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, fpFace, 1f - Mathf.Exp(-18f * dt));
                }
                else
                {
                    Quaternion targetRot = Quaternion.LookRotation(_moveDirection, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-(_isSwimming ? 7.5f : rotationSpeed) * dt));
                }
            }
            else if (_cameraRig != null && _cameraRig.IsFirstPerson && _movementEnabled && !autoApproach)
            {
                Quaternion fpFace = Quaternion.Euler(0f, _cameraRig.Yaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, fpFace, 1f - Mathf.Exp(-18f * dt));
            }

            // Check swimmable water zones (e.g. Çınarlı Açık Yüzme Havuzu)
            bool inWaterZone = WaterZone.TrySampleWater(transform.position, out float surfaceY, out float floorY);
            bool shouldSwim = inWaterZone && (surfaceY - transform.position.y) >= (_isSwimming ? 0.45f : 0.72f);

            if (shouldSwim && !_isSwimming)
            {
                CarryableProp.DropHeldPropIfAny();
                _isSwimming = true;
                _wasGrounded = true;
                _airPeakY = transform.position.y;
                _hitStumbleVelocity = Vector3.zero;
                _waterSurfaceY = surfaceY;
                _verticalVelocity *= 0.25f;
                if (_footstepAudio != null && _sharedSplashClip != null)
                {
                    _footstepAudio.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
                    _footstepAudio.PlayOneShot(_sharedSplashClip, 0.48f);
                }
                VisualEffectsManager.SpawnWaterSplash(new Vector3(transform.position.x, surfaceY, transform.position.z), bigDive: true);
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
                _rainWetness01 = 1f;
                _wetShoeStepsRemaining = 10;
                _coyoteTimer = 0f;
                _jumpBufferTimer = 0f;
                _planarVelocity = Vector3.zero;
                if (_cameraRig != null) _cameraRig.SetSprintMotion(0f);
                // Holding Jump dives underwater (stroke toggle is handled cleanly via HandleAttackPressed)
                bool diveHeld = InputHub.Instance != null && InputHub.Instance.JumpHeld;

                _isUnderwater = diveHeld && (surfaceY - floorY) >= 1.15f;
                if (_cameraRig != null) _cameraRig.SetFootStrideMotion(0f, _isUnderwater);
                if (_isUnderwater)
                {
                    _swimOxygen01 = Mathf.Max(0f, _swimOxygen01 - dt * 0.11f); // ~9 seconds underwater breath
                    _bubbleTimer -= dt;
                    if (_bubbleTimer <= 0f)
                    {
                        _bubbleTimer = _swimOxygen01 <= 0.15f ? 0.16f : 0.28f;
                        GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.95f, new Color(0.76f, 0.96f, 1.0f), count: 3, spread: 0.18f);
                    }

                    // Real-world drowning risk if staying submerged after running out of air!
                    if (_swimOxygen01 <= 0.001f)
                    {
                        _drownTickTimer -= dt;
                        if (_drownTickTimer <= 0f)
                        {
                            _drownTickTimer = 1.35f;
                            RealWorldDetailsSystem.PlayDrownGasp();
                            PlayerNeedsController.Instance?.TakeEnvironmentalDamage(
                                7f,
                                transform.position,
                                "🫁 Nefesin tükendi! Hemen yüzeye çık!",
                                "🫁 You're out of breath! Surface immediately!",
                                heavy: false);
                        }
                    }
                    else
                    {
                        _drownTickTimer = 0.25f;
                    }
                }
                else
                {
                    _swimOxygen01 = Mathf.Min(1f, _swimOxygen01 + dt * 0.38f);
                    _drownTickTimer = 0f;
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

                if (!_isUnderwater && _currentSpeed > 0.55f)
                {
                    _swimSplashTimer -= dt;
                    if (_swimSplashTimer <= 0f)
                    {
                        _swimSplashTimer = 0.42f;
                        VisualEffectsManager.SpawnWaterSplash(new Vector3(transform.position.x, surfaceY, transform.position.z), bigDive: false);
                    }
                }

                if (humanoid != null)
                {
                    humanoid.SetLookAtPoint(false, Vector3.zero);
                    humanoid.SetSwimming(true, _currentSpeed, _swimBreaststroke);
                }
                return;
            }

            if (_pivotDustCooldown > 0f) _pivotDustCooldown -= dt;
            if (_jumpBufferTimer > 0f) _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - dt);

            bool groundedNow = _controller.isGrounded;
            if (groundedNow && _verticalVelocity <= 0.1f)
            {
                _coyoteTimer = 0.14f;
                if (!_wasGrounded)
                {
                    _wasGrounded = true;
                    float dropHeight = _airPeakY - transform.position.y;
                    if (dropHeight >= 5.2f && !WaterZone.TrySampleWater(transform.position, out _, out _))
                    {
                        float fallDmg = Mathf.Clamp((dropHeight - 4.2f) * 12f, 14f, 100f);
                        bool en = LocalizationService.IsEnglish;
                        PlayerNeedsController.Instance?.TakeDamage(
                            fallDmg,
                            transform.position,
                            en ? "Hard Fall" : "Yüksekten Düşme",
                            heavyHit: dropHeight >= 8.0f);
                    }
                }
                _airPeakY = transform.position.y;
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }
            }
            else
            {
                if (_coyoteTimer > 0f) _coyoteTimer = Mathf.Max(0f, _coyoteTimer - dt);
                if (_wasGrounded)
                {
                    _wasGrounded = false;
                    _airPeakY = transform.position.y;
                }
                else
                {
                    _airPeakY = Mathf.Max(_airPeakY, transform.position.y);
                }
                // Variable jump height: releasing Jump early while rising gives a crisp short hop
                float extraGravity = (_verticalVelocity > 1.8f && InputHub.Instance != null && !InputHub.Instance.JumpHeld)
                    ? gravity * 0.85f
                    : 0f;
                _verticalVelocity += (gravity + extraGravity) * dt;
            }

            // Consume buffered Jump / Parkour Vault if grounded or inside Coyote Time window
            if (_jumpBufferTimer > 0f && (groundedNow || _coyoteTimer > 0f))
            {
                if (TryConsumeBufferedJumpOrVault() && _isVaulting)
                {
                    return;
                }
            }

            // Track outdoor rain wetness (dries smoothly indoors or once the sun comes back out)
            bool exposedToRain = NeighborhoodWeatherSystem.Instance != null &&
                                 NeighborhoodWeatherSystem.Instance.CurrentWeather == NeighborhoodWeatherState.SummerShower &&
                                 !NeighborhoodWeatherSystem.IsPositionSheltered(transform.position);
            if (exposedToRain)
            {
                _rainWetness01 = Mathf.Min(1f, _rainWetness01 + dt * 0.075f);
                if (_rainWetness01 >= 0.78f && !_warnedRainSoaked)
                {
                    _warnedRainSoaked = true;
                    bool enWet = LocalizationService.IsEnglish;
                    PlayerModeController.Instance?.RaiseStatusMessage(enWet
                        ? "🌧️ You're soaked from the Istanbul rain — step indoors or under shelter to dry off!"
                        : "🌧️ Yağmurda iyice ıslandın — kurulanmak için bir çatı altına ya da eve geç!");
                }
            }
            else
            {
                _rainWetness01 = Mathf.Max(0f, _rainWetness01 - dt * 0.12f);
                if (_rainWetness01 < 0.20f) _warnedRainSoaked = false;
            }

            bool isTired = PlayerNeedsController.Instance != null && PlayerNeedsController.Instance.IsTired;
            bool isStarving = PlayerNeedsController.Instance != null && PlayerNeedsController.Instance.IsStarving;
            bool hasShoes = SaveManager.Instance != null && SaveManager.Instance.Data != null && SaveManager.Instance.Data.hasRunningShoes;
            float baseRun = hasShoes ? (runSpeed * 1.22f) : runSpeed;
            float fatigueMult = (isTired ? 0.75f : 1f) * (isStarving ? 0.80f : 1f) * (_rainWetness01 > 0.75f ? 0.92f : 1f);
            float effectiveRunSpeed = baseRun * fatigueMult;
            // Sprinting automatically stands you up out of Stealth Crouch
            if (_isCrouching && sprint && inputMag > 0.25f)
            {
                SetCrouching(false);
            }
            float baseMoveTarget = _isCrouching ? 2.15f : (sprint ? effectiveRunSpeed : walkSpeed);
            float targetSpeed = autoApproach ? pickupSpeed : baseMoveTarget * inputMag;
            Vector3 targetPlanarVel = desiredDir.sqrMagnitude > 0.0001f ? desiredDir * targetSpeed : Vector3.zero;

            // Real 2D planar momentum (eliminates 180-deg reverse skating while feeling super snappy & athletic!)
            float curPlanarSpeed = _planarVelocity.magnitude;
            float moveRate;
            if (groundedNow)
            {
                bool hardCut = curPlanarSpeed > 2.85f && targetPlanarVel.sqrMagnitude > 0.5f &&
                               Vector3.Dot(_planarVelocity / curPlanarSpeed, desiredDir) < -0.25f;
                if (hardCut)
                {
                    moveRate = 24f;
                    if (_pivotDustCooldown <= 0f)
                    {
                        _pivotDustCooldown = 0.42f;
                        SampleGroundSurface();
                        VisualEffectsManager.SpawnFootDust(transform.position, heavyLanding: false, tintOverride: GetSurfaceDustTint(_groundSurface));
                        if (_footstepAudio != null && _sharedFootstepClip != null)
                        {
                            _footstepAudio.pitch = UnityEngine.Random.Range(1.12f, 1.24f);
                            _footstepAudio.PlayOneShot(_sharedFootstepClip, 0.16f);
                        }
                    }
                }
                else if (targetPlanarVel.sqrMagnitude > _planarVelocity.sqrMagnitude)
                {
                    moveRate = Mathf.Max(acceleration, 13.5f);
                }
                else
                {
                    moveRate = Mathf.Max(deceleration, 15.5f);
                }
            }
            else
            {
                moveRate = targetPlanarVel.sqrMagnitude > 0.01f ? 6.5f : 2.2f;
            }

            _planarVelocity = Vector3.MoveTowards(_planarVelocity, targetPlanarVel, moveRate * dt);
            _currentSpeed = _planarVelocity.magnitude;
            if (_currentSpeed > 0.05f)
            {
                _moveDirection = _planarVelocity / _currentSpeed;
            }

            Vector3 velocity = _planarVelocity + _combatLungeVelocity + _hitStumbleVelocity + Vector3.up * _verticalVelocity;
            _combatLungeVelocity = Vector3.MoveTowards(_combatLungeVelocity, Vector3.zero, 16f * dt);
            _hitStumbleVelocity = Vector3.MoveTowards(_hitStumbleVelocity, Vector3.zero, 14f * dt);
            Vector3 start = transform.position;
            _controller.Move(velocity * dt);
            Vector3 travel = Vector3.ProjectOnPlane(transform.position - start, Vector3.up);
            float actualSpeed = dt > 0f ? travel.magnitude / dt : 0f;

            // Keep planar velocity aligned with wall slide collisions so running into walls doesn't build phantom speed
            if (dt > 0.0001f && _planarVelocity.sqrMagnitude > 0.04f && actualSpeed < _currentSpeed * 0.45f)
            {
                _planarVelocity = travel / dt;
                _currentSpeed = actualSpeed;
            }

            if (_cameraRig != null)
            {
                float sprintMotion = (_controller.isGrounded && sprint && !autoApproach)
                    ? Mathf.InverseLerp(walkSpeed + 0.15f, Mathf.Max(walkSpeed + 0.5f, effectiveRunSpeed), actualSpeed)
                    : 0f;
                _cameraRig.SetSprintMotion(sprintMotion);
                _cameraRig.SetFootStrideMotion(_controller.isGrounded && !autoApproach ? actualSpeed : 0f, _isUnderwater);
            }

            if (characterAnimator != null)
            {
                characterAnimator.SetFloat(SpeedHash, actualSpeed, 0.08f, dt);
                characterAnimator.SetBool(GroundedHash, _controller.isGrounded);
                TrySetMixamoAnimatorBool(CrouchingHash, _isCrouching);
                TrySetMixamoAnimatorBool(SwimmingHash, _isSwimming);
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
