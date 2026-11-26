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
    public enum VehicleRoadSurface
    {
        Asphalt = 0,
        Cobblestone = 1,
        OffroadGrass = 2,
        Sand = 3
    }

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
    [DefaultExecutionOrder(-20)]
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
        private float _shiftPitchKick;
        private OurWorld.CameraSystem.ThirdPersonCameraRig _cachedCamRig;
        private bool _nitroVisualActive;

        // Real-world detail state: car alarm, water hydro-lock flooding, rain spray, driver crash cooldown,
        // road surface feel, progressive dirt accumulation, turn signals / emergency hazards, and tire skid marks
        private float _alarmTimer;
        private float _alarmPulseTimer;
        private bool _alarmFlashOn;
        private bool _alarmHighTone;
        private float _deepWaterSubmergeTime;
        private float _waterFloodTimer;
        private float _wetSprayTimer;
        private float _driverCrashHurtCooldown;
        private EngineAudio _engineAudio;
        private float _surfaceSampleTimer;
        private float _skidMarkTimer;
        private float _offroadDustTimer;
        private float _dirtiness01;
        private float _appliedDirtiness = -1f;
        private int _manualSignalState; // -1 = left, 0 = off, +1 = right, 2 = 4-way hazards
        private bool _signalTurnEngaged;
        private float _blinkerTimer;
        private bool _blinkerPhaseOn;
        private bool _blinkerWasActive;
        private readonly List<Renderer> _leftSignalRenderers = new List<Renderer>(4);
        private readonly List<Renderer> _rightSignalRenderers = new List<Renderer>(4);
        private readonly List<Renderer> _paintBodyRenderers = new List<Renderer>(8);

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
        private float _driftSmokeTimer;
        private float _exhaustWispTimer;
        private float _driftStreakSeconds;
        private float _driftGraceTimer;
        private float _burnoutCharge01;
        private bool _wasNitroEmptyWarned;
        public bool IsAllWheelsAirborne { get; private set; }

        private ProceduralHumanoid _seatedDriverHumanoid;
        private Light _headlightBeam;
        private Quaternion _steeringWheelBaseRot = Quaternion.identity;
        private bool _steeringWheelCached;
        private bool _cockpitUpgraded;
        private bool _wheelVisualsSynced;

        // Phase 2 Tuning & Car Wash state + Phase 4 Oto Galeri vehicle spec
        private float _tuningTurboMult = 1.0f;
        private float _dealershipTorqueMult = 1.0f;
        private float _tuningBrakeMult = 1.0f;
        private Vector3 _baseBodyScale = Vector3.one;
        private bool _baseBodyScaleCached;
        private GameObject _spoilerObj;
        private GameObject _roofRackObj;

        public static readonly Color[] TuningPaintColors =
        {
            new Color(0.11f, 0.56f, 0.58f), // 0: İstanbul Turkuazı
            new Color(0.76f, 0.14f, 0.14f), // 1: Ateş Kırmızısı
            new Color(0.10f, 0.11f, 0.14f), // 2: Gece Siyahı
            new Color(0.94f, 0.94f, 0.92f), // 3: İnci Beyazı
            new Color(0.14f, 0.44f, 0.24f), // 4: Aydos Yeşili
            new Color(0.92f, 0.72f, 0.14f), // 5: Altın Sarısı
            new Color(0.42f, 0.18f, 0.62f), // 6: Metalik Mor
            new Color(0.16f, 0.42f, 0.82f)  // 7: Boğaz Mavisi
        };

        public static readonly Color[] TuningRimColors =
        {
            new Color(0.84f, 0.86f, 0.88f), // 0: Klasik Gümüş
            new Color(0.14f, 0.14f, 0.16f), // 1: Spor Mat Siyah
            new Color(0.92f, 0.72f, 0.22f), // 2: Altın Alaşım
            new Color(0.14f, 0.76f, 0.72f)  // 3: Turkuaz Krom
        };

        public bool IsPlayerDriving => _isPlayerDriving;
        public bool AreHeadlightsOn => _lastHeadlightOnVisual;
        public bool IsEngineFlooded => _waterFloodTimer > 0f;
        public bool IsAlarmActive => _alarmTimer > 0f;
        public VehicleRoadSurface CurrentRoadSurface { get; private set; } = VehicleRoadSurface.Asphalt;
        public bool IsRoadWet { get; private set; }
        public float Dirtiness01 => _dirtiness01;
        public bool HazardLightsActive => _manualSignalState == 2 || IsAutoEmergencyHazards();
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
            _engineAudio = GetComponent<EngineAudio>();
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
            if (!_isPlayerDriving)
            {
                TriggerCarAlarm(7.5f);
            }
        }

        /// <summary>
        /// Triggers a real-world pulsating parked car alarm (flashing headlights/taillights + siren chirp + nearby witness shock).
        /// </summary>
        public void TriggerCarAlarm(float duration = 7.5f)
        {
            bool newlyTriggered = _alarmTimer <= 0.05f;
            _alarmTimer = Mathf.Max(_alarmTimer, Mathf.Clamp(duration, 2.5f, 12f));
            if (newlyTriggered)
            {
                _alarmPulseTimer = 0f;
                _alarmFlashOn = true;
                _alarmHighTone = true;
                AlertNearbyWitnessesToAlarm();
                NpcWorldAwareness.NotifyCarAlarm(transform.position);
            }
        }

        public void SilenceCarAlarm()
        {
            if (_alarmTimer <= 0f && !_alarmFlashOn) return;
            _alarmTimer = 0f;
            _alarmFlashOn = false;
            _headlightsInitialized = false;
            _lastBrakingVisual = !IsBraking;
            UpdateHeadlightState();
        }

        private void AlertNearbyWitnessesToAlarm()
        {
            bool en = LocalizationService.IsEnglish;
            int alerted = 0;
            for (int i = 0; i < TownPedestrian.ActiveCitizens.Count && alerted < 2; i++)
            {
                TownPedestrian ped = TownPedestrian.ActiveCitizens[i];
                if (ped == null || ped.IsBusyOrPanicking) continue;
                Vector3 d = ped.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 14f * 14f) continue;
                ProceduralHumanoid hum = ped.GetComponentInChildren<ProceduralHumanoid>();
                hum?.PlayStartled(1.0f, transform.position);
                if (alerted == 0)
                {
                    NpcSpeechBubbleManager.Say(
                        ped.transform,
                        ped.CitizenName,
                        en ? "Whoa! Someone's car alarm is going off!" : "Eyvah! Kimin arabasının alarmı çalıyor?!",
                        2.8f,
                        NpcBubbleMood.Alert,
                        3.5f);
                }
                alerted++;
            }
        }

        private void Start()
        {
            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.OnNightStateChanged += HandleNightStateChanged;
            }
            UpdateHeadlightState();

            if (SaveManager.Instance != null && SaveManager.Instance.Data != null &&
                PlayerModeController.Instance != null && PlayerModeController.Instance.PlayerSedan == this)
            {
                ApplyTuningFromSave(SaveManager.Instance.Data);
            }
        }

        public void ApplyTuningFromSave(SaveData data)
        {
            if (data == null) return;

            _tuningTurboMult = data.carHasTurboUpgrade ? 1.24f : 1.0f;
            _tuningBrakeMult = data.carHasSportSuspension ? 1.28f : 1.0f;
            ApplyDealershipChassisFromId(data.activeOwnedVehicleId);
            if (data.carHasSportSuspension)
            {
                baseRearSidewaysStiffness = 2.15f;
            }
            if (data.carHasNitroUpgrade)
            {
                _nitroCharge = Mathf.Max(_nitroCharge, 1.0f);
            }

            // 1. Live Metallic Body Paint
            if (bodyVisualRoot != null && data.carPaintIndex >= 0 && data.carPaintIndex < TuningPaintColors.Length)
            {
                Color paint = TuningPaintColors[data.carPaintIndex];
                Renderer[] renderers = bodyVisualRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null) continue;
                    string n = r.gameObject.name;
                    if (n.IndexOf("Headlight", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Taillight", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Reverse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Window", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Wheel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Spoiler", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("RoofRack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Driver", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    Material[] mats = r.materials;
                    bool changed = false;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        Material mat = mats[m];
                        if (mat == null) continue;
                        string mn = mat.name;
                        if (mn.IndexOf("Paint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mn.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mn.IndexOf("Car", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mn.IndexOf("Sedan", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", paint);
                            if (mat.HasProperty("_Color")) mat.SetColor("_Color", paint);
                            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.84f);
                            UrpRealismLightingOverhaul.EnableClearcoatOnMaterial(mat);
                            changed = true;
                        }
                    }
                    if (changed) r.materials = mats;
                }
            }

            // 2. Custom Alloy Rims on all 4 wheels
            if (data.carRimIndex >= 0 && data.carRimIndex < TuningRimColors.Length)
            {
                Color rimCol = TuningRimColors[data.carRimIndex];
                for (int i = 0; i < axleInfos.Count; i++)
                {
                    TintWheelRim(axleInfos[i].leftWheelVisual, rimCol);
                    TintWheelRim(axleInfos[i].rightWheelVisual, rimCol);
                }
            }

            // 3. Headlight Kit (0=Warm, 1=Ice Xenon, 2=Golden Rally)
            if (_headlightBeam != null)
            {
                _headlightBeam.color = data.carHeadlightMode == 1
                    ? new Color(0.76f, 0.92f, 1.0f)
                    : (data.carHeadlightMode == 2 ? new Color(1.0f, 0.86f, 0.36f) : new Color(1.0f, 0.95f, 0.82f));
                _headlightBeam.intensity = data.carHeadlightMode > 0 ? 155f : 128f;
                _headlightBeam.range = Mathf.Max(_headlightBeam.range, 68f);
                _headlightBeam.spotAngle = Mathf.Max(_headlightBeam.spotAngle, 84f);
                _headlightBeam.innerSpotAngle = Mathf.Max(_headlightBeam.innerSpotAngle, 48f);
            }

            // 4. Blender GT Spoiler & Overland Roof Rack mounts!
            Transform attachRoot = bodyVisualRoot != null ? bodyVisualRoot : transform;
            if (data.carHasSpoiler)
            {
                if (_spoilerObj == null)
                {
                    _spoilerObj = OurWorldModelLibrary.InstantiateModel(
                        "OW_CarSpoilerSport", attachRoot, new Vector3(0f, 0.96f, -1.96f), Quaternion.identity, Vector3.one * 0.92f);
                }
                if (_spoilerObj != null) _spoilerObj.SetActive(true);
            }
            else if (_spoilerObj != null)
            {
                _spoilerObj.SetActive(false);
            }

            if (data.carHasRoofRack)
            {
                if (_roofRackObj == null)
                {
                    _roofRackObj = OurWorldModelLibrary.InstantiateModel(
                        "OW_CarRoofRack", attachRoot, new Vector3(0f, 1.44f, -0.18f), Quaternion.identity, Vector3.one * 0.95f);
                }
                if (_roofRackObj != null) _roofRackObj.SetActive(true);
            }
            else if (_roofRackObj != null)
            {
                _roofRackObj.SetActive(false);
            }
        }

        public void ApplyDealershipChassisFromId(string vehicleId)
        {
            if (bodyVisualRoot != null && !_baseBodyScaleCached)
            {
                _baseBodyScale = bodyVisualRoot.localScale;
                _baseBodyScaleCached = true;
            }

            Vector3 stance = Vector3.one;
            switch (vehicleId)
            {
                case "car_aydos_suv":
                    _dealershipTorqueMult = 1.14f;
                    stance = new Vector3(1.04f, 1.14f, 1.02f);
                    break;
                case "car_hamidiye_pickup":
                    _dealershipTorqueMult = 1.18f;
                    stance = new Vector3(1.06f, 1.08f, 1.08f);
                    break;
                case "car_bogaz_coupe":
                    _dealershipTorqueMult = 1.28f;
                    stance = new Vector3(1.05f, 0.91f, 1.02f);
                    break;
                case "car_turkuaz_gt":
                    _dealershipTorqueMult = 1.42f;
                    stance = new Vector3(1.09f, 0.88f, 1.05f);
                    break;
                default:
                    _dealershipTorqueMult = 1.0f;
                    stance = Vector3.one;
                    break;
            }

            if (bodyVisualRoot != null && _baseBodyScaleCached)
            {
                bodyVisualRoot.localScale = Vector3.Scale(_baseBodyScale, stance);
            }
        }

        public void SparklePolishClean()
        {
            _dirtiness01 = 0f;
            _appliedDirtiness = -1f;
            if (_damageSystem != null)
            {
                _damageSystem.RepairFull();
            }
            if (bodyVisualRoot != null)
            {
                Renderer[] renderers = bodyVisualRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null || r.sharedMaterial == null) continue;
                    if (r.sharedMaterial.name.IndexOf("Paint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        r.sharedMaterial.name.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Material m = r.material;
                        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.88f);
                        UrpRealismLightingOverhaul.EnableClearcoatOnMaterial(m);
                    }
                }
            }
        }

        private static void TintWheelRim(Transform wheelVisual, Color rimCol)
        {
            if (wheelVisual == null) return;
            Renderer[] rends = wheelVisual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null) continue;
                Material[] mats = r.materials;
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null) continue;
                    string mn = mat.name;
                    if (mn.IndexOf("Tire", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("Rubber", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", rimCol);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", rimCol);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.72f);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.76f);
                    changed = true;
                }
                if (changed) r.materials = mats;
            }
        }

        private void OnDestroy()
        {
            if (_nitroVisualActive && _cachedCamRig != null)
            {
                _cachedCamRig.SetNitroBoost(false);
                _nitroVisualActive = false;
            }
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

            // Hard crash driver whiplash / airbag injury (> ~41 km/h sudden collision impact)
            if (_isPlayerDriving && relSpeed >= 11.4f && _driverCrashHurtCooldown <= 0f && PlayerNeedsController.Instance != null)
            {
                _driverCrashHurtCooldown = 1.5f;
                float crashSeverity = Mathf.Clamp01((relSpeed - 11.4f) / 14.5f);
                float driverDmg = Mathf.Lerp(6f, 26f, crashSeverity);
                PlayerNeedsController.Instance.TakeVehicleCrashDamage(driverDmg, contact.point, severe: relSpeed >= 17.5f);
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
                _leftSignalRenderers.Clear();
                _rightSignalRenderers.Clear();
                _paintBodyRenderers.Clear();

                Renderer[] renderers = bodyVisualRoot.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    string objName = r.gameObject.name;
                    if (objName == "Placeholder_HeadlightL" || objName == "Placeholder_HeadlightR" || objName == "Car_Headlights")
                    {
                        _headlightRenderers.Add(r);
                        float localX = bodyVisualRoot.InverseTransformPoint(r.bounds.center).x;
                        if (objName.EndsWith("L", StringComparison.Ordinal) || localX < -0.15f) _leftSignalRenderers.Add(r);
                        else if (objName.EndsWith("R", StringComparison.Ordinal) || localX > 0.15f) _rightSignalRenderers.Add(r);
                        else { _leftSignalRenderers.Add(r); _rightSignalRenderers.Add(r); }
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
                        float localX = bodyVisualRoot.InverseTransformPoint(r.bounds.center).x;
                        if (objName.EndsWith("L", StringComparison.Ordinal) || localX < -0.15f) _leftSignalRenderers.Add(r);
                        else if (objName.EndsWith("R", StringComparison.Ordinal) || localX > 0.15f) _rightSignalRenderers.Add(r);
                        else { _leftSignalRenderers.Add(r); _rightSignalRenderers.Add(r); }
                    }
                    else if (objName == "Placeholder_ReverseL" || objName == "Placeholder_ReverseR" || objName == "Car_ReverseLights")
                    {
                        _reverseRenderers.Add(r);
                    }
                    else if (r.sharedMaterial != null &&
                             (r.sharedMaterial.name.IndexOf("Paint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              r.sharedMaterial.name.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        _paintBodyRenderers.Add(r);
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
                _headlightBeam.range = Mathf.Max(_headlightBeam.range, 68f);
                _headlightBeam.spotAngle = Mathf.Max(_headlightBeam.spotAngle, 84f);
                _headlightBeam.innerSpotAngle = Mathf.Max(_headlightBeam.innerSpotAngle, 48f);
                _headlightBeam.intensity = Mathf.Max(_headlightBeam.intensity, 128f);
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

            if (!_cockpitUpgraded && steeringWheelTransform != null)
            {
                _cockpitUpgraded = true;
                UpgradeCockpitSteeringWheelAndGlow();
            }
        }

        /// <summary>
        /// Replaces primitive cylinder/cube steering wheel with sculpted Blender OW_SteeringWheel.fbx
        /// (contoured leather rim, red 12-o'clock sport stripe, brushed 3-spoke chrome hub)
        /// and adds subtle glowing cockpit dashboard gauge & infotainment accents for 1st-Person Cockpit view!
        /// </summary>
        private void UpgradeCockpitSteeringWheelAndGlow()
        {
            GameObject sculptedWheel = OurWorldModelLibrary.InstantiateModel(
                "OW_SteeringWheel", steeringWheelTransform, Vector3.zero, Quaternion.identity, Vector3.one, removeColliders: true);
            if (sculptedWheel != null)
            {
                for (int i = 0; i < steeringWheelTransform.childCount; i++)
                {
                    Transform ch = steeringWheelTransform.GetChild(i);
                    if (ch != null && ch.gameObject != sculptedWheel &&
                        (ch.name.Contains("Rim") || ch.name.Contains("Spoke")))
                    {
                        ch.gameObject.SetActive(false);
                    }
                }
            }

            // Subtle glowing cockpit instrument cluster & center infotainment screen accent (visible through windshield & in 1st Person!)
            if (bodyVisualRoot != null && bodyVisualRoot.Find("Cockpit_GaugeGlow") == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (lit != null)
                {
                    Material gaugeMat = new Material(lit) { name = "MAT_Cockpit_GaugeGlow" };
                    Color cyanGlow = new Color(0.28f, 0.86f, 1.0f, 1f);
                    if (gaugeMat.HasProperty("_BaseColor")) gaugeMat.SetColor("_BaseColor", new Color(0.08f, 0.14f, 0.18f, 1f));
                    if (gaugeMat.HasProperty("_EmissionColor"))
                    {
                        gaugeMat.EnableKeyword("_EMISSION");
                        gaugeMat.SetColor("_EmissionColor", cyanGlow * 1.45f);
                    }

                    Vector3 swPos = steeringWheelTransform.localPosition;
                    GameObject dashGlow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    dashGlow.name = "Cockpit_GaugeGlow";
                    dashGlow.transform.SetParent(bodyVisualRoot, false);
                    dashGlow.transform.localPosition = new Vector3(swPos.x, swPos.y + 0.055f, swPos.z + 0.22f);
                    dashGlow.transform.localRotation = Quaternion.Euler(10f, 180f, 0f);
                    dashGlow.transform.localScale = new Vector3(0.26f, 0.072f, 1f);
                    Collider dc = dashGlow.GetComponent<Collider>();
                    if (dc != null) Destroy(dc);
                    Renderer dr = dashGlow.GetComponent<Renderer>();
                    if (dr != null)
                    {
                        dr.sharedMaterial = gaugeMat;
                        dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }

                    GameObject navGlow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    navGlow.name = "Cockpit_NavGlow";
                    navGlow.transform.SetParent(bodyVisualRoot, false);
                    navGlow.transform.localPosition = new Vector3(0.02f, swPos.y + 0.055f, swPos.z + 0.21f);
                    navGlow.transform.localRotation = Quaternion.Euler(8f, 180f, 0f);
                    navGlow.transform.localScale = new Vector3(0.19f, 0.105f, 1f);
                    Collider nc = navGlow.GetComponent<Collider>();
                    if (nc != null) Destroy(nc);
                    Renderer nr = navGlow.GetComponent<Renderer>();
                    if (nr != null)
                    {
                        nr.sharedMaterial = gaugeMat;
                        nr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
            }
        }

        /// <summary>
        /// Computes authentic driver-seat eye position inside the 3D cockpit behind the rotating steering wheel,
        /// looking over the glowing instrument binnacle and sloped clearcoat hood through the transparent windshield.
        /// Automatically adapts cleanly across Sedan, Hatchback, SportsCoupe, SUV, Pickup & DeliveryVan!
        /// </summary>
        public Vector3 GetFirstPersonCockpitEyeWorldPos()
        {
            if (steeringWheelTransform != null && bodyVisualRoot != null)
            {
                Vector3 swLocal = steeringWheelTransform.localPosition;
                // Place eye right behind the steering wheel with comfortable headroom below the dark roof headliner
                float eyeUp = swLocal.y < 0.92f ? 0.19f : 0.22f; // Extra-low roof clearance on SportsCoupe
                Vector3 localEye = new Vector3(swLocal.x, swLocal.y + eyeUp, swLocal.z - 0.36f);
                return bodyVisualRoot.TransformPoint(localEye);
            }
            return transform.TransformPoint(new Vector3(-0.36f, 1.21f, -0.05f));
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
                SilenceCarAlarm();
                if (_engineAudio == null) _engineAudio = GetComponent<EngineAudio>();
                if (fuel > 0.01f && _waterFloodTimer <= 0f && (_damageSystem == null || !_damageSystem.IsStalled))
                {
                    _engineAudio?.PlayIgnitionStart();
                }
            }
            UpdateSeatedDriverVisibility();

            if (!driving)
            {
                CurrentThrottle = 0f;
                CurrentGear = 0;
                IsBraking = false;
                IsReversing = false;
                if (_nitroVisualActive)
                {
                    _nitroVisualActive = false;
                    if (_cachedCamRig != null) _cachedCamRig.SetNitroBoost(false);
                }
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
            if (leftExitPoint != null && TryValidateExitPoint(leftExitPoint.position, playerRadius, playerHeight, out Vector3 safePos))
            {
                return safePos;
            }
            if (rightExitPoint != null && TryValidateExitPoint(rightExitPoint.position, playerRadius, playerHeight, out safePos))
            {
                return safePos;
            }

            // Wider left/right and rear-quarter fallbacks when parked close to a wall or curb
            Vector3 basePos = transform.position;
            Vector3 right = transform.right;
            Vector3 fwd = transform.forward;
            if (TryValidateExitPoint(basePos - right * 2.15f, playerRadius, playerHeight, out safePos)) return safePos;
            if (TryValidateExitPoint(basePos + right * 2.15f, playerRadius, playerHeight, out safePos)) return safePos;
            if (TryValidateExitPoint(basePos - right * 1.65f - fwd * 2.2f, playerRadius, playerHeight, out safePos)) return safePos;
            if (TryValidateExitPoint(basePos + right * 1.65f - fwd * 2.2f, playerRadius, playerHeight, out safePos)) return safePos;

            Vector3 fallback = basePos - right * 2.2f;
            if (Physics.Raycast(fallback + Vector3.up * 1.6f, Vector3.down, out RaycastHit hit, 4.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                fallback.y = hit.point.y + 0.05f;
            }
            else
            {
                fallback.y += 0.12f;
            }
            return fallback;
        }

        private bool TryValidateExitPoint(Vector3 candidatePos, float playerRadius, float playerHeight, out Vector3 snappedPos)
        {
            snappedPos = candidatePos;
            if (!Physics.Raycast(candidatePos + Vector3.up * 1.35f, Vector3.down, out RaycastHit ground, 3.2f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.65f)
            {
                return false;
            }

            snappedPos = new Vector3(candidatePos.x, ground.point.y + 0.05f, candidatePos.z);
            Vector3 bottom = snappedPos + Vector3.up * (playerRadius + 0.12f);
            Vector3 top = snappedPos + Vector3.up * Mathf.Max(playerHeight - playerRadius, playerRadius + 0.25f);
            return !Physics.CheckCapsule(bottom, top, playerRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
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

        public void TriggerHorn()
        {
            if (GameFeelAudio.PlayCarHorn())
            {
                StartleNearbyPedestriansFromHorn();
                RealWorldDetailsSystem.Instance?.NotifyPlayerCarHorn(transform.position);
            }
        }

        public void ToggleHeadlights()
        {
            _headlightsManualOverride = true;
            _headlightsManualState = !_lastHeadlightOnVisual;
            UpdateHeadlightState();
            GameFeelAudio.PlayUiClick();
            if (PlayerModeController.Instance != null)
            {
                bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
                PlayerModeController.Instance.RaiseStatusMessage(_headlightsManualState
                    ? (isEn ? "Headlights turned on." : "Farlar açıldı.")
                    : (isEn ? "Headlights turned off." : "Farlar kapatıldı."));
            }
        }

        public string CycleCarRadio()
        {
            string station = TurkishCarRadio.CycleStation();
            GameFeelAudio.PlayUiClick();
            if (PlayerModeController.Instance != null)
            {
                bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
                PlayerModeController.Instance.RaiseStatusMessage(isEn ? $"Car Radio: {station}" : $"Araç Radyosu: {station}");
            }
            return station;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_driverCrashHurtCooldown > 0f)
            {
                _driverCrashHurtCooldown = Mathf.Max(0f, _driverCrashHurtCooldown - dt);
            }

            if (_alarmTimer > 0f)
            {
                _alarmTimer -= dt;
                _alarmPulseTimer -= dt;
                if (_alarmPulseTimer <= 0f)
                {
                    _alarmPulseTimer = 0.44f;
                    _alarmFlashOn = !_alarmFlashOn;
                    if (_alarmFlashOn)
                    {
                        _alarmHighTone = !_alarmHighTone;
                        RealWorldDetailsSystem.PlayCarAlarmChirp(transform.position, _alarmHighTone);
                    }
                    ApplyAlarmStrobeVisuals(_alarmFlashOn);
                }
                if (_alarmTimer <= 0f)
                {
                    SilenceCarAlarm();
                }
            }

            if (!_isPlayerDriving || _inputLocked || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.hKey.wasPressedThisFrame)
            {
                TriggerHorn();
            }

            if (Keyboard.current.lKey.wasPressedThisFrame)
            {
                ToggleHeadlights();
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                CycleCarRadio();
            }

            if (Keyboard.current.zKey.wasPressedThisFrame)
            {
                ToggleTurnSignal(-1);
            }
            else if (Keyboard.current.cKey.wasPressedThisFrame)
            {
                ToggleTurnSignal(1);
            }
            else if (Keyboard.current.xKey.wasPressedThisFrame)
            {
                ToggleTurnSignal(2);
            }
        }

        private bool IsAutoEmergencyHazards()
        {
            return _waterFloodTimer > 0f || (_damageSystem != null && (_damageSystem.IsStalled || _damageSystem.Health < 28f));
        }

        public void ToggleTurnSignal(int mode)
        {
            _manualSignalState = (_manualSignalState == mode) ? 0 : mode;
            _signalTurnEngaged = false;
            _blinkerTimer = 0f;
            _blinkerPhaseOn = false;
            bool en = LocalizationService.IsEnglish;
            if (_manualSignalState == 0)
            {
                RestoreLightsAfterBlinker();
            }
            else if (PlayerModeController.Instance != null)
            {
                string msg = _manualSignalState == -1
                    ? (en ? "◀ Left turn signal ON" : "◀ Sol sinyal açık")
                    : (_manualSignalState == 1
                        ? (en ? "▶ Right turn signal ON" : "▶ Sağ sinyal açık")
                        : (en ? "⚠ Hazard flashers ON" : "⚠ Dörtlü flaşörler açık"));
                PlayerModeController.Instance.RaiseStatusMessage(msg);
            }
        }

        private void RestoreLightsAfterBlinker()
        {
            _headlightsInitialized = false;
            _lastBrakingVisual = !IsBraking;
            UpdateHeadlightState();
        }

        private void ApplyAlarmStrobeVisuals(bool on)
        {
            if (!_lightsCached) CacheVisualReferences();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            Color headCol = on ? new Color(1.0f, 0.72f, 0.14f) * 4.2f : Color.black;
            for (int i = 0; i < _headlightRenderers.Count; i++)
            {
                Renderer r = _headlightRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorId, headCol);
                r.SetPropertyBlock(_mpb);
            }

            float tailMult = on ? 4.2f : 0.25f;
            for (int i = 0; i < _taillightRenderers.Count; i++)
            {
                Renderer r = _taillightRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorId, _taillightBaseEmissions[i] * tailMult);
                r.SetPropertyBlock(_mpb);
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
                    Mathf.Clamp(transform.position.x, -416f, 416f),
                    0.35f,
                    Mathf.Clamp(transform.position.z, -416f, 416f));
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
                        bool isEnRep = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
                PlayerModeController.Instance.RaiseStatusMessage(isEnRep ? "No:73 Auto Garage: Vehicle fully repaired!" : "No:73 Oto Tamir: Araç tamamen onarıldı!");
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

            float speedFactor120 = IsReversing ? 0f : Mathf.InverseLerp(0f, 120f, speedKmh);
            float maxSteerAtSpeed = Mathf.Lerp(maxSteeringAngleLowSpeed, maxSteeringAngleHighSpeed, speedFactor120);
            float baseSteerRate = Mathf.Lerp(Mathf.Max(steerRateLowSpeed, 165f), Mathf.Max(steerRateHighSpeed, 78f), speedFactor120);

            if (Mathf.Abs(steerInput) > 0.01f)
            {
                float targetSteer = steerInput * maxSteerAtSpeed;
                // Snappy 2.15x counter-steering boost when reversing wheel direction across center
                bool counterSteering = _currentSteerAngle * targetSteer < -0.5f;
                float steerRate = counterSteering ? baseSteerRate * 2.15f : baseSteerRate;
                _currentSteerAngle = Mathf.MoveTowards(_currentSteerAngle, targetSteer, steerRate * dt);
            }
            else
            {
                float centerRate = Mathf.Lerp(110f, 185f, Mathf.InverseLerp(0f, 60f, speedKmh));
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
                    // Tactile gear-shift suspension nod, exhaust puff & subtle FOV kick
                    _shiftPitchKick = 1.65f;
                    if (_isPlayerDriving)
                    {
                        if (_cachedCamRig == null) _cachedCamRig = FindAnyObjectByType<OurWorld.CameraSystem.ThirdPersonCameraRig>();
                        _cachedCamRig?.AddFovImpulse(1.25f);
                        _engineAudio?.PlayGearShiftClick(upshift: true);
                        Vector3 shiftExhaust = transform.TransformPoint(new Vector3(-0.42f, 0.30f, -2.28f));
                        VisualEffectsManager.SpawnNitroExhaust(shiftExhaust, -transform.forward, isNitro: false);
                    }
                }
                else if (EngineRpm <= downshiftRpm && CurrentGear > 1)
                {
                    CurrentGear--;
                    _shiftTimer = shiftDuration * 0.75f;
                    _shiftCooldown = shiftDuration + 0.30f;
                    activeGearRatio = gearRatios[CurrentGear - 1];
                    _shiftPitchKick = -0.85f;
                    if (_isPlayerDriving)
                    {
                        _engineAudio?.PlayGearShiftClick(upshift: false);
                    }
                }
            }

            // Real-world water flooding / hydro-lock + wet rain road physics + surface texture & puddle splash!
            float roadWetness = NeighborhoodWeatherSystem.Instance != null ? NeighborhoodWeatherSystem.Instance.Wetness01 : 0f;
            bool isRaining = NeighborhoodWeatherSystem.Instance != null &&
                             NeighborhoodWeatherSystem.Instance.CurrentWeather == NeighborhoodWeatherState.SummerShower;
            bool inPuddle = IstanbulThemeOverhaul.IsPositionInPuddle(transform.position);
            IsRoadWet = isRaining || roadWetness > 0.35f || inPuddle;

            _surfaceSampleTimer -= dt;
            if (_surfaceSampleTimer <= 0f)
            {
                _surfaceSampleTimer = 0.22f;
                SampleVehicleRoadSurface();
            }

            // Gradual realistic paint dirt accumulation off-road or in rain (cleaned at Aydos Oto Yıkama!)
            if (_isPlayerDriving && speedKmh > 14f)
            {
                float dirtRate = CurrentRoadSurface == VehicleRoadSurface.OffroadGrass || CurrentRoadSurface == VehicleRoadSurface.Sand
                    ? 0.018f
                    : (IsRoadWet ? 0.0055f : 0f);
                if (dirtRate > 0f && _dirtiness01 < 1f)
                {
                    _dirtiness01 = Mathf.Min(1f, _dirtiness01 + dirtRate * dt);
                    if (_dirtiness01 - _appliedDirtiness >= 0.08f)
                    {
                        _appliedDirtiness = _dirtiness01;
                        ApplyPaintDirtinessVisual();
                    }
                }
            }

            bool inWater = WaterZone.TrySampleWater(transform.position, out float waterSurfY, out _);
            float waterDepth = inWater ? (waterSurfY - transform.position.y) : 0f;

            if (waterDepth > 0.22f)
            {
                // Heavy hydrodynamic drag in deep water + splash spray
                _rb.AddForce(-_rb.linearVelocity * (_rb.mass * Mathf.Clamp(waterDepth * 2.2f, 0.5f, 3.4f)), ForceMode.Force);
                _wetSprayTimer -= dt;
                if (_wetSprayTimer <= 0f && speedKmh > 6f)
                {
                    _wetSprayTimer = 0.20f;
                    VisualEffectsManager.SpawnWaterSplash(
                        new Vector3(transform.position.x, waterSurfY, transform.position.z),
                        bigDive: speedKmh > 24f);
                }

                if (waterDepth > 0.52f)
                {
                    _deepWaterSubmergeTime += dt;
                    if (_deepWaterSubmergeTime >= 0.85f && _waterFloodTimer <= 0f)
                    {
                        _waterFloodTimer = 7.0f;
                        RealWorldDetailsSystem.PlayEngineSputter();
                        Vector3 hoodPos = transform.TransformPoint(new Vector3(0f, 0.95f, 1.55f));
                        VisualEffectsManager.SpawnTireSmoke(hoodPos, Vector3.up * 1.4f, 1.25f);
                        if (_isPlayerDriving && PlayerModeController.Instance != null)
                        {
                            bool enW = LocalizationService.IsEnglish;
                            PlayerModeController.Instance.RaiseStatusMessage(enW
                                ? "🌊 Engine hydro-locked in deep water! Exit the car or wait for it to dry."
                                : "🌊 Motor su aldı ve stop etti! Sudan çık ya da kurumasını bekle.");
                        }
                    }
                }
            }
            else
            {
                _deepWaterSubmergeTime = Mathf.Max(0f, _deepWaterSubmergeTime - dt * 2f);
                if (_waterFloodTimer > 0f)
                {
                    _waterFloodTimer = Mathf.Max(0f, _waterFloodTimer - dt);
                    if (_waterFloodTimer <= 0f && _isPlayerDriving && PlayerModeController.Instance != null)
                    {
                        bool enDry = LocalizationService.IsEnglish;
                        PlayerModeController.Instance.RaiseStatusMessage(enDry
                            ? "🔧 Engine dried out and restarted!"
                            : "🔧 Motor kurudu, tekrar çalıştı!");
                    }
                }
            }

            if ((isRaining || inPuddle) && !inWater && _isPlayerDriving && speedKmh > (inPuddle ? 10f : 22f))
            {
                _wetSprayTimer -= dt;
                if (_wetSprayTimer <= 0f)
                {
                    _wetSprayTimer = inPuddle ? 0.09f : 0.14f;
                    Vector3 rearL = transform.TransformPoint(new Vector3(-0.82f, 0.14f, -1.45f));
                    Vector3 rearR = transform.TransformPoint(new Vector3( 0.82f, 0.14f, -1.45f));
                    VisualEffectsManager.SpawnWaterSplash(rearL, bigDive: inPuddle && speedKmh > 32f);
                    VisualEffectsManager.SpawnWaterSplash(rearR, bigDive: inPuddle && speedKmh > 32f);
                }
            }

            // Off-road dirt/sand wheel dust plumes
            if ((CurrentRoadSurface == VehicleRoadSurface.OffroadGrass || CurrentRoadSurface == VehicleRoadSurface.Sand) &&
                !inWater && _isPlayerDriving && speedKmh > 16f)
            {
                _offroadDustTimer -= dt;
                if (_offroadDustTimer <= 0f)
                {
                    _offroadDustTimer = 0.12f;
                    Color dustCol = CurrentRoadSurface == VehicleRoadSurface.Sand
                        ? new Color(0.92f, 0.82f, 0.58f, 1f)
                        : new Color(0.54f, 0.48f, 0.36f, 1f);
                    VisualEffectsManager.SpawnFootDust(transform.TransformPoint(new Vector3(-0.82f, 0.06f, -1.38f)), false, dustCol);
                    VisualEffectsManager.SpawnFootDust(transform.TransformPoint(new Vector3( 0.82f, 0.06f, -1.38f)), false, dustCol);
                }
            }

            float perDrivenWheelTorque = 0f;
            float damageTorqueMult = _damageSystem != null ? _damageSystem.EngineTorqueMultiplier : 1f;
            if (!parked && hasFuel && _waterFloodTimer <= 0f && damageTorqueMult > 0.01f && !handbrake && _shiftTimer <= 0f && effectiveThrottle > 0.001f && drivenWheelCount > 0 && activeGearRatio > 0f)
            {
                float engineTorque = EvaluateEngineTorque(EngineRpm) * damageTorqueMult * _tuningTurboMult * _dealershipTorqueMult;
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

            float rainGripMult = IsRoadWet ? 0.80f : 1.0f;
            float offroadGripMult = (CurrentRoadSurface == VehicleRoadSurface.OffroadGrass || CurrentRoadSurface == VehicleRoadSurface.Sand) ? 0.78f : 1.0f;
            float targetRearStiffness = ((!parked && handbrake) ? handbrakeRearSidewaysStiffness : baseRearSidewaysStiffness) * rainGripMult * offroadGripMult;
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
                    baseWheelBrake = maxBrakeTorque * _tuningBrakeMult * serviceBrake * biasMultiplier;
                    applyAbs = true;
                }
                else if (axle.motor && engineBrakeTorque > 0f)
                {
                    baseWheelBrake = engineBrakeTorque;
                }

                axle.leftWheel.brakeTorque = ComputeAbsBrakeTorque(axle.leftWheel, baseWheelBrake, applyAbs, isRear, ref maxRearSlip);
                axle.rightWheel.brakeTorque = ComputeAbsBrakeTorque(axle.rightWheel, baseWheelBrake, applyAbs, isRear, ref maxRearSlip);

                ApplyAntiRoll(axle.leftWheel, axle.rightWheel);
            }

            MaxRearSidewaysSlip = maxRearSlip;

            // Check airborne state for GTA Mid-Air Pitch/Roll Control & Stunt Jump detection!
            bool anyWheelGrounded = false;
            for (int i = 0; i < axleInfos.Count; i++)
            {
                AxleInfo ax = axleInfos[i];
                if ((ax.leftWheel != null && ax.leftWheel.isGrounded) || (ax.rightWheel != null && ax.rightWheel.isGrounded))
                {
                    anyWheelGrounded = true;
                    break;
                }
            }
            IsAllWheelsAirborne = !anyWheelGrounded;

            // GTA 5/6 Mid-Air Chassis Control: W/S pitches nose, A/D rolls/steers smoothly while flying off stunt ramps!
            if (_isPlayerDriving && !_inputLocked && IsAllWheelsAirborne && speedKmh > 25f)
            {
                float pitchInput = throttleInput - brakeInput;
                _rb.AddRelativeTorque(new Vector3(pitchInput * _rb.mass * 1.15f, steerInput * _rb.mass * 0.45f, -steerInput * _rb.mass * 1.35f), ForceMode.Force);
            }

            // GTA Burnout Launch: hold Gas + Brake/Handbrake at standstill -> redline rubber smoke -> release for explosive launch!
            if (_isPlayerDriving && !_inputLocked && !IsAllWheelsAirborne && fuel > 0.01f && (_damageSystem == null || !_damageSystem.IsStalled))
            {
                bool burningOut = throttleInput > 0.65f && (brakeInput > 0.45f || handbrake) && speedKmh < 7.5f;
                if (burningOut)
                {
                    _burnoutCharge01 = Mathf.Min(1f, _burnoutCharge01 + dt * 0.85f);
                    EngineRpm = Mathf.Lerp(EngineRpm, redlineRpm * 0.90f, 1f - Mathf.Exp(-8f * dt));
                    _driftSmokeTimer -= dt;
                    if (_driftSmokeTimer <= 0f)
                    {
                        _driftSmokeTimer = 0.055f;
                        Vector3 rearL = transform.TransformPoint(new Vector3(-0.82f, 0.14f, -1.32f));
                        Vector3 rearR = transform.TransformPoint(new Vector3( 0.82f, 0.14f, -1.32f));
                        VisualEffectsManager.SpawnTireSmoke(rearL, -transform.forward * 2.2f, 1.30f);
                        VisualEffectsManager.SpawnTireSmoke(rearR, -transform.forward * 2.2f, 1.30f);
                    }
                }
                else if (_burnoutCharge01 >= 0.45f && throttleInput > 0.5f && brakeInput < 0.2f && !handbrake)
                {
                    float launchImpulse = Mathf.Lerp(5.5f, 10.5f, _burnoutCharge01);
                    _rb.AddForce(transform.forward * (_rb.mass * launchImpulse), ForceMode.Impulse);
                    _burnoutCharge01 = 0f;
                    _cachedCamRig?.AddFovImpulse(4.8f);
                    _cachedCamRig?.AddShake(0.14f, 0.22f);
                    Vector3 rearL = transform.TransformPoint(new Vector3(-0.82f, 0.14f, -1.32f));
                    Vector3 rearR = transform.TransformPoint(new Vector3( 0.82f, 0.14f, -1.32f));
                    VisualEffectsManager.SpawnTireSmoke(rearL, -transform.forward * 3.5f, 1.45f);
                    VisualEffectsManager.SpawnTireSmoke(rearR, -transform.forward * 3.5f, 1.45f);
                }
                else
                {
                    _burnoutCharge01 = Mathf.MoveTowards(_burnoutCharge01, 0f, dt * 1.4f);
                }
            }

            // Gentle drift yaw-stabilization assist: keeps handbrake drifts sweeping & controllable instead of uncontrollable snap-spins
            if (_isPlayerDriving && !_inputLocked && maxRearSlip > 0.32f && speedKmh > 18f)
            {
                float yawVel = _rb.angularVelocity.y;
                float excessYaw = Mathf.Abs(yawVel) > 1.35f ? yawVel : yawVel * 0.55f;
                float stabStrength = Mathf.InverseLerp(0.32f, 1.15f, maxRearSlip) * 1.45f;
                _rb.AddTorque(Vector3.up * (-excessYaw * stabStrength * _rb.mass * 0.42f), ForceMode.Force);
            }

            // --- Rechargeable Nitro Boost (Left Shift / Boost while driving) + Drift-to-Charge & Drift Cash Bonus! ---
            bool nitroActiveNow = false;
            if (_isPlayerDriving && !_inputLocked && fuel > 0.01f && (_damageSystem == null || !_damageSystem.IsStalled))
            {
                bool isDriftingNow = maxRearSlip > 0.34f && speedKmh > 20f;
                bool wantTireSmoke = (maxRearSlip > 0.30f && speedKmh > 16f) || (handbrake && speedKmh > 18f);
                if (wantTireSmoke)
                {
                    _driftSmokeTimer -= dt;
                    if (_driftSmokeTimer <= 0f)
                    {
                        _driftSmokeTimer = 0.055f;
                        float smokeIntensity = Mathf.Clamp(maxRearSlip * 1.3f, 0.7f, 1.35f);
                        Vector3 rearL = transform.TransformPoint(new Vector3(-0.82f, 0.14f, -1.32f));
                        Vector3 rearR = transform.TransformPoint(new Vector3( 0.82f, 0.14f, -1.32f));
                        VisualEffectsManager.SpawnTireSmoke(rearL, _rb.linearVelocity, smokeIntensity);
                        VisualEffectsManager.SpawnTireSmoke(rearR, _rb.linearVelocity, smokeIntensity);
                    }
                }

                // Lay realistic pooled tire skid marks / off-road ruts beneath rear wheels when drifting or hard-braking
                bool wantSkidMarks = (maxRearSlip > 0.28f && speedKmh > 14f) ||
                                     (handbrake && speedKmh > 14f) ||
                                     (serviceBrake > 0.72f && speedKmh > 38f);
                if (wantSkidMarks && !inWater)
                {
                    _skidMarkTimer -= dt;
                    if (_skidMarkTimer <= 0f)
                    {
                        _skidMarkTimer = 0.065f;
                        Color skidCol = CurrentRoadSurface == VehicleRoadSurface.Sand
                            ? new Color(0.58f, 0.48f, 0.32f, 0.46f)
                            : (CurrentRoadSurface == VehicleRoadSurface.OffroadGrass
                                ? new Color(0.22f, 0.18f, 0.12f, 0.48f)
                                : new Color(0.06f, 0.06f, 0.07f, IsRoadWet ? 0.34f : 0.56f));
                        Vector3 travelFwd = _rb.linearVelocity.sqrMagnitude > 1f ? _rb.linearVelocity.normalized : transform.forward;
                        float segLen = Mathf.Clamp(_rb.linearVelocity.magnitude * 0.085f, 0.48f, 1.35f);
                        Vector3 wheelL = transform.TransformPoint(new Vector3(-0.82f, 0.02f, -1.34f));
                        Vector3 wheelR = transform.TransformPoint(new Vector3( 0.82f, 0.02f, -1.34f));
                        WorldRealismAtmosphere.SpawnSkidMark(wheelL, travelFwd, 0.24f, segLen, skidCol);
                        WorldRealismAtmosphere.SpawnSkidMark(wheelR, travelFwd, 0.24f, segLen, skidCol);
                    }
                }
                if (effectiveThrottle > 0.65f && speedKmh < 38f)
                {
                    _exhaustWispTimer -= dt;
                    if (_exhaustWispTimer <= 0f)
                    {
                        _exhaustWispTimer = 0.16f;
                        Vector3 exhaustPos = transform.TransformPoint(new Vector3(-0.42f, 0.30f, -2.28f));
                        VisualEffectsManager.SpawnNitroExhaust(exhaustPos, -transform.forward, isNitro: false);
                    }
                }

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
                            bool enDrift = LocalizationService.IsEnglish;
                            int nitroPct = Mathf.RoundToInt(_nitroCharge * 100f);
                            PlayerModeController.Instance.RaiseStatusMessage(enDrift
                                ? $"Awesome Drift! (+{driftBonus} ₺ · Nitro: {nitroPct}%)"
                                : $"Harika Drift! (+{driftBonus} ₺ · Nitro: %{nitroPct})");
                        }
                        _driftStreakSeconds = 0f;
                    }
                    else
                    {
                        _driftStreakSeconds = 0f;
                    }
                }

                bool shiftHeld = (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ||
                                 (InputHub.Instance != null && InputHub.Instance.SprintHeld);
                if (shiftHeld && throttleInput > 0.1f && _nitroCharge > 0.04f)
                {
                    nitroActiveNow = true;
                    _wasNitroEmptyWarned = false;
                    _nitroCharge = Mathf.Max(0f, _nitroCharge - dt * 0.40f);
                    _rb.AddForce(transform.forward * (_rb.mass * 10.5f), ForceMode.Force);
                    _nitroFxTimer -= dt;
                    if (_nitroFxTimer <= 0f)
                    {
                        _nitroFxTimer = 0.055f;
                        Vector3 exhaustPos = transform.TransformPoint(new Vector3(-0.42f, 0.30f, -2.35f));
                        VisualEffectsManager.SpawnNitroExhaust(exhaustPos, -transform.forward, isNitro: true);
                        DayNightCycle.Instance?.PulseImpactChromatic(0.11f);
                    }
                }
                else
                {
                    if (shiftHeld && _nitroCharge <= 0.04f && !_wasNitroEmptyWarned)
                    {
                        _wasNitroEmptyWarned = true;
                        if (PlayerModeController.Instance != null)
                        {
                            bool isEnNitro = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
                            PlayerModeController.Instance.RaiseStatusMessage(isEnNitro ? "Nitro empty — drift to recharge quickly!" : "Nitro bitti — Drift yaparak hızla doldur!");
                        }
                    }
                    _nitroCharge = Mathf.Min(1f, _nitroCharge + dt * 0.11f);
                }
            }
            if (_isPlayerDriving && nitroActiveNow != _nitroVisualActive)
            {
                _nitroVisualActive = nitroActiveNow;
                if (_cachedCamRig == null) _cachedCamRig = FindAnyObjectByType<OurWorld.CameraSystem.ThirdPersonCameraRig>();
                _cachedCamRig?.SetNitroBoost(_nitroVisualActive);
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

        private float _lastRecklessAlertTime = -10f;

        private void CheckPedestrianSweep(float speedKmh)
        {
            if (_isPlayerDriving && speedKmh >= 66f && Time.time - _lastRecklessAlertTime > 4.5f)
            {
                _lastRecklessAlertTime = Time.time;
                NpcWorldAwareness.NotifyRecklessDriving(transform.position, transform.forward, speedKmh);
            }

            Vector3 boxCenter = transform.TransformPoint(new Vector3(0f, 0.85f, 0.15f));
            Vector3 halfExtents = new Vector3(1.06f, 0.85f, 2.38f);
            int count = Physics.OverlapBoxNonAlloc(boxCenter, halfExtents, _pedOverlapBuffer, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = _pedOverlapBuffer[i];
                if (col == null) continue;

                TownPedestrian ped = col.GetComponentInParent<TownPedestrian>();
                if (ped != null)
                {
                    ped.OnHitByVehicle(transform.position, _rb.linearVelocity, speedKmh);
                    if (_damageSystem != null)
                    {
                        Vector3 hitPt = col.ClosestPoint(transform.position + transform.forward * 1.8f + Vector3.up * 0.75f);
                        _damageSystem.ApplyPedestrianHitFeedback(hitPt, -transform.forward, speedKmh, _isPlayerDriving);
                    }
                    continue;
                }

                LivingNpcColliderBridge living = col.GetComponentInParent<LivingNpcColliderBridge>();
                if (living != null)
                {
                    living.NotifyHitByVehicle(transform.position, _rb.linearVelocity, speedKmh);
                    if (_damageSystem != null)
                    {
                        Vector3 hitPt = col.ClosestPoint(transform.position + transform.forward * 1.8f + Vector3.up * 0.75f);
                        _damageSystem.ApplyPedestrianHitFeedback(hitPt, -transform.forward, speedKmh, _isPlayerDriving);
                    }
                    continue;
                }

                // Wake kinematic-until-hit DynamicWorldProps on car collision so parked props fly cleanly!
                DynamicWorldProp dynProp = col.GetComponentInParent<DynamicWorldProp>();
                if (dynProp != null && speedKmh >= 6f)
                {
                    Vector3 hitPt = col.ClosestPoint(transform.position + transform.forward * 1.8f + Vector3.up * 0.55f);
                    Vector3 impulse = (_rb.linearVelocity * 0.75f + Vector3.up * Mathf.Clamp(speedKmh * 0.08f, 1.2f, 4.5f)) *
                                      Mathf.Clamp(dynProp.MassKg * 0.55f, 8f, 32f);
                    dynProp.ApplyPunchOrCrashImpulse(impulse, hitPt, speedKmh >= 24f);
                }
            }
        }

        private void LateUpdate()
        {
            UpdateLightVisuals();
            SyncWheelVisuals();

            if (steeringWheelTransform != null && _steeringWheelCached)
            {
                steeringWheelTransform.localRotation = _steeringWheelBaseRot * Quaternion.Euler(0f, -_currentSteerAngle * 4.5f, 0f);
            }

            if (_isPlayerDriving)
            {
                if (_cachedCamRig == null && UnityEngine.Camera.main != null)
                {
                    _cachedCamRig = UnityEngine.Camera.main.GetComponent<OurWorld.CameraSystem.ThirdPersonCameraRig>();
                }
                bool hideDriverForCockpit = _cachedCamRig != null && _cachedCamRig.IsFirstPerson && !_cachedCamRig.IsAimOrSportOverride;
                if (seatedDriverObject != null && seatedDriverObject.activeSelf == hideDriverForCockpit)
                {
                    seatedDriverObject.SetActive(!hideDriverForCockpit);
                }
                if (!hideDriverForCockpit && _seatedDriverHumanoid != null)
                {
                    _seatedDriverHumanoid.SetSteeringAngle(_currentSteerAngle);
                    _seatedDriverHumanoid.SetSteeringWheel(steeringWheelTransform);
                    float pedalHint = IsBraking ? -0.85f : CurrentThrottle;
                    _seatedDriverHumanoid.SetDrivingDynamics(pedalHint, -_bodyPitch * 4.0f);
                }
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

            _shiftPitchKick = Mathf.Lerp(_shiftPitchKick, 0f, 1f - Mathf.Exp(-9.5f * dt));
            float targetPitch = Mathf.Clamp(-longAccel * 0.28f + _shiftPitchKick, -3.2f, 3.2f);
            float targetRoll = Mathf.Clamp(latAccel * 0.32f, -3.8f, 3.8f);

            float blend = 1f - Mathf.Exp(-8.5f * dt);
            _bodyPitch = Mathf.Lerp(_bodyPitch, targetPitch, blend);
            _bodyRoll = Mathf.Lerp(_bodyRoll, targetRoll, blend);

            bodyVisualRoot.localRotation = Quaternion.Euler(_bodyPitch, 0f, _bodyRoll);

            if (_isPlayerDriving)
            {
                if (_cachedCamRig == null) _cachedCamRig = FindAnyObjectByType<OurWorld.CameraSystem.ThirdPersonCameraRig>();
                if (_cachedCamRig != null)
                {
                    float speedNorm = Mathf.InverseLerp(8f, 85f, currentVel.magnitude * 3.6f);
                    float latG = Mathf.Clamp(latAccel / 9.81f, -1.4f, 1.4f);
                    float roughness = CurrentRoadSurface switch
                    {
                        VehicleRoadSurface.OffroadGrass => 0.78f * speedNorm,
                        VehicleRoadSurface.Sand => 0.66f * speedNorm,
                        VehicleRoadSurface.Cobblestone => 0.44f * speedNorm,
                        _ => 0.08f * speedNorm
                    };
                    _cachedCamRig.SetDrivingDynamics(latG, roughness);
                }
            }
        }

        private void SampleVehicleRoadSurface()
        {
            if (axleInfos != null)
            {
                for (int i = 0; i < axleInfos.Count; i++)
                {
                    WheelCollider wc = axleInfos[i].leftWheel != null ? axleInfos[i].leftWheel : axleInfos[i].rightWheel;
                    if (wc != null && wc.GetGroundHit(out WheelHit hit) && hit.collider != null)
                    {
                        string n = hit.collider.gameObject.name.ToLowerInvariant();
                        if (n.Contains("sand") || n.Contains("beach") || n.Contains("shore"))
                        {
                            CurrentRoadSurface = VehicleRoadSurface.Sand;
                            return;
                        }
                        if (n.Contains("grass") || n.Contains("meadow") || n.Contains("lawn") || n.Contains("park") ||
                            n.Contains("farm") || n.Contains("hill") || n.Contains("turf") || n.Contains("valley"))
                        {
                            CurrentRoadSurface = VehicleRoadSurface.OffroadGrass;
                            return;
                        }
                        if (n.Contains("parke") || n.Contains("cobble") || n.Contains("plaza") ||
                            n.Contains("sidewalk") || n.Contains("promenade") || n.Contains("pavement"))
                        {
                            CurrentRoadSurface = VehicleRoadSurface.Cobblestone;
                            return;
                        }
                        CurrentRoadSurface = VehicleRoadSurface.Asphalt;
                        return;
                    }
                }
            }
            CurrentRoadSurface = VehicleRoadSurface.Asphalt;
        }

        private void ApplyPaintDirtinessVisual()
        {
            float gloss = Mathf.Lerp(0.84f, 0.32f, _dirtiness01);
            for (int i = 0; i < _paintBodyRenderers.Count; i++)
            {
                Renderer r = _paintBodyRenderers[i];
                if (r == null) continue;
                Material m = r.material;
                if (m != null && m.HasProperty("_Smoothness"))
                {
                    m.SetFloat("_Smoothness", gloss);
                }
            }
        }

        private void UpdateLightVisuals()
        {
            if (!_lightsCached) CacheVisualReferences();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            if (_alarmTimer > 0f)
            {
                return;
            }

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

            UpdateTurnAndHazardFlashers();
        }

        private void UpdateTurnAndHazardFlashers()
        {
            // Auto-cancel manual left/right signal once driver completes turn & straightens steering wheel
            if ((_manualSignalState == -1 || _manualSignalState == 1) && _isPlayerDriving)
            {
                float steerSign = _manualSignalState;
                if (_currentSteerAngle * steerSign > 13f) _signalTurnEngaged = true;
                else if (_signalTurnEngaged && Mathf.Abs(_currentSteerAngle) < 4.5f)
                {
                    _manualSignalState = 0;
                    _signalTurnEngaged = false;
                    RestoreLightsAfterBlinker();
                }
            }

            int activeSignal = IsAutoEmergencyHazards() ? 2 : _manualSignalState;
            if (activeSignal == 0)
            {
                if (_blinkerWasActive)
                {
                    _blinkerWasActive = false;
                    _blinkerPhaseOn = false;
                    RestoreLightsAfterBlinker();
                }
                return;
            }

            _blinkerWasActive = true;
            _blinkerTimer -= Time.deltaTime;
            if (_blinkerTimer <= 0f)
            {
                _blinkerTimer = 0.42f;
                _blinkerPhaseOn = !_blinkerPhaseOn;
                if (_isPlayerDriving)
                {
                    IstanbulLivingSoundscape.PlayBlinkerClick(_blinkerPhaseOn);
                }

                Color amberCol = _blinkerPhaseOn ? new Color(1.0f, 0.58f, 0.04f) * 4.4f : Color.black;
                if (activeSignal == -1 || activeSignal == 2)
                {
                    ApplySignalGroup(_leftSignalRenderers, _blinkerPhaseOn, amberCol);
                }
                if (activeSignal == 1 || activeSignal == 2)
                {
                    ApplySignalGroup(_rightSignalRenderers, _blinkerPhaseOn, amberCol);
                }
            }
        }

        private void ApplySignalGroup(List<Renderer> group, bool on, Color amberEmission)
        {
            if (group == null || group.Count == 0) return;
            for (int i = 0; i < group.Count; i++)
            {
                Renderer r = group[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                if (on)
                {
                    _mpb.SetColor(EmissionColorId, amberEmission);
                }
                else
                {
                    bool isHead = _headlightRenderers.Contains(r);
                    int tailIdx = _taillightRenderers.IndexOf(r);
                    if (isHead)
                    {
                        _mpb.SetColor(EmissionColorId, _lastHeadlightOnVisual ? new Color(1.0f, 0.92f, 0.70f) * 4.5f : Color.black);
                    }
                    else if (tailIdx >= 0 && tailIdx < _taillightBaseEmissions.Count)
                    {
                        _mpb.SetColor(EmissionColorId, _taillightBaseEmissions[tailIdx] * (IsBraking ? 4f : 1f));
                    }
                    else
                    {
                        _mpb.SetColor(EmissionColorId, Color.black);
                    }
                }
                r.SetPropertyBlock(_mpb);
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

        /// <summary>
        /// Wheel meshes follow the WheelColliders every rendered frame. The body Rigidbody is interpolated, so
        /// sampling GetWorldPose here (instead of in FixedUpdate) keeps tyres glued to the interpolated chassis
        /// and removes the per-physics-step wheel jitter/stutter visible at speed and at high frame rates.
        /// </summary>
        private void SyncWheelVisuals()
        {
            if (axleInfos == null) return;
            if (_rb != null && _rb.IsSleeping() && _wheelVisualsSynced) return;
            for (int i = 0; i < axleInfos.Count; i++)
            {
                AxleInfo axle = axleInfos[i];
                if (axle.leftWheel != null) ApplyLocalPositionToVisuals(axle.leftWheel, axle.leftWheelVisual);
                if (axle.rightWheel != null) ApplyLocalPositionToVisuals(axle.rightWheel, axle.rightWheelVisual);
            }
            _wheelVisualsSynced = true;
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
