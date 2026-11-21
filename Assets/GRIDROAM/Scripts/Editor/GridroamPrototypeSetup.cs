using System.Collections.Generic;
using System.IO;
using Gridroam.CameraSystem;
using Gridroam.Core;
using Gridroam.Input;
using Gridroam.Jobs;
using Gridroam.Missions;
using Gridroam.Player;
using Gridroam.UI;
using Gridroam.Vehicle;
using Gridroam.World;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gridroam.Editor
{
    /// <summary>
    /// One-click Unity Editor setup tool for GRIDROAM (Android + iOS):
    /// - Configures shared Landscape settings, Android (API 26+, IL2CPP ARM64), iOS (iOS 15+, Defer System Gestures, IL2CPP)
    /// - Creates shared Mobile URP Low/Medium profiles + ACES/Bloom Post-Processing Volume
    /// - Generates 9-sliced UI sprites (UI_RoundedRect, UI_Circle, UI_Ring) and orthographic TownMap.png
    /// - Builds procedural jointed humanoid player/NPCs, 4 drivable vehicles with fuel, enterable houses & shops,
    ///   Side Jobs, Economy, Day/Night Cycle, Gas Station, Phone/Map, Minimap, and Turkish-first calm modern HUD.
    /// </summary>
    public static partial class GridroamPrototypeSetup
    {
        private const string RootDir = "Assets/GRIDROAM";
        private const string ArtDir = RootDir + "/Art";
        private const string ArtMatDir = ArtDir + "/Materials";
        private const string TexturesDir = ArtDir + "/Textures";
        private const string ModelsDir = ArtDir + "/Models";
        private const string BlenderModelsDir = ModelsDir + "/Blender";
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
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureCrossPlatformMobileSettings();
            ConfigureAllImportedTextures();

            GeneratedUiSprites uiSprites = GenerateUiSprites();
            var (lowUrp, medUrp) = CreateOrUpdateUrpProfiles();
            VolumeProfile eveningVolume = CreateOrUpdateEveningVolumeProfile();
            MaterialSet mats = CreateMaterials();
            PrefabSet prefabs = CreatePrefabs(mats);
            MissionCatalog catalog = CreateMissionDataAssets();

            BuildTestScene(mats, prefabs, catalog, lowUrp, medUrp, eveningVolume, uiSprites);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GRIDROAM] Open-World Liman Kasabası v5 (Side Jobs, Economy, Day/Night Cycle, Shops & Gas Station, Phone/Map & Minimap, Turkish UI) built successfully!");
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "GRIDROAM Open-World Liman Kasabası Ready (Android & iOS)",
                    "Liman Kasabası open world with Side Jobs (Pizza, Taxi, Street Cleanup, House Painting), Economy, Day/Night Cycle, Café/Market/Gas Station, Phone Map & Minimap, Turkish primary localization, and Missions 1 & 2 is ready.\n\nPress Play to explore!",
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

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, bundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

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
                RootDir, ArtDir, ArtMatDir, GeneratedArtDir, PrefabDir, DataDir, MissionDir, DialogueDir, SettingsDir, SceneDir
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
            public Material JobRingGlow;
            public Material LampGlow;
            public Material WindowLitWarm;
            public Material RedBeaconGlow;
            public Material ReverseLightOff;
            public Material CharSkin;
            public Material CharShirt;
            public Material CharPants;
            public Material CharShoes;
            public Material CharHair;
            public Material TownPaving;
            public Material TownRoofTile;
            public Material TownBrickHouse;
            public Material TownGrass;
            public Material CountryEarth;
            public Material CoastalCliffRock;
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
            public Material TownWoodFloor;
            public Material TownDoorWood;
            public Material TownInteriorPaintWarm;
            public Material TownInteriorPaintCream;
            public Material TownInteriorPaintPearl;
            public Material TownFabricGrey;
            public Material TownFabricTeal;
            public Material TownFabricMustard;
            public Material TownFurnitureWood;
            public Material TownPaintCan;
            public Material CarRed;
            public Material CarWhite;
            public Material CarDarkGreen;
            public Material BrakeCaliper;
            public Material CanRed;
            public Material CanBlue;
            public Material CanGreen;
            public Material CanOrange;
            public Material CharUndershirt;
            public Material CharBelt;
            public Material CharShoeSole;
            public Material CharEyeWhite;
            public Material CharEyeIris;
            public Material CharLips;
            public Material CharSkinMedium;
            public Material CharSkinWarm;
            public Material CharHairBrown;
            public Material CharHairBlonde;
            public Material CharPantsDenim;
            public Material CharPantsKhaki;
        }

        private enum BlenderCharacterVariant
        {
            CasualMale,
            CasualFemale,
            Worker
        }

        private static MaterialSet CreateMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Shader skyProcedural = Shader.Find("Skybox/Procedural");
            Material skyMat = CreateOrLoadMaterial("MAT_Skybox_EveningCoastal", skyProcedural != null ? skyProcedural : urpLit, Color.white, 0f);
            if (skyProcedural != null)
            {
                skyMat.SetFloat("_SunDisk", 2f);
                skyMat.SetFloat("_SunSize", 0.04f);
                skyMat.SetFloat("_SunSizeConvergence", 5f);
                skyMat.SetFloat("_AtmosphereThickness", 1.0f);
                skyMat.SetColor("_SkyTint", new Color(0.50f, 0.52f, 0.56f));
                skyMat.SetColor("_GroundColor", new Color(0.34f, 0.38f, 0.30f));
                skyMat.SetFloat("_Exposure", 1.10f);
                EditorUtility.SetDirty(skyMat);
            }

            MaterialSet set = new MaterialSet
            {
                EveningSkybox = skyMat,
                RoadAsphaltMain = CreatePbrMaterial("MAT_Road_Asphalt012", urpLit, "Asphalt012", new Color(0.72f, 0.72f, 0.76f), 0.34f, 0f, new Vector2(2f, 36f), 0.9f),
                RoadAsphaltCross = CreatePbrMaterial("MAT_Road_Asphalt012_Cross", urpLit, "Asphalt012", new Color(0.72f, 0.72f, 0.76f), 0.34f, 0f, new Vector2(26f, 2f), 0.9f),
                RoadMarkingWhite = CreateOrLoadMaterial("MAT_Road_Marking_White", urpLit, new Color(0.88f, 0.88f, 0.86f), 0.18f),
                RoadMarkingYellow = CreateOrLoadMaterial("MAT_Road_Marking_Yellow", urpLit, new Color(0.92f, 0.72f, 0.18f), 0.22f),
                Ground = CreatePbrMaterial("MAT_Ground_Urban", urpLit, "Concrete034", new Color(0.55f, 0.55f, 0.57f), 0.14f, 0f, new Vector2(22f, 64f), 0.7f),
                Sidewalk = CreatePbrMaterial("MAT_Sidewalk_Paving", urpLit, "PavingStones070", new Color(0.84f, 0.82f, 0.80f), 0.18f, 0f, new Vector2(2f, 44f), 0.85f),
                SeaWater = CreateOrLoadMaterial("MAT_Sea_Evening", urpLit, new Color(0.06f, 0.19f, 0.30f), 0.92f, 0.16f),
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
                JobRingGlow = CreateOrLoadMaterial("MAT_Job_RingTeal", urpLit, new Color(0.31f, 0.70f, 0.66f), 0.35f, 0f, new Color(0.31f, 0.70f, 0.66f) * 2.3f),
                LampGlow = CreateOrLoadMaterial("MAT_StreetLamp_Glow", urpLit, new Color(1.0f, 0.88f, 0.62f), 0.2f, 0f, new Color(1.0f, 0.80f, 0.42f) * 3.0f),
                WindowLitWarm = CreateOrLoadMaterial("MAT_Window_LitWarm", urpLit, new Color(1.0f, 0.82f, 0.52f), 0.5f, 0f, new Color(1.0f, 0.74f, 0.36f) * 1.65f),
                RedBeaconGlow = CreateOrLoadMaterial("MAT_Harbor_RedBeacon", urpLit, new Color(1.0f, 0.18f, 0.14f), 0.2f, 0f, new Color(1.0f, 0.14f, 0.10f) * 3.2f),
                ReverseLightOff = CreateOrLoadMaterial("MAT_Sedan_ReverseLight", urpLit, new Color(0.86f, 0.86f, 0.84f), 0.6f, 0f, Color.black),
                CharSkin = CreateOrLoadMaterial("MAT_Char_Skin", urpLit, new Color(0.86f, 0.67f, 0.54f), 0.35f),
                CharShirt = CreateOrLoadMaterial("MAT_Char_Shirt", urpLit, new Color(0.16f, 0.27f, 0.42f), 0.20f),
                CharPants = CreateOrLoadMaterial("MAT_Char_Pants", urpLit, new Color(0.13f, 0.13f, 0.15f), 0.15f),
                CharShoes = CreateOrLoadMaterial("MAT_Char_Shoes", urpLit, new Color(0.08f, 0.07f, 0.06f), 0.45f),
                CharHair = CreateOrLoadMaterial("MAT_Char_Hair", urpLit, new Color(0.10f, 0.07f, 0.05f), 0.30f),
                TownPaving = CreatePbrMaterial("MAT_Town_Paving", urpLit, "PavingStones070", new Color(0.86f, 0.83f, 0.78f), 0.18f, 0f, Vector2.one, 0.85f),
                TownRoofTile = CreatePbrMaterial("MAT_Town_RoofTile", urpLit, "RoofingTiles006", new Color(0.88f, 0.56f, 0.44f), 0.22f, 0f, new Vector2(2f, 2f), 0.95f),
                TownBrickHouse = CreatePbrMaterial("MAT_Town_BrickHouse", urpLit, "Bricks059", new Color(0.86f, 0.74f, 0.64f), 0.16f, 0f, Vector2.one, 0.90f),
                TownGrass = CreatePbrMaterial("MAT_Town_Grass", urpLit, "Grass001", new Color(0.72f, 0.80f, 0.62f), 0.12f, 0f, new Vector2(28f, 52f), 0.85f),
                CountryEarth = CreatePbrMaterial("MAT_Country_Earth", urpLit, "Ground037", new Color(0.76f, 0.82f, 0.66f), 0.14f, 0f, new Vector2(36f, 48f), 0.90f),
                CoastalCliffRock = CreatePbrMaterial("MAT_Coastal_CliffRock", urpLit, "Rock030", new Color(0.74f, 0.72f, 0.68f), 0.18f, 0f, new Vector2(18f, 12f), 1.0f),
                TownAwningTerracotta = CreateOrLoadMaterial("MAT_Town_Awning_Terracotta", urpLit, new Color(0.74f, 0.30f, 0.20f), 0.16f),
                TownAwningGreen = CreateOrLoadMaterial("MAT_Town_Awning_Green", urpLit, new Color(0.16f, 0.42f, 0.25f), 0.16f),
                TownAwningNavy = CreateOrLoadMaterial("MAT_Town_Awning_Navy", urpLit, new Color(0.14f, 0.24f, 0.44f), 0.16f),
                TownAwningCream = CreateOrLoadMaterial("MAT_Town_Awning_Cream", urpLit, new Color(0.88f, 0.83f, 0.72f), 0.16f),
                TownBark = CreatePbrMaterial("MAT_Town_Bark", urpLit, "Rock030", new Color(0.42f, 0.30f, 0.20f), 0.12f, 0f, new Vector2(1f, 3f), 0.95f),
                TownFoliageA = CreatePbrMaterial("MAT_Town_Foliage_A", urpLit, "Grass001", new Color(0.46f, 0.62f, 0.38f), 0.14f, 0f, new Vector2(3f, 3f), 0.85f),
                TownFoliageB = CreatePbrMaterial("MAT_Town_Foliage_B", urpLit, "Grass001", new Color(0.56f, 0.68f, 0.42f), 0.14f, 0f, new Vector2(3f, 3f), 0.85f),
                TownHedge = CreatePbrMaterial("MAT_Town_Hedge", urpLit, "Grass001", new Color(0.42f, 0.58f, 0.36f), 0.12f, 0f, new Vector2(2f, 2f), 0.80f),
                TownWood = CreateOrLoadMaterial("MAT_Town_Wood", urpLit, new Color(0.42f, 0.26f, 0.14f), 0.24f),
                TownDoorDark = CreateOrLoadMaterial("MAT_Town_DoorDark", urpLit, new Color(0.24f, 0.15f, 0.09f), 0.26f),
                TrafficLightBulb = CreateOrLoadMaterial("MAT_TrafficLight_Bulb", urpLit, new Color(0.14f, 0.14f, 0.15f), 0.55f, 0f, Color.black),
                PedShirtRed = CreateOrLoadMaterial("MAT_Ped_Shirt_Red", urpLit, new Color(0.66f, 0.18f, 0.18f), 0.20f),
                PedShirtOlive = CreateOrLoadMaterial("MAT_Ped_Shirt_Olive", urpLit, new Color(0.30f, 0.40f, 0.22f), 0.20f),
                PedShirtMustard = CreateOrLoadMaterial("MAT_Ped_Shirt_Mustard", urpLit, new Color(0.76f, 0.58f, 0.18f), 0.20f),
                PedShirtTeal = CreateOrLoadMaterial("MAT_Ped_Shirt_Teal", urpLit, new Color(0.15f, 0.44f, 0.46f), 0.20f),
                PedShirtBurgundy = CreateOrLoadMaterial("MAT_Ped_Shirt_Burgundy", urpLit, new Color(0.44f, 0.14f, 0.24f), 0.20f),
                TownWoodFloor = CreateOrLoadMaterial("MAT_Town_WoodFloor", urpLit, new Color(0.45f, 0.30f, 0.18f), 0.35f),
                TownDoorWood = CreateOrLoadMaterial("MAT_Town_DoorWood", urpLit, new Color(0.28f, 0.17f, 0.10f), 0.32f),
                TownInteriorPaintWarm = CreatePbrMaterial("MAT_Town_InteriorPaint_WarmWhite", urpLit, "Plaster003", new Color(0.95f, 0.93f, 0.88f), 0.14f, 0f, Vector2.one, 0.35f),
                TownInteriorPaintCream = CreatePbrMaterial("MAT_Town_InteriorPaint_Cream", urpLit, "Plaster003", new Color(0.94f, 0.90f, 0.82f), 0.14f, 0f, Vector2.one, 0.35f),
                TownInteriorPaintPearl = CreatePbrMaterial("MAT_Town_InteriorPaint_Pearl", urpLit, "Plaster003", new Color(0.91f, 0.92f, 0.90f), 0.14f, 0f, Vector2.one, 0.35f),
                TownFabricGrey = CreateOrLoadMaterial("MAT_Town_Fabric_Grey", urpLit, new Color(0.42f, 0.43f, 0.46f), 0.12f),
                TownFabricTeal = CreateOrLoadMaterial("MAT_Town_Fabric_Teal", urpLit, new Color(0.18f, 0.44f, 0.46f), 0.12f),
                TownFabricMustard = CreateOrLoadMaterial("MAT_Town_Fabric_Mustard", urpLit, new Color(0.76f, 0.58f, 0.20f), 0.12f),
                TownFurnitureWood = CreateOrLoadMaterial("MAT_Town_FurnitureWood", urpLit, new Color(0.50f, 0.33f, 0.19f), 0.38f),
                TownPaintCan = CreateOrLoadMaterial("MAT_Town_PaintCan", urpLit, new Color(0.80f, 0.83f, 0.87f), 0.68f, 0.55f),
                CarRed = CreateOrLoadMaterial("MAT_Car_Red", urpLit, new Color(0.55f, 0.06f, 0.05f), 0.75f, 0.40f),
                CarWhite = CreateOrLoadMaterial("MAT_Car_White", urpLit, new Color(0.92f, 0.92f, 0.90f), 0.78f, 0.32f),
                CarDarkGreen = CreateOrLoadMaterial("MAT_Car_DarkGreen", urpLit, new Color(0.07f, 0.23f, 0.15f), 0.76f, 0.42f),
                BrakeCaliper = CreateOrLoadMaterial("MAT_Brake_Caliper", urpLit, new Color(0.76f, 0.11f, 0.09f), 0.65f, 0.35f),
                CanRed = CreateOrLoadMaterial("MAT_Can_Red", urpLit, new Color(0.78f, 0.14f, 0.14f), 0.72f, 0.55f),
                CanBlue = CreateOrLoadMaterial("MAT_Can_Blue", urpLit, new Color(0.14f, 0.38f, 0.76f), 0.72f, 0.55f),
                CanGreen = CreateOrLoadMaterial("MAT_Can_Green", urpLit, new Color(0.14f, 0.56f, 0.28f), 0.72f, 0.55f),
                CanOrange = CreateOrLoadMaterial("MAT_Can_Orange", urpLit, new Color(0.90f, 0.46f, 0.10f), 0.72f, 0.55f),
                CharUndershirt = CreateOrLoadMaterial("MAT_Char_Undershirt", urpLit, new Color(0.91f, 0.90f, 0.87f), 0.16f),
                CharBelt = CreateOrLoadMaterial("MAT_Char_Belt", urpLit, new Color(0.18f, 0.11f, 0.07f), 0.42f),
                CharShoeSole = CreateOrLoadMaterial("MAT_Char_ShoeSole", urpLit, new Color(0.92f, 0.91f, 0.88f), 0.24f),
                CharEyeWhite = CreateOrLoadMaterial("MAT_Char_EyeWhite", urpLit, new Color(0.96f, 0.96f, 0.95f), 0.65f),
                CharEyeIris = CreateOrLoadMaterial("MAT_Char_EyeIris", urpLit, new Color(0.08f, 0.07f, 0.06f), 0.82f),
                CharLips = CreateOrLoadMaterial("MAT_Char_Lips", urpLit, new Color(0.70f, 0.44f, 0.40f), 0.40f),
                CharSkinMedium = CreateOrLoadMaterial("MAT_Char_SkinMedium", urpLit, new Color(0.76f, 0.56f, 0.42f), 0.34f),
                CharSkinWarm = CreateOrLoadMaterial("MAT_Char_SkinWarm", urpLit, new Color(0.62f, 0.43f, 0.31f), 0.34f),
                CharHairBrown = CreateOrLoadMaterial("MAT_Char_HairBrown", urpLit, new Color(0.24f, 0.15f, 0.09f), 0.30f),
                CharHairBlonde = CreateOrLoadMaterial("MAT_Char_HairBlonde", urpLit, new Color(0.62f, 0.49f, 0.28f), 0.30f),
                CharPantsDenim = CreateOrLoadMaterial("MAT_Char_PantsDenim", urpLit, new Color(0.17f, 0.24f, 0.34f), 0.14f),
                CharPantsKhaki = CreateOrLoadMaterial("MAT_Char_PantsKhaki", urpLit, new Color(0.46f, 0.41f, 0.32f), 0.14f)
            };

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

        private enum VehicleBodyVariant
        {
            Sedan,
            Hatchback,
            Pickup
        }

        private static MaterialSet _activeMaterialSet;

        private class PrefabSet
        {
            public GameObject Player;
            public GameObject PlayerSedan;
            public GameObject TownCarRedHatchback;
            public GameObject TownCarWhitePickup;
            public GameObject TownCarGreenSedan;
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
            public GameObject WoodenBench;
        }

        private static PrefabSet CreatePrefabs(MaterialSet mats)
        {
            _activeMaterialSet = mats;
            return new PrefabSet
            {
                Player = BuildPlayerPrefab(mats),
                PlayerSedan = BuildSedanPrefab("Vehicle_PlayerSedan", "player_sedan", mats.SedanBody, InteractableKind.PlayerVehicle),
                TownCarRedHatchback = BuildVehicleVariantPrefab("Vehicle_TownHatchback_Red", string.Empty, mats.CarRed, InteractableKind.TownVehicle, VehicleBodyVariant.Hatchback, mats),
                TownCarWhitePickup = BuildVehicleVariantPrefab("Vehicle_TownPickup_White", string.Empty, mats.CarWhite, InteractableKind.TownVehicle, VehicleBodyVariant.Pickup, mats),
                TownCarGreenSedan = BuildSedanPrefab("Vehicle_TownSedan_Green", string.Empty, mats.CarDarkGreen, InteractableKind.TownVehicle),
                NpcMechanic = BuildNpcPrefab("NPC_Mechanic", "npc_mechanic", mats, mats.MechanicSuit, mats.CharPantsDenim, BlenderCharacterVariant.Worker, mats.CharSkinMedium, mats.CharHair),
                NpcHarborReceiver = BuildNpcPrefab("NPC_HarborReceiver", "npc_harbor_receiver", mats, mats.ReceiverSuit, mats.CharPants, BlenderCharacterVariant.CasualFemale, mats.CharSkin, mats.CharHairBrown),
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
                RollerShutterDoor = BuildFbxPropPrefab("rollershutter_door", "Prop_RollerShutter_CC0", 3.6f, addCollider: false, isStatic: true, fallbackMat: mats.GarageRoofMetal),
                WoodenBench = BuildFbxPropPrefab("painted_wooden_bench", "Prop_WoodenBench_CC0", 0.86f, addCollider: false, isStatic: true, fallbackMat: mats.TownWood)
            };
        }

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

                    float rawHeight = Mathf.Max(0.001f, combinedBounds.size.y);
                    float scaleFactor = targetHeightMeters / rawHeight;
                    instance.transform.localScale = Vector3.one * scaleFactor;

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

            GameObject bulbGo = null;
            if (addLampGlow && lampGlowMat != null)
            {
                bulbGo = CreatePrimitiveChild("Lantern_WarmBulb", PrimitiveType.Sphere, visualRoot.transform,
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

            if (bulbGo != null)
            {
                MarkStaticPaintable(bulbGo);
                NightLight nl = bulbGo.AddComponent<NightLight>();
                nl.Configure(null, new[] { bulbGo.GetComponent<Renderer>() }, new Color(1.0f, 0.80f, 0.42f) * 3.0f);
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

        private static GameObject InstantiateBlenderModel(
            string fbxFileName,
            string instanceName,
            Transform parent,
            MaterialSet mats,
            Material bodyPaintOverride = null,
            Material foliageAOverride = null,
            Material foliageBOverride = null)
        {
            string fbxPath = $"{BlenderModelsDir}/{fbxFileName}";
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null)
            {
                Debug.LogWarning($"[GRIDROAM] Blender FBX not found at '{fbxPath}' — falling back to procedural placeholder.");
                return null;
            }

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, parent);
            if (inst == null)
            {
                inst = Object.Instantiate(fbxAsset, parent);
            }
            else
            {
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            inst.name = instanceName;
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            // Strip any imported MeshColliders (we use clean fast primitive colliders for 30+ FPS mobile)
            foreach (Collider col in inst.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(col);
            }

            // Remap Blender material slots cleanly to our URP Lit PBR MaterialSet (zero Standard/pink materials!)
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                Material[] srcMats = r.sharedMaterials;
                Material[] mapped = new Material[srcMats.Length];
                for (int i = 0; i < srcMats.Length; i++)
                {
                    string slotName = srcMats[i] != null ? srcMats[i].name : string.Empty;
                    mapped[i] = ResolveBlenderUrpMaterial(slotName, mats, bodyPaintOverride, foliageAOverride, foliageBOverride);
                }
                r.sharedMaterials = mapped;
            }

            return inst;
        }

        private static Material ResolveBlenderUrpMaterial(
            string slotName,
            MaterialSet mats,
            Material bodyPaintOverride,
            Material foliageAOverride,
            Material foliageBOverride)
        {
            if (string.IsNullOrEmpty(slotName)) return mats.Concrete;

            if (slotName.StartsWith("BodyPaint")) return bodyPaintOverride != null ? bodyPaintOverride : mats.SedanBody;
            if (slotName.StartsWith("ContainerPaint")) return bodyPaintOverride != null ? bodyPaintOverride : mats.ContainerRed;
            if (slotName.StartsWith("TrimDark") || slotName.StartsWith("InteriorDark")) return mats.SedanTrimDark;
            if (slotName.StartsWith("Chrome")) return mats.SedanChrome;
            if (slotName.StartsWith("Glass")) return mats.SedanGlass;
            if (slotName.StartsWith("Headlight")) return mats.LampGlow;
            if (slotName.StartsWith("Taillight")) return mats.TaillightGlow;
            if (slotName.StartsWith("ReverseLight")) return mats.ReverseLightOff;
            if (slotName.StartsWith("WheelRubber")) return mats.WheelRubber;
            if (slotName.StartsWith("BrakeCaliper")) return mats.BrakeCaliper;
            if (slotName.StartsWith("Bark")) return mats.TownBark;
            if (slotName.StartsWith("FoliageA")) return foliageAOverride != null ? foliageAOverride : mats.TownFoliageA;
            if (slotName.StartsWith("FoliageB")) return foliageBOverride != null ? foliageBOverride : mats.TownFoliageB;
            if (slotName.StartsWith("StoneTrim")) return mats.PlasterTrim;
            if (slotName.StartsWith("Concrete")) return mats.Concrete;
            if (slotName.StartsWith("Water")) return mats.SeaWater;
            if (slotName.StartsWith("AccentTeal")) return mats.TownFabricTeal;
            if (slotName.StartsWith("AccentMustard")) return mats.TownFabricMustard;
            if (slotName.StartsWith("AccentBlue")) return mats.ContainerBlue;
            if (slotName.StartsWith("LitScreen")) return mats.WindowLitWarm;
            if (slotName.StartsWith("CanRed")) return mats.CanRed;
            if (slotName.StartsWith("CanBlue")) return mats.CanBlue;
            if (slotName.StartsWith("CanGreen")) return mats.CanGreen;
            if (slotName.StartsWith("CanOrange")) return mats.CanOrange;

            return mats.Concrete;
        }

        private static void CreateBlenderShippingContainer(
            string name,
            Transform parent,
            Vector3 basePos,
            Quaternion rot,
            Material containerMat,
            MaterialSet mats)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(basePos, rot);

            GameObject mesh = InstantiateBlenderModel("Shipping_Container.fbx", "ContainerMesh", root.transform, mats, bodyPaintOverride: containerMat);
            if (mesh == null)
            {
                CreateStaticBlock("FallbackBox", root.transform, basePos + Vector3.up * 1.5f, new Vector3(3.2f, 3.0f, 8.5f), containerMat, addCollider: false);
            }

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.5f, 0f);
            col.size = new Vector3(3.2f, 3.0f, 8.5f);
            MarkStaticEnvironment(root);
        }

        private static ProceduralHumanoid BuildBlenderHumanoid(
            Transform parent,
            MaterialSet mats,
            BlenderCharacterVariant variant,
            Material shirtMat,
            Material pantsMat = null,
            Material skinMat = null,
            Material hairMat = null,
            float height = 1.78f)
        {
            Material useSkin = skinMat != null ? skinMat : mats.CharSkin;
            Material useShirt = shirtMat != null ? shirtMat : mats.CharShirt;
            Material usePants = pantsMat != null ? pantsMat : mats.CharPants;
            Material useHair = hairMat != null ? hairMat : mats.CharHair;

            string fbxName = variant == BlenderCharacterVariant.CasualFemale
                ? "Character_CasualFemale.fbx"
                : (variant == BlenderCharacterVariant.Worker ? "Character_Worker.fbx" : "Character_CasualMale.fbx");

            string fbxPath = $"{BlenderModelsDir}/{fbxName}";
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null)
            {
                Debug.LogWarning($"[GRIDROAM] Character FBX not found at '{fbxPath}' — falling back to primitive humanoid.");
                return ProceduralHumanoid.Build(parent, useSkin, useShirt, usePants, mats.CharShoes, useHair, height);
            }

            // Cache child sub-part templates by exact name inside the flat FBX asset
            Dictionary<string, Transform> partTemplates = new Dictionary<string, Transform>();
            foreach (Transform child in fbxAsset.transform)
            {
                partTemplates[child.name] = child;
            }

            bool isFemale = variant == BlenderCharacterVariant.CasualFemale;
            float shldrX = isFemale ? 0.172f : 0.198f;
            float hipX = isFemale ? 0.098f : 0.102f;

            return ProceduralHumanoid.BuildWithCustomParts(
                parent,
                (partName, pivot) =>
                {
                    if (!partTemplates.TryGetValue(partName, out Transform srcPart) || srcPart == null)
                    {
                        return null;
                    }

                    GameObject partInst = Object.Instantiate(srcPart.gameObject, pivot, false);
                    partInst.name = partName;
                    partInst.transform.localPosition = Vector3.zero;
                    partInst.transform.localRotation = Quaternion.identity;
                    partInst.transform.localScale = Vector3.one;

                    foreach (Collider c in partInst.GetComponentsInChildren<Collider>(true))
                    {
                        Object.DestroyImmediate(c);
                    }

                    foreach (Renderer r in partInst.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] srcMats = r.sharedMaterials;
                        Material[] mapped = new Material[srcMats.Length];
                        for (int i = 0; i < srcMats.Length; i++)
                        {
                            string slot = srcMats[i] != null ? srcMats[i].name : string.Empty;
                            mapped[i] = ResolveCharacterUrpMaterial(slot, mats, useSkin, useShirt, usePants, useHair);
                        }
                        r.sharedMaterials = mapped;
                    }

                    return partInst.transform;
                },
                height: height,
                shoulderX: shldrX,
                hipX: hipX,
                shoulderY: 0.382f);
        }

        private static Material ResolveCharacterUrpMaterial(
            string slot,
            MaterialSet mats,
            Material skin,
            Material shirt,
            Material pants,
            Material hair)
        {
            if (slot.StartsWith("CharSkin")) return skin;
            if (slot.StartsWith("CharShirt")) return shirt;
            if (slot.StartsWith("CharUndershirt")) return mats.CharUndershirt;
            if (slot.StartsWith("CharPants")) return pants;
            if (slot.StartsWith("CharBelt")) return mats.CharBelt;
            if (slot.StartsWith("CharBuckle")) return mats.SedanChrome;
            if (slot.StartsWith("CharShoeSole")) return mats.CharShoeSole;
            if (slot.StartsWith("CharShoes")) return mats.CharShoes;
            if (slot.StartsWith("CharHair")) return hair;
            if (slot.StartsWith("CharEyeWhite")) return mats.CharEyeWhite;
            if (slot.StartsWith("CharEyeIris")) return mats.CharEyeIris;
            if (slot.StartsWith("CharLips")) return mats.CharLips;
            return shirt;
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

            ProceduralHumanoid humanoid = BuildBlenderHumanoid(
                visualRoot.transform,
                mats,
                BlenderCharacterVariant.CasualMale,
                mats.CharShirt,
                mats.CharPantsDenim,
                mats.CharSkin,
                mats.CharHair,
                1.78f);

            foot.ConfigureVisuals(visualRoot.transform, null, humanoid);
            return SaveAsPrefab(root, $"{PrefabDir}/Player_GRIDROAM.prefab");
        }

        private static GameObject BuildSedanPrefab(
            string prefabName,
            string anchorId,
            Material bodyMat,
            InteractableKind kind)
        {
            return BuildVehicleVariantPrefab(prefabName, anchorId, bodyMat, kind, VehicleBodyVariant.Sedan, _activeMaterialSet);
        }

        private static GameObject BuildVehicleVariantPrefab(
            string prefabName,
            string anchorId,
            Material bodyMat,
            InteractableKind kind,
            VehicleBodyVariant variant,
            MaterialSet mats)
        {
            GameObject root = new GameObject(prefabName);

            bool isHatchback = variant == VehicleBodyVariant.Hatchback;
            bool isPickup = variant == VehicleBodyVariant.Pickup;

            float massKg = isHatchback ? 1150f : (isPickup ? 1800f : 1350f);
            float bodyLength = isHatchback ? 3.90f : (isPickup ? 5.00f : 4.42f);
            float bodyWidth = isHatchback ? 1.80f : (isPickup ? 1.94f : 1.88f);
            float halfLen = bodyLength * 0.5f;
            float halfWid = bodyWidth * 0.5f;

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = massKg;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.25f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (!string.IsNullOrEmpty(anchorId))
            {
                WorldAnchor anchor = root.AddComponent<WorldAnchor>();
                anchor.Configure(anchorId, 3.8f, new Vector3(0f, 2.3f, 0f));
            }

            string triggerId = !string.IsNullOrEmpty(anchorId) ? anchorId : prefabName.ToLowerInvariant();
            string promptKey = kind == InteractableKind.PlayerVehicle ? "ui.btn.enter_car" : "ui.btn.drive";
            InteractableTrigger interactable = root.AddComponent<InteractableTrigger>();
            interactable.Configure(kind, triggerId, promptKey, 3.8f);

            GameObject chassisColObj = new GameObject("ChassisCollider");
            chassisColObj.transform.SetParent(root.transform, false);
            BoxCollider chassisBox = chassisColObj.AddComponent<BoxCollider>();
            chassisBox.center = new Vector3(0f, isPickup ? 0.78f : 0.72f, 0f);
            chassisBox.size = new Vector3(bodyWidth + 0.02f, isPickup ? 1.05f : 0.95f, bodyLength + 0.04f);

            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            float frontAxleZ = isHatchback ? 1.22f : (isPickup ? 1.56f : 1.38f);
            float rearAxleZ = isHatchback ? -1.22f : (isPickup ? -1.56f : -1.32f);
            float wheelX = isHatchback ? 0.80f : (isPickup ? 0.87f : 0.84f);
            float frontZ = halfLen + 0.02f;

            string carFbx = isPickup ? "Car_Pickup.fbx" : (isHatchback ? "Car_Hatchback.fbx" : "Car_Sedan.fbx");
            GameObject blenderCar = InstantiateBlenderModel(carFbx, "BlenderCarMesh", visualRoot.transform, mats, bodyPaintOverride: bodyMat);
            if (blenderCar == null)
            {
                // Fallback primitive body if FBX hasn't imported yet
                CreatePrimitiveChild("Placeholder_LowerBody", PrimitiveType.Cube, visualRoot.transform,
                    new Vector3(0f, 0.62f, 0f), new Vector3(bodyWidth, 0.56f, bodyLength), bodyMat, removeCollider: true);
                CreatePrimitiveChild("Placeholder_CabinRoof", PrimitiveType.Cube, visualRoot.transform,
                    new Vector3(0f, 1.28f, -0.20f), new Vector3(bodyWidth * 0.82f, 0.42f, bodyLength * 0.42f), mats.SedanGlass, removeCollider: true);
            }

            GameObject steeringWheel = new GameObject("SteeringWheel");
            steeringWheel.transform.SetParent(visualRoot.transform, false);
            steeringWheel.transform.localPosition = new Vector3(-0.38f, isPickup ? 1.08f : 1.0f, isPickup ? 0.72f : 0.35f);
            steeringWheel.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            CreatePrimitiveChild("SteeringWheelRim", PrimitiveType.Cylinder, steeringWheel.transform,
                Vector3.zero, new Vector3(0.34f, 0.025f, 0.34f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("SteeringWheelSpoke", PrimitiveType.Cube, steeringWheel.transform,
                Vector3.zero, new Vector3(0.30f, 0.03f, 0.045f), mats.SedanChrome, removeCollider: true);

            GameObject seatedDriverRoot = new GameObject("SeatedDriverRoot");
            seatedDriverRoot.transform.SetParent(visualRoot.transform, false);
            seatedDriverRoot.transform.localPosition = new Vector3(-0.38f, isPickup ? 0.52f : 0.45f, isPickup ? 0.12f : -0.25f);
            ProceduralHumanoid seatedDriver = BuildBlenderHumanoid(
                seatedDriverRoot.transform,
                mats,
                BlenderCharacterVariant.CasualMale,
                mats.CharShirt,
                mats.CharPantsDenim,
                mats.CharSkin,
                mats.CharHair,
                1.78f);
            seatedDriver.SetSitting(true, driving: true);
            seatedDriverRoot.SetActive(false);

            GameObject beamObj = new GameObject("Headlight_Beam");
            beamObj.transform.SetParent(visualRoot.transform, false);
            beamObj.transform.localPosition = new Vector3(0f, 0.76f, frontZ + 0.04f);
            beamObj.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
            Light headlight = beamObj.AddComponent<Light>();
            headlight.type = LightType.Spot;
            headlight.color = new Color(1.0f, 0.94f, 0.80f);
            headlight.range = 34f;
            headlight.spotAngle = 56f;
            headlight.intensity = 4.2f;
            headlight.shadows = LightShadows.None;
            headlight.enabled = false;

            GameObject wcParent = new GameObject("WheelColliders");
            wcParent.transform.SetParent(root.transform, false);

            GameObject wvParent = new GameObject("WheelVisuals");
            wvParent.transform.SetParent(root.transform, false);

            float wheelRadius = isPickup ? 0.37f : (isHatchback ? 0.32f : 0.34f);
            float suspDist = isPickup ? 0.28f : (isHatchback ? 0.21f : 0.22f);
            float springRate = isPickup ? 25000f : (isHatchback ? 32000f : 35000f);
            float damperRate = isPickup ? 3300f : (isHatchback ? 4200f : 4500f);
            float wheelY = isPickup ? 0.44f : 0.40f;

            WheelCollider fl = CreateWheelCollider("WheelFL", wcParent.transform, new Vector3(-wheelX, wheelY, frontAxleZ), wheelRadius, suspDist, springRate, damperRate);
            WheelCollider fr = CreateWheelCollider("WheelFR", wcParent.transform, new Vector3(wheelX, wheelY, frontAxleZ), wheelRadius, suspDist, springRate, damperRate);
            WheelCollider rl = CreateWheelCollider("WheelRL", wcParent.transform, new Vector3(-wheelX, wheelY, rearAxleZ), wheelRadius, suspDist, springRate, damperRate);
            WheelCollider rr = CreateWheelCollider("WheelRR", wcParent.transform, new Vector3(wheelX, wheelY, rearAxleZ), wheelRadius, suspDist, springRate, damperRate);

            Transform vfl = CreateWheelVisual("WheelVisual_FL", wvParent.transform, fl.transform.localPosition, mats, wheelRadius);
            Transform vfr = CreateWheelVisual("WheelVisual_FR", wvParent.transform, fr.transform.localPosition, mats, wheelRadius);
            Transform vrl = CreateWheelVisual("WheelVisual_RL", wvParent.transform, rl.transform.localPosition, mats, wheelRadius);
            Transform vrr = CreateWheelVisual("WheelVisual_RR", wvParent.transform, rr.transform.localPosition, mats, wheelRadius);

            GameObject exitL = new GameObject("ExitLeft");
            exitL.transform.SetParent(root.transform, false);
            exitL.transform.localPosition = new Vector3(-1.90f, 0.08f, 0.1f);

            GameObject exitR = new GameObject("ExitRight");
            exitR.transform.SetParent(root.transform, false);
            exitR.transform.localPosition = new Vector3(1.90f, 0.08f, 0.1f);

            bool frontMotor = isHatchback;
            bool rearMotor = !isHatchback;

            SedanVehicleController controller = root.AddComponent<SedanVehicleController>();
            List<AxleInfo> axles = new List<AxleInfo>
            {
                new AxleInfo { leftWheel = fl, rightWheel = fr, leftWheelVisual = vfl, rightWheelVisual = vfr, motor = frontMotor, steering = true },
                new AxleInfo { leftWheel = rl, rightWheel = rr, leftWheelVisual = vrl, rightWheelVisual = vrr, motor = rearMotor, steering = false }
            };
            controller.ConfigureSetup(axles, visualRoot.transform, exitL.transform, exitR.transform, seatedDriverRoot, steeringWheel.transform);
            controller.ConfigureTuning(
                wheelbaseMeters: frontAxleZ - rearAxleZ,
                trackWidthMeters: wheelX * 2f,
                antiRoll: isPickup ? 3000f : 4200f,
                comOffset: isPickup ? new Vector3(0f, -0.38f, 0.12f) : new Vector3(0f, -0.42f, 0.08f),
                maxBrake: isPickup ? 3600f : 3200f,
                finalDrive: isHatchback ? 4.1f : 3.9f);

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

            return SaveAsPrefab(root, $"{PrefabDir}/{prefabName}.prefab");
        }

        private static WheelCollider CreateWheelCollider(
            string wheelName,
            Transform parent,
            Vector3 localPos,
            float radius = 0.34f,
            float suspDist = 0.22f,
            float springRate = 35000f,
            float damperRate = 4500f)
        {
            GameObject go = new GameObject(wheelName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            WheelCollider wc = go.AddComponent<WheelCollider>();
            wc.mass = 20f;
            wc.radius = radius;
            wc.wheelDampingRate = 0.25f;
            wc.suspensionDistance = suspDist;
            wc.forceAppPointDistance = 0.1f;

            JointSpring spring = wc.suspensionSpring;
            spring.spring = springRate;
            spring.damper = damperRate;
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

        private static Transform CreateWheelVisual(string visualName, Transform parent, Vector3 localPos, MaterialSet mats, float wheelRadius = 0.34f)
        {
            GameObject pivot = new GameObject(visualName);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = localPos;

            GameObject blenderWheel = InstantiateBlenderModel("Car_Wheel.fbx", "BlenderWheelMesh", pivot.transform, mats);
            if (blenderWheel != null)
            {
                float s = wheelRadius / 0.34f;
                blenderWheel.transform.localScale = Vector3.one * s;
            }
            else
            {
                GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.name = "Placeholder_TireMesh";
                cyl.transform.SetParent(pivot.transform, false);
                cyl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                cyl.transform.localScale = new Vector3(wheelRadius * 2f, 0.14f, wheelRadius * 2f);
                Object.DestroyImmediate(cyl.GetComponent<Collider>());
                cyl.GetComponent<Renderer>().sharedMaterial = mats.WheelRubber;
            }

            return pivot.transform;
        }

        private static GameObject BuildNpcPrefab(
            string prefabName,
            string anchorId,
            MaterialSet mats,
            Material shirtMat,
            Material pantsMat,
            BlenderCharacterVariant variant = BlenderCharacterVariant.CasualMale,
            Material skinMat = null,
            Material hairMat = null)
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

            ProceduralHumanoid humanoid = BuildBlenderHumanoid(
                visualRoot.transform,
                mats,
                variant,
                shirtMat,
                pantsMat,
                skinMat,
                hairMat,
                variant == BlenderCharacterVariant.CasualFemale ? 1.72f : 1.78f);
            humanoid.SetAutoRandomWave(true);

            return SaveAsPrefab(root, $"{PrefabDir}/{prefabName}.prefab");
        }

        private static GameObject BuildApartmentPrefab(MaterialSet mats)
        {
            GameObject root = new GameObject("Building_Apartment_Placeholder");
            MarkStaticEnvironment(root);

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 7.0f, 0f);
            col.size = new Vector3(14f, 14f, 14f);

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
            col.size = new Vector3(1.88f, 1.38f, 4.42f);

            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            List<Renderer> headlightRenderers = new List<Renderer>();
            GameObject blenderCar = InstantiateBlenderModel("Car_Sedan.fbx", "BlenderTrafficSedan", visualRoot.transform, mats, bodyPaintOverride: mats.TrafficBody);
            if (blenderCar != null)
            {
                // Add all 4 sculpted Blender alloy wheels
                CreateWheelVisual("TrafficWheel_FL", visualRoot.transform, new Vector3(-0.84f, 0.34f, 1.38f), mats, 0.34f);
                CreateWheelVisual("TrafficWheel_FR", visualRoot.transform, new Vector3(0.84f, 0.34f, 1.38f), mats, 0.34f);
                CreateWheelVisual("TrafficWheel_RL", visualRoot.transform, new Vector3(-0.84f, 0.34f, -1.32f), mats, 0.34f);
                CreateWheelVisual("TrafficWheel_RR", visualRoot.transform, new Vector3(0.84f, 0.34f, -1.32f), mats, 0.34f);

                foreach (Renderer r in blenderCar.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.gameObject.name == "Car_Headlights")
                    {
                        headlightRenderers.Add(r);
                    }
                }
            }
            else
            {
                CreatePrimitiveChild("Body", PrimitiveType.Cube, visualRoot.transform,
                    new Vector3(0f, 0.58f, 0f), new Vector3(1.82f, 0.56f, 4.10f), mats.TrafficBody, removeCollider: true);
                GameObject headL = CreatePrimitiveChild("HeadL", PrimitiveType.Cube, visualRoot.transform,
                    new Vector3(-0.62f, 0.62f, 2.06f), new Vector3(0.32f, 0.14f, 0.06f), mats.LampGlow, removeCollider: true);
                headlightRenderers.Add(headL.GetComponent<Renderer>());
            }

            // E2: Traffic vehicle headlights glow at night (20:00-06:00) and turn off during day
            NightLight trafficHeadlights = root.AddComponent<NightLight>();
            trafficHeadlights.Configure(
                null,
                headlightRenderers.ToArray(),
                new Color(1.0f, 0.88f, 0.62f) * 2.8f);

            return SaveAsPrefab(root, $"{PrefabDir}/Vehicle_Traffic_Placeholder.prefab");
        }

        private static MissionCatalog CreateMissionDataAssets()
        {
            // Dialogue 1: Marco hands over the sedan keys
            DialogueData dlgM1 = CreateOrLoadAsset<DialogueData>($"{DialogueDir}/DLG_M01_Mechanic.asset");
            dlgM1.lines = new List<DialogueLine>
            {
                new DialogueLine { speakerKey = "npc.mechanic.name", textKey = "m01.dlg.1" },
                new DialogueLine { speakerKey = "npc.mechanic.name", textKey = "m01.dlg.2" },
                new DialogueLine { speakerKey = "npc.mechanic.name", textKey = "m01.dlg.3" }
            };
            dlgM1.choices = new List<DialogueChoice>();
            EditorUtility.SetDirty(dlgM1);

            // Dialogue 2: Elena accepts the delivery and mentions the missing dark sedan
            DialogueData dlgM2 = CreateOrLoadAsset<DialogueData>($"{DialogueDir}/DLG_M02_HarborReceiver.asset");
            dlgM2.lines = new List<DialogueLine>
            {
                new DialogueLine { speakerKey = "npc.harbor_receiver.name", textKey = "m02.dlg.1" },
                new DialogueLine { speakerKey = "npc.harbor_receiver.name", textKey = "m02.dlg.2" },
                new DialogueLine { speakerKey = "npc.harbor_receiver.name", textKey = "m02.dlg.3" }
            };
            dlgM2.choices = new List<DialogueChoice>();
            EditorUtility.SetDirty(dlgM2);

            // Dialogue 3: Inspecting the missing car reveals the harbor courier manifest
            DialogueData dlgM3 = CreateOrLoadAsset<DialogueData>($"{DialogueDir}/DLG_M03_MissingCarClue.asset");
            dlgM3.lines = new List<DialogueLine>
            {
                new DialogueLine { speakerKey = "npc.player.name", textKey = "m03.dlg.1" },
                new DialogueLine { speakerKey = "npc.player.name", textKey = "m03.dlg.2" },
                new DialogueLine { speakerKey = "npc.player.name", textKey = "m03.dlg.3" }
            };
            dlgM3.choices = new List<DialogueChoice>();
            EditorUtility.SetDirty(dlgM3);

            // Dialogue 4: Collecting the dropped courier package in the north back alley
            DialogueData dlgM4 = CreateOrLoadAsset<DialogueData>($"{DialogueDir}/DLG_M04_PackagePickup.asset");
            dlgM4.lines = new List<DialogueLine>
            {
                new DialogueLine { speakerKey = "npc.player.name", textKey = "m04.dlg.1" },
                new DialogueLine { speakerKey = "npc.player.name", textKey = "m04.dlg.2" }
            };
            dlgM4.choices = new List<DialogueChoice>();
            EditorUtility.SetDirty(dlgM4);

            // Dialogue 5A: Ending if the player delivers the package to Marco at the Repair Garage
            DialogueData dlgM5Marco = CreateOrLoadAsset<DialogueData>($"{DialogueDir}/DLG_M05_Ending_Marco.asset");
            dlgM5Marco.lines = new List<DialogueLine>
            {
                new DialogueLine { speakerKey = "npc.mechanic.name", textKey = "m05.marco.dlg.1" },
                new DialogueLine { speakerKey = "npc.mechanic.name", textKey = "m05.marco.dlg.2" },
                new DialogueLine { speakerKey = "npc.mechanic.name", textKey = "m05.marco.dlg.3" }
            };
            dlgM5Marco.choices = new List<DialogueChoice>();
            EditorUtility.SetDirty(dlgM5Marco);

            // Dialogue 5B: Ending if the player delivers the package to Elena at the Town Square
            DialogueData dlgM5Elena = CreateOrLoadAsset<DialogueData>($"{DialogueDir}/DLG_M05_Ending_Elena.asset");
            dlgM5Elena.lines = new List<DialogueLine>
            {
                new DialogueLine { speakerKey = "npc.harbor_receiver.name", textKey = "m05.elena.dlg.1" },
                new DialogueLine { speakerKey = "npc.harbor_receiver.name", textKey = "m05.elena.dlg.2" },
                new DialogueLine { speakerKey = "npc.harbor_receiver.name", textKey = "m05.elena.dlg.3" }
            };
            dlgM5Elena.choices = new List<DialogueChoice>();
            EditorUtility.SetDirty(dlgM5Elena);

            // Mission 1: Get the Keys
            MissionData m01 = CreateOrLoadAsset<MissionData>($"{MissionDir}/Mission_01_GetTheKeys.asset");
            m01.missionId = "m01_get_the_keys";
            m01.titleKey = "m01.title";
            m01.summaryKey = "m01.summary";
            m01.rewardMoney = 100;
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

            // Mission 2: Last Delivery
            MissionData m02 = CreateOrLoadAsset<MissionData>($"{MissionDir}/Mission_02_LastDelivery.asset");
            m02.missionId = "m02_last_delivery";
            m02.titleKey = "m02.title";
            m02.summaryKey = "m02.summary";
            m02.rewardMoney = 150;
            m02.nextMissionId = "m03_missing_car";
            m02.autoStartNextMission = true;
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

            // Mission 3: Missing Car
            MissionData m03 = CreateOrLoadAsset<MissionData>($"{MissionDir}/Mission_03_MissingCar.asset");
            m03.missionId = "m03_missing_car";
            m03.titleKey = "m03.title";
            m03.summaryKey = "m03.summary";
            m03.rewardMoney = 150;
            m03.nextMissionId = "m04_night_tail";
            m03.autoStartNextMission = true;
            m03.retryPlayerAnchorId = "spawn_garage_foot";
            m03.retryVehicleAnchorId = "spawn_garage_sedan";
            m03.retryStartInVehicle = false;
            m03.steps = new List<MissionStepData>
            {
                new MissionStepData
                {
                    stepType = MissionStepType.ReachLocation,
                    objectiveKey = "m03.step1.objective",
                    targetAnchorId = "missing_car_zone",
                    triggerRadius = 14f
                },
                new MissionStepData
                {
                    stepType = MissionStepType.InspectTarget,
                    objectiveKey = "m03.step2.objective",
                    targetAnchorId = "missing_car_clue",
                    dialogueAsset = dlgM3,
                    requiresOnFoot = true
                }
            };
            EditorUtility.SetDirty(m03);

            // Mission 4: Night Tail
            MissionData m04 = CreateOrLoadAsset<MissionData>($"{MissionDir}/Mission_04_NightTail.asset");
            m04.missionId = "m04_night_tail";
            m04.titleKey = "m04.title";
            m04.summaryKey = "m04.summary";
            m04.rewardMoney = 200;
            m04.nextMissionId = "m05_end_of_shift";
            m04.autoStartNextMission = true;
            m04.retryPlayerAnchorId = "spawn_harbor_tail_start";
            m04.retryVehicleAnchorId = "spawn_harbor_tail_start";
            m04.retryStartInVehicle = true;
            m04.steps = new List<MissionStepData>
            {
                new MissionStepData
                {
                    stepType = MissionStepType.ReachLocation,
                    objectiveKey = "m04.step1.objective",
                    targetAnchorId = "tail_start_zone",
                    triggerRadius = 11f
                },
                new MissionStepData
                {
                    stepType = MissionStepType.TailVehicle,
                    objectiveKey = "m04.step2.objective",
                    targetAnchorId = "tail_target_car",
                    tailMinDistance = 11f,
                    tailMaxDistance = 76f,
                    tailGraceSeconds = 6.5f
                },
                new MissionStepData
                {
                    stepType = MissionStepType.InspectTarget,
                    objectiveKey = "m04.step3.objective",
                    targetAnchorId = "tail_dropped_package",
                    dialogueAsset = dlgM4,
                    requiresOnFoot = true
                }
            };
            EditorUtility.SetDirty(m04);

            // Mission 5: End of Shift (Two routes back to neighborhood + choice between Marco and Elena)
            MissionData m05 = CreateOrLoadAsset<MissionData>($"{MissionDir}/Mission_05_EndOfShift.asset");
            m05.missionId = "m05_end_of_shift";
            m05.titleKey = "m05.title";
            m05.summaryKey = "m05.summary";
            m05.rewardMoney = 300;
            m05.nextMissionId = string.Empty;
            m05.autoStartNextMission = false;
            m05.retryPlayerAnchorId = "spawn_garage_foot";
            m05.retryVehicleAnchorId = "spawn_garage_sedan";
            m05.retryStartInVehicle = false;
            m05.steps = new List<MissionStepData>
            {
                new MissionStepData
                {
                    stepType = MissionStepType.ReachLocation,
                    objectiveKey = "m05.step1.objective",
                    targetAnchorId = "neighborhood_return_zone",
                    triggerRadius = 15f
                },
                new MissionStepData
                {
                    stepType = MissionStepType.TalkToNpc,
                    objectiveKey = "m05.step2.objective",
                    targetAnchorId = "npc_mechanic",
                    dialogueAsset = dlgM5Marco,
                    primaryChoiceId = "marco",
                    secondaryTargetAnchorId = "npc_elena_neighborhood",
                    secondaryDialogueAsset = dlgM5Elena,
                    secondaryChoiceId = "elena"
                }
            };
            EditorUtility.SetDirty(m05);

            MissionCatalog catalog = CreateOrLoadAsset<MissionCatalog>($"{MissionDir}/MissionCatalog.asset");
            catalog.missions = new List<MissionData> { m01, m02, m03, m04, m05 };
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void BuildTestScene(
            MaterialSet mats,
            PrefabSet prefabs,
            MissionCatalog catalog,
            UniversalRenderPipelineAsset lowUrp,
            UniversalRenderPipelineAsset medUrp,
            VolumeProfile eveningVolume,
            GeneratedUiSprites uiSprites)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Morning 09:00 Default Coastal Skybox & Sunlight (managed dynamically by DayNightCycle) ---
            RenderSettings.skybox = mats.EveningSkybox;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.12f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1.0f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.62f, 0.70f, 0.78f);
            RenderSettings.fogDensity = 0.0016f;

            GameObject sunObj = new GameObject("Sun_CoastalDirectional");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.94f, 0.84f);
            sun.intensity = 1.85f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sunObj.transform.rotation = Quaternion.Euler(34f, -38f, 0f);
            RenderSettings.sun = sun;

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
            systems.AddComponent<DayNightCycle>();

            GraphicsProfileManager gfxMgr = systems.AddComponent<GraphicsProfileManager>();
            gfxMgr.ConfigureAssets(lowUrp, medUrp);

            PlayerNeedsController needsController = systems.AddComponent<PlayerNeedsController>();
            PlayerModeController modeController = systems.AddComponent<PlayerModeController>();
            MissionManager missionManager = systems.AddComponent<MissionManager>();
            JobManager jobManager = systems.AddComponent<JobManager>();

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

            // --- World Environment (Single 900m x 900m solid meadow foundation — zero fake sea, zero fake mountains, impossible to fall!) ---
            GameObject env = new GameObject("--- WORLD_ENVIRONMENT ---");

            CreateTiledBox(
                "Ground_SolidMeadowFoundation",
                env.transform,
                new Vector3(0f, -3.0f, 80f),
                new Vector3(900f, 6.0f, 900f),
                Quaternion.identity,
                mats.TownGrass,
                metersPerTile: 3.5f,
                collider: true);

            // --- REPAIR GARAGE (Tamirhane, south of Güney Yolu z = -24, facing North +Z onto the street) ---
            GameObject garageRoot = new GameObject("Zone_RepairGarage");
            garageRoot.transform.SetParent(env.transform, false);
            CreateTiledBox("Garage_Pad", garageRoot.transform, new Vector3(1.0f, 0.014f, -35.0f), new Vector3(30f, 0.028f, 14f), Quaternion.identity, mats.HarborDock, 3.0f, collider: false);
            CreateStaticBlock("Garage_BackWall", garageRoot.transform, new Vector3(-3.0f, 3.0f, -41.0f), new Vector3(15f, 6f, 1f), mats.GarageBrick);
            CreateStaticBlock("Garage_SideWallL", garageRoot.transform, new Vector3(-10.0f, 3.0f, -36.0f), new Vector3(1f, 6f, 11f), mats.GarageBrick);
            CreateStaticBlock("Garage_SideWallR", garageRoot.transform, new Vector3(4.0f, 3.0f, -38.0f), new Vector3(1f, 6f, 7f), mats.GarageBrick);
            CreateStaticBlock("Garage_Roof", garageRoot.transform, new Vector3(-3.0f, 6.2f, -36.0f), new Vector3(16.0f, 0.6f, 12.0f), mats.GarageRoofMetal);

            GameObject garageFascia = CreateStaticBlock("Garage_SignFascia", garageRoot.transform, new Vector3(-3.0f, 6.5f, -30.1f), new Vector3(12f, 0.8f, 0.35f), mats.SedanTrimDark, addCollider: false);
            CreateWorldSignText(
                "TAMİRHANE",
                garageRoot.transform,
                new Vector3(-3.0f, 6.5f, -29.88f),
                Quaternion.Euler(0f, 180f, 0f),
                new Vector2(10.5f, 0.65f),
                new Color(1.0f, 0.86f, 0.48f, 1f));
            MarkStaticPaintable(garageFascia);

            PlacePrefabInstance(prefabs.RollerShutterDoor, garageRoot.transform, new Vector3(-3.0f, 0.03f, -40.3f), Quaternion.identity);
            PlacePrefabInstance(prefabs.ToolCart, garageRoot.transform, new Vector3(-6.8f, 0.03f, -35.5f), Quaternion.Euler(0f, 25f, 0f));
            PlacePrefabInstance(prefabs.OilBarrel, garageRoot.transform, new Vector3(-8.6f, 0.03f, -38.8f), Quaternion.Euler(0f, 20f, 0f));
            PlacePrefabInstance(prefabs.OilBarrel, garageRoot.transform, new Vector3(-7.6f, 0.03f, -39.2f), Quaternion.Euler(0f, 115f, 0f));
            PlacePrefabInstance(prefabs.OldTyre, garageRoot.transform, new Vector3(-8.8f, 0.03f, -32.2f), Quaternion.identity);
            PlacePrefabInstance(prefabs.OldTyre, garageRoot.transform, new Vector3(-8.8f, 0.31f, -32.2f), Quaternion.Euler(0f, 40f, 0f));
            PlacePrefabInstance(prefabs.WoodenCrate, garageRoot.transform, new Vector3(2.8f, 0.03f, -39.2f), Quaternion.Euler(0f, 12f, 0f));
            PlacePrefabInstance(prefabs.FloodLights, garageRoot.transform, new Vector3(4.6f, 0.03f, -34.0f), Quaternion.Euler(0f, -45f, 0f));
            CreatePointLamp("Lamp_GarageBay", garageRoot.transform, new Vector3(-3.0f, 5.2f, -35.0f), new Color(1f, 0.78f, 0.45f), 22f, 3.4f);

            // --- CLEAN WALKABLE SMALL TOWN ("Çınarlı Kasabası") & WOODEN COUNTRY FENCE ---
            List<TrafficStopLine> stopLines = BuildSmallTown(env.transform, mats, prefabs);
            BuildWorldBoundary(env.transform, mats);

            // Street Lamps cleanly along Cumhuriyet Caddesi (x = +30.8) & Çınar Caddesi (x = -30.8)
            float[] lampZs = { -8f, 26f, 62f, 98f, 136f, 170f };
            for (int i = 0; i < lampZs.Length; i++)
            {
                float lz = lampZs[i];
                PlacePrefabInstance(prefabs.StreetLamp, env.transform, new Vector3(30.8f, 0.02f, lz), Quaternion.Euler(0f, -90f, 0f));
                CreatePointLamp($"StreetLight_Main_{lz:0}", env.transform, new Vector3(29.4f, 5.2f, lz), new Color(1.0f, 0.78f, 0.44f), 20f, 2.2f);

                if (i % 2 == 0)
                {
                    PlacePrefabInstance(prefabs.StreetLamp, env.transform, new Vector3(-30.8f, 0.02f, lz), Quaternion.Euler(0f, 90f, 0f));
                    CreatePointLamp($"StreetLight_West_{lz:0}", env.transform, new Vector3(-29.4f, 5.2f, lz), new Color(1.0f, 0.76f, 0.42f), 18f, 1.9f);
                }
            }

            // --- NORTH CARGO DEPOT (Kuzey Kargo Deposu, north of Kuzey Yolu z = +184, facing South -Z onto the street) ---
            GameObject depotRoot = new GameObject("Zone_CargoDepot");
            depotRoot.transform.SetParent(env.transform, false);
            CreateTiledBox("Depot_ConcreteYard", depotRoot.transform, new Vector3(5.0f, 0.014f, 196.0f), new Vector3(52f, 0.028f, 15f), Quaternion.identity, mats.HarborDock, 3.0f, collider: false);

            // Flush yellow delivery bay lines centered at (10.5, 0.03, 193.5)
            CreateStaticBlock("DeliveryBay_North", depotRoot.transform, new Vector3(10.5f, 0.03f, 197.0f), new Vector3(9.5f, 0.015f, 0.3f), mats.RoadMarkingYellow, addCollider: false);
            CreateStaticBlock("DeliveryBay_South", depotRoot.transform, new Vector3(10.5f, 0.03f, 190.0f), new Vector3(9.5f, 0.015f, 0.3f), mats.RoadMarkingYellow, addCollider: false);
            CreateStaticBlock("DeliveryBay_West", depotRoot.transform, new Vector3(5.75f, 0.03f, 193.5f), new Vector3(0.3f, 0.015f, 7.3f), mats.RoadMarkingYellow, addCollider: false);
            CreateStaticBlock("DeliveryBay_East", depotRoot.transform, new Vector3(15.25f, 0.03f, 193.5f), new Vector3(0.3f, 0.015f, 7.3f), mats.RoadMarkingYellow, addCollider: false);

            CreateStaticBlock("Depot_Warehouse", depotRoot.transform, new Vector3(-10.5f, 3.8f, 198.5f), new Vector3(15f, 7.6f, 8.0f), mats.GarageBrick);
            CreateStaticBlock("Depot_WarehouseRoof", depotRoot.transform, new Vector3(-10.5f, 7.9f, 198.5f), new Vector3(16f, 0.6f, 8.8f), mats.GarageRoofMetal);
            PlacePrefabInstance(prefabs.RollerShutterDoor, depotRoot.transform, new Vector3(-10.5f, 0.03f, 194.35f), Quaternion.Euler(0f, 180f, 0f));

            CreateStaticBlock("Depot_SignFascia", depotRoot.transform, new Vector3(-10.5f, 6.6f, 194.3f), new Vector3(12f, 0.8f, 0.30f), mats.SedanTrimDark, addCollider: false);
            CreateWorldSignText(
                "KARGO DEPOSU",
                depotRoot.transform,
                new Vector3(-10.5f, 6.6f, 194.10f),
                Quaternion.identity,
                new Vector2(11.0f, 0.62f),
                new Color(0.96f, 0.96f, 0.94f, 1f));

            CreateBlenderShippingContainer("Depot_Container_01", depotRoot.transform, new Vector3(21.0f, 0.03f, 198.5f), Quaternion.Euler(0f, 180f, 0f), mats.ContainerRed, mats);
            CreateBlenderShippingContainer("Depot_Container_02", depotRoot.transform, new Vector3(25.0f, 0.03f, 198.5f), Quaternion.Euler(0f, 180f, 0f), mats.ContainerBlue, mats);
            CreateBlenderShippingContainer("Depot_Container_03", depotRoot.transform, new Vector3(23.0f, 3.03f, 198.5f), Quaternion.Euler(0f, 180f, 0f), mats.ContainerOchre, mats);

            PlacePrefabInstance(prefabs.WoodenCrate, depotRoot.transform, new Vector3(3.8f, 0.03f, 196.2f), Quaternion.Euler(0f, 14f, 0f));
            PlacePrefabInstance(prefabs.WoodenCrate, depotRoot.transform, new Vector3(5.0f, 0.03f, 196.5f), Quaternion.Euler(0f, -8f, 0f));
            PlacePrefabInstance(prefabs.OilBarrel, depotRoot.transform, new Vector3(-1.8f, 0.03f, 197.6f), Quaternion.Euler(0f, 45f, 0f));
            PlacePrefabInstance(prefabs.OilBarrel, depotRoot.transform, new Vector3(-0.8f, 0.03f, 198.0f), Quaternion.Euler(0f, 120f, 0f));
            PlacePrefabInstance(prefabs.FloodLights, depotRoot.transform, new Vector3(17.2f, 0.03f, 196.5f), Quaternion.Euler(0f, 145f, 0f));
            CreatePointLamp("Lamp_CargoDepot", depotRoot.transform, new Vector3(10.5f, 7.6f, 193.5f), new Color(1f, 0.82f, 0.50f), 30f, 3.8f);

            // Render top-down orthographic TownMap.png from the completed environment before adding dynamic actors
            Texture2D townMapTexture = RenderAndSaveTownMapTexture(sun);

            // --- WORLD ANCHORS & SPAWNS (Missions 1-5) ---
            GameObject anchorsRoot = new GameObject("--- WORLD_ANCHORS ---");
            CreateWorldAnchor("Spawn_GarageFoot", "spawn_garage_foot", anchorsRoot.transform, new Vector3(-3.0f, 0.12f, -21.5f), Quaternion.Euler(0f, 180f, 0f), 2f);
            CreateWorldAnchor("Spawn_GarageSedan", "spawn_garage_sedan", anchorsRoot.transform, new Vector3(9.5f, 0.28f, -32.5f), Quaternion.identity, 3.5f);
            CreateWorldAnchor("Harbor_DeliveryZone", "harbor_delivery_zone", anchorsRoot.transform, new Vector3(10.5f, 0.10f, 193.5f), Quaternion.identity, 7.5f);
            CreateWorldAnchor("MissingCar_SearchZone", "missing_car_zone", anchorsRoot.transform, new Vector3(-35.5f, 0.10f, 19.25f), Quaternion.identity, 14f);
            CreateWorldAnchor("Tail_StartLookoutZone", "tail_start_zone", anchorsRoot.transform, new Vector3(21.4f, 0.10f, 173.0f), Quaternion.Euler(0f, 180f, 0f), 11f);
            CreateWorldAnchor("Spawn_HarborTailStart", "spawn_harbor_tail_start", anchorsRoot.transform, new Vector3(21.4f, 0.28f, 177.5f), Quaternion.Euler(0f, 180f, 0f), 4f);
            CreateWorldAnchor("Neighborhood_ReturnZone", "neighborhood_return_zone", anchorsRoot.transform, new Vector3(0f, 0.10f, 44.0f), Quaternion.identity, 16f);

            // --- ACTORS (Player, Sedan, Story NPCs, Mission 3-5 Actors, Taxi Passenger) ---
            GameObject actorsRoot = new GameObject("--- ACTORS ---");
            GameObject playerObj = PlacePrefabInstance(prefabs.Player, actorsRoot.transform, new Vector3(-3.0f, 0.12f, -21.5f), Quaternion.Euler(0f, 180f, 0f));
            GameObject sedanObj = PlacePrefabInstance(prefabs.PlayerSedan, actorsRoot.transform, new Vector3(9.5f, 0.28f, -32.5f), Quaternion.identity);
            PlacePrefabInstance(prefabs.NpcMechanic, actorsRoot.transform, new Vector3(-2.5f, 0.04f, -32.2f), Quaternion.identity);
            PlacePrefabInstance(prefabs.NpcHarborReceiver, actorsRoot.transform, new Vector3(1.5f, 0.04f, 194.2f), Quaternion.Euler(0f, 155f, 0f));

            // Mission 3: Missing Dark Sedan parked in open residential courtyard lane with manifest clue
            GameObject missingCarObj = PlacePrefabInstance(prefabs.TrafficVehicle, actorsRoot.transform, new Vector3(-35.5f, 0.04f, 19.25f), Quaternion.Euler(0f, 85f, 0f));
            missingCarObj.name = "Mission03_MissingDarkSedan";
            WorldAnchor missingCarAnchor = missingCarObj.AddComponent<WorldAnchor>();
            missingCarAnchor.Configure("missing_car_clue", 4.2f, new Vector3(0f, 2.2f, 0f));
            InteractableTrigger missingCarTrig = missingCarObj.AddComponent<InteractableTrigger>();
            missingCarTrig.Configure(InteractableKind.InspectObject, "missing_car_clue", "ui.btn.inspect", 4.2f);

            // Mission 4: Courier Tail Vehicle + Dropped Package on West Street
            GameObject tailCarObj = PlacePrefabInstance(prefabs.TrafficVehicle, actorsRoot.transform, new Vector3(21.4f, 0.04f, 156.0f), Quaternion.Euler(0f, 180f, 0f));
            tailCarObj.name = "Mission04_TailCourierCar";
            WorldAnchor tailCarAnchor = tailCarObj.AddComponent<WorldAnchor>();
            tailCarAnchor.Configure("tail_target_car", 5.0f, new Vector3(0f, 2.4f, 0f));

            GameObject droppedPkgObj = PlacePrefabInstance(prefabs.WoodenCrate, actorsRoot.transform, new Vector3(-62.5f, 0.06f, 121.5f), Quaternion.Euler(0f, 25f, 0f));
            droppedPkgObj.name = "Mission04_DroppedPackage";
            droppedPkgObj.transform.localScale = new Vector3(0.65f, 0.65f, 0.65f);
            GameObjectUtility.SetStaticEditorFlags(droppedPkgObj, 0);
            WorldAnchor pkgAnchor = droppedPkgObj.AddComponent<WorldAnchor>();
            pkgAnchor.Configure("tail_dropped_package", 3.6f, new Vector3(0f, 1.8f, 0f));
            InteractableTrigger pkgTrig = droppedPkgObj.AddComponent<InteractableTrigger>();
            pkgTrig.Configure(InteractableKind.PickupPackage, "tail_dropped_package", "ui.btn.pickup", 3.6f);

            TailTargetVehicleController tailCtrl = tailCarObj.AddComponent<TailTargetVehicleController>();
            List<Vector3> tailRoute = new List<Vector3>
            {
                new Vector3(21.4f, 0.04f, 156.0f),
                new Vector3(21.4f, 0.04f, 116.0f),
                new Vector3(21.4f, 0.04f, 44.0f),
                new Vector3(-24.0f, 0.04f, 44.0f),
                new Vector3(-24.0f, 0.04f, 116.0f),
                new Vector3(-64.0f, 0.04f, 116.0f)
            };
            tailCtrl.Configure(tailRoute, droppedPkgObj);

            // Mission 5: Elena also waits at the Town Square fountain so player can choose Marco (Garage) OR Elena (Square)
            GameObject elenaSquareObj = PlacePrefabInstance(prefabs.NpcHarborReceiver, actorsRoot.transform, new Vector3(6.8f, 0.04f, 77.5f), Quaternion.Euler(0f, -110f, 0f));
            elenaSquareObj.name = "NPC_Elena_TownSquare";
            WorldAnchor elenaSqAnchor = elenaSquareObj.GetComponent<WorldAnchor>();
            if (elenaSqAnchor != null) elenaSqAnchor.Configure("npc_elena_neighborhood", 3.6f, new Vector3(0f, 2.3f, 0f));
            InteractableTrigger elenaSqTrig = elenaSquareObj.GetComponent<InteractableTrigger>();
            if (elenaSqTrig != null) elenaSqTrig.Configure(InteractableKind.Npc, "npc_elena_neighborhood", "ui.btn.talk", 3.6f);

            // Dedicated Taxi Passenger actor (inactive until Taxi job starts)
            GameObject taxiPassengerObj = new GameObject("NPC_TaxiPassenger");
            taxiPassengerObj.transform.SetParent(actorsRoot.transform, false);
            CapsuleCollider taxiCol = taxiPassengerObj.AddComponent<CapsuleCollider>();
            taxiCol.center = new Vector3(0f, 0.9f, 0f);
            taxiCol.radius = 0.34f;
            taxiCol.height = 1.76f;
            ProceduralHumanoid taxiHum = BuildBlenderHumanoid(
                taxiPassengerObj.transform,
                mats,
                BlenderCharacterVariant.CasualFemale,
                mats.PedShirtTeal,
                mats.CharPantsDenim,
                mats.CharSkin,
                mats.CharHairBrown,
                1.72f);
            taxiPassengerObj.SetActive(false);

            modeController.ConfigureReferences(
                playerObj.GetComponent<PlayerFootController>(),
                sedanObj.GetComponent<SedanVehicleController>(),
                camRig);

            // --- 3D Beacons (Story Mission Beacon + Side-Job Teal Beacon) ---
            ObjectiveBeacon missionBeacon = BuildObjectiveBeacon("ObjectiveBeacon", mats.BeaconGlow);
            missionManager.Configure(catalog, missionBeacon);

            ObjectiveBeacon jobBeacon = BuildObjectiveBeacon("JobObjectiveBeacon", mats.JobRingGlow);
            jobBeacon.SetVisible(false);
            jobManager.Configure(jobBeacon, taxiPassengerObj, taxiHum, mats.ContainerOchre, mats.TownFabricTeal);

            // --- Pooled Ambient Traffic along Cumhuriyet Caddesi (stops at red lights & yields via SphereCast) ---
            GameObject trafficRoot = new GameObject("AmbientTrafficManager");
            AmbientTrafficManager trafficMgr = trafficRoot.AddComponent<AmbientTrafficManager>();
            List<Transform> waypoints = new List<Transform>();
            Vector3[] wpCoords =
            {
                new Vector3(26.6f, 0.05f, -18f),
                new Vector3(26.6f, 0.05f, 44f),
                new Vector3(26.6f, 0.05f, 116f),
                new Vector3(26.6f, 0.05f, 178f),
                new Vector3(21.4f, 0.05f, 178f),
                new Vector3(21.4f, 0.05f, 116f),
                new Vector3(21.4f, 0.05f, 44f),
                new Vector3(21.4f, 0.05f, -18f)
            };
            for (int i = 0; i < wpCoords.Length; i++)
            {
                GameObject wp = new GameObject($"WP_{i:00}");
                wp.transform.SetParent(trafficRoot.transform, false);
                wp.transform.position = wpCoords[i];
                waypoints.Add(wp.transform);
            }
            trafficMgr.Configure(prefabs.TrafficVehicle, waypoints, stopLines);

            // --- Calm Modern HUD, Minimap, Shop Panel, Paint Palette & Phone/Map ---
            BuildHudCanvas(uiSprites, townMapTexture, needsController);

            DynamicGI.UpdateEnvironment();
            EditorSceneManager.SaveScene(scene, TestScenePath);
            RegisterSceneInBuildSettings(TestScenePath);
        }

        private static ObjectiveBeacon BuildObjectiveBeacon(string objectName, Material beaconMat)
        {
            GameObject root = new GameObject(objectName);
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
            light.enabled = false;

            NightLight nl = go.AddComponent<NightLight>();
            nl.Configure(light, null, Color.black);
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
            txt.lineSpacing = 1.1f;
            txt.alignment = alignment;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }
    }
}
