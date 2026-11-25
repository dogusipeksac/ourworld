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
    public class ProceduralHumanoid : MonoBehaviour
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
        public bool HasUnevenGroundSample => Mathf.Abs(_leftGroundYOffset) > 0.015f || Mathf.Abs(_rightGroundYOffset) > 0.015f;
        public event Action<float> OnFootstepContact;
        public event Action OnLanded;

        public static ProceduralHumanoid Build(
            Transform parent,
            Material skin,
            Material shirt,
            Material pants,
            Material shoes,
            Material hair,
            float height = 1.78f)
        {
            GameObject root = new GameObject("ProceduralHumanoid");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            float uniformScale = Mathf.Max(0.25f, height / 1.78f);
            root.transform.localScale = Vector3.one * uniformScale;

            ProceduralHumanoid hum = root.AddComponent<ProceduralHumanoid>();
            hum.hipsBaseY = 0.95f;

            hum.hips = CreatePivot("Hips", root.transform, new Vector3(0f, 0.95f, 0f));
            CreatePart("Pelvis", PrimitiveType.Capsule, hum.hips,
                new Vector3(0f, 0.00f, 0f), new Vector3(0.31f, 0.13f, 0.21f), pants);

            hum.spine = CreatePivot("Spine", hum.hips, new Vector3(0f, 0.06f, 0f));
            CreatePart("Waist", PrimitiveType.Capsule, hum.spine,
                new Vector3(0f, 0.13f, 0f), new Vector3(0.30f, 0.17f, 0.20f), shirt);
            GameObject chest = CreatePart("Chest", PrimitiveType.Capsule, hum.spine,
                new Vector3(0f, 0.30f, 0.01f), new Vector3(0.39f, 0.20f, 0.23f), shirt);
            hum.chestVisual = chest.transform;
            hum._chestBaseScale = chest.transform.localScale;

            hum.neck = CreatePivot("Neck", hum.spine, new Vector3(0f, 0.46f, 0f));
            CreatePart("NeckMesh", PrimitiveType.Cylinder, hum.neck,
                new Vector3(0f, 0.045f, 0f), new Vector3(0.105f, 0.055f, 0.105f), skin);

            hum.head = CreatePivot("Head", hum.neck, new Vector3(0f, 0.09f, 0f));
            CreatePart("HeadMesh", PrimitiveType.Sphere, hum.head,
                new Vector3(0f, 0.10f, 0f), new Vector3(0.22f, 0.25f, 0.23f), skin);
            CreatePart("HairCap", PrimitiveType.Sphere, hum.head,
                new Vector3(0f, 0.145f, -0.025f), new Vector3(0.235f, 0.175f, 0.235f), hair);
            CreatePart("Nose", PrimitiveType.Cube, hum.head,
                new Vector3(0f, 0.092f, 0.115f), new Vector3(0.036f, 0.048f, 0.042f), skin);

            BuildArm("L", -1f, hum.spine, shirt, skin, out hum.lShoulder, out hum.lElbow);
            BuildArm("R", 1f, hum.spine, shirt, skin, out hum.rShoulder, out hum.rElbow);

            BuildLeg("L", -1f, hum.hips, pants, shoes, out hum.lHip, out hum.lKnee, out hum.lAnkle);
            BuildLeg("R", 1f, hum.hips, pants, shoes, out hum.rHip, out hum.rKnee, out hum.rAnkle);

            return hum;
        }

        /// <summary>
        /// Builds the exact 14-pivot ProceduralHumanoid skeleton and mounts externally sculpted
        /// body-part meshes (e.g. Blender FBX parts centered at each joint origin) onto every pivot.
        /// </summary>
        public static ProceduralHumanoid BuildWithCustomParts(
            Transform parent,
            Func<string, Transform, Transform> mountPart,
            float height = 1.78f,
            float shoulderX = 0.198f,
            float hipX = 0.102f,
            float shoulderY = 0.382f)
        {
            GameObject root = new GameObject("ProceduralHumanoid");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            float uniformScale = Mathf.Max(0.25f, height / 1.78f);
            root.transform.localScale = Vector3.one * uniformScale;

            ProceduralHumanoid hum = root.AddComponent<ProceduralHumanoid>();
            hum.hipsBaseY = 0.95f;

            hum.hips = CreatePivot("Hips", root.transform, new Vector3(0f, 0.95f, 0f));
            mountPart?.Invoke("Part_Pelvis", hum.hips);

            hum.spine = CreatePivot("Spine", hum.hips, new Vector3(0f, 0.06f, 0f));
            Transform chestMounted = mountPart?.Invoke("Part_Chest", hum.spine);
            if (chestMounted != null)
            {
                hum.chestVisual = chestMounted;
                hum._chestBaseScale = chestMounted.localScale;
            }

            hum.neck = CreatePivot("Neck", hum.spine, new Vector3(0f, 0.46f, 0f));
            mountPart?.Invoke("Part_Neck", hum.neck);

            hum.head = CreatePivot("Head", hum.neck, new Vector3(0f, 0.09f, 0f));
            mountPart?.Invoke("Part_Head", hum.head);

            hum.lShoulder = CreatePivot("L_Shoulder", hum.spine, new Vector3(-shoulderX, shoulderY, 0f));
            mountPart?.Invoke("Part_L_UpperArm", hum.lShoulder);
            hum.lElbow = CreatePivot("L_Elbow", hum.lShoulder, new Vector3(0f, -0.295f, 0f));
            mountPart?.Invoke("Part_L_Forearm", hum.lElbow);

            hum.rShoulder = CreatePivot("R_Shoulder", hum.spine, new Vector3(shoulderX, shoulderY, 0f));
            mountPart?.Invoke("Part_R_UpperArm", hum.rShoulder);
            hum.rElbow = CreatePivot("R_Elbow", hum.rShoulder, new Vector3(0f, -0.295f, 0f));
            mountPart?.Invoke("Part_R_Forearm", hum.rElbow);

            hum.lHip = CreatePivot("L_Hip", hum.hips, new Vector3(-hipX, -0.02f, 0f));
            mountPart?.Invoke("Part_L_Thigh", hum.lHip);
            hum.lKnee = CreatePivot("L_Knee", hum.lHip, new Vector3(0f, -0.43f, 0f));
            mountPart?.Invoke("Part_L_Shin", hum.lKnee);
            hum.lAnkle = CreatePivot("L_Ankle", hum.lKnee, new Vector3(0f, -0.42f, 0f));
            mountPart?.Invoke("Part_L_Foot", hum.lAnkle);

            hum.rHip = CreatePivot("R_Hip", hum.hips, new Vector3(hipX, -0.02f, 0f));
            mountPart?.Invoke("Part_R_Thigh", hum.rHip);
            hum.rKnee = CreatePivot("R_Knee", hum.rHip, new Vector3(0f, -0.43f, 0f));
            mountPart?.Invoke("Part_R_Shin", hum.rKnee);
            hum.rAnkle = CreatePivot("R_Ankle", hum.rKnee, new Vector3(0f, -0.42f, 0f));
            mountPart?.Invoke("Part_R_Foot", hum.rAnkle);

            return hum;
        }

        private static void BuildArm(
            string side,
            float xSign,
            Transform spineParent,
            Material shirt,
            Material skin,
            out Transform shoulderPivot,
            out Transform elbowPivot)
        {
            shoulderPivot = CreatePivot($"{side}_Shoulder", spineParent, new Vector3(xSign * 0.225f, 0.40f, 0f));
            CreatePart($"{side}_UpperArm", PrimitiveType.Capsule, shoulderPivot,
                new Vector3(0f, -0.15f, 0f), new Vector3(0.115f, 0.165f, 0.115f), shirt);

            elbowPivot = CreatePivot($"{side}_Elbow", shoulderPivot, new Vector3(0f, -0.30f, 0f));
            CreatePart($"{side}_Forearm", PrimitiveType.Capsule, elbowPivot,
                new Vector3(0f, -0.135f, 0f), new Vector3(0.095f, 0.15f, 0.095f), shirt);
            CreatePart($"{side}_Hand", PrimitiveType.Sphere, elbowPivot,
                new Vector3(0f, -0.295f, 0.01f), new Vector3(0.072f, 0.105f, 0.045f), skin);
        }

        private static void BuildLeg(
            string side,
            float xSign,
            Transform hipsParent,
            Material pants,
            Material shoes,
            out Transform hipPivot,
            out Transform kneePivot,
            out Transform anklePivot)
        {
            hipPivot = CreatePivot($"{side}_Hip", hipsParent, new Vector3(xSign * 0.105f, -0.02f, 0f));
            CreatePart($"{side}_Thigh", PrimitiveType.Capsule, hipPivot,
                new Vector3(0f, -0.215f, 0f), new Vector3(0.155f, 0.23f, 0.16f), pants);

            kneePivot = CreatePivot($"{side}_Knee", hipPivot, new Vector3(0f, -0.43f, 0f));
            CreatePart($"{side}_Shin", PrimitiveType.Capsule, kneePivot,
                new Vector3(0f, -0.21f, 0f), new Vector3(0.125f, 0.225f, 0.13f), pants);

            anklePivot = CreatePivot($"{side}_Ankle", kneePivot, new Vector3(0f, -0.42f, 0f));
            CreatePart($"{side}_Foot", PrimitiveType.Cube, anklePivot,
                new Vector3(0f, -0.04f, 0.055f), new Vector3(0.11f, 0.08f, 0.25f), shoes);
        }

        private static Transform CreatePivot(string pivotName, Transform parent, Vector3 localPos)
        {
            GameObject go = new GameObject(pivotName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        private static GameObject CreatePart(
            string partName,
            PrimitiveType type,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = partName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(col);
                }
                else
                {
                    DestroyImmediate(col);
                }
            }

            if (mat != null)
            {
                Renderer r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = mat;
                }
            }

            return go;
        }

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

        private void ResetSeatedGesture()
        {
            _seatGestureKind = 0;
            _seatGestureTimer = -1f;
            _seatGestureCooldown = -1f;
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
                hips.localPosition = new Vector3(0f, 0.08f, 0.02f);
                hips.localRotation = Quaternion.identity;
                if (spine != null) spine.localRotation = Quaternion.Euler(-7f, 0f, 0f);
                if (neck != null) neck.localRotation = Quaternion.Euler(5f, 0f, 0f);
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

            _idleTime += dt;
            if (_isLyingDown)
            {
                _lyingBlend = Mathf.MoveTowards(_lyingBlend, _lyingTarget ? 1f : 0f, dt / Mathf.Max(0.05f, _lyingTransition));
                if (!_lyingTarget && _lyingBlend <= 0f)
                {
                    _isLyingDown = false;
                    _lyingStirTimer = -1f;
                }
                else if (_lyingBlend >= 0.999f && _lyingStirTimer < 0f)
                {
                    _lyingStirCountdown -= dt;
                    if (_lyingStirCountdown <= 0f)
                    {
                        _lyingStirTimer = 0f;
                        _lyingStirSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                        _lyingStirCountdown = UnityEngine.Random.Range(9f, 20f);
                    }
                }
            }
            if (!_grounded)
            {
                _airborneTimer += dt;
            }

            if (_landSquatTimer >= 0f)
            {
                _landSquatTimer += dt;
                if (_landSquatTimer >= _landSquatDuration)
                {
                    _landSquatTimer = -1f;
                }
            }

            if (_carryReleaseTimer >= 0f)
            {
                _carryReleaseTimer += dt;
                if (_carryReleaseTimer >= CarryReleaseFadeDuration)
                {
                    _carryReleaseTimer = -1f;
                }
            }

            if (_carryThrowTimer >= 0f)
            {
                _carryThrowTimer += dt;
                if (_carryThrowTimer >= CarryThrowFollowDuration)
                {
                    _carryThrowTimer = -1f;
                }
            }

            if ((transform.position - _prevWorldPos).sqrMagnitude > 1.0f)
            {
                _leftGroundYOffset = 0f;
                _rightGroundYOffset = 0f;
                _leftRawGroundYOffset = 0f;
                _rightRawGroundYOffset = 0f;
                _leftGroundNormal = transform.up;
                _rightGroundNormal = transform.up;
                _leftRawGroundNormal = transform.up;
                _rightRawGroundNormal = transform.up;
                _smoothedPelvisDrop = 0f;
                _groundSampleValid = false;
            }
            _prevWorldPos = transform.position;

            if (_startledTimer >= 0f)
            {
                _startledTimer += dt;
                if (_startledTimer >= _startledDuration)
                {
                    _startledTimer = -1f;
                }
            }

            if (autoRandomWave && !isSitting && !_isLyingDown && _speedMs < 0.1f)
            {
                _autoWaveCountdown -= dt;
                if (_autoWaveCountdown <= 0f)
                {
                    PlayWave();
                    _autoWaveCountdown = UnityEngine.Random.Range(8f, 12f);
                }
            }

            if (_waveTimer >= 0f)
            {
                _waveTimer += dt;
                if (_waveTimer >= WaveDuration)
                {
                    _waveTimer = -1f;
                }
            }

            if (_punchTimer >= 0f)
            {
                _punchTimer += dt;
                float dur = _combatMoveIndex == 2 ? KickDuration : PunchDuration;
                if (_punchTimer >= dur)
                {
                    _punchTimer = -1f;
                }
            }

            if (_hitReactTimer >= 0f)
            {
                _hitReactTimer += dt;
                if (_hitReactTimer >= HitReactDuration)
                {
                    _hitReactTimer = -1f;
                }
            }

            if (_knockdownTimer >= 0f)
            {
                _knockdownTimer += dt;
                if (_knockdownTimer >= _activeKnockdownDuration)
                {
                    _knockdownTimer = -1f;
                }
            }

            if (_vaultTimer >= 0f)
            {
                _vaultTimer += dt;
                if (_vaultTimer >= VaultDuration)
                {
                    _vaultTimer = -1f;
                }
            }

            if (_sportAnimTimer >= 0f)
            {
                _sportAnimTimer += dt;
                if (_sportAnimTimer >= _sportAnimDuration)
                {
                    _sportAnimTimer = -1f;
                    _sportAnimKind = SportAnimKind.None;
                }
            }

            if (isSitting)
            {
                AnimateSitting(dt);
                _baseHipsLocalPos = hips.localPosition;
                return;
            }

            // Detect active carry and cat petting early so conflicting arm overlays never fight them.
            CarryableProp carry = CarryableProp.CurrentlyHeld;
            bool carrying = carry != null && carry.UsesRig(this);
            if (carrying)
            {
                _carryReleaseTimer = -1f;
                _carryThrowTimer = -1f;
                _waveTimer = -1f;
            }
            float targetCarryLoad = (carrying && carry.IsAttached) ? carry.CarryHeaviness : 0f;
            _carryLoadBlend = Mathf.Lerp(_carryLoadBlend, targetCarryLoad, 1f - Mathf.Exp(-8f * dt));

            Vector3 petHand = Vector3.zero;
            float petCrouch = 0f;
            float petWeight = 0f;
            StreetCatController cat = StreetCatController.ActivePet;
            bool petting = cat != null && cat.TryGetPetPose(this, out petCrouch, out petWeight, out petHand);
            if (petting)
            {
                float scaleY = transform.lossyScale.y;
                Vector3 rawPetHand = petHand;
                if (_hasPrevPetSample)
                {
                    float fwdMotion = Vector3.Dot(rawPetHand - _prevPetHandSample, cat.transform.forward);
                    float desiredReturnLift = fwdMotion > 0.0004f ? 0.024f : 0f;
                    _petReturnLift = Mathf.Lerp(_petReturnLift, desiredReturnLift, 1f - Mathf.Exp(-14f * dt));
                }
                else
                {
                    _petReturnLift = 0f;
                    _hasPrevPetSample = true;
                }
                _prevPetHandSample = rawPetHand;
                // Keep palm surface gliding cleanly over the cat's head->back coat, never sinking into the body.
                petHand += transform.up * (0.036f * scaleY + _petReturnLift);
                float catBackMinY = cat.transform.position.y + 0.305f * cat.transform.lossyScale.y;
                if (petHand.y < catBackMinY) petHand.y = catBackMinY;
                _lastPetHandWorld = petHand;
                _petFadeBlend = petWeight;
            }
            else
            {
                _hasPrevPetSample = false;
                _petReturnLift = 0f;
                if (_petFadeBlend > 0.001f)
                {
                    _petFadeBlend = Mathf.Lerp(_petFadeBlend, 0f, 1f - Mathf.Exp(-14f * dt));
                    if (_petFadeBlend < 0.005f) _petFadeBlend = 0f;
                }
            }

            // Track yaw change rate of root for turning lean (+/- 10 deg) AND natural head turn anticipation!
            float currentYaw = transform.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(_prevYaw, currentYaw);
            _prevYaw = currentYaw;
            float yawRate = yawDelta / dt;
            float moveFactor = Mathf.Clamp01(_speedMs / WalkSpeedRef);
            float targetTurnLean = Mathf.Clamp(-yawRate * 0.068f, -10f, 10f) * moveFactor;
            _smoothedTurnLean = Mathf.Lerp(_smoothedTurnLean, targetTurnLean, 1f - Mathf.Exp(-8.5f * dt));
            float targetHeadLead = Mathf.Clamp(yawRate * 0.14f, -22f, 22f) * Mathf.Clamp01(moveFactor + 0.35f);
            _smoothedHeadTurnLead = Mathf.Lerp(_smoothedHeadTurnLead, targetHeadLead, 1f - Mathf.Exp(-9f * dt));

            // Track recent peak speed so stopping from normal walk/run deceleration triggers braking settle.
            if (_speedMs > _recentPeakSpeed)
            {
                _recentPeakSpeed = _speedMs;
            }
            else
            {
                _recentPeakSpeed = Mathf.MoveTowards(_recentPeakSpeed, _speedMs, 2.4f * dt);
            }
            if (_grounded && _recentPeakSpeed > 1.45f && _speedMs < 0.25f && _prevSpeedMs >= 0.25f && _stopSettleTimer < 0f)
            {
                _stopSettleTimer = 0f;
                _stopSettleStrength = Mathf.Clamp01((_recentPeakSpeed - 1.05f) / 3.0f);
                _recentPeakSpeed = 0f;
            }
            if (_stopSettleTimer >= 0f)
            {
                _stopSettleTimer += dt;
                if (_stopSettleTimer >= 0.38f || _speedMs > 0.8f)
                {
                    _stopSettleTimer = -1f;
                }
            }

            if (_talkingTimer > 0f)
            {
                _talkingTimer = Mathf.Max(0f, _talkingTimer - dt);
            }
            float desiredTalkBlend = (_talkingTimer > 0f && _speedMs < 0.45f && _grounded) ? 1f : 0f;
            _talkingBlend = Mathf.Lerp(_talkingBlend, desiredTalkBlend, 1f - Mathf.Exp(-7.5f * dt));

            // Compute idle / walk / run blends and smooth travel direction (prevents 1-frame foot pop on sharp cuts).
            float moveBlend = Mathf.Clamp01((_speedMs - 0.08f) / 1.2f);
            float runBlend = Mathf.InverseLerp(WalkSpeedRef, RunSpeedRef, _speedMs);

            if (_gaitWeight < 0.04f && _speedMs > 0.05f)
            {
                _smoothedTravelDir = _travelDirection;
            }
            else if (_travelDirection.sqrMagnitude > 0.0001f)
            {
                _smoothedTravelDir = Vector3.Slerp(_smoothedTravelDir, _travelDirection, 1f - Mathf.Exp(-14f * dt));
                if (_smoothedTravelDir.sqrMagnitude > 0.0001f) _smoothedTravelDir.Normalize();
            }
            Vector3 localTravel = transform.InverseTransformDirection(_smoothedTravelDir);
            localTravel.y = 0f;
            if (localTravel.sqrMagnitude > 0.0001f) localTravel.Normalize();
            else localTravel = Vector3.forward;

            float fwdDot = localTravel.z;
            float lateralMag = Mathf.Abs(localTravel.x);
            float cutIntensity = (_grounded && _speedMs > 1.0f)
                ? Mathf.Clamp01((1f - Vector3.Dot(_smoothedTravelDir, _travelDirection)) * 1.4f)
                : 0f;

            _gaitWeight = Mathf.MoveTowards(_gaitWeight, moveBlend, 9f * dt);
            bool tennisStance = _tennisRacket != null;
            float stance = tennisStance ? 0.54f : Mathf.Lerp(0.61f, 0.44f, runBlend);
            float dirStrideScale = Mathf.Lerp(1f, 0.50f, lateralMag) * (fwdDot < 0f ? Mathf.Lerp(1f, 0.72f, -fwdDot) : 1f);
            float carryStrideScale = Mathf.Lerp(1f, 0.78f, _carryLoadBlend);
            float baseStride = (tennisStance ? 1.12f : Mathf.Lerp(1.22f, 1.72f, runBlend)) * dirStrideScale * carryStrideScale;
            float strideLength = baseStride * Mathf.Max(0.28f, _gaitWeight);

            // Advance gait phase by distance travelled & fire footstep contact events
            if (_speedMs > 0.05f && _grounded)
            {
                _phase += _speedMs * dt / (strideLength * transform.lossyScale.y) * TwoPi;

                if (_speedMs > 0.25f)
                {
                    int currentStepCount = Mathf.FloorToInt(_phase / Mathf.PI);
                    if (currentStepCount != _lastStepCount)
                    {
                        _lastStepCount = currentStepCount;
                        OnFootstepContact?.Invoke(runBlend);
                    }
                }

                if (_phase > Mathf.PI * 200f)
                {
                    _phase -= Mathf.PI * 200f;
                    _lastStepCount = Mathf.FloorToInt(_phase / Mathf.PI);
                }
            }

            // Turn-in-place foot shuffle when yaw rotates while stationary
            bool canTurnInPlace = _grounded && _speedMs < 0.28f && !_isLyingDown && _prayerStage == 0 &&
                                  !_isSwimming && _kneelBlend < 0.12f && !petting &&
                                  _knockdownTimer < 0f && _vaultTimer < 0f;
            float absYawRate = Mathf.Abs(yawRate);
            float desiredTurnW = (canTurnInPlace && absYawRate > 18f) ? Mathf.Clamp01((absYawRate - 18f) / 55f) : 0f;
            _turnInPlaceWeight = Mathf.Lerp(_turnInPlaceWeight, desiredTurnW,
                1f - Mathf.Exp(-(desiredTurnW > _turnInPlaceWeight ? 14f : 8.5f) * dt));
            if (_turnInPlaceWeight > 0.01f)
            {
                if (absYawRate > 12f) _turnYawSign = Mathf.Sign(yawRate);
                _turnStepPhase += Mathf.Max(absYawRate, 68f * desiredTurnW) * dt / 95f * TwoPi;
                if (_turnStepPhase > TwoPi * 100f) _turnStepPhase -= TwoPi * 100f;
            }
            else
            {
                _turnInPlaceWeight = 0f;
            }

            // --- Idle components ---
            float breathWave = Mathf.Sin((_idleTime + _noiseSeed) * Mathf.PI * 2f * 0.25f);
            float idleChestPitch = breathWave * 1.5f;
            float idleWeightShiftX = Mathf.Sin((_idleTime + _noiseSeed * 0.5f) * Mathf.PI * 2f * 0.2f) * 0.015f;
            float headNoise = (Mathf.PerlinNoise(_noiseSeed, _idleTime * 0.18f) - 0.5f) * 2f;
            float idleHeadYaw = headNoise * 10f;

            // --- Head-look IK-lite toward nearest interactable within 4 m (max 60 deg yaw) ---
            float desiredLookYaw = 0f;
            float desiredLookPitch = 0f;
            bool useLookIk = false;
            if (_hasLookPoint && _grounded && !_isLyingDown)
            {
                Vector3 localTarget = transform.InverseTransformPoint(_lookWorldPoint) - new Vector3(0f, 1.54f, 0f);
                float horizDist = new Vector2(localTarget.x, localTarget.z).magnitude;
                if (horizDist > 0.12f)
                {
                    float rawYaw = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(rawYaw) <= 100f)
                    {
                        desiredLookYaw = Mathf.Clamp(rawYaw, -60f, 60f);
                        desiredLookPitch = Mathf.Clamp(-Mathf.Atan2(localTarget.y, horizDist) * Mathf.Rad2Deg, -24f, 24f);
                        useLookIk = true;
                    }
                }
            }

            float lookBlendRate = 1f - Mathf.Exp(-7.5f * dt);
            _smoothedLookYaw = Mathf.Lerp(_smoothedLookYaw, useLookIk ? desiredLookYaw : 0f, lookBlendRate);
            _smoothedLookPitch = Mathf.Lerp(_smoothedLookPitch, useLookIk ? desiredLookPitch : 0f, lookBlendRate);
            _lookWeight = Mathf.Lerp(_lookWeight, useLookIk ? 1f : 0f, lookBlendRate);

            // Biomechanical arm pendulum with realistic elbow phase-lag & inward cross-body arc!
            float armDirScale = Mathf.Lerp(-0.65f, 1f, Mathf.InverseLerp(-1f, 0.25f, fwdDot)) * Mathf.Lerp(1f, 0.45f, lateralMag);
            float carrySwingDamp = Mathf.Lerp(1f, 0.35f, _carryLoadBlend);
            float armCycle = Mathf.Cos(_phase - 0.19f) * armDirScale * carrySwingDamp;
            float forearmLagL = Mathf.Cos(_phase - 0.62f) * armDirScale * carrySwingDamp;
            float forearmLagR = -forearmLagL;
            float armAmp = Mathf.Lerp(19f, 44f, runBlend);
            float elbowBaseBend = Mathf.Lerp(18f, 70f, runBlend);
            float walkShoulderL = armCycle * armAmp;
            float walkShoulderR = -walkShoulderL;
            float walkShoulderYawL = Mathf.Lerp(6f, 14f, runBlend) * Mathf.Max(0f, -armCycle);
            float walkShoulderYawR = -Mathf.Lerp(6f, 14f, runBlend) * Mathf.Max(0f, armCycle);
            float walkElbowL = -(elbowBaseBend + Mathf.Max(0f, -forearmLagL) * Mathf.Lerp(18f, 28f, runBlend));
            float walkElbowR = -(elbowBaseBend + Mathf.Max(0f, -forearmLagR) * Mathf.Lerp(18f, 28f, runBlend));
            float torsoForwardLean = fwdDot >= 0f
                ? Mathf.Lerp(3.5f, 12.5f, runBlend) * Mathf.Lerp(0.35f, 1f, fwdDot)
                : Mathf.Lerp(1f, -4.5f, -fwdDot);
            float strafeLeanZ = -localTravel.x * Mathf.Lerp(3f, 6.5f, runBlend) * moveBlend;
            float walkBobY = -Mathf.Cos(_phase * 2f) * Mathf.Lerp(0.020f, 0.042f, runBlend) * Mathf.Lerp(1f, 0.65f, lateralMag);
            float walkHipYaw = Mathf.Cos(_phase) * Mathf.Lerp(6.2f, 10.5f, runBlend) * Mathf.Lerp(1f, 0.4f, lateralMag) * carrySwingDamp;
            float walkHipRoll = -Mathf.Sin(_phase) * Mathf.Lerp(3.4f, 4.6f, runBlend);
            float walkHipSwayX = -Mathf.Sin(_phase) * Mathf.Lerp(0.020f, 0.015f, runBlend);

            // Blend Idle <-> Locomotion
            Vector3 targetHipsPos = new Vector3(
                Mathf.Lerp(idleWeightShiftX, walkHipSwayX, moveBlend),
                hipsBaseY - _gaitWeight * 0.064f + walkBobY * moveBlend - 0.055f * cutIntensity - 0.014f * _turnInPlaceWeight,
                0f);

            float defaultHeadYaw = Mathf.Lerp(idleHeadYaw, 0f, moveBlend) + _smoothedHeadTurnLead * 0.65f;
            float lookHeadYaw = _smoothedLookYaw * 0.75f + _smoothedHeadTurnLead * 0.25f;
            float finalHeadYaw = Mathf.Lerp(defaultHeadYaw, lookHeadYaw, _lookWeight);

            Vector3 targetHipsRot = new Vector3(
                0f,
                walkHipYaw * moveBlend - _turnYawSign * 7f * _turnInPlaceWeight,
                walkHipRoll * moveBlend);
            Vector3 targetSpineRot = new Vector3(
                Mathf.Lerp(idleChestPitch, torsoForwardLean, moveBlend),
                -walkHipYaw * 1.20f * moveBlend,
                _smoothedTurnLean + strafeLeanZ - Mathf.Sign(localTravel.x) * 7f * cutIntensity - walkHipRoll * 0.72f * moveBlend);
            Vector3 targetNeckRot = new Vector3(
                -targetSpineRot.x * 0.35f + _smoothedLookPitch * 0.25f,
                _smoothedLookYaw * 0.25f + _smoothedHeadTurnLead * 0.35f,
                -_smoothedTurnLean * 0.35f);
            Vector3 targetHeadRot = new Vector3(_smoothedLookPitch * 0.75f, finalHeadYaw, -_smoothedTurnLean * 0.25f);

            Vector3 targetLHipRot = new Vector3(0f, 0f, -3.2f);
            Vector3 targetRHipRot = new Vector3(0f, 0f, 3.2f);
            Vector3 targetLKneeRot = new Vector3(3.5f, 0f, 0f);
            Vector3 targetRKneeRot = targetLKneeRot;
            Vector3 targetLAnkleRot = Vector3.zero;
            Vector3 targetRAnkleRot = Vector3.zero;

            float idleArmBreath = breathWave * 1.6f;
            Vector3 targetLShoulderRot = new Vector3(
                Mathf.Lerp(3.5f + idleArmBreath, walkShoulderL, moveBlend),
                walkShoulderYawL * moveBlend,
                Mathf.Lerp(-9.5f, -5.2f + Mathf.Abs(armCycle) * 2.5f, moveBlend));
            Vector3 targetRShoulderRot = new Vector3(
                Mathf.Lerp(3.5f + idleArmBreath, walkShoulderR, moveBlend),
                walkShoulderYawR * moveBlend,
                Mathf.Lerp(9.5f, 5.2f - Mathf.Abs(armCycle) * 2.5f, moveBlend));
            Vector3 targetLElbowRot = new Vector3(Mathf.Lerp(-16f, walkElbowL, moveBlend), 0f, 0f);
            Vector3 targetRElbowRot = new Vector3(Mathf.Lerp(-16f, walkElbowR, moveBlend), 0f, 0f);

            // Braking / stopping forward-dip & rebound settle!
            if (_stopSettleTimer >= 0f && _grounded)
            {
                float sn = Mathf.Clamp01(_stopSettleTimer / 0.38f);
                float settleWave = Mathf.Sin(sn * Mathf.PI) * (1f - sn * 0.45f) * _stopSettleStrength;
                targetHipsPos.y -= 0.048f * settleWave;
                targetHipsPos.z += 0.032f * settleWave * Mathf.Sign(fwdDot >= -0.1f ? 1f : -1f);
                targetSpineRot.x += 7.0f * settleWave * (fwdDot >= -0.1f ? 1f : -0.6f);
                if (!carrying)
                {
                    targetLShoulderRot.x += 12f * settleWave;
                    targetRShoulderRot.x += 12f * settleWave;
                }
                targetLKneeRot.x += 12f * settleWave;
                targetRKneeRot.x += 12f * settleWave;
            }

            // Natural Idle Micro-Fidgets (Hands-on-hips, Neck/Shoulder Stretch, Wrist Glance) when standing idle!
            bool canFidget = moveBlend < 0.04f && _grounded && _talkingBlend < 0.1f && !_hasDualAction &&
                             !carrying && !petting && _consumeWeight < 0.02f && !_hasShelfReach &&
                             _carryReleaseTimer < 0f && _carryThrowTimer < 0f &&
                             _prayerStage == 0 && !_isSwimming && !_isLyingDown && _waveTimer < 0f &&
                             _sportAnimTimer < 0f && _tennisRacket == null;
            if (canFidget)
            {
                if (_idleGestureTimer >= 0f)
                {
                    _idleGestureTimer += dt;
                    if (_idleGestureTimer >= _idleGestureDuration)
                    {
                        _idleGestureTimer = -1f;
                        _idleGestureKind = 0;
                        _idleGestureCooldown = UnityEngine.Random.Range(7.5f, 14.5f);
                    }
                }
                else
                {
                    _idleGestureCooldown -= dt;
                    if (_idleGestureCooldown <= 0f)
                    {
                        _idleGestureKind = UnityEngine.Random.Range(1, 4);
                        _idleGestureTimer = 0f;
                        _idleGestureDuration = _idleGestureKind == 1 ? 3.6f : (_idleGestureKind == 2 ? 2.6f : 2.3f);
                    }
                }
            }
            else
            {
                _idleGestureTimer = -1f;
                _idleGestureKind = 0;
            }

            if (_idleGestureTimer >= 0f && _idleGestureKind > 0)
            {
                float gn = Mathf.Clamp01(_idleGestureTimer / Mathf.Max(0.2f, _idleGestureDuration));
                float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(gn / 0.22f)) *
                            (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((gn - 0.76f) / 0.24f)));

                if (_idleGestureKind == 1)
                {
                    // Relaxed hands-on-hips breather
                    targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(12f, 14f, -34f), env);
                    targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(12f, -14f, 34f), env);
                    targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-86f, 0f, 24f), env);
                    targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-86f, 0f, -24f), env);
                    targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -4f + breathWave * 2.5f, env);
                }
                else if (_idleGestureKind == 2)
                {
                    // Subtle neck stretch & shoulder roll
                    float tilt = Mathf.Sin(gn * Mathf.PI * 2f) * 16f;
                    targetHeadRot.z += tilt * env;
                    targetHeadRot.y += Mathf.Sin(gn * Mathf.PI) * 18f * env;
                    targetLShoulderRot.z -= 5f * Mathf.Sin(gn * Mathf.PI) * env;
                    targetRShoulderRot.z += 5f * Mathf.Sin(gn * Mathf.PI) * env;
                }
                else
                {
                    // Quick glance at wristwatch on left arm
                    targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-44f, 28f, -12f), env);
                    targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-96f, 0f, 0f), env);
                    targetHeadRot.x = Mathf.Lerp(targetHeadRot.x, 22f, env);
                    targetHeadRot.y = Mathf.Lerp(targetHeadRot.y, -16f, env);
                }
            }

            // Natural Conversational Hand Gestures & Expressive Head Nods when speaking!
            if (_talkingBlend > 0.02f && !carrying && !petting && _consumeWeight < 0.05f && !_hasShelfReach &&
                !_hasDualAction && _waveTimer < 0f && _prayerStage == 0 && !_isLyingDown)
            {
                float t = _idleTime + _noiseSeed;
                // Phrase-modulated cadence so hands pause naturally between sentences instead of metronomic swinging
                float phrase = Mathf.SmoothStep(0.15f, 0.85f, Mathf.Sin(t * 1.45f) * 0.5f + 0.5f);
                float gR = Mathf.Sin(t * 3.6f) * phrase;
                float gL = Mathf.Sin(t * 2.7f + 1.6f) * (0.35f + 0.65f * (1f - phrase * 0.5f));
                float emphasize = Mathf.SmoothStep(0f, 1f, Mathf.Sin(t * 1.9f) * 0.5f + 0.5f);

                Vector3 talkRSh = new Vector3(-34f - gR * 20f * emphasize, -12f + gR * 12f, 16f + Mathf.Abs(gR) * 14f);
                Vector3 talkRElb = new Vector3(-68f + gR * 22f, 0f, 0f);
                Vector3 talkLSh = new Vector3(-26f - gL * 14f, 10f - gL * 9f, -14f - Mathf.Abs(gL) * 11f);
                Vector3 talkLElb = new Vector3(-64f + gL * 18f, 0f, 0f);

                targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, talkRSh, _talkingBlend);
                targetRElbowRot = Vector3.Lerp(targetRElbowRot, talkRElb, _talkingBlend);
                targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, talkLSh, _talkingBlend * 0.85f);
                targetLElbowRot = Vector3.Lerp(targetLElbowRot, talkLElb, _talkingBlend * 0.85f);
                targetSpineRot.x += (4f + Mathf.Sin(t * 2.1f) * 2.5f) * _talkingBlend;
                targetSpineRot.y += Mathf.Sin(t * 1.5f) * 6.5f * _talkingBlend;
                targetHeadRot.x += Mathf.Sin(t * 4.2f) * 7f * _talkingBlend * (0.4f + 0.6f * phrase);
                targetHeadRot.z += Mathf.Sin(t * 2.2f) * 5.5f * _talkingBlend;
            }
            else if (_talkingBlend > 0.02f && carrying && _prayerStage == 0 && !_isLyingDown)
            {
                // Keep hands firmly on the carried object; only animate expressive head nods while talking.
                float t = _idleTime + _noiseSeed;
                targetHeadRot.x += Mathf.Sin(t * 4.0f) * 6f * _talkingBlend;
                targetHeadRot.z += Mathf.Sin(t * 2.1f) * 4f * _talkingBlend;
            }

            // --- v13 polish: lean into acceleration, settle back when stopping ---
            float accel = (_speedMs - _prevSpeedMs) / dt;
            _prevSpeedMs = _speedMs;
            float accelDirSign = fwdDot >= -0.15f ? 1f : -0.7f;
            float targetAccelLean = _grounded ? Mathf.Clamp(accel * 1.4f * accelDirSign, -7f, 9f) : 0f;
            _smoothedAccelLean = Mathf.Lerp(_smoothedAccelLean, targetAccelLean, 1f - Mathf.Exp(-6f * dt));
            targetSpineRot.x += _smoothedAccelLean;
            targetNeckRot.x -= _smoothedAccelLean * 0.4f; // keep gaze level

            // --- v13 polish: slightly lower, more athletic pelvis while running (reduces stiff-leg float) ---
            targetHipsPos.y -= 0.035f * runBlend * moveBlend;

            // --- v13 polish: natural idle weight shift from leg to leg (contrapposto) every few seconds ---
            if (moveBlend < 0.05f && _grounded && !_isLyingDown && _turnInPlaceWeight < 0.1f)
            {
                _idleShiftCountdown -= dt;
                if (_idleShiftCountdown <= 0f)
                {
                    _idleShiftTarget = _idleShiftTarget == 0f ? (UnityEngine.Random.value < 0.5f ? -1f : 1f) : 0f;
                    _idleShiftCountdown = UnityEngine.Random.Range(4.5f, 9f);
                }
            }
            else
            {
                _idleShiftTarget = 0f;
            }
            _idleShift = Mathf.Lerp(_idleShift, _idleShiftTarget, 1f - Mathf.Exp(-1.8f * dt));
            float idleW = _idleShift * (1f - moveBlend);
            targetHipsPos.x += idleW * 0.03f;
            targetHipsRot.z += idleW * 3.2f;
            targetSpineRot.z -= idleW * 2.4f;
            targetLKneeRot.x += Mathf.Max(0f, idleW) * 9f;   // relaxed knee on the unloaded leg
            targetRKneeRot.x += Mathf.Max(0f, -idleW) * 9f;
            targetLHipRot.x -= Mathf.Max(0f, idleW) * 4f;
            targetRHipRot.x -= Mathf.Max(0f, -idleW) * 4f;

            // --- Airborne override (take-off extension -> apex tuck & air balance -> landing reach) ---
            if (!_grounded)
            {
                float launchImpulse = _verticalVelocity > 1.2f
                    ? (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_airborneTimer / 0.18f)))
                    : 0f;
                float descendPrep = Mathf.Clamp01((-_verticalVelocity - 1.5f) / 5.5f);
                float apexTuck = Mathf.Clamp01(1f - launchImpulse * 0.85f - descendPrep * 0.55f);
                float airBalance = Mathf.Sin(_idleTime * 6.5f) * 8f;

                targetHipsPos.y = hipsBaseY + Mathf.Lerp(0.02f, 0.06f, apexTuck) - 0.04f * launchImpulse;
                targetLHipRot = new Vector3(Mathf.Lerp(-8f, -34f, apexTuck) + 14f * launchImpulse, 0f, -5f);
                targetRHipRot = new Vector3(Mathf.Lerp(-16f, -24f, apexTuck) + 10f * launchImpulse, 0f, 5f);
                targetLKneeRot = new Vector3(Mathf.Lerp(22f, 58f, apexTuck) * (1f - 0.55f * launchImpulse), 0f, 0f);
                targetRKneeRot = new Vector3(Mathf.Lerp(18f, 48f, apexTuck) * (1f - 0.55f * launchImpulse), 0f, 0f);
                targetLAnkleRot = new Vector3(Mathf.Lerp(12f, 26f, launchImpulse) - 10f * descendPrep, 0f, 0f);
                targetRAnkleRot = new Vector3(Mathf.Lerp(12f, 26f, launchImpulse) - 10f * descendPrep, 0f, 0f);

                if (!carrying)
                {
                    float armPitch = Mathf.Lerp(-26f, -64f, launchImpulse) + descendPrep * -18f;
                    float armOut = Mathf.Lerp(36f, 24f, launchImpulse) + descendPrep * 10f;
                    targetLShoulderRot = new Vector3(armPitch + airBalance, 0f, -armOut - _smoothedTurnLean * 0.8f);
                    targetRShoulderRot = new Vector3(armPitch - airBalance, 0f, armOut - _smoothedTurnLean * 0.8f);
                    targetLElbowRot = new Vector3(Mathf.Lerp(-38f, -26f, descendPrep), 0f, 0f);
                    targetRElbowRot = new Vector3(Mathf.Lerp(-38f, -26f, descendPrep), 0f, 0f);
                }
                targetSpineRot.x = Mathf.Lerp(8f, -4f, launchImpulse) + 6f * descendPrep;
            }
            // --- Jump landing squat (fast compression + smooth recovery scaled by fall impact) ---
            else if (_landSquatTimer >= 0f)
            {
                float squatNorm = Mathf.Clamp01(_landSquatTimer / Mathf.Max(0.15f, _landSquatDuration));
                float squatEnv = squatNorm < 0.28f
                    ? Mathf.SmoothStep(0f, 1f, squatNorm / 0.28f)
                    : Mathf.SmoothStep(1f, 0f, (squatNorm - 0.28f) / 0.72f);
                float impactScale = Mathf.Lerp(1f, 1.85f, _landImpact01);
                targetHipsPos.y -= 0.135f * impactScale * squatEnv;
                targetHipsPos.z -= 0.025f * impactScale * squatEnv;
                targetLHipRot.x -= 20f * impactScale * squatEnv;
                targetRHipRot.x -= 20f * impactScale * squatEnv;
                targetLKneeRot.x = Mathf.Max(targetLKneeRot.x, 38f * impactScale * squatEnv);
                targetRKneeRot.x = Mathf.Max(targetRKneeRot.x, 38f * impactScale * squatEnv);
                targetLAnkleRot.x -= 17f * squatEnv;
                targetRAnkleRot.x -= 17f * squatEnv;
                targetSpineRot.x += Mathf.Lerp(10f, 20f, _landImpact01) * squatEnv;
                if (!carrying)
                {
                    targetLShoulderRot.x = Mathf.Lerp(targetLShoulderRot.x, 12f, squatEnv * 0.55f);
                    targetRShoulderRot.x = Mathf.Lerp(targetRShoulderRot.x, 12f, squatEnv * 0.55f);
                    targetLShoulderRot.z -= 16f * squatEnv;
                    targetRShoulderRot.z += 16f * squatEnv;
                    targetLElbowRot.x = Mathf.Lerp(targetLElbowRot.x, -42f, squatEnv * 0.65f);
                    targetRElbowRot.x = Mathf.Lerp(targetRElbowRot.x, -42f, squatEnv * 0.65f);
                }
            }

            // --- v13 polish: arms flail and legs reach smoothly when falling from height ---
            if (!_grounded && _verticalVelocity < -3.5f && _airborneTimer > 0.30f)
            {
                float fallTimeRamp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_airborneTimer - 0.30f) / 0.22f));
                float fall = Mathf.Clamp01((-_verticalVelocity - 3.5f) / 6f) * fallTimeRamp;
                float flail = Mathf.Sin(_idleTime * 14f);
                if (!carrying)
                {
                    targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-95f + flail * 18f, 0f, -55f), fall);
                    targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-95f - flail * 18f, 0f, 55f), fall);
                    targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-20f, 0f, 0f), fall);
                    targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-20f, 0f, 0f), fall);
                }
                targetLHipRot = Vector3.Lerp(targetLHipRot, new Vector3(-10f - flail * 8f, 0f, -6f), fall);
                targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(-10f + flail * 8f, 0f, 6f), fall);
                targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(22f, 0f, 0f), fall);
                targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(22f, 0f, 0f), fall);
                targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -6f, fall);
            }

            float knockdownFootBlend = 1f;
            float startledStepW = 0f;
            float hitStaggerW = 0f;

            // --- Bed sleep lying-down override (Stand -> SitOnMattress -> Lie variant + breathing + stirring) ---
            if (_isLyingDown)
            {
                float e = _lyingBlend;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / 0.45f));
                float b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((e - 0.45f) / 0.55f));

                // Intermediate pose: sitting upright on the mattress, legs straight forward, hands beside hips
                Vector3 sitHipsPos = new Vector3(0f, 0.22f, 0f);
                Vector3 sitHipsRot = Vector3.zero;
                Vector3 sitSpine = new Vector3(-4f, 0f, 0f);
                Vector3 sitNeck = new Vector3(4f, 0f, 0f);
                Vector3 sitHead = new Vector3(6f, 0f, 0f);
                Vector3 sitLHip = new Vector3(-86f, 0f, -5f);
                Vector3 sitRHip = new Vector3(-86f, 0f, 5f);
                Vector3 sitLKnee = new Vector3(12f, 0f, 0f);
                Vector3 sitRKnee = new Vector3(12f, 0f, 0f);
                Vector3 sitLAnkle = new Vector3(10f, 0f, 0f);
                Vector3 sitRAnkle = new Vector3(10f, 0f, 0f);
                Vector3 sitLSh = new Vector3(14f, 0f, -14f);
                Vector3 sitRSh = new Vector3(14f, 0f, 14f);
                Vector3 sitLElb = new Vector3(-30f, 0f, 0f);
                Vector3 sitRElb = new Vector3(-30f, 0f, 0f);

                // Final lying pose (Variant 0 default: on back, hands resting on belly)
                Vector3 lieHipsPos = new Vector3(0f, 0.16f, 0f);
                Vector3 lieHipsRot = new Vector3(-90f, 0f, 0f);
                Vector3 lieSpine = new Vector3(2f, 0f, 0f);
                Vector3 lieNeck = new Vector3(6f, 0f, 0f);
                Vector3 lieHead = new Vector3(10f, 6f, 0f);
                Vector3 lieLSh = new Vector3(-22f, 0f, -6f);
                Vector3 lieRSh = new Vector3(-22f, 0f, 6f);
                Vector3 lieLElb = new Vector3(-78f, 0f, 14f);
                Vector3 lieRElb = new Vector3(-78f, 0f, -14f);
                Vector3 lieLHip = new Vector3(2f, 0f, -4f);
                Vector3 lieRHip = new Vector3(2f, 0f, 4f);
                Vector3 lieLKnee = new Vector3(6f, 0f, 0f);
                Vector3 lieRKnee = new Vector3(6f, 0f, 0f);
                Vector3 lieLAnkle = new Vector3(18f, 0f, 0f);
                Vector3 lieRAnkle = new Vector3(18f, 0f, 0f);

                if (_lyingVariant == 1)
                {
                    // On back, right knee raised, right hand resting behind head
                    lieRHip = new Vector3(-42f, 0f, 6f);
                    lieRKnee = new Vector3(78f, 0f, 0f);
                    lieRAnkle = new Vector3(-6f, 0f, 0f);
                    lieRSh = new Vector3(-158f, 0f, 28f);
                    lieRElb = new Vector3(-118f, 0f, 0f);
                    lieHead = new Vector3(8f, 14f, 0f);
                }
                else if (_lyingVariant == 2)
                {
                    // Slightly turned onto the right side, knees gently bent, left arm across body
                    lieHipsPos = new Vector3(0f, 0.19f, 0f);
                    lieHipsRot = new Vector3(-90f, 0f, 24f);
                    lieSpine = new Vector3(2f, 0f, 10f);
                    lieHead = new Vector3(8f, -10f, 6f);
                    lieLHip = new Vector3(-28f, 0f, -2f);
                    lieRHip = new Vector3(-20f, 0f, 4f);
                    lieLKnee = new Vector3(42f, 0f, 0f);
                    lieRKnee = new Vector3(34f, 0f, 0f);
                    lieLAnkle = new Vector3(14f, 0f, 0f);
                    lieRAnkle = new Vector3(14f, 0f, 0f);
                    lieLSh = new Vector3(-48f, 0f, -4f);
                    lieLElb = new Vector3(-62f, 0f, 10f);
                    lieRSh = new Vector3(-8f, 0f, 16f);
                    lieRElb = new Vector3(-24f, 0f, 0f);
                }

                targetHipsPos = Vector3.Lerp(Vector3.Lerp(targetHipsPos, sitHipsPos, a), lieHipsPos, b);
                targetHipsRot = Vector3.Lerp(Vector3.Lerp(targetHipsRot, sitHipsRot, a), lieHipsRot, b);
                targetSpineRot = Vector3.Lerp(Vector3.Lerp(targetSpineRot, sitSpine, a), lieSpine, b);
                targetNeckRot = Vector3.Lerp(Vector3.Lerp(targetNeckRot, sitNeck, a), lieNeck, b);
                targetHeadRot = Vector3.Lerp(Vector3.Lerp(targetHeadRot, sitHead, a), lieHead, b);
                targetLShoulderRot = Vector3.Lerp(Vector3.Lerp(targetLShoulderRot, sitLSh, a), lieLSh, b);
                targetRShoulderRot = Vector3.Lerp(Vector3.Lerp(targetRShoulderRot, sitRSh, a), lieRSh, b);
                targetLElbowRot = Vector3.Lerp(Vector3.Lerp(targetLElbowRot, sitLElb, a), lieLElb, b);
                targetRElbowRot = Vector3.Lerp(Vector3.Lerp(targetRElbowRot, sitRElb, a), lieRElb, b);
                targetLHipRot = Vector3.Lerp(Vector3.Lerp(targetLHipRot, sitLHip, a), lieLHip, b);
                targetRHipRot = Vector3.Lerp(Vector3.Lerp(targetRHipRot, sitRHip, a), lieRHip, b);
                targetLKneeRot = Vector3.Lerp(Vector3.Lerp(targetLKneeRot, sitLKnee, a), lieLKnee, b);
                targetRKneeRot = Vector3.Lerp(Vector3.Lerp(targetRKneeRot, sitRKnee, a), lieRKnee, b);
                targetLAnkleRot = Vector3.Lerp(Vector3.Lerp(targetLAnkleRot, sitLAnkle, a), lieLAnkle, b);
                targetRAnkleRot = Vector3.Lerp(Vector3.Lerp(targetRAnkleRot, sitRAnkle, a), lieRAnkle, b);

                // Slow sleep breathing scaled by lying stage
                float lieBreathArg = (_idleTime + _noiseSeed) * TwoPi * 0.22f;
                targetSpineRot.x += Mathf.Sin(lieBreathArg) * 1.8f * b;
                targetHeadRot.x += Mathf.Sin(lieBreathArg - 0.6f) * 0.8f * b;

                // Periodic gentle sleep stir (head turn, knee flex & subtle torso roll)
                if (_lyingStirTimer >= 0f)
                {
                    float s = Mathf.Clamp01(_lyingStirTimer / 1.8f);
                    float w = Mathf.Sin(s * Mathf.PI) * b;
                    targetHeadRot.y += 18f * _lyingStirSide * w;
                    if (_lyingStirSide < 0f)
                    {
                        targetLKneeRot.x += 16f * w;
                        targetLHipRot.x -= 10f * w;
                    }
                    else
                    {
                        targetRKneeRot.x += 16f * w;
                        targetRHipRot.x -= 10f * w;
                    }
                    targetSpineRot.z += 4f * _lyingStirSide * w;
                    _lyingStirTimer += dt;
                    if (_lyingStirTimer >= 1.8f)
                    {
                        _lyingStirTimer = -1f;
                    }
                }
            }
            // --- Prayer (Namaz / Salah) mathematically exact pose override (Tekbir -> Kıyam -> Rükû -> Kavme -> Secde -> Oturuş/Selam) ---
            else if (_prayerStage > 0)
            {
                if (_prayerStage == 1 || _prayerStage == 5 || _prayerStage == 6)
                {
                    // Standing phases: 5 = Tekbir (hands beside ears via IK), 1 = Kıyam (hands folded over abdomen via IK), 6 = Kavme (arms at sides)
                    targetHipsPos = new Vector3(0f, hipsBaseY, 0f);
                    targetHipsRot = Vector3.zero;
                    targetSpineRot = new Vector3(2f + idleChestPitch * 0.35f, 0f, 0f);
                    targetNeckRot = new Vector3(7f, 0f, 0f);
                    targetHeadRot = new Vector3(11f, 0f, 0f);
                    targetLShoulderRot = new Vector3(4f, 0f, -6f);
                    targetRShoulderRot = new Vector3(4f, 0f, 6f);
                    targetLElbowRot = new Vector3(-10f, 0f, 0f);
                    targetRElbowRot = new Vector3(-10f, 0f, 0f);
                    targetLHipRot = new Vector3(0f, 0f, -2f);
                    targetRHipRot = new Vector3(0f, 0f, 2f);
                    targetLKneeRot = new Vector3(2f, 0f, 0f);
                    targetRKneeRot = new Vector3(2f, 0f, 0f);
                    targetLAnkleRot = Vector3.zero;
                    targetRAnkleRot = Vector3.zero;
                }
                else if (_prayerStage == 2)
                {
                    // Rükû: crisp straight 88° horizontal back, head aligned with spine, palms placed directly on knees via IK
                    targetHipsPos = new Vector3(0f, hipsBaseY - 0.025f, -0.06f);
                    targetHipsRot = new Vector3(12f, 0f, 0f);
                    targetSpineRot = new Vector3(76f, 0f, 0f);
                    targetNeckRot = new Vector3(-4f, 0f, 0f);
                    targetHeadRot = new Vector3(-4f, 0f, 0f);
                    targetLShoulderRot = new Vector3(-78f, 0f, -6f);
                    targetRShoulderRot = new Vector3(-78f, 0f, 6f);
                    targetLElbowRot = new Vector3(-10f, 0f, 0f);
                    targetRElbowRot = new Vector3(-10f, 0f, 0f);
                    targetLHipRot = new Vector3(-12f, 0f, -2f);
                    targetRHipRot = new Vector3(-12f, 0f, 2f);
                    targetLKneeRot = new Vector3(3f, 0f, 0f);
                    targetRKneeRot = new Vector3(3f, 0f, 0f);
                    targetLAnkleRot = Vector3.zero;
                    targetRAnkleRot = Vector3.zero;
                }
                else if (_prayerStage == 3)
                {
                    // Secde: exact 7-point prostration — knees (y≈0.058), toes, forehead (y≈0.03), and both palms (via IK) resting cleanly on the carpet!
                    targetHipsPos = new Vector3(0f, 0.45f, -0.14f);
                    targetHipsRot = new Vector3(62f, 0f, 0f);
                    targetSpineRot = new Vector3(64f, 0f, 0f);
                    targetNeckRot = new Vector3(-10f, 0f, 0f);
                    targetHeadRot = new Vector3(-12f, 0f, 0f);
                    targetLShoulderRot = new Vector3(-105f, 0f, -18f);
                    targetRShoulderRot = new Vector3(-105f, 0f, 18f);
                    targetLElbowRot = new Vector3(-55f, 0f, 0f);
                    targetRElbowRot = new Vector3(-55f, 0f, 0f);
                    targetLHipRot = new Vector3(-92f, 0f, -5f);
                    targetRHipRot = new Vector3(-92f, 0f, 5f);
                    targetLKneeRot = new Vector3(122f, 0f, 0f);
                    targetRKneeRot = new Vector3(122f, 0f, 0f);
                    targetLAnkleRot = new Vector3(-62f, 0f, 0f);
                    targetRAnkleRot = new Vector3(-62f, 0f, 0f);
                }
                else
                {
                    // Oturuş / Celse / Tahiyyat & Selam: kneeling back on heels with knees & shins flat on carpet (y≈0.059), upright torso, palms on thighs via IK
                    targetHipsPos = new Vector3(0f, 0.24f, -0.18f);
                    targetHipsRot = new Vector3(8f, 0f, 0f);
                    targetSpineRot = new Vector3(-5f + idleChestPitch * 0.3f, 0f, 0f);
                    targetNeckRot = new Vector3(5f, _prayerSelamYaw * 0.38f, 0f);
                    targetHeadRot = new Vector3(9f, _prayerSelamYaw * 0.62f, 0f);
                    targetLShoulderRot = new Vector3(-28f, 0f, -6f);
                    targetRShoulderRot = new Vector3(-28f, 0f, 6f);
                    targetLElbowRot = new Vector3(-24f, 0f, 0f);
                    targetRElbowRot = new Vector3(-24f, 0f, 0f);
                    targetLHipRot = new Vector3(-76f, 0f, -5f);
                    targetRHipRot = new Vector3(-76f, 0f, 5f);
                    targetLKneeRot = new Vector3(158f, 0f, 0f);
                    targetRKneeRot = new Vector3(158f, 0f, 0f);
                    targetLAnkleRot = new Vector3(56f, 0f, 0f);
                    targetRAnkleRot = new Vector3(56f, 0f, 0f);
                }
            }
            // --- Swimming override (Freestyle front-crawl or Breaststroke Kurbağalama!) ---
            else if (_isSwimming)
            {
                float swimBlend = Mathf.Clamp01(_swimSpeed / 1.8f);
                _swimPhase += dt * Mathf.Lerp(2.8f, _swimBreaststroke ? 4.8f : 6.4f, swimBlend);

                if (_swimBreaststroke)
                {
                    float pull = Mathf.Sin(_swimPhase);
                    float sweep = Mathf.Cos(_swimPhase);
                    targetHipsPos = new Vector3(0f, Mathf.Lerp(0.56f, 0.70f, swimBlend) + pull * 0.06f, 0f);
                    targetHipsRot = new Vector3(Mathf.Lerp(18f, 68f, swimBlend) - pull * 8f, 0f, 0f);
                    targetSpineRot = new Vector3(Mathf.Lerp(-8f, -14f - pull * 10f, swimBlend), 0f, 0f);
                    targetNeckRot = new Vector3(Mathf.Lerp(-8f, -34f, swimBlend), 0f, 0f);
                    targetHeadRot = new Vector3(Mathf.Lerp(-6f, -18f, swimBlend), 0f, 0f);

                    float armPitch = Mathf.Lerp(-55f, -142f + pull * 42f, swimBlend);
                    float armSpread = Mathf.Lerp(26f, 18f + Mathf.Max(0f, sweep) * 58f, swimBlend);
                    targetLShoulderRot = new Vector3(armPitch, 0f, -armSpread);
                    targetRShoulderRot = new Vector3(armPitch, 0f, armSpread);
                    targetLElbowRot = new Vector3(Mathf.Lerp(-44f, -22f - Mathf.Max(0f, -pull) * 48f, swimBlend), 0f, 0f);
                    targetRElbowRot = new Vector3(Mathf.Lerp(-44f, -22f - Mathf.Max(0f, -pull) * 48f, swimBlend), 0f, 0f);

                    // Frog kick synchronized with glide!
                    float frog = Mathf.Max(0f, -pull);
                    targetLHipRot = new Vector3(Mathf.Lerp(-14f, -28f * frog + 12f, swimBlend), -18f * frog, -10f - 18f * frog);
                    targetRHipRot = new Vector3(Mathf.Lerp(-14f, -28f * frog + 12f, swimBlend), 18f * frog, 10f + 18f * frog);
                    targetLKneeRot = new Vector3(Mathf.Lerp(32f, 16f + frog * 74f, swimBlend), 0f, 0f);
                    targetRKneeRot = new Vector3(Mathf.Lerp(32f, 16f + frog * 74f, swimBlend), 0f, 0f);
                    targetLAnkleRot = new Vector3(24f, 0f, 0f);
                    targetRAnkleRot = new Vector3(24f, 0f, 0f);
                }
                else
                {
                    float strokeL = Mathf.Sin(_swimPhase);
                    float strokeR = Mathf.Sin(_swimPhase + Mathf.PI);

                    targetHipsPos = new Vector3(0f, Mathf.Lerp(0.56f, 0.72f, swimBlend) + Mathf.Sin(_swimPhase * 0.5f) * 0.04f, 0f);
                    targetHipsRot = new Vector3(Mathf.Lerp(18f, 74f, swimBlend), 0f, strokeL * 14f * swimBlend);
                    targetSpineRot = new Vector3(Mathf.Lerp(-8f, -18f, swimBlend), -strokeL * 18f * swimBlend, 0f);
                    targetNeckRot = new Vector3(Mathf.Lerp(-8f, -38f, swimBlend), 0f, 0f);
                    targetHeadRot = new Vector3(Mathf.Lerp(-6f, -20f, swimBlend), 0f, 0f);

                    targetLShoulderRot = new Vector3(Mathf.Lerp(-55f, -155f + strokeL * 78f, swimBlend), 0f, Mathf.Lerp(-36f + strokeL * 24f, -22f, swimBlend));
                    targetRShoulderRot = new Vector3(Mathf.Lerp(-55f, -155f + strokeR * 78f, swimBlend), 0f, Mathf.Lerp(36f - strokeR * 24f, 22f, swimBlend));
                    targetLElbowRot = new Vector3(Mathf.Lerp(-48f, -28f - Mathf.Max(0f, strokeL) * 45f, swimBlend), 0f, 0f);
                    targetRElbowRot = new Vector3(Mathf.Lerp(-48f, -28f - Mathf.Max(0f, strokeR) * 45f, swimBlend), 0f, 0f);

                    float kickL = Mathf.Sin(_swimPhase * 2.2f);
                    float kickR = Mathf.Sin(_swimPhase * 2.2f + Mathf.PI);
                    targetLHipRot = new Vector3(Mathf.Lerp(-16f, 10f + kickL * 22f, swimBlend), 0f, -5f);
                    targetRHipRot = new Vector3(Mathf.Lerp(-16f, 10f + kickR * 22f, swimBlend), 0f, 5f);
                    targetLKneeRot = new Vector3(Mathf.Lerp(36f, 18f + Mathf.Max(0f, -kickL) * 26f, swimBlend), 0f, 0f);
                    targetRKneeRot = new Vector3(Mathf.Lerp(36f, 18f + Mathf.Max(0f, -kickR) * 26f, swimBlend), 0f, 0f);
                    targetLAnkleRot = new Vector3(28f, 0f, 0f);
                    targetRAnkleRot = new Vector3(28f, 0f, 0f);
                }
            }
            // --- Parkour Vault hurdle animation (0.42 s) ---
            else if (_vaultTimer >= 0f)
            {
                float vNorm = Mathf.Clamp01(_vaultTimer / VaultDuration);
                float vEnv = Mathf.Sin(vNorm * Mathf.PI);
                targetHipsPos.y = Mathf.Lerp(targetHipsPos.y, hipsBaseY - 0.11f, vEnv);
                targetSpineRot = Vector3.Lerp(targetSpineRot, new Vector3(49f, -12f, 14f), vEnv);
                targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-86f, 12f, -16f), vEnv);
                targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-72f, -12f, 24f), vEnv);
                targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-24f, 0f, 0f), vEnv);
                targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-34f, 0f, 0f), vEnv);
                float leadingLeg = Mathf.Sin(Mathf.Clamp01(vNorm * 1.24f) * Mathf.PI);
                float trailingLeg = Mathf.Sin(Mathf.Clamp01((vNorm - 0.15f) * 1.17f) * Mathf.PI);
                targetLHipRot = Vector3.Lerp(targetLHipRot, new Vector3(-89f, 0f, -14f), leadingLeg);
                targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(-72f, 0f, 18f), trailingLeg);
                targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(105f, 0f, 0f), vEnv);
                targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(95f, 0f, 0f), vEnv);
            }
            // --- Full NPC Knockdown onto back & multi-stage recovery (Fall -> Lie -> Sit-Up -> Kneel -> Stand) ---
            else if (_knockdownTimer >= 0f)
            {
                float totalDur = Mathf.Max(1.2f, _activeKnockdownDuration);
                float fallDur = Mathf.Min(0.36f, totalDur * 0.22f);
                float getUpDur = Mathf.Min(1.26f, totalDur * 0.52f);
                float getUpStart = totalDur - getUpDur;

                // Stand baseline captured before override so final stand stage converges seamlessly to standing pose
                Vector3 standHipsPos = targetHipsPos;
                Vector3 standHipsRot = targetHipsRot;
                Vector3 standSpine = targetSpineRot;
                Vector3 standNeck = targetNeckRot;
                Vector3 standHead = targetHeadRot;
                Vector3 standLSh = targetLShoulderRot;
                Vector3 standRSh = targetRShoulderRot;
                Vector3 standLElb = targetLElbowRot;
                Vector3 standRElb = targetRElbowRot;
                Vector3 standLHip = targetLHipRot;
                Vector3 standRHip = targetRHipRot;
                Vector3 standLKnee = targetLKneeRot;
                Vector3 standRKnee = targetRKneeRot;
                Vector3 standLAnkle = targetLAnkleRot;
                Vector3 standRAnkle = targetRAnkleRot;

                // Keyframe 1: Supine knockdown on back
                float lieBreath = Mathf.Sin((_idleTime + _noiseSeed) * 3.2f) * 2.2f;
                Vector3 lieHipsPos = new Vector3(0f, 0.20f, -0.18f);
                Vector3 lieHipsRot = new Vector3(-86f, 8f, 6f);
                Vector3 lieSpine = new Vector3(10f + lieBreath, 0f, 0f);
                Vector3 lieNeck = new Vector3(8f, 0f, 0f);
                Vector3 lieHead = new Vector3(16f, -14f, 0f);
                Vector3 lieLSh = new Vector3(-25f, 0f, -62f);
                Vector3 lieRSh = new Vector3(-18f, 0f, 58f);
                Vector3 lieLElb = new Vector3(-28f, 0f, 0f);
                Vector3 lieRElb = new Vector3(-34f, 0f, 0f);
                Vector3 lieLHip = new Vector3(6f, 0f, -14f);
                Vector3 lieRHip = new Vector3(-12f, 0f, 16f);
                Vector3 lieLKnee = new Vector3(18f, 0f, 0f);
                Vector3 lieRKnee = new Vector3(32f, 0f, 0f);
                Vector3 lieLAnkle = new Vector3(18f, 0f, 0f);
                Vector3 lieRAnkle = new Vector3(18f, 0f, 0f);

                // Keyframe 2: Sit-up on ground with knees bent & hands pushing off floor
                Vector3 sitHipsPos = new Vector3(0f, 0.21f, -0.06f);
                Vector3 sitHipsRot = new Vector3(-6f, 4f, 0f);
                Vector3 sitSpine = new Vector3(20f, -4f, 0f);
                Vector3 sitNeck = new Vector3(4f, 0f, 0f);
                Vector3 sitHead = new Vector3(14f, 0f, 0f);
                Vector3 sitLSh = new Vector3(26f, 0f, -24f);
                Vector3 sitRSh = new Vector3(22f, 0f, 26f);
                Vector3 sitLElb = new Vector3(-26f, 0f, 0f);
                Vector3 sitRElb = new Vector3(-26f, 0f, 0f);
                Vector3 sitLHip = new Vector3(-126f, 0f, -8f);
                Vector3 sitRHip = new Vector3(-118f, 0f, 10f);
                Vector3 sitLKnee = new Vector3(106f, 0f, 0f);
                Vector3 sitRKnee = new Vector3(102f, 0f, 0f);
                Vector3 sitLAnkle = new Vector3(-8f, 0f, 0f);
                Vector3 sitRAnkle = new Vector3(-4f, 0f, 0f);

                // Keyframe 3: One-knee kneel (left foot planted forward, right knee down, left hand bracing on knee)
                Vector3 kneelHipsPos = new Vector3(0f, 0.57f, 0.04f);
                Vector3 kneelHipsRot = new Vector3(6f, -6f, 0f);
                Vector3 kneelSpine = new Vector3(24f, 6f, 0f);
                Vector3 kneelNeck = new Vector3(-6f, 0f, 0f);
                Vector3 kneelHead = new Vector3(10f, 0f, 0f);
                Vector3 kneelLSh = new Vector3(-48f, 12f, -10f);
                Vector3 kneelRSh = new Vector3(-26f, 0f, 22f);
                Vector3 kneelLElb = new Vector3(-44f, 0f, 0f);
                Vector3 kneelRElb = new Vector3(-38f, 0f, 0f);
                Vector3 kneelLHip = new Vector3(-82f, 0f, -6f);
                Vector3 kneelRHip = new Vector3(12f, 0f, 6f);
                Vector3 kneelLKnee = new Vector3(82f, 0f, 0f);
                Vector3 kneelRKnee = new Vector3(86f, 0f, 0f);
                Vector3 kneelLAnkle = new Vector3(-6f, 0f, 0f);
                Vector3 kneelRAnkle = new Vector3(48f, 0f, 0f);

                if (_knockdownTimer < fallDur)
                {
                    knockdownFootBlend = 0f;
                    float fallU = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_knockdownTimer / fallDur));
                    float bounce = Mathf.Sin(Mathf.Clamp01((_knockdownTimer / fallDur - 0.72f) / 0.28f) * Mathf.PI) * 0.03f;
                    targetHipsPos = Vector3.Lerp(standHipsPos, lieHipsPos + Vector3.up * bounce, fallU);
                    targetHipsRot = Vector3.Lerp(standHipsRot, lieHipsRot, fallU);
                    targetSpineRot = Vector3.Lerp(standSpine, lieSpine, fallU);
                    targetNeckRot = Vector3.Lerp(standNeck, lieNeck, fallU);
                    targetHeadRot = Vector3.Lerp(standHead, lieHead, fallU);
                    targetLShoulderRot = Vector3.Lerp(standLSh, lieLSh, fallU);
                    targetRShoulderRot = Vector3.Lerp(standRSh, lieRSh, fallU);
                    targetLElbowRot = Vector3.Lerp(standLElb, lieLElb, fallU);
                    targetRElbowRot = Vector3.Lerp(standRElb, lieRElb, fallU);
                    targetLHipRot = Vector3.Lerp(standLHip, lieLHip, fallU);
                    targetRHipRot = Vector3.Lerp(standRHip, lieRHip, fallU);
                    targetLKneeRot = Vector3.Lerp(standLKnee, lieLKnee, fallU);
                    targetRKneeRot = Vector3.Lerp(standRKnee, lieRKnee, fallU);
                    targetLAnkleRot = Vector3.Lerp(standLAnkle, lieLAnkle, fallU);
                    targetRAnkleRot = Vector3.Lerp(standRAnkle, lieRAnkle, fallU);
                }
                else if (_knockdownTimer < getUpStart)
                {
                    knockdownFootBlend = 0f;
                    targetHipsPos = lieHipsPos;
                    targetHipsRot = lieHipsRot;
                    targetSpineRot = lieSpine;
                    targetNeckRot = lieNeck;
                    targetHeadRot = lieHead;
                    targetLShoulderRot = lieLSh;
                    targetRShoulderRot = lieRSh;
                    targetLElbowRot = lieLElb;
                    targetRElbowRot = lieRElb;
                    targetLHipRot = lieLHip;
                    targetRHipRot = lieRHip;
                    targetLKneeRot = lieLKnee;
                    targetRKneeRot = lieRKnee;
                    targetLAnkleRot = lieLAnkle;
                    targetRAnkleRot = lieRAnkle;
                }
                else
                {
                    float gNorm = Mathf.Clamp01((_knockdownTimer - getUpStart) / Mathf.Max(0.25f, getUpDur));
                    if (gNorm < 0.34f)
                    {
                        // Stage A: Lie -> Sit-Up
                        knockdownFootBlend = 0f;
                        float u = Mathf.SmoothStep(0f, 1f, gNorm / 0.34f);
                        targetHipsPos = Vector3.Lerp(lieHipsPos, sitHipsPos, u);
                        targetHipsRot = Vector3.Lerp(lieHipsRot, sitHipsRot, u);
                        targetSpineRot = Vector3.Lerp(lieSpine, sitSpine, u);
                        targetNeckRot = Vector3.Lerp(lieNeck, sitNeck, u);
                        targetHeadRot = Vector3.Lerp(lieHead, sitHead, u);
                        targetLShoulderRot = Vector3.Lerp(lieLSh, sitLSh, u);
                        targetRShoulderRot = Vector3.Lerp(lieRSh, sitRSh, u);
                        targetLElbowRot = Vector3.Lerp(lieLElb, sitLElb, u);
                        targetRElbowRot = Vector3.Lerp(lieRElb, sitRElb, u);
                        targetLHipRot = Vector3.Lerp(lieLHip, sitLHip, u);
                        targetRHipRot = Vector3.Lerp(lieRHip, sitRHip, u);
                        targetLKneeRot = Vector3.Lerp(lieLKnee, sitLKnee, u);
                        targetRKneeRot = Vector3.Lerp(lieRKnee, sitRKnee, u);
                        targetLAnkleRot = Vector3.Lerp(lieLAnkle, sitLAnkle, u);
                        targetRAnkleRot = Vector3.Lerp(lieRAnkle, sitRAnkle, u);
                    }
                    else if (gNorm < 0.68f)
                    {
                        // Stage B: Sit-Up -> One-Knee Kneel
                        knockdownFootBlend = 0f;
                        float u = Mathf.SmoothStep(0f, 1f, (gNorm - 0.34f) / 0.34f);
                        targetHipsPos = Vector3.Lerp(sitHipsPos, kneelHipsPos, u);
                        targetHipsRot = Vector3.Lerp(sitHipsRot, kneelHipsRot, u);
                        targetSpineRot = Vector3.Lerp(sitSpine, kneelSpine, u);
                        targetNeckRot = Vector3.Lerp(sitNeck, kneelNeck, u);
                        targetHeadRot = Vector3.Lerp(sitHead, kneelHead, u);
                        targetLShoulderRot = Vector3.Lerp(sitLSh, kneelLSh, u);
                        targetRShoulderRot = Vector3.Lerp(sitRSh, kneelRSh, u);
                        targetLElbowRot = Vector3.Lerp(sitLElb, kneelLElb, u);
                        targetRElbowRot = Vector3.Lerp(sitRElb, kneelRElb, u);
                        targetLHipRot = Vector3.Lerp(sitLHip, kneelLHip, u);
                        targetRHipRot = Vector3.Lerp(sitRHip, kneelRHip, u);
                        targetLKneeRot = Vector3.Lerp(sitLKnee, kneelLKnee, u);
                        targetRKneeRot = Vector3.Lerp(sitRKnee, kneelRKnee, u);
                        targetLAnkleRot = Vector3.Lerp(sitLAnkle, kneelLAnkle, u);
                        targetRAnkleRot = Vector3.Lerp(sitRAnkle, kneelRAnkle, u);
                    }
                    else
                    {
                        // Stage C: One-Knee Kneel -> Stand-Up with smooth PlantFeet handoff (zero end pop)
                        float standU = Mathf.Clamp01((gNorm - 0.68f) / 0.32f);
                        float u = Mathf.SmoothStep(0f, 1f, standU);
                        knockdownFootBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((standU - 0.28f) / 0.72f));
                        float riseArch = Mathf.Sin(u * Mathf.PI);
                        targetHipsPos = Vector3.Lerp(kneelHipsPos, standHipsPos, u);
                        targetHipsRot = Vector3.Lerp(kneelHipsRot, standHipsRot, u);
                        targetSpineRot = Vector3.Lerp(kneelSpine, standSpine, u) + new Vector3(6f * riseArch, 0f, 0f);
                        targetNeckRot = Vector3.Lerp(kneelNeck, standNeck, u);
                        targetHeadRot = Vector3.Lerp(kneelHead, standHead, u);
                        targetLShoulderRot = Vector3.Lerp(kneelLSh, standLSh, u);
                        targetRShoulderRot = Vector3.Lerp(kneelRSh, standRSh, u);
                        targetLElbowRot = Vector3.Lerp(kneelLElb, standLElb, u);
                        targetRElbowRot = Vector3.Lerp(kneelRElb, standRElb, u);
                        targetLHipRot = Vector3.Lerp(kneelLHip, standLHip, u);
                        targetRHipRot = Vector3.Lerp(kneelRHip, standRHip, u);
                        targetLKneeRot = Vector3.Lerp(kneelLKnee, standLKnee, u);
                        targetRKneeRot = Vector3.Lerp(kneelRKnee, standRKnee, u);
                        targetLAnkleRot = Vector3.Lerp(kneelLAnkle, standLAnkle, u);
                        targetRAnkleRot = Vector3.Lerp(kneelRAnkle, standRAnkle, u);
                    }
                }
            }
            // --- Hit recoil stagger animation (crisp impact snap -> weighted stagger -> smooth recovery) ---
            else if (_hitReactTimer >= 0f)
            {
                float hNorm = Mathf.Clamp01(_hitReactTimer / HitReactDuration);
                float hEnv = hNorm < 0.22f
                    ? Mathf.SmoothStep(0f, 1f, hNorm / 0.22f)
                    : Mathf.SmoothStep(1f, 0f, (hNorm - 0.22f) / 0.78f);
                hitStaggerW = hEnv;
                targetHipsPos.y -= 0.095f * hEnv;
                targetHipsPos.z -= 0.075f * hEnv;
                targetHipsRot.y += 10f * hEnv;
                targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -26f, hEnv);
                targetSpineRot.y = Mathf.Lerp(targetSpineRot.y, 22f, hEnv);
                targetHeadRot.x = Mathf.Lerp(targetHeadRot.x, -24f, hEnv);
                if (!carrying)
                {
                    targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-78f, 0f, -40f), hEnv);
                    targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-70f, 0f, 38f), hEnv);
                    targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-52f, 0f, 0f), hEnv);
                    targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-56f, 0f, 0f), hEnv);
                }
            }
            // --- 3-Hit Weighted Street Combat Combo: 0 = Left Jab, 1 = Heavy Right Hook, 2 = Front Push Kick ---
            else if (_punchTimer >= 0f)
            {
                float dur = _combatMoveIndex == 2 ? KickDuration : PunchDuration;
                float pNorm = Mathf.Clamp01(_punchTimer / dur);
                float thrust = pNorm < 0.25f
                    ? -(pNorm / 0.25f) * 0.45f
                    : (pNorm < 0.58f
                        ? Mathf.Lerp(-0.45f, 1.0f, (pNorm - 0.25f) / 0.33f)
                        : Mathf.Lerp(1.0f, 0f, (pNorm - 0.58f) / 0.42f));
                float tPos = Mathf.Clamp01(thrust);
                float guardEnv = Mathf.Sin(pNorm * Mathf.PI);

                if (_combatMoveIndex == 0)
                {
                    // Hit 1: Snappy Left Jab
                    targetSpineRot.y += 24f * thrust;
                    targetSpineRot.x += 10f * tPos;
                    targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-90f, 10f, -6f), tPos);
                    targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-8f, 0f, 0f), tPos);
                    targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-52f, -18f, 16f), guardEnv);
                    targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-92f, 0f, 0f), guardEnv);
                }
                else if (_combatMoveIndex == 1)
                {
                    // Hit 2: Heavy Right Hook / Cross with big torso torque
                    targetSpineRot.y += -38f * thrust;
                    targetSpineRot.x += 15f * tPos;
                    targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-86f, -34f, 22f), tPos);
                    targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-36f, 0f, 0f), tPos);
                    targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-54f, 20f, -18f), guardEnv);
                    targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-96f, 0f, 0f), guardEnv);
                }
                else
                {
                    // Hit 3: Powerful Front Push Kick!
                    targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -22f, tPos);
                    targetSpineRot.y += -16f * tPos;
                    targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(-98f, 0f, 6f), tPos);
                    targetRKneeRot = Vector3.Lerp(targetRKneeRot, pNorm < 0.34f ? new Vector3(88f, 0f, 0f) : new Vector3(8f, 0f, 0f), guardEnv);
                    targetRAnkleRot = Vector3.Lerp(targetRAnkleRot, new Vector3(24f, 0f, 0f), tPos);
                    targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-42f, 0f, -34f), guardEnv);
                    targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(26f, 0f, 28f), guardEnv);
                }
            }
            // --- Startled / vehicle-dodge pose (step & lean away from threat side with defensive arms) ---
            else if (_startledTimer >= 0f)
            {
                float sNorm = Mathf.Clamp01(_startledTimer / _startledDuration);
                float sEnv = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sNorm / 0.14f)) *
                             (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sNorm - 0.52f) / 0.48f)));

                if (_speedMs > 0.35f && Mathf.Abs(localTravel.x) > 0.35f)
                {
                    _startledDodgeSide = Mathf.Sign(localTravel.x);
                    _startledHasThreat = true;
                }

                float dodgeSide = _startledDodgeSide;
                float lateralScale = _startledHasThreat ? 1f : 0.45f;
                startledStepW = (_grounded && _speedMs < 0.35f) ? (sEnv * lateralScale) : 0f;

                // Lean and shift away from the approaching vehicle while turning gaze toward it
                targetHipsPos.x += dodgeSide * 0.13f * sEnv * lateralScale;
                targetHipsPos.y -= 0.095f * sEnv;
                targetHipsPos.z -= 0.06f * sEnv;
                targetHipsRot.z -= dodgeSide * 6f * sEnv * lateralScale;
                targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -10f, sEnv);
                targetSpineRot.y += dodgeSide * 18f * sEnv * lateralScale;
                targetSpineRot.z -= dodgeSide * 17f * sEnv * lateralScale;
                targetHeadRot.y -= dodgeSide * 24f * sEnv * lateralScale;
                targetHeadRot.x = Mathf.Lerp(targetHeadRot.x, -6f, sEnv);

                if (!carrying)
                {
                    // Near-side arm shields toward threat; far-side arm reaches out toward dodge side for balance
                    Vector3 shieldSh = new Vector3(-112f, dodgeSide * 16f, -dodgeSide * 26f);
                    Vector3 shieldElb = new Vector3(-68f, 0f, 0f);
                    Vector3 balanceSh = new Vector3(-76f, -dodgeSide * 10f, dodgeSide * 48f);
                    Vector3 balanceElb = new Vector3(-40f, 0f, 0f);

                    if (dodgeSide > 0f)
                    {
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, shieldSh, sEnv);
                        targetLElbowRot = Vector3.Lerp(targetLElbowRot, shieldElb, sEnv);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, balanceSh, sEnv);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, balanceElb, sEnv);
                    }
                    else
                    {
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, shieldSh, sEnv);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, shieldElb, sEnv);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, balanceSh, sEnv);
                        targetLElbowRot = Vector3.Lerp(targetLElbowRot, balanceElb, sEnv);
                    }
                }
            }
            // --- One-shot right-arm wave overlay (~1.5 s) ---
            else if (_waveTimer >= 0f && !carrying && !petting && _consumeWeight < 0.05f && !_hasShelfReach && !_hasDualAction)
            {
                float norm = _waveTimer / WaveDuration;
                float env = Mathf.SmoothStep(0f, 1f, norm / 0.24f) *
                    (1f - Mathf.SmoothStep(0f, 1f, (norm - 0.73f) / 0.27f));
                float waveOsc = Mathf.Sin(_waveTimer * 15f) * 20f * env;

                Vector3 waveShoulder = new Vector3(-42f, 10f, 118f + waveOsc * 0.22f);
                Vector3 waveElbow = new Vector3(-26f, 0f, 50f + waveOsc);
                targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, waveShoulder, env);
                targetRElbowRot = Vector3.Lerp(targetRElbowRot, waveElbow, env);
                targetSpineRot.z -= 4f * env;
                targetHeadRot.y = Mathf.Lerp(targetHeadRot.y, 10f, env * 0.55f);
                targetHeadRot.z += 5f * env;
            }

            if (petting)
            {
                // Kneel drop is handled cleanly by _kneelBlend; keep spine gently leaned & head warmly tilted toward cat
                targetHipsPos.y -= 0.05f * petCrouch;
                targetHipsPos.z -= 0.03f * petCrouch;
                targetSpineRot = Vector3.Lerp(targetSpineRot, new Vector3(28f, -8f, -4f), petCrouch);
                targetNeckRot.x = Mathf.Lerp(targetNeckRot.x, 12f, petCrouch);
                targetHeadRot.x = Mathf.Lerp(targetHeadRot.x, 14f, petCrouch);
                targetHeadRot.z += 7f * petCrouch;
            }
            if (carrying)
            {
                float crouch = carry.PickupCrouch;
                float heaviness = carry.CarryHeaviness;
                // Squat lift with hips/knees (back stays straighter ~30 deg instead of hunched 53 deg)
                // and counter-lean backward while carrying heavier items.
                float squatDrop = Mathf.Lerp(0.38f, 0.48f, heaviness);
                float liftSpineLean = Mathf.Lerp(32f, 26f, heaviness);
                float carryCounterLean = carry.IsAttached ? -Mathf.Lerp(3.5f, 10.5f, heaviness) : 2f;

                targetHipsPos.y -= squatDrop * crouch;
                targetHipsPos.z -= 0.095f * crouch;
                targetSpineRot.x = Mathf.Lerp(targetSpineRot.x * 0.45f + carryCounterLean, liftSpineLean, crouch);
                targetSpineRot.y *= Mathf.Lerp(0.35f, 0.18f, heaviness);
                targetHeadRot.x += Mathf.Lerp(10f, 16f, heaviness) * crouch;

                if (carry.IsThrowing)
                {
                    float tp = carry.ThrowProgress;
                    float wind = tp < 0.64f
                        ? Mathf.SmoothStep(0f, 1f, tp / 0.64f)
                        : (1f - Mathf.SmoothStep(0f, 1f, (tp - 0.64f) / 0.36f));
                    float heave = tp >= 0.58f ? Mathf.SmoothStep(0f, 1f, (tp - 0.58f) / 0.42f) : 0f;
                    targetSpineRot.x += -14f * wind + 20f * heave;
                    targetHipsPos.z += -0.05f * wind + 0.07f * heave;
                }
            }
            else if (_carryReleaseTimer >= 0f)
            {
                float relU = Mathf.Clamp01(_carryReleaseTimer / CarryReleaseFadeDuration);
                float relEnv = 1f - Mathf.SmoothStep(0f, 1f, relU);
                targetHipsPos.y -= 0.42f * _carryReleaseCrouch * relEnv;
                targetHipsPos.z -= 0.08f * _carryReleaseCrouch * relEnv;
                targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, 26f, _carryReleaseCrouch * relEnv);
            }
            else if (_carryThrowTimer >= 0f)
            {
                float fNorm = Mathf.Clamp01(_carryThrowTimer / CarryThrowFollowDuration);
                float fEnv = 1f - Mathf.SmoothStep(0f, 1f, fNorm);
                targetSpineRot.x += Mathf.Lerp(14f, 22f, _carryThrowHeaviness) * fEnv;
                targetHipsPos.z += 0.05f * fEnv;
                targetHipsPos.y -= 0.04f * fEnv;
            }

            // --- Dedicated Full-Body Sports Action Animations (Soccer, Basketball, Volleyball, Celebration) ---
            if (_sportAnimTimer >= 0f && _sportAnimKind != SportAnimKind.None)
            {
                float sn = Mathf.Clamp01(_sportAnimTimer / Mathf.Max(0.1f, _sportAnimDuration));
                float env = Mathf.Sin(sn * Mathf.PI);

                switch (_sportAnimKind)
                {
                    case SportAnimKind.SoccerKick:
                    {
                        // Authentic windup -> explosive right-foot strike -> follow-through (continuous curves, zero snap)
                        float strikeBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.24f) / 0.16f));
                        float cock = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sn / 0.26f)) * (1f - strikeBlend);
                        float whip = Mathf.Sin(Mathf.Clamp01((sn - 0.22f) / 0.78f) * Mathf.PI);
                        targetHipsPos.y -= 0.06f * env;
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, Mathf.Lerp(8f, -16f, strikeBlend), env);
                        targetSpineRot.y += 22f * cock - 28f * whip;
                        targetSpineRot.z += -12f * whip;
                        // Planted left leg bends naturally, right leg cocks far back then whips through ball
                        targetLHipRot = Vector3.Lerp(targetLHipRot, new Vector3(-18f, 0f, -6f), env);
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(24f, 0f, 0f), env);
                        float kickHip = Mathf.Lerp(46f * cock, -78f, whip);
                        float kneeExtend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.34f) / 0.26f));
                        float kickKnee = Mathf.Lerp(Mathf.Lerp(14f, 76f, cock), Mathf.Lerp(76f, 12f, kneeExtend), strikeBlend);
                        targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(kickHip, 0f, 8f), env);
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(kickKnee, 0f, 0f), env);
                        targetRAnkleRot = Vector3.Lerp(targetRAnkleRot, new Vector3(28f, 0f, 0f), whip);
                        // Counter-balancing arms spread athletically
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-48f, 0f, -42f), env);
                        targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-36f, 0f, 0f), env);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(32f, 0f, 34f), env);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-28f, 0f, 0f), env);
                        break;
                    }
                    case SportAnimKind.SoccerSteal:
                    {
                        // Low forward poke-tackle lunge
                        targetHipsPos.y -= 0.14f * env;
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, 22f, env);
                        targetSpineRot.y += -24f * env;
                        targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(-62f, 0f, 10f), env);
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(14f, 0f, 0f), env);
                        targetLHipRot = Vector3.Lerp(targetLHipRot, new Vector3(26f, 0f, -6f), env);
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(32f, 0f, 0f), env);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-28f, 0f, -46f), env);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(24f, 0f, 40f), env);
                        break;
                    }
                    case SportAnimKind.BasketballJumpShot:
                    {
                        // Squat -> explosive 0.56 m jump -> high 2-hand overhead shot release & smooth wrist extension
                        float dip = sn < 0.24f ? Mathf.Sin((sn / 0.24f) * Mathf.PI) : 0f;
                        float air = sn >= 0.16f ? Mathf.Sin(Mathf.Clamp01((sn - 0.16f) / 0.84f) * Mathf.PI) : 0f;
                        float riseBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.14f) / 0.16f));
                        float release = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.20f) / 0.26f)) *
                                        (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.80f) / 0.20f)));
                        float shootExtend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.34f) / 0.20f));
                        targetHipsPos.y += air * 0.56f - dip * 0.14f;
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, Mathf.Lerp(14f, -10f, riseBlend), Mathf.Max(dip, air));
                        targetLHipRot = Vector3.Lerp(targetLHipRot, new Vector3(-18f * air + 16f * dip, 0f, -5f), Mathf.Max(dip, air));
                        targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(-14f * air + 16f * dip, 0f, 5f), Mathf.Max(dip, air));
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(26f * air + 36f * dip, 0f, 0f), Mathf.Max(dip, air));
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(22f * air + 36f * dip, 0f, 0f), Mathf.Max(dip, air));
                        // Right shooting arm extends straight overhead, left guide hand supports cleanly
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-164f, 0f, 12f), release);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(Mathf.Lerp(-54f, -10f, shootExtend), 0f, 0f), release);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-146f, 14f, -12f), release);
                        targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(Mathf.Lerp(-44f, -26f, shootExtend), 0f, 0f), release);
                        break;
                    }
                    case SportAnimKind.BasketballPass:
                    {
                        // Snappy two-hand chest pass push with smooth elbow extension
                        float pushExtend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.22f) / 0.24f));
                        targetHipsPos.y -= 0.06f * env;
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, 14f, env);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-84f, 16f, -8f), env);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-84f, -16f, 8f), env);
                        float passElbow = Mathf.Lerp(-74f, -8f, pushExtend);
                        targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(passElbow, 0f, 0f), env);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(passElbow, 0f, 0f), env);
                        break;
                    }
                    case SportAnimKind.VolleyballBump:
                    {
                        // Deep athletic knee crouch -> smooth upward forearm dig platform!
                        float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.25f) / 0.45f));
                        targetHipsPos.y -= Mathf.Lerp(0.18f, 0.04f, lift) * env;
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, Mathf.Lerp(26f, 10f, lift), env);
                        targetLHipRot = Vector3.Lerp(targetLHipRot, new Vector3(-28f, 0f, -10f), env);
                        targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(-28f, 0f, 10f), env);
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(48f, 0f, 0f), env);
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(48f, 0f, 0f), env);
                        float armPitch = Mathf.Lerp(-52f, -94f, lift);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(armPitch, 24f, -4f), env);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(armPitch, -24f, 4f), env);
                        targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-6f, 0f, 0f), env);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-6f, 0f, 0f), env);
                        break;
                    }
                    case SportAnimKind.VolleyballSpike:
                    {
                        // Explosive 0.66 m airborne leap -> arched back cock -> smooth overhead spike whip!
                        float air = Mathf.Sin(sn * Mathf.PI);
                        float swingBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.34f) / 0.18f));
                        float cock = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sn / 0.34f)) * (1f - swingBlend);
                        float slam = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.36f) / 0.24f)) *
                                     (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.78f) / 0.22f)));
                        targetHipsPos.y += air * 0.66f;
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, Mathf.Lerp(-20f, 16f, swingBlend), air);
                        targetSpineRot.y += 28f * cock - 34f * slam;
                        targetLHipRot = Vector3.Lerp(targetLHipRot, new Vector3(-22f, 0f, -5f), air);
                        targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(-18f, 0f, 5f), air);
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(58f, 0f, 0f), air);
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(64f, 0f, 0f), air);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(Mathf.Lerp(-145f, -40f, swingBlend), 0f, -24f), air);
                        targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-18f, 0f, 0f), air);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(Mathf.Lerp(-172f, -48f, slam), -14f, 22f), air);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(Mathf.Lerp(-78f, -12f, swingBlend), 0f, 0f), air);
                        break;
                    }
                    case SportAnimKind.VictoryJump:
                    {
                        // Joyful celebration hop with both fists pumped skyward!
                        float hop = Mathf.Sin(sn * Mathf.PI);
                        targetHipsPos.y += hop * 0.40f;
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -10f, hop);
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(38f, 0f, 0f), hop);
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(38f, 0f, 0f), hop);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-156f, 0f, -34f), hop);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-156f, 0f, 34f), hop);
                        targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-26f, 0f, 0f), hop);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-26f, 0f, 0f), hop);
                        break;
                    }
                    case SportAnimKind.SoccerSlideTackle:
                    {
                        // Low turf-skimming slide tackle: hips drop low, lead leg spears straight forward, rear knee tucks!
                        float slide = Mathf.Sin(Mathf.Clamp01(sn / 0.85f) * Mathf.PI);
                        targetHipsPos.y -= 0.54f * slide;
                        targetHipsRot.x = Mathf.Lerp(targetHipsRot.x, -42f, slide);
                        targetHipsRot.z = Mathf.Lerp(targetHipsRot.z, -24f * _sportAnimSide, slide);
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, 18f, slide);
                        targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(-72f, 0f, 8f), slide);
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(8f, 0f, 0f), slide);
                        targetLHipRot = Vector3.Lerp(targetLHipRot, new Vector3(-28f, -20f, -18f), slide);
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(92f, 0f, 0f), slide);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(24f, 0f, -54f), slide);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-36f, 0f, 44f), slide);
                        break;
                    }
                    case SportAnimKind.SoccerFeint:
                    {
                        // Crisp step-over body feint: quick shoulder drop to one side then explosive push opposite!
                        float sway = Mathf.Sin(sn * TwoPi) * _sportAnimSide;
                        targetHipsPos.y -= 0.14f * env;
                        targetHipsPos.x += sway * 0.16f;
                        targetSpineRot.z += -24f * sway;
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, 18f, env);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-30f, 0f, -38f), env);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-30f, 0f, 38f), env);
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(36f, 0f, 0f), env);
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(36f, 0f, 0f), env);
                        break;
                    }
                    case SportAnimKind.GoalkeeperDive:
                    {
                        // Full horizontal diving stretch toward shot corner (sideSign = +1 right, -1 left)
                        float dive = Mathf.Sin(sn * Mathf.PI);
                        float s = _sportAnimSide;
                        targetHipsPos.y += (0.20f * Mathf.Sin(Mathf.Clamp01(sn * 1.4f) * Mathf.PI) - 0.34f * Mathf.Clamp01(sn * 1.15f)) * dive;
                        targetHipsPos.x += s * 0.52f * dive;
                        targetHipsRot.z = Mathf.Lerp(targetHipsRot.z, -82f * s, dive);
                        targetSpineRot.z = Mathf.Lerp(targetSpineRot.z, -10f * s, dive);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-164f, 0f, -14f), dive);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-164f, 0f, 14f), dive);
                        targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-8f, 0f, 0f), dive);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-8f, 0f, 0f), dive);
                        targetLHipRot = Vector3.Lerp(targetLHipRot, new Vector3(14f, 0f, -12f), dive);
                        targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(14f, 0f, 12f), dive);
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(24f, 0f, 0f), dive);
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(24f, 0f, 0f), dive);
                        break;
                    }
                    case SportAnimKind.VolleyballBlock:
                    {
                        // Vertical 0.72 m roof-block leap with both hands forming a solid net wall overhead!
                        float air = Mathf.Sin(sn * Mathf.PI);
                        targetHipsPos.y += air * 0.72f;
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, 8f, air);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-172f, 8f, -6f), air);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-172f, -8f, 6f), air);
                        targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-4f, 0f, 0f), air);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-4f, 0f, 0f), air);
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(26f, 0f, 0f), air);
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(26f, 0f, 0f), air);
                        break;
                    }
                    case SportAnimKind.VolleyballDiveSave:
                    {
                        // Athletic forward sand pancake dive: body stretches horizontally near ground, arms extended forward!
                        float dive = Mathf.Sin(sn * Mathf.PI);
                        targetHipsPos.y -= 0.56f * dive;
                        targetHipsPos.z += 0.34f * dive;
                        targetHipsRot.x = Mathf.Lerp(targetHipsRot.x, 74f, dive);
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -18f, dive);
                        targetHeadRot.x = Mathf.Lerp(targetHeadRot.x, -38f, dive);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-164f, 14f, -8f), dive);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-164f, -14f, 8f), dive);
                        targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-6f, 0f, 0f), dive);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-6f, 0f, 0f), dive);
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(36f, 0f, 0f), dive);
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(44f, 0f, 0f), dive);
                        break;
                    }
                    case SportAnimKind.VolleyballTip:
                    {
                        // Nimble jump + delicate fingertip dink/plase push right over the opponent's block!
                        float air = Mathf.Sin(sn * Mathf.PI);
                        targetHipsPos.y += air * 0.44f;
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -6f, air);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-148f, -10f, 14f), air);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-22f, 0f, 0f), air);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-76f, 0f, -24f), air);
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(34f, 0f, 0f), air);
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(40f, 0f, 0f), air);
                        break;
                    }
                    case SportAnimKind.SwimDiveStart:
                    {
                        // Olympic streamlined arrow dive: holds crisp head-first entry through water splash before leveling into stroke
                        float entryIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sn / 0.22f));
                        float entryOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.84f) / 0.16f));
                        float diveW = entryIn * entryOut;
                        float pitchT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sn / 0.65f));
                        float arc = Mathf.Sin(sn * Mathf.PI);
                        targetHipsRot.x = Mathf.Lerp(targetHipsRot.x, Mathf.Lerp(28f, 96f, pitchT), diveW);
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, 12f, diveW);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-176f, 0f, -6f), diveW);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-176f, 0f, 6f), diveW);
                        targetLElbowRot = Vector3.Lerp(targetLElbowRot, Vector3.zero, diveW);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, Vector3.zero, diveW);
                        targetLHipRot = Vector3.Lerp(targetLHipRot, new Vector3(12f * arc, 0f, -4f), diveW);
                        targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(12f * arc, 0f, 4f), diveW);
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(6f, 0f, 0f), diveW);
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(6f, 0f, 0f), diveW);
                        targetLAnkleRot = Vector3.Lerp(targetLAnkleRot, new Vector3(42f, 0f, 0f), diveW);
                        targetRAnkleRot = Vector3.Lerp(targetRAnkleRot, new Vector3(42f, 0f, 0f), diveW);
                        break;
                    }
                    case SportAnimKind.SwimFlipTurn:
                    {
                        // Tucked 360-degree Olympic flip turn / mid-air diving somersault!
                        float tuck = Mathf.Sin(sn * Mathf.PI);
                        targetHipsRot.x = sn * 360f;
                        targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, 42f, tuck);
                        targetLHipRot = Vector3.Lerp(targetLHipRot, new Vector3(-92f, 0f, -8f), tuck);
                        targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(-92f, 0f, 8f), tuck);
                        targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(115f, 0f, 0f), tuck);
                        targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(115f, 0f, 0f), tuck);
                        targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-68f, 0f, -24f), tuck);
                        targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-68f, 0f, 24f), tuck);
                        targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-86f, 0f, 0f), tuck);
                        targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-86f, 0f, 0f), tuck);
                        break;
                    }
                }
            }

            float swingTime = Time.time - _tennisContactAt;
            if (tennisStance)
            {
                float serveJump = 0f;
                if (_tennisServing && swingTime >= -0.22f && swingTime <= 0.22f)
                {
                    serveJump = Mathf.Sin(Mathf.InverseLerp(-0.22f, 0.22f, swingTime) * Mathf.PI) * 0.24f;
                }
                float readyBounce = Mathf.Abs(Mathf.Sin(_idleTime * 4.2f)) * 0.016f * (1f - moveBlend);
                targetHipsPos.y += serveJump - 0.072f + readyBounce;
                targetHipsRot.y *= 0.35f;
                targetSpineRot.x = _tennisServing && swingTime < 0.06f ? -14f : 14f;
                float windup = Mathf.Clamp01(1f - Mathf.Abs(swingTime + 0.14f) / 0.18f);
                float follow = Mathf.Clamp01(1f - Mathf.Abs(swingTime - 0.11f) / 0.20f);
                float sideSign = _tennisBackhand ? -1f : 1f;
                targetSpineRot.y += sideSign * (36f * windup - 42f * follow);
                targetHipsRot.y += sideSign * (14f * windup - 16f * follow);
                if (serveJump > 0.02f)
                {
                    targetLKneeRot.x = Mathf.Lerp(targetLKneeRot.x, 26f, serveJump / 0.24f);
                    targetRKneeRot.x = Mathf.Lerp(targetRKneeRot.x, 30f, serveJump / 0.24f);
                }
            }

            float doorBodyWeight = 0f;
            if (!carrying && !petting && !tennisStance && !_isLyingDown && !_isSwimming && _doorHandle != null && _grounded)
            {
                float dElapsed = Time.time - _doorReachStarted;
                if (dElapsed < _doorReachDuration)
                {
                    float dIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dElapsed / _doorReachTime));
                    float releaseStart = Mathf.Max(_doorReachTime, _doorReachDuration * 0.64f);
                    float dOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dElapsed - releaseStart) / Mathf.Max(0.08f, _doorReachDuration - releaseStart)));
                    float doorScaleY = transform.lossyScale.y;
                    Vector3 nominalSh = transform.TransformPoint(new Vector3(_doorReachRight ? 0.21f : -0.21f, 1.38f, 0f));
                    float distFade = 1f - Mathf.InverseLerp(0.68f * doorScaleY, 1.18f * doorScaleY, Vector3.Distance(nominalSh, _doorHandle.position));
                    doorBodyWeight = dIn * dOut * distFade;
                    targetSpineRot.x += 7f * doorBodyWeight;
                    targetSpineRot.y += (_doorReachRight ? -11f : 11f) * doorBodyWeight;
                    targetHeadRot.y += (_doorReachRight ? 6f : -6f) * doorBodyWeight;
                }
            }

            if (!carrying && !petting)
            {
                if (_hasShelfReach && _shelfReachWeight > 0.01f)
                {
                    targetSpineRot.x += 9f * _shelfReachWeight;
                    targetSpineRot.y -= 10f * _shelfReachWeight;
                    targetHeadRot.x += 6f * _shelfReachWeight;
                }
                else if (_consumeMode == 1 && _consumeWeight > 0.01f)
                {
                    // Realistic drinking tilt: approach cup gently, tip head back smoothly on sip, subtle swallow bob between sips
                    float sip = Mathf.Clamp01(_consumeCyclePhase);
                    float swallowBob = Mathf.Sin((_idleTime + _noiseSeed) * 8.5f) * 2.2f * (1f - sip) * _consumeWeight;
                    float approachNod = Mathf.Sin(Mathf.Clamp01(sip * 2f) * Mathf.PI) * 4f * _consumeWeight;
                    targetHeadRot.x = Mathf.Lerp(targetHeadRot.x, -18f, sip * _consumeWeight) + approachNod + swallowBob;
                    targetHeadRot.y = Mathf.Lerp(targetHeadRot.y, 4f, sip * _consumeWeight * 0.5f);
                    targetNeckRot.x = Mathf.Lerp(targetNeckRot.x, -7f, sip * _consumeWeight);
                    targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -3.5f, sip * _consumeWeight);
                }
                else if (_consumeMode == 2 && _consumeWeight > 0.01f)
                {
                    // Realistic eating nod toward snack + subtle chewing micro-motion between bites
                    float bite = Mathf.Clamp01(_consumeCyclePhase);
                    float chew = Mathf.Sin((_idleTime + _noiseSeed) * 19f) * 3.0f * (1f - bite) * _consumeWeight;
                    targetHeadRot.x = Mathf.Lerp(targetHeadRot.x, 13f, bite * _consumeWeight) + chew;
                    targetHeadRot.y = Mathf.Lerp(targetHeadRot.y, 6f, bite * _consumeWeight * 0.65f);
                    targetHeadRot.z += 3.5f * bite * _consumeWeight;
                    targetNeckRot.x = Mathf.Lerp(targetNeckRot.x, 5f, bite * _consumeWeight);
                }
                else if (_hasDualAction)
                {
                    float actionBlend = Mathf.Max(_dualLeftWeight, _dualRightWeight);
                    if (actionBlend < 0.01f && (Mathf.Abs(_dualSpineLean) > 0.1f || Mathf.Abs(_dualHeadPitch) > 0.1f || Mathf.Abs(_dualHipsDrop) > 0.001f))
                    {
                        actionBlend = 1f;
                    }
                    targetHipsPos.y -= _dualHipsDrop * actionBlend;
                    targetSpineRot.x += _dualSpineLean;
                    targetNeckRot.x += _dualHeadPitch * 0.35f;
                    targetHeadRot.x += _dualHeadPitch * 0.65f;
                }
            }

            // One-knee kneel overlay (farm work, low interactions & cat petting; clamped to hipsBaseY * 0.5f)
            bool canKneel = _grounded && !isSitting && !_isLyingDown && !_isSwimming && _prayerStage == 0 && _knockdownTimer < 0f;
            float desiredKneel = petting ? Mathf.Max(_kneelWeight, petCrouch * 0.90f) : _kneelWeight;
            _activeKneelRight = petting ? true : _kneelRight;
            if (canKneel && (desiredKneel > 0.01f || _kneelBlend > 0.001f))
            {
                float kneelRate = petting ? 14f : 8f;
                _kneelBlend = Mathf.Lerp(_kneelBlend, desiredKneel, 1f - Mathf.Exp(-kneelRate * dt));
                if (desiredKneel <= 0.01f && _kneelBlend < 0.002f)
                {
                    _kneelBlend = 0f;
                }
                if (_kneelBlend > 0.01f)
                {
                    float kneelDrop = Mathf.Lerp(0f, hipsBaseY * 0.42f, _kneelBlend);
                    targetHipsPos.y = Mathf.Max(hipsBaseY - hipsBaseY * 0.5f, targetHipsPos.y - kneelDrop);
                    if (!petting) targetSpineRot.x += 6f * _kneelBlend;
                }
            }
            else if (!canKneel && _kneelBlend > 0f)
            {
                _kneelBlend = Mathf.Lerp(_kneelBlend, 0f, 1f - Mathf.Exp(-10f * dt));
                if (_kneelBlend < 0.002f) _kneelBlend = 0f;
            }

            if (chestVisual != null)
            {
                float breathScale = 1f + breathWave * 0.018f * (1f - moveBlend);
                Vector3 targetScale = new Vector3(
                    _chestBaseScale.x * breathScale,
                    _chestBaseScale.y,
                    _chestBaseScale.z * breathScale);
                chestVisual.localScale = Vector3.Lerp(chestVisual.localScale, targetScale, 1f - Mathf.Exp(-6f * dt));
            }

            float damp = _isLyingDown ? 5.5f : (_prayerStage > 0 ? 6.5f : (_landSquatTimer >= 0f || carrying || petting || _carryThrowTimer >= 0f ? 25f : (_grounded ? 14f : 10f)));
            _baseHipsLocalPos = Vector3.Lerp(_baseHipsLocalPos, targetHipsPos, 1f - Mathf.Exp(-damp * dt));
            hips.localPosition = _baseHipsLocalPos;
            SmoothRotate(hips, targetHipsRot, damp, dt);
            SmoothRotate(spine, targetSpineRot, damp, dt);
            SmoothRotate(neck, targetNeckRot, damp, dt);
            SmoothRotate(head, targetHeadRot, _isLyingDown ? 4.5f : (_prayerStage > 0 ? 6.0f : 9f), dt);

            SmoothRotate(lHip, targetLHipRot, damp, dt);
            SmoothRotate(rHip, targetRHipRot, damp, dt);
            SmoothRotate(lKnee, targetLKneeRot, damp, dt);
            SmoothRotate(rKnee, targetRKneeRot, damp, dt);
            SmoothRotate(lAnkle, targetLAnkleRot, damp, dt);
            SmoothRotate(rAnkle, targetRAnkleRot, damp, dt);

            SmoothRotate(lShoulder, targetLShoulderRot, damp, dt);
            SmoothRotate(rShoulder, targetRShoulderRot, damp, dt);
            SmoothRotate(lElbow, targetLElbowRot, damp, dt);
            SmoothRotate(rElbow, targetRElbowRot, damp, dt);

            Vector3 leftGrip = Vector3.zero;
            Vector3 rightGrip = Vector3.zero;
            float carryWeight = 0f;
            if (carrying)
            {
                carryWeight = carry.UpdateGrip(this, hips.position, out leftGrip, out rightGrip);
                if (carryWeight > 0f)
                {
                    Vector3 leftReach = ShoulderReach(lShoulder, lElbow, leftGrip);
                    Vector3 rightReach = ShoulderReach(rShoulder, rElbow, rightGrip);
                    Vector3 reach = leftReach.sqrMagnitude > rightReach.sqrMagnitude ? leftReach : rightReach;
                    hips.position += Vector3.ClampMagnitude(reach, 0.16f * transform.lossyScale.y) * carryWeight;
                }
            }
            if (petting)
            {
                Vector3 reach = ShoulderReach(rShoulder, rElbow, petHand);
                hips.position += Vector3.ClampMagnitude(reach, 0.13f * transform.lossyScale.y) * petWeight;
            }
            else if (doorBodyWeight > 0.01f && _doorHandle != null)
            {
                Transform dSh = _doorReachRight ? rShoulder : lShoulder;
                Transform dElb = _doorReachRight ? rElbow : lElbow;
                Vector3 dReach = ShoulderReach(dSh, dElb, _doorHandle.position);
                hips.position += Vector3.ClampMagnitude(dReach, 0.08f * transform.lossyScale.y) * doorBodyWeight;
            }

            bool sportAirOrKick = _sportAnimTimer >= 0f &&
                                  (_sportAnimKind == SportAnimKind.SoccerKick ||
                                   _sportAnimKind == SportAnimKind.SoccerSteal ||
                                   _sportAnimKind == SportAnimKind.SoccerSlideTackle ||
                                   _sportAnimKind == SportAnimKind.SoccerFeint ||
                                   _sportAnimKind == SportAnimKind.GoalkeeperDive ||
                                   _sportAnimKind == SportAnimKind.BasketballJumpShot ||
                                   _sportAnimKind == SportAnimKind.VolleyballSpike ||
                                   _sportAnimKind == SportAnimKind.VolleyballBlock ||
                                   _sportAnimKind == SportAnimKind.VolleyballDiveSave ||
                                   _sportAnimKind == SportAnimKind.VolleyballTip ||
                                   _sportAnimKind == SportAnimKind.SwimDiveStart ||
                                   _sportAnimKind == SportAnimKind.SwimFlipTurn ||
                                   _sportAnimKind == SportAnimKind.VictoryJump);
            bool tennisJumpServe = tennisStance && _tennisServing && swingTime >= -0.22f && swingTime <= 0.22f;

            float footPlantWeight = _knockdownTimer >= 0f ? knockdownFootBlend : 1f;
            if (_grounded && !_isSwimming && !_isLyingDown && _prayerStage == 0 && _vaultTimer < 0f &&
                _punchTimer < 0f && footPlantWeight > 0.005f &&
                !sportAirOrKick && !tennisJumpServe &&
                lAnkle != null && rAnkle != null)
            {
                bool stationaryGround = _speedMs < 0.015f && absYawRate < 0.25f && _turnInPlaceWeight < 0.01f;
                PlantFeet(strideLength, stance, runBlend, tennisStance, idleW, localTravel, dt,
                    footPlantWeight, startledStepW, hitStaggerW, stationaryGround);
            }
            else
            {
                _smoothedPelvisDrop = Mathf.Lerp(_smoothedPelvisDrop, 0f, 1f - Mathf.Exp(-10f * dt));
            }

            // Smoothly blended 2-bone IK hand placements for every Prayer (Namaz) stage so hands glide naturally between ears, abdomen, knees, carpet & thighs!
            if (_prayerStage > 0 && _prayerStage != 6 && !_isLyingDown && lShoulder != null && lElbow != null && rShoulder != null && rElbow != null)
            {
                Vector3 leftWorld = Vector3.zero;
                Vector3 rightWorld = Vector3.zero;
                Vector3 leftPole = -transform.right;
                Vector3 rightPole = transform.right;
                bool hasTargets = false;

                if (_prayerStage == 5 && head != null)
                {
                    // İftitah Tekbiri: both hands raised respectfully beside the ears
                    leftWorld = head.position - transform.right * 0.21f + transform.forward * 0.09f + transform.up * 0.04f;
                    rightWorld = head.position + transform.right * 0.21f + transform.forward * 0.09f + transform.up * 0.04f;
                    leftPole = -transform.right - transform.up * 0.5f;
                    rightPole = transform.right - transform.up * 0.5f;
                    hasTargets = true;
                }
                else if (_prayerStage == 1 && spine != null)
                {
                    // Kıyam: right hand folded cleanly over left hand at navel/abdomen
                    leftWorld = spine.position + transform.forward * 0.165f + transform.up * 0.075f - transform.right * 0.01f;
                    rightWorld = spine.position + transform.forward * 0.185f + transform.up * 0.085f + transform.right * 0.01f;
                    leftPole = -transform.right - transform.up * 0.35f;
                    rightPole = transform.right - transform.up * 0.35f;
                    hasTargets = true;
                }
                else if (_prayerStage == 2 && lKnee != null && rKnee != null)
                {
                    // Rükû: both palms directly grasping the knees
                    leftWorld = lKnee.position + transform.forward * 0.04f + transform.up * 0.02f;
                    rightWorld = rKnee.position + transform.forward * 0.04f + transform.up * 0.02f;
                    hasTargets = true;
                }
                else if (_prayerStage == 3)
                {
                    // Secde: both palms placed flat on the carpet right beside the head, elbows lifted outward
                    leftWorld = transform.TransformPoint(new Vector3(-0.23f, 0.045f, 0.36f));
                    rightWorld = transform.TransformPoint(new Vector3(0.23f, 0.045f, 0.36f));
                    leftPole = -transform.right + transform.up * 0.45f;
                    rightPole = transform.right + transform.up * 0.45f;
                    hasTargets = true;
                }
                else if (_prayerStage == 4 && lKnee != null && rKnee != null)
                {
                    // Oturuş / Tahiyyat & Selam: both palms resting cleanly on thighs just behind knees
                    leftWorld = lKnee.position - transform.forward * 0.09f + transform.up * 0.085f;
                    rightWorld = rKnee.position - transform.forward * 0.09f + transform.up * 0.085f;
                    hasTargets = true;
                }

                if (hasTargets)
                {
                    Vector3 targetL = transform.InverseTransformPoint(leftWorld);
                    Vector3 targetR = transform.InverseTransformPoint(rightWorld);
                    if (!_prayerHandsInit)
                    {
                        _prayerLeftHandLocal = targetL;
                        _prayerRightHandLocal = targetR;
                        _prayerHandsInit = true;
                    }
                    else
                    {
                        float hBlend = 1f - Mathf.Exp(-8.5f * dt);
                        _prayerLeftHandLocal = Vector3.Lerp(_prayerLeftHandLocal, targetL, hBlend);
                        _prayerRightHandLocal = Vector3.Lerp(_prayerRightHandLocal, targetR, hBlend);
                    }
                    _prayerHandFade = Mathf.MoveTowards(_prayerHandFade, 1f, dt * 6f);
                    AimHand(lShoulder, lElbow, transform.TransformPoint(_prayerLeftHandLocal), leftPole, _prayerHandFade);
                    AimHand(rShoulder, rElbow, transform.TransformPoint(_prayerRightHandLocal), rightPole, _prayerHandFade);
                }
            }
            else if (_prayerHandFade > 0.005f && _prayerHandsInit && !_isLyingDown && lShoulder != null && lElbow != null && rShoulder != null && rElbow != null)
            {
                _prayerHandFade = Mathf.MoveTowards(_prayerHandFade, 0f, dt * 4.2f);
                if (_prayerHandFade > 0.005f)
                {
                    AimHand(lShoulder, lElbow, transform.TransformPoint(_prayerLeftHandLocal), -transform.right, _prayerHandFade);
                    AimHand(rShoulder, rElbow, transform.TransformPoint(_prayerRightHandLocal), transform.right, _prayerHandFade);
                }
                else
                {
                    _prayerHandsInit = false;
                }
            }

            if (carryWeight > 0f)
            {
                AimHand(lShoulder, lElbow, leftGrip, -transform.right, carryWeight);
                AimHand(rShoulder, rElbow, rightGrip, transform.right, carryWeight);
            }
            else if (_carryReleaseTimer >= 0f)
            {
                float relW = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_carryReleaseTimer / CarryReleaseFadeDuration));
                if (relW > 0.005f)
                {
                    AimHand(lShoulder, lElbow, _carryReleaseLeft, -transform.right, relW);
                    AimHand(rShoulder, rElbow, _carryReleaseRight, transform.right, relW);
                }
            }
            else if (_carryThrowTimer >= 0f)
            {
                float fNorm = Mathf.Clamp01(_carryThrowTimer / CarryThrowFollowDuration);
                float arc = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fNorm / 0.35f));
                float fW = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((fNorm - 0.14f) / 0.86f));
                if (fW > 0.005f)
                {
                    Vector3 followOff = new Vector3(0f, 0.08f * arc, 0.15f * arc);
                    AimHand(lShoulder, lElbow, transform.TransformPoint(_carryThrowLeftLocal + followOff), -transform.right, fW);
                    AimHand(rShoulder, rElbow, transform.TransformPoint(_carryThrowRightLocal + followOff), transform.right, fW);
                }
            }
            if (petting)
            {
                AimHand(rShoulder, rElbow, petHand, transform.right, petWeight);
                if (lKnee != null)
                    AimHand(lShoulder, lElbow, lKnee.position + transform.up * 0.09f - transform.right * 0.03f,
                        -transform.right, petWeight);
            }
            else if (_petFadeBlend > 0.005f)
            {
                AimHand(rShoulder, rElbow, _lastPetHandWorld, transform.right, _petFadeBlend);
                if (lKnee != null)
                    AimHand(lShoulder, lElbow, lKnee.position + transform.up * 0.09f - transform.right * 0.03f,
                        -transform.right, _petFadeBlend);
            }
            if (_vaultTimer >= 0f && _vaultSupport.HasValue && rShoulder != null)
            {
                Vector3 palm = _vaultSupport.Value + transform.right * 0.12f;
                float time = _vaultTimer / VaultDuration;
                float plant = Mathf.SmoothStep(0f, 1f, (time - 0.13f) / 0.15f) *
                    (1f - Mathf.SmoothStep(0f, 1f, (time - 0.50f) / 0.18f));
                float reachable = 1f - Mathf.InverseLerp(0.51f, 0.74f, Vector3.Distance(rShoulder.position, palm));
                AimHand(rShoulder, rElbow, palm, transform.right, plant * reachable);
            }
            if (_isSwimming && !_swimBreaststroke)
            {
                float strokeBlend = Mathf.SmoothStep(0f, 1f, _swimSpeed / 1.70f);
                AimHand(lShoulder, lElbow, SwimHand(-1f, _swimPhase), -transform.right, strokeBlend);
                AimHand(rShoulder, rElbow, SwimHand(1f, _swimPhase + Mathf.PI), transform.right, strokeBlend);
            }
            else if (!carrying && !petting && !tennisStance && !_isLyingDown && _doorHandle != null && _grounded)
            {
                float elapsed = Time.time - _doorReachStarted;
                Transform reachSh = _doorReachRight ? rShoulder : lShoulder;
                Transform reachElb = _doorReachRight ? rElbow : lElbow;
                float doorScaleY = transform.lossyScale.y;
                float reachFade = reachSh != null
                    ? (1f - Mathf.InverseLerp(0.68f * doorScaleY, 1.18f * doorScaleY, Vector3.Distance(reachSh.position, _doorHandle.position)))
                    : 1f;
                float weight = doorBodyWeight * reachFade;
                if (weight > 0.005f)
                    AimHand(reachSh, reachElb, _doorHandle.position, transform.right * (_doorReachRight ? 1f : -1f), weight);
                if (elapsed >= _doorReachDuration) _doorHandle = null;
            }

            if (tennisStance) ApplyTennisRacketPose(swingTime);

            // Shop shelf IK reach & drink/eat mouth-target IK overlay
            if (!carrying && !petting && _carryReleaseTimer < 0f && _carryThrowTimer < 0f &&
                !tennisStance && !_isSwimming && !_isLyingDown && rShoulder != null && rElbow != null)
            {
                if (_hasShelfReach && _shelfReachWeight > 0.01f)
                {
                    AimHand(rShoulder, rElbow, _shelfReachWorldPos, transform.right, _shelfReachWeight);
                }
                else if (_consumeMode > 0 && _consumeWeight > 0.01f && head != null)
                {
                    float phase = Mathf.Clamp01(_consumeCyclePhase);
                    Vector3 holdPos = head.position + transform.forward * 0.28f + transform.right * 0.14f - transform.up * 0.24f;
                    Vector3 mouthPos = _consumeMode == 1
                        ? head.position + transform.forward * 0.17f + transform.right * 0.03f - transform.up * 0.01f
                        : head.position + transform.forward * 0.19f + transform.right * 0.04f - transform.up * 0.06f;
                    Vector3 handTarget = Vector3.Lerp(holdPos, mouthPos, phase);
                    AimHand(rShoulder, rElbow, handTarget, transform.right, _consumeWeight);
                }
            }

            // Dual-hand physical interaction IK overlay (washing face/hands, abdest, typing, ATM, remote, etc.)
            if (!carrying && !petting && _carryReleaseTimer < 0f && _carryThrowTimer < 0f &&
                !tennisStance && !_isSwimming && !_isLyingDown && _hasDualAction)
            {
                if (_dualLeftWeight > 0.01f && lShoulder != null && lElbow != null)
                {
                    AimHand(lShoulder, lElbow, _dualLeftTarget, -transform.right, _dualLeftWeight);
                }
                if (_dualRightWeight > 0.01f && rShoulder != null && rElbow != null)
                {
                    AimHand(rShoulder, rElbow, _dualRightTarget, transform.right, _dualRightWeight);
                }
            }

            // Shooting Range two-handed sporting firearm stance (smooth raise/lower, weapon switch blend, pump/bolt/reload hand sync)
            float weaponAimTarget = _weaponAimActive ? 1f : 0f;
            _weaponAimBlend = Mathf.Lerp(_weaponAimBlend, weaponAimTarget, 1f - Mathf.Exp(-14f * dt));
            if (_weaponAimBlend < 0.004f && !_weaponAimActive) _weaponAimBlend = 0f;
            _weaponLongGunBlend = Mathf.Lerp(_weaponLongGunBlend, _weaponIsLongGun ? 1f : 0f, 1f - Mathf.Exp(-11f * dt));

            if (_weaponAimBlend > 0.005f && rShoulder != null && rElbow != null && lShoulder != null && lElbow != null)
            {
                float longW = _weaponLongGunBlend;
                float reloadDip = _weaponReloadPhase > 0.005f ? Mathf.Sin(_weaponReloadPhase * Mathf.PI) : 0f;
                float kickPitch = _weaponRecoilKick * Mathf.Lerp(14.0f, 9.5f, longW);
                float raiseDrop = (1f - _weaponAimBlend) * 24f;
                Quaternion aimRot = transform.rotation * Quaternion.Euler(
                    _weaponAimPitchDeg - kickPitch + reloadDip * 18f + raiseDrop,
                    0f,
                    reloadDip * -14f);
                Vector3 aimFwd = aimRot * Vector3.forward;
                Vector3 aimRight = aimRot * Vector3.right;
                Vector3 aimUp = aimRot * Vector3.up;

                if (spine != null)
                {
                    Quaternion aimSpine = Quaternion.Euler(
                        4f - _weaponRecoilKick * 3.5f,
                        Mathf.Lerp(-6f, -14f, longW),
                        0f);
                    spine.localRotation = Quaternion.Slerp(spine.localRotation, aimSpine, _weaponAimBlend);
                }
                if (head != null)
                {
                    Quaternion aimHead = Quaternion.Euler(
                        Mathf.Clamp(_weaponAimPitchDeg * 0.55f, -24f, 24f),
                        Mathf.Lerp(6f, 14f, longW),
                        Mathf.Lerp(0f, 5f, longW));
                    head.localRotation = Quaternion.Slerp(head.localRotation, aimHead, _weaponAimBlend);
                }

                Vector3 rPistol = rShoulder.position
                    + aimFwd * (0.42f - _weaponRecoilKick * 0.055f)
                    - aimRight * 0.11f
                    + aimUp * (0.045f + _weaponRecoilKick * 0.025f);
                Vector3 rLong = rShoulder.position
                    + aimFwd * (0.27f - _weaponRecoilKick * 0.050f)
                    - aimRight * 0.03f
                    - aimUp * (0.015f - _weaponRecoilKick * 0.018f);
                Vector3 rBase = Vector3.Lerp(rPistol, rLong, longW);
                rBase -= aimUp * (reloadDip * 0.12f + (1f - _weaponAimBlend) * 0.16f);

                Vector3 lPistol = rBase - aimFwd * 0.018f - aimRight * 0.028f - aimUp * 0.025f;
                Vector3 lLong = rBase + aimFwd * 0.31f - aimRight * 0.025f + aimUp * 0.015f;
                Vector3 lTarget = Vector3.Lerp(lPistol, lLong, longW);
                Vector3 rHandTarget = rBase;

                if (_weaponReloadPhase > 0.02f)
                {
                    if (_weaponReloadStyle == 1)
                    {
                        // Shotgun shell-by-shell feed: 3 rhythmic dips from hip shell pouch into underside loading port
                        float shellPhase = Mathf.Repeat(_weaponReloadPhase * 3f, 1f);
                        float shellDip = Mathf.Sin(shellPhase * Mathf.PI);
                        lTarget = rBase + aimFwd * 0.14f - aimUp * (0.045f + shellDip * 0.28f) - aimRight * (0.03f + shellDip * 0.10f);
                    }
                    else if (_weaponReloadStyle == 2)
                    {
                        // Sniper reload: 0..0.72 mag swap with left hand, 0.72..1.0 right hand racks bolt cleanly
                        if (_weaponReloadPhase < 0.72f)
                        {
                            float magNorm = _weaponReloadPhase / 0.72f;
                            float pouchPull = Mathf.Sin(Mathf.Clamp01(magNorm * 1.35f) * Mathf.PI);
                            lTarget = rBase + aimFwd * 0.14f - aimUp * (0.06f + pouchPull * 0.32f) - aimRight * (0.04f + pouchPull * 0.08f);
                        }
                        else
                        {
                            float boltCycle = Mathf.Clamp01((_weaponReloadPhase - 0.72f) / 0.28f);
                            float liftW = Mathf.Sin(boltCycle * Mathf.PI);
                            float pullBack = Mathf.Sin(Mathf.Clamp01((boltCycle - 0.15f) / 0.70f) * Mathf.PI);
                            rHandTarget = rBase + aimUp * (0.065f * liftW) + aimRight * (0.06f * liftW) + aimFwd * (0.055f - 0.075f * pullBack);
                        }
                    }
                    else
                    {
                        // Pistol / Carbine mag swap: left hand drops to belt pouch then seats magazine into magwell
                        float pouchPull = Mathf.Sin(Mathf.Clamp01(_weaponReloadPhase * 1.45f) * Mathf.PI);
                        lTarget = rBase + aimFwd * Mathf.Lerp(0.01f, 0.12f, longW)
                            - aimUp * (0.06f + pouchPull * 0.34f)
                            - aimRight * (0.04f + pouchPull * 0.08f);
                    }
                }
                else if (_weaponActionKind == 1 && _weaponActionPhase > 0.005f)
                {
                    // Shotgun pump cycle: left hand tracks forend pump slider back and forward cleanly
                    float pumpBack = Mathf.Sin(_weaponActionPhase * Mathf.PI);
                    lTarget -= aimFwd * (pumpBack * 0.095f);
                }
                else if (_weaponActionKind == 2 && _weaponActionPhase > 0.005f)
                {
                    // Sniper bolt cycle: right hand lifts off grip to bolt handle, pulls back, pushes forward & locks
                    float liftW = Mathf.Sin(_weaponActionPhase * Mathf.PI);
                    float pullBack = Mathf.Sin(Mathf.Clamp01((_weaponActionPhase - 0.15f) / 0.70f) * Mathf.PI);
                    rHandTarget = rBase + aimUp * (0.065f * liftW) + aimRight * (0.06f * liftW) + aimFwd * (0.055f - 0.075f * pullBack);
                }

                AimHand(rShoulder, rElbow, rHandTarget, transform.right * 0.75f - Vector3.up * 0.55f, _weaponAimBlend);
                AimHand(lShoulder, lElbow, lTarget, -transform.right * 0.75f - Vector3.up * 0.55f, _weaponAimBlend);

                WeaponGripWorldPos = rBase;
                WeaponGripWorldRot = aimRot;
            }
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

        private void AnimateSitting(float dt)
        {
            if (_autoSeatActive)
            {
                _autoSeatElapsed += dt;
                float t = Mathf.Clamp01(_autoSeatElapsed / _autoSeatDuration);
                _seatAmount = Mathf.Lerp(_autoSeatFrom, _autoSeatTo, Mathf.SmoothStep(0f, 1f, t));
                if (t >= 1f)
                {
                    _autoSeatActive = false;
                    if (_autoSeatTo <= 0f)
                    {
                        SetSitting(false);
                        return;
                    }
                }
            }

            float breath = Mathf.Sin(_idleTime * Mathf.PI * 2f * 0.25f) * 0.8f;

            if (isDrivingSeat)
            {
                float steerNorm = Mathf.Clamp(_steerAngleDeg / 36f, -1f, 1f);
                _smoothedDriveSteerNorm = Mathf.Lerp(_smoothedDriveSteerNorm, steerNorm, 1f - Mathf.Exp(-11f * dt));
                float sNorm = _smoothedDriveSteerNorm;
                float accelPitch = Mathf.Clamp(-_driveLongitudinalAccel * 0.75f, -5.5f, 6.5f);

                hips.localPosition = Vector3.Lerp(hips.localPosition, new Vector3(0f, 0.17f, 0f), 1f - Mathf.Exp(-12f * dt));
                SmoothRotate(hips, Vector3.zero, 12f, dt);
                float wheelLean = _steeringWheel != null ? 8f : -6f;
                SmoothRotate(spine, new Vector3(wheelLean + breath + accelPitch, sNorm * 6f, -sNorm * 4f), 10f, dt);
                SmoothRotate(neck, new Vector3(4f - accelPitch * 0.35f, sNorm * 4f, 0f), 10f, dt);
                SmoothRotate(head, new Vector3(-accelPitch * 0.25f, sNorm * 16f, sNorm * 2.5f), 8f, dt);

                SmoothRotate(lHip, new Vector3(-90f, 0f, -6f), 12f, dt);
                SmoothRotate(rHip, new Vector3(-90f, 0f, 6f), 12f, dt);
                SmoothRotate(lKnee, new Vector3(80f, 0f, 0f), 12f, dt);
                SmoothRotate(rKnee, new Vector3(80f - _driveThrottleInput * 4f, 0f, 0f), 12f, dt);
                SmoothRotate(lAnkle, new Vector3(10f, 0f, 0f), 12f, dt);
                SmoothRotate(rAnkle, new Vector3(10f + _driveThrottleInput * 14f, 0f, 0f), 12f, dt);

                float leftReach = -62f - sNorm * 14f;
                float rightReach = -62f + sNorm * 14f;
                SmoothRotate(lShoulder, new Vector3(leftReach, 10f + sNorm * 10f, -10f - sNorm * 8f), 14f, dt);
                SmoothRotate(rShoulder, new Vector3(rightReach, -10f + sNorm * 10f, 10f - sNorm * 8f), 14f, dt);
                SmoothRotate(lElbow, new Vector3(-32f + sNorm * 10f, 0f, 0f), 14f, dt);
                SmoothRotate(rElbow, new Vector3(-32f - sNorm * 10f, 0f, 0f), 14f, dt);
                if (_steeringWheel != null)
                {
                    // Project driver basis onto steering wheel plane (invariant to wheel local-Y spin) so hands stay cleanly on the rim without ever tangling at full lock
                    Vector3 wheelAxis = _steeringWheel.up.normalized;
                    Vector3 wheelRight = Vector3.ProjectOnPlane(transform.right, wheelAxis);
                    Vector3 wheelUp = Vector3.ProjectOnPlane(transform.up, wheelAxis);
                    if (wheelRight.sqrMagnitude > 0.001f && wheelUp.sqrMagnitude > 0.001f)
                    {
                        wheelRight.Normalize();
                        wheelUp.Normalize();
                        const float rimRadius = 0.162f;
                        float handTurnRad = Mathf.Clamp(sNorm * 68f, -68f, 68f) * Mathf.Deg2Rad;
                        float rightAngle = 14f * Mathf.Deg2Rad - handTurnRad;
                        float leftAngle = 166f * Mathf.Deg2Rad - handTurnRad;
                        Vector3 rimCenter = _steeringWheel.position - wheelAxis * 0.024f;
                        Vector3 leftTarget = rimCenter + (wheelRight * Mathf.Cos(leftAngle) + wheelUp * Mathf.Sin(leftAngle)) * rimRadius;
                        Vector3 rightTarget = rimCenter + (wheelRight * Mathf.Cos(rightAngle) + wheelUp * Mathf.Sin(rightAngle)) * rimRadius;
                        AimHand(lShoulder, lElbow, leftTarget, -transform.right - transform.up * 0.25f, 1f);
                        AimHand(rShoulder, rElbow, rightTarget, transform.right - transform.up * 0.25f, 1f);
                    }
                    else
                    {
                        AimHand(lShoulder, lElbow, _steeringWheel.TransformPoint(new Vector3(-0.145f, 0f, 0.055f)), -transform.right, 1f);
                        AimHand(rShoulder, rElbow, _steeringWheel.TransformPoint(new Vector3(0.145f, 0f, 0.055f)), transform.right, 1f);
                    }
                }
                return;
            }

            // Non-driving seated life: talking decay/blend + chest breathing (desynchronized via _noiseSeed)
            if (_talkingTimer > 0f)
            {
                _talkingTimer = Mathf.Max(0f, _talkingTimer - dt);
            }
            _talkingBlend = Mathf.Lerp(_talkingBlend, _talkingTimer > 0f ? 1f : 0f, 1f - Mathf.Exp(-6f * dt));

            float seatedBreathWave = Mathf.Sin((_idleTime + _noiseSeed) * TwoPi * 0.25f);
            float seatedBreath = seatedBreathWave * 0.8f;
            if (chestVisual != null)
            {
                float breathScale = 1f + seatedBreathWave * 0.018f;
                Vector3 targetScale = new Vector3(
                    _chestBaseScale.x * breathScale,
                    _chestBaseScale.y,
                    _chestBaseScale.z * breathScale);
                chestVisual.localScale = Vector3.Lerp(chestVisual.localScale, targetScale, 1f - Mathf.Exp(-6f * dt));
            }

            if (_seatFloor.HasValue)
            {
                AnimateSeatContact(_seatFloor.Value, _seatAmount, seatedBreath, dt);
            }
            else
            {
                float headNoise = (Mathf.PerlinNoise(_noiseSeed, _idleTime * 0.16f) - 0.5f) * 2f;
                float headLookYaw = headNoise * 11f * (_hasLookPoint ? 0.15f : 1f);

                float gestureW = UpdateSeatedGesture(dt, lookAroundOnly: true);
                float gestureT = _seatGestureDuration > 0.01f ? Mathf.Clamp01(_seatGestureTimer / _seatGestureDuration) : 0f;

                Vector3 spineRot = new Vector3(-7f + seatedBreath, 0f, 0f);
                Vector3 neckRot = new Vector3(5f, 0f, 0f);
                Vector3 headRot = new Vector3(0f, headLookYaw, 0f);

                if (gestureW > 0.001f && _seatGestureKind == 4)
                {
                    float scan = Mathf.Sin(gestureT * TwoPi);
                    headRot.y += scan * 38f * gestureW;
                    spineRot.y += scan * 6f * gestureW;
                }

                Vector3 lShRot = new Vector3(8f + seatedBreath * 0.5f, 0f, -10f);
                Vector3 rShRot = new Vector3(8f + seatedBreath * 0.5f, 0f, 10f);
                Vector3 lElbRot = new Vector3(-44f, 0f, 10f);
                Vector3 rElbRot = new Vector3(-44f, 0f, -10f);

                if (_talkingBlend > 0.02f)
                {
                    float talkTime = _idleTime + _noiseSeed;
                    float beatR = Mathf.Sin(talkTime * 6.2f);
                    float beatL = Mathf.Sin(talkTime * 6.2f + 1.5f);
                    headRot.x += Mathf.Sin(talkTime * 4.1f) * 4f * _talkingBlend;
                    spineRot.x += 6f * _talkingBlend;
                    rShRot = Vector3.Lerp(rShRot, new Vector3(-34f - beatR * 12f, -10f, 14f), _talkingBlend);
                    rElbRot = Vector3.Lerp(rElbRot, new Vector3(-74f + beatR * 16f, 0f, -10f), _talkingBlend);
                    lShRot = Vector3.Lerp(lShRot, new Vector3(-16f - beatL * 6f, 6f, -12f), _talkingBlend * 0.65f);
                    lElbRot = Vector3.Lerp(lElbRot, new Vector3(-58f + beatL * 8f, 0f, 10f), _talkingBlend * 0.65f);
                }

                ApplySeatedLookAt(dt, ref spineRot, ref neckRot, ref headRot);

                hips.localPosition = Vector3.Lerp(hips.localPosition, new Vector3(0f, 0.08f, 0.02f), 1f - Mathf.Exp(-12f * dt));
                SmoothRotate(hips, Vector3.zero, 12f, dt);
                SmoothRotate(spine, spineRot, 10f, dt);
                SmoothRotate(neck, neckRot, 10f, dt);
                SmoothRotate(head, headRot, 6f, dt);

                SmoothRotate(lHip, new Vector3(-88f, 0f, -5f), 12f, dt);
                SmoothRotate(rHip, new Vector3(-88f, 0f, 5f), 12f, dt);
                SmoothRotate(lKnee, new Vector3(88f, 0f, 0f), 12f, dt);
                SmoothRotate(rKnee, new Vector3(88f, 0f, 0f), 12f, dt);
                SmoothRotate(lAnkle, Vector3.zero, 12f, dt);
                SmoothRotate(rAnkle, Vector3.zero, 12f, dt);

                // Hands resting comfortably downward on lap/thighs (never floating out in front!)
                SmoothRotate(lShoulder, lShRot, 12f, dt);
                SmoothRotate(rShoulder, rShRot, 12f, dt);
                SmoothRotate(lElbow, lElbRot, 12f, dt);
                SmoothRotate(rElbow, rElbRot, 12f, dt);
            }
        }

        private float UpdateSeatedGesture(float dt, bool lookAroundOnly = false)
        {
            if (_seatGestureCooldown < 0f)
            {
                _seatGestureCooldown = UnityEngine.Random.Range(2f, 8f);
            }

            if (_seatGestureTimer >= 0f)
            {
                _seatGestureTimer += dt;
                if (_seatGestureTimer >= _seatGestureDuration)
                {
                    _seatGestureTimer = -1f;
                    _seatGestureKind = 0;
                    _seatGestureCooldown = UnityEngine.Random.Range(5f, 13f);
                    return 0f;
                }

                float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_seatGestureTimer / 0.6f));
                float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_seatGestureDuration - _seatGestureTimer) / 0.6f));
                float w = Mathf.Min(fadeIn, fadeOut) * (1f - _talkingBlend);

                if (_talkingTimer > 0f && _talkingBlend > 0.96f)
                {
                    _seatGestureTimer = -1f;
                    _seatGestureKind = 0;
                    _seatGestureCooldown = UnityEngine.Random.Range(5f, 13f);
                    return 0f;
                }
                return w;
            }

            if (_talkingTimer > 0f) return 0f;

            _seatGestureCooldown -= dt;
            if (_seatGestureCooldown <= 0f)
            {
                _seatGestureKind = lookAroundOnly ? 4 : UnityEngine.Random.Range(1, 6);
                _seatGestureDuration = _seatGestureKind == 1
                    ? UnityEngine.Random.Range(6f, 12f)
                    : UnityEngine.Random.Range(2.6f, 4.8f);
                _seatGestureTimer = 0f;
            }
            return 0f;
        }

        private void ApplySeatedLookAt(float dt, ref Vector3 spineRot, ref Vector3 neckRot, ref Vector3 headRot)
        {
            float targetYaw = 0f;
            float targetPitch = 0f;
            if (_hasLookPoint)
            {
                Vector3 chestWorld = spine != null
                    ? spine.position + transform.up * (0.30f * transform.lossyScale.y)
                    : transform.TransformPoint(new Vector3(0f, 0.56f, 0f));
                Vector3 localDelta = transform.InverseTransformDirection(_lookWorldPoint - chestWorld);
                float horizDist = new Vector2(localDelta.x, localDelta.z).magnitude;
                if (horizDist > 0.10f)
                {
                    float rawYaw = Mathf.Atan2(localDelta.x, localDelta.z) * Mathf.Rad2Deg;
                    targetYaw = Mathf.Clamp(rawYaw, -70f, 70f);
                    targetPitch = Mathf.Clamp(-Mathf.Atan2(localDelta.y, horizDist) * Mathf.Rad2Deg, -25f, 25f);
                }
            }

            float blend = 1f - Mathf.Exp(-5f * dt);
            _smoothedLookYaw = Mathf.Lerp(_smoothedLookYaw, targetYaw, blend);
            _smoothedLookPitch = Mathf.Lerp(_smoothedLookPitch, targetPitch, blend);

            headRot.y += _smoothedLookYaw * 0.70f;
            headRot.x += _smoothedLookPitch * 0.70f;
            neckRot.y += _smoothedLookYaw * 0.20f;
            neckRot.x += _smoothedLookPitch * 0.30f;
            spineRot.y += _smoothedLookYaw * 0.10f;
        }

        private void AnimateSeatContact(float floor, float seated, float breath, float dt)
        {
            float shift = Mathf.Sin(seated * Mathf.PI);

            float gestureW = 0f;
            int gestureKind = 0;
            float gestureT = 0f;
            if (seated >= 0.98f && !_autoSeatActive)
            {
                gestureW = UpdateSeatedGesture(dt, lookAroundOnly: false);
                gestureKind = _seatGestureKind;
                gestureT = _seatGestureDuration > 0.01f ? Mathf.Clamp01(_seatGestureTimer / _seatGestureDuration) : 0f;
            }
            else if (_seatGestureTimer >= 0f)
            {
                _seatGestureTimer = -1f;
                _seatGestureKind = 0;
            }

            Vector3 spineRot = new Vector3(30f * shift - 5f * seated + breath, 0f, 0f);
            Vector3 neckRot = new Vector3(-8f * shift + 5f * seated, 0f, 0f);
            Vector3 headRot = new Vector3(12f * shift, 0f, 0f);

            if (gestureW > 0.001f)
            {
                if (gestureKind == 1)
                {
                    spineRot.x -= 5f * gestureW;
                }
                else if (gestureKind == 2)
                {
                    spineRot.x -= 11f * gestureW;
                    neckRot.x += 6f * gestureW;
                }
                else if (gestureKind == 3)
                {
                    spineRot.x += 24f * gestureW;
                    neckRot.x -= 8f * gestureW;
                    headRot.x -= 8f * gestureW;
                }
                else if (gestureKind == 4)
                {
                    float scan = Mathf.Sin(gestureT * TwoPi);
                    headRot.y += scan * 38f * gestureW;
                    spineRot.y += scan * 6f * gestureW;
                }
                else if (gestureKind == 5)
                {
                    spineRot.x += 12f * gestureW;
                }
            }

            if (_talkingBlend > 0.02f)
            {
                float talkTime = _idleTime + _noiseSeed;
                headRot.x += Mathf.Sin(talkTime * 4.1f) * 4f * _talkingBlend;
                spineRot.x += 6f * _talkingBlend;
            }

            ApplySeatedLookAt(dt, ref spineRot, ref neckRot, ref headRot);

            hips.localPosition = new Vector3(0f, Mathf.Lerp(hipsBaseY, 0.08f, seated), 0.09f * shift);
            SmoothRotate(hips, Vector3.zero, 15f, dt);
            SmoothRotate(spine, spineRot, 16f, dt);
            SmoothRotate(neck, neckRot, 14f, dt);
            SmoothRotate(head, headRot, 10f, dt);

            float scaleY = transform.lossyScale.y;
            float footSlide = (gestureKind == 2 && gestureW > 0.001f) ? 0.14f * gestureW : 0f;
            Vector3 left = transform.TransformPoint(new Vector3(-0.14f, 0f, Mathf.Lerp(0.04f, 0.46f, seated) + footSlide));
            Vector3 right = transform.TransformPoint(new Vector3(0.14f, 0f, Mathf.Lerp(0.09f, 0.44f, seated) + footSlide));
            left.y = right.y = floor + AnkleHeight * scaleY;

            if (gestureKind == 1 && gestureW > 0.001f)
            {
                Vector3 crossedRight = transform.TransformPoint(new Vector3(-0.06f, 0f, 0.50f));
                crossedRight.y = floor + AnkleHeight * scaleY + 0.30f * scaleY;
                right = Vector3.Lerp(right, crossedRight, gestureW);
            }

            if (lHip != null && rHip != null && lKnee != null && rKnee != null && lAnkle != null && rAnkle != null)
            {
                float drop = Mathf.Max(0f, Mathf.Max(LegExtension(lHip, lKnee, lAnkle, left),
                    LegExtension(rHip, rKnee, rAnkle, right)));
                hips.position -= transform.up * drop;
                AimLimb(lHip, lKnee, left, transform.forward, lAnkle.localPosition.magnitude);
                AimLimb(rHip, rKnee, right, transform.forward, rAnkle.localPosition.magnitude);
                lAnkle.rotation = transform.rotation;
                rAnkle.rotation = gestureKind == 1 && gestureW > 0.001f
                    ? transform.rotation * Quaternion.Euler(14f * gestureW, -18f * gestureW, 0f)
                    : transform.rotation;
            }

            if (lKnee != null && rKnee != null)
            {
                Vector3 lRest = Vector3.Lerp(transform.TransformPoint(new Vector3(-0.27f, 0.82f, 0.06f)),
                    lKnee.position + transform.up * 0.10f, seated);
                Vector3 rRest = Vector3.Lerp(transform.TransformPoint(new Vector3(0.27f, 0.82f, 0.06f)),
                    rKnee.position + transform.up * 0.10f, seated);

                // Reach back to seat edge to support body weight during sit-down / stand-up transition
                float edgeSupport = Mathf.Clamp01(shift * 1.3f);
                if (edgeSupport > 0.001f)
                {
                    lRest = Vector3.Lerp(lRest, transform.TransformPoint(new Vector3(-0.25f, 0.04f, -0.04f)), edgeSupport);
                    rRest = Vector3.Lerp(rRest, transform.TransformPoint(new Vector3(0.25f, 0.04f, -0.04f)), edgeSupport);
                }

                if (gestureW > 0.001f)
                {
                    if (gestureKind == 1)
                    {
                        lRest = Vector3.Lerp(lRest, rKnee.position + transform.up * 0.08f, gestureW);
                    }
                    else if (gestureKind == 2)
                    {
                        lRest = Vector3.Lerp(lRest, transform.TransformPoint(new Vector3(-0.24f, 0.10f, 0.06f)), gestureW);
                        rRest = Vector3.Lerp(rRest, transform.TransformPoint(new Vector3(0.24f, 0.10f, 0.06f)), gestureW);
                    }
                    else if (gestureKind == 3)
                    {
                        Vector3 midKnees = (lKnee.position + rKnee.position) * 0.5f + transform.forward * 0.10f + transform.up * 0.16f;
                        lRest = Vector3.Lerp(lRest, midKnees - transform.right * 0.04f, gestureW);
                        rRest = Vector3.Lerp(rRest, midKnees + transform.right * 0.04f, gestureW);
                    }
                    else if (gestureKind == 5 && head != null)
                    {
                        Vector3 chin = head.position + transform.forward * 0.11f - transform.up * 0.11f;
                        rRest = Vector3.Lerp(rRest, chin, gestureW);
                    }
                }

                if (_talkingBlend > 0.02f)
                {
                    float talkTime = _idleTime + _noiseSeed;
                    Vector3 talkR = rRest + transform.up * (0.22f + 0.06f * Mathf.Sin(talkTime * 6.2f)) + transform.forward * 0.16f;
                    Vector3 talkL = lRest + transform.up * (0.11f + 0.03f * Mathf.Sin(talkTime * 6.2f + 1.5f)) + transform.forward * 0.08f;
                    rRest = Vector3.Lerp(rRest, talkR, _talkingBlend);
                    lRest = Vector3.Lerp(lRest, talkL, _talkingBlend);
                }

                AimHand(lShoulder, lElbow, lRest, -transform.right, 1f);
                AimHand(rShoulder, rElbow, rRest, transform.right, 1f);
            }
        }

        private Vector3 SwimHand(float side, float phase)
        {
            float stroke = Mathf.Repeat(phase / TwoPi, 1f);
            const float pullEnd = 0.62f;
            float pull = Mathf.Clamp01(stroke / pullEnd);
            float recovery = Mathf.InverseLerp(pullEnd, 1f, stroke);
            float forward = stroke < pullEnd ? Mathf.Lerp(1.04f, -0.07f, pull) : Mathf.SmoothStep(-0.07f, 1.04f, recovery);
            float height = 0.83f - 0.30f * Mathf.Sin(pull * Mathf.PI) + 0.32f * Mathf.Sin(recovery * Mathf.PI);
            float width = 0.22f + 0.15f * Mathf.Sin(recovery * Mathf.PI);
            return transform.TransformPoint(new Vector3(side * width, height, forward));
        }

        public void SetCycling(bool active)
        {
            _cycling = active;
            _motorcyclePose = false;
            if (!active)
            {
                _cycleMount = 1f;
                _cycleFootPeg = 1f;
                _cycleSteerNorm = 0f;
                _cycleAccelLean = 0f;
            }
        }

        public void SetCycling(bool active, Vector3 leftPedal, Vector3 rightPedal, Vector3 leftGrip, Vector3 rightGrip,
            float mount = 1f, float footPeg = 1f, float steerNorm = 0f, float accelLean = 0f)
        {
            _cycling = active;
            _motorcyclePose = false;
            _leftPedal = leftPedal;
            _rightPedal = rightPedal;
            _leftGrip = leftGrip;
            _rightGrip = rightGrip;
            _cycleMount = Mathf.Clamp01(mount);
            _cycleFootPeg = Mathf.Clamp01(footPeg);
            _cycleSteerNorm = Mathf.Clamp(steerNorm, -1f, 1f);
            _cycleAccelLean = Mathf.Clamp(accelLean, -1f, 1f);
        }

        public void SetMotorcyclePose(Vector3 leftPeg, Vector3 rightPeg, Vector3 leftGrip, Vector3 rightGrip,
            float mount, float footPeg, float steerNorm = 0f, float accelLean = 0f)
        {
            SetCycling(true, leftPeg, rightPeg, leftGrip, rightGrip, mount, footPeg, steerNorm, accelLean);
            _motorcyclePose = true;
        }

        public void SetTennisRacket(Transform racket)
        {
            _tennisRacket = rElbow != null ? racket : null;
            if (_tennisRacket != null) _tennisRacket.SetParent(rElbow, false);
            ClearTennisSwing();
            _tennisLeftHand = null;
        }

        public void SetTennisLeftHand(Vector3? point)
        {
            _tennisLeftHand = point;
        }

        public void PlayTennisSwing(Vector3 contact, float secondsToContact, bool serving = false)
        {
            _tennisContact = contact;
            _tennisContactAt = Time.time + secondsToContact;
            _tennisServing = serving;
            _tennisBackhand = !serving && transform.InverseTransformPoint(contact).x < -0.05f;
        }

        public void ClearTennisSwing()
        {
            _tennisContactAt = float.NegativeInfinity;
            _tennisServing = false;
            _tennisBackhand = false;
        }

        public void SetShelfReach(bool active, Vector3 worldPos, float weight)
        {
            _hasShelfReach = active;
            _shelfReachWorldPos = worldPos;
            _shelfReachWeight = active ? Mathf.Clamp01(weight) : 0f;
        }

        public void SetConsumePose(int mode, float weight, float cyclePhase)
        {
            _consumeMode = mode;
            _consumeWeight = mode > 0 ? Mathf.Clamp01(weight) : 0f;
            _consumeCyclePhase = Mathf.Clamp01(cyclePhase);
        }

        private void ApplyTennisRacketPose(float swingTime)
        {
            Vector3 readyHand = transform.TransformPoint(new Vector3(0.24f, 1.06f, 0.30f));
            Vector3 readyAxis = new Vector3(0.42f, 0.86f, 0.28f);
            Vector3 axis = readyAxis;
            Vector3 hand = readyHand;
            Vector3 balanceLeft = transform.TransformPoint(new Vector3(-0.27f, 1.04f, 0.30f));

            if (swingTime >= -TennisWindup && swingTime < TennisFollowThrough)
            {
                Vector3 strikeAxis = _tennisServing
                    ? new Vector3(0.32f, 0.92f, 0.22f)
                    : (_tennisBackhand ? new Vector3(-0.88f, 0.28f, 0.38f) : new Vector3(0.89f, 0.25f, 0.38f));
                Vector3 strikeUp = transform.TransformDirection(strikeAxis.normalized);
                Vector3 strikeFace = Vector3.ProjectOnPlane(transform.forward, strikeUp).normalized;
                Vector3 strikeHand = _tennisContact - strikeFace * TennisCourt.BallRadius -
                    strikeUp * (RacketHeadFromHand * transform.lossyScale.y);

                if (swingTime < 0f)
                {
                    const float backswingTime = -0.12f;
                    Vector3 backswingHand = transform.TransformPoint(_tennisServing
                        ? new Vector3(0.30f, 1.56f, -0.18f)
                        : (_tennisBackhand ? new Vector3(-0.34f, 0.96f, -0.16f) : new Vector3(0.46f, 1.02f, -0.26f)));
                    Vector3 backswingAxis = _tennisServing
                        ? new Vector3(0.42f, 0.32f, -0.85f)
                        : (_tennisBackhand ? new Vector3(-0.72f, 0.42f, -0.55f) : new Vector3(0.66f, 0.45f, -0.60f));
                    if (swingTime < backswingTime)
                    {
                        float windup = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-TennisWindup, backswingTime, swingTime));
                        hand = Vector3.Lerp(readyHand, backswingHand, windup);
                        axis = Vector3.Slerp(readyAxis, backswingAxis, windup);
                        balanceLeft = transform.TransformPoint(Vector3.Lerp(
                            new Vector3(-0.27f, 1.04f, 0.30f),
                            _tennisBackhand ? new Vector3(-0.32f, 1.08f, 0.08f) : new Vector3(-0.42f, 1.16f, 0.42f),
                            windup));
                    }
                    else
                    {
                        float strike = Mathf.InverseLerp(backswingTime, 0f, swingTime);
                        float ease = strike * strike;
                        hand = Vector3.Lerp(backswingHand, strikeHand, ease);
                        axis = Vector3.Slerp(backswingAxis, strikeAxis, ease);
                        Vector3 backBalance = _tennisBackhand
                            ? new Vector3(-0.32f, 1.08f, 0.08f)
                            : new Vector3(-0.42f, 1.16f, 0.42f);
                        balanceLeft = transform.TransformPoint(Vector3.Lerp(
                            backBalance,
                            new Vector3(-0.44f, 1.05f, 0.04f),
                            ease));
                    }
                }
                else
                {
                    Vector3 finishHand = transform.TransformPoint(_tennisServing
                        ? new Vector3(-0.22f, 0.88f, 0.36f)
                        : (_tennisBackhand ? new Vector3(0.38f, 1.32f, 0.34f) : new Vector3(-0.18f, 1.32f, 0.34f)));
                    Vector3 finishAxis = _tennisServing
                        ? new Vector3(-0.62f, -0.58f, 0.52f)
                        : (_tennisBackhand ? new Vector3(0.78f, 0.58f, 0.24f) : new Vector3(-0.78f, 0.60f, 0.22f));
                    const float finishTime = 0.14f;
                    if (swingTime < finishTime)
                    {
                        float follow = Mathf.SmoothStep(0f, 1f, swingTime / finishTime);
                        hand = Vector3.Lerp(strikeHand, finishHand, follow);
                        axis = Vector3.Slerp(strikeAxis, finishAxis, follow);
                        balanceLeft = transform.TransformPoint(Vector3.Lerp(
                            new Vector3(-0.44f, 1.05f, 0.04f),
                            new Vector3(-0.44f, 1.06f, -0.06f),
                            follow));
                    }
                    else
                    {
                        float recover = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(finishTime, TennisFollowThrough, swingTime));
                        hand = Vector3.Lerp(finishHand, readyHand, recover);
                        axis = Vector3.Slerp(finishAxis, readyAxis, recover);
                        balanceLeft = transform.TransformPoint(Vector3.Lerp(new Vector3(-0.44f, 1.06f, -0.06f), new Vector3(-0.27f, 1.04f, 0.30f), recover));
                    }
                }
            }

            AimHand(rShoulder, rElbow, hand, _tennisBackhand ? -transform.right : transform.right, 1f);
            AimHand(lShoulder, lElbow, _tennisLeftHand ?? balanceLeft, -transform.right, 1f);
            Vector3 racketUp = transform.TransformDirection(axis.normalized);
            Vector3 racketForward = Vector3.ProjectOnPlane(transform.forward, racketUp).normalized;
            Quaternion rotation = Quaternion.LookRotation(racketForward, racketUp);
            Vector3 handCenter = rElbow.TransformPoint(Vector3.down * HandLength);
            _tennisRacket.SetPositionAndRotation(handCenter - racketUp * (RacketHandleCenter * transform.lossyScale.y), rotation);
        }

        private void AimHand(Transform shoulder, Transform elbow, Vector3 target, Vector3 outward, float weight)
        {
            if (shoulder == null || elbow == null) return;
            Quaternion shoulderPose = shoulder.localRotation;
            Quaternion elbowPose = elbow.localRotation;
            AimLimb(shoulder, elbow, target, -transform.up + outward * 0.55f, HandLength);
            shoulder.localRotation = Quaternion.Slerp(shoulderPose, shoulder.localRotation, weight);
            elbow.localRotation = Quaternion.Slerp(elbowPose, elbow.localRotation, weight);
        }

        private void AnimateCycling(float dt)
        {
            float mount = _cycleMount;
            float footPeg = _cycleFootPeg;
            _cycleSmoothedSteer = Mathf.Lerp(_cycleSmoothedSteer, _cycleSteerNorm, 1f - Mathf.Exp(-10f * dt));
            _cycleSmoothedAccel = Mathf.Lerp(_cycleSmoothedAccel, _cycleAccelLean, 1f - Mathf.Exp(-8f * dt));
            float breathWave = Mathf.Sin((_idleTime + _noiseSeed) * TwoPi * 0.28f);
            float breath = breathWave * 0.7f;

            Vector3 seatedHips = _motorcyclePose
                ? new Vector3(Mathf.Lerp(-0.11f, 0f, footPeg), Mathf.Lerp(0.91f, 0.97f, footPeg), -0.25f)
                : new Vector3(Mathf.Lerp(-0.07f, 0f, footPeg), Mathf.Lerp(hipsBaseY + 0.04f, hipsBaseY + 0.115f, footPeg), -0.235f);
            Vector3 targetHipsPos = Vector3.Lerp(new Vector3(0f, hipsBaseY, 0f), seatedHips, mount);
            hips.localPosition = Vector3.Lerp(hips.localPosition, targetHipsPos, 1f - Mathf.Exp(-18f * dt));
            SmoothRotate(hips, new Vector3(0f, 0f, -_cycleSmoothedSteer * (_motorcyclePose ? 4f : 3f) * mount), 14f, dt);

            float lean = ((_motorcyclePose ? 19f : 31f) + _cycleSmoothedAccel * (_motorcyclePose ? 8.5f : 5.5f) + breath) * mount;
            SmoothRotate(spine, new Vector3(lean, _cycleSmoothedSteer * 7f * mount, -_cycleSmoothedSteer * 4.5f * mount), 12f, dt);
            SmoothRotate(neck, new Vector3(-lean * 0.65f, _cycleSmoothedSteer * 5f * mount, 0f), 12f, dt);
            SmoothRotate(head, new Vector3((-7f - _cycleSmoothedAccel * 2.5f) * mount, _cycleSmoothedSteer * 18f * mount, _cycleSmoothedSteer * 4f * mount), 10f, dt);

            if (chestVisual != null)
            {
                float breathScale = 1f + breathWave * 0.015f;
                Vector3 targetScale = new Vector3(_chestBaseScale.x * breathScale, _chestBaseScale.y, _chestBaseScale.z * breathScale);
                chestVisual.localScale = Vector3.Lerp(chestVisual.localScale, targetScale, 1f - Mathf.Exp(-6f * dt));
            }

            Vector3 ankleOffset = transform.up * 0.065f - transform.forward * 0.04f;
            Vector3 leftFoot = Vector3.Lerp(transform.TransformPoint(new Vector3(-0.13f, AnkleHeight, 0f)),
                _leftPedal + ankleOffset, mount);
            Vector3 rightFoot = Vector3.Lerp(transform.TransformPoint(new Vector3(0.13f,
                AnkleHeight + Mathf.Sin(mount * Mathf.PI) * 0.34f, 0f)), _rightPedal + ankleOffset, mount);
            AimLimb(lHip, lKnee, leftFoot, transform.forward, 0.42f);
            AimLimb(rHip, rKnee, rightFoot, transform.forward, 0.42f);
            if (lAnkle != null) lAnkle.rotation = transform.rotation * Quaternion.Euler(Mathf.Lerp(0f, 6f, footPeg * mount), 0f, 0f);
            if (rAnkle != null) rAnkle.rotation = transform.rotation * Quaternion.Euler(6f * mount, 0f, 0f);
            Vector3 leftHand = Vector3.Lerp(transform.TransformPoint(new Vector3(-0.26f, 0.83f, 0.08f)), _leftGrip, mount);
            Vector3 rightHand = Vector3.Lerp(transform.TransformPoint(new Vector3(0.26f, 0.83f, 0.08f)), _rightGrip, mount);
            AimHand(lShoulder, lElbow, leftHand, -transform.right, 1f);
            AimHand(rShoulder, rElbow, rightHand, transform.right, 1f);
        }

        private void AimLimb(Transform first, Transform second, Vector3 target, Vector3 bendToward, float lowerLength)
        {
            if (first == null || second == null) return;

            Vector3 start = first.position;
            Vector3 delta = target - start;
            Vector3 direction = delta.sqrMagnitude > 0.000001f ? delta.normalized : -transform.up;
            float upperLength = second.localPosition.magnitude * transform.lossyScale.y;
            lowerLength *= transform.lossyScale.y;
            float distance = Mathf.Clamp(delta.magnitude,
                Mathf.Abs(upperLength - lowerLength) + 0.001f, upperLength + lowerLength - 0.001f);
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 pole = Vector3.ProjectOnPlane(bendToward, direction).normalized;
            if (pole.sqrMagnitude < 0.0001f) pole = Vector3.ProjectOnPlane(transform.right, direction).normalized;
            Vector3 elbowOrKnee = start + direction * along + pole * bend;
            AimBone(first, elbowOrKnee - start);
            AimBone(second, start + direction * distance - second.position);
        }

        private void AimBone(Transform bone, Vector3 direction)
        {
            Vector3 forward = Vector3.Cross(direction, transform.right);
            if (forward.sqrMagnitude < 0.000001f) forward = Vector3.ProjectOnPlane(transform.forward, direction);
            bone.rotation = Quaternion.LookRotation(forward, -direction);
        }

        private static void SmoothRotate(Transform target, Vector3 targetEuler, float damp, float dt)
        {
            if (target == null) return;
            target.localRotation = Quaternion.Slerp(
                target.localRotation,
                Quaternion.Euler(targetEuler),
                1f - Mathf.Exp(-damp * dt));
        }
    }
}
