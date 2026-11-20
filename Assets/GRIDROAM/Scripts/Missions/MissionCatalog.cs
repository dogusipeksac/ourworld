using System.Collections.Generic;
using UnityEngine;

namespace Gridroam.Missions
{
    /// <summary>
    /// Ordered catalog of all missions in GRIDROAM.
    /// </summary>
    [CreateAssetMenu(menuName = "GRIDROAM/Mission Catalog", fileName = "MissionCatalog")]
    public class MissionCatalog : ScriptableObject
    {
        public List<MissionData> missions = new List<MissionData>();

        public MissionData GetById(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
            {
                return null;
            }

            foreach (MissionData m in missions)
            {
                if (m != null && string.Equals(m.missionId, missionId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return m;
                }
            }

            return null;
        }

        public MissionData GetFirstUncompleted(List<string> completedIds)
        {
            foreach (MissionData m in missions)
            {
                if (m == null) continue;
                if (completedIds == null || !completedIds.Contains(m.missionId))
                {
                    return m;
                }
            }

            return null;
        }
    }
}
