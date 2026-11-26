using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
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
            public string DisplayNameTr;
            public string DisplayNameEn;
            public float MinX, MaxX, MinZ, MaxZ;
            public int Priority;
        }

        private static readonly StreetZone[] Zones =
        {
            // New Expanded Nature & Farm Regions (highest priority)
            new StreetZone { DisplayNameTr = "Aydos Yeşil Vadi · Çiftlik & Bostan", DisplayNameEn = "Aydos Green Valley · Farm & Garden", MinX = 132f, MaxX = 244f, MinZ = -56f, MaxZ =  56f, Priority = 4 },
            new StreetZone { DisplayNameTr = "Aydos Çiftlik Yolu",                  DisplayNameEn = "Aydos Farm Road",                     MinX = 108f, MaxX = 136f, MinZ = -12f, MaxZ =  12f, Priority = 3 },
            new StreetZone { DisplayNameTr = "Evimin Bostanı (No:48)",              DisplayNameEn = "Home Garden (No:48)",                 MinX = -60f, MaxX = -51f, MinZ =   6f, MaxZ =  15f, Priority = 4 },
            new StreetZone { DisplayNameTr = "Sultanbeyli Göleti & Aydos Korusu",   DisplayNameEn = "Sultanbeyli Lake & Aydos Grove",      MinX =-244f, MaxX =-156f, MinZ = -62f, MaxZ =  62f, Priority = 4 },
            new StreetZone { DisplayNameTr = "Gölet Yürüyüş Yolu",                  DisplayNameEn = "Lakeside Promenade",                  MinX =-160f, MaxX =-108f, MinZ = -12f, MaxZ =  12f, Priority = 3 },

            // Landmarks & Hero Streets (higher priority)
            new StreetZone { DisplayNameTr = "Hz. Osman Camii Avlusu",              DisplayNameEn = "Hz. Osman Mosque Courtyard",          MinX =  68f, MaxX =  95f, MinZ = -54f, MaxZ = -21f, Priority = 3 },
            new StreetZone { DisplayNameTr = "Hamidiye Açık Yüzme Havuzu",          DisplayNameEn = "Hamidiye Outdoor Swimming Pool",      MinX =   4f, MaxX =  28f, MinZ = -50f, MaxZ = -29f, Priority = 3 },
            new StreetZone { DisplayNameTr = "Hamidiye Çay Bahçesi & Parkı",        DisplayNameEn = "Hamidiye Tea Garden & Park",          MinX = -32f, MaxX =  -8f, MinZ =  62f, MaxZ =  78f, Priority = 3 },
            new StreetZone { DisplayNameTr = "Gürsu Sokağı · Tenis Kortu",          DisplayNameEn = "Gürsu St. · Tennis Court",            MinX = -50f, MaxX = -32f, MinZ = -58f, MaxZ = -26f, Priority = 3 },
            new StreetZone { DisplayNameTr = "Fatih Bulvarı · Saat Kulesi Meydanı", DisplayNameEn = "Fatih Blvd. · Clock Tower Plaza",     MinX = -32f, MaxX =  32f, MinZ = 128f, MaxZ = 158f, Priority = 3 },
            new StreetZone { DisplayNameTr = "Selman Sokağı",                       DisplayNameEn = "Selman Street",                       MinX = -82f, MaxX =  95f, MinZ =  -7f, MaxZ =   7f, Priority = 2 },
            new StreetZone { DisplayNameTr = "Gürsu Sokağı · Mahalle Sahası",       DisplayNameEn = "Gürsu St. · Neighborhood Pitch",      MinX = -48f, MaxX = -34f, MinZ = -28f, MaxZ =  -7f, Priority = 2 },
            new StreetZone { DisplayNameTr = "Çiçekçi Sokağı",                      DisplayNameEn = "Çiçekçi Street",                      MinX = -17f, MaxX =  -4f, MinZ = -28f, MaxZ =  -7f, Priority = 2 },
            new StreetZone { DisplayNameTr = "Nutuk Sokağı",                        DisplayNameEn = "Nutuk Street",                        MinX =  53f, MaxX =  66f, MinZ =   7f, MaxZ =  40f, Priority = 2 },

            // Hamidiye Mahallesi Main Avenues & Expanded Google Maps Streets
            new StreetZone { DisplayNameTr = "Havadis Sokağı · Taş Fırın & Mahalle", DisplayNameEn = "Havadis Street · Stone Bakery",         MinX = -175f, MaxX = 165f, MinZ = 175f, MaxZ = 206f, Priority = 2 },
            new StreetZone { DisplayNameTr = "Kadife Sokağı · Kıraathane & Bahçe",   DisplayNameEn = "Kadife Street · Tea House & Garden",  MinX = -175f, MaxX = 165f, MinZ = -206f, MaxZ = -175f, Priority = 2 },
            new StreetZone { DisplayNameTr = "Fatih Bulvarı",                       DisplayNameEn = "Fatih Boulevard",                     MinX = -195f, MaxX = 180f, MinZ = 130f, MaxZ = 156f, Priority = 1 },
            new StreetZone { DisplayNameTr = "Atatürk Caddesi (TEM Yan Yolu)",      DisplayNameEn = "Atatürk Avenue (TEM Side Rd)",        MinX = -185f, MaxX = 175f, MinZ =  86f, MaxZ = 108f, Priority = 1 },
            new StreetZone { DisplayNameTr = "Mürşit Caddesi",                      DisplayNameEn = "Mürşit Avenue",                       MinX = -175f, MaxX = 165f, MinZ =  36f, MaxZ =  56f, Priority = 1 },
            new StreetZone { DisplayNameTr = "Petrol Yolu Caddesi",                 DisplayNameEn = "Petrol Yolu Avenue",                  MinX = -175f, MaxX = 165f, MinZ = -88f, MaxZ = -68f, Priority = 1 },
            new StreetZone { DisplayNameTr = "Aziziye Caddesi",                     DisplayNameEn = "Aziziye Avenue",                      MinX = -175f, MaxX = 165f, MinZ = -124f, MaxZ = -104f, Priority = 1 },
            new StreetZone { DisplayNameTr = "Zübeyde Hanım Caddesi",               DisplayNameEn = "Zübeyde Hanım Avenue",                MinX = -175f, MaxX = 165f, MinZ = -156f, MaxZ = -136f, Priority = 1 },
            new StreetZone { DisplayNameTr = "Eyyubi Caddesi",                      DisplayNameEn = "Eyyubi Avenue",                       MinX = -158f, MaxX = -138f, MinZ = -196f, MaxZ = 196f, Priority = 1 },
            new StreetZone { DisplayNameTr = "Seyyid Kutup Caddesi",                DisplayNameEn = "Seyyid Kutup Avenue",                 MinX =  -94f, MaxX =  -76f, MinZ = -196f, MaxZ = 196f, Priority = 1 },
            new StreetZone { DisplayNameTr = "Aydos Tazyikli Oto Yıkama",           DisplayNameEn = "Aydos Pressure Car Wash",             MinX =  114f, MaxX =  134f, MinZ = -24f, MaxZ =  -4f, Priority = 7 },
            new StreetZone { DisplayNameTr = "Abdülkadir Udeh Caddesi",             DisplayNameEn = "Abdülkadir Udeh Avenue",              MinX =   90f, MaxX =  108f, MinZ = -196f, MaxZ = 196f, Priority = 1 }
        };

        private int _currentZoneIndex = -1;
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
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

            // Dynamic 5-level floor tracking inside Hamidiye Atlas AVM (X:-135..-97, Z:-68..-17)
            if (p.x >= -135f && p.x <= -97f && p.z >= -68f && p.z <= -17f)
            {
                int floorIdx;
                string mallDisplay;
                if (p.z < -50.5f)
                {
                    floorIdx = 100;
                    mallDisplay = isEn ? "Hamidiye Atlas Mall · Entrance Plaza" : "Hamidiye Atlas AVM · Giriş Meydanı";
                }
                else if (p.y >= 17.0f)
                {
                    floorIdx = 104;
                    mallDisplay = isEn ? "Hamidiye Atlas Mall · Rooftop Terrace" : "Hamidiye Atlas AVM · Çatı Terası";
                }
                else if (p.y >= 12.4f)
                {
                    floorIdx = 103;
                    mallDisplay = isEn ? "Hamidiye Atlas Mall · 3rd Floor (Cinema)" : "Hamidiye Atlas AVM · 3. Kat (Sinema)";
                }
                else if (p.y >= 7.8f)
                {
                    floorIdx = 102;
                    mallDisplay = isEn ? "Hamidiye Atlas Mall · 2nd Floor (Food Court)" : "Hamidiye Atlas AVM · 2. Kat (Yemek)";
                }
                else if (p.y >= 3.2f)
                {
                    floorIdx = 101;
                    mallDisplay = isEn ? "Hamidiye Atlas Mall · 1st Floor (Fashion)" : "Hamidiye Atlas AVM · 1. Kat (Moda)";
                }
                else
                {
                    floorIdx = 105;
                    mallDisplay = isEn ? "Hamidiye Atlas Mall · Ground Atrium" : "Hamidiye Atlas AVM · Zemin Kat";
                }

                if (floorIdx == _currentZoneIndex)
                {
                    _currentStreet = mallDisplay;
                    return;
                }

                bool wasEmptyMall = _currentZoneIndex < 0;
                _currentZoneIndex = floorIdx;
                _currentStreet = mallDisplay;
                CrashlyticsService.SetKey("current_street", mallDisplay);
                if (!wasEmptyMall && Time.time - _lastAnnounceTime >= 1.5f)
                {
                    _lastAnnounceTime = Time.time;
                    mode.RaiseStatusMessage(mallDisplay);
                }
                return;
            }

            int bestIndex = -1;
            int bestPriority = -1;

            for (int i = 0; i < Zones.Length; i++)
            {
                StreetZone z = Zones[i];
                if (p.x >= z.MinX && p.x <= z.MaxX && p.z >= z.MinZ && p.z <= z.MaxZ)
                {
                    if (z.Priority > bestPriority)
                    {
                        bestPriority = z.Priority;
                        bestIndex = i;
                    }
                }
            }

            if (bestIndex < 0)
            {
                return;
            }

            StreetZone best = Zones[bestIndex];
            string display = isEn ? best.DisplayNameEn : best.DisplayNameTr;

            if (bestIndex == _currentZoneIndex)
            {
                _currentStreet = display;
                return;
            }

            bool wasEmpty = _currentZoneIndex < 0;
            _currentZoneIndex = bestIndex;
            _currentStreet = display;
            CrashlyticsService.SetKey("current_street", best.DisplayNameTr);

            if (!wasEmpty && Time.time - _lastAnnounceTime >= 2.2f)
            {
                _lastAnnounceTime = Time.time;
                string prefix = isEn ? "Hamidiye District" : "Hamidiye Mahallesi";
                mode.RaiseStatusMessage($"{prefix} · {display}");
            }
        }
    }
}
