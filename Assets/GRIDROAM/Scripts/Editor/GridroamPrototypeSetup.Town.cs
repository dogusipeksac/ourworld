using System;
using Gridroam.Player;
using Gridroam.World;
using UnityEngine;

namespace Gridroam.Editor
{
    public static partial class GridroamPrototypeSetup
    {
        /// <summary>
        /// Builds the "Liman Kasabası" coastal small town replacing the old apartment blocks:
        /// Main Street shops, central Town Square with fountain & clock tower, Residential Quarter,
        /// neighborhood Park, extended cross streets, street furniture, traffic lights, and pedestrians.
        /// </summary>
        private static void BuildSmallTown(Transform envParent, MaterialSet mats, PrefabSet prefabs)
        {
            GameObject townRoot = new GameObject("Zone_SmallTown_LimanKasabasi");
            townRoot.transform.SetParent(envParent, false);

            System.Random rng = new System.Random(1234);

            // C3e: Extend cross streets westward to reach Residential Lane at x = -46
            BuildTownRoads(townRoot.transform, mats);

            // C3a: Main Street Shops (east-facing row + back-alley companion row)
            BuildMainStreetShops(townRoot.transform, mats, rng);

            // C3b: Town Square (paving, octagon fountain, benches, trees, 16m clock tower, 4 lamps, planters)
            BuildTownSquare(townRoot.transform, mats, prefabs);

            // C3c & C3d: Residential Quarter (2 rows of detached 2-storey houses) & Park
            BuildResidentialAndPark(townRoot.transform, mats, rng);

            // C3f: Street Furniture (crosswalks, traffic lights, hydrants, trash cans, benches, parked cars, trees)
            BuildTownStreetFurniture(townRoot.transform, mats, prefabs, rng);

            // C4: Walking Sidewalk & Square Pedestrians
            BuildTownPedestrians(townRoot.transform, mats);
        }

        /// <summary>
        /// Creates a static box with procedurally generated per-face world-scale UVs (size / metersPerTile),
        /// compensating for any material _BaseMap tiling so brick, facade, and paving textures never stretch.
        /// </summary>
        private static GameObject CreateTiledBox(
            string name,
            Transform parent,
            Vector3 center,
            Vector3 size,
            Quaternion rot,
            Material mat,
            float metersPerTile,
            bool collider = true)
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

            MarkStaticEnvironment(go);
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

            // Back (-Z)
            AddFace(
                new Vector3(-hx, -hy, -hz), new Vector3(-hx, hy, -hz),
                new Vector3(hx, hy, -hz), new Vector3(hx, -hy, -hz),
                Vector3.back, size.x, size.y);

            // Front (+Z)
            AddFace(
                new Vector3(hx, -hy, hz), new Vector3(hx, hy, hz),
                new Vector3(-hx, hy, hz), new Vector3(-hx, -hy, hz),
                Vector3.forward, size.x, size.y);

            // Right (+X)
            AddFace(
                new Vector3(hx, -hy, -hz), new Vector3(hx, hy, -hz),
                new Vector3(hx, hy, hz), new Vector3(hx, -hy, hz),
                Vector3.right, size.z, size.y);

            // Left (-X)
            AddFace(
                new Vector3(-hx, -hy, hz), new Vector3(-hx, hy, hz),
                new Vector3(-hx, hy, -hz), new Vector3(-hx, -hy, -hz),
                Vector3.left, size.z, size.y);

            // Top (+Y)
            AddFace(
                new Vector3(-hx, hy, -hz), new Vector3(-hx, hy, hz),
                new Vector3(hx, hy, hz), new Vector3(hx, hy, -hz),
                Vector3.up, size.x, size.z);

            // Bottom (-Y)
            AddFace(
                new Vector3(-hx, -hy, hz), new Vector3(-hx, -hy, -hz),
                new Vector3(hx, -hy, -hz), new Vector3(hx, -hy, hz),
                Vector3.down, size.x, size.z);

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

            // Narrow residential lane at x = -46, width 6, z = 10..240
            CreateStaticBlock("Road_ResidentialLane", roadsRoot.transform,
                new Vector3(-46f, 0.025f, 125f), new Vector3(6f, 0.08f, 230f),
                mats.RoadAsphaltMain, addCollider: false);

            // Extend Road_CrossStreet_South (z = 92) and Road_CrossStreet_North (z = 174) westward to x = -49
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

            // Segments along Z leaving cross-street gaps (85..100, 166..182) and Town Square (120..148)
            Vector2[] zSegments =
            {
                new Vector2(20f, 85f),
                new Vector2(100f, 120f),
                new Vector2(148f, 166f),
                new Vector2(182f, 235f)
            };

            int shopIdx = 0;
            foreach (Vector2 seg in zSegments)
            {
                // East-facing Main Street row (front at x = 13.4, depth 9..11 m)
                float cursorZ = seg.x;
                while (cursorZ < seg.y - 5.5f)
                {
                    float remaining = seg.y - cursorZ;
                    float widthZ = remaining < 12.5f
                        ? remaining
                        : Mathf.Min(remaining, 7.0f + (float)rng.NextDouble() * 3.0f);
                    float depthX = 9.0f + (float)rng.NextDouble() * 2.0f;
                    int storeys = 2 + rng.Next(3); // 2 to 4 storeys
                    float height = 3.4f + (storeys - 1) * 3.0f + (float)rng.NextDouble() * 0.45f;

                    float centerZ = cursorZ + widthZ * 0.5f;
                    float eastFaceX = 13.4f;
                    float centerX = eastFaceX - depthX * 0.5f;

                    Material facade = facadeMats[shopIdx % facadeMats.Length];
                    float tileMeters = facade == mats.GarageBrick ? 1.5f : 3.0f;
                    Material awning = awningMats[(shopIdx + rng.Next(2)) % awningMats.Length];
                    bool pitchedRoof = (shopIdx % 2 == 0) && storeys <= 3;

                    BuildShopBuilding(
                        $"Shop_Main_{shopIdx + 1:00}",
                        shopsRoot.transform,
                        new Vector3(centerX, 0f, centerZ),
                        depthX, widthZ, height, storeys,
                        facingEast: true,
                        facade, tileMeters, awning, pitchedRoof, mats, rng);

                    cursorZ += widthZ;
                    shopIdx++;
                }

                // West-facing Back-Alley row (front at x = -18.8, depth 9..10.5 m)
                float alleyZ = seg.x;
                int alleyLocalIdx = 0;
                while (alleyZ < seg.y - 6.0f)
                {
                    float remaining = seg.y - alleyZ;
                    float widthZ = remaining < 13.5f
                        ? remaining
                        : Mathf.Min(remaining, 8.0f + (float)rng.NextDouble() * 3.0f);
                    float depthX = 9.0f + (float)rng.NextDouble() * 1.5f;
                    int storeys = 2 + rng.Next(2); // 2 to 3 storeys
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

            // Main building body (tiled UVs + collider)
            CreateTiledBox("Body", root.transform,
                new Vector3(basePos.x, height * 0.5f, basePos.z),
                new Vector3(depthX, height, widthZ),
                Quaternion.identity, facadeMat, metersPerTile, collider: true);

            // Ground-floor shopfront glass band (0.05m proud)
            bool litShop = rng.NextDouble() < 0.55;
            Material shopGlassMat = litShop ? mats.WindowLitWarm : mats.SedanGlass;
            CreateTiledBox("ShopfrontGlass", root.transform,
                new Vector3(frontX + frontSign * 0.05f, 1.55f, basePos.z),
                new Vector3(0.10f, 2.55f, Mathf.Max(2.5f, widthZ - 1.1f)),
                Quaternion.identity, shopGlassMat, 2.0f, collider: false);

            // Dark shopfront frame / door accent
            CreateTiledBox("ShopDoor", root.transform,
                new Vector3(frontX + frontSign * 0.08f, 1.25f, basePos.z + (rng.Next(2) == 0 ? -1.4f : 1.4f)),
                new Vector3(0.08f, 2.30f, 1.15f),
                Quaternion.identity, mats.TownDoorDark, 2.0f, collider: false);

            // Coloured fabric awning protruding over sidewalk
            float awningAngle = facingEast ? -22f : 22f;
            CreateTiledBox("Awning", root.transform,
                new Vector3(frontX + frontSign * 0.68f, 3.05f, basePos.z),
                new Vector3(1.38f, 0.11f, Mathf.Max(2.6f, widthZ - 0.9f)),
                Quaternion.Euler(0f, 0f, awningAngle), awningMat, 2.0f, collider: false);

            // Sign board above awning
            CreateTiledBox("SignBoard", root.transform,
                new Vector3(frontX + frontSign * 0.12f, 3.55f, basePos.z),
                new Vector3(0.14f, 0.56f, Mathf.Max(2.4f, widthZ - 1.6f)),
                Quaternion.identity, mats.SedanTrimDark, 2.0f, collider: false);

            // Upper floor windows on front face every ~2.2 m
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
                    Material wMat = rng.NextDouble() < 0.42 ? mats.WindowLitWarm : mats.SedanGlass;
                    CreateTiledBox($"Win_F{floor}_{c}", root.transform,
                        new Vector3(frontX + frontSign * 0.05f, wy, wz),
                        new Vector3(0.10f, 1.35f, 1.10f),
                        Quaternion.identity, wMat, 2.0f, collider: false);
                }

                // Back facade: fewer plain windows (2 per upper floor)
                if (floor == 1 || floor == storeys - 1)
                {
                    for (int b = 0; b < 2; b++)
                    {
                        float bz = basePos.z + (b == 0 ? -widthZ * 0.24f : widthZ * 0.24f);
                        Material bwMat = rng.NextDouble() < 0.25 ? mats.WindowLitWarm : mats.SedanGlass;
                        CreateTiledBox($"BackWin_F{floor}_{b}", root.transform,
                            new Vector3(backX - frontSign * 0.05f, wy, bz),
                            new Vector3(0.10f, 1.20f, 1.05f),
                            Quaternion.identity, bwMat, 2.0f, collider: false);
                    }
                }
            }

            // Top cornice / parapet strip
            CreateTiledBox("Cornice", root.transform,
                new Vector3(basePos.x, height + 0.24f, basePos.z),
                new Vector3(depthX + 0.40f, 0.48f, widthZ + 0.20f),
                Quaternion.identity, mats.PlasterTrim, 3.0f, collider: false);

            // Optional pitched tiled roof
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

        private static void BuildTownSquare(Transform parent, MaterialSet mats, PrefabSet prefabs)
        {
            GameObject sqRoot = new GameObject("Town_Square");
            sqRoot.transform.SetParent(parent, false);

            // Paving covering x -18..13, z 120..148 (top flush at y = 0.16)
            CreateTiledBox("Square_Paving", sqRoot.transform,
                new Vector3(-2.5f, 0.08f, 134f), new Vector3(31f, 0.16f, 28f),
                Quaternion.identity, mats.TownPaving, metersPerTile: 2.0f, collider: true);

            // Central Octagon Fountain at (-2.0, 0.16, 134.0)
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

            // Fountain water disc
            GameObject waterDisc = CreatePrimitiveChild("FountainWater", PrimitiveType.Cylinder, fountain.transform,
                fountainCenter + new Vector3(0f, 0.50f, 0f), new Vector3(5.8f, 0.06f, 5.8f), mats.SeaWater, removeCollider: true);
            MarkStaticEnvironment(waterDisc);

            // 2-tier stone column
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

            // 6 Benches around the fountain
            float[] benchAngles = { 30f, 90f, 150f, 210f, 270f, 330f };
            for (int i = 0; i < benchAngles.Length; i++)
            {
                float rad = benchAngles[i] * Mathf.Deg2Rad;
                Vector3 pos = fountainCenter + new Vector3(Mathf.Sin(rad) * 5.6f, 0f, Mathf.Cos(rad) * 5.6f);
                Quaternion rot = Quaternion.Euler(0f, benchAngles[i] + 180f, 0f);
                CreateBench($"Square_Bench_{i + 1}", sqRoot.transform, pos, rot, mats);
            }

            // 6 Trees around the edge of the square
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

            // 16 m tall Clock Tower at the west edge of the square
            BuildClockTower(sqRoot.transform, new Vector3(-14.6f, 0.16f, 134.0f), mats);

            // 4 Street lamps with non-shadow warm point lights
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

            // Low planter boxes framing the square entrances
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

            // Base plinth (0..3m)
            CreateTiledBox("Tower_Base", tower.transform,
                basePos + new Vector3(0f, 1.5f, 0f), new Vector3(4.4f, 3.0f, 4.4f),
                Quaternion.identity, mats.Concrete, 2.0f, collider: true);

            // Main shaft (3..11.5m)
            CreateTiledBox("Tower_Shaft", tower.transform,
                basePos + new Vector3(0f, 7.25f, 0f), new Vector3(3.6f, 8.5f, 3.6f),
                Quaternion.identity, mats.PlasterTrim, 3.0f, collider: true);

            // Clock chamber (11.5..14.3m)
            CreateTiledBox("Tower_ClockBox", tower.transform,
                basePos + new Vector3(0f, 12.9f, 0f), new Vector3(3.9f, 2.8f, 3.9f),
                Quaternion.identity, mats.Concrete, 2.0f, collider: true);

            // Cornice & pitched cap reaching 16 m
            CreateTiledBox("Tower_Cornice", tower.transform,
                basePos + new Vector3(0f, 14.45f, 0f), new Vector3(4.3f, 0.35f, 4.3f),
                Quaternion.identity, mats.PlasterTrim, 2.0f, collider: false);
            CreateTiledBox("Tower_CapLower", tower.transform,
                basePos + new Vector3(0f, 15.05f, 0f), new Vector3(3.2f, 0.9f, 3.2f),
                Quaternion.identity, mats.TownRoofTile, 1.5f, collider: false);
            CreateTiledBox("Tower_CapUpper", tower.transform,
                basePos + new Vector3(0f, 15.75f, 0f), new Vector3(1.6f, 0.7f, 1.6f),
                Quaternion.identity, mats.TownRoofTile, 1.5f, collider: false);

            // Emissive clock faces on all 4 sides
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
                MarkStaticEnvironment(dial);

                GameObject hourHand = CreatePrimitiveChild("HourHand", PrimitiveType.Cube, faceRoot.transform,
                    new Vector3(0f, 0.22f, 0.05f), new Vector3(0.09f, 0.50f, 0.04f), mats.SedanTrimDark, removeCollider: true);
                MarkStaticEnvironment(hourHand);

                GameObject minHand = CreatePrimitiveChild("MinuteHand", PrimitiveType.Cube, faceRoot.transform,
                    new Vector3(0.24f, 0f, 0.05f), new Vector3(0.58f, 0.07f, 0.04f), mats.SedanTrimDark, removeCollider: true);
                MarkStaticEnvironment(minHand);
            }
        }

        private static void BuildResidentialAndPark(Transform parent, MaterialSet mats, System.Random rng)
        {
            GameObject resRoot = new GameObject("Town_ResidentialQuarter");
            resRoot.transform.SetParent(parent, false);

            Material[] wallPalette = { mats.PlasterTrim, mats.BuildingFacadeB, mats.TownBrickHouse };

            // 3 Z-bands leaving gaps for South Cross Street (z=92), Park (z=97..116), and North Cross Street (z=174)
            float[] houseZs =
            {
                24f, 38f, 52f, 66f, 80f,
                124f, 140f, 156f,
                186f, 201f, 216f, 231f
            };

            int houseIdx = 0;
            foreach (float z in houseZs)
            {
                // Row 1: between Sidewalk_AlleyWest (x=-33.2) and Road_ResidentialLane (x=-43.0) -> centered at x=-38.0
                Material wall1 = wallPalette[rng.Next(wallPalette.Length)];
                BuildResidentialHouse(
                    $"House_R1_{houseIdx + 1:00}", resRoot.transform,
                    new Vector3(-38.1f, 0f, z),
                    depthX: 7.4f, widthZ: 8.6f + (float)(rng.NextDouble() * 0.8),
                    wallMat: wall1, hasChimney: rng.Next(3) == 0, hasGardenTree: rng.Next(2) == 0,
                    mats, rng);

                // Row 2: west of Road_ResidentialLane (x=-49.0) -> centered at x=-56.5
                Material wall2 = wallPalette[rng.Next(wallPalette.Length)];
                BuildResidentialHouse(
                    $"House_R2_{houseIdx + 1:00}", resRoot.transform,
                    new Vector3(-56.5f, 0f, z),
                    depthX: 8.0f, widthZ: 8.8f + (float)(rng.NextDouble() * 0.8),
                    wallMat: wall2, hasChimney: rng.Next(3) == 0, hasGardenTree: rng.Next(2) == 0,
                    mats, rng);

                houseIdx++;
            }

            // C3d: Neighborhood Park (x -70..-34, z 97..116, split cleanly around Road_ResidentialLane at x -49..-43)
            GameObject parkRoot = new GameObject("Town_NeighborhoodPark");
            parkRoot.transform.SetParent(parent, false);

            // Main West Park Lawn (x = -69..-49.5, z = 96.8..116.5)
            CreateTiledBox("Park_GrassWest", parkRoot.transform,
                new Vector3(-59.25f, 0.02f, 106.65f), new Vector3(19.5f, 0.04f, 19.7f),
                Quaternion.identity, mats.TownGrass, 4.0f, collider: false);

            // East Pocket Lawn (x = -42.5..-33.5, z = 96.8..116.5)
            CreateTiledBox("Park_GrassEast", parkRoot.transform,
                new Vector3(-38.0f, 0.02f, 106.65f), new Vector3(9.0f, 0.04f, 19.7f),
                Quaternion.identity, mats.TownGrass, 4.0f, collider: false);

            // Walking paths through the park
            CreateTiledBox("Park_PathWest", parkRoot.transform,
                new Vector3(-59.25f, 0.04f, 106.5f), new Vector3(19.5f, 0.04f, 2.4f),
                Quaternion.identity, mats.TownPaving, 2.0f, collider: false);
            CreateTiledBox("Park_PathWestCross", parkRoot.transform,
                new Vector3(-58.5f, 0.04f, 106.65f), new Vector3(2.2f, 0.04f, 19.2f),
                Quaternion.identity, mats.TownPaving, 2.0f, collider: false);
            CreateTiledBox("Park_PathEast", parkRoot.transform,
                new Vector3(-38.0f, 0.04f, 106.5f), new Vector3(9.0f, 0.04f, 2.4f),
                Quaternion.identity, mats.TownPaving, 2.0f, collider: false);

            // 8 Trees in the park
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

            // 3 Benches along the park path
            CreateBench("Park_Bench_1", parkRoot.transform, new Vector3(-63.0f, 0.06f, 104.6f), Quaternion.identity, mats);
            CreateBench("Park_Bench_2", parkRoot.transform, new Vector3(-53.8f, 0.06f, 108.4f), Quaternion.Euler(0f, 180f, 0f), mats);
            CreateBench("Park_Bench_3", parkRoot.transform, new Vector3(-38.0f, 0.06f, 104.6f), Quaternion.identity, mats);
        }

        private static void BuildResidentialHouse(
            string name,
            Transform parent,
            Vector3 basePos,
            float depthX,
            float widthZ,
            Material wallMat,
            bool hasChimney,
            bool hasGardenTree,
            MaterialSet mats,
            System.Random rng)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = basePos;

            float height = 5.9f + (float)(rng.NextDouble() * 0.5);
            float tileMeters = wallMat == mats.TownBrickHouse ? 1.5f : 3.0f;
            float eastFaceX = basePos.x + depthX * 0.5f;

            // Main 2-storey house volume
            CreateTiledBox("Walls", root.transform,
                new Vector3(basePos.x, height * 0.5f, basePos.z),
                new Vector3(depthX, height, widthZ),
                Quaternion.identity, wallMat, tileMeters, collider: true);

            // Pitched tiled roof with overhang
            float overhang = 0.55f;
            float slopeLen = depthX * 0.60f + overhang;
            float roofY = height + 0.95f;
            CreateTiledBox("Roof_East", root.transform,
                new Vector3(basePos.x + depthX * 0.25f, roofY, basePos.z),
                new Vector3(slopeLen, 0.24f, widthZ + overhang * 2f),
                Quaternion.Euler(0f, 0f, -26f), mats.TownRoofTile, 1.5f, collider: false);
            CreateTiledBox("Roof_West", root.transform,
                new Vector3(basePos.x - depthX * 0.25f, roofY, basePos.z),
                new Vector3(slopeLen, 0.24f, widthZ + overhang * 2f),
                Quaternion.Euler(0f, 0f, 26f), mats.TownRoofTile, 1.5f, collider: false);

            // Front door facing east
            CreateTiledBox("FrontDoor", root.transform,
                new Vector3(eastFaceX + 0.05f, 1.15f, basePos.z),
                new Vector3(0.10f, 2.25f, 1.10f),
                Quaternion.identity, mats.TownDoorDark, 2.0f, collider: false);

            // 5 windows (2 ground floor flanking door, 3 upper floor) with frames
            Vector3[] winPositions =
            {
                new Vector3(eastFaceX + 0.05f, 1.65f, basePos.z - 2.4f),
                new Vector3(eastFaceX + 0.05f, 1.65f, basePos.z + 2.4f),
                new Vector3(eastFaceX + 0.05f, 4.35f, basePos.z - 2.4f),
                new Vector3(eastFaceX + 0.05f, 4.35f, basePos.z),
                new Vector3(eastFaceX + 0.05f, 4.35f, basePos.z + 2.4f)
            };

            for (int i = 0; i < winPositions.Length; i++)
            {
                Material wMat = rng.NextDouble() < 0.45 ? mats.WindowLitWarm : mats.SedanGlass;
                CreateTiledBox($"Win_{i}", root.transform, winPositions[i],
                    new Vector3(0.08f, 1.25f, 1.05f), Quaternion.identity, wMat, 2.0f, collider: false);
                CreateTiledBox($"WinSill_{i}", root.transform,
                    winPositions[i] + new Vector3(0.04f, -0.68f, 0f),
                    new Vector3(0.14f, 0.10f, 1.22f), Quaternion.identity, mats.PlasterTrim, 2.0f, collider: false);
            }

            // Optional chimney (~1/3 of houses)
            if (hasChimney)
            {
                CreateTiledBox("Chimney", root.transform,
                    new Vector3(basePos.x - depthX * 0.18f, height + 1.9f, basePos.z + widthZ * 0.28f),
                    new Vector3(0.75f, 2.1f, 0.75f),
                    Quaternion.identity, mats.GarageBrick, 1.2f, collider: false);
            }

            // Low garden hedge along front yard with entrance gap
            float fenceX = eastFaceX + 1.05f;
            float hedgeLen = widthZ * 0.36f;
            CreateTiledBox("GardenHedge_S", root.transform,
                new Vector3(fenceX, 0.45f, basePos.z - widthZ * 0.32f),
                new Vector3(0.42f, 0.90f, hedgeLen),
                Quaternion.identity, mats.TownHedge, 1.5f, collider: true);
            CreateTiledBox("GardenHedge_N", root.transform,
                new Vector3(fenceX, 0.45f, basePos.z + widthZ * 0.32f),
                new Vector3(0.42f, 0.90f, hedgeLen),
                Quaternion.identity, mats.TownHedge, 1.5f, collider: true);

            // Optional garden tree (~half of gardens)
            if (hasGardenTree)
            {
                float treeOffsetZ = rng.Next(2) == 0 ? -(widthZ * 0.5f + 1.2f) : (widthZ * 0.5f + 1.2f);
                CreateTownTree("GardenTree", root.transform,
                    new Vector3(basePos.x + depthX * 0.35f, 0f, basePos.z + treeOffsetZ),
                    mats, 0.85f + (float)rng.NextDouble() * 0.25f, rng.Next(100));
            }
        }

        private static void BuildTownStreetFurniture(
            Transform parent,
            MaterialSet mats,
            PrefabSet prefabs,
            System.Random rng)
        {
            GameObject furnRoot = new GameObject("Town_StreetFurniture");
            furnRoot.transform.SetParent(parent, false);

            // 1. Zebra Crosswalks at every intersection of Coastal Main (x=24) & Back Alley (x=-26) with z=92 and z=174
            float[] crossZs = { 92f, 174f };
            foreach (float cz in crossZs)
            {
                // Coastal Main north & south approaches (crossing x = 18.5..29.5)
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Coastal_{cz}_S", new Vector3(24f, 0.08f, cz - 6.2f), 10.5f, horizontalX: true, mats.RoadMarkingWhite);
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Coastal_{cz}_N", new Vector3(24f, 0.08f, cz + 6.2f), 10.5f, horizontalX: true, mats.RoadMarkingWhite);
                // Coastal Main west cross-street approach (crossing z = cz-3.8..cz+3.8 at x = 15.8)
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Coastal_{cz}_W", new Vector3(16.0f, 0.08f, cz), 7.5f, horizontalX: false, mats.RoadMarkingWhite);

                // Back Alley north & south approaches (crossing x = -29.8..-22.2)
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Alley_{cz}_S", new Vector3(-26f, 0.08f, cz - 6.2f), 7.5f, horizontalX: true, mats.RoadMarkingWhite);
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Alley_{cz}_N", new Vector3(-26f, 0.08f, cz + 6.2f), 7.5f, horizontalX: true, mats.RoadMarkingWhite);
                // Back Alley east & west cross-street approaches
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Alley_{cz}_E", new Vector3(-20.4f, 0.08f, cz), 7.5f, horizontalX: false, mats.RoadMarkingWhite);
                CreateZebraCrosswalk(furnRoot.transform, $"Zebra_Alley_{cz}_W", new Vector3(-31.6f, 0.08f, cz), 7.5f, horizontalX: false, mats.RoadMarkingWhite);
            }

            // 2. Four Traffic Light poles at Coastal Main / z=92 and z=174 intersections (off road surface)
            CreateTrafficLightPole("TrafficLight_South_SW", furnRoot.transform, new Vector3(17.0f, 0.16f, 86.2f), Quaternion.Euler(0f, 0f, 0f), mats, phaseOffset: 0f);
            CreateTrafficLightPole("TrafficLight_South_NE", furnRoot.transform, new Vector3(31.2f, 0.16f, 97.8f), Quaternion.Euler(0f, 180f, 0f), mats, phaseOffset: 0f);
            CreateTrafficLightPole("TrafficLight_North_SW", furnRoot.transform, new Vector3(17.0f, 0.16f, 168.2f), Quaternion.Euler(0f, 0f, 0f), mats, phaseOffset: 8f);
            CreateTrafficLightPole("TrafficLight_North_NE", furnRoot.transform, new Vector3(31.2f, 0.16f, 179.8f), Quaternion.Euler(0f, 180f, 0f), mats, phaseOffset: 8f);

            // 3. Fire hydrants, trash cans, and benches along sidewalks every ~25 m
            int propIdx = 0;
            for (float z = 25f; z <= 228f; z += 25f)
            {
                if (IsNearCrossStreet(z, 7.5f)) continue;

                // Main West sidewalk (x = 16.5 near curb)
                if (propIdx % 2 == 0)
                {
                    PlacePrefabInstance(prefabs.FireHydrant, furnRoot.transform, new Vector3(16.6f, 0.16f, z), Quaternion.identity);
                }
                else
                {
                    PlacePrefabInstance(prefabs.TrashCan, furnRoot.transform, new Vector3(16.5f, 0.16f, z), Quaternion.Euler(0f, propIdx * 30f, 0f));
                }

                // Sidewalk bench outside shops (skip Town Square which has its own benches)
                if ((z < 118f || z > 150f) && propIdx % 2 == 1)
                {
                    CreateBench($"MainSidewalk_Bench_{propIdx}", furnRoot.transform,
                        new Vector3(14.35f, 0.16f, z + 5f), Quaternion.Euler(0f, 90f, 0f), mats);
                }

                // Back Alley sidewalks
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

            // 4. 8 Parked cars along Back Alley & Residential Lane, aligned with road
            (Vector3 pos, float yaw)[] parkedCars =
            {
                (new Vector3(-23.0f, 0.07f, 34f), 0f),
                (new Vector3(-29.0f, 0.07f, 68f), 180f),
                (new Vector3(-23.0f, 0.07f, 134f), 0f),
                (new Vector3(-29.0f, 0.07f, 206f), 180f),
                (new Vector3(-44.1f, 0.07f, 44f), 0f),
                (new Vector3(-47.9f, 0.07f, 76f), 180f),
                (new Vector3(-44.1f, 0.07f, 146f), 0f),
                (new Vector3(-47.9f, 0.07f, 212f), 180f)
            };

            for (int i = 0; i < parkedCars.Length; i++)
            {
                GameObject car = PlacePrefabInstance(prefabs.TrafficVehicle, furnRoot.transform,
                    parkedCars[i].pos, Quaternion.Euler(0f, parkedCars[i].yaw, 0f));
                car.name = $"Town_ParkedCar_{i + 1:00}";
            }

            // 5. Trees along Sidewalk_MainWest every 16 m (clear of intersections & square entrance)
            int treeIdx = 0;
            for (float z = 24f; z <= 232f; z += 16f)
            {
                if (IsNearCrossStreet(z, 9.0f)) continue;
                if (z > 127f && z < 141f) continue; // Keep Town Square center entrance open
                float scale = 0.92f + (float)rng.NextDouble() * 0.24f;
                CreateTownTree($"MainStreet_Tree_{treeIdx + 1:00}", furnRoot.transform,
                    new Vector3(16.65f, 0.16f, z), mats, scale, treeIdx);
                treeIdx++;
            }
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
                float offset = startOffset + i * 1.0f; // 0.5m stripe + 0.5m gap = 1.0m pitch
                Vector3 pos = horizontalX
                    ? center + new Vector3(offset, 0f, 0f)
                    : center + new Vector3(0f, 0f, offset);
                Vector3 scale = horizontalX
                    ? new Vector3(0.50f, 0.02f, 2.8f)
                    : new Vector3(2.8f, 0.02f, 0.50f);

                CreateStaticBlock($"Stripe_{i}", cw.transform, pos, scale, whiteMat, addCollider: false);
            }
        }

        private static void CreateTrafficLightPole(
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

            // Pole
            GameObject pole = CreatePrimitiveChild("Pole", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 2.7f, 0f), new Vector3(0.16f, 2.7f, 0.16f), mats.SedanTrimDark, removeCollider: false);
            MarkStaticEnvironment(pole);

            // Short horizontal arm + signal head box
            GameObject arm = CreatePrimitiveChild("Arm", PrimitiveType.Cube, root.transform,
                new Vector3(0.55f, 5.15f, 0f), new Vector3(1.2f, 0.12f, 0.12f), mats.SedanTrimDark, removeCollider: true);
            MarkStaticEnvironment(arm);

            GameObject head = CreatePrimitiveChild("SignalHead", PrimitiveType.Cube, root.transform,
                new Vector3(1.05f, 4.75f, 0f), new Vector3(0.36f, 1.02f, 0.36f), mats.SedanTrimDark, removeCollider: true);
            MarkStaticEnvironment(head);

            // Red, Amber, Green bulbs driven by TrafficLightCycler via MaterialPropertyBlock
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
        }

        private static void CreateBench(string name, Transform parent, Vector3 pos, Quaternion rot, MaterialSet mats)
        {
            GameObject bench = new GameObject(name);
            bench.transform.SetParent(parent, false);
            bench.transform.SetPositionAndRotation(pos, rot);

            // Wood seat & backrest
            CreatePrimitiveChild("Seat", PrimitiveType.Cube, bench.transform,
                new Vector3(0f, 0.45f, 0f), new Vector3(1.85f, 0.09f, 0.52f), mats.TownWood, removeCollider: true);
            GameObject back = CreatePrimitiveChild("Backrest", PrimitiveType.Cube, bench.transform,
                new Vector3(0f, 0.78f, -0.22f), new Vector3(1.85f, 0.38f, 0.08f), mats.TownWood, removeCollider: true);
            back.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);

            // Metal legs
            CreatePrimitiveChild("LegL", PrimitiveType.Cube, bench.transform,
                new Vector3(-0.74f, 0.23f, 0f), new Vector3(0.10f, 0.46f, 0.46f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("LegR", PrimitiveType.Cube, bench.transform,
                new Vector3(0.74f, 0.23f, 0f), new Vector3(0.10f, 0.46f, 0.46f), mats.SedanTrimDark, removeCollider: true);

            BoxCollider col = bench.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.48f, -0.04f);
            col.size = new Vector3(1.9f, 0.96f, 0.62f);

            MarkStaticEnvironment(bench);
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

            // Trunk cylinder with collider
            CreatePrimitiveChild("Trunk", PrimitiveType.Cylinder, tree.transform,
                new Vector3(0f, 1.55f, 0f), new Vector3(0.36f, 1.55f, 0.36f), mats.TownBark, removeCollider: true);

            CapsuleCollider trunkCol = tree.AddComponent<CapsuleCollider>();
            trunkCol.center = new Vector3(0f, 1.55f, 0f);
            trunkCol.radius = 0.24f;
            trunkCol.height = 3.1f;

            // 3 overlapping foliage spheres in two slightly different greens
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

        private static void BuildTownPedestrians(Transform parent, MaterialSet mats)
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
                mats.CharShirt,
                mats.PedShirtOlive,
                mats.PedShirtRed
            };

            (Vector3 a, Vector3 b, float startT)[] routes =
            {
                // Sidewalk_MainWest (3 pedestrians, centered cleanly between shop benches and curb trees)
                (new Vector3(15.35f, 0.16f, 26f), new Vector3(15.35f, 0.16f, 80f), 0.25f),
                (new Vector3(15.35f, 0.16f, 104f), new Vector3(15.35f, 0.16f, 160f), 0.60f),
                (new Vector3(15.35f, 0.16f, 186f), new Vector3(15.35f, 0.16f, 228f), 0.40f),
                // Sidewalk_AlleyEast & Sidewalk_AlleyWest (3 pedestrians)
                (new Vector3(-20.4f, 0.16f, 28f), new Vector3(-20.4f, 0.16f, 82f), 0.50f),
                (new Vector3(-20.4f, 0.16f, 186f), new Vector3(-20.4f, 0.16f, 228f), 0.30f),
                (new Vector3(-31.8f, 0.16f, 104f), new Vector3(-31.8f, 0.16f, 162f), 0.70f),
                // Town Square (2 pedestrians strolling clear of central fountain)
                (new Vector3(-8.5f, 0.16f, 142.5f), new Vector3(7.5f, 0.16f, 142.5f), 0.35f),
                (new Vector3(7.5f, 0.16f, 125.5f), new Vector3(-8.5f, 0.16f, 125.5f), 0.55f)
            };

            for (int i = 0; i < routes.Length; i++)
            {
                GameObject pedObj = new GameObject($"TownPedestrian_{i + 1:00}");
                pedObj.transform.SetParent(pedRoot.transform, false);

                Vector3 startPos = Vector3.Lerp(routes[i].a, routes[i].b, routes[i].startT);
                Vector3 dir = (routes[i].b - routes[i].a);
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

                float height = 1.68f + (i % 4) * 0.05f;
                ProceduralHumanoid hum = ProceduralHumanoid.Build(
                    pedObj.transform,
                    mats.CharSkin,
                    pedShirts[i % pedShirts.Length],
                    mats.CharPants,
                    mats.CharShoes,
                    mats.CharHair,
                    height);

                TownPedestrian ped = pedObj.AddComponent<TownPedestrian>();
                ped.Configure(routes[i].a, routes[i].b, hum, speed: 1.3f, startHeadingToB: (i % 2 == 0));
            }
        }
    }
}
