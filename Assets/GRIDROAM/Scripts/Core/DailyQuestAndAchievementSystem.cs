using System;
using System.Collections.Generic;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;

namespace OurWorld.Core
{
    public struct DailyQuestInfo
    {
        public int Index;
        public string TitleTr;
        public string TitleEn;
        public string DescTr;
        public string DescEn;
        public int Current;
        public int Target;
        public int RewardCash;
        public int RewardXp;
        public bool Claimed;
        public bool ReadyToClaim => !Claimed && Current >= Target;
    }

    public struct AchievementInfo
    {
        public string Id;
        public string TitleTr;
        public string TitleEn;
        public string DescTr;
        public string DescEn;
        public int Current;
        public int Target;
        public bool Unlocked => Current >= Target;
    }

    /// <summary>
    /// Persistent Daily Quests & 10 Neighborhood Achievements tracker.
    /// Automatically resets daily quests each calendar day, syncs passively with
    /// existing SaveData activity counters (jobs, fishing, farming, sports, street events),
    /// and exposes direct hooks for Mall purchases and NPC chats.
    /// </summary>
    public sealed class DailyQuestAndAchievementSystem : MonoBehaviour
    {
        public static DailyQuestAndAchievementSystem Instance { get; private set; }

        private int _lastSyncedJobsTotal = -1;
        private int _lastSyncedSportsEventsTotal = -1;
        private float _syncTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("OurWorld_DailyQuestsAndAchievements");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DailyQuestAndAchievementSystem>();
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
            EnsureTodayReset();
            CaptureBaselines();
        }

        private void Update()
        {
            _syncTimer -= Time.unscaledDeltaTime;
            if (_syncTimer > 0f) return;
            _syncTimer = 1.5f;
            SyncFromSaveCounters();
        }

        public static void EnsureTodayReset()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return;

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (!string.Equals(d.dailyQuestDate, today, StringComparison.Ordinal))
            {
                d.dailyQuestDate = today;
                d.dailyMallOrShopCount = 0;
                d.dailyJobsCount = 0;
                d.dailySocialOrSportCount = 0;
                d.dailyQuest0Claimed = false;
                d.dailyQuest1Claimed = false;
                d.dailyQuest2Claimed = false;
                SaveManager.Instance.Save();
            }
        }

        private void CaptureBaselines()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return;
            _lastSyncedJobsTotal = ComputeJobsCounter(d);
            _lastSyncedSportsEventsTotal = ComputeSportsAndEventsCounter(d);
        }

        private static int ComputeJobsCounter(SaveData d)
        {
            return d.deliveriesDone + d.taxiFaresDone + d.housesPaintedCount + d.litterCollectedCount
                 + d.totalFishCaught + d.totalCropsHarvested + d.totalAnimalProductsCollected + d.totalCarWashes;
        }

        private static int ComputeSportsAndEventsCounter(SaveData d)
        {
            return d.footballGoalsScored + d.footballWins + d.volleyballWins + d.tennisWins
                 + d.swimWins + d.streetEventsHelpedCount;
        }

        private void SyncFromSaveCounters()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return;

            EnsureTodayReset();
            int curJobs = ComputeJobsCounter(d);
            int curSports = ComputeSportsAndEventsCounter(d);

            if (_lastSyncedJobsTotal < 0)
            {
                _lastSyncedJobsTotal = curJobs;
                _lastSyncedSportsEventsTotal = curSports;
                return;
            }

            bool dirty = false;
            if (curJobs > _lastSyncedJobsTotal)
            {
                int delta = curJobs - _lastSyncedJobsTotal;
                _lastSyncedJobsTotal = curJobs;
                int before = d.dailyJobsCount;
                d.dailyJobsCount += delta;
                dirty = true;
                if (before < 1 && d.dailyJobsCount >= 1 && !d.dailyQuest1Claimed)
                {
                    NotifyQuestReady("Mahalle Emeği", "Neighborhood Hustle");
                }
            }
            else if (curJobs < _lastSyncedJobsTotal)
            {
                _lastSyncedJobsTotal = curJobs;
            }

            if (curSports > _lastSyncedSportsEventsTotal)
            {
                int delta = curSports - _lastSyncedSportsEventsTotal;
                _lastSyncedSportsEventsTotal = curSports;
                int before = d.dailySocialOrSportCount;
                d.dailySocialOrSportCount += delta;
                dirty = true;
                if (before < 2 && d.dailySocialOrSportCount >= 2 && !d.dailyQuest2Claimed)
                {
                    NotifyQuestReady("Sosyal & Aktif", "Social & Active");
                }
            }
            else if (curSports < _lastSyncedSportsEventsTotal)
            {
                _lastSyncedSportsEventsTotal = curSports;
            }

            if (dirty) SaveManager.Instance.Save();
        }

        public static void RecordMallPurchase()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return;
            EnsureTodayReset();
            int before = d.dailyMallOrShopCount;
            d.dailyMallOrShopCount++;
            d.totalMallPurchases++;
            SaveManager.Instance.Save();
            if (before < 2 && d.dailyMallOrShopCount >= 2 && !d.dailyQuest0Claimed)
            {
                NotifyQuestReady("AVM & Esnaf Turu", "Mall & Shop Run");
            }
        }

        public static void RecordNpcChatOrArcade()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return;
            EnsureTodayReset();
            int before = d.dailySocialOrSportCount;
            d.dailySocialOrSportCount++;
            d.totalNpcChats++;
            SaveManager.Instance.Save();
            if (before < 2 && d.dailySocialOrSportCount >= 2 && !d.dailyQuest2Claimed)
            {
                NotifyQuestReady("Sosyal & Aktif", "Social & Active");
            }
        }

        private static void NotifyQuestReady(string trName, string enName)
        {
            if (PlayerModeController.Instance == null) return;
            bool en = LocalizationService.IsEnglish;
            PlayerModeController.Instance.RaiseStatusMessage(en
                ? $"Daily Quest Complete: {enName}! Claim your reward in the Phone!"
                : $"Günlük Görev Tamamlandı: {trName}! Telefondan ödülünü al!");
        }

        public static int GetUnclaimedReadyCount()
        {
            DailyQuestInfo[] quests = GetDailyQuests();
            int count = 0;
            for (int i = 0; i < quests.Length; i++)
            {
                if (quests[i].ReadyToClaim) count++;
            }
            return count;
        }

        public static DailyQuestInfo[] GetDailyQuests()
        {
            EnsureTodayReset();
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            int q0 = d != null ? d.dailyMallOrShopCount : 0;
            int q1 = d != null ? d.dailyJobsCount : 0;
            int q2 = d != null ? d.dailySocialOrSportCount : 0;

            return new[]
            {
                new DailyQuestInfo
                {
                    Index = 0,
                    TitleTr = "AVM & Esnaf Turu",
                    TitleEn = "Mall & Shop Run",
                    DescTr = "Hamidiye Atlas AVM mağazalarından 2 alışveriş yap (kahve, yemek veya moda)",
                    DescEn = "Buy 2 items at Hamidiye Atlas Mall (coffee, food court, or fashion)",
                    Current = Mathf.Min(q0, 2),
                    Target = 2,
                    RewardCash = 90,
                    RewardXp = 25,
                    Claimed = d != null && d.dailyQuest0Claimed
                },
                new DailyQuestInfo
                {
                    Index = 1,
                    TitleTr = "Mahalle Emeği",
                    TitleEn = "Neighborhood Hustle",
                    DescTr = "1 ek iş, balık tutma, tarla hasadı veya araç yıkama tamamla",
                    DescEn = "Complete 1 side job, catch a fish, harvest crops, or wash your car",
                    Current = Mathf.Min(q1, 1),
                    Target = 1,
                    RewardCash = 120,
                    RewardXp = 35,
                    Claimed = d != null && d.dailyQuest1Claimed
                },
                new DailyQuestInfo
                {
                    Index = 2,
                    TitleTr = "Sosyal & Aktif",
                    TitleEn = "Social & Active",
                    DescTr = "2 komşuyla sohbet et, sinema/arcade oyna veya spor maçına katıl",
                    DescEn = "Chat with 2 neighbors, play arcade/cinema, or join a sports match",
                    Current = Mathf.Min(q2, 2),
                    Target = 2,
                    RewardCash = 85,
                    RewardXp = 25,
                    Claimed = d != null && d.dailyQuest2Claimed
                }
            };
        }

        public static bool TryClaimDailyQuest(int questIndex)
        {
            SaveManager sm = SaveManager.Instance;
            if (sm == null || sm.Data == null) return false;
            DailyQuestInfo[] quests = GetDailyQuests();
            if (questIndex < 0 || questIndex >= quests.Length) return false;
            DailyQuestInfo q = quests[questIndex];
            if (!q.ReadyToClaim) return false;

            if (questIndex == 0) sm.Data.dailyQuest0Claimed = true;
            else if (questIndex == 1) sm.Data.dailyQuest1Claimed = true;
            else if (questIndex == 2) sm.Data.dailyQuest2Claimed = true;

            sm.AddMoney(q.RewardCash);
            NeighborhoodReputation.AddReputation(q.RewardXp);
            sm.Save();

            GameFeelAudio.PlayCoinChime();
            if (PlayerModeController.Instance != null)
            {
                Vector3 fxPos = PlayerModeController.Instance.ActiveWorldPosition + Vector3.up * 1.25f;
                GameFeelAudio.SpawnBurst(fxPos, new Color(0.97f, 0.78f, 0.24f), 14, 0.55f);
                bool en = LocalizationService.IsEnglish;
                PlayerModeController.Instance.RaiseStatusMessage(en
                    ? $"Daily Reward Claimed! +{q.RewardCash} ₺ · +{q.RewardXp} XP"
                    : $"Günlük Ödül Alındı! +{q.RewardCash} ₺ · +{q.RewardXp} XP");
            }
            return true;
        }

        public static AchievementInfo[] GetAchievements()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            int missions = d != null && d.completedMissionIds != null ? d.completedMissionIds.Count : 0;
            int landmarks = LandmarkDiscoverySystem.DiscoveredCount;
            int memories = NeighborhoodCollectibleManager.CollectedCount;
            int jobs = d != null ? (d.deliveriesDone + d.taxiFaresDone + d.housesPaintedCount + d.litterCollectedCount) : 0;
            int mallBuys = d != null ? d.totalMallPurchases : 0;
            int sports = d != null ? (d.footballWins + d.volleyballWins + d.tennisWins + d.swimWins + d.footballGoalsScored) : 0;
            int fish = d != null ? d.totalFishCaught : 0;
            int carsOwned = d != null && d.ownedVehicleIds != null ? Mathf.Max(1, d.ownedVehicleIds.Count) : 1;
            int styleAccessories = d != null ? ((d.ownsSunglasses ? 1 : 0) + (d.ownsStreetCap ? 1 : 0)) : 0;
            int repXp = NeighborhoodReputation.CurrentXp;

            return new[]
            {
                new AchievementInfo { Id = "ach_story1", TitleTr = "Mahalleye İlk Adım", TitleEn = "First Steps", DescTr = "İlk hikâye görevini tamamla", DescEn = "Complete your first story mission", Current = Mathf.Min(missions, 1), Target = 1 },
                new AchievementInfo { Id = "ach_explore", TitleTr = "Hamidiye Kâşifi", TitleEn = "District Explorer", DescTr = "5 farklı mahalle noktası keşfet", DescEn = "Discover 5 neighborhood landmarks", Current = Mathf.Min(landmarks, 5), Target = 5 },
                new AchievementInfo { Id = "ach_memory", TitleTr = "Hatıra Koleksiyoncusu", TitleEn = "Memory Collector", DescTr = "5 mahalle hatırası topla", DescEn = "Collect 5 neighborhood memories", Current = Mathf.Min(memories, 5), Target = 5 },
                new AchievementInfo { Id = "ach_worker", TitleTr = "Çalışkan Komşu", TitleEn = "Neighborhood Pro", DescTr = "Toplam 5 ek iş tamamla", DescEn = "Complete 5 side jobs", Current = Mathf.Min(jobs, 5), Target = 5 },
                new AchievementInfo { Id = "ach_mall", TitleTr = "Atlas AVM Müdavimi", TitleEn = "Atlas Mall Regular", DescTr = "AVM mağazalarından 3 alışveriş yap", DescEn = "Make 3 purchases at Atlas Mall", Current = Mathf.Min(mallBuys, 3), Target = 3 },
                new AchievementInfo { Id = "ach_sport", TitleTr = "Saha Yıldızı", TitleEn = "Arena Star", DescTr = "3 spor galibiyeti veya gol kaydet", DescEn = "Score 3 sports wins or goals", Current = Mathf.Min(sports, 3), Target = 3 },
                new AchievementInfo { Id = "ach_angler", TitleTr = "Gölet Usta Balıkçısı", TitleEn = "Lake Master Angler", DescTr = "Sultanbeyli Göleti'nde 3 balık tut", DescEn = "Catch 3 fish at Sultanbeyli Lake", Current = Mathf.Min(fish, 3), Target = 3 },
                new AchievementInfo { Id = "ach_garage", TitleTr = "Oto Galeri Koleksiyoncusu", TitleEn = "Garage Collector", DescTr = "Hamidiye Oto Galeri'den yeni araç al (2+ araç)", DescEn = "Own 2+ cars from Hamidiye Auto Gallery", Current = Mathf.Min(carsOwned, 2), Target = 2 },
                new AchievementInfo { Id = "ach_style", TitleTr = "İstanbul Tarzı", TitleEn = "Istanbul Style Icon", DescTr = "3D Güneş Gözlüğü veya Şapka edin", DescEn = "Own 3D Sunglasses or Street Cap", Current = Mathf.Min(styleAccessories, 1), Target = 1 },
                new AchievementInfo { Id = "ach_legend", TitleTr = "Hamidiye Efsanesi", TitleEn = "Hamidiye Legend", DescTr = "500 Mahalle Saygınlığı XP'sine ulaş", DescEn = "Reach 500 Neighborhood Reputation XP", Current = Mathf.Min(repXp, 500), Target = 500 }
            };
        }
    }
}
