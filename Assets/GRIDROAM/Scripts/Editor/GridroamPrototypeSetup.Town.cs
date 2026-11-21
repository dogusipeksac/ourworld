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
            ring.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            ring.transform.localScale = new Vector3(2.8f, 0.035f, 2.8f);
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

            BuildClockTower(sqRoot.transform, new Vector3(-13.8f, 0.02f, 80.0f), mats);

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

        private static void BuildClockTower(Transform parent, Vector3 basePos, MaterialSet mats)
        {
            GameObject tower = new GameObject("Square_ClockTower");
            tower.transform.SetParent(parent, false);

            CreateTiledBox("Tower_Base", tower.transform,
                basePos + new Vector3(0f, 1.5f, 0f), new Vector3(4.2f, 3.0f, 4.2f),
                Quaternion.identity, mats.Concrete, 2.0f, collider: true);

            CreateTiledBox("Tower_Shaft", tower.transform,
                basePos + new Vector3(0f, 7.25f, 0f), new Vector3(3.4f, 8.5f, 3.4f),
                Quaternion.identity, mats.PlasterTrim, 3.0f, collider: true);

            CreateTiledBox("Tower_ClockBox", tower.transform,
                basePos + new Vector3(0f, 12.9f, 0f), new Vector3(3.7f, 2.8f, 3.7f),
                Quaternion.identity, mats.Concrete, 2.0f, collider: true);

            CreateTiledBox("Tower_Cornice", tower.transform,
                basePos + new Vector3(0f, 14.45f, 0f), new Vector3(4.1f, 0.35f, 4.1f),
                Quaternion.identity, mats.PlasterTrim, 2.0f, collider: false);
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
                    $"Çınar Caddesi No: {i + 1}",
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
                    $"Cumhuriyet Caddesi No: {i + 10}",
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

            // South-East Community Plaza & Parking Garden (x = +48, z = +10)
            GameObject eastGarden = new GameObject("Town_EastCommunityGarden");
            eastGarden.transform.SetParent(parent, false);
            CreateTiledBox("EastGarden_WalkwayNS", eastGarden.transform,
                new Vector3(48.0f, 0.014f, 10.0f), new Vector3(3.2f, 0.028f, 48.0f),
                Quaternion.identity, mats.TownPaving, 2.0f, collider: false);
            CreateTiledBox("EastGarden_WalkwayEW", eastGarden.transform,
                new Vector3(48.0f, 0.014f, 10.0f), new Vector3(30.0f, 0.028f, 3.2f),
                Quaternion.identity, mats.TownPaving, 2.0f, collider: false);
            var egB1 = CreateBench("EastGarden_Bench_1", eastGarden.transform, new Vector3(44.5f, 0.02f, 6.0f), Quaternion.Euler(0f, 90f, 0f), mats);
            var egB2 = CreateBench("EastGarden_Bench_2", eastGarden.transform, new Vector3(51.5f, 0.02f, 14.0f), Quaternion.Euler(0f, -90f, 0f), mats);
            benchSeatCollector.Add(egB1.leftSlot); benchSeatCollector.Add(egB1.rightSlot);
            benchSeatCollector.Add(egB2.leftSlot); benchSeatCollector.Add(egB2.rightSlot);
            CreateTownTree("EastGarden_Tree_1", eastGarden.transform, new Vector3(38.0f, 0f, -6.0f), mats, 1.05f, 41);
            CreateTownTree("EastGarden_Tree_2", eastGarden.transform, new Vector3(58.0f, 0f, -6.0f), mats, 1.12f, 42);
            CreateTownTree("EastGarden_Tree_3", eastGarden.transform, new Vector3(38.0f, 0f, 26.0f), mats, 0.98f, 43);
            CreateTownTree("EastGarden_Tree_4", eastGarden.transform, new Vector3(58.0f, 0f, 26.0f), mats, 1.06f, 44);

            // Central North Block: Çınarlı Park (x = -16..+16, z = +124..+176, centered at (0, 0, 150))
            GameObject parkRoot = new GameObject("Town_CinarliPark");
            parkRoot.transform.SetParent(parent, false);

            CreateTiledBox("Park_PathNS", parkRoot.transform,
                new Vector3(0f, 0.014f, 150f), new Vector3(3.2f, 0.028f, 52f),
                Quaternion.identity, mats.TownPaving, 2.0f, collider: false);
            CreateTiledBox("Park_PathEW", parkRoot.transform,
                new Vector3(0f, 0.014f, 150f), new Vector3(32f, 0.028f, 3.2f),
                Quaternion.identity, mats.TownPaving, 2.0f, collider: false);

            Vector3[] parkTrees =
            {
                new Vector3(-11f, 0f, 134f),
                new Vector3(11f, 0f, 134f),
                new Vector3(-11f, 0f, 166f),
                new Vector3(11f, 0f, 166f),
                new Vector3(-8f, 0f, 156f),
                new Vector3(8f, 0f, 142f)
            };
            for (int i = 0; i < parkTrees.Length; i++)
            {
                CreateTownTree($"Park_Tree_{i + 1}", parkRoot.transform, parkTrees[i], mats, 1.0f + (i % 3) * 0.14f, i + 20);
            }

            var pb1 = CreateBench("Park_Bench_1", parkRoot.transform, new Vector3(-5.5f, 0.02f, 146.5f), Quaternion.identity, mats);
            var pb2 = CreateBench("Park_Bench_2", parkRoot.transform, new Vector3(5.5f, 0.02f, 153.5f), Quaternion.Euler(0f, 180f, 0f), mats);
            var pb3 = CreateBench("Park_Bench_3", parkRoot.transform, new Vector3(5.5f, 0.02f, 146.5f), Quaternion.identity, mats);
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

            // 8 Drivable Town Cars parked cleanly off active driving lanes
            (Vector3 pos, float yaw, int fixedVariant)[] parkedCars =
            {
                (new Vector3(19.6f, 0.28f, -12.0f), 0f, 0),    // Red Hatchback near Pizzeria
                (new Vector3(19.6f, 0.28f, 102.0f), 0f, 2),    // Green Sedan near Taxi Rank
                (new Vector3(-28.4f, 0.28f, 0.0f), 180f, 1),   // White Pickup on Çınar Caddesi
                (new Vector3(-28.4f, 0.28f, 70.0f), 180f, 0),  // Hatchback in West Residential
                (new Vector3(-28.4f, 0.28f, 140.0f), 180f, 2), // Sedan near Park
                (new Vector3(28.4f, 0.28f, 70.0f), 0f, 1),     // Pickup in East Residential
                (new Vector3(56.0f, 0.28f, 18.0f), 90f, 0),    // Hatchback at East Community Garden
                (new Vector3(-67.6f, 0.28f, 80.0f), 0f, 1)     // Pickup on West Ring Street
            };

            GameObject[] drivableVariants =
            {
                prefabs.TownCarRedHatchback,
                prefabs.TownCarWhitePickup,
                prefabs.TownCarGreenSedan
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

            if (_activeBenchPrefab != null)
            {
                GameObject meshInst = PlacePrefabInstance(_activeBenchPrefab, bench.transform, Vector3.zero, Quaternion.identity);
                meshInst.transform.localPosition = Vector3.zero;
                meshInst.transform.localRotation = Quaternion.identity;
            }
            else
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

                ProceduralHumanoid hum = ProceduralHumanoid.Build(
                    pedObj.transform,
                    mats.CharSkin,
                    pedShirts[i % pedShirts.Length],
                    mats.CharPants,
                    mats.CharShoes,
                    mats.CharHair,
                    1.72f + (i % 3) * 0.03f);

                TownPedestrian ped = pedObj.AddComponent<TownPedestrian>();
                ped.ConfigureSchedule(route, seatArray, hum, 1.30f + (i % 4) * 0.12f, nextWp);
            }
        }
    }
}
