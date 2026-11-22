using System;
using System.Collections;
using System.Collections.Generic;
using Gridroam.Core;
using Gridroam.Input;
using Gridroam.Missions;
using Gridroam.Player;
using Gridroam.UI;
using Gridroam.Vehicle;
using Gridroam.World;
using UnityEngine;

namespace Gridroam.Jobs
{
    /// <summary>
    /// Manages open-world Side Jobs (Pizza Delivery, Taxi, Street Cleanup, House Painting):
    /// - Only one active job at a time
    /// - Dedicated teal ObjectiveBeacon and separate "İŞ: ..." HUD line that never overwrites MissionManager
    /// - Supports cancellation via HUD Cancel button or Retry shortcut
    /// </summary>
    public class JobManager : MonoBehaviour
    {
        public static JobManager Instance { get; private set; }

        [Header("Scene Visuals")]
        [SerializeField] private ObjectiveBeacon jobBeacon;
        [SerializeField] private GameObject taxiPassengerObject;
        [SerializeField] private ProceduralHumanoid taxiPassengerHumanoid;
        [SerializeField] private Material litterBoxMaterial;
        [SerializeField] private Material litterCanMaterial;

        private static readonly string[] SwatchColorKeys =
        {
            "paint.color.0",
            "paint.color.1",
            "paint.color.2",
            "paint.color.3",
            "paint.color.4",
            "paint.color.5",
            "paint.color.6",
            "paint.color.7"
        };

        private static readonly string[] SwatchColorFallbacksTr =
        {
            "Beyaz",
            "Krem",
            "Kiremit",
            "Gök Mavisi",
            "Adaçayı Yeşili",
            "Açık Sarı",
            "Lila",
            "Antrasit"
        };

        // Validated Sultanbeyli · Selman Sokağı sidewalk pickup points (X in [-70..+92], Z in [-24..+5])
        private static readonly Vector3[] TaxiPickupSpots =
        {
            new Vector3(-64.0f, 0.08f,  4.7f), // No:48 Selman Sk. Önü
            new Vector3(-42.0f, 0.08f,  4.7f), // No:52 & Gürsu Sk. Karşısı
            new Vector3(  6.0f, 0.08f,  4.7f), // No:58 Selman Bakkal
            new Vector3( 38.0f, 0.08f, -4.7f), // No:66S Hafsa Tesisat
            new Vector3( 56.0f, 0.08f,  4.7f), // Nutuk Sk. Köşesi
            new Vector3( 71.5f, 0.08f, -4.7f), // No:81S Mavi Apartman
            new Vector3( 91.0f, 0.08f,  4.7f), // No:95 Çay Ocağı
            new Vector3( 82.0f, 0.08f, -23.5f) // Hz. Osman Camii Avlusu
        };

        private struct TaxiDestination
        {
            public string NameKey;
            public string FallbackTr;
            public Vector3 WorldPos;
        }

        // Validated drivable Sultanbeyli · Selman Sokağı dropoff points with safe sidewalk exit!
        private static readonly TaxiDestination[] TaxiDestinations =
        {
            new TaxiDestination { NameKey = "job.taxi.dest.seyyidkutup", FallbackTr = "Seyyid Kutup Cd. Kavşağı", WorldPos = new Vector3(-85.0f, 0.08f,  0.0f) },
            new TaxiDestination { NameKey = "job.taxi.dest.bakkal58",    FallbackTr = "No:58 Selman Bakkal",      WorldPos = new Vector3(  8.0f, 0.08f,  0.0f) },
            new TaxiDestination { NameKey = "job.taxi.dest.ototamir",    FallbackTr = "No:73 Oto Tamir",          WorldPos = new Vector3( 25.0f, 0.08f,  0.0f) },
            new TaxiDestination { NameKey = "job.taxi.dest.nutuk",       FallbackTr = "Nutuk Sokağı Girişi",      WorldPos = new Vector3( 59.2f, 0.08f, 11.5f) },
            new TaxiDestination { NameKey = "job.taxi.dest.camii",       FallbackTr = "Hz. Osman Camii",          WorldPos = new Vector3( 98.5f, 0.08f, -31.0f) },
            new TaxiDestination { NameKey = "job.taxi.dest.fuel",        FallbackTr = "Akaryakıt İstasyonu",      WorldPos = new Vector3(-84.5f, 0.08f,  9.5f) }
        };

        // 10 safe sidewalk/pocket litter points along Selman Sokağı (never forcing pedestrians into fast traffic!)
        private static readonly Vector3[] LitterSpawnPoints =
        {
            new Vector3(-70.0f, 0.08f,  4.9f),
            new Vector3(-54.0f, 0.08f, -4.9f),
            new Vector3(-34.0f, 0.08f,  4.9f),
            new Vector3(-16.0f, 0.08f, -4.9f),
            new Vector3(  2.0f, 0.08f,  4.9f),
            new Vector3( 18.0f, 0.08f, -4.9f),
            new Vector3( 36.0f, 0.08f,  4.9f),
            new Vector3( 54.0f, 0.08f, -4.9f),
            new Vector3( 74.0f, 0.08f,  4.9f),
            new Vector3( 86.0f, 0.08f, -24.5f)
        };

        private JobType _activeJob = JobType.None;
        private string _currentJobHudText = string.Empty;
        private bool _hasJobTimer;
        private float _jobRemainingTime;
        private bool _hasJobWaypoint;
        private Vector3 _jobWaypointPos;

        // Cached town references
        private PaintableHouse[] _allHouses;
        private SedanVehicleController[] _allVehicles;
        private JobBoardPoint[] _jobBoards;
        private AudioSource _sfxSource;
        private AudioClip _pickupBlipClip;

        // Pizza state
        private const int MaxPizzaChain = 5;
        private int _pizzaChainIndex;
        private PaintableHouse _pizzaTargetHouse;
        private float _lastPizzaRewardTime = -99f;

        // Taxi state
        private bool _taxiPassengerAboard;
        private Vector3 _taxiPickupPos;
        private TaxiDestination _taxiCurrentDest;
        private int _taxiHardCollisions;
        private float _taxiWaveTimer;
        private float _lastTaxiWarningTime = -99f;

        // Street Cleanup state
        private readonly List<LitterPickup> _spawnedLitter = new List<LitterPickup>();
        private readonly HashSet<LitterPickup> _awardedLitter = new HashSet<LitterPickup>();
        private int _litterCollected;
        private int _litterTotal;

        // House Painting state
        private PaintableHouse _targetPaintHouse;
        private int _targetPaintSwatchIndex = -1;

        public JobType ActiveJob => _activeJob;
        public bool HasActiveJob => _activeJob != JobType.None;
        public string CurrentJobHudText => _currentJobHudText;
        public bool HasJobTimer => _hasJobTimer;
        public float JobRemainingTime => _jobRemainingTime;
        public bool HasActiveWaypoint => _hasJobWaypoint;
        public Vector3 ActiveWaypointPosition => _jobWaypointPos;
        public PaintableHouse TargetPaintHouse => _activeJob == JobType.HousePainting ? _targetPaintHouse : null;
        public int TargetPaintSwatchIndex => _activeJob == JobType.HousePainting ? _targetPaintSwatchIndex : -1;

        public event Action OnJobStateChanged;

        public void Configure(
            ObjectiveBeacon beacon,
            GameObject taxiPassenger,
            ProceduralHumanoid passengerHum,
            Material boxMat,
            Material canMat)
        {
            jobBeacon = beacon;
            taxiPassengerObject = taxiPassenger;
            taxiPassengerHumanoid = passengerHum;
            litterBoxMaterial = boxMat;
            litterCanMaterial = canMat;
        }

        public static string GetLocalizedColorName(int swatchIndex)
        {
            int idx = Mathf.Clamp(swatchIndex, 0, SwatchColorKeys.Length - 1);
            return LocalizationService.Tr(SwatchColorKeys[idx], SwatchColorFallbacksTr[idx]);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureBlipAudio();
        }

        private void Start()
        {
            _allHouses = FindObjectsByType<PaintableHouse>(FindObjectsInactive.Include);
            _allVehicles = FindObjectsByType<SedanVehicleController>(FindObjectsInactive.Include);
            _jobBoards = FindObjectsByType<JobBoardPoint>(FindObjectsInactive.Include);

            for (int i = 0; i < _allVehicles.Length; i++)
            {
                if (_allVehicles[i] != null)
                {
                    _allVehicles[i].OnCollisionImpulse += HandleVehicleCollisionImpulse;
                }
            }

            PaintableHouse.OnAnyHousePainted += HandleAnyHousePainted;

            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnRetryPressed += HandleRetryOrCancelShortcut;
            }

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.OnMissionFailed += HandleStoryMissionFailed;
            }

            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged += HandleLanguageChanged;
            }

            if (jobBeacon != null)
            {
                jobBeacon.SetVisible(false);
            }
            if (taxiPassengerObject != null)
            {
                taxiPassengerObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_allVehicles != null)
            {
                for (int i = 0; i < _allVehicles.Length; i++)
                {
                    if (_allVehicles[i] != null)
                    {
                        _allVehicles[i].OnCollisionImpulse -= HandleVehicleCollisionImpulse;
                    }
                }
            }

            PaintableHouse.OnAnyHousePainted -= HandleAnyHousePainted;

            if (InputHub.Instance != null)
            {
                InputHub.Instance.OnRetryPressed -= HandleRetryOrCancelShortcut;
            }

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.OnMissionFailed -= HandleStoryMissionFailed;
            }

            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged -= HandleLanguageChanged;
            }

            if (_pickupBlipClip != null)
            {
                Destroy(_pickupBlipClip);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void EnsureBlipAudio()
        {
            _sfxSource = GetComponent<AudioSource>();
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
            }
            _sfxSource.playOnAwake = false;
            _sfxSource.loop = false;
            _sfxSource.spatialBlend = 0f;

            int sr = 44100;
            int samples = Mathf.RoundToInt(sr * 0.09f);
            float[] buf = new float[samples];
            double ph = 0.0;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = t < 0.5f ? 580f : 780f;
                ph += (2.0 * Math.PI * freq) / sr;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.12f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 5.5f);
                buf[i] = (float)Math.Sin(ph) * env * 0.25f;
            }
            _pickupBlipClip = AudioClip.Create("JobPickupBlip", samples, 1, sr, false);
            _pickupBlipClip.SetData(buf, 0);
        }

        private void PlayBlip()
        {
            if (_sfxSource != null && _pickupBlipClip != null)
            {
                _sfxSource.pitch = UnityEngine.Random.Range(0.96f, 1.06f);
                _sfxSource.PlayOneShot(_pickupBlipClip, 0.5f);
            }
        }

        public Vector3 GetJobBoardPosition(JobType type)
        {
            if (_jobBoards == null || _jobBoards.Length == 0)
            {
                _jobBoards = FindObjectsByType<JobBoardPoint>(FindObjectsInactive.Include);
            }
            for (int i = 0; i < _jobBoards.Length; i++)
            {
                if (_jobBoards[i] != null && _jobBoards[i].Type == type)
                {
                    return _jobBoards[i].transform.position;
                }
            }
            return Vector3.zero;
        }

        private void HandleStoryMissionFailed(string _)
        {
            if (HasActiveJob)
            {
                CancelActiveJob(silent: true);
            }
        }

        private void HandleRetryOrCancelShortcut()
        {
            if (MissionManager.Instance != null && MissionManager.Instance.IsFailed)
            {
                return;
            }
            if (HasActiveJob)
            {
                CancelActiveJob(silent: false);
            }
        }

        private void HandleLanguageChanged(string _)
        {
            if (HasActiveJob)
            {
                RefreshJobHudText();
                OnJobStateChanged?.Invoke();
            }
        }

        public void InteractWithJobBoard(JobBoardPoint board)
        {
            if (board == null) return;
            TryStartJob(board.Type);
        }

        public bool TryStartJob(JobType type)
        {
            if (type == JobType.None) return false;

            MissionManager mm = MissionManager.Instance;
            if (mm != null && (mm.IsFailed || mm.IsDialoguePlaying || mm.HasActiveTimer))
            {
                NotifyToast(LocalizationService.Tr("job.blocked_by_mission", "Önce aktif süreli hikaye görevini tamamla."));
                return false;
            }

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null) return false;

            // If clicking same active job board, cancel it cleanly
            if (_activeJob == type)
            {
                CancelActiveJob(silent: false);
                return false;
            }

            if (type == JobType.StreetCleanup && mode.IsDriving)
            {
                NotifyToast(LocalizationService.Tr("job.cleanup.need_onfoot", "Sokak temizliği yaya yapılır — önce araçtan in."));
                return false;
            }

            if (HasActiveJob)
            {
                CancelActiveJob(silent: true);
            }

            switch (type)
            {
                case JobType.PizzaDelivery:
                    return BeginPizzaDeliveryJob();
                case JobType.Taxi:
                    return BeginTaxiJob();
                case JobType.StreetCleanup:
                    return BeginStreetCleanupJob();
                case JobType.HousePainting:
                    return BeginHousePaintingJob();
                default:
                    return false;
            }
        }

        public void CancelActiveJob(bool silent = false)
        {
            if (!HasActiveJob) return;

            CleanupJobVisuals();
            _activeJob = JobType.None;
            _currentJobHudText = string.Empty;
            _hasJobTimer = false;
            _jobRemainingTime = 0f;
            _hasJobWaypoint = false;
            _pizzaTargetHouse = null;
            _targetPaintHouse = null;
            _targetPaintSwatchIndex = -1;
            _taxiPassengerAboard = false;

            if (!silent)
            {
                NotifyToast(LocalizationService.Tr("job.cancelled", "İş iptal edildi."));
            }

            OnJobStateChanged?.Invoke();
        }

        private void CleanupJobVisuals()
        {
            if (jobBeacon != null)
            {
                jobBeacon.SetVisible(false);
            }
            if (taxiPassengerObject != null)
            {
                taxiPassengerObject.SetActive(false);
            }
            for (int i = 0; i < _spawnedLitter.Count; i++)
            {
                if (_spawnedLitter[i] != null)
                {
                    Destroy(_spawnedLitter[i].gameObject);
                }
            }
            _spawnedLitter.Clear();
            _awardedLitter.Clear();
        }

        // =====================================================================
        // A) PIZZA DELIVERY
        // =====================================================================

        private bool BeginPizzaDeliveryJob()
        {
            if (_allHouses == null || _allHouses.Length == 0)
            {
                _allHouses = FindObjectsByType<PaintableHouse>(FindObjectsInactive.Include);
            }
            if (_allHouses == null || _allHouses.Length == 0) return false;

            _activeJob = JobType.PizzaDelivery;
            _pizzaChainIndex = 1;
            PickNextPizzaHouse(null);
            PlayBlip();
            NotifyToast(LocalizationService.Tr("job.pizza.started", "Pizza Teslimatı başladı! Herhangi bir araçla adrese götür."));
            OnJobStateChanged?.Invoke();
            return true;
        }

        private void PickNextPizzaHouse(PaintableHouse previous)
        {
            PaintableHouse chosen = _allHouses[UnityEngine.Random.Range(0, _allHouses.Length)];
            if (_allHouses.Length > 1 && chosen == previous)
            {
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    PaintableHouse candidate = _allHouses[UnityEngine.Random.Range(0, _allHouses.Length)];
                    if (candidate != previous)
                    {
                        chosen = candidate;
                        break;
                    }
                }
            }

            _pizzaTargetHouse = chosen;
            _hasJobTimer = true;
            _jobRemainingTime = 90f;
            SetJobWaypoint(_pizzaTargetHouse.FrontDoorWorldPosition, 5.5f);
            RefreshJobHudText();
        }

        private void UpdatePizzaJob()
        {
            if (_pizzaTargetHouse == null)
            {
                CancelActiveJob(silent: true);
                return;
            }

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null) return;

            RefreshJobHudText();

            if (Time.time - _lastPizzaRewardTime < 0.8f) return;

            // Accept delivery when stopped cleanly by vehicle near the street front OR walking right up to the front door
            bool delivered = false;
            if (mode.IsDriving && mode.CurrentVehicle != null)
            {
                Vector3 diff = mode.CurrentVehicle.transform.position - _pizzaTargetHouse.FrontDoorWorldPosition;
                diff.y = 0f;
                delivered = diff.magnitude <= 6.5f && mode.CurrentVehicle.SpeedKmh < 5.5f;
            }
            else if (mode.CurrentMode == ControlMode.OnFoot)
            {
                Vector3 diff = mode.ActiveWorldPosition - _pizzaTargetHouse.FrontDoorWorldPosition;
                diff.y = 0f;
                delivered = diff.magnitude <= 3.6f;
            }

            if (delivered)
            {
                _lastPizzaRewardTime = Time.time;
                int timeBonus = Mathf.Clamp(Mathf.RoundToInt(_jobRemainingTime * 0.35f), 0, 30);
                int reward = 40 + timeBonus;

                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.Data.deliveriesDone++;
                    SaveManager.Instance.AddMoney(reward);
                }

                PlayBlip();
                GameFeelAudio.PlayCoinChime();

                if (_pizzaChainIndex < MaxPizzaChain)
                {
                    string deliveredFmt = LocalizationService.Tr("job.pizza.delivered", "Pizza teslim edildi! +{0} ₺ ({1}/{2})");
                    NotifyToast(string.Format(deliveredFmt, reward, _pizzaChainIndex, MaxPizzaChain));
                    _pizzaChainIndex++;
                    PickNextPizzaHouse(_pizzaTargetHouse);
                    OnJobStateChanged?.Invoke();
                }
                else
                {
                    string allFmt = LocalizationService.Tr("job.pizza.all_done", "5/5 Pizza vardiyası tamamlandı! +{0} ₺");
                    NotifyToast(string.Format(allFmt, reward));
                    GameFeelAudio.PlayMilestoneFanfare();
                    FinishActiveJobCleanly();
                }
            }
        }

        // =====================================================================
        // B) TAXI
        // =====================================================================

        private bool BeginTaxiJob()
        {
            _activeJob = JobType.Taxi;
            _taxiPassengerAboard = false;
            _taxiHardCollisions = 0;
            _hasJobTimer = false;

            Vector3 playerPos = PlayerModeController.Instance != null
                ? PlayerModeController.Instance.ActiveWorldPosition
                : Vector3.zero;

            Vector3 pickup = TaxiPickupSpots[UnityEngine.Random.Range(0, TaxiPickupSpots.Length)];
            for (int i = 0; i < TaxiPickupSpots.Length; i++)
            {
                Vector3 cand = TaxiPickupSpots[(i + UnityEngine.Random.Range(0, TaxiPickupSpots.Length)) % TaxiPickupSpots.Length];
                if ((cand - playerPos).sqrMagnitude > 900f)
                {
                    pickup = cand;
                    break;
                }
            }

            _taxiPickupPos = pickup;
            EnsureTaxiPassengerInstance();
            if (taxiPassengerObject != null)
            {
                taxiPassengerObject.transform.SetPositionAndRotation(pickup, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                taxiPassengerObject.SetActive(true);
            }
            if (taxiPassengerHumanoid != null)
            {
                taxiPassengerHumanoid.PlayWave();
            }
            _taxiWaveTimer = 2.0f;

            SetJobWaypoint(pickup, 5.5f);
            RefreshJobHudText();
            PlayBlip();
            NotifyToast(LocalizationService.Tr("job.taxi.started", "Taksi çağrısı alındı! Bekleyen yolcuya sür."));
            OnJobStateChanged?.Invoke();
            return true;
        }

        private void EnsureTaxiPassengerInstance()
        {
            if (taxiPassengerObject != null) return;

            taxiPassengerObject = new GameObject("TaxiPassenger_Runtime");
            taxiPassengerObject.transform.SetParent(transform, false);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            Material mat = new Material(lit);
            taxiPassengerHumanoid = ProceduralHumanoid.Build(taxiPassengerObject.transform, mat, mat, mat, mat, mat, 1.75f);
        }

        private void HandleVehicleCollisionImpulse(SedanVehicleController veh, float impulse)
        {
            if (_activeJob != JobType.Taxi || !_taxiPassengerAboard) return;
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || !mode.IsDriving || mode.CurrentVehicle != veh) return;

            if (impulse > 6000f)
            {
                _taxiHardCollisions++;
                NotifyToast(LocalizationService.Tr("job.taxi.collision", "Sert çarpışma! Yolcu sarsıldı (-%10 ücret)"));
                RefreshJobHudText();
                OnJobStateChanged?.Invoke();
            }
        }

        private void UpdateTaxiJob(float dt)
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null) return;

            if (!_taxiPassengerAboard)
            {
                if (taxiPassengerObject != null && taxiPassengerObject.activeSelf)
                {
                    Vector3 toCar = mode.ActiveWorldPosition - taxiPassengerObject.transform.position;
                    toCar.y = 0f;
                    if (toCar.sqrMagnitude > 0.2f)
                    {
                        taxiPassengerObject.transform.rotation = Quaternion.Slerp(
                            taxiPassengerObject.transform.rotation,
                            Quaternion.LookRotation(toCar.normalized, Vector3.up),
                            dt * 5f);
                    }

                    _taxiWaveTimer -= dt;
                    if (_taxiWaveTimer <= 0f)
                    {
                        _taxiWaveTimer = 2.6f;
                        if (taxiPassengerHumanoid != null)
                        {
                            taxiPassengerHumanoid.PlayWave();
                        }
                    }
                }

                RefreshJobHudText();

                if (mode.IsDriving && mode.CurrentVehicle != null)
                {
                    SedanVehicleController car = mode.CurrentVehicle;
                    VehicleDamageSystem dmg = car.GetComponent<VehicleDamageSystem>();
                    bool carUnusable = (dmg != null && dmg.IsStalled) || car.Fuel <= 0.01f || Vector3.Dot(car.transform.up, Vector3.up) < 0.5f;
                    if (carUnusable && Time.time - _lastTaxiWarningTime > 6f)
                    {
                        _lastTaxiWarningTime = Time.time;
                        if (dmg != null && dmg.IsStalled)
                            NotifyToast("Araç ağır hasarlı — No:73 Oto Tamir'de veya ATM'den onar ya da başka bir araca bin!");
                        else if (car.Fuel <= 0.01f)
                            NotifyToast("Yakıt bitti — Seyyid Kutup benzin pompasından yakıt al ya da başka bir araca bin!");
                        else
                            NotifyToast("Araç devrilmiş — inip başka bir araca geçebilirsin.");
                    }

                    Vector3 diff = car.transform.position - _taxiPickupPos;
                    diff.y = 0f;
                    if (!carUnusable && diff.magnitude <= 6.5f && car.SpeedKmh < 5.5f)
                    {
                        _taxiPassengerAboard = true;
                        _taxiHardCollisions = 0;
                        if (taxiPassengerObject != null)
                        {
                            taxiPassengerObject.SetActive(false);
                        }

                        // Pick destination sufficiently far from pickup (>= 35 m along Selman Sokağı)
                        TaxiDestination bestDest = TaxiDestinations[0];
                        int startIdx = UnityEngine.Random.Range(0, TaxiDestinations.Length);
                        for (int i = 0; i < TaxiDestinations.Length; i++)
                        {
                            TaxiDestination cand = TaxiDestinations[(startIdx + i) % TaxiDestinations.Length];
                            if ((cand.WorldPos - _taxiPickupPos).sqrMagnitude > 1225f)
                            {
                                bestDest = cand;
                                break;
                            }
                        }

                        _taxiCurrentDest = bestDest;
                        SetJobWaypoint(_taxiCurrentDest.WorldPos, 6.0f);
                        RefreshJobHudText();
                        PlayBlip();
                        string destName = LocalizationService.Tr(_taxiCurrentDest.NameKey, _taxiCurrentDest.FallbackTr);
                        string aboardFmt = LocalizationService.Tr("job.taxi.picked_up", "Yolcu bindi! Hedef: {0}");
                        NotifyToast(string.Format(aboardFmt, destName));
                        OnJobStateChanged?.Invoke();
                    }
                }
            }
            else
            {
                RefreshJobHudText();

                if (mode.IsDriving && mode.CurrentVehicle != null)
                {
                    Vector3 diff = mode.CurrentVehicle.transform.position - _taxiCurrentDest.WorldPos;
                    diff.y = 0f;
                    if (diff.magnitude <= 6.5f && mode.CurrentVehicle.SpeedKmh < 5.0f)
                    {
                        float tripDist = Vector3.Distance(_taxiPickupPos, _taxiCurrentDest.WorldPos) * 1.25f;
                        int baseFare = Mathf.Max(12, Mathf.RoundToInt(tripDist / 10f));
                        int finalFare;
                        if (_taxiHardCollisions == 0)
                        {
                            finalFare = baseFare + 12; // Clean driving bonus
                        }
                        else
                        {
                            float mult = Mathf.Max(0.2f, 1f - 0.10f * _taxiHardCollisions);
                            finalFare = Mathf.Max(5, Mathf.RoundToInt(baseFare * mult));
                        }

                        if (SaveManager.Instance != null)
                        {
                            SaveManager.Instance.Data.taxiFaresDone++;
                            SaveManager.Instance.AddMoney(finalFare);
                        }

                        PlayBlip();
                        StartCoroutine(ShowDropoffPassengerBriefRoutine(mode.CurrentVehicle.GetSafeExitPosition()));

                        string fareFmt = _taxiHardCollisions == 0
                            ? LocalizationService.Tr("job.taxi.done_bonus", "Yolcu indirildi! Temiz sürüş bonusu ile +{0} ₺")
                            : LocalizationService.Tr("job.taxi.done", "Yolcu indirildi! +{0} ₺");
                        NotifyToast(string.Format(fareFmt, finalFare));
                        FinishActiveJobCleanly(keepPassengerBrief: true);
                    }
                }
            }
        }

        private IEnumerator ShowDropoffPassengerBriefRoutine(Vector3 sidewalkPos)
        {
            if (taxiPassengerObject == null) yield break;
            taxiPassengerObject.transform.position = sidewalkPos;
            taxiPassengerObject.SetActive(true);
            if (taxiPassengerHumanoid != null)
            {
                taxiPassengerHumanoid.PlayWave();
            }
            yield return new WaitForSeconds(2.5f);
            if (_activeJob != JobType.Taxi && taxiPassengerObject != null)
            {
                taxiPassengerObject.SetActive(false);
            }
        }

        // =====================================================================
        // C) STREET CLEANUP
        // =====================================================================

        private bool BeginStreetCleanupJob()
        {
            _activeJob = JobType.StreetCleanup;
            _litterCollected = 0;
            _litterTotal = LitterSpawnPoints.Length;
            _hasJobTimer = true;
            _jobRemainingTime = 180f; // 3 minutes

            for (int i = 0; i < LitterSpawnPoints.Length; i++)
            {
                Vector3 pos = LitterSpawnPoints[i] + new Vector3(UnityEngine.Random.Range(-0.8f, 0.8f), 0f, UnityEngine.Random.Range(-0.8f, 0.8f));
                LitterPickup item = CreateLitterItem(pos, i);
                _spawnedLitter.Add(item);
            }

            UpdateNearestLitterWaypoint();
            RefreshJobHudText();
            PlayBlip();
            NotifyToast(LocalizationService.Tr("job.cleanup.started", "Sokak Temizliği başladı! Meydan ve caddedeki 10 çöpü topla."));
            OnJobStateChanged?.Invoke();
            return true;
        }

        private LitterPickup CreateLitterItem(Vector3 worldPos, int index)
        {
            GameObject root = new GameObject($"LitterItem_{index + 1:00}");
            root.transform.SetParent(transform, false);
            root.transform.position = worldPos;
            root.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            bool isCan = (index % 2 == 1);
            GameObject meshObj = GameObject.CreatePrimitive(isCan ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            meshObj.name = "Visual";
            meshObj.transform.SetParent(root.transform, false);
            meshObj.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            meshObj.transform.localRotation = Quaternion.Euler(isCan ? 72f : 14f, 25f, isCan ? 18f : -12f);
            meshObj.transform.localScale = isCan
                ? new Vector3(0.20f, 0.16f, 0.20f)
                : new Vector3(0.32f, 0.22f, 0.28f);
            Destroy(meshObj.GetComponent<Collider>());

            Renderer r = meshObj.GetComponent<Renderer>();
            if (r != null)
            {
                Material chosenMat = isCan ? litterCanMaterial : litterBoxMaterial;
                if (chosenMat != null)
                {
                    r.sharedMaterial = chosenMat;
                }
            }

            SphereCollider sc = root.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 1.15f;

            LitterPickup pickup = root.AddComponent<LitterPickup>();
            pickup.Initialize(this);
            return pickup;
        }

        public void NotifyLitterCollected(LitterPickup pickup)
        {
            if (_activeJob != JobType.StreetCleanup || pickup == null) return;
            if (!_awardedLitter.Add(pickup)) return; // Strict single-reward guard

            _litterCollected++;
            PlayBlip();
            GameFeelAudio.PlayCoinChime();

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Data.litterCollectedCount++;
                SaveManager.Instance.AddMoney(5);
            }

            if (_litterCollected >= _litterTotal)
            {
                NotifyToast(LocalizationService.Tr("job.cleanup.done", "Sokaklar tertemiz! 10/10 çöp toplandı."));
                GameFeelAudio.PlayMilestoneFanfare();
                FinishActiveJobCleanly();
            }
            else
            {
                UpdateNearestLitterWaypoint();
                RefreshJobHudText();
                OnJobStateChanged?.Invoke();
            }
        }

        private void UpdateNearestLitterWaypoint()
        {
            Vector3 playerPos = PlayerModeController.Instance != null
                ? PlayerModeController.Instance.ActiveWorldPosition
                : Vector3.zero;

            LitterPickup nearest = null;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < _spawnedLitter.Count; i++)
            {
                LitterPickup item = _spawnedLitter[i];
                if (item == null || item.IsCollected) continue;
                float dSq = (item.transform.position - playerPos).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    nearest = item;
                }
            }

            if (nearest != null)
            {
                SetJobWaypoint(nearest.transform.position, 2.2f);
            }
        }

        // =====================================================================
        // D) HOUSE PAINTING JOB
        // =====================================================================

        private bool BeginHousePaintingJob()
        {
            if (_allHouses == null || _allHouses.Length == 0)
            {
                _allHouses = FindObjectsByType<PaintableHouse>(FindObjectsInactive.Include);
            }
            if (_allHouses == null || _allHouses.Length == 0) return false;

            _activeJob = JobType.HousePainting;
            _hasJobTimer = false;
            _targetPaintHouse = _allHouses[UnityEngine.Random.Range(0, _allHouses.Length)];

            Color[] swatches = PaintPalettePanel.DefaultSwatchColors;
            int chosenSwatch = UnityEngine.Random.Range(0, swatches.Length);
            for (int i = 0; i < swatches.Length; i++)
            {
                int idx = (chosenSwatch + i) % swatches.Length;
                if (!ColorsApproxEqual(_targetPaintHouse.CurrentColor, swatches[idx]))
                {
                    chosenSwatch = idx;
                    break;
                }
            }

            _targetPaintSwatchIndex = chosenSwatch;
            SetJobWaypoint(_targetPaintHouse.FrontDoorWorldPosition, 3.2f);
            RefreshJobHudText();
            PlayBlip();

            string colorName = GetLocalizedColorName(_targetPaintSwatchIndex);
            string startFmt = LocalizationService.Tr("job.paint.started", "Ev Boyama İşi: {0} evini {1} rengine boya.");
            NotifyToast(string.Format(startFmt, _targetPaintHouse.DisplayName, colorName));
            OnJobStateChanged?.Invoke();
            return true;
        }

        private void HandleAnyHousePainted(PaintableHouse house, Color appliedColor)
        {
            if (_activeJob != JobType.HousePainting || house == null || house != _targetPaintHouse)
            {
                return;
            }

            Color[] swatches = PaintPalettePanel.DefaultSwatchColors;
            if (_targetPaintSwatchIndex >= 0 && _targetPaintSwatchIndex < swatches.Length)
            {
                Color expected = swatches[_targetPaintSwatchIndex];
                if (ColorsApproxEqual(appliedColor, expected))
                {
                    if (SaveManager.Instance != null)
                    {
                        SaveManager.Instance.AddMoney(60);
                    }
                    PlayBlip();
                    NotifyToast(LocalizationService.Tr("job.paint.done", "Harika işçilik! Ev istenen renge boyandı (+60 ₺)"));
                    FinishActiveJobCleanly();
                }
                else
                {
                    string colorName = GetLocalizedColorName(_targetPaintSwatchIndex);
                    string wrongFmt = LocalizationService.Tr("job.paint.wrong_color", "Ev sahibi {0} rengini istemişti! Tekrar boyayabilirsin.");
                    NotifyToast(string.Format(wrongFmt, colorName));
                }
            }
        }

        private static bool ColorsApproxEqual(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.09f;
        }

        // =====================================================================
        // UPDATE & HELPERS
        // =====================================================================

        private void Update()
        {
            if (!HasActiveJob) return;

            float dt = Time.deltaTime;
            if (_hasJobTimer)
            {
                _jobRemainingTime -= dt;
                if (_jobRemainingTime <= 0f)
                {
                    _jobRemainingTime = 0f;
                    string failMsg = _activeJob == JobType.PizzaDelivery
                        ? LocalizationService.Tr("job.pizza.failed", "Süre doldu! Pizza teslimatı başarısız.")
                        : LocalizationService.Tr("job.cleanup.timeout", "Süre doldu! Sokak temizliği sona erdi.");
                    NotifyToast(failMsg);
                    FinishActiveJobCleanly();
                    return;
                }
            }

            switch (_activeJob)
            {
                case JobType.PizzaDelivery:
                    UpdatePizzaJob();
                    break;
                case JobType.Taxi:
                    UpdateTaxiJob(dt);
                    break;
                case JobType.StreetCleanup:
                    UpdateNearestLitterWaypoint();
                    break;
            }
        }

        private void SetJobWaypoint(Vector3 worldPos, float radius)
        {
            _hasJobWaypoint = true;
            _jobWaypointPos = worldPos;
            if (jobBeacon != null)
            {
                jobBeacon.SetTarget(worldPos, radius);
            }
        }

        private void FinishActiveJobCleanly(bool keepPassengerBrief = false)
        {
            if (jobBeacon != null)
            {
                jobBeacon.SetVisible(false);
            }
            if (!keepPassengerBrief && taxiPassengerObject != null)
            {
                taxiPassengerObject.SetActive(false);
            }
            for (int i = 0; i < _spawnedLitter.Count; i++)
            {
                if (_spawnedLitter[i] != null)
                {
                    Destroy(_spawnedLitter[i].gameObject);
                }
            }
            _spawnedLitter.Clear();
            _awardedLitter.Clear();

            _activeJob = JobType.None;
            _currentJobHudText = string.Empty;
            _hasJobTimer = false;
            _jobRemainingTime = 0f;
            _hasJobWaypoint = false;
            _pizzaTargetHouse = null;
            _targetPaintHouse = null;
            _targetPaintSwatchIndex = -1;
            _taxiPassengerAboard = false;

            OnJobStateChanged?.Invoke();
        }

        private void RefreshJobHudText()
        {
            PlayerModeController mode = PlayerModeController.Instance;
            bool inCar = mode != null && mode.IsDriving;

            switch (_activeJob)
            {
                case JobType.PizzaDelivery:
                {
                    string houseName = _pizzaTargetHouse != null ? _pizzaTargetHouse.DisplayName : "";
                    string fmt = inCar
                        ? LocalizationService.Tr("job.hud.pizza_drive", "İŞ: Pizza ({0}/{1}) — {2} kapısında dur")
                        : LocalizationService.Tr("job.hud.pizza_enter_car", "İŞ: Pizza ({0}/{1}) — Bir arabaya bin ({2})");
                    _currentJobHudText = string.Format(fmt, _pizzaChainIndex, MaxPizzaChain, houseName);
                    break;
                }
                case JobType.Taxi:
                {
                    if (!_taxiPassengerAboard)
                    {
                        _currentJobHudText = inCar
                            ? LocalizationService.Tr("job.hud.taxi_pickup", "İŞ: Taksi — El sallayan yolcunun yanında dur")
                            : LocalizationService.Tr("job.hud.taxi_enter_car", "İŞ: Taksi — Bir arabaya bin ve yolcuyu al");
                    }
                    else
                    {
                        string dest = LocalizationService.Tr(_taxiCurrentDest.NameKey, _taxiCurrentDest.FallbackTr);
                        string fmt = LocalizationService.Tr("job.hud.taxi_dropoff", "İŞ: Taksi — Yolcuyu {0} noktasına bırak");
                        _currentJobHudText = string.Format(fmt, dest);
                    }
                    break;
                }
                case JobType.StreetCleanup:
                {
                    string fmt = LocalizationService.Tr("job.hud.cleanup", "İŞ: Sokak Temizliği — Çöpleri topla ({0}/{1})");
                    _currentJobHudText = string.Format(fmt, _litterCollected, _litterTotal);
                    break;
                }
                case JobType.HousePainting:
                {
                    string houseName = _targetPaintHouse != null ? _targetPaintHouse.DisplayName : "";
                    string colorName = GetLocalizedColorName(_targetPaintSwatchIndex);
                    string fmt = LocalizationService.Tr("job.hud.paint", "İŞ: Ev Boyama — {0} evini {1} rengine boya");
                    _currentJobHudText = string.Format(fmt, houseName, colorName);
                    break;
                }
                default:
                    _currentJobHudText = string.Empty;
                    break;
            }
        }

        private static void NotifyToast(string msg)
        {
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage(msg);
            }
        }
    }
}
