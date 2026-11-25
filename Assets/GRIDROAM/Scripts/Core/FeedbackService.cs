using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace OurWorld.Core
{
    public enum FeedbackSubmitResult
    {
        Sent,
        Queued,
        TooSoon,
        TooShort
    }

    public struct CommunityMessage
    {
        public string SenderName;
        public string Category;
        public string Status;
        public string Text;
        public string CreatedAt;
    }

    /// <summary>
    /// Sends & reads player ideas, bug reports, and live community messages via Firebase Cloud Firestore REST API
    /// (zero native SDK bloat; works on Android, iOS & Editor). Auth: Firebase Anonymous Auth over REST.
    /// Config: Resources/FirebaseConfig.json { "projectId": "...", "apiKey": "...", "androidApiKey": "...", "iosApiKey": "...", "collection": "feedback" }.
    /// Offline / unconfigured submissions are queued in PlayerPrefs and flushed automatically later.
    /// </summary>
    public class FeedbackService : MonoBehaviour
    {
        public const int MinLength = 5;
        public const int MaxLength = 1000;
        public const int MaxSenderNameLength = 32;
        private const float MinSecondsBetweenSubmits = 15f;
        private const int MaxQueued = 20;

        private const string PrefQueue = "gr_fb_queue";
        private const string PrefRefresh = "gr_fb_refresh";
        private const string PrefUid = "gr_fb_uid";
        private const string PrefSenderName = "gr_fb_sender_name";

        public static FeedbackService Instance { get; private set; }

        [Serializable]
        private class FirebaseConfig
        {
            public string projectId;
            public string apiKey;
            public string androidApiKey;
            public string iosApiKey;
            public string androidAppId;
            public string iosAppId;
            public string collection = "feedback";

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
        }

        [Serializable]
        private class PendingFeedback
        {
            public string text;
            public string senderName;
            public string category;
            public string createdAt;
            public string appVersion;
            public string platform;
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
            _config != null && !string.IsNullOrWhiteSpace(_config.projectId) && !string.IsNullOrWhiteSpace(_config.ActiveApiKey);

        public int PendingCount => _queue.items.Count;

        public string SavedSenderName
        {
            get => PlayerPrefs.GetString(PrefSenderName, "Oyuncu");
            set
            {
                string clean = NormalizeSenderName(value);
                PlayerPrefs.SetString(PrefSenderName, clean);
                PlayerPrefs.Save();
            }
        }

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

            string cachedUid = PlayerPrefs.GetString(PrefUid, string.Empty);
            if (!string.IsNullOrEmpty(cachedUid))
            {
                CrashlyticsService.SetUserId(cachedUid);
            }
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

        /// <summary>Queues the feedback/message and tries to send it to Firestore right away.</summary>
        public FeedbackSubmitResult Submit(string category, string text, Action<bool> onSendFinished = null)
        {
            return Submit(category, text, SavedSenderName, onSendFinished);
        }

        public FeedbackSubmitResult Submit(string category, string text, string senderName, Action<bool> onSendFinished = null)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length < MinLength) return FeedbackSubmitResult.TooShort;
            if (Time.unscaledTime - _lastSubmitTime < MinSecondsBetweenSubmits) return FeedbackSubmitResult.TooSoon;

            if (text.Length > MaxLength) text = text.Substring(0, MaxLength);
            string cleanSender = NormalizeSenderName(senderName);
            SavedSenderName = cleanSender;
            _lastSubmitTime = Time.unscaledTime;

            SaveData save = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            string appVer = string.IsNullOrWhiteSpace(Application.version) ? "1.0.0" : Application.version.Trim();
            if (appVer.Length > 32) appVer = appVer.Substring(0, 32);

            string plat = Application.platform.ToString();
            if (plat.Length > 40) plat = plat.Substring(0, 40);

            string lang = LocalizationService.Instance != null ? LocalizationService.Instance.CurrentLanguage : "tr";
            if (string.IsNullOrWhiteSpace(lang) || lang.Length < 2) lang = "tr";
            if (lang.Length > 8) lang = lang.Substring(0, 8);

            PendingFeedback item = new PendingFeedback
            {
                text = text,
                senderName = cleanSender,
                category = NormalizeCategory(category),
                createdAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                appVersion = appVer,
                platform = plat,
                language = lang,
                completedMissions = Mathf.Clamp(save != null && save.completedMissionIds != null ? save.completedMissionIds.Count : 0, 0, 1000),
                playMinutes = Mathf.Clamp(save != null ? Mathf.RoundToInt(save.totalPlayTimeSeconds / 60f) : 0, 0, 1000000)
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

        /// <summary>Fetches the latest community messages/ideas from Firestore (newest first).</summary>
        public void FetchRecentMessages(int limit, Action<List<CommunityMessage>, string> onFinished)
        {
            if (onFinished == null) return;
            if (!IsConfigured)
            {
                onFinished(new List<CommunityMessage>(), LocalizationService.Tr("feedback.err.unconfigured", "Firebase ayarlı değil."));
                return;
            }
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                onFinished(new List<CommunityMessage>(), LocalizationService.Tr("feedback.err.offline", "İnternet bağlantısı yok."));
                return;
            }

            StartCoroutine(FetchMessagesRoutine(Mathf.Clamp(limit, 1, 50), onFinished));
        }

        private IEnumerator FetchMessagesRoutine(int limit, Action<List<CommunityMessage>, string> onFinished)
        {
            yield return EnsureAuth();
            if (string.IsNullOrEmpty(_idToken))
            {
                onFinished(new List<CommunityMessage>(), LocalizationService.Tr("feedback.err.auth", "Bağlantı doğrulanamadı. Lütfen internetinizi kontrol edip tekrar deneyin."));
                yield break;
            }

            string collection = string.IsNullOrWhiteSpace(_config.collection) ? "feedback" : _config.collection;
            string url = $"https://firestore.googleapis.com/v1/projects/{_config.projectId}/databases/(default)/documents/{collection}?pageSize={limit}&orderBy=createdAt%20desc";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 15;
                req.SetRequestHeader("Authorization", "Bearer " + _idToken);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Feedback] Firestore fetch failed: {req.responseCode} {req.error}");
                    if (req.responseCode == 401 || req.responseCode == 403)
                    {
                        _idToken = null;
                    }
                    onFinished(new List<CommunityMessage>(), string.Format(LocalizationService.Tr("feedback.err.fetch", "Mesajlar alınamadı ({0})."), req.responseCode));
                    yield break;
                }

                List<CommunityMessage> parsed = ParseFirestoreDocuments(req.downloadHandler.text);
                onFinished(parsed, null);
            }
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

        private static string NormalizeSenderName(string senderName)
        {
            string s = (senderName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s)) return LocalizationService.Tr("feedback.default_sender", "Oyuncu");
            if (s.Length > MaxSenderNameLength) s = s.Substring(0, MaxSenderNameLength);
            return s;
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
                onFinished?.Invoke(true);
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

            string activeKey = _config.ActiveApiKey;
            string refresh = PlayerPrefs.GetString(PrefRefresh, string.Empty);
            if (!string.IsNullOrEmpty(refresh))
            {
                string url = $"https://securetoken.googleapis.com/v1/token?key={activeKey}";
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

            string signUpUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={activeKey}";
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
            if (!string.IsNullOrEmpty(uid))
            {
                PlayerPrefs.SetString(PrefUid, uid);
                CrashlyticsService.SetUserId(uid);
            }
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
            AppendString(sb, "senderName", NormalizeSenderName(item.senderName)); sb.Append(',');
            AppendString(sb, "category", NormalizeCategory(item.category)); sb.Append(',');
            AppendString(sb, "status", "new"); sb.Append(',');
            AppendString(sb, "uid", uid); sb.Append(',');
            AppendString(sb, "appVersion", string.IsNullOrWhiteSpace(item.appVersion) ? "1.0.0" : item.appVersion); sb.Append(',');
            AppendString(sb, "platform", string.IsNullOrWhiteSpace(item.platform) ? Application.platform.ToString() : item.platform); sb.Append(',');
            AppendString(sb, "language", string.IsNullOrWhiteSpace(item.language) ? "tr" : item.language); sb.Append(',');
            sb.Append("\"completedMissions\":{\"integerValue\":\"").Append(Mathf.Clamp(item.completedMissions, 0, 1000)).Append("\"},");
            sb.Append("\"playMinutes\":{\"integerValue\":\"").Append(Mathf.Clamp(item.playMinutes, 0, 1000000)).Append("\"},");
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
                        _idToken = null;
                    }
                    if (req.responseCode == 400)
                    {
                        ok = true;
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

        // Lightweight zero-dependency parser for Firestore REST list-documents response
        private static List<CommunityMessage> ParseFirestoreDocuments(string json)
        {
            List<CommunityMessage> result = new List<CommunityMessage>();
            if (string.IsNullOrEmpty(json)) return result;

            int searchPos = 0;
            while (true)
            {
                int fieldsIdx = json.IndexOf("\"fields\"", searchPos, StringComparison.Ordinal);
                if (fieldsIdx < 0) break;

                int openBrace = json.IndexOf('{', fieldsIdx + 8);
                if (openBrace < 0) break;

                int closeBrace = FindMatchingBrace(json, openBrace);
                if (closeBrace < 0) break;

                string block = json.Substring(openBrace, closeBrace - openBrace + 1);
                string text = ExtractTypedField(block, "text", "stringValue");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Add(new CommunityMessage
                    {
                        Text = text,
                        SenderName = NormalizeSenderName(ExtractTypedField(block, "senderName", "stringValue")),
                        Category = NormalizeCategory(ExtractTypedField(block, "category", "stringValue")),
                        Status = ExtractTypedField(block, "status", "stringValue") ?? "new",
                        CreatedAt = ExtractTypedField(block, "createdAt", "timestampValue") ?? string.Empty
                    });
                }

                searchPos = closeBrace + 1;
            }

            return result;
        }

        private static int FindMatchingBrace(string s, int openIdx)
        {
            int depth = 0;
            bool inStr = false;
            for (int i = openIdx; i < s.Length; i++)
            {
                char c = s[i];
                if (inStr)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') { inStr = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static string ExtractTypedField(string block, string fieldName, string valueKey)
        {
            string fieldToken = $"\"{fieldName}\"";
            int idx = block.IndexOf(fieldToken, StringComparison.Ordinal);
            if (idx < 0) return null;

            int keyIdx = block.IndexOf($"\"{valueKey}\"", idx + fieldToken.Length, StringComparison.Ordinal);
            if (keyIdx < 0) return null;

            int colon = block.IndexOf(':', keyIdx + valueKey.Length + 2);
            if (colon < 0) return null;

            int qStart = block.IndexOf('"', colon + 1);
            if (qStart < 0) return null;

            StringBuilder sb = new StringBuilder();
            for (int i = qStart + 1; i < block.Length; i++)
            {
                char c = block[i];
                if (c == '"') return sb.ToString();
                if (c == '\\' && i + 1 < block.Length)
                {
                    char next = block[++i];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u' when i + 4 < block.Length:
                            string hex = block.Substring(i + 1, 4);
                            if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default: sb.Append(next); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
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
