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
    /// Tactile open-world environmental realism & micro-detail layer (100% pooled, zero GC, URP mobile-safe):
    /// 1. Organic wind sway on Istanbul plane/cypress/pine/blossom tree canopies & cafe/beach umbrellas
    ///    synced to IstanbulLivingSoundscape.WindDirection & WindStrength01 (distance-culled, physics-safe).
    /// 2. Rising savory steam plumes over Simit Carts, Turkish Tea Garden tables, Bakeries, and glowing
    ///    Backyard Mangal embers, plus chilly dawn/night breath vapor and post-swim/rain clothing water drips.
    /// 3. Zero-allocation pooled ground decal bank for vehicle tire skid marks (rubber on asphalt, ruts on
    ///    sand/grass) and alternating wet/sand shoe footprints.
    /// </summary>
    public sealed class WorldRealismAtmosphere : MonoBehaviour
    {
        public static WorldRealismAtmosphere Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_WorldRealismAtmosphere");
            go.AddComponent<WorldRealismAtmosphere>();
        }

        private sealed class SwayEntry
        {
            public Transform Target;
            public Quaternion BaseLocalRot;
            public float Phase;
            public float AmplitudeDeg;
            public float Speed;
        }

        private sealed class SteamAnchor
        {
            public Vector3 WorldPos;
            public Color Tint;
            public float Interval;
            public float Timer;
            public float RiseSpeed;
            public float Size;
            public bool RequiresMangalUnlock;
        }

        private sealed class DecalSlot
        {
            public GameObject Go;
            public Renderer Rend;
            public MaterialPropertyBlock Mpb;
            public Color BaseColor;
            public float Elapsed;
            public float Duration;
            public bool Active;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<SwayEntry> _swayTargets = new List<SwayEntry>(128);
        private readonly List<SteamAnchor> _steamAnchors = new List<SteamAnchor>(24);
        private readonly List<DecalSlot> _decalPool = new List<DecalSlot>(80);
        private int _decalCursor;

        private Texture2D _steamTex;
        private Material _steamMat;
        private ParticleSystem _steamPs;

        private Texture2D _skidTex;
        private Texture2D _footprintTex;
        private Material _skidMat;
        private Material _footprintMat;
        private Mesh _quadMesh;

        private Camera _cachedCam;
        private float _breathTimer = 2.6f;
        private float _wetDripTimer = 0.4f;
        private bool _swayCollected;

        private static bool IsLowTier =>
            GraphicsProfileManager.Instance != null && GraphicsProfileManager.Instance.CurrentTier == OurWorld.Core.GraphicsTier.Low;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildSteamParticleSystem();
            BuildDecalPool();
        }

        private IEnumerator Start()
        {
            // Wait 2 frames so runtime builders (IstanbulThemeOverhaul, WorldExpansionAndFarmBuilder, Sports) finish spawning
            yield return null;
            yield return null;
            CollectSwayTargetsAndSteamAnchors();
        }

        private void OnDestroy()
        {
            if (_steamMat != null) Destroy(_steamMat);
            if (_steamTex != null) Destroy(_steamTex);
            if (_skidMat != null) Destroy(_skidMat);
            if (_footprintMat != null) Destroy(_footprintMat);
            if (_skidTex != null) Destroy(_skidTex);
            if (_footprintTex != null) Destroy(_footprintTex);
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        // PUBLIC ZERO-ALLOCATION DECAL & MICRO-VFX API
        // =====================================================================
        public static void SpawnSkidMark(Vector3 groundPos, Vector3 forwardDir, float width = 0.24f, float length = 0.72f, Color? tint = null, float duration = 18f)
        {
            if (Instance == null || Instance._decalPool.Count == 0) return;
            Vector3 flatFwd = forwardDir;
            flatFwd.y = 0f;
            if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
            else flatFwd.Normalize();

            Color col = tint ?? new Color(0.07f, 0.07f, 0.08f, 0.52f);
            Instance.PlaceDecal(
                groundPos + Vector3.up * 0.026f,
                Quaternion.LookRotation(Vector3.down, flatFwd),
                new Vector3(width, length, 1f),
                Instance._skidMat,
                col,
                duration);
        }

        public static void SpawnFootprint(Vector3 groundPos, Vector3 forwardDir, bool rightFoot, bool isWet, float alpha = 0.42f)
        {
            if (Instance == null || Instance._decalPool.Count == 0) return;
            Vector3 flatFwd = forwardDir;
            flatFwd.y = 0f;
            if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
            else flatFwd.Normalize();

            Vector3 right = new Vector3(flatFwd.z, 0f, -flatFwd.x);
            Vector3 pos = groundPos + right * (rightFoot ? 0.11f : -0.11f) + Vector3.up * 0.024f;
            Color col = isWet
                ? new Color(0.10f, 0.13f, 0.16f, Mathf.Clamp(alpha, 0.16f, 0.56f))
                : new Color(0.54f, 0.45f, 0.32f, Mathf.Clamp(alpha * 0.92f, 0.16f, 0.46f));

            Instance.PlaceDecal(
                pos,
                Quaternion.LookRotation(Vector3.down, flatFwd),
                new Vector3(rightFoot ? 0.14f : -0.14f, 0.28f, 1f),
                Instance._footprintMat,
                col,
                isWet ? 9.5f : 12.5f);
        }

        public static void EmitSteamWisp(Vector3 pos, Color tint, float size = 0.34f, float riseSpeed = 0.65f, float lifetime = 1.6f)
        {
            if (Instance == null || Instance._steamPs == null) return;
            Vector3 wind = IstanbulLivingSoundscape.WindDirection * (0.25f + IstanbulLivingSoundscape.WindStrength01 * 0.65f);
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = wind + Vector3.up * riseSpeed + UnityEngine.Random.insideUnitSphere * 0.08f,
                startSize = size,
                startLifetime = lifetime,
                startColor = tint
            };
            Instance._steamPs.Emit(ep, 1);
        }

        private void PlaceDecal(Vector3 pos, Quaternion rot, Vector3 scale, Material mat, Color color, float duration)
        {
            int count = _decalPool.Count;
            if (count == 0) return;

            DecalSlot slot = _decalPool[_decalCursor % count];
            _decalCursor = (_decalCursor + 1) % count;

            slot.Active = true;
            slot.Elapsed = 0f;
            slot.Duration = Mathf.Max(2f, duration);
            slot.BaseColor = color;
            slot.Go.transform.SetPositionAndRotation(pos, rot);
            slot.Go.transform.localScale = scale;
            slot.Rend.sharedMaterial = mat;
            slot.Mpb.SetColor(BaseColorId, color);
            slot.Mpb.SetColor(ColorId, color);
            slot.Rend.SetPropertyBlock(slot.Mpb);
            if (!slot.Go.activeSelf) slot.Go.SetActive(true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || OurWorld.UI.MainMenuController.IsMenuActive) return;

            if (_cachedCam == null) _cachedCam = Camera.main;
            Vector3 focusPos = _cachedCam != null
                ? _cachedCam.transform.position
                : (PlayerModeController.Instance != null ? PlayerModeController.Instance.ActiveWorldPosition : Vector3.zero);

            UpdateFoliageWindSway(focusPos);
            UpdateStreetSteamAndPlayerMicroFx(dt, focusPos);
            UpdateDecalFadeOut(dt);
        }

        // =====================================================================
        // 1. ORGANIC FOLIAGE & UMBRELLA WIND SWAY (DISTANCE-CULLED, ZERO GC)
        // =====================================================================
        private void UpdateFoliageWindSway(Vector3 focusPos)
        {
            if (!_swayCollected || _swayTargets.Count == 0) return;

            float t = Time.time;
            float windStr = IstanbulLivingSoundscape.WindStrength01;
            float gust = IstanbulLivingSoundscape.GustFactor;
            Vector3 windDir = IstanbulLivingSoundscape.WindDirection;
            float maxDistSq = IsLowTier ? (52f * 52f) : (82f * 82f);

            for (int i = 0; i < _swayTargets.Count; i++)
            {
                SwayEntry e = _swayTargets[i];
                if (e.Target == null) continue;
                Vector3 d = e.Target.position - focusPos;
                if (d.x * d.x + d.z * d.z > maxDistSq) continue;

                float wave = Mathf.Sin(t * e.Speed * (0.85f + windStr * 0.65f) + e.Phase)
                           + Mathf.Sin(t * e.Speed * 1.93f + e.Phase * 1.7f) * 0.35f;
                float amp = e.AmplitudeDeg * (0.35f + windStr * 0.95f + gust * 0.45f);
                float tiltX = (windDir.z * 0.55f + wave * 0.72f) * amp;
                float tiltZ = (-windDir.x * 0.55f + wave * 0.58f) * amp;
                e.Target.localRotation = e.BaseLocalRot * Quaternion.Euler(tiltX, 0f, tiltZ);
            }
        }

        // =====================================================================
        // 2. SAVORY STREET STEAM, COLD-NIGHT BREATH VAPOR & WET CLOTHES DRIPS
        // =====================================================================
        private void UpdateStreetSteamAndPlayerMicroFx(float dt, Vector3 focusPos)
        {
            if (_steamPs == null) return;

            float maxSteamSq = IsLowTier ? (26f * 26f) : (42f * 42f);
            bool mangalOn = HomeDecorManager.IsUnlocked(HomeDecorUpgradeId.BackyardMangal);

            for (int i = 0; i < _steamAnchors.Count; i++)
            {
                SteamAnchor a = _steamAnchors[i];
                if (a.RequiresMangalUnlock && !mangalOn) continue;

                Vector3 diff = a.WorldPos - focusPos;
                if (diff.x * diff.x + diff.z * diff.z > maxSteamSq) continue;

                a.Timer -= dt;
                if (a.Timer <= 0f)
                {
                    a.Timer = a.Interval * UnityEngine.Random.Range(0.80f, 1.25f);
                    EmitSteamWisp(
                        a.WorldPos + new Vector3(UnityEngine.Random.Range(-0.14f, 0.14f), 0f, UnityEngine.Random.Range(-0.14f, 0.14f)),
                        a.Tint,
                        a.Size * UnityEngine.Random.Range(0.85f, 1.20f),
                        a.RiseSpeed,
                        UnityEngine.Random.Range(1.35f, 2.05f));
                }
            }

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.CurrentMode != ControlMode.OnFoot || mode.FootController == null) return;
            PlayerFootController foot = mode.FootController;
            if (foot.IsSwimming) return;

            // Cool Istanbul night / crisp dawn breath vapor outdoors (21:30–07:00)
            float hour = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentHour : 11f;
            bool chillyAir = (hour >= 21.5f || hour < 7.0f) && !NeighborhoodWeatherSystem.IsPositionSheltered(foot.transform.position);
            if (chillyAir)
            {
                _breathTimer -= dt;
                if (_breathTimer <= 0f)
                {
                    float speed = foot.CurrentSpeed;
                    _breathTimer = speed > 2.8f ? UnityEngine.Random.Range(1.3f, 1.8f) : UnityEngine.Random.Range(2.8f, 3.8f);
                    Vector3 mouthPos = foot.transform.position + Vector3.up * 1.56f + foot.transform.forward * 0.20f;
                    ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams
                    {
                        position = mouthPos,
                        velocity = foot.transform.forward * 0.42f + Vector3.up * 0.22f + IstanbulLivingSoundscape.WindDirection * 0.18f,
                        startSize = 0.16f,
                        startLifetime = 0.88f,
                        startColor = new Color(0.92f, 0.96f, 1.0f, 0.24f)
                    };
                    _steamPs.Emit(ep, 1);
                }
            }

            // Visible subtle water drips falling off soaked clothes after swimming or heavy rain
            if (foot.RainWetness01 > 0.55f)
            {
                _wetDripTimer -= dt;
                if (_wetDripTimer <= 0f)
                {
                    _wetDripTimer = UnityEngine.Random.Range(0.28f, 0.52f);
                    Vector3 dripPos = foot.transform.position
                        + new Vector3(UnityEngine.Random.Range(-0.22f, 0.22f), UnityEngine.Random.Range(0.55f, 1.35f), UnityEngine.Random.Range(-0.18f, 0.18f));
                    ParticleSystem.EmitParams dp = new ParticleSystem.EmitParams
                    {
                        position = dripPos,
                        velocity = new Vector3(0f, -1.55f, 0f),
                        startSize = 0.055f,
                        startLifetime = 0.42f,
                        startColor = new Color(0.76f, 0.90f, 1.0f, 0.45f)
                    };
                    _steamPs.Emit(dp, 1);
                }
            }
        }

        private void UpdateDecalFadeOut(float dt)
        {
            for (int i = 0; i < _decalPool.Count; i++)
            {
                DecalSlot slot = _decalPool[i];
                if (!slot.Active || slot.Go == null) continue;

                slot.Elapsed += dt;
                float u = slot.Elapsed / slot.Duration;
                if (u >= 1f)
                {
                    slot.Active = false;
                    slot.Go.SetActive(false);
                    continue;
                }

                // Stay crisp for first 60% of lifetime, then fade smoothly to invisible
                if (u > 0.60f)
                {
                    float a = slot.BaseColor.a * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.60f) / 0.40f));
                    Color c = slot.BaseColor;
                    c.a = a;
                    slot.Mpb.SetColor(BaseColorId, c);
                    slot.Mpb.SetColor(ColorId, c);
                    slot.Rend.SetPropertyBlock(slot.Mpb);
                }
            }
        }

        // =====================================================================
        // SCENE DISCOVERY & POOL CONSTRUCTION
        // =====================================================================
        private void CollectSwayTargetsAndSteamAnchors()
        {
            _swayTargets.Clear();
            _steamAnchors.Clear();

            Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            HashSet<Transform> addedSway = new HashSet<Transform>();
            System.Random rng = new System.Random(20261);

            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform tr = allTransforms[i];
                if (tr == null) continue;
                string n = tr.name;

                // Tree canopies / visual foliage children (never sway objects carrying a Collider directly!)
                bool isTreeFoliage =
                    n.IndexOf("Crown", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Foliage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Canopy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Leaves", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Blossom", StringComparison.OrdinalIgnoreCase) >= 0;

                bool isUmbrellaFabric =
                    (n.IndexOf("Umbrella", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     n.IndexOf("Awning", StringComparison.OrdinalIgnoreCase) >= 0) &&
                    tr.GetComponent<Collider>() == null &&
                    tr.GetComponent<MeshRenderer>() != null;

                if ((isTreeFoliage && tr.GetComponent<Collider>() == null && tr.GetComponent<MeshRenderer>() != null) || isUmbrellaFabric)
                {
                    if (addedSway.Add(tr) && _swayTargets.Count < 140)
                    {
                        _swayTargets.Add(new SwayEntry
                        {
                            Target = tr,
                            BaseLocalRot = tr.localRotation,
                            Phase = (float)(rng.NextDouble() * Math.PI * 2.0),
                            AmplitudeDeg = isUmbrellaFabric ? 1.15f : 1.95f,
                            Speed = isUmbrellaFabric ? 1.85f : (1.15f + (float)rng.NextDouble() * 0.55f)
                        });
                    }
                }
                else if ((n.StartsWith("OW_PlaneTree", StringComparison.OrdinalIgnoreCase) ||
                          n.StartsWith("OW_CypressTree", StringComparison.OrdinalIgnoreCase)) &&
                         tr.childCount > 0)
                {
                    // Tilt visual model child (not collider root!) gently in Istanbul breeze
                    Transform visChild = tr.GetChild(0);
                    if (visChild != null && visChild.GetComponent<Collider>() == null && addedSway.Add(visChild) && _swayTargets.Count < 140)
                    {
                        _swayTargets.Add(new SwayEntry
                        {
                            Target = visChild,
                            BaseLocalRot = visChild.localRotation,
                            Phase = (float)(rng.NextDouble() * Math.PI * 2.0),
                            AmplitudeDeg = 1.45f,
                            Speed = 1.05f + (float)rng.NextDouble() * 0.45f
                        });
                    }
                }

                // Discover Simit Carts & Tea Garden Sets for authentic Istanbul street food steam
                if (n.IndexOf("SimitCart", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("istanbul_simit", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _steamAnchors.Add(new SteamAnchor
                    {
                        WorldPos = tr.position + Vector3.up * 1.42f,
                        Tint = new Color(1.0f, 0.96f, 0.88f, 0.24f),
                        Interval = 0.55f,
                        Timer = (float)rng.NextDouble() * 0.5f,
                        RiseSpeed = 0.62f,
                        Size = 0.34f
                    });
                }
                else if (n.IndexOf("TeaGardenSet", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _steamAnchors.Add(new SteamAnchor
                    {
                        WorldPos = tr.position + Vector3.up * 0.98f,
                        Tint = new Color(0.98f, 0.96f, 0.94f, 0.20f),
                        Interval = 0.72f,
                        Timer = (float)rng.NextDouble() * 0.6f,
                        RiseSpeed = 0.48f,
                        Size = 0.22f
                    });
                }
            }

            // Guaranteed Simit Cart & Bakery oven chimney anchors + Backyard Mangal smoke
            Vector3[] fallbackWarmAnchors =
            {
                new Vector3(6.8f, 1.45f, 13.8f),
                new Vector3(-25.5f, 1.45f, -21.8f),
                new Vector3(-9.5f, 1.45f, 8.2f),
                new Vector3(-165.0f, 1.45f, -5.2f)
            };
            for (int i = 0; i < fallbackWarmAnchors.Length; i++)
            {
                if (!HasAnchorNear(fallbackWarmAnchors[i], 2.0f))
                {
                    _steamAnchors.Add(new SteamAnchor
                    {
                        WorldPos = fallbackWarmAnchors[i],
                        Tint = new Color(1.0f, 0.96f, 0.88f, 0.24f),
                        Interval = 0.58f,
                        Timer = i * 0.14f,
                        RiseSpeed = 0.60f,
                        Size = 0.32f
                    });
                }
            }

            // Backyard Mangal warm charcoal smoke plume (activates automatically when unlocked!)
            _steamAnchors.Add(new SteamAnchor
            {
                WorldPos = HomeDecorManager.BackyardMangalWorldPos + Vector3.up * 0.98f,
                Tint = new Color(0.76f, 0.72f, 0.68f, 0.34f),
                Interval = 0.34f,
                Timer = 0.1f,
                RiseSpeed = 0.92f,
                Size = 0.48f,
                RequiresMangalUnlock = true
            });

            _swayCollected = true;
        }

        private bool HasAnchorNear(Vector3 pos, float radius)
        {
            float rSq = radius * radius;
            for (int i = 0; i < _steamAnchors.Count; i++)
            {
                Vector3 d = _steamAnchors[i].WorldPos - pos;
                d.y = 0f;
                if (d.sqrMagnitude <= rSq) return true;
            }
            return false;
        }

        private void BuildSteamParticleSystem()
        {
            _steamTex = BuildSoftCloudTexture(64);
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            _steamMat = new Material(sh);
            if (_steamMat.HasProperty("_BaseMap")) _steamMat.SetTexture("_BaseMap", _steamTex);
            if (_steamMat.HasProperty("_MainTex")) _steamMat.SetTexture("_MainTex", _steamTex);
            if (_steamMat.HasProperty("_Surface")) _steamMat.SetFloat("_Surface", 1f);
            if (_steamMat.HasProperty("_Blend")) _steamMat.SetFloat("_Blend", 0f);
            if (_steamMat.HasProperty("_ZWrite")) _steamMat.SetFloat("_ZWrite", 0f);
            _steamMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _steamMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _steamMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _steamMat.renderQueue = (int)RenderQueue.Transparent;

            GameObject go = new GameObject("SteamAndMicroVfxEmitter");
            go.transform.SetParent(transform, false);
            _steamPs = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = _steamPs.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = IsLowTier ? 60 : 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.EmissionModule em = _steamPs.emission;
            em.enabled = false;

            ParticleSystem.SizeOverLifetimeModule sz = _steamPs.sizeOverLifetime;
            sz.enabled = true;
            AnimationCurve growCurve = new AnimationCurve(
                new Keyframe(0f, 0.45f),
                new Keyframe(0.35f, 1.0f),
                new Keyframe(1f, 1.85f));
            sz.size = new ParticleSystem.MinMaxCurve(1f, growCurve);

            ParticleSystem.ColorOverLifetimeModule col = _steamPs.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.22f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            ParticleSystemRenderer pr = _steamPs.GetComponent<ParticleSystemRenderer>();
            if (pr != null)
            {
                pr.sharedMaterial = _steamMat;
                pr.shadowCastingMode = ShadowCastingMode.Off;
                pr.receiveShadows = false;
            }
        }

        private void BuildDecalPool()
        {
            GameObject tmpQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quadMesh = tmpQuad.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmpQuad);

            _skidTex = BuildTireSkidTexture(64);
            _footprintTex = BuildFootprintTexture(64);
            _skidMat = BuildTransparentDecalMat(_skidTex);
            _footprintMat = BuildTransparentDecalMat(_footprintTex);

            Transform root = new GameObject("GroundDecalPool").transform;
            root.SetParent(transform, false);

            int poolSize = IsLowTier ? 44 : 80;
            for (int i = 0; i < poolSize; i++)
            {
                GameObject go = new GameObject($"Decal_{i:00}");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = _quadMesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _skidMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = true;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                go.SetActive(false);

                _decalPool.Add(new DecalSlot
                {
                    Go = go,
                    Rend = mr,
                    Mpb = new MaterialPropertyBlock(),
                    Active = false
                });
            }
        }

        private static Material BuildTransparentDecalMat(Texture2D tex)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material m = new Material(sh);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent - 10;
            return m;
        }

        private static Texture2D BuildSoftCloudTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ProceduralSteamCloud",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D BuildTireSkidTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ProceduralTireSkid",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                float fadeEnd = 1f - Mathf.SmoothStep(0.55f, 1.0f, Mathf.Abs(ny));
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float edgeX = 1f - Mathf.SmoothStep(0.62f, 1.0f, Mathf.Abs(nx));
                    // 3 crisp longitudinal tire tread grooves
                    float groove = 0.72f + 0.28f * Mathf.Cos(nx * Mathf.PI * 5f);
                    float a = Mathf.Clamp01(fadeEnd * edgeX * groove);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D BuildFootprintTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ProceduralShoePrint",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1); // 0 = heel, 1 = toe
                for (int x = 0; x < size; x++)
                {
                    float u = (x / (float)(size - 1)) * 2f - 1f;
                    // Front sole ellipse + rear heel ellipse with subtle arch gap
                    float soleDx = (u - 0.04f) / 0.52f;
                    float soleDy = (v - 0.68f) / 0.27f;
                    float soleD = Mathf.Sqrt(soleDx * soleDx + soleDy * soleDy);

                    float heelDx = (u + 0.03f) / 0.36f;
                    float heelDy = (v - 0.20f) / 0.17f;
                    float heelD = Mathf.Sqrt(heelDx * heelDx + heelDy * heelDy);

                    float aSole = 1f - Mathf.SmoothStep(0.72f, 1.0f, soleD);
                    float aHeel = 1f - Mathf.SmoothStep(0.70f, 1.0f, heelD);
                    float a = Mathf.Max(aSole, aHeel);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
