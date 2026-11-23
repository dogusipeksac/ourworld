using System.Collections.Generic;
using OurWorld.Missions;
using UnityEditor;
using UnityEngine;

namespace OurWorld.Editor
{
    /// <summary>
    /// Chapter 3 ("Hamidiye'nin Kalbi", Missions 11-14) spanning all 8 main Hamidiye avenues,
    /// Fatih Bulvarı Saat Kulesi Plaza, 53 Eyyubi Büfe, Aziziye Primary School, Petrol Yolu Ave,
    /// and the swimmable Hamidiye Open Swimming Pool!
    /// Matching NPCs, pickups and zones are spawned at runtime by <see cref="ChapterThreeWorldContent"/>.
    /// </summary>
    public static partial class OurWorldPrototypeSetup
    {
        public const string ChapterThreeFirstMissionId = "m11_saat_kulesi_dosyasi";

        [MenuItem("OurWorld/Görevler/Bölüm 3 Görevlerini Oluştur (11-14)")]
        public static void EnsureChapterThreeMissionsMenu()
        {
            MissionCatalog catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>($"{MissionDir}/MissionCatalog.asset");
            if (catalog == null)
            {
                Debug.LogError("[OurWorld] MissionCatalog.asset bulunamadı. Önce sahne kurulumunu çalıştırın.");
                return;
            }

            AppendChapterTwoMissions(catalog);
            AppendChapterThreeMissions(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[OurWorld] Bölüm 3 görevleri hazır: {catalog.missions.Count} görev.");
        }

        public static void AppendChapterThreeMissions(MissionCatalog catalog)
        {
            if (catalog == null) return;

            // --- Dialogues ---
            DialogueData d11a = Dlg("DLG_M11_Tarik_Intro", ("npc.tarik.name", "m11.dlg.a1"), ("npc.tarik.name", "m11.dlg.a2"));
            DialogueData d11b = Dlg("DLG_M11_ZoningFile", ("npc.player.name", "m11.dlg.b1"));
            DialogueData d11c = Dlg("DLG_M11_Tarik_Plan", ("npc.tarik.name", "m11.dlg.c1"));

            DialogueData d12a = Dlg("DLG_M12_SigEyyubi", ("npc.player.name", "m12.dlg.a1"));
            DialogueData d12b = Dlg("DLG_M12_SigAziziye", ("npc.player.name", "m12.dlg.b1"));
            DialogueData d12c = Dlg("DLG_M12_SigPetrol", ("npc.player.name", "m12.dlg.c1"));
            DialogueData d12d = Dlg("DLG_M12_Tarik_Done", ("npc.tarik.name", "m12.dlg.d1"), ("npc.tarik.name", "m12.dlg.d2"));

            DialogueData d13a = Dlg("DLG_M13_BakkalCrate", ("npc.player.name", "m13.dlg.a1"));
            DialogueData d13b = Dlg("DLG_M13_PoolDrop", ("npc.player.name", "m13.dlg.b1"));
            DialogueData d13c = Dlg("DLG_M13_Tarik_FinalCall", ("npc.tarik.name", "m13.dlg.c1"));

            DialogueData d14a = Dlg("DLG_M14_Charter", ("npc.player.name", "m14.dlg.a1"));
            DialogueData d14b = Dlg("DLG_M14_Victory", ("npc.muhtar.name", "m14.dlg.b1"), ("npc.muhtar.name", "m14.dlg.b2"), ("npc.muhtar.name", "m14.dlg.b3"));

            // --- Mission 11: Saat Kulesi'ndeki Dosya ---
            MissionData m11 = Mission("Mission_11_SaatKulesiDosyasi", ChapterThreeFirstMissionId, "m11", 550, "m12_sekiz_cadde_imza", true);
            m11.steps = new List<MissionStepData>
            {
                Reach("m11.step1.objective", "m11_saat_kulesi_zone", 16f, false, false),
                Talk("m11.step2.objective", "npc_avukat_tarik", d11a),
                Inspect("m11.step3.objective", "m11_zoning_dossier", d11b),
                Talk("m11.step4.objective", "npc_avukat_tarik", d11c)
            };

            // --- Mission 12: Sekiz Caddenin İmzası ---
            MissionData m12 = Mission("Mission_12_SekizCaddeImza", "m12_sekiz_cadde_imza", "m12", 650, "m13_havuz_senligi", true);
            m12.steps = new List<MissionStepData>
            {
                Inspect("m12.step1.objective", "m12_sig_eyyubi", d12a),
                Inspect("m12.step2.objective", "m12_sig_aziziye", d12b),
                Inspect("m12.step3.objective", "m12_sig_petrol", d12c),
                Talk("m12.step4.objective", "npc_avukat_tarik", d12d)
            };

            // --- Mission 13: Yaz Sürprizi · Havuz Günü ---
            MissionData m13 = Mission("Mission_13_HavuzSenligi", "m13_havuz_senligi", "m13", 700, "m14_hamidiye_zaferi", false);
            m13.steps = new List<MissionStepData>
            {
                Inspect("m13.step1.objective", "m13_summer_crate", d13a),
                Reach("m13.step2.objective", "m13_pool_deck_zone", 12f, false, false),
                Inspect("m13.step3.objective", "m13_pool_table", d13b),
                Talk("m13.step4.objective", "npc_avukat_tarik", d13c)
            };

            // --- Mission 14: Hamidiye'nin Zaferi (Büyük Final) ---
            MissionData m14 = Mission("Mission_14_HamidiyeZaferi", "m14_hamidiye_zaferi", "m14", 2000, string.Empty, false);
            m14.autoStartNextMission = false;
            m14.steps = new List<MissionStepData>
            {
                Inspect("m14.step1.objective", "m14_charter_folder", d14a),
                Talk("m14.step2.objective", "npc_muhtar_plaza", d14b)
            };

            foreach (MissionData m in new[] { m11, m12, m13, m14 })
            {
                EditorUtility.SetDirty(m);
            }

            // Link Chapter 2 finale (Mission 10) directly to Chapter 3 (Mission 11)!
            MissionData m10 = catalog.GetById("m10_mahalle_senligi");
            if (m10 != null)
            {
                m10.nextMissionId = ChapterThreeFirstMissionId;
                m10.autoStartNextMission = true;
                EditorUtility.SetDirty(m10);
            }

            foreach (MissionData m in new[] { m11, m12, m13, m14 })
            {
                if (!catalog.missions.Contains(m))
                {
                    catalog.missions.Add(m);
                }
            }

            EditorUtility.SetDirty(catalog);
        }
    }

    [InitializeOnLoad]
    internal static class OurWorldChapterThreeAutoInstall
    {
        static OurWorldChapterThreeAutoInstall()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                MissionCatalog catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>("Assets/GRIDROAM/Data/Missions/MissionCatalog.asset");
                if (catalog == null || catalog.GetById(OurWorldPrototypeSetup.ChapterThreeFirstMissionId) != null) return;
                OurWorldPrototypeSetup.EnsureChapterThreeMissionsMenu();
            };
        }
    }
}
