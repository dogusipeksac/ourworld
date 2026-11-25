using OurWorld.Core;
using OurWorld.Player;
using OurWorld.Vehicle;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Phase 3B: Aydos High-Pressure Car Wash Station on the Lake Boulevard approach (-124, 0, 11.8):
    /// - Features our custom Blender OW_CarWashGantry with 3 animated spinning foam brush rollers!
    /// - Interactive both IN-CAR (drive under the arch and tap Wash) and ON-FOOT!
    /// - Runs a joyful 4.2s 3-stage animated Foam -> Spinning Brushes & Water Jets -> Wax Polish sequence,
    ///   restoring vehicle gloss, chassis health, and awarding neighborhood style reputation.
    /// </summary>
    public sealed class CarWashStationController : MonoBehaviour
    {
        public static CarWashStationController Instance { get; private set; }
        public static readonly Vector3 StationWorldPos = new Vector3(124.0f, 0.0f, -14.5f);
        public const int WashCostTl = 40;

        private Transform _brushL;
        private Transform _brushR;
        private Transform _brushTop;
        private Vector3 _brushLBasePos;
        private Vector3 _brushRBasePos;
        private Vector3 _brushTopBasePos;
        private float _currentSpinSpeed = 32f;
        private float _brushEngageBlend;

        private bool _isWashing;
        private float _washTimer;
        private float _fxTimer;
        private SedanVehicleController _targetCar;

        public bool IsWashing => _isWashing;

        private static bool IsEn =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_CarWashStation");
            go.AddComponent<CarWashStationController>();
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

        private void Start()
        {
            BuildCarWashStation();
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void StartWash(PlayerModeController player)
        {
            if (_isWashing || player == null) return;

            if (SaveManager.Instance != null && !SaveManager.Instance.TrySpend(WashCostTl))
            {
                player.RaiseStatusMessage(IsEn
                    ? $"Need {WashCostTl} ₺ for Foam & Wax Car Wash."
                    : $"Köpüklü Oto Yıkama için {WashCostTl} ₺ gerekiyor.");
                return;
            }

            _targetCar = player.IsDriving && player.CurrentVehicle != null
                ? player.CurrentVehicle
                : player.PlayerSedan;

            _isWashing = true;
            _washTimer = 0f;
            _fxTimer = 0f;

            GameFeelAudio.PlayUiClick();
            player.RaiseStatusMessage(IsEn
                ? "Aydos Car Wash started! Active Foam & High-Pressure Brushes..."
                : "Aydos Oto Yıkama başladı! Köpük, Dönen Fırçalar & Tazyikli Su...");
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Smooth motor inertia spin-up/spin-down & gentle brush hug/roof-descent articulation
            float targetSpin = _isWashing ? 420f : 32f;
            _currentSpinSpeed = Mathf.Lerp(_currentSpinSpeed, targetSpin, 1f - Mathf.Exp(-3.2f * dt));
            float targetEngage = (_isWashing && _washTimer >= 0.35f && _washTimer <= 3.75f) ? 1f : 0f;
            _brushEngageBlend = Mathf.Lerp(_brushEngageBlend, targetEngage, 1f - Mathf.Exp(-3.8f * dt));

            float sweep = Mathf.Sin(Time.time * 4.5f) * 0.015f * _brushEngageBlend;
            if (_brushL != null)
            {
                _brushL.Rotate(0f, _currentSpinSpeed * dt, 0f, Space.Self);
                _brushL.localPosition = _brushLBasePos + new Vector3(0.06f * _brushEngageBlend + sweep, 0f, 0f);
            }
            if (_brushR != null)
            {
                _brushR.Rotate(0f, -_currentSpinSpeed * dt, 0f, Space.Self);
                _brushR.localPosition = _brushRBasePos + new Vector3(-0.06f * _brushEngageBlend - sweep, 0f, 0f);
            }
            if (_brushTop != null)
            {
                _brushTop.Rotate(_currentSpinSpeed * 0.85f * dt, 0f, 0f, Space.Self);
                _brushTop.localPosition = _brushTopBasePos + new Vector3(0f, -0.52f * _brushEngageBlend + sweep * 0.6f, 0f);
            }

            if (!_isWashing) return;

            _washTimer += dt;
            _fxTimer -= dt;

            if (_fxTimer <= 0f)
            {
                _fxTimer = 0.22f;
                Vector3 center = StationWorldPos + new Vector3(0f, 1.5f, 0f);
                Color fxCol = _washTimer < 1.6f
                    ? new Color(0.95f, 0.98f, 1.0f) // Thick white foam clouds
                    : (_washTimer < 3.2f ? new Color(0.28f, 0.86f, 0.98f) // High-pressure water jets
                                         : new Color(0.98f, 0.86f, 0.36f)); // Golden wax shine
                GameFeelAudio.SpawnBurst(center + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.2f, 0.5f), Random.Range(-0.8f, 0.8f)),
                    fxCol, count: 10, spread: 0.95f);
            }

            if (_washTimer >= 4.2f)
            {
                _isWashing = false;
                if (_targetCar != null)
                {
                    _targetCar.SparklePolishClean();
                }
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.Data.totalCarWashes++;
                    SaveManager.Instance.Save();
                }
                NeighborhoodReputation.AddReputation(8, IsEn ? "Sparkling Car Wash" : "Oto Yıkama Bakımı");
                GameFeelAudio.PlayCoinChime();
                GameFeelAudio.SpawnBurst(StationWorldPos + Vector3.up * 1.8f, new Color(0.32f, 0.95f, 0.88f), count: 24, spread: 1.2f);

                if (PlayerModeController.Instance != null)
                {
                    PlayerModeController.Instance.RaiseStatusMessage(IsEn
                        ? "Car Wash Complete! Mirror-gloss wax finish & chassis refreshed!"
                        : "Oto Yıkama Tamamlandı! Aracın pırıl pırıl cilalandı!");
                }
            }
        }

        private void BuildCarWashStation()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material padMat = MakeMat(lit, "MAT_WashPad", new Color(0.38f, 0.42f, 0.46f), 0.38f);
            Material grateMat = MakeMat(lit, "MAT_WashGrate", new Color(0.16f, 0.22f, 0.26f), 0.45f);
            Material tealMat = MakeMat(lit, "MAT_WashTeal", new Color(0.10f, 0.66f, 0.64f), 0.30f);

            Transform root = new GameObject("Aydos_CarWashStation").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(StationWorldPos, Quaternion.Euler(0f, 180f, 0f));

            // Concrete driveway apron connecting smoothly to Lake Boulevard (z = 0)!
            CreateBox("WashApron", root, new Vector3(0f, 0.025f, -2.6f), new Vector3(7.6f, 0.05f, 12.6f), padMat, collider: false);
            CreateBox("DrainGrate", root, new Vector3(0f, 0.055f, 0f), new Vector3(3.4f, 0.01f, 5.2f), grateMat, collider: false);

            // Real Blender OW_CarWashGantry with 3 animated spinning brush rollers!
            GameObject gantry = OurWorldModelLibrary.InstantiateModel("OW_CarWashGantry", root, Vector3.zero, Quaternion.identity);
            if (gantry != null)
            {
                _brushL = OurWorldModelLibrary.FindPart(gantry.transform, "BrushRollerL");
                _brushR = OurWorldModelLibrary.FindPart(gantry.transform, "BrushRollerR");
                _brushTop = OurWorldModelLibrary.FindPart(gantry.transform, "TopBrushRoller");
                if (_brushL != null) _brushLBasePos = _brushL.localPosition;
                if (_brushR != null) _brushRBasePos = _brushR.localPosition;
                if (_brushTop != null) _brushTopBasePos = _brushTop.localPosition;
            }

            // Overhead readable sign facing South toward Selman/Lake Boulevard!
            GameObject signObj = new GameObject("CarWashSignText");
            signObj.transform.SetParent(root, false);
            signObj.transform.localPosition = new Vector3(0f, 3.68f, -0.86f);
            TextMesh tm = signObj.AddComponent<TextMesh>();
            tm.text = "AYDOS OTO YIKAMA · 40 ₺";
            tm.characterSize = 0.068f;
            tm.fontSize = 44;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(0.05f, 0.14f, 0.20f);

            // Side pay kiosk & interactable trigger (works BOTH driving inside bay & walking up!)
            CreateBox("PayKiosk", root, new Vector3(-3.3f, 0.72f, -3.2f), new Vector3(0.55f, 1.44f, 0.55f), tealMat, collider: true);

            InteractableTrigger trig = root.gameObject.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.CarWashStation, "aydos_car_wash_station", "ui.btn.talk", 5.6f);
            trig.SetCustomPrompt(IsEn ? $"High-Pressure Car Wash ({WashCostTl} ₺)" : $"Tazyikli Oto Yıkama ({WashCostTl} ₺)");

            // Bright crisp night bay light
            GameObject lampObj = new GameObject("CarWashNightLamp");
            lampObj.transform.SetParent(root, false);
            lampObj.transform.localPosition = new Vector3(0f, 3.25f, -0.4f);
            Light pl = lampObj.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(0.76f, 0.96f, 1.0f);
            pl.range = 28f;
            pl.intensity = 30f;
            pl.shadows = LightShadows.None;
            pl.enabled = false;
            NightLight nl = lampObj.AddComponent<NightLight>();
            nl.Configure(pl, null, Color.black);
            if (NeighborhoodNightLighting.Instance != null)
            {
                NeighborhoodNightLighting.Instance.RegisterDynamicOutdoorLight(nl);
            }
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (!collider) Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static Material MakeMat(Shader sh, string name, Color c, float smooth)
        {
            Material m = new Material(sh) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }
    }
}
