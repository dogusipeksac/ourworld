using System;
using Gridroam.World;
using UnityEngine;

namespace Gridroam.Player
{
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

        private const float WalkSpeedRef = 3.4f;
        private const float RunSpeedRef = 6.2f;
        private const float WaveDuration = 1.5f;
        private const float LandSquatDuration = 0.15f;
        private const float HandLength = 0.30f;
        private const float AnkleHeight = 0.081f;
        private const float TwoPi = 2f * Mathf.PI;

        private float _speedMs;
        private Vector3 _travelDirection = Vector3.forward;
        private bool _sampleGround;
        private float _gaitWeight;
        private bool _grounded = true;
        private bool _wasGrounded = true;
        private float _airborneTimer;
        private float _landSquatTimer = -1f;
        private float _verticalVelocity;
        private float _steerAngleDeg;
        private Transform _steeringWheel;
        private float? _seatFloor;
        private float _seatAmount;
        private Transform _doorHandle;
        private float _doorReachStarted;
        private float _doorReachDuration;
        private bool _doorReachRight;
        private bool _cycling;
        private bool _motorcyclePose;
        private float _motorcycleMount;
        private float _motorcycleFootPeg;
        private Vector3 _leftPedal, _rightPedal, _leftGrip, _rightGrip;
        private Transform _tennisRacket;
        private Vector3 _tennisContact;
        private float _tennisContactAt = float.NegativeInfinity;
        private bool _tennisServing;
        private Vector3? _tennisLeftHand;
        private const float TennisWindup = 0.19f;
        private const float TennisFollowThrough = 0.29f;
        private const float RacketHandleCenter = 0.09f;
        private const float RacketHeadFromHand = 0.39f;

        private float _phase;
        private int _lastStepCount;
        private float _idleTime;
        private float _noiseSeed;
        private float _prevYaw;
        private float _smoothedTurnLean;
        // v13 animation polish: acceleration lean, idle weight shifts, run crouch, fall flail
        private float _prevSpeedMs;
        private float _smoothedAccelLean;
        private float _idleShift;
        private float _idleShiftTarget;
        private float _idleShiftCountdown = 7f;
        private float _waveTimer = -1f;
        private float _punchTimer = -1f;
        private int _combatMoveIndex;
        private float _hitReactTimer = -1f;
        private float _knockdownTimer = -1f;
        private float _vaultTimer = -1f;
        private Vector3? _vaultSupport;
        private bool _isSwimming;
        private float _swimSpeed;
        private float _swimPhase;
        private int _prayerStage;
        private float _prayerSelamYaw;
        private bool _prayerHandsInit;
        private Vector3 _prayerLeftHandLocal;
        private Vector3 _prayerRightHandLocal;
        private bool _isLyingDown;
        private float _startledTimer = -1f;
        private float _startledDuration = 1.1f;
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

        public Transform HeadTransform => head;
        public Transform RightForearm => rElbow;
        public Transform LeftForearm => lElbow;

        public bool IsSitting => isSitting;
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
                _landSquatTimer = 0f;
                OnLanded?.Invoke();
            }

            _wasGrounded = grounded;
            if (grounded)
            {
                _airborneTimer = 0f;
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
            _waveTimer = 0f;
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
            _landSquatTimer = 0f;
            _vaultTimer = -1f;
            OnLanded?.Invoke();
        }

        public void SetSwimming(bool swimming, float swimSpeed)
        {
            _isSwimming = swimming;
            _swimSpeed = Mathf.Max(0f, swimSpeed);
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
            }
            else
            {
                _prayerHandsInit = false;
            }
        }

        public void SetLyingDown(bool lying)
        {
            _isLyingDown = lying;
            if (lying)
            {
                _waveTimer = -1f;
                _punchTimer = -1f;
                _prayerStage = 0;
            }
        }

        public void PlayStartled(float duration = 1.1f)
        {
            _startledDuration = Mathf.Max(0.35f, duration);
            _startledTimer = 0f;
        }

        public void SetSitting(bool sitting, bool driving = false)
        {
            isSitting = sitting;
            isDrivingSeat = sitting && driving;
            if (!sitting || driving) _seatFloor = null;
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

        public void SetSteeringWheel(Transform wheel)
        {
            _steeringWheel = wheel;
        }

        public void SetSeatContact(float floorHeight, float seatedAmount)
        {
            _seatFloor = floorHeight;
            _seatAmount = seatedAmount;
        }

        public void PlayDoorReach(Transform handle, float duration)
        {
            _doorHandle = handle;
            _doorReachStarted = Time.time;
            _doorReachDuration = duration;
            _doorReachRight = transform.InverseTransformPoint(handle.position).x >= -0.08f;
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
                AnimateCycling();
                return;
            }

            _idleTime += dt;
            if (!_grounded)
            {
                _airborneTimer += dt;
            }

            if (_landSquatTimer >= 0f)
            {
                _landSquatTimer += dt;
                if (_landSquatTimer >= LandSquatDuration)
                {
                    _landSquatTimer = -1f;
                }
            }

            if (_startledTimer >= 0f)
            {
                _startledTimer += dt;
                if (_startledTimer >= _startledDuration)
                {
                    _startledTimer = -1f;
                }
            }

            if (autoRandomWave && !isSitting && _speedMs < 0.1f)
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

            if (isSitting)
            {
                AnimateSitting(dt);
                return;
            }

            // Track yaw change rate of root for turning lean into turns (+/- 8 deg)
            float currentYaw = transform.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(_prevYaw, currentYaw);
            _prevYaw = currentYaw;
            float yawRate = yawDelta / dt;
            float moveFactor = Mathf.Clamp01(_speedMs / WalkSpeedRef);
            float targetTurnLean = Mathf.Clamp(-yawRate * 0.06f, -8f, 8f) * moveFactor;
            _smoothedTurnLean = Mathf.Lerp(_smoothedTurnLean, targetTurnLean, 1f - Mathf.Exp(-8f * dt));

            // Compute idle / walk / run blends
            float moveBlend = Mathf.Clamp01((_speedMs - 0.08f) / 1.2f);
            float runBlend = Mathf.InverseLerp(WalkSpeedRef, RunSpeedRef, _speedMs);

            _gaitWeight = Mathf.MoveTowards(_gaitWeight, moveBlend, 9f * dt);
            bool tennisStance = _tennisRacket != null;
            float stance = tennisStance ? 0.54f : Mathf.Lerp(0.61f, 0.44f, runBlend);
            float strideLength = tennisStance ? 1.12f : Mathf.Lerp(1.22f, 1.72f, runBlend);
            strideLength *= Mathf.Lerp(0.68f, 1f, _gaitWeight);

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

            // --- Idle components ---
            float breathWave = Mathf.Sin(_idleTime * Mathf.PI * 2f * 0.25f);
            float idleChestPitch = breathWave * 1.5f;
            float idleWeightShiftX = Mathf.Sin(_idleTime * Mathf.PI * 2f * 0.2f) * 0.015f;
            float headNoise = (Mathf.PerlinNoise(_noiseSeed, _idleTime * 0.18f) - 0.5f) * 2f;
            float idleHeadYaw = headNoise * 10f;

            // --- Head-look IK-lite toward nearest interactable within 4 m (max 60 deg yaw) ---
            float desiredLookYaw = 0f;
            float desiredLookPitch = 0f;
            bool useLookIk = false;
            if (_hasLookPoint && _grounded)
            {
                Vector3 localTarget = transform.InverseTransformPoint(_lookWorldPoint) - new Vector3(0f, 1.54f, 0f);
                float horizDist = new Vector2(localTarget.x, localTarget.z).magnitude;
                if (horizDist > 0.12f)
                {
                    float rawYaw = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(rawYaw) <= 100f)
                    {
                        desiredLookYaw = Mathf.Clamp(rawYaw, -60f, 60f);
                        desiredLookPitch = Mathf.Clamp(-Mathf.Atan2(localTarget.y, horizDist) * Mathf.Rad2Deg, -18f, 18f);
                        useLookIk = true;
                    }
                }
            }

            float lookBlendRate = 1f - Mathf.Exp(-7.5f * dt);
            _smoothedLookYaw = Mathf.Lerp(_smoothedLookYaw, useLookIk ? desiredLookYaw : 0f, lookBlendRate);
            _smoothedLookPitch = Mathf.Lerp(_smoothedLookPitch, useLookIk ? desiredLookPitch : 0f, lookBlendRate);

            // The opposite arm is forward at heel strike. The torso counter-rotates against the pelvis.
            float armCycle = Mathf.Cos(_phase - 0.19f);
            float armAmp = Mathf.Lerp(17f, 39f, runBlend);
            float elbowBaseBend = Mathf.Lerp(16f, 65f, runBlend);
            float walkShoulderL = armCycle * armAmp;
            float walkShoulderR = -walkShoulderL;
            float walkElbowL = -(elbowBaseBend + Mathf.Max(0f, -armCycle) * 15f);
            float walkElbowR = -(elbowBaseBend + Mathf.Max(0f, armCycle) * 15f);
            float torsoForwardLean = Mathf.Lerp(3f, 10f, runBlend);
            float walkBobY = -Mathf.Cos(_phase * 2f) * Mathf.Lerp(0.017f, 0.035f, runBlend);
            float walkHipYaw = Mathf.Cos(_phase) * Mathf.Lerp(5f, 8f, runBlend);
            float walkHipRoll = -Mathf.Sin(_phase) * Mathf.Lerp(2.5f, 3.5f, runBlend);
            float walkHipSwayX = -Mathf.Sin(_phase) * Mathf.Lerp(0.017f, 0.012f, runBlend);

            // Blend Idle <-> Locomotion
            Vector3 targetHipsPos = new Vector3(
                Mathf.Lerp(idleWeightShiftX, walkHipSwayX, moveBlend),
                hipsBaseY - _gaitWeight * 0.064f + walkBobY * moveBlend,
                0f);

            float defaultHeadYaw = Mathf.Lerp(idleHeadYaw, 0f, moveBlend);
            float finalHeadYaw = useLookIk || Mathf.Abs(_smoothedLookYaw) > 0.5f
                ? (_smoothedLookYaw * 0.75f)
                : defaultHeadYaw;

            Vector3 targetHipsRot = new Vector3(0f, walkHipYaw * moveBlend, walkHipRoll * moveBlend);
            Vector3 targetSpineRot = new Vector3(
                Mathf.Lerp(idleChestPitch, torsoForwardLean, moveBlend),
                -walkHipYaw * 1.15f * moveBlend,
                _smoothedTurnLean - walkHipRoll * 0.65f * moveBlend);
            Vector3 targetNeckRot = new Vector3(
                -targetSpineRot.x * 0.35f + _smoothedLookPitch * 0.25f,
                _smoothedLookYaw * 0.25f,
                -_smoothedTurnLean * 0.35f);
            Vector3 targetHeadRot = new Vector3(_smoothedLookPitch * 0.75f, finalHeadYaw, 0f);

            Vector3 targetLHipRot = new Vector3(0f, 0f, -3.2f);
            Vector3 targetRHipRot = new Vector3(0f, 0f, 3.2f);
            Vector3 targetLKneeRot = new Vector3(3.5f, 0f, 0f);
            Vector3 targetRKneeRot = targetLKneeRot;
            Vector3 targetLAnkleRot = Vector3.zero;
            Vector3 targetRAnkleRot = Vector3.zero;

            float idleArmBreath = breathWave * 1.4f;
            Vector3 targetLShoulderRot = new Vector3(Mathf.Lerp(3.5f + idleArmBreath, walkShoulderL, moveBlend), 0f, Mathf.Lerp(-9.5f, -4.5f, moveBlend));
            Vector3 targetRShoulderRot = new Vector3(Mathf.Lerp(3.5f + idleArmBreath, walkShoulderR, moveBlend), 0f, Mathf.Lerp(9.5f, 4.5f, moveBlend));
            Vector3 targetLElbowRot = new Vector3(Mathf.Lerp(-16f, walkElbowL, moveBlend), 0f, 0f);
            Vector3 targetRElbowRot = new Vector3(Mathf.Lerp(-16f, walkElbowR, moveBlend), 0f, 0f);

            // --- v13 polish: lean into acceleration, settle back when stopping ---
            float accel = (_speedMs - _prevSpeedMs) / dt;
            _prevSpeedMs = _speedMs;
            float targetAccelLean = _grounded ? Mathf.Clamp(accel * 1.4f, -7f, 9f) : 0f;
            _smoothedAccelLean = Mathf.Lerp(_smoothedAccelLean, targetAccelLean, 1f - Mathf.Exp(-6f * dt));
            targetSpineRot.x += _smoothedAccelLean;
            targetNeckRot.x -= _smoothedAccelLean * 0.4f; // keep gaze level

            // --- v13 polish: slightly lower, more athletic pelvis while running (reduces stiff-leg float) ---
            targetHipsPos.y -= 0.035f * runBlend * moveBlend;

            // --- v13 polish: natural idle weight shift from leg to leg (contrapposto) every few seconds ---
            if (moveBlend < 0.05f && _grounded)
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

            // --- Airborne override ---
            if (!_grounded)
            {
                float tuckScale = Mathf.Clamp01(0.75f + Mathf.Clamp(_verticalVelocity * 0.05f, -0.2f, 0.25f));
                targetHipsPos.y = hipsBaseY + 0.04f;
                targetLHipRot = new Vector3(-28f * tuckScale, 0f, -4f);
                targetRHipRot = new Vector3(-22f * tuckScale, 0f, 4f);
                targetLKneeRot = new Vector3(50f, 0f, 0f);
                targetRKneeRot = new Vector3(50f, 0f, 0f);
                targetLAnkleRot = new Vector3(14f, 0f, 0f);
                targetRAnkleRot = new Vector3(14f, 0f, 0f);

                targetLShoulderRot = new Vector3(-18f, 0f, -34f);
                targetRShoulderRot = new Vector3(-18f, 0f, 34f);
                targetLElbowRot = new Vector3(-32f, 0f, 0f);
                targetRElbowRot = new Vector3(-32f, 0f, 0f);
                targetSpineRot.x = 6f;
            }
            // --- Jump landing squat (knees 35 deg for 0.15 s) ---
            else if (_landSquatTimer >= 0f)
            {
                float squatNorm = Mathf.Clamp01(_landSquatTimer / LandSquatDuration);
                float squatEnv = Mathf.Sin(squatNorm * Mathf.PI);
                targetHipsPos.y -= 0.11f * squatEnv;
                targetLHipRot.x -= 18f * squatEnv;
                targetRHipRot.x -= 18f * squatEnv;
                targetLKneeRot.x = Mathf.Max(targetLKneeRot.x, 35f * squatEnv);
                targetRKneeRot.x = Mathf.Max(targetRKneeRot.x, 35f * squatEnv);
                targetLAnkleRot.x -= 17f * squatEnv;
                targetRAnkleRot.x -= 17f * squatEnv;
                targetSpineRot.x += 7f * squatEnv;
            }

            // --- v13 polish: arms flail and legs reach when falling from height ---
            if (!_grounded && _verticalVelocity < -3.5f && _airborneTimer > 0.35f)
            {
                float fall = Mathf.Clamp01((-_verticalVelocity - 3.5f) / 6f);
                float flail = Mathf.Sin(_idleTime * 14f);
                targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-95f + flail * 18f, 0f, -55f), fall);
                targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-95f - flail * 18f, 0f, 55f), fall);
                targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-20f, 0f, 0f), fall);
                targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-20f, 0f, 0f), fall);
                targetLHipRot = Vector3.Lerp(targetLHipRot, new Vector3(-10f - flail * 8f, 0f, -6f), fall);
                targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(-10f + flail * 8f, 0f, 6f), fall);
                targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(22f, 0f, 0f), fall);
                targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(22f, 0f, 0f), fall);
                targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -6f, fall);
            }

            // --- Bed sleep lying-down override ---
            if (_isLyingDown)
            {
                targetHipsPos = new Vector3(0f, 0.16f, 0f);
                targetHipsRot = new Vector3(-90f, 0f, 0f);
                targetSpineRot = new Vector3(2f + idleChestPitch * 0.4f, 0f, 0f);
                targetNeckRot = new Vector3(6f, 0f, 0f);
                targetHeadRot = new Vector3(10f, 6f, 0f);
                targetLShoulderRot = new Vector3(4f, 0f, -10f);
                targetRShoulderRot = new Vector3(4f, 0f, 10f);
                targetLElbowRot = new Vector3(-16f, 0f, 0f);
                targetRElbowRot = new Vector3(-16f, 0f, 0f);
                targetLHipRot = new Vector3(2f, 0f, -4f);
                targetRHipRot = new Vector3(2f, 0f, 4f);
                targetLKneeRot = new Vector3(6f, 0f, 0f);
                targetRKneeRot = new Vector3(6f, 0f, 0f);
                targetLAnkleRot = new Vector3(18f, 0f, 0f);
                targetRAnkleRot = new Vector3(18f, 0f, 0f);
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
            // --- Swimming override (front-crawl freestyle when moving, treading water when idle) ---
            else if (_isSwimming)
            {
                float swimBlend = Mathf.Clamp01(_swimSpeed / 1.8f);
                _swimPhase += dt * Mathf.Lerp(2.8f, 6.4f, swimBlend);
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
            // --- Parkour Vault hurdle animation (0.42 s) ---
            else if (_vaultTimer >= 0f)
            {
                float vNorm = Mathf.Clamp01(_vaultTimer / VaultDuration);
                float vEnv = Mathf.Sin(vNorm * Mathf.PI);
                targetHipsPos.y = hipsBaseY - 0.11f * vEnv;
                targetSpineRot = new Vector3(49f * vEnv, -12f * vEnv, 14f * vEnv);
                targetLShoulderRot = new Vector3(-86f * vEnv, 12f * vEnv, -16f * vEnv);
                targetRShoulderRot = new Vector3(-72f * vEnv, -12f * vEnv, 24f * vEnv);
                targetLElbowRot = new Vector3(-24f * vEnv, 0f, 0f);
                targetRElbowRot = new Vector3(-34f * vEnv, 0f, 0f);
                float leadingLeg = Mathf.Sin(Mathf.Clamp01(vNorm * 1.24f) * Mathf.PI);
                float trailingLeg = Mathf.Sin(Mathf.Clamp01((vNorm - 0.15f) * 1.17f) * Mathf.PI);
                targetLHipRot = new Vector3(-89f * leadingLeg, 0f, -14f * vEnv);
                targetRHipRot = new Vector3(-72f * trailingLeg, 0f, 18f * vEnv);
                targetLKneeRot = new Vector3(105f * vEnv, 0f, 0f);
                targetRKneeRot = new Vector3(95f * vEnv, 0f, 0f);
            }
            // --- Full NPC Knockdown onto back & smooth recovery stand-up ---
            else if (_knockdownTimer >= 0f)
            {
                float kNorm = Mathf.Clamp01(_knockdownTimer / Mathf.Max(1.0f, _activeKnockdownDuration));
                float downFactor = kNorm < 0.16f
                    ? Mathf.SmoothStep(0f, 1f, kNorm / 0.16f)
                    : (kNorm < 0.72f ? 1f : Mathf.SmoothStep(1f, 0f, (kNorm - 0.72f) / 0.28f));

                targetHipsPos = Vector3.Lerp(targetHipsPos, new Vector3(0f, 0.20f, -0.18f), downFactor);
                targetHipsRot = Vector3.Lerp(targetHipsRot, new Vector3(-86f, 8f, 6f), downFactor);
                targetSpineRot = Vector3.Lerp(targetSpineRot, new Vector3(10f, 0f, 0f), downFactor);
                targetHeadRot = Vector3.Lerp(targetHeadRot, new Vector3(18f, -14f, 0f), downFactor);
                targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-25f, 0f, -62f), downFactor);
                targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-18f, 0f, 58f), downFactor);
                targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-28f, 0f, 0f), downFactor);
                targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-34f, 0f, 0f), downFactor);
                targetLHipRot = Vector3.Lerp(targetLHipRot, new Vector3(6f, 0f, -14f), downFactor);
                targetRHipRot = Vector3.Lerp(targetRHipRot, new Vector3(-12f, 0f, 16f), downFactor);
                targetLKneeRot = Vector3.Lerp(targetLKneeRot, new Vector3(18f, 0f, 0f), downFactor);
                targetRKneeRot = Vector3.Lerp(targetRKneeRot, new Vector3(32f, 0f, 0f), downFactor);
            }
            // --- Hit recoil stagger animation (when punched) ---
            else if (_hitReactTimer >= 0f)
            {
                float hNorm = Mathf.Clamp01(_hitReactTimer / HitReactDuration);
                float hEnv = Mathf.Sin(hNorm * Mathf.PI);
                targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -28f, hEnv);
                targetSpineRot.y = Mathf.Lerp(targetSpineRot.y, 22f, hEnv);
                targetHeadRot.x = Mathf.Lerp(targetHeadRot.x, -26f, hEnv);
                targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-78f, 0f, -40f), hEnv);
                targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-70f, 0f, 38f), hEnv);
                targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-52f, 0f, 0f), hEnv);
                targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-56f, 0f, 0f), hEnv);
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
            // --- Startled arms-up pose (pedestrian near fast vehicle) ---
            else if (_startledTimer >= 0f)
            {
                float sNorm = Mathf.Clamp01(_startledTimer / _startledDuration);
                float sEnv = Mathf.Sin(sNorm * Mathf.PI);
                sEnv = Mathf.Clamp01(sEnv * 2.0f);

                targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-135f, 0f, -28f), sEnv);
                targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-135f, 0f, 28f), sEnv);
                targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-22f, 0f, 0f), sEnv);
                targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-22f, 0f, 0f), sEnv);
                targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -8f, sEnv);
            }
            // --- One-shot right-arm wave overlay (~1.5 s) ---
            else if (_waveTimer >= 0f)
            {
                float norm = _waveTimer / WaveDuration;
                float env = Mathf.SmoothStep(0f, 1f, norm / 0.24f) *
                    (1f - Mathf.SmoothStep(0f, 1f, (norm - 0.73f) / 0.27f));
                float waveOsc = Mathf.Sin(_waveTimer * 15f) * 18f * env;

                Vector3 waveShoulder = new Vector3(-31f, 0f, 113f);
                Vector3 waveElbow = new Vector3(-18f, 0f, 22f + waveOsc);
                targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, waveShoulder, env);
                targetRElbowRot = Vector3.Lerp(targetRElbowRot, waveElbow, env);
                targetHeadRot.y = Mathf.Lerp(targetHeadRot.y, 8f, env * 0.5f);
            }

            CarryableProp carry = CarryableProp.CurrentlyHeld;
            bool carrying = carry != null && carry.UsesRig(this);
            Vector3 petHand = Vector3.zero;
            float petCrouch = 0f;
            float petWeight = 0f;
            StreetCatController cat = StreetCatController.ActivePet;
            bool petting = cat != null && cat.TryGetPetPose(this, out petCrouch, out petWeight, out petHand);
            if (petting)
            {
                targetHipsPos.y -= 0.42f * petCrouch;
                targetHipsPos.z -= 0.04f * petCrouch;
                targetSpineRot = Vector3.Lerp(targetSpineRot, new Vector3(43f, -7f, -5f), petCrouch);
                targetNeckRot.x = Mathf.Lerp(targetNeckRot.x, 14f, petCrouch);
                targetHeadRot.x = Mathf.Lerp(targetHeadRot.x, 12f, petCrouch);
            }
            if (carrying)
            {
                float crouch = carry.PickupCrouch;
                targetHipsPos.y -= 0.52f * crouch;
                targetHipsPos.z -= 0.075f * crouch;
                targetSpineRot.x = Mathf.Lerp(targetSpineRot.x * 0.55f + 5f, 53f, crouch);
                targetSpineRot.y *= 0.35f;
                targetHeadRot.x += 14f * crouch;
            }

            float swingTime = Time.time - _tennisContactAt;
            if (tennisStance)
            {
                targetHipsPos.y -= 0.064f;
                targetHipsRot.y *= 0.35f;
                targetSpineRot.x = _tennisServing && swingTime < 0.09f ? 1f : 12f;
                float windup = 1f - Mathf.Abs(swingTime + 0.12f) / 0.15f;
                float follow = 1f - Mathf.Abs(swingTime - 0.10f) / 0.17f;
                targetSpineRot.y += 32f * Mathf.Clamp01(windup) - 37f * Mathf.Clamp01(follow);
            }

            if (_hasShelfReach && _shelfReachWeight > 0.01f)
            {
                targetSpineRot.x += 9f * _shelfReachWeight;
                targetSpineRot.y -= 10f * _shelfReachWeight;
                targetHeadRot.x += 6f * _shelfReachWeight;
            }
            else if (_consumeMode == 1 && _consumeWeight > 0.01f)
            {
                // Realistic drinking tilt: gently tip head back on each sip
                float sip = Mathf.Clamp01(_consumeCyclePhase);
                targetHeadRot.x = Mathf.Lerp(targetHeadRot.x, -26f, sip * _consumeWeight);
                targetNeckRot.x = Mathf.Lerp(targetNeckRot.x, -9f, sip * _consumeWeight);
                targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -4f, sip * _consumeWeight);
            }
            else if (_consumeMode == 2 && _consumeWeight > 0.01f)
            {
                // Realistic eating nod: lean slightly toward bite
                float bite = Mathf.Clamp01(_consumeCyclePhase);
                targetHeadRot.x = Mathf.Lerp(targetHeadRot.x, 14f, bite * _consumeWeight);
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

            if (chestVisual != null)
            {
                float breathScale = 1f + breathWave * 0.018f * (1f - moveBlend);
                Vector3 targetScale = new Vector3(
                    _chestBaseScale.x * breathScale,
                    _chestBaseScale.y,
                    _chestBaseScale.z * breathScale);
                chestVisual.localScale = Vector3.Lerp(chestVisual.localScale, targetScale, 1f - Mathf.Exp(-6f * dt));
            }

            float damp = _prayerStage > 0 ? 6.5f : (_landSquatTimer >= 0f || carrying || petting ? 25f : (_grounded ? 14f : 10f));
            hips.localPosition = Vector3.Lerp(hips.localPosition, targetHipsPos, 1f - Mathf.Exp(-damp * dt));
            SmoothRotate(hips, targetHipsRot, damp, dt);
            SmoothRotate(spine, targetSpineRot, damp, dt);
            SmoothRotate(neck, targetNeckRot, damp, dt);
            SmoothRotate(head, targetHeadRot, _prayerStage > 0 ? 6.0f : 9f, dt);

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
                Vector3 leftReach = ShoulderReach(lShoulder, lElbow, leftGrip);
                Vector3 rightReach = ShoulderReach(rShoulder, rElbow, rightGrip);
                Vector3 reach = leftReach.sqrMagnitude > rightReach.sqrMagnitude ? leftReach : rightReach;
                hips.position += Vector3.ClampMagnitude(reach, 0.16f * transform.lossyScale.y) * carryWeight;
            }
            if (petting)
            {
                Vector3 reach = ShoulderReach(rShoulder, rElbow, petHand);
                hips.position += Vector3.ClampMagnitude(reach, 0.13f * transform.lossyScale.y) * petWeight;
            }

            if (_grounded && !_isSwimming && !_isLyingDown && _prayerStage == 0 && _vaultTimer < 0f &&
                _knockdownTimer < 0f && _hitReactTimer < 0f && _punchTimer < 0f &&
                lAnkle != null && rAnkle != null)
            {
                PlantFeet(strideLength, stance, runBlend, tennisStance, idleW);
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
                    AimHand(lShoulder, lElbow, transform.TransformPoint(_prayerLeftHandLocal), leftPole, 1f);
                    AimHand(rShoulder, rElbow, transform.TransformPoint(_prayerRightHandLocal), rightPole, 1f);
                }
            }

            if (carryWeight > 0f)
            {
                AimHand(lShoulder, lElbow, leftGrip, -transform.right, carryWeight);
                AimHand(rShoulder, rElbow, rightGrip, transform.right, carryWeight);
            }
            if (petting)
            {
                AimHand(rShoulder, rElbow, petHand, transform.right, petWeight);
                if (lKnee != null)
                    AimHand(lShoulder, lElbow, lKnee.position + transform.up * 0.11f - transform.right * 0.04f,
                        -transform.right, petWeight);
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
            if (_isSwimming)
            {
                float strokeBlend = Mathf.SmoothStep(0f, 1f, _swimSpeed / 1.70f);
                AimHand(lShoulder, lElbow, SwimHand(-1f, _swimPhase), -transform.right, strokeBlend);
                AimHand(rShoulder, rElbow, SwimHand(1f, _swimPhase + Mathf.PI), transform.right, strokeBlend);
            }
            else if (!carrying && !petting && !tennisStance && _doorHandle != null && _grounded)
            {
                float elapsed = Time.time - _doorReachStarted;
                float weight = Mathf.SmoothStep(0f, 1f, elapsed / 0.16f) *
                    (1f - Mathf.SmoothStep(0f, 1f, (elapsed - _doorReachDuration * 0.66f) / (_doorReachDuration * 0.34f)));
                if (weight > 0f)
                    AimHand(_doorReachRight ? rShoulder : lShoulder, _doorReachRight ? rElbow : lElbow,
                        _doorHandle.position, transform.right * (_doorReachRight ? 1f : -1f), weight);
                if (elapsed >= _doorReachDuration) _doorHandle = null;
            }

            if (tennisStance) ApplyTennisRacketPose(swingTime);

            // Shop shelf IK reach & drink/eat mouth-target IK overlay
            if (!carrying && !petting && !tennisStance && !_isSwimming && rShoulder != null && rElbow != null)
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
            if (!carrying && !petting && !tennisStance && !_isSwimming && _hasDualAction)
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
        }

        private Vector3 ShoulderReach(Transform shoulder, Transform elbow, Vector3 target)
        {
            if (shoulder == null || elbow == null) return Vector3.zero;
            Vector3 delta = target - shoulder.position;
            float reach = (elbow.localPosition.magnitude + HandLength - 0.014f) * transform.lossyScale.y;
            return delta.normalized * Mathf.Max(0f, delta.magnitude - reach);
        }

        private void PlantFeet(float strideLength, float stance, float runBlend, bool tennisStance, float idleWeight)
        {
            Vector3 direction = transform.InverseTransformDirection(_travelDirection);
            Vector3 left = GaitFoot(Mathf.Repeat(_phase / TwoPi, 1f), -1f, direction,
                strideLength, stance, runBlend, tennisStance, idleWeight, out Quaternion leftRotation);
            Vector3 right = GaitFoot(Mathf.Repeat(_phase / TwoPi + 0.5f, 1f), 1f, direction,
                strideLength, stance, runBlend, tennisStance, idleWeight, out Quaternion rightRotation);

            // Keep both targets within reach when a long step or a low curb extends the support leg.
            float pelvisDrop = Mathf.Max(0f, Mathf.Max(LegExtension(lHip, lKnee, lAnkle, left),
                LegExtension(rHip, rKnee, rAnkle, right)));
            hips.position -= transform.up * pelvisDrop;
            AimLimb(lHip, lKnee, left, transform.forward, lAnkle.localPosition.magnitude);
            AimLimb(rHip, rKnee, right, transform.forward, rAnkle.localPosition.magnitude);
            lAnkle.rotation = leftRotation;
            rAnkle.rotation = rightRotation;
        }

        private Vector3 GaitFoot(float phase, float side, Vector3 direction, float strideLength, float stance,
            float runBlend, bool tennisStance, float idleWeight, out Quaternion rotation)
        {
            float halfStep = strideLength * stance * 0.5f;
            float offset;
            float lift = 0f;
            float pitch;
            if (phase < stance)
            {
                float load = phase / stance;
                offset = Mathf.Lerp(halfStep, -halfStep, load);
                pitch = -9f * (1f - Mathf.SmoothStep(0f, 1f, load / 0.22f)) +
                    16f * Mathf.SmoothStep(0f, 1f, (load - 0.74f) / 0.26f);
            }
            else
            {
                float swing = (phase - stance) / (1f - stance);
                offset = Mathf.SmoothStep(-halfStep, halfStep, swing);
                lift = Mathf.Sin(swing * Mathf.PI) * (tennisStance ? 0.085f : Mathf.Lerp(0.11f, 0.20f, runBlend));
                pitch = Mathf.Lerp(16f, -9f, swing);
            }

            float width = tennisStance ? 0.22f : Mathf.Lerp(0.13f, 0.102f, _gaitWeight);
            Vector3 local = new Vector3(side * width, 0f, -side * idleWeight * 0.035f) +
                direction * (offset * _gaitWeight);
            local.x = side * Mathf.Max(0.052f, side * local.x);
            Vector3 ground = transform.TransformPoint(local);
            Vector3 normal = transform.up;
            if (_sampleGround && Physics.Raycast(ground + transform.up * 0.32f, -transform.up,
                    out RaycastHit hit, 0.53f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.normal.y >= 0.65f && hit.collider.transform.root != transform.root)
            {
                ground = hit.point;
                normal = hit.normal;
            }

            pitch *= _gaitWeight * (tennisStance ? 0.4f : 1f);
            float soleReach = pitch > 0f ? 0.19f : 0.075f;
            float soleLift = Mathf.Abs(Mathf.Sin(pitch * Mathf.Deg2Rad)) * soleReach;
            rotation = Quaternion.FromToRotation(transform.up, normal) * transform.rotation *
                Quaternion.Euler(pitch, side * Mathf.Lerp(6f, 2f, _gaitWeight), 0f);
            return ground + normal * ((AnkleHeight + soleLift + lift * _gaitWeight) * transform.lossyScale.y);
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
            float breath = Mathf.Sin(_idleTime * Mathf.PI * 2f * 0.25f) * 0.8f;

            if (isDrivingSeat)
            {
                float steerNorm = Mathf.Clamp(_steerAngleDeg / 36f, -1f, 1f);

                hips.localPosition = Vector3.Lerp(hips.localPosition, new Vector3(0f, 0.17f, 0f), 1f - Mathf.Exp(-12f * dt));
                SmoothRotate(hips, Vector3.zero, 12f, dt);
                float wheelLean = _steeringWheel != null ? 8f : -6f;
                SmoothRotate(spine, new Vector3(wheelLean + breath, steerNorm * 5f, -steerNorm * 3f), 10f, dt);
                SmoothRotate(neck, new Vector3(4f, 0f, 0f), 10f, dt);
                SmoothRotate(head, new Vector3(0f, steerNorm * 14f, 0f), 8f, dt);

                SmoothRotate(lHip, new Vector3(-90f, 0f, -6f), 12f, dt);
                SmoothRotate(rHip, new Vector3(-90f, 0f, 6f), 12f, dt);
                SmoothRotate(lKnee, new Vector3(80f, 0f, 0f), 12f, dt);
                SmoothRotate(rKnee, new Vector3(80f, 0f, 0f), 12f, dt);
                SmoothRotate(lAnkle, new Vector3(10f, 0f, 0f), 12f, dt);
                SmoothRotate(rAnkle, new Vector3(10f, 0f, 0f), 12f, dt);

                float leftReach = -62f - steerNorm * 14f;
                float rightReach = -62f + steerNorm * 14f;
                SmoothRotate(lShoulder, new Vector3(leftReach, 10f + steerNorm * 10f, -10f - steerNorm * 8f), 14f, dt);
                SmoothRotate(rShoulder, new Vector3(rightReach, -10f + steerNorm * 10f, 10f - steerNorm * 8f), 14f, dt);
                SmoothRotate(lElbow, new Vector3(-32f + steerNorm * 10f, 0f, 0f), 14f, dt);
                SmoothRotate(rElbow, new Vector3(-32f - steerNorm * 10f, 0f, 0f), 14f, dt);
                if (_steeringWheel != null)
                {
                    AimHand(lShoulder, lElbow, _steeringWheel.TransformPoint(new Vector3(-0.145f, 0f, 0.055f)), -transform.right, 1f);
                    AimHand(rShoulder, rElbow, _steeringWheel.TransformPoint(new Vector3(0.145f, 0f, 0.055f)), transform.right, 1f);
                }
            }
            else if (_seatFloor.HasValue)
            {
                AnimateSeatContact(_seatFloor.Value, _seatAmount, breath, dt);
            }
            else
            {
                float headNoise = (Mathf.PerlinNoise(_noiseSeed, _idleTime * 0.16f) - 0.5f) * 2f;
                float headLookYaw = headNoise * 11f;

                hips.localPosition = Vector3.Lerp(hips.localPosition, new Vector3(0f, 0.08f, 0.02f), 1f - Mathf.Exp(-12f * dt));
                SmoothRotate(hips, Vector3.zero, 12f, dt);
                SmoothRotate(spine, new Vector3(-7f + breath, 0f, 0f), 10f, dt);
                SmoothRotate(neck, new Vector3(5f, 0f, 0f), 10f, dt);
                SmoothRotate(head, new Vector3(0f, headLookYaw, 0f), 6f, dt);

                SmoothRotate(lHip, new Vector3(-88f, 0f, -5f), 12f, dt);
                SmoothRotate(rHip, new Vector3(-88f, 0f, 5f), 12f, dt);
                SmoothRotate(lKnee, new Vector3(88f, 0f, 0f), 12f, dt);
                SmoothRotate(rKnee, new Vector3(88f, 0f, 0f), 12f, dt);
                SmoothRotate(lAnkle, Vector3.zero, 12f, dt);
                SmoothRotate(rAnkle, Vector3.zero, 12f, dt);

                // Hands resting comfortably downward on lap/thighs (never floating out in front!)
                SmoothRotate(lShoulder, new Vector3(8f, 0f, -10f), 12f, dt);
                SmoothRotate(rShoulder, new Vector3(8f, 0f, 10f), 12f, dt);
                SmoothRotate(lElbow, new Vector3(-44f, 0f, 10f), 12f, dt);
                SmoothRotate(rElbow, new Vector3(-44f, 0f, -10f), 12f, dt);
            }
        }

        private void AnimateSeatContact(float floor, float seated, float breath, float dt)
        {
            float shift = Mathf.Sin(seated * Mathf.PI);
            hips.localPosition = new Vector3(0f, Mathf.Lerp(hipsBaseY, 0.08f, seated), 0.09f * shift);
            SmoothRotate(hips, Vector3.zero, 15f, dt);
            SmoothRotate(spine, new Vector3(24f * shift - 5f * seated + breath, 0f, 0f), 16f, dt);
            SmoothRotate(neck, new Vector3(-8f * shift + 5f * seated, 0f, 0f), 14f, dt);
            SmoothRotate(head, Vector3.zero, 12f, dt);
            Vector3 left = transform.TransformPoint(new Vector3(-0.14f, 0f, Mathf.Lerp(0.04f, 0.46f, seated)));
            Vector3 right = transform.TransformPoint(new Vector3(0.14f, 0f, Mathf.Lerp(0.09f, 0.44f, seated)));
            left.y = right.y = floor + AnkleHeight * transform.lossyScale.y;
            if (lAnkle != null && rAnkle != null)
            {
                float drop = Mathf.Max(0f, Mathf.Max(LegExtension(lHip, lKnee, lAnkle, left),
                    LegExtension(rHip, rKnee, rAnkle, right)));
                hips.position -= transform.up * drop;
                AimLimb(lHip, lKnee, left, transform.forward, lAnkle.localPosition.magnitude);
                AimLimb(rHip, rKnee, right, transform.forward, rAnkle.localPosition.magnitude);
                lAnkle.rotation = transform.rotation;
                rAnkle.rotation = transform.rotation;
            }
            if (lKnee != null && rKnee != null)
            {
                Vector3 lRest = Vector3.Lerp(transform.TransformPoint(new Vector3(-0.27f, 0.82f, 0.06f)),
                    lKnee.position + transform.up * 0.10f, seated);
                Vector3 rRest = Vector3.Lerp(transform.TransformPoint(new Vector3(0.27f, 0.82f, 0.06f)),
                    rKnee.position + transform.up * 0.10f, seated);
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
        }

        public void SetCycling(bool active, Vector3 leftPedal, Vector3 rightPedal, Vector3 leftGrip, Vector3 rightGrip)
        {
            _cycling = active;
            _motorcyclePose = false;
            _leftPedal = leftPedal;
            _rightPedal = rightPedal;
            _leftGrip = leftGrip;
            _rightGrip = rightGrip;
        }

        public void SetMotorcyclePose(Vector3 leftPeg, Vector3 rightPeg, Vector3 leftGrip, Vector3 rightGrip, float mount, float footPeg)
        {
            SetCycling(true, leftPeg, rightPeg, leftGrip, rightGrip);
            _motorcyclePose = true;
            _motorcycleMount = mount;
            _motorcycleFootPeg = footPeg;
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
        }

        public void ClearTennisSwing()
        {
            _tennisContactAt = float.NegativeInfinity;
            _tennisServing = false;
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
            Vector3 readyHand = transform.TransformPoint(new Vector3(0.24f, 1.06f, 0.28f));
            Vector3 readyAxis = new Vector3(0.42f, 0.86f, 0.28f);
            Vector3 axis = readyAxis;
            Vector3 hand = readyHand;
            if (swingTime >= -TennisWindup && swingTime < TennisFollowThrough)
            {
                Vector3 strikeAxis = _tennisServing
                    ? new Vector3(0.45f, 0.86f, 0.22f)
                    : new Vector3(0.89f, 0.25f, 0.38f);
                Vector3 strikeUp = transform.TransformDirection(strikeAxis.normalized);
                Vector3 strikeFace = Vector3.ProjectOnPlane(transform.forward, strikeUp).normalized;
                Vector3 strikeHand = _tennisContact - strikeFace * TennisCourt.BallRadius -
                    strikeUp * (RacketHeadFromHand * transform.lossyScale.y);
                if (swingTime < 0f)
                {
                    const float backswingTime = -0.11f;
                    Vector3 backswingHand = transform.TransformPoint(_tennisServing
                        ? new Vector3(0.27f, 1.42f, -0.14f)
                        : new Vector3(0.43f, 1.02f, -0.23f));
                    Vector3 backswingAxis = new Vector3(0.64f, 0.45f, -0.62f);
                    if (swingTime < backswingTime)
                    {
                        float windup = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-TennisWindup, backswingTime, swingTime));
                        hand = Vector3.Lerp(readyHand, backswingHand, windup);
                        axis = Vector3.Slerp(readyAxis, backswingAxis, windup);
                    }
                    else
                    {
                        float strike = Mathf.InverseLerp(backswingTime, 0f, swingTime);
                        hand = Vector3.Lerp(backswingHand, strikeHand, strike * strike);
                        axis = Vector3.Slerp(backswingAxis, strikeAxis, strike * strike);
                    }
                }
                else
                {
                    Vector3 finishHand = transform.TransformPoint(new Vector3(-0.10f, 1.28f, 0.32f));
                    Vector3 finishAxis = new Vector3(-0.75f, 0.62f, 0.22f);
                    const float finishTime = 0.12f;
                    if (swingTime < finishTime)
                    {
                        float follow = Mathf.SmoothStep(0f, 1f, swingTime / finishTime);
                        hand = Vector3.Lerp(strikeHand, finishHand, follow);
                        axis = Vector3.Slerp(strikeAxis, finishAxis, follow);
                    }
                    else
                    {
                        float recover = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(finishTime, TennisFollowThrough, swingTime));
                        hand = Vector3.Lerp(finishHand, readyHand, recover);
                        axis = Vector3.Slerp(finishAxis, readyAxis, recover);
                    }
                }
            }

            AimHand(rShoulder, rElbow, hand, transform.right, 1f);
            AimHand(lShoulder, lElbow,
                _tennisLeftHand ?? transform.TransformPoint(new Vector3(-0.27f, 1.03f, 0.29f)),
                -transform.right, 1f);
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

        private void AnimateCycling()
        {
            float mount = _motorcyclePose ? _motorcycleMount : 1f;
            Vector3 seatedHips = _motorcyclePose
                ? new Vector3(Mathf.Lerp(-0.11f, 0f, _motorcycleFootPeg), Mathf.Lerp(0.91f, 0.97f, _motorcycleFootPeg), -0.25f)
                : new Vector3(0f, hipsBaseY + 0.12f, -0.235f);
            hips.localPosition = Vector3.Lerp(new Vector3(0f, hipsBaseY, 0f), seatedHips, mount);
            hips.localRotation = Quaternion.identity;
            float lean = (_motorcyclePose ? 19f : 33f) * mount;
            if (spine != null) spine.localRotation = Quaternion.Euler(lean, 0f, 0f);
            if (neck != null) neck.localRotation = Quaternion.Euler(-lean * 0.7f, 0f, 0f);
            if (head != null) head.localRotation = Quaternion.Euler(-7f * mount, 0f, 0f);

            Vector3 ankleOffset = transform.up * 0.065f - transform.forward * 0.04f;
            Vector3 leftFoot = Vector3.Lerp(transform.TransformPoint(new Vector3(-0.13f, AnkleHeight, 0f)),
                _leftPedal + ankleOffset, mount);
            Vector3 rightFoot = Vector3.Lerp(transform.TransformPoint(new Vector3(0.13f,
                AnkleHeight + Mathf.Sin(mount * Mathf.PI) * 0.30f, 0f)), _rightPedal + ankleOffset, mount);
            AimLimb(lHip, lKnee, leftFoot, transform.forward, 0.42f);
            AimLimb(rHip, rKnee, rightFoot, transform.forward, 0.42f);
            if (lAnkle != null) lAnkle.rotation = transform.rotation;
            if (rAnkle != null) rAnkle.rotation = transform.rotation;
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
