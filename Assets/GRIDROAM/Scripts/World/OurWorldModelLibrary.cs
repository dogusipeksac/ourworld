using System.Collections.Generic;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Runtime loader and cache for all commercial-safe custom Blender FBX models stored under
    /// Assets/GRIDROAM/Resources/OurWorldLife/.
    /// Provides fast recursive pivot lookup for articulated procedural animations and ensures
    /// URP-safe materials across Editor, iOS, and Android builds.
    /// </summary>
    public static class OurWorldModelLibrary
    {
        private const string ResourceRoot = "OurWorldLife/";
        private static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>(24);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

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

            GameObject inst = Object.Instantiate(prefab, parent, false);
            inst.name = modelName;
            inst.transform.localPosition = localPos;
            inst.transform.localRotation = localRot;
            inst.transform.localScale = localScale ?? Vector3.one;

            if (removeColliders)
            {
                Collider[] cols = inst.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] != null) Object.Destroy(cols[i]);
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
                        mat.name.IndexOf(materialNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
                        if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
                        changed = true;
                    }
                }
                if (changed) r.materials = mats;
            }
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
                        mat.shader.name == "Standard")
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
                    if (clean.HasProperty("_Smoothness")) clean.SetFloat("_Smoothness", 0.35f);
                    fixedMats[m] = clean;
                }
                r.sharedMaterials = fixedMats;
            }
        }
    }
}
