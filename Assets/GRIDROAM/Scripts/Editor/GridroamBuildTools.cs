using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_EDITOR_OSX
using UnityEditor.iOS.Xcode;
#endif

namespace Gridroam.Editor
{
    /// <summary>
    /// Manual cross-platform build menu commands for GRIDROAM:
    /// - Android Test APK (Development + Profiler)
    /// - Android Google Play AAB (Release, IL2CPP ARM64)
    /// - iOS Xcode Project export (ready for USB iPhone testing & TestFlight Archive)
    /// Nothing runs automatically unless the developer explicitly clicks a menu command.
    /// </summary>
    public static class GridroamBuildTools
    {
        private const string AndroidBuildDir = "Builds/Android";
        private const string IosBuildDir = "Builds/iOS/OurWorld_Xcode";

        [MenuItem("GRIDROAM/Build/0. Store Readiness Pre-Flight Check (v1.0.0)", priority = 19)]
        public static void RunStoreReadinessCheck()
        {
            GridroamPrototypeSetup.ConfigureCrossPlatformMobileSettings();
            List<string> ok = new List<string>();
            List<string> warn = new List<string>();

            string androidId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string iosId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            if (!string.IsNullOrEmpty(androidId) && androidId == iosId && !androidId.Contains("Default"))
                ok.Add($"Bundle ID: {androidId}");
            else
                warn.Add($"Bundle ID tutarsız: Android={androidId}, iOS={iosId}");

            if (!string.IsNullOrEmpty(PlayerSettings.companyName) && PlayerSettings.companyName != "DefaultCompany")
                ok.Add($"Şirket / Geliştirici: {PlayerSettings.companyName}");
            else
                warn.Add("companyName hâlâ 'DefaultCompany'!");

            ok.Add($"Sürüm: {PlayerSettings.bundleVersion} (Android VC={PlayerSettings.Android.bundleVersionCode}, iOS Build={PlayerSettings.iOS.buildNumber})");

            if (File.Exists(GridroamPrototypeSetup.AppIconAssetPath))
                ok.Add("Uygulama İkonu (1024x1024 RGB PNG): Mevcut ve PlayerSettings'e atandı.");
            else
                warn.Add($"Uygulama İkonu eksik: {GridroamPrototypeSetup.AppIconAssetPath}");

            if (File.Exists("Assets/Plugins/iOS/PrivacyInfo.xcprivacy"))
                ok.Add("iOS Privacy Manifest (PrivacyInfo.xcprivacy): Mevcut.");
            else
                warn.Add("iOS PrivacyInfo.xcprivacy eksik!");

            if (File.Exists("Docs/privacy_policy.html"))
                ok.Add("Gizlilik Politikası (Docs/privacy_policy.html): Mevcut.");
            else
                warn.Add("Gizlilik Politikası HTML dosyası eksik!");

            // Player feedback -> Firebase (Resources/FirebaseConfig.json)
            TextAsset fbConfig = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/GRIDROAM/Resources/FirebaseConfig.json");
            if (fbConfig != null && fbConfig.text.Contains("\"projectId\": \"\"") == false && fbConfig.text.Contains("\"apiKey\": \"\"") == false)
                ok.Add("Firebase öneri sistemi: FirebaseConfig.json dolu.");
            else
                warn.Add("Firebase öneri sistemi yapılandırılmadı: Assets/GRIDROAM/Resources/FirebaseConfig.json (projectId + apiKey). Öneriler yalnızca cihazda bekler!");

            // Mission catalog size (store v1 = 10 missions)
            Gridroam.Missions.MissionCatalog catalog = AssetDatabase.LoadAssetAtPath<Gridroam.Missions.MissionCatalog>("Assets/GRIDROAM/Data/Missions/MissionCatalog.asset");
            if (catalog != null && catalog.missions.Count >= 10)
                ok.Add($"Görev kataloğu: {catalog.missions.Count} görev.");
            else
                warn.Add($"Görev kataloğu eksik: {(catalog != null ? catalog.missions.Count : 0)}/10 (GRIDROAM > Görevler > Bölüm 2 Görevlerini Oluştur).");

            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 1 && scenes[0] == GridroamPrototypeSetup.TestScenePath)
                ok.Add($"Giriş Sahnesi: {scenes[0]} (SampleScene devre dışı).");
            else
                warn.Add($"Build Settings sahnelerini kontrol et (etkin sayı: {scenes.Length}).");

            if (PlayerSettings.Android.useCustomKeystore)
                ok.Add("Android Release Keystore: Etkin.");
            else
                warn.Add("Android Release Keystore henüz seçilmedi (Google Play AAB yüklemesi için Publishing Settings'ten keystore oluştur/seç).");

            string summary = "=== OURWORLD (v1.0.0) STORE HAZIRLIK RAPORU ===\n\n" +
                             "HAZIR (" + ok.Count + "):\n- " + string.Join("\n- ", ok) +
                             (warn.Count > 0 ? "\n\nDİKKAT / EKSİK (" + warn.Count + "):\n- " + string.Join("\n- ", warn) : "\n\nTüm otomatik kontroller geçti!");
            Debug.Log(summary);
            EditorUtility.DisplayDialog("OurWorld — Store Readiness", summary, "Tamam");
        }

        [MenuItem("GRIDROAM/Build/Android - Build Test APK (Development)", priority = 20)]
        public static void BuildAndroidTestApk()
        {
            GridroamPrototypeSetup.ConfigureCrossPlatformMobileSettings();
            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("GRIDROAM Build", "Etkin sahne bulunamadı. Önce 'GRIDROAM -> 1. Build Sultanbeyli Selman Sokağı (Görevler 1-5 + Yan İşler)' komutunu çalıştır.", "Tamam");
                return;
            }

            Directory.CreateDirectory(AndroidBuildDir);
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            string outputPath = Path.Combine(AndroidBuildDir, "OurWorld_Test.apk");
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging
            };

            ExecuteBuild(options, "Android Test APK");
        }

        [MenuItem("GRIDROAM/Build/Android - Build Google Play AAB (Release)", priority = 21)]
        public static void BuildAndroidPlayAab()
        {
            GridroamPrototypeSetup.ConfigureCrossPlatformMobileSettings();
            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("GRIDROAM Build", "Etkin sahne bulunamadı. Önce 'GRIDROAM -> 1. Build Sultanbeyli Selman Sokağı (Görevler 1-5 + Yan İşler)' komutunu çalıştır.", "Tamam");
                return;
            }

            if (!PlayerSettings.Android.useCustomKeystore)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Google Play Signing Notice",
                    "Custom Android Keystore is not enabled in Player Settings -> Publishing Settings.\n\nGoogle Play requires a release upload keystore for AAB submission. Continue with unsigned/debug AAB export anyway?",
                    "Continue",
                    "Cancel");
                if (!proceed) return;
            }

            Directory.CreateDirectory(AndroidBuildDir);
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            string outputPath = Path.Combine(AndroidBuildDir, "OurWorld_GooglePlay.aab");
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            ExecuteBuild(options, "Android Google Play AAB");
        }

        [MenuItem("GRIDROAM/Build/iOS - Export Xcode Project", priority = 22)]
        public static void ExportIosXcodeProject()
        {
            GridroamPrototypeSetup.ConfigureCrossPlatformMobileSettings();
            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("GRIDROAM Build", "Etkin sahne bulunamadı. Önce 'GRIDROAM -> 1. Build Sultanbeyli Selman Sokağı (Görevler 1-5 + Yan İşler)' komutunu çalıştır.", "Tamam");
                return;
            }

            Directory.CreateDirectory(IosBuildDir);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = IosBuildDir,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            ExecuteBuild(options, "iOS Xcode Project");
        }

        private static string[] GetEnabledScenes()
        {
            List<string> enabled = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s != null && s.enabled && !string.IsNullOrEmpty(s.path) && s.path != "Assets/Scenes/SampleScene.unity")
                {
                    enabled.Add(s.path);
                }
            }

            if (enabled.Count == 0 && File.Exists(GridroamPrototypeSetup.TestScenePath))
            {
                enabled.Add(GridroamPrototypeSetup.TestScenePath);
            }

            return enabled.ToArray();
        }

        private static void ExecuteBuild(BuildPlayerOptions options, string label)
        {
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[GRIDROAM Build] {label} succeeded: {summary.outputPath} ({summary.totalSize / (1024f * 1024f):0.0} MB)");
            }
            else
            {
                Debug.LogError($"[GRIDROAM Build] {label} finished with result: {summary.result}");
            }
        }
    }

#if UNITY_EDITOR_OSX
    /// <summary>
    /// Automatically configures exported iOS Xcode Info.plist for TestFlight & notched iPhone landscape safety.
    /// </summary>
    public class GridroamIosPostprocess : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            string plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            if (!File.Exists(plistPath))
            {
                return;
            }

            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            PlistElementDict root = plist.root;

            // Avoid repeated manual export compliance prompts on TestFlight / App Store uploads.
            root.SetString("CFBundleDisplayName", "OurWorld");
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            root.SetBoolean("UIRequiresFullScreen", true);

            plist.WriteToFile(plistPath);
            Debug.Log("[OurWorld iOS Postprocess] Updated Info.plist (CFBundleDisplayName=OurWorld, ITSAppUsesNonExemptEncryption=false, UIRequiresFullScreen=true).");
        }
    }
#endif
}
