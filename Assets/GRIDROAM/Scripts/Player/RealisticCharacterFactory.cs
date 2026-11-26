using System;
using System.Collections.Generic;
using UnityEngine;

namespace OurWorld.Player
{
    public enum CharacterArchetype
    {
        CasualMale = 0,
        CasualFemale = 1,
        Worker = 2,
        Child = 3,
        BossKuzgun = 4
    }

    /// <summary>
    /// Runtime factory that assembles realistic, multi-part Blender 5.3.0 articulated humanoids
    /// (CasualMale, CasualFemale, Worker, Child with hoodie+backpack, and Boss Kuzgun Bora in leather trenchcoat)
    /// from Resources/OurWorldCharacters/ and maps the live scene's PBR normal-mapped Cotton, Denim, Hair,
    /// Skin, Leather, Chrome, Eye & Lip materials onto every spawned citizen, neighbor, gang thug & Boss (0 MB extra textures!).
    /// </summary>
    public static class RealisticCharacterFactory
    {
        private const string ResourceRoot = "OurWorldCharacters/";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        // Authentic diverse Istanbul skin & hair palettes
        public static readonly Color[] DiverseSkinPalette =
        {
            new Color(0.92f, 0.76f, 0.64f), // Fair warm
            new Color(0.87f, 0.69f, 0.56f), // Warm Mediterranean
            new Color(0.82f, 0.63f, 0.49f), // Sunlit olive
            new Color(0.75f, 0.56f, 0.42f), // Tanned Anatolian
            new Color(0.68f, 0.49f, 0.36f)  // Deep bronze
        };

        public static readonly Color[] DiverseHairPalette =
        {
            new Color(0.09f, 0.08f, 0.07f), // Jet black
            new Color(0.16f, 0.11f, 0.08f), // Dark espresso
            new Color(0.28f, 0.17f, 0.10f), // Chestnut brown
            new Color(0.44f, 0.28f, 0.15f), // Warm hazelnut
            new Color(0.62f, 0.48f, 0.28f)  // Honey blonde
        };

        public static readonly Color SilverElderHair = new Color(0.82f, 0.84f, 0.87f);

        private static readonly Dictionary<CharacterArchetype, Dictionary<string, Transform>> CachedPartMaps
            = new Dictionary<CharacterArchetype, Dictionary<string, Transform>>(5);
        private static readonly Dictionary<string, Material> BaseTemplateMats = new Dictionary<string, Material>(16);
        private static readonly Dictionary<string, Material> TintedMatCache = new Dictionary<string, Material>(96);
        private static bool _templateMatsHarvested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CachedPartMaps.Clear();
            BaseTemplateMats.Clear();
            TintedMatCache.Clear();
            _templateMatsHarvested = false;
        }

        private static string GetResourceName(CharacterArchetype archetype)
        {
            switch (archetype)
            {
                case CharacterArchetype.CasualFemale: return "Character_CasualFemale";
                case CharacterArchetype.Worker:       return "Character_Worker";
                case CharacterArchetype.Child:        return "Character_Child";
                case CharacterArchetype.BossKuzgun:   return "Character_BossKuzgun";
                default:                              return "Character_CasualMale";
            }
        }

        private static Dictionary<string, Transform> GetOrLoadPartMap(CharacterArchetype archetype)
        {
            if (CachedPartMaps.TryGetValue(archetype, out Dictionary<string, Transform> existing))
            {
                return existing;
            }

            GameObject fbx = Resources.Load<GameObject>(ResourceRoot + GetResourceName(archetype));
            if (fbx == null)
            {
                CachedPartMaps[archetype] = null;
                return null;
            }

            var map = new Dictionary<string, Transform>(16);
            foreach (Transform ch in fbx.transform)
            {
                map[ch.name] = ch;
            }
            CachedPartMaps[archetype] = map;
            return map;
        }

        private static void EnsurePbrTemplateMaterialsHarvested(ProceduralHumanoid fallbackTemplate)
        {
            if (_templateMatsHarvested && BaseTemplateMats.Count >= 4) return;
            _templateMatsHarvested = true;

            ProceduralHumanoid srcHum = fallbackTemplate ?? PlayerModeController.Instance?.FootController?.Humanoid;
            if (srcHum == null) return;

            Renderer[] rends = srcHum.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                Material[] mats = rends[i].sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null) continue;
                    string n = mat.name;
                    if (n.Contains("Char_Skin") && !BaseTemplateMats.ContainsKey("CharSkin")) BaseTemplateMats["CharSkin"] = mat;
                    else if (n.Contains("Char_Shirt") && !BaseTemplateMats.ContainsKey("CharShirt")) BaseTemplateMats["CharShirt"] = mat;
                    else if (n.Contains("Char_Undershirt") && !BaseTemplateMats.ContainsKey("CharUndershirt")) BaseTemplateMats["CharUndershirt"] = mat;
                    else if (n.Contains("Char_Pants") && !BaseTemplateMats.ContainsKey("CharPants")) BaseTemplateMats["CharPants"] = mat;
                    else if (n.Contains("Char_Belt") && !BaseTemplateMats.ContainsKey("CharBelt")) BaseTemplateMats["CharBelt"] = mat;
                    else if ((n.Contains("Chrome") || n.Contains("Buckle")) && !BaseTemplateMats.ContainsKey("CharBuckle")) BaseTemplateMats["CharBuckle"] = mat;
                    else if (n.Contains("Char_ShoeSole") && !BaseTemplateMats.ContainsKey("CharShoeSole")) BaseTemplateMats["CharShoeSole"] = mat;
                    else if (n.Contains("Char_Shoes") && !BaseTemplateMats.ContainsKey("CharShoes")) BaseTemplateMats["CharShoes"] = mat;
                    else if (n.Contains("Char_Hair") && !BaseTemplateMats.ContainsKey("CharHair")) BaseTemplateMats["CharHair"] = mat;
                    else if (n.Contains("Char_EyeWhite") && !BaseTemplateMats.ContainsKey("CharEyeWhite")) BaseTemplateMats["CharEyeWhite"] = mat;
                    else if (n.Contains("Char_EyeIris") && !BaseTemplateMats.ContainsKey("CharEyeIris")) BaseTemplateMats["CharEyeIris"] = mat;
                    else if (n.Contains("Char_Lips") && !BaseTemplateMats.ContainsKey("CharLips")) BaseTemplateMats["CharLips"] = mat;
                }
            }
        }

        private static Material GetTintedPbrMaterial(string slotKey, Color tint, float? smoothnessOverride = null, float? metallicOverride = null)
        {
            Color32 c32 = tint;
            int smKey = smoothnessOverride.HasValue ? Mathf.RoundToInt(smoothnessOverride.Value * 100f) : -1;
            int mtKey = metallicOverride.HasValue ? Mathf.RoundToInt(metallicOverride.Value * 100f) : -1;
            string cacheKey = $"{slotKey}_{c32.r:X2}{c32.g:X2}{c32.b:X2}_{smKey}_{mtKey}";
            if (TintedMatCache.TryGetValue(cacheKey, out Material cached) && cached != null)
            {
                return cached;
            }

            Material mat;
            if (BaseTemplateMats.TryGetValue(slotKey, out Material baseTmpl) && baseTmpl != null)
            {
                // Preserves real PBR Albedo + Normal Map (Cotton, Denim, Skin pores, Hair strands!)
                mat = new Material(baseTmpl) { name = $"MAT_PBR_{cacheKey}" };
            }
            else
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(lit) { name = $"MAT_PBR_{cacheKey}" };
            }

            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, tint);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, tint);
            if (smoothnessOverride.HasValue && mat.HasProperty(SmoothnessId)) mat.SetFloat(SmoothnessId, smoothnessOverride.Value);
            if (metallicOverride.HasValue && mat.HasProperty(MetallicId)) mat.SetFloat(MetallicId, metallicOverride.Value);

            TintedMatCache[cacheKey] = mat;
            return mat;
        }

        private static Material ResolveSlotMaterial(
            string rawSlotName,
            Material shirtMat,
            Material pantsMat,
            Material skinMat,
            Material hairMat,
            Material buckleMat,
            Material undershirtMat,
            Material beltMat)
        {
            if (rawSlotName.StartsWith("CharSkin", StringComparison.OrdinalIgnoreCase)) return skinMat;
            if (rawSlotName.StartsWith("CharShirt", StringComparison.OrdinalIgnoreCase)) return shirtMat;
            if (rawSlotName.StartsWith("CharUndershirt", StringComparison.OrdinalIgnoreCase)) return undershirtMat;
            if (rawSlotName.StartsWith("CharPants", StringComparison.OrdinalIgnoreCase)) return pantsMat;
            if (rawSlotName.StartsWith("CharBelt", StringComparison.OrdinalIgnoreCase)) return beltMat;
            if (rawSlotName.StartsWith("CharBuckle", StringComparison.OrdinalIgnoreCase)) return buckleMat;
            if (rawSlotName.StartsWith("CharShoeSole", StringComparison.OrdinalIgnoreCase))
                return BaseTemplateMats.TryGetValue("CharShoeSole", out Material s) ? s : GetTintedPbrMaterial("CharShoeSole", new Color(0.92f, 0.91f, 0.88f), 0.25f);
            if (rawSlotName.StartsWith("CharShoes", StringComparison.OrdinalIgnoreCase))
                return BaseTemplateMats.TryGetValue("CharShoes", out Material sh) ? sh : GetTintedPbrMaterial("CharShoes", new Color(0.14f, 0.13f, 0.13f), 0.34f);
            if (rawSlotName.StartsWith("CharHair", StringComparison.OrdinalIgnoreCase)) return hairMat;
            if (rawSlotName.StartsWith("CharEyeWhite", StringComparison.OrdinalIgnoreCase))
                return BaseTemplateMats.TryGetValue("CharEyeWhite", out Material ew) ? ew : GetTintedPbrMaterial("CharEyeWhite", new Color(0.95f, 0.95f, 0.94f), 0.72f);
            if (rawSlotName.StartsWith("CharEyeIris", StringComparison.OrdinalIgnoreCase))
                return BaseTemplateMats.TryGetValue("CharEyeIris", out Material ei) ? ei : GetTintedPbrMaterial("CharEyeIris", new Color(0.08f, 0.07f, 0.06f), 0.92f, 0.10f);
            if (rawSlotName.StartsWith("CharLips", StringComparison.OrdinalIgnoreCase))
                return BaseTemplateMats.TryGetValue("CharLips", out Material lp) ? lp : GetTintedPbrMaterial("CharLips", new Color(0.70f, 0.44f, 0.40f), 0.45f);
            return shirtMat;
        }

        /// <summary>
        /// Builds an articulated 14-pivot Blender 5.3.0 realistic humanoid with live PBR normal-mapped
        /// Cotton/Denim/Skin/Hair/Leather materials. Falls back cleanly to cloning fallbackTemplate if needed.
        /// </summary>
        public static ProceduralHumanoid BuildCharacter(
            Transform parent,
            CharacterArchetype archetype,
            ProceduralHumanoid fallbackTemplate,
            Color shirtColor,
            Color pantsColor,
            Color hairColor,
            Color skinColor,
            float height = 1.78f,
            bool leatherCoat = false,
            bool goldAccents = false,
            Color? backpackOrBeltColor = null)
        {
            EnsurePbrTemplateMaterialsHarvested(fallbackTemplate);
            Dictionary<string, Transform> partMap = GetOrLoadPartMap(archetype);
            if (partMap == null || partMap.Count == 0)
            {
                return fallbackTemplate != null ? UnityEngine.Object.Instantiate(fallbackTemplate, parent) : null;
            }

            Material shirtMat = GetTintedPbrMaterial("CharShirt", shirtColor, leatherCoat ? 0.56f : (float?)null, leatherCoat ? 0.08f : (float?)null);
            Material pantsMat = GetTintedPbrMaterial("CharPants", pantsColor);
            Material skinMat  = GetTintedPbrMaterial("CharSkin",  skinColor, 0.36f);
            Material hairMat  = GetTintedPbrMaterial("CharHair",  hairColor, 0.34f);
            Material buckleMat = goldAccents
                ? GetTintedPbrMaterial("CharBuckle", new Color(0.95f, 0.76f, 0.22f), 0.86f, 0.92f)
                : (BaseTemplateMats.TryGetValue("CharBuckle", out Material b) ? b : GetTintedPbrMaterial("CharBuckle", new Color(0.82f, 0.84f, 0.86f), 0.82f, 0.88f));
            Material undershirtMat = archetype == CharacterArchetype.BossKuzgun
                ? GetTintedPbrMaterial("CharUndershirt", new Color(0.16f, 0.17f, 0.20f), 0.22f)
                : (BaseTemplateMats.TryGetValue("CharUndershirt", out Material u) ? u : GetTintedPbrMaterial("CharUndershirt", new Color(0.91f, 0.90f, 0.87f), 0.20f));
            Material beltMat = backpackOrBeltColor.HasValue
                ? GetTintedPbrMaterial("CharBelt", backpackOrBeltColor.Value, 0.34f)
                : (BaseTemplateMats.TryGetValue("CharBelt", out Material blt) ? blt : GetTintedPbrMaterial("CharBelt", new Color(0.18f, 0.11f, 0.07f), 0.45f));

            bool narrowFrame = archetype == CharacterArchetype.CasualFemale || archetype == CharacterArchetype.Child;
            float shldrX = narrowFrame ? 0.174f : (archetype == CharacterArchetype.BossKuzgun ? 0.212f : 0.200f);
            float hipX = narrowFrame ? 0.102f : 0.106f;

            ProceduralHumanoid hum = ProceduralHumanoid.BuildWithCustomParts(
                parent,
                (partName, pivot) =>
                {
                    if (!partMap.TryGetValue(partName, out Transform srcPart) || srcPart == null)
                    {
                        return null;
                    }

                    GameObject partInst = UnityEngine.Object.Instantiate(srcPart.gameObject, pivot, false);
                    partInst.name = partName;
                    partInst.transform.localPosition = Vector3.zero;
                    partInst.transform.localRotation = Quaternion.identity;
                    partInst.transform.localScale = Vector3.one;

                    Collider[] cols = partInst.GetComponentsInChildren<Collider>(true);
                    for (int c = 0; c < cols.Length; c++) UnityEngine.Object.Destroy(cols[c]);

                    Renderer[] rends = partInst.GetComponentsInChildren<Renderer>(true);
                    for (int r = 0; r < rends.Length; r++)
                    {
                        Material[] srcMats = rends[r].sharedMaterials;
                        Material[] mapped = new Material[srcMats.Length];
                        for (int m = 0; m < srcMats.Length; m++)
                        {
                            string slot = srcMats[m] != null ? srcMats[m].name : string.Empty;
                            mapped[m] = ResolveSlotMaterial(slot, shirtMat, pantsMat, skinMat, hairMat, buckleMat, undershirtMat, beltMat);
                        }
                        rends[r].sharedMaterials = mapped;
                    }

                    return partInst.transform;
                },
                height: height,
                shoulderX: shldrX,
                hipX: hipX,
                shoulderY: 0.382f);

            return hum;
        }
    }
}
