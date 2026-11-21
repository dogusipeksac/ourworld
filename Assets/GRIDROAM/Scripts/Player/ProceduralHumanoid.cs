using System;
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

        private float _speedMs;
        private bool _grounded = true;
        private bool _wasGrounded = true;
        private float _airborneTimer;
        private float _landSquatTimer = -1f;
        private float _verticalVelocity;
        private float _steerAngleDeg;

        private float _phase;
        private int _lastStepCount;
        private float _idleTime;
        private float _noiseSeed;
        private float _prevYaw;
        private float _smoothedTurnLean;
        private float _waveTimer = -1f;
        private float _punchTimer = -1f;
        private float _hitReactTimer = -1f;
        private float _startledTimer = -1f;
        private float _startledDuration = 1.1f;
        private float _autoWaveCountdown = 8f;
        private const float PunchDuration = 0.36f;
        private const float HitReactDuration = 0.55f;
        private Vector3 _chestBaseScale = new Vector3(0.39f, 0.20f, 0.23f);
        private bool _initialized;

        // Head-look IK-lite state
        private bool _hasLookPoint;
        private Vector3 _lookWorldPoint;
        private float _smoothedLookYaw;
        private float _smoothedLookPitch;

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

        public void SetLocomotion(float speedMs, bool grounded, float verticalVelocity)
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
            _punchTimer = 0f;
            _waveTimer = -1f;
        }

        public void PlayHitReact()
        {
            _hitReactTimer = 0f;
            _waveTimer = -1f;
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
                hips.localPosition = new Vector3(0f, 0.08f, -0.02f);
                hips.localRotation = Quaternion.identity;
                if (spine != null) spine.localRotation = Quaternion.Euler(-4f, 0f, 0f);
                if (neck != null) neck.localRotation = Quaternion.Euler(3f, 0f, 0f);
                if (head != null) head.localRotation = Quaternion.identity;

                if (lHip != null) lHip.localRotation = Quaternion.Euler(-90f, 0f, -5f);
                if (rHip != null) rHip.localRotation = Quaternion.Euler(-90f, 0f, 5f);
                if (lKnee != null) lKnee.localRotation = Quaternion.Euler(90f, 0f, 0f);
                if (rKnee != null) rKnee.localRotation = Quaternion.Euler(90f, 0f, 0f);
                if (lAnkle != null) lAnkle.localRotation = Quaternion.identity;
                if (rAnkle != null) rAnkle.localRotation = Quaternion.identity;

                if (lShoulder != null) lShoulder.localRotation = Quaternion.Euler(-22f, 8f, 10f);
                if (rShoulder != null) rShoulder.localRotation = Quaternion.Euler(-22f, -8f, -10f);
                if (lElbow != null) lElbow.localRotation = Quaternion.Euler(-58f, 0f, -12f);
                if (rElbow != null) rElbow.localRotation = Quaternion.Euler(-58f, 0f, 12f);
            }
        }

        private void LateUpdate()
        {
            if (hips == null) return;
            if (!_initialized) InitializeRuntime();

            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

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
                if (_punchTimer >= PunchDuration)
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

            // Advance gait phase by distance travelled & fire footstep contact events
            if (_speedMs > 0.05f && _grounded)
            {
                float strideLength = Mathf.Lerp(1.4f, 2.2f, runBlend);
                _phase += (_speedMs * dt / strideLength) * (Mathf.PI * 2f);

                if (_speedMs > 0.25f)
                {
                    int currentStepCount = Mathf.FloorToInt((_phase + Mathf.PI * 0.5f) / Mathf.PI);
                    if (currentStepCount != _lastStepCount)
                    {
                        _lastStepCount = currentStepCount;
                        OnFootstepContact?.Invoke(runBlend);
                    }
                }

                if (_phase > Mathf.PI * 200f)
                {
                    _phase -= Mathf.PI * 200f;
                    _lastStepCount = Mathf.FloorToInt((_phase + Mathf.PI * 0.5f) / Mathf.PI);
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

            // --- Walk / Run components ---
            float sinL = Mathf.Sin(_phase);
            float sinR = Mathf.Sin(_phase + Mathf.PI);

            float thighAmp = Mathf.Lerp(25f, 45f, runBlend);
            float maxKneeBend = Mathf.Lerp(60f, 95f, runBlend);
            float armAmp = Mathf.Lerp(18f, 50f, runBlend);
            float elbowBaseBend = Mathf.Lerp(15f, 80f, runBlend);
            float bobAmp = Mathf.Lerp(0.02f, 0.05f, runBlend);
            float torsoForwardLean = Mathf.Lerp(3f, 12f, runBlend);

            float walkThighL = -sinL * thighAmp;
            float walkThighR = -sinR * thighAmp;

            float swingL = Mathf.Max(0f, Mathf.Sin(_phase + 1.15f));
            float swingR = Mathf.Max(0f, Mathf.Sin(_phase + Mathf.PI + 1.15f));
            float walkKneeL = swingL * maxKneeBend;
            float walkKneeR = swingR * maxKneeBend;

            float walkAnkleL = Mathf.Clamp(-(walkThighL + walkKneeL) * 0.65f, -25f, 25f);
            float walkAnkleR = Mathf.Clamp(-(walkThighR + walkKneeR) * 0.65f, -25f, 25f);

            float walkShoulderL = sinL * armAmp;
            float walkShoulderR = sinR * armAmp;
            float walkElbowL = -(elbowBaseBend + Mathf.Max(0f, -sinL) * 14f);
            float walkElbowR = -(elbowBaseBend + Mathf.Max(0f, -sinR) * 14f);

            float walkBobY = Mathf.Abs(Mathf.Sin(_phase)) * bobAmp - bobAmp * 0.45f;
            float walkHipYaw = sinL * 6f;

            // Blend Idle <-> Locomotion
            Vector3 targetHipsPos = new Vector3(
                Mathf.Lerp(idleWeightShiftX, 0f, moveBlend),
                hipsBaseY + walkBobY * moveBlend,
                0f);

            float defaultHeadYaw = Mathf.Lerp(idleHeadYaw, 0f, moveBlend);
            float finalHeadYaw = useLookIk || Mathf.Abs(_smoothedLookYaw) > 0.5f
                ? (_smoothedLookYaw * 0.75f)
                : defaultHeadYaw;

            Vector3 targetHipsRot = new Vector3(0f, walkHipYaw * moveBlend, 0f);
            Vector3 targetSpineRot = new Vector3(
                Mathf.Lerp(idleChestPitch, torsoForwardLean, moveBlend),
                -walkHipYaw * 0.55f * moveBlend,
                _smoothedTurnLean);
            Vector3 targetNeckRot = new Vector3(
                -targetSpineRot.x * 0.35f + _smoothedLookPitch * 0.25f,
                _smoothedLookYaw * 0.25f,
                -_smoothedTurnLean * 0.35f);
            Vector3 targetHeadRot = new Vector3(_smoothedLookPitch * 0.75f, finalHeadYaw, 0f);

            Vector3 targetLHipRot = new Vector3(walkThighL * moveBlend, 0f, 0f);
            Vector3 targetRHipRot = new Vector3(walkThighR * moveBlend, 0f, 0f);
            Vector3 targetLKneeRot = new Vector3(walkKneeL * moveBlend, 0f, 0f);
            Vector3 targetRKneeRot = new Vector3(walkKneeR * moveBlend, 0f, 0f);
            Vector3 targetLAnkleRot = new Vector3(walkAnkleL * moveBlend, 0f, 0f);
            Vector3 targetRAnkleRot = new Vector3(walkAnkleR * moveBlend, 0f, 0f);

            Vector3 targetLShoulderRot = new Vector3(walkShoulderL * moveBlend, 0f, -3f);
            Vector3 targetRShoulderRot = new Vector3(walkShoulderR * moveBlend, 0f, 3f);
            Vector3 targetLElbowRot = new Vector3(Mathf.Lerp(-8f, walkElbowL, moveBlend), 0f, 0f);
            Vector3 targetRElbowRot = new Vector3(Mathf.Lerp(-8f, walkElbowR, moveBlend), 0f, 0f);

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

            // --- Hit recoil stagger animation (when punched) ---
            if (_hitReactTimer >= 0f)
            {
                float hNorm = Mathf.Clamp01(_hitReactTimer / HitReactDuration);
                float hEnv = Mathf.Sin(hNorm * Mathf.PI);
                targetSpineRot.x = Mathf.Lerp(targetSpineRot.x, -26f, hEnv);
                targetSpineRot.y = Mathf.Lerp(targetSpineRot.y, 18f, hEnv);
                targetHeadRot.x = Mathf.Lerp(targetHeadRot.x, -24f, hEnv);
                targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-75f, 0f, -38f), hEnv);
                targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-68f, 0f, 36f), hEnv);
                targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-48f, 0f, 0f), hEnv);
                targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-52f, 0f, 0f), hEnv);
            }
            // --- Right-hand melee punch / shove overlay (0.36 s) ---
            else if (_punchTimer >= 0f)
            {
                float pNorm = Mathf.Clamp01(_punchTimer / PunchDuration);
                // Crisp windup (0..0.28), snap punch (0.28..0.60), recover (0.60..1.0)
                float thrust = pNorm < 0.28f
                    ? -(pNorm / 0.28f) * 0.4f
                    : (pNorm < 0.62f
                        ? Mathf.Lerp(-0.4f, 1.0f, (pNorm - 0.28f) / 0.34f)
                        : Mathf.Lerp(1.0f, 0f, (pNorm - 0.62f) / 0.38f));
                float guardEnv = Mathf.Sin(pNorm * Mathf.PI);

                targetSpineRot.y += -28f * thrust;
                targetSpineRot.x += 12f * Mathf.Max(0f, thrust);
                targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, new Vector3(-92f, -14f, 8f), Mathf.Clamp01(thrust));
                targetRElbowRot = Vector3.Lerp(targetRElbowRot, new Vector3(-12f, 0f, 0f), Mathf.Clamp01(thrust));
                targetLShoulderRot = Vector3.Lerp(targetLShoulderRot, new Vector3(-45f, 18f, -16f), guardEnv);
                targetLElbowRot = Vector3.Lerp(targetLElbowRot, new Vector3(-88f, 0f, 0f), guardEnv);
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
                float env = Mathf.Sin(Mathf.Clamp01(norm) * Mathf.PI);
                env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(env * 2.2f));
                float waveOsc = Mathf.Sin(_waveTimer * 15f) * 24f;

                Vector3 waveShoulder = new Vector3(-22f, 0f, 132f);
                Vector3 waveElbow = new Vector3(-18f, 0f, 22f + waveOsc);
                targetRShoulderRot = Vector3.Lerp(targetRShoulderRot, waveShoulder, env);
                targetRElbowRot = Vector3.Lerp(targetRElbowRot, waveElbow, env);
                targetHeadRot.y = Mathf.Lerp(targetHeadRot.y, 8f, env * 0.5f);
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

            float damp = _landSquatTimer >= 0f ? 28f : (_grounded ? 14f : 10f);
            hips.localPosition = Vector3.Lerp(hips.localPosition, targetHipsPos, 1f - Mathf.Exp(-damp * dt));
            SmoothRotate(hips, targetHipsRot, damp, dt);
            SmoothRotate(spine, targetSpineRot, damp, dt);
            SmoothRotate(neck, targetNeckRot, damp, dt);
            SmoothRotate(head, targetHeadRot, 9f, dt);

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
        }

        private void AnimateSitting(float dt)
        {
            float breath = Mathf.Sin(_idleTime * Mathf.PI * 2f * 0.25f) * 0.8f;

            if (isDrivingSeat)
            {
                float steerNorm = Mathf.Clamp(_steerAngleDeg / 36f, -1f, 1f);

                hips.localPosition = Vector3.Lerp(hips.localPosition, new Vector3(0f, 0.17f, 0f), 1f - Mathf.Exp(-12f * dt));
                SmoothRotate(hips, Vector3.zero, 12f, dt);
                SmoothRotate(spine, new Vector3(-6f + breath, steerNorm * 5f, -steerNorm * 3f), 10f, dt);
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
            }
            else
            {
                float headNoise = (Mathf.PerlinNoise(_noiseSeed, _idleTime * 0.16f) - 0.5f) * 2f;
                float headLookYaw = headNoise * 11f;

                hips.localPosition = Vector3.Lerp(hips.localPosition, new Vector3(0f, 0.08f, -0.02f), 1f - Mathf.Exp(-12f * dt));
                SmoothRotate(hips, Vector3.zero, 12f, dt);
                SmoothRotate(spine, new Vector3(-4f + breath, 0f, 0f), 10f, dt);
                SmoothRotate(neck, new Vector3(3f, 0f, 0f), 10f, dt);
                SmoothRotate(head, new Vector3(0f, headLookYaw, 0f), 6f, dt);

                SmoothRotate(lHip, new Vector3(-90f, 0f, -5f), 12f, dt);
                SmoothRotate(rHip, new Vector3(-90f, 0f, 5f), 12f, dt);
                SmoothRotate(lKnee, new Vector3(90f, 0f, 0f), 12f, dt);
                SmoothRotate(rKnee, new Vector3(90f, 0f, 0f), 12f, dt);
                SmoothRotate(lAnkle, Vector3.zero, 12f, dt);
                SmoothRotate(rAnkle, Vector3.zero, 12f, dt);

                SmoothRotate(lShoulder, new Vector3(-22f, 8f, 10f), 12f, dt);
                SmoothRotate(rShoulder, new Vector3(-22f, -8f, -10f), 12f, dt);
                SmoothRotate(lElbow, new Vector3(-58f, 0f, -12f), 12f, dt);
                SmoothRotate(rElbow, new Vector3(-58f, 0f, 12f), 12f, dt);
            }
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
