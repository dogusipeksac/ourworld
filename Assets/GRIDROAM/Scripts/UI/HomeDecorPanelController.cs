using OurWorld.Core;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Phase 3C Mobile-Ergonomic Home Decoration & Real Estate Upgrade Catalog (No:48 Hero House & Aydos Farm).
    /// Displays all 6 commercial Blender furniture/pergola/aquarium/lantern upgrades in a clean 2x3 grid.
    /// </summary>
    public sealed class HomeDecorPanelController : MonoBehaviour
    {
        public static HomeDecorPanelController Instance { get; private set; }
        public bool IsOpen { get; private set; }

        private GameObject _canvasObj;
        private GameObject _panelRoot;
        private Font _font;

        private Text _titleText;
        private Text _moneyText;
        private Text _footerText;
        private readonly Image[] _cardBgs = new Image[6];
        private readonly Text[] _cardTitles = new Text[6];
        private readonly Text[] _cardDescs = new Text[6];
        private readonly Text[] _cardPriceLabels = new Text[6];

        private static readonly Color BgDark = new Color(0.055f, 0.082f, 0.134f, 0.96f);
        private static readonly Color CardBg = new Color(0.105f, 0.148f, 0.228f, 0.96f);
        private static readonly Color CardOwned = new Color(0.095f, 0.34f, 0.32f, 0.98f);
        private static readonly Color AccentTeal = new Color(0.14f, 0.74f, 0.66f, 1f);
        private static readonly Color AccentGold = new Color(0.98f, 0.78f, 0.28f, 1f);
        private static readonly Color TextLight = new Color(0.96f, 0.98f, 1f, 1f);
        private static readonly Color TextMuted = new Color(0.76f, 0.84f, 0.92f, 1f);

        private static bool IsEn =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        public static HomeDecorPanelController EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_HomeDecorPanel");
            return go.AddComponent<HomeDecorPanelController>();
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
        }

        private void OnDisable()
        {
            if (IsOpen && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            if (_panelRoot == null) BuildUi();
            IsOpen = true;
            _panelRoot.SetActive(true);
            if (_footerText != null) _footerText.text = string.Empty;
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, true);
            }
            RefreshAll();
            GameFeelAudio.PlayUiClick();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (_panelRoot != null) _panelRoot.SetActive(false);
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }
            GameFeelAudio.PlayUiClick();
        }

        private void BuildUi()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Font.CreateDynamicFontFromOSFont("Arial", 22);

            _canvasObj = new GameObject("Canvas_OurWorld_HomeDecor");
            _canvasObj.layer = LayerMask.NameToLayer("UI");
            _canvasObj.transform.SetParent(transform, false);
            Canvas c = _canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 194;

            CanvasScaler cs = _canvasObj.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;
            _canvasObj.AddComponent<GraphicRaycaster>();

            _panelRoot = new GameObject("HomeDecorModalRoot", typeof(RectTransform), typeof(Image));
            _panelRoot.transform.SetParent(_canvasObj.transform, false);
            RectTransform rootRt = (RectTransform)_panelRoot.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            _panelRoot.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.66f);

            GameObject card = CreateRect("Card", _panelRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1300f, 820f), BgDark);
            Outline o = card.AddComponent<Outline>();
            o.effectColor = new Color(0.20f, 0.80f, 0.72f, 0.55f);
            o.effectDistance = new Vector2(2f, -2f);

            _titleText = CreateText("Title", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -26f), new Vector2(780f, 52f), 32, FontStyle.Bold, TextLight, TextAnchor.MiddleLeft);
            _moneyText = CreateText("Money", card.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-126f, -26f), new Vector2(280f, 52f), 30, FontStyle.Bold, AccentGold, TextAnchor.MiddleRight);

            GameObject closeGo = CreateRect("BtnClose", card.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-32f, -22f), new Vector2(68f, 60f), new Color(0.76f, 0.24f, 0.24f, 0.95f));
            Button closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeGo.GetComponent<Image>();
            closeBtn.onClick.AddListener(Close);
            CreateText("X", closeGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter).text = "X";

            // 2 columns x 3 rows grid of 6 real Blender furniture/property upgrades!
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                int col = i % 2;
                int row = i / 2;
                float x = 36f + col * 624f;
                float y = -104f - row * 212f;

                GameObject itemCard = CreateRect($"DecorItem_{i}", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(x, y), new Vector2(604f, 194f), CardBg);
                Outline io = itemCard.AddComponent<Outline>();
                io.effectColor = new Color(1f, 1f, 1f, 0.14f);
                io.effectDistance = new Vector2(1.5f, -1.5f);
                _cardBgs[i] = itemCard.GetComponent<Image>();

                Button btn = itemCard.AddComponent<Button>();
                btn.targetGraphic = _cardBgs[i];
                btn.onClick.AddListener(() =>
                {
                    if (HomeDecorManager.Instance != null)
                    {
                        HomeDecorManager.Instance.TryBuyUpgrade((HomeDecorUpgradeId)idx, out string msg);
                        if (_footerText != null) _footerText.text = msg;
                        RefreshAll();
                    }
                });

                _cardTitles[i] = CreateText("Title", itemCard.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(20f, -16f), new Vector2(-40f, 38f), 23, FontStyle.Bold, TextLight, TextAnchor.MiddleLeft);
                _cardTitles[i].rectTransform.pivot = new Vector2(0f, 1f);

                _cardDescs[i] = CreateText("Desc", itemCard.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(20f, -58f), new Vector2(-40f, 68f), 17, FontStyle.Normal, TextMuted, TextAnchor.UpperLeft);
                _cardDescs[i].rectTransform.pivot = new Vector2(0f, 1f);
                _cardDescs[i].horizontalOverflow = HorizontalWrapMode.Wrap;

                GameObject pill = CreateRect("PricePill", itemCard.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(20f, 16f), new Vector2(236f, 44f), AccentTeal);
                ((RectTransform)pill.transform).pivot = new Vector2(0f, 0f);
                _cardPriceLabels[i] = CreateText("PriceTxt", pill.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            }

            _footerText = CreateText("Footer", card.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 20f), new Vector2(-72f, 42f), 21, FontStyle.Bold, AccentGold, TextAnchor.MiddleCenter);
            ((RectTransform)_footerText.transform).pivot = new Vector2(0.5f, 0f);

            _panelRoot.SetActive(false);
        }

        private void RefreshAll()
        {
            if (_panelRoot == null) return;
            bool en = IsEn;
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;

            _titleText.text = en ? "NO:48 HOME DECOR & REAL ESTATE CATALOG" : "NO:48 EV DEKORASYONU & EMLAK KATALOĞU";
            _moneyText.text = $"₺ {(d != null ? d.money : 0)}";

            for (int i = 0; i < HomeDecorManager.Catalog.Length && i < 6; i++)
            {
                HomeDecorCatalogItem item = HomeDecorManager.Catalog[i];
                bool owned = HomeDecorManager.IsUnlocked(item.Id);
                _cardBgs[i].color = owned ? CardOwned : CardBg;
                _cardTitles[i].text = en ? item.NameEn : item.NameTr;
                _cardDescs[i].text = en ? item.DescEn : item.DescTr;
                _cardPriceLabels[i].text = owned
                    ? (en ? "INSTALLED ✓" : "KURULU ✓")
                    : (en ? $"BUY  •  {item.CostTl} ₺" : $"SATIN AL  •  {item.CostTl} ₺");
            }
        }

        private GameObject CreateRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color col)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(aMin.x, aMax.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = col;
            return go;
        }

        private Text CreateText(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, int fs, FontStyle style, Color col, TextAnchor align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(aMin.x, aMax.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Text t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = fs;
            t.fontStyle = style;
            t.color = col;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }
    }
}
