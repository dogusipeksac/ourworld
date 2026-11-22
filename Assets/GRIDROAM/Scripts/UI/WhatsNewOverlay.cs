using System.Collections.Generic;
using Gridroam.Core;
using Gridroam.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// Fullscreen swipeable "What's New in This Version" community release carousel.
    /// Showcases each new feature AND prominently credits the player who requested it
    /// ("★ BU ÖZELLİĞİ [İSİM] İSTEDİ"). Supports touch/mouse horizontal swipe,
    /// arrow buttons, dot pagination, keyboard navigation, and a direct "Sen de Fikir Yaz" CTA.
    /// </summary>
    public class WhatsNewOverlay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static readonly Color BackdropColor = new Color(0.04f, 0.06f, 0.10f, 0.94f);
        private static readonly Color CardBg = new Color(0.09f, 0.12f, 0.18f, 0.99f);
        private static readonly Color SurfaceBg = new Color(0.13f, 0.17f, 0.24f, 0.95f);
        private static readonly Color TextWhite = new Color(0.96f, 0.97f, 0.99f, 1f);
        private static readonly Color TextDim = new Color(0.76f, 0.82f, 0.88f, 1f);
        private static readonly Color AccentTeal = new Color(0.24f, 0.78f, 0.72f, 1f);
        private static readonly Color AccentGold = new Color(0.98f, 0.76f, 0.26f, 1f);
        private static readonly Color AccentSky = new Color(0.32f, 0.72f, 0.98f, 1f);
        private static readonly Color AccentCoral = new Color(0.96f, 0.44f, 0.34f, 1f);

        private static readonly object ModalLockToken = new object();
        private static WhatsNewOverlay _active;

        private List<ReleaseHighlightItem> _items;
        private bool _isTr;
        private int _index;

        private RectTransform _slideCardRect;
        private Image _accentTopBar;
        private Image _categoryBadgeBg;
        private Text _categoryBadgeText;
        private Image _requesterRibbonBg;
        private Text _requesterText;
        private Text _slideTitleText;
        private Text _slideDescText;
        private Text _slideCounterText;
        private Button _prevBtn;
        private Button _nextBtn;
        private Button _primaryCtaBtn;
        private Text _primaryCtaLabel;
        private Button _suggestBtn;
        private readonly List<Image> _dots = new List<Image>();

        private Vector2 _dragStartPos;
        private float _dragDeltaX;
        private bool _isDragging;
        private float _animVelocity;

        public static bool IsShowing => _active != null;

        public static void Show(string headerTitle, List<ReleaseHighlightItem> items, bool isTr)
        {
            if (items == null || items.Count == 0) return;
            Hide();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(ModalLockToken, true);
            }

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
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 24);

            // Fullscreen backdrop (also receives drag/swipe events!)
            GameObject backdrop = CreatePanel("Backdrop", transform, Vector2.zero, Vector2.one, BackdropColor);
            backdrop.GetComponent<Image>().raycastTarget = true;

            // Header bar: Version pill + Skip button
            GameObject headerPill = CreatePanel("HeaderPill", backdrop.transform,
                new Vector2(0.28f, 0.87f), new Vector2(0.72f, 0.945f), new Color(0.14f, 0.24f, 0.32f, 0.95f));
            CreateText("HeaderLabel", headerPill.transform, font,
                string.IsNullOrWhiteSpace(headerTitle) ? (_isTr ? "YENİ SÜRÜMDE EKLENDİ" : "WHAT'S NEW") : headerTitle.ToUpperInvariant(),
                24, FontStyle.Bold, AccentTeal, Vector2.zero, Vector2.one);

            string skipText = LocalizationService.Tr("whatsnew.skip", _isTr ? "ATLA  ✕" : "SKIP  ✕");
            Button skipBtn = CreateButton("Btn_Skip", backdrop.transform, font, skipText,
                new Color(0.16f, 0.20f, 0.27f, 0.92f), TextDim,
                new Vector2(0.84f, 0.875f), new Vector2(0.955f, 0.94f), 21);
            skipBtn.onClick.AddListener(Hide);

            // Swipeable center feature card
            GameObject cardGo = CreatePanel("FeatureCard", backdrop.transform,
                new Vector2(0.13f, 0.20f), new Vector2(0.87f, 0.835f), CardBg);
            _slideCardRect = (RectTransform)cardGo.transform;

            GameObject topBar = CreatePanel("TopAccentBar", cardGo.transform,
                new Vector2(0f, 0.982f), new Vector2(1f, 1f), AccentTeal);
            _accentTopBar = topBar.GetComponent<Image>();

            // Category badge (top-left) + Slide counter (top-right)
            GameObject catBg = CreatePanel("CategoryBadge", cardGo.transform,
                new Vector2(0.05f, 0.84f), new Vector2(0.34f, 0.925f), new Color(0.24f, 0.78f, 0.72f, 0.18f));
            _categoryBadgeBg = catBg.GetComponent<Image>();
            _categoryBadgeText = CreateText("CategoryLabel", catBg.transform, font, "YENİ ÖZELLİK",
                21, FontStyle.Bold, AccentTeal, Vector2.zero, Vector2.one);

            _slideCounterText = CreateText("SlideCounter", cardGo.transform, font, "1 / 4",
                22, FontStyle.Bold, TextDim, new Vector2(0.82f, 0.84f), new Vector2(0.95f, 0.925f));
            _slideCounterText.alignment = TextAnchor.MiddleRight;

            // HERO "REQUESTED BY" PLAYER CREDIT RIBBON!
            GameObject reqRibbon = CreatePanel("RequesterRibbon", cardGo.transform,
                new Vector2(0.08f, 0.65f), new Vector2(0.92f, 0.795f), new Color(0.98f, 0.76f, 0.26f, 0.18f));
            _requesterRibbonBg = reqRibbon.GetComponent<Image>();
            _requesterText = CreateText("RequesterLabel", reqRibbon.transform, font, "",
                28, FontStyle.Bold, AccentGold, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f));

            // Feature Title
            _slideTitleText = CreateText("FeatureTitle", cardGo.transform, font, "",
                46, FontStyle.Bold, TextWhite, new Vector2(0.06f, 0.43f), new Vector2(0.94f, 0.62f));

            // Feature Description
            _slideDescText = CreateText("FeatureDesc", cardGo.transform, font, "",
                27, FontStyle.Normal, TextDim, new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.42f));
            _slideDescText.lineSpacing = 1.25f;

            // Subtle swipe hint inside card bottom
            string swipeHint = LocalizationService.Tr("whatsnew.swipe_hint",
                _isTr ? "←  Kaydırarak diğer özellikleri gör  →" : "←  Swipe to browse new features  →");
            CreateText("SwipeHint", cardGo.transform, font, swipeHint,
                19, FontStyle.Italic, new Color(0.58f, 0.65f, 0.74f, 0.9f),
                new Vector2(0.15f, 0.03f), new Vector2(0.85f, 0.10f));

            // Left & Right Navigation Arrows
            _prevBtn = CreateButton("Btn_Prev", backdrop.transform, font, "‹", SurfaceBg, TextWhite,
                new Vector2(0.035f, 0.44f), new Vector2(0.105f, 0.60f), 56);
            _prevBtn.onClick.AddListener(() => StepSlide(-1));

            _nextBtn = CreateButton("Btn_Next", backdrop.transform, font, "›", SurfaceBg, TextWhite,
                new Vector2(0.895f, 0.44f), new Vector2(0.965f, 0.60f), 56);
            _nextBtn.onClick.AddListener(() => StepSlide(1));

            // Page Indicator Dots
            BuildDots(backdrop.transform);

            // Bottom Action Bar: "Sen de Fikir Yaz" + Primary CTA ("Sonraki ->" / "Oyuna Başla!")
            string suggestLabel = LocalizationService.Tr("whatsnew.suggest",
                _isTr ? "SEN DE FİKİR YAZ" : "SUGGEST AN IDEA");
            _suggestBtn = CreateButton("Btn_SuggestIdea", backdrop.transform, font, suggestLabel,
                new Color(0.16f, 0.24f, 0.34f, 0.96f), AccentGold,
                new Vector2(0.20f, 0.045f), new Vector2(0.47f, 0.125f), 24);
            _suggestBtn.onClick.AddListener(OpenCommunityIdeasTab);

            _primaryCtaBtn = CreateButton("Btn_PrimaryCta", backdrop.transform, font, "",
                AccentTeal, new Color(0.04f, 0.10f, 0.10f, 1f),
                new Vector2(0.51f, 0.045f), new Vector2(0.80f, 0.125f), 26);
            _primaryCtaLabel = _primaryCtaBtn.GetComponentInChildren<Text>();
            _primaryCtaBtn.onClick.AddListener(HandlePrimaryCta);
        }

        private void BuildDots(Transform parent)
        {
            _dots.Clear();
            int count = _items.Count;
            float spacing = 0.022f;
            float totalW = (count - 1) * spacing;
            float startX = 0.5f - totalW * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float cx = startX + i * spacing;
                GameObject dot = CreatePanel($"Dot_{i}", parent,
                    new Vector2(cx - 0.006f, 0.148f), new Vector2(cx + 0.006f, 0.172f),
                    new Color(0.45f, 0.52f, 0.62f, 0.65f));
                _dots.Add(dot.GetComponent<Image>());
            }
        }

        private void RenderSlide(bool instant)
        {
            if (_items == null || _items.Count == 0) return;
            _index = Mathf.Clamp(_index, 0, _items.Count - 1);
            ReleaseHighlightItem item = _items[_index];

            Color accent = ResolveCategoryColor(item.category);
            if (_accentTopBar != null) _accentTopBar.color = accent;
            if (_categoryBadgeBg != null) _categoryBadgeBg.color = new Color(accent.r, accent.g, accent.b, 0.18f);
            if (_categoryBadgeText != null)
            {
                _categoryBadgeText.color = accent;
                _categoryBadgeText.text = ResolveCategoryLabel(item.category, item.fromLiveFirestore, _isTr);
            }

            if (_slideCounterText != null)
            {
                _slideCounterText.text = $"{_index + 1} / {_items.Count}";
            }

            if (_requesterText != null)
            {
                string requester = string.IsNullOrWhiteSpace(item.requestedBy)
                    ? (_isTr ? "OurWorld Topluluğu" : "OurWorld Community")
                    : item.requestedBy.Trim();
                _requesterText.text = _isTr
                    ? $"★   BU ÖZELLİĞİ  {requester.ToUpperInvariant()}  İSTEDİ!"
                    : $"★   REQUESTED BY  {requester.ToUpperInvariant()}!";
            }

            if (_requesterRibbonBg != null)
            {
                _requesterRibbonBg.color = item.fromLiveFirestore
                    ? new Color(AccentGold.r, AccentGold.g, AccentGold.b, 0.24f)
                    : new Color(accent.r, accent.g, accent.b, 0.16f);
            }

            if (_slideTitleText != null) _slideTitleText.text = item.GetTitle(_isTr);
            if (_slideDescText != null) _slideDescText.text = item.GetDescription(_isTr);

            bool isFirst = _index == 0;
            bool isLast = _index == _items.Count - 1;
            if (_prevBtn != null) _prevBtn.interactable = !isFirst;
            if (_nextBtn != null) _nextBtn.interactable = !isLast;

            if (_primaryCtaLabel != null)
            {
                _primaryCtaLabel.text = isLast
                    ? LocalizationService.Tr("whatsnew.play", _isTr ? "OYUNA BAŞLA!" : "LET'S PLAY!")
                    : LocalizationService.Tr("whatsnew.next", _isTr ? "SONRAKİ  →" : "NEXT  →");
            }

            for (int i = 0; i < _dots.Count; i++)
            {
                _dots[i].color = i == _index ? accent : new Color(0.45f, 0.52f, 0.62f, 0.45f);
                _dots[i].transform.localScale = i == _index ? new Vector3(1.45f, 1.45f, 1f) : Vector3.one;
            }

            if (instant && _slideCardRect != null)
            {
                _slideCardRect.anchoredPosition = Vector2.zero;
            }
        }

        private void StepSlide(int delta)
        {
            int next = Mathf.Clamp(_index + delta, 0, _items.Count - 1);
            if (next == _index) return;
            _index = next;
            GameFeelAudio.PlayUiClick();
            if (_slideCardRect != null)
            {
                _slideCardRect.anchoredPosition = new Vector2(delta > 0 ? 110f : -110f, 0f);
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
            // Smooth spring-back animation for swipe / step transition
            if (!_isDragging && _slideCardRect != null)
            {
                Vector2 pos = _slideCardRect.anchoredPosition;
                if (Mathf.Abs(pos.x) > 0.5f)
                {
                    float nextX = Mathf.SmoothDamp(pos.x, 0f, ref _animVelocity, 0.11f, Mathf.Infinity, Time.unscaledDeltaTime);
                    _slideCardRect.anchoredPosition = new Vector2(nextX, 0f);
                }
                else if (pos.x != 0f)
                {
                    _slideCardRect.anchoredPosition = Vector2.zero;
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
            if (!_isDragging || _slideCardRect == null) return;
            _dragDeltaX = eventData.position.x - _dragStartPos.x;
            float clamped = Mathf.Clamp(_dragDeltaX * 0.45f, -180f, 180f);
            _slideCardRect.anchoredPosition = new Vector2(clamped, 0f);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging = false;
            const float swipeThresholdPx = 75f;
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
                case "world": return AccentTeal;
                case "vehicle": return AccentSky;
                case "mechanic": return AccentCoral;
                default: return AccentGold;
            }
        }

        private static string ResolveCategoryLabel(string cat, bool fromLive, bool isTr)
        {
            if (fromLive) return isTr ? "TOPLULUKTAN EKLENDİ" : "COMMUNITY REQUEST";
            switch ((cat ?? string.Empty).ToLowerInvariant())
            {
                case "world": return isTr ? "HARİTA & DÜNYA" : "WORLD & MAP";
                case "vehicle": return isTr ? "ARAÇ & SÜRÜŞ" : "VEHICLES & DRIVING";
                case "mechanic": return isTr ? "YENİ MEKANİK" : "NEW MECHANIC";
                default: return isTr ? "TOPLULUK" : "COMMUNITY";
            }
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int fontSize, FontStyle style, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.raycastTarget = false;
            return txt;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label, Color bgColor, Color textColor, Vector2 anchorMin, Vector2 anchorMax, int fontSize)
        {
            GameObject go = CreatePanel(name, parent, anchorMin, anchorMax, bgColor);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            UiButtonFeedback fb = go.AddComponent<UiButtonFeedback>();
            fb.Configure(btn, go.GetComponent<Image>());
            CreateText("Label", go.transform, font, label, fontSize, FontStyle.Bold, textColor, Vector2.zero, Vector2.one);
            return btn;
        }
    }
}
