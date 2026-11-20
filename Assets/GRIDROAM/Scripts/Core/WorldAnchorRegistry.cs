using System.Collections.Generic;
using UnityEngine;

namespace Gridroam.Core
{
    /// <summary>
    /// Runtime lookup for WorldAnchor components by ID.
    /// </summary>
    public static class WorldAnchorRegistry
    {
        private static readonly Dictionary<string, WorldAnchor> Anchors =
            new Dictionary<string, WorldAnchor>(System.StringComparer.OrdinalIgnoreCase);

        public static void Register(WorldAnchor anchor)
        {
            if (anchor == null || string.IsNullOrWhiteSpace(anchor.AnchorId))
            {
                return;
            }

            Anchors[anchor.AnchorId] = anchor;
        }

        public static void Unregister(WorldAnchor anchor)
        {
            if (anchor == null || string.IsNullOrWhiteSpace(anchor.AnchorId))
            {
                return;
            }

            if (Anchors.TryGetValue(anchor.AnchorId, out WorldAnchor current) && current == anchor)
            {
                Anchors.Remove(anchor.AnchorId);
            }
        }

        public static WorldAnchor Get(string anchorId)
        {
            if (string.IsNullOrWhiteSpace(anchorId))
            {
                return null;
            }

            if (Anchors.TryGetValue(anchorId, out WorldAnchor anchor) && anchor != null)
            {
                return anchor;
            }

            // Lazy fallback scan in case of script execution order differences.
            WorldAnchor[] all = Object.FindObjectsByType<WorldAnchor>();
            foreach (WorldAnchor item in all)
            {
                if (item != null && !string.IsNullOrWhiteSpace(item.AnchorId))
                {
                    Anchors[item.AnchorId] = item;
                }
            }

            Anchors.TryGetValue(anchorId, out anchor);
            return anchor;
        }

        public static void Clear()
        {
            Anchors.Clear();
        }
    }
}
