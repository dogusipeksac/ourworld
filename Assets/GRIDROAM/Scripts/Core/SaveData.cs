using System;
using System.Collections.Generic;

namespace OurWorld.Core
{
    [Serializable]
    public class HousePaintEntry
    {
        public string houseId = string.Empty;
        public float r = 1f;
        public float g = 1f;
        public float b = 1f;
    }

    [Serializable]
    public class FarmPlotSaveEntry
    {
        public string plotId = string.Empty;
        public int cropType;
        public float growthProgress;
        public bool isWatered;
    }

    /// <summary>
    /// Serializable local save structure for mission progress, unlocked keys,
    /// economy, player needs, clock, fuel, stats, player/vehicle world state,
    /// graphics/language preferences, and painted house colours.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int schemaVersion = 4;
        public List<string> completedMissionIds = new List<string>();
        public string nextMissionId = "m01_get_the_keys";
        public bool hasVehicleKeys = true;
        public string lastEndingChoice = string.Empty;
        public string languageCode = "tr";
        public int graphicsTierIndex = 1; // 0 = Low, 1 = Medium
        public List<HousePaintEntry> housePaint = new List<HousePaintEntry>();

        // Open-World Story, Reputation, Discovery & Collectibles (v4)
        public int reputationXp;
        public bool hasSeenStoryIntro;
        public bool chapterThreeCompleted;
        public List<string> discoveredLandmarkIds = new List<string>();
        public List<string> collectedMemoryIds = new List<string>();
        public int streetEventsHelpedCount;

        // E1: Economy
        public int money = 250;

        // E2: Day / Night clock (0..24 hours, default 09:00)
        public float timeOfDayHours = 9.0f;

        // E4: Player needs & mission sedan fuel
        public float energy = 80f;
        public float missionSedanFuel = 0.8f;

        // E5: Open-world activity stats
        public int deliveriesDone;
        public int taxiFaresDone;
        public int housesPaintedCount;
        public int litterCollectedCount;
        public int footballGoalsScored;
        public float totalPlayTimeSeconds;
        public float lastAtmClaimPlayTimeSeconds = -999f;

        // E6: Permanent shop upgrades & wardrobe
        public bool hasRunningShoes;
        public bool hasTurboKit;
        public int playerJacketColorIndex;

        // E8: Player position & active mode/vehicle state
        public bool hasSavedPlayerState;
        public float playerPosX = -3.0f;
        public float playerPosY = 0.12f;
        public float playerPosZ = -21.5f;
        public float playerRotY = 180f;
        public bool savedInVehicle;
        public string savedVehicleId = string.Empty;
        public float savedVehiclePosX = 9.5f;
        public float savedVehiclePosY = 0.28f;
        public float savedVehiclePosZ = -32.5f;
        public float savedVehicleRotY = 0f;
        public float savedVehicleFuel = 0.8f;

        // Phase 1: Gardening, Farming & Livestock (Home Bostan + Aydos Farm)
        public List<FarmPlotSaveEntry> farmPlots = new List<FarmPlotSaveEntry>();
        public int selectedSeedIndex = 0; // 0=Tomato, 1=Pepper, 2=Strawberry, 3=Sunflower
        public int invTomatoes;
        public int invPeppers;
        public int invStrawberries;
        public int invSunflowers;
        public int invEggs;
        public int invMilk;
        public int invWool;
        public int extraChickens;
        public int extraSheep;
        public int extraCows;
        public bool hasAutoSprinkler;
        public int totalCropsHarvested;
        public int totalAnimalProductsCollected;

        // Phase 2: No:73 Auto Repair Workshop & Car Modification
        public int carPaintIndex = -1;
        public int carRimIndex;
        public int carHeadlightMode;
        public bool carHasSpoiler;
        public bool carHasRoofRack;
        public bool carHasTurboUpgrade;
        public bool carHasSportSuspension;
        public bool carHasNitroUpgrade;
        public int totalCarsRepaired;

        // Phase 3: Sultanbeyli Lake Fishing, Car Wash & No:48 Home Decor
        public int invFishCount;
        public int invFishTotalValue;
        public int totalFishCaught;
        public string bestFishName = string.Empty;
        public int totalCarWashes;
        public bool decorLuxurySofa;
        public bool decorMediaConsole;
        public bool decorAquarium;
        public bool decorBackyardMangal;
        public bool decorGardenLanterns;
        public bool decorFarmLounge;

        // Sports Leagues & Personal Records (Football, Volleyball, Tennis, Swimming)
        public int footballWins;
        public int footballLeagueTier; // 0=Acemi, 1=Mahalle, 2=Şampiyonlar
        public int volleyballWins;
        public int volleyballLeagueTier;
        public int tennisWins;
        public int tennisLeagueTier;
        public float bestTennisServeKmh;
        public int swimWins;
        public float bestSwim50mSeconds;
    }
}
