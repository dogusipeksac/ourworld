using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Interactive Barber Tuncay Hair & Style Studio (Havadis Sokağı + Wardrobe/Phone access):
    /// - Spawns a rotating classic Turkish Barber Pole & interactive mirror kiosk right beside
    ///   Vendor_HavadisBerberCay (X=44.6, Z=180.5) at runtime.
    /// - Opens a flagship OurWorldUiKit studio allowing players to customize:
    ///   Hair Color (25 ₺ at Berber Tuncay), Pants Color, Jacket Style, and equip/toggle
    ///   3D Polarized Sunglasses (from Atlas Optik) & 3D Street Cap (from Mavi & Kotonica).
    /// </summary>
    public sealed class BarberAndStylePanel : MonoBehaviour
    {
        public static BarberAndStylePanel Instance { get; private set; }

        private const string BarberInteractId = "berber_tuncay_style";
        private static readonly string[] HairNamesTr = { "Gece Siyahı", "Kestane Kahve", "Karamel Sarı", "Gümüş Kır", "Bakır Kızıl", "Gece Mavisi" };
        private static readonly string[] HairNamesEn = { "Jet Black", "Chestnut Brown", "Caramel Blonde", "Silver Grey", "Copper Red", "Midnight Blue" };
        private static readonly string[] PantsNamesTr = { "Antrasit Klasik", "Koyu Denim", "Haki Keten", "Zeytin Kargo", "Bordo Chino", "Krem Yazlık" };
        private static readonly string[] PantsNamesEn = { "Charcoal Classic", "Dark Denim", "Khaki Linen", "Olive Cargo", "Burgundy Chino", "Cream Summer" };
        private static readonly string[] JacketNamesTr = { "Klasik Lacivert", "Bordo Deri", "Zeytin Parka", "Antrasit Sokak", "İstanbul Turkuazı", "Safran Hardal", "Krem Keten", "Mürdüm Kadife" };
        private static readonly string[] JacketNamesEn = { "Classic Navy", "Burgundy Leather", "Olive Parka", "Charcoal Street", "Istanbul Turquoise", "Saffron Mustard", "Cream Linen", "Plum Velvet" };

        private Canvas _canvas;
        private GameObject _root;
        private RectTransform _content;
        private Text _titleText;
        private Text _subtitleText;
        private Text _balanceText;
        private Transform _barberPoleStripe;
        private bool _isOpen;
        private bool _subscribed;

        public bool IsOpen => _isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("OurWorld_BarberAndStyleStudio");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<BarberAndStylePanel>();
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
            BuildBarberKioskInWorld();
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
            if (_barberPoleStripe != null)
            {
                _barberPoleStripe.Rotate(0f, 75f * Time.deltaTime, 0f, Space.Self);
            }
        }

        private void HandleInteracted(InteractableTrigger trig)
        {
            if (trig == null) return;
            if (trig.InteractableId == BarberInteractId)
            {
                Show();
            }
        }

        private void BuildBarberKioskInWorld()
        {
            if (GameObject.Find("Havadis_BerberTuncay_Pole") != null) return;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            GameObject root = new GameObject("Havadis_BerberTuncay_Pole");
            root.transform.position = new Vector3(39.8f, 0.04f, 180.5f);

            Material chrome = MakeMat(sh, new Color(0.84f, 0.88f, 0.92f), 0.85f, 0.78f);
            Material redMat = MakeMat(sh, new Color(0.86f, 0.16f, 0.18f), 0.55f, 0.08f);
            Material blueMat = MakeMat(sh, new Color(0.14f, 0.36f, 0.82f), 0.55f, 0.08f);
            Material whiteMat = MakeMat(sh, new Color(0.96f, 0.96f, 0.98f), 0.45f, 0.04f);

            MakePart("Base", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.46f, 0.12f, 0.46f), chrome);
            MakePart("PoleCore", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.24f, 0.90f, 0.24f), whiteMat);

            GameObject spinner = new GameObject("SpinningStripes");
            spinner.transform.SetParent(root.transform, false);
            spinner.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            _barberPoleStripe = spinner.transform;

            for (int i = 0; i < 6; i++)
            {
                float y = -0.68f + i * 0.27f;
                float ang = i * 48f;
                Material m = (i % 2 == 0) ? redMat : blueMat;
                GameObject band = MakePart($"Stripe_{i}", PrimitiveType.Cube, spinner.transform, new Vector3(0f, y, 0f), new Vector3(0.28f, 0.11f, 0.28f), m);
                band.transform.localRotation = Quaternion.Euler(14f, ang, 0f);
            }

            MakePart("CapTop", PrimitiveType.Sphere, root.transform, new Vector3(0f, 2.12f, 0f), new Vector3(0.32f, 0.28f, 0.32f), chrome);

            // Trigger collider for E interaction
            BoxCollider bc = root.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = new Vector3(0f, 1.1f, 0f);
            bc.size = new Vector3(2.4f, 2.2f, 2.4f);

            InteractableTrigger trig = root.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.InspectObject, BarberInteractId, "ui.btn.interact", 3.0f);
            trig.SetCustomPrompt("Berber Tuncay · Saç & Stil Stüdyosu", "Barber Tuncay · Hair & Style Studio");
            PlayerModeController.Instance?.RefreshInteractablesCache();
        }

        private static GameObject MakePart(string name, PrimitiveType type, Transform parent, Vector3 lp, Vector3 sc, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lp;
            go.transform.localScale = sc;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
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

        public void Show()
        {
            if (_root == null) BuildUi();
            _isOpen = true;
            _root.SetActive(true);
            PlayerModeController.Instance?.SetModalLock(this, true);
            RefreshView();
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            if (_root != null) _root.SetActive(false);
            PlayerModeController.Instance?.SetModalLock(this, false);
        }

        private void BuildUi()
        {
            OurWorldUiKit.EnsureInitialized();

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 196;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            _root = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(transform, false);
            RectTransform scRt = (RectTransform)_root.transform;
            scRt.anchorMin = Vector2.zero;
            scRt.anchorMax = Vector2.one;
            scRt.offsetMin = Vector2.zero;
            scRt.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = OurWorldUiKit.Scrim;

            GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(_root.transform, false);
            RectTransform crt = (RectTransform)card.transform;
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(940f, 700f);

            OurWorldUiKit.ApplyRounded(card.GetComponent<Image>(), OurWorldUiKit.CardSprite, OurWorldUiKit.CardBg);
            OurWorldUiKit.AddCardDropShadow(card.transform, 28f, -12f, 0.58f);
            OurWorldUiKit.AddSoftTopGlow(card.transform, OurWorldUiKit.AccentSky, 160f, 0.16f);
            OurWorldUiKit.AddHairlineBorder(card.transform, OurWorldUiKit.CardOutlineSprite, OurWorldUiKit.BorderSubtle);

            _titleText = OurWorldUiKit.CreateText("Title", card.transform, "BERBER TUNCAY · SAÇ & STİL STÜDYOSU",
                26, FontStyle.Bold, OurWorldUiKit.TextPrimary, TextAnchor.MiddleLeft);
            RectTransform trt = _titleText.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(32f, -24f);
            trt.sizeDelta = new Vector2(-240f, 36f);

            _subtitleText = OurWorldUiKit.CreateText("Sub", card.transform, "Saç rengi, pantolon, ceket ve 3D aksesuarlarını anında değiştir",
                16, FontStyle.Normal, OurWorldUiKit.TextSecondary, TextAnchor.MiddleLeft);
            RectTransform srt = _subtitleText.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(32f, -60f);
            srt.sizeDelta = new Vector2(-240f, 24f);

            _balanceText = OurWorldUiKit.CreateText("Balance", card.transform, "₺ 250",
                22, FontStyle.Bold, OurWorldUiKit.AccentGold, TextAnchor.MiddleRight);
            RectTransform brt = _balanceText.rectTransform;
            brt.anchorMin = new Vector2(1f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-84f, -28f);
            brt.sizeDelta = new Vector2(150f, 36f);

            Button closeBtn = OurWorldUiKit.CreateCircularIconButton(
                "Btn_Close", card.transform, OurWorldUiKit.IconClose, 44f, 18f,
                OurWorldUiKit.SurfaceBg, OurWorldUiKit.TextSecondary, OurWorldUiKit.BorderSubtle,
                () => { GameFeelAudio.PlayUiClick(); Close(); });
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
            scListRt.offsetMax = new Vector2(-28f, -98f);
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
            _content = (RectTransform)cont.transform;
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 700f);
            sr.content = _content;

            VerticalLayoutGroup vlg = cont.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14f;
            vlg.padding = new RectOffset(4, 4, 4, 14);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            ContentSizeFitter csf = cont.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void RefreshView()
        {
            bool en = LocalizationService.IsEnglish;
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            int money = d != null ? d.money : 0;

            _titleText.text = en ? "BARBER TUNCAY · HAIR & STYLE STUDIO" : "BERBER TUNCAY · SAÇ & STİL STÜDYOSU";
            _subtitleText.text = en
                ? "Customize hair color (25 ₺), pants, jacket & 3D accessories live"
                : "Saç boyası (25 ₺), pantolon, ceket ve 3D aksesuarlarını anında değiştir";
            _balanceText.text = $"₺ {money}";

            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                Destroy(_content.GetChild(i).gameObject);
            }

            // 1. Hair Color (Berber Tuncay 25 ₺)
            AddSectionHeader(en ? "HAIR COLOR · BARBER TUNCAY (25 ₺)" : "SAÇ RENGİ · BERBER TUNCAY (25 ₺)", OurWorldUiKit.AccentGold);
            AddColorGrid(PlayerFootController.HairColorCount,
                i => PlayerFootController.GetHairColor(i),
                i => en ? HairNamesEn[i] : HairNamesTr[i],
                d != null ? d.playerHairColorIndex : 0,
                idx => ApplyHairChoice(idx, en));

            // 2. 3D Accessories (Sunglasses & Street Cap)
            AddSectionHeader(en ? "3D HEAD ACCESSORIES" : "3D AKSESUARLAR (GÖZLÜK & ŞAPKA)", OurWorldUiKit.AccentTeal);
            BuildAccessoriesRow(d, en);

            // 3. Pants Color (Free wardrobe swap)
            AddSectionHeader(en ? "PANTS STYLE (FREE)" : "PANTOLON STİLİ (ÜCRETSİZ)", OurWorldUiKit.AccentSky);
            AddColorGrid(PlayerFootController.PantsColorCount,
                i => PlayerFootController.GetPantsColor(i),
                i => en ? PantsNamesEn[i] : PantsNamesTr[i],
                d != null ? d.playerPantsColorIndex : 0,
                idx => ApplyPantsChoice(idx, en));

            // 4. Jacket Style (Free wardrobe swap)
            AddSectionHeader(en ? "JACKET STYLE (FREE)" : "CEKET STİLİ (ÜCRETSİZ)", OurWorldUiKit.AccentCoral);
            AddColorGrid(PlayerFootController.JacketColorCount,
                i => PlayerFootController.GetJacketColor(i),
                i => en ? JacketNamesEn[i] : JacketNamesTr[i],
                d != null ? d.playerJacketColorIndex : 0,
                idx => ApplyJacketChoice(idx, en));
        }

        private void AddSectionHeader(string text, Color accent)
        {
            GameObject go = new GameObject("SecHeader", typeof(RectTransform));
            go.transform.SetParent(_content, false);
            go.AddComponent<LayoutElement>().preferredHeight = 28f;
            Text t = OurWorldUiKit.CreateStretchText("Label", go.transform, text, 16, FontStyle.Bold, accent, TextAnchor.MiddleLeft, 6f, 0f);
            t.raycastTarget = false;
        }

        private void AddColorGrid(int count, System.Func<int, Color> colorFn, System.Func<int, string> nameFn, int activeIdx, System.Action<int> onPick)
        {
            int rows = Mathf.CeilToInt(count / 3f);
            GameObject gridGo = new GameObject("Grid", typeof(RectTransform));
            gridGo.transform.SetParent(_content, false);
            gridGo.AddComponent<LayoutElement>().preferredHeight = rows * 62f + Mathf.Max(0, rows - 1) * 10f;

            GridLayoutGroup glg = gridGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(278f, 60f);
            glg.spacing = new Vector2(12f, 10f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 3;
            glg.childAlignment = TextAnchor.UpperLeft;

            for (int i = 0; i < count; i++)
            {
                int idx = i;
                bool selected = idx == activeIdx;
                GameObject item = new GameObject($"Opt_{i}", typeof(RectTransform), typeof(Image));
                item.transform.SetParent(gridGo.transform, false);
                Image bg = OurWorldUiKit.ApplyRounded(item.GetComponent<Image>(), OurWorldUiKit.SubCardSprite,
                    selected ? OurWorldUiKit.SurfaceActive : OurWorldUiKit.SurfaceBg);
                OurWorldUiKit.AddHairlineBorder(item.transform, OurWorldUiKit.SubCardOutlineSprite,
                    selected ? OurWorldUiKit.AccentTeal : OurWorldUiKit.BorderSubtle);

                GameObject sw = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
                sw.transform.SetParent(item.transform, false);
                RectTransform swRt = (RectTransform)sw.transform;
                swRt.anchorMin = new Vector2(0f, 0.5f);
                swRt.anchorMax = new Vector2(0f, 0.5f);
                swRt.pivot = new Vector2(0f, 0.5f);
                swRt.anchoredPosition = new Vector2(12f, 0f);
                swRt.sizeDelta = new Vector2(30f, 30f);
                OurWorldUiKit.ApplyRounded(sw.GetComponent<Image>(), OurWorldUiKit.CircleSprite, colorFn(i));
                OurWorldUiKit.AddHairlineBorder(sw.transform, OurWorldUiKit.CircleOutlineSprite, Color.white * 0.45f);

                Text lbl = OurWorldUiKit.CreateStretchText("Label", item.transform,
                    selected ? $"{nameFn(i)} ✓" : nameFn(i),
                    17, FontStyle.Bold, selected ? OurWorldUiKit.AccentTeal : OurWorldUiKit.TextPrimary,
                    TextAnchor.MiddleLeft, 52f, 4f);
                lbl.raycastTarget = false;

                Button btn = item.AddComponent<Button>();
                btn.targetGraphic = bg;
                UiButtonFeedback fb = item.AddComponent<UiButtonFeedback>();
                fb.Configure(btn, bg);
                btn.onClick.AddListener(() => onPick(idx));
            }
        }

        private void BuildAccessoriesRow(SaveData d, bool en)
        {
            GameObject row = new GameObject("AccessoriesRow", typeof(RectTransform));
            row.transform.SetParent(_content, false);
            row.AddComponent<LayoutElement>().preferredHeight = 72f;

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            bool ownsGlass = d != null && d.ownsSunglasses;
            bool eqGlass = ownsGlass && d.equipSunglasses;
            string glassTxt = !ownsGlass
                ? (en ? "3D Sunglasses · Buy (60 ₺)" : "3D Güneş Gözlüğü · Satın Al (60 ₺)")
                : (eqGlass ? (en ? "3D Sunglasses · WEARING ✓" : "3D Güneş Gözlüğü · TAKILI ✓") : (en ? "3D Sunglasses · Equip" : "3D Güneş Gözlüğü · Tak"));

            OurWorldUiKit.CreatePillButton("Btn_Sunglasses", row.transform, glassTxt, 17,
                eqGlass ? OurWorldUiKit.AccentTeal : OurWorldUiKit.SurfaceBg,
                eqGlass ? OurWorldUiKit.InkDark : OurWorldUiKit.TextPrimary,
                eqGlass ? Color.clear : OurWorldUiKit.BorderSubtle, out _, () => ToggleOrBuyAccessory(isSunglasses: true, en));

            bool ownsCap = d != null && d.ownsStreetCap;
            bool eqCap = ownsCap && d.equipStreetCap;
            string capTxt = !ownsCap
                ? (en ? "3D Street Cap · Buy (45 ₺)" : "3D Sokak Şapkası · Satın Al (45 ₺)")
                : (eqCap ? (en ? "3D Street Cap · WEARING ✓" : "3D Sokak Şapkası · TAKILI ✓") : (en ? "3D Street Cap · Equip" : "3D Sokak Şapkası · Tak"));

            OurWorldUiKit.CreatePillButton("Btn_StreetCap", row.transform, capTxt, 17,
                eqCap ? OurWorldUiKit.AccentTeal : OurWorldUiKit.SurfaceBg,
                eqCap ? OurWorldUiKit.InkDark : OurWorldUiKit.TextPrimary,
                eqCap ? Color.clear : OurWorldUiKit.BorderSubtle, out _, () => ToggleOrBuyAccessory(isSunglasses: false, en));
        }

        private void ApplyHairChoice(int idx, bool en)
        {
            SaveManager sm = SaveManager.Instance;
            if (sm == null || sm.Data == null) return;
            if (sm.Data.playerHairColorIndex == idx) { GameFeelAudio.PlayUiClick(); return; }

            if (!sm.TrySpend(25))
            {
                GameFeelAudio.PlayUiClick();
                PlayerModeController.Instance?.RaiseStatusMessage(en ? "Barber Tuncay haircut & dye costs 25 ₺!" : "Berber Tuncay saç boyama 25 ₺!");
                return;
            }

            sm.Data.playerHairColorIndex = idx;
            sm.Save();
            PlayerModeController.Instance?.FootController?.ApplyHairColor(idx);
            NeighborhoodReputation.AddReputation(10);
            GameFeelAudio.PlayCoinChime();
            if (PlayerModeController.Instance != null)
            {
                GameFeelAudio.SpawnBurst(PlayerModeController.Instance.ActiveWorldPosition + Vector3.up * 1.55f, PlayerFootController.GetHairColor(idx), 10, 0.45f);
                PlayerModeController.Instance.RaiseStatusMessage(en
                    ? $"Barber Tuncay styled your hair: {HairNamesEn[idx]}! (-25 ₺ · +10 XP)"
                    : $"Berber Tuncay saçını boyadı: {HairNamesTr[idx]}! (-25 ₺ · +10 XP)");
            }
            RefreshView();
        }

        private void ApplyPantsChoice(int idx, bool en)
        {
            SaveManager sm = SaveManager.Instance;
            if (sm == null || sm.Data == null) return;
            sm.Data.playerPantsColorIndex = idx;
            sm.Save();
            PlayerModeController.Instance?.FootController?.ApplyPantsColor(idx);
            GameFeelAudio.PlayUiClick();
            PlayerModeController.Instance?.RaiseStatusMessage(en ? $"Pants updated: {PantsNamesEn[idx]}" : $"Pantolon değiştirildi: {PantsNamesTr[idx]}");
            RefreshView();
        }

        private void ApplyJacketChoice(int idx, bool en)
        {
            SaveManager sm = SaveManager.Instance;
            if (sm == null || sm.Data == null) return;
            sm.Data.playerJacketColorIndex = idx;
            sm.Save();
            PlayerModeController.Instance?.FootController?.ApplyJacketColor(idx);
            GameFeelAudio.PlayUiClick();
            PlayerModeController.Instance?.RaiseStatusMessage(en ? $"Jacket updated: {JacketNamesEn[idx]}" : $"Ceket değiştirildi: {JacketNamesTr[idx]}");
            RefreshView();
        }

        private void ToggleOrBuyAccessory(bool isSunglasses, bool en)
        {
            SaveManager sm = SaveManager.Instance;
            if (sm == null || sm.Data == null) return;
            SaveData d = sm.Data;

            if (isSunglasses)
            {
                if (!d.ownsSunglasses)
                {
                    if (!sm.TrySpend(60))
                    {
                        GameFeelAudio.PlayUiClick();
                        PlayerModeController.Instance?.RaiseStatusMessage(en ? "Polarized Sunglasses cost 60 ₺!" : "Polarize Güneş Gözlüğü 60 ₺!");
                        return;
                    }
                    d.ownsSunglasses = true;
                    d.equipSunglasses = true;
                    NeighborhoodReputation.AddReputation(20);
                    GameFeelAudio.PlayCoinChime();
                }
                else
                {
                    d.equipSunglasses = !d.equipSunglasses;
                    GameFeelAudio.PlayUiClick();
                }
            }
            else
            {
                if (!d.ownsStreetCap)
                {
                    if (!sm.TrySpend(45))
                    {
                        GameFeelAudio.PlayUiClick();
                        PlayerModeController.Instance?.RaiseStatusMessage(en ? "3D Street Cap costs 45 ₺!" : "3D Sokak Şapkası 45 ₺!");
                        return;
                    }
                    d.ownsStreetCap = true;
                    d.equipStreetCap = true;
                    NeighborhoodReputation.AddReputation(15);
                    GameFeelAudio.PlayCoinChime();
                }
                else
                {
                    d.equipStreetCap = !d.equipStreetCap;
                    GameFeelAudio.PlayUiClick();
                }
            }

            sm.Save();
            PlayerModeController.Instance?.FootController?.ApplyHeadAccessories(d.ownsSunglasses && d.equipSunglasses, d.ownsStreetCap && d.equipStreetCap);
            RefreshView();
        }
    }
}
