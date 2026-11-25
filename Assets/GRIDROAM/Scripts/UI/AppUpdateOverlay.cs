using System;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Flagship Force Update / Soft Update / Maintenance Gate overlay matching the
    /// Istanbul Night Slate + Turquoise + Golden Amber rounded design kit.
    /// Locks gameplay input via PlayerModeController while active and opens the store on confirm.
    /// </summary>
    public static class AppUpdateOverlay
    {
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

            OurWorldUiKit.EnsureInitialized();

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

            // Fullscreen dimmed backdrop
            GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(canvasGo.transform, false);
            RectTransform backRt = (RectTransform)backdrop.transform;
            backRt.anchorMin = Vector2.zero;
            backRt.anchorMax = Vector2.one;
            backRt.offsetMin = Vector2.zero;
            backRt.offsetMax = Vector2.zero;
            Image backImg = backdrop.GetComponent<Image>();
            backImg.color = OurWorldUiKit.Scrim;
            backImg.raycastTarget = true;

            Color themeAccent = info.IsMaintenance
                ? OurWorldUiKit.AccentGold
                : (info.ForceUpdate ? OurWorldUiKit.AccentCoral : OurWorldUiKit.AccentTeal);

            // Centered elevated rounded card
            GameObject card = new GameObject("UpdateCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(backdrop.transform, false);
            RectTransform cardRt = (RectTransform)card.transform;
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(820f, 560f);

            OurWorldUiKit.ApplyRounded(card.GetComponent<Image>(), OurWorldUiKit.CardSprite, OurWorldUiKit.CardBg);
            OurWorldUiKit.AddCardDropShadow(card.transform, spread: 28f, offsetY: -12f, alpha: 0.62f);
            OurWorldUiKit.AddSoftTopGlow(card.transform, themeAccent, heightPx: 190f, alpha: 0.16f);
            OurWorldUiKit.AddHairlineBorder(card.transform, OurWorldUiKit.CardOutlineSprite,
                Color.Lerp(OurWorldUiKit.BorderSubtle, themeAccent, 0.28f));

            // Circular icon medallion
            GameObject med = new GameObject("IconMedallion", typeof(RectTransform), typeof(Image));
            med.transform.SetParent(card.transform, false);
            RectTransform medRt = (RectTransform)med.transform;
            medRt.anchorMin = medRt.anchorMax = new Vector2(0.5f, 1f);
            medRt.pivot = new Vector2(0.5f, 1f);
            medRt.anchoredPosition = new Vector2(0f, -40f);
            medRt.sizeDelta = new Vector2(86f, 86f);
            Image medImg = med.GetComponent<Image>();
            medImg.sprite = OurWorldUiKit.CircleSprite;
            medImg.color = new Color(themeAccent.r, themeAccent.g, themeAccent.b, 0.16f);
            medImg.raycastTarget = false;
            OurWorldUiKit.AddHairlineBorder(med.transform, OurWorldUiKit.CircleOutlineSprite,
                new Color(themeAccent.r, themeAccent.g, themeAccent.b, 0.52f));
            OurWorldUiKit.AddIcon("Icon", med.transform,
                info.IsMaintenance ? OurWorldUiKit.IconMechanic : OurWorldUiKit.IconUpdate,
                40f, themeAccent);

            // Status eyebrow pill
            string eyebrow = info.IsMaintenance
                ? LocalizationService.Tr("update.maintenance_eyebrow", "BAKIM ÇALIŞMASI")
                : (info.ForceUpdate
                    ? LocalizationService.Tr("update.force_eyebrow", "ZORUNLU GÜNCELLEME")
                    : LocalizationService.Tr("update.soft_eyebrow", "YENİ SÜRÜM HAZIR"));

            GameObject pill = new GameObject("EyebrowPill", typeof(RectTransform), typeof(Image));
            pill.transform.SetParent(card.transform, false);
            RectTransform pillRt = (RectTransform)pill.transform;
            pillRt.anchorMin = pillRt.anchorMax = new Vector2(0.5f, 1f);
            pillRt.pivot = new Vector2(0.5f, 1f);
            pillRt.anchoredPosition = new Vector2(0f, -142f);
            pillRt.sizeDelta = new Vector2(290f, 34f);
            OurWorldUiKit.ApplyRounded(pill.GetComponent<Image>(), OurWorldUiKit.PillSprite,
                new Color(themeAccent.r, themeAccent.g, themeAccent.b, 0.14f));
            OurWorldUiKit.AddHairlineBorder(pill.transform, OurWorldUiKit.PillOutlineSprite,
                new Color(themeAccent.r, themeAccent.g, themeAccent.b, 0.36f));
            OurWorldUiKit.CreateStretchText("Eyebrow", pill.transform, eyebrow.ToUpperInvariant(),
                15, FontStyle.Bold, themeAccent, TextAnchor.MiddleCenter);

            // Title
            CreateTopText("Title", card.transform, info.Title, 36, FontStyle.Bold, OurWorldUiKit.TextPrimary, -190f, 700f, 64f);

            // Description
            Text body = CreateTopText("Body", card.transform, info.Description, 22, FontStyle.Normal, OurWorldUiKit.TextSecondary, -260f, 660f, 120f);
            body.lineSpacing = 1.35f;

            // Action Buttons
            if (info.IsMaintenance)
            {
                string retryLabel = LocalizationService.Tr("update.retry", "Tekrar Dene");
                Button retryBtn = OurWorldUiKit.CreatePillButton("RetryBtn", card.transform, retryLabel, 22,
                    OurWorldUiKit.AccentGold, OurWorldUiKit.InkDark, Color.clear, out _,
                    onClick: () =>
                    {
                        GameFeelAudio.PlayUiClick();
                        onRetryMaintenance?.Invoke();
                    });
                PlaceBottomCenter(retryBtn.transform, 340f, 58f, 44f);
            }
            else if (info.ForceUpdate)
            {
                string confirmLabel = !string.IsNullOrEmpty(info.ConfirmButtonText) ? info.ConfirmButtonText : "Güncelle";
                Button updateBtn = OurWorldUiKit.CreatePillButton("UpdateBtn", card.transform, confirmLabel, 22,
                    OurWorldUiKit.AccentTeal, OurWorldUiKit.InkDark, Color.clear, out _,
                    onClick: () =>
                    {
                        GameFeelAudio.PlayUiClick();
                        OpenStore(info);
                    });
                PlaceBottomCenter(updateBtn.transform, 380f, 58f, 44f);
            }
            else
            {
                string confirmLabel = !string.IsNullOrEmpty(info.ConfirmButtonText) ? info.ConfirmButtonText : "Güncelle";
                Button updateBtn = OurWorldUiKit.CreatePillButton("UpdateBtn", card.transform, confirmLabel, 22,
                    OurWorldUiKit.AccentTeal, OurWorldUiKit.InkDark, Color.clear, out _,
                    onClick: () =>
                    {
                        GameFeelAudio.PlayUiClick();
                        OpenStore(info);
                    });
                PlaceBottomCenter(updateBtn.transform, 380f, 56f, 98f);

                if (!string.IsNullOrEmpty(info.CancelButtonText))
                {
                    Button laterBtn = OurWorldUiKit.CreatePillButton("LaterBtn", card.transform, info.CancelButtonText, 19,
                        OurWorldUiKit.SurfaceBg, OurWorldUiKit.TextSecondary, OurWorldUiKit.BorderSubtle, out _,
                        onClick: () =>
                        {
                            GameFeelAudio.PlayUiClick();
                            Hide();
                            onSoftDismissed?.Invoke();
                        });
                    PlaceBottomCenter(laterBtn.transform, 280f, 48f, 34f);
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

        private static Text CreateTopText(string name, Transform parent, string content, int fontSize, FontStyle style, Color color, float yFromTop, float width, float height)
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

        private static void PlaceBottomCenter(Transform target, float width, float height, float bottomOffset)
        {
            RectTransform rt = (RectTransform)target;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, bottomOffset);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
