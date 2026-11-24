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
    /// - Adds crisp glowing luminaire bulbs & soft warm asphalt light pools to all street lamps
    /// - Fills dark street corridors, plazas, and all 4 sports courts with real URP lights + stadium floodlights
    /// - Lights up ~72% of Sultanbeyli/Hamidiye apartment windows with cozy warm amber/cream glow at evening/night
    /// - Prioritizes the closest real PointLights around the camera/player so both Medium (Forward+) and Low (Forward)
    ///   render brightly and smoothly without Unity dropping nearby street lights on long asphalt road meshes.
    /// </summary>
    public sealed class NeighborhoodNightLighting : MonoBehaviour
    {
        public static NeighborhoodNightLighting Instance { get; private set; }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private readonly List<NightLight> _managedOutdoorLights = new List<NightLight>(96);
        private readonly List<(NightLight nl, float distSq)> _sortBuffer = new List<(NightLight, float)>(96);
        private readonly List<GameObject> _nightOnlyVisuals = new List<GameObject>(160);

        private struct WindowRendererEntry
        {
            public MeshRenderer Renderer;
            public Material[] DayMaterials;
            public Material[] NightMaterials;
        }
        private readonly List<WindowRendererEntry> _windowEntries = new List<WindowRendererEntry>(128);

        private Material _bulbWarmMat;
        private Material _bulbCoolMat;
        private Material _poolWarmMat;
        private Material _poolCoolMat;
        private Texture2D _radialPoolTex;
        private readonly List<Material> _runtimeWindowMats = new List<Material>(8);
        private Mesh _sphereMesh;
        private Mesh _quadMesh;

        private bool _isNightApplied;
        private bool _initialized;
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
        }

        private void Start()
        {
            BuildSharedVisualAssets();
            _generatedRoot = new GameObject("RuntimeNightStreetLights").transform;
            _generatedRoot.SetParent(transform, false);

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
            if (_radialPoolTex != null) Destroy(_radialPoolTex);

            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_initialized) return;
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
            RefreshLightDistancePriority();
        }

        private void HandleTierChanged(OurWorld.Core.GraphicsTier _)
        {
            RefreshLightDistancePriority();
        }

        private void BuildSharedVisualAssets()
        {
            // Grab Unity's built-in sphere & quad meshes cleanly without keeping temp GameObjects
            GameObject tmpSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphereMesh = tmpSphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmpSphere);

            GameObject tmpQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quadMesh = tmpQuad.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmpQuad);

            _radialPoolTex = BuildRadialGlowTexture(64);

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");

            _bulbWarmMat = new Material(unlitShader) { name = "MAT_RuntimeStreetBulb_Warm" };
            SetColorProp(_bulbWarmMat, new Color(1.0f, 0.90f, 0.62f, 1f) * 1.45f);

            _bulbCoolMat = new Material(unlitShader) { name = "MAT_RuntimeStreetBulb_Cool" };
            SetColorProp(_bulbCoolMat, new Color(0.92f, 0.96f, 1.0f, 1f) * 1.55f);

            _poolWarmMat = CreateAdditivePoolMaterial(unlitShader, "MAT_RuntimeStreetPool_Warm", new Color(1.0f, 0.78f, 0.42f, 0.28f));
            _poolCoolMat = CreateAdditivePoolMaterial(unlitShader, "MAT_RuntimeStreetPool_Cool", new Color(0.84f, 0.92f, 1.0f, 0.24f));
        }

        private Material CreateAdditivePoolMaterial(Shader shader, string matName, Color tint)
        {
            Material m = new Material(shader) { name = matName };
            m.SetTexture(BaseMapId, _radialPoolTex);
            m.mainTexture = _radialPoolTex;
            SetColorProp(m, tint);

            // Configure URP Unlit for soft transparent additive light pool (zero z-write, zero shadow)
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

            _managedOutdoorLights.Add(nl);

            if (_generatedRoot != null && _sphereMesh != null && _quadMesh != null)
            {
                Vector3 lampPos = nl.TargetLight.transform.position;
                bool isCool = nl.TargetLight.color.b > 0.85f && nl.TargetLight.color.g > 0.85f;
                if (lampPos.y >= 2.2f && lampPos.y <= 11.5f)
                {
                    int beforeCount = _nightOnlyVisuals.Count;
                    SpawnLampBulbAndPool(lampPos, isCool, poolDiameter: isCool ? 16f : 15f);
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

        private void FillDarkStreetAndCourtGaps()
        {
            Color warmSodium = new Color(1.0f, 0.80f, 0.46f);
            Color coolStadium = new Color(0.90f, 0.95f, 1.0f);

            // Key street intersections, sidewalks & plazas across Hamidiye Mahallesi
            Vector3[] warmStreetPoints =
            {
                // Full Selman Sokağı corridor (fills any dark stretches between poles)
                new Vector3(-92f, 6.1f,  0f),
                new Vector3(-68f, 6.1f,  0f),
                new Vector3(-36f, 6.1f,  0f),
                new Vector3( -8f, 6.1f,  0f),
                new Vector3( 14f, 6.1f,  0f),
                new Vector3( 36f, 6.1f,  0f),
                new Vector3( 62f, 6.1f,  0f),
                new Vector3( 88f, 6.1f,  0f),
                new Vector3(112f, 6.1f,  0f),

                // Gürsu Sokağı & Çiçekçi Sokağı (south of Selman Sk. toward sports & homes)
                new Vector3(-42f, 6.1f, -20f),
                new Vector3(-14f, 6.1f, -20f),
                new Vector3( 14f, 6.1f, -20f),
                new Vector3( 42f, 6.1f, -20f),
                new Vector3( 68f, 6.1f, -20f),

                // Hz. Osman Camii Courtyard & South Promenade
                new Vector3( 82f, 6.8f, -34f),
                new Vector3( 82f, 6.8f, -52f),

                // Fatih Bulvarı & Clock Tower Plaza
                new Vector3(-72f, 6.4f, 134f),
                new Vector3(  0f, 7.2f, 134f),
                new Vector3( 72f, 6.4f, 134f),

                // Atatürk & Mürşit Avenues + Northern Hamidiye Park
                new Vector3(  0f, 6.4f,  42f),
                new Vector3(  0f, 6.4f,  90f),
                new Vector3(-84f, 6.4f,  42f),
                new Vector3( 84f, 6.4f,  42f),
                new Vector3(-84f, 6.4f, -72f),
                new Vector3(  0f, 6.4f, -72f),
                new Vector3( 84f, 6.4f, -72f),
                new Vector3(  0f, 6.4f,-109f)
            };

            for (int i = 0; i < warmStreetPoints.Length; i++)
            {
                Vector3 pt = warmStreetPoints[i];
                if (HasNearbyOutdoorLight(pt, 19.5f)) continue;
                CreateRuntimeStreetLight($"RuntimeStreetGlow_{i:00}", pt, warmSodium, range: 32f, intensity: 28f, isCool: false, poolDiameter: 16f);
            }

            // Crisp night stadium floodlights over ALL 4 Playable Sports Courts + Open Pool!
            (string name, Vector3 pos, float range, float intensity)[] courtFloodlights =
            {
                ("Floodlight_TennisCourt",     new Vector3(-41.0f, 8.8f, -42.2f), 38f, 40f),
                ("Floodlight_FootballPitch",   new Vector3(-14.0f, 9.2f, -44.0f), 42f, 44f),
                ("Floodlight_BasketballCourt", new Vector3( 41.0f, 8.6f, -44.0f), 36f, 38f),
                ("Floodlight_VolleyballCourt", new Vector3( 59.0f, 8.6f, -44.0f), 36f, 38f),
                ("Floodlight_OpenPool",        new Vector3( 16.0f, 7.8f, -41.0f), 34f, 30f)
            };

            for (int i = 0; i < courtFloodlights.Length; i++)
            {
                var fl = courtFloodlights[i];
                CreateRuntimeStreetLight(fl.name, fl.pos, coolStadium, fl.range, fl.intensity, isCool: true, poolDiameter: 22f);
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

        private void CreateRuntimeStreetLight(
            string lightName,
            Vector3 worldPos,
            Color color,
            float range,
            float intensity,
            bool isCool,
            float poolDiameter)
        {
            GameObject go = new GameObject(lightName);
            go.transform.SetParent(_generatedRoot, false);
            go.transform.position = worldPos;

            Light pl = go.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = color;
            pl.range = range;
            pl.intensity = intensity;
            pl.shadows = LightShadows.None;
            pl.enabled = false;

            NightLight nl = go.AddComponent<NightLight>();
            nl.Configure(pl, null, Color.black);
            _managedOutdoorLights.Add(nl);

            SpawnLampBulbAndPool(worldPos, isCool, poolDiameter);
        }

        private void SpawnLampBulbAndPool(Vector3 lightWorldPos, bool isCool, float poolDiameter)
        {
            // 1. Glowing lamp head orb (visible looking down the street!)
            GameObject bulb = new GameObject("NightBulbGlow");
            bulb.transform.SetParent(_generatedRoot, false);
            bulb.transform.position = lightWorldPos;
            bulb.transform.localScale = Vector3.one * (isCool ? 0.52f : 0.38f);
            MeshFilter bf = bulb.AddComponent<MeshFilter>();
            bf.sharedMesh = _sphereMesh;
            MeshRenderer br = bulb.AddComponent<MeshRenderer>();
            br.sharedMaterial = isCool ? _bulbCoolMat : _bulbWarmMat;
            br.shadowCastingMode = ShadowCastingMode.Off;
            br.receiveShadows = false;
            bulb.SetActive(false);
            _nightOnlyVisuals.Add(bulb);

            // 2. Soft additive warm light pool on the asphalt/court floor right underneath
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

                // Deterministic hash by world position: ~72% of buildings glow warmly, ~28% stay dark/asleep
                Vector3 p = mr.bounds.center;
                int hash = Mathf.Abs(Mathf.RoundToInt(p.x * 13.7f) * 31 + Mathf.RoundToInt(p.z * 17.3f) * 17 + Mathf.RoundToInt(p.y * 7.1f));
                if (hash % 100 >= 72) continue;

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
                        warmA.SetColor(BaseColorId, new Color(1.0f, 0.84f, 0.50f, 1f));
                        warmA.SetColor(EmissionColorId, new Color(1.0f, 0.76f, 0.36f, 1f) * 1.95f);

                        Material warmB = new Material(orig) { name = orig.name + "_NightWarmCream" };
                        warmB.EnableKeyword("_EMISSION");
                        warmB.SetColor(BaseColorId, new Color(1.0f, 0.92f, 0.72f, 1f));
                        warmB.SetColor(EmissionColorId, new Color(1.0f, 0.88f, 0.62f, 1f) * 1.65f);

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

            Vector3 focusPos = Vector3.zero;
            if (PlayerModeController.Instance != null)
            {
                focusPos = PlayerModeController.Instance.ActiveWorldPosition;
            }
            else if (Camera.main != null)
            {
                focusPos = Camera.main.transform.position;
            }

            bool isLowTier = GraphicsProfileManager.Instance != null &&
                             GraphicsProfileManager.Instance.CurrentTier == OurWorld.Core.GraphicsTier.Low;
            int maxActiveLights = isLowTier ? 8 : 24;
            const float maxRangeSq = 98f * 98f;

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
