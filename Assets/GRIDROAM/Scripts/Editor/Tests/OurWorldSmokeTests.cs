using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using NUnit.Framework;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Missions;
using OurWorld.Player;
using OurWorld.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OurWorld.Editor.Tests
{
    /// <summary>
    /// Fast, deterministic EditMode regression suite guarding localization parity,
    /// Remote Config update decisions, URP shader inclusion, voiceover manifest integrity,
    /// mobile privacy/store compliance, and main scene health.
    /// </summary>
    public sealed class OurWorldSmokeTests
    {
        [Serializable]
        private sealed class LocEntry { public string key; public string value; }
        [Serializable]
        private sealed class LocTable { public LocEntry[] entries; }

        [Serializable]
        private sealed class VoiceAmbientEntry
        {
            public string text;
            public string clipId;
            public string femaleClipId;
            public string elderFemaleClipId;
            public string maleClipId;
            public string elderMaleClipId;
        }
        [Serializable]
        private sealed class VoiceManifestDoc
        {
            public string language;
            public VoiceAmbientEntry[] ambientEntries;
        }

        [Serializable]
        private sealed class HighlightItem
        {
            public string id;
            public string titleTr;
            public string titleEn;
            public string descriptionTr;
            public string descriptionEn;
        }
        [Serializable]
        private sealed class HighlightsDoc
        {
            public string version;
            public int buildCode;
            public HighlightItem[] items;
        }

        [Test]
        public void Localization_TrAndEn_HaveExactKeyParity_AndNoEmptyValues()
        {
            TextAsset trAsset = Resources.Load<TextAsset>("Localization/tr");
            TextAsset enAsset = Resources.Load<TextAsset>("Localization/en");
            Assert.IsNotNull(trAsset, "Resources/Localization/tr.json must exist");
            Assert.IsNotNull(enAsset, "Resources/Localization/en.json must exist");

            LocTable tr = JsonUtility.FromJson<LocTable>(trAsset.text);
            LocTable en = JsonUtility.FromJson<LocTable>(enAsset.text);
            Assert.IsNotNull(tr?.entries);
            Assert.IsNotNull(en?.entries);

            Dictionary<string, string> trMap = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, string> enMap = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (LocEntry e in tr.entries)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.key), "Empty TR key detected");
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.value), $"Empty TR value for key '{e.key}'");
                trMap[e.key] = e.value;
            }
            foreach (LocEntry e in en.entries)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.key), "Empty EN key detected");
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.value), $"Empty EN value for key '{e.key}'");
                enMap[e.key] = e.value;
            }

            foreach (string k in trMap.Keys)
            {
                Assert.IsTrue(enMap.ContainsKey(k), $"Key '{k}' exists in tr.json but is missing in en.json");
            }
            foreach (string k in enMap.Keys)
            {
                Assert.IsTrue(trMap.ContainsKey(k), $"Key '{k}' exists in en.json but is missing in tr.json");
            }

            Assert.IsTrue(trMap.ContainsKey("sports.tennis.title"), "sports.tennis.title must exist");
            Assert.IsTrue(trMap.ContainsKey("ui.menu.set_voice"), "ui.menu.set_voice must exist");
            Assert.IsTrue(trMap.ContainsKey("ui.menu.set_invert_y"), "ui.menu.set_invert_y must exist");
            Assert.IsTrue(trMap.ContainsKey("ui.menu.set_subtitles"), "ui.menu.set_subtitles must exist");
            Assert.IsTrue(trMap.ContainsKey("feedback.err.offline"), "feedback.err.offline must exist");
        }

        [Test]
        public void UpdateDecisionRuntime_EvaluatesMaintenanceForceSoftAndStoreUrl()
        {
            Assert.IsTrue(
                UpdateConfigKeys.DefaultIosStoreUrl.Contains("id6838324963"),
                "DefaultIosStoreUrl should point directly to Our World App Store ID 6838324963");

            try
            {
                // 1. Maintenance overrides everything
                UpdateDecisionRuntime.ApplyEntries(new Dictionary<string, string>
                {
                    { UpdateConfigKeys.MaintenanceAndroidEnabled, "true" },
                    { UpdateConfigKeys.MaintenanceIosEnabled, "true" },
                    { UpdateConfigKeys.MaintTitleEn, "Scheduled Maintenance" }
                });
                UpdateInfo maint = UpdateDecisionRuntime.BuildUpdateInfo(currentVersionCode: 99, languageCode: "en");
                Assert.IsNotNull(maint);
                Assert.IsTrue(maint.IsMaintenance);
                Assert.IsTrue(maint.ForceUpdate);
                Assert.AreEqual("Scheduled Maintenance", maint.Title);

                // 2. Force update when current < minRequired and force flag true
                UpdateDecisionRuntime.ApplyEntries(new Dictionary<string, string>
                {
                    { UpdateConfigKeys.AndroidLatest, "10" },
                    { UpdateConfigKeys.AndroidMin, "8" },
                    { UpdateConfigKeys.AndroidForce, "true" },
                    { UpdateConfigKeys.IosLatest, "10" },
                    { UpdateConfigKeys.IosMin, "8" },
                    { UpdateConfigKeys.IosForce, "true" }
                });
                UpdateInfo forced = UpdateDecisionRuntime.BuildUpdateInfo(currentVersionCode: 7, languageCode: "tr");
                Assert.IsNotNull(forced);
                Assert.IsFalse(forced.IsMaintenance);
                Assert.IsTrue(forced.ForceUpdate);
                Assert.IsNull(forced.CancelButtonText);

                // 3. Soft update when current >= minRequired but < latest
                UpdateInfo soft = UpdateDecisionRuntime.BuildUpdateInfo(currentVersionCode: 8, languageCode: "tr");
                Assert.IsNotNull(soft);
                Assert.IsFalse(soft.ForceUpdate);
                Assert.IsFalse(string.IsNullOrEmpty(soft.CancelButtonText));

                // 4. Up-to-date returns null
                UpdateInfo upToDate = UpdateDecisionRuntime.BuildUpdateInfo(currentVersionCode: 10, languageCode: "tr");
                Assert.IsNull(upToDate);
            }
            finally
            {
                UpdateDecisionRuntime.Clear();
            }
        }

        [Test]
        public void GraphicsAndMobileSettings_HaveUrpShadersAndZeroAccelerometerDrain()
        {
            Assert.IsNotNull(Shader.Find("Universal Render Pipeline/Lit"), "URP Lit shader must resolve");
            Assert.IsNotNull(Shader.Find("Universal Render Pipeline/Unlit"), "URP Unlit shader must resolve");
            Assert.IsNotNull(Shader.Find("Universal Render Pipeline/Simple Lit"), "URP Simple Lit shader must resolve");
            Assert.IsNotNull(Shader.Find("Universal Render Pipeline/Particles/Unlit"), "URP Particles/Unlit shader must resolve");
            Assert.AreEqual(0, PlayerSettings.accelerometerFrequency, "Unused accelerometer should stay disabled (0 Hz) to save mobile battery");
        }

        [Test]
        public void VoiceoverManifest_AllReferencedTurkishClipsExist()
        {
            TextAsset manifestAsset = Resources.Load<TextAsset>("Audio/Voice/tr/voice_manifest");
            Assert.IsNotNull(manifestAsset, "Resources/Audio/Voice/tr/voice_manifest.json must exist");

            VoiceManifestDoc doc = JsonUtility.FromJson<VoiceManifestDoc>(manifestAsset.text);
            Assert.IsNotNull(doc?.ambientEntries);
            Assert.Greater(doc.ambientEntries.Length, 20, "Voice manifest should contain ambient entries");

            string voiceDir = Path.Combine(Application.dataPath, "GRIDROAM/Resources/Audio/Voice/tr");
            HashSet<string> checkedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (VoiceAmbientEntry entry in doc.ambientEntries)
            {
                string[] ids = { entry.clipId, entry.femaleClipId, entry.elderFemaleClipId, entry.maleClipId, entry.elderMaleClipId };
                foreach (string id in ids)
                {
                    if (string.IsNullOrEmpty(id) || !checkedIds.Add(id)) continue;
                    bool exists = File.Exists(Path.Combine(voiceDir, id + ".wav"))
                               || File.Exists(Path.Combine(voiceDir, id + ".ogg"))
                               || File.Exists(Path.Combine(voiceDir, id + ".mp3"));
                    Assert.IsTrue(exists, $"Voice clip '{id}' referenced in voice_manifest.json is missing on disk");
                }
            }
        }

        [Test]
        public void VoiceoverManifest_AllReferencedEnglishClipsExist_AndLicensesDocumented()
        {
            TextAsset manifestAsset = Resources.Load<TextAsset>("Audio/Voice/en/voice_manifest");
            Assert.IsNotNull(manifestAsset, "Resources/Audio/Voice/en/voice_manifest.json must exist");

            VoiceManifestDoc doc = JsonUtility.FromJson<VoiceManifestDoc>(manifestAsset.text);
            Assert.IsNotNull(doc?.ambientEntries);
            Assert.Greater(doc.ambientEntries.Length, 20, "English voice manifest should contain ambient entries");

            string voiceDir = Path.Combine(Application.dataPath, "GRIDROAM/Resources/Audio/Voice/en");
            HashSet<string> checkedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (VoiceAmbientEntry entry in doc.ambientEntries)
            {
                string[] ids = { entry.clipId, entry.femaleClipId, entry.elderFemaleClipId, entry.maleClipId, entry.elderMaleClipId };
                foreach (string id in ids)
                {
                    if (string.IsNullOrEmpty(id) || !checkedIds.Add(id)) continue;
                    bool exists = File.Exists(Path.Combine(voiceDir, id + ".wav"))
                               || File.Exists(Path.Combine(voiceDir, id + ".ogg"))
                               || File.Exists(Path.Combine(voiceDir, id + ".mp3"));
                    Assert.IsTrue(exists, $"English voice clip '{id}' referenced in en/voice_manifest.json is missing on disk");
                }
            }

            // Spot-check critical English story & shooting range clips
            string[] mustExist = { "m01_dlg_1", "range_start_classic", "range_bullseye", "range_gold" };
            foreach (string id in mustExist)
            {
                Assert.IsTrue(File.Exists(Path.Combine(voiceDir, id + ".wav")), $"Expected English voice clip '{id}.wav'");
            }

            string licensePath = Path.Combine(Application.dataPath, "GRIDROAM/Resources/Audio/Voice/THIRD_PARTY_VOICE_LICENSES.txt");
            Assert.IsTrue(File.Exists(licensePath));
            string licText = File.ReadAllText(licensePath);
            Assert.IsTrue(licText.Contains("en_US-ljspeech-medium") && licText.Contains("Public Domain"),
                "THIRD_PARTY_VOICE_LICENSES.txt must document Public Domain / CC0 English models");
        }

        [Test]
        public void IosLaunchScreen_IsConfiguredInProjectSettings()
        {
            const string launchPath = "Assets/GRIDROAM/Art/UI/OurWorld_LaunchScreen.png";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(launchPath);
            Assert.IsNotNull(tex, "Branded iOS Launch Screen asset must exist at " + launchPath);

            UnityEngine.Object[] psAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            Assert.IsNotNull(psAssets);
            Assert.Greater(psAssets.Length, 0);

            SerializedObject so = new SerializedObject(psAssets[0]);
            Assert.AreEqual(1, so.FindProperty("iOSLaunchScreenType").intValue, "iPhone Launch Screen type should be Image & Background (1)");
            Assert.AreEqual(tex, so.FindProperty("iOSLaunchScreenLandscape").objectReferenceValue, "iPhone Landscape Launch Screen must reference OurWorld_LaunchScreen.png");
            Assert.AreEqual(1, so.FindProperty("iOSLaunchScreeniPadType").intValue, "iPad Launch Screen type should be Image & Background (1)");
            Assert.AreEqual(tex, so.FindProperty("iOSLaunchScreeniPadImage").objectReferenceValue, "iPad Launch Screen must reference OurWorld_LaunchScreen.png");
        }

        [Test]
        public void AccessibilityPreferences_InvertYAndSubtitles_PersistCleanly()
        {
            bool prevSub = GameHUDController.LargeSubtitles;
            int prevInv = PlayerPrefs.GetInt("ow_cam_invert_y", 0);
            try
            {
                GameObject tempGo = new GameObject("TempInputHubTest");
                try
                {
                    InputHub hub = tempGo.AddComponent<InputHub>();
                    hub.SetInvertY(true);
                    Assert.IsTrue(hub.InvertY);
                    Assert.AreEqual(1, PlayerPrefs.GetInt("ow_cam_invert_y", 0));

                    hub.SetInvertY(false);
                    Assert.IsFalse(hub.InvertY);
                    Assert.AreEqual(0, PlayerPrefs.GetInt("ow_cam_invert_y", 0));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tempGo);
                }

                GameObject hudGo = new GameObject("TempHudTest");
                try
                {
                    GameHUDController hud = hudGo.AddComponent<GameHUDController>();
                    hud.SetLargeSubtitles(true);
                    Assert.IsTrue(GameHUDController.LargeSubtitles);
                    hud.SetLargeSubtitles(false);
                    Assert.IsFalse(GameHUDController.LargeSubtitles);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(hudGo);
                }
            }
            finally
            {
                PlayerPrefs.SetInt("ow_cam_invert_y", prevInv);
                PlayerPrefs.SetInt("ow_subtitle_large", prevSub ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void PrivacyAndReleaseMetadata_AreConsistentAndValid()
        {
            string gradlePath = Path.Combine(Application.dataPath, "Plugins/Android/launcherTemplate.gradle");
            Assert.IsTrue(File.Exists(gradlePath));
            string gradle = File.ReadAllText(gradlePath);
            Assert.IsFalse(gradle.Contains("firebase-analytics"),
                "Android launcherTemplate.gradle must not bundle firebase-analytics (conflicts with no-ADID privacy policy)");
            Assert.IsTrue(gradle.Contains("firebase-crashlytics-ndk"),
                "Android launcherTemplate.gradle should keep firebase-crashlytics-ndk");

            string xcprivacyPath = Path.Combine(Application.dataPath, "Plugins/iOS/PrivacyInfo.xcprivacy");
            Assert.IsTrue(File.Exists(xcprivacyPath));
            string plistXml = File.ReadAllText(xcprivacyPath);
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(plistXml);
            Assert.IsTrue(plistXml.Contains("NSPrivacyCollectedDataTypeCrashData"),
                "iOS PrivacyInfo.xcprivacy must declare Crash Data for Firebase Crashlytics");

            TextAsset highlightsAsset = Resources.Load<TextAsset>("ReleaseHighlights");
            Assert.IsNotNull(highlightsAsset, "Resources/ReleaseHighlights.json must exist");
            HighlightsDoc highlights = JsonUtility.FromJson<HighlightsDoc>(highlightsAsset.text);
            Assert.IsNotNull(highlights?.items);
            Assert.GreaterOrEqual(highlights.items.Length, 3);
            foreach (HighlightItem item in highlights.items)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.titleTr));
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.titleEn));
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.descriptionTr));
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.descriptionEn));
            }
        }

        [Test]
        public void SaveData_RoundTripJsonSerialization_PreservesFields()
        {
            SaveData src = new SaveData
            {
                money = 777,
                energy = 64.5f,
                languageCode = "en",
                bestFishName = "Aynalı Sazan",
                footballWins = 3
            };
            src.completedMissionIds.Add("m01_get_the_keys");
            src.farmPlots.Add(new FarmPlotSaveEntry { plotId = "home_0", cropType = 2, growthProgress = 0.75f, isWatered = true });

            string json = JsonUtility.ToJson(src);
            SaveData dst = JsonUtility.FromJson<SaveData>(json);
            Assert.IsNotNull(dst);
            Assert.AreEqual(777, dst.money);
            Assert.AreEqual(64.5f, dst.energy, 0.01f);
            Assert.AreEqual("en", dst.languageCode);
            Assert.AreEqual("Aynalı Sazan", dst.bestFishName);
            Assert.AreEqual(1, dst.completedMissionIds.Count);
            Assert.AreEqual(1, dst.farmPlots.Count);
            Assert.AreEqual("home_0", dst.farmPlots[0].plotId);
        }

        [Test]
        public void MainScene_HasCoreSystems_AndZeroMissingScripts()
        {
            Assert.Greater(EditorBuildSettings.scenes.Length, 0, "Build Settings must have at least one scene");
            EditorBuildSettingsScene primary = EditorBuildSettings.scenes[0];
            Assert.IsTrue(primary.enabled, "Primary scene must be enabled in Build Settings");
            Assert.IsTrue(File.Exists(primary.path), $"Primary scene missing at {primary.path}");

            Scene scene = EditorSceneManager.OpenScene(primary.path, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid() && scene.isLoaded);

            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<SaveManager>(), "Main scene missing SaveManager");
            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<MissionManager>(), "Main scene missing MissionManager");
            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<PlayerModeController>(), "Main scene missing PlayerModeController");
            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<GameHUDController>(), "Main scene missing GameHUDController");
            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<InputHub>(), "Main scene missing InputHub");

            int missingCount = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] all = roots[r].GetComponentsInChildren<Transform>(includeInactive: true);
                for (int i = 0; i < all.Length; i++)
                {
                    missingCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(all[i].gameObject);
                }
            }
            Assert.AreEqual(0, missingCount, $"Main scene contains {missingCount} missing MonoBehaviour script references");
        }

        [Test]
        public void AssetOptimization_PropsAndVoiceover_MeetMobileSizeBudgets()
        {
            string[] decimatedProps =
            {
                "GRIDROAM/Art/Models/concrete_road_barrier/concrete_road_barrier_1k.fbx",
                "GRIDROAM/Art/Models/fire_hydrant/fire_hydrant_1k.fbx",
                "GRIDROAM/Art/Models/flood_lights/flood_lights_1k.fbx",
                "GRIDROAM/Art/Models/street_lamp_01/street_lamp_01_1k.fbx",
                "GRIDROAM/Art/Models/tool_cart/tool_cart_1k.fbx",
                "GRIDROAM/Art/Models/ocean_buoy/ocean_buoy_1k.fbx",
                "GRIDROAM/Art/Models/metal_trash_can/metal_trash_can_1k.fbx"
            };
            foreach (string rel in decimatedProps)
            {
                string full = Path.Combine(Application.dataPath, rel);
                Assert.IsTrue(File.Exists(full), $"Expected optimized prop FBX at {rel}");
                long bytes = new FileInfo(full).Length;
                Assert.Less(bytes, 650 * 1024, $"Prop {rel} grew above 650 KB budget ({bytes / 1024} KB)");
            }

            string voiceDir = Path.Combine(Application.dataPath, "GRIDROAM/Resources/Audio/Voice");
            string[] metas = Directory.GetFiles(voiceDir, "*.wav.meta", SearchOption.AllDirectories);
            Assert.Greater(metas.Length, 400, "Expected TR+EN voiceover .wav.meta files");
            int highQualityCount = 0;
            foreach (string m in metas)
            {
                string text = File.ReadAllText(m);
                if (!text.Contains("quality: 0.36")) highQualityCount++;
            }
            Assert.AreEqual(0, highQualityCount, $"{highQualityCount} voiceover clips are not set to 0.36 mobile Vorbis quality");
        }

        [Test]
        public void RuntimeScripts_HaveZeroSingleArgTurkishCustomPrompts()
        {
            string scriptsDir = Path.Combine(Application.dataPath, "GRIDROAM/Scripts");
            string[] files = Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories);
            var rxCall = new System.Text.RegularExpressions.Regex(@"SetCustomPrompt\s*\(([^;]+?)\);", System.Text.RegularExpressions.RegexOptions.Singleline);
            var rxStr = new System.Text.RegularExpressions.Regex(@"\$?""(?:[^""\\]|\\.)*""");
            var rxTr = new System.Text.RegularExpressions.Regex(@"[çğıöşüÇĞİÖŞÜ]");

            List<string> leaks = new List<string>();
            foreach (string f in files)
            {
                if (f.Replace('\\', '/').Contains("/Editor/")) continue;
                string txt = File.ReadAllText(f);
                foreach (System.Text.RegularExpressions.Match m in rxCall.Matches(txt))
                {
                    string args = m.Groups[1].Value;
                    if (args.Contains("?") || args.Contains("Tr(") || args.Contains("L(")) continue;
                    var strs = rxStr.Matches(args);
                    if (strs.Count == 1 && rxTr.IsMatch(strs[0].Value))
                    {
                        leaks.Add($"{Path.GetFileName(f)}: {strs[0].Value}");
                    }
                }
            }
            Assert.AreEqual(0, leaks.Count, "Single-argument Turkish SetCustomPrompt leaks found:\n" + string.Join("\n", leaks));
        }
    }
}
