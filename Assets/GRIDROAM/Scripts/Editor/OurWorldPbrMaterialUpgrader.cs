using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OurWorld.Editor
{
    /// <summary>
    /// Upgrades URP Lit PBR materials under Assets/GRIDROAM/Art/Materials by:
    /// 1. Packing ambientCG Roughness (+ optional Metalness) into URP linear RGBA32 MetalSmooth maps (R = Metalness, A = 1 - Roughness).
    /// 2. Binding NormalGL, MetalSmooth (_MetallicGlossMap), and AmbientOcclusion (_OcclusionMap) with proper URP shader keywords.
    /// 3. Generating a tileable neutral-mean (128, ±22% contrast) macro variation detail texture and applying it to large ground/road/paving surfaces.
    /// </summary>
    public static class OurWorldPbrMaterialUpgrader
    {
        private const string TexturesRoot = "Assets/GRIDROAM/Art/Textures";
        private const string MaterialsRoot = "Assets/GRIDROAM/Art/Materials";
        private const string DetailDir = TexturesRoot + "/Detail";
        private const string MacroVariationAssetPath = DetailDir + "/Detail_MacroVariation.png";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        private const int MacroTextureSize = 512;
        private const float MacroHalfRange = 28f; // 28 / 128 ~= 21.9% contrast -> [100, 156], mean 128

        private static readonly string[] IgnoredTextureFolders =
        {
            "Weathering",
            "RealismPBR",
            "Detail"
        };

        [MenuItem("OurWorld/Realism/Upgrade PBR Materials")]
        public static void UpgradeAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsRoot });
            int updatedCount = 0;
            int macroCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string matPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(matPath)) continue;

                string normalizedPath = matPath.Replace('\\', '/');
                if (!normalizedPath.StartsWith(MaterialsRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(normalizedPath);
                if (mat == null || mat.shader == null) continue;
                if (!string.Equals(mat.shader.name, UrpLitShaderName, StringComparison.Ordinal)) continue;

                if (!TryExtractTextureSetName(mat, out string setName))
                {
                    continue;
                }

                ApplyPbrMaps(mat, setName);
                updatedCount++;

                if (ShouldApplyMacroVariation(mat.name, setName))
                {
                    ApplyMacroVariation(mat);
                    macroCount++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[OurWorldPbrMaterialUpgrader] {updatedCount} malzeme güncellendi, {macroCount} makro varyasyon");
        }

        /// <summary>
        /// Generates (or reuses if up-to-date) <c>&lt;Set&gt;_MetalSmooth.png</c> in <c>Assets/GRIDROAM/Art/Textures/&lt;Set&gt;/</c>
        /// with R = Metalness (0 if absent), G = 0, B = 0, A = 1 - Roughness, at Roughness texture resolution.
        /// Reads source textures via RenderTexture + Graphics.Blit + ReadPixels without modifying source importers.
        /// </summary>
        public static Texture2D EnsureMetalSmoothMap(string setName)
        {
            if (string.IsNullOrEmpty(setName)) return null;

            string setDir = $"{TexturesRoot}/{setName}";
            if (!Directory.Exists(setDir)) return null;

            string roughPath = FindSetTexturePath(setDir, "_Roughness");
            if (string.IsNullOrEmpty(roughPath)) return null;

            string metalPath = FindSetTexturePath(setDir, "_Metalness");
            string outputPath = $"{setDir}/{setName}_MetalSmooth.png";

            if (File.Exists(outputPath))
            {
                DateTime outTime = File.GetLastWriteTimeUtc(outputPath);
                bool upToDate = outTime >= File.GetLastWriteTimeUtc(roughPath);
                if (upToDate && !string.IsNullOrEmpty(metalPath) && File.Exists(metalPath))
                {
                    upToDate = outTime >= File.GetLastWriteTimeUtc(metalPath);
                }

                if (upToDate)
                {
                    Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
                    if (existing != null)
                    {
                        return existing;
                    }
                }
            }

            Texture2D roughTex = AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
            if (roughTex == null) return null;

            Texture2D metalTex = !string.IsNullOrEmpty(metalPath)
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(metalPath)
                : null;

            int width = roughTex.width;
            int height = roughTex.height;
            if (width <= 0 || height <= 0) return null;

            Color[] roughPixels = ReadTexturePixelsRaw(roughTex, width, height);
            Color[] metalPixels = metalTex != null ? ReadTexturePixelsRaw(metalTex, width, height) : null;

            int pixelCount = width * height;
            Color32[] packed = new Color32[pixelCount];
            for (int i = 0; i < pixelCount; i++)
            {
                float roughness = Mathf.Clamp01(roughPixels[i].r);
                float smoothness = 1f - roughness;
                float metalness = metalPixels != null ? Mathf.Clamp01(metalPixels[i].r) : 0f;

                byte r = (byte)Mathf.RoundToInt(metalness * 255f);
                byte a = (byte)Mathf.RoundToInt(smoothness * 255f);
                packed[i] = new Color32(r, 0, 0, a);
            }

            Texture2D outTex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            byte[] pngBytes;
            try
            {
                outTex.SetPixels32(packed);
                outTex.Apply(false, false);
                pngBytes = outTex.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(outTex);
            }

            File.WriteAllBytes(outputPath, pngBytes);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; dirty = true; }
                if (importer.sRGBTexture) { importer.sRGBTexture = false; dirty = true; }
                if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; dirty = true; }
                if (importer.alphaSource != TextureImporterAlphaSource.FromInput) { importer.alphaSource = TextureImporterAlphaSource.FromInput; dirty = true; }
                if (importer.alphaIsTransparency) { importer.alphaIsTransparency = false; dirty = true; }
                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
        }

        /// <summary>
        /// Connects NormalGL (if _BumpMap is empty), packed MetalSmooth (_MetallicGlossMap), and AmbientOcclusion (_OcclusionMap)
        /// from <c>Assets/GRIDROAM/Art/Textures/&lt;setName&gt;/</c> onto a URP Lit material and logs the previous _Smoothness value.
        /// </summary>
        public static void ApplyPbrMaps(Material mat, string setName)
        {
            if (mat == null || string.IsNullOrEmpty(setName)) return;

            string setDir = $"{TexturesRoot}/{setName}";
            if (!Directory.Exists(setDir)) return;

            float previousSmoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : -1f;

            if (mat.HasProperty("_BumpMap"))
            {
                if (mat.GetTexture("_BumpMap") == null)
                {
                    string normalPath = FindSetTexturePath(setDir, "_NormalGL");
                    Texture2D normalTex = !string.IsNullOrEmpty(normalPath)
                        ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath)
                        : null;
                    if (normalTex != null)
                    {
                        mat.SetTexture("_BumpMap", normalTex);
                        if (mat.HasProperty("_BaseMap"))
                        {
                            mat.SetTextureScale("_BumpMap", mat.GetTextureScale("_BaseMap"));
                            mat.SetTextureOffset("_BumpMap", mat.GetTextureOffset("_BaseMap"));
                        }
                        mat.EnableKeyword("_NORMALMAP");
                    }
                }
                else
                {
                    mat.EnableKeyword("_NORMALMAP");
                }
            }

            Texture2D metalSmooth = EnsureMetalSmoothMap(setName);
            if (metalSmooth != null)
            {
                if (mat.HasProperty("_MetallicGlossMap"))
                {
                    mat.SetTexture("_MetallicGlossMap", metalSmooth);
                }
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");

                if (mat.HasProperty("_Smoothness"))
                {
                    mat.SetFloat("_Smoothness", 1.0f);
                }
                if (mat.HasProperty("_SmoothnessTextureChannel"))
                {
                    mat.SetFloat("_SmoothnessTextureChannel", 0f);
                }
            }

            string aoPath = FindSetTexturePath(setDir, "_AmbientOcclusion");
            Texture2D aoTex = !string.IsNullOrEmpty(aoPath)
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath)
                : null;
            if (aoTex != null && mat.HasProperty("_OcclusionMap"))
            {
                mat.SetTexture("_OcclusionMap", aoTex);
                if (mat.HasProperty("_OcclusionStrength"))
                {
                    mat.SetFloat("_OcclusionStrength", 0.85f);
                }
                mat.EnableKeyword("_OCCLUSIONMAP");
            }

            Debug.Log(
                $"[OurWorldPbrMaterialUpgrader] {mat.name} (set: {setName}) | önceki _Smoothness={previousSmoothness:F2}, " +
                $"MetalSmooth={(metalSmooth != null ? metalSmooth.name : "yok")}, AO={(aoTex != null ? aoTex.name : "yok")}");

            EditorUtility.SetDirty(mat);
        }

        /// <summary>
        /// Ensures Assets/GRIDROAM/Art/Textures/Detail/Detail_MacroVariation.png exists and binds it as
        /// _DetailAlbedoMap (_DETAIL_MULX2) at _BaseMap tiling / macroTilingDivisor (without detail normal).
        /// </summary>
        public static void ApplyMacroVariation(Material mat, float macroTilingDivisor = 7f)
        {
            if (mat == null) return;
            if (macroTilingDivisor <= 0.001f) macroTilingDivisor = 7f;

            Texture2D detailTex = EnsureMacroVariationTexture();
            if (detailTex == null) return;

            Vector2 baseTiling = mat.HasProperty("_BaseMap") ? mat.GetTextureScale("_BaseMap") : Vector2.one;
            if (Mathf.Abs(baseTiling.x) < 0.0001f) baseTiling.x = 1f;
            if (Mathf.Abs(baseTiling.y) < 0.0001f) baseTiling.y = 1f;

            Vector2 detailTiling = baseTiling / macroTilingDivisor;

            if (mat.HasProperty("_DetailAlbedoMap"))
            {
                mat.SetTexture("_DetailAlbedoMap", detailTex);
                mat.SetTextureScale("_DetailAlbedoMap", detailTiling);
                mat.SetTextureOffset("_DetailAlbedoMap", Vector2.zero);
            }

            if (mat.HasProperty("_DetailAlbedoMapScale"))
            {
                mat.SetFloat("_DetailAlbedoMapScale", 1.0f);
            }

            mat.EnableKeyword("_DETAIL_MULX2");
            mat.DisableKeyword("_DETAIL_SCALED");
            EditorUtility.SetDirty(mat);
        }

        /// <summary>
        /// Generates a 512x512 tileable 4-octave periodic gradient noise texture (largest octave period = 2),
        /// grayscale, exact mean 128 (neutral under URP _DETAIL_MULX2), contrast ±22% (~[100, 156]), sRGB enabled.
        /// </summary>
        public static Texture2D EnsureMacroVariationTexture()
        {
            if (File.Exists(MacroVariationAssetPath))
            {
                Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(MacroVariationAssetPath);
                if (existing != null)
                {
                    return existing;
                }
            }

            if (!Directory.Exists(DetailDir))
            {
                Directory.CreateDirectory(DetailDir);
            }

            const int size = MacroTextureSize;
            int total = size * size;
            float[] values = new float[total];
            double sum = 0.0;

            int[] periods = { 2, 4, 8, 16 };
            float[] amps = { 1.0f, 0.5f, 0.25f, 0.125f };
            uint[] seeds = { 1013u, 2027u, 4057u, 8117u };

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                int rowOffset = y * size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float n = 0f;
                    for (int o = 0; o < periods.Length; o++)
                    {
                        n += PeriodicGradientNoise(u, v, periods[o], seeds[o]) * amps[o];
                    }

                    values[rowOffset + x] = n;
                    sum += n;
                }
            }

            float mean = (float)(sum / total);
            float maxAbs = 0f;
            for (int i = 0; i < total; i++)
            {
                float d = Mathf.Abs(values[i] - mean);
                if (d > maxAbs) maxAbs = d;
            }
            if (maxAbs < 1e-6f) maxAbs = 1f;

            float scale = MacroHalfRange / maxAbs;
            Color32[] pixels = new Color32[total];
            for (int i = 0; i < total; i++)
            {
                int g = Mathf.Clamp(Mathf.RoundToInt(128f + (values[i] - mean) * scale), 100, 156);
                byte b = (byte)g;
                pixels[i] = new Color32(b, b, b, 255);
            }

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            byte[] pngBytes;
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                pngBytes = tex.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }

            File.WriteAllBytes(MacroVariationAssetPath, pngBytes);
            AssetDatabase.ImportAsset(MacroVariationAssetPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(MacroVariationAssetPath) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; dirty = true; }
                if (!importer.sRGBTexture) { importer.sRGBTexture = true; dirty = true; }
                if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; dirty = true; }
                if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; dirty = true; }
                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(MacroVariationAssetPath);
        }

        private static bool TryExtractTextureSetName(Material mat, out string setName)
        {
            setName = null;
            if (mat == null || !mat.HasProperty("_BaseMap")) return false;

            Texture baseTex = mat.GetTexture("_BaseMap");
            if (baseTex == null) return false;

            string texPath = AssetDatabase.GetAssetPath(baseTex);
            if (string.IsNullOrEmpty(texPath)) return false;

            texPath = texPath.Replace('\\', '/');
            string prefix = TexturesRoot + "/";
            if (!texPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string relative = texPath.Substring(prefix.Length);
            int slash = relative.IndexOf('/');
            if (slash <= 0) return false;

            string candidate = relative.Substring(0, slash);
            for (int i = 0; i < IgnoredTextureFolders.Length; i++)
            {
                if (string.Equals(candidate, IgnoredTextureFolders[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            setName = candidate;
            return true;
        }

        private static bool ShouldApplyMacroVariation(string matName, string setName)
        {
            if (!string.IsNullOrEmpty(setName) &&
                (string.Equals(setName, "Grass001", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(setName, "Ground037", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (string.IsNullOrEmpty(matName)) return false;

            return matName.StartsWith("MAT_Road_", StringComparison.Ordinal) ||
                   matName.StartsWith("MAT_Ground_", StringComparison.Ordinal) ||
                   matName.StartsWith("MAT_Sidewalk_", StringComparison.Ordinal) ||
                   matName.StartsWith("MAT_Town_Paving", StringComparison.Ordinal) ||
                   matName.StartsWith("MAT_Harbor_Dock", StringComparison.Ordinal);
        }

        private static string FindSetTexturePath(string setDir, string token)
        {
            if (string.IsNullOrEmpty(setDir) || !Directory.Exists(setDir)) return null;

            string[] files = Directory.GetFiles(setDir, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string p = files[i].Replace('\\', '/');
                if (p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

                string ext = Path.GetExtension(p);
                if (!string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fileName = Path.GetFileNameWithoutExtension(p);
                if (fileName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return p;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads raw normalized [0..1] pixel values from a non-readable Texture2D via Graphics.Blit into a
        /// temporary RenderTexture. Compensates for GPU sRGB-to-Linear decoding when the source texture was
        /// imported with sRGB enabled in a Linear color space project so raw data bytes are preserved accurately.
        /// </summary>
        private static Color[] ReadTexturePixelsRaw(Texture2D source, int width, int height)
        {
            bool needsSrgbUndo = QualitySettings.activeColorSpace == ColorSpace.Linear && source.isDataSRGB;
            bool supportsFloatRt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat);

            RenderTextureFormat rtFormat = supportsFloatRt ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGB32;
            TextureFormat readFormat = supportsFloatRt ? TextureFormat.RGBAFloat : TextureFormat.RGBA32;
            RenderTextureReadWrite readWrite = (!supportsFloatRt && needsSrgbUndo)
                ? RenderTextureReadWrite.sRGB
                : RenderTextureReadWrite.Linear;

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, rtFormat, readWrite);
            RenderTexture prevActive = RenderTexture.active;
            Texture2D readback = null;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                readback = new Texture2D(width, height, readFormat, false, true);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                readback.Apply(false, false);

                Color[] pixels = readback.GetPixels();
                if (supportsFloatRt && needsSrgbUndo)
                {
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i].r = Mathf.LinearToGammaSpace(Mathf.Clamp01(pixels[i].r));
                        pixels[i].g = Mathf.LinearToGammaSpace(Mathf.Clamp01(pixels[i].g));
                        pixels[i].b = Mathf.LinearToGammaSpace(Mathf.Clamp01(pixels[i].b));
                    }
                }

                return pixels;
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                if (readback != null)
                {
                    UnityEngine.Object.DestroyImmediate(readback);
                }
            }
        }

        private static float PeriodicGradientNoise(float u, float v, int period, uint seed)
        {
            float fx = u * period;
            float fy = v * period;

            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;

            int ix0 = ((x0 % period) + period) % period;
            int iy0 = ((y0 % period) + period) % period;
            int ix1 = (ix0 + 1) % period;
            int iy1 = (iy0 + 1) % period;

            float sx = Fade(tx);
            float sy = Fade(ty);

            float n00 = GradDot(ix0, iy0, seed, tx, ty);
            float n10 = GradDot(ix1, iy0, seed, tx - 1f, ty);
            float n01 = GradDot(ix0, iy1, seed, tx, ty - 1f);
            float n11 = GradDot(ix1, iy1, seed, tx - 1f, ty - 1f);

            float nx0 = n00 + sx * (n10 - n00);
            float nx1 = n01 + sx * (n11 - n01);
            return nx0 + sy * (nx1 - nx0);
        }

        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static float GradDot(int ix, int iy, uint seed, float dx, float dy)
        {
            uint h = (uint)(ix * 374761393) ^ (uint)(iy * 668265263) ^ (seed * 1442695041u);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;

            float angle = (h & 0xFFFFu) * (Mathf.PI * 2f / 65536f);
            return Mathf.Cos(angle) * dx + Mathf.Sin(angle) * dy;
        }
    }
}
