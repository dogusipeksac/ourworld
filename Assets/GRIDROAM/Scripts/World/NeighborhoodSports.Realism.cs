using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    public sealed partial class NeighborhoodSports
    {
        private SportsArenaAtmosphere _atmosphere;

        // =====================================================================
        // PROCEDURAL PBR COURT SURFACE TEXTURES (iOS METAL SAFE, OWNED ASSETS)
        // =====================================================================

        private Material MaterialForTextured(
            Shader shader,
            string matName,
            Texture2D tex,
            Color tint,
            float smoothness,
            Vector2 tiling)
        {
            Material mat = MaterialFor(shader, matName, tint, smoothness);
            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.mainTextureScale = tiling;
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTextureScale("_BaseMap", tiling);
                }
            }
            return mat;
        }

        private Material MaterialForTranslucent(Shader shader, string matName, Color color, float smoothness)
        {
            Material mat = MaterialFor(shader, matName, color, smoothness);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }

        private Texture2D BuildTurfTexture(Color baseCol, Color bladeAccent, int seed)
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "TEX_Court_Turf_" + seed,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            Color32[] px = new Color32[size * size];
            System.Random rng = new System.Random(seed);

            for (int y = 0; y < size; y++)
            {
                float mowSheen = Mathf.Sin((y / (float)size) * Mathf.PI * 4f) * 0.025f;
                for (int x = 0; x < size; x++)
                {
                    float n = (float)rng.NextDouble();
                    float blade = ((x + (y & 3) * 7) % 5 == 0) ? 0.035f : -0.015f;
                    Color c = Color.Lerp(baseCol, bladeAccent, n * 0.42f);
                    c.r += mowSheen + blade * 0.5f;
                    c.g += mowSheen + blade;
                    c.b += mowSheen * 0.6f;

                    // Authentic Turkish Halı Saha recycled black rubber infill granules (~6% speckle)
                    if (n > 0.945f)
                    {
                        c = Color.Lerp(c, new Color(0.06f, 0.07f, 0.06f, 1f), 0.52f);
                    }
                    px[y * size + x] = (Color32)ClampColor(c);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            _ownedAssets.Add(tex);
            return tex;
        }

        private Texture2D BuildAcrylicCourtTexture(Color baseCol, int seed)
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "TEX_Court_Acrylic_" + seed,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            Color32[] px = new Color32[size * size];
            System.Random rng = new System.Random(seed);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float grit = ((float)rng.NextDouble() - 0.5f) * 0.085f;
                    float macro = Mathf.Sin(x * 0.07f + y * 0.04f) * Mathf.Cos(y * 0.06f) * 0.024f;
                    Color c = new Color(baseCol.r + grit + macro, baseCol.g + grit + macro, baseCol.b + grit + macro, 1f);
                    // Subtle fine white silica micro-specks for realistic non-slip acrylic court grip
                    if (grit > 0.038f) c = Color.Lerp(c, Color.white, 0.08f);
                    px[y * size + x] = (Color32)ClampColor(c);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            _ownedAssets.Add(tex);
            return tex;
        }

        private Texture2D BuildClayCourtTexture(Color baseCol, int seed)
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "TEX_Court_Clay_" + seed,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            Color32[] px = new Color32[size * size];
            System.Random rng = new System.Random(seed);

            for (int y = 0; y < size; y++)
            {
                // Subtle drag-broom sweep striations across red clay
                float sweep = Mathf.Sin(y * 0.35f) * 0.018f + Mathf.Sin(y * 0.09f) * 0.014f;
                for (int x = 0; x < size; x++)
                {
                    float r1 = (float)rng.NextDouble();
                    float grit = (r1 - 0.5f) * 0.11f;
                    float patch = Mathf.Sin(x * 0.05f) * Mathf.Sin(y * 0.045f + 1.1f) * 0.025f;
                    Color c = new Color(
                        baseCol.r + grit * 1.1f + sweep + patch,
                        baseCol.g + grit * 0.72f + sweep * 0.6f + patch * 0.6f,
                        baseCol.b + grit * 0.52f + sweep * 0.4f,
                        1f);
                    if (r1 > 0.96f) c = Color.Lerp(c, new Color(0.84f, 0.58f, 0.42f, 1f), 0.35f);
                    px[y * size + x] = (Color32)ClampColor(c);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            _ownedAssets.Add(tex);
            return tex;
        }

        private Texture2D BuildSandCourtTexture(Color baseCol, int seed)
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "TEX_Court_Sand_" + seed,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            Color32[] px = new Color32[size * size];
            System.Random rng = new System.Random(seed);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float ripple = Mathf.Sin((y + Mathf.Sin(x * 0.049f) * 8f) * 0.245f) * 0.036f;
                    float grain = ((float)rng.NextDouble() - 0.5f) * 0.085f;
                    Color c = new Color(
                        baseCol.r + ripple + grain,
                        baseCol.g + ripple * 0.92f + grain * 0.9f,
                        baseCol.b + ripple * 0.72f + grain * 0.75f,
                        1f);
                    px[y * size + x] = (Color32)ClampColor(c);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            _ownedAssets.Add(tex);
            return tex;
        }

        private Texture2D BuildAsphaltTexture(Color baseCol, int seed)
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "TEX_Court_Asphalt_" + seed,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            Color32[] px = new Color32[size * size];
            System.Random rng = new System.Random(seed);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = (float)rng.NextDouble();
                    float n = (r - 0.5f) * 0.09f;
                    float speckle = r > 0.93f ? 0.06f : (r < 0.06f ? -0.05f : 0f);
                    Color c = new Color(baseCol.r + n + speckle, baseCol.g + n + speckle, baseCol.b + n + speckle, 1f);
                    px[y * size + x] = (Color32)ClampColor(c);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            _ownedAssets.Add(tex);
            return tex;
        }

        private static Color ClampColor(Color c)
        {
            return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
        }

        // =====================================================================
        // AUTHENTIC TURKISH NEIGHBORHOOD STADIUM ARCHITECTURE HELPERS
        // =====================================================================

        private void AddTallCageFence(
            MeshParts parts,
            Transform court,
            Material postMat,
            Material wireMat,
            Vector3 start,
            Vector3 end,
            float height = 3.55f,
            bool addCollider = true)
        {
            float length = Vector3.Distance(start, end);
            if (length < 0.2f) return;
            Vector3 mid = (start + end) * 0.5f;
            int bays = Mathf.Max(1, Mathf.CeilToInt(length / 2.8f));

            // Heavy perimeter steel posts
            for (int i = 0; i <= bays; i++)
            {
                Vector3 post = Vector3.Lerp(start, end, i / (float)bays);
                parts.Box(postMat, post + Vector3.up * (height * 0.5f), new Vector3(0.085f, height, 0.085f));
            }

            // Horizontal tension bars
            int hCount = height > 3.0f ? 6 : 4;
            for (int h = 0; h <= hCount; h++)
            {
                float y = Mathf.Lerp(0.14f, height - 0.06f, h / (float)hCount);
                float rad = (h == 0 || h == hCount) ? 0.024f : 0.013f;
                parts.Rod(h == 0 || h == hCount ? postMat : wireMat, _cylinder, start + Vector3.up * y, end + Vector3.up * y, rad);
            }

            // Fine vertical chain-link wire uprights (readable silhouette without high poly overhead)
            int wires = Mathf.Clamp(Mathf.CeilToInt(length / 0.62f), 2, 46);
            for (int w = 1; w < wires; w++)
            {
                Vector3 wp = Vector3.Lerp(start, end, w / (float)wires);
                parts.Box(wireMat, wp + Vector3.up * (height * 0.5f), new Vector3(0.012f, height - 0.14f, 0.012f));
            }

            if (addCollider)
            {
                Vector3 size = end - start;
                AddCollider(court, "CageFenceCollision", mid + Vector3.up * (height * 0.5f),
                    new Vector3(Mathf.Max(0.12f, Mathf.Abs(size.x)), height, Mathf.Max(0.12f, Mathf.Abs(size.z))));
            }
        }

        private void AddWindscreenTarp(
            MeshParts parts,
            Material tarpMat,
            Vector3 start,
            Vector3 end,
            float bottomY,
            float topY,
            Vector3 inwardOffset)
        {
            Vector3 delta = end - start;
            float len = delta.magnitude;
            if (len < 0.2f) return;
            Vector3 mid = (start + end) * 0.5f + inwardOffset;
            float h = topY - bottomY;
            Vector3 sz = Mathf.Abs(delta.x) > Mathf.Abs(delta.z)
                ? new Vector3(len - 0.12f, h, 0.024f)
                : new Vector3(0.024f, h, len - 0.12f);
            parts.Box(tarpMat, new Vector3(mid.x, bottomY + h * 0.5f, mid.z), sz);
        }

        private void AddPerimeterAdBoard(
            Transform parent,
            Vector3 localPos,
            float yawDeg,
            float width,
            float height,
            Material boardMat,
            Material trimMat,
            string textLabel,
            Color textColor)
        {
            Transform board = Child(parent, "AdBoard_" + textLabel.Replace(" ", "_"), localPos);
            board.localRotation = Quaternion.Euler(0f, yawDeg, 0f);

            MeshParts p = new MeshParts(this);
            p.Box(boardMat, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0.055f));
            p.Box(trimMat, new Vector3(0f, height + 0.02f, 0f), new Vector3(width + 0.04f, 0.045f, 0.07f));
            p.Box(trimMat, new Vector3(0f, 0.025f, 0f), new Vector3(width + 0.04f, 0.05f, 0.07f));
            p.Emit(board, "BoardPanel", shadows: false);
            AddCollider(board, "AdBoardCollider", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0.12f));

            Transform letters = Child(board, "BoardText", new Vector3(0f, height * 0.5f, -0.034f));
            TextMesh tm = letters.gameObject.AddComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            letters.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.text = textLabel;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 44;
            tm.characterSize = 0.048f;
            tm.fontStyle = FontStyle.Bold;
            tm.color = textColor;
        }

        private void AddStadiumFloodlightTower(
            Transform parent,
            Vector3 baseLocalPos,
            Vector3 aimLocalPoint,
            float height,
            Material poleMat,
            Material housingMat,
            Material lensMat,
            Material nightConeMat)
        {
            Transform tower = Child(parent, "StadiumFloodlightTower", baseLocalPos);
            Vector3 toTarget = aimLocalPoint - baseLocalPos;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                tower.localRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }

            MeshParts p = new MeshParts(this);
            // Concrete plinth footing + tapered vertical steel mast
            p.Box(housingMat, new Vector3(0f, 0.22f, 0f), new Vector3(0.42f, 0.44f, 0.42f));
            p.Rod(poleMat, _cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(0f, height, 0f), 0.095f);
            // Diagonal brace & upper horizontal floodlight crossbar
            p.Rod(poleMat, _cylinder, new Vector3(0f, height - 0.65f, 0f), new Vector3(0f, height, 0.32f), 0.032f);
            p.Box(poleMat, new Vector3(0f, height, 0.28f), new Vector3(1.28f, 0.09f, 0.09f));

            // Twin angled dark LED luminaire housings + bright glowing lenses
            foreach (float lx in new[] { -0.36f, 0.36f })
            {
                Quaternion tilt = Quaternion.Euler(28f, 0f, 0f);
                p.Add(housingMat, _cube, new Vector3(lx, height + 0.06f, 0.36f), tilt, new Vector3(0.46f, 0.24f, 0.16f));
                p.Add(lensMat, _cube, new Vector3(lx, height + 0.02f, 0.44f), tilt, new Vector3(0.40f, 0.18f, 0.04f));
            }
            p.Emit(tower, "FloodlightMast", shadows: false);
            AddCollider(tower, "MastCollider", new Vector3(0f, height * 0.4f, 0f), new Vector3(0.36f, height * 0.8f, 0.36f));

            // Soft translucent volumetric floodlight glow cone (toggled on automatically at night!)
            GameObject coneObj = null;
            if (nightConeMat != null)
            {
                Transform cone = Child(tower, "NightFloodCone", new Vector3(0f, height * 0.56f, 2.35f));
                cone.localRotation = Quaternion.Euler(36f, 0f, 0f);
                cone.localScale = new Vector3(2.8f, height * 0.55f, 2.8f);
                cone.gameObject.AddComponent<MeshFilter>().sharedMesh = _cylinder;
                MeshRenderer mr = cone.gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = nightConeMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                coneObj = cone.gameObject;
                coneObj.SetActive(false);
            }

            _atmosphere?.RegisterFloodlightVisual(lensMat, coneObj);
        }

        private TextMesh AddDigitalScoreboard(
            Transform parent,
            Vector3 localPos,
            string initialText,
            Shader shader,
            Color accentColor,
            Color textColor,
            float width = 2.65f,
            float height = 0.84f)
        {
            Material cabinetMat = MaterialFor(shader, "LED Scoreboard Cabinet", new Color(0.08f, 0.09f, 0.11f), 0.32f);
            Material bezelMat = MaterialFor(shader, "LED Scoreboard Bezel", accentColor, 0.45f);
            bezelMat.EnableKeyword("_EMISSION");
            bezelMat.SetColor("_EmissionColor", accentColor * 0.55f);
            Material postMat = MaterialFor(shader, "Scoreboard Post", new Color(0.24f, 0.27f, 0.30f), 0.45f);

            Transform sign = Child(parent, "LiveScoreboard", localPos);
            sign.localRotation = Quaternion.identity;

            MeshParts parts = new MeshParts(this);
            parts.Box(cabinetMat, Vector3.zero, new Vector3(width, height, 0.14f));
            // Top & bottom glowing LED trim bars
            parts.Box(bezelMat, new Vector3(0f, height * 0.5f + 0.025f, -0.02f), new Vector3(width + 0.06f, 0.055f, 0.16f));
            parts.Box(bezelMat, new Vector3(0f, -height * 0.5f - 0.025f, -0.02f), new Vector3(width + 0.06f, 0.055f, 0.16f));
            // Twin steel support legs anchored cleanly to the ground
            foreach (float px in new[] { -width * 0.34f, width * 0.34f })
            {
                parts.Rod(postMat, _cylinder, new Vector3(px, -localPos.y, 0f), new Vector3(px, -height * 0.5f, 0f), 0.055f);
            }
            parts.Emit(sign, "DigitalScoreboardCabinet", shadows: false);
            AddCollider(sign, "ScoreboardCollider", new Vector3(0f, -localPos.y * 0.5f, 0f), new Vector3(width * 0.85f, localPos.y + height * 0.5f, 0.24f));

            Transform letters = Child(sign, "ScoreboardText", new Vector3(0f, 0f, -0.078f));
            TextMesh text = letters.gameObject.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            letters.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.text = initialText;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.054f;
            text.fontStyle = FontStyle.Bold;
            text.color = textColor;
            return text;
        }

        private void AddCornerFlag(Transform parent, Vector3 localPos, Material poleMat, Material flagMat, float phase)
        {
            Transform root = Child(parent, "CornerFlag", localPos);
            MeshParts pole = new MeshParts(this);
            pole.Rod(poleMat, _cylinder, Vector3.zero, Vector3.up * 1.38f, 0.018f);
            pole.Add(flagMat, _sphere, Vector3.up * 1.40f, Quaternion.identity, Vector3.one * 0.055f);
            pole.Emit(root, "FlagPole", shadows: false);

            Transform pennant = Child(root, "Pennant", new Vector3(0f, 1.24f, 0f));
            MeshParts cloth = new MeshParts(this);
            cloth.Box(flagMat, new Vector3(0.22f, 0f, 0f), new Vector3(0.44f, 0.24f, 0.012f));
            cloth.Emit(pennant, "FlagCloth", shadows: false);

            _atmosphere?.RegisterFlag(pennant, phase);
        }

        private void AddSpectatorBleacher(
            Transform parent,
            Vector3 localPos,
            float yawDeg,
            float length,
            bool covered,
            Shader shader,
            Material steelMat,
            ProceduralHumanoid template,
            string sportKind,
            Transform watchBall,
            string[] spectatorNames,
            Color[] shirtColors)
        {
            Transform bleacher = Child(parent, "SpectatorBleacher_" + sportKind, localPos);
            bleacher.localRotation = Quaternion.Euler(0f, yawDeg, 0f);

            Material woodMat = MaterialFor(shader, "Bleacher Warm Timber", new Color(0.56f, 0.36f, 0.20f), 0.26f);
            Material canopyMat = MaterialFor(shader, "Bleacher Canopy", new Color(0.14f, 0.32f, 0.28f), 0.30f);

            MeshParts p = new MeshParts(this);
            // Concrete base slab (top Y = 0.08m, extended front to Z = +1.04m so Tier 1 feet rest flat on slab)
            p.Box(steelMat, new Vector3(0f, 0.04f, 0.08f), new Vector3(length + 0.24f, 0.08f, 1.92f));
            // Tier 1 seat (front, Z = +0.48, top surface Y = 0.48m)
            p.Box(woodMat, new Vector3(0f, 0.45f, 0.48f), new Vector3(length, 0.06f, 0.40f));
            // Tier 2 wooden footrest deck (Z = +0.06, top surface Y = 0.475m — directly beneath Tier 2 spectators' shoes!)
            p.Box(woodMat, new Vector3(0f, 0.45f, 0.06f), new Vector3(length, 0.05f, 0.34f));
            // Tier 2 upper seat (back, Z = -0.36, top surface Y = 0.92m — full legroom behind Tier 1)
            p.Box(woodMat, new Vector3(0f, 0.89f, -0.36f), new Vector3(length, 0.06f, 0.40f));

            // Steel A-frame risers + rear safety railing (clear behind Tier 2 backs at Z = -0.74)
            int frames = Mathf.Max(2, Mathf.RoundToInt(length / 2.2f) + 1);
            for (int i = 0; i < frames; i++)
            {
                float x = Mathf.Lerp(-length * 0.46f, length * 0.46f, i / (float)(frames - 1));
                p.Box(steelMat, new Vector3(x, 0.24f, 0.48f), new Vector3(0.07f, 0.42f, 0.07f));
                p.Box(steelMat, new Vector3(x, 0.24f, 0.06f), new Vector3(0.06f, 0.42f, 0.06f));
                p.Box(steelMat, new Vector3(x, 0.46f, -0.36f), new Vector3(0.07f, 0.86f, 0.07f));
                p.Box(steelMat, new Vector3(x, 1.24f, -0.74f), new Vector3(0.065f, 1.48f, 0.065f));
            }
            p.Rod(steelMat, _cylinder, new Vector3(-length * 0.48f, 1.56f, -0.74f), new Vector3(length * 0.48f, 1.56f, -0.74f), 0.022f);
            p.Rod(steelMat, _cylinder, new Vector3(-length * 0.48f, 1.22f, -0.74f), new Vector3(length * 0.48f, 1.22f, -0.74f), 0.018f);

            if (covered)
            {
                // Covered Halı Saha tribün roof canopy
                foreach (float cx in new[] { -length * 0.46f, 0f, length * 0.46f })
                {
                    p.Rod(steelMat, _cylinder, new Vector3(cx, 0.08f, -0.76f), new Vector3(cx, 2.62f, -0.76f), 0.042f);
                    p.Rod(steelMat, _cylinder, new Vector3(cx, 2.58f, -0.80f), new Vector3(cx, 2.82f, 0.90f), 0.032f);
                }
                p.Add(canopyMat, _cube, new Vector3(0f, 2.74f, 0.06f), Quaternion.Euler(-8f, 0f, 0f), new Vector3(length + 0.35f, 0.07f, 1.88f));
            }

            p.Emit(bleacher, "BleacherStructure", shadows: true);
            AddCollider(bleacher, "BleacherCollider", new Vector3(0f, 0.55f, 0.02f), new Vector3(length, 1.1f, 1.72f));

            // Spawn cheering neighborhood spectators cleanly seated ON TOP of Tier 1 (Y=0.48) & Tier 2 (Y=0.92) planks!
            if (template != null && spectatorNames != null && _atmosphere != null)
            {
                Vector3 courtWorldCenter = parent.position;
                float slabTopWorldY = bleacher.TransformPoint(new Vector3(0f, 0.08f, 0f)).y;
                float footrestTopWorldY = bleacher.TransformPoint(new Vector3(0f, 0.475f, 0f)).y;

                for (int i = 0; i < spectatorNames.Length; i++)
                {
                    bool upperTier = (i % 2 == 1);
                    float xOff = spectatorNames.Length == 1
                        ? 0f
                        : Mathf.Lerp(-length * 0.34f, length * 0.34f, i / (float)(spectatorNames.Length - 1));
                    // Root Y = top surface of wooden plank; ProceduralHumanoid offsets hips +0.105m above seat surface
                    Vector3 seatLocal = upperTier
                        ? new Vector3(xOff, 0.92f, -0.32f)
                        : new Vector3(xOff, 0.48f,  0.52f);
                    float footFloorY = upperTier ? footrestTopWorldY : slabTopWorldY;

                    ProceduralHumanoid fan = Instantiate(template, bleacher);
                    fan.name = $"SportSpectator_{sportKind}_{i + 1}";
                    fan.transform.localPosition = seatLocal;
                    fan.transform.localRotation = Quaternion.identity;
                    Color shirt = (shirtColors != null && i < shirtColors.Length)
                        ? shirtColors[i]
                        : new Color(0.28f, 0.48f, 0.68f);
                    ApplyNpcShirtColor(fan, shader, $"SpectatorShirt_{sportKind}_{i}", shirt);
                    _atmosphere.RegisterSpectator(fan, spectatorNames[i], sportKind, watchBall, courtWorldCenter, seated: true, seatFloorY: footFloorY);
                }
            }
        }

        private void AddTeamDugout(Transform parent, Vector3 localPos, float yawDeg, Shader shader, Material steelMat)
        {
            Transform dugout = Child(parent, "TeamDugout_YedekKulubesi", localPos);
            dugout.localRotation = Quaternion.Euler(0f, yawDeg, 0f);

            Material benchWood = MaterialFor(shader, "Dugout Wood", new Color(0.58f, 0.38f, 0.22f), 0.26f);
            Material polyMat = MaterialForTranslucent(shader, "Dugout Polycarbonate", new Color(0.76f, 0.88f, 0.96f, 0.44f), 0.72f);
            Material coolerWhite = MaterialFor(shader, "Cooler White", new Color(0.92f, 0.94f, 0.96f), 0.38f);
            Material coolerBlue = MaterialFor(shader, "Water Jug Blue", new Color(0.22f, 0.64f, 0.92f), 0.62f);

            MeshParts p = new MeshParts(this);
            // Rear & side steel frame
            foreach (float x in new[] { -1.75f, 0f, 1.75f })
            {
                p.Rod(steelMat, _cylinder, new Vector3(x, 0f, -0.58f), new Vector3(x, 2.05f, -0.58f), 0.032f);
                p.Rod(steelMat, _cylinder, new Vector3(x, 2.02f, -0.58f), new Vector3(x, 2.18f, 0.56f), 0.026f);
            }
            p.Rod(steelMat, _cylinder, new Vector3(-1.75f, 2.04f, -0.58f), new Vector3(1.75f, 2.04f, -0.58f), 0.028f);
            p.Rod(steelMat, _cylinder, new Vector3(-1.75f, 2.18f, 0.56f), new Vector3(1.75f, 2.18f, 0.56f), 0.026f);

            // Translucent polycarbonate shelter back & curved roof
            p.Box(polyMat, new Vector3(0f, 1.05f, -0.58f), new Vector3(3.46f, 1.92f, 0.03f));
            p.Add(polyMat, _cube, new Vector3(0f, 2.12f, 0f), Quaternion.Euler(-8f, 0f, 0f), new Vector3(3.58f, 0.04f, 1.24f));

            // Player bench inside dugout
            p.Box(benchWood, new Vector3(-0.22f, 0.46f, -0.18f), new Vector3(2.65f, 0.06f, 0.42f));
            foreach (float bx in new[] { -1.35f, -0.22f, 0.92f })
            {
                p.Box(steelMat, new Vector3(bx, 0.23f, -0.18f), new Vector3(0.06f, 0.46f, 0.36f));
            }

            // Water dispenser cooler (Damacana Sebil) at right end of dugout
            p.Box(coolerWhite, new Vector3(1.42f, 0.44f, -0.18f), new Vector3(0.38f, 0.88f, 0.38f));
            p.Add(coolerBlue, _cylinder, new Vector3(1.42f, 1.08f, -0.18f), Quaternion.identity, new Vector3(0.28f, 0.22f, 0.28f));

            p.Emit(dugout, "DugoutParts", shadows: false);
            AddCollider(dugout, "DugoutCollider", new Vector3(0f, 1.0f, -0.12f), new Vector3(3.6f, 2.0f, 1.15f));
        }

        private void AddElevatedRefChair(
            Transform parent,
            Vector3 localPos,
            float yawDeg,
            float seatHeight,
            bool withCanopy,
            Shader shader,
            Material frameMat,
            Material seatMat)
        {
            Transform chair = Child(parent, "RefereeChair", localPos);
            chair.localRotation = Quaternion.Euler(0f, yawDeg, 0f);

            MeshParts p = new MeshParts(this);
            // Four tall A-frame legs
            p.Rod(frameMat, _cylinder, new Vector3(-0.34f, 0f, -0.36f), new Vector3(-0.28f, seatHeight + 0.65f, -0.28f), 0.024f);
            p.Rod(frameMat, _cylinder, new Vector3( 0.34f, 0f, -0.36f), new Vector3( 0.28f, seatHeight + 0.65f, -0.28f), 0.024f);
            p.Rod(frameMat, _cylinder, new Vector3(-0.34f, 0f,  0.42f), new Vector3(-0.28f, seatHeight + 0.28f,  0.28f), 0.024f);
            p.Rod(frameMat, _cylinder, new Vector3( 0.34f, 0f,  0.42f), new Vector3( 0.28f, seatHeight + 0.28f,  0.28f), 0.024f);

            // Access ladder rungs up the back
            int rungs = Mathf.Clamp(Mathf.RoundToInt(seatHeight / 0.34f), 3, 6);
            for (int r = 1; r <= rungs; r++)
            {
                float ry = r * (seatHeight / (rungs + 0.4f));
                p.Rod(frameMat, _cylinder, new Vector3(-0.30f, ry, -0.32f), new Vector3(0.30f, ry, -0.32f), 0.016f);
            }

            // Platform / Seat + Backrest + Armrests
            p.Box(seatMat, new Vector3(0f, seatHeight, 0f), new Vector3(0.58f, 0.06f, 0.56f));
            p.Box(seatMat, new Vector3(0f, seatHeight + 0.34f, -0.26f), new Vector3(0.56f, 0.44f, 0.06f));
            p.Rod(frameMat, _cylinder, new Vector3(-0.28f, seatHeight + 0.26f, -0.26f), new Vector3(-0.28f, seatHeight + 0.26f, 0.28f), 0.018f);
            p.Rod(frameMat, _cylinder, new Vector3( 0.28f, seatHeight + 0.26f, -0.26f), new Vector3( 0.28f, seatHeight + 0.26f, 0.28f), 0.018f);
            // Footrest bar
            p.Box(frameMat, new Vector3(0f, seatHeight - 0.38f, 0.36f), new Vector3(0.56f, 0.04f, 0.14f));

            if (withCanopy)
            {
                Material canopyWhite = MaterialFor(shader, "Umpire Canopy White", new Color(0.95f, 0.95f, 0.92f), 0.22f);
                p.Rod(frameMat, _cylinder, new Vector3(-0.28f, seatHeight + 0.55f, -0.26f), new Vector3(-0.28f, seatHeight + 1.18f, -0.26f), 0.016f);
                p.Rod(frameMat, _cylinder, new Vector3( 0.28f, seatHeight + 0.55f, -0.26f), new Vector3( 0.28f, seatHeight + 1.18f, -0.26f), 0.016f);
                p.Add(canopyWhite, _cube, new Vector3(0f, seatHeight + 1.22f, 0.04f), Quaternion.Euler(-6f, 0f, 0f), new Vector3(0.84f, 0.05f, 0.88f));
            }

            p.Emit(chair, "RefChairParts", shadows: false);
            AddCollider(chair, "RefChairCollider", new Vector3(0f, seatHeight * 0.5f, 0f), new Vector3(0.72f, seatHeight, 0.78f));
        }

        private void AddBallRack(
            Transform parent,
            Vector3 localPos,
            float yawDeg,
            Material frameMat,
            Material ballMat,
            float ballDiameter,
            int ballCount = 3)
        {
            Transform rack = Child(parent, "SportBallRack", localPos);
            rack.localRotation = Quaternion.Euler(0f, yawDeg, 0f);

            MeshParts p = new MeshParts(this);
            float w = ballCount * (ballDiameter + 0.08f) + 0.16f;
            foreach (float x in new[] { -w * 0.48f, w * 0.48f })
            {
                p.Rod(frameMat, _cylinder, new Vector3(x, 0f, -0.18f), new Vector3(x, 0.86f, -0.18f), 0.018f);
                p.Rod(frameMat, _cylinder, new Vector3(x, 0f,  0.18f), new Vector3(x, 0.86f,  0.18f), 0.018f);
            }
            foreach (float y in new[] { 0.40f, 0.76f })
            {
                p.Rod(frameMat, _cylinder, new Vector3(-w * 0.48f, y, -0.14f), new Vector3(w * 0.48f, y, -0.14f), 0.014f);
                p.Rod(frameMat, _cylinder, new Vector3(-w * 0.48f, y,  0.14f), new Vector3(w * 0.48f, y,  0.14f), 0.014f);
            }
            for (int i = 0; i < ballCount; i++)
            {
                float bx = Mathf.Lerp(-w * 0.34f, w * 0.34f, ballCount == 1 ? 0.5f : i / (float)(ballCount - 1));
                p.Add(ballMat, _sphere, new Vector3(bx, 0.76f + ballDiameter * 0.45f, 0f), Quaternion.identity, Vector3.one * ballDiameter);
                if (i % 2 == 0)
                {
                    p.Add(ballMat, _sphere, new Vector3(bx, 0.40f + ballDiameter * 0.45f, 0f), Quaternion.identity, Vector3.one * ballDiameter);
                }
            }
            p.Emit(rack, "BallRackParts", shadows: false);
            AddCollider(rack, "BallRackCollider", new Vector3(0f, 0.48f, 0f), new Vector3(w, 0.96f, 0.42f));
        }

        private void AddBeachUmbrella(Transform parent, Vector3 localPos, Shader shader, Material poleMat, Color primary, Color stripe)
        {
            Transform umb = Child(parent, "BeachShadeUmbrella", localPos);
            Material m1 = MaterialFor(shader, "Umbrella Primary", primary, 0.22f);
            Material m2 = MaterialFor(shader, "Umbrella Stripe", stripe, 0.22f);

            MeshParts p = new MeshParts(this);
            p.Add(poleMat, _cylinder, new Vector3(0f, 0.05f, 0f), Quaternion.identity, new Vector3(0.42f, 0.05f, 0.42f));
            p.Rod(poleMat, _cylinder, Vector3.zero, Vector3.up * 2.48f, 0.028f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Quaternion rot = Quaternion.Euler(0f, i * 45f, 0f) * Quaternion.Euler(20f, 0f, 0f);
                Vector3 pos = new Vector3(Mathf.Sin(a) * 0.62f, 2.34f, Mathf.Cos(a) * 0.62f);
                p.Add(i % 2 == 0 ? m1 : m2, _cube, pos, rot, new Vector3(0.68f, 0.035f, 1.32f));
            }
            p.Emit(umb, "UmbrellaParts", shadows: true);
            WorldPhysicsSolidifier.EnsureTrunkCollider(umb.gameObject, radius: 0.16f, height: 2.4f);
        }
    }
}
