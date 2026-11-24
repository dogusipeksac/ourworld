using System;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    public enum FarmCropType
    {
        None = 0,
        Tomato = 1,
        Pepper = 2,
        Strawberry = 3,
        Sunflower = 4
    }

    public enum FarmAnimalKind
    {
        Chicken = 0,
        Cow = 1,
        Sheep = 2
    }

    public struct FarmCropSpec
    {
        public FarmCropType Type;
        public string NameTr;
        public string NameEn;
        public int SeedCost;
        public int SellPrice;
        public float GrowSeconds;
        public Color FruitColor;
    }

    /// <summary>
    /// Central Phase 1 Farming & Livestock economy manager:
    /// Handles seed selection, crop & animal harvest inventory, farm upgrades (extra animals & auto-sprinkler),
    /// market selling (both at the Village Farm Stand and Selman Bakkal), and SaveData persistence.
    /// </summary>
    public static class FarmEconomyManager
    {
        public const int EggSellPrice = 32;
        public const int MilkSellPrice = 65;
        public const int WoolSellPrice = 75;

        public const int ExtraChickenCost = 120;
        public const int ExtraSheepCost = 200;
        public const int ExtraCowCost = 320;
        public const int AutoSprinklerCost = 350;

        public const int MaxExtraChickens = 5;
        public const int MaxExtraSheep = 4;
        public const int MaxExtraCows = 3;

        public static readonly FarmCropSpec[] Crops =
        {
            new FarmCropSpec
            {
                Type = FarmCropType.Tomato,
                NameTr = "Domates",
                NameEn = "Tomato",
                SeedCost = 10,
                SellPrice = 42,
                GrowSeconds = 36f,
                FruitColor = new Color(0.92f, 0.18f, 0.14f)
            },
            new FarmCropSpec
            {
                Type = FarmCropType.Pepper,
                NameTr = "Biber",
                NameEn = "Pepper",
                SeedCost = 15,
                SellPrice = 58,
                GrowSeconds = 44f,
                FruitColor = new Color(0.24f, 0.78f, 0.22f)
            },
            new FarmCropSpec
            {
                Type = FarmCropType.Strawberry,
                NameTr = "Çilek",
                NameEn = "Strawberry",
                SeedCost = 25,
                SellPrice = 88,
                GrowSeconds = 56f,
                FruitColor = new Color(0.95f, 0.16f, 0.36f)
            },
            new FarmCropSpec
            {
                Type = FarmCropType.Sunflower,
                NameTr = "Ayçiçeği",
                NameEn = "Sunflower",
                SeedCost = 35,
                SellPrice = 125,
                GrowSeconds = 68f,
                FruitColor = new Color(0.99f, 0.80f, 0.14f)
            }
        };

        public static event Action OnFarmDataChanged;
        public static event Action<FarmAnimalKind> OnExtraAnimalPurchased;
        public static event Action OnAutoSprinklerUnlocked;

        private static bool IsEn =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        public static SaveData Data => SaveManager.Instance != null ? SaveManager.Instance.Data : null;

        public static int SelectedSeedIndex
        {
            get => Data != null ? Mathf.Clamp(Data.selectedSeedIndex, 0, Crops.Length - 1) : 0;
            set
            {
                if (Data != null)
                {
                    Data.selectedSeedIndex = Mathf.Clamp(value, 0, Crops.Length - 1);
                    SaveManager.Instance.Save();
                }
                OnFarmDataChanged?.Invoke();
            }
        }

        public static FarmCropSpec SelectedCropSpec => Crops[SelectedSeedIndex];

        public static FarmCropSpec GetCropSpec(FarmCropType type)
        {
            for (int i = 0; i < Crops.Length; i++)
            {
                if (Crops[i].Type == type) return Crops[i];
            }
            return Crops[0];
        }

        public static string GetCropName(FarmCropType type)
        {
            FarmCropSpec spec = GetCropSpec(type);
            return IsEn ? spec.NameEn : spec.NameTr;
        }

        public static FarmPlotSaveEntry GetOrCreatePlotEntry(string plotId)
        {
            SaveData d = Data;
            if (d == null)
            {
                return new FarmPlotSaveEntry { plotId = plotId };
            }
            if (d.farmPlots == null) d.farmPlots = new List<FarmPlotSaveEntry>();

            for (int i = 0; i < d.farmPlots.Count; i++)
            {
                if (d.farmPlots[i] != null && d.farmPlots[i].plotId == plotId)
                {
                    return d.farmPlots[i];
                }
            }

            FarmPlotSaveEntry created = new FarmPlotSaveEntry { plotId = plotId };
            d.farmPlots.Add(created);
            return created;
        }

        public static void AddCropHarvest(FarmCropType crop, int amount = 1)
        {
            SaveData d = Data;
            if (d == null || amount <= 0) return;

            switch (crop)
            {
                case FarmCropType.Tomato: d.invTomatoes += amount; break;
                case FarmCropType.Pepper: d.invPeppers += amount; break;
                case FarmCropType.Strawberry: d.invStrawberries += amount; break;
                case FarmCropType.Sunflower: d.invSunflowers += amount; break;
            }
            d.totalCropsHarvested += amount;
            NeighborhoodReputation.AddReputation(4, IsEn ? "Farm Harvest" : "Bostan Hasadı");
            SaveManager.Instance.Save();
            OnFarmDataChanged?.Invoke();
        }

        public static void AddAnimalProduct(FarmAnimalKind kind, int amount = 1)
        {
            SaveData d = Data;
            if (d == null || amount <= 0) return;

            switch (kind)
            {
                case FarmAnimalKind.Chicken: d.invEggs += amount; break;
                case FarmAnimalKind.Cow: d.invMilk += amount; break;
                case FarmAnimalKind.Sheep: d.invWool += amount; break;
            }
            d.totalAnimalProductsCollected += amount;
            NeighborhoodReputation.AddReputation(kind == FarmAnimalKind.Chicken ? 4 : 6,
                IsEn ? "Farm Care" : "Çiftlik Üretimi");
            SaveManager.Instance.Save();
            OnFarmDataChanged?.Invoke();
        }

        public static int GetTotalProduceCount()
        {
            SaveData d = Data;
            if (d == null) return 0;
            return d.invTomatoes + d.invPeppers + d.invStrawberries + d.invSunflowers +
                   d.invEggs + d.invMilk + d.invWool + d.invFishCount;
        }

        public static int GetTotalProduceValue()
        {
            SaveData d = Data;
            if (d == null) return 0;
            return d.invTomatoes * Crops[0].SellPrice +
                   d.invPeppers * Crops[1].SellPrice +
                   d.invStrawberries * Crops[2].SellPrice +
                   d.invSunflowers * Crops[3].SellPrice +
                   d.invEggs * EggSellPrice +
                   d.invMilk * MilkSellPrice +
                   d.invWool * WoolSellPrice +
                   d.invFishTotalValue;
        }

        public static void AddCaughtFish(string fishName, int sellValue)
        {
            SaveData d = Data;
            if (d == null || sellValue <= 0) return;
            d.invFishCount++;
            d.invFishTotalValue += sellValue;
            d.totalFishCaught++;
            if (string.IsNullOrEmpty(d.bestFishName) || sellValue >= 165)
            {
                d.bestFishName = fishName;
            }
            SaveManager.Instance.Save();
            OnFarmDataChanged?.Invoke();
        }

        public static int SellAllProduce(Vector3 fxWorldPos, string locationLabel = null)
        {
            SaveData d = Data;
            if (d == null) return 0;

            int count = GetTotalProduceCount();
            int baseTotal = GetTotalProduceValue();
            if (count <= 0 || baseTotal <= 0)
            {
                if (PlayerModeController.Instance != null)
                {
                    PlayerModeController.Instance.RaiseStatusMessage(
                        IsEn
                            ? "No farm produce yet — harvest crops or collect eggs, milk & wool first!"
                            : "Satılacak çiftlik ürünün yok — önce bostandan hasat yap veya yumurta, süt, yün topla!");
                }
                return 0;
            }

            int earned = Mathf.RoundToInt(baseTotal * NeighborhoodReputation.GetJobRewardMultiplier());
            d.invTomatoes = 0;
            d.invPeppers = 0;
            d.invStrawberries = 0;
            d.invSunflowers = 0;
            d.invEggs = 0;
            d.invMilk = 0;
            d.invWool = 0;
            d.invFishCount = 0;
            d.invFishTotalValue = 0;

            SaveManager.Instance.AddMoney(earned);
            NeighborhoodReputation.AddReputation(Mathf.Clamp(count * 3, 5, 25),
                IsEn ? "Farm Market Sale" : "Çiftlik Satışı");
            SaveManager.Instance.Save();

            GameFeelAudio.PlayCoinChime();
            GameFeelAudio.SpawnBurst(fxWorldPos + Vector3.up * 1.3f, new Color(0.98f, 0.80f, 0.28f), count: 16, spread: 0.7f);

            if (PlayerModeController.Instance != null)
            {
                string loc = string.IsNullOrEmpty(locationLabel)
                    ? (IsEn ? "Farm Market" : "Köy Pazarı Tezgâhı")
                    : locationLabel;
                string msg = IsEn
                    ? $"{loc}: Sold {count} fresh farm items for +{earned} ₺!"
                    : $"{loc}: {count} taze çiftlik ürünü satıldı! (+{earned} ₺)";
                PlayerModeController.Instance.RaiseStatusMessage(msg);
            }

            OnFarmDataChanged?.Invoke();
            return earned;
        }

        public static bool TryBuyExtraAnimal(FarmAnimalKind kind, out string feedback)
        {
            SaveData d = Data;
            if (d == null)
            {
                feedback = string.Empty;
                return false;
            }

            int cost;
            int current;
            int max;
            string nameTr;
            string nameEn;

            switch (kind)
            {
                case FarmAnimalKind.Chicken:
                    cost = ExtraChickenCost; current = d.extraChickens; max = MaxExtraChickens;
                    nameTr = "Tavuk"; nameEn = "Chicken"; break;
                case FarmAnimalKind.Sheep:
                    cost = ExtraSheepCost; current = d.extraSheep; max = MaxExtraSheep;
                    nameTr = "Koyun"; nameEn = "Sheep"; break;
                default:
                    cost = ExtraCowCost; current = d.extraCows; max = MaxExtraCows;
                    nameTr = "İnek"; nameEn = "Cow"; break;
            }

            if (current >= max)
            {
                feedback = IsEn ? $"Maximum {nameEn} capacity reached!" : $"{nameTr} kapasitesi maksimuma ulaştı!";
                return false;
            }

            if (d.money < cost)
            {
                feedback = IsEn ? $"Need {cost} ₺ to buy a {nameEn}." : $"{nameTr} almak için {cost} ₺ gerekiyor.";
                return false;
            }

            SaveManager.Instance.TrySpend(cost);
            if (kind == FarmAnimalKind.Chicken) d.extraChickens++;
            else if (kind == FarmAnimalKind.Sheep) d.extraSheep++;
            else d.extraCows++;
            SaveManager.Instance.Save();

            GameFeelAudio.PlayCoinChime();
            feedback = IsEn ? $"New {nameEn} joined your farm!" : $"Çiftliğine yeni bir {nameTr} katıldı!";
            OnExtraAnimalPurchased?.Invoke(kind);
            OnFarmDataChanged?.Invoke();
            return true;
        }

        public static bool TryBuyAutoSprinkler(out string feedback)
        {
            SaveData d = Data;
            if (d == null)
            {
                feedback = string.Empty;
                return false;
            }

            if (d.hasAutoSprinkler)
            {
                feedback = IsEn ? "Auto-Sprinklers already active!" : "Otomatik Fıskiye Sulama zaten aktif!";
                return false;
            }

            if (d.money < AutoSprinklerCost)
            {
                feedback = IsEn ? $"Need {AutoSprinklerCost} ₺ for Auto-Sprinklers." : $"Otomatik Fıskiye için {AutoSprinklerCost} ₺ gerekiyor.";
                return false;
            }

            SaveManager.Instance.TrySpend(AutoSprinklerCost);
            d.hasAutoSprinkler = true;
            SaveManager.Instance.Save();
            GameFeelAudio.PlayCoinChime();

            feedback = IsEn
                ? "Auto-Sprinklers installed! All garden & farm plots stay watered automatically."
                : "Otomatik Fıskiye kuruldu! Tüm bostan ve çiftlik tarlaları otomatik sulanıyor.";
            OnAutoSprinklerUnlocked?.Invoke();
            OnFarmDataChanged?.Invoke();
            return true;
        }
    }
}
