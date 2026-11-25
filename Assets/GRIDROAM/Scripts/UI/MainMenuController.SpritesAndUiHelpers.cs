using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Jobs;
using OurWorld.Missions;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    public sealed partial class MainMenuController
    {
        // =====================================================================
        // PROCEDURAL UI SPRITES, VECTOR SDF ICONS & UGUI HELPERS
        // =====================================================================

        private void BuildProceduralSprites()
        {
            _roundedCardSprite = CreateRoundedRectSprite(96, 96, 20, outlineOnly: false, outlineThickness: 0f);
            _roundedOutlineSprite = CreateRoundedRectSprite(96, 96, 20, outlineOnly: true, outlineThickness: 2.0f);
            _pillSprite = CreateRoundedRectSprite(96, 48, 23, outlineOnly: false, outlineThickness: 0f);
            _pillOutlineSprite = CreateRoundedRectSprite(96, 48, 23, outlineOnly: true, outlineThickness: 2.0f);

            _leftVignetteSprite = CreateHorizontalGradientSprite(192,
                new Color(0.028f, 0.045f, 0.082f, 0.88f),
                new Color(0.028f, 0.045f, 0.082f, 0.52f),
                new Color(0.028f, 0.045f, 0.082f, 0.0f));
            _bottomVignetteSprite = CreateVerticalGradientSprite(96,
                new Color(0.028f, 0.045f, 0.082f, 0.64f),
                new Color(0.028f, 0.045f, 0.082f, 0.0f));

            // Crisp 64x64 procedural SDF vector icons (100% consistent across iOS, Android, macOS)
            _iconPlaySprite = CreateVectorIconSprite(64, SamplePlayTriangle);
            _iconPinSprite = CreateVectorIconSprite(64, SampleMapPin);
            _iconSlidersSprite = CreateVectorIconSprite(64, SampleSlidersIcon);
            _iconSparkleSprite = CreateVectorIconSprite(64, SampleSparkleStar);
            _iconMapSprite = CreateVectorIconSprite(64, SampleMapGrid);
            _iconMenuBarsSprite = CreateVectorIconSprite(64, SampleHamburgerBars);

            _brandIconSprite = Resources.Load<Sprite>("UI/Icons/ourworld_menu_icon");
            _brandIconCreatedAtRuntime = false;
            if (_brandIconSprite == null)
            {
                Texture2D iconTex = Resources.Load<Texture2D>("UI/Icons/ourworld_menu_icon");
                if (iconTex != null)
                {
                    _brandIconSprite = Sprite.Create(
                        iconTex,
                        new Rect(0, 0, iconTex.width, iconTex.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                    _brandIconCreatedAtRuntime = true;
                }
            }
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

        private static float SmoothEdge(float signedDist, float aa = 0.055f) => Mathf.Clamp01(0.5f - signedDist / aa);

        private static float SdSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(0.0001f, Vector2.Dot(ba, ba)));
            return (pa - ba * h).magnitude;
        }

        private static float SamplePlayTriangle(float x, float y)
        {
            // Right-pointing rounded triangle centered slightly right for optical balance
            float left = -0.46f;
            float right = 0.62f;
            float slope = 0.62f;
            float maxY = Mathf.Max(0f, (right - x) * slope);
            float dLeft = left - x;
            float dDiag = (Mathf.Abs(y) - maxY) / Mathf.Sqrt(1f + slope * slope);
            return SmoothEdge(Mathf.Max(dLeft, dDiag) - 0.06f);
        }

        private static float SampleMapPin(float x, float y)
        {
            // Teardrop location pin with crisp inner circular cutout
            Vector2 headCenter = new Vector2(0f, 0.24f);
            float dCircle = (new Vector2(x, y) - headCenter).magnitude - 0.44f;
            float tipT = Mathf.Clamp01((0.24f - y) / 0.96f);
            float coneHalfW = Mathf.Lerp(0.42f, 0.04f, tipT);
            float dCone = y <= 0.24f && y >= -0.74f ? (Mathf.Abs(x) - coneHalfW) : 1f;
            float dPin = Mathf.Min(dCircle, dCone);
            float dHole = (new Vector2(x, y) - headCenter).magnitude - 0.17f;
            float pinAlpha = SmoothEdge(dPin);
            float holeAlpha = SmoothEdge(dHole);
            return Mathf.Clamp01(pinAlpha - holeAlpha);
        }

        private static float SampleSlidersIcon(float x, float y)
        {
            // 3 horizontal equalizer/setting bars with circular knobs
            Vector2 p = new Vector2(x, y);
            float d = 1f;
            float[] ys = { 0.48f, 0.0f, -0.48f };
            float[] knobs = { -0.22f, 0.26f, -0.10f };
            for (int i = 0; i < 3; i++)
            {
                float bar = SdSegment(p, new Vector2(-0.68f, ys[i]), new Vector2(0.68f, ys[i])) - 0.065f;
                float knob = (p - new Vector2(knobs[i], ys[i])).magnitude - 0.165f;
                d = Mathf.Min(d, Mathf.Min(bar, knob));
            }
            return SmoothEdge(d);
        }

        private static float SampleSparkleStar(float x, float y)
        {
            // Crisp 4-point star sparkle + secondary small accent star
            float ax = Mathf.Abs(x);
            float ay = Mathf.Abs(y);
            float star = Mathf.Pow(ax, 0.62f) + Mathf.Pow(ay, 0.62f) - 0.74f;
            float mini = Mathf.Pow(Mathf.Abs(x - 0.52f), 0.65f) + Mathf.Pow(Mathf.Abs(y - 0.52f), 0.65f) - 0.30f;
            return SmoothEdge(Mathf.Min(star, mini), 0.08f);
        }

        private static float SampleMapGrid(float x, float y)
        {
            // Clean folded 3-panel map / compass grid icon
            Vector2 p = new Vector2(x, y);
            float d = 1f;
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.66f, -0.50f), new Vector2(-0.66f, 0.56f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.22f, -0.60f), new Vector2(-0.22f, 0.46f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(0.22f, -0.46f), new Vector2(0.22f, 0.60f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(0.66f, -0.56f), new Vector2(0.66f, 0.50f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.66f, 0.56f), new Vector2(-0.22f, 0.46f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.22f, 0.46f), new Vector2(0.22f, 0.60f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(0.22f, 0.60f), new Vector2(0.66f, 0.50f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.66f, -0.50f), new Vector2(-0.22f, -0.60f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.22f, -0.60f), new Vector2(0.22f, -0.46f)) - 0.065f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(0.22f, -0.46f), new Vector2(0.66f, -0.56f)) - 0.065f);
            return SmoothEdge(d);
        }

        private static float SampleHamburgerBars(float x, float y)
        {
            Vector2 p = new Vector2(x, y);
            float d = SdSegment(p, new Vector2(-0.68f, 0.48f), new Vector2(0.68f, 0.48f)) - 0.095f;
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.68f, 0.0f), new Vector2(0.68f, 0.0f)) - 0.095f);
            d = Mathf.Min(d, SdSegment(p, new Vector2(-0.68f, -0.48f), new Vector2(0.40f, -0.48f)) - 0.095f);
            return SmoothEdge(d);
        }

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

                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            int b = Mathf.Min(radius + 2, Mathf.Min(w, h) / 2 - 1);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static Sprite CreateHorizontalGradientSprite(int width, Color left, Color mid, Color right)
        {
            Texture2D tex = new Texture2D(width, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] cols = new Color[width * 2];
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (width - 1);
                Color c = u < 0.52f
                    ? Color.Lerp(left, mid, Mathf.SmoothStep(0f, 1f, u / 0.52f))
                    : Color.Lerp(mid, right, Mathf.SmoothStep(0f, 1f, (u - 0.52f) / 0.48f));
                cols[x] = c;
                cols[width + x] = c;
            }
            tex.SetPixels(cols);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, width, 2), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateVerticalGradientSprite(int height, Color bottom, Color top)
        {
            Texture2D tex = new Texture2D(2, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] cols = new Color[height * 2];
            for (int y = 0; y < height; y++)
            {
                float v = (float)y / (height - 1);
                Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, v));
                cols[y * 2] = c;
                cols[y * 2 + 1] = c;
            }
            tex.SetPixels(cols);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 2, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void CreateCenteredIcon(string name, Transform parent, Sprite sprite, float sizePx, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(sizePx, sizePx);
            UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        private void AddTopAccentStrip(Transform parent, Color accent)
        {
            GameObject strip = new GameObject("TopAccentStrip", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            strip.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)strip.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -1f);
            rt.sizeDelta = new Vector2(-48f, 3f);
            UnityEngine.UI.Image img = strip.GetComponent<UnityEngine.UI.Image>();
            img.sprite = _pillSprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = new Color(accent.r, accent.g, accent.b, 0.78f);
            img.raycastTarget = false;
        }

        private GameObject CreateRoundedPanel(
            string name, Transform parent, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta, Color color, bool addBorder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;

            if (addBorder)
            {
                AddOutlineOverlay(go.transform, BorderSubtle);
            }

            return go;
        }

        private UnityEngine.UI.Image AddOutlineOverlay(Transform parent, Color color, Sprite customOutline = null)
        {
            GameObject go = new GameObject("OutlineRim", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            StretchFull((RectTransform)go.transform, 0f, 0f);
            UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = customOutline != null ? customOutline : _roundedOutlineSprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private UnityEngine.UI.Text CreateText(
            string name, Transform parent, string text, int fontSize, FontStyle style, Color color,
            TextAnchor alignment, bool addShadow = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(parent, false);
            UnityEngine.UI.Text t = go.GetComponent<UnityEngine.UI.Text>();
            t.font = _uiFont;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = alignment;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            if (addShadow)
            {
                UnityEngine.UI.Shadow sh = go.AddComponent<UnityEngine.UI.Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.55f);
                sh.effectDistance = new Vector2(0f, -2f);
            }

            return t;
        }

        private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void StretchFull(RectTransform rt, float padX, float padY)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }
    }
}
