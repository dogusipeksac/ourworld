using System.Collections.Generic;
using OurWorld.Missions;
using UnityEditor;
using UnityEngine;

namespace OurWorld.Editor
{
    /// <summary>
    /// Chapters 4 & 5 (Missions 15-20) expanding Our World (Bizim Dünyamız) into a full 20-mission
    /// open-world campaign: Grandfather's Riddle Time Capsule, the Great Hamidiye Sports Cup
    /// (Tennis, 3v3 Football, 2v2 Basketball & Pool Swim), Midnight Blackout Emergency (22:00),
    /// 8-Avenue Grand Street Rally, Nighttime Espionage Counter-Operation, and the Grand Legacy Finale
    /// featuring 2v2 Volleyball and a permanent neighborhood legacy choice.
    /// </summary>
    public static partial class OurWorldPrototypeSetup
    {
        public const string ChapterFourFirstMissionId = "m15_dedemin_sandigi";

        [MenuItem("OurWorld/Görevler/Bölüm 4 & 5 Görevlerini Oluştur (15-20)")]
        public static void EnsureChapterFourFiveMissionsMenu()
        {
            MissionCatalog catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>($"{MissionDir}/MissionCatalog.asset");
            if (catalog == null)
            {
                Debug.LogError("[OurWorld] MissionCatalog.asset bulunamadı. Önce sahne kurulumunu çalıştırın.");
                return;
            }

            AppendChapterTwoMissions(catalog);
            AppendChapterThreeMissions(catalog);
            AppendChapterFourFiveMissions(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[OurWorld] 20 görevlik tam hikaye kataloğu hazır: {catalog.missions.Count} görev.");
        }

        public static void AppendChapterFourFiveMissions(MissionCatalog catalog)
        {
            if (catalog == null) return;

            // --- Mission 15: Dedemin Kayıp Sandığı (Riddle Hunt + Golden Hour) ---
            DialogueData d15a = Dlg("DLG_M15_Ayse_Riddle",
                ("npc.ayse.name", "m15.dlg.a1"),
                ("npc.ayse.name", "m15.dlg.a2"));
            DialogueData d15b = Dlg("DLG_M15_ClueMosque", ("npc.player.name", "m15.dlg.b1"));
            DialogueData d15c = Dlg("DLG_M15_CluePark", ("npc.player.name", "m15.dlg.c1"));
            DialogueData d15d = Dlg("DLG_M15_ClueZubeyde", ("npc.player.name", "m15.dlg.d1"));
            DialogueData d15e = Dlg("DLG_M15_ChestOpen",
                ("npc.player.name", "m15.dlg.e1"),
                ("npc.player.name", "m15.dlg.e2"));
            DialogueData d15f = Dlg("DLG_M15_Muhtar_Cup",
                ("npc.muhtar.name", "m15.dlg.f1"),
                ("npc.muhtar.name", "m15.dlg.f2"));

            MissionData m15 = Mission("Mission_15_DedeminSandigi", ChapterFourFirstMissionId, "m15", 900, "m16_buyuk_spor_kupasi", false);
            m15.steps = new List<MissionStepData>
            {
                Talk("m15.step1.objective", "npc_ayse_teyze", d15a),
                Inspect("m15.step2.objective", "m15_clue_mosque", d15b),
                Inspect("m15.step3.objective", "m15_clue_park", d15c),
                WithTime(Inspect("m15.step4.objective", "m15_clue_zubeyde", d15d), 18.5f),
                Inspect("m15.step5.objective", "m15_grandfather_chest", d15e),
                Talk("m15.step6.objective", "npc_muhtar_plaza", d15f)
            };

            // --- Mission 16: Büyük Hamidiye Spor Kupası (Real Playable Sports + Swim!) ---
            DialogueData d16a = Dlg("DLG_M16_Kerem_Intro",
                ("npc.kerem.name", "m16.dlg.a1"),
                ("npc.kerem.name", "m16.dlg.a2"));
            DialogueData d16b = Dlg("DLG_M16_Kerem_Trophy",
                ("npc.kerem.name", "m16.dlg.b1"),
                ("npc.kerem.name", "m16.dlg.b2"));

            MissionData m16 = Mission("Mission_16_BuyukSporKupasi", "m16_buyuk_spor_kupasi", "m16", 1200, "m17_gece_kesintisi", false);
            m16.steps = new List<MissionStepData>
            {
                WithTime(Talk("m16.step1.objective", "npc_coach_kerem", d16a), 14.0f),
                SportStep("m16.step2.objective", "sport_tennis"),
                SportStep("m16.step3.objective", "sport_football"),
                SportStep("m16.step4.objective", "sport_basketball"),
                new MissionStepData
                {
                    stepType = MissionStepType.SwimAtPool,
                    objectiveKey = "m16.step5.objective",
                    targetAnchorId = "m16_pool_swim_zone",
                    triggerRadius = 11f
                },
                Talk("m16.step6.objective", "npc_coach_kerem", d16b)
            };

            // --- Mission 17: Gece Yarısı Elektrik Kesintisi (Midnight 22:00 Timed Rescue!) ---
            DialogueData d17a = Dlg("DLG_M17_Hasan_Blackout",
                ("npc.hasan.name", "m17.dlg.a1"),
                ("npc.hasan.name", "m17.dlg.a2"));
            DialogueData d17b = Dlg("DLG_M17_FuseBox", ("npc.player.name", "m17.dlg.b1"));
            DialogueData d17c = Dlg("DLG_M17_GenFuel", ("npc.player.name", "m17.dlg.c1"));
            DialogueData d17d = Dlg("DLG_M17_Relay", ("npc.player.name", "m17.dlg.d1"));
            DialogueData d17e = Dlg("DLG_M17_LightsRestored",
                ("npc.hasan.name", "m17.dlg.e1"),
                ("npc.hasan.name", "m17.dlg.e2"));

            MissionData m17 = Mission("Mission_17_GeceKesintisi", "m17_gece_kesintisi", "m17", 1100, "m18_sekiz_cadde_rallisi", true);
            m17.steps = new List<MissionStepData>
            {
                WithTime(Talk("m17.step1.objective", "npc_electrician_hasan", d17a), 21.5f),
                TimedInspect("m17.step2.objective", "m17_fuse_box", d17b, 130f),
                TimedInspect("m17.step3.objective", "m17_gen_fuel", d17c, 130f),
                TimedInspect("m17.step4.objective", "m17_relay_panel", d17d, 130f),
                new MissionStepData
                {
                    stepType = MissionStepType.DeliverToZone,
                    objectiveKey = "m17.step5.objective",
                    targetAnchorId = "m17_trafo_zone",
                    triggerRadius = 11f,
                    timeLimitSeconds = 120f
                },
                WithTime(Talk("m17.step6.objective", "npc_electrician_hasan", d17e), 9.5f)
            };

            // --- Mission 18: Sekiz Cadde Büyük Rallisi (Full-Map Timed Street Circuit!) ---
            DialogueData d18a = Dlg("DLG_M18_Kemal_RallyStart",
                ("npc.mechanic.name", "m18.dlg.a1"),
                ("npc.mechanic.name", "m18.dlg.a2"));
            DialogueData d18b = Dlg("DLG_M18_Kemal_RallyWin",
                ("npc.mechanic.name", "m18.dlg.b1"),
                ("npc.mechanic.name", "m18.dlg.b2"));

            MissionData m18 = Mission("Mission_18_SekizCaddeRallisi", "m18_sekiz_cadde_rallisi", "m18", 1400, "m19_son_hamle", true);
            m18.steps = new List<MissionStepData>
            {
                WithTime(Talk("m18.step1.objective", "npc_mechanic", d18a), 17.5f),
                new MissionStepData { stepType = MissionStepType.EnterVehicle, objectiveKey = "m18.step2.objective", targetAnchorId = "player_sedan" },
                RallyGate("m18.step3.objective", "m18_cp1_fatih", 16f, 80f),
                RallyGate("m18.step4.objective", "m18_cp2_eyyubi", 16f, 75f),
                RallyGate("m18.step5.objective", "m18_cp3_zubeyde", 16f, 75f),
                RallyGate("m18.step6.objective", "m18_cp4_udeh", 16f, 75f),
                new MissionStepData
                {
                    stepType = MissionStepType.DeliverToZone,
                    objectiveKey = "m18.step7.objective",
                    targetAnchorId = "m18_cp5_finish",
                    triggerRadius = 14f,
                    timeLimitSeconds = 65f,
                    requiresVehicle = true,
                    dialogueAsset = d18b
                }
            };

            // --- Mission 19: Kaya Holding'in Son Hamlesi (Night Investigative Thriller!) ---
            DialogueData d19a = Dlg("DLG_M19_Defne_Tip",
                ("npc.defne.name", "m19.dlg.a1"),
                ("npc.defne.name", "m19.dlg.a2"));
            DialogueData d19b = Dlg("DLG_M19_EvPetrol", ("npc.player.name", "m19.dlg.b1"));
            DialogueData d19c = Dlg("DLG_M19_EvAtaturk", ("npc.player.name", "m19.dlg.c1"));
            DialogueData d19d = Dlg("DLG_M19_EvAziziye", ("npc.player.name", "m19.dlg.d1"));
            DialogueData d19e = Dlg("DLG_M19_Tarik_Expose",
                ("npc.tarik.name", "m19.dlg.e1"),
                ("npc.defne.name", "m19.dlg.e2"));

            MissionData m19 = Mission("Mission_19_SonHamle", "m19_son_hamle", "m19", 1600, "m20_bizim_dunyamiz_mirasi", true);
            m19.steps = new List<MissionStepData>
            {
                WithTime(Talk("m19.step1.objective", "npc_journalist_defne", d19a), 21.0f),
                Inspect("m19.step2.objective", "m19_evidence_petrol", d19b),
                Inspect("m19.step3.objective", "m19_evidence_ataturk", d19c),
                Inspect("m19.step4.objective", "m19_evidence_aziziye", d19d),
                new MissionStepData
                {
                    stepType = MissionStepType.TalkToNpc,
                    objectiveKey = "m19.step5.objective",
                    targetAnchorId = "npc_avukat_tarik",
                    dialogueAsset = d19e,
                    timeLimitSeconds = 145f
                }
            };

            // --- Mission 20: Bizim Dünyamız · Büyük Mahalle Mirası (Grand Finale + Choice!) ---
            DialogueData d20a = Dlg("DLG_M20_Muhtar_Council",
                ("npc.muhtar.name", "m20.dlg.a1"),
                ("npc.muhtar.name", "m20.dlg.a2"));
            DialogueData d20b = Dlg("DLG_M20_Cake", ("npc.player.name", "m20.dlg.b1"));
            DialogueData d20c = Dlg("DLG_M20_Plaque",
                ("npc.player.name", "m20.dlg.c1"),
                ("npc.player.name", "m20.dlg.c2"));
            DialogueData d20Academy = Dlg("DLG_M20_Ending_Academy",
                ("npc.tarik.name", "m20.academy.1"),
                ("npc.muhtar.name", "m20.academy.2"),
                ("npc.player.name", "m20.academy.3"));
            DialogueData d20Park = Dlg("DLG_M20_Ending_Park",
                ("npc.harbor_receiver.name", "m20.park.1"),
                ("npc.muhtar.name", "m20.park.2"),
                ("npc.player.name", "m20.park.3"));

            MissionData m20 = Mission("Mission_20_BizimDunyamizMirasi", "m20_bizim_dunyamiz_mirasi", "m20", 3000, string.Empty, false);
            m20.autoStartNextMission = false;
            m20.steps = new List<MissionStepData>
            {
                WithTime(Talk("m20.step1.objective", "npc_muhtar_plaza", d20a), 10.0f),
                SportStep("m20.step2.objective", "sport_volleyball"),
                Inspect("m20.step3.objective", "m20_celebration_cake", d20b),
                Inspect("m20.step4.objective", "m20_legacy_plaque", d20c),
                new MissionStepData
                {
                    stepType = MissionStepType.TalkToNpc,
                    objectiveKey = "m20.step5.objective",
                    targetAnchorId = "npc_avukat_tarik",
                    dialogueAsset = d20Academy,
                    primaryChoiceId = "legacy_youth_academy",
                    secondaryTargetAnchorId = "npc_elena_neighborhood",
                    secondaryDialogueAsset = d20Park,
                    secondaryChoiceId = "legacy_green_park"
                }
            };

            MissionData[] newMissions = { m15, m16, m17, m18, m19, m20 };
            foreach (MissionData m in newMissions)
            {
                EditorUtility.SetDirty(m);
            }

            // Link Mission 14 (Chapter 3 Finale) smoothly into Mission 15 (Chapter 4 Start)!
            MissionData m14 = catalog.GetById("m14_hamidiye_zaferi");
            if (m14 != null)
            {
                m14.nextMissionId = ChapterFourFirstMissionId;
                m14.autoStartNextMission = true;
                EditorUtility.SetDirty(m14);
            }

            foreach (MissionData m in newMissions)
            {
                if (!catalog.missions.Contains(m))
                {
                    catalog.missions.Add(m);
                }
            }

            EditorUtility.SetDirty(catalog);
        }

        private static MissionStepData WithTime(MissionStepData step, float hour)
        {
            step.setTimeOfDayHours = hour;
            return step;
        }

        private static MissionStepData SportStep(string objectiveKey, string sportAnchorId)
        {
            return new MissionStepData
            {
                stepType = MissionStepType.PlaySportMatch,
                objectiveKey = objectiveKey,
                targetAnchorId = sportAnchorId,
                triggerRadius = 8f
            };
        }

        private static MissionStepData TimedInspect(string objectiveKey, string anchorId, DialogueData dialogue, float limitSeconds)
        {
            return new MissionStepData
            {
                stepType = MissionStepType.InspectTarget,
                objectiveKey = objectiveKey,
                targetAnchorId = anchorId,
                dialogueAsset = dialogue,
                requiresOnFoot = true,
                timeLimitSeconds = limitSeconds
            };
        }

        private static MissionStepData RallyGate(string objectiveKey, string anchorId, float radius, float limitSeconds)
        {
            return new MissionStepData
            {
                stepType = MissionStepType.ReachLocation,
                objectiveKey = objectiveKey,
                targetAnchorId = anchorId,
                triggerRadius = radius,
                requiresVehicle = true,
                timeLimitSeconds = limitSeconds
            };
        }
    }

    [InitializeOnLoad]
    internal static class OurWorldChapterFourFiveAutoInstall
    {
        static OurWorldChapterFourFiveAutoInstall()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                MissionCatalog catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>("Assets/GRIDROAM/Data/Missions/MissionCatalog.asset");
                if (catalog == null || catalog.GetById(OurWorldPrototypeSetup.ChapterFourFirstMissionId) != null) return;
                OurWorldPrototypeSetup.EnsureChapterFourFiveMissionsMenu();
            };
        }
    }
}
