using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Gridroam.Core
{
    /// <summary>
    /// Persists GRIDROAM progress locally as JSON inside Application.persistentDataPath,
    /// including economy, time of day, energy, fuel, stats, player/vehicle position,
    /// 60 s autosave, and mobile pause/quit persistence.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [SerializeField] private string saveFileName = "gridroam_save.json";
        [SerializeField] private float autoSaveIntervalSeconds = 60f;

        private float _autoSaveTimer;
        private bool _suppressSceneCapture;

        public SaveData Data { get; private set; } = new SaveData();
        public int LastMoneyDelta { get; private set; }

        public event Action<SaveData> OnSaveLoaded;
        public event Action<SaveData> OnBeforeSave;
        public event Action<SaveData> OnSaveUpdated;
        public event Action<int> OnMoneyChanged;

        private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Load();
        }

        private void Update()
        {
            float unscaledDt = Time.unscaledDeltaTime;
            if (unscaledDt > 0f && unscaledDt < 1f && Data != null)
            {
                Data.totalPlayTimeSeconds += unscaledDt;
                _autoSaveTimer += unscaledDt;
                if (autoSaveIntervalSeconds > 5f && _autoSaveTimer >= autoSaveIntervalSeconds)
                {
                    _autoSaveTimer = 0f;
                    Save();
                }
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    SaveData loaded = JsonUtility.FromJson<SaveData>(json);
                    if (loaded != null)
                    {
                        Data = loaded;
                        SanitizeAndMigrateLoadedData();
                    }
                    else
                    {
                        Data = new SaveData();
                    }
                }
                else
                {
                    Data = new SaveData();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Failed to load save file, resetting defaults. {ex.Message}");
                Data = new SaveData();
            }

            LastMoneyDelta = 0;
            OnSaveLoaded?.Invoke(Data);
        }

        private void SanitizeAndMigrateLoadedData()
        {
            if (Data.completedMissionIds == null)
            {
                Data.completedMissionIds = new List<string>();
            }
            if (Data.housePaint == null)
            {
                Data.housePaint = new List<HousePaintEntry>();
            }

            // Safely upgrade older schema v1 saves so new fields have valid starting defaults
            if (Data.schemaVersion < 2)
            {
                Data.schemaVersion = 2;
                Data.money = 250;
                Data.energy = 80f;
                Data.timeOfDayHours = 9.0f;
                Data.missionSedanFuel = 0.8f;
                Data.savedVehicleFuel = 0.8f;
                Data.languageCode = "tr";
                Data.hasSavedPlayerState = false;
            }

            // Clear stale world coordinates from pre-v8 map layouts so player spawns safely in the redesigned town
            if (Data.schemaVersion < 3)
            {
                Data.schemaVersion = 3;
                Data.hasSavedPlayerState = false;
            }

            if (string.IsNullOrWhiteSpace(Data.languageCode))
            {
                Data.languageCode = "tr";
            }

            Data.money = Mathf.Max(0, Data.money);
            Data.energy = Mathf.Clamp(Data.energy, 0f, 100f);
            Data.timeOfDayHours = Mathf.Repeat(Data.timeOfDayHours, 24f);
            Data.missionSedanFuel = Mathf.Clamp01(Data.missionSedanFuel);
            Data.savedVehicleFuel = Mathf.Clamp01(Data.savedVehicleFuel);
            Data.totalPlayTimeSeconds = Mathf.Max(0f, Data.totalPlayTimeSeconds);
        }

        public void Save()
        {
            try
            {
                if (!_suppressSceneCapture)
                {
                    OnBeforeSave?.Invoke(Data);
                }

                _autoSaveTimer = 0f;
                string json = JsonUtility.ToJson(Data, true);
                File.WriteAllText(SavePath, json);
                OnSaveUpdated?.Invoke(Data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to save progress: {ex.Message}");
            }
        }

        public void AddMoney(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Data.money += amount;
            LastMoneyDelta = amount;
            OnMoneyChanged?.Invoke(Data.money);
            Save();
        }

        public bool TrySpend(int cost)
        {
            if (cost < 0)
            {
                return false;
            }
            if (cost == 0)
            {
                return true;
            }
            if (Data.money < cost)
            {
                return false;
            }

            Data.money -= cost;
            LastMoneyDelta = -cost;
            OnMoneyChanged?.Invoke(Data.money);
            Save();
            return true;
        }

        public bool IsMissionCompleted(string missionId)
        {
            return !string.IsNullOrEmpty(missionId) && Data.completedMissionIds.Contains(missionId);
        }

        public void MarkMissionCompleted(string missionId, string nextMissionId)
        {
            if (!string.IsNullOrEmpty(missionId) && !Data.completedMissionIds.Contains(missionId))
            {
                Data.completedMissionIds.Add(missionId);
            }

            Data.nextMissionId = nextMissionId ?? string.Empty;
            Save();
        }

        public void SetVehicleKeysUnlocked(bool unlocked)
        {
            if (Data.hasVehicleKeys == unlocked)
            {
                return;
            }

            Data.hasVehicleKeys = unlocked;
            Save();
        }

        public void SetGraphicsTier(int tierIndex)
        {
            Data.graphicsTierIndex = Mathf.Clamp(tierIndex, 0, 1);
            Save();
        }

        public void SetLanguageCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            Data.languageCode = code;
            Save();
        }

        public void SetHouseColor(string id, Color color)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            if (Data.housePaint == null)
            {
                Data.housePaint = new List<HousePaintEntry>();
            }

            for (int i = 0; i < Data.housePaint.Count; i++)
            {
                HousePaintEntry existing = Data.housePaint[i];
                if (existing != null && string.Equals(existing.houseId, id, StringComparison.Ordinal))
                {
                    existing.r = color.r;
                    existing.g = color.g;
                    existing.b = color.b;
                    Save();
                    return;
                }
            }

            Data.housePaint.Add(new HousePaintEntry
            {
                houseId = id,
                r = color.r,
                g = color.g,
                b = color.b
            });
            Save();
        }

        public bool TryGetHouseColor(string id, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(id) || Data == null || Data.housePaint == null)
            {
                return false;
            }

            for (int i = 0; i < Data.housePaint.Count; i++)
            {
                HousePaintEntry entry = Data.housePaint[i];
                if (entry != null && string.Equals(entry.houseId, id, StringComparison.Ordinal))
                {
                    color = new Color(entry.r, entry.g, entry.b, 1f);
                    return true;
                }
            }

            return false;
        }

        public void ResetSave()
        {
            Data = new SaveData();
            LastMoneyDelta = 0;

            try
            {
                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Could not delete save file: {ex.Message}");
            }

            _suppressSceneCapture = true;
            Save();
            _suppressSceneCapture = false;

            OnMoneyChanged?.Invoke(Data.money);
        }
    }
}
