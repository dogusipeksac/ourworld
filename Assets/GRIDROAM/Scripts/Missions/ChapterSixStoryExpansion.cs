using System;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.Missions
{
    /// <summary>
    /// Chapter 6 Story Expansion ("Hamidiye Yaz Şöleni & Göletin Sırrı", Missions 21-26):
    /// Appends 6 richly written Istanbul neighborhood story missions with branching finale choices,
    /// memorable story NPCs (Simitçi Ahmet Amca, Çaycı Rıza Usta, Kaptan Tarık, Yüzme Hakemi Selin,
    /// Çiftçi İrfan Amca), sunken Sultanbeyli Lake heirloom diving chest, festival feast props,
    /// and Tennis/Swimming/Diving gameplay tie-ins cleanly at runtime.
    /// </summary>
    public sealed class ChapterSixStoryExpansion : MonoBehaviour
    {
        public static ChapterSixStoryExpansion Instance { get; private set; }

        private struct StoryPickup
        {
            public GameObject Go;
            public string AnchorId;
            public string MissionId;
        }

        private readonly List<StoryPickup> _pickups = new List<StoryPickup>(4);
        private readonly List<ScriptableObject> _runtimeAssets = new List<ScriptableObject>(24);
        private readonly List<Material> _ownedMats = new List<Material>(8);
        private Transform _captainTarik;
        private Vector3 _captainPierPos = new Vector3(-178.2f, 0.44f, 0.0f);
        private Vector3 _captainFestivalPos = new Vector3(-16.0f, 0.04f, 69.5f);
        private bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<MissionManager>() == null) return;

            GameObject go = new GameObject("--- CHAPTER6_STORY_EXPANSION ---");
            go.AddComponent<ChapterSixStoryExpansion>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            InstallMissionsIntoCatalog();
        }

        private void Start()
        {
            SpawnStoryNpcsAndProps();
            RefreshStoryState();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted += HandleStoryIdleTalk;
                PlayerModeController.Instance.RefreshInteractablesCache();
                _subscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (_subscribed && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted -= HandleStoryIdleTalk;
            }
            for (int i = 0; i < _runtimeAssets.Count; i++)
            {
                if (_runtimeAssets[i] != null) Destroy(_runtimeAssets[i]);
            }
            for (int i = 0; i < _ownedMats.Count; i++)
            {
                if (_ownedMats[i] != null) Destroy(_ownedMats[i]);
            }
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Time.frameCount % 12 == 0)
            {
                RefreshStoryState();
            }
        }

        // =====================================================================
        // 1. APPEND MISSIONS 21-26 INTO MISSION CATALOG AT RUNTIME
        // =====================================================================
        private void InstallMissionsIntoCatalog()
        {
            MissionManager mm = MissionManager.Instance ?? FindAnyObjectByType<MissionManager>();
            if (mm == null || mm.Catalog == null || mm.Catalog.missions == null) return;

            if (mm.Catalog.GetById("m21_ilk_simit_ve_mektup") != null) return;

            // Chain Mission 20 directly into Chapter 6 Mission 21!
            MissionData m20 = mm.Catalog.GetById("m20_bizim_dunyamiz_mirasi");
            if (m20 != null && string.IsNullOrEmpty(m20.nextMissionId))
            {
                m20.nextMissionId = "m21_ilk_simit_ve_mektup";
                m20.autoStartNextMission = true;
            }

            // --- M21: Sabahın İlk Simidi & 1984 Mektubu ---
            MissionData m21 = MakeMission("m21_ilk_simit_ve_mektup", "m21.title", "m21.summary", 220, "m22_kortta_onur_maci");
            m21.steps.Add(MakeTalkStep("m21.s1.obj", "npc_simitci_ahmet", MakeDlg(
                ("npc.simitci_ahmet.name", "m21.d1.1"),
                ("npc.simitci_ahmet.name", "m21.d1.2"),
                ("npc.simitci_ahmet.name", "m21.d1.3"))));
            m21.steps.Add(MakeTalkStep("m21.s2.obj", "npc_cayci_riza", MakeDlg(
                ("npc.cayci_riza.name", "m21.d2.1"),
                ("npc.cayci_riza.name", "m21.d2.2"))));
            m21.steps.Add(MakeTalkStep("m21.s3.obj", "npc_kaptan_tarik", MakeDlg(
                ("npc.kaptan_tarik.name", "m21.d3.1"),
                ("npc.kaptan_tarik.name", "m21.d3.2"))));

            // --- M22: Kortta Onur Maçı: Hamidiye Tenis Kupası ---
            MissionData m22 = MakeMission("m22_kortta_onur_maci", "m22.title", "m22.summary", 280, "m23_goletin_batik_yadigari");
            m22.steps.Add(MakeTalkStep("m22.s1.obj", "npc_coach_kerem", MakeDlg(
                ("npc.kerem.name", "m22.d1.1"),
                ("npc.kerem.name", "m22.d1.2"))));
            m22.steps.Add(new MissionStepData
            {
                stepType = MissionStepType.PlaySportMatch,
                objectiveKey = "m22.s2.obj",
                targetAnchorId = "sport_tennis"
            });
            m22.steps.Add(MakeTalkStep("m22.s3.obj", "npc_coach_kerem", MakeDlg(
                ("npc.kerem.name", "m22.d2.1"),
                ("npc.kerem.name", "m22.d2.2"))));

            // --- M23: Sultanbeyli Göleti'nin Batık Yadigarı ---
            MissionData m23 = MakeMission("m23_goletin_batik_yadigari", "m23.title", "m23.summary", 320, "m24_altin_kulac_yarisi");
            m23.steps.Add(MakeTalkStep("m23.s1.obj", "npc_kaptan_tarik", MakeDlg(
                ("npc.kaptan_tarik.name", "m23.d1.1"),
                ("npc.kaptan_tarik.name", "m23.d1.2"))));
            m23.steps.Add(new MissionStepData
            {
                stepType = MissionStepType.InspectTarget,
                objectiveKey = "m23.s2.obj",
                targetAnchorId = "m23_sunken_watch",
                dialogueAsset = MakeDlg(("npc.player.name", "m23.d2.1"))
            });
            m23.steps.Add(MakeTalkStep("m23.s3.obj", "npc_kaptan_tarik", MakeDlg(
                ("npc.kaptan_tarik.name", "m23.d3.1"),
                ("npc.kaptan_tarik.name", "m23.d3.2"))));

            // --- M24: Altın Kulaç: Hamidiye Su Festivali ---
            MissionData m24 = MakeMission("m24_altin_kulac_yarisi", "m24.title", "m24.summary", 340, "m25_aydos_solen_sofrasi");
            m24.steps.Add(MakeTalkStep("m24.s1.obj", "npc_havuz_selin", MakeDlg(
                ("npc.havuz_selin.name", "m24.d1.1"),
                ("npc.havuz_selin.name", "m24.d1.2"))));
            m24.steps.Add(new MissionStepData
            {
                stepType = MissionStepType.PlaySportMatch,
                objectiveKey = "m24.s2.obj",
                targetAnchorId = "sport_swim_race"
            });
            m24.steps.Add(new MissionStepData
            {
                stepType = MissionStepType.PlaySportMatch,
                objectiveKey = "m24.s3.obj",
                targetAnchorId = "sport_diving_board"
            });
            m24.steps.Add(MakeTalkStep("m24.s4.obj", "npc_havuz_selin", MakeDlg(
                ("npc.havuz_selin.name", "m24.d2.1"),
                ("npc.havuz_selin.name", "m24.d2.2"))));

            // --- M25: Aydos Çiftliği'nden Şölen Sofrası ---
            MissionData m25 = MakeMission("m25_aydos_solen_sofrasi", "m25.title", "m25.summary", 360, "m26_hamidiye_yaz_soleni");
            m25.steps.Add(MakeTalkStep("m25.s1.obj", "npc_farmer_irfan", MakeDlg(
                ("npc.farmer_irfan.name", "m25.d1.1"),
                ("npc.farmer_irfan.name", "m25.d1.2"))));
            m25.steps.Add(new MissionStepData
            {
                stepType = MissionStepType.InspectTarget,
                objectiveKey = "m25.s2.obj",
                targetAnchorId = "m25_goat_cheese_crate",
                dialogueAsset = MakeDlg(("npc.player.name", "m25.d2.1"))
            });
            m25.steps.Add(new MissionStepData
            {
                stepType = MissionStepType.InspectTarget,
                objectiveKey = "m25.s3.obj",
                targetAnchorId = "m25_festival_simit_tray",
                dialogueAsset = MakeDlg(("npc.simitci_ahmet.name", "m25.d3.1"))
            });
            m25.steps.Add(MakeTalkStep("m25.s4.obj", "npc_cayci_riza", MakeDlg(
                ("npc.cayci_riza.name", "m25.d4.1"))));

            // --- M26: Büyük Hamidiye Yaz Şöleni (Interactive 3-Choice Emotional Finale!) ---
            MissionData m26 = MakeMission("m26_hamidiye_yaz_soleni", "m26.title", "m26.summary", 600, string.Empty);
            m26.autoStartNextMission = false;

            DialogueData finaleDlg = MakeDlg(
                ("npc.kaptan_tarik.name", "m26.d1.1"),
                ("npc.kaptan_tarik.name", "m26.d1.2"));
            finaleDlg.choices.Add(new DialogueChoice
            {
                choiceId = "ch6_neighborhood_home",
                labelKey = "m26.c1.label",
                followUpDialogue = MakeDlg(("npc.cayci_riza.name", "m26.c1.f1"), ("npc.kaptan_tarik.name", "m26.c1.f2"))
            });
            finaleDlg.choices.Add(new DialogueChoice
            {
                choiceId = "ch6_sport_spirit",
                labelKey = "m26.c2.label",
                followUpDialogue = MakeDlg(("npc.kerem.name", "m26.c2.f1"), ("npc.havuz_selin.name", "m26.c2.f2"))
            });
            finaleDlg.choices.Add(new DialogueChoice
            {
                choiceId = "ch6_1984_legacy",
                labelKey = "m26.c3.label",
                followUpDialogue = MakeDlg(("npc.kaptan_tarik.name", "m26.c3.f1"), ("npc.simitci_ahmet.name", "m26.c3.f2"))
            });

            MissionStepData finaleStep = MakeTalkStep("m26.s1.obj", "npc_kaptan_tarik", finaleDlg);
            finaleStep.setTimeOfDayHours = 18.75f; // Magical golden-hour sunset lanterns!
            m26.steps.Add(finaleStep);

            mm.Catalog.missions.Add(m21);
            mm.Catalog.missions.Add(m22);
            mm.Catalog.missions.Add(m23);
            mm.Catalog.missions.Add(m24);
            mm.Catalog.missions.Add(m25);
            mm.Catalog.missions.Add(m26);

            Debug.Log($"[ChapterSixStoryExpansion] Installed Chapter 6 (M21-M26). Total story missions: {mm.Catalog.missions.Count}.");
        }

        private MissionData MakeMission(string id, string titleKey, string summaryKey, int reward, string nextId)
        {
            MissionData m = ScriptableObject.CreateInstance<MissionData>();
            m.name = id;
            m.missionId = id;
            m.titleKey = titleKey;
            m.summaryKey = summaryKey;
            m.rewardMoney = reward;
            m.nextMissionId = nextId;
            m.autoStartNextMission = !string.IsNullOrEmpty(nextId);
            _runtimeAssets.Add(m);
            return m;
        }

        private MissionStepData MakeTalkStep(string objKey, string npcAnchorId, DialogueData dlg)
        {
            return new MissionStepData
            {
                stepType = MissionStepType.TalkToNpc,
                objectiveKey = objKey,
                targetAnchorId = npcAnchorId,
                dialogueAsset = dlg
            };
        }

        private DialogueData MakeDlg(params (string speakerKey, string textKey)[] lines)
        {
            DialogueData d = ScriptableObject.CreateInstance<DialogueData>();
            for (int i = 0; i < lines.Length; i++)
            {
                d.lines.Add(new DialogueLine { speakerKey = lines[i].speakerKey, textKey = lines[i].textKey });
            }
            _runtimeAssets.Add(d);
            return d;
        }

        // =====================================================================
        // 2. SPAWN CHAPTER 6 STORY NPCs, ANCHORS & GLOWING STORY PROPS
        // =====================================================================
        private void SpawnStoryNpcsAndProps()
        {
            ProceduralHumanoid template = PlayerModeController.Instance?.FootController?.Humanoid;
            if (template == null) return;

            SpawnStoryNpc("npc_simitci_ahmet", "NPC_SimitciAhmet", "Simitçi Ahmet Amca ile Konuş", template, new Vector3(-14.0f, 0.04f, 180.8f), 180f, new Color(0.84f, 0.20f, 0.18f));
            SpawnStoryNpc("npc_cayci_riza", "NPC_CayciRiza", "Çaycı Rıza Usta ile Konuş", template, new Vector3(-20.0f, 0.04f, 68.5f), 170f, new Color(0.14f, 0.56f, 0.62f));
            _captainTarik = SpawnStoryNpc("npc_kaptan_tarik", "NPC_KaptanTarik", "Kaptan Tarık ile Konuş", template, _captainPierPos, 90f, new Color(0.16f, 0.28f, 0.52f));
            SpawnStoryNpc("npc_havuz_selin", "NPC_HakemSelin", "Yüzme Hakemi Selin ile Konuş", template, new Vector3(11.4f, 1.44f, -34.0f), 160f, new Color(0.12f, 0.66f, 0.72f));
            SpawnStoryNpc("npc_farmer_irfan", "NPC_CiftciIrfan", "Çiftçi İrfan Amca ile Konuş", template, new Vector3(160.2f, 0.04f, -10.2f), -45f, new Color(0.46f, 0.54f, 0.22f));

            // Sport WorldAnchors for M24 objective beacon guidance
            SpawnZoneAnchor("sport_swim_race", new Vector3(8.4f, 1.45f, -36.4f), 8f);
            SpawnZoneAnchor("sport_diving_board", new Vector3(8.4f, 1.45f, -40.0f), 8f);

            // Glowing Mission Story Pickups (visible only while their mission is active!)
            SpawnStoryPickup("m23_sunken_watch", "m23_goletin_batik_yadigari", new Vector3(-191.0f, -0.55f, 3.8f),
                new Color(1.0f, 0.86f, 0.28f), new Vector3(0.56f, 0.36f, 0.42f), "Batık Gümüş Cep Saati Sandığını Çıkar");
            SpawnStoryPickup("m25_goat_cheese_crate", "m25_aydos_solen_sofrasi", new Vector3(163.5f, 0.06f, -11.5f),
                new Color(0.96f, 0.90f, 0.66f), new Vector3(0.58f, 0.38f, 0.46f), "Taze Köy Peyniri Sepetini Al");
            SpawnStoryPickup("m25_festival_simit_tray", "m25_aydos_solen_sofrasi", new Vector3(-16.0f, 0.06f, 181.2f),
                new Color(0.94f, 0.64f, 0.22f), new Vector3(0.62f, 0.24f, 0.46f), "Şölen Simit Tepsisini Al");
        }

        private Transform SpawnStoryNpc(string anchorId, string objName, string prompt, ProceduralHumanoid template, Vector3 pos, float yaw, Color shirtColor)
        {
            ProceduralHumanoid hum = Instantiate(template, transform);
            hum.name = objName;
            hum.transform.position = pos;
            hum.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            hum.SetLocomotion(0f, true, 0f);

            Collider[] cols = hum.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) Destroy(cols[i]);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                Material m = new Material(lit) { color = shirtColor };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", shirtColor);
                _ownedMats.Add(m);
                Renderer[] rends = hum.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rends.Length; r++)
                {
                    string rn = rends[r].name.ToLowerInvariant();
                    if (rn.Contains("chest") || rn.Contains("torso") || rn.Contains("shirt") || rn.Contains("upperarm"))
                    {
                        rends[r].sharedMaterial = m;
                    }
                }
            }

            hum.gameObject.AddComponent<WorldAnchor>().Configure(anchorId, 3.4f, new Vector3(0f, 2.25f, 0f));
            InteractableTrigger trig = hum.gameObject.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.Npc, anchorId, "ui.btn.talk", 3.4f);
            trig.SetCustomPrompt(prompt);
            return hum.transform;
        }

        private void SpawnZoneAnchor(string anchorId, Vector3 pos, float radius)
        {
            GameObject z = new GameObject("Anchor_" + anchorId);
            z.transform.SetParent(transform, false);
            z.transform.position = pos;
            z.AddComponent<WorldAnchor>().Configure(anchorId, radius, new Vector3(0f, 2.2f, 0f));
        }

        private void SpawnStoryPickup(string anchorId, string missionId, Vector3 pos, Color glowColor, Vector3 scale, string prompt)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "StoryPickup_" + anchorId;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.position = pos + Vector3.up * (scale.y * 0.5f);
            go.transform.localScale = scale;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            Material m = new Material(lit) { color = glowColor };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", glowColor * 1.45f);
            _ownedMats.Add(m);
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.sharedMaterial = m;
            }

            go.AddComponent<WorldAnchor>().Configure(anchorId, 3.0f, new Vector3(0f, 1.4f, 0f));
            InteractableTrigger trig = go.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.PickupPackage, anchorId, "ui.btn.interact", 3.2f);
            trig.SetCustomPrompt(prompt);

            go.SetActive(false);
            _pickups.Add(new StoryPickup { Go = go, AnchorId = anchorId, MissionId = missionId });
        }

        private void RefreshStoryState()
        {
            MissionManager mm = MissionManager.Instance;
            if (mm == null || mm.IsDialoguePlaying) return;
            MissionData active = mm.ActiveMission;
            string activeId = active != null ? active.missionId : string.Empty;

            // Move Captain Tarik cleanly to the Sunset Tea Garden Festival during M26!
            if (_captainTarik != null)
            {
                Vector3 desired = activeId == "m26_hamidiye_yaz_soleni" ? _captainFestivalPos : _captainPierPos;
                if ((_captainTarik.position - desired).sqrMagnitude > 0.25f)
                {
                    _captainTarik.position = desired;
                    _captainTarik.rotation = Quaternion.Euler(0f, activeId == "m26_hamidiye_yaz_soleni" ? 180f : 90f, 0f);
                }
            }

            int stepIdx = (active != null && mm.CurrentStep != null) ? active.steps.IndexOf(mm.CurrentStep) : -1;
            for (int i = 0; i < _pickups.Count; i++)
            {
                StoryPickup p = _pickups[i];
                if (p.Go == null) continue;
                bool show = false;
                if (active != null && activeId == p.MissionId && stepIdx >= 0)
                {
                    for (int s = stepIdx; s < active.steps.Count; s++)
                    {
                        if (string.Equals(active.steps[s].targetAnchorId, p.AnchorId, StringComparison.OrdinalIgnoreCase))
                        {
                            show = true;
                            break;
                        }
                    }
                }
                if (p.Go.activeSelf != show) p.Go.SetActive(show);
            }
        }

        private void HandleStoryIdleTalk(InteractableTrigger trig)
        {
            if (trig == null) return;
            MissionManager mm = MissionManager.Instance;
            if (mm != null && mm.IsDialoguePlaying) return;
            if (mm != null && mm.CurrentStep != null &&
                string.Equals(mm.CurrentStep.targetAnchorId, trig.InteractableId, StringComparison.OrdinalIgnoreCase))
            {
                return; // MissionManager already handles the active mission dialogue!
            }

            // Friendly ambient chatter when talking to Chapter 6 story NPCs outside their active step!
            string id = trig.InteractableId;
            if (id == "npc_simitci_ahmet")
                NpcSpeechBubbleManager.Say(trig.transform, "Simitçi Ahmet Amca", "Günaydın Deniz! Tezgahta sıcacık simitler hazır, 1984 hatıraları da aklımda!", 4.2f, NpcBubbleMood.Mission);
            else if (id == "npc_cayci_riza")
                NpcSpeechBubbleManager.Say(trig.transform, "Çaycı Rıza Usta", "Hoş geldin evladım, çınar altında tavşan kanı çayımız her daim taze!", 4.2f, NpcBubbleMood.Friendly);
            else if (id == "npc_kaptan_tarik")
                NpcSpeechBubbleManager.Say(trig.transform, "Kaptan Tarık", "Kuğular bugün bir başka zarif yüzüyor Deniz. Göletin suları eski hatıralarla dolu...", 4.4f, NpcBubbleMood.Mission);
            else if (id == "npc_havuz_selin")
                NpcSpeechBubbleManager.Say(trig.transform, "Yüzme Hakemi Selin", "Havuzda 50m yarışa ya da tramplen dalışına hazırsan başlangıç bloğu seni bekliyor!", 4.2f, NpcBubbleMood.Friendly);
            else if (id == "npc_farmer_irfan")
                NpcSpeechBubbleManager.Say(trig.transform, "Çiftçi İrfan Amca", "Aydos Çiftliği bereket saçıyor! Yeni keçilerimiz Akkız ve Kınalı seni görünce sevindi.", 4.2f, NpcBubbleMood.Friendly);
        }
    }
}
