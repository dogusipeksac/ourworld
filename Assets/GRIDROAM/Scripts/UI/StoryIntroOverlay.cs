using System.Collections;
using Gridroam.Core;
using Gridroam.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Gridroam.UI
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

            // Wait while any higher-priority modal (Update / What's New) is active
            int guard = 0;
            while (PlayerModeController.Instance != null && PlayerModeController.Instance.IsModalLocked && guard < 120)
            {
                guard++;
                yield return new WaitForSecondsRealtime(0.25f);
            }

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
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _rootCanvas = new GameObject("StoryIntroCanvas");
            _rootCanvas.transform.SetParent(transform, false);
            Canvas canvas = _rootCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 185;
            CanvasScaler scaler = _rootCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            _rootCanvas.AddComponent<GraphicRaycaster>();

            // Fullscreen warm dark scrim
            GameObject scrim = new GameObject("Scrim");
            scrim.transform.SetParent(_rootCanvas.transform, false);
            Image scrimImg = scrim.AddComponent<Image>();
            scrimImg.color = new Color(0.05f, 0.08f, 0.13f, 0.84f);
            RectTransform srt = scrim.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            // Center editorial story card
            GameObject card = new GameObject("StoryCard");
            card.transform.SetParent(scrim.transform, false);
            Image cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.10f, 0.14f, 0.21f, 0.97f);
            RectTransform crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(760f, 430f);

            // Warm amber top accent bar
            GameObject accent = new GameObject("AccentBar");
            accent.transform.SetParent(card.transform, false);
            Image accentImg = accent.AddComponent<Image>();
            accentImg.color = new Color(0.95f, 0.68f, 0.22f, 1f);
            RectTransform art = accent.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0f, 1f);
            art.anchorMax = new Vector2(1f, 1f);
            art.pivot = new Vector2(0.5f, 1f);
            art.sizeDelta = new Vector2(0f, 6f);

            AddText(card.transform, "Eyebrow", font, 16, FontStyle.Bold, new Color(0.95f, 0.72f, 0.28f),
                isEn ? "OURWORLD  ·  ISTANBUL · HAMİDİYE NEIGHBORHOOD" : "OURWORLD  ·  İSTANBUL · HAMİDİYE MAHALLESİ",
                new Vector2(40f, -26f), new Vector2(680f, 28f), TextAnchor.MiddleLeft);

            AddText(card.transform, "Title", font, 32, FontStyle.Bold, Color.white,
                isEn ? "The Wind of Hamidiye" : "Hamidiye'nin Rüzgârı",
                new Vector2(40f, -62f), new Vector2(680f, 46f), TextAnchor.MiddleLeft);

            string body = isEn
                ? "After years away, you've returned to your grandfather's corner house on Selman Street.\n\n" +
                  "Warm corner shops, stray cats, kids playing football, the swimming pool, tennis court, and Fatih Boulevard's Clock Tower still beat with life — yet a shadowy developer called Kaya Holding wants to buy up the old deeds.\n\n" +
                  "Master Kemal is waiting outside Auto Repair No:73 with your grandfather's freshly tuned sedan. Explore all 8 avenues, uncover 12 Neighborhood Memories, and save Hamidiye together!"
                : "Yıllar sonra dedenden kalan Selman Sokağı'ndaki bahçeli eve döndün.\n\n" +
                  "Sıcak mahalle esnafı, sokak kedileri, tenis kortu, açık yüzme havuzu, Hz. Osman Camii ve Fatih Bulvarı Saat Kulesi hâlâ yaşam dolu — ancak Kaya Holding adlı bir şirket mahallenin eski tapularının peşinde.\n\n" +
                  "Usta Kemal, No:73 Oto Tamir'in önünde dedenin bakımdan çıkan sedanıyla seni bekliyor. 8 caddeyi keşfet, 12 Hamidiye Hatırası'nı topla ve mahallemizi birlikte koru!";

            AddText(card.transform, "Body", font, 19, FontStyle.Normal, new Color(0.86f, 0.90f, 0.95f),
                body, new Vector2(40f, -120f), new Vector2(680f, 215f), TextAnchor.UpperLeft);

            // Start button
            GameObject btnGo = new GameObject("StartButton");
            btnGo.transform.SetParent(card.transform, false);
            Image btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.16f, 0.64f, 0.56f, 1f);
            Button btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(Dismiss);
            RectTransform brt = btnGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0f);
            brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 28f);
            brt.sizeDelta = new Vector2(300f, 54f);

            GameObject lblGo = new GameObject("Label");
            lblGo.transform.SetParent(btnGo.transform, false);
            Text lbl = lblGo.AddComponent<Text>();
            lbl.font = font;
            lbl.fontSize = 20;
            lbl.fontStyle = FontStyle.Bold;
            lbl.color = Color.white;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.text = isEn ? "Step Into Hamidiye" : "Mahalleye Adım At";
            RectTransform lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
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
