using System.Collections.Generic;
using OurWorld.Missions;
using UnityEditor;
using UnityEngine;

namespace OurWorld.Editor
{
    /// <summary>
    /// Chapter 2 (Missions 6-10) for the v1.0 store release.
    /// The matching NPCs / pickups / zones are spawned at runtime by
    /// <see cref="OurWorld.Missions.ChapterTwoWorldContent"/>, so the existing scene
    /// does not need to be regenerated for these missions to work.
    /// </summary>
    public static partial class OurWorldPrototypeSetup
    {
        public const string ChapterTwoFirstMissionId = "m06_bakkal_siparisi";

        [MenuItem("OurWorld/Görevler/Bölüm 2 Görevlerini Oluştur (6-10)")]
        public static void EnsureChapterTwoMissionsMenu()
        {
            MissionCatalog catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>($"{MissionDir}/MissionCatalog.asset");
            if (catalog == null)
            {
                Debug.LogError("[OurWorld] MissionCatalog.asset bulunamadı. Önce sahne kurulumunu çalıştırın.");
                return;
            }

            AppendChapterTwoMissions(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[OurWorld] Görev kataloğu hazır: {catalog.missions.Count} görev.");
        }

        /// <summary>
        /// Creates/updates Missions 6-10, links Mission 5 to Mission 6 and appends them to the catalog (idempotent).
        /// </summary>
        public static void AppendChapterTwoMissions(MissionCatalog catalog)
        {
            if (catalog == null) return;

            // --- Dialogues ---
            DialogueData d6a = Dlg("DLG_M06_Ayse_Request", ("npc.ayse.name", "m06.dlg.a1"), ("npc.ayse.name", "m06.dlg.a2"));
            DialogueData d6b = Dlg("DLG_M06_Groceries", ("npc.player.name", "m06.dlg.b1"));
            DialogueData d6c = Dlg("DLG_M06_Ayse_Thanks", ("npc.ayse.name", "m06.dlg.c1"), ("npc.ayse.name", "m06.dlg.c2"));

            DialogueData d7a = Dlg("DLG_M07_Efe_Request", ("npc.efe.name", "m07.dlg.a1"), ("npc.efe.name", "m07.dlg.a2"));
            DialogueData d7b = Dlg("DLG_M07_Collar", ("npc.player.name", "m07.dlg.b1"), ("npc.player.name", "m07.dlg.b2"));
            DialogueData d7c = Dlg("DLG_M07_CatFound", ("npc.player.name", "m07.dlg.c1"));
            DialogueData d7d = Dlg("DLG_M07_Efe_Thanks", ("npc.efe.name", "m07.dlg.d1"), ("npc.efe.name", "m07.dlg.d2"));

            DialogueData d8a = Dlg("DLG_M08_Ayse_Urgent", ("npc.ayse.name", "m08.dlg.a1"), ("npc.ayse.name", "m08.dlg.a2"));
            DialogueData d8b = Dlg("DLG_M08_Pharmacist", ("npc.pharmacist.name", "m08.dlg.b1"), ("npc.pharmacist.name", "m08.dlg.b2"));
            DialogueData d8c = Dlg("DLG_M08_Ayse_Relief", ("npc.ayse.name", "m08.dlg.c1"), ("npc.ayse.name", "m08.dlg.c2"));

            DialogueData d9a = Dlg("DLG_M09_Mechanic_Part", ("npc.mechanic.name", "m09.dlg.a1"), ("npc.mechanic.name", "m09.dlg.a2"));
            DialogueData d9b = Dlg("DLG_M09_PartFound", ("npc.player.name", "m09.dlg.b1"));
            DialogueData d9c = Dlg("DLG_M09_Mechanic_Thanks", ("npc.mechanic.name", "m09.dlg.c1"), ("npc.mechanic.name", "m09.dlg.c2"));

            DialogueData d10a = Dlg("DLG_M10_Elena_Festival", ("npc.harbor_receiver.name", "m10.dlg.a1"), ("npc.harbor_receiver.name", "m10.dlg.a2"));
            DialogueData d10b = Dlg("DLG_M10_Tea", ("npc.player.name", "m10.dlg.b1"));
            DialogueData d10c = Dlg("DLG_M10_Simit", ("npc.player.name", "m10.dlg.c1"));
            DialogueData d10d = Dlg("DLG_M10_Muhtar_Final", ("npc.muhtar.name", "m10.dlg.d1"), ("npc.muhtar.name", "m10.dlg.d2"), ("npc.muhtar.name", "m10.dlg.d3"));

            // --- Mission 6: Bakkal Siparişi ---
            MissionData m06 = Mission("Mission_06_BakkalSiparisi", ChapterTwoFirstMissionId, "m06", 300, "m07_kayip_kedi", false);
            m06.steps = new List<MissionStepData>
            {
                Talk("m06.step1.objective", "npc_ayse_teyze", d6a),
                Inspect("m06.step2.objective", "m06_grocery_bag", d6b),
                new MissionStepData
                {
                    stepType = MissionStepType.TalkToNpc, objectiveKey = "m06.step3.objective",
                    targetAnchorId = "npc_ayse_teyze", dialogueAsset = d6c, timeLimitSeconds = 150f
                }
            };

            // --- Mission 7: Kayıp Kedi Pamuk ---
            MissionData m07 = Mission("Mission_07_KayipKedi", "m07_kayip_kedi", "m07", 350, "m08_acil_eczane", false);
            m07.steps = new List<MissionStepData>
            {
                Talk("m07.step1.objective", "npc_efe", d7a),
                Reach("m07.step2.objective", "m07_pitch_zone", 10f, false, false),
                Inspect("m07.step3.objective", "m07_cat_collar", d7b),
                Inspect("m07.step4.objective", "m07_lost_cat", d7c),
                Talk("m07.step5.objective", "npc_efe", d7d)
            };

            // --- Mission 8: Acil Eczane ---
            MissionData m08 = Mission("Mission_08_AcilEczane", "m08_acil_eczane", "m08", 400, "m09_ustanin_parcasi", true);
            m08.steps = new List<MissionStepData>
            {
                Talk("m08.step1.objective", "npc_ayse_teyze", d8a),
                new MissionStepData { stepType = MissionStepType.EnterVehicle, objectiveKey = "m08.step2.objective", targetAnchorId = "player_sedan" },
                new MissionStepData
                {
                    stepType = MissionStepType.DeliverToZone, objectiveKey = "m08.step3.objective",
                    targetAnchorId = "m08_pharmacy_zone", triggerRadius = 8f, timeLimitSeconds = 100f, requiresVehicle = true
                },
                Talk("m08.step4.objective", "npc_pharmacist", d8b),
                new MissionStepData
                {
                    stepType = MissionStepType.TalkToNpc, objectiveKey = "m08.step5.objective",
                    targetAnchorId = "npc_ayse_teyze", dialogueAsset = d8c, timeLimitSeconds = 140f
                }
            };

            // --- Mission 9: Ustanın Parçası ---
            MissionData m09 = Mission("Mission_09_UstaninParcasi", "m09_ustanin_parcasi", "m09", 450, "m10_mahalle_senligi", true);
            m09.steps = new List<MissionStepData>
            {
                Talk("m09.step1.objective", "npc_mechanic", d9a),
                Reach("m09.step2.objective", "m09_fuel_zone", 10f, true, false),
                Inspect("m09.step3.objective", "m09_spare_part", d9b),
                new MissionStepData
                {
                    stepType = MissionStepType.DeliverToZone, objectiveKey = "m09.step4.objective",
                    targetAnchorId = "m09_garage_zone", triggerRadius = 8f, timeLimitSeconds = 110f, requiresVehicle = true
                },
                Talk("m09.step5.objective", "npc_mechanic", d9c)
            };

            // --- Mission 10: Mahalle Şenliği (final) ---
            MissionData m10 = Mission("Mission_10_MahalleSenligi", "m10_mahalle_senligi", "m10", 1000, string.Empty, false);
            m10.autoStartNextMission = false;
            m10.steps = new List<MissionStepData>
            {
                Talk("m10.step1.objective", "npc_elena_neighborhood", d10a),
                Inspect("m10.step2.objective", "m10_tea_box", d10b),
                Inspect("m10.step3.objective", "m10_simit_tray", d10c),
                Reach("m10.step4.objective", "m10_festival_zone", 10f, false, false),
                Talk("m10.step5.objective", "npc_muhtar", d10d)
            };

            foreach (MissionData m in new[] { m06, m07, m08, m09, m10 })
            {
                EditorUtility.SetDirty(m);
            }

            // Link Chapter 1 finale to Chapter 2.
            MissionData m05 = catalog.GetById("m05_end_of_shift");
            if (m05 != null)
            {
                m05.nextMissionId = ChapterTwoFirstMissionId;
                m05.autoStartNextMission = true;
                EditorUtility.SetDirty(m05);
            }

            foreach (MissionData m in new[] { m06, m07, m08, m09, m10 })
            {
                if (!catalog.missions.Contains(m))
                {
                    catalog.missions.Add(m);
                }
            }

            EditorUtility.SetDirty(catalog);
        }

        private static DialogueData Dlg(string assetName, params (string speaker, string text)[] lines)
        {
            DialogueData d = CreateOrLoadAsset<DialogueData>($"{DialogueDir}/{assetName}.asset");
            d.lines = new List<DialogueLine>();
            foreach ((string speaker, string text) in lines)
            {
                d.lines.Add(new DialogueLine { speakerKey = speaker, textKey = text });
            }
            d.choices = new List<DialogueChoice>();
            EditorUtility.SetDirty(d);
            return d;
        }

        private static MissionData Mission(string assetName, string id, string keyPrefix, int reward, string nextId, bool retryInVehicle)
        {
            MissionData m = CreateOrLoadAsset<MissionData>($"{MissionDir}/{assetName}.asset");
            m.missionId = id;
            m.titleKey = $"{keyPrefix}.title";
            m.summaryKey = $"{keyPrefix}.summary";
            m.rewardMoney = reward;
            m.nextMissionId = nextId;
            m.autoStartNextMission = !string.IsNullOrEmpty(nextId);
            m.retryPlayerAnchorId = "spawn_garage_foot";
            m.retryVehicleAnchorId = "spawn_garage_sedan";
            m.retryStartInVehicle = retryInVehicle;
            return m;
        }

        private static MissionStepData Talk(string objectiveKey, string anchorId, DialogueData dialogue)
        {
            return new MissionStepData
            {
                stepType = MissionStepType.TalkToNpc,
                objectiveKey = objectiveKey,
                targetAnchorId = anchorId,
                dialogueAsset = dialogue
            };
        }

        private static MissionStepData Inspect(string objectiveKey, string anchorId, DialogueData dialogue)
        {
            return new MissionStepData
            {
                stepType = MissionStepType.InspectTarget,
                objectiveKey = objectiveKey,
                targetAnchorId = anchorId,
                dialogueAsset = dialogue,
                requiresOnFoot = true
            };
        }

        private static MissionStepData Reach(string objectiveKey, string anchorId, float radius, bool vehicle, bool onFoot)
        {
            return new MissionStepData
            {
                stepType = MissionStepType.ReachLocation,
                objectiveKey = objectiveKey,
                targetAnchorId = anchorId,
                triggerRadius = radius,
                requiresVehicle = vehicle,
                requiresOnFoot = onFoot
            };
        }
    }

    /// <summary>
    /// Makes sure Missions 6-10 exist in the catalog once after scripts compile,
    /// without regenerating the scene (keeps the user's scene edits intact).
    /// </summary>
    [InitializeOnLoad]
    internal static class OurWorldChapterTwoAutoInstall
    {
        static OurWorldChapterTwoAutoInstall()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                MissionCatalog catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>("Assets/GRIDROAM/Data/Missions/MissionCatalog.asset");
                if (catalog == null || catalog.GetById(OurWorldPrototypeSetup.ChapterTwoFirstMissionId) != null) return;
                OurWorldPrototypeSetup.EnsureChapterTwoMissionsMenu();
            };
        }
    }
}
