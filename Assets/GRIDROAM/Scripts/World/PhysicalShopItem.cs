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
        private int _reputationXp;
        private bool _isTryOnWearable;
        private string _storeName;
        private bool _lastTagWasEn;
        private bool _lastWearingTrial;

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
                { "Meyve Çantası", "Fruit Bag" },
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
            float visualScale = 1.05f,
            string itemNameEn = null,
            int reputationXp = 0,
            bool isTryOnWearable = false,
            string storeName = null)
        {
            _itemName = itemName;
            _itemNameEn = !string.IsNullOrEmpty(itemNameEn)
                ? itemNameEn
                : (EnItemNames.TryGetValue(itemName ?? string.Empty, out string mappedEn) ? mappedEn : itemName);
            _price = price;
            _effect = effect;
            _effectParam = effectParam;
            _consumeKind = consumeKind;
            _visual = visual;
            _requiresCheckout = requiresCheckout;
            _jacketColorIndex = jacketColorIndex;
            _reputationXp = reputationXp;
            _isTryOnWearable = isTryOnWearable;
            _storeName = storeName ?? string.Empty;

            _visualRoot = ShopPropFactory.BuildProp(transform, visual, Vector3.zero, visualScale, jacketColorIndex);
            _baseVisualScale = _visualRoot != null ? _visualRoot.transform.localScale : Vector3.one;

            BuildWorldPriceTag();

            _trigger = gameObject.GetComponent<InteractableTrigger>();
            if (_trigger == null)
            {
                _trigger = gameObject.AddComponent<InteractableTrigger>();
            }
            _trigger.Configure(InteractableKind.ShopItem, $"shopitem_{itemName}".ToLowerInvariant(), "ui.btn.shop", interactionRadius);
            RefreshPromptAndTag(force: true);
        }

        private void RefreshPromptAndTag(bool force = false)
        {
            bool isEnNow = IsEn;
            bool wearingTrial = _isTryOnWearable && _requiresCheckout &&
                                PlayerShoppingCart.IsWearingUnpaidTrial(_effect, _jacketColorIndex);

            if (!force && isEnNow == _lastTagWasEn && wearingTrial == _lastWearingTrial) return;
            _lastTagWasEn = isEnNow;
            _lastWearingTrial = wearingTrial;

            if (_trigger != null)
            {
                if (wearingTrial)
                {
                    _trigger.SetCustomPrompt(
                        $"Çıkar & Stanta Bırak: {_itemName}",
                        $"Take Off & Return: {_itemNameEn}");
                }
                else if (_isTryOnWearable && _requiresCheckout)
                {
                    _trigger.SetCustomPrompt(
                        $"3D Dene: {_itemName} · {_price} ₺",
                        $"Try On (3D): {_itemNameEn} · {_price} ₺");
                }
                else
                {
                    string verbTr = _requiresCheckout ? "Eline Al" : "Satın Al";
                    string verbEn = _requiresCheckout ? "Pick Up" : "Buy";
                    _trigger.SetCustomPrompt(
                        $"{verbTr}: {_itemName} · {_price} ₺",
                        $"{verbEn}: {_itemNameEn} · {_price} ₺");
                }
            }

            if (_priceTagText != null)
            {
                if (wearingTrial)
                {
                    _priceTagText.text = isEnNow
                        ? $"{_itemNameEn}\n✓ Wearing · Pay at Register"
                        : $"{_itemName}\n✓ Üzerinde · Kasada Öde";
                }
                else if (_isTryOnWearable && _requiresCheckout)
                {
                    _priceTagText.text = isEnNow
                        ? $"{_itemNameEn}\nTRY ON · {_price} ₺"
                        : $"{_itemName}\nDENE · {_price} ₺";
                }
                else
                {
                    _priceTagText.text = $"{LocalizedName}\n{_price} ₺";
                }
            }

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode != null && _trigger != null && mode.NearestInteractable == _trigger)
            {
                mode.RefreshCurrentInteractPrompt();
            }
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
            if (showTag || isTargeted)
            {
                RefreshPromptAndTag();
            }
            if (_priceTagText != null && showTag)
            {
                _priceTagText.color = _lastWearingTrial
                    ? new Color(0.32f, 0.96f, 0.62f)
                    : (isTargeted ? new Color(1.0f, 0.88f, 0.24f) : new Color(0.95f, 0.96f, 0.98f));
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
            if (_effect == ShopItemEffect.Sunglasses && data.ownsSunglasses)
            {
                mode.RaiseStatusMessage(isEn
                    ? "You already own Polarized Sunglasses! Toggle them anytime at Barber Tuncay."
                    : "Polarize Güneş Gözlüğüne zaten sahipsin! Berber Tuncay'dan takıp çıkarabilirsin.");
                return;
            }
            if (_effect == ShopItemEffect.StreetCap && data.ownsStreetCap)
            {
                mode.RaiseStatusMessage(isEn
                    ? "You already own the 3D Street Cap! Toggle it anytime at Barber Tuncay."
                    : "3D Sokak Şapkasına zaten sahipsin! Berber Tuncay'dan takıp çıkarabilirsin.");
                return;
            }
            if (_effect == ShopItemEffect.JacketColor &&
                data.playerJacketColorIndex == _jacketColorIndex &&
                !PlayerShoppingCart.IsWearingUnpaidTrial(ShopItemEffect.JacketColor, _jacketColorIndex))
            {
                mode.RaiseStatusMessage(isEn
                    ? $"You are already wearing {dispName}!"
                    : $"{dispName} zaten senin kayıtlı ceketin!");
                return;
            }

            // FLOW 1: Retail store stand / supermarket shelf — physically pick up or Try On in 3D, then pay at Checkout!
            if (_requiresCheckout)
            {
                PlayerShoppingCart cart = PlayerShoppingCart.GetOrCreate(mode);
                if (cart == null) return;

                if (_isTryOnWearable && PlayerShoppingCart.IsWearingUnpaidTrial(_effect, _jacketColorIndex))
                {
                    PlayerActionAnimator.Play(mode, transform, PlayerActionKind.ChangeOutfit, onClimax: () =>
                    {
                        PlayerShoppingCart.TryTakeOffTrialItem(mode, _effect, _jacketColorIndex);
                        RefreshPromptAndTag(force: true);
                    }, startStatus: isEn ? $"Taking off {dispName}..." : $"{dispName} çıkarılıyor...");
                    return;
                }

                ShopCartItem entry = new ShopCartItem
                {
                    Name = dispName,
                    NameTr = _itemName,
                    NameEn = _itemNameEn,
                    Price = _price,
                    Effect = _effect,
                    EffectParam = _effectParam,
                    ConsumeKind = _consumeKind,
                    Visual = _visual,
                    JacketColorIndex = _jacketColorIndex,
                    ReputationXp = _reputationXp,
                    IsTryOnWearable = _isTryOnWearable,
                    SourceStoreName = _storeName
                };

                PlayerActionKind reachAnim = _isTryOnWearable ? PlayerActionKind.ChangeOutfit : PlayerActionKind.InspectReach;
                string statusStr = _isTryOnWearable
                    ? (isEn ? $"Trying on {dispName}..." : $"{dispName} üzerinde deneniyor...")
                    : (isEn ? $"Picking up {dispName}..." : $"Raftan {dispName} alınıyor...");

                PlayerActionAnimator.Play(mode, transform, reachAnim, onClimax: () =>
                {
                    if (cart.TryAddItem(mode, entry, transform.position))
                    {
                        Color fxCol = _isTryOnWearable ? new Color(0.32f, 0.95f, 0.68f) : new Color(0.45f, 0.88f, 0.98f);
                        GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.14f, fxCol, count: 8, spread: 0.24f);
                        RefreshPromptAndTag(force: true);
                        // Keep Try-On display samples visible on the stand so you can compare colors or return them!
                        if (!_isTryOnWearable)
                        {
                            StartCoroutine(RestockRoutine(10f));
                        }
                    }
                }, startStatus: statusStr);
                return;
            }

            // FLOW 2: Direct over-the-counter / cafe / food court / street stall purchase — reach, pay & use immediately!
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
                if (_reputationXp > 0)
                {
                    NeighborhoodReputation.AddReputation(_reputationXp);
                }
                if (!string.IsNullOrEmpty(_storeName))
                {
                    DailyQuestAndAchievementSystem.RecordMallPurchase();
                }
                GameFeelAudio.PlayCoinChime();
                GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.2f, new Color(0.98f, 0.84f, 0.28f), count: 10, spread: 0.35f);
                string xpNote = _reputationXp > 0 ? $" · +{_reputationXp} XP" : string.Empty;
                StartCoroutine(RestockRoutine(8f));

                ShopCartItem directPackCheck = new ShopCartItem
                {
                    Name = dispName,
                    NameTr = _itemName,
                    NameEn = _itemNameEn,
                    Price = _price,
                    Effect = _effect,
                    EffectParam = _effectParam,
                    ConsumeKind = _consumeKind,
                    Visual = _visual,
                    SourceStoreName = _storeName
                };
                if (PlayerBackpackInventory.ShouldPackIntoBackpack(directPackCheck, isCheckoutStore: false))
                {
                    int servings = PlayerBackpackInventory.EnsureInstance().AddFromShopCartItem(directPackCheck);
                    mode.RaiseStatusMessage(isEn
                        ? $"You're full — packed {dispName} (×{servings}) into your Backpack! (-{_price} ₺{xpNote})"
                        : $"Tok olduğun için {dispName} (×{servings}) Çantana konuldu! (-{_price} ₺{xpNote})");
                    return;
                }

                mode.RaiseStatusMessage(isEn ? $"Bought {dispName} (-{_price} ₺{xpNote}) · Enjoy!" : $"{dispName} alındı (-{_price} ₺{xpNote}) · Afiyet olsun!");
                PlayerConsumeAnimator.PlayPurchase(
                    mode.FootController,
                    "direct_counter",
                    _consumeKind,
                    dispName,
                    _effectParam,
                    explicitTarget: transform);
                return;
            }

            // Non-food physical purchases (Jacket, Running Shoes, Turbo Kit, Fuel Can, Accessories)
            PlayerActionKind animKind = (_effect == ShopItemEffect.JacketColor || _effect == ShopItemEffect.Sunglasses || _effect == ShopItemEffect.StreetCap)
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
                    JacketColorIndex = _jacketColorIndex,
                    ReputationXp = _reputationXp,
                    SourceStoreName = _storeName
                };
                PlayerShoppingCart.ApplyNonEnergyEffect(mode, entry);
                if (_reputationXp > 0)
                {
                    NeighborhoodReputation.AddReputation(_reputationXp);
                }
                if (!string.IsNullOrEmpty(_storeName))
                {
                    DailyQuestAndAchievementSystem.RecordMallPurchase();
                }

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
                        if (_effect != ShopItemEffect.ReputationPrestige && _effectParam > 0f && PlayerNeedsController.Instance != null)
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
