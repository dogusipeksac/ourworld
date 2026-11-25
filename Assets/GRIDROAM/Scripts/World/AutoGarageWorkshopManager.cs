using System;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    public enum GarageRepairType
    {
        TireChange,
        EngineOilService,
        BodyworkAndPaint
    }

    public struct CustomerRepairOrder
    {
        public string CustomerName;
        public string CarNameTr;
        public string CarNameEn;
        public GarageRepairType RepairType;
        public Color CarColor;
        public int RewardTl;
        public string[] StepLabelsTr;
        public string[] StepLabelsEn;
        public string ComplaintTr;
        public string ComplaintEn;
    }

    /// <summary>
    /// Phase 2: Interactive No:73 Auto Repair Workshop & Vehicle Tuning Station right beside Usta Kemal's garage!
    /// - Spawns Blender OW_HydraulicCarLift + OW_GarageToolStation on the sunny apron next to No:73 (x=35.0, z=9.2)
    /// - Spawns dynamic customer cars directly on the hydraulic lift carriage, raises/lowers smoothly during repairs,
    ///   emits wrench/spark bursts on each repair step, and pays out cash + neighborhood reputation!
    /// - Also places the Garage Tuning Board right beside No:73 for instant own-car paint, rims, spoiler, roof rack & performance mods.
    /// </summary>
    public sealed class AutoGarageWorkshopManager : MonoBehaviour
    {
        public static AutoGarageWorkshopManager Instance { get; private set; }

        public static readonly Vector3 LiftBayWorldPos = new Vector3(35.0f, 0f, 9.2f);
        public static readonly Vector3 ModBoardWorldPos = new Vector3(28.8f, 0f, 5.4f);

        private Transform _liftCarriage;
        private GameObject _customerCarRoot;
        private MeshRenderer[] _customerPaintRenderers;
        private InteractableTrigger _repairTrigger;
        private InteractableTrigger _modTrigger;

        private float _carriageBaseY;
        private float _carriageTargetLift;
        private float _carriageCurrentLift;
        private float _carriageLiftVelocity;
        private bool _hasActiveCustomer;
        private float _nextCustomerTimer;
        private int _orderCounter;
        private CustomerRepairOrder _activeOrder;
        private int _currentStepIndex;

        public bool HasActiveCustomer => _hasActiveCustomer;
        public CustomerRepairOrder ActiveOrder => _activeOrder;
        public int CurrentStepIndex => _currentStepIndex;
        public event Action OnWorkshopStateChanged;

        private static bool IsEn =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_AutoGarageWorkshop");
            go.AddComponent<AutoGarageWorkshopManager>();
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
            BuildWorkshopBay();
            SpawnNextCustomerOrder();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }
        }

        private readonly System.Collections.Generic.List<Material> _runtimeMats =
            new System.Collections.Generic.List<Material>(16);

        private void OnDestroy()
        {
            for (int i = 0; i < _runtimeMats.Count; i++)
            {
                if (_runtimeMats[i] != null) Destroy(_runtimeMats[i]);
            }
            _runtimeMats.Clear();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Smooth hydraulic lift animation with gentle ramp-up and cushioned stop
            if (_liftCarriage != null)
            {
                _carriageCurrentLift = Mathf.SmoothDamp(_carriageCurrentLift, _carriageTargetLift, ref _carriageLiftVelocity, 0.36f, 0.92f, dt);
                Vector3 lp = _liftCarriage.localPosition;
                lp.y = _carriageBaseY + _carriageCurrentLift;
                _liftCarriage.localPosition = lp;
            }

            if (_customerCarRoot != null && _customerCarRoot.activeSelf)
            {
                _customerCarRoot.transform.position = LiftBayWorldPos + new Vector3(0f, 0.16f + _carriageCurrentLift, 0f);
            }

            if (!_hasActiveCustomer && _nextCustomerTimer > 0f)
            {
                _nextCustomerTimer -= dt;
                if (_nextCustomerTimer <= 0f)
                {
                    SpawnNextCustomerOrder();
                }
            }
        }

        public void SetLiftRaised(bool raised)
        {
            _carriageTargetLift = raised ? 1.12f : 0f;
        }

        public bool AdvanceRepairStep(out bool completed, out string feedback)
        {
            completed = false;
            feedback = string.Empty;
            if (!_hasActiveCustomer)
            {
                feedback = IsEn ? "Waiting for next customer..." : "Yeni müşteri aracı bekleniyor...";
                return false;
            }

            _carriageTargetLift = _activeOrder.RepairType == GarageRepairType.TireChange ? 1.15f : 0.72f;
            _currentStepIndex++;

            Vector3 fxPos = LiftBayWorldPos + new Vector3(0f, 1.1f + _carriageCurrentLift, 0f);
            Color fxCol = _activeOrder.RepairType == GarageRepairType.BodyworkAndPaint
                ? _activeOrder.CarColor
                : new Color(1.0f, 0.78f, 0.24f);
            GameFeelAudio.PlayUiClick();
            GameFeelAudio.SpawnBurst(fxPos, fxCol, count: 14, spread: 0.75f);

            int totalSteps = _activeOrder.StepLabelsTr.Length;
            if (_currentStepIndex < totalSteps)
            {
                string stepDone = IsEn ? _activeOrder.StepLabelsEn[_currentStepIndex - 1] : _activeOrder.StepLabelsTr[_currentStepIndex - 1];
                feedback = IsEn ? $"Done: {stepDone} ({_currentStepIndex}/{totalSteps})" : $"Tamamlandı: {stepDone} ({_currentStepIndex}/{totalSteps})";
                OnWorkshopStateChanged?.Invoke();
                return true;
            }

            // Repair Finished! Lower lift, award money & reputation!
            completed = true;
            _hasActiveCustomer = false;
            _carriageTargetLift = 0f;
            _nextCustomerTimer = 14f;

            int earned = Mathf.RoundToInt(_activeOrder.RewardTl * NeighborhoodReputation.GetJobRewardMultiplier());
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Data.totalCarsRepaired++;
                SaveManager.Instance.AddMoney(earned);
                SaveManager.Instance.Save();
            }
            NeighborhoodReputation.AddReputation(12, IsEn ? "Auto Repair" : "Oto Tamir");
            GameFeelAudio.PlayCoinChime();
            GameFeelAudio.SpawnBurst(LiftBayWorldPos + Vector3.up * 1.4f, new Color(0.24f, 0.90f, 0.72f), count: 22, spread: 0.95f);

            string thanks = IsEn
                ? $"Runs like brand new! Thanks mechanic! (+{earned} ₺)"
                : $"Ellerine sağlık usta, araba sıfır gibi oldu! (+{earned} ₺)";
            if (_customerCarRoot != null)
            {
                NpcSpeechBubbleManager.Say(_customerCarRoot.transform, _activeOrder.CustomerName, thanks, 3.6f, NpcBubbleMood.Friendly, 1.9f);
            }

            feedback = IsEn
                ? $"Repaired {_activeOrder.CustomerName}'s {_activeOrder.CarNameEn}! (+{earned} ₺)"
                : $"{_activeOrder.CustomerName}'in {_activeOrder.CarNameTr} aracı onarıldı! (+{earned} ₺)";
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage(feedback);
            }

            RefreshPrompts();
            OnWorkshopStateChanged?.Invoke();
            return true;
        }

        private void SpawnNextCustomerOrder()
        {
            string[] names = { "Hakan Bey", "Selin Hanım", "Murat Abi", "Zeynep Abla", "Cemil Amca", "Burak" };
            Color[] colors =
            {
                new Color(0.76f, 0.16f, 0.16f),
                new Color(0.14f, 0.44f, 0.76f),
                new Color(0.88f, 0.68f, 0.16f),
                new Color(0.16f, 0.56f, 0.44f),
                new Color(0.86f, 0.87f, 0.88f),
                new Color(0.46f, 0.22f, 0.58f)
            };

            GarageRepairType type = (GarageRepairType)(_orderCounter % 3);
            string customer = names[_orderCounter % names.Length];
            Color carCol = colors[_orderCounter % colors.Length];
            _orderCounter++;

            if (type == GarageRepairType.TireChange)
            {
                _activeOrder = new CustomerRepairOrder
                {
                    CustomerName = customer,
                    CarNameTr = "Aile Sedanı",
                    CarNameEn = "Family Sedan",
                    RepairType = type,
                    CarColor = carCol,
                    RewardTl = 145,
                    ComplaintTr = "Usta, sağ ön lastik patladı! Lifti kaldırıp değiştirebilir misin?",
                    ComplaintEn = "Mechanic, front tire blew out! Can you lift it and swap the wheel?",
                    StepLabelsTr = new[] { "1. Bijonları Sök & Patlak Lastiği Çıkar", "2. Yeni Alaşım Lastiği Tak", "3. Bijonları Tork Anahtarıyla Sık" },
                    StepLabelsEn = new[] { "1. Loosen Lug Nuts & Remove Flat Tire", "2. Mount Fresh Alloy Wheel", "3. Torque-Tighten All Lug Nuts" }
                };
            }
            else if (type == GarageRepairType.EngineOilService)
            {
                _activeOrder = new CustomerRepairOrder
                {
                    CustomerName = customer,
                    CarNameTr = "Şehir Hatchback",
                    CarNameEn = "City Hatchback",
                    RepairType = type,
                    CarColor = carCol,
                    RewardTl = 185,
                    ComplaintTr = "Motor hararet ikazı yaktı, yağ ve buji bakımı lazım usta!",
                    ComplaintEn = "Engine warning light came on — needs fresh oil & spark plug service!",
                    StepLabelsTr = new[] { "1. Kaputu Aç & Eski Motor Yağını Boşalt", "2. Yeni Yağ Filtresi & Bujileri Tak", "3. Tam Sentetik Yağ Doldur & Test Et" },
                    StepLabelsEn = new[] { "1. Open Hood & Drain Old Engine Oil", "2. Install Fresh Oil Filter & Spark Plugs", "3. Pour Synthetic Oil & Test Idle" }
                };
            }
            else
            {
                _activeOrder = new CustomerRepairOrder
                {
                    CustomerName = customer,
                    CarNameTr = "Spor Coupe",
                    CarNameEn = "Sport Coupe",
                    RepairType = type,
                    CarColor = carCol,
                    RewardTl = 220,
                    ComplaintTr = "Park ederken kaportayı sürttüm, düzeltip parlak boya atabilir misin?",
                    ComplaintEn = "Scratched the fender parking — can you buff the dent and repaint?",
                    StepLabelsTr = new[] { "1. Kaporta Eziğini Çekiçle & Düzelt", "2. Yüzeyi Zımparala & Astar Çek", "3. Metalik Boya & Parlak Vernik Uygula" },
                    StepLabelsEn = new[] { "1. Hammer & Smooth Fender Dent", "2. Sand Panel & Apply Primer Coat", "3. Spray Metallic Paint & Gloss Clearcoat" }
                };
            }

            _hasActiveCustomer = true;
            _currentStepIndex = 0;
            _carriageTargetLift = 0.08f;

            if (_customerCarRoot != null)
            {
                _customerCarRoot.SetActive(true);
                OurWorldModelLibrary.TintMaterialByKeyword(_customerCarRoot, "CarBodyPaint", carCol);
                OurWorldModelLibrary.TintMaterialByKeyword(_customerCarRoot, "OW_CarPaint", carCol);
                OurWorldModelLibrary.TintMaterialByKeyword(_customerCarRoot, "OW_CarPaintAccent", carCol * 0.72f);
                if (_customerPaintRenderers != null)
                {
                    for (int i = 0; i < _customerPaintRenderers.Length; i++)
                    {
                        if (_customerPaintRenderers[i] != null)
                        {
                            Material m = _customerPaintRenderers[i].material;
                            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", carCol);
                            if (m.HasProperty("_Color")) m.SetColor("_Color", carCol);
                        }
                    }
                }
            }

            RefreshPrompts();
            OnWorkshopStateChanged?.Invoke();
        }

        private void RefreshPrompts()
        {
            if (_repairTrigger != null)
            {
                if (_hasActiveCustomer)
                {
                    _repairTrigger.SetCustomPrompt(IsEn
                        ? $"Repair Car ({_activeOrder.CustomerName} · +{_activeOrder.RewardTl} ₺)"
                        : $"Araç Tamir Et ({_activeOrder.CustomerName} · +{_activeOrder.RewardTl} ₺)");
                }
                else
                {
                    _repairTrigger.SetCustomPrompt(IsEn ? "No:73 Auto Lift (Next Customer Soon)" : "No:73 Tamir Lifti (Yeni Müşteri Geliyor)");
                }
            }
            if (_modTrigger != null)
            {
                _modTrigger.SetCustomPrompt(IsEn ? "Modify & Tune My Car · No:73" : "Kendi Aracını Modifiye Et · No:73");
            }
        }

        private void BuildWorkshopBay()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material padMat = MakeMat(lit, "MAT_GaragePad", new Color(0.46f, 0.47f, 0.49f), 0.16f);
            Material stripeMat = MakeMat(lit, "MAT_GarageYellow", new Color(0.96f, 0.74f, 0.14f), 0.30f);
            Material darkMat = MakeMat(lit, "MAT_GarageDark", new Color(0.16f, 0.17f, 0.19f), 0.45f);
            Material blueMat = MakeMat(lit, "MAT_GarageBlue", new Color(0.12f, 0.44f, 0.78f), 0.35f);
            Material glassMat = MakeMat(lit, "MAT_CustGlass", new Color(0.14f, 0.24f, 0.34f), 0.85f);
            Material tireMat = MakeMat(lit, "MAT_CustTire", new Color(0.11f, 0.11f, 0.12f), 0.12f);
            Material paintMat = MakeMat(lit, "MAT_CustPaint", new Color(0.76f, 0.16f, 0.16f), 0.72f);

            Transform root = new GameObject("No73_OpenAirWorkshopBay").transform;
            root.SetParent(transform, false);

            // Concrete service pad & safety yellow curb stripes at x=35.0, z=9.2
            CreateBox("ServicePad", root, new Vector3(35.0f, 0.025f, 9.2f), new Vector3(7.4f, 0.05f, 7.2f), padMat, collider: false);
            CreateBox("SafetyStripeL", root, new Vector3(31.5f, 0.055f, 9.2f), new Vector3(0.18f, 0.01f, 6.6f), stripeMat, collider: false);
            CreateBox("SafetyStripeR", root, new Vector3(38.5f, 0.055f, 9.2f), new Vector3(0.18f, 0.01f, 6.6f), stripeMat, collider: false);

            // Blender OW_HydraulicCarLift!
            GameObject liftObj = OurWorldModelLibrary.InstantiateModel("OW_HydraulicCarLift", root, LiftBayWorldPos, Quaternion.identity);
            if (liftObj != null)
            {
                _liftCarriage = OurWorldModelLibrary.FindPart(liftObj.transform, "LiftCarriage");
                if (_liftCarriage != null) _carriageBaseY = _liftCarriage.localPosition.y;
            }

            // Blender OW_GarageToolStation!
            OurWorldModelLibrary.InstantiateModel("OW_GarageToolStation", root, new Vector3(32.1f, 0.05f, 11.4f), Quaternion.Euler(0f, 35f, 0f));

            // Sleek Blender OW_CustomerCar sitting cleanly on the hydraulic lift runners!
            _customerCarRoot = new GameObject("CustomerCarOnLift");
            _customerCarRoot.transform.SetParent(root, false);
            _customerCarRoot.transform.SetPositionAndRotation(LiftBayWorldPos + new Vector3(0f, 0.16f, 0f), Quaternion.identity);

            GameObject custFbx = OurWorldModelLibrary.InstantiateModel("OW_CustomerCar", _customerCarRoot.transform, Vector3.zero, Quaternion.identity);
            if (custFbx != null)
            {
                BoxCollider col = _customerCarRoot.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.72f, 0f);
                col.size = new Vector3(1.85f, 1.35f, 4.20f);
                _customerPaintRenderers = null;
            }
            else
            {
                GameObject lowerBody = CreateBox("CustBodyLower", _customerCarRoot.transform, new Vector3(0f, 0.54f, 0f), new Vector3(1.80f, 0.56f, 4.10f), paintMat, collider: true);
                GameObject upperCab = CreateBox("CustCabin", _customerCarRoot.transform, new Vector3(0f, 1.02f, -0.15f), new Vector3(1.56f, 0.48f, 2.10f), paintMat, collider: false);
                CreateBox("CustWindshield", _customerCarRoot.transform, new Vector3(0f, 1.02f, 0.88f), new Vector3(1.44f, 0.40f, 0.10f), glassMat, collider: false);
                CreateBox("CustRearGlass", _customerCarRoot.transform, new Vector3(0f, 1.02f, -1.18f), new Vector3(1.44f, 0.38f, 0.10f), glassMat, collider: false);
                CreateBox("CustSideGlass", _customerCarRoot.transform, new Vector3(0f, 1.02f, -0.15f), new Vector3(1.59f, 0.34f, 1.72f), glassMat, collider: false);
                CreateBox("CustBumperF", _customerCarRoot.transform, new Vector3(0f, 0.32f, 2.08f), new Vector3(1.76f, 0.24f, 0.16f), darkMat, collider: false);
                CreateBox("CustBumperR", _customerCarRoot.transform, new Vector3(0f, 0.32f, -2.08f), new Vector3(1.76f, 0.24f, 0.16f), darkMat, collider: false);

                Vector3[] wPos = { new Vector3(-0.88f, 0.32f, 1.24f), new Vector3(0.88f, 0.32f, 1.24f), new Vector3(-0.88f, 0.32f, -1.24f), new Vector3(0.88f, 0.32f, -1.24f) };
                for (int i = 0; i < wPos.Length; i++)
                {
                    GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    w.name = $"CustWheel_{i}";
                    w.transform.SetParent(_customerCarRoot.transform, false);
                    w.transform.localPosition = wPos[i];
                    w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    w.transform.localScale = new Vector3(0.64f, 0.14f, 0.64f);
                    Destroy(w.GetComponent<Collider>());
                    w.GetComponent<MeshRenderer>().sharedMaterial = tireMat;
                }
                _customerPaintRenderers = new[] { lowerBody.GetComponent<MeshRenderer>(), upperCab.GetComponent<MeshRenderer>() };
            }

            // Repair Trigger right in front of the hydraulic lift!
            GameObject repairTrigObj = new GameObject("Trigger_GarageRepairBay");
            repairTrigObj.transform.SetParent(root, false);
            repairTrigObj.transform.position = new Vector3(35.0f, 0.05f, 6.2f);
            _repairTrigger = repairTrigObj.AddComponent<InteractableTrigger>();
            _repairTrigger.Configure(InteractableKind.GarageRepairBay, "no73_garage_repair_bay", "ui.btn.talk", 3.6f);

            // Own-Car Tuning & Modification Stand right beside No:73 gate!
            GameObject modBoard = new GameObject("No73_CarModificationBoard");
            modBoard.transform.SetParent(root, false);
            modBoard.transform.position = ModBoardWorldPos;
            CreateBox("PostL", modBoard.transform, new Vector3(-0.95f, 1.05f, 0f), new Vector3(0.14f, 2.1f, 0.14f), darkMat, collider: true);
            CreateBox("PostR", modBoard.transform, new Vector3( 0.95f, 1.05f, 0f), new Vector3(0.14f, 2.1f, 0.14f), darkMat, collider: true);
            CreateBox("Board", modBoard.transform, new Vector3(0f, 1.50f, 0f), new Vector3(2.10f, 1.05f, 0.12f), blueMat, collider: false);
            CreateBox("TopTrim", modBoard.transform, new Vector3(0f, 2.08f, 0f), new Vector3(2.22f, 0.14f, 0.20f), stripeMat, collider: false);

            GameObject txtObj = new GameObject("ModBoardText");
            txtObj.transform.SetParent(modBoard.transform, false);
            txtObj.transform.localPosition = new Vector3(0f, 1.52f, -0.09f);
            TextMesh tm = txtObj.AddComponent<TextMesh>();
            tm.text = "NO:73 MODİFİYE\n& TUNING";
            tm.characterSize = 0.056f;
            tm.fontSize = 40;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.94f, 0.72f);

            _modTrigger = modBoard.AddComponent<InteractableTrigger>();
            _modTrigger.Configure(InteractableKind.GarageModBoard, "no73_garage_mod_board", "ui.btn.talk", 3.8f);

            // Warm overhead garage work light for night repairs!
            GameObject lampObj = new GameObject("GarageLiftNightLamp");
            lampObj.transform.SetParent(root, false);
            lampObj.transform.position = new Vector3(35.0f, 4.1f, 9.2f);
            Light pl = lampObj.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(1.0f, 0.88f, 0.62f);
            pl.range = 26f;
            pl.intensity = 28f;
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

        private Material MakeMat(Shader sh, string name, Color c, float smooth)
        {
            Material m = new Material(sh) { name = name };
            _runtimeMats.Add(m);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }
    }
}
