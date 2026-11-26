using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Builds "Hamidiye Atlas AVM" (Atlaspark Sultanbeyli inspired) — a grand, fully enterable 5-level shopping mall
    /// with 18 real walk-in glass-fronted stores, coffee houses, food court, CineAtlas movie theater, JoyPark arcade,
    /// Ennjoy rooftop terrace cafe, 4 walkable escalators, working 5-level panoramic glass elevator, and 28+ living NPCs!
    /// </summary>
    public sealed partial class HamidiyeMallBuilder : MonoBehaviour
    {
        public static HamidiyeMallBuilder Instance { get; private set; }

        public static readonly Vector3 MallCenter = new Vector3(-116f, 0f, -34f);
        public const float HalfW = 17f;     // 34 m wide (X: -133..-99)
        public const float HalfD = 16f;     // 32 m deep (Z: -50..-18)
        public const float AtriumHalf = 6f; // 12m x 12m open central atrium
        public const float FloorH = 4.6f;   // 4.6 m per level (0, 4.6, 9.2, 13.8, 18.4)

        private readonly List<Material> _mats = new List<Material>(48);
        private readonly List<Transform> _escalatorStrips = new List<Transform>(8);

        // Core architectural materials
        private Material _marbleFloor;
        private Material _terraceTile;
        private Material _facadeStone;
        private Material _darkFrame;
        private Material _storeWallLight;
        private Material _cinemaDarkWall;
        private Material _glassCurtain;
        private Material _glassRailing;
        private Material _asphaltMat;
        private Material _whiteLineMat;
        private Material _woodWarm;
        private Material _woodDark;
        private Material _tealAccent;
        private Material _goldAccent;
        private Material _coralRed;
        private Material _cinemaRed;
        private Material _screenGlow;
        private Material _neonTeal;
        private Material _neonAmber;
        private Material _ceilingGlow;
        private Material _waterMat;
        private Material _foliageMat;

        // Atlaspark-authentic brand accent materials
        private Material _brandGreen;   // StarBean Coffee / Pizza Lazza
        private Material _brandOrange;  // 5M Atlas Market / Cajun
        private Material _brandBlue;    // Atlas Waikiki / Mavi / D&M
        private Material _brandLilac;   // Gratis Beauty
        private Material _brandBrown;   // Kahve Diyarı
        private Material _brandRose;    // Karaca / English Living
        private Material _brandYellow;  // TeknoCell / Tavuk Diyarı

        private Transform _fountainSpinner;
        private MallElevatorController _elevator;
        private HamidiyeMallInteractionHandler _handler;
        private Vector3 _cinemaSeatWorldPos;
        private int _npcSerial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;

            GameObject go = new GameObject("OurWorld_HamidiyeAtlasAVM");
            go.AddComponent<HamidiyeMallBuilder>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            transform.position = MallCenter;
            BuildMaterials();

            Transform exteriorRoot = new GameObject("AVM_ExteriorAndParking").transform;
            exteriorRoot.SetParent(transform, false);

            GameObject interiorGo = new GameObject("AVM_InteriorFloorsAndShops");
            interiorGo.transform.SetParent(transform, false);
            Transform interiorRoot = interiorGo.transform;

            _handler = gameObject.AddComponent<HamidiyeMallInteractionHandler>();
            _handler.MallCenter = MallCenter;
            _handler.InteriorRoot = interiorGo;
            _handler.CinemaScreenMaterial = _screenGlow;

            BuildParkingAndEntryPlaza(exteriorRoot);
            BuildBuildingShellAndFloors(exteriorRoot, interiorRoot);
            BuildEscalatorsAndElevator(interiorRoot);

            BuildFloor0_GroundAtriumAndStores(interiorRoot);
            BuildFloor1_FashionTechAndHome(interiorRoot);
            BuildFloor2_AtlasparkFoodCourt(interiorRoot);
            BuildFloor3_CineAtlasAndJoyPark(interiorRoot);
            BuildFloor4_EnnjoyRooftopTerrace(interiorRoot);
            SpawnStrollingMallShoppers(interiorRoot);

            _handler.FountainSpinner = _fountainSpinner;
            _handler.EscalatorStepStrips = _escalatorStrips.ToArray();
            _handler.Elevator = _elevator;
            _handler.CinemaSeatWorldPos = _cinemaSeatWorldPos;

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _mats.Count; i++)
            {
                if (_mats[i] != null) Destroy(_mats[i]);
            }
            _mats.Clear();
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        // MATERIALS
        // =====================================================================
        private void BuildMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Sprites/Default");

            _marbleFloor    = MakeMat(lit, "AVM_Marble",       new Color(0.91f, 0.90f, 0.86f), 0.72f);
            _terraceTile    = MakeMat(lit, "AVM_Terrace",      new Color(0.76f, 0.73f, 0.67f), 0.38f);
            _facadeStone    = MakeMat(lit, "AVM_Stone",        new Color(0.84f, 0.81f, 0.75f), 0.28f);
            _darkFrame      = MakeMat(lit, "AVM_DarkFrame",    new Color(0.11f, 0.14f, 0.19f), 0.65f, 0.35f);
            _storeWallLight = MakeMat(lit, "AVM_StoreWall",    new Color(0.94f, 0.93f, 0.90f), 0.22f);
            _cinemaDarkWall = MakeMat(lit, "AVM_CinemaWall",   new Color(0.08f, 0.08f, 0.12f), 0.18f);
            _asphaltMat     = MakeMat(lit, "AVM_Asphalt",      new Color(0.18f, 0.19f, 0.21f), 0.16f);
            _whiteLineMat   = MakeMat(lit, "AVM_Line",         new Color(0.94f, 0.94f, 0.92f), 0.25f);
            _woodWarm       = MakeMat(lit, "AVM_Wood",         new Color(0.58f, 0.39f, 0.22f), 0.35f);
            _woodDark       = MakeMat(lit, "AVM_WoodDark",     new Color(0.28f, 0.18f, 0.11f), 0.38f);
            _tealAccent     = MakeMat(lit, "AVM_Teal",         new Color(0.10f, 0.58f, 0.56f), 0.55f);
            _goldAccent     = MakeMat(lit, "AVM_Gold",         new Color(0.90f, 0.68f, 0.20f), 0.75f, 0.55f);
            _coralRed       = MakeMat(lit, "AVM_Coral",        new Color(0.80f, 0.20f, 0.18f), 0.45f);
            _cinemaRed      = MakeMat(lit, "AVM_CinemaRed",    new Color(0.62f, 0.10f, 0.14f), 0.22f);
            _foliageMat     = MakeMat(lit, "AVM_Foliage",      new Color(0.20f, 0.48f, 0.26f), 0.20f);
            _waterMat       = MakeMat(lit, "AVM_Water",        new Color(0.14f, 0.68f, 0.82f), 0.88f);

            _brandGreen  = MakeMat(lit, "AVM_BrandGreen",  new Color(0.06f, 0.42f, 0.26f), 0.45f);
            _brandOrange = MakeMat(lit, "AVM_BrandOrange", new Color(0.92f, 0.46f, 0.12f), 0.45f);
            _brandBlue   = MakeMat(lit, "AVM_BrandBlue",   new Color(0.12f, 0.34f, 0.68f), 0.48f);
            _brandLilac  = MakeMat(lit, "AVM_BrandLilac",  new Color(0.60f, 0.22f, 0.64f), 0.48f);
            _brandBrown  = MakeMat(lit, "AVM_BrandBrown",  new Color(0.38f, 0.20f, 0.12f), 0.40f);
            _brandRose   = MakeMat(lit, "AVM_BrandRose",   new Color(0.82f, 0.56f, 0.54f), 0.42f);
            _brandYellow = MakeMat(lit, "AVM_BrandYellow", new Color(0.94f, 0.74f, 0.12f), 0.52f);

            _glassCurtain = MakeTransparentGlass(lit, "AVM_GlassCurtain", new Color(0.32f, 0.58f, 0.72f, 0.40f));
            _glassRailing = MakeTransparentGlass(lit, "AVM_GlassRail",    new Color(0.56f, 0.82f, 0.90f, 0.30f));

            _neonTeal    = MakeEmissive(lit, "AVM_NeonTeal",    new Color(0.14f, 0.84f, 0.78f), 1.55f);
            _neonAmber   = MakeEmissive(lit, "AVM_NeonAmber",   new Color(1.00f, 0.76f, 0.24f), 1.70f);
            _ceilingGlow = MakeEmissive(lit, "AVM_CeilingGlow", new Color(1.00f, 0.95f, 0.86f), 1.25f);
            _screenGlow  = MakeEmissive(lit, "AVM_CinemaScr",   new Color(0.38f, 0.64f, 0.96f), 1.40f);
        }

        // =====================================================================
        // 1. FRONT PARKING PLAZA & ENTRANCE CANOPY (Petrol Yolu Cephesi)
        // =====================================================================
        private void BuildParkingAndEntryPlaza(Transform ext)
        {
            Box("ParkingAsphalt", ext, new Vector3(0f, 0.025f, -26.5f), new Vector3(38f, 0.05f, 21f), _asphaltMat, col: true, shadows: false);
            Box("DrivewayApron", ext, new Vector3(0f, 0.022f, -38.5f), new Vector3(14f, 0.044f, 5f), _asphaltMat, col: false, shadows: false);
            Box("EastWalkwayApron", ext, new Vector3(21.5f, 0.03f, 0f), new Vector3(9f, 0.06f, 8f), _terraceTile, col: false, shadows: false);

            Box("EntryRedCarpetWalk", ext, new Vector3(0f, 0.045f, -25.5f), new Vector3(8.4f, 0.06f, 19f), _terraceTile, col: false, shadows: false);
            Box("EntryAccentStripL", ext, new Vector3(-4.0f, 0.06f, -25.5f), new Vector3(0.28f, 0.04f, 18.5f), _neonTeal, col: false, shadows: false);
            Box("EntryAccentStripR", ext, new Vector3( 4.0f, 0.06f, -25.5f), new Vector3(0.28f, 0.04f, 18.5f), _neonTeal, col: false, shadows: false);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int s = 0; s < 5; s++)
                {
                    float px = side * (7.2f + s * 2.9f);
                    Box($"ParkLine_{side}_{s}", ext, new Vector3(px, 0.055f, -24.5f), new Vector3(0.14f, 0.02f, 5.4f), _whiteLineMat, col: false, shadows: false);
                }
            }

            Box("SouthCanopyRoof", ext, new Vector3(0f, 4.35f, -18.5f), new Vector3(11.6f, 0.34f, 5.2f), _darkFrame, col: false);
            Box("SouthCanopyNeon", ext, new Vector3(0f, 4.18f, -21.12f), new Vector3(11.2f, 0.14f, 0.12f), _neonAmber, col: false, shadows: false);
            Box("SouthCanopyPillarL", ext, new Vector3(-5.2f, 2.15f, -20.6f), new Vector3(0.45f, 4.3f, 0.45f), _darkFrame, col: true);
            Box("SouthCanopyPillarR", ext, new Vector3( 5.2f, 2.15f, -20.6f), new Vector3(0.45f, 4.3f, 0.45f), _darkFrame, col: true);
            CreateSignText("HAMİDİYE ATLAS AVM", ext, new Vector3(0f, 4.95f, -21.15f), Quaternion.identity, 0.115f, new Color(1f, 0.90f, 0.55f));

            Box("RoofSignBoard_S", ext, new Vector3(0f, 20.6f, -16.15f), new Vector3(17.5f, 2.6f, 0.4f), _darkFrame, col: false);
            Box("RoofSignGlow_S",  ext, new Vector3(0f, 19.22f,-16.25f), new Vector3(17.5f, 0.18f, 0.18f), _neonTeal, col: false, shadows: false);
            CreateSignText("ATLAS AVM · 5 KAT", ext, new Vector3(0f, 20.6f, -16.42f), Quaternion.identity, 0.175f, new Color(1f, 0.88f, 0.44f));

            Box("RoofSignBoard_N", ext, new Vector3(0f, 20.6f, 16.15f), new Vector3(17.5f, 2.6f, 0.4f), _darkFrame, col: false);
            CreateSignText("ATLAS AVM · 5 KAT", ext, new Vector3(0f, 20.6f, 16.42f), Quaternion.Euler(0f, 180f, 0f), 0.175f, new Color(1f, 0.88f, 0.44f));

            float[] planterX = { -8.5f, 8.5f, -14.5f, 14.5f };
            for (int i = 0; i < planterX.Length; i++)
            {
                Vector3 bp = new Vector3(planterX[i], 0.35f, -18.2f);
                Box($"Planter_{i}", ext, bp, new Vector3(2.4f, 0.7f, 1.4f), _facadeStone, col: true, shadows: false);
                Sphere($"Shrub_{i}", ext, bp + new Vector3(0f, 0.72f, 0f), new Vector3(1.5f, 1.1f, 1.2f), _foliageMat, shadows: false);
            }
        }

        // =====================================================================
        // 2. 5-LEVEL ARCHITECTURAL SHELL, GLASS CURTAIN WALLS & FLOOR SLABS
        // =====================================================================
        private void BuildBuildingShellAndFloors(Transform ext, Transform interior)
        {
            Box("Slab_Floor0", ext, new Vector3(0f, 0.02f, 0f), new Vector3(HalfW * 2f, 0.12f, HalfD * 2f), _marbleFloor, col: true, shadows: false);

            for (int f = 1; f <= 4; f++)
            {
                float yTop = f * FloorH;
                float slabY = yTop - 0.14f;
                Material mat = (f == 4) ? _terraceTile : _marbleFloor;

                Box($"Slab_F{f}_S", ext, new Vector3(0f, slabY, -11f), new Vector3(34f, 0.28f, 10f), mat, col: true, shadows: false);
                Box($"Slab_F{f}_N", ext, new Vector3(0f, slabY,  11f), new Vector3(34f, 0.28f, 10f), mat, col: true, shadows: false);
                Box($"Slab_F{f}_W", ext, new Vector3(-11.5f, slabY, 0f), new Vector3(11f, 0.28f, 12f), mat, col: true, shadows: false);
                Box($"Slab_F{f}_E", ext, new Vector3( 11.5f, slabY, 0f), new Vector3(11f, 0.28f, 12f), mat, col: true, shadows: false);

                Box($"Fascia_F{f}_S", ext, new Vector3(0f, yTop - 0.18f, -HalfD - 0.12f), new Vector3(34.5f, 0.58f, 0.26f), _facadeStone, col: false, shadows: false);
                Box($"Fascia_F{f}_N", ext, new Vector3(0f, yTop - 0.18f,  HalfD + 0.12f), new Vector3(34.5f, 0.58f, 0.26f), _facadeStone, col: false, shadows: false);
                Box($"Fascia_F{f}_W", ext, new Vector3(-HalfW - 0.12f, yTop - 0.18f, 0f), new Vector3(0.26f, 0.58f, 32.5f), _facadeStone, col: false, shadows: false);
                Box($"Fascia_F{f}_E", ext, new Vector3( HalfW + 0.12f, yTop - 0.18f, 0f), new Vector3(0.26f, 0.58f, 32.5f), _facadeStone, col: false, shadows: false);

                if (f == 2 || f == 4)
                {
                    Box($"LedBand_F{f}_S", ext, new Vector3(0f, yTop + 0.14f, -HalfD - 0.22f), new Vector3(34.2f, 0.10f, 0.08f), f == 4 ? _neonTeal : _neonAmber, col: false, shadows: false);
                }

                // Bright mall promenade ceiling strip lights under Floors 1..4
                float ceilLightY = yTop - 0.30f;
                Box($"PromLight_F{f}_W", interior, new Vector3(-8.0f, ceilLightY, 0f), new Vector3(0.45f, 0.06f, 16f), _ceilingGlow, col: false, shadows: false);
                Box($"PromLight_F{f}_E", interior, new Vector3( 8.0f, ceilLightY, 0f), new Vector3(0.45f, 0.06f, 16f), _ceilingGlow, col: false, shadows: false);
                Box($"PromLight_F{f}_N", interior, new Vector3(0f, ceilLightY,  8.0f), new Vector3(16f, 0.06f, 0.45f), _ceilingGlow, col: false, shadows: false);
                Box($"PromLight_F{f}_S", interior, new Vector3(0f, ceilLightY, -8.0f), new Vector3(16f, 0.06f, 0.45f), _ceilingGlow, col: false, shadows: false);

                BuildAtriumRailingsForFloor(interior, f, yTop);
            }

            float enclosedH = 4 * FloorH;
            float wallCenterY = enclosedH * 0.5f;

            foreach (float sx in new[] { -HalfW, HalfW })
            {
                foreach (float sz in new[] { -HalfD, HalfD })
                {
                    Box($"CornerTower_{sx}_{sz}", ext, new Vector3(sx, wallCenterY, sz), new Vector3(2.2f, enclosedH + 1.2f, 2.2f), _facadeStone, col: true);
                }
            }

            Box("Wall_North_Glass", ext, new Vector3(0f, wallCenterY, HalfD), new Vector3(32f, enclosedH, 0.35f), _glassCurtain, col: true, shadows: false);
            Box("Wall_West_Glass",  ext, new Vector3(-HalfW, wallCenterY, 0f), new Vector3(0.35f, enclosedH, 30f), _glassCurtain, col: true, shadows: false);

            Box("Wall_South_Left",        ext, new Vector3(-10.2f, wallCenterY, -HalfD), new Vector3(13.6f, enclosedH, 0.35f), _glassCurtain, col: true, shadows: false);
            Box("Wall_South_Right",       ext, new Vector3( 10.2f, wallCenterY, -HalfD), new Vector3(13.6f, enclosedH, 0.35f), _glassCurtain, col: true, shadows: false);
            Box("Wall_South_UpperCenter", ext, new Vector3(0f, (FloorH + enclosedH) * 0.5f, -HalfD), new Vector3(6.8f, enclosedH - FloorH, 0.35f), _glassCurtain, col: true, shadows: false);

            Box("Wall_East_South",       ext, new Vector3(HalfW, wallCenterY, -9.3f), new Vector3(0.35f, enclosedH, 13.4f), _glassCurtain, col: true, shadows: false);
            Box("Wall_East_North",       ext, new Vector3(HalfW, wallCenterY,  9.3f), new Vector3(0.35f, enclosedH, 13.4f), _glassCurtain, col: true, shadows: false);
            Box("Wall_East_UpperCenter", ext, new Vector3(HalfW, (FloorH + enclosedH) * 0.5f, 0f), new Vector3(0.35f, enclosedH - FloorH, 5.2f), _glassCurtain, col: true, shadows: false);

            for (float mx = -12f; mx <= 12f; mx += 6f)
            {
                if (Mathf.Abs(mx) < 1f) continue;
                Box($"Mullion_S_{mx}", ext, new Vector3(mx, wallCenterY, -HalfD - 0.16f), new Vector3(0.32f, enclosedH, 0.32f), _darkFrame, col: false, shadows: false);
                Box($"Mullion_N_{mx}", ext, new Vector3(mx, wallCenterY,  HalfD + 0.16f), new Vector3(0.32f, enclosedH, 0.32f), _darkFrame, col: false, shadows: false);
            }

            float roofY = 4 * FloorH;
            Box("RoofRail_S", ext, new Vector3(0f, roofY + 0.62f, -HalfD + 0.15f), new Vector3(33.4f, 1.24f, 0.18f), _glassRailing, col: true, shadows: false);
            Box("RoofRail_N", ext, new Vector3(0f, roofY + 0.62f,  HalfD - 0.15f), new Vector3(33.4f, 1.24f, 0.18f), _glassRailing, col: true, shadows: false);
            Box("RoofRail_W", ext, new Vector3(-HalfW + 0.15f, roofY + 0.62f, 0f), new Vector3(0.18f, 1.24f, 31.4f), _glassRailing, col: true, shadows: false);
            Box("RoofRail_E", ext, new Vector3( HalfW - 0.15f, roofY + 0.62f, 0f), new Vector3(0.18f, 1.24f, 31.4f), _glassRailing, col: true, shadows: false);

            float skyY = roofY + 4.4f;
            foreach (float sx in new[] { -6.1f, 6.1f })
            {
                foreach (float sz in new[] { -6.1f, 6.1f })
                {
                    Box($"SkylightPost_{sx}_{sz}", ext, new Vector3(sx, roofY + 2.2f, sz), new Vector3(0.38f, 4.4f, 0.38f), _darkFrame, col: true, shadows: false);
                }
            }
            Box("SkylightGlassTop", ext, new Vector3(0f, skyY, 0f), new Vector3(13.2f, 0.18f, 13.2f), _glassCurtain, col: false, shadows: false);
            Box("SkylightRimS", ext, new Vector3(0f, skyY, -6.5f), new Vector3(13.4f, 0.36f, 0.32f), _darkFrame, col: false, shadows: false);
            Box("SkylightRimN", ext, new Vector3(0f, skyY,  6.5f), new Vector3(13.4f, 0.36f, 0.32f), _darkFrame, col: false, shadows: false);
            Box("SkylightRimW", ext, new Vector3(-6.5f, skyY, 0f), new Vector3(0.32f, 0.36f, 13.4f), _darkFrame, col: false, shadows: false);
            Box("SkylightRimE", ext, new Vector3( 6.5f, skyY, 0f), new Vector3(0.32f, 0.36f, 13.4f), _darkFrame, col: false, shadows: false);
        }

        private void BuildAtriumRailingsForFloor(Transform parent, int floor, float yTop)
        {
            float ry = yTop + 0.58f;
            float rh = 1.16f;

            Box($"Rail_F{floor}_W", parent, new Vector3(-AtriumHalf, ry, 0f), new Vector3(0.14f, rh, 12.1f), _glassRailing, col: true, shadows: false);
            Box($"Rail_F{floor}_E", parent, new Vector3( AtriumHalf, ry, 0f), new Vector3(0.14f, rh, 12.1f), _glassRailing, col: true, shadows: false);

            bool northEscalatorsOpen = (floor == 1 || floor == 3);
            if (northEscalatorsOpen)
            {
                Box($"Rail_F{floor}_NL", parent, new Vector3(-2.4f, ry, AtriumHalf), new Vector3(1.8f, rh, 0.14f), _glassRailing, col: true, shadows: false);
                Box($"Rail_F{floor}_NR", parent, new Vector3( 2.4f, ry, AtriumHalf), new Vector3(1.8f, rh, 0.14f), _glassRailing, col: true, shadows: false);
            }
            else
            {
                Box($"Rail_F{floor}_NL", parent, new Vector3(-3.7f, ry, AtriumHalf), new Vector3(4.6f, rh, 0.14f), _glassRailing, col: true, shadows: false);
                Box($"Rail_F{floor}_NR", parent, new Vector3( 3.7f, ry, AtriumHalf), new Vector3(4.6f, rh, 0.14f), _glassRailing, col: true, shadows: false);
            }

            if (floor == 2)
            {
                Box($"Rail_F{floor}_SC", parent, new Vector3(0f, ry, -AtriumHalf), new Vector3(6.8f, rh, 0.14f), _glassRailing, col: true, shadows: false);
            }
            else if (floor == 4)
            {
                Box($"Rail_F{floor}_SL", parent, new Vector3(-1.3f, ry, -AtriumHalf), new Vector3(9.4f, rh, 0.14f), _glassRailing, col: true, shadows: false);
            }
            else
            {
                Box($"Rail_F{floor}_S", parent, new Vector3(0f, ry, -AtriumHalf), new Vector3(12.1f, rh, 0.14f), _glassRailing, col: true, shadows: false);
            }
        }

        // =====================================================================
        // 3. ESCALATORS + PANORAMIC GLASS ELEVATOR
        // =====================================================================
        private void BuildEscalatorsAndElevator(Transform interior)
        {
            BuildSingleEscalator(interior, "Escalator_0_to_1", x: -4.6f, yStart: 0f * FloorH, yEnd: 1f * FloorH, zStart: -6.1f, zEnd:  6.1f);
            BuildSingleEscalator(interior, "Escalator_1_to_2", x:  4.6f, yStart: 1f * FloorH, yEnd: 2f * FloorH, zStart:  6.1f, zEnd: -6.1f);
            BuildSingleEscalator(interior, "Escalator_2_to_3", x: -4.6f, yStart: 2f * FloorH, yEnd: 3f * FloorH, zStart: -6.1f, zEnd:  6.1f);
            BuildSingleEscalator(interior, "Escalator_3_to_4", x:  4.6f, yStart: 3f * FloorH, yEnd: 4f * FloorH, zStart:  6.1f, zEnd: -6.1f);

            Transform elevRoot = new GameObject("Panoramic_Elevator").transform;
            elevRoot.SetParent(interior, false);
            elevRoot.localPosition = new Vector3(0f, 0f, 4.65f);

            foreach (float gx in new[] { -1.32f, 1.32f })
            {
                foreach (float gz in new[] { -1.22f, 1.22f })
                {
                    Box($"ShaftRail_{gx}_{gz}", elevRoot, new Vector3(gx, 11.1f, gz), new Vector3(0.14f, 22.2f, 0.14f), _darkFrame, col: false, shadows: false);
                }
            }

            Transform cabin = new GameObject("ElevatorCabin").transform;
            cabin.SetParent(elevRoot, false);
            cabin.localPosition = Vector3.zero;
            Box("CabinFloor",  cabin, new Vector3(0f, 0.08f, 0f), new Vector3(2.48f, 0.16f, 2.30f), _darkFrame, col: true, shadows: false);
            Box("CabinRoof",   cabin, new Vector3(0f, 2.72f, 0f), new Vector3(2.48f, 0.16f, 2.30f), _darkFrame, col: false, shadows: false);
            Box("CabinGlow",   cabin, new Vector3(0f, 2.60f, 0f), new Vector3(2.0f, 0.06f, 1.8f), _neonAmber, col: false, shadows: false);
            Box("CabinGlassS", cabin, new Vector3(0f, 1.40f, -1.12f), new Vector3(2.42f, 2.48f, 0.08f), _glassRailing, col: false, shadows: false);
            Box("CabinGlassW", cabin, new Vector3(-1.20f, 1.40f, 0f), new Vector3(0.08f, 2.48f, 2.24f), _glassRailing, col: false, shadows: false);
            Box("CabinGlassE", cabin, new Vector3( 1.20f, 1.40f, 0f), new Vector3(0.08f, 2.48f, 2.24f), _glassRailing, col: false, shadows: false);

            _elevator = elevRoot.gameObject.AddComponent<MallElevatorController>();
            _elevator.CabinTransform = cabin;
            _elevator.FloorHeights = new[] { 0f, FloorH, 2f * FloorH, 3f * FloorH, 4f * FloorH };
            _elevator.LandingOffsetLocal = new Vector3(0f, 0.08f, 2.55f);

            for (int f = 0; f <= 4; f++)
            {
                float fy = f * FloorH;
                GameObject panel = Box($"ElevCallPanel_F{f}", interior, new Vector3(1.85f, fy + 1.25f, 6.25f), new Vector3(0.36f, 0.62f, 0.16f), _darkFrame, col: false, shadows: false);
                Box($"ElevCallBtnGlow_F{f}", panel.transform, new Vector3(0f, 0.05f, 0.09f), new Vector3(0.18f, 0.24f, 0.04f), _neonTeal, col: false, shadows: false);

                InteractableTrigger trig = panel.AddComponent<InteractableTrigger>();
                trig.Configure(InteractableKind.InspectObject, $"mall_elevator_{f}", "ui.btn.interact", 2.8f);
                trig.SetCustomPrompt($"Panoramik Asansör (Kat {f} — Kat Seç)", $"Panoramic Elevator (Floor {f} — Select Level)");
            }
        }

        private void BuildSingleEscalator(Transform parent, string name, float x, float yStart, float yEnd, float zStart, float zEnd)
        {
            Vector3 p0 = new Vector3(x, yStart, zStart);
            Vector3 p1 = new Vector3(x, yEnd, zEnd);
            Vector3 delta = p1 - p0;
            float len = delta.magnitude;
            Vector3 mid = (p0 + p1) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(delta.normalized, Vector3.up);

            Transform esc = new GameObject(name).transform;
            esc.SetParent(parent, false);
            esc.localPosition = mid;
            esc.localRotation = rot;

            Box("RampDeck", esc, new Vector3(0f, -0.14f, 0f), new Vector3(2.15f, 0.28f, len + 0.55f), _darkFrame, col: true, shadows: false);
            GameObject strip = Box("MovingStrip", esc, new Vector3(0f, 0.02f, 0f), new Vector3(1.55f, 0.04f, len - 0.6f), _neonTeal, col: false, shadows: false);
            _escalatorStrips.Add(strip.transform);

            foreach (float side in new[] { -1.04f, 1.04f })
            {
                Box($"GlassSide_{side}", esc, new Vector3(side, 0.54f, 0f), new Vector3(0.08f, 1.05f, len), _glassRailing, col: true, shadows: false);
                Box($"Handrail_{side}",  esc, new Vector3(side, 1.08f, 0f), new Vector3(0.14f, 0.08f, len + 0.25f), _darkFrame, col: false, shadows: false);
            }

            GameObject zoneGo = new GameObject("EscalatorTrigger");
            zoneGo.transform.SetParent(esc, false);
            zoneGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            BoxCollider bc = zoneGo.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(1.9f, 1.8f, len - 0.3f);
            MallEscalatorZone zone = zoneGo.AddComponent<MallEscalatorZone>();
            zone.UpDirection = delta.normalized;
            zone.AssistSpeed = 1.65f;
        }

        // =====================================================================
        // SHARED WALK-IN GLASS STOREFRONT ARCHITECTURAL BUILDER
        // =====================================================================
        /// <summary>
        /// Builds a real walk-in glass-fronted retail room inside the mall:
        /// - Local +Z faces the mall corridor (wide 3.4m open entrance doorway + flanking glass display windows!)
        /// - Side partition walls, illuminated brand fascia header, warm interior ceiling light,
        ///   interior floor mat, checkout counter with register & monitor, interactive shop trigger, and staff NPC!
        /// </summary>
        private Transform BuildWalkInStore(
            Transform parent,
            string storeObjName,
            Vector3 localPos,
            float facingYaw,
            float width,
            float depth,
            Material brandMat,
            Material interiorFloorMat,
            string signText,
            string interactId,
            string promptTr,
            string promptEn,
            string staffName,
            Color staffUniform,
            string[] staffLines,
            bool darkInterior = false,
            bool counterAtBack = true)
        {
            Transform store = new GameObject(storeObjName).transform;
            store.SetParent(parent, false);
            store.localPosition = localPos;
            store.localRotation = Quaternion.Euler(0f, facingYaw, 0f);

            float hw = width * 0.5f;
            float hd = depth * 0.5f;
            const float h = 4.25f;
            float doorHalf = Mathf.Min(1.75f, hw - 0.85f);
            Material wallMat = darkInterior ? _cinemaDarkWall : _storeWallLight;

            // Interior flooring inlay (slightly raised 0.03m over marble slab)
            Box("FloorInlay", store, new Vector3(0f, 0.03f, 0f), new Vector3(width - 0.12f, 0.04f, depth - 0.12f), interiorFloorMat, col: false, shadows: false);

            // Side partition walls (solid so each shop is its own real room!)
            Box("SideWall_L", store, new Vector3(-hw, h * 0.5f, 0f), new Vector3(0.16f, h, depth), wallMat, col: true, shadows: false);
            Box("SideWall_R", store, new Vector3( hw, h * 0.5f, 0f), new Vector3(0.16f, h, depth), wallMat, col: true, shadows: false);

            // Glass storefront windows on left & right of wide open doorway (local +Z edge)
            float winW = hw - doorHalf;
            if (winW > 0.35f)
            {
                float winCx = doorHalf + winW * 0.5f;
                Box("FrontGlass_L", store, new Vector3(-winCx, 1.55f, hd), new Vector3(winW, 3.1f, 0.10f), _glassRailing, col: true, shadows: false);
                Box("FrontGlass_R", store, new Vector3( winCx, 1.55f, hd), new Vector3(winW, 3.1f, 0.10f), _glassRailing, col: true, shadows: false);
                Box("FrontBase_L",  store, new Vector3(-winCx, 0.14f, hd), new Vector3(winW, 0.28f, 0.16f), _darkFrame, col: false, shadows: false);
                Box("FrontBase_R",  store, new Vector3( winCx, 0.14f, hd), new Vector3(winW, 0.28f, 0.16f), _darkFrame, col: false, shadows: false);
            }

            // Illuminated Brand Fascia Sign above storefront (+Z facing corridor!)
            Box("StoreFascia",   store, new Vector3(0f, 3.68f, hd + 0.08f), new Vector3(width + 0.1f, 1.05f, 0.24f), brandMat, col: false, shadows: false);
            Box("StoreTrimLine", store, new Vector3(0f, 3.14f, hd + 0.14f), new Vector3(width + 0.1f, 0.09f, 0.10f), _neonAmber, col: false, shadows: false);
            CreateSignText(signText, store, new Vector3(0f, 3.68f, hd + 0.24f), Quaternion.Euler(0f, 180f, 0f), width >= 9f ? 0.072f : 0.058f, Color.white);

            // Warm interior ceiling LED panel
            Box("InteriorLight", store, new Vector3(0f, h - 0.12f, 0f), new Vector3(width * 0.62f, 0.06f, 0.55f), darkInterior ? _neonAmber : _ceilingGlow, col: false, shadows: false);

            // Checkout / Service Counter + POS Register Monitor + Interactive Trigger
            if (!string.IsNullOrEmpty(interactId))
            {
                float counterZ = counterAtBack ? (-hd + 1.55f) : (hd - 1.35f);
                float counterW = Mathf.Clamp(width * 0.44f, 2.4f, 4.4f);
                GameObject counter = Box("ServiceCounter", store, new Vector3(0f, 0.54f, counterZ), new Vector3(counterW, 1.08f, 0.92f), brandMat, col: true, shadows: false);
                Box("CounterTop",  counter.transform, new Vector3(0f, 0.57f, 0f), new Vector3(counterW + 0.14f, 0.08f, 1.08f), _marbleFloor, col: false, shadows: false);
                Box("PosMonitor",  counter.transform, new Vector3(counterW * 0.25f, 0.80f, 0.08f), new Vector3(0.42f, 0.34f, 0.08f), _darkFrame, col: false, shadows: false);
                Box("PosScreen",   counter.transform, new Vector3(counterW * 0.25f, 0.80f, 0.13f), new Vector3(0.36f, 0.26f, 0.03f), _neonTeal, col: false, shadows: false);

                InteractableTrigger trig = counter.AddComponent<InteractableTrigger>();
                trig.Configure(InteractableKind.InspectObject, interactId, "ui.btn.interact", 3.3f);
                trig.SetCustomPrompt(promptTr, promptEn);

                // Uniformed Store Staff NPC standing behind the counter!
                if (!string.IsNullOrEmpty(staffName))
                {
                    Vector3 staffPos = new Vector3(-counterW * 0.18f, 0.05f, counterZ - 0.88f);
                    SpawnMallNpc(store, staffPos, 0f, staffName, signText, signText, staffUniform, staffLines);
                }
            }

            return store;
        }

        private ProceduralHumanoid SpawnMallNpc(
            Transform parent,
            Vector3 localPos,
            float localYaw,
            string displayName,
            string roleTr,
            string roleEn,
            Color shirtColor,
            string[] lines)
        {
            ProceduralHumanoid tpl = PlayerModeController.Instance?.FootController?.Humanoid;
            if (tpl == null) return null;

            int idNum = _npcSerial++;
            string npcId = $"mall_npc_{idNum:00}";
            ProceduralHumanoid hum = Instantiate(tpl, parent);
            hum.name = $"MallNPC_{idNum:00}_{displayName}";
            hum.transform.localPosition = localPos;
            hum.transform.localRotation = Quaternion.Euler(0f, localYaw, 0f);
            hum.SetLocomotion(0f, true, 0f);

            Collider[] cols = hum.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < cols.Length; c++) Destroy(cols[c]);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            if (lit != null)
            {
                Material sm = MakeMat(lit, $"MallNpcShirt_{idNum}", shirtColor, 0.3f);
                Renderer[] rends = hum.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rends.Length; r++)
                {
                    string rn = rends[r].name.ToLowerInvariant();
                    if (rn.Contains("chest") || rn.Contains("torso") || rn.Contains("shirt") || rn.Contains("upperarm"))
                    {
                        rends[r].sharedMaterial = sm;
                    }
                }
            }

            InteractableTrigger trig = hum.gameObject.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.TownCitizen, npcId, "ui.btn.chat", 2.75f);

            if (_handler != null)
            {
                _handler.NpcProfiles[npcId] = new MallNpcProfile
                {
                    DisplayName = displayName,
                    RoleTr = roleTr,
                    RoleEn = roleEn,
                    Lines = lines ?? new[] { "Hamidiye Atlas AVM'ye hoş geldin komşu!" }
                };
            }
            return hum;
        }

        // =====================================================================
        // FURNITURE & PRIMITIVE HELPERS
        // =====================================================================
        private void BuildMallBench(Transform parent, string name, Vector3 localPos, float yaw)
        {
            Transform bench = new GameObject(name).transform;
            bench.SetParent(parent, false);
            bench.localPosition = localPos;
            bench.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Box("SeatPlank", bench, new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.12f, 0.65f), _woodWarm, col: true, shadows: false);
            Box("BackRest",  bench, new Vector3(0f, 0.82f, -0.28f), new Vector3(2.2f, 0.56f, 0.10f), _woodWarm, col: false, shadows: false);
            Box("LegL",      bench, new Vector3(-0.92f, 0.22f, 0f), new Vector3(0.12f, 0.44f, 0.56f), _darkFrame, col: false, shadows: false);
            Box("LegR",      bench, new Vector3( 0.92f, 0.22f, 0f), new Vector3(0.12f, 0.44f, 0.56f), _darkFrame, col: false, shadows: false);

            GameObject sp = new GameObject("SitPoint");
            sp.transform.SetParent(bench, false);
            sp.transform.localPosition = new Vector3(0f, 0.51f, 0.05f);
            sp.transform.localRotation = Quaternion.identity;

            InteractableTrigger trig = bench.gameObject.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.Seat, $"seat_{name}", "ui.btn.sit", 2.4f);
            bench.gameObject.AddComponent<SeatInteractable>().Configure(sp.transform, trig);
        }

        private void BuildDiningSet(Transform parent, string name, Vector3 localPos, Material chairMat = null)
        {
            Transform set = new GameObject(name).transform;
            set.SetParent(parent, false);
            set.localPosition = localPos;

            Box("TableTop", set, new Vector3(0f, 0.78f, 0f), new Vector3(1.38f, 0.08f, 1.38f), _woodWarm, col: true, shadows: false);
            Cylinder("TableLeg", set, new Vector3(0f, 0.39f, 0f), new Vector3(0.16f, 0.39f, 0.16f), _darkFrame, col: false, shadows: false);

            Material cm = chairMat ?? _tealAccent;
            GameObject chair = Box("ChairSeat", set, new Vector3(0f, 0.44f, 0.96f), new Vector3(0.54f, 0.08f, 0.54f), cm, col: true, shadows: false);
            Box("ChairBack", chair.transform, new Vector3(0f, 0.32f, 0.23f), new Vector3(0.52f, 0.54f, 0.08f), cm, col: false, shadows: false);

            GameObject sp = new GameObject("SitPoint");
            sp.transform.SetParent(chair.transform, false);
            sp.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            sp.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            InteractableTrigger trig = chair.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.Seat, $"seat_{name}", "ui.btn.sit", 2.2f);
            chair.AddComponent<SeatInteractable>().Configure(sp.transform, trig);
        }

        private void BuildCinemaSeat(Transform parent, string name, Vector3 localPos)
        {
            Transform seat = new GameObject(name).transform;
            seat.SetParent(parent, false);
            seat.localPosition = localPos;
            seat.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Box("Cushion", seat, new Vector3(0f, 0.42f, 0f), new Vector3(0.90f, 0.22f, 0.74f), _cinemaRed, col: true, shadows: false);
            Box("Back",    seat, new Vector3(0f, 0.88f, -0.32f), new Vector3(0.90f, 0.82f, 0.18f), _cinemaRed, col: false, shadows: false);

            GameObject sp = new GameObject("SitPoint");
            sp.transform.SetParent(seat, false);
            sp.transform.localPosition = new Vector3(0f, 0.54f, 0.04f);
            sp.transform.localRotation = Quaternion.identity;

            InteractableTrigger trig = seat.gameObject.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.Seat, $"seat_{name}", "ui.btn.sit", 2.2f);
            seat.gameObject.AddComponent<SeatInteractable>().Configure(sp.transform, trig);
        }

        private GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool col = true, bool shadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (!col) Destroy(go.GetComponent<Collider>());
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (!shadows) mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private GameObject Cylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool col = true, bool shadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (!col) Destroy(go.GetComponent<Collider>());
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (!shadows) mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private GameObject Sphere(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool shadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (!shadows) mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static void CreateSignText(string text, Transform parent, Vector3 localPos, Quaternion localRot, float charSize, Color color)
        {
            GameObject go = new GameObject("Sign_" + text.Substring(0, Mathf.Min(12, text.Length)));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = charSize;
            tm.fontSize = 48;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = color;
        }

        private Material MakeMat(Shader sh, string name, Color col, float smoothness, float metallic = 0f)
        {
            Material m = new Material(sh) { name = name };
            _mats.Add(m);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return m;
        }

        private Material MakeEmissive(Shader sh, string name, Color col, float intensity)
        {
            Material m = MakeMat(sh, name, col, 0.6f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", col * intensity);
            }
            return m;
        }

        private Material MakeTransparentGlass(Shader sh, string name, Color col)
        {
            Material m = MakeMat(sh, name, col, 0.92f, 0.1f);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }
    }
}
