using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Guarantees that every solid, visible world object blocks the player, NPCs and vehicles.
    /// Runs time-sliced sweeps after the runtime builders (and again later for lazily spawned content such as
    /// FacadeDetailDresser cells and RealisticTreeUpgrader trees) and adds a collider to every MeshRenderer that
    /// has none of its own and is not already covered by a parent collider:
    ///  - readable meshes (runtime-built geometry, combined cells) get an exact static MeshCollider,
    ///  - non-readable imported meshes get an oriented BoxCollider fitted to the mesh bounds,
    ///  - tree bark/trunk meshes get a trunk capsule on the tree root.
    /// Skipped on purpose: characters/vehicles/dynamic props (they own their physics), foliage, grass, decals,
    /// flat ground overlays, light/VFX meshes, water, doors/entrances (entry triggers), and big imported
    /// meshes whose volume already contains authored colliders (enterable shells with their own walls).
    /// </summary>
    public sealed class UniversalColliderCoverage : MonoBehaviour
    {
        public static UniversalColliderCoverage Instance { get; private set; }

        private const float FrameBudgetSeconds = 0.003f;
        private const float MinSolidSize = 0.25f;     // smaller than this: not an obstacle (bulbs, cups, wires)
        private const float FlatOverlayHeight = 0.12f; // thinner than this: ground overlay (roads, decals, markings)
        private const int MaxMeshColliderTriangles = 60000;
        private const float BigObjectSize = 6f;

        private static readonly float[] SweepDelays = { 0.25f, 3f, 12f };
        private static readonly Collider[] OverlapBuf = new Collider[16];

        private static readonly string[] SkipKeywords =
        {
            "grass", "leaf", "leaves", "canopy", "foliage", "crown",
            "decal", "shadow", "glow", "halo", "flare", "lightbeam", "light_beam", "lightcone", "light_cone", "bulb", "festoon",
            "water", "lake", "ocean", "sea_", "skybox", "cloud", "steam", "smoke", "fog", "mist", "raindrop", "splash", "puddle",
            "marking", "lane_", "_lane", "zebra", "crosswalk",
            "marker", "arrow", "indicator", "waypoint", "highlight", "outline", "beacon",
            "wire", "cable", "rope", "laundry", "flag", "curtain", "carpet", "rug", "cloth",
            "blade", "propeller", "rotor", "_fan", "fan_",
            "kapi", "gate", "entrance", "portal",
            "label", "_text", "text_", "hologram", "minimap", "icon"
        };

        // Never skipped by name even if a skip keyword also matches (e.g. "FlagPole", "GatePost").
        private static readonly string[] ForceSolidKeywords = { "pole", "post", "column", "pillar", "bollard", "direk" };

        private static readonly string[] TrunkKeywords = { "bark", "trunk", "govde" };
        private static readonly string[] InteriorKeywords = { "interior", "room", "kitchen", "bedroom", "living", "salon", "mutfak", "bathroom" };

        private readonly HashSet<int> _processed = new HashSet<int>();
        private int _meshColliders;
        private int _boxColliders;
        private int _trunkColliders;
        private int _skippedCovered;
        private readonly List<string> _skippedBigNames = new List<string>(24);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_UniversalColliderCoverage");
            go.AddComponent<UniversalColliderCoverage>();
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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private IEnumerator Start()
        {
            // Let IstanbulThemeOverhaul, district/mall/farm builders and WorldPhysicsSolidifier's first pass run first.
            for (int i = 0; i < 4; i++) yield return null;

            float elapsed = 0f;
            for (int s = 0; s < SweepDelays.Length; s++)
            {
                float wait = SweepDelays[s] - elapsed;
                if (wait > 0f) yield return new WaitForSeconds(wait);
                elapsed = SweepDelays[s];
                yield return SweepRoutine();
            }
        }

        /// <summary>Call after spawning new world content at runtime to make it solid immediately.</summary>
        public void CoverHierarchy(Transform root)
        {
            if (root == null) return;
            MeshRenderer[] rends = root.GetComponentsInChildren<MeshRenderer>(false);
            for (int i = 0; i < rends.Length; i++) TryCover(rends[i]);
        }

        private IEnumerator SweepRoutine()
        {
            int before = _meshColliders + _boxColliders + _trunkColliders;
            MeshRenderer[] rends = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
            float frameStart = Time.realtimeSinceStartup;

            for (int i = 0; i < rends.Length; i++)
            {
                TryCover(rends[i]);
                if (Time.realtimeSinceStartup - frameStart >= FrameBudgetSeconds)
                {
                    yield return null;
                    frameStart = Time.realtimeSinceStartup;
                }
            }

            int added = _meshColliders + _boxColliders + _trunkColliders - before;
            if (added > 0)
            {
                Debug.Log($"[UniversalColliderCoverage] +{added} collider (toplam mesh={_meshColliders}, box={_boxColliders}, gövde={_trunkColliders}, zaten kapalı={_skippedCovered}).");
            }
            if (_skippedBigNames.Count > 0)
            {
                Debug.Log("[UniversalColliderCoverage] İçinde collider olduğu için atlanan büyük nesneler: " + string.Join(", ", _skippedBigNames));
                _skippedBigNames.Clear();
            }
        }

        private void TryCover(MeshRenderer r)
        {
            if (r == null) return;
            GameObject go = r.gameObject;
            int id = go.GetInstanceID();
            if (_processed.Contains(id)) return;

            MeshFilter mf = go.GetComponent<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null) { _processed.Add(id); return; }

            string n = go.name;

            // Tree trunks: capsule on the tree root (works for every LOD and even when LOD0 is disabled on Low tier).
            if (ContainsAny(n, TrunkKeywords))
            {
                _processed.Add(id);
                CoverTrunk(r);
                return;
            }

            if (!r.enabled) return; // may become enabled later; re-checked on the next sweep
            _processed.Add(id);

            if (go.layer == 5) return; // UI
            if (go.GetComponent<Collider>() != null) return;
            if (ShouldSkipByName(r, n)) return;

            Bounds wb = r.bounds;
            Vector3 size = wb.size;
            float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (maxDim < MinSolidSize) return;
            if (size.y < FlatOverlayHeight) return;

            // Things that move or own their physics: characters, vehicles, dynamic/carryable props, animals.
            if (go.GetComponentInParent<Rigidbody>() != null ||
                go.GetComponentInParent<CharacterController>() != null ||
                go.GetComponentInParent<Animator>() != null ||
                go.GetComponentInParent<DynamicWorldProp>() != null ||
                go.GetComponentInParent<CarryableProp>() != null)
            {
                return;
            }

            if (IsCoveredByParentCollider(go.transform, wb))
            {
                _skippedCovered++;
                return;
            }

            if (mesh.isReadable && mesh.vertexCount > 2)
            {
                int tris = EstimateTriangles(mesh);
                if (tris <= MaxMeshColliderTriangles)
                {
                    MeshCollider mc = go.AddComponent<MeshCollider>();
                    mc.convex = false;
                    mc.sharedMesh = mesh;
                    _meshColliders++;
                    return;
                }
            }

            // Non-readable imported mesh -> oriented box. Guard big shells/room sets that already have authored colliders.
            if (maxDim >= BigObjectSize || IsLargeInteriorSet(go.transform, size))
            {
                if (HasAuthoredCollidersInside(wb, go.transform))
                {
                    if (_skippedBigNames.Count < 20) _skippedBigNames.Add(n);
                    return;
                }
                if (IsLargeInteriorSet(go.transform, size))
                {
                    // A whole furnished room as one mesh: a box would fill the room and trap the player.
                    if (_skippedBigNames.Count < 20) _skippedBigNames.Add(n + " (oda seti)");
                    return;
                }
            }

            Bounds lb = mesh.bounds;
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.center = lb.center;
            bc.size = new Vector3(Mathf.Max(lb.size.x, 0.05f), Mathf.Max(lb.size.y, 0.05f), Mathf.Max(lb.size.z, 0.05f));
            _boxColliders++;
        }

        private void CoverTrunk(MeshRenderer r)
        {
            Transform host = r.transform;
            LODGroup lod = r.GetComponentInParent<LODGroup>();
            if (lod != null) host = lod.transform;
            else if (host.parent != null) host = host.parent;

            // The tree (or its upgraded root) already blocks: keep the authored collider.
            Transform t = host;
            for (int depth = 0; depth < 3 && t != null; depth++, t = t.parent)
            {
                Collider existing = t.GetComponent<Collider>();
                if (existing != null && !existing.isTrigger) return;
            }
            if (host.GetComponentInParent<Rigidbody>() != null) return;

            Bounds wb = r.bounds;
            float height = Mathf.Clamp(wb.size.y * 0.45f, 1.6f, 3.4f);
            float radius = Mathf.Clamp(Mathf.Min(wb.size.x, wb.size.z) * 0.06f, 0.18f, 0.55f);

            CapsuleCollider cc = host.gameObject.AddComponent<CapsuleCollider>();
            Vector3 lossy = host.lossyScale;
            float sy = Mathf.Max(0.01f, Mathf.Abs(lossy.y));
            float sxz = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z)));
            Vector3 baseWorld = new Vector3(host.position.x, wb.min.y, host.position.z);
            Vector3 centerLocal = host.InverseTransformPoint(baseWorld + Vector3.up * (height * 0.5f));
            cc.direction = 1;
            cc.center = centerLocal;
            cc.height = height / sy;
            cc.radius = radius / sxz;
            _trunkColliders++;
        }

        private static bool ShouldSkipByName(MeshRenderer r, string goName)
        {
            if (ContainsAny(goName, ForceSolidKeywords)) return false;
            if (IsDoorName(goName)) return true;
            // Tree LOD2 billboards are foliage; advertising billboards stay solid.
            if (goName.IndexOf("billboard", StringComparison.OrdinalIgnoreCase) >= 0 &&
                goName.IndexOf("lod", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (ContainsAny(goName, SkipKeywords)) return true;
            Transform p = r.transform.parent;
            if (p != null && (ContainsAny(p.name, SkipKeywords) || IsDoorName(p.name)) &&
                !ContainsAny(p.name, ForceSolidKeywords)) return true;
            Material m = r.sharedMaterial;
            if (m != null)
            {
                string mn = m.name;
                if (ContainsAny(mn, SkipKeywords)) return true;
                string sh = m.shader != null ? m.shader.name : string.Empty;
                if (sh.IndexOf("Particle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (sh.IndexOf("Unlit", StringComparison.OrdinalIgnoreCase) >= 0 && m.renderQueue >= 3000))
                {
                    return true; // transparent unlit VFX
                }
            }
            if (goName.StartsWith("StreetDecals_Cell", StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsDoorName(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (text.IndexOf("door", StringComparison.OrdinalIgnoreCase) < 0) return false;
            return text.IndexOf("indoor", StringComparison.OrdinalIgnoreCase) < 0 &&
                   text.IndexOf("outdoor", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsCoveredByParentCollider(Transform t, Bounds wb)
        {
            Transform p = t.parent;
            for (int depth = 0; depth < 4 && p != null; depth++, p = p.parent)
            {
                Collider[] cols = p.GetComponents<Collider>();
                for (int i = 0; i < cols.Length; i++)
                {
                    Collider c = cols[i];
                    if (c == null || c.isTrigger || !c.enabled) continue;
                    if (c.bounds.Contains(wb.center)) return true;
                }
            }
            return false;
        }

        private static bool IsLargeInteriorSet(Transform t, Vector3 size)
        {
            if (size.x < 2.5f || size.z < 2.5f) return false;
            Transform p = t;
            for (int depth = 0; depth < 5 && p != null; depth++, p = p.parent)
            {
                if (ContainsAny(p.name, InteriorKeywords)) return true;
            }
            return false;
        }

        private static bool HasAuthoredCollidersInside(Bounds wb, Transform self)
        {
            // Ignore the bottom 0.4 m so the ground collider under every building does not count.
            Vector3 c = wb.center;
            Vector3 e = wb.extents;
            float bottom = wb.min.y + 0.4f;
            if (bottom >= wb.max.y) return false;
            Vector3 center = new Vector3(c.x, (bottom + wb.max.y) * 0.5f, c.z);
            Vector3 half = new Vector3(e.x * 0.8f, (wb.max.y - bottom) * 0.5f, e.z * 0.8f);

            int count = Physics.OverlapBoxNonAlloc(center, half, OverlapBuf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = OverlapBuf[i];
                if (col == null || col.attachedRigidbody != null) continue;
                if (col.transform == self || col.transform.IsChildOf(self)) continue;
                // A ground/terrain slab much wider than the object is not an authored wall.
                Vector3 cs = col.bounds.size;
                if (cs.y < 0.6f && (cs.x > wb.size.x * 1.5f || cs.z > wb.size.z * 1.5f)) continue;
                return true;
            }
            return false;
        }

        private static int EstimateTriangles(Mesh mesh)
        {
            long total = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) total += mesh.GetIndexCount(s) / 3;
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        private static bool ContainsAny(string text, string[] keywords)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < keywords.Length; i++)
            {
                if (text.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}
