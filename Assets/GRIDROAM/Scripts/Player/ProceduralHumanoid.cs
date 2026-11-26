using System;
using OurWorld.World;
using UnityEngine;

namespace OurWorld.Player
{
    public enum SportAnimKind
    {
        None = 0,
        SoccerKick = 1,
        SoccerSteal = 2,
        BasketballJumpShot = 3,
        BasketballPass = 4,
        VolleyballBump = 5,
        VolleyballSpike = 6,
        VictoryJump = 7,
        SoccerSlideTackle = 8,
        SoccerFeint = 9,
        GoalkeeperDive = 10,
        VolleyballBlock = 11,
        VolleyballDiveSave = 12,
        VolleyballTip = 13,
        SwimDiveStart = 14,
        SwimFlipTurn = 15
    }

    /// <summary>
    /// Jointed primitive humanoid character with smooth procedural animation:
    /// idle breathing/weight-shift/head look, head-look IK-lite toward nearby interactables (max 60° yaw),
    /// distance-driven walk/run cycle with footstep contact events, jump landing squat (knees 35° for 0.15 s),
    /// turning lean, airborne tuck, one-shot right-arm wave, startled arms-up pose, chair/bench sitting,
    /// and seated driving pose.
    /// </summary>
    public partial class ProceduralHumanoid : MonoBehaviour
    {
        [Header("Skeleton Pivots")]
        [SerializeField] private Transform hips;
        [SerializeField] private Transform spine;
        [SerializeField] private Transform neck;
        [SerializeField] private Transform head;
        [SerializeField] private Transform lShoulder;
        [SerializeField] private Transform lElbow;
        [SerializeField] private Transform rShoulder;
        [SerializeField] private Transform rElbow;
        [SerializeField] private Transform lHip;
        [SerializeField] private Transform lKnee;
        [SerializeField] private Transform lAnkle;
        [SerializeField] private Transform rHip;
        [SerializeField] private Transform rKnee;
        [SerializeField] private Transform rAnkle;

        [Header("Visual Helpers")]
        [SerializeField] private Transform chestVisual;
        [SerializeField] private float hipsBaseY = 0.95f;
        [SerializeField] private bool autoRandomWave;
        [SerializeField] private bool isSitting;
        [SerializeField] private bool isDrivingSeat;

        public Transform HeadPivot => head;

        private const float WalkSpeedRef = 2.25f;
        private const float RunSpeedRef = 4.25f;
        private const float WaveDuration = 1.5f;
        private const float LandSquatDuration = 0.28f;
        private const float HandLength = 0.30f;
        private const float AnkleHeight = 0.081f;
        private const float TwoPi = 2f * Mathf.PI;

        private float _speedMs;
        private Vector3 _travelDirection = Vector3.forward;
        private Vector3 _smoothedTravelDir = Vector3.forward;
        private bool _sampleGround;
        private float _gaitWeight;
        private bool _grounded = true;
        private bool _wasGrounded = true;
        private float _airborneTimer;
        private float _peakFallSpeed;
        private float _landSquatTimer = -1f;
        private float _landSquatDuration = LandSquatDuration;
        private float _landImpact01;
        private float _verticalVelocity;
        private float _steerAngleDeg;
        private float _smoothedDriveSteerNorm;
        private float _driveThrottleInput;
        private float _driveLongitudinalAccel;
        private Transform _steeringWheel;
        private float? _seatFloor;
        private float _seatAmount;
        private bool _autoSeatActive;
        private float _autoSeatFrom;
        private float _autoSeatTo;
        private float _autoSeatElapsed;
        private float _autoSeatDuration = 0.85f;
        private int _seatGestureKind;
        private float _seatGestureTimer = -1f;
        private float _seatGestureDuration;
        private float _seatGestureCooldown = -1f;
        private float _seatedCheerTimer = -1f;
        private float _seatedCheerDuration = 1.4f;
        private Transform _doorHandle;
        private float _doorReachStarted;
        private float _doorReachDuration;
        private float _doorReachTime = 0.16f;
        private bool _doorReachRight;
        private bool _cycling;
        private bool _motorcyclePose;
        private float _cycleMount = 1f;
        private float _cycleFootPeg = 1f;
        private float _cycleSteerNorm;
        private float _cycleAccelLean;
        private float _cycleSmoothedSteer;
        private float _cycleSmoothedAccel;
        private Vector3 _leftPedal, _rightPedal, _leftGrip, _rightGrip;
        private Transform _tennisRacket;
        private Vector3 _tennisContact;
        private float _tennisContactAt = float.NegativeInfinity;
        private bool _tennisServing;
        private bool _tennisBackhand;
        private Vector3? _tennisLeftHand;
        private const float TennisWindup = 0.24f;
        private const float TennisFollowThrough = 0.34f;
        private const float RacketHandleCenter = 0.09f;
        private const float RacketHeadFromHand = 0.39f;

        // Dedicated full-body sports action animations (Soccer, Basketball, Volleyball, Celebrations)
        private SportAnimKind _sportAnimKind = SportAnimKind.None;
        private float _sportAnimTimer = -1f;
        private float _sportAnimDuration = 0.55f;
        private float _sportAnimSide = 1f;

        private float _phase;
        private int _lastStepCount;
        private float _idleTime;
        private float _noiseSeed;
        private float _prevYaw;
        private float _smoothedTurnLean;
        // v13 animation polish: acceleration lean, idle weight shifts, run crouch, fall flail
        private float _prevSpeedMs;
        private float _recentPeakSpeed;
        private float _smoothedAccelLean;
        private float _smoothedHeadTurnLead;
        private float _stopSettleTimer = -1f;
        private float _stopSettleStrength;
        private float _turnInPlaceWeight;
        private float _turnStepPhase;
        private float _turnYawSign = 1f;
        private float _idleShift;
        private float _idleShiftTarget;
        private float _idleShiftCountdown = 7f;
        private int _idleGestureKind; // 0 = none, 1 = hands-on-hips, 2 = neck/shoulder stretch, 3 = check wrist
        private float _idleGestureTimer = -1f;
        private float _idleGestureDuration = 2.8f;
        private float _idleGestureCooldown = 8f;
        private float _talkingTimer;
        private float _talkingBlend;
        private float _waveTimer = -1f;
        private float _punchTimer = -1f;
        private int _combatMoveIndex;
        private float _hitReactTimer = -1f;
        private float _knockdownTimer = -1f;
        private float _vaultTimer = -1f;
        private bool _isCrouching;
        private float _crouchBlend;
        private bool _isSprintSliding;
        private float _slideBlend;
        private float _dodgeRollTimer = -1f;
        private float _dodgeRollDuration = 0.48f;
        private Vector3? _vaultSupport;
        private bool _isSwimming;
        private bool _swimBreaststroke;
        private float _swimSpeed;
        private float _swimPhase;
        private int _prayerStage;
        private float _prayerSelamYaw;
        private bool _prayerHandsInit;
        private float _prayerHandFade;
        private Vector3 _prayerLeftHandLocal;
        private Vector3 _prayerRightHandLocal;
        private bool _isLyingDown;
        private float _lyingBlend;
        private bool _lyingTarget;
        private float _lyingTransition = 1.1f;
        private int _lyingVariant;
        private float _lyingStirTimer = -1f;
        private float _lyingStirCountdown = 12f;
        private float _lyingStirSide = 1f;
        private float _startledTimer = -1f;
        private float _startledDuration = 1.1f;
        private float _startledDodgeSide = 1f;
        private bool _startledHasThreat;
        private float _autoWaveCountdown = 8f;
        private const float PunchDuration = 0.36f;
        private const float KickDuration = 0.44f;
        private const float HitReactDuration = 0.55f;
        private const float KnockdownDuration = 2.65f;
        private const float VaultDuration = 0.42f;
        private Vector3 _chestBaseScale = new Vector3(0.39f, 0.20f, 0.23f);
        private bool _initialized;

        // Head-look IK-lite state
        private bool _hasLookPoint;
        private Vector3 _lookWorldPoint;
        private float _smoothedLookYaw;
        private float _smoothedLookPitch;
        private float _lookWeight;

        // Ground sampling & stair smoothing state
        private Vector3 _prevWorldPos;
        private float _leftGroundYOffset;
        private float _rightGroundYOffset;
        private float _leftRawGroundYOffset;
        private float _rightRawGroundYOffset;
        private Vector3 _leftGroundNormal = Vector3.up;
        private Vector3 _rightGroundNormal = Vector3.up;
        private Vector3 _leftRawGroundNormal = Vector3.up;
        private Vector3 _rightRawGroundNormal = Vector3.up;
        private float _groundCacheTimer;
        private bool _groundSampleValid;
        private float _smoothedPelvisDrop;
        private Vector3 _baseHipsLocalPos;

        // Carry release fade, throw follow-through & load smoothing
        private float _carryLoadBlend;
        private float _carryReleaseTimer = -1f;
        private const float CarryReleaseFadeDuration = 0.24f;
        private Vector3 _carryReleaseLeft;
        private Vector3 _carryReleaseRight;
        private float _carryReleaseCrouch;
        private float _carryThrowTimer = -1f;
        private const float CarryThrowFollowDuration = 0.30f;
        private float _carryThrowHeaviness;
        private Vector3 _carryThrowLeftLocal;
        private Vector3 _carryThrowRightLocal;

        // Petting smooth surface & early-cancel fade state
        private float _petFadeBlend;
        private Vector3 _lastPetHandWorld;
        private Vector3 _prevPetHandSample;
        private bool _hasPrevPetSample;
        private float _petReturnLift;

        // Shop shelf reach & drinking/eating animation state
        private bool _hasShelfReach;
        private Vector3 _shelfReachWorldPos;
        private float _shelfReachWeight;
        private int _consumeMode; // 0 = none, 1 = drink bottle/cup, 2 = eat food/cone
        private float _consumeWeight;
        private float _consumeCyclePhase;

        // Dual-hand interactive action overlay (wash face, abdest, typing, ATM, remote, wardrobe, washer, refuel)
        private bool _hasDualAction;
        private Vector3 _dualLeftTarget;
        private Vector3 _dualRightTarget;
        private float _dualLeftWeight;
        private float _dualRightWeight;
        private float _dualSpineLean;
        private float _dualHeadPitch;
        private float _dualHipsDrop;

        // One-knee kneel overlay (farm work & low interactions)
        private float _kneelWeight;
        private float _kneelBlend;
        private bool _kneelRight = true;
        private bool _activeKneelRight = true;

        // Shooting Range two-handed tactical aiming, recoil kick & reload overlay
        private bool _weaponAimActive;
        private bool _weaponIsLongGun;
        private float _weaponAimBlend;
        private float _weaponLongGunBlend;
        private float _weaponAimPitchDeg;
        private float _weaponRecoilKick;
        private float _weaponReloadPhase;
        private int _weaponActionKind; // 0 = none, 1 = shotgun pump, 2 = sniper bolt
        private float _weaponActionPhase;
        private int _weaponReloadStyle; // 0 = mag swap, 1 = shotgun shell load, 2 = sniper mag+bolt

        public Vector3 WeaponGripWorldPos { get; private set; }
        public Quaternion WeaponGripWorldRot { get; private set; }

        public Transform HeadTransform => head;
        public Transform RightForearm => rElbow;
        public Transform LeftForearm => lElbow;

        public bool IsSitting => isSitting;
        public bool IsSeatTransitionActive => _autoSeatActive;
        public bool IsSeatedOnFloorContact => isSitting && _seatFloor.HasValue && !isDrivingSeat;
        public float KneelWeight => _kneelWeight;
        public float LastLandImpact01 => _landImpact01;
        public bool HasUnevenGroundSample => Mathf.Abs(_leftGroundYOffset) > 0.015f || Mathf.Abs(_rightGroundYOffset) > 0.015f;
        public event Action<float> OnFootstepContact;
        public event Action OnLanded;

        public void SetAutoRandomWave(bool enabled)
        {
            autoRandomWave = enabled;
        }

        public void SetLocomotion(float speedMs, bool grounded, float verticalVelocity,
            Vector3 travelDirection = default, bool sampleGround = false)
        {
            if (_initialized && !_wasGrounded && grounded && _airborneTimer > 0.08f)
            {
                _landImpact01 = Mathf.Clamp01((_peakFallSpeed - 2.5f) / 10f);
                _landSquatDuration = Mathf.Lerp(0.26f, 0.42f, _landImpact01);
                _landSquatTimer = 0f;
                OnLanded?.Invoke();
            }

            _wasGrounded = grounded;
            if (grounded)
            {
                _airborneTimer = 0f;
                _peakFallSpeed = 0f;
            }
            else if (verticalVelocity < 0f)
            {
                _peakFallSpeed = Mathf.Max(_peakFallSpeed, -verticalVelocity);
            }

            _speedMs = Mathf.Max(0f, speedMs);
            _grounded = grounded;
            _verticalVelocity = verticalVelocity;
            _sampleGround = sampleGround;
            if (travelDirection.sqrMagnitude > 0.0001f)
            {
                _travelDirection = travelDirection.normalized;
            }
            else if (_speedMs > 0.05f)
            {
                _travelDirection = transform.forward;
            }
        }

        public void SetLookAtPoint(bool active, Vector3 worldPoint)
        {
            _hasLookPoint = active;
            _lookWorldPoint = worldPoint;
        }

        public void PlayWave()
        {
            CarryableProp carry = CarryableProp.CurrentlyHeld;
            if (carry != null && carry.UsesRig(this)) return;
            if (_consumeWeight > 0.05f || _hasShelfReach || _hasDualAction) return;
            _waveTimer = 0f;
            _idleGestureTimer = -1f;
        }

        /// <summary>
        /// Triggers natural conversational hand gestures, rhythmic head nods & attentive posture
        /// while speaking to the player or another citizen!
        /// </summary>
        public void SetTalking(bool talking, float duration = 3.2f)
        {
            if (talking)
            {
                _talkingTimer = Mathf.Max(_talkingTimer, Mathf.Max(0.4f, duration));
                _idleGestureTimer = -1f;
            }
            else
            {
                _talkingTimer = 0f;
            }
        }

        public void PlayPunch()
        {
            PlayCombatMove(1);
        }

        public void PlayCombatMove(int comboStep)
        {
            _combatMoveIndex = comboStep % 3;
            _punchTimer = 0f;
            _waveTimer = -1f;
        }

        public void PlaySportAnim(SportAnimKind kind, float duration = 0.55f, float sideSign = 1f)
        {
            if (kind == SportAnimKind.None)
            {
                _sportAnimKind = SportAnimKind.None;
                _sportAnimTimer = -1f;
                return;
            }
            _sportAnimKind = kind;
            _sportAnimDuration = Mathf.Max(0.24f, duration);
            _sportAnimTimer = 0f;
            _sportAnimSide = sideSign < 0f ? -1f : 1f;
            _waveTimer = -1f;
        }

        public void PlayHitReact()
        {
            _hitReactTimer = 0f;
            _waveTimer = -1f;
        }

        private float _activeKnockdownDuration = KnockdownDuration;

        public void PlayKnockdown(float duration = KnockdownDuration)
        {
            _activeKnockdownDuration = Mathf.Max(1.2f, duration);
            _knockdownTimer = 0f;
            _hitReactTimer = -1f;
            _waveTimer = -1f;
        }

        public void PlayVault(Vector3? support = null)
        {
            _vaultTimer = 0f;
            _vaultSupport = support;
            _waveTimer = -1f;
        }

        public void SetCrouch(bool crouching)
        {
            _isCrouching = crouching;
        }

        public void SetSprintSlide(bool sliding)
        {
            _isSprintSliding = sliding;
        }

        public void PlayDodgeRoll(float duration = 0.48f)
        {
            _dodgeRollDuration = Mathf.Max(0.32f, duration);
            _dodgeRollTimer = 0f;
            _waveTimer = -1f;
            _punchTimer = -1f;
        }

        public void PlayLanding()
        {
            _landImpact01 = Mathf.Max(_landImpact01, 0.35f);
            _landSquatDuration = Mathf.Lerp(0.26f, 0.40f, _landImpact01);
            _landSquatTimer = 0f;
            _vaultTimer = -1f;
            OnLanded?.Invoke();
        }

        public void SetSwimming(bool swimming, float swimSpeed, bool breaststroke = false)
        {
            _isSwimming = swimming;
            _swimSpeed = Mathf.Max(0f, swimSpeed);
            _swimBreaststroke = breaststroke;
            if (swimming)
            {
                _kneelWeight = 0f;
                _kneelBlend = 0f;
            }
        }

        /// <summary>
        /// Sets an animated Salah (Namaz) prayer stage:
        /// 0 = None, 1 = Kıyam (standing hands folded over abdomen), 2 = Rükû (90° bow, palms on knees),
        /// 3 = Secde (7-point prostration: forehead, palms, knees & toes on carpet),
        /// 4 = Oturuş / Celse / Tahiyyat (kneeling back on heels, palms on thighs; selamHeadYaw ±64° for Selam),
        /// 5 = İftitah Tekbiri (standing, hands raised beside ears),
        /// 6 = Kavme (standing upright from Rükû, arms relaxed at sides).
        /// </summary>
        public void SetPrayerPose(int stage, float selamHeadYaw = 0f)
        {
            if (_prayerStage == 0 && stage > 0)
            {
                _prayerHandsInit = false;
            }
            _prayerStage = Mathf.Clamp(stage, 0, 6);
            _prayerSelamYaw = Mathf.Clamp(selamHeadYaw, -70f, 70f);
            if (_prayerStage > 0)
            {
                _waveTimer = -1f;
                _punchTimer = -1f;
                _startledTimer = -1f;
                _isLyingDown = false;
                _lyingTarget = false;
                _lyingBlend = 0f;
                _lyingStirTimer = -1f;
            }
            else
            {
                _prayerHandsInit = false;
            }
        }

        public void SetLyingDown(bool lying, float transitionSeconds = 1.1f)
        {
            _lyingTransition = transitionSeconds;
            _lyingTarget = lying;

            if (lying)
            {
                if (!_isLyingDown)
                {
                    _lyingVariant = UnityEngine.Random.Range(0, 3);
                    _lyingStirCountdown = UnityEngine.Random.Range(8f, 16f);
                    _lyingStirTimer = -1f;
                    _isLyingDown = true;
                }
                _waveTimer = -1f;
                _punchTimer = -1f;
                _prayerStage = 0;
                _kneelWeight = 0f;
                _kneelBlend = 0f;
            }

            if (transitionSeconds <= 0.01f)
            {
                _lyingBlend = lying ? 1f : 0f;
                if (!lying)
                {
                    _isLyingDown = false;
                    _lyingStirTimer = -1f;
                }
            }
        }

        public void PlayStartled(float duration = 1.1f, Vector3? threatWorldPos = null)
        {
            _startledDuration = Mathf.Max(0.35f, duration);
            _startledTimer = 0f;
            _startledHasThreat = false;

            if (threatWorldPos.HasValue)
            {
                float localX = transform.InverseTransformPoint(threatWorldPos.Value).x;
                _startledDodgeSide = localX >= 0f ? -1f : 1f;
                _startledHasThreat = true;
            }
            else
            {
                PlayerModeController mode = PlayerModeController.Instance;
                if (mode != null && mode.IsDriving)
                {
                    Vector3 carPos = mode.ActiveWorldPosition;
                    Vector3 toCar = carPos - transform.position;
                    toCar.y = 0f;
                    if (toCar.sqrMagnitude <= 18f * 18f)
                    {
                        float localX = transform.InverseTransformPoint(carPos).x;
                        _startledDodgeSide = localX >= 0f ? -1f : 1f;
                        _startledHasThreat = true;
                    }
                }
                if (!_startledHasThreat)
                {
                    _startledDodgeSide = (_noiseSeed % 2f) >= 1f ? 1f : -1f;
                }
            }
        }

        public void SetSitting(bool sitting, bool driving = false)
        {
            isSitting = sitting;
            isDrivingSeat = sitting && driving;
            if (sitting)
            {
                _kneelWeight = 0f;
                _kneelBlend = 0f;
            }
            if (!sitting || driving)
            {
                _seatFloor = null;
                _autoSeatActive = false;
                ResetSeatedGesture();
            }
            if (!Application.isPlaying && sitting)
            {
                ApplySittingPoseImmediate();
            }
        }

        public void SetSittingPose(bool onChair)
        {
            SetSitting(onChair, driving: false);
        }

        public void SetSteeringAngle(float wheelDegrees)
        {
            _steerAngleDeg = wheelDegrees;
        }

        public void SetDrivingDynamics(float throttleOrBrake, float longitudinalAccel)
        {
            _driveThrottleInput = Mathf.Clamp(throttleOrBrake, -1f, 1f);
            _driveLongitudinalAccel = Mathf.Clamp(longitudinalAccel, -12f, 12f);
        }

        public void SetSteeringWheel(Transform wheel)
        {
            _steeringWheel = wheel;
        }

        public void SetSeatContact(float floorHeight, float seatedAmount)
        {
            _seatFloor = floorHeight;
            _seatAmount = seatedAmount;
        }

        /// <summary>
        /// Caller moves the root from the approach point onto the seat point during the same duration (sit down)
        /// or back off it (stand up). When a stand-up transition finishes, SetSitting(false) is called automatically.
        /// </summary>
        public void BeginSeatTransition(float floorHeight, bool sitDown, float duration = 0.85f)
        {
            bool wasAlreadySittingWithContact = isSitting && _seatFloor.HasValue && !isDrivingSeat;
            isSitting = true;
            isDrivingSeat = false;
            _seatFloor = floorHeight;
            _autoSeatFrom = sitDown ? (wasAlreadySittingWithContact ? _seatAmount : 0f) : _seatAmount;
            _autoSeatTo = sitDown ? 1f : 0f;
            _autoSeatElapsed = 0f;
            _autoSeatDuration = Mathf.Max(0.3f, duration);
            _autoSeatActive = true;
            if (sitDown && !wasAlreadySittingWithContact)
            {
                _seatAmount = 0f;
            }
            ResetSeatedGesture();
        }

        public void PlaySeatedCheer(float duration = 1.55f)
        {
            _seatedCheerDuration = Mathf.Max(0.45f, duration);
            _seatedCheerTimer = 0f;
            _seatGestureTimer = -1f;
        }

        private void ResetSeatedGesture()
        {
            _seatGestureKind = 0;
            _seatGestureTimer = -1f;
            _seatGestureCooldown = -1f;
            _seatedCheerTimer = -1f;
        }

        public void PlayDoorReach(Transform handle, float duration, float reachTime = 0.16f)
        {
            if (handle == null) return;
            if (_isLyingDown || isSitting || _cycling || _weaponAimActive || _prayerStage > 0) return;
            if (CarryableProp.CurrentlyHeld != null && CarryableProp.CurrentlyHeld.UsesRig(this)) return;
            _doorHandle = handle;
            _doorReachStarted = Time.time;
            _doorReachDuration = Mathf.Max(0.15f, duration);
            _doorReachTime = Mathf.Clamp(reachTime, 0.06f, _doorReachDuration * 0.65f);
            _doorReachRight = transform.InverseTransformPoint(handle.position).x >= -0.08f;
        }

        internal void BeginCarryReleaseFade(Vector3 leftGripWorld, Vector3 rightGripWorld, float crouchAmount)
        {
            _carryReleaseLeft = leftGripWorld;
            _carryReleaseRight = rightGripWorld;
            _carryReleaseCrouch = Mathf.Clamp01(crouchAmount);
            _carryReleaseTimer = 0f;
            _carryThrowTimer = -1f;
        }

        public void PlayCarryThrowFollowThrough(float heaviness = 0.7f, Vector3? releaseLeftWorld = null, Vector3? releaseRightWorld = null)
        {
            _carryThrowHeaviness = Mathf.Clamp01(heaviness);
            _carryThrowTimer = 0f;
            _carryReleaseTimer = -1f;
            _waveTimer = -1f;
            _carryThrowLeftLocal = releaseLeftWorld.HasValue
                ? transform.InverseTransformPoint(releaseLeftWorld.Value)
                : new Vector3(-0.18f, 1.32f, 0.56f);
            _carryThrowRightLocal = releaseRightWorld.HasValue
                ? transform.InverseTransformPoint(releaseRightWorld.Value)
                : new Vector3(0.18f, 1.32f, 0.56f);
        }

        public void SetWeaponAimPose(
            bool active,
            bool isLongGun,
            float aimPitchDeg,
            float recoilKick01 = 0f,
            float reloadProgress01 = 0f,
            int weaponActionKind = 0,
            float weaponActionPhase01 = 0f,
            int reloadStyle = 0)
        {
            _weaponAimActive = active;
            _weaponIsLongGun = isLongGun;
            _weaponAimPitchDeg = Mathf.Clamp(aimPitchDeg, -45f, 45f);
            _weaponRecoilKick = Mathf.Clamp01(recoilKick01);
            _weaponReloadPhase = Mathf.Clamp01(reloadProgress01);
            _weaponActionKind = active ? weaponActionKind : 0;
            _weaponActionPhase = active ? Mathf.Clamp01(weaponActionPhase01) : 0f;
            _weaponReloadStyle = reloadStyle;
        }

        public void SetDualHandAction(
            bool active,
            Vector3 leftHandWorld,
            Vector3 rightHandWorld,
            float leftWeight,
            float rightWeight,
            float spineLeanDeg = 0f,
            float headPitchDeg = 0f,
            float hipsDrop = 0f)
        {
            _hasDualAction = active;
            _dualLeftTarget = leftHandWorld;
            _dualRightTarget = rightHandWorld;
            _dualLeftWeight = active ? Mathf.Clamp01(leftWeight) : 0f;
            _dualRightWeight = active ? Mathf.Clamp01(rightWeight) : 0f;
            _dualSpineLean = active ? spineLeanDeg : 0f;
            _dualHeadPitch = active ? headPitchDeg : 0f;
            _dualHipsDrop = active ? hipsDrop : 0f;
            if (active)
            {
                _waveTimer = -1f;
            }
        }

        public void SetKneel(float weight, bool rightKneeDown = true)
        {
            _kneelWeight = Mathf.Clamp01(weight);
            _kneelRight = rightKneeDown;
        }

        private void Start()
        {
            InitializeRuntime();
            if (isSitting)
            {
                ApplySittingPoseImmediate();
            }
        }

        private void InitializeRuntime()
        {
            if (_initialized) return;
            _initialized = true;
            _prevYaw = transform.eulerAngles.y;
            _prevWorldPos = transform.position;
            _smoothedTravelDir = transform.forward;
            _baseHipsLocalPos = hips != null ? hips.localPosition : new Vector3(0f, hipsBaseY, 0f);
            _noiseSeed = UnityEngine.Random.Range(0f, 100f);
            _autoWaveCountdown = UnityEngine.Random.Range(4f, 10f);
            if (chestVisual != null)
            {
                _chestBaseScale = chestVisual.localScale;
            }
        }

        private void ApplySittingPoseImmediate()
        {
            if (hips == null) return;

            if (isDrivingSeat)
            {
                hips.localPosition = new Vector3(0f, 0.17f, 0f);
                hips.localRotation = Quaternion.identity;
                if (spine != null) spine.localRotation = Quaternion.Euler(-6f, 0f, 0f);
                if (neck != null) neck.localRotation = Quaternion.Euler(4f, 0f, 0f);
                if (head != null) head.localRotation = Quaternion.identity;

                if (lHip != null) lHip.localRotation = Quaternion.Euler(-90f, 0f, -6f);
                if (rHip != null) rHip.localRotation = Quaternion.Euler(-90f, 0f, 6f);
                if (lKnee != null) lKnee.localRotation = Quaternion.Euler(80f, 0f, 0f);
                if (rKnee != null) rKnee.localRotation = Quaternion.Euler(80f, 0f, 0f);
                if (lAnkle != null) lAnkle.localRotation = Quaternion.Euler(10f, 0f, 0f);
                if (rAnkle != null) rAnkle.localRotation = Quaternion.Euler(10f, 0f, 0f);

                if (lShoulder != null) lShoulder.localRotation = Quaternion.Euler(-62f, 10f, -10f);
                if (rShoulder != null) rShoulder.localRotation = Quaternion.Euler(-62f, -10f, 10f);
                if (lElbow != null) lElbow.localRotation = Quaternion.Euler(-32f, 0f, 0f);
                if (rElbow != null) rElbow.localRotation = Quaternion.Euler(-32f, 0f, 0f);
            }
            else
            {
                hips.localPosition = new Vector3(0f, 0.105f, 0.03f);
                hips.localRotation = Quaternion.identity;
                if (spine != null) spine.localRotation = Quaternion.Euler(-5f, 0f, 0f);
                if (neck != null) neck.localRotation = Quaternion.Euler(4f, 0f, 0f);
                if (head != null) head.localRotation = Quaternion.identity;

                if (lHip != null) lHip.localRotation = Quaternion.Euler(-88f, 0f, -5f);
                if (rHip != null) rHip.localRotation = Quaternion.Euler(-88f, 0f, 5f);
                if (lKnee != null) lKnee.localRotation = Quaternion.Euler(88f, 0f, 0f);
                if (rKnee != null) rKnee.localRotation = Quaternion.Euler(88f, 0f, 0f);
                if (lAnkle != null) lAnkle.localRotation = Quaternion.identity;
                if (rAnkle != null) rAnkle.localRotation = Quaternion.identity;

                if (lShoulder != null) lShoulder.localRotation = Quaternion.Euler(8f, 0f, -10f);
                if (rShoulder != null) rShoulder.localRotation = Quaternion.Euler(8f, 0f, 10f);
                if (lElbow != null) lElbow.localRotation = Quaternion.Euler(-44f, 0f, 10f);
                if (rElbow != null) rElbow.localRotation = Quaternion.Euler(-44f, 0f, -10f);
            }
        }

        private void LateUpdate()
        {
            if (hips == null) return;
            if (!_initialized) InitializeRuntime();

            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            if (_cycling)
            {
                _idleTime += dt;
                AnimateCycling(dt);
                _baseHipsLocalPos = hips.localPosition;
                return;
            }

            TickActionTimers(dt);

            if (isSitting)
            {
                AnimateSitting(dt);
                _baseHipsLocalPos = hips.localPosition;
                return;
            }

            FramePose p = new FramePose
            {
                dt = dt,
                knockdownFootBlend = 1f
            };
            DetectCarryAndPetState(ref p);
            ComputeBaseLocomotionPose(ref p);
            ApplyIdleFidgetTalkAndAirborne(ref p);
            ApplyActivityOverrides(ref p);
            ApplyCarryPetAndSportOverlays(ref p);
            ApplyCoreBoneTransformsAndFootPlanting(ref p);
            ApplyPostTransformHandIk(ref p);
        }

        private Vector3 ShoulderReach(Transform shoulder, Transform elbow, Vector3 target)
        {
            if (shoulder == null || elbow == null) return Vector3.zero;
            Vector3 delta = target - shoulder.position;
            float reach = (elbow.localPosition.magnitude + HandLength - 0.014f) * transform.lossyScale.y;
            return delta.normalized * Mathf.Max(0f, delta.magnitude - reach);
        }

        private void PlantFeet(float strideLength, float stance, float runBlend, bool tennisStance, float idleWeight,
            Vector3 localDirection, float dt, float weight, float startledStepW, float hitStaggerW, bool stationaryGround)
        {
            bool refreshGround = !stationaryGround || !_groundSampleValid;
            if (!refreshGround)
            {
                _groundCacheTimer -= dt;
                if (_groundCacheTimer <= 0f)
                {
                    refreshGround = true;
                    _groundCacheTimer = 0.25f;
                }
            }
            else
            {
                _groundCacheTimer = 0.25f;
            }

            Vector3 left = GaitFoot(Mathf.Repeat(_phase / TwoPi, 1f), -1f, localDirection,
                strideLength, stance, runBlend, tennisStance, idleWeight, dt, refreshGround, out Quaternion leftRotation);
            Vector3 right = GaitFoot(Mathf.Repeat(_phase / TwoPi + 0.5f, 1f), 1f, localDirection,
                strideLength, stance, runBlend, tennisStance, idleWeight, dt, refreshGround, out Quaternion rightRotation);
            if (_sampleGround && refreshGround) _groundSampleValid = true;
            else if (!_sampleGround) _groundSampleValid = false;

            float scaleY = transform.lossyScale.y;

            // Stationary startled vehicle-dodge foot step away from threat side
            if (startledStepW > 0.005f)
            {
                float sNorm = Mathf.Clamp01(_startledTimer / Mathf.Max(0.2f, _startledDuration));
                float hopLift = Mathf.Sin(Mathf.Clamp01(sNorm / 0.22f) * Mathf.PI) * 0.085f * startledStepW * scaleY;
                Vector3 dodgeOffset = (transform.right * (_startledDodgeSide * 0.21f) - transform.forward * 0.09f) * (startledStepW * scaleY) + transform.up * hopLift;
                if (_startledDodgeSide > 0f) right += dodgeOffset;
                else left += dodgeOffset;
            }

            // Grounded hit-react stagger back-step on right foot so legs absorb recoil naturally
            if (hitStaggerW > 0.005f)
            {
                right += (-transform.forward * 0.14f + transform.right * 0.05f) * (hitStaggerW * scaleY);
            }

            if (_kneelBlend > 0.01f)
            {
                float frontSign = _activeKneelRight ? -1f : 1f;
                float kneelSign = -frontSign;

                Vector3 frontGround = SampleKneelGround(new Vector3(frontSign * 0.13f, 0f, 0.30f), out Vector3 frontNormal);
                Vector3 kneelGround = SampleKneelGround(new Vector3(kneelSign * 0.12f, 0f, -0.34f), out Vector3 kneelNormal);

                Vector3 frontTarget = frontGround + frontNormal * (AnkleHeight * scaleY);
                Quaternion frontRot = Quaternion.FromToRotation(transform.up, frontNormal) * transform.rotation;

                const float kneelToePitch = 55f;
                float kneelSoleLift = Mathf.Sin(kneelToePitch * Mathf.Deg2Rad) * 0.19f;
                Vector3 kneelTarget = kneelGround + kneelNormal * ((AnkleHeight + kneelSoleLift) * scaleY);
                Quaternion kneelRot = Quaternion.FromToRotation(transform.up, kneelNormal) *
                    transform.rotation * Quaternion.Euler(kneelToePitch, 0f, 0f);

                if (_activeKneelRight)
                {
                    left = Vector3.Lerp(left, frontTarget, _kneelBlend);
                    leftRotation = Quaternion.Slerp(leftRotation, frontRot, _kneelBlend);
                    right = Vector3.Lerp(right, kneelTarget, _kneelBlend);
                    rightRotation = Quaternion.Slerp(rightRotation, kneelRot, _kneelBlend);
                }
                else
                {
                    right = Vector3.Lerp(right, frontTarget, _kneelBlend);
                    rightRotation = Quaternion.Slerp(rightRotation, frontRot, _kneelBlend);
                    left = Vector3.Lerp(left, kneelTarget, _kneelBlend);
                    leftRotation = Quaternion.Slerp(leftRotation, kneelRot, _kneelBlend);
                }
            }

            Vector3 preHipsPos = hips.position;
            Quaternion preLHip = lHip != null ? lHip.localRotation : Quaternion.identity;
            Quaternion preLKnee = lKnee != null ? lKnee.localRotation : Quaternion.identity;
            Quaternion preLAnkle = lAnkle.localRotation;
            Quaternion preRHip = rHip != null ? rHip.localRotation : Quaternion.identity;
            Quaternion preRKnee = rKnee != null ? rKnee.localRotation : Quaternion.identity;
            Quaternion preRAnkle = rAnkle.localRotation;

            // Keep both targets within reach when a long step or a curb/stair extends the support leg.
            float rawDrop = Mathf.Max(0f, Mathf.Max(LegExtension(lHip, lKnee, lAnkle, left),
                LegExtension(rHip, rKnee, rAnkle, right)));
            rawDrop = Mathf.Min(rawDrop, 0.36f * scaleY);
            if (rawDrop >= _smoothedPelvisDrop)
            {
                _smoothedPelvisDrop = rawDrop;
            }
            else
            {
                _smoothedPelvisDrop = Mathf.Lerp(_smoothedPelvisDrop, rawDrop, 1f - Mathf.Exp(-14f * dt));
                if (_smoothedPelvisDrop < 0.001f) _smoothedPelvisDrop = 0f;
            }
            hips.position -= transform.up * _smoothedPelvisDrop;

            AimLimb(lHip, lKnee, left, transform.forward, lAnkle.localPosition.magnitude);
            AimLimb(rHip, rKnee, right, transform.forward, rAnkle.localPosition.magnitude);
            lAnkle.rotation = leftRotation;
            rAnkle.rotation = rightRotation;

            if (weight < 0.999f)
            {
                hips.position = Vector3.Lerp(preHipsPos, hips.position, weight);
                if (lHip != null) lHip.localRotation = Quaternion.Slerp(preLHip, lHip.localRotation, weight);
                if (lKnee != null) lKnee.localRotation = Quaternion.Slerp(preLKnee, lKnee.localRotation, weight);
                lAnkle.localRotation = Quaternion.Slerp(preLAnkle, lAnkle.localRotation, weight);
                if (rHip != null) rHip.localRotation = Quaternion.Slerp(preRHip, rHip.localRotation, weight);
                if (rKnee != null) rKnee.localRotation = Quaternion.Slerp(preRKnee, rKnee.localRotation, weight);
                rAnkle.localRotation = Quaternion.Slerp(preRAnkle, rAnkle.localRotation, weight);
            }
        }

        private Vector3 SampleKneelGround(Vector3 localPos, out Vector3 normal)
        {
            Vector3 ground = transform.TransformPoint(localPos);
            normal = transform.up;
            float scaleY = transform.lossyScale.y;
            if (_sampleGround && Physics.Raycast(ground + transform.up * (0.42f * scaleY), -transform.up,
                    out RaycastHit hit, 0.88f * scaleY, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.normal.y >= 0.65f && hit.collider.transform.root != transform.root)
            {
                ground = hit.point;
                normal = hit.normal;
            }
            return ground;
        }

        private Vector3 GaitFoot(float phase, float side, Vector3 direction, float strideLength, float stance,
            float runBlend, bool tennisStance, float idleWeight, float dt, bool refreshGround, out Quaternion rotation)
        {
            float halfStep = strideLength * stance * 0.5f;
            float offset;
            float lift = 0f;
            float pitch;
            if (phase < stance)
            {
                float load = phase / stance;
                offset = Mathf.Lerp(halfStep, -halfStep, load);
                // Crisp biomechanical heel-strike (-15 deg) -> flat midstance (0 deg) -> springy toe push-off (+25 deg)!
                pitch = -15f * (1f - Mathf.SmoothStep(0f, 1f, load / 0.20f)) +
                    Mathf.Lerp(21f, 28f, runBlend) * Mathf.SmoothStep(0f, 1f, (load - 0.68f) / 0.32f);
            }
            else
            {
                float swing = (phase - stance) / (1f - stance);
                offset = Mathf.SmoothStep(-halfStep, halfStep, swing);
                // Slightly asymmetric knee-drive lift peaks earlier in swing for energetic human stride!
                float liftArc = Mathf.Sin(Mathf.Pow(swing, 0.85f) * Mathf.PI);
                lift = liftArc * (tennisStance ? 0.095f : Mathf.Lerp(0.125f, 0.24f, runBlend));
                pitch = Mathf.Lerp(Mathf.Lerp(21f, 28f, runBlend), -15f, Mathf.SmoothStep(0f, 1f, swing));
            }

            // Couple stride offset 1:1 with _phase advancement for all walking speeds >= 0.28 gaitWeight (zero foot sliding).
            float footOffsetWeight = Mathf.Clamp01(_gaitWeight / 0.28f);
            float width = tennisStance ? 0.22f : Mathf.Lerp(0.13f, 0.102f, _gaitWeight);
            float lateralMag = Mathf.Abs(direction.x);
            float lateralClearance = Mathf.Max(0f, halfStep * lateralMag * footOffsetWeight - (width - 0.065f));

            Vector3 local = new Vector3(side * (width + lateralClearance), 0f, -side * idleWeight * 0.035f) +
                direction * (offset * footOffsetWeight);
            local.x = side * Mathf.Max(0.052f, side * local.x);

            // Turn-in-place alternating shuffle step when yaw rotates while stationary
            float turnLift = 0f;
            float turnFootYaw = 0f;
            if (_turnInPlaceWeight > 0.01f && _gaitWeight < 0.35f)
            {
                float turnBlend = _turnInPlaceWeight * (1f - Mathf.Clamp01(_gaitWeight / 0.35f));
                float tPhase = Mathf.Repeat(_turnStepPhase / TwoPi + (side > 0f ? 0.5f : 0f), 1f);
                if (tPhase < 0.55f)
                {
                    float hold = tPhase / 0.55f;
                    local.z += side * _turnYawSign * Mathf.Lerp(0.036f, -0.036f, hold) * turnBlend;
                    turnFootYaw = _turnYawSign * Mathf.Lerp(9f, -9f, hold) * turnBlend;
                }
                else
                {
                    float tSwing = (tPhase - 0.55f) / 0.45f;
                    float sU = Mathf.SmoothStep(0f, 1f, tSwing);
                    local.z += side * _turnYawSign * Mathf.Lerp(-0.036f, 0.036f, sU) * turnBlend;
                    turnLift = Mathf.Sin(tSwing * Mathf.PI) * 0.065f * turnBlend;
                    turnFootYaw = _turnYawSign * Mathf.Lerp(-9f, 9f, sU) * turnBlend;
                }
            }

            float scaleY = transform.lossyScale.y;
            Vector3 ground = transform.TransformPoint(local);
            Vector3 normal = transform.up;
            bool isLeft = side < 0f;

            if (_sampleGround)
            {
                if (refreshGround)
                {
                    float rawDelta = 0f;
                    Vector3 rawNorm = transform.up;
                    if (Physics.Raycast(ground + transform.up * (0.42f * scaleY), -transform.up,
                            out RaycastHit hit, 0.88f * scaleY, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                        hit.normal.y >= 0.65f && hit.collider.transform.root != transform.root)
                    {
                        rawDelta = Mathf.Clamp(Vector3.Dot(hit.point - ground, transform.up), -0.38f * scaleY, 0.36f * scaleY);
                        rawNorm = hit.normal;
                    }
                    if (isLeft)
                    {
                        _leftRawGroundYOffset = rawDelta;
                        _leftRawGroundNormal = rawNorm;
                    }
                    else
                    {
                        _rightRawGroundYOffset = rawDelta;
                        _rightRawGroundNormal = rawNorm;
                    }
                }

                float smoothK = 1f - Mathf.Exp(-22f * dt);
                if (isLeft)
                {
                    _leftGroundYOffset = Mathf.Lerp(_leftGroundYOffset, _leftRawGroundYOffset, smoothK);
                    _leftGroundNormal = Vector3.Slerp(_leftGroundNormal, _leftRawGroundNormal, smoothK);
                    ground += transform.up * _leftGroundYOffset;
                    normal = _leftGroundNormal;
                }
                else
                {
                    _rightGroundYOffset = Mathf.Lerp(_rightGroundYOffset, _rightRawGroundYOffset, smoothK);
                    _rightGroundNormal = Vector3.Slerp(_rightGroundNormal, _rightRawGroundNormal, smoothK);
                    ground += transform.up * _rightGroundYOffset;
                    normal = _rightGroundNormal;
                }
            }
            else
            {
                float decayK = 1f - Mathf.Exp(-14f * dt);
                if (isLeft)
                {
                    _leftGroundYOffset = Mathf.Lerp(_leftGroundYOffset, 0f, decayK);
                    _leftGroundNormal = Vector3.Slerp(_leftGroundNormal, transform.up, decayK);
                    ground += transform.up * _leftGroundYOffset;
                    normal = _leftGroundNormal;
                }
                else
                {
                    _rightGroundYOffset = Mathf.Lerp(_rightGroundYOffset, 0f, decayK);
                    _rightGroundNormal = Vector3.Slerp(_rightGroundNormal, transform.up, decayK);
                    ground += transform.up * _rightGroundYOffset;
                    normal = _rightGroundNormal;
                }
            }

            // Orient heel/toe pitch along forward/back travel component (flat sole during pure sideways strafe).
            pitch *= direction.z * _gaitWeight * (tennisStance ? 0.4f : 1f);
            float soleReach = pitch > 0f ? 0.19f : 0.075f;
            float soleLift = Mathf.Abs(Mathf.Sin(pitch * Mathf.Deg2Rad)) * soleReach;
            rotation = Quaternion.FromToRotation(transform.up, normal) * transform.rotation *
                Quaternion.Euler(pitch, side * Mathf.Lerp(6f, 2f, _gaitWeight) + turnFootYaw, 0f);
            return ground + normal * ((AnkleHeight + soleLift + lift * _gaitWeight + turnLift) * scaleY);
        }

        private float LegExtension(Transform hip, Transform knee, Transform ankle, Vector3 target)
        {
            float reach = (knee.localPosition.magnitude + ankle.localPosition.magnitude - 0.009f) * transform.lossyScale.y;
            Vector3 delta = hip.position - target;
            float vertical = Vector3.Dot(delta, transform.up);
            float horizontalSquared = Vector3.ProjectOnPlane(delta, transform.up).sqrMagnitude;
            return vertical - Mathf.Sqrt(Mathf.Max(0f, reach * reach - horizontalSquared));
        }

    }
}
