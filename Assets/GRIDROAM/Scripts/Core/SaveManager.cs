using System;
using System.IO;
using UnityEngine;

namespace Gridroam.Core
{
    /// <summary>
    /// Persists GRIDROAM progress locally as JSON inside Application.persistentDataPath.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [SerializeField] private string saveFileName = "gridroam_save.json";

        public SaveData Data { get; private set; } = new SaveData();

        public event Action<SaveData> OnSaveLoaded;
        public event Action<SaveData> OnSaveUpdated;

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
                        if (Data.completedMissionIds == null)
                        {
                            Data.completedMissionIds = new System.Collections.Generic.List<string>();
                        }
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

            OnSaveLoaded?.Invoke(Data);
        }

        public void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(Data, true);
                File.WriteAllText(SavePath, json);
                OnSaveUpdated?.Invoke(Data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to save progress: {ex.Message}");
            }
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

        public void ResetSave()
        {
            Data = new SaveData();
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

            Save();
        }
    }
}
