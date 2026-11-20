using System;
using System.Collections.Generic;
using Gridroam.Jobs;
using Gridroam.Player;
using Gridroam.World;
using UnityEngine;

namespace Gridroam.Editor
{
    public static partial class GridroamPrototypeSetup
    {
        /// <summary>
        /// Builds the "Liman Kasabası" open-world coastal town:
        /// Main Street shops (incl. PİZZA, KAFE, MARKET), Town Square with fountain & clock tower,
        /// Coastal Gas Station (canopy, 2 fuel pumps, kiosk), 4 Side-Job boards, Vending Machines & Mailboxes,
        /// 24 enterable Residential Houses with sleepable beds, Park, Traffic Stop Lines, and 14 scheduled Pedestrians.
        /// </summary>
        private static List<TrafficStopLine> BuildSmallTown(Transform envParent, MaterialSet mats, PrefabSet prefabs)
        {
            GameObject townRoot = new GameObject("Zone_SmallTown_LimanKasabasi");
            townRoot.transform.SetParent(envParent, false);

            System.Random rng = new System.Random(1234);
            List<SeatInteractable> outdoorBenchSeats = new List<SeatInteractable>();

            BuildTownRoads(townRoot.transform, mats);
            BuildMainStreetShops(townRoot.transform, mats, rng);
            BuildGasStation(townRoot.transform, mats);
            BuildTownSquare(townRoot.transform, mats, prefabs, outdoorBenchSeats);
            BuildResidentialAndPark(townRoot.transform, mats, rng, outdoorBenchSeats);
            List<TrafficStopLine> stopLines = BuildTownStreetFurniture(townRoot.transform, mats, prefabs, rng, outdoorBenchSeats);
            BuildSideJobStartPoints(townRoot.transform, mats);
            BuildVendingAndMailboxes(townRoot.transform, mats);
            BuildTownPedestrians(townRoot.transform, mats, outdoorBenchSeats);

            return stopLines;
        }

        private static GameObject CreateTiledBox(
            string name,
            Transform parent,
            Vector3 center,
            Vector3 size,
            Quaternion rot,
            Material mat,
            float metersPerTile,
            bool collider = true,
            bool staticBatch = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.rotation = rot;
            go.transform.localScale = Vector3.one;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            Vector2 matScale = Vector2.one;
            if (mat != null && mat.HasProperty("_BaseMap"))
            {
                Vector2 s = mat.GetTextureScale("_BaseMap");
                if (Mathf.Abs(s.x) > 0.001f) matScale.x = s.x;
                if (Mathf.Abs(s.y) > 0.001f) matScale.y = s.y;
            }

            mf.sharedMesh = BuildTiledBoxMesh(name + "_Mesh", size, Mathf.Max(0.1f, metersPerTile), matScale);
            if (mat != null)
            {
                mr.sharedMaterial = mat;
            }

            if (collider)
            {
                BoxCollider bc = go.AddComponent<BoxCollider>();
                bc.center = Vector3.zero;
                bc.size = size;
            }

            if (staticBatch)
            {
                MarkStaticEnvironment(go);
            }
            else
            {
                MarkStaticPaintable(go);
            }

            return go;
        }

        private static Mesh BuildTiledBoxMesh(string meshName, Vector3 size, float metersPerTile, Vector2 matScale)
        {
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            float hz = size.z * 0.5f;

            float invTileU = 1f / (metersPerTile * matScale.x);
            float invTileV = 1f / (metersPerTile * matScale.y);

            Vector3[] verts = new Vector3[24];
            Vector3[] norms = new Vector3[24];
            Vector2[] uvs = new Vector2[24];
            int[] tris = new int[36];

            int vIdx = 0;
            int tIdx = 0;

            void AddFace(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 normal, float width, float height)
            {
                int start = vIdx;
                float uMax = width * invTileU;
                float vMax = height * invTileV;

                verts[vIdx] = bl; norms[vIdx] = normal; uvs[vIdx] = new Vector2(0f, 0f); vIdx++;
                verts[vIdx] = tl; norms[vIdx] = normal; uvs[vIdx] = new Vector2(0f, vMax); vIdx++;
                verts[vIdx] = tr; norms[vIdx] = normal; uvs[vIdx] = new Vector2(uMax, vMax); vIdx++;
                verts[vIdx] = br; norms[vIdx] = normal; uvs[vIdx] = new Vector2(uMax, 0f); vIdx++;

                tris[tIdx++] = start;
                tris[tIdx++] = start + 1;
                tris[tIdx++] = start + 2;
                tris[tIdx++] = start;
                tris[tIdx++] = start + 2;
                tris[tIdx++] = start + 3;
            }

            AddFace(new Vector3(-hx, -hy, -hz), new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, -hy, -hz), Vector3.back, size.x, size.y);
            AddFace(new Vector3(hx, -hy, hz), new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx, -hy, hz), Vector3.forward, size.x, size.y);
            AddFace(new Vector3(hx, -hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz), new Vector3(hx, -hy, hz), Vector3.right, size.z, size.y);
            AddFace(new Vector3(-hx, -hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx, hy, -hz), new Vector3(-hx, -hy, -hz), Vector3.left, size.z, size.y);
            AddFace(new Vector3(-hx, hy, -hz), new Vector3(-hx, hy, hz), new Vector3(hx, hy, hz), new Vector3(hx, hy, -hz), Vector3.up, size.x, size.z);
            AddFace(new Vector3(-hx, -hy, hz), new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz), new Vector3(hx, -hy, hz), Vector3.down, size.x, size.z);

            Mesh mesh = new Mesh
            {
                name = meshName,
                vertices = verts,
                normals = norms,
                uv = uvs,
                triangles = tris
            };
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildTownRoads(Transform parent, MaterialSet mats)
        {
            GameObject roadsRoot = new GameObject("Town_Roads");
            roadsRoot.transform.SetParent(parent, false);

            CreateStaticBlock("Road_ResidentialLane", roadsRoot.transform,
                new Vector3(-46f, 0.025f, 125f), new Vector3(6f, 0.08f, 230f),
                mats.RoadAsphaltMain, addCollider: false);

            CreateStaticBlock("Road_CrossStreet_South_WestExt", roadsRoot.transform,
                new Vector3(-38.5f, 0.025f, 92f), new Vector3(21f, 0.08f, 9f),
                mats.RoadAsphaltCross, addCollider: false);
            CreateStaticBlock("Road_CrossStreet_North_WestExt", roadsRoot.transform,
                new Vector3(-38.5f, 0.025f, 174f), new Vector3(21f, 0.08f, 9f),
                mats.RoadAsphaltCross, addCollider: false);
        }

        private static void BuildMainStreetShops(Transform parent, MaterialSet mats, System.Random rng)
        {
            GameObject shopsRoot = new GameObject("Town_MainStreetShops");
            shopsRoot.transform.SetParent(parent, false);

            Material[] facadeMats = { mats.BuildingFacadeA, mats.BuildingFacadeB, mats.BuildingFacadeC, mats.GarageBrick, mats.PlasterTrim };
            Material[] awningMats = { mats.TownAwningTerracotta, mats.TownAwningGreen, mats.TownAwningNavy, mats.TownAwningCream };

            // North east-facing shop row stops at z = 213.5 so z = 216..237 hosts the Coastal Gas Station cleanly
            Vector2[] eastZSegments =
            {
                new Vector2(20f, 85f),
                new Vector2(100f, 120f),
                new Vector2(148f, 166f),
                new Vector2(182f, 213.5f)
            };

            Vector2[] alleyZSegments =
            {
                new Vector2(20f, 85f),
                new Vector2(100f, 120f),
                new Vector2(148f, 166f),
                new Vector2(182f, 235f)
            };

            int shopIdx = 0;
            for (int segIdx = 0; segIdx < eastZSegments.Length; segIdx++)
            {
                Vector2 seg = eastZSegments[segIdx];
                float cursorZ = seg.x;
                int segLocalIdx = 0;

                while (cursorZ < seg.y - 5.5f)
                {
                    bool makeEnterable = segLocalIdx == 0 && (segIdx == 0 || segIdx == 1 || segIdx == 3);

                    float remaining = seg.y - cursorZ;
                    float widthZ;
                    float depthX;
                    if (makeEnterable)
                    {
                        widthZ = Mathf.Min(remaining, 9.2f);
                        depthX = 9.4f;
                    }
                    else
                    {
                        widthZ = remaining < 12.5f
                            ? remaining
                            : Mathf.Min(remaining, 7.0f + (float)rng.NextDouble() * 3.0f);
                        depthX = 9.0f + (float)rng.NextDouble() * 2.0f;
                    }

                    int storeys = 2 + rng.Next(3);
                    float height = 3.6f + (storeys - 1) * 3.0f + (float)rng.NextDouble() * 0.45f;

                    float centerZ = cursorZ + widthZ * 0.5f;
                    float eastFaceX = 13.4f;
                    float centerX = eastFaceX - depthX * 0.5f;

                    Material facade = facadeMats[shopIdx % facadeMats.Length];
                    float tileMeters = facade == mats.GarageBrick ? 1.5f : 3.0f;
                    Material awning = awningMats[(shopIdx + rng.Next(2)) % awningMats.Length];
                    bool pitchedRoof = (shopIdx % 2 == 0) && storeys <= 3;

                    if (makeEnterable)
                    {
                        EnterableShopKind sKind = segIdx == 0
                            ? EnterableShopKind.Pizzeria
                            : (segIdx == 1 ? EnterableShopKind.Cafe : EnterableShopKind.Market);
                        string sId = segIdx == 0
                            ? "shop_pizza_01"
                            : (segIdx == 1 ? "shop_cafe_01" : "shop_market_01");
                        Material roleAwning = segIdx == 0
                            ? mats.TownAwningTerracotta
                            : (segIdx == 1 ? mats.TownAwningGreen : mats.TownAwningNavy);

                        BuildEnterableShopBuilding(
                            $"Shop_Main_{shopIdx + 1:00}_{sKind}",
                            sId,
                            sKind,
                            shopsRoot.transform,
                            new Vector3(centerX, 0f, centerZ),
                            depthX, widthZ, height, storeys,
                            facade, tileMeters, roleAwning, pitchedRoof, mats,
                            shopSeed: 4200 + shopIdx * 53);
                    }
                    else
                    {
                        BuildShopBuilding(
                            $"Shop_Main_{shopIdx + 1:00}",
                            shopsRoot.transform,
                            new Vector3(centerX, 0f, centerZ),
                            depthX, widthZ, height, storeys,
                            facingEast: true,
                            facade, tileMeters, awning, pitchedRoof, mats, rng);
                    }

                    cursorZ += widthZ;
                    shopIdx++;
                    segLocalIdx++;
                }

                // West-facing Back-Alley companion row
                Vector2 alleySeg = alleyZSegments[segIdx];
                float alleyZ = alleySeg.x;
                int alleyLocalIdx = 0;
                while (alleyZ < alleySeg.y - 6.0f)
                {
                    float remaining = alleySeg.y - alleyZ;
                    float widthZ = remaining < 13.5f
                        ? remaining
                        : Mathf.Min(remaining, 8.0f + (float)rng.NextDouble() * 3.0f);
                    float depthX = 9.0f + (float)rng.NextDouble() * 1.5f;
                    int storeys = 2 + rng.Next(2);
                    float height = 3.2f + (storeys - 1) * 2.95f;

                    float centerZ = alleyZ + widthZ * 0.5f;
                    float westFaceX = -18.8f;
                    float centerX = westFaceX + depthX * 0.5f;

                    Material facade = facadeMats[(shopIdx + alleyLocalIdx + 2) % facadeMats.Length];
                    float tileMeters = facade == mats.GarageBrick ? 1.5f : 3.0f;
                    Material awning = awningMats[(alleyLocalIdx + 1) % awningMats.Length];

                    BuildShopBuilding(
                        $"Shop_Alley_{shopIdx + 1:00}_{alleyLocalIdx + 1}",
                        shopsRoot.transform,
                        new Vector3(centerX, 0f, centerZ),
                        depthX, widthZ, height, storeys,
                        facingEast: false,
                        facade, tileMeters, awning, pitchedRoof: (alleyLocalIdx % 2 == 1), mats, rng);

                    alleyZ += widthZ;
                    alleyLocalIdx++;
                }
            }
        }

        private static void BuildShopBuilding(
            string name,
            Transform parent,
            Vector3 basePos,
            float depthX,
            float widthZ,
            float height,
            int storeys,
            bool facingEast,
            Material facadeMat,
            float metersPerTile,
            Material awningMat,
            bool pitchedRoof,
            MaterialSet mats,
            System.Random rng)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = basePos;

            float frontSign = facingEast ? 1f : -1f;
            float frontX = basePos.x + frontSign * (depthX * 0.5f);
            float backX = basePos.x - frontSign * (depthX * 0.5f);

            CreateTiledBox("Body", root.transform,
                new Vector3(basePos.x, height * 0.5f, basePos.z),
                new Vector3(depthX, height, widthZ),
                Quaternion.identity, facadeMat, metersPerTile, collider: true);

            bool litShop = rng.NextDouble() < 0.65;
            Material shopGlassMat = litShop ? mats.WindowLitWarm : mats.SedanGlass;
            GameObject shopGlass = CreateTiledBox("ShopfrontGlass", root.transform,
                new Vector3(frontX + frontSign * 0.05f, 1.55f, basePos.z),
                new Vector3(0.10f, 2.55f, Mathf.Max(2.5f, widthZ - 1.1f)),
                Quaternion.identity, shopGlassMat, 2.0f, collider: false, staticBatch: !litShop);
            if (litShop)
            {
                AttachWindowNightLight(shopGlass);
            }

            CreateTiledBox("ShopDoor", root.transform,
                new Vector3(frontX + frontSign * 0.08f, 1.25f, basePos.z + (rng.Next(2) == 0 ? -1.4f : 1.4f)),
                new Vector3(0.08f, 2.30f, 1.15f),
                Quaternion.identity, mats.TownDoorDark, 2.0f, collider: false);

            float awningAngle = facingEast ? -22f : 22f;
            CreateTiledBox("Awning", root.transform,
                new Vector3(frontX + frontSign * 0.68f, 3.05f, basePos.z),
                new Vector3(1.38f, 0.11f, Mathf.Max(2.6f, widthZ - 0.9f)),
                Quaternion.Euler(0f, 0f, awningAngle), awningMat, 2.0f, collider: false);

            CreateTiledBox("SignBoard", root.transform,
                new Vector3(frontX + frontSign * 0.12f, 3.55f, basePos.z),
                new Vector3(0.14f, 0.56f, Mathf.Max(2.4f, widthZ - 1.6f)),
                Quaternion.identity, mats.SedanTrimDark, 2.0f, collider: false);

            int winCols = Mathf.Max(2, Mathf.FloorToInt((widthZ - 1.2f) / 2.2f));
            float winStep = (widthZ - 1.6f) / Mathf.Max(1, winCols);
            float winStartZ = basePos.z - (widthZ - 1.6f) * 0.5f + winStep * 0.5f;

            for (int floor = 1; floor < storeys; floor++)
            {
                float wy = 1.9f + floor * 3.0f;
                if (wy > height - 0.9f) break;

                for (int c = 0; c < winCols; c++)
                {
                    float wz = winStartZ + c * winStep;
                    bool litWin = rng.NextDouble() < 0.50;
                    Material wMat = litWin ? mats.WindowLitWarm : mats.SedanGlass;
                    GameObject wGo = CreateTiledBox($"Win_F{floor}_{c}", root.transform,
                        new Vector3(frontX + frontSign * 0.05f, wy, wz),
                        new Vector3(0.10f, 1.35f, 1.10f),
                        Quaternion.identity, wMat, 2.0f, collider: false, staticBatch: !litWin);
                    if (litWin)
                    {
                        AttachWindowNightLight(wGo);
                    }
                }

                if (floor == 1 || floor == storeys - 1)
                {
                    for (int b = 0; b < 2; b++)
                    {
                        float bz = basePos.z + (b == 0 ? -widthZ * 0.24f : widthZ * 0.24f);
                        bool litBack = rng.NextDouble() < 0.35;
                        Material bwMat = litBack ? mats.WindowLitWarm : mats.SedanGlass;
                        GameObject bwGo = CreateTiledBox($"BackWin_F{floor}_{b}", root.transform,
                            new Vector3(backX - frontSign * 0.05f, wy, bz),
                            new Vector3(0.10f, 1.20f, 1.05f),
                            Quaternion.identity, bwMat, 2.0f, collider: false, staticBatch: !litBack);
                        if (litBack)
                        {
                            AttachWindowNightLight(bwGo);
                        }
                    }
                }
            }

            CreateTiledBox("Cornice", root.transform,
                new Vector3(basePos.x, height + 0.24f, basePos.z),
                new Vector3(depthX + 0.40f, 0.48f, widthZ + 0.20f),
                Quaternion.identity, mats.PlasterTrim, 3.0f, collider: false);

            if (pitchedRoof)
            {
                float slopeLen = depthX * 0.58f;
                float roofPeakOffset = depthX * 0.25f;
                float roofY = height + 0.45f + roofPeakOffset * 0.45f;

                CreateTiledBox("RoofSlope_East", root.transform,
                    new Vector3(basePos.x + depthX * 0.24f, roofY, basePos.z),
                    new Vector3(slopeLen, 0.24f, widthZ + 0.30f),
                    Quaternion.Euler(0f, 0f, -24f), mats.TownRoofTile, 1.5f, collider: false);
                CreateTiledBox("RoofSlope_West", root.transform,
                    new Vector3(basePos.x - depthX * 0.24f, roofY, basePos.z),
                    new Vector3(slopeLen, 0.24f, widthZ + 0.30f),
                    Quaternion.Euler(0f, 0f, 24f), mats.TownRoofTile, 1.5f, collider: false);
            }
        }

        /// <summary>
        /// E4: Coastal Gas Station on the empty corner lot along Coastal Main Road west side
        /// just south of the Harbor Connector (x = 1..16.5, z = 216..237) — blocks zero roads!
        /// </summary>
        private static void BuildGasStation(Transform parent, MaterialSet mats)
        {
            GameObject station = new GameObject("Zone_CoastalGasStation");
            station.transform.SetParent(parent, false);

            // Concrete forecourt flush with sidewalk & road
            CreateTiledBox("Station_Forecourt", station.transform,
                new Vector3(9.6f, 0.08f, 226.5f), new Vector3(15.2f, 0.16f, 20.4f),
                Quaternion.identity, mats.HarborDock, 3.0f, collider: true);

            // Attendant Kiosk on west side of lot (x = 4.2)
            GameObject kiosk = new GameObject("Station_Kiosk");
            kiosk.transform.SetParent(station.transform, false);
            CreateTiledBox("Kiosk_Body", kiosk.transform,
                new Vector3(4.2f, 1.75f, 226.5f), new Vector3(4.2f, 3.5f, 7.6f),
                Quaternion.identity, mats.PlasterTrim, 2.5f, collider: true);
            CreateTiledBox("Kiosk_Roof", kiosk.transform,
                new Vector3(4.2f, 3.65f, 226.5f), new Vector3(4.8f, 0.32f, 8.2f),
                Quaternion.identity, mats.SedanTrimDark, 2.0f, collider: false);
            GameObject kioskWin = CreateTiledBox("Kiosk_Window", kiosk.transform,
                new Vector3(6.34f, 1.65f, 226.5f), new Vector3(0.08f, 1.85f, 4.8f),
                Quaternion.identity, mats.WindowLitWarm, 2.0f, collider: false, staticBatch: false);
            AttachWindowNightLight(kioskWin);

            // Canopy over pump island (x = 11.8, clearance 4.2 m)
            GameObject canopy = new GameObject("Station_Canopy");
            canopy.transform.SetParent(station.transform, false);
            CreateTiledBox("Canopy_RoofDeck", canopy.transform,
                new Vector3(11.8f, 4.45f, 226.5f), new Vector3(6.6f, 0.46f, 12.8f),
                Quaternion.identity, mats.SedanTrimDark, 2.5f, collider: true);
            CreateTiledBox("Canopy_TealFascia", canopy.transform,
                new Vector3(11.8f, 4.52f, 226.5f), new Vector3(6.72f, 0.24f, 12.92f),
                Quaternion.identity, mats.TownFabricTeal, 2.0f, collider: false);

            CreateWorldSignText(
                "AKARYAKIT",
                canopy.transform,
                new Vector3(15.20f, 4.45f, 226.5f),
                Quaternion.Euler(0f, -90f, 0f),
                new Vector2(8.0f, 0.42f),
                new Color(0.95f, 0.96f, 0.96f, 1f));

            CreateTiledBox("Canopy_Pillar_S", canopy.transform,
                new Vector3(11.8f, 2.25f, 222.0f), new Vector3(0.45f, 4.3f, 0.45f),
                Quaternion.identity, mats.Concrete, 1.5f, collider: true);
            CreateTiledBox("Canopy_Pillar_N", canopy.transform,
                new Vector3(11.8f, 2.25f, 231.0f), new Vector3(0.45f, 4.3f, 0.45f),
                Quaternion.identity, mats.Concrete, 1.5f, collider: true);

            // Canopy night downlights
            CreatePointLamp("Lamp_GasCanopy_S", canopy.transform, new Vector3(11.8f, 4.05f, 223.8f), new Color(0.95f, 0.96f, 1.0f), 18f, 2.6f);
            CreatePointLamp("Lamp_GasCanopy_N", canopy.transform, new Vector3(11.8f, 4.05f, 229.2f), new Color(0.95f, 0.96f, 1.0f), 18f, 2.6f);

            // Raised pump island curb (x = 11.8, cars pull into x ~ 14.8 east lane or x ~ 8.8 west lane)
            CreateTiledBox("Pump_Island", station.transform,
                new Vector3(11.8f, 0.21f, 226.5f), new Vector3(1.25f, 0.14f, 9.8f),
                Quaternion.identity, mats.Concrete, 1.5f, collider: true);

            // 2 Fuel Pumps
            CreateFuelPump("FuelPump_01", "fuel_pump_01", station.transform, new Vector3(11.8f, 0.28f, 223.8f), mats);
            CreateFuelPump("FuelPump_02", "fuel_pump_02", station.transform, new Vector3(11.8f, 0.28f, 229.2f), mats);
        }

        private static void CreateFuelPump(string name, string triggerId, Transform parent, Vector3 worldPos, MaterialSet mats)
        {
            GameObject pump = new GameObject(name);
            pump.transform.SetParent(parent, false);
            pump.transform.position = worldPos;

            CreateStaticPart("PumpBody", PrimitiveType.Cube, pump.transform,
                new Vector3(0f, 0.85f, 0f), Quaternion.identity, new Vector3(0.62f, 1.70f, 0.86f), mats.TownInteriorPaintPearl);
            CreateStaticPart("PumpHeader", PrimitiveType.Cube, pump.transform,
                new Vector3(0f, 1.78f, 0f), Quaternion.identity, new Vector3(0.68f, 0.24f, 0.92f), mats.TownFabricTeal);
            CreateStaticPart("MeterScreen_E", PrimitiveType.Cube, pump.transform,
                new Vector3(0.32f, 1.24f, 0f), Quaternion.identity, new Vector3(0.03f, 0.28f, 0.46f), mats.SedanTrimDark);
            CreateStaticPart("MeterScreen_W", PrimitiveType.Cube, pump.transform,
                new Vector3(-0.32f, 1.24f, 0f), Quaternion.identity, new Vector3(0.03f, 0.28f, 0.46f), mats.SedanTrimDark);
            CreateStaticPart("NozzleHose", PrimitiveType.Cylinder, pump.transform,
                new Vector3(0f, 0.82f, 0.48f), Quaternion.Euler(14f, 0f, 0f), new Vector3(0.05f, 0.46f, 0.05f), mats.WheelRubber);

            BoxCollider col = pump.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.95f, 0f);
            col.size = new Vector3(0.70f, 1.90f, 0.96f);
            MarkStaticEnvironment(pump);

            GameObject trigGo = new GameObject("FuelPumpTrigger");
            trigGo.transform.SetParent(pump.transform, false);
            trigGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            InteractableTrigger trig = trigGo.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.FuelPump, triggerId, "ui.btn.refuel", 4.6f);
        }

        /// <summary>
        /// E3: Places the 4 Side-Job start points, each with a glowing ground ring + JobBoard trigger.
        /// </summary>
        private static void BuildSideJobStartPoints(Transform parent, MaterialSet mats)
        {
            GameObject jobsRoot = new GameObject("Town_SideJobBoards");
            jobsRoot.transform.SetParent(parent, false);

            // a) Pizza Delivery right outside the "PİZZA" shop on Main Street (z = 24.6)
            CreateJobStartRing(
                "JobBoard_Pizza",
                "job_board_pizza",
                JobType.PizzaDelivery,
                "job.pizza.name",
                "job.pizza.loc",
                jobsRoot.transform,
                new Vector3(16.6f, 0.16f, 24.6f),
                radius: 4.6f,
                mats.JobRingGlow);

            // b) Taxi Rank at the NE edge of Town Square (z = 145.5) + Taxi Signpost
            GameObject taxiBoard = CreateJobStartRing(
                "JobBoard_Taxi",
                "job_board_taxi",
                JobType.Taxi,
                "job.taxi.name",
                "job.taxi.loc",
                jobsRoot.transform,
                new Vector3(16.6f, 0.16f, 145.5f),
                radius: 4.6f,
                mats.JobRingGlow);

            CreateStaticPart("TaxiSignPole", PrimitiveType.Cylinder, taxiBoard.transform,
                new Vector3(-1.35f, 1.35f, 0f), Quaternion.identity, new Vector3(0.10f, 1.35f, 0.10f), mats.SedanTrimDark);
            CreateStaticPart("TaxiSignPlate", PrimitiveType.Cube, taxiBoard.transform,
                new Vector3(-1.35f, 2.75f, 0f), Quaternion.identity, new Vector3(0.12f, 0.42f, 1.15f), mats.RoadMarkingYellow);
            CreateWorldSignText("TAKSİ", taxiBoard.transform,
                new Vector3(-1.26f, 2.75f, 0f), Quaternion.Euler(0f, -90f, 0f),
                new Vector2(1.05f, 0.36f), new Color(0.10f, 0.11f, 0.13f, 1f));

            // c) Street Cleanup in front of the Clock Tower at Town Square (x = -11.2, z = 134)
            CreateJobStartRing(
                "JobBoard_Cleanup",
                "job_board_cleanup",
                JobType.StreetCleanup,
                "job.cleanup.name",
                "job.cleanup.loc",
                jobsRoot.transform,
                new Vector3(-11.2f, 0.16f, 134.0f),
                radius: 3.2f,
                mats.JobRingGlow);

            // d) House Painting Job NPC + Ring at Residential Quarter / Park corner (x = -33.4, z = 118.2)
            GameObject paintBoard = CreateJobStartRing(
                "JobBoard_HousePainting",
                "job_board_painting",
                JobType.HousePainting,
                "job.paint.name",
                "job.paint.loc",
                jobsRoot.transform,
                new Vector3(-33.4f, 0.12f, 118.2f),
                radius: 3.5f,
                mats.JobRingGlow);

            GameObject painterNpc = new GameObject("NPC_HousePainterContractor");
            painterNpc.transform.SetParent(paintBoard.transform, false);
            painterNpc.transform.localPosition = new Vector3(-0.75f, 0f, 0.55f);
            painterNpc.transform.localRotation = Quaternion.Euler(0f, 125f, 0f);
            CapsuleCollider npcCol = painterNpc.AddComponent<CapsuleCollider>();
            npcCol.center = new Vector3(0f, 0.9f, 0f);
            npcCol.radius = 0.36f;
            npcCol.height = 1.78f;

            ProceduralHumanoid painterHum = ProceduralHumanoid.Build(
                painterNpc.transform,
                mats.CharSkin,
                mats.PedShirtMustard,
                mats.CharPants,
                mats.CharShoes,
                mats.CharHair,
                1.76f);
            painterHum.SetAutoRandomWave(true);
        }

        private static GameObject CreateJobStartRing(
            string name,
            string triggerId,
            JobType jobType,
            string nameKey,
            string locKey,
            Transform parent,
            Vector3 worldPos,
            float radius,
            Material glowMat)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "JobGlowRing";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.045f, 0f);
            ring.transform.localScale = new Vector3(2.8f, 0.045f, 2.8f);
            UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());
            if (glowMat != null)
            {
                ring.GetComponent<Renderer>().sharedMaterial = glowMat;
            }

            InteractableTrigger trig = root.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.JobBoard, triggerId, "ui.btn.job_board", radius);

            JobBoardPoint board = root.AddComponent<JobBoardPoint>();
            board.Configure(jobType, nameKey, locKey, ring.transform);
            return root;
        }

        /// <summary>
        /// E6: Builds interactable Vending Machines (5 ₺, +8 energy) and Mailboxes for town life flavour.
        /// </summary>
        private static void BuildVendingAndMailboxes(Transform parent, MaterialSet mats)
        {
            GameObject propsRoot = new GameObject("Town_VendingAndMailboxes");
            propsRoot.transform.SetParent(parent, false);

            CreateVendingMachine("Vending_Square", "vending_square", propsRoot.transform,
                new Vector3(-12.2f, 0.16f, 128.8f), Quaternion.Euler(0f, 90f, 0f), mats);
            CreateVendingMachine("Vending_Promenade", "vending_promenade", propsRoot.transform,
                new Vector3(34.4f, 0.16f, 134.0f), Quaternion.Euler(0f, -90f, 0f), mats);
            CreateVendingMachine("Vending_GasStation", "vending_gasstation", propsRoot.transform,
                new Vector3(6.6f, 0.16f, 231.5f), Quaternion.Euler(0f, 90f, 0f), mats);

            (Vector3 pos, float yaw)[] mailboxes =
            {
                (new Vector3(13.95f, 0.16f, 34.5f), 90f),
                (new Vector3(11.2f, 0.16f, 122.4f), 0f),
                (new Vector3(13.95f, 0.16f, 196.0f), 90f),
                (new Vector3(-34.2f, 0.10f, 100.5f), 90f)
            };

            for (int i = 0; i < mailboxes.Length; i++)
            {
                CreateMailbox($"Mailbox_{i + 1:00}", $"mailbox_{i + 1:00}", propsRoot.transform,
                    mailboxes[i].pos, Quaternion.Euler(0f, mailboxes[i].yaw, 0f), mats);
            }
        }

        private static void CreateVendingMachine(string name, string id, Transform parent, Vector3 pos, Quaternion rot, MaterialSet mats)
        {
            GameObject vm = new GameObject(name);
            vm.transform.SetParent(parent, false);
            vm.transform.SetPositionAndRotation(pos, rot);

            CreateStaticPart("Cabinet", PrimitiveType.Cube, vm.transform,
                new Vector3(0f, 0.98f, 0f), Quaternion.identity, new Vector3(0.95f, 1.96f, 0.78f), mats.TownFabricTeal);
            GameObject frontPanel = CreateStaticPart("LitDisplay", PrimitiveType.Cube, vm.transform,
                new Vector3(0f, 1.15f, 0.40f), Quaternion.identity, new Vector3(0.76f, 1.18f, 0.04f), mats.WindowLitWarm);
            CreateStaticPart("PickupSlot", PrimitiveType.Cube, vm.transform,
                new Vector3(0f, 0.34f, 0.40f), Quaternion.identity, new Vector3(0.62f, 0.24f, 0.05f), mats.SedanTrimDark);

            BoxCollider col = vm.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.98f, 0f);
            col.size = new Vector3(0.98f, 1.96f, 0.82f);
            MarkStaticEnvironment(vm);
            AttachWindowNightLight(frontPanel);

            GameObject trigObj = new GameObject("VendingTrigger");
            trigObj.transform.SetParent(vm.transform, false);
            trigObj.transform.localPosition = new Vector3(0f, 0.9f, 0.45f);
            InteractableTrigger trig = trigObj.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.VendingMachine, id, "ui.btn.vending", 2.1f);
        }

        private static void CreateMailbox(string name, string id, Transform parent, Vector3 pos, Quaternion rot, MaterialSet mats)
        {
            GameObject mb = new GameObject(name);
            mb.transform.SetParent(parent, false);
            mb.transform.SetPositionAndRotation(pos, rot);

            CreateStaticPart("Post", PrimitiveType.Cylinder, mb.transform,
                new Vector3(0f, 0.52f, 0f), Quaternion.identity, new Vector3(0.12f, 0.52f, 0.12f), mats.SedanTrimDark);
            CreateStaticPart("BoxBody", PrimitiveType.Cube, mb.transform,
                new Vector3(0f, 1.15f, 0f), Quaternion.identity, new Vector3(0.46f, 0.52f, 0.38f), mats.ContainerBlue);
            CreateStaticPart("BoxCap", PrimitiveType.Cylinder, mb.transform,
                new Vector3(0f, 1.40f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.38f, 0.23f, 0.38f), mats.ContainerBlue);

            BoxCollider col = mb.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.78f, 0f);
            col.size = new Vector3(0.48f, 1.56f, 0.42f);
            MarkStaticEnvironment(mb);

            GameObject trigObj = new GameObject("MailboxTrigger");
            trigObj.transform.SetParent(mb.transform, false);
            trigObj.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            InteractableTrigger trig = trigObj.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.InspectObject, id, "ui.btn.inspect", 1.85f);
        }

        private static void BuildTownSquare(
            Transform parent,
            MaterialSet mats,
            PrefabSet prefabs,
            List<SeatInteractable> benchSeatCollector)
        {
            GameObject sqRoot = new GameObject("Town_Square");
            sqRoot.transform.SetParent(parent, false);

            CreateTiledBox("Square_Paving", sqRoot.transform,
                new Vector3(-2.5f, 0.08f, 134f), new Vector3(31f, 0.16f, 28f),
                Quaternion.identity, mats.TownPaving, metersPerTile: 2.0f, collider: true);

            Vector3 fountainCenter = new Vector3(-2.0f, 0.16f, 134.0f);
            GameObject fountain = new GameObject("Square_Fountain");
            fountain.transform.SetParent(sqRoot.transform, false);

            float basinRadius = 3.1f;
            float sideLength = 2f * basinRadius * Mathf.Tan(Mathf.PI / 8f) + 0.18f;
            for (int i = 0; i < 8; i++)
            {
                float angleDeg = i * 45f;
                float rad = angleDeg * Mathf.Deg2Rad;
                Vector3 segPos = fountainCenter + new Vector3(Mathf.Sin(rad) * basinRadius, 0.34f, Mathf.Cos(rad) * basinRadius);
                CreateTiledBox($"FountainRim_{i}", fountain.transform,
                    segPos, new Vector3(sideLength, 0.68f, 0.44f),
                    Quaternion.Euler(0f, angleDeg, 0f), mats.PlasterTrim, 1.5f, collider: true);
            }

            GameObject waterDisc = CreatePrimitiveChild("FountainWater", PrimitiveType.Cylinder, fountain.transform,
                fountainCenter + new Vector3(0f, 0.50f, 0f), new Vector3(5.8f, 0.06f, 5.8f), mats.SeaWater, removeCollider: true);
            MarkStaticEnvironment(waterDisc);

            CreateTiledBox("FountainPedestal", fountain.transform,
                fountainCenter + new Vector3(0f, 0.85f, 0f), new Vector3(1.4f, 1.7f, 1.4f),
                Quaternion.identity, mats.Concrete, 1.5f, collider: true);
            GameObject lowerBowl = CreatePrimitiveChild("FountainBowl_Lower", PrimitiveType.Cylinder, fountain.transform,
                fountainCenter + new Vector3(0f, 1.75f, 0f), new Vector3(2.2f, 0.16f, 2.2f), mats.PlasterTrim, removeCollider: true);
            MarkStaticEnvironment(lowerBowl);
            CreateTiledBox("FountainShaft_Upper", fountain.transform,
                fountainCenter + new Vector3(0f, 2.35f, 0f), new Vector3(0.75f, 1.15f, 0.75f),
                Quaternion.identity, mats.Concrete, 1.5f, collider: false);
            GameObject upperBowl = CreatePrimitiveChild("FountainBowl_Upper", PrimitiveType.Cylinder, fountain.transform,
                fountainCenter + new Vector3(0f, 2.95f, 0f), new Vector3(1.25f, 0.12f, 1.25f), mats.PlasterTrim, removeCollider: true);
            MarkStaticEnvironment(upperBowl);
            GameObject finial = CreatePrimitiveChild("FountainFinial", PrimitiveType.Sphere, fountain.transform,
                fountainCenter + new Vector3(0f, 3.25f, 0f), new Vector3(0.42f, 0.52f, 0.42f), mats.PlasterTrim, removeCollider: true);
            MarkStaticEnvironment(finial);

            float[] benchAngles = { 30f, 90f, 150f, 210f, 270f, 330f };
            SeatInteractable[] npcBenchSlots = new SeatInteractable[2];
            for (int i = 0; i < benchAngles.Length; i++)
            {
                float rad = benchAngles[i] * Mathf.Deg2Rad;
                Vector3 pos = fountainCenter + new Vector3(Mathf.Sin(rad) * 5.6f, 0f, Mathf.Cos(rad) * 5.6f);
                Quaternion rot = Quaternion.Euler(0f, benchAngles[i] + 180f, 0f);
                var (slotL, slotR) = CreateBench($"Square_Bench_{i + 1}", sqRoot.transform, pos, rot, mats);
                if (i == 0)
                {
                    npcBenchSlots[0] = slotL;
                    benchSeatCollector.Add(slotR);
                }
                else if (i == 3)
                {
                    benchSeatCollector.Add(slotL);
                    npcBenchSlots[1] = slotR;
                }
                else
                {
                    benchSeatCollector.Add(slotL);
                    benchSeatCollector.Add(slotR);
                }
            }

            BuildSeatedSquarePedestrians(sqRoot.transform, npcBenchSlots, mats);

            Vector3[] sqTrees =
            {
                new Vector3(9.5f, 0.16f, 123.5f),
                new Vector3(9.5f, 0.16f, 144.5f),
                new Vector3(-1.5f, 0.16f, 122.5f),
                new Vector3(-1.5f, 0.16f, 145.5f),
                new Vector3(-12.5f, 0.16f, 123.5f),
                new Vector3(-12.5f, 0.16f, 144.5f)
            };
            for (int i = 0; i < sqTrees.Length; i++)
            {
                CreateTownTree($"Square_Tree_{i + 1}", sqRoot.transform, sqTrees[i], mats, 1.0f + (i % 3) * 0.12f, i);
            }

            BuildClockTower(sqRoot.transform, new Vector3(-14.6f, 0.16f, 134.0f), mats);

            Vector3[] lampPositions =
            {
                new Vector3(8.5f, 0.16f, 127.0f),
                new Vector3(8.5f, 0.16f, 141.0f),
                new Vector3(-9.5f, 0.16f, 127.0f),
                new Vector3(-9.5f, 0.16f, 141.0f)
            };
            for (int i = 0; i < lampPositions.Length; i++)
            {
                PlacePrefabInstance(prefabs.StreetLamp, sqRoot.transform, lampPositions[i], Quaternion.Euler(0f, i * 90f, 0f));
                CreatePointLamp($"Square_LampLight_{i + 1}", sqRoot.transform,
                    lampPositions[i] + Vector3.up * 5.1f, new Color(1.0f, 0.80f, 0.46f), 20f, 2.2f);
            }

            Vector3[] planterCenters =
            {
                new Vector3(11.4f, 0.16f, 128.0f),
                new Vector3(11.4f, 0.16f, 140.0f),
                new Vector3(-6.0f, 0.16f, 121.6f),
                new Vector3(-6.0f, 0.16f, 146.4f)
            };
            for (int i = 0; i < planterCenters.Length; i++)
            {
                Vector3 size = i < 2 ? new Vector3(1.1f, 0.55f, 3.2f) : new Vector3(3.4f, 0.55f, 1.1f);
                CreateTiledBox($"Square_Planter_{i + 1}", sqRoot.transform,
                    planterCenters[i] + Vector3.up * 0.275f, size,
                    Quaternion.identity, mats.GarageBrick, 1.5f, collider: true);
                CreateTiledBox($"Square_PlanterHedge_{i + 1}", sqRoot.transform,
                    planterCenters[i] + Vector3.up * 0.72f, new Vector3(size.x - 0.22f, 0.42f, size.z - 0.22f),
                    Quaternion.identity, mats.TownHedge, 1.5f, collider: false);
            }
        }

        private static void BuildClockTower(Transform parent, Vector3 basePos, MaterialSet mats)
        {
            GameObject tower = new GameObject("Square_ClockTower");
            tower.transform.SetParent(parent, false);

            CreateTiledBox("Tower_Base", tower.transform,
                basePos + new Vector3(0f, 1.5f, 0f), new Vector3(4.4f, 3.0f, 4.4f),
                Quaternion.identity, mats.Concrete, 2.0f, collider: true);

            CreateTiledBox("Tower_Shaft", tower.transform,
                basePos + new Vector3(0f, 7.25f, 0f), new Vector3(3.6f, 8.5f, 3.6f),
                Quaternion.identity, mats.PlasterTrim, 3.0f, collider: true);

            CreateTiledBox("Tower_ClockBox", tower.transform,
                basePos + new Vector3(0f, 12.9f, 0f), new Vector3(3.9f, 2.8f, 3.9f),
                Quaternion.identity, mats.Concrete, 2.0f, collider: true);

            CreateTiledBox("Tower_Cornice", tower.transform,
                basePos + new Vector3(0f, 14.45f, 0f), new Vector3(4.3f, 0.35f, 4.3f),
                Quaternion.identity, mats.PlasterTrim, 2.0f, collider: false);
            CreateTiledBox("Tower_CapLower", tower.transform,
                basePos + new Vector3(0f, 15.05f, 0f), new Vector3(3.2f, 0.9f, 3.2f),
                Quaternion.identity, mats.TownRoofTile, 1.5f, collider: false);
            CreateTiledBox("Tower_CapUpper", tower.transform,
                basePos + new Vector3(0f, 15.75f, 0f), new Vector3(1.6f, 0.7f, 1.6f),
                Quaternion.identity, mats.TownRoofTile, 1.5f, collider: false);

            Vector3[] faceOffsets =
            {
                new Vector3(1.98f, 12.9f, 0f),
                new Vector3(-1.98f, 12.9f, 0f),
                new Vector3(0f, 12.9f, 1.98f),
                new Vector3(0f, 12.9f, -1.98f)
            };
            Quaternion[] faceRots =
            {
                Quaternion.Euler(0f, 90f, 0f),
                Quaternion.Euler(0f, -90f, 0f),
                Quaternion.identity,
                Quaternion.Euler(0f, 180f, 0f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject faceRoot = new GameObject($"ClockFace_{i}");
                faceRoot.transform.SetParent(tower.transform, false);
                faceRoot.transform.position = basePos + faceOffsets[i];
                faceRoot.transform.rotation = faceRots[i];

                GameObject dial = CreatePrimitiveChild("Dial", PrimitiveType.Cylinder, faceRoot.transform,
                    Vector3.zero, new Vector3(1.75f, 0.04f, 1.75f), mats.LampGlow, removeCollider: true);
                dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                MarkStaticPaintable(dial);
                NightLight dialNl = dial.AddComponent<NightLight>();
                dialNl.Configure(null, new[] { dial.GetComponent<Renderer>() }, new Color(1.0f, 0.85f, 0.55f) * 2.5f);

                GameObject hourHand = CreatePrimitiveChild("HourHand", PrimitiveType.Cube, faceRoot.transform,
                    new Vector3(0f, 0.22f, 0.05f), new Vector3(0.09f, 0.50f, 0.04f), mats.SedanTrimDark, removeCollider: true);
                MarkStaticEnvironment(hourHand);

                GameObject minHand = CreatePrimitiveChild("MinuteHand", PrimitiveType.Cube, faceRoot.transform,
                    new Vector3(0.24f, 0f, 0.05f), new Vector3(0.58f, 0.07f, 0.04f), mats.SedanTrimDark, removeCollider: true);
                MarkStaticEnvironment(minHand);
            }
        }

        private static void BuildResidentialAndPark(
            Transform parent,
            MaterialSet mats,
            System.Random rng,
            List<SeatInteractable> benchSeatCollector)
        {
            GameObject resRoot = new GameObject("Town_ResidentialQuarter");
            resRoot.transform.SetParent(parent, false);

            Material[] wallPalette = { mats.PlasterTrim, mats.BuildingFacadeB, mats.TownBrickHouse };

            float[] houseZs =
            {
                24f, 38f, 52f, 66f, 80f,
                124f, 140f, 156f,
                186f, 201f, 216f, 231f
            };

            int houseIdx = 0;
            foreach (float z in houseZs)
            {
                Material wall1 = wallPalette[rng.Next(wallPalette.Length)];
                BuildEnterableResidentialHouse(
                    $"House_R1_{houseIdx + 1:00}",
                    $"house_r1_{houseIdx + 1:00}",
                    $"Çınar Sokak No: {houseIdx + 1}",
                    resRoot.transform,
                    new Vector3(-38.1f, 0f, z),
                    depthX: 7.4f,
                    widthZ: 8.8f + (float)(rng.NextDouble() * 0.7),
                    wallMat: wall1,
                    hasChimney: rng.Next(3) == 0,
                    hasGardenTree: rng.Next(2) == 0,
                    mats: mats,
                    houseSeed: 2000 + houseIdx * 37);

                Material wall2 = wallPalette[rng.Next(wallPalette.Length)];
                BuildEnterableResidentialHouse(
                    $"House_R2_{houseIdx + 1:00}",
                    $"house_r2_{houseIdx + 1:00}",
                    $"Lale Sokak No: {houseIdx + 1}",
                    resRoot.transform,
                    new Vector3(-56.5f, 0f, z),
                    depthX: 8.0f,
                    widthZ: 9.0f + (float)(rng.NextDouble() * 0.7),
                    wallMat: wall2,
                    hasChimney: rng.Next(3) == 0,
                    hasGardenTree: rng.Next(2) == 0,
                    mats: mats,
                    houseSeed: 3000 + houseIdx * 41);

                houseIdx++;
            }

            GameObject parkRoot = new GameObject("Town_NeighborhoodPark");
            parkRoot.transform.SetParent(parent, false);

            CreateTiledBox("Park_GrassWest", parkRoot.transform,
                new Vector3(-59.25f, 0.02f, 106.65f), new Vector3(19.5f, 0.04f, 19.7f),
                Quaternion.identity, mats.TownGrass, 4.0f, collider: false);

            CreateTiledBox("Park_GrassEast", parkRoot.transform,
                new Vector3(-38.0f, 0.02f, 106.65f), new Vector3(9.0f, 0.04f, 19.7f),
                Quaternion.identity, mats.TownGrass, 4.0f, collider: false);

            CreateTiledBox("Park_PathWest", parkRoot.transform,
                new Vector3(-59.25f, 0.04f, 106.5f), new Vector3(19.5f, 0.04f, 2.4f),
                Quaternion.identity, mats.TownPaving, 2.0f, collider: false);
            CreateTiledBox("Park_PathWestCross", parkRoot.transform,
                new Vector3(-58.5f, 0.04f, 106.65f), new Vector3(2.2f, 0.04f, 19.2f),
                Quaternion.identity, mats.TownPaving, 2.0f, collider: false);
            CreateTiledBox("Park_PathEast", parkRoot.transform,
                new Vector3(-38.0f, 0.04f, 106.5f), new Vector3(9.0f, 0.04f, 2.4f),
                Quaternion.identity, mats.TownPaving, 2.0f, collider: false);

            Vector3[] parkTrees =
            {
                new Vector3(-65.5f, 0.04f, 100.5f),
                new Vector3(-53.5f, 0.04f, 100.5f),
                new Vector3(-65.5f, 0.04f, 112.5f),
                new Vector3(-53.5f, 0.04f, 112.5f),
                new Vector3(-62.0f, 0.04f, 114.2f),
                new Vector3(-62.0f, 0.04f, 99.0f),
                new Vector3(-37.5f, 0.04f, 101.2f),
                new Vector3(-37.5f, 0.04f, 112.2f)
            };
            for (int i = 0; i < parkTrees.Length; i++)
            {
                CreateTownTree($"Park_Tree_{i + 1}", parkRoot.transform, parkTrees[i], mats, 0.95f + (i % 3) * 0.16f, i + 10);
            }

            var pb1 = CreateBench("Park_Bench_1", parkRoot.transform, new Vector3(-63.0f, 0.06f, 104.6f), Quaternion.identity, mats);
            var pb2 = CreateBench("Park_Bench_2", parkRoot.transform, new Vector3(-53.8f, 0.06f, 108.4f), Quaternion.Euler(0f, 180f, 0f), mats);
            var pb3 = CreateBench("Park_Bench_3", parkRoot.transform, new Vector3(-38.0f, 0.06f, 104.6f), Quaternion.identity, mats);
            benchSeatCollector.Add(pb1.leftSlot); benchSeatCollector.Add(pb1.rightSlot);
            benchSeatCollector.Add(pb2.leftSlot); benchSeatCollector.Add(pb2.rightSlot);
            benchSeatCollector.Add(pb3.leftSlot); benchSeatCollector.Add(pb3.rightSlot);
        }

        private static List<TrafficStopLine> BuildTownStreetFurniture(
            Transform parent,
            MaterialSet mats,
            PrefabSet prefabs,
            System.Random rng,
            List<SeatInteractable> benchSeatCollector)
        {
            GameObject furnRoot = new GameObject("Town_StreetFurniture");
            furnRoot.transform.SetParent(parent, false);

            float[] crossZs = { 92f, 174f };
            foreach (float cz in crossZs)
            {
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Coastal_{cz}_S", new Vector3(24f, 0.08f, cz - 6.2f), 10.5f, horizontalX: true, mats.RoadMarkingWhite);
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Coastal_{cz}_N", new Vector3(24f, 0.08f, cz + 6.2f), 10.5f, horizontalX: true, mats.RoadMarkingWhite);
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Coastal_{cz}_W", new Vector3(16.0f, 0.08f, cz), 7.5f, horizontalX: false, mats.RoadMarkingWhite);

                // Painted white traffic Stop Lines before each Coastal Main crosswalk
                CreateStaticBlock($"StopLine_Coastal_{cz}_NB", furnRoot.transform,
                    new Vector3(27.0f, 0.082f, cz - 8.5f), new Vector3(5.2f, 0.02f, 0.45f), mats.RoadMarkingWhite, addCollider: false);
                CreateStaticBlock($"StopLine_Coastal_{cz}_SB", furnRoot.transform,
                    new Vector3(21.0f, 0.082f, cz + 8.5f), new Vector3(5.2f, 0.02f, 0.45f), mats.RoadMarkingWhite, addCollider: false);

                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Alley_{cz}_S", new Vector3(-26f, 0.08f, cz - 6.2f), 7.5f, horizontalX: true, mats.RoadMarkingWhite);
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Alley_{cz}_N", new Vector3(-26f, 0.08f, cz + 6.2f), 7.5f, horizontalX: true, mats.RoadMarkingWhite);
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Alley_{cz}_E", new Vector3(-20.4f, 0.08f, cz), 7.5f, horizontalX: false, mats.RoadMarkingWhite);
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Alley_{cz}_W", new Vector3(-31.6f, 0.08f, cz), 7.5f, horizontalX: false, mats.RoadMarkingWhite);
            }

            TrafficLightCycler southSW = CreateTrafficLightPole("TrafficLight_South_SW", furnRoot.transform, new Vector3(17.0f, 0.16f, 86.2f), Quaternion.Euler(0f, 0f, 0f), mats, phaseOffset: 0f);
            TrafficLightCycler southNE = CreateTrafficLightPole("TrafficLight_South_NE", furnRoot.transform, new Vector3(31.2f, 0.16f, 97.8f), Quaternion.Euler(0f, 180f, 0f), mats, phaseOffset: 0f);
            TrafficLightCycler northSW = CreateTrafficLightPole("TrafficLight_North_SW", furnRoot.transform, new Vector3(17.0f, 0.16f, 168.2f), Quaternion.Euler(0f, 0f, 0f), mats, phaseOffset: 8f);
            TrafficLightCycler northNE = CreateTrafficLightPole("TrafficLight_North_NE", furnRoot.transform, new Vector3(31.2f, 0.16f, 179.8f), Quaternion.Euler(0f, 180f, 0f), mats, phaseOffset: 8f);

            List<TrafficStopLine> stopLines = new List<TrafficStopLine>
            {
                new TrafficStopLine { stopPosition = new Vector3(27.0f, 0.1f, 83.5f), approachDirection = Vector3.forward, trafficLight = southSW },
                new TrafficStopLine { stopPosition = new Vector3(21.0f, 0.1f, 100.5f), approachDirection = Vector3.back, trafficLight = southNE },
                new TrafficStopLine { stopPosition = new Vector3(27.0f, 0.1f, 165.5f), approachDirection = Vector3.forward, trafficLight = northSW },
                new TrafficStopLine { stopPosition = new Vector3(21.0f, 0.1f, 182.5f), approachDirection = Vector3.back, trafficLight = northNE }
            };

            int propIdx = 0;
            for (float z = 25f; z <= 228f; z += 25f)
            {
                if (IsNearCrossStreet(z, 7.5f)) continue;

                // Keep Gas Station driveway (z = 214..239) completely open on MainWest
                if (z < 214f)
                {
                    if (propIdx % 2 == 0)
                    {
                        PlacePrefabInstance(prefabs.FireHydrant, furnRoot.transform, new Vector3(16.6f, 0.16f, z), Quaternion.identity);
                    }
                    else
                    {
                        PlacePrefabInstance(prefabs.TrashCan, furnRoot.transform, new Vector3(16.5f, 0.16f, z), Quaternion.Euler(0f, propIdx * 30f, 0f));
                    }

                    if ((z < 118f || z > 150f) && propIdx % 2 == 1 && z + 5f < 212f)
                    {
                        var swBench = CreateBench($"MainSidewalk_Bench_{propIdx}", furnRoot.transform,
                            new Vector3(14.35f, 0.16f, z + 5f), Quaternion.Euler(0f, 90f, 0f), mats);
                        benchSeatCollector.Add(swBench.leftSlot);
                        benchSeatCollector.Add(swBench.rightSlot);
                    }
                }

                float alleyX = propIdx % 2 == 0 ? -20.6f : -31.6f;
                if (propIdx % 3 == 0)
                {
                    PlacePrefabInstance(prefabs.FireHydrant, furnRoot.transform, new Vector3(alleyX, 0.16f, z + 3f), Quaternion.identity);
                }
                else
                {
                    PlacePrefabInstance(prefabs.TrashCan, furnRoot.transform, new Vector3(alleyX, 0.16f, z + 3f), Quaternion.Euler(0f, -20f * propIdx, 0f));
                }

                propIdx++;
            }

            // 8 Drivable Town Cars (6 neighborhood curb cars + 1 near Pizzeria + 1 near Taxi Rank), each with unique Save ID
            (Vector3 pos, float yaw, int fixedVariant)[] parkedCars =
            {
                (new Vector3(-23.1f, 0.28f, 36f), 0f, -1),
                (new Vector3(-28.9f, 0.28f, 70f), 180f, -1),
                (new Vector3(-23.1f, 0.28f, 134f), 0f, -1),
                (new Vector3(-28.9f, 0.28f, 206f), 180f, -1),
                (new Vector3(-44.1f, 0.28f, 106f), 0f, -1),
                (new Vector3(-47.9f, 0.28f, 193f), 180f, -1),
                (new Vector3(19.2f, 0.28f, 33.5f), 0f, 0),   // Red hatchback parked near Pizzeria
                (new Vector3(19.2f, 0.28f, 152.5f), 0f, 2)   // Sedan parked near Taxi Rank
            };

            GameObject[] drivableVariants =
            {
                prefabs.TownCarRedHatchback,
                prefabs.TownCarWhitePickup,
                prefabs.TownCarGreenSedan
            };

            int[] variantOrder = { 0, 1, 2, 0, 1, 2 };
            for (int i = variantOrder.Length - 1; i > 0; i--)
            {
                int swapIdx = rng.Next(i + 1);
                (variantOrder[i], variantOrder[swapIdx]) = (variantOrder[swapIdx], variantOrder[i]);
            }

            for (int i = 0; i < parkedCars.Length; i++)
            {
                int vIdx = parkedCars[i].fixedVariant >= 0 ? parkedCars[i].fixedVariant : variantOrder[i % variantOrder.Length];
                GameObject chosenPrefab = drivableVariants[vIdx] != null ? drivableVariants[vIdx] : prefabs.TrafficVehicle;
                GameObject car = PlacePrefabInstance(chosenPrefab, furnRoot.transform,
                    parkedCars[i].pos, Quaternion.Euler(0f, parkedCars[i].yaw, 0f));
                string carId = $"town_car_{i + 1:00}";
                car.name = $"Town_DrivableCar_{i + 1:00}";
                InteractableTrigger carTrig = car.GetComponent<InteractableTrigger>();
                if (carTrig != null)
                {
                    carTrig.Configure(InteractableKind.TownVehicle, carId, "ui.btn.drive", 3.8f);
                }
            }

            int treeIdx = 0;
            for (float z = 32f; z <= 210f; z += 16f)
            {
                if (IsNearCrossStreet(z, 9.0f)) continue;
                if (z > 125f && z < 150f) continue; // Keep Town Square & Taxi Rank curb clear
                float scale = 0.92f + (float)rng.NextDouble() * 0.24f;
                CreateTownTree($"MainStreet_Tree_{treeIdx + 1:00}", furnRoot.transform,
                    new Vector3(16.65f, 0.16f, z), mats, scale, treeIdx);
                treeIdx++;
            }

            return stopLines;
        }

        private static bool IsNearCrossStreet(float z, float halfMargin)
        {
            return Mathf.Abs(z - 92f) < halfMargin || Mathf.Abs(z - 174f) < halfMargin;
        }

        private static void CreateZebraCrosswalk(
            Transform parent,
            string name,
            Vector3 center,
            float spanMeters,
            bool horizontalX,
            Material whiteMat)
        {
            GameObject cw = new GameObject(name);
            cw.transform.SetParent(parent, false);

            int count = Mathf.FloorToInt(spanMeters);
            float startOffset = -((count - 1) * 1.0f) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float offset = startOffset + i * 1.0f;
                Vector3 pos = horizontalX
                    ? center + new Vector3(offset, 0f, 0f)
                    : center + new Vector3(0f, 0f, offset);
                Vector3 scale = horizontalX
                    ? new Vector3(0.50f, 0.02f, 2.8f)
                    : new Vector3(2.8f, 0.02f, 0.50f);

                CreateStaticBlock($"Stripe_{i}", cw.transform, pos, scale, whiteMat, addCollider: false);
            }
        }

        private static TrafficLightCycler CreateTrafficLightPole(
            string name,
            Transform parent,
            Vector3 basePos,
            Quaternion rot,
            MaterialSet mats,
            float phaseOffset)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(basePos, rot);

            GameObject pole = CreatePrimitiveChild("Pole", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 2.7f, 0f), new Vector3(0.16f, 2.7f, 0.16f), mats.SedanTrimDark, removeCollider: false);
            MarkStaticEnvironment(pole);

            GameObject arm = CreatePrimitiveChild("Arm", PrimitiveType.Cube, root.transform,
                new Vector3(0.55f, 5.15f, 0f), new Vector3(1.2f, 0.12f, 0.12f), mats.SedanTrimDark, removeCollider: true);
            MarkStaticEnvironment(arm);

            GameObject head = CreatePrimitiveChild("SignalHead", PrimitiveType.Cube, root.transform,
                new Vector3(1.05f, 4.75f, 0f), new Vector3(0.36f, 1.02f, 0.36f), mats.SedanTrimDark, removeCollider: true);
            MarkStaticEnvironment(head);

            GameObject redBulb = CreatePrimitiveChild("Bulb_Red", PrimitiveType.Sphere, root.transform,
                new Vector3(1.05f, 5.05f, -0.17f), new Vector3(0.22f, 0.22f, 0.12f), mats.TrafficLightBulb, removeCollider: true);
            GameObject amberBulb = CreatePrimitiveChild("Bulb_Amber", PrimitiveType.Sphere, root.transform,
                new Vector3(1.05f, 4.75f, -0.17f), new Vector3(0.22f, 0.22f, 0.12f), mats.TrafficLightBulb, removeCollider: true);
            GameObject greenBulb = CreatePrimitiveChild("Bulb_Green", PrimitiveType.Sphere, root.transform,
                new Vector3(1.05f, 4.45f, -0.17f), new Vector3(0.22f, 0.22f, 0.12f), mats.TrafficLightBulb, removeCollider: true);

            TrafficLightCycler cycler = root.AddComponent<TrafficLightCycler>();
            cycler.Configure(
                redBulb.GetComponent<Renderer>(),
                amberBulb.GetComponent<Renderer>(),
                greenBulb.GetComponent<Renderer>(),
                phaseOffset);
            return cycler;
        }

        private static (SeatInteractable leftSlot, SeatInteractable rightSlot) CreateBench(
            string name,
            Transform parent,
            Vector3 pos,
            Quaternion rot,
            MaterialSet mats)
        {
            GameObject bench = new GameObject(name);
            bench.transform.SetParent(parent, false);
            bench.transform.SetPositionAndRotation(pos, rot);

            CreatePrimitiveChild("Seat", PrimitiveType.Cube, bench.transform,
                new Vector3(0f, 0.45f, 0f), new Vector3(1.85f, 0.09f, 0.52f), mats.TownWood, removeCollider: true);
            GameObject back = CreatePrimitiveChild("Backrest", PrimitiveType.Cube, bench.transform,
                new Vector3(0f, 0.78f, -0.22f), new Vector3(1.85f, 0.38f, 0.08f), mats.TownWood, removeCollider: true);
            back.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);

            CreatePrimitiveChild("LegL", PrimitiveType.Cube, bench.transform,
                new Vector3(-0.74f, 0.23f, 0f), new Vector3(0.10f, 0.46f, 0.46f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("LegR", PrimitiveType.Cube, bench.transform,
                new Vector3(0.74f, 0.23f, 0f), new Vector3(0.10f, 0.46f, 0.46f), mats.SedanTrimDark, removeCollider: true);

            BoxCollider col = bench.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.48f, -0.04f);
            col.size = new Vector3(1.9f, 0.96f, 0.62f);

            MarkStaticEnvironment(bench);

            string idBase = name.ToLowerInvariant();
            SeatInteractable slotL = CreateSeatSlot("BenchSlot_L", $"{idBase}_l", bench.transform,
                new Vector3(-0.46f, 0.495f, 0.04f), Quaternion.identity, 1.4f);
            SeatInteractable slotR = CreateSeatSlot("BenchSlot_R", $"{idBase}_r", bench.transform,
                new Vector3(0.46f, 0.495f, 0.04f), Quaternion.identity, 1.4f);

            return (slotL, slotR);
        }

        private static void BuildSeatedSquarePedestrians(Transform parent, SeatInteractable[] slots, MaterialSet mats)
        {
            if (slots == null) return;

            Material[] shirts = { mats.PedShirtMustard, mats.PedShirtTeal };
            for (int i = 0; i < slots.Length; i++)
            {
                SeatInteractable slot = slots[i];
                if (slot == null) continue;

                slot.SetOccupied(true);

                GameObject npcObj = new GameObject($"TownSeatedPedestrian_{i + 1:00}");
                npcObj.transform.SetParent(parent, false);
                npcObj.transform.SetPositionAndRotation(slot.SitPoint.position, slot.SitPoint.rotation);

                CapsuleCollider col = npcObj.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.42f, 0.08f);
                col.radius = 0.30f;
                col.height = 1.15f;

                ProceduralHumanoid hum = ProceduralHumanoid.Build(
                    npcObj.transform,
                    mats.CharSkin,
                    shirts[i % shirts.Length],
                    mats.CharPants,
                    mats.CharShoes,
                    mats.CharHair,
                    1.74f);
                hum.SetSitting(true, driving: false);
            }
        }

        private static void CreateTownTree(
            string name,
            Transform parent,
            Vector3 basePos,
            MaterialSet mats,
            float scale,
            int variantSeed)
        {
            GameObject tree = new GameObject(name);
            tree.transform.SetParent(parent, false);
            tree.transform.position = basePos;
            tree.transform.localScale = Vector3.one * scale;

            CreatePrimitiveChild("Trunk", PrimitiveType.Cylinder, tree.transform,
                new Vector3(0f, 1.55f, 0f), new Vector3(0.36f, 1.55f, 0.36f), mats.TownBark, removeCollider: true);

            CapsuleCollider trunkCol = tree.AddComponent<CapsuleCollider>();
            trunkCol.center = new Vector3(0f, 1.55f, 0f);
            trunkCol.radius = 0.24f;
            trunkCol.height = 3.1f;

            Material folA = (variantSeed % 2 == 0) ? mats.TownFoliageA : mats.TownFoliageB;
            Material folB = (variantSeed % 2 == 0) ? mats.TownFoliageB : mats.TownFoliageA;

            CreatePrimitiveChild("Canopy_Main", PrimitiveType.Sphere, tree.transform,
                new Vector3(0f, 3.65f, 0f), new Vector3(2.45f, 2.05f, 2.45f), folA, removeCollider: true);
            CreatePrimitiveChild("Canopy_SideA", PrimitiveType.Sphere, tree.transform,
                new Vector3(0.75f, 3.25f, 0.45f), new Vector3(1.65f, 1.45f, 1.65f), folB, removeCollider: true);
            CreatePrimitiveChild("Canopy_SideB", PrimitiveType.Sphere, tree.transform,
                new Vector3(-0.68f, 3.35f, -0.40f), new Vector3(1.75f, 1.55f, 1.75f), folA, removeCollider: true);

            MarkStaticEnvironment(tree);
        }

        /// <summary>
        /// E6: Spawns 14 scheduled pedestrians strolling between 3-4 sidewalk waypoints,
        /// pausing 3-6 s, sitting on unoccupied benches, and yielding to player vehicles.
        /// </summary>
        private static void BuildTownPedestrians(
            Transform parent,
            MaterialSet mats,
            List<SeatInteractable> benchSeats)
        {
            GameObject pedRoot = new GameObject("Town_Pedestrians");
            pedRoot.transform.SetParent(parent, false);

            Material[] pedShirts =
            {
                mats.PedShirtRed,
                mats.PedShirtOlive,
                mats.PedShirtMustard,
                mats.PedShirtTeal,
                mats.PedShirtBurgundy,
                mats.CharShirt
            };

            Vector3[][] schedules =
            {
                // 1-4: Main Street West Sidewalk
                new[] { new Vector3(15.35f, 0.16f, 22f), new Vector3(15.35f, 0.16f, 48f), new Vector3(15.35f, 0.16f, 78f) },
                new[] { new Vector3(15.35f, 0.16f, 64f), new Vector3(15.35f, 0.16f, 84f), new Vector3(15.35f, 0.16f, 108f), new Vector3(15.35f, 0.16f, 124f) },
                new[] { new Vector3(15.35f, 0.16f, 118f), new Vector3(15.35f, 0.16f, 138f), new Vector3(15.35f, 0.16f, 160f) },
                new[] { new Vector3(15.35f, 0.16f, 184f), new Vector3(15.35f, 0.16f, 198f), new Vector3(15.35f, 0.16f, 212f) },
                // 5-6: Coastal Promenade along Seawall
                new[] { new Vector3(32.8f, 0.16f, 36f), new Vector3(32.8f, 0.16f, 78f), new Vector3(32.8f, 0.16f, 118f) },
                new[] { new Vector3(32.8f, 0.16f, 130f), new Vector3(32.8f, 0.16f, 172f), new Vector3(32.8f, 0.16f, 214f) },
                // 7-9: Town Square Plaza
                new[] { new Vector3(-8.5f, 0.16f, 142.5f), new Vector3(0f, 0.16f, 143.0f), new Vector3(7.5f, 0.16f, 142.0f), new Vector3(7.8f, 0.16f, 128.0f) },
                new[] { new Vector3(7.5f, 0.16f, 125.5f), new Vector3(-1.5f, 0.16f, 125.2f), new Vector3(-8.5f, 0.16f, 126.0f) },
                new[] { new Vector3(-10.2f, 0.16f, 129.0f), new Vector3(-10.2f, 0.16f, 139.0f), new Vector3(-4.0f, 0.16f, 143.2f) },
                // 10-12: Back Alley Sidewalks
                new[] { new Vector3(-20.4f, 0.16f, 28f), new Vector3(-20.4f, 0.16f, 54f), new Vector3(-20.4f, 0.16f, 82f) },
                new[] { new Vector3(-20.4f, 0.16f, 186f), new Vector3(-20.4f, 0.16f, 208f), new Vector3(-20.4f, 0.16f, 228f) },
                new[] { new Vector3(-31.8f, 0.16f, 102f), new Vector3(-31.8f, 0.16f, 132f), new Vector3(-31.8f, 0.16f, 162f) },
                // 13-14: Neighborhood Park & Residential Walk
                new[] { new Vector3(-64.5f, 0.08f, 106.5f), new Vector3(-58.5f, 0.08f, 106.5f), new Vector3(-51.5f, 0.08f, 106.5f), new Vector3(-38.0f, 0.08f, 106.5f) },
                new[] { new Vector3(-42.5f, 0.08f, 44f), new Vector3(-42.5f, 0.08f, 76f), new Vector3(-42.5f, 0.08f, 104f) }
            };

            SeatInteractable[] seatArray = benchSeats != null ? benchSeats.ToArray() : Array.Empty<SeatInteractable>();

            for (int i = 0; i < schedules.Length; i++)
            {
                Vector3[] route = schedules[i];
                GameObject pedObj = new GameObject($"TownPedestrian_{i + 1:00}");
                pedObj.transform.SetParent(pedRoot.transform, false);

                int startWp = i % route.Length;
                int nextWp = (startWp + 1) % route.Length;
                Vector3 startPos = Vector3.Lerp(route[startWp], route[nextWp], 0.35f);
                Vector3 dir = route[nextWp] - route[startWp];
                dir.y = 0f;
                Quaternion startRot = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;
                pedObj.transform.SetPositionAndRotation(startPos, startRot);

                CapsuleCollider col = pedObj.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.9f, 0f);
                col.radius = 0.34f;
                col.height = 1.76f;

                Rigidbody rb = pedObj.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                float height = 1.66f + (i % 4) * 0.05f;
                ProceduralHumanoid hum = ProceduralHumanoid.Build(
                    pedObj.transform,
                    mats.CharSkin,
                    pedShirts[i % pedShirts.Length],
                    mats.CharPants,
                    mats.CharShoes,
                    mats.CharHair,
                    height);

                TownPedestrian ped = pedObj.AddComponent<TownPedestrian>();
                ped.ConfigureSchedule(route, seatArray, hum, speed: 1.22f + (i % 3) * 0.08f, startIndex: nextWp);
            }
        }
    }
}
