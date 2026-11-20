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
        private const string IosBuildDir = "Builds/iOS/GRIDROAM_Xcode";

        [MenuItem("GRIDROAM/Build/Android - Build Test APK (Development)", priority = 20)]
        public static void BuildAndroidTestApk()
        {
            GridroamPrototypeSetup.ConfigureCrossPlatformMobileSettings();
            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("GRIDROAM Build", "No enabled scenes found. Run 'GRIDROAM -> 1. Build Garage-Harbor Prototype (Missions 1-2)' first.", "OK");
                return;
            }

            Directory.CreateDirectory(AndroidBuildDir);
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            string outputPath = Path.Combine(AndroidBuildDir, "GRIDROAM_Test.apk");
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
                EditorUtility.DisplayDialog("GRIDROAM Build", "No enabled scenes found. Run 'GRIDROAM -> 1. Build Garage-Harbor Prototype (Missions 1-2)' first.", "OK");
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

            string outputPath = Path.Combine(AndroidBuildDir, "GRIDROAM_GooglePlay.aab");
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
                EditorUtility.DisplayDialog("GRIDROAM Build", "No enabled scenes found. Run 'GRIDROAM -> 1. Build Garage-Harbor Prototype (Missions 1-2)' first.", "OK");
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
                if (s != null && s.enabled && !string.IsNullOrEmpty(s.path))
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

            // Avoid repeated manual export compliance prompts on TestFlight uploads.
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            root.SetBoolean("UIRequiresFullScreen", true);

            plist.WriteToFile(plistPath);
            Debug.Log("[GRIDROAM iOS Postprocess] Updated Info.plist (ITSAppUsesNonExemptEncryption=false, UIRequiresFullScreen=true).");
        }
    }
#endif
}
