using System.Collections.Generic;
using System.IO;
using Gridroam.CameraSystem;
using Gridroam.Core;
using Gridroam.Input;
using Gridroam.Missions;
using Gridroam.Player;
using Gridroam.UI;
using Gridroam.Vehicle;
using Gridroam.World;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gridroam.Editor
{
    /// <summary>
    /// One-click Unity Editor setup tool for GRIDROAM (Android + iOS):
    /// - Configures shared Landscape settings, Android (API 26+, IL2CPP ARM64), iOS (iOS 15+, Defer System Gestures, IL2CPP)
    /// - Creates shared Mobile URP Low/Medium profiles + ACES/Bloom Evening Post-Processing Volume
    /// - Binds CC0 Poly Haven Evening Coastal HDRI Skybox + 9 ambientCG 1K PBR Texture Sets
    /// - Builds procedural jointed humanoid player/NPC prefabs, realistic RWD sedan with procedural audio, and Poly Haven FBX props
    /// - Generates data-driven Mission 1 & Mission 2 ScriptableObjects
    /// - Builds the playable Evening Coastal Garage-Harbor + "Liman Kasabası" small town scene and iPhone/Android Safe-Area HUD
    /// </summary>
    public static partial class GridroamPrototypeSetup
    {
        private const string RootDir = "Assets/GRIDROAM";
        private const string ArtDir = RootDir + "/Art";
        private const string ArtMatDir = ArtDir + "/Materials";
        private const string TexturesDir = ArtDir + "/Textures";
        private const string ModelsDir = ArtDir + "/Models";
        private const string HdriDir = ArtDir + "/HDRI";
        private const string PrefabDir = RootDir + "/Prefabs";
        private const string DataDir = RootDir + "/Data";
        private const string MissionDir = DataDir + "/Missions";
        private const string DialogueDir = DataDir + "/Dialogues";
        private const string SettingsDir = RootDir + "/Settings";
        private const string SceneDir = RootDir + "/Scenes";
        public const string TestScenePath = SceneDir + "/GRIDROAM_GarageHarbor_Test.unity";

        [MenuItem("GRIDROAM/1. Build Garage-Harbor Prototype (Missions 1-2)", priority = 1)]
        public static void BuildFullPrototypeMenu()
        {
            BuildFullPrototype(showDialog: true);
        }

        public static void BuildFullPrototype(bool showDialog)
        {
            EnsureDirectories();
            ConfigureCrossPlatformMobileSettings();
            ConfigureAllImportedTextures();

            var (lowUrp, medUrp) = CreateOrUpdateUrpProfiles();
            VolumeProfile eveningVolume = CreateOrUpdateEveningVolumeProfile();
            MaterialSet mats = CreateMaterials();
            PrefabSet prefabs = CreatePrefabs(mats);
            MissionCatalog catalog = CreateMissionDataAssets();

            BuildTestScene(mats, prefabs, catalog, lowUrp, medUrp, eveningVolume);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GRIDROAM] Coastal Garage-Harbor + Liman Kasabası small town scene (procedural humanoid, realistic RWD driving + synth audio, HDRI sky, 1K PBR sets, Poly Haven props, Missions 1-2) built successfully!");
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "GRIDROAM Coastal Town World Ready (Android & iOS)",
                    "Evening Coastal Garage-Harbor & Liman Kasabası small town scene with procedural animated humanoid character, realistic RWD sedan driving & engine audio, town square, shops, residential quarter, park, pedestrians, and Missions 1 & 2 is ready.\n\nPress Play to explore!",
                    "OK");
            }
        }

        [MenuItem("GRIDROAM/2. Configure Android + iOS & URP Profiles Only", priority = 2)]
        public static void ConfigureProjectOnly()
        {
            EnsureDirectories();
            ConfigureCrossPlatformMobileSettings();
            CreateOrUpdateUrpProfiles();
            AssetDatabase.SaveAssets();
            Debug.Log("[GRIDROAM] Shared Android & iOS settings + URP Mobile Low/Medium profiles configured.");
        }

        public static void ConfigureCrossPlatformMobileSettings()
        {
            const string bundleId = "com.dogusstore.gridroam";

            PlayerSettings.productName = "GRIDROAM";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.statusBarHidden = true;

            // --- Android ---
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, bundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // --- iOS (iPhone Notch / Dynamic Island + Landscape Gesture Safety) ---
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, bundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.deferSystemGesturesMode =
                UnityEngine.iOS.SystemGestureDeferMode.BottomEdge |
                UnityEngine.iOS.SystemGestureDeferMode.TopEdge;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.requiresFullScreen = true;
        }

        private static void EnsureDirectories()
        {
            string[] dirs =
            {
                RootDir, ArtDir, ArtMatDir, PrefabDir, DataDir, MissionDir, DialogueDir, SettingsDir, SceneDir
            };

            foreach (string dir in dirs)
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            AssetDatabase.Refresh();
        }

        private static void ConfigureAllImportedTextures()
        {
            // 1. Configure Evening Coastal HDRI Skybox
            string hdriPath = $"{HdriDir}/venice_sunset_2k.hdr";
            TextureImporter hdriImporter = AssetImporter.GetAtPath(hdriPath) as TextureImporter;
            if (hdriImporter != null)
            {
                bool dirty = false;
                if (hdriImporter.textureType != TextureImporterType.Default) { hdriImporter.textureType = TextureImporterType.Default; dirty = true; }
                if (hdriImporter.textureShape != TextureImporterShape.Texture2D) { hdriImporter.textureShape = TextureImporterShape.Texture2D; dirty = true; }
                if (hdriImporter.sRGBTexture) { hdriImporter.sRGBTexture = false; dirty = true; }
                if (hdriImporter.maxTextureSize != 2048) { hdriImporter.maxTextureSize = 2048; dirty = true; }
                if (hdriImporter.wrapMode != TextureWrapMode.Clamp) { hdriImporter.wrapMode = TextureWrapMode.Clamp; dirty = true; }
                if (dirty) hdriImporter.SaveAndReimport();
            }

            // 2. Configure all 1K PBR textures under Art/Textures and Art/Models (1024 max for mobile 30 FPS)
            string[] scanRoots = { TexturesDir, ModelsDir };
            foreach (string root in scanRoots)
            {
                if (!Directory.Exists(root)) continue;
                string[] files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);
                foreach (string rawPath in files)
                {
                    string assetPath = rawPath.Replace("\\", "/");
                    string lower = assetPath.ToLowerInvariant();
                    if (!lower.EndsWith(".jpg") && !lower.EndsWith(".jpeg") && !lower.EndsWith(".png"))
                    {
                        continue;
                    }

                    bool isNormal = lower.Contains("normalgl") || lower.Contains("nor_gl");
                    ConfigureTextureImporter(assetPath, isNormal, 1024);
                }
            }
        }

        private static void ConfigureTextureImporter(string assetPath, bool isNormal, int maxSize)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.maxTextureSize != maxSize)
            {
                importer.maxTextureSize = maxSize;
                dirty = true;
            }

            TextureImporterType targetType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != targetType)
            {
                importer.textureType = targetType;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        private static (UniversalRenderPipelineAsset low, UniversalRenderPipelineAsset medium) CreateOrUpdateUrpProfiles()
        {
            const string sourceUrpPath = "Assets/Settings/Mobile_RPAsset.asset";
            const string lowPath = SettingsDir + "/URP_Mobile_Low.asset";
            const string medPath = SettingsDir + "/URP_Mobile_Medium.asset";

            if (!File.Exists(lowPath) && File.Exists(sourceUrpPath))
            {
                AssetDatabase.CopyAsset(sourceUrpPath, lowPath);
            }

            if (!File.Exists(medPath) && File.Exists(sourceUrpPath))
            {
                AssetDatabase.CopyAsset(sourceUrpPath, medPath);
            }

            AssetDatabase.Refresh();

            UniversalRenderPipelineAsset lowAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(lowPath);
            UniversalRenderPipelineAsset medAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(medPath);

            if (lowAsset != null)
            {
                ConfigureUrpSerialized(lowAsset, renderScale: 0.75f, shadowDistance: 26f, shadowRes: 512, hdr: false, msaa: 1, cascades: 1);
            }

            if (medAsset != null)
            {
                ConfigureUrpSerialized(medAsset, renderScale: 0.90f, shadowDistance: 45f, shadowRes: 1024, hdr: true, msaa: 2, cascades: 2);
            }

            return (lowAsset, medAsset);
        }

        private static VolumeProfile CreateOrUpdateEveningVolumeProfile()
        {
            const string volPath = SettingsDir + "/Volume_EveningCoastal.asset";
            if (File.Exists(volPath))
            {
                AssetDatabase.DeleteAsset(volPath);
            }

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, volPath);

            Tonemapping tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            AssetDatabase.AddObjectToAsset(tone, profile);

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.65f);
            AssetDatabase.AddObjectToAsset(bloom, profile);

            ColorAdjustments colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.Override(0.08f);
            colorAdj.contrast.Override(12f);
            colorAdj.saturation.Override(8f);
            colorAdj.colorFilter.Override(new Color(1.0f, 0.97f, 0.93f));
            AssetDatabase.AddObjectToAsset(colorAdj, profile);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.20f);
            vignette.smoothness.Override(0.45f);
            AssetDatabase.AddObjectToAsset(vignette, profile);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void ConfigureUrpSerialized(
            UniversalRenderPipelineAsset asset,
            float renderScale,
            float shadowDistance,
            int shadowRes,
            bool hdr,
            int msaa,
            int cascades)
        {
            SerializedObject so = new SerializedObject(asset);
            SetFloat(so, "m_RenderScale", renderScale);
            SetFloat(so, "m_ShadowDistance", shadowDistance);
            SetInt(so, "m_MainLightShadowmapResolution", shadowRes);
            SetBool(so, "m_SupportsHDR", hdr);
            SetInt(so, "m_MSAA", msaa);
            SetInt(so, "m_ShadowCascadeCount", cascades);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void SetFloat(SerializedObject so, string prop, float val)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null) p.floatValue = val;
        }

        private static void SetInt(SerializedObject so, string prop, int val)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null) p.intValue = val;
        }

        private static void SetBool(SerializedObject so, string prop, bool val)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null) p.boolValue = val;
        }

        private class MaterialSet
        {
            public Material EveningSkybox;
            public Material RoadAsphaltMain;
            public Material RoadAsphaltCross;
            public Material RoadMarkingWhite;
            public Material RoadMarkingYellow;
            public Material Ground;
            public Material Sidewalk;
            public Material SeaWater;
            public Material HarborDock;
            public Material GarageBrick;
            public Material GarageRoofMetal;
            public Material PlasterTrim;
            public Material BuildingFacadeA;
            public Material BuildingFacadeB;
            public Material BuildingFacadeC;
            public Material Concrete;
            public Material ContainerRed;
            public Material ContainerBlue;
            public Material ContainerOchre;
            public Material GantrySteel;
            public Material PlayerSuit;
            public Material MechanicSuit;
            public Material ReceiverSuit;
            public Material SedanBody;
            public Material SedanTrimDark;
            public Material SedanChrome;
            public Material SedanGlass;
            public Material TaillightGlow;
            public Material WheelRubber;
            public Material TrafficBody;
            public Material BeaconGlow;
            public Material LampGlow;
            public Material WindowLitWarm;
            public Material RedBeaconGlow;
            public Material ReverseLightOff;
            // Humanoid character materials
            public Material CharSkin;
            public Material CharShirt;
            public Material CharPants;
            public Material CharShoes;
            public Material CharHair;
            // Small Town ("Liman Kasabası") materials
            public Material TownPaving;
            public Material TownRoofTile;
            public Material TownBrickHouse;
            public Material TownGrass;
            public Material TownAwningTerracotta;
            public Material TownAwningGreen;
            public Material TownAwningNavy;
            public Material TownAwningCream;
            public Material TownBark;
            public Material TownFoliageA;
            public Material TownFoliageB;
            public Material TownHedge;
            public Material TownWood;
            public Material TownDoorDark;
            public Material TrafficLightBulb;
            public Material PedShirtRed;
            public Material PedShirtOlive;
            public Material PedShirtMustard;
            public Material PedShirtTeal;
            public Material PedShirtBurgundy;
        }

        private static MaterialSet CreateMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Shader skyPanoramic = Shader.Find("Skybox/Panoramic");
            Material skyMat = CreateOrLoadMaterial("MAT_Skybox_EveningCoastal", skyPanoramic != null ? skyPanoramic : urpLit, Color.white, 0f);
            Texture2D hdriTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdriDir}/venice_sunset_2k.hdr");
            if (hdriTex != null && skyPanoramic != null)
            {
                skyMat.SetTexture("_MainTex", hdriTex);
                skyMat.SetFloat("_Exposure", 0.92f);
                skyMat.SetFloat("_Rotation", 108f);
                skyMat.SetFloat("_Mapping", 1f);
                skyMat.SetFloat("_ImageType", 0f);
                EditorUtility.SetDirty(skyMat);
            }

            MaterialSet set = new MaterialSet
            {
                EveningSkybox = skyMat,
                RoadAsphaltMain = CreatePbrMaterial("MAT_Road_Asphalt012", urpLit, "Asphalt012", new Color(0.72f, 0.72f, 0.76f), 0.34f, 0f, new Vector2(2f, 36f), 0.9f),
                RoadAsphaltCross = CreatePbrMaterial("MAT_Road_Asphalt012_Cross", urpLit, "Asphalt012", new Color(0.72f, 0.72f, 0.76f), 0.34f, 0f, new Vector2(9f, 2f), 0.9f),
                RoadMarkingWhite = CreateOrLoadMaterial("MAT_Road_Marking_White", urpLit, new Color(0.88f, 0.88f, 0.86f), 0.18f),
                RoadMarkingYellow = CreateOrLoadMaterial("MAT_Road_Marking_Yellow", urpLit, new Color(0.92f, 0.72f, 0.18f), 0.22f),
                Ground = CreatePbrMaterial("MAT_Ground_Urban", urpLit, "Concrete034", new Color(0.55f, 0.55f, 0.57f), 0.14f, 0f, new Vector2(22f, 64f), 0.7f),
                Sidewalk = CreatePbrMaterial("MAT_Sidewalk_Paving", urpLit, "PavingStones070", new Color(0.84f, 0.82f, 0.80f), 0.18f, 0f, new Vector2(2f, 44f), 0.85f),
                SeaWater = CreateOrLoadMaterial("MAT_Sea_Evening", urpLit, new Color(0.04f, 0.13f, 0.22f), 0.92f, 0.16f),
                HarborDock = CreatePbrMaterial("MAT_Harbor_Dock", urpLit, "Concrete034", new Color(0.74f, 0.73f, 0.72f), 0.22f, 0f, new Vector2(10f, 6f), 0.95f),
                GarageBrick = CreatePbrMaterial("MAT_Garage_Bricks", urpLit, "Bricks059", new Color(0.88f, 0.84f, 0.82f), 0.18f, 0f, new Vector2(3.5f, 2f), 1.0f),
                GarageRoofMetal = CreatePbrMaterial("MAT_Garage_RoofMetal", urpLit, "CorrugatedSteel005", new Color(0.52f, 0.55f, 0.58f), 0.42f, 0.55f, new Vector2(4f, 3f), 0.9f),
                PlasterTrim = CreatePbrMaterial("MAT_Plaster_Trim", urpLit, "Plaster003", new Color(0.80f, 0.77f, 0.72f), 0.14f, 0f, new Vector2(3f, 2f), 0.7f),
                BuildingFacadeA = CreatePbrMaterial("MAT_Building_FacadeA", urpLit, "Facade005", new Color(0.92f, 0.88f, 0.84f), 0.28f, 0.08f, new Vector2(2f, 2f), 0.95f),
                BuildingFacadeB = CreatePbrMaterial("MAT_Building_FacadeB", urpLit, "Facade006", new Color(0.88f, 0.90f, 0.94f), 0.30f, 0.10f, new Vector2(2f, 2f), 0.95f),
                BuildingFacadeC = CreatePbrMaterial("MAT_Building_FacadeC", urpLit, "Facade018A", new Color(0.94f, 0.86f, 0.82f), 0.26f, 0.08f, new Vector2(2f, 2f), 0.95f),
                Concrete = CreatePbrMaterial("MAT_Concrete_Props", urpLit, "Concrete034", new Color(0.76f, 0.76f, 0.74f), 0.16f, 0f, new Vector2(3f, 1f), 0.85f),
                ContainerRed = CreatePbrMaterial("MAT_Container_Rust", urpLit, "CorrugatedSteel005", new Color(0.68f, 0.22f, 0.17f), 0.35f, 0.48f, new Vector2(3f, 1f), 1.0f),
                ContainerBlue = CreatePbrMaterial("MAT_Container_Navy", urpLit, "CorrugatedSteel005", new Color(0.18f, 0.34f, 0.56f), 0.35f, 0.48f, new Vector2(3f, 1f), 1.0f),
                ContainerOchre = CreatePbrMaterial("MAT_Container_Ochre", urpLit, "CorrugatedSteel005", new Color(0.76f, 0.56f, 0.18f), 0.35f, 0.48f, new Vector2(3f, 1f), 1.0f),
                GantrySteel = CreatePbrMaterial("MAT_Harbor_Gantry", urpLit, "CorrugatedSteel005", new Color(0.76f, 0.34f, 0.16f), 0.40f, 0.62f, new Vector2(2f, 6f), 0.8f),
                PlayerSuit = CreateOrLoadMaterial("MAT_Player_Placeholder", urpLit, new Color(0.18f, 0.46f, 0.74f), 0.28f),
                MechanicSuit = CreateOrLoadMaterial("MAT_NPC_Mechanic", urpLit, new Color(0.78f, 0.44f, 0.16f), 0.25f),
                ReceiverSuit = CreateOrLoadMaterial("MAT_NPC_Receiver", urpLit, new Color(0.16f, 0.58f, 0.48f), 0.25f),
                SedanBody = CreateOrLoadMaterial("MAT_Sedan_Body", urpLit, new Color(0.68f, 0.14f, 0.14f), 0.86f, 0.48f),
                SedanTrimDark = CreateOrLoadMaterial("MAT_Sedan_TrimDark", urpLit, new Color(0.12f, 0.13f, 0.15f), 0.35f, 0.15f),
                SedanChrome = CreateOrLoadMaterial("MAT_Sedan_Chrome", urpLit, new Color(0.84f, 0.86f, 0.88f), 0.92f, 0.88f),
                SedanGlass = CreateOrLoadMaterial("MAT_Sedan_Glass", urpLit, new Color(0.06f, 0.09f, 0.14f), 0.94f, 0.20f),
                TaillightGlow = CreateOrLoadMaterial("MAT_Sedan_Taillight", urpLit, new Color(0.95f, 0.12f, 0.10f), 0.6f, 0f, new Color(1.0f, 0.12f, 0.08f) * 2.6f),
                WheelRubber = CreateOrLoadMaterial("MAT_Wheel_Rubber", urpLit, new Color(0.11f, 0.11f, 0.12f), 0.20f),
                TrafficBody = CreateOrLoadMaterial("MAT_Traffic_Placeholder", urpLit, new Color(0.24f, 0.30f, 0.38f), 0.75f, 0.42f),
                BeaconGlow = CreateOrLoadMaterial("MAT_Objective_Beacon", urpLit, new Color(1.0f, 0.72f, 0.15f), 0.3f, 0f, new Color(1.0f, 0.65f, 0.12f) * 2.1f),
                LampGlow = CreateOrLoadMaterial("MAT_StreetLamp_Glow", urpLit, new Color(1.0f, 0.88f, 0.62f), 0.2f, 0f, new Color(1.0f, 0.80f, 0.42f) * 3.0f),
                WindowLitWarm = CreateOrLoadMaterial("MAT_Window_LitWarm", urpLit, new Color(1.0f, 0.82f, 0.52f), 0.5f, 0f, new Color(1.0f, 0.74f, 0.36f) * 1.65f),
                RedBeaconGlow = CreateOrLoadMaterial("MAT_Harbor_RedBeacon", urpLit, new Color(1.0f, 0.18f, 0.14f), 0.2f, 0f, new Color(1.0f, 0.14f, 0.10f) * 3.2f),
                ReverseLightOff = CreateOrLoadMaterial("MAT_Sedan_ReverseLight", urpLit, new Color(0.86f, 0.86f, 0.84f), 0.6f, 0f, Color.black),
                // Humanoid character materials
                CharSkin = CreateOrLoadMaterial("MAT_Char_Skin", urpLit, new Color(0.86f, 0.67f, 0.54f), 0.35f),
                CharShirt = CreateOrLoadMaterial("MAT_Char_Shirt", urpLit, new Color(0.16f, 0.27f, 0.42f), 0.20f),
                CharPants = CreateOrLoadMaterial("MAT_Char_Pants", urpLit, new Color(0.13f, 0.13f, 0.15f), 0.15f),
                CharShoes = CreateOrLoadMaterial("MAT_Char_Shoes", urpLit, new Color(0.08f, 0.07f, 0.06f), 0.45f),
                CharHair = CreateOrLoadMaterial("MAT_Char_Hair", urpLit, new Color(0.10f, 0.07f, 0.05f), 0.30f),
                // Small Town ("Liman Kasabası") materials
                TownPaving = CreatePbrMaterial("MAT_Town_Paving", urpLit, "PavingStones070", new Color(0.86f, 0.83f, 0.78f), 0.18f, 0f, Vector2.one, 0.85f),
                TownRoofTile = CreatePbrMaterial("MAT_Town_RoofTile", urpLit, "Bricks059", new Color(0.62f, 0.30f, 0.22f), 0.16f, 0f, Vector2.one, 0.95f),
                TownBrickHouse = CreatePbrMaterial("MAT_Town_BrickHouse", urpLit, "Bricks059", new Color(0.86f, 0.74f, 0.64f), 0.16f, 0f, Vector2.one, 0.90f),
                TownGrass = CreateOrLoadMaterial("MAT_Town_Grass", urpLit, new Color(0.28f, 0.42f, 0.18f), 0.10f),
                TownAwningTerracotta = CreateOrLoadMaterial("MAT_Town_Awning_Terracotta", urpLit, new Color(0.74f, 0.30f, 0.20f), 0.16f),
                TownAwningGreen = CreateOrLoadMaterial("MAT_Town_Awning_Green", urpLit, new Color(0.16f, 0.42f, 0.25f), 0.16f),
                TownAwningNavy = CreateOrLoadMaterial("MAT_Town_Awning_Navy", urpLit, new Color(0.14f, 0.24f, 0.44f), 0.16f),
                TownAwningCream = CreateOrLoadMaterial("MAT_Town_Awning_Cream", urpLit, new Color(0.88f, 0.83f, 0.72f), 0.16f),
                TownBark = CreateOrLoadMaterial("MAT_Town_Bark", urpLit, new Color(0.32f, 0.22f, 0.14f), 0.12f),
                TownFoliageA = CreateOrLoadMaterial("MAT_Town_Foliage_A", urpLit, new Color(0.18f, 0.38f, 0.16f), 0.12f),
                TownFoliageB = CreateOrLoadMaterial("MAT_Town_Foliage_B", urpLit, new Color(0.25f, 0.45f, 0.20f), 0.12f),
                TownHedge = CreateOrLoadMaterial("MAT_Town_Hedge", urpLit, new Color(0.17f, 0.34f, 0.15f), 0.10f),
                TownWood = CreateOrLoadMaterial("MAT_Town_Wood", urpLit, new Color(0.42f, 0.26f, 0.14f), 0.24f),
                TownDoorDark = CreateOrLoadMaterial("MAT_Town_DoorDark", urpLit, new Color(0.24f, 0.15f, 0.09f), 0.26f),
                TrafficLightBulb = CreateOrLoadMaterial("MAT_TrafficLight_Bulb", urpLit, new Color(0.14f, 0.14f, 0.15f), 0.55f, 0f, Color.black),
                PedShirtRed = CreateOrLoadMaterial("MAT_Ped_Shirt_Red", urpLit, new Color(0.66f, 0.18f, 0.18f), 0.20f),
                PedShirtOlive = CreateOrLoadMaterial("MAT_Ped_Shirt_Olive", urpLit, new Color(0.30f, 0.40f, 0.22f), 0.20f),
                PedShirtMustard = CreateOrLoadMaterial("MAT_Ped_Shirt_Mustard", urpLit, new Color(0.76f, 0.58f, 0.18f), 0.20f),
                PedShirtTeal = CreateOrLoadMaterial("MAT_Ped_Shirt_Teal", urpLit, new Color(0.15f, 0.44f, 0.46f), 0.20f),
                PedShirtBurgundy = CreateOrLoadMaterial("MAT_Ped_Shirt_Burgundy", urpLit, new Color(0.44f, 0.14f, 0.24f), 0.20f)
            };

            // Add subtle animated ripple normal to the coastal evening sea
            Texture2D waterNormal = FindTextureBySuffix($"{TexturesDir}/Plaster003", "_NormalGL.jpg");
            if (waterNormal != null)
            {
                set.SeaWater.SetTexture("_BumpMap", waterNormal);
                set.SeaWater.SetTextureScale("_BumpMap", new Vector2(36f, 84f));
                set.SeaWater.SetFloat("_BumpScale", 0.35f);
                set.SeaWater.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(set.SeaWater);
            }

            return set;
        }

        private static Material CreatePbrMaterial(
            string matName,
            Shader shader,
            string acgFolder,
            Color tint,
            float smoothness,
            float metallic,
            Vector2 tiling,
            float normalStrength)
        {
            Material mat = CreateOrLoadMaterial(matName, shader, tint, smoothness, metallic);
            string dir = $"{TexturesDir}/{acgFolder}";

            Texture2D baseTex = FindTextureBySuffix(dir, "_Color.jpg");
            Texture2D normalTex = FindTextureBySuffix(dir, "_NormalGL.jpg");
            Texture2D aoTex = FindTextureBySuffix(dir, "_AmbientOcclusion.jpg");

            if (baseTex != null)
            {
                mat.SetTexture("_BaseMap", baseTex);
                mat.SetTextureScale("_BaseMap", tiling);
            }

            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.SetFloat("_BumpScale", normalStrength);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (aoTex != null)
            {
                mat.SetTexture("_OcclusionMap", aoTex);
                mat.SetFloat("_OcclusionStrength", 0.75f);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Texture2D FindTextureBySuffix(string directory, string suffix)
        {
            if (!Directory.Exists(directory)) return null;
            string[] files = Directory.GetFiles(directory, "*" + suffix, SearchOption.AllDirectories);
            if (files.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(files[0].Replace("\\", "/"));
        }

        private static Material CreateOrLoadMaterial(
            string matName,
            Shader shader,
            Color baseColor,
            float smoothness,
            float metallic = 0f,
            Color? emission = null)
        {
            string path = $"{ArtMatDir}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = shader;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            if (emission.HasValue && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private class PrefabSet
        {
            public GameObject Player;
            public GameObject PlayerSedan;
            public GameObject NpcMechanic;
            public GameObject NpcHarborReceiver;
            public GameObject ApartmentBuilding;
            public GameObject TrafficVehicle;
            public GameObject ConcreteBarrier;
            public GameObject StreetLamp;
            public GameObject WoodenCrate;
            public GameObject OilBarrel;
            public GameObject FireHydrant;
            public GameObject TrashCan;
            public GameObject ToolCart;
            public GameObject OldTyre;
            public GameObject FloodLights;
            public GameObject OceanBuoy;
            public GameObject RollerShutterDoor;
        }

        private static PrefabSet CreatePrefabs(MaterialSet mats)
        {
            return new PrefabSet
            {
                Player = BuildPlayerPrefab(mats),
                PlayerSedan = BuildPlayerSedanPrefab(mats),
                NpcMechanic = BuildNpcPrefab("NPC_Mechanic", "npc_mechanic", mats, mats.MechanicSuit, mats.MechanicSuit),
                NpcHarborReceiver = BuildNpcPrefab("NPC_HarborReceiver", "npc_harbor_receiver", mats, mats.ReceiverSuit, mats.CharPants),
                ApartmentBuilding = BuildApartmentPrefab(mats),
                TrafficVehicle = BuildTrafficPlaceholderPrefab(mats),
                ConcreteBarrier = BuildFbxPropPrefab("concrete_road_barrier", "Prop_ConcreteBarrier_Placeholder", 1.05f, addCollider: true, isStatic: true, fallbackMat: mats.Concrete),
                StreetLamp = BuildFbxPropPrefab("street_lamp_01", "Prop_StreetLamp_CC0", 5.2f, addCollider: true, isStatic: true, fallbackMat: mats.Concrete, addLampGlow: true, lampGlowMat: mats.LampGlow),
                WoodenCrate = BuildFbxPropPrefab("wooden_crate_01", "Prop_WoodenCrate_CC0", 1.05f, addCollider: true, isStatic: true, fallbackMat: mats.PlasterTrim),
                OilBarrel = BuildFbxPropPrefab("Barrel_01", "Prop_OilBarrel_CC0", 0.95f, addCollider: true, isStatic: true, fallbackMat: mats.ContainerRed),
                FireHydrant = BuildFbxPropPrefab("fire_hydrant", "Prop_FireHydrant_CC0", 0.85f, addCollider: true, isStatic: true, fallbackMat: mats.ContainerRed),
                TrashCan = BuildFbxPropPrefab("metal_trash_can", "Prop_TrashCan_CC0", 0.85f, addCollider: true, isStatic: true, fallbackMat: mats.GarageRoofMetal),
                ToolCart = BuildFbxPropPrefab("tool_cart", "Prop_ToolCart_CC0", 1.02f, addCollider: true, isStatic: true, fallbackMat: mats.ContainerRed),
                OldTyre = BuildFbxPropPrefab("old_tyre", "Prop_OldTyre_CC0", 0.30f, addCollider: false, isStatic: true, fallbackMat: mats.WheelRubber),
                FloodLights = BuildFbxPropPrefab("flood_lights", "Prop_FloodLights_CC0", 2.25f, addCollider: true, isStatic: true, fallbackMat: mats.GarageRoofMetal),
                OceanBuoy = BuildFbxPropPrefab("ocean_buoy", "Prop_OceanBuoy_CC0", 2.8f, addCollider: false, isStatic: false, fallbackMat: mats.ContainerRed),
                RollerShutterDoor = BuildFbxPropPrefab("rollershutter_door", "Prop_RollerShutter_CC0", 3.6f, addCollider: false, isStatic: true, fallbackMat: mats.GarageRoofMetal)
            };
        }

        /// <summary>
        /// Imports a CC0 Poly Haven FBX model into a mobile-optimized URP prefab with
        /// automatic 1K URP Lit material binding, grounded Y=0 pivot, and single simple BoxCollider.
        /// </summary>
        private static GameObject BuildFbxPropPrefab(
            string modelId,
            string prefabName,
            float targetHeightMeters,
            bool addCollider,
            bool isStatic,
            Material fallbackMat,
            bool addLampGlow = false,
            Material lampGlowMat = null)
        {
            GameObject root = new GameObject(prefabName);
            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            string modelFolder = $"{ModelsDir}/{modelId}";
            GameObject fbxAsset = null;
            if (Directory.Exists(modelFolder))
            {
                string[] fbxFiles = Directory.GetFiles(modelFolder, "*.fbx", SearchOption.TopDirectoryOnly);
                if (fbxFiles.Length > 0)
                {
                    fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxFiles[0].Replace("\\", "/"));
                }
            }

            Bounds combinedBounds = new Bounds(Vector3.up * (targetHeightMeters * 0.5f), Vector3.one * targetHeightMeters);

            if (fbxAsset != null)
            {
                Material urpPropMat = CreatePolyHavenUrpMaterial(modelId, modelFolder);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, visualRoot.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;

                // Strip any MeshColliders from imported FBX for Android/iOS physics speed
                foreach (Collider col in instance.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(col);
                }

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    combinedBounds = renderers[0].bounds;
                    foreach (Renderer r in renderers)
                    {
                        combinedBounds.Encapsulate(r.bounds);
                        if (urpPropMat != null)
                        {
                            Material[] mats = new Material[r.sharedMaterials.Length];
                            for (int i = 0; i < mats.Length; i++) mats[i] = urpPropMat;
                            r.sharedMaterials = mats;
                        }
                    }

                    // Normalize real-world height and align bottom flush to y = 0
                    float rawHeight = Mathf.Max(0.001f, combinedBounds.size.y);
                    float scaleFactor = targetHeightMeters / rawHeight;
                    instance.transform.localScale = Vector3.one * scaleFactor;

                    // Recompute grounded bounds after uniform scale
                    Bounds scaledBounds = new Bounds(combinedBounds.center * scaleFactor, combinedBounds.size * scaleFactor);
                    Vector3 centerShift = new Vector3(-scaledBounds.center.x, -scaledBounds.min.y, -scaledBounds.center.z);
                    instance.transform.localPosition = centerShift;
                    combinedBounds = new Bounds(new Vector3(0f, scaledBounds.size.y * 0.5f, 0f), scaledBounds.size);
                }
            }
            else
            {
                CreatePrimitiveChild("Fallback_Mesh", PrimitiveType.Cube, visualRoot.transform,
                    new Vector3(0f, targetHeightMeters * 0.5f, 0f),
                    new Vector3(targetHeightMeters, targetHeightMeters, targetHeightMeters), fallbackMat, removeCollider: true);
            }

            if (addLampGlow && lampGlowMat != null)
            {
                CreatePrimitiveChild("Lantern_WarmBulb", PrimitiveType.Sphere, visualRoot.transform,
                    new Vector3(0f, combinedBounds.size.y * 0.88f, 0f),
                    new Vector3(0.32f, 0.42f, 0.32f), lampGlowMat, removeCollider: true);
            }

            if (addCollider)
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.center = combinedBounds.center;
                box.size = new Vector3(
                    Mathf.Max(0.35f, combinedBounds.size.x),
                    Mathf.Max(0.35f, combinedBounds.size.y),
                    Mathf.Max(0.35f, combinedBounds.size.z));
            }

            if (isStatic)
            {
                MarkStaticEnvironment(root);
            }

            return SaveAsPrefab(root, $"{PrefabDir}/{prefabName}.prefab");
        }

        private static Material CreatePolyHavenUrpMaterial(string modelId, string modelFolder)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Material mat = CreateOrLoadMaterial($"MAT_PH_{modelId}", urpLit, Color.white, 0.32f, 0.10f);
            string texDir = $"{modelFolder}/textures";
            if (!Directory.Exists(texDir)) return mat;

            Texture2D diff = FindFirstMatchingTexture(texDir, new[] { "_diff_1k.jpg", "_diff_1k.png", "diff_1k" });
            Texture2D normal = FindFirstMatchingTexture(texDir, new[] { "_nor_gl_1k.jpg", "_nor_gl_1k.png", "nor_gl" });
            Texture2D emission = FindFirstMatchingTexture(texDir, new[] { "emissive", "emission" });
            Texture2D metallic = FindFirstMatchingTexture(texDir, new[] { "metallic", "_metal_" });

            if (diff != null) mat.SetTexture("_BaseMap", diff);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 0.9f);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (metallic != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
                mat.SetFloat("_Metallic", 0.7f);
            }
            if (emission != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", emission);
                mat.SetColor("_EmissionColor", new Color(1.0f, 0.88f, 0.65f) * 2.4f);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Texture2D FindFirstMatchingTexture(string dir, string[] patterns)
        {
            string[] files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
            foreach (string pattern in patterns)
            {
                foreach (string f in files)
                {
                    string lower = f.ToLowerInvariant();
                    if ((lower.EndsWith(".jpg") || lower.EndsWith(".png")) && lower.Contains(pattern) && !lower.Contains("glass"))
                    {
                        return AssetDatabase.LoadAssetAtPath<Texture2D>(f.Replace("\\", "/"));
                    }
                }
            }
            return null;
        }

        private static GameObject BuildPlayerPrefab(MaterialSet mats)
        {
            GameObject root = new GameObject("Player_GRIDROAM");
            CharacterController cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.38f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.35f;

            PlayerFootController foot = root.AddComponent<PlayerFootController>();

            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            ProceduralHumanoid humanoid = ProceduralHumanoid.Build(
                visualRoot.transform,
                mats.CharSkin,
                mats.CharShirt,
                mats.CharPants,
                mats.CharShoes,
                mats.CharHair,
                1.78f);

            foot.ConfigureVisuals(visualRoot.transform, null, humanoid);
            return SaveAsPrefab(root, $"{PrefabDir}/Player_GRIDROAM.prefab");
        }

        private static GameObject BuildPlayerSedanPrefab(MaterialSet mats)
        {
            GameObject root = new GameObject("Vehicle_PlayerSedan");

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 1350f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.25f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            WorldAnchor anchor = root.AddComponent<WorldAnchor>();
            anchor.Configure("player_sedan", 3.8f, new Vector3(0f, 2.3f, 0f));

            InteractableTrigger interactable = root.AddComponent<InteractableTrigger>();
            interactable.Configure(InteractableKind.PlayerVehicle, "player_sedan", "ui.btn.enter_car", 3.8f);

            // Simple box collider for chassis
            GameObject chassisColObj = new GameObject("ChassisCollider");
            chassisColObj.transform.SetParent(root.transform, false);
            BoxCollider chassisBox = chassisColObj.AddComponent<BoxCollider>();
            chassisBox.center = new Vector3(0f, 0.72f, 0f);
            chassisBox.size = new Vector3(1.90f, 0.95f, 4.45f);

            // VisualRoot (sculpted sedan placeholder with windscreen, mirrors, door lines, wheel arches & interior)
            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            // Lower chassis + hood + trunk + cabin + bumpers + headlights + taillights + reverse lights
            CreatePrimitiveChild("Placeholder_LowerBody", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 0.62f, 0f), new Vector3(1.88f, 0.56f, 4.42f), mats.SedanBody, removeCollider: true);
            CreatePrimitiveChild("Placeholder_Hood", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 0.86f, 1.38f), new Vector3(1.74f, 0.14f, 1.48f), mats.SedanBody, removeCollider: true);
            CreatePrimitiveChild("Placeholder_CabinRoof", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 1.30f, -0.22f), new Vector3(1.54f, 0.34f, 1.76f), mats.SedanBody, removeCollider: true);
            CreatePrimitiveChild("Placeholder_GlassBelt", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 1.14f, -0.22f), new Vector3(1.58f, 0.36f, 1.72f), mats.SedanGlass, removeCollider: true);

            // Sloped windscreen & sloped rear glass
            GameObject windscreen = CreatePrimitiveChild("Placeholder_Windscreen", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 1.16f, 0.80f), new Vector3(1.54f, 0.56f, 0.08f), mats.SedanGlass, removeCollider: true);
            windscreen.transform.localRotation = Quaternion.Euler(36f, 0f, 0f);

            GameObject rearGlass = CreatePrimitiveChild("Placeholder_RearGlass", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 1.17f, -1.24f), new Vector3(1.52f, 0.54f, 0.08f), mats.SedanGlass, removeCollider: true);
            rearGlass.transform.localRotation = Quaternion.Euler(-34f, 0f, 0f);

            // Side mirrors
            CreatePrimitiveChild("Placeholder_MirrorStemL", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(-0.92f, 0.94f, 0.56f), new Vector3(0.14f, 0.05f, 0.06f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("Placeholder_MirrorL", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(-1.04f, 0.96f, 0.55f), new Vector3(0.16f, 0.11f, 0.13f), mats.SedanBody, removeCollider: true);
            CreatePrimitiveChild("Placeholder_MirrorStemR", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0.92f, 0.94f, 0.56f), new Vector3(0.14f, 0.05f, 0.06f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("Placeholder_MirrorR", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(1.04f, 0.96f, 0.55f), new Vector3(0.16f, 0.11f, 0.13f), mats.SedanBody, removeCollider: true);

            // Door lines & side molding
            CreatePrimitiveChild("Placeholder_DoorSeamL", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(-0.945f, 0.64f, -0.08f), new Vector3(0.02f, 0.50f, 0.03f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("Placeholder_DoorSeamR", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0.945f, 0.64f, -0.08f), new Vector3(0.02f, 0.50f, 0.03f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("Placeholder_DoorSillL", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(-0.945f, 0.54f, -0.04f), new Vector3(0.025f, 0.04f, 1.92f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("Placeholder_DoorSillR", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0.945f, 0.54f, -0.04f), new Vector3(0.025f, 0.04f, 1.92f), mats.SedanTrimDark, removeCollider: true);

            // Wheel arches above each wheel
            CreatePrimitiveChild("Placeholder_WheelArchFL", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(-0.94f, 0.78f, 1.38f), new Vector3(0.07f, 0.14f, 0.80f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("Placeholder_WheelArchFR", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0.94f, 0.78f, 1.38f), new Vector3(0.07f, 0.14f, 0.80f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("Placeholder_WheelArchRL", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(-0.94f, 0.78f, -1.32f), new Vector3(0.07f, 0.14f, 0.80f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("Placeholder_WheelArchRR", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0.94f, 0.78f, -1.32f), new Vector3(0.07f, 0.14f, 0.80f), mats.SedanTrimDark, removeCollider: true);

            CreatePrimitiveChild("Placeholder_FrontBumper", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 0.44f, 2.24f), new Vector3(1.92f, 0.24f, 0.18f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("Placeholder_RearBumper", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 0.44f, -2.24f), new Vector3(1.92f, 0.24f, 0.18f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("Placeholder_Grille", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 0.64f, 2.22f), new Vector3(0.76f, 0.20f, 0.08f), mats.SedanChrome, removeCollider: true);
            CreatePrimitiveChild("Placeholder_HeadlightL", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(-0.66f, 0.68f, 2.22f), new Vector3(0.38f, 0.15f, 0.08f), mats.LampGlow, removeCollider: true);
            CreatePrimitiveChild("Placeholder_HeadlightR", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0.66f, 0.68f, 2.22f), new Vector3(0.38f, 0.15f, 0.08f), mats.LampGlow, removeCollider: true);
            CreatePrimitiveChild("Placeholder_TaillightL", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(-0.66f, 0.70f, -2.22f), new Vector3(0.42f, 0.15f, 0.08f), mats.TaillightGlow, removeCollider: true);
            CreatePrimitiveChild("Placeholder_TaillightR", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0.66f, 0.70f, -2.22f), new Vector3(0.42f, 0.15f, 0.08f), mats.TaillightGlow, removeCollider: true);
            CreatePrimitiveChild("Placeholder_ReverseL", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(-0.37f, 0.70f, -2.22f), new Vector3(0.14f, 0.13f, 0.08f), mats.ReverseLightOff, removeCollider: true);
            CreatePrimitiveChild("Placeholder_ReverseR", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0.37f, 0.70f, -2.22f), new Vector3(0.14f, 0.13f, 0.08f), mats.ReverseLightOff, removeCollider: true);

            // Steering wheel (tilted 65 deg, rotates visually with steering angle)
            GameObject steeringWheel = new GameObject("SteeringWheel");
            steeringWheel.transform.SetParent(visualRoot.transform, false);
            steeringWheel.transform.localPosition = new Vector3(-0.38f, 1.0f, 0.35f);
            steeringWheel.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            CreatePrimitiveChild("SteeringWheelRim", PrimitiveType.Cylinder, steeringWheel.transform,
                Vector3.zero, new Vector3(0.34f, 0.025f, 0.34f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("SteeringWheelSpoke", PrimitiveType.Cube, steeringWheel.transform,
                Vector3.zero, new Vector3(0.30f, 0.03f, 0.045f), mats.SedanChrome, removeCollider: true);

            // Seated driver visual inside cabin (shown only while IsPlayerDriving)
            GameObject seatedDriverRoot = new GameObject("SeatedDriverRoot");
            seatedDriverRoot.transform.SetParent(visualRoot.transform, false);
            seatedDriverRoot.transform.localPosition = new Vector3(-0.38f, 0.45f, -0.25f);
            ProceduralHumanoid seatedDriver = ProceduralHumanoid.Build(
                seatedDriverRoot.transform,
                mats.CharSkin,
                mats.CharShirt,
                mats.CharPants,
                mats.CharShoes,
                mats.CharHair,
                1.78f);
            seatedDriver.SetSitting(true);
            seatedDriverRoot.SetActive(false);

            // Real evening headlight beam illuminating asphalt ahead (shadowless for mobile 30 FPS)
            GameObject beamObj = new GameObject("Headlight_Beam");
            beamObj.transform.SetParent(visualRoot.transform, false);
            beamObj.transform.localPosition = new Vector3(0f, 0.76f, 2.28f);
            beamObj.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
            Light headlight = beamObj.AddComponent<Light>();
            headlight.type = LightType.Spot;
            headlight.color = new Color(1.0f, 0.94f, 0.80f);
            headlight.range = 34f;
            headlight.spotAngle = 56f;
            headlight.intensity = 4.2f;
            headlight.shadows = LightShadows.None;

            // WheelColliders & WheelVisuals (Unity 6 official WheelCollider tutorial hierarchy)
            GameObject wcParent = new GameObject("WheelColliders");
            wcParent.transform.SetParent(root.transform, false);

            GameObject wvParent = new GameObject("WheelVisuals");
            wvParent.transform.SetParent(root.transform, false);

            WheelCollider fl = CreateWheelCollider("WheelFL", wcParent.transform, new Vector3(-0.84f, 0.40f, 1.38f));
            WheelCollider fr = CreateWheelCollider("WheelFR", wcParent.transform, new Vector3(0.84f, 0.40f, 1.38f));
            WheelCollider rl = CreateWheelCollider("WheelRL", wcParent.transform, new Vector3(-0.84f, 0.40f, -1.32f));
            WheelCollider rr = CreateWheelCollider("WheelRR", wcParent.transform, new Vector3(0.84f, 0.40f, -1.32f));

            Transform vfl = CreateWheelVisual("WheelVisual_FL", wvParent.transform, fl.transform.localPosition, mats.WheelRubber, mats.SedanChrome);
            Transform vfr = CreateWheelVisual("WheelVisual_FR", wvParent.transform, fr.transform.localPosition, mats.WheelRubber, mats.SedanChrome);
            Transform vrl = CreateWheelVisual("WheelVisual_RL", wvParent.transform, rl.transform.localPosition, mats.WheelRubber, mats.SedanChrome);
            Transform vrr = CreateWheelVisual("WheelVisual_RR", wvParent.transform, rr.transform.localPosition, mats.WheelRubber, mats.SedanChrome);

            GameObject exitL = new GameObject("ExitLeft");
            exitL.transform.SetParent(root.transform, false);
            exitL.transform.localPosition = new Vector3(-1.85f, 0.08f, 0.1f);

            GameObject exitR = new GameObject("ExitRight");
            exitR.transform.SetParent(root.transform, false);
            exitR.transform.localPosition = new Vector3(1.85f, 0.08f, 0.1f);

            // Rear-wheel drive by default (front axle motor = false, rear axle motor = true)
            SedanVehicleController controller = root.AddComponent<SedanVehicleController>();
            List<AxleInfo> axles = new List<AxleInfo>
            {
                new AxleInfo { leftWheel = fl, rightWheel = fr, leftWheelVisual = vfl, rightWheelVisual = vfr, motor = false, steering = true },
                new AxleInfo { leftWheel = rl, rightWheel = rr, leftWheelVisual = vrl, rightWheelVisual = vrr, motor = true, steering = false }
            };
            controller.ConfigureSetup(axles, visualRoot.transform, exitL.transform, exitR.transform, seatedDriverRoot, steeringWheel.transform);

            // Procedural engine & tyre skid audio
            AudioSource engineSrc = root.AddComponent<AudioSource>();
            engineSrc.spatialBlend = 0.6f;
            engineSrc.loop = true;
            engineSrc.playOnAwake = true;

            GameObject skidObj = new GameObject("SkidAudio");
            skidObj.transform.SetParent(root.transform, false);
            AudioSource skidSrc = skidObj.AddComponent<AudioSource>();
            skidSrc.spatialBlend = 0.6f;
            skidSrc.loop = true;
            skidSrc.playOnAwake = true;
            skidSrc.volume = 0f;

            EngineAudio engineAudio = root.AddComponent<EngineAudio>();
            engineAudio.Configure(controller, engineSrc, skidSrc);

            return SaveAsPrefab(root, $"{PrefabDir}/Vehicle_PlayerSedan.prefab");
        }

        private static WheelCollider CreateWheelCollider(string wheelName, Transform parent, Vector3 localPos)
        {
            GameObject go = new GameObject(wheelName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            WheelCollider wc = go.AddComponent<WheelCollider>();
            wc.mass = 20f;
            wc.radius = 0.34f;
            wc.wheelDampingRate = 0.25f;
            wc.suspensionDistance = 0.22f;
            wc.forceAppPointDistance = 0.1f;

            JointSpring spring = wc.suspensionSpring;
            spring.spring = 35000f;
            spring.damper = 4500f;
            spring.targetPosition = 0.5f;
            wc.suspensionSpring = spring;

            WheelFrictionCurve fwd = wc.forwardFriction;
            fwd.extremumSlip = 0.4f;
            fwd.extremumValue = 1.0f;
            fwd.asymptoteSlip = 0.8f;
            fwd.asymptoteValue = 0.6f;
            fwd.stiffness = 1.6f;
            wc.forwardFriction = fwd;

            WheelFrictionCurve side = wc.sidewaysFriction;
            side.extremumSlip = 0.25f;
            side.extremumValue = 1.0f;
            side.asymptoteSlip = 0.5f;
            side.asymptoteValue = 0.75f;
            side.stiffness = 1.8f;
            wc.sidewaysFriction = side;

            return wc;
        }

        private static Transform CreateWheelVisual(string visualName, Transform parent, Vector3 localPos, Material tireMat, Material rimMat)
        {
            GameObject pivot = new GameObject(visualName);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = localPos;

            GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "Placeholder_TireMesh";
            cyl.transform.SetParent(pivot.transform, false);
            cyl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            cyl.transform.localScale = new Vector3(0.68f, 0.14f, 0.68f);
            Object.DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.GetComponent<Renderer>().sharedMaterial = tireMat;

            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Placeholder_Rim";
            rim.transform.SetParent(pivot.transform, false);
            rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            rim.transform.localScale = new Vector3(0.40f, 0.155f, 0.40f);
            Object.DestroyImmediate(rim.GetComponent<Collider>());
            rim.GetComponent<Renderer>().sharedMaterial = rimMat;

            return pivot.transform;
        }

        private static GameObject BuildNpcPrefab(string prefabName, string anchorId, MaterialSet mats, Material shirtMat, Material pantsMat)
        {
            GameObject root = new GameObject(prefabName);

            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.radius = 0.4f;
            col.height = 1.8f;

            WorldAnchor anchor = root.AddComponent<WorldAnchor>();
            anchor.Configure(anchorId, 3.4f, new Vector3(0f, 2.3f, 0f));

            InteractableTrigger trigger = root.AddComponent<InteractableTrigger>();
            trigger.Configure(InteractableKind.Npc, anchorId, "ui.btn.talk", 3.4f);

            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            ProceduralHumanoid humanoid = ProceduralHumanoid.Build(
                visualRoot.transform,
                mats.CharSkin,
                shirtMat,
                pantsMat,
                mats.CharShoes,
                mats.CharHair,
                1.78f);
            humanoid.SetAutoRandomWave(true);

            return SaveAsPrefab(root, $"{PrefabDir}/{prefabName}.prefab");
        }

        private static GameObject BuildApartmentPrefab(MaterialSet mats)
        {
            GameObject root = new GameObject("Building_Apartment_Placeholder");
            MarkStaticEnvironment(root);

            // Single simple BoxCollider for Android & iOS physics performance
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 7.0f, 0f);
            col.size = new Vector3(14f, 14f, 14f);

            // LOD0: PBR Facade + Ground Plinth + Parapet + Rooftop HVAC + Warm Dusk Windows
            GameObject lod0 = new GameObject("LOD0_Detailed");
            lod0.transform.SetParent(root.transform, false);

            GameObject lod0Plinth = CreatePrimitiveChild("GroundPlinth", PrimitiveType.Cube, lod0.transform,
                new Vector3(0f, 1.4f, 0f), new Vector3(14.3f, 2.8f, 14.3f), mats.Concrete, removeCollider: true);
            GameObject lod0Main = CreatePrimitiveChild("MainBlock", PrimitiveType.Cube, lod0.transform,
                new Vector3(0f, 8.2f, 0f), new Vector3(14f, 11.2f, 14f), mats.BuildingFacadeA, removeCollider: true);
            GameObject lod0Roof = CreatePrimitiveChild("RoofParapet", PrimitiveType.Cube, lod0.transform,
                new Vector3(0f, 14.1f, 0f), new Vector3(14.5f, 0.7f, 14.5f), mats.PlasterTrim, removeCollider: true);
            GameObject lod0Hvac = CreatePrimitiveChild("RoofHVAC", PrimitiveType.Cube, lod0.transform,
                new Vector3(2.4f, 15.1f, -1.8f), new Vector3(3.2f, 1.4f, 2.6f), mats.GarageRoofMetal, removeCollider: true);
            GameObject lod0Win1 = CreatePrimitiveChild("LitWindow_East", PrimitiveType.Cube, lod0.transform,
                new Vector3(7.04f, 6.2f, 2.2f), new Vector3(0.10f, 1.25f, 2.1f), mats.WindowLitWarm, removeCollider: true);
            GameObject lod0Win2 = CreatePrimitiveChild("LitWindow_North", PrimitiveType.Cube, lod0.transform,
                new Vector3(-2.4f, 9.8f, 7.04f), new Vector3(2.2f, 1.25f, 0.10f), mats.WindowLitWarm, removeCollider: true);
            GameObject lod0Win3 = CreatePrimitiveChild("LitWindow_West", PrimitiveType.Cube, lod0.transform,
                new Vector3(-7.04f, 8.0f, -2.5f), new Vector3(0.10f, 1.25f, 2.4f), mats.WindowLitWarm, removeCollider: true);

            // LOD1: Single simplified PBR block for distance rendering
            GameObject lod1 = new GameObject("LOD1_Medium");
            lod1.transform.SetParent(root.transform, false);
            GameObject lod1Main = CreatePrimitiveChild("SimpleBlock", PrimitiveType.Cube, lod1.transform,
                new Vector3(0f, 7.0f, 0f), new Vector3(14f, 14f, 14f), mats.BuildingFacadeA, removeCollider: true);

            MarkStaticEnvironment(lod0);
            MarkStaticEnvironment(lod1);

            LODGroup lodGroup = root.AddComponent<LODGroup>();
            LOD[] lods =
            {
                new LOD(0.24f, new[]
                {
                    lod0Plinth.GetComponent<Renderer>(),
                    lod0Main.GetComponent<Renderer>(),
                    lod0Roof.GetComponent<Renderer>(),
                    lod0Hvac.GetComponent<Renderer>(),
                    lod0Win1.GetComponent<Renderer>(),
                    lod0Win2.GetComponent<Renderer>(),
                    lod0Win3.GetComponent<Renderer>()
                }),
                new LOD(0.04f, new[] { lod1Main.GetComponent<Renderer>() })
            };
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            return SaveAsPrefab(root, $"{PrefabDir}/Building_Apartment_Placeholder.prefab");
        }

        private static GameObject BuildTrafficPlaceholderPrefab(MaterialSet mats)
        {
            GameObject root = new GameObject("Vehicle_Traffic_Placeholder");
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.72f, 0f);
            col.size = new Vector3(1.85f, 1.35f, 4.15f);

            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            CreatePrimitiveChild("Body", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 0.58f, 0f), new Vector3(1.82f, 0.56f, 4.10f), mats.TrafficBody, removeCollider: true);
            CreatePrimitiveChild("Roof", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 1.12f, -0.15f), new Vector3(1.50f, 0.48f, 1.95f), mats.SedanGlass, removeCollider: true);
            CreatePrimitiveChild("HeadL", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(-0.62f, 0.62f, 2.06f), new Vector3(0.32f, 0.14f, 0.06f), mats.LampGlow, removeCollider: true);
            CreatePrimitiveChild("HeadR", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0.62f, 0.62f, 2.06f), new Vector3(0.32f, 0.14f, 0.06f), mats.LampGlow, removeCollider: true);
            CreatePrimitiveChild("TailL", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(-0.62f, 0.64f, -2.06f), new Vector3(0.34f, 0.14f, 0.06f), mats.TaillightGlow, removeCollider: true);
            CreatePrimitiveChild("TailR", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0.62f, 0.64f, -2.06f), new Vector3(0.34f, 0.14f, 0.06f), mats.TaillightGlow, removeCollider: true);

            return SaveAsPrefab(root, $"{PrefabDir}/Vehicle_Traffic_Placeholder.prefab");
        }

        private static MissionCatalog CreateMissionDataAssets()
        {
            DialogueData dlgM1 = CreateOrLoadAsset<DialogueData>($"{DialogueDir}/DLG_M01_Mechanic.asset");
            dlgM1.lines = new List<DialogueLine>
            {
                new DialogueLine { speakerKey = "npc.mechanic.name", textKey = "m01.dlg.1" },
                new DialogueLine { speakerKey = "npc.mechanic.name", textKey = "m01.dlg.2" },
                new DialogueLine { speakerKey = "npc.mechanic.name", textKey = "m01.dlg.3" }
            };
            dlgM1.choices = new List<DialogueChoice>();
            EditorUtility.SetDirty(dlgM1);

            DialogueData dlgM2 = CreateOrLoadAsset<DialogueData>($"{DialogueDir}/DLG_M02_HarborReceiver.asset");
            dlgM2.lines = new List<DialogueLine>
            {
                new DialogueLine { speakerKey = "npc.harbor_receiver.name", textKey = "m02.dlg.1" },
                new DialogueLine { speakerKey = "npc.harbor_receiver.name", textKey = "m02.dlg.2" },
                new DialogueLine { speakerKey = "npc.harbor_receiver.name", textKey = "m02.dlg.3" }
            };
            dlgM2.choices = new List<DialogueChoice>();
            EditorUtility.SetDirty(dlgM2);

            MissionData m01 = CreateOrLoadAsset<MissionData>($"{MissionDir}/Mission_01_GetTheKeys.asset");
            m01.missionId = "m01_get_the_keys";
            m01.titleKey = "m01.title";
            m01.summaryKey = "m01.summary";
            m01.nextMissionId = "m02_last_delivery";
            m01.autoStartNextMission = true;
            m01.retryPlayerAnchorId = "spawn_garage_foot";
            m01.retryVehicleAnchorId = "spawn_garage_sedan";
            m01.retryStartInVehicle = false;
            m01.steps = new List<MissionStepData>
            {
                new MissionStepData
                {
                    stepType = MissionStepType.TalkToNpc,
                    objectiveKey = "m01.step1.objective",
                    targetAnchorId = "npc_mechanic",
                    dialogueAsset = dlgM1,
                    unlocksVehicleKeys = true
                },
                new MissionStepData
                {
                    stepType = MissionStepType.EnterVehicle,
                    objectiveKey = "m01.step2.objective",
                    targetAnchorId = "player_sedan"
                }
            };
            EditorUtility.SetDirty(m01);

            MissionData m02 = CreateOrLoadAsset<MissionData>($"{MissionDir}/Mission_02_LastDelivery.asset");
            m02.missionId = "m02_last_delivery";
            m02.titleKey = "m02.title";
            m02.summaryKey = "m02.summary";
            m02.nextMissionId = string.Empty;
            m02.autoStartNextMission = false;
            m02.retryPlayerAnchorId = "spawn_garage_foot";
            m02.retryVehicleAnchorId = "spawn_garage_sedan";
            m02.retryStartInVehicle = true;
            m02.steps = new List<MissionStepData>
            {
                new MissionStepData
                {
                    stepType = MissionStepType.DeliverToZone,
                    objectiveKey = "m02.step1.objective",
                    targetAnchorId = "harbor_delivery_zone",
                    triggerRadius = 7.5f,
                    timeLimitSeconds = 95f
                },
                new MissionStepData
                {
                    stepType = MissionStepType.TalkToNpc,
                    objectiveKey = "m02.step2.objective",
                    targetAnchorId = "npc_harbor_receiver",
                    dialogueAsset = dlgM2
                }
            };
            EditorUtility.SetDirty(m02);

            MissionCatalog catalog = CreateOrLoadAsset<MissionCatalog>($"{MissionDir}/MissionCatalog.asset");
            catalog.missions = new List<MissionData> { m01, m02 };
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void BuildTestScene(
            MaterialSet mats,
            PrefabSet prefabs,
            MissionCatalog catalog,
            UniversalRenderPipelineAsset lowUrp,
            UniversalRenderPipelineAsset medUrp,
            VolumeProfile eveningVolume)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Realistic Evening Coastal HDRI Skybox & Atmospheric Dusk Lighting ---
            RenderSettings.skybox = mats.EveningSkybox;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1.0f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.26f, 0.24f, 0.30f);
            RenderSettings.fogDensity = 0.0028f;

            GameObject sunObj = new GameObject("Sun_EveningCoastal");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.68f, 0.42f);
            sun.intensity = 1.85f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
            sunObj.transform.rotation = Quaternion.Euler(14f, -52f, 0f);
            RenderSettings.sun = sun;

            // Global Post-Processing Volume (ACES Tonemapping + Warm Bloom + Subtle Vignette)
            GameObject volObj = new GameObject("GlobalVolume_EveningCoastal");
            Volume vol = volObj.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = eveningVolume;

            // --- Systems Root ---
            GameObject systems = new GameObject("--- SYSTEMS ---");
            systems.AddComponent<SaveManager>();
            systems.AddComponent<LocalizationService>();
            systems.AddComponent<InputHub>();
            systems.AddComponent<PerformanceMonitor>();

            GraphicsProfileManager gfxMgr = systems.AddComponent<GraphicsProfileManager>();
            gfxMgr.ConfigureAssets(lowUrp, medUrp);

            PlayerModeController modeController = systems.AddComponent<PlayerModeController>();
            MissionManager missionManager = systems.AddComponent<MissionManager>();

            // --- Third-Person Camera ---
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 58f;
            cam.farClipPlane = 480f;
            camObj.AddComponent<AudioListener>();
            UniversalAdditionalCameraData camData = camObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            ThirdPersonCameraRig camRig = camObj.AddComponent<ThirdPersonCameraRig>();

            // --- World Environment ---
            GameObject env = new GameObject("--- WORLD_ENVIRONMENT ---");

            // Main urban ground & reflective coastal sea
            CreateStaticBlock("Ground_CoastalCity", env.transform, new Vector3(-18f, -0.5f, 125f), new Vector3(114f, 1f, 350f), mats.Ground);
            GameObject seaObj = CreateStaticBlock("Coastal_Sea", env.transform, new Vector3(155f, -1.6f, 125f), new Vector3(240f, 1f, 520f), mats.SeaWater, addCollider: false);
            CreateStaticBlock("Coastal_Seawall", env.transform, new Vector3(36.8f, 0.35f, 125f), new Vector3(1.8f, 1.5f, 330f), mats.Concrete);
            CreateStaticBlock("Coastal_Promenade", env.transform, new Vector3(33.2f, 0.08f, 122f), new Vector3(5.2f, 0.16f, 256f), mats.Sidewalk);

            // --- ROADS (1K CC0 Asphalt 012 + Sidewalks + Painted Lane Markings) ---
            CreateStaticBlock("Road_GarageConnector", env.transform, new Vector3(0f, 0.02f, 0f), new Vector3(64f, 0.08f, 12f), mats.RoadAsphaltCross, addCollider: false);
            CreateStaticBlock("Road_CoastalMain", env.transform, new Vector3(24f, 0.03f, 122f), new Vector3(13f, 0.08f, 256f), mats.RoadAsphaltMain, addCollider: false);
            CreateStaticBlock("Road_BackAlleyRoute", env.transform, new Vector3(-26f, 0.03f, 122f), new Vector3(8.5f, 0.08f, 256f), mats.RoadAsphaltMain, addCollider: false);
            CreateStaticBlock("Road_CrossStreet_South", env.transform, new Vector3(-1f, 0.025f, 92f), new Vector3(54f, 0.08f, 9f), mats.RoadAsphaltCross, addCollider: false);
            CreateStaticBlock("Road_CrossStreet_North", env.transform, new Vector3(-1f, 0.025f, 174f), new Vector3(54f, 0.08f, 9f), mats.RoadAsphaltCross, addCollider: false);
            CreateStaticBlock("Road_HarborConnector", env.transform, new Vector3(-1f, 0.025f, 246f), new Vector3(62f, 0.08f, 14f), mats.RoadAsphaltCross, addCollider: false);

            // Sidewalks along interior blocks
            CreateStaticBlock("Sidewalk_MainWest", env.transform, new Vector3(15.6f, 0.08f, 122f), new Vector3(3.4f, 0.16f, 240f), mats.Sidewalk);
            CreateStaticBlock("Sidewalk_AlleyEast", env.transform, new Vector3(-20.4f, 0.08f, 122f), new Vector3(2.4f, 0.16f, 240f), mats.Sidewalk);
            CreateStaticBlock("Sidewalk_AlleyWest", env.transform, new Vector3(-31.8f, 0.08f, 122f), new Vector3(2.8f, 0.16f, 240f), mats.Sidewalk);

            // Crisp road lane markings (Coastal Main Road dashed centerline + edge stripes)
            GameObject markingsRoot = new GameObject("Road_Markings");
            markingsRoot.transform.SetParent(env.transform, false);
            CreateStaticBlock("Coastal_EdgeLine_East", markingsRoot.transform, new Vector3(29.8f, 0.08f, 122f), new Vector3(0.18f, 0.02f, 244f), mats.RoadMarkingYellow, addCollider: false);
            CreateStaticBlock("Coastal_EdgeLine_West", markingsRoot.transform, new Vector3(18.2f, 0.08f, 122f), new Vector3(0.18f, 0.02f, 244f), mats.RoadMarkingWhite, addCollider: false);
            for (int z = 12; z <= 232; z += 11)
            {
                CreateStaticBlock($"CenterDash_{z}", markingsRoot.transform, new Vector3(24f, 0.08f, z), new Vector3(0.22f, 0.02f, 4.8f), mats.RoadMarkingWhite, addCollider: false);
            }

            // Harbor Delivery Bay painted outline
            CreateStaticBlock("DeliveryBay_North", markingsRoot.transform, new Vector3(11f, 0.14f, 253f), new Vector3(9.5f, 0.02f, 0.3f), mats.RoadMarkingYellow, addCollider: false);
            CreateStaticBlock("DeliveryBay_South", markingsRoot.transform, new Vector3(11f, 0.14f, 243f), new Vector3(9.5f, 0.02f, 0.3f), mats.RoadMarkingYellow, addCollider: false);
            CreateStaticBlock("DeliveryBay_West", markingsRoot.transform, new Vector3(6.2f, 0.14f, 248f), new Vector3(0.3f, 0.02f, 10.2f), mats.RoadMarkingYellow, addCollider: false);
            CreateStaticBlock("DeliveryBay_East", markingsRoot.transform, new Vector3(15.8f, 0.14f, 248f), new Vector3(0.3f, 0.02f, 10.2f), mats.RoadMarkingYellow, addCollider: false);

            // --- REPAIR GARAGE (Tamirhane, z ~ 0) ---
            GameObject garageRoot = new GameObject("Zone_RepairGarage");
            garageRoot.transform.SetParent(env.transform, false);
            CreateStaticBlock("Garage_Pad", garageRoot.transform, new Vector3(-2f, 0.04f, 7f), new Vector3(26f, 0.12f, 19f), mats.HarborDock);
            CreateStaticBlock("Garage_BackWall", garageRoot.transform, new Vector3(-10f, 3.0f, 14.5f), new Vector3(15f, 6f, 1f), mats.GarageBrick);
            CreateStaticBlock("Garage_SideWallL", garageRoot.transform, new Vector3(-17f, 3.0f, 9.5f), new Vector3(1f, 6f, 11f), mats.GarageBrick);
            CreateStaticBlock("Garage_SideWallR", garageRoot.transform, new Vector3(-2.8f, 3.0f, 11.8f), new Vector3(1f, 6f, 6.4f), mats.GarageBrick);
            CreateStaticBlock("Garage_Roof", garageRoot.transform, new Vector3(-10f, 6.2f, 9.5f), new Vector3(16.2f, 0.6f, 12.2f), mats.GarageRoofMetal);
            CreateStaticBlock("Garage_SignFascia", garageRoot.transform, new Vector3(-10f, 6.5f, 3.4f), new Vector3(12f, 0.8f, 0.35f), mats.LampGlow, addCollider: false);

            // Real Poly Haven CC0 Props at Marco's Garage
            PlacePrefabInstance(prefabs.RollerShutterDoor, garageRoot.transform, new Vector3(-10.5f, 0.10f, 13.85f), Quaternion.Euler(0f, 180f, 0f));
            PlacePrefabInstance(prefabs.ToolCart, garageRoot.transform, new Vector3(-7.4f, 0.10f, 8.2f), Quaternion.Euler(0f, 28f, 0f));
            PlacePrefabInstance(prefabs.OilBarrel, garageRoot.transform, new Vector3(-14.6f, 0.10f, 11.2f), Quaternion.Euler(0f, 20f, 0f));
            PlacePrefabInstance(prefabs.OilBarrel, garageRoot.transform, new Vector3(-13.6f, 0.10f, 11.8f), Quaternion.Euler(0f, 115f, 0f));
            PlacePrefabInstance(prefabs.OldTyre, garageRoot.transform, new Vector3(-14.8f, 0.10f, 5.2f), Quaternion.identity);
            PlacePrefabInstance(prefabs.OldTyre, garageRoot.transform, new Vector3(-14.8f, 0.38f, 5.2f), Quaternion.Euler(0f, 40f, 0f));
            PlacePrefabInstance(prefabs.WoodenCrate, garageRoot.transform, new Vector3(-1.4f, 0.10f, 12.6f), Quaternion.Euler(0f, 12f, 0f));
            PlacePrefabInstance(prefabs.FloodLights, garageRoot.transform, new Vector3(-1.5f, 0.10f, 9.2f), Quaternion.Euler(0f, -135f, 0f));
            PlacePrefabInstance(prefabs.ConcreteBarrier, garageRoot.transform, new Vector3(-16.2f, 0.04f, -3.5f), Quaternion.Euler(0f, 90f, 0f));
            PlacePrefabInstance(prefabs.ConcreteBarrier, garageRoot.transform, new Vector3(10.5f, 0.04f, 13.5f), Quaternion.identity);
            CreatePointLamp("Lamp_GarageBay", garageRoot.transform, new Vector3(-6.5f, 5.2f, 7.5f), new Color(1f, 0.78f, 0.45f), 22f, 3.4f);

            // --- SMALL TOWN ("Liman Kasabası") ---
            BuildSmallTown(env.transform, mats, prefabs);

            // Real Poly Haven Street Lamps along Coastal Promenade & Back Alleys
            for (int z = 20; z <= 220; z += 40)
            {
                PlacePrefabInstance(prefabs.StreetLamp, env.transform, new Vector3(32.8f, 0.16f, z), Quaternion.Euler(0f, -90f, 0f));
                CreatePointLamp($"StreetLight_Coastal_{z}", env.transform, new Vector3(31.5f, 5.2f, z), new Color(1.0f, 0.78f, 0.44f), 20f, 2.2f);
            }
            for (int z = 45; z <= 205; z += 80)
            {
                PlacePrefabInstance(prefabs.StreetLamp, env.transform, new Vector3(-31.6f, 0.16f, z), Quaternion.Euler(0f, 90f, 0f));
                CreatePointLamp($"StreetLight_Alley_{z}", env.transform, new Vector3(-30.0f, 5.2f, z), new Color(1.0f, 0.76f, 0.42f), 18f, 1.9f);
            }

            // --- SMALL CARGO HARBOR (Yük Limanı, z ~ 242..276) ---
            GameObject harborRoot = new GameObject("Zone_CargoHarbor");
            harborRoot.transform.SetParent(env.transform, false);
            CreateStaticBlock("Harbor_QuayDeck", harborRoot.transform, new Vector3(10f, 0.05f, 260f), new Vector3(58f, 0.18f, 36f), mats.HarborDock);
            CreateStaticBlock("Harbor_Warehouse", harborRoot.transform, new Vector3(-12f, 4.0f, 268f), new Vector3(18f, 8f, 14f), mats.GarageBrick);
            CreateStaticBlock("Harbor_WarehouseRoof", harborRoot.transform, new Vector3(-12f, 8.3f, 268f), new Vector3(19f, 0.7f, 15f), mats.GarageRoofMetal);
            PlacePrefabInstance(prefabs.RollerShutterDoor, harborRoot.transform, new Vector3(-12f, 0.14f, 260.85f), Quaternion.Euler(0f, 180f, 0f));

            // Corrugated steel shipping containers
            CreateStaticBlock("Harbor_Container_01", harborRoot.transform, new Vector3(24f, 1.6f, 266f), new Vector3(3.2f, 3.0f, 9.5f), mats.ContainerRed);
            CreateStaticBlock("Harbor_Container_02", harborRoot.transform, new Vector3(28f, 1.6f, 266f), new Vector3(3.2f, 3.0f, 9.5f), mats.ContainerBlue);
            CreateStaticBlock("Harbor_Container_03", harborRoot.transform, new Vector3(26f, 4.6f, 266f), new Vector3(3.2f, 3.0f, 9.5f), mats.ContainerOchre);
            CreateStaticBlock("Harbor_Container_04", harborRoot.transform, new Vector3(19f, 1.6f, 270f), new Vector3(3.2f, 3.0f, 9.5f), mats.ContainerBlue);

            // Harbor Gantry Crane silhouette (adds dramatic coastal port skyline!)
            GameObject crane = new GameObject("Harbor_GantryCrane");
            crane.transform.SetParent(harborRoot.transform, false);
            CreateStaticBlock("Crane_Leg_FL", crane.transform, new Vector3(31f, 8.5f, 250f), new Vector3(1.1f, 17f, 1.1f), mats.GantrySteel);
            CreateStaticBlock("Crane_Leg_FR", crane.transform, new Vector3(31f, 8.5f, 260f), new Vector3(1.1f, 17f, 1.1f), mats.GantrySteel);
            CreateStaticBlock("Crane_Leg_BL", crane.transform, new Vector3(21f, 8.5f, 250f), new Vector3(1.1f, 17f, 1.1f), mats.GantrySteel);
            CreateStaticBlock("Crane_Leg_BR", crane.transform, new Vector3(21f, 8.5f, 260f), new Vector3(1.1f, 17f, 1.1f), mats.GantrySteel);
            CreateStaticBlock("Crane_Boom", crane.transform, new Vector3(36f, 17.4f, 255f), new Vector3(38f, 1.5f, 3.2f), mats.GantrySteel, addCollider: false);
            CreateStaticBlock("Crane_Cab", crane.transform, new Vector3(26f, 15.2f, 255f), new Vector3(4.2f, 2.8f, 3.6f), mats.SedanGlass, addCollider: false);
            CreateStaticBlock("Crane_TopBeacon", crane.transform, new Vector3(31f, 18.7f, 255f), new Vector3(0.6f, 0.6f, 0.6f), mats.RedBeaconGlow, addCollider: false);

            // Real Poly Haven CC0 Harbor Props (Crates, Barrels, Concrete Barriers, Flood Lights, Ocean Buoys)
            PlacePrefabInstance(prefabs.WoodenCrate, harborRoot.transform, new Vector3(5.2f, 0.14f, 254.2f), Quaternion.Euler(0f, 14f, 0f));
            PlacePrefabInstance(prefabs.WoodenCrate, harborRoot.transform, new Vector3(6.6f, 0.14f, 254.6f), Quaternion.Euler(0f, -8f, 0f));
            PlacePrefabInstance(prefabs.WoodenCrate, harborRoot.transform, new Vector3(5.8f, 1.18f, 254.4f), Quaternion.Euler(0f, 32f, 0f));
            PlacePrefabInstance(prefabs.OilBarrel, harborRoot.transform, new Vector3(-2.5f, 0.14f, 257.5f), Quaternion.Euler(0f, 45f, 0f));
            PlacePrefabInstance(prefabs.OilBarrel, harborRoot.transform, new Vector3(-1.4f, 0.14f, 258.1f), Quaternion.Euler(0f, 120f, 0f));
            PlacePrefabInstance(prefabs.FloodLights, harborRoot.transform, new Vector3(17.2f, 0.14f, 256.5f), Quaternion.Euler(0f, -145f, 0f));
            PlacePrefabInstance(prefabs.ConcreteBarrier, harborRoot.transform, new Vector3(35.5f, 0.14f, 250f), Quaternion.Euler(0f, 90f, 0f));
            PlacePrefabInstance(prefabs.ConcreteBarrier, harborRoot.transform, new Vector3(35.5f, 0.14f, 256f), Quaternion.Euler(0f, 90f, 0f));
            PlacePrefabInstance(prefabs.ConcreteBarrier, harborRoot.transform, new Vector3(35.5f, 0.14f, 262f), Quaternion.Euler(0f, 90f, 0f));
            CreatePointLamp("Lamp_HarborDock", harborRoot.transform, new Vector3(11f, 8.2f, 251f), new Color(1f, 0.82f, 0.50f), 32f, 4.2f);

            // Floating buoys in the evening sea + lightweight wave animation
            GameObject buoy1 = PlacePrefabInstance(prefabs.OceanBuoy, env.transform, new Vector3(52f, -1.35f, 115f), Quaternion.identity);
            GameObject buoy2 = PlacePrefabInstance(prefabs.OceanBuoy, env.transform, new Vector3(56f, -1.35f, 245f), Quaternion.Euler(0f, 65f, 0f));
            SeaWaveAnimator waveAnim = seaObj.AddComponent<SeaWaveAnimator>();
            waveAnim.Configure(seaObj.GetComponent<Renderer>(), new[] { buoy1.transform, buoy2.transform });

            // --- WORLD ANCHORS & SPAWNS ---
            GameObject anchorsRoot = new GameObject("--- WORLD_ANCHORS ---");
            CreateWorldAnchor("Spawn_GarageFoot", "spawn_garage_foot", anchorsRoot.transform, new Vector3(-8.5f, 0.14f, -6.0f), Quaternion.Euler(0f, 25f, 0f), 2f);
            CreateWorldAnchor("Spawn_GarageSedan", "spawn_garage_sedan", anchorsRoot.transform, new Vector3(3.5f, 0.28f, 4.5f), Quaternion.Euler(0f, 90f, 0f), 3.5f);
            CreateWorldAnchor("Harbor_DeliveryZone", "harbor_delivery_zone", anchorsRoot.transform, new Vector3(11f, 0.18f, 248f), Quaternion.identity, 7.5f);

            // --- ACTORS (Player, Sedan, NPCs) ---
            GameObject actorsRoot = new GameObject("--- ACTORS ---");
            GameObject playerObj = PlacePrefabInstance(prefabs.Player, actorsRoot.transform, new Vector3(-8.5f, 0.14f, -6.0f), Quaternion.Euler(0f, 25f, 0f));
            GameObject sedanObj = PlacePrefabInstance(prefabs.PlayerSedan, actorsRoot.transform, new Vector3(3.5f, 0.28f, 4.5f), Quaternion.Euler(0f, 90f, 0f));
            PlacePrefabInstance(prefabs.NpcMechanic, actorsRoot.transform, new Vector3(-4.5f, 0.10f, 6.2f), Quaternion.Euler(0f, 165f, 0f));
            PlacePrefabInstance(prefabs.NpcHarborReceiver, actorsRoot.transform, new Vector3(2.2f, 0.16f, 255.5f), Quaternion.Euler(0f, 160f, 0f));

            modeController.ConfigureReferences(
                playerObj.GetComponent<PlayerFootController>(),
                sedanObj.GetComponent<SedanVehicleController>(),
                camRig);

            // --- 3D Objective Beacon ---
            ObjectiveBeacon beacon = BuildObjectiveBeacon(mats.BeaconGlow);
            missionManager.Configure(catalog, beacon);

            // --- Pooled Ambient Traffic along Coastal Loop ---
            GameObject trafficRoot = new GameObject("AmbientTrafficManager");
            AmbientTrafficManager trafficMgr = trafficRoot.AddComponent<AmbientTrafficManager>();
            List<Transform> waypoints = new List<Transform>();
            Vector3[] wpCoords =
            {
                new Vector3(27f, 0.1f, 15f),
                new Vector3(27f, 0.1f, 92f),
                new Vector3(27f, 0.1f, 174f),
                new Vector3(27f, 0.1f, 232f),
                new Vector3(21f, 0.1f, 232f),
                new Vector3(21f, 0.1f, 174f),
                new Vector3(21f, 0.1f, 92f),
                new Vector3(21f, 0.1f, 15f)
            };
            for (int i = 0; i < wpCoords.Length; i++)
            {
                GameObject wp = new GameObject($"WP_{i:00}");
                wp.transform.SetParent(trafficRoot.transform, false);
                wp.transform.position = wpCoords[i];
                waypoints.Add(wp.transform);
            }
            trafficMgr.Configure(prefabs.TrafficVehicle, waypoints);

            // --- HUD & InputSystem EventSystem ---
            BuildHudCanvas();

            DynamicGI.UpdateEnvironment();
            EditorSceneManager.SaveScene(scene, TestScenePath);
            RegisterSceneInBuildSettings(TestScenePath);
        }

        private static ObjectiveBeacon BuildObjectiveBeacon(Material beaconMat)
        {
            GameObject root = new GameObject("ObjectiveBeacon");
            ObjectiveBeacon beacon = root.AddComponent<ObjectiveBeacon>();

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "GroundRing";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            ring.transform.localScale = new Vector3(4f, 0.08f, 4f);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.GetComponent<Renderer>().sharedMaterial = beaconMat;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "FloatingDiamond";
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            marker.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
            marker.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = beaconMat;

            beacon.ConfigureParts(marker.transform, ring.transform);
            return beacon;
        }

        private static void BuildHudCanvas()
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();

            GameObject canvasObj = new GameObject("Canvas_GRIDROAM_HUD");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            GameHUDController hud = canvasObj.AddComponent<GameHUDController>();
            Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject safeRoot = CreateUiRect("SafeAreaRoot", canvasObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeRoot.AddComponent<SafeAreaFitter>();

            GameObject camPad = CreateUiRect("CameraTouchZone", safeRoot.transform, new Vector2(0.45f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            Image camPadImg = camPad.AddComponent<Image>();
            camPadImg.color = new Color(0f, 0f, 0f, 0f);
            camPad.AddComponent<CameraTouchZone>();

            GameObject missionBox = CreateUiPanel("MissionBox", safeRoot.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -24f), new Vector2(540f, 134f), new Color(0.07f, 0.10f, 0.15f, 0.78f));
            Text missionTitle = CreateUiText("MissionTitle", missionBox.transform, uiFont, "GET THE KEYS", 28, TextAnchor.UpperLeft,
                new Color(1f, 0.76f, 0.25f), new Vector2(18f, -14f), new Vector2(504f, 38f), FontStyle.Bold);
            Text missionObj = CreateUiText("MissionObjective", missionBox.transform, uiFont, "Walk to the repair garage and talk to Marco.", 22, TextAnchor.UpperLeft,
                Color.white, new Vector2(18f, -54f), new Vector2(504f, 68f));

            GameObject waypointBox = CreateUiPanel("WaypointIndicator", safeRoot.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(210f, 74f), new Color(0.07f, 0.10f, 0.15f, 0.78f));
            Text arrowText = CreateUiText("Arrow", waypointBox.transform, uiFont, "^", 38, TextAnchor.MiddleCenter,
                new Color(1f, 0.76f, 0.25f), new Vector2(14f, -8f), new Vector2(58f, 58f), FontStyle.Bold);
            Text distText = CreateUiText("Distance", waypointBox.transform, uiFont, "0 m", 26, TextAnchor.MiddleCenter,
                Color.white, new Vector2(76f, -12f), new Vector2(120f, 50f), FontStyle.Bold);
            ObjectiveWaypointIndicator waypointComp = canvasObj.AddComponent<ObjectiveWaypointIndicator>();
            waypointComp.Configure(waypointBox, arrowText.rectTransform, distText);

            GameObject timerBox = CreateUiPanel("TimerBox", safeRoot.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -108f), new Vector2(170f, 54f), new Color(0.15f, 0.08f, 0.08f, 0.85f));
            Text timerText = CreateUiText("TimerText", timerBox.transform, uiFont, "01:35", 30, TextAnchor.MiddleCenter,
                Color.white, new Vector2(0f, 0f), new Vector2(170f, 54f), FontStyle.Bold);

            GameObject tailBox = CreateUiPanel("TailWarningBox", safeRoot.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -170f), new Vector2(440f, 52f), new Color(0.14f, 0.46f, 0.28f, 0.80f));
            Text tailText = CreateUiText("TailWarningText", tailBox.transform, uiFont, "HOLDING SAFE DISTANCE", 22, TextAnchor.MiddleCenter,
                Color.white, Vector2.zero, new Vector2(440f, 52f), FontStyle.Bold);

            GameObject perfPanel = CreateUiPanel("TelemetryPanel", safeRoot.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -24f), new Vector2(460f, 44f), new Color(0.05f, 0.08f, 0.12f, 0.78f));
            Button perfBtn = perfPanel.AddComponent<Button>();
            perfBtn.targetGraphic = perfPanel.GetComponent<Image>();
            Text perfText = CreateUiText("TelemetryText", perfPanel.transform, uiFont, "FPS -- | -- ms | -- MB", 18, TextAnchor.MiddleCenter,
                new Color(0.65f, 0.96f, 0.75f), Vector2.zero, new Vector2(460f, 44f));

            var (gfxBtn, gfxLbl) = CreateUiButton("Btn_GraphicsTier", safeRoot.transform, uiFont, "GFX: MED",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-168f, -78f), new Vector2(150f, 48f), new Color(0.14f, 0.22f, 0.34f, 0.86f), 20);

            var (resetBtn, resetLbl) = CreateUiButton("Btn_ResetSave", safeRoot.transform, uiFont, "RESET",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -78f), new Vector2(126f, 48f), new Color(0.38f, 0.16f, 0.16f, 0.86f), 20);

            GameObject onFootRoot = CreateUiRect("OnFootControls", safeRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject joyBg = CreateUiPanel("VirtualJoystick_BG", onFootRoot.transform,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(60f, 58f), new Vector2(210f, 210f), new Color(0.10f, 0.14f, 0.20f, 0.56f));
            GameObject joyHandle = CreateUiPanel("Joystick_Handle", joyBg.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(88f, 88f), new Color(0.95f, 0.72f, 0.24f, 0.85f));
            VirtualJoystick joystick = joyBg.AddComponent<VirtualJoystick>();
            joystick.Configure(joyBg.GetComponent<RectTransform>(), joyHandle.GetComponent<RectTransform>(), 70f);

            var (interactBtn, interactLbl) = CreateUiButton("Btn_Interact", onFootRoot.transform, uiFont, "INTERACT",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-64f, 84f), new Vector2(220f, 92f), new Color(0.92f, 0.58f, 0.16f, 0.92f), 26);

            GameObject drivingRoot = CreateUiRect("DrivingControls", safeRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            HoldButton steerL = CreateHoldButton("Btn_SteerLeft", drivingRoot.transform, uiFont, "<",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(48f, 64f), new Vector2(150f, 130f), new Color(0.12f, 0.16f, 0.22f, 0.82f), 44);
            HoldButton steerR = CreateHoldButton("Btn_SteerRight", drivingRoot.transform, uiFont, ">",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(218f, 64f), new Vector2(150f, 130f), new Color(0.12f, 0.16f, 0.22f, 0.82f), 44);

            HoldButton brakeBtn = CreateHoldButton("Btn_Brake", drivingRoot.transform, uiFont, "BRAKE",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-232f, 64f), new Vector2(156f, 130f), new Color(0.55f, 0.18f, 0.16f, 0.85f), 26);
            HoldButton gasBtn = CreateHoldButton("Btn_Gas", drivingRoot.transform, uiFont, "GAS",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-54f, 64f), new Vector2(156f, 155f), new Color(0.18f, 0.56f, 0.30f, 0.88f), 28);

            var (exitBtn, exitLbl) = CreateUiButton("Btn_ExitCar", drivingRoot.transform, uiFont, "EXIT CAR",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-54f, 244f), new Vector2(180f, 66f), new Color(0.18f, 0.24f, 0.34f, 0.88f), 22);

            GameObject speedPanel = CreateUiPanel("Speedometer", drivingRoot.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 52f), new Vector2(210f, 64f), new Color(0.07f, 0.10f, 0.15f, 0.78f));
            Text speedText = CreateUiText("SpeedText", speedPanel.transform, uiFont, "0 KM/H", 28, TextAnchor.MiddleCenter,
                Color.white, Vector2.zero, new Vector2(210f, 64f), FontStyle.Bold);

            Text toastText = CreateUiText("StatusToast", safeRoot.transform, uiFont, "", 24, TextAnchor.MiddleCenter,
                new Color(1f, 0.85f, 0.35f), Vector2.zero, new Vector2(760f, 46f), FontStyle.Bold);
            toastText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            toastText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            toastText.rectTransform.pivot = new Vector2(0.5f, 0f);
            toastText.rectTransform.anchoredPosition = new Vector2(0f, 134f);

            Text hintText = CreateUiText("EditorControlsHint", safeRoot.transform, uiFont, "", 16, TextAnchor.LowerCenter,
                new Color(0.85f, 0.88f, 0.94f, 0.75f), Vector2.zero, new Vector2(820f, 30f));
            hintText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            hintText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            hintText.rectTransform.pivot = new Vector2(0.5f, 0f);
            hintText.rectTransform.anchoredPosition = new Vector2(0f, 12f);

            GameObject dlgPanel = CreateUiPanel("DialoguePanel", safeRoot.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 36f), new Vector2(1060f, 210f), new Color(0.06f, 0.09f, 0.14f, 0.94f));
            Text dlgSpeaker = CreateUiText("SpeakerName", dlgPanel.transform, uiFont, "Speaker", 26, TextAnchor.UpperLeft,
                new Color(1f, 0.76f, 0.25f), new Vector2(28f, -20f), new Vector2(760f, 38f), FontStyle.Bold);
            Text dlgBody = CreateUiText("DialogueBody", dlgPanel.transform, uiFont, "Line...", 24, TextAnchor.UpperLeft,
                Color.white, new Vector2(28f, -64f), new Vector2(780f, 122f));
            var (dlgBtn, dlgContinueLbl) = CreateUiButton("Btn_DialogueContinue", dlgPanel.transform, uiFont, "CONTINUE",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 24f), new Vector2(200f, 68f), new Color(0.92f, 0.58f, 0.16f, 0.95f), 22);

            GameObject failPanel = CreateUiPanel("MissionFailedPanel", safeRoot.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(660f, 290f), new Color(0.14f, 0.06f, 0.07f, 0.95f));
            Text failTitle = CreateUiText("FailedTitle", failPanel.transform, uiFont, "MISSION FAILED", 36, TextAnchor.MiddleCenter,
                new Color(1f, 0.36f, 0.32f), new Vector2(0f, -28f), new Vector2(660f, 54f), FontStyle.Bold);
            Text failReason = CreateUiText("FailedReason", failPanel.transform, uiFont, "Time ran out.", 24, TextAnchor.MiddleCenter,
                Color.white, new Vector2(40f, -94f), new Vector2(580f, 84f));
            var (retryBtn, retryLbl) = CreateUiButton("Btn_RetryMission", failPanel.transform, uiFont, "RETRY MISSION",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 28f), new Vector2(300f, 68f), new Color(0.92f, 0.58f, 0.16f, 0.96f), 24);

            GameObject bannerPanel = CreateUiPanel("MissionCompleteBanner", safeRoot.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 120f), new Vector2(640f, 128f), new Color(0.08f, 0.22f, 0.16f, 0.92f));
            Text bannerText = CreateUiText("BannerText", bannerPanel.transform, uiFont, "MISSION COMPLETE", 30, TextAnchor.MiddleCenter,
                new Color(0.75f, 1f, 0.80f), Vector2.zero, new Vector2(640f, 128f), FontStyle.Bold);

            hud.WireReferences(
                onFootRoot, drivingRoot,
                interactBtn, interactLbl,
                steerL, steerR, gasBtn, brakeBtn,
                exitBtn, exitLbl, speedText,
                missionTitle, missionObj,
                timerBox, timerText,
                tailBox, tailText, tailBox.GetComponent<Image>(),
                toastText, hintText,
                dlgPanel, dlgSpeaker, dlgBody, dlgBtn, dlgContinueLbl,
                failPanel, failTitle, failReason, retryBtn, retryLbl,
                bannerPanel, bannerText,
                gfxBtn, gfxLbl, resetBtn, resetLbl,
                perfPanel, perfText);
        }

        // --- Helper Builders ---
        private static GameObject CreatePrimitiveChild(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Material mat,
            bool removeCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (removeCollider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }

            if (mat != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }

            return go;
        }

        private static GameObject CreateStaticBlock(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material mat,
            bool addCollider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            if (!addCollider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }

            if (mat != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }

            MarkStaticEnvironment(go);
            return go;
        }

        private static void CreatePointLamp(string name, Transform parent, Vector3 pos, Color color, float range, float intensity)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Mixed;
        }

        private static void CreateWorldAnchor(string name, string id, Transform parent, Vector3 pos, Quaternion rot, float radius)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            WorldAnchor anchor = go.AddComponent<WorldAnchor>();
            anchor.Configure(id, radius, new Vector3(0f, 2.5f, 0f));
        }

        private static GameObject PlacePrefabInstance(GameObject prefab, Transform parent, Vector3 pos, Quaternion rot)
        {
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.transform.SetPositionAndRotation(pos, rot);
            return inst;
        }

        private static GameObject SaveAsPrefab(GameObject root, string prefabPath)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void MarkStaticEnvironment(GameObject go)
        {
            StaticEditorFlags flags = StaticEditorFlags.ContributeGI |
                                      StaticEditorFlags.BatchingStatic |
                                      StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.OccludeeStatic |
                                      StaticEditorFlags.ReflectionProbeStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            foreach (Transform child in go.transform)
            {
                MarkStaticEnvironment(child.gameObject);
            }
        }

        private static void RegisterSceneInBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene s in scenes)
            {
                if (s.path == scenePath)
                {
                    s.enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // --- UI Factory Helpers ---
        private static GameObject CreateUiRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return go;
        }

        private static GameObject CreateUiPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text CreateUiText(string name, Transform parent, Font font, string content, int fontSize, TextAnchor alignment, Color color, Vector2 pos, Vector2 size, FontStyle style = FontStyle.Normal)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = alignment;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        private static (Button button, Text label) CreateUiButton(
            string name,
            Transform parent,
            Font font,
            string labelText,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            Color color,
            int fontSize)
        {
            GameObject go = CreateUiPanel(name, parent, anchorMin, anchorMax, pivot, pos, size, color);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();

            Text lbl = CreateUiText("Label", go.transform, font, labelText, fontSize, TextAnchor.MiddleCenter, Color.white, Vector2.zero, size, FontStyle.Bold);
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = Vector2.one;
            lbl.rectTransform.offsetMin = Vector2.zero;
            lbl.rectTransform.offsetMax = Vector2.zero;

            return (btn, lbl);
        }

        private static HoldButton CreateHoldButton(
            string name,
            Transform parent,
            Font font,
            string labelText,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            Color normalColor,
            int fontSize)
        {
            GameObject go = CreateUiPanel(name, parent, anchorMin, anchorMax, pivot, pos, size, normalColor);
            Image img = go.GetComponent<Image>();
            HoldButton hold = go.AddComponent<HoldButton>();
            hold.ConfigureVisual(img, normalColor, new Color(0.95f, 0.62f, 0.18f, 0.94f), releaseOnExit: false);

            Text lbl = CreateUiText("Label", go.transform, font, labelText, fontSize, TextAnchor.MiddleCenter, Color.white, Vector2.zero, size, FontStyle.Bold);
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = Vector2.one;
            lbl.rectTransform.offsetMin = Vector2.zero;
            lbl.rectTransform.offsetMax = Vector2.zero;

            return hold;
        }
    }
}
