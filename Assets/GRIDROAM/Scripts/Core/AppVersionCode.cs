using System;
using UnityEngine;

namespace Gridroam.Core
{
    /// <summary>
    /// Platform build versionCode (Android) / CFBundleVersion (iOS) — matches Kube 2026 AppVersionCode.
    /// Reads Android's live PackageInfo versionCode on device, or Resources/OurWorldBuildInfo.txt on iOS/Editor.
    /// </summary>
    public static class AppVersionCode
    {
        private const string ResourcePath = "OurWorldBuildInfo";

        public static int Current
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                int android = ReadAndroidVersionCode();
                if (android > 0) return android;
#endif
                return ReadFromResources();
            }
        }

        private static int ReadFromResources()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                return FallbackFromVersionName();
            }

            string text = asset.text.Trim();
#if UNITY_IOS
            foreach (string line in text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("ios=", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(line.Substring(4).Trim(), out int ios))
                {
                    return ios;
                }
            }
#else
            foreach (string line in text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("android=", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(line.Substring(8).Trim(), out int android))
                {
                    return android;
                }
            }
#endif
            return int.TryParse(text, out int single) ? single : FallbackFromVersionName();
        }

        private static int FallbackFromVersionName()
        {
            string[] parts = (Application.version ?? "1.0.0").Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out int a) ? a : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int b) ? b : 0;
            int patch = parts.Length > 2 && int.TryParse(parts[2], out int c) ? c : 0;
            return major * 10000 + minor * 100 + patch;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static int ReadAndroidVersionCode()
        {
            try
            {
                using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject pm = activity.Call<AndroidJavaObject>("getPackageManager");
                string packageName = activity.Call<string>("getPackageName");
                using AndroidJavaObject info = pm.Call<AndroidJavaObject>("getPackageInfo", packageName, 0);
                try
                {
                    return (int)info.Call<long>("getLongVersionCode");
                }
                catch
                {
                    return info.Get<int>("versionCode");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OurWorld] Android versionCode okunamadı: {e.Message}");
                return 0;
            }
        }
#endif
    }
}
