using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.Missions
{
    /// <summary>
    /// Spawns Chapter 3 ("Hamidiye'nin Kalbi", Missions 11-14) NPCs, glowing story dossiers,
    /// signature folders, summer pool crate, and plazas across Fatih Bulvarı Saat Kulesi,
    /// Eyyubi Cd., Aziziye İTO Primary School, Petrol Yolu Cd., and Hamidiye Open Swimming Pool.
    /// </summary>
    public sealed class ChapterThreeWorldContent : MonoBehaviour
    {
        private struct PickupEntry
        {
            public GameObject go;
            public string anchorId;
            public string missionId;
        }

        private readonly List<PickupEntry> _pickups = new List<PickupEntry>();
        private Transform _staging;
        private bool _subscribed;

        private static readonly Color ShirtTarik = new Color(0.16f, 0.24f, 0.36f);
        private static readonly Color ShirtMuhtar = new Color(0.24f, 0.30f, 0.42f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<MissionManager>() == null) return;
            if (FindAnyObjectByType<ChapterThreeWorldContent>() != null) return;

            GameObject root = new GameObject("--- CHAPTER3_MISSION_CONTENT ---");
            root.AddComponent<ChapterThreeWorldContent>().SpawnAll();
        }

        private void SpawnAll()
        {
            GameObject staging = new GameObject("_Staging");
            staging.SetActive(false);
            staging.transform.SetParent(transform, false);
            _staging = staging.transform;

            GameObject maleTpl = FindAnchorObject("npc_mechanic") ?? FindAnchorObject("npc_harbor_receiver");

            // --- Chapter 3 Story NPCs at Fatih Bulvarı · Saat Kulesi Plaza ---
            SpawnNpc("npc_avukat_tarik", "NPC_AvukatTarik_SaatKulesi", maleTpl, new Vector3(-4.2f, 0.16f, 140.4f), 180f, 1.0f, ShirtTarik);
            SpawnNpc("npc_muhtar_plaza", "NPC_MuhtarRemzi_SaatKulesi", maleTpl, new Vector3(4.5f, 0.16f, 140.4f), 180f, 1.02f, ShirtMuhtar);

            // --- Chapter 3 Mission Pickups (visible only during their mission) ---
            SpawnStoryFolder("m11_zoning_dossier", "m11_saat_kulesi_dosyasi", new Vector3(-1.8f, 0.22f, 139.6f), new Color(0.92f, 0.36f, 0.16f));
            SpawnStoryFolder("m12_sig_eyyubi", "m12_sekiz_cadde_imza", new Vector3(-152.2f, 0.08f, -15.5f), new Color(0.20f, 0.62f, 0.90f));
            SpawnStoryFolder("m12_sig_aziziye", "m12_sekiz_cadde_imza", new Vector3(-12.5f, 0.08f, -119.4f), new Color(0.20f, 0.62f, 0.90f));
            SpawnStoryFolder("m12_sig_petrol", "m12_sekiz_cadde_imza", new Vector3(5.2f, 0.08f, -71.5f), new Color(0.20f, 0.62f, 0.90f));
            SpawnStoryFolder("m13_summer_crate", "m13_havuz_senligi", new Vector3(7.6f, 0.08f, 5.2f), new Color(0.18f, 0.78f, 0.52f), boxScale: new Vector3(0.62f, 0.42f, 0.48f));
            SpawnStoryFolder("m13_pool_table", "m13_havuz_senligi", new Vector3(16.0f, 1.44f, -33.8f), new Color(0.24f, 0.85f, 0.95f), boxScale: new Vector3(0.68f, 0.46f, 0.52f));
            SpawnStoryFolder("m14_charter_folder", "m14_hamidiye_zaferi", new Vector3(0.0f, 0.24f, 139.4f), new Color(0.98f, 0.80f, 0.20f));

            // --- Chapter 3 Zones ---
            SpawnZone("m11_saat_kulesi_zone", new Vector3(0.0f, 0.16f, 136.5f), 16f);
            SpawnZone("m13_pool_deck_zone", new Vector3(16.0f, 1.40f, -34.0f), 12f);

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
            new System.Collections.Generic.List<Material>(4);

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
            if (step != null && string.Equals(step.targetAnchorId, trigger.InteractableId, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(trigger.InteractableId, "npc_avukat_tarik", System.StringComparison.OrdinalIgnoreCase))
            {
                mm.ShowQuickNpcDialogue("npc.tarik.name", "npc.tarik.idle");
            }
            else if (string.Equals(trigger.InteractableId, "npc_muhtar_plaza", System.StringComparison.OrdinalIgnoreCase))
            {
                mm.ShowQuickNpcDialogue("npc.muhtar.name", "npc.muhtar.idle");
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

        private void SpawnStoryFolder(string anchorId, string missionId, Vector3 pos, Color accentColor, Vector3? boxScale = null)
        {
            if (AnchorExists(anchorId)) return;

            GameObject root = new GameObject($"Pickup_{anchorId}");
            root.transform.SetParent(_staging, false);
            root.transform.position = pos;

            Vector3 sz = boxScale ?? new Vector3(0.46f, 0.16f, 0.36f);
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, sz.y * 0.5f + 0.12f, 0f);
            visual.transform.localScale = sz;
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

            // Ground glowing ring so story item is unmistakable from a distance
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "GlowRing";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localScale = new Vector3(1.1f, 0.015f, 1.1f);
            Destroy(ring.GetComponent<Collider>());
            Renderer rr = ring.GetComponent<Renderer>();
            if (rr != null)
            {
                rr.sharedMaterial = mat;
                rr.shadowCastingMode = ShadowCastingMode.Off;
            }

            SphereCollider sc = root.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 1.6f;

            WorldAnchor anchor = root.AddComponent<WorldAnchor>();
            anchor.Configure(anchorId, 3.2f, new Vector3(0f, 1.2f, 0f));

            InteractableTrigger trig = root.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.InspectObject, anchorId, "ui.btn.pickup", 3.4f);

            root.transform.SetParent(transform, true);
            _pickups.Add(new PickupEntry { go = root, anchorId = anchorId, missionId = missionId });
        }

        private void SpawnZone(string anchorId, Vector3 pos, float radius)
        {
            if (AnchorExists(anchorId)) return;
            GameObject go = new GameObject($"Zone_{anchorId}");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            WorldAnchor a = go.AddComponent<WorldAnchor>();
            a.Configure(anchorId, radius, Vector3.zero);
        }
    }
}
