using System;
using System.Collections;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    public struct MallShopItemDef
    {
        public string NameTr;
        public string NameEn;
        public string DescTr;
        public string DescEn;
        public int Price;
        public float EnergyRestore;
        public int ReputationXp;
        public ConsumeAnimKind ConsumeAnim;
        public string SpecialUpgradeId; // "", "running_shoes", "turbo_kit", "seed_pack"
        public Color SwatchColor;
    }

    public struct MallShopCatalog
    {
        public string ShopId;
        public string TitleTr;
        public string TitleEn;
        public string SubtitleTr;
        public string SubtitleEn;
        public Color AccentColor;
        public MallShopItemDef[] Items;
    }

    /// <summary>
    /// Flagship OurWorldUiKit runtime overlays for Hamidiye Atlas AVM:
    /// 1. Panoramic Glass Elevator Floor Selector (Floors 0..4)
    /// 2. Atlas Waikiki Fashion Boutique Wardrobe Picker (8 persistent jacket styles)
    /// 3. Universal Interactive Store / Coffee / Food Court Catalog Modal (with Consume animations & upgrades!)
    /// 4. CineAtlas Movie Ticket Selector + Live Widescreen Cinema Screening Sequence!
    /// </summary>
    public sealed class MallUiPanels : MonoBehaviour
    {
        public static MallUiPanels Instance { get; private set; }

        private Canvas _canvas;
        private GameObject _root;
        private CanvasGroup _cg;
        private RectTransform _card;
        private Image _topGlow;
        private Text _titleText;
        private Text _subtitleText;
        private Text _balanceText;
        private Transform _contentRoot;
        private Coroutine _animRoutine;

        // Live CineAtlas Widescreen Screening Overlay
        private GameObject _cinemaRoot;
        private Image _cinemaScreenBg;
        private Text _cinemaMovieTitle;
        private Text _cinemaCaption;
        private Image _cinemaProgressFill;
        private bool _skipCinemaRequested;

        public bool IsCinemaPlaying => _cinemaRoot != null && _cinemaRoot.activeSelf;
        public void SkipCinema() => _skipCinemaRequested = true;

        private static readonly string[] FloorNamesTr =
        {
            "0 · Zemin Kat — 5M Atlas Market · StarBean · Kahve Diyarı · Gratis · Eczane",
            "1 · Moda & Teknoloji — Atlas Waikiki · Mavi & Kotonica · TeknoCell · Karaca · D&M",
            "2 · Yemek Katı — Kral Burger · Tavuk Diyarı · Bereket Döner · Pizza Lazza · Sütlüce",
            "3 · Sinema & Eğlence — CineAtlas Sinema · JoyPark Oyun Merkezi",
            "4 · Açık Çatı Terası — Ennjoy Lounge Teras Kafe · Seyir Dürbünü"
        };

        private static readonly string[] FloorNamesEn =
        {
            "0 · Ground Floor — 5M Market · StarBean · Coffee · Gratis · Pharmacy",
            "1 · Fashion & Tech — Atlas Waikiki · Mavi & Kotonica · TeknoCell · Karaca · D&M",
            "2 · Food Court — Kral Burger · Tavuk Diyarı · Bereket Döner · Pizza Lazza · Kadayıf",
            "3 · Cinema & Fun — CineAtlas Cinema · JoyPark Game Center",
            "4 · Rooftop Terrace — Ennjoy Lounge Sky Cafe · Panorama Binoculars"
        };

        private static readonly string[] OutfitNamesTr =
        {
            "Klasik Lacivert",
            "Bordo Deri",
            "Zeytin Parka",
            "Antrasit Sokak",
            "İstanbul Turkuazı",
            "Safran Hardal",
            "Krem Keten",
            "Mürdüm Kadife"
        };

        private static readonly string[] OutfitNamesEn =
        {
            "Classic Navy",
            "Burgundy Leather",
            "Olive Parka",
            "Charcoal Street",
            "Istanbul Turquoise",
            "Saffron Ochre",
            "Cream Linen",
            "Plum Velvet"
        };

        public bool IsOpen => (_root != null && _root.activeSelf) || (_cinemaRoot != null && _cinemaRoot.activeSelf);

        public static MallUiPanels EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_MallUiPanels");
            return go.AddComponent<MallUiPanels>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildBaseShell();
            BuildCinemaOverlay();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildBaseShell()
        {
            OurWorldUiKit.EnsureInitialized();

            GameObject canvasGo = new GameObject("MallPanelsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 148;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _root = new GameObject("ModalScrim", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _root.transform.SetParent(canvasGo.transform, false);
            RectTransform rootRt = (RectTransform)_root.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = OurWorldUiKit.Scrim;
            _cg = _root.GetComponent<CanvasGroup>();

            Button scrimBtn = _root.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(Close);

            GameObject cardGo = new GameObject("MallModalCard", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(_root.transform, false);
            _card = (RectTransform)cardGo.transform;
            _card.anchorMin = new Vector2(0.5f, 0.5f);
            _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(880f, 660f);
            OurWorldUiKit.ApplyRounded(cardGo.GetComponent<Image>(), OurWorldUiKit.CardSprite, OurWorldUiKit.CardBg);
            OurWorldUiKit.AddCardDropShadow(cardGo.transform, 28f, -12f, 0.58f);
            _topGlow = OurWorldUiKit.AddSoftTopGlow(cardGo.transform, OurWorldUiKit.AccentTeal, 150f, 0.18f);
            OurWorldUiKit.AddHairlineBorder(cardGo.transform, OurWorldUiKit.CardOutlineSprite, OurWorldUiKit.BorderSubtle);

            // Title
            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(cardGo.transform, false);
            RectTransform tRt = (RectTransform)titleGo.transform;
            tRt.anchorMin = new Vector2(0f, 1f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.anchoredPosition = new Vector2(-46f, -24f);
            tRt.sizeDelta = new Vector2(-150f, 44f);
            _titleText = titleGo.GetComponent<Text>();
            _titleText.font = OurWorldUiKit.DefaultFont;
            _titleText.fontSize = 31;
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.color = OurWorldUiKit.TextPrimary;
            _titleText.alignment = TextAnchor.MiddleLeft;
            _titleText.raycastTarget = false;

            // Subtitle
            GameObject subGo = new GameObject("Subtitle", typeof(RectTransform), typeof(Text));
            subGo.transform.SetParent(cardGo.transform, false);
            RectTransform sRt = (RectTransform)subGo.transform;
            sRt.anchorMin = new Vector2(0f, 1f);
            sRt.anchorMax = new Vector2(1f, 1f);
            sRt.pivot = new Vector2(0.5f, 1f);
            sRt.anchoredPosition = new Vector2(-46f, -68f);
            sRt.sizeDelta = new Vector2(-150f, 30f);
            _subtitleText = subGo.GetComponent<Text>();
            _subtitleText.font = OurWorldUiKit.DefaultFont;
            _subtitleText.fontSize = 19;
            _subtitleText.color = OurWorldUiKit.AccentGold;
            _subtitleText.alignment = TextAnchor.MiddleLeft;
            _subtitleText.raycastTarget = false;

            // Balance Pill (top-right next to close)
            GameObject balGo = new GameObject("BalancePill", typeof(RectTransform), typeof(Image));
            balGo.transform.SetParent(cardGo.transform, false);
            RectTransform bRt = (RectTransform)balGo.transform;
            bRt.anchorMin = new Vector2(1f, 1f);
            bRt.anchorMax = new Vector2(1f, 1f);
            bRt.pivot = new Vector2(1f, 1f);
            bRt.anchoredPosition = new Vector2(-86f, -26f);
            bRt.sizeDelta = new Vector2(168f, 44f);
            OurWorldUiKit.ApplyRounded(balGo.GetComponent<Image>(), OurWorldUiKit.PillSprite, OurWorldUiKit.SurfaceSoft);
            OurWorldUiKit.AddHairlineBorder(balGo.transform, OurWorldUiKit.PillOutlineSprite, OurWorldUiKit.BorderSubtle);
            _balanceText = OurWorldUiKit.CreateStretchText("BalText", balGo.transform, "₺ 250", 21, FontStyle.Bold, OurWorldUiKit.AccentGold);

            Button closeBtn = OurWorldUiKit.CreateCircularIconButton(
                "CloseBtn", cardGo.transform, OurWorldUiKit.IconClose, 48f, 22f,
                OurWorldUiKit.SurfaceSoft, OurWorldUiKit.TextPrimary, OurWorldUiKit.BorderSubtle, Close);
            RectTransform closeRt = (RectTransform)closeBtn.transform;
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-24f, -24f);

            OurWorldUiKit.AddDividerLine(cardGo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -110f), new Vector2(-28f, -108f));

            GameObject contentGo = new GameObject("ContentRoot", typeof(RectTransform));
            contentGo.transform.SetParent(cardGo.transform, false);
            RectTransform cRt = (RectTransform)contentGo.transform;
            cRt.anchorMin = new Vector2(0f, 0f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.offsetMin = new Vector2(28f, 24f);
            cRt.offsetMax = new Vector2(-28f, -124f);
            _contentRoot = contentGo.transform;

            _root.SetActive(false);
        }

        private void BuildCinemaOverlay()
        {
            _cinemaRoot = new GameObject("CineAtlasScreeningOverlay", typeof(RectTransform), typeof(Image));
            _cinemaRoot.transform.SetParent(_canvas.transform, false);
            RectTransform crt = (RectTransform)_cinemaRoot.transform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            _cinemaRoot.GetComponent<Image>().color = new Color(0.015f, 0.018f, 0.032f, 0.94f);

            // Giant Widescreen Cinema Projection Canvas
            GameObject screenGo = new GameObject("ProjectionScreen", typeof(RectTransform), typeof(Image));
            screenGo.transform.SetParent(_cinemaRoot.transform, false);
            RectTransform srt = (RectTransform)screenGo.transform;
            srt.anchorMin = new Vector2(0.08f, 0.22f);
            srt.anchorMax = new Vector2(0.92f, 0.84f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            _cinemaScreenBg = OurWorldUiKit.ApplyRounded(screenGo.GetComponent<Image>(), OurWorldUiKit.CardSprite, new Color(0.08f, 0.14f, 0.26f, 1f));
            OurWorldUiKit.AddHairlineBorder(screenGo.transform, OurWorldUiKit.CardOutlineSprite, new Color(1f, 0.82f, 0.32f, 0.55f));
            OurWorldUiKit.AddSoftTopGlow(screenGo.transform, OurWorldUiKit.AccentGold, 220f, 0.22f);

            // Top Cinema Badge
            GameObject badgeGo = new GameObject("CinemaBadge", typeof(RectTransform), typeof(Text));
            badgeGo.transform.SetParent(screenGo.transform, false);
            RectTransform brt = (RectTransform)badgeGo.transform;
            brt.anchorMin = new Vector2(0f, 0.72f);
            brt.anchorMax = new Vector2(1f, 0.92f);
            brt.offsetMin = new Vector2(40f, 0f);
            brt.offsetMax = new Vector2(-40f, 0f);
            _cinemaMovieTitle = badgeGo.GetComponent<Text>();
            _cinemaMovieTitle.font = OurWorldUiKit.DefaultFont;
            _cinemaMovieTitle.fontSize = 44;
            _cinemaMovieTitle.fontStyle = FontStyle.Bold;
            _cinemaMovieTitle.color = OurWorldUiKit.AccentGold;
            _cinemaMovieTitle.alignment = TextAnchor.MiddleCenter;

            // Live Subtitle / Story Beat Text
            GameObject capGo = new GameObject("CinemaCaption", typeof(RectTransform), typeof(Text));
            capGo.transform.SetParent(screenGo.transform, false);
            RectTransform capRt = (RectTransform)capGo.transform;
            capRt.anchorMin = new Vector2(0.08f, 0.16f);
            capRt.anchorMax = new Vector2(0.92f, 0.66f);
            capRt.offsetMin = Vector2.zero;
            capRt.offsetMax = Vector2.zero;
            _cinemaCaption = capGo.GetComponent<Text>();
            _cinemaCaption.font = OurWorldUiKit.DefaultFont;
            _cinemaCaption.fontSize = 30;
            _cinemaCaption.fontStyle = FontStyle.Italic;
            _cinemaCaption.color = OurWorldUiKit.TextPrimary;
            _cinemaCaption.alignment = TextAnchor.MiddleCenter;

            // Progress Bar
            GameObject progBg = new GameObject("ProgressBg", typeof(RectTransform), typeof(Image));
            progBg.transform.SetParent(_cinemaRoot.transform, false);
            RectTransform pbrt = (RectTransform)progBg.transform;
            pbrt.anchorMin = new Vector2(0.18f, 0.13f);
            pbrt.anchorMax = new Vector2(0.82f, 0.148f);
            pbrt.offsetMin = Vector2.zero;
            pbrt.offsetMax = Vector2.zero;
            OurWorldUiKit.ApplyRounded(progBg.GetComponent<Image>(), OurWorldUiKit.PillSprite, OurWorldUiKit.SurfaceBg);

            GameObject progFill = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
            progFill.transform.SetParent(progBg.transform, false);
            RectTransform pfrt = (RectTransform)progFill.transform;
            pfrt.anchorMin = Vector2.zero;
            pfrt.anchorMax = Vector2.one;
            pfrt.offsetMin = Vector2.zero;
            pfrt.offsetMax = Vector2.zero;
            _cinemaProgressFill = progFill.GetComponent<Image>();
            _cinemaProgressFill.sprite = OurWorldUiKit.PillSprite;
            _cinemaProgressFill.type = Image.Type.Filled;
            _cinemaProgressFill.fillMethod = Image.FillMethod.Horizontal;
            _cinemaProgressFill.color = OurWorldUiKit.AccentGold;

            // Skip / Exit Button
            Button skipBtn = OurWorldUiKit.CreatePillButton(
                "SkipMovieBtn", _cinemaRoot.transform, "Filmi Bitir / Salondan Çık", 22,
                OurWorldUiKit.SurfaceSoft, OurWorldUiKit.TextPrimary, OurWorldUiKit.BorderSubtle, out _, () => _skipCinemaRequested = true);
            RectTransform skipRt = (RectTransform)skipBtn.transform;
            skipRt.anchorMin = new Vector2(0.5f, 0.045f);
            skipRt.anchorMax = new Vector2(0.5f, 0.045f);
            skipRt.pivot = new Vector2(0.5f, 0.5f);
            skipRt.sizeDelta = new Vector2(340f, 54f);

            _cinemaRoot.SetActive(false);
        }

        private void ClearContent()
        {
            for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_contentRoot.GetChild(i).gameObject);
            }
            VerticalLayoutGroup vlg = _contentRoot.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) DestroyImmediate(vlg);
            GridLayoutGroup glg = _contentRoot.GetComponent<GridLayoutGroup>();
            if (glg != null) DestroyImmediate(glg);
        }

        private void RefreshBalanceBadge()
        {
            int money = SaveManager.Instance != null && SaveManager.Instance.Data != null ? SaveManager.Instance.Data.money : 0;
            if (_balanceText != null) _balanceText.text = $"₺ {money}";
        }

        // =====================================================================
        // 1. PANORAMIC ELEVATOR FLOOR SELECTOR
        // =====================================================================
        public void ShowElevatorModal(int currentFloor, Action<int> onFloorSelected)
        {
            ClearContent();
            RefreshBalanceBadge();
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            _card.sizeDelta = new Vector2(900f, 620f);
            SetTopAccent(OurWorldUiKit.AccentTeal);
            _titleText.text = isEn ? "Atlaspark Mall · Panoramic Elevator" : "Hamidiye Atlas AVM · Panoramik Asansör";
            _subtitleText.text = isEn
                ? $"Current level: Floor {currentFloor} — Select your destination"
                : $"Bulunduğun kat: {currentFloor}. Kat — Gitmek istediğin katı seç";

            VerticalLayoutGroup vlg = _contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;

            string[] names = isEn ? FloorNamesEn : FloorNamesTr;
            for (int f = 4; f >= 0; f--)
            {
                int floorIndex = f;
                bool isCurrent = floorIndex == currentFloor;
                Color bg = isCurrent ? OurWorldUiKit.SurfaceActive : OurWorldUiKit.SurfaceBg;
                Color border = isCurrent ? OurWorldUiKit.AccentTeal : OurWorldUiKit.BorderSubtle;
                Color txtCol = isCurrent ? OurWorldUiKit.AccentGold : OurWorldUiKit.TextPrimary;
                string label = isCurrent
                    ? (isEn ? $"●  {names[floorIndex]}  (Here)" : $"●  {names[floorIndex]}  (Buradasın)")
                    : $"▲  {names[floorIndex]}";

                OurWorldUiKit.CreatePillButton(
                    $"FloorBtn_{floorIndex}", _contentRoot, label, 20, bg, txtCol, border, out Text lbl, () =>
                    {
                        Close();
                        onFloorSelected?.Invoke(floorIndex);
                    });
                lbl.alignment = TextAnchor.MiddleLeft;
                RectTransform lRt = lbl.rectTransform;
                lRt.offsetMin = new Vector2(24f, 4f);
                lRt.offsetMax = new Vector2(-24f, -4f);
            }

            OpenModal();
        }

        // =====================================================================
        // 2. UNIVERSAL INTERACTIVE RETAIL / CAFE / FOOD COURT CATALOG MODAL
        // =====================================================================
        public void ShowShopMenuModal(MallShopCatalog catalog, Transform counterTransform)
        {
            ClearContent();
            RefreshBalanceBadge();
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            _card.sizeDelta = new Vector2(880f, 640f);
            SetTopAccent(catalog.AccentColor);
            _titleText.text = isEn ? catalog.TitleEn : catalog.TitleTr;
            _subtitleText.text = isEn ? catalog.SubtitleEn : catalog.SubtitleTr;

            GridLayoutGroup glg = _contentRoot.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(400f, 118f);
            glg.spacing = new Vector2(20f, 16f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;
            glg.childAlignment = TextAnchor.UpperCenter;

            if (catalog.Items != null)
            {
                for (int i = 0; i < catalog.Items.Length; i++)
                {
                    MallShopItemDef item = catalog.Items[i];
                    bool alreadyOwned = IsPermanentUpgradeOwned(item.SpecialUpgradeId);

                    GameObject row = new GameObject($"ShopItem_{i}", typeof(RectTransform), typeof(Image));
                    row.transform.SetParent(_contentRoot, false);
                    Image rowBg = OurWorldUiKit.ApplyRounded(row.GetComponent<Image>(), OurWorldUiKit.SubCardSprite,
                        alreadyOwned ? OurWorldUiKit.SurfaceActive : OurWorldUiKit.SurfaceBg);
                    OurWorldUiKit.AddHairlineBorder(row.transform, OurWorldUiKit.SubCardOutlineSprite,
                        alreadyOwned ? OurWorldUiKit.AccentTeal : OurWorldUiKit.BorderSubtle);

                    // Left color accent badge
                    GameObject sw = new GameObject("Badge", typeof(RectTransform), typeof(Image));
                    sw.transform.SetParent(row.transform, false);
                    RectTransform swRt = (RectTransform)sw.transform;
                    swRt.anchorMin = new Vector2(0f, 0.5f);
                    swRt.anchorMax = new Vector2(0f, 0.5f);
                    swRt.pivot = new Vector2(0f, 0.5f);
                    swRt.anchoredPosition = new Vector2(16f, 0f);
                    swRt.sizeDelta = new Vector2(14f, 76f);
                    OurWorldUiKit.ApplyRounded(sw.GetComponent<Image>(), OurWorldUiKit.PillSprite,
                        item.SwatchColor.a > 0.05f ? item.SwatchColor : catalog.AccentColor);

                    string name = isEn ? item.NameEn : item.NameTr;
                    string desc = isEn ? item.DescEn : item.DescTr;
                    string priceTag = alreadyOwned
                        ? (isEn ? "OWNED ✓" : "SAHİPSİN ✓")
                        : $"{item.Price} ₺";
                    string hexCol = alreadyOwned ? "24C7B3" : "F7BF3D";

                    Text t = OurWorldUiKit.CreateStretchText(
                        "Label", row.transform,
                        $"<b>{name}</b>\n<size=16><color=#B8CBD9>{desc}</color></size>\n<size=18><color=#{hexCol}><b>{priceTag}</b></color></size>",
                        21, FontStyle.Normal, OurWorldUiKit.TextPrimary, TextAnchor.MiddleLeft, 44f, 8f);
                    t.supportRichText = true;

                    Button btn = row.AddComponent<Button>();
                    btn.targetGraphic = rowBg;
                    UiButtonFeedback fb = row.AddComponent<UiButtonFeedback>();
                    fb.Configure(btn, rowBg);
                    btn.onClick.AddListener(() => PurchaseShopItem(catalog, item, counterTransform, isEn));
                }
            }

            OpenModal();
        }

        private void PurchaseShopItem(MallShopCatalog catalog, MallShopItemDef item, Transform counterTransform, bool isEn)
        {
            PlayerModeController mode = PlayerModeController.Instance;
            SaveManager sm = SaveManager.Instance;

            if (IsPermanentUpgradeOwned(item.SpecialUpgradeId))
            {
                GameFeelAudio.PlayUiClick();
                mode?.RaiseStatusMessage(isEn ? "You already own this upgrade!" : "Bu yükseltmeye zaten sahipsin!");
                return;
            }

            if (sm != null && item.Price > 0 && !sm.TrySpend(item.Price))
            {
                GameFeelAudio.PlayUiClick();
                mode?.RaiseStatusMessage(isEn ? $"Not enough money ({item.Price} ₺ needed)!" : $"Yetersiz bakiye ({item.Price} ₺ gerekli)!");
                return;
            }

            string itemName = isEn ? item.NameEn : item.NameTr;

            // Permanent Upgrades & Special Packs
            if (!string.IsNullOrEmpty(item.SpecialUpgradeId) && sm != null && sm.Data != null)
            {
                switch (item.SpecialUpgradeId)
                {
                    case "running_shoes":
                        sm.Data.hasRunningShoes = true;
                        break;
                    case "turbo_kit":
                        sm.Data.hasTurboKit = true;
                        break;
                    case "seed_pack":
                        sm.Data.invTomatoes += 2;
                        sm.Data.invStrawberries += 2;
                        break;
                    case "sunglasses":
                        sm.Data.ownsSunglasses = true;
                        sm.Data.equipSunglasses = true;
                        mode?.FootController?.ApplySavedAppearance();
                        break;
                    case "street_cap":
                        sm.Data.ownsStreetCap = true;
                        sm.Data.equipStreetCap = true;
                        mode?.FootController?.ApplySavedAppearance();
                        break;
                }
                sm.Save();
            }

            DailyQuestAndAchievementSystem.RecordMallPurchase();

            if (item.ReputationXp > 0)
            {
                NeighborhoodReputation.AddReputation(item.ReputationXp);
            }

            // Close modal FIRST so PlayerConsumeAnimator can play cleanly without modal lock
            Close();

            if (item.ConsumeAnim != ConsumeAnimKind.None && mode != null && mode.FootController != null && mode.CurrentMode == ControlMode.OnFoot)
            {
                PlayerConsumeAnimator.PlayPurchase(mode.FootController, catalog.ShopId, item.ConsumeAnim, itemName, item.EnergyRestore, counterTransform);
            }
            else if (item.EnergyRestore > 0f)
            {
                PlayerNeedsController.Instance?.RestoreEnergy(item.EnergyRestore);
            }

            GameFeelAudio.PlayCoinChime();
            if (mode != null && mode.FootController != null)
            {
                GameFeelAudio.SpawnBurst(mode.FootController.transform.position + Vector3.up * 1.25f, catalog.AccentColor, 10, 0.55f);
            }

            string bonus = item.EnergyRestore > 0f ? $" · +{Mathf.RoundToInt(item.EnergyRestore)} {(isEn ? "Energy" : "Enerji")}" : string.Empty;
            if (item.ReputationXp > 0) bonus += $" · +{item.ReputationXp} XP";
            mode?.RaiseStatusMessage(isEn
                ? $"Purchased: {itemName} (-{item.Price} ₺{bonus})"
                : $"Alındı: {itemName} (-{item.Price} ₺{bonus})");
        }

        private static bool IsPermanentUpgradeOwned(string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId)) return false;
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return false;
            if (upgradeId == "running_shoes") return d.hasRunningShoes;
            if (upgradeId == "turbo_kit") return d.hasTurboKit;
            if (upgradeId == "sunglasses") return d.ownsSunglasses;
            if (upgradeId == "street_cap") return d.ownsStreetCap;
            return false;
        }

        // =====================================================================
        // 3. ATLAS WAIKIKI FASHION BOUTIQUE WARDROBE (8 STYLES)
        // =====================================================================
        public void ShowBoutiqueModal()
        {
            ClearContent();
            RefreshBalanceBadge();
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            _card.sizeDelta = new Vector2(840f, 620f);
            SetTopAccent(OurWorldUiKit.AccentSky);
            _titleText.text = isEn ? "Atlas Waikiki · 1st Floor Fashion" : "Atlas Waikiki · 1. Kat Giyim Mağazası";
            _subtitleText.text = isEn
                ? "Try on & wear a new jacket style — 35 ₺ per change"
                : "Yeni ceket stilini anında dene & giy — Değişim 35 ₺";

            GridLayoutGroup glg = _contentRoot.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(378f, 96f);
            glg.spacing = new Vector2(20f, 16f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;
            glg.childAlignment = TextAnchor.UpperCenter;

            int currentIdx = SaveManager.Instance != null && SaveManager.Instance.Data != null
                ? SaveManager.Instance.Data.playerJacketColorIndex
                : 0;

            int count = PlayerFootController.JacketColorCount;
            string[] names = isEn ? OutfitNamesEn : OutfitNamesTr;

            for (int i = 0; i < count; i++)
            {
                int idx = i;
                bool active = idx == currentIdx;
                Color swatch = PlayerFootController.GetJacketColor(idx);

                GameObject row = new GameObject($"Outfit_{idx}", typeof(RectTransform), typeof(Image));
                row.transform.SetParent(_contentRoot, false);
                Image rowBg = OurWorldUiKit.ApplyRounded(row.GetComponent<Image>(), OurWorldUiKit.SubCardSprite,
                    active ? OurWorldUiKit.SurfaceActive : OurWorldUiKit.SurfaceBg);
                OurWorldUiKit.AddHairlineBorder(row.transform, OurWorldUiKit.SubCardOutlineSprite,
                    active ? OurWorldUiKit.AccentTeal : OurWorldUiKit.BorderSubtle);

                GameObject sw = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
                sw.transform.SetParent(row.transform, false);
                RectTransform swRt = (RectTransform)sw.transform;
                swRt.anchorMin = new Vector2(0f, 0.5f);
                swRt.anchorMax = new Vector2(0f, 0.5f);
                swRt.pivot = new Vector2(0f, 0.5f);
                swRt.anchoredPosition = new Vector2(18f, 0f);
                swRt.sizeDelta = new Vector2(54f, 54f);
                Image swImg = sw.GetComponent<Image>();
                swImg.sprite = OurWorldUiKit.CircleSprite;
                swImg.color = swatch;
                swImg.raycastTarget = false;
                OurWorldUiKit.AddHairlineBorder(sw.transform, OurWorldUiKit.CircleOutlineSprite, new Color(1f, 1f, 1f, 0.45f));

                string title = idx < names.Length ? names[idx] : $"Stil #{idx + 1}";
                string tag = active
                    ? (isEn ? "Currently Worn" : "Üzerinde")
                    : (isEn ? "Wear · 35 ₺" : "Giy · 35 ₺");

                Text t = OurWorldUiKit.CreateStretchText(
                    "Label", row.transform,
                    $"{title}\n<size=17><color=#{(active ? "24C7B3" : "F7BF3D")}>{tag}</color></size>",
                    22, FontStyle.Bold, OurWorldUiKit.TextPrimary, TextAnchor.MiddleLeft, 88f, 8f);
                t.supportRichText = true;

                Button btn = row.AddComponent<Button>();
                btn.targetGraphic = rowBg;
                UiButtonFeedback fb = row.AddComponent<UiButtonFeedback>();
                fb.Configure(btn, rowBg);
                btn.onClick.AddListener(() => SelectOutfit(idx, title, isEn));
            }

            OpenModal();
        }

        private void SelectOutfit(int idx, string title, bool isEn)
        {
            SaveManager sm = SaveManager.Instance;
            PlayerModeController mode = PlayerModeController.Instance;
            int currentIdx = sm != null && sm.Data != null ? sm.Data.playerJacketColorIndex : 0;

            if (idx == currentIdx)
            {
                Close();
                return;
            }

            const int price = 35;
            if (sm != null && !sm.TrySpend(price))
            {
                GameFeelAudio.PlayUiClick();
                mode?.RaiseStatusMessage(isEn ? "Not enough money (35 ₺ needed)!" : "Yetersiz bakiye (35 ₺ gerekli)!");
                return;
            }

            if (sm != null && sm.Data != null)
            {
                sm.Data.playerJacketColorIndex = idx;
                sm.Save();
            }
            if (mode != null && mode.FootController != null)
            {
                mode.FootController.ApplyJacketColor(idx);
                GameFeelAudio.SpawnBurst(mode.FootController.transform.position + Vector3.up * 1.2f,
                    PlayerFootController.GetJacketColor(idx), count: 10, spread: 0.55f);
            }

            GameFeelAudio.PlayCoinChime();
            mode?.RaiseStatusMessage(isEn ? $"New style equipped: {title} (-35 ₺)" : $"Yeni stil giyildi: {title} (-35 ₺)");
            Close();
        }

        // =====================================================================
        // 4. CINEATLAS CINEMA TICKET PICKER & LIVE WIDESCREEN SCREENING
        // =====================================================================
        public void ShowCinemaModal(Action<int> onWatchMovie)
        {
            ClearContent();
            RefreshBalanceBadge();
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            _card.sizeDelta = new Vector2(860f, 560f);
            SetTopAccent(OurWorldUiKit.AccentCoral);
            _titleText.text = isEn ? "CineAtlas · Dolby Atmos Cinema (3rd Floor)" : "CineAtlas · Dolby Atmos Sinema (3. Kat)";
            _subtitleText.text = isEn
                ? "Pick a movie screening — Includes buttered popcorn & energy boost!"
                : "Vizyondaki filmlerden birini seç — Patlamış mısır + enerji yenileme dahil!";

            VerticalLayoutGroup vlg = _contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;

            (string tr, string en, int price)[] movies =
            {
                ("1 · İSTANBUL GECELERİ  —  Romantik Dram  ·  35 ₺  (+45 Enerji · +15 XP)",
                 "1 · ISTANBUL NIGHTS  —  Romantic Drama  ·  35 ₺  (+45 Energy · +15 XP)", 35),
                ("2 · AYDOS'TA HIZ TUTKUSU  —  Aksiyon & Yarış  ·  40 ₺  (+50 Enerji · +20 XP)",
                 "2 · AYDOS VELOCITY  —  Action Racing  ·  40 ₺  (+50 Energy · +20 XP)", 40),
                ("3 · BOĞAZ'IN GİZEMİ  —  Macera & Gerilim  ·  35 ₺  (+45 Enerji · +18 XP)",
                 "3 · BOSPHORUS MYSTERY  —  Adventure  ·  35 ₺  (+45 Energy · +18 XP)", 35)
            };

            for (int i = 0; i < movies.Length; i++)
            {
                int movieIdx = i;
                int price = movies[i].price;
                OurWorldUiKit.CreatePillButton(
                    $"MovieBtn_{i}", _contentRoot, isEn ? movies[i].en : movies[i].tr, 21,
                    OurWorldUiKit.SurfaceBg, OurWorldUiKit.TextPrimary, OurWorldUiKit.AccentGold, out Text lbl, () =>
                    {
                        SaveManager sm = SaveManager.Instance;
                        if (sm != null && !sm.TrySpend(price))
                        {
                            GameFeelAudio.PlayUiClick();
                            PlayerModeController.Instance?.RaiseStatusMessage(isEn
                                ? $"Ticket costs {price} ₺!"
                                : $"Sinema bileti {price} ₺!");
                            return;
                        }
                        Close();
                        onWatchMovie?.Invoke(movieIdx);
                    });
                lbl.alignment = TextAnchor.MiddleLeft;
                lbl.rectTransform.offsetMin = new Vector2(26f, 4f);
                lbl.rectTransform.offsetMax = new Vector2(-26f, -4f);
            }

            OpenModal();
        }

        public void StartCinemaScreening(int movieIdx, Action onFinished)
        {
            StartCoroutine(CinemaScreeningRoutine(movieIdx, onFinished));
        }

        private IEnumerator CinemaScreeningRoutine(int movieIdx, Action onFinished)
        {
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            string[] titlesTr = { "CİNEATLAS · İSTANBUL GECELERİ", "CİNEATLAS · AYDOS'TA HIZ TUTKUSU", "CİNEATLAS · BOĞAZ'IN GİZEMİ" };
            string[] titlesEn = { "CINEATLAS · ISTANBUL NIGHTS", "CINEATLAS · AYDOS VELOCITY", "CINEATLAS · BOSPHORUS MYSTERY" };
            string[][] beatsTr =
            {
                new[] { "Işıklar sönüyor... Patlamış mısırın hazır, perde açılıyor!", "Kız Kulesi ışıkları Boğaz'da parlıyor — muhteşem bir açılış sahnesi...", "Salon alkışlarla inliyor! Harika bir İstanbul filmiydi." },
                new[] { "Dolby Atmos motor sesleri tüm salonu titretiyor!", "Aydos virajlarında nefes kesen gece yarışı doruğa ulaşıyor...", "Bitiş çizgisi geçildi! Adrenalin dolu muhteşem final!" },
                new[] { "Sisli bir İstanbul sabahında eski bir harita bulunuyor...", "Tarihi yarımadada soluksuz bir gizem çözülüyor...", "Büyük sır aydınlandı! İzleyiciler ayakta alkışlıyor!" }
            };
            string[][] beatsEn =
            {
                new[] { "Lights dimming... Popcorn ready as the CineAtlas curtain rises!", "Maiden's Tower glows over the Bosphorus — breathtaking cinematography...", "Applause fills the auditorium! A timeless Istanbul classic." },
                new[] { "Dolby Atmos engines roar across the theater!", "Midnight drifts through the Aydos mountain bends hit peak speed...", "Photo finish at the line! What an adrenaline rush!" },
                new[] { "An ancient Istanbul map surfaces in the morning mist...", "Clues unfold across historic streets at breakneck pace...", "The grand mystery is solved! Standing ovation in the hall!" }
            };
            Color[] sceneColors =
            {
                new Color(0.08f, 0.14f, 0.28f, 1f),
                new Color(0.24f, 0.10f, 0.16f, 1f),
                new Color(0.07f, 0.22f, 0.24f, 1f)
            };

            int idx = Mathf.Clamp(movieIdx, 0, 2);
            _skipCinemaRequested = false;
            _cinemaRoot.SetActive(true);
            _cinemaMovieTitle.text = isEn ? titlesEn[idx] : titlesTr[idx];
            if (PlayerModeController.Instance != null) PlayerModeController.Instance.SetModalLock(this, true);
            GameFeelAudio.PlayUiClick();

            string[] beats = isEn ? beatsEn[idx] : beatsTr[idx];
            const float totalDur = 6.0f;
            float elapsed = 0f;
            while (elapsed < totalDur && !_skipCinemaRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / totalDur);
                int bIdx = Mathf.Clamp(Mathf.FloorToInt(p * beats.Length), 0, beats.Length - 1);
                _cinemaCaption.text = beats[bIdx];
                _cinemaProgressFill.fillAmount = p;
                _cinemaScreenBg.color = Color.Lerp(sceneColors[bIdx], sceneColors[(bIdx + 1) % sceneColors.Length], Mathf.Repeat(p * 3f, 1f));
                yield return null;
            }

            _cinemaRoot.SetActive(false);
            if (PlayerModeController.Instance != null) PlayerModeController.Instance.SetModalLock(this, false);
            onFinished?.Invoke();
        }

        private void SetTopAccent(Color accent)
        {
            if (_topGlow != null)
            {
                _topGlow.color = new Color(accent.r, accent.g, accent.b, 0.18f);
            }
        }

        private void OpenModal()
        {
            _root.SetActive(true);
            GameFeelAudio.PlayUiClick();
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, true);
            }
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimateRoutine(true));
        }

        public void Close()
        {
            if (_root == null || !_root.activeSelf) return;
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimateRoutine(false));
        }

        private IEnumerator AnimateRoutine(bool opening)
        {
            const float dur = 0.15f;
            float t = 0f;
            float startA = opening ? 0f : 1f;
            float endA = opening ? 1f : 0f;
            Vector3 startS = opening ? Vector3.one * 0.94f : Vector3.one;
            Vector3 endS = opening ? Vector3.one : Vector3.one * 0.95f;

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                u = u * u * (3f - 2f * u);
                if (_cg != null) _cg.alpha = Mathf.Lerp(startA, endA, u);
                if (_card != null) _card.localScale = Vector3.Lerp(startS, endS, u);
                yield return null;
            }

            if (_cg != null) _cg.alpha = endA;
            if (_card != null) _card.localScale = endS;
            if (!opening && _root != null) _root.SetActive(false);
        }
    }
}
