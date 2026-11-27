using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Populates Istanbul · Sultanbeyli (Hamidiye + all 6 neighboring districts) with realistic,
    /// diverse urban demographics:
    /// - Playful children (scaled 0.66–0.75x with natural child head ratio, 3D school backpacks & baseball caps)
    ///   playing tag, cheering near parks/schools/playgrounds/pitches, and waving at Deniz!
    /// - Parent + Child family pairs (mothers with sculpted 3D headscarves & fathers strolling side-by-side with kids).
    /// - Neighborhood elders (Dede with tweed flat cap & white mustache, Teyze with silver hair & silk headscarf).
    /// - Students & youth with hoodies & backpacks near M5 Metro stations, schools & Atlas AVM.
    /// - Municipal street sweepers (hi-vis reflective vest + 3D broom), delivery couriers (thermal backpack box),
    ///   construction/utility workers (yellow hard hat), and patrolling Zabıta officers (navy peaked cap & badge).
    ///
    /// Full 3D physical collision (CapsuleCollider + kinematic Rigidbody + LivingNpcColliderBridge),
    /// smart obstacle/traffic/rain AI (NpcSmartNavigation), interactive SmartNpcInteractionPanel conversations,
    /// strict child-safety rules, and 3-tier distance LOD for locked 60 FPS mobile performance.
    /// </summary>
    public sealed class IstanbulDiverseCitizensPopulator : MonoBehaviour
    {
        public static IstanbulDiverseCitizensPopulator Instance { get; private set; }

        public enum CitizenArchetype
        {
            ChildPlay = 0,
            ChildWithParent = 1,
            Mother = 2,
            Father = 3,
            ElderMan = 4,
            ElderWoman = 5,
            Student = 6,
            Sweeper = 7,
            Courier = 8,
            Zabita = 9,
            Worker = 10
        }

        private sealed class DiverseCitizen
        {
            public string InteractableId;
            public GameObject Root;
            public Rigidbody Rb;
            public ProceduralHumanoid Humanoid;
            public CitizenArchetype Archetype;
            public string DisplayName;
            public string RoleTr;
            public string RoleEn;
            public bool IsChild;
            public bool IsAuthority;
            public bool IsFighter;
            public float HeightScale;
            public float BubbleHeight;

            public Vector3[] Route;
            public int WaypointIdx;
            public float Speed;
            public float PauseTimer;
            public float PlayActionTimer;
            public float GreetingCooldown;
            public float TalkTimer;
            public float LookRetarget;
            public string[] LinesTr;
            public string[] LinesEn;

            // Family / tag buddy linking
            public DiverseCitizen ParentPartner;
            public DiverseCitizen TagPartner;
            public float PartnerSideSign;

            // Smart dodging, weather, bump & stuck recovery
            public float BumpCooldown;
            public float VehicleHitCooldown;
            public float SidestepTimer;
            public float SidestepDuration;
            public float SidestepPeakSpeed;
            public Vector3 SidestepDir;
            public float CarDodgeCooldown;
            public float EventReactCooldown;
            public bool RainShieldActive;
            public Vector3 LastProgressPos;
            public float StuckTimer;

            // Combat / flee / health / knockout reaction state (children NEVER get knocked down or hurt!)
            public float Health;
            public float MaxHealth;
            public bool IsKnockedOut;
            public float HitCooldown;
            public float KnockdownTimer;
            public float FleeTimer;
            public float FightTimer;
            public float PunchCooldown;
            public float PendingPunchTimer;
            public int HitCount;
            public Vector3 StumbleVel;
            public Vector3 FleeDir;
            public Vector3 AirVel;
            public bool IsAirborne;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private readonly List<DiverseCitizen> _citizens = new List<DiverseCitizen>(72);
        private readonly List<Material> _ownedMats = new List<Material>(64);

        private Material[] _kidShirtMats;
        private Material[] _adultShirtMats;
        private Material[] _pantsMats;
        private Material[] _skinMats;
        private Material[] _hairMats;
        private Material _silverHairMat;
        private Material[] _scarfMats;
        private Material[] _backpackMats;
        private Material _hiVisYellowMat;
        private Material _hiVisOrangeMat;
        private Material _reflectiveMat;
        private Material _zabitaNavyMat;
        private Material _goldBadgeMat;
        private Material _hardHatYellowMat;
        private Material _tweedCapMat;
        private Material _woodMat;
        private Material _strawMat;
        private Material _darkVisorMat;

        private float _globalBubbleCooldown;
        private bool _subscribed;
        private int _lodFrameBucket;

        public int SpawnedCount => _citizens.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;

            GameObject go = new GameObject("OurWorld_IstanbulDiverseCitizensPopulator");
            go.AddComponent<IstanbulDiverseCitizensPopulator>();
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

        private IEnumerator Start()
        {
            // Wait 3 frames so SultanbeyliDistrictsBuilder, WorldExpansionAndFarmBuilder & Player finish initializing
            yield return null;
            yield return null;
            yield return null;

            ProceduralHumanoid template = PlayerModeController.Instance?.FootController?.Humanoid;
            if (template == null) yield break;

            BuildSharedMaterialPalette();
            PopulateAllSultanbeyliDemographics(template);

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted += HandleCitizenInteracted;
                PlayerModeController.Instance.RefreshInteractablesCache();
                _subscribed = true;
            }

            NpcWorldAwareness.OnViolenceOccurred += HandleWorldViolence;
            NpcWorldAwareness.OnHornHeard += HandleWorldHorn;
            NpcWorldAwareness.OnCarAlarmSounded += HandleWorldAlarm;
            NpcWorldAwareness.OnPropImpactOccurred += HandleWorldPropImpact;
            NpcWorldAwareness.OnRecklessDriving += HandleWorldRecklessDriving;

            Debug.Log($"[IstanbulDiverseCitizensPopulator] Populated {_citizens.Count} diverse Istanbul citizens (children, families, elders, students, sweepers, couriers, workers & Zabıta) across Hamidiye + 6 Sultanbeyli districts.");
        }

        private void OnDestroy()
        {
            NpcWorldAwareness.OnViolenceOccurred -= HandleWorldViolence;
            NpcWorldAwareness.OnHornHeard -= HandleWorldHorn;
            NpcWorldAwareness.OnCarAlarmSounded -= HandleWorldAlarm;
            NpcWorldAwareness.OnPropImpactOccurred -= HandleWorldPropImpact;
            NpcWorldAwareness.OnRecklessDriving -= HandleWorldRecklessDriving;

            if (_subscribed && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted -= HandleCitizenInteracted;
            }
            for (int i = 0; i < _ownedMats.Count; i++)
            {
                if (_ownedMats[i] != null) Destroy(_ownedMats[i]);
            }
            _ownedMats.Clear();
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        // SHARED PBR MATERIAL PALETTE (SRP-BATCHER & MOBILE MEMORY SAFE)
        // =====================================================================
        private Material MakeMat(Shader sh, Color col, float smoothness = 0.22f)
        {
            Material m = new Material(sh);
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, col);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, col);
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, smoothness);
            _ownedMats.Add(m);
            return m;
        }

        private void BuildSharedMaterialPalette()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (lit == null) return;

            _kidShirtMats = new[]
            {
                MakeMat(lit, new Color(0.18f, 0.68f, 0.94f)), // Sky blue tee
                MakeMat(lit, new Color(0.96f, 0.76f, 0.16f)), // Sunshine yellow
                MakeMat(lit, new Color(0.92f, 0.30f, 0.34f)), // Coral red
                MakeMat(lit, new Color(0.24f, 0.76f, 0.48f)), // Mint green
                MakeMat(lit, new Color(0.70f, 0.44f, 0.88f)), // Lilac purple
                MakeMat(lit, new Color(0.96f, 0.50f, 0.18f)), // Tangerine orange
                MakeMat(lit, new Color(0.94f, 0.44f, 0.66f))  // Rose pink
            };

            _adultShirtMats = new[]
            {
                MakeMat(lit, new Color(0.24f, 0.38f, 0.54f)), // Slate blue cardigan
                MakeMat(lit, new Color(0.56f, 0.24f, 0.28f)), // Burgundy coat
                MakeMat(lit, new Color(0.32f, 0.48f, 0.38f)), // Sage olive
                MakeMat(lit, new Color(0.76f, 0.64f, 0.50f)), // Warm camel beige
                MakeMat(lit, new Color(0.26f, 0.28f, 0.36f)), // Charcoal jacket
                MakeMat(lit, new Color(0.68f, 0.42f, 0.26f)), // Cinnamon knit
                MakeMat(lit, new Color(0.18f, 0.52f, 0.56f)), // Bosphorus teal
                MakeMat(lit, new Color(0.84f, 0.80f, 0.72f))  // Cream sweater
            };

            _pantsMats = new[]
            {
                MakeMat(lit, new Color(0.16f, 0.24f, 0.38f), 0.14f), // Classic denim
                MakeMat(lit, new Color(0.14f, 0.15f, 0.18f), 0.16f), // Charcoal
                MakeMat(lit, new Color(0.42f, 0.36f, 0.28f), 0.15f), // Warm khaki
                MakeMat(lit, new Color(0.22f, 0.20f, 0.26f), 0.15f), // Dark navy
                MakeMat(lit, new Color(0.30f, 0.22f, 0.18f), 0.16f)  // Espresso brown
            };

            _skinMats = new[]
            {
                MakeMat(lit, new Color(0.88f, 0.70f, 0.57f), 0.34f),
                MakeMat(lit, new Color(0.80f, 0.60f, 0.46f), 0.34f),
                MakeMat(lit, new Color(0.71f, 0.51f, 0.38f), 0.34f)
            };

            _hairMats = new[]
            {
                MakeMat(lit, new Color(0.11f, 0.08f, 0.06f), 0.28f), // Dark espresso
                MakeMat(lit, new Color(0.26f, 0.16f, 0.09f), 0.28f), // Chestnut brown
                MakeMat(lit, new Color(0.46f, 0.32f, 0.17f), 0.28f), // Honey brown
                MakeMat(lit, new Color(0.66f, 0.52f, 0.30f), 0.28f)  // Warm blonde
            };
            _silverHairMat = MakeMat(lit, new Color(0.82f, 0.83f, 0.86f), 0.26f);

            _scarfMats = new[]
            {
                MakeMat(lit, new Color(0.92f, 0.87f, 0.78f), 0.24f), // Warm silk cream
                MakeMat(lit, new Color(0.62f, 0.20f, 0.28f), 0.26f), // Ottoman burgundy
                MakeMat(lit, new Color(0.18f, 0.44f, 0.48f), 0.26f), // Iznik teal
                MakeMat(lit, new Color(0.76f, 0.54f, 0.58f), 0.24f), // Dusty rose
                MakeMat(lit, new Color(0.18f, 0.24f, 0.42f), 0.26f)  // Midnight navy
            };

            _backpackMats = new[]
            {
                MakeMat(lit, new Color(0.90f, 0.24f, 0.26f), 0.26f), // Red school bag
                MakeMat(lit, new Color(0.16f, 0.50f, 0.88f), 0.26f), // Cobalt school bag
                MakeMat(lit, new Color(0.95f, 0.68f, 0.14f), 0.26f), // Gold backpack
                MakeMat(lit, new Color(0.22f, 0.68f, 0.44f), 0.26f), // Emerald backpack
                MakeMat(lit, new Color(0.58f, 0.32f, 0.78f), 0.26f)  // Violet backpack
            };

            _hiVisYellowMat = MakeMat(lit, new Color(0.86f, 0.96f, 0.14f), 0.28f);
            _hiVisOrangeMat = MakeMat(lit, new Color(0.98f, 0.42f, 0.08f), 0.30f);
            _reflectiveMat = MakeMat(lit, new Color(0.88f, 0.92f, 0.96f), 0.78f);
            _zabitaNavyMat = MakeMat(lit, new Color(0.08f, 0.16f, 0.32f), 0.24f);
            _goldBadgeMat = MakeMat(lit, new Color(0.95f, 0.76f, 0.22f), 0.72f);
            _hardHatYellowMat = MakeMat(lit, new Color(0.98f, 0.80f, 0.08f), 0.68f);
            _tweedCapMat = MakeMat(lit, new Color(0.36f, 0.32f, 0.28f), 0.16f);
            _woodMat = MakeMat(lit, new Color(0.56f, 0.37f, 0.20f), 0.24f);
            _strawMat = MakeMat(lit, new Color(0.82f, 0.68f, 0.34f), 0.14f);
            _darkVisorMat = MakeMat(lit, new Color(0.08f, 0.09f, 0.12f), 0.72f);
        }

        // =====================================================================
        // AUTHENTIC DIALOGUE POOLS (TR + EN)
        // =====================================================================
        private static readonly string[] KidLinesTr =
        {
            "Ebe sensin, yakala bakalım!",
            "Abi araban çok havalıymış!",
            "Anne bana da dondurma alalım mı?",
            "Okuldan yeni çıktık, parka gidiyoruz!",
            "Deniz Abi gel beraber maç yapalım!",
            "Bak kedi şurada uyuyor, çok tatlı!",
            "En hızlı ben koşuyorum, izle!",
            "Abdurrahmangazi Meydanı'ndaki fıskiyeye gidelim!"
        };
        private static readonly string[] KidLinesEn =
        {
            "You're it, try to catch me!",
            "Whoa, your car looks awesome!",
            "Mom, can we get ice cream too?",
            "School just finished, we're heading to the park!",
            "Hey Deniz, come play football with us!",
            "Look at that sleepy street cat, so cute!",
            "Watch me run, I'm the fastest!",
            "Let's go see the square fountain!"
        };

        private static readonly string[] ParentLinesTr =
        {
            "Yavaş koş yavrum, düşeceksin sonra!",
            "Pazardan taze çilek ve kiraz aldık.",
            "Hava çok güzel, çocukla parka çıktık.",
            "Sultanbeyli'nin yeni parkları çocuklar için harika oldu.",
            "Ödevlerini bitirince akşam dışarıda oynarsın canım."
        };
        private static readonly string[] ParentLinesEn =
        {
            "Slow down sweetheart, don't trip!",
            "We picked up fresh strawberries and cherries at the market.",
            "Such lovely weather for a walk with the kids.",
            "The new Sultanbeyli parks are wonderful for families.",
            "Finish your homework and you can play outside this evening."
        };

        private static readonly string[] ElderLinesTr =
        {
            "Hayırlı günler evladım, Allah kolaylık versin.",
            "Eskiden Aydos etekleri baştan başa kiraz bahçesiydi yavrum.",
            "Çeşmenin suyu buz gibi akıyor maşallah.",
            "Aydos Kalesi'ne çıktın mı? Oradan bütün Sultanbeyli görünür!",
            "Komşuluk hakkı önemlidir evladım, selamı eksik etme.",
            "İkindi vakti cami avlusunda çay sohbeti bir başka güzel."
        };
        private static readonly string[] ElderLinesEn =
        {
            "Good day my child, may your day go smoothly.",
            "Back in the day, Aydos hills were all cherry orchards.",
            "The stone fountain water runs ice cold, bless it.",
            "Have you climbed up to Aydos Castle? You can see all of Sultanbeyli!",
            "Good neighbors are a blessing, never forget to greet folks.",
            "Afternoon tea in the mosque courtyard is pure peace."
        };

        private static readonly string[] StudentLinesTr =
        {
            "M5 Sultanbeyli metrosu açılınca ulaşım süper oldu!",
            "Dersten yeni çıktım, bir sıcak simit gider şimdi.",
            "Akşam Atlas AVM'de arkadaşlarla buluşacağız.",
            "Hasanpaşa Millet Parkı'nda koşu pisti çok güzel olmuş.",
            "Erdoğan Caddesi'ndeki kırtasiyeden defter aldım."
        };
        private static readonly string[] StudentLinesEn =
        {
            "The M5 Sultanbeyli Metro made getting around so easy!",
            "Just got out of class, craving a warm sesame simit.",
            "Meeting friends at Atlas Mall this evening.",
            "The jogging track at Hasanpaşa Park is awesome.",
            "Picked up notebooks from Erdoğan Street stationery."
        };

        private static readonly string[] SweeperLinesTr =
        {
            "Sultanbeyli tertemiz kalsın diye sabahtan beri süpürüyoruz!",
            "Çöpleri kutuya atan komşulara helal olsun.",
            "Kent Meydanı pırıl pırıl oldu maşallah.",
            "Kolay gelsin komşu, sokakları taze temizledik!"
        };
        private static readonly string[] SweeperLinesEn =
        {
            "Sweeping since dawn to keep Sultanbeyli spotless!",
            "Huge thanks to neighbors who use the trash bins.",
            "Kent Meydanı square is gleaming clean today.",
            "Have a great day neighbor, fresh swept sidewalks!"
        };

        private static readonly string[] CourierLinesTr =
        {
            "Sipariş sıcak sıcak yetişecek abi, müsaade!",
            "Erdoğan Caddesi'nden üç paket üst üste geldi!",
            "Battalgazi'ye pide götürüyorum, hız kesmek yok!",
            "İyi günler, kargonuz yolda!"
        };
        private static readonly string[] CourierLinesEn =
        {
            "Hot delivery coming through, excuse me!",
            "Three orders back-to-back from Erdoğan Street!",
            "Delivering hot pide to Battalgazi, no time to lose!",
            "Good day, express parcel on the way!"
        };

        private static readonly string[] ZabitaLinesTr =
        {
            "Hayırlı günler komşu, mahallede düzen yerinde.",
            "Battalgazi Semt Pazarı'nda etiket denetimi yapıyoruz.",
            "Sultanbeyli Belediyesi Zabıta ekibi her daim görevde.",
            "Trafik kurallarına ve yaya geçitlerine dikkat edelim lütfen."
        };
        private static readonly string[] ZabitaLinesEn =
        {
            "Good day citizen, everything is orderly in the district.",
            "Inspecting stalls and keeping Battalgazi Market fair.",
            "Sultanbeyli Municipal Police always on patrol.",
            "Please watch out for pedestrians and traffic rules."
        };

        private static readonly string[] WorkerLinesTr =
        {
            "Hasanpaşa'da yeni binaların cephe işleri harika gidiyor!",
            "Baret takmadan sahaya girmeyiz usta, güvenlik önce gelir.",
            "Öğle paydosunda güzel bir çay ve börek iyi gider.",
            "Yeni kaldırım taşları mis gibi oturdu!"
        };
        private static readonly string[] WorkerLinesEn =
        {
            "The urban renewal facades in Hasanpaşa are looking sharp!",
            "Safety first—hard hat stays on at all times.",
            "Tea and pastry will hit the spot at lunch break.",
            "New cobblestones laid down clean and level!"
        };

        // =====================================================================
        // POPULATION ACROSS HAMIDIYE + 6 NEW SULTANBEYLI NEIGHBORHOODS
        // =====================================================================
        private void PopulateAllSultanbeyliDemographics(ProceduralHumanoid template)
        {
            Transform root = new GameObject("Diverse_Istanbul_Citizens").transform;
            root.SetParent(transform, false);

            bool lowTier = GraphicsProfileManager.Instance != null &&
                           GraphicsProfileManager.Instance.CurrentTier == OurWorld.Core.GraphicsTier.Low;
            int idSeq = 0;

            // -----------------------------------------------------------------
            // 1. PLAYFUL CHILDREN (18 kids across schools, parks, playgrounds & plazas — 12 on Low tier)
            // -----------------------------------------------------------------
            (string name, Vector3[] route, bool hasCap)[] kidConfigs =
            {
                // Hamidiye Mahallesi (Schools, Park, Sports & Selman Street)
                ("Efe",   new[] { new Vector3(-22f, 0.06f, -112f), new Vector3(-11f, 0.06f, -112f), new Vector3(-16f, 0.06f, -106f) }, true),
                ("Ada",   new[] { new Vector3(-12f, 0.06f, -109f), new Vector3(-21f, 0.06f, -108f), new Vector3(-16f, 0.06f, -114f) }, false),
                ("Kerem", new[] { new Vector3(-26f, 0.06f, 64f),   new Vector3(-14f, 0.06f, 64f),   new Vector3(-20f, 0.06f, 72f) },   true),
                ("Defne", new[] { new Vector3(-16f, 0.06f, 70f),   new Vector3(-25f, 0.06f, 68f) },                                    false),
                ("Arda",  new[] { new Vector3(-22f, 0.06f, -26f),  new Vector3(-8f, 0.06f, -26f),   new Vector3(-14f, 0.06f, -20f) },  true),
                // Abdurrahmangazi · Sultanbeyli Kent Meydanı (NE)
                ("Emir",  new[] { new Vector3(172f, 0.08f, 189f),  new Vector3(188f, 0.08f, 189f),  new Vector3(186f, 0.08f, 202f), new Vector3(174f, 0.08f, 202f) }, true),
                ("Ela",   new[] { new Vector3(186f, 0.08f, 201f),  new Vector3(174f, 0.08f, 190f) },                                    false),
                // Mehmet Akif · Ersoy İlkokulu Courtyard (N)
                ("Yigit", new[] { new Vector3(16f, 0.08f, 300f),   new Vector3(32f, 0.08f, 300f),   new Vector3(24f, 0.08f, 312f) },   true),
                ("Lina",  new[] { new Vector3(30f, 0.08f, 310f),   new Vector3(18f, 0.08f, 308f),   new Vector3(22f, 0.08f, 300f) },   false),
                // Hasanpaşa · Millet Parkı Playground & Track (NW)
                ("Mert",  new[] { new Vector3(-196f, 0.08f, 202f), new Vector3(-174f, 0.08f, 202f), new Vector3(-185f, 0.08f, 214f) }, true),
                ("Azra",  new[] { new Vector3(-176f, 0.08f, 210f), new Vector3(-194f, 0.08f, 210f) },                                  false),
                // Battalgazi · Semt Pazarı & Street Football Pitch (SW)
                ("Berat", new[] { new Vector3(-174f, 0.08f, -195f),new Vector3(-156f, 0.08f, -195f),new Vector3(-164f, 0.08f, -206f) },true),
                ("Mira",  new[] { new Vector3(-158f, 0.08f, -204f),new Vector3(-172f, 0.08f, -202f) },                                 false),
                // Mimar Sinan · Kültür Parkı Playground (S)
                ("Çınar", new[] { new Vector3(-60f, 0.08f, -278f), new Vector3(-42f, 0.08f, -278f), new Vector3(-50f, 0.08f, -292f) }, true),
                ("Zümra", new[] { new Vector3(-44f, 0.08f, -290f), new Vector3(-58f, 0.08f, -288f) },                                  false),
                ("Poyraz",new[] { new Vector3(-56f, 0.08f, -264f), new Vector3(-36f, 0.08f, -264f) },                                  true),
                // Turgut Reis · Aydos Kale Picnic Grove (SE)
                ("Kaan",  new[] { new Vector3(224f, 0.08f, -124f), new Vector3(244f, 0.08f, -124f), new Vector3(234f, 0.08f, -114f) }, true),
                ("Nehir", new[] { new Vector3(242f, 0.08f, -116f), new Vector3(226f, 0.08f, -118f) },                                  false)
            };

            int maxKids = lowTier ? 12 : kidConfigs.Length;
            DiverseCitizen prevKid = null;
            for (int i = 0; i < maxKids; i++)
            {
                var kc = kidConfigs[i];
                DiverseCitizen kid = SpawnCitizen(
                    root, template, idSeq++,
                    kc.name, "Mahalle Çocuğu", "Neighborhood Kid",
                    CitizenArchetype.ChildPlay,
                    kc.route, UnityEngine.Random.Range(2.35f, 2.95f),
                    KidLinesTr, KidLinesEn,
                    accessoryVariant: kc.hasCap ? 1 : 0);

                // Pair adjacent kids in the same park/school so they play tag together!
                if (prevKid != null && Vector3.Distance(prevKid.Route[0], kid.Route[0]) < 24f)
                {
                    kid.TagPartner = prevKid;
                    prevKid.TagPartner = kid;
                    prevKid = null;
                }
                else
                {
                    prevKid = kid;
                }
            }

            // -----------------------------------------------------------------
            // 2. PARENT + CHILD FAMILY PAIRS (6 heartwarming pairs = 12 actors!)
            // -----------------------------------------------------------------
            (string parentName, bool isMother, string childName, Vector3[] route)[] familyConfigs =
            {
                ("Zeliha Hanım", true,  "Ali",    new[] { new Vector3(-38f, 0.06f, 132.8f),  new Vector3(36f, 0.06f, 132.8f) }),   // Fatih Saat Kulesi
                ("Murat Bey",    false, "Zeynep", new[] { new Vector3(156f, 0.08f, 180f),    new Vector3(204f, 0.08f, 180f) }),    // Abdurrahmangazi Meydan
                ("Sema Hanım",   true,  "Ömer",   new[] { new Vector3(-42f, 0.08f, 274f),    new Vector3(30f, 0.08f, 274f) }),     // Mehmet Akif Erdoğan Cd.
                ("Serkan Bey",   false, "Ece",    new[] { new Vector3(-212f, 0.08f, 184f),   new Vector3(-156f, 0.08f, 184f) }),   // Hasanpaşa Bulvarı
                ("Gülsüm Hanım", true,  "Yusuf",  new[] { new Vector3(-210f, 0.08f, -172f),  new Vector3(-158f, 0.08f, -172f) }),  // Battalgazi Semt Pazarı
                ("Fatma Hanım",  true,  "Ecrin",  new[] { new Vector3(-84f, 0.08f, -264f),   new Vector3(-16f, 0.08f, -264f) })    // Mimar Sinan Caddesi
            };

            int maxFamilies = lowTier ? 4 : familyConfigs.Length;
            for (int f = 0; f < maxFamilies; f++)
            {
                var fc = familyConfigs[f];
                DiverseCitizen parent = SpawnCitizen(
                    root, template, idSeq++,
                    fc.parentName,
                    fc.isMother ? "Anne" : "Baba",
                    fc.isMother ? "Mother" : "Father",
                    fc.isMother ? CitizenArchetype.Mother : CitizenArchetype.Father,
                    fc.route, 1.48f,
                    ParentLinesTr, ParentLinesEn,
                    accessoryVariant: f);

                Vector3 childStart = parent.Root.transform.position + parent.Root.transform.right * 0.72f;
                DiverseCitizen child = SpawnCitizen(
                    root, template, idSeq++,
                    fc.childName, "Çocuk (Ailesiyle)", "Child (with Parent)",
                    CitizenArchetype.ChildWithParent,
                    new[] { childStart }, 1.55f,
                    KidLinesTr, KidLinesEn,
                    accessoryVariant: f % 2);
                child.ParentPartner = parent;
                child.PartnerSideSign = (f % 2 == 0) ? 1f : -1f;
            }

            // -----------------------------------------------------------------
            // 3. NEIGHBORHOOD ELDERS (10 Dedeler & Teyzeler with flat caps & headscarves)
            // -----------------------------------------------------------------
            (string name, bool isMan, Vector3[] route)[] elderConfigs =
            {
                ("Ahmet Dede",   true,  new[] { new Vector3(68f, 0.06f, -22f),    new Vector3(92f, 0.06f, -22f) }),      // Hz. Osman Camii
                ("Nermin Teyze", false, new[] { new Vector3(-36f, 0.06f, 51.4f),  new Vector3(14f, 0.06f, 51.4f) }),     // Mürşit Cd. & Park
                ("Hüseyin Dede", true,  new[] { new Vector3(198f, 0.08f, 206f),   new Vector3(216f, 0.08f, 206f) }),     // Abdurrahmangazi Merkez Camii
                ("Hacer Nine",   false, new[] { new Vector3(162f, 0.08f, 208f),   new Vector3(186f, 0.08f, 208f) }),     // Abdurrahmangazi Meydan
                ("Kemal Amca",   true,  new[] { new Vector3(-28f, 0.08f, 294f),   new Vector3(10f, 0.08f, 294f) }),      // Mehmet Akif
                ("Emine Teyze",  false, new[] { new Vector3(-206f, 0.08f, 198f),  new Vector3(-164f, 0.08f, 198f) }),    // Hasanpaşa Millet Parkı
                ("İbrahim Dede", true,  new[] { new Vector3(-204f, 0.08f, -198f), new Vector3(-174f, 0.08f, -198f) }),   // Battalgazi
                ("Şükran Teyze", false, new[] { new Vector3(-76f, 0.08f, -296f),  new Vector3(-30f, 0.08f, -296f) }),    // Mimar Sinan Çeşmesi
                ("Bayram Dede",  true,  new[] { new Vector3(204f, 0.08f, -124f),  new Vector3(236f, 0.08f, -124f) }),    // Turgut Reis Tea Garden
                ("Remziye Hanım",false, new[] { new Vector3(-162f, 0.06f, -14f),  new Vector3(-162f, 0.06f, 14f) })      // Sultanbeyli Göleti
            };

            int maxElders = lowTier ? 6 : elderConfigs.Length;
            for (int e = 0; e < maxElders; e++)
            {
                var ec = elderConfigs[e];
                SpawnCitizen(
                    root, template, idSeq++,
                    ec.name,
                    ec.isMan ? "Mahalle Büyüğü" : "Mahalle Teyzesi",
                    ec.isMan ? "Neighborhood Elder" : "Elder Neighbor",
                    ec.isMan ? CitizenArchetype.ElderMan : CitizenArchetype.ElderWoman,
                    ec.route, UnityEngine.Random.Range(1.10f, 1.28f),
                    ElderLinesTr, ElderLinesEn,
                    accessoryVariant: e);
            }

            // -----------------------------------------------------------------
            // 4. STUDENTS & YOUTH (8 actors near M5 Metro, Atlas AVM & Plazas)
            // -----------------------------------------------------------------
            (string name, Vector3[] route)[] studentConfigs =
            {
                ("Berke",  new[] { new Vector3(-18f, 0.08f, 266f),  new Vector3(12f, 0.08f, 266f) }),    // M5 Sultanbeyli Metro
                ("İlayda", new[] { new Vector3(-10f, 0.08f, 282f),  new Vector3(-36f, 0.08f, 282f) }),   // Mehmet Akif Shops
                ("Tuna",   new[] { new Vector3(-156f, 0.08f, 174f), new Vector3(-196f, 0.08f, 174f) }),  // M5 Hasanpaşa Metro
                ("Melis",  new[] { new Vector3(-126f, 0.08f, -58f), new Vector3(-104f, 0.08f, -58f) }),  // Atlas AVM Plaza
                ("Batuhan",new[] { new Vector3(166f, 0.08f, 172f),  new Vector3(196f, 0.08f, 172f) }),   // Abdurrahmangazi
                ("Sıla",   new[] { new Vector3(-68f, 0.08f, -272f), new Vector3(-28f, 0.08f, -272f) }),  // Mimar Sinan
                ("Umut",   new[] { new Vector3(-198f, 0.08f, -166f),new Vector3(-164f, 0.08f, -166f) }), // Battalgazi Bosna Blv.
                ("Beren",  new[] { new Vector3(206f, 0.08f, -152f), new Vector3(244f, 0.08f, -152f) })   // Turgut Reis Demokrasi Cd.
            };

            int maxStudents = lowTier ? 5 : studentConfigs.Length;
            for (int s = 0; s < maxStudents; s++)
            {
                SpawnCitizen(
                    root, template, idSeq++,
                    studentConfigs[s].name, "Öğrenci", "Student",
                    CitizenArchetype.Student,
                    studentConfigs[s].route, UnityEngine.Random.Range(1.82f, 2.12f),
                    StudentLinesTr, StudentLinesEn,
                    accessoryVariant: s);
            }

            // -----------------------------------------------------------------
            // 5. MUNICIPAL STREET SWEEPERS (4 actors with hi-vis vest + 3D broom!)
            // -----------------------------------------------------------------
            (string name, Vector3[] route)[] sweeperConfigs =
            {
                ("Salih Usta",  new[] { new Vector3(-34f, 0.06f, 141.5f),  new Vector3(34f, 0.06f, 141.5f) }),   // Fatih Bulvarı
                ("Metin Abi",   new[] { new Vector3(164f, 0.08f, 195f),    new Vector3(196f, 0.08f, 195f) }),    // Abdurrahmangazi Kent Meydanı
                ("İsmail Abi",  new[] { new Vector3(-28f, 0.08f, 272f),    new Vector3(22f, 0.08f, 272f) }),     // Mehmet Akif Erdoğan Cd.
                ("Recep Usta",  new[] { new Vector3(-206f, 0.08f, -184f),  new Vector3(-166f, 0.08f, -184f) })   // Battalgazi Semt Pazarı
            };

            int maxSweepers = lowTier ? 2 : sweeperConfigs.Length;
            for (int w = 0; w < maxSweepers; w++)
            {
                SpawnCitizen(
                    root, template, idSeq++,
                    sweeperConfigs[w].name, "Belediye Temizlik Görevlisi", "Municipal Street Sweeper",
                    CitizenArchetype.Sweeper,
                    sweeperConfigs[w].route, 1.32f,
                    SweeperLinesTr, SweeperLinesEn,
                    accessoryVariant: w);
            }

            // -----------------------------------------------------------------
            // 6. EXPRESS COURIERS (4 actors with thermal delivery backpack!)
            // -----------------------------------------------------------------
            (string name, Vector3[] route)[] courierConfigs =
            {
                ("Kurye Onur",  new[] { new Vector3(-24f, 0.06f, 4.9f),    new Vector3(44f, 0.06f, 4.9f) }),     // Selman Sokağı
                ("Kurye Furkan",new[] { new Vector3(-44f, 0.08f, 279f),    new Vector3(32f, 0.08f, 279f) }),     // Mehmet Akif
                ("Kurye Enes",  new[] { new Vector3(154f, 0.08f, 174f),    new Vector3(212f, 0.08f, 174f) }),    // Abdurrahmangazi
                ("Kurye Gökhan",new[] { new Vector3(-212f, 0.08f, -170f),  new Vector3(-156f, 0.08f, -170f) })   // Battalgazi
            };

            int maxCouriers = lowTier ? 2 : courierConfigs.Length;
            for (int c = 0; c < maxCouriers; c++)
            {
                SpawnCitizen(
                    root, template, idSeq++,
                    courierConfigs[c].name, "Paket Kuryesi", "Express Courier",
                    CitizenArchetype.Courier,
                    courierConfigs[c].route, 2.42f,
                    CourierLinesTr, CourierLinesEn,
                    accessoryVariant: c);
            }

            // -----------------------------------------------------------------
            // 7. ZABITA PATROL OFFICERS (3 officers with navy uniform + peaked cap + gold badge!)
            // -----------------------------------------------------------------
            (string name, Vector3[] route)[] zabitaConfigs =
            {
                ("Zabıta Erkan",  new[] { new Vector3(168f, 0.08f, 204f),  new Vector3(196f, 0.08f, 204f), new Vector3(196f, 0.08f, 184f), new Vector3(168f, 0.08f, 184f) }), // Belediye Meydanı
                ("Zabıta Volkan", new[] { new Vector3(-206f, 0.08f, -178f),new Vector3(-164f, 0.08f, -178f) }), // Battalgazi Semt Pazarı
                ("Zabıta Cengiz", new[] { new Vector3(-22f, 0.06f, 133f),  new Vector3(26f, 0.06f, 133f) })     // Fatih Saat Kulesi
            };
            for (int z = 0; z < zabitaConfigs.Length; z++)
            {
                SpawnCitizen(
                    root, template, idSeq++,
                    zabitaConfigs[z].name, "Belediye Zabıtası", "Municipal Officer (Zabıta)",
                    CitizenArchetype.Zabita,
                    zabitaConfigs[z].route, 1.62f,
                    ZabitaLinesTr, ZabitaLinesEn,
                    accessoryVariant: z);
            }

            // -----------------------------------------------------------------
            // 8. CONSTRUCTION & UTILITY WORKERS (3 actors with yellow hard hat!)
            // -----------------------------------------------------------------
            (string name, Vector3[] route)[] workerConfigs =
            {
                ("Usta Ramazan", new[] { new Vector3(-214f, 0.08f, 218f), new Vector3(-162f, 0.08f, 218f) }),   // Hasanpaşa Residences
                ("Usta Orhan",   new[] { new Vector3(208f, 0.08f, -158f), new Vector3(252f, 0.08f, -158f) }),   // Turgut Reis
                ("Usta Nihat",   new[] { new Vector3(-86f, 0.08f, -256f), new Vector3(-36f, 0.08f, -256f) })    // Mimar Sinan
            };
            int maxWorkers = lowTier ? 2 : workerConfigs.Length;
            for (int k = 0; k < maxWorkers; k++)
            {
                SpawnCitizen(
                    root, template, idSeq++,
                    workerConfigs[k].name, "Teknik Usta", "Site Technician",
                    CitizenArchetype.Worker,
                    workerConfigs[k].route, 1.50f,
                    WorkerLinesTr, WorkerLinesEn,
                    accessoryVariant: k);
            }
        }

        // =====================================================================
        // SPAWN & 3D VISUAL ACCESSORY STYLING PER ARCHETYPE
        // =====================================================================
        private DiverseCitizen SpawnCitizen(
            Transform parent,
            ProceduralHumanoid template,
            int serial,
            string displayName,
            string roleTr,
            string roleEn,
            CitizenArchetype arch,
            Vector3[] route,
            float speed,
            string[] linesTr,
            string[] linesEn,
            int accessoryVariant)
        {
            bool isChild = arch == CitizenArchetype.ChildPlay || arch == CitizenArchetype.ChildWithParent;
            CharacterArchetype meshArch = arch switch
            {
                CitizenArchetype.ChildPlay or CitizenArchetype.ChildWithParent => CharacterArchetype.Child,
                CitizenArchetype.ElderWoman => CharacterArchetype.ElderFemale,
                CitizenArchetype.ElderMan => CharacterArchetype.ElderMale,
                CitizenArchetype.Mother => CharacterArchetype.CasualFemale,
                CitizenArchetype.Student => (serial % 2 == 0) ? CharacterArchetype.CasualFemale : CharacterArchetype.SportyYouth,
                CitizenArchetype.Sweeper or CitizenArchetype.Courier or CitizenArchetype.Zabita or CitizenArchetype.Worker => CharacterArchetype.Worker,
                _ => (serial % 4 == 3) ? CharacterArchetype.Shopkeeper : CharacterArchetype.CasualMale
            };

            Color shirtCol = ExtractMatColor(arch switch
            {
                CitizenArchetype.ChildPlay or CitizenArchetype.ChildWithParent => _kidShirtMats[serial % _kidShirtMats.Length],
                CitizenArchetype.Sweeper => _hiVisYellowMat,
                CitizenArchetype.Courier => (serial % 2 == 0) ? _hiVisOrangeMat : _adultShirtMats[6],
                CitizenArchetype.Zabita => _zabitaNavyMat,
                CitizenArchetype.Worker => _hiVisOrangeMat,
                _ => _adultShirtMats[serial % _adultShirtMats.Length]
            });
            Color pantsCol = ExtractMatColor(arch == CitizenArchetype.Zabita ? _zabitaNavyMat : _pantsMats[(serial * 3 + 1) % _pantsMats.Length]);
            Color hairCol = (arch == CitizenArchetype.ElderMan || arch == CitizenArchetype.ElderWoman)
                ? RealisticCharacterFactory.SilverElderHair
                : RealisticCharacterFactory.DiverseHairPalette[(serial * 5) % RealisticCharacterFactory.DiverseHairPalette.Length];
            Color skinCol = RealisticCharacterFactory.DiverseSkinPalette[serial % RealisticCharacterFactory.DiverseSkinPalette.Length];
            Color? packCol = isChild ? ExtractMatColor(_backpackMats[accessoryVariant % _backpackMats.Length]) : (Color?)null;

            ProceduralHumanoid hum = RealisticCharacterFactory.BuildCharacter(
                parent, meshArch, template, shirtCol, pantsCol, hairCol, skinCol,
                height: 1.78f,
                goldAccents: arch == CitizenArchetype.Zabita,
                backpackOrBeltColor: packCol);
            if (hum == null) hum = Instantiate(template, parent);
            hum.name = $"DiverseCitizen_{serial:00}_{arch}_{displayName}";

            Vector3 startPos = route[0];
            if (route.Length > 1)
            {
                float u = (serial * 0.31f) % 1f;
                startPos = Vector3.Lerp(route[0], route[1], u);
            }
            hum.transform.position = startPos;

            float yaw = 0f;
            if (route.Length > 1)
            {
                Vector3 d = route[1] - route[0];
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f) yaw = Quaternion.LookRotation(d.normalized, Vector3.up).eulerAngles.y;
            }
            hum.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            float heightScale = arch switch
            {
                CitizenArchetype.ChildPlay => UnityEngine.Random.Range(0.66f, 0.74f),
                CitizenArchetype.ChildWithParent => UnityEngine.Random.Range(0.63f, 0.70f),
                CitizenArchetype.Student => UnityEngine.Random.Range(0.86f, 0.94f),
                CitizenArchetype.ElderWoman => UnityEngine.Random.Range(0.88f, 0.94f),
                CitizenArchetype.ElderMan => UnityEngine.Random.Range(0.92f, 0.98f),
                CitizenArchetype.Mother => UnityEngine.Random.Range(0.91f, 0.97f),
                CitizenArchetype.Zabita => UnityEngine.Random.Range(1.01f, 1.06f),
                _ => UnityEngine.Random.Range(0.96f, 1.05f)
            };
            hum.transform.localScale = Vector3.one * heightScale;

            // Replace any stray colliders with a single accurate CapsuleCollider + kinematic Rigidbody + LivingNpcColliderBridge
            Collider[] oldCols = hum.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < oldCols.Length; c++) Destroy(oldCols[c]);

            CapsuleCollider cap = hum.gameObject.AddComponent<CapsuleCollider>();
            cap.radius = isChild ? 0.28f : 0.30f;
            cap.height = 1.76f; // scaled automatically by root localScale!
            cap.center = new Vector3(0f, 0.88f, 0f);
            Rigidbody rb = WorldPhysicsSolidifier.EnsureKinematicRigidbody(hum.gameObject);

            int listIdx = _citizens.Count;
            hum.gameObject.AddComponent<LivingNpcColliderBridge>().ConfigureDiverse(listIdx);

            // Attach distinctive role accessories (scarf, cap, broom, thermal box, hard hat; Child already has sculpted Blender backpack!)
            AttachArchetypeAccessories(hum, arch, accessoryVariant);

            hum.SetLocomotion(0f, true, 0f);

            string trigId = $"diverse_npc_{serial:00}";
            InteractableTrigger trig = hum.gameObject.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.TownCitizen, trigId, "ui.btn.chat", isChild ? 2.5f : 2.9f);
            trig.SetCustomPrompt($"Konuş: {displayName}", $"Talk: {displayName}");

            var citizen = new DiverseCitizen
            {
                InteractableId = trigId,
                Root = hum.gameObject,
                Rb = rb,
                Humanoid = hum,
                Archetype = arch,
                DisplayName = displayName,
                RoleTr = roleTr,
                RoleEn = roleEn,
                IsChild = isChild,
                IsAuthority = arch == CitizenArchetype.Zabita,
                IsFighter = arch == CitizenArchetype.Zabita || arch == CitizenArchetype.Worker || arch == CitizenArchetype.Father,
                HeightScale = heightScale,
                BubbleHeight = isChild ? 1.52f : 2.08f,
                Route = route,
                WaypointIdx = route.Length > 1 ? 1 : 0,
                Speed = speed,
                PlayActionTimer = UnityEngine.Random.Range(3.5f, 9.0f),
                LookRetarget = UnityEngine.Random.Range(2f, 5f),
                LinesTr = linesTr,
                LinesEn = linesEn,
                LastProgressPos = startPos
            };

            _citizens.Add(citizen);
            return citizen;
        }

        private void ApplyArchetypeMaterials(ProceduralHumanoid hum, CitizenArchetype arch, int seed)
        {
            Material shirtMat = arch switch
            {
                CitizenArchetype.ChildPlay or CitizenArchetype.ChildWithParent => _kidShirtMats[seed % _kidShirtMats.Length],
                CitizenArchetype.Sweeper => _hiVisYellowMat,
                CitizenArchetype.Courier => (seed % 2 == 0) ? _hiVisOrangeMat : _adultShirtMats[6],
                CitizenArchetype.Zabita => _zabitaNavyMat,
                CitizenArchetype.Worker => _hiVisOrangeMat,
                _ => _adultShirtMats[seed % _adultShirtMats.Length]
            };

            Material pantsMat = arch == CitizenArchetype.Zabita
                ? _zabitaNavyMat
                : _pantsMats[(seed * 3 + 1) % _pantsMats.Length];

            Material hairMat = (arch == CitizenArchetype.ElderMan || arch == CitizenArchetype.ElderWoman)
                ? _silverHairMat
                : _hairMats[(seed * 5) % _hairMats.Length];

            Material skinMat = _skinMats[seed % _skinMats.Length];

            Renderer[] rends = hum.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null) continue;
                string rn = r.name.ToLowerInvariant();
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    string mn = mats[m] != null ? mats[m].name : string.Empty;
                    if (mn.Contains("MAT_Char_Shirt") || rn.Contains("chest") || rn.Contains("torso") || rn.Contains("shirt") || rn.Contains("upperarm"))
                    {
                        mats[m] = shirtMat;
                        changed = true;
                    }
                    else if (mn.Contains("MAT_Char_Pants") || rn.Contains("pelvis") || rn.Contains("thigh") || rn.Contains("shin"))
                    {
                        mats[m] = pantsMat;
                        changed = true;
                    }
                    else if (mn.Contains("MAT_Char_Hair") || rn.Contains("hair"))
                    {
                        mats[m] = hairMat;
                        changed = true;
                    }
                    else if (mn.Contains("MAT_Char_Skin"))
                    {
                        mats[m] = skinMat;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        private static Color ExtractMatColor(Material m)
        {
            if (m == null) return new Color(0.28f, 0.42f, 0.58f);
            if (m.HasProperty(BaseColorId)) return m.GetColor(BaseColorId);
            if (m.HasProperty(ColorId)) return m.GetColor(ColorId);
            return new Color(0.28f, 0.42f, 0.58f);
        }

        private void AttachArchetypeAccessories(ProceduralHumanoid hum, CitizenArchetype arch, int variant)
        {
            Transform headTf = hum.HeadPivot ?? FindChildRecursive(hum.transform, "Head");
            Transform spineTf = FindChildRecursive(hum.transform, "Spine");
            Transform rElbowTf = FindChildRecursive(hum.transform, "R_Elbow");

            switch (arch)
            {
                case CitizenArchetype.ChildPlay:
                case CitizenArchetype.ChildWithParent:
                    // Sculpted Blender Character_Child already has a 3D hoodie & backpack baked into Part_Chest!
                    if (headTf != null && (variant % 3 == 1))
                    {
                        BuildBaseballCap(headTf, _backpackMats[(variant + 2) % _backpackMats.Length], _goldBadgeMat);
                    }
                    break;

                case CitizenArchetype.Mother:
                case CitizenArchetype.ElderWoman:
                    if (headTf != null)
                    {
                        BuildHeadscarf(headTf, _scarfMats[variant % _scarfMats.Length]);
                    }
                    break;

                case CitizenArchetype.ElderMan:
                    if (headTf != null)
                    {
                        BuildFlatCap(headTf, _tweedCapMat);
                        // Classic distinguished white mustache
                        CreateAccPart("Mustache", PrimitiveType.Cube, headTf,
                            new Vector3(0f, 0.056f, 0.118f), new Vector3(0.088f, 0.022f, 0.024f), _silverHairMat);
                    }
                    break;

                case CitizenArchetype.Student:
                    if (spineTf != null)
                    {
                        BuildSchoolBackpack(spineTf, _backpackMats[(variant + 1) % _backpackMats.Length], _darkVisorMat);
                    }
                    if (headTf != null && variant % 3 == 0)
                    {
                        BuildBaseballCap(headTf, _zabitaNavyMat, _reflectiveMat);
                    }
                    break;

                case CitizenArchetype.Sweeper:
                    if (spineTf != null)
                    {
                        BuildReflectiveVestStripes(spineTf, _reflectiveMat);
                    }
                    if (headTf != null)
                    {
                        BuildBaseballCap(headTf, _zabitaNavyMat, _hiVisYellowMat);
                    }
                    if (rElbowTf != null)
                    {
                        BuildStreetBroom(rElbowTf, _woodMat, _strawMat);
                    }
                    break;

                case CitizenArchetype.Courier:
                    if (spineTf != null)
                    {
                        BuildCourierThermalBox(spineTf, variant % 2 == 0 ? _hiVisOrangeMat : _adultShirtMats[6], _reflectiveMat);
                    }
                    if (headTf != null)
                    {
                        BuildBaseballCap(headTf, _darkVisorMat, _hiVisOrangeMat);
                    }
                    break;

                case CitizenArchetype.Zabita:
                    if (headTf != null)
                    {
                        BuildZabitaPeakedCap(headTf, _zabitaNavyMat, _darkVisorMat, _goldBadgeMat);
                    }
                    if (spineTf != null)
                    {
                        // Gold municipal badge on left chest
                        CreateAccPart("ZabitaBadge", PrimitiveType.Cube, spineTf,
                            new Vector3(-0.085f, 0.32f, 0.125f), new Vector3(0.048f, 0.056f, 0.016f), _goldBadgeMat);
                    }
                    break;

                case CitizenArchetype.Worker:
                    if (headTf != null)
                    {
                        BuildHardHat(headTf, _hardHatYellowMat);
                    }
                    if (spineTf != null)
                    {
                        BuildReflectiveVestStripes(spineTf, _reflectiveMat);
                    }
                    break;
            }
        }

        private static void BuildSchoolBackpack(Transform spineTf, Material bodyMat, Material trimMat)
        {
            GameObject bag = new GameObject("Acc_SchoolBackpack");
            bag.transform.SetParent(spineTf, false);
            bag.transform.localPosition = new Vector3(0f, 0.24f, -0.17f);
            CreateAccPart("Body", PrimitiveType.Cube, bag.transform, Vector3.zero, new Vector3(0.27f, 0.34f, 0.14f), bodyMat);
            CreateAccPart("Pocket", PrimitiveType.Cube, bag.transform, new Vector3(0f, -0.06f, -0.078f), new Vector3(0.21f, 0.15f, 0.04f), trimMat);
            CreateAccPart("TopHandle", PrimitiveType.Cube, bag.transform, new Vector3(0f, 0.185f, 0.01f), new Vector3(0.09f, 0.03f, 0.03f), trimMat);
        }

        private static void BuildCourierThermalBox(Transform spineTf, Material boxMat, Material stripeMat)
        {
            GameObject box = new GameObject("Acc_CourierBox");
            box.transform.SetParent(spineTf, false);
            box.transform.localPosition = new Vector3(0f, 0.28f, -0.27f);
            CreateAccPart("ThermalCube", PrimitiveType.Cube, box.transform, Vector3.zero, new Vector3(0.38f, 0.40f, 0.32f), boxMat);
            CreateAccPart("ReflectStripe", PrimitiveType.Cube, box.transform, new Vector3(0f, 0.08f, -0.164f), new Vector3(0.34f, 0.06f, 0.014f), stripeMat);
            CreateAccPart("TopLid", PrimitiveType.Cube, box.transform, new Vector3(0f, 0.21f, 0f), new Vector3(0.40f, 0.03f, 0.34f), stripeMat);
        }

        private static void BuildReflectiveVestStripes(Transform spineTf, Material reflectiveMat)
        {
            CreateAccPart("HiVisBandFront", PrimitiveType.Cube, spineTf, new Vector3(0f, 0.18f, 0.122f), new Vector3(0.32f, 0.042f, 0.016f), reflectiveMat);
            CreateAccPart("HiVisBandBack", PrimitiveType.Cube, spineTf, new Vector3(0f, 0.18f, -0.122f), new Vector3(0.32f, 0.042f, 0.016f), reflectiveMat);
            CreateAccPart("HiVisBandUpper", PrimitiveType.Cube, spineTf, new Vector3(0f, 0.29f, 0.126f), new Vector3(0.35f, 0.036f, 0.016f), reflectiveMat);
        }

        private static void BuildBaseballCap(Transform headTf, Material capMat, Material accentMat)
        {
            GameObject cap = new GameObject("Acc_BaseballCap");
            cap.transform.SetParent(headTf, false);
            cap.transform.localPosition = new Vector3(0f, 0.175f, 0.008f);
            cap.transform.localRotation = Quaternion.Euler(-5f, 0f, 0f);
            CreateAccPart("Crown", PrimitiveType.Sphere, cap.transform, new Vector3(0f, 0.010f, -0.012f), new Vector3(0.246f, 0.142f, 0.250f), capMat);
            CreateAccPart("Visor", PrimitiveType.Cube, cap.transform, new Vector3(0f, -0.014f, 0.138f), new Vector3(0.188f, 0.014f, 0.112f), capMat, Quaternion.Euler(10f, 0f, 0f));
            CreateAccPart("Button", PrimitiveType.Sphere, cap.transform, new Vector3(0f, 0.080f, -0.012f), new Vector3(0.026f, 0.014f, 0.026f), accentMat);
        }

        private static void BuildHeadscarf(Transform headTf, Material scarfMat)
        {
            GameObject scarf = new GameObject("Acc_Headscarf");
            scarf.transform.SetParent(headTf, false);
            scarf.transform.localPosition = Vector3.zero;
            // Smooth silk dome covering crown & hair neatly while keeping face fully clear
            CreateAccPart("CrownWrap", PrimitiveType.Sphere, scarf.transform, new Vector3(0f, 0.135f, -0.022f), new Vector3(0.262f, 0.228f, 0.266f), scarfMat);
            CreateAccPart("SideL", PrimitiveType.Cube, scarf.transform, new Vector3(-0.116f, 0.052f, -0.018f), new Vector3(0.042f, 0.155f, 0.165f), scarfMat);
            CreateAccPart("SideR", PrimitiveType.Cube, scarf.transform, new Vector3( 0.116f, 0.052f, -0.018f), new Vector3(0.042f, 0.155f, 0.165f), scarfMat);
            CreateAccPart("NeckShawl", PrimitiveType.Cylinder, scarf.transform, new Vector3(0f, -0.032f, -0.016f), new Vector3(0.225f, 0.052f, 0.215f), scarfMat);
        }

        private static void BuildFlatCap(Transform headTf, Material tweedMat)
        {
            GameObject cap = new GameObject("Acc_FlatCap");
            cap.transform.SetParent(headTf, false);
            cap.transform.localPosition = new Vector3(0f, 0.182f, 0.014f);
            CreateAccPart("Top", PrimitiveType.Sphere, cap.transform, new Vector3(0f, 0.004f, -0.005f), new Vector3(0.258f, 0.095f, 0.278f), tweedMat);
            CreateAccPart("Brim", PrimitiveType.Cube, cap.transform, new Vector3(0f, -0.020f, 0.134f), new Vector3(0.185f, 0.016f, 0.088f), tweedMat, Quaternion.Euler(8f, 0f, 0f));
        }

        private static void BuildHardHat(Transform headTf, Material yellowMat)
        {
            GameObject hat = new GameObject("Acc_HardHat");
            hat.transform.SetParent(headTf, false);
            hat.transform.localPosition = new Vector3(0f, 0.185f, 0.004f);
            CreateAccPart("Dome", PrimitiveType.Sphere, hat.transform, new Vector3(0f, 0.016f, -0.006f), new Vector3(0.268f, 0.165f, 0.276f), yellowMat);
            CreateAccPart("Brim", PrimitiveType.Cylinder, hat.transform, new Vector3(0f, -0.022f, 0.012f), new Vector3(0.295f, 0.012f, 0.315f), yellowMat);
            CreateAccPart("Ridge", PrimitiveType.Cube, hat.transform, new Vector3(0f, 0.085f, -0.004f), new Vector3(0.052f, 0.032f, 0.22f), yellowMat);
        }

        private static void BuildZabitaPeakedCap(Transform headTf, Material navyMat, Material visorMat, Material goldMat)
        {
            GameObject cap = new GameObject("Acc_ZabitaCap");
            cap.transform.SetParent(headTf, false);
            cap.transform.localPosition = new Vector3(0f, 0.184f, 0.010f);
            CreateAccPart("Band", PrimitiveType.Cylinder, cap.transform, new Vector3(0f, 0.006f, -0.008f), new Vector3(0.244f, 0.044f, 0.248f), navyMat);
            CreateAccPart("Crown", PrimitiveType.Cylinder, cap.transform, new Vector3(0f, 0.056f, -0.004f), new Vector3(0.272f, 0.032f, 0.276f), navyMat);
            CreateAccPart("Visor", PrimitiveType.Cube, cap.transform, new Vector3(0f, -0.022f, 0.138f), new Vector3(0.195f, 0.014f, 0.105f), visorMat, Quaternion.Euler(12f, 0f, 0f));
            CreateAccPart("GoldEmblem", PrimitiveType.Cube, cap.transform, new Vector3(0f, 0.036f, 0.126f), new Vector3(0.044f, 0.044f, 0.016f), goldMat);
        }

        private static void BuildStreetBroom(Transform rElbowTf, Material woodMat, Material strawMat)
        {
            GameObject broom = new GameObject("Acc_StreetBroom");
            broom.transform.SetParent(rElbowTf, false);
            broom.transform.localPosition = new Vector3(0.035f, -0.25f, 0.055f);
            broom.transform.localRotation = Quaternion.Euler(24f, 0f, -6f);
            CreateAccPart("Handle", PrimitiveType.Cylinder, broom.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.032f, 0.68f, 0.032f), woodMat);
            CreateAccPart("BristleHead", PrimitiveType.Cube, broom.transform, new Vector3(0f, -0.62f, 0f), new Vector3(0.30f, 0.12f, 0.08f), strawMat);
        }

        private static void CreateAccPart(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c.name == targetName) return c;
                Transform found = FindChildRecursive(c, targetName);
                if (found != null) return found;
            }
            return null;
        }

        // =====================================================================
        // UPDATE LOOP — 3-TIER DISTANCE LOD + REALISTIC ARCHETYPE AI
        // =====================================================================
        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _citizens.Count == 0) return;
            if (_globalBubbleCooldown > 0f) _globalBubbleCooldown -= dt;

            PlayerModeController mode = PlayerModeController.Instance;
            Vector3 focusPos = mode != null ? mode.ActiveWorldPosition : Vector3.zero;
            bool isRaining = NeighborhoodWeatherSystem.Instance != null &&
                             NeighborhoodWeatherSystem.Instance.CurrentWeather == NeighborhoodWeatherState.SummerShower;
            bool en = LocalizationService.IsEnglish;

            _lodFrameBucket = (_lodFrameBucket + 1) & 3;
            const float cullDistSq = 98f * 98f;
            const float nearDistSq = 44f * 44f;

            for (int i = 0; i < _citizens.Count; i++)
            {
                DiverseCitizen c = _citizens[i];
                if (c == null || c.Root == null) continue;

                Vector3 pos = c.Root.transform.position;
                float dx = pos.x - focusPos.x;
                float dz = pos.z - focusPos.z;
                float distSq = dx * dx + dz * dz;

                // Tier 3: Far distance cull (> 98m) — zero CPU/GPU cost!
                if (distSq > cullDistSq)
                {
                    if (c.Root.activeSelf) c.Root.SetActive(false);
                    continue;
                }
                if (!c.Root.activeSelf) c.Root.SetActive(true);

                // Tier 2: Mid-range throttle (44..98m) — update 1/4th per frame with 4x dt
                float stepDt = dt;
                if (distSq > nearDistSq)
                {
                    if ((i & 3) != _lodFrameBucket) continue;
                    stepDt = Mathf.Min(dt * 4f, 0.14f);
                }

                UpdateSingleCitizen(c, stepDt, focusPos, distSq, isRaining, en);
            }
        }

        private void UpdateSingleCitizen(DiverseCitizen c, float dt, Vector3 focusPos, float distSq, bool isRaining, bool en)
        {
            Transform tr = c.Root.transform;
            if (c.HitCooldown > 0f) c.HitCooldown -= dt;
            if (c.BumpCooldown > 0f) c.BumpCooldown -= dt;
            if (c.VehicleHitCooldown > 0f) c.VehicleHitCooldown -= dt;
            if (c.CarDodgeCooldown > 0f) c.CarDodgeCooldown -= dt;
            if (c.EventReactCooldown > 0f) c.EventReactCooldown -= dt;
            if (c.GreetingCooldown > 0f) c.GreetingCooldown -= dt;
            if (c.TalkTimer > 0f) c.TalkTimer -= dt;
            if (c.PunchCooldown > 0f) c.PunchCooldown -= dt;

            // 1. Airborne / knockdown recovery (adults only — children never get ragdolled!)
            if (c.IsAirborne)
            {
                c.AirVel.y -= 18.5f * dt;
                Vector3 next = tr.position + c.AirVel * dt;
                float groundY = SampleGroundY(next);
                if (next.y <= groundY)
                {
                    next.y = groundY;
                    c.IsAirborne = false;
                    c.AirVel = Vector3.zero;
                }
                tr.position = next;
                c.Humanoid?.SetLocomotion(0f, !c.IsAirborne, c.AirVel.y);
                return;
            }

            if (c.KnockdownTimer > 0f)
            {
                c.KnockdownTimer -= dt;
                if (c.StumbleVel.sqrMagnitude > 0.02f)
                {
                    Vector3 dummy = tr.forward;
                    NpcSmartNavigation.MoveActorSafely(tr, c.Rb, c.StumbleVel * dt, ref dummy, false, 0.28f);
                    c.StumbleVel = Vector3.MoveTowards(c.StumbleVel, Vector3.zero, 12f * dt);
                }
                c.Humanoid?.SetLocomotion(0f, true, 0f);
                if (c.KnockdownTimer <= 0f && c.IsKnockedOut)
                {
                    c.IsKnockedOut = false;
                    c.Health = c.MaxHealth > 0f ? c.MaxHealth : 100f;
                    c.FightTimer = 0f;
                    c.PendingPunchTimer = 0f;
                    NpcSpeechBubbleManager.Say(tr, c.DisplayName,
                        en ? "😵 Ugh... my head is spinning..." : "😵 Ah başım... yeni ayıldım...", 2.8f, NpcBubbleMood.Alert, c.BubbleHeight);
                }
                return;
            }

            if (c.StumbleVel.sqrMagnitude > 0.04f)
            {
                Vector3 dummy = tr.forward;
                NpcSmartNavigation.MoveActorSafely(tr, c.Rb, c.StumbleVel * dt, ref dummy, false, 0.28f);
                c.StumbleVel = Vector3.MoveTowards(c.StumbleVel, Vector3.zero, 11f * dt);
            }

            // 2. Approaching car/bike/traffic proactive sidestep (extra alert for children & families!)
            if (distSq < 40f * 40f && c.CarDodgeCooldown <= 0f && c.SidestepTimer <= 0f)
            {
                ApproachingVehicleInfo threat = NpcSmartNavigation.DetectApproachingVehicle(tr.position, tr.forward);
                if (threat.Detected)
                {
                    float side = threat.LateralOffset >= 0f ? 1f : -1f;
                    c.SidestepDir = threat.VehicleRight * side;
                    c.SidestepDuration = threat.Urgent ? 0.92f : 0.72f;
                    c.SidestepTimer = c.SidestepDuration;
                    c.SidestepPeakSpeed = threat.Urgent ? 3.4f : 2.5f;
                    c.CarDodgeCooldown = 1.35f;
                    c.Humanoid?.PlayStartled(c.SidestepDuration, threat.VehiclePos);
                    if (_globalBubbleCooldown <= 0f && distSq < 18f * 18f)
                    {
                        _globalBubbleCooldown = 2.8f;
                        string warn = c.IsChild
                            ? (en ? "Whoa! Car coming!" : "Aa! Araba geliyor, kaç!")
                            : (c.Archetype == CitizenArchetype.Mother || c.Archetype == CitizenArchetype.Father
                                ? (en ? "Hold my hand, watch the car!" : "Elimi tut yavrum, araba geçiyor!")
                                : (en ? "Watch out!" : "Dikkat et komşu!"));
                        NpcSpeechBubbleManager.Say(tr, c.DisplayName, warn, 2.2f, NpcBubbleMood.Alert, c.BubbleHeight);
                    }
                }
            }

            if (c.SidestepTimer > 0f)
            {
                c.SidestepTimer -= dt;
                float frac = Mathf.Clamp01(c.SidestepTimer / Mathf.Max(0.1f, c.SidestepDuration));
                float spd = c.SidestepPeakSpeed * Mathf.Sin(frac * Mathf.PI);
                Vector3 stepDir = c.SidestepDir;
                NpcSmartNavigation.MoveActorSafely(tr, c.Rb, stepDir * (spd * dt), ref stepDir, false, c.IsChild ? 0.24f : 0.30f);
                if (stepDir.sqrMagnitude > 0.01f)
                {
                    tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(stepDir, Vector3.up), 1f - Mathf.Exp(-10f * dt));
                }
                c.Humanoid?.SetLocomotion(spd, true, 0f, stepDir);
                return;
            }

            // 3. Flee behavior (after disturbance or child safety dash)
            if (c.FleeTimer > 0f)
            {
                c.FleeTimer -= dt;
                float fleeSpeed = c.IsChild ? 3.55f : 3.95f;
                Vector3 dir = NpcSmartNavigation.ComputeSmartSteering(tr, c.FleeDir, 2.0f, c.IsChild ? 0.24f : 0.30f, avoidPlayer: false);
                NpcSmartNavigation.MoveActorSafely(tr, c.Rb, dir * (fleeSpeed * dt), ref c.FleeDir, true, c.IsChild ? 0.24f : 0.30f);
                tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-11f * dt));
                c.Humanoid?.SetLocomotion(fleeSpeed, true, 0f, dir);
                return;
            }

            // 4. Zabıta / Protective Adult Retaliation Combat (never children or elders!)
            if (c.FightTimer > 0f && !c.IsChild && c.Archetype != CitizenArchetype.ElderMan && c.Archetype != CitizenArchetype.ElderWoman)
            {
                UpdateCitizenCombat(c, dt, focusPos, en);
                return;
            }

            // 5. Pause / chatting / sweeping / kid cheer moments
            if (c.PauseTimer > 0f)
            {
                c.PauseTimer -= dt;
                c.Humanoid?.SetLocomotion(0f, true, 0f);
                if (distSq < 9f * 9f)
                {
                    c.Humanoid?.SetLookAtPoint(true, focusPos + Vector3.up * 1.45f);
                }
                return;
            }

            // 6. Determine current movement goal based on Archetype!
            Vector3 goalPos = ResolveArchetypeGoal(c, dt, focusPos, isRaining, en, out float targetSpeed);
            Vector3 toGoal = goalPos - tr.position;
            toGoal.y = 0f;
            float remainDist = toGoal.magnitude;

            float arrivalDist = c.Archetype == CitizenArchetype.ChildWithParent ? 0.55f : 1.05f;
            if (remainDist <= arrivalDist)
            {
                HandleGoalReached(c, dt, focusPos, distSq, isRaining, en);
                return;
            }

            Vector3 desiredDir = toGoal / remainDist;
            Vector3 steerDir = NpcSmartNavigation.ComputeSmartSteering(
                tr, desiredDir, probeDistance: 2.1f, bodyRadius: c.IsChild ? 0.23f : 0.30f, avoidPlayer: true, avoidNeighbors: true);

            // Speed adjustment for rain hurrying or parent catching-up
            if (isRaining && !c.RainShieldActive && !c.IsChild)
            {
                targetSpeed *= 1.24f;
            }

            Vector3 persistent = desiredDir;
            NpcSmartNavigation.MoveActorSafely(
                tr, c.Rb, steerDir * (targetSpeed * dt), ref persistent, false,
                bodyRadius: c.IsChild ? 0.23f : 0.30f,
                lowHeight: c.IsChild ? 0.32f : 0.44f,
                highHeight: c.IsChild ? 0.78f : 1.05f);

            // Ground height alignment (critical for elevated hills like Turgut Reis / Aydos Castle!)
            float gY = SampleGroundY(tr.position);
            Vector3 pNow = tr.position;
            pNow.y = Mathf.Lerp(pNow.y, gY, 1f - Mathf.Exp(-14f * dt));
            tr.position = pNow;

            tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(steerDir, Vector3.up), 1f - Mathf.Exp(-8.5f * dt));
            c.Humanoid?.SetLocomotion(targetSpeed, true, 0f, steerDir);

            // Stuck recovery
            c.StuckTimer += dt;
            if (c.StuckTimer >= 2.2f)
            {
                c.StuckTimer = 0f;
                if ((tr.position - c.LastProgressPos).sqrMagnitude < 0.25f && c.Route != null && c.Route.Length > 1)
                {
                    c.WaypointIdx = (c.WaypointIdx + 1) % c.Route.Length;
                }
                c.LastProgressPos = tr.position;
            }

            // Friendly close-range greeting & playful kid antics!
            UpdateProximityRealism(c, dt, focusPos, distSq, en);
        }

        private Vector3 ResolveArchetypeGoal(DiverseCitizen c, float dt, Vector3 focusPos, bool isRaining, bool en, out float targetSpeed)
        {
            targetSpeed = c.Speed;

            // Rain shelter seeking for non-rain-working citizens
            if (isRaining && c.Archetype != CitizenArchetype.Zabita && c.Archetype != CitizenArchetype.Courier &&
                NpcSmartNavigation.TryFindNearestRainShelter(c.Root.transform.position, 28f, out Vector3 shelter))
            {
                c.RainShieldActive = true;
                targetSpeed = c.Speed * 1.25f;
                return shelter;
            }
            c.RainShieldActive = false;

            // Family Child walks right beside their Mother / Father!
            if (c.Archetype == CitizenArchetype.ChildWithParent && c.ParentPartner != null && c.ParentPartner.Root != null)
            {
                Transform pt = c.ParentPartner.Root.transform;
                Vector3 desired = pt.position + pt.right * (0.70f * c.PartnerSideSign) - pt.forward * 0.14f;
                float gap = Vector3.Distance(c.Root.transform.position, desired);
                targetSpeed = gap > 2.2f ? 2.85f : (gap > 1.0f ? 1.85f : 1.48f);
                return desired;
            }

            // Playful kids chase tag buddy occasionally!
            if (c.Archetype == CitizenArchetype.ChildPlay && c.TagPartner != null && c.TagPartner.Root != null)
            {
                c.PlayActionTimer -= dt;
                if (c.PlayActionTimer <= 0f)
                {
                    c.PlayActionTimer = UnityEngine.Random.Range(5.5f, 10.5f);
                    // Brief cheerful hop or wave while playing!
                    c.Humanoid?.PlaySportAnim(SportAnimKind.VictoryJump, 0.68f);
                    c.PauseTimer = 0.72f;
                }
            }

            if (c.Route == null || c.Route.Length == 0) return c.Root.transform.position;
            return c.Route[c.WaypointIdx % c.Route.Length];
        }

        private void HandleGoalReached(DiverseCitizen c, float dt, Vector3 focusPos, float distSq, bool isRaining, bool en)
        {
            if (c.RainShieldActive)
            {
                c.Humanoid?.SetLocomotion(0f, true, 0f);
                return;
            }

            if (c.Archetype == CitizenArchetype.ChildWithParent)
            {
                if (c.ParentPartner != null && c.ParentPartner.Root != null)
                {
                    c.Root.transform.rotation = Quaternion.Slerp(
                        c.Root.transform.rotation, c.ParentPartner.Root.transform.rotation, 1f - Mathf.Exp(-7f * dt));
                }
                c.Humanoid?.SetLocomotion(0f, true, 0f);
                return;
            }

            if (c.Route != null && c.Route.Length > 1)
            {
                c.WaypointIdx = (c.WaypointIdx + 1) % c.Route.Length;
            }

            // Archetype-specific natural micro-pauses
            c.PauseTimer = c.Archetype switch
            {
                CitizenArchetype.ElderMan or CitizenArchetype.ElderWoman => UnityEngine.Random.Range(2.4f, 4.8f),
                CitizenArchetype.Sweeper => UnityEngine.Random.Range(1.8f, 3.6f),
                CitizenArchetype.ChildPlay => UnityEngine.Random.Range(0.5f, 1.4f),
                CitizenArchetype.Zabita => UnityEngine.Random.Range(2.0f, 4.0f),
                CitizenArchetype.Courier => UnityEngine.Random.Range(0.4f, 1.0f),
                _ => UnityEngine.Random.Range(1.0f, 2.5f)
            };

            if (c.Archetype == CitizenArchetype.ChildPlay && distSq < 25f * 25f)
            {
                c.Humanoid?.PlayWave();
            }
            c.Humanoid?.SetLocomotion(0f, true, 0f);
        }

        private void UpdateProximityRealism(DiverseCitizen c, float dt, Vector3 focusPos, float distSq, bool en)
        {
            if (distSq < 9.5f * 9.5f)
            {
                c.Humanoid?.SetLookAtPoint(true, focusPos + Vector3.up * 1.42f);

                if (distSq < 5.5f * 5.5f && c.GreetingCooldown <= 0f && _globalBubbleCooldown <= 0f)
                {
                    c.GreetingCooldown = UnityEngine.Random.Range(16f, 28f);
                    _globalBubbleCooldown = 4.2f;

                    string line;
                    NpcBubbleMood mood = NpcBubbleMood.Friendly;
                    if (NpcSmartNavigation.TryGetPlayerContextRemark(en, out string ctxLine, out NpcBubbleMood ctxMood)
                        && !c.IsChild && UnityEngine.Random.value < 0.45f)
                    {
                        line = ctxLine;
                        mood = ctxMood;
                    }
                    else
                    {
                        string[] pool = en ? c.LinesEn : c.LinesTr;
                        line = pool[UnityEngine.Random.Range(0, pool.Length)];
                    }

                    NpcSpeechBubbleManager.Say(c.Root.transform, c.DisplayName, line, 3.2f, mood, c.BubbleHeight);
                    c.Humanoid?.SetTalking(true, 2.8f);
                    if (c.IsChild || c.Archetype == CitizenArchetype.ElderMan || c.Archetype == CitizenArchetype.ElderWoman)
                    {
                        c.Humanoid?.PlayWave();
                    }
                }
            }
            else
            {
                c.Humanoid?.SetLookAtPoint(false, Vector3.zero);
            }
        }

        private void UpdateCitizenCombat(DiverseCitizen c, float dt, Vector3 focusPos, bool en)
        {
            c.FightTimer -= dt;
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.CurrentMode != ControlMode.OnFoot ||
                (PlayerNeedsController.Instance != null && PlayerNeedsController.Instance.IsKnockedOut))
            {
                c.FightTimer = 0f;
                return;
            }

            Vector3 toPlayer = focusPos - c.Root.transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist > 24f)
            {
                c.FightTimer = 0f;
                return;
            }

            Vector3 dir = dist > 0.05f ? toPlayer / dist : c.Root.transform.forward;
            c.Root.transform.rotation = Quaternion.Slerp(c.Root.transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-12f * dt));

            if (c.PendingPunchTimer > 0f)
            {
                c.PendingPunchTimer -= dt;
                c.Humanoid?.SetLocomotion(0f, true, 0f);
                if (c.PendingPunchTimer <= 0f && dist <= 1.85f)
                {
                    PlayerNeedsController.Instance?.TakeDamage(
                        c.IsAuthority ? 14f : 9f,
                        c.Root.transform.position,
                        c.DisplayName,
                        heavyHit: c.IsAuthority);
                }
                return;
            }

            if (dist > 1.42f)
            {
                float chaseSpd = c.IsAuthority ? 4.25f : 3.75f;
                Vector3 steer = NpcSmartNavigation.ComputeSmartSteering(c.Root.transform, dir, 1.8f, 0.30f, avoidPlayer: false);
                Vector3 persistent = steer;
                NpcSmartNavigation.MoveActorSafely(c.Root.transform, c.Rb, steer * (chaseSpd * dt), ref persistent, false, 0.30f);
                c.Humanoid?.SetLocomotion(chaseSpd, true, 0f, steer);
            }
            else
            {
                c.Humanoid?.SetLocomotion(0f, true, 0f);
                if (c.PunchCooldown <= 0f)
                {
                    c.PunchCooldown = c.IsAuthority ? 1.15f : 1.45f;
                    c.PendingPunchTimer = 0.22f;
                    c.Humanoid?.PlayPunch();
                }
            }
        }

        private static float SampleGroundY(Vector3 pos)
        {
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 1.8f, pos.z), Vector3.down, out RaycastHit hit, 4.0f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.65f)
            {
                return hit.point.y + 0.02f;
            }
            return 0.06f;
        }

        // =====================================================================
        // COLLISION, INTERACTION, COMBAT & STRICT CHILD PROTECTION HOOKS
        // =====================================================================
        private void HandleCitizenInteracted(InteractableTrigger trig)
        {
            if (trig == null || string.IsNullOrEmpty(trig.InteractableId)) return;
            if (!trig.InteractableId.StartsWith("diverse_npc_", StringComparison.Ordinal)) return;

            Vector3 playerPos = PlayerModeController.Instance != null ? PlayerModeController.Instance.ActiveWorldPosition : Vector3.zero;
            bool en = LocalizationService.IsEnglish;

            for (int i = 0; i < _citizens.Count; i++)
            {
                DiverseCitizen c = _citizens[i];
                if (!string.Equals(c.InteractableId, trig.InteractableId, StringComparison.Ordinal)) continue;
                if (c.IsKnockedOut && c.KnockdownTimer > 0f)
                {
                    PlayerModeController.Instance?.RaiseStatusMessage(en
                        ? $"😵 {c.DisplayName} is knocked out right now..."
                        : $"😵 {c.DisplayName} şu an baygın yatıyor...");
                    return;
                }

                c.PauseTimer = Mathf.Max(c.PauseTimer, 5.0f);
                SmartNpcInteractionPanel.EnsureInstance().OpenForLivingNpc(
                    c.Root.transform, c.Humanoid, c.DisplayName, c.RoleTr, c.RoleEn,
                    en ? c.LinesEn : c.LinesTr, playerPos);
                return;
            }
        }

        public static void CalmAllHostileCitizens()
        {
            if (Instance == null) return;
            for (int i = 0; i < Instance._citizens.Count; i++)
            {
                DiverseCitizen c = Instance._citizens[i];
                if (c == null) continue;
                c.FightTimer = 0f;
                c.PendingPunchTimer = 0f;
                c.FleeTimer = 0f;
                c.HitCount = 0;
            }
        }

        public bool TryReceivePlayerPunch(Vector3 strikeCenter, float radius, Vector3 attackerPos, Vector3 punchDir, int comboStep)
        {
            DiverseCitizen best = null;
            float maxR = radius + 0.55f;
            float bestSq = maxR * maxR;

            for (int i = 0; i < _citizens.Count; i++)
            {
                DiverseCitizen c = _citizens[i];
                if (c == null || c.Root == null || !c.Root.activeInHierarchy || c.HitCooldown > 0f || c.KnockdownTimer > 0f) continue;

                Vector3 nPos = c.Root.transform.position;
                if (Mathf.Abs(nPos.y - attackerPos.y) > 1.85f) continue;
                Vector3 d = (nPos + Vector3.up * (c.IsChild ? 0.65f : 1.0f)) - strikeCenter;
                float dSq = d.sqrMagnitude;
                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    best = c;
                }
            }

            if (best == null) return false;
            bool en = LocalizationService.IsEnglish;

            // STRICT CHILD PROTECTION: Kids NEVER take violence/ragdoll — they bolt to safety & trigger instant Zabıta penalty!
            if (best.IsChild)
            {
                best.HitCooldown = 2.0f;
                Vector3 away = best.Root.transform.position - attackerPos;
                away.y = 0f;
                best.FleeDir = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
                best.FleeTimer = 7.5f;
                best.Humanoid?.PlayStartled(0.85f, attackerPos);
                NpcSpeechBubbleManager.Say(best.Root.transform, best.DisplayName,
                    en ? "Mom!! Help! Leave me alone!" : "Anne!! İmdat! Uzak dur benden!", 3.2f, NpcBubbleMood.Alert, best.BubbleHeight);

                NeighborhoodConsequenceSystem conseq = NeighborhoodConsequenceSystem.EnsureInstance();
                conseq.PlayZabitaWhistle(0.80f);
                conseq.ReportOffense(
                    NeighborhoodOffenseKind.AssaultStaffOrAuthority,
                    best.Root.transform.position,
                    en ? "Child Safety Violation" : "Çocuğu Korkutma Cezası",
                    repPenalty: 25,
                    immediateFineTl: 120,
                    heatDelta: 2.8f);

                // Alert nearby parent/Zabıta immediately!
                NpcWorldAwareness.NotifyViolence(best.Root.transform.position, attackerPos, null);
                if (best.ParentPartner != null && best.ParentPartner.Root != null)
                {
                    best.ParentPartner.FightTimer = 10f;
                    NpcSpeechBubbleManager.Say(best.ParentPartner.Root.transform, best.ParentPartner.DisplayName,
                        en ? "Stay away from my child!!" : "Çocuğumdan uzak dur!!", 3.2f, NpcBubbleMood.Alert, 2.08f);
                }
                return true;
            }

            Vector3 knockDir = punchDir;
            knockDir.y = 0f;
            if (knockDir.sqrMagnitude < 0.01f) knockDir = best.Root.transform.position - attackerPos;
            knockDir.y = 0f;
            knockDir = knockDir.sqrMagnitude > 0.001f ? knockDir.normalized : Vector3.forward;

            if (best.MaxHealth <= 0f)
            {
                best.MaxHealth = best.IsAuthority ? 130f : (best.IsFighter ? 120f : 100f);
                best.Health = best.MaxHealth;
            }

            best.HitCount++;
            float dmg = comboStep >= 2 ? 50f : (comboStep == 1 ? 35f : 30f);
            best.Health = Mathf.Max(0f, best.Health - dmg);
            bool knockedOut = best.Health <= 0f;
            bool knockdown = knockedOut || comboStep >= 2 || best.HitCount >= 3;

            if (knockedOut)
            {
                const float koDur = 20.0f;
                best.IsKnockedOut = true;
                best.HitCooldown = 1.2f;
                best.KnockdownTimer = koDur;
                best.FightTimer = 0f;
                best.PendingPunchTimer = 0f;
                best.FleeTimer = 0f;
                best.HitCount = 0;
                best.StumbleVel = knockDir * 7.4f;
                best.Root.transform.rotation = Quaternion.LookRotation(-knockDir, Vector3.up);
                best.Humanoid?.PlayKnockdown(koDur);

                VisualEffectsManager.SpawnFloatingText(
                    best.Root.transform.position + Vector3.up * 1.65f,
                    en ? "💫 KNOCKED OUT!" : "💫 BAYILDI!",
                    new Color(1.0f, 0.86f, 0.22f),
                    scale: 1.18f,
                    duration: 1.45f);
                GameFeelAudio.SpawnBurst(best.Root.transform.position + Vector3.up * 1.25f, new Color(1.0f, 0.88f, 0.26f), count: 14, spread: 0.65f);
                GameFeelAudio.TriggerHitstop(0.068f, slowScale: 0.18f);

                NpcSpeechBubbleManager.Say(best.Root.transform, best.DisplayName,
                    en ? "😵 Ugh... lights out..." : "😵 Off... gözlerim karardı...", 3.4f, NpcBubbleMood.Alert, best.BubbleHeight);
                PlayerModeController.Instance?.RaiseStatusMessage(en
                    ? $"💫 {best.DisplayName} was knocked out!"
                    : $"💫 {best.DisplayName} bayıldı!");
            }
            else if (knockdown)
            {
                best.HitCooldown = 0.85f;
                best.KnockdownTimer = 2.4f;
                best.HitCount = 0;
                best.StumbleVel = knockDir * 6.2f;
                best.Root.transform.rotation = Quaternion.LookRotation(-knockDir, Vector3.up);
                best.Humanoid?.PlayKnockdown(2.4f);
            }
            else
            {
                best.HitCooldown = 0.25f;
                best.StumbleVel = knockDir * 3.8f;
                best.Humanoid?.PlayHitReact();
                VisualEffectsManager.SpawnFloatingText(
                    best.Root.transform.position + Vector3.up * 1.55f,
                    $"-{Mathf.RoundToInt(dmg)}",
                    new Color(1.0f, 0.68f, 0.28f),
                    scale: 0.88f,
                    duration: 0.65f);
            }

            if (!knockedOut)
            {
                if (best.IsAuthority || best.IsFighter)
                {
                    best.FightTimer = 12f;
                    best.PunchCooldown = 0.55f;
                    if (best.IsAuthority) NeighborhoodConsequenceSystem.EnsureInstance().PlayZabitaWhistle(0.75f);
                    NpcSpeechBubbleManager.Say(best.Root.transform, best.DisplayName,
                        best.IsAuthority
                            ? (en ? "Assaulting a Municipal Officer?! Stop right there!" : "Zabıta memuruna saldırı ha?! Dur orada!")
                            : (en ? "Hey!! Who do you think you are?!" : "Hey!! Kime el kaldırıyorsun sen?!"),
                        3.0f, NpcBubbleMood.Alert, best.BubbleHeight);
                }
                else
                {
                    best.FleeTimer = 6.5f;
                    best.FleeDir = knockDir;
                    NpcSpeechBubbleManager.Say(best.Root.transform, best.DisplayName,
                        en ? "Help! Somebody call Zabıta!" : "İmdat! Zabıta çağırın!", 2.8f, NpcBubbleMood.Alert, best.BubbleHeight);
                }
            }

            NeighborhoodConsequenceSystem.EnsureInstance().ReportOffense(
                best.IsAuthority ? NeighborhoodOffenseKind.AssaultStaffOrAuthority : NeighborhoodOffenseKind.AssaultCitizen,
                best.Root.transform.position,
                best.DisplayName,
                repPenalty: best.IsAuthority ? 22 : 14,
                immediateFineTl: best.IsAuthority ? 95 : 0,
                heatDelta: best.IsAuthority ? 2.2f : 1.15f);
            NpcWorldAwareness.NotifyViolence(best.Root.transform.position, attackerPos, null);
            return true;
        }

        public void OnDiverseNpcBumpedByPlayer(int idx, Vector3 playerPos, Vector3 playerVel)
        {
            if (idx < 0 || idx >= _citizens.Count) return;
            DiverseCitizen c = _citizens[idx];
            if (c == null || c.Root == null || c.BumpCooldown > 0f || c.KnockdownTimer > 0f || c.IsAirborne) return;
            c.BumpCooldown = 2.4f;

            Vector3 away = c.Root.transform.position - playerPos;
            away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : c.Root.transform.right;
            c.StumbleVel = away * (c.IsChild ? 1.5f : 2.1f);
            c.PauseTimer = Mathf.Max(c.PauseTimer, 1.1f);
            c.Humanoid?.PlayStartled(0.60f, playerPos);

            bool en = LocalizationService.IsEnglish;
            string line = c.IsChild
                ? (en ? "Oops! Watch out!" : "Ayy, az kalsın çarpışıyorduk!")
                : (c.IsAuthority
                    ? (en ? "Easy citizen, watch your step." : "Yavaş ol komşu, dikkat et.")
                    : (en ? "Whoa, careful!" : "Hop, pardon komşu!"));
            NpcSpeechBubbleManager.Say(c.Root.transform, c.DisplayName, line, 2.2f, NpcBubbleMood.Alert, c.BubbleHeight);
        }

        public void OnDiverseNpcHitByVehicle(int idx, Vector3 carPos, Vector3 carVel, float speedKmh)
        {
            if (idx < 0 || idx >= _citizens.Count) return;
            DiverseCitizen c = _citizens[idx];
            if (c == null || c.Root == null || c.VehicleHitCooldown > 0f) return;
            c.VehicleHitCooldown = 0.9f;

            bool en = LocalizationService.IsEnglish;
            Vector3 away = c.Root.transform.position - carPos;
            away.y = 0f;
            Vector3 pushDir = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;

            // Children safely leap aside instead of violent ragdoll + heavy reckless-driving fine!
            if (c.IsChild)
            {
                c.SidestepDir = pushDir;
                c.SidestepDuration = 0.85f;
                c.SidestepTimer = 0.85f;
                c.SidestepPeakSpeed = 4.2f;
                c.FleeDir = pushDir;
                c.FleeTimer = 5.0f;
                c.Humanoid?.PlayStartled(0.90f, carPos);
                NpcSpeechBubbleManager.Say(c.Root.transform, c.DisplayName,
                    en ? "Whoa!! Watch out for kids!" : "Ayy!! Çocuklara dikkat et!", 3.0f, NpcBubbleMood.Alert, c.BubbleHeight);
                if (speedKmh >= 6f)
                {
                    NeighborhoodConsequenceSystem.EnsureInstance().PlayZabitaWhistle(0.80f);
                    NeighborhoodConsequenceSystem.EnsureInstance().ReportOffense(
                        NeighborhoodOffenseKind.HitPedestrianWithCar, c.Root.transform.position,
                        en ? "School Zone Reckless Driving" : "Çocuk Bölgesi Dikkatsiz Sürüş",
                        repPenalty: 25, immediateFineTl: 120, heatDelta: 2.5f);
                }
                return;
            }

            Vector3 impactDir = carVel;
            impactDir.y = 0f;
            if (impactDir.sqrMagnitude < 0.04f) impactDir = pushDir;
            else impactDir = (impactDir.normalized * 0.72f + pushDir * 0.28f).normalized;

            float horiz = Mathf.Clamp(speedKmh * 0.34f, 4.8f, 16.5f);
            float up = Mathf.Clamp(speedKmh * 0.12f, 2.0f, 6.5f);
            c.AirVel = impactDir * horiz + Vector3.up * up;
            c.IsAirborne = true;
            c.KnockdownTimer = Mathf.Clamp(3.0f + speedKmh * 0.04f, 3.2f, 5.2f);
            c.FleeTimer = 6.0f;
            c.FleeDir = impactDir;
            c.Root.transform.rotation = Quaternion.LookRotation(-impactDir, Vector3.up);
            c.Humanoid?.PlayKnockdown(c.KnockdownTimer);

            NpcSpeechBubbleManager.Say(c.Root.transform, c.DisplayName,
                en ? "Watch the road!!" : "Önüne baksana!!", 3.0f, NpcBubbleMood.Alert, c.BubbleHeight);

            if (speedKmh >= 8f)
            {
                NeighborhoodConsequenceSystem.EnsureInstance().ReportOffense(
                    NeighborhoodOffenseKind.HitPedestrianWithCar, c.Root.transform.position,
                    c.DisplayName, repPenalty: 18, immediateFineTl: 80, heatDelta: 1.8f);
            }
        }

        public void OnDiverseNpcHitByProp(int idx, Vector3 impactPos, Vector3 impactVel, string propLabel)
        {
            if (idx < 0 || idx >= _citizens.Count) return;
            DiverseCitizen c = _citizens[idx];
            if (c == null || c.Root == null || c.HitCooldown > 0f) return;
            c.HitCooldown = 0.65f;
            bool en = LocalizationService.IsEnglish;

            // Kids cheer when a football bounces near them!
            if (c.IsChild && propLabel != null && propLabel.IndexOf("ball", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                c.Humanoid?.PlaySportAnim(SportAnimKind.SoccerKick, 0.48f);
                NpcSpeechBubbleManager.Say(c.Root.transform, c.DisplayName,
                    en ? "Nice pass!! Pass it here!" : "Güzel pas!! Buraya at!", 2.4f, NpcBubbleMood.Friendly, c.BubbleHeight);
                return;
            }

            c.Humanoid?.PlayStartled(0.75f, impactPos);
            NpcSpeechBubbleManager.Say(c.Root.transform, c.DisplayName,
                c.IsChild ? (en ? "Hey, careful!" : "Dikkat et!") : (en ? "Hey! Don't throw things!" : "Hey! Eşya fırlatma!"),
                2.5f, NpcBubbleMood.Alert, c.BubbleHeight);
        }

        // =====================================================================
        // WORLD AWARENESS EVENT REACTIONS
        // =====================================================================
        private void HandleWorldViolence(Vector3 incidentPos, Vector3 attackerPos, TownPedestrian victim)
        {
            bool en = LocalizationService.IsEnglish;
            int reacted = 0;
            for (int i = 0; i < _citizens.Count; i++)
            {
                DiverseCitizen c = _citizens[i];
                if (c == null || c.Root == null || !c.Root.activeInHierarchy || c.KnockdownTimer > 0f || c.IsAirborne) continue;
                Vector3 d = c.Root.transform.position - incidentPos;
                d.y = 0f;
                if (d.sqrMagnitude > 18f * 18f) continue;

                if (c.IsAuthority && c.FightTimer <= 0f)
                {
                    c.FightTimer = 11f;
                    c.PunchCooldown = 0.65f;
                    NeighborhoodConsequenceSystem.EnsureInstance().PlayZabitaWhistle(0.70f);
                    NpcSpeechBubbleManager.Say(c.Root.transform, c.DisplayName,
                        en ? "Zabıta! Break it up immediately!" : "Zabıta! Hemen dur, olay çıkarma!", 3.0f, NpcBubbleMood.Alert, c.BubbleHeight);
                }
                else if (c.FleeTimer <= 0f && c.FightTimer <= 0f)
                {
                    Vector3 away = c.Root.transform.position - attackerPos;
                    away.y = 0f;
                    c.FleeDir = away.sqrMagnitude > 0.01f ? away.normalized : c.Root.transform.forward;
                    c.FleeTimer = UnityEngine.Random.Range(4.2f, 6.8f);
                    c.Humanoid?.PlayStartled(0.80f, incidentPos);
                    if (reacted < 2)
                    {
                        reacted++;
                        NpcSpeechBubbleManager.Say(c.Root.transform, c.DisplayName,
                            c.IsChild ? (en ? "Run! There's a fight!" : "Kaçın, kavga var!")
                                      : (en ? "Oh no, get the kids back!" : "Aman Allahım, çocukları çekin!"),
                            2.6f, NpcBubbleMood.Alert, c.BubbleHeight);
                    }
                }
            }
        }

        private void HandleWorldHorn(Vector3 carPos, Vector3 carFwd)
        {
            Vector3 right = Vector3.Cross(Vector3.up, carFwd).normalized;
            for (int i = 0; i < _citizens.Count; i++)
            {
                DiverseCitizen c = _citizens[i];
                if (c == null || c.Root == null || !c.Root.activeInHierarchy || c.CarDodgeCooldown > 0f) continue;
                Vector3 toMe = c.Root.transform.position - carPos;
                toMe.y = 0f;
                if (toMe.sqrMagnitude > 13f * 13f) continue;

                float side = Vector3.Dot(toMe, right) >= 0f ? 1f : -1f;
                c.SidestepDir = right * side;
                c.SidestepDuration = 0.78f;
                c.SidestepTimer = 0.78f;
                c.SidestepPeakSpeed = 2.9f;
                c.CarDodgeCooldown = 1.4f;
                c.Humanoid?.PlayStartled(0.75f, carPos);
            }
        }

        private void HandleWorldAlarm(Vector3 alarmPos)
        {
            for (int i = 0; i < _citizens.Count; i++)
            {
                DiverseCitizen c = _citizens[i];
                if (c == null || c.Root == null || !c.Root.activeInHierarchy || c.EventReactCooldown > 0f) continue;
                Vector3 d = alarmPos - c.Root.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 18f * 18f) continue;
                c.EventReactCooldown = 10f;
                c.PauseTimer = Mathf.Max(c.PauseTimer, 2.0f);
                c.Humanoid?.SetLookAtPoint(true, alarmPos + Vector3.up);
            }
        }

        private void HandleWorldPropImpact(Vector3 impactPos, Vector3 throwerPos, string propLabel)
        {
            for (int i = 0; i < _citizens.Count; i++)
            {
                DiverseCitizen c = _citizens[i];
                if (c == null || c.Root == null || !c.Root.activeInHierarchy || c.EventReactCooldown > 0f) continue;
                Vector3 d = impactPos - c.Root.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 11f * 11f) continue;
                c.EventReactCooldown = 8f;
                c.Humanoid?.PlayStartled(0.70f, impactPos);
            }
        }

        private void HandleWorldRecklessDriving(Vector3 carPos, Vector3 carVel, float speedKmh)
        {
            bool en = LocalizationService.IsEnglish;
            for (int i = 0; i < _citizens.Count; i++)
            {
                DiverseCitizen c = _citizens[i];
                if (c == null || c.Root == null || !c.Root.activeInHierarchy || c.EventReactCooldown > 0f) continue;
                Vector3 d = carPos - c.Root.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 16f * 16f) continue;
                c.EventReactCooldown = 9f;
                if (_globalBubbleCooldown <= 0f)
                {
                    _globalBubbleCooldown = 3.5f;
                    string line = c.IsAuthority
                        ? (en ? "Slow down! Children play on these streets!" : "Yavaş sür! Bu sokaklarda çocuklar oynuyor!")
                        : (en ? "Whoa, slow down!!" : "Yavaş git biraz, çocuklar var!!");
                    NpcSpeechBubbleManager.Say(c.Root.transform, c.DisplayName, line, 2.6f, NpcBubbleMood.Alert, c.BubbleHeight);
                }
            }
        }
    }
}
