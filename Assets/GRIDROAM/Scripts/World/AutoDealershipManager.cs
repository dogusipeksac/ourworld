using System;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.UI;
using OurWorld.Vehicle;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace OurWorld.World
{
    /// <summary>
    /// Hamidiye Oto Galeri — Open-air architectural car showroom & personal vehicle garage
    /// built on the clear NW Atatürk–Mürşit parcel (Center X=-115, Z=71, facing Mürşit Cd.).
    /// Players can inspect 4 slowly rotating display cars, purchase 5 distinct vehicles
    /// persisted in SaveData, and summon/drive their active owned vehicle anywhere via Valet.
    /// </summary>
    public sealed class AutoDealershipManager : MonoBehaviour
    {
        public static AutoDealershipManager Instance { get; private set; }

        public static readonly Vector3 DealershipCenter = new Vector3(-115f, 0f, 71f);
        public static readonly Vector3 DeliveryBayPos = new Vector3(-115f, 0.38f, 56.5f);
        private const string DeskInteractId = "auto_dealership_desk";

        public sealed class DealershipCarDef
        {
            public string Id;
            public string NameTr;
            public string NameEn;
            public string SpecTr;
            public string SpecEn;
            public int Price;
            public int PaintIndex;
            public int RimIndex;
            public int HeadlightMode;
            public bool HasSpoiler;
            public bool HasRoofRack;
            public bool SportSuspension;
            public Color DisplayColor;
            public Vector3 DisplayScale;
            public int SpeedRating; // 1..10
            public int AccelRating; // 1..10
        }

        private static readonly DealershipCarDef[] Catalog =
        {
            new DealershipCarDef
            {
                Id = "car_classic_sedan",
                NameTr = "Klasik Mahalle Sedanı",
                NameEn = "Classic Neighborhood Sedan",
                SpecTr = "Dengeli şehir içi sürüş · Başlangıç aracı",
                SpecEn = "Balanced neighborhood cruiser · Starter car",
                Price = 0,
                PaintIndex = 7, // Bosphorus Blue
                RimIndex = 0,
                HeadlightMode = 0,
                HasSpoiler = false,
                HasRoofRack = false,
                SportSuspension = false,
                DisplayColor = new Color(0.16f, 0.42f, 0.82f),
                DisplayScale = new Vector3(1.0f, 1.0f, 1.0f),
                SpeedRating = 6,
                AccelRating = 6
            },
            new DealershipCarDef
            {
                Id = "car_aydos_suv",
                NameTr = "Aydos 4x4 SUV",
                NameEn = "Aydos 4x4 SUV",
                SpecTr = "Yüksek tork · Tavan barı · Çiftlik & Gölet ustası",
                SpecEn = "High torque · Roof rack · Built for Farm & Lake trails",
                Price = 650,
                PaintIndex = 4, // Aydos Green
                RimIndex = 1,
                HeadlightMode = 2, // Golden Rally
                HasSpoiler = false,
                HasRoofRack = true,
                SportSuspension = false,
                DisplayColor = new Color(0.14f, 0.44f, 0.24f),
                DisplayScale = new Vector3(1.06f, 1.22f, 1.04f),
                SpeedRating = 7,
                AccelRating = 8
            },
            new DealershipCarDef
            {
                Id = "car_hamidiye_pickup",
                NameTr = "Hamidiye Pikap GT",
                NameEn = "Hamidiye Pickup GT",
                SpecTr = "Güçlü iş ve teslimat canavarı · Altın jant · +%18 tork",
                SpecEn = "Heavy-duty street hauler · Gold rims · +18% torque",
                Price = 850,
                PaintIndex = 5, // Gold / Amber
                RimIndex = 2,
                HeadlightMode = 2,
                HasSpoiler = false,
                HasRoofRack = true,
                SportSuspension = true,
                DisplayColor = new Color(0.92f, 0.68f, 0.16f),
                DisplayScale = new Vector3(1.08f, 1.12f, 1.12f),
                SpeedRating = 8,
                AccelRating = 8
            },
            new DealershipCarDef
            {
                Id = "car_bogaz_coupe",
                NameTr = "Boğaz Spor Coupe",
                NameEn = "Bosphorus Sport Coupe",
                SpecTr = "Alçak spor gövde · GT Kanat · Xenon far · +%28 tork",
                SpecEn = "Low sport stance · GT Wing · Ice Xenon · +28% torque",
                Price = 1200,
                PaintIndex = 1, // Fire Red
                RimIndex = 1,
                HeadlightMode = 1, // Ice Xenon
                HasSpoiler = true,
                HasRoofRack = false,
                SportSuspension = true,
                DisplayColor = new Color(0.78f, 0.14f, 0.16f),
                DisplayScale = new Vector3(1.06f, 0.88f, 1.04f),
                SpeedRating = 9,
                AccelRating = 9
            },
            new DealershipCarDef
            {
                Id = "car_turkuaz_gt",
                NameTr = "Sultanbeyli Turkuaz GT",
                NameEn = "Sultanbeyli Turquoise GT",
                SpecTr = "Amiral gemisi süper coupe · Full modifiye · +%42 tork",
                SpecEn = "Flagship super-coupe · Full sport trim · +42% torque",
                Price = 2200,
                PaintIndex = 0, // Istanbul Turquoise
                RimIndex = 3,   // Turquoise Chrome
                HeadlightMode = 1,
                HasSpoiler = true,
                HasRoofRack = false,
                SportSuspension = true,
                DisplayColor = new Color(0.11f, 0.68f, 0.68f),
                DisplayScale = new Vector3(1.12f, 0.84f, 1.08f),
                SpeedRating = 10,
                AccelRating = 10
            }
        };

        private readonly List<Transform> _turntables = new List<Transform>(4);
        private Canvas _canvas;
        private GameObject _modalRoot;
        private RectTransform _listRoot;
        private Text _titleText;
        private Text _subtitleText;
        private Text _balanceText;
        private bool _isOpen;
        private bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("OurWorld_AutoDealershipManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AutoDealershipManager>();
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
            EnsureStarterOwned();
            BuildDealershipWorld();
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted += HandleInteracted;
                _subscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (_subscribed && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted -= HandleInteracted;
            }
            if (_isOpen && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _turntables.Count; i++)
            {
                if (_turntables[i] != null)
                {
                    _turntables[i].Rotate(0f, (i % 2 == 0 ? 16f : -14f) * dt, 0f, Space.Self);
                }
            }

            if (_isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseModal();
            }
        }

        private static void EnsureStarterOwned()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return;
            if (d.ownedVehicleIds == null) d.ownedVehicleIds = new List<string>();
            if (!d.ownedVehicleIds.Contains("car_classic_sedan"))
            {
                d.ownedVehicleIds.Add("car_classic_sedan");
            }
            if (string.IsNullOrEmpty(d.activeOwnedVehicleId))
            {
                d.activeOwnedVehicleId = "car_classic_sedan";
            }
        }

        private void HandleInteracted(InteractableTrigger trig)
        {
            if (trig == null) return;
            if (trig.InteractableId == DeskInteractId)
            {
                ShowModal();
            }
        }

        // =====================================================================
        // 3D ARCHITECTURAL SHOWROOM (NW ATATÜRK–MÜRŞİT PARCEL)
        // =====================================================================

        private void BuildDealershipWorld()
        {
            if (GameObject.Find("Hamidiye_OtoGaleri_Root") != null) return;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            GameObject root = new GameObject("Hamidiye_OtoGaleri_Root");
            root.transform.position = DealershipCenter;

            Material marbleFloor = MakeMat(sh, new Color(0.84f, 0.86f, 0.89f), 0.52f, 0.08f);
            Material asphaltApron = MakeMat(sh, new Color(0.24f, 0.26f, 0.30f), 0.20f, 0.04f);
            Material darkSteel = MakeMat(sh, new Color(0.11f, 0.14f, 0.19f), 0.65f, 0.62f);
            Material chrome = MakeMat(sh, new Color(0.84f, 0.88f, 0.92f), 0.88f, 0.85f);
            Material cyanGlow = MakeEmissiveMat(sh, new Color(0.12f, 0.82f, 0.78f), 1.6f);
            Material goldGlow = MakeEmissiveMat(sh, new Color(0.98f, 0.76f, 0.22f), 1.3f);
            Material glassTint = MakeMat(sh, new Color(0.22f, 0.36f, 0.48f), 0.92f, 0.25f);
            Material deskWood = MakeMat(sh, new Color(0.32f, 0.22f, 0.16f), 0.45f, 0.06f);
            Material tireMat = MakeMat(sh, new Color(0.10f, 0.10f, 0.12f), 0.25f, 0.05f);

            // Forecourt Apron & Showroom Marble Plinth
            Part("ForecourtApron", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.02f, -4.5f), new Vector3(28f, 0.06f, 22f), asphaltApron, withCollider: true);
            Part("ShowroomDeck", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.08f, 1.5f), new Vector3(24.5f, 0.12f, 13.5f), marbleFloor, withCollider: true);

            // Modern Floating Roof Canopy + Back Feature Wall + Glowing Cyan/Gold Fascia
            Part("RoofCanopy", PrimitiveType.Cube, root.transform, new Vector3(0f, 4.85f, 1.5f), new Vector3(25.2f, 0.58f, 14.2f), darkSteel, withCollider: true);
            Part("FasciaGlowFront", PrimitiveType.Cube, root.transform, new Vector3(0f, 4.88f, -5.65f), new Vector3(24.4f, 0.14f, 0.12f), cyanGlow, withCollider: false);
            Part("FasciaGlowGold", PrimitiveType.Cube, root.transform, new Vector3(0f, 4.62f, -5.65f), new Vector3(24.4f, 0.08f, 0.10f), goldGlow, withCollider: false);
            Part("BackWall", PrimitiveType.Cube, root.transform, new Vector3(0f, 2.45f, 8.1f), new Vector3(24.2f, 4.7f, 0.35f), darkSteel, withCollider: true);
            Part("BackGlassPanel", PrimitiveType.Cube, root.transform, new Vector3(0f, 2.5f, 7.85f), new Vector3(21.0f, 3.6f, 0.12f), glassTint, withCollider: false);

            // 6 Slim Steel Canopy Columns
            float[] colX = { -11.6f, 0f, 11.6f };
            float[] colZ = { -4.8f, 7.6f };
            for (int ix = 0; ix < colX.Length; ix++)
            {
                for (int iz = 0; iz < colZ.Length; iz++)
                {
                    if (ix == 1 && iz == 0) continue; // Keep center front entrance wide open!
                    Part($"Pillar_{ix}_{iz}", PrimitiveType.Cube, root.transform, new Vector3(colX[ix], 2.45f, colZ[iz]), new Vector3(0.42f, 4.7f, 0.42f), chrome, withCollider: true);
                }
            }

            // Crisp Rooftop Signboard ("HAMİDİYE OTO GALERİ")
            GameObject signBoard = Part("SignBoardBg", PrimitiveType.Cube, root.transform, new Vector3(0f, 5.75f, -5.35f), new Vector3(13.6f, 1.25f, 0.28f), darkSteel, withCollider: false);
            GameObject signTextGo = new GameObject("SignText_3D");
            signTextGo.transform.SetParent(signBoard.transform, false);
            signTextGo.transform.localPosition = new Vector3(0f, 0f, -0.56f);
            signTextGo.transform.localScale = new Vector3(1f / 13.6f, 1f / 1.25f, 1f);
            TextMesh tm = signTextGo.AddComponent<TextMesh>();
            tm.text = "HAMİDİYE OTO GALERİ";
            tm.fontSize = 72;
            tm.characterSize = 0.115f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = new Color(0.97f, 0.80f, 0.28f);

            // 4 Rotating Showroom Display Turntables (SUV, Pickup, Coupe, Turkuaz GT)
            float[] displayX = { -8.6f, -3.4f, 3.4f, 8.6f };
            for (int i = 0; i < 4; i++)
            {
                DealershipCarDef car = Catalog[i + 1];
                GameObject pad = Part($"TurntableBase_{i}", PrimitiveType.Cylinder, root.transform, new Vector3(displayX[i], 0.18f, 1.8f), new Vector3(4.2f, 0.08f, 4.2f), darkSteel, withCollider: true);
                Part($"TurntableRing_{i}", PrimitiveType.Cylinder, root.transform, new Vector3(displayX[i], 0.19f, 1.8f), new Vector3(4.35f, 0.03f, 4.35f), i == 3 ? cyanGlow : goldGlow, withCollider: false);

                GameObject spin = new GameObject($"CarSpinner_{car.Id}");
                spin.transform.SetParent(root.transform, false);
                spin.transform.localPosition = new Vector3(displayX[i], 0.26f, 1.8f);
                spin.transform.localRotation = Quaternion.Euler(0f, -25f + i * 18f, 0f);
                _turntables.Add(spin.transform);

                BuildStylizedShowroomCar(spin.transform, car, sh, glassTint, chrome, tireMat, goldGlow);
            }

            // Sales Reception Desk + Friendly Manager Kemal Bey right at Center Entry
            Part("SalesDesk", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.56f, -2.0f), new Vector3(2.8f, 1.02f, 0.95f), deskWood, withCollider: true);
            Part("SalesDeskTrim", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.98f, -2.49f), new Vector3(2.84f, 0.10f, 0.05f), cyanGlow, withCollider: false);

            Material skinM = MakeMat(sh, new Color(0.84f, 0.65f, 0.52f), 0.35f, 0f);
            Material suitM = MakeMat(sh, new Color(0.10f, 0.16f, 0.26f), 0.28f, 0.05f);
            Material pantsM = MakeMat(sh, new Color(0.11f, 0.12f, 0.15f), 0.18f, 0f);
            Material hairM = MakeMat(sh, new Color(0.18f, 0.14f, 0.12f), 0.28f, 0f);
            ProceduralHumanoid manager = ProceduralHumanoid.Build(root.transform, skinM, suitM, pantsM, tireMat, hairM, 1.80f);
            manager.transform.localPosition = new Vector3(0f, 0.14f, -1.05f);
            manager.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Interactive Dealership Trigger
            GameObject trigGo = new GameObject("OtoGaleri_Trigger");
            trigGo.transform.SetParent(root.transform, false);
            trigGo.transform.localPosition = new Vector3(0f, 1.0f, -3.1f);
            BoxCollider bc = trigGo.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(4.2f, 2.4f, 3.2f);
            InteractableTrigger trig = trigGo.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.InspectObject, DeskInteractId, "ui.btn.interact", 3.6f);
            trig.SetCustomPrompt("Hamidiye Oto Galeri · Araç Satın Al & Garaj", "Hamidiye Auto Gallery · Buy Cars & Garage");

            // Warm showroom overhead illumination (single lightweight point light)
            GameObject lgo = new GameObject("Showroom_Light");
            lgo.transform.SetParent(root.transform, false);
            lgo.transform.localPosition = new Vector3(0f, 4.2f, 0.5f);
            Light l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.96f, 0.88f);
            l.range = 24f;
            l.intensity = 4.2f;
            l.shadows = LightShadows.None;
        }

        private static void BuildStylizedShowroomCar(Transform parent, DealershipCarDef car, Shader sh, Material glass, Material chrome, Material tire, Material headGlow)
        {
            Material paint = MakeMat(sh, car.DisplayColor, 0.86f, 0.35f);
            GameObject carRoot = new GameObject("DisplayCar");
            carRoot.transform.SetParent(parent, false);
            carRoot.transform.localScale = Vector3.Scale(new Vector3(0.76f, 0.76f, 0.76f), car.DisplayScale);

            // Lower body + Cabin + Hood + Wheels
            Part("Chassis", PrimitiveType.Cube, carRoot.transform, new Vector3(0f, 0.52f, 0f), new Vector3(1.88f, 0.58f, 4.25f), paint, false);
            Part("Cabin", PrimitiveType.Cube, carRoot.transform, new Vector3(0f, 1.04f, -0.18f), new Vector3(1.62f, 0.56f, 2.18f), paint, false);
            Part("Windshield", PrimitiveType.Cube, carRoot.transform, new Vector3(0f, 1.04f, -0.18f), new Vector3(1.65f, 0.44f, 2.02f), glass, false);
            Part("Grille", PrimitiveType.Cube, carRoot.transform, new Vector3(0f, 0.48f, 2.13f), new Vector3(1.05f, 0.24f, 0.06f), chrome, false);
            Part("HeadL", PrimitiveType.Cube, carRoot.transform, new Vector3(-0.68f, 0.56f, 2.13f), new Vector3(0.32f, 0.14f, 0.06f), headGlow, false);
            Part("HeadR", PrimitiveType.Cube, carRoot.transform, new Vector3(0.68f, 0.56f, 2.13f), new Vector3(0.32f, 0.14f, 0.06f), headGlow, false);

            if (car.HasSpoiler)
            {
                Part("SpoilerWing", PrimitiveType.Cube, carRoot.transform, new Vector3(0f, 0.98f, -1.96f), new Vector3(1.72f, 0.08f, 0.32f), chrome, false);
                Part("SpoilerLegL", PrimitiveType.Cube, carRoot.transform, new Vector3(-0.58f, 0.86f, -1.94f), new Vector3(0.08f, 0.24f, 0.14f), chrome, false);
                Part("SpoilerLegR", PrimitiveType.Cube, carRoot.transform, new Vector3(0.58f, 0.86f, -1.94f), new Vector3(0.08f, 0.24f, 0.14f), chrome, false);
            }
            if (car.HasRoofRack)
            {
                Part("RackL", PrimitiveType.Cube, carRoot.transform, new Vector3(-0.56f, 1.38f, -0.18f), new Vector3(0.07f, 0.08f, 1.65f), chrome, false);
                Part("RackR", PrimitiveType.Cube, carRoot.transform, new Vector3(0.56f, 1.38f, -0.18f), new Vector3(0.07f, 0.08f, 1.65f), chrome, false);
            }

            Vector3[] wPos =
            {
                new Vector3(-0.92f, 0.32f, 1.32f), new Vector3(0.92f, 0.32f, 1.32f),
                new Vector3(-0.92f, 0.32f, -1.32f), new Vector3(0.92f, 0.32f, -1.32f)
            };
            for (int w = 0; w < wPos.Length; w++)
            {
                GameObject wh = Part($"Wheel_{w}", PrimitiveType.Cylinder, carRoot.transform, wPos[w], new Vector3(0.62f, 0.14f, 0.62f), tire, false);
                wh.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                GameObject rim = Part($"Rim_{w}", PrimitiveType.Cylinder, carRoot.transform, wPos[w], new Vector3(0.36f, 0.155f, 0.36f), chrome, false);
                rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        private static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 lp, Vector3 sc, Material mat, bool withCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lp;
            go.transform.localScale = sc;
            Collider c = go.GetComponent<Collider>();
            if (!withCollider && c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        private static Material MakeMat(Shader sh, Color col, float smooth, float metal)
        {
            Material m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            return m;
        }

        private static Material MakeEmissiveMat(Shader sh, Color col, float intensity)
        {
            Material m = MakeMat(sh, col, 0.7f, 0.1f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", col * intensity);
            }
            return m;
        }

        // =====================================================================
        // FLAGSHIP OURWORLDUIKIT DEALERSHIP & GARAGE MODAL
        // =====================================================================

        public void ShowModal()
        {
            EnsureStarterOwned();
            if (_modalRoot == null) BuildUi();
            _isOpen = true;
            _modalRoot.SetActive(true);
            PlayerModeController.Instance?.SetModalLock(this, true);
            RefreshModal();
        }

        public void CloseModal()
        {
            if (!_isOpen) return;
            _isOpen = false;
            if (_modalRoot != null) _modalRoot.SetActive(false);
            PlayerModeController.Instance?.SetModalLock(this, false);
        }

        private void BuildUi()
        {
            OurWorldUiKit.EnsureInitialized();

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 197;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            _modalRoot = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            _modalRoot.transform.SetParent(transform, false);
            RectTransform scRt = (RectTransform)_modalRoot.transform;
            scRt.anchorMin = Vector2.zero;
            scRt.anchorMax = Vector2.one;
            scRt.offsetMin = Vector2.zero;
            scRt.offsetMax = Vector2.zero;
            _modalRoot.GetComponent<Image>().color = OurWorldUiKit.Scrim;

            GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(_modalRoot.transform, false);
            RectTransform crt = (RectTransform)card.transform;
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(980f, 720f);

            OurWorldUiKit.ApplyRounded(card.GetComponent<Image>(), OurWorldUiKit.CardSprite, OurWorldUiKit.CardBg);
            OurWorldUiKit.AddCardDropShadow(card.transform, 28f, -12f, 0.58f);
            OurWorldUiKit.AddSoftTopGlow(card.transform, OurWorldUiKit.AccentTeal, 170f, 0.18f);
            OurWorldUiKit.AddHairlineBorder(card.transform, OurWorldUiKit.CardOutlineSprite, OurWorldUiKit.BorderSubtle);

            _titleText = OurWorldUiKit.CreateText("Title", card.transform, "HAMİDİYE OTO GALERİ & GARAJ",
                28, FontStyle.Bold, OurWorldUiKit.TextPrimary, TextAnchor.MiddleLeft);
            RectTransform trt = _titleText.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(34f, -24f);
            trt.sizeDelta = new Vector2(-260f, 38f);

            _subtitleText = OurWorldUiKit.CreateText("Subtitle", card.transform, "Yeni araç satın al veya garajından seçip hemen teslim al",
                16, FontStyle.Normal, OurWorldUiKit.TextSecondary, TextAnchor.MiddleLeft);
            RectTransform srt = _subtitleText.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(34f, -62f);
            srt.sizeDelta = new Vector2(-260f, 24f);

            _balanceText = OurWorldUiKit.CreateText("Balance", card.transform, "₺ 250",
                22, FontStyle.Bold, OurWorldUiKit.AccentGold, TextAnchor.MiddleRight);
            RectTransform brt = _balanceText.rectTransform;
            brt.anchorMin = new Vector2(1f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-86f, -28f);
            brt.sizeDelta = new Vector2(170f, 36f);

            Button closeBtn = OurWorldUiKit.CreateCircularIconButton(
                "Btn_Close", card.transform, OurWorldUiKit.IconClose, 44f, 18f,
                OurWorldUiKit.SurfaceBg, OurWorldUiKit.TextSecondary, OurWorldUiKit.BorderSubtle,
                () => { GameFeelAudio.PlayUiClick(); CloseModal(); });
            RectTransform clRt = (RectTransform)closeBtn.transform;
            clRt.anchorMin = new Vector2(1f, 1f);
            clRt.anchorMax = new Vector2(1f, 1f);
            clRt.pivot = new Vector2(1f, 1f);
            clRt.anchoredPosition = new Vector2(-24f, -24f);

            GameObject scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image));
            scrollGo.transform.SetParent(card.transform, false);
            RectTransform scListRt = (RectTransform)scrollGo.transform;
            scListRt.anchorMin = Vector2.zero;
            scListRt.anchorMax = Vector2.one;
            scListRt.offsetMin = new Vector2(28f, 22f);
            scListRt.offsetMax = new Vector2(-28f, -100f);
            scrollGo.GetComponent<Image>().color = Color.clear;

            ScrollRect sr = scrollGo.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 36f;

            GameObject vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            vp.transform.SetParent(scrollGo.transform, false);
            RectTransform vpRt = (RectTransform)vp.transform;
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;
            sr.viewport = vpRt;

            GameObject cont = new GameObject("Content", typeof(RectTransform));
            cont.transform.SetParent(vp.transform, false);
            _listRoot = (RectTransform)cont.transform;
            _listRoot.anchorMin = new Vector2(0f, 1f);
            _listRoot.anchorMax = new Vector2(1f, 1f);
            _listRoot.pivot = new Vector2(0.5f, 1f);
            _listRoot.anchoredPosition = Vector2.zero;
            _listRoot.sizeDelta = new Vector2(0f, 650f);
            sr.content = _listRoot;

            VerticalLayoutGroup vlg = cont.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(4, 4, 4, 14);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            ContentSizeFitter csf = cont.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void RefreshModal()
        {
            bool en = LocalizationService.IsEnglish;
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            int money = d != null ? d.money : 0;

            _titleText.text = en ? "HAMİDİYE AUTO GALLERY & GARAGE" : "HAMİDİYE OTO GALERİ & GARAJ";
            _subtitleText.text = en
                ? "Purchase vehicles permanently & deliver your active ride to the gallery bay"
                : "Kalıcı araç satın al · Aktif aracını galeri önüne anında teslim al (Vale ile her yerden çağır)";
            _balanceText.text = $"₺ {money}";

            for (int i = _listRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_listRoot.GetChild(i).gameObject);
            }

            for (int i = 0; i < Catalog.Length; i++)
            {
                DealershipCarDef car = Catalog[i];
                bool owned = car.Price == 0 || (d != null && d.ownedVehicleIds != null && d.ownedVehicleIds.Contains(car.Id));
                bool active = d != null && string.Equals(d.activeOwnedVehicleId, car.Id, StringComparison.Ordinal);

                GameObject row = new GameObject($"Car_{car.Id}", typeof(RectTransform), typeof(Image));
                row.transform.SetParent(_listRoot, false);
                row.AddComponent<LayoutElement>().preferredHeight = 112f;

                OurWorldUiKit.ApplyRounded(row.GetComponent<Image>(), OurWorldUiKit.SubCardSprite,
                    active ? OurWorldUiKit.SurfaceActive : OurWorldUiKit.SurfaceBg);
                OurWorldUiKit.AddHairlineBorder(row.transform, OurWorldUiKit.SubCardOutlineSprite,
                    active ? OurWorldUiKit.AccentTeal : OurWorldUiKit.BorderSubtle);

                // Left metallic color stripe
                GameObject sw = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
                sw.transform.SetParent(row.transform, false);
                RectTransform swRt = (RectTransform)sw.transform;
                swRt.anchorMin = new Vector2(0f, 0.5f);
                swRt.anchorMax = new Vector2(0f, 0.5f);
                swRt.pivot = new Vector2(0f, 0.5f);
                swRt.anchoredPosition = new Vector2(16f, 0f);
                swRt.sizeDelta = new Vector2(18f, 78f);
                OurWorldUiKit.ApplyRounded(sw.GetComponent<Image>(), OurWorldUiKit.PillSprite, car.DisplayColor);

                string title = en ? car.NameEn : car.NameTr;
                string spec = en ? car.SpecEn : car.SpecTr;
                string bars = en
                    ? $"Speed: {car.SpeedRating}/10   ·   Accel: {car.AccelRating}/10"
                    : $"Hız: {car.SpeedRating}/10   ·   İvme: {car.AccelRating}/10";

                Text info = OurWorldUiKit.CreateText("Info", row.transform,
                    $"<b>{title}</b>\n<size=15><color=#B8CBD9>{spec}</color></size>\n<size=14><color=#24C7B3>{bars}</color></size>",
                    21, FontStyle.Normal, OurWorldUiKit.TextPrimary, TextAnchor.MiddleLeft);
                info.supportRichText = true;
                RectTransform irt = info.rectTransform;
                irt.anchorMin = new Vector2(0f, 0f);
                irt.anchorMax = new Vector2(0.70f, 1f);
                irt.offsetMin = new Vector2(48f, 6f);
                irt.offsetMax = new Vector2(-10f, -6f);

                string btnText = active
                    ? (en ? "ACTIVE · DELIVER" : "AKTİF · GETİR")
                    : (owned ? (en ? "SELECT & DRIVE" : "SEÇ & SÜR") : (en ? $"BUY · {car.Price} ₺" : $"AL · {car.Price} ₺"));
                Color btnCol = active ? OurWorldUiKit.AccentTeal : (owned ? OurWorldUiKit.SurfaceSoft : OurWorldUiKit.AccentGold);
                Color txtCol = (active || !owned) ? OurWorldUiKit.InkDark : OurWorldUiKit.TextPrimary;

                DealershipCarDef captured = car;
                Button btn = OurWorldUiKit.CreatePillButton("Btn_Action", row.transform, btnText, 17, btnCol, txtCol,
                    owned && !active ? OurWorldUiKit.BorderSubtle : Color.clear, out _, () => HandleCarAction(captured, en));
                RectTransform brt = (RectTransform)btn.transform;
                brt.anchorMin = new Vector2(1f, 0.5f);
                brt.anchorMax = new Vector2(1f, 0.5f);
                brt.pivot = new Vector2(1f, 0.5f);
                brt.anchoredPosition = new Vector2(-20f, 0f);
                brt.sizeDelta = new Vector2(214f, 52f);
            }
        }

        private void HandleCarAction(DealershipCarDef car, bool en)
        {
            SaveManager sm = SaveManager.Instance;
            if (sm == null || sm.Data == null) return;
            SaveData d = sm.Data;
            EnsureStarterOwned();

            bool owned = car.Price == 0 || d.ownedVehicleIds.Contains(car.Id);
            if (!owned)
            {
                if (!sm.TrySpend(car.Price))
                {
                    GameFeelAudio.PlayUiClick();
                    PlayerModeController.Instance?.RaiseStatusMessage(en
                        ? $"Insufficient funds ({car.Price} ₺ needed)!"
                        : $"Yetersiz bakiye ({car.Price} ₺ gerekli)!");
                    return;
                }
                d.ownedVehicleIds.Add(car.Id);
                NeighborhoodReputation.AddReputation(45);
                GameFeelAudio.PlayCoinChime();
            }
            else
            {
                GameFeelAudio.PlayUiClick();
            }

            // Apply chosen dealership car spec permanently as the player's personal ride!
            d.activeOwnedVehicleId = car.Id;
            d.carPaintIndex = car.PaintIndex;
            d.carRimIndex = car.RimIndex;
            d.carHeadlightMode = car.HeadlightMode;
            d.carHasSpoiler = car.HasSpoiler;
            d.carHasRoofRack = car.HasRoofRack;
            d.carHasSportSuspension = car.SportSuspension;
            d.missionSedanFuel = 1.0f;
            sm.Save();

            SedanVehicleController carCtrl = PlayerModeController.Instance != null ? PlayerModeController.Instance.PlayerSedan : null;
            if (carCtrl != null)
            {
                carCtrl.ApplyTuningFromSave(d);
                carCtrl.SetFuel(1.0f);
                carCtrl.SparklePolishClean();
                carCtrl.TeleportAndReset(DeliveryBayPos, Quaternion.Euler(0f, 180f, 0f));
                GameFeelAudio.SpawnBurst(DeliveryBayPos + Vector3.up * 1.4f, car.DisplayColor, 18, 0.9f);
            }

            CloseModal();
            string carName = en ? car.NameEn : car.NameTr;
            PlayerModeController.Instance?.RaiseStatusMessage(owned
                ? (en ? $"{carName} delivered outside Hamidiye Auto Gallery!" : $"{carName} galeri önüne hazırlandı, keyifli sürüşler!")
                : (en ? $"Congratulations! Purchased {carName}! Delivered outside! (+45 XP)" : $"Hayırlı olsun! {carName} satın alındı ve galeri önüne getirildi! (+45 XP)"));
        }
    }
}
