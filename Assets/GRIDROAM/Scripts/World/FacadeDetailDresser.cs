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
    /// Runtime procedural neighborhood facade & rooftop detail dresser.
    /// Discovers residential/apartment buildings once after scene load, casts frame-sliced raycasts
    /// (<= 60/frame) to anchor authentic Istanbul facade/roof props (AC units, solar water heaters,
    /// satellite dishes, TV antennas, boiler flues, stacked downpipes, laundry lines, window grilles,
    /// rooftop water tanks), and bakes them into <= 40 spatially grouped 64x64m combined meshes
    /// sharing a single SRP-Batcher-compatible URP Lit material with 4 Hz distance culling.
    /// </summary>
    public sealed class FacadeDetailDresser : MonoBehaviour
    {
        public static FacadeDetailDresser Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_FacadeDetailDresser");
            go.AddComponent<FacadeDetailDresser>();
        }

        private const string KitResourceFolder = "OurWorldFacadeKit/";
        private const float CellSize = 64f;
        private const int MaxCells = 40;
        private const int MaxPartsMedium = 900;
        private const int MaxPartsLow = 450;
        private const int MaxRaycastsPerFrame = 60;
        private const double FrameBudgetMs = 1.8;
        private const float MinPartSpacingSq = 0.9f * 0.9f;
        private const float WallRayOffsetOut = 1.5f;
        private const float WallRayMaxDist = 2.5f;
        private const float WallSurfaceOffset = 0.01f;
        private const float FloorHeight = 3.0f;
        private const float BaySpacing = 3.2f;
        private const float CullIntervalSec = 0.25f; // 4 Hz
        private const float CullDistanceMedium = 140f;
        private const float CullDistanceLow = 90f;

        private static readonly string[] IncludeKeywords =
        {
            "apt", "apartment", "house", "building", "facade", "block", "konut", "ev_"
        };

        private static readonly string[] ExcludeKeywords =
        {
            "interior", "mall", "store_", "camii", "mosque", "garage",
            "roof", "window", "door", "sign", "shop_counter"
        };

        private static readonly string[] PluralContainerSuffixes =
        {
            "buildings", "apartments", "houses", "blocks"
        };

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private enum PartKind
        {
            SplitAC = 0,
            SolarHeater = 1,
            Dish = 2,
            TvAntenna = 3,
            BoilerFlue = 4,
            Downpipe = 5,
            LaundryLine = 6,
            WindowGrille = 7,
            WaterTank = 8,
            Count = 9
        }

        private static readonly string[] PartResourceNames =
        {
            "FK_SplitAC",
            "FK_SolarHeater",
            "FK_Dish",
            "FK_TvAntenna",
            "FK_BoilerFlue",
            "FK_Downpipe",
            "FK_LaundryLine",
            "FK_WindowGrille",
            "FK_WaterTank"
        };

        private struct PartTemplate
        {
            public Mesh Mesh;
            public Matrix4x4 LocalMatrix;
            public int VertexCount;
        }

        private struct FacadeInfo
        {
            public Vector3 Normal;
            public Vector3 Tangent;
            public float Width;
            public float HalfDepth;
            public bool IsFront;
        }

        private sealed class BuildingInfo
        {
            public Transform Root;
            public string Name;
            public Vector3 ObbCenter;
            public Vector3 Right;
            public Vector3 Forward;
            public float Width;
            public float Height;
            public float Depth;
            public float BaseY;
            public float TopY;
            public int Floors;
            public int NameHash;
        }

        private struct PlacedItem
        {
            public PartKind Kind;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private sealed class CellBucket
        {
            public int Cx;
            public int Cz;
            public readonly List<PlacedItem> Items = new List<PlacedItem>(32);
        }

        private struct CellEntry
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public Vector3 Center;
        }

        private readonly PartTemplate[] _templates = new PartTemplate[(int)PartKind.Count];
        private readonly List<CellEntry> _cells = new List<CellEntry>(MaxCells);
        private readonly List<Vector3> _buildingPlacedPositions = new List<Vector3>(32);
        private readonly List<Vector3> _buildingSolarPositions = new List<Vector3>(4);

        private Material _sharedMaterial;
        private Camera _cachedCam;
        private float _cullTimer;
        private float _cullDistanceSq = CullDistanceMedium * CullDistanceMedium;
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
            // Wait 1 frame so runtime world/building generators finish spawning
            yield return null;

            TrySubscribeTierEvent();
            _cachedCam = Camera.main;

            yield return StartCoroutine(BuildFacadeDetailsRoutine());
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
            if (!_setupComplete || _cells.Count == 0) return;

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
            bool isLow = tier == GraphicsTier.Low;
            float dist = isLow ? CullDistanceLow : CullDistanceMedium;
            _cullDistanceSq = dist * dist;
            ShadowCastingMode shadowMode = isLow ? ShadowCastingMode.Off : ShadowCastingMode.On;

            for (int i = 0; i < _cells.Count; i++)
            {
                MeshRenderer mr = _cells[i].Renderer;
                if (mr != null)
                {
                    mr.shadowCastingMode = shadowMode;
                }
            }
        }

        // =====================================================================
        // COROUTINE SETUP PIPELINE (FRAME-SLICED, <= 60 RAYCASTS / FRAME)
        // =====================================================================
        private IEnumerator BuildFacadeDetailsRoutine()
        {
            Stopwatch totalSw = Stopwatch.StartNew();
            Stopwatch frameSw = Stopwatch.StartNew();

            if (!LoadKitAssets())
            {
                yield break;
            }

            bool isLow = IsLowTier;
            int maxTotalParts = isLow ? MaxPartsLow : MaxPartsMedium;
            float probScale = isLow ? 0.5f : 1.0f;

            Quaternion solarRotation = ComputeSolarFacingRotation();

            // 1. Discover and group building candidates once
            List<BuildingInfo> buildings = DiscoverBuildings();
            if (buildings.Count == 0)
            {
                totalSw.Stop();
                _setupComplete = true;
                Debug.Log($"FacadeDetailDresser: 0 bina, 0 parça, 0 hücre, {totalSw.Elapsed.TotalMilliseconds:F1} ms");
                yield break;
            }

            if (frameSw.Elapsed.TotalMilliseconds >= FrameBudgetMs)
            {
                yield return null;
                frameSw.Restart();
            }

            // 2. Place facade & roof details across buildings (frame-sliced <= 60 raycasts/frame)
            Dictionary<long, CellBucket> buckets = new Dictionary<long, CellBucket>(MaxCells);
            int totalParts = 0;
            int dressedBuildings = 0;
            int raycastsThisFrame = 0;
            FacadeInfo[] facades = new FacadeInfo[4];

            for (int bIdx = 0; bIdx < buildings.Count && totalParts < maxTotalParts; bIdx++)
            {
                BuildingInfo b = buildings[bIdx];
                if (b.Root == null) continue;

                _buildingPlacedPositions.Clear();
                _buildingSolarPositions.Clear();

                System.Random rng = new System.Random(b.NameHash);
                int partsBeforeBuilding = totalParts;
                bool hasAC = false;

                BuildSortedFacades(b, facades);

                // Determine 2 longest facades (+ 3rd if open corner building)
                int facadeCount = 2;
                if (facades[2].Width >= 5.5f)
                {
                    Vector3 probeOrigin = b.ObbCenter + facades[2].Normal * (facades[2].HalfDepth + 0.35f);
                    raycastsThisFrame++;
                    bool blocked = Physics.Raycast(probeOrigin, facades[2].Normal, 3.5f, ~0, QueryTriggerInteraction.Ignore);
                    if (!blocked)
                    {
                        facadeCount = 3;
                    }
                    if (raycastsThisFrame >= MaxRaycastsPerFrame || frameSw.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                    {
                        raycastsThisFrame = 0;
                        yield return null;
                        frameSw.Restart();
                    }
                }

                // Identify front facade (prefer IsFront == true)
                int frontIdx = 0;
                for (int fi = 0; fi < 4; fi++)
                {
                    if (facades[fi].IsFront)
                    {
                        frontIdx = fi;
                        break;
                    }
                }
                FacadeInfo frontFace = facades[frontIdx];

                // 2a. Corner downpipe stack on 1 corner of front facade (Floors x 3m segments)
                if (HasTemplate(PartKind.Downpipe) && totalParts < maxTotalParts && (!isLow || rng.NextDouble() < 0.5))
                {
                    float cornerSign = rng.NextDouble() < 0.5 ? -1f : 1f;
                    float edgeOffset = cornerSign * Mathf.Max(0f, frontFace.Width * 0.5f - 0.28f);
                    float probeY = Mathf.Min(b.BaseY + 1.5f, b.TopY - 0.5f);
                    Vector3 basePt = GetFacadePoint(b, frontFace, edgeOffset, probeY);
                    raycastsThisFrame++;

                    if (TryWallRaycast(basePt, frontFace.Normal, b.Root, out RaycastHit dpHit))
                    {
                        Quaternion dpRot = Quaternion.LookRotation(dpHit.normal, Vector3.up);
                        Vector3 wallXZ = dpHit.point + dpHit.normal * WallSurfaceOffset;
                        int segCount = Mathf.Min(b.Floors, maxTotalParts - totalParts);
                        for (int s = 0; s < segCount; s++)
                        {
                            Vector3 segPos = new Vector3(wallXZ.x, b.BaseY + s * FloorHeight, wallXZ.z);
                            AddToCellBucket(buckets, PartKind.Downpipe, segPos, dpRot);
                            _buildingPlacedPositions.Add(segPos + Vector3.up * 1.5f);
                            totalParts++;
                        }
                    }

                    if (raycastsThisFrame >= MaxRaycastsPerFrame || frameSw.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                    {
                        raycastsThisFrame = 0;
                        yield return null;
                        frameSw.Restart();
                    }
                }

                // 2b. Ground-floor window grilles (k = 0, front facade only, 35%)
                if (HasTemplate(PartKind.WindowGrille) && totalParts < maxTotalParts)
                {
                    int gBays = ComputeBayCount(frontFace.Width);
                    float grilleY = b.BaseY + 1.35f;
                    if (grilleY < b.TopY - 0.8f)
                    {
                        for (int bay = 0; bay < gBays && totalParts < maxTotalParts; bay++)
                        {
                            if (rng.NextDouble() >= 0.35 * probScale) continue;

                            float along = GetBayCenterOffset(bay, gBays);
                            Vector3 targetPt = GetFacadePoint(b, frontFace, along, grilleY);
                            raycastsThisFrame++;

                            if (TryWallRaycast(targetPt, frontFace.Normal, b.Root, out RaycastHit hit))
                            {
                                Vector3 pos = hit.point + hit.normal * WallSurfaceOffset;
                                if (IsClearOfPlaced(pos))
                                {
                                    AddToCellBucket(buckets, PartKind.WindowGrille, pos, Quaternion.LookRotation(hit.normal, Vector3.up));
                                    _buildingPlacedPositions.Add(pos);
                                    totalParts++;
                                }
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

                // 2c. Upper floors (k >= 1) across 2 (or 3) longest facades
                for (int fIdx = 0; fIdx < facadeCount && totalParts < maxTotalParts; fIdx++)
                {
                    FacadeInfo face = facades[fIdx];
                    int bayCount = ComputeBayCount(face.Width);
                    float maxAlong = Mathf.Max(0f, face.Width * 0.5f - 0.75f);

                    for (int k = 1; k < b.Floors && totalParts < maxTotalParts; k++)
                    {
                        float floorBaseY = b.BaseY + k * FloorHeight;
                        if (floorBaseY + 2.4f > b.TopY) break;

                        for (int bay = 0; bay < bayCount && totalParts < maxTotalParts; bay++)
                        {
                            float bayCenter = GetBayCenterOffset(bay, bayCount);

                            // FK_SplitAC: 22% (window-sill height floorBaseY + 0.9m, +-0.6m horizontal)
                            if (HasTemplate(PartKind.SplitAC) && totalParts < maxTotalParts && rng.NextDouble() < 0.22 * probScale)
                            {
                                float jitter = (float)(rng.NextDouble() * 1.2 - 0.6);
                                float along = Mathf.Clamp(bayCenter + jitter, -maxAlong, maxAlong);
                                Vector3 pt = GetFacadePoint(b, face, along, floorBaseY + 0.9f);
                                raycastsThisFrame++;

                                if (TryWallRaycast(pt, face.Normal, b.Root, out RaycastHit hit))
                                {
                                    Vector3 pos = hit.point + hit.normal * WallSurfaceOffset;
                                    if (IsClearOfPlaced(pos))
                                    {
                                        AddToCellBucket(buckets, PartKind.SplitAC, pos, Quaternion.LookRotation(hit.normal, Vector3.up));
                                        _buildingPlacedPositions.Add(pos);
                                        totalParts++;
                                        hasAC = true;
                                    }
                                }
                            }

                            // FK_BoilerFlue: 14% (floorBaseY + 2.3m)
                            if (HasTemplate(PartKind.BoilerFlue) && totalParts < maxTotalParts && rng.NextDouble() < 0.14 * probScale)
                            {
                                float jitter = (float)(rng.NextDouble() * 0.8 - 0.4);
                                float along = Mathf.Clamp(bayCenter + jitter, -maxAlong, maxAlong);
                                Vector3 pt = GetFacadePoint(b, face, along, floorBaseY + 2.3f);
                                raycastsThisFrame++;

                                if (TryWallRaycast(pt, face.Normal, b.Root, out RaycastHit hit))
                                {
                                    Vector3 pos = hit.point + hit.normal * WallSurfaceOffset;
                                    if (IsClearOfPlaced(pos))
                                    {
                                        AddToCellBucket(buckets, PartKind.BoilerFlue, pos, Quaternion.LookRotation(hit.normal, Vector3.up));
                                        _buildingPlacedPositions.Add(pos);
                                        totalParts++;
                                    }
                                }
                            }

                            // FK_Dish: 6% (floorBaseY + 1.85m)
                            if (HasTemplate(PartKind.Dish) && totalParts < maxTotalParts && rng.NextDouble() < 0.06 * probScale)
                            {
                                float jitter = (float)(rng.NextDouble() * 0.8 - 0.4);
                                float along = Mathf.Clamp(bayCenter + jitter, -maxAlong, maxAlong);
                                Vector3 pt = GetFacadePoint(b, face, along, floorBaseY + 1.85f);
                                raycastsThisFrame++;

                                if (TryWallRaycast(pt, face.Normal, b.Root, out RaycastHit hit))
                                {
                                    Vector3 pos = hit.point + hit.normal * WallSurfaceOffset;
                                    if (IsClearOfPlaced(pos))
                                    {
                                        AddToCellBucket(buckets, PartKind.Dish, pos, Quaternion.LookRotation(hit.normal, Vector3.up));
                                        _buildingPlacedPositions.Add(pos);
                                        totalParts++;
                                    }
                                }
                            }

                            // FK_LaundryLine: 5% (only k >= 1, floorBaseY + 0.85m)
                            if (HasTemplate(PartKind.LaundryLine) && totalParts < maxTotalParts && rng.NextDouble() < 0.05 * probScale)
                            {
                                float along = Mathf.Clamp(bayCenter, -maxAlong, maxAlong);
                                Vector3 pt = GetFacadePoint(b, face, along, floorBaseY + 0.85f);
                                raycastsThisFrame++;

                                if (TryWallRaycast(pt, face.Normal, b.Root, out RaycastHit hit))
                                {
                                    Vector3 pos = hit.point + hit.normal * WallSurfaceOffset;
                                    if (IsClearOfPlaced(pos))
                                    {
                                        AddToCellBucket(buckets, PartKind.LaundryLine, pos, Quaternion.LookRotation(hit.normal, Vector3.up));
                                        _buildingPlacedPositions.Add(pos);
                                        totalParts++;
                                    }
                                }
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

                // 2d. Guarantee at least 1 AC unit per building (if budget remains)
                if (!hasAC && HasTemplate(PartKind.SplitAC) && totalParts < maxTotalParts)
                {
                    float acY = b.Floors >= 2 ? (b.BaseY + FloorHeight + 0.9f) : Mathf.Min(b.BaseY + 2.4f, b.TopY - 0.8f);
                    int acBays = ComputeBayCount(frontFace.Width);
                    float maxAlong = Mathf.Max(0f, frontFace.Width * 0.5f - 0.75f);

                    for (int attempt = 0; attempt < 3 && !hasAC; attempt++)
                    {
                        int bayIdx = attempt % acBays;
                        float along = Mathf.Clamp(GetBayCenterOffset(bayIdx, acBays) + (attempt == 0 ? 0f : (attempt == 1 ? 0.55f : -0.55f)), -maxAlong, maxAlong);
                        Vector3 pt = GetFacadePoint(b, frontFace, along, acY);
                        raycastsThisFrame++;

                        if (TryWallRaycast(pt, frontFace.Normal, b.Root, out RaycastHit hit))
                        {
                            Vector3 pos = hit.point + hit.normal * WallSurfaceOffset;
                            if (IsClearOfPlaced(pos))
                            {
                                AddToCellBucket(buckets, PartKind.SplitAC, pos, Quaternion.LookRotation(hit.normal, Vector3.up));
                                _buildingPlacedPositions.Add(pos);
                                totalParts++;
                                hasAC = true;
                            }
                        }

                        if (raycastsThisFrame >= MaxRaycastsPerFrame || frameSw.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                        {
                            raycastsThisFrame = 0;
                            yield return null;
                            frameSw.Restart();
                        }
                    }
                }

                // 2e. Rooftop placement: raycast down from TopY + 2m
                if (totalParts < maxTotalParts)
                {
                    float roofMaxDist = Mathf.Clamp(b.Height * 0.5f + 3.0f, 4.0f, 10.0f);
                    Vector3 centerRayStart = new Vector3(b.ObbCenter.x, b.TopY + 2.0f, b.ObbCenter.z);
                    raycastsThisFrame++;

                    if (Physics.Raycast(centerRayStart, Vector3.down, out RaycastHit roofProbe, roofMaxDist, ~0, QueryTriggerInteraction.Ignore) &&
                        IsOwnCollider(roofProbe.collider, b.Root))
                    {
                        bool isFlatRoof = roofProbe.normal.y > 0.95f;

                        if (isFlatRoof)
                        {
                            // Solar Heater: 2-6 floor flat roofs, 60% -> 1-2 units facing opposite directional light
                            if (HasTemplate(PartKind.SolarHeater) &&
                                b.Floors >= 2 && b.Floors <= 6 &&
                                b.Width >= 3.0f && b.Depth >= 3.0f &&
                                totalParts < maxTotalParts &&
                                rng.NextDouble() < 0.60 * probScale)
                            {
                                int count = isLow ? 1 : (rng.NextDouble() < 0.45 ? 2 : 1);
                                float halfW = Mathf.Max(0f, b.Width * 0.5f - 1.35f);
                                float halfD = Mathf.Max(0f, b.Depth * 0.5f - 1.35f);

                                for (int sh = 0; sh < count && totalParts < maxTotalParts; sh++)
                                {
                                    for (int tries = 0; tries < 3; tries++)
                                    {
                                        float rx = (float)(rng.NextDouble() * 2.0 - 1.0) * halfW;
                                        float rz = (float)(rng.NextDouble() * 2.0 - 1.0) * halfD;
                                        Vector3 rayStart = b.ObbCenter + b.Right * rx + b.Forward * rz;
                                        rayStart.y = b.TopY + 2.0f;
                                        raycastsThisFrame++;

                                        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, roofMaxDist, ~0, QueryTriggerInteraction.Ignore) &&
                                            IsOwnCollider(hit.collider, b.Root) &&
                                            hit.normal.y > 0.95f &&
                                            IsClearOfPlaced(hit.point) &&
                                            IsClearOfList(_buildingSolarPositions, hit.point, 2.35f * 2.35f))
                                        {
                                            AddToCellBucket(buckets, PartKind.SolarHeater, hit.point, solarRotation);
                                            _buildingPlacedPositions.Add(hit.point);
                                            _buildingSolarPositions.Add(hit.point);
                                            totalParts++;
                                            break;
                                        }
                                    }
                                }
                            }

                            // TV Antenna: 50% on flat roofs (>= 1m inside roof edge)
                            if (HasTemplate(PartKind.TvAntenna) && totalParts < maxTotalParts && rng.NextDouble() < 0.50 * probScale)
                            {
                                float halfW = Mathf.Max(0f, b.Width * 0.5f - 1.05f);
                                float halfD = Mathf.Max(0f, b.Depth * 0.5f - 1.05f);
                                float rx = (float)(rng.NextDouble() * 2.0 - 1.0) * halfW;
                                float rz = (float)(rng.NextDouble() * 2.0 - 1.0) * halfD;
                                Vector3 rayStart = b.ObbCenter + b.Right * rx + b.Forward * rz;
                                rayStart.y = b.TopY + 2.0f;
                                raycastsThisFrame++;

                                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, roofMaxDist, ~0, QueryTriggerInteraction.Ignore) &&
                                    IsOwnCollider(hit.collider, b.Root) &&
                                    hit.normal.y > 0.95f &&
                                    IsClearOfPlaced(hit.point))
                                {
                                    Quaternion yawRot = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                                    AddToCellBucket(buckets, PartKind.TvAntenna, hit.point, yawRot);
                                    _buildingPlacedPositions.Add(hit.point);
                                    totalParts++;
                                }
                            }

                            // Water Tank: 25% on flat roofs (>= 1m inside roof edge)
                            if (HasTemplate(PartKind.WaterTank) && totalParts < maxTotalParts && rng.NextDouble() < 0.25 * probScale)
                            {
                                float halfW = Mathf.Max(0f, b.Width * 0.5f - 1.15f);
                                float halfD = Mathf.Max(0f, b.Depth * 0.5f - 1.15f);
                                float rx = (float)(rng.NextDouble() * 2.0 - 1.0) * halfW;
                                float rz = (float)(rng.NextDouble() * 2.0 - 1.0) * halfD;
                                Vector3 rayStart = b.ObbCenter + b.Right * rx + b.Forward * rz;
                                rayStart.y = b.TopY + 2.0f;
                                raycastsThisFrame++;

                                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, roofMaxDist, ~0, QueryTriggerInteraction.Ignore) &&
                                    IsOwnCollider(hit.collider, b.Root) &&
                                    hit.normal.y > 0.95f &&
                                    IsClearOfPlaced(hit.point))
                                {
                                    Quaternion yawRot = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                                    AddToCellBucket(buckets, PartKind.WaterTank, hit.point, yawRot);
                                    _buildingPlacedPositions.Add(hit.point);
                                    totalParts++;
                                }
                            }
                        }
                        else if (roofProbe.normal.y > 0.20f)
                        {
                            // Pitched / tiled roof: ONLY TV Antenna (40%), placed strictly UPRIGHT (not normal-aligned)
                            if (HasTemplate(PartKind.TvAntenna) && totalParts < maxTotalParts && rng.NextDouble() < 0.40 * probScale)
                            {
                                float halfW = Mathf.Max(0f, b.Width * 0.5f - 1.15f) * 0.65f;
                                float halfD = Mathf.Max(0f, b.Depth * 0.5f - 1.15f) * 0.65f;
                                float rx = (float)(rng.NextDouble() * 2.0 - 1.0) * halfW;
                                float rz = (float)(rng.NextDouble() * 2.0 - 1.0) * halfD;
                                Vector3 rayStart = b.ObbCenter + b.Right * rx + b.Forward * rz;
                                rayStart.y = b.TopY + 2.0f;
                                raycastsThisFrame++;

                                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, roofMaxDist, ~0, QueryTriggerInteraction.Ignore) &&
                                    IsOwnCollider(hit.collider, b.Root) &&
                                    IsClearOfPlaced(hit.point))
                                {
                                    Quaternion uprightRot = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                                    Vector3 pos = hit.point - Vector3.up * 0.04f;
                                    AddToCellBucket(buckets, PartKind.TvAntenna, pos, uprightRot);
                                    _buildingPlacedPositions.Add(pos);
                                    totalParts++;
                                }
                            }
                        }
                    }

                    if (raycastsThisFrame >= MaxRaycastsPerFrame || frameSw.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                    {
                        raycastsThisFrame = 0;
                        yield return null;
                        frameSw.Restart();
                    }
                }

                if (totalParts > partsBeforeBuilding)
                {
                    dressedBuildings++;
                }
            }

            // 3. Enforce <= 40 cell meshes by merging smallest buckets into nearest spatial neighbor
            List<CellBucket> finalBuckets = new List<CellBucket>(buckets.Values);
            MergeExcessBuckets(finalBuckets, MaxCells);

            // 4. Bake combined cell meshes (frame-sliced <= 1.8 ms/frame)
            ShadowCastingMode shadowMode = IsLowTier ? ShadowCastingMode.Off : ShadowCastingMode.On;
            for (int i = 0; i < finalBuckets.Count; i++)
            {
                BuildCellMeshObject(finalBuckets[i], shadowMode);
                if (frameSw.Elapsed.TotalMilliseconds >= FrameBudgetMs)
                {
                    yield return null;
                    frameSw.Restart();
                }
            }

            totalSw.Stop();
            _setupComplete = true;
            _cullTimer = CullIntervalSec; // Trigger immediate first visibility pass on next Update

            Debug.Log($"FacadeDetailDresser: {dressedBuildings} bina, {totalParts} parça, {_cells.Count} hücre, {totalSw.Elapsed.TotalMilliseconds:F1} ms");
        }

        // =====================================================================
        // ASSET LOADING & SHARED MATERIAL
        // =====================================================================
        private bool LoadKitAssets()
        {
            Texture2D albedo = Resources.Load<Texture2D>(KitResourceFolder + "FK_PaletteAlbedo");
            Texture2D metalSmooth = Resources.Load<Texture2D>(KitResourceFolder + "FK_PaletteMetalSmooth");
            if (albedo == null)
            {
                Debug.LogWarning("[FacadeDetailDresser] Missing Resources/OurWorldFacadeKit/FK_PaletteAlbedo. Skipping facade dressing.");
                return false;
            }

            _sharedMaterial = BuildSharedMaterial(albedo, metalSmooth);

            int loadedCount = 0;
            bool anyUnreadable = false;
            for (int i = 0; i < (int)PartKind.Count; i++)
            {
                string path = KitResourceFolder + PartResourceNames[i];
                Mesh mesh = null;
                Matrix4x4 localMatrix = Matrix4x4.identity;

                GameObject prefab = Resources.Load<GameObject>(path);
                if (prefab != null)
                {
                    MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>(true);
                    if (mf != null && mf.sharedMesh != null)
                    {
                        mesh = mf.sharedMesh;
                        if (mf.transform != prefab.transform)
                        {
                            localMatrix = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                        }
                    }
                }
                else
                {
                    mesh = Resources.Load<Mesh>(path);
                }

                if (mesh == null) continue;
                if (!mesh.isReadable)
                {
                    anyUnreadable = true;
                    continue;
                }

                _templates[i] = new PartTemplate
                {
                    Mesh = mesh,
                    LocalMatrix = localMatrix,
                    VertexCount = mesh.vertexCount
                };
                loadedCount++;
            }

            if (anyUnreadable)
            {
                Debug.LogWarning("[FacadeDetailDresser] One or more FK_*.fbx meshes have Read/Write disabled. Run 'OurWorld/Art/Reimport Facade Detail Kit' in Unity Editor.");
            }

            if (loadedCount == 0)
            {
                Debug.LogWarning("[FacadeDetailDresser] No readable FK_*.fbx meshes found under Resources/OurWorldFacadeKit/.");
                return false;
            }

            return true;
        }

        private static Material BuildSharedMaterial(Texture2D albedo, Texture2D metalSmooth)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader)
            {
                name = "FacadeKit_SharedLit",
                enableInstancing = false // SRP Batcher compatible; GPU instancing disabled
            };

            if (mat.HasProperty(BaseMapId)) mat.SetTexture(BaseMapId, albedo);
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, Color.white);

            if (metalSmooth != null && mat.HasProperty(MetallicGlossMapId))
            {
                mat.SetTexture(MetallicGlossMapId, metalSmooth);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            if (mat.HasProperty(MetallicId)) mat.SetFloat(MetallicId, 1f);
            if (mat.HasProperty(SmoothnessId)) mat.SetFloat(SmoothnessId, 1f);
            return mat;
        }

        private bool HasTemplate(PartKind kind) => _templates[(int)kind].Mesh != null;

        // =====================================================================
        // BUILDING DISCOVERY & OBB EXTRACTION
        // =====================================================================
        private static List<BuildingInfo> DiscoverBuildings()
        {
            MeshRenderer[] allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
            Dictionary<Transform, List<MeshRenderer>> groups = new Dictionary<Transform, List<MeshRenderer>>(64);

            for (int i = 0; i < allRenderers.Length; i++)
            {
                MeshRenderer mr = allRenderers[i];
                if (mr == null || !mr.enabled) continue;

                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                Transform root = ResolveBuildingRoot(mr.transform);
                if (root == null) continue;

                if (!groups.TryGetValue(root, out List<MeshRenderer> list))
                {
                    list = new List<MeshRenderer>(8);
                    groups[root] = list;
                }
                list.Add(mr);
            }

            List<BuildingInfo> buildings = new List<BuildingInfo>(groups.Count);
            Vector3[] localCorners = new Vector3[8];

            foreach (KeyValuePair<Transform, List<MeshRenderer>> kv in groups)
            {
                Transform root = kv.Key;
                List<MeshRenderer> rends = kv.Value;
                if (root == null || rends.Count == 0) continue;

                Vector3 fwd = root.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
                else fwd.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                Vector3 origin = root.position;

                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                bool anyPoint = false;

                for (int r = 0; r < rends.Count; r++)
                {
                    MeshRenderer mr = rends[r];
                    MeshFilter mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;

                    Bounds lb = mf.sharedMesh.bounds;
                    Vector3 c = lb.center;
                    Vector3 e = lb.extents;
                    localCorners[0] = new Vector3(c.x - e.x, c.y - e.y, c.z - e.z);
                    localCorners[1] = new Vector3(c.x + e.x, c.y - e.y, c.z - e.z);
                    localCorners[2] = new Vector3(c.x - e.x, c.y + e.y, c.z - e.z);
                    localCorners[3] = new Vector3(c.x + e.x, c.y + e.y, c.z - e.z);
                    localCorners[4] = new Vector3(c.x - e.x, c.y - e.y, c.z + e.z);
                    localCorners[5] = new Vector3(c.x + e.x, c.y - e.y, c.z + e.z);
                    localCorners[6] = new Vector3(c.x - e.x, c.y + e.y, c.z + e.z);
                    localCorners[7] = new Vector3(c.x + e.x, c.y + e.y, c.z + e.z);

                    Matrix4x4 l2w = mr.localToWorldMatrix;
                    for (int ci = 0; ci < 8; ci++)
                    {
                        Vector3 wp = l2w.MultiplyPoint3x4(localCorners[ci]);
                        Vector3 rel = wp - origin;
                        float px = Vector3.Dot(rel, right);
                        float pz = Vector3.Dot(rel, fwd);
                        if (px < minX) minX = px;
                        if (px > maxX) maxX = px;
                        if (wp.y < minY) minY = wp.y;
                        if (wp.y > maxY) maxY = wp.y;
                        if (pz < minZ) minZ = pz;
                        if (pz > maxZ) maxZ = pz;
                        anyPoint = true;
                    }
                }

                if (!anyPoint) continue;

                float width = maxX - minX;
                float height = maxY - minY;
                float depth = maxZ - minZ;

                // Candidate size filter: height >= 5m and at least one horizontal edge >= 6m
                if (height < 5.0f) continue;
                if (width < 6.0f && depth < 6.0f) continue;
                // Safety guard against accidentally grouping an entire district container
                if (width > 80.0f && depth > 80.0f) continue;

                int floors = Mathf.FloorToInt(height / FloorHeight);
                if (floors < 1) continue;

                Vector3 obbCenter = origin + right * ((minX + maxX) * 0.5f) + fwd * ((minZ + maxZ) * 0.5f);
                obbCenter.y = (minY + maxY) * 0.5f;

                buildings.Add(new BuildingInfo
                {
                    Root = root,
                    Name = root.name,
                    ObbCenter = obbCenter,
                    Right = right,
                    Forward = fwd,
                    Width = width,
                    Height = height,
                    Depth = depth,
                    BaseY = minY,
                    TopY = maxY,
                    Floors = floors,
                    NameHash = StableHash(root.name)
                });
            }

            // Deterministic ordering across sessions
            buildings.Sort((a, b) =>
            {
                int cmp = a.NameHash.CompareTo(b.NameHash);
                if (cmp != 0) return cmp;
                cmp = a.ObbCenter.x.CompareTo(b.ObbCenter.x);
                return cmp != 0 ? cmp : a.ObbCenter.z.CompareTo(b.ObbCenter.z);
            });

            return buildings;
        }

        private static Transform ResolveBuildingRoot(Transform leaf)
        {
            Transform highestMatch = null;
            Transform cur = leaf;

            while (cur != null)
            {
                string n = cur.name;
                if (!string.IsNullOrEmpty(n))
                {
                    if (ContainsAnyIgnoreCase(n, ExcludeKeywords))
                    {
                        return null;
                    }

                    if (ContainsAnyIgnoreCase(n, IncludeKeywords) && !IsPluralMegaContainer(n))
                    {
                        highestMatch = cur;
                    }
                }
                cur = cur.parent;
            }

            return highestMatch;
        }

        private static bool ContainsAnyIgnoreCase(string text, string[] keywords)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                if (text.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsPluralMegaContainer(string name)
        {
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsDigit(name[i])) return false;
            }
            for (int i = 0; i < PluralContainerSuffixes.Length; i++)
            {
                if (name.EndsWith(PluralContainerSuffixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // =====================================================================
        // GEOMETRY, RAYCAST & SPACING HELPERS
        // =====================================================================
        private static void BuildSortedFacades(BuildingInfo b, FacadeInfo[] dst)
        {
            dst[0] = new FacadeInfo { Normal = b.Forward, Tangent = b.Right, Width = b.Width, HalfDepth = b.Depth * 0.5f, IsFront = true };
            dst[1] = new FacadeInfo { Normal = -b.Forward, Tangent = -b.Right, Width = b.Width, HalfDepth = b.Depth * 0.5f, IsFront = false };
            dst[2] = new FacadeInfo { Normal = b.Right, Tangent = -b.Forward, Width = b.Depth, HalfDepth = b.Width * 0.5f, IsFront = false };
            dst[3] = new FacadeInfo { Normal = -b.Right, Tangent = b.Forward, Width = b.Depth, HalfDepth = b.Width * 0.5f, IsFront = false };

            // Stable insertion sort descending by Width (keeps Front facade first on ties)
            for (int i = 1; i < 4; i++)
            {
                FacadeInfo key = dst[i];
                int j = i - 1;
                while (j >= 0 && dst[j].Width < key.Width)
                {
                    dst[j + 1] = dst[j];
                    j--;
                }
                dst[j + 1] = key;
            }
        }

        private static int ComputeBayCount(float faceWidth)
        {
            float usable = Mathf.Max(0f, faceWidth - 1.4f);
            return Mathf.Max(1, Mathf.FloorToInt(usable / BaySpacing));
        }

        private static float GetBayCenterOffset(int bayIdx, int bayCount)
        {
            return (bayIdx - (bayCount - 1) * 0.5f) * BaySpacing;
        }

        private static Vector3 GetFacadePoint(BuildingInfo b, FacadeInfo face, float alongTangent, float worldY)
        {
            Vector3 p = b.ObbCenter + face.Normal * face.HalfDepth + face.Tangent * alongTangent;
            p.y = worldY;
            return p;
        }

        private static bool TryWallRaycast(Vector3 facePoint, Vector3 outwardNormal, Transform buildingRoot, out RaycastHit hit)
        {
            Vector3 rayOrigin = facePoint + outwardNormal * WallRayOffsetOut;
            if (!Physics.Raycast(rayOrigin, -outwardNormal, out hit, WallRayMaxDist, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            if (!IsOwnCollider(hit.collider, buildingRoot))
            {
                return false;
            }
            return Mathf.Abs(hit.normal.y) <= 0.2f;
        }

        private static bool IsOwnCollider(Collider col, Transform root)
        {
            if (col == null || root == null) return false;
            Transform t = col.transform;
            return t == root || t.IsChildOf(root);
        }

        private bool IsClearOfPlaced(Vector3 pos)
        {
            return IsClearOfList(_buildingPlacedPositions, pos, MinPartSpacingSq);
        }

        private static bool IsClearOfList(List<Vector3> points, Vector3 pos, float minDistSq)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 d = points[i] - pos;
                if (d.x * d.x + d.y * d.y + d.z * d.z < minDistSq)
                {
                    return false;
                }
            }
            return true;
        }

        private static Quaternion ComputeSolarFacingRotation()
        {
            Light sun = RenderSettings.sun;
            if (sun == null || sun.type != LightType.Directional)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null && lights[i].type == LightType.Directional)
                    {
                        sun = lights[i];
                        break;
                    }
                }
            }

            Vector3 dir;
            if (sun != null)
            {
                Vector3 f = sun.transform.forward;
                dir = new Vector3(-f.x, 0f, -f.z);
            }
            else
            {
                dir = new Vector3(0.55f, 0f, -0.83f);
            }

            if (dir.sqrMagnitude < 1e-4f)
            {
                dir = new Vector3(0.55f, 0f, -0.83f);
            }
            dir.Normalize();
            return Quaternion.LookRotation(dir, Vector3.up);
        }

        private static int StableHash(string s)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (!string.IsNullOrEmpty(s))
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

        // =====================================================================
        // 64x64m SPATIAL CELL BUCKETING & MESH COMBINING
        // =====================================================================
        private static void AddToCellBucket(Dictionary<long, CellBucket> buckets, PartKind kind, Vector3 pos, Quaternion rot)
        {
            int cx = Mathf.FloorToInt(pos.x / CellSize);
            int cz = Mathf.FloorToInt(pos.z / CellSize);
            long key = ((long)cx << 32) | (uint)cz;

            if (!buckets.TryGetValue(key, out CellBucket bucket))
            {
                bucket = new CellBucket { Cx = cx, Cz = cz };
                buckets[key] = bucket;
            }

            bucket.Items.Add(new PlacedItem
            {
                Kind = kind,
                Position = pos,
                Rotation = rot
            });
        }

        private static void MergeExcessBuckets(List<CellBucket> list, int maxCount)
        {
            while (list.Count > maxCount)
            {
                int smallestIdx = 0;
                int smallestCount = list[0].Items.Count;
                for (int i = 1; i < list.Count; i++)
                {
                    if (list[i].Items.Count < smallestCount)
                    {
                        smallestCount = list[i].Items.Count;
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
                list[nearestIdx].Items.AddRange(src.Items);
                list.RemoveAt(smallestIdx);
            }
        }

        private void BuildCellMeshObject(CellBucket bucket, ShadowCastingMode shadowMode)
        {
            int count = bucket.Items.Count;
            if (count == 0) return;

            Vector3 cellOrigin = new Vector3(bucket.Cx * CellSize + CellSize * 0.5f, 0f, bucket.Cz * CellSize + CellSize * 0.5f);
            CombineInstance[] combines = new CombineInstance[count];
            int totalVerts = 0;
            int valid = 0;

            for (int i = 0; i < count; i++)
            {
                PlacedItem item = bucket.Items[i];
                PartTemplate tpl = _templates[(int)item.Kind];
                if (tpl.Mesh == null) continue;

                Matrix4x4 trs = Matrix4x4.TRS(item.Position - cellOrigin, item.Rotation, Vector3.one);
                combines[valid] = new CombineInstance
                {
                    mesh = tpl.Mesh,
                    subMeshIndex = 0,
                    transform = trs * tpl.LocalMatrix
                };
                totalVerts += tpl.VertexCount;
                valid++;
            }

            if (valid == 0) return;
            if (valid < count)
            {
                Array.Resize(ref combines, valid);
            }

            Mesh combinedMesh = new Mesh
            {
                name = $"FacadeKit_Cell_{bucket.Cx}_{bucket.Cz}_Mesh"
            };
            if (totalVerts > 65535)
            {
                combinedMesh.indexFormat = IndexFormat.UInt32;
            }
            combinedMesh.CombineMeshes(combines, mergeSubMeshes: true, useMatrices: true, hasLightmapData: false);
            combinedMesh.RecalculateBounds();
            combinedMesh.UploadMeshData(markNoLongerReadable: true);

            GameObject cellGo = new GameObject($"FacadeKit_Cell_{bucket.Cx}_{bucket.Cz}");
            cellGo.transform.SetParent(transform, false);
            cellGo.transform.position = cellOrigin;

            MeshFilter mf = cellGo.AddComponent<MeshFilter>();
            mf.sharedMesh = combinedMesh;

            MeshRenderer mr = cellGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _sharedMaterial;
            mr.staticShadowCaster = true;
            mr.shadowCastingMode = shadowMode;
            mr.receiveShadows = true;

            _cells.Add(new CellEntry
            {
                Renderer = mr,
                Mesh = combinedMesh,
                Center = cellOrigin + combinedMesh.bounds.center
            });
        }
    }
}
