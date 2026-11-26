using OurWorld.Core;
using OurWorld.Player;
using OurWorld.Vehicle;
using OurWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Phase 2 Mobile-Ergonomic Auto Repair & Car Modification Overlay (No:73 Oto Tamir & Modifiye):
    /// - Tab 0 (Customer Repair Service): Interactive 3-step touch mini-game controlling the real 3D hydraulic car lift!
    /// - Tab 1 (Modify My Car): Live paint swatches (8 colors), alloy rims (4 styles), Xenon/Rally headlights,
    ///   Blender GT Spoiler, Overland Roof Rack, Turbo Engine, Sport Suspension, and Extended Nitro Tank!
    /// </summary>
    public sealed class AutoGaragePanelController : MonoBehaviour
    {
        public static AutoGaragePanelController Instance { get; private set; }
        public bool IsOpen { get; private set; }

        private GameObject _canvasObj;
        private GameObject _panelRoot;
        private Font _font;
        private int _activeTab;

        private Text _titleText;
        private Text _moneyText;
        private Text _footerText;
        private Image _tabRepairBg;
        private Image _tabModBg;
        private Text _tabRepairLabel;
        private Text _tabModLabel;

        private GameObject _repairTabRoot;
        private GameObject _modTabRoot;

        // Repair Tab Widgets
        private Text _customerHeaderText;
        private Text _complaintText;
        private Text _stepProgressText;
        private Image _progressFillImg;
        private Button _repairActionBtn;
        private Text _repairActionLabel;

        // Mod Tab Widgets
        private Text _paintHeaderText;
        private Text _rimHeaderText;
        private Text _kitHeaderText;
        private readonly Image[] _paintBtns = new Image[8];
        private readonly Text[] _paintLabels = new Text[8];
        private readonly Image[] _rimBtns = new Image[4];
        private readonly Text[] _rimLabels = new Text[4];
        private Text _spoilerBtnText;
        private Text _roofRackBtnText;
        private Text _headlightBtnText;
        private Text _turboBtnText;
        private Text _suspBtnText;
        private Text _nitroBtnText;

        private static readonly Color BgDark = new Color(0.055f, 0.080f, 0.132f, 0.96f);
        private static readonly Color CardBg = new Color(0.105f, 0.145f, 0.222f, 0.96f);
        private static readonly Color AccentAmber = new Color(0.96f, 0.68f, 0.18f, 1f);
        private static readonly Color AccentTeal = new Color(0.14f, 0.74f, 0.66f, 1f);
        private static readonly Color TextLight = new Color(0.96f, 0.98f, 1f, 1f);

        private static readonly string[] PaintNamesTr = { "Turkuaz", "Ateş", "Siyah", "İnci", "Aydos", "Altın", "Mor", "Boğaz" };
        private static readonly string[] PaintNamesEn = { "Teal", "Red", "Black", "Pearl", "Green", "Gold", "Violet", "Blue" };
        private static readonly string[] RimNamesTr = { "Gümüş Jant", "Siyah Jant", "Altın Jant", "Turkuaz Jant" };
        private static readonly string[] RimNamesEn = { "Silver Rim", "Black Rim", "Gold Rim", "Teal Rim" };

        private static bool IsEn =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        public static AutoGaragePanelController EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_AutoGaragePanel");
            return go.AddComponent<AutoGaragePanelController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildUi();
        }

        private void OnDisable()
        {
            if (IsOpen && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open(int initialTab = 0)
        {
            if (_panelRoot == null) BuildUi();
            _activeTab = Mathf.Clamp(initialTab, 0, 1);
            IsOpen = true;
            _panelRoot.SetActive(true);
            if (_footerText != null) _footerText.text = string.Empty;
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, true);
            }
            if (_activeTab == 0 && AutoGarageWorkshopManager.Instance != null && AutoGarageWorkshopManager.Instance.HasActiveCustomer)
            {
                AutoGarageWorkshopManager.Instance.SetLiftRaised(true);
            }
            SelectTab(_activeTab);
            GameFeelAudio.PlayUiClick();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (_panelRoot != null) _panelRoot.SetActive(false);
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
            GameFeelAudio.PlayUiClick();
        }

        private void SelectTab(int tab)
        {
            _activeTab = tab;
            if (_repairTabRoot != null) _repairTabRoot.SetActive(tab == 0);
            if (_modTabRoot != null) _modTabRoot.SetActive(tab == 1);
            if (_tabRepairBg != null) _tabRepairBg.color = tab == 0 ? AccentAmber : OurWorldUiKit.SurfaceBg;
            if (_tabModBg != null) _tabModBg.color = tab == 1 ? AccentTeal : OurWorldUiKit.SurfaceBg;
            if (_tabRepairLabel != null) _tabRepairLabel.color = tab == 0 ? OurWorldUiKit.InkDark : TextLight;
            if (_tabModLabel != null) _tabModLabel.color = tab == 1 ? OurWorldUiKit.InkDark : TextLight;
            RefreshAll();
        }

        private void BuildUi()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 22);

            _canvasObj = new GameObject("Canvas_OurWorld_AutoGarage");
            _canvasObj.layer = LayerMask.NameToLayer("UI");
            _canvasObj.transform.SetParent(transform, false);
            Canvas c = _canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 192;

            CanvasScaler cs = _canvasObj.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;
            _canvasObj.AddComponent<GraphicRaycaster>();

            OurWorldUiKit.EnsureInitialized();
            _panelRoot = new GameObject("GarageModalRoot", typeof(RectTransform), typeof(Image));
            _panelRoot.transform.SetParent(_canvasObj.transform, false);
            RectTransform rootRt = (RectTransform)_panelRoot.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            _panelRoot.GetComponent<Image>().color = OurWorldUiKit.Scrim;

            GameObject card = CreateRect("Card", _panelRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1320f, 840f), OurWorldUiKit.CardBg, OurWorldUiKit.CardSprite);
            OurWorldUiKit.AddCardDropShadow(card.transform, spread: 28f, offsetY: -12f, alpha: 0.60f);
            OurWorldUiKit.AddSoftTopGlow(card.transform, AccentAmber, heightPx: 140f, alpha: 0.14f);
            OurWorldUiKit.AddHairlineBorder(card.transform, OurWorldUiKit.CardOutlineSprite, new Color(AccentAmber.r, AccentAmber.g, AccentAmber.b, 0.34f));

            _titleText = CreateText("Title", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -24f), new Vector2(780f, 52f), 31, FontStyle.Bold, TextLight, TextAnchor.MiddleLeft);

            GameObject moneyPill = CreateRect("MoneyPill", card.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-98f, -26f), new Vector2(190f, 44f), new Color(AccentAmber.r, AccentAmber.g, AccentAmber.b, 0.14f), OurWorldUiKit.PillSprite);
            OurWorldUiKit.AddHairlineBorder(moneyPill.transform, OurWorldUiKit.PillOutlineSprite, new Color(AccentAmber.r, AccentAmber.g, AccentAmber.b, 0.36f));
            _moneyText = OurWorldUiKit.CreateStretchText("Money", moneyPill.transform, "₺ 0", 24, FontStyle.Bold, AccentAmber, TextAnchor.MiddleCenter);

            Button closeBtn = OurWorldUiKit.CreateCircularIconButton(
                "BtnClose", card.transform, OurWorldUiKit.IconClose,
                diameter: 46f, iconSize: 18f,
                bgColor: new Color(0.14f, 0.19f, 0.27f, 0.94f),
                iconColor: OurWorldUiKit.TextSecondary,
                borderColor: OurWorldUiKit.BorderSubtle,
                onClick: Close);
            RectTransform closeRt = (RectTransform)closeBtn.transform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-32f, -24f);

            // Top Tab Bar
            Button tab0 = CreateButton("TabRepair", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -94f), new Vector2(614f, 58f), AccentAmber, "", 22, out _tabRepairLabel);
            _tabRepairBg = tab0.GetComponent<Image>();
            tab0.onClick.AddListener(() => { GameFeelAudio.PlayUiClick(); SelectTab(0); });

            Button tab1 = CreateButton("TabMod", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(670f, -94f), new Vector2(614f, 58f), CardBg, "", 22, out _tabModLabel);
            _tabModBg = tab1.GetComponent<Image>();
            tab1.onClick.AddListener(() => { GameFeelAudio.PlayUiClick(); SelectTab(1); });

            // --- TAB 0: CUSTOMER REPAIR MINI-GAME ---
            _repairTabRoot = new GameObject("RepairTabRoot", typeof(RectTransform));
            _repairTabRoot.transform.SetParent(card.transform, false);
            RectTransform rrt = (RectTransform)_repairTabRoot.transform;
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = new Vector2(36f, 84f);
            rrt.offsetMax = new Vector2(-36f, -172f);

            GameObject infoBox = CreateRect("CustomerInfoBox", _repairTabRoot.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 198f), OurWorldUiKit.SurfaceBg, OurWorldUiKit.SubCardSprite);
            OurWorldUiKit.AddHairlineBorder(infoBox.transform, OurWorldUiKit.SubCardOutlineSprite, OurWorldUiKit.BorderSubtle);
            ((RectTransform)infoBox.transform).pivot = new Vector2(0.5f, 1f);
            _customerHeaderText = CreateText("CustHeader", infoBox.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -16f), new Vector2(-44f, 48f), 27, FontStyle.Bold, AccentAmber, TextAnchor.MiddleLeft);
            ((RectTransform)_customerHeaderText.transform).pivot = new Vector2(0.5f, 1f);
            _complaintText = CreateText("CustComplaint", infoBox.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -72f), new Vector2(-44f, 108f), 23, FontStyle.Normal, TextLight, TextAnchor.UpperLeft);
            ((RectTransform)_complaintText.transform).pivot = new Vector2(0.5f, 1f);
            _complaintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _complaintText.lineSpacing = 1.25f;

            _stepProgressText = CreateText("StepProgress", _repairTabRoot.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -222f), new Vector2(0f, 44f), 24, FontStyle.Bold, AccentTeal, TextAnchor.MiddleLeft);
            ((RectTransform)_stepProgressText.transform).pivot = new Vector2(0.5f, 1f);

            GameObject barBg = CreateRect("BarBg", _repairTabRoot.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -278f), new Vector2(0f, 34f), OurWorldUiKit.SurfaceBg, OurWorldUiKit.PillSprite);
            OurWorldUiKit.AddHairlineBorder(barBg.transform, OurWorldUiKit.PillOutlineSprite, OurWorldUiKit.BorderSubtle);
            ((RectTransform)barBg.transform).pivot = new Vector2(0.5f, 1f);
            GameObject barFill = CreateRect("BarFill", barBg.transform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                Vector2.zero, Vector2.zero, AccentTeal, OurWorldUiKit.PillSprite);
            _progressFillImg = barFill.GetComponent<Image>();

            _repairActionBtn = CreateButton("BtnRepairStep", _repairTabRoot.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 48f), new Vector2(780f, 140f), AccentAmber, "", 27, out _repairActionLabel);
            _repairActionLabel.color = OurWorldUiKit.InkDark;
            ((RectTransform)_repairActionBtn.transform).pivot = new Vector2(0.5f, 0f);
            _repairActionBtn.onClick.AddListener(HandleRepairStepClicked);

            // --- TAB 1: OWN CAR MODIFICATION & TUNING ---
            _modTabRoot = new GameObject("ModTabRoot", typeof(RectTransform));
            _modTabRoot.transform.SetParent(card.transform, false);
            RectTransform mrt = (RectTransform)_modTabRoot.transform;
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.offsetMin = new Vector2(36f, 84f);
            mrt.offsetMax = new Vector2(-36f, -172f);

            _paintHeaderText = CreateText("PaintHeader", _modTabRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -4f), new Vector2(1200f, 32f), 20, FontStyle.Bold, AccentAmber, TextAnchor.MiddleLeft);

            for (int i = 0; i < 8; i++)
            {
                int idx = i;
                Color swCol = SedanVehicleController.TuningPaintColors[i];
                Button pb = CreateButton($"Paint_{i}", _modTabRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(i * 157f, -42f), new Vector2(146f, 84f), swCol, PaintNamesTr[i], 18, out Text pLbl);
                pLbl.color = (i == 3 || i == 5) ? Color.black : Color.white;
                _paintBtns[i] = pb.GetComponent<Image>();
                _paintLabels[i] = pLbl;
                pb.onClick.AddListener(() => BuyPaint(idx));
            }

            _rimHeaderText = CreateText("RimHeader", _modTabRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -148f), new Vector2(1200f, 32f), 20, FontStyle.Bold, AccentTeal, TextAnchor.MiddleLeft);

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                Button rb = CreateButton($"Rim_{i}", _modTabRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(i * 314f, -186f), new Vector2(300f, 74f), CardBg, RimNamesTr[i], 20, out Text rLbl);
                _rimBtns[i] = rb.GetComponent<Image>();
                _rimLabels[i] = rLbl;
                rb.onClick.AddListener(() => BuyRim(idx));
            }

            _kitHeaderText = CreateText("KitHeader", _modTabRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -284f), new Vector2(1200f, 32f), 20, FontStyle.Bold, AccentAmber, TextAnchor.MiddleLeft);

            Button bSpoil = CreateButton("BtnSpoiler", _modTabRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -324f), new Vector2(404f, 118f), CardBg, "", 20, out _spoilerBtnText);
            bSpoil.onClick.AddListener(BuySpoiler);

            Button bRack = CreateButton("BtnRoofRack", _modTabRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(422f, -324f), new Vector2(404f, 118f), CardBg, "", 20, out _roofRackBtnText);
            bRack.onClick.AddListener(BuyRoofRack);

            Button bHead = CreateButton("BtnHeadlight", _modTabRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(844f, -324f), new Vector2(404f, 118f), CardBg, "", 20, out _headlightBtnText);
            bHead.onClick.AddListener(CycleHeadlightKit);

            Button bTurbo = CreateButton("BtnTurbo", _modTabRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -456f), new Vector2(404f, 118f), CardBg, "", 20, out _turboBtnText);
            bTurbo.onClick.AddListener(BuyTurbo);

            Button bSusp = CreateButton("BtnSusp", _modTabRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(422f, -456f), new Vector2(404f, 118f), CardBg, "", 20, out _suspBtnText);
            bSusp.onClick.AddListener(BuySuspension);

            Button bNitro = CreateButton("BtnNitro", _modTabRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(844f, -456f), new Vector2(404f, 118f), CardBg, "", 20, out _nitroBtnText);
            bNitro.onClick.AddListener(BuyNitro);

            _footerText = CreateText("Footer", card.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 20f), new Vector2(-72f, 44f), 21, FontStyle.Bold, AccentAmber, TextAnchor.MiddleCenter);
            ((RectTransform)_footerText.transform).pivot = new Vector2(0.5f, 0f);

            _panelRoot.SetActive(false);
        }

        private void HandleRepairStepClicked()
        {
            if (AutoGarageWorkshopManager.Instance == null) return;
            AutoGarageWorkshopManager.Instance.AdvanceRepairStep(out _, out string msg);
            if (_footerText != null) _footerText.text = msg;
            RefreshAll();
        }

        private void BuyPaint(int idx)
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return;
            if (d.carPaintIndex == idx) return;
            if (!SaveManager.Instance.TrySpend(65)) { SetFooter(IsEn ? "Need 65 ₺ for custom paint." : "Boya için 65 ₺ gerekiyor."); return; }
            d.carPaintIndex = idx;
            SaveManager.Instance.Save();
            ApplyTuningLive();
            PoliceWantedSystem.Instance?.ClearWantedFromRespray();
            SetFooter(IsEn ? $"Painted your Sedan {PaintNamesEn[idx]}!" : $"Aracın {PaintNamesTr[idx]} metalik boyayla boyandı!");
            RefreshAll();
        }

        private void BuyRim(int idx)
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return;
            if (d.carRimIndex == idx) return;
            if (!SaveManager.Instance.TrySpend(55)) { SetFooter(IsEn ? "Need 55 ₺ for alloy rims." : "Jant seti için 55 ₺ gerekiyor."); return; }
            d.carRimIndex = idx;
            SaveManager.Instance.Save();
            ApplyTuningLive();
            SetFooter(IsEn ? $"Installed {RimNamesEn[idx]}!" : $"{RimNamesTr[idx]} takıldı!");
            RefreshAll();
        }

        private void BuySpoiler()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return;
            if (d.carHasSpoiler) { SetFooter(IsEn ? "GT Spoiler already installed!" : "GT Rüzgârlık zaten takılı!"); return; }
            if (!SaveManager.Instance.TrySpend(180)) { SetFooter(IsEn ? "Need 180 ₺ for GT Spoiler." : "GT Rüzgârlık için 180 ₺ gerekiyor."); return; }
            d.carHasSpoiler = true;
            SaveManager.Instance.Save();
            ApplyTuningLive();
            SetFooter(IsEn ? "GT Carbon Rear Spoiler mounted on your Sedan!" : "GT Karbon Rüzgârlık aracına monte edildi!");
            RefreshAll();
        }

        private void BuyRoofRack()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return;
            if (d.carHasRoofRack) { SetFooter(IsEn ? "Roof Rack already installed!" : "Portbagaj zaten takılı!"); return; }
            if (!SaveManager.Instance.TrySpend(160)) { SetFooter(IsEn ? "Need 160 ₺ for Roof Rack." : "Tavan Portbagajı için 160 ₺ gerekiyor."); return; }
            d.carHasRoofRack = true;
            SaveManager.Instance.Save();
            ApplyTuningLive();
            SetFooter(IsEn ? "Overland Roof Rack & Camp Box installed!" : "Tavan Portbagajı & Kamp Kutusu monte edildi!");
            RefreshAll();
        }

        private void CycleHeadlightKit()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return;
            int cost = d.carHeadlightMode == 0 ? 95 : 0;
            if (cost > 0 && !SaveManager.Instance.TrySpend(cost))
            {
                SetFooter(IsEn ? "Need 95 ₺ to unlock Xenon/Rally Headlights." : "Xenon/Ralli Far kiti için 95 ₺ gerekiyor.");
                return;
            }
            d.carHeadlightMode = (d.carHeadlightMode + 1) % 3;
            SaveManager.Instance.Save();
            ApplyTuningLive();
            string mode = d.carHeadlightMode == 1 ? "Xenon Buz Mavisi" : (d.carHeadlightMode == 2 ? "Ralli Altın Sarısı" : "Klasik Sıcak");
            SetFooter(IsEn ? "Updated Headlight Kit!" : $"Far Kiti güncellendi: {mode}!");
            RefreshAll();
        }

        private void BuyTurbo()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null || d.carHasTurboUpgrade) return;
            if (!SaveManager.Instance.TrySpend(320)) { SetFooter(IsEn ? "Need 320 ₺ for Turbo Engine." : "Turbo Motor için 320 ₺ gerekiyor."); return; }
            d.carHasTurboUpgrade = true;
            SaveManager.Instance.Save();
            ApplyTuningLive();
            SetFooter(IsEn ? "Turbo Engine installed! (+24% Torque)" : "Turbo Motor takıldı! (+%24 Tork & Hız)");
            RefreshAll();
        }

        private void BuySuspension()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null || d.carHasSportSuspension) return;
            if (!SaveManager.Instance.TrySpend(260)) { SetFooter(IsEn ? "Need 260 ₺ for Sport Suspension." : "Spor Süspansiyon için 260 ₺ gerekiyor."); return; }
            d.carHasSportSuspension = true;
            SaveManager.Instance.Save();
            ApplyTuningLive();
            SetFooter(IsEn ? "Sport Suspension & Brakes installed!" : "Spor Süspansiyon & Güçlü Frenler takıldı!");
            RefreshAll();
        }

        private void BuyNitro()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null || d.carHasNitroUpgrade) return;
            if (!SaveManager.Instance.TrySpend(220)) { SetFooter(IsEn ? "Need 220 ₺ for Nitro Tank." : "Nitro Tankı için 220 ₺ gerekiyor."); return; }
            d.carHasNitroUpgrade = true;
            SaveManager.Instance.Save();
            ApplyTuningLive();
            SetFooter(IsEn ? "Extended Nitro System installed!" : "Genişletilmiş Nitro Sistemi kuruldu!");
            RefreshAll();
        }

        private static void ApplyTuningLive()
        {
            GameFeelAudio.PlayCoinChime();
            if (PlayerModeController.Instance != null && PlayerModeController.Instance.PlayerSedan != null && SaveManager.Instance != null)
            {
                SedanVehicleController sedan = PlayerModeController.Instance.PlayerSedan;
                sedan.ApplyTuningFromSave(SaveManager.Instance.Data);
                GameFeelAudio.SpawnBurst(sedan.transform.position + Vector3.up * 1.4f, AccentTeal, count: 16, spread: 0.9f);
            }
        }

        private void SetFooter(string msg)
        {
            if (_footerText != null) _footerText.text = msg ?? string.Empty;
        }

        private void RefreshAll()
        {
            if (_panelRoot == null) return;
            bool en = IsEn;
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;

            _titleText.text = en ? "NO:73 AUTO REPAIR & TUNING GARAGE" : "NO:73 OTO TAMİR & MODİFİYE GARAJI";
            _moneyText.text = $"₺ {(d != null ? d.money : 0)}";
            _tabRepairLabel.text = en ? "CUSTOMER REPAIR SERVICE" : "MÜŞTERİ TAMİR SERVİSİ";
            _tabModLabel.text = en ? "MODIFY & TUNE MY CAR" : "KENDİ ARACINI MODİFİYE ET";

            // Refresh Repair Tab
            AutoGarageWorkshopManager ws = AutoGarageWorkshopManager.Instance;
            if (ws != null && ws.HasActiveCustomer)
            {
                CustomerRepairOrder ord = ws.ActiveOrder;
                int step = ws.CurrentStepIndex;
                int total = ord.StepLabelsTr.Length;
                _customerHeaderText.text = en
                    ? $"Customer: {ord.CustomerName}  •  {ord.CarNameEn}  •  Reward: +{ord.RewardTl} ₺"
                    : $"Müşteri: {ord.CustomerName}  •  {ord.CarNameTr}  •  Ödül: +{ord.RewardTl} ₺";
                _complaintText.text = en ? $"\"{ord.ComplaintEn}\"" : $"\"{ord.ComplaintTr}\"";
                _stepProgressText.text = en ? $"Repair Progress: {step} / {total}" : $"Tamir İlerlemesi: {step} / {total}";

                RectTransform fRt = _progressFillImg.rectTransform;
                fRt.anchorMin = Vector2.zero;
                fRt.anchorMax = new Vector2(Mathf.Clamp01(step / (float)total), 1f);
                fRt.offsetMin = Vector2.zero;
                fRt.offsetMax = Vector2.zero;

                string nextLabel = step < total ? (en ? ord.StepLabelsEn[step] : ord.StepLabelsTr[step]) : "";
                _repairActionLabel.text = nextLabel;
                _repairActionBtn.interactable = true;
            }
            else
            {
                _customerHeaderText.text = en ? "Lift Bay Clear — Great Work!" : "Lift Boş — Harika İş Çıkardın!";
                _complaintText.text = en
                    ? "The last customer drove away thrilled! A new neighbor car will pull into No:73 in a few seconds."
                    : "Son müşteri çok memnun ayrıldı! Birkaç saniye içinde yeni bir komşu aracı No:73 liftine yanaşacak.";
                _stepProgressText.text = en ? $"Total Cars Repaired: {(d != null ? d.totalCarsRepaired : 0)}" : $"Toplam Onarılan Araç: {(d != null ? d.totalCarsRepaired : 0)}";
                _progressFillImg.rectTransform.anchorMax = new Vector2(1f, 1f);
                _repairActionLabel.text = en ? "WAITING FOR NEXT CUSTOMER..." : "YENİ MÜŞTERİ BEKLENİYOR...";
                _repairActionBtn.interactable = false;
            }

            // Refresh Mod Tab
            if (_paintHeaderText != null)
                _paintHeaderText.text = en ? "1. METALLIC CAR PAINT (65 ₺)" : "1. METALİK ARAÇ BOYASI (65 ₺)";
            if (_rimHeaderText != null)
                _rimHeaderText.text = en ? "2. ALLOY RIM SET (55 ₺)" : "2. ALAŞIM JANT SETİ (55 ₺)";
            if (_kitHeaderText != null)
                _kitHeaderText.text = en ? "3. AERO KITS, HEADLIGHTS & PERFORMANCE UPGRADES" : "3. AERO KİTLER, FAR & PERFORMANS YÜKSELTMELERİ";
            for (int i = 0; i < _paintLabels.Length; i++)
            {
                if (_paintLabels[i] != null) _paintLabels[i].text = en ? PaintNamesEn[i] : PaintNamesTr[i];
            }
            for (int i = 0; i < _rimLabels.Length; i++)
            {
                if (_rimLabels[i] != null) _rimLabels[i].text = en ? RimNamesEn[i] : RimNamesTr[i];
            }

            if (d != null)
            {
                for (int i = 0; i < _rimBtns.Length; i++)
                {
                    _rimBtns[i].color = d.carRimIndex == i ? new Color(0.10f, 0.40f, 0.38f, 0.98f) : CardBg;
                }
                _spoilerBtnText.text = d.carHasSpoiler
                    ? (en ? "GT CARBON SPOILER\nINSTALLED ✓" : "GT KARBON RÜZGÂRLIK\nTAKILI ✓")
                    : (en ? "GT CARBON SPOILER\n180 ₺  •  Sport Aero" : "GT KARBON RÜZGÂRLIK\n180 ₺  •  Spor Kanat");
                _roofRackBtnText.text = d.carHasRoofRack
                    ? (en ? "OVERLAND ROOF RACK\nINSTALLED ✓" : "TAVAN PORTBAGAJI\nTAKILI ✓")
                    : (en ? "OVERLAND ROOF RACK\n160 ₺  •  Camp Box" : "TAVAN PORTBAGAJI\n160 ₺  •  Kamp Kutusu");
                string hlState = d.carHeadlightMode == 0 ? (en ? "Warm (95 ₺)" : "Standart (95 ₺)")
                    : (d.carHeadlightMode == 1 ? (en ? "Ice Xenon ✓ (Tap)" : "Buz Xenon ✓ (Değiş)") : (en ? "Gold Rally ✓ (Tap)" : "Altın Ralli ✓ (Değiş)"));
                _headlightBtnText.text = (en ? "HEADLIGHT KIT\n" : "FAR KİTİ\n") + hlState;
                _turboBtnText.text = d.carHasTurboUpgrade
                    ? (en ? "TURBO ENGINE\nACTIVE (+24%) ✓" : "TURBO MOTOR\nAKTİF (+%24) ✓")
                    : (en ? "TURBO ENGINE\n320 ₺  •  +24% Power" : "TURBO MOTOR\n320 ₺  •  +%24 Güç");
                _suspBtnText.text = d.carHasSportSuspension
                    ? (en ? "SPORT SUSPENSION\nACTIVE ✓" : "SPOR SÜSPANSİYON\nAKTİF ✓")
                    : (en ? "SPORT SUSPENSION\n260 ₺  •  Grip & Brakes" : "SPOR SÜSPANSİYON\n260 ₺  •  Yol Tutuş");
                _nitroBtnText.text = d.carHasNitroUpgrade
                    ? (en ? "NITRO TANK PRO\nACTIVE ✓" : "NİTRO TANKI PRO\nAKTİF ✓")
                    : (en ? "NITRO TANK PRO\n220 ₺  •  Boost Ready" : "NİTRO TANKI PRO\n220 ₺  •  Güçlü Nitro");
            }
        }

        private GameObject CreateRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color col, Sprite sprite = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(aMin.x, aMax.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = go.GetComponent<Image>();
            if (sprite != null) OurWorldUiKit.ApplyRounded(img, sprite, col);
            else img.color = col;
            return go;
        }

        private Text CreateText(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, int fs, FontStyle style, Color col, TextAnchor align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(aMin.x, aMax.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Text t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = fs;
            t.fontStyle = style;
            t.color = col;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        private Button CreateButton(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color col, string label, int fs, out Text outText)
        {
            GameObject go = CreateRect(name, parent, aMin, aMax, pos, size, col, OurWorldUiKit.SubCardSprite);
            OurWorldUiKit.AddHairlineBorder(go.transform, OurWorldUiKit.SubCardOutlineSprite, OurWorldUiKit.BorderSubtle);
            Image bg = go.GetComponent<Image>();
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            UiButtonFeedback fb = go.AddComponent<UiButtonFeedback>();
            fb.Configure(btn, bg);

            GameObject lo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lo.transform.SetParent(go.transform, false);
            RectTransform lrt = (RectTransform)lo.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10f, 8f);
            lrt.offsetMax = new Vector2(-10f, -8f);
            Text txt = lo.GetComponent<Text>();
            txt.font = _font;
            txt.fontSize = fs;
            txt.fontStyle = FontStyle.Bold;
            txt.color = TextLight;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.lineSpacing = 1.18f;
            txt.text = label;
            txt.raycastTarget = false;
            outText = txt;
            return btn;
        }
    }
}
