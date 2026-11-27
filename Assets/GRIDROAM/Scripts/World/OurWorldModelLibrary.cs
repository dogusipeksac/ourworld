using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Runtime loader and cache for all commercial-safe custom Blender FBX models stored under
    /// Assets/GRIDROAM/Resources/OurWorldLife/.
    /// Provides fast recursive pivot lookup, URP-safe materials, double-sided alpha-cutout
    /// botanical foliage binding (Plane, Aydos Pine & Cypress trees), transparent architectural/
    /// automotive glass, and PolyHaven PBR normal-mapped fortress stonework across Editor, iOS, and Android.
    /// </summary>
    public static class OurWorldModelLibrary
    {
        private const string ResourceRoot = "OurWorldLife/";
        private const string ThemeTexRoot = "OurWorldTheme/Textures/";

        private static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>(32);
        private static readonly Dictionary<string, Material> SharedSpecialMats = new Dictionary<string, Material>(16);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PrefabCache.Clear();
            SharedSpecialMats.Clear();
        }

        public static GameObject InstantiateModel(
            string modelName,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Vector3? localScale = null,
            bool removeColliders = true)
        {
            if (string.IsNullOrEmpty(modelName)) return null;

            if (!PrefabCache.TryGetValue(modelName, out GameObject prefab) || prefab == null)
            {
                prefab = Resources.Load<GameObject>(ResourceRoot + modelName);
                PrefabCache[modelName] = prefab;
            }

            if (prefab == null)
            {
                return null;
            }

            GameObject inst = UnityEngine.Object.Instantiate(prefab, parent, false);
            inst.name = modelName;
            inst.transform.localPosition = localPos;
            inst.transform.localRotation = localRot;
            inst.transform.localScale = localScale ?? Vector3.one;

            if (removeColliders)
            {
                Collider[] cols = inst.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] != null) UnityEngine.Object.Destroy(cols[i]);
                }
            }

            EnsureUrpMaterials(inst);
            return inst;
        }

        public static Transform FindPart(Transform root, string partName)
        {
            if (root == null || string.IsNullOrEmpty(partName)) return null;
            if (root.name == partName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindPart(root.GetChild(i), partName);
                if (found != null) return found;
            }
            return null;
        }

        public static void TintMaterialByKeyword(GameObject root, string materialNameContains, Color color)
        {
            if (root == null) return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                Material[] mats = r.materials;
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null) continue;
                    if (string.IsNullOrEmpty(materialNameContains) ||
                        mat.name.IndexOf(materialNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
                        if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
                        changed = true;
                    }
                }
                if (changed) r.materials = mats;
            }
        }

        // =====================================================================
        // SHARED BOTANICAL ALPHA-CLIPPED FOLIAGE & TRANSPARENT GLASS MATERIALS
        // =====================================================================
        public static Material GetFoliagePlaneMaterial(bool altTint = false)
        {
            string key = altTint ? "FoliagePlaneB" : "FoliagePlaneA";
            Color tint = altTint ? new Color(0.88f, 0.94f, 0.84f, 1f) : new Color(0.98f, 1.00f, 0.95f, 1f);
            return GetOrCreateAlphaFoliageMaterial(key, "foliage_plane_albedo", "foliage_plane_normal", tint, 0.40f);
        }

        public static Material GetFoliagePineMaterial()
        {
            return GetOrCreateAlphaFoliageMaterial("FoliagePine", "foliage_pine_albedo", "foliage_pine_normal", new Color(0.95f, 0.98f, 0.92f, 1f), 0.36f);
        }

        public static Material GetFoliageCypressMaterial()
        {
            return GetOrCreateAlphaFoliageMaterial("FoliageCypress", "foliage_cypress_albedo", "foliage_cypress_normal", new Color(0.94f, 0.98f, 0.94f, 1f), 0.36f);
        }

        public static Material GetBarkTreeMaterial()
        {
            return GetOrCreatePbrSurfaceMaterial("BarkTree", "bark_tree_albedo", "bark_tree_normal", Color.white, smoothness: 0.14f, bumpScale: 1.15f, tiling: new Vector2(1.5f, 1.5f));
        }

        public static Material GetTransparentAutomotiveGlassMaterial()
        {
            return GetOrCreateTransparentGlassMaterial("AutoGlassTransparent", new Color(0.08f, 0.13f, 0.18f, 0.34f), smoothness: 0.97f, metallic: 0.16f);
        }

        public static Material GetTransparentArchitecturalGlassMaterial()
        {
            return GetOrCreateTransparentGlassMaterial("MetroGlassTransparent", new Color(0.24f, 0.56f, 0.72f, 0.38f), smoothness: 0.96f, metallic: 0.18f);
        }

        private static Material GetOrCreateAlphaFoliageMaterial(string key, string albedoName, string normalName, Color tint, float cutoff)
        {
            if (SharedSpecialMats.TryGetValue(key, out Material cached) && cached != null) return cached;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(urpLit) { name = "MAT_OW_" + key };

            Texture2D alb = Resources.Load<Texture2D>(ThemeTexRoot + albedoName);
            Texture2D nor = Resources.Load<Texture2D>(ThemeTexRoot + normalName);

            if (alb != null)
            {
                if (mat.HasProperty(BaseMapId)) mat.SetTexture(BaseMapId, alb);
                if (mat.HasProperty(MainTexId)) mat.SetTexture(MainTexId, alb);
            }
            if (nor != null && mat.HasProperty(BumpMapId))
            {
                mat.SetTexture(BumpMapId, nor);
                if (mat.HasProperty(BumpScaleId)) mat.SetFloat(BumpScaleId, 0.85f);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, tint);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, tint);
            if (mat.HasProperty(SmoothnessId)) mat.SetFloat(SmoothnessId, 0.22f);
            if (mat.HasProperty(MetallicId)) mat.SetFloat(MetallicId, 0.0f);

            // Crisp Double-Sided URP Alpha-Clipped Foliage (real leaf silhouettes + accurate dappled shadows!)
            if (mat.HasProperty(SurfaceId)) mat.SetFloat(SurfaceId, 0f);
            if (mat.HasProperty(AlphaClipId)) mat.SetFloat(AlphaClipId, 1f);
            if (mat.HasProperty(CutoffId)) mat.SetFloat(CutoffId, cutoff);
            if (mat.HasProperty(CullId)) mat.SetFloat(CullId, (float)CullMode.Off);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)RenderQueue.AlphaTest;

            SharedSpecialMats[key] = mat;
            return mat;
        }

        private static Material GetOrCreateTransparentGlassMaterial(string key, Color rgba, float smoothness, float metallic)
        {
            if (SharedSpecialMats.TryGetValue(key, out Material cached) && cached != null) return cached;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(urpLit) { name = "MAT_OW_" + key };

            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, rgba);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, rgba);
            if (mat.HasProperty(SmoothnessId)) mat.SetFloat(SmoothnessId, smoothness);
            if (mat.HasProperty(MetallicId)) mat.SetFloat(MetallicId, metallic);

            if (mat.HasProperty(SurfaceId)) mat.SetFloat(SurfaceId, 1f);
            if (mat.HasProperty(BlendId)) mat.SetFloat(BlendId, 0f);
            if (mat.HasProperty(SrcBlendId)) mat.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            if (mat.HasProperty(DstBlendId)) mat.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty(ZWriteId)) mat.SetFloat(ZWriteId, 0f);
            if (mat.HasProperty(AlphaClipId)) mat.SetFloat(AlphaClipId, 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;

            SharedSpecialMats[key] = mat;
            return mat;
        }

        private static Material GetOrCreatePbrSurfaceMaterial(
            string key,
            string albedoName,
            string normalName,
            Color tint,
            float smoothness,
            float bumpScale,
            Vector2 tiling,
            float metallic = 0f)
        {
            if (SharedSpecialMats.TryGetValue(key, out Material cached) && cached != null) return cached;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(urpLit) { name = "MAT_OW_" + key };

            Texture2D alb = !string.IsNullOrEmpty(albedoName) ? Resources.Load<Texture2D>(ThemeTexRoot + albedoName) : null;
            Texture2D nor = !string.IsNullOrEmpty(normalName) ? Resources.Load<Texture2D>(ThemeTexRoot + normalName) : null;
            if (alb != null)
            {
                if (mat.HasProperty(BaseMapId)) { mat.SetTexture(BaseMapId, alb); mat.SetTextureScale(BaseMapId, tiling); }
                if (mat.HasProperty(MainTexId)) { mat.SetTexture(MainTexId, alb); mat.SetTextureScale(MainTexId, tiling); }
            }
            if (nor != null && mat.HasProperty(BumpMapId))
            {
                mat.SetTexture(BumpMapId, nor);
                mat.SetTextureScale(BumpMapId, tiling);
                if (mat.HasProperty(BumpScaleId)) mat.SetFloat(BumpScaleId, bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, tint);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, tint);
            if (mat.HasProperty(SmoothnessId)) mat.SetFloat(SmoothnessId, smoothness);
            if (mat.HasProperty(MetallicId)) mat.SetFloat(MetallicId, metallic);

            SharedSpecialMats[key] = mat;
            return mat;
        }

        private static Material GetOrCreateEmissiveMaterial(string key, Color baseColor, Color emissionColor, float smoothness = 0.85f)
        {
            if (SharedSpecialMats.TryGetValue(key, out Material cached) && cached != null) return cached;
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(urpLit) { name = "MAT_OW_" + key };
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, baseColor);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, baseColor);
            if (mat.HasProperty(SmoothnessId)) mat.SetFloat(SmoothnessId, smoothness);
            if (mat.HasProperty(EmissionColorId))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColorId, emissionColor);
            }
            SharedSpecialMats[key] = mat;
            return mat;
        }

        private static Color ExtractSourceColor(Material src)
        {
            if (src == null) return Color.white;
            if (src.HasProperty(BaseColorId)) return src.GetColor(BaseColorId);
            if (src.HasProperty(ColorId)) return src.GetColor(ColorId);
            return Color.white;
        }

        private static string ColorKey(Color c)
        {
            return ColorUtility.ToHtmlStringRGB(c);
        }

        private static Material ResolveSpecialRealismMaterial(Material src)
        {
            if (src == null || string.IsNullOrEmpty(src.name)) return null;
            string rawMatName = src.name;
            Color srcCol = ExtractSourceColor(src);

            if (rawMatName.StartsWith("FoliagePlaneB", StringComparison.OrdinalIgnoreCase)) return GetFoliagePlaneMaterial(altTint: true);
            if (rawMatName.StartsWith("FoliagePlane", StringComparison.OrdinalIgnoreCase))  return GetFoliagePlaneMaterial(altTint: false);
            if (rawMatName.StartsWith("FoliagePine", StringComparison.OrdinalIgnoreCase))   return GetFoliagePineMaterial();
            if (rawMatName.StartsWith("FoliageCypress", StringComparison.OrdinalIgnoreCase)) return GetFoliageCypressMaterial();
            if (rawMatName.StartsWith("BarkTree", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_BarkWood", StringComparison.OrdinalIgnoreCase)) return GetBarkTreeMaterial();
            if (rawMatName.StartsWith("MetroGlass", StringComparison.OrdinalIgnoreCase))    return GetTransparentArchitecturalGlassMaterial();
            if (rawMatName.StartsWith("Windows", StringComparison.OrdinalIgnoreCase))       return GetTransparentAutomotiveGlassMaterial();
            if (rawMatName.StartsWith("GlassClear", StringComparison.OrdinalIgnoreCase))
                return GetOrCreateTransparentGlassMaterial("SimitGlassClear", new Color(0.84f, 0.93f, 0.96f, 0.28f), smoothness: 0.96f, metallic: 0.08f);

            if (rawMatName.StartsWith("CastleStoneWall", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("CastleStoneWall", "ph_cobblestone_albedo", "ph_cobblestone_normal", new Color(0.84f, 0.78f, 0.68f), 0.16f, 1.30f, new Vector2(0.85f, 0.85f));
            if (rawMatName.StartsWith("CastleStoneDark", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("CastleStoneDark", "ph_cobblestone_albedo", "ph_cobblestone_normal", new Color(0.60f, 0.56f, 0.50f), 0.18f, 1.35f, new Vector2(0.95f, 0.95f));
            if (rawMatName.StartsWith("CastleWoodOak", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("CastleWoodOak", "ph_woodplanks_albedo", "ph_woodplanks_normal", new Color(0.76f, 0.58f, 0.42f), 0.24f, 1.05f, new Vector2(0.85f, 0.85f));

            // Phase 3 Stylized-Realism Benchmark PBR Materials (Hero Sheep & Farm Tractor)
            if (rawMatName.StartsWith("OW_SheepWool", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("SheepWool", "animal_sheep_wool_albedo", "animal_sheep_wool_normal", new Color(0.98f, 0.96f, 0.93f), 0.10f, 0.95f, new Vector2(1.0f, 1.0f));
            if (rawMatName.StartsWith("OW_SheepFace", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("SheepFace", "animal_sheep_face_albedo", "animal_sheep_face_normal", new Color(0.95f, 0.93f, 0.91f), 0.24f, 0.60f, new Vector2(1.0f, 1.0f));
            if (rawMatName.StartsWith("OW_EyeGloss", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_EyeDark", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("Eye_Black", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("AnimalPupil", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("EyeGloss", null, null, new Color(0.09f, 0.06f, 0.045f), 0.92f, 0.0f, Vector2.one, metallic: 0.08f);
            if (rawMatName.StartsWith("CatAmberEye", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_DogEyeAmber", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("EyeAmber_" + ColorKey(srcCol), null, null, srcCol, 0.92f, 0.0f, Vector2.one, metallic: 0.08f);

            if (rawMatName.StartsWith("OW_TractorRed", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("TractorRed", null, "tractor_paint_normal", new Color(0.78f, 0.12f, 0.10f), 0.72f, 0.10f, new Vector2(2.0f, 2.0f), metallic: 0.18f);
            if (rawMatName.StartsWith("OW_SafetyYellow", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("SafetyYellow", null, "tractor_paint_normal", new Color(0.92f, 0.72f, 0.16f), 0.68f, 0.10f, new Vector2(2.0f, 2.0f), metallic: 0.18f);
            if (rawMatName.StartsWith("OW_TireRubber", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("GripRubber", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("TireRubber", "tractor_tire_albedo", "tractor_tire_normal", new Color(0.92f, 0.92f, 0.94f), 0.14f, 0.50f, new Vector2(1.5f, 1.5f), metallic: 0.0f);
            if (rawMatName.StartsWith("OW_Chrome", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("BrushedChrome", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("WheelChrome", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("Chrome", null, "metal_brushed_normal", new Color(0.86f, 0.88f, 0.90f), 0.88f, 0.14f, new Vector2(2.0f, 2.0f), metallic: 0.92f);
            if (rawMatName.StartsWith("OW_SteelDark", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("WroughtIron", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("GunMetalDark", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("BluedSteel", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("MachinedSteel", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("MetalDark_" + ColorKey(srcCol), null, "metal_brushed_normal", srcCol, 0.56f, 0.30f, new Vector2(2.0f, 2.0f), metallic: 0.72f);
            if (rawMatName.StartsWith("OW_GoldBrass", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("PolishedBrass", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("ShellBrass", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("Brass_" + ColorKey(srcCol), null, "metal_brushed_normal", srcCol, 0.80f, 0.18f, new Vector2(2.0f, 2.0f), metallic: 0.86f);
            if (rawMatName.StartsWith("OW_StoneWhite", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("LimestoneCarved", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("MarbleLight", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("StoneLight_" + ColorKey(srcCol), null, "ph_cobblestone_normal", srcCol, 0.36f, 0.28f, new Vector2(1.4f, 1.4f), metallic: 0.02f);

            // Phase 4 Emissive Lights, Screens & Glowing Fixtures
            if (rawMatName.StartsWith("OW_HeadlightGlow", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("Headlights", StringComparison.OrdinalIgnoreCase))
                return GetOrCreateEmissiveMaterial("HeadlightGlow", new Color(1.0f, 0.95f, 0.80f), new Color(1.0f, 0.90f, 0.66f) * 1.6f, smoothness: 0.90f);
            if (rawMatName.StartsWith("OW_TailGlow", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("TailLights", StringComparison.OrdinalIgnoreCase))
                return GetOrCreateEmissiveMaterial("TailGlow", new Color(0.92f, 0.10f, 0.08f), new Color(0.95f, 0.12f, 0.08f) * 1.6f, smoothness: 0.86f);
            if (rawMatName.StartsWith("OW_WarmLampGlow", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("WarmLanternGlow", StringComparison.OrdinalIgnoreCase))
                return GetOrCreateEmissiveMaterial("WarmLampGlow", new Color(1.0f, 0.91f, 0.72f), new Color(1.0f, 0.84f, 0.52f) * 1.9f, smoothness: 0.72f);
            if (rawMatName.StartsWith("OW_AquariumGlow", StringComparison.OrdinalIgnoreCase))
                return GetOrCreateEmissiveMaterial("AquariumGlow", new Color(0.28f, 0.78f, 0.88f), new Color(0.20f, 0.74f, 0.86f) * 1.25f, smoothness: 0.92f);
            if (rawMatName.StartsWith("OW_CharcoalEmber", StringComparison.OrdinalIgnoreCase))
                return GetOrCreateEmissiveMaterial("CharcoalEmber", new Color(0.22f, 0.07f, 0.05f), new Color(1.0f, 0.30f, 0.06f) * 1.55f, smoothness: 0.12f);
            if (rawMatName.StartsWith("MetroLedGlow", StringComparison.OrdinalIgnoreCase))
            {
                Color warm = new Color(1.0f, 0.96f, 0.86f, 1f);
                return GetOrCreateEmissiveMaterial("MetroLedGlow", warm, warm * 2.6f, smoothness: 0.65f);
            }

            // Phase 4 Animals — Short Mammalian Fur (Cow, Dogs, Goat, Cat)
            // Keep rawMatName in cache key so TintMaterialByKeyword("CatFurPrimary"/"CowSpot"/"CowWhite") always matches!
            if (rawMatName.StartsWith("OW_CowWhite", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_CowSpot", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("Muzzle", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_DogKangal", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_DogBelly", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_DogBlackMask", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("GoatCoat", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("CatFur", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial(rawMatName + "_" + ColorKey(srcCol), null, "animal_fur_short_normal", srcCol, 0.16f, 0.62f, new Vector2(2.0f, 2.0f), metallic: 0.0f);

            // Phase 4 Animals — Layered Bird Feathers (Chicken, Pigeon, PondDuck, LakeSwan)
            if (rawMatName.StartsWith("OW_ChickenFeather", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_ChickenWing", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_ChickenTail", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("PigeonSlate", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("PigeonWingBar", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_DuckWhite", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_DuckWing", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_DuckCollar", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("SwanPlumage", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial(rawMatName + "_" + ColorKey(srcCol), null, "animal_feather_normal", srcCol, 0.24f, 0.66f, new Vector2(2.0f, 2.0f), metallic: 0.0f);

            if (rawMatName.StartsWith("PigeonIridescent", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_DuckEmerald", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial(rawMatName + "_" + ColorKey(srcCol), null, "animal_feather_normal", srcCol, 0.48f, 0.58f, new Vector2(2.0f, 2.0f), metallic: 0.18f);

            // Phase 4 Animals — Iridescent Fish Scales (LakeFish)
            if (rawMatName.StartsWith("OW_FishScale", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("FishScale", "animal_fish_scales_albedo", "animal_fish_scales_normal", new Color(0.92f, 0.96f, 1.0f), 0.78f, 0.78f, new Vector2(1.5f, 1.5f), metallic: 0.28f);
            if (rawMatName.StartsWith("OW_FishBelly", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("FishBelly", null, "animal_fish_scales_normal", new Color(0.88f, 0.91f, 0.94f), 0.72f, 0.45f, new Vector2(1.5f, 1.5f), metallic: 0.18f);

            // Phase 4 Vehicles — Metallic Clearcoat Paint & Tactile Saddle/Wheel Leather
            if (rawMatName.StartsWith("OW_CarPaint", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial(rawMatName + "_" + ColorKey(srcCol), null, "tractor_paint_normal", srcCol, 0.82f, 0.06f, new Vector2(2.0f, 2.0f), metallic: 0.42f);
            if (rawMatName.StartsWith("OW_BikePaint", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_LiftBlue", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_ToolChestRed", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_TealAccent", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial(rawMatName + "_" + ColorKey(srcCol), null, "tractor_paint_normal", srcCol, 0.74f, 0.08f, new Vector2(2.0f, 2.0f), metallic: 0.34f);
            if (rawMatName.StartsWith("OW_LeatherDark", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("WheelLeather", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial(rawMatName + "_" + ColorKey(srcCol), null, "fabric_linen_normal", srcCol, 0.42f, 0.34f, new Vector2(2.5f, 2.5f), metallic: 0.0f);

            // Phase 4 Furniture & Architecture — Natural Wood Grain & Woven Fabrics / Kilims
            if (rawMatName.StartsWith("OW_TimberOak", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_TimberDark", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_WalnutWood", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("WarmOakWood", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("DarkWalnut", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("RangeTimber", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("WalnutStock", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("IstanbulBenchTealWood", StringComparison.OrdinalIgnoreCase))
            {
                Color woodTint = Color.Lerp(Color.white, srcCol, 0.52f);
                return GetOrCreatePbrSurfaceMaterial(rawMatName + "_" + ColorKey(woodTint), "wood_oak_albedo", "wood_oak_normal", woodTint, 0.28f, 0.72f, new Vector2(1.4f, 1.4f), metallic: 0.0f);
            }

            if (rawMatName.StartsWith("OW_SofaVelvetTeal", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_CushionGold", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_KilimRed", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_KilimCream", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("IstanbulCream", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("IstanbulCrimson", StringComparison.OrdinalIgnoreCase))
            {
                Color fabTint = new Color(Mathf.Min(1f, srcCol.r * 1.12f), Mathf.Min(1f, srcCol.g * 1.12f), Mathf.Min(1f, srcCol.b * 1.12f), 1f);
                return GetOrCreatePbrSurfaceMaterial(rawMatName + "_" + ColorKey(fabTint), "fabric_linen_albedo", "fabric_linen_normal", fabTint, 0.14f, 0.64f, new Vector2(2.2f, 2.2f), metallic: 0.0f);
            }

            // Phase 4 Botanical Crops, Shrubs & Mossy Limestone Rocks
            if (rawMatName.StartsWith("OW_BushLeafA", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_BushLeafB", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_CropLeaf", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("OW_OliveLeaf", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("LeafDeepOlive", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.StartsWith("LeafMidEmerald", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.Equals("Green", StringComparison.OrdinalIgnoreCase) ||
                rawMatName.Equals("DarkGreen2", StringComparison.OrdinalIgnoreCase))
            {
                Color leafTint = Color.Lerp(Color.white, new Color(
                    Mathf.Clamp01(srcCol.r * 1.6f),
                    Mathf.Clamp01(srcCol.g * 1.45f),
                    Mathf.Clamp01(srcCol.b * 1.5f), 1f), 0.45f);
                return GetOrCreatePbrSurfaceMaterial(rawMatName + "_" + ColorKey(leafTint), "crop_leaf_albedo", "crop_leaf_normal", leafTint, 0.18f, 0.72f, new Vector2(1.8f, 1.8f), metallic: 0.0f);
            }

            if (rawMatName.StartsWith("OW_RockLimestone", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("RockLimestone", "ph_cobblestone_albedo", "ph_cobblestone_normal", new Color(0.84f, 0.82f, 0.76f), 0.15f, 1.10f, new Vector2(1.2f, 1.2f), metallic: 0.0f);

            return null;
        }

        private static void EnsureUrpMaterials(GameObject root)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                Material[] shared = r.sharedMaterials;
                bool needsReplace = false;
                for (int m = 0; m < shared.Length; m++)
                {
                    Material mat = shared[m];
                    if (mat == null || mat.shader == null ||
                        mat.shader.name.Contains("Error") ||
                        mat.shader.name == "Standard" ||
                        ResolveSpecialRealismMaterial(mat) != null)
                    {
                        needsReplace = true;
                        break;
                    }
                }

                if (!needsReplace) continue;

                Material[] fixedMats = new Material[shared.Length];
                for (int m = 0; m < shared.Length; m++)
                {
                    Material src = shared[m];
                    Material special = ResolveSpecialRealismMaterial(src);
                    if (special != null)
                    {
                        fixedMats[m] = special;
                        continue;
                    }

                    if (src != null && src.shader != null &&
                        !src.shader.name.Contains("Error") &&
                        src.shader.name != "Standard")
                    {
                        fixedMats[m] = src;
                        continue;
                    }

                    Color col = ExtractSourceColor(src);
                    Material clean = new Material(urpLit) { name = src != null ? src.name : "URP_Fallback" };
                    clean.SetColor(BaseColorId, col);
                    if (clean.HasProperty(SmoothnessId)) clean.SetFloat(SmoothnessId, 0.35f);
                    fixedMats[m] = clean;
                }
                r.sharedMaterials = fixedMats;
            }
        }
    }
}
