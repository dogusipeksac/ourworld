using System.Collections;
using Gridroam.Core;
using Gridroam.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// Safe-area modal shop panel for Café and Market counters:
    /// 3 energy-restoring items (Kahve 15 ₺ +25, Sandviç 22 ₺ +35, Sıcak Yemek 30 ₺ +50) + Close,
    /// animated with 0.2 s CanvasGroup fade + scale using unscaled time.
    /// </summary>
    public class ShopMenuPanel : MonoBehaviour
    {
        public static ShopMenuPanel Instance { get; private set; }

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text balanceSubLabel;
        [SerializeField] private Button[] itemButtons = new Button[3];
        [SerializeField] private Text[] itemLabels = new Text[3];
        [SerializeField] private Button closeButton;
        [SerializeField] private Text closeLabel;

        private static readonly int[] ItemPrices = { 15, 22, 30 };
        private static readonly float[] ItemEnergyGains = { 25f, 35f, 50f };
        private static readonly string[] ItemNameKeys =
        {
            "ui.shop.item_coffee",
            "ui.shop.item_sandwich",
            "ui.shop.item_meal"
        };
        private static readonly string[] ItemFallbacksTr =
        {
            "Taze Kahve · 15 ₺ (+25 Enerji)",
            "Sahil Sandviçi · 22 ₺ (+35 Enerji)",
            "Sıcak Yemek · 30 ₺ (+50 Enerji)"
        };

        private string _activeShopId = string.Empty;
        private int _openedFrame = -1;
        private Coroutine _animRoutine;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        public void WireReferences(
            GameObject root,
            CanvasGroup group,
            Text titleTxt,
            Text balanceTxt,
            Button[] buttons,
            Text[] labels,
            Button closeBtn,
            Text closeTxt)
        {
            panelRoot = root;
            canvasGroup = group;
            titleLabel = titleTxt;
            balanceSubLabel = balanceTxt;
            itemButtons = buttons;
            itemLabels = labels;
            closeButton = closeBtn;
            closeLabel = closeTxt;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (itemButtons != null)
            {
                for (int i = 0; i < itemButtons.Length; i++)
                {
                    int idx = i;
                    if (itemButtons[i] != null)
                    {
                        itemButtons[i].onClick.AddListener(() => TryBuyItem(idx));
                    }
                }
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged += HandleLanguageChanged;
            }

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnMoneyChanged += HandleMoneyChanged;
            }

            RefreshLabels();
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged -= HandleLanguageChanged;
            }
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnMoneyChanged -= HandleMoneyChanged;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleLanguageChanged(string _) => RefreshLabels();
        private void HandleMoneyChanged(int _) => RefreshLabels();

        public void Open(string shopId)
        {
            if (panelRoot == null) return;

            _activeShopId = shopId ?? string.Empty;
            _openedFrame = Time.frameCount;
            RefreshLabels();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, true);
            }

            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimatePanelRoutine(opening: true));
        }

        public void Close()
        {
            if (panelRoot == null || !panelRoot.activeSelf) return;

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
            }

            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimatePanelRoutine(opening: false));
        }

        private void TryBuyItem(int index)
        {
            if (index < 0 || index >= ItemPrices.Length || SaveManager.Instance == null)
            {
                return;
            }

            int cost = ItemPrices[index];
            float gain = ItemEnergyGains[index];

            if (!SaveManager.Instance.TrySpend(cost))
            {
                if (PlayerModeController.Instance != null)
                {
                    PlayerModeController.Instance.RaiseStatusMessage(LocalizationService.Tr("ui.shop.no_money", "Yetersiz bakiye!"));
                }
                return;
            }

            if (PlayerNeedsController.Instance != null)
            {
                PlayerNeedsController.Instance.RestoreEnergy(gain);
            }

            RefreshLabels();

            if (PlayerModeController.Instance != null)
            {
                string fmt = LocalizationService.Tr("ui.shop.bought", "Afiyet olsun! +{0} Enerji");
                PlayerModeController.Instance.RaiseStatusMessage(string.Format(fmt, Mathf.RoundToInt(gain)));
            }
        }

        public void RefreshLabels()
        {
            bool isMarket = !string.IsNullOrEmpty(_activeShopId) && _activeShopId.ToLowerInvariant().Contains("market");
            bool isPizza = !string.IsNullOrEmpty(_activeShopId) && _activeShopId.ToLowerInvariant().Contains("pizza");

            if (titleLabel != null)
            {
                if (isMarket)
                {
                    titleLabel.text = LocalizationService.Tr("ui.shop.market_title", "Kasaba Market");
                }
                else if (isPizza)
                {
                    titleLabel.text = LocalizationService.Tr("ui.shop.pizza_title", "Liman Pizza");
                }
                else
                {
                    titleLabel.text = LocalizationService.Tr("ui.shop.cafe_title", "Sahil Kafe");
                }
            }

            int money = SaveManager.Instance != null ? SaveManager.Instance.Data.money : 250;
            float energy = PlayerNeedsController.Instance != null ? PlayerNeedsController.Instance.Energy : 80f;

            if (balanceSubLabel != null)
            {
                string subFmt = LocalizationService.Tr("ui.shop.status_line", "Bakiye: ₺ {0}   ·   Enerji: %{1}");
                balanceSubLabel.text = string.Format(subFmt, money, Mathf.RoundToInt(energy));
            }

            if (itemLabels != null)
            {
                for (int i = 0; i < itemLabels.Length && i < ItemNameKeys.Length; i++)
                {
                    if (itemLabels[i] != null)
                    {
                        itemLabels[i].text = LocalizationService.Tr(ItemNameKeys[i], ItemFallbacksTr[i]);
                    }
                    if (itemButtons != null && i < itemButtons.Length && itemButtons[i] != null)
                    {
                        itemButtons[i].interactable = money >= ItemPrices[i];
                    }
                }
            }

            if (closeLabel != null)
            {
                closeLabel.text = LocalizationService.Tr("ui.btn.close", "Kapat");
            }
        }

        private void Update()
        {
            if (!IsOpen || Time.frameCount == _openedFrame) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) { TryBuyItem(0); return; }
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) { TryBuyItem(1); return; }
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) { TryBuyItem(2); }
        }

        private IEnumerator AnimatePanelRoutine(bool opening)
        {
            if (panelRoot == null) yield break;

            RectTransform rt = panelRoot.GetComponent<RectTransform>();
            if (opening)
            {
                panelRoot.SetActive(true);
            }

            const float duration = 0.20f;
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : (opening ? 0f : 1f);
            float endAlpha = opening ? 1f : 0f;
            Vector3 startScale = opening ? Vector3.one * 0.94f : Vector3.one;
            Vector3 endScale = opening ? Vector3.one : Vector3.one * 0.94f;

            if (opening && canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            if (rt != null)
            {
                rt.localScale = startScale;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                }
                if (rt != null)
                {
                    rt.localScale = Vector3.Lerp(startScale, endScale, t);
                }
                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = endAlpha;
            }
            if (rt != null)
            {
                rt.localScale = opening ? Vector3.one : endScale;
            }
            if (!opening)
            {
                panelRoot.SetActive(false);
            }

            _animRoutine = null;
        }
    }
}
