using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gridroam.Core
{
    /// <summary>
    /// Lightweight JSON-backed localization service.
    /// Defaults to English ("en") and loads Resources/Localization/{lang}.json.
    /// Mission and dialogue text stay separated from C# logic so Turkish ("tr")
    /// or other languages can be added or updated cleanly without code changes.
    /// </summary>
    public class LocalizationService : MonoBehaviour
    {
        public static LocalizationService Instance { get; private set; }

        [SerializeField] private string defaultLanguageCode = "en";

        public string CurrentLanguage { get; private set; } = "en";
        public event Action<string> OnLanguageChanged;

        private readonly Dictionary<string, string> _table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            string initialLang = string.IsNullOrWhiteSpace(defaultLanguageCode) ? "en" : defaultLanguageCode;
            LoadLanguage(initialLang);
        }

        public bool LoadLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                languageCode = "en";
            }

            TextAsset asset = Resources.Load<TextAsset>($"Localization/{languageCode}");
            if (asset == null)
            {
                Debug.LogWarning($"[LocalizationService] Missing localization table 'Resources/Localization/{languageCode}.json'. Falling back to 'en'.");
                if (languageCode != "en")
                {
                    asset = Resources.Load<TextAsset>("Localization/en");
                    languageCode = "en";
                }
            }

            if (asset == null)
            {
                Debug.LogError("[LocalizationService] Could not load default English localization table.");
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

            return !string.IsNullOrEmpty(fallback) ? fallback : key;
        }

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
