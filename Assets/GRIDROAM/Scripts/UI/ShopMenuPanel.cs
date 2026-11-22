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
        [SerializeField] private Button[] itemButtons = new Button[5];
        [SerializeField] private Text[] itemLabels = new Text[5];
        [SerializeField] private Button closeButton;
        [SerializeField] private Text closeLabel;

        private enum ShopOfferKind
        {
            Energy,
            RunningShoes,
            TurboKit,
            JacketColor,
            FuelCan
        }

        private struct ShopOffer
        {
            public string LabelTr;
            public int Price;
            public ShopOfferKind Kind;
            public float Param;
        }

        private static readonly ShopOffer[] FoodShopOffers =
        {
            new ShopOffer { LabelTr = "Türk Kahvesi · 15 ₺ (+25 Enerji)",           Price = 15, Kind = ShopOfferKind.Energy, Param = 25f },
            new ShopOffer { LabelTr = "Kaşarlı Tost · 25 ₺ (+40 Enerji)",           Price = 25, Kind = ShopOfferKind.Energy, Param = 40f },
            new ShopOffer { LabelTr = "Sıcak Yemek / Pizza · 40 ₺ (+65 Enerji)",    Price = 40, Kind = ShopOfferKind.Energy, Param = 65f },
            new ShopOffer { LabelTr = "Enerji İçeceği · 55 ₺ (Tam Enerji %100)",    Price = 55, Kind = ShopOfferKind.Energy, Param = 100f },
            new ShopOffer { LabelTr = "Yol Kumanyası · 75 ₺ (Tam Enerji + Depo)",   Price = 75, Kind = ShopOfferKind.FuelCan, Param = 100f }
        };

        private static readonly ShopOffer[] GearVendorOffers =
        {
            new ShopOffer { LabelTr = "Koşu Ayakkabısı · 180 ₺ (Kalıcı +%22 Hız)",   Price = 180, Kind = ShopOfferKind.RunningShoes, Param = 0f },
            new ShopOffer { LabelTr = "Bordo Deri Ceket · 220 ₺ (Kıyafet Değiştir)", Price = 220, Kind = ShopOfferKind.JacketColor,  Param = 1f },
            new ShopOffer { LabelTr = "Zeytin Parka · 220 ₺ (Kıyafet Değiştir)",     Price = 220, Kind = ShopOfferKind.JacketColor,  Param = 2f },
            new ShopOffer { LabelTr = "Antrasit Ceket · 220 ₺ (Kıyafet Değiştir)",   Price = 220, Kind = ShopOfferKind.JacketColor,  Param = 3f },
            new ShopOffer { LabelTr = "Soğuk Meyve Suyu · 20 ₺ (+30 Enerji)",        Price = 20,  Kind = ShopOfferKind.Energy,       Param = 30f }
        };

        private static readonly ShopOffer[] AutoVendorOffers =
        {
            new ShopOffer { LabelTr = "Turbo Motor Kiti · 350 ₺ (Kalıcı +%28 Güç)",  Price = 350, Kind = ShopOfferKind.TurboKit,    Param = 0f },
            new ShopOffer { LabelTr = "Acil Yakıt Bidonu · 60 ₺ (Depoyu Doldur)",    Price = 60,  Kind = ShopOfferKind.FuelCan,     Param = 0f },
            new ShopOffer { LabelTr = "Klasik Lacivert Ceket · 120 ₺ (Kıyafet)",     Price = 120, Kind = ShopOfferKind.JacketColor, Param = 0f },
            new ShopOffer { LabelTr = "Tamirci Kahvesi · 15 ₺ (+25 Enerji)",         Price = 15,  Kind = ShopOfferKind.Energy,      Param = 25f },
            new ShopOffer { LabelTr = "Enerji Barı · 35 ₺ (+55 Enerji)",             Price = 35,  Kind = ShopOfferKind.Energy,      Param = 55f }
        };

        private static readonly ShopOffer[] SimitVendorOffers =
        {
            new ShopOffer { LabelTr = "Sıcak Susamlı Simit · 12 ₺ (+22 Enerji)",       Price = 12, Kind = ShopOfferKind.Energy,  Param = 22f },
            new ShopOffer { LabelTr = "İnce Belli Demli Çay · 10 ₺ (+18 Enerji)",      Price = 10, Kind = ShopOfferKind.Energy,  Param = 18f },
            new ShopOffer { LabelTr = "Sıcak Kaşarlı Poğaça · 18 ₺ (+32 Enerji)",      Price = 18, Kind = ShopOfferKind.Energy,  Param = 32f },
            new ShopOffer { LabelTr = "Simit + Ayran Menüsü · 24 ₺ (+44 Enerji)",      Price = 24, Kind = ShopOfferKind.Energy,  Param = 44f },
            new ShopOffer { LabelTr = "Büyük Termos Çay & Simit · 45 ₺ (+85 Enerji)",  Price = 45, Kind = ShopOfferKind.Energy,  Param = 85f }
        };

        private static readonly ShopOffer[] ManavOffers =
        {
            new ShopOffer { LabelTr = "Taze Portakal & Mandalina · 18 ₺ (+30 Enerji)", Price = 18, Kind = ShopOfferKind.Energy,  Param = 30f },
            new ShopOffer { LabelTr = "Tarla Domatesi & Biber · 22 ₺ (+35 Enerji)",    Price = 22, Kind = ShopOfferKind.Energy,  Param = 35f },
            new ShopOffer { LabelTr = "Aydos Elması & Muz · 28 ₺ (+45 Enerji)",        Price = 28, Kind = ShopOfferKind.Energy,  Param = 45f },
            new ShopOffer { LabelTr = "Karışık Vitamin Sepeti · 42 ₺ (+75 Enerji)",    Price = 42, Kind = ShopOfferKind.Energy,  Param = 75f },
            new ShopOffer { LabelTr = "Aile Boyu Manav Kasası · 60 ₺ (%100 Enerji)",   Price = 60, Kind = ShopOfferKind.Energy,  Param = 100f }
        };

        private static readonly ShopOffer[] SarkuteriOffers =
        {
            new ShopOffer { LabelTr = "Ezine Beyaz Peynir · 24 ₺ (+38 Enerji)",        Price = 24, Kind = ShopOfferKind.Energy,  Param = 38f },
            new ShopOffer { LabelTr = "Gemlik Siyah Zeytin · 20 ₺ (+30 Enerji)",       Price = 20, Kind = ShopOfferKind.Energy,  Param = 30f },
            new ShopOffer { LabelTr = "Kayseri Sucuklu Tost · 36 ₺ (+58 Enerji)",      Price = 36, Kind = ShopOfferKind.Energy,  Param = 58f },
            new ShopOffer { LabelTr = "Taze Kaşar & Bal Kaymak · 48 ₺ (+80 Enerji)",   Price = 48, Kind = ShopOfferKind.Energy,  Param = 80f },
            new ShopOffer { LabelTr = "Serpme Kahvaltı Paketi · 68 ₺ (%100 Enerji)",   Price = 68, Kind = ShopOfferKind.Energy,  Param = 100f }
        };

        private static readonly ShopOffer[] DondurmaOffers =
        {
            new ShopOffer { LabelTr = "Külah Dondurma · 15 ₺ (+24 Enerji)",            Price = 15, Kind = ShopOfferKind.Energy,  Param = 24f },
            new ShopOffer { LabelTr = "Buz Gibi Yayık Ayran · 14 ₺ (+25 Enerji)",      Price = 14, Kind = ShopOfferKind.Energy,  Param = 25f },
            new ShopOffer { LabelTr = "Soğuk Gazoz & Limonata · 18 ₺ (+32 Enerji)",    Price = 18, Kind = ShopOfferKind.Energy,  Param = 32f },
            new ShopOffer { LabelTr = "Maraş Dondurma Kutusu · 38 ₺ (+65 Enerji)",     Price = 38, Kind = ShopOfferKind.Energy,  Param = 65f },
            new ShopOffer { LabelTr = "Soğuk Enerji İçeceği · 50 ₺ (%100 Enerji)",     Price = 50, Kind = ShopOfferKind.Energy,  Param = 100f }
        };

        private static readonly ShopOffer[] CayOcagiOffers =
        {
            new ShopOffer { LabelTr = "İnce Belli Demli Çay · 10 ₺ (+20 Enerji)",      Price = 10, Kind = ShopOfferKind.Energy,  Param = 20f },
            new ShopOffer { LabelTr = "Közde Türk Kahvesi · 20 ₺ (+35 Enerji)",        Price = 20, Kind = ShopOfferKind.Energy,  Param = 35f },
            new ShopOffer { LabelTr = "Oralet / Kuşburnu · 14 ₺ (+26 Enerji)",         Price = 14, Kind = ShopOfferKind.Energy,  Param = 26f },
            new ShopOffer { LabelTr = "Kaşarlı Karışık Tost · 32 ₺ (+55 Enerji)",      Price = 32, Kind = ShopOfferKind.Energy,  Param = 55f },
            new ShopOffer { LabelTr = "Esnaf Kahvaltı Tabağı · 55 ₺ (%100 Enerji)",    Price = 55, Kind = ShopOfferKind.Energy,  Param = 100f }
        };

        private static readonly ShopOffer[] SelmanBakkalOffers =
        {
            new ShopOffer { LabelTr = "Taze Somun Ekmek & Peynir · 18 ₺ (+30 Enerji)", Price = 18, Kind = ShopOfferKind.Energy,  Param = 30f },
            new ShopOffer { LabelTr = "Manav Meyve Sepeti · 25 ₺ (+42 Enerji)",        Price = 25, Kind = ShopOfferKind.Energy,  Param = 42f },
            new ShopOffer { LabelTr = "Şarküteri Sucuklu Tost · 35 ₺ (+58 Enerji)",    Price = 35, Kind = ShopOfferKind.Energy,  Param = 58f },
            new ShopOffer { LabelTr = "Buz Gibi Ayran & Dondurma · 30 ₺ (+50 Enerji)", Price = 30, Kind = ShopOfferKind.Energy,  Param = 50f },
            new ShopOffer { LabelTr = "Haftalık Market Kumanyası · 68 ₺ (Enerji+Depo)",Price = 68, Kind = ShopOfferKind.FuelCan, Param = 100f }
        };

        private static readonly ShopOffer[] HafsaTesisatOffers =
        {
            new ShopOffer { LabelTr = "Acil Yakıt Bidonu · 55 ₺ (Depoyu Doldur)",        Price = 55,  Kind = ShopOfferKind.FuelCan,      Param = 0f },
            new ShopOffer { LabelTr = "Turbo & Tesisat Kiti · 320 ₺ (Kalıcı +%28 Güç)",  Price = 320, Kind = ShopOfferKind.TurboKit,     Param = 0f },
            new ShopOffer { LabelTr = "Rahat Yürüyüş Ayakkabısı · 165 ₺ (+%22 Hız)",     Price = 165, Kind = ShopOfferKind.RunningShoes, Param = 0f },
            new ShopOffer { LabelTr = "Zeytin İş Parkası · 200 ₺ (Kıyafet Değiştir)",    Price = 200, Kind = ShopOfferKind.JacketColor,  Param = 2f },
            new ShopOffer { LabelTr = "Usta İkramı Çay & Kek · 12 ₺ (+22 Enerji)",       Price = 12,  Kind = ShopOfferKind.Energy,       Param = 22f }
        };

        private ShopOffer[] GetActiveOffers()
        {
            string id = (_activeShopId ?? string.Empty).ToLowerInvariant();
            if (id.Contains("vendor_bakery") || id.Contains("simit")) return SimitVendorOffers;
            if (id.Contains("manav")) return ManavOffers;
            if (id.Contains("sarkuteri")) return SarkuteriOffers;
            if (id.Contains("dondurma")) return DondurmaOffers;
            if (id.Contains("shop_hafsa") || id.Contains("hafsa")) return HafsaTesisatOffers;
            if (id.Contains("shop_bakkal") || id.Contains("bakkal")) return SelmanBakkalOffers;
            if (id.Contains("coffee") || id.Contains("cay")) return CayOcagiOffers;
            if (id.Contains("vendor_gear")) return GearVendorOffers;
            if (id.Contains("vendor_auto")) return AutoVendorOffers;
            return FoodShopOffers;
        }

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
            ShopOffer[] offers = GetActiveOffers();
            if (index < 0 || index >= offers.Length || SaveManager.Instance == null)
            {
                return;
            }

            ShopOffer offer = offers[index];
            SaveData data = SaveManager.Instance.Data;

            if (offer.Kind == ShopOfferKind.RunningShoes && data.hasRunningShoes)
            {
                if (PlayerModeController.Instance != null)
                {
                    PlayerModeController.Instance.RaiseStatusMessage("Koşu ayakkabısına zaten sahipsin!");
                }
                return;
            }
            if (offer.Kind == ShopOfferKind.TurboKit && data.hasTurboKit)
            {
                if (PlayerModeController.Instance != null)
                {
                    PlayerModeController.Instance.RaiseStatusMessage("Turbo Motor Kiti aracında zaten takılı!");
                }
                return;
            }

            if (!SaveManager.Instance.TrySpend(offer.Price))
            {
                if (PlayerModeController.Instance != null)
                {
                    PlayerModeController.Instance.RaiseStatusMessage(LocalizationService.Tr("ui.shop.no_money", "Yetersiz bakiye!"));
                }
                return;
            }

            GameFeelAudio.PlayCoinChime();
            if (PlayerModeController.Instance != null)
            {
                GameFeelAudio.SpawnBurst(PlayerModeController.Instance.ActiveWorldPosition + Vector3.up * 1.25f, new Color(0.98f, 0.82f, 0.28f), count: 10, spread: 0.45f);
            }

            string feedback = "Satın alındı!";
            switch (offer.Kind)
            {
                case ShopOfferKind.Energy:
                    if (PlayerNeedsController.Instance != null)
                    {
                        PlayerNeedsController.Instance.RestoreEnergy(offer.Param);
                    }
                    feedback = $"Afiyet olsun! +{Mathf.RoundToInt(offer.Param)} Enerji";
                    break;

                case ShopOfferKind.RunningShoes:
                    data.hasRunningShoes = true;
                    SaveManager.Instance.Save();
                    feedback = "Koşu Ayakkabısı alındı! Koşma hızın kalıcı olarak +%22 arttı.";
                    break;

                case ShopOfferKind.TurboKit:
                    data.hasTurboKit = true;
                    SaveManager.Instance.Save();
                    feedback = "Turbo Motor Kiti takıldı! Araç tork ve hızı kalıcı olarak +%28 arttı.";
                    break;

                case ShopOfferKind.JacketColor:
                    int colorIdx = Mathf.RoundToInt(offer.Param);
                    data.playerJacketColorIndex = colorIdx;
                    SaveManager.Instance.Save();
                    if (PlayerModeController.Instance != null && PlayerModeController.Instance.FootController != null)
                    {
                        PlayerModeController.Instance.FootController.ApplyJacketColor(colorIdx);
                    }
                    feedback = "Yeni ceketin üzerine giyildi! Harika görünüyor.";
                    break;

                case ShopOfferKind.FuelCan:
                    if (offer.Param > 0f && PlayerNeedsController.Instance != null)
                    {
                        PlayerNeedsController.Instance.RestoreEnergy(offer.Param);
                    }
                    if (PlayerModeController.Instance != null && PlayerModeController.Instance.PlayerSedan != null)
                    {
                        PlayerModeController.Instance.PlayerSedan.SetFuel(1f);
                    }
                    data.missionSedanFuel = 1f;
                    SaveManager.Instance.Save();
                    feedback = "Sedan yakıt deposu %100 dolduruldu!";
                    break;
            }

            RefreshLabels();
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage(feedback);
            }
        }

        public void RefreshLabels()
        {
            string lowerId = (_activeShopId ?? string.Empty).ToLowerInvariant();
            if (titleLabel != null)
            {
                if (lowerId.Contains("vendor_bakery") || lowerId.Contains("simit"))
                    titleLabel.text = "HASAN USTA · Taze Simit & Çay Tezgâhı";
                else if (lowerId.Contains("manav"))
                    titleLabel.text = "SELMAN MANAV REYONU · Taze Meyve & Sebze";
                else if (lowerId.Contains("sarkuteri"))
                    titleLabel.text = "SELMAN ŞARKÜTERİ · Peynir, Zeytin & Sucuk";
                else if (lowerId.Contains("dondurma"))
                    titleLabel.text = "SOĞUK İÇECEK & DONDURMA DOLABI";
                else if (lowerId.Contains("shop_hafsa") || lowerId.Contains("hafsa"))
                    titleLabel.text = "HAFSA TESİSAT & NALBUR · Selman Sk. No:66";
                else if (lowerId.Contains("shop_bakkal") || lowerId.Contains("bakkal"))
                    titleLabel.text = "SELMAN BAKKAL & SÜPERMARKET · No:58";
                else if (lowerId.Contains("coffee") || lowerId.Contains("cay"))
                    titleLabel.text = "95 SELMAN ÇAY OCAĞI · Rıza Abi";
                else if (lowerId.Contains("vendor_gear"))
                    titleLabel.text = "Kerem · Giyim & Spor Tezgâhı";
                else if (lowerId.Contains("vendor_auto"))
                    titleLabel.text = "Tarık · Oto Parça & Modifiye";
                else if (lowerId.Contains("market"))
                    titleLabel.text = "SELMAN BAKKAL · No:58";
                else if (lowerId.Contains("pizza"))
                    titleLabel.text = "Hamidiye Fırın & Pide";
                else
                    titleLabel.text = "Selman Sokağı Esnafı";
            }

            SaveData data = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            int money = data != null ? data.money : 250;
            float energy = PlayerNeedsController.Instance != null ? PlayerNeedsController.Instance.Energy : 80f;

            if (balanceSubLabel != null)
            {
                string subFmt = LocalizationService.Tr("ui.shop.status_line", "Bakiye: ₺ {0}   ·   Enerji: %{1}");
                balanceSubLabel.text = string.Format(subFmt, money, Mathf.RoundToInt(energy));
            }

            ShopOffer[] offers = GetActiveOffers();
            if (itemLabels != null)
            {
                for (int i = 0; i < itemLabels.Length; i++)
                {
                    bool hasOffer = i < offers.Length;
                    if (itemButtons != null && i < itemButtons.Length && itemButtons[i] != null)
                    {
                        itemButtons[i].gameObject.SetActive(hasOffer);
                    }
                    if (!hasOffer) continue;

                    ShopOffer off = offers[i];
                    bool alreadyOwned =
                        (off.Kind == ShopOfferKind.RunningShoes && data != null && data.hasRunningShoes) ||
                        (off.Kind == ShopOfferKind.TurboKit && data != null && data.hasTurboKit);

                    if (itemLabels[i] != null)
                    {
                        itemLabels[i].text = alreadyOwned ? $"{off.LabelTr}  [ALINDI]" : off.LabelTr;
                    }
                    if (itemButtons != null && i < itemButtons.Length && itemButtons[i] != null)
                    {
                        itemButtons[i].interactable = !alreadyOwned && money >= off.Price;
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
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) { TryBuyItem(2); return; }
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) { TryBuyItem(3); return; }
            if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) { TryBuyItem(4); }
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
