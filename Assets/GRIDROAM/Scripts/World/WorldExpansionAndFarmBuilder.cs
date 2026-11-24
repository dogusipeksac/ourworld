using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Expands & beautifies the Our World open-world map at runtime (zero scene rebuild required!):
    /// 1. Expands the lush valley ground to 660x660 m + rings the 360-degree horizon with 28 rolling green Aydos Hills
    ///    and hilltop pine silhouettes (eliminates flat empty horizon edges!).
    /// 2. Builds EAST EXPANSION: Aydos Yeşil Vadi · Çiftlik & Bostan (country road from Selman St., wooden archway,
    ///    12 raised soil farm plots, stone water well, rotating windmill, red timber barn + Cow Pasture,
    ///    Chicken Coop & Run, Sheep Pen + Anatolian shepherd dog Karabaş, Village Market Stand, Farm Management Board,
    ///    tea pergola, tractor decor, and warm evening farm lanterns).
    /// 3. Builds HOME GARDEN BOSTAN (4 starter plots right beside Deniz's No:48 house!).
    /// 4. Builds WEST EXPANSION: Sultanbeyli Göleti & Aydos Çam Korusu (shimmering turquoise lake, lily pads & lotuses,
    ///    3 animated gliding ducks, wooden viewing pier with benches & lanterns, pine/blossom grove & picnic gazebos).
    /// 5. Beautifies existing Hamidiye plazas & avenues with spring cherry-blossom trees, flowerbeds, cypresses & signposts.
    /// </summary>
    public sealed class WorldExpansionAndFarmBuilder : MonoBehaviour
    {
        public static WorldExpansionAndFarmBuilder Instance { get; private set; }

        private Material _meadowMat;
        private Material _hillNearMat;
        private Material _hillFarMat;
        private Material _roadCountryMat;
        private Material _stonePathMat;
        private Material _woodTimberMat;
        private Material _woodLightMat;
        private Material _barnRedMat;
        private Material _roofTileMat;
        private Material _whiteTrimMat;
        private Material _drySoilMat;
        private Material _wetSoilMat;
        private Material _hayGoldMat;
        private Material _pineDarkMat;
        private Material _leafBrightMat;
        private Material _blossomPinkMat;
        private Material _flowerRedMat;
        private Material _flowerYellowMat;
        private Material _flowerPurpleMat;
        private Material _lakeWaterMat;
        private Material _sandShoreMat;
        private Material _tractorRedMat;
        private Material _darkMetalMat;

        private Transform _windmillBlades;
        private Transform _lakeSurface;
        private readonly List<(Transform duck, SkeletalClipPlayer skel, Vector3 center, float radius, float speed, float phase)> _ducks = new List<(Transform, SkeletalClipPlayer, Vector3, float, float, float)>();
        private readonly List<Transform> _sprinklerHeads = new List<Transform>();
        private Transform _shepherdDog;
        private SkeletalClipPlayer _shepherdSkel;
        private Transform _shepherdHead;
        private Transform _shepherdTail;
        private readonly List<Transform> _shepherdLegs = new List<Transform>(4);
        private Vector3 _dogHome;
        private float _dogPhase;
        private float _dogWalkPhase;

        private Transform _farmRoot;
        private int _spawnedChickens;
        private int _spawnedSheep;
        private int _spawnedCows;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_ExpandedValleyAndFarm");
            go.AddComponent<WorldExpansionAndFarmBuilder>();
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
            ClearLegacyTightPerimeterWalls();
            BuildMaterials();
            BuildValleyGroundAndAydosHills();
            BuildHomeGardenBostan();
            BuildEastAydosFarmValley();
            BuildWestSultanbeyliLakeAndGrove();
            BeautifyHamidiyePlazasAndAvenues();

            FarmEconomyManager.OnExtraAnimalPurchased += HandleExtraAnimalPurchased;
            FarmEconomyManager.OnAutoSprinklerUnlocked += HandleSprinklerUnlocked;

            // Refresh PlayerModeController interactable cache so every new plot, animal & stand is immediately interactive!
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }
        }

        private void OnDestroy()
        {
            FarmEconomyManager.OnExtraAnimalPurchased -= HandleExtraAnimalPurchased;
            FarmEconomyManager.OnAutoSprinklerUnlocked -= HandleSprinklerUnlocked;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_windmillBlades != null)
            {
                _windmillBlades.Rotate(0f, 0f, -26f * dt, Space.Self);
            }

            if (_lakeSurface != null)
            {
                Vector3 p = _lakeSurface.localPosition;
                p.y = 0.04f + Mathf.Sin(Time.time * 1.4f) * 0.018f;
                _lakeSurface.localPosition = p;
            }

            // Gliding & dabbling pond ducks (smooth buoyancy pitch/roll + periodic playful water head-dips!)
            for (int i = 0; i < _ducks.Count; i++)
            {
                var d = _ducks[i];
                if (d.duck == null) continue;
                float a = Time.time * d.speed + d.phase;
                float bob = Mathf.Sin(a * 3f) * 0.022f;
                Vector3 pos = d.center + new Vector3(Mathf.Cos(a) * d.radius, 0.09f + bob, Mathf.Sin(a) * d.radius * 0.72f);
                Vector3 nextPos = d.center + new Vector3(Mathf.Cos(a + 0.08f) * d.radius, 0.09f, Mathf.Sin(a + 0.08f) * d.radius * 0.72f);
                d.duck.position = pos;
                Vector3 dir = nextPos - pos;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    float dabbleCycle = Mathf.Repeat(Time.time * 0.22f + d.phase, 1f);
                    bool dabbling = dabbleCycle < 0.24f;
                    if (d.skel != null && d.skel.HasClips)
                    {
                        d.skel.PlayClip(dabbling ? "Dabble" : "Swim", 0.30f, 1.0f);
                        d.duck.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    }
                    else
                    {
                        float dipPitch = dabbling ? Mathf.Sin((dabbleCycle / 0.24f) * Mathf.PI) * 26f : Mathf.Sin(a * 2.4f) * 3.5f;
                        float roll = Mathf.Cos(a * 2.1f) * 5.5f;
                        d.duck.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(dipPitch, 0f, roll);
                    }
                }
            }

            // Friendly Anatolian shepherd dog Karabaş: patrols sheep pen & gallops/trots enthusiastically to greet Deniz!
            if (_shepherdDog != null)
            {
                _dogPhase += dt * 0.52f;
                Vector3 target = _dogHome + new Vector3(Mathf.Sin(_dogPhase) * 4.5f, 0f, Mathf.Cos(_dogPhase * 0.7f) * 2.2f);
                bool greetingPlayer = false;
                if (PlayerModeController.Instance != null)
                {
                    Vector3 pPos = PlayerModeController.Instance.ActiveWorldPosition;
                    Vector3 fromHome = pPos - _dogHome;
                    fromHome.y = 0f;
                    if (fromHome.sqrMagnitude < 14f * 14f)
                    {
                        greetingPlayer = true;
                        Vector3 greetSpot = _dogHome + Vector3.ClampMagnitude(fromHome, 6.5f);
                        Vector3 toP = pPos - greetSpot;
                        toP.y = 0f;
                        if (toP.magnitude < 1.8f) greetSpot = pPos - toP.normalized * 1.8f;
                        target = new Vector3(greetSpot.x, _dogHome.y, greetSpot.z);
                    }
                }

                Vector3 delta = target - _shepherdDog.position;
                delta.y = 0f;
                bool moving = delta.sqrMagnitude > 0.04f;
                if (moving)
                {
                    float dogSpd = greetingPlayer ? 2.65f : 1.50f;
                    _shepherdDog.rotation = Quaternion.Slerp(_shepherdDog.rotation, Quaternion.LookRotation(delta.normalized, Vector3.up), 1f - Mathf.Exp(-6.5f * dt));
                    _shepherdDog.position = Vector3.MoveTowards(_shepherdDog.position, target, dogSpd * dt);
                }
                else if (greetingPlayer && PlayerModeController.Instance != null)
                {
                    Vector3 lookP = PlayerModeController.Instance.ActiveWorldPosition - _shepherdDog.position;
                    lookP.y = 0f;
                    if (lookP.sqrMagnitude > 0.02f)
                    {
                        _shepherdDog.rotation = Quaternion.Slerp(_shepherdDog.rotation, Quaternion.LookRotation(lookP.normalized, Vector3.up), 1f - Mathf.Exp(-6f * dt));
                    }
                }

                if (_shepherdSkel != null && _shepherdSkel.HasClips)
                {
                    if (moving)
                    {
                        _shepherdSkel.PlayClip(greetingPlayer ? "Gallop" : "Walk", 0.22f, greetingPlayer ? 1.15f : 1.05f);
                    }
                    else
                    {
                        _shepherdSkel.PlayClip(greetingPlayer ? "Happy" : "Idle", 0.28f, 1.0f);
                    }
                }
                else
                {
                    if (moving)
                    {
                        _dogWalkPhase += dt * (greetingPlayer ? 13.5f : 9.2f);
                        float[] trotPhase = { 0f, 0.5f, 0.5f, 0f };
                        for (int i = 0; i < _shepherdLegs.Count; i++)
                        {
                            if (_shepherdLegs[i] == null) continue;
                            float ang = Mathf.Sin(_dogWalkPhase + trotPhase[i] * Mathf.PI * 2f) * (greetingPlayer ? 32f : 24f);
                            _shepherdLegs[i].localRotation = Quaternion.Euler(ang, 0f, 0f);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < _shepherdLegs.Count; i++)
                        {
                            if (_shepherdLegs[i] != null)
                                _shepherdLegs[i].localRotation = Quaternion.Slerp(_shepherdLegs[i].localRotation, Quaternion.identity, 1f - Mathf.Exp(-8f * dt));
                        }
                    }
                    if (_shepherdHead != null)
                    {
                        float headPitch = greetingPlayer ? -12f : Mathf.Sin(_dogWalkPhase * 0.5f) * 5f;
                        float headYaw = greetingPlayer ? Mathf.Sin(Time.time * 2.5f) * 8f : Mathf.Sin(Time.time * 1.4f) * 16f;
                        _shepherdHead.localRotation = Quaternion.Slerp(_shepherdHead.localRotation, Quaternion.Euler(headPitch, headYaw, greetingPlayer ? 8f : 0f), 1f - Mathf.Exp(-8f * dt));
                    }
                    if (_shepherdTail != null)
                    {
                        float wagFreq = greetingPlayer ? 16f : 8.5f;
                        float wagAmp = greetingPlayer ? 38f : 22f;
                        _shepherdTail.localRotation = Quaternion.Euler(greetingPlayer ? 22f : 12f, Mathf.Sin(Time.time * wagFreq) * wagAmp, 0f);
                    }
                }
            }

            // Spin auto-sprinklers if unlocked
            bool hasSprinkler = FarmEconomyManager.Data != null && FarmEconomyManager.Data.hasAutoSprinkler;
            if (hasSprinkler)
            {
                for (int i = 0; i < _sprinklerHeads.Count; i++)
                {
                    if (_sprinklerHeads[i] != null)
                    {
                        _sprinklerHeads[i].Rotate(0f, 180f * dt, 0f, Space.Self);
                    }
                }
            }
        }

        private void BuildMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _meadowMat = MakeMat(lit, "MAT_Exp_Meadow", new Color(0.24f, 0.45f, 0.19f), 0.10f);
            _hillNearMat = MakeMat(lit, "MAT_Exp_HillNear", new Color(0.20f, 0.40f, 0.18f), 0.08f);
            _hillFarMat = MakeMat(lit, "MAT_Exp_HillFar", new Color(0.17f, 0.34f, 0.22f), 0.06f);
            _roadCountryMat = MakeMat(lit, "MAT_Exp_CountryRoad", new Color(0.30f, 0.29f, 0.27f), 0.18f);
            _stonePathMat = MakeMat(lit, "MAT_Exp_StonePath", new Color(0.68f, 0.64f, 0.56f), 0.16f);
            _woodTimberMat = MakeMat(lit, "MAT_Exp_WoodDark", new Color(0.42f, 0.26f, 0.14f), 0.15f);
            _woodLightMat = MakeMat(lit, "MAT_Exp_WoodLight", new Color(0.68f, 0.48f, 0.28f), 0.18f);
            _barnRedMat = MakeMat(lit, "MAT_Exp_BarnRed", new Color(0.72f, 0.19f, 0.16f), 0.20f);
            _roofTileMat = MakeMat(lit, "MAT_Exp_RoofTile", new Color(0.62f, 0.24f, 0.14f), 0.22f);
            _whiteTrimMat = MakeMat(lit, "MAT_Exp_WhiteTrim", new Color(0.94f, 0.93f, 0.90f), 0.25f);
            _drySoilMat = MakeMat(lit, "MAT_Exp_DrySoil", new Color(0.48f, 0.34f, 0.20f), 0.06f);
            _wetSoilMat = MakeMat(lit, "MAT_Exp_WetSoil", new Color(0.24f, 0.15f, 0.08f), 0.35f);
            _hayGoldMat = MakeMat(lit, "MAT_Exp_HayGold", new Color(0.86f, 0.72f, 0.30f), 0.10f);
            _pineDarkMat = MakeMat(lit, "MAT_Exp_PineDark", new Color(0.12f, 0.34f, 0.16f), 0.12f);
            _leafBrightMat = MakeMat(lit, "MAT_Exp_LeafBright", new Color(0.22f, 0.54f, 0.20f), 0.16f);
            _blossomPinkMat = MakeMat(lit, "MAT_Exp_BlossomPink", new Color(0.95f, 0.60f, 0.74f), 0.22f);
            _flowerRedMat = MakeMat(lit, "MAT_Exp_FlowerRed", new Color(0.92f, 0.20f, 0.22f), 0.28f);
            _flowerYellowMat = MakeMat(lit, "MAT_Exp_FlowerYellow", new Color(0.98f, 0.82f, 0.18f), 0.28f);
            _flowerPurpleMat = MakeMat(lit, "MAT_Exp_FlowerPurple", new Color(0.64f, 0.34f, 0.88f), 0.28f);
            _lakeWaterMat = MakeMat(lit, "MAT_Exp_LakeWater", new Color(0.12f, 0.56f, 0.68f), 0.88f);
            _sandShoreMat = MakeMat(lit, "MAT_Exp_SandShore", new Color(0.78f, 0.72f, 0.56f), 0.14f);
            _tractorRedMat = MakeMat(lit, "MAT_Exp_TractorRed", new Color(0.84f, 0.18f, 0.14f), 0.65f);
            _darkMetalMat = MakeMat(lit, "MAT_Exp_DarkMetal", new Color(0.18f, 0.19f, 0.21f), 0.45f);
        }

        private static void ClearLegacyTightPerimeterWalls()
        {
            string[] legacyPerimeters = { "Hamidiye_DistrictPerimeter", "World_CountryFencePerimeter" };
            for (int i = 0; i < legacyPerimeters.Length; i++)
            {
                GameObject old = GameObject.Find(legacyPerimeters[i]);
                if (old != null)
                {
                    old.SetActive(false);
                }
            }
        }

        // =====================================================================
        // 1. EXPANDED 980x980 VALLEY FLOOR + 360-DEGREE ROLLING AYDOS HILLS
        // =====================================================================
        private void BuildValleyGroundAndAydosHills()
        {
            Transform envRoot = new GameObject("ValleyGroundAndAydosHills").transform;
            envRoot.SetParent(transform, false);

            // Large walkable lush valley ground right under existing scene floor (-0.045 so zero z-fighting!)
            GameObject ground = CreateBox("ExpandedValleyFloor", envRoot, new Vector3(0f, -0.545f, 0f),
                new Vector3(980f, 1.0f, 980f), _meadowMat, collider: true);
            ground.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // 28 layered rolling sunlit Aydos Hills encircling the 360-degree horizon safely OUTSIDE ±250m!
            System.Random rng = new System.Random(1453);
            for (int i = 0; i < 28; i++)
            {
                float angle = (i / 28f) * Mathf.PI * 2f + (float)(rng.NextDouble() * 0.12 - 0.06);
                bool outer = (i % 2) == 1;
                float dist = outer ? 410f + (float)rng.NextDouble() * 40f : 345f + (float)rng.NextDouble() * 28f;
                float hx = Mathf.Cos(angle) * dist;
                float hz = Mathf.Sin(angle) * dist;
                float radiusX = 58f + (float)rng.NextDouble() * 26f;
                float radiusZ = 52f + (float)rng.NextDouble() * 26f;
                float height = outer ? (38f + (float)rng.NextDouble() * 26f) : (20f + (float)rng.NextDouble() * 16f);

                GameObject hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hill.name = $"AydosHill_{i:00}";
                hill.transform.SetParent(envRoot, false);
                hill.transform.position = new Vector3(hx, -height * 0.22f, hz);
                hill.transform.localScale = new Vector3(radiusX * 2f, height * 1.4f, radiusZ * 2f);
                Destroy(hill.GetComponent<Collider>());
                MeshRenderer hr = hill.GetComponent<MeshRenderer>();
                hr.sharedMaterial = outer ? _hillFarMat : _hillNearMat;
                hr.shadowCastingMode = ShadowCastingMode.Off;

                // Hilltop pine trees on inner hills for rich Istanbul Aydos Forest skyline!
                if (!outer)
                {
                    for (int t = 0; t < 4; t++)
                    {
                        float ox = (float)(rng.NextDouble() * 28.0 - 14.0);
                        float oz = (float)(rng.NextDouble() * 28.0 - 14.0);
                        SpawnPineTree(envRoot, new Vector3(hx + ox, height * 0.38f, hz + oz), 1.4f + (float)rng.NextDouble() * 0.8f);
                    }
                }
            }
        }

        // =====================================================================
        // 2. HOME GARDEN BOSTAN (4 Starter Raised Plots beside Deniz's No:48 House!)
        // =====================================================================
        private void BuildHomeGardenBostan()
        {
            Transform homeRoot = new GameObject("HomeGardenBostan_No48").transform;
            homeRoot.SetParent(transform, false);

            // Cozy sunlit lawn & stepping stones at x=[-58.4..-52.0], z=[6.4..13.6] right beside No:48 driveway!
            CreateBox("HomeBostan_Path", homeRoot, new Vector3(-55.2f, 0.015f, 10.0f), new Vector3(6.4f, 0.03f, 7.2f), _stonePathMat, collider: false);

            // Low rustic whitewashed wooden garden fence (open south entry facing Selman St.!)
            CreateBox("FenceW", homeRoot, new Vector3(-58.4f, 0.42f, 10.0f), new Vector3(0.14f, 0.84f, 7.2f), _whiteTrimMat, collider: true);
            CreateBox("FenceE", homeRoot, new Vector3(-52.0f, 0.42f, 10.0f), new Vector3(0.14f, 0.84f, 7.2f), _whiteTrimMat, collider: true);
            CreateBox("FenceN", homeRoot, new Vector3(-55.2f, 0.42f, 13.6f), new Vector3(6.5f, 0.84f, 0.14f), _whiteTrimMat, collider: true);

            // Entrance sign
            CreateWorldSign(homeRoot, "EVİMİN BOSTANI", new Vector3(-57.6f, 0f, 6.5f), 0f, _woodTimberMat, new Color(0.24f, 0.64f, 0.34f));

            // 4 Starter Raised Soil Plots (pre-seed first plot with ripe Tomatoes on brand new saves!)
            Vector3[] homePlotCoords =
            {
                new Vector3(-56.6f, 0.02f,  8.4f),
                new Vector3(-53.8f, 0.02f,  8.4f),
                new Vector3(-56.6f, 0.02f, 11.4f),
                new Vector3(-53.8f, 0.02f, 11.4f)
            };

            for (int i = 0; i < homePlotCoords.Length; i++)
            {
                string pid = $"home_plot_{i + 1:00}";
                if (i == 0) EnsureStarterCropIfFresh(pid, FarmCropType.Tomato, 1.0f);
                SpawnFarmPlot(homeRoot, pid, homePlotCoords[i]);
            }

            // Cute cherry-blossom tree + flowerbed framing Deniz's home garden
            SpawnBlossomTree(homeRoot, new Vector3(-59.2f, 0f, 13.8f), 0.92f);
            SpawnFlowerBed(homeRoot, new Vector3(-51.4f, 0.02f, 6.8f), 0f);
        }

        // =====================================================================
        // 3. EAST EXPANSION: AYDOS YEŞİL VADİ · ÇİFTLİK & BOSTAN
        // =====================================================================
        private void BuildEastAydosFarmValley()
        {
            _farmRoot = new GameObject("AydosFarmValley_East").transform;
            _farmRoot.SetParent(transform, false);

            // Scenic country road continuing straight East from Selman Sokağı (x = 108..148, z = 0)
            CreateBox("FarmCountryRoad", _farmRoot, new Vector3(128f, 0.02f, 0f), new Vector3(44f, 0.04f, 8.6f), _roadCountryMat, collider: false);
            CreateBox("FarmCourtyard", _farmRoot, new Vector3(165f, 0.015f, 0f), new Vector3(42f, 0.03f, 14f), _stonePathMat, collider: false);

            // Cypress & Blossom tree boulevard lining Aydos Çiftlik Yolu (keep south x=[116..132] clear for Aydos Car Wash!)
            for (int i = 0; i < 4; i++)
            {
                float rx = 113f + i * 7.8f;
                SpawnBlossomTree(_farmRoot, new Vector3(rx, 0f, 6.8f), 0.95f);
                if (rx + 2f < 116f || rx + 2f > 132f)
                {
                    SpawnPineTree(_farmRoot, new Vector3(rx + 2f, 0f, -6.8f), 1.05f);
                }
                if (i % 2 == 0)
                {
                    SpawnFarmLantern(_farmRoot, new Vector3(rx + 3.5f, 0f, 5.2f));
                }
            }

            // Grand Wooden Entrance Archway at (138, 0, 0)
            BuildFarmArchway(_farmRoot, new Vector3(138f, 0f, 0f));

            // Village Market Stand (Sell Harvest!) at (146, 0, -5.8) & Farm Management Board at (146, 0, +5.8)
            BuildVillageMarketStand(_farmRoot, new Vector3(146f, 0f, -5.8f));
            BuildFarmManagementBoard(_farmRoot, new Vector3(146f, 0f, 5.8f));

            // --- 12 RAISED FARM PLOTS (2 rows x 6 beds, x = 152..177, z = +11.5 & +17.5) ---
            int plotIdx = 1;
            for (int row = 0; row < 2; row++)
            {
                float pz = row == 0 ? 11.5f : 17.5f;
                for (int col = 0; col < 6; col++)
                {
                    float px = 152f + col * 4.6f;
                    string pid = $"aydos_plot_{plotIdx:00}";
                    if (plotIdx == 1) EnsureStarterCropIfFresh(pid, FarmCropType.Tomato, 1.0f);
                    else if (plotIdx == 2) EnsureStarterCropIfFresh(pid, FarmCropType.Sunflower, 1.0f);
                    else if (plotIdx == 3) EnsureStarterCropIfFresh(pid, FarmCropType.Strawberry, 0.65f);
                    SpawnFarmPlot(_farmRoot, pid, new Vector3(px, 0.02f, pz));
                    plotIdx++;
                }
            }

            // Stone Water Well + Auto-Sprinkler poles in the garden
            BuildStoneWaterWell(_farmRoot, new Vector3(163.5f, 0f, 5.8f));
            BuildSprinklerPole(_farmRoot, new Vector3(156.6f, 0f, 14.5f));
            BuildSprinklerPole(_farmRoot, new Vector3(170.4f, 0f, 14.5f));
            HandleSprinklerUnlocked();

            // --- CHICKEN COOP & RUN (x = 146..170, z = -30..-12) ---
            BuildChickenCoopAndPasture(_farmRoot);

            // --- RED TIMBER BARN & COW PASTURE (x = 184..220, z = +10..+42) ---
            BuildRedBarnAndCowPasture(_farmRoot);

            // --- SHEEP PEN & SHEPHERD DOG KARABAŞ (x = 180..216, z = -34..-10) ---
            BuildSheepPenAndDog(_farmRoot);

            // --- ICONIC ROTATING AYDOS WINDMILL at (215, 0, 0) ---
            BuildRotatingWindmill(_farmRoot, new Vector3(215f, 0f, 0f));

            // --- COZY FARM TEA GAZEBO & VINTAGE TRACTOR DECOR ---
            BuildGazebo(_farmRoot, new Vector3(172f, 0f, -5.0f));
            BuildFarmTractorDecor(_farmRoot, new Vector3(179f, 0f, 3.8f), 210f);

            // Warm farmyard lanterns for cozy glowing nights!
            SpawnFarmLantern(_farmRoot, new Vector3(150f, 0f, 7.8f));
            SpawnFarmLantern(_farmRoot, new Vector3(176f, 0f, 7.8f));
            SpawnFarmLantern(_farmRoot, new Vector3(183f, 0f, -8.5f));
            SpawnFarmLantern(_farmRoot, new Vector3(204f, 0f, 4.5f));

            // Perimeter scenic pines & blossoms framing Aydos Farm Valley
            Vector3[] borderTrees =
            {
                new Vector3(145f, 0f,  26f), new Vector3(158f, 0f,  28f), new Vector3(172f, 0f,  27f),
                new Vector3(224f, 0f,  24f), new Vector3(226f, 0f,   8f), new Vector3(225f, 0f, -14f),
                new Vector3(222f, 0f, -34f), new Vector3(196f, 0f, -40f), new Vector3(162f, 0f, -36f),
                new Vector3(140f, 0f, -26f), new Vector3(140f, 0f,  18f)
            };
            for (int i = 0; i < borderTrees.Length; i++)
            {
                if (i % 3 == 0) SpawnBlossomTree(_farmRoot, borderTrees[i], 1.05f);
                else SpawnPineTree(_farmRoot, borderTrees[i], 1.15f + (i % 3) * 0.15f);
            }
        }

        private void BuildFarmArchway(Transform parent, Vector3 pos)
        {
            Transform arch = new GameObject("AydosFarmArchway").transform;
            arch.SetParent(parent, false);
            arch.position = pos;

            CreateBox("PillarN", arch, new Vector3(0f, 2.4f,  4.8f), new Vector3(0.45f, 4.8f, 0.45f), _woodTimberMat, collider: true);
            CreateBox("PillarS", arch, new Vector3(0f, 2.4f, -4.8f), new Vector3(0.45f, 4.8f, 0.45f), _woodTimberMat, collider: true);
            CreateBox("TopBeam", arch, new Vector3(0f, 4.7f,  0.0f), new Vector3(0.55f, 0.65f, 10.6f), _woodTimberMat, collider: false);
            CreateBox("SignBoard", arch, new Vector3(-0.30f, 4.7f, 0f), new Vector3(0.10f, 1.05f, 7.6f), _barnRedMat, collider: false);

            GameObject txtObj = new GameObject("ArchText");
            txtObj.transform.SetParent(arch, false);
            txtObj.transform.localPosition = new Vector3(-0.38f, 4.72f, 0f);
            txtObj.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            TextMesh tm = txtObj.AddComponent<TextMesh>();
            tm.text = "AYDOS ÇİFTLİĞİ · BOSTAN & MANDIRA";
            tm.characterSize = 0.08f;
            tm.fontSize = 48;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 0.94f, 0.78f);

            SpawnHangingLantern(arch, new Vector3(0f, 4.15f,  3.6f));
            SpawnHangingLantern(arch, new Vector3(0f, 4.15f, -3.6f));
        }

        private void BuildVillageMarketStand(Transform parent, Vector3 pos)
        {
            GameObject stand = new GameObject("VillageFarmMarketStand");
            stand.transform.SetParent(parent, false);
            stand.transform.position = pos;

            CreateBox("Counter", stand.transform, new Vector3(0f, 0.52f, 0f), new Vector3(4.2f, 1.04f, 1.4f), _woodLightMat, collider: true);
            CreateBox("PostL", stand.transform, new Vector3(-1.9f, 1.4f, -0.55f), new Vector3(0.16f, 2.8f, 0.16f), _woodTimberMat, collider: false);
            CreateBox("PostR", stand.transform, new Vector3( 1.9f, 1.4f, -0.55f), new Vector3(0.16f, 2.8f, 0.16f), _woodTimberMat, collider: false);
            CreateBox("AwningRed", stand.transform, new Vector3(0f, 2.75f, 0.15f), new Vector3(4.6f, 0.14f, 2.1f), _barnRedMat, collider: false).transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            CreateBox("AwningTrim", stand.transform, new Vector3(0f, 2.52f, 1.12f), new Vector3(4.6f, 0.24f, 0.08f), _whiteTrimMat, collider: false);

            // Colorful produce crates on counter
            CreateBox("CrateTom", stand.transform, new Vector3(-1.2f, 1.18f, 0f), new Vector3(0.7f, 0.28f, 0.6f), _woodTimberMat, collider: false);
            CreateSphere("TomDisplay", stand.transform, new Vector3(-1.2f, 1.38f, 0f), new Vector3(0.55f, 0.22f, 0.45f), _flowerRedMat);
            CreateBox("CrateSun", stand.transform, new Vector3(0f, 1.18f, 0f), new Vector3(0.7f, 0.28f, 0.6f), _woodTimberMat, collider: false);
            CreateSphere("SunDisplay", stand.transform, new Vector3(0f, 1.38f, 0f), new Vector3(0.55f, 0.22f, 0.45f), _flowerYellowMat);
            CreateBox("CrateMilk", stand.transform, new Vector3(1.2f, 1.24f, 0f), new Vector3(0.32f, 0.42f, 0.32f), _whiteTrimMat, collider: false);

            InteractableTrigger trig = stand.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.FarmMarket, "aydos_farm_market", "ui.btn.talk", 3.4f);
            trig.SetCustomPrompt("Ürünleri Sat · Köy Tezgâhı");

            // Overhead glowing stand sign (faces courtyard cleanly!)
            GameObject signTxt = new GameObject("MarketSign");
            signTxt.transform.SetParent(stand.transform, false);
            signTxt.transform.localPosition = new Vector3(0f, 3.05f, 0.65f);
            signTxt.transform.localRotation = Quaternion.identity;
            TextMesh tm = signTxt.AddComponent<TextMesh>();
            tm.text = "KÖY PAZARI TEZGÂHI";
            tm.characterSize = 0.06f;
            tm.fontSize = 42;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 0.92f, 0.65f);
        }

        private void BuildFarmManagementBoard(Transform parent, Vector3 pos)
        {
            GameObject board = new GameObject("FarmManagementBoard");
            board.transform.SetParent(parent, false);
            board.transform.position = pos;

            CreateBox("PostL", board.transform, new Vector3(-1.1f, 1.15f, 0f), new Vector3(0.16f, 2.3f, 0.16f), _woodTimberMat, collider: true);
            CreateBox("PostR", board.transform, new Vector3( 1.1f, 1.15f, 0f), new Vector3(0.16f, 2.3f, 0.16f), _woodTimberMat, collider: true);
            CreateBox("Panel", board.transform, new Vector3(0f, 1.55f, 0f), new Vector3(2.2f, 1.25f, 0.12f), new Material(_woodLightMat), collider: false);
            CreateBox("Roof", board.transform, new Vector3(0f, 2.32f, 0f), new Vector3(2.7f, 0.18f, 0.72f), _roofTileMat, collider: false);

            GameObject txtObj = new GameObject("BoardText");
            txtObj.transform.SetParent(board.transform, false);
            txtObj.transform.localPosition = new Vector3(0f, 1.60f, -0.09f);
            TextMesh tm = txtObj.AddComponent<TextMesh>();
            tm.text = "TOHUM & ÇİFTLİK\nYÖNETİMİ";
            tm.characterSize = 0.055f;
            tm.fontSize = 40;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.12f, 0.24f, 0.14f);

            InteractableTrigger trig = board.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.FarmBoard, "aydos_farm_board", "ui.btn.talk", 3.4f);
            trig.SetCustomPrompt("Tohum & Çiftlik Yönetimi");
        }

        private void BuildChickenCoopAndPasture(Transform parent)
        {
            Transform coopRoot = new GameObject("ChickenCoopAndRun").transform;
            coopRoot.SetParent(parent, false);

            // Rustic raised timber chicken coop at (158, 0, -26.5)
            Vector3 cp = new Vector3(158f, 0f, -26.5f);
            CreateBox("CoopBody", coopRoot, cp + new Vector3(0f, 1.35f, 0f), new Vector3(4.8f, 2.1f, 3.2f), _woodLightMat, collider: true);
            CreateBox("CoopRoof", coopRoot, cp + new Vector3(0f, 2.65f, 0f), new Vector3(5.4f, 0.45f, 3.8f), _roofTileMat, collider: false);
            CreateBox("CoopDoor", coopRoot, cp + new Vector3(0f, 0.9f, 1.62f), new Vector3(0.95f, 1.1f, 0.1f), _darkMetalMat, collider: false);
            CreateBox("CoopRamp", coopRoot, cp + new Vector3(0f, 0.30f, 2.35f), new Vector3(0.9f, 0.10f, 1.6f), _woodTimberMat, collider: false).transform.localRotation = Quaternion.Euler(20f, 0f, 0f);

            // Fenced yard (x = 146..170, z = -30..-12, wide open gate on North side at x = 158, z = -12)
            BuildPaddockFence(coopRoot, 146f, 170f, -30f, -12f, gateSide: 0, gatePos: 158f, gateHalfW: 2.4f);
            CreateWorldSign(coopRoot, "TAVUK KÜMESİ", new Vector3(154.5f, 0f, -11.5f), 180f, _woodTimberMat, new Color(0.86f, 0.56f, 0.16f));

            int totalChickens = 5 + (FarmEconomyManager.Data != null ? FarmEconomyManager.Data.extraChickens : 0);
            for (int i = 0; i < totalChickens; i++)
            {
                SpawnLivestock(FarmAnimalKind.Chicken, i);
            }
        }

        private void BuildRedBarnAndCowPasture(Transform parent)
        {
            Transform barnRoot = new GameObject("RedBarnAndCowPasture").transform;
            barnRoot.SetParent(parent, false);

            // Iconic Red Timber Barn at (204, 0, 34)
            Vector3 bp = new Vector3(204f, 0f, 34f);
            CreateBox("BarnBody", barnRoot, bp + new Vector3(0f, 3.0f, 0f), new Vector3(12.5f, 6.0f, 9.2f), _barnRedMat, collider: true);
            CreateBox("BarnRoofL", barnRoot, bp + new Vector3(0f, 7.1f, -2.5f), new Vector3(13.4f, 0.35f, 5.8f), _roofTileMat, collider: false).transform.localRotation = Quaternion.Euler(-26f, 0f, 0f);
            CreateBox("BarnRoofR", barnRoot, bp + new Vector3(0f, 7.1f,  2.5f), new Vector3(13.4f, 0.35f, 5.8f), _roofTileMat, collider: false).transform.localRotation = Quaternion.Euler( 26f, 0f, 0f);
            CreateBox("BarnDoorFrame", barnRoot, bp + new Vector3(0f, 2.2f, -4.64f), new Vector3(4.2f, 4.4f, 0.14f), _whiteTrimMat, collider: false);
            CreateBox("BarnDoorInner", barnRoot, bp + new Vector3(0f, 2.2f, -4.70f), new Vector3(3.6f, 3.9f, 0.10f), _barnRedMat, collider: false);
            CreateBox("HayLoftWin", barnRoot, bp + new Vector3(0f, 5.0f, -4.64f), new Vector3(1.8f, 1.4f, 0.14f), _whiteTrimMat, collider: false);

            // Golden hay bales + water trough
            SpawnHayBale(barnRoot, new Vector3(193f, 0.55f, 30f));
            SpawnHayBale(barnRoot, new Vector3(194.6f, 0.55f, 30.8f));
            SpawnHayBale(barnRoot, new Vector3(193.8f, 1.55f, 30.4f));
            CreateBox("WaterTrough", barnRoot, new Vector3(190f, 0.38f, 24f), new Vector3(2.8f, 0.76f, 1.1f), _woodTimberMat, collider: true);
            CreateBox("WaterTroughFill", barnRoot, new Vector3(190f, 0.72f, 24f), new Vector3(2.5f, 0.12f, 0.85f), _lakeWaterMat, collider: false);

            // Pasture fence (x = 184..218, z = 10..40, open West gate at x = 184, z = 19)
            BuildPaddockFence(barnRoot, 184f, 218f, 10f, 40f, gateSide: 2, gatePos: 19f, gateHalfW: 2.8f);
            CreateWorldSign(barnRoot, "İNEK AHIRI · MANDIRA", new Vector3(183.5f, 0f, 14.5f), 90f, _woodTimberMat, new Color(0.72f, 0.19f, 0.16f));

            int totalCows = 3 + (FarmEconomyManager.Data != null ? FarmEconomyManager.Data.extraCows : 0);
            for (int i = 0; i < totalCows; i++)
            {
                SpawnLivestock(FarmAnimalKind.Cow, i);
            }
        }

        private void BuildSheepPenAndDog(Transform parent)
        {
            Transform sheepRoot = new GameObject("SheepPenAndKarabas").transform;
            sheepRoot.SetParent(parent, false);

            // Timber shade shelter at (204, 0, -29)
            Vector3 sp = new Vector3(204f, 0f, -29f);
            CreateBox("ShelterBack", sheepRoot, sp + new Vector3(0f, 1.4f, -1.8f), new Vector3(7.6f, 2.8f, 0.24f), _woodTimberMat, collider: true);
            CreateBox("ShelterRoof", sheepRoot, sp + new Vector3(0f, 2.95f, 0f), new Vector3(8.2f, 0.22f, 4.4f), _roofTileMat, collider: false).transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            SpawnHayBale(sheepRoot, sp + new Vector3(-2.6f, 0.55f, 0.6f));
            SpawnHayBale(sheepRoot, sp + new Vector3( 2.6f, 0.55f, 0.6f));

            // Sheep paddock fence (x = 180..216, z = -34..-10, open West gate at x = 180, z = -18)
            BuildPaddockFence(sheepRoot, 180f, 216f, -34f, -10f, gateSide: 2, gatePos: -18f, gateHalfW: 2.6f);
            CreateWorldSign(sheepRoot, "KOYUN AĞILI", new Vector3(179.5f, 0f, -13.8f), 90f, _woodTimberMat, new Color(0.24f, 0.54f, 0.68f));

            int totalSheep = 4 + (FarmEconomyManager.Data != null ? FarmEconomyManager.Data.extraSheep : 0);
            for (int i = 0; i < totalSheep; i++)
            {
                SpawnLivestock(FarmAnimalKind.Sheep, i);
            }

            // Friendly Anatolian Shepherd Dog "Karabaş" guarding outside the sheep pen!
            BuildShepherdDog(sheepRoot, new Vector3(177f, 0f, -22f));
        }

        private void HandleExtraAnimalPurchased(FarmAnimalKind kind)
        {
            if (kind == FarmAnimalKind.Chicken) SpawnLivestock(kind, _spawnedChickens);
            else if (kind == FarmAnimalKind.Sheep) SpawnLivestock(kind, _spawnedSheep);
            else SpawnLivestock(kind, _spawnedCows);

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }
        }

        private void SpawnLivestock(FarmAnimalKind kind, int idx)
        {
            if (_farmRoot == null) return;

            string[] chickenNames = { "Çilli", "Pamuk", "Kınalı", "Sarı", "Benekli", "Fındık", "Badem", "Zeytin", "Boncuk", "Şeker" };
            string[] cowNames = { "Sarıkız", "Alaca", "Karakız", "Nazlı", "Sultan", "Gülbahar" };
            string[] sheepNames = { "Yumak", "Bulut", "Kar", "Pofuduk", "Kuzu", "Duman", "İnci", "Tüylü" };

            Vector3 center;
            Vector2 half;
            string name;
            float radius;

            if (kind == FarmAnimalKind.Chicken)
            {
                center = new Vector3(158f, 0.02f, -19.5f);
                half = new Vector2(8.5f, 5.5f);
                name = chickenNames[idx % chickenNames.Length];
                radius = 2.6f;
                _spawnedChickens++;
            }
            else if (kind == FarmAnimalKind.Cow)
            {
                center = new Vector3(198f, 0.02f, 21f);
                half = new Vector2(11.0f, 7.5f);
                name = cowNames[idx % cowNames.Length];
                radius = 3.6f;
                _spawnedCows++;
            }
            else
            {
                center = new Vector3(197f, 0.02f, -20.5f);
                half = new Vector2(12.5f, 6.5f);
                name = sheepNames[idx % sheepNames.Length];
                radius = 3.0f;
                _spawnedSheep++;
            }

            GameObject go = new GameObject($"FarmAnimal_{kind}_{name}_{idx}");
            go.transform.SetParent(_farmRoot, false);
            float ox = ((idx * 37) % 10 - 5) * (half.x * 0.16f);
            float oz = ((idx * 53) % 10 - 5) * (half.y * 0.16f);
            go.transform.position = center + new Vector3(ox, 0f, oz);
            go.transform.rotation = Quaternion.Euler(0f, idx * 67f, 0f);

            InteractableTrigger trig = go.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.FarmAnimal, $"farm_animal_{kind}_{idx}", "ui.btn.talk", radius);

            FarmAnimalController ctrl = go.AddComponent<FarmAnimalController>();
            ctrl.Initialize(kind, name, center, half, trig, idx, startReady: idx < 3);
        }

        private void SpawnFarmPlot(Transform parent, string plotId, Vector3 worldPos)
        {
            GameObject go = new GameObject($"FarmPlot_{plotId}");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;

            // Raised timber frame + rich soil top
            CreateBox("FrameN", go.transform, new Vector3(0f, 0.14f,  0.96f), new Vector3(2.44f, 0.28f, 0.14f), _woodTimberMat, collider: false);
            CreateBox("FrameS", go.transform, new Vector3(0f, 0.14f, -0.96f), new Vector3(2.44f, 0.28f, 0.14f), _woodTimberMat, collider: false);
            CreateBox("FrameW", go.transform, new Vector3(-1.16f, 0.14f, 0f), new Vector3(0.14f, 0.28f, 1.84f), _woodTimberMat, collider: false);
            CreateBox("FrameE", go.transform, new Vector3( 1.16f, 0.14f, 0f), new Vector3(0.14f, 0.28f, 1.84f), _woodTimberMat, collider: false);

            GameObject soil = CreateBox("SoilBed", go.transform, new Vector3(0f, 0.12f, 0f), new Vector3(2.22f, 0.22f, 1.80f), _drySoilMat, collider: false);
            MeshRenderer soilMr = soil.GetComponent<MeshRenderer>();

            InteractableTrigger trig = go.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.FarmPlot, plotId, "ui.btn.talk", 2.8f);

            FarmPlotInteractable plot = go.AddComponent<FarmPlotInteractable>();
            plot.Initialize(plotId, trig, soilMr, _drySoilMat, _wetSoilMat);
        }

        private static void EnsureStarterCropIfFresh(string plotId, FarmCropType crop, float progress)
        {
            SaveData d = FarmEconomyManager.Data;
            if (d == null) return;
            if (d.farmPlots == null) d.farmPlots = new List<FarmPlotSaveEntry>();
            for (int i = 0; i < d.farmPlots.Count; i++)
            {
                if (d.farmPlots[i] != null && d.farmPlots[i].plotId == plotId) return; // Already persisted
            }
            d.farmPlots.Add(new FarmPlotSaveEntry
            {
                plotId = plotId,
                cropType = (int)crop,
                growthProgress = progress,
                isWatered = true
            });
        }

        private void BuildRotatingWindmill(Transform parent, Vector3 pos)
        {
            Transform wm = new GameObject("AydosWindmill").transform;
            wm.SetParent(parent, false);
            wm.SetPositionAndRotation(pos, Quaternion.Euler(0f, -90f, 0f));

            GameObject fbx = OurWorldModelLibrary.InstantiateModel("OW_Windmill", wm, Vector3.zero, Quaternion.identity);
            if (fbx != null)
            {
                BoxCollider col = wm.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 3.2f, 0f);
                col.size = new Vector3(4.8f, 6.4f, 4.8f);
                _windmillBlades = OurWorldModelLibrary.FindPart(fbx.transform, "BladesPivot")
                               ?? OurWorldModelLibrary.FindPart(fbx.transform, "WindmillBlades");
                // Decorative golden wheat & corn bundles framing the stone windmill base!
                OurWorldModelLibrary.InstantiateModel("OW_Crop_Wheat", wm, new Vector3(3.1f, 0f, 2.2f), Quaternion.Euler(0f, 20f, 0f), Vector3.one * 1.35f);
                OurWorldModelLibrary.InstantiateModel("OW_Crop_Wheat", wm, new Vector3(-3.0f, 0f, 2.4f), Quaternion.Euler(0f, -35f, 0f), Vector3.one * 1.25f);
                OurWorldModelLibrary.InstantiateModel("OW_Crop_Corn", wm, new Vector3(3.3f, 0f, -2.0f), Quaternion.Euler(0f, 15f, 0f), Vector3.one * 1.15f);
                OurWorldModelLibrary.InstantiateModel("OW_Crop_Corn", wm, new Vector3(-3.2f, 0f, -1.9f), Quaternion.Euler(0f, 45f, 0f), Vector3.one * 1.10f);
                return;
            }

            // Fallback procedural windmill
            wm.rotation = Quaternion.identity;
            CreateCylinder("TowerBase", wm, new Vector3(0f, 3.2f, 0f), new Vector3(4.6f, 3.2f, 4.6f), _whiteTrimMat, collider: true);
            CreateCylinder("TowerTop", wm, new Vector3(0f, 7.6f, 0f), new Vector3(3.6f, 2.2f, 3.6f), _whiteTrimMat, collider: false);
            CreateSphere("RoofDome", wm, new Vector3(0f, 10.2f, 0f), new Vector3(4.2f, 2.8f, 4.2f), _roofTileMat);
            CreateBox("Door", wm, new Vector3(-2.25f, 1.2f, 0f), new Vector3(0.18f, 2.4f, 1.25f), _woodTimberMat, collider: false);

            GameObject hub = new GameObject("WindmillBladeHub");
            hub.transform.SetParent(wm, false);
            hub.transform.localPosition = new Vector3(-2.1f, 8.6f, 0f);
            hub.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            _windmillBlades = hub.transform;

            CreateCylinder("HubCore", _windmillBlades, Vector3.zero, new Vector3(0.6f, 0.35f, 0.6f), _woodTimberMat, collider: false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                Transform arm = new GameObject($"Blade_{i}").transform;
                arm.SetParent(_windmillBlades, false);
                arm.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                CreateBox("Spar", arm, new Vector3(0f, 2.8f, 0f), new Vector3(0.22f, 5.6f, 0.14f), _woodTimberMat, collider: false);
                CreateBox("Sail", arm, new Vector3(0.48f, 3.3f, 0.04f), new Vector3(0.95f, 4.0f, 0.05f), _whiteTrimMat, collider: false);
            }
        }

        private void BuildStoneWaterWell(Transform parent, Vector3 pos)
        {
            Transform w = new GameObject("FarmWaterWell").transform;
            w.SetParent(parent, false);
            w.position = pos;
            CreateCylinder("WellRing", w, new Vector3(0f, 0.55f, 0f), new Vector3(2.1f, 0.55f, 2.1f), _stonePathMat, collider: true);
            CreateCylinder("WellWater", w, new Vector3(0f, 0.92f, 0f), new Vector3(1.65f, 0.10f, 1.65f), _lakeWaterMat, collider: false);
            CreateBox("WellPostL", w, new Vector3(-0.95f, 1.6f, 0f), new Vector3(0.16f, 2.1f, 0.16f), _woodTimberMat, collider: false);
            CreateBox("WellPostR", w, new Vector3( 0.95f, 1.6f, 0f), new Vector3(0.16f, 2.1f, 0.16f), _woodTimberMat, collider: false);
            CreateBox("WellRoof", w, new Vector3(0f, 2.7f, 0f), new Vector3(2.6f, 0.24f, 1.8f), _roofTileMat, collider: false);
        }

        private void BuildSprinklerPole(Transform parent, Vector3 pos)
        {
            Transform sp = new GameObject("AutoSprinkler").transform;
            sp.SetParent(parent, false);
            sp.position = pos;
            CreateCylinder("Pipe", sp, new Vector3(0f, 0.45f, 0f), new Vector3(0.12f, 0.45f, 0.12f), _darkMetalMat, collider: false);
            GameObject head = new GameObject("Head");
            head.transform.SetParent(sp, false);
            head.transform.localPosition = new Vector3(0f, 0.92f, 0f);
            CreateBox("Bar", head.transform, Vector3.zero, new Vector3(0.65f, 0.08f, 0.08f), _flowerYellowMat, collider: false);
            _sprinklerHeads.Add(head.transform);
            sp.gameObject.SetActive(false);
        }

        private void HandleSprinklerUnlocked()
        {
            bool active = FarmEconomyManager.Data != null && FarmEconomyManager.Data.hasAutoSprinkler;
            for (int i = 0; i < _sprinklerHeads.Count; i++)
            {
                if (_sprinklerHeads[i] != null && _sprinklerHeads[i].parent != null)
                {
                    _sprinklerHeads[i].parent.gameObject.SetActive(active);
                }
            }
        }

        private void BuildFarmTractorDecor(Transform parent, Vector3 pos, float yaw)
        {
            Transform tr = new GameObject("VintageFarmTractor").transform;
            tr.SetParent(parent, false);
            tr.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            GameObject fbx = OurWorldModelLibrary.InstantiateModel("OW_FarmTractor", tr, Vector3.zero, Quaternion.identity);
            if (fbx != null)
            {
                BoxCollider col = tr.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 1.2f, 0.1f);
                col.size = new Vector3(2.0f, 2.4f, 3.3f);
                return;
            }

            CreateBox("Hood", tr, new Vector3(0f, 1.15f, 0.75f), new Vector3(1.1f, 0.95f, 1.75f), _tractorRedMat, collider: true);
            CreateBox("Cab", tr, new Vector3(0f, 1.65f, -0.55f), new Vector3(1.35f, 1.35f, 1.15f), _tractorRedMat, collider: true);
            CreateBox("CabRoof", tr, new Vector3(0f, 2.38f, -0.55f), new Vector3(1.50f, 0.12f, 1.35f), _whiteTrimMat, collider: false);
            CreateCylinder("Exhaust", tr, new Vector3(0.45f, 2.0f, 1.15f), new Vector3(0.12f, 0.55f, 0.12f), _darkMetalMat, collider: false);
            // Big rear wheels + smaller front wheels
            CreateCylinder("WheelRL", tr, new Vector3(-0.85f, 0.72f, -0.65f), new Vector3(1.44f, 0.24f, 1.44f), _darkMetalMat, collider: false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreateCylinder("WheelRR", tr, new Vector3( 0.85f, 0.72f, -0.65f), new Vector3(1.44f, 0.24f, 1.44f), _darkMetalMat, collider: false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreateCylinder("WheelFL", tr, new Vector3(-0.72f, 0.45f,  1.20f), new Vector3(0.90f, 0.18f, 0.90f), _darkMetalMat, collider: false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreateCylinder("WheelFR", tr, new Vector3( 0.72f, 0.45f,  1.20f), new Vector3(0.90f, 0.18f, 0.90f), _darkMetalMat, collider: false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private void BuildShepherdDog(Transform parent, Vector3 pos)
        {
            GameObject dog = new GameObject("ShepherdDog_Karabas");
            dog.transform.SetParent(parent, false);
            dog.transform.position = pos;
            _shepherdDog = dog.transform;
            _dogHome = pos;
            _shepherdLegs.Clear();

            GameObject fbx = OurWorldModelLibrary.InstantiateModel("OW_ShepherdDog", _shepherdDog, Vector3.zero, Quaternion.identity);
            if (fbx != null)
            {
                _shepherdSkel = fbx.AddComponent<SkeletalClipPlayer>();
                if (!_shepherdSkel.Initialize("OW_ShepherdDog", "Idle", 0.5f))
                {
                    _shepherdSkel = null;
                    _shepherdHead = OurWorldModelLibrary.FindPart(fbx.transform, "HeadPivot");
                    _shepherdTail = OurWorldModelLibrary.FindPart(fbx.transform, "TailPivot");
                    string[] legNames = { "Leg_FL", "Leg_FR", "Leg_BL", "Leg_BR" };
                    for (int i = 0; i < legNames.Length; i++)
                    {
                        Transform leg = OurWorldModelLibrary.FindPart(fbx.transform, legNames[i]);
                        if (leg != null) _shepherdLegs.Add(leg);
                    }
                }
                return;
            }

            Material tanMat = MakeMat(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"), "MAT_DogTan", new Color(0.84f, 0.74f, 0.56f), 0.18f);
            CreateBox("Body", _shepherdDog, new Vector3(0f, 0.52f, 0f), new Vector3(0.38f, 0.34f, 0.86f), tanMat, collider: false);
            CreateBox("Head", _shepherdDog, new Vector3(0f, 0.72f, 0.46f), new Vector3(0.28f, 0.26f, 0.32f), tanMat, collider: false);
            CreateBox("Snout", _shepherdDog, new Vector3(0f, 0.66f, 0.66f), new Vector3(0.16f, 0.14f, 0.16f), _darkMetalMat, collider: false);
            CreateBox("EarL", _shepherdDog, new Vector3(-0.14f, 0.82f, 0.42f), new Vector3(0.08f, 0.14f, 0.08f), _darkMetalMat, collider: false);
            CreateBox("EarR", _shepherdDog, new Vector3( 0.14f, 0.82f, 0.42f), new Vector3(0.08f, 0.14f, 0.08f), _darkMetalMat, collider: false);
            CreateBox("LegFL", _shepherdDog, new Vector3(-0.12f, 0.22f,  0.28f), new Vector3(0.09f, 0.44f, 0.09f), tanMat, collider: false);
            CreateBox("LegFR", _shepherdDog, new Vector3( 0.12f, 0.22f,  0.28f), new Vector3(0.09f, 0.44f, 0.09f), tanMat, collider: false);
            CreateBox("LegBL", _shepherdDog, new Vector3(-0.12f, 0.22f, -0.28f), new Vector3(0.09f, 0.44f, 0.09f), tanMat, collider: false);
            CreateBox("LegBR", _shepherdDog, new Vector3( 0.12f, 0.22f, -0.28f), new Vector3(0.09f, 0.44f, 0.09f), tanMat, collider: false);

            GameObject tailPiv = new GameObject("Tail");
            tailPiv.transform.SetParent(_shepherdDog, false);
            tailPiv.transform.localPosition = new Vector3(0f, 0.62f, -0.42f);
            _shepherdTail = tailPiv.transform;
            CreateBox("TailMesh", _shepherdTail, new Vector3(0f, 0.14f, -0.06f), new Vector3(0.07f, 0.30f, 0.07f), tanMat, collider: false);
        }

        // =====================================================================
        // 4. WEST EXPANSION: SULTANBEYLİ GÖLETİ & AYDOS ÇAM KORUSU
        // =====================================================================
        private void BuildWestSultanbeyliLakeAndGrove()
        {
            Transform lakeRoot = new GameObject("SultanbeyliLakeAndPineGrove_West").transform;
            lakeRoot.SetParent(transform, false);

            // Scenic boulevard connecting West end of Selman Sokağı (x = -112..-168, z = 0)
            CreateBox("LakeBoulevard", lakeRoot, new Vector3(-138f, 0.02f, 0f), new Vector3(56f, 0.04f, 8.6f), _roadCountryMat, collider: false);
            CreateBox("LakePromenade", lakeRoot, new Vector3(-168f, 0.025f, 0f), new Vector3(8.5f, 0.05f, 38f), _stonePathMat, collider: false);

            // Shimmering Turquoise Pond (-198, 0, 0) + Sandy Shore Rim
            CreateCylinder("LakeShore", lakeRoot, new Vector3(-198f, 0.01f, 0f), new Vector3(62f, 0.02f, 48f), _sandShoreMat, collider: false);
            GameObject water = CreateCylinder("LakeWater", lakeRoot, new Vector3(-198f, 0.04f, 0f), new Vector3(56f, 0.04f, 42f), _lakeWaterMat, collider: false);
            _lakeSurface = water.transform;

            // Wooden Viewing Pier jutting out over the turquoise lake!
            CreateBox("PierDeck", lakeRoot, new Vector3(-175.5f, 0.36f, 0f), new Vector3(15f, 0.16f, 4.2f), _woodLightMat, collider: true);
            CreateBox("PierRamp", lakeRoot, new Vector3(-167.2f, 0.18f, 0f), new Vector3(2.2f, 0.14f, 4.2f), _woodLightMat, collider: true).transform.localRotation = Quaternion.Euler(0f, 0f, 10f);
            for (int p = 0; p < 4; p++)
            {
                float px = -170f - p * 4.0f;
                CreateCylinder($"PierPilingN_{p}", lakeRoot, new Vector3(px, 0.25f,  1.95f), new Vector3(0.22f, 0.65f, 0.22f), _woodTimberMat, collider: false);
                CreateCylinder($"PierPilingS_{p}", lakeRoot, new Vector3(px, 0.25f, -1.95f), new Vector3(0.22f, 0.65f, 0.22f), _woodTimberMat, collider: false);
            }
            SpawnFarmLantern(lakeRoot, new Vector3(-182.2f, 0.44f,  1.8f));
            SpawnFarmLantern(lakeRoot, new Vector3(-182.2f, 0.44f, -1.8f));
            CreateWorldSign(lakeRoot, "SULTANBEYLİ GÖLETİ", new Vector3(-166f, 0f, 4.8f), -90f, _woodTimberMat, new Color(0.14f, 0.56f, 0.68f));

            // Floating Emerald Lily Pads & Pink Lotus Blossoms
            Vector3[] lilies =
            {
                new Vector3(-192f, 0.07f,  9f), new Vector3(-196f, 0.07f, 11f), new Vector3(-206f, 0.07f,  6f),
                new Vector3(-202f, 0.07f, -10f), new Vector3(-189f, 0.07f, -8f), new Vector3(-212f, 0.07f, -4f)
            };
            for (int i = 0; i < lilies.Length; i++)
            {
                CreateCylinder($"LilyPad_{i}", lakeRoot, lilies[i], new Vector3(1.15f, 0.03f, 1.15f), _leafBrightMat, collider: false);
                if (i % 2 == 0)
                {
                    CreateSphere($"Lotus_{i}", lakeRoot, lilies[i] + Vector3.up * 0.14f, new Vector3(0.40f, 0.22f, 0.40f), _blossomPinkMat);
                }
            }

            // 3 Rigged & Skeletal-Animated Swimming Pond Ducks (Blender MCP OW_PondDuck FBX!)
            for (int d = 0; d < 3; d++)
            {
                Transform duck = new GameObject($"PondDuck_{d}").transform;
                duck.SetParent(lakeRoot, false);
                SkeletalClipPlayer duckSkel = null;
                GameObject duckFbx = OurWorldModelLibrary.InstantiateModel("OW_PondDuck", duck, Vector3.zero, Quaternion.identity);
                if (duckFbx != null)
                {
                    if (d > 0)
                    {
                        OurWorldModelLibrary.TintMaterialByKeyword(duckFbx, "DuckEmerald", new Color(0.96f, 0.95f, 0.92f));
                    }
                    duckSkel = duckFbx.AddComponent<SkeletalClipPlayer>();
                    if (!duckSkel.Initialize("OW_PondDuck", "Swim", d * 1.4f))
                    {
                        duckSkel = null;
                    }
                }
                else
                {
                    CreateSphere("DuckBody", duck, new Vector3(0f, 0.12f, 0f), new Vector3(0.36f, 0.24f, 0.52f), _whiteTrimMat);
                    CreateSphere("DuckHead", duck, new Vector3(0f, 0.28f, 0.22f), new Vector3(0.20f, 0.20f, 0.22f), d == 0 ? _leafBrightMat : _whiteTrimMat);
                    CreateBox("DuckBeak", duck, new Vector3(0f, 0.26f, 0.36f), new Vector3(0.09f, 0.06f, 0.14f), _flowerYellowMat, collider: false);
                }
                _ducks.Add((duck, duckSkel, new Vector3(-198f + (d - 1) * 7f, 0f, (d - 1) * 5f), 5.5f + d * 2.2f, 0.36f + d * 0.08f, d * 2.1f));
            }

            // Lakeside Gazebos + Lush Aydos Pine & Cherry-Blossom Grove!
            BuildGazebo(lakeRoot, new Vector3(-168f, 0f,  15f));
            BuildGazebo(lakeRoot, new Vector3(-168f, 0f, -15f));
            SpawnFlowerBed(lakeRoot, new Vector3(-166f, 0.03f,  8.5f), 90f);
            SpawnFlowerBed(lakeRoot, new Vector3(-166f, 0.03f, -8.5f), 90f);

            for (int i = 0; i < 24; i++)
            {
                float a = (i / 24f) * Mathf.PI * 2f;
                // Keep East pier approach clear
                if (Mathf.Cos(a) > 0.72f && Mathf.Abs(Mathf.Sin(a)) < 0.38f) continue;
                float rx = -198f + Mathf.Cos(a) * (35f + (i % 3) * 5f);
                float rz = Mathf.Sin(a) * (27f + (i % 2) * 6f);
                if (i % 4 == 0) SpawnBlossomTree(lakeRoot, new Vector3(rx, 0f, rz), 1.05f);
                else SpawnPineTree(lakeRoot, new Vector3(rx, 0f, rz), 1.15f + (i % 3) * 0.2f);
            }

            // Boulevard blossom trees & warm lanterns (keep x=[-156..-136] north side clear for Aydos Car Wash!)
            for (int i = 0; i < 5; i++)
            {
                float bx = -120f - i * 9.5f;
                if (bx > -136f || bx < -156f)
                {
                    SpawnBlossomTree(lakeRoot, new Vector3(bx, 0f, 6.8f), 0.95f);
                }
                SpawnPineTree(lakeRoot, new Vector3(bx, 0f, -6.8f), 1.05f);
                if (i % 2 == 0) SpawnFarmLantern(lakeRoot, new Vector3(bx + 4f, 0f, -5.2f));
            }
        }

        // =====================================================================
        // 5. HAMİDİYE NEIGHBORHOOD SCENIC BEAUTIFICATION
        // =====================================================================
        private void BeautifyHamidiyePlazasAndAvenues()
        {
            Transform beautyRoot = new GameObject("HamidiyeScenicBeautification").transform;
            beautyRoot.SetParent(transform, false);

            // Vibrant flowerbeds & cherry-blossom accents framing Fatih Bulvarı Clock Tower Plaza!
            SpawnFlowerBed(beautyRoot, new Vector3(-11.5f, 0.02f, 134.5f), 0f);
            SpawnFlowerBed(beautyRoot, new Vector3( 11.5f, 0.02f, 134.5f), 0f);
            SpawnFlowerBed(beautyRoot, new Vector3(-11.5f, 0.02f, 147.0f), 0f);
            SpawnFlowerBed(beautyRoot, new Vector3( 11.5f, 0.02f, 147.0f), 0f);
            SpawnBlossomTree(beautyRoot, new Vector3(-17.5f, 0f, 145.5f), 1.05f);
            SpawnBlossomTree(beautyRoot, new Vector3( 17.5f, 0f, 145.5f), 1.05f);
            SpawnPineTree(beautyRoot, new Vector3(-26f, 0f, 148f), 1.2f);
            SpawnPineTree(beautyRoot, new Vector3( 26f, 0f, 148f), 1.2f);

            // Scenic directional guideposts on Selman Sokağı pointing East to Aydos Farm & West to Lake!
            CreateWorldSign(beautyRoot, "AYDOS ÇİFTLİĞİ →", new Vector3(106f, 0f, 5.8f), 0f, _woodTimberMat, new Color(0.20f, 0.62f, 0.34f));
            CreateWorldSign(beautyRoot, "← SULTANBEYLİ GÖLETİ", new Vector3(-106f, 0f, 5.8f), 0f, _woodTimberMat, new Color(0.14f, 0.56f, 0.68f));

            // Colorful flowerbeds & spring blossom trees near Sports Courts & Mosque approach
            SpawnFlowerBed(beautyRoot, new Vector3(-28f, 0.02f, -26.5f), 0f);
            SpawnFlowerBed(beautyRoot, new Vector3(  2f, 0.02f, -26.5f), 0f);
            SpawnFlowerBed(beautyRoot, new Vector3( 30f, 0.02f, -26.5f), 0f);
            SpawnBlossomTree(beautyRoot, new Vector3( 70f, 0f, -21.5f), 0.96f);
            SpawnBlossomTree(beautyRoot, new Vector3( 94f, 0f, -21.5f), 0.96f);
            SpawnBlossomTree(beautyRoot, new Vector3( 29f, 0f, -26.5f), 0.92f);
        }

        // =====================================================================
        // SHARED MODULAR PROP HELPERS
        // =====================================================================
        private void BuildPaddockFence(Transform parent, float minX, float maxX, float minZ, float maxZ, int gateSide, float gatePos, float gateHalfW)
        {
            // gateSide: 0=North(maxZ), 1=South(minZ), 2=West(minX), 3=East(maxX)
            BuildFenceLine(parent, new Vector3(minX, 0f, maxZ), new Vector3(maxX, 0f, maxZ), gateSide == 0, gatePos, gateHalfW, alongX: true);
            BuildFenceLine(parent, new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, minZ), gateSide == 1, gatePos, gateHalfW, alongX: true);
            BuildFenceLine(parent, new Vector3(minX, 0f, minZ), new Vector3(minX, 0f, maxZ), gateSide == 2, gatePos, gateHalfW, alongX: false);
            BuildFenceLine(parent, new Vector3(maxX, 0f, minZ), new Vector3(maxX, 0f, maxZ), gateSide == 3, gatePos, gateHalfW, alongX: false);
        }

        private void BuildFenceLine(Transform parent, Vector3 a, Vector3 b, bool hasGate, float gateCoord, float gateHalfW, bool alongX)
        {
            float start = alongX ? a.x : a.z;
            float end = alongX ? b.x : b.z;
            float fixedCoord = alongX ? a.z : a.x;

            if (!hasGate)
            {
                BuildFenceSpan(parent, start, end, fixedCoord, alongX);
            }
            else
            {
                float g0 = Mathf.Max(start, gateCoord - gateHalfW);
                float g1 = Mathf.Min(end, gateCoord + gateHalfW);
                if (g0 - start > 1f) BuildFenceSpan(parent, start, g0, fixedCoord, alongX);
                if (end - g1 > 1f) BuildFenceSpan(parent, g1, end, fixedCoord, alongX);
            }
        }

        private void BuildFenceSpan(Transform parent, float s0, float s1, float fixedCoord, bool alongX)
        {
            float len = s1 - s0;
            float mid = (s0 + s1) * 0.5f;
            Vector3 railPos1 = alongX ? new Vector3(mid, 0.88f, fixedCoord) : new Vector3(fixedCoord, 0.88f, mid);
            Vector3 railPos2 = alongX ? new Vector3(mid, 0.44f, fixedCoord) : new Vector3(fixedCoord, 0.44f, mid);
            Vector3 railScale = alongX ? new Vector3(len, 0.12f, 0.08f) : new Vector3(0.08f, 0.12f, len);
            CreateBox("FenceRailTop", parent, railPos1, railScale, _woodLightMat, collider: true);
            CreateBox("FenceRailMid", parent, railPos2, railScale, _woodLightMat, collider: false);

            int posts = Mathf.Max(2, Mathf.CeilToInt(len / 3.4f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float c = Mathf.Lerp(s0, s1, i / (float)(posts - 1));
                Vector3 p = alongX ? new Vector3(c, 0.58f, fixedCoord) : new Vector3(fixedCoord, 0.58f, c);
                CreateBox("FencePost", parent, p, new Vector3(0.16f, 1.16f, 0.16f), _woodTimberMat, collider: false);
            }
        }

        private void SpawnHayBale(Transform parent, Vector3 pos)
        {
            CreateCylinder("HayBale", parent, pos, new Vector3(1.15f, 0.65f, 1.15f), _hayGoldMat, collider: true).transform.localRotation = Quaternion.Euler(0f, 25f, 90f);
        }

        private void BuildGazebo(Transform parent, Vector3 pos)
        {
            Transform g = new GameObject("ScenicGazebo").transform;
            g.SetParent(parent, false);
            g.position = pos;
            CreateBox("Deck", g, new Vector3(0f, 0.10f, 0f), new Vector3(4.8f, 0.20f, 4.8f), _woodLightMat, collider: true);
            Vector3[] posts = { new Vector3(-2.1f, 1.55f, -2.1f), new Vector3(2.1f, 1.55f, -2.1f), new Vector3(-2.1f, 1.55f, 2.1f), new Vector3(2.1f, 1.55f, 2.1f) };
            for (int i = 0; i < posts.Length; i++) CreateBox("Post", g, posts[i], new Vector3(0.18f, 2.9f, 0.18f), _woodTimberMat, collider: true);
            CreateBox("Roof", g, new Vector3(0f, 3.12f, 0f), new Vector3(5.6f, 0.28f, 5.6f), _roofTileMat, collider: false);
            CreateBox("Table", g, new Vector3(0f, 0.72f, 0f), new Vector3(1.5f, 0.12f, 1.0f), _woodTimberMat, collider: true);
            CreateBox("BenchN", g, new Vector3(0f, 0.45f,  1.2f), new Vector3(1.8f, 0.12f, 0.45f), _woodLightMat, collider: false);
            CreateBox("BenchS", g, new Vector3(0f, 0.45f, -1.2f), new Vector3(1.8f, 0.12f, 0.45f), _woodLightMat, collider: false);
        }

        private void SpawnPineTree(Transform parent, Vector3 pos, float scale)
        {
            Transform t = new GameObject("AydosPine").transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.localScale = Vector3.one * scale;
            CreateCylinder("Trunk", t, new Vector3(0f, 1.3f, 0f), new Vector3(0.45f, 1.3f, 0.45f), _woodTimberMat, collider: false);
            CreateSphere("Tier1", t, new Vector3(0f, 2.7f, 0f), new Vector3(2.9f, 2.0f, 2.9f), _pineDarkMat);
            CreateSphere("Tier2", t, new Vector3(0f, 4.1f, 0f), new Vector3(2.2f, 1.8f, 2.2f), _leafBrightMat);
            CreateSphere("Tier3", t, new Vector3(0f, 5.2f, 0f), new Vector3(1.3f, 1.4f, 1.3f), _pineDarkMat);
        }

        private void SpawnBlossomTree(Transform parent, Vector3 pos, float scale)
        {
            Transform t = new GameObject("SpringBlossomTree").transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.localScale = Vector3.one * scale;
            CreateCylinder("Trunk", t, new Vector3(0f, 1.4f, 0f), new Vector3(0.42f, 1.4f, 0.42f), _woodTimberMat, collider: false);
            CreateSphere("CrownA", t, new Vector3(0f, 3.2f, 0f), new Vector3(3.1f, 2.2f, 3.1f), _blossomPinkMat);
            CreateSphere("CrownB", t, new Vector3(0.8f, 3.7f, 0.5f), new Vector3(2.0f, 1.7f, 2.0f), _blossomPinkMat);
            CreateSphere("CrownC", t, new Vector3(-0.7f, 3.5f, -0.5f), new Vector3(1.9f, 1.6f, 1.9f), _blossomPinkMat);
        }

        private void SpawnFlowerBed(Transform parent, Vector3 pos, float yaw)
        {
            Transform fb = new GameObject("ScenicFlowerBed").transform;
            fb.SetParent(parent, false);
            fb.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            CreateBox("PlanterBorder", fb, new Vector3(0f, 0.16f, 0f), new Vector3(3.6f, 0.32f, 1.2f), _stonePathMat, collider: false);
            CreateBox("PlanterSoil", fb, new Vector3(0f, 0.28f, 0f), new Vector3(3.2f, 0.12f, 0.9f), _wetSoilMat, collider: false);
            Material[] fMats = { _flowerRedMat, _flowerYellowMat, _flowerPurpleMat };
            for (int i = 0; i < 6; i++)
            {
                float fx = -1.25f + i * 0.50f;
                CreateSphere($"Leaf_{i}", fb, new Vector3(fx, 0.42f, 0f), new Vector3(0.42f, 0.24f, 0.42f), _leafBrightMat);
                CreateSphere($"Bloom_{i}", fb, new Vector3(fx, 0.56f, (i % 2 == 0 ? 0.10f : -0.10f)), new Vector3(0.24f, 0.18f, 0.24f), fMats[i % fMats.Length]);
            }
        }

        private void SpawnFarmLantern(Transform parent, Vector3 pos)
        {
            Transform l = new GameObject("FarmLanternPost").transform;
            l.SetParent(parent, false);
            l.position = pos;
            CreateCylinder("Post", l, new Vector3(0f, 2.2f, 0f), new Vector3(0.16f, 2.2f, 0.16f), _woodTimberMat, collider: false);
            CreateBox("Arm", l, new Vector3(0f, 4.25f, 0.35f), new Vector3(0.12f, 0.12f, 0.8f), _woodTimberMat, collider: false);
            SpawnHangingLantern(l, new Vector3(0f, 4.0f, 0.65f));
        }

        private void SpawnHangingLantern(Transform parent, Vector3 localPos)
        {
            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "WarmFarmLantern";
            bulb.transform.SetParent(parent, false);
            bulb.transform.localPosition = localPos;
            bulb.transform.localScale = Vector3.one * 0.32f;
            Destroy(bulb.GetComponent<Collider>());
            MeshRenderer mr = bulb.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _whiteTrimMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            Light pl = bulb.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(1.0f, 0.80f, 0.46f);
            pl.range = 28f;
            pl.intensity = 26f;
            pl.shadows = LightShadows.None;
            pl.enabled = false;

            NightLight nl = bulb.AddComponent<NightLight>();
            nl.Configure(pl, new[] { mr }, new Color(1.0f, 0.82f, 0.46f) * 3.8f);
            if (NeighborhoodNightLighting.Instance != null)
            {
                NeighborhoodNightLighting.Instance.RegisterDynamicOutdoorLight(nl);
            }
        }

        private void CreateWorldSign(Transform parent, string text, Vector3 pos, float yaw, Material postMat, Color boardCol)
        {
            Transform s = new GameObject($"Sign_{text}").transform;
            s.SetParent(parent, false);
            s.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            CreateCylinder("Post", s, new Vector3(0f, 1.05f, 0f), new Vector3(0.14f, 1.05f, 0.14f), postMat, collider: false);
            Material bm = MakeMat(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"), "MAT_SignBoard", boardCol, 0.25f);
            CreateBox("Board", s, new Vector3(0f, 2.0f, 0f), new Vector3(2.8f, 0.65f, 0.10f), bm, collider: false);
            GameObject to = new GameObject("Label");
            to.transform.SetParent(s, false);
            to.transform.localPosition = new Vector3(0f, 2.0f, -0.07f);
            TextMesh tm = to.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = 0.048f;
            tm.fontSize = 40;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (!collider) Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (!collider) Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject CreateSphere(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static Material MakeMat(Shader sh, string name, Color col, float smoothness)
        {
            Material m = new Material(sh) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }
    }
}
