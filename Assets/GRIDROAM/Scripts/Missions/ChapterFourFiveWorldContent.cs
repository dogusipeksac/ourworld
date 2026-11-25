using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.Missions
{
    /// <summary>
    /// Spawns Chapter 4 ("Gece Gizemi & Spor Kupası", Missions 15-17) and Chapter 5
    /// ("Büyük Sultanbeyli Finali", Missions 18-20) NPCs, glowing story clues, brass time capsule chest,
    /// blackout repair kits, espionage evidence cases, sports tournament anchors, and 8-avenue rally checkpoints.
    /// </summary>
    public sealed class ChapterFourFiveWorldContent : MonoBehaviour
    {
        private struct PickupEntry
        {
            public GameObject go;
            public string anchorId;
            public string missionId;
        }

        private struct NpcIdleLine
        {
            public string anchorId;
            public string speakerKey;
            public string lineKey;
        }

        private readonly List<PickupEntry> _pickups = new List<PickupEntry>();
        private readonly List<NpcIdleLine> _idleLines = new List<NpcIdleLine>();
        private Transform _staging;
        private bool _subscribed;

        private static readonly Color ShirtCoachKerem = new Color(0.10f, 0.58f, 0.44f);
        private static readonly Color ShirtElectricianHasan = new Color(0.90f, 0.46f, 0.10f);
        private static readonly Color ShirtJournalistDefne = new Color(0.68f, 0.16f, 0.30f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<MissionManager>() == null) return;
            if (FindAnyObjectByType<ChapterFourFiveWorldContent>() != null) return;

            GameObject root = new GameObject("--- CHAPTER4_5_MISSION_CONTENT ---");
            root.AddComponent<ChapterFourFiveWorldContent>().SpawnAll();
        }

        private void SpawnAll()
        {
            GameObject staging = new GameObject("_Staging");
            staging.SetActive(false);
            staging.transform.SetParent(transform, false);
            _staging = staging.transform;

            GameObject maleTpl = FindAnchorObject("npc_mechanic") ?? FindAnchorObject("npc_harbor_receiver");
            GameObject femaleTpl = FindAnchorObject("npc_harbor_receiver") ?? FindAnchorObject("npc_elena_neighborhood") ?? maleTpl;

            // --- Chapter 4 & 5 Memorable Story NPCs ---
            SpawnNpc("npc_coach_kerem", "NPC_AntrenorKerem_SporKompleksi", maleTpl, new Vector3(-26.5f, 0.06f, -27.5f), 0f, 1.02f, ShirtCoachKerem);
            SpawnNpc("npc_electrician_hasan", "NPC_ElektrikUstasiHasan_Mursit", maleTpl, new Vector3(24.0f, 0.06f, 40.4f), 180f, 1.0f, ShirtElectricianHasan);
            SpawnWorkBeaconLight(new Vector3(24.8f, 0.9f, 40.8f));
            SpawnNpc("npc_journalist_defne", "NPC_GazeteciDefne_FatihBulvari", femaleTpl, new Vector3(-14.5f, 0.16f, 132.4f), 0f, 0.99f, ShirtJournalistDefne);

            _idleLines.Add(new NpcIdleLine { anchorId = "npc_coach_kerem", speakerKey = "npc.kerem.name", lineKey = "npc.kerem.idle" });
            _idleLines.Add(new NpcIdleLine { anchorId = "npc_electrician_hasan", speakerKey = "npc.hasan.name", lineKey = "npc.hasan.idle" });
            _idleLines.Add(new NpcIdleLine { anchorId = "npc_journalist_defne", speakerKey = "npc.defne.name", lineKey = "npc.defne.idle" });

            // --- M15: Dedemin Kayıp Sandığı (Brass Clues + Time Capsule Chest) ---
            SpawnStoryProp("m15_clue_mosque", "m15_dedemin_sandigi", new Vector3(82.0f, 0.08f, -31.5f), new Color(0.95f, 0.78f, 0.24f), new Vector3(0.42f, 0.12f, 0.34f));
            SpawnStoryProp("m15_clue_park", "m15_dedemin_sandigi", new Vector3(-20.0f, 0.08f, 69.5f), new Color(0.95f, 0.78f, 0.24f), new Vector3(0.42f, 0.12f, 0.34f));
            SpawnStoryProp("m15_clue_zubeyde", "m15_dedemin_sandigi", new Vector3(12.0f, 0.08f, -151.0f), new Color(0.95f, 0.78f, 0.24f), new Vector3(0.42f, 0.12f, 0.34f));
            SpawnStoryProp("m15_grandfather_chest", "m15_dedemin_sandigi", new Vector3(-63.8f, 0.10f, 6.2f), new Color(0.92f, 0.68f, 0.16f), new Vector3(0.72f, 0.44f, 0.46f));

            // --- M17: Gece Yarısı Elektrik Kesintisi (Fuse Box, Generator Fuel, Relay Switch) ---
            SpawnStoryProp("m17_fuse_box", "m17_gece_kesintisi", new Vector3(22.8f, 0.08f, 4.6f), new Color(0.98f, 0.56f, 0.14f), new Vector3(0.56f, 0.34f, 0.42f));
            SpawnStoryProp("m17_gen_fuel", "m17_gece_kesintisi", new Vector3(-18.5f, 0.08f, -72.0f), new Color(0.92f, 0.22f, 0.18f), new Vector3(0.40f, 0.54f, 0.28f));
            SpawnStoryProp("m17_relay_panel", "m17_gece_kesintisi", new Vector3(32.0f, 0.08f, 90.0f), new Color(0.24f, 0.86f, 0.96f), new Vector3(0.52f, 0.68f, 0.26f));

            // --- M19: Kaya Holding'in Son Hamlesi (Night Espionage Evidence Briefcases) ---
            SpawnStoryProp("m19_evidence_petrol", "m19_son_hamle", new Vector3(-6.5f, 0.08f, -84.0f), new Color(0.88f, 0.22f, 0.36f), new Vector3(0.52f, 0.18f, 0.38f));
            SpawnStoryProp("m19_evidence_ataturk", "m19_son_hamle", new Vector3(-38.0f, 0.08f, 90.2f), new Color(0.88f, 0.22f, 0.36f), new Vector3(0.52f, 0.18f, 0.38f));
            SpawnStoryProp("m19_evidence_aziziye", "m19_son_hamle", new Vector3(-18.5f, 0.08f, -119.2f), new Color(0.88f, 0.22f, 0.36f), new Vector3(0.52f, 0.18f, 0.38f));

            // --- M20: Bizim Dünyamız · Büyük Mahalle Mirası (Celebration Cake & Legacy Plaque) ---
            SpawnStoryProp("m20_celebration_cake", "m20_bizim_dunyamiz_mirasi", new Vector3(9.5f, 0.08f, 5.2f), new Color(0.98f, 0.82f, 0.42f), new Vector3(0.58f, 0.36f, 0.58f));
            SpawnStoryProp("m20_legacy_plaque", "m20_bizim_dunyamiz_mirasi", new Vector3(-62.2f, 0.12f, 5.4f), new Color(1.0f, 0.86f, 0.28f), new Vector3(0.54f, 0.44f, 0.18f));

            // --- Sports Tournament & Pool Anchors (M16 & M20) ---
            SpawnZone("sport_tennis", new Vector3(-46.25f, 0.10f, -27.2f), 8f);
            SpawnZone("sport_football", new Vector3(-14.0f, 0.10f, -30.6f), 8f);
            SpawnZone("sport_basketball", new Vector3(41.0f, 0.10f, -33.2f), 8f);
            SpawnZone("sport_volleyball", new Vector3(59.0f, 0.10f, -35.2f), 8f);
            SpawnZone("m16_pool_swim_zone", new Vector3(16.0f, 1.40f, -40.0f), 11f);

            // --- M17 Trafo Delivery Zone ---
            SpawnZone("m17_trafo_zone", new Vector3(24.0f, 0.10f, 44.5f), 11f);

            // --- M18 Eight-Avenue Grand Street Rally Checkpoints ---
            SpawnZone("m18_cp1_fatih", new Vector3(0.0f, 0.18f, 142.0f), 16f);
            SpawnZone("m18_cp2_eyyubi", new Vector3(-148.0f, 0.12f, -18.0f), 16f);
            SpawnZone("m18_cp3_zubeyde", new Vector3(6.0f, 0.12f, -146.0f), 16f);
            SpawnZone("m18_cp4_udeh", new Vector3(99.0f, 0.12f, -30.0f), 16f);
            SpawnZone("m18_cp5_finish", new Vector3(22.0f, 0.12f, 0.0f), 14f);

            RefreshVisibility();
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }
        }

        private void Start()
        {
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
                PlayerModeController.Instance.OnInteracted += HandleInteracted;
                _subscribed = true;
            }
        }

        private readonly System.Collections.Generic.List<Material> _runtimeMats =
            new System.Collections.Generic.List<Material>(8);

        private void OnDestroy()
        {
            if (_subscribed && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted -= HandleInteracted;
            }
            for (int i = 0; i < _runtimeMats.Count; i++)
            {
                if (_runtimeMats[i] != null) Destroy(_runtimeMats[i]);
            }
            _runtimeMats.Clear();
        }

        private void Update()
        {
            if (Time.frameCount % 10 == 0)
            {
                RefreshVisibility();
            }
        }

        private void RefreshVisibility()
        {
            MissionManager mm = MissionManager.Instance;
            if (mm != null && mm.IsDialoguePlaying) return;
            MissionData active = mm != null ? mm.ActiveMission : null;
            int stepIndex = (active != null && mm.CurrentStep != null) ? active.steps.IndexOf(mm.CurrentStep) : -1;

            for (int i = 0; i < _pickups.Count; i++)
            {
                PickupEntry p = _pickups[i];
                if (p.go == null) continue;

                bool shouldShow = false;
                if (active != null && active.missionId == p.missionId && stepIndex >= 0)
                {
                    for (int s = stepIndex; s < active.steps.Count; s++)
                    {
                        if (string.Equals(active.steps[s].targetAnchorId, p.anchorId, System.StringComparison.OrdinalIgnoreCase))
                        {
                            shouldShow = true;
                            break;
                        }
                    }
                }

                if (p.go.activeSelf != shouldShow)
                {
                    p.go.SetActive(shouldShow);
                }
            }
        }

        private void HandleInteracted(InteractableTrigger trigger)
        {
            if (trigger == null) return;
            MissionManager mm = MissionManager.Instance;
            if (mm == null || mm.IsDialoguePlaying || mm.IsFailed) return;

            MissionStepData step = mm.CurrentStep;
            if (step != null &&
                (string.Equals(step.targetAnchorId, trigger.InteractableId, System.StringComparison.OrdinalIgnoreCase) ||
                 (!string.IsNullOrEmpty(step.secondaryTargetAnchorId) &&
                  string.Equals(step.secondaryTargetAnchorId, trigger.InteractableId, System.StringComparison.OrdinalIgnoreCase))))
            {
                return;
            }

            for (int i = 0; i < _idleLines.Count; i++)
            {
                if (string.Equals(_idleLines[i].anchorId, trigger.InteractableId, System.StringComparison.OrdinalIgnoreCase))
                {
                    mm.ShowQuickNpcDialogue(_idleLines[i].speakerKey, _idleLines[i].lineKey);
                    return;
                }
            }
        }

        private static GameObject FindAnchorObject(string anchorId)
        {
            WorldAnchor a = WorldAnchorRegistry.Get(anchorId);
            return a != null ? a.gameObject : null;
        }

        private static bool AnchorExists(string anchorId)
        {
            return WorldAnchorRegistry.Get(anchorId) != null;
        }

        private void SpawnNpc(string anchorId, string objName, GameObject template, Vector3 pos, float yaw, float scale, Color shirt)
        {
            if (template == null || AnchorExists(anchorId)) return;

            GameObject npc = Instantiate(template, _staging);
            npc.name = objName;
            npc.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            npc.transform.localScale = template.transform.lossyScale * scale;

            WorldAnchor anchor = npc.GetComponent<WorldAnchor>();
            if (anchor == null) anchor = npc.AddComponent<WorldAnchor>();
            anchor.Configure(anchorId, 3.8f, new Vector3(0f, 2.3f * scale, 0f));

            InteractableTrigger trig = npc.GetComponent<InteractableTrigger>();
            if (trig == null) trig = npc.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.Npc, anchorId, "ui.btn.talk", 3.8f);

            Renderer[] renderers = npc.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                Material[] mats = r.materials;
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null) continue;
                    string n = mats[m].name;
                    if (n.Contains("Shirt") || n.Contains("Mechanic") || n.Contains("Receiver") || n.Contains("Suit"))
                    {
                        if (mats[m].HasProperty("_BaseColor")) mats[m].SetColor("_BaseColor", shirt);
                        else if (mats[m].HasProperty("_Color")) mats[m].SetColor("_Color", shirt);
                        changed = true;
                    }
                }
                if (changed) r.materials = mats;
            }

            npc.transform.SetParent(transform, true);
        }

        private void SpawnStoryProp(string anchorId, string missionId, Vector3 pos, Color accentColor, Vector3 boxScale)
        {
            if (AnchorExists(anchorId)) return;

            GameObject root = new GameObject($"Pickup_{anchorId}");
            root.transform.SetParent(_staging, false);
            root.transform.position = pos;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, boxScale.y * 0.5f + 0.14f, 0f);
            visual.transform.localScale = boxScale;
            Destroy(visual.GetComponent<Collider>());

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material mat = new Material(sh);
            _runtimeMats.Add(mat);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", accentColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", accentColor * 0.65f);
            }
            Renderer r = visual.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "GlowRing";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localScale = new Vector3(1.18f, 0.015f, 1.18f);
            Destroy(ring.GetComponent<Collider>());
            Renderer rr = ring.GetComponent<Renderer>();
            if (rr != null)
            {
                rr.sharedMaterial = mat;
                rr.shadowCastingMode = ShadowCastingMode.Off;
            }

            SphereCollider sc = root.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 1.65f;

            WorldAnchor anchor = root.AddComponent<WorldAnchor>();
            anchor.Configure(anchorId, 3.4f, new Vector3(0f, 1.3f, 0f));

            InteractableTrigger trig = root.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.InspectObject, anchorId, "ui.btn.pickup", 3.6f);

            root.transform.SetParent(transform, true);
            _pickups.Add(new PickupEntry { go = root, anchorId = anchorId, missionId = missionId });
        }

        private void SpawnWorkBeaconLight(Vector3 pos)
        {
            GameObject lamp = new GameObject("ElectricianWorkLamp");
            lamp.transform.SetParent(transform, false);
            lamp.transform.position = pos;

            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.transform.SetParent(lamp.transform, false);
            bulb.transform.localScale = Vector3.one * 0.24f;
            Destroy(bulb.GetComponent<Collider>());

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (sh != null)
            {
                Material m = new Material(sh);
                _runtimeMats.Add(m);
                Color warm = new Color(1.0f, 0.78f, 0.26f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", warm);
                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", warm * 2.2f);
                }
                Renderer r = bulb.GetComponent<Renderer>();
                if (r != null) { r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; }
            }

            Light pt = lamp.AddComponent<Light>();
            pt.type = LightType.Point;
            pt.color = new Color(1.0f, 0.82f, 0.45f);
            pt.intensity = 6.5f;
            pt.range = 11f;
            pt.shadows = LightShadows.None;
        }

        private void SpawnZone(string anchorId, Vector3 pos, float radius)
        {
            if (AnchorExists(anchorId)) return;
            GameObject go = new GameObject($"Zone_{anchorId}");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            WorldAnchor a = go.AddComponent<WorldAnchor>();
            a.Configure(anchorId, radius, new Vector3(0f, 2.2f, 0f));
        }
    }
}
