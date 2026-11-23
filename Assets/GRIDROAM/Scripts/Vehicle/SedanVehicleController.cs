using System;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OurWorld.Vehicle
{
    [Serializable]
    public class AxleInfo
    {
        public WheelCollider leftWheel;
        public WheelCollider rightWheel;
        public Transform leftWheelVisual;
        public Transform rightWheelVisual;
        public bool motor;
        public bool steering;
    }

    /// <summary>
    /// Realistic WheelCollider vehicle controller with torque curve, 5-speed automatic gearbox,
    /// aerodynamic drag, Ackermann speed-sensitive steering, 60/40 ABS braking, handbrake drift,
    /// fuel tank consumption (~15 min full tank), night-aware headlights, collision impulse reporting,
    /// and seated driver + steering wheel animation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SedanVehicleController : MonoBehaviour
    {
        [Header("Axles (Unity 6 WheelCollider Tutorial Pattern)")]
        [SerializeField] private List<AxleInfo> axleInfos = new List<AxleInfo>();

        [Header("Drivetrain")]
        [SerializeField] private float idleRpm = 850f;
        [SerializeField] private float redlineRpm = 6500f;
        [SerializeField] private float upshiftRpm = 5800f;
        [SerializeField] private float downshiftRpm = 2200f;
        [SerializeField] private float shiftDuration = 0.25f;
        [SerializeField] private float finalDriveRatio = 3.9f;
        [SerializeField] private float drivetrainEfficiency = 0.85f;
        [SerializeField] private float[] gearRatios = { 3.6f, 2.2f, 1.5f, 1.15f, 0.92f };
        [SerializeField] private float reverseGearRatio = 3.4f;
        [SerializeField] private float maxReverseSpeedKmh = 25f;

        [Header("Fuel")]
        [SerializeField] private float fuel = 0.8f;

        [Header("Braking & Aerodynamics")]
        [SerializeField] private float maxBrakeTorque = 3200f;
        [SerializeField] private float parkedBrakeTorque = 3800f;
        [SerializeField] private float frontBrakeBias = 0.6f;
        [SerializeField] private float absSlipThreshold = 0.35f;
        [SerializeField] private float airDensity = 1.2f;
        [SerializeField] private float dragCoefficient = 0.30f;
        [SerializeField] private float frontalArea = 2.2f;
        [SerializeField] private float rollingResistanceFactor = 12f;

        [Header("Ackermann Steering")]
        [SerializeField] private float wheelbase = 2.70f;
        [SerializeField] private float trackWidth = 1.68f;
        [SerializeField] private float maxSteeringAngleLowSpeed = 36f;
        [SerializeField] private float maxSteeringAngleHighSpeed = 9f;
        [SerializeField] private float steerRateLowSpeed = 120f;
        [SerializeField] private float steerRateHighSpeed = 60f;

        [Header("Tyre Drift & Stability")]
        [SerializeField] private float baseRearSidewaysStiffness = 1.8f;
        [SerializeField] private float handbrakeRearSidewaysStiffness = 0.9f;
        [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.42f, 0.08f);
        [SerializeField] private float antiRollForce = 4200f;
        [SerializeField] private float downforceFactor = 16f;

        [Header("Entry / Exit Points")]
        [SerializeField] private Transform leftExitPoint;
        [SerializeField] private Transform rightExitPoint;

        [Header("Visuals & Seated Driver")]
        [SerializeField] private Transform bodyVisualRoot;
        [SerializeField] private GameObject seatedDriverObject;
        [SerializeField] private Transform steeringWheelTransform;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Rigidbody _rb;
        private InteractableTrigger _selfTrigger;
        private VehicleDamageSystem _damageSystem;
        private readonly Collider[] _pedOverlapBuffer = new Collider[8];
        private float _rammedCoastTimer;
        private bool _isPlayerDriving;
        private bool _inputLocked;
        private bool _warnedEmptyFuel;
        private float _currentSteerAngle;
        private float _shiftTimer;
        private float _shiftCooldown;
        private float _currentRearSidewaysStiffness = 1.8f;

        private Vector3 _prevVelocity;
        private float _bodyPitch;
        private float _bodyRoll;

        private readonly List<Renderer> _headlightRenderers = new List<Renderer>();
        private readonly List<Renderer> _taillightRenderers = new List<Renderer>();
        private readonly List<Color> _taillightBaseEmissions = new List<Color>();
        private readonly List<Renderer> _reverseRenderers = new List<Renderer>();
        private MaterialPropertyBlock _mpb;
        private bool _lastBrakingVisual;
        private bool _lastReversingVisual;
        private bool _lastHeadlightOnVisual;
        private bool _headlightsInitialized;
        private bool _lightsCached;
        private bool _headlightsManualOverride;
        private bool _headlightsManualState;
        private float _nitroCharge = 1.0f;
        private float _nitroFxTimer;
        private float _driftStreakSeconds;
        private float _driftGraceTimer;
        private bool _wasNitroEmptyWarned;

        private ProceduralHumanoid _seatedDriverHumanoid;
        private Light _headlightBeam;
        private Quaternion _steeringWheelBaseRot = Quaternion.identity;
        private bool _steeringWheelCached;

        public bool IsPlayerDriving => _isPlayerDriving;
        public float SpeedKmh => _rb != null ? _rb.linearVelocity.magnitude * 3.6f : 0f;
        public float ForwardSpeedMs => _rb != null ? Vector3.Dot(_rb.linearVelocity, transform.forward) : 0f;
        public Transform BodyVisualRoot => bodyVisualRoot;
        public List<AxleInfo> AxleInfos => axleInfos;
        public float Fuel => fuel;

        public string VehicleSaveId
        {
            get
            {
                if (_selfTrigger == null) _selfTrigger = GetComponent<InteractableTrigger>();
                if (_selfTrigger != null && !string.IsNullOrEmpty(_selfTrigger.InteractableId))
                {
                    return _selfTrigger.InteractableId;
                }
                return gameObject.name;
            }
        }

        public int CurrentGear { get; private set; }
        public float EngineRpm { get; private set; }
        public bool IsBraking { get; private set; }
        public bool IsReversing { get; private set; }
        public float CurrentThrottle { get; private set; }
        public float MaxRearSidewaysSlip { get; private set; }

        public event Action<SedanVehicleController, float> OnCollisionImpulse;

        public void ConfigureSetup(
            List<AxleInfo> axles,
            Transform bodyRoot,
            Transform leftExit,
            Transform rightExit,
            GameObject seatedDriver = null,
            Transform steeringWheel = null)
        {
            axleInfos = axles;
            bodyVisualRoot = bodyRoot;
            leftExitPoint = leftExit;
            rightExitPoint = rightExit;
            if (seatedDriver != null) seatedDriverObject = seatedDriver;
            if (steeringWheel != null) steeringWheelTransform = steeringWheel;
        }

        public void ConfigureTuning(
            float wheelbaseMeters,
            float trackWidthMeters,
            float antiRoll,
            Vector3 comOffset,
            float maxBrake = 3200f,
            float finalDrive = 3.9f)
        {
            wheelbase = wheelbaseMeters;
            trackWidth = trackWidthMeters;
            antiRollForce = antiRoll;
            centerOfMassOffset = comOffset;
            maxBrakeTorque = maxBrake;
            finalDriveRatio = finalDrive;
        }

        public void SetFuel(float normalizedFuel)
        {
            fuel = Mathf.Clamp01(normalizedFuel);
            if (fuel > 0.02f)
            {
                _warnedEmptyFuel = false;
            }

            if (PlayerModeController.Instance != null && PlayerModeController.Instance.PlayerSedan == this &&
                SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                SaveManager.Instance.Data.missionSedanFuel = fuel;
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _selfTrigger = GetComponent<InteractableTrigger>();
            _damageSystem = GetComponent<VehicleDamageSystem>();
            if (_damageSystem == null)
            {
                _damageSystem = gameObject.AddComponent<VehicleDamageSystem>();
            }
            _rb.centerOfMass = centerOfMassOffset;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            EngineRpm = idleRpm;
            CurrentGear = 0;
            fuel = Mathf.Clamp01(fuel);
            _currentRearSidewaysStiffness = baseRearSidewaysStiffness;
            _mpb = new MaterialPropertyBlock();

            CacheVisualReferences();
            UpdateSeatedDriverVisibility();
        }

        public void NotifyRammed(float coastSeconds = 1.4f)
        {
            _rammedCoastTimer = Mathf.Max(_rammedCoastTimer, coastSeconds);
            if (_rb != null && _rb.IsSleeping())
            {
                _rb.WakeUp();
            }
        }

        private void Start()
        {
            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.OnNightStateChanged += HandleNightStateChanged;
            }
            UpdateHeadlightState();
        }

        private void OnDestroy()
        {
            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.OnNightStateChanged -= HandleNightStateChanged;
            }
        }

        private void HandleNightStateChanged(bool _)
        {
            UpdateHeadlightState();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || collision.contactCount == 0) return;

            ContactPoint contact = collision.GetContact(0);
            float relSpeed = collision.relativeVelocity.magnitude;
            float impulse = collision.impulse.magnitude;

            if (impulse > 150f)
            {
                OnCollisionImpulse?.Invoke(this, impulse);
            }

            // Pedestrian direct collision
            TownPedestrian ped = collision.collider != null
                ? collision.collider.GetComponentInParent<TownPedestrian>()
                : null;
            if (ped != null && SpeedKmh >= 7f)
            {
                ped.OnHitByVehicle(transform.position, _rb != null ? _rb.linearVelocity : transform.forward * 6f, SpeedKmh);
                if (_damageSystem != null)
                {
                    _damageSystem.ApplyPedestrianHitFeedback(contact.point, -transform.forward, SpeedKmh, _isPlayerDriving);
                }
                return;
            }

            // Ignore gentle ground scrapes
            if (contact.normal.y > 0.72f && relSpeed < 6.5f)
            {
                return;
            }

            // If ramming another parked/moving vehicle, release its parked brake so it physically shoves & spins!
            if (collision.collider != null)
            {
                SedanVehicleController otherCar = collision.collider.GetComponentInParent<SedanVehicleController>();
                if (otherCar != null && otherCar != this && relSpeed >= 3.2f)
                {
                    otherCar.NotifyRammed(1.6f);
                }
            }

            if (relSpeed >= 3.2f && _damageSystem != null)
            {
                // Push vertices inward from impact face into the car chassis
                Vector3 pushInward = contact.normal;
                if (Vector3.Dot(pushInward, transform.position - contact.point) < 0f)
                {
                    pushInward = -pushInward;
                }
                _damageSystem.ApplyCrashImpact(contact.point, pushInward, relSpeed, _isPlayerDriving);
            }
        }

        private void CacheVisualReferences()
        {
            if (bodyVisualRoot == null) return;

            if (!_lightsCached)
            {
                _lightsCached = true;
                _headlightRenderers.Clear();
                _taillightRenderers.Clear();
                _taillightBaseEmissions.Clear();
                _reverseRenderers.Clear();

                Renderer[] renderers = bodyVisualRoot.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    string objName = r.gameObject.name;
                    if (objName == "Placeholder_HeadlightL" || objName == "Placeholder_HeadlightR" || objName == "Car_Headlights")
                    {
                        _headlightRenderers.Add(r);
                    }
                    else if (objName == "Placeholder_TaillightL" || objName == "Placeholder_TaillightR" || objName == "Car_Taillights")
                    {
                        _taillightRenderers.Add(r);
                        Color baseEm = new Color(1.0f, 0.12f, 0.08f) * 2.6f;
                        if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(EmissionColorId))
                        {
                            baseEm = r.sharedMaterial.GetColor(EmissionColorId);
                        }
                        _taillightBaseEmissions.Add(baseEm);
                    }
                    else if (objName == "Placeholder_ReverseL" || objName == "Placeholder_ReverseR" || objName == "Car_ReverseLights")
                    {
                        _reverseRenderers.Add(r);
                    }
                }
            }

            if (seatedDriverObject == null)
            {
                Transform found = bodyVisualRoot.Find("SeatedDriverRoot");
                if (found != null) seatedDriverObject = found.gameObject;
            }

            if (seatedDriverObject != null && _seatedDriverHumanoid == null)
            {
                _seatedDriverHumanoid = seatedDriverObject.GetComponentInChildren<ProceduralHumanoid>(true);
                if (_seatedDriverHumanoid != null)
                {
                    _seatedDriverHumanoid.SetSitting(true, driving: true);
                }
            }

            if (_headlightBeam == null)
            {
                Transform beamTr = bodyVisualRoot.Find("Headlight_Beam");
                if (beamTr != null)
                {
                    _headlightBeam = beamTr.GetComponent<Light>();
                }
                else
                {
                    GameObject beamObj = new GameObject("Headlight_Beam");
                    beamObj.transform.SetParent(bodyVisualRoot, false);
                    beamObj.transform.localPosition = new Vector3(0f, 0.80f, 2.25f);
                    beamObj.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
                    _headlightBeam = beamObj.AddComponent<Light>();
                    _headlightBeam.type = LightType.Spot;
                    _headlightBeam.shadows = LightShadows.None;
                }
            }

            if (_headlightBeam != null)
            {
                // Calibrate car headlight beam for crisp URP Linear night street visibility
                _headlightBeam.type = LightType.Spot;
                _headlightBeam.color = new Color(1.0f, 0.95f, 0.82f);
                _headlightBeam.range = Mathf.Max(_headlightBeam.range, 54f);
                _headlightBeam.spotAngle = Mathf.Max(_headlightBeam.spotAngle, 74f);
                _headlightBeam.innerSpotAngle = Mathf.Max(_headlightBeam.innerSpotAngle, 40f);
                _headlightBeam.intensity = Mathf.Max(_headlightBeam.intensity, 48f);
                _headlightBeam.shadows = LightShadows.None;
            }

            if (steeringWheelTransform == null)
            {
                steeringWheelTransform = bodyVisualRoot.Find("SteeringWheel");
            }

            if (steeringWheelTransform != null && !_steeringWheelCached)
            {
                _steeringWheelBaseRot = steeringWheelTransform.localRotation;
                _steeringWheelCached = true;
            }
        }

        private void UpdateSeatedDriverVisibility()
        {
            if (seatedDriverObject != null)
            {
                seatedDriverObject.SetActive(_isPlayerDriving);
            }
            UpdateHeadlightState();
        }

        private void UpdateHeadlightState()
        {
            bool nightNow = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
            bool headlightsOn = _isPlayerDriving && (_headlightsManualOverride ? _headlightsManualState : nightNow);

            if (_headlightBeam != null)
            {
                _headlightBeam.enabled = headlightsOn;
            }

            if (!_headlightsInitialized || headlightsOn != _lastHeadlightOnVisual)
            {
                _headlightsInitialized = true;
                _lastHeadlightOnVisual = headlightsOn;
                if (_mpb == null) _mpb = new MaterialPropertyBlock();

                Color headEmission = headlightsOn ? new Color(1.0f, 0.92f, 0.70f) * 4.5f : Color.black;
                for (int i = 0; i < _headlightRenderers.Count; i++)
                {
                    Renderer r = _headlightRenderers[i];
                    if (r == null) continue;
                    if (headlightsOn)
                    {
                        Material[] mats = r.sharedMaterials;
                        for (int m = 0; m < mats.Length; m++)
                        {
                            if (mats[m] != null && !mats[m].IsKeywordEnabled("_EMISSION"))
                            {
                                mats[m].EnableKeyword("_EMISSION");
                            }
                        }
                    }
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(EmissionColorId, headEmission);
                    r.SetPropertyBlock(_mpb);
                }
            }
        }

        public void SetPlayerDriving(bool driving)
        {
            _isPlayerDriving = driving;
            TurkishCarRadio.SetDrivingActive(driving);
            if (driving)
            {
                CarryableProp.DropHeldPropIfAny();
            }
            UpdateSeatedDriverVisibility();

            if (!driving)
            {
                CurrentThrottle = 0f;
                CurrentGear = 0;
                IsBraking = false;
                IsReversing = false;
                if (InputHub.Instance != null)
                {
                    InputHub.Instance.ClearDrivingTouchInputs();
                }
            }
            else if (CurrentGear == 0)
            {
                CurrentGear = 1;
            }
        }

        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;
        }

        public Vector3 GetSafeExitPosition(float playerRadius = 0.42f, float playerHeight = 1.8f)
        {
            Transform[] candidates = { leftExitPoint, rightExitPoint };
            foreach (Transform candidate in candidates)
            {
                if (candidate == null) continue;
                Vector3 pos = candidate.position;
                Vector3 bottom = pos + Vector3.up * (playerRadius + 0.1f);
                Vector3 top = pos + Vector3.up * Mathf.Max(playerHeight - playerRadius, playerRadius + 0.2f);
                if (!Physics.CheckCapsule(bottom, top, playerRadius, ~0, QueryTriggerInteraction.Ignore))
                {
                    return pos;
                }
            }

            return transform.position + (-transform.right * 2.2f) + Vector3.up * 0.2f;
        }

        public void TeleportAndReset(Vector3 position, Quaternion rotation)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position, rotation);
            _currentSteerAngle = 0f;
            _prevVelocity = Vector3.zero;
            _bodyPitch = 0f;
            _bodyRoll = 0f;
            _shiftTimer = 0f;
            EngineRpm = idleRpm;
            CurrentGear = _isPlayerDriving ? 1 : 0;
            IsBraking = false;
            IsReversing = false;
            CurrentThrottle = 0f;
            MaxRearSidewaysSlip = 0f;
            if (bodyVisualRoot != null)
            {
                bodyVisualRoot.localRotation = Quaternion.identity;
            }
        }

        private float EvaluateEngineTorque(float rpm)
        {
            if (rpm >= redlineRpm)
            {
                return 0f;
            }
            if (rpm <= 1000f)
            {
                return Mathf.Lerp(155f, 180f, Mathf.InverseLerp(idleRpm, 1000f, rpm));
            }
            if (rpm <= 3500f)
            {
                return Mathf.Lerp(180f, 260f, Mathf.InverseLerp(1000f, 3500f, rpm));
            }
            if (rpm <= 6200f)
            {
                return Mathf.Lerp(260f, 200f, Mathf.InverseLerp(3500f, 6200f, rpm));
            }
            return Mathf.Lerp(200f, 140f, Mathf.InverseLerp(6200f, redlineRpm, rpm));
        }

        private void Update()
        {
            if (!_isPlayerDriving || _inputLocked || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.hKey.wasPressedThisFrame)
            {
                if (GameFeelAudio.PlayCarHorn())
                {
                    StartleNearbyPedestriansFromHorn();
                }
            }

            if (Keyboard.current.lKey.wasPressedThisFrame)
            {
                _headlightsManualOverride = true;
                _headlightsManualState = !_lastHeadlightOnVisual;
                UpdateHeadlightState();
                GameFeelAudio.PlayUiClick();
                if (PlayerModeController.Instance != null)
                {
                    PlayerModeController.Instance.RaiseStatusMessage(_headlightsManualState ? "Farlar açıldı." : "Farlar kapatıldı.");
                }
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                string station = TurkishCarRadio.CycleStation();
                GameFeelAudio.PlayUiClick();
                if (PlayerModeController.Instance != null)
                {
                    PlayerModeController.Instance.RaiseStatusMessage($"Araç Radyosu: {station}");
                }
            }
        }

        private void StartleNearbyPedestriansFromHorn()
        {
            Vector3 probeCenter = transform.position + transform.forward * 7.5f;
            Collider[] hits = Physics.OverlapSphere(probeCenter, 9.5f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null) continue;
                TownPedestrian ped = hits[i].GetComponentInParent<TownPedestrian>();
                if (ped != null)
                {
                    ProceduralHumanoid hum = ped.GetComponentInChildren<ProceduralHumanoid>();
                    if (hum != null)
                    {
                        hum.PlayStartled(1.25f);
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (transform.position.y < -3.5f)
            {
                Vector3 safePos = new Vector3(
                    Mathf.Clamp(transform.position.x, -160f, 160f),
                    0.35f,
                    Mathf.Clamp(transform.position.z, -160f, 160f));
                TeleportAndReset(safePos, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
                return;
            }
            if (_rb == null || axleInfos == null || axleInfos.Count == 0)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            if (_rammedCoastTimer > 0f)
            {
                _rammedCoastTimer = Mathf.Max(0f, _rammedCoastTimer - dt);
            }

            float speedKmh = SpeedKmh;
            float forwardMs = ForwardSpeedMs;

            // Reliable high-speed pedestrian run-over & hood-launch physics sweep
            if (speedKmh >= 8f)
            {
                CheckPedestrianSweep(speedKmh);
            }

            // Auto-repair damaged vehicle when stopped outside No:73 Selman Sokağı Oto Tamir (24.5, 4.5)
            if (_isPlayerDriving && speedKmh < 3.5f && _damageSystem != null && _damageSystem.Health < 99f)
            {
                Vector3 p = transform.position;
                if (p.x >= 18f && p.x <= 31f && p.z >= -4f && p.z <= 8f)
                {
                    _damageSystem.RepairFull();
                    if (PlayerModeController.Instance != null)
                    {
                        PlayerModeController.Instance.RaiseStatusMessage("No:73 Oto Tamir: Araç tamamen onarıldı!");
                    }
                }
            }

            float steerInput = 0f;
            float throttleInput = 0f;
            float brakeInput = 0f;
            bool handbrake = false;

            if (_isPlayerDriving && !_inputLocked && InputHub.Instance != null)
            {
                steerInput = InputHub.Instance.DriveSteer;
                throttleInput = InputHub.Instance.DriveThrottle;
                brakeInput = InputHub.Instance.DriveBrake;
                handbrake = InputHub.Instance.HandbrakeHeld;
            }

            float speedFactor120 = Mathf.InverseLerp(0f, 120f, speedKmh);
            float maxSteerAtSpeed = Mathf.Lerp(maxSteeringAngleLowSpeed, maxSteeringAngleHighSpeed, speedFactor120);
            float steerRate = Mathf.Lerp(steerRateLowSpeed, steerRateHighSpeed, speedFactor120);

            if (Mathf.Abs(steerInput) > 0.01f)
            {
                float targetSteer = steerInput * maxSteerAtSpeed;
                _currentSteerAngle = Mathf.MoveTowards(_currentSteerAngle, targetSteer, steerRate * dt);
            }
            else
            {
                float centerRate = Mathf.Lerp(65f, 145f, Mathf.InverseLerp(0f, 60f, speedKmh));
                _currentSteerAngle = Mathf.MoveTowards(_currentSteerAngle, 0f, centerRate * dt);
            }

            ComputeAckermannAngles(_currentSteerAngle, out float leftSteerAngle, out float rightSteerAngle);

            if (_shiftTimer > 0f) _shiftTimer = Mathf.Max(0f, _shiftTimer - dt);
            if (_shiftCooldown > 0f) _shiftCooldown = Mathf.Max(0f, _shiftCooldown - dt);

            float drivenWheelRpmSum = 0f;
            int drivenWheelCount = 0;
            foreach (AxleInfo axle in axleInfos)
            {
                if (!axle.motor) continue;
                if (axle.leftWheel != null)
                {
                    drivenWheelRpmSum += Mathf.Abs(axle.leftWheel.rpm);
                    drivenWheelCount++;
                }
                if (axle.rightWheel != null)
                {
                    drivenWheelRpmSum += Mathf.Abs(axle.rightWheel.rpm);
                    drivenWheelCount++;
                }
            }
            float avgWheelRpm = drivenWheelCount > 0 ? drivenWheelRpmSum / drivenWheelCount : 0f;

            float effectiveThrottle = 0f;
            float serviceBrake = 0f;
            bool parked = !_isPlayerDriving || _inputLocked;

            if (parked)
            {
                CurrentGear = 0;
                IsBraking = false;
                IsReversing = false;
            }
            else
            {
                if (throttleInput > 0.01f)
                {
                    if (forwardMs < -0.6f)
                    {
                        serviceBrake = throttleInput;
                        IsBraking = true;
                        IsReversing = false;
                    }
                    else
                    {
                        if (CurrentGear <= 0) CurrentGear = 1;
                        effectiveThrottle = throttleInput;
                        IsBraking = false;
                        IsReversing = false;
                    }
                }
                else if (brakeInput > 0.01f)
                {
                    if (forwardMs > 0.6f)
                    {
                        serviceBrake = brakeInput;
                        IsBraking = true;
                        IsReversing = false;
                    }
                    else
                    {
                        CurrentGear = -1;
                        IsBraking = false;
                        IsReversing = true;
                        if (-forwardMs * 3.6f < maxReverseSpeedKmh)
                        {
                            effectiveThrottle = brakeInput;
                        }
                    }
                }
                else
                {
                    IsBraking = handbrake;
                    IsReversing = CurrentGear == -1 && forwardMs < -0.15f;
                    if (CurrentGear == -1 && Mathf.Abs(forwardMs) < 0.25f)
                    {
                        CurrentGear = 1;
                    }
                }
            }

            bool hasFuel = fuel > 0.0001f;
            if (!hasFuel)
            {
                if (_isPlayerDriving && effectiveThrottle > 0.1f && !_warnedEmptyFuel)
                {
                    _warnedEmptyFuel = true;
                    if (PlayerNeedsController.Instance != null)
                    {
                        // Surface empty-tank hint once
                        PlayerModeController mode = PlayerModeController.Instance;
                        if (mode != null)
                        {
                            mode.RaiseStatusMessage(LocalizationService.Tr("ui.fuel.empty", "Yakıt bitti! Benzin istasyonuna uğra."));
                        }
                    }
                }
                effectiveThrottle = 0f;
            }

            CurrentThrottle = effectiveThrottle;

            float activeGearRatio = 0f;
            if (CurrentGear == -1)
            {
                activeGearRatio = reverseGearRatio;
            }
            else if (CurrentGear >= 1 && gearRatios != null && gearRatios.Length > 0)
            {
                int idx = Mathf.Clamp(CurrentGear - 1, 0, gearRatios.Length - 1);
                activeGearRatio = gearRatios[idx];
            }

            float wheelDrivenRpm = avgWheelRpm * activeGearRatio * finalDriveRatio;
            float clutchSlipFloor = hasFuel ? (idleRpm + effectiveThrottle * 750f) : 0f;
            float targetRpm = (CurrentGear == 0 || !hasFuel)
                ? (hasFuel ? idleRpm : 0f)
                : Mathf.Clamp(Mathf.Max(clutchSlipFloor, wheelDrivenRpm), idleRpm, redlineRpm);

            if (_shiftTimer > 0f && hasFuel)
            {
                targetRpm = Mathf.Max(idleRpm, targetRpm * 0.85f);
            }

            EngineRpm = Mathf.Lerp(EngineRpm, targetRpm, 1f - Mathf.Exp(-10f * dt));

            // Consume fuel proportional to throttle * rpm (full tank ≈ 15 minutes of driving)
            if (_isPlayerDriving && !_inputLocked && hasFuel)
            {
                float rpmNorm = Mathf.Clamp01(EngineRpm / Mathf.Max(1000f, redlineRpm));
                float burnFactor = 0.03f + 0.97f * (effectiveThrottle * rpmNorm);
                float burnPerSec = burnFactor / 380f;
                fuel = Mathf.Max(0f, fuel - burnPerSec * dt);

                if (PlayerModeController.Instance != null && PlayerModeController.Instance.PlayerSedan == this &&
                    SaveManager.Instance != null && SaveManager.Instance.Data != null)
                {
                    SaveManager.Instance.Data.missionSedanFuel = fuel;
                }
            }

            if (!parked && hasFuel && CurrentGear >= 1 && gearRatios != null && _shiftTimer <= 0f && _shiftCooldown <= 0f)
            {
                if (EngineRpm >= upshiftRpm && CurrentGear < gearRatios.Length && effectiveThrottle > 0.1f)
                {
                    CurrentGear++;
                    _shiftTimer = shiftDuration;
                    _shiftCooldown = shiftDuration + 0.30f;
                    activeGearRatio = gearRatios[CurrentGear - 1];
                }
                else if (EngineRpm <= downshiftRpm && CurrentGear > 1)
                {
                    CurrentGear--;
                    _shiftTimer = shiftDuration * 0.75f;
                    _shiftCooldown = shiftDuration + 0.30f;
                    activeGearRatio = gearRatios[CurrentGear - 1];
                }
            }

            float perDrivenWheelTorque = 0f;
            float damageTorqueMult = _damageSystem != null ? _damageSystem.EngineTorqueMultiplier : 1f;
            if (!parked && hasFuel && damageTorqueMult > 0.01f && !handbrake && _shiftTimer <= 0f && effectiveThrottle > 0.001f && drivenWheelCount > 0 && activeGearRatio > 0f)
            {
                float engineTorque = EvaluateEngineTorque(EngineRpm) * damageTorqueMult;
                if (SaveManager.Instance != null && SaveManager.Instance.Data != null && SaveManager.Instance.Data.hasTurboKit)
                {
                    engineTorque *= 1.28f;
                }
                float totalWheelTorque = engineTorque * effectiveThrottle * activeGearRatio * finalDriveRatio * drivetrainEfficiency;
                float sign = CurrentGear == -1 ? -1f : 1f;
                perDrivenWheelTorque = (totalWheelTorque / drivenWheelCount) * sign;
            }

            float engineBrakeTorque = 0f;
            if (!parked && !handbrake && serviceBrake <= 0.01f && effectiveThrottle <= 0.01f && CurrentGear != 0 && Mathf.Abs(forwardMs) > 0.4f)
            {
                float rpmNorm = Mathf.InverseLerp(idleRpm, redlineRpm, EngineRpm);
                engineBrakeTorque = 18f + rpmNorm * 130f;
            }

            float targetRearStiffness = (!parked && handbrake) ? handbrakeRearSidewaysStiffness : baseRearSidewaysStiffness;
            float stiffnessRate = targetRearStiffness < _currentRearSidewaysStiffness ? 12f : 4.5f;
            _currentRearSidewaysStiffness = Mathf.MoveTowards(_currentRearSidewaysStiffness, targetRearStiffness, stiffnessRate * dt);

            float maxRearSlip = 0f;

            for (int i = 0; i < axleInfos.Count; i++)
            {
                AxleInfo axle = axleInfos[i];
                if (axle.leftWheel == null || axle.rightWheel == null) continue;

                bool isFront = axle.steering;
                bool isRear = !axle.steering;

                if (axle.steering)
                {
                    axle.leftWheel.steerAngle = leftSteerAngle;
                    axle.rightWheel.steerAngle = rightSteerAngle;
                }

                if (axle.motor)
                {
                    axle.leftWheel.motorTorque = perDrivenWheelTorque;
                    axle.rightWheel.motorTorque = perDrivenWheelTorque;
                }
                else
                {
                    axle.leftWheel.motorTorque = 0f;
                    axle.rightWheel.motorTorque = 0f;
                }

                if (isRear)
                {
                    WheelFrictionCurve sideL = axle.leftWheel.sidewaysFriction;
                    sideL.stiffness = _currentRearSidewaysStiffness;
                    axle.leftWheel.sidewaysFriction = sideL;

                    WheelFrictionCurve sideR = axle.rightWheel.sidewaysFriction;
                    sideR.stiffness = _currentRearSidewaysStiffness;
                    axle.rightWheel.sidewaysFriction = sideR;
                }

                float baseWheelBrake = 0f;
                bool applyAbs = false;
                if (parked)
                {
                    // Release parked brake briefly when rammed so parked cars physically shove & spin!
                    baseWheelBrake = _rammedCoastTimer > 0f ? 140f : parkedBrakeTorque;
                }
                else if (handbrake && isRear)
                {
                    baseWheelBrake = parkedBrakeTorque;
                }
                else if (serviceBrake > 0.01f)
                {
                    float biasMultiplier = isFront ? (frontBrakeBias * 2f) : ((1f - frontBrakeBias) * 2f);
                    baseWheelBrake = maxBrakeTorque * serviceBrake * biasMultiplier;
                    applyAbs = true;
                }
                else if (axle.motor && engineBrakeTorque > 0f)
                {
                    baseWheelBrake = engineBrakeTorque;
                }

                axle.leftWheel.brakeTorque = ComputeAbsBrakeTorque(axle.leftWheel, baseWheelBrake, applyAbs, isRear, ref maxRearSlip);
                axle.rightWheel.brakeTorque = ComputeAbsBrakeTorque(axle.rightWheel, baseWheelBrake, applyAbs, isRear, ref maxRearSlip);

                ApplyAntiRoll(axle.leftWheel, axle.rightWheel);
                ApplyLocalPositionToVisuals(axle.leftWheel, axle.leftWheelVisual);
                ApplyLocalPositionToVisuals(axle.rightWheel, axle.rightWheelVisual);
            }

            MaxRearSidewaysSlip = maxRearSlip;

            // --- Rechargeable Nitro Boost (Left Shift while driving) + Drift-to-Charge & Drift Cash Bonus! ---
            if (_isPlayerDriving && !_inputLocked && fuel > 0.01f && (_damageSystem == null || !_damageSystem.IsStalled))
            {
                bool isDriftingNow = maxRearSlip > 0.34f && speedKmh > 20f;
                if (isDriftingNow)
                {
                    _driftStreakSeconds += dt;
                    _driftGraceTimer = 0.45f;
                    _nitroCharge = Mathf.Min(1f, _nitroCharge + dt * 0.42f);
                }
                else
                {
                    if (_driftGraceTimer > 0f)
                    {
                        _driftGraceTimer -= dt;
                    }
                    else if (_driftStreakSeconds >= 1.1f)
                    {
                        int driftBonus = Mathf.Clamp(Mathf.RoundToInt(_driftStreakSeconds * 16f), 15, 65);
                        if (SaveManager.Instance != null)
                        {
                            SaveManager.Instance.AddMoney(driftBonus);
                        }
                        GameFeelAudio.PlayCoinChime();
                        if (PlayerModeController.Instance != null)
                        {
                            PlayerModeController.Instance.RaiseStatusMessage($"Harika Drift! (+{driftBonus} ₺ · Nitro: %{Mathf.RoundToInt(_nitroCharge * 100f)})");
                        }
                        _driftStreakSeconds = 0f;
                    }
                    else
                    {
                        _driftStreakSeconds = 0f;
                    }
                }

                bool shiftHeld = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
                if (shiftHeld && throttleInput > 0.1f && _nitroCharge > 0.04f)
                {
                    _wasNitroEmptyWarned = false;
                    _nitroCharge = Mathf.Max(0f, _nitroCharge - dt * 0.40f);
                    _rb.AddForce(transform.forward * (_rb.mass * 10.5f), ForceMode.Force);
                    _nitroFxTimer -= dt;
                    if (_nitroFxTimer <= 0f)
                    {
                        _nitroFxTimer = 0.11f;
                        Vector3 exhaustPos = transform.TransformPoint(new Vector3(0f, 0.34f, -2.35f));
                        GameFeelAudio.SpawnBurst(exhaustPos, new Color(0.26f, 0.78f, 1.0f), count: 4, spread: 0.28f);
                    }
                }
                else
                {
                    if (shiftHeld && _nitroCharge <= 0.04f && !_wasNitroEmptyWarned)
                    {
                        _wasNitroEmptyWarned = true;
                        if (PlayerModeController.Instance != null)
                        {
                            PlayerModeController.Instance.RaiseStatusMessage("Nitro bitti — Drift yaparak hızla doldur!");
                        }
                    }
                    _nitroCharge = Mathf.Min(1f, _nitroCharge + dt * 0.11f);
                }
            }

            Vector3 vel = _rb.linearVelocity;
            float speedMs = vel.magnitude;
            if (speedMs > 0.05f)
            {
                float aeroDrag = 0.5f * airDensity * dragCoefficient * frontalArea * speedMs * speedMs;
                float rollingDrag = rollingResistanceFactor * speedMs;
                _rb.AddForce(-(vel / speedMs) * (aeroDrag + rollingDrag), ForceMode.Force);
            }

            _rb.AddForce(-transform.up * (downforceFactor * speedMs), ForceMode.Force);
            UpdateBodyRollPitch(vel, dt);
        }

        private void CheckPedestrianSweep(float speedKmh)
        {
            Vector3 boxCenter = transform.TransformPoint(new Vector3(0f, 0.85f, 0.15f));
            Vector3 halfExtents = new Vector3(1.06f, 0.85f, 2.38f);
            int count = Physics.OverlapBoxNonAlloc(boxCenter, halfExtents, _pedOverlapBuffer, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = _pedOverlapBuffer[i];
                if (col == null) continue;
                TownPedestrian ped = col.GetComponentInParent<TownPedestrian>();
                if (ped == null) continue;

                ped.OnHitByVehicle(transform.position, _rb.linearVelocity, speedKmh);
                if (_damageSystem != null)
                {
                    Vector3 hitPt = col.ClosestPoint(transform.position + transform.forward * 1.8f + Vector3.up * 0.75f);
                    _damageSystem.ApplyPedestrianHitFeedback(hitPt, -transform.forward, speedKmh, _isPlayerDriving);
                }
            }
        }

        private void LateUpdate()
        {
            UpdateLightVisuals();

            if (steeringWheelTransform != null && _steeringWheelCached)
            {
                steeringWheelTransform.localRotation = _steeringWheelBaseRot * Quaternion.Euler(0f, -_currentSteerAngle * 8f, 0f);
            }

            if (_isPlayerDriving && _seatedDriverHumanoid != null)
            {
                _seatedDriverHumanoid.SetSteeringAngle(_currentSteerAngle);
                _seatedDriverHumanoid.SetSteeringWheel(steeringWheelTransform);
            }
        }

        private void ComputeAckermannAngles(float centerSteerDeg, out float leftDeg, out float rightDeg)
        {
            if (Mathf.Abs(centerSteerDeg) < 0.05f)
            {
                leftDeg = 0f;
                rightDeg = 0f;
                return;
            }

            float tanAngle = Mathf.Tan(Mathf.Abs(centerSteerDeg) * Mathf.Deg2Rad);
            float turnRadius = wheelbase / Mathf.Max(0.0001f, tanAngle);
            float halfTrack = trackWidth * 0.5f;

            float innerDeg = Mathf.Atan(wheelbase / Mathf.Max(0.5f, turnRadius - halfTrack)) * Mathf.Rad2Deg;
            float outerDeg = Mathf.Atan(wheelbase / (turnRadius + halfTrack)) * Mathf.Rad2Deg;
            innerDeg = Mathf.Clamp(innerDeg, 0f, 45f);
            outerDeg = Mathf.Clamp(outerDeg, 0f, 45f);

            if (centerSteerDeg > 0f)
            {
                leftDeg = outerDeg;
                rightDeg = innerDeg;
            }
            else
            {
                leftDeg = -innerDeg;
                rightDeg = -outerDeg;
            }
        }

        private float ComputeAbsBrakeTorque(
            WheelCollider wc,
            float requestedBrake,
            bool applyAbs,
            bool trackRearSlip,
            ref float maxRearSlip)
        {
            if (wc.GetGroundHit(out WheelHit hit))
            {
                if (trackRearSlip)
                {
                    float sideSlip = Mathf.Abs(hit.sidewaysSlip);
                    if (sideSlip > maxRearSlip) maxRearSlip = sideSlip;
                }

                if (applyAbs && requestedBrake > 0f && Mathf.Abs(hit.forwardSlip) > absSlipThreshold)
                {
                    return requestedBrake * 0.5f;
                }
            }

            return requestedBrake;
        }

        private void UpdateBodyRollPitch(Vector3 currentVel, float dt)
        {
            if (bodyVisualRoot == null) return;

            Vector3 worldAccel = (currentVel - _prevVelocity) / Mathf.Max(0.001f, dt);
            _prevVelocity = currentVel;

            float longAccel = Vector3.Dot(worldAccel, transform.forward);
            float latAccel = Vector3.Dot(worldAccel, transform.right);

            float targetPitch = Mathf.Clamp(-longAccel * 0.25f, -2.5f, 2.5f);
            float targetRoll = Mathf.Clamp(latAccel * 0.30f, -3.5f, 3.5f);

            float blend = 1f - Mathf.Exp(-7.5f * dt);
            _bodyPitch = Mathf.Lerp(_bodyPitch, targetPitch, blend);
            _bodyRoll = Mathf.Lerp(_bodyRoll, targetRoll, blend);

            bodyVisualRoot.localRotation = Quaternion.Euler(_bodyPitch, 0f, _bodyRoll);
        }

        private void UpdateLightVisuals()
        {
            if (!_lightsCached) CacheVisualReferences();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            UpdateHeadlightState();

            if (IsBraking != _lastBrakingVisual)
            {
                _lastBrakingVisual = IsBraking;
                float mult = IsBraking ? 4f : 1f;
                for (int i = 0; i < _taillightRenderers.Count; i++)
                {
                    Renderer r = _taillightRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(EmissionColorId, _taillightBaseEmissions[i] * mult);
                    r.SetPropertyBlock(_mpb);
                }
            }

            if (IsReversing != _lastReversingVisual)
            {
                _lastReversingVisual = IsReversing;
                Color revColor = IsReversing ? new Color(1.0f, 0.98f, 0.92f) * 3.5f : Color.black;
                for (int i = 0; i < _reverseRenderers.Count; i++)
                {
                    Renderer r = _reverseRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(EmissionColorId, revColor);
                    r.SetPropertyBlock(_mpb);
                }
            }
        }

        private void ApplyAntiRoll(WheelCollider leftWheel, WheelCollider rightWheel)
        {
            float travelL = 1f;
            float travelR = 1f;

            bool groundedL = leftWheel.GetGroundHit(out WheelHit hitL);
            if (groundedL)
            {
                travelL = (-leftWheel.transform.InverseTransformPoint(hitL.point).y - leftWheel.radius) /
                          Mathf.Max(0.01f, leftWheel.suspensionDistance);
            }

            bool groundedR = rightWheel.GetGroundHit(out WheelHit hitR);
            if (groundedR)
            {
                travelR = (-rightWheel.transform.InverseTransformPoint(hitR.point).y - rightWheel.radius) /
                          Mathf.Max(0.01f, rightWheel.suspensionDistance);
            }

            float antiRoll = (travelL - travelR) * antiRollForce;
            if (groundedL)
            {
                _rb.AddForceAtPosition(leftWheel.transform.up * -antiRoll, leftWheel.transform.position);
            }
            if (groundedR)
            {
                _rb.AddForceAtPosition(rightWheel.transform.up * antiRoll, rightWheel.transform.position);
            }
        }

        private static void ApplyLocalPositionToVisuals(WheelCollider collider, Transform visualWheel)
        {
            if (visualWheel == null)
            {
                return;
            }

            collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            visualWheel.position = position;
            visualWheel.rotation = rotation;
        }
    }
}
