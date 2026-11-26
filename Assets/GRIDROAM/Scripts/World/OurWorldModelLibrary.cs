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

        private static Material GetOrCreatePbrSurfaceMaterial(string key, string albedoName, string normalName, Color tint, float smoothness, float bumpScale, Vector2 tiling)
        {
            if (SharedSpecialMats.TryGetValue(key, out Material cached) && cached != null) return cached;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(urpLit) { name = "MAT_OW_" + key };

            Texture2D alb = Resources.Load<Texture2D>(ThemeTexRoot + albedoName);
            Texture2D nor = Resources.Load<Texture2D>(ThemeTexRoot + normalName);
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

            SharedSpecialMats[key] = mat;
            return mat;
        }

        private static Material ResolveSpecialRealismMaterial(string rawMatName)
        {
            if (string.IsNullOrEmpty(rawMatName)) return null;

            if (rawMatName.StartsWith("FoliagePlaneB", StringComparison.OrdinalIgnoreCase)) return GetFoliagePlaneMaterial(altTint: true);
            if (rawMatName.StartsWith("FoliagePlane", StringComparison.OrdinalIgnoreCase))  return GetFoliagePlaneMaterial(altTint: false);
            if (rawMatName.StartsWith("FoliagePine", StringComparison.OrdinalIgnoreCase))   return GetFoliagePineMaterial();
            if (rawMatName.StartsWith("FoliageCypress", StringComparison.OrdinalIgnoreCase)) return GetFoliageCypressMaterial();
            if (rawMatName.StartsWith("BarkTree", StringComparison.OrdinalIgnoreCase))      return GetBarkTreeMaterial();
            if (rawMatName.StartsWith("MetroGlass", StringComparison.OrdinalIgnoreCase))    return GetTransparentArchitecturalGlassMaterial();

            if (rawMatName.StartsWith("CastleStoneWall", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("CastleStoneWall", "ph_cobblestone_albedo", "ph_cobblestone_normal", new Color(0.84f, 0.78f, 0.68f), 0.16f, 1.30f, new Vector2(0.85f, 0.85f));
            if (rawMatName.StartsWith("CastleStoneDark", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("CastleStoneDark", "ph_cobblestone_albedo", "ph_cobblestone_normal", new Color(0.60f, 0.56f, 0.50f), 0.18f, 1.35f, new Vector2(0.95f, 0.95f));
            if (rawMatName.StartsWith("CastleWoodOak", StringComparison.OrdinalIgnoreCase))
                return GetOrCreatePbrSurfaceMaterial("CastleWoodOak", "ph_woodplanks_albedo", "ph_woodplanks_normal", new Color(0.76f, 0.58f, 0.42f), 0.24f, 1.05f, new Vector2(0.85f, 0.85f));

            if (rawMatName.StartsWith("MetroLedGlow", StringComparison.OrdinalIgnoreCase))
            {
                if (SharedSpecialMats.TryGetValue("MetroLedGlow", out Material cachedLed) && cachedLed != null) return cachedLed;
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material led = new Material(urpLit) { name = "MAT_OW_MetroLedGlow" };
                Color warm = new Color(1.0f, 0.96f, 0.86f, 1f);
                if (led.HasProperty(BaseColorId)) led.SetColor(BaseColorId, warm);
                if (led.HasProperty(EmissionColorId))
                {
                    led.EnableKeyword("_EMISSION");
                    led.SetColor(EmissionColorId, warm * 2.6f);
                }
                SharedSpecialMats["MetroLedGlow"] = led;
                return led;
            }

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
                        ResolveSpecialRealismMaterial(mat.name) != null)
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
                    Material special = src != null ? ResolveSpecialRealismMaterial(src.name) : null;
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

                    Color col = Color.white;
                    if (src != null)
                    {
                        if (src.HasProperty(BaseColorId)) col = src.GetColor(BaseColorId);
                        else if (src.HasProperty(ColorId)) col = src.GetColor(ColorId);
                    }
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
