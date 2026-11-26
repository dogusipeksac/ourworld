using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Transforms the flat prototype surfaces into a tactile, warm, lived-in
    /// Istanbul · Sultanbeyli · Hamidiye Mahallesi visual theme at runtime (zero scene rebuild required):
    /// 1. Upgrades flat scene materials with commercial-safe Poly Haven CC0 PBR Albedo + Normal Maps
    ///    (grass meadow, warm Istanbul stucco plaster, kilit parke cobblestones, asphalt, sand, terracotta roof tiles, oak planks)
    ///    using a compact shared material cache to preserve SRP Batcher & iOS 60 FPS performance.
    /// 2. Populates iconic Istanbul streetscape hero props crafted in Blender 5.3.0 & Poly Haven CC0:
    ///    interactive Simit Carts, Turkish Tea Garden tables (sittable + hot tea!), carved Mahalle Cesmesi fountains,
    ///    multi-branch Cinar (Plane) & Selvi (Cypress) trees, travertine tulip planters, striped shop awnings,
    ///    park benches, and dusk-glowing catenary cafe festoon string lights.
    /// 3. Applies subtle, sunlit Mediterranean atmospheric horizon depth synced smoothly to DayNightCycle.
    /// </summary>
    public sealed class IstanbulThemeOverhaul : MonoBehaviour
    {
        public static IstanbulThemeOverhaul Instance { get; private set; }

        private const string TexRoot = "OurWorldTheme/Textures/";
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private enum SurfaceClass
        {
            None,
            Grass,
            Plaster,
            Cobblestone,
            Asphalt,
            Sand,
            RoofTiles,
            WoodPlanks
        }

        private struct PbrTexSet
        {
            public Texture2D Albedo;
            public Texture2D Normal;
        }

        private readonly Dictionary<SurfaceClass, PbrTexSet> _textures = new Dictionary<SurfaceClass, PbrTexSet>(8);
        private readonly Dictionary<string, Material> _upgradedMatCache = new Dictionary<string, Material>(64);
        private readonly List<Material> _ownedMaterials = new List<Material>(64);
        private readonly List<Transform> _fountainStreams = new List<Transform>(4);
        private readonly List<Renderer> _festoonBulbs = new List<Renderer>(64);
        private Material _festoonLitMat;
        private Material _festoonDayMat;
        private Material _wireMat;
        private Material _fountainWaterMat;
        private bool _subscribed;
        private bool _festoonsCurrentlyLit;
        private float _lastPurchaseTime = -9f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;

            GameObject go = new GameObject("OurWorld_IstanbulThemeOverhaul");
            go.AddComponent<IstanbulThemeOverhaul>();
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
            // Wait 1 frame so WorldExpansionAndFarmBuilder & NeighborhoodSports finish creating runtime meshes
            yield return null;

            LoadPolyHavenTextures();
            UpgradeWorldSurfaceMaterials();
            BuildIstanbulStreetscapeHeroProps();
            ConfigureAtmosphericDepth();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted += HandleThemePropInteracted;
                PlayerModeController.Instance.RefreshInteractablesCache();
                _subscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (_subscribed && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted -= HandleThemePropInteracted;
            }
            for (int i = 0; i < _ownedMaterials.Count; i++)
            {
                if (_ownedMaterials[i] != null) Destroy(_ownedMaterials[i]);
            }
            _ownedMaterials.Clear();
            _upgradedMatCache.Clear();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Shimmering animated water streams on the Istanbul Mahalle Fountains
            if (_fountainStreams.Count > 0)
            {
                float t = Time.time;
                for (int i = 0; i < _fountainStreams.Count; i++)
                {
                    Transform st = _fountainStreams[i];
                    if (st == null) continue;
                    float pulse = 0.88f + Mathf.Sin(t * 11f + i * 2.1f) * 0.14f;
                    st.localScale = new Vector3(0.036f * pulse, 0.34f, 0.036f * pulse);
                }
            }

            // Sync festoon cafe string lights & subtle atmospheric depth to time of day
            if (Time.frameCount % 12 == 0)
            {
                UpdateAtmosphereAndFestoonLights();
            }
        }

        // =====================================================================
        // 1. POLY HAVEN CC0 PBR TEXTURE LOADING & SCENE MATERIAL ENRICHMENT
        // =====================================================================
        private void LoadPolyHavenTextures()
        {
            LoadSet(SurfaceClass.Grass, "ph_grass");
            LoadSet(SurfaceClass.Plaster, "ph_plaster");
            LoadSet(SurfaceClass.Cobblestone, "ph_cobblestone");
            LoadSet(SurfaceClass.Asphalt, "ph_asphalt");
            LoadSet(SurfaceClass.Sand, "ph_sand");
            LoadSet(SurfaceClass.RoofTiles, "ph_rooftiles");
            LoadSet(SurfaceClass.WoodPlanks, "ph_woodplanks");
        }

        private void LoadSet(SurfaceClass sc, string prefix)
        {
            Texture2D alb = Resources.Load<Texture2D>(TexRoot + prefix + "_albedo");
            Texture2D nor = Resources.Load<Texture2D>(TexRoot + prefix + "_normal");
            if (alb != null || nor != null)
            {
                _textures[sc] = new PbrTexSet { Albedo = alb, Normal = nor };
            }
        }

        private void UpgradeWorldSurfaceMaterials()
        {
            if (_textures.Count == 0) return;

            MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>();
            int upgradedCount = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer mr = renderers[i];
                if (mr == null || !mr.gameObject.activeInHierarchy) continue;
                if (ShouldSkipRenderer(mr)) continue;

                Material[] mats = mr.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                bool changed = false;
                Vector3 size = mr.bounds.size;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material src = mats[m];
                    if (src == null || src.shader == null) continue;
                    if (!src.shader.name.Contains("Lit")) continue;

                    // Preserve materials that ALREADY have a custom authored texture
                    Texture existingTex = src.HasProperty(BaseMapId) ? src.GetTexture(BaseMapId) : null;
                    if (existingTex == null && src.HasProperty(MainTexId)) existingTex = src.GetTexture(MainTexId);
                    if (existingTex != null) continue;

                    SurfaceClass cls = ClassifySurface(mr.gameObject.name, src.name, src, size);
                    if (cls == SurfaceClass.None || !_textures.TryGetValue(cls, out PbrTexSet texSet)) continue;

                    Vector2 tiling = ComputeTilingBucket(cls, size);
                    Material enriched = GetOrBuildEnrichedMaterial(src, cls, texSet, tiling);
                    if (enriched != null)
                    {
                        mats[m] = enriched;
                        changed = true;
                        upgradedCount++;
                    }
                }

                if (changed)
                {
                    mr.sharedMaterials = mats;
                }
            }

            Debug.Log($"[IstanbulThemeOverhaul] Upgraded {upgradedCount} flat scene surfaces using {_upgradedMatCache.Count} cached Poly Haven CC0 PBR materials.");
        }

        private static bool ShouldSkipRenderer(MeshRenderer mr)
        {
            Transform tr = mr.transform;
            string n = tr.name;
            // Skip sports lines/balls/rings, vehicles, characters, custom Blender OurWorldLife prefabs, water, glass
            if (n.IndexOf("Line", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Ball", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Reticle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Ring", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Net", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Window", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Sign", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Lamp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Stripes", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Court", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Pitch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Turf", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("AydosHill", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Skip dynamic actors / vehicles / sports arenas / interior zones
            Transform p = tr;
            int depth = 0;
            while (p != null && depth < 5)
            {
                string pn = p.name;
                if (pn.StartsWith("OW_", StringComparison.Ordinal) ||
                    pn.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pn.IndexOf("NPC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pn.IndexOf("Pedestrian", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pn.IndexOf("Vehicle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pn.IndexOf("Car", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pn.IndexOf("Football", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pn.IndexOf("Basketball", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pn.IndexOf("Tennis", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pn.IndexOf("Interior", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
                p = p.parent;
                depth++;
            }
            return false;
        }

        private static SurfaceClass ClassifySurface(string objName, string matName, Material mat, Vector3 size)
        {
            string objLow = objName.ToLowerInvariant();
            string matLow = matName.ToLowerInvariant();
            string combined = objLow + " " + matLow;
            Color col = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : (mat.HasProperty(ColorId) ? mat.GetColor(ColorId) : Color.gray);

            // Never overwrite fabric umbrellas, canopies, awnings, cushions, seats, or vehicles
            if (combined.Contains("umbrella") || combined.Contains("canopy") || combined.Contains("awning") ||
                combined.Contains("fabric") || combined.Contains("cushion") || combined.Contains("lounger") ||
                combined.Contains("chair") || combined.Contains("bench") || combined.Contains("post") ||
                combined.Contains("pole") || combined.Contains("rail") || combined.Contains("frame"))
            {
                return SurfaceClass.None;
            }

            // Volleyball beach sand & lake sandy shore
            if (combined.Contains("sand") || combined.Contains("beach") || combined.Contains("volleysand") || combined.Contains("lakeshore"))
            {
                return SurfaceClass.Sand;
            }

            // Grass, meadows, hills, park lawns
            if (combined.Contains("grass") || combined.Contains("meadow") || combined.Contains("lawn") ||
                combined.Contains("valleyground") || combined.Contains("aydoshill") || combined.Contains("pasture"))
            {
                return SurfaceClass.Grass;
            }

            // Asphalt roadways
            if (combined.Contains("asphalt") || combined.Contains("road") || combined.Contains("boulevard") || combined.Contains("street"))
            {
                return SurfaceClass.Asphalt;
            }

            // Cobblestone plazas, kilit parke sidewalks & walkways
            if (combined.Contains("parke") || combined.Contains("sidewalk") || combined.Contains("plaza") ||
                combined.Contains("promenade") || combined.Contains("walkway") || combined.Contains("cobble") ||
                combined.Contains("travertine") || combined.Contains("pavement") || combined.Contains("stonepath"))
            {
                return SurfaceClass.Cobblestone;
            }

            // Only actual roof geometry receives terracotta roof tiles (never walls or umbrellas!)
            if ((objLow.Contains("roof") || objLow.Contains("kiremit") || matLow.Contains("rooftile")) &&
                !objLow.Contains("wall") && !objLow.Contains("facade"))
            {
                return SurfaceClass.RoofTiles;
            }

            // Only actual wooden piers, fences, pergolas & barns receive wood planks (never residential buildings!)
            if ((objLow.Contains("pier") || objLow.Contains("fence") || objLow.Contains("barn") ||
                 objLow.Contains("pergola") || objLow.Contains("plank") || objLow.Contains("deck")) &&
                !objLow.Contains("pool_deck"))
            {
                return SurfaceClass.WoodPlanks;
            }

            // Building facades & neighborhood plaster walls (bright Mediterranean plaster)
            if (combined.Contains("facade") || combined.Contains("plaster") || combined.Contains("building") ||
                combined.Contains("apt") || combined.Contains("house") || combined.Contains("wall") ||
                combined.Contains("block") || combined.Contains("shop") || combined.Contains("camii") ||
                (size.y >= 3.2f && Mathf.Max(size.x, size.z) >= 3.5f))
            {
                if (col.g > col.r * 1.18f && col.g > col.b * 1.18f) return SurfaceClass.None;
                return SurfaceClass.Plaster;
            }

            return SurfaceClass.None;
        }

        private static Vector2 ComputeTilingBucket(SurfaceClass cls, Vector3 size)
        {
            float spanX = Mathf.Max(size.x, size.z);
            float spanY = cls == SurfaceClass.Plaster ? Mathf.Max(2f, size.y) : Mathf.Min(size.x, size.z);
            if (spanY < 0.5f) spanY = spanX;

            float unit = cls switch
            {
                SurfaceClass.Grass => 7.5f,
                SurfaceClass.Asphalt => 5.5f,
                SurfaceClass.Cobblestone => 3.2f,
                SurfaceClass.Sand => 4.2f,
                SurfaceClass.RoofTiles => 3.0f,
                SurfaceClass.WoodPlanks => 2.4f,
                _ => 4.0f
            };

            // Quantize into clean power-ish buckets to keep material cache small & SRP-batched!
            float tx = QuantizeTile(spanX / unit);
            float ty = QuantizeTile(spanY / unit);
            return new Vector2(tx, ty);
        }

        private static float QuantizeTile(float raw)
        {
            if (raw <= 1.5f) return 1f;
            if (raw <= 3.0f) return 2f;
            if (raw <= 6.0f) return 4f;
            if (raw <= 12.0f) return 8f;
            if (raw <= 26.0f) return 16f;
            if (raw <= 56.0f) return 32f;
            return 64f;
        }

        private Material GetOrBuildEnrichedMaterial(Material src, SurfaceClass cls, PbrTexSet texSet, Vector2 tiling)
        {
            string key = $"{src.GetEntityId()}_{cls}_{tiling.x:0}_{tiling.y:0}";
            if (_upgradedMatCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material m = new Material(src);
            m.name = $"{src.name}_Theme_{cls}";

            Color orig = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : (m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white);
            // Gently lift luminance when multiplying over a photo-scanned Poly Haven albedo so scenes look brighter & richer, never muddy!
            Color lifted = cls switch
            {
                SurfaceClass.Grass => new Color(
                    Mathf.Clamp01(orig.r * 1.18f + 0.14f),
                    Mathf.Clamp01(orig.g * 1.24f + 0.14f),
                    Mathf.Clamp01(orig.b * 1.08f + 0.08f),
                    1f),
                SurfaceClass.Plaster => new Color(
                    Mathf.Clamp01(orig.r * 1.18f + 0.10f),
                    Mathf.Clamp01(orig.g * 1.16f + 0.09f),
                    Mathf.Clamp01(orig.b * 1.12f + 0.07f),
                    1f),
                SurfaceClass.Cobblestone => new Color(
                    Mathf.Clamp01(orig.r * 1.24f + 0.16f),
                    Mathf.Clamp01(orig.g * 1.22f + 0.14f),
                    Mathf.Clamp01(orig.b * 1.18f + 0.12f),
                    1f),
                SurfaceClass.Asphalt => new Color(
                    Mathf.Clamp01(orig.r * 1.32f + 0.16f),
                    Mathf.Clamp01(orig.g * 1.32f + 0.16f),
                    Mathf.Clamp01(orig.b * 1.35f + 0.18f),
                    1f),
                SurfaceClass.Sand => new Color(1.0f, 0.96f, 0.88f, 1f),
                SurfaceClass.RoofTiles => new Color(
                    Mathf.Clamp01(orig.r * 1.22f + 0.12f),
                    Mathf.Clamp01(orig.g * 1.12f + 0.06f),
                    Mathf.Clamp01(orig.b * 1.06f + 0.04f),
                    1f),
                SurfaceClass.WoodPlanks => new Color(
                    Mathf.Clamp01(orig.r * 1.22f + 0.10f),
                    Mathf.Clamp01(orig.g * 1.16f + 0.08f),
                    Mathf.Clamp01(orig.b * 1.10f + 0.06f),
                    1f),
                _ => orig
            };

            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, lifted);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, lifted);

            if (texSet.Albedo != null)
            {
                if (m.HasProperty(BaseMapId))
                {
                    m.SetTexture(BaseMapId, texSet.Albedo);
                    m.SetTextureScale(BaseMapId, tiling);
                }
                if (m.HasProperty(MainTexId))
                {
                    m.SetTexture(MainTexId, texSet.Albedo);
                    m.SetTextureScale(MainTexId, tiling);
                }
            }

            if (texSet.Normal != null && m.HasProperty(BumpMapId))
            {
                m.SetTexture(BumpMapId, texSet.Normal);
                m.SetTextureScale(BumpMapId, tiling);
                m.EnableKeyword("_NORMALMAP");
                if (m.HasProperty(BumpScaleId))
                {
                    m.SetFloat(BumpScaleId, cls == SurfaceClass.Plaster ? 0.38f : 0.55f);
                }
            }

            if (m.HasProperty(SmoothnessId))
            {
                m.SetFloat(SmoothnessId, cls == SurfaceClass.Asphalt ? 0.24f : (cls == SurfaceClass.Cobblestone ? 0.28f : 0.16f));
            }

            _upgradedMatCache[key] = m;
            _ownedMaterials.Add(m);
            return m;
        }

        // =====================================================================
        // 2. ICONIC ISTANBUL STREETSCAPE HERO PROPS (BLENDER 5.3 + POLY HAVEN CC0)
        // =====================================================================
        private void BuildIstanbulStreetscapeHeroProps()
        {
            Transform root = new GameObject("Istanbul_Streetscape_Props").transform;
            root.SetParent(transform, false);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            _festoonLitMat = new Material(lit) { color = new Color(1.0f, 0.88f, 0.52f, 1f) };
            _festoonLitMat.EnableKeyword("_EMISSION");
            _festoonLitMat.SetColor(EmissionColorId, new Color(1.0f, 0.78f, 0.32f) * 2.6f);
            _festoonDayMat = new Material(lit) { color = new Color(0.96f, 0.94f, 0.86f, 1f) };
            _wireMat = new Material(lit) { color = new Color(0.14f, 0.15f, 0.17f, 1f) };
            _fountainWaterMat = new Material(lit) { color = new Color(0.54f, 0.90f, 0.98f, 0.85f) };
            _fountainWaterMat.EnableKeyword("_EMISSION");
            _fountainWaterMat.SetColor(EmissionColorId, new Color(0.32f, 0.72f, 0.86f) * 0.45f);
            _ownedMaterials.Add(_festoonLitMat);
            _ownedMaterials.Add(_festoonDayMat);
            _ownedMaterials.Add(_wireMat);
            _ownedMaterials.Add(_fountainWaterMat);

            // A. 4 Interactive Istanbul Simit Carts (buy fresh hot sesame simit!)
            (Vector3 pos, float yaw, string id)[] simitCarts =
            {
                (new Vector3(6.8f, 0.04f, 13.8f), 195f, "istanbul_simit_square"),
                (new Vector3(-25.5f, 0.04f, -21.8f), 30f, "istanbul_simit_sports"),
                (new Vector3(-9.5f, 0.04f, 8.2f), 175f, "istanbul_simit_selman"),
                (new Vector3(-165.0f, 0.05f, -5.2f), -90f, "istanbul_simit_lake")
            };
            for (int i = 0; i < simitCarts.Length; i++)
            {
                var sc = simitCarts[i];
                GameObject cart = OurWorldModelLibrary.InstantiateModel("OW_SimitCart", root, sc.pos, Quaternion.Euler(0f, sc.yaw, 0f), Vector3.one * 1.05f);
                if (cart != null)
                {
                    BoxCollider bc = cart.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, 0.85f, 0f);
                    bc.size = new Vector3(1.55f, 1.70f, 0.85f);

                    InteractableTrigger trig = cart.AddComponent<InteractableTrigger>();
                    trig.Configure(InteractableKind.InspectObject, sc.id, "ui.btn.interact", 2.8f);
                    trig.SetCustomPrompt("Sıcak Simit Al (10 ₺ · +28 Enerji)", "Buy Fresh Hot Simit (10 ₺ · +28 Energy)");
                }
            }

            // B. 7 Authentic Turkish Tea Garden Sets (Çay Bahçesi) — sittable + drink hot çay!
            (Vector3 pos, float yaw, string id)[] teaSets =
            {
                // Town Square Tea Garden under Cinar shade
                (new Vector3(14.5f, 0.04f, 15.2f), 15f, "istanbul_tea_sq1"),
                (new Vector3(18.6f, 0.04f, 15.5f), -20f, "istanbul_tea_sq2"),
                (new Vector3(16.5f, 0.04f, 19.2f), 8f, "istanbul_tea_sq3"),
                // Sports Complex Overlook Tea Deck (between Football & Pool!)
                (new Vector3(-2.5f, 0.04f, -23.5f), 0f, "istanbul_tea_sport1"),
                (new Vector3(2.2f, 0.04f, -23.5f), 18f, "istanbul_tea_sport2"),
                // Sultanbeyli Lake Lakeside Tea Patio
                (new Vector3(-164.5f, 0.05f, 7.2f), -85f, "istanbul_tea_lake1"),
                (new Vector3(-164.5f, 0.05f, 11.4f), -95f, "istanbul_tea_lake2")
            };
            for (int i = 0; i < teaSets.Length; i++)
            {
                var ts = teaSets[i];
                GameObject set = OurWorldModelLibrary.InstantiateModel("OW_TeaGardenSet", root, ts.pos, Quaternion.Euler(0f, ts.yaw, 0f), Vector3.one);
                if (set != null)
                {
                    // Vary umbrella colors (terracotta & turquoise) for lively Istanbul cafe rhythm!
                    if (i % 2 == 1)
                    {
                        OurWorldModelLibrary.TintMaterialByKeyword(set, "TerracottaWarm", new Color(0.11f, 0.56f, 0.60f));
                    }

                    // Hot Turkish Tea interactable on the table
                    InteractableTrigger teaTrig = set.AddComponent<InteractableTrigger>();
                    teaTrig.Configure(InteractableKind.InspectObject, ts.id, "ui.btn.interact", 2.6f);
                    teaTrig.SetCustomPrompt("Tavşan Kanı Çay İç (8 ₺ · +22 Enerji)", "Drink Hot Turkish Tea (8 ₺ · +22 Energy)");

                    // Sitting slot on left chair
                    GameObject seatGo = new GameObject($"TeaChairSeat_{i}");
                    seatGo.transform.SetParent(set.transform, false);
                    seatGo.transform.localPosition = new Vector3(-0.88f, 0.48f, 0f);
                    seatGo.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    SeatInteractable seat = seatGo.AddComponent<SeatInteractable>();
                    InteractableTrigger seatTrig = seatGo.AddComponent<InteractableTrigger>();
                    seatTrig.Configure(InteractableKind.Seat, $"tea_seat_{i}", "ui.btn.sit", 1.8f);
                    seat.Configure(seatGo.transform, seatTrig, false);
                }
            }

            // C. 2 Carved Limestone Ottoman/Istanbul Mahalle Çeşmeleri with Shimmering Water Jets!
            (Vector3 pos, float yaw)[] fountains =
            {
                (new Vector3(47.5f, 0.04f, -15.0f), 180f),
                (new Vector3(-8.5f, 0.04f, -22.5f), 0f)
            };
            for (int i = 0; i < fountains.Length; i++)
            {
                var fn = fountains[i];
                GameObject f = OurWorldModelLibrary.InstantiateModel("OW_IstanbulFountain", root, fn.pos, Quaternion.Euler(0f, fn.yaw, 0f), Vector3.one);
                if (f != null)
                {
                    BoxCollider bc = f.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, 1.5f, 0f);
                    bc.size = new Vector3(2.2f, 3.0f, 2.0f);

                    // Twin animated sparkling water streams falling from the brass taps into the marble trough
                    for (int s = -1; s <= 1; s += 2)
                    {
                        GameObject stream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        stream.name = $"FountainWaterStream_{s}";
                        Destroy(stream.GetComponent<Collider>());
                        stream.transform.SetParent(f.transform, false);
                        stream.transform.localPosition = new Vector3(s * 0.28f, 0.94f, 0.80f);
                        stream.transform.localScale = new Vector3(0.035f, 0.34f, 0.035f);
                        MeshRenderer mr = stream.GetComponent<MeshRenderer>();
                        if (mr != null)
                        {
                            mr.shadowCastingMode = ShadowCastingMode.Off;
                            mr.sharedMaterial = _fountainWaterMat;
                        }
                        _fountainStreams.Add(stream.transform);
                    }

                    InteractableTrigger trig = f.AddComponent<InteractableTrigger>();
                    trig.Configure(InteractableKind.InspectObject, $"istanbul_fountain_{i}", "ui.btn.interact", 3.0f);
                    trig.SetCustomPrompt("Mahalle Çeşmesinden Su İç (+16 Enerji)", "Drink from Neighborhood Fountain (+16 Energy)");
                }
            }

            // D. 8 Poly Haven CC0 Sittable Park Benches + Brass Lanterns!
            (Vector3 pos, float yaw)[] benches =
            {
                (new Vector3(-28.0f, 0.04f, -25.0f), 180f),
                (new Vector3(-19.0f, 0.04f, -25.0f), 180f),
                (new Vector3(31.0f, 0.04f, -24.5f), 180f),
                (new Vector3(48.5f, 0.04f, -24.5f), 180f),
                (new Vector3(-22.8f, 0.04f, 5.6f), 180f),
                (new Vector3(13.2f, 0.04f, 5.6f), 180f),
                (new Vector3(-164.5f, 0.05f, -9.5f), -90f),
                (new Vector3(-164.5f, 0.05f, 15.5f), -90f)
            };
            for (int i = 0; i < benches.Length; i++)
            {
                var bn = benches[i];
                GameObject bench = OurWorldModelLibrary.InstantiateModel("OW_ParkBench", root, bn.pos, Quaternion.Euler(0f, bn.yaw, 0f), Vector3.one);
                if (bench != null)
                {
                    GameObject seatGo = new GameObject($"CC0BenchSeat_{i}");
                    seatGo.transform.SetParent(bench.transform, false);
                    seatGo.transform.localPosition = new Vector3(0f, 0.48f, 0f);
                    SeatInteractable seat = seatGo.AddComponent<SeatInteractable>();
                    InteractableTrigger seatTrig = seatGo.AddComponent<InteractableTrigger>();
                    seatTrig.Configure(InteractableKind.Seat, $"istanbul_bench_{i}", "ui.btn.sit", 2.1f);
                    seat.Configure(seatGo.transform, seatTrig, false);
                }
            }

            // E. 26 Sculpted Multi-Branch Istanbul Çınar (Plane) & Selvi (Cypress) Trees!
            (Vector3 pos, float scale, bool cypress)[] trees =
            {
                // Sports Complex lush perimeter framing (Tennis, Football, Pool, Basketball, Volleyball!)
                (new Vector3(-31.0f, 0.02f, -22.5f), 1.08f, false),
                (new Vector3(-5.0f,  0.02f, -21.5f), 1.15f, false),
                (new Vector3(28.5f,  0.02f, -22.5f), 1.05f, false),
                (new Vector3(52.5f,  0.02f, -22.5f), 1.12f, false),
                (new Vector3(71.5f,  0.02f, -34.0f), 1.10f, false),
                (new Vector3(-53.5f, 0.02f, -38.0f), 1.18f, false),
                (new Vector3(3.5f,   0.02f, -44.0f), 1.05f, true),
                (new Vector3(29.0f,  0.02f, -46.0f), 1.10f, true),
                (new Vector3(-28.0f, 0.02f, -49.5f), 1.12f, true),
                (new Vector3(51.0f,  0.02f, -49.5f), 1.08f, true),
                // Town Square & Tea Garden Shade Canopy!
                (new Vector3(11.5f,  0.02f, 18.5f), 1.25f, false),
                (new Vector3(21.5f,  0.02f, 18.8f), 1.20f, false),
                (new Vector3(16.5f,  0.02f, 23.5f), 1.10f, true),
                (new Vector3(-14.5f, 0.02f, 16.0f), 1.12f, false),
                (new Vector3(-24.0f, 0.02f, 12.0f), 1.05f, true),
                // Mosque Courtyard & Selman Sokağı Boulevard (safely clear of house walls & garden fences!)
                (new Vector3(68.0f,  0.02f, -20.5f), 1.18f, true),
                (new Vector3(74.5f,  0.02f, -20.5f), 1.24f, true),
                (new Vector3(58.0f,  0.02f, -15.0f), 1.14f, false),
                (new Vector3(35.0f,  0.02f, 10.5f),  1.05f, false),
                (new Vector3(-39.2f, 0.02f, 10.5f),  1.08f, false),
                (new Vector3(-55.2f, 0.02f, 15.0f),  1.12f, true),
                // Sultanbeyli Lake Scenic Promenade & Aydos Farm Entrance
                (new Vector3(-160.0f, 0.02f, 18.0f), 1.22f, false),
                (new Vector3(-160.0f, 0.02f, -18.0f),1.18f, false),
                (new Vector3(-174.0f, 0.02f, 22.0f), 1.15f, true),
                (new Vector3(132.0f,  0.02f, 8.5f),  1.20f, false),
                (new Vector3(132.0f,  0.02f, -8.5f), 1.16f, true)
            };
            for (int i = 0; i < trees.Length; i++)
            {
                var tr = trees[i];
                string model = tr.cypress ? "OW_CypressTree" : "OW_PlaneTree";
                OurWorldModelLibrary.InstantiateModel(model, root, tr.pos, Quaternion.Euler(0f, i * 47f, 0f), Vector3.one * tr.scale);
            }

            // F. 14 Carved Travertine Tulip & Lavender Flower Planters!
            (Vector3 pos, float yaw)[] planters =
            {
                (new Vector3(-17.5f, 0.03f, -26.5f), 0f),
                (new Vector3(-10.5f, 0.03f, -26.5f), 0f),
                (new Vector3(11.5f,  0.03f, -28.0f), 0f),
                (new Vector3(20.5f,  0.03f, -28.0f), 0f),
                (new Vector3(36.5f,  0.03f, -26.5f), 0f),
                (new Vector3(45.5f,  0.03f, -26.5f), 0f),
                (new Vector3(55.5f,  0.03f, -27.0f), 0f),
                (new Vector3(62.5f,  0.03f, -27.0f), 0f),
                (new Vector3(9.6f,   0.03f, 5.8f),   90f),
                (new Vector3(11.0f,  0.03f, 11.5f),  0f),
                (new Vector3(16.6f,  0.03f, 6.4f),   0f),
                (new Vector3(-6.0f,  0.03f, 8.5f),   0f),
                (new Vector3(-165.5f, 0.04f, 3.2f),  90f),
                (new Vector3(-165.5f, 0.04f, -3.2f), 90f)
            };
            for (int i = 0; i < planters.Length; i++)
            {
                OurWorldModelLibrary.InstantiateModel("OW_FlowerPlanter", root, planters[i].pos, Quaternion.Euler(0f, planters[i].yaw, 0f), Vector3.one);
            }

            // G. Decorative Striped Turkish Shop Awnings (Tente) & Brass Lanterns on Tea Garden Posts!
            BuildFestoonStringLights(root, new Vector3(11.5f, 3.4f, 14.0f), new Vector3(21.5f, 3.4f, 14.0f), 8);
            BuildFestoonStringLights(root, new Vector3(11.5f, 3.4f, 19.5f), new Vector3(21.5f, 3.4f, 19.5f), 8);
            BuildFestoonStringLights(root, new Vector3(-5.5f, 3.5f, -22.5f), new Vector3(5.5f, 3.5f, -22.5f), 8);
            BuildFestoonStringLights(root, new Vector3(-166.5f, 3.2f, 4.5f), new Vector3(-166.5f, 3.2f, 14.5f), 8);

            // Hang 6 Poly Haven CC0 Ornate Brass Lanterns beside Tea Gardens & Fountains
            Vector3[] lanternSpots =
            {
                new Vector3(11.5f, 2.85f, 14.0f),
                new Vector3(21.5f, 2.85f, 14.0f),
                new Vector3(-5.5f, 2.95f, -22.5f),
                new Vector3(5.5f,  2.95f, -22.5f),
                new Vector3(-166.5f, 2.65f, 4.5f),
                new Vector3(-166.5f, 2.65f, 14.5f)
            };
            for (int i = 0; i < lanternSpots.Length; i++)
            {
                // Timber post supporting festoon wire + hanging brass lantern
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = $"FestoonPost_{i}";
                Destroy(post.GetComponent<Collider>());
                post.transform.SetParent(root, false);
                post.transform.position = new Vector3(lanternSpots[i].x, lanternSpots[i].y * 0.56f, lanternSpots[i].z);
                post.transform.localScale = new Vector3(0.11f, lanternSpots[i].y * 0.58f, 0.11f);
                MeshRenderer pmr = post.GetComponent<MeshRenderer>();
                if (pmr != null) pmr.sharedMaterial = _wireMat;

                OurWorldModelLibrary.InstantiateModel("OW_IstanbulLantern", root, lanternSpots[i], Quaternion.identity, Vector3.one);
            }

            // Striped Shop Awnings (Tente) mounted flush on Selman Sokağı shopfront facades
            (Vector3 pos, float yaw, bool teal)[] awnings =
            {
                (new Vector3(-32.5f, 3.05f, 6.45f),  180f, false),
                (new Vector3(-16.5f, 3.05f, 6.25f),  180f, true),
                (new Vector3(2.6f,   3.05f, 5.05f),  180f, false),
                (new Vector3(22.9f,  3.05f, 6.05f),  180f, true),
                (new Vector3(-31.2f, 3.05f, -6.45f), 0f, true),
                (new Vector3(44.0f,  3.05f, -6.85f), 0f, false)
            };
            for (int i = 0; i < awnings.Length; i++)
            {
                var aw = awnings[i];
                GameObject aGo = OurWorldModelLibrary.InstantiateModel("OW_ShopAwning", root, aw.pos, Quaternion.Euler(0f, aw.yaw, 0f), Vector3.one);
                if (aGo != null && aw.teal)
                {
                    OurWorldModelLibrary.TintMaterialByKeyword(aGo, "IstanbulCrimson", new Color(0.10f, 0.56f, 0.62f));
                }
            }
        }

        private void BuildFestoonStringLights(Transform parent, Vector3 start, Vector3 end, int bulbs)
        {
            GameObject strand = new GameObject("FestoonLightStrand");
            strand.transform.SetParent(parent, false);

            Vector3 mid = (start + end) * 0.5f;
            float dist = Vector3.Distance(start, end);
            GameObject wire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wire.name = "Wire";
            Destroy(wire.GetComponent<Collider>());
            wire.transform.SetParent(strand.transform, false);
            wire.transform.position = mid + Vector3.down * 0.14f;
            wire.transform.rotation = Quaternion.LookRotation((end - start).normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            wire.transform.localScale = new Vector3(0.018f, dist * 0.5f, 0.018f);
            MeshRenderer wmr = wire.GetComponent<MeshRenderer>();
            if (wmr != null) { wmr.shadowCastingMode = ShadowCastingMode.Off; wmr.sharedMaterial = _wireMat; }

            for (int i = 1; i < bulbs; i++)
            {
                float u = i / (float)bulbs;
                Vector3 p = Vector3.Lerp(start, end, u);
                // Natural catenary sag
                p.y -= Mathf.Sin(u * Mathf.PI) * 0.34f;
                GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = $"FestoonBulb_{i}";
                Destroy(bulb.GetComponent<Collider>());
                bulb.transform.SetParent(strand.transform, false);
                bulb.transform.position = p;
                bulb.transform.localScale = Vector3.one * 0.13f;
                MeshRenderer bmr = bulb.GetComponent<MeshRenderer>();
                if (bmr != null)
                {
                    bmr.shadowCastingMode = ShadowCastingMode.Off;
                    bmr.sharedMaterial = _festoonDayMat;
                    _festoonBulbs.Add(bmr);
                }
            }
        }

        // =====================================================================
        // 3. ATMOSPHERIC DEPTH & INTERACTIVE STREETSCAPE ACTIONS
        // =====================================================================
        private void ConfigureAtmosphericDepth()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            if (DayNightCycle.Instance == null)
            {
                RenderSettings.fogColor = new Color(0.68f, 0.77f, 0.85f, 1f);
                RenderSettings.fogDensity = 0.0016f;
            }
            UpdateAtmosphereAndFestoonLights();
        }

        private void UpdateAtmosphereAndFestoonLights()
        {
            // DayNightCycle owns RenderSettings.fogColor and RenderSettings.fogDensity smoothly;
            // never overwrite fog here or distant Aydos hills will strobe-flicker between the two values!
            float hour = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentHour : 11f;
            bool isNightOrDusk = hour >= 18.2f || hour < 6.5f;

            if (isNightOrDusk != _festoonsCurrentlyLit)
            {
                _festoonsCurrentlyLit = isNightOrDusk;
                Material target = isNightOrDusk ? _festoonLitMat : _festoonDayMat;
                for (int i = 0; i < _festoonBulbs.Count; i++)
                {
                    if (_festoonBulbs[i] != null) _festoonBulbs[i].sharedMaterial = target;
                }
            }
        }

        private void HandleThemePropInteracted(InteractableTrigger trig)
        {
            if (trig == null || string.IsNullOrEmpty(trig.InteractableId)) return;
            if (Time.time - _lastPurchaseTime < 1.1f) return;

            string id = trig.InteractableId;
            bool en = LocalizationService.IsEnglish;
            if (id.StartsWith("istanbul_simit_", StringComparison.Ordinal))
            {
                _lastPurchaseTime = Time.time;
                if (SaveManager.Instance != null && !SaveManager.Instance.TrySpend(10))
                {
                    PlayerModeController.Instance?.RaiseStatusMessage(en ? "Insufficient funds! (Hot Simit: 10 ₺)" : "Yetersiz bakiye! (Sıcak Simit: 10 ₺)");
                    return;
                }
                PlayerNeedsController.Instance?.RestoreEnergy(28f);
                GameFeelAudio.PlayUiClick();
                GameFeelAudio.SpawnBurst(trig.transform.position + Vector3.up * 1.35f, new Color(0.98f, 0.78f, 0.26f), count: 10, spread: 0.42f);
                PlayerModeController.Instance?.RaiseStatusMessage(en ? "Warm crispy sesame Istanbul simit! (+28 Energy)" : "Sıcacık susamlı İstanbul simidi! (+28 Enerji)");
            }
            else if (id.StartsWith("istanbul_tea_", StringComparison.Ordinal))
            {
                _lastPurchaseTime = Time.time;
                if (SaveManager.Instance != null && !SaveManager.Instance.TrySpend(8))
                {
                    PlayerModeController.Instance?.RaiseStatusMessage(en ? "Insufficient funds! (Turkish Tea: 8 ₺)" : "Yetersiz bakiye! (Çay: 8 ₺)");
                    return;
                }
                PlayerNeedsController.Instance?.RestoreEnergy(22f);
                GameFeelAudio.PlayUiClick();
                GameFeelAudio.SpawnBurst(trig.transform.position + Vector3.up * 1.10f, new Color(0.94f, 0.42f, 0.16f), count: 8, spread: 0.35f);
                PlayerModeController.Instance?.RaiseStatusMessage(en ? "Freshly brewed Turkish tea! (+22 Energy)" : "Tavşan kanı ince belli çay! (+22 Enerji)");
            }
            else if (id.StartsWith("istanbul_fountain_", StringComparison.Ordinal))
            {
                _lastPurchaseTime = Time.time;
                PlayerNeedsController.Instance?.RestoreEnergy(16f);
                GameFeelAudio.PlayWaterSplash();
                GameFeelAudio.SpawnBurst(trig.transform.position + Vector3.up * 1.15f, new Color(0.45f, 0.88f, 0.98f), count: 10, spread: 0.42f);
                PlayerModeController.Instance?.RaiseStatusMessage(en ? "Refreshing cold water from the neighborhood fountain! (+16 Energy)" : "Mahalle çeşmesinden buz gibi su! (+16 Enerji)");
            }
        }
    }
}
