using Gridroam.Player;
using UnityEngine;

namespace Gridroam.Core
{
    /// <summary>
    /// Tracks Hamidiye Mahallesi Reputation ("Mahalle Saygınlığı") across 5 ranks,
    /// unlocking passive job income bonuses and milestone celebrations as the player
    /// completes story chapters, discovers landmarks, collects memories, and helps neighbors.
    /// </summary>
    public static class NeighborhoodReputation
    {
        private static readonly int[] RankThresholds = { 0, 100, 250, 500, 900 };

        private static readonly string[] RankTitlesTr =
        {
            "Yeni Komşu",
            "Tanıdık Yüz",
            "Mahalle Dostu",
            "Hamidiye'nin Gururu",
            "Hamidiye Efsanesi"
        };

        private static readonly string[] RankTitlesEn =
        {
            "New Neighbor",
            "Familiar Face",
            "Neighborhood Friend",
            "Pride of Hamidiye",
            "Hamidiye Legend"
        };

        private static readonly float[] JobMultipliers = { 1.00f, 1.05f, 1.12f, 1.20f, 1.30f };

        public static int CurrentXp => SaveManager.Instance != null && SaveManager.Instance.Data != null
            ? Mathf.Max(0, SaveManager.Instance.Data.reputationXp)
            : 0;

        public static int GetRankIndex(int xp)
        {
            int idx = 0;
            for (int i = RankThresholds.Length - 1; i >= 0; i--)
            {
                if (xp >= RankThresholds[i])
                {
                    idx = i;
                    break;
                }
            }
            return idx;
        }

        public static int CurrentRankIndex => GetRankIndex(CurrentXp);

        public static string GetRankTitle(int rankIndex = -1)
        {
            int idx = Mathf.Clamp(rankIndex < 0 ? CurrentRankIndex : rankIndex, 0, RankTitlesTr.Length - 1);
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            return isEn ? RankTitlesEn[idx] : RankTitlesTr[idx];
        }

        public static float GetJobRewardMultiplier()
        {
            float baseMult = JobMultipliers[Mathf.Clamp(CurrentRankIndex, 0, JobMultipliers.Length - 1)];
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null && SaveManager.Instance.Data.chapterThreeCompleted)
            {
                baseMult += 0.10f;
            }
            return baseMult;
        }

        public static int GetNextRankThreshold()
        {
            int idx = CurrentRankIndex;
            return idx + 1 < RankThresholds.Length ? RankThresholds[idx + 1] : RankThresholds[RankThresholds.Length - 1];
        }

        public static void AddReputation(int deltaXp, string toastPrefix = null)
        {
            if (deltaXp <= 0 || SaveManager.Instance == null || SaveManager.Instance.Data == null) return;

            int oldRank = CurrentRankIndex;
            SaveManager.Instance.Data.reputationXp += deltaXp;
            int newRank = CurrentRankIndex;
            SaveManager.Instance.Save();

            if (newRank > oldRank)
            {
                GameFeelAudio.PlayMilestoneFanfare();
                if (PlayerModeController.Instance != null)
                {
                    string title = GetRankTitle(newRank);
                    int bonusPct = Mathf.RoundToInt((GetJobRewardMultiplier() - 1f) * 100f);
                    string msg = bonusPct > 0
                        ? $"Mahalle Saygınlığı Yükseldi: {title}! (İş geliri +%{bonusPct})"
                        : $"Mahalle Saygınlığı Yükseldi: {title}!";
                    PlayerModeController.Instance.RaiseStatusMessage(msg);
                    GameFeelAudio.SpawnBurst(
                        PlayerModeController.Instance.ActiveWorldPosition + Vector3.up * 1.4f,
                        new Color(1.0f, 0.84f, 0.24f),
                        count: 14,
                        spread: 0.7f);
                }
            }
            else if (!string.IsNullOrEmpty(toastPrefix) && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage($"{toastPrefix} (+{deltaXp} Saygınlık)");
            }
        }
    }
}
