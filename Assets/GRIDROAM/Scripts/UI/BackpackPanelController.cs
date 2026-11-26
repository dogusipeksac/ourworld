using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Flagship glassmorphic Backpack ("Sırt Çantam") overlay panel:
    /// - Opens via 1-tap HUD "Çanta" button or keyboard B / I (Esc to close)
    /// - Shows live Tokluk (Hunger), Enerji (Energy), and Can (Health) bars
    /// - Lists every bought Fruit Bag, grocery, snack, drink, vitamin, and fresh farm produce
    /// - 1-tap Eat / Drink triggers live 3D handheld consumption on the player character!
    /// </summary>
    public sealed class BackpackPanelController : MonoBehaviour
    {
        public static BackpackPanelController Instance { get; private set; }

        public bool IsOpen { get; private set; }

        private GameObject _rootObj;
        private Text _titleText;
        private Text _subtitleText;
        private Image _hungerFill;
        private Text _hungerValueText;
        private Image _energyFill;
        private Text _energyValueText;
        private Image _healthFill;
        private Text _healthValueText;
        private RectTransform _gridContent;
        private GameObject _emptyStateObj;
        private Text _emptyStateText;
        private readonly List<GameObject> _rowObjects = new List<GameObject>(24);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            EnsureInstance();
        }

        public static BackpackPanelController EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_BackpackPanelController");
            return go.AddComponent<BackpackPanelController>();
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
            BuildUi();
            PlayerBackpackInventory inv = PlayerBackpackInventory.EnsureInstance();
            inv.OnInventoryChanged += HandleInventoryChanged;
            if (PlayerNeedsController.Instance != null)
            {
                PlayerNeedsController.Instance.OnHungerChanged += HandleNeedsChanged;
                PlayerNeedsController.Instance.OnEnergyChanged += HandleNeedsChanged;
                PlayerNeedsController.Instance.OnHealthChanged += HandleHealthChanged;
            }
            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged += HandleLanguageChanged;
            }
        }

        private void OnDisable()
        {
            if (IsOpen) Close();
        }

        private void OnDestroy()
        {
            if (IsOpen && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
            if (PlayerBackpackInventory.Instance != null)
            {
                PlayerBackpackInventory.Instance.OnInventoryChanged -= HandleInventoryChanged;
            }
            if (PlayerNeedsController.Instance != null)
            {
                PlayerNeedsController.Instance.OnHungerChanged -= HandleNeedsChanged;
                PlayerNeedsController.Instance.OnEnergyChanged -= HandleNeedsChanged;
                PlayerNeedsController.Instance.OnHealthChanged -= HandleHealthChanged;
            }
            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged -= HandleLanguageChanged;
            }
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (IsOpen)
            {
                if (kb.escapeKey.wasPressedThisFrame || kb.bKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame)
                {
                    Close();
                }
                return;
            }

            if (kb.bKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame)
            {
                if (CanOpenNow())
                {
                    Open();
                }
            }
        }

        private static bool CanOpenNow()
        {
            if (MainMenuController.IsMenuActive) return false;
            if (PhonePanelController.Instance != null && PhonePanelController.Instance.IsOpen) return false;
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsModalLocked) return false;
            if (mode.CurrentMode == ControlMode.Tennis || mode.CurrentMode == ControlMode.ArenaSport) return false;
            if (PlayerNeedsController.Instance != null && PlayerNeedsController.Instance.IsKnockedOut) return false;
            return true;
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else if (CanOpenNow()) Open();
        }

        public void Open()
        {
            if (_rootObj == null) BuildUi();
            if (_rootObj == null || IsOpen) return;

            IsOpen = true;
            _rootObj.SetActive(true);
            PlayerModeController.Instance?.SetModalLock(this, true);
            GameFeelAudio.PlayUiClick();
            RefreshAll();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (_rootObj != null) _rootObj.SetActive(false);
            PlayerModeController.Instance?.SetModalLock(this, false);
        }

        private void HandleInventoryChanged()
        {
            if (IsOpen) RefreshAll();
        }

        private void HandleNeedsChanged(float _, bool __)
        {
            if (IsOpen) RefreshNeedsBars();
        }

        private void HandleHealthChanged(float _, float __)
        {
            if (IsOpen) RefreshNeedsBars();
        }

        private void HandleLanguageChanged(string _)
        {
            if (IsOpen) RefreshAll();
        }

        private void BuildUi()
        {
            if (_rootObj != null) return;
            OurWorldUiKit.EnsureInitialized();
            Font font = OurWorldUiKit.DefaultFont;

            GameObject canvasGo = new GameObject("Canvas_BackpackOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            Canvas c = canvasGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 235;
            CanvasScaler cs = canvasGo.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cs.matchWidthOrHeight = 0.5f;

            _rootObj = canvasGo;

            // Dimmed click-outside-to-close scrim
            GameObject scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image), typeof(Button));
            scrim.transform.SetParent(canvasGo.transform, false);
            RectTransform srt = (RectTransform)scrim.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            scrim.GetComponent<Image>().color = new Color(0.02f, 0.035f, 0.065f, 0.72f);
            scrim.GetComponent<Button>().onClick.AddListener(Close);

            // Main Glassmorphic Backpack Card (940 x 680)
            GameObject card = new GameObject("BackpackCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(canvasGo.transform, false);
            RectTransform crt = (RectTransform)card.transform;
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(940f, 680f);
            OurWorldUiKit.ApplyRounded(card.GetComponent<Image>(), OurWorldUiKit.CardSprite, OurWorldUiKit.CardBg);
            OurWorldUiKit.AddCardDropShadow(crt, spread: 28f, offsetY: -12f, alpha: 0.62f);
            OurWorldUiKit.AddSoftTopGlow(crt, OurWorldUiKit.AccentGold, heightPx: 145f, alpha: 0.14f);
            OurWorldUiKit.AddHairlineBorder(crt, OurWorldUiKit.CardOutlineSprite, OurWorldUiKit.BorderSubtle);

            // Header Title + Subtitle + Close Button
            _titleText = CreateText("Title", card.transform, font, "Sırt Çantam", 30, FontStyle.Bold, OurWorldUiKit.TextPrimary, TextAnchor.MiddleLeft);
            SetAnchoredRect(_titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(34f, -66f), new Vector2(-100f, -20f));

            _subtitleText = CreateText("Subtitle", card.transform, font, "Acıktığında meyve, yiyecek veya içeceklerini buradan tüketebilirsin.", 17, FontStyle.Normal, OurWorldUiKit.AccentGold, TextAnchor.MiddleLeft);
            SetAnchoredRect(_subtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(34f, -96f), new Vector2(-100f, -64f));

            Button closeBtn = OurWorldUiKit.CreateCircularIconButton(
                "Btn_CloseBackpack", card.transform, OurWorldUiKit.IconClose, 50f, 22f,
                OurWorldUiKit.SurfaceBg, OurWorldUiKit.TextPrimary, OurWorldUiKit.BorderSubtle, Close);
            RectTransform closeRt = (RectTransform)closeBtn.transform;
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-26f, -26f);

            // Live Needs Status Strip (Tokluk / Enerji / Can)
            GameObject strip = new GameObject("NeedsStrip", typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(card.transform, false);
            SetAnchoredRect((RectTransform)strip.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -162f), new Vector2(-30f, -106f));
            OurWorldUiKit.ApplyRounded(strip.GetComponent<Image>(), OurWorldUiKit.SubCardSprite, new Color(0.085f, 0.120f, 0.185f, 0.96f));
            OurWorldUiKit.AddHairlineBorder(strip.transform, OurWorldUiKit.SubCardOutlineSprite, OurWorldUiKit.BorderSubtle);

            BuildNeedGauge(strip.transform, font, "HungerGauge", 0.015f, 0.335f, new Color(0.98f, 0.66f, 0.18f), out _hungerFill, out _hungerValueText);
            BuildNeedGauge(strip.transform, font, "EnergyGauge", 0.345f, 0.665f, OurWorldUiKit.AccentTeal, out _energyFill, out _energyValueText);
            BuildNeedGauge(strip.transform, font, "HealthGauge", 0.675f, 0.985f, new Color(0.94f, 0.28f, 0.32f), out _healthFill, out _healthValueText);

            // ScrollView Area for Backpack Item Cards
            GameObject scrollGo = new GameObject("ItemsScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(card.transform, false);
            SetAnchoredRect((RectTransform)scrollGo.transform, Vector2.zero, Vector2.one, new Vector2(28f, 26f), new Vector2(-28f, -176f));
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            RectTransform vRt = (RectTransform)viewport.transform;
            vRt.anchorMin = Vector2.zero;
            vRt.anchorMax = Vector2.one;
            vRt.offsetMin = Vector2.zero;
            vRt.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            _gridContent = (RectTransform)content.transform;
            _gridContent.anchorMin = new Vector2(0f, 1f);
            _gridContent.anchorMax = new Vector2(1f, 1f);
            _gridContent.pivot = new Vector2(0.5f, 1f);
            _gridContent.anchoredPosition = Vector2.zero;
            _gridContent.sizeDelta = new Vector2(0f, 200f);

            GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(432f, 108f);
            grid.spacing = new Vector2(16f, 14f);
            grid.padding = new RectOffset(2, 2, 4, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = scrollGo.GetComponent<ScrollRect>();
            sr.viewport = vRt;
            sr.content = _gridContent;
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 28f;
            sr.movementType = ScrollRect.MovementType.Clamped;

            // Empty state message
            _emptyStateObj = new GameObject("EmptyState", typeof(RectTransform));
            _emptyStateObj.transform.SetParent(scrollGo.transform, false);
            RectTransform ert = (RectTransform)_emptyStateObj.transform;
            ert.anchorMin = Vector2.zero;
            ert.anchorMax = Vector2.one;
            ert.offsetMin = new Vector2(40f, 40f);
            ert.offsetMax = new Vector2(-40f, -40f);
            _emptyStateText = CreateText("EmptyText", _emptyStateObj.transform, font, "", 20, FontStyle.Normal, OurWorldUiKit.TextSecondary, TextAnchor.MiddleCenter);
            RectTransform etrt = _emptyStateText.rectTransform;
            etrt.anchorMin = Vector2.zero;
            etrt.anchorMax = Vector2.one;
            etrt.offsetMin = Vector2.zero;
            etrt.offsetMax = Vector2.zero;
            _emptyStateObj.SetActive(false);

            _rootObj.SetActive(false);
        }

        private void BuildNeedGauge(
            Transform parent,
            Font font,
            string name,
            float xMin,
            float xMax,
            Color fillCol,
            out Image fillImg,
            out Text labelText)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)host.transform;
            rt.anchorMin = new Vector2(xMin, 0f);
            rt.anchorMax = new Vector2(xMax, 1f);
            rt.offsetMin = new Vector2(8f, 6f);
            rt.offsetMax = new Vector2(-8f, -6f);

            labelText = CreateText("Label", host.transform, font, "100%", 15, FontStyle.Bold, OurWorldUiKit.TextPrimary, TextAnchor.UpperLeft);
            RectTransform lrt = labelText.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0.45f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            GameObject track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(host.transform, false);
            RectTransform trt = (RectTransform)track.transform;
            trt.anchorMin = new Vector2(0f, 0.08f);
            trt.anchorMax = new Vector2(1f, 0.36f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            OurWorldUiKit.ApplyRounded(track.GetComponent<Image>(), OurWorldUiKit.PillSprite, new Color(1f, 1f, 1f, 0.14f));

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            RectTransform frt = (RectTransform)fill.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            fillImg = fill.GetComponent<Image>();
            fillImg.sprite = OurWorldUiKit.PillSprite;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.color = fillCol;
            fillImg.raycastTarget = false;
        }

        private void RefreshAll()
        {
            bool en = LocalizationService.IsEnglish;
            if (_titleText != null) _titleText.text = en ? "My Backpack" : "Sırt Çantam";
            if (_subtitleText != null)
            {
                _subtitleText.text = en
                    ? "Eat fruit bags, snacks, or drinks anytime you get hungry or low on energy."
                    : "Acıktığında ya da enerjin düştüğünde meyve çantası, atıştırmalık ve içeceklerini buradan tüket.";
            }

            RefreshNeedsBars();
            RebuildItemRows(en);
        }

        private void RefreshNeedsBars()
        {
            bool en = LocalizationService.IsEnglish;
            PlayerNeedsController needs = PlayerNeedsController.Instance;
            float hunger = needs != null ? needs.Hunger : 85f;
            float energy = needs != null ? needs.Energy : 80f;
            float health = needs != null ? needs.Health : 100f;

            if (_hungerFill != null)
            {
                _hungerFill.fillAmount = Mathf.Clamp01(hunger / 100f);
                _hungerFill.color = hunger < 28f ? OurWorldUiKit.AccentCoral : new Color(0.98f, 0.68f, 0.18f);
            }
            if (_hungerValueText != null)
            {
                _hungerValueText.text = en ? $"Food  {Mathf.RoundToInt(hunger)}%" : $"Tokluk  %{Mathf.RoundToInt(hunger)}";
            }

            if (_energyFill != null) _energyFill.fillAmount = Mathf.Clamp01(energy / 100f);
            if (_energyValueText != null)
            {
                _energyValueText.text = en ? $"Energy  {Mathf.RoundToInt(energy)}%" : $"Enerji  %{Mathf.RoundToInt(energy)}";
            }

            if (_healthFill != null) _healthFill.fillAmount = Mathf.Clamp01(health / 100f);
            if (_healthValueText != null)
            {
                _healthValueText.text = en ? $"Health  {Mathf.RoundToInt(health)}%" : $"Can  %{Mathf.RoundToInt(health)}";
            }
        }

        private void RebuildItemRows(bool en)
        {
            for (int i = 0; i < _rowObjects.Count; i++)
            {
                if (_rowObjects[i] != null) Destroy(_rowObjects[i]);
            }
            _rowObjects.Clear();

            if (_gridContent == null) return;
            List<BackpackDisplayItem> items = PlayerBackpackInventory.EnsureInstance().GetAllEntriesSnapshot();

            bool empty = items.Count == 0;
            if (_emptyStateObj != null)
            {
                _emptyStateObj.SetActive(empty);
                if (empty && _emptyStateText != null)
                {
                    _emptyStateText.text = en
                        ? "Your backpack is empty!\n\nVisit Selman Bakkal or 5M Atlas Market to buy a Fruit Bag, simit, toast, or cold drinks to carry with you."
                        : "Çantan şu an boş!\n\nSelman Bakkal'dan veya 5M Atlas Market'ten Meyve Çantası, simit, tost ve soğuk içecek alıp çantanda taşıyabilirsin.";
                }
            }
            if (empty) return;

            Font font = OurWorldUiKit.DefaultFont;
            for (int i = 0; i < items.Count; i++)
            {
                BackpackDisplayItem item = items[i];
                GameObject row = BuildItemCard(item, font, en);
                _rowObjects.Add(row);
            }
        }

        private GameObject BuildItemCard(BackpackDisplayItem item, Font font, bool en)
        {
            GameObject card = new GameObject("BagItem_" + item.ItemId, typeof(RectTransform), typeof(Image));
            card.transform.SetParent(_gridContent, false);
            OurWorldUiKit.ApplyRounded(card.GetComponent<Image>(), OurWorldUiKit.SubCardSprite, OurWorldUiKit.SurfaceBg);
            OurWorldUiKit.AddHairlineBorder(card.transform, OurWorldUiKit.SubCardOutlineSprite, OurWorldUiKit.BorderSubtle);

            Color accent = GetCategoryColor(item);

            // Colored left category pill strip
            GameObject bar = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(card.transform, false);
            SetAnchoredRect((RectTransform)bar.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 14f), new Vector2(18f, -14f));
            OurWorldUiKit.ApplyRounded(bar.GetComponent<Image>(), OurWorldUiKit.PillSprite, accent);

            // Item name + quantity
            string title = en ? item.NameEn : item.NameTr;
            string farmSuffix = item.IsFromFarm ? (en ? " · Farm" : " · Çiftlik") : string.Empty;
            Text nameTxt = CreateText("Name", card.transform, font, $"{title}  ×{item.Count}", 19, FontStyle.Bold, OurWorldUiKit.TextPrimary, TextAnchor.MiddleLeft);
            SetAnchoredRect(nameTxt.rectTransform, new Vector2(0f, 0.50f), new Vector2(1f, 1f), new Vector2(28f, 0f), new Vector2(-124f, -6f));

            // Exact stat benefits
            string statStr = en
                ? $"+{Mathf.RoundToInt(item.HungerRestore)} Food · +{Mathf.RoundToInt(item.EnergyRestore)} Energy · +{Mathf.RoundToInt(item.HealthRestore)} HP{farmSuffix}"
                : $"+{Mathf.RoundToInt(item.HungerRestore)} Tokluk · +{Mathf.RoundToInt(item.EnergyRestore)} Enerji · +{Mathf.RoundToInt(item.HealthRestore)} Can{farmSuffix}";
            Text statTxt = CreateText("Stats", card.transform, font, statStr, 14, FontStyle.Normal, OurWorldUiKit.TextSecondary, TextAnchor.MiddleLeft);
            SetAnchoredRect(statTxt.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.50f), new Vector2(28f, 8f), new Vector2(-124f, 2f));

            bool isDrink = item.ConsumeKind == ConsumeAnimKind.DrinkWater || item.ConsumeKind == ConsumeAnimKind.DrinkHotCup;
            string btnLabel = isDrink ? (en ? "Drink" : "İç") : (en ? "Eat" : "Ye");
            string capturedId = item.ItemId;

            Button useBtn = OurWorldUiKit.CreatePillButton(
                "Btn_UseItem", card.transform, btnLabel, 18,
                accent, OurWorldUiKit.InkDark, new Color(1f, 1f, 1f, 0.22f),
                out _,
                () =>
                {
                    Close();
                    PlayerBackpackInventory.EnsureInstance().TryConsumeItem(capturedId, PlayerModeController.Instance);
                });
            RectTransform brt = (RectTransform)useBtn.transform;
            brt.anchorMin = new Vector2(1f, 0.5f);
            brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = new Vector2(-14f, 0f);
            brt.sizeDelta = new Vector2(96f, 44f);

            return card;
        }

        private static Color GetCategoryColor(BackpackDisplayItem item)
        {
            if (item.IsFromFarm) return new Color(0.94f, 0.42f, 0.54f);
            if (item.PropVisual == ShopPropVisual.MedicineBox) return new Color(0.24f, 0.86f, 0.56f);
            if (item.ConsumeKind == ConsumeAnimKind.DrinkWater || item.ConsumeKind == ConsumeAnimKind.DrinkHotCup)
                return OurWorldUiKit.AccentSky;
            if (item.PropVisual == ShopPropVisual.FreshFruit) return new Color(0.98f, 0.64f, 0.20f);
            return OurWorldUiKit.AccentGold;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int size, FontStyle style, Color color, TextAnchor align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        private static void SetAnchoredRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
