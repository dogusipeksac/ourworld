using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using GraphicsTier = OurWorld.Core.GraphicsTier;

namespace OurWorld.World
{
    /// <summary>
    /// Runtime procedural street surface detailer (zero-GC Update, mobile URP-safe):
    /// Discovers road/asphalt MeshRenderers once after scene load, deterministically generates
    /// asphalt cracks, repair patches, cast-iron manholes, storm drain grates, oil/puddle stains,
    /// tire skid marks, and curb-side dirt strips, aligns each quad to the road surface via
    /// frame-sliced raycasts (<= 80/frame), and builds <= 30 combined 64x64m cell meshes sharing
    /// a single URP Lit Transparent material with 4 Hz distance culling and rain-wetness sheen sync.
    /// </summary>
    public sealed class StreetSurfaceDetailer : MonoBehaviour
    {
        public static StreetSurfaceDetailer Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_StreetSurfaceDetailer");
            go.AddComponent<StreetSurfaceDetailer>();
        }

        private const string AlbedoResourcePath = "OurWorldStreetDecals/SD_Atlas_Albedo";
        private const string NormalResourcePath = "OurWorldStreetDecals/SD_Atlas_Normal";

        private const float CellSize = 64f;
        private const int MaxCells = 30;
        private const int MaxDecalsMedium = 1400;
        private const int MaxDecalsLow = 600;
        private const float LowProbMultiplier = 0.45f;
        private const int MaxRaycastsPerFrame = 80;
        private const double FrameBudgetMs = 1.8;

        private const float SurfaceLiftMeters = 0.015f;
        private const float MinRoadAreaSqM = 40f;
        private const float MaxRoadHeightM = 1.0f;
        private const float MinRoadWidthM = 2.0f;

        private const float SliceStepMeters = 7.0f;
        private const float ManholeMinStepMeters = 28.0f;
        private const float ManholeMaxStepMeters = 40.0f;
        private const float CurbStepMeters = 24.0f;
        private const float CurbEdgeOffsetMeters = 0.35f;

        private const float CullIntervalSec = 0.25f; // 4 Hz
        private const float CullDistanceMedium = 110f;
        private const float CullDistanceLow = 70f;

        private const float DrySmoothness = 0.25f;
        private const float WetSmoothness = 0.60f;
        private const int DecalRenderQueue = 2550;

        // 4x4 atlas UV padding (2 px inset inside the 16 px transparent border)
        private const float AtlasTileSpan = 0.25f;
        private const float AtlasUvPad = 2f / 2048f;

        // 0-based tile indices matching scripts/blender/generate_street_decal_atlas.py
        private const int TileCrackFirst = 0;   // 0, 1, 2
        private const int TilePatchFirst = 3;   // 3, 4, 5
        private const int TileManhole = 6;
        private const int TileStormGrate = 7;
        private const int TileOilFirst = 8;     // 8, 9
        private const int TileTireSkid = 10;
        private const int TileCurbDirt = 11;
        private const int TilePuddle = 13;

        private static readonly string[] IncludeKeywords =
        {
            "road", "asphalt", "boulevard", "street", "avenue", "cadde"
        };

        private static readonly string[] ExcludeKeywords =
        {
            "marking", "lane", "zebra", "line", "sidewalk", "curb"
        };

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int SrcBlendAlphaId = Shader.PropertyToID("_SrcBlendAlpha");
        private static readonly int DstBlendAlphaId = Shader.PropertyToID("_DstBlendAlpha");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int SpecularHighlightsId = Shader.PropertyToID("_SpecularHighlights");
        private static readonly int EnvironmentReflectionsId = Shader.PropertyToID("_EnvironmentReflections");

        private struct RoadCandidate
        {
            public MeshRenderer Renderer;
            public Collider Collider;
            public Bounds WorldBounds;
            public Vector3 ObbCenter;
            public Vector3 LongAxis;
            public Vector3 CrossAxis;
            public float Length;
            public float Width;
            public int Seed;
        }

        private struct PlacedDecal
        {
            public int TileIndex;
            public Vector3 Position;
            public Vector3 Normal;
            public Vector3 Forward;
            public float SizeU;
            public float SizeV;
        }

        private sealed class CellBucket
        {
            public int Cx;
            public int Cz;
            public readonly List<PlacedDecal> Decals = new List<PlacedDecal>(48);
        }

        private struct CellEntry
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public Vector3 Center;
        }

        /// <summary>Zero-allocation deterministic xorshift32 PRNG.</summary>
        private struct FastRng
        {
            private uint _state;

            public FastRng(int seed)
            {
                _state = (uint)seed;
                if (_state == 0u) _state = 0x9E3779B9u;
            }

            public uint NextUInt()
            {
                uint x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }

            public float NextFloat()
            {
                return (NextUInt() & 0x00FFFFFFu) * (1f / 16777216f);
            }

            public float Range(float min, float max)
            {
                return min + (max - min) * NextFloat();
            }

            public int RangeInt(int minInclusive, int maxExclusive)
            {
                int span = maxExclusive - minInclusive;
                if (span <= 1) return minInclusive;
                return minInclusive + (int)(NextUInt() % (uint)span);
            }
        }

        private readonly List<CellEntry> _cells = new List<CellEntry>(MaxCells);

        // Reusable scratch buffers for zero-CombineMeshes direct cell mesh construction
        private readonly List<Vector3> _meshVerts = new List<Vector3>(512);
        private readonly List<Vector3> _meshNormals = new List<Vector3>(512);
        private readonly List<Vector4> _meshTangents = new List<Vector4>(512);
        private readonly List<Vector2> _meshUvs = new List<Vector2>(512);
        private readonly List<int> _meshTris = new List<int>(768);

        private Material _sharedMaterial;
        private Camera _cachedCam;
        private float _cullTimer;
        private float _cullDistanceSq = CullDistanceMedium * CullDistanceMedium;
        private float _lastAppliedWetness = -1f;
        private bool _subscribedTier;
        private bool _setupComplete;

        private static bool IsLowTier =>
            GraphicsProfileManager.Instance != null &&
            GraphicsProfileManager.Instance.CurrentTier == GraphicsTier.Low;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            float dist = IsLowTier ? CullDistanceLow : CullDistanceMedium;
            _cullDistanceSq = dist * dist;
            TrySubscribeTierEvent();
        }

        private IEnumerator Start()
        {
            // Wait 2 frames so runtime road/district builders finish spawning
            yield return null;
            yield return null;

            TrySubscribeTierEvent();
            _cachedCam = Camera.main;

            yield return StartCoroutine(BuildStreetDecalsRoutine());
        }

        private void OnDestroy()
        {
            if (_subscribedTier && GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged -= HandleTierChanged;
            }
            _subscribedTier = false;

            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i].Mesh != null)
                {
                    Destroy(_cells[i].Mesh);
                }
            }
            _cells.Clear();

            if (_sharedMaterial != null)
            {
                Destroy(_sharedMaterial);
                _sharedMaterial = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!_setupComplete) return;

            // 1. Rain wetness sheen sync (only updates material when wetness changes)
            if (_sharedMaterial != null)
            {
                float wet = NeighborhoodWeatherSystem.Instance != null
                    ? NeighborhoodWeatherSystem.Instance.Wetness01
                    : 0f;
                if (Mathf.Abs(wet - _lastAppliedWetness) >= 0.01f)
                {
                    _lastAppliedWetness = wet;
                    _sharedMaterial.SetFloat(SmoothnessId, Mathf.Lerp(DrySmoothness, WetSmoothness, Mathf.Clamp01(wet)));
                }
            }

            if (_cells.Count == 0) return;

            // 2. 4 Hz camera distance culling (zero GC)
            _cullTimer += Time.deltaTime;
            if (_cullTimer < CullIntervalSec) return;
            _cullTimer = 0f;

            if (_cachedCam == null || !_cachedCam.isActiveAndEnabled)
            {
                _cachedCam = Camera.main;
                if (_cachedCam == null) return;
            }

            Vector3 camPos = _cachedCam.transform.position;
            float maxDistSq = _cullDistanceSq;

            for (int i = 0; i < _cells.Count; i++)
            {
                MeshRenderer mr = _cells[i].Renderer;
                if (mr == null) continue;
                Vector3 d = _cells[i].Center - camPos;
                bool visible = (d.x * d.x + d.y * d.y + d.z * d.z) <= maxDistSq;
                if (mr.enabled != visible)
                {
                    mr.enabled = visible;
                }
            }
        }

        private void TrySubscribeTierEvent()
        {
            if (_subscribedTier || GraphicsProfileManager.Instance == null) return;
            GraphicsProfileManager.Instance.OnTierChanged += HandleTierChanged;
            _subscribedTier = true;
        }

        private void HandleTierChanged(GraphicsTier tier)
        {
            float dist = tier == GraphicsTier.Low ? CullDistanceLow : CullDistanceMedium;
            _cullDistanceSq = dist * dist;
        }

        // =====================================================================
        // FRAME-SLICED COROUTINE SETUP (<= 80 RAYCASTS / FRAME, <= ~2 MS/FRAME)
        // =====================================================================
        private IEnumerator BuildStreetDecalsRoutine()
        {
            Stopwatch totalSw = Stopwatch.StartNew();
            Stopwatch frameSw = Stopwatch.StartNew();

            if (!CreateSharedDecalMaterial())
            {
                totalSw.Stop();
                _setupComplete = true;
                yield break;
            }

            bool isLow = IsLowTier;
            int maxDecals = isLow ? MaxDecalsLow : MaxDecalsMedium;
            float tierProbScale = isLow ? LowProbMultiplier : 1.0f;

            // 1. Discover eligible road MeshRenderers once
            List<RoadCandidate> roads = DiscoverRoads();
            if (roads.Count == 0)
            {
                totalSw.Stop();
                _setupComplete = true;
                Debug.Log($"StreetSurfaceDetailer: 0 yol, 0 decal, 0 hücre, {totalSw.Elapsed.TotalMilliseconds:F1} ms");
                yield break;
            }

            // Compute fair global density scale so all districts share the decal budget evenly
            float estimatedTotal = EstimateTotalDecals(roads, tierProbScale);
            float budgetScale = estimatedTotal > maxDecals
                ? Mathf.Clamp(maxDecals / estimatedTotal, 0.20f, 1.0f)
                : 1.0f;
            float effectiveProbScale = tierProbScale * budgetScale;

            if (frameSw.Elapsed.TotalMilliseconds >= FrameBudgetMs)
            {
                yield return null;
                frameSw.Restart();
            }

            // 2. Generate & raycast decals across all roads
            Dictionary<long, CellBucket> buckets = new Dictionary<long, CellBucket>(MaxCells * 2);
            int totalDecals = 0;
            int raycastsThisFrame = 0;

            float pCrackEnd = 0.30f * effectiveProbScale;
            float pPatchEnd = pCrackEnd + 0.12f * effectiveProbScale;
            float pOilEnd = pPatchEnd + 0.08f * effectiveProbScale;
            float pPuddleEnd = pOilEnd + 0.04f * effectiveProbScale;
            float pSkidEnd = pPuddleEnd + 0.03f * effectiveProbScale;

            for (int rIdx = 0; rIdx < roads.Count && totalDecals < maxDecals; rIdx++)
            {
                RoadCandidate road = roads[rIdx];
                FastRng rng = new FastRng(road.Seed);
                float halfLen = road.Length * 0.5f;
                float halfWidth = road.Width * 0.5f;

                // 2a. Surface slices every 7m along long axis
                for (float s = -halfLen + 3.5f; s <= halfLen - 3.5f && totalDecals < maxDecals; s += SliceStepMeters)
                {
                    float roll = rng.NextFloat();
                    if (roll >= pSkidEnd) continue;

                    int tileIdx;
                    float sizeU;
                    float sizeV;
                    bool randomYaw = true;

                    if (roll < pCrackEnd)
                    {
                        tileIdx = TileCrackFirst + rng.RangeInt(0, 3);
                        sizeU = rng.Range(1.2f, 2.6f);
                        sizeV = Mathf.Clamp(sizeU * rng.Range(0.75f, 1.15f), 1.2f, 2.6f);
                    }
                    else if (roll < pPatchEnd)
                    {
                        tileIdx = TilePatchFirst + rng.RangeInt(0, 3);
                        sizeU = rng.Range(1.5f, 3.0f);
                        sizeV = rng.Range(1.0f, 2.0f);
                    }
                    else if (roll < pOilEnd)
                    {
                        tileIdx = TileOilFirst + rng.RangeInt(0, 2);
                        sizeU = rng.Range(0.8f, 1.6f);
                        sizeV = Mathf.Clamp(sizeU * rng.Range(0.8f, 1.2f), 0.8f, 1.6f);
                    }
                    else if (roll < pPuddleEnd)
                    {
                        tileIdx = TilePuddle;
                        sizeU = rng.Range(0.8f, 1.6f);
                        sizeV = Mathf.Clamp(sizeU * rng.Range(0.8f, 1.2f), 0.8f, 1.6f);
                    }
                    else
                    {
                        tileIdx = TileTireSkid;
                        sizeU = rng.Range(0.95f, 1.6f);
                        sizeV = rng.Range(0.42f, 0.65f);
                    }

                    float jitterS = Mathf.Clamp(s + rng.Range(-1.8f, 1.8f), -halfLen + 1.0f, halfLen - 1.0f);
                    float maxLat = Mathf.Max(0.15f, halfWidth - Mathf.Min(Mathf.Max(sizeU, sizeV) * 0.45f, halfWidth * 0.65f));
                    float lat = rng.Range(-maxLat, maxLat);

                    Vector3 samplePos = road.ObbCenter + road.LongAxis * jitterS + road.CrossAxis * lat;
                    Vector3 forwardDir;
                    if (randomYaw)
                    {
                        float yawRad = rng.Range(0f, Mathf.PI * 2f);
                        forwardDir = new Vector3(Mathf.Cos(yawRad), 0f, Mathf.Sin(yawRad));
                    }
                    else
                    {
                        forwardDir = road.LongAxis;
                    }

                    if (TryResolveSurface(road, samplePos, ref raycastsThisFrame, out Vector3 hitPos, out Vector3 hitNormal))
                    {
                        AddDecalToBucket(buckets, tileIdx, hitPos, hitNormal, forwardDir, sizeU, sizeV);
                        totalDecals++;
                    }

                    if (raycastsThisFrame >= MaxRaycastsPerFrame || frameSw.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                    {
                        raycastsThisFrame = 0;
                        yield return null;
                        frameSw.Restart();
                    }
                }

                // 2b. Cast-iron manholes every 28-40m, offset 1/4 width from centerline (aligned to road)
                float manholeS = -halfLen + rng.Range(8f, 22f);
                int manholeSide = rng.NextFloat() < 0.5f ? -1 : 1;
                while (manholeS <= halfLen - 4f && totalDecals < maxDecals)
                {
                    if (rng.NextFloat() <= effectiveProbScale)
                    {
                        float lat = manholeSide * (road.Width * 0.25f);
                        Vector3 samplePos = road.ObbCenter + road.LongAxis * manholeS + road.CrossAxis * lat;
                        if (TryResolveSurface(road, samplePos, ref raycastsThisFrame, out Vector3 hitPos, out Vector3 hitNormal))
                        {
                            AddDecalToBucket(buckets, TileManhole, hitPos, hitNormal, road.LongAxis, 0.75f, 0.75f);
                            totalDecals++;
                        }

                        if (raycastsThisFrame >= MaxRaycastsPerFrame || frameSw.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                        {
                            raycastsThisFrame = 0;
                            yield return null;
                            frameSw.Restart();
                        }
                    }

                    manholeSide = -manholeSide;
                    manholeS += rng.Range(ManholeMinStepMeters, ManholeMaxStepMeters);
                }

                // 2c. Storm drain grates & curb-side dirt strips along both edges every 24m
                float curbLatMag = Mathf.Max(0f, halfWidth - CurbEdgeOffsetMeters);
                for (int sideIdx = 0; sideIdx < 2 && totalDecals < maxDecals; sideIdx++)
                {
                    float side = sideIdx == 0 ? -1f : 1f;
                    Vector3 inwardDir = -side * road.CrossAxis;

                    for (float s = -halfLen + 6f; s <= halfLen - 6f && totalDecals < maxDecals; s += CurbStepMeters)
                    {
                        // Storm drain grate (0.5m across road x 0.9m along road, aligned to road)
                        if (rng.NextFloat() <= effectiveProbScale)
                        {
                            Vector3 gratePos = road.ObbCenter + road.LongAxis * s + road.CrossAxis * (side * curbLatMag);
                            if (TryResolveSurface(road, gratePos, ref raycastsThisFrame, out Vector3 hitPos, out Vector3 hitNormal))
                            {
                                AddDecalToBucket(buckets, TileStormGrate, hitPos, hitNormal, road.LongAxis, 0.5f, 0.9f);
                                totalDecals++;
                            }

                            if (raycastsThisFrame >= MaxRaycastsPerFrame || frameSw.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                            {
                                raycastsThisFrame = 0;
                                yield return null;
                                frameSw.Restart();
                            }
                        }

                        if (totalDecals >= maxDecals) break;

                        // Curb-side dirt/dust strip (3.0m along road x 0.35m across road)
                        // Oriented with +V (transparent fade) pointing inward, -V (opaque grit) hugging curb
                        float dirtS = s + 6.5f;
                        if (dirtS <= halfLen - 2.0f && rng.NextFloat() <= effectiveProbScale)
                        {
                            float dirtLatMag = Mathf.Max(0f, halfWidth - 0.22f);
                            Vector3 dirtPos = road.ObbCenter + road.LongAxis * dirtS + road.CrossAxis * (side * dirtLatMag);
                            if (TryResolveSurface(road, dirtPos, ref raycastsThisFrame, out Vector3 hitPos, out Vector3 hitNormal))
                            {
                                AddDecalToBucket(buckets, TileCurbDirt, hitPos, hitNormal, inwardDir, 3.0f, 0.35f);
                                totalDecals++;
                            }

                            if (raycastsThisFrame >= MaxRaycastsPerFrame || frameSw.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                            {
                                raycastsThisFrame = 0;
                                yield return null;
                                frameSw.Restart();
                            }
                        }
                    }
                }
            }

            // 3. Enforce <= 30 spatial cell meshes by merging smallest cells into nearest neighbor
            List<CellBucket> bucketList = new List<CellBucket>(buckets.Count);
            foreach (KeyValuePair<long, CellBucket> kvp in buckets)
            {
                if (kvp.Value.Decals.Count > 0)
                {
                    bucketList.Add(kvp.Value);
                }
            }

            MergeExcessBuckets(bucketList, MaxCells);

            // 4. Build one direct vertex-buffer Mesh per cell (frame-sliced)
            for (int i = 0; i < bucketList.Count; i++)
            {
                BuildCellMeshDirect(bucketList[i]);
                if (frameSw.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                {
                    yield return null;
                    frameSw.Restart();
                }
            }

            // Free temporary vertex scratch lists' excess capacity
            _meshVerts.Clear();
            _meshNormals.Clear();
            _meshTangents.Clear();
            _meshUvs.Clear();
            _meshTris.Clear();

            totalSw.Stop();
            _setupComplete = true;
            Debug.Log($"StreetSurfaceDetailer: {roads.Count} yol, {totalDecals} decal, {_cells.Count} hücre, {totalSw.Elapsed.TotalMilliseconds:F1} ms");
        }

        // =====================================================================
        // MATERIAL CREATION (SHARED URP LIT TRANSPARENT, RENDER QUEUE 2550)
        // =====================================================================
        private bool CreateSharedDecalMaterial()
        {
            Texture2D albedo = Resources.Load<Texture2D>(AlbedoResourcePath);
            Texture2D normal = Resources.Load<Texture2D>(NormalResourcePath);
            if (albedo == null)
            {
                Debug.LogWarning($"[StreetSurfaceDetailer] Missing albedo atlas at Resources/{AlbedoResourcePath}. Skipping street decals.");
                return false;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogWarning("[StreetSurfaceDetailer] Universal Render Pipeline/Lit shader not found.");
                return false;
            }

            Material mat = new Material(urpLit)
            {
                name = "MAT_OurWorld_StreetDecals_Shared"
            };

            // Configure URP Lit Transparent (Alpha blend, ZWrite Off, ZTest LEqual, RenderQueue 2550)
            mat.SetFloat(SurfaceId, 1f);
            mat.SetFloat(BlendId, 0f);
            mat.SetInt(SrcBlendId, (int)BlendMode.SrcAlpha);
            mat.SetInt(DstBlendId, (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt(SrcBlendAlphaId, (int)BlendMode.One);
            mat.SetInt(DstBlendAlphaId, (int)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat(ZWriteId, 0f);
            mat.SetFloat(AlphaClipId, 0f);
            mat.SetFloat(CullId, (float)CullMode.Back);
            if (mat.HasProperty(ZTestId))
            {
                mat.SetInt(ZTestId, (int)CompareFunction.LessEqual);
            }

            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = DecalRenderQueue;

            mat.SetTexture(BaseMapId, albedo);
            mat.SetColor(BaseColorId, Color.white);
            mat.SetFloat(MetallicId, 0f);
            mat.SetFloat(SmoothnessId, DrySmoothness);

            if (mat.HasProperty(SpecularHighlightsId)) mat.SetFloat(SpecularHighlightsId, 1f);
            if (mat.HasProperty(EnvironmentReflectionsId)) mat.SetFloat(EnvironmentReflectionsId, 1f);

            if (normal != null)
            {
                mat.SetTexture(BumpMapId, normal);
                mat.SetFloat(BumpScaleId, 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }

            _sharedMaterial = mat;
            _lastAppliedWetness = 0f;
            return true;
        }

        // =====================================================================
        // ROAD CANDIDATE DISCOVERY & OBB EXTRACTION
        // =====================================================================
        private List<RoadCandidate> DiscoverRoads()
        {
            MeshRenderer[] all = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            List<RoadCandidate> result = new List<RoadCandidate>(64);

            for (int i = 0; i < all.Length; i++)
            {
                MeshRenderer mr = all[i];
                if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) continue;

                string goName = mr.gameObject.name;
                Material sharedMat = mr.sharedMaterial;
                string matName = sharedMat != null ? sharedMat.name : string.Empty;

                if (ContainsAnyKeyword(goName, ExcludeKeywords) || ContainsAnyKeyword(matName, ExcludeKeywords))
                {
                    continue;
                }

                if (!ContainsAnyKeyword(goName, IncludeKeywords) && !ContainsAnyKeyword(matName, IncludeKeywords))
                {
                    continue;
                }

                Bounds wb = mr.bounds;
                if (wb.size.y >= MaxRoadHeightM) continue;
                if (wb.size.x * wb.size.z < MinRoadAreaSqM) continue;

                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0) continue;

                if (!TryExtractHorizontalObb(mf.sharedMesh.bounds, mr.transform, out Vector3 obbCenter, out Vector3 longAxis, out Vector3 crossAxis, out float length, out float width))
                {
                    continue;
                }

                if (length * width < MinRoadAreaSqM || width < MinRoadWidthM) continue;

                Collider col = mr.GetComponent<Collider>();
                int seed = StableStringHash(goName)
                    ^ (Mathf.RoundToInt(obbCenter.x * 2f) * 73856093)
                    ^ (Mathf.RoundToInt(obbCenter.z * 2f) * 19349663);

                result.Add(new RoadCandidate
                {
                    Renderer = mr,
                    Collider = col,
                    WorldBounds = wb,
                    ObbCenter = obbCenter,
                    LongAxis = longAxis,
                    CrossAxis = crossAxis,
                    Length = length,
                    Width = width,
                    Seed = seed
                });
            }

            return result;
        }

        private static bool TryExtractHorizontalObb(
            Bounds localBounds,
            Transform tr,
            out Vector3 obbCenter,
            out Vector3 longAxis,
            out Vector3 crossAxis,
            out float length,
            out float width)
        {
            obbCenter = tr.TransformPoint(localBounds.center);
            Vector3 ax = tr.TransformVector(new Vector3(localBounds.size.x, 0f, 0f));
            Vector3 ay = tr.TransformVector(new Vector3(0f, localBounds.size.y, 0f));
            Vector3 az = tr.TransformVector(new Vector3(0f, 0f, localBounds.size.z));

            // Pick the two axes with the largest horizontal (XZ) span (handles Blender Z-up/Y-up rotations)
            float hx = new Vector2(ax.x, ax.z).magnitude;
            float hy = new Vector2(ay.x, ay.z).magnitude;
            float hz = new Vector2(az.x, az.z).magnitude;

            Vector3 uVec, vVec;
            float uLen, vLen;

            if (hy <= hx && hy <= hz)
            {
                uVec = ax; uLen = hx;
                vVec = az; vLen = hz;
            }
            else if (hz <= hx && hz <= hy)
            {
                uVec = ax; uLen = hx;
                vVec = ay; vLen = hy;
            }
            else
            {
                uVec = ay; uLen = hy;
                vVec = az; vLen = hz;
            }

            if (uLen < 0.01f || vLen < 0.01f)
            {
                longAxis = Vector3.forward;
                crossAxis = Vector3.right;
                length = 0f;
                width = 0f;
                return false;
            }

            if (uLen >= vLen)
            {
                longAxis = new Vector3(uVec.x, 0f, uVec.z).normalized;
                length = uLen;
                width = vLen;
            }
            else
            {
                longAxis = new Vector3(vVec.x, 0f, vVec.z).normalized;
                length = vLen;
                width = uLen;
            }

            if (longAxis.sqrMagnitude < 0.5f)
            {
                longAxis = Vector3.forward;
            }

            crossAxis = Vector3.Cross(Vector3.up, longAxis).normalized;
            return true;
        }

        private static float EstimateTotalDecals(List<RoadCandidate> roads, float tierProbScale)
        {
            float sum = 0f;
            const float sliceExpectedPer7m = 0.57f; // 30% + 12% + 8% + 4% + 3%
            for (int i = 0; i < roads.Count; i++)
            {
                float len = roads[i].Length;
                float slices = Mathf.Max(0f, (len - 7f) / SliceStepMeters + 1f) * sliceExpectedPer7m;
                float manholes = Mathf.Max(0f, (len - 12f) / 34f);
                float curbPairs = Mathf.Max(0f, (len - 12f) / CurbStepMeters + 1f) * 4f; // 2 sides x (grate + dirt)
                sum += (slices + manholes + curbPairs) * tierProbScale;
            }
            return sum;
        }

        // =====================================================================
        // SURFACE RAYCAST & SPATIAL BUCKETING
        // =====================================================================
        private static bool TryResolveSurface(
            RoadCandidate road,
            Vector3 xzPoint,
            ref int raycastsThisFrame,
            out Vector3 outPos,
            out Vector3 outNormal)
        {
            float topY = road.WorldBounds.max.y;

            if (road.Collider != null && road.Collider.enabled)
            {
                Vector3 rayStart = new Vector3(xzPoint.x, topY + 1.5f, xzPoint.z);
                Ray ray = new Ray(rayStart, Vector3.down);
                float maxDist = road.WorldBounds.size.y + 3.5f;
                raycastsThisFrame++;

                // Collider.Raycast tests exclusively against this road's collider (ignores cars/NPCs, zero GC)
                if (road.Collider.Raycast(ray, out RaycastHit hit, maxDist) && hit.normal.y >= 0.7f)
                {
                    outNormal = hit.normal.normalized;
                    outPos = hit.point + outNormal * SurfaceLiftMeters;
                    return true;
                }
                outPos = default; outNormal = Vector3.up;
                return false; // point lies outside the road surface (L-shaped/merged meshes): skip
            }

            // Fallback when road has no collider
            outNormal = Vector3.up;
            outPos = new Vector3(xzPoint.x, topY + SurfaceLiftMeters, xzPoint.z);
            return true;
        }

        private static void AddDecalToBucket(
            Dictionary<long, CellBucket> buckets,
            int tileIndex,
            Vector3 pos,
            Vector3 normal,
            Vector3 forward,
            float sizeU,
            float sizeV)
        {
            int cx = Mathf.FloorToInt(pos.x / CellSize);
            int cz = Mathf.FloorToInt(pos.z / CellSize);
            long key = ((long)cx << 32) | (uint)cz;

            if (!buckets.TryGetValue(key, out CellBucket bucket))
            {
                bucket = new CellBucket { Cx = cx, Cz = cz };
                buckets[key] = bucket;
            }

            bucket.Decals.Add(new PlacedDecal
            {
                TileIndex = tileIndex,
                Position = pos,
                Normal = normal,
                Forward = forward,
                SizeU = sizeU,
                SizeV = sizeV
            });
        }

        private static void MergeExcessBuckets(List<CellBucket> list, int maxCount)
        {
            while (list.Count > maxCount)
            {
                int smallestIdx = 0;
                int smallestCount = list[0].Decals.Count;
                for (int i = 1; i < list.Count; i++)
                {
                    if (list[i].Decals.Count < smallestCount)
                    {
                        smallestCount = list[i].Decals.Count;
                        smallestIdx = i;
                    }
                }

                CellBucket src = list[smallestIdx];
                int nearestIdx = -1;
                int bestDistSq = int.MaxValue;
                for (int i = 0; i < list.Count; i++)
                {
                    if (i == smallestIdx) continue;
                    int dx = list[i].Cx - src.Cx;
                    int dz = list[i].Cz - src.Cz;
                    int dSq = dx * dx + dz * dz;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        nearestIdx = i;
                    }
                }

                if (nearestIdx < 0) break;
                list[nearestIdx].Decals.AddRange(src.Decals);
                list.RemoveAt(smallestIdx);
            }
        }

        // =====================================================================
        // DIRECT CELL MESH CONSTRUCTION (4 VERTS / DECAL, NO CombineMeshes)
        // =====================================================================
        private void BuildCellMeshDirect(CellBucket bucket)
        {
            int count = bucket.Decals.Count;
            if (count == 0) return;

            _meshVerts.Clear();
            _meshNormals.Clear();
            _meshTangents.Clear();
            _meshUvs.Clear();
            _meshTris.Clear();

            Vector3 cellOrigin = new Vector3(
                bucket.Cx * CellSize + CellSize * 0.5f,
                0f,
                bucket.Cz * CellSize + CellSize * 0.5f);

            for (int i = 0; i < count; i++)
            {
                PlacedDecal d = bucket.Decals[i];
                Vector3 n = d.Normal.sqrMagnitude > 0.001f ? d.Normal.normalized : Vector3.up;

                // Project desired forward onto surface plane
                Vector3 fwd = d.Forward - n * Vector3.Dot(d.Forward, n);
                if (fwd.sqrMagnitude < 0.0001f)
                {
                    fwd = Vector3.Cross(Vector3.right, n);
                }
                fwd.Normalize();

                // Left-handed basis: Cross(n, fwd) = right, Cross(fwd, right) = +n (up)
                Vector3 right = Vector3.Cross(n, fwd).normalized;

                Vector3 localCenter = d.Position - cellOrigin;
                Vector3 halfRight = right * (d.SizeU * 0.5f);
                Vector3 halfFwd = fwd * (d.SizeV * 0.5f);

                int baseVert = _meshVerts.Count;
                _meshVerts.Add(localCenter - halfRight - halfFwd); // bottom-left  (uMin, vMin)
                _meshVerts.Add(localCenter - halfRight + halfFwd); // top-left     (uMin, vMax)
                _meshVerts.Add(localCenter + halfRight + halfFwd); // top-right    (uMax, vMax)
                _meshVerts.Add(localCenter + halfRight - halfFwd); // bottom-right (uMax, vMin)

                _meshNormals.Add(n);
                _meshNormals.Add(n);
                _meshNormals.Add(n);
                _meshNormals.Add(n);

                // Tangent +U = right; bitangent = Cross(n, right) * (-1) = +fwd (+V) for OpenGL +Y normal map
                Vector4 tan = new Vector4(right.x, right.y, right.z, -1f);
                _meshTangents.Add(tan);
                _meshTangents.Add(tan);
                _meshTangents.Add(tan);
                _meshTangents.Add(tan);

                int col = d.TileIndex & 3;
                int rowFromTop = d.TileIndex >> 2;
                float uMin = col * AtlasTileSpan + AtlasUvPad;
                float uMax = (col + 1) * AtlasTileSpan - AtlasUvPad;
                float vMax = 1f - rowFromTop * AtlasTileSpan - AtlasUvPad;
                float vMin = 1f - (rowFromTop + 1) * AtlasTileSpan + AtlasUvPad;

                _meshUvs.Add(new Vector2(uMin, vMin));
                _meshUvs.Add(new Vector2(uMin, vMax));
                _meshUvs.Add(new Vector2(uMax, vMax));
                _meshUvs.Add(new Vector2(uMax, vMin));

                // Clockwise from above (+n)
                _meshTris.Add(baseVert);
                _meshTris.Add(baseVert + 1);
                _meshTris.Add(baseVert + 2);
                _meshTris.Add(baseVert);
                _meshTris.Add(baseVert + 2);
                _meshTris.Add(baseVert + 3);
            }

            Mesh mesh = new Mesh
            {
                name = $"StreetDecals_Cell_{bucket.Cx}_{bucket.Cz}_Mesh"
            };
            if (_meshVerts.Count > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(_meshVerts);
            mesh.SetNormals(_meshNormals);
            mesh.SetTangents(_meshTangents);
            mesh.SetUVs(0, _meshUvs);
            mesh.SetTriangles(_meshTris, 0, calculateBounds: true);
            mesh.UploadMeshData(markNoLongerReadable: true);

            GameObject cellGo = new GameObject($"StreetDecals_Cell_{bucket.Cx}_{bucket.Cz}");
            cellGo.transform.SetParent(transform, false);
            cellGo.transform.position = cellOrigin;

            MeshFilter mf = cellGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = cellGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _sharedMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.BlendProbes;

            _cells.Add(new CellEntry
            {
                Renderer = mr,
                Mesh = mesh,
                Center = cellOrigin + mesh.bounds.center
            });
        }

        // =====================================================================
        // STRING & HASH UTILITIES
        // =====================================================================
        private static bool ContainsAnyKeyword(string text, string[] keywords)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < keywords.Length; i++)
            {
                if (text.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static int StableStringHash(string s)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (s != null)
                {
                    for (int i = 0; i < s.Length; i++)
                    {
                        hash ^= s[i];
                        hash *= 16777619u;
                    }
                }
                return (int)hash;
            }
        }
    }
}
