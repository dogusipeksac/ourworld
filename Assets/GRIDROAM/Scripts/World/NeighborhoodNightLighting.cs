using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Complete mobile-optimized Istanbul evening & night street lighting manager:
    /// - Adds iOS shader-strip-proof glowing luminaire bulbs & soft warm asphalt light pools to all street lamps
    /// - Spawns grounded galvanized streetlamp poles & stadium floodlight masts (never floating mid-air orbs)
    ///   across Selman St., Gürsu/Çiçekçi residential lanes, sports courts, pool promenade, plazas, and avenues
    /// - Maintains a smooth player-following soft nocturnal visibility fill light so every alley, courtyard,
    ///   farm pasture, and lakeside grove remains clearly readable at night on both Low and Medium profiles
    /// - Lights up ~78% of Sultanbeyli/Hamidiye apartment windows with cozy warm amber/cream glow at night
    /// - Prioritizes the closest real PointLights around the player/camera for crisp mobile performance.
    /// </summary>
    public sealed class NeighborhoodNightLighting : MonoBehaviour
    {
        public static NeighborhoodNightLighting Instance { get; private set; }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private readonly List<NightLight> _managedOutdoorLights = new List<NightLight>(128);
        private readonly List<(NightLight nl, float distSq)> _sortBuffer = new List<(NightLight, float)>(128);
        private readonly List<GameObject> _nightOnlyVisuals = new List<GameObject>(220);

        private struct WindowRendererEntry
        {
            public MeshRenderer Renderer;
            public Material[] DayMaterials;
            public Material[] NightMaterials;
        }
        private readonly List<WindowRendererEntry> _windowEntries = new List<WindowRendererEntry>(160);

        private Material _bulbWarmMat;
        private Material _bulbCoolMat;
        private Material _poolWarmMat;
        private Material _poolCoolMat;
        private Material _poleMetalMat;
        private Texture2D _radialPoolTex;
        private readonly List<Material> _runtimeWindowMats = new List<Material>(8);
        private Mesh _sphereMesh;
        private Mesh _quadMesh;
        private Mesh _cylinderMesh;
        private Mesh _cubeMesh;

        private Light _playerNightFillLight;
        private bool _assetsBuilt;
        private bool _isNightApplied;
        private bool _initialized;
        private bool _lateSweepDone;
        private float _lateSweepTimer = 0.65f;
        private float _cullTimer;
        private Transform _generatedRoot;

        public int ActiveOutdoorLightCount { get; private set; }
        public int LitWindowRendererCount => _windowEntries.Count;
        public int TotalManagedStreetLights => _managedOutdoorLights.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_NeighborhoodNightLighting");
            go.AddComponent<NeighborhoodNightLighting>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureAssetsAndRoot();
        }

        private void Start()
        {
            EnsureAssetsAndRoot();

            CollectAndDecorateExistingStreetLights();
            FillDarkStreetAndCourtGaps();
            CollectApartmentWindowRenderers();

            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.OnNightStateChanged += HandleNightStateChanged;
            }
            if (GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged += HandleTierChanged;
            }

            _initialized = true;
            bool nightNow = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
            ApplyNightVisualState(nightNow, force: true);
            UpdatePlayerNightFillLight();
            RefreshLightDistancePriority();
        }

        private void OnDestroy()
        {
            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.OnNightStateChanged -= HandleNightStateChanged;
            }
            if (GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged -= HandleTierChanged;
            }

            // Always restore original daytime material arrays so scene assets stay untouched
            for (int i = 0; i < _windowEntries.Count; i++)
            {
                if (_windowEntries[i].Renderer != null && _windowEntries[i].DayMaterials != null)
                {
                    _windowEntries[i].Renderer.sharedMaterials = _windowEntries[i].DayMaterials;
                }
            }

            for (int i = 0; i < _runtimeWindowMats.Count; i++)
            {
                if (_runtimeWindowMats[i] != null) Destroy(_runtimeWindowMats[i]);
            }
            if (_bulbWarmMat != null) Destroy(_bulbWarmMat);
            if (_bulbCoolMat != null) Destroy(_bulbCoolMat);
            if (_poolWarmMat != null) Destroy(_poolWarmMat);
            if (_poolCoolMat != null) Destroy(_poolCoolMat);
            if (_poleMetalMat != null) Destroy(_poleMetalMat);
            if (_radialPoolTex != null) Destroy(_radialPoolTex);

            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_initialized) return;

            // One-shot late sweep catches any NightLights spawned by other runtime bootstraps in Start()
            if (!_lateSweepDone)
            {
                _lateSweepTimer -= Time.unscaledDeltaTime;
                if (_lateSweepTimer <= 0f)
                {
                    _lateSweepDone = true;
                    CollectAndDecorateExistingStreetLights();
                    RefreshLightDistancePriority();
                }
            }

            UpdatePlayerNightFillLight();

            if (DayNightCycle.Instance == null || !DayNightCycle.Instance.IsNight) return;

            _cullTimer -= Time.unscaledDeltaTime;
            if (_cullTimer <= 0f)
            {
                _cullTimer = 0.25f;
                RefreshLightDistancePriority();
            }
        }

        private void HandleNightStateChanged(bool isNight)
        {
            ApplyNightVisualState(isNight, force: false);
            UpdatePlayerNightFillLight();
            RefreshLightDistancePriority();
        }

        private void HandleTierChanged(OurWorld.Core.GraphicsTier _)
        {
            RefreshLightDistancePriority();
        }

        private void EnsureAssetsAndRoot()
        {
            if (_assetsBuilt) return;
            _assetsBuilt = true;

            _generatedRoot = new GameObject("RuntimeNightStreetLights").transform;
            _generatedRoot.SetParent(transform, false);

            BuildSharedVisualAssets();
            CreatePlayerNightFillLight();
        }

        private void CreatePlayerNightFillLight()
        {
            GameObject fillGo = new GameObject("PlayerNocturnalVisibilityFill");
            fillGo.transform.SetParent(_generatedRoot, false);
            _playerNightFillLight = fillGo.AddComponent<Light>();
            _playerNightFillLight.type = LightType.Point;
            _playerNightFillLight.color = new Color(0.96f, 0.94f, 0.92f);
            _playerNightFillLight.range = 40f;
            _playerNightFillLight.intensity = 0f;
            _playerNightFillLight.shadows = LightShadows.None;
            _playerNightFillLight.enabled = false;
        }

        private void UpdatePlayerNightFillLight()
        {
            if (_playerNightFillLight == null) return;

            float nf = DayNightCycle.Instance != null ? DayNightCycle.Instance.NightFactor : 0f;
            bool active = nf > 0.02f;
            _playerNightFillLight.enabled = active;
            if (!active) return;

            Vector3 focusPos = GetFocusPosition();
            _playerNightFillLight.transform.position = focusPos + new Vector3(0f, 6.5f, 0f);
            _playerNightFillLight.intensity = 22f * nf;
        }

        private static Vector3 GetFocusPosition()
        {
            if (PlayerModeController.Instance != null)
            {
                return PlayerModeController.Instance.ActiveWorldPosition;
            }
            if (Camera.main != null)
            {
                return Camera.main.transform.position;
            }
            return Vector3.zero;
        }

        private void BuildSharedVisualAssets()
        {
            // Grab Unity's built-in primitive meshes cleanly without keeping temp GameObjects
            GameObject tmpSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphereMesh = tmpSphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmpSphere);

            GameObject tmpQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quadMesh = tmpQuad.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmpQuad);

            GameObject tmpCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _cylinderMesh = tmpCyl.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmpCyl);

            GameObject tmpCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cubeMesh = tmpCube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmpCube);

            _radialPoolTex = BuildRadialGlowTexture(64);

            // Use URP/Lit for emissive bulbs and poles (always included in scene builds -> 100% iOS shader-strip-proof!)
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            _bulbWarmMat = new Material(litShader) { name = "MAT_RuntimeStreetBulb_Warm" };
            SetColorProp(_bulbWarmMat, new Color(1.0f, 0.92f, 0.68f, 1f));
            _bulbWarmMat.EnableKeyword("_EMISSION");
            if (_bulbWarmMat.HasProperty(EmissionColorId))
            {
                _bulbWarmMat.SetColor(EmissionColorId, new Color(1.0f, 0.84f, 0.48f, 1f) * 3.4f);
            }

            _bulbCoolMat = new Material(litShader) { name = "MAT_RuntimeStreetBulb_Cool" };
            SetColorProp(_bulbCoolMat, new Color(0.94f, 0.98f, 1.0f, 1f));
            _bulbCoolMat.EnableKeyword("_EMISSION");
            if (_bulbCoolMat.HasProperty(EmissionColorId))
            {
                _bulbCoolMat.SetColor(EmissionColorId, new Color(0.88f, 0.95f, 1.0f, 1f) * 3.6f);
            }

            _poleMetalMat = new Material(litShader) { name = "MAT_RuntimeStreetPole_Steel" };
            SetColorProp(_poleMetalMat, new Color(0.24f, 0.26f, 0.29f, 1f));
            if (_poleMetalMat.HasProperty("_Smoothness")) _poleMetalMat.SetFloat("_Smoothness", 0.42f);
            if (_poleMetalMat.HasProperty("_Metallic")) _poleMetalMat.SetFloat("_Metallic", 0.55f);

            // Soft additive ground pools: prefer URP/Unlit, with built-in Sprites/Default fallback (never stripped on iOS!)
            Shader poolShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (poolShader == null) poolShader = Shader.Find("Sprites/Default");
            if (poolShader == null) poolShader = litShader;

            _poolWarmMat = CreateAdditivePoolMaterial(poolShader, "MAT_RuntimeStreetPool_Warm", new Color(1.0f, 0.78f, 0.38f, 0.30f));
            _poolCoolMat = CreateAdditivePoolMaterial(poolShader, "MAT_RuntimeStreetPool_Cool", new Color(0.84f, 0.93f, 1.0f, 0.24f));
        }

        private Material CreateAdditivePoolMaterial(Shader shader, string matName, Color tint)
        {
            Material m = new Material(shader) { name = matName };
            if (m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, _radialPoolTex);
            m.mainTexture = _radialPoolTex;
            SetColorProp(m, tint);

            // Configure soft transparent additive light pool (zero z-write, zero shadow)
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f); // Additive
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        private static Texture2D BuildRadialGlowTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - half) / half;
                    float ny = (y - half) / half;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float falloff = Mathf.Clamp01(1f - r);
                    float a = falloff * falloff * (0.72f + 0.28f * falloff);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static void SetColorProp(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
        }

        private void CollectAndDecorateExistingStreetLights()
        {
            NightLight[] allLights = FindObjectsByType<NightLight>(FindObjectsInactive.Include);
            for (int i = 0; i < allLights.Length; i++)
            {
                RegisterDynamicOutdoorLight(allLights[i]);
            }
        }

        public void RegisterDynamicOutdoorLight(NightLight nl)
        {
            if (nl == null || !nl.IsManagedOutdoorLight || nl.TargetLight == null) return;
            if (_managedOutdoorLights.Contains(nl)) return;

            EnsureAssetsAndRoot();
            _managedOutdoorLights.Add(nl);

            if (_generatedRoot != null && _sphereMesh != null && _quadMesh != null)
            {
                Vector3 lampPos = nl.TargetLight.transform.position;
                bool isCool = nl.TargetLight.color.b > 0.85f && nl.TargetLight.color.g > 0.85f;
                // Only attach a glowing bulb sphere to physical lamp head fixtures (NightPoolLight on Sultanbeyli_StreetLamp);
                // bare legacy fill lights (e.g. Selman_StreetLight_XX beside power poles) stay orb-free so nothing floats in mid-air!
                bool hasPhysicalFixtureHead = nl.gameObject.name == "NightPoolLight";
                bool overLakeWater = IsOverSultanbeyliLake(lampPos);

                if (lampPos.y >= 2.2f && lampPos.y <= 11.5f)
                {
                    int beforeCount = _nightOnlyVisuals.Count;
                    SpawnLampBulbAndPool(
                        lampPos,
                        isCool,
                        poolDiameter: isCool ? 16f : 16f,
                        spawnBulb: hasPhysicalFixtureHead,
                        spawnPool: !overLakeWater,
                        bulbScale: isCool ? 0.30f : 0.22f);
                    if (_initialized && _isNightApplied)
                    {
                        for (int k = beforeCount; k < _nightOnlyVisuals.Count; k++)
                        {
                            if (_nightOnlyVisuals[k] != null) _nightOnlyVisuals[k].SetActive(true);
                        }
                    }
                }
            }
        }

        private static bool IsOverSultanbeyliLake(Vector3 pos)
        {
            // Sultanbeyli Lake water cylinder is centered at (-198, 0.04, 0) with diameter 56x42m
            float dx = (pos.x + 198f) / 29f;
            float dz = pos.z / 22f;
            return (dx * dx + dz * dz) <= 1.05f;
        }

        private void FillDarkStreetAndCourtGaps()
        {
            Color warmSodium = new Color(1.0f, 0.82f, 0.48f);
            Color coolStadium = new Color(0.90f, 0.95f, 1.0f);

            // Grounded streetlamp poles placed neatly on sidewalks, residential paths & plazas (NEVER floating in mid-air!)
            (Vector3 poleBase, float armYaw)[] warmStreetPoles =
            {
                // Selman Sokağı west/east outer sidewalk extensions (z = ±5.55f)
                (new Vector3(-96f, 0f,  5.55f), 180f),
                (new Vector3(-84f, 0f, -5.55f),   0f),
                (new Vector3( -4f, 0f, -5.55f),   0f),
                (new Vector3( 80f, 0f, -5.55f),   0f),
                (new Vector3(106f, 0f,  5.55f), 180f),

                // Gürsu Sokağı & Çiçekçi Sokağı residential corridor (illuminates No:48 spawn, houses & lawn!)
                (new Vector3(-48f, 0f, -18.5f),   0f),
                (new Vector3(-24f, 0f, -18.5f),   0f),
                (new Vector3( -2f, 0f, -18.5f),   0f),
                (new Vector3( 20f, 0f, -18.5f),   0f),
                (new Vector3( 42f, 0f, -18.5f),   0f),
                (new Vector3( 66f, 0f, -18.5f),   0f),

                // Residential park lawn & Pool/Sports walkway (eliminates dark shadows around pool & gardens!)
                (new Vector3(-28f, 0f, -31.5f),   0f),
                (new Vector3( -3f, 0f, -31.5f),   0f),
                (new Vector3( 28f, 0f, -31.5f),   0f),
                (new Vector3( 52f, 0f, -31.5f),   0f),

                // Hz. Osman Camii Courtyard & South Promenade
                (new Vector3( 76f, 0f, -36f),    90f),
                (new Vector3( 88f, 0f, -52f),   -90f),

                // Fatih Bulvarı & Clock Tower Plaza
                (new Vector3(-72f, 0f, 128f),     0f),
                (new Vector3(  0f, 0f, 126f),     0f),
                (new Vector3( 72f, 0f, 128f),     0f),

                // Atatürk & Mürşit Avenues + Northern Hamidiye Park + No:48 North Sidewalk
                (new Vector3(-42f, 0f,  26f),     0f),
                (new Vector3(  6f, 0f,  42f),   -90f),
                (new Vector3(  6f, 0f,  90f),   -90f),
                (new Vector3(-78f, 0f,  42f),    90f),
                (new Vector3( 78f, 0f,  42f),   -90f),
                (new Vector3(-78f, 0f, -72f),    90f),
                (new Vector3(  6f, 0f, -72f),   -90f),
                (new Vector3( 78f, 0f, -72f),   -90f),
                (new Vector3(  6f, 0f,-109f),   -90f)
            };

            for (int i = 0; i < warmStreetPoles.Length; i++)
            {
                var p = warmStreetPoles[i];
                Vector3 armDir = Quaternion.Euler(0f, p.armYaw, 0f) * Vector3.forward;
                Vector3 lightPos = p.poleBase + new Vector3(0f, 5.75f, 0f) + armDir * 1.15f;
                if (HasNearbyOutdoorLight(lightPos, 17.5f)) continue;
                CreateGroundedRuntimeStreetLamp($"RuntimeStreetLamp_{i:00}", p.poleBase, p.armYaw, warmSodium, range: 38f, intensity: 44f, poolDiameter: 18f);
            }

            // Grounded stadium floodlight masts along the perimeter of ALL 4 Playable Sports Courts + Open Pool!
            (string name, Vector3 mastBase, float faceYaw, float range, float intensity)[] courtMasts =
            {
                ("Floodlight_TennisCourt",     new Vector3(-50.5f, 0f, -42.2f),  90f, 44f, 54f),
                ("Floodlight_FootballPitch",   new Vector3(-14.0f, 0f, -33.6f), 180f, 48f, 60f),
                ("Floodlight_OpenPool",        new Vector3( 16.0f, 0f, -33.6f), 180f, 42f, 46f),
                ("Floodlight_BasketballCourt", new Vector3( 41.0f, 0f, -34.6f), 180f, 44f, 52f),
                ("Floodlight_VolleyballCourt", new Vector3( 59.0f, 0f, -34.6f), 180f, 44f, 52f)
            };

            for (int i = 0; i < courtMasts.Length; i++)
            {
                var cm = courtMasts[i];
                CreateGroundedFloodlightMast(cm.name, cm.mastBase, cm.faceYaw, coolStadium, cm.range, cm.intensity, poolDiameter: 22f);
            }
        }

        private bool HasNearbyOutdoorLight(Vector3 pos, float radius)
        {
            float rSq = radius * radius;
            for (int i = 0; i < _managedOutdoorLights.Count; i++)
            {
                NightLight nl = _managedOutdoorLights[i];
                if (nl == null || nl.TargetLight == null) continue;
                Vector3 d = nl.TargetLight.transform.position - pos;
                d.y = 0f;
                if (d.sqrMagnitude <= rSq) return true;
            }
            return false;
        }

        private void CreateGroundedRuntimeStreetLamp(
            string lampName,
            Vector3 poleBasePos,
            float armYaw,
            Color color,
            float range,
            float intensity,
            float poolDiameter)
        {
            GameObject root = new GameObject(lampName);
            root.transform.SetParent(_generatedRoot, false);
            root.transform.SetPositionAndRotation(poleBasePos, Quaternion.Euler(0f, armYaw, 0f));

            // Sleek galvanized steel pole + outreach arm + luminaire housing (zero colliders, grounded at y=0!)
            AddStaticPolePart("PoleShaft", root.transform, _cylinderMesh, new Vector3(0f, 2.95f, 0f), new Vector3(0.14f, 2.95f, 0.14f), _poleMetalMat);
            AddStaticPolePart("PoleBase", root.transform, _cylinderMesh, new Vector3(0f, 0.25f, 0f), new Vector3(0.24f, 0.25f, 0.24f), _poleMetalMat);
            AddStaticPolePart("Arm", root.transform, _cubeMesh, new Vector3(0f, 5.82f, 0.62f), new Vector3(0.09f, 0.09f, 1.30f), _poleMetalMat);
            AddStaticPolePart("Housing", root.transform, _cubeMesh, new Vector3(0f, 5.78f, 1.24f), new Vector3(0.28f, 0.10f, 0.44f), _poleMetalMat);

            GameObject lightGo = new GameObject("PointLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 5.65f, 1.24f);

            Light pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = color;
            pl.range = range;
            pl.intensity = intensity;
            pl.shadows = LightShadows.None;
            pl.enabled = false;

            NightLight nl = lightGo.AddComponent<NightLight>();
            nl.Configure(pl, null, Color.black);
            _managedOutdoorLights.Add(nl);

            SpawnLampBulbAndPool(lightGo.transform.position, isCool: false, poolDiameter: poolDiameter, spawnBulb: true, spawnPool: true, bulbScale: 0.24f);
        }

        private void CreateGroundedFloodlightMast(
            string mastName,
            Vector3 mastBasePos,
            float faceYaw,
            Color color,
            float range,
            float intensity,
            float poolDiameter)
        {
            GameObject root = new GameObject(mastName);
            root.transform.SetParent(_generatedRoot, false);
            root.transform.SetPositionAndRotation(mastBasePos, Quaternion.Euler(0f, faceYaw, 0f));

            // Tall slim galvanized stadium mast + crossbar + twin angled LED heads
            AddStaticPolePart("MastShaft", root.transform, _cylinderMesh, new Vector3(0f, 4.35f, 0f), new Vector3(0.18f, 4.35f, 0.18f), _poleMetalMat);
            AddStaticPolePart("Crossbar", root.transform, _cubeMesh, new Vector3(0f, 8.65f, 0.15f), new Vector3(1.55f, 0.12f, 0.14f), _poleMetalMat);
            AddStaticPolePart("HeadL", root.transform, _cubeMesh, new Vector3(-0.52f, 8.58f, 0.30f), new Vector3(0.42f, 0.24f, 0.18f), _poleMetalMat);
            AddStaticPolePart("HeadR", root.transform, _cubeMesh, new Vector3( 0.52f, 8.58f, 0.30f), new Vector3(0.42f, 0.24f, 0.18f), _poleMetalMat);

            // Light projects forward onto the court surface while glowing heads sit right on the crossbar
            GameObject lightGo = new GameObject("FloodlightPoint");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 8.2f, 4.8f);

            Light pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = color;
            pl.range = range;
            pl.intensity = intensity;
            pl.shadows = LightShadows.None;
            pl.enabled = false;

            NightLight nl = lightGo.AddComponent<NightLight>();
            nl.Configure(pl, null, Color.black);
            _managedOutdoorLights.Add(nl);

            // Glowing LED panels attached to the mast heads (never floating!) + wide soft court light pool
            Vector3 headLWorld = root.transform.TransformPoint(new Vector3(-0.52f, 8.52f, 0.42f));
            Vector3 headRWorld = root.transform.TransformPoint(new Vector3( 0.52f, 8.52f, 0.42f));
            SpawnLampBulbAndPool(headLWorld, isCool: true, poolDiameter: 0f, spawnBulb: true, spawnPool: false, bulbScale: 0.28f);
            SpawnLampBulbAndPool(headRWorld, isCool: true, poolDiameter: 0f, spawnBulb: true, spawnPool: false, bulbScale: 0.28f);
            SpawnLampBulbAndPool(lightGo.transform.position, isCool: true, poolDiameter: poolDiameter, spawnBulb: false, spawnPool: true, bulbScale: 0f);
        }

        private void AddStaticPolePart(string partName, Transform parent, Mesh mesh, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject part = new GameObject(partName);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            MeshFilter mf = part.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = part.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }

        private void SpawnLampBulbAndPool(
            Vector3 lightWorldPos,
            bool isCool,
            float poolDiameter,
            bool spawnBulb,
            bool spawnPool,
            float bulbScale)
        {
            if (spawnBulb && bulbScale > 0.01f)
            {
                GameObject bulb = new GameObject("NightBulbGlow");
                bulb.transform.SetParent(_generatedRoot, false);
                bulb.transform.position = lightWorldPos;
                bulb.transform.localScale = Vector3.one * bulbScale;
                MeshFilter bf = bulb.AddComponent<MeshFilter>();
                bf.sharedMesh = _sphereMesh;
                MeshRenderer br = bulb.AddComponent<MeshRenderer>();
                br.sharedMaterial = isCool ? _bulbCoolMat : _bulbWarmMat;
                br.shadowCastingMode = ShadowCastingMode.Off;
                br.receiveShadows = false;
                bulb.SetActive(false);
                _nightOnlyVisuals.Add(bulb);
            }

            if (spawnPool && poolDiameter > 0.5f)
            {
                GameObject pool = new GameObject("NightAsphaltPool");
                pool.transform.SetParent(_generatedRoot, false);
                pool.transform.SetPositionAndRotation(
                    new Vector3(lightWorldPos.x, 0.055f, lightWorldPos.z),
                    Quaternion.Euler(90f, 0f, 0f));
                pool.transform.localScale = new Vector3(poolDiameter, poolDiameter, 1f);
                MeshFilter pf = pool.AddComponent<MeshFilter>();
                pf.sharedMesh = _quadMesh;
                MeshRenderer pr = pool.AddComponent<MeshRenderer>();
                pr.sharedMaterial = isCool ? _poolCoolMat : _poolWarmMat;
                pr.shadowCastingMode = ShadowCastingMode.Off;
                pr.receiveShadows = false;
                pool.SetActive(false);
                _nightOnlyVisuals.Add(pool);
            }
        }

        private void CollectApartmentWindowRenderers()
        {
            MeshRenderer[] allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
            Dictionary<Material, (Material warmA, Material warmB)> matCache = new Dictionary<Material, (Material, Material)>();

            for (int i = 0; i < allRenderers.Length; i++)
            {
                MeshRenderer mr = allRenderers[i];
                if (mr == null) continue;
                if (mr.GetComponent<NightLight>() != null) continue;
                if (mr.GetComponentInParent<SedanVehicleController>() != null) continue;
                if (mr.GetComponentInParent<AmbientTrafficManager>() != null) continue;

                Material[] shared = mr.sharedMaterials;
                if (shared == null || shared.Length == 0) continue;

                bool hasWindowGlass = false;
                for (int m = 0; m < shared.Length; m++)
                {
                    if (shared[m] != null && shared[m].name.IndexOf("WindowGlass", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasWindowGlass = true;
                        break;
                    }
                }
                if (!hasWindowGlass) continue;

                // Deterministic hash by world position: ~78% of buildings glow warmly, ~22% stay dark/asleep
                Vector3 p = mr.bounds.center;
                int hash = Mathf.Abs(Mathf.RoundToInt(p.x * 13.7f) * 31 + Mathf.RoundToInt(p.z * 17.3f) * 17 + Mathf.RoundToInt(p.y * 7.1f));
                if (hash % 100 >= 78) continue;

                bool useWarmAmber = (hash % 3) != 0;
                Material[] dayCopy = (Material[])shared.Clone();
                Material[] nightCopy = (Material[])shared.Clone();

                for (int m = 0; m < nightCopy.Length; m++)
                {
                    Material orig = nightCopy[m];
                    if (orig == null || orig.name.IndexOf("WindowGlass", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                    if (!matCache.TryGetValue(orig, out var pair))
                    {
                        Material warmA = new Material(orig) { name = orig.name + "_NightWarmAmber" };
                        warmA.EnableKeyword("_EMISSION");
                        warmA.SetColor(BaseColorId, new Color(1.0f, 0.86f, 0.54f, 1f));
                        warmA.SetColor(EmissionColorId, new Color(1.0f, 0.78f, 0.40f, 1f) * 2.25f);

                        Material warmB = new Material(orig) { name = orig.name + "_NightWarmCream" };
                        warmB.EnableKeyword("_EMISSION");
                        warmB.SetColor(BaseColorId, new Color(0.96f, 0.92f, 0.76f, 1f));
                        warmB.SetColor(EmissionColorId, new Color(1.0f, 0.90f, 0.66f, 1f) * 1.90f);

                        pair = (warmA, warmB);
                        matCache[orig] = pair;
                        _runtimeWindowMats.Add(warmA);
                        _runtimeWindowMats.Add(warmB);
                    }

                    nightCopy[m] = useWarmAmber ? pair.warmA : pair.warmB;
                }

                _windowEntries.Add(new WindowRendererEntry
                {
                    Renderer = mr,
                    DayMaterials = dayCopy,
                    NightMaterials = nightCopy
                });
            }
        }

        private void ApplyNightVisualState(bool isNight, bool force)
        {
            if (!force && _isNightApplied == isNight) return;
            _isNightApplied = isNight;

            for (int i = 0; i < _nightOnlyVisuals.Count; i++)
            {
                if (_nightOnlyVisuals[i] != null)
                {
                    _nightOnlyVisuals[i].SetActive(isNight);
                }
            }

            for (int i = 0; i < _windowEntries.Count; i++)
            {
                WindowRendererEntry e = _windowEntries[i];
                if (e.Renderer == null) continue;
                e.Renderer.sharedMaterials = isNight ? e.NightMaterials : e.DayMaterials;
            }
        }

        public void RefreshLightDistancePriority()
        {
            bool nightNow = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
            if (!nightNow)
            {
                ActiveOutdoorLightCount = 0;
                for (int i = 0; i < _managedOutdoorLights.Count; i++)
                {
                    if (_managedOutdoorLights[i] != null)
                    {
                        _managedOutdoorLights[i].SetDistanceCulled(false);
                    }
                }
                return;
            }

            Vector3 focusPos = GetFocusPosition();

            bool isLowTier = GraphicsProfileManager.Instance != null &&
                             GraphicsProfileManager.Instance.CurrentTier == OurWorld.Core.GraphicsTier.Low;
            int maxActiveLights = isLowTier ? 14 : 32;
            const float maxRangeSq = 135f * 135f;

            _sortBuffer.Clear();
            for (int i = 0; i < _managedOutdoorLights.Count; i++)
            {
                NightLight nl = _managedOutdoorLights[i];
                if (nl == null || nl.TargetLight == null) continue;
                Vector3 d = nl.TargetLight.transform.position - focusPos;
                d.y = 0f;
                _sortBuffer.Add((nl, d.sqrMagnitude));
            }

            _sortBuffer.Sort((a, b) => a.distSq.CompareTo(b.distSq));

            int active = 0;
            for (int i = 0; i < _sortBuffer.Count; i++)
            {
                var (nl, distSq) = _sortBuffer[i];
                bool shouldEnable = active < maxActiveLights && distSq <= maxRangeSq;
                nl.SetDistanceCulled(!shouldEnable);
                if (shouldEnable) active++;
            }

            ActiveOutdoorLightCount = active;
        }
    }
}
