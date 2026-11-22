using System;
using System.Collections;
using System.Collections.Generic;
using Gridroam.UI;
using UnityEngine;

namespace Gridroam.Core
{
    [Serializable]
    public class ReleaseHighlightItem
    {
        public string id;
        public string category = "community";
        public string requestedBy;
        public string titleTr;
        public string titleEn;
        public string descriptionTr;
        public string descriptionEn;
        public bool fromLiveFirestore;

        public string GetTitle(bool isTr) =>
            isTr
                ? (!string.IsNullOrWhiteSpace(titleTr) ? titleTr : titleEn)
                : (!string.IsNullOrWhiteSpace(titleEn) ? titleEn : titleTr);

        public string GetDescription(bool isTr) =>
            isTr
                ? (!string.IsNullOrWhiteSpace(descriptionTr) ? descriptionTr : descriptionEn)
                : (!string.IsNullOrWhiteSpace(descriptionEn) ? descriptionEn : descriptionTr);
    }

    [Serializable]
    public class ReleaseHighlightsCatalog
    {
        public string version = "1.0.0";
        public int buildCode = 1;
        public string headerTr = "YENİ SÜRÜMDE NELER EKLENDİ?";
        public string headerEn = "WHAT'S NEW IN THIS VERSION";
        public List<ReleaseHighlightItem> items = new List<ReleaseHighlightItem>();
    }

    /// <summary>
    /// Loads bundled `Resources/ReleaseHighlights.json` AND merges any live Firestore `feedback`
    /// items whose `status` was marked `"done"` by the developer, then automatically presents
    /// the swipeable <see cref="WhatsNewOverlay"/> once per new app version!
    /// Players can also re-open it anytime via <see cref="ShowNow(bool)"/>.
    /// </summary>
    public class ReleaseNotesService : MonoBehaviour
    {
        public static ReleaseNotesService Instance { get; private set; }

        private const string PrefSeenVersionKey = "gr_whats_new_seen_version";
        private const string ResourcePath = "ReleaseHighlights";

        private ReleaseHighlightsCatalog _catalog;
        private readonly List<ReleaseHighlightItem> _mergedItems = new List<ReleaseHighlightItem>();
        private bool _autoShowChecked;

        public string VersionKey =>
            $"{(_catalog != null && !string.IsNullOrWhiteSpace(_catalog.version) ? _catalog.version : Application.version)}_{AppVersionCode.Current}";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("ReleaseNotesService");
            DontDestroyOnLoad(go);
            go.AddComponent<ReleaseNotesService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            LoadBundledCatalog();
        }

        private void Start()
        {
            StartCoroutine(AutoShowOnNewVersionRoutine());
        }

        private IEnumerator AutoShowOnNewVersionRoutine()
        {
            if (_autoShowChecked) yield break;
            _autoShowChecked = true;

            // Give startup & Remote Config update check a short head start so gates never overlap
            yield return new WaitForSecondsRealtime(1.6f);

            string seen = PlayerPrefs.GetString(PrefSeenVersionKey, string.Empty);
            if (seen == VersionKey)
            {
                yield break;
            }

            // Wait cleanly if a Force/Soft Update or Maintenance overlay is currently visible
            while (AppUpdateOverlay.IsShowing)
            {
                yield return new WaitForSecondsRealtime(0.5f);
            }

            ShowNow(markSeen: true);
        }

        /// <summary>Opens the swipeable "What's New" community showcase carousel.</summary>
        public void ShowNow(bool markSeen = false)
        {
            if (_catalog == null) LoadBundledCatalog();
            if (_catalog == null || _catalog.items == null || _catalog.items.Count == 0)
            {
                return;
            }

            StartCoroutine(PrepareAndShowRoutine(markSeen));
        }

        private IEnumerator PrepareAndShowRoutine(bool markSeen)
        {
            _mergedItems.Clear();

            // Fetch live Firestore `feedback` items marked status == "done" (best-effort, 2.5s cap so launch stays snappy)
            List<CommunityMessage> completedCommunity = null;
            bool fetchDone = false;

            if (FeedbackService.Instance != null && FeedbackService.Instance.IsConfigured &&
                Application.internetReachability != NetworkReachability.NotReachable)
            {
                FeedbackService.Instance.FetchRecentMessages(30, (msgs, _) =>
                {
                    if (msgs != null)
                    {
                        completedCommunity = new List<CommunityMessage>();
                        for (int i = 0; i < msgs.Count; i++)
                        {
                            if (string.Equals(msgs[i].Status, "done", StringComparison.OrdinalIgnoreCase))
                            {
                                completedCommunity.Add(msgs[i]);
                            }
                        }
                    }
                    fetchDone = true;
                });

                float wait = 0f;
                while (!fetchDone && wait < 2.2f)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // 1. Live Firestore completed community requests appear FIRST so credited players see their names right away!
            if (completedCommunity != null && completedCommunity.Count > 0)
            {
                int maxLive = Mathf.Min(completedCommunity.Count, 8);
                for (int i = 0; i < maxLive; i++)
                {
                    CommunityMessage m = completedCommunity[i];
                    string sender = string.IsNullOrWhiteSpace(m.SenderName) ? "Topluluk Oyuncusu" : m.SenderName.Trim();
                    string cat = m.Category == "bug" ? "mechanic" : "community";
                    _mergedItems.Add(new ReleaseHighlightItem
                    {
                        id = $"live_done_{i}",
                        category = cat,
                        requestedBy = sender,
                        titleTr = $"{sender} İstedi, Biz Ekledik!",
                        titleEn = $"Requested by {sender} — Now Live!",
                        descriptionTr = m.Text,
                        descriptionEn = m.Text,
                        fromLiveFirestore = true
                    });
                }
            }

            // 2. Append curated bundled release highlights
            for (int i = 0; i < _catalog.items.Count; i++)
            {
                if (_catalog.items[i] != null)
                {
                    _mergedItems.Add(_catalog.items[i]);
                }
            }

            if (_mergedItems.Count == 0) yield break;

            if (markSeen)
            {
                PlayerPrefs.SetString(PrefSeenVersionKey, VersionKey);
                PlayerPrefs.Save();
            }

            string lang = LocalizationService.Instance != null ? LocalizationService.Instance.CurrentLanguage : "tr";
            bool isTr = string.IsNullOrEmpty(lang) || lang.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
            string header = isTr ? _catalog.headerTr : _catalog.headerEn;

            WhatsNewOverlay.Show(header, _mergedItems, isTr);
        }

        private void LoadBundledCatalog()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                _catalog = new ReleaseHighlightsCatalog();
                return;
            }

            try
            {
                _catalog = JsonUtility.FromJson<ReleaseHighlightsCatalog>(asset.text) ?? new ReleaseHighlightsCatalog();
                if (_catalog.items == null) _catalog.items = new List<ReleaseHighlightItem>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ReleaseNotes] ReleaseHighlights.json parse hatası: {e.Message}");
                _catalog = new ReleaseHighlightsCatalog();
            }
        }
    }
}
