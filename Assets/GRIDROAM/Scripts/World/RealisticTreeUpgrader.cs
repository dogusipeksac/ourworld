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
    /// Upgrades scene tree instances (OW_PlaneTree, OW_CypressTree, OW_AydosPineTree, and baked
    /// Tree_PlaneTree / BlenderPlaneTreeMesh roots) to alpha-card 3-LOD realistic Istanbul trees
    /// (TR_Cinar, TR_Selvi, TR_FistikCami) using shared URP Lit materials, zero per-instance
    /// material copies, frame-budgeted coroutine instantiation, WorldRealismAtmosphere canopy sway,
    /// and Low/Medium GraphicsProfileManager tier adaptation.
    /// </summary>
    public sealed class RealisticTreeUpgrader : MonoBehaviour
    {
        public static RealisticTreeUpgrader Instance { get; private set; }

        private const string TreeResourceRoot = "OurWorldTrees/";
        private const string ThemeTextureRoot = "OurWorldTheme/Textures/";

        private const float Lod0ScreenHeight = 0.35f;
        private const float Lod1ScreenHeight = 0.10f;
        private const float Lod2ScreenHeight = 0.015f;
        private const float FrameBudgetSeconds = 0.0018f; // <= 2 ms per frame target

        private enum TreeSpecies
        {
            Cinar = 0,
            Selvi = 1,
            FistikCami = 2
        }

        private sealed class UpgradedTreeRecord
        {
            public LODGroup LodGroup;
            public MeshRenderer[] Lod0Bark;
            public MeshRenderer[] Lod0Canopy;
            public MeshRenderer[] Lod1Bark;
            public MeshRenderer[] Lod1Canopy;
            public MeshRenderer[] Lod2Billboard;
            public LOD[] OriginalLods;
            public bool Lod0SwappedForFallback;
        }

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int CullId = Shader.PropertyToID("_Cull");

        private readonly GameObject[] _prefabs = new GameObject[3];
        private readonly Material[] _billboardMats = new Material[3];
        private readonly List<UpgradedTreeRecord> _upgradedTrees = new List<UpgradedTreeRecord>(128);

        private Material _barkMat;
        private Material _leavesMat;
        private bool _subscribedTier;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;

            GameObject go = new GameObject("OurWorld_RealisticTreeUpgrader");
            go.AddComponent<RealisticTreeUpgrader>();
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
        }

        private IEnumerator Start()
        {
            // Wait 3 frames so IstanbulThemeOverhaul, SultanbeyliDistrictsBuilder, ShootingRangeArena,
            // UrpRealismLightingOverhaul, and WorldRealismAtmosphere finish their initial setup.
            yield return null;
            yield return null;
            yield return null;

            if (!LoadPrefabsAndBuildSharedMaterials())
            {
                Debug.LogWarning("[RealisticTreeUpgrader] Tree prefabs under Resources/OurWorldTrees/ not ready; skipping upgrade.");
                yield break;
            }

            if (GraphicsProfileManager.Instance != null && !_subscribedTier)
            {
                GraphicsProfileManager.Instance.OnTierChanged += HandleTierChanged;
                _subscribedTier = true;
            }

            List<Transform> roots = DiscoverExistingTreeRoots();
            if (roots.Count == 0)
            {
                yield break;
            }

            bool isLow = ActiveTier == GraphicsTier.Low;
            float frameStart = Time.realtimeSinceStartup;
            int processedThisFrame = 0;

            for (int i = 0; i < roots.Count; i++)
            {
                Transform root = roots[i];
                if (root != null)
                {
                    UpgradeSingleTreeRoot(root, isLow);
                }

                processedThisFrame++;
                if (processedThisFrame >= 4 && (Time.realtimeSinceStartup - frameStart) >= FrameBudgetSeconds)
                {
                    yield return null;
                    frameStart = Time.realtimeSinceStartup;
                    processedThisFrame = 0;
                    isLow = ActiveTier == GraphicsTier.Low;
                }
            }
        }

        private void OnDestroy()
        {
            if (_subscribedTier && GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged -= HandleTierChanged;
                _subscribedTier = false;
            }

            if (_barkMat != null) Destroy(_barkMat);
            if (_leavesMat != null) Destroy(_leavesMat);
            for (int i = 0; i < _billboardMats.Length; i++)
            {
                if (_billboardMats[i] != null) Destroy(_billboardMats[i]);
            }

            if (Instance == this) Instance = null;
        }

        private void HandleTierChanged(GraphicsTier tier)
        {
            bool isLow = tier == GraphicsTier.Low;
            for (int i = 0; i < _upgradedTrees.Count; i++)
            {
                ApplyTierToRecord(_upgradedTrees[i], isLow);
            }
        }

        // =====================================================================
        // 1. PREFABS & SHARED MATERIALS (BUILT ONCE, ZERO PER-TREE COPIES)
        // =====================================================================
        private bool LoadPrefabsAndBuildSharedMaterials()
        {
            _prefabs[(int)TreeSpecies.Cinar] = Resources.Load<GameObject>(TreeResourceRoot + "TR_Cinar");
            _prefabs[(int)TreeSpecies.Selvi] = Resources.Load<GameObject>(TreeResourceRoot + "TR_Selvi");
            _prefabs[(int)TreeSpecies.FistikCami] = Resources.Load<GameObject>(TreeResourceRoot + "TR_FistikCami");

            if (_prefabs[0] == null && _prefabs[1] == null && _prefabs[2] == null)
            {
                return false;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (urpLit == null)
            {
                return false;
            }

            // Shared Bark Material
            _barkMat = new Material(urpLit) { name = "MAT_TR_SharedBark", enableInstancing = true };
            Texture2D barkAlb = Resources.Load<Texture2D>(ThemeTextureRoot + "bark_tree_albedo");
            Texture2D barkNor = Resources.Load<Texture2D>(ThemeTextureRoot + "bark_tree_normal");
            ApplyTexture(_barkMat, barkAlb);
            ApplyNormalMap(_barkMat, barkNor, 1.10f);
            SetColorAndPbr(_barkMat, Color.white, smoothness: 0.14f, metallic: 0.0f);

            // Shared Leaves Material (Alpha Clip, Double-Sided, Normal Mapped)
            _leavesMat = CreateAlphaClipMaterial(
                urpLit,
                "MAT_TR_SharedLeaves",
                Resources.Load<Texture2D>(TreeResourceRoot + "TR_LeafAtlas_Albedo"),
                Resources.Load<Texture2D>(TreeResourceRoot + "TR_LeafAtlas_Normal"),
                smoothness: 0.18f,
                bumpScale: 0.75f);

            // Shared Billboard Materials (one per species, reused by every LOD2 of that species)
            _billboardMats[(int)TreeSpecies.Cinar] = CreateAlphaClipMaterial(
                urpLit,
                "MAT_TR_Billboard_Cinar",
                Resources.Load<Texture2D>(TreeResourceRoot + "TR_Cinar_Billboard"),
                null,
                smoothness: 0.12f,
                bumpScale: 0f);

            _billboardMats[(int)TreeSpecies.Selvi] = CreateAlphaClipMaterial(
                urpLit,
                "MAT_TR_Billboard_Selvi",
                Resources.Load<Texture2D>(TreeResourceRoot + "TR_Selvi_Billboard"),
                null,
                smoothness: 0.12f,
                bumpScale: 0f);

            _billboardMats[(int)TreeSpecies.FistikCami] = CreateAlphaClipMaterial(
                urpLit,
                "MAT_TR_Billboard_FistikCami",
                Resources.Load<Texture2D>(TreeResourceRoot + "TR_FistikCami_Billboard"),
                null,
                smoothness: 0.12f,
                bumpScale: 0f);

            return true;
        }

        private static Material CreateAlphaClipMaterial(
            Shader shader,
            string matName,
            Texture2D albedo,
            Texture2D normal,
            float smoothness,
            float bumpScale)
        {
            Material mat = new Material(shader)
            {
                name = matName,
                enableInstancing = true
            };

            ApplyTexture(mat, albedo);
            if (normal != null)
            {
                ApplyNormalMap(mat, normal, bumpScale);
            }
            SetColorAndPbr(mat, Color.white, smoothness: smoothness, metallic: 0.0f);

            if (mat.HasProperty(SurfaceId)) mat.SetFloat(SurfaceId, 0f);
            if (mat.HasProperty(AlphaClipId)) mat.SetFloat(AlphaClipId, 1f);
            if (mat.HasProperty(CutoffId)) mat.SetFloat(CutoffId, 0.5f);
            if (mat.HasProperty(CullId)) mat.SetFloat(CullId, (float)CullMode.Off);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            return mat;
        }

        private static void ApplyTexture(Material mat, Texture2D tex)
        {
            if (tex == null) return;
            if (mat.HasProperty(BaseMapId)) mat.SetTexture(BaseMapId, tex);
            if (mat.HasProperty(MainTexId)) mat.SetTexture(MainTexId, tex);
        }

        private static void ApplyNormalMap(Material mat, Texture2D normalTex, float bumpScale)
        {
            if (normalTex == null || !mat.HasProperty(BumpMapId)) return;
            mat.SetTexture(BumpMapId, normalTex);
            if (mat.HasProperty(BumpScaleId)) mat.SetFloat(BumpScaleId, bumpScale);
            mat.EnableKeyword("_NORMALMAP");
        }

        private static void SetColorAndPbr(Material mat, Color color, float smoothness, float metallic)
        {
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
            if (mat.HasProperty(SmoothnessId)) mat.SetFloat(SmoothnessId, smoothness);
            if (mat.HasProperty(MetallicId)) mat.SetFloat(MetallicId, metallic);
        }

        // =====================================================================
        // 2. SCENE TREE ROOT DISCOVERY (SINGLE PASS, DEDUPLICATED)
        // =====================================================================
        private static List<Transform> DiscoverExistingTreeRoots()
        {
            List<Transform> results = new List<Transform>(128);
            HashSet<Transform> unique = new HashSet<Transform>();

            Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);

            // Pass 1: Direct OW_PlaneTree / OW_CypressTree / OW_AydosPineTree roots & BlenderPlaneTreeMesh parents
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform tr = allTransforms[i];
                if (tr == null) continue;
                string n = tr.name;

                if (n.StartsWith("OW_PlaneTree", StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("OW_CypressTree", StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("OW_AydosPineTree", StringComparison.OrdinalIgnoreCase))
                {
                    AddRootIfNotNested(tr, unique, results);
                }
                else if (string.Equals(n, "BlenderPlaneTreeMesh", StringComparison.OrdinalIgnoreCase))
                {
                    Transform treeRoot = tr.parent != null ? tr.parent : tr;
                    AddRootIfNotNested(treeRoot, unique, results);
                }
            }

            // Pass 2: Catch remaining baked Tree_PlaneTree / PlaneTree meshes (mirrors UrpRealismLightingOverhaul)
            MeshRenderer[] rends = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < rends.Length; i++)
            {
                MeshRenderer mr = rends[i];
                if (mr == null) continue;
                string n = mr.gameObject.name;
                if (n.StartsWith("TR_", StringComparison.Ordinal)) continue;
                if (n.IndexOf("Tree_PlaneTree", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("PlaneTree", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                Transform candidate = ResolveBakedTreeRoot(mr.transform);
                AddRootIfNotNested(candidate, unique, results);
            }

            return results;
        }

        private static Transform ResolveBakedTreeRoot(Transform leafMesh)
        {
            Transform cur = leafMesh;
            for (int depth = 0; depth < 3 && cur.parent != null; depth++)
            {
                Transform p = cur.parent;
                if (p.GetComponent<Collider>() != null ||
                    string.Equals(cur.name, "BlenderPlaneTreeMesh", StringComparison.OrdinalIgnoreCase) ||
                    p.name.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cur = p;
                    if (cur.GetComponent<Collider>() != null)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
            return cur;
        }

        private static void AddRootIfNotNested(Transform candidate, HashSet<Transform> unique, List<Transform> list)
        {
            if (candidate == null || unique.Contains(candidate)) return;

            // Skip if an ancestor is already registered
            Transform anc = candidate.parent;
            while (anc != null)
            {
                if (unique.Contains(anc)) return;
                anc = anc.parent;
            }

            // Skip if already upgraded previously
            for (int c = 0; c < candidate.childCount; c++)
            {
                string cn = candidate.GetChild(c).name;
                if (cn.StartsWith("TR_Cinar", StringComparison.Ordinal) ||
                    cn.StartsWith("TR_Selvi", StringComparison.Ordinal) ||
                    cn.StartsWith("TR_FistikCami", StringComparison.Ordinal))
                {
                    return;
                }
            }

            unique.Add(candidate);
            list.Add(candidate);
        }

        // =====================================================================
        // 3. SINGLE TREE UPGRADE (DISABLE OLD MESHES, KEEP COLLIDERS, INSTANTIATE)
        // =====================================================================
        private void UpgradeSingleTreeRoot(Transform root, bool isLow)
        {
            TreeSpecies species = DetermineSpecies(root);
            GameObject prefab = _prefabs[(int)species] ?? _prefabs[(int)TreeSpecies.Cinar];
            if (prefab == null) return;

            // Measure existing visible mesh bounds height before disabling old MeshRenderers
            MeshRenderer[] oldRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            bool hasBounds = false;
            Bounds combined = default;
            for (int i = 0; i < oldRenderers.Length; i++)
            {
                MeshRenderer mr = oldRenderers[i];
                if (mr == null || !mr.enabled) continue;
                if (!hasBounds)
                {
                    combined = mr.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(mr.bounds);
                }
            }

            // Disable old visual MeshRenderers while leaving ALL Colliders intact
            for (int i = 0; i < oldRenderers.Length; i++)
            {
                if (oldRenderers[i] != null)
                {
                    oldRenderers[i].enabled = false;
                }
            }

            float nominalHeight = GetNominalHeight(species);
            float baseScale = hasBounds && combined.size.y > 0.5f
                ? Mathf.Clamp(combined.size.y / nominalHeight, 0.80f, 1.25f)
                : 1.0f;

            // Deterministic per-position +-10 deg yaw and +-8% scale variation
            Vector3 worldPos = root.position;
            uint hash = HashPosition(worldPos);
            float yawOffsetDeg = (((hash & 0xFFFFu) / 65535f) * 2f - 1f) * 10f;
            float scaleJitter = 1f + ((((hash >> 16) & 0xFFFFu) / 65535f) * 2f - 1f) * 0.08f;
            float targetWorldScale = Mathf.Clamp(baseScale * scaleJitter, 0.80f, 1.25f);

            GameObject inst = Instantiate(prefab, root, false);
            inst.name = prefab.name;

            // Keep exact ground base position and strictly upright yaw + deterministic offset
            inst.transform.position = worldPos;
            inst.transform.rotation = Quaternion.Euler(0f, root.eulerAngles.y + yawOffsetDeg, 0f);

            // Compensate for root.lossyScale so final world height matches targetWorldScale accurately
            Vector3 ls = root.lossyScale;
            float sx = Mathf.Abs(ls.x) > 0.001f ? targetWorldScale / ls.x : targetWorldScale;
            float sy = Mathf.Abs(ls.y) > 0.001f ? targetWorldScale / ls.y : targetWorldScale;
            float sz = Mathf.Abs(ls.z) > 0.001f ? targetWorldScale / ls.z : targetWorldScale;
            inst.transform.localScale = new Vector3(sx, sy, sz);

            // Assign shared materials, classify LOD renderers, and register canopies for wind sway
            UpgradedTreeRecord record = ConfigureInstantiatedTree(inst, species);
            if (record != null)
            {
                ApplyTierToRecord(record, isLow);
                _upgradedTrees.Add(record);
            }
        }

        private UpgradedTreeRecord ConfigureInstantiatedTree(GameObject inst, TreeSpecies species)
        {
            MeshRenderer[] rends = inst.GetComponentsInChildren<MeshRenderer>(true);
            List<MeshRenderer> lod0Bark = new List<MeshRenderer>(1);
            List<MeshRenderer> lod0Canopy = new List<MeshRenderer>(1);
            List<MeshRenderer> lod1Bark = new List<MeshRenderer>(1);
            List<MeshRenderer> lod1Canopy = new List<MeshRenderer>(1);
            List<MeshRenderer> lod2Billboard = new List<MeshRenderer>(1);

            Material bbMat = _billboardMats[(int)species] ?? _billboardMats[0];
            WorldRealismAtmosphere atmo = WorldRealismAtmosphere.Instance;

            for (int i = 0; i < rends.Length; i++)
            {
                MeshRenderer mr = rends[i];
                if (mr == null) continue;
                string n = mr.gameObject.name;

                bool isBillboard = n.IndexOf("Billboard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   n.EndsWith("_LOD2", StringComparison.OrdinalIgnoreCase);
                bool isCanopy = !isBillboard &&
                                (n.IndexOf("Canopy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 n.IndexOf("Leaves", StringComparison.OrdinalIgnoreCase) >= 0);

                if (isBillboard)
                {
                    mr.sharedMaterial = bbMat;
                    mr.receiveShadows = true;
                    lod2Billboard.Add(mr);
                }
                else if (isCanopy)
                {
                    mr.sharedMaterial = _leavesMat;
                    mr.receiveShadows = true;
                    if (n.EndsWith("_LOD1", StringComparison.OrdinalIgnoreCase))
                    {
                        lod1Canopy.Add(mr);
                    }
                    else
                    {
                        lod0Canopy.Add(mr);
                    }

                    if (atmo != null)
                    {
                        atmo.RegisterSway(mr.transform);
                    }
                }
                else
                {
                    mr.sharedMaterial = _barkMat;
                    mr.receiveShadows = true;
                    if (n.EndsWith("_LOD1", StringComparison.OrdinalIgnoreCase))
                    {
                        lod1Bark.Add(mr);
                    }
                    else
                    {
                        lod0Bark.Add(mr);
                    }
                }
            }

            // Preserve auto-imported LODGroup; build one in code with matching screen ratios if absent
            LODGroup lodGroup = inst.GetComponent<LODGroup>();
            if (lodGroup == null && (lod0Bark.Count + lod0Canopy.Count) > 0)
            {
                lodGroup = inst.AddComponent<LODGroup>();
                LOD[] lods =
                {
                    new LOD(Lod0ScreenHeight, MergeRenderers(lod0Bark, lod0Canopy)),
                    new LOD(Lod1ScreenHeight, MergeRenderers(lod1Bark, lod1Canopy)),
                    new LOD(Lod2ScreenHeight, ToRendererArray(lod2Billboard))
                };
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
            }

            return new UpgradedTreeRecord
            {
                LodGroup = lodGroup,
                Lod0Bark = lod0Bark.ToArray(),
                Lod0Canopy = lod0Canopy.ToArray(),
                Lod1Bark = lod1Bark.ToArray(),
                Lod1Canopy = lod1Canopy.ToArray(),
                Lod2Billboard = lod2Billboard.ToArray(),
                OriginalLods = lodGroup != null ? lodGroup.GetLODs() : null,
                Lod0SwappedForFallback = false
            };
        }

        private static void ApplyTierToRecord(UpgradedTreeRecord rec, bool isLow)
        {
            if (rec == null) return;

            // Low tier: disable LOD0 renderers (no ForceLOD, no QualitySettings changes) & shadows on Bark only.
            // Note: GraphicsProfileManager sets maximumLODLevel=1 on Low tier, so keeping the 3-LOD array makes
            // Unity naturally pick LOD1 close up & LOD2 far. If maximumLODLevel happens to be 0 while isLow,
            // map slot 0 to LOD1 renderers so trees never turn invisible at close range.
            if (isLow)
            {
                SetRenderersEnabled(rec.Lod0Bark, false);
                SetRenderersEnabled(rec.Lod0Canopy, false);
                SetShadowMode(rec.Lod0Bark, ShadowCastingMode.On);
                SetShadowMode(rec.Lod0Canopy, ShadowCastingMode.Off);
                SetShadowMode(rec.Lod1Bark, ShadowCastingMode.On);
                SetShadowMode(rec.Lod1Canopy, ShadowCastingMode.Off);
                SetShadowMode(rec.Lod2Billboard, ShadowCastingMode.Off);

                if (rec.LodGroup != null && rec.OriginalLods != null && rec.OriginalLods.Length >= 3)
                {
                    if (QualitySettings.maximumLODLevel == 0 && !rec.Lod0SwappedForFallback)
                    {
                        LOD[] fallbackLods = (LOD[])rec.OriginalLods.Clone();
                        fallbackLods[0].renderers = fallbackLods[1].renderers;
                        rec.LodGroup.SetLODs(fallbackLods);
                        rec.Lod0SwappedForFallback = true;
                    }
                    else if (QualitySettings.maximumLODLevel >= 1 && rec.Lod0SwappedForFallback)
                    {
                        rec.LodGroup.SetLODs(rec.OriginalLods);
                        rec.Lod0SwappedForFallback = false;
                    }
                }
            }
            else
            {
                if (rec.LodGroup != null && rec.OriginalLods != null && rec.Lod0SwappedForFallback)
                {
                    rec.LodGroup.SetLODs(rec.OriginalLods);
                    rec.Lod0SwappedForFallback = false;
                }

                SetRenderersEnabled(rec.Lod0Bark, true);
                SetRenderersEnabled(rec.Lod0Canopy, true);
                SetShadowMode(rec.Lod0Bark, ShadowCastingMode.On);
                SetShadowMode(rec.Lod0Canopy, ShadowCastingMode.TwoSided);
                SetShadowMode(rec.Lod1Bark, ShadowCastingMode.On);
                SetShadowMode(rec.Lod1Canopy, ShadowCastingMode.TwoSided);
                SetShadowMode(rec.Lod2Billboard, ShadowCastingMode.Off);
            }
        }

        private static TreeSpecies DetermineSpecies(Transform root)
        {
            string n = root.name;
            string parentName = root.parent != null ? root.parent.name : string.Empty;

            if (ContainsIgnoreCase(n, "Cypress") || ContainsIgnoreCase(n, "Selvi") ||
                ContainsIgnoreCase(parentName, "Cypress") || ContainsIgnoreCase(parentName, "Selvi"))
            {
                return TreeSpecies.Selvi;
            }

            if (ContainsIgnoreCase(n, "Pine") || ContainsIgnoreCase(n, "Fistik") ||
                ContainsIgnoreCase(parentName, "Pine") || ContainsIgnoreCase(parentName, "Fistik"))
            {
                return TreeSpecies.FistikCami;
            }

            return TreeSpecies.Cinar;
        }

        private static float GetNominalHeight(TreeSpecies species)
        {
            switch (species)
            {
                case TreeSpecies.Selvi: return 10.0f;
                case TreeSpecies.FistikCami: return 9.0f;
                default: return 11.0f;
            }
        }

        private static uint HashPosition(Vector3 pos)
        {
            unchecked
            {
                uint x = (uint)Mathf.RoundToInt(pos.x * 10f);
                uint z = (uint)Mathf.RoundToInt(pos.z * 10f);
                uint h = x * 374761393u + z * 668265263u + 1442695041u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Renderer[] MergeRenderers(List<MeshRenderer> a, List<MeshRenderer> b)
        {
            Renderer[] arr = new Renderer[a.Count + b.Count];
            int idx = 0;
            for (int i = 0; i < a.Count; i++) arr[idx++] = a[i];
            for (int i = 0; i < b.Count; i++) arr[idx++] = b[i];
            return arr;
        }

        private static Renderer[] ToRendererArray(List<MeshRenderer> list)
        {
            Renderer[] arr = new Renderer[list.Count];
            for (int i = 0; i < list.Count; i++) arr[i] = list[i];
            return arr;
        }

        private static void SetRenderersEnabled(MeshRenderer[] list, bool enabled)
        {
            if (list == null) return;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null) list[i].enabled = enabled;
            }
        }

        private static void SetShadowMode(MeshRenderer[] list, ShadowCastingMode mode)
        {
            if (list == null) return;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null) list[i].shadowCastingMode = mode;
            }
        }
    }
}
