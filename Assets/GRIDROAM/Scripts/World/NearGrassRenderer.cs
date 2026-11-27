using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsTier = OurWorld.Core.GraphicsTier;

namespace OurWorld.World
{
    /// <summary>
    /// Medium-tier near-player instanced grass renderer (zero GC in Update):
    /// - 36 m radius, 3 m spatial grid around the player.
    /// - Probes each entering cell once with downward Physics.RaycastNonAlloc against a pre-built
    ///   grass/meadow/lawn/park/pasture collider lookup; caches 6-10 deterministic grass matrices per cell
    ///   and evicts cells leaving range back into a pre-allocated pool.
    /// - Renders <= 3500 instances with Graphics.RenderMeshInstanced using a single shared URP Lit
    ///   alpha-clipped double-sided instanced material, thinning by 50% beyond 28 m from camera.
    /// </summary>
    public sealed class NearGrassRenderer : MonoBehaviour
    {
        public static NearGrassRenderer Instance { get; private set; }

        private const string GrassMeshResource = "OurWorldTrees/TR_GrassClump";
        private const string GrassAtlasResource = "OurWorldTrees/TR_GrassAtlas";

        private const float CellSize = 3.0f;
        private const float InvCellSize = 1.0f / CellSize;
        private const float ActiveRadius = 36.0f;
        private const float ActiveRadiusSq = ActiveRadius * ActiveRadius;
        private const float RenderCullRadiusSq = 37.5f * 37.5f;
        private const float EvictRadiusSq = 42.0f * 42.0f;
        private const float ThinDistanceSq = 28.0f * 28.0f;
        private const int CellRadius = 12; // 36 m / 3 m

        private const int MaxInstancesPerCell = 10;
        private const int MaxTotalInstances = 3500;
        private const int MaxBatchSize = 1023;
        private const int MaxNewCellsPerFrame = 20;
        private const int InitialPoolCapacity = 640;
        private const float SetupFrameBudgetSeconds = 0.0018f;

        private sealed class CellData
        {
            public long Key;
            public float CenterX;
            public float CenterZ;
            public int Count;
            public readonly Matrix4x4[] Matrices = new Matrix4x4[MaxInstancesPerCell];
        }

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int CullId = Shader.PropertyToID("_Cull");

        private readonly Dictionary<int, bool> _colliderGrassLookup = new Dictionary<int, bool>(512);

        // Flat hard surfaces (road / sidewalk / parke / plaza / water) indexed by 16 m XZ buckets.
        // Many roads and sidewalks are visual-only meshes lying on top of a large grass ground collider,
        // so the collider test alone would scatter grass across asphalt; every clump is checked here too.
        private struct HardRect { public float MinX, MinZ, MaxX, MaxZ, TopY; }
        private const float HardBucketSize = 16f;
        private const float HardRectPad = 0.45f;
        private readonly Dictionary<long, List<HardRect>> _hardRects = new Dictionary<long, List<HardRect>>(1024);
        private readonly Dictionary<long, CellData> _cellMap = new Dictionary<long, CellData>(640);
        private readonly List<CellData> _activeCells = new List<CellData>(640);
        private readonly Stack<CellData> _cellPool = new Stack<CellData>(InitialPoolCapacity);
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];
        private readonly Matrix4x4[] _drawMatrices = new Matrix4x4[MaxTotalInstances];

        private Mesh _grassMesh;
        private Material _grassMat;
        private RenderParams _renderParams;
        private Camera _cachedCam;
        private bool _ready;
        private bool _isActiveTier;
        private bool _subscribedTier;
        private int _lastCenterCx = int.MinValue;
        private int _lastCenterCz = int.MinValue;
        private int _scanCursor;
        private int _scanTotal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;

            GameObject go = new GameObject("OurWorld_NearGrassRenderer");
            go.AddComponent<NearGrassRenderer>();
        }

        private static GraphicsTier ActiveTier =>
            GraphicsProfileManager.Instance != null
                ? GraphicsProfileManager.Instance.CurrentTier
                : GraphicsTier.Medium;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Pre-allocate cell pool so runtime movement allocates zero heap memory
            for (int i = 0; i < InitialPoolCapacity; i++)
            {
                _cellPool.Push(new CellData());
            }

            _scanTotal = (CellRadius * 2 + 1) * (CellRadius * 2 + 1);
        }

        private IEnumerator Start()
        {
            // Wait 3 frames so runtime world/district/park builders finish spawning ground colliders
            yield return null;
            yield return null;
            yield return null;

            if (!LoadGrassMeshAndMaterial())
            {
                Debug.LogWarning("[NearGrassRenderer] Grass assets under Resources/OurWorldTrees/ not ready; disabling.");
                yield break;
            }

            if (GraphicsProfileManager.Instance != null && !_subscribedTier)
            {
                GraphicsProfileManager.Instance.OnTierChanged += HandleTierChanged;
                _subscribedTier = true;
            }

            _isActiveTier = ActiveTier == GraphicsTier.Medium;

            // Pre-index all scene colliders in time-sliced chunks so Update never calls GetComponent
            yield return BuildColliderGrassLookupRoutine();
            yield return BuildHardSurfaceIndexRoutine();

            _ready = true;
        }

        private void OnDestroy()
        {
            if (_subscribedTier && GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged -= HandleTierChanged;
                _subscribedTier = false;
            }

            if (_grassMat != null)
            {
                Destroy(_grassMat);
                _grassMat = null;
            }

            if (Instance == this) Instance = null;
        }

        private void HandleTierChanged(GraphicsTier tier)
        {
            _isActiveTier = tier == GraphicsTier.Medium;
            if (!_isActiveTier)
            {
                ReleaseAllCachedCells();
            }
        }

        private bool LoadGrassMeshAndMaterial()
        {
            GameObject prefab = Resources.Load<GameObject>(GrassMeshResource);
            if (prefab == null) return false;

            MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null) return false;
            _grassMesh = mf.sharedMesh;

            Texture2D atlas = Resources.Load<Texture2D>(GrassAtlasResource);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (urpLit == null) return false;

            _grassMat = new Material(urpLit)
            {
                name = "MAT_TR_InstancedGrass",
                enableInstancing = true
            };

            if (atlas != null)
            {
                if (_grassMat.HasProperty(BaseMapId)) _grassMat.SetTexture(BaseMapId, atlas);
                if (_grassMat.HasProperty(MainTexId)) _grassMat.SetTexture(MainTexId, atlas);
            }
            if (_grassMat.HasProperty(BaseColorId)) _grassMat.SetColor(BaseColorId, new Color(0.70f, 0.76f, 0.60f, 1f)); // tone toward Grass001 ground, avoid neon clumps
            if (_grassMat.HasProperty(ColorId)) _grassMat.SetColor(ColorId, Color.white);
            if (_grassMat.HasProperty(SmoothnessId)) _grassMat.SetFloat(SmoothnessId, 0.12f);
            if (_grassMat.HasProperty(MetallicId)) _grassMat.SetFloat(MetallicId, 0.0f);
            if (_grassMat.HasProperty(SurfaceId)) _grassMat.SetFloat(SurfaceId, 0f);
            if (_grassMat.HasProperty(AlphaClipId)) _grassMat.SetFloat(AlphaClipId, 1f);
            if (_grassMat.HasProperty(CutoffId)) _grassMat.SetFloat(CutoffId, 0.45f);
            if (_grassMat.HasProperty(CullId)) _grassMat.SetFloat(CullId, (float)CullMode.Off);
            _grassMat.EnableKeyword("_ALPHATEST_ON");
            _grassMat.SetOverrideTag("RenderType", "TransparentCutout");
            _grassMat.renderQueue = (int)RenderQueue.AlphaTest;

            _renderParams = new RenderParams(_grassMat)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(Vector3.zero, new Vector3(96f, 40f, 96f))
            };
            return true;
        }

        private IEnumerator BuildColliderGrassLookupRoutine()
        {
            Collider[] colliders = FindObjectsByType<Collider>(FindObjectsInactive.Exclude);
            float frameStart = Time.realtimeSinceStartup;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col != null && !col.isTrigger)
                {
                    _colliderGrassLookup[col.GetInstanceID()] = IsGrassCollider(col);
                }

                if ((i & 31) == 31 && (Time.realtimeSinceStartup - frameStart) >= SetupFrameBudgetSeconds)
                {
                    yield return null;
                    frameStart = Time.realtimeSinceStartup;
                }
            }
        }

        private IEnumerator BuildHardSurfaceIndexRoutine()
        {
            MeshRenderer[] rends = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
            float frameStart = Time.realtimeSinceStartup;

            for (int i = 0; i < rends.Length; i++)
            {
                MeshRenderer r = rends[i];
                if (r != null && IsFlatHardSurfaceRenderer(r))
                {
                    Bounds b = r.bounds;
                    HardRect rect = new HardRect
                    {
                        MinX = b.min.x - HardRectPad, MinZ = b.min.z - HardRectPad,
                        MaxX = b.max.x + HardRectPad, MaxZ = b.max.z + HardRectPad,
                        TopY = b.max.y
                    };
                    int x0 = Mathf.FloorToInt(rect.MinX / HardBucketSize), x1 = Mathf.FloorToInt(rect.MaxX / HardBucketSize);
                    int z0 = Mathf.FloorToInt(rect.MinZ / HardBucketSize), z1 = Mathf.FloorToInt(rect.MaxZ / HardBucketSize);
                    for (int bx = x0; bx <= x1; bx++)
                    {
                        for (int bz = z0; bz <= z1; bz++)
                        {
                            long key = ((long)bx << 32) ^ (uint)bz;
                            if (!_hardRects.TryGetValue(key, out List<HardRect> list))
                            {
                                list = new List<HardRect>(4);
                                _hardRects.Add(key, list);
                            }
                            list.Add(rect);
                        }
                    }
                }

                if ((i & 31) == 31 && (Time.realtimeSinceStartup - frameStart) >= SetupFrameBudgetSeconds)
                {
                    yield return null;
                    frameStart = Time.realtimeSinceStartup;
                }
            }
        }

        private static bool IsFlatHardSurfaceRenderer(MeshRenderer r)
        {
            Bounds b = r.bounds;
            if (b.size.y > 1.2f || b.size.x * b.size.z < 2f) return false;
            if (IsHardSurfaceName(r.gameObject.name)) return true;
            Transform parent = r.transform.parent;
            if (parent != null && IsHardSurfaceName(parent.name)) return true;
            Material[] mats = r.sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] != null && IsHardSurfaceName(mats[m].name)) return true;
            }
            return false;
        }

        private static bool IsHardSurfaceName(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return ContainsIgnoreCase(text, "asphalt") || ContainsIgnoreCase(text, "road") ||
                   ContainsIgnoreCase(text, "street") || ContainsIgnoreCase(text, "cadde") ||
                   ContainsIgnoreCase(text, "sidewalk") || ContainsIgnoreCase(text, "kaldirim") ||
                   ContainsIgnoreCase(text, "curb") || ContainsIgnoreCase(text, "parke") ||
                   ContainsIgnoreCase(text, "parking") || ContainsIgnoreCase(text, "paving") ||
                   ContainsIgnoreCase(text, "pavement") || ContainsIgnoreCase(text, "cobble") ||
                   ContainsIgnoreCase(text, "plaza") || ContainsIgnoreCase(text, "travertine") ||
                   ContainsIgnoreCase(text, "concrete") || ContainsIgnoreCase(text, "lane") ||
                   ContainsIgnoreCase(text, "marking") || ContainsIgnoreCase(text, "crosswalk") ||
                   ContainsIgnoreCase(text, "zebra") || ContainsIgnoreCase(text, "boulevard") ||
                   ContainsIgnoreCase(text, "water") || ContainsIgnoreCase(text, "lake");
        }

        private bool IsOnHardSurface(float x, float y, float z)
        {
            long key = ((long)Mathf.FloorToInt(x / HardBucketSize) << 32) ^ (uint)Mathf.FloorToInt(z / HardBucketSize);
            if (!_hardRects.TryGetValue(key, out List<HardRect> list)) return false;
            for (int i = 0; i < list.Count; i++)
            {
                HardRect r = list[i];
                if (x >= r.MinX && x <= r.MaxX && z >= r.MinZ && z <= r.MaxZ &&
                    y >= r.TopY - 1.0f && y <= r.TopY + 0.6f)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsGrassCollider(Collider col)
        {
            string goName = col.gameObject.name;
            if (IsExcludedHardSurface(goName)) return false;

            bool matched = MatchesGrassKeyword(goName);

            Renderer rend = col.GetComponent<Renderer>();
            if (rend == null && col.transform.parent != null)
            {
                rend = col.transform.parent.GetComponent<Renderer>();
            }

            if (rend != null)
            {
                Material[] mats = rend.sharedMaterials;
                if (mats != null)
                {
                    for (int m = 0; m < mats.Length; m++)
                    {
                        Material mat = mats[m];
                        if (mat == null) continue;
                        string mn = mat.name;
                        if (IsExcludedHardSurface(mn)) return false;
                        if (MatchesGrassKeyword(mn)) matched = true;
                    }
                }
            }

            return matched;
        }

        private static bool MatchesGrassKeyword(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (ContainsIgnoreCase(text, "grass") ||
                ContainsIgnoreCase(text, "meadow") ||
                ContainsIgnoreCase(text, "lawn") ||
                ContainsIgnoreCase(text, "pasture"))
            {
                return true;
            }

            // Match "park" while avoiding Turkish "parke" (cobblestone), "parking", "atlaspark", "joypark"
            int idx = text.IndexOf("park", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                if (ContainsIgnoreCase(text, "parke") ||
                    ContainsIgnoreCase(text, "parking") ||
                    ContainsIgnoreCase(text, "atlaspark") ||
                    ContainsIgnoreCase(text, "joypark"))
                {
                    return false;
                }
                return true;
            }

            return false;
        }

        private static bool IsExcludedHardSurface(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return ContainsIgnoreCase(text, "asphalt") ||
                   ContainsIgnoreCase(text, "sidewalk") ||
                   ContainsIgnoreCase(text, "road") ||
                   ContainsIgnoreCase(text, "curb") ||
                   ContainsIgnoreCase(text, "parke") ||
                   ContainsIgnoreCase(text, "parking") ||
                   ContainsIgnoreCase(text, "paving") ||
                   ContainsIgnoreCase(text, "travertine") ||
                   ContainsIgnoreCase(text, "concrete") ||
                   ContainsIgnoreCase(text, "roof") ||
                   ContainsIgnoreCase(text, "wall") ||
                   ContainsIgnoreCase(text, "building") ||
                   ContainsIgnoreCase(text, "water") ||
                   ContainsIgnoreCase(text, "lake");
        }

        private void Update()
        {
            if (!_ready || !_isActiveTier || _grassMesh == null || _grassMat == null) return;
            if (OurWorld.UI.MainMenuController.IsMenuActive) return;

            PlayerModeController player = PlayerModeController.Instance;
            if (player == null) return;

            if (_cachedCam == null) _cachedCam = Camera.main;
            Vector3 playerPos = player.ActiveWorldPosition;
            Vector3 camPos = _cachedCam != null ? _cachedCam.transform.position : playerPos;

            UpdateCellsAroundPlayer(playerPos);
            RenderVisibleGrass(playerPos, camPos);
        }

        private void UpdateCellsAroundPlayer(Vector3 playerPos)
        {
            int centerCx = Mathf.FloorToInt(playerPos.x * InvCellSize);
            int centerCz = Mathf.FloorToInt(playerPos.z * InvCellSize);

            if (centerCx != _lastCenterCx || centerCz != _lastCenterCz)
            {
                _lastCenterCx = centerCx;
                _lastCenterCz = centerCz;
                _scanCursor = 0;
                EvictOutOfRangeCells(playerPos);
            }

            // Probe up to MaxNewCellsPerFrame newly entered cells (zero GC)
            int span = CellRadius * 2 + 1;
            int probed = 0;
            while (_scanCursor < _scanTotal && probed < MaxNewCellsPerFrame)
            {
                int idx = _scanCursor++;
                int dz = (idx / span) - CellRadius;
                int dx = (idx % span) - CellRadius;

                int cx = centerCx + dx;
                int cz = centerCz + dz;
                float worldX = (cx + 0.5f) * CellSize;
                float worldZ = (cz + 0.5f) * CellSize;

                float offX = worldX - playerPos.x;
                float offZ = worldZ - playerPos.z;
                if (offX * offX + offZ * offZ > ActiveRadiusSq)
                {
                    continue;
                }

                long key = PackCellKey(cx, cz);
                if (_cellMap.ContainsKey(key))
                {
                    continue;
                }

                ProbeAndCacheCell(key, cx, cz, worldX, worldZ, playerPos.y);
                probed++;
            }
        }

        private void EvictOutOfRangeCells(Vector3 playerPos)
        {
            for (int i = _activeCells.Count - 1; i >= 0; i--)
            {
                CellData cell = _activeCells[i];
                float dx = cell.CenterX - playerPos.x;
                float dz = cell.CenterZ - playerPos.z;
                if (dx * dx + dz * dz > EvictRadiusSq)
                {
                    _cellMap.Remove(cell.Key);
                    int last = _activeCells.Count - 1;
                    _activeCells[i] = _activeCells[last];
                    _activeCells.RemoveAt(last);
                    cell.Count = 0;
                    _cellPool.Push(cell);
                }
            }
        }

        private void ReleaseAllCachedCells()
        {
            for (int i = 0; i < _activeCells.Count; i++)
            {
                CellData cell = _activeCells[i];
                cell.Count = 0;
                _cellPool.Push(cell);
            }
            _activeCells.Clear();
            _cellMap.Clear();
            _lastCenterCx = int.MinValue;
            _lastCenterCz = int.MinValue;
            _scanCursor = 0;
        }

        private void ProbeAndCacheCell(long key, int cx, int cz, float centerX, float centerZ, float refY)
        {
            CellData cell = _cellPool.Count > 0 ? _cellPool.Pop() : new CellData();
            cell.Key = key;
            cell.CenterX = centerX;
            cell.CenterZ = centerZ;
            cell.Count = 0;

            Vector3 rayOrigin = new Vector3(centerX, refY + 22f, centerZ);
            int hits = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, _hitBuffer, 48f, ~0, QueryTriggerInteraction.Ignore);

            if (hits > 0)
            {
                // Choose the lowest/most grounded upward-facing hit near ground level, avoiding roofs/vehicles
                int bestIdx = -1;
                float bestY = float.NegativeInfinity;
                for (int i = 0; i < hits; i++)
                {
                    RaycastHit h = _hitBuffer[i];
                    if (h.normal.y < 0.65f) continue;
                    // Ignore tall roofs well above player feet
                    if (h.point.y > refY + 3.2f) continue;
                    if (h.point.y > bestY)
                    {
                        bestY = h.point.y;
                        bestIdx = i;
                    }
                }

                if (bestIdx >= 0)
                {
                    RaycastHit groundHit = _hitBuffer[bestIdx];
                    Collider hitCol = groundHit.collider;
                    if (hitCol != null)
                    {
                        bool isGrass;
                        if (!_colliderGrassLookup.TryGetValue(hitCol.GetInstanceID(), out isGrass))
                        {
                            string hitName = hitCol.gameObject.name;
                            isGrass = !IsExcludedHardSurface(hitName) && MatchesGrassKeyword(hitName);
                            _colliderGrassLookup[hitCol.GetInstanceID()] = isGrass;
                        }

                        if (isGrass)
                        {
                            PopulateCellMatrices(cell, cx, cz, groundHit.point, groundHit.normal);
                        }
                    }
                }
            }

            _cellMap.Add(key, cell);
            _activeCells.Add(cell);
        }

        private void PopulateCellMatrices(CellData cell, int cx, int cz, Vector3 hitPoint, Vector3 hitNormal)
        {
            uint seed = HashCell(cx, cz);
            int count = 6 + (int)(seed % 5u); // 6 to 10 grass clumps per cell
            int written = 0;
            float invNy = hitNormal.y > 0.1f ? (1.0f / hitNormal.y) : 1.0f;
            Quaternion slopeTilt = Quaternion.FromToRotation(Vector3.up, hitNormal);

            for (int i = 0; i < count; i++)
            {
                seed = NextHash(seed);
                float jx = ((seed & 0xFFFFu) / 65535f * 2f - 1f) * 1.38f;
                float jz = (((seed >> 16) & 0xFFFFu) / 65535f * 2f - 1f) * 1.38f;

                seed = NextHash(seed);
                float yaw = ((seed & 0xFFFFu) / 65535f) * 360f;
                float scaleXZ = 0.80f + (((seed >> 16) & 0xFFu) / 255f) * 0.50f;  // 0.80 .. 1.30
                float scaleY = 0.80f + (((seed >> 24) & 0xFFu) / 255f) * 0.50f;   // 0.80 .. 1.30

                // Project clump onto local ground plane for seamless footing on gentle slopes
                float dy = -(jx * hitNormal.x + jz * hitNormal.z) * invNy;
                Vector3 pos = new Vector3(cell.CenterX + jx, hitPoint.y + dy - 0.02f, cell.CenterZ + jz);
                Quaternion rot = slopeTilt * Quaternion.Euler(0f, yaw, 0f);
                Vector3 scl = new Vector3(scaleXZ, scaleY, scaleXZ);

                if (IsOnHardSurface(pos.x, pos.y, pos.z)) continue;
                cell.Matrices[written++] = Matrix4x4.TRS(pos, rot, scl);
            }

            cell.Count = written;
        }

        private void RenderVisibleGrass(Vector3 playerPos, Vector3 camPos)
        {
            int drawCount = 0;
            int cellCount = _activeCells.Count;

            for (int i = 0; i < cellCount; i++)
            {
                CellData cell = _activeCells[i];
                int c = cell.Count;
                if (c == 0) continue;

                float dx = cell.CenterX - camPos.x;
                float dz = cell.CenterZ - camPos.z;
                float distSq = dx * dx + dz * dz;
                if (distSq > RenderCullRadiusSq) continue;

                // Thin grass density by 50% beyond 28 m from camera
                int step = distSq > ThinDistanceSq ? 2 : 1;
                for (int j = 0; j < c; j += step)
                {
                    if (drawCount >= MaxTotalInstances)
                    {
                        break;
                    }
                    _drawMatrices[drawCount++] = cell.Matrices[j];
                }

                if (drawCount >= MaxTotalInstances)
                {
                    break;
                }
            }

            if (drawCount == 0) return;

            _renderParams.worldBounds = new Bounds(playerPos, new Vector3(96f, 40f, 96f));

            int offset = 0;
            while (offset < drawCount)
            {
                int batch = drawCount - offset;
                if (batch > MaxBatchSize) batch = MaxBatchSize;
                Graphics.RenderMeshInstanced(_renderParams, _grassMesh, 0, _drawMatrices, batch, offset);
                offset += batch;
            }
        }

        private static long PackCellKey(int cx, int cz)
        {
            return ((long)cx << 32) | (uint)cz;
        }

        private static uint HashCell(int cx, int cz)
        {
            unchecked
            {
                uint h = (uint)cx * 374761393u + (uint)cz * 668265263u + 918273645u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        private static uint NextHash(uint state)
        {
            unchecked
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
