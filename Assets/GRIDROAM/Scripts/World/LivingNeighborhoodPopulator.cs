using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
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
        }

        private sealed class FlockPigeon
        {
            public Transform Root;
            public SkeletalClipPlayer Skel;
            public Vector3 HomePos;
            public Vector3 TargetPos;
            public bool Flying;
            public float FlyTimer;
            public float FlyDuration;
            public float OrbitAngle;
            public float OrbitRadius;
            public float OrbitHeight;
            public float StateTimer;
            public bool Walking;
        }

        private sealed class LivingAnimal
        {
            public string Id;
            public string Kind; // "cat", "dog", "goat"
            public string DisplayName;
            public Transform Root;
            public SkeletalClipPlayer Skel;
            public Vector3 HomePos;
            public Vector3 TargetPos;
            public float WanderRadius;
            public float WalkSpeed;
            public float StateTimer;
            public float HappyTimer;
            public float FollowPlayerTimer;
            public float LastRewardTime;
            public bool Walking;
            public bool Sitting;
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
        }

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
            "Spor sahaları bugün çok canlı, maç var!"
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

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material[] shirtMats = new Material[ShirtPalette.Length];
            for (int i = 0; i < ShirtPalette.Length; i++)
            {
                Material m = new Material(lit) { color = ShirtPalette[i] };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", ShirtPalette[i]);
                shirtMats[i] = m;
                _ownedMats.Add(m);
            }

            int idx = 0;

            // A. 24 Strolling Sidewalk, Plaza, Sports & Lake Promenade NPCs!
            Vector3[][] strollRoutes =
            {
                // Selman Sokağı North & South lively foot traffic
                new[] { new Vector3(-42f, 0.06f, 8.2f), new Vector3(38f, 0.06f, 8.2f) },
                new[] { new Vector3(35f, 0.06f, 7.6f), new Vector3(-36f, 0.06f, 7.6f) },
                new[] { new Vector3(-48f, 0.06f, -7.8f), new Vector3(32f, 0.06f, -7.8f) },
                new[] { new Vector3(28f, 0.06f, -8.4f), new Vector3(-30f, 0.06f, -8.4f) },
                new[] { new Vector3(-16f, 0.06f, 9.0f), new Vector3(22f, 0.06f, 9.0f), new Vector3(22f, 0.06f, 18.0f), new Vector3(-16f, 0.06f, 18.0f) },
                new[] { new Vector3(18f, 0.06f, 14.5f), new Vector3(4f, 0.06f, 14.5f), new Vector3(4f, 0.06f, 22.0f), new Vector3(18f, 0.06f, 22.0f) },
                // Sports Complex lively walkway (Tennis -> Football -> Pool -> Basketball -> Volleyball)
                new[] { new Vector3(-46f, 0.06f, -24.5f), new Vector3(62f, 0.06f, -24.5f) },
                new[] { new Vector3(58f, 0.06f, -23.0f), new Vector3(-40f, 0.06f, -23.0f) },
                new[] { new Vector3(-24f, 0.06f, -26.0f), new Vector3(12f, 0.06f, -26.0f), new Vector3(12f, 0.06f, -12.0f), new Vector3(-24f, 0.06f, -12.0f) },
                new[] { new Vector3(28f, 0.06f, -25.0f), new Vector3(64f, 0.06f, -25.0f), new Vector3(64f, 0.06f, -16.0f), new Vector3(28f, 0.06f, -16.0f) },
                // Mosque Courtyard & Fountain Plaza
                new[] { new Vector3(44f, 0.06f, -14.0f), new Vector3(72f, 0.06f, -14.0f), new Vector3(72f, 0.06f, -24.0f), new Vector3(44f, 0.06f, -24.0f) },
                new[] { new Vector3(50f, 0.06f, -18.0f), new Vector3(66f, 0.06f, -18.0f) },
                // Park & Residential Streets
                new[] { new Vector3(-22f, 0.06f, 26f), new Vector3(16f, 0.06f, 26f), new Vector3(16f, 0.06f, 44f), new Vector3(-22f, 0.06f, 44f) },
                new[] { new Vector3(-60f, 0.06f, -4f), new Vector3(-60f, 0.06f, 28f) },
                new[] { new Vector3(42f, 0.06f, 6f), new Vector3(42f, 0.06f, 38f) },
                new[] { new Vector3(-12f, 0.06f, -58f), new Vector3(44f, 0.06f, -58f) },
                // Lake Boulevard & Sultanbeyli Göleti Promenade (strollers + joggers!)
                new[] { new Vector3(-118f, 0.06f, 5.2f), new Vector3(-164f, 0.06f, 5.2f) },
                new[] { new Vector3(-165f, 0.06f, -16f), new Vector3(-165f, 0.06f, 16f) },
                new[] { new Vector3(-163f, 0.06f, 14f), new Vector3(-163f, 0.06f, -14f) },
                new[] { new Vector3(-168f, 0.06f, -2f), new Vector3(-181f, 0.44f, -0.8f) },
                // Aydos Farm Country Road & Farmyard
                new[] { new Vector3(118f, 0.06f, 4.2f), new Vector3(156f, 0.06f, 4.2f) },
                new[] { new Vector3(154f, 0.06f, -6f), new Vector3(178f, 0.06f, -6f), new Vector3(178f, 0.06f, 10f), new Vector3(154f, 0.06f, 10f) },
                new[] { new Vector3(162f, 0.06f, -12f), new Vector3(176f, 0.06f, -12f) },
                new[] { new Vector3(-84f, 0.06f, 5.4f), new Vector3(-118f, 0.06f, 5.4f) }
            };

            for (int r = 0; r < strollRoutes.Length; r++)
            {
                bool isJogger = (r == 6 || r == 17 || r == 18 || r == 20);
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

            // C. 4 Iconic Istanbul Street Vendors (Simitçi Ahmet, Çaycı Rıza, Spor Simitçisi, Gölet Simitçisi)
            (Vector3 pos, float yaw, string name, string[] lines)[] vendors =
            {
                (new Vector3(7.9f, 0.06f, 13.8f), -90f, "Simitçi Ahmet", new[] { "Taze çıtır simit var, sıcak sıcak!", "Sabah simidi 10 TL, susamlı çıtır!" }),
                (new Vector3(-24.3f, 0.06f, -21.8f), -60f, "Simitçi Kemal", new[] { "Maç arası sıcak simit al, enerjin dolsun!", "Halı sahada gol atan simidi kapar!" }),
                (new Vector3(16.6f, 0.06f, 13.4f), 180f, "Çaycı Rıza", new[] { "Tavşan kanı ince belli çaylar taze!", "Buyur otur komşu, çayın hemen geliyor!" }),
                (new Vector3(-164.0f, 0.06f, -5.2f), -90f, "Gölet Simitçisi", new[] { "Gölet manzarasında sıcak simit gibisi yok!", "Kuğular da simit kokusuna geliyor!" })
            };
            for (int v = 0; v < vendors.Length; v++)
            {
                var vd = vendors[v];
                SpawnNpcInstance(npcRoot, template, shirtMats[idx % shirtMats.Length], vd.name, NpcRole.Vendor, new[] { vd.pos }, 0f, vd.yaw, vd.lines, idx++);
            }

            // D. 10 Seated Tea Garden & Lakeside Park Guests chatting warmly!
            (Vector3 pos, float yaw)[] teaGuests =
            {
                (new Vector3(15.38f, 0.06f, 15.2f), -90f),
                (new Vector3(19.48f, 0.06f, 15.5f), -90f),
                (new Vector3(17.38f, 0.06f, 19.2f), -90f),
                (new Vector3(-1.62f, 0.06f, -23.5f), -90f),
                (new Vector3(3.08f,  0.06f, -23.5f), -90f),
                (new Vector3(-163.6f, 0.06f, 7.2f), -90f),
                (new Vector3(-163.6f, 0.06f, 11.4f), -90f),
                (new Vector3(49.2f,  0.06f, -15.0f), -90f),
                (new Vector3(-7.0f,  0.06f, -22.5f), 45f),
                (new Vector3(168.0f, 0.06f, 5.5f), 180f)
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
                hum.SetSitting(true);
            }
            else
            {
                hum.SetLocomotion(0f, true, 0f);
            }

            _npcs.Add(new LivingNpc
            {
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
                BaseYaw = baseYaw
            });
        }

        // =====================================================================
        // 2. RIGGED WILDLIFE ECOSYSTEM (PIGEONS, CATS, DOGS, SWANS, GOATS!)
        // =====================================================================
        private void SpawnPigeonFlocks()
        {
            Transform root = new GameObject("Istanbul_Pigeon_Flocks").transform;
            root.SetParent(transform, false);

            // 3 flocks of 6 pigeons (18 total): Town Square Simit Plaza, Mosque Courtyard, Sports Fountain Plaza
            Vector3[] centers =
            {
                new Vector3(9.5f, 0.04f, 13.0f),
                new Vector3(52.0f, 0.04f, -18.5f),
                new Vector3(-7.0f, 0.04f, -20.5f)
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
                (new Vector3(13.2f, 0.04f, 13.8f), "Sarman", new Color(0.90f, 0.56f, 0.22f)),
                (new Vector3(-11.0f, 0.04f, 8.8f), "Tekir", new Color(0.58f, 0.54f, 0.48f)),
                (new Vector3(45.5f, 0.04f, -14.5f), "Pamuk", new Color(0.95f, 0.93f, 0.89f)),
                (new Vector3(-1.2f, 0.04f, -21.8f), "Kömür", new Color(0.22f, 0.23f, 0.26f)),
                (new Vector3(-28.0f, 0.04f, -23.0f), "Tarçın", new Color(0.76f, 0.42f, 0.20f)),
                (new Vector3(26.5f, 0.04f, 9.5f), "Boncuk", new Color(0.86f, 0.72f, 0.52f)),
                (new Vector3(-164.0f, 0.05f, 5.0f), "Martı", new Color(0.74f, 0.76f, 0.80f)),
                (new Vector3(158.0f, 0.04f, 4.5f), "Fındık", new Color(0.84f, 0.52f, 0.26f))
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
                trig.SetCustomPrompt($"{c.name} Kedisini Sev (+12 Enerji)");

                _animals.Add(new LivingAnimal
                {
                    Id = id,
                    Kind = "cat",
                    DisplayName = c.name,
                    Root = cat.transform,
                    Skel = skel,
                    HomePos = c.pos,
                    TargetPos = c.pos,
                    WanderRadius = 3.5f,
                    WalkSpeed = 1.05f,
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
                (new Vector3(4.2f, 0.04f, 16.5f), "Paşa"),
                (new Vector3(-16.5f, 0.04f, -22.8f), "Kral"),
                (new Vector3(38.0f, 0.04f, -23.5f), "Poyraz"),
                (new Vector3(-166.0f, 0.05f, 13.0f), "Rüzgar"),
                (new Vector3(-165.5f, 0.05f, -12.0f), "Duman"),
                (new Vector3(-28.0f, 0.04f, 10.2f), "Çakıl")
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
                trig.SetCustomPrompt($"{d.name} Köpeğini Sev (+15 Enerji · Dost Ol)");

                _animals.Add(new LivingAnimal
                {
                    Id = id,
                    Kind = "dog",
                    DisplayName = d.name,
                    Root = dog.transform,
                    Skel = skel,
                    HomePos = d.pos,
                    TargetPos = d.pos,
                    WanderRadius = 5.5f,
                    WalkSpeed = 1.85f,
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
                    Phase = i * 1.65f
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
                trig.SetCustomPrompt($"{goatNames[i]} Keçisini Sev & Peynir Al (+45 ₺)");

                _animals.Add(new LivingAnimal
                {
                    Id = id,
                    Kind = "goat",
                    DisplayName = goatNames[i],
                    Root = goat.transform,
                    Skel = skel,
                    HomePos = penCenter,
                    TargetPos = pos,
                    WanderRadius = 4.8f,
                    WalkSpeed = 1.45f,
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
            UpdateLivingAnimals(dt, playerPos);
            UpdateLakeSwans();
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
                        }
                        else
                        {
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
                    // Startled flock takeoff into a graceful circling sky arc!
                    p.Flying = true;
                    p.FlyTimer = 0f;
                    p.FlyDuration = UnityEngine.Random.Range(4.8f, 7.2f);
                    p.OrbitAngle = Mathf.Atan2(pos.z - p.HomePos.z, pos.x - p.HomePos.x);
                    p.Skel?.PlayClip("Fly", 0.12f, 1.35f);
                }

                if (p.Flying)
                {
                    p.FlyTimer += dt;
                    float u = Mathf.Clamp01(p.FlyTimer / p.FlyDuration);
                    p.OrbitAngle += dt * 1.45f;
                    float heightEnv = Mathf.Sin(u * Mathf.PI);
                    float r = Mathf.Lerp(1.6f, p.OrbitRadius, heightEnv);
                    Vector3 target = p.HomePos + new Vector3(Mathf.Cos(p.OrbitAngle) * r, heightEnv * p.OrbitHeight, Mathf.Sin(p.OrbitAngle) * r);
                    Vector3 delta = target - pos;
                    p.Root.position = Vector3.Lerp(pos, target, 1f - Mathf.Exp(-6f * dt));
                    if (delta.sqrMagnitude > 0.002f)
                    {
                        p.Root.rotation = Quaternion.Slerp(p.Root.rotation, Quaternion.LookRotation(delta.normalized, Vector3.up), 1f - Mathf.Exp(-8f * dt));
                    }
                    if (u >= 1f)
                    {
                        p.Flying = false;
                        p.Root.position = new Vector3(p.Root.position.x, p.HomePos.y, p.Root.position.z);
                        p.StateTimer = UnityEngine.Random.Range(1.5f, 4.0f);
                        p.Skel?.PlayClip("Peck", 0.22f, 1.0f);
                    }
                    continue;
                }

                p.StateTimer -= dt;
                if (p.Walking)
                {
                    Vector3 delta = p.TargetPos - pos;
                    delta.y = 0f;
                    if (delta.magnitude < 0.18f || p.StateTimer <= 0f)
                    {
                        p.Walking = false;
                        p.StateTimer = UnityEngine.Random.Range(1.8f, 4.6f);
                        p.Skel?.PlayClip("Peck", 0.22f, 1.0f);
                    }
                    else
                    {
                        Vector3 dir = delta.normalized;
                        p.Root.position = pos + dir * (0.65f * dt);
                        p.Root.rotation = Quaternion.Slerp(p.Root.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-8f * dt));
                    }
                }
                else if (p.StateTimer <= 0f)
                {
                    p.Walking = true;
                    p.StateTimer = UnityEngine.Random.Range(1.4f, 3.2f);
                    Vector2 rnd = UnityEngine.Random.insideUnitCircle * 2.8f;
                    p.TargetPos = p.HomePos + new Vector3(rnd.x, 0f, rnd.y);
                    p.Skel?.PlayClip("Walk", 0.18f, 1.1f);
                }
            }
        }

        private void UpdateLivingAnimals(float dt, Vector3 playerPos)
        {
            for (int i = 0; i < _animals.Count; i++)
            {
                LivingAnimal a = _animals[i];
                if (a.Root == null) continue;
                Vector3 pos = a.Root.position;
                float distSq = (pos - playerPos).sqrMagnitude;
                if (distSq > 64f * 64f) continue;

                if (a.HappyTimer > 0f)
                {
                    a.HappyTimer -= dt;
                    Vector3 look = playerPos - pos;
                    look.y = 0f;
                    if (look.sqrMagnitude > 0.05f)
                    {
                        a.Root.rotation = Quaternion.Slerp(a.Root.rotation, Quaternion.LookRotation(look.normalized, Vector3.up), 1f - Mathf.Exp(-7f * dt));
                    }
                    a.Skel?.PlayClip("Happy", 0.18f, 1.15f);
                    continue;
                }

                // Friendly petted Street Dog trots happily alongside Deniz for 25 seconds!
                if (a.FollowPlayerTimer > 0f && a.Kind == "dog")
                {
                    a.FollowPlayerTimer -= dt;
                    Vector3 toPlayer = playerPos - pos;
                    toPlayer.y = 0f;
                    float d = toPlayer.magnitude;
                    if (d > 2.4f && d < 28f)
                    {
                        bool run = d > 5.5f;
                        float spd = run ? 4.4f : 2.3f;
                        Vector3 dir = toPlayer / d;
                        a.Root.position = pos + dir * (spd * dt);
                        a.Root.rotation = Quaternion.Slerp(a.Root.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-8f * dt));
                        a.Skel?.PlayClip(run ? "Gallop" : "Walk", 0.22f, run ? 1.15f : 1.0f);
                    }
                    else
                    {
                        a.Skel?.PlayClip("Idle", 0.24f, 1.0f);
                    }
                    continue;
                }

                a.StateTimer -= dt;
                if (a.Walking)
                {
                    Vector3 delta = a.TargetPos - pos;
                    delta.y = 0f;
                    if (delta.magnitude < 0.35f || a.StateTimer <= 0f)
                    {
                        a.Walking = false;
                        a.StateTimer = UnityEngine.Random.Range(2.4f, 6.5f);
                        if (a.Kind == "cat" && UnityEngine.Random.value < 0.55f)
                        {
                            a.Sitting = true;
                            a.Skel?.PlayClip("Sit", 0.30f, 1.0f);
                        }
                        else if (a.Kind == "goat" && UnityEngine.Random.value < 0.6f)
                        {
                            a.Skel?.PlayClip("Eating", 0.28f, 1.0f);
                        }
                        else
                        {
                            a.Sitting = false;
                            a.Skel?.PlayClip("Idle", 0.28f, 1.0f);
                        }
                    }
                    else
                    {
                        Vector3 dir = delta.normalized;
                        a.Root.position = pos + dir * (a.WalkSpeed * dt);
                        a.Root.rotation = Quaternion.Slerp(a.Root.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-6.5f * dt));
                        a.Skel?.PlayClip("Walk", 0.22f, 1.05f);
                    }
                }
                else if (a.StateTimer <= 0f)
                {
                    a.Walking = true;
                    a.Sitting = false;
                    a.StateTimer = UnityEngine.Random.Range(2.2f, 5.0f);
                    Vector2 rnd = UnityEngine.Random.insideUnitCircle * a.WanderRadius;
                    a.TargetPos = a.HomePos + new Vector3(rnd.x, 0f, rnd.y);
                    a.Skel?.PlayClip("Walk", 0.22f, 1.05f);
                }
            }
        }

        private void UpdateLakeSwans()
        {
            float t = Time.time;
            for (int i = 0; i < _swans.Count; i++)
            {
                LakeSwan s = _swans[i];
                if (s.Root == null) continue;
                float a = t * s.Speed + s.Phase;
                float bob = Mathf.Sin(a * 2.6f) * 0.018f;
                Vector3 pos = s.Center + new Vector3(Mathf.Cos(a) * s.RadiusX, bob, Mathf.Sin(a) * s.RadiusZ);
                Vector3 next = s.Center + new Vector3(Mathf.Cos(a + 0.08f) * s.RadiusX, bob, Mathf.Sin(a + 0.08f) * s.RadiusZ);
                s.Root.position = pos;
                Vector3 dir = next - pos;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    s.Root.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
                bool spread = Mathf.Repeat(t * 0.14f + s.Phase, 1f) < 0.18f;
                s.Skel?.PlayClip(spread ? "WingSpread" : "Swim", 0.35f, 1.0f);
            }
        }

        private void HandleLivingAnimalInteracted(InteractableTrigger trig)
        {
            if (trig == null || string.IsNullOrEmpty(trig.InteractableId)) return;
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
