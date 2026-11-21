using System;
using System.Collections.Generic;

namespace Gridroam.Core
{
    [Serializable]
    public class HousePaintEntry
    {
        public string houseId = string.Empty;
        public float r = 1f;
        public float g = 1f;
        public float b = 1f;
    }

    /// <summary>
    /// Serializable local save structure for mission progress, unlocked keys,
    /// economy, player needs, clock, fuel, stats, player/vehicle world state,
    /// graphics/language preferences, and painted house colours.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int schemaVersion = 3;
        public List<string> completedMissionIds = new List<string>();
        public string nextMissionId = "m01_get_the_keys";
        public bool hasVehicleKeys;
        public string lastEndingChoice = string.Empty;
        public string languageCode = "tr";
        public int graphicsTierIndex = 1; // 0 = Low, 1 = Medium
        public List<HousePaintEntry> housePaint = new List<HousePaintEntry>();

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
        public float totalPlayTimeSeconds;

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
    }
}
