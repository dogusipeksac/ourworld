using System.Collections.Generic;
using Gridroam.Jobs;
using Gridroam.Player;
using Gridroam.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gridroam.Editor
{
    public static partial class GridroamPrototypeSetup
    {
        /// <summary>
        /// Builds the complete, photo-accurate 1-to-1 recreation of Selman Sokağı
        /// (Hamidiye Mahallesi, Sultanbeyli, İstanbul) from 46 Selman Sokağı (West) through
        /// 48, 50, 52, 54, 58, 62, 63, 66, 73, 81, Nutuk Sokağı, 95 Selman Sokağı &
        /// Abdülkadir Udeh Sokağı (with Hz. Osman Camii & High-Voltage Pylon skyline) —
        /// where EVERY SINGLE HOUSE & SHOP on the street is hollow, furnished & 100% enterable!
        /// </summary>
        private enum SelmanFloorPlan
        {
            Studio_1_0,
            Flat_1_1,
            Family_2_1,
            Grand_3_1
        }

        private static List<TrafficStopLine> BuildSultanbeyliSelmanSokagiWorld(
            Transform envParent,
            MaterialSet mats,
            PrefabSet prefabs)
        {
            _activeBenchPrefab = prefabs.WoodenBench;
            GameObject root = new GameObject("SelmanSokagi_1to1_Complete_AllEnterable");
            root.transform.SetParent(envParent, false);

            List<TrafficStopLine> stopLines = new List<TrafficStopLine>();
            List<SeatInteractable> seats = new List<SeatInteractable>();

            // 1. Complete Selman Sokağı Roadway, Kilit Parke Sidewalks, Yellow-White Corner Curbs & All 5 Real Cross-Streets
            BuildSelmanStreetRoadway(root.transform, mats, prefabs);

            // 2. North Side (West -> East: No:46 Garden, No:48, No:52, No:54, No:63, No:58 Bakkal, No:73 Oto Tamir, No:66 Yellow, Nutuk Sk., No:81 Rose, No:95 Çay Ocağı, East White Villa — ALL ENTERABLE!)
            BuildSelmanNorthSideBuildings(root.transform, mats, prefabs);

            // 3. South Side (West -> East: No:46S Grey-Pink, No:50S Coral, Gürsu Sk. Peach & Yellow Apt, Çiçekçi Sk. Modern & Half-Brick, No:62 Paintable Home, No:66S Cobalt-Door + Hafsa Tesisat, No:81S Powder-Blue Apt, SE Corner Mesh Yard & Hz. Osman Camii — ALL ENTERABLE!)
            BuildSelmanSouthSideBuildings(root.transform, mats, seats);

            // 4. Utility Poles, Sagging Black Street Cables & Iconic High-Voltage Transmission Pylons
            BuildSelmanPolesAndHighVoltageLines(root.transform, mats);

            // 5. Real Street View Props (Sultanbeyli Belediyesi Dumpsters, Kasis Sign, Fire Hydrant, Simitçi, Side Jobs)
            BuildSelmanStreetPropsAndGameplay(root.transform, mats, prefabs, seats);

            // 6. Real Photo-Matched Drivable Parked Cars (All unlocked & crushable!)
            BuildSelmanParkedCars(root.transform, prefabs);

            // 7. Named Sultanbeyli / Hamidiye Neighborhood Citizens with Local Dialogue
            BuildSelmanPedestrians(root.transform, mats, seats);

            // 8. Living Istanbul Streetlamps (NightLight URP Bloom) & Roaming Pettable Neighborhood Cats!
            BuildSelmanStreetLampsAndCats(root.transform, mats);

            // 9. Interactive Gameplay Mechanics: Street Football Pitch, Mahalle ATMs & Grab/Throw Damacana Props!
            BuildSelmanInteractiveMechanics(root.transform, mats);

            // 10. Photo-Matched 1-to-1 Google Street View Expansion: Hamidiye Mahallesi's 8 Main Avenues!
            BuildHamidiyeEightMainAvenues(root.transform, mats, seats);

            // 11. Clean perimeter bounds framing Hamidiye Mahallesi's 8 main avenues & Selman Sokağı core
            BuildSelmanCorridorBoundary(root.transform, mats);

            return stopLines;
        }

        private static void BuildSelmanStreetRoadway(Transform parent, MaterialSet mats, PrefabSet prefabs)
        {
            GameObject roadRoot = new GameObject("SelmanSokagi_Roadway");
            roadRoot.transform.SetParent(parent, false);

            // Complete Selman Sokağı asphalt ribbon: X = -112 .. +124 (236m long), Z = -3.8 .. +3.8 (7.6m wide)
            CreateTiledBox(
                "Selman_AsphaltMain",
                roadRoot.transform,
                new Vector3(6f, 0.012f, 0f),
                new Vector3(238f, 0.024f, 7.6f),
                Quaternion.identity,
                mats.RoadAsphaltMain,
                metersPerTile: 3.4f,
                collider: false);

            // Authentic worn asphalt manhole covers along Selman Sokağı
            float[] manholeXs = { -84f, -44f, -11f, 22f, 58f, 96f };
            for (int i = 0; i < manholeXs.Length; i++)
            {
                GameObject mh = CreatePrimitiveChild($"Manhole_{i}", PrimitiveType.Cylinder, roadRoot.transform,
                    new Vector3(manholeXs[i], 0.026f, i % 2 == 0 ? 1.1f : -1.2f),
                    new Vector3(0.75f, 0.008f, 0.75f), mats.SedanTrimDark, removeCollider: true);
                MarkStaticEnvironment(mh);
            }

            // North Kilit Parke sidewalk broken cleanly around Nutuk Sokağı (X = +56..+63) & Abdülkadir Udeh Sokağı (X = +95..+103)
            (float x0, float x1, bool yellowCurb)[] northSegments =
            {
                (-106f, -89f, false),
                (-81f, -71f, true),    // Yellow-white painted corner curb at Seyyid Kutup & No:48/50!
                (-71f, 55.5f, false),
                (63.0f, 84.0f, false),
                (84.0f, 94.5f, true),  // Yellow-white painted corner curb at No:95 & Abdülkadir Udeh Sk.!
                (103.5f, 118f, true)
            };
            for (int i = 0; i < northSegments.Length; i++)
            {
                float sx0 = northSegments[i].x0;
                float sx1 = northSegments[i].x1;
                CreateTiledBox(
                    $"Selman_KilitParke_North_{i}",
                    roadRoot.transform,
                    new Vector3((sx0 + sx1) * 0.5f, 0.032f, 4.85f),
                    new Vector3(sx1 - sx0, 0.064f, 2.10f),
                    Quaternion.identity,
                    mats.KilitParke,
                    metersPerTile: 1.50f,
                    collider: false);
                BuildCurbRun(roadRoot.transform, $"NorthCurb_{i}", sx0, sx1, 3.82f, mats, northSegments[i].yellowCurb);
            }

            // South Kilit Parke sidewalk broken cleanly around Seyyid Kutup (-85), Gürsu Sk. (-41), Çiçekçi Sk. (-11), Abdülkadir Udeh (+99)
            (float x0, float x1, bool yellowCurb)[] southSegments =
            {
                (-106f, -89f, false),
                (-81f, -71f, true),
                (-71f, -45.5f, false),
                (-36.5f, -14.8f, false),
                (-6.5f, 84.0f, false),
                (84.0f, 94.5f, true),
                (103.5f, 118f, true)
            };
            for (int i = 0; i < southSegments.Length; i++)
            {
                float sx0 = southSegments[i].x0;
                float sx1 = southSegments[i].x1;
                CreateTiledBox(
                    $"Selman_KilitParke_South_{i}",
                    roadRoot.transform,
                    new Vector3((sx0 + sx1) * 0.5f, 0.032f, -4.85f),
                    new Vector3(sx1 - sx0, 0.064f, 2.10f),
                    Quaternion.identity,
                    mats.KilitParke,
                    metersPerTile: 1.50f,
                    collider: false);
                BuildCurbRun(roadRoot.transform, $"SouthCurb_{i}", sx0, sx1, -3.82f, mats, southSegments[i].yellowCurb);
            }

            // All 5 Real Cross-Street Openings surveyed on Google Street View!
            // 1. Seyyid Kutup Caddesi (West cross-street, X = -85)
            CreateTiledBox("CrossStreet_SeyyidKutup", roadRoot.transform,
                new Vector3(-85.0f, 0.015f, 0f), new Vector3(8.2f, 0.030f, 54f),
                Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);
            // 2. Gürsu Sokağı (South Kilit-Parke opening, X = -41)
            CreateTiledBox("SideStreet_GursuSk", roadRoot.transform,
                new Vector3(-41.0f, 0.018f, -15.0f), new Vector3(6.2f, 0.036f, 24f),
                Quaternion.identity, mats.KilitParke, 1.8f, collider: false);
            // 3. Çiçekçi Sokağı (South Kilit-Parke opening, X = -10.6)
            CreateTiledBox("SideStreet_CicekciSk", roadRoot.transform,
                new Vector3(-10.6f, 0.018f, -15.5f), new Vector3(6.8f, 0.036f, 25f),
                Quaternion.identity, mats.KilitParke, 1.8f, collider: false);
            // 4. Nutuk Sokağı (North asphalt opening right beside 66 Yellow House, X = +59.2 — exact from step 1358!)
            CreateTiledBox("SideStreet_NutukSk", roadRoot.transform,
                new Vector3(59.2f, 0.016f, 16.5f), new Vector3(7.0f, 0.032f, 27f),
                Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);
            // Red Fire Hydrant right on the corner of 66 Yellow House & Nutuk Sokağı (exact from step 1358!)
            if (prefabs.FireHydrant != null)
            {
                PlacePrefabInstance(prefabs.FireHydrant, roadRoot.transform, new Vector3(54.8f, 0.04f, 5.2f), Quaternion.identity);
            }
            // 5. Abdülkadir Udeh Sokağı (East cross-street at No:95 leading South toward Hz. Osman Camii, X = +99 — exact from step 1359/1370!)
            CreateTiledBox("CrossStreet_AbdulkadirUdeh", roadRoot.transform,
                new Vector3(99.0f, 0.015f, -10f), new Vector3(8.8f, 0.030f, 84f),
                Quaternion.identity, mats.RoadAsphaltMain, 3.4f, collider: false);

            // Speed bump (Kasis) right across Selman Sokağı near Çiçekçi Sk. corner
            GameObject kasis = CreatePrimitiveChild("Selman_SpeedBump", PrimitiveType.Cube, roadRoot.transform,
                new Vector3(-6.2f, 0.045f, 0f), new Vector3(0.55f, 0.09f, 7.2f), mats.RoadMarkingYellow, removeCollider: false);
            MarkStaticEnvironment(kasis);

            // Authentic Istanbul / Sultanbeyli RED-AND-WHITE Street Signs!
            CreateIstanbulRedStreetSign(roadRoot.transform, "SELMAN SOKAĞI", new Vector3(-79.5f, 0f, 4.3f), 0f, mats);
            CreateIstanbulRedStreetSign(roadRoot.transform, "SEYYİD KUTUP CD.", new Vector3(-80.5f, 0f, -4.3f), 90f, mats);
            CreateIstanbulRedStreetSign(roadRoot.transform, "GÜRSU SOKAĞI", new Vector3(-44.6f, 0f, -4.6f), 180f, mats);
            CreateIstanbulRedStreetSign(roadRoot.transform, "ÇİÇEKÇİ SOKAĞI", new Vector3(-7.0f, 0f, -4.6f), 180f, mats);
            CreateIstanbulRedStreetSign(roadRoot.transform, "NUTUK SOKAĞI", new Vector3(55.0f, 0f, 4.4f), 90f, mats);
            CreateIstanbulRedStreetSign(roadRoot.transform, "ABDÜLKADİR UDEH SK.", new Vector3(94.2f, 0f, 4.3f), 90f, mats);
            CreateIstanbulRedStreetSign(roadRoot.transform, "SELMAN SOKAĞI", new Vector3(104.5f, 0f, 4.3f), 0f, mats);
        }

        private static void BuildCurbRun(Transform parent, string name, float x0, float x1, float z, MaterialSet mats, bool yellowWhitePainted)
        {
            if (!yellowWhitePainted)
            {
                GameObject curb = CreatePrimitiveChild(name, PrimitiveType.Cube, parent,
                    new Vector3((x0 + x1) * 0.5f, 0.048f, z),
                    new Vector3(x1 - x0, 0.096f, 0.20f), mats.Concrete, removeCollider: true);
                MarkStaticEnvironment(curb);
                return;
            }

            float cur = x0;
            int idx = 0;
            while (cur < x1 - 0.1f)
            {
                float segLen = Mathf.Min(1.1f, x1 - cur);
                Material mat = (idx % 2 == 0) ? mats.RoadMarkingYellow : mats.WhitePVC;
                GameObject block = CreatePrimitiveChild($"{name}_{idx}", PrimitiveType.Cube, parent,
                    new Vector3(cur + segLen * 0.5f, 0.052f, z),
                    new Vector3(segLen, 0.104f, 0.22f), mat, removeCollider: true);
                MarkStaticEnvironment(block);
                cur += segLen;
                idx++;
            }
        }

        private static void CreateIstanbulRedStreetSign(Transform parent, string streetName, Vector3 pos, float yawDeg, MaterialSet mats)
        {
            GameObject root = new GameObject($"StreetSign_{streetName}");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));

            CreatePrimitiveChild("Pole", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 1.48f, 0f), new Vector3(0.055f, 1.48f, 0.055f), mats.Galvanized, removeCollider: true);
            CreatePrimitiveChild("Plaque", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 2.82f, 0f), new Vector3(1.62f, 0.36f, 0.05f), mats.SignRed, removeCollider: true);

            CreateWorldSignText(streetName, root.transform, new Vector3(0f, 2.82f, 0.036f), Quaternion.Euler(0f, 180f, 0f),
                new Vector2(1.48f, 0.26f), Color.white);
            CreateWorldSignText(streetName, root.transform, new Vector3(0f, 2.82f, -0.036f), Quaternion.identity,
                new Vector2(1.48f, 0.26f), Color.white);
            MarkStaticEnvironment(root);
        }

        /// <summary>
        /// Places ALL North-side buildings along Selman Sokağı (West -> East) — EVERY SINGLE ONE HOLLOW, FURNISHED & ENTERABLE!
        /// </summary>
        private static void BuildSelmanNorthSideBuildings(Transform parent, MaterialSet mats, PrefabSet prefabs)
        {
            GameObject northRoot = new GameObject("Selman_NorthSide_AllEnterable");
            northRoot.transform.SetParent(parent, false);
            Quaternion faceSouth = Quaternion.Euler(0f, 180f, 0f);

            // 0. 46 SELMAN SOKAĞI WEST GARDEN (X = -98, Z = +10 — rustic timber garden & big trees from step 1357!)
            CreateStaticBlock("No46_TimberFence", northRoot.transform, new Vector3(-98.0f, 0.65f, 6.2f), new Vector3(12.0f, 1.3f, 0.16f), mats.TownDoorDark, addCollider: true);
            CreateTownTree("No46_GardenTree1", northRoot.transform, new Vector3(-96.0f, 0.04f, 8.8f), mats, 1.25f, 461);
            CreateTownTree("No46_GardenTree2", northRoot.transform, new Vector3(-102.0f, 0.04f, 11.2f), mats, 1.15f, 462);

            // 1. 48 SELMAN SOKAĞI HERO HOUSE (Hollow Walk-In + Salon & Kitchen + Emine Teyze!)
            GameObject house48 = PlaceSelmanModel(
                "Sultanbeyli_Selman48_House.fbx", "Selman_No48_HeroHouse",
                northRoot.transform, new Vector3(-68.0f, 0f, 9.55f), faceSouth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (house48 != null)
            {
                AddHollowRoomColliders(house48, -4.2f, 4.6f, -6.4f, 0.80f, 3.35f, 0.24f, doorX: -2.65f, doorW: 1.50f);
                AddLocalBoxCollider(house48, new Vector3(1.4f, 0.9f, 3.55f), new Vector3(6.4f, 1.8f, 0.32f));
                AddLocalBoxCollider(house48, new Vector3(-6.6f, 0.9f, 3.55f), new Vector3(4.4f, 1.8f, 0.32f));
                CreatePrimitiveChild("No48_PlaqueBg", PrimitiveType.Cube, house48.transform,
                    new Vector3(4.65f, 1.48f, 3.81f), new Vector3(0.42f, 0.26f, 0.04f), mats.SignRed, removeCollider: true);
                CreateWorldSignText("NO: 48", house48.transform, new Vector3(4.65f, 1.48f, 3.85f),
                    Quaternion.Euler(0f, 180f, 0f), new Vector2(0.38f, 0.20f), Color.white);

                FurnishSelmanHomeInterior(house48.transform, new Vector3(0.2f, 0.09f, -2.8f), SelmanFloorPlan.Flat_1_1, 8.2f, 6.6f, mats);
                AttachSelmanPaintableAddress(house48, "selman_home_48", "Selman Sokağı No: 48", new Vector3(-68.0f, 0.12f, 4.9f));
                SpawnSelmanResidentNpc(
                    "NPC_EmineTeyze_No48", "Emine Teyze", "48 Selman Sk. Ev Sahibi",
                    house48.transform, new Vector3(-0.6f, 0.09f, -2.2f), Quaternion.Euler(0f, 35f, 0f),
                    BlenderCharacterVariant.CasualFemale, mats.PedShirtBurgundy, mats.CharPants, mats.CharSkin, mats.CharHairBrown,
                    new[]
                    {
                        "Hoş geldin evladım, buyur içeri! Demlikte taze çay var, sehpanın üstüne koydum.",
                        "48 numaranın önündeki asma çardağını rahmetli beyimle dikmiştik, yazın gölgesinde otururuz.",
                        "Hamidiye Mahallesi eskiden hep böyle bahçeli müstakil evlerdi, şimdi arkamızda yeni binalar yükseliyor.",
                        "Pazar günleri torunlarla Sultanbeyli Gölet'e yürüyüşe iniyoruz, havası Aydos'tan serin gelir."
                    },
                    mats);
            }
            CreateTiledBox("No48_Driveway", northRoot.transform, new Vector3(-61.4f, 0.036f, 7.2f),
                new Vector3(4.5f, 0.072f, 4.2f), Quaternion.identity, mats.KilitParke, 1.35f, collider: false);
            if (prefabs.TownCarWhitePickup != null)
            {
                PlacePrefabInstance(prefabs.TownCarWhitePickup, northRoot.transform, new Vector3(-61.4f, 0.22f, 10.2f), faceSouth);
            }

            // Behind No:48: Yellow Apartment (3+1!), Brick Construction & Modern Apartment (3+1!)
            GameObject backYel = PlaceSelmanModel("Sultanbeyli_YellowApartman.fbx", "Selman_No48_Backdrop_YellowApt",
                northRoot.transform, new Vector3(-76.5f, 0f, 21.5f), Quaternion.Euler(0f, 172f, 0f), Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (backYel != null)
            {
                AddHollowRoomColliders(backYel, -5.3f, 5.3f, -4.9f, 4.9f, 3.25f, 0.26f, doorX: 0f, doorW: 1.70f);
                FurnishSelmanHomeInterior(backYel.transform, new Vector3(0f, 0.09f, 0f), SelmanFloorPlan.Grand_3_1, 9.8f, 9.0f, mats);
            }
            PlaceSelmanModel("Sultanbeyli_BrickConstruction.fbx", "Selman_No48_Backdrop_BrickConstruction",
                northRoot.transform, new Vector3(-56.0f, 0f, 20.5f), faceSouth, Vector3.one,
                new Vector3(11.6f, 10.2f, 9.8f), new Vector3(0f, 5.1f, 0f), mats);
            GameObject backMod = PlaceSelmanModel("Sultanbeyli_ModernApartman.fbx", "Selman_No48_Backdrop_ModernApt",
                northRoot.transform, new Vector3(-42.5f, 0f, 22.0f), faceSouth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (backMod != null)
            {
                AddHollowRoomColliders(backMod, -5.9f, 5.9f, -5.1f, 5.1f, 3.30f, 0.26f, doorX: 0f, doorW: 1.80f);
                FurnishSelmanHomeInterior(backMod.transform, new Vector3(0f, 0.09f, 0f), SelmanFloorPlan.Grand_3_1, 10.8f, 9.4f, mats);
            }

            // 2. 52 SELMAN SK. / 1 GÜRSU SK. — 1+1 Daire!
            GameObject no52 = PlaceSelmanModel("Sultanbeyli_HalfBrickHouse.fbx", "Selman_No52_HalfBrickHouse",
                northRoot.transform, new Vector3(-46.5f, 0f, 7.3f), faceSouth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (no52 != null)
            {
                AddHollowRoomColliders(no52, -4.6f, 4.6f, -6.8f, 0.80f, 3.50f, 0.24f, doorX: -2.8f, doorW: 1.55f);
                FurnishSelmanHomeInterior(no52.transform, new Vector3(0.0f, 0.09f, -3.0f), SelmanFloorPlan.Flat_1_1, 8.6f, 7.0f, mats);
                AttachSelmanPaintableAddress(no52, "selman_home_52", "Selman Sokağı No: 52", new Vector3(-46.5f, 0.12f, 4.9f));
            }

            // 3. 54 SELMAN SK. — 1+0 Stüdyo Daire!
            GameObject no54 = PlaceSelmanModel("Sultanbeyli_CoralHouse.fbx", "Selman_No54_CoralHouse",
                northRoot.transform, new Vector3(-32.5f, 0f, 7.6f), faceSouth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (no54 != null)
            {
                AddHollowRoomColliders(no54, -3.8f, 3.8f, -5.8f, 1.00f, 3.55f, 0.22f, doorX: -2.4f, doorW: 1.50f);
                FurnishSelmanHomeInterior(no54.transform, new Vector3(0.0f, 0.09f, -2.4f), SelmanFloorPlan.Studio_1_0, 7.0f, 6.2f, mats);
                AttachSelmanPaintableAddress(no54, "selman_home_54", "Selman Sokağı No: 54", new Vector3(-32.5f, 0.12f, 4.9f));
            }

            // 4. 63 SELMAN SK. — HOLLOW WALK-IN RUSTED STEEL GARAGE + Workshop Interior!
            GameObject no63 = PlaceSelmanModel("Sultanbeyli_RustGarage63.fbx", "Selman_No63_RustGarage",
                northRoot.transform, new Vector3(-16.5f, 0f, 6.8f), faceSouth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (no63 != null)
            {
                AddHollowRoomColliders(no63, -2.8f, 2.8f, -4.8f, 0.40f, 3.30f, 0.18f, doorX: 0f, doorW: 3.30f);
                PlaceInteriorModel("Interior_GarageTools.fbx", "No63_GarageTools", no63.transform, new Vector3(-0.8f, 0.09f, -3.6f), Quaternion.identity, mats, colliderSize: new Vector3(1.8f, 1.1f, 0.8f));
                PlaceInteriorModel("Interior_PlumberBench.fbx", "No63_Bench", no63.transform, new Vector3(1.2f, 0.09f, -3.7f), Quaternion.identity, mats, colliderSize: new Vector3(1.8f, 0.95f, 0.75f));
                CreateInteriorLight("No63_Light", no63.transform, new Vector3(0f, 2.95f, -2.2f), new Color(1.0f, 0.88f, 0.65f), 9f, 2.2f);
            }
            PlaceSelmanModel("Sultanbeyli_PinkApartman.fbx", "Selman_No63_Backdrop_PinkApt",
                northRoot.transform, new Vector3(-9.0f, 0f, 18.2f), faceSouth, Vector3.one,
                new Vector3(8.6f, 10.0f, 9.0f), new Vector3(0f, 5.0f, 0f), mats);

            // 5. 58 SELMAN SK. — FULL REALISTIC 'SELMAN BAKKAL & MANAV' (Manav Reyonu, Şarküteri, Dondurma Dolabı, Kasa Bandı, İçecek Dolabı & Hüseyin Abi!)
            GameObject shop58 = PlaceSelmanModel("Sultanbeyli_Shop58_ArchGate.fbx", "Selman_No58_OrangeShop_ArchGate",
                northRoot.transform, new Vector3(4.5f, 0f, 6.9f), faceSouth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (shop58 != null)
            {
                AddHollowRoomColliders(shop58, 0.5f, 7.5f, -6.2f, 0.60f, 3.50f, 0.22f, doorX: 4.2f, doorW: 2.50f);
                AddLocalBoxCollider(shop58, new Vector3(-2.2f, 1.05f, 0.45f), new Vector3(3.2f, 2.1f, 0.24f));
                CreateWorldSignText("SELMAN BAKKAL & MANAV · 58", shop58.transform, new Vector3(4.0f, 3.05f, 0.86f),
                    Quaternion.Euler(0f, 180f, 0f), new Vector2(6.8f, 0.60f), new Color(0.14f, 0.16f, 0.20f, 1f));

                // Outside-front realistic MANAV REYONU (tomatoes, peppers, oranges, lemons!) — directly interactive!
                GameObject manav = PlaceInteriorModel("Market_ManavReyonu.fbx", "Bakkal_ManavReyonu", shop58.transform,
                    new Vector3(6.2f, 0.04f, 1.25f), Quaternion.identity, mats, colliderSize: new Vector3(2.1f, 1.2f, 0.92f));
                AttachShopSectionTrigger(manav, "shop_manav", "ui.btn.shop", 2.8f);

                // Inside Back Wall: Curved-Glass ŞARKÜTERİ & PEYNİR DOLABI + Stocked Gondola Shelf!
                GameObject sarkuteri = PlaceInteriorModel("Market_SarkuteriDolabi.fbx", "Bakkal_Sarkuteri", shop58.transform,
                    new Vector3(2.4f, 0.09f, -5.3f), Quaternion.identity, mats, colliderSize: new Vector3(2.05f, 1.35f, 0.95f));
                AttachShopSectionTrigger(sarkuteri, "shop_sarkuteri", "ui.btn.shop", 2.8f);

                PlaceInteriorModel("Interior_MarketShelf.fbx", "Bakkal_ShelfBackR", shop58.transform, new Vector3(5.6f, 0.09f, -5.5f), Quaternion.identity, mats, colliderSize: new Vector3(1.9f, 1.8f, 0.5f));
                PlaceInteriorModel("Interior_MarketShelf.fbx", "Bakkal_ShelfSideL", shop58.transform, new Vector3(6.9f, 0.09f, -3.0f), Quaternion.Euler(0f, -90f, 0f), mats, colliderSize: new Vector3(1.9f, 1.8f, 0.5f));

                // Side Wall: Tall Glass İçecek Dolabı + Chest DONDURMA DOLABI (interactive!)
                PlaceInteriorModel("Interior_DrinkFridge.fbx", "Bakkal_DrinkFridge", shop58.transform, new Vector3(1.25f, 0.09f, -3.7f), Quaternion.Euler(0f, 90f, 0f), mats, colliderSize: new Vector3(0.95f, 2.0f, 0.72f));
                GameObject dondurma = PlaceInteriorModel("Market_DondurmaDolabi.fbx", "Bakkal_DondurmaDolabi", shop58.transform,
                    new Vector3(6.7f, 0.09f, -0.9f), Quaternion.Euler(0f, -90f, 0f), mats, colliderSize: new Vector3(1.42f, 0.95f, 0.72f));
                AttachShopSectionTrigger(dondurma, "shop_dondurma", "ui.btn.shop", 2.6f);

                // Checkout Conveyor Belt (Kasa Bandı) w/ POS Monitor & Red Shopping Baskets + Bread Counter!
                GameObject kasa = PlaceInteriorModel("Market_KasaBandi.fbx", "Bakkal_KasaBandi", shop58.transform,
                    new Vector3(2.3f, 0.09f, -1.55f), Quaternion.identity, mats, colliderSize: new Vector3(2.15f, 1.0f, 0.88f));
                AttachShopSectionTrigger(kasa, "shop_bakkal", "ui.btn.shop", 3.0f);

                CreateInteriorLight("Bakkal_ShopLight", shop58.transform, new Vector3(4.0f, 3.10f, -2.8f), new Color(1.0f, 0.97f, 0.92f), 12f, 3.0f);

                SpawnSelmanResidentNpc(
                    "NPC_BakkalHuseyin_No58", "Hüseyin Abi", "Selman Bakkal & Manav · No:58",
                    shop58.transform, new Vector3(2.3f, 0.09f, -2.55f), Quaternion.identity,
                    BlenderCharacterVariant.CasualMale, mats.PedShirtOlive, mats.CharPantsKhaki, mats.CharSkinMedium, mats.CharHair,
                    new[]
                    {
                        "Hoş geldin komşu! Dışarıdaki manavdan taze meyve, arkadaki şarküteriden peynir-sucuk, dolaptan dondurma alabilirsin!",
                        "Aziziye Caddesi'ndeki İTO Şehit Er Dursun Sıvaz İlkokulu dağılınca bütün çocuklar dondurma dolabının başına üşüşür.",
                        "Hamidiye Muhtarlığı'ndan Zübeyde Hanım Caddesi'ne kadar herkes Selman Bakkal & Manav'ı bilir!",
                        "İstersen kasa bandına gel, istersen doğrudan reyonlardan alışveriş yap komşu."
                    },
                    mats, vendorShopId: "shop_bakkal");
            }

            // 6. 73 SELMAN SK. — HOLLOW OPEN-SHUTTER 'OTO TAMİR' BAY!
            GameObject garage73 = PlaceSelmanModel("Sultanbeyli_Garage73_LilacGate.fbx", "Selman_No73_Garage_LilacGate",
                northRoot.transform, new Vector3(24.5f, 0f, 6.8f), faceSouth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (garage73 != null)
            {
                AddHollowRoomColliders(garage73, -3.2f, 3.2f, -5.7f, 0.55f, 3.40f, 0.22f, doorX: 0.0f, doorW: 3.90f);
                AddLocalBoxCollider(garage73, new Vector3(4.85f, 0.88f, 0.45f), new Vector3(3.1f, 1.76f, 0.24f));
                CreateWorldSignText("OTO TAMİR · NO:73", garage73.transform, new Vector3(0f, 3.08f, 0.84f),
                    Quaternion.Euler(0f, 180f, 0f), new Vector2(3.8f, 0.34f), new Color(0.12f, 0.12f, 0.14f, 1f));
                PlaceInteriorModel("Interior_GarageTools.fbx", "Garage73_Tools", garage73.transform, new Vector3(-1.4f, 0.09f, -4.4f), Quaternion.identity, mats, colliderSize: new Vector3(1.8f, 1.1f, 0.8f));
                PlaceInteriorModel("Interior_PlumberBench.fbx", "Garage73_Bench", garage73.transform, new Vector3(1.5f, 0.09f, -4.5f), Quaternion.identity, mats, colliderSize: new Vector3(1.8f, 0.95f, 0.75f));
                CreateInteriorLight("Lamp_Garage73_Inside", garage73.transform, new Vector3(0f, 3.05f, -2.6f), new Color(1f, 0.86f, 0.58f), 11f, 2.8f);
                CreatePointLamp("Lamp_Garage73", northRoot.transform, new Vector3(24.5f, 3.6f, 5.5f), new Color(1f, 0.80f, 0.45f), 16f, 2.4f);
            }

            // 7. 66 SELMAN SOKAĞI — HOLLOW WALK-IN YELLOW 2-STORY HOUSE + Emekli Mehmet Amca!
            GameObject house66 = PlaceSelmanModel("Sultanbeyli_House66_Yellow.fbx", "Selman_No66_YellowHouse",
                northRoot.transform, new Vector3(45.5f, 0f, 9.1f), faceSouth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (house66 != null)
            {
                AddHollowRoomColliders(house66, -2.2f, 4.2f, -6.0f, 0.40f, 3.45f, 0.22f, doorX: -0.75f, doorW: 1.50f);
                AddLocalBoxCollider(house66, new Vector3(2.6f, 0.9f, 3.10f), new Vector3(5.4f, 1.8f, 0.28f));
                AddLocalBoxCollider(house66, new Vector3(-4.8f, 0.9f, 3.10f), new Vector3(5.2f, 1.8f, 0.28f));
                CreateWorldSignText("SELMAN SK. 66", house66.transform, new Vector3(0.45f, 2.45f, 0.48f),
                    Quaternion.Euler(0f, 180f, 0f), new Vector2(0.46f, 0.16f), Color.white);

                FurnishSelmanHomeInterior(house66.transform, new Vector3(1.0f, 0.09f, -2.8f), SelmanFloorPlan.Family_2_1, 6.0f, 6.0f, mats);
                AttachSelmanPaintableAddress(house66, "selman_home_66", "Selman Sokağı No: 66 (Sarı Ev)", new Vector3(45.5f, 0.12f, 4.9f));
                SpawnSelmanResidentNpc(
                    "NPC_MehmetAmca_No66", "Mehmet Amca", "66 Selman Sk. Emekli Sakini",
                    house66.transform, new Vector3(-0.7f, 0.09f, -1.6f), Quaternion.identity,
                    BlenderCharacterVariant.CasualMale, mats.CharShirt, mats.CharPants, mats.CharSkinWarm, mats.CharHair,
                    new[]
                    {
                        "Hoş geldin delikanlı, geç otur! 30 yıldır bu sarı evde otururum, Hamidiye'nin her taşını bilirim.",
                        "Sabah namazına aşağıda Abdülkadir Udeh'in sonundaki Hz. Osman Camii'ne yürürüm.",
                        "Karşıdaki Hafsa Tesisat'taki Mahmut Usta geçen kış bizim petekleri temizledi, ev hamam gibi oldu.",
                        "Eskiden buralar hep Aydos Ormanı'na bakan tarlaydı, şimdi koca Sultanbeyli oldu."
                    },
                    mats);
            }

            // 8. 81 SELMAN SOKAĞI — HOLLOW WALK-IN DUSTY-ROSE GARDEN HOUSE (right after Nutuk Sokağı mouth!)
            GameObject no81 = PlaceSelmanModel("Sultanbeyli_House81_Rose.fbx", "Selman_No81_RoseHouse",
                northRoot.transform, new Vector3(71.5f, 0f, 9.6f), faceSouth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (no81 != null)
            {
                AddHollowRoomColliders(no81, -4.6f, 4.6f, -8.6f, -0.40f, 3.25f, 0.24f, doorX: 0f, doorW: 1.60f);
                AddLocalBoxCollider(no81, new Vector3(-6.8f, 1.25f, -0.4f), new Vector3(3.8f, 2.5f, 5.4f));
                FurnishSelmanHomeInterior(no81.transform, new Vector3(0f, 0.09f, -4.5f), SelmanFloorPlan.Family_2_1, 8.6f, 7.6f, mats);
                AttachSelmanPaintableAddress(no81, "selman_home_81n", "Selman Sokağı No: 81", new Vector3(71.5f, 0.12f, 4.9f));
            }
            CreateTownTree("Selman_No81_FrontYardTree", northRoot.transform, new Vector3(75.5f, 0.04f, 7.8f), mats, 1.15f, 811);

            // 9. 95 SELMAN SOKAĞI — NEW HOLLOW WALK-IN CORAL CORNER ÇAY OCAĞI (Abdülkadir Udeh Sk. corner — from step 1360!)
            GameObject no95 = PlaceSelmanModel("Sultanbeyli_CornerShop95.fbx", "Selman_No95_CornerCayOcagi",
                northRoot.transform, new Vector3(87.5f, 0f, 7.2f), faceSouth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (no95 != null)
            {
                AddHollowRoomColliders(no95, -4.2f, 4.2f, -5.6f, 0.80f, 3.35f, 0.22f, doorX: 0.8f, doorW: 2.30f);
                AddLocalBoxCollider(no95, new Vector3(-6.6f, 1.05f, -1.6f), new Vector3(4.6f, 2.1f, 4.8f));
                CreateWorldSignText("SELMAN ÇAY OCAĞI · 95", no95.transform, new Vector3(0.8f, 2.95f, 0.90f),
                    Quaternion.Euler(0f, 180f, 0f), new Vector2(4.6f, 0.42f), Color.white);
                PlaceInteriorModel("Interior_MarketCounter.fbx", "CayOcagi_Counter", no95.transform, new Vector3(-1.8f, 0.09f, -4.4f), Quaternion.identity, mats, colliderSize: new Vector3(2.0f, 1.0f, 0.72f));
                PlaceInteriorModel("Interior_DrinkFridge.fbx", "CayOcagi_Fridge", no95.transform, new Vector3(-3.3f, 0.09f, -2.2f), Quaternion.Euler(0f, 90f, 0f), mats, colliderSize: new Vector3(0.95f, 2.0f, 0.72f));
                PlaceInteriorModel("Interior_TurkishTeaTable.fbx", "CayOcagi_Table1", no95.transform, new Vector3(1.6f, 0.09f, -2.6f), Quaternion.identity, mats, colliderSize: new Vector3(1.1f, 0.55f, 0.65f));
                PlaceInteriorModel("Interior_TurkishTeaTable.fbx", "CayOcagi_Table2", no95.transform, new Vector3(1.6f, 0.09f, -4.2f), Quaternion.identity, mats, colliderSize: new Vector3(1.1f, 0.55f, 0.65f));
                CreateInteriorLight("CayOcagi_Light", no95.transform, new Vector3(0f, 3.0f, -2.4f), new Color(1.0f, 0.92f, 0.75f), 10f, 2.6f);

                SpawnSelmanResidentNpc(
                    "NPC_CayciRiza_No95", "Rıza Abi", "95 Selman Çay Ocağı",
                    no95.transform, new Vector3(-1.8f, 0.09f, -3.2f), Quaternion.identity,
                    BlenderCharacterVariant.CasualMale, mats.PedShirtMustard, mats.CharPants, mats.CharSkinMedium, mats.CharHair,
                    new[]
                    {
                        "Buyur geç, demli çay yeni demlendi! 95 numara Abdülkadir Udeh köşesinde sokağın buluşma noktasıdır.",
                        "Karşıdaki beyaz evin ötesinden doğrudan Mürşit Caddesi ve Sultanbeyli merkeze çıkılır.",
                        "Aşağı Abdülkadir Udeh Sokağı'ndan baktın mı Hz. Osman Camii'nin çift minaresi hemen görünür."
                    },
                    mats, vendorShopId: "shop_coffee");
            }

            // 10. EAST CORNER WHITE 2-STORY VILLA across Abdülkadir Udeh Sk. (from step 1359 — HOLLOW WALK-IN!)
            GameObject whiteVilla = PlaceSelmanModel("Sultanbeyli_WhiteVillaEast.fbx", "Selman_EastCorner_WhiteVilla",
                northRoot.transform, new Vector3(110.5f, 0f, 10.4f), faceSouth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (whiteVilla != null)
            {
                AddHollowRoomColliders(whiteVilla, -5.2f, 5.2f, -6.4f, 1.20f, 3.25f, 0.24f, doorX: 0f, doorW: 1.70f);
                AddLocalBoxCollider(whiteVilla, new Vector3(3.8f, 0.92f, 4.1f), new Vector3(4.2f, 1.84f, 0.28f));
                AddLocalBoxCollider(whiteVilla, new Vector3(-3.8f, 0.92f, 4.1f), new Vector3(4.2f, 1.84f, 0.28f));
                FurnishSelmanHomeInterior(whiteVilla.transform, new Vector3(0f, 0.09f, -2.6f), SelmanFloorPlan.Grand_3_1, 9.8f, 7.2f, mats);
            }
        }

        /// <summary>
        /// Places ALL South-side buildings along Selman Sokağı (West -> East) — EVERY SINGLE ONE HOLLOW, FURNISHED & ENTERABLE,
        /// plus real Hz. Osman Camii rising down Abdülkadir Udeh Sokağı!
        /// </summary>
        private static void BuildSelmanSouthSideBuildings(Transform parent, MaterialSet mats, List<SeatInteractable> seats)
        {
            GameObject southRoot = new GameObject("Selman_SouthSide_AllEnterable");
            southRoot.transform.SetParent(parent, false);
            Quaternion faceNorth = Quaternion.identity;

            // 0. 46 SELMAN SK. SOUTH — 1+1 Daire!
            GameObject no46S = PlaceSelmanModel("Sultanbeyli_GreyPinkHouse46.fbx", "Selman_No46S_GreyPinkHouse",
                southRoot.transform, new Vector3(-96.0f, 0f, -8.2f), faceNorth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (no46S != null)
            {
                AddHollowRoomColliders(no46S, -4.5f, 4.5f, -6.5f, 0.80f, 3.10f, 0.24f, doorX: -1.6f, doorW: 1.50f);
                FurnishSelmanHomeInterior(no46S.transform, new Vector3(0.0f, 0.09f, -2.85f), SelmanFloorPlan.Flat_1_1, 8.4f, 6.8f, mats);
            }

            // 1. 50 SELMAN SK. SOUTH — 1+0 Stüdyo Bahçe Evi!
            CreateStaticBlock("No50S_CorrugatedFenceL", southRoot.transform, new Vector3(-74.0f, 0.95f, -6.4f), new Vector3(5.2f, 1.9f, 0.16f), mats.Galvanized, addCollider: true);
            CreateStaticBlock("No50S_CorrugatedFenceR", southRoot.transform, new Vector3(-64.5f, 0.95f, -6.4f), new Vector3(5.2f, 1.9f, 0.16f), mats.Galvanized, addCollider: true);
            GameObject no50S = PlaceSelmanModel("Sultanbeyli_CoralHouse.fbx", "Selman_No50S_GardenHouse",
                southRoot.transform, new Vector3(-69.0f, 0f, -10.8f), faceNorth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (no50S != null)
            {
                AddHollowRoomColliders(no50S, -3.8f, 3.8f, -5.8f, 1.00f, 3.55f, 0.22f, doorX: -2.4f, doorW: 1.50f);
                FurnishSelmanHomeInterior(no50S.transform, new Vector3(0.0f, 0.09f, -2.4f), SelmanFloorPlan.Studio_1_0, 7.0f, 6.2f, mats);
            }

            // 2. GÜRSU SOKAĞI CORNER (X = -41): 1+1 Peach Corner House (West) + 3+1 Yellow Apt (East)!
            GameObject peachCorner = PlaceSelmanModel("Sultanbeyli_PeachCornerHouse.fbx", "Selman_GursuCorner_PeachHouse",
                southRoot.transform, new Vector3(-51.5f, 0f, -7.8f), faceNorth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (peachCorner != null)
            {
                AddHollowRoomColliders(peachCorner, -4.7f, 4.7f, -6.1f, 1.10f, 3.65f, 0.24f, doorX: 0f, doorW: 1.60f);
                FurnishSelmanHomeInterior(peachCorner.transform, new Vector3(0f, 0.09f, -2.5f), SelmanFloorPlan.Flat_1_1, 8.8f, 6.8f, mats);
            }
            GameObject gursuEast = PlaceSelmanModel("Sultanbeyli_YellowApartman.fbx", "Selman_GursuCorner_EastApt",
                southRoot.transform, new Vector3(-31.2f, 0f, -11.5f), faceNorth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (gursuEast != null)
            {
                AddHollowRoomColliders(gursuEast, -5.3f, 5.3f, -4.9f, 4.9f, 3.25f, 0.26f, doorX: 0f, doorW: 1.70f);
                FurnishSelmanHomeInterior(gursuEast.transform, new Vector3(0f, 0.09f, 0f), SelmanFloorPlan.Grand_3_1, 9.8f, 9.0f, mats);
            }

            // 3. ÇİÇEKÇİ SOKAĞI JUNCTION (X = -10.6):
            // West corner (-20.2): 3+1 Modern Apartment!
            GameObject cicekciWest = PlaceSelmanModel("Sultanbeyli_ModernApartman.fbx", "Selman_CicekciCorner_WestApt",
                southRoot.transform, new Vector3(-20.2f, 0f, -11.8f), faceNorth, new Vector3(0.84f, 0.85f, 0.88f),
                Vector3.zero, Vector3.zero, mats);
            if (cicekciWest != null)
            {
                AddHollowRoomColliders(cicekciWest, -5.9f, 5.9f, -5.1f, 5.1f, 3.30f, 0.26f, doorX: 0f, doorW: 1.80f);
                FurnishSelmanHomeInterior(cicekciWest.transform, new Vector3(0f, 0.09f, 0f), SelmanFloorPlan.Grand_3_1, 10.8f, 9.4f, mats);
            }
            // East corner (+3.0): Split yellow garden wall (walk-in opening!) + 2+1 Half-Brick House!
            CreateStaticBlock("CicekciCorner_YellowWallL", southRoot.transform, new Vector3(-1.8f, 0.78f, -6.4f), new Vector3(4.5f, 1.56f, 0.24f), mats.YellowPlaster, addCollider: true);
            CreateStaticBlock("CicekciCorner_YellowWallR", southRoot.transform, new Vector3(6.8f, 0.78f, -6.4f), new Vector3(4.5f, 1.56f, 0.24f), mats.YellowPlaster, addCollider: true);
            GameObject cicekciEast = PlaceSelmanModel("Sultanbeyli_HalfBrickHouse.fbx", "Selman_CicekciCorner_BrickHouse",
                southRoot.transform, new Vector3(3.0f, 0f, -9.8f), faceNorth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (cicekciEast != null)
            {
                AddHollowRoomColliders(cicekciEast, -4.6f, 4.6f, -6.8f, 0.80f, 3.50f, 0.24f, doorX: -2.8f, doorW: 1.55f);
                FurnishSelmanHomeInterior(cicekciEast.transform, new Vector3(0.0f, 0.09f, -3.0f), SelmanFloorPlan.Family_2_1, 8.6f, 7.0f, mats);
            }

            // 4. 62 SELMAN SOKAĞI — Full Enterable & Paintable Neighborhood Home!
            BuildEnterableResidentialHouse(
                "Selman_EnterableHome_No62", "selman_home_62", "Selman Sokağı No: 62",
                southRoot.transform, new Vector3(21.5f, 0f, -12.5f), Quaternion.Euler(0f, -90f, 0f),
                10.2f, 11.0f, mats.PeachPlaster, hasChimney: true, hasGardenTree: false, mats, houseSeed: 7062);

            // 5. 66 SELMAN SOKAĞI SOUTH — BOTH Cobalt-Blue Door House AND HAFSA TESİSAT Workshop 100% HOLLOW WALK-IN!
            GameObject house66S = PlaceSelmanModel("Sultanbeyli_House66South_Hafsa.fbx", "Selman_No66S_CobaltDoor_HafsaTesisat",
                southRoot.transform, new Vector3(45.5f, 0f, -7.8f), faceNorth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (house66S != null)
            {
                // Hollow colliders for Cobalt-Blue Door Family House (2+1 Aile Evi!)
                AddHollowRoomColliders(house66S, -2.2f, 5.6f, -6.0f, 0.80f, 3.15f, 0.22f, doorX: 0.55f, doorW: 1.55f);
                FurnishSelmanHomeInterior(house66S.transform, new Vector3(1.7f, 0.09f, -2.6f), SelmanFloorPlan.Family_2_1, 7.2f, 6.2f, mats);

                // Hollow colliders for HAFSA TESİSAT Workshop (X = -8.6..-3.8, open wide door at X = -6.2!)
                AddHollowRoomColliders(house66S, -8.6f, -3.8f, -4.6f, 0.60f, 3.10f, 0.20f, doorX: -6.2f, doorW: 2.50f);
                CreateWorldSignText("HAFSA TESİSAT", house66S.transform, new Vector3(-6.2f, 2.72f, 0.72f),
                    Quaternion.Euler(0f, 180f, 0f), new Vector2(3.8f, 0.48f), new Color(0.06f, 0.24f, 0.62f, 1f));

                PlaceInteriorModel("Interior_KombiDisplay.fbx", "Hafsa_KombiDisplay", house66S.transform, new Vector3(-6.2f, 0.09f, -4.1f), Quaternion.identity, mats, colliderSize: new Vector3(2.1f, 2.0f, 0.45f));
                PlaceInteriorModel("Interior_PlumberBench.fbx", "Hafsa_PlumberBench", house66S.transform, new Vector3(-7.4f, 0.09f, -1.8f), Quaternion.Euler(0f, 90f, 0f), mats, colliderSize: new Vector3(1.8f, 0.95f, 0.75f));
                PlaceInteriorModel("Interior_GarageTools.fbx", "Hafsa_ToolRack", house66S.transform, new Vector3(-4.6f, 0.09f, -2.2f), Quaternion.Euler(0f, -90f, 0f), mats, colliderSize: new Vector3(1.5f, 1.1f, 0.7f));
                CreateInteriorLight("Hafsa_ShopLight", house66S.transform, new Vector3(-6.2f, 2.85f, -2.0f), new Color(0.95f, 0.98f, 1.0f), 10f, 2.6f);

                GameObject hafsaTrig = new GameObject("HafsaTesisat_Interact");
                hafsaTrig.transform.SetParent(house66S.transform, false);
                hafsaTrig.transform.localPosition = new Vector3(-6.2f, 0.1f, -0.8f);
                InteractableTrigger trig = hafsaTrig.AddComponent<InteractableTrigger>();
                trig.Configure(InteractableKind.ShopCounter, "shop_hafsa", "ui.btn.shop", 3.4f);

                SpawnSelmanResidentNpc(
                    "NPC_MahmutUsta_Hafsa", "Mahmut Usta", "Hafsa Tesisat · Kombi & Sıhhi Tesisat",
                    house66S.transform, new Vector3(-6.2f, 0.09f, -2.4f), Quaternion.identity,
                    BlenderCharacterVariant.Worker, mats.CharShirt, mats.CharPantsDenim, mats.CharSkinMedium, mats.CharHair,
                    new[]
                    {
                        "Buyur kardeşim, Hafsa Tesisat'a hoş geldin! Sıhhi tesisat, doğalgaz, kombi bakımı ve petek temizliği bizde.",
                        "Ocak soğuklarında Aydos'tan esen rüzgarla Hamidiye'de kombiler hiç durmaz, yetişmeye çalışıyoruz.",
                        "48 numaranın arkasındaki tuğla inşaatın bütün kalorifer tesisatını da haftaya biz döşeyeceğiz.",
                        "Aracın için acil yakıt bidonu ya da malzeme lazımsa tezgahtan verebilirim."
                    },
                    mats, vendorShopId: "shop_hafsa");
            }

            // 6. 81 SELMAN SOKAĞI SOUTH — HOLLOW WALK-IN POWDER-BLUE APARTMENT + Open Black Double Gate!
            GameObject blue81 = PlaceSelmanModel("Sultanbeyli_BlueApartman81.fbx", "Selman_No81S_PowderBlueApt",
                southRoot.transform, new Vector3(71.5f, 0f, -10.5f), faceNorth, Vector3.one,
                Vector3.zero, Vector3.zero, mats);
            if (blue81 != null)
            {
                AddHollowRoomColliders(blue81, -4.4f, 4.4f, -7.3f, 0.90f, 3.25f, 0.24f, doorX: 0f, doorW: 1.70f);
                // Split cream courtyard wall leaving open black double gate completely clear to walk through!
                AddLocalBoxCollider(blue81, new Vector3(4.5f, 1.05f, 4.2f), new Vector3(3.4f, 2.1f, 0.30f));
                AddLocalBoxCollider(blue81, new Vector3(-4.8f, 1.05f, 4.2f), new Vector3(4.2f, 2.1f, 0.30f));
                FurnishSelmanHomeInterior(blue81.transform, new Vector3(0f, 0.09f, -3.2f), SelmanFloorPlan.Grand_3_1, 8.2f, 7.6f, mats);
                AttachSelmanPaintableAddress(blue81, "selman_home_81s", "Selman Sokağı No: 81 Güney", new Vector3(71.5f, 0.12f, -4.9f));
            }

            // 7. SOUTH-EAST CORNER (2+1 Aile Evi!)
            CreateStaticBlock("SECorner_GreenMeshL", southRoot.transform, new Vector3(84.2f, 1.05f, -6.2f), new Vector3(4.6f, 2.1f, 0.14f), mats.MeshGreen, addCollider: true);
            CreateStaticBlock("SECorner_GreenMeshR", southRoot.transform, new Vector3(92.0f, 1.05f, -6.2f), new Vector3(4.6f, 2.1f, 0.14f), mats.MeshGreen, addCollider: true);
            GameObject seHouse = PlaceSelmanModel("Sultanbeyli_GreyPinkHouse46.fbx", "Selman_SECorner_ConcreteHouse",
                southRoot.transform, new Vector3(88.0f, 0f, -11.5f), faceNorth, new Vector3(0.92f, 0.72f, 0.92f),
                Vector3.zero, Vector3.zero, mats);
            if (seHouse != null)
            {
                AddHollowRoomColliders(seHouse, -4.5f, 4.5f, -6.5f, 0.80f, 3.10f, 0.24f, doorX: -1.6f, doorW: 1.50f);
                FurnishSelmanHomeInterior(seHouse.transform, new Vector3(0.0f, 0.09f, -2.85f), SelmanFloorPlan.Family_2_1, 8.4f, 6.8f, mats);
            }

            // 8. FULL WALK-IN HZ. OSMAN CAMİİ COMPLEX down Abdülkadir Udeh Sokağı
            // (Hollow Domed Sanctuary + Green Saf Carpet + Mihrab + Minber + Chandelier + Şadırvan Abdest + Interactive Namaz!)
            BuildHzOsmanCamiiEnterable(southRoot.transform, mats, seats);

            var (s1, s2) = CreateBench("Selman_PocketBench", southRoot.transform, new Vector3(13.5f, 0.04f, -6.4f), Quaternion.identity, mats);
            seats.Add(s1);
            seats.Add(s2);
            CreateTownTree("Selman_SouthTree_01", southRoot.transform, new Vector3(32.5f, 0.04f, -7.8f), mats, 0.96f, 601);
            CreateTownTree("Selman_SouthTree_02", southRoot.transform, new Vector3(58.5f, 0.04f, -8.5f), mats, 1.04f, 602);
        }

        private static void BuildHzOsmanCamiiEnterable(Transform parent, MaterialSet mats, List<SeatInteractable> seats)
        {
            GameObject camiiComplex = new GameObject("HzOsmanCamii_EnterableComplex");
            camiiComplex.transform.SetParent(parent, false);

            // Travertine Mosque Courtyard connecting seamlessly from Abdülkadir Udeh Sokağı (X = 94.6) to the North portal
            CreateTiledBox(
                "HzOsman_CourtyardPaving",
                camiiComplex.transform,
                new Vector3(82.3f, 0.02f, -37.5f),
                new Vector3(24.6f, 0.04f, 31.0f),
                Quaternion.identity,
                mats.Concrete,
                metersPerTile: 2.2f,
                collider: false);

            // Main Hollow Walk-In Sanctuary (portal faces North +Z toward Selman Sokağı, Mihrab on South -Z Kıble wall!)
            GameObject mosqueRoot = new GameObject("HzOsmanCamii_Sanctuary");
            mosqueRoot.transform.SetParent(camiiComplex.transform, false);
            mosqueRoot.transform.SetPositionAndRotation(new Vector3(82.0f, 0f, -43.5f), Quaternion.identity);

            InstantiateBlenderModel("Sultanbeyli_HzOsmanCamii.fbx", "HzOsman_ShellMesh", mosqueRoot.transform, mats);
            InstantiateBlenderModel("Mosque_InteriorProps.fbx", "HzOsman_InteriorProps", mosqueRoot.transform, mats);

            // Hollow room perimeter walls leaving the wide 3.35m arched front entrance completely walk-in!
            AddHollowRoomColliders(
                mosqueRoot,
                xMin: -7.8f,
                xMax: 7.8f,
                zBack: -7.8f,
                zFront: 7.8f,
                height: 6.6f,
                wallThick: 0.45f,
                doorX: 0f,
                doorW: 3.35f,
                indoorMaxDistance: 3.25f,
                lightRange: 26f);

            // Solid walkable sanctuary floor slab (top at y = 0.16m matching the emerald carpet) + smooth entry step ramp
            AddLocalBoxCollider(mosqueRoot, new Vector3(0f, 0.08f, 0f), new Vector3(15.6f, 0.16f, 15.6f));
            AddLocalBoxCollider(mosqueRoot, new Vector3(0f, 0.04f, 8.35f), new Vector3(4.4f, 0.08f, 1.3f));

            // Colliders for the 4 interior marble columns, Minber & Vaaz Kürsüsü so players walk neatly around them
            AddLocalBoxCollider(mosqueRoot, new Vector3(-3.8f, 3.0f, -3.8f), new Vector3(0.75f, 6.0f, 0.75f));
            AddLocalBoxCollider(mosqueRoot, new Vector3(3.8f, 3.0f, -3.8f), new Vector3(0.75f, 6.0f, 0.75f));
            AddLocalBoxCollider(mosqueRoot, new Vector3(-3.8f, 3.0f, 3.8f), new Vector3(0.75f, 6.0f, 0.75f));
            AddLocalBoxCollider(mosqueRoot, new Vector3(3.8f, 3.0f, 3.8f), new Vector3(0.75f, 6.0f, 0.75f));
            AddLocalBoxCollider(mosqueRoot, new Vector3(-4.2f, 1.4f, -4.8f), new Vector3(1.1f, 2.8f, 3.4f));
            AddLocalBoxCollider(mosqueRoot, new Vector3(4.4f, 1.0f, -4.8f), new Vector3(1.1f, 2.0f, 1.1f));

            // Warm Ottoman Chandelier & Mihrab interior accent lighting
            CreateInteriorLight("HzOsman_ChandelierLight", mosqueRoot.transform, new Vector3(0f, 4.3f, 0f), new Color(1.0f, 0.90f, 0.70f), 22f, 3.0f);
            CreateInteriorLight("HzOsman_MihrabLight", mosqueRoot.transform, new Vector3(0f, 3.4f, -5.2f), new Color(1.0f, 0.94f, 0.78f), 14f, 2.4f);

            // Crisp Turkish Mosque Sign Plaque above the North Arched Portal
            CreateWorldSignText("HZ. OSMAN CAMİİ", mosqueRoot.transform,
                new Vector3(0f, 4.45f, 8.12f), Quaternion.Euler(0f, 180f, 0f),
                new Vector2(3.6f, 0.48f), new Color(0.11f, 0.34f, 0.22f, 1f));

            // 4 Interactive Namaz Safı prayer spots along the emerald saf rows facing South (-Z = Mihrab / Kıble!)
            Quaternion faceMihrab = Quaternion.Euler(0f, 180f, 0f);
            Vector3[] safSpots =
            {
                new Vector3(0.0f, 0.17f, -3.5f),
                new Vector3(-3.0f, 0.17f, -3.5f),
                new Vector3(3.0f, 0.17f, -3.5f),
                new Vector3(0.0f, 0.17f, 0.3f)
            };
            for (int i = 0; i < safSpots.Length; i++)
            {
                GameObject safGo = new GameObject($"HzOsman_NamazSafi_{i + 1}");
                safGo.transform.SetParent(mosqueRoot.transform, false);
                safGo.transform.localPosition = safSpots[i];
                safGo.transform.localRotation = faceMihrab;

                MosquePrayerInteractable prayer = safGo.AddComponent<MosquePrayerInteractable>();
                prayer.Configure(MosqueInteractionKind.PrayerSaf, Vector3.zero, 0f);

                InteractableTrigger prayTrig = safGo.AddComponent<InteractableTrigger>();
                prayTrig.Configure(InteractableKind.MosquePrayer, $"mosque_saf_{i + 1}", "ui.btn.pray", i == 0 || i == 3 ? 3.1f : 2.6f);
            }

            MarkStaticEnvironment(mosqueRoot);

            // Courtyard Octagonal Marble Şadırvan (Ablution Fountain) directly in front of the portal
            GameObject sadirvanGo = PlaceSelmanModel(
                "Mosque_Sadirvan.fbx",
                "HzOsman_Sadirvan",
                camiiComplex.transform,
                new Vector3(82.0f, 0.04f, -28.0f),
                Quaternion.identity,
                Vector3.one,
                new Vector3(2.8f, 2.2f, 2.8f),
                new Vector3(0f, 1.1f, 0f),
                mats);
            if (sadirvanGo != null)
            {
                MosquePrayerInteractable abdest = sadirvanGo.AddComponent<MosquePrayerInteractable>();
                abdest.Configure(MosqueInteractionKind.SadirvanAbdest);

                InteractableTrigger abdestTrig = sadirvanGo.AddComponent<InteractableTrigger>();
                abdestTrig.Configure(InteractableKind.MosquePrayer, "mosque_sadirvan", "ui.btn.abdest", 3.5f);
            }

            // Courtyard Imam Ahmet Hoca welcoming visitors
            SpawnSelmanResidentNpc(
                "NPC_ImamAhmetHoca", "Ahmet Hoca", "Hz. Osman Camii İmam Hatibi",
                camiiComplex.transform, new Vector3(78.2f, 0.04f, -33.2f), Quaternion.Euler(0f, 35f, 0f),
                BlenderCharacterVariant.CasualMale, mats.SedanTrimDark, mats.CharPants, mats.CharSkin, mats.WhitePVC,
                new[]
                {
                    "Selâmün aleyküm evladım, Hz. Osman Camii'ne hoş geldin!",
                    "Avludaki mermer şadırvanda abdestini alıp içeri buyurabilir, yeşil saflarda huzurla namazını kılabilirsin.",
                    "Abdülkadir Udeh ile Selman Sokağı'nın tüm komşuları beş vakit burada buluşur, kapımız herkese her zaman açıktır.",
                    "Allah ibadetlerini kabul etsin, gönlün daim ferah olsun."
                },
                mats);

            // Courtyard benches & trees
            var (mb1, mb2) = CreateBench("HzOsman_CourtyardBench_W", camiiComplex.transform, new Vector3(74.2f, 0.04f, -28.0f), Quaternion.Euler(0f, 90f, 0f), mats);
            seats.Add(mb1);
            seats.Add(mb2);
            var (mb3, mb4) = CreateBench("HzOsman_CourtyardBench_E", camiiComplex.transform, new Vector3(90.0f, 0.04f, -28.0f), Quaternion.Euler(0f, -90f, 0f), mats);
            seats.Add(mb3);
            seats.Add(mb4);

            CreateTownTree("HzOsman_Tree_NW", camiiComplex.transform, new Vector3(72.0f, 0.04f, -24.5f), mats, 1.08f, 701);
            CreateTownTree("HzOsman_Tree_NE", camiiComplex.transform, new Vector3(91.5f, 0.04f, -24.5f), mats, 1.05f, 702);
            CreateTownTree("HzOsman_Tree_SW", camiiComplex.transform, new Vector3(71.5f, 0.04f, -49.5f), mats, 1.12f, 703);

            // Glowing NightLight Kandil / Şerefe warm beacons on both Hz. Osman Camii minarets!
            Vector3[] minaretBeacons = { new Vector3(73.2f, 11.4f, -35.8f), new Vector3(90.8f, 11.4f, -35.8f) };
            for (int m = 0; m < minaretBeacons.Length; m++)
            {
                GameObject beaconGo = new GameObject($"HzOsman_MinaretKandil_{m + 1}");
                beaconGo.transform.SetParent(camiiComplex.transform, false);
                beaconGo.transform.position = minaretBeacons[m];
                Light bl = beaconGo.AddComponent<Light>();
                bl.type = LightType.Point;
                bl.color = new Color(1.0f, 0.80f, 0.42f);
                bl.range = 18f;
                bl.intensity = 3.6f;
                bl.shadows = LightShadows.None;
                bl.enabled = false;
                NightLight nl = beaconGo.AddComponent<NightLight>();
                nl.Configure(bl, null, new Color(1.0f, 0.80f, 0.42f) * 2.5f);
            }
        }

        private static void BuildSelmanStreetLampsAndCats(Transform parent, MaterialSet mats)
        {
            GameObject livingRoot = new GameObject("Selman_LivingStreetLamps_And_Cats");
            livingRoot.transform.SetParent(parent, false);

            // 1. 12 Authentic AYEDAŞ Galvanized Streetlamps with warm NightLight pools along Selman Sokağı & Hz. Osman Camii
            (Vector3 pos, float yaw, string tag)[] lampSpots =
            {
                (new Vector3(-74.0f, 0.04f,  5.55f), 180f, "N_No50"),
                (new Vector3(-46.0f, 0.04f,  5.55f), 180f, "N_No52"),
                (new Vector3(-18.0f, 0.04f,  5.55f), 180f, "N_No63"),
                (new Vector3(  9.5f, 0.04f,  5.55f), 180f, "N_No58Bakkal"),
                (new Vector3( 37.0f, 0.04f,  5.55f), 180f, "N_No66Yellow"),
                (new Vector3( 66.5f, 0.04f,  5.55f), 180f, "N_No81Rose"),
                (new Vector3( 93.0f, 0.04f,  5.55f), 180f, "N_No95Corner"),
                (new Vector3(-60.0f, 0.04f, -5.55f),   0f, "S_No46S"),
                (new Vector3(-26.0f, 0.04f, -5.55f),   0f, "S_Gursu"),
                (new Vector3( 22.0f, 0.04f, -5.55f),   0f, "S_No62"),
                (new Vector3( 54.0f, 0.04f, -5.55f),   0f, "S_Hafsa"),
                (new Vector3( 82.0f, 0.04f, -22.8f), 180f, "Camii_AvluN")
            };

            Color warmSodium = new Color(1.0f, 0.80f, 0.48f);
            for (int i = 0; i < lampSpots.Length; i++)
            {
                var ls = lampSpots[i];
                GameObject lamp = PlaceSelmanModel(
                    "Sultanbeyli_StreetLamp.fbx",
                    $"Selman_StreetLamp_{ls.tag}",
                    livingRoot.transform,
                    ls.pos,
                    Quaternion.Euler(0f, ls.yaw, 0f),
                    Vector3.one,
                    new Vector3(0.32f, 5.8f, 0.32f),
                    new Vector3(0f, 2.9f, 0f),
                    mats);

                if (lamp != null)
                {
                    GameObject lightGo = new GameObject("NightPoolLight");
                    lightGo.transform.SetParent(lamp.transform, false);
                    lightGo.transform.localPosition = new Vector3(0f, 5.85f, 1.50f);
                    Light pl = lightGo.AddComponent<Light>();
                    pl.type = LightType.Point;
                    pl.color = warmSodium;
                    pl.range = 19f;
                    pl.intensity = 3.4f;
                    pl.shadows = LightShadows.None;
                    pl.enabled = false;

                    Renderer lensRenderer = null;
                    Transform[] children = lamp.GetComponentsInChildren<Transform>(true);
                    for (int c = 0; c < children.Length; c++)
                    {
                        if (children[c].name.Contains("HeadLens"))
                        {
                            lensRenderer = children[c].GetComponent<Renderer>();
                            break;
                        }
                    }

                    NightLight nl = lightGo.AddComponent<NightLight>();
                    nl.Configure(pl, lensRenderer != null ? new[] { lensRenderer } : null, warmSodium * 4.0f);
                }
            }

            // 2. 6 Roaming Pettable Istanbul Street Cats (Sarman, Tekir, Pamuk, Kömür, Badem, Huzur!)
            (string name, Vector3 pos, Color fur, float radius)[] cats =
            {
                ("Sarman", new Vector3(-61.5f, 0.06f,  5.1f), new Color(0.90f, 0.56f, 0.24f, 1f), 2.8f),
                ("Tekir",  new Vector3(  4.5f, 0.06f,  5.1f), new Color(0.56f, 0.49f, 0.42f, 1f), 3.0f),
                ("Pamuk",  new Vector3( 53.2f, 0.06f,  5.0f), new Color(0.95f, 0.93f, 0.89f, 1f), 2.6f),
                ("Kömür",  new Vector3( 38.0f, 0.06f, -5.1f), new Color(0.18f, 0.19f, 0.21f, 1f), 2.8f),
                ("Badem",  new Vector3( 90.2f, 0.06f,  5.0f), new Color(0.84f, 0.66f, 0.38f, 1f), 2.6f),
                ("Huzur",  new Vector3( 85.4f, 0.06f, -31.2f), new Color(0.62f, 0.64f, 0.68f, 1f), 3.4f)
            };

            for (int i = 0; i < cats.Length; i++)
            {
                var c = cats[i];
                GameObject catGo = new GameObject($"IstanbulCat_{c.name}");
                catGo.transform.SetParent(livingRoot.transform, false);
                catGo.transform.SetPositionAndRotation(c.pos, Quaternion.Euler(0f, i * 65f, 0f));

                InstantiateBlenderModel("Istanbul_StreetCat.fbx", $"{c.name}_Mesh", catGo.transform, mats);

                StreetCatController ctrl = catGo.AddComponent<StreetCatController>();
                ctrl.Configure(c.name, c.fur, c.radius);

                InteractableTrigger trig = catGo.AddComponent<InteractableTrigger>();
                trig.Configure(InteractableKind.StreetCat, $"street_cat_{c.name.ToLowerInvariant()}", "ui.btn.pet_cat", 2.5f);
            }
        }

        private static void BuildSelmanInteractiveMechanics(Transform parent, MaterialSet mats)
        {
            GameObject mechRoot = new GameObject("Selman_InteractiveMechanics_v25");
            mechRoot.transform.SetParent(parent, false);

            // 1. MAHALLE FUTBOL SAHASI (Gürsu Sokağı pocket, X = -41, Z = -20): Goal + Net Sensor + Physics Football!
            GameObject goalGo = PlaceSelmanModel(
                "Istanbul_FootballGoal.fbx",
                "Selman_FootballGoal",
                mechRoot.transform,
                new Vector3(-41.0f, 0.04f, -21.2f),
                Quaternion.identity, // Opening faces North (+Z) toward Selman Sokağı
                Vector3.one,
                Vector3.zero,
                Vector3.zero,
                mats);
            if (goalGo != null)
            {
                // Side & back net colliders so ball stays inside the net after scoring!
                AddLocalBoxCollider(goalGo, new Vector3(0f, 1.05f, -1.18f), new Vector3(3.45f, 2.15f, 0.14f));
                AddLocalBoxCollider(goalGo, new Vector3(-1.72f, 1.05f, -0.60f), new Vector3(0.12f, 2.15f, 1.25f));
                AddLocalBoxCollider(goalGo, new Vector3(1.72f, 1.05f, -0.60f), new Vector3(0.12f, 2.15f, 1.25f));
                AddLocalBoxCollider(goalGo, new Vector3(0f, 2.12f, -0.60f), new Vector3(3.45f, 0.12f, 1.25f));

                GameObject sensorGo = new GameObject("GoalLine_Trigger");
                sensorGo.transform.SetParent(goalGo.transform, false);
                sensorGo.transform.localPosition = new Vector3(0f, 1.0f, -0.58f);
                BoxCollider goalBox = sensorGo.AddComponent<BoxCollider>();
                goalBox.isTrigger = true;
                goalBox.size = new Vector3(3.10f, 1.95f, 0.90f);
                sensorGo.AddComponent<FootballGoal>();

                CreateWorldSignText("MAHALLE MAÇI · GOL = +35 ₺", goalGo.transform,
                    new Vector3(0f, 2.48f, -0.04f), Quaternion.Euler(0f, 180f, 0f),
                    new Vector2(3.1f, 0.38f), new Color(0.10f, 0.36f, 0.22f, 1f));
            }

            // Physics-driven Soccer Ball on the Gürsu Sokağı penalty spot!
            GameObject ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "Selman_StreetFootball";
            ballGo.transform.SetParent(mechRoot.transform, false);
            ballGo.transform.position = new Vector3(-41.0f, 0.30f, -12.6f);
            ballGo.transform.localScale = Vector3.one * 0.42f;
            Renderer ballRend = ballGo.GetComponent<Renderer>();
            if (ballRend != null) ballRend.sharedMaterial = mats.WhitePVC;
            // Classic black pentagon accent band so spinning motion is clearly visible!
            CreatePrimitiveChild("BallBandA", PrimitiveType.Cube, ballGo.transform, Vector3.zero, new Vector3(1.03f, 0.24f, 0.24f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("BallBandB", PrimitiveType.Cube, ballGo.transform, Vector3.zero, new Vector3(0.24f, 1.03f, 0.24f), mats.SedanTrimDark, removeCollider: true);
            ballGo.AddComponent<Rigidbody>();
            ballGo.AddComponent<StreetFootball>();
            InteractableTrigger ballTrig = ballGo.AddComponent<InteractableTrigger>();
            ballTrig.Configure(InteractableKind.Football, "selman_football", "ui.btn.kick_ball", 2.4f);

            // 2. TWO ILLUMINATED MAHALLE ATM KIOSKS (No:58 Selman Bakkal & No:95 Abdülkadir Udeh Corner)
            (string id, string label, Vector3 pos, float yaw)[] atmSpots =
            {
                ("atm_bakkal58", "Sultanbeyli Bank ATM · No:58", new Vector3(10.8f, 0.04f,  5.35f), 180f),
                ("atm_kose95",   "Hamidiye ATM · No:95 Köşe",    new Vector3(93.6f, 0.04f, -5.35f),   0f)
            };
            for (int i = 0; i < atmSpots.Length; i++)
            {
                var a = atmSpots[i];
                GameObject atmGo = PlaceSelmanModel(
                    "Istanbul_ATM.fbx",
                    $"Selman_ATM_{i + 1}",
                    mechRoot.transform,
                    a.pos,
                    Quaternion.Euler(0f, a.yaw, 0f),
                    Vector3.one,
                    new Vector3(1.05f, 2.2f, 0.85f),
                    new Vector3(0f, 1.1f, 0f),
                    mats);
                if (atmGo != null)
                {
                    AtmTerminalInteractable atm = atmGo.AddComponent<AtmTerminalInteractable>();
                    atm.Configure(a.label);
                    InteractableTrigger atmTrig = atmGo.AddComponent<InteractableTrigger>();
                    atmTrig.Configure(InteractableKind.AtmTerminal, a.id, "ui.btn.use_atm", 2.8f);

                    GameObject atmLightGo = new GameObject("ATM_ScreenGlow");
                    atmLightGo.transform.SetParent(atmGo.transform, false);
                    atmLightGo.transform.localPosition = new Vector3(0f, 1.5f, 0.65f);
                    Light al = atmLightGo.AddComponent<Light>();
                    al.type = LightType.Point;
                    al.color = new Color(0.32f, 0.82f, 1.0f);
                    al.range = 6.5f;
                    al.intensity = 1.6f;
                    al.shadows = LightShadows.None;
                    al.enabled = false;
                    NightLight nl = atmLightGo.AddComponent<NightLight>();
                    nl.Configure(al, null, new Color(0.32f, 0.82f, 1.0f) * 2.2f);
                }
            }

            // 3. SIX PHYSICAL GRAB & THROW TURKISH STREET PROPS (Damacana 19L Water Jugs & Street Cones!)
            (string name, string label, Vector3 pos, bool isDamacana)[] carryProps =
            {
                ("Prop_Damacana_No48",   "19L Damacana Su", new Vector3(-59.2f, 0.06f,  5.2f), true),
                ("Prop_Damacana_Bakkal", "19L Damacana Su", new Vector3(  2.8f, 0.06f,  5.1f), true),
                ("Prop_Damacana_Hafsa",  "19L Damacana Su", new Vector3( 36.2f, 0.06f, -5.1f), true),
                ("Prop_Koni_OtoTamir1",  "Trafik Konisi",   new Vector3( 21.5f, 0.06f,  4.8f), false),
                ("Prop_Koni_OtoTamir2",  "Trafik Konisi",   new Vector3( 27.5f, 0.06f,  4.8f), false),
                ("Prop_Kasa_Cicekci",    "Manav Kasası",    new Vector3(-13.8f, 0.06f, -4.9f), false)
            };
            for (int i = 0; i < carryProps.Length; i++)
            {
                var cp = carryProps[i];
                GameObject propGo = new GameObject(cp.name);
                propGo.transform.SetParent(mechRoot.transform, false);
                propGo.transform.position = cp.pos;

                if (cp.isDamacana)
                {
                    InstantiateBlenderModel("Istanbul_WaterJug.fbx", "Damacana_Mesh", propGo.transform, mats);
                    BoxCollider bc = propGo.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, 0.26f, 0f);
                    bc.size = new Vector3(0.30f, 0.52f, 0.30f);
                }
                else if (cp.label.Contains("Koni"))
                {
                    CreatePrimitiveChild("ConeBase", PrimitiveType.Cube, propGo.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.34f, 0.06f, 0.34f), mats.SedanTrimDark, removeCollider: true);
                    CreatePrimitiveChild("ConeBody", PrimitiveType.Cylinder, propGo.transform, new Vector3(0f, 0.28f, 0f), new Vector3(0.18f, 0.24f, 0.18f), mats.CanOrange, removeCollider: true);
                    CreatePrimitiveChild("ConeStripe", PrimitiveType.Cylinder, propGo.transform, new Vector3(0f, 0.32f, 0f), new Vector3(0.19f, 0.05f, 0.19f), mats.WhitePVC, removeCollider: true);
                    BoxCollider bc = propGo.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, 0.26f, 0f);
                    bc.size = new Vector3(0.32f, 0.52f, 0.32f);
                }
                else
                {
                    CreatePrimitiveChild("CrateBox", PrimitiveType.Cube, propGo.transform, new Vector3(0f, 0.18f, 0f), new Vector3(0.52f, 0.36f, 0.38f), mats.TownWood, removeCollider: true);
                    BoxCollider bc = propGo.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, 0.18f, 0f);
                    bc.size = new Vector3(0.52f, 0.36f, 0.38f);
                }

                Rigidbody rb = propGo.AddComponent<Rigidbody>();
                rb.mass = cp.isDamacana ? 3.5f : 1.5f;
                CarryableProp carry = propGo.AddComponent<CarryableProp>();
                carry.Configure(cp.label, cp.isDamacana ? 10.5f : 13.0f);

                InteractableTrigger trig = propGo.AddComponent<InteractableTrigger>();
                trig.Configure(InteractableKind.CarryableProp, cp.name.ToLowerInvariant(), "ui.btn.carry_prop", 2.2f);
            }
        }

        private static void AttachSelmanPaintableAddress(GameObject houseRoot, string houseId, string displayName, Vector3 streetFrontWorldPos)
        {
            if (houseRoot == null) return;

            // Collect only exterior facade wall renderers on the building shell mesh (never interior furniture!)
            List<Renderer> facadeRenderers = new List<Renderer>();
            Transform shell = houseRoot.transform.Find($"{houseRoot.name}_Mesh");
            if (shell != null)
            {
                Renderer[] all = shell.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    Renderer r = all[i];
                    if (r == null) continue;
                    string n = r.gameObject.name.ToLowerInvariant();
                    if (n.Contains("wall") || n.Contains("facade") || n.Contains("body") || n.Contains("plaster") || n.Contains("story") || n.Contains("upper") || n.Contains("main"))
                    {
                        facadeRenderers.Add(r);
                    }
                }
                if (facadeRenderers.Count == 0 && all.Length > 0)
                {
                    facadeRenderers.Add(all[0]);
                }
            }

            Vector3 localFront = houseRoot.transform.InverseTransformPoint(streetFrontWorldPos);
            PaintableHouse ph = houseRoot.AddComponent<PaintableHouse>();
            ph.ConfigureWithLocalFront(houseId, facadeRenderers, displayName, localFront);

            // Place the Paint interactable trigger cleanly at the street-facing entrance so players can paint easily
            GameObject paintSpot = new GameObject($"PaintTrigger_{houseId}");
            paintSpot.transform.SetParent(houseRoot.transform, false);
            paintSpot.transform.position = streetFrontWorldPos;
            InteractableTrigger trig = paintSpot.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.PaintableHouse, $"paint_{houseId}", "ui.btn.paint", 3.2f);
        }

        private static void AttachShopSectionTrigger(GameObject host, string shopId, string promptKey, float radius)
        {
            if (host == null) return;
            InteractableTrigger trig = host.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.ShopCounter, shopId, promptKey, radius);
        }

        private static void AttachHomeAppliance(GameObject host, HomeApplianceKind kind, string label, string promptKey, float radius = 2.4f)
        {
            if (host == null) return;
            HomeApplianceInteractable app = host.AddComponent<HomeApplianceInteractable>();
            app.Configure(kind, label);
            InteractableTrigger trig = host.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.HomeAppliance, $"{kind}_{host.name}".ToLowerInvariant(), promptKey, radius);
        }

        private static void AttachSofaSeat(GameObject sofaObj)
        {
            if (sofaObj == null) return;
            CreateSeatSlot("Sofa_SeatSlot", $"seat_{sofaObj.name}".ToLowerInvariant(), sofaObj.transform,
                new Vector3(0f, 0.45f, 0.08f), Quaternion.identity, 2.4f);
        }

        private static void AttachBedTrigger(GameObject bedObj)
        {
            if (bedObj == null) return;
            InteractableTrigger trig = bedObj.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.Bed, $"bed_{bedObj.name}".ToLowerInvariant(), "ui.btn.sleep", 2.5f);
        }

        private static void BuildInteriorPartitionWall(
            Transform host,
            string name,
            Vector3 localCenter,
            float length,
            float height,
            bool alongZ,
            float doorGapCenterOffset,
            float doorGapWidth,
            Material wallMat)
        {
            const float t = 0.14f;
            float half = length * 0.5f;
            float gapLo = Mathf.Clamp(doorGapCenterOffset - doorGapWidth * 0.5f, -half, half);
            float gapHi = Mathf.Clamp(doorGapCenterOffset + doorGapWidth * 0.5f, -half, half);

            if (gapLo > -half + 0.15f)
            {
                float seg = gapLo - (-half);
                float off = (-half + gapLo) * 0.5f;
                Vector3 pos = localCenter + (alongZ ? new Vector3(0f, height * 0.5f, off) : new Vector3(off, height * 0.5f, 0f));
                Vector3 sz = alongZ ? new Vector3(t, height, seg) : new Vector3(seg, height, t);
                GameObject w = CreatePrimitiveChild($"{name}_A", PrimitiveType.Cube, host, pos, sz, wallMat, removeCollider: false);
                MarkStaticEnvironment(w);
            }
            if (half > gapHi + 0.15f)
            {
                float seg = half - gapHi;
                float off = (gapHi + half) * 0.5f;
                Vector3 pos = localCenter + (alongZ ? new Vector3(0f, height * 0.5f, off) : new Vector3(off, height * 0.5f, 0f));
                Vector3 sz = alongZ ? new Vector3(t, height, seg) : new Vector3(seg, height, t);
                GameObject w = CreatePrimitiveChild($"{name}_B", PrimitiveType.Cube, host, pos, sz, wallMat, removeCollider: false);
                MarkStaticEnvironment(w);
            }
            // Top lintel above interior doorway
            float lintelH = Mathf.Max(0.25f, height - 2.25f);
            Vector3 lPos = localCenter + (alongZ
                ? new Vector3(0f, height - lintelH * 0.5f, doorGapCenterOffset)
                : new Vector3(doorGapCenterOffset, height - lintelH * 0.5f, 0f));
            Vector3 lSz = alongZ ? new Vector3(t, lintelH, doorGapWidth) : new Vector3(doorGapWidth, lintelH, t);
            GameObject lintel = CreatePrimitiveChild($"{name}_Lintel", PrimitiveType.Cube, host, lPos, lSz, wallMat, removeCollider: true);
            MarkStaticEnvironment(lintel);
        }

        /// <summary>
        /// Furnishes each Selman Sokağı home with an authentic distinct Turkish architectural floor plan
        /// (1+0 Stüdyo, 1+1 Daire, 2+1 Aile Evi, or 3+1 Geniş Aile Dairesi) with real interior partition
        /// walls, separate rooms, and usable interactive appliances (TV, Çaydanlık, Buzdolabı, Gardırop,
        /// Bilgisayar, Çamaşır Makinesi, Lavabo, Oturulabilir L-Koltuk & Uyuma Yatağı)!
        /// </summary>
        private static void FurnishSelmanHomeInterior(
            Transform host,
            Vector3 center,
            SelmanFloorPlan plan,
            float roomW,
            float roomD,
            MaterialSet mats)
        {
            float hw = roomW * 0.5f;
            float hd = roomD * 0.5f;
            const float partH = 3.05f;

            switch (plan)
            {
                case SelmanFloorPlan.Studio_1_0:
                {
                    // 1+0 STÜDYO DAİRE: Open-plan cozy studio with Study PC Desk, Sofa, Tea Table, Kitchenette & Wardrobe!
                    CreateWorldSignText("1+0 STÜDYO DAİRE", host, center + new Vector3(0f, 2.55f, -hd + 0.16f),
                        Quaternion.Euler(0f, 180f, 0f), new Vector2(2.6f, 0.34f), new Color(0.22f, 0.24f, 0.28f, 1f));

                    PlaceInteriorModel("Interior_TurkishRugKilim.fbx", "Studio_Kilim", host, center + new Vector3(0.4f, 0f, 0.2f), Quaternion.identity, mats);
                    GameObject sofa = PlaceInteriorModel("Interior_TurkishSofaL.fbx", "Studio_Sofa", host, center + new Vector3(0.7f, 0f, -hd + 1.15f), Quaternion.identity, mats, colliderSize: new Vector3(2.4f, 0.85f, 0.9f));
                    AttachSofaSeat(sofa);

                    GameObject tea = PlaceInteriorModel("Interior_TurkishTeaTable.fbx", "Studio_TeaTable", host, center + new Vector3(0.6f, 0f, 0.2f), Quaternion.identity, mats, colliderSize: new Vector3(1.1f, 0.55f, 0.65f));
                    AttachHomeAppliance(tea, HomeApplianceKind.TeaKettle, "Çaydanlık", "ui.btn.use_tea");

                    GameObject desk = PlaceInteriorModel("Interior_StudyDesk.fbx", "Studio_StudyDesk", host, center + new Vector3(-hw + 1.05f, 0f, -hd + 1.05f), Quaternion.identity, mats, colliderSize: new Vector3(1.35f, 0.85f, 0.7f));
                    AttachHomeAppliance(desk, HomeApplianceKind.ComputerDesk, "Bilgisayar", "ui.btn.use_pc");

                    GameObject kit = PlaceInteriorModel("Interior_TurkishKitchen.fbx", "Studio_Kitchen", host, center + new Vector3(-hw + 0.85f, 0f, 0.9f), Quaternion.Euler(0f, 90f, 0f), mats, colliderSize: new Vector3(2.8f, 1.8f, 0.7f));
                    AttachHomeAppliance(kit, HomeApplianceKind.Refrigerator, "Buzdolabı", "ui.btn.use_fridge");

                    GameObject ward = PlaceInteriorModel("Interior_Wardrobe.fbx", "Studio_Wardrobe", host, center + new Vector3(hw - 0.65f, 0f, 0.6f), Quaternion.Euler(0f, -90f, 0f), mats, colliderSize: new Vector3(1.7f, 2.0f, 0.6f));
                    AttachHomeAppliance(ward, HomeApplianceKind.Wardrobe, "Gardırop", "ui.btn.use_wardrobe");
                    break;
                }

                case SelmanFloorPlan.Flat_1_1:
                {
                    // 1+1 DAİRE: Real interior partition wall splitting Salon+Mutfak (right/+X) from Ayrı Yatak Odası (left/-X)!
                    float splitX = -hw * 0.14f;
                    BuildInteriorPartitionWall(host, "Wall_1plus1_Split", center + new Vector3(splitX, 0f, 0f),
                        roomD - 0.2f, partH, alongZ: true, doorGapCenterOffset: hd * 0.42f, doorGapWidth: 1.35f, mats.TownInteriorPaintCream);

                    CreateWorldSignText("1+1 DAİRE", host, center + new Vector3(splitX + 0.12f, 2.55f, 0f),
                        Quaternion.Euler(0f, -90f, 0f), new Vector2(1.8f, 0.30f), new Color(0.24f, 0.24f, 0.28f, 1f));

                    // Room 1 (+X side): Salon & Açık Mutfak
                    float salonX = (splitX + hw) * 0.5f;
                    PlaceInteriorModel("Interior_TurkishRugKilim.fbx", "Flat11_Kilim", host, center + new Vector3(salonX, 0f, 0.1f), Quaternion.identity, mats);
                    GameObject sofa = PlaceInteriorModel("Interior_TurkishSofaL.fbx", "Flat11_SofaL", host, center + new Vector3(salonX + 0.15f, 0f, -hd + 1.1f), Quaternion.identity, mats, colliderSize: new Vector3(2.4f, 0.85f, 0.9f));
                    AttachSofaSeat(sofa);

                    GameObject tea = PlaceInteriorModel("Interior_TurkishTeaTable.fbx", "Flat11_TeaTable", host, center + new Vector3(salonX, 0f, -0.35f), Quaternion.identity, mats, colliderSize: new Vector3(1.1f, 0.55f, 0.65f));
                    AttachHomeAppliance(tea, HomeApplianceKind.TeaKettle, "Çaydanlık", "ui.btn.use_tea");

                    GameObject tv = PlaceInteriorModel("Interior_TvUnitVitrin.fbx", "Flat11_TvUnit", host, center + new Vector3(salonX, 0f, hd - 0.65f), Quaternion.Euler(0f, 180f, 0f), mats, colliderSize: new Vector3(2.2f, 1.5f, 0.5f));
                    AttachHomeAppliance(tv, HomeApplianceKind.Television, "Televizyon", "ui.btn.use_tv");

                    GameObject kit = PlaceInteriorModel("Interior_TurkishKitchen.fbx", "Flat11_Kitchen", host, center + new Vector3(hw - 0.65f, 0f, 0.4f), Quaternion.Euler(0f, -90f, 0f), mats, colliderSize: new Vector3(2.6f, 1.8f, 0.7f));
                    AttachHomeAppliance(kit, HomeApplianceKind.Refrigerator, "Buzdolabı", "ui.btn.use_fridge");

                    // Room 2 (-X side): Ayrı Yatak Odası
                    float bedX = (-hw + splitX) * 0.5f;
                    GameObject bed = PlaceInteriorModel("Interior_BedroomSet.fbx", "Flat11_Bed", host, center + new Vector3(bedX, 0f, -hd + 1.45f), Quaternion.identity, mats, colliderSize: new Vector3(1.8f, 0.9f, 2.1f));
                    AttachBedTrigger(bed);

                    GameObject ward = PlaceInteriorModel("Interior_Wardrobe.fbx", "Flat11_Wardrobe", host, center + new Vector3(-hw + 0.65f, 0f, 0.5f), Quaternion.Euler(0f, 90f, 0f), mats, colliderSize: new Vector3(1.7f, 2.0f, 0.6f));
                    AttachHomeAppliance(ward, HomeApplianceKind.Wardrobe, "Gardırop", "ui.btn.use_wardrobe");
                    break;
                }

                case SelmanFloorPlan.Family_2_1:
                {
                    // 2+1 AİLE EVİ: Partitioned Salon+Yemek Odası (+X), Yatak Odası (-X Back) & Banyo/Çamaşır Köşesi (-X Front)!
                    float splitX = -hw * 0.08f;
                    BuildInteriorPartitionWall(host, "Wall_2plus1_Main", center + new Vector3(splitX, 0f, 0f),
                        roomD - 0.2f, partH, alongZ: true, doorGapCenterOffset: 0.2f, doorGapWidth: 1.45f, mats.TownInteriorPaintWarm);
                    BuildInteriorPartitionWall(host, "Wall_2plus1_Bath", center + new Vector3((-hw + splitX) * 0.5f, 0f, hd * 0.16f),
                        (splitX - (-hw)) - 0.2f, partH, alongZ: false, doorGapCenterOffset: 0.65f, doorGapWidth: 1.15f, mats.TownInteriorPaintPearl);

                    // Salon (+X)
                    float salonX = (splitX + hw) * 0.5f;
                    PlaceInteriorModel("Interior_TurkishRugKilim.fbx", "Fam21_Kilim", host, center + new Vector3(salonX, 0f, -0.4f), Quaternion.identity, mats);
                    GameObject sofa = PlaceInteriorModel("Interior_TurkishSofaL.fbx", "Fam21_SofaL", host, center + new Vector3(salonX, 0f, -hd + 1.05f), Quaternion.identity, mats, colliderSize: new Vector3(2.4f, 0.85f, 0.9f));
                    AttachSofaSeat(sofa);

                    GameObject tea = PlaceInteriorModel("Interior_TurkishTeaTable.fbx", "Fam21_TeaTable", host, center + new Vector3(salonX, 0f, -0.6f), Quaternion.identity, mats, colliderSize: new Vector3(1.1f, 0.55f, 0.65f));
                    AttachHomeAppliance(tea, HomeApplianceKind.TeaKettle, "Çaydanlık", "ui.btn.use_tea");

                    GameObject tv = PlaceInteriorModel("Interior_TvUnitVitrin.fbx", "Fam21_TvUnit", host, center + new Vector3(hw - 0.65f, 0f, -0.2f), Quaternion.Euler(0f, -90f, 0f), mats, colliderSize: new Vector3(2.2f, 1.5f, 0.5f));
                    AttachHomeAppliance(tv, HomeApplianceKind.Television, "Televizyon", "ui.btn.use_tv");

                    PlaceInteriorModel("Interior_DiningSet.fbx", "Fam21_Dining", host, center + new Vector3(salonX, 0f, hd - 1.35f), Quaternion.identity, mats, colliderSize: new Vector3(1.6f, 0.8f, 1.0f));

                    // Yatak Odası (-X Back)
                    float westX = (-hw + splitX) * 0.5f;
                    GameObject bed = PlaceInteriorModel("Interior_BedroomSet.fbx", "Fam21_Bed", host, center + new Vector3(westX, 0f, -hd + 1.35f), Quaternion.identity, mats, colliderSize: new Vector3(1.8f, 0.9f, 2.1f));
                    AttachBedTrigger(bed);

                    GameObject ward = PlaceInteriorModel("Interior_Wardrobe.fbx", "Fam21_Wardrobe", host, center + new Vector3(-hw + 0.62f, 0f, -hd + 1.8f), Quaternion.Euler(0f, 90f, 0f), mats, colliderSize: new Vector3(1.7f, 2.0f, 0.6f));
                    AttachHomeAppliance(ward, HomeApplianceKind.Wardrobe, "Gardırop", "ui.btn.use_wardrobe");

                    // Banyo & Çamaşır Odası (-X Front)
                    GameObject bath = PlaceInteriorModel("Interior_BathroomSet.fbx", "Fam21_Bathroom", host, center + new Vector3(westX, 0f, hd - 0.72f), Quaternion.Euler(0f, 180f, 0f), mats, colliderSize: new Vector3(2.1f, 1.0f, 0.7f));
                    AttachHomeAppliance(bath, HomeApplianceKind.WashingMachine, "Çamaşır Makinesi", "ui.btn.use_washer");
                    GameObject sinkTrig = new GameObject("Fam21_SinkInteract");
                    sinkTrig.transform.SetParent(bath.transform, false);
                    sinkTrig.transform.localPosition = new Vector3(0.45f, 0.8f, 0.4f);
                    AttachHomeAppliance(sinkTrig, HomeApplianceKind.BathroomSink, "Lavabo", "ui.btn.use_sink");
                    break;
                }

                case SelmanFloorPlan.Grand_3_1:
                {
                    // 3+1 GENİŞ AİLE DAİRESİ: Central Hallway splitting 4 distinct rooms!
                    //   NE (+X, -Z): Geniş Salon + Yemek Masası + TV
                    //   SE (+X, +Z): Tam Türk Mutfağı + Buzdolabı + Banyo/Çamaşır
                    //   NW (-X, -Z): Ebeveyn Yatak Odası (Yatak + Aynalı Gardırop)
                    //   SW (-X, +Z): Genç / Çalışma Odası (Bilgisayar Masası + Kitaplık)
                    BuildInteriorPartitionWall(host, "Wall_3plus1_CenterZ", center,
                        roomD - 0.3f, partH, alongZ: true, doorGapCenterOffset: 0.0f, doorGapWidth: 1.80f, mats.TownInteriorPaintCream);
                    BuildInteriorPartitionWall(host, "Wall_3plus1_WestSplit", center + new Vector3(-hw * 0.5f, 0f, 0f),
                        hw - 0.25f, partH, alongZ: false, doorGapCenterOffset: hw * 0.24f, doorGapWidth: 1.25f, mats.TownInteriorPaintWarm);
                    BuildInteriorPartitionWall(host, "Wall_3plus1_EastSplit", center + new Vector3(hw * 0.5f, 0f, 0f),
                        hw - 0.25f, partH, alongZ: false, doorGapCenterOffset: -hw * 0.24f, doorGapWidth: 1.25f, mats.TownInteriorPaintWarm);

                    // Room 1 (+X, -Z): Geniş Salon
                    float ex = hw * 0.52f;
                    float wx = -hw * 0.52f;
                    float bz = -hd * 0.52f;
                    float fz = hd * 0.52f;

                    PlaceInteriorModel("Interior_TurkishRugKilim.fbx", "G31_Kilim", host, center + new Vector3(ex, 0f, bz), Quaternion.identity, mats);
                    GameObject sofa = PlaceInteriorModel("Interior_TurkishSofaL.fbx", "G31_SofaL", host, center + new Vector3(ex, 0f, -hd + 0.95f), Quaternion.identity, mats, colliderSize: new Vector3(2.4f, 0.85f, 0.9f));
                    AttachSofaSeat(sofa);

                    GameObject tea = PlaceInteriorModel("Interior_TurkishTeaTable.fbx", "G31_TeaTable", host, center + new Vector3(ex, 0f, bz + 0.1f), Quaternion.identity, mats, colliderSize: new Vector3(1.1f, 0.55f, 0.65f));
                    AttachHomeAppliance(tea, HomeApplianceKind.TeaKettle, "Çaydanlık", "ui.btn.use_tea");

                    GameObject tv = PlaceInteriorModel("Interior_TvUnitVitrin.fbx", "G31_TvUnit", host, center + new Vector3(hw - 0.65f, 0f, bz), Quaternion.Euler(0f, -90f, 0f), mats, colliderSize: new Vector3(2.2f, 1.5f, 0.5f));
                    AttachHomeAppliance(tv, HomeApplianceKind.Television, "Televizyon", "ui.btn.use_tv");

                    // Room 2 (+X, +Z): Mutfak + Yemek Masası
                    GameObject kit = PlaceInteriorModel("Interior_TurkishKitchen.fbx", "G31_Kitchen", host, center + new Vector3(hw - 0.68f, 0f, fz), Quaternion.Euler(0f, -90f, 0f), mats, colliderSize: new Vector3(2.8f, 1.8f, 0.7f));
                    AttachHomeAppliance(kit, HomeApplianceKind.Refrigerator, "Buzdolabı", "ui.btn.use_fridge");
                    PlaceInteriorModel("Interior_DiningSet.fbx", "G31_Dining", host, center + new Vector3(ex - 0.5f, 0f, fz + 0.2f), Quaternion.identity, mats, colliderSize: new Vector3(1.55f, 0.8f, 0.95f));

                    // Room 3 (-X, -Z): Ebeveyn Yatak Odası
                    GameObject bed = PlaceInteriorModel("Interior_BedroomSet.fbx", "G31_MasterBed", host, center + new Vector3(wx, 0f, -hd + 1.35f), Quaternion.identity, mats, colliderSize: new Vector3(1.8f, 0.9f, 2.1f));
                    AttachBedTrigger(bed);
                    GameObject ward = PlaceInteriorModel("Interior_Wardrobe.fbx", "G31_Wardrobe", host, center + new Vector3(-hw + 0.65f, 0f, bz), Quaternion.Euler(0f, 90f, 0f), mats, colliderSize: new Vector3(1.7f, 2.0f, 0.6f));
                    AttachHomeAppliance(ward, HomeApplianceKind.Wardrobe, "Gardırop", "ui.btn.use_wardrobe");

                    // Room 4 (-X, +Z): Çalışma Odası + Banyo/Çamaşır
                    GameObject desk = PlaceInteriorModel("Interior_StudyDesk.fbx", "G31_StudyDesk", host, center + new Vector3(-hw + 1.15f, 0f, fz - 0.35f), Quaternion.Euler(0f, 90f, 0f), mats, colliderSize: new Vector3(1.35f, 0.85f, 0.7f));
                    AttachHomeAppliance(desk, HomeApplianceKind.ComputerDesk, "Bilgisayar", "ui.btn.use_pc");

                    GameObject bath = PlaceInteriorModel("Interior_BathroomSet.fbx", "G31_Bathroom", host, center + new Vector3(wx + 0.3f, 0f, hd - 0.70f), Quaternion.Euler(0f, 180f, 0f), mats, colliderSize: new Vector3(2.1f, 1.0f, 0.7f));
                    AttachHomeAppliance(bath, HomeApplianceKind.WashingMachine, "Çamaşır Makinesi", "ui.btn.use_washer");
                    GameObject sinkTrig = new GameObject("G31_SinkInteract");
                    sinkTrig.transform.SetParent(bath.transform, false);
                    sinkTrig.transform.localPosition = new Vector3(0.45f, 0.8f, 0.4f);
                    AttachHomeAppliance(sinkTrig, HomeApplianceKind.BathroomSink, "Lavabo", "ui.btn.use_sink");
                    break;
                }
            }

            CreateInteriorLight("Int_WarmLight", host, center + new Vector3(0f, 2.75f, 0f), new Color(1.0f, 0.91f, 0.74f), 13f, 2.5f);
        }

        private static void BuildSelmanPolesAndHighVoltageLines(Transform parent, MaterialSet mats)
        {
            GameObject powerRoot = new GameObject("Selman_UtilityPoles_And_HighVoltagePylon");
            powerRoot.transform.SetParent(parent, false);

            (float x, bool steel)[] poles =
            {
                (-94.0f, false),
                (-76.5f, true),
                (-73.0f, false),
                (-40.5f, true),
                (-12.0f, false),
                (18.5f,  false),
                (21.2f,  true),
                (40.0f,  false),
                (54.2f,  true),   // Steel pole right beside 66 Yellow House & Nutuk Sk. (exact from step 1358!)
                (68.5f,  false),
                (92.0f,  false),  // Paired wood + steel pole right at 95 Selman Sk. corner (exact from step 1360!)
                (94.2f,  true),
                (112.0f, false)
            };

            List<Vector3> wirePoints = new List<Vector3>();
            for (int i = 0; i < poles.Length; i++)
            {
                string fbx = poles[i].steel ? "Sultanbeyli_PowerPole_Steel.fbx" : "Sultanbeyli_PowerPole_Wood.fbx";
                Vector3 p = new Vector3(poles[i].x, 0f, 5.45f);
                PlaceSelmanModel(fbx, $"Selman_Pole_{i:00}", powerRoot.transform, p, Quaternion.Euler(0f, 180f, 0f),
                    Vector3.one, new Vector3(0.42f, 7.6f, 0.42f), new Vector3(0f, 3.8f, 0f), mats);
                wirePoints.Add(p + new Vector3(0f, poles[i].steel ? 8.35f : 7.65f, 0f));

                if (i % 2 == 0)
                {
                    CreatePointLamp($"Selman_StreetLight_{i:00}", powerRoot.transform,
                        new Vector3(poles[i].x, 6.9f, 3.6f), new Color(1.0f, 0.80f, 0.46f), 24f, 2.5f);
                }
            }

            float[] wireZOffsets = { -0.42f, 0f, 0.42f };
            for (int i = 0; i < wirePoints.Count - 1; i++)
            {
                Vector3 a = wirePoints[i];
                Vector3 b = wirePoints[i + 1];
                float sag = Mathf.Clamp(Vector3.Distance(a, b) * 0.036f, 0.14f, 1.15f);
                for (int w = 0; w < wireZOffsets.Length; w++)
                {
                    Vector3 lat = new Vector3(0f, 0f, wireZOffsets[w]);
                    Vector3 prev = a + lat;
                    for (int s = 1; s <= 5; s++)
                    {
                        float t = s / 5f;
                        Vector3 curr = Vector3.Lerp(a, b, t) + lat - Vector3.up * (4f * sag * t * (1f - t));
                        CreateCableSegment($"StreetWire_{i}_{w}_{s}", powerRoot.transform, prev, curr, 0.020f, mats.SedanTrimDark);
                        prev = curr;
                    }
                }
            }

            // Iconic High-Voltage Transmission Pylons framing East & West horizons of Selman Sokağı!
            Vector3 eastPylonPos = new Vector3(122.0f, 0f, 0f);
            PlaceSelmanModel("Sultanbeyli_HighVoltagePylon.fbx", "Selman_EastHighVoltagePylon",
                powerRoot.transform, eastPylonPos, Quaternion.Euler(0f, 90f, 0f), Vector3.one,
                new Vector3(5.8f, 26f, 5.8f), new Vector3(0f, 13f, 0f), mats);

            Vector3 westPylonPos = new Vector3(-118.0f, 0f, 0f);
            PlaceSelmanModel("Sultanbeyli_HighVoltagePylon.fbx", "Selman_WestHighVoltagePylon",
                powerRoot.transform, westPylonPos, Quaternion.Euler(0f, 90f, 0f), Vector3.one,
                new Vector3(5.8f, 26f, 5.8f), new Vector3(0f, 13f, 0f), mats);

            (float y, float z)[] hvAnchors = { (17.95f, -5.1f), (17.95f, 5.1f), (22.45f, -4.0f), (22.45f, 4.0f) };
            for (int h = 0; h < hvAnchors.Length; h++)
            {
                Vector3 p0 = new Vector3(westPylonPos.x, hvAnchors[h].y, hvAnchors[h].z);
                Vector3 p1 = new Vector3(eastPylonPos.x, hvAnchors[h].y, hvAnchors[h].z);
                Vector3 prev = p0;
                for (int s = 1; s <= 12; s++)
                {
                    float t = s / 12f;
                    float drop = 4f * 4.2f * t * (1f - t);
                    Vector3 curr = Vector3.Lerp(p0, p1, t) - Vector3.up * drop;
                    CreateCableSegment($"HVLine_{h}_{s}", powerRoot.transform, prev, curr, 0.036f, mats.SedanTrimDark);
                    prev = curr;
                }
            }
        }

        private static void BuildSelmanStreetPropsAndGameplay(Transform parent, MaterialSet mats, PrefabSet prefabs, List<SeatInteractable> seats)
        {
            GameObject propsRoot = new GameObject("Selman_AuthenticProps_And_Gameplay");
            propsRoot.transform.SetParent(parent, false);

            // 1. TWO Sultanbeyli Belediyesi Wheeled Dumpsters at 50 Selman Sk. / Seyyid Kutup corner
            PlaceSelmanModel("Sultanbeyli_Dumpster.fbx", "Dumpster_SeyyidKutup_01", propsRoot.transform,
                new Vector3(-78.8f, 0.04f, 5.0f), Quaternion.Euler(0f, 165f, 0f), Vector3.one,
                new Vector3(1.45f, 1.25f, 1.05f), new Vector3(0f, 0.62f, 0f), mats);
            PlaceSelmanModel("Sultanbeyli_Dumpster.fbx", "Dumpster_SeyyidKutup_02", propsRoot.transform,
                new Vector3(-76.9f, 0.04f, 4.8f), Quaternion.Euler(0f, 180f, 0f), Vector3.one,
                new Vector3(1.45f, 1.25f, 1.05f), new Vector3(0f, 0.62f, 0f), mats);

            // 2. Galvanized Dumpster + Red-Triangle Speed-Bump ('Kasis') Sign at Çiçekçi Sk. corner
            PlaceSelmanModel("Sultanbeyli_Dumpster.fbx", "Dumpster_CicekciCorner", propsRoot.transform,
                new Vector3(-4.2f, 0.04f, -4.9f), Quaternion.Euler(0f, 12f, 0f), Vector3.one,
                new Vector3(1.45f, 1.25f, 1.05f), new Vector3(0f, 0.62f, 0f), mats);
            PlaceSelmanModel("Sultanbeyli_SpeedBumpSign.fbx", "Sign_Kasis_CicekciCorner", propsRoot.transform,
                new Vector3(-6.8f, 0.04f, -5.2f), Quaternion.Euler(0f, -90f, 0f), Vector3.one,
                new Vector3(0.25f, 2.2f, 0.25f), new Vector3(0f, 1.1f, 0f), mats);

            // 3. Authentic Istanbul Simitçi Cart + Street Vendor
            GameObject simitGo = new GameObject("Selman_SimitciTezgahi");
            simitGo.transform.SetParent(propsRoot.transform, false);
            simitGo.transform.SetPositionAndRotation(new Vector3(-56.5f, 0.04f, 4.9f), Quaternion.Euler(0f, 180f, 0f));
            InstantiateBlenderModel("Istanbul_SimitCart.fbx", "SimitCart_Mesh", simitGo.transform, mats);
            BoxCollider sc = simitGo.AddComponent<BoxCollider>();
            sc.center = new Vector3(0f, 0.85f, 0f);
            sc.size = new Vector3(1.5f, 1.7f, 1.0f);
            MarkStaticEnvironment(simitGo);

            CreateStreetVendorStall(
                "Vendor_SelmanSimit", "vendor_bakery", "Hasan Usta", "Simitçi", "TAZE SİMİT & ÇAY",
                propsRoot.transform, new Vector3(-24.0f, 0.04f, 5.2f), Quaternion.Euler(0f, 180f, 0f),
                mats.TownAwningTerracotta, BlenderCharacterVariant.CasualMale,
                mats.PedShirtMustard, mats.CharPantsDenim, mats.CharSkinMedium, mats.CharHairBrown, mats);

            // 4. Side-Job Start Rings
            CreateJobStartRing("JobBoard_Pizza", "job_board_pizza", JobType.PizzaDelivery, "job.pizza.name", "job.pizza.loc",
                propsRoot.transform, new Vector3(8.5f, 0.06f, 4.6f), 4.0f, mats.JobRingGlow);
            CreateJobStartRing("JobBoard_Taxi", "job_board_taxi", JobType.Taxi, "job.taxi.name", "job.taxi.loc",
                propsRoot.transform, new Vector3(-36.0f, 0.06f, -4.6f), 4.0f, mats.JobRingGlow);
            CreateJobStartRing("JobBoard_Cleanup", "job_board_cleanup", JobType.StreetCleanup, "job.cleanup.name", "job.cleanup.loc",
                propsRoot.transform, new Vector3(52.0f, 0.06f, 4.6f), 3.6f, mats.JobRingGlow);
            CreateJobStartRing("JobBoard_HousePainting", "job_board_painting", JobType.HousePainting, "job.paint.name", "job.paint.loc",
                propsRoot.transform, new Vector3(19.5f, 0.06f, -5.2f), 3.6f, mats.JobRingGlow);

            // 5. Fuel Pump & Mailboxes/Vending
            CreateFuelPump("Selman_FuelPump", "fuel_pump_01", propsRoot.transform, new Vector3(-84.5f, 0.04f, 12.0f), mats);
            CreateMailbox("Selman_Mailbox_48", "mailbox_01", propsRoot.transform, new Vector3(-60.0f, 0.04f, 5.3f), Quaternion.Euler(0f, 180f, 0f), mats);
            CreateVendingMachine("Selman_Vending_58", "vending_selman_01", propsRoot.transform, new Vector3(12.2f, 0.04f, 5.4f), Quaternion.Euler(0f, 180f, 0f), mats);
        }

        private static void BuildSelmanParkedCars(Transform parent, PrefabSet prefabs)
        {
            GameObject carsRoot = new GameObject("Selman_PhotoMatched_DrivableCars");
            carsRoot.transform.SetParent(parent, false);

            (GameObject prefab, Vector3 pos, float yaw, string label)[] spots =
            {
                (prefabs.TownCarWhiteVan,          new Vector3(-44.0f, 0.25f,  2.4f),  90f, "Car_No52_WhiteDeliveryVan"),
                (prefabs.TownCarRedHatchback,      new Vector3(-74.5f, 0.25f, -2.5f), 270f, "Car_No50_RedVanHatch"),
                (prefabs.TownCarCharcoalSUV,       new Vector3(-2.5f,  0.25f,  2.4f),  90f, "Car_No58_MPV"),
                (prefabs.TownCarGreenSedan,        new Vector3(-13.5f, 0.25f,  7.5f), 180f, "Car_No63_DrivewaySedan"),
                (prefabs.TownCarBlueHatchback,     new Vector3(41.0f,  0.25f, -2.4f), 270f, "Car_No66S_HafsaHatchback"),
                (prefabs.TownCarGreenSedan,        new Vector3(57.8f,  0.25f, 11.5f),   0f, "Car_NutukSk_SilverSedan"),
                (prefabs.TownCarBlueSportsCoupe,   new Vector3(71.5f,  0.25f, -2.5f), 270f, "Car_No81S_DarkBlueSedan"),
                (prefabs.TownCarCharcoalSUV,       new Vector3(92.5f,  0.25f, -2.6f), 270f, "Car_No95_DarkBlueSUV"),
                (prefabs.TownCarYellowSportsCoupe, new Vector3(109.0f, 0.25f,  2.3f),  90f, "Car_SelmanEast_YellowCoupe")
            };

            for (int i = 0; i < spots.Length; i++)
            {
                if (spots[i].prefab == null) continue;
                GameObject car = PlacePrefabInstance(spots[i].prefab, carsRoot.transform, spots[i].pos, Quaternion.Euler(0f, spots[i].yaw, 0f));
                car.name = spots[i].label;
            }
        }

        private static void BuildSelmanPedestrians(Transform parent, MaterialSet mats, List<SeatInteractable> seats)
        {
            GameObject pedsRoot = new GameObject("Selman_NeighborhoodCitizens");
            pedsRoot.transform.SetParent(parent, false);

            (string name, string role, BlenderCharacterVariant variant, Material shirt, Material pants, Material skin, Material hair, float height, Vector3[] route, string[] lines, bool brave)[] citizens =
            {
                (
                    "Yılmaz Usta", "İnşaat Ustası · 48/52 Sk.", BlenderCharacterVariant.Worker,
                    mats.PedShirtMustard, mats.CharPantsKhaki, mats.CharSkinMedium, mats.CharHair, 1.81f,
                    new[] { new Vector3(-72f, 0.06f, 4.7f), new Vector3(-42f, 0.06f, 4.7f), new Vector3(-28f, 0.06f, 4.7f), new Vector3(-56f, 0.06f, 4.7f) },
                    new[]
                    {
                        "Kolay gelsin hemşehrim! 48 numaranın arkasındaki 3 katlı tuğla binanın sıvasına hazırlanıyoruz.",
                        "Çatıya çıktın mı ta Aydos Tepesi ve Aydos Ormanı ayaklarının altında görünüyor, manzarası şahane.",
                        "52 numaranın üst katı da tuğla kaldı, Hamidiye Mahallesi her sene biraz daha yenileniyor."
                    },
                    true
                ),
                (
                    "Zeynep", "İTO Şehit Er Dursun Sıvaz Öğrencisi", BlenderCharacterVariant.CasualFemale,
                    mats.PedShirtTeal, mats.CharPantsDenim, mats.CharSkin, mats.CharHairBrown, 1.64f,
                    new[] { new Vector3(-62f, 0.06f, -4.7f), new Vector3(-20f, 0.06f, -4.7f), new Vector3(14f, 0.06f, 4.7f), new Vector3(-30f, 0.06f, 4.7f) },
                    new[]
                    {
                        "Aziziye Caddesi'ndeki İTO Şehit Er Dursun Sıvaz İlkokulu'ndan çıkıp Selman Sokağı'ndan eve yürüyorum!",
                        "58 numaradaki Hüseyin Abi'nin bakkalında çok güzel sıcak tost ve soğuk ayran var.",
                        "Kilit parke kaldırımda yürürken 48 numaranın mavi kapısının önündeki kedileri seviyorum."
                    },
                    false
                ),
                (
                    "Ali Abi", "Sultanbeyli - Gölet Minibüs Şoförü", BlenderCharacterVariant.CasualMale,
                    mats.CharShirt, mats.CharPants, mats.CharSkinWarm, mats.CharHair, 1.78f,
                    new[] { new Vector3(-92f, 0.06f, -4.7f), new Vector3(-44f, 0.06f, -4.7f), new Vector3(-8f, 0.06f, -4.7f), new Vector3(-44f, 0.06f, -4.7f) },
                    new[]
                    {
                        "Aman dikkat et kardeş, Çiçekçi Sokağı ağzındaki kasise hızlı girersen arabanın ön takımı dağılır!",
                        "Seyyid Kutup Caddesi'nden çıkıp Zübeyde Hanım ve Eyyübi Caddesi üstünden Gölet'e bağlanıyoruz.",
                        "Sabah işe yetişenler Gölet Girişi durağında sıraya giriyor, Sultanbeyli sabahları çok hareketli olur."
                    },
                    true
                ),
                (
                    "Ayşe Abla", "54 Numara Komşusu", BlenderCharacterVariant.CasualFemale,
                    mats.PedShirtRed, mats.CharPants, mats.CharSkin, mats.CharHairBlonde, 1.69f,
                    new[] { new Vector3(-34f, 0.06f, 4.8f), new Vector3(-4f, 0.06f, 4.8f), new Vector3(20f, 0.06f, 4.8f), new Vector3(-12f, 0.06f, 4.8f) },
                    new[]
                    {
                        "Merhaba komşu! Hamidiye Semt Pazarı'ndan geliyorum, poşetler ağırdı biraz soluklandım.",
                        "58 numaranın kapısındaki siyah-altın kemer sokağın en meşhur yeridir, tarif ederken hep onu söyleriz.",
                        "Bizim Selman Sokağı'nda kapılarımız komşulara hep açıktır, istediğin eve buyurup çay içebilirsin!"
                    },
                    false
                ),
                (
                    "Kurye Emre", "Hamidiye Mahalle Kuryesi", BlenderCharacterVariant.CasualMale,
                    mats.PedShirtOlive, mats.CharPantsDenim, mats.CharSkinMedium, mats.CharHairBrown, 1.77f,
                    new[] { new Vector3(-52f, 0.06f, -4.7f), new Vector3(10f, 0.06f, -4.7f), new Vector3(68f, 0.06f, -4.7f), new Vector3(8f, 0.06f, 4.7f) },
                    new[]
                    {
                        "Selam! Nutuk Sokağı'ndan Mürşit Caddesi'ne, oradan Selman Sokağı'na paket yetiştiriyorum.",
                        "Sokağın kapı numaraları biraz karışık: 46, 48, 52, 58, 63, 73, 66, 81, 95 derken artık ezberledim.",
                        "Paran azalırsa sokaktaki yeşil çemberlerden kuryelik ya da taksi işi alabilirsin, iyi kazandırır!"
                    },
                    true
                ),
                (
                    "Kerem Teknisyen", "AYEDAŞ Hat Bakım", BlenderCharacterVariant.Worker,
                    mats.PedShirtMustard, mats.CharPants, mats.CharSkinMedium, mats.CharHair, 1.80f,
                    new[] { new Vector3(64f, 0.06f, 4.7f), new Vector3(108f, 0.06f, 4.7f), new Vector3(108f, 0.06f, -4.7f), new Vector3(64f, 0.06f, -4.7f) },
                    new[]
                    {
                        "Selman Sokağı'nın doğu ucundaki büyük yüksek gerilim direğini ve sokak hatlarını kontrol ediyorum.",
                        "95 numaranın köşesindeki sarı-beyaz kaldırımdan Abdülkadir Udeh Sokağı'na inerken Hz. Osman Camii görünüyor.",
                        "66 numaranın önündeki kabloları yeni gerdik, akşam sokak lambaları pırıl pırıl yanıyor."
                    },
                    true
                ),
                (
                    "Burak", "Hamidiye Gençliği", BlenderCharacterVariant.CasualMale,
                    mats.PedShirtBurgundy, mats.CharPantsDenim, mats.CharSkin, mats.CharHair, 1.79f,
                    new[] { new Vector3(20f, 0.06f, -4.8f), new Vector3(74f, 0.06f, -4.8f), new Vector3(82f, 0.06f, 4.7f), new Vector3(32f, 0.06f, 4.7f) },
                    new[]
                    {
                        "Abi hoş geldin! Hafta sonu arkadaşlarla Sultanbeyli Gölet Parkı'nda halı saha yapıyoruz.",
                        "81 numaranın karşısındaki mavi apartmanda oturuyorum, kapı açık buyur içeri bir çay iç!",
                        "Canın sıkılırsa 62 numaralı eve girip duvarlarını istediğin renge boyayabilirsin."
                    },
                    false
                ),
                (
                    "Fatma Teyze", "81 Selman Sk. Bahçeli Ev", BlenderCharacterVariant.CasualFemale,
                    mats.PedShirtTeal, mats.CharPantsKhaki, mats.CharSkinWarm, mats.CharHairBlonde, 1.66f,
                    new[] { new Vector3(66f, 0.06f, 4.8f), new Vector3(92f, 0.06f, 4.8f), new Vector3(66f, 0.06f, 4.8f), new Vector3(76f, 0.06f, 4.8f) },
                    new[]
                    {
                        "Ah yavrum, 81 numaranın ön bahçesindeki erik ağacı bahar gelince bembeyaz çiçek açar.",
                        "Nutuk Sokağı köşesindeki kırmızı yangın musluğunun orada mahallenin kedilerine mama bırakıyorum.",
                        "95 numaradaki Rıza Abi'nin çay ocağında akşamları bütün mahalle toplanır."
                    },
                    false
                )
            };

            for (int i = 0; i < citizens.Length; i++)
            {
                var c = citizens[i];
                GameObject pedGo = new GameObject($"Selman_Citizen_{i + 1:00}_{c.name.Replace(" ", "")}");
                pedGo.transform.SetParent(pedsRoot.transform, false);
                pedGo.transform.position = c.route[0];

                CapsuleCollider col = pedGo.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.9f, 0f);
                col.radius = 0.34f;
                col.height = 1.76f;

                ProceduralHumanoid hum = BuildBlenderHumanoid(
                    pedGo.transform, mats, c.variant, c.shirt, c.pants, c.skin, c.hair, c.height);

                TownPedestrian tp = pedGo.AddComponent<TownPedestrian>();
                tp.ConfigureSchedule(c.route, seats.ToArray(), hum, 1.24f + (i % 3) * 0.14f, 0);
                tp.ConfigureCitizen(c.name, c.role, c.lines, brave: c.brave);

                InteractableTrigger trig = pedGo.AddComponent<InteractableTrigger>();
                trig.Configure(InteractableKind.TownCitizen, $"citizen_selman_{i + 1:00}", "ui.btn.chat", 2.9f);
            }
        }

        private static void SpawnSelmanResidentNpc(
            string objName,
            string displayName,
            string role,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            BlenderCharacterVariant variant,
            Material shirtMat,
            Material pantsMat,
            Material skinMat,
            Material hairMat,
            string[] chatLines,
            MaterialSet mats,
            string vendorShopId = "")
        {
            GameObject go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;

            CapsuleCollider col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.radius = 0.34f;
            col.height = 1.76f;

            ProceduralHumanoid hum = BuildBlenderHumanoid(go.transform, mats, variant, shirtMat, pantsMat, skinMat, hairMat,
                variant == BlenderCharacterVariant.CasualFemale ? 1.68f : 1.78f);
            hum.SetAutoRandomWave(true);

            TownPedestrian tp = go.AddComponent<TownPedestrian>();
            Vector3 worldPos = go.transform.position;
            tp.ConfigureSchedule(new[] { worldPos }, new SeatInteractable[0], hum, 0f, 0);
            tp.ConfigureCitizen(displayName, role, chatLines, brave: true, shopId: vendorShopId);

            InteractableTrigger trig = go.AddComponent<InteractableTrigger>();
            string promptKey = string.IsNullOrEmpty(vendorShopId) ? "ui.btn.chat" : "ui.btn.vendor";
            trig.Configure(InteractableKind.TownCitizen, objName.ToLowerInvariant(), promptKey, 3.2f);
        }

        private static void AddHollowRoomColliders(
            GameObject host,
            float xMin,
            float xMax,
            float zBack,
            float zFront,
            float height,
            float wallThick,
            float doorX,
            float doorW,
            float indoorMaxDistance = 1.90f,
            float lightRange = 14f)
        {
            float w = xMax - xMin;
            float d = zFront - zBack;
            float cx = (xMin + xMax) * 0.5f;
            float cz = (zBack + zFront) * 0.5f;
            float cy = height * 0.5f;

            // Thin perimeter walls (interior stays 100% open to walk through!)
            AddLocalBoxCollider(host, new Vector3(cx, cy, zBack + wallThick * 0.5f), new Vector3(w, height, wallThick));
            AddLocalBoxCollider(host, new Vector3(xMax - wallThick * 0.5f, cy, cz), new Vector3(wallThick, height, d));
            AddLocalBoxCollider(host, new Vector3(xMin + wallThick * 0.5f, cy, cz), new Vector3(wallThick, height, d));

            // Solid Ceiling collider so ThirdPersonCameraRig SphereCast & overhead Raycast ALWAYS stay cleanly below ceiling!
            AddLocalBoxCollider(host, new Vector3(cx, height - 0.08f, cz), new Vector3(w, 0.24f, d));

            float dLo = doorX - doorW * 0.5f;
            float dHi = doorX + doorW * 0.5f;
            float fz = zFront - wallThick * 0.5f;
            if (dLo > xMin + 0.08f)
            {
                AddLocalBoxCollider(host, new Vector3((xMin + dLo) * 0.5f, cy, fz), new Vector3(dLo - xMin, height, wallThick));
            }
            if (xMax > dHi + 0.08f)
            {
                AddLocalBoxCollider(host, new Vector3((dHi + xMax) * 0.5f, cy, fz), new Vector3(xMax - dHi, height, wallThick));
            }

            // Automatic InteriorZone trigger + bright indoor lamp so camera pulls smoothly indoors and lights up every room!
            GameObject zoneGo = new GameObject("InteriorZone_Trigger");
            zoneGo.transform.SetParent(host.transform, false);
            BoxCollider zoneBox = zoneGo.AddComponent<BoxCollider>();
            zoneBox.isTrigger = true;
            zoneBox.center = new Vector3(cx, height * 0.48f, cz);
            zoneBox.size = new Vector3(Mathf.Max(1.0f, w - wallThick * 1.5f), height - 0.15f, Mathf.Max(1.0f, d - wallThick * 1.5f));

            GameObject lampGo = new GameObject("InteriorZone_CeilingLamp");
            lampGo.transform.SetParent(zoneGo.transform, false);
            lampGo.transform.localPosition = new Vector3(cx, height - 0.35f, cz);
            Light ceilingLight = lampGo.AddComponent<Light>();
            ceilingLight.type = LightType.Point;
            ceilingLight.color = new Color(1.0f, 0.93f, 0.80f);
            ceilingLight.range = lightRange;
            ceilingLight.intensity = 3.2f;
            ceilingLight.shadows = LightShadows.None;

            InteriorZone iz = zoneGo.AddComponent<InteriorZone>();
            iz.Configure(new[] { ceilingLight }, indoorMaxDistance: indoorMaxDistance);
        }

        private static void AddLocalBoxCollider(GameObject host, Vector3 center, Vector3 size)
        {
            BoxCollider bc = host.AddComponent<BoxCollider>();
            bc.center = center;
            bc.size = size;
        }

        private static GameObject PlaceInteriorModel(
            string fbxName,
            string objName,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            MaterialSet mats,
            Vector3? colliderSize = null)
        {
            GameObject root = new GameObject(objName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = localRot;

            InstantiateBlenderModel(fbxName, $"{objName}_Mesh", root.transform, mats);

            if (colliderSize.HasValue && colliderSize.Value.sqrMagnitude > 0.01f)
            {
                BoxCollider bc = root.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, colliderSize.Value.y * 0.5f, 0f);
                bc.size = colliderSize.Value;
            }

            MarkStaticEnvironment(root);
            return root;
        }

        private static void CreateInteriorLight(string name, Transform parent, Vector3 localPos, Color color, float range, float intensity)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.enabled = true;
        }

        private static void BuildSelmanCorridorBoundary(Transform parent, MaterialSet mats)
        {
            GameObject boundRoot = new GameObject("Hamidiye_DistrictPerimeter");
            boundRoot.transform.SetParent(parent, false);

            CreateStaticBlock("Bound_North", boundRoot.transform, new Vector3(-8f, 1.2f, 174f), new Vector3(390f, 2.4f, 0.8f), mats.TownHedge);
            CreateStaticBlock("Bound_South", boundRoot.transform, new Vector3(-8f, 1.2f, -170f), new Vector3(390f, 2.4f, 0.8f), mats.TownHedge);
            CreateStaticBlock("Bound_West", boundRoot.transform, new Vector3(-202f, 1.2f, 2f), new Vector3(0.8f, 2.4f, 346f), mats.TownHedge);
            CreateStaticBlock("Bound_East", boundRoot.transform, new Vector3(186f, 1.2f, 2f), new Vector3(0.8f, 2.4f, 346f), mats.TownHedge);
        }

        private static GameObject PlaceSelmanModel(
            string fbxName,
            string objName,
            Transform parent,
            Vector3 pos,
            Quaternion rot,
            Vector3 scale,
            Vector3 colliderSize,
            Vector3 colliderCenter,
            MaterialSet mats)
        {
            GameObject root = new GameObject(objName);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, rot);
            root.transform.localScale = scale;

            GameObject mesh = InstantiateBlenderModel(fbxName, $"{objName}_Mesh", root.transform, mats);
            if (mesh == null)
            {
                CreatePrimitiveChild("Fallback", PrimitiveType.Cube, root.transform,
                    colliderCenter, colliderSize, mats.PlasterTrim, removeCollider: true);
            }

            if (colliderSize.sqrMagnitude > 0.01f)
            {
                BoxCollider col = root.AddComponent<BoxCollider>();
                col.center = colliderCenter;
                col.size = colliderSize;
            }

            MarkStaticEnvironment(root);
            return root;
        }
    }
}
