using System;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Shared flagship UI design kit (Istanbul Night Slate + Turquoise + Golden Amber)
    /// providing cached anti-aliased 9-sliced rounded sprites, soft drop shadows, glass hairline borders,
    /// top glow gradients, and crisp 64x64 SDF vector icons for all runtime overlays and panels.
    /// </summary>
    public static class OurWorldUiKit
    {
        // --- Core Palette ---
        public static readonly Color Scrim = new Color(0.025f, 0.042f, 0.075f, 0.86f);
        public static readonly Color CardBg = new Color(0.062f, 0.088f, 0.138f, 0.98f);
        public static readonly Color SurfaceBg = new Color(0.102f, 0.140f, 0.208f, 0.95f);
        public static readonly Color SurfaceSoft = new Color(0.132f, 0.180f, 0.262f, 0.92f);
        public static readonly Color SurfaceActive = new Color(0.088f, 0.305f, 0.295f, 0.98f);
        public static readonly Color BorderSubtle = new Color(1f, 1f, 1f, 0.11f);
        public static readonly Color Divider = new Color(1f, 1f, 1f, 0.08f);

        public static readonly Color TextPrimary = new Color(0.96f, 0.975f, 0.995f, 1f);
        public static readonly Color TextSecondary = new Color(0.75f, 0.82f, 0.89f, 1f);
        public static readonly Color TextMuted = new Color(0.54f, 0.62f, 0.72f, 1f);
        public static readonly Color InkDark = new Color(0.035f, 0.085f, 0.11f, 1f);

        public static readonly Color AccentTeal = new Color(0.14f, 0.78f, 0.70f, 1f);
        public static readonly Color AccentGold = new Color(0.97f, 0.75f, 0.24f, 1f);
        public static readonly Color AccentSky = new Color(0.24f, 0.69f, 0.98f, 1f);
        public static readonly Color AccentCoral = new Color(0.96f, 0.44f, 0.34f, 1f);
        public static readonly Color AccentRed = new Color(0.86f, 0.31f, 0.29f, 1f);

        private static bool _initialized;
        private static Font _cachedFont;

        public static Sprite CardSprite { get; private set; }
        public static Sprite CardOutlineSprite { get; private set; }
        public static Sprite SubCardSprite { get; private set; }
        public static Sprite SubCardOutlineSprite { get; private set; }
        public static Sprite PillSprite { get; private set; }
        public static Sprite PillOutlineSprite { get; private set; }
        public static Sprite CircleSprite { get; private set; }
        public static Sprite CircleOutlineSprite { get; private set; }
        public static Sprite TopGlowSprite { get; private set; }
        public static Sprite DropShadowSprite { get; private set; }

        public static Sprite IconClose { get; private set; }
        public static Sprite IconChevronLeft { get; private set; }
        public static Sprite IconChevronRight { get; private set; }
        public static Sprite IconStar { get; private set; }
        public static Sprite IconWorld { get; private set; }
        public static Sprite IconMechanic { get; private set; }
        public static Sprite IconSports { get; private set; }
        public static Sprite IconCommunity { get; private set; }
        public static Sprite IconUpdate { get; private set; }

        public static Font DefaultFont
        {
            get
            {
                if (_cachedFont == null)
                {
                    _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                                  ?? Resources.GetBuiltinResource<Font>("Arial.ttf")
                                  ?? Font.CreateDynamicFontFromOSFont("Arial", 24);
                }
                return _cachedFont;
            }
        }

        public static void EnsureInitialized()
        {
            if (_initialized && CardSprite != null) return;
            _initialized = true;

            CardSprite = CreateRoundedRectSprite(128, 128, 26, outlineOnly: false, outlineThickness: 0f);
            CardOutlineSprite = CreateRoundedRectSprite(128, 128, 26, outlineOnly: true, outlineThickness: 2.0f);
            SubCardSprite = CreateRoundedRectSprite(96, 96, 16, outlineOnly: false, outlineThickness: 0f);
            SubCardOutlineSprite = CreateRoundedRectSprite(96, 96, 16, outlineOnly: true, outlineThickness: 1.6f);
            PillSprite = CreateRoundedRectSprite(96, 48, 23, outlineOnly: false, outlineThickness: 0f);
            PillOutlineSprite = CreateRoundedRectSprite(96, 48, 23, outlineOnly: true, outlineThickness: 1.8f);
            CircleSprite = CreateRoundedRectSprite(64, 64, 31, outlineOnly: false, outlineThickness: 0f);
            CircleOutlineSprite = CreateRoundedRectSprite(64, 64, 31, outlineOnly: true, outlineThickness: 2.0f);
            TopGlowSprite = CreateVerticalFadeSprite(96, topAlpha: 1f, bottomAlpha: 0f);
            DropShadowSprite = CreateSoftShadowSprite(128, 128, 28, 18);

            IconClose = CreateVectorIconSprite(64, SampleCloseX);
            IconChevronLeft = CreateVectorIconSprite(64, SampleChevronLeft);
            IconChevronRight = CreateVectorIconSprite(64, SampleChevronRight);
            IconStar = CreateVectorIconSprite(64, SampleStar);
            IconWorld = CreateVectorIconSprite(64, SampleWorldPin);
            IconMechanic = CreateVectorIconSprite(64, SampleSparkleGear);
            IconSports = CreateVectorIconSprite(64, SampleTrophy);
            IconCommunity = CreateVectorIconSprite(64, SampleChatHeart);
            IconUpdate = CreateVectorIconSprite(64, SampleUpdateArrow);
        }

        // =====================================================================
        // COMPONENT STYLING & CREATION HELPERS
        // =====================================================================

        public static Image ApplyRounded(Image img, Sprite sprite, Color color)
        {
            EnsureInitialized();
            img.sprite = sprite;
            img.type = sprite != null && sprite.border.sqrMagnitude > 0.1f ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            return img;
        }

        public static Image AddHairlineBorder(Transform parent, Sprite outlineSprite, Color color)
        {
            EnsureInitialized();
            GameObject go = new GameObject("HairlineBorder", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = go.GetComponent<Image>();
            img.sprite = outlineSprite;
            img.type = outlineSprite != null && outlineSprite.border.sqrMagnitude > 0.1f ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image AddCardDropShadow(Transform cardTransform, float spread = 26f, float offsetY = -12f, float alpha = 0.56f)
        {
            EnsureInitialized();
            GameObject go = new GameObject("DropShadow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(cardTransform, false);
            go.transform.SetAsFirstSibling();
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-spread, -spread + offsetY);
            rt.offsetMax = new Vector2(spread, spread + offsetY * 0.35f);
            Image img = go.GetComponent<Image>();
            img.sprite = DropShadowSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0f, 0f, 0f, alpha);
            img.raycastTarget = false;
            return img;
        }

        public static Image AddSoftTopGlow(Transform cardTransform, Color accent, float heightPx = 180f, float alpha = 0.16f)
        {
            EnsureInitialized();
            GameObject go = new GameObject("TopAccentGlow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(cardTransform, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -2f);
            rt.sizeDelta = new Vector2(-4f, heightPx);
            Image img = go.GetComponent<Image>();
            img.sprite = TopGlowSprite;
            img.color = new Color(accent.r, accent.g, accent.b, alpha);
            img.raycastTarget = false;
            return img;
        }

        public static Image AddDividerLine(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color? color = null)
        {
            GameObject go = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            Image img = go.GetComponent<Image>();
            img.color = color ?? Divider;
            img.raycastTarget = false;
            return img;
        }

        public static Image AddIcon(string name, Transform parent, Sprite icon, float sizePx, Color color, Vector2? anchoredPos = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos ?? Vector2.zero;
            rt.sizeDelta = new Vector2(sizePx, sizePx);
            Image img = go.GetComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Button CreateCircularIconButton(
            string name,
            Transform parent,
            Sprite icon,
            float diameter,
            float iconSize,
            Color bgColor,
            Color iconColor,
            Color borderColor,
            Action onClick)
        {
            EnsureInitialized();
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(diameter, diameter);

            Image bg = go.GetComponent<Image>();
            bg.sprite = CircleSprite;
            bg.color = bgColor;

            AddHairlineBorder(go.transform, CircleOutlineSprite, borderColor);
            AddIcon("Icon", go.transform, icon, iconSize, iconColor);

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            UiButtonFeedback fb = go.AddComponent<UiButtonFeedback>();
            fb.Configure(btn, bg);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static Button CreatePillButton(
            string name,
            Transform parent,
            string label,
            int fontSize,
            Color bgColor,
            Color textColor,
            Color borderColor,
            out Text outLabel,
            Action onClick = null)
        {
            EnsureInitialized();
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image bg = ApplyRounded(go.GetComponent<Image>(), PillSprite, bgColor);
            if (borderColor.a > 0.005f)
            {
                AddHairlineBorder(go.transform, PillOutlineSprite, borderColor);
            }

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            UiButtonFeedback fb = go.AddComponent<UiButtonFeedback>();
            fb.Configure(btn, bg);
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            outLabel = CreateStretchText("Label", go.transform, label, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter, 18f, 4f);
            return btn;
        }

        public static Text CreateText(
            string name,
            Transform parent,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            return CreateStretchText(name, parent, content, fontSize, style, color, alignment, 0f, 0f);
        }

        public static Text CreateStretchText(
            string name,
            Transform parent,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter,
            float padX = 0f,
            float padY = 0f)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);

            Text t = go.GetComponent<Text>();
            t.font = DefaultFont;
            t.text = content;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        // =====================================================================
        // PROCEDURAL ANTI-ALIASED SDF SPRITE GENERATORS
        // =====================================================================

        private static Sprite CreateRoundedRectSprite(int w, int h, int radius, bool outlineOnly, float outlineThickness)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[w * h];
            float r = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f - 1f);
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float bx = halfW - r;
            float by = halfH - r;

            for (int y = 0; y < h; y++)
            {
                float py = Mathf.Abs(y + 0.5f - halfH) - by;
                for (int x = 0; x < w; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - halfW) - bx;
                    float dist = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f) + Mathf.Max(py, 0f) * Mathf.Max(py, 0f))
                                 + Mathf.Min(Mathf.Max(px, py), 0f) - r;

                    float alpha;
                    if (outlineOnly)
                    {
                        float outer = Mathf.Clamp01(0.5f - dist);
                        float inner = Mathf.Clamp01(0.5f - (dist + outlineThickness));
                        alpha = Mathf.Clamp01(outer - inner);
                    }
                    else
                    {
                        alpha = Mathf.Clamp01(0.5f - dist);
                    }

                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            int b = Mathf.Min(radius + 2, Mathf.Min(w, h) / 2 - 1);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static Sprite CreateSoftShadowSprite(int w, int h, int radius, int feather)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[w * h];
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float innerHalfW = halfW - feather;
            float innerHalfH = halfH - feather;
            float r = Mathf.Min(radius, Mathf.Min(innerHalfW, innerHalfH) - 1f);
            float bx = innerHalfW - r;
            float by = innerHalfH - r;

            for (int y = 0; y < h; y++)
            {
                float py = Mathf.Abs(y + 0.5f - halfH) - by;
                for (int x = 0; x < w; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - halfW) - bx;
                    float dist = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f) + Mathf.Max(py, 0f) * Mathf.Max(py, 0f))
                                 + Mathf.Min(Mathf.Max(px, py), 0f) - r;
                    float t = Mathf.Clamp01(1f - dist / Mathf.Max(1f, feather));
                    float alpha = t * t * (3f - 2f * t);
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            int b = Mathf.Min(radius + feather, Mathf.Min(w, h) / 2 - 1);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static Sprite CreateVerticalFadeSprite(int height, float topAlpha, float bottomAlpha)
        {
            Texture2D tex = new Texture2D(2, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] cols = new Color[height * 2];
            for (int y = 0; y < height; y++)
            {
                float u = (float)y / (height - 1); // 0 = bottom, 1 = top
                float smooth = u * u;
                float a = Mathf.Lerp(bottomAlpha, topAlpha, smooth);
                Color c = new Color(1f, 1f, 1f, a);
                cols[y * 2] = c;
                cols[y * 2 + 1] = c;
            }
            tex.SetPixels(cols);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 2, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateVectorIconSprite(int size, Func<float, float, float> alphaFn)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = ((y + 0.5f) / size) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float a = Mathf.Clamp01(alphaFn(nx, ny));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float SmoothEdge(float signedDist, float aa = 0.06f) => Mathf.Clamp01(0.5f - signedDist / aa);

        private static float SdSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(0.0001f, Vector2.Dot(ba, ba)));
            return (pa - ba * h).magnitude;
        }

        private static float SampleCloseX(float x, float y)
        {
            Vector2 p = new Vector2(x, y);
            float d1 = SdSegment(p, new Vector2(-0.46f, -0.46f), new Vector2(0.46f, 0.46f)) - 0.10f;
            float d2 = SdSegment(p, new Vector2(-0.46f, 0.46f), new Vector2(0.46f, -0.46f)) - 0.10f;
            return SmoothEdge(Mathf.Min(d1, d2));
        }

        private static float SampleChevronLeft(float x, float y)
        {
            Vector2 p = new Vector2(x, y);
            float d1 = SdSegment(p, new Vector2(0.22f, 0.48f), new Vector2(-0.26f, 0f)) - 0.10f;
            float d2 = SdSegment(p, new Vector2(-0.26f, 0f), new Vector2(0.22f, -0.48f)) - 0.10f;
            return SmoothEdge(Mathf.Min(d1, d2));
        }

        private static float SampleChevronRight(float x, float y) => SampleChevronLeft(-x, y);

        private static float SampleStar(float x, float y)
        {
            float ax = Mathf.Abs(x);
            float ay = Mathf.Abs(y);
            float star = Mathf.Pow(ax, 0.62f) + Mathf.Pow(ay, 0.62f) - 0.72f;
            float mini = Mathf.Pow(Mathf.Abs(x - 0.52f), 0.65f) + Mathf.Pow(Mathf.Abs(y - 0.52f), 0.65f) - 0.28f;
            return SmoothEdge(Mathf.Min(star, mini), 0.08f);
        }

        private static float SampleWorldPin(float x, float y)
        {
            // Globe ring + latitude/longitude lines + location pin accent
            Vector2 p = new Vector2(x, y);
            float r = p.magnitude;
            float ring = Mathf.Abs(r - 0.64f) - 0.075f;
            float eq = SdSegment(p, new Vector2(-0.62f, 0f), new Vector2(0.62f, 0f)) - 0.055f;
            float mer = Mathf.Abs(new Vector2(x * 1.9f, y).magnitude - 0.64f) - 0.065f;
            float innerMask = r - 0.66f;
            float clippedMer = Mathf.Max(mer, innerMask);
            float vert = SdSegment(p, new Vector2(0f, -0.62f), new Vector2(0f, 0.62f)) - 0.05f;
            return SmoothEdge(Mathf.Min(Mathf.Min(ring, eq), Mathf.Min(clippedMer, vert)));
        }

        private static float SampleSparkleGear(float x, float y)
        {
            // Clean 8-tooth gear / workshop emblem
            Vector2 p = new Vector2(x, y);
            float r = p.magnitude;
            float ang = Mathf.Atan2(y, x);
            float teeth = 0.62f + 0.12f * Mathf.Cos(ang * 8f);
            float outer = r - teeth;
            float hole = 0.28f - r;
            return SmoothEdge(Mathf.Max(outer, hole));
        }

        private static float SampleTrophy(float x, float y)
        {
            // Crisp sports trophy silhouette
            Vector2 p = new Vector2(x, y);
            float cup = Mathf.Max(Mathf.Abs(x) - Mathf.Lerp(0.24f, 0.48f, Mathf.Clamp01((y + 0.12f) / 0.72f)), Mathf.Max(y - 0.62f, -0.14f - y));
            float stem = SdSegment(p, new Vector2(0f, -0.14f), new Vector2(0f, -0.48f)) - 0.09f;
            float baseBar = SdSegment(p, new Vector2(-0.34f, -0.56f), new Vector2(0.34f, -0.56f)) - 0.085f;
            float handleL = Mathf.Abs((p - new Vector2(-0.46f, 0.30f)).magnitude - 0.19f) - 0.055f;
            float handleR = Mathf.Abs((p - new Vector2(0.46f, 0.30f)).magnitude - 0.19f) - 0.055f;
            return SmoothEdge(Mathf.Min(Mathf.Min(cup, stem), Mathf.Min(baseBar, Mathf.Min(handleL, handleR))));
        }

        private static float SampleChatHeart(float x, float y)
        {
            // Rounded speech bubble + centered sparkle star
            Vector2 p = new Vector2(x, y - 0.08f);
            Vector2 q = new Vector2(Mathf.Abs(p.x) - 0.44f, Mathf.Abs(p.y) - 0.30f);
            float bubble = Mathf.Sqrt(Mathf.Max(q.x, 0f) * Mathf.Max(q.x, 0f) + Mathf.Max(q.y, 0f) * Mathf.Max(q.y, 0f))
                           + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 0.22f;
            float tail = SdSegment(new Vector2(x, y), new Vector2(-0.28f, -0.34f), new Vector2(-0.48f, -0.64f)) - 0.11f;
            float outer = Mathf.Min(bubble, tail);
            float starCut = Mathf.Pow(Mathf.Abs(x), 0.65f) + Mathf.Pow(Mathf.Abs(y - 0.08f), 0.65f) - 0.30f;
            float bubbleAlpha = SmoothEdge(outer);
            float starAlpha = SmoothEdge(starCut, 0.08f);
            return Mathf.Clamp01(bubbleAlpha - starAlpha * 0.9f);
        }

        private static float SampleUpdateArrow(float x, float y)
        {
            // Upward arrow inside sleek ring
            Vector2 p = new Vector2(x, y);
            float ring = Mathf.Abs(p.magnitude - 0.68f) - 0.075f;
            float shaft = SdSegment(p, new Vector2(0f, -0.36f), new Vector2(0f, 0.32f)) - 0.085f;
            float wingL = SdSegment(p, new Vector2(-0.28f, 0.08f), new Vector2(0f, 0.36f)) - 0.085f;
            float wingR = SdSegment(p, new Vector2(0.28f, 0.08f), new Vector2(0f, 0.36f)) - 0.085f;
            return SmoothEdge(Mathf.Min(Mathf.Min(ring, shaft), Mathf.Min(wingL, wingR)));
        }
    }
}
