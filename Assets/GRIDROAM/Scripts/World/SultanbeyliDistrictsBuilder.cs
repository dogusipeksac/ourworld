using System;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Builds 6 authentic neighboring Sultanbeyli, Istanbul districts surrounding Hamidiye Mahallesi
    /// modeled after real Sultanbeyli geography, landmarks & architectural character (zero scene edits):
    ///
    /// 1. ABDURRAHMANGAZİ MAHALLESİ (NE · +180, +195):
    ///    Sultanbeyli Kent Meydanı pedestrian plaza, Sultanbeyli Belediyesi modern glass-travertine civic building,
    ///    Abdurrahmangazi Merkez Camii (domed mosque, twin minarets, interactive Şadırvan), central animated fountain,
    ///    Meydan Çay & Simit stand, surrounding commercial/residential apartment blocks.
    ///
    /// 2. MEHMET AKİF MAHALLESİ (N · -6, +285):
    ///    Fatih Bulvarı north spine, iconic M5 Sultanbeyli Metro Station (red-white 'M' pylon totem, glass canopy,
    ///    escalator stairs & IETT stop), Erdoğan Caddesi retail strip (interactive Pastane bakery, Eczane, Kırtasiye, Kafe),
    ///    Mehmet Akif Ersoy İlkokulu schoolyard & basketball hoop.
    ///
    /// 3. HASANPAŞA MAHALLESİ (NW · -185, +195):
    ///    M5 Hasanpaşa Metro Station entrance, Hasanpaşa Millet Parkı (terracotta jogging track, outdoor yellow-blue
    ///    fitness gear, shade pergolas, kids' swings & slide, interactive park kiosk), modern urban-renewal residences.
    ///
    /// 4. BATTALGAZİ MAHALLESİ (SW · -185, -185):
    ///    Bosna Bulvarı, vibrant Battalgazi Semt Pazarı (8 colorful striped produce stalls with interactive fresh
    ///    fruit/veggie bag packed into PlayerBackpackInventory!), Battalgazi Kültür Merkezi, fenced neighborhood
    ///    street football mini-pitch with playable StreetFootball & FootballGoal nets.
    ///
    /// 5. MİMAR SİNAN MAHALLESİ (S · -52, -282):
    ///    Mimar Sinan Kültür Parkı (full kids' playground: twin swings, slide tower, seesaw, sandbox & parent benches),
    ///    historic arched stone Mimar Sinan Çeşmesi (interactive cold water), Maraş ice-cream cart, Mimar Sinan İlkokulu & Mescit.
    ///
    /// 6. TURGUT REİS MAHALLESİ & AYDOS KALESİ (SE · +238, -138):
    ///    Demokrasi Caddesi, walkable green pine hill rising to historic stone Aydos Kalesi (Byzantine/Ottoman fortress walls,
    ///    crenellated battlements, 2 watchtowers, arched gate, panoramic viewing deck & interactive viewpoint!),
    ///    pine picnic grove & Kale Çay Bahçesi, plus Turgut Reis Fuel Station (real working FuelPump!).
    ///
    /// Also connects all 6 districts with drivable paved ring/radial boulevards, Istanbul neighborhood welcome arches,
    /// night-lit streetlamps, and a live GTA/Forza-style Neighborhood Entry announcement banner!
    /// </summary>
    public sealed class SultanbeyliDistrictsBuilder : MonoBehaviour
    {
        public static SultanbeyliDistrictsBuilder Instance { get; private set; }

        private struct DistrictZone
        {
            public string Id;
            public string TitleTr;
            public string TitleEn;
            public Vector3 Center;
            public float Radius;
        }

        private static readonly DistrictZone[] DistrictZones =
        {
            new DistrictZone { Id = "hamidiye",        TitleTr = "Hamidiye Mahallesi · Merkez",             TitleEn = "Hamidiye District · Center",           Center = new Vector3(   0f, 0f,    0f), Radius = 128f },
            new DistrictZone { Id = "abdurrahmangazi", TitleTr = "Abdurrahmangazi Mah. · Kent Meydanı",     TitleEn = "Abdurrahmangazi · Sultanbeyli Square", Center = new Vector3( 180f, 0f,  195f), Radius =  64f },
            new DistrictZone { Id = "mehmetakif",      TitleTr = "Mehmet Akif Mah. · M5 Sultanbeyli Metro", TitleEn = "Mehmet Akif · M5 Sultanbeyli Metro",   Center = new Vector3(  -6f, 0f,  285f), Radius =  66f },
            new DistrictZone { Id = "hasanpasa",       TitleTr = "Hasanpaşa Mah. · Millet Parkı & Metro",   TitleEn = "Hasanpaşa · Millet Park & M5 Metro",   Center = new Vector3(-185f, 0f,  195f), Radius =  64f },
            new DistrictZone { Id = "battalgazi",      TitleTr = "Battalgazi Mah. · Bosna Blv. & Pazar",    TitleEn = "Battalgazi · Bosna Blvd & Market",     Center = new Vector3(-185f, 0f, -185f), Radius =  64f },
            new DistrictZone { Id = "mimarsinan",      TitleTr = "Mimar Sinan Mah. · Kültür Parkı",         TitleEn = "Mimar Sinan · Culture Park",           Center = new Vector3( -52f, 0f, -282f), Radius =  64f },
            new DistrictZone { Id = "turgutreis",      TitleTr = "Turgut Reis Mah. · Tarihi Aydos Kalesi",  TitleEn = "Turgut Reis · Historic Aydos Castle",  Center = new Vector3( 238f, 0f, -138f), Radius =  68f }
        };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly List<Material> _mats = new List<Material>(48);
        private readonly List<Texture2D> _textures = new List<Texture2D>(24);
        private readonly List<Transform> _flags = new List<Transform>(10);
        private readonly List<Transform> _fountainJets = new List<Transform>(8);

        // Shared PBR materials
        private Material _asphaltMat;
        private Material _laneWhiteMat;
        private Material _sidewalkMat;
        private Material _plazaTravertineMat;
        private Material _parkGrassMat;
        private Material _joggingTrackMat;
        private Material _stoneFortressMat;
        private Material _stoneDarkMat;
        private Material _roofTileMat;
        private Material _mosqueDomeMat;
        private Material _glassBlueMat;
        private Material _windowLitMat;
        private Material _woodMat;
        private Material _darkMetalMat;
        private Material _whiteTrimMat;
        private Material _metroRedMat;
        private Material _istanbulBlueMat;
        private Material _turkishRedMat;
        private Material _goldAccentMat;
        private Material _waterBlueMat;
        private Material _pineMat;
        private Material _planeLeafMat;
        private Material _trunkMat;
        private Material _sandMat;
        private Material _lampBulbMat;
        private Material[] _plasterPalette;
        private Material[] _canopyPalette;
        private Material[] _producePalette;

        private string _currentDistrictId = "hamidiye";
        private float _districtCheckTimer = 1.5f;
        private float _lastDistrictToastTime = -20f;
        private float _lastInteractTime = -5f;
        private bool _subscribed;

        public string CurrentDistrictId => _currentDistrictId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;

            GameObject go = new GameObject("OurWorld_SultanbeyliDistricts");
            go.AddComponent<SultanbeyliDistrictsBuilder>();
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

        private void Start()
        {
            BuildMaterials();

            Transform worldRoot = new GameObject("Sultanbeyli_6_Districts_World").transform;
            worldRoot.SetParent(transform, false);

            // Connect Hamidiye cleanly to all 6 surrounding neighborhoods with drivable roads
            BuildConnectingBoulevardNetwork(worldRoot);

            // Build all 6 authentic Sultanbeyli districts!
            BuildAbdurrahmangaziDistrict(worldRoot);
            BuildMehmetAkifDistrict(worldRoot);
            BuildHasanpasaDistrict(worldRoot);
            BuildBattalgaziDistrict(worldRoot);
            BuildMimarSinanDistrict(worldRoot);
            BuildTurgutReisAndAydosCastleDistrict(worldRoot);

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted += HandleDistrictInteracted;
                PlayerModeController.Instance.RefreshInteractablesCache();
                _subscribed = true;
            }

            Debug.Log("[SultanbeyliDistrictsBuilder] Built 6 authentic neighboring Sultanbeyli districts (Abdurrahmangazi, Mehmet Akif, Hasanpaşa, Battalgazi, Mimar Sinan, Turgut Reis & Aydos Kalesi) + connecting boulevards.");
        }

        private void OnDestroy()
        {
            if (_subscribed && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted -= HandleDistrictInteracted;
            }
            for (int i = 0; i < _mats.Count; i++)
            {
                if (_mats[i] != null) Destroy(_mats[i]);
            }
            for (int i = 0; i < _textures.Count; i++)
            {
                if (_textures[i] != null) Destroy(_textures[i]);
            }
            _mats.Clear();
            _textures.Clear();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Animate waving Turkish flags synced with real wind strength
            float wind = 0.6f + IstanbulLivingSoundscape.WindStrength01 * 1.4f;
            for (int i = 0; i < _flags.Count; i++)
            {
                Transform f = _flags[i];
                if (f == null) continue;
                float sway = Mathf.Sin(Time.time * (2.8f + wind) + i * 1.3f) * (8f * wind);
                f.localRotation = Quaternion.Euler(0f, sway, Mathf.Sin(Time.time * 4.2f + i) * 3f);
            }

            // Animate plaza fountain water jets softly
            for (int i = 0; i < _fountainJets.Count; i++)
            {
                Transform j = _fountainJets[i];
                if (j == null) continue;
                Vector3 sc = j.localScale;
                sc.y = 1.05f + Mathf.Sin(Time.time * 4.6f + i * 1.1f) * 0.16f;
                j.localScale = sc;
            }

            // Live GTA/Forza-style Neighborhood Transition Banner Tracker
            _districtCheckTimer -= dt;
            if (_districtCheckTimer > 0f) return;
            _districtCheckTimer = 0.65f;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsModalLocked) return;

            Vector3 p = mode.ActiveWorldPosition;
            string detected = ResolveDistrictAt(p, out DistrictZone matched);
            if (!string.IsNullOrEmpty(detected) && detected != _currentDistrictId)
            {
                _currentDistrictId = detected;
                if (Time.time - _lastDistrictToastTime >= 7.0f)
                {
                    _lastDistrictToastTime = Time.time;
                    bool en = LocalizationService.IsEnglish;
                    string label = en ? matched.TitleEn : matched.TitleTr;
                    mode.RaiseStatusMessage($"📍 {label}");
                    GameFeelAudio.PlayUiClick();
                }
            }
        }

        private static string ResolveDistrictAt(Vector3 pos, out DistrictZone matched)
        {
            matched = default;
            float bestDistSq = float.MaxValue;
            string bestId = null;
            for (int i = 0; i < DistrictZones.Length; i++)
            {
                DistrictZone z = DistrictZones[i];
                float dx = pos.x - z.Center.x;
                float dz = pos.z - z.Center.z;
                float dSq = dx * dx + dz * dz;
                if (dSq <= z.Radius * z.Radius && dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestId = z.Id;
                    matched = z;
                }
            }
            return bestId;
        }

        // =====================================================================
        // MATERIALS
        // =====================================================================
        private Material CreateMat(Shader sh, string name, Color col, float smoothness = 0.22f, float metallic = 0f, Color? emission = null)
        {
            Material m = new Material(sh) { name = name };
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, col);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, col);
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, smoothness);
            if (m.HasProperty(MetallicId)) m.SetFloat(MetallicId, metallic);
            if (emission.HasValue && m.HasProperty(EmissionColorId))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionColorId, emission.Value);
            }
            _mats.Add(m);
            return m;
        }

        private void BuildMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (lit == null) return;

            _asphaltMat = CreateMat(lit, "MAT_Dist_Asphalt", new Color(0.22f, 0.23f, 0.25f), 0.20f);
            _laneWhiteMat = CreateMat(lit, "MAT_Dist_LaneMark", new Color(0.92f, 0.92f, 0.90f), 0.15f);
            _sidewalkMat = CreateMat(lit, "MAT_Dist_Sidewalk_Parke", new Color(0.72f, 0.69f, 0.64f), 0.22f);
            _plazaTravertineMat = CreateMat(lit, "MAT_Dist_Plaza_Travertine", new Color(0.84f, 0.80f, 0.72f), 0.28f);
            _parkGrassMat = CreateMat(lit, "MAT_Dist_Park_Grass", new Color(0.28f, 0.56f, 0.24f), 0.12f);
            _joggingTrackMat = CreateMat(lit, "MAT_Dist_Track", new Color(0.72f, 0.29f, 0.22f), 0.26f);
            _stoneFortressMat = CreateMat(lit, "MAT_Dist_Fortress_Stone", new Color(0.64f, 0.60f, 0.53f), 0.14f);
            _stoneDarkMat = CreateMat(lit, "MAT_Dist_StoneDark", new Color(0.42f, 0.40f, 0.37f), 0.16f);
            _roofTileMat = CreateMat(lit, "MAT_Dist_RoofTile_Kiremit", new Color(0.68f, 0.30f, 0.19f), 0.22f);
            _mosqueDomeMat = CreateMat(lit, "MAT_Dist_MosqueDome", new Color(0.56f, 0.60f, 0.64f), 0.45f, 0.35f);
            _glassBlueMat = CreateMat(lit, "MAT_Dist_Glass", new Color(0.20f, 0.44f, 0.62f), 0.86f, 0.28f);
            _windowLitMat = CreateMat(lit, "MAT_Dist_WindowGlass", new Color(0.22f, 0.34f, 0.46f), 0.82f, 0.20f);
            _woodMat = CreateMat(lit, "MAT_Dist_Wood_Pergola", new Color(0.54f, 0.35f, 0.19f), 0.24f);
            _darkMetalMat = CreateMat(lit, "MAT_Dist_DarkMetal", new Color(0.18f, 0.20f, 0.24f), 0.55f, 0.65f);
            _whiteTrimMat = CreateMat(lit, "MAT_Dist_WhiteTrim", new Color(0.93f, 0.92f, 0.89f), 0.26f);
            _metroRedMat = CreateMat(lit, "MAT_Dist_MetroRed", new Color(0.84f, 0.14f, 0.16f), 0.42f);
            _istanbulBlueMat = CreateMat(lit, "MAT_Dist_IstanbulBlue", new Color(0.10f, 0.34f, 0.68f), 0.38f);
            _turkishRedMat = CreateMat(lit, "MAT_Dist_TurkishRed", new Color(0.86f, 0.08f, 0.12f), 0.22f);
            _goldAccentMat = CreateMat(lit, "MAT_Dist_Gold", new Color(0.92f, 0.74f, 0.24f), 0.76f, 0.72f);
            _waterBlueMat = CreateMat(lit, "MAT_Dist_FountainWater", new Color(0.22f, 0.72f, 0.92f), 0.88f, 0.10f, new Color(0.08f, 0.26f, 0.36f));
            _pineMat = CreateMat(lit, "MAT_Dist_PineLeaf", new Color(0.16f, 0.38f, 0.20f), 0.12f);
            _planeLeafMat = CreateMat(lit, "MAT_Dist_PlaneLeaf", new Color(0.26f, 0.52f, 0.22f), 0.14f);
            _trunkMat = CreateMat(lit, "MAT_Dist_Trunk", new Color(0.36f, 0.25f, 0.16f), 0.12f);
            _sandMat = CreateMat(lit, "MAT_Dist_Sandbox_Sand", new Color(0.86f, 0.77f, 0.56f), 0.14f);
            _lampBulbMat = CreateMat(lit, "MAT_Dist_LampBulb", new Color(1.0f, 0.90f, 0.64f), 0.5f, 0f, new Color(1.0f, 0.84f, 0.46f) * 1.4f);

            // Warm authentic Sultanbeyli apartment facade plaster palette
            _plasterPalette = new[]
            {
                CreateMat(lit, "MAT_Dist_Plaster_Cream",     new Color(0.91f, 0.86f, 0.76f), 0.16f),
                CreateMat(lit, "MAT_Dist_Plaster_Apricot",   new Color(0.90f, 0.74f, 0.58f), 0.16f),
                CreateMat(lit, "MAT_Dist_Plaster_Terracotta",new Color(0.82f, 0.56f, 0.44f), 0.16f),
                CreateMat(lit, "MAT_Dist_Plaster_Sage",      new Color(0.75f, 0.82f, 0.74f), 0.16f),
                CreateMat(lit, "MAT_Dist_Plaster_SkyGrey",   new Color(0.76f, 0.80f, 0.84f), 0.18f),
                CreateMat(lit, "MAT_Dist_Plaster_Anthracite",new Color(0.34f, 0.37f, 0.42f), 0.22f)
            };

            _canopyPalette = new[]
            {
                CreateMat(lit, "MAT_Dist_Canopy_Red",    new Color(0.84f, 0.22f, 0.20f), 0.18f),
                CreateMat(lit, "MAT_Dist_Canopy_Orange", new Color(0.94f, 0.52f, 0.14f), 0.18f),
                CreateMat(lit, "MAT_Dist_Canopy_Blue",   new Color(0.18f, 0.46f, 0.82f), 0.18f),
                CreateMat(lit, "MAT_Dist_Canopy_Green",  new Color(0.18f, 0.60f, 0.34f), 0.18f),
                CreateMat(lit, "MAT_Dist_Canopy_Yellow", new Color(0.94f, 0.76f, 0.16f), 0.18f)
            };

            _producePalette = new[]
            {
                CreateMat(lit, "MAT_Dist_Prod_Tomato", new Color(0.88f, 0.18f, 0.14f), 0.38f),
                CreateMat(lit, "MAT_Dist_Prod_Orange", new Color(0.96f, 0.54f, 0.08f), 0.36f),
                CreateMat(lit, "MAT_Dist_Prod_Apple",  new Color(0.34f, 0.76f, 0.22f), 0.42f),
                CreateMat(lit, "MAT_Dist_Prod_Lemon",  new Color(0.96f, 0.86f, 0.18f), 0.36f)
            };
        }

        // =====================================================================
        // CONNECTING SULTANBEYLİ BOULEVARD & RING ROAD NETWORK
        // =====================================================================
        private void BuildConnectingBoulevardNetwork(Transform root)
        {
            Transform roadRoot = new GameObject("Sultanbeyli_Connecting_Boulevards").transform;
            roadRoot.SetParent(root, false);

            // 1. Fatih Bulvarı North Extension (Havadis Z=186 -> Mehmet Akif Z=276)
            BuildRoadSegment(roadRoot, "Road_FatihBlvd_North", new Vector3(-6f, 0f, 232f), 11.5f, 88f, alongZ: true);

            // 2. Havadis -> Abdurrahmangazi East Boulevard (X=64 -> X=180 along Z=186)
            BuildRoadSegment(roadRoot, "Road_Abdurrahman_West", new Vector3(114f, 0f, 186f), 11.0f, 108f, alongZ: false);

            // 3. Aydos Çiftlik -> Abdurrahmangazi NE Connector (X=138, Z=50 -> Z=186)
            BuildRoadSegment(roadRoot, "Road_Ciftlik_Abdurrahman", new Vector3(138f, 0f, 118f), 10.5f, 136f, alongZ: true);

            // 4. Havadis -> Hasanpaşa West Boulevard (X=-185 -> X=-64 along Z=186)
            BuildRoadSegment(roadRoot, "Road_Hasanpasa_East", new Vector3(-124f, 0f, 186f), 11.0f, 124f, alongZ: false);

            // 5. NW Avenue (Lake/Oto Galeri gap X=-142, Z=44 -> Z=186)
            BuildRoadSegment(roadRoot, "Road_NW_Hasanpasa", new Vector3(-142f, 0f, 115f), 10.0f, 142f, alongZ: true);

            // 6. SW Avenue (West of Atlas AVM X=-142, Z=-186 -> Z=-44)
            BuildRoadSegment(roadRoot, "Road_SW_Battalgazi", new Vector3(-142f, 0f, -115f), 10.0f, 142f, alongZ: true);

            // 7. Kadife Sk. -> Battalgazi Bosna Bulvarı (X=-186 -> X=-64 along Z=-186)
            BuildRoadSegment(roadRoot, "Road_BosnaBlvd_East", new Vector3(-124f, 0f, -186f), 11.0f, 124f, alongZ: false);

            // 8. Kadife Sk. -> Mimar Sinan South Connector (X=-52, Z=-262 -> Z=-186)
            BuildRoadSegment(roadRoot, "Road_MimarSinan_North", new Vector3(-52f, 0f, -224f), 10.5f, 76f, alongZ: true);

            // 9. Aydos Çiftlik -> Turgut Reis SE Boulevard (X=214, Z=-148 -> Z=-46)
            BuildRoadSegment(roadRoot, "Road_TurgutReis_North", new Vector3(214f, 0f, -97f), 10.5f, 102f, alongZ: true);

            // 10. Turgut Reis -> Hamidiye East Connector (X=146 -> X=214 along Z=-148, clear of Shooting Range X<=140!)
            BuildRoadSegment(roadRoot, "Road_Demokrasi_West", new Vector3(184f, 0f, -148f), 10.5f, 74f, alongZ: false);
        }

        // =====================================================================
        // DISTRICT 1: ABDURRAHMANGAZİ MAHALLESİ (NE · +180, +195)
        // Sultanbeyli Kent Meydanı · Belediye Binası · Abdurrahmangazi Merkez Camii
        // =====================================================================
        private void BuildAbdurrahmangaziDistrict(Transform root)
        {
            Transform d = new GameObject("District_01_Abdurrahmangazi").transform;
            d.SetParent(root, false);

            // Neighborhood Welcome Arch at West Entry
            BuildWelcomeArch(d, new Vector3(144f, 0f, 186f), 90f, "ABDURRAHMANGAZİ MAH.", "SULTANBEYLİ KENT MEYDANI", _istanbulBlueMat);

            // Main Boulevard & Central Pedestrian Kent Meydanı Plaza
            BuildRoadSegment(d, "Road_Abdurrahman_Main", new Vector3(180f, 0f, 174f), 10.5f, 92f, alongZ: false);
            BuildSlab(d, "Abdurrahman_KentMeydani_Plaza", new Vector3(180f, 0.05f, 198f), new Vector3(54f, 0.10f, 36f), _plazaTravertineMat, solid: true);

            // Illuminated Central Tiered Marble Fountain at (180, 196)
            BuildPlazaFountain(d, new Vector3(180f, 0.10f, 196f), radius: 4.4f);

            // Sultanbeyli Belediyesi Modern 4-Story Civic Building (180, 224)
            BuildCivicHall(d, new Vector3(180f, 0.05f, 224f), 180f, "SULTANBEYLİ BELEDİYESİ", "T.C. SULTANBEYLİ");
            BuildFlagpole(d, new Vector3(167f, 0.10f, 213f), 8.2f);
            BuildFlagpole(d, new Vector3(193f, 0.10f, 213f), 8.2f);

            // Abdurrahmangazi Merkez Camii (218, 198) + Interactive Şadırvan
            BuildOttomanMosque(d, new Vector3(218f, 0.05f, 198f), -90f, "ABDURRAHMANGAZİ MERKEZ CAMİİ");
            BuildInteractiveFountain(d, new Vector3(204f, 0.10f, 198f), -90f,
                "district_abdurrahman_fountain", "Şadırvandan Su İç (Ücretsiz)", "Drink Cold Fountain Water (Free)");

            // Interactive Meydan Çay & Simit Köşkü (158, 188)
            BuildInteractiveKiosk(d, new Vector3(158f, 0.10f, 188f), 90f,
                "MEYDAN ÇAY & SİMİT", _canopyPalette[0],
                "district_abdurrahman_tea", "Taze Çay & Çıtır Simit Al (15 ₺)", "Buy Hot Tea & Simit (15 ₺)");

            // Surrounding 4–5 Story Istanbul Residential & Commercial Apartment Blocks
            BuildIstanbulApartment(d, new Vector3(148f, 0f, 216f),   0f, 14f, 14.5f, 11f, 0, "ECZANE");
            BuildIstanbulApartment(d, new Vector3(154f, 0f, 158f),   0f, 15f, 12.5f, 11f, 1, "KUYUMCU");
            BuildIstanbulApartment(d, new Vector3(180f, 0f, 158f),   0f, 16f, 15.0f, 11f, 2, "MEYDAN KAFE");
            BuildIstanbulApartment(d, new Vector3(206f, 0f, 158f),   0f, 14f, 13.0f, 11f, 3, "BÖREKÇİ");
            BuildIstanbulApartment(d, new Vector3(212f, 0f, 224f), 180f, 15f, 14.0f, 11f, 4, "SULTAN MARKET");

            // Park Benches (Sittable!), Plane Trees & Night Streetlamps
            BuildSittableBench(d, new Vector3(170f, 0.10f, 196f),  90f, "abdurrahman_0");
            BuildSittableBench(d, new Vector3(190f, 0.10f, 196f), -90f, "abdurrahman_1");
            BuildSittableBench(d, new Vector3(180f, 0.10f, 184f),   0f, "abdurrahman_2");
            BuildSittableBench(d, new Vector3(180f, 0.10f, 208f), 180f, "abdurrahman_3");

            Vector3[] trees = { new(161f, 0.1f, 206f), new(199f, 0.1f, 206f), new(162f, 0.1f, 182f), new(198f, 0.1f, 182f), new(202f, 0.1f, 168f) };
            for (int i = 0; i < trees.Length; i++) BuildPlaneTree(d, trees[i], 1.0f + (i % 2) * 0.18f);

            BuildStreetLamp(d, new Vector3(165f, 0.1f, 181f));
            BuildStreetLamp(d, new Vector3(195f, 0.1f, 181f));
            BuildStreetLamp(d, new Vector3(180f, 0.1f, 211f));
            BuildStreetLamp(d, new Vector3(207f, 0.1f, 192f));
        }

        // =====================================================================
        // DISTRICT 2: MEHMET AKİF MAHALLESİ (N · -6, +285)
        // M5 Sultanbeyli Metro · Erdoğan Caddesi Çarşı · Mehmet Akif Ersoy İlkokulu
        // =====================================================================
        private void BuildMehmetAkifDistrict(Transform root)
        {
            Transform d = new GameObject("District_02_MehmetAkif").transform;
            d.SetParent(root, false);

            BuildWelcomeArch(d, new Vector3(-6f, 0f, 244f), 0f, "MEHMET AKİF MAH.", "FATİH BULVARI & M5 METRO", _metroRedMat);

            // Erdoğan Caddesi East-West Commercial Avenue + North Spine
            BuildRoadSegment(d, "Road_ErdoganCd_Main", new Vector3(-6f, 0f, 276f), 11.0f, 108f, alongZ: false);
            BuildSlab(d, "MehmetAkif_Metro_Plaza", new Vector3(-6f, 0.05f, 258f), new Vector3(38f, 0.10f, 18f), _plazaTravertineMat, solid: true);
            BuildSlab(d, "ErdoganCd_Sidewalk_North", new Vector3(-6f, 0.05f, 284f), new Vector3(106f, 0.10f, 5.5f), _sidewalkMat, solid: true);
            BuildSlab(d, "ErdoganCd_Sidewalk_South", new Vector3(-6f, 0.05f, 268f), new Vector3(106f, 0.10f, 5.5f), _sidewalkMat, solid: true);

            // Iconic M5 Sultanbeyli Metro Station Entrance + Red-White 'M' Totem + IETT Bus Stop
            BuildMetroStationEntrance(d, new Vector3(-6f, 0.10f, 256f), 0f, "M5 SULTANBEYLİ METROSU");
            BuildBusStopShelter(d, new Vector3(-26f, 0.10f, 267f), 0f, "İETT · SULTANBEYLİ MERKEZ");

            // Erdoğan Caddesi Retail Apartments & Interactive Stone Bakery / Pastane!
            BuildIstanbulApartment(d, new Vector3(-44f, 0f, 294f), 180f, 15f, 13.5f, 11f, 1, "KIRTASİYE");
            BuildIstanbulApartment(d, new Vector3(-24f, 0f, 294f), 180f, 16f, 15.0f, 11f, 0, "MEHMET AKİF PASTANE");
            BuildIstanbulApartment(d, new Vector3( -4f, 0f, 294f), 180f, 15f, 14.0f, 11f, 4, "NÖBETÇİ ECZANE");
            BuildIstanbulApartment(d, new Vector3(-42f, 0f, 254f),   0f, 15f, 12.5f, 11f, 2, "ERDOĞAN KAFE");
            BuildIstanbulApartment(d, new Vector3( 28f, 0f, 258f),   0f, 16f, 14.0f, 11f, 3, "FATİH MANAV");

            // Interactive Pastane Counter outside Mehmet Akif Pastane
            BuildInteractiveKiosk(d, new Vector3(-24f, 0.10f, 285.5f), 180f,
                "TAZE POĞAÇA & BÖREK", _canopyPalette[1],
                "district_mehmetakif_bakery", "Sıcak Poğaça Paketi Al (25 ₺ · Çantaya)", "Buy Warm Pastry Pack (25 ₺ · Backpack)");

            // Mehmet Akif Ersoy İlkokulu (+24, +316) with Schoolyard, Flagpole & Basketball Hoop!
            BuildTurkishSchool(d, new Vector3(24f, 0.05f, 318f), 180f, "MEHMET AKİF ERSOY İLKOKULU");
            BuildSlab(d, "MehmetAkif_Schoolyard_Parke", new Vector3(24f, 0.05f, 304f), new Vector3(28f, 0.10f, 18f), _sidewalkMat, solid: true);
            BuildFlagpole(d, new Vector3(13f, 0.10f, 306f), 7.5f);
            BuildMiniBasketballHoop(d, new Vector3(34f, 0.10f, 304f), -90f);

            BuildSittableBench(d, new Vector3(-16f, 0.10f, 261f), 0f, "mehmetakif_0");
            BuildSittableBench(d, new Vector3(  4f, 0.10f, 261f), 0f, "mehmetakif_1");
            BuildSittableBench(d, new Vector3( 14f, 0.10f, 298f), 0f, "mehmetakif_2");

            Vector3[] trees = { new(-34f, 0.1f, 267f), new(14f, 0.1f, 267f), new(38f, 0.1f, 285f), new(-52f, 0.1f, 285f), new(10f, 0.1f, 310f) };
            for (int i = 0; i < trees.Length; i++) BuildPlaneTree(d, trees[i], 0.96f + (i % 2) * 0.16f);

            BuildStreetLamp(d, new Vector3(-26f, 0.1f, 270f));
            BuildStreetLamp(d, new Vector3(  8f, 0.1f, 270f));
            BuildStreetLamp(d, new Vector3( -6f, 0.1f, 250f));
            BuildStreetLamp(d, new Vector3( 24f, 0.1f, 295f));
        }

        // =====================================================================
        // DISTRICT 3: HASANPAŞA MAHALLESİ (NW · -185, +195)
        // M5 Hasanpaşa Metro · Hasanpaşa Millet Parkı · Kentsel Dönüşüm Konutları
        // =====================================================================
        private void BuildHasanpasaDistrict(Transform root)
        {
            Transform d = new GameObject("District_03_Hasanpasa").transform;
            d.SetParent(root, false);

            BuildWelcomeArch(d, new Vector3(-148f, 0f, 186f), -90f, "HASANPAŞA MAH.", "MİLLET PARKI & M5 METRO", _canopyPalette[3]);

            BuildRoadSegment(d, "Road_HasanpasaBlvd", new Vector3(-185f, 0f, 184f), 10.5f, 92f, alongZ: false);
            BuildSlab(d, "Hasanpasa_Metro_Plaza", new Vector3(-158f, 0.05f, 174f), new Vector3(24f, 0.10f, 16f), _plazaTravertineMat, solid: true);

            // Second M5 Metro Station (Hasanpaşa)
            BuildMetroStationEntrance(d, new Vector3(-158f, 0.10f, 171f), 0f, "M5 HASANPAŞA İSTASYONU");

            // Hasanpaşa Millet Parkı (-185, +208): Green Lawn + Rubberized Jogging Track + Fitness + Playground + Pergolas!
            BuildSlab(d, "Hasanpasa_Park_Lawn", new Vector3(-185f, 0.04f, 208f), new Vector3(48f, 0.08f, 34f), _parkGrassMat, solid: true);
            // Terracotta jogging track perimeter strips
            BuildSlab(d, "Hasanpasa_Track_N", new Vector3(-185f, 0.07f, 222f), new Vector3(42f, 0.04f, 2.8f), _joggingTrackMat, solid: false);
            BuildSlab(d, "Hasanpasa_Track_S", new Vector3(-185f, 0.07f, 194f), new Vector3(42f, 0.04f, 2.8f), _joggingTrackMat, solid: false);
            BuildSlab(d, "Hasanpasa_Track_W", new Vector3(-205f, 0.07f, 208f), new Vector3(2.8f, 0.04f, 30f), _joggingTrackMat, solid: false);
            BuildSlab(d, "Hasanpasa_Track_E", new Vector3(-165f, 0.07f, 208f), new Vector3(2.8f, 0.04f, 30f), _joggingTrackMat, solid: false);

            // Kids' Playground & Outdoor Fitness Equipment inside Millet Parkı
            BuildKidsPlayground(d, new Vector3(-192f, 0.08f, 208f));
            BuildOutdoorFitnessArea(d, new Vector3(-174f, 0.08f, 208f));
            BuildWoodenPergola(d, new Vector3(-185f, 0.08f, 198f), 0f, "hasanpasa_p0");

            // Interactive Park Büfesi
            BuildInteractiveKiosk(d, new Vector3(-168f, 0.08f, 194f), 180f,
                "MİLLET PARKI BÜFE", _canopyPalette[2],
                "district_hasanpasa_kiosk", "Soğuk Ayran & Meyve Çantası Al (30 ₺)", "Buy Cold Ayran & Fruit Bag (30 ₺)");

            // Modern Urban-Renewal Anthracite & Cream Residence Towers
            BuildIstanbulApartment(d, new Vector3(-220f, 0f, 208f),  90f, 15f, 16.5f, 12f, 5, "HASANPAŞA KONUTLARI");
            BuildIstanbulApartment(d, new Vector3(-214f, 0f, 166f),   0f, 15f, 15.5f, 11f, 0, "FİTNESS & MARKET");
            BuildIstanbulApartment(d, new Vector3(-188f, 0f, 166f),   0f, 16f, 15.0f, 11f, 5, "MİLLET KAFE");
            BuildIstanbulApartment(d, new Vector3(-185f, 0f, 236f), 180f, 18f, 16.0f, 12f, 4, "AYDOS REZİDANS");
            BuildIstanbulApartment(d, new Vector3(-156f, 0f, 224f), -90f, 14f, 14.5f, 11f, 3, "ECZANE");

            BuildSittableBench(d, new Vector3(-176f, 0.08f, 198f),   0f, "hasanpasa_0");
            BuildSittableBench(d, new Vector3(-194f, 0.08f, 198f),   0f, "hasanpasa_1");

            Vector3[] pines = { new(-202f, 0.08f, 218f), new(-168f, 0.08f, 218f), new(-202f, 0.08f, 196f), new(-172f, 0.08f, 174f) };
            for (int i = 0; i < pines.Length; i++) BuildPineTree(d, pines[i], 1.05f + (i % 2) * 0.18f);

            BuildStreetLamp(d, new Vector3(-164f, 0.08f, 189f));
            BuildStreetLamp(d, new Vector3(-185f, 0.08f, 189f));
            BuildStreetLamp(d, new Vector3(-204f, 0.08f, 189f));
            BuildStreetLamp(d, new Vector3(-185f, 0.08f, 216f));
        }

        // =====================================================================
        // DISTRICT 4: BATTALGAZİ MAHALLESİ (SW · -185, -185)
        // Bosna Bulvarı · Battalgazi Semt Pazarı · Sokak Futbolu Sahası · Kültür Merkezi
        // =====================================================================
        private void BuildBattalgaziDistrict(Transform root)
        {
            Transform d = new GameObject("District_04_Battalgazi").transform;
            d.SetParent(root, false);

            BuildWelcomeArch(d, new Vector3(-148f, 0f, -186f), -90f, "BATTALGAZİ MAH.", "BOSNA BULVARI & SEMT PAZARI", _canopyPalette[1]);

            BuildRoadSegment(d, "Road_BosnaBlvd_Main", new Vector3(-185f, 0f, -170f), 11.0f, 92f, alongZ: false);
            BuildSlab(d, "Battalgazi_Pazar_Plaza", new Vector3(-186f, 0.05f, -186f), new Vector3(44f, 0.10f, 22f), _sidewalkMat, solid: true);

            // Vibrant Battalgazi Semt Pazarı — 8 Colorful Striped Canopies with Produce Crates!
            for (int row = 0; row < 2; row++)
            {
                float z = row == 0 ? -180f : -191f;
                for (int col = 0; col < 4; col++)
                {
                    float x = -202f + col * 10.5f;
                    BuildPazarStall(d, new Vector3(x, 0.10f, z), row == 0 ? 180f : 0f, (row * 4 + col));
                }
            }

            // Interactive Fresh Market Produce Stand right in the heart of Semt Pazarı!
            InteractableTrigger pazarTrig = BuildInvisibleInteractPoint(
                d, "Battalgazi_Pazar_FreshStall", new Vector3(-186f, 0.10f, -185.5f),
                InteractableKind.InspectObject, "district_battalgazi_pazar", 3.4f,
                "Taze Semt Pazarı Meyve Çantası Al (35 ₺ · 3 Porsiyon)",
                "Buy Fresh Bazaar Fruit Bag (35 ₺ · 3 Servings)");
            if (pazarTrig != null)
            {
                BuildSignBoard(d, new Vector3(-186f, 3.05f, -180.0f), 180f, "BATTALGAZİ SEMT PAZARI", "TAZE MEYVE & SEBZE", _canopyPalette[0]);
            }

            // Neighborhood Street Football Mini-Pitch (-164, -206) with REAL Playable StreetFootball + 2 Goals!
            BuildStreetFootballMiniPitch(d, new Vector3(-164f, 0.06f, -206f));

            // Battalgazi Kültür Merkezi & Lively Family Apartments
            BuildCivicHall(d, new Vector3(-216f, 0.05f, -152f), 180f, "BATTALGAZİ KÜLTÜR MERKEZİ", "KÜTÜPHANE & SPOR");
            BuildIstanbulApartment(d, new Vector3(-186f, 0f, -152f), 180f, 16f, 14.0f, 11f, 2, "BOSNA FIRINI");
            BuildIstanbulApartment(d, new Vector3(-158f, 0f, -152f), 180f, 15f, 13.5f, 11f, 1, "BERBER & MANAV");
            BuildIstanbulApartment(d, new Vector3(-220f, 0f, -196f),  90f, 15f, 15.0f, 11f, 0, "BATTALGAZİ ÇARŞI");
            BuildIstanbulApartment(d, new Vector3(-196f, 0f, -214f),   0f, 16f, 13.0f, 11f, 3, "GÜL PASTANE");

            BuildSittableBench(d, new Vector3(-174f, 0.10f, -175f), 180f, "battalgazi_0");
            BuildSittableBench(d, new Vector3(-198f, 0.10f, -175f), 180f, "battalgazi_1");

            Vector3[] trees = { new(-212f, 0.1f, -175f), new(-160f, 0.1f, -175f), new(-182f, 0.1f, -208f), new(-146f, 0.1f, -206f) };
            for (int i = 0; i < trees.Length; i++) BuildPlaneTree(d, trees[i], 0.98f);

            BuildStreetLamp(d, new Vector3(-204f, 0.1f, -175f));
            BuildStreetLamp(d, new Vector3(-168f, 0.1f, -175f));
            BuildStreetLamp(d, new Vector3(-164f, 0.1f, -196f));
            BuildStreetLamp(d, new Vector3(-186f, 0.1f, -196f));
        }

        // =====================================================================
        // DISTRICT 5: MİMAR SİNAN MAHALLESİ (S · -52, -282)
        // Mimar Sinan Kültür Parkı · Çocuk Oyun Alanı · Tarihi Çeşme · İlkokul & Mescit
        // =====================================================================
        private void BuildMimarSinanDistrict(Transform root)
        {
            Transform d = new GameObject("District_05_MimarSinan").transform;
            d.SetParent(root, false);

            BuildWelcomeArch(d, new Vector3(-52f, 0f, -242f), 180f, "MİMAR SİNAN MAH.", "KÜLTÜR PARKI & ÇOCUK ALANI", _istanbulBlueMat);

            BuildRoadSegment(d, "Road_MimarSinanCd", new Vector3(-52f, 0f, -262f), 10.5f, 94f, alongZ: false);

            // Mimar Sinan Kültür Parkı (-52, -286)
            BuildSlab(d, "MimarSinan_Park_Lawn", new Vector3(-52f, 0.04f, -286f), new Vector3(42f, 0.08f, 34f), _parkGrassMat, solid: true);
            BuildSlab(d, "MimarSinan_Park_Walkway", new Vector3(-52f, 0.07f, -286f), new Vector3(4.5f, 0.04f, 34f), _plazaTravertineMat, solid: false);

            // Full Colorful Kids' Playground + Sandbox + Wooden Pergola!
            BuildKidsPlayground(d, new Vector3(-56f, 0.08f, -282f));
            BuildSandbox(d, new Vector3(-42f, 0.08f, -282f));
            BuildWoodenPergola(d, new Vector3(-42f, 0.08f, -296f), 180f, "mimarsinan_p0");

            // Historic Ottoman Arched Stone Mimar Sinan Çeşmesi (Interactive!)
            BuildInteractiveFountain(d, new Vector3(-52f, 0.08f, -300f), 0f,
                "district_mimarsinan_fountain", "Mimar Sinan Çeşmesinden Su İç (Ücretsiz)", "Drink from Mimar Sinan Fountain (Free)");

            // Interactive Maraş Dondurmacısı (Ice Cream Cart!)
            BuildInteractiveKiosk(d, new Vector3(-38f, 0.08f, -271f), 180f,
                "MARAŞ DONDURMACISI", _canopyPalette[4],
                "district_mimarsinan_icecream", "Maraş Dondurması Al (20 ₺)", "Buy Maraş Ice Cream (20 ₺)");

            // Mimar Sinan İlkokulu (-88, -284) & Mimar Sinan Mescidi (-14, -290)
            BuildTurkishSchool(d, new Vector3(-88f, 0.05f, -284f), 90f, "MİMAR SİNAN İLKOKULU");
            BuildFlagpole(d, new Vector3(-76f, 0.08f, -274f), 7.2f);
            BuildOttomanMosque(d, new Vector3(-14f, 0.05f, -290f), -90f, "MİMAR SİNAN MESCİDİ", scale: 0.78f);

            // Warm Pastel Family Apartment Blocks
            BuildIstanbulApartment(d, new Vector3(-84f, 0f, -248f), 180f, 15f, 13.0f, 11f, 0, "SİNAN KIRTASİYE");
            BuildIstanbulApartment(d, new Vector3(-22f, 0f, -248f), 180f, 15f, 14.0f, 11f, 1, "MAHALLE FIRINI");
            BuildIstanbulApartment(d, new Vector3(-52f, 0f, -318f),   0f, 17f, 14.5f, 11f, 3, "KÜLTÜR APARTMANI");
            BuildIstanbulApartment(d, new Vector3(-82f, 0f, -316f),   0f, 15f, 13.0f, 11f, 2, "MİMAR MARKET");

            BuildSittableBench(d, new Vector3(-62f, 0.08f, -273f), 180f, "mimarsinan_0");
            BuildSittableBench(d, new Vector3(-46f, 0.08f, -273f), 180f, "mimarsinan_1");
            BuildSittableBench(d, new Vector3(-58f, 0.08f, -296f),   0f, "mimarsinan_2");

            Vector3[] trees = { new(-68f, 0.08f, -274f), new(-34f, 0.08f, -274f), new(-68f, 0.08f, -298f), new(-34f, 0.08f, -298f) };
            for (int i = 0; i < trees.Length; i++) BuildPlaneTree(d, trees[i], 1.05f);

            BuildStreetLamp(d, new Vector3(-68f, 0.08f, -267f));
            BuildStreetLamp(d, new Vector3(-36f, 0.08f, -267f));
            BuildStreetLamp(d, new Vector3(-52f, 0.08f, -288f));
        }

        // =====================================================================
        // DISTRICT 6: TURGUT REİS MAHALLESİ & TARİHİ AYDOS KALESİ (SE · +238, -138)
        // Walkable Historic Stone Aydos Fortress · Panoramic Deck · Pine Grove · Fuel Station
        // =====================================================================
        private void BuildTurgutReisAndAydosCastleDistrict(Transform root)
        {
            Transform d = new GameObject("District_06_TurgutReis_AydosKalesi").transform;
            d.SetParent(root, false);

            BuildWelcomeArch(d, new Vector3(214f, 0f, -92f), 180f, "TURGUT REİS MAH.", "TARİHİ AYDOS KALESİ & ORMAN", _stoneDarkMat);

            BuildRoadSegment(d, "Road_DemokrasiCd", new Vector3(236f, 0f, -148f), 10.5f, 88f, alongZ: false);

            // HISTORIC AYDOS KALESİ (+264, -118) — Iconic Walkable Stepped Stone Fortress!
            BuildHistoricAydosCastle(d, new Vector3(264f, 0f, -118f));

            // Turgut Reis Pine Picnic Grove & Kale Çay Bahçesi (+230, -118)
            BuildSlab(d, "TurgutReis_PineGrove_Lawn", new Vector3(230f, 0.04f, -118f), new Vector3(32f, 0.08f, 34f), _parkGrassMat, solid: true);
            BuildWoodenPergola(d, new Vector3(226f, 0.08f, -124f), 0f, "turgutreis_p0");
            BuildInteractiveKiosk(d, new Vector3(234f, 0.08f, -132f), 180f,
                "AYDOS KALE ÇAY BAHÇESİ", _canopyPalette[3],
                "district_turgutreis_tea", "Semaver Çayı & Gözleme Al (30 ₺)", "Buy Samovar Tea & Gözleme (30 ₺)");

            // Turgut Reis Akaryakıt İstasyonu (+214, -166) with REAL working FuelPump!
            BuildDistrictFuelStation(d, new Vector3(214f, 0.05f, -166f));

            // Demokrasi Caddesi Residential Buildings
            BuildIstanbulApartment(d, new Vector3(244f, 0f, -168f), 0f, 15f, 13.5f, 11f, 0, "TURGUT REİS MARKET");
            BuildIstanbulApartment(d, new Vector3(268f, 0f, -168f), 0f, 16f, 14.5f, 11f, 2, "KALE FIRINI");
            BuildIstanbulApartment(d, new Vector3(196f, 0f, -124f), 90f, 14f, 13.0f, 11f, 1, "AYDOS KAFE");
            BuildIstanbulApartment(d, new Vector3(196f, 0f, -164f), 90f, 14f, 12.5f, 11f, 4, "OTO LASTİK");

            BuildSittableBench(d, new Vector3(222f, 0.08f, -112f),  90f, "turgutreis_0");
            BuildSittableBench(d, new Vector3(238f, 0.08f, -112f), -90f, "turgutreis_1");

            // Dense Aydos Pine Grove surrounding the castle hill!
            Vector3[] pines =
            {
                new(220f, 0.08f, -104f), new(232f, 0.08f, -102f), new(244f, 0.08f, -98f),
                new(278f, 0.08f, -98f),  new(284f, 0.08f, -118f), new(280f, 0.08f, -138f),
                new(218f, 0.08f, -134f), new(242f, 0.08f, -134f)
            };
            for (int i = 0; i < pines.Length; i++) BuildPineTree(d, pines[i], 1.15f + (i % 3) * 0.22f);

            BuildStreetLamp(d, new Vector3(224f, 0.08f, -142f));
            BuildStreetLamp(d, new Vector3(250f, 0.08f, -142f));
            BuildStreetLamp(d, new Vector3(246f, 0.08f, -118f));
        }

        // =====================================================================
        // ARCHITECTURAL & STREETSCAPE BUILDERS (CLEAN, SOLID, MOBILE-OPTIMIZED)
        // =====================================================================
        private void BuildHistoricAydosCastle(Transform parent, Vector3 center)
        {
            GameObject castle = new GameObject("Aydos_Kalesi_HistoricFortress");
            castle.transform.SetParent(parent, false);
            castle.transform.position = center;

            // 1. Stepped grassy hillside base (terraced to Y=3.2m so you can walk/climb smoothly!)
            BuildSlab(castle.transform, "AydosHill_Terrace1_Grass", new Vector3(0f, 0.8f, 0f), new Vector3(36f, 1.6f, 32f), _parkGrassMat, solid: true);
            BuildSlab(castle.transform, "AydosHill_Terrace2_Stone", new Vector3(0f, 2.2f, 0f), new Vector3(26f, 1.6f, 24f), _stoneFortressMat, solid: true);

            // Smooth walkable stone approach ramp from West side (X=-23..X=-12, Y=0 -> Y=3.0m)
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Aydos_Castle_WalkRamp";
            ramp.transform.SetParent(castle.transform, false);
            ramp.transform.localPosition = new Vector3(-18.5f, 1.40f, 0f);
            ramp.transform.localRotation = Quaternion.Euler(0f, 0f, 14.5f);
            ramp.transform.localScale = new Vector3(13.5f, 0.45f, 5.2f);
            SetupMeshMat(ramp, _stoneDarkMat, castShadows: false);

            // 2. Fortress Courtyard Floor at Y = 3.0m
            BuildSlab(castle.transform, "Aydos_Castle_Courtyard_Plaza", new Vector3(0f, 3.05f, 0f), new Vector3(22f, 0.25f, 20f), _plazaTravertineMat, solid: true);

            // 3. Medieval Stone Curtain Walls (North, South, East + West Gate Walls)
            BuildWall(castle.transform, "CastleWall_North", new Vector3(0f, 5.2f,  9.5f), new Vector3(22f, 4.4f, 1.4f), _stoneFortressMat);
            BuildWall(castle.transform, "CastleWall_South", new Vector3(0f, 5.2f, -9.5f), new Vector3(22f, 4.4f, 1.4f), _stoneFortressMat);
            BuildWall(castle.transform, "CastleWall_East",  new Vector3(10.5f, 5.2f, 0f), new Vector3(1.4f, 4.4f, 20f), _stoneFortressMat);
            // West wall with open arched entrance gate!
            BuildWall(castle.transform, "CastleWall_WestN", new Vector3(-10.5f, 5.2f,  6.2f), new Vector3(1.4f, 4.4f, 6.6f), _stoneFortressMat);
            BuildWall(castle.transform, "CastleWall_WestS", new Vector3(-10.5f, 5.2f, -6.2f), new Vector3(1.4f, 4.4f, 6.6f), _stoneFortressMat);
            BuildWall(castle.transform, "CastleGate_Arch",  new Vector3(-10.5f, 6.7f,  0.0f), new Vector3(1.6f, 1.4f, 6.2f), _stoneDarkMat);

            // Crenellated Battlements along wall tops
            for (int b = -8; b <= 8; b += 4)
            {
                BuildDecorativeBox(castle.transform, "Merlon_N", new Vector3(b, 7.8f,  9.5f), new Vector3(1.6f, 0.9f, 1.5f), _stoneDarkMat);
                BuildDecorativeBox(castle.transform, "Merlon_S", new Vector3(b, 7.8f, -9.5f), new Vector3(1.6f, 0.9f, 1.5f), _stoneDarkMat);
            }

            // 4. Twin Cylindrical Byzantine/Ottoman Watchtowers (NE & SE corners)
            BuildWatchtower(castle.transform, new Vector3( 10.5f, 3.0f,  9.5f), 8.5f);
            BuildWatchtower(castle.transform, new Vector3( 10.5f, 3.0f, -9.5f), 7.5f);
            BuildWatchtower(castle.transform, new Vector3(-10.5f, 3.0f,  9.5f), 6.5f);

            // 5. Giant Turkish Flag flying high above Aydos Castle!
            BuildFlagpole(castle.transform, new Vector3(10.5f, 11.4f, 9.5f), 6.2f);
            BuildSignBoard(castle.transform, new Vector3(-11.4f, 6.6f, 0f), -90f, "TARİHİ AYDOS KALESİ", "325M · SULTANBEYLİ", _stoneDarkMat);

            // 6. Panoramic Wooden Viewing Deck + Interactive Viewpoint overlooking all of Sultanbeyli!
            BuildSlab(castle.transform, "Aydos_Panoramic_Deck", new Vector3(-4.5f, 3.22f, -4.5f), new Vector3(6.5f, 0.16f, 5.2f), _woodMat, solid: true);
            BuildSittableBench(castle.transform, new Vector3(-4.5f, 3.30f, -5.5f), -90f, "aydos_castle_0");
            BuildInvisibleInteractPoint(
                castle.transform, "Aydos_Castle_Viewpoint", new Vector3(-3.5f, 3.30f, 0.0f),
                InteractableKind.InspectObject, "district_aydos_viewpoint", 3.4f,
                "Aydos Kalesi'nden İstanbul Manzarasına Bak", "Admire Sultanbeyli Panorama from Aydos Castle");

            BuildStreetLamp(castle.transform, new Vector3(-8.5f, 3.15f,  4.0f));
            BuildStreetLamp(castle.transform, new Vector3( 6.5f, 3.15f, -4.0f));
        }

        private void BuildWatchtower(Transform parent, Vector3 localPos, float height)
        {
            GameObject t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            t.name = "Castle_Watchtower";
            t.transform.SetParent(parent, false);
            t.transform.localPosition = localPos + Vector3.up * (height * 0.5f);
            t.transform.localScale = new Vector3(4.6f, height * 0.5f, 4.6f);
            SetupMeshMat(t, _stoneFortressMat, castShadows: true);

            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crown.name = "Watchtower_Crown";
            crown.transform.SetParent(parent, false);
            crown.transform.localPosition = localPos + Vector3.up * (height + 0.35f);
            crown.transform.localScale = new Vector3(5.2f, 0.45f, 5.2f);
            Destroy(crown.GetComponent<Collider>());
            SetupMeshMat(crown, _stoneDarkMat, castShadows: false);
        }

        private void BuildRoadSegment(Transform parent, string name, Vector3 center, float width, float length, bool alongZ)
        {
            Vector3 size = alongZ ? new Vector3(width, 0.05f, length) : new Vector3(length, 0.05f, width);
            BuildSlab(parent, name + "_Asphalt", new Vector3(center.x, 0.025f, center.z), size, _asphaltMat, solid: false);

            // Dashed center lane markings
            int dashes = Mathf.Max(2, Mathf.FloorToInt(length / 9f));
            float start = -length * 0.5f + 4.5f;
            for (int i = 0; i < dashes; i++)
            {
                float offset = start + i * 9f;
                Vector3 p = alongZ
                    ? new Vector3(center.x, 0.056f, center.z + offset)
                    : new Vector3(center.x + offset, 0.056f, center.z);
                Vector3 sc = alongZ ? new Vector3(0.24f, 0.012f, 4.2f) : new Vector3(4.2f, 0.012f, 0.24f);
                BuildDecorativeBox(parent, "LaneDash", p, sc, _laneWhiteMat);
            }
        }

        private void BuildWelcomeArch(Transform parent, Vector3 pos, float yaw, string titleTop, string subtitle, Material accentMat)
        {
            GameObject arch = new GameObject($"WelcomeArch_{titleTop}");
            arch.transform.SetParent(parent, false);
            arch.transform.position = pos;
            arch.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Left & right structural columns clear of road width (span = 12.6m)
            BuildWall(arch.transform, "ArchPillarL", new Vector3(-6.3f, 2.75f, 0f), new Vector3(0.55f, 5.5f, 0.55f), _darkMetalMat);
            BuildWall(arch.transform, "ArchPillarR", new Vector3( 6.3f, 2.75f, 0f), new Vector3(0.55f, 5.5f, 0.55f), _darkMetalMat);
            BuildDecorativeBox(arch.transform, "ArchBeam", new Vector3(0f, 5.35f, 0f), new Vector3(13.4f, 1.25f, 0.48f), accentMat);

            // Both front & back crisp readable labels
            BuildSignTextMesh(arch.transform, new Vector3(0f, 5.56f, -0.28f), Quaternion.identity, titleTop, 0.14f, 46, Color.white);
            BuildSignTextMesh(arch.transform, new Vector3(0f, 5.10f, -0.28f), Quaternion.identity, subtitle, 0.10f, 34, new Color(0.96f, 0.90f, 0.65f));
            BuildSignTextMesh(arch.transform, new Vector3(0f, 5.56f,  0.28f), Quaternion.Euler(0f, 180f, 0f), titleTop, 0.14f, 46, Color.white);
            BuildSignTextMesh(arch.transform, new Vector3(0f, 5.10f,  0.28f), Quaternion.Euler(0f, 180f, 0f), subtitle, 0.10f, 34, new Color(0.96f, 0.90f, 0.65f));
        }

        private void BuildCivicHall(Transform parent, Vector3 pos, float yaw, string title, string sub)
        {
            GameObject g = new GameObject($"CivicHall_{title}");
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Main travertine civic mass + blue modern glass atrium centerpiece
            BuildWall(g.transform, "Civic_Main_Facade", new Vector3(0f, 6.5f, 0f), new Vector3(26f, 13f, 13f), _plazaTravertineMat);
            BuildDecorativeBox(g.transform, "Civic_GlassAtrium", new Vector3(0f, 6.8f, 6.58f), new Vector3(10.5f, 10.6f, 0.25f), _glassBlueMat);
            BuildDecorativeBox(g.transform, "Civic_RoofCornice", new Vector3(0f, 13.3f, 0f), new Vector3(27.2f, 0.7f, 14.0f), _stoneDarkMat);
            // Entrance portico canopy & columns
            BuildDecorativeBox(g.transform, "Civic_Canopy", new Vector3(0f, 3.6f, 7.9f), new Vector3(11.5f, 0.45f, 3.2f), _whiteTrimMat);
            BuildWall(g.transform, "Civic_ColL", new Vector3(-4.8f, 1.8f, 8.9f), new Vector3(0.55f, 3.6f, 0.55f), _whiteTrimMat);
            BuildWall(g.transform, "Civic_ColR", new Vector3( 4.8f, 1.8f, 8.9f), new Vector3(0.55f, 3.6f, 0.55f), _whiteTrimMat);

            // Symmetric wings windows (picked up by NeighborhoodNightLighting!)
            for (int floor = 0; floor < 3; floor++)
            {
                float wy = 3.2f + floor * 3.2f;
                BuildDecorativeBox(g.transform, "Apt_Window_L", new Vector3(-9.2f, wy, 6.56f), new Vector3(4.2f, 1.7f, 0.12f), _windowLitMat);
                BuildDecorativeBox(g.transform, "Apt_Window_R", new Vector3( 9.2f, wy, 6.56f), new Vector3(4.2f, 1.7f, 0.12f), _windowLitMat);
            }

            BuildSignBoard(g.transform, new Vector3(0f, 4.35f, 9.55f), 180f, title, sub, _istanbulBlueMat);
        }

        private void BuildOttomanMosque(Transform parent, Vector3 pos, float yaw, string title, float scale = 1f)
        {
            GameObject m = new GameObject($"Camii_{title}");
            m.transform.SetParent(parent, false);
            m.transform.position = pos;
            m.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            m.transform.localScale = Vector3.one * scale;

            // Main square sanctuary hall (warm travertine stone)
            BuildWall(m.transform, "Camii_Sanctuary_Facade", new Vector3(0f, 4.8f, 0f), new Vector3(16f, 9.6f, 16f), _plazaTravertineMat);

            // Main Lead-Grey Dome + Golden Crescent Finial
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Camii_MainDome";
            dome.transform.SetParent(m.transform, false);
            dome.transform.localPosition = new Vector3(0f, 9.6f, 0f);
            dome.transform.localScale = new Vector3(12.2f, 7.6f, 12.2f);
            Destroy(dome.GetComponent<Collider>());
            SetupMeshMat(dome, _mosqueDomeMat, castShadows: true);

            BuildDecorativeBox(m.transform, "Dome_Alem", new Vector3(0f, 14.0f, 0f), new Vector3(0.24f, 1.4f, 0.24f), _goldAccentMat);

            // Entrance Portico & 3 mini domes
            BuildDecorativeBox(m.transform, "Camii_PorticoRoof", new Vector3(0f, 4.4f, 9.6f), new Vector3(13f, 0.45f, 3.8f), _whiteTrimMat);
            for (int c = -1; c <= 1; c++)
            {
                BuildWall(m.transform, "Camii_Col", new Vector3(c * 4.8f, 2.2f, 11.1f), new Vector3(0.45f, 4.4f, 0.45f), _whiteTrimMat);
            }

            // Twin Slender Minarets with Şerefe Balconies & Conical Spires!
            float[] mx = { -8.8f, 8.8f };
            for (int i = 0; i < 2; i++)
            {
                GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                shaft.name = $"Camii_Minaret_{i}";
                shaft.transform.SetParent(m.transform, false);
                shaft.transform.localPosition = new Vector3(mx[i], 10.5f, 7.8f);
                shaft.transform.localScale = new Vector3(1.25f, 10.5f, 1.25f);
                SetupMeshMat(shaft, _whiteTrimMat, castShadows: true);

                GameObject serefe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                serefe.name = "Serefe";
                serefe.transform.SetParent(m.transform, false);
                serefe.transform.localPosition = new Vector3(mx[i], 15.8f, 7.8f);
                serefe.transform.localScale = new Vector3(1.95f, 0.35f, 1.95f);
                Destroy(serefe.GetComponent<Collider>());
                SetupMeshMat(serefe, _plazaTravertineMat, castShadows: false);

                GameObject spire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spire.name = "Spire";
                spire.transform.SetParent(m.transform, false);
                spire.transform.localPosition = new Vector3(mx[i], 22.0f, 7.8f);
                spire.transform.localScale = new Vector3(0.75f, 1.8f, 0.75f);
                Destroy(spire.GetComponent<Collider>());
                SetupMeshMat(spire, _mosqueDomeMat, castShadows: false);
            }

            BuildSignBoard(m.transform, new Vector3(0f, 5.15f, 11.55f), 180f, title, "SULTANBEYLİ", _stoneDarkMat);
        }

        private void BuildMetroStationEntrance(Transform parent, Vector3 pos, float yaw, string stationTitle)
        {
            GameObject metro = new GameObject($"Metro_{stationTitle}");
            metro.transform.SetParent(parent, false);
            metro.transform.position = pos;
            metro.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Modern Glass & Red Steel Entrance Canopy
            BuildDecorativeBox(metro.transform, "Metro_CanopyTop", new Vector3(0f, 3.85f, 0f), new Vector3(11.5f, 0.36f, 6.4f), _glassBlueMat);
            BuildDecorativeBox(metro.transform, "Metro_FasciaRed", new Vector3(0f, 3.88f, 3.24f), new Vector3(11.6f, 0.56f, 0.18f), _metroRedMat);
            BuildWall(metro.transform, "Metro_PostFL", new Vector3(-5.4f, 1.9f,  2.9f), new Vector3(0.32f, 3.8f, 0.32f), _darkMetalMat);
            BuildWall(metro.transform, "Metro_PostFR", new Vector3( 5.4f, 1.9f,  2.9f), new Vector3(0.32f, 3.8f, 0.32f), _darkMetalMat);
            BuildWall(metro.transform, "Metro_PostBL", new Vector3(-5.4f, 1.9f, -2.9f), new Vector3(0.32f, 3.8f, 0.32f), _darkMetalMat);
            BuildWall(metro.transform, "Metro_PostBR", new Vector3( 5.4f, 1.9f, -2.9f), new Vector3(0.32f, 3.8f, 0.32f), _darkMetalMat);
            // Side glass balustrades + dark stair entrance hall
            BuildWall(metro.transform, "Metro_BackWall", new Vector3(0f, 1.6f, -2.9f), new Vector3(10.8f, 3.2f, 0.35f), _stoneDarkMat);
            BuildDecorativeBox(metro.transform, "Metro_EscalatorOpening", new Vector3(0f, 0.08f, -0.5f), new Vector3(7.4f, 0.06f, 4.2f), _darkMetalMat);

            // Iconic Istanbul Red-White 'M' Tall Metro Totem Pylon!
            BuildWall(metro.transform, "Metro_TotemPole", new Vector3(-7.4f, 3.4f, 2.2f), new Vector3(0.45f, 6.8f, 0.45f), _darkMetalMat);
            BuildDecorativeBox(metro.transform, "Metro_TotemBox", new Vector3(-7.4f, 6.4f, 2.2f), new Vector3(1.85f, 1.85f, 0.55f), _metroRedMat);
            BuildSignTextMesh(metro.transform, new Vector3(-7.4f, 6.42f, 2.52f), Quaternion.Euler(0f, 180f, 0f), "M", 0.34f, 68, Color.white);
            BuildSignTextMesh(metro.transform, new Vector3(-7.4f, 6.42f, 1.88f), Quaternion.identity, "M", 0.34f, 68, Color.white);

            // Illuminated station name fascia
            BuildSignTextMesh(metro.transform, new Vector3(0f, 3.88f, 3.38f), Quaternion.Euler(0f, 180f, 0f), stationTitle, 0.11f, 40, Color.white);
            BuildSignTextMesh(metro.transform, new Vector3(0f, 3.88f, -3.38f), Quaternion.identity, stationTitle, 0.11f, 40, Color.white);
        }

        private void BuildBusStopShelter(Transform parent, Vector3 pos, float yaw, string stopName)
        {
            GameObject s = new GameObject($"BusStop_{stopName}");
            s.transform.SetParent(parent, false);
            s.transform.position = pos;
            s.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            BuildWall(s.transform, "StopBackGlass", new Vector3(0f, 1.35f, -0.75f), new Vector3(4.2f, 2.5f, 0.14f), _glassBlueMat);
            BuildDecorativeBox(s.transform, "StopCanopy", new Vector3(0f, 2.65f, 0f), new Vector3(4.5f, 0.18f, 1.75f), _canopyPalette[4]);
            BuildWall(s.transform, "StopBench", new Vector3(0f, 0.45f, -0.35f), new Vector3(3.2f, 0.12f, 0.45f), _woodMat);
            BuildSignTextMesh(s.transform, new Vector3(0f, 2.66f, 0.90f), Quaternion.Euler(0f, 180f, 0f), stopName, 0.08f, 32, Color.black);
        }

        private void BuildTurkishSchool(Transform parent, Vector3 pos, float yaw, string schoolName)
        {
            GameObject sc = new GameObject($"School_{schoolName}");
            sc.transform.SetParent(parent, false);
            sc.transform.position = pos;
            sc.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Warm cream & terracotta 3-story Turkish school facade
            BuildWall(sc.transform, "School_Main_Facade", new Vector3(0f, 4.8f, 0f), new Vector3(24f, 9.6f, 11f), _plasterPalette[0]);
            BuildDecorativeBox(sc.transform, "School_Roof_Kiremit", new Vector3(0f, 10.1f, 0f), new Vector3(25.2f, 1.1f, 12.0f), _roofTileMat);
            BuildDecorativeBox(sc.transform, "School_EntranceTrim", new Vector3(0f, 2.4f, 5.65f), new Vector3(5.6f, 4.8f, 0.45f), _plasterPalette[2]);

            // Classroom window rows
            for (int f = 0; f < 3; f++)
            {
                float wy = 2.1f + f * 2.9f;
                for (int w = -3; w <= 3; w++)
                {
                    if (w == 0 && f == 0) continue;
                    BuildDecorativeBox(sc.transform, "Apt_Window", new Vector3(w * 3.1f, wy, 5.56f), new Vector3(1.8f, 1.45f, 0.10f), _windowLitMat);
                }
            }

            BuildSignBoard(sc.transform, new Vector3(0f, 5.35f, 5.95f), 180f, schoolName, "T.C. MİLLİ EĞİTİM BAKANLIĞI", _turkishRedMat);
        }

        private void BuildIstanbulApartment(Transform parent, Vector3 pos, float yaw, float width, float height, float depth, int plasterIdx, string shopLabel)
        {
            GameObject apt = new GameObject($"DistrictApt_{shopLabel}");
            apt.transform.SetParent(parent, false);
            apt.transform.position = pos;
            apt.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            Material wallMat = _plasterPalette[plasterIdx % _plasterPalette.Length];
            BuildWall(apt.transform, "Apt_Building_Facade", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), wallMat);
            BuildDecorativeBox(apt.transform, "Apt_Roof_Kiremit", new Vector3(0f, height + 0.55f, 0f), new Vector3(width + 0.9f, 1.1f, depth + 0.9f), _roofTileMat);

            // Ground-floor shopfront fascia + striped awning + shop display window
            float frontZ = depth * 0.5f;
            BuildDecorativeBox(apt.transform, "Shop_DisplayGlass", new Vector3(0f, 1.5f, frontZ + 0.08f), new Vector3(width * 0.72f, 2.4f, 0.14f), _glassBlueMat);
            BuildDecorativeBox(apt.transform, "Shop_Awning", new Vector3(0f, 3.05f, frontZ + 0.85f), new Vector3(width * 0.76f, 0.24f, 1.65f), _canopyPalette[plasterIdx % _canopyPalette.Length]);
            BuildSignTextMesh(apt.transform, new Vector3(0f, 3.46f, frontZ + 0.15f), Quaternion.Euler(0f, 180f, 0f), shopLabel, 0.10f, 36, Color.white);

            // Upper residential floor windows & recessed balconies (auto-glow at night!)
            int floors = Mathf.Clamp(Mathf.FloorToInt((height - 3.6f) / 3.0f), 2, 4);
            for (int f = 0; f < floors; f++)
            {
                float wy = 4.9f + f * 3.0f;
                for (int w = -1; w <= 1; w++)
                {
                    float wx = w * (width * 0.28f);
                    BuildDecorativeBox(apt.transform, "Apt_Window", new Vector3(wx, wy, frontZ + 0.08f), new Vector3(2.1f, 1.45f, 0.10f), _windowLitMat);
                    if (w == 0)
                    {
                        // Classic Istanbul front balcony
                        BuildDecorativeBox(apt.transform, "Apt_BalconySlab", new Vector3(wx, wy - 0.85f, frontZ + 0.52f), new Vector3(3.1f, 0.18f, 0.95f), _whiteTrimMat);
                        BuildDecorativeBox(apt.transform, "Apt_BalconyRail", new Vector3(wx, wy - 0.35f, frontZ + 0.96f), new Vector3(3.1f, 0.85f, 0.08f), _darkMetalMat);
                    }
                }
            }
        }

        private void BuildPlazaFountain(Transform parent, Vector3 pos, float radius)
        {
            GameObject f = new GameObject("Sultanbeyli_Square_Fountain");
            f.transform.SetParent(parent, false);
            f.transform.position = pos;

            GameObject basin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basin.name = "Fountain_Basin";
            basin.transform.SetParent(f.transform, false);
            basin.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            basin.transform.localScale = new Vector3(radius * 2f, 0.42f, radius * 2f);
            SetupMeshMat(basin, _plazaTravertineMat, castShadows: true);

            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "Fountain_WaterPool";
            water.transform.SetParent(f.transform, false);
            water.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            water.transform.localScale = new Vector3(radius * 1.78f, 0.08f, radius * 1.78f);
            Destroy(water.GetComponent<Collider>());
            SetupMeshMat(water, _waterBlueMat, castShadows: false);

            GameObject tier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tier.name = "Fountain_CenterTier";
            tier.transform.SetParent(f.transform, false);
            tier.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            tier.transform.localScale = new Vector3(2.2f, 0.75f, 2.2f);
            Destroy(tier.GetComponent<Collider>());
            SetupMeshMat(tier, _whiteTrimMat, castShadows: false);

            // Animated sparkling water jets
            for (int i = 0; i < 4; i++)
            {
                float ang = i * Mathf.PI * 0.5f;
                GameObject jet = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                jet.name = $"Fountain_Jet_{i}";
                jet.transform.SetParent(f.transform, false);
                jet.transform.localPosition = new Vector3(Mathf.Cos(ang) * 1.15f, 1.65f, Mathf.Sin(ang) * 1.15f);
                jet.transform.localScale = new Vector3(0.18f, 1.1f, 0.18f);
                Destroy(jet.GetComponent<Collider>());
                SetupMeshMat(jet, _waterBlueMat, castShadows: false);
                _fountainJets.Add(jet.transform);
            }
        }

        private void BuildInteractiveFountain(Transform parent, Vector3 pos, float yaw, string id, string promptTr, string promptEn)
        {
            GameObject f = new GameObject($"Cesme_{id}");
            f.transform.SetParent(parent, false);
            f.transform.position = pos;
            f.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            BuildWall(f.transform, "Cesme_StoneBody", new Vector3(0f, 1.35f, 0f), new Vector3(2.4f, 2.7f, 1.2f), _stoneFortressMat);
            BuildDecorativeBox(f.transform, "Cesme_Roof", new Vector3(0f, 2.85f, 0f), new Vector3(2.8f, 0.35f, 1.6f), _roofTileMat);
            BuildDecorativeBox(f.transform, "Cesme_Basin", new Vector3(0f, 0.42f, 0.85f), new Vector3(1.8f, 0.55f, 0.65f), _stoneDarkMat);
            BuildDecorativeBox(f.transform, "Cesme_Water", new Vector3(0f, 0.68f, 0.85f), new Vector3(1.5f, 0.08f, 0.45f), _waterBlueMat);
            GameObject stream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stream.name = "Cesme_Stream";
            stream.transform.SetParent(f.transform, false);
            stream.transform.localPosition = new Vector3(0f, 0.95f, 0.72f);
            stream.transform.localScale = new Vector3(0.08f, 0.36f, 0.08f);
            Destroy(stream.GetComponent<Collider>());
            SetupMeshMat(stream, _waterBlueMat, castShadows: false);
            _fountainJets.Add(stream.transform);

            InteractableTrigger trig = f.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.InspectObject, id, "ui.btn.interact", 2.9f);
            trig.SetCustomPrompt(promptTr, promptEn);
        }

        private void BuildInteractiveKiosk(Transform parent, Vector3 pos, float yaw, string signText, Material canopyMat, string id, string promptTr, string promptEn)
        {
            GameObject k = new GameObject($"Kiosk_{id}");
            k.transform.SetParent(parent, false);
            k.transform.position = pos;
            k.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            BuildWall(k.transform, "Kiosk_Counter", new Vector3(0f, 0.60f, 0f), new Vector3(3.2f, 1.2f, 1.8f), _woodMat);
            BuildDecorativeBox(k.transform, "Kiosk_Canopy", new Vector3(0f, 2.65f, 0f), new Vector3(3.6f, 0.28f, 2.4f), canopyMat);
            BuildDecorativeBox(k.transform, "Kiosk_PostL", new Vector3(-1.48f, 1.85f,  0.78f), new Vector3(0.14f, 1.6f, 0.14f), _darkMetalMat);
            BuildDecorativeBox(k.transform, "Kiosk_PostR", new Vector3( 1.48f, 1.85f,  0.78f), new Vector3(0.14f, 1.6f, 0.14f), _darkMetalMat);
            BuildDecorativeBox(k.transform, "Kiosk_PostBL",new Vector3(-1.48f, 1.85f, -0.78f), new Vector3(0.14f, 1.6f, 0.14f), _darkMetalMat);
            BuildDecorativeBox(k.transform, "Kiosk_PostBR",new Vector3( 1.48f, 1.85f, -0.78f), new Vector3(0.14f, 1.6f, 0.14f), _darkMetalMat);
            BuildSignTextMesh(k.transform, new Vector3(0f, 2.66f, 1.25f), Quaternion.Euler(0f, 180f, 0f), signText, 0.08f, 34, Color.white);

            InteractableTrigger trig = k.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.InspectObject, id, "ui.btn.interact", 3.1f);
            trig.SetCustomPrompt(promptTr, promptEn);
        }

        private void BuildPazarStall(Transform parent, Vector3 pos, float yaw, int idx)
        {
            GameObject st = new GameObject($"PazarStall_{idx:00}");
            st.transform.SetParent(parent, false);
            st.transform.position = pos;
            st.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            Material canopyMat = _canopyPalette[idx % _canopyPalette.Length];
            BuildWall(st.transform, "Stall_Table", new Vector3(0f, 0.52f, 0f), new Vector3(5.6f, 1.04f, 1.6f), _woodMat);
            BuildDecorativeBox(st.transform, "Stall_Canopy", new Vector3(0f, 2.72f, 0f), new Vector3(6.2f, 0.16f, 3.0f), canopyMat);
            BuildDecorativeBox(st.transform, "Stall_PoleL", new Vector3(-2.7f, 1.4f, 0f), new Vector3(0.10f, 2.7f, 0.10f), _darkMetalMat);
            BuildDecorativeBox(st.transform, "Stall_PoleR", new Vector3( 2.7f, 1.4f, 0f), new Vector3(0.10f, 2.7f, 0.10f), _darkMetalMat);

            // Colorful fruit/veggie produce crates on the counter!
            for (int c = -1; c <= 1; c++)
            {
                BuildDecorativeBox(st.transform, "ProduceCrate", new Vector3(c * 1.65f, 1.18f, 0f), new Vector3(1.25f, 0.30f, 1.05f), _producePalette[(idx + c + 4) % _producePalette.Length]);
            }
        }

        private void BuildKidsPlayground(Transform parent, Vector3 pos)
        {
            GameObject pg = new GameObject("Kids_Playground");
            pg.transform.SetParent(parent, false);
            pg.transform.position = pos;

            // 1. Twin Swing Set (Blue frame + Red seats)
            BuildWall(pg.transform, "Swing_PoleL", new Vector3(-3.2f, 1.4f, 0f), new Vector3(0.14f, 2.8f, 0.14f), _canopyPalette[2]);
            BuildWall(pg.transform, "Swing_PoleR", new Vector3( 0.4f, 1.4f, 0f), new Vector3(0.14f, 2.8f, 0.14f), _canopyPalette[2]);
            BuildDecorativeBox(pg.transform, "Swing_TopBar", new Vector3(-1.4f, 2.8f, 0f), new Vector3(3.8f, 0.14f, 0.14f), _canopyPalette[2]);
            BuildDecorativeBox(pg.transform, "Swing_Seat1", new Vector3(-2.1f, 0.65f, 0f), new Vector3(0.65f, 0.08f, 0.32f), _canopyPalette[0]);
            BuildDecorativeBox(pg.transform, "Swing_Seat2", new Vector3(-0.7f, 0.65f, 0f), new Vector3(0.65f, 0.08f, 0.32f), _canopyPalette[4]);

            // 2. Colorful Slide Tower (Yellow deck + Red slide chute)
            BuildWall(pg.transform, "Slide_Tower", new Vector3(3.4f, 1.25f, 0f), new Vector3(1.5f, 2.5f, 1.5f), _canopyPalette[4]);
            BuildDecorativeBox(pg.transform, "Slide_Roof", new Vector3(3.4f, 2.75f, 0f), new Vector3(1.85f, 0.35f, 1.85f), _canopyPalette[0]);
            GameObject chute = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chute.name = "Slide_Chute";
            chute.transform.SetParent(pg.transform, false);
            chute.transform.localPosition = new Vector3(3.4f, 0.95f, 2.0f);
            chute.transform.localRotation = Quaternion.Euler(34f, 0f, 0f);
            chute.transform.localScale = new Vector3(0.85f, 0.12f, 3.2f);
            SetupMeshMat(chute, _canopyPalette[0], castShadows: false);

            // 3. Seesaw (Tahterevalli)
            BuildWall(pg.transform, "Seesaw_Base", new Vector3(0.8f, 0.32f, 3.2f), new Vector3(0.35f, 0.64f, 0.35f), _darkMetalMat);
            GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = "Seesaw_Plank";
            plank.transform.SetParent(pg.transform, false);
            plank.transform.localPosition = new Vector3(0.8f, 0.68f, 3.2f);
            plank.transform.localRotation = Quaternion.Euler(0f, 0f, 9f);
            plank.transform.localScale = new Vector3(3.4f, 0.10f, 0.34f);
            SetupMeshMat(plank, _canopyPalette[3], castShadows: false);
        }

        private void BuildSandbox(Transform parent, Vector3 pos)
        {
            GameObject sb = new GameObject("Playground_Sandbox");
            sb.transform.SetParent(parent, false);
            sb.transform.position = pos;
            BuildSlab(sb.transform, "Sandbox_Sand", new Vector3(0f, 0.12f, 0f), new Vector3(4.6f, 0.24f, 4.6f), _sandMat, solid: true);
            BuildWall(sb.transform, "Sandbox_RimN", new Vector3(0f, 0.22f,  2.35f), new Vector3(4.9f, 0.32f, 0.22f), _woodMat);
            BuildWall(sb.transform, "Sandbox_RimS", new Vector3(0f, 0.22f, -2.35f), new Vector3(4.9f, 0.32f, 0.22f), _woodMat);
            BuildWall(sb.transform, "Sandbox_RimE", new Vector3( 2.35f, 0.22f, 0f), new Vector3(0.22f, 0.32f, 4.7f), _woodMat);
            BuildWall(sb.transform, "Sandbox_RimW", new Vector3(-2.35f, 0.22f, 0f), new Vector3(0.22f, 0.32f, 4.7f), _woodMat);
        }

        private void BuildOutdoorFitnessArea(Transform parent, Vector3 pos)
        {
            GameObject fit = new GameObject("Outdoor_Fitness_Station");
            fit.transform.SetParent(parent, false);
            fit.transform.position = pos;
            // Pull-up bar + elliptical + parallel bars in classic municipal yellow/blue steel
            BuildWall(fit.transform, "PullupL", new Vector3(-2.2f, 1.25f, 0f), new Vector3(0.14f, 2.5f, 0.14f), _canopyPalette[2]);
            BuildWall(fit.transform, "PullupR", new Vector3( 0.0f, 1.25f, 0f), new Vector3(0.14f, 2.5f, 0.14f), _canopyPalette[2]);
            BuildDecorativeBox(fit.transform, "PullupBar", new Vector3(-1.1f, 2.42f, 0f), new Vector3(2.3f, 0.10f, 0.10f), _canopyPalette[4]);
            BuildWall(fit.transform, "StepperPost", new Vector3(2.2f, 0.85f, 0f), new Vector3(0.22f, 1.7f, 0.22f), _canopyPalette[4]);
            BuildDecorativeBox(fit.transform, "StepperHandle", new Vector3(2.2f, 1.45f, 0.35f), new Vector3(0.75f, 0.10f, 0.10f), _canopyPalette[2]);
        }

        private void BuildWoodenPergola(Transform parent, Vector3 pos, float yaw, string idTag)
        {
            GameObject p = new GameObject($"Pergola_{idTag}");
            p.transform.SetParent(parent, false);
            p.transform.position = pos;
            p.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            BuildWall(p.transform, "Pergola_PostFL", new Vector3(-2.1f, 1.45f,  1.5f), new Vector3(0.22f, 2.9f, 0.22f), _woodMat);
            BuildWall(p.transform, "Pergola_PostFR", new Vector3( 2.1f, 1.45f,  1.5f), new Vector3(0.22f, 2.9f, 0.22f), _woodMat);
            BuildWall(p.transform, "Pergola_PostBL", new Vector3(-2.1f, 1.45f, -1.5f), new Vector3(0.22f, 2.9f, 0.22f), _woodMat);
            BuildWall(p.transform, "Pergola_PostBR", new Vector3( 2.1f, 1.45f, -1.5f), new Vector3(0.22f, 2.9f, 0.22f), _woodMat);
            BuildDecorativeBox(p.transform, "Pergola_Roof", new Vector3(0f, 2.95f, 0f), new Vector3(4.8f, 0.22f, 3.6f), _woodMat);
            BuildSittableBench(p.transform, Vector3.zero, 0f, idTag);
        }

        private void BuildStreetFootballMiniPitch(Transform parent, Vector3 center)
        {
            GameObject pitch = new GameObject("Battalgazi_StreetFootball_Pitch");
            pitch.transform.SetParent(parent, false);
            pitch.transform.position = center;

            BuildSlab(pitch.transform, "Pitch_Turf", new Vector3(0f, 0.04f, 0f), new Vector3(22f, 0.08f, 14f), _parkGrassMat, solid: true);
            BuildDecorativeBox(pitch.transform, "Pitch_MidLine", new Vector3(0f, 0.09f, 0f), new Vector3(0.14f, 0.012f, 13.6f), _laneWhiteMat);

            // West & East Football Goals with real FootballGoal scoring trigger!
            for (int side = -1; side <= 1; side += 2)
            {
                float gx = side * 10.2f;
                GameObject goal = new GameObject(side < 0 ? "Battalgazi_Goal_West" : "Battalgazi_Goal_East");
                goal.transform.SetParent(pitch.transform, false);
                goal.transform.localPosition = new Vector3(gx, 0.08f, 0f);
                BuildWall(goal.transform, "PostL", new Vector3(0f, 1.15f, -1.9f), new Vector3(0.14f, 2.3f, 0.14f), _whiteTrimMat);
                BuildWall(goal.transform, "PostR", new Vector3(0f, 1.15f,  1.9f), new Vector3(0.14f, 2.3f, 0.14f), _whiteTrimMat);
                BuildDecorativeBox(goal.transform, "Crossbar", new Vector3(0f, 2.28f, 0f), new Vector3(0.14f, 0.14f, 3.94f), _whiteTrimMat);

                GameObject trigGo = new GameObject("GoalScoreTrigger");
                trigGo.transform.SetParent(goal.transform, false);
                trigGo.transform.localPosition = new Vector3(side * 0.55f, 1.1f, 0f);
                BoxCollider bc = trigGo.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(1.1f, 2.2f, 3.6f);
                trigGo.AddComponent<FootballGoal>();
            }

            // Real kickable StreetFootball at center spot!
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Battalgazi_StreetFootball";
            ball.transform.SetParent(pitch.transform, false);
            ball.transform.localPosition = new Vector3(0f, 0.30f, 0f);
            ball.transform.localScale = Vector3.one * 0.38f;
            SetupMeshMat(ball, _whiteTrimMat, castShadows: true);
            ball.AddComponent<StreetFootball>();
            InteractableTrigger ballTrig = ball.AddComponent<InteractableTrigger>();
            ballTrig.Configure(InteractableKind.Football, "battalgazi_street_ball", "ui.btn.kick", 2.3f);
            ballTrig.SetCustomPrompt("Şut Çek (Battalgazi Sahası)", "Kick Ball (Battalgazi Pitch)");
        }

        private void BuildDistrictFuelStation(Transform parent, Vector3 pos)
        {
            GameObject st = new GameObject("TurgutReis_FuelStation");
            st.transform.SetParent(parent, false);
            st.transform.position = pos;

            BuildSlab(st.transform, "FuelForecourt_Asphalt", new Vector3(0f, 0.04f, 0f), new Vector3(18f, 0.08f, 14f), _asphaltMat, solid: true);
            BuildDecorativeBox(st.transform, "FuelCanopy_Roof", new Vector3(0f, 4.8f, 0f), new Vector3(14f, 0.65f, 8.5f), _whiteTrimMat);
            BuildDecorativeBox(st.transform, "FuelCanopy_RedStripe", new Vector3(0f, 4.8f, 4.30f), new Vector3(14.1f, 0.45f, 0.14f), _metroRedMat);
            BuildWall(st.transform, "FuelColL", new Vector3(-5.2f, 2.4f, 0f), new Vector3(0.55f, 4.8f, 0.55f), _whiteTrimMat);
            BuildWall(st.transform, "FuelColR", new Vector3( 5.2f, 2.4f, 0f), new Vector3(0.55f, 4.8f, 0.55f), _whiteTrimMat);

            // Central Pump Island + Real Working FuelPump Trigger!
            BuildWall(st.transform, "PumpIsland", new Vector3(0f, 0.95f, 0f), new Vector3(1.1f, 1.9f, 0.85f), _metroRedMat);
            BuildSignTextMesh(st.transform, new Vector3(0f, 4.82f, 4.42f), Quaternion.Euler(0f, 180f, 0f), "AYDOS PETROL · 7/24", 0.12f, 40, Color.white);

            GameObject pumpTrigGo = new GameObject("TurgutReis_FuelPump");
            pumpTrigGo.transform.SetParent(st.transform, false);
            pumpTrigGo.transform.localPosition = new Vector3(0f, 0.9f, 2.0f);
            InteractableTrigger trig = pumpTrigGo.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.FuelPump, "fuel_pump_turgutreis", "ui.btn.refuel", 5.2f);
            trig.SetCustomPrompt("Yakıt Doldur (Turgut Reis Petrol)", "Refuel Vehicle (Turgut Reis Fuel)");
        }

        private void BuildMiniBasketballHoop(Transform parent, Vector3 pos, float yaw)
        {
            GameObject h = new GameObject("School_BasketballHoop");
            h.transform.SetParent(parent, false);
            h.transform.position = pos;
            h.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            BuildWall(h.transform, "HoopPole", new Vector3(0f, 1.8f, 0f), new Vector3(0.18f, 3.6f, 0.18f), _darkMetalMat);
            BuildDecorativeBox(h.transform, "Backboard", new Vector3(0f, 3.25f, 0.45f), new Vector3(1.6f, 1.05f, 0.10f), _whiteTrimMat);
            BuildDecorativeBox(h.transform, "Rim", new Vector3(0f, 2.90f, 0.82f), new Vector3(0.52f, 0.06f, 0.52f), _canopyPalette[1]);
        }

        private void BuildFlagpole(Transform parent, Vector3 localPos, float height)
        {
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Turkish_Flagpole";
            pole.transform.SetParent(parent, false);
            pole.transform.localPosition = localPos + Vector3.up * (height * 0.5f);
            pole.transform.localScale = new Vector3(0.14f, height * 0.5f, 0.14f);
            SetupMeshMat(pole, _whiteTrimMat, castShadows: false);

            GameObject flagPivot = new GameObject("TurkishFlag_Pivot");
            flagPivot.transform.SetParent(parent, false);
            flagPivot.transform.localPosition = localPos + Vector3.up * (height - 0.65f);
            BuildDecorativeBox(flagPivot.transform, "FlagCloth", new Vector3(0.95f, 0f, 0f), new Vector3(1.85f, 1.15f, 0.04f), _turkishRedMat);
            BuildDecorativeBox(flagPivot.transform, "FlagCrescent", new Vector3(0.72f, 0f, 0.03f), new Vector3(0.32f, 0.36f, 0.02f), _whiteTrimMat);
            BuildDecorativeBox(flagPivot.transform, "FlagCrescentBack", new Vector3(0.72f, 0f, -0.03f), new Vector3(0.32f, 0.36f, 0.02f), _whiteTrimMat);
            _flags.Add(flagPivot.transform);
        }

        private void BuildSittableBench(Transform parent, Vector3 pos, float yaw, string idTag)
        {
            GameObject bench = new GameObject($"DistrictBench_{idTag}");
            bench.transform.SetParent(parent, false);
            bench.transform.localPosition = pos;
            bench.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            BuildWall(bench.transform, "BenchSeat", new Vector3(0f, 0.45f, 0f), new Vector3(1.95f, 0.08f, 0.52f), _woodMat);
            BuildDecorativeBox(bench.transform, "BenchBack", new Vector3(0f, 0.80f, -0.22f), new Vector3(1.95f, 0.42f, 0.07f), _woodMat);
            BuildDecorativeBox(bench.transform, "LegL", new Vector3(-0.82f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.46f), _darkMetalMat);
            BuildDecorativeBox(bench.transform, "LegR", new Vector3( 0.82f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.46f), _darkMetalMat);

            GameObject sitPt = new GameObject("SitPoint");
            sitPt.transform.SetParent(bench.transform, false);
            sitPt.transform.localPosition = new Vector3(0f, 0.50f, 0.04f);
            sitPt.transform.localRotation = Quaternion.identity;

            InteractableTrigger trig = bench.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.Seat, $"seat_dist_{idTag}", "ui.btn.sit", 2.3f);
            SeatInteractable seat = bench.AddComponent<SeatInteractable>();
            seat.Configure(sitPt.transform, trig, occupied: false);
        }

        private void BuildPlaneTree(Transform parent, Vector3 pos, float scale)
        {
            GameObject tree = new GameObject("District_CinarTree");
            tree.transform.SetParent(parent, false);
            tree.transform.position = pos;
            tree.transform.localScale = Vector3.one * scale;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            trunk.transform.localScale = new Vector3(0.46f, 1.8f, 0.46f);
            Destroy(trunk.GetComponent<Collider>());
            SetupMeshMat(trunk, _trunkMat, castShadows: false);

            GameObject crown1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown1.name = "CrownMain";
            crown1.transform.SetParent(tree.transform, false);
            crown1.transform.localPosition = new Vector3(0f, 4.6f, 0f);
            crown1.transform.localScale = new Vector3(4.2f, 3.2f, 4.2f);
            Destroy(crown1.GetComponent<Collider>());
            SetupMeshMat(crown1, _planeLeafMat, castShadows: true);

            WorldPhysicsSolidifier.EnsureTrunkCollider(tree, radius: 0.36f, height: 3.4f);
        }

        private void BuildPineTree(Transform parent, Vector3 pos, float scale)
        {
            GameObject tree = new GameObject("District_AydosPine");
            tree.transform.SetParent(parent, false);
            tree.transform.position = pos;
            tree.transform.localScale = Vector3.one * scale;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            trunk.transform.localScale = new Vector3(0.38f, 1.4f, 0.38f);
            Destroy(trunk.GetComponent<Collider>());
            SetupMeshMat(trunk, _trunkMat, castShadows: false);

            for (int tier = 0; tier < 2; tier++)
            {
                GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                foliage.name = $"PineTier_{tier}";
                foliage.transform.SetParent(tree.transform, false);
                foliage.transform.localPosition = new Vector3(0f, 3.5f + tier * 1.9f, 0f);
                float r = tier == 0 ? 3.2f : 2.2f;
                foliage.transform.localScale = new Vector3(r, 2.5f, r);
                Destroy(foliage.GetComponent<Collider>());
                SetupMeshMat(foliage, _pineMat, castShadows: tier == 0);
            }

            WorldPhysicsSolidifier.EnsureTrunkCollider(tree, radius: 0.32f, height: 3.0f);
        }

        private void BuildStreetLamp(Transform parent, Vector3 localPos)
        {
            GameObject lamp = new GameObject("District_StreetLamp");
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = localPos;

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "LampPole";
            pole.transform.SetParent(lamp.transform, false);
            pole.transform.localPosition = new Vector3(0f, 2.8f, 0f);
            pole.transform.localScale = new Vector3(0.14f, 2.8f, 0.14f);
            SetupMeshMat(pole, _darkMetalMat, castShadows: false);

            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "LampBulb";
            bulb.transform.SetParent(lamp.transform, false);
            bulb.transform.localPosition = new Vector3(0f, 5.7f, 0f);
            bulb.transform.localScale = new Vector3(0.46f, 0.34f, 0.46f);
            Destroy(bulb.GetComponent<Collider>());
            MeshRenderer mr = SetupMeshMat(bulb, _lampBulbMat, castShadows: false);

            Light pt = bulb.AddComponent<Light>();
            pt.type = LightType.Point;
            pt.color = new Color(1.0f, 0.84f, 0.56f);
            pt.intensity = 38f;
            pt.range = 38f;
            pt.shadows = LightShadows.None;

            NightLight nl = lamp.AddComponent<NightLight>();
            nl.Configure(pt, mr != null ? new Renderer[] { mr } : Array.Empty<Renderer>(), new Color(1.0f, 0.82f, 0.48f) * 2.2f);
        }

        private void BuildSignBoard(Transform parent, Vector3 localPos, float yaw, string line1, string line2, Material bgMat)
        {
            GameObject board = new GameObject($"Sign_{line1}");
            board.transform.SetParent(parent, false);
            board.transform.localPosition = localPos;
            board.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            BuildDecorativeBox(board.transform, "BoardBg", Vector3.zero, new Vector3(7.6f, 1.35f, 0.22f), bgMat);
            BuildSignTextMesh(board.transform, new Vector3(0f, 0.22f, -0.14f), Quaternion.identity, line1, 0.10f, 40, Color.white);
            BuildSignTextMesh(board.transform, new Vector3(0f, -0.26f, -0.14f), Quaternion.identity, line2, 0.07f, 30, new Color(0.96f, 0.90f, 0.65f));
        }

        private static void BuildSignTextMesh(Transform parent, Vector3 localPos, Quaternion rot, string text, float charSize, int fontSize, Color col)
        {
            GameObject go = new GameObject("SignText");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = charSize;
            tm.fontSize = fontSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = col;
        }

        private static InteractableTrigger BuildInvisibleInteractPoint(
            Transform parent, string name, Vector3 localPos, InteractableKind kind, string id, float radius, string promptTr, string promptEn)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            InteractableTrigger trig = go.AddComponent<InteractableTrigger>();
            trig.Configure(kind, id, "ui.btn.interact", radius);
            trig.SetCustomPrompt(promptTr, promptEn);
            return trig;
        }

        private static void BuildSlab(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool solid)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (!solid)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }
            SetupMeshMat(go, mat, castShadows: false);
        }

        private static void BuildWall(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            SetupMeshMat(go, mat, castShadows: true);
        }

        private static void BuildDecorativeBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            SetupMeshMat(go, mat, castShadows: false);
        }

        private static MeshRenderer SetupMeshMat(GameObject go, Material mat, bool castShadows)
        {
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            return mr;
        }

        // =====================================================================
        // INTERACTIVE STALLS, FOUNTAINS, BAKERY, MARKET & VIEWPOINT HANDLER
        // =====================================================================
        private void HandleDistrictInteracted(InteractableTrigger trig)
        {
            if (trig == null || string.IsNullOrEmpty(trig.InteractableId)) return;
            if (!trig.InteractableId.StartsWith("district_", StringComparison.Ordinal)) return;
            if (Time.time - _lastInteractTime < 0.65f) return;
            _lastInteractTime = Time.time;

            bool en = LocalizationService.IsEnglish;
            PlayerModeController mode = PlayerModeController.Instance;
            SaveManager sm = SaveManager.Instance;
            PlayerNeedsController needs = PlayerNeedsController.Instance;
            Vector3 p = mode != null ? mode.ActiveWorldPosition : Vector3.zero;

            switch (trig.InteractableId)
            {
                case "district_abdurrahman_fountain":
                case "district_mimarsinan_fountain":
                    needs?.ApplyMealRestore(12f, 22f, 10f);
                    GameFeelAudio.PlayUiClick();
                    GameFeelAudio.SpawnBurst(p + Vector3.up * 1.2f, new Color(0.28f, 0.80f, 0.98f), count: 8, spread: 0.4f);
                    mode?.RaiseStatusMessage(en
                        ? "💧 Refreshed with ice-cold Istanbul fountain water! (+22 Energy)"
                        : "💧 Buz gibi çeşme suyundan ferahladın! (+22 Enerji)");
                    break;

                case "district_abdurrahman_tea":
                    if (sm != null && !sm.TrySpend(15))
                    {
                        mode?.RaiseStatusMessage(en ? "Hot tea & simit costs 15 ₺!" : "Çay & simit 15 ₺!");
                        return;
                    }
                    needs?.ApplyMealRestore(30f, 26f, 10f);
                    GameFeelAudio.PlayCoinChime();
                    mode?.RaiseStatusMessage(en
                        ? "🫖 Enjoyed hot Turkish tea & crispy simit at Kent Meydanı! (-15 ₺)"
                        : "🫖 Kent Meydanı'nda taze çay & çıtır simit keyfi! (-15 ₺)");
                    break;

                case "district_mehmetakif_bakery":
                    if (sm != null && !sm.TrySpend(25))
                    {
                        mode?.RaiseStatusMessage(en ? "Warm pastry pack costs 25 ₺!" : "Sıcak poğaça paketi 25 ₺!");
                        return;
                    }
                    PlayerBackpackInventory.EnsureInstance().AddItem(
                        "item_pogaca_pack", "Sıcak Poğaça Paketi", "Warm Pastry Pack", 2,
                        hungerRestore: 38f, energyRestore: 28f, healthRestore: 12f,
                        ConsumeAnimKind.EatFood, ShopPropVisual.FreshFruit);
                    GameFeelAudio.PlayCoinChime();
                    mode?.RaiseStatusMessage(en
                        ? "🥐 Packed 2 warm pastries into your Backpack! (-25 ₺)"
                        : "🥐 2 adet sıcak poğaça çantana eklendi! (-25 ₺)");
                    break;

                case "district_hasanpasa_kiosk":
                    if (sm != null && !sm.TrySpend(30))
                    {
                        mode?.RaiseStatusMessage(en ? "Ayran & Fruit Bag costs 30 ₺!" : "Ayran & Meyve Çantası 30 ₺!");
                        return;
                    }
                    PlayerBackpackInventory.EnsureInstance().AddItem(
                        "item_fruit_bag", "Meyve Çantası", "Fruit Bag", 2,
                        hungerRestore: 36f, energyRestore: 28f, healthRestore: 14f,
                        ConsumeAnimKind.EatFood, ShopPropVisual.FreshFruit);
                    needs?.ApplyMealRestore(14f, 20f, 8f);
                    GameFeelAudio.PlayCoinChime();
                    mode?.RaiseStatusMessage(en
                        ? "🥤 Drank cold Ayran & packed a Fruit Bag in Hasanpaşa Park! (-30 ₺)"
                        : "🥤 Soğuk ayranını içtin & Meyve Çantası çantana eklendi! (-30 ₺)");
                    break;

                case "district_battalgazi_pazar":
                    if (sm != null && !sm.TrySpend(35))
                    {
                        mode?.RaiseStatusMessage(en ? "Fresh Bazaar Fruit Bag costs 35 ₺!" : "Semt Pazarı Meyve Çantası 35 ₺!");
                        return;
                    }
                    PlayerBackpackInventory.EnsureInstance().AddItem(
                        "item_pazar_fruit_bag", "Pazar Meyve Çantası", "Bazaar Fruit Bag", 3,
                        hungerRestore: 42f, energyRestore: 32f, healthRestore: 18f,
                        ConsumeAnimKind.EatFood, ShopPropVisual.FreshFruit);
                    GameFeelAudio.PlayCoinChime();
                    NeighborhoodReputation.AddReputation(8);
                    mode?.RaiseStatusMessage(en
                        ? "🍎 Packed 3 servings of fresh Battalgazi Bazaar Fruit into your Backpack! (-35 ₺)"
                        : "🍎 Battalgazi Semt Pazarı'ndan 3 porsiyon taze meyve çantana eklendi! (-35 ₺)");
                    break;

                case "district_mimarsinan_icecream":
                    if (sm != null && !sm.TrySpend(20))
                    {
                        mode?.RaiseStatusMessage(en ? "Maraş ice cream costs 20 ₺!" : "Maraş dondurması 20 ₺!");
                        return;
                    }
                    needs?.ApplyMealRestore(24f, 30f, 12f);
                    GameFeelAudio.PlayCoinChime();
                    mode?.RaiseStatusMessage(en
                        ? "🍦 Enjoyed authentic Maraş ice cream at Mimar Sinan Park! (-20 ₺)"
                        : "🍦 Mimar Sinan Parkı'nda nefis Maraş dondurması yedin! (-20 ₺)");
                    break;

                case "district_turgutreis_tea":
                    if (sm != null && !sm.TrySpend(30))
                    {
                        mode?.RaiseStatusMessage(en ? "Samovar tea & gözleme costs 30 ₺!" : "Semaver çayı & gözleme 30 ₺!");
                        return;
                    }
                    needs?.ApplyMealRestore(48f, 38f, 18f);
                    GameFeelAudio.PlayCoinChime();
                    mode?.RaiseStatusMessage(en
                        ? "🫖 Enjoyed samovar tea & fresh gözleme under Aydos pines! (-30 ₺)"
                        : "🫖 Aydos çamlarının altında semaver çayı & gözleme keyfi! (-30 ₺)");
                    break;

                case "district_aydos_viewpoint":
                    needs?.RestoreEnergy(25f);
                    NeighborhoodReputation.AddReputation(12);
                    GameFeelAudio.PlayMilestoneFanfare();
                    GameFeelAudio.SpawnBurst(p + Vector3.up * 1.6f, new Color(0.98f, 0.78f, 0.26f), count: 14, spread: 0.7f);
                    mode?.RaiseStatusMessage(en
                        ? "🏰 Breathtaking view from Historic Aydos Castle overlooking all 7 Sultanbeyli districts! (+25 Energy)"
                        : "🏰 Tarihi Aydos Kalesi'nden tüm Sultanbeyli ayaklarının altında! (+25 Enerji · +12 Saygınlık)");
                    break;
            }
        }
    }
}
