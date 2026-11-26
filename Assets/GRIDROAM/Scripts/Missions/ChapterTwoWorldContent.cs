using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;

namespace OurWorld.Missions
{
    /// <summary>
    /// Spawns the NPCs, pickups and zones used by Chapter 2 (Missions 6-10) at runtime,
    /// cloning existing scene actors so no scene regeneration is required.
    /// Pickups are only visible while their mission step is still ahead of the player,
    /// so retries automatically restore them and free roam stays clean.
    /// </summary>
    public class ChapterTwoWorldContent : MonoBehaviour
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

        private static readonly Color ShirtAyse = new Color(0.55f, 0.24f, 0.42f);
        private static readonly Color ShirtEfe = new Color(0.95f, 0.62f, 0.12f);
        private static readonly Color ShirtPharmacist = new Color(0.93f, 0.95f, 0.96f);
        private static readonly Color ShirtMuhtar = new Color(0.20f, 0.27f, 0.40f);

        private readonly List<PickupEntry> _pickups = new List<PickupEntry>();
        private readonly List<NpcIdleLine> _idleLines = new List<NpcIdleLine>();
        private Transform _staging;
        private bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<MissionManager>() == null) return;
            if (FindAnyObjectByType<ChapterTwoWorldContent>() != null) return;

            GameObject root = new GameObject("--- CHAPTER2_MISSION_CONTENT ---");
            root.AddComponent<ChapterTwoWorldContent>().SpawnAll();
        }

        private void SpawnAll()
        {
            GameObject staging = new GameObject("_Staging");
            staging.SetActive(false);
            staging.transform.SetParent(transform, false);
            _staging = staging.transform;

            GameObject femaleTpl = FindAnchorObject("npc_harbor_receiver") ?? FindAnchorObject("npc_elena_neighborhood");
            GameObject maleTpl = FindAnchorObject("npc_mechanic") ?? femaleTpl;

            // --- NPCs (distributed across Mürşit Cd., Hamidiye Parkı, Petrol Yolu Cd., and Fatih Bulvarı Saat Kulesi!) ---
            SpawnNpc("npc_ayse_teyze", "NPC_AyseTeyze", femaleTpl, new Vector3(12.0f, 0.04f, 51.4f), 180f, 0.97f, ShirtAyse);
            SpawnNpc("npc_efe", "NPC_Efe_Child", maleTpl, new Vector3(-22.0f, 0.04f, 66.5f), 0f, 0.74f, ShirtEfe);
            SpawnNpc("npc_pharmacist", "NPC_EczaciSelin", femaleTpl, new Vector3(-28.0f, 0.04f, -71.8f), 180f, 1.0f, ShirtPharmacist);
            SpawnNpc("npc_muhtar", "NPC_MuhtarRemzi", maleTpl, new Vector3(-8.0f, 0.16f, 136.5f), 180f, 1.02f, ShirtMuhtar);

            _idleLines.Add(new NpcIdleLine { anchorId = "npc_ayse_teyze", speakerKey = "npc.ayse.name", lineKey = "npc.ayse.idle" });
            _idleLines.Add(new NpcIdleLine { anchorId = "npc_efe", speakerKey = "npc.efe.name", lineKey = "npc.efe.idle" });
            _idleLines.Add(new NpcIdleLine { anchorId = "npc_pharmacist", speakerKey = "npc.pharmacist.name", lineKey = "npc.pharmacist.idle" });
            _idleLines.Add(new NpcIdleLine { anchorId = "npc_muhtar", speakerKey = "npc.muhtar.name", lineKey = "npc.muhtar.idle" });

            // --- Pickups (distributed across Selman Bakkal, Gürsu, Hz. Osman Camii, Eyyubi Cd., Kadife Sk. & Havadis Sk.) ---
            SpawnPickup("m06_grocery_bag", "m06_bakkal_siparisi", new Vector3(13.0f, 0.06f, 5.0f), 0.48f);
            SpawnPickup("m07_cat_collar", "m07_kayip_kedi", new Vector3(-38.5f, 0.06f, -15.0f), 0.25f);
            SpawnCat("m07_lost_cat", "m07_kayip_kedi", new Vector3(88.5f, 0.06f, -27.0f));
            SpawnPickup("m09_spare_part", "m09_ustanin_parcasi", new Vector3(-152.0f, 0.06f, -16.0f), 0.55f);
            SpawnPickup("m10_tea_box", "m10_mahalle_senligi", new Vector3(-14.0f, 0.06f, -180.8f), 0.5f);
            SpawnPickup("m10_simit_tray", "m10_mahalle_senligi", new Vector3(-18.0f, 0.06f, 180.8f), 0.5f);

            // --- Zones ---
            SpawnZone("m07_pitch_zone", new Vector3(-41.0f, 0.1f, -15.0f), 10f);
            SpawnZone("m08_pharmacy_zone", new Vector3(-28.0f, 0.1f, -76.5f), 10f);
            SpawnZone("m09_fuel_zone", new Vector3(-148.0f, 0.1f, -16.0f), 12f);
            SpawnZone("m09_garage_zone", new Vector3(24.5f, 0.1f, 1.5f), 8f);
            SpawnZone("m10_festival_zone", new Vector3(0.0f, 0.16f, 136.5f), 14f);

            RefreshVisibility();
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }
        }

        private void Start()
        {
            // Safety net in case PlayerModeController cached interactables before we spawned.
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
                PlayerModeController.Instance.OnInteracted += HandleInteracted;
                _subscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (_subscribed && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted -= HandleInteracted;
            }
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
            if (mm != null && mm.IsDialoguePlaying) return; // keep collected items hidden mid-dialogue
            MissionData active = mm != null ? mm.ActiveMission : null;
            int stepIndex = -1;
            if (active != null && mm.CurrentStep != null)
            {
                stepIndex = active.steps.IndexOf(mm.CurrentStep);
            }

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
                return; // MissionManager handles it.
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

        // ------------------------------------------------------------------ spawning helpers

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
            anchor.Configure(anchorId, 3.6f, new Vector3(0f, 2.3f * scale, 0f));

            InteractableTrigger trig = npc.GetComponent<InteractableTrigger>();
            if (trig == null) trig = npc.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.Npc, anchorId, "ui.btn.talk", 3.6f);

            TintShirt(npc, shirt);
            npc.transform.SetParent(transform, true);
        }

        private void SpawnPickup(string anchorId, string missionId, Vector3 pos, float scale)
        {
            if (AnchorExists(anchorId)) return;

            GameObject crateTpl = FindAnchorObject("tail_dropped_package");
            GameObject go;
            if (crateTpl != null)
            {
                go = Instantiate(crateTpl, _staging);
                go.transform.localScale = Vector3.one * scale;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(_staging, false);
                go.transform.localScale = Vector3.one * scale;
            }

            go.name = $"Pickup_{anchorId}";
            go.transform.SetPositionAndRotation(pos + Vector3.up * (crateTpl != null ? 0f : scale * 0.5f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            ConfigurePickup(go, anchorId, "ui.btn.pickup");
            go.transform.SetParent(transform, true);
            _pickups.Add(new PickupEntry { go = go, anchorId = anchorId, missionId = missionId });
        }

        private void SpawnCat(string anchorId, string missionId, Vector3 pos)
        {
            if (AnchorExists(anchorId)) return;

            StreetCatController catTpl = FindAnyObjectByType<StreetCatController>();
            if (catTpl == null)
            {
                SpawnPickup(anchorId, missionId, pos, 0.35f);
                return;
            }

            GameObject cat = Instantiate(catTpl.gameObject, _staging);
            cat.name = "Mission07_Pamuk_Cat";
            StreetCatController ctrl = cat.GetComponent<StreetCatController>();
            if (ctrl != null) DestroyImmediate(ctrl);
            Rigidbody rb = cat.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            cat.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 200f, 0f));
            ConfigurePickup(cat, anchorId, "ui.btn.pickup_cat");
            cat.transform.SetParent(transform, true);
            _pickups.Add(new PickupEntry { go = cat, anchorId = anchorId, missionId = missionId });
        }

        private static void ConfigurePickup(GameObject go, string anchorId, string promptKey)
        {
            WorldAnchor anchor = go.GetComponent<WorldAnchor>();
            if (anchor == null) anchor = go.AddComponent<WorldAnchor>();
            anchor.Configure(anchorId, 3.4f, new Vector3(0f, 1.6f, 0f));

            InteractableTrigger trig = go.GetComponent<InteractableTrigger>();
            if (trig == null) trig = go.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.PickupPackage, anchorId, promptKey, 3.4f);
        }

        private void SpawnZone(string anchorId, Vector3 pos, float radius)
        {
            if (AnchorExists(anchorId)) return;

            GameObject zone = new GameObject($"Zone_{anchorId}");
            zone.transform.SetParent(_staging, false);
            zone.transform.position = pos;
            zone.AddComponent<WorldAnchor>().Configure(anchorId, radius, new Vector3(0f, 2.2f, 0f));
            zone.transform.SetParent(transform, true);
        }

        private static void TintShirt(GameObject root, Color color)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string n = mats[i].name;
                    if (n.IndexOf("Shirt", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        n.IndexOf("Receiver", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        n.IndexOf("Mechanic", System.StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    r.GetPropertyBlock(block, i);
                    block.SetColor("_BaseColor", color);
                    block.SetColor("_Color", color);
                    r.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
