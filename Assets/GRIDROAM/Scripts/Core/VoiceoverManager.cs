using System;
using System.Collections.Generic;
using OurWorld.Missions;
using OurWorld.UI;
using OurWorld.World;
using UnityEngine;

namespace OurWorld.Core
{
    /// <summary>
    /// Lightweight mobile voiceover manager playing offline Apache-2.0 neural Turkish speech clips
    /// from Resources/Audio/Voice/{lang}/:
    /// - 2D priority channel for story dialogues (M01-M26), NPC talks, and range announcer callouts
    /// - 3D spatialized channel for neighborhood speech bubbles with automatic gender & age voice matching
    /// - Bounded LRU clip cache (max 16 clips) so iOS/Android RAM stays minimal.
    /// </summary>
    public sealed class VoiceoverManager : MonoBehaviour
    {
        public static VoiceoverManager Instance { get; private set; }

        private const string PrefVoiceEnabledKey = "OurWorld_VoiceoverEnabled";
        private const int MaxCachedClips = 16;
        private const float MaxWorldVoiceDistance = 16.5f;

        private enum SpeakerVoiceBand
        {
            Unknown,
            YoungFemale,
            ElderFemale,
            YoungMale,
            ElderMale
        }

        [Serializable]
        private sealed class AmbientEntry
        {
            public string text;
            public string clipId;
            public string femaleClipId;
            public string elderFemaleClipId;
            public string maleClipId;
            public string elderMaleClipId;
        }

        [Serializable]
        private sealed class VoiceManifest
        {
            public string language;
            public AmbientEntry[] ambientEntries;
        }

        private static readonly string[] ElderFemaleTokens = { "teyze", "hanım", "nine", "ayşe", "hatice", "fatma" };
        private static readonly string[] YoungFemaleTokens =
        {
            "elif", "selin", "defne", "zeynep", "merve", "büşra", "derya", "irem", "sude", "gizem", "nisa", "ceren", "damla", "abla", "eczacı"
        };
        private static readonly string[] ElderMaleTokens = { "amca", "dede", "muhtar", "remzi", "kaptan", "rıza", "irfan", "cevdet", "mustafa" };
        private static readonly string[] YoungMaleTokens =
        {
            "ahmet", "kemal", "mehmet", "emre", "can", "burak", "hakan", "oğuz", "yusuf", "kadir", "tarık", "ömer",
            "berkay", "sinan", "murat", "hasan", "kerem", "efe", "deniz", "usta", "abi", "bey", "simitçi", "çaycı"
        };

        private AudioSource _dialogueSource;
        private AudioSource _worldSource;
        private Transform _worldEmitter;
        private Transform _worldSpeakerTarget;

        private readonly Dictionary<string, AmbientEntry> _ambientByText = new Dictionary<string, AmbientEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>(MaxCachedClips + 2, StringComparer.Ordinal);
        private readonly List<string> _cacheOrder = new List<string>(MaxCachedClips + 2);

        private string _loadedManifestLang = string.Empty;
        private float _lastWorldPlayTime = -99f;
        private float _lastCalloutTime = -99f;
        private string _lastWorldClipId = string.Empty;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(PrefVoiceEnabledKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(PrefVoiceEnabledKey, value ? 1 : 0);
                if (!value && Instance != null)
                {
                    Instance.StopAllVoicesInternal();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<OurWorld.Player.PlayerModeController>() == null) return;
            EnsureInstance();
        }

        public static VoiceoverManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("OurWorld_VoiceoverManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<VoiceoverManager>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _dialogueSource = gameObject.AddComponent<AudioSource>();
            _dialogueSource.spatialBlend = 0f;
            _dialogueSource.playOnAwake = false;
            _dialogueSource.loop = false;
            _dialogueSource.priority = 32;
            _dialogueSource.volume = 0.94f;

            GameObject emitterObj = new GameObject("WorldVoiceEmitter");
            emitterObj.transform.SetParent(transform, false);
            _worldEmitter = emitterObj.transform;

            _worldSource = emitterObj.AddComponent<AudioSource>();
            _worldSource.spatialBlend = 0.85f;
            _worldSource.rolloffMode = AudioRolloffMode.Linear;
            _worldSource.minDistance = 4.0f;
            _worldSource.maxDistance = 18.0f;
            _worldSource.dopplerLevel = 0f;
            _worldSource.playOnAwake = false;
            _worldSource.loop = false;
            _worldSource.priority = 96;
            _worldSource.volume = 0.86f;

            EnsureManifestLoaded(GetCurrentLanguage());
        }

        private void OnDestroy()
        {
            ClearClipCache();
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            if (_worldSource != null && _worldSource.isPlaying && _worldSpeakerTarget != null && _worldEmitter != null)
            {
                _worldEmitter.position = _worldSpeakerTarget.position + Vector3.up * 1.65f;
            }
        }

        /// <summary>
        /// Plays the voiceover for a story dialogue or quick NPC conversation line on the 2D dialogue channel.
        /// Stops any previously playing dialogue line immediately so lines never overlap.
        /// </summary>
        public static void PlayDialogue(string textKey, string resolvedText)
        {
            if (!Enabled) return;
            VoiceoverManager inst = EnsureInstance();
            inst.PlayDialogueInternal(textKey, resolvedText);
        }

        public static void StopDialogue()
        {
            if (Instance != null && Instance._dialogueSource != null)
            {
                Instance._dialogueSource.Stop();
            }
        }

        /// <summary>
        /// Plays a short 3D spatialized voiceover from a speaking neighborhood NPC if close enough
        /// and outside modal dialogues/menus. Matches speaker gender and age when variants exist.
        /// </summary>
        public static void PlayWorldSpeech(Transform speaker, string text, NpcBubbleMood mood, string speakerName = null)
        {
            if (!Enabled || speaker == null || string.IsNullOrWhiteSpace(text)) return;
            VoiceoverManager inst = EnsureInstance();
            inst.PlayWorldSpeechInternal(speaker, text, mood, speakerName);
        }

        /// <summary>
        /// Plays an announcer/gameplay callout clip (e.g., Shooting Range commands) by clip ID.
        /// </summary>
        public static void PlayCallout(string clipId, float minCooldownSeconds = 0.5f, float volume = 0.92f)
        {
            if (!Enabled || string.IsNullOrEmpty(clipId)) return;
            VoiceoverManager inst = EnsureInstance();
            inst.PlayCalloutInternal(clipId, minCooldownSeconds, volume);
        }

        private void PlayDialogueInternal(string textKey, string resolvedText)
        {
            if (_dialogueSource == null) return;
            _dialogueSource.Stop();
            if (_worldSource != null && _worldSource.isPlaying)
            {
                _worldSource.Stop();
            }

            string lang = GetCurrentLanguage();
            EnsureManifestLoaded(lang);

            AudioClip clip = null;
            if (!string.IsNullOrEmpty(textKey))
            {
                string keyClipId = textKey.Replace('.', '_');
                clip = LoadVoiceClip(lang, keyClipId);
            }

            if (clip == null && !string.IsNullOrEmpty(resolvedText) &&
                _ambientByText.TryGetValue(resolvedText.Trim(), out AmbientEntry ambEntry) &&
                ambEntry != null && !string.IsNullOrEmpty(ambEntry.clipId))
            {
                clip = LoadVoiceClip(lang, ambEntry.clipId);
            }

            if (clip != null)
            {
                _dialogueSource.volume = 0.94f;
                _dialogueSource.clip = clip;
                _dialogueSource.Play();
            }
        }

        private void PlayWorldSpeechInternal(Transform speaker, string text, NpcBubbleMood mood, string speakerName)
        {
            if (_worldSource == null || _worldEmitter == null) return;
            if (MainMenuController.IsMenuActive) return;
            if (MissionManager.Instance != null && MissionManager.Instance.IsDialoguePlaying) return;
            if (_dialogueSource != null && _dialogueSource.isPlaying) return;

            Camera cam = Camera.main;
            Vector3 listenerPos = cam != null
                ? cam.transform.position
                : (OurWorld.Player.PlayerModeController.Instance != null
                    ? OurWorld.Player.PlayerModeController.Instance.ActiveWorldPosition
                    : speaker.position);

            float sqrDist = (speaker.position - listenerPos).sqrMagnitude;
            if (sqrDist > MaxWorldVoiceDistance * MaxWorldVoiceDistance) return;

            float now = Time.time;
            bool highPriority = mood == NpcBubbleMood.Alert || mood == NpcBubbleMood.Mission;
            float cooldown = highPriority ? 1.15f : 2.65f;
            if (now - _lastWorldPlayTime < cooldown) return;
            if (_worldSource.isPlaying && !highPriority) return;

            string lang = GetCurrentLanguage();
            EnsureManifestLoaded(lang);

            if (!_ambientByText.TryGetValue(text.Trim(), out AmbientEntry entry) || entry == null)
            {
                return;
            }

            string clipId = ResolveClipForSpeaker(entry, speakerName, speaker);
            if (string.IsNullOrEmpty(clipId)) return;

            // Avoid repeating the exact same ambient clip back-to-back within 5 seconds
            if (clipId == _lastWorldClipId && now - _lastWorldPlayTime < 5.5f)
            {
                return;
            }

            AudioClip clip = LoadVoiceClip(lang, clipId);
            if (clip == null) return;

            _worldSpeakerTarget = speaker;
            _worldEmitter.position = speaker.position + Vector3.up * 1.65f;
            _worldSource.Stop();
            _worldSource.clip = clip;
            _worldSource.volume = highPriority ? 0.92f : 0.84f;
            _worldSource.Play();

            _lastWorldPlayTime = now;
            _lastWorldClipId = clipId;
        }

        private static string ResolveClipForSpeaker(AmbientEntry entry, string speakerName, Transform speaker)
        {
            SpeakerVoiceBand band = ClassifySpeakerVoice(speakerName, speaker);
            switch (band)
            {
                case SpeakerVoiceBand.ElderFemale:
                    if (!string.IsNullOrEmpty(entry.elderFemaleClipId)) return entry.elderFemaleClipId;
                    if (!string.IsNullOrEmpty(entry.femaleClipId)) return entry.femaleClipId;
                    break;
                case SpeakerVoiceBand.YoungFemale:
                    if (!string.IsNullOrEmpty(entry.femaleClipId)) return entry.femaleClipId;
                    if (!string.IsNullOrEmpty(entry.elderFemaleClipId)) return entry.elderFemaleClipId;
                    break;
                case SpeakerVoiceBand.ElderMale:
                    if (!string.IsNullOrEmpty(entry.elderMaleClipId)) return entry.elderMaleClipId;
                    if (!string.IsNullOrEmpty(entry.maleClipId)) return entry.maleClipId;
                    break;
                case SpeakerVoiceBand.YoungMale:
                    if (!string.IsNullOrEmpty(entry.maleClipId)) return entry.maleClipId;
                    if (!string.IsNullOrEmpty(entry.elderMaleClipId)) return entry.elderMaleClipId;
                    break;
            }
            return entry.clipId;
        }

        private static SpeakerVoiceBand ClassifySpeakerVoice(string speakerName, Transform speaker)
        {
            if (!string.IsNullOrEmpty(speakerName))
            {
                string low = speakerName.Replace('İ', 'i').Replace('I', 'ı').ToLowerInvariant();
                if (ContainsAnyToken(low, ElderFemaleTokens)) return SpeakerVoiceBand.ElderFemale;
                if (ContainsAnyToken(low, YoungFemaleTokens)) return SpeakerVoiceBand.YoungFemale;
                if (ContainsAnyToken(low, ElderMaleTokens)) return SpeakerVoiceBand.ElderMale;
                if (ContainsAnyToken(low, YoungMaleTokens)) return SpeakerVoiceBand.YoungMale;
            }

            if (speaker != null)
            {
                string goLow = speaker.name.ToLowerInvariant();
                if (goLow.Contains("female") || goLow.Contains("ayse") || goLow.Contains("elif") ||
                    goLow.Contains("selin") || goLow.Contains("defne") || goLow.Contains("pharmacist"))
                {
                    return goLow.Contains("elder") || goLow.Contains("ayse")
                        ? SpeakerVoiceBand.ElderFemale
                        : SpeakerVoiceBand.YoungFemale;
                }

                // Deterministic gender/age variety for unnamed crowd/spectator NPCs so each individual stays consistent
                int hash = Mathf.Abs(speaker.GetInstanceID());
                return (hash % 2 == 0) ? SpeakerVoiceBand.YoungFemale : SpeakerVoiceBand.YoungMale;
            }

            return SpeakerVoiceBand.Unknown;
        }

        private static bool ContainsAnyToken(string haystack, string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (haystack.IndexOf(tokens[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void PlayCalloutInternal(string clipId, float minCooldownSeconds, float volume)
        {
            if (_dialogueSource == null) return;
            float now = Time.time;
            if (now - _lastCalloutTime < minCooldownSeconds) return;

            string lang = GetCurrentLanguage();
            AudioClip clip = LoadVoiceClip(lang, clipId);
            if (clip == null) return;

            _dialogueSource.Stop();
            _dialogueSource.volume = Mathf.Clamp01(volume);
            _dialogueSource.clip = clip;
            _dialogueSource.Play();
            _lastCalloutTime = now;
        }

        private void StopAllVoicesInternal()
        {
            if (_dialogueSource != null) _dialogueSource.Stop();
            if (_worldSource != null) _worldSource.Stop();
        }

        private static string GetCurrentLanguage()
        {
            if (LocalizationService.Instance != null && !string.IsNullOrEmpty(LocalizationService.Instance.CurrentLanguage))
            {
                return LocalizationService.Instance.CurrentLanguage;
            }
            return "tr";
        }

        private void EnsureManifestLoaded(string lang)
        {
            if (_loadedManifestLang == lang) return;
            _loadedManifestLang = lang;
            _ambientByText.Clear();

            TextAsset asset = Resources.Load<TextAsset>($"Audio/Voice/{lang}/voice_manifest");
            if (asset == null || string.IsNullOrEmpty(asset.text)) return;

            VoiceManifest manifest = JsonUtility.FromJson<VoiceManifest>(asset.text);
            if (manifest?.ambientEntries == null) return;

            for (int i = 0; i < manifest.ambientEntries.Length; i++)
            {
                AmbientEntry e = manifest.ambientEntries[i];
                if (e != null && !string.IsNullOrEmpty(e.text) && !string.IsNullOrEmpty(e.clipId))
                {
                    _ambientByText[e.text] = e;
                }
            }
        }

        private AudioClip LoadVoiceClip(string lang, string clipId)
        {
            if (string.IsNullOrEmpty(clipId)) return null;
            string path = $"Audio/Voice/{lang}/{clipId}";

            if (_clipCache.TryGetValue(path, out AudioClip cached) && cached != null)
            {
                _cacheOrder.Remove(path);
                _cacheOrder.Add(path);
                return cached;
            }

            AudioClip loaded = Resources.Load<AudioClip>(path);
            if (loaded == null) return null;

            _clipCache[path] = loaded;
            _cacheOrder.Add(path);

            while (_cacheOrder.Count > MaxCachedClips)
            {
                string oldestKey = _cacheOrder[0];
                _cacheOrder.RemoveAt(0);
                if (_clipCache.TryGetValue(oldestKey, out AudioClip oldClip))
                {
                    _clipCache.Remove(oldestKey);
                    bool inUse = (_dialogueSource != null && _dialogueSource.clip == oldClip && _dialogueSource.isPlaying) ||
                                 (_worldSource != null && _worldSource.clip == oldClip && _worldSource.isPlaying);
                    if (oldClip != null && !inUse)
                    {
                        Resources.UnloadAsset(oldClip);
                    }
                }
            }

            return loaded;
        }

        private void ClearClipCache()
        {
            foreach (var kv in _clipCache)
            {
                if (kv.Value != null)
                {
                    Resources.UnloadAsset(kv.Value);
                }
            }
            _clipCache.Clear();
            _cacheOrder.Clear();
        }
    }
}
