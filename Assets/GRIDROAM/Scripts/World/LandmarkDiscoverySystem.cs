using System.Collections.Generic;
using Gridroam.Core;
using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Tracks exploration across 10 iconic Hamidiye Mahallesi landmarks.
    /// First time the player visits each area, awards cash, Neighborhood Reputation XP,
    /// chime feedback, and persists discovery in SaveData.discoveredLandmarkIds.
    /// </summary>
    public sealed class LandmarkDiscoverySystem : MonoBehaviour
    {
        public struct LandmarkDef
        {
            public string Id;
            public string NameTr;
            public string NameEn;
            public Vector3 Center;
            public float Radius;
        }

        public static readonly LandmarkDef[] Landmarks =
        {
            new LandmarkDef { Id = "lm_selman",      NameTr = "Selman Sokağı · Mahalle Merkezi",   NameEn = "Selman Street · Heart of Hamidiye", Center = new Vector3(   6f, 0f,    0f), Radius = 18f },
            new LandmarkDef { Id = "lm_camii",       NameTr = "Hz. Osman Camii Avlusu",            NameEn = "Hz. Osman Mosque Courtyard",        Center = new Vector3(  82f, 0f,  -34f), Radius = 18f },
            new LandmarkDef { Id = "lm_havuz",       NameTr = "Hamidiye Açık Yüzme Havuzu",        NameEn = "Hamidiye Open Swimming Pool",       Center = new Vector3(  16f, 0f,  -40f), Radius = 15f },
            new LandmarkDef { Id = "lm_tenis",       NameTr = "Gürsu Sokağı · Tenis Kortu",        NameEn = "Gürsu Street Tennis Court",         Center = new Vector3( -41f, 0f,  -42f), Radius = 16f },
            new LandmarkDef { Id = "lm_saatkulesi",  NameTr = "Fatih Bulvarı · Saat Kulesi",       NameEn = "Fatih Blvd · Clock Tower Plaza",    Center = new Vector3(   0f, 0f,  142f), Radius = 24f },
            new LandmarkDef { Id = "lm_caybahcesi",  NameTr = "Hamidiye Çay Bahçesi & Parkı",      NameEn = "Hamidiye Tea Garden & Park",        Center = new Vector3( -20f, 0f,   70f), Radius = 16f },
            new LandmarkDef { Id = "lm_eyyubi",      NameTr = "53 Eyyubi Mahalle Büfesi",          NameEn = "53 Eyyubi Neighborhood Market",     Center = new Vector3(-152f, 0f,  -18f), Radius = 18f },
            new LandmarkDef { Id = "lm_petrolyolu",  NameTr = "Petrol Yolu · Ticaret Şeridi",      NameEn = "Petrol Yolu Commercial Strip",      Center = new Vector3( -14f, 0f,  -78f), Radius = 22f },
            new LandmarkDef { Id = "lm_aziziye",     NameTr = "İTO Şehit Er Dursun Sıvaz İlkokulu",NameEn = "Aziziye Primary School",            Center = new Vector3( -16f, 0f, -118f), Radius = 18f },
            new LandmarkDef { Id = "lm_zubeyde",     NameTr = "Zübeyde Hanım · Çamlı Mavi Evler",  NameEn = "Zübeyde Hanım Pine Houses",         Center = new Vector3(  14f, 0f, -148f), Radius = 20f }
        };

        public static int TotalCount => Landmarks.Length;

        public static int DiscoveredCount =>
            SaveManager.Instance != null && SaveManager.Instance.Data != null && SaveManager.Instance.Data.discoveredLandmarkIds != null
                ? SaveManager.Instance.Data.discoveredLandmarkIds.Count
                : 0;

        private float _checkTimer = 1.2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            if (FindAnyObjectByType<LandmarkDiscoverySystem>() != null) return;
            GameObject go = new GameObject("Hamidiye_LandmarkDiscoverySystem");
            go.AddComponent<LandmarkDiscoverySystem>();
        }

        private void Update()
        {
            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = 0.75f;

            PlayerModeController mode = PlayerModeController.Instance;
            SaveManager sm = SaveManager.Instance;
            if (mode == null || sm == null || sm.Data == null || mode.IsModalLocked) return;

            List<string> discovered = sm.Data.discoveredLandmarkIds;
            if (discovered == null)
            {
                discovered = new List<string>();
                sm.Data.discoveredLandmarkIds = discovered;
            }
            if (discovered.Count >= Landmarks.Length) return;

            Vector3 p = mode.ActiveWorldPosition;
            for (int i = 0; i < Landmarks.Length; i++)
            {
                LandmarkDef lm = Landmarks[i];
                if (discovered.Contains(lm.Id)) continue;

                float dx = p.x - lm.Center.x;
                float dz = p.z - lm.Center.z;
                if (dx * dx + dz * dz <= lm.Radius * lm.Radius)
                {
                    discovered.Add(lm.Id);
                    sm.AddMoney(75);
                    bool allFound = discovered.Count >= Landmarks.Length;
                    if (allFound)
                    {
                        sm.AddMoney(500);
                    }

                    bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
                    string name = isEn ? lm.NameEn : lm.NameTr;
                    string msg = allFound
                        ? (isEn
                            ? $"All {TotalCount} Landmarks Discovered! Explorer Bonus +575 ₺"
                            : $"Tüm {TotalCount} Bölge Keşfedildi! Mahalle Kâşifi Ödülü +575 ₺")
                        : (isEn
                            ? $"Landmark Discovered ({discovered.Count}/{TotalCount}): {name} · +75 ₺"
                            : $"Yeni Bölge Keşfedildi ({discovered.Count}/{TotalCount}): {name} · +75 ₺");

                    mode.RaiseStatusMessage(msg);
                    if (allFound) GameFeelAudio.PlayMilestoneFanfare();
                    else GameFeelAudio.PlayCoinChime();

                    GameFeelAudio.SpawnBurst(p + Vector3.up * 1.4f, new Color(0.32f, 0.88f, 0.96f), count: 10, spread: 0.6f);
                    NeighborhoodReputation.AddReputation(allFound ? 60 : 20);
                    return;
                }
            }
        }
    }
}
