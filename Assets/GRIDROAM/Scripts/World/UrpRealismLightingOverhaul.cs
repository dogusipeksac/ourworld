using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OurWorld.World
{
    /// <summary>
    /// URP Lighting, SSAO, ACES Filmic, Reflection Probe & Clearcoat Car Paint Realism Overhaul (0 MB extra!):
    /// 1. Boosts URP Screen Space Ambient Occlusion (SSAO) at runtime on Medium tier (Intensity 0.88, Radius 0.90,
    ///    DirectLightingStrength 0.34) for grounded contact shadows under every car, bench, curb, building & NPC,
    ///    and cleanly disables SSAO on Low tier for steady mobile FPS.
    /// 2. Enables Soft Directional Shadows & guarantees ACES Filmic Tonemapping + High-Quality Controlled Bloom
    ///    on the active URP Global Volume.
    /// 3. Places 8 Box-Projected 360-degree Reflection Probes covering ALL 7 Sultanbeyli districts + Lake + Aydos Castle,
    ///    plus a time-sliced dynamic Player-Car Reflection Probe (0.0 ms steady-state cost; refreshes only on
    ///    Dawn/Day/Sunset/Night phase shifts or when driving into a new block).
    /// 4. Upgrades ALL vehicle bodies (player cars, gallery cars, traffic & customer cars) with dual-lobe URP
    ///    Clearcoat metallic automotive paint (_CLEARCOAT) and glossy reflective glass.
    /// 5. Drives realistic rain-wet asphalt & travertine plaza specular gloss across all 6 new Sultanbeyli districts!
    /// </summary>
    public sealed class UrpRealismLightingOverhaul : MonoBehaviour
    {
        public static UrpRealismLightingOverhaul Instance { get; private set; }

        private struct WetSurfaceMat
        {
            public Material Mat;
            public Color DryColor;
            public float DrySmoothness;
            public float WetSmoothness;
            public float WetDarken;
        }

        private struct DistrictProbeDef
        {
            public string Name;
            public Vector3 Pos;
            public Vector3 Size;
        }

        private static readonly DistrictProbeDef[] LandmarkProbes =
        {
            new DistrictProbeDef { Name = "Probe_Hamidiye_Center",   Pos = new Vector3(   0f,  7.5f,   32f), Size = new Vector3(195f, 48f, 225f) },
            new DistrictProbeDef { Name = "Probe_Sultanbeyli_Lake",  Pos = new Vector3(-184f,  6.5f,    0f), Size = new Vector3(115f, 42f, 115f) },
            new DistrictProbeDef { Name = "Probe_Abdurrahmangazi",   Pos = new Vector3( 180f,  8.5f,  195f), Size = new Vector3(145f, 52f, 145f) },
            new DistrictProbeDef { Name = "Probe_MehmetAkif_Metro",  Pos = new Vector3(  -6f,  8.5f,  282f), Size = new Vector3(155f, 52f, 135f) },
            new DistrictProbeDef { Name = "Probe_Hasanpasa_Park",    Pos = new Vector3(-185f,  8.0f,  195f), Size = new Vector3(145f, 52f, 145f) },
            new DistrictProbeDef { Name = "Probe_Battalgazi_Pazar",  Pos = new Vector3(-185f,  8.0f, -185f), Size = new Vector3(145f, 52f, 145f) },
            new DistrictProbeDef { Name = "Probe_MimarSinan_Park",   Pos = new Vector3( -52f,  8.0f, -282f), Size = new Vector3(145f, 52f, 145f) },
            new DistrictProbeDef { Name = "Probe_TurgutReis_Aydos",  Pos = new Vector3( 246f, 11.0f, -124f), Size = new Vector3(155f, 62f, 155f) }
        };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int ClearCoatId = Shader.PropertyToID("_ClearCoat");
        private static readonly int ClearCoatMaskId = Shader.PropertyToID("_ClearCoatMask");
        private static readonly int ClearCoatSmoothnessId = Shader.PropertyToID("_ClearCoatSmoothness");

        private readonly List<ReflectionProbe> _landmarkProbes = new List<ReflectionProbe>(8);
        private readonly List<WetSurfaceMat> _districtWetMats = new List<WetSurfaceMat>(48);
        private readonly HashSet<GameObject> _upgradedVehicleRoots = new HashSet<GameObject>();

        private ReflectionProbe _dynamicCarProbe;
        private Vector3 _lastDynamicProbePos = new Vector3(9999f, 0f, 9999f);
        private float _dynamicProbeCooldown;
        private int _lastLightingPhase = -1; // 0=Night, 1=Dawn, 2=Day, 3=Golden
        private int _staggeredProbeIdx = -1;
        private float _staggeredProbeTimer;
        private float _lastAppliedWetness = -1f;
        private float _vehicleRescanTimer = 3.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;

            GameObject go = new GameObject("OurWorld_UrpRealismLighting");
            go.AddComponent<UrpRealismLightingOverhaul>();
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

        private IEnumerator Start()
        {
            // Wait 2 frames so SultanbeyliDistrictsBuilder, OwnableVehicleGallery, DayNightCycle & GraphicsProfileManager initialize
            yield return null;
            yield return null;

            bool isLow = GraphicsProfileManager.Instance != null &&
                         GraphicsProfileManager.Instance.CurrentTier == OurWorld.Core.GraphicsTier.Low;

            ApplyUrpLightingAndSsao(isLow);
            EnsureAcesFilmicAndBloomQuality(isLow);
            BuildDistrictReflectionProbes(isLow);
            UpgradeAllVehiclesWithClearcoat();
            UpgradeScenePlaneTreesToBotanicalAlphaLeaves();
            CollectDistrictWettableMaterials();

            if (GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged += HandleTierChanged;
            }

            // Trigger initial staggered probe capture so frame 1 stays silky smooth
            _lastLightingPhase = ComputeLightingPhase();
            _staggeredProbeIdx = 0;
            _staggeredProbeTimer = 0.25f;
        }

        private void OnDestroy()
        {
            if (GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged -= HandleTierChanged;
            }
            if (Instance == this) Instance = null;
        }

        private void HandleTierChanged(OurWorld.Core.GraphicsTier tier)
        {
            bool isLow = tier == OurWorld.Core.GraphicsTier.Low;
            ApplyUrpLightingAndSsao(isLow);
            EnsureAcesFilmicAndBloomQuality(isLow);

            for (int i = 0; i < _landmarkProbes.Count; i++)
            {
                if (_landmarkProbes[i] != null) _landmarkProbes[i].enabled = !isLow;
            }
            if (_dynamicCarProbe != null) _dynamicCarProbe.enabled = !isLow;
        }

        // =====================================================================
        // 1. URP SSAO CONTACT SHADING + SOFT DIRECTIONAL SHADOWS
        // =====================================================================
        private static void ApplyUrpLightingAndSsao(bool isLow)
        {
            // Soft directional sunlight/moonlight shadows with clean bias (eliminates jagged stairstep shadows!)
            Light sun = RenderSettings.sun;
            if (sun != null)
            {
                sun.shadows = isLow ? LightShadows.Hard : LightShadows.Soft;
                sun.shadowStrength = isLow ? 0.68f : 0.88f;
                sun.shadowBias = 0.038f;
                sun.shadowNormalBias = 0.34f;
            }

            // Rich skybox/probe environment reflection bounce
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = isLow ? 0.92f : 1.24f;
            RenderSettings.reflectionBounces = isLow ? 1 : 2;

            // Safely enhance URP ScreenSpaceAmbientOcclusion (SSAO) feature on active renderer at runtime!
            // Grounds car wheels, building bases, benches, curbs & NPC shoes with soft contact occlusion.
            try
            {
                UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urp == null) return;

                // Enable silky soft PCF shadows on Medium tier so shadows never look pixelated!
                FieldInfo softField = typeof(UniversalRenderPipelineAsset).GetField("m_SoftShadowsSupported", BindingFlags.Instance | BindingFlags.NonPublic);
                if (softField != null && softField.FieldType == typeof(bool))
                {
                    softField.SetValue(urp, !isLow);
                }

                FieldInfo listField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
                if (listField == null) return;
                ScriptableRendererData[] renderers = listField.GetValue(urp) as ScriptableRendererData[];
                if (renderers == null) return;

                for (int r = 0; r < renderers.Length; r++)
                {
                    ScriptableRendererData data = renderers[r];
                    if (data == null || data.rendererFeatures == null) continue;
                    for (int f = 0; f < data.rendererFeatures.Count; f++)
                    {
                        ScriptableRendererFeature feat = data.rendererFeatures[f];
                        if (feat == null) continue;
                        if (feat.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;

                        feat.SetActive(!isLow);
                        if (!isLow)
                        {
                            FieldInfo settingsField = feat.GetType().GetField("m_Settings", BindingFlags.Instance | BindingFlags.NonPublic);
                            if (settingsField != null)
                            {
                                object s = settingsField.GetValue(feat);
                                if (s != null)
                                {
                                    Type st = s.GetType();
                                    SetMemberFloat(st, s, "Intensity", 0.88f);
                                    SetMemberFloat(st, s, "Radius", 0.92f);
                                    SetMemberFloat(st, s, "DirectLightingStrength", 0.34f);
                                    SetMemberFloat(st, s, "Falloff", 120f);
                                    settingsField.SetValue(feat, s);
                                }
                            }
                        }
                        data.SetDirty();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UrpRealismLightingOverhaul] Runtime SSAO tuning skipped cleanly: {ex.Message}");
            }
        }

        private static void SetMemberFloat(Type type, object target, string name, float value)
        {
            FieldInfo fi = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null && fi.FieldType == typeof(float))
            {
                fi.SetValue(target, value);
                return;
            }
            PropertyInfo pi = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pi != null && pi.CanWrite && pi.PropertyType == typeof(float))
            {
                pi.SetValue(target, value);
            }
        }

        // =====================================================================
        // 2. ACES FILMIC TONEMAPPING & HIGH-QUALITY CONTROLLED BLOOM
        // =====================================================================
        private static void EnsureAcesFilmicAndBloomQuality(bool isLow)
        {
            Volume[] vols = FindObjectsByType<Volume>(FindObjectsInactive.Exclude);
            for (int i = 0; i < vols.Length; i++)
            {
                Volume v = vols[i];
                if (v == null || !v.isGlobal) continue;
                VolumeProfile prof = v.profile;
                if (prof == null) continue;

                if (!prof.TryGet(out Tonemapping tone))
                {
                    tone = prof.Add<Tonemapping>(true);
                }
                if (tone != null)
                {
                    tone.active = !isLow;
                    tone.mode.Override(TonemappingMode.ACES);
                }

                if (prof.TryGet(out Bloom bloom) && bloom != null)
                {
                    bloom.highQualityFiltering.Override(!isLow);
                    bloom.clamp.Override(6.5f);
                }
            }
        }

        // =====================================================================
        // 3. 8 BOX-PROJECTED DISTRICT REFLECTION PROBES + DYNAMIC CAR PROBE
        // =====================================================================
        private void BuildDistrictReflectionProbes(bool isLow)
        {
            Transform probeRoot = new GameObject("Sultanbeyli_ReflectionProbes").transform;
            probeRoot.SetParent(transform, false);

            int probeMask = ~(1 << LayerMask.NameToLayer("UI"));

            for (int i = 0; i < LandmarkProbes.Length; i++)
            {
                DistrictProbeDef def = LandmarkProbes[i];
                GameObject go = new GameObject(def.Name);
                go.transform.SetParent(probeRoot, false);
                go.transform.position = def.Pos;

                ReflectionProbe rp = go.AddComponent<ReflectionProbe>();
                rp.mode = ReflectionProbeMode.Realtime;
                rp.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                rp.resolution = 128;
                rp.hdr = true;
                rp.boxProjection = true;
                rp.size = def.Size;
                rp.blendDistance = 14f;
                rp.intensity = 1.18f;
                rp.importance = 1;
                rp.nearClipPlane = 0.6f;
                rp.farClipPlane = 260f;
                rp.cullingMask = probeMask;
                rp.enabled = !isLow;

                _landmarkProbes.Add(rp);
            }

            // Dedicated local probe that follows the player's car for live Clearcoat hood/door reflections!
            GameObject carProbeGo = new GameObject("PlayerCar_DynamicReflectionProbe");
            carProbeGo.transform.SetParent(probeRoot, false);
            carProbeGo.transform.position = new Vector3(0f, 3.2f, 0f);
            _dynamicCarProbe = carProbeGo.AddComponent<ReflectionProbe>();
            _dynamicCarProbe.mode = ReflectionProbeMode.Realtime;
            _dynamicCarProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            _dynamicCarProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            _dynamicCarProbe.resolution = 128;
            _dynamicCarProbe.hdr = true;
            _dynamicCarProbe.boxProjection = false;
            _dynamicCarProbe.size = new Vector3(28f, 16f, 28f);
            _dynamicCarProbe.blendDistance = 5f;
            _dynamicCarProbe.intensity = 1.28f;
            _dynamicCarProbe.importance = 2;
            _dynamicCarProbe.nearClipPlane = 2.6f; // Excludes the car's own hood/roof from self-occluding reflection
            _dynamicCarProbe.farClipPlane = 210f;
            _dynamicCarProbe.cullingMask = probeMask;
            _dynamicCarProbe.enabled = !isLow;
        }

        // =====================================================================
        // 4. DUAL-LOBE URP CLEARCOAT AUTOMOTIVE PAINT & REFLECTIVE GLASS
        // =====================================================================
        public static void ApplyClearcoatToVehicleHierarchy(GameObject vehicleRoot)
        {
            if (vehicleRoot == null) return;
            Renderer[] rends = vehicleRoot.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rends.Length; r++)
            {
                Renderer rend = rends[r];
                if (rend == null) continue;
                rend.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;

                Material[] mats = rend.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;
                bool arrayModified = false;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null || mat.shader == null || !mat.shader.name.Contains("Lit")) continue;
                    string mn = mat.name;
                    string goName = rend.gameObject.name;

                    bool isTireOrDarkRubber =
                        mn.IndexOf("Tire", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("Tyre", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("Rubber", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        goName.IndexOf("Tire", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (isTireOrDarkRubber) continue;

                    bool isGlass =
                        mn.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("Window", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("Windshield", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        goName.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        goName.IndexOf("Windshield", StringComparison.OrdinalIgnoreCase) >= 0;

                    bool isChromeOrRim =
                        mn.IndexOf("Chrome", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("Rim", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("Alloy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0;

                    bool isPaint =
                        mn.IndexOf("Paint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("Sedan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("Car_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mn.IndexOf("Police", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        goName.IndexOf("Chassis", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        goName.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isGlass)
                    {
                        // Replace opaque shared MAT_Sedan_Glass ONLY on vehicles with true URP Transparent reflective automotive glass
                        // so 3D seats, dashboard, rotating steering wheel & driver are visible through the windshield!
                        Material autoGlass = OurWorldModelLibrary.GetTransparentAutomotiveGlassMaterial();
                        if (autoGlass != null && mats[m] != autoGlass)
                        {
                            mats[m] = autoGlass;
                            arrayModified = true;
                        }
                    }
                    else if (isChromeOrRim)
                    {
                        if (mat.HasProperty(MetallicId)) mat.SetFloat(MetallicId, 0.90f);
                        if (mat.HasProperty(SmoothnessId)) mat.SetFloat(SmoothnessId, 0.88f);
                        mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    }
                    else if (isPaint)
                    {
                        EnableClearcoatOnMaterial(mat);
                    }
                }

                if (arrayModified)
                {
                    rend.sharedMaterials = mats;
                }
            }
        }

        public static void EnableClearcoatOnMaterial(Material mat)
        {
            if (mat == null) return;
            if (mat.HasProperty(MetallicId))
            {
                float curMet = mat.GetFloat(MetallicId);
                if (curMet < 0.28f) mat.SetFloat(MetallicId, 0.34f);
            }
            if (mat.HasProperty(SmoothnessId))
            {
                float curSm = mat.GetFloat(SmoothnessId);
                if (curSm < 0.80f) mat.SetFloat(SmoothnessId, 0.84f);
            }
            if (mat.HasProperty(ClearCoatId)) mat.SetFloat(ClearCoatId, 1.0f);
            if (mat.HasProperty(ClearCoatMaskId)) mat.SetFloat(ClearCoatMaskId, 0.94f);
            if (mat.HasProperty(ClearCoatSmoothnessId)) mat.SetFloat(ClearCoatSmoothnessId, 0.95f);
            mat.EnableKeyword("_CLEARCOAT");
            mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }

        private void UpgradeAllVehiclesWithClearcoat()
        {
            SedanVehicleController[] sedans = FindObjectsByType<SedanVehicleController>(FindObjectsInactive.Include);
            for (int i = 0; i < sedans.Length; i++)
            {
                if (sedans[i] == null) continue;
                if (_upgradedVehicleRoots.Add(sedans[i].gameObject))
                {
                    ApplyClearcoatToVehicleHierarchy(sedans[i].gameObject);
                }
            }

            // Also apply Clearcoat & reflective glass to Ambient Traffic cars, Dealership showroom cars & Garage cars!
            AmbientTrafficManager trafficMgr = FindAnyObjectByType<AmbientTrafficManager>();
            if (trafficMgr != null && _upgradedVehicleRoots.Add(trafficMgr.gameObject))
            {
                ApplyClearcoatToVehicleHierarchy(trafficMgr.gameObject);
            }

            AutoDealershipManager dealer = FindAnyObjectByType<AutoDealershipManager>();
            if (dealer != null && _upgradedVehicleRoots.Add(dealer.gameObject))
            {
                ApplyClearcoatToVehicleHierarchy(dealer.gameObject);
            }

            AutoGarageWorkshopManager garage = FindAnyObjectByType<AutoGarageWorkshopManager>();
            if (garage != null && _upgradedVehicleRoots.Add(garage.gameObject))
            {
                ApplyClearcoatToVehicleHierarchy(garage.gameObject);
            }
        }

        // =====================================================================
        // 4B. SCENE-WIDE BOTANICAL ALPHA-CUTOUT FOLIAGE UPGRADE (Tree_PlaneTree)
        // =====================================================================
        private static void UpgradeScenePlaneTreesToBotanicalAlphaLeaves()
        {
            Material leafA = OurWorldModelLibrary.GetFoliagePlaneMaterial(altTint: false);
            Material leafB = OurWorldModelLibrary.GetFoliagePlaneMaterial(altTint: true);
            Material bark = OurWorldModelLibrary.GetBarkTreeMaterial();
            if (leafA == null || leafB == null) return;

            MeshRenderer[] rends = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < rends.Length; i++)
            {
                MeshRenderer mr = rends[i];
                if (mr == null) continue;
                string n = mr.gameObject.name;
                if (n.IndexOf("Tree_PlaneTree", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("PlaneTree", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                if (n.StartsWith("OW_", StringComparison.Ordinal)) continue;

                Material[] mats = mr.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material src = mats[m];
                    if (src == null) continue;
                    string mn = src.name;
                    if (mn.IndexOf("FoliageB", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mats[m] = leafB;
                        changed = true;
                    }
                    else if (mn.IndexOf("Foliage", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mats[m] = leafA;
                        changed = true;
                    }
                    else if (bark != null && mn.IndexOf("Bark", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mats[m] = bark;
                        changed = true;
                    }
                }
                if (changed)
                {
                    mr.sharedMaterials = mats;
                }
            }
        }

        // =====================================================================
        // 5. 7-DISTRICT RAIN-WET REFLECTIVE ASPHALT & PLAZA GLOSS
        // =====================================================================
        private void CollectDistrictWettableMaterials()
        {
            _districtWetMats.Clear();
            SultanbeyliDistrictsBuilder dist = SultanbeyliDistrictsBuilder.Instance;
            if (dist == null) return;

            HashSet<Material> seen = new HashSet<Material>();
            MeshRenderer[] rends = dist.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                MeshRenderer mr = rends[i];
                if (mr == null) continue;
                Material mat = mr.sharedMaterial;
                if (mat == null || !seen.Add(mat)) continue;
                string n = mat.name;
                bool isRoad = n.Contains("MAT_Dist_Asphalt");
                bool isStone = n.Contains("MAT_Dist_Sidewalk_Parke") || n.Contains("MAT_Dist_Plaza_Travertine") || n.Contains("MAT_Dist_Fortress_Stone");
                if (!isRoad && !isStone) continue;

                Color dryCol = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
                float drySm = mat.HasProperty(SmoothnessId) ? mat.GetFloat(SmoothnessId) : 0.25f;
                _districtWetMats.Add(new WetSurfaceMat
                {
                    Mat = mat,
                    DryColor = dryCol,
                    DrySmoothness = drySm,
                    WetSmoothness = isRoad ? 0.88f : 0.72f,
                    WetDarken = isRoad ? 0.58f : 0.74f
                });
            }
        }

        // =====================================================================
        // UPDATE LOOP — LIGHTWEIGHT STAGGERED REFLECTION & RAIN GLOSS SYNC
        // =====================================================================
        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            bool isLow = GraphicsProfileManager.Instance != null &&
                         GraphicsProfileManager.Instance.CurrentTier == OurWorld.Core.GraphicsTier.Low;

            // 1. Periodically catch newly bought Oto Galeri vehicles for Clearcoat upgrade
            _vehicleRescanTimer -= dt;
            if (_vehicleRescanTimer <= 0f)
            {
                _vehicleRescanTimer = 6.0f;
                UpgradeAllVehiclesWithClearcoat();
            }

            // 2. Sync rain wetness sheen across all 6 new Sultanbeyli districts
            float wet = NeighborhoodWeatherSystem.Instance != null ? NeighborhoodWeatherSystem.Instance.Wetness01 : 0f;
            if (Mathf.Abs(wet - _lastAppliedWetness) >= 0.025f && _districtWetMats.Count > 0)
            {
                _lastAppliedWetness = wet;
                for (int i = 0; i < _districtWetMats.Count; i++)
                {
                    WetSurfaceMat entry = _districtWetMats[i];
                    if (entry.Mat == null) continue;
                    Color targetCol = Color.Lerp(entry.DryColor, entry.DryColor * entry.WetDarken, wet);
                    targetCol.a = entry.DryColor.a;
                    if (entry.Mat.HasProperty(BaseColorId)) entry.Mat.SetColor(BaseColorId, targetCol);
                    if (entry.Mat.HasProperty(ColorId)) entry.Mat.SetColor(ColorId, targetCol);
                    if (entry.Mat.HasProperty(SmoothnessId))
                    {
                        entry.Mat.SetFloat(SmoothnessId, Mathf.Lerp(entry.DrySmoothness, entry.WetSmoothness, wet));
                    }
                }
            }

            if (isLow) return;

            // 3. Re-render landmark reflection probes staggered across frames only when time-of-day phase shifts!
            int phase = ComputeLightingPhase();
            if (phase != _lastLightingPhase)
            {
                _lastLightingPhase = phase;
                _staggeredProbeIdx = 0;
                _staggeredProbeTimer = 0.25f;
            }

            if (_staggeredProbeIdx >= 0 && _staggeredProbeIdx < _landmarkProbes.Count)
            {
                _staggeredProbeTimer -= dt;
                if (_staggeredProbeTimer <= 0f)
                {
                    ReflectionProbe rp = _landmarkProbes[_staggeredProbeIdx];
                    if (rp != null && rp.enabled)
                    {
                        rp.intensity = phase == 3 ? 1.32f : (phase == 0 ? 0.96f : 1.18f) + wet * 0.18f;
                        rp.RenderProbe();
                    }
                    _staggeredProbeIdx++;
                    _staggeredProbeTimer = 0.65f;
                    if (_staggeredProbeIdx >= _landmarkProbes.Count) _staggeredProbeIdx = -1;
                }
            }

            // 4. Update dynamic Player-Car Clearcoat Reflection Probe when moving across blocks (zero per-frame cost!)
            if (_dynamicCarProbe != null && _dynamicCarProbe.enabled)
            {
                _dynamicProbeCooldown -= dt;
                PlayerModeController mode = PlayerModeController.Instance;
                if (_dynamicProbeCooldown <= 0f && mode != null && !mode.IsModalLocked)
                {
                    Vector3 targetPos = mode.ActiveWorldPosition + Vector3.up * 2.4f;
                    float movedSq = (targetPos - _lastDynamicProbePos).sqrMagnitude;
                    if (movedSq >= 28f * 28f || _lastDynamicProbePos.x > 5000f)
                    {
                        _lastDynamicProbePos = targetPos;
                        _dynamicCarProbe.transform.position = targetPos;
                        _dynamicCarProbe.intensity = (phase == 3 ? 1.38f : (phase == 0 ? 1.05f : 1.26f)) + wet * 0.22f;
                        _dynamicCarProbe.RenderProbe();
                        _dynamicProbeCooldown = mode.IsDriving ? 3.5f : 7.5f;
                    }
                }
            }
        }

        private static int ComputeLightingPhase()
        {
            if (DayNightCycle.Instance == null) return 2;
            float h = DayNightCycle.Instance.CurrentHour;
            if (h >= 19.5f || h < 5.5f) return 0;      // Night
            if (h >= 5.5f && h < 7.8f) return 1;       // Dawn
            if (h >= 16.5f && h < 19.5f) return 3;     // Golden Hour / Sunset
            return 2;                                   // Bright Mediterranean Day
        }
    }
}
