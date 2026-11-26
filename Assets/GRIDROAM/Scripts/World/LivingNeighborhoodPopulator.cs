using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.UI;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Populates Istanbul · Sultanbeyli · Hamidiye Mahallesi with a vibrant, LOD-optimized
    /// living ecosystem of 52+ additional diverse NPCs and 41+ skeletal-animated wildlife actors
    /// powered by our custom Blender 5.3.0 & CC0 FBX rigs:
    /// - Strolling neighbors, joggers, tea garden guests, simit/tea vendors, sports spectators & farm residents
    ///   with context-aware Turkish speech bubbles reacting to time of day & player trophies.
    /// - Interactive flocks of Istanbul Pigeons (OW_Pigeon: Peck, Walk, Fly takeoff arcs when approached!).
    /// - Fluffy rigged Istanbul Street Cats (OW_StreetCat: Idle, Walk, Sit, Happy petting!).
    /// - Friendly Golden Street Dogs (OW_StreetDog: Idle, Walk, Gallop, Happy + 25s companion follow!).
    /// - Graceful Sultanbeyli Lake Swans (OW_LakeSwan: Swim & WingSpread!).
    /// - Horned Anatolian Farm Goats at Aydos Farm (OW_FarmGoat: Idle, Walk, Eating, Happy + fresh milk/cheese!).
    /// </summary>
    public sealed class LivingNeighborhoodPopulator : MonoBehaviour
    {
        public static LivingNeighborhoodPopulator Instance { get; private set; }

        private enum NpcRole { Stroller, Jogger, Spectator, Vendor, SeatedTeaGuest }

        private sealed class LivingNpc
        {
            public string InteractableId;
            public GameObject Root;
            public ProceduralHumanoid Humanoid;
            public NpcRole Role;
            public string DisplayName;
            public Vector3[] Route;
            public int WaypointIdx;
            public float Speed;
            public float PauseTimer;
            public float CheerTimer;
            public float NextGreetingAllowed;
            public string[] Lines;
            public float BaseYaw;
            public float TalkTimer;
            public float LookRetarget;
            public LivingNpc Partner;
            public bool LookingAtPlayer;
        }

        private sealed class FlockPigeon
        {
            public Transform Root;
            public SkeletalClipPlayer Skel;
            public Vector3 HomePos;
            public Vector3 TargetPos;
            public Vector3 TakeoffPos;
            public Vector3 LandingPos;
            public bool Flying;
            public float FlyTimer;
            public float FlyDuration;
            public float OrbitAngle;
            public float OrbitRadius;
            public float OrbitHeight;
            public float StateTimer;
            public float CurrentSpeed;
            public float BankRoll;
            public bool Walking;
        }

        private sealed class LivingAnimal
        {
            public string Id;
            public string Kind; // "cat", "dog", "goat"
            public string DisplayName;
            public Transform Root;
            public SkeletalClipPlayer Skel;
            public Transform HeadBone;
            public Vector3 HomePos;
            public Vector3 TargetPos;
            public float WanderRadius;
            public float WalkSpeed;
            public float RefWalkSpeed;
            public float CurrentSpeed;
            public float CurHeadYaw;
            public float StateTimer;
            public float HappyTimer;
            public float FollowPlayerTimer;
            public float ReactCooldown;
            public float LastRewardTime;
            public bool Walking;
            public bool Sitting;
            public bool Retreating;
            public bool FollowMoving;
            public bool IsGalloping;
        }

        private sealed class LakeSwan
        {
            public Transform Root;
            public SkeletalClipPlayer Skel;
            public Vector3 Center;
            public float RadiusX;
            public float RadiusZ;
            public float Speed;
            public float Phase;
            public float Angle;
            public float SpeedScale;
            public bool Initialized;
        }

        private static readonly RaycastHit[] AnimalObstacleHits = new RaycastHit[4];

        private readonly List<LivingNpc> _npcs = new List<LivingNpc>(64);
        private readonly List<FlockPigeon> _pigeons = new List<FlockPigeon>(24);
        private readonly List<LivingAnimal> _animals = new List<LivingAnimal>(24);
        private readonly List<LakeSwan> _swans = new List<LakeSwan>(6);
        private readonly List<Material> _ownedMats = new List<Material>(32);

        private float _globalBubbleCooldown;
        private bool _subscribed;

        public int SpawnedNpcCount => _npcs.Count;
        public int SpawnedAnimalCount => _pigeons.Count + _animals.Count + _swans.Count;

        private static readonly Color[] ShirtPalette =
        {
            new Color(0.14f, 0.56f, 0.62f), // Istanbul Turquoise
            new Color(0.76f, 0.22f, 0.24f), // Crimson
            new Color(0.86f, 0.62f, 0.18f), // Ochre Gold
            new Color(0.24f, 0.46f, 0.28f), // Olive Forest
            new Color(0.22f, 0.34f, 0.64f), // Bosphorus Navy
            new Color(0.66f, 0.34f, 0.52f), // Plum
            new Color(0.84f, 0.44f, 0.24f), // Terracotta
            new Color(0.32f, 0.58f, 0.50f), // Mint Sage
            new Color(0.88f, 0.84f, 0.74f), // Cream Linen
            new Color(0.30f, 0.32f, 0.40f)  // Charcoal Slate
        };

        private static readonly string[] NeighborNames =
        {
            "Ahmet Abi", "Ayşe Teyze", "Mehmet", "Zeynep", "Emre", "Elif", "Mustafa Amca", "Fatma Hanım",
            "Can", "Selin", "Burak", "Merve", "Hakan", "Büşra", "Oğuz", "Derya",
            "Cevdet Amca", "Hatice Teyze", "Yusuf", "İrem", "Kadir", "Sude", "Tarık", "Gizem",
            "Ömer", "Nisa", "Berkay", "Ceren", "Sinan", "Damla"
        };

        private static readonly string[] StreetGreetings =
        {
            "Günaydın komşu, hava bugün harika!",
            "Çay bahçesinde taze dem var, uğra mutlaka!",
            "Mahallemiz her geçen gün daha da güzelleşiyor.",
            "Selman Sokağı'nda simit kokusu harika!",
            "Gölet kenarında yürüyüş yaptın mı bugün?",
            "Aydos Çiftliği'nin taze sütü efsane olmuş!",
            "Spor sahaları bugün çok canlı, maç var!",
            "Havadis Sokağı'ndaki taş fırından sıcak börek kokusu geliyor!",
            "Kadife Sokağı'ndaki kıraathanede akşam çayı bir başka güzel!"
        };

        private static readonly string[] SpectatorLines =
        {
            "Haydi göster hünerini, tribün arkanda!",
            "O ne falsolu vuruştu öyle, helal olsun!",
            "Mahalle şampiyonası çok çekişmeli geçiyor!",
            "Harika oynuyorsun, skor tabelasını salladın!",
            "Birazdan 50m yüzme yarışı başlayacak!"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;

            GameObject go = new GameObject("OurWorld_LivingNeighborhoodPopulator");
            go.AddComponent<LivingNeighborhoodPopulator>();
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
            // Wait 2 frames so Player, IstanbulThemeOverhaul & FarmBuilder finish installing
            yield return null;
            yield return null;

            ProceduralHumanoid template = PlayerModeController.Instance?.FootController?.Humanoid;
            if (template != null)
            {
                SpawnDenseLivingNpcs(template);
            }

            SpawnPigeonFlocks();
            SpawnRiggedStreetCats();
            SpawnRiggedStreetDogs();
            SpawnLakeSwans();
            SpawnAydosFarmGoats();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted += HandleLivingAnimalInteracted;
                PlayerModeController.Instance.RefreshInteractablesCache();
                _subscribed = true;
            }

            Debug.Log($"[LivingNeighborhoodPopulator] Spawned {_npcs.Count} living NPCs and {SpawnedAnimalCount} rigged animals (Pigeons={_pigeons.Count}, Cats/Dogs/Goats={_animals.Count}, Swans={_swans.Count}).");
        }

        private void OnDestroy()
        {
            if (_subscribed && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted -= HandleLivingAnimalInteracted;
            }
            for (int i = 0; i < _ownedMats.Count; i++)
            {
                if (_ownedMats[i] != null) Destroy(_ownedMats[i]);
            }
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        // 1. DENSE LIVING NEIGHBORHOOD NPCs (52+ CHARACTERS WITH 3-TIER LOD!)
        // =====================================================================
        private void SpawnDenseLivingNpcs(ProceduralHumanoid template)
        {
            Transform npcRoot = new GameObject("Living_Istanbul_NPCs").transform;
            npcRoot.SetParent(transform, false);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            if (lit == null) return;
            Material[] shirtMats = new Material[ShirtPalette.Length];
            for (int i = 0; i < ShirtPalette.Length; i++)
            {
                Material m = new Material(lit) { color = ShirtPalette[i] };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", ShirtPalette[i]);
                shirtMats[i] = m;
                _ownedMats.Add(m);
            }

            int idx = 0;

            // A. 38 Strolling Sidewalk, Avenue, Plaza, Sports, Park, Lake & Farm NPCs spread across ALL 10 streets!
            Vector3[][] strollRoutes =
            {
                // Selman Sokağı (balanced 2 local strollers instead of crowding No:48!)
                new[] { new Vector3(-34f, 0.06f, 8.2f), new Vector3(38f, 0.06f, 8.2f) },
                new[] { new Vector3(28f, 0.06f, -8.4f), new Vector3(-30f, 0.06f, -8.4f) },
                // Fatih Bulvarı · Saat Kulesi Meydanı (North Main Boulevard)
                new[] { new Vector3(-68f, 0.06f, 132.4f), new Vector3(58f, 0.06f, 132.4f) },
                new[] { new Vector3(44f, 0.06f, 142.8f), new Vector3(-46f, 0.06f, 142.8f) },
                // Atatürk Caddesi (Pine-lined Avenue)
                new[] { new Vector3(-64f, 0.06f, 90.2f), new Vector3(62f, 0.06f, 90.2f) },
                new[] { new Vector3(54f, 0.06f, 101.6f), new Vector3(-48f, 0.06f, 101.6f) },
                // Mürşit Caddesi & Hamidiye Parkı Tea Garden Loop
                new[] { new Vector3(-44f, 0.06f, 51.2f), new Vector3(52f, 0.06f, 51.2f) },
                new[] { new Vector3(-30f, 0.06f, 62f), new Vector3(-8f, 0.06f, 62f), new Vector3(-8f, 0.06f, 76f), new Vector3(-30f, 0.06f, 76f) },
                // Petrol Yolu Caddesi (South Commercial Strip)
                new[] { new Vector3(-66f, 0.06f, -71.8f), new Vector3(64f, 0.06f, -71.8f) },
                new[] { new Vector3(52f, 0.06f, -84.2f), new Vector3(-56f, 0.06f, -84.2f) },
                // Aziziye Caddesi · İTO İlkokulu
                new[] { new Vector3(-62f, 0.06f, -119.5f), new Vector3(56f, 0.06f, -119.5f) },
                new[] { new Vector3(42f, 0.06f, -108.5f), new Vector3(-48f, 0.06f, -108.5f) },
                // Zübeyde Hanım Caddesi (Pine Blue-Balcony Houses)
                new[] { new Vector3(-58f, 0.06f, -151.2f), new Vector3(60f, 0.06f, -151.2f) },
                new[] { new Vector3(46f, 0.06f, -140.5f), new Vector3(-44f, 0.06f, -140.5f) },
                // 53 Eyyubi & Seyyid Kutup North-South Avenues
                new[] { new Vector3(-153.2f, 0.06f, -78f), new Vector3(-153.2f, 0.06f, 68f) },
                new[] { new Vector3(-90.2f, 0.06f, -64f), new Vector3(-90.2f, 0.06f, 84f) },
                new[] { new Vector3(93.2f, 0.06f, -92f), new Vector3(93.2f, 0.06f, 64f) },
                // Sports Complex lively walkway (Tennis -> Football -> Pool -> Basketball -> Volleyball)
                new[] { new Vector3(-46f, 0.06f, -24.5f), new Vector3(62f, 0.06f, -24.5f) },
                new[] { new Vector3(58f, 0.06f, -23.0f), new Vector3(-40f, 0.06f, -23.0f) },
                // Mosque Courtyard & Fountain Plaza
                new[] { new Vector3(44f, 0.06f, -14.0f), new Vector3(72f, 0.06f, -14.0f), new Vector3(72f, 0.06f, -24.0f), new Vector3(44f, 0.06f, -24.0f) },
                new[] { new Vector3(50f, 0.06f, -18.0f), new Vector3(66f, 0.06f, -18.0f) },
                // Lake Boulevard & Sultanbeyli Göleti Promenade (strollers + joggers!)
                new[] { new Vector3(-118f, 0.06f, 5.2f), new Vector3(-164f, 0.06f, 5.2f) },
                new[] { new Vector3(-165f, 0.06f, -16f), new Vector3(-165f, 0.06f, 16f) },
                new[] { new Vector3(-163f, 0.06f, 14f), new Vector3(-163f, 0.06f, -14f) },
                new[] { new Vector3(-168f, 0.06f, -2f), new Vector3(-181f, 0.44f, -0.8f) },
                // Aydos Farm Country Road & Farmyard
                new[] { new Vector3(118f, 0.06f, 4.2f), new Vector3(156f, 0.06f, 4.2f) },
                new[] { new Vector3(154f, 0.06f, -6f), new Vector3(178f, 0.06f, -6f), new Vector3(178f, 0.06f, 10f), new Vector3(154f, 0.06f, 10f) },
                new[] { new Vector3(162f, 0.06f, -12f), new Vector3(176f, 0.06f, -12f) },
                // Havadis Sokağı (Z = +186) lively sidewalk strollers & joggers!
                new[] { new Vector3(-72f, 0.06f, 180.5f), new Vector3(28f, 0.06f, 180.5f) },
                new[] { new Vector3(68f, 0.06f, 191.5f), new Vector3(-42f, 0.06f, 191.5f) },
                new[] { new Vector3(-120f, 0.06f, 180.5f), new Vector3(-44f, 0.06f, 180.5f) },
                new[] { new Vector3(14f, 0.06f, 191.5f), new Vector3(92f, 0.06f, 191.5f) },
                // Kadife Sokağı (Z = -186) lively sidewalk strollers & joggers!
                new[] { new Vector3(-68f, 0.06f, -180.5f), new Vector3(34f, 0.06f, -180.5f) },
                new[] { new Vector3(72f, 0.06f, -191.5f), new Vector3(-38f, 0.06f, -191.5f) },
                new[] { new Vector3(-124f, 0.06f, -191.5f), new Vector3(-46f, 0.06f, -191.5f) },
                new[] { new Vector3(18f, 0.06f, -180.5f), new Vector3(92f, 0.06f, -180.5f) }
            };

            for (int r = 0; r < strollRoutes.Length; r++)
            {
                bool isJogger = (r == 2 || r == 7 || r == 14 || r == 17 || r == 22 || r == 25 || r == 29 || r == 33);
                SpawnNpcInstance(
                    npcRoot, template, shirtMats[idx % shirtMats.Length],
                    NeighborNames[idx % NeighborNames.Length],
                    isJogger ? NpcRole.Jogger : NpcRole.Stroller,
                    strollRoutes[r],
                    isJogger ? UnityEngine.Random.Range(3.2f, 3.85f) : UnityEngine.Random.Range(1.45f, 2.05f),
                    0f,
                    StreetGreetings,
                    idx++);
            }

            // B. 14 Cheering Sports Spectators around Football, Volleyball, Tennis, Basketball & Pool!
            (Vector3 pos, float yaw, string title)[] spectators =
            {
                // Halı Saha Football sideline fans
                (new Vector3(-23.2f, 0.06f, -35.0f), 90f, "Tribün Taraftarı"),
                (new Vector3(-23.2f, 0.06f, -41.5f), 90f, "Mahalle Gençleri"),
                (new Vector3(-4.8f,  0.06f, -36.5f), -90f, "Halı Saha İzleyicisi"),
                (new Vector3(-4.8f,  0.06f, -43.0f), -90f, "Spor Tutkunu"),
                // Plaj Voleybolu fans
                (new Vector3(53.0f,  0.06f, -39.0f), 90f, "Voleybol Taraftarı"),
                (new Vector3(65.0f,  0.06f, -41.5f), -90f, "Voleybol İzleyicisi"),
                // Tennis Court spectators
                (new Vector3(-49.0f, 0.06f, -36.0f), 90f, "Tenis İzleyicisi"),
                (new Vector3(-34.0f, 0.06f, -40.0f), -90f, "Kort Hakemi"),
                // Basketball fans
                (new Vector3(33.5f,  0.06f, -38.5f), 90f, "Basketbol Taraftarı"),
                (new Vector3(48.5f,  0.06f, -41.0f), -90f, "Sokak Basketbolcusu"),
                // Swimming Pool sun-deck fans & lifeguard!
                (new Vector3(16.0f,  1.42f, -33.6f), 180f, "Havuz Hakemi"),
                (new Vector3(11.5f,  1.42f, -33.6f), 170f, "Yüzme İzleyicisi"),
                (new Vector3(20.5f,  1.42f, -33.6f), 190f, "Yüzme İzleyicisi"),
                (new Vector3(24.5f,  1.42f, -40.0f), -90f, "Cankurtaran")
            };
            for (int s = 0; s < spectators.Length; s++)
            {
                var sp = spectators[s];
                SpawnNpcInstance(
                    npcRoot, template, shirtMats[idx % shirtMats.Length],
                    sp.title, NpcRole.Spectator,
                    new[] { sp.pos }, 0f, sp.yaw,
                    SpectatorLines, idx++);
            }

            // C. 6 Iconic Istanbul Street Vendors spread across Saat Kulesi, Sports Complex, Park, Lake, Havadis & Kadife!
            (Vector3 pos, float yaw, string name, string[] lines)[] vendors =
            {
                (new Vector3(-12.0f, 0.16f, 134.5f), 180f, "Saat Kulesi Simitçisi", new[] { "Saat Kulesi meydanında taze çıtır simit var!", "Sabah simidi 10 TL, susamlı çıtır!" }),
                (new Vector3(-24.3f, 0.06f, -21.8f), -60f, "Simitçi Kemal", new[] { "Maç arası sıcak simit al, enerjin dolsun!", "Halı sahada gol atan simidi kapar!" }),
                (new Vector3(-22.4f, 0.06f,  69.2f),  90f, "Park Çaycısı", new[] { "Hamidiye Parkı'nda tavşan kanı çaylar taze!", "Buyur otur komşu, çayın hemen geliyor!" }),
                (new Vector3(-164.0f, 0.06f, -5.2f), -90f, "Gölet Simitçisi", new[] { "Gölet manzarasında sıcak simit gibisi yok!", "Kuğular da simit kokusuna geliyor!" }),
                (new Vector3( 36.0f, 0.06f, -71.8f), 180f, "Petrol Yolu Kestanecisi", new[] { "Közde sıcak kestane, akşam serinliğinde birebir!", "Petrol Yolu'ndan geçerken uğra komşu!" }),
                (new Vector3(-10.0f, 0.06f,-119.5f), 180f, "Okul Önü Simitçisi", new[] { "Teneffüs zili çaldı, taze açma ve simit hazır!", "Aziziye'nin en çıtır simidi burada!" })
            };
            for (int v = 0; v < vendors.Length; v++)
            {
                var vd = vendors[v];
                SpawnNpcInstance(npcRoot, template, shirtMats[idx % shirtMats.Length], vd.name, NpcRole.Vendor, new[] { vd.pos }, 0f, vd.yaw, vd.lines, idx++);
            }

            // D. 12 Seated Tea Garden, Plaza, Lakeside, Farm, Havadis & Kadife Guests chatting warmly!
            (Vector3 pos, float yaw)[] teaGuests =
            {
                (new Vector3(-18.0f, 0.06f,  67.2f), -90f), // Hamidiye Parkı
                (new Vector3(-18.0f, 0.06f,  71.4f), -90f), // Hamidiye Parkı
                (new Vector3(  8.0f, 0.16f, 134.8f), 180f), // Fatih Saat Kulesi
                (new Vector3( 12.2f, 0.16f, 134.8f), 180f), // Fatih Saat Kulesi
                (new Vector3( -1.62f,0.06f, -23.5f), -90f), // Spor Kompleksi
                (new Vector3(  3.08f,0.06f, -23.5f), -90f), // Spor Kompleksi
                (new Vector3(-163.6f,0.06f,   7.2f), -90f), // Sultanbeyli Göleti
                (new Vector3(-163.6f,0.06f,  11.4f), -90f), // Sultanbeyli Göleti
                (new Vector3( 49.2f, 0.06f, -15.0f), -90f), // Hz. Osman Camii
                (new Vector3( 168.0f,0.06f,   5.5f), 180f), // Aydos Çiftliği
                (new Vector3(-21.5f, 0.06f,-181.6f),   0f), // Kadife Kıraathanesi
                (new Vector3(-10.5f, 0.06f,-181.6f), 180f)  // Kadife Kıraathanesi
            };
            for (int g = 0; g < teaGuests.Length; g++)
            {
                var tg = teaGuests[g];
                SpawnNpcInstance(
                    npcRoot, template, shirtMats[idx % shirtMats.Length],
                    NeighborNames[idx % NeighborNames.Length],
                    NpcRole.SeatedTeaGuest,
                    new[] { tg.pos }, 0f, tg.yaw,
                    StreetGreetings, idx++);
            }

            // Pair each seated tea guest with its nearest seated neighbor within 3.5 m
            const float pairMaxSq = 3.5f * 3.5f;
            for (int a = 0; a < _npcs.Count; a++)
            {
                LivingNpc guestA = _npcs[a];
                if (guestA.Role != NpcRole.SeatedTeaGuest || guestA.Root == null) continue;
                LivingNpc best = null;
                float bestDistSq = pairMaxSq;
                Vector3 posA = guestA.Root.transform.position;
                for (int b = 0; b < _npcs.Count; b++)
                {
                    if (a == b) continue;
                    LivingNpc guestB = _npcs[b];
                    if (guestB.Role != NpcRole.SeatedTeaGuest || guestB.Root == null) continue;
                    float dSq = (guestB.Root.transform.position - posA).sqrMagnitude;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        best = guestB;
                    }
                }
                guestA.Partner = best;
            }
        }

        private void SpawnNpcInstance(
            Transform parent,
            ProceduralHumanoid template,
            Material shirtMat,
            string displayName,
            NpcRole role,
            Vector3[] route,
            float speed,
            float baseYaw,
            string[] lines,
            int index)
        {
            ProceduralHumanoid hum = Instantiate(template, parent);
            hum.name = $"LivingNPC_{index:00}_{role}";
            Vector3 startPos = route[0];
            if (route.Length > 1)
            {
                // Spread NPCs cleanly along their route so they never clump at spawn!
                float u = (index * 0.37f) % 1f;
                startPos = Vector3.Lerp(route[0], route[1], u);
            }
            hum.transform.position = startPos;
            hum.transform.rotation = Quaternion.Euler(0f, baseYaw, 0f);
            float heightScale = UnityEngine.Random.Range(0.92f, 1.07f);
            hum.transform.localScale = Vector3.one * heightScale;

            // Remove any stray colliders so NPCs never block cars or player movement
            Collider[] cols = hum.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < cols.Length; c++) Destroy(cols[c]);

            // Apply distinctive neighborhood outfit color
            Renderer[] rends = hum.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rends.Length; r++)
            {
                string rn = rends[r].name.ToLowerInvariant();
                if (rn.Contains("chest") || rn.Contains("torso") || rn.Contains("shirt") || rn.Contains("jacket") || rn.Contains("upperarm"))
                {
                    rends[r].sharedMaterial = shirtMat;
                }
            }

            if (role == NpcRole.SeatedTeaGuest)
            {
                float groundY = hum.transform.position.y - 0.02f;
                hum.transform.position = new Vector3(hum.transform.position.x, groundY + 0.48f, hum.transform.position.z);
                hum.SetSitting(true);
                hum.SetSeatContact(groundY, 1f);
            }
            else
            {
                hum.SetLocomotion(0f, true, 0f);
            }

            string npcTriggerId = $"living_npc_{index:00}";
            InteractableTrigger npcTrig = hum.gameObject.AddComponent<InteractableTrigger>();
            npcTrig.Configure(InteractableKind.TownCitizen, npcTriggerId, "ui.btn.chat", 2.85f);

            _npcs.Add(new LivingNpc
            {
                InteractableId = npcTriggerId,
                Root = hum.gameObject,
                Humanoid = hum,
                Role = role,
                DisplayName = displayName,
                Route = route,
                WaypointIdx = route.Length > 1 ? 1 : 0,
                Speed = speed,
                PauseTimer = UnityEngine.Random.Range(0.2f, 2.0f),
                CheerTimer = UnityEngine.Random.Range(2.5f, 7.5f),
                NextGreetingAllowed = Time.time + UnityEngine.Random.Range(3f, 10f),
                Lines = lines,
                BaseYaw = baseYaw,
                TalkTimer = role == NpcRole.SeatedTeaGuest
                    ? UnityEngine.Random.Range(5f, 12f)
                    : (role == NpcRole.Vendor ? UnityEngine.Random.Range(8f, 15f) : 0f),
                LookRetarget = UnityEngine.Random.Range(2f, 5f)
            });
        }

        // =====================================================================
        // 2. RIGGED WILDLIFE ECOSYSTEM (PIGEONS, CATS, DOGS, SWANS, GOATS!)
        // =====================================================================
        private void SpawnPigeonFlocks()
        {
            Transform root = new GameObject("Istanbul_Pigeon_Flocks").transform;
            root.SetParent(transform, false);

            // 5 flocks of 6 pigeons (30 total) spread across Saat Kulesi, Mosque, Park, Havadis & Kadife!
            Vector3[] centers =
            {
                new Vector3(  4.5f, 0.16f,  136.0f), // Fatih Bulvarı Saat Kulesi Meydanı
                new Vector3( 52.0f, 0.04f,  -18.5f), // Hz. Osman Camii Avlusu
                new Vector3(-18.0f, 0.04f,   68.5f), // Hamidiye Parkı & Çay Bahçesi
                new Vector3(-16.0f, 0.04f,  180.8f), // Havadis Sokağı Fırın önü
                new Vector3(-14.0f, 0.04f, -180.8f)  // Kadife Sokağı Kıraathane önü
            };

            for (int f = 0; f < centers.Length; f++)
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = (i / 6f) * Mathf.PI * 2f + f;
                    Vector3 pos = centers[f] + new Vector3(Mathf.Cos(a) * UnityEngine.Random.Range(1.0f, 2.8f), 0f, Mathf.Sin(a) * UnityEngine.Random.Range(1.0f, 2.8f));
                    GameObject bird = OurWorldModelLibrary.InstantiateModel("OW_Pigeon", root, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), Vector3.one * UnityEngine.Random.Range(0.95f, 1.15f));
                    if (bird == null) continue;

                    SkeletalClipPlayer skel = bird.AddComponent<SkeletalClipPlayer>();
                    if (!skel.Initialize("OW_Pigeon", "Peck", i * 0.45f)) skel = null;

                    _pigeons.Add(new FlockPigeon
                    {
                        Root = bird.transform,
                        Skel = skel,
                        HomePos = centers[f],
                        TargetPos = pos,
                        OrbitAngle = a,
                        OrbitRadius = UnityEngine.Random.Range(4.0f, 7.5f),
                        OrbitHeight = UnityEngine.Random.Range(4.8f, 7.8f),
                        StateTimer = UnityEngine.Random.Range(1.0f, 3.8f)
                    });
                }
            }
        }

        private void SpawnRiggedStreetCats()
        {
            Transform root = new GameObject("Istanbul_Rigged_StreetCats").transform;
            root.SetParent(transform, false);

            (Vector3 pos, string name, Color coat)[] cats =
            {
                (new Vector3(  14.0f, 0.04f,  132.8f), "Sarman", new Color(0.90f, 0.56f, 0.22f)), // Fatih Bulvarı
                (new Vector3( -11.0f, 0.04f,    8.8f), "Tekir",  new Color(0.58f, 0.54f, 0.48f)), // Selman Sokağı
                (new Vector3(  45.5f, 0.04f,  -14.5f), "Pamuk",  new Color(0.95f, 0.93f, 0.89f)), // Hz. Osman Camii
                (new Vector3( -22.0f, 0.04f,   67.0f), "Kömür",  new Color(0.22f, 0.23f, 0.26f)), // Hamidiye Parkı
                (new Vector3( -18.0f, 0.04f,  -71.8f), "Tarçın", new Color(0.76f, 0.42f, 0.20f)), // Petrol Yolu Cd.
                (new Vector3(  16.0f, 0.04f, -151.2f), "Boncuk", new Color(0.86f, 0.72f, 0.52f)), // Zübeyde Hanım Cd.
                (new Vector3(-164.0f, 0.05f,    5.0f), "Martı",  new Color(0.74f, 0.76f, 0.80f)), // Sultanbeyli Göleti
                (new Vector3( 158.0f, 0.04f,    4.5f), "Fındık", new Color(0.84f, 0.52f, 0.26f)), // Aydos Çiftliği
                (new Vector3(-152.0f, 0.04f,  -19.0f), "Mırnav", new Color(0.66f, 0.62f, 0.58f)), // 53 Eyyubi Büfesi
                (new Vector3(  38.0f, 0.04f,  180.8f), "Lokum",  new Color(0.92f, 0.78f, 0.58f)), // Havadis Sokağı
                (new Vector3( -28.0f, 0.04f, -180.8f), "Zeytin", new Color(0.18f, 0.20f, 0.24f))  // Kadife Sokağı
            };

            for (int i = 0; i < cats.Length; i++)
            {
                var c = cats[i];
                GameObject cat = OurWorldModelLibrary.InstantiateModel("OW_StreetCat", root, c.pos, Quaternion.Euler(0f, i * 55f, 0f), Vector3.one * 1.12f);
                if (cat == null) continue;
                OurWorldModelLibrary.TintMaterialByKeyword(cat, "CatFurPrimary", c.coat);

                SkeletalClipPlayer skel = cat.AddComponent<SkeletalClipPlayer>();
                if (!skel.Initialize("OW_StreetCat", i % 2 == 0 ? "Sit" : "Idle", i * 0.7f)) skel = null;

                string id = $"living_cat_{i}";
                InteractableTrigger trig = cat.AddComponent<InteractableTrigger>();
                trig.Configure(InteractableKind.InspectObject, id, "ui.btn.interact", 2.4f);
                trig.SetCustomPrompt($"{c.name} Kedisini Sev (+12 Enerji)", $"Pet {c.name} the Cat (+12 Energy)");

                _animals.Add(new LivingAnimal
                {
                    Id = id,
                    Kind = "cat",
                    DisplayName = c.name,
                    Root = cat.transform,
                    Skel = skel,
                    HeadBone = skel != null ? skel.FindBone("Head", "Neck3", "Neck") : null,
                    HomePos = c.pos,
                    TargetPos = c.pos,
                    WanderRadius = 3.5f,
                    WalkSpeed = 1.05f,
                    RefWalkSpeed = 1.00f,
                    StateTimer = UnityEngine.Random.Range(2f, 6f),
                    Sitting = i % 2 == 0,
                    LastRewardTime = -60f
                });
            }
        }

        private void SpawnRiggedStreetDogs()
        {
            Transform root = new GameObject("Istanbul_Rigged_StreetDogs").transform;
            root.SetParent(transform, false);

            (Vector3 pos, string name)[] dogs =
            {
                (new Vector3(  32.0f, 0.04f,   90.2f), "Paşa"),   // Atatürk Caddesi
                (new Vector3( -16.5f, 0.04f,  -22.8f), "Kral"),   // Spor Kompleksi
                (new Vector3( -18.0f, 0.04f, -119.5f), "Poyraz"), // Aziziye Cd. İlkokul
                (new Vector3(-166.0f, 0.05f,   13.0f), "Rüzgar"), // Sultanbeyli Göleti
                (new Vector3(-165.5f, 0.05f,  -12.0f), "Duman"),  // Sultanbeyli Göleti
                (new Vector3( -28.0f, 0.04f,   10.2f), "Çakıl"),  // Selman Sokağı
                (new Vector3( -30.0f, 0.04f,  180.6f), "Badem"),  // Havadis Sokağı
                (new Vector3(  18.0f, 0.04f, -191.4f), "Tarçın")  // Kadife Sokağı
            };

            for (int i = 0; i < dogs.Length; i++)
            {
                var d = dogs[i];
                GameObject dog = OurWorldModelLibrary.InstantiateModel("OW_StreetDog", root, d.pos, Quaternion.Euler(0f, i * 60f, 0f), Vector3.one * 0.28f);
                if (dog == null) continue;

                SkeletalClipPlayer skel = dog.AddComponent<SkeletalClipPlayer>();
                if (!skel.Initialize("OW_StreetDog", "Idle", i * 0.9f)) skel = null;

                string id = $"living_dog_{i}";
                InteractableTrigger trig = dog.AddComponent<InteractableTrigger>();
                trig.Configure(InteractableKind.InspectObject, id, "ui.btn.interact", 2.6f);
                trig.SetCustomPrompt($"{d.name} Köpeğini Sev (+15 Enerji · Dost Ol)", $"Pet {d.name} the Dog (+15 Energy · Befriend)");

                _animals.Add(new LivingAnimal
                {
                    Id = id,
                    Kind = "dog",
                    DisplayName = d.name,
                    Root = dog.transform,
                    Skel = skel,
                    HeadBone = skel != null ? skel.FindBone("Head", "Neck3", "Neck") : null,
                    HomePos = d.pos,
                    TargetPos = d.pos,
                    WanderRadius = 5.5f,
                    WalkSpeed = 1.85f,
                    RefWalkSpeed = 1.75f,
                    StateTimer = UnityEngine.Random.Range(1.5f, 4.5f),
                    LastRewardTime = -60f
                });
            }
        }

        private void SpawnLakeSwans()
        {
            Transform root = new GameObject("SultanbeyliLake_Rigged_Swans").transform;
            root.SetParent(transform, false);

            Vector3 lakeCenter = new Vector3(-198.0f, 0.06f, 0.0f);
            for (int i = 0; i < 4; i++)
            {
                GameObject swan = OurWorldModelLibrary.InstantiateModel("OW_LakeSwan", root, lakeCenter, Quaternion.identity, Vector3.one * UnityEngine.Random.Range(1.05f, 1.20f));
                if (swan == null) continue;

                SkeletalClipPlayer skel = swan.AddComponent<SkeletalClipPlayer>();
                if (!skel.Initialize("OW_LakeSwan", "Swim", i * 1.1f)) skel = null;

                _swans.Add(new LakeSwan
                {
                    Root = swan.transform,
                    Skel = skel,
                    Center = lakeCenter + new Vector3((i - 1.5f) * 4.5f, 0f, (i % 2 == 0 ? 4.5f : -4.5f)),
                    RadiusX = 7.5f + i * 1.8f,
                    RadiusZ = 5.5f + i * 1.4f,
                    Speed = 0.24f + i * 0.04f,
                    Phase = i * 1.65f,
                    Angle = i * 1.65f,
                    SpeedScale = 1f
                });
            }
        }

        private void SpawnAydosFarmGoats()
        {
            Transform root = new GameObject("AydosFarm_Rigged_Goats").transform;
            root.SetParent(transform, false);

            Vector3 penCenter = new Vector3(165.0f, 0.04f, -14.0f);
            string[] goatNames = { "Akkız", "Kınalı", "Sakar", "Benekli", "Dağlı" };
            for (int i = 0; i < goatNames.Length; i++)
            {
                float a = (i / (float)goatNames.Length) * Mathf.PI * 2f;
                Vector3 pos = penCenter + new Vector3(Mathf.Cos(a) * 3.6f, 0f, Mathf.Sin(a) * 2.8f);
                GameObject goat = OurWorldModelLibrary.InstantiateModel("OW_FarmGoat", root, pos, Quaternion.Euler(0f, i * 72f, 0f), Vector3.one * 1.05f);
                if (goat == null) continue;

                SkeletalClipPlayer skel = goat.AddComponent<SkeletalClipPlayer>();
                if (!skel.Initialize("OW_FarmGoat", i % 2 == 0 ? "Eating" : "Idle", i * 0.8f)) skel = null;

                string id = $"living_goat_{i}";
                InteractableTrigger trig = goat.AddComponent<InteractableTrigger>();
                trig.Configure(InteractableKind.InspectObject, id, "ui.btn.interact", 2.8f);
                trig.SetCustomPrompt($"{goatNames[i]} Keçisini Sev & Peynir Al (+45 ₺)", $"Pet {goatNames[i]} the Goat & Collect Cheese (+45 ₺)");

                _animals.Add(new LivingAnimal
                {
                    Id = id,
                    Kind = "goat",
                    DisplayName = goatNames[i],
                    Root = goat.transform,
                    Skel = skel,
                    HeadBone = skel != null ? skel.FindBone("Head", "Neck3", "Neck") : null,
                    HomePos = penCenter,
                    TargetPos = pos,
                    WanderRadius = 4.8f,
                    WalkSpeed = 1.45f,
                    RefWalkSpeed = 1.35f,
                    StateTimer = UnityEngine.Random.Range(2f, 5f),
                    LastRewardTime = -90f
                });
            }
        }

        // =====================================================================
        // 3. DISTANCE-LOD UPDATE LOOP (60 FPS ON iOS!)
        // =====================================================================
        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            Vector3 playerPos = PlayerModeController.Instance != null
                ? PlayerModeController.Instance.ActiveWorldPosition
                : Vector3.zero;
            bool playerMovingFast = PlayerModeController.Instance != null &&
                (PlayerModeController.Instance.CurrentMode == ControlMode.Driving ||
                 (PlayerModeController.Instance.FootController != null && PlayerModeController.Instance.FootController.CurrentSpeed > 3.2f));

            if (_globalBubbleCooldown > 0f) _globalBubbleCooldown -= dt;

            UpdateLivingNpcs(dt, playerPos);
            UpdatePigeonFlocks(dt, playerPos, playerMovingFast);
            UpdateLivingAnimals(dt, playerPos, playerMovingFast);
            UpdateLakeSwans(dt);
        }

        private void LateUpdate()
        {
            for (int i = 0; i < _animals.Count; i++)
            {
                LivingAnimal a = _animals[i];
                if (a.HeadBone != null && a.Root != null && Mathf.Abs(a.CurHeadYaw) > 0.5f)
                {
                    a.HeadBone.Rotate(a.Root.up, a.CurHeadYaw, Space.World);
                }
            }
        }

        private void UpdateLivingNpcs(float dt, Vector3 playerPos)
        {
            int frame = Time.frameCount;
            for (int i = 0; i < _npcs.Count; i++)
            {
                LivingNpc npc = _npcs[i];
                if (npc.Root == null || npc.Humanoid == null) continue;

                Vector3 pos = npc.Root.transform.position;
                float distSq = (pos - playerPos).sqrMagnitude;

                // Tier 3 LOD: cull beyond 78 m
                if (distSq > 78f * 78f)
                {
                    if (npc.Root.activeSelf) npc.Root.SetActive(false);
                    continue;
                }
                if (!npc.Root.activeSelf) npc.Root.SetActive(true);

                // Tier 2 LOD (32-78 m): update only 1/3 of frames
                bool farLod = distSq > 32f * 32f;
                if (farLod && ((frame + i) % 3 != 0)) continue;
                float stepDt = farLod ? dt * 3f : dt;

                if (npc.Role == NpcRole.Stroller || npc.Role == NpcRole.Jogger)
                {
                    if (npc.Route != null && npc.Route.Length >= 2)
                    {
                        if (npc.PauseTimer > 0f)
                        {
                            npc.PauseTimer -= stepDt;
                            npc.Humanoid.SetLocomotion(0f, true, 0f);
                            if (npc.Role == NpcRole.Stroller)
                            {
                                UpdateStandingLookAtPlayer(npc, distSq, playerPos);
                            }
                        }
                        else
                        {
                            if (npc.LookingAtPlayer && distSq > 6f * 6f)
                            {
                                npc.Humanoid.SetLookAtPoint(false, Vector3.zero);
                                npc.LookingAtPlayer = false;
                            }
                            Vector3 target = npc.Route[npc.WaypointIdx];
                            Vector3 delta = target - pos;
                            delta.y = 0f;
                            float d = delta.magnitude;
                            if (d < 0.45f)
                            {
                                npc.WaypointIdx = (npc.WaypointIdx + 1) % npc.Route.Length;
                                npc.PauseTimer = npc.Role == NpcRole.Jogger ? 0.1f : UnityEngine.Random.Range(0.4f, 1.8f);
                            }
                            else
                            {
                                Vector3 dir = delta / d;
                                // Smart personal-space avoidance around the player so living NPCs politely step around Deniz!
                                if (distSq > 0.01f && distSq < 1.95f * 1.95f)
                                {
                                    Vector3 away = pos - playerPos;
                                    away.y = 0f;
                                    float awayMag = away.magnitude;
                                    if (awayMag > 0.01f)
                                    {
                                        float w = Mathf.InverseLerp(1.95f, 0.60f, awayMag) * 1.45f;
                                        Vector3 steered = dir + (away / awayMag) * w;
                                        steered.y = 0f;
                                        if (steered.sqrMagnitude > 0.001f) dir = steered.normalized;
                                    }
                                }
                                pos += dir * (npc.Speed * stepDt);
                                npc.Root.transform.position = pos;
                                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
                                npc.Root.transform.rotation = Quaternion.Slerp(npc.Root.transform.rotation, rot, 1f - Mathf.Exp(-7f * stepDt));
                                npc.Humanoid.SetLocomotion(npc.Speed, true, 0f, dir, sampleGround: false);
                            }
                        }
                    }
                }
                else if (npc.Role == NpcRole.Spectator || npc.Role == NpcRole.Vendor)
                {
                    npc.Humanoid.SetLocomotion(0f, true, 0f);
                    UpdateStandingLookAtPlayer(npc, distSq, playerPos);
                    npc.CheerTimer -= stepDt;
                    if (npc.CheerTimer <= 0f)
                    {
                        npc.CheerTimer = UnityEngine.Random.Range(4.0f, 9.5f);
                        if (!farLod)
                        {
                            if (npc.Role == NpcRole.Spectator && UnityEngine.Random.value < 0.45f)
                                npc.Humanoid.PlaySportAnim(SportAnimKind.VictoryJump, 0.65f);
                            else
                                npc.Humanoid.PlayWave();
                        }
                    }
                    if (npc.Role == NpcRole.Vendor)
                    {
                        npc.TalkTimer -= stepDt;
                        if (npc.TalkTimer <= 0f)
                        {
                            npc.TalkTimer = UnityEngine.Random.Range(8f, 15f);
                            if (!farLod) npc.Humanoid.SetTalking(true, 2.5f);
                        }
                    }
                }
                else if (npc.Role == NpcRole.SeatedTeaGuest)
                {
                    bool hasPartner = npc.Partner != null && npc.Partner.Root != null &&
                                      npc.Partner.Humanoid != null && npc.Partner.Root.activeInHierarchy;

                    npc.LookRetarget -= stepDt;
                    if (npc.LookRetarget <= 0f)
                    {
                        npc.LookRetarget = UnityEngine.Random.Range(3f, 6f);
                        if (distSq <= 6f * 6f && UnityEngine.Random.value < 0.35f)
                        {
                            npc.LookingAtPlayer = true;
                        }
                        else
                        {
                            npc.LookingAtPlayer = false;
                            if (hasPartner)
                            {
                                Vector3 partnerHead = npc.Partner.Humanoid.HeadTransform != null
                                    ? npc.Partner.Humanoid.HeadTransform.position
                                    : npc.Partner.Root.transform.position + Vector3.up * 1.15f;
                                npc.Humanoid.SetLookAtPoint(true, partnerHead);
                            }
                            else
                            {
                                npc.Humanoid.SetLookAtPoint(false, Vector3.zero);
                            }
                        }
                    }

                    if (npc.LookingAtPlayer)
                    {
                        if (distSq <= 6f * 6f)
                        {
                            npc.Humanoid.SetLookAtPoint(true, playerPos + Vector3.up * 1.55f);
                        }
                        else
                        {
                            npc.LookingAtPlayer = false;
                            npc.Humanoid.SetLookAtPoint(false, Vector3.zero);
                        }
                    }

                    npc.TalkTimer -= stepDt;
                    if (npc.TalkTimer <= 0f)
                    {
                        npc.TalkTimer = UnityEngine.Random.Range(5f, 12f);
                        if (hasPartner && !farLod)
                        {
                            npc.Humanoid.SetTalking(true, UnityEngine.Random.Range(2.2f, 4f));
                            Vector3 speakerHead = npc.Humanoid.HeadTransform != null
                                ? npc.Humanoid.HeadTransform.position
                                : pos + Vector3.up * 1.15f;
                            Vector3 partnerHead = npc.Partner.Humanoid.HeadTransform != null
                                ? npc.Partner.Humanoid.HeadTransform.position
                                : npc.Partner.Root.transform.position + Vector3.up * 1.15f;
                            npc.Partner.Humanoid.SetLookAtPoint(true, speakerHead);
                            npc.Partner.LookingAtPlayer = false;
                            npc.Humanoid.SetLookAtPoint(true, partnerHead);
                            npc.LookingAtPlayer = false;
                        }
                    }
                }

                // Friendly reactive neighborhood greetings when player walks close!
                if (distSq < 9.5f * 9.5f && _globalBubbleCooldown <= 0f && Time.time >= npc.NextGreetingAllowed)
                {
                    npc.NextGreetingAllowed = Time.time + UnityEngine.Random.Range(18f, 30f);
                    _globalBubbleCooldown = 5.2f;
                    string line = ChooseReactiveLine(npc);
                    NpcSpeechBubbleManager.Say(npc.Root.transform, npc.DisplayName, line, 3.6f, NpcBubbleMood.Friendly, 2.08f);
                    if (npc.Role != NpcRole.SeatedTeaGuest) npc.Humanoid.PlayWave();
                }
            }
        }

        private static void UpdateStandingLookAtPlayer(LivingNpc npc, float distSq, Vector3 playerPos)
        {
            if (distSq <= 4f * 4f)
            {
                npc.Humanoid.SetLookAtPoint(true, playerPos + Vector3.up * 1.55f);
                npc.LookingAtPlayer = true;
            }
            else if (npc.LookingAtPlayer && distSq > 6f * 6f)
            {
                npc.Humanoid.SetLookAtPoint(false, Vector3.zero);
                npc.LookingAtPlayer = false;
            }
        }

        private static string ChooseReactiveLine(LivingNpc npc)
        {
            // React dynamically to player's sports trophies or time of day!
            SaveData save = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (save != null && UnityEngine.Random.value < 0.35f)
            {
                if (save.footballWins > 0 && UnityEngine.Random.value < 0.5f)
                    return $"Halı sahada {save.footballWins} galibiyet almışsın, helal olsun!";
                if (save.swimWins > 0 && UnityEngine.Random.value < 0.5f)
                    return "Havuzda 50 metreyi fırtına gibi yüzmüşsün!";
                if (save.tennisWins > 0)
                    return $"Tenis kortunda {save.bestTennisServeKmh} km/s servis konuşuluyor!";
            }

            float hr = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentHour : 11f;
            if ((hr >= 19.5f || hr < 5.5f) && UnityEngine.Random.value < 0.35f)
            {
                return "İyi akşamlar komşu, ışıklar yanınca mahalle bir başka güzel!";
            }

            if (npc.Lines != null && npc.Lines.Length > 0)
            {
                return npc.Lines[UnityEngine.Random.Range(0, npc.Lines.Length)];
            }
            return "Merhaba komşu!";
        }

        private void UpdatePigeonFlocks(float dt, Vector3 playerPos, bool playerFast)
        {
            float scareDistSq = playerFast ? (6.8f * 6.8f) : (3.9f * 3.9f);
            for (int i = 0; i < _pigeons.Count; i++)
            {
                FlockPigeon p = _pigeons[i];
                if (p.Root == null) continue;
                Vector3 pos = p.Root.position;
                float distSq = (pos - playerPos).sqrMagnitude;
                if (distSq > 55f * 55f) continue;

                if (!p.Flying && distSq < scareDistSq)
                {
                    // Startled flock takeoff into a smooth climb -> circling arc -> flared landing!
                    p.Flying = true;
                    p.Walking = false;
                    p.CurrentSpeed = 0f;
                    p.FlyTimer = 0f;
                    p.FlyDuration = UnityEngine.Random.Range(4.8f, 7.2f);
                    p.TakeoffPos = pos;
                    p.OrbitAngle = Mathf.Atan2(pos.z - p.HomePos.z, pos.x - p.HomePos.x);

                    // Choose a clean landing spot near HomePos (away from player if player is close)
                    Vector2 landRnd = UnityEngine.Random.insideUnitCircle * 2.4f;
                    Vector3 landSpot = p.HomePos + new Vector3(landRnd.x, 0f, landRnd.y);
                    Vector3 awayPlayer = landSpot - playerPos;
                    awayPlayer.y = 0f;
                    if (awayPlayer.sqrMagnitude < 4f && awayPlayer.sqrMagnitude > 0.01f)
                    {
                        landSpot = p.HomePos + awayPlayer.normalized * 2.5f;
                    }
                    landSpot.y = p.HomePos.y;
                    p.LandingPos = landSpot;
                    p.Skel?.PlayClip("Fly", 0.12f, 1.55f);
                }

                if (p.Flying)
                {
                    p.FlyTimer += dt;
                    float u = Mathf.Clamp01(p.FlyTimer / Mathf.Max(0.5f, p.FlyDuration));
                    float takeoffBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.22f));
                    float landBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.78f) / 0.22f));

                    p.OrbitAngle += dt * Mathf.Lerp(1.55f, 0.85f, landBlend);
                    float heightEnv = Mathf.SmoothStep(0f, 1f, Mathf.Sin(u * Mathf.PI));
                    float r = Mathf.Lerp(2.0f, p.OrbitRadius, takeoffBlend);
                    Vector3 orbitPos = p.HomePos + new Vector3(Mathf.Cos(p.OrbitAngle) * r, 0f, Mathf.Sin(p.OrbitAngle) * r);
                    Vector3 horiz = Vector3.Lerp(Vector3.Lerp(p.TakeoffPos, orbitPos, takeoffBlend), p.LandingPos, landBlend);
                    Vector3 target = new Vector3(horiz.x, p.HomePos.y + heightEnv * p.OrbitHeight, horiz.z);

                    Vector3 nextPos = Vector3.Lerp(pos, target, 1f - Mathf.Exp(-7.5f * dt));
                    Vector3 vel = (nextPos - pos) / dt;
                    p.Root.position = nextPos;

                    Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);
                    if (vel.sqrMagnitude > 0.04f && flatVel.sqrMagnitude > 0.01f)
                    {
                        Quaternion flightRot = Quaternion.LookRotation(vel.normalized, Vector3.up);
                        Quaternion flatRot = Quaternion.LookRotation(flatVel.normalized, Vector3.up);
                        // Gentle nose-up braking flare right before touchdown, leveling cleanly to 0°!
                        float flarePitch = -11f * Mathf.Sin(landBlend * Mathf.PI);
                        Quaternion flareRot = flatRot * Quaternion.Euler(flarePitch, 0f, 0f);
                        Quaternion desiredRot = Quaternion.Slerp(flightRot, flareRot, landBlend);

                        float targetBank = 16f * takeoffBlend * (1f - landBlend);
                        p.BankRoll = Mathf.Lerp(p.BankRoll, targetBank, 1f - Mathf.Exp(-6f * dt));
                        desiredRot *= Quaternion.Euler(0f, 0f, p.BankRoll);
                        p.Root.rotation = Quaternion.Slerp(p.Root.rotation, desiredRot, 1f - Mathf.Exp(-8.5f * dt));
                    }

                    float flyClipSpd = u < 0.22f ? 1.55f : (u > 0.78f ? 1.45f : 1.18f);
                    p.Skel?.PlayClip("Fly", 0.14f, flyClipSpd);

                    if (u >= 1f)
                    {
                        p.Flying = false;
                        p.BankRoll = 0f;
                        p.CurrentSpeed = 0f;
                        p.Root.position = new Vector3(nextPos.x, p.HomePos.y, nextPos.z);
                        p.StateTimer = UnityEngine.Random.Range(1.5f, 4.0f);
                        p.Skel?.PlayClip("Peck", 0.24f, 1.0f);
                    }
                    continue;
                }

                // Keep grounded pigeon upright (smoothly clears any residual flight pitch/roll)
                Vector3 levelFwd = p.Root.forward;
                levelFwd.y = 0f;
                if (levelFwd.sqrMagnitude > 0.001f)
                {
                    Quaternion upright = Quaternion.LookRotation(levelFwd.normalized, Vector3.up);
                    p.Root.rotation = Quaternion.Slerp(p.Root.rotation, upright, 1f - Mathf.Exp(-10f * dt));
                }

                p.StateTimer -= dt;
                if (p.Walking)
                {
                    Vector3 delta = p.TargetPos - pos;
                    delta.y = 0f;
                    float dist = delta.magnitude;
                    if (dist < 0.18f || p.StateTimer <= 0f)
                    {
                        p.Walking = false;
                        p.CurrentSpeed = 0f;
                        p.StateTimer = UnityEngine.Random.Range(1.8f, 4.6f);
                        p.Skel?.PlayClip("Peck", 0.24f, 1.0f);
                    }
                    else
                    {
                        Vector3 dir = delta / dist;
                        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                        p.Root.rotation = Quaternion.Slerp(p.Root.rotation, targetRot, 1f - Mathf.Exp(-8.5f * dt));

                        float alignDot = Vector3.Dot(p.Root.forward, dir);
                        float alignGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.92f, alignDot));
                        float arriveGate = Mathf.Clamp01(dist / 0.35f);
                        float targetSpd = 0.65f * alignGate * arriveGate;
                        p.CurrentSpeed = Mathf.Lerp(p.CurrentSpeed, targetSpd, 1f - Mathf.Exp(-10f * dt));

                        if (p.CurrentSpeed > 0.02f)
                        {
                            Vector3 moveFwd = p.Root.forward;
                            moveFwd.y = 0f;
                            if (moveFwd.sqrMagnitude > 0.001f) moveFwd.Normalize();
                            p.Root.position = pos + moveFwd * (p.CurrentSpeed * dt);
                            p.Skel?.PlayClip("Walk", 0.20f, Mathf.Clamp(p.CurrentSpeed / 0.58f, 0.6f, 1.6f));
                        }
                        else
                        {
                            p.Skel?.PlayClip("Peck", 0.22f, 1.0f);
                        }
                    }
                }
                else
                {
                    p.CurrentSpeed = Mathf.Lerp(p.CurrentSpeed, 0f, 1f - Mathf.Exp(-10f * dt));
                    if (p.StateTimer <= 0f)
                    {
                        Vector2 rnd = UnityEngine.Random.insideUnitCircle * 2.8f;
                        Vector3 candidate = p.HomePos + new Vector3(rnd.x, 0f, rnd.y);
                        Vector3 toCand = candidate - pos;
                        toCand.y = 0f;
                        float candDist = toCand.magnitude;
                        // Single one-shot obstacle check on target selection (not per frame!)
                        if (candDist > 0.25f && !IsPathBlockedByObstacle(p.Root, pos, toCand / candDist, Mathf.Min(candDist, 2.5f), 0.14f))
                        {
                            p.TargetPos = candidate;
                            p.Walking = true;
                            p.StateTimer = UnityEngine.Random.Range(1.4f, 3.2f);
                        }
                        else
                        {
                            p.StateTimer = UnityEngine.Random.Range(0.8f, 2.0f);
                        }
                    }
                }
            }
        }

        private void UpdateLivingAnimals(float dt, Vector3 playerPos, bool playerFast)
        {
            int frame = Time.frameCount;
            for (int i = 0; i < _animals.Count; i++)
            {
                LivingAnimal a = _animals[i];
                if (a.Root == null) continue;
                Vector3 pos = a.Root.position;
                Vector3 toPlayer = playerPos - pos;
                toPlayer.y = 0f;
                float distSq = toPlayer.sqrMagnitude;
                if (distSq > 64f * 64f)
                {
                    a.CurHeadYaw = 0f;
                    continue;
                }

                if (a.ReactCooldown > 0f) a.ReactCooldown = Mathf.Max(0f, a.ReactCooldown - dt);

                // Smooth living head look-at when player is within 6.5 m
                float desiredHeadYaw = 0f;
                if (a.HeadBone != null && !a.Retreating && distSq > 0.16f && distSq < 42.25f)
                {
                    Vector3 localP = a.Root.InverseTransformDirection(toPlayer.normalized);
                    float yaw = Mathf.Atan2(localP.x, localP.z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(yaw) < 95f)
                    {
                        desiredHeadYaw = Mathf.Clamp(yaw, -48f, 48f) * 0.6f;
                    }
                }
                a.CurHeadYaw = Mathf.Lerp(a.CurHeadYaw, desiredHeadYaw, 1f - Mathf.Exp(-6f * dt));

                if (a.HappyTimer > 0f)
                {
                    a.HappyTimer -= dt;
                    a.CurrentSpeed = Mathf.Lerp(a.CurrentSpeed, 0f, 1f - Mathf.Exp(-10f * dt));
                    if (distSq > 0.05f)
                    {
                        a.Root.rotation = Quaternion.Slerp(a.Root.rotation, Quaternion.LookRotation(toPlayer.normalized, Vector3.up), 1f - Mathf.Exp(-7f * dt));
                    }
                    a.Skel?.PlayClip("Happy", 0.20f, 1.15f);
                    continue;
                }

                // Friendly petted Street Dog trots happily alongside Deniz for 25 seconds!
                if (a.FollowPlayerTimer > 0f && a.Kind == "dog")
                {
                    a.FollowPlayerTimer -= dt;
                    float d = Mathf.Sqrt(distSq);
                    if (!a.FollowMoving && d > 2.7f && d < 28f) a.FollowMoving = true;
                    else if (a.FollowMoving && (d < 2.0f || d >= 28f)) a.FollowMoving = false;

                    if (!a.IsGalloping && d > 6.0f) a.IsGalloping = true;
                    else if (a.IsGalloping && d < 4.5f) a.IsGalloping = false;

                    float desiredSpd = 0f;
                    if (a.FollowMoving && d > 0.05f)
                    {
                        Vector3 dir = toPlayer / d;
                        a.Root.rotation = Quaternion.Slerp(a.Root.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-8f * dt));
                        float alignDot = Vector3.Dot(a.Root.forward, dir);
                        float alignGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.90f, alignDot));
                        float arriveGate = Mathf.Clamp01((d - 1.9f) / 1.1f);
                        float maxFollowSpd = a.IsGalloping ? 4.4f : 2.3f;
                        desiredSpd = maxFollowSpd * alignGate * arriveGate;
                    }
                    else if (d > 0.05f)
                    {
                        a.Root.rotation = Quaternion.Slerp(a.Root.rotation, Quaternion.LookRotation(toPlayer / d, Vector3.up), 1f - Mathf.Exp(-5.5f * dt));
                    }

                    a.CurrentSpeed = Mathf.Lerp(a.CurrentSpeed, desiredSpd, 1f - Mathf.Exp(-7.5f * dt));
                    bool blocked = a.CurrentSpeed > 0.1f && distSq < 32f * 32f && ((frame + i) % 3 == 0) &&
                                   IsPathBlockedByObstacle(a.Root, pos, a.Root.forward, 0.72f, 0.24f);
                    if (blocked)
                    {
                        a.CurrentSpeed = 0f;
                    }
                    else if (a.CurrentSpeed > 0.02f)
                    {
                        a.Root.position = pos + a.Root.forward * (a.CurrentSpeed * dt);
                    }

                    if (a.CurrentSpeed > 0.14f)
                    {
                        bool useGallop = a.IsGalloping && a.CurrentSpeed > 2.6f;
                        float clipRef = useGallop ? 3.8f : a.RefWalkSpeed;
                        float clipSpd = Mathf.Clamp(a.CurrentSpeed / Mathf.Max(0.5f, clipRef), 0.6f, 1.6f);
                        a.Skel?.PlayClip(useGallop ? "Gallop" : "Walk", 0.24f, clipSpd);
                    }
                    else
                    {
                        a.Skel?.PlayClip("Idle", 0.28f, 1.0f);
                    }
                    continue;
                }

                // Approaching player reactions for rigged cats & goats (cautious retreat from fast player, curious approach for goats)
                if (playerFast && distSq < 4.0f * 4.0f && distSq > 0.04f && a.ReactCooldown <= 0f && (a.Kind == "cat" || a.Kind == "goat"))
                {
                    Vector3 away = -toPlayer / Mathf.Sqrt(distSq);
                    Vector3 retreatTarget = pos + away * 2.5f;
                    Vector3 fromHome = retreatTarget - a.HomePos;
                    fromHome.y = 0f;
                    if (fromHome.sqrMagnitude > a.WanderRadius * a.WanderRadius)
                    {
                        retreatTarget = a.HomePos + fromHome.normalized * a.WanderRadius;
                    }
                    retreatTarget.y = a.HomePos.y;
                    a.TargetPos = retreatTarget;
                    a.Walking = true;
                    a.Sitting = false;
                    a.Retreating = true;
                    a.StateTimer = 1.6f;
                    a.ReactCooldown = 3.5f;
                }
                else if (!playerFast && !a.Walking && a.Kind == "goat" && a.ReactCooldown <= 0f &&
                         distSq > 2.3f * 2.3f && distSq < 5.0f * 5.0f)
                {
                    float d = Mathf.Sqrt(distSq);
                    Vector3 approach = playerPos - (toPlayer / d) * 1.9f;
                    Vector3 fromHome = approach - a.HomePos;
                    fromHome.y = 0f;
                    if (fromHome.sqrMagnitude > a.WanderRadius * a.WanderRadius)
                    {
                        approach = a.HomePos + fromHome.normalized * a.WanderRadius;
                    }
                    approach.y = a.HomePos.y;
                    if ((approach - pos).sqrMagnitude > 0.64f)
                    {
                        a.TargetPos = approach;
                        a.Walking = true;
                        a.Retreating = false;
                        a.StateTimer = 2.8f;
                        a.ReactCooldown = 12f;
                    }
                }

                a.StateTimer -= dt;
                if (a.Walking)
                {
                    Vector3 delta = a.TargetPos - pos;
                    delta.y = 0f;
                    float dist = delta.magnitude;
                    if (dist < 0.35f || a.StateTimer <= 0f)
                    {
                        a.Walking = false;
                        a.Retreating = false;
                        a.StateTimer = UnityEngine.Random.Range(2.4f, 6.5f);
                        if (a.Kind == "cat" && UnityEngine.Random.value < 0.55f)
                        {
                            a.Sitting = true;
                            a.Skel?.PlayClip("Sit", 0.32f, 1.0f);
                        }
                        else if (a.Kind == "goat" && UnityEngine.Random.value < 0.6f)
                        {
                            a.Skel?.PlayClip("Eating", 0.30f, 1.0f);
                        }
                        else
                        {
                            a.Sitting = false;
                            a.Skel?.PlayClip("Idle", 0.30f, 1.0f);
                        }
                    }
                    else
                    {
                        Vector3 dir = delta / dist;
                        float turnRate = a.Retreating ? 9.5f : 6.5f;
                        a.Root.rotation = Quaternion.Slerp(a.Root.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-turnRate * dt));

                        float alignDot = Vector3.Dot(a.Root.forward, dir);
                        float alignGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.90f, alignDot));
                        float arriveGate = Mathf.Clamp01((dist - 0.16f) / 0.65f);
                        float maxSpd = a.WalkSpeed * (a.Retreating ? 1.38f : 1f);
                        float desiredSpd = maxSpd * alignGate * arriveGate;
                        a.CurrentSpeed = Mathf.Lerp(a.CurrentSpeed, desiredSpd, 1f - Mathf.Exp(-7.5f * dt));

                        // Throttled short obstacle guard so wandering animals don't walk through walls/fences/benches
                        if (a.CurrentSpeed > 0.1f && distSq < 32f * 32f && ((frame + i) % 3 == 0) &&
                            IsPathBlockedByObstacle(a.Root, pos, a.Root.forward, 0.65f, 0.22f))
                        {
                            a.Walking = false;
                            a.Retreating = false;
                            a.CurrentSpeed = 0f;
                            a.StateTimer = UnityEngine.Random.Range(1.2f, 2.8f);
                            a.Skel?.PlayClip("Idle", 0.26f, 1.0f);
                            continue;
                        }

                        if (a.CurrentSpeed > 0.02f)
                        {
                            Vector3 nextPos = pos + a.Root.forward * (a.CurrentSpeed * dt);
                            if (a.Kind == "goat")
                            {
                                Vector3 offsetHome = nextPos - a.HomePos;
                                offsetHome.y = 0f;
                                if (offsetHome.sqrMagnitude > a.WanderRadius * a.WanderRadius)
                                {
                                    nextPos = a.HomePos + offsetHome.normalized * a.WanderRadius;
                                    nextPos.y = pos.y;
                                }
                            }
                            a.Root.position = nextPos;
                        }

                        if (a.CurrentSpeed > 0.12f)
                        {
                            float clipSpd = Mathf.Clamp(a.CurrentSpeed / Mathf.Max(0.4f, a.RefWalkSpeed), 0.6f, 1.6f);
                            a.Skel?.PlayClip("Walk", 0.24f, clipSpd);
                        }
                        else if (alignDot < 0.85f)
                        {
                            a.Skel?.PlayClip("Walk", 0.24f, 0.6f);
                        }
                        else
                        {
                            a.Skel?.PlayClip("Idle", 0.26f, 1.0f);
                        }
                    }
                }
                else
                {
                    a.CurrentSpeed = Mathf.Lerp(a.CurrentSpeed, 0f, 1f - Mathf.Exp(-9f * dt));
                    if (!a.Sitting && !playerFast && a.Kind == "cat" && distSq < 12.25f && distSq > 0.09f)
                    {
                        a.Root.rotation = Quaternion.Slerp(a.Root.rotation, Quaternion.LookRotation(toPlayer.normalized, Vector3.up), 1f - Mathf.Exp(-2.2f * dt));
                    }
                    if (a.StateTimer <= 0f)
                    {
                        a.Walking = true;
                        a.Sitting = false;
                        a.Retreating = false;
                        a.StateTimer = UnityEngine.Random.Range(2.2f, 5.0f);
                        Vector2 rnd = UnityEngine.Random.insideUnitCircle * a.WanderRadius;
                        a.TargetPos = a.HomePos + new Vector3(rnd.x, 0f, rnd.y);
                    }
                }
            }
        }

        private void UpdateLakeSwans(float dt)
        {
            float t = Time.time;
            for (int i = 0; i < _swans.Count; i++)
            {
                LakeSwan s = _swans[i];
                if (s.Root == null) continue;

                bool spread = Mathf.Repeat(t * 0.14f + s.Phase, 1f) < 0.18f;
                s.SpeedScale = Mathf.Lerp(s.SpeedScale, spread ? 0.52f : 1.0f, 1f - Mathf.Exp(-3.5f * dt));
                s.Angle += dt * s.Speed * s.SpeedScale;

                float bob = Mathf.Sin(s.Angle * 2.6f) * 0.018f;
                Vector3 pos = s.Center + new Vector3(Mathf.Cos(s.Angle) * s.RadiusX, bob, Mathf.Sin(s.Angle) * s.RadiusZ);

                // Smoothly deflect west of the wooden viewing pier (x = -183..-168, z = -2.1..+2.1) so swans never clip pier pilings!
                if (pos.x > -184.6f)
                {
                    float pierZone = 1f - Mathf.SmoothStep(2.2f, 4.6f, Mathf.Abs(pos.z));
                    pos.x = Mathf.Lerp(pos.x, -184.6f, pierZone);
                }

                if (!s.Initialized)
                {
                    s.Initialized = true;
                    s.Root.position = pos;
                    Vector3 initTan = new Vector3(-Mathf.Sin(s.Angle) * s.RadiusX, 0f, Mathf.Cos(s.Angle) * s.RadiusZ);
                    if (initTan.sqrMagnitude > 0.0001f)
                    {
                        s.Root.rotation = Quaternion.LookRotation(initTan.normalized, Vector3.up);
                    }
                }
                else
                {
                    Vector3 prevPos = s.Root.position;
                    s.Root.position = pos;
                    Vector3 vel = pos - prevPos;
                    vel.y = 0f;
                    float spd = vel.magnitude / dt;
                    if (spd > 0.02f)
                    {
                        float buoyPitch = Mathf.Cos(s.Angle * 2.6f) * 2.2f;
                        float buoyRoll = Mathf.Sin(s.Angle * 1.9f) * 2.8f + 2.5f;
                        Quaternion targetRot = Quaternion.LookRotation(vel.normalized, Vector3.up) *
                                               Quaternion.Euler(buoyPitch, 0f, buoyRoll);
                        s.Root.rotation = Quaternion.Slerp(s.Root.rotation, targetRot, 1f - Mathf.Exp(-5.5f * dt));
                    }

                    float swimClipSpd = Mathf.Clamp(spd / 2.2f, 0.7f, 1.35f);
                    s.Skel?.PlayClip(spread ? "WingSpread" : "Swim", 0.35f, spread ? 1.0f : swimClipSpd);
                }
            }
        }

        private static bool IsPathBlockedByObstacle(Transform selfRoot, Vector3 origin, Vector3 dir, float distance, float radius)
        {
            float castY = radius >= 0.20f ? 0.62f : (radius + 0.14f);
            float castRadius = radius >= 0.20f ? 0.28f : radius;
            Vector3 castOrigin = origin + Vector3.up * castY;
            int hits = Physics.SphereCastNonAlloc(castOrigin, castRadius, dir, AnimalObstacleHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                RaycastHit h = AnimalObstacleHits[i];
                if (h.collider == null) continue;
                if (h.collider.transform.IsChildOf(selfRoot)) continue;
                if (h.normal.y > 0.65f) continue; // Ignore ground/ramps
                if (h.collider.GetComponentInParent<PlayerFootController>() != null) continue;
                return true;
            }
            return false;
        }

        private void HandleLivingAnimalInteracted(InteractableTrigger trig)
        {
            if (trig == null || string.IsNullOrEmpty(trig.InteractableId)) return;

            // Interactive Smart Conversation for ALL runtime LivingNeighborhoodPopulator NPCs!
            if (trig.InteractableId.StartsWith("living_npc_", StringComparison.Ordinal))
            {
                Vector3 playerPos = PlayerModeController.Instance != null
                    ? PlayerModeController.Instance.ActiveWorldPosition
                    : Vector3.zero;
                for (int i = 0; i < _npcs.Count; i++)
                {
                    LivingNpc npc = _npcs[i];
                    if (!string.Equals(npc.InteractableId, trig.InteractableId, StringComparison.Ordinal)) continue;
                    npc.PauseTimer = Mathf.Max(npc.PauseTimer, 4.5f);
                    string roleTr = npc.Role == NpcRole.Jogger ? "Mahalle Koşucusu"
                        : npc.Role == NpcRole.Spectator ? "Spor İzleyicisi"
                        : npc.Role == NpcRole.Vendor ? "Mahalle Esnafı"
                        : npc.Role == NpcRole.SeatedTeaGuest ? "Çay Bahçesi Misafiri"
                        : "Hamidiye Sakini";
                    string roleEn = npc.Role == NpcRole.Jogger ? "Neighborhood Jogger"
                        : npc.Role == NpcRole.Spectator ? "Sports Fan"
                        : npc.Role == NpcRole.Vendor ? "Street Vendor"
                        : npc.Role == NpcRole.SeatedTeaGuest ? "Tea Garden Guest"
                        : "Hamidiye Neighbor";
                    SmartNpcInteractionPanel.EnsureInstance().OpenForLivingNpc(
                        npc.Root.transform, npc.Humanoid, npc.DisplayName, roleTr, roleEn, npc.Lines, playerPos);
                    return;
                }
            }

            for (int i = 0; i < _animals.Count; i++)
            {
                LivingAnimal a = _animals[i];
                if (!string.Equals(a.Id, trig.InteractableId, StringComparison.Ordinal)) continue;

                a.HappyTimer = 2.6f;
                GameFeelAudio.PlayUiClick();
                GameFeelAudio.SpawnBurst(a.Root.position + Vector3.up * 0.65f, new Color(1.0f, 0.62f, 0.72f), count: 10, spread: 0.45f);

                bool canReward = Time.time - a.LastRewardTime >= 25f;
                if (canReward) a.LastRewardTime = Time.time;

                if (a.Kind == "cat")
                {
                    if (canReward) PlayerNeedsController.Instance?.RestoreEnergy(12f);
                    PlayerModeController.Instance?.RaiseStatusMessage(canReward
                        ? $"{a.DisplayName} mırıldanarak bacağına sürtündü! (+12 Enerji)"
                        : $"{a.DisplayName} keyifle mırıldanıyor!");
                }
                else if (a.Kind == "dog")
                {
                    a.FollowPlayerTimer = 25f;
                    if (canReward) PlayerNeedsController.Instance?.RestoreEnergy(15f);
                    PlayerModeController.Instance?.RaiseStatusMessage($"{a.DisplayName} kuyruk sallayarak peşine takıldı! (+15 Enerji)");
                }
                else if (a.Kind == "goat")
                {
                    if (canReward)
                    {
                        SaveManager.Instance?.AddMoney(45);
                        GameFeelAudio.PlayCoinChime();
                        NeighborhoodReputation.AddReputation(10, $"Aydos Keçi Peyniri (+45 ₺)");
                        PlayerModeController.Instance?.RaiseStatusMessage($"{a.DisplayName} taze keçi sütü & köy peyniri verdi! (+45 ₺)");
                    }
                    else
                    {
                        PlayerModeController.Instance?.RaiseStatusMessage($"{a.DisplayName} neşeyle zıplıyor!");
                    }
                }
                return;
            }
        }
    }
}
