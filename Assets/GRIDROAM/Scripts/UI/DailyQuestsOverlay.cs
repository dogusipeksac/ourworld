using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Flagship OurWorldUiKit modal for Daily Quests (with 1-tap reward claims)
    /// and 10 Neighborhood Achievements with live progress bars.
    /// Opened directly from the Phone panel or Barber/Mall quick links.
    /// </summary>
    public sealed class DailyQuestsOverlay : MonoBehaviour
    {
        public static DailyQuestsOverlay Instance { get; private set; }

        private Canvas _canvas;
        private GameObject _root;
        private RectTransform _listContent;
        private Text _titleText;
        private Text _subtitleText;
        private Button _tabDailyBtn;
        private Button _tabAchBtn;
        private Text _tabDailyLbl;
        private Text _tabAchLbl;
        private int _activeTab; // 0 = Daily Quests, 1 = Achievements
        private bool _isOpen;

        public bool IsOpen => _isOpen;

        public static DailyQuestsOverlay EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_DailyQuestsOverlay");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DailyQuestsOverlay>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildUi();
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_isOpen && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_isOpen) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        public void Show(int initialTab = 0)
        {
            _activeTab = Mathf.Clamp(initialTab, 0, 1);
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
            _canvas.sortingOrder = 195;

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
            Image scImg = _root.GetComponent<Image>();
            scImg.color = OurWorldUiKit.Scrim;

            GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(_root.transform, false);
            RectTransform crt = (RectTransform)card.transform;
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(920f, 690f);

            OurWorldUiKit.ApplyRounded(card.GetComponent<Image>(), OurWorldUiKit.CardSprite, OurWorldUiKit.CardBg);
            OurWorldUiKit.AddCardDropShadow(card.transform, 28f, -12f, 0.58f);
            OurWorldUiKit.AddSoftTopGlow(card.transform, OurWorldUiKit.AccentGold, 165f, 0.16f);
            OurWorldUiKit.AddHairlineBorder(card.transform, OurWorldUiKit.CardOutlineSprite, OurWorldUiKit.BorderSubtle);

            _titleText = OurWorldUiKit.CreateText("Title", card.transform, "GÜNLÜK GÖREVLER & BAŞARIMLAR",
                28, FontStyle.Bold, OurWorldUiKit.TextPrimary, TextAnchor.MiddleLeft);
            RectTransform trt = _titleText.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(34f, -26f);
            trt.sizeDelta = new Vector2(-120f, 38f);

            _subtitleText = OurWorldUiKit.CreateText("Subtitle", card.transform, "Her gün yeni ödüller kazan · Mahalle rozetlerini aç",
                17, FontStyle.Normal, OurWorldUiKit.TextSecondary, TextAnchor.MiddleLeft);
            RectTransform srt = _subtitleText.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(34f, -64f);
            srt.sizeDelta = new Vector2(-120f, 26f);

            Button closeBtn = OurWorldUiKit.CreateCircularIconButton(
                "Btn_Close", card.transform, OurWorldUiKit.IconClose, 44f, 18f,
                OurWorldUiKit.SurfaceBg, OurWorldUiKit.TextSecondary, OurWorldUiKit.BorderSubtle,
                () => { GameFeelAudio.PlayUiClick(); Close(); });
            RectTransform clRt = (RectTransform)closeBtn.transform;
            clRt.anchorMin = new Vector2(1f, 1f);
            clRt.anchorMax = new Vector2(1f, 1f);
            clRt.pivot = new Vector2(1f, 1f);
            clRt.anchoredPosition = new Vector2(-26f, -26f);

            // Tabs Row
            GameObject tabRow = new GameObject("Tabs", typeof(RectTransform));
            tabRow.transform.SetParent(card.transform, false);
            RectTransform tabRt = (RectTransform)tabRow.transform;
            tabRt.anchorMin = new Vector2(0f, 1f);
            tabRt.anchorMax = new Vector2(1f, 1f);
            tabRt.pivot = new Vector2(0.5f, 1f);
            tabRt.anchoredPosition = new Vector2(0f, -104f);
            tabRt.sizeDelta = new Vector2(-68f, 48f);

            HorizontalLayoutGroup thlg = tabRow.AddComponent<HorizontalLayoutGroup>();
            thlg.spacing = 14f;
            thlg.childControlWidth = true;
            thlg.childControlHeight = true;
            thlg.childForceExpandWidth = true;
            thlg.childForceExpandHeight = true;

            _tabDailyBtn = OurWorldUiKit.CreatePillButton("Tab_Daily", tabRow.transform, "Günlük Görevler", 19,
                OurWorldUiKit.AccentGold, OurWorldUiKit.InkDark, Color.clear, out _tabDailyLbl,
                () => { GameFeelAudio.PlayUiClick(); _activeTab = 0; RefreshView(); });

            _tabAchBtn = OurWorldUiKit.CreatePillButton("Tab_Achievements", tabRow.transform, "Başarımlar (10)", 19,
                OurWorldUiKit.SurfaceBg, OurWorldUiKit.TextSecondary, OurWorldUiKit.BorderSubtle, out _tabAchLbl,
                () => { GameFeelAudio.PlayUiClick(); _activeTab = 1; RefreshView(); });

            // Scrollable List Container
            GameObject scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image));
            scrollGo.transform.SetParent(card.transform, false);
            RectTransform scListRt = (RectTransform)scrollGo.transform;
            scListRt.anchorMin = Vector2.zero;
            scListRt.anchorMax = Vector2.one;
            scListRt.offsetMin = new Vector2(30f, 24f);
            scListRt.offsetMax = new Vector2(-30f, -168f);
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

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(vp.transform, false);
            _listContent = (RectTransform)content.transform;
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.anchoredPosition = Vector2.zero;
            _listContent.sizeDelta = new Vector2(0f, 600f);
            sr.content = _listContent;

            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(4, 4, 4, 14);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void RefreshView()
        {
            bool en = LocalizationService.IsEnglish;
            _titleText.text = en ? "DAILY QUESTS & ACHIEVEMENTS" : "GÜNLÜK GÖREVLER & BAŞARIMLAR";
            _subtitleText.text = en
                ? "Complete neighborhood goals every day to earn cash & reputation badges"
                : "Her gün mahalle görevlerini tamamla, nakit ve saygınlık rozetleri kazan";

            int readyCount = DailyQuestAndAchievementSystem.GetUnclaimedReadyCount();
            string readySuffix = readyCount > 0 ? $"  ({readyCount}!)" : string.Empty;
            _tabDailyLbl.text = (en ? "Daily Quests" : "Günlük Görevler") + readySuffix;

            AchievementInfo[] achs = DailyQuestAndAchievementSystem.GetAchievements();
            int unlockedCount = 0;
            for (int i = 0; i < achs.Length; i++) if (achs[i].Unlocked) unlockedCount++;
            _tabAchLbl.text = en ? $"Achievements ({unlockedCount}/{achs.Length})" : $"Başarımlar ({unlockedCount}/{achs.Length})";

            StyleTab(_tabDailyBtn, _tabDailyLbl, _activeTab == 0, OurWorldUiKit.AccentGold);
            StyleTab(_tabAchBtn, _tabAchLbl, _activeTab == 1, OurWorldUiKit.AccentTeal);

            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_listContent.GetChild(i).gameObject);
            }

            if (_activeTab == 0)
            {
                DailyQuestInfo[] quests = DailyQuestAndAchievementSystem.GetDailyQuests();
                for (int i = 0; i < quests.Length; i++)
                {
                    BuildDailyQuestRow(quests[i], en);
                }
            }
            else
            {
                for (int i = 0; i < achs.Length; i++)
                {
                    BuildAchievementRow(achs[i], en);
                }
            }
        }

        private static void StyleTab(Button btn, Text lbl, bool active, Color activeBg)
        {
            if (btn == null) return;
            Image img = btn.GetComponent<Image>();
            if (img != null) img.color = active ? activeBg : OurWorldUiKit.SurfaceBg;
            if (lbl != null) lbl.color = active ? OurWorldUiKit.InkDark : OurWorldUiKit.TextSecondary;
        }

        private void BuildDailyQuestRow(DailyQuestInfo q, bool en)
        {
            GameObject row = new GameObject($"Daily_{q.Index}", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(_listContent, false);
            row.AddComponent<LayoutElement>().preferredHeight = 128f;

            Color bg = q.ReadyToClaim ? OurWorldUiKit.SurfaceActive : OurWorldUiKit.SurfaceBg;
            OurWorldUiKit.ApplyRounded(row.GetComponent<Image>(), OurWorldUiKit.SubCardSprite, bg);
            OurWorldUiKit.AddHairlineBorder(row.transform, OurWorldUiKit.SubCardOutlineSprite,
                q.ReadyToClaim ? OurWorldUiKit.AccentGold : OurWorldUiKit.BorderSubtle);

            string title = en ? q.TitleEn : q.TitleTr;
            string desc = en ? q.DescEn : q.DescTr;
            Text info = OurWorldUiKit.CreateText("Info", row.transform,
                $"<b>{title}</b>   <color=#F7BF3D>+{q.RewardCash} ₺ · +{q.RewardXp} XP</color>\n<size=16><color=#B8CBD9>{desc}</color></size>",
                21, FontStyle.Normal, OurWorldUiKit.TextPrimary, TextAnchor.UpperLeft);
            info.supportRichText = true;
            RectTransform irt = info.rectTransform;
            irt.anchorMin = new Vector2(0f, 0.34f);
            irt.anchorMax = new Vector2(0.70f, 1f);
            irt.offsetMin = new Vector2(22f, 0f);
            irt.offsetMax = new Vector2(-10f, -14f);

            // Progress bar
            BuildProgressBar(row.transform, q.Current, q.Target, q.Claimed || q.ReadyToClaim ? OurWorldUiKit.AccentGold : OurWorldUiKit.AccentTeal, 0.68f);

            // Right status / claim button
            int idx = q.Index;
            string btnLabel = q.Claimed
                ? (en ? "CLAIMED ✓" : "ALINDI ✓")
                : (q.ReadyToClaim ? (en ? $"CLAIM +{q.RewardCash} ₺" : $"AL +{q.RewardCash} ₺") : $"{q.Current} / {q.Target}");
            Color btnCol = q.ReadyToClaim ? OurWorldUiKit.AccentGold : (q.Claimed ? OurWorldUiKit.SurfaceSoft : OurWorldUiKit.CardBg);
            Color txtCol = q.ReadyToClaim ? OurWorldUiKit.InkDark : (q.Claimed ? OurWorldUiKit.AccentTeal : OurWorldUiKit.TextSecondary);

            Button actBtn = OurWorldUiKit.CreatePillButton("Btn_Claim", row.transform, btnLabel, 17, btnCol, txtCol,
                q.ReadyToClaim ? Color.clear : OurWorldUiKit.BorderSubtle, out _, () =>
                {
                    if (DailyQuestAndAchievementSystem.TryClaimDailyQuest(idx))
                    {
                        RefreshView();
                    }
                    else
                    {
                        GameFeelAudio.PlayUiClick();
                    }
                });
            actBtn.interactable = q.ReadyToClaim;
            RectTransform brt = (RectTransform)actBtn.transform;
            brt.anchorMin = new Vector2(1f, 0.5f);
            brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = new Vector2(-20f, 0f);
            brt.sizeDelta = new Vector2(204f, 52f);
        }

        private void BuildAchievementRow(AchievementInfo a, bool en)
        {
            GameObject row = new GameObject($"Ach_{a.Id}", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(_listContent, false);
            row.AddComponent<LayoutElement>().preferredHeight = 96f;

            OurWorldUiKit.ApplyRounded(row.GetComponent<Image>(), OurWorldUiKit.SubCardSprite,
                a.Unlocked ? OurWorldUiKit.SurfaceActive : OurWorldUiKit.SurfaceBg);
            OurWorldUiKit.AddHairlineBorder(row.transform, OurWorldUiKit.SubCardOutlineSprite,
                a.Unlocked ? OurWorldUiKit.AccentTeal : OurWorldUiKit.BorderSubtle);

            string icon = a.Unlocked ? "★" : "☆";
            string colHex = a.Unlocked ? "24C7B3" : "8A9EB0";
            string title = en ? a.TitleEn : a.TitleTr;
            string desc = en ? a.DescEn : a.DescTr;

            Text info = OurWorldUiKit.CreateText("Info", row.transform,
                $"<color=#{colHex}><b>{icon}  {title}</b></color>\n<size=15><color=#B8CBD9>{desc}</color></size>",
                19, FontStyle.Normal, OurWorldUiKit.TextPrimary, TextAnchor.UpperLeft);
            info.supportRichText = true;
            RectTransform irt = info.rectTransform;
            irt.anchorMin = new Vector2(0f, 0.36f);
            irt.anchorMax = new Vector2(0.76f, 1f);
            irt.offsetMin = new Vector2(20f, 0f);
            irt.offsetMax = new Vector2(-10f, -10f);

            BuildProgressBar(row.transform, a.Current, a.Target, a.Unlocked ? OurWorldUiKit.AccentTeal : OurWorldUiKit.AccentSky, 0.72f);

            Text badge = OurWorldUiKit.CreateText("Badge", row.transform,
                a.Unlocked ? (en ? "UNLOCKED ✓" : "AÇILDI ✓") : $"{a.Current}/{a.Target}",
                17, FontStyle.Bold, a.Unlocked ? OurWorldUiKit.AccentGold : OurWorldUiKit.TextSecondary, TextAnchor.MiddleRight);
            RectTransform brt = badge.rectTransform;
            brt.anchorMin = new Vector2(1f, 0.5f);
            brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = new Vector2(-22f, 0f);
            brt.sizeDelta = new Vector2(160f, 36f);
        }

        private static void BuildProgressBar(Transform parent, int cur, int target, Color fillCol, float maxAnchorX)
        {
            GameObject bg = new GameObject("ProgBg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(parent, false);
            RectTransform bgrt = (RectTransform)bg.transform;
            bgrt.anchorMin = new Vector2(0f, 0f);
            bgrt.anchorMax = new Vector2(maxAnchorX, 0f);
            bgrt.pivot = new Vector2(0f, 0f);
            bgrt.offsetMin = new Vector2(22f, 14f);
            bgrt.offsetMax = new Vector2(-12f, 24f);
            OurWorldUiKit.ApplyRounded(bg.GetComponent<Image>(), OurWorldUiKit.PillSprite, new Color(1f, 1f, 1f, 0.12f));

            float frac = target > 0 ? Mathf.Clamp01((float)cur / target) : 0f;
            GameObject fill = new GameObject("ProgFill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bg.transform, false);
            RectTransform frt = (RectTransform)fill.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(Mathf.Max(0.02f, frac), 1f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            OurWorldUiKit.ApplyRounded(fill.GetComponent<Image>(), OurWorldUiKit.PillSprite, fillCol);
        }
    }
}
