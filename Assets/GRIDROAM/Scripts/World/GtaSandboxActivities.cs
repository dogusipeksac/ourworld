using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// GTA 6-style Open-World Sandbox Activities across all 7 Sultanbeyli districts:
    /// 1. 10 Iconic GTA Stunt Jumps (yellow/black striped launch ramps with cinematic slow-motion,
    ///    air-distance & peak-height measurement, and cash/reputation rewards!).
    /// 2. 6 Roadside Speed Trap Cameras (Hız Radarı) with bright camera flashes, top-speed cash payouts,
    ///    and automatic ★1 Police Pursuit if you blow past above 118 km/h!
    /// 3. Dynamic Street Crime Opportunity: Fleeing Purse Snatcher (Kaçan Yankesici) with live blue
    ///    minimap radar blip — chase down & knock out the thief to recover the bag for +220 ₺ & +35 Rep!
    /// </summary>
    public sealed class GtaSandboxActivities : MonoBehaviour
    {
        public static GtaSandboxActivities Instance { get; private set; }

        private struct StuntRampDef
        {
            public string Id;
            public string NameTr;
            public string NameEn;
            public Vector3 Pos;
            public float Yaw;
        }

        private static readonly StuntRampDef[] StuntRamps =
        {
            new StuntRampDef { Id = "sj_fatih",        NameTr = "Fatih Bulvarı Atlayışı",       NameEn = "Fatih Blvd Leap",         Pos = new Vector3( -32f, 0.04f,  136f), Yaw =  90f },
            new StuntRampDef { Id = "sj_petrolyolu",   NameTr = "Petrol Yolu Rampa Atlayışı",   NameEn = "Petrol Yolu Jump",        Pos = new Vector3( -34f, 0.04f,  -78f), Yaw =  90f },
            new StuntRampDef { Id = "sj_ciftlik",      NameTr = "Aydos Çiftlik Vadisi",         NameEn = "Aydos Farm Valley Jump",  Pos = new Vector3( 138f, 0.04f,   82f), Yaw =   0f },
            new StuntRampDef { Id = "sj_golet",        NameTr = "Sultanbeyli Göleti Yolu",      NameEn = "Sultanbeyli Lake Launch", Pos = new Vector3(-142f, 0.04f,   74f), Yaw =   0f },
            new StuntRampDef { Id = "sj_abdurrahman",  NameTr = "Abdurrahmangazi Bulvarı",      NameEn = "Abdurrahmangazi Jump",    Pos = new Vector3( 116f, 0.04f,  186f), Yaw =  90f },
            new StuntRampDef { Id = "sj_mehmetakif",   NameTr = "Mehmet Akif M5 Rampası",       NameEn = "Mehmet Akif Metro Leap",  Pos = new Vector3(  -6f, 0.04f,  218f), Yaw =   0f },
            new StuntRampDef { Id = "sj_hasanpasa",    NameTr = "Hasanpaşa Bulvar Atlayışı",    NameEn = "Hasanpaşa Blvd Jump",     Pos = new Vector3(-106f, 0.04f,  186f), Yaw = -90f },
            new StuntRampDef { Id = "sj_battalgazi",   NameTr = "Bosna Bulvarı Stunt",          NameEn = "Bosna Blvd Stunt",        Pos = new Vector3(-114f, 0.04f, -186f), Yaw = -90f },
            new StuntRampDef { Id = "sj_mimarsinan",   NameTr = "Mimar Sinan Cadde Atlayışı",   NameEn = "Mimar Sinan Leap",        Pos = new Vector3( -52f, 0.04f, -214f), Yaw = 180f },
            new StuntRampDef { Id = "sj_aydoskale",    NameTr = "Aydos Kalesi Zirve Atlayışı",  NameEn = "Aydos Castle Ridge Jump", Pos = new Vector3( 214f, 0.04f,  -82f), Yaw = 180f }
        };

        private struct SpeedCameraDef
        {
            public string NameTr;
            public string NameEn;
            public Vector3 Pos;
            public float Yaw;
        }

        private static readonly SpeedCameraDef[] SpeedCameras =
        {
            new SpeedCameraDef { NameTr = "Fatih Bulvarı Radarı",      NameEn = "Fatih Blvd Radar",      Pos = new Vector3(  24f, 0.05f,  131.5f), Yaw = 180f },
            new SpeedCameraDef { NameTr = "Petrol Yolu Radarı",        NameEn = "Petrol Yolu Radar",     Pos = new Vector3(  16f, 0.05f,  -72.5f), Yaw =   0f },
            new SpeedCameraDef { NameTr = "Abdurrahmangazi Radarı",    NameEn = "Abdurrahmangazi Radar", Pos = new Vector3( 158f, 0.05f,  168.5f), Yaw =   0f },
            new SpeedCameraDef { NameTr = "Erdoğan Caddesi Radarı",    NameEn = "Erdoğan Ave Radar",     Pos = new Vector3(  18f, 0.05f,  270.5f), Yaw =   0f },
            new SpeedCameraDef { NameTr = "Bosna Bulvarı Radarı",      NameEn = "Bosna Blvd Radar",      Pos = new Vector3(-164f, 0.05f, -164.5f), Yaw = 180f },
            new SpeedCameraDef { NameTr = "Demokrasi Caddesi Radarı",  NameEn = "Demokrasi Ave Radar",   Pos = new Vector3( 232f, 0.05f, -142.5f), Yaw = 180f }
        };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private readonly HashSet<string> _completedJumps = new HashSet<string>();
        private readonly List<Material> _ownedMats = new List<Material>(12);
        private readonly Light[] _cameraFlashLights = new Light[6];
        private readonly float[] _cameraCooldowns = new float[6];
        private readonly float[] _cameraFlashTimers = new float[6];
        private ThirdPersonCameraRig _cachedCamRig;

        private Material _rampYellowMat;
        private Material _rampDarkMat;
        private Material _rampGlowMat;
        private Material _radarMetalMat;
        private Material _radarLensMat;
        private Material _thiefJacketMat;
        private Material _stolenBagMat;

        // Active Stunt Jump flight tracking
        private bool _stuntActive;
        private int _stuntRampIdx = -1;
        private Vector3 _stuntLaunchPos;
        private float _stuntPeakY;
        private float _stuntAirTime;
        private float _stuntSlowMoTimer;
        private float _stuntCooldown;

        // Dynamic Street Crime: Fleeing Purse Snatcher (Kaçan Yankesici!)
        private float _crimeSpawnTimer = 42f;
        private GameObject _thiefRoot;
        private Rigidbody _thiefRb;
        private ProceduralHumanoid _thiefHum;
        private float _thiefHealth;
        private float _thiefLifetime;
        private float _thiefHitCooldown;
        private Vector3 _thiefRunDir;
        private bool _thiefActive;

        public bool HasActiveCrimeEvent => _thiefActive && _thiefRoot != null;
        public Vector3 ActiveCrimePosition => HasActiveCrimeEvent ? _thiefRoot.transform.position : Vector3.zero;
        public int CompletedStuntCount => _completedJumps.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;

            GameObject go = new GameObject("OurWorld_GtaSandboxActivities");
            go.AddComponent<GtaSandboxActivities>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private IEnumerator Start()
        {
            yield return null;
            SyncCompletedJumpsFromSave();
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            }
            BuildMaterials();
            BuildAllStuntRamps();
            BuildAllSpeedCameras();
        }

        private void HandleSaveLoaded(SaveData _) => SyncCompletedJumpsFromSave();

        private void SyncCompletedJumpsFromSave()
        {
            _completedJumps.Clear();
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d?.completedStuntJumpIds == null) return;
            for (int i = 0; i < d.completedStuntJumpIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(d.completedStuntJumpIds[i]))
                {
                    _completedJumps.Add(d.completedStuntJumpIds[i]);
                }
            }
        }

        private void OnDestroy()
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            }
            if (_stuntSlowMoTimer > 0f && Time.timeScale < 0.99f)
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
            }
            if (_thiefRoot != null) Destroy(_thiefRoot);
            for (int i = 0; i < _ownedMats.Count; i++)
            {
                if (_ownedMats[i] != null) Destroy(_ownedMats[i]);
            }
            _ownedMats.Clear();
            if (Instance == this) Instance = null;
        }

        private Material MakeMat(Shader sh, Color col, float smooth = 0.28f, Color? emission = null)
        {
            Material m = new Material(sh);
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, col);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, col);
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, smooth);
            if (emission.HasValue && m.HasProperty(EmissionColorId))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionColorId, emission.Value);
            }
            _ownedMats.Add(m);
            return m;
        }

        private void BuildMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (lit == null) return;
            _rampYellowMat = MakeMat(lit, new Color(0.96f, 0.74f, 0.10f), 0.34f);
            _rampDarkMat = MakeMat(lit, new Color(0.14f, 0.16f, 0.19f), 0.28f);
            _rampGlowMat = MakeMat(lit, new Color(1.0f, 0.62f, 0.12f), 0.65f, new Color(1.0f, 0.56f, 0.08f) * 2.2f);
            _radarMetalMat = MakeMat(lit, new Color(0.32f, 0.36f, 0.40f), 0.52f);
            _radarLensMat = MakeMat(lit, new Color(0.08f, 0.12f, 0.16f), 0.90f);
            _thiefJacketMat = MakeMat(lit, new Color(0.12f, 0.12f, 0.15f), 0.22f);
            _stolenBagMat = MakeMat(lit, new Color(0.94f, 0.24f, 0.44f), 0.38f, new Color(0.94f, 0.24f, 0.44f) * 0.45f);
        }

        // =====================================================================
        // 1. 10 ICONIC SULTANBEYLİ GTA STUNT RAMPS
        // =====================================================================
        private void BuildAllStuntRamps()
        {
            Transform rampRoot = new GameObject("Sultanbeyli_GTA_StuntRamps").transform;
            rampRoot.SetParent(transform, false);

            for (int i = 0; i < StuntRamps.Length; i++)
            {
                StuntRampDef def = StuntRamps[i];
                GameObject r = new GameObject($"StuntRamp_{i:00}_{def.Id}");
                r.transform.SetParent(rampRoot, false);
                r.transform.position = def.Pos;
                r.transform.rotation = Quaternion.Euler(0f, def.Yaw, 0f);

                // Smooth drivable 17-degree steel-and-concrete launch deck (wide 4.4m for easy cars/motorcycles!)
                GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
                deck.name = "RampDeck";
                deck.transform.SetParent(r.transform, false);
                deck.transform.localPosition = new Vector3(0f, 0.72f, 0f);
                deck.transform.localRotation = Quaternion.Euler(-16.5f, 0f, 0f);
                deck.transform.localScale = new Vector3(4.4f, 0.32f, 7.6f);
                SetupMat(deck, _rampDarkMat, castShadows: true);

                // High-visibility GTA yellow-black chevron rails + glowing launch edge!
                MakeDecor(r.transform, "RailL", new Vector3(-2.15f, 0.88f, 0f), new Vector3(0.24f, 0.36f, 7.6f), Quaternion.Euler(-16.5f, 0f, 0f), _rampYellowMat);
                MakeDecor(r.transform, "RailR", new Vector3( 2.15f, 0.88f, 0f), new Vector3(0.24f, 0.36f, 7.6f), Quaternion.Euler(-16.5f, 0f, 0f), _rampYellowMat);
                MakeDecor(r.transform, "LaunchLipGlow", new Vector3(0f, 1.84f, 3.60f), new Vector3(4.35f, 0.14f, 0.35f), Quaternion.identity, _rampGlowMat);
                // Solid rear support strut
                GameObject support = GameObject.CreatePrimitive(PrimitiveType.Cube);
                support.name = "RampSupport";
                support.transform.SetParent(r.transform, false);
                support.transform.localPosition = new Vector3(0f, 0.86f, 3.35f);
                support.transform.localScale = new Vector3(4.2f, 1.72f, 0.45f);
                SetupMat(support, _rampDarkMat, castShadows: false);
            }
        }

        // =====================================================================
        // 2. 6 SULTANBEYLİ SPEED TRAP CAMERAS (HIZ RADARI)
        // =====================================================================
        private void BuildAllSpeedCameras()
        {
            Transform camRoot = new GameObject("Sultanbeyli_SpeedCameras").transform;
            camRoot.SetParent(transform, false);

            for (int i = 0; i < SpeedCameras.Length; i++)
            {
                SpeedCameraDef def = SpeedCameras[i];
                GameObject c = new GameObject($"SpeedCamera_{i:00}");
                c.transform.SetParent(camRoot, false);
                c.transform.position = def.Pos;
                c.transform.rotation = Quaternion.Euler(0f, def.Yaw, 0f);

                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "RadarPole";
                pole.transform.SetParent(c.transform, false);
                pole.transform.localPosition = new Vector3(0f, 1.9f, 0f);
                pole.transform.localScale = new Vector3(0.18f, 1.9f, 0.18f);
                SetupMat(pole, _radarMetalMat, castShadows: false);

                MakeDecor(c.transform, "RadarBox", new Vector3(0f, 3.85f, 0f), new Vector3(0.58f, 0.72f, 0.44f), Quaternion.identity, _rampYellowMat);
                MakeDecor(c.transform, "RadarLens", new Vector3(0f, 3.92f, 0.24f), new Vector3(0.24f, 0.24f, 0.06f), Quaternion.identity, _radarLensMat);

                GameObject flashGo = new GameObject("RadarFlash");
                flashGo.transform.SetParent(c.transform, false);
                flashGo.transform.localPosition = new Vector3(0f, 3.92f, 0.55f);
                Light fl = flashGo.AddComponent<Light>();
                fl.type = LightType.Point;
                fl.color = Color.white;
                fl.intensity = 65f;
                fl.range = 24f;
                fl.shadows = LightShadows.None;
                fl.enabled = false;
                _cameraFlashLights[i] = fl;
            }
        }

        // =====================================================================
        // UPDATE LOOP — STUNT JUMPS, SPEED CAMERAS & PURSE SNATCHER CRIME
        // =====================================================================
        private void Update()
        {
            float dt = Time.deltaTime;
            float udt = Time.unscaledDeltaTime;
            if (udt <= 0f) return;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsModalLocked || OurWorld.UI.MainMenuController.IsMenuActive)
            {
                // Safety: never leave Stunt Slow-Motion stuck if Phone / Menu / Dialogue opens mid-jump!
                if (_stuntSlowMoTimer > 0f)
                {
                    _stuntSlowMoTimer = 0f;
                    Time.timeScale = 1f;
                    Time.fixedDeltaTime = 0.02f;
                }
                return;
            }

            // Smooth GTA Stunt Slow-Motion restore
            if (_stuntSlowMoTimer > 0f)
            {
                _stuntSlowMoTimer -= udt;
                if (_stuntSlowMoTimer <= 0f)
                {
                    Time.timeScale = 1f;
                    Time.fixedDeltaTime = 0.02f;
                }
            }

            UpdateStuntJumps(mode, dt);
            UpdateSpeedCameras(mode, dt);
            UpdatePurseSnatcherCrime(mode, dt);
        }

        private void UpdateStuntJumps(PlayerModeController mode, float dt)
        {
            if (_stuntCooldown > 0f) _stuntCooldown -= dt;

            bool inCar = mode.IsDriving && mode.CurrentVehicle != null;
            bool onBike = mode.CurrentMode == ControlMode.Cycling && mode.CurrentBicycle != null;
            if (!inCar && !onBike)
            {
                if (_stuntActive) EndStuntFlight(mode, aborted: true);
                return;
            }

            Vector3 pos = mode.ActiveWorldPosition;
            float speedKmh = inCar ? mode.CurrentVehicle.SpeedKmh : mode.CurrentBicycle.SpeedKmh;
            bool airborne = inCar ? mode.CurrentVehicle.IsAllWheelsAirborne : (pos.y > 0.75f);

            if (!_stuntActive)
            {
                if (_stuntCooldown > 0f || speedKmh < 36f || pos.y < 0.85f) return;

                // Check proximity to any of the 10 Stunt Ramp launch lips!
                for (int i = 0; i < StuntRamps.Length; i++)
                {
                    Vector3 d = pos - StuntRamps[i].Pos;
                    d.y = 0f;
                    if (d.sqrMagnitude <= 7.8f * 7.8f)
                    {
                        _stuntActive = true;
                        _stuntRampIdx = i;
                        _stuntLaunchPos = pos;
                        _stuntPeakY = pos.y;
                        _stuntAirTime = 0f;

                        // Iconic GTA Stunt Slow-Motion & FOV Kick!
                        _stuntSlowMoTimer = 1.45f;
                        Time.timeScale = 0.44f;
                        Time.fixedDeltaTime = 0.02f * 0.44f;
                        if (_cachedCamRig == null) _cachedCamRig = FindAnyObjectByType<ThirdPersonCameraRig>();
                        _cachedCamRig?.AddFovImpulse(6.5f);
                        GameFeelAudio.PlayUiClick();
                        return;
                    }
                }
            }
            else
            {
                _stuntAirTime += Time.unscaledDeltaTime;
                if (pos.y > _stuntPeakY) _stuntPeakY = pos.y;

                // Landed cleanly after flight (or safety timeout)
                if ((!airborne && _stuntAirTime >= 0.45f) || _stuntAirTime >= 5.2f)
                {
                    EndStuntFlight(mode, aborted: false);
                }
            }
        }

        private void EndStuntFlight(PlayerModeController mode, bool aborted)
        {
            _stuntActive = false;
            _stuntCooldown = 3.5f;
            if (_stuntSlowMoTimer > 0f)
            {
                _stuntSlowMoTimer = 0f;
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
            }
            if (aborted || _stuntRampIdx < 0 || _stuntRampIdx >= StuntRamps.Length) return;

            Vector3 landPos = mode.ActiveWorldPosition;
            float distMeters = Vector3.Distance(new Vector3(_stuntLaunchPos.x, 0f, _stuntLaunchPos.z), new Vector3(landPos.x, 0f, landPos.z));
            float peakHeight = Mathf.Max(0f, _stuntPeakY - 0.1f);

            if (distMeters < 11f) return;

            StuntRampDef def = StuntRamps[_stuntRampIdx];
            bool firstTime = _completedJumps.Add(def.Id);
            if (firstTime && SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                if (SaveManager.Instance.Data.completedStuntJumpIds == null)
                    SaveManager.Instance.Data.completedStuntJumpIds = new List<string>();
                if (!SaveManager.Instance.Data.completedStuntJumpIds.Contains(def.Id))
                    SaveManager.Instance.Data.completedStuntJumpIds.Add(def.Id);
            }
            int cash = firstTime ? 250 : Mathf.Clamp(Mathf.RoundToInt(distMeters * 3.5f), 45, 160);
            SaveManager.Instance?.AddMoney(cash);
            NeighborhoodReputation.AddReputation(firstTime ? 30 : 10);
            GameFeelAudio.PlayMilestoneFanfare();
            GameFeelAudio.SpawnBurst(landPos + Vector3.up * 1.4f, new Color(1.0f, 0.82f, 0.22f), count: 16, spread: 0.8f);

            bool en = LocalizationService.IsEnglish;
            string name = en ? def.NameEn : def.NameTr;
            VisualEffectsManager.SpawnFloatingText(
                landPos + Vector3.up * 2.2f,
                en ? $"🏆 STUNT JUMP! {distMeters:0}m" : $"🏆 STUNT ATLAYIŞI! {distMeters:0}m",
                new Color(1.0f, 0.86f, 0.24f), scale: 1.25f, duration: 1.8f);

            mode.RaiseStatusMessage(en
                ? $"🏆 STUNT JUMP COMPLETED ({_completedJumps.Count}/{StuntRamps.Length}): {name} · {distMeters:0}m (Height {peakHeight:0.0}m) · +{cash} ₺!"
                : $"🏆 STUNT ATLAYIŞI ({_completedJumps.Count}/{StuntRamps.Length}): {name} · {distMeters:0}m (Yükseklik {peakHeight:0.0}m) · +{cash} ₺!");
        }

        private void UpdateSpeedCameras(PlayerModeController mode, float dt)
        {
            for (int i = 0; i < SpeedCameras.Length; i++)
            {
                if (_cameraCooldowns[i] > 0f) _cameraCooldowns[i] -= dt;
                if (_cameraFlashTimers[i] > 0f)
                {
                    _cameraFlashTimers[i] -= dt;
                    if (_cameraFlashTimers[i] <= 0f && _cameraFlashLights[i] != null)
                    {
                        _cameraFlashLights[i].enabled = false;
                    }
                }
            }

            if (!mode.IsDriving || mode.CurrentVehicle == null) return;
            float speedKmh = mode.CurrentVehicle.SpeedKmh;
            if (speedKmh < 78f) return;

            Vector3 carPos = mode.ActiveWorldPosition;
            bool en = LocalizationService.IsEnglish;

            for (int i = 0; i < SpeedCameras.Length; i++)
            {
                if (_cameraCooldowns[i] > 0f) continue;
                Vector3 d = carPos - SpeedCameras[i].Pos;
                d.y = 0f;
                if (d.sqrMagnitude <= 11.5f * 11.5f)
                {
                    _cameraCooldowns[i] = 18f;
                    _cameraFlashTimers[i] = 0.22f;
                    if (_cameraFlashLights[i] != null) _cameraFlashLights[i].enabled = true;

                    int bonus = Mathf.Clamp(Mathf.RoundToInt((speedKmh - 65f) * 2.5f), 60, 220);
                    if (SaveManager.Instance != null && SaveManager.Instance.Data != null && speedKmh > SaveManager.Instance.Data.bestSpeedTrapKmh)
                    {
                        SaveManager.Instance.Data.bestSpeedTrapKmh = speedKmh;
                    }
                    SaveManager.Instance?.AddMoney(bonus);
                    GameFeelAudio.PlayCoinChime();

                    string camName = en ? SpeedCameras[i].NameEn : SpeedCameras[i].NameTr;
                    VisualEffectsManager.SpawnFloatingText(
                        carPos + Vector3.up * 2.1f,
                        $"📸 {Mathf.RoundToInt(speedKmh)} km/h!",
                        new Color(0.98f, 0.96f, 0.82f), scale: 1.15f, duration: 1.3f);

                    if (speedKmh >= 118f && PoliceWantedSystem.PoliceSystemEnabled)
                    {
                        PoliceWantedSystem.Instance?.AddCrimeHeat(1.4f, carPos, "Speed Trap");
                        mode.RaiseStatusMessage(en
                            ? $"📸 {camName}: {Mathf.RoundToInt(speedKmh)} km/h! +{bonus} ₺ Speed Bonus · 🚨 Police alerted!"
                            : $"📸 {camName}: {Mathf.RoundToInt(speedKmh)} km/h! +{bonus} ₺ Hız Bonusu · 🚨 Polis alarma geçti!");
                    }
                    else
                    {
                        mode.RaiseStatusMessage(en
                            ? $"📸 {camName}: {Mathf.RoundToInt(speedKmh)} km/h! Street Speed Bonus +{bonus} ₺"
                            : $"📸 {camName}: {Mathf.RoundToInt(speedKmh)} km/h! Hız Rekoru Ödülü +{bonus} ₺");
                    }
                    return;
                }
            }
        }

        // =====================================================================
        // 3. DYNAMIC GTA STREET CRIME — KAÇAN YANKESİCİ (PURSE SNATCHER!)
        // =====================================================================
        private void UpdatePurseSnatcherCrime(PlayerModeController mode, float dt)
        {
            if (!_thiefActive)
            {
                if (PoliceWantedSystem.Instance != null && PoliceWantedSystem.Instance.IsWanted) return;
                _crimeSpawnTimer -= dt;
                if (_crimeSpawnTimer <= 0f)
                {
                    _crimeSpawnTimer = UnityEngine.Random.Range(95f, 145f);
                    TrySpawnPurseSnatcher(mode.ActiveWorldPosition);
                }
                return;
            }

            if (_thiefRoot == null)
            {
                _thiefActive = false;
                return;
            }

            _thiefLifetime -= dt;
            if (_thiefHitCooldown > 0f) _thiefHitCooldown -= dt;

            Vector3 tPos = _thiefRoot.transform.position;
            Vector3 pPos = mode.ActiveWorldPosition;
            float distToPlayer = Vector3.Distance(tPos, pPos);

            if (_thiefLifetime <= 0f || distToPlayer > 98f)
            {
                Destroy(_thiefRoot);
                _thiefActive = false;
                bool enEsc = LocalizationService.IsEnglish;
                mode.RaiseStatusMessage(enEsc
                    ? "💨 The purse snatcher got away into the alleys!"
                    : "💨 Yankesici ara sokaklara karışıp kaçtı!");
                return;
            }

            // Blocking / bumping the fleeing thief with your car knocks him out cleanly too!
            if (mode.IsDriving && mode.CurrentVehicle != null && mode.CurrentVehicle.SpeedKmh > 14f && distToPlayer < 2.85f)
            {
                CompleteSnatcherTakedown(mode);
                return;
            }

            // Thief sprints away from player using smart obstacle steering!
            Vector3 away = tPos - pPos;
            away.y = 0f;
            if (away.sqrMagnitude > 0.2f && distToPlayer < 22f)
            {
                _thiefRunDir = Vector3.Slerp(_thiefRunDir, away.normalized, 1f - Mathf.Exp(-3.5f * dt)).normalized;
            }

            Vector3 steer = NpcSmartNavigation.ComputeSmartSteering(_thiefRoot.transform, _thiefRunDir, 2.2f, 0.30f, avoidPlayer: false);
            NpcSmartNavigation.MoveActorSafely(_thiefRoot.transform, _thiefRb, steer * (4.15f * dt), ref _thiefRunDir, true, 0.30f);
            _thiefRoot.transform.rotation = Quaternion.Slerp(_thiefRoot.transform.rotation, Quaternion.LookRotation(steer, Vector3.up), 1f - Mathf.Exp(-10f * dt));
            _thiefHum?.SetLocomotion(4.15f, true, 0f, steer);
        }

        private void TrySpawnPurseSnatcher(Vector3 playerPos)
        {
            ProceduralHumanoid template = PlayerModeController.Instance?.FootController?.Humanoid;
            if (template == null) return;

            for (int a = 0; a < 8; a++)
            {
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                Vector3 cand = playerPos + new Vector3(Mathf.Cos(ang) * 24f, 0.08f, Mathf.Sin(ang) * 24f);
                cand.x = Mathf.Clamp(cand.x, -380f, 380f);
                cand.z = Mathf.Clamp(cand.z, -380f, 380f);
                if (cand.x >= -226f && cand.x <= -168f && cand.z >= -26f && cand.z <= 26f) continue;

                if (Physics.CheckCapsule(cand + Vector3.up * 0.45f, cand + Vector3.up * 1.65f, 0.45f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;

                ProceduralHumanoid hum = Instantiate(template, transform);
                hum.name = "GTA_PurseSnatcher";
                hum.transform.position = cand;
                Collider[] cols = hum.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < cols.Length; c++) Destroy(cols[c]);

                CapsuleCollider cap = hum.gameObject.AddComponent<CapsuleCollider>();
                cap.radius = 0.30f;
                cap.height = 1.76f;
                cap.center = new Vector3(0f, 0.88f, 0f);
                _thiefRb = WorldPhysicsSolidifier.EnsureKinematicRigidbody(hum.gameObject);

                // Dark hoodie + bright stolen magenta handbag carried in hand!
                Renderer[] rends = hum.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rends.Length; r++)
                {
                    Material[] ms = rends[r].sharedMaterials;
                    for (int m = 0; m < ms.Length; m++)
                    {
                        if (ms[m] != null && ms[m].name.Contains("MAT_Char_Shirt")) ms[m] = _thiefJacketMat;
                    }
                    rends[r].sharedMaterials = ms;
                }
                MakeDecor(hum.transform, "StolenHandbag", new Vector3(0.32f, 0.92f, 0.24f), new Vector3(0.28f, 0.24f, 0.14f), Quaternion.identity, _stolenBagMat);

                _thiefRoot = hum.gameObject;
                _thiefHum = hum;
                _thiefHealth = 70f;
                _thiefLifetime = 55f;
                _thiefRunDir = (cand - playerPos).normalized;
                _thiefRunDir.y = 0f;
                if (_thiefRunDir.sqrMagnitude < 0.01f) _thiefRunDir = Vector3.forward;
                _thiefActive = true;

                bool en = LocalizationService.IsEnglish;
                NpcSpeechBubbleManager.Say(hum.transform, en ? "Purse Snatcher" : "Yankesici",
                    en ? "Haha! Catch me if you can!" : "Haha! Yakalayabilirsen yakala!", 3.2f, NpcBubbleMood.Alert, 2.08f);
                PlayerModeController.Instance?.RaiseStatusMessage(en
                    ? "🔵 STREET CRIME! A purse snatcher is fleeing nearby (Blue Radar Dot) — stop him for +220 ₺!"
                    : "🔵 SOKAK OLAYI! Yakında kaçan bir yankesici var (Haritada Mavi Nokta) — yakala ve +220 ₺ kazan!");
                GameFeelAudio.PlayUiClick();
                return;
            }
        }

        public bool TryReceivePlayerPunch(Vector3 strikeCenter, float radius, Vector3 attackerPos, Vector3 punchDir, int comboStep)
        {
            if (!_thiefActive || _thiefRoot == null || _thiefHitCooldown > 0f) return false;
            Vector3 d = (_thiefRoot.transform.position + Vector3.up * 1.0f) - strikeCenter;
            if (Mathf.Abs(d.y) > 1.8f || d.sqrMagnitude > (radius + 0.65f) * (radius + 0.65f)) return false;

            _thiefHitCooldown = 0.24f;
            float dmg = comboStep >= 2 ? 50f : 38f;
            _thiefHealth -= dmg;

            if (_thiefHealth <= 0f)
            {
                CompleteSnatcherTakedown(PlayerModeController.Instance);
            }
            else
            {
                _thiefHum?.PlayHitReact();
                VisualEffectsManager.SpawnFloatingText(
                    _thiefRoot.transform.position + Vector3.up * 1.6f,
                    $"-{Mathf.RoundToInt(dmg)}", new Color(0.28f, 0.90f, 1f), scale: 0.95f, duration: 0.7f);
            }
            return true;
        }

        private void CompleteSnatcherTakedown(PlayerModeController mode)
        {
            Vector3 pos = _thiefRoot != null ? _thiefRoot.transform.position : Vector3.zero;
            _thiefActive = false;

            if (_thiefHum != null) _thiefHum.PlayKnockdown(8f);
            if (_thiefRoot != null) Destroy(_thiefRoot, 6.5f);
            _thiefRoot = null;
            _thiefHum = null;

            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                SaveManager.Instance.Data.streetCrimesStoppedCount++;
            }
            SaveManager.Instance?.AddMoney(220);
            NeighborhoodReputation.AddReputation(35);
            GameFeelAudio.PlayMilestoneFanfare();
            GameFeelAudio.SpawnBurst(pos + Vector3.up * 1.3f, new Color(0.26f, 0.92f, 0.72f), count: 16, spread: 0.7f);
            GameFeelAudio.TriggerHitstop(0.068f, slowScale: 0.18f);

            bool en = LocalizationService.IsEnglish;
            VisualEffectsManager.SpawnFloatingText(pos + Vector3.up * 1.8f,
                en ? "🏆 BAG RECOVERED! +220 ₺" : "🏆 ÇANTA KURTARILDI! +220 ₺",
                new Color(0.28f, 0.96f, 0.66f), scale: 1.2f, duration: 1.6f);
            mode?.RaiseStatusMessage(en
                ? "🏆 Neighborhood Hero! You stopped the purse snatcher & returned the bag (+220 ₺ · +35 Rep)!"
                : "🏆 Mahalle Kahramanı! Yankesiciyi durdurup çantayı sahibine ulaştırdın (+220 ₺ · +35 Saygınlık)!");
        }

        private static void MakeDecor(Transform parent, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            g.transform.localScale = scale;
            Collider c = g.GetComponent<Collider>();
            if (c != null) Destroy(c);
            SetupMat(g, mat, castShadows: false);
        }

        private static void SetupMat(GameObject go, Material mat, bool castShadows)
        {
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }
    }
}
