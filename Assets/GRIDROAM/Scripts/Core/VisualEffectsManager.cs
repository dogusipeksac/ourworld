using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.Core
{
    /// <summary>
    /// High-detail, zero-bloat procedural GPU Particle VFX & World-Space Floating Feedback system
    /// built specifically for URP mobile/desktop 60 FPS (zero per-hit GameObject sphere allocations,
    /// 100% built-in URP shaders so iOS shader stripping can never break an effect):
    /// - Combat impact shockwave rings, directional stretched sparks & punch dust
    /// - Sprint footstep dust puffs & jump landing radial dust rings
    /// - Pool/lake water splash droplets & expanding surface ripple rings
    /// - Vehicle tire drift smoke & electric cyan-amber Nitro/Turbo exhaust flames
    /// - Crisp shadowed 3D billboard floating damage / cash / XP popups
    /// </summary>
    public sealed class VisualEffectsManager : MonoBehaviour
    {
        public static VisualEffectsManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private Texture2D _softCircleTex;
        private Texture2D _ringTex;
        private Material _additiveSparkMat;
        private Material _alphaDustMat;
        private Material _ringAdditiveMat;

        private ParticleSystem _sparkPs;
        private ParticleSystem _dustPs;
        private ParticleSystem _splashPs;
        private ParticleSystem _nitroPs;
        private ParticleSystem _ringBillboardPs;
        private ParticleSystem _ringFlatPs;

        private Camera _cachedCam;

        private sealed class FloatingTextSlot
        {
            public GameObject Root;
            public TextMesh MainText;
            public TextMesh ShadowText;
            public Vector3 StartPos;
            public Vector3 Velocity;
            public Color BaseColor;
            public float BaseScale;
            public float Elapsed;
            public float Duration;
            public bool Active;
        }

        private readonly List<FloatingTextSlot> _floatingPool = new List<FloatingTextSlot>(16);

        private static bool IsLowTier =>
            GraphicsProfileManager.Instance != null && GraphicsProfileManager.Instance.CurrentTier == GraphicsTier.Low;

        public static VisualEffectsManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_VisualEffectsManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<VisualEffectsManager>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildTexturesAndMaterials();
            BuildParticleEmitters();
        }

        private void OnDestroy()
        {
            if (_additiveSparkMat != null) Destroy(_additiveSparkMat);
            if (_alphaDustMat != null) Destroy(_alphaDustMat);
            if (_ringAdditiveMat != null) Destroy(_ringAdditiveMat);
            if (_softCircleTex != null) Destroy(_softCircleTex);
            if (_ringTex != null) Destroy(_ringTex);
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            if (_floatingPool.Count == 0) return;
            if (_cachedCam == null) _cachedCam = Camera.main;

            float dt = Time.deltaTime;
            for (int i = 0; i < _floatingPool.Count; i++)
            {
                FloatingTextSlot slot = _floatingPool[i];
                if (!slot.Active || slot.Root == null) continue;

                slot.Elapsed += dt;
                float u = Mathf.Clamp01(slot.Elapsed / slot.Duration);
                if (u >= 1f)
                {
                    slot.Active = false;
                    slot.Root.SetActive(false);
                    continue;
                }

                float easeOut = 1f - Mathf.Pow(1f - u, 3f);
                slot.Root.transform.position = slot.StartPos + slot.Velocity * easeOut + Vector3.up * (0.85f * easeOut);

                // Pop-in elastic scale then crisp settle
                float pop = u < 0.16f
                    ? Mathf.Lerp(0.45f, 1.22f, u / 0.16f)
                    : (u < 0.30f ? Mathf.Lerp(1.22f, 1.0f, (u - 0.16f) / 0.14f) : 1.0f);
                slot.Root.transform.localScale = Vector3.one * (slot.BaseScale * pop);

                if (_cachedCam != null)
                {
                    Vector3 look = slot.Root.transform.position - _cachedCam.transform.position;
                    if (look.sqrMagnitude > 0.001f)
                    {
                        slot.Root.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                    }
                }

                float alpha = u < 0.65f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (u - 0.65f) / 0.35f);
                if (slot.MainText != null)
                {
                    Color c = slot.BaseColor;
                    c.a = alpha;
                    slot.MainText.color = c;
                }
                if (slot.ShadowText != null)
                {
                    slot.ShadowText.color = new Color(0.04f, 0.05f, 0.08f, alpha * 0.88f);
                }
            }
        }

        // =====================================================================
        // PUBLIC HIGH-DETAIL VFX API
        // =====================================================================
        public static void SpawnGlowBurst(Vector3 pos, Color color, int count = 12, float spread = 0.55f)
        {
            VisualEffectsManager fx = EnsureInstance();
            int n = Mathf.Clamp(IsLowTier ? Mathf.RoundToInt(count * 0.6f) : count, 4, 28);

            // Soft glowing particles radiating with gentle gravity
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.28f, 0.28f);
                float hSpd = Random.Range(spread * 1.1f, spread * 2.7f);
                Vector3 vel = new Vector3(Mathf.Cos(a) * hSpd, Random.Range(1.2f, 3.0f), Mathf.Sin(a) * hSpd);
                ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams
                {
                    position = pos + Random.insideUnitSphere * (spread * 0.22f),
                    velocity = vel,
                    startSize = Random.Range(0.09f, 0.17f),
                    startLifetime = Random.Range(0.42f, 0.72f),
                    startColor = Color.Lerp(color, Color.white, Random.Range(0.10f, 0.35f))
                };
                fx._sparkPs.Emit(ep, 1);
            }

            // Crisp soft glow ring at center
            ParticleSystem.EmitParams ringEp = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = Vector3.zero,
                startSize = Mathf.Clamp(spread * 1.4f, 0.45f, 1.25f),
                startLifetime = 0.32f,
                startColor = new Color(color.r, color.g, color.b, 0.72f)
            };
            fx._ringBillboardPs.Emit(ringEp, 1);
        }

        public static void SpawnCombatImpact(Vector3 pos, Vector3 hitDir, bool heavy, Color accent)
        {
            VisualEffectsManager fx = EnsureInstance();
            Vector3 dir = hitDir.sqrMagnitude > 0.01f ? hitDir.normalized : Vector3.forward;

            // 1. Expanding crisp shockwave impact ring
            ParticleSystem.EmitParams shock = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = Vector3.zero,
                startSize = heavy ? 1.35f : 0.85f,
                startLifetime = heavy ? 0.28f : 0.20f,
                startColor = Color.Lerp(accent, Color.white, 0.45f)
            };
            fx._ringBillboardPs.Emit(shock, 1);

            // 2. Stretched directional hot sparks
            int sparkCount = heavy ? (IsLowTier ? 10 : 18) : (IsLowTier ? 6 : 11);
            for (int i = 0; i < sparkCount; i++)
            {
                Vector3 cone = (dir * 0.65f + Random.onUnitSphere * 0.85f).normalized;
                float spd = heavy ? Random.Range(2.8f, 6.2f) : Random.Range(1.8f, 4.2f);
                ParticleSystem.EmitParams sp = new ParticleSystem.EmitParams
                {
                    position = pos + Random.insideUnitSphere * 0.08f,
                    velocity = cone * spd + Vector3.up * Random.Range(0.4f, 1.6f),
                    startSize = heavy ? Random.Range(0.11f, 0.19f) : Random.Range(0.08f, 0.14f),
                    startLifetime = Random.Range(0.24f, 0.46f),
                    startColor = i % 3 == 0 ? Color.white : accent
                };
                fx._sparkPs.Emit(sp, 1);
            }

            // 3. Soft tactile impact dust puff
            int dustCount = heavy ? 6 : 3;
            for (int i = 0; i < dustCount; i++)
            {
                ParticleSystem.EmitParams dp = new ParticleSystem.EmitParams
                {
                    position = pos + Random.insideUnitSphere * 0.12f,
                    velocity = Random.insideUnitSphere * (heavy ? 1.35f : 0.85f) + Vector3.up * 0.4f,
                    startSize = heavy ? Random.Range(0.42f, 0.72f) : Random.Range(0.26f, 0.45f),
                    startLifetime = Random.Range(0.35f, 0.58f),
                    startColor = new Color(0.95f, 0.90f, 0.82f, heavy ? 0.44f : 0.30f)
                };
                fx._dustPs.Emit(dp, 1);
            }
        }

        public static void SpawnFootDust(Vector3 groundPos, bool heavyLanding = false, Color? tintOverride = null)
        {
            VisualEffectsManager fx = EnsureInstance();
            Color baseTint = tintOverride ?? new Color(0.84f, 0.80f, 0.72f, 1f);
            int count = heavyLanding ? (IsLowTier ? 5 : 9) : (IsLowTier ? 1 : 2);
            for (int i = 0; i < count; i++)
            {
                float a = heavyLanding ? (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f) : Random.Range(0f, Mathf.PI * 2f);
                float spd = heavyLanding ? Random.Range(0.9f, 2.0f) : Random.Range(0.25f, 0.65f);
                ParticleSystem.EmitParams dp = new ParticleSystem.EmitParams
                {
                    position = groundPos + new Vector3(Mathf.Cos(a) * 0.12f, 0.08f, Mathf.Sin(a) * 0.12f),
                    velocity = new Vector3(Mathf.Cos(a) * spd, Random.Range(0.25f, heavyLanding ? 0.95f : 0.55f), Mathf.Sin(a) * spd),
                    startSize = heavyLanding ? Random.Range(0.36f, 0.62f) : Random.Range(0.22f, 0.38f),
                    startLifetime = heavyLanding ? Random.Range(0.45f, 0.72f) : Random.Range(0.30f, 0.48f),
                    startColor = new Color(baseTint.r, baseTint.g, baseTint.b, heavyLanding ? 0.36f : 0.22f)
                };
                fx._dustPs.Emit(dp, 1);
            }

            if (heavyLanding)
            {
                Color ringCol = Color.Lerp(baseTint, Color.white, 0.14f);
                ParticleSystem.EmitParams ring = new ParticleSystem.EmitParams
                {
                    position = groundPos + Vector3.up * 0.05f,
                    velocity = Vector3.zero,
                    startSize = 1.35f,
                    startLifetime = 0.38f,
                    startColor = new Color(ringCol.r, ringCol.g, ringCol.b, 0.42f)
                };
                fx._ringFlatPs.Emit(ring, 1);
            }
        }

        public static void SpawnWaterSplash(Vector3 surfacePos, bool bigDive = false)
        {
            VisualEffectsManager fx = EnsureInstance();
            int drops = bigDive ? (IsLowTier ? 12 : 22) : (IsLowTier ? 4 : 7);
            for (int i = 0; i < drops; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float hSpd = bigDive ? Random.Range(0.8f, 2.8f) : Random.Range(0.35f, 1.15f);
                ParticleSystem.EmitParams sp = new ParticleSystem.EmitParams
                {
                    position = surfacePos + new Vector3(Mathf.Cos(a) * 0.18f, 0.06f, Mathf.Sin(a) * 0.18f),
                    velocity = new Vector3(Mathf.Cos(a) * hSpd, bigDive ? Random.Range(2.4f, 4.8f) : Random.Range(0.9f, 1.9f), Mathf.Sin(a) * hSpd),
                    startSize = bigDive ? Random.Range(0.11f, 0.22f) : Random.Range(0.07f, 0.13f),
                    startLifetime = bigDive ? Random.Range(0.48f, 0.82f) : Random.Range(0.30f, 0.50f),
                    startColor = new Color(0.82f, 0.96f, 1.0f, 0.82f)
                };
                fx._splashPs.Emit(sp, 1);
            }

            // Expanding horizontal water surface ripple ring
            ParticleSystem.EmitParams rip = new ParticleSystem.EmitParams
            {
                position = surfacePos + Vector3.up * 0.04f,
                velocity = Vector3.zero,
                startSize = bigDive ? 2.4f : 1.15f,
                startLifetime = bigDive ? 0.85f : 0.55f,
                startColor = new Color(0.85f, 0.97f, 1.0f, bigDive ? 0.70f : 0.45f)
            };
            fx._ringFlatPs.Emit(rip, 1);
        }

        public static void SpawnTireSmoke(Vector3 wheelWorldPos, Vector3 carVelocity, float intensity = 1f)
        {
            VisualEffectsManager fx = EnsureInstance();
            int count = IsLowTier ? 1 : Mathf.Clamp(Mathf.RoundToInt(2f * intensity), 1, 3);
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.EmitParams smoke = new ParticleSystem.EmitParams
                {
                    position = wheelWorldPos + new Vector3(Random.Range(-0.12f, 0.12f), 0.12f, Random.Range(-0.12f, 0.12f)),
                    velocity = carVelocity * 0.14f + new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(0.55f, 1.35f), Random.Range(-0.7f, 0.7f)),
                    startSize = Random.Range(0.55f, 0.95f) * Mathf.Clamp(intensity, 0.7f, 1.35f),
                    startLifetime = Random.Range(0.58f, 0.96f),
                    startColor = new Color(0.86f, 0.88f, 0.91f, Mathf.Clamp(0.28f * intensity, 0.16f, 0.42f))
                };
                fx._dustPs.Emit(smoke, 1);
            }
        }

        public static void SpawnNitroExhaust(Vector3 exhaustPos, Vector3 backDir, bool isNitro = true)
        {
            VisualEffectsManager fx = EnsureInstance();
            Vector3 b = backDir.sqrMagnitude > 0.01f ? backDir.normalized : -Vector3.forward;

            if (isNitro)
            {
                int count = IsLowTier ? 3 : 6;
                for (int i = 0; i < count; i++)
                {
                    Color flameCol = i % 2 == 0
                        ? new Color(0.18f, 0.84f, 1.00f, 0.95f)
                        : new Color(1.00f, 0.68f, 0.18f, 0.92f);
                    ParticleSystem.EmitParams np = new ParticleSystem.EmitParams
                    {
                        position = exhaustPos + Random.insideUnitSphere * 0.06f,
                        velocity = b * Random.Range(4.0f, 7.5f) + Random.insideUnitSphere * 0.55f,
                        startSize = Random.Range(0.18f, 0.34f),
                        startLifetime = Random.Range(0.14f, 0.26f),
                        startColor = flameCol
                    };
                    fx._nitroPs.Emit(np, 1);
                }
            }
            else
            {
                ParticleSystem.EmitParams wisp = new ParticleSystem.EmitParams
                {
                    position = exhaustPos,
                    velocity = b * Random.Range(1.4f, 2.5f) + Vector3.up * 0.35f,
                    startSize = Random.Range(0.20f, 0.32f),
                    startLifetime = 0.42f,
                    startColor = new Color(0.75f, 0.77f, 0.80f, 0.18f)
                };
                fx._dustPs.Emit(wisp, 1);
            }
        }

        public static void SpawnFloatingText(Vector3 worldPos, string text, Color color, float scale = 1f, float duration = 0.95f)
        {
            if (string.IsNullOrEmpty(text)) return;
            VisualEffectsManager fx = EnsureInstance();
            FloatingTextSlot slot = fx.AcquireFloatingSlot();
            slot.Active = true;
            slot.StartPos = worldPos + new Vector3(Random.Range(-0.18f, 0.18f), 0f, Random.Range(-0.18f, 0.18f));
            slot.Velocity = new Vector3(Random.Range(-0.22f, 0.22f), 0.18f, Random.Range(-0.22f, 0.22f));
            slot.BaseColor = color;
            slot.BaseScale = scale;
            slot.Elapsed = 0f;
            slot.Duration = Mathf.Max(0.45f, duration);

            slot.Root.transform.position = slot.StartPos;
            slot.Root.transform.localScale = Vector3.one * (scale * 0.5f);
            slot.MainText.text = text;
            slot.MainText.color = color;
            slot.ShadowText.text = text;
            slot.ShadowText.color = new Color(0.04f, 0.05f, 0.08f, 0.88f);
            slot.Root.SetActive(true);
        }

        private FloatingTextSlot AcquireFloatingSlot()
        {
            for (int i = 0; i < _floatingPool.Count; i++)
            {
                if (!_floatingPool[i].Active) return _floatingPool[i];
            }
            if (_floatingPool.Count >= 16)
            {
                // Recycle oldest active slot cleanly
                FloatingTextSlot oldest = _floatingPool[0];
                for (int i = 1; i < _floatingPool.Count; i++)
                {
                    if (_floatingPool[i].Elapsed > oldest.Elapsed) oldest = _floatingPool[i];
                }
                return oldest;
            }

            GameObject root = new GameObject("FloatingVfxText");
            root.transform.SetParent(transform, false);

            GameObject shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(root.transform, false);
            shadowGo.transform.localPosition = new Vector3(0.018f, -0.018f, 0.012f);
            TextMesh shadow = shadowGo.AddComponent<TextMesh>();
            shadow.fontSize = 52;
            shadow.characterSize = 0.028f;
            shadow.anchor = TextAnchor.MiddleCenter;
            shadow.alignment = TextAlignment.Center;
            shadow.fontStyle = FontStyle.Bold;
            MeshRenderer smr = shadowGo.GetComponent<MeshRenderer>();
            if (smr != null) { smr.shadowCastingMode = ShadowCastingMode.Off; smr.receiveShadows = false; }

            TextMesh main = root.AddComponent<TextMesh>();
            main.fontSize = 52;
            main.characterSize = 0.028f;
            main.anchor = TextAnchor.MiddleCenter;
            main.alignment = TextAlignment.Center;
            main.fontStyle = FontStyle.Bold;
            MeshRenderer mmr = root.GetComponent<MeshRenderer>();
            if (mmr != null) { mmr.shadowCastingMode = ShadowCastingMode.Off; mmr.receiveShadows = false; }

            root.SetActive(false);
            FloatingTextSlot created = new FloatingTextSlot { Root = root, MainText = main, ShadowText = shadow };
            _floatingPool.Add(created);
            return created;
        }

        // =====================================================================
        // PROCEDURAL TEXTURES & GPU PARTICLE EMITTERS (100% URP SAFE)
        // =====================================================================
        private void BuildTexturesAndMaterials()
        {
            _softCircleTex = BuildRadialGlowTexture(64);
            _ringTex = BuildShockwaveRingTexture(128);

            Shader partShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");

            _additiveSparkMat = MakeParticleMat(partShader, _softCircleTex, additive: true);
            _alphaDustMat = MakeParticleMat(partShader, _softCircleTex, additive: false);
            _ringAdditiveMat = MakeParticleMat(partShader, _ringTex, additive: true);
        }

        private void BuildParticleEmitters()
        {
            // 1. Bright glowing sparks (gravity + velocity stretch + size shrink)
            _sparkPs = CreateEmitter("VFX_Sparks", _additiveSparkMat, maxParticles: 160, gravity: 0.72f, stretch: true);
            SetSizeCurve(_sparkPs, new AnimationCurve(new Keyframe(0f, 1.0f), new Keyframe(0.7f, 0.65f), new Keyframe(1f, 0.0f)));
            SetAlphaFade(_sparkPs, fadeInEnd: 0.08f, fadeOutStart: 0.55f);

            // 2. Soft expanding dust & tire smoke puffs
            _dustPs = CreateEmitter("VFX_DustSmoke", _alphaDustMat, maxParticles: 140, gravity: -0.04f, stretch: false);
            SetSizeCurve(_dustPs, new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.35f, 1.05f), new Keyframe(1f, 1.85f)));
            SetAlphaFade(_dustPs, fadeInEnd: 0.14f, fadeOutStart: 0.38f);

            // 3. Water splash droplets
            _splashPs = CreateEmitter("VFX_WaterSplash", _alphaDustMat, maxParticles: 120, gravity: 0.95f, stretch: true);
            SetSizeCurve(_splashPs, new AnimationCurve(new Keyframe(0f, 0.85f), new Keyframe(0.6f, 1.0f), new Keyframe(1f, 0.15f)));
            SetAlphaFade(_splashPs, fadeInEnd: 0.05f, fadeOutStart: 0.60f);

            // 4. Electric Nitro/Turbo exhaust flames
            _nitroPs = CreateEmitter("VFX_NitroFlame", _additiveSparkMat, maxParticles: 90, gravity: -0.02f, stretch: true);
            SetSizeCurve(_nitroPs, new AnimationCurve(new Keyframe(0f, 1.15f), new Keyframe(0.45f, 0.85f), new Keyframe(1f, 0.05f)));
            SetAlphaFade(_nitroPs, fadeInEnd: 0.05f, fadeOutStart: 0.42f);

            // 5. Billboard expanding shockwave rings (combat impacts & reward bursts)
            _ringBillboardPs = CreateEmitter("VFX_ShockwaveRings", _ringAdditiveMat, maxParticles: 36, gravity: 0f, stretch: false);
            SetSizeCurve(_ringBillboardPs, new AnimationCurve(new Keyframe(0f, 0.18f), new Keyframe(0.45f, 0.82f), new Keyframe(1f, 1.25f)));
            SetAlphaFade(_ringBillboardPs, fadeInEnd: 0.05f, fadeOutStart: 0.25f);

            // 6. Horizontal flat rings (water ripples & landing shock rings!)
            _ringFlatPs = CreateEmitter("VFX_GroundRippleRings", _ringAdditiveMat, maxParticles: 40, gravity: 0f, stretch: false);
            ParticleSystemRenderer flatRend = _ringFlatPs.GetComponent<ParticleSystemRenderer>();
            if (flatRend != null)
            {
                flatRend.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            }
            SetSizeCurve(_ringFlatPs, new AnimationCurve(new Keyframe(0f, 0.22f), new Keyframe(0.55f, 0.90f), new Keyframe(1f, 1.45f)));
            SetAlphaFade(_ringFlatPs, fadeInEnd: 0.08f, fadeOutStart: 0.30f);
        }

        private ParticleSystem CreateEmitter(string objName, Material mat, int maxParticles, float gravity, bool stretch)
        {
            GameObject go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = maxParticles;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = false;

            ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
            if (pr != null)
            {
                pr.sharedMaterial = mat;
                pr.shadowCastingMode = ShadowCastingMode.Off;
                pr.receiveShadows = false;
                if (stretch)
                {
                    pr.renderMode = ParticleSystemRenderMode.Stretch;
                    pr.velocityScale = 0.085f;
                    pr.lengthScale = 1.45f;
                }
                else
                {
                    pr.renderMode = ParticleSystemRenderMode.Billboard;
                }
            }
            return ps;
        }

        private static void SetSizeCurve(ParticleSystem ps, AnimationCurve curve)
        {
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void SetAlphaFade(ParticleSystem ps, float fadeInEnd, float fadeOutStart)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, fadeInEnd),
                    new GradientAlphaKey(1f, fadeOutStart),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = g;
        }

        private static Material MakeParticleMat(Shader shader, Texture2D tex, bool additive)
        {
            Material m = new Material(shader);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // Transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additive ? 2f : 0f); // Additive vs Alpha
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_BLENDMODE_ADD");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        private static Texture2D BuildRadialGlowTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color[] pix = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a); // Smooth radial bell
                    pix[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private static Texture2D BuildShockwaveRingTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color[] pix = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float ring = Mathf.Exp(-Mathf.Pow((d - 0.74f) / 0.13f, 2f));
                    float outerMask = 1f - Mathf.SmoothStep(0.88f, 0.99f, d);
                    pix[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(ring * outerMask));
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }
    }
}
