using System.Collections;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// A real, physical 3D product sitting on a shop shelf, fridge, manav stand, ice-cream chest,
    /// tea counter, or street vendor stall. Players walk directly up to the product they want and
    /// press E to grab it off the shelf or buy it over the counter — zero modal UI menus!
    /// </summary>
    public class PhysicalShopItem : MonoBehaviour
    {
        private string _itemName = "Ürün";
        private string _itemNameEn = "Item";
        private int _price = 10;
        private ShopItemEffect _effect = ShopItemEffect.Energy;
        private float _effectParam = 20f;
        private ConsumeAnimKind _consumeKind = ConsumeAnimKind.DrinkWater;
        private ShopPropVisual _visual = ShopPropVisual.WaterBottle;
        private int _jacketColorIndex;
        private bool _requiresCheckout;
        private bool _lastTagWasEn;

        private static readonly System.Collections.Generic.Dictionary<string, string> EnItemNames =
            new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal)
            {
                { "Soğuk Su", "Cold Water" },
                { "Yayık Ayran", "Churned Ayran" },
                { "Enerji İçeceği", "Energy Drink" },
                { "Külah Dondurma", "Ice Cream Cone" },
                { "Soğuk Gazoz", "Chilled Soda" },
                { "Taze Portakal", "Fresh Orange" },
                { "Aydos Elması", "Aydos Apple" },
                { "Vitamin Sepeti", "Vitamin Basket" },
                { "Ezine Peynir & Zeytin", "Ezine Cheese & Olives" },
                { "Sucuklu Tost", "Sucuk Toast" },
                { "Taze Somun Ekmek", "Fresh Bread Loaf" },
                { "Kaşarlı Tost", "Cheese Toast" },
                { "Sıcak Simit", "Warm Simit" },
                { "Kahvaltı Paketi", "Breakfast Pack" },
                { "İnce Belli Çay", "Turkish Tea" },
                { "Türk Kahvesi", "Turkish Coffee" },
                { "Acil Yakıt Bidonu", "Emergency Fuel Can" },
                { "Turbo Motor Kiti", "Turbo Engine Kit" },
                { "Usta İkramı Çay", "Master's Tea" },
                { "Yürüyüş Ayakkabısı", "Walking Shoes" },
                { "Koşu Ayakkabısı", "Running Shoes" },
                { "Zeytin İş Parkası", "Olive Work Parka" },
                { "Bordo Deri Ceket", "Burgundy Leather Jacket" },
                { "Zeytin Parka", "Olive Parka" },
                { "Antrasit Mont", "Charcoal Jacket" },
                { "Lacivert Ceket", "Navy Jacket" },
                { "Tamirci Kahvesi", "Mechanic's Coffee" },
                { "Sıcak Yemek", "Hot Meal" }
            };

        private static bool IsEn =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        private string LocalizedName => IsEn ? _itemNameEn : _itemName;

        private GameObject _visualRoot;
        private GameObject _priceTagObj;
        private TextMesh _priceTagText;
        private InteractableTrigger _trigger;
        private Vector3 _baseVisualScale = Vector3.one;
        private bool _isTemporarilyTaken;
        private Camera _cachedCam;

        public void Configure(
            string itemName,
            int price,
            ShopItemEffect effect,
            float effectParam,
            ConsumeAnimKind consumeKind,
            ShopPropVisual visual,
            bool requiresCheckout,
            int jacketColorIndex = 0,
            float interactionRadius = 2.05f,
            float visualScale = 1.05f)
        {
            _itemName = itemName;
            _itemNameEn = EnItemNames.TryGetValue(itemName ?? string.Empty, out string mappedEn) ? mappedEn : itemName;
            _price = price;
            _effect = effect;
            _effectParam = effectParam;
            _consumeKind = consumeKind;
            _visual = visual;
            _requiresCheckout = requiresCheckout;
            _jacketColorIndex = jacketColorIndex;

            _visualRoot = ShopPropFactory.BuildProp(transform, visual, Vector3.zero, visualScale, jacketColorIndex);
            _baseVisualScale = _visualRoot != null ? _visualRoot.transform.localScale : Vector3.one;

            BuildWorldPriceTag();

            _trigger = gameObject.GetComponent<InteractableTrigger>();
            if (_trigger == null)
            {
                _trigger = gameObject.AddComponent<InteractableTrigger>();
            }
            string verbTr = requiresCheckout ? "Eline Al" : "Satın Al";
            string verbEn = requiresCheckout ? "Pick Up" : "Buy";
            _trigger.Configure(InteractableKind.ShopItem, $"shopitem_{itemName}".ToLowerInvariant(), "ui.btn.shop", interactionRadius);
            _trigger.SetCustomPrompt($"{verbTr}: {_itemName} · {price} ₺", $"{verbEn}: {_itemNameEn} · {price} ₺");
        }

        private void BuildWorldPriceTag()
        {
            _priceTagObj = new GameObject("PriceTag");
            _priceTagObj.transform.SetParent(transform, false);
            _priceTagObj.transform.localPosition = new Vector3(0f, 0.36f, 0f);

            _lastTagWasEn = IsEn;
            _priceTagText = _priceTagObj.AddComponent<TextMesh>();
            _priceTagText.text = $"{LocalizedName}\n{_price} ₺";
            _priceTagText.fontSize = 44;
            _priceTagText.characterSize = 0.0115f;
            _priceTagText.anchor = TextAnchor.LowerCenter;
            _priceTagText.alignment = TextAlignment.Center;
            _priceTagText.fontStyle = FontStyle.Bold;
            _priceTagText.color = new Color(1.0f, 0.96f, 0.82f);

            MeshRenderer mr = _priceTagObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            _priceTagObj.SetActive(false);
        }

        private void Update()
        {
            if (_isTemporarilyTaken) return;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.CurrentMode != ControlMode.OnFoot)
            {
                if (_priceTagObj != null && _priceTagObj.activeSelf) _priceTagObj.SetActive(false);
                return;
            }

            float distSq = (mode.ActiveWorldPosition - transform.position).sqrMagnitude;
            bool showTag = distSq <= 5.2f * 5.2f;
            if (_priceTagObj != null)
            {
                if (_priceTagObj.activeSelf != showTag)
                {
                    _priceTagObj.SetActive(showTag);
                }

                if (showTag)
                {
                    if (_cachedCam == null) _cachedCam = Camera.main;
                    if (_cachedCam != null)
                    {
                        Vector3 look = _priceTagObj.transform.position - _cachedCam.transform.position;
                        look.y = 0f;
                        if (look.sqrMagnitude > 0.001f)
                        {
                            _priceTagObj.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                        }
                    }
                }
            }

            bool isTargeted = _trigger != null && mode.NearestInteractable == _trigger;
            if (_visualRoot != null)
            {
                float targetMul = isTargeted ? (1.10f + Mathf.Sin(Time.time * 6f) * 0.04f) : 1.0f;
                _visualRoot.transform.localScale = Vector3.Lerp(
                    _visualRoot.transform.localScale,
                    _baseVisualScale * targetMul,
                    1f - Mathf.Exp(-12f * Time.deltaTime));
            }
            if (_priceTagText != null && showTag)
            {
                bool isEnNow = IsEn;
                if (isEnNow != _lastTagWasEn)
                {
                    _lastTagWasEn = isEnNow;
                    _priceTagText.text = $"{LocalizedName}\n{_price} ₺";
                }
                _priceTagText.color = isTargeted
                    ? new Color(1.0f, 0.88f, 0.24f)
                    : new Color(0.95f, 0.96f, 0.98f);
            }
        }

        public void Use(PlayerModeController mode)
        {
            if (mode == null || _isTemporarilyTaken || PlayerActionAnimator.IsBusyOn(mode)) return;
            if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return;

            SaveData data = SaveManager.Instance.Data;
            bool isEn = IsEn;
            string dispName = LocalizedName;

            if (_effect == ShopItemEffect.RunningShoes && data.hasRunningShoes)
            {
                mode.RaiseStatusMessage(isEn ? "You already own Running Shoes!" : "Koşu ayakkabısına zaten sahipsin!");
                return;
            }
            if (_effect == ShopItemEffect.TurboKit && data.hasTurboKit)
            {
                mode.RaiseStatusMessage(isEn ? "Turbo Engine Kit is already installed on your car!" : "Turbo Motor Kiti aracında zaten takılı!");
                return;
            }

            // FLOW 1: Supermarket shelf/fridge/manav — physically pick the item off the shelf and carry it to Kasa!
            if (_requiresCheckout)
            {
                PlayerShoppingCart cart = PlayerShoppingCart.GetOrCreate(mode);
                if (cart == null) return;

                ShopCartItem entry = new ShopCartItem
                {
                    Name = dispName,
                    Price = _price,
                    Effect = _effect,
                    EffectParam = _effectParam,
                    ConsumeKind = _consumeKind,
                    Visual = _visual,
                    JacketColorIndex = _jacketColorIndex
                };

                PlayerActionAnimator.Play(mode, transform, PlayerActionKind.InspectReach, onClimax: () =>
                {
                    if (cart.TryAddItem(mode, entry, transform.position))
                    {
                        GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.12f, new Color(0.45f, 0.88f, 0.98f), count: 7, spread: 0.22f);
                        StartCoroutine(RestockRoutine(10f));
                    }
                }, startStatus: isEn ? $"Picking up {dispName}..." : $"Raftan {dispName} alınıyor...");
                return;
            }

            // FLOW 2: Direct over-the-counter / street stall purchase — reach, pay & use immediately!
            if (data.money < _price)
            {
                mode.RaiseStatusMessage(isEn
                    ? $"Not enough cash! ({dispName}: {_price} ₺ · Balance: {data.money} ₺)"
                    : $"Yetersiz bakiye! ({dispName}: {_price} ₺ · Bakiye: {data.money} ₺)");
                return;
            }

            if (_effect == ShopItemEffect.Energy && _consumeKind != ConsumeAnimKind.None &&
                mode.FootController != null && mode.CurrentMode == ControlMode.OnFoot)
            {
                if (!SaveManager.Instance.TrySpend(_price))
                {
                    mode.RaiseStatusMessage(isEn ? "Not enough cash!" : "Yetersiz bakiye!");
                    return;
                }
                GameFeelAudio.PlayCoinChime();
                GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.2f, new Color(0.98f, 0.84f, 0.28f), count: 10, spread: 0.35f);
                mode.RaiseStatusMessage(isEn ? $"Bought {dispName} (-{_price} ₺) · Enjoy!" : $"{dispName} alındı (-{_price} ₺) · Afiyet olsun!");
                StartCoroutine(RestockRoutine(8f));

                PlayerConsumeAnimator.PlayPurchase(
                    mode.FootController,
                    "direct_counter",
                    _consumeKind,
                    dispName,
                    _effectParam,
                    explicitTarget: transform);
                return;
            }

            // Non-food physical purchases (Jacket, Running Shoes, Turbo Kit, Fuel Can)
            PlayerActionKind animKind = _effect == ShopItemEffect.JacketColor
                ? PlayerActionKind.ChangeOutfit
                : (_effect == ShopItemEffect.RunningShoes ? PlayerActionKind.LoadWashingMachine : PlayerActionKind.InspectReach);

            PlayerActionAnimator.Play(mode, transform, animKind, onClimax: () =>
            {
                if (SaveManager.Instance == null || !SaveManager.Instance.TrySpend(_price))
                {
                    mode.RaiseStatusMessage(isEn ? "Not enough cash!" : "Yetersiz bakiye!");
                    return;
                }

                ShopCartItem entry = new ShopCartItem
                {
                    Name = dispName,
                    Price = _price,
                    Effect = _effect,
                    EffectParam = _effectParam,
                    ConsumeKind = _consumeKind,
                    Visual = _visual,
                    JacketColorIndex = _jacketColorIndex
                };
                PlayerShoppingCart.ApplyNonEnergyEffect(mode, entry);

                GameFeelAudio.PlayCoinChime();
                GameFeelAudio.SpawnBurst(mode.ActiveWorldPosition + Vector3.up * 1.15f, new Color(0.98f, 0.84f, 0.28f), count: 12, spread: 0.45f);
                StartCoroutine(RestockRoutine(8f));

                switch (_effect)
                {
                    case ShopItemEffect.RunningShoes:
                        mode.RaiseStatusMessage(isEn
                            ? $"Equipped Running Shoes (-{_price} ₺) · Sprint speed permanently +22%!"
                            : $"Koşu Ayakkabısı giyildi (-{_price} ₺) · Koşma hızın kalıcı olarak +%22 arttı!");
                        break;
                    case ShopItemEffect.TurboKit:
                        mode.RaiseStatusMessage(isEn
                            ? $"Installed Turbo Engine Kit (-{_price} ₺) · Car power permanently +28%!"
                            : $"Turbo Motor Kiti alındı (-{_price} ₺) · Araç gücü kalıcı olarak +%28 arttı!");
                        break;
                    case ShopItemEffect.JacketColor:
                        mode.RaiseStatusMessage(isEn
                            ? $"Put on {dispName} (-{_price} ₺) · Looking sharp!"
                            : $"{dispName} üzerine giyildi (-{_price} ₺) · Harika görünüyor!");
                        break;
                    case ShopItemEffect.FuelCan:
                        mode.RaiseStatusMessage(isEn
                            ? $"Bought Emergency Fuel Can (-{_price} ₺) · Fuel tank refilled to 100%!"
                            : $"Acil Yakıt Bidonu alındı (-{_price} ₺) · Aracının deposu %100 doldu!");
                        break;
                    default:
                        if (_effectParam > 0f && PlayerNeedsController.Instance != null)
                        {
                            PlayerNeedsController.Instance.RestoreEnergy(_effectParam);
                        }
                        mode.RaiseStatusMessage(isEn ? $"Bought {dispName} (-{_price} ₺)!" : $"{dispName} alındı (-{_price} ₺)!");
                        break;
                }
            }, startStatus: isEn ? $"Buying {dispName} ({_price} ₺)..." : $"{dispName} alınıyor ({_price} ₺)...");
        }

        private IEnumerator RestockRoutine(float delaySeconds)
        {
            _isTemporarilyTaken = true;
            if (_visualRoot != null) _visualRoot.SetActive(false);
            if (_priceTagObj != null) _priceTagObj.SetActive(false);
            if (_trigger != null) _trigger.enabled = false;

            yield return new WaitForSeconds(delaySeconds);

            _isTemporarilyTaken = false;
            if (_visualRoot != null) _visualRoot.SetActive(true);
            if (_trigger != null) _trigger.enabled = true;
        }
    }
}
