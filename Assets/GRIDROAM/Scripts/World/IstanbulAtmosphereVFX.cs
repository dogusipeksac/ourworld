using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Transforms the Istanbul · Sultanbeyli · Hamidiye sky and open-world atmosphere at runtime
    /// with zero scene rebuild and ultra-low mobile GPU cost:
    /// - 28 sculpted multi-puff 3D drifting cumulus clouds dynamically tinted by sunrise/noon/sunset/night/weather
    /// - Glowing 3D Sun Disc + multi-layer solar corona halo synced to RenderSettings.sun
    /// - Luminous Silver Moon + single-draw-call 110-star twinkling night sky dome
    /// - Outdoor sunlit golden dust motes, twilight/night glowing fireflies, drifting plane-tree leaves,
    ///   and live raindrop ground-splash ripples during summer showers!
    /// </summary>
    public sealed class IstanbulAtmosphereVFX : MonoBehaviour
    {
        public static IstanbulAtmosphereVFX Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_IstanbulAtmosphereVFX");
            go.AddComponent<IstanbulAtmosphereVFX>();
        }

        private sealed class CloudCluster
        {
            public Transform Root;
            public float Speed;
            public float BaseY;
            public float BobPhase;
        }

        private readonly List<CloudCluster> _clouds = new List<CloudCluster>(32);
        private readonly List<Material> _ownedMats = new List<Material>(16);
        private readonly List<Texture2D> _ownedTex = new List<Texture2D>(8);

        private Material _cloudTopMat;
        private Material _cloudUnderMat;
        private Material _sunCoreMat;
        private Material _sunHaloMat;
        private Material _moonMat;
        private Material _starMat;

        private Transform _celestialRoot;
        private Transform _sunDisc;
        private Transform _sunHalo;
        private Transform _moonDisc;
        private ParticleSystem _starFieldPs;
        private ParticleSystem.Particle[] _starParticles;
        private float[] _starBaseSizes;
        private float[] _starTwinklePhases;

        private ParticleSystem _dayMotesPs;
        private ParticleSystem.EmissionModule _dayMotesEm;
        private ParticleSystem _firefliesPs;
        private ParticleSystem.EmissionModule _firefliesEm;
        private ParticleSystem _leavesPs;
        private ParticleSystem.EmissionModule _leavesEm;
        private ParticleSystem _rainSplashPs;
        private ParticleSystem.EmissionModule _rainSplashEm;

        private Camera _cachedCam;
        private float _themeTimer;

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
        }

        private void Start()
        {
            BuildSkyAndClouds();
            BuildCelestialDome();
            BuildAmbientParticles();
            UpdateSkyAndAtmosphereColors(force: true);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _ownedMats.Count; i++)
            {
                if (_ownedMats[i] != null) Destroy(_ownedMats[i]);
            }
            for (int i = 0; i < _ownedTex.Count; i++)
            {
                if (_ownedTex[i] != null) Destroy(_ownedTex[i]);
            }
            _ownedMats.Clear();
            _ownedTex.Clear();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_cachedCam == null) _cachedCam = Camera.main;
            Vector3 focusPos = _cachedCam != null
                ? _cachedCam.transform.position
                : (PlayerModeController.Instance != null ? PlayerModeController.Instance.ActiveWorldPosition : Vector3.zero);

            // 1. Gently drift all 3D cumulus cloud clusters along the unified Istanbul wind field
            NeighborhoodWeatherState weather = NeighborhoodWeatherSystem.Instance != null
                ? NeighborhoodWeatherSystem.Instance.CurrentWeather
                : NeighborhoodWeatherState.Clear;
            float windMul = (0.85f + IstanbulLivingSoundscape.WindStrength01 * 1.45f) *
                            (weather == NeighborhoodWeatherState.SummerShower ? 1.25f : 1.0f);
            Vector3 windDir = IstanbulLivingSoundscape.WindDirection;

            for (int i = 0; i < _clouds.Count; i++)
            {
                CloudCluster c = _clouds[i];
                if (c.Root == null) continue;
                Vector3 p = c.Root.position;
                p.x += c.Speed * windDir.x * windMul * dt;
                p.z += c.Speed * windDir.z * windMul * dt;
                if (p.x > 440f) p.x = -440f;
                else if (p.x < -440f) p.x = 440f;
                if (p.z > 440f) p.z = -440f;
                else if (p.z < -440f) p.z = 440f;
                p.y = c.BaseY + Mathf.Sin(Time.time * 0.18f + c.BobPhase) * 0.8f;
                c.Root.position = p;
            }

            // 2. Keep Celestial Dome (Sun, Moon, Stars) centered on camera XZ
            if (_celestialRoot != null)
            {
                _celestialRoot.position = new Vector3(focusPos.x, 0f, focusPos.z);
                UpdateCelestialObjects(focusPos);
            }

            // 3. Periodic smooth tinting & ambient particle modulation (~7 Hz)
            _themeTimer -= dt;
            if (_themeTimer <= 0f)
            {
                _themeTimer = 0.14f;
                UpdateSkyAndAtmosphereColors(force: false);
                UpdateAmbientParticleEmitters(weather);
            }
        }

        // =====================================================================
        // 1. SCULPTED 3D VOLUMETRIC-STYLE DRIFTING CUMULUS CLOUDS
        // =====================================================================
        private void BuildSkyAndClouds()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _cloudTopMat = MakeSimpleMat(unlit, new Color(0.98f, 0.97f, 0.95f, 0.92f), transparent: true);
            _cloudUnderMat = MakeSimpleMat(unlit, new Color(0.84f, 0.86f, 0.92f, 0.88f), transparent: true);

            Transform cloudRoot = new GameObject("Istanbul_3D_Clouds").transform;
            cloudRoot.SetParent(transform, false);

            GameObject tmpSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh sphereMesh = tmpSphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmpSphere);

            System.Random rng = new System.Random(2026);
            int cloudCount = IsLowTier ? 16 : 26;

            for (int i = 0; i < cloudCount; i++)
            {
                Transform cRoot = new GameObject($"Cloud_{i:00}").transform;
                cRoot.SetParent(cloudRoot, false);

                float x = (float)(rng.NextDouble() * 820.0 - 410.0);
                float z = (float)(rng.NextDouble() * 820.0 - 410.0);
                float y = 78f + (float)rng.NextDouble() * 44f;
                cRoot.position = new Vector3(x, y, z);
                cRoot.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);

                float mainScale = 14f + (float)rng.NextDouble() * 16f;
                int puffs = 4 + (i % 3);

                // Flattened warm shaded base puff
                CreateCloudPuff(cRoot, sphereMesh, _cloudUnderMat, Vector3.zero,
                    new Vector3(mainScale * 2.1f, mainScale * 0.44f, mainScale * 1.25f));

                // Sunlit layered billowy top puffs
                for (int p = 0; p < puffs; p++)
                {
                    float u = puffs > 1 ? (p / (float)(puffs - 1)) * 2f - 1f : 0f;
                    float px = u * mainScale * 0.72f + (float)(rng.NextDouble() * 2.5 - 1.25);
                    float pz = (float)(rng.NextDouble() * mainScale * 0.36 - mainScale * 0.18);
                    float py = mainScale * (0.20f + (1f - Mathf.Abs(u) * 0.55f) * 0.28f);
                    float sx = mainScale * (0.85f + (1f - Mathf.Abs(u) * 0.35f) * 0.52f);
                    float sy = mainScale * (0.48f + (1f - Mathf.Abs(u) * 0.45f) * 0.32f);
                    float sz = mainScale * (0.78f + (float)rng.NextDouble() * 0.25f);
                    CreateCloudPuff(cRoot, sphereMesh, _cloudTopMat, new Vector3(px, py, pz), new Vector3(sx, sy, sz));
                }

                _clouds.Add(new CloudCluster
                {
                    Root = cRoot,
                    Speed = 1.6f + (float)rng.NextDouble() * 2.2f,
                    BaseY = y,
                    BobPhase = (float)(rng.NextDouble() * Mathf.PI * 2.0)
                });
            }
        }

        private static void CreateCloudPuff(Transform parent, Mesh mesh, Material mat, Vector3 localPos, Vector3 scale)
        {
            GameObject go = new GameObject("Puff");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.lightProbeUsage = LightProbeUsage.Off;
        }

        // =====================================================================
        // 2. GLOWING SUN DISC, CORONA HALO, SILVER MOON & TWINKLING STARS
        // =====================================================================
        private void BuildCelestialDome()
        {
            _celestialRoot = new GameObject("Istanbul_CelestialDome").transform;
            _celestialRoot.SetParent(transform, false);

            Texture2D softGlow = BuildRadialTex(128, power: 2.2f);
            Texture2D crispDisc = BuildSharpDiscTex(128);
            Shader partShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");

            _sunCoreMat = MakeAdditiveGlowMat(partShader, crispDisc, new Color(1.0f, 0.96f, 0.84f, 1.0f));
            _sunHaloMat = MakeAdditiveGlowMat(partShader, softGlow, new Color(1.0f, 0.78f, 0.42f, 0.55f));
            _moonMat = MakeAdditiveGlowMat(partShader, crispDisc, new Color(0.84f, 0.92f, 1.0f, 0.92f));
            _starMat = MakeAdditiveGlowMat(partShader, softGlow, new Color(0.92f, 0.95f, 1.0f, 0.95f));

            _sunDisc = BuildSkyQuad("SunDisc", _celestialRoot, _sunCoreMat, 36f);
            _sunHalo = BuildSkyQuad("SunCoronaHalo", _celestialRoot, _sunHaloMat, 148f);
            _moonDisc = BuildSkyQuad("MoonDisc", _celestialRoot, _moonMat, 26f);

            // 110 Twinkling Night Stars in a single GPU ParticleSystem (1 draw call!)
            GameObject starGo = new GameObject("NightStarField");
            starGo.transform.SetParent(_celestialRoot, false);
            _starFieldPs = starGo.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule sm = _starFieldPs.main;
            sm.loop = false;
            sm.playOnAwake = false;
            sm.maxParticles = 120;
            sm.simulationSpace = ParticleSystemSimulationSpace.Local;
            ParticleSystem.EmissionModule se = _starFieldPs.emission;
            se.enabled = false;
            ParticleSystemRenderer spr = _starFieldPs.GetComponent<ParticleSystemRenderer>();
            if (spr != null)
            {
                spr.sharedMaterial = _starMat;
                spr.shadowCastingMode = ShadowCastingMode.Off;
                spr.receiveShadows = false;
            }

            int starCount = IsLowTier ? 60 : 110;
            _starParticles = new ParticleSystem.Particle[starCount];
            _starBaseSizes = new float[starCount];
            _starTwinklePhases = new float[starCount];
            System.Random rng = new System.Random(1923);

            for (int i = 0; i < starCount; i++)
            {
                float az = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float elev = Mathf.Deg2Rad * (18f + (float)rng.NextDouble() * 68f);
                float dist = 470f;
                Vector3 pos = new Vector3(
                    Mathf.Cos(az) * Mathf.Cos(elev) * dist,
                    Mathf.Sin(elev) * dist,
                    Mathf.Sin(az) * Mathf.Cos(elev) * dist);
                float sz = 2.6f + (float)rng.NextDouble() * 3.4f;
                _starBaseSizes[i] = sz;
                _starTwinklePhases[i] = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                _starParticles[i] = new ParticleSystem.Particle
                {
                    position = pos,
                    startSize = sz,
                    startLifetime = 99999f,
                    remainingLifetime = 99999f,
                    startColor = Color.Lerp(new Color(0.80f, 0.88f, 1.0f, 0f), new Color(1.0f, 0.94f, 0.82f, 0f), (float)rng.NextDouble())
                };
            }
            _starFieldPs.SetParticles(_starParticles, starCount);
        }

        private static Transform BuildSkyQuad(string name, Transform parent, Material mat, float scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.lightProbeUsage = LightProbeUsage.Off;
            }
            return go.transform;
        }

        private void UpdateCelestialObjects(Vector3 camPos)
        {
            float nf = DayNightCycle.Instance != null ? DayNightCycle.Instance.NightFactor : 0f;
            Light sun = RenderSettings.sun;
            Vector3 sunDir = sun != null ? -sun.transform.forward : new Vector3(0.4f, 0.6f, 0.5f).normalized;

            // Sun + Corona visible during daylight & golden hour
            float sunVis = Mathf.Clamp01(1f - nf * 1.15f);
            bool showSun = sunVis > 0.02f;
            if (_sunDisc != null)
            {
                if (_sunDisc.gameObject.activeSelf != showSun) _sunDisc.gameObject.SetActive(showSun);
                if (_sunHalo != null && _sunHalo.gameObject.activeSelf != showSun) _sunHalo.gameObject.SetActive(showSun);
                if (showSun)
                {
                    Vector3 sunPos = _celestialRoot.position + sunDir * 440f;
                    _sunDisc.position = sunPos;
                    _sunDisc.LookAt(camPos);
                    if (_sunHalo != null)
                    {
                        _sunHalo.position = _celestialRoot.position + sunDir * 446f;
                        _sunHalo.LookAt(camPos);
                    }
                }
            }

            // Moon visible during twilight & night
            float moonVis = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.85f, nf));
            bool showMoon = moonVis > 0.02f;
            if (_moonDisc != null)
            {
                if (_moonDisc.gameObject.activeSelf != showMoon) _moonDisc.gameObject.SetActive(showMoon);
                if (showMoon)
                {
                    Vector3 moonDir = new Vector3(-0.35f, 0.66f, 0.66f).normalized;
                    _moonDisc.position = _celestialRoot.position + moonDir * 445f;
                    _moonDisc.LookAt(camPos);
                }
            }

            // Gentle twinkling starfield at night
            if (_starFieldPs != null && _starParticles != null)
            {
                if (_starFieldPs.gameObject.activeSelf != showMoon) _starFieldPs.gameObject.SetActive(showMoon);
                if (showMoon)
                {
                    float t = Time.time;
                    for (int i = 0; i < _starParticles.Length; i++)
                    {
                        float tw = 0.55f + 0.45f * Mathf.Sin(t * 2.4f + _starTwinklePhases[i]);
                        Color32 c = _starParticles[i].startColor;
                        c.a = (byte)Mathf.RoundToInt(moonVis * tw * 235f);
                        _starParticles[i].startColor = c;
                        _starParticles[i].startSize = _starBaseSizes[i] * (0.85f + 0.25f * tw);
                    }
                    _starFieldPs.SetParticles(_starParticles, _starParticles.Length);
                }
            }
        }

        // =====================================================================
        // 3. LIVING OUTDOOR MICRO-ATMOSPHERE (MOTES, FIREFLIES, LEAVES, RAIN RIPPLES)
        // =====================================================================
        private void BuildAmbientParticles()
        {
            Texture2D softGlow = BuildRadialTex(64, power: 1.9f);
            Texture2D ringTex = BuildRippleTex(64);
            Shader partShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");

            Material motesMat = MakeAdditiveGlowMat(partShader, softGlow, Color.white);
            Material leafMat = MakeAlphaParticleMat(partShader, softGlow, Color.white);
            Material splashMat = MakeAdditiveGlowMat(partShader, ringTex, new Color(0.84f, 0.94f, 1.0f, 0.52f));

            // 1. Sunlit Golden Dust Motes / Pollen
            _dayMotesPs = BuildWorldBoxEmitter("Ambient_SunMotes", motesMat, maxP: IsLowTier ? 18 : 38,
                boxSize: new Vector3(18f, 6f, 18f), lifeMin: 3.5f, lifeMax: 6.5f, sizeMin: 0.05f, sizeMax: 0.11f,
                speedMin: 0.12f, speedMax: 0.35f, colA: new Color(1.0f, 0.92f, 0.66f, 0.50f), colB: new Color(1.0f, 0.98f, 0.86f, 0.38f),
                noiseStrength: 0.45f, out _dayMotesEm);

            // 2. Twilight & Night Glowing Fireflies (Ateş Böcekleri!)
            _firefliesPs = BuildWorldBoxEmitter("Ambient_Fireflies", motesMat, maxP: IsLowTier ? 16 : 36,
                boxSize: new Vector3(22f, 4.5f, 22f), lifeMin: 3.2f, lifeMax: 5.8f, sizeMin: 0.09f, sizeMax: 0.17f,
                speedMin: 0.25f, speedMax: 0.65f, colA: new Color(1.0f, 0.86f, 0.24f, 0.88f), colB: new Color(0.72f, 1.0f, 0.34f, 0.82f),
                noiseStrength: 1.15f, out _firefliesEm);

            // 3. Drifting Breeze Plane-Tree Leaves
            _leavesPs = BuildWorldBoxEmitter("Ambient_BreezeLeaves", leafMat, maxP: IsLowTier ? 10 : 22,
                boxSize: new Vector3(22f, 7f, 22f), lifeMin: 4.5f, lifeMax: 7.5f, sizeMin: 0.10f, sizeMax: 0.17f,
                speedMin: 0.45f, speedMax: 1.15f, colA: new Color(0.76f, 0.62f, 0.24f, 0.72f), colB: new Color(0.38f, 0.62f, 0.28f, 0.68f),
                noiseStrength: 0.85f, out _leavesEm);
            ParticleSystem.VelocityOverLifetimeModule leafVel = _leavesPs.velocityOverLifetime;
            leafVel.enabled = true;
            leafVel.x = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            leafVel.y = new ParticleSystem.MinMaxCurve(-0.55f, -0.22f);
            leafVel.z = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);

            // 4. Summer Shower Raindrop Ground Splash Ripples
            _rainSplashPs = BuildWorldBoxEmitter("Ambient_RainRipples", splashMat, maxP: IsLowTier ? 18 : 42,
                boxSize: new Vector3(16f, 0.1f, 16f), lifeMin: 0.28f, lifeMax: 0.45f, sizeMin: 0.18f, sizeMax: 0.34f,
                speedMin: 0f, speedMax: 0.02f, colA: new Color(0.84f, 0.94f, 1.0f, 0.55f), colB: new Color(0.95f, 0.98f, 1.0f, 0.45f),
                noiseStrength: 0f, out _rainSplashEm);
            ParticleSystemRenderer rsr = _rainSplashPs.GetComponent<ParticleSystemRenderer>();
            if (rsr != null) rsr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            ParticleSystem.SizeOverLifetimeModule rsol = _rainSplashPs.sizeOverLifetime;
            rsol.enabled = true;
            rsol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1f, 1.45f)));
        }

        private ParticleSystem BuildWorldBoxEmitter(
            string name,
            Material mat,
            int maxP,
            Vector3 boxSize,
            float lifeMin,
            float lifeMax,
            float sizeMin,
            float sizeMax,
            float speedMin,
            float speedMax,
            Color colA,
            Color colB,
            float noiseStrength,
            out ParticleSystem.EmissionModule emissionMod)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = maxP;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startColor = new ParticleSystem.MinMaxGradient(colA, colB);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            emissionMod = ps.emission;
            emissionMod.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxSize;

            ParticleSystem.ColorOverLifetimeModule colLife = ps.colorOverLifetime;
            colLife.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.22f), new GradientAlphaKey(1f, 0.72f), new GradientAlphaKey(0f, 1f) });
            colLife.color = g;

            if (noiseStrength > 0.01f)
            {
                ParticleSystem.NoiseModule noise = ps.noise;
                noise.enabled = true;
                noise.strength = noiseStrength;
                noise.frequency = 0.45f;
                noise.scrollSpeed = 0.25f;
            }

            ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
            if (pr != null)
            {
                pr.sharedMaterial = mat;
                pr.shadowCastingMode = ShadowCastingMode.Off;
                pr.receiveShadows = false;
            }
            return ps;
        }

        private void UpdateAmbientParticleEmitters(NeighborhoodWeatherState weather)
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null) return;
            Vector3 p = mode.ActiveWorldPosition;

            bool indoors = NeighborhoodWeatherSystem.IsPositionSheltered(p);

            float nf = DayNightCycle.Instance != null ? DayNightCycle.Instance.NightFactor : 0f;
            bool raining = weather == NeighborhoodWeatherState.SummerShower;
            bool overcast = weather == NeighborhoodWeatherState.BreezyOvercast;

            if (_dayMotesPs != null)
            {
                _dayMotesPs.transform.position = p + Vector3.up * 2.8f;
                _dayMotesEm.rateOverTime = (!indoors && !raining && nf < 0.65f) ? (IsLowTier ? 3.5f : 7.5f) : 0f;
            }

            if (_firefliesPs != null)
            {
                _firefliesPs.transform.position = p + Vector3.up * 2.0f;
                _firefliesEm.rateOverTime = (!indoors && !raining && nf > 0.35f) ? (IsLowTier ? 4.0f : 8.5f) : 0f;
            }

            if (_leavesPs != null)
            {
                _leavesPs.transform.position = p + Vector3.up * 4.5f;
                float leafRate = indoors ? 0f : (overcast ? 4.5f : (raining ? 2.5f : 1.6f));
                if (IsLowTier) leafRate *= 0.55f;
                _leavesEm.rateOverTime = leafRate;
            }

            if (_rainSplashPs != null)
            {
                _rainSplashPs.transform.position = new Vector3(p.x, Mathf.Max(0.05f, p.y + 0.04f), p.z);
                _rainSplashEm.rateOverTime = (!indoors && raining) ? (IsLowTier ? 16f : 38f) : 0f;
            }
        }

        private void UpdateSkyAndAtmosphereColors(bool force)
        {
            float hour = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentHour : 12f;
            float nf = DayNightCycle.Instance != null ? DayNightCycle.Instance.NightFactor : 0f;
            float golden = ComputeGoldenHourFactor(hour);

            NeighborhoodWeatherState weather = NeighborhoodWeatherSystem.Instance != null
                ? NeighborhoodWeatherSystem.Instance.CurrentWeather
                : NeighborhoodWeatherState.Clear;
            float stormDim = weather == NeighborhoodWeatherState.SummerShower ? 0.58f
                : (weather == NeighborhoodWeatherState.BreezyOvercast ? 0.78f : 1.0f);

            // Cloud top & underside colors smoothly follow sunrise -> noon -> golden hour -> moonlit night
            Color dayTop = new Color(0.99f, 0.98f, 0.96f, 0.90f);
            Color goldenTop = new Color(1.00f, 0.82f, 0.60f, 0.92f);
            Color nightTop = new Color(0.24f, 0.30f, 0.44f, 0.78f);

            Color dayUnder = new Color(0.76f, 0.82f, 0.90f, 0.86f);
            Color goldenUnder = new Color(0.86f, 0.54f, 0.46f, 0.88f);
            Color nightUnder = new Color(0.14f, 0.18f, 0.28f, 0.78f);

            Color topCol = Color.Lerp(Color.Lerp(dayTop, goldenTop, golden), nightTop, nf);
            Color underCol = Color.Lerp(Color.Lerp(dayUnder, goldenUnder, golden), nightUnder, nf);
            topCol.r *= stormDim; topCol.g *= stormDim; topCol.b *= Mathf.Lerp(stormDim, 1f, 0.22f);
            underCol.r *= stormDim; underCol.g *= stormDim; underCol.b *= Mathf.Lerp(stormDim, 1f, 0.28f);

            SetMatColor(_cloudTopMat, topCol);
            SetMatColor(_cloudUnderMat, underCol);

            // Sun core & warm corona intensity
            float sunAlpha = Mathf.Clamp01(1f - nf * 1.15f) * (weather == NeighborhoodWeatherState.SummerShower ? 0.22f : 1f);
            Color sunCore = Color.Lerp(new Color(1.0f, 0.97f, 0.88f, sunAlpha), new Color(1.0f, 0.76f, 0.46f, sunAlpha), golden);
            Color sunHalo = Color.Lerp(new Color(1.0f, 0.86f, 0.56f, sunAlpha * 0.48f), new Color(1.0f, 0.56f, 0.26f, sunAlpha * 0.74f), golden);
            SetMatColor(_sunCoreMat, sunCore);
            SetMatColor(_sunHaloMat, sunHalo);

            // Moon glow
            float moonAlpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.85f, nf)) * (weather == NeighborhoodWeatherState.SummerShower ? 0.35f : 0.92f);
            SetMatColor(_moonMat, new Color(0.86f, 0.93f, 1.0f, moonAlpha));
        }

        public static float ComputeGoldenHourFactor(float hour)
        {
            // Peaks at Sunrise (~06:30) and Bosphorus Sunset (~18:10)
            float sunrise = 1f - Mathf.Clamp01(Mathf.Abs(hour - 6.55f) / 1.35f);
            float sunset = 1f - Mathf.Clamp01(Mathf.Abs(hour - 18.10f) / 1.65f);
            return Mathf.SmoothStep(0f, 1f, Mathf.Max(sunrise, sunset));
        }

        private Material MakeSimpleMat(Shader sh, Color col, bool transparent)
        {
            Material m = new Material(sh);
            _ownedMats.Add(m);
            SetMatColor(m, col);
            if (transparent)
            {
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            return m;
        }

        private Material MakeAdditiveGlowMat(Shader sh, Texture2D tex, Color col)
        {
            Material m = new Material(sh);
            _ownedMats.Add(m);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            SetMatColor(m, col);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_BLENDMODE_ADD");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        private Material MakeAlphaParticleMat(Shader sh, Texture2D tex, Color col)
        {
            Material m = new Material(sh);
            _ownedMats.Add(m);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            SetMatColor(m, col);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        private static void SetMatColor(Material m, Color col)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        }

        private Texture2D BuildRadialTex(int size, float power)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            _ownedTex.Add(tex);
            Color[] pix = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), power);
                    pix[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Texture2D BuildSharpDiscTex(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            _ownedTex.Add(tex);
            Color[] pix = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float core = 1f - Mathf.SmoothStep(0.36f, 0.56f, d);
                    float halo = Mathf.Pow(Mathf.Clamp01(1f - d), 3.0f) * 0.55f;
                    pix[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(core + halo));
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Texture2D BuildRippleTex(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            _ownedTex.Add(tex);
            Color[] pix = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float ring = Mathf.Exp(-Mathf.Pow((d - 0.76f) / 0.12f, 2f));
                    pix[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(ring));
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }
    }
}
