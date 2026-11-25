using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Flagship swipeable "What's New in This Version" community release carousel.
    /// Uses the unified Istanbul Night Slate + Turquoise + Golden Amber rounded design kit
    /// with category SDF icons, scannable highlight chips, clickable capsule pagination,
    /// smooth open/slide motion, keyboard navigation, and direct "Fikir Yaz" CTA.
    /// </summary>
    public class WhatsNewOverlay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float CardWidth = 1120f;
        private const float CardHeight = 700f;
        private const int MaxTagChips = 3;

        private static readonly object ModalLockToken = new object();
        private static WhatsNewOverlay _active;

        private List<ReleaseHighlightItem> _items;
        private bool _isTr;
        private int _index;

        private CanvasGroup _rootGroup;
        private RectTransform _cardShellRect;
        private RectTransform _contentSlideRect;
        private Image _topGlow;
        private Image _cardBorder;
        private Image _medallionBg;
        private Image _medallionRing;
        private Image _medallionIcon;
        private Text _categoryLabel;
        private Text _slideTitleText;
        private Text _slideDescText;
        private Text _slideCounterText;
        private Image _requesterBg;
        private Image _requesterBorder;
        private Image _requesterStar;
        private Text _requesterText;

        private readonly GameObject[] _tagChipObjects = new GameObject[MaxTagChips];
        private readonly Image[] _tagChipDots = new Image[MaxTagChips];
        private readonly Text[] _tagChipLabels = new Text[MaxTagChips];

        private Button _prevBtn;
        private CanvasGroup _prevGroup;
        private Button _nextBtn;
        private CanvasGroup _nextGroup;
        private Button _primaryCtaBtn;
        private Image _primaryCtaBg;
        private Text _primaryCtaLabel;

        private readonly List<Image> _dots = new List<Image>();
        private readonly List<RectTransform> _dotRects = new List<RectTransform>();

        private Vector2 _dragStartPos;
        private float _dragDeltaX;
        private bool _isDragging;
        private float _animVelocity;
        private float _openAnimT;

        public static bool IsShowing => _active != null;

        public static void Show(string headerTitle, List<ReleaseHighlightItem> items, bool isTr)
        {
            if (items == null || items.Count == 0) return;
            Hide();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(ModalLockToken, true);
            }

            OurWorldUiKit.EnsureInitialized();

            GameObject canvasGo = new GameObject("OurWorld_WhatsNewCarousel");
            DontDestroyOnLoad(canvasGo);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            WhatsNewOverlay overlay = canvasGo.AddComponent<WhatsNewOverlay>();
            overlay._items = new List<ReleaseHighlightItem>(items);
            overlay._isTr = isTr;
            overlay._index = 0;
            overlay._openAnimT = 0f;
            overlay.BuildUi(headerTitle);
            overlay.RenderSlide(instant: true);

            _active = overlay;
        }

        public static void Hide()
        {
            if (_active != null)
            {
                Destroy(_active.gameObject);
                _active = null;
            }

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(ModalLockToken, false);
            }
        }

        private void BuildUi(string headerTitle)
        {
            // Fullscreen dimmed backdrop (captures horizontal drag/swipe gestures)
            GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            backdrop.transform.SetParent(transform, false);
            RectTransform backRt = (RectTransform)backdrop.transform;
            backRt.anchorMin = Vector2.zero;
            backRt.anchorMax = Vector2.one;
            backRt.offsetMin = Vector2.zero;
            backRt.offsetMax = Vector2.zero;
            Image backImg = backdrop.GetComponent<Image>();
            backImg.color = OurWorldUiKit.Scrim;
            backImg.raycastTarget = true;
            _rootGroup = backdrop.GetComponent<CanvasGroup>();
            _rootGroup.alpha = 0f;

            // Main centered elevated card shell
            GameObject cardGo = new GameObject("FeatureCard", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(backdrop.transform, false);
            _cardShellRect = (RectTransform)cardGo.transform;
            _cardShellRect.anchorMin = new Vector2(0.5f, 0.5f);
            _cardShellRect.anchorMax = new Vector2(0.5f, 0.5f);
            _cardShellRect.pivot = new Vector2(0.5f, 0.5f);
            _cardShellRect.anchoredPosition = Vector2.zero;
            _cardShellRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            _cardShellRect.localScale = Vector3.one * 0.94f;

            OurWorldUiKit.ApplyRounded(cardGo.GetComponent<Image>(), OurWorldUiKit.CardSprite, OurWorldUiKit.CardBg);
            OurWorldUiKit.AddCardDropShadow(cardGo.transform, spread: 30f, offsetY: -14f, alpha: 0.62f);

            // Soft top gradient glow inside the card
            _topGlow = OurWorldUiKit.AddSoftTopGlow(cardGo.transform, OurWorldUiKit.AccentTeal, heightPx: 220f, alpha: 0.15f);
            _cardBorder = OurWorldUiKit.AddHairlineBorder(cardGo.transform, OurWorldUiKit.CardOutlineSprite, OurWorldUiKit.BorderSubtle);

            // --- INTEGRATED HEADER BAR ---
            BuildHeaderBar(cardGo.transform, headerTitle);

            // --- SLIDEABLE CONTENT BODY ---
            GameObject bodyMaskGo = new GameObject("ContentViewport", typeof(RectTransform), typeof(RectMask2D));
            bodyMaskGo.transform.SetParent(cardGo.transform, false);
            RectTransform maskRt = (RectTransform)bodyMaskGo.transform;
            maskRt.anchorMin = new Vector2(0f, 0f);
            maskRt.anchorMax = new Vector2(1f, 1f);
            maskRt.offsetMin = new Vector2(24f, 108f);
            maskRt.offsetMax = new Vector2(-24f, -84f);

            GameObject slideGo = new GameObject("SlideContent", typeof(RectTransform));
            slideGo.transform.SetParent(bodyMaskGo.transform, false);
            _contentSlideRect = (RectTransform)slideGo.transform;
            _contentSlideRect.anchorMin = Vector2.zero;
            _contentSlideRect.anchorMax = Vector2.one;
            _contentSlideRect.offsetMin = Vector2.zero;
            _contentSlideRect.offsetMax = Vector2.zero;

            BuildSlideContent(slideGo.transform);

            // --- INTEGRATED FOOTER BAR ---
            OurWorldUiKit.AddDividerLine(cardGo.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(36f, 102f), new Vector2(-36f, 103.5f),
                OurWorldUiKit.Divider);
            BuildFooterBar(cardGo.transform);

            // --- FLOATING CIRCULAR LEFT / RIGHT CHEVRONS ---
            _prevBtn = OurWorldUiKit.CreateCircularIconButton(
                "Btn_Prev", backdrop.transform, OurWorldUiKit.IconChevronLeft,
                diameter: 58f, iconSize: 24f,
                bgColor: OurWorldUiKit.SurfaceBg,
                iconColor: OurWorldUiKit.TextPrimary,
                borderColor: OurWorldUiKit.BorderSubtle,
                onClick: () => StepSlide(-1));
            RectTransform prevRt = (RectTransform)_prevBtn.transform;
            prevRt.anchorMin = prevRt.anchorMax = new Vector2(0.5f, 0.5f);
            prevRt.anchoredPosition = new Vector2(-(CardWidth * 0.5f + 56f), 0f);
            _prevGroup = _prevBtn.gameObject.AddComponent<CanvasGroup>();

            _nextBtn = OurWorldUiKit.CreateCircularIconButton(
                "Btn_Next", backdrop.transform, OurWorldUiKit.IconChevronRight,
                diameter: 58f, iconSize: 24f,
                bgColor: OurWorldUiKit.SurfaceBg,
                iconColor: OurWorldUiKit.TextPrimary,
                borderColor: OurWorldUiKit.BorderSubtle,
                onClick: () => StepSlide(1));
            RectTransform nextRt = (RectTransform)_nextBtn.transform;
            nextRt.anchorMin = nextRt.anchorMax = new Vector2(0.5f, 0.5f);
            nextRt.anchoredPosition = new Vector2(CardWidth * 0.5f + 56f, 0f);
            _nextGroup = _nextBtn.gameObject.AddComponent<CanvasGroup>();
        }

        private void BuildHeaderBar(Transform cardParent, string headerTitle)
        {
            // Left: Version capsule pill with live accent dot
            GameObject verPill = new GameObject("VersionPill", typeof(RectTransform), typeof(Image));
            verPill.transform.SetParent(cardParent, false);
            RectTransform vpRt = (RectTransform)verPill.transform;
            vpRt.anchorMin = vpRt.anchorMax = new Vector2(0f, 1f);
            vpRt.pivot = new Vector2(0f, 1f);
            vpRt.anchoredPosition = new Vector2(36f, -24f);
            vpRt.sizeDelta = new Vector2(288f, 40f);
            OurWorldUiKit.ApplyRounded(verPill.GetComponent<Image>(), OurWorldUiKit.PillSprite, new Color(0.11f, 0.16f, 0.24f, 0.92f));
            OurWorldUiKit.AddHairlineBorder(verPill.transform, OurWorldUiKit.PillOutlineSprite, OurWorldUiKit.BorderSubtle);

            GameObject liveDot = new GameObject("LiveDot", typeof(RectTransform), typeof(Image));
            liveDot.transform.SetParent(verPill.transform, false);
            RectTransform ldRt = (RectTransform)liveDot.transform;
            ldRt.anchorMin = ldRt.anchorMax = new Vector2(0f, 0.5f);
            ldRt.pivot = new Vector2(0.5f, 0.5f);
            ldRt.anchoredPosition = new Vector2(22f, 0f);
            ldRt.sizeDelta = new Vector2(9f, 9f);
            Image ldImg = liveDot.GetComponent<Image>();
            ldImg.sprite = OurWorldUiKit.CircleSprite;
            ldImg.color = OurWorldUiKit.AccentTeal;
            ldImg.raycastTarget = false;

            string cleanHeader = string.IsNullOrWhiteSpace(headerTitle)
                ? (_isTr ? "YENİ SÜRÜM · v1.0.0" : "WHAT'S NEW · v1.0.0")
                : headerTitle.ToUpperInvariant();
            Text verTxt = OurWorldUiKit.CreateStretchText("VersionText", verPill.transform, cleanHeader,
                16, FontStyle.Bold, OurWorldUiKit.TextSecondary, TextAnchor.MiddleLeft, 36f, 0f);
            verTxt.rectTransform.offsetMax = new Vector2(-14f, 0f);

            // Right: Slide counter pill
            GameObject cntPill = new GameObject("CounterPill", typeof(RectTransform), typeof(Image));
            cntPill.transform.SetParent(cardParent, false);
            RectTransform cpRt = (RectTransform)cntPill.transform;
            cpRt.anchorMin = cpRt.anchorMax = new Vector2(1f, 1f);
            cpRt.pivot = new Vector2(1f, 1f);
            cpRt.anchoredPosition = new Vector2(-94f, -25f);
            cpRt.sizeDelta = new Vector2(78f, 38f);
            OurWorldUiKit.ApplyRounded(cntPill.GetComponent<Image>(), OurWorldUiKit.PillSprite, new Color(0.11f, 0.16f, 0.24f, 0.85f));
            OurWorldUiKit.AddHairlineBorder(cntPill.transform, OurWorldUiKit.PillOutlineSprite, OurWorldUiKit.BorderSubtle);
            _slideCounterText = OurWorldUiKit.CreateStretchText("CounterText", cntPill.transform, "1 / 4",
                16, FontStyle.Bold, OurWorldUiKit.TextSecondary, TextAnchor.MiddleCenter);

            // Far-Right: Crisp circular Close (X) button
            Button closeBtn = OurWorldUiKit.CreateCircularIconButton(
                "Btn_Close", cardParent, OurWorldUiKit.IconClose,
                diameter: 44f, iconSize: 17f,
                bgColor: new Color(0.14f, 0.19f, 0.27f, 0.92f),
                iconColor: OurWorldUiKit.TextSecondary,
                borderColor: OurWorldUiKit.BorderSubtle,
                onClick: () =>
                {
                    GameFeelAudio.PlayUiClick();
                    Hide();
                });
            RectTransform closeRt = (RectTransform)closeBtn.transform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-34f, -22f);
        }

        private void BuildSlideContent(Transform parent)
        {
            // 1. Hero Circular Icon Medallion (centered near top)
            GameObject medGo = new GameObject("HeroMedallion", typeof(RectTransform), typeof(Image));
            medGo.transform.SetParent(parent, false);
            RectTransform medRt = (RectTransform)medGo.transform;
            medRt.anchorMin = medRt.anchorMax = new Vector2(0.5f, 1f);
            medRt.pivot = new Vector2(0.5f, 1f);
            medRt.anchoredPosition = new Vector2(0f, -8f);
            medRt.sizeDelta = new Vector2(88f, 88f);
            _medallionBg = medGo.GetComponent<Image>();
            _medallionBg.sprite = OurWorldUiKit.CircleSprite;
            _medallionBg.color = new Color(OurWorldUiKit.AccentTeal.r, OurWorldUiKit.AccentTeal.g, OurWorldUiKit.AccentTeal.b, 0.15f);
            _medallionBg.raycastTarget = false;

            _medallionRing = OurWorldUiKit.AddHairlineBorder(medGo.transform, OurWorldUiKit.CircleOutlineSprite,
                new Color(OurWorldUiKit.AccentTeal.r, OurWorldUiKit.AccentTeal.g, OurWorldUiKit.AccentTeal.b, 0.55f));
            _medallionIcon = OurWorldUiKit.AddIcon("CategoryIcon", medGo.transform, OurWorldUiKit.IconWorld, 42f, OurWorldUiKit.AccentTeal);

            // 2. Category Eyebrow Kicker
            _categoryLabel = CreateTopAnchoredText("CategoryKicker", parent, 17, FontStyle.Bold,
                OurWorldUiKit.AccentTeal, yFromTop: -112f, width: 640f, height: 28f);

            // 3. Feature Title
            _slideTitleText = CreateTopAnchoredText("FeatureTitle", parent, 38, FontStyle.Bold,
                OurWorldUiKit.TextPrimary, yFromTop: -146f, width: 920f, height: 84f);
            _slideTitleText.lineSpacing = 1.12f;

            // 4. Scannable Feature Description
            _slideDescText = CreateTopAnchoredText("FeatureDesc", parent, 22, FontStyle.Normal,
                OurWorldUiKit.TextSecondary, yFromTop: -236f, width: 820f, height: 112f);
            _slideDescText.lineSpacing = 1.38f;

            // 5. Up to 3 Key Highlight Chips (instant 1-second scan!)
            const float chipW = 264f;
            const float chipGap = 16f;
            for (int i = 0; i < MaxTagChips; i++)
            {
                float cx = (i - 1) * (chipW + chipGap);
                GameObject chip = new GameObject($"TagChip_{i}", typeof(RectTransform), typeof(Image));
                chip.transform.SetParent(parent, false);
                RectTransform crt = (RectTransform)chip.transform;
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
                crt.pivot = new Vector2(0.5f, 1f);
                crt.anchoredPosition = new Vector2(cx, -366f);
                crt.sizeDelta = new Vector2(chipW, 42f);
                OurWorldUiKit.ApplyRounded(chip.GetComponent<Image>(), OurWorldUiKit.PillSprite, OurWorldUiKit.SurfaceBg);
                OurWorldUiKit.AddHairlineBorder(chip.transform, OurWorldUiKit.PillOutlineSprite, OurWorldUiKit.BorderSubtle);

                GameObject dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                dot.transform.SetParent(chip.transform, false);
                RectTransform drt = (RectTransform)dot.transform;
                drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f);
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.anchoredPosition = new Vector2(20f, 0f);
                drt.sizeDelta = new Vector2(8f, 8f);
                Image dImg = dot.GetComponent<Image>();
                dImg.sprite = OurWorldUiKit.CircleSprite;
                dImg.raycastTarget = false;

                Text lbl = OurWorldUiKit.CreateStretchText("Label", chip.transform, "", 17, FontStyle.Bold,
                    OurWorldUiKit.TextPrimary, TextAnchor.MiddleCenter, 28f, 0f);
                lbl.rectTransform.offsetMax = new Vector2(-14f, 0f);

                _tagChipObjects[i] = chip;
                _tagChipDots[i] = dImg;
                _tagChipLabels[i] = lbl;
            }

            // 6. Refined Compact Community Credit Capsule
            GameObject reqGo = new GameObject("RequesterChip", typeof(RectTransform), typeof(Image));
            reqGo.transform.SetParent(parent, false);
            RectTransform reqRt = (RectTransform)reqGo.transform;
            reqRt.anchorMin = reqRt.anchorMax = new Vector2(0.5f, 1f);
            reqRt.pivot = new Vector2(0.5f, 1f);
            reqRt.anchoredPosition = new Vector2(0f, -434f);
            reqRt.sizeDelta = new Vector2(540f, 40f);
            _requesterBg = OurWorldUiKit.ApplyRounded(reqGo.GetComponent<Image>(), OurWorldUiKit.PillSprite,
                new Color(OurWorldUiKit.AccentGold.r, OurWorldUiKit.AccentGold.g, OurWorldUiKit.AccentGold.b, 0.11f));
            _requesterBg.raycastTarget = false;
            _requesterBorder = OurWorldUiKit.AddHairlineBorder(reqGo.transform, OurWorldUiKit.PillOutlineSprite,
                new Color(OurWorldUiKit.AccentGold.r, OurWorldUiKit.AccentGold.g, OurWorldUiKit.AccentGold.b, 0.34f));

            _requesterStar = OurWorldUiKit.AddIcon("StarIcon", reqGo.transform, OurWorldUiKit.IconStar, 18f, OurWorldUiKit.AccentGold, new Vector2(-240f, 0f));
            _requesterText = OurWorldUiKit.CreateStretchText("RequesterText", reqGo.transform, "",
                17, FontStyle.Bold, OurWorldUiKit.AccentGold, TextAnchor.MiddleCenter, 40f, 0f);
        }

        private void BuildFooterBar(Transform cardParent)
        {
            // Left: Outlined Secondary Button ("Fikir Yaz")
            string suggestLabel = LocalizationService.Tr("whatsnew.suggest", _isTr ? "Fikir Yaz" : "Suggest Idea");
            Button suggestBtn = OurWorldUiKit.CreatePillButton(
                "Btn_SuggestIdea", cardParent, suggestLabel, 19,
                bgColor: new Color(0.11f, 0.16f, 0.24f, 0.94f),
                textColor: OurWorldUiKit.AccentGold,
                borderColor: new Color(OurWorldUiKit.AccentGold.r, OurWorldUiKit.AccentGold.g, OurWorldUiKit.AccentGold.b, 0.42f),
                out Text sugLbl,
                onClick: OpenCommunityIdeasTab);
            RectTransform sugRt = (RectTransform)suggestBtn.transform;
            sugRt.anchorMin = sugRt.anchorMax = new Vector2(0f, 0f);
            sugRt.pivot = new Vector2(0f, 0f);
            sugRt.anchoredPosition = new Vector2(36f, 24f);
            sugRt.sizeDelta = new Vector2(226f, 54f);
            OurWorldUiKit.AddIcon("StarIcon", suggestBtn.transform, OurWorldUiKit.IconStar, 18f, OurWorldUiKit.AccentGold, new Vector2(-78f, 0f));
            sugLbl.rectTransform.offsetMin = new Vector2(26f, 0f);

            // Center: Clickable Capsule Pagination Dots
            BuildDots(cardParent);

            // Right: Primary Action Pill ("Devam Et →" / "Oyuna Başla!")
            _primaryCtaBtn = OurWorldUiKit.CreatePillButton(
                "Btn_PrimaryCta", cardParent, "", 20,
                bgColor: OurWorldUiKit.AccentTeal,
                textColor: OurWorldUiKit.InkDark,
                borderColor: Color.clear,
                out _primaryCtaLabel,
                onClick: HandlePrimaryCta);
            _primaryCtaBg = _primaryCtaBtn.GetComponent<Image>();
            RectTransform ctaRt = (RectTransform)_primaryCtaBtn.transform;
            ctaRt.anchorMin = ctaRt.anchorMax = new Vector2(1f, 0f);
            ctaRt.pivot = new Vector2(1f, 0f);
            ctaRt.anchoredPosition = new Vector2(-36f, 24f);
            ctaRt.sizeDelta = new Vector2(246f, 54f);
        }

        private void BuildDots(Transform cardParent)
        {
            _dots.Clear();
            _dotRects.Clear();

            GameObject rowGo = new GameObject("PaginationDots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(cardParent, false);
            RectTransform rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 0f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.anchoredPosition = new Vector2(0f, 51f);
            rowRt.sizeDelta = new Vector2(260f, 24f);

            HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            for (int i = 0; i < _items.Count; i++)
            {
                int targetIdx = i;
                GameObject dotGo = new GameObject($"Dot_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                dotGo.transform.SetParent(rowGo.transform, false);
                RectTransform drt = (RectTransform)dotGo.transform;
                drt.sizeDelta = new Vector2(10f, 10f);

                Image img = dotGo.GetComponent<Image>();
                img.sprite = OurWorldUiKit.PillSprite;
                img.type = Image.Type.Sliced;
                img.color = new Color(0.52f, 0.60f, 0.72f, 0.45f);

                Button btn = dotGo.GetComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => JumpToSlide(targetIdx));

                _dots.Add(img);
                _dotRects.Add(drt);
            }
        }

        private void RenderSlide(bool instant)
        {
            if (_items == null || _items.Count == 0) return;
            _index = Mathf.Clamp(_index, 0, _items.Count - 1);
            ReleaseHighlightItem item = _items[_index];

            Color accent = ResolveCategoryColor(item.category);

            if (_topGlow != null) _topGlow.color = new Color(accent.r, accent.g, accent.b, 0.15f);
            if (_cardBorder != null) _cardBorder.color = Color.Lerp(OurWorldUiKit.BorderSubtle, accent, 0.22f);
            if (_medallionBg != null) _medallionBg.color = new Color(accent.r, accent.g, accent.b, 0.15f);
            if (_medallionRing != null) _medallionRing.color = new Color(accent.r, accent.g, accent.b, 0.50f);
            if (_medallionIcon != null)
            {
                _medallionIcon.sprite = ResolveCategoryIcon(item.category);
                _medallionIcon.color = accent;
            }

            if (_categoryLabel != null)
            {
                _categoryLabel.color = accent;
                _categoryLabel.text = ResolveCategoryLabel(item.category, item.fromLiveFirestore, _isTr);
            }

            if (_slideCounterText != null)
            {
                _slideCounterText.text = $"{_index + 1} / {_items.Count}";
            }

            if (_slideTitleText != null) _slideTitleText.text = item.GetTitle(_isTr);
            if (_slideDescText != null) _slideDescText.text = item.GetDescription(_isTr);

            // Key highlight chips (up to 3)
            string[] tags = item.GetTags(_isTr);
            int tagCount = Mathf.Min(tags.Length, MaxTagChips);
            float chipW = 264f;
            float chipGap = 16f;
            float totalRowW = tagCount > 0 ? (tagCount * chipW + (tagCount - 1) * chipGap) : 0f;
            for (int i = 0; i < MaxTagChips; i++)
            {
                bool active = i < tagCount && !string.IsNullOrWhiteSpace(tags[i]);
                _tagChipObjects[i].SetActive(active);
                if (active)
                {
                    float cx = -totalRowW * 0.5f + chipW * 0.5f + i * (chipW + chipGap);
                    ((RectTransform)_tagChipObjects[i].transform).anchoredPosition = new Vector2(cx, -366f);
                    _tagChipDots[i].color = accent;
                    _tagChipLabels[i].text = tags[i];
                }
            }

            // Community credit pill
            string requester = string.IsNullOrWhiteSpace(item.requestedBy)
                ? (_isTr ? "OurWorld Topluluğu" : "OurWorld Community")
                : item.requestedBy.Trim();
            if (_requesterText != null)
            {
                _requesterText.text = _isTr
                    ? $"Topluluk İsteği  ·  {requester}"
                    : $"Requested by  ·  {requester}";
            }
            if (_requesterBg != null)
            {
                Color c = item.fromLiveFirestore ? OurWorldUiKit.AccentGold : Color.Lerp(OurWorldUiKit.AccentGold, accent, 0.25f);
                _requesterBg.color = new Color(c.r, c.g, c.b, item.fromLiveFirestore ? 0.16f : 0.11f);
                if (_requesterBorder != null) _requesterBorder.color = new Color(c.r, c.g, c.b, 0.36f);
                if (_requesterStar != null) _requesterStar.color = OurWorldUiKit.AccentGold;
            }

            bool isFirst = _index == 0;
            bool isLast = _index == _items.Count - 1;
            if (_prevBtn != null) _prevBtn.interactable = !isFirst;
            if (_prevGroup != null) _prevGroup.alpha = isFirst ? 0.28f : 1f;
            if (_nextBtn != null) _nextBtn.interactable = !isLast;
            if (_nextGroup != null) _nextGroup.alpha = isLast ? 0.28f : 1f;

            if (_primaryCtaLabel != null)
            {
                _primaryCtaLabel.text = isLast
                    ? LocalizationService.Tr("whatsnew.play", _isTr ? "Oyuna Başla!" : "Let's Play!")
                    : LocalizationService.Tr("whatsnew.next", _isTr ? "Devam Et  →" : "Continue  →");
            }
            if (_primaryCtaBg != null)
            {
                _primaryCtaBg.color = isLast ? OurWorldUiKit.AccentTeal : accent;
            }

            for (int i = 0; i < _dots.Count; i++)
            {
                bool active = i == _index;
                _dots[i].color = active ? accent : new Color(0.54f, 0.62f, 0.72f, 0.38f);
                _dotRects[i].sizeDelta = active ? new Vector2(34f, 10f) : new Vector2(10f, 10f);
            }

            if (instant && _contentSlideRect != null)
            {
                _contentSlideRect.anchoredPosition = Vector2.zero;
            }
        }

        private void StepSlide(int delta)
        {
            JumpToSlide(_index + delta);
        }

        private void JumpToSlide(int targetIndex)
        {
            int next = Mathf.Clamp(targetIndex, 0, _items.Count - 1);
            if (next == _index) return;
            int dir = next > _index ? 1 : -1;
            _index = next;
            GameFeelAudio.PlayUiClick();
            if (_contentSlideRect != null)
            {
                _contentSlideRect.anchoredPosition = new Vector2(dir * 95f, 0f);
            }
            RenderSlide(instant: false);
        }

        private void HandlePrimaryCta()
        {
            GameFeelAudio.PlayUiClick();
            if (_index < _items.Count - 1)
            {
                StepSlide(1);
            }
            else
            {
                Hide();
            }
        }

        private void OpenCommunityIdeasTab()
        {
            GameFeelAudio.PlayUiClick();
            Hide();
            if (PhonePanelController.Instance != null)
            {
                PhonePanelController.Instance.OpenFeedbackTab();
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Smooth entrance fade + gentle scale settle
            if (_openAnimT < 1f)
            {
                _openAnimT = Mathf.MoveTowards(_openAnimT, 1f, dt * 5.2f);
                float ease = 1f - Mathf.Pow(1f - _openAnimT, 3f);
                if (_rootGroup != null) _rootGroup.alpha = ease;
                if (_cardShellRect != null) _cardShellRect.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, ease);
            }

            // Smooth spring-back animation for horizontal slide transition
            if (!_isDragging && _contentSlideRect != null)
            {
                Vector2 pos = _contentSlideRect.anchoredPosition;
                if (Mathf.Abs(pos.x) > 0.5f)
                {
                    float nextX = Mathf.SmoothDamp(pos.x, 0f, ref _animVelocity, 0.10f, Mathf.Infinity, dt);
                    _contentSlideRect.anchoredPosition = new Vector2(nextX, 0f);
                }
                else if (pos.x != 0f)
                {
                    _contentSlideRect.anchoredPosition = Vector2.zero;
                }
            }

            if (Keyboard.current == null) return;
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame) StepSlide(1);
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame) StepSlide(-1);
            else if (Keyboard.current.escapeKey.wasPressedThisFrame) Hide();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _dragStartPos = eventData.position;
            _dragDeltaX = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _contentSlideRect == null) return;
            _dragDeltaX = eventData.position.x - _dragStartPos.x;
            float clamped = Mathf.Clamp(_dragDeltaX * 0.42f, -150f, 150f);
            _contentSlideRect.anchoredPosition = new Vector2(clamped, 0f);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging = false;
            const float swipeThresholdPx = 70f;
            if (_dragDeltaX < -swipeThresholdPx && _index < _items.Count - 1)
            {
                StepSlide(1);
            }
            else if (_dragDeltaX > swipeThresholdPx && _index > 0)
            {
                StepSlide(-1);
            }
        }

        private static Color ResolveCategoryColor(string cat)
        {
            switch ((cat ?? string.Empty).ToLowerInvariant())
            {
                case "world": return OurWorldUiKit.AccentTeal;
                case "sports":
                case "vehicle": return OurWorldUiKit.AccentSky;
                case "mechanic": return OurWorldUiKit.AccentCoral;
                default: return OurWorldUiKit.AccentGold;
            }
        }

        private static Sprite ResolveCategoryIcon(string cat)
        {
            switch ((cat ?? string.Empty).ToLowerInvariant())
            {
                case "world": return OurWorldUiKit.IconWorld;
                case "sports": return OurWorldUiKit.IconSports;
                case "mechanic":
                case "vehicle": return OurWorldUiKit.IconMechanic;
                default: return OurWorldUiKit.IconCommunity;
            }
        }

        private static string ResolveCategoryLabel(string cat, bool fromLive, bool isTr)
        {
            if (fromLive) return isTr ? "TOPLULUKTAN EKLENDİ" : "COMMUNITY REQUEST";
            switch ((cat ?? string.Empty).ToLowerInvariant())
            {
                case "world": return isTr ? "HARİTA & HİKÂYE" : "WORLD & STORY";
                case "sports": return isTr ? "SPOR & POLİGON" : "SPORTS & RANGE";
                case "vehicle": return isTr ? "ARAÇ & SÜRÜŞ" : "VEHICLES & DRIVING";
                case "mechanic": return isTr ? "YAŞAM & SİMÜLASYON" : "LIFE & SIMULATION";
                default: return isTr ? "TOPLULUK" : "COMMUNITY";
            }
        }

        private static Text CreateTopAnchoredText(string name, Transform parent, int fontSize, FontStyle style, Color color, float yFromTop, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yFromTop);
            rt.sizeDelta = new Vector2(width, height);

            Text txt = go.GetComponent<Text>();
            txt.font = OurWorldUiKit.DefaultFont;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.raycastTarget = false;
            return txt;
        }
    }
}
