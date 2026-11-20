using System;
using System.Collections.Generic;

namespace Gridroam.Core
{
    /// <summary>
    /// Serializable local save structure for mission progress, unlocked keys,
    /// player choices, and graphics/language preferences.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int schemaVersion = 1;
        public List<string> completedMissionIds = new List<string>();
        public string nextMissionId = "m01_get_the_keys";
        public bool hasVehicleKeys;
        public string lastEndingChoice = string.Empty;
        public string languageCode = "en";
        public int graphicsTierIndex = 1; // 0 = Low, 1 = Medium
    }
}
