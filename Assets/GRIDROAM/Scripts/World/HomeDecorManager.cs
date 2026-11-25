using System;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    public enum HomeDecorUpgradeId
    {
        LuxurySofa,
        MediaConsole,
        AquariumAndPlant,
        BackyardMangal,
        GardenLanterns,
        FarmLounge
    }

    public struct HomeDecorCatalogItem
    {
        public HomeDecorUpgradeId Id;
        public string NameTr;
        public string NameEn;
        public string DescTr;
        public string DescEn;
        public int CostTl;
    }

    /// <summary>
    /// Phase 3C: No:48 Hero House Furniture, Animated Aquarium, Backyard Mangal & Real Estate Upgrades!
    /// - Spawns a Home Decor Catalog Stand outside Deniz's No:48 house (-64.2, 0, 5.2)
    /// - Instantiates real commercial-safe Blender FBX furniture & backyard pergola sets when unlocked
    /// - Drives continuous swimming animation for the tropical aquarium fish (FishSwimmerA & FishSwimmerB)
    /// - Makes the Backyard Mangal & Brass Semaver interactive (grill caught lake fish or brew tea for full Energy & Hunger!)
    /// </summary>
    public sealed class HomeDecorManager : MonoBehaviour
    {
        public static HomeDecorManager Instance { get; private set; }
        public static readonly Vector3 DecorBoardWorldPos = new Vector3(-64.2f, 0f, 5.2f);
        public static readonly Vector3 BackyardMangalWorldPos = new Vector3(-76.2f, 0f, 10.6f);

        public static readonly HomeDecorCatalogItem[] Catalog =
        {
            new HomeDecorCatalogItem
            {
                Id = HomeDecorUpgradeId.LuxurySofa,
                NameTr = "Lüks L-Koltuk & Anadolu Kilimi",
                NameEn = "Velvet L-Sofa & Anatolian Kilim",
                DescTr = "No:48 salonuna modern turkuaz köşe koltuk, meşe sehpa ve dokuma kilim.",
                DescEn = "Modern teal velvet corner sofa, oak table & woven Turkish rug for No:48.",
                CostTl = 240
            },
            new HomeDecorCatalogItem
            {
                Id = HomeDecorUpgradeId.MediaConsole,
                NameTr = "65\" 4K Akıllı TV & Oyun Konsolu",
                NameEn = "65\" 4K Smart TV & Game Console",
                DescTr = "Salon eğlence köşesi, konsol, ses sistemi ve sıcak lambader.",
                DescEn = "Living room 4K Smart TV, next-gen console, speakers & warm floor lamp.",
                CostTl = 290
            },
            new HomeDecorCatalogItem
            {
                Id = HomeDecorUpgradeId.AquariumAndPlant,
                NameTr = "Tropikal Akvaryum & Zeytin Ağacı",
                NameEn = "Tropical Aquarium & Olive Tree",
                DescTr = "İçinde canlı yüzen balıklı ışıklı akvaryum ve terakota saksı zeytin ağacı.",
                DescEn = "Illuminated aquarium with animated swimming fish + potted olive tree.",
                CostTl = 210
            },
            new HomeDecorCatalogItem
            {
                Id = HomeDecorUpgradeId.BackyardMangal,
                NameTr = "Bahçe Çardağı, Mangal & Semaver",
                NameEn = "Backyard Pergola, Mangal & Semaver",
                DescTr = "No:48 yan bahçeye ahşap çardak! Tuttuğun balıkları pişir, çay demle.",
                DescEn = "Timber garden pergola with charcoal grill! Cook caught fish & brew tea.",
                CostTl = 260
            },
            new HomeDecorCatalogItem
            {
                Id = HomeDecorUpgradeId.GardenLanterns,
                NameTr = "No:48 Gece Bahçe Fenerleri",
                NameEn = "No:48 Warm Garden Lanterns",
                DescTr = "Evini ve bostanını akşamları ışıl ışıl aydınlatan bahçe fenerleri.",
                DescEn = "Cozy warm evening garden lanterns illuminating No:48 & your Bostan.",
                CostTl = 150
            },
            new HomeDecorCatalogItem
            {
                Id = HomeDecorUpgradeId.FarmLounge,
                NameTr = "Aydos Çiftlik Evi Veranda Seti",
                NameEn = "Aydos Farm Gazebo Lounge Set",
                DescTr = "Aydos Çiftliği kamelyasını lüks kır evi dinlenme alanına dönüştürür.",
                DescEn = "Upgrades the Aydos Farm gazebo with luxury lounge seating & tea urn.",
                CostTl = 340
            }
        };

        private GameObject _sofaObj;
        private GameObject _mediaObj;
        private GameObject _aquariumObj;
        private Transform _fishA;
        private Transform _fishB;
        private GameObject _mangalObj;
        private GameObject _lanternsObj;
        private GameObject _farmLoungeObj;

        public event Action OnDecorChanged;

        private static bool IsEn =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_HomeDecorManager");
            go.AddComponent<HomeDecorManager>();
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
            BuildDecorCatalogStand();
            SyncAllUnlockedDecor(fx: false);

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }
        }

        private readonly System.Collections.Generic.List<Material> _runtimeMats =
            new System.Collections.Generic.List<Material>(8);

        private void OnDestroy()
        {
            for (int i = 0; i < _runtimeMats.Count; i++)
            {
                if (_runtimeMats[i] != null) Destroy(_runtimeMats[i]);
            }
            _runtimeMats.Clear();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Continuously animate the swimming tropical fish inside the No:48 Aquarium!
            if (_fishA != null)
            {
                float t = Time.time;
                Vector3 basePos = _fishA.localPosition;
                basePos.x = -0.42f + Mathf.Sin(t * 1.15f) * 0.34f;
                basePos.y = 1.16f + Mathf.Sin(t * 2.4f) * 0.05f;
                _fishA.localPosition = basePos;
                _fishA.localRotation = Quaternion.Euler(0f, Mathf.Cos(t * 1.15f) >= 0f ? 0f : 180f, 0f);
            }
            if (_fishB != null)
            {
                float t = Time.time + 2.1f;
                Vector3 basePos = _fishB.localPosition;
                basePos.x = -0.42f + Mathf.Sin(t * 1.45f) * 0.32f;
                basePos.y = 1.02f + Mathf.Cos(t * 2.1f) * 0.05f;
                _fishB.localPosition = basePos;
                _fishB.localRotation = Quaternion.Euler(0f, Mathf.Cos(t * 1.45f) >= 0f ? 0f : 180f, 0f);
            }
        }

        public static bool IsUnlocked(HomeDecorUpgradeId id)
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return false;
            return id switch
            {
                HomeDecorUpgradeId.LuxurySofa => d.decorLuxurySofa,
                HomeDecorUpgradeId.MediaConsole => d.decorMediaConsole,
                HomeDecorUpgradeId.AquariumAndPlant => d.decorAquarium,
                HomeDecorUpgradeId.BackyardMangal => d.decorBackyardMangal,
                HomeDecorUpgradeId.GardenLanterns => d.decorGardenLanterns,
                HomeDecorUpgradeId.FarmLounge => d.decorFarmLounge,
                _ => false
            };
        }

        public bool TryBuyUpgrade(HomeDecorUpgradeId id, out string feedback)
        {
            feedback = string.Empty;
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return false;

            if (IsUnlocked(id))
            {
                feedback = IsEn ? "Already installed at your property!" : "Bu dekorasyon zaten evinde kurulu!";
                return false;
            }

            HomeDecorCatalogItem item = Catalog[(int)id];
            if (!SaveManager.Instance.TrySpend(item.CostTl))
            {
                feedback = IsEn ? $"Need {item.CostTl} ₺ for {item.NameEn}." : $"{item.NameTr} için {item.CostTl} ₺ gerekiyor.";
                return false;
            }

            switch (id)
            {
                case HomeDecorUpgradeId.LuxurySofa: d.decorLuxurySofa = true; break;
                case HomeDecorUpgradeId.MediaConsole: d.decorMediaConsole = true; break;
                case HomeDecorUpgradeId.AquariumAndPlant: d.decorAquarium = true; break;
                case HomeDecorUpgradeId.BackyardMangal: d.decorBackyardMangal = true; break;
                case HomeDecorUpgradeId.GardenLanterns: d.decorGardenLanterns = true; break;
                case HomeDecorUpgradeId.FarmLounge: d.decorFarmLounge = true; break;
            }
            SaveManager.Instance.Save();
            NeighborhoodReputation.AddReputation(10, IsEn ? "Home Upgrade" : "Ev Dekorasyonu");
            GameFeelAudio.PlayCoinChime();

            SyncAllUnlockedDecor(fx: true);
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }

            feedback = IsEn ? $"Installed {item.NameEn}!" : $"{item.NameTr} evine yerleştirildi!";
            OnDecorChanged?.Invoke();
            return true;
        }

        public void InteractWithBackyardMangal(PlayerModeController player)
        {
            if (player == null) return;
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            PlayerActionAnimator.Play(player, _mangalObj != null ? _mangalObj.transform : transform, PlayerActionKind.GrillMangal);
            GameFeelAudio.SpawnBurst(BackyardMangalWorldPos + new Vector3(-1.1f, 1.3f, 0.4f), new Color(1.0f, 0.56f, 0.16f), count: 16, spread: 0.55f);
            GameFeelAudio.PlayCoinChime();

            if (PlayerNeedsController.Instance != null)
            {
                PlayerNeedsController.Instance.RestoreEnergy(100f);
            }

            if (d != null && d.invFishCount > 0)
            {
                int avgVal = Mathf.Max(40, d.invFishTotalValue / d.invFishCount);
                d.invFishCount = Mathf.Max(0, d.invFishCount - 1);
                d.invFishTotalValue = Mathf.Max(0, d.invFishTotalValue - avgVal);
                SaveManager.Instance.Save();
                player.RaiseStatusMessage(IsEn
                    ? "Grilled fresh Sultanbeyli Lake fish on the Mangal + hot Semaver tea! Energy & Hunger 100%!"
                    : "Göletten tuttuğun taze balığı mangalda pişirip semaver çayı içtin! Enerji & Tokluk %100!");
            }
            else
            {
                player.RaiseStatusMessage(IsEn
                    ? "Brewed fragrant Turkish tea in the backyard Brass Semaver! Energy & Hunger 100%!"
                    : "Bahçedeki pirinç semaverden mis gibi çay demledin! Enerji & Tokluk %100!");
            }
        }

        private void SyncAllUnlockedDecor(bool fx)
        {
            // Inside No:48 Hero House (world center ~ -68.0, 0.10, 12.2)
            if (IsUnlocked(HomeDecorUpgradeId.LuxurySofa) && _sofaObj == null)
            {
                _sofaObj = OurWorldModelLibrary.InstantiateModel("OW_HomeLuxurySofaSet", transform,
                    new Vector3(-69.2f, 0.11f, 12.8f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 0.90f);
                if (fx && _sofaObj != null) GameFeelAudio.SpawnBurst(_sofaObj.transform.position + Vector3.up, new Color(0.16f, 0.80f, 0.76f), 14, 0.8f);
            }

            if (IsUnlocked(HomeDecorUpgradeId.MediaConsole) && _mediaObj == null)
            {
                _mediaObj = OurWorldModelLibrary.InstantiateModel("OW_HomeMediaAndConsole", transform,
                    new Vector3(-66.0f, 0.11f, 14.4f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 0.88f);
                if (fx && _mediaObj != null) GameFeelAudio.SpawnBurst(_mediaObj.transform.position + Vector3.up, new Color(0.28f, 0.72f, 0.96f), 14, 0.8f);
            }

            if (IsUnlocked(HomeDecorUpgradeId.AquariumAndPlant) && _aquariumObj == null)
            {
                _aquariumObj = OurWorldModelLibrary.InstantiateModel("OW_HomeAquariumAndPlant", transform,
                    new Vector3(-64.9f, 0.11f, 10.6f), Quaternion.Euler(0f, -90f, 0f), Vector3.one * 0.90f);
                if (_aquariumObj != null)
                {
                    _fishA = OurWorldModelLibrary.FindPart(_aquariumObj.transform, "FishSwimmerA");
                    _fishB = OurWorldModelLibrary.FindPart(_aquariumObj.transform, "FishSwimmerB");
                    if (fx) GameFeelAudio.SpawnBurst(_aquariumObj.transform.position + Vector3.up, new Color(0.20f, 0.88f, 0.98f), 14, 0.7f);
                }
            }

            if (IsUnlocked(HomeDecorUpgradeId.BackyardMangal) && _mangalObj == null)
            {
                _mangalObj = OurWorldModelLibrary.InstantiateModel("OW_HomeBackyardMangal", transform,
                    BackyardMangalWorldPos, Quaternion.Euler(0f, 180f, 0f), Vector3.one);
                if (_mangalObj != null)
                {
                    InteractableTrigger mTrig = _mangalObj.AddComponent<InteractableTrigger>();
                    mTrig.Configure(InteractableKind.HomeMangal, "no48_backyard_mangal", "ui.btn.talk", 3.6f);
                    mTrig.SetCustomPrompt(IsEn ? "Grill Fish / Brew Tea · Mangal" : "Balık Pişir / Semaver Çay · Mangal");
                    if (fx) GameFeelAudio.SpawnBurst(BackyardMangalWorldPos + Vector3.up * 1.4f, new Color(1.0f, 0.62f, 0.18f), 18, 1.0f);
                }
            }

            if (IsUnlocked(HomeDecorUpgradeId.GardenLanterns) && _lanternsObj == null)
            {
                _lanternsObj = new GameObject("No48_WarmGardenLanterns");
                _lanternsObj.transform.SetParent(transform, false);
                Vector3[] posts =
                {
                    new Vector3(-73.0f, 0f, 5.4f),
                    new Vector3(-63.2f, 0f, 5.4f),
                    new Vector3(-58.8f, 0f, 6.2f),
                    new Vector3(-51.6f, 0f, 6.2f)
                };
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                Material postMat = new Material(lit);
                _runtimeMats.Add(postMat);
                if (postMat.HasProperty("_BaseColor")) postMat.SetColor("_BaseColor", new Color(0.20f, 0.18f, 0.17f));
                Material bulbMat = new Material(lit);
                _runtimeMats.Add(bulbMat);
                if (bulbMat.HasProperty("_BaseColor")) bulbMat.SetColor("_BaseColor", new Color(1.0f, 0.90f, 0.65f));

                for (int i = 0; i < posts.Length; i++)
                {
                    GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    p.transform.SetParent(_lanternsObj.transform, false);
                    p.transform.position = posts[i] + new Vector3(0f, 1.35f, 0f);
                    p.transform.localScale = new Vector3(0.12f, 1.35f, 0.12f);
                    Destroy(p.GetComponent<Collider>());
                    p.GetComponent<MeshRenderer>().sharedMaterial = postMat;

                    GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    b.transform.SetParent(_lanternsObj.transform, false);
                    b.transform.position = posts[i] + new Vector3(0f, 2.82f, 0f);
                    b.transform.localScale = Vector3.one * 0.28f;
                    Destroy(b.GetComponent<Collider>());
                    MeshRenderer mr = b.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = bulbMat;
                    Light pl = b.AddComponent<Light>();
                    pl.type = LightType.Point;
                    pl.color = new Color(1.0f, 0.84f, 0.48f);
                    pl.range = 18f;
                    pl.intensity = 20f;
                    pl.shadows = LightShadows.None;
                    pl.enabled = false;
                    NightLight nl = b.AddComponent<NightLight>();
                    nl.Configure(pl, new[] { mr }, new Color(1.0f, 0.86f, 0.50f) * 3.6f);
                    if (NeighborhoodNightLighting.Instance != null)
                    {
                        NeighborhoodNightLighting.Instance.RegisterDynamicOutdoorLight(nl);
                    }
                }
            }

            if (IsUnlocked(HomeDecorUpgradeId.FarmLounge) && _farmLoungeObj == null)
            {
                _farmLoungeObj = OurWorldModelLibrary.InstantiateModel("OW_HomeLuxurySofaSet", transform,
                    new Vector3(172.0f, 0.22f, -5.0f), Quaternion.Euler(0f, 90f, 0f), Vector3.one * 0.85f);
                if (fx && _farmLoungeObj != null) GameFeelAudio.SpawnBurst(_farmLoungeObj.transform.position + Vector3.up, new Color(0.96f, 0.78f, 0.28f), 16, 0.9f);
            }
        }

        private void BuildDecorCatalogStand()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material woodMat = new Material(lit);
            _runtimeMats.Add(woodMat);
            if (woodMat.HasProperty("_BaseColor")) woodMat.SetColor("_BaseColor", new Color(0.42f, 0.26f, 0.14f));
            Material boardMat = new Material(lit);
            _runtimeMats.Add(boardMat);
            if (boardMat.HasProperty("_BaseColor")) boardMat.SetColor("_BaseColor", new Color(0.12f, 0.46f, 0.48f));

            GameObject stand = new GameObject("No48_HomeDecorCatalogBoard");
            stand.transform.SetParent(transform, false);
            stand.transform.position = DecorBoardWorldPos;

            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.transform.SetParent(stand.transform, false);
            post.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            post.transform.localScale = new Vector3(0.14f, 0.95f, 0.14f);
            Destroy(post.GetComponent<Collider>());
            post.GetComponent<MeshRenderer>().sharedMaterial = woodMat;

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.transform.SetParent(stand.transform, false);
            panel.transform.localPosition = new Vector3(0f, 1.78f, 0f);
            panel.transform.localScale = new Vector3(1.85f, 0.92f, 0.10f);
            Destroy(panel.GetComponent<Collider>());
            panel.GetComponent<MeshRenderer>().sharedMaterial = boardMat;

            GameObject txt = new GameObject("DecorText");
            txt.transform.SetParent(stand.transform, false);
            txt.transform.localPosition = new Vector3(0f, 1.78f, -0.07f);
            TextMesh tm = txt.AddComponent<TextMesh>();
            tm.text = "EV DEKORASYON\n& EMLAK (NO:48)";
            tm.characterSize = 0.045f;
            tm.fontSize = 38;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.95f, 0.80f);

            InteractableTrigger trig = stand.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.HomeDecorBoard, "no48_home_decor_board", "ui.btn.talk", 3.4f);
            trig.SetCustomPrompt(IsEn ? "Home Decor & Upgrades · No:48" : "Ev Dekorasyonu & Emlak · No:48");
        }
    }
}
