using System.Collections.Generic;
using Gridroam.Player;
using Gridroam.World;
using UnityEngine;

namespace Gridroam.Editor
{
    public static partial class GridroamPrototypeSetup
    {
        /// <summary>
        /// Builds the photo-matched 1-to-1 Google Street View expansion of Hamidiye Mahallesi's
        /// 8 main avenues surrounding our Selman Sokağı + Hz. Osman Camii hero core:
        ///   1. Fatih Bulvarı (Grand 2x2 boulevard + Sultanbeyli Saat Kulesi plaza + commercial blocks)
        ///   2. Atatürk Caddesi (TEM O-4 highway guardrail + dense pine embankment + shops)
        ///   3. Mürşit Caddesi (AYEDAŞ Trafo house + dark-grey white-striped apartments + pylon)
        ///   4. Eyyubi Caddesi (53 Eyyubi Mahalle Büfesi + turquoise-red shutters + blue garden gates)
        ///   5. Seyyid Kutup Caddesi (3-story tan-beige apartments + raw-brick corners)
        ///   6. Petrol Yolu Caddesi (395 Petrol Yolu orange-awning commercial blocks + terracotta apts)
        ///   7. Aziziye Caddesi (46 Aziziye cobalt-blue storefronts + İTO Şehit Er Dursun Sıvaz İlkokulu)
        ///   8. Zübeyde Hanım Caddesi (71 Zübeyde Hanım sky-blue wood-balcony houses + pine trees)
        /// </summary>
        private static void BuildHamidiyeEightMainAvenues(Transform parent, MaterialSet mats, List<SeatInteractable> seats)
        {
            GameObject hamRoot = new GameObject("Hamidiye_8MainAvenues_1to1_StreetView");
            hamRoot.transform.SetParent(parent, false);

            Quaternion faceSouth = Quaternion.Euler(0f, 180f, 0f);
            Quaternion faceNorth = Quaternion.identity;
            Quaternion faceEast  = Quaternion.Euler(0f, 90f, 0f);
            Quaternion faceWest  = Quaternion.Euler(0f, -90f, 0f);

            // =========================================================================
            // 1. CONNECTED ROAD NETWORK (All 8 Avenues seamlessly tied into Selman Sk.!)
            // =========================================================================
            GameObject roads = new GameObject("Hamidiye_AvenueRoads");
            roads.transform.SetParent(hamRoot.transform, false);

            // North-South Arterials extending from Zübeyde Hanım (-150) all the way to Fatih Bulvarı (+148):
            // a) Eyyubi Caddesi (X = -148)
            CreateTiledBox("Road_EyyubiCd", roads.transform, new Vector3(-148f, 0.014f, -2f), new Vector3(9.2f, 0.028f, 304f), Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);
            // b) Seyyid Kutup Caddesi (X = -85, seamless extension of existing Selman west cross-street!)
            CreateTiledBox("Road_SeyyidKutupCd_Ext", roads.transform, new Vector3(-85f, 0.014f, -2f), new Vector3(8.6f, 0.028f, 304f), Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);
            // c) Abdülkadir Udeh Caddesi (X = +99, seamless extension linking Fatih Bulvarı -> Selman -> Hz. Osman Camii -> Zübeyde Hanım!)
            CreateTiledBox("Road_AbdulkadirUdeh_Ext", roads.transform, new Vector3(99f, 0.014f, -2f), new Vector3(9.0f, 0.028f, 304f), Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);
            // d) East Hamidiye Ring Connector (X = +152)
            CreateTiledBox("Road_EastRingConnector", roads.transform, new Vector3(152f, 0.014f, -2f), new Vector3(8.6f, 0.028f, 304f), Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);
            // e) Nutuk Sokağı North Connector (X = +59.2 -> Mürşit Cd.)
            CreateTiledBox("Road_NutukToMursit", roads.transform, new Vector3(59.2f, 0.015f, 30f), new Vector3(7.0f, 0.030f, 34f), Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);
            // f) Selman Sokağı West Connector -> Eyyubi Cd.
            CreateTiledBox("Road_SelmanWestToEyyubi", roads.transform, new Vector3(-130f, 0.014f, 0f), new Vector3(44f, 0.028f, 7.6f), Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);
            // g) Selman Sokağı East Connector -> East Ring
            CreateTiledBox("Road_SelmanEastToRing", roads.transform, new Vector3(136f, 0.014f, 0f), new Vector3(38f, 0.028f, 7.6f), Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);

            // East-West Main Avenues:
            // 1. Mürşit Caddesi (Z = +46)
            CreateTiledBox("Road_MursitCd", roads.transform, new Vector3(2f, 0.015f, 46f), new Vector3(312f, 0.030f, 8.6f), Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);
            // 2. Atatürk Caddesi (Z = +96, wide 2-lane avenue with yellow-white curbs & TEM guardrail!)
            CreateTiledBox("Road_AtaturkCd", roads.transform, new Vector3(2f, 0.015f, 96f), new Vector3(316f, 0.030f, 10.6f), Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);
            // 3. Fatih Bulvarı (Z = +142, Grand 2x2 divided boulevard — 19m wide with central travertine Saat Kulesi median!)
            CreateTiledBox("Road_FatihBulvari", roads.transform, new Vector3(2f, 0.016f, 142f), new Vector3(322f, 0.032f, 19.0f), Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);
            // 4. Petrol Yolu Caddesi (Z = -78, wide commercial arterial passing south of Hz. Osman Camii!)
            CreateTiledBox("Road_PetrolYoluCd", roads.transform, new Vector3(2f, 0.015f, -78f), new Vector3(312f, 0.030f, 10.6f), Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);
            // 5. Aziziye Caddesi (Z = -114)
            CreateTiledBox("Road_AziziyeCd", roads.transform, new Vector3(2f, 0.015f, -114f), new Vector3(312f, 0.030f, 9.2f), Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);
            // 6. Zübeyde Hanım Caddesi (Z = -146)
            CreateTiledBox("Road_ZubeydeHanimCd", roads.transform, new Vector3(2f, 0.015f, -146f), new Vector3(312f, 0.030f, 8.8f), Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);

            // =========================================================================
            // 2. AVENUE 1: FATİH BULVARI + ICONIC SULTANBEYLİ SAAT KULESİ (Mar 2026 SV!)
            // =========================================================================
            GameObject fatihRoot = new GameObject("Avenue_01_FatihBulvari_SaatKulesi");
            fatihRoot.transform.SetParent(hamRoot.transform, false);

            // Central stone plaza median (Saat Kulesi Meydanı)
            CreateTiledBox("Fatih_SaatKulesiMedian", fatihRoot.transform, new Vector3(0f, 0.08f, 142f), new Vector3(44f, 0.16f, 5.4f), Quaternion.identity, mats.Concrete, 2.0f, collider: true);
            PlaceSelmanModel("Hamidiye_FatihSaatKulesi.fbx", "Sultanbeyli_SaatKulesi", fatihRoot.transform,
                new Vector3(0f, 0.16f, 142f), faceSouth, Vector3.one,
                new Vector3(3.6f, 9.5f, 3.6f), new Vector3(0f, 4.75f, 0f), mats);
            CreateWorldSignText("SULTANBEYLİ · FATİH BULVARI", fatihRoot.transform,
                new Vector3(0f, 6.2f, 138.2f), Quaternion.identity, new Vector2(4.4f, 0.52f), new Color(0.11f, 0.28f, 0.42f, 1f));

            // Median benches, trees & warm night lights around Saat Kulesi
            var (fb1, fb2) = CreateBench("Fatih_MedianBench_W", fatihRoot.transform, new Vector3(-9.5f, 0.16f, 142f), Quaternion.identity, mats);
            seats.Add(fb1); seats.Add(fb2);
            var (fb3, fb4) = CreateBench("Fatih_MedianBench_E", fatihRoot.transform, new Vector3(9.5f, 0.16f, 142f), Quaternion.identity, mats);
            seats.Add(fb3); seats.Add(fb4);
            CreateTownTree("Fatih_MedianTree_W", fatihRoot.transform, new Vector3(-16.5f, 0.16f, 142f), mats, 1.05f, 901);
            CreateTownTree("Fatih_MedianTree_E", fatihRoot.transform, new Vector3(16.5f, 0.16f, 142f), mats, 1.05f, 902);
            CreatePointLamp("Fatih_SaatKulesiGlow", fatihRoot.transform, new Vector3(0f, 7.5f, 137.5f), new Color(1.0f, 0.86f, 0.55f), 26f, 3.8f);

            // Commercial blocks lining North & South sides of Fatih Bulvarı
            float[] fatihXs = { -120f, -56f, -24f, 12f, 48f, 124f };
            for (int i = 0; i < fatihXs.Length; i++)
            {
                PlaceSelmanModel("Hamidiye_FatihCommercialBlock.fbx", $"Fatih_NorthBlock_{i + 1}", fatihRoot.transform,
                    new Vector3(fatihXs[i], 0f, 158.5f), faceSouth, Vector3.one,
                    new Vector3(14.2f, 13.5f, 9.6f), new Vector3(0f, 6.75f, 0f), mats);
                if (Mathf.Abs(fatihXs[i]) > 18f)
                {
                    PlaceSelmanModel("Hamidiye_FatihCommercialBlock.fbx", $"Fatih_SouthBlock_{i + 1}", fatihRoot.transform,
                        new Vector3(fatihXs[i], 0f, 125.5f), faceNorth, Vector3.one,
                        new Vector3(14.2f, 13.5f, 9.6f), new Vector3(0f, 6.75f, 0f), mats);
                }
            }
            CreateIstanbulRedStreetSign(fatihRoot.transform, "FATİH BULVARI", new Vector3(-76f, 0f, 132f), 0f, mats);
            CreateIstanbulRedStreetSign(fatihRoot.transform, "FATİH BULVARI", new Vector3(92f, 0f, 132f), 0f, mats);

            // =========================================================================
            // 3. AVENUE 2: ATATÜRK CADDESİ (TEM O-4 Guardrail & Pine Embankment — Oca 2026!)
            // =========================================================================
            GameObject ataturkRoot = new GameObject("Avenue_02_AtaturkCaddesi");
            ataturkRoot.transform.SetParent(hamRoot.transform, false);

            // Continuous galvanized W-beam highway guardrail + dense green cypress wall along north edge (Z = +102.5)
            for (int gx = -132; gx <= 132; gx += 12)
            {
                if (Mathf.Abs(gx + 85) < 9 || Mathf.Abs(gx - 99) < 9) continue; // Keep cross-avenues clear
                PlaceSelmanModel("Hamidiye_AtaturkHighwayGuardrail.fbx", $"Ataturk_Guardrail_{gx}", ataturkRoot.transform,
                    new Vector3(gx, 0.04f, 102.2f), Quaternion.identity, Vector3.one,
                    new Vector3(12f, 1.0f, 0.35f), new Vector3(0f, 0.5f, 0f), mats);
                if (gx % 24 == 0)
                {
                    CreateTownTree($"Ataturk_Pine_{gx}", ataturkRoot.transform, new Vector3(gx, 0.04f, 106.5f), mats, 1.35f, 920 + gx);
                }
            }
            // South side of Atatürk Cd.: commercial & residential buildings
            float[] ataturkXs = { -116f, -52f, -12f, 32f, 70f, 126f };
            for (int i = 0; i < ataturkXs.Length; i++)
            {
                string fbx = (i % 2 == 0) ? "Hamidiye_PetrolYoluBrandaBlock.fbx" : "Hamidiye_SeyyidKutupApt.fbx";
                PlaceSelmanModel(fbx, $"Ataturk_SouthBldg_{i + 1}", ataturkRoot.transform,
                    new Vector3(ataturkXs[i], 0f, 82.0f), faceNorth, Vector3.one,
                    new Vector3(12f, 11f, 9.2f), new Vector3(0f, 5.5f, 0f), mats);
            }
            CreateIstanbulRedStreetSign(ataturkRoot.transform, "ATATÜRK CADDESİ", new Vector3(-78f, 0f, 90f), 0f, mats);
            CreateIstanbulRedStreetSign(ataturkRoot.transform, "ATATÜRK CADDESİ", new Vector3(92f, 0f, 90f), 0f, mats);

            // =========================================================================
            // 4. AVENUE 3: MÜRŞİT CADDESİ (AYEDAŞ Trafo + Pylon + Grey Striped Apts — Oca 2026!)
            // =========================================================================
            GameObject mursitRoot = new GameObject("Avenue_03_MursitCaddesi");
            mursitRoot.transform.SetParent(hamRoot.transform, false);

            // Exact Mürşit Cd. signature landmark: High-Voltage Pylon + Concrete Trafo Binası + Dark-Grey Striped Apt!
            PlaceSelmanModel("Sultanbeyli_HighVoltagePylon.fbx", "Mursit_HVPylon", mursitRoot.transform,
                new Vector3(-18f, 0f, 56.0f), Quaternion.identity, Vector3.one,
                new Vector3(5.8f, 26f, 5.8f), new Vector3(0f, 13f, 0f), mats);

            float[] mursitXs = { -118f, -52f, 2f, 40f, 78f, 124f };
            for (int i = 0; i < mursitXs.Length; i++)
            {
                PlaceSelmanModel("Hamidiye_MursitTrafoAndGreyApt.fbx", $"Mursit_Block_{i + 1}", mursitRoot.transform,
                    new Vector3(mursitXs[i], 0f, 57.5f), faceSouth, Vector3.one,
                    new Vector3(16f, 9.6f, 8.8f), new Vector3(0f, 4.8f, 0f), mats);
            }
            CreateIstanbulRedStreetSign(mursitRoot.transform, "MÜRŞİT CADDESİ", new Vector3(53f, 0f, 41f), 0f, mats);
            CreateIstanbulRedStreetSign(mursitRoot.transform, "MÜRŞİT CADDESİ", new Vector3(-78f, 0f, 41f), 0f, mats);

            // =========================================================================
            // 5. AVENUE 4: EYYUBİ CADDESİ (53 Eyyubi Mahalle Büfesi + Blue Gates!)
            // =========================================================================
            GameObject eyyubiRoot = new GameObject("Avenue_04_EyyubiCaddesi");
            eyyubiRoot.transform.SetParent(hamRoot.transform, false);

            float[] eyyubiZs = { -128f, -96f, -46f, -18f, 22f, 70f, 118f };
            for (int i = 0; i < eyyubiZs.Length; i++)
            {
                PlaceSelmanModel("Hamidiye_EyyubiBufeBlock.fbx", $"Eyyubi_WestBufe_{i + 1}", eyyubiRoot.transform,
                    new Vector3(-160.5f, 0f, eyyubiZs[i]), faceEast, Vector3.one,
                    new Vector3(8.0f, 6.4f, 14.0f), new Vector3(0f, 3.2f, 0f), mats);
                if (Mathf.Abs(eyyubiZs[i]) > 14f)
                {
                    PlaceSelmanModel("Hamidiye_SeyyidKutupApt.fbx", $"Eyyubi_EastApt_{i + 1}", eyyubiRoot.transform,
                        new Vector3(-135.5f, 0f, eyyubiZs[i]), faceWest, Vector3.one,
                        new Vector3(9.4f, 10.5f, 12.6f), new Vector3(0f, 5.25f, 0f), mats);
                }
            }
            CreateIstanbulRedStreetSign(eyyubiRoot.transform, "EYYUBİ CADDESİ", new Vector3(-142f, 0f, 8f), 90f, mats);
            CreateIstanbulRedStreetSign(eyyubiRoot.transform, "EYYUBİ CADDESİ", new Vector3(-142f, 0f, -70f), 90f, mats);

            // =========================================================================
            // 6. AVENUE 5: SEYYİD KUTUP CADDESİ (3-Story Tan Apartments & Brick Corners!)
            // =========================================================================
            GameObject seyyidRoot = new GameObject("Avenue_05_SeyyidKutupCaddesi");
            seyyidRoot.transform.SetParent(hamRoot.transform, false);

            float[] seyyidZs = { -130f, -96f, -52f, 68f, 118f };
            for (int i = 0; i < seyyidZs.Length; i++)
            {
                PlaceSelmanModel("Hamidiye_SeyyidKutupApt.fbx", $"SeyyidKutup_WestApt_{i + 1}", seyyidRoot.transform,
                    new Vector3(-97.5f, 0f, seyyidZs[i]), faceEast, Vector3.one,
                    new Vector3(9.4f, 10.5f, 12.6f), new Vector3(0f, 5.25f, 0f), mats);
                PlaceSelmanModel("Hamidiye_SeyyidKutupApt.fbx", $"SeyyidKutup_EastApt_{i + 1}", seyyidRoot.transform,
                    new Vector3(-72.5f, 0f, seyyidZs[i]), faceWest, Vector3.one,
                    new Vector3(9.4f, 10.5f, 12.6f), new Vector3(0f, 5.25f, 0f), mats);
            }

            // =========================================================================
            // 7. AVENUE 6: PETROL YOLU CADDESİ (395 Petrol Yolu Orange-Awning Commercial Blocks!)
            // =========================================================================
            GameObject petrolRoot = new GameObject("Avenue_06_PetrolYoluCaddesi");
            petrolRoot.transform.SetParent(hamRoot.transform, false);

            float[] petrolXs = { -120f, -56f, -18f, 18f, 54f, 126f };
            for (int i = 0; i < petrolXs.Length; i++)
            {
                PlaceSelmanModel("Hamidiye_PetrolYoluBrandaBlock.fbx", $"PetrolYolu_NorthBlock_{i + 1}", petrolRoot.transform,
                    new Vector3(petrolXs[i], 0f, -64.5f), faceSouth, Vector3.one,
                    new Vector3(11.4f, 12.8f, 9.4f), new Vector3(0f, 6.4f, 0f), mats);
                PlaceSelmanModel("Hamidiye_PetrolYoluBrandaBlock.fbx", $"PetrolYolu_SouthBlock_{i + 1}", petrolRoot.transform,
                    new Vector3(petrolXs[i], 0f, -91.5f), faceNorth, Vector3.one,
                    new Vector3(11.4f, 12.8f, 9.4f), new Vector3(0f, 6.4f, 0f), mats);
            }
            CreateWorldSignText("PETROL YOLU CD. · TENTE & TİCARET", petrolRoot.transform,
                new Vector3(-18f, 4.15f, -69.5f), Quaternion.identity, new Vector2(5.2f, 0.45f), new Color(0.84f, 0.34f, 0.10f, 1f));
            CreateIstanbulRedStreetSign(petrolRoot.transform, "PETROL YOLU CD.", new Vector3(-78f, 0f, -72f), 0f, mats);
            CreateIstanbulRedStreetSign(petrolRoot.transform, "PETROL YOLU CD.", new Vector3(92f, 0f, -72f), 0f, mats);

            // =========================================================================
            // 8. AVENUE 7: AZİZİYE CADDESİ (46 Aziziye Cobalt-Blue Shopfronts + İlkokul!)
            // =========================================================================
            GameObject aziziyeRoot = new GameObject("Avenue_07_AziziyeCaddesi");
            aziziyeRoot.transform.SetParent(hamRoot.transform, false);

            float[] aziziyeXs = { -118f, -54f, -16f, 24f, 62f, 126f };
            for (int i = 0; i < aziziyeXs.Length; i++)
            {
                PlaceSelmanModel("Hamidiye_AziziyeShopBlock.fbx", $"Aziziye_SouthShop_{i + 1}", aziziyeRoot.transform,
                    new Vector3(aziziyeXs[i], 0f, -126.5f), faceNorth, Vector3.one,
                    new Vector3(13.4f, 12.4f, 9.0f), new Vector3(0f, 6.2f, 0f), mats);
            }
            // Real Hamidiye School Plaque along Aziziye Cd.!
            CreateWorldSignText("İTO ŞEHİT ER DURSUN SIVAZ İLKOKULU", aziziyeRoot.transform,
                new Vector3(-16f, 4.5f, -121.6f), Quaternion.Euler(0f, 180f, 0f), new Vector2(6.4f, 0.56f), new Color(0.12f, 0.24f, 0.52f, 1f));
            CreateIstanbulRedStreetSign(aziziyeRoot.transform, "AZİZİYE CADDESİ", new Vector3(-78f, 0f, -108f), 0f, mats);
            CreateIstanbulRedStreetSign(aziziyeRoot.transform, "AZİZİYE CADDESİ", new Vector3(92f, 0f, -108f), 0f, mats);

            // =========================================================================
            // 9. AVENUE 8: ZÜBEYDE HANIM CADDESİ (71 Zübeyde Sky-Blue Wood-Balcony Houses + Pines!)
            // =========================================================================
            GameObject zubeydeRoot = new GameObject("Avenue_08_ZubeydeHanimCaddesi");
            zubeydeRoot.transform.SetParent(hamRoot.transform, false);

            float[] zubeydeXs = { -122f, -58f, -24f, 14f, 52f, 124f };
            for (int i = 0; i < zubeydeXs.Length; i++)
            {
                PlaceSelmanModel("Hamidiye_ZubeydeBlueHouse.fbx", $"Zubeyde_SouthHouse_{i + 1}", zubeydeRoot.transform,
                    new Vector3(zubeydeXs[i], 0f, -158.0f), faceNorth, Vector3.one,
                    new Vector3(9.6f, 9.2f, 8.8f), new Vector3(0f, 4.6f, 0f), mats);
                CreateTownTree($"Zubeyde_Pine_{i + 1}", zubeydeRoot.transform, new Vector3(zubeydeXs[i] + 7.5f, 0.04f, -152.5f), mats, 1.25f, 960 + i);
            }
            CreateIstanbulRedStreetSign(zubeydeRoot.transform, "ZÜBEYDE HANIM CD.", new Vector3(-78f, 0f, -140f), 0f, mats);
            CreateIstanbulRedStreetSign(zubeydeRoot.transform, "ZÜBEYDE HANIM CD.", new Vector3(92f, 0f, -140f), 0f, mats);

            // =========================================================================
            // 10. WARM NIGHT STREETLAMPS ALONG THE 8 MAIN AVENUES (Mobile-budgeted 16 lamps!)
            // =========================================================================
            Vector3[] avenueLampCoords =
            {
                new Vector3(-40f, 0.04f, 133f), new Vector3(40f, 0.04f, 133f),
                new Vector3(-40f, 0.04f,  91f), new Vector3(40f, 0.04f,  91f),
                new Vector3(-40f, 0.04f,  41f), new Vector3(40f, 0.04f,  41f),
                new Vector3(-40f, 0.04f, -72f), new Vector3(40f, 0.04f, -72f),
                new Vector3(-40f, 0.04f,-109f), new Vector3(40f, 0.04f,-109f),
                new Vector3(-40f, 0.04f,-141f), new Vector3(40f, 0.04f,-141f),
                new Vector3(-143f,0.04f,  40f), new Vector3(-143f,0.04f,-40f),
                new Vector3(104f, 0.04f,  40f), new Vector3(104f, 0.04f,-40f)
            };
            Color warmSodium = new Color(1.0f, 0.80f, 0.48f);
            for (int i = 0; i < avenueLampCoords.Length; i++)
            {
                GameObject lamp = PlaceSelmanModel("Sultanbeyli_StreetLamp.fbx", $"Hamidiye_AveLamp_{i + 1:00}", hamRoot.transform,
                    avenueLampCoords[i], Quaternion.identity, Vector3.one,
                    new Vector3(0.32f, 5.8f, 0.32f), new Vector3(0f, 2.9f, 0f), mats);
                if (lamp != null)
                {
                    CreatePointLamp($"AveLampLight_{i + 1:00}", lamp.transform, avenueLampCoords[i] + new Vector3(0f, 5.8f, 1.4f), warmSodium, 22f, 3.2f);
                }
            }
        }
    }
}
