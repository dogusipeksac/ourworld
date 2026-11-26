using System.Collections;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Atmospheric one-time story prologue card shown on a brand-new save to introduce
    /// Deniz's return to Istanbul · Sultanbeyli · Hamidiye Mahallesi and the Kaya Holding mystery.
    /// </summary>
    public sealed class StoryIntroOverlay : MonoBehaviour
    {
        private GameObject _rootCanvas;
        private bool _holdingLock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            if (FindAnyObjectByType<StoryIntroOverlay>() != null) return;
            GameObject go = new GameObject("StoryIntroOverlay");
            go.AddComponent<StoryIntroOverlay>();
        }

        private void Start()
        {
            StartCoroutine(MaybeShowIntroRoutine());
        }

        private void OnDestroy()
        {
            ReleaseLock();
        }

        private IEnumerator MaybeShowIntroRoutine()
        {
            // Give Update/WhatsNew overlays a moment to claim priority if needed
            yield return new WaitForSecondsRealtime(0.85f);

            SaveManager sm = SaveManager.Instance;
            if (sm == null || sm.Data == null) yield break;
            if (sm.Data.hasSeenStoryIntro || sm.Data.completedMissionIds.Count > 0) yield break;

            // Wait while the Main Menu, any higher-priority modal (Update / What's New), or an active Sport Match is in progress
            int guard = 0;
            while ((MainMenuController.IsMenuActive ||
                    (PlayerModeController.Instance != null &&
                     (PlayerModeController.Instance.IsModalLocked ||
                      PlayerModeController.Instance.CurrentMode == ControlMode.Tennis ||
                      PlayerModeController.Instance.CurrentMode == ControlMode.ArenaSport))) && guard < 1200)
            {
                guard++;
                yield return new WaitForSecondsRealtime(0.25f);
            }

            // Re-verify save data in case the player reset or completed missions before dismissing
            if (sm == null || sm.Data == null || sm.Data.hasSeenStoryIntro || sm.Data.completedMissionIds.Count > 0) yield break;

            ShowCard();
        }

        private void ShowCard()
        {
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, true);
                _holdingLock = true;
            }

            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            OurWorldUiKit.EnsureInitialized();
            Font font = OurWorldUiKit.DefaultFont;

            _rootCanvas = new GameObject("StoryIntroCanvas");
            _rootCanvas.transform.SetParent(transform, false);
            Canvas canvas = _rootCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 185;
            CanvasScaler scaler = _rootCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _rootCanvas.AddComponent<GraphicRaycaster>();

            // Fullscreen flagship scrim
            GameObject scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            scrim.transform.SetParent(_rootCanvas.transform, false);
            Image scrimImg = scrim.GetComponent<Image>();
            scrimImg.color = OurWorldUiKit.Scrim;
            RectTransform srt = (RectTransform)scrim.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            // Flagship WhatsNew-style elevated rounded card
            GameObject card = new GameObject("StoryCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(scrim.transform, false);
            RectTransform crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(1020f, 620f);
            OurWorldUiKit.ApplyRounded(card.GetComponent<Image>(), OurWorldUiKit.CardSprite, OurWorldUiKit.CardBg);
            OurWorldUiKit.AddCardDropShadow(card.transform, spread: 30f, offsetY: -14f, alpha: 0.62f);
            OurWorldUiKit.AddSoftTopGlow(card.transform, OurWorldUiKit.AccentGold, heightPx: 210f, alpha: 0.16f);
            OurWorldUiKit.AddHairlineBorder(card.transform, OurWorldUiKit.CardOutlineSprite,
                Color.Lerp(OurWorldUiKit.BorderSubtle, OurWorldUiKit.AccentGold, 0.24f));

            // Top eyebrow capsule pill with live gold dot
            GameObject pill = new GameObject("EyebrowPill", typeof(RectTransform), typeof(Image));
            pill.transform.SetParent(card.transform, false);
            RectTransform prt = (RectTransform)pill.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(40f, -28f);
            prt.sizeDelta = new Vector2(490f, 40f);
            OurWorldUiKit.ApplyRounded(pill.GetComponent<Image>(), OurWorldUiKit.PillSprite,
                new Color(OurWorldUiKit.AccentGold.r, OurWorldUiKit.AccentGold.g, OurWorldUiKit.AccentGold.b, 0.12f));
            OurWorldUiKit.AddHairlineBorder(pill.transform, OurWorldUiKit.PillOutlineSprite,
                new Color(OurWorldUiKit.AccentGold.r, OurWorldUiKit.AccentGold.g, OurWorldUiKit.AccentGold.b, 0.36f));

            GameObject dot = new GameObject("LiveDot", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(pill.transform, false);
            RectTransform drt = (RectTransform)dot.transform;
            drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = new Vector2(22f, 0f);
            drt.sizeDelta = new Vector2(9f, 9f);
            Image dotImg = dot.GetComponent<Image>();
            dotImg.sprite = OurWorldUiKit.CircleSprite;
            dotImg.color = OurWorldUiKit.AccentGold;
            dotImg.raycastTarget = false;

            Text eyebrowTxt = OurWorldUiKit.CreateStretchText("Eyebrow", pill.transform,
                isEn ? "OURWORLD  ·  ISTANBUL · HAMİDİYE" : "OURWORLD  ·  İSTANBUL · HAMİDİYE MAHALLESİ",
                16, FontStyle.Bold, OurWorldUiKit.AccentGold, TextAnchor.MiddleLeft, 38f, 0f);
            eyebrowTxt.rectTransform.offsetMax = new Vector2(-14f, 0f);

            // Top-right hero circular world medallion
            GameObject medGo = new GameObject("HeroMedallion", typeof(RectTransform), typeof(Image));
            medGo.transform.SetParent(card.transform, false);
            RectTransform mrt = (RectTransform)medGo.transform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(1f, 1f);
            mrt.pivot = new Vector2(1f, 1f);
            mrt.anchoredPosition = new Vector2(-40f, -24f);
            mrt.sizeDelta = new Vector2(68f, 68f);
            Image medBg = medGo.GetComponent<Image>();
            medBg.sprite = OurWorldUiKit.CircleSprite;
            medBg.color = new Color(OurWorldUiKit.AccentGold.r, OurWorldUiKit.AccentGold.g, OurWorldUiKit.AccentGold.b, 0.14f);
            medBg.raycastTarget = false;
            OurWorldUiKit.AddHairlineBorder(medGo.transform, OurWorldUiKit.CircleOutlineSprite,
                new Color(OurWorldUiKit.AccentGold.r, OurWorldUiKit.AccentGold.g, OurWorldUiKit.AccentGold.b, 0.48f));
            OurWorldUiKit.AddIcon("WorldIcon", medGo.transform, OurWorldUiKit.IconWorld, 34f, OurWorldUiKit.AccentGold);

            AddText(card.transform, "Title", font, 40, FontStyle.Bold, OurWorldUiKit.TextPrimary,
                isEn ? "The Wind of Hamidiye" : "Hamidiye'nin Rüzgârı",
                new Vector2(40f, -86f), new Vector2(820f, 54f), TextAnchor.MiddleLeft);

            OurWorldUiKit.AddDividerLine(card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -152f), new Vector2(-40f, -150.5f),
                OurWorldUiKit.Divider);

            string body = isEn
                ? "After years away, you've returned to your grandfather's corner house on Selman Street.\n\n" +
                  "Warm corner shops, stray cats, kids playing football, the swimming pool, tennis court, Fatih Boulevard's Clock Tower, Havadis Street's stone bakery, and Kadife Street's tea house beat with life — yet Kaya Holding wants to buy up the old deeds.\n\n" +
                  "Master Kemal is waiting outside Auto Repair No:73 with your grandfather's freshly tuned sedan. Explore all 10 avenues and streets, chat with smart neighbors, and save Hamidiye together!"
                : "Yıllar sonra dedenden kalan Selman Sokağı'ndaki bahçeli eve döndün.\n\n" +
                  "Sıcak mahalle esnafı, sokak kedileri, tenis kortu, açık yüzme havuzu, Hz. Osman Camii, Fatih Bulvarı Saat Kulesi, Havadis Sokağı taş fırını ve Kadife Sokağı kıraathanesi yaşam dolu — ancak Kaya Holding mahallenin eski tapularının peşinde.\n\n" +
                  "Usta Kemal, No:73 Oto Tamir'in önünde dedenin bakımdan çıkan sedanıyla seni bekliyor. Tüm caddeleri keşfet, zeki komşularla sohbet et ve mahallemizi birlikte koru!";

            AddText(card.transform, "Body", font, 23, FontStyle.Normal, OurWorldUiKit.TextSecondary,
                body, new Vector2(40f, -172f), new Vector2(940f, 300f), TextAnchor.UpperLeft);

            OurWorldUiKit.AddDividerLine(card.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(40f, 108f), new Vector2(-40f, 109.5f),
                OurWorldUiKit.Divider);

            // Flagship Turquoise Pill CTA Button with dark ink text
            Button btn = OurWorldUiKit.CreatePillButton(
                "StartButton", card.transform,
                isEn ? "Step Into Hamidiye  →" : "Mahalleye Adım At  →",
                22, OurWorldUiKit.AccentTeal, OurWorldUiKit.InkDark, Color.clear,
                out _, onClick: Dismiss);
            RectTransform brt = (RectTransform)btn.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 28f);
            brt.sizeDelta = new Vector2(340f, 58f);
        }

        private void Dismiss()
        {
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                SaveManager.Instance.Data.hasSeenStoryIntro = true;
                SaveManager.Instance.Save();
            }
            GameFeelAudio.PlayUiClick();
            ReleaseLock();
            if (_rootCanvas != null)
            {
                Destroy(_rootCanvas);
                _rootCanvas = null;
            }
            Destroy(gameObject);
        }

        private void ReleaseLock()
        {
            if (_holdingLock && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
            _holdingLock = false;
        }

        private static void AddText(Transform parent, string name, Font font, int size, FontStyle style, Color color,
            string value, Vector2 topLeftOffset, Vector2 boxSize, TextAnchor anchor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.22f;
            t.text = value;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeftOffset;
            rt.sizeDelta = boxSize;
        }
    }
}
