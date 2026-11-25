using System.Collections.Generic;
using OurWorld.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.World
{
    public enum NpcBubbleMood
    {
        Friendly = 0,
        Mission = 1,
        Alert = 2,
        Weather = 3
    }

    /// <summary>
    /// Pooled, crisp screen-space projected overhead speech bubbles for living neighborhood NPCs.
    /// Caps concurrent visible bubbles to 4 closest speakers within 24 m with smooth alpha fade,
    /// zero per-frame allocations, and automatic hiding during menus or modal dialogues.
    /// </summary>
    public sealed class NpcSpeechBubbleManager : MonoBehaviour
    {
        public static NpcSpeechBubbleManager Instance { get; private set; }
        public int ActiveSpeechCount => _speeches.Count;

        private const int MaxBubbles = 4;
        private const float MaxVisibleDistance = 24f;

        private sealed class ActiveSpeech
        {
            public Transform Speaker;
            public string SpeakerName;
            public string Text;
            public float ExpireTime;
            public float SpawnTime;
            public NpcBubbleMood Mood;
            public float HeadHeight;
        }

        private sealed class BubbleSlot
        {
            public GameObject Root;
            public RectTransform Rect;
            public CanvasGroup Group;
            public Image Background;
            public Image AccentBar;
            public Text NameText;
            public Text BodyText;
        }

        private readonly List<ActiveSpeech> _speeches = new List<ActiveSpeech>(8);
        private readonly List<(ActiveSpeech speech, float dist, Vector3 vp)> _candidates = new List<(ActiveSpeech, float, Vector3)>(8);
        private readonly BubbleSlot[] _slots = new BubbleSlot[MaxBubbles];
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Font _font;
        private Sprite _roundedSprite;

        private static readonly Color BgColor = new Color(0.055f, 0.088f, 0.135f, 0.92f);
        private static readonly Color BorderColor = new Color(1f, 1f, 1f, 0.16f);
        private static readonly Color MoodFriendly = new Color(0.20f, 0.80f, 0.72f, 1f);
        private static readonly Color MoodMission = new Color(0.98f, 0.76f, 0.28f, 1f);
        private static readonly Color MoodAlert = new Color(0.92f, 0.34f, 0.32f, 1f);
        private static readonly Color MoodWeather = new Color(0.36f, 0.74f, 0.98f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<OurWorld.Player.PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_NpcSpeechBubbles");
            go.AddComponent<NpcSpeechBubbleManager>();
        }

        public static void Say(
            Transform speaker,
            string speakerName,
            string text,
            float duration = 3.6f,
            NpcBubbleMood mood = NpcBubbleMood.Friendly,
            float headHeight = 2.15f)
        {
            if (speaker == null || string.IsNullOrWhiteSpace(text)) return;
            if (Instance == null)
            {
                Bootstrap();
                if (Instance == null) return;
            }
            Instance.ShowSpeech(speaker, speakerName, text, duration, mood, headHeight);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildCanvasAndPool();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_roundedSprite != null)
            {
                Texture2D tex = _roundedSprite.texture;
                Destroy(_roundedSprite);
                if (tex != null) Destroy(tex);
                _roundedSprite = null;
            }
        }

        private static Sprite BuildRoundedPillSprite(int size = 32, int radius = 9)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - x - 0.5f, (x + 0.5f) - (size - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - y - 0.5f, (y + 0.5f) - (size - r)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private void BuildCanvasAndPool()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 18);
            _roundedSprite = BuildRoundedPillSprite(32, 9);

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0) uiLayer = 5;

            GameObject canvasObj = new GameObject("NpcSpeechBubbleCanvas");
            canvasObj.layer = uiLayer;
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 25;
            _canvasRect = (RectTransform)canvasObj.transform;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            for (int i = 0; i < MaxBubbles; i++)
            {
                _slots[i] = CreateSlot(canvasObj.transform, i, uiLayer);
            }
        }

        private BubbleSlot CreateSlot(Transform parent, int idx, int uiLayer)
        {
            GameObject root = new GameObject($"NpcBubble_{idx}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.layer = uiLayer;
            root.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(360f, 78f);

            Image bg = root.GetComponent<Image>();
            bg.sprite = _roundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = BgColor;
            bg.raycastTarget = false;

            Shadow shadow = root.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -4f);

            // Tiny bottom center speech pointer notch
            GameObject tailObj = new GameObject("TailNotch", typeof(RectTransform), typeof(Image));
            tailObj.layer = uiLayer;
            tailObj.transform.SetParent(root.transform, false);
            RectTransform tailRt = (RectTransform)tailObj.transform;
            tailRt.anchorMin = new Vector2(0.5f, 0f);
            tailRt.anchorMax = new Vector2(0.5f, 0f);
            tailRt.pivot = new Vector2(0.5f, 0.5f);
            tailRt.anchoredPosition = new Vector2(0f, -1f);
            tailRt.sizeDelta = new Vector2(12f, 12f);
            tailRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image tailImg = tailObj.GetComponent<Image>();
            tailImg.color = BgColor;
            tailImg.raycastTarget = false;

            CanvasGroup cg = root.GetComponent<CanvasGroup>();
            cg.interactable = false;
            cg.blocksRaycasts = false;
            cg.alpha = 0f;

            // Crisp top mood accent bar (inset nicely within rounded corners)
            GameObject barObj = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
            barObj.layer = uiLayer;
            barObj.transform.SetParent(root.transform, false);
            RectTransform brt = (RectTransform)barObj.transform;
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -2f);
            brt.sizeDelta = new Vector2(-20f, 4f);
            Image barImg = barObj.GetComponent<Image>();
            barImg.color = MoodFriendly;
            barImg.raycastTarget = false;

            // Speaker name caption
            GameObject nameObj = new GameObject("SpeakerName", typeof(RectTransform), typeof(Text));
            nameObj.layer = uiLayer;
            nameObj.transform.SetParent(root.transform, false);
            RectTransform nrt = (RectTransform)nameObj.transform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(0.5f, 1f);
            nrt.anchoredPosition = new Vector2(0f, -8f);
            nrt.sizeDelta = new Vector2(-28f, 22f);
            Text nameTxt = nameObj.GetComponent<Text>();
            nameTxt.font = _font;
            nameTxt.fontSize = 16;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.color = MoodFriendly;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.raycastTarget = false;

            // Speech body text
            GameObject bodyObj = new GameObject("SpeechBody", typeof(RectTransform), typeof(Text));
            bodyObj.layer = uiLayer;
            bodyObj.transform.SetParent(root.transform, false);
            RectTransform trt = (RectTransform)bodyObj.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(14f, 8f);
            trt.offsetMax = new Vector2(-14f, -30f);
            Text bodyTxt = bodyObj.GetComponent<Text>();
            bodyTxt.font = _font;
            bodyTxt.fontSize = 19;
            bodyTxt.fontStyle = FontStyle.Normal;
            bodyTxt.color = new Color(0.96f, 0.98f, 1f, 1f);
            bodyTxt.alignment = TextAnchor.UpperLeft;
            bodyTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyTxt.verticalOverflow = VerticalWrapMode.Truncate;
            bodyTxt.raycastTarget = false;

            rt.anchoredPosition = new Vector2(-9999f, -9999f);

            return new BubbleSlot
            {
                Root = root,
                Rect = rt,
                Group = cg,
                Background = bg,
                AccentBar = barImg,
                NameText = nameTxt,
                BodyText = bodyTxt
            };
        }

        private void ShowSpeech(Transform speaker, string speakerName, string text, float duration, NpcBubbleMood mood, float headHeight)
        {
            OurWorld.Core.VoiceoverManager.PlayWorldSpeech(speaker, text, mood, speakerName);

            float now = Time.time;
            for (int i = 0; i < _speeches.Count; i++)
            {
                if (_speeches[i].Speaker == speaker)
                {
                    _speeches[i].SpeakerName = speakerName;
                    _speeches[i].Text = text;
                    _speeches[i].SpawnTime = now;
                    _speeches[i].ExpireTime = now + Mathf.Clamp(duration, 1.5f, 6.5f);
                    _speeches[i].Mood = mood;
                    _speeches[i].HeadHeight = headHeight;
                    return;
                }
            }

            if (_speeches.Count >= 8)
            {
                _speeches.RemoveAt(0);
            }

            _speeches.Add(new ActiveSpeech
            {
                Speaker = speaker,
                SpeakerName = speakerName ?? string.Empty,
                Text = text,
                SpawnTime = now,
                ExpireTime = now + Mathf.Clamp(duration, 1.5f, 6.5f),
                Mood = mood,
                HeadHeight = headHeight
            });
        }

        public void ForceRefreshNow()
        {
            RefreshBubbles();
        }

        private void LateUpdate()
        {
            RefreshBubbles();
        }

        private void RefreshBubbles()
        {
            float now = Time.time;
            for (int i = _speeches.Count - 1; i >= 0; i--)
            {
                if (_speeches[i].Speaker == null || !_speeches[i].Speaker.gameObject.activeInHierarchy || now >= _speeches[i].ExpireTime)
                {
                    _speeches.RemoveAt(i);
                }
            }

            bool hideAll = MainMenuController.IsMenuActive ||
                           (OurWorld.Player.PlayerModeController.Instance != null && OurWorld.Player.PlayerModeController.Instance.IsModalLocked);
            Camera cam = Camera.main;
            if (hideAll || cam == null || _speeches.Count == 0 || _canvasRect == null)
            {
                HideAllSlots();
                return;
            }

            Vector3 camPos = cam.transform.position;
            _candidates.Clear();

            for (int i = 0; i < _speeches.Count; i++)
            {
                ActiveSpeech sp = _speeches[i];
                Vector3 worldAnchor = sp.Speaker.position + Vector3.up * sp.HeadHeight;
                float dist = Vector3.Distance(camPos, worldAnchor);
                if (dist > MaxVisibleDistance) continue;

                Vector3 vp = cam.WorldToViewportPoint(worldAnchor);
                if (vp.z <= 0.4f) continue;
                if (vp.x < 0.04f || vp.x > 0.96f || vp.y < 0.08f || vp.y > 0.88f)
                {
                    continue;
                }

                _candidates.Add((sp, dist, vp));
            }

            if (_candidates.Count > 1)
            {
                _candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
            }

            Rect canvasRect = _canvasRect.rect;
            if (canvasRect.width < 10f || canvasRect.height < 10f)
            {
                canvasRect = new Rect(-960f, -540f, 1920f, 1080f);
            }
            int used = 0;

            for (int i = 0; i < _candidates.Count && used < MaxBubbles; i++)
            {
                var (sp, dist, vp) = _candidates[i];
                BubbleSlot slot = _slots[used++];
                if (!slot.Root.activeSelf) slot.Root.SetActive(true);

                Color accent = sp.Mood switch
                {
                    NpcBubbleMood.Mission => MoodMission,
                    NpcBubbleMood.Alert => MoodAlert,
                    NpcBubbleMood.Weather => MoodWeather,
                    _ => MoodFriendly
                };

                slot.AccentBar.color = accent;
                slot.NameText.color = accent;
                slot.NameText.text = sp.SpeakerName;
                slot.BodyText.text = sp.Text;

                // Auto-size pill cleanly for single-line vs two-line text
                int charLen = sp.Text.Length;
                float width = Mathf.Clamp(168f + charLen * 8.4f, 244f, 440f);
                float height = charLen > 45 ? 96f : 72f;
                slot.Rect.sizeDelta = new Vector2(width, height);

                float scale = Mathf.Lerp(1.0f, 0.80f, Mathf.InverseLerp(4f, MaxVisibleDistance, dist));
                slot.Rect.localScale = Vector3.one * scale;

                float x = Mathf.Lerp(canvasRect.xMin, canvasRect.xMax, vp.x);
                float y = Mathf.Lerp(canvasRect.yMin, canvasRect.yMax, vp.y);
                // Keep full pill safely inside visible margins
                float halfW = width * 0.5f * scale + 18f;
                x = Mathf.Clamp(x, canvasRect.xMin + halfW, canvasRect.xMax - halfW);
                y = Mathf.Clamp(y, canvasRect.yMin + 24f, canvasRect.yMax - height * scale - 24f);
                slot.Rect.anchoredPosition = new Vector2(x, y);

                float age = now - sp.SpawnTime;
                float rem = sp.ExpireTime - now;
                float fadeIn = Mathf.Clamp01(age / 0.18f);
                float fadeOut = Mathf.Clamp01(rem / 0.35f);
                float distT = Mathf.InverseLerp(MaxVisibleDistance * 0.72f, MaxVisibleDistance, dist);
                float distAlpha = 1f - Mathf.SmoothStep(0f, 1f, distT);
                slot.Group.alpha = Mathf.Clamp01(fadeIn * fadeOut * distAlpha);
            }

            for (int i = used; i < MaxBubbles; i++)
            {
                _slots[i].Group.alpha = 0f;
                _slots[i].Rect.anchoredPosition = new Vector2(-9999f, -9999f);
            }
        }

        private void HideAllSlots()
        {
            for (int i = 0; i < MaxBubbles; i++)
            {
                if (_slots[i] != null && _slots[i].Group != null)
                {
                    _slots[i].Group.alpha = 0f;
                    _slots[i].Rect.anchoredPosition = new Vector2(-9999f, -9999f);
                }
            }
        }
    }
}
