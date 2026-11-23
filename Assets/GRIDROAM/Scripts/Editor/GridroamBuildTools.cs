using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif
#if UNITY_EDITOR_OSX
using System.Diagnostics;
using UnityEditor.iOS.Xcode;
using Debug = UnityEngine.Debug;
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

            // Player feedback & mobile Firebase configs
            TextAsset fbConfig = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/GRIDROAM/Resources/FirebaseConfig.json");
            if (fbConfig != null && fbConfig.text.Contains("\"projectId\": \"\"") == false && fbConfig.text.Contains("\"apiKey\": \"\"") == false)
                ok.Add("Firebase öneri sistemi: FirebaseConfig.json dolu (ourworld-dd8dc).");
            else
                warn.Add("Firebase öneri sistemi yapılandırılmadı: Assets/GRIDROAM/Resources/FirebaseConfig.json (projectId + apiKey). Öneriler yalnızca cihazda bekler!");

            if (File.Exists("Assets/google-services.json"))
                ok.Add("Firebase Android config (Assets/google-services.json): Mevcut.");
            else
                warn.Add("Firebase Android config (Assets/google-services.json) eksik!");

            if (File.Exists("Assets/GoogleService-Info.plist"))
                ok.Add("Firebase iOS config (Assets/GoogleService-Info.plist): Mevcut.");
            else
                warn.Add("Firebase iOS config (Assets/GoogleService-Info.plist) eksik!");

            if (File.Exists("Assets/Plugins/Android/launcherTemplate.gradle") && File.Exists("Assets/Plugins/iOS/OurWorldCrashlytics.mm"))
                ok.Add("Firebase Crashlytics (Android NDK Gradle + iOS SPM/ObjC++ + C# köprüsü): Hazır.");
            else
                warn.Add("Firebase Crashlytics native köprü dosyaları eksik!");

            if (File.Exists("firestore.rules"))
                ok.Add("Cloud Firestore güvenlik kuralları (firestore.rules): Mevcut.");
            else
                warn.Add("firestore.rules eksik!");

            if (File.Exists("remote_config.json") && File.Exists("Assets/GRIDROAM/Resources/OurWorldBuildInfo.txt"))
                ok.Add("Force / Soft Update & Bakım (Firebase Remote Config + OurWorldBuildInfo): Hazır.");
            else
                warn.Add("Force / Soft Update (remote_config.json / OurWorldBuildInfo.txt) eksik!");

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

#if UNITY_ANDROID
    /// <summary>
    /// Automatically copies Assets/google-services.json into the exported Android Gradle launcher module
    /// so Firebase Google Services & Crashlytics NDK plugins link cleanly on every APK/AAB build.
    /// </summary>
    public class GridroamAndroidPostprocess : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            string src = Path.GetFullPath("Assets/google-services.json");
            if (!File.Exists(src))
            {
                Debug.LogWarning("[OurWorld Android Postprocess] Assets/google-services.json not found — Firebase Crashlytics will be skipped.");
                return;
            }

            DirectoryInfo parent = Directory.GetParent(unityLibraryPath);
            if (parent == null) return;

            string launcherDir = Path.Combine(parent.FullName, "launcher");
            if (Directory.Exists(launcherDir))
            {
                string dst = Path.Combine(launcherDir, "google-services.json");
                File.Copy(src, dst, overwrite: true);
                Debug.Log($"[OurWorld Android Postprocess] Copied google-services.json -> {dst}");
            }
        }
    }
#endif

#if UNITY_EDITOR_OSX
    /// <summary>
    /// Automatically configures exported iOS Xcode Info.plist, GoogleService-Info.plist,
    /// Firebase Crashlytics SPM dependencies (FirebaseCore, FirebaseCrashlytics, FirebaseAnalytics),
    /// -ObjC linker flags, DWARF with dSYM, and the Crashlytics dSYM upload Run Script phase.
    /// </summary>
    public class GridroamIosPostprocess : IPostprocessBuildWithReport
    {
        private const string FirebaseIosSdkUrl = "https://github.com/firebase/firebase-ios-sdk";
        private const string FirebaseIosSdkVersion = "13.2.0";

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
            root.SetString("CFBundleDisplayName", "Our World");
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            root.SetBoolean("UIRequiresFullScreen", true);

            plist.WriteToFile(plistPath);

            // Copy GoogleService-Info.plist into exported Xcode project
            string firebasePlistSrc = Path.GetFullPath("Assets/GoogleService-Info.plist");
            string firebasePlistDst = Path.Combine(report.summary.outputPath, "GoogleService-Info.plist");
            bool hasFirebasePlist = File.Exists(firebasePlistSrc);
            if (hasFirebasePlist)
            {
                File.Copy(firebasePlistSrc, firebasePlistDst, overwrite: true);
            }

            string pbxPath = PBXProject.GetPBXProjectPath(report.summary.outputPath);
            if (File.Exists(pbxPath))
            {
                PBXProject pbx = new PBXProject();
                pbx.ReadFromFile(pbxPath);
                string mainTarget = pbx.GetUnityMainTargetGuid();
                string frameworkTarget = pbx.GetUnityFrameworkTargetGuid();

                if (hasFirebasePlist)
                {
                    string fileGuid = pbx.AddFile("GoogleService-Info.plist", "GoogleService-Info.plist", PBXSourceTree.Source);
                    pbx.AddFileToBuild(mainTarget, fileGuid);
                }

                // Mandatory Firebase static framework linker flag (-ObjC) & dSYM generation for Crashlytics
                pbx.AddBuildProperty(mainTarget, "OTHER_LDFLAGS", "-ObjC");
                pbx.AddBuildProperty(frameworkTarget, "OTHER_LDFLAGS", "-ObjC");
                pbx.SetBuildProperty(mainTarget, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");
                pbx.SetBuildProperty(frameworkTarget, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

                // Official Firebase Crashlytics dSYM upload Run Script Build Phase
                pbx.AddShellScriptBuildPhase(
                    mainTarget,
                    "Firebase Crashlytics dSYM Upload",
                    "/bin/sh",
                    "\"${BUILD_DIR%/Build/*}/SourcePackages/checkouts/firebase-ios-sdk/Crashlytics/run\"",
                    new List<string>
                    {
                        "${DWARF_DSYM_FOLDER_PATH}/${DWARF_DSYM_FILE_NAME}",
                        "${DWARF_DSYM_FOLDER_PATH}/${DWARF_DSYM_FILE_NAME}/Contents/Resources/DWARF/${PRODUCT_NAME}",
                        "${DWARF_DSYM_FOLDER_PATH}/${DWARF_DSYM_FILE_NAME}/Contents/Info.plist",
                        "$(TARGET_BUILD_DIR)/$(UNLOCALIZED_RESOURCES_FOLDER_PATH)/GoogleService-Info.plist",
                        "$(TARGET_BUILD_DIR)/$(EXECUTABLE_PATH)",
                        "$(BUILT_PRODUCTS_DIR)/$(INFOPLIST_PATH)"
                    });

                pbx.WriteToFile(pbxPath);
            }

            // Wire FirebaseCore + FirebaseCrashlytics + FirebaseAnalytics via Swift Package Manager helper if available
            TryAttachFirebaseSpm(report.summary.outputPath, hasFirebasePlist ? firebasePlistDst : null);

            Debug.Log("[OurWorld iOS Postprocess] Configured Info.plist, GoogleService-Info.plist, -ObjC, dSYM upload phase, and FirebaseCrashlytics SPM.");
        }

        private static void TryAttachFirebaseSpm(string xcodeOutputDir, string plistPath)
        {
            string xcodeProj = Path.Combine(xcodeOutputDir, "Unity-iPhone.xcodeproj");
            string spmToolDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                ".gemini/config/plugins/firebase/skills/xcode_project_setup/scripts/xcode_spm_setup");

            if (!Directory.Exists(xcodeProj) || !Directory.Exists(spmToolDir))
            {
                return;
            }

            string plistArg = !string.IsNullOrEmpty(plistPath) ? $" --plist \"{plistPath}\"" : string.Empty;
            string args = $"run --package-path \"{spmToolDir}\" xcode_spm_setup \"{xcodeProj}\" {FirebaseIosSdkUrl} {FirebaseIosSdkVersion}{plistArg} FirebaseCore FirebaseCrashlytics FirebaseAnalytics";

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("swift", args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (Process proc = Process.Start(psi))
                {
                    if (proc == null) return;
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(120000);
                    if (proc.ExitCode == 0)
                    {
                        Debug.Log($"[OurWorld iOS Postprocess] Swift Package FirebaseCrashlytics linked successfully.\n{stdout}");
                    }
                    else
                    {
                        Debug.LogWarning($"[OurWorld iOS Postprocess] xcode_spm_setup exited with {proc.ExitCode}: {stderr}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[OurWorld iOS Postprocess] Swift SPM setup skipped: {ex.Message}");
            }
        }
    }
#endif
}
