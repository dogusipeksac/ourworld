using System;
using System.Collections.Generic;
using UnityEngine;

namespace OurWorld.Core
{
    /// <summary>
    /// Lightweight JSON-backed localization service.
    /// Defaults to Turkish ("tr") as the primary language and loads Resources/Localization/{lang}.json,
    /// with English ("en") available as the secondary localization.
    /// </summary>
    public class LocalizationService : MonoBehaviour
    {
        public static LocalizationService Instance { get; private set; }

        [SerializeField] private string defaultLanguageCode = "tr";

        public string CurrentLanguage { get; private set; } = "tr";
        public event Action<string> OnLanguageChanged;

        private readonly Dictionary<string, string> _table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, (string tr, string en)> RuntimeFallbackTable
            = new Dictionary<string, (string tr, string en)>(StringComparer.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeTable() => RuntimeFallbackTable.Clear();

        public static void RegisterRuntimeText(string key, string trValue, string enValue)
        {
            if (string.IsNullOrEmpty(key)) return;
            RuntimeFallbackTable[key] = (trValue ?? string.Empty, enValue ?? trValue ?? string.Empty);
        }

        [Serializable]
        private class LocalizationEntry
        {
            public string key = string.Empty;
            public string value = string.Empty;
        }

        [Serializable]
        private class LocalizationTable
        {
            public string languageCode = string.Empty;
            public LocalizationEntry[] entries = Array.Empty<LocalizationEntry>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            string initialLang = defaultLanguageCode;
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null && !string.IsNullOrWhiteSpace(SaveManager.Instance.Data.languageCode))
            {
                initialLang = SaveManager.Instance.Data.languageCode;
            }
            if (string.IsNullOrWhiteSpace(initialLang))
            {
                initialLang = "tr";
            }

            LoadLanguage(initialLang);
        }

        private void Start()
        {
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                string savedLang = SaveManager.Instance.Data.languageCode;
                if (!string.IsNullOrWhiteSpace(savedLang) && !string.Equals(savedLang, CurrentLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    LoadLanguage(savedLang);
                }
            }
        }

        public bool LoadLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                languageCode = "tr";
            }

            TextAsset asset = Resources.Load<TextAsset>($"Localization/{languageCode}");
            if (asset == null)
            {
                Debug.LogWarning($"[LocalizationService] Missing localization table 'Resources/Localization/{languageCode}.json'. Falling back to 'tr'.");
                if (languageCode != "tr")
                {
                    asset = Resources.Load<TextAsset>("Localization/tr");
                    languageCode = "tr";
                }
                if (asset == null)
                {
                    asset = Resources.Load<TextAsset>("Localization/en");
                    languageCode = "en";
                }
            }

            if (asset == null)
            {
                Debug.LogError("[LocalizationService] Could not load localization table.");
                return false;
            }

            LocalizationTable parsed = JsonUtility.FromJson<LocalizationTable>(asset.text);
            _table.Clear();
            if (parsed != null && parsed.entries != null)
            {
                foreach (LocalizationEntry entry in parsed.entries)
                {
                    if (!string.IsNullOrEmpty(entry.key))
                    {
                        _table[entry.key] = entry.value ?? string.Empty;
                    }
                }
            }

            CurrentLanguage = languageCode;
            OnLanguageChanged?.Invoke(CurrentLanguage);
            return true;
        }

        public void ToggleLanguage()
        {
            string next = string.Equals(CurrentLanguage, "tr", StringComparison.OrdinalIgnoreCase) ? "en" : "tr";
            if (LoadLanguage(next) && SaveManager.Instance != null)
            {
                SaveManager.Instance.SetLanguageCode(CurrentLanguage);
            }
        }

        public string Get(string key, string fallback = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                return fallback ?? string.Empty;
            }

            if (_table.TryGetValue(key, out string value))
            {
                return value;
            }

            if (RuntimeFallbackTable.TryGetValue(key, out var rt))
            {
                return string.Equals(CurrentLanguage, "en", StringComparison.OrdinalIgnoreCase) ? rt.en : rt.tr;
            }

            return !string.IsNullOrEmpty(fallback) ? fallback : key;
        }

        public static bool IsEnglish =>
            Instance != null && string.Equals(Instance.CurrentLanguage, "en", StringComparison.OrdinalIgnoreCase);

        public static string Bi(string tr, string en) => IsEnglish ? en : tr;

        public static string Tr(string key, string fallback = null)
        {
            if (Instance != null)
            {
                return Instance.Get(key, fallback);
            }

            return !string.IsNullOrEmpty(fallback) ? fallback : key;
        }
    }
}
