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
        private static GameObject _activeBenchPrefab;
        private static Mesh _cachedTreeTrunkMesh;
        private static Mesh _cachedTreeCanopyMeshA;
        private static Mesh _cachedTreeCanopyMeshB;

        /// <summary>
        /// Builds the clean, compact, 100% walkable & drivable "Çınarlı Kasabası" small town:
        /// - 4x4 connected street grid (zero dead ends, zero raised curb colliders across intersections)
        /// - 3 spacious enterable shops (PİZZA, KAFE, MARKET — zero fake glued-on dummy buildings)
        /// - Central Town Square (Fountain, Clock Tower, 3D Wooden Benches, Trees)
        /// - Çınarlı Park, Drive-Through Gas Station, 12 spacious Enterable & Paintable Houses
        /// - 4 Side-Job boards, Vending Machines, Mailboxes, 8 Drivable Cars, Traffic Lights & 14 Pedestrians
        /// </summary>
        private static List<TrafficStopLine> BuildSmallTown(Transform envParent, MaterialSet mats, PrefabSet prefabs)
        {
            GameObject townRoot = new GameObject("Zone_SmallTown_Cinarli");
            townRoot.transform.SetParent(envParent, false);

            System.Random rng = new System.Random(1234);
            List<SeatInteractable> outdoorBenchSeats = new List<SeatInteractable>();
            _activeBenchPrefab = prefabs != null ? prefabs.WoodenBench : null;

            BuildTownGridRoads(townRoot.transform, mats);
            BuildMainStreetShops(townRoot.transform, mats);
            BuildGasStation(townRoot.transform, mats);
            BuildTownSquare(townRoot.transform, mats, prefabs, outdoorBenchSeats);
            BuildResidentialAndPark(townRoot.transform, mats, rng, outdoorBenchSeats);
            BuildIstanbulBosphorusAndAnatolia(townRoot.transform, mats, prefabs, rng, outdoorBenchSeats);
            List<TrafficStopLine> stopLines = BuildTownStreetFurniture(townRoot.transform, mats, prefabs, rng, outdoorBenchSeats);
            BuildSideJobStartPoints(townRoot.transform, mats);
            BuildStreetVendors(townRoot.transform, mats);
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

        private static void BuildTownGridRoads(Transform parent, MaterialSet mats)
        {
            GameObject roadsRoot = new GameObject("Town_GridRoads");
            roadsRoot.transform.SetParent(parent, false);

            // 4 North-South Streets (x = -72, -24, +24, +72; z = -24..+184 -> length 219m, width 11m, flush visual layer)
            float[] nsXs = { -72f, -24f, 24f, 72f };
            for (int i = 0; i < nsXs.Length; i++)
            {
                CreateStaticBlock($"Road_NS_{i + 1}", roadsRoot.transform,
                    new Vector3(nsXs[i], 0.012f, 80f), new Vector3(11f, 0.024f, 219f),
                    mats.RoadAsphaltMain, addCollider: false);

                // Dashed white centerlines (skipping intersections at z = -24, 44, 116, 184)
                for (float z = -12f; z <= 172f; z += 12f)
                {
                    if (Mathf.Abs(z - 44f) < 9f || Mathf.Abs(z - 116f) < 9f) continue;
                    CreateStaticBlock($"Dash_NS_{i}_{z:0}", roadsRoot.transform,
                        new Vector3(nsXs[i], 0.028f, z), new Vector3(0.20f, 0.01f, 4.4f),
                        mats.RoadMarkingWhite, addCollider: false);
                }
            }

            // 4 East-West Cross Streets (z = -24, +44, +116, +184; x = -72..+72 -> length 155m, width 11m)
            float[] ewZs = { -24f, 44f, 116f, 184f };
            for (int i = 0; i < ewZs.Length; i++)
            {
                CreateStaticBlock($"Road_EW_{i + 1}", roadsRoot.transform,
                    new Vector3(0f, 0.014f, ewZs[i]), new Vector3(155f, 0.024f, 11f),
                    mats.RoadAsphaltCross, addCollider: false);

                for (float x = -60f; x <= 60f; x += 12f)
                {
                    if (Mathf.Abs(x + 24f) < 9f || Mathf.Abs(x - 24f) < 9f) continue;
                    CreateStaticBlock($"Dash_EW_{i}_{x:0}", roadsRoot.transform,
                        new Vector3(x, 0.029f, ewZs[i]), new Vector3(4.4f, 0.01f, 0.20f),
                        mats.RoadMarkingWhite, addCollider: false);
                }
            }

            // Flush non-blocking sidewalks framing the central commercial, square, and park blocks
            float[] blockCentersZ = { 10f, 80f, 150f };
            float[] blockSpansZ = { 54f, 58f, 54f };
            for (int b = 0; b < blockCentersZ.Length; b++)
            {
                CreateTiledBox($"Sidewalk_CenterBlock_{b}_E", roadsRoot.transform,
                    new Vector3(17.0f, 0.015f, blockCentersZ[b]), new Vector3(3.0f, 0.03f, blockSpansZ[b]),
                    Quaternion.identity, mats.Sidewalk, 2.0f, collider: false);
                CreateTiledBox($"Sidewalk_CenterBlock_{b}_W", roadsRoot.transform,
                    new Vector3(-17.0f, 0.015f, blockCentersZ[b]), new Vector3(3.0f, 0.03f, blockSpansZ[b]),
                    Quaternion.identity, mats.Sidewalk, 2.0f, collider: false);
            }
        }

        private static void BuildMainStreetShops(Transform parent, MaterialSet mats)
        {
            GameObject shopsRoot = new GameObject("Town_CommercialShops");
            shopsRoot.transform.SetParent(parent, false);

            // Flush paved courtyard under the commercial block (x = -15.5..+15.5, z = -15..+36) — 100% walkable!
            CreateTiledBox("Commercial_CourtyardPaving", shopsRoot.transform,
                new Vector3(0f, 0.012f, 10f), new Vector3(31f, 0.024f, 52f),
                Quaternion.identity, mats.TownPaving, 2.5f, collider: false);

            // ONLY real, spacious, 100% enterable shops (zero fake solid dummy buildings!)
            // Placed at x = +8.5 facing East (+X) onto Cumhuriyet Caddesi (x = +24), with 6.2m wide walkways between them!
            BuildEnterableShopBuilding(
                "Shop_01_Pizzeria",
                "shop_pizza_01",
                EnterableShopKind.Pizzeria,
                shopsRoot.transform,
                new Vector3(8.5f, 0f, -6.5f),
                depthX: 10.0f, widthZ: 10.6f, height: 6.8f, storeys: 2,
                mats.BuildingFacadeC, 3.0f, mats.TownAwningTerracotta, pitchedRoof: true, mats,
                shopSeed: 4201);

            BuildEnterableShopBuilding(
                "Shop_02_Cafe",
                "shop_cafe_01",
                EnterableShopKind.Cafe,
                shopsRoot.transform,
                new Vector3(8.5f, 0f, 10.0f),
                depthX: 10.0f, widthZ: 10.6f, height: 6.8f, storeys: 2,
                mats.BuildingFacadeA, 3.0f, mats.TownAwningGreen, pitchedRoof: false, mats,
                shopSeed: 4254);

            BuildEnterableShopBuilding(
                "Shop_03_Market",
                "shop_market_01",
                EnterableShopKind.Market,
                shopsRoot.transform,
                new Vector3(8.5f, 0f, 26.5f),
                depthX: 10.0f, widthZ: 10.6f, height: 6.8f, storeys: 2,
                mats.BuildingFacadeB, 3.0f, mats.TownAwningNavy, pitchedRoof: true, mats,
                shopSeed: 4307);

            // Courtyard trees on the west side of the commercial block
            CreateTownTree("Courtyard_Tree_1", shopsRoot.transform, new Vector3(-9.5f, 0f, -6.0f), mats, 1.0f, 11);
            CreateTownTree("Courtyard_Tree_2", shopsRoot.transform, new Vector3(-9.5f, 0f, 10.0f), mats, 1.08f, 12);
            CreateTownTree("Courtyard_Tree_3", shopsRoot.transform, new Vector3(-9.5f, 0f, 26.0f), mats, 0.96f, 13);
        }

        private static void BuildGasStation(Transform parent, MaterialSet mats)
        {
            // Drive-through Gas Station in North-East Block (x = +32..+64, z = +134..+166), open from both x=+24 and x=+72!
            GameObject station = new GameObject("Zone_GasStation");
            station.transform.SetParent(parent, false);

            CreateTiledBox("Station_Forecourt", station.transform,
                new Vector3(48.0f, 0.015f, 150.0f), new Vector3(34.0f, 0.03f, 28.0f),
                Quaternion.identity, mats.HarborDock, 3.0f, collider: false);

            // Attendant Kiosk on south side of forecourt (z = +139.5) so East-West drive-through is wide open
            GameObject kiosk = new GameObject("Station_Kiosk");
            kiosk.transform.SetParent(station.transform, false);
            CreateTiledBox("Kiosk_Body", kiosk.transform,
                new Vector3(48.0f, 1.75f, 138.8f), new Vector3(8.2f, 3.5f, 4.4f),
                Quaternion.identity, mats.PlasterTrim, 2.5f, collider: true);
            CreateTiledBox("Kiosk_Roof", kiosk.transform,
                new Vector3(48.0f, 3.65f, 138.8f), new Vector3(8.8f, 0.32f, 5.0f),
                Quaternion.identity, mats.SedanTrimDark, 2.0f, collider: false);
            GameObject kioskWin = CreateTiledBox("Kiosk_Window", kiosk.transform,
                new Vector3(48.0f, 1.65f, 141.05f), new Vector3(5.2f, 1.85f, 0.08f),
                Quaternion.identity, mats.WindowLitWarm, 2.0f, collider: false, staticBatch: false);
            AttachWindowNightLight(kioskWin);

            // Canopy over pump island (centered at x = 48, z = 152, clearance 4.3m)
            GameObject canopy = new GameObject("Station_Canopy");
            canopy.transform.SetParent(station.transform, false);
            CreateTiledBox("Canopy_RoofDeck", canopy.transform,
                new Vector3(48.0f, 4.45f, 152.0f), new Vector3(13.6f, 0.46f, 7.2f),
                Quaternion.identity, mats.SedanTrimDark, 2.5f, collider: true);
            CreateTiledBox("Canopy_TealFascia", canopy.transform,
                new Vector3(48.0f, 4.52f, 152.0f), new Vector3(13.75f, 0.24f, 7.35f),
                Quaternion.identity, mats.TownFabricTeal, 2.0f, collider: false);

            CreateWorldSignText(
                "AKARYAKIT",
                canopy.transform,
                new Vector3(41.1f, 4.45f, 152.0f),
                Quaternion.Euler(0f, 90f, 0f),
                new Vector2(6.8f, 0.42f),
                new Color(0.95f, 0.96f, 0.96f, 1f));

            CreateTiledBox("Canopy_Pillar_W", canopy.transform,
                new Vector3(43.2f, 2.25f, 152.0f), new Vector3(0.45f, 4.3f, 0.45f),
                Quaternion.identity, mats.Concrete, 1.5f, collider: true);
            CreateTiledBox("Canopy_Pillar_E", canopy.transform,
                new Vector3(52.8f, 2.25f, 152.0f), new Vector3(0.45f, 4.3f, 0.45f),
                Quaternion.identity, mats.Concrete, 1.5f, collider: true);

            CreatePointLamp("Lamp_GasCanopy_W", canopy.transform, new Vector3(45.5f, 4.05f, 152.0f), new Color(0.95f, 0.96f, 1.0f), 18f, 2.6f);
            CreatePointLamp("Lamp_GasCanopy_E", canopy.transform, new Vector3(50.5f, 4.05f, 152.0f), new Color(0.95f, 0.96f, 1.0f), 18f, 2.6f);

            CreateFuelPump("FuelPump_01", "fuel_pump_01", station.transform, new Vector3(45.6f, 0.03f, 152.0f), mats);
            CreateFuelPump("FuelPump_02", "fuel_pump_02", station.transform, new Vector3(50.4f, 0.03f, 152.0f), mats);
        }

        private static void CreateFuelPump(string name, string triggerId, Transform parent, Vector3 worldPos, MaterialSet mats)
        {
            GameObject pump = new GameObject(name);
            pump.transform.SetParent(parent, false);
            pump.transform.position = worldPos;

            GameObject mesh = InstantiateBlenderModel("Gas_FuelPump.fbx", "FuelPumpMesh", pump.transform, mats);
            if (mesh == null)
            {
                CreateStaticPart("PumpBody", PrimitiveType.Cube, pump.transform,
                    new Vector3(0f, 0.85f, 0f), Quaternion.identity, new Vector3(0.86f, 1.70f, 0.62f), mats.TownInteriorPaintPearl);
                CreateStaticPart("PumpHeader", PrimitiveType.Cube, pump.transform,
                    new Vector3(0f, 1.78f, 0f), Quaternion.identity, new Vector3(0.92f, 0.24f, 0.68f), mats.TownFabricTeal);
            }

            BoxCollider col = pump.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.05f, 0f);
            col.size = new Vector3(1.18f, 2.10f, 0.72f);
            MarkStaticEnvironment(pump);

            GameObject trigGo = new GameObject("FuelPumpTrigger");
            trigGo.transform.SetParent(pump.transform, false);
            trigGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            InteractableTrigger trig = trigGo.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.FuelPump, triggerId, "ui.btn.refuel", 5.2f);
        }

        private static void BuildSideJobStartPoints(Transform parent, MaterialSet mats)
        {
            GameObject jobsRoot = new GameObject("Town_SideJobBoards");
            jobsRoot.transform.SetParent(parent, false);

            // a) Pizza Delivery right outside PİZZA on Cumhuriyet Caddesi
            CreateJobStartRing(
                "JobBoard_Pizza",
                "job_board_pizza",
                JobType.PizzaDelivery,
                "job.pizza.name",
                "job.pizza.loc",
                jobsRoot.transform,
                new Vector3(16.8f, 0.03f, -6.5f),
                radius: 4.6f,
                mats.JobRingGlow);

            // b) Taxi Rank at NE edge of Town Square
            GameObject taxiBoard = CreateJobStartRing(
                "JobBoard_Taxi",
                "job_board_taxi",
                JobType.Taxi,
                "job.taxi.name",
                "job.taxi.loc",
                jobsRoot.transform,
                new Vector3(16.8f, 0.03f, 96.0f),
                radius: 4.6f,
                mats.JobRingGlow);

            CreateStaticPart("TaxiSignPole", PrimitiveType.Cylinder, taxiBoard.transform,
                new Vector3(-1.35f, 1.35f, 0f), Quaternion.identity, new Vector3(0.10f, 1.35f, 0.10f), mats.SedanTrimDark);
            CreateStaticPart("TaxiSignPlate", PrimitiveType.Cube, taxiBoard.transform,
                new Vector3(-1.35f, 2.75f, 0f), Quaternion.identity, new Vector3(0.12f, 0.42f, 1.15f), mats.RoadMarkingYellow);
            CreateWorldSignText("TAKSİ", taxiBoard.transform,
                new Vector3(-1.26f, 2.75f, 0f), Quaternion.Euler(0f, -90f, 0f),
                new Vector2(1.05f, 0.36f), new Color(0.10f, 0.11f, 0.13f, 1f));

            // c) Street Cleanup in front of the Clock Tower at Town Square
            CreateJobStartRing(
                "JobBoard_Cleanup",
                "job_board_cleanup",
                JobType.StreetCleanup,
                "job.cleanup.name",
                "job.cleanup.loc",
                jobsRoot.transform,
                new Vector3(-9.5f, 0.03f, 80.0f),
                radius: 3.4f,
                mats.JobRingGlow);

            // d) House Painting Job NPC + Ring at Çınarlı Park
            GameObject paintBoard = CreateJobStartRing(
                "JobBoard_HousePainting",
                "job_board_painting",
                JobType.HousePainting,
                "job.paint.name",
                "job.paint.loc",
                jobsRoot.transform,
                new Vector3(-13.5f, 0.03f, 148.0f),
                radius: 3.6f,
                mats.JobRingGlow);

            GameObject painterNpc = new GameObject("NPC_HousePainterContractor");
            painterNpc.transform.SetParent(paintBoard.transform, false);
            painterNpc.transform.localPosition = new Vector3(-0.75f, 0f, 0.55f);
            painterNpc.transform.localRotation = Quaternion.Euler(0f, 125f, 0f);
            CapsuleCollider npcCol = painterNpc.AddComponent<CapsuleCollider>();
            npcCol.center = new Vector3(0f, 0.9f, 0f);
            npcCol.radius = 0.36f;
            npcCol.height = 1.78f;

            ProceduralHumanoid painterHum = BuildBlenderHumanoid(
                painterNpc.transform,
                mats,
                BlenderCharacterVariant.Worker,
                mats.PedShirtMustard,
                mats.CharPantsKhaki,
                mats.CharSkinMedium,
                mats.CharHairBrown,
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

            // Grounded wooden Street Notice Board instead of unrealistic glowing arcade ground circles!
            Material woodMat = _activeMaterialSet != null ? _activeMaterialSet.TownFurnitureWood : glowMat;
            Material darkMat = _activeMaterialSet != null ? _activeMaterialSet.SedanTrimDark : glowMat;
            Material paperMat = _activeMaterialSet != null ? _activeMaterialSet.TownInteriorPaintCream : glowMat;

            GameObject boardStand = new GameObject("NoticeBoardStand");
            boardStand.transform.SetParent(root.transform, false);
            CreateStaticPart("PostL", PrimitiveType.Cube, boardStand.transform, new Vector3(-0.46f, 0.88f, 0f), Quaternion.identity, new Vector3(0.08f, 1.76f, 0.08f), darkMat);
            CreateStaticPart("PostR", PrimitiveType.Cube, boardStand.transform, new Vector3(0.46f, 0.88f, 0f), Quaternion.identity, new Vector3(0.08f, 1.76f, 0.08f), darkMat);
            CreateStaticPart("FrameBack", PrimitiveType.Cube, boardStand.transform, new Vector3(0f, 1.38f, 0f), Quaternion.identity, new Vector3(1.04f, 0.78f, 0.09f), woodMat);
            CreateStaticPart("PaperFlyer1", PrimitiveType.Cube, boardStand.transform, new Vector3(-0.22f, 1.42f, 0.052f), Quaternion.identity, new Vector3(0.32f, 0.42f, 0.01f), paperMat);
            CreateStaticPart("PaperFlyer2", PrimitiveType.Cube, boardStand.transform, new Vector3(0.22f, 1.34f, 0.052f), Quaternion.identity, new Vector3(0.32f, 0.36f, 0.01f), paperMat);
            CreateStaticPart("RoofCap", PrimitiveType.Cube, boardStand.transform, new Vector3(0f, 1.80f, 0f), Quaternion.identity, new Vector3(1.16f, 0.08f, 0.24f), darkMat);
            MarkStaticEnvironment(boardStand);

            InteractableTrigger trig = root.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.JobBoard, triggerId, "ui.btn.job_board", radius);

            JobBoardPoint board = root.AddComponent<JobBoardPoint>();
            board.Configure(jobType, nameKey, locKey, null);
            return root;
        }

        private static void BuildStreetVendors(Transform parent, MaterialSet mats)
        {
            GameObject vendorsRoot = new GameObject("Town_StreetVendors");
            vendorsRoot.transform.SetParent(parent, false);

            // 1. Kerem — Kasaba Meydanı Giyim & Spor Tezgâhı (Running Shoes + Jackets!)
            CreateStreetVendorStall(
                "Vendor_Gear_Kerem",
                "vendor_gear",
                "Kerem",
                "Giyim & Spor Satıcısı",
                "GİYİM & SPOR",
                vendorsRoot.transform,
                new Vector3(12.2f, 0.02f, 80.0f),
                Quaternion.Euler(0f, -90f, 0f),
                mats.TownAwningTerracotta,
                BlenderCharacterVariant.CasualMale,
                mats.PedShirtBurgundy,
                mats.CharPantsDenim,
                mats.CharSkinMedium,
                mats.CharHair,
                mats);

            // 2. Tarık — Tamirhane Yanı Oto Parça & Modifiye Tezgâhı (Turbo Kit + Emergency Fuel!)
            CreateStreetVendorStall(
                "Vendor_Auto_Tarik",
                "vendor_auto",
                "Tarık",
                "Oto Parça & Modifiye",
                "OTO MODİFİYE",
                vendorsRoot.transform,
                new Vector3(13.8f, 0.02f, -35.0f),
                Quaternion.identity,
                mats.TownAwningNavy,
                BlenderCharacterVariant.Worker,
                mats.PedShirtOlive,
                mats.CharPantsKhaki,
                mats.CharSkinWarm,
                mats.CharHairBrown,
                mats);
        }

        private static void CreateStreetVendorStall(
            string stallName,
            string vendorShopId,
            string vendorName,
            string vendorRole,
            string signText,
            Transform parent,
            Vector3 pos,
            Quaternion rot,
            Material awningMat,
            BlenderCharacterVariant variant,
            Material shirtMat,
            Material pantsMat,
            Material skinMat,
            Material hairMat,
            MaterialSet mats)
        {
            GameObject stall = new GameObject(stallName);
            stall.transform.SetParent(parent, false);
            stall.transform.SetPositionAndRotation(pos, rot);

            // Wooden counter table + canopy awning + sign
            CreateStaticPart("CounterBody", PrimitiveType.Cube, stall.transform,
                new Vector3(0f, 0.48f, 0f), Quaternion.identity, new Vector3(2.4f, 0.96f, 0.85f), mats.TownFurnitureWood);
            CreateStaticPart("CounterTop", PrimitiveType.Cube, stall.transform,
                new Vector3(0f, 0.99f, 0f), Quaternion.identity, new Vector3(2.55f, 0.07f, 0.95f), mats.PlasterTrim);
            CreateStaticPart("PostL", PrimitiveType.Cube, stall.transform,
                new Vector3(-1.12f, 1.35f, -0.34f), Quaternion.identity, new Vector3(0.09f, 2.70f, 0.09f), mats.SedanTrimDark);
            CreateStaticPart("PostR", PrimitiveType.Cube, stall.transform,
                new Vector3(1.12f, 1.35f, -0.34f), Quaternion.identity, new Vector3(0.09f, 2.70f, 0.09f), mats.SedanTrimDark);
            CreateStaticPart("CanopyAwning", PrimitiveType.Cube, stall.transform,
                new Vector3(0f, 2.66f, 0.05f), Quaternion.Euler(10f, 0f, 0f), new Vector3(2.75f, 0.14f, 1.55f), awningMat);

            CreateWorldSignText(
                signText,
                stall.transform,
                new Vector3(0f, 2.64f, 0.84f),
                Quaternion.Euler(0f, 180f, 0f),
                new Vector2(2.3f, 0.34f),
                new Color(0.98f, 0.97f, 0.94f, 1f));

            BoxCollider col = stall.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.52f, 0f);
            col.size = new Vector3(2.55f, 1.04f, 0.95f);
            MarkStaticEnvironment(stall);

            // Blender Street Vendor NPC standing behind the counter
            GameObject npcObj = new GameObject($"NPC_{vendorName}");
            npcObj.transform.SetParent(stall.transform, false);
            npcObj.transform.localPosition = new Vector3(0f, 0f, -0.82f);
            npcObj.transform.localRotation = Quaternion.identity;

            CapsuleCollider npcCol = npcObj.AddComponent<CapsuleCollider>();
            npcCol.center = new Vector3(0f, 0.9f, 0f);
            npcCol.radius = 0.36f;
            npcCol.height = 1.78f;
            Rigidbody rb = npcObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            ProceduralHumanoid hum = BuildBlenderHumanoid(npcObj.transform, mats, variant, shirtMat, pantsMat, skinMat, hairMat, 1.76f);
            hum.SetAutoRandomWave(true);

            TownPedestrian vendorPed = npcObj.AddComponent<TownPedestrian>();
            vendorPed.ConfigureCitizen(vendorName, vendorRole, null, brave: true, shopId: vendorShopId);

            GameObject trigGo = new GameObject("VendorTrigger");
            trigGo.transform.SetParent(stall.transform, false);
            trigGo.transform.localPosition = new Vector3(0f, 0.9f, 0.65f);
            InteractableTrigger trig = trigGo.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.ShopCounter, vendorShopId, "ui.btn.vendor", 3.2f);
        }

        private static void BuildVendingAndMailboxes(Transform parent, MaterialSet mats)
        {
            GameObject propsRoot = new GameObject("Town_VendingAndMailboxes");
            propsRoot.transform.SetParent(parent, false);

            CreateVendingMachine("Vending_Square", "vending_square", propsRoot.transform,
                new Vector3(-13.8f, 0.03f, 72.0f), Quaternion.Euler(0f, 90f, 0f), mats);
            CreateVendingMachine("Vending_CafePatio", "vending_promenade", propsRoot.transform,
                new Vector3(14.5f, 0.03f, 18.0f), Quaternion.Euler(0f, 90f, 0f), mats);
            CreateVendingMachine("Vending_GasStation", "vending_gasstation", propsRoot.transform,
                new Vector3(53.2f, 0.03f, 139.2f), Quaternion.Euler(0f, 0f, 0f), mats);

            (Vector3 pos, float yaw)[] mailboxes =
            {
                (new Vector3(16.2f, 0.03f, 2.5f), 90f),
                (new Vector3(14.5f, 0.03f, 64.0f), 90f),
                (new Vector3(-30.5f, 0.03f, 56.0f), 90f),
                (new Vector3(-30.5f, 0.03f, 128.0f), 90f)
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

            GameObject mesh = InstantiateBlenderModel("Town_VendingMachine.fbx", "VendingMachineMesh", vm.transform, mats);
            if (mesh == null)
            {
                CreateStaticPart("Cabinet", PrimitiveType.Cube, vm.transform,
                    new Vector3(0f, 0.98f, 0f), Quaternion.identity, new Vector3(0.95f, 1.96f, 0.78f), mats.TownFabricTeal);
            }

            BoxCollider col = vm.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.99f, 0f);
            col.size = new Vector3(1.02f, 1.98f, 0.82f);
            MarkStaticEnvironment(vm);

            GameObject trigObj = new GameObject("VendingTrigger");
            trigObj.transform.SetParent(vm.transform, false);
            trigObj.transform.localPosition = new Vector3(0f, 0.9f, 0.48f);
            InteractableTrigger trig = trigObj.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.VendingMachine, id, "ui.btn.vending", 2.2f);
        }

        private static void CreateMailbox(string name, string id, Transform parent, Vector3 pos, Quaternion rot, MaterialSet mats)
        {
            GameObject mb = new GameObject(name);
            mb.transform.SetParent(parent, false);
            mb.transform.SetPositionAndRotation(pos, rot);

            GameObject mesh = InstantiateBlenderModel("Town_Mailbox.fbx", "MailboxMesh", mb.transform, mats);
            if (mesh == null)
            {
                CreateStaticPart("Post", PrimitiveType.Cylinder, mb.transform,
                    new Vector3(0f, 0.52f, 0f), Quaternion.identity, new Vector3(0.12f, 0.52f, 0.12f), mats.SedanTrimDark);
                CreateStaticPart("BoxBody", PrimitiveType.Cube, mb.transform,
                    new Vector3(0f, 1.15f, 0f), Quaternion.identity, new Vector3(0.46f, 0.52f, 0.38f), mats.ContainerBlue);
            }

            BoxCollider col = mb.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.76f, 0f);
            col.size = new Vector3(0.48f, 1.52f, 0.44f);
            MarkStaticEnvironment(mb);

            GameObject trigObj = new GameObject("MailboxTrigger");
            trigObj.transform.SetParent(mb.transform, false);
            trigObj.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            InteractableTrigger trig = trigObj.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.InspectObject, id, "ui.btn.inspect", 1.9f);
        }

        private static void BuildTownSquare(
            Transform parent,
            MaterialSet mats,
            PrefabSet prefabs,
            List<SeatInteractable> benchSeatCollector)
        {
            GameObject sqRoot = new GameObject("Town_Square");
            sqRoot.transform.SetParent(parent, false);

            // Flush non-blocking Town Square paving (x = -16.5..+16.5, z = +52..+108, centered at (0, 0, 80))
            CreateTiledBox("Square_Paving", sqRoot.transform,
                new Vector3(0f, 0.014f, 80f), new Vector3(33f, 0.028f, 56f),
                Quaternion.identity, mats.TownPaving, metersPerTile: 2.0f, collider: false);

            Vector3 fountainCenter = new Vector3(1.5f, 0.02f, 80.0f);
            GameObject fountain = new GameObject("Square_Fountain");
            fountain.transform.SetParent(sqRoot.transform, false);
            fountain.transform.position = fountainCenter;

            GameObject blenderFountain = InstantiateBlenderModel("Town_Fountain.fbx", "BlenderFountainMesh", fountain.transform, mats);
            if (blenderFountain != null)
            {
                // Clean octagonal-footprint capsule/box collider for the fountain basin + central column
                CapsuleCollider basinCol = fountain.AddComponent<CapsuleCollider>();
                basinCol.center = new Vector3(0f, 0.38f, 0f);
                basinCol.radius = 3.12f;
                basinCol.height = 0.78f;

                GameObject centerColGo = new GameObject("FountainCenterCollider");
                centerColGo.transform.SetParent(fountain.transform, false);
                CapsuleCollider pedCol = centerColGo.AddComponent<CapsuleCollider>();
                pedCol.center = new Vector3(0f, 1.65f, 0f);
                pedCol.radius = 0.95f;
                pedCol.height = 3.30f;

                MarkStaticEnvironment(fountain);
            }
            else
            {
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
            }

            // Live animated sparkling water jets & cascading mist droplets on the fountain!
            CreateFountainWaterParticles(fountain.transform, mats.PoolWaterClear);

            // 4 Carved Travertine Flowerbed Planters with lush shrubs & Mediterranean blooms framing Town Square
            Vector3[] planterSpots =
            {
                new Vector3(12.2f, 0.02f, 68.5f),
                new Vector3(12.2f, 0.02f, 91.5f),
                new Vector3(-8.2f, 0.02f, 68.5f),
                new Vector3(-8.2f, 0.02f, 91.5f)
            };
            for (int p = 0; p < planterSpots.Length; p++)
            {
                CreateSquarePlanter($"Square_Planter_{p + 1}", sqRoot.transform, planterSpots[p], mats, p);
            }

            float[] benchAngles = { 30f, 90f, 150f, 210f, 270f, 330f };
            SeatInteractable[] npcBenchSlots = new SeatInteractable[2];
            for (int i = 0; i < benchAngles.Length; i++)
            {
                float rad = benchAngles[i] * Mathf.Deg2Rad;
                Vector3 pos = fountainCenter + new Vector3(Mathf.Sin(rad) * 6.2f, 0f, Mathf.Cos(rad) * 6.2f);
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
                new Vector3(12.5f, 0f, 58f),
                new Vector3(12.5f, 0f, 102f),
                new Vector3(-12.5f, 0f, 58f),
                new Vector3(-12.5f, 0f, 102f)
            };
            for (int i = 0; i < sqTrees.Length; i++)
            {
                CreateTownTree($"Square_Tree_{i + 1}", sqRoot.transform, sqTrees[i], mats, 1.0f + (i % 2) * 0.14f, i);
            }

            // Iconic Blender 3D Galata Kulesi overlooking Beyoğlu / Taksim Square!
            BuildGalataTowerLandmark(sqRoot.transform, new Vector3(-13.8f, 0.02f, 80.0f), mats);

            // Nostaljik İstiklal Kırmızı Tramvayı on steel rails + İstanbul Simitçi Arabası!
            BuildIstanbulTramAndSimitCart(sqRoot.transform, mats);

            Vector3[] lampPositions =
            {
                new Vector3(11.5f, 0.02f, 68.0f),
                new Vector3(11.5f, 0.02f, 92.0f),
                new Vector3(-9.5f, 0.02f, 68.0f),
                new Vector3(-9.5f, 0.02f, 92.0f)
            };
            for (int i = 0; i < lampPositions.Length; i++)
            {
                PlacePrefabInstance(prefabs.StreetLamp, sqRoot.transform, lampPositions[i], Quaternion.Euler(0f, i * 90f, 0f));
                CreatePointLamp($"Square_LampLight_{i + 1}", sqRoot.transform,
                    lampPositions[i] + Vector3.up * 5.1f, new Color(1.0f, 0.80f, 0.46f), 20f, 2.2f);
            }
        }

        private static void CreateFountainWaterParticles(Transform fountainRoot, Material waterMat)
        {
            GameObject sprayGo = new GameObject("Fountain_WaterSpray");
            sprayGo.transform.SetParent(fountainRoot, false);
            sprayGo.transform.localPosition = new Vector3(0f, 3.50f, 0f);
            sprayGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem ps = sprayGo.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 2.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.45f, 2.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.115f);
            main.startColor = new Color(0.82f, 0.95f, 1.0f, 0.72f);
            main.gravityModifier = 0.62f;
            main.maxParticles = 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 72f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.12f;

            ParticleSystemRenderer psr = sprayGo.GetComponent<ParticleSystemRenderer>();
            if (waterMat != null)
            {
                psr.sharedMaterial = waterMat;
            }
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
        }

        private static void CreateSquarePlanter(string name, Transform parent, Vector3 basePos, MaterialSet mats, int idx)
        {
            GameObject planter = new GameObject(name);
            planter.transform.SetParent(parent, false);
            planter.transform.position = basePos;

            Material stone = mats.TravertineStone != null ? mats.TravertineStone : mats.PlasterTrim;
            CreateLocalTiledBox("Planter_StoneBox", planter.transform,
                new Vector3(0f, 0.28f, 0f), new Vector3(1.85f, 0.56f, 1.85f),
                Quaternion.identity, stone, 1.4f, collider: true);
            CreateLocalTiledBox("Planter_Rim", planter.transform,
                new Vector3(0f, 0.58f, 0f), new Vector3(2.02f, 0.08f, 2.02f),
                Quaternion.identity, stone, 1.4f, collider: false);
            CreateLocalTiledBox("Planter_Soil", planter.transform,
                new Vector3(0f, 0.54f, 0f), new Vector3(1.62f, 0.06f, 1.62f),
                Quaternion.identity, mats.TownBark, 1.2f, collider: false);

            // Lush layered greenery + bright flower accents
            Material shrubMat = idx % 2 == 0 ? mats.TownFoliageA : mats.TownFoliageB;
            CreateStaticPart("Shrub_Center", PrimitiveType.Sphere, planter.transform,
                new Vector3(0f, 0.88f, 0f), Quaternion.identity, new Vector3(1.22f, 0.82f, 1.22f), shrubMat);
            CreateStaticPart("Shrub_SideA", PrimitiveType.Sphere, planter.transform,
                new Vector3(-0.42f, 0.78f, 0.34f), Quaternion.identity, new Vector3(0.76f, 0.56f, 0.76f), mats.TownHedge);
            CreateStaticPart("Shrub_SideB", PrimitiveType.Sphere, planter.transform,
                new Vector3(0.42f, 0.76f, -0.32f), Quaternion.identity, new Vector3(0.74f, 0.54f, 0.74f), mats.TownHedge);

            Material bloomMat = idx % 2 == 0 ? mats.TownAwningTerracotta : mats.TownFabricMustard;
            CreateStaticPart("Bloom_1", PrimitiveType.Sphere, planter.transform, new Vector3(-0.26f, 1.12f, 0.20f), Quaternion.identity, Vector3.one * 0.18f, bloomMat);
            CreateStaticPart("Bloom_2", PrimitiveType.Sphere, planter.transform, new Vector3(0.28f, 1.08f, -0.16f), Quaternion.identity, Vector3.one * 0.16f, bloomMat);
            CreateStaticPart("Bloom_3", PrimitiveType.Sphere, planter.transform, new Vector3(0.08f, 1.22f, 0.14f), Quaternion.identity, Vector3.one * 0.15f, mats.TownInteriorPaintCream);
        }

        private static void BuildGalataTowerLandmark(Transform parent, Vector3 basePos, MaterialSet mats)
        {
            GameObject galata = new GameObject("Istanbul_GalataKulesi");
            galata.transform.SetParent(parent, false);
            galata.transform.position = basePos;

            GameObject mesh = InstantiateBlenderModel("Istanbul_GalataTower.fbx", "GalataTowerMesh", galata.transform, mats);
            if (mesh != null)
            {
                CapsuleCollider col = galata.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 12.0f, 0f);
                col.radius = 3.75f;
                col.height = 24.0f;
                MarkStaticEnvironment(galata);
                CreatePointLamp("Lamp_GalataBalcony", galata.transform, basePos + new Vector3(0f, 16.5f, 0f), new Color(1.0f, 0.82f, 0.48f), 26f, 3.2f);
            }
            else
            {
                BuildClockTower(parent, basePos, mats);
            }
        }

        private static void BuildIstanbulTramAndSimitCart(Transform parent, MaterialSet mats)
        {
            // Embedded steel tram rails along East side of Beyoğlu Square (x = +10.2, z = 54..104)
            CreateStaticBlock("TramRail_W", parent, new Vector3(9.42f, 0.030f, 79.0f), new Vector3(0.08f, 0.015f, 50.0f), mats.SedanChrome, addCollider: false);
            CreateStaticBlock("TramRail_E", parent, new Vector3(10.98f, 0.030f, 79.0f), new Vector3(0.08f, 0.015f, 50.0f), mats.SedanChrome, addCollider: false);

            // Nostaljik Kırmızı İstiklal Tramvayı (T2 Taksim-Tünel)
            GameObject tram = new GameObject("Istanbul_IstiklalTramvayi");
            tram.transform.SetParent(parent, false);
            tram.transform.position = new Vector3(10.2f, 0.03f, 65.5f);
            GameObject tramMesh = InstantiateBlenderModel("Istanbul_Tram.fbx", "IstiklalTramMesh", tram.transform, mats);
            if (tramMesh != null)
            {
                BoxCollider tCol = tram.AddComponent<BoxCollider>();
                tCol.center = new Vector3(0f, 1.65f, 0f);
                tCol.size = new Vector3(2.25f, 3.30f, 8.40f);
                MarkStaticEnvironment(tram);
                CreateWorldSignText("TAKSİM · TÜNEL", tram.transform, new Vector3(0f, 3.00f, 4.22f), Quaternion.Euler(0f, 180f, 0f), new Vector2(1.4f, 0.24f), new Color(0.14f, 0.10f, 0.08f, 1f));
            }

            // Iconic Red Istanbul Simitçi Cart on Beyoğlu Square
            GameObject simit = new GameObject("Istanbul_SimitciArabasi");
            simit.transform.SetParent(parent, false);
            simit.transform.SetPositionAndRotation(new Vector3(6.8f, 0.03f, 88.5f), Quaternion.Euler(0f, -125f, 0f));
            GameObject simitMesh = InstantiateBlenderModel("Istanbul_SimitCart.fbx", "SimitCartMesh", simit.transform, mats);
            if (simitMesh != null)
            {
                BoxCollider sCol = simit.AddComponent<BoxCollider>();
                sCol.center = new Vector3(0f, 0.85f, 0f);
                sCol.size = new Vector3(1.15f, 1.70f, 1.45f);
                MarkStaticEnvironment(simit);
            }
        }

        private static void BuildClockTower(Transform parent, Vector3 basePos, MaterialSet mats)
        {
            GameObject tower = new GameObject("Square_ClockTower");
            tower.transform.SetParent(parent, false);

            Material stone = mats.TravertineStone != null ? mats.TravertineStone : mats.PlasterTrim;

            // Stepped stone plinth + carved masonry base (replaces blank concrete cube!)
            CreateTiledBox("Tower_Plinth", tower.transform,
                basePos + new Vector3(0f, 0.25f, 0f), new Vector3(4.8f, 0.50f, 4.8f),
                Quaternion.identity, stone, 2.0f, collider: true);
            CreateTiledBox("Tower_Base", tower.transform,
                basePos + new Vector3(0f, 1.85f, 0f), new Vector3(4.2f, 2.75f, 4.2f),
                Quaternion.identity, mats.TownBrickHouse, 1.6f, collider: true);
            CreateTiledBox("Tower_BaseCornice", tower.transform,
                basePos + new Vector3(0f, 3.32f, 0f), new Vector3(4.55f, 0.28f, 4.55f),
                Quaternion.identity, stone, 2.0f, collider: false);

            // 4 Architectural Travertine Corner Pilasters framing the base & shaft
            float[] cornerSigns = { -1f, 1f };
            for (int cx = 0; cx < 2; cx++)
            {
                for (int cz = 0; cz < 2; cz++)
                {
                    float px = cornerSigns[cx] * 2.04f;
                    float pz = cornerSigns[cz] * 2.04f;
                    CreateTiledBox($"Tower_BasePilaster_{cx}_{cz}", tower.transform,
                        basePos + new Vector3(px, 1.85f, pz), new Vector3(0.42f, 2.85f, 0.42f),
                        Quaternion.identity, stone, 1.5f, collider: false);

                    float spx = cornerSigns[cx] * 1.64f;
                    float spz = cornerSigns[cz] * 1.64f;
                    CreateTiledBox($"Tower_ShaftPilaster_{cx}_{cz}", tower.transform,
                        basePos + new Vector3(spx, 7.40f, spz), new Vector3(0.32f, 8.20f, 0.32f),
                        Quaternion.identity, stone, 1.5f, collider: false);
                }
            }

            // Recessed ornate wooden entrance portal facing East (+X) onto Town Square
            CreateTiledBox("Tower_DoorFrame", tower.transform,
                basePos + new Vector3(2.12f, 1.38f, 0f), new Vector3(0.18f, 2.35f, 1.48f),
                Quaternion.identity, stone, 1.4f, collider: false);
            CreateTiledBox("Tower_DoorLeaf", tower.transform,
                basePos + new Vector3(2.18f, 1.32f, 0f), new Vector3(0.08f, 2.18f, 1.18f),
                Quaternion.identity, mats.TownDoorWood, 1.4f, collider: false);

            CreateTiledBox("Tower_Shaft", tower.transform,
                basePos + new Vector3(0f, 7.45f, 0f), new Vector3(3.4f, 8.2f, 3.4f),
                Quaternion.identity, stone, 2.4f, collider: true);
            CreateTiledBox("Tower_MidMold", tower.transform,
                basePos + new Vector3(0f, 11.45f, 0f), new Vector3(3.85f, 0.26f, 3.85f),
                Quaternion.identity, stone, 2.0f, collider: false);

            // Tall lit narrow lancet windows on East & West shaft faces
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject win = CreateTiledBox($"Tower_LancetWin_{side}", tower.transform,
                    basePos + new Vector3(side * 1.72f, 7.60f, 0f), new Vector3(0.08f, 2.60f, 0.72f),
                    Quaternion.identity, mats.WindowLitWarm, 1.5f, collider: false, staticBatch: false);
                AttachWindowNightLight(win);
            }

            CreateTiledBox("Tower_ClockBox", tower.transform,
                basePos + new Vector3(0f, 12.9f, 0f), new Vector3(3.7f, 2.8f, 3.7f),
                Quaternion.identity, mats.TownBrickHouse, 1.6f, collider: true);

            CreateTiledBox("Tower_Cornice", tower.transform,
                basePos + new Vector3(0f, 14.45f, 0f), new Vector3(4.25f, 0.36f, 4.25f),
                Quaternion.identity, stone, 2.0f, collider: false);
            CreateTiledBox("Tower_CapLower", tower.transform,
                basePos + new Vector3(0f, 15.05f, 0f), new Vector3(3.0f, 0.9f, 3.0f),
                Quaternion.identity, mats.TownRoofTile, 1.5f, collider: false);
            CreateTiledBox("Tower_CapUpper", tower.transform,
                basePos + new Vector3(0f, 15.75f, 0f), new Vector3(1.5f, 0.7f, 1.5f),
                Quaternion.identity, mats.TownRoofTile, 1.5f, collider: false);

            Vector3[] faceOffsets =
            {
                new Vector3(1.88f, 12.9f, 0f),
                new Vector3(-1.88f, 12.9f, 0f),
                new Vector3(0f, 12.9f, 1.88f),
                new Vector3(0f, 12.9f, -1.88f)
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
                    Vector3.zero, new Vector3(1.65f, 0.04f, 1.65f), mats.LampGlow, removeCollider: true);
                dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                MarkStaticPaintable(dial);
                NightLight dialNl = dial.AddComponent<NightLight>();
                dialNl.Configure(null, new[] { dial.GetComponent<Renderer>() }, new Color(1.0f, 0.85f, 0.55f) * 2.5f);

                GameObject hourHand = CreatePrimitiveChild("HourHand", PrimitiveType.Cube, faceRoot.transform,
                    new Vector3(0f, 0.22f, 0.05f), new Vector3(0.09f, 0.48f, 0.04f), mats.SedanTrimDark, removeCollider: true);
                MarkStaticEnvironment(hourHand);

                GameObject minHand = CreatePrimitiveChild("MinuteHand", PrimitiveType.Cube, faceRoot.transform,
                    new Vector3(0.22f, 0f, 0.05f), new Vector3(0.54f, 0.07f, 0.04f), mats.SedanTrimDark, removeCollider: true);
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

            Material[] wallPalette = { mats.PlasterTrim, mats.BuildingFacadeB, mats.TownBrickHouse, mats.BuildingFacadeA };

            // 9 West-side spacious enterable houses (x = -43.5, facing East onto Çınar Caddesi x = -24, 18.5m spacing!)
            float[] westHouseZs =
            {
                -8.5f, 10.0f, 28.5f,
                61.5f, 80.0f, 98.5f,
                131.5f, 150.0f, 168.5f
            };

            for (int i = 0; i < westHouseZs.Length; i++)
            {
                Material wallMat = wallPalette[i % wallPalette.Length];
                BuildEnterableResidentialHouse(
                    $"House_West_{i + 1:00}",
                    $"house_w_{i + 1:00}",
                    $"Balat Caddesi No: {i + 1}",
                    resRoot.transform,
                    new Vector3(-43.5f, 0f, westHouseZs[i]),
                    Quaternion.identity,
                    depthX: 9.2f,
                    widthZ: 10.6f,
                    wallMat: wallMat,
                    hasChimney: i % 2 == 0,
                    hasGardenTree: i % 3 == 0,
                    mats: mats,
                    houseSeed: 2000 + i * 37);
            }

            // 3 East-side spacious enterable houses (x = +43.5, facing West 180° onto Cumhuriyet Caddesi x = +24!)
            float[] eastHouseZs = { 61.5f, 80.0f, 98.5f };
            for (int i = 0; i < eastHouseZs.Length; i++)
            {
                Material wallMat = wallPalette[(i + 2) % wallPalette.Length];
                BuildEnterableResidentialHouse(
                    $"House_East_{i + 1:00}",
                    $"house_e_{i + 1:00}",
                    $"Karaköy Yokuşu No: {i + 10}",
                    resRoot.transform,
                    new Vector3(43.5f, 0f, eastHouseZs[i]),
                    Quaternion.Euler(0f, 180f, 0f),
                    depthX: 9.2f,
                    widthZ: 10.6f,
                    wallMat: wallMat,
                    hasChimney: i % 2 == 1,
                    hasGardenTree: i == 1,
                    mats: mats,
                    houseSeed: 3100 + i * 41);
            }

            // South-East Block (x = +48, z = +10): Çınarlı Açık Yüzme Havuzu (Swimmable Outdoor Pool Club!)
            BuildTownSwimmingPool(parent, mats, benchSeatCollector);

            // Central North Block: Ortaköy / Sultanahmet Camii & Avlusu (x = -16..+16, z = +124..+176)
            GameObject parkRoot = new GameObject("Town_IstanbulMosquePlaza");
            parkRoot.transform.SetParent(parent, false);

            CreateTiledBox("MosquePlaza_Paving", parkRoot.transform,
                new Vector3(0f, 0.014f, 150f), new Vector3(31f, 0.028f, 50f),
                Quaternion.identity, mats.TownPaving, 2.2f, collider: false);

            GameObject mosqueGo = new GameObject("Istanbul_BogazCamii");
            mosqueGo.transform.SetParent(parkRoot.transform, false);
            mosqueGo.transform.SetPositionAndRotation(new Vector3(0f, 0.02f, 155.5f), Quaternion.Euler(0f, 180f, 0f));
            GameObject mosqueMesh = InstantiateBlenderModel("Istanbul_Mosque.fbx", "IstanbulMosqueMesh", mosqueGo.transform, mats);
            if (mosqueMesh != null)
            {
                BoxCollider mCol = mosqueGo.AddComponent<BoxCollider>();
                mCol.center = new Vector3(0f, 5.0f, -1.0f);
                mCol.size = new Vector3(17.5f, 10.0f, 17.5f);
                MarkStaticEnvironment(mosqueGo);
                CreatePointLamp("Lamp_MosqueCourtyard", mosqueGo.transform, new Vector3(0f, 7.5f, 143.5f), new Color(1.0f, 0.86f, 0.56f), 28f, 3.0f);
            }

            Vector3[] parkTrees =
            {
                new Vector3(-12.5f, 0f, 132f),
                new Vector3(12.5f, 0f, 132f),
                new Vector3(-12.5f, 0f, 170f),
                new Vector3(12.5f, 0f, 170f)
            };
            for (int i = 0; i < parkTrees.Length; i++)
            {
                CreateTownTree($"Park_Tree_{i + 1}", parkRoot.transform, parkTrees[i], mats, 1.0f + (i % 3) * 0.14f, i + 20);
            }

            var pb1 = CreateBench("Park_Bench_1", parkRoot.transform, new Vector3(-6.5f, 0.02f, 135.5f), Quaternion.identity, mats);
            var pb2 = CreateBench("Park_Bench_2", parkRoot.transform, new Vector3(6.5f, 0.02f, 135.5f), Quaternion.identity, mats);
            benchSeatCollector.Add(pb1.leftSlot); benchSeatCollector.Add(pb1.rightSlot);
            benchSeatCollector.Add(pb2.leftSlot); benchSeatCollector.Add(pb2.rightSlot);
        }

        /// <summary>
        /// Builds the swimmable İstanbul Boğazı strait, 100% drivable Boğaziçi Asma Köprüsü (z=116)
        /// & Galata Köprüsü (z=44), walkable Kız Kulesi Island, Şehir Hatları Ferry Pier,
        /// and Anadolu Yakası (Üsküdar Sahil Bulvarı + 6 Enterable Kuzguncuk Yalıları + 4 Cars).
        /// </summary>
        private static void BuildIstanbulBosphorusAndAnatolia(
            Transform parent,
            MaterialSet mats,
            PrefabSet prefabs,
            System.Random rng,
            List<SeatInteractable> benchSeatCollector)
        {
            GameObject istRoot = new GameObject("Istanbul_BosphorusAndAnatolia");
            istRoot.transform.SetParent(parent, false);

            // =================================================================
            // 1. SWIMMABLE İSTANBUL BOĞAZI STRAIT (x = 78..148, water y = -0.20m, seabed y = -1.75m)
            // =================================================================
            CreateStaticBlock("Bosphorus_WaterSurface", istRoot.transform,
                new Vector3(113f, -0.22f, 80f), new Vector3(70f, 0.06f, 640f),
                mats.IstanbulBosphorusWater, addCollider: false);

            GameObject straitZoneGo = new GameObject("Bosphorus_WaterZone");
            straitZoneGo.transform.SetParent(istRoot.transform, false);
            straitZoneGo.transform.position = new Vector3(113f, -0.95f, 80f);
            BoxCollider swCol = straitZoneGo.AddComponent<BoxCollider>();
            swCol.isTrigger = true;
            swCol.size = new Vector3(69.5f, 2.2f, 360f);
            WaterZone straitWater = straitZoneGo.AddComponent<WaterZone>();
            straitWater.Configure(Vector3.zero, new Vector3(69.5f, 2.2f, 360f), surfaceY: -0.20f, floorY: -1.75f);

            // European & Anatolian stone seawalls + waterfront promenades
            CreateTiledBox("Seawall_Avrupa", istRoot.transform,
                new Vector3(77.4f, -0.88f, 80f), new Vector3(1.4f, 1.80f, 300f),
                Quaternion.identity, mats.TravertineStone, 2.5f, collider: true);
            CreateTiledBox("Seawall_Anadolu", istRoot.transform,
                new Vector3(148.6f, -0.88f, 80f), new Vector3(1.4f, 1.80f, 300f),
                Quaternion.identity, mats.TravertineStone, 2.5f, collider: true);

            // Wide stone swimming staircases descending into the Bosphorus on BOTH shores (so player can swim in & walk out!)
            float[] shoreStairZs = { 10f, 92f };
            for (int si = 0; si < shoreStairZs.Length; si++)
            {
                float sz = shoreStairZs[si];
                for (int step = 0; step < 5; step++)
                {
                    float topY = -0.15f - step * 0.28f;
                    float h = topY - (-1.75f);
                    // European side stepping east (+X) into water
                    CreateTiledBox($"BosphorusStair_EU_{si}_{step}", istRoot.transform,
                        new Vector3(78.25f + step * 0.45f, -1.75f + h * 0.5f, sz), new Vector3(0.46f, h, 4.8f),
                        Quaternion.identity, mats.TravertineStone, 1.5f, collider: true);
                    // Anatolian side stepping west (-X) into water
                    CreateTiledBox($"BosphorusStair_AS_{si}_{step}", istRoot.transform,
                        new Vector3(147.75f - step * 0.45f, -1.75f + h * 0.5f, sz), new Vector3(0.46f, h, 4.8f),
                        Quaternion.identity, mats.TravertineStone, 1.5f, collider: true);
                }
            }

            // =================================================================
            // 2. BOĞAZİÇİ ASMA KÖPRÜSÜ (100% DRIVABLE & WALKABLE at z = +116.0!)
            // =================================================================
            BuildBosphorusSuspensionBridge(istRoot.transform, mats, zCenter: 116.0f);

            // =================================================================
            // 3. GALATA KÖPRÜSÜ (Southern Low-Arch Coastal Bridge at z = +44.0!)
            // =================================================================
            BuildGalataCoastalBridge(istRoot.transform, mats, prefabs, zCenter: 44.0f);

            // =================================================================
            // 4. KIZ KULESİ ADASI (Walkable Islet in the Bosphorus at x=113, z=10)
            // =================================================================
            GameObject kizRoot = new GameObject("Istanbul_KizKulesiAdasi");
            kizRoot.transform.SetParent(istRoot.transform, false);
            CreateTiledBox("Kiz_StoneIslet", kizRoot.transform,
                new Vector3(113f, -0.58f, 10f), new Vector3(22f, 2.36f, 22f),
                Quaternion.identity, mats.TravertineStone, 2.2f, collider: true);

            // Stone landing steps on West & East sides of Kız Kulesi islet so swimmers can climb right onto the island!
            for (int step = 0; step < 6; step++)
            {
                float topY = 0.45f - step * 0.28f;
                float h = Mathf.Max(0.25f, topY - (-1.75f));
                CreateTiledBox($"KizStep_W_{step}", kizRoot.transform,
                    new Vector3(101.75f - step * 0.45f, -1.75f + h * 0.5f, 10f), new Vector3(0.46f, h, 4.2f),
                    Quaternion.identity, mats.TravertineStone, 1.5f, collider: true);
                CreateTiledBox($"KizStep_E_{step}", kizRoot.transform,
                    new Vector3(124.25f + step * 0.45f, -1.75f + h * 0.5f, 10f), new Vector3(0.46f, h, 4.2f),
                    Quaternion.identity, mats.TravertineStone, 1.5f, collider: true);
            }

            GameObject kizTower = new GameObject("Istanbul_KizKulesi");
            kizTower.transform.SetParent(kizRoot.transform, false);
            kizTower.transform.position = new Vector3(113f, 0.60f, 10f);
            GameObject kizMesh = InstantiateBlenderModel("Istanbul_KizKulesi.fbx", "KizKulesiMesh", kizTower.transform, mats);
            if (kizMesh != null)
            {
                BoxCollider kCol = kizTower.AddComponent<BoxCollider>();
                kCol.center = new Vector3(0f, 2.2f, 0f);
                kCol.size = new Vector3(12.0f, 4.4f, 12.0f);
                MarkStaticEnvironment(kizTower);
            }
            CreatePointLamp("Lamp_KizKulesi", kizRoot.transform, new Vector3(113f, 13.5f, 10f), new Color(1.0f, 0.84f, 0.52f), 28f, 3.4f);

            // =================================================================
            // 5. BEŞİKTAŞ / ORTAKÖY İSKELESİ + ŞEHİR HATLARI BOĞAZ VAPURU (x=85, z=80)
            // =================================================================
            CreateTiledBox("Besiktas_FerryPier", istRoot.transform,
                new Vector3(80.2f, 0.08f, 80f), new Vector3(5.2f, 0.34f, 15.0f),
                Quaternion.identity, mats.TownWood, 1.8f, collider: true);
            CreateStaticPart("PierSignPost", PrimitiveType.Cylinder, istRoot.transform, new Vector3(79.5f, 1.5f, 73.2f), Quaternion.identity, new Vector3(0.10f, 1.5f, 0.10f), mats.SedanTrimDark);
            CreateStaticPart("PierSignBoard", PrimitiveType.Cube, istRoot.transform, new Vector3(79.5f, 2.75f, 73.2f), Quaternion.identity, new Vector3(0.14f, 0.65f, 3.2f), mats.TownAwningNavy);
            CreateWorldSignText("BOĞAZ İSKELESİ", istRoot.transform, new Vector3(79.40f, 2.75f, 73.2f), Quaternion.Euler(0f, 90f, 0f), new Vector2(3.0f, 0.52f), Color.white);

            GameObject ferryObj = new GameObject("Istanbul_SehirHatlariVapuru");
            ferryObj.transform.SetParent(istRoot.transform, false);
            ferryObj.transform.SetPositionAndRotation(new Vector3(86.2f, -0.35f, 80f), Quaternion.identity);
            GameObject ferryMesh = InstantiateBlenderModel("Istanbul_Ferry.fbx", "BogazVapuruMesh", ferryObj.transform, mats);
            if (ferryMesh != null)
            {
                BoxCollider fCol = ferryObj.AddComponent<BoxCollider>();
                fCol.center = new Vector3(0f, 2.5f, 0f);
                fCol.size = new Vector3(5.2f, 5.0f, 21.5f);
                MarkStaticEnvironment(ferryObj);
            }

            // =================================================================
            // 6. ANADOLU YAKASI — ÜSKÜDAR SAHİL BULVARI & KUZGUNCUK YALILARI (x=148..215)
            // =================================================================
            GameObject anadoluRoot = new GameObject("Istanbul_AnadoluYakasi_Uskudar");
            anadoluRoot.transform.SetParent(istRoot.transform, false);

            // Wide stone Üsküdar waterfront promenade facing Kız Kulesi & Avrupa skyline
            CreateTiledBox("Uskudar_SahilPromenade", anadoluRoot.transform,
                new Vector3(153.5f, 0.014f, 80f), new Vector3(9.2f, 0.028f, 224f),
                Quaternion.identity, mats.TownPaving, 2.2f, collider: false);

            // Üsküdar Sahil Asphalt Boulevard (x = 164.0, connects Boğaziçi Bridge z=116 & Galata Bridge z=44!)
            CreateStaticBlock("Road_UskudarSahil", anadoluRoot.transform,
                new Vector3(164.0f, 0.012f, 80f), new Vector3(11.5f, 0.024f, 218f),
                mats.RoadAsphaltMain, addCollider: false);
            for (float z = -12f; z <= 172f; z += 12f)
            {
                if (Mathf.Abs(z - 44f) < 9f || Mathf.Abs(z - 116f) < 9f) continue;
                CreateStaticBlock($"Dash_Uskudar_{z:0}", anadoluRoot.transform,
                    new Vector3(164.0f, 0.028f, z), new Vector3(0.20f, 0.01f, 4.4f),
                    mats.RoadMarkingWhite, addCollider: false);
            }

            // Üsküdar Waterfront Benches, Trees, Street Lamps & Signboard
            float[] uskudarBenchZs = { 2f, 22f, 68f, 90f, 140f };
            for (int i = 0; i < uskudarBenchZs.Length; i++)
            {
                var ub = CreateBench($"Uskudar_Bench_{i + 1}", anadoluRoot.transform,
                    new Vector3(152.2f, 0.02f, uskudarBenchZs[i]), Quaternion.Euler(0f, -90f, 0f), mats);
                benchSeatCollector.Add(ub.leftSlot);
                benchSeatCollector.Add(ub.rightSlot);
                CreateTownTree($"Uskudar_Tree_{i + 1}", anadoluRoot.transform,
                    new Vector3(155.8f, 0f, uskudarBenchZs[i] + 9.0f), mats, 1.02f + (i % 2) * 0.14f, 70 + i);
                PlacePrefabInstance(prefabs.StreetLamp, anadoluRoot.transform,
                    new Vector3(157.5f, 0.02f, uskudarBenchZs[i] - 4.0f), Quaternion.Euler(0f, 90f, 0f));
            }

            CreateStaticPart("UskudarSign_Post", PrimitiveType.Cylinder, anadoluRoot.transform, new Vector3(156.5f, 1.5f, 34f), Quaternion.identity, new Vector3(0.11f, 1.5f, 0.11f), mats.SedanTrimDark);
            CreateStaticPart("UskudarSign_Board", PrimitiveType.Cube, anadoluRoot.transform, new Vector3(156.5f, 2.85f, 34f), Quaternion.identity, new Vector3(0.16f, 0.72f, 3.8f), mats.TownAwningTerracotta);
            CreateWorldSignText("ÜSKÜDAR SAHİL", anadoluRoot.transform, new Vector3(156.40f, 2.85f, 34f), Quaternion.Euler(0f, 90f, 0f), new Vector2(3.6f, 0.60f), Color.white);

            // 6 Enterable Kuzguncuk / Üsküdar Cumbalı Boğaz Yalıları overlooking the Bosphorus!
            Material[] yaliPalette = { mats.BuildingFacadeC, mats.PlasterTrim, mats.BuildingFacadeA, mats.TownBrickHouse, mats.BuildingFacadeB };
            float[] yaliZs = { -8.0f, 16.0f, 64.0f, 88.0f, 138.0f, 164.0f };
            for (int i = 0; i < yaliZs.Length; i++)
            {
                BuildEnterableResidentialHouse(
                    $"House_Anadolu_Yali_{i + 1:00}",
                    $"house_as_{i + 1:00}",
                    $"Kuzguncuk Yalı No: {i + 1}",
                    anadoluRoot.transform,
                    new Vector3(186.0f, 0f, yaliZs[i]),
                    Quaternion.Euler(0f, 180f, 0f),
                    depthX: 9.4f,
                    widthZ: 10.8f,
                    wallMat: yaliPalette[i % yaliPalette.Length],
                    hasChimney: i % 2 == 0,
                    hasGardenTree: true,
                    mats: mats,
                    houseSeed: 5100 + i * 53);
            }

            // 4 Extra Drivable Cars parked cleanly along Üsküdar Sahil Bulvarı
            (Vector3 pos, float yaw, GameObject prefab)[] anadoluCars =
            {
                (new Vector3(170.5f, 0.28f, 12.0f), 0f, prefabs.TownCarYellowSportsCoupe),
                (new Vector3(170.5f, 0.28f, 76.0f), 180f, prefabs.TownCarCharcoalSUV),
                (new Vector3(170.5f, 0.28f, 148.0f), 0f, prefabs.TownCarBlueHatchback),
                (new Vector3(170.5f, 0.28f, -12.0f), 0f, prefabs.TownCarWhitePickup)
            };
            for (int i = 0; i < anadoluCars.Length; i++)
            {
                if (anadoluCars[i].prefab == null) continue;
                GameObject car = PlacePrefabInstance(anadoluCars[i].prefab, anadoluRoot.transform,
                    anadoluCars[i].pos, Quaternion.Euler(0f, anadoluCars[i].yaw, 0f));
                car.name = $"Anadolu_DrivableCar_{i + 1:00}";
                InteractableTrigger trig = car.GetComponent<InteractableTrigger>();
                if (trig != null)
                {
                    trig.Configure(InteractableKind.TownVehicle, $"anadolu_car_{i + 1:00}", "ui.btn.drive", 3.8f);
                }
            }
        }

        private static void BuildBosphorusSuspensionBridge(Transform parent, MaterialSet mats, float zCenter)
        {
            GameObject bridge = new GameObject("Istanbul_BogaziciAsmaKoprusu");
            bridge.transform.SetParent(parent, false);

            const float deckY = 4.40f;
            const float deckW = 12.4f;
            const float deckThick = 0.60f;
            const float westStart = 74.5f;
            const float westTowerX = 96.0f;
            const float eastTowerX = 130.0f;
            const float eastEnd = 151.5f;

            // 1. Flat central suspended asphalt roadway deck (x = 96..130, top y = 4.40m)
            float spanLen = eastTowerX - westTowerX;
            CreateTiledBox("Bridge_CenterDeck", bridge.transform,
                new Vector3((westTowerX + eastTowerX) * 0.5f, deckY - deckThick * 0.5f, zCenter),
                new Vector3(spanLen + 0.4f, deckThick, deckW),
                Quaternion.identity, mats.RoadAsphaltCross, 3.0f, collider: true);

            // 2. Smooth drivable West approach ramp (x = 74.5 -> 96.0, y = 0.0 -> 4.40m, slope 11.5 deg)
            BuildSmoothBridgeRamp("Bridge_WestRamp", bridge.transform, westStart, 0.01f, westTowerX, deckY, zCenter, deckW, deckThick, mats.RoadAsphaltCross);
            // 3. Smooth drivable East descent ramp (x = 130.0 -> 151.5, y = 4.40m -> 0.0)
            BuildSmoothBridgeRamp("Bridge_EastRamp", bridge.transform, eastTowerX, deckY, eastEnd, 0.01f, zCenter, deckW, deckThick, mats.RoadAsphaltCross);

            // Short flat asphalt connectors joining x=72 & x=164 coastal boulevards seamlessly to bridge ends
            CreateStaticBlock("Bridge_WestJoin", bridge.transform, new Vector3(72.5f, 0.014f, zCenter), new Vector3(6.0f, 0.024f, deckW), mats.RoadAsphaltCross, addCollider: false);
            CreateStaticBlock("Bridge_EastJoin", bridge.transform, new Vector3(157.5f, 0.014f, zCenter), new Vector3(13.0f, 0.024f, deckW), mats.RoadAsphaltCross, addCollider: false);

            // Crash guardrails along both sides of central suspended deck
            for (int side = -1; side <= 1; side += 2)
            {
                float gz = zCenter + side * (deckW * 0.5f - 0.25f);
                CreateTiledBox($"Bridge_GuardRail_{side}", bridge.transform,
                    new Vector3((westTowerX + eastTowerX) * 0.5f, deckY + 0.52f, gz),
                    new Vector3(spanLen, 1.04f, 0.32f),
                    Quaternion.identity, mats.IstanbulBridgeSteel, 2.0f, collider: true);
            }

            // 4. Twin 34m Blender Steel H-Frame Suspension Pylons (cross-beam tucked cleanly right below deckY!)
            float[] towerXs = { westTowerX, eastTowerX };
            for (int t = 0; t < towerXs.Length; t++)
            {
                GameObject pylon = new GameObject($"Bosphorus_Pylon_{t + 1}");
                pylon.transform.SetParent(bridge.transform, false);
                pylon.transform.position = new Vector3(towerXs[t], -2.60f, zCenter);
                InstantiateBlenderModel("Istanbul_BosphorusBridgeTower.fbx", $"PylonMesh_{t + 1}", pylon.transform, mats);
                MarkStaticEnvironment(pylon);
            }

            // 5. Parabolic Steel Main Suspension Cables + Vertical Suspender Hangers on both sides (z = zCenter +/- 8.4)
            const float towerTopY = 31.8f; // 34.4 - 2.60
            const float midSagY = 7.2f;
            for (int side = -1; side <= 1; side += 2)
            {
                float cz = zCenter + side * 8.4f;
                // Center span parabolic main cable + vertical suspender ropes
                const int segs = 12;
                Vector3 prev = Vector3.zero;
                for (int i = 0; i <= segs; i++)
                {
                    float u = (float)i / segs;
                    float x = Mathf.Lerp(westTowerX, eastTowerX, u);
                    float d = (u - 0.5f) * 2f; // -1..+1
                    float y = Mathf.Lerp(midSagY, towerTopY, d * d);
                    Vector3 cur = new Vector3(x, y, cz);
                    if (i > 0)
                    {
                        CreateCableSegment($"Cable_Mid_{side}_{i}", bridge.transform, prev, cur, 0.24f, mats.IstanbulBridgeSteel);
                    }
                    if (i > 0 && i < segs)
                    {
                        CreateCableSegment($"Hanger_{side}_{i}", bridge.transform, new Vector3(x, deckY, zCenter + side * (deckW * 0.5f - 0.2f)), cur, 0.075f, mats.SedanChrome);
                    }
                    prev = cur;
                }
                // Backstay cables sloping from tower tops down to shore anchorages
                CreateCableSegment($"Cable_BackW_{side}", bridge.transform, new Vector3(westStart, 0.4f, cz), new Vector3(westTowerX, towerTopY, cz), 0.24f, mats.IstanbulBridgeSteel);
                CreateCableSegment($"Cable_BackE_{side}", bridge.transform, new Vector3(eastTowerX, towerTopY, cz), new Vector3(eastEnd, 0.4f, cz), 0.24f, mats.IstanbulBridgeSteel);
            }

            // Overhead Highway Signboard ("BOĞAZİÇİ KÖPRÜSÜ")
            CreateStaticBlock("Bridge_SignBeam", bridge.transform, new Vector3(westTowerX - 1.2f, deckY + 5.2f, zCenter), new Vector3(0.24f, 0.95f, 8.2f), mats.TownAwningNavy, addCollider: false);
            CreateWorldSignText("BOĞAZİÇİ KÖPRÜSÜ", bridge.transform, new Vector3(westTowerX - 1.36f, deckY + 5.2f, zCenter), Quaternion.Euler(0f, 90f, 0f), new Vector2(7.6f, 0.78f), Color.white);
            CreatePointLamp("Lamp_BridgeCenter", bridge.transform, new Vector3(113f, deckY + 8.5f, zCenter), new Color(1.0f, 0.85f, 0.55f), 36f, 3.4f);
        }

        private static void BuildSmoothBridgeRamp(
            string name,
            Transform parent,
            float x0,
            float y0,
            float x1,
            float y1,
            float zCenter,
            float widthZ,
            float thickness,
            Material mat)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            float angleDeg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            Quaternion rot = Quaternion.Euler(0f, 0f, angleDeg);
            Vector3 midTop = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, zCenter);
            Vector3 center = midTop - (rot * Vector3.up) * (thickness * 0.5f);

            CreateTiledBox(name, parent, center, new Vector3(len + 0.16f, thickness, widthZ), rot, mat, 3.0f, collider: true);
        }

        private static void CreateCableSegment(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material mat)
        {
            Vector3 delta = b - a;
            float len = delta.magnitude;
            if (len < 0.05f) return;
            GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = name;
            cyl.transform.SetParent(parent, false);
            cyl.transform.position = (a + b) * 0.5f;
            cyl.transform.up = delta / len;
            cyl.transform.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
            UnityEngine.Object.DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.GetComponent<Renderer>().sharedMaterial = mat;
            MarkStaticEnvironment(cyl);
        }

        private static void BuildGalataCoastalBridge(Transform parent, MaterialSet mats, PrefabSet prefabs, float zCenter)
        {
            GameObject galataBr = new GameObject("Istanbul_GalataKoprusu");
            galataBr.transform.SetParent(parent, false);

            const float deckY = 0.52f;
            const float deckW = 12.6f;
            const float deckThick = 0.50f;

            // Central flat coastal bridge deck (x = 78..148) + ultra-gentle shore ramps (72..78 & 148..154)
            CreateTiledBox("GalataBridge_Deck", galataBr.transform,
                new Vector3(113f, deckY - deckThick * 0.5f, zCenter),
                new Vector3(70.4f, deckThick, deckW),
                Quaternion.identity, mats.RoadAsphaltCross, 3.0f, collider: true);
            BuildSmoothBridgeRamp("GalataBridge_RampW", galataBr.transform, 72.5f, 0.01f, 78.2f, deckY, zCenter, deckW, deckThick, mats.RoadAsphaltCross);
            BuildSmoothBridgeRamp("GalataBridge_RampE", galataBr.transform, 147.8f, deckY, 153.5f, 0.01f, zCenter, deckW, deckThick, mats.RoadAsphaltCross);
            CreateStaticBlock("GalataBridge_EastJoin", galataBr.transform, new Vector3(158.0f, 0.014f, zCenter), new Vector3(12.0f, 0.024f, deckW), mats.RoadAsphaltCross, addCollider: false);

            // Stone piers standing in the Bosphorus + cast-iron promenade railings + vintage street lamps
            float[] pierXs = { 88f, 104f, 122f, 138f };
            for (int i = 0; i < pierXs.Length; i++)
            {
                CreateTiledBox($"GalataBridge_Pier_{i}", galataBr.transform,
                    new Vector3(pierXs[i], -0.95f, zCenter), new Vector3(2.4f, 2.2f, deckW + 0.6f),
                    Quaternion.identity, mats.TravertineStone, 2.0f, collider: true);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float rz = zCenter + side * (deckW * 0.5f - 0.25f);
                CreateTiledBox($"GalataBridge_Rail_{side}", galataBr.transform,
                    new Vector3(113f, deckY + 0.48f, rz), new Vector3(70.0f, 0.96f, 0.26f),
                    Quaternion.identity, mats.SedanTrimDark, 2.0f, collider: true);

                for (float lx = 86f; lx <= 140f; lx += 18f)
                {
                    PlacePrefabInstance(prefabs.StreetLamp, galataBr.transform,
                        new Vector3(lx, deckY, rz - side * 0.35f), Quaternion.Euler(0f, side > 0 ? 180f : 0f, 0f));
                }
            }
        }

        private static void BuildTownSwimmingPool(
            Transform parent,
            MaterialSet mats,
            List<SeatInteractable> benchSeatCollector)
        {
            GameObject poolRoot = new GameObject("Town_SwimmingPoolClub");
            poolRoot.transform.SetParent(parent, false);

            const float cx = 48.0f;
            const float cz = 10.0f;
            const float deckH = 1.60f;
            const float waterY = 1.48f;
            const float floorTopY = 0.06f;

            // Ground apron paving around the pool terrace
            CreateTiledBox("Pool_GroundApron", poolRoot.transform,
                new Vector3(cx, 0.012f, cz), new Vector3(30.0f, 0.024f, 36.0f),
                Quaternion.identity, mats.TownPaving, 2.5f, collider: false);

            // 4 Solid raised stone deck blocks enclosing a real open 10m x 16m recessed basin (zero ground holes!)
            // Outer: x = 37..59 (22m), z = -4..24 (28m). Inner basin: x = 43..53 (10m), z = 2..18 (16m).
            CreateTiledBox("PoolDeck_West", poolRoot.transform,
                new Vector3(40.0f, deckH * 0.5f, cz), new Vector3(6.0f, deckH, 28.0f),
                Quaternion.identity, mats.TownPaving, 2.2f, collider: true);
            CreateTiledBox("PoolDeck_East", poolRoot.transform,
                new Vector3(56.0f, deckH * 0.5f, cz), new Vector3(6.0f, deckH, 28.0f),
                Quaternion.identity, mats.TownPaving, 2.2f, collider: true);
            CreateTiledBox("PoolDeck_South", poolRoot.transform,
                new Vector3(cx, deckH * 0.5f, -1.0f), new Vector3(10.0f, deckH, 6.0f),
                Quaternion.identity, mats.TownPaving, 2.2f, collider: true);
            CreateTiledBox("PoolDeck_North", poolRoot.transform,
                new Vector3(cx, deckH * 0.5f, 21.0f), new Vector3(10.0f, deckH, 6.0f),
                Quaternion.identity, mats.TownPaving, 2.2f, collider: true);

            // Clean white stone coping lip framing the water edge
            CreateTiledBox("PoolCoping_W", poolRoot.transform, new Vector3(42.82f, deckH + 0.02f, cz), new Vector3(0.38f, 0.04f, 16.76f), Quaternion.identity, mats.PlasterTrim, 2.0f, collider: false);
            CreateTiledBox("PoolCoping_E", poolRoot.transform, new Vector3(53.18f, deckH + 0.02f, cz), new Vector3(0.38f, 0.04f, 16.76f), Quaternion.identity, mats.PlasterTrim, 2.0f, collider: false);
            CreateTiledBox("PoolCoping_S", poolRoot.transform, new Vector3(cx, deckH + 0.02f, 1.82f), new Vector3(10.0f, 0.04f, 0.38f), Quaternion.identity, mats.PlasterTrim, 2.0f, collider: false);
            CreateTiledBox("PoolCoping_N", poolRoot.transform, new Vector3(cx, deckH + 0.02f, 18.18f), new Vector3(10.0f, 0.04f, 0.38f), Quaternion.identity, mats.PlasterTrim, 2.0f, collider: false);

            // Bright ceramic tiled pool floor + 4 decorative inner blue wall liners + swimming lane lines
            CreateTiledBox("PoolBasin_Floor", poolRoot.transform,
                new Vector3(cx, floorTopY * 0.5f, cz), new Vector3(10.04f, floorTopY, 16.04f),
                Quaternion.identity, mats.PoolTileBlue, 1.8f, collider: true);
            CreateTiledBox("PoolBasin_InnerWall_W", poolRoot.transform, new Vector3(43.04f, deckH * 0.5f, cz), new Vector3(0.08f, deckH, 16.0f), Quaternion.identity, mats.PoolTileBlue, 1.5f, collider: false);
            CreateTiledBox("PoolBasin_InnerWall_E", poolRoot.transform, new Vector3(52.96f, deckH * 0.5f, cz), new Vector3(0.08f, deckH, 16.0f), Quaternion.identity, mats.PoolTileBlue, 1.5f, collider: false);
            CreateTiledBox("PoolBasin_InnerWall_S", poolRoot.transform, new Vector3(cx, deckH * 0.5f, 2.04f), new Vector3(10.0f, deckH, 0.08f), Quaternion.identity, mats.PoolTileBlue, 1.5f, collider: false);
            CreateTiledBox("PoolBasin_InnerWall_N", poolRoot.transform, new Vector3(cx, deckH * 0.5f, 17.96f), new Vector3(10.0f, deckH, 0.08f), Quaternion.identity, mats.PoolTileBlue, 1.5f, collider: false);

            // Lane guide stripes on pool bottom
            for (int lane = -1; lane <= 1; lane++)
            {
                CreateStaticBlock($"PoolLaneStripe_{lane + 1}", poolRoot.transform,
                    new Vector3(cx + lane * 2.8f, floorTopY + 0.008f, cz), new Vector3(0.16f, 0.012f, 12.8f),
                    mats.SedanTrimDark, addCollider: false);
            }

            // Submerged walk-in corner steps (SW corner, x = 43.1..46.0, z = 2.1..4.6) so player can walk in & out smoothly
            for (int s = 0; s < 5; s++)
            {
                float stepTopY = deckH - (s + 1) * 0.28f;
                float stepZ = 2.08f + s * 0.44f + 0.22f;
                CreateTiledBox($"PoolSubmergedStep_{s + 1}", poolRoot.transform,
                    new Vector3(44.55f, stepTopY * 0.5f, stepZ), new Vector3(2.90f, stepTopY, 0.44f),
                    Quaternion.identity, mats.PoolTileBlue, 1.2f, collider: true);
            }

            // Translucent turquoise water surface + WaterZone swimming trigger
            CreateStaticBlock("Pool_WaterSurfaceVisual", poolRoot.transform,
                new Vector3(cx, waterY - 0.03f, cz), new Vector3(9.94f, 0.06f, 15.94f),
                mats.PoolWaterClear, addCollider: false);

            GameObject waterZoneGo = new GameObject("Pool_WaterZone");
            waterZoneGo.transform.SetParent(poolRoot.transform, false);
            waterZoneGo.transform.position = new Vector3(cx, 0.85f, cz);
            BoxCollider wzCol = waterZoneGo.AddComponent<BoxCollider>();
            wzCol.isTrigger = true;
            wzCol.size = new Vector3(9.92f, 1.72f, 15.92f);
            WaterZone waterZone = waterZoneGo.AddComponent<WaterZone>();
            waterZone.Configure(Vector3.zero, new Vector3(9.92f, 1.72f, 15.92f), surfaceY: waterY, floorY: floorTopY);

            // Wide walkable exterior staircases on West (facing Cumhuriyet Caddesi) and South (facing Güney Yolu)
            const int extSteps = 6;
            const float stepDepth = 0.45f;
            float stepRise = deckH / extSteps;
            for (int i = 0; i < extSteps; i++)
            {
                float h = stepRise * (i + 1);
                // West stairs (ascending +X toward x = 37.0)
                float wx = 37.0f - (extSteps - i - 0.5f) * stepDepth;
                CreateTiledBox($"PoolStairs_W_{i + 1}", poolRoot.transform,
                    new Vector3(wx, h * 0.5f, cz), new Vector3(stepDepth, h, 6.2f),
                    Quaternion.identity, mats.Sidewalk, 1.5f, collider: true);

                // South stairs (ascending +Z toward z = -4.0)
                float sz = -4.0f - (extSteps - i - 0.5f) * stepDepth;
                CreateTiledBox($"PoolStairs_S_{i + 1}", poolRoot.transform,
                    new Vector3(cx, h * 0.5f, sz), new Vector3(6.2f, h, stepDepth),
                    Quaternion.identity, mats.Sidewalk, 1.5f, collider: true);
            }

            // Diving board on North deck extending out over the water
            CreateTiledBox("DivingBoard_Base", poolRoot.transform,
                new Vector3(cx, deckH + 0.16f, 19.45f), new Vector3(0.74f, 0.32f, 1.35f),
                Quaternion.identity, mats.Concrete, 1.5f, collider: true);
            CreateTiledBox("DivingBoard_Plank", poolRoot.transform,
                new Vector3(cx, deckH + 0.35f, 18.10f), new Vector3(0.60f, 0.08f, 3.20f),
                Quaternion.identity, mats.PlasterTrim, 1.5f, collider: true);

            // Chrome pool ladders on East & West rims
            Vector3[] ladderSpots = { new Vector3(53.05f, deckH, 14.2f), new Vector3(42.95f, deckH, 14.2f) };
            for (int i = 0; i < ladderSpots.Length; i++)
            {
                float dir = i == 0 ? 1f : -1f;
                Vector3 lp = ladderSpots[i];
                CreateStaticPart($"PoolLadder_RailA_{i}", PrimitiveType.Cylinder, poolRoot.transform, lp + new Vector3(dir * 0.15f, 0.32f, -0.26f), Quaternion.identity, new Vector3(0.05f, 0.65f, 0.05f), mats.SedanChrome);
                CreateStaticPart($"PoolLadder_RailB_{i}", PrimitiveType.Cylinder, poolRoot.transform, lp + new Vector3(dir * 0.15f, 0.32f, 0.26f), Quaternion.identity, new Vector3(0.05f, 0.65f, 0.05f), mats.SedanChrome);
                CreateStaticPart($"PoolLadder_TopA_{i}", PrimitiveType.Cube, poolRoot.transform, lp + new Vector3(0f, 0.64f, -0.26f), Quaternion.identity, new Vector3(0.36f, 0.05f, 0.05f), mats.SedanChrome);
                CreateStaticPart($"PoolLadder_TopB_{i}", PrimitiveType.Cube, poolRoot.transform, lp + new Vector3(0f, 0.64f, 0.26f), Quaternion.identity, new Vector3(0.36f, 0.05f, 0.05f), mats.SedanChrome);
            }

            // 4 Sittable Sun Loungers + 2 Striped Parasols on East & West decks
            (Vector3 pos, float yaw, Material mat)[] loungers =
            {
                (new Vector3(56.2f, deckH, 5.2f), -90f, mats.TownFabricTeal),
                (new Vector3(56.2f, deckH, 8.2f), -90f, mats.TownFabricTeal),
                (new Vector3(56.2f, deckH, 13.0f), -90f, mats.TownFabricMustard),
                (new Vector3(56.2f, deckH, 16.0f), -90f, mats.TownFabricMustard)
            };
            for (int i = 0; i < loungers.Length; i++)
            {
                GameObject lg = new GameObject($"Pool_SunLounger_{i + 1}");
                lg.transform.SetParent(poolRoot.transform, false);
                lg.transform.SetPositionAndRotation(loungers[i].pos, Quaternion.Euler(0f, loungers[i].yaw, 0f));

                CreateStaticPart("Frame", PrimitiveType.Cube, lg.transform, new Vector3(0f, 0.22f, 0f), Quaternion.identity, new Vector3(0.76f, 0.08f, 1.92f), mats.PlasterTrim);
                CreateStaticPart("Pad", PrimitiveType.Cube, lg.transform, new Vector3(0f, 0.29f, 0.14f), Quaternion.identity, new Vector3(0.70f, 0.07f, 1.38f), loungers[i].mat);
                CreateStaticPart("Backrest", PrimitiveType.Cube, lg.transform, new Vector3(0f, 0.48f, -0.68f), Quaternion.Euler(28f, 0f, 0f), new Vector3(0.70f, 0.08f, 0.64f), loungers[i].mat);
                BoxCollider lCol = lg.AddComponent<BoxCollider>();
                lCol.center = new Vector3(0f, 0.30f, 0f);
                lCol.size = new Vector3(0.78f, 0.60f, 1.94f);
                MarkStaticEnvironment(lg);

                SeatInteractable seat = CreateSeatSlot($"LoungerSeat_{i + 1}", $"pool_lounger_{i + 1}", lg.transform,
                    new Vector3(0f, 0.35f, 0.05f), Quaternion.identity, 1.55f);
                benchSeatCollector.Add(seat);
            }

            // 2 Poolside Parasol Umbrellas
            Vector3[] umbrellaPos = { new Vector3(56.4f, deckH, 6.7f), new Vector3(56.4f, deckH, 14.5f) };
            Material[] umbrellaMats = { mats.TownAwningTerracotta, mats.TownAwningNavy };
            for (int i = 0; i < umbrellaPos.Length; i++)
            {
                CreateStaticPart($"Parasol_Pole_{i}", PrimitiveType.Cylinder, poolRoot.transform, umbrellaPos[i] + Vector3.up * 1.32f, Quaternion.identity, new Vector3(0.06f, 1.32f, 0.06f), mats.SedanTrimDark);
                CreateStaticPart($"Parasol_Canopy_{i}", PrimitiveType.Cylinder, poolRoot.transform, umbrellaPos[i] + Vector3.up * 2.52f, Quaternion.identity, new Vector3(2.45f, 0.10f, 2.45f), umbrellaMats[i]);
            }

            // Pool Club Signboard facing Cumhuriyet Caddesi
            CreateStaticPart("PoolSign_PostL", PrimitiveType.Cylinder, poolRoot.transform, new Vector3(36.8f, 1.4f, 14.0f), Quaternion.identity, new Vector3(0.10f, 1.4f, 0.10f), mats.SedanTrimDark);
            CreateStaticPart("PoolSign_PostR", PrimitiveType.Cylinder, poolRoot.transform, new Vector3(36.8f, 1.4f, 17.6f), Quaternion.identity, new Vector3(0.10f, 1.4f, 0.10f), mats.SedanTrimDark);
            CreateStaticPart("PoolSign_Board", PrimitiveType.Cube, poolRoot.transform, new Vector3(36.8f, 2.55f, 15.8f), Quaternion.identity, new Vector3(0.14f, 0.78f, 3.85f), mats.TownFabricTeal);
            CreateWorldSignText("YÜZME HAVUZU", poolRoot.transform, new Vector3(36.70f, 2.55f, 15.8f), Quaternion.Euler(0f, 90f, 0f), new Vector2(3.6f, 0.65f), new Color(0.98f, 0.98f, 0.96f, 1f));

            // Lush perimeter trees & night lighting
            CreateTownTree("Pool_Tree_SW", poolRoot.transform, new Vector3(34.5f, 0f, -6.5f), mats, 1.05f, 41);
            CreateTownTree("Pool_Tree_SE", poolRoot.transform, new Vector3(61.5f, 0f, -6.5f), mats, 1.12f, 42);
            CreateTownTree("Pool_Tree_NW", poolRoot.transform, new Vector3(34.5f, 0f, 26.5f), mats, 1.02f, 43);
            CreateTownTree("Pool_Tree_NE", poolRoot.transform, new Vector3(61.5f, 0f, 26.5f), mats, 1.08f, 44);
            CreatePointLamp("Lamp_Pool_N", poolRoot.transform, new Vector3(cx, 5.2f, 20.5f), new Color(0.82f, 0.96f, 1.0f), 22f, 2.8f);
            CreatePointLamp("Lamp_Pool_S", poolRoot.transform, new Vector3(cx, 5.2f, -0.5f), new Color(0.82f, 0.96f, 1.0f), 22f, 2.8f);
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

            // Crosswalks & Traffic Lights at Cumhuriyet Caddesi (x = +24) intersections (z = 44 & z = 116)
            float[] crossZs = { 44f, 116f };
            foreach (float cz in crossZs)
            {
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Main_{cz}_S", new Vector3(24f, 0.028f, cz - 7.0f), 9.5f, horizontalX: true, mats.RoadMarkingWhite);
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Main_{cz}_N", new Vector3(24f, 0.028f, cz + 7.0f), 9.5f, horizontalX: true, mats.RoadMarkingWhite);
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_West_{cz}_S", new Vector3(-24f, 0.028f, cz - 7.0f), 9.5f, horizontalX: true, mats.RoadMarkingWhite);
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_West_{cz}_N", new Vector3(-24f, 0.028f, cz + 7.0f), 9.5f, horizontalX: true, mats.RoadMarkingWhite);

                CreateStaticBlock($"StopLine_Main_{cz}_NB", furnRoot.transform,
                    new Vector3(26.6f, 0.03f, cz - 9.2f), new Vector3(4.6f, 0.01f, 0.40f), mats.RoadMarkingWhite, addCollider: false);
                CreateStaticBlock($"StopLine_Main_{cz}_SB", furnRoot.transform,
                    new Vector3(21.4f, 0.03f, cz + 9.2f), new Vector3(4.6f, 0.01f, 0.40f), mats.RoadMarkingWhite, addCollider: false);
            }

            TrafficLightCycler southSW = CreateTrafficLightPole("TrafficLight_44_SW", furnRoot.transform, new Vector3(17.5f, 0.02f, 37.5f), Quaternion.identity, mats, phaseOffset: 0f);
            TrafficLightCycler southNE = CreateTrafficLightPole("TrafficLight_44_NE", furnRoot.transform, new Vector3(30.5f, 0.02f, 50.5f), Quaternion.Euler(0f, 180f, 0f), mats, phaseOffset: 0f);
            TrafficLightCycler northSW = CreateTrafficLightPole("TrafficLight_116_SW", furnRoot.transform, new Vector3(17.5f, 0.02f, 109.5f), Quaternion.identity, mats, phaseOffset: 8f);
            TrafficLightCycler northNE = CreateTrafficLightPole("TrafficLight_116_NE", furnRoot.transform, new Vector3(30.5f, 0.02f, 122.5f), Quaternion.Euler(0f, 180f, 0f), mats, phaseOffset: 8f);

            List<TrafficStopLine> stopLines = new List<TrafficStopLine>
            {
                new TrafficStopLine { stopPosition = new Vector3(26.6f, 0.05f, 34.8f), approachDirection = Vector3.forward, trafficLight = southSW },
                new TrafficStopLine { stopPosition = new Vector3(21.4f, 0.05f, 53.2f), approachDirection = Vector3.back, trafficLight = southNE },
                new TrafficStopLine { stopPosition = new Vector3(26.6f, 0.05f, 106.8f), approachDirection = Vector3.forward, trafficLight = northSW },
                new TrafficStopLine { stopPosition = new Vector3(21.4f, 0.05f, 125.2f), approachDirection = Vector3.back, trafficLight = northNE }
            };

            // Street Props: Fire Hydrants & Trash Cans placed neatly off the road & off sidewalks
            Vector3[] hydrants =
            {
                new Vector3(17.2f, 0.02f, -14f),
                new Vector3(17.2f, 0.02f, 58f),
                new Vector3(-17.2f, 0.02f, 58f),
                new Vector3(-30.8f, 0.02f, 2f)
            };
            for (int i = 0; i < hydrants.Length; i++)
            {
                PlacePrefabInstance(prefabs.FireHydrant, furnRoot.transform, hydrants[i], Quaternion.identity);
            }

            Vector3[] trashCans =
            {
                new Vector3(15.2f, 0.02f, 2.0f),
                new Vector3(15.2f, 0.02f, 19.5f),
                new Vector3(-15.2f, 0.02f, 96.0f),
                new Vector3(-30.8f, 0.02f, 88.0f)
            };
            for (int i = 0; i < trashCans.Length; i++)
            {
                PlacePrefabInstance(prefabs.TrashCan, furnRoot.transform, trashCans[i], Quaternion.Euler(0f, i * 45f, 0f));
            }

            // 12 Diverse Drivable Town Cars across all 6 distinct Blender 3D vehicle models (Sports Coupe, SUV, Delivery Van, Pickup, Hatchback, Sedan)
            // Parked cleanly in shoulder bays off active driving lanes — EVERY single car is unlocked & drivable!
            (Vector3 pos, float yaw, int fixedVariant)[] parkedCars =
            {
                (new Vector3(19.6f, 0.28f, -12.0f), 0f, 0),    // Red Hatchback near Pizzeria
                (new Vector3(19.6f, 0.28f, 24.0f), 0f, 7),     // White Delivery Van near Market
                (new Vector3(19.6f, 0.28f, 64.0f), 0f, 3),     // Yellow Sports Coupe at Town Square!
                (new Vector3(19.6f, 0.28f, 102.0f), 0f, 2),    // Green Sedan near Taxi Rank
                (new Vector3(32.2f, 0.28f, 1.5f), 0f, 5),      // Charcoal 4x4 SUV at Swimming Pool Club!
                (new Vector3(15.5f, 0.28f, -32.5f), 0f, 4),    // Blue Sports Coupe next to Tamirhane!
                (new Vector3(-28.4f, 0.28f, 0.0f), 180f, 1),   // White Pickup on Çınar Caddesi
                (new Vector3(-28.4f, 0.28f, 70.0f), 180f, 8),  // Blue Hatchback in West Residential
                (new Vector3(-28.4f, 0.28f, 140.0f), 180f, 6), // Forest Green 4x4 SUV near Çınarlı Park
                (new Vector3(28.4f, 0.28f, 70.0f), 0f, 1),     // White Pickup in East Residential
                (new Vector3(28.4f, 0.28f, 162.0f), 0f, 7),    // White Delivery Van near North Cargo Depot
                (new Vector3(-67.6f, 0.28f, 80.0f), 0f, 3)     // Yellow Sports Coupe on West Ring Street
            };

            GameObject[] drivableVariants =
            {
                prefabs.TownCarRedHatchback,       // 0
                prefabs.TownCarWhitePickup,        // 1
                prefabs.TownCarGreenSedan,         // 2
                prefabs.TownCarYellowSportsCoupe,  // 3
                prefabs.TownCarBlueSportsCoupe,    // 4
                prefabs.TownCarCharcoalSUV,        // 5
                prefabs.TownCarGreenSUV,           // 6
                prefabs.TownCarWhiteVan,           // 7
                prefabs.TownCarBlueHatchback       // 8
            };

            for (int i = 0; i < parkedCars.Length; i++)
            {
                int vIdx = parkedCars[i].fixedVariant % drivableVariants.Length;
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

            return stopLines;
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
                    ? new Vector3(0.48f, 0.01f, 2.6f)
                    : new Vector3(2.6f, 0.01f, 0.48f);

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

            GameObject blenderPole = InstantiateBlenderModel("Traffic_Light_Pole.fbx", "TrafficLightPoleMesh", root.transform, mats);
            if (blenderPole != null)
            {
                CapsuleCollider poleCol = root.AddComponent<CapsuleCollider>();
                poleCol.center = new Vector3(0f, 2.7f, 0f);
                poleCol.radius = 0.16f;
                poleCol.height = 5.4f;
                MarkStaticEnvironment(blenderPole);
            }
            else
            {
                GameObject pole = CreatePrimitiveChild("Pole", PrimitiveType.Cylinder, root.transform,
                    new Vector3(0f, 2.7f, 0f), new Vector3(0.16f, 2.7f, 0.16f), mats.SedanTrimDark, removeCollider: false);
                MarkStaticEnvironment(pole);
                GameObject arm = CreatePrimitiveChild("Arm", PrimitiveType.Cube, root.transform,
                    new Vector3(0.55f, 5.15f, 0f), new Vector3(1.2f, 0.12f, 0.12f), mats.SedanTrimDark, removeCollider: true);
                MarkStaticEnvironment(arm);
                GameObject head = CreatePrimitiveChild("SignalHead", PrimitiveType.Cube, root.transform,
                    new Vector3(1.05f, 4.75f, 0f), new Vector3(0.36f, 1.02f, 0.36f), mats.SedanTrimDark, removeCollider: true);
                MarkStaticEnvironment(head);
            }

            GameObject redBulb = CreatePrimitiveChild("Bulb_Red", PrimitiveType.Sphere, root.transform,
                new Vector3(1.05f, 5.05f, -0.15f), new Vector3(0.21f, 0.21f, 0.12f), mats.TrafficLightBulb, removeCollider: true);
            GameObject amberBulb = CreatePrimitiveChild("Bulb_Amber", PrimitiveType.Sphere, root.transform,
                new Vector3(1.05f, 4.75f, -0.15f), new Vector3(0.21f, 0.21f, 0.12f), mats.TrafficLightBulb, removeCollider: true);
            GameObject greenBulb = CreatePrimitiveChild("Bulb_Green", PrimitiveType.Sphere, root.transform,
                new Vector3(1.05f, 4.45f, -0.15f), new Vector3(0.21f, 0.21f, 0.12f), mats.TrafficLightBulb, removeCollider: true);

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

            // Always use our upright sculpted Blender Park Bench (Teak slats at Y=0.45m + wrought-iron frame — zero floating NPCs!)
            GameObject benchMesh = InstantiateBlenderModel("Town_ParkBench.fbx", "ParkBenchMesh", bench.transform, mats);
            if (benchMesh == null)
            {
                CreatePrimitiveChild("Seat", PrimitiveType.Cube, bench.transform,
                    new Vector3(0f, 0.45f, 0f), new Vector3(1.85f, 0.09f, 0.52f), mats.TownWood, removeCollider: true);
                GameObject back = CreatePrimitiveChild("Backrest", PrimitiveType.Cube, bench.transform,
                    new Vector3(0f, 0.78f, -0.22f), new Vector3(1.85f, 0.38f, 0.08f), mats.TownWood, removeCollider: true);
                back.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            }

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

                BlenderCharacterVariant seatVar = (i % 2 == 0) ? BlenderCharacterVariant.CasualFemale : BlenderCharacterVariant.CasualMale;
                ProceduralHumanoid hum = BuildBlenderHumanoid(
                    npcObj.transform,
                    mats,
                    seatVar,
                    shirts[i % shirts.Length],
                    i % 2 == 0 ? mats.CharPantsDenim : mats.CharPantsKhaki,
                    i % 2 == 0 ? mats.CharSkin : mats.CharSkinMedium,
                    i % 2 == 0 ? mats.CharHairBlonde : mats.CharHair,
                    seatVar == BlenderCharacterVariant.CasualFemale ? 1.70f : 1.76f);
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
            tree.transform.localRotation = Quaternion.Euler(0f, (variantSeed * 67f) % 360f, 0f);
            tree.transform.localScale = Vector3.one * scale;

            Material folA = (variantSeed % 2 == 0) ? mats.TownFoliageA : mats.TownFoliageB;
            Material folB = (variantSeed % 2 == 0) ? mats.TownFoliageB : mats.TownFoliageA;

            GameObject blenderTree = InstantiateBlenderModel(
                "Tree_PlaneTree.fbx",
                "BlenderPlaneTreeMesh",
                tree.transform,
                mats,
                foliageAOverride: folA,
                foliageBOverride: folB);

            if (blenderTree == null)
            {
                if (_cachedTreeTrunkMesh == null) _cachedTreeTrunkMesh = BuildOrganicTreeTrunkMesh();
                if (_cachedTreeCanopyMeshA == null) _cachedTreeCanopyMeshA = BuildOrganicTreeCanopyMesh(seed: 101, horizontalRadius: 1.55f, verticalRadius: 1.28f);

                GameObject trunkObj = new GameObject("OrganicTrunk");
                trunkObj.transform.SetParent(tree.transform, false);
                trunkObj.AddComponent<MeshFilter>().sharedMesh = _cachedTreeTrunkMesh;
                trunkObj.AddComponent<MeshRenderer>().sharedMaterial = mats.TownBark;

                GameObject canopyObj = new GameObject("FoliageCluster_Fallback");
                canopyObj.transform.SetParent(tree.transform, false);
                canopyObj.transform.localPosition = new Vector3(0f, 3.85f, 0f);
                canopyObj.AddComponent<MeshFilter>().sharedMesh = _cachedTreeCanopyMeshA;
                canopyObj.AddComponent<MeshRenderer>().sharedMaterial = folA;
            }

            CapsuleCollider trunkCol = tree.AddComponent<CapsuleCollider>();
            trunkCol.center = new Vector3(0f, 1.55f, 0f);
            trunkCol.radius = 0.28f;
            trunkCol.height = 3.1f;

            MarkStaticEnvironment(tree);
        }

        private static Mesh BuildOrganicTreeTrunkMesh()
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            void AddTaperedLimb(Vector3 p0, Vector3 p1, Vector3 p2, float r0, float r1, float r2, int sides)
            {
                int baseIdx = verts.Count;
                Vector3[] centers = { p0, p1, p2 };
                float[] radii = { r0, r1, r2 };
                float[] vCoords = { 0f, 0.55f, 1.1f };

                for (int ring = 0; ring < 3; ring++)
                {
                    Vector3 tangent = ring == 0 ? (p1 - p0).normalized : (p2 - p0).normalized;
                    Vector3 right = Vector3.Cross(Vector3.forward, tangent);
                    if (right.sqrMagnitude < 0.01f) right = Vector3.right;
                    right.Normalize();
                    Vector3 fwd = Vector3.Cross(tangent, right).normalized;

                    for (int s = 0; s <= sides; s++)
                    {
                        float frac = s / (float)sides;
                        float ang = frac * Mathf.PI * 2f;
                        float groove = 1f + Mathf.Sin(ang * 5f + ring * 1.1f) * 0.07f;
                        float r = radii[ring] * groove;
                        Vector3 offset = (right * Mathf.Cos(ang) + fwd * Mathf.Sin(ang)) * r;
                        verts.Add(centers[ring] + offset);
                        uvs.Add(new Vector2(frac, vCoords[ring]));
                    }
                }

                int stride = sides + 1;
                for (int ring = 0; ring < 2; ring++)
                {
                    int row0 = baseIdx + ring * stride;
                    int row1 = row0 + stride;
                    for (int s = 0; s < sides; s++)
                    {
                        tris.Add(row0 + s);
                        tris.Add(row1 + s);
                        tris.Add(row1 + s + 1);
                        tris.Add(row0 + s);
                        tris.Add(row1 + s + 1);
                        tris.Add(row0 + s + 1);
                    }
                }
            }

            AddTaperedLimb(new Vector3(0f, 0f, 0f), new Vector3(0.04f, 1.35f, -0.02f), new Vector3(0.02f, 2.65f, 0.03f), 0.24f, 0.17f, 0.12f, 8);
            AddTaperedLimb(new Vector3(0.02f, 2.35f, 0.02f), new Vector3(0.45f, 2.95f, 0.25f), new Vector3(0.84f, 3.45f, 0.48f), 0.11f, 0.08f, 0.05f, 6);
            AddTaperedLimb(new Vector3(0.01f, 2.40f, 0.01f), new Vector3(-0.42f, 3.00f, -0.24f), new Vector3(-0.80f, 3.50f, -0.45f), 0.11f, 0.08f, 0.05f, 6);
            AddTaperedLimb(new Vector3(0.02f, 2.45f, 0.02f), new Vector3(-0.18f, 2.95f, 0.42f), new Vector3(-0.36f, 3.35f, 0.78f), 0.10f, 0.07f, 0.04f, 6);

            Mesh m = new Mesh
            {
                name = "OrganicTreeTrunk_Mesh",
                vertices = verts.ToArray(),
                uv = uvs.ToArray(),
                triangles = tris.ToArray()
            };
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildOrganicTreeCanopyMesh(int seed, float horizontalRadius, float verticalRadius)
        {
            const int latSegments = 8;
            const int lonSegments = 12;
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            float seedOffset = seed * 0.73f;
            for (int lat = 0; lat <= latSegments; lat++)
            {
                float v = lat / (float)latSegments;
                float phi = v * Mathf.PI;
                float sinPhi = Mathf.Sin(phi);
                float cosPhi = Mathf.Cos(phi);

                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    float u = lon / (float)lonSegments;
                    float theta = u * Mathf.PI * 2f;

                    Vector3 dir = new Vector3(sinPhi * Mathf.Cos(theta), cosPhi, sinPhi * Mathf.Sin(theta));
                    float lobe1 = Mathf.Sin(dir.x * 3.8f + seedOffset) * Mathf.Cos(dir.z * 3.8f - seedOffset);
                    float lobe2 = Mathf.Sin(dir.y * 4.5f + dir.x * 2.7f + seedOffset * 1.4f);
                    float displacement = 1f + lobe1 * 0.18f + lobe2 * 0.11f;

                    float yFactor = dir.y < 0f ? 0.72f : 1.0f;
                    Vector3 pos = new Vector3(
                        dir.x * horizontalRadius * displacement,
                        dir.y * verticalRadius * yFactor * displacement,
                        dir.z * horizontalRadius * displacement);

                    verts.Add(pos);
                    uvs.Add(new Vector2(u * 2f, v * 2f));
                }
            }

            int stride = lonSegments + 1;
            for (int lat = 0; lat < latSegments; lat++)
            {
                int row0 = lat * stride;
                int row1 = row0 + stride;
                for (int lon = 0; lon < lonSegments; lon++)
                {
                    tris.Add(row0 + lon);
                    tris.Add(row1 + lon + 1);
                    tris.Add(row1 + lon);

                    tris.Add(row0 + lon);
                    tris.Add(row0 + lon + 1);
                    tris.Add(row1 + lon + 1);
                }
            }

            Mesh m = new Mesh
            {
                name = $"OrganicTreeCanopy_{seed}_Mesh",
                vertices = verts.ToArray(),
                uv = uvs.ToArray(),
                triangles = tris.ToArray()
            };
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

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
                // Commercial & Main Street Sidewalk
                new[] { new Vector3(17.0f, 0.04f, -12f), new Vector3(17.0f, 0.04f, 10f), new Vector3(17.0f, 0.04f, 34f) },
                new[] { new Vector3(17.0f, 0.04f, 54f), new Vector3(17.0f, 0.04f, 80f), new Vector3(17.0f, 0.04f, 106f) },
                new[] { new Vector3(17.0f, 0.04f, 126f), new Vector3(17.0f, 0.04f, 150f), new Vector3(17.0f, 0.04f, 174f) },
                // West Central Sidewalk
                new[] { new Vector3(-17.0f, 0.04f, -12f), new Vector3(-17.0f, 0.04f, 12f), new Vector3(-17.0f, 0.04f, 34f) },
                new[] { new Vector3(-17.0f, 0.04f, 54f), new Vector3(-17.0f, 0.04f, 80f), new Vector3(-17.0f, 0.04f, 106f) },
                new[] { new Vector3(-17.0f, 0.04f, 126f), new Vector3(-17.0f, 0.04f, 150f), new Vector3(-17.0f, 0.04f, 174f) },
                // Town Square Plaza
                new[] { new Vector3(-8.5f, 0.04f, 64f), new Vector3(9.5f, 0.04f, 64f), new Vector3(9.5f, 0.04f, 96f), new Vector3(-8.5f, 0.04f, 96f) },
                new[] { new Vector3(8.5f, 0.04f, 94f), new Vector3(-7.5f, 0.04f, 94f), new Vector3(-7.5f, 0.04f, 66f) },
                // Çınarlı Park
                new[] { new Vector3(-14f, 0.04f, 150f), new Vector3(0f, 0.04f, 150f), new Vector3(14f, 0.04f, 150f) },
                new[] { new Vector3(0f, 0.04f, 128f), new Vector3(0f, 0.04f, 150f), new Vector3(0f, 0.04f, 172f) },
                // West Residential Walkways
                new[] { new Vector3(-31.5f, 0.04f, -8f), new Vector3(-31.5f, 0.04f, 10f), new Vector3(-31.5f, 0.04f, 28f) },
                new[] { new Vector3(-31.5f, 0.04f, 62f), new Vector3(-31.5f, 0.04f, 80f), new Vector3(-31.5f, 0.04f, 98f) },
                // East Residential & Community Garden
                new[] { new Vector3(31.5f, 0.04f, 62f), new Vector3(31.5f, 0.04f, 80f), new Vector3(31.5f, 0.04f, 98f) },
                new[] { new Vector3(48.0f, 0.04f, -10f), new Vector3(48.0f, 0.04f, 10f), new Vector3(48.0f, 0.04f, 30f) }
            };

            SeatInteractable[] seatArray = benchSeats != null ? benchSeats.ToArray() : Array.Empty<SeatInteractable>();

            string[] citizenNames = { "Ahmet", "Selin", "Kemal", "Zeynep", "Murat", "Elif", "Hakan", "Ayşe", "Burak", "Deniz", "Emre", "Merve", "Can", "Derya" };
            string[] citizenRoles = { "Mahalleli", "Esnaf", "Şoför", "Öğrenci", "Tüccar", "Komşu", "Teknisyen" };
            string[][] citizenChats =
            {
                new[] { "Selam! Meydandaki Kerem yeni koşu ayakkabıları getirmiş, bir bak istersen.", "Bugün hava tam yürüyüşlük, değil mi?" },
                new[] { "Tamirhane tarafındaki Tarık'ta araba için turbo kiti var, bayağı fark ediyormuş.", "Çınarlı Kasabası küçük ama herkes birbirini tanır." },
                new[] { "Kuzey Kargo Deposu'ndan çıkan araçları gördün mü? Bu aralar bir gariplik var.", "Dikkatli sür, kavşaklardaki trafik ışıkları düzenli çalışıyor." },
                new[] { "Batı taraftaki evlerin arasında koyu renkli terk edilmiş bir araba görmüştüm sanki.", "Yorulduysan Çınar Kafe'de güzel kahve yapıyorlar." },
                new[] { "İlan panolarından pizza veya taksi işi alıp hızlıca para biriktirebilirsin.", "Kazandığın parayla ceketini bile değiştirebilirsin!" }
            };

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

                Material[] pedSkins = { mats.CharSkin, mats.CharSkinMedium, mats.CharSkinWarm };
                Material[] pedHairs = { mats.CharHair, mats.CharHairBrown, mats.CharHairBlonde };
                Material[] pedPants = { mats.CharPants, mats.CharPantsDenim, mats.CharPantsKhaki };
                BlenderCharacterVariant pedVar = (i % 5 == 4)
                    ? BlenderCharacterVariant.Worker
                    : ((i % 2 == 0) ? BlenderCharacterVariant.CasualMale : BlenderCharacterVariant.CasualFemale);

                ProceduralHumanoid hum = BuildBlenderHumanoid(
                    pedObj.transform,
                    mats,
                    pedVar,
                    pedShirts[i % pedShirts.Length],
                    pedPants[i % pedPants.Length],
                    pedSkins[i % pedSkins.Length],
                    pedHairs[(i + 1) % pedHairs.Length],
                    pedVar == BlenderCharacterVariant.CasualFemale ? (1.68f + (i % 3) * 0.025f) : (1.74f + (i % 3) * 0.03f));

                TownPedestrian ped = pedObj.AddComponent<TownPedestrian>();
                ped.ConfigureSchedule(route, seatArray, hum, 1.30f + (i % 4) * 0.12f, nextWp);
                ped.ConfigureCitizen(
                    citizenNames[i % citizenNames.Length],
                    citizenRoles[i % citizenRoles.Length],
                    citizenChats[i % citizenChats.Length],
                    brave: (i % 4 == 0));

                InteractableTrigger chatTrig = pedObj.AddComponent<InteractableTrigger>();
                chatTrig.Configure(InteractableKind.TownCitizen, $"citizen_{i + 1:00}", "ui.btn.chat", 2.6f);
            }
        }
    }
}
