using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Gridroam.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace Gridroam.Core
{
    /// <summary>
    /// Fetches Firebase Remote Config over REST (`ourworld-dd8dc`) on launch & foreground resume
    /// and displays OurWorld's fullscreen Maintenance / Force Update / Soft Update gate (Kube 2026 pattern).
    /// Zero C++ SDK dependencies — works identically on Android, iOS, and Unity Editor.
    /// </summary>
    public class AppUpdateService : MonoBehaviour
    {
        public static AppUpdateService Instance { get; private set; }

        private const float ReleaseFetchIntervalSeconds = 300f; // 5 minutes in release; instant in Debug/Editor
        private const string PrefAppInstanceId = "gr_rc_instance_id";

        [Serializable]
        private class FirebaseConfig
        {
            public string projectId;
            public string apiKey;
            public string androidApiKey;
            public string iosApiKey;
            public string androidAppId;
            public string iosAppId;

            public string ActiveApiKey
            {
                get
                {
#if UNITY_IOS && !UNITY_EDITOR
                    if (!string.IsNullOrWhiteSpace(iosApiKey)) return iosApiKey;
#elif UNITY_ANDROID && !UNITY_EDITOR
                    if (!string.IsNullOrWhiteSpace(androidApiKey)) return androidApiKey;
#endif
                    if (!string.IsNullOrWhiteSpace(apiKey)) return apiKey;
                    if (!string.IsNullOrWhiteSpace(androidApiKey)) return androidApiKey;
                    return iosApiKey;
                }
            }

            public string ActiveAppId
            {
                get
                {
#if UNITY_IOS
                    if (!string.IsNullOrWhiteSpace(iosAppId)) return iosAppId;
#endif
                    if (!string.IsNullOrWhiteSpace(androidAppId)) return androidAppId;
                    return iosAppId;
                }
            }
        }

        [Serializable]
        private class RemoteConfigFetchRequest
        {
            public string appId;
            public string appInstanceId;
            public string platformVersion;
            public string appVersion;
            public string languageCode;
            public string countryCode;
            public string sdkVersion;
        }

        private FirebaseConfig _config;
        private bool _fetching;
        private float _lastFetchTime = -9999f;
        private int _dismissedSoftVersion = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("AppUpdateService");
            DontDestroyOnLoad(go);
            go.AddComponent<AppUpdateService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            LoadConfig();
        }

        private void Start()
        {
            CheckForUpdates(forceNetwork: true);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) return;
            bool forceRefresh = AppUpdateOverlay.IsShowing || Debug.isDebugBuild;
            CheckForUpdates(forceNetwork: forceRefresh);
        }

        public void CheckForUpdates(bool forceNetwork = false)
        {
            if (_fetching) return;
            if (_config == null ||
                string.IsNullOrWhiteSpace(_config.projectId) ||
                string.IsNullOrWhiteSpace(_config.ActiveApiKey) ||
                string.IsNullOrWhiteSpace(_config.ActiveAppId))
            {
                return;
            }

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                return;
            }

            if (!forceNetwork && !Debug.isDebugBuild && Time.unscaledTime - _lastFetchTime < ReleaseFetchIntervalSeconds)
            {
                EvaluateAndDisplay();
                return;
            }

            StartCoroutine(FetchAndEvaluateRoutine());
        }

        private IEnumerator FetchAndEvaluateRoutine()
        {
            _fetching = true;

            string instanceId = PlayerPrefs.GetString(PrefAppInstanceId, string.Empty);
            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(PrefAppInstanceId, instanceId);
                PlayerPrefs.Save();
            }

            string url = $"https://firebaseremoteconfig.googleapis.com/v1/projects/{_config.projectId}/namespaces/firebase:fetch?key={UnityWebRequest.EscapeURL(_config.ActiveApiKey)}";
            string langCode = LocalizationService.Instance != null ? LocalizationService.Instance.CurrentLanguage : "tr";

            RemoteConfigFetchRequest payload = new RemoteConfigFetchRequest
            {
                appId = _config.ActiveAppId,
                appInstanceId = instanceId,
                platformVersion = SystemInfo.operatingSystem ?? "Unity",
                appVersion = Application.version ?? "1.0.0",
                languageCode = langCode,
                countryCode = "TR",
                sdkVersion = "22.0.0"
            };

            byte[] raw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using (UnityWebRequest req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 15;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    _lastFetchTime = Time.unscaledTime;
                    Dictionary<string, string> entries = ParseEntries(req.downloadHandler.text);
                    if (entries != null && entries.Count > 0)
                    {
                        UpdateDecisionRuntime.ApplyEntries(entries);
                        Debug.Log($"[OurWorld Update] Remote Config fetched ({entries.Count} keys, build={AppVersionCode.Current}).");
                        EvaluateAndDisplay();
                    }
                    else
                    {
                        // Empty Remote Config template -> hide any previous maintenance/force gate
                        UpdateDecisionRuntime.Clear();
                        AppUpdateOverlay.Hide();
                    }
                }
                else
                {
                    Debug.LogWarning($"[OurWorld Update] Remote Config fetch failed: {req.responseCode} {req.error}");
                }
            }

            _fetching = false;
        }

        private void EvaluateAndDisplay()
        {
            int currentBuild = AppVersionCode.Current;
            string lang = LocalizationService.Instance != null ? LocalizationService.Instance.CurrentLanguage : "tr";
            UpdateInfo info = UpdateDecisionRuntime.BuildUpdateInfo(currentBuild, lang);

            if (info == null)
            {
                AppUpdateOverlay.Hide();
                return;
            }

            if (!info.IsMaintenance && !info.ForceUpdate && info.LatestVersionCode == _dismissedSoftVersion)
            {
                return;
            }

            AppUpdateOverlay.Show(
                info,
                onSoftDismissed: () =>
                {
                    _dismissedSoftVersion = info.LatestVersionCode;
                },
                onRetryMaintenance: () =>
                {
                    CheckForUpdates(forceNetwork: true);
                });
        }

        private static Dictionary<string, string> ParseEntries(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            Match m = Regex.Match(json, "\"entries\"\\s*:\\s*\\{([\\s\\S]*?)\\}", RegexOptions.CultureInvariant);
            if (!m.Success) return null;

            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match kv in Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\""))
            {
                string key = UnescapeJson(kv.Groups[1].Value);
                string val = UnescapeJson(kv.Groups[2].Value);
                dict[key] = val;
            }
            return dict;
        }

        private static string UnescapeJson(string s) =>
            s.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");

        private void LoadConfig()
        {
            TextAsset asset = Resources.Load<TextAsset>("FirebaseConfig");
            if (asset == null) return;
            try
            {
                _config = JsonUtility.FromJson<FirebaseConfig>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OurWorld Update] FirebaseConfig.json invalid: {e.Message}");
            }
        }
    }
}
