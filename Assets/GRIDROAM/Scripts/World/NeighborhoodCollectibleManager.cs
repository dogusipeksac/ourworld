using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Places 12 collectible golden "Hamidiye Hatıra Kartpostalı" (Neighborhood Memories)
    /// across all avenues and landmarks. Collecting each reveals a nostalgic local story line,
    /// awards +100 ₺ & Reputation XP, and collecting all 12 grants a +1500 ₺ Master Bonus!
    /// </summary>
    public sealed class NeighborhoodCollectibleManager : MonoBehaviour
    {
        public struct MemoryDef
        {
            public string Id;
            public Vector3 Position;
            public string CaptionTr;
            public string CaptionEn;
        }

        public static readonly MemoryDef[] Memories =
        {
            new MemoryDef { Id = "mem_01_selman48",   Position = new Vector3(-63.2f, 0.95f,   7.5f), CaptionTr = "1992 · No:48 Bahçesi: Dedemin diktiği asma çardağı hâlâ gölge veriyor.", CaptionEn = "1992 · No:48 Garden: Grandfather's grapevine pergola still casts cool shade." },
            new MemoryDef { Id = "mem_02_bakkal",     Position = new Vector3( 11.4f, 0.95f,   7.6f), CaptionTr = "1996 · Selman Bakkal: Çocukken veresiye defterine gazoz yazdırırdık.", CaptionEn = "1996 · Selman Market: As kids we'd put cold soda on the corner-shop tab." },
            new MemoryDef { Id = "mem_03_gursu",      Position = new Vector3(-41.0f, 0.95f, -24.5f), CaptionTr = "2001 · Gürsu Sahası: Mahalle turnuvasının tek golü 90. dakikada atılmıştı.", CaptionEn = "2001 · Gürsu Pitch: The winning neighborhood cup goal came in the 90th minute." },
            new MemoryDef { Id = "mem_04_pool",       Position = new Vector3( 16.0f, 2.25f, -46.2f), CaptionTr = "2005 · Yüzme Havuzu: Temmuz sıcağında bütün çocuklar atlama taşında sıraya girerdi.", CaptionEn = "2005 · Swimming Pool: In July heat every kid lined up on the diving blocks." },
            new MemoryDef { Id = "mem_05_camii",      Position = new Vector3( 82.0f, 0.95f, -24.0f), CaptionTr = "2007 · Hz. Osman Camii: Bayram sabahları avluda şeker dağıtılırdı.", CaptionEn = "2007 · Hz. Osman Mosque: Eid mornings always began with sweets in the courtyard." },
            new MemoryDef { Id = "mem_06_no66",       Position = new Vector3( 52.8f, 0.95f,   6.2f), CaptionTr = "2009 · Nutuk Köşesi: Sarı evin balkonundan akşamları çay kokusu yayılırdı.", CaptionEn = "2009 · Nutuk Corner: Evening tea aroma drifted from the yellow house's balcony." },
            new MemoryDef { Id = "mem_07_park",       Position = new Vector3(-20.0f, 0.95f,  70.0f), CaptionTr = "2011 · Hamidiye Parkı: Çay bahçesinde tavla sesleri geceye kadar sürerdi.", CaptionEn = "2011 · Hamidiye Park: Backgammon tiles clicked in the tea garden past dusk." },
            new MemoryDef { Id = "mem_08_saatkulesi", Position = new Vector3(  0.0f, 1.15f, 145.6f), CaptionTr = "2014 · Saat Kulesi: Sultanbeyli'nin bütün yolları burada buluşur.", CaptionEn = "2014 · Clock Tower: Every avenue in Sultanbeyli meets right under this spire." },
            new MemoryDef { Id = "mem_09_ataturk",    Position = new Vector3( 44.0f, 0.95f,  90.2f), CaptionTr = "2016 · Atatürk Caddesi: Çam ağaçlarının arasından serin Aydos rüzgârı eser.", CaptionEn = "2016 · Atatürk Ave: Cool Aydos forest breeze sweeps past the pines." },
            new MemoryDef { Id = "mem_10_eyyubi",     Position = new Vector3(-152.0f,0.95f, -21.5f), CaptionTr = "2018 · 53 Eyyubi Büfe: Sabah işe giden herkesin ilk durağı sıcacık simit.", CaptionEn = "2018 · 53 Eyyubi Market: Warm morning simit for everyone heading to work." },
            new MemoryDef { Id = "mem_11_aziziye",    Position = new Vector3( -8.5f, 0.95f, -119.5f),CaptionTr = "2020 · İTO İlkokulu: Zilin çalmasıyla Aziziye Caddesi kahkahayla dolar.", CaptionEn = "2020 · Primary School: The afternoon bell fills Aziziye Ave with laughter." },
            new MemoryDef { Id = "mem_12_zubeyde",    Position = new Vector3(  4.5f, 0.95f, -152.0f),CaptionTr = "2024 · Zübeyde Hanım: Mavi balkonlu evler ve çamlar — Hamidiye'nin huzuru.", CaptionEn = "2024 · Zübeyde Hanım: Sky-blue balconies and pines — peace of Hamidiye." }
        };

        public static int TotalCount => Memories.Length;

        public static int CollectedCount =>
            SaveManager.Instance != null && SaveManager.Instance.Data != null && SaveManager.Instance.Data.collectedMemoryIds != null
                ? SaveManager.Instance.Data.collectedMemoryIds.Count
                : 0;

        private struct SpawnedMemory
        {
            public MemoryDef Def;
            public Transform Visual;
            public GameObject Root;
            public float BaseY;
        }

        private readonly List<SpawnedMemory> _active = new List<SpawnedMemory>();
        private Material _goldMat;
        private Material _cardMat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            if (FindAnyObjectByType<NeighborhoodCollectibleManager>() != null) return;
            GameObject go = new GameObject("Hamidiye_MemoryCollectibles");
            go.AddComponent<NeighborhoodCollectibleManager>();
        }

        private void Start()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _goldMat = new Material(sh);
            if (_goldMat.HasProperty("_BaseColor")) _goldMat.SetColor("_BaseColor", new Color(1.0f, 0.80f, 0.22f));
            if (_goldMat.HasProperty("_Metallic")) _goldMat.SetFloat("_Metallic", 0.82f);
            if (_goldMat.HasProperty("_Smoothness")) _goldMat.SetFloat("_Smoothness", 0.88f);
            if (_goldMat.HasProperty("_EmissionColor"))
            {
                _goldMat.EnableKeyword("_EMISSION");
                _goldMat.SetColor("_EmissionColor", new Color(0.95f, 0.72f, 0.16f) * 0.75f);
            }

            _cardMat = new Material(sh);
            if (_cardMat.HasProperty("_BaseColor")) _cardMat.SetColor("_BaseColor", new Color(0.96f, 0.93f, 0.84f));
            if (_cardMat.HasProperty("_EmissionColor"))
            {
                _cardMat.EnableKeyword("_EMISSION");
                _cardMat.SetColor("_EmissionColor", new Color(0.45f, 0.38f, 0.22f));
            }

            List<string> collected = SaveManager.Instance != null && SaveManager.Instance.Data != null
                ? SaveManager.Instance.Data.collectedMemoryIds
                : null;

            for (int i = 0; i < Memories.Length; i++)
            {
                if (collected != null && collected.Contains(Memories[i].Id)) continue;
                SpawnMemoryVisual(Memories[i]);
            }
        }

        private void OnDestroy()
        {
            if (_goldMat != null) Destroy(_goldMat);
            if (_cardMat != null) Destroy(_cardMat);
        }

        private void SpawnMemoryVisual(MemoryDef def)
        {
            GameObject root = new GameObject($"Memory_{def.Id}");
            root.transform.SetParent(transform, false);
            root.transform.position = def.Position;

            GameObject pivot = new GameObject("SpinPivot");
            pivot.transform.SetParent(root.transform, false);

            // Golden framed postcard silhouette
            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "GoldFrame";
            frame.transform.SetParent(pivot.transform, false);
            frame.transform.localScale = new Vector3(0.46f, 0.34f, 0.05f);
            Destroy(frame.GetComponent<Collider>());
            Renderer fr = frame.GetComponent<Renderer>();
            if (fr != null) { fr.sharedMaterial = _goldMat; fr.shadowCastingMode = ShadowCastingMode.Off; }

            GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            inner.name = "CardInner";
            inner.transform.SetParent(pivot.transform, false);
            inner.transform.localScale = new Vector3(0.36f, 0.24f, 0.062f);
            Destroy(inner.GetComponent<Collider>());
            Renderer ir = inner.GetComponent<Renderer>();
            if (ir != null) { ir.sharedMaterial = _cardMat; ir.shadowCastingMode = ShadowCastingMode.Off; }

            _active.Add(new SpawnedMemory
            {
                Def = def,
                Visual = pivot.transform,
                Root = root,
                BaseY = def.Position.y
            });
        }

        private void Update()
        {
            if (_active.Count == 0) return;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null) return;

            Vector3 pPos = mode.ActiveWorldPosition;
            float t = Time.time;
            float collectDistSq = mode.IsDriving ? 12.25f : 4.41f; // 3.5m driving, 2.1m on foot

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                SpawnedMemory sm = _active[i];
                if (sm.Root == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                Vector3 delta = pPos - sm.Root.transform.position;
                delta.y = 0f;
                float distSq = delta.sqrMagnitude;

                // Only spin visuals within 85m for zero wasted transform work
                if (distSq < 7225f && sm.Visual != null)
                {
                    sm.Visual.localRotation = Quaternion.Euler(10f, t * 72f + i * 35f, 0f);
                    sm.Visual.localPosition = new Vector3(0f, Mathf.Sin(t * 2.4f + i) * 0.12f, 0f);
                }

                if (distSq <= collectDistSq && Mathf.Abs(pPos.y - sm.BaseY) <= 2.4f && !mode.IsModalLocked)
                {
                    CollectMemory(i);
                }
            }
        }

        private void CollectMemory(int index)
        {
            SpawnedMemory item = _active[index];
            _active.RemoveAt(index);
            Vector3 burstPos = item.Root != null ? item.Root.transform.position : Vector3.zero;
            if (item.Root != null) Destroy(item.Root);

            SaveManager sm = SaveManager.Instance;
            if (sm != null && sm.Data != null)
            {
                if (sm.Data.collectedMemoryIds == null) sm.Data.collectedMemoryIds = new List<string>();
                if (!sm.Data.collectedMemoryIds.Contains(item.Def.Id))
                {
                    sm.Data.collectedMemoryIds.Add(item.Def.Id);
                }
                sm.AddMoney(100);
                bool allDone = sm.Data.collectedMemoryIds.Count >= TotalCount;
                if (allDone)
                {
                    sm.AddMoney(1500);
                }

                bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
                string caption = isEn ? item.Def.CaptionEn : item.Def.CaptionTr;
                string header = allDone
                    ? (isEn ? $"ALL 12 MEMORIES COLLECTED! (+1600 ₺)\n{caption}" : $"12/12 HAMİDİYE HATIRASI TAMAMLANDI! (+1600 ₺)\n{caption}")
                    : (isEn ? $"Memory {sm.Data.collectedMemoryIds.Count}/{TotalCount} (+100 ₺): {caption}" : $"Hatıra {sm.Data.collectedMemoryIds.Count}/{TotalCount} (+100 ₺): {caption}");

                if (PlayerModeController.Instance != null)
                {
                    PlayerModeController.Instance.RaiseStatusMessage(header);
                }
                if (allDone) GameFeelAudio.PlayMilestoneFanfare();
                else GameFeelAudio.PlayCoinChime();

                GameFeelAudio.SpawnBurst(burstPos + Vector3.up * 0.25f, new Color(1.0f, 0.84f, 0.24f), count: 16, spread: 0.75f);
                NeighborhoodReputation.AddReputation(allDone ? 100 : 25);
            }
        }
    }
}
