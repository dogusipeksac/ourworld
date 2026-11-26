using System.Collections.Generic;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Defines a swimmable pool or water basin in the open world.
    /// Players entering the horizontal bounds below waterSurfaceY smoothly transition into swimming mode.
    /// </summary>
    public class WaterZone : MonoBehaviour
    {
        private static readonly List<WaterZone> ActiveZones = new List<WaterZone>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ActiveZones.Clear();

        [SerializeField] private Vector3 localCenter = Vector3.zero;
        [SerializeField] private Vector3 zoneSize = new Vector3(12f, 2.2f, 18f);
        [SerializeField] private float waterSurfaceWorldY = 1.18f;
        [SerializeField] private float poolFloorWorldY = 0.05f;

        public void Configure(Vector3 center, Vector3 size, float surfaceY, float floorY)
        {
            localCenter = center;
            zoneSize = size;
            waterSurfaceWorldY = surfaceY;
            poolFloorWorldY = floorY;
        }

        private void OnEnable()
        {
            if (!ActiveZones.Contains(this))
            {
                ActiveZones.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveZones.Remove(this);
        }

        public static bool TrySampleWater(Vector3 worldPos, out float surfaceY, out float floorY)
        {
            for (int i = 0; i < ActiveZones.Count; i++)
            {
                WaterZone z = ActiveZones[i];
                if (z == null) continue;

                Vector3 c = z.transform.TransformPoint(z.localCenter);
                Vector3 half = z.zoneSize * 0.5f;
                if (Mathf.Abs(worldPos.x - c.x) <= half.x &&
                    Mathf.Abs(worldPos.z - c.z) <= half.z &&
                    worldPos.y <= z.waterSurfaceWorldY + 0.12f &&
                    worldPos.y >= z.poolFloorWorldY - 0.6f)
                {
                    surfaceY = z.waterSurfaceWorldY;
                    floorY = z.poolFloorWorldY;
                    return true;
                }
            }

            surfaceY = 0f;
            floorY = 0f;
            return false;
        }
    }
}
