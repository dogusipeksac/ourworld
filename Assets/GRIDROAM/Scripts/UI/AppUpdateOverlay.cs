using System;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Fullscreen Force Update / Soft Update / Maintenance Gate overlay (Kube 2026 pattern).
    /// Locks gameplay input via PlayerModeController while active and opens the store on confirm.
    /// </summary>
    public static class AppUpdateOverlay
    {
        private static readonly Color BackdropColor = new Color(0.05f, 0.07f, 0.10f, 0.96f);
        private static readonly Color CardColor = new Color(0.10f, 0.13f, 0.18f, 0.98f);
        private static readonly Color AccentTeal = new Color(0.24f, 0.72f, 0.67f, 1f);
        private static readonly Color AccentGold = new Color(0.95f, 0.74f, 0.28f, 1f);
        private static readonly Color AccentRed = new Color(0.88f, 0.34f, 0.30f, 1f);
        private static readonly Color GhostBtnColor = new Color(0.18f, 0.22f, 0.28f, 0.95f);
        private static readonly Color TextWhite = new Color(0.95f, 0.96f, 0.98f, 1f);
        private static readonly Color TextDim = new Color(0.74f, 0.79f, 0.85f, 1f);

        private static readonly object ModalLockToken = new object();
        private static GameObject _activeCanvas;

        public static bool IsShowing => _activeCanvas != null;

        public static void Show(UpdateInfo info, Action onSoftDismissed = null, Action onRetryMaintenance = null)
        {
            if (info == null) return;
            Hide();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(ModalLockToken, true);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 24);

            GameObject canvasGo = new GameObject(info.IsMaintenance ? "OurWorld_MaintenanceGate" : "OurWorld_UpdateGate");
            UnityEngine.Object.DontDestroyOnLoad(canvasGo);
            _activeCanvas = canvasGo;

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Fullscreen raycast-blocking backdrop
            GameObject backdrop = CreatePanel("Backdrop", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, BackdropColor);
            backdrop.GetComponent<Image>().raycastTarget = true;

            Color themeAccent = info.IsMaintenance ? AccentGold : (info.ForceUpdate ? AccentRed : AccentTeal);

            // Centered hero card
            GameObject card = CreatePanel("UpdateCard", backdrop.transform,
                new Vector2(0.24f, 0.16f), new Vector2(0.76f, 0.84f),
                Vector2.zero, Vector2.zero, CardColor);

            // Top accent bar
            CreatePanel("AccentBar", card.transform,
                new Vector2(0f, 0.985f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, themeAccent);

            // Eyebrow pill
            string eyebrow = info.IsMaintenance
                ? LocalizationService.Tr("update.maintenance_eyebrow", "BAKIM ÇALIŞMASI")
                : (info.ForceUpdate
                    ? LocalizationService.Tr("update.force_eyebrow", "ZORUNLU GÜNCELLEME")
                    : LocalizationService.Tr("update.soft_eyebrow", "YENİ SÜRÜM"));

            GameObject pill = CreatePanel("EyebrowPill", card.transform,
                new Vector2(0.28f, 0.81f), new Vector2(0.72f, 0.90f),
                Vector2.zero, Vector2.zero, new Color(themeAccent.r, themeAccent.g, themeAccent.b, 0.18f));
            CreateText("Eyebrow", pill.transform, font, eyebrow.ToUpperInvariant(), 22, FontStyle.Bold, themeAccent,
                new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f));

            // Title
            CreateText("Title", card.transform, font, info.Title, 44, FontStyle.Bold, TextWhite,
                new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.78f));

            // Description
            CreateText("Body", card.transform, font, info.Description, 26, FontStyle.Normal, TextDim,
                new Vector2(0.10f, 0.36f), new Vector2(0.90f, 0.60f));

            // Buttons
            if (info.IsMaintenance)
            {
                string retryLabel = LocalizationService.Tr("update.retry", "TEKRAR DENE");
                Button retryBtn = CreateButton("RetryBtn", card.transform, font, retryLabel, AccentGold, new Color(0.08f, 0.09f, 0.12f, 1f),
                    new Vector2(0.26f, 0.10f), new Vector2(0.74f, 0.24f), 28);
                retryBtn.onClick.AddListener(() => onRetryMaintenance?.Invoke());
            }
            else if (info.ForceUpdate)
            {
                string confirmLabel = !string.IsNullOrEmpty(info.ConfirmButtonText) ? info.ConfirmButtonText.ToUpperInvariant() : "GÜNCELLE";
                Button updateBtn = CreateButton("UpdateBtn", card.transform, font, confirmLabel, AccentTeal, new Color(0.04f, 0.10f, 0.10f, 1f),
                    new Vector2(0.18f, 0.11f), new Vector2(0.82f, 0.25f), 30);
                updateBtn.onClick.AddListener(() => OpenStore(info));
            }
            else
            {
                string confirmLabel = !string.IsNullOrEmpty(info.ConfirmButtonText) ? info.ConfirmButtonText.ToUpperInvariant() : "GÜNCELLE";
                Button updateBtn = CreateButton("UpdateBtn", card.transform, font, confirmLabel, AccentTeal, new Color(0.04f, 0.10f, 0.10f, 1f),
                    new Vector2(0.18f, 0.21f), new Vector2(0.82f, 0.34f), 30);
                updateBtn.onClick.AddListener(() => OpenStore(info));

                if (!string.IsNullOrEmpty(info.CancelButtonText))
                {
                    Button laterBtn = CreateButton("LaterBtn", card.transform, font, info.CancelButtonText.ToUpperInvariant(), GhostBtnColor, TextWhite,
                        new Vector2(0.28f, 0.07f), new Vector2(0.72f, 0.17f), 24);
                    laterBtn.onClick.AddListener(() =>
                    {
                        Hide();
                        onSoftDismissed?.Invoke();
                    });
                }
            }
        }

        public static void Hide()
        {
            if (_activeCanvas != null)
            {
                UnityEngine.Object.Destroy(_activeCanvas);
                _activeCanvas = null;
            }

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(ModalLockToken, false);
            }
        }

        private static void OpenStore(UpdateInfo info)
        {
            string url = info.StoreUrl;
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogWarning("[OurWorld Update] Store URL tanımlı değil.");
                return;
            }
            Application.OpenURL(url);
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
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
            txt.lineSpacing = 1.15f;
            txt.raycastTarget = false;
            return txt;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label, Color bgColor, Color textColor, Vector2 anchorMin, Vector2 anchorMax, int fontSize)
        {
            GameObject go = CreatePanel(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero, bgColor);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            UiButtonFeedback fb = go.AddComponent<UiButtonFeedback>();
            fb.Configure(btn, go.GetComponent<Image>());
            CreateText("Label", go.transform, font, label, fontSize, FontStyle.Bold, textColor, Vector2.zero, Vector2.one);
            return btn;
        }
    }
}
