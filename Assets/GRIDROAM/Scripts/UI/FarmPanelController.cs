using System;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Sleek mobile-ergonomic Farm & Bostan management overlay (Istanbul Night Slate + Turquoise + Golden Amber):
    /// - Switch active planting seed (Tomato, Pepper, Strawberry, Sunflower) with clear cost & profit tags
    /// - Inspect live Farm Inventory (crops + Eggs, Milk, Wool) and Sell All in one tap
    /// - Purchase farm upgrades (Extra Chickens, Sheep, Cows, and Auto-Sprinkler watering system)
    /// </summary>
    public sealed class FarmPanelController : MonoBehaviour
    {
        public static FarmPanelController Instance { get; private set; }
        public bool IsOpen { get; private set; }

        private GameObject _canvasObj;
        private GameObject _panelRoot;
        private Font _font;

        private Text _titleText;
        private Text _moneyText;
        private Text _seedHeaderText;
        private Text _invHeaderText;
        private Text _invSummaryText;
        private Text _sellBtnText;
        private Text _upgradeHeaderText;
        private Text _statusFooterText;

        private readonly Image[] _seedBtnBgs = new Image[4];
        private readonly Text[] _seedBtnLabels = new Text[4];

        private Text _chickenBtnText;
        private Text _sheepBtnText;
        private Text _cowBtnText;
        private Text _sprinklerBtnText;

        private static readonly Color BgDark = new Color(0.055f, 0.084f, 0.138f, 0.96f);
        private static readonly Color CardBg = new Color(0.102f, 0.148f, 0.228f, 0.95f);
        private static readonly Color CardActive = new Color(0.095f, 0.36f, 0.34f, 0.98f);
        private static readonly Color AccentTeal = new Color(0.14f, 0.74f, 0.66f, 1f);
        private static readonly Color AccentGold = new Color(0.98f, 0.78f, 0.28f, 1f);
        private static readonly Color TextLight = new Color(0.96f, 0.98f, 1f, 1f);
        private static readonly Color TextMuted = new Color(0.75f, 0.83f, 0.92f, 1f);

        private static bool IsEn =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        public static FarmPanelController EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_FarmPanelController");
            return go.AddComponent<FarmPanelController>();
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

        private void OnEnable()
        {
            FarmEconomyManager.OnFarmDataChanged += RefreshAll;
        }

        private void OnDisable()
        {
            FarmEconomyManager.OnFarmDataChanged -= RefreshAll;
            if (IsOpen && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            if (_panelRoot == null) BuildUi();
            IsOpen = true;
            _panelRoot.SetActive(true);
            if (_statusFooterText != null) _statusFooterText.text = string.Empty;
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, true);
            }
            RefreshAll();
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

        private void BuildUi()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 22);

            _canvasObj = new GameObject("Canvas_OurWorld_FarmPanel");
            _canvasObj.layer = LayerMask.NameToLayer("UI");
            _canvasObj.transform.SetParent(transform, false);
            Canvas c = _canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 190;

            CanvasScaler cs = _canvasObj.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;
            _canvasObj.AddComponent<GraphicRaycaster>();

            // Full-screen dim scrim
            _panelRoot = new GameObject("FarmModalRoot", typeof(RectTransform), typeof(Image));
            _panelRoot.transform.SetParent(_canvasObj.transform, false);
            RectTransform rootRt = (RectTransform)_panelRoot.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            _panelRoot.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.68f);

            // Centered spacious mobile card
            GameObject card = CreateRect("Card", _panelRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1280f, 820f), BgDark);
            Outline cardOut = card.AddComponent<Outline>();
            cardOut.effectColor = new Color(0.20f, 0.80f, 0.72f, 0.55f);
            cardOut.effectDistance = new Vector2(2f, -2f);

            // Top bar: Title + Money + Close X
            _titleText = CreateText("Title", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -28f), new Vector2(760f, 54f), 34, FontStyle.Bold, TextLight, TextAnchor.MiddleLeft);
            _moneyText = CreateText("Money", card.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-126f, -28f), new Vector2(280f, 54f), 30, FontStyle.Bold, AccentGold, TextAnchor.MiddleRight);

            Button closeBtn = CreateButton("BtnClose", card.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-32f, -24f), new Vector2(68f, 60f), new Color(0.76f, 0.24f, 0.24f, 0.95f), "X", 28, out _);
            closeBtn.onClick.AddListener(Close);

            // --- SECTION 1: SEED SELECTOR ---
            _seedHeaderText = CreateText("SeedHeader", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -104f), new Vector2(1200f, 36f), 22, FontStyle.Bold, AccentTeal, TextAnchor.MiddleLeft);

            for (int i = 0; i < 4; i++)
            {
                int seedIdx = i;
                float bx = 36f + i * 304f;
                Button b = CreateButton($"SeedBtn_{i}", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(bx, -148f), new Vector2(288f, 124f), CardBg, "", 20, out Text lbl);
                _seedBtnBgs[i] = b.GetComponent<Image>();
                _seedBtnLabels[i] = lbl;
                b.onClick.AddListener(() =>
                {
                    FarmEconomyManager.SelectedSeedIndex = seedIdx;
                    GameFeelAudio.PlayUiClick();
                    SetFooter(IsEn
                        ? $"Selected seed: {FarmEconomyManager.SelectedCropSpec.NameEn}"
                        : $"Seçilen tohum: {FarmEconomyManager.SelectedCropSpec.NameTr}");
                });
            }

            // --- SECTION 2: HARVEST & LIVESTOCK INVENTORY + SELL ALL ---
            _invHeaderText = CreateText("InvHeader", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -296f), new Vector2(1200f, 36f), 22, FontStyle.Bold, AccentGold, TextAnchor.MiddleLeft);

            GameObject invBox = CreateRect("InvBox", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -340f), new Vector2(850f, 152f), CardBg);
            _invSummaryText = CreateText("InvSummary", invBox.transform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, 21, FontStyle.Normal, TextLight, TextAnchor.MiddleLeft);
            RectTransform irt = _invSummaryText.rectTransform;
            irt.offsetMin = new Vector2(22f, 10f);
            irt.offsetMax = new Vector2(-22f, -10f);

            Button sellBtn = CreateButton("BtnSellAll", card.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-36f, -340f), new Vector2(336f, 152f), AccentTeal, "", 24, out _sellBtnText);
            sellBtn.onClick.AddListener(() =>
            {
                Vector3 pos = PlayerModeController.Instance != null ? PlayerModeController.Instance.ActiveWorldPosition : Vector3.zero;
                int earned = FarmEconomyManager.SellAllProduce(pos);
                if (earned > 0)
                {
                    SetFooter(IsEn ? $"Sold all farm harvest for +{earned} ₺!" : $"Tüm çiftlik ürünleri satıldı! (+{earned} ₺)");
                }
                else
                {
                    SetFooter(IsEn ? "Harvest crops or collect eggs, milk & wool first." : "Önce hasat yap veya yumurta/süt/yün topla.");
                }
            });

            // --- SECTION 3: FARM EXPANSION & LIVESTOCK UPGRADES ---
            _upgradeHeaderText = CreateText("UpgHeader", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -518f), new Vector2(1200f, 36f), 22, FontStyle.Bold, AccentTeal, TextAnchor.MiddleLeft);

            Button bChick = CreateButton("BtnBuyChicken", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -562f), new Vector2(288f, 148f), CardBg, "", 20, out _chickenBtnText);
            bChick.onClick.AddListener(() =>
            {
                FarmEconomyManager.TryBuyExtraAnimal(FarmAnimalKind.Chicken, out string msg);
                SetFooter(msg);
            });

            Button bSheep = CreateButton("BtnBuySheep", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(340f, -562f), new Vector2(288f, 148f), CardBg, "", 20, out _sheepBtnText);
            bSheep.onClick.AddListener(() =>
            {
                FarmEconomyManager.TryBuyExtraAnimal(FarmAnimalKind.Sheep, out string msg);
                SetFooter(msg);
            });

            Button bCow = CreateButton("BtnBuyCow", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(644f, -562f), new Vector2(288f, 148f), CardBg, "", 20, out _cowBtnText);
            bCow.onClick.AddListener(() =>
            {
                FarmEconomyManager.TryBuyExtraAnimal(FarmAnimalKind.Cow, out string msg);
                SetFooter(msg);
            });

            Button bSprink = CreateButton("BtnBuySprinkler", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(948f, -562f), new Vector2(296f, 148f), CardBg, "", 20, out _sprinklerBtnText);
            bSprink.onClick.AddListener(() =>
            {
                FarmEconomyManager.TryBuyAutoSprinkler(out string msg);
                SetFooter(msg);
            });

            _statusFooterText = CreateText("FooterStatus", card.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 24f), new Vector2(-72f, 44f), 21, FontStyle.Bold, AccentGold, TextAnchor.MiddleCenter);
            RectTransform frt = _statusFooterText.rectTransform;
            frt.pivot = new Vector2(0.5f, 0f);
            frt.anchoredPosition = new Vector2(0f, 22f);

            _panelRoot.SetActive(false);
        }

        private void SetFooter(string msg)
        {
            if (_statusFooterText != null) _statusFooterText.text = msg ?? string.Empty;
        }

        private void RefreshAll()
        {
            if (_panelRoot == null) return;
            bool en = IsEn;
            SaveData d = FarmEconomyManager.Data;
            int money = d != null ? d.money : 0;

            _titleText.text = en ? "AYDOS FARM & GARDEN HUB" : "AYDOS ÇİFTLİĞİ & BOSTAN YÖNETİMİ";
            _moneyText.text = $"₺ {money}";
            _seedHeaderText.text = en ? "1. SELECT ACTIVE SEED FOR PLANTING" : "1. EKİM İÇİN AKTİF TOHUM SEÇ";
            _invHeaderText.text = en ? "2. FRESH HARVEST & DAIRY BASKET" : "2. BOSTAN & MANDIRA SEPETİ";
            _upgradeHeaderText.text = en ? "3. GROW YOUR FARM & LIVESTOCK" : "3. ÇİFTLİĞİNİ & HAYVANLARINI BÜYÜT";

            int sel = FarmEconomyManager.SelectedSeedIndex;
            for (int i = 0; i < FarmEconomyManager.Crops.Length; i++)
            {
                FarmCropSpec c = FarmEconomyManager.Crops[i];
                bool active = i == sel;
                _seedBtnBgs[i].color = active ? CardActive : CardBg;
                string nm = en ? c.NameEn : c.NameTr;
                string badge = active ? (en ? " [SELECTED]" : " [SEÇİLİ]") : "";
                _seedBtnLabels[i].text = en
                    ? $"{nm}{badge}\nSeed: {c.SeedCost} ₺  •  Sell: {c.SellPrice} ₺\nGrows in {c.GrowSeconds:0}s"
                    : $"{nm}{badge}\nTohum: {c.SeedCost} ₺  •  Satış: {c.SellPrice} ₺\nBüyüme: {c.GrowSeconds:0} sn";
            }

            if (d != null)
            {
                string fishPart = d.invFishCount > 0
                    ? (en ? $"   •   Lake Fish: {d.invFishCount} ({d.invFishTotalValue} ₺)" : $"   •   Gölet Balığı: {d.invFishCount} ({d.invFishTotalValue} ₺)")
                    : string.Empty;
                _invSummaryText.text = en
                    ? $"Tomatoes: {d.invTomatoes}   •   Peppers: {d.invPeppers}   •   Strawberries: {d.invStrawberries}   •   Sunflowers: {d.invSunflowers}\n" +
                      $"Eggs: {d.invEggs} ({FarmEconomyManager.EggSellPrice} ₺)   •   Milk: {d.invMilk} ({FarmEconomyManager.MilkSellPrice} ₺)   •   Wool: {d.invWool} ({FarmEconomyManager.WoolSellPrice} ₺){fishPart}"
                    : $"Domates: {d.invTomatoes}   •   Biber: {d.invPeppers}   •   Çilek: {d.invStrawberries}   •   Ayçiçeği: {d.invSunflowers}\n" +
                      $"Yumurta: {d.invEggs} ({FarmEconomyManager.EggSellPrice} ₺)   •   Süt: {d.invMilk} ({FarmEconomyManager.MilkSellPrice} ₺)   •   Yün: {d.invWool} ({FarmEconomyManager.WoolSellPrice} ₺){fishPart}";

                int count = FarmEconomyManager.GetTotalProduceCount();
                int estVal = Mathf.RoundToInt(FarmEconomyManager.GetTotalProduceValue() * NeighborhoodReputation.GetJobRewardMultiplier());
                _sellBtnText.text = count > 0
                    ? (en ? $"SELL ALL ({count})\n+{estVal} ₺" : $"HEPSİNİ SAT ({count})\n+{estVal} ₺")
                    : (en ? "BASKET EMPTY\n0 ₺" : "SEPET BOŞ\n0 ₺");

                _chickenBtnText.text = d.extraChickens >= FarmEconomyManager.MaxExtraChickens
                    ? (en ? $"CHICKENS MAXED\n({5 + d.extraChickens} Hens)" : $"TAVUK MAKSİMUM\n({5 + d.extraChickens} Tavuk)")
                    : (en ? $"+1 CHICKEN\n{FarmEconomyManager.ExtraChickenCost} ₺\n({5 + d.extraChickens} active)"
                          : $"+1 TAVUK AL\n{FarmEconomyManager.ExtraChickenCost} ₺\n({5 + d.extraChickens} tavuk)");

                _sheepBtnText.text = d.extraSheep >= FarmEconomyManager.MaxExtraSheep
                    ? (en ? $"SHEEP MAXED\n({4 + d.extraSheep} Sheep)" : $"KOYUN MAKSİMUM\n({4 + d.extraSheep} Koyun)")
                    : (en ? $"+1 SHEEP\n{FarmEconomyManager.ExtraSheepCost} ₺\n({4 + d.extraSheep} active)"
                          : $"+1 KOYUN AL\n{FarmEconomyManager.ExtraSheepCost} ₺\n({4 + d.extraSheep} koyun)");

                _cowBtnText.text = d.extraCows >= FarmEconomyManager.MaxExtraCows
                    ? (en ? $"COWS MAXED\n({3 + d.extraCows} Cows)" : $"İNEK MAKSİMUM\n({3 + d.extraCows} İnek)")
                    : (en ? $"+1 COW\n{FarmEconomyManager.ExtraCowCost} ₺\n({3 + d.extraCows} active)"
                          : $"+1 İNEK AL\n{FarmEconomyManager.ExtraCowCost} ₺\n({3 + d.extraCows} inek)");

                _sprinklerBtnText.text = d.hasAutoSprinkler
                    ? (en ? "AUTO-SPRINKLERS\nACTIVE ✓" : "OTOMATİK FISKİYE\nAKTİF ✓")
                    : (en ? $"AUTO-SPRINKLER\n{FarmEconomyManager.AutoSprinklerCost} ₺\nAuto-waters all beds"
                          : $"OTOMATİK FISKİYE\n{FarmEconomyManager.AutoSprinklerCost} ₺\nTüm tarlaları sular");
            }
        }

        private GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color col)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = col;
            return go;
        }

        private Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, int fontSize, FontStyle style, Color col, TextAnchor align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Text t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = col;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        private Button CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color col, string labelText, int fontSize, out Text outText)
        {
            GameObject go = CreateRect(name, parent, anchorMin, anchorMax, pos, size, col);
            Outline o = go.AddComponent<Outline>();
            o.effectColor = new Color(1f, 1f, 1f, 0.16f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();

            GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblObj.transform.SetParent(go.transform, false);
            RectTransform lrt = (RectTransform)lblObj.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12f, 8f);
            lrt.offsetMax = new Vector2(-12f, -8f);
            Text txt = lblObj.GetComponent<Text>();
            txt.font = _font;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.color = TextLight;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = labelText;
            txt.raycastTarget = false;
            outText = txt;
            return btn;
        }
    }
}
