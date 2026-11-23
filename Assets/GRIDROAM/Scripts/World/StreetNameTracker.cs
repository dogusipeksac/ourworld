using Gridroam.Core;
using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Live GPS district street-name tracker across Hamidiye Mahallesi's 8 main avenues
    /// and Selman Sokağı's side streets: smoothly announces the street name via the HUD toast
    /// whenever the player turns onto a new avenue or enters a landmark plaza.
    /// </summary>
    public class StreetNameTracker : MonoBehaviour
    {
        private struct StreetZone
        {
            public string DisplayName;
            public float MinX, MaxX, MinZ, MaxZ;
            public int Priority;
        }

        private static readonly StreetZone[] Zones =
        {
            // Landmarks & Hero Streets (higher priority)
            new StreetZone { DisplayName = "Hz. Osman Camii Avlusu",            MinX =  68f, MaxX =  95f, MinZ = -54f, MaxZ = -21f, Priority = 3 },
            new StreetZone { DisplayName = "Hamidiye Açık Yüzme Havuzu",        MinX =   4f, MaxX =  28f, MinZ = -50f, MaxZ = -29f, Priority = 3 },
            new StreetZone { DisplayName = "Hamidiye Çay Bahçesi & Parkı",      MinX = -32f, MaxX =  -8f, MinZ =  62f, MaxZ =  78f, Priority = 3 },
            new StreetZone { DisplayName = "Gürsu Sokağı · Tenis Kortu",        MinX = -50f, MaxX = -32f, MinZ = -58f, MaxZ = -26f, Priority = 3 },
            new StreetZone { DisplayName = "Fatih Bulvarı · Saat Kulesi Meydanı", MinX = -32f, MaxX =  32f, MinZ = 128f, MaxZ = 158f, Priority = 3 },
            new StreetZone { DisplayName = "Selman Sokağı",                      MinX = -82f, MaxX =  95f, MinZ =  -7f, MaxZ =   7f, Priority = 2 },
            new StreetZone { DisplayName = "Gürsu Sokağı · Mahalle Sahası",      MinX = -48f, MaxX = -34f, MinZ = -28f, MaxZ =  -7f, Priority = 2 },
            new StreetZone { DisplayName = "Çiçekçi Sokağı",                     MinX = -17f, MaxX =  -4f, MinZ = -28f, MaxZ =  -7f, Priority = 2 },
            new StreetZone { DisplayName = "Nutuk Sokağı",                       MinX =  53f, MaxX =  66f, MinZ =   7f, MaxZ =  40f, Priority = 2 },

            // Hamidiye Mahallesi 8 Main Avenues
            new StreetZone { DisplayName = "Fatih Bulvarı",                      MinX = -195f, MaxX = 180f, MinZ = 130f, MaxZ = 156f, Priority = 1 },
            new StreetZone { DisplayName = "Atatürk Caddesi (TEM Yan Yolu)",     MinX = -185f, MaxX = 175f, MinZ =  86f, MaxZ = 108f, Priority = 1 },
            new StreetZone { DisplayName = "Mürşit Caddesi",                     MinX = -175f, MaxX = 165f, MinZ =  36f, MaxZ =  56f, Priority = 1 },
            new StreetZone { DisplayName = "Petrol Yolu Caddesi",                MinX = -175f, MaxX = 165f, MinZ = -88f, MaxZ = -68f, Priority = 1 },
            new StreetZone { DisplayName = "Aziziye Caddesi",                    MinX = -175f, MaxX = 165f, MinZ = -124f, MaxZ = -104f, Priority = 1 },
            new StreetZone { DisplayName = "Zübeyde Hanım Caddesi",              MinX = -175f, MaxX = 165f, MinZ = -156f, MaxZ = -136f, Priority = 1 },
            new StreetZone { DisplayName = "Eyyubi Caddesi",                     MinX = -158f, MaxX = -138f, MinZ = -156f, MaxZ = 156f, Priority = 1 },
            new StreetZone { DisplayName = "Seyyid Kutup Caddesi",               MinX =  -94f, MaxX =  -76f, MinZ = -156f, MaxZ = 156f, Priority = 1 },
            new StreetZone { DisplayName = "Abdülkadir Udeh Caddesi",            MinX =   90f, MaxX =  108f, MinZ = -156f, MaxZ = 156f, Priority = 1 }
        };

        private string _currentStreet = string.Empty;
        private float _checkTimer;
        private float _lastAnnounceTime = -99f;

        public string CurrentStreetName => _currentStreet;

        private void Update()
        {
            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = 0.45f;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsModalLocked) return;

            Vector3 p = mode.ActiveWorldPosition;
            string bestName = null;
            int bestPriority = -1;

            for (int i = 0; i < Zones.Length; i++)
            {
                StreetZone z = Zones[i];
                if (p.x >= z.MinX && p.x <= z.MaxX && p.z >= z.MinZ && p.z <= z.MaxZ)
                {
                    if (z.Priority > bestPriority)
                    {
                        bestPriority = z.Priority;
                        bestName = z.DisplayName;
                    }
                }
            }

            if (string.IsNullOrEmpty(bestName) || bestName == _currentStreet)
            {
                return;
            }

            bool wasEmpty = string.IsNullOrEmpty(_currentStreet);
            _currentStreet = bestName;
            CrashlyticsService.SetKey("current_street", bestName);

            if (!wasEmpty && Time.time - _lastAnnounceTime >= 2.2f)
            {
                _lastAnnounceTime = Time.time;
                mode.RaiseStatusMessage($"Hamidiye Mahallesi · {bestName}");
            }
        }
    }
}
