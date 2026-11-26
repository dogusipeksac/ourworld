using System;
using System.Collections.Generic;
using OurWorld.Core;
using UnityEngine;

namespace OurWorld.Player
{
    public struct BackpackDisplayItem
    {
        public string ItemId;
        public string NameTr;
        public string NameEn;
        public int Count;
        public float HungerRestore;
        public float EnergyRestore;
        public float HealthRestore;
        public ConsumeAnimKind ConsumeKind;
        public ShopPropVisual PropVisual;
        public bool IsFromFarm;
    }

    /// <summary>
    /// Persistent player Backpack / Inventory ("Çanta") holding bought fruit bags, groceries,
    /// snacks, drinks, vitamins, and fresh harvested farm produce so the player can eat or drink
    /// anytime they get hungry or low on energy/health.
    /// </summary>
    public sealed class PlayerBackpackInventory : MonoBehaviour
    {
        public static PlayerBackpackInventory Instance { get; private set; }

        public const int MaxUniqueSlots = 24;
        public const int MaxStackCount = 99;

        private const string FarmStrawberriesId = "farm_strawberries";
        private const string FarmTomatoesId = "farm_tomatoes";
        private const string FarmMilkId = "farm_milk";

        public event Action OnInventoryChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            EnsureInstance();
        }

        public static PlayerBackpackInventory EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_PlayerBackpackInventory");
            return go.AddComponent<PlayerBackpackInventory>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            }
            EnsureStarterItemsIfFirstRun();
            OnInventoryChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            }
            if (Instance == this) Instance = null;
        }

        private void HandleSaveLoaded(SaveData _)
        {
            EnsureStarterItemsIfFirstRun();
            OnInventoryChanged?.Invoke();
        }

        private void EnsureStarterItemsIfFirstRun()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return;
            SaveData data = SaveManager.Instance.Data;
            if (data.backpackItems == null)
            {
                data.backpackItems = new List<BackpackItemSaveEntry>();
            }
            if (data.hasStarterBackpackItems) return;

            data.hasStarterBackpackItems = true;
            AddItemInternal(
                "item_fruit_bag",
                "Meyve Çantası",
                "Fruit Bag",
                count: 3,
                hungerRestore: 36f,
                energyRestore: 28f,
                healthRestore: 14f,
                ConsumeAnimKind.EatFood,
                ShopPropVisual.FreshFruit);
            AddItemInternal(
                "item_cold_water",
                "Soğuk Su",
                "Cold Water",
                count: 2,
                hungerRestore: 12f,
                energyRestore: 20f,
                healthRestore: 8f,
                ConsumeAnimKind.DrinkWater,
                ShopPropVisual.WaterBottle);
            SaveManager.Instance.Save();
        }

        public int TotalItemCount
        {
            get
            {
                if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return 0;
                SaveData data = SaveManager.Instance.Data;
                int total = 0;
                if (data.backpackItems != null)
                {
                    for (int i = 0; i < data.backpackItems.Count; i++)
                    {
                        if (data.backpackItems[i] != null && data.backpackItems[i].count > 0)
                        {
                            total += data.backpackItems[i].count;
                        }
                    }
                }
                if (data.invStrawberries > 0) total += data.invStrawberries;
                if (data.invTomatoes > 0) total += data.invTomatoes;
                if (data.invMilk > 0) total += data.invMilk;
                return total;
            }
        }

        public List<BackpackDisplayItem> GetAllEntriesSnapshot()
        {
            var list = new List<BackpackDisplayItem>(12);
            if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return list;

            SaveData data = SaveManager.Instance.Data;
            if (data.backpackItems != null)
            {
                for (int i = 0; i < data.backpackItems.Count; i++)
                {
                    BackpackItemSaveEntry e = data.backpackItems[i];
                    if (e == null || e.count <= 0) continue;
                    list.Add(new BackpackDisplayItem
                    {
                        ItemId = e.itemId,
                        NameTr = string.IsNullOrEmpty(e.nameTr) ? e.nameEn : e.nameTr,
                        NameEn = string.IsNullOrEmpty(e.nameEn) ? e.nameTr : e.nameEn,
                        Count = e.count,
                        HungerRestore = e.hungerRestore,
                        EnergyRestore = e.energyRestore,
                        HealthRestore = e.healthRestore,
                        ConsumeKind = (ConsumeAnimKind)e.consumeKind,
                        PropVisual = (ShopPropVisual)e.propVisual,
                        IsFromFarm = false
                    });
                }
            }

            // Fresh home garden & Aydos farm edibles are also directly snackable from the Backpack!
            if (data.invStrawberries > 0)
            {
                list.Add(new BackpackDisplayItem
                {
                    ItemId = FarmStrawberriesId,
                    NameTr = "Bahçe Çileği",
                    NameEn = "Garden Strawberries",
                    Count = data.invStrawberries,
                    HungerRestore = 28f,
                    EnergyRestore = 24f,
                    HealthRestore = 14f,
                    ConsumeKind = ConsumeAnimKind.EatFood,
                    PropVisual = ShopPropVisual.FreshFruit,
                    IsFromFarm = true
                });
            }
            if (data.invTomatoes > 0)
            {
                list.Add(new BackpackDisplayItem
                {
                    ItemId = FarmTomatoesId,
                    NameTr = "Taze Bostan Domatesi",
                    NameEn = "Fresh Garden Tomato",
                    Count = data.invTomatoes,
                    HungerRestore = 24f,
                    EnergyRestore = 18f,
                    HealthRestore = 12f,
                    ConsumeKind = ConsumeAnimKind.EatFood,
                    PropVisual = ShopPropVisual.FreshFruit,
                    IsFromFarm = true
                });
            }
            if (data.invMilk > 0)
            {
                list.Add(new BackpackDisplayItem
                {
                    ItemId = FarmMilkId,
                    NameTr = "Taze Çiftlik Sütü",
                    NameEn = "Fresh Farm Milk",
                    Count = data.invMilk,
                    HungerRestore = 22f,
                    EnergyRestore = 30f,
                    HealthRestore = 16f,
                    ConsumeKind = ConsumeAnimKind.DrinkWater,
                    PropVisual = ShopPropVisual.AyranCup,
                    IsFromFarm = true
                });
            }

            return list;
        }

        public static bool ShouldPackIntoBackpack(ShopCartItem item, bool isCheckoutStore)
        {
            if (item.Effect != ShopItemEffect.Energy || item.ConsumeKind == ConsumeAnimKind.None)
            {
                return false;
            }
            // Melting ice cream & hot cafe cups are always enjoyed right away on the spot!
            if (item.ConsumeKind == ConsumeAnimKind.EatIceCream || item.ConsumeKind == ConsumeAnimKind.DrinkHotCup)
            {
                return false;
            }
            if (isCheckoutStore)
            {
                // All retail market / bakkal / pharmacy packaged groceries, fruit bags, drinks & vitamins go into your Backpack!
                return true;
            }
            // At direct stalls/counters, pack portable snacks/drinks into Backpack if player is already full!
            PlayerNeedsController needs = PlayerNeedsController.Instance;
            return needs != null && needs.Hunger >= 90f && needs.Energy >= 90f;
        }

        public int AddFromShopCartItem(ShopCartItem item)
        {
            string tr = string.IsNullOrEmpty(item.NameTr) ? item.Name : item.NameTr;
            string en = string.IsNullOrEmpty(item.NameEn) ? item.Name : item.NameEn;
            string low = (tr + " " + en).ToLowerInvariant();

            int servings = 1;
            if (low.Contains("meyve çantası") || low.Contains("fruit bag"))
            {
                servings = 3;
            }
            else if (low.Contains("sepet") || low.Contains("basket") || low.Contains("kumanya") ||
                     low.Contains("picnic") || low.Contains("kahvaltı") || low.Contains("breakfast"))
            {
                servings = 2;
            }

            bool isDrink = item.ConsumeKind == ConsumeAnimKind.DrinkWater || item.ConsumeKind == ConsumeAnimKind.DrinkHotCup;
            bool isMedicine = item.Visual == ShopPropVisual.MedicineBox;

            float baseEnergy = Mathf.Clamp(item.EffectParam > 0f ? item.EffectParam : 22f, 12f, 100f);
            float perEnergy = servings > 1 ? Mathf.Clamp(Mathf.Round(baseEnergy * 0.72f), 18f, 45f) : baseEnergy;
            float perHunger;
            float perHealth;

            if (isMedicine)
            {
                perHunger = 10f;
                perHealth = Mathf.Clamp(Mathf.Round(baseEnergy * 0.65f), 25f, 60f);
            }
            else if (isDrink)
            {
                perHunger = Mathf.Clamp(Mathf.Round(perEnergy * 0.55f), 10f, 28f);
                perHealth = Mathf.Clamp(Mathf.Round(perEnergy * 0.42f), 8f, 24f);
            }
            else
            {
                // Fruits, simit, bread, toast, cheese/olives, picnic food fill Hunger strongly!
                perHunger = Mathf.Clamp(Mathf.Round(perEnergy * 1.15f), 22f, 65f);
                perHealth = Mathf.Clamp(Mathf.Round(perEnergy * 0.48f), 10f, 32f);
            }

            string id = MakeSlugId(en, item.Visual);
            AddItem(id, tr, en, servings, perHunger, perEnergy, perHealth, item.ConsumeKind, item.Visual);
            return servings;
        }

        public bool AddItem(
            string itemId,
            string nameTr,
            string nameEn,
            int count,
            float hungerRestore,
            float energyRestore,
            float healthRestore,
            ConsumeAnimKind consumeKind,
            ShopPropVisual propVisual)
        {
            if (count <= 0 || SaveManager.Instance == null || SaveManager.Instance.Data == null) return false;
            bool ok = AddItemInternal(itemId, nameTr, nameEn, count, hungerRestore, energyRestore, healthRestore, consumeKind, propVisual);
            if (ok)
            {
                SaveManager.Instance.Save();
                OnInventoryChanged?.Invoke();
            }
            return ok;
        }

        private bool AddItemInternal(
            string itemId,
            string nameTr,
            string nameEn,
            int count,
            float hungerRestore,
            float energyRestore,
            float healthRestore,
            ConsumeAnimKind consumeKind,
            ShopPropVisual propVisual)
        {
            SaveData data = SaveManager.Instance.Data;
            if (data.backpackItems == null) data.backpackItems = new List<BackpackItemSaveEntry>();

            for (int i = 0; i < data.backpackItems.Count; i++)
            {
                BackpackItemSaveEntry e = data.backpackItems[i];
                if (e != null && string.Equals(e.itemId, itemId, StringComparison.Ordinal))
                {
                    e.count = Mathf.Min(MaxStackCount, e.count + count);
                    return true;
                }
            }

            if (data.backpackItems.Count >= MaxUniqueSlots)
            {
                return false;
            }

            data.backpackItems.Add(new BackpackItemSaveEntry
            {
                itemId = itemId,
                nameTr = nameTr,
                nameEn = nameEn,
                count = Mathf.Min(MaxStackCount, count),
                hungerRestore = hungerRestore,
                energyRestore = energyRestore,
                healthRestore = healthRestore,
                consumeKind = (int)consumeKind,
                propVisual = (int)propVisual
            });
            return true;
        }

        public bool TryConsumeItem(string itemId, PlayerModeController mode)
        {
            if (string.IsNullOrEmpty(itemId) || SaveManager.Instance == null || SaveManager.Instance.Data == null) return false;
            PlayerNeedsController needs = PlayerNeedsController.Instance;
            if (needs != null && needs.IsKnockedOut) return false;

            SaveData data = SaveManager.Instance.Data;
            bool en = LocalizationService.IsEnglish;

            BackpackDisplayItem chosen = default;
            bool found = false;

            if (itemId == FarmStrawberriesId && data.invStrawberries > 0)
            {
                data.invStrawberries--;
                chosen = new BackpackDisplayItem
                {
                    ItemId = itemId, NameTr = "Bahçe Çileği", NameEn = "Garden Strawberries",
                    HungerRestore = 28f, EnergyRestore = 24f, HealthRestore = 14f,
                    ConsumeKind = ConsumeAnimKind.EatFood, PropVisual = ShopPropVisual.FreshFruit
                };
                found = true;
            }
            else if (itemId == FarmTomatoesId && data.invTomatoes > 0)
            {
                data.invTomatoes--;
                chosen = new BackpackDisplayItem
                {
                    ItemId = itemId, NameTr = "Taze Bostan Domatesi", NameEn = "Fresh Garden Tomato",
                    HungerRestore = 24f, EnergyRestore = 18f, HealthRestore = 12f,
                    ConsumeKind = ConsumeAnimKind.EatFood, PropVisual = ShopPropVisual.FreshFruit
                };
                found = true;
            }
            else if (itemId == FarmMilkId && data.invMilk > 0)
            {
                data.invMilk--;
                chosen = new BackpackDisplayItem
                {
                    ItemId = itemId, NameTr = "Taze Çiftlik Sütü", NameEn = "Fresh Farm Milk",
                    HungerRestore = 22f, EnergyRestore = 30f, HealthRestore = 16f,
                    ConsumeKind = ConsumeAnimKind.DrinkWater, PropVisual = ShopPropVisual.AyranCup
                };
                found = true;
            }
            else if (data.backpackItems != null)
            {
                for (int i = 0; i < data.backpackItems.Count; i++)
                {
                    BackpackItemSaveEntry e = data.backpackItems[i];
                    if (e == null || e.count <= 0 || !string.Equals(e.itemId, itemId, StringComparison.Ordinal)) continue;
                    chosen = new BackpackDisplayItem
                    {
                        ItemId = e.itemId,
                        NameTr = e.nameTr,
                        NameEn = e.nameEn,
                        HungerRestore = e.hungerRestore,
                        EnergyRestore = e.energyRestore,
                        HealthRestore = e.healthRestore,
                        ConsumeKind = (ConsumeAnimKind)e.consumeKind,
                        PropVisual = (ShopPropVisual)e.propVisual
                    };
                    e.count--;
                    if (e.count <= 0)
                    {
                        data.backpackItems.RemoveAt(i);
                    }
                    found = true;
                    break;
                }
            }

            if (!found) return false;

            SaveManager.Instance.Save();
            OnInventoryChanged?.Invoke();

            string dispName = en ? chosen.NameEn : chosen.NameTr;
            if (mode != null && mode.CurrentMode == ControlMode.OnFoot && mode.FootController != null)
            {
                PlayerConsumeAnimator.PlayFromBackpack(
                    mode.FootController,
                    chosen.PropVisual,
                    chosen.ConsumeKind,
                    dispName,
                    chosen.HungerRestore,
                    chosen.EnergyRestore,
                    chosen.HealthRestore);
            }
            else
            {
                // Driving / cycling / seated: consume cleanly right away without leaving seat!
                needs?.ApplyMealRestore(chosen.HungerRestore, chosen.EnergyRestore, chosen.HealthRestore);
                GameFeelAudio.PlayUiClick();
                mode?.RaiseStatusMessage(en
                    ? $"Enjoyed {dispName} from Backpack! (+{Mathf.RoundToInt(chosen.HungerRestore)} Food · +{Mathf.RoundToInt(chosen.EnergyRestore)} Energy)"
                    : $"Çantandan {dispName} yedin/içtin! (+{Mathf.RoundToInt(chosen.HungerRestore)} Tokluk · +{Mathf.RoundToInt(chosen.EnergyRestore)} Enerji)");
            }

            return true;
        }

        public bool TryQuickEatBestItem(PlayerModeController mode)
        {
            List<BackpackDisplayItem> all = GetAllEntriesSnapshot();
            if (all.Count == 0) return false;

            // Pick the highest hunger-restoring item when hungry, or highest energy item when tired
            bool preferFood = PlayerNeedsController.Instance == null ||
                              PlayerNeedsController.Instance.Hunger <= PlayerNeedsController.Instance.Energy;
            int bestIdx = 0;
            float bestScore = -1f;
            for (int i = 0; i < all.Count; i++)
            {
                float s = preferFood ? all[i].HungerRestore : all[i].EnergyRestore;
                if (s > bestScore)
                {
                    bestScore = s;
                    bestIdx = i;
                }
            }
            return TryConsumeItem(all[bestIdx].ItemId, mode);
        }

        private static string MakeSlugId(string name, ShopPropVisual visual)
        {
            string s = (name ?? visual.ToString()).Trim().ToLowerInvariant();
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length + 6);
            sb.Append("item_");
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
                else if (c == ' ' || c == '-' || c == '&') sb.Append('_');
            }
            return sb.ToString();
        }
    }
}
