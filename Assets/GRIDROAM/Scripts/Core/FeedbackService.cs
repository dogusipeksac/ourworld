using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Gridroam.Core
{
    public enum FeedbackSubmitResult
    {
        Sent,
        Queued,
        TooSoon,
        TooShort
    }

    /// <summary>
    /// Sends player suggestions / bug reports to Firebase Cloud Firestore using the REST API
    /// (no Firebase SDK needed). Auth: Firebase Anonymous Auth over REST.
    /// Config: Resources/FirebaseConfig.json { "projectId": "...", "apiKey": "...", "collection": "feedback" }.
    /// Offline / unconfigured submissions are queued in PlayerPrefs and flushed later.
    /// </summary>
    public class FeedbackService : MonoBehaviour
    {
        public const int MinLength = 5;
        public const int MaxLength = 1000;
        private const float MinSecondsBetweenSubmits = 30f;
        private const int MaxQueued = 20;

        private const string PrefQueue = "gr_fb_queue";
        private const string PrefRefresh = "gr_fb_refresh";
        private const string PrefUid = "gr_fb_uid";

        public static FeedbackService Instance { get; private set; }

        [Serializable]
        private class FirebaseConfig
        {
            public string projectId;
            public string apiKey;
            public string collection = "feedback";
        }

        [Serializable]
        private class PendingFeedback
        {
            public string text;
            public string category;
            public string createdAt;
            public string appVersion;
            public string platform;
            public string deviceModel;
            public string language;
            public int completedMissions;
            public int playMinutes;
        }

        [Serializable]
        private class PendingQueue
        {
            public List<PendingFeedback> items = new List<PendingFeedback>();
        }

        [Serializable]
        private class SignUpResponse
        {
            public string idToken;
            public string refreshToken;
            public string expiresIn;
            public string localId;
        }

        [Serializable]
        private class RefreshResponse
        {
            public string id_token;
            public string refresh_token;
            public string expires_in;
            public string user_id;
        }

        private FirebaseConfig _config;
        private PendingQueue _queue = new PendingQueue();
        private string _idToken;
        private float _idTokenExpiresAt;
        private bool _flushing;
        private float _lastSubmitTime = -999f;

        public bool IsConfigured =>
            _config != null && !string.IsNullOrWhiteSpace(_config.projectId) && !string.IsNullOrWhiteSpace(_config.apiKey);

        public int PendingCount => _queue.items.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("FeedbackService");
            DontDestroyOnLoad(go);
            go.AddComponent<FeedbackService>();
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
            LoadQueue();
        }

        private void Start()
        {
            StartCoroutine(DelayedFlush(6f));
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && _queue.items.Count > 0 && !_flushing)
            {
                StartCoroutine(DelayedFlush(2f));
            }
        }

        /// <summary>Queues the feedback and tries to send it right away.</summary>
        public FeedbackSubmitResult Submit(string category, string text, Action<bool> onSendFinished = null)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length < MinLength) return FeedbackSubmitResult.TooShort;
            if (Time.unscaledTime - _lastSubmitTime < MinSecondsBetweenSubmits) return FeedbackSubmitResult.TooSoon;

            if (text.Length > MaxLength) text = text.Substring(0, MaxLength);
            _lastSubmitTime = Time.unscaledTime;

            SaveData save = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            PendingFeedback item = new PendingFeedback
            {
                text = text,
                category = NormalizeCategory(category),
                createdAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                appVersion = Application.version,
                platform = Application.platform.ToString(),
                deviceModel = SystemInfo.deviceModel,
                language = LocalizationService.Instance != null ? LocalizationService.Instance.CurrentLanguage : "tr",
                completedMissions = save != null && save.completedMissionIds != null ? save.completedMissionIds.Count : 0,
                playMinutes = save != null ? Mathf.RoundToInt(save.totalPlayTimeSeconds / 60f) : 0
            };

            _queue.items.Add(item);
            while (_queue.items.Count > MaxQueued) _queue.items.RemoveAt(0);
            SaveQueue();

            if (!IsConfigured || Application.internetReachability == NetworkReachability.NotReachable)
            {
                onSendFinished?.Invoke(false);
                return FeedbackSubmitResult.Queued;
            }

            StartCoroutine(FlushRoutine(onSendFinished));
            return FeedbackSubmitResult.Sent;
        }

        private static string NormalizeCategory(string category)
        {
            switch (category)
            {
                case "bug": return "bug";
                case "other": return "other";
                default: return "idea";
            }
        }

        private IEnumerator DelayedFlush(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (_queue.items.Count > 0 && IsConfigured && !_flushing)
            {
                yield return FlushRoutine(null);
            }
        }

        private IEnumerator FlushRoutine(Action<bool> onFinished)
        {
            if (_flushing)
            {
                onFinished?.Invoke(true); // already queued; current flush will pick it up
                yield break;
            }

            _flushing = true;
            bool allOk = true;

            yield return EnsureAuth();
            if (string.IsNullOrEmpty(_idToken))
            {
                _flushing = false;
                onFinished?.Invoke(false);
                yield break;
            }

            while (_queue.items.Count > 0)
            {
                PendingFeedback item = _queue.items[0];
                bool ok = false;
                yield return PostFeedback(item, r => ok = r);
                if (!ok)
                {
                    allOk = false;
                    break;
                }

                _queue.items.RemoveAt(0);
                SaveQueue();
            }

            _flushing = false;
            onFinished?.Invoke(allOk);
        }

        // ------------------------------------------------------------------ Firebase REST

        private IEnumerator EnsureAuth()
        {
            if (!string.IsNullOrEmpty(_idToken) && Time.unscaledTime < _idTokenExpiresAt - 60f)
            {
                yield break;
            }

            string refresh = PlayerPrefs.GetString(PrefRefresh, string.Empty);
            if (!string.IsNullOrEmpty(refresh))
            {
                string url = $"https://securetoken.googleapis.com/v1/token?key={_config.apiKey}";
                WWWForm form = new WWWForm();
                form.AddField("grant_type", "refresh_token");
                form.AddField("refresh_token", refresh);
                using (UnityWebRequest req = UnityWebRequest.Post(url, form))
                {
                    req.timeout = 15;
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        RefreshResponse r = JsonUtility.FromJson<RefreshResponse>(req.downloadHandler.text);
                        if (r != null && !string.IsNullOrEmpty(r.id_token))
                        {
                            StoreToken(r.id_token, r.refresh_token, r.expires_in, r.user_id);
                            yield break;
                        }
                    }
                }
            }

            string signUpUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_config.apiKey}";
            using (UnityWebRequest req = JsonPost(signUpUrl, "{\"returnSecureToken\":true}", null))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    SignUpResponse r = JsonUtility.FromJson<SignUpResponse>(req.downloadHandler.text);
                    if (r != null && !string.IsNullOrEmpty(r.idToken))
                    {
                        StoreToken(r.idToken, r.refreshToken, r.expiresIn, r.localId);
                    }
                }
                else
                {
                    Debug.LogWarning($"[Feedback] Anonymous sign-in failed: {req.responseCode} {req.error}");
                }
            }
        }

        private void StoreToken(string idToken, string refreshToken, string expiresIn, string uid)
        {
            _idToken = idToken;
            float seconds = 3600f;
            float.TryParse(expiresIn, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
            if (seconds <= 0f) seconds = 3600f;
            _idTokenExpiresAt = Time.unscaledTime + seconds;
            if (!string.IsNullOrEmpty(refreshToken)) PlayerPrefs.SetString(PrefRefresh, refreshToken);
            if (!string.IsNullOrEmpty(uid)) PlayerPrefs.SetString(PrefUid, uid);
            PlayerPrefs.Save();
        }

        private IEnumerator PostFeedback(PendingFeedback item, Action<bool> done)
        {
            string uid = PlayerPrefs.GetString(PrefUid, string.Empty);
            string collection = string.IsNullOrWhiteSpace(_config.collection) ? "feedback" : _config.collection;
            string url = $"https://firestore.googleapis.com/v1/projects/{_config.projectId}/databases/(default)/documents/{collection}";

            StringBuilder sb = new StringBuilder(512);
            sb.Append("{\"fields\":{");
            AppendString(sb, "text", item.text); sb.Append(',');
            AppendString(sb, "category", item.category); sb.Append(',');
            AppendString(sb, "status", "new"); sb.Append(',');
            AppendString(sb, "uid", uid); sb.Append(',');
            AppendString(sb, "appVersion", item.appVersion); sb.Append(',');
            AppendString(sb, "platform", item.platform); sb.Append(',');
            AppendString(sb, "deviceModel", item.deviceModel); sb.Append(',');
            AppendString(sb, "language", item.language); sb.Append(',');
            sb.Append("\"completedMissions\":{\"integerValue\":\"").Append(item.completedMissions).Append("\"},");
            sb.Append("\"playMinutes\":{\"integerValue\":\"").Append(item.playMinutes).Append("\"},");
            sb.Append("\"createdAt\":{\"timestampValue\":\"").Append(item.createdAt).Append("\"}");
            sb.Append("}}");

            using (UnityWebRequest req = JsonPost(url, sb.ToString(), _idToken))
            {
                yield return req.SendWebRequest();
                bool ok = req.result == UnityWebRequest.Result.Success;
                if (!ok)
                {
                    Debug.LogWarning($"[Feedback] Firestore write failed: {req.responseCode} {req.error} {req.downloadHandler?.text}");
                    if (req.responseCode == 401 || req.responseCode == 403)
                    {
                        _idToken = null; // force re-auth next time
                    }
                    if (req.responseCode == 400)
                    {
                        ok = true; // malformed item would block the queue forever; drop it
                    }
                }
                done(ok);
            }
        }

        private static UnityWebRequest JsonPost(string url, string json, string bearer)
        {
            UnityWebRequest req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 15
            };
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(bearer)) req.SetRequestHeader("Authorization", "Bearer " + bearer);
            return req;
        }

        private static void AppendString(StringBuilder sb, string field, string value)
        {
            sb.Append('"').Append(field).Append("\":{\"stringValue\":\"");
            AppendEscaped(sb, value ?? string.Empty);
            sb.Append("\"}");
        }

        private static void AppendEscaped(StringBuilder sb, string s)
        {
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ persistence

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
                Debug.LogWarning($"[Feedback] FirebaseConfig.json invalid: {e.Message}");
            }

            if (!IsConfigured)
            {
                Debug.LogWarning("[Feedback] Firebase is not configured (Resources/FirebaseConfig.json). Feedback will be queued locally.");
            }
        }

        private void LoadQueue()
        {
            string json = PlayerPrefs.GetString(PrefQueue, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                _queue = JsonUtility.FromJson<PendingQueue>(json) ?? new PendingQueue();
                if (_queue.items == null) _queue.items = new List<PendingFeedback>();
            }
            catch
            {
                _queue = new PendingQueue();
            }
        }

        private void SaveQueue()
        {
            PlayerPrefs.SetString(PrefQueue, JsonUtility.ToJson(_queue));
            PlayerPrefs.Save();
        }
    }
}
