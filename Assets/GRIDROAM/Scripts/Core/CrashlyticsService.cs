using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OurWorld.Core
{
    /// <summary>
    /// Cross-platform Firebase Crashlytics bridge for OurWorld (Android + iOS + Editor):
    /// - Android: bridges directly to native `com.google.firebase.crashlytics.FirebaseCrashlytics` (wired via Gradle BoM + NDK).
    /// - iOS: bridges directly to native `FIRCrashlytics` via `Assets/Plugins/iOS/OurWorldCrashlytics.mm` (wired via Xcode SPM).
    /// - Automatically forwards unhandled Unity C# exceptions & critical errors with breadcrumbs, scene, street, and user context.
    /// </summary>
    public class CrashlyticsService : MonoBehaviour
    {
        public static CrashlyticsService Instance { get; private set; }

        private const int MaxReportsPerSession = 40;
        private readonly HashSet<int> _reportedHashes = new HashSet<int>();
        private readonly object _lock = new object();
        private int _reportCount;
        private bool _initialized;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void OurWorld_CrashlyticsInit();
        [DllImport("__Internal")] private static extern void OurWorld_CrashlyticsLog(string message);
        [DllImport("__Internal")] private static extern void OurWorld_CrashlyticsSetKey(string key, string value);
        [DllImport("__Internal")] private static extern void OurWorld_CrashlyticsSetUserId(string userId);
        [DllImport("__Internal")] private static extern void OurWorld_CrashlyticsRecordException(string name, string reason, string stackTrace);
        [DllImport("__Internal")] private static extern void OurWorld_CrashlyticsForceTestCrash();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("CrashlyticsService");
            DontDestroyOnLoad(go);
            go.AddComponent<CrashlyticsService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeNative();
            Application.logMessageReceivedThreaded += HandleUnityLog;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Application.logMessageReceivedThreaded -= HandleUnityLog;
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }
        }

        private void InitializeNative()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                OurWorld_CrashlyticsInit();
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (AndroidJavaClass cls = new AndroidJavaClass("com.google.firebase.crashlytics.FirebaseCrashlytics"))
                using (AndroidJavaObject inst = cls.CallStatic<AndroidJavaObject>("getInstance"))
                {
                    inst?.Call("setCrashlyticsCollectionEnabled", true);
                }
#endif
                SetKey("app_version", Application.version);
                SetKey("unity_version", Application.unityVersion);
                SetKey("platform", Application.platform.ToString());
                SetKey("firebase_project", "ourworld-dd8dc");
                Log($"OurWorld Crashlytics initialized (v{Application.version}, {Application.platform}).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Crashlytics] Native init skipped or unavailable: {ex.Message}");
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SetKey("scene", scene.name);
            Log($"Scene loaded: {scene.name}");
        }

        private void HandleUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Assert)
            {
                return;
            }

            int hash = (condition ?? string.Empty).GetHashCode() ^ (stackTrace ?? string.Empty).GetHashCode();
            lock (_lock)
            {
                if (_reportCount >= MaxReportsPerSession || !_reportedHashes.Add(hash))
                {
                    return;
                }
                _reportCount++;
            }

            RecordException(type == LogType.Exception ? "UnityException" : "UnityAssert", condition, stackTrace);
        }

        public static void Log(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                OurWorld_CrashlyticsLog(message);
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (AndroidJavaClass cls = new AndroidJavaClass("com.google.firebase.crashlytics.FirebaseCrashlytics"))
                using (AndroidJavaObject inst = cls.CallStatic<AndroidJavaObject>("getInstance"))
                {
                    inst?.Call("log", message);
                }
#endif
            }
            catch
            {
                // Never throw inside crash logger
            }
        }

        public static void SetKey(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            value ??= string.Empty;
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                OurWorld_CrashlyticsSetKey(key, value);
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (AndroidJavaClass cls = new AndroidJavaClass("com.google.firebase.crashlytics.FirebaseCrashlytics"))
                using (AndroidJavaObject inst = cls.CallStatic<AndroidJavaObject>("getInstance"))
                {
                    inst?.Call("setCustomKey", key, value);
                }
#endif
            }
            catch
            {
            }
        }

        public static void SetUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                OurWorld_CrashlyticsSetUserId(userId);
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (AndroidJavaClass cls = new AndroidJavaClass("com.google.firebase.crashlytics.FirebaseCrashlytics"))
                using (AndroidJavaObject inst = cls.CallStatic<AndroidJavaObject>("getInstance"))
                {
                    inst?.Call("setUserId", userId);
                }
#endif
            }
            catch
            {
            }
        }

        public static void RecordException(string name, string message, string stackTrace)
        {
            name = string.IsNullOrEmpty(name) ? "UnityException" : name;
            message ??= string.Empty;
            stackTrace ??= string.Empty;

            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                OurWorld_CrashlyticsRecordException(name, message, stackTrace);
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (AndroidJavaClass cls = new AndroidJavaClass("com.google.firebase.crashlytics.FirebaseCrashlytics"))
                using (AndroidJavaObject inst = cls.CallStatic<AndroidJavaObject>("getInstance"))
                {
                    if (inst == null) return;
                    if (!string.IsNullOrEmpty(stackTrace))
                    {
                        inst.Call("log", $"[UnityStack]\n{stackTrace}");
                    }
                    using (AndroidJavaObject javaEx = new AndroidJavaObject("java.lang.Exception", $"{name}: {message}"))
                    {
                        inst.Call("recordException", javaEx);
                    }
                }
#endif
            }
            catch
            {
            }
        }

        /// <summary>
        /// Explicitly triggers a native test crash ONLY when called manually during QA Crashlytics verification.
        /// </summary>
        public static void ForceTestCrash()
        {
            Log("Manual QA ForceTestCrash triggered.");
#if UNITY_IOS && !UNITY_EDITOR
            OurWorld_CrashlyticsForceTestCrash();
#elif UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                activity?.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    throw new Exception("OurWorld Test Crash for Firebase Crashlytics verification");
                }));
            }
#else
            throw new Exception("OurWorld Editor Test Exception for Crashlytics");
#endif
        }
    }
}
