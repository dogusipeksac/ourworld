using System.Collections.Generic;
using System.IO;
using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Jobs;
using OurWorld.Missions;
using OurWorld.Player;
using OurWorld.UI;
using OurWorld.Vehicle;
using OurWorld.World;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OurWorld.Editor
{
    /// <summary>
    /// One-click Unity Editor setup tool for GRIDROAM (Android + iOS):
    /// - Configures shared Landscape settings, Android (API 26+, IL2CPP ARM64), iOS (iOS 15+, Defer System Gestures, IL2CPP)
    /// - Creates shared Mobile URP Low/Medium profiles + ACES/Bloom Post-Processing Volume
    /// - Generates 9-sliced UI sprites (UI_RoundedRect, UI_Circle, UI_Ring) and orthographic TownMap.png
    /// - Builds procedural jointed humanoid player/NPCs, 4 drivable vehicles with fuel, enterable houses & shops,
    ///   Side Jobs, Economy, Day/Night Cycle, Gas Station, Phone/Map, Minimap, and Turkish-first calm modern HUD.
    /// </summary>
    public static partial class OurWorldPrototypeSetup
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

        [MenuItem("OurWorld/1. Build Sultanbeyli Selman Sokağı (Görevler 1-5 + Yan İşler)", priority = 1)]
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

            Debug.Log("[OurWorld] Sultanbeyli · Selman Sokağı (Görevler 1-5, 4 Yan İş, Hz. Osman Camii, Ekonomi, Gün/Gece, Telefon/Harita & Türkçe Arayüz) başarıyla oluşturuldu!");
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Our World · Sultanbeyli Selman Sokağı Hazır (Android & iOS)",
                    "Sultanbeyli · Selman Sokağı açık dünyası (Görevler 1–5, Pizza/Taksi/Temizlik/Boyama yan işleri, içi gezilebilir evler & Hz. Osman Camii, ATM, sokak kedileri, mahalle futbolu, telefon/harita ve Türkçe arayüz) hazır.\n\nOynamak için Play'e bas!",
                    "Tamam");
            }
        }

        [MenuItem("OurWorld/2. Configure Android + iOS & URP Profiles Only", priority = 2)]
        public static void ConfigureProjectOnly()
        {
            EnsureDirectories();
            ConfigureCrossPlatformMobileSettings();
            CreateOrUpdateUrpProfiles();
            AssetDatabase.SaveAssets();
            Debug.Log("[OurWorld] Shared Android & iOS settings + URP Mobile Low/Medium profiles configured.");
        }

        public const string AppIconAssetPath = "Assets/GRIDROAM/Art/Store/AppIcon_1024.png";

        public static void ConfigureCrossPlatformMobileSettings()
        {
            const string bundleId = "com.dogusipeksac.ourworld";

            PlayerSettings.companyName = "Doğuş İpeksaç";
            PlayerSettings.productName = "Our World";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.09f, 0.114f, 0.141f, 1f);

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, bundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.resizeableActivity = true; // Google Play Large Screen / Tablet & Foldable support
            PlayerSettings.Android.forceInternetPermission = true; // Player feedback -> Firebase (Öneri sekmesi)
            if (PlayerSettings.Android.bundleVersionCode < 1)
            {
                PlayerSettings.Android.bundleVersionCode = 1;
            }

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, bundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.appleDeveloperTeamID = "659VRL92D3";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            if (string.IsNullOrWhiteSpace(PlayerSettings.iOS.buildNumber) || PlayerSettings.iOS.buildNumber == "0")
            {
                PlayerSettings.iOS.buildNumber = "1";
            }
            PlayerSettings.iOS.deferSystemGesturesMode =
                UnityEngine.iOS.SystemGestureDeferMode.BottomEdge |
                UnityEngine.iOS.SystemGestureDeferMode.TopEdge;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.requiresFullScreen = true;

            ApplyStoreAppIcons();
            SyncBuildInfoResource();
        }

        private static void SyncBuildInfoResource()
        {
            string buildInfoPath = $"{RootDir}/Resources/OurWorldBuildInfo.txt";
            string dir = Path.GetDirectoryName(buildInfoPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            int androidCode = Mathf.Max(1, PlayerSettings.Android.bundleVersionCode);
            string iosBuild = string.IsNullOrWhiteSpace(PlayerSettings.iOS.buildNumber) ? "1" : PlayerSettings.iOS.buildNumber.Trim();
            string content = $"android={androidCode}\nios={iosBuild}\n";
            if (!File.Exists(buildInfoPath) || File.ReadAllText(buildInfoPath) != content)
            {
                File.WriteAllText(buildInfoPath, content);
            }
        }

        public static void ApplyStoreAppIcons()
        {
            if (!File.Exists(AppIconAssetPath))
            {
                return;
            }

            TextureImporter ti = AssetImporter.GetAtPath(AppIconAssetPath) as TextureImporter;
            if (ti != null)
            {
                bool dirty = false;
                if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
                if (ti.npotScale != TextureImporterNPOTScale.None) { ti.npotScale = TextureImporterNPOTScale.None; dirty = true; }
                if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
                if (ti.alphaIsTransparency) { ti.alphaIsTransparency = false; dirty = true; }
                if (ti.maxTextureSize < 1024) { ti.maxTextureSize = 1024; dirty = true; }
                if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }

            Texture2D iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconAssetPath);
            if (iconTex == null) return;

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { iconTex }, IconKind.Any);

            // Populate all Android & iOS legacy/application icon slots cleanly
            NamedBuildTarget[] targets = { NamedBuildTarget.Android, NamedBuildTarget.iOS };
            for (int t = 0; t < targets.Length; t++)
            {
                int[] sizes = PlayerSettings.GetIconSizes(targets[t], IconKind.Application);
                if (sizes != null && sizes.Length > 0)
                {
                    Texture2D[] filled = new Texture2D[sizes.Length];
                    for (int i = 0; i < filled.Length; i++) filled[i] = iconTex;
                    PlayerSettings.SetIcons(targets[t], filled, IconKind.Application);
                }
            }
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

            // Enable Read/Write on all Blender vehicle FBX meshes so VehicleDamageSystem can deform/crumple them at runtime!
            if (Directory.Exists(BlenderModelsDir))
            {
                string[] carFbxs = Directory.GetFiles(BlenderModelsDir, "Car_*.fbx", SearchOption.TopDirectoryOnly);
                foreach (string rawFbx in carFbxs)
                {
                    string fbxAssetPath = rawFbx.Replace("\\", "/");
                    ModelImporter mi = AssetImporter.GetAtPath(fbxAssetPath) as ModelImporter;
                    if (mi != null && !mi.isReadable)
                    {
                        mi.isReadable = true;
                        mi.SaveAndReimport();
                    }
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

            if (assetPath.Contains("/Weathering/"))
            {
                if (importer.maxTextureSize != 512)
                {
                    importer.maxTextureSize = 512;
                    dirty = true;
                }
                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    dirty = true;
                }
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
                // Wire PC_Renderer (with ScreenSpaceAmbientOcclusion enabled) for rich contact shading & crisp 2048 shadows!
                ScriptableRendererData pcRenderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
                if (pcRenderer != null)
                {
                    SerializedObject medSo = new SerializedObject(medAsset);
                    SerializedProperty listProp = medSo.FindProperty("m_RendererDataList");
                    if (listProp != null && listProp.arraySize > 0)
                    {
                        listProp.GetArrayElementAtIndex(0).objectReferenceValue = pcRenderer;
                        medSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                ConfigureUrpSerialized(medAsset, renderScale: 1.00f, shadowDistance: 75f, shadowRes: 2048, hdr: true, msaa: 4, cascades: 3);
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
            public Material CarYellow;
            public Material CarSkyBlue;
            public Material CarCharcoal;
            public Material CarVanCream;
            public Material PoolTileBlue;
            public Material PoolWaterClear;
            public Material TravertineStone;
            public Material IstanbulLeadRoof;
            public Material IstanbulBridgeSteel;
            public Material IstanbulTramRed;
            public Material IstanbulGoldBrass;
            public Material IstanbulFerryHull;
            public Material IstanbulBosphorusWater;
            public Material KilitParke;
            public Material RawBrick;
            public Material RedPlaster;
            public Material SagePlaster;
            public Material YellowPlaster;
            public Material OrangeStripe;
            public Material OliveFacade;
            public Material FenceBlue;
            public Material WhitePVC;
            public Material DarkInterior;
            public Material RustSteel;
            public Material VineBrown;
            public Material DoorTeal;
            public Material StreetSignBlue;
            public Material PeachPlaster;
            public Material CoralPlaster;
            public Material PinkPlaster;
            public Material PowderBlue;
            public Material MeshGreen;
            public Material CobaltBlue;
            public Material LilacGate;
            public Material TurquoiseRust;
            public Material Galvanized;
            public Material SignRed;
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
            public Material WeatherManholeGrime;
            public Material WeatherAsphaltPatch;
            public Material WeatherGutterStreak;
            public Material WindowGlassBlue;
            public Material WindowCurtain;
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
                RoadAsphaltMain = CreatePbrMaterial("MAT_Road_Asphalt012", urpLit, "Asphalt012", new Color(0.58f, 0.59f, 0.62f), 0.13f, 0f, Vector2.one, 0.68f),
                RoadAsphaltCross = CreatePbrMaterial("MAT_Road_Asphalt012_Cross", urpLit, "Asphalt012", new Color(0.58f, 0.59f, 0.62f), 0.13f, 0f, Vector2.one, 0.68f),
                RoadMarkingWhite = CreateOrLoadMaterial("MAT_Road_Marking_White", urpLit, new Color(0.84f, 0.84f, 0.82f), 0.12f),
                RoadMarkingYellow = CreateOrLoadMaterial("MAT_Road_Marking_Yellow", urpLit, new Color(0.86f, 0.68f, 0.20f), 0.14f),
                Ground = CreatePbrMaterial("MAT_Ground_Urban", urpLit, "Concrete034", new Color(0.55f, 0.55f, 0.57f), 0.12f, 0f, new Vector2(22f, 64f), 0.7f),
                Sidewalk = CreatePbrMaterial("MAT_Sidewalk_Paving", urpLit, "PavingStones070", new Color(0.80f, 0.78f, 0.76f), 0.12f, 0f, new Vector2(2f, 44f), 0.85f),
                SeaWater = CreateOrLoadMaterial("MAT_Sea_Evening", urpLit, new Color(0.06f, 0.19f, 0.30f), 0.92f, 0.16f),
                HarborDock = CreatePbrMaterial("MAT_Harbor_Dock", urpLit, "Concrete034", new Color(0.74f, 0.73f, 0.72f), 0.16f, 0f, new Vector2(10f, 6f), 0.95f),
                GarageBrick = CreatePbrMaterial("MAT_Garage_Bricks", urpLit, "Bricks059", new Color(0.84f, 0.80f, 0.78f), 0.14f, 0f, new Vector2(3.5f, 2f), 1.0f),
                GarageRoofMetal = CreatePbrMaterial("MAT_Garage_RoofMetal", urpLit, "CorrugatedSteel005", new Color(0.52f, 0.55f, 0.58f), 0.42f, 0.55f, new Vector2(4f, 3f), 0.9f),
                PlasterTrim = CreatePbrMaterial("MAT_Plaster_Trim", urpLit, "Plaster003", new Color(0.78f, 0.75f, 0.70f), 0.09f, 0f, new Vector2(3.2f, 2.8f), 0.82f),
                BuildingFacadeA = CreatePbrMaterial("MAT_Building_FacadeA", urpLit, "Facade005", new Color(0.92f, 0.88f, 0.84f), 0.24f, 0.08f, new Vector2(2f, 2f), 0.95f),
                BuildingFacadeB = CreatePbrMaterial("MAT_Building_FacadeB", urpLit, "Facade006", new Color(0.88f, 0.90f, 0.94f), 0.24f, 0.10f, new Vector2(2f, 2f), 0.95f),
                BuildingFacadeC = CreatePbrMaterial("MAT_Building_FacadeC", urpLit, "Facade018A", new Color(0.94f, 0.86f, 0.82f), 0.22f, 0.08f, new Vector2(2f, 2f), 0.95f),
                Concrete = CreatePbrMaterial("MAT_Concrete_Props", urpLit, "Concrete034", new Color(0.68f, 0.68f, 0.66f), 0.10f, 0f, new Vector2(3.5f, 1.5f), 0.88f),
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
                TownInteriorPaintWarm = CreatePbrMaterial("MAT_Town_InteriorPaint_WarmWhite", urpLit, "Plaster003", new Color(0.93f, 0.91f, 0.86f), 0.10f, 0f, new Vector2(2.6f, 2.6f), 0.48f),
                TownInteriorPaintCream = CreatePbrMaterial("MAT_Town_InteriorPaint_Cream", urpLit, "Plaster003", new Color(0.90f, 0.86f, 0.78f), 0.09f, 0f, new Vector2(3.0f, 3.0f), 0.65f),
                TownInteriorPaintPearl = CreatePbrMaterial("MAT_Town_InteriorPaint_Pearl", urpLit, "Plaster003", new Color(0.89f, 0.90f, 0.88f), 0.10f, 0f, new Vector2(2.6f, 2.6f), 0.48f),
                TownFabricGrey = CreateOrLoadMaterial("MAT_Town_Fabric_Grey", urpLit, new Color(0.42f, 0.43f, 0.46f), 0.12f),
                TownFabricTeal = CreateOrLoadMaterial("MAT_Town_Fabric_Teal", urpLit, new Color(0.18f, 0.44f, 0.46f), 0.12f),
                TownFabricMustard = CreateOrLoadMaterial("MAT_Town_Fabric_Mustard", urpLit, new Color(0.76f, 0.58f, 0.20f), 0.12f),
                TownFurnitureWood = CreateOrLoadMaterial("MAT_Town_FurnitureWood", urpLit, new Color(0.50f, 0.33f, 0.19f), 0.38f),
                TownPaintCan = CreateOrLoadMaterial("MAT_Town_PaintCan", urpLit, new Color(0.80f, 0.83f, 0.87f), 0.68f, 0.55f),
                CarRed = CreateOrLoadMaterial("MAT_Car_Red", urpLit, new Color(0.55f, 0.06f, 0.05f), 0.75f, 0.40f),
                CarWhite = CreateOrLoadMaterial("MAT_Car_White", urpLit, new Color(0.92f, 0.92f, 0.90f), 0.78f, 0.32f),
                CarDarkGreen = CreateOrLoadMaterial("MAT_Car_DarkGreen", urpLit, new Color(0.07f, 0.23f, 0.15f), 0.76f, 0.42f),
                CarYellow = CreateOrLoadMaterial("MAT_Car_Yellow", urpLit, new Color(0.94f, 0.68f, 0.08f), 0.88f, 0.46f),
                CarSkyBlue = CreateOrLoadMaterial("MAT_Car_SkyBlue", urpLit, new Color(0.12f, 0.42f, 0.74f), 0.86f, 0.48f),
                CarCharcoal = CreateOrLoadMaterial("MAT_Car_Charcoal", urpLit, new Color(0.16f, 0.18f, 0.21f), 0.82f, 0.52f),
                CarVanCream = CreateOrLoadMaterial("MAT_Car_VanCream", urpLit, new Color(0.92f, 0.90f, 0.84f), 0.65f, 0.20f),
                PoolTileBlue = CreateOrLoadMaterial("MAT_Pool_TileBlue", urpLit, new Color(0.16f, 0.58f, 0.78f), 0.72f, 0.05f),
                PoolWaterClear = CreateOrLoadMaterial("MAT_Pool_WaterClear", urpLit, new Color(0.12f, 0.70f, 0.88f, 0.66f), 0.95f, 0.08f),
                TravertineStone = CreateOrLoadMaterial("MAT_Travertine_Stone", urpLit, new Color(0.94f, 0.91f, 0.85f), 0.24f, 0.0f),
                IstanbulLeadRoof = CreateOrLoadMaterial("MAT_Istanbul_LeadRoof", urpLit, new Color(0.28f, 0.31f, 0.36f), 0.52f, 0.62f),
                IstanbulBridgeSteel = CreateOrLoadMaterial("MAT_Istanbul_BridgeSteel", urpLit, new Color(0.86f, 0.88f, 0.90f), 0.65f, 0.68f),
                IstanbulTramRed = CreateOrLoadMaterial("MAT_Istanbul_TramRed", urpLit, new Color(0.74f, 0.10f, 0.10f), 0.86f, 0.42f),
                IstanbulGoldBrass = CreateOrLoadMaterial("MAT_Istanbul_GoldBrass", urpLit, new Color(0.88f, 0.70f, 0.22f), 0.84f, 0.90f),
                IstanbulFerryHull = CreateOrLoadMaterial("MAT_Istanbul_FerryHull", urpLit, new Color(0.10f, 0.14f, 0.20f), 0.55f, 0.22f),
                IstanbulBosphorusWater = CreateOrLoadMaterial("MAT_Istanbul_BosphorusWater", urpLit, new Color(0.06f, 0.34f, 0.50f, 0.78f), 0.94f, 0.14f),
                KilitParke = CreateOrLoadMaterial("MAT_Sultanbeyli_KilitParke", urpLit, new Color(0.75f, 0.74f, 0.73f), 0.10f, 0.0f),
                RawBrick = CreateOrLoadMaterial("MAT_Sultanbeyli_RawBrick", urpLit, new Color(0.80f, 0.73f, 0.68f), 0.10f, 0.0f),
                RedPlaster = CreatePbrMaterial("MAT_Sultanbeyli_RedPlaster", urpLit, "Plaster003", new Color(0.65f, 0.22f, 0.17f), 0.09f, 0f, new Vector2(3.2f, 3.2f), 0.82f),
                SagePlaster = CreatePbrMaterial("MAT_Sultanbeyli_SagePlaster", urpLit, "Plaster003", new Color(0.64f, 0.67f, 0.56f), 0.09f, 0f, new Vector2(3.2f, 3.2f), 0.82f),
                YellowPlaster = CreatePbrMaterial("MAT_Sultanbeyli_YellowPlaster", urpLit, "Plaster003", new Color(0.84f, 0.76f, 0.54f), 0.09f, 0f, new Vector2(3.4f, 3.4f), 0.82f),
                OrangeStripe = CreatePbrMaterial("MAT_Sultanbeyli_OrangeStripe", urpLit, "Plaster003", new Color(0.73f, 0.41f, 0.21f), 0.10f, 0f, new Vector2(3.0f, 3.2f), 0.82f),
                OliveFacade = CreatePbrMaterial("MAT_Sultanbeyli_OliveFacade", urpLit, "Plaster003", new Color(0.34f, 0.42f, 0.30f), 0.10f, 0f, new Vector2(3.0f, 3.2f), 0.82f),
                FenceBlue = CreateOrLoadMaterial("MAT_Sultanbeyli_FenceBlue", urpLit, new Color(0.38f, 0.54f, 0.70f), 0.52f, 0.52f),
                WhitePVC = CreateOrLoadMaterial("MAT_Sultanbeyli_WhitePVC", urpLit, new Color(0.90f, 0.90f, 0.88f), 0.44f, 0.04f),
                DarkInterior = CreateOrLoadMaterial("MAT_Sultanbeyli_DarkInterior", urpLit, new Color(0.13f, 0.22f, 0.30f), 0.88f, 0.28f),
                WindowGlassBlue = CreateOrLoadMaterial("MAT_Sultanbeyli_WindowGlass", urpLit, new Color(0.20f, 0.34f, 0.48f), 0.94f, 0.36f),
                WindowCurtain = CreateOrLoadMaterial("MAT_Sultanbeyli_WindowCurtain", urpLit, new Color(0.76f, 0.81f, 0.85f), 0.14f, 0.0f),
                RustSteel = CreateOrLoadMaterial("MAT_Sultanbeyli_RustSteel", urpLit, new Color(0.38f, 0.24f, 0.17f), 0.24f, 0.55f),
                VineBrown = CreateOrLoadMaterial("MAT_Sultanbeyli_VineBrown", urpLit, new Color(0.30f, 0.21f, 0.15f), 0.10f, 0.0f),
                DoorTeal = CreateOrLoadMaterial("MAT_Sultanbeyli_DoorTeal", urpLit, new Color(0.11f, 0.29f, 0.38f), 0.48f, 0.22f),
                StreetSignBlue = CreateOrLoadMaterial("MAT_Sultanbeyli_StreetSignBlue", urpLit, new Color(0.08f, 0.24f, 0.58f), 0.48f, 0.15f),
                PeachPlaster = CreatePbrMaterial("MAT_Sultanbeyli_PeachPlaster", urpLit, "Plaster003", new Color(0.84f, 0.71f, 0.62f), 0.09f, 0f, new Vector2(3.2f, 3.2f), 0.82f),
                CoralPlaster = CreatePbrMaterial("MAT_Sultanbeyli_CoralPlaster", urpLit, "Plaster003", new Color(0.74f, 0.42f, 0.39f), 0.09f, 0f, new Vector2(3.2f, 3.2f), 0.82f),
                PinkPlaster = CreatePbrMaterial("MAT_Sultanbeyli_PinkPlaster", urpLit, "Plaster003", new Color(0.80f, 0.61f, 0.63f), 0.09f, 0f, new Vector2(3.2f, 3.4f), 0.82f),
                PowderBlue = CreatePbrMaterial("MAT_Sultanbeyli_PowderBlue", urpLit, "Plaster003", new Color(0.56f, 0.66f, 0.75f), 0.09f, 0f, new Vector2(3.2f, 3.4f), 0.82f),
                MeshGreen = CreatePbrMaterial("MAT_Sultanbeyli_MeshGreen", urpLit, "Grass001", new Color(0.14f, 0.30f, 0.17f), 0.12f, 0f, new Vector2(4f, 2f), 0.60f),
                CobaltBlue = CreateOrLoadMaterial("MAT_Sultanbeyli_CobaltBlue", urpLit, new Color(0.09f, 0.38f, 0.78f), 0.55f, 0.35f),
                LilacGate = CreateOrLoadMaterial("MAT_Sultanbeyli_LilacGate", urpLit, new Color(0.66f, 0.58f, 0.78f), 0.48f, 0.42f),
                TurquoiseRust = CreateOrLoadMaterial("MAT_Sultanbeyli_TurquoiseRust", urpLit, new Color(0.22f, 0.56f, 0.52f), 0.34f, 0.40f),
                Galvanized = CreatePbrMaterial("MAT_Sultanbeyli_Galvanized", urpLit, "CorrugatedSteel005", new Color(0.70f, 0.72f, 0.74f), 0.52f, 0.68f, new Vector2(2f, 2f), 0.85f),
                SignRed = CreateOrLoadMaterial("MAT_Sultanbeyli_SignRed", urpLit, new Color(0.76f, 0.11f, 0.11f), 0.54f, 0.15f),
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

                set.PoolWaterClear.SetTexture("_BumpMap", waterNormal);
                set.PoolWaterClear.SetTextureScale("_BumpMap", new Vector2(8f, 12f));
                set.PoolWaterClear.SetFloat("_BumpScale", 0.18f);
                set.PoolWaterClear.EnableKeyword("_NORMALMAP");

                set.IstanbulBosphorusWater.SetTexture("_BumpMap", waterNormal);
                set.IstanbulBosphorusWater.SetTextureScale("_BumpMap", new Vector2(24f, 90f));
                set.IstanbulBosphorusWater.SetFloat("_BumpScale", 0.28f);
                set.IstanbulBosphorusWater.EnableKeyword("_NORMALMAP");
            }

            // Configure transparent water surfaces (Pool & Bosphorus Strait)
            Material[] transparentWaters = { set.PoolWaterClear, set.IstanbulBosphorusWater };
            foreach (Material wm in transparentWaters)
            {
                if (wm == null) continue;
                wm.SetFloat("_Surface", 1f);
                wm.SetFloat("_Blend", 0f);
                wm.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                wm.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                wm.SetFloat("_ZWrite", 0f);
                wm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                wm.renderQueue = (int)RenderQueue.Transparent;
                EditorUtility.SetDirty(wm);
            }

            // Wire RealismPBR micro-textures (Denim, Cotton, Hair, Skin, Teak Wood, Travertine Stone)
            // to eliminate flat untextured plastic surfaces across characters, benches & fountain!
            string pbrDir = $"{TexturesDir}/RealismPBR";
            Texture2D cottonCol = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/Cotton_Color.png");
            Texture2D cottonNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/Cotton_NormalGL.png");
            Texture2D denimCol = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/Denim_Color.png");
            Texture2D denimNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/Denim_NormalGL.png");
            Texture2D hairCol = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/Hair_Color.png");
            Texture2D hairNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/Hair_NormalGL.png");
            Texture2D skinNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/Skin_NormalGL.png");
            Texture2D teakCol = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/TeakWood_Color.png");
            Texture2D teakNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/TeakWood_NormalGL.png");
            Texture2D travCol = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/Travertine_Color.png");
            Texture2D travNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/Travertine_NormalGL.png");

            Material[] clothMats =
            {
                set.CharShirt, set.CharUndershirt, set.MechanicSuit, set.ReceiverSuit,
                set.PedShirtRed, set.PedShirtOlive, set.PedShirtMustard, set.PedShirtTeal, set.PedShirtBurgundy
            };
            foreach (Material m in clothMats) ApplyPbrDetailMaps(m, cottonCol, cottonNrm, new Vector2(3.5f, 3.5f), 0.55f, 0.14f);

            Material[] denimMats = { set.CharPants, set.CharPantsDenim, set.CharPantsKhaki };
            foreach (Material m in denimMats) ApplyPbrDetailMaps(m, denimCol, denimNrm, new Vector2(4.0f, 4.0f), 0.65f, 0.12f);

            Material[] hairMats = { set.CharHair, set.CharHairBrown, set.CharHairBlonde };
            foreach (Material m in hairMats) ApplyPbrDetailMaps(m, hairCol, hairNrm, new Vector2(3.0f, 3.0f), 0.58f, 0.36f);

            Material[] skinMats = { set.CharSkin, set.CharSkinMedium, set.CharSkinWarm };
            foreach (Material m in skinMats) ApplyPbrDetailMaps(m, null, skinNrm, new Vector2(4.0f, 4.0f), 0.22f, 0.22f);

            Material[] woodMats = { set.TownWood, set.TownFurnitureWood, set.TownDoorWood };
            foreach (Material m in woodMats) ApplyPbrDetailMaps(m, teakCol, teakNrm, new Vector2(1.8f, 1.8f), 0.65f, 0.32f);

            ApplyPbrDetailMaps(set.TravertineStone, travCol, travNrm, new Vector2(2.2f, 2.2f), 0.72f, 0.24f);

            // Authentic Sultanbeyli / Selman Sokağı PBR detail maps (Kilit Parke sidewalk + exposed hollow red brick!)
            Texture2D kilitCol = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/KilitParke_Color.png");
            Texture2D kilitNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/KilitParke_NormalGL.png");
            Texture2D rawBrickCol = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/RawBrick_Color.png");
            Texture2D rawBrickNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pbrDir}/RawBrick_NormalGL.png");
            ApplyPbrDetailMaps(set.KilitParke, kilitCol, kilitNrm, Vector2.one, 0.78f, 0.10f);
            ApplyPbrDetailMaps(set.RawBrick, rawBrickCol, rawBrickNrm, new Vector2(2.1f, 2.1f), 0.85f, 0.10f);

            // Shared lightweight 512px street weathering overlay materials (zero extra lights, GPU instanced!)
            string weatherDir = $"{TexturesDir}/Weathering";
            set.WeatherManholeGrime = CreateWeatherOverlayMaterial("MAT_Weather_ManholeGrime", urpLit, $"{weatherDir}/Weather_ManholeGrime.png", new Color(0.28f, 0.27f, 0.26f, 0.72f), 0.05f);
            set.WeatherAsphaltPatch = CreateWeatherOverlayMaterial("MAT_Weather_AsphaltPatch", urpLit, $"{weatherDir}/Weather_AsphaltPatch.png", new Color(0.26f, 0.27f, 0.29f, 0.76f), 0.08f);
            set.WeatherGutterStreak = CreateWeatherOverlayMaterial("MAT_Weather_GutterStreak", urpLit, $"{weatherDir}/Weather_GutterAndStreak.png", new Color(0.24f, 0.23f, 0.22f, 0.68f), 0.04f);

            // Automotive glossy metallic clearcoat across all 6 vehicle paint materials!
            Material[] carPaintMats =
            {
                set.SedanBody, set.CarRed, set.CarWhite, set.CarDarkGreen,
                set.CarYellow, set.CarSkyBlue, set.CarCharcoal, set.CarVanCream, set.TrafficBody
            };
            foreach (Material m in carPaintMats) ApplyAutomotiveClearcoat(m);

            return set;
        }

        private static void ApplyPbrDetailMaps(
            Material mat,
            Texture2D albedo,
            Texture2D normalMap,
            Vector2 tiling,
            float bumpScale,
            float smoothness)
        {
            if (mat == null) return;
            if (albedo != null && mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTextureScale("_BaseMap", tiling);
            }
            if (normalMap != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.SetFloat("_BumpScale", bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }
            EditorUtility.SetDirty(mat);
        }

        private static Material CreateWeatherOverlayMaterial(
            string matName,
            Shader shader,
            string texturePath,
            Color tint,
            float smoothness)
        {
            Material mat = CreateOrLoadMaterial(matName, shader, tint, smoothness, 0f);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (tex != null && mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", Vector2.one);
            }
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateWeatherDecalQuad(
            string name,
            Transform parent,
            Vector3 worldPos,
            Quaternion worldRot,
            Vector2 size,
            Material mat)
        {
            if (mat == null) return null;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
            MarkStaticEnvironment(go);
            return go;
        }

        private static void DisableShadowCasting(GameObject go)
        {
            if (go == null) return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// Mounts a crisp, low-poly Turkish PVC residential/shop window flush on any building wall
        /// (local +Z faces outward from the wall). All sub-meshes use GPU-instanced shared materials,
        /// zero colliders, and ShadowCastingMode.Off to keep mobile 30+ FPS completely unaffected.
        /// </summary>
        private static GameObject AddTurkishWindowFrame(
            Transform parent,
            string name,
            Vector3 localPos,
            Quaternion localRot,
            float width,
            float height,
            MaterialSet mats,
            bool warmLit = false,
            bool withCurtains = true,
            bool withTransom = true)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = localRot;

            Material glassMat = warmLit && mats.WindowLitWarm != null
                ? mats.WindowLitWarm
                : (mats.WindowGlassBlue != null ? mats.WindowGlassBlue : mats.SedanGlass);

            // 1. Outer crisp white PVC frame backing
            CreatePrimitiveChild("Frame", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0f, 0.032f), new Vector3(width, height, 0.064f), mats.WhitePVC, removeCollider: true);

            // 2. Reflective sky-blue architectural glass (or warm cozy lit pane for select living rooms)
            float glassW = Mathf.Max(0.24f, width - 0.15f);
            float glassH = Mathf.Max(0.24f, height - 0.15f);
            CreatePrimitiveChild("Glass", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0f, 0.066f), new Vector3(glassW, glassH, 0.018f), glassMat, removeCollider: true);

            // 3. Subtle recessed sheer curtain drapes at left & right edges (seen behind mullions!)
            if (withCurtains && !warmLit && mats.WindowCurtain != null && glassW >= 0.72f)
            {
                float drapeW = glassW * 0.17f;
                float drapeX = (glassW * 0.5f) - (drapeW * 0.5f) - 0.02f;
                CreatePrimitiveChild("CurtainL", PrimitiveType.Cube, root.transform,
                    new Vector3(-drapeX, 0f, 0.074f), new Vector3(drapeW, glassH - 0.04f, 0.010f), mats.WindowCurtain, removeCollider: true);
                CreatePrimitiveChild("CurtainR", PrimitiveType.Cube, root.transform,
                    new Vector3(drapeX, 0f, 0.074f), new Vector3(drapeW, glassH - 0.04f, 0.010f), mats.WindowCurtain, removeCollider: true);
            }

            // 4. Vertical center PVC mullion & optional upper transom sash bar
            CreatePrimitiveChild("MullionV", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0f, 0.082f), new Vector3(0.06f, glassH + 0.04f, 0.042f), mats.WhitePVC, removeCollider: true);

            if (withTransom && height >= 1.10f)
            {
                CreatePrimitiveChild("TransomH", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, height * 0.22f, 0.082f), new Vector3(glassW + 0.04f, 0.052f, 0.042f), mats.WhitePVC, removeCollider: true);
            }

            // 5. Projecting concrete/marble window sill
            CreatePrimitiveChild("Sill", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -height * 0.5f - 0.032f, 0.068f), new Vector3(width + 0.14f, 0.075f, 0.16f), mats.Concrete, removeCollider: true);

            DisableShadowCasting(root);
            MarkStaticEnvironment(root);
            return root;
        }

        private static void AddSideWallWindowGrid(
            Transform parent,
            string prefix,
            float wallX,
            bool facePosX,
            float[] localZs,
            float[] floorYs,
            float width,
            float height,
            MaterialSet mats,
            int warmLitIndex = -1)
        {
            Quaternion rot = facePosX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.Euler(0f, -90f, 0f);
            int idx = 0;
            for (int f = 0; f < floorYs.Length; f++)
            {
                for (int c = 0; c < localZs.Length; c++)
                {
                    bool warm = (idx == warmLitIndex);
                    AddTurkishWindowFrame(
                        parent,
                        $"{prefix}_F{f}_C{c}",
                        new Vector3(wallX, floorYs[f], localZs[c]),
                        rot,
                        width,
                        height,
                        mats,
                        warmLit: warm);
                    idx++;
                }
            }
        }

        /// <summary>
        /// Mounts one of the handcrafted Blender 5.3 Turkish balconies (ClassicIron, CamBalkon, MasonryCorner)
        /// flush onto an upper-floor building wall (+Z in FBX local space cantilevers outward from the wall).
        /// Uses GPU-instanced shared URP materials and ShadowCastingMode.Off to protect mobile 30+ FPS.
        /// </summary>
        private static GameObject MountBlenderBalcony(
            Transform parent,
            string fbxFileName,
            string instanceName,
            Vector3 localPos,
            Quaternion localRot,
            MaterialSet mats,
            float uniformScale = 1f)
        {
            GameObject go = InstantiateBlenderModel(fbxFileName, instanceName, parent, mats);
            if (go == null) return null;
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = Vector3.one * uniformScale;
            DisableShadowCasting(go);
            MarkStaticEnvironment(go);
            return go;
        }

        private static void ApplyAutomotiveClearcoat(Material mat)
        {
            if (mat == null) return;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.86f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", Mathf.Max(0.38f, mat.GetFloat("_Metallic")));
            if (mat.HasProperty("_ClearCoat")) mat.SetFloat("_ClearCoat", 1.0f);
            if (mat.HasProperty("_ClearCoatMask")) mat.SetFloat("_ClearCoatMask", 0.90f);
            if (mat.HasProperty("_ClearCoatSmoothness")) mat.SetFloat("_ClearCoatSmoothness", 0.96f);
            mat.EnableKeyword("_CLEARCOAT");
            EditorUtility.SetDirty(mat);
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
            Pickup,
            SportsCoupe,
            SUV,
            DeliveryVan
        }

        private static MaterialSet _activeMaterialSet;

        private class PrefabSet
        {
            public GameObject Player;
            public GameObject PlayerSedan;
            public GameObject TownCarRedHatchback;
            public GameObject TownCarBlueHatchback;
            public GameObject TownCarWhitePickup;
            public GameObject TownCarGreenSedan;
            public GameObject TownCarYellowSportsCoupe;
            public GameObject TownCarBlueSportsCoupe;
            public GameObject TownCarCharcoalSUV;
            public GameObject TownCarGreenSUV;
            public GameObject TownCarWhiteVan;
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
                TownCarBlueHatchback = BuildVehicleVariantPrefab("Vehicle_TownHatchback_Blue", string.Empty, mats.CarSkyBlue, InteractableKind.TownVehicle, VehicleBodyVariant.Hatchback, mats),
                TownCarWhitePickup = BuildVehicleVariantPrefab("Vehicle_TownPickup_White", string.Empty, mats.CarWhite, InteractableKind.TownVehicle, VehicleBodyVariant.Pickup, mats),
                TownCarGreenSedan = BuildSedanPrefab("Vehicle_TownSedan_Green", string.Empty, mats.CarDarkGreen, InteractableKind.TownVehicle),
                TownCarYellowSportsCoupe = BuildVehicleVariantPrefab("Vehicle_SportsCoupe_Yellow", string.Empty, mats.CarYellow, InteractableKind.TownVehicle, VehicleBodyVariant.SportsCoupe, mats),
                TownCarBlueSportsCoupe = BuildVehicleVariantPrefab("Vehicle_SportsCoupe_Blue", string.Empty, mats.CarSkyBlue, InteractableKind.TownVehicle, VehicleBodyVariant.SportsCoupe, mats),
                TownCarCharcoalSUV = BuildVehicleVariantPrefab("Vehicle_SUV_Charcoal", string.Empty, mats.CarCharcoal, InteractableKind.TownVehicle, VehicleBodyVariant.SUV, mats),
                TownCarGreenSUV = BuildVehicleVariantPrefab("Vehicle_SUV_Green", string.Empty, mats.CarDarkGreen, InteractableKind.TownVehicle, VehicleBodyVariant.SUV, mats),
                TownCarWhiteVan = BuildVehicleVariantPrefab("Vehicle_DeliveryVan_White", string.Empty, mats.CarVanCream, InteractableKind.TownVehicle, VehicleBodyVariant.DeliveryVan, mats),
                NpcMechanic = BuildNpcPrefab("NPC_Mechanic", "npc_mechanic", mats, mats.MechanicSuit, mats.CharPantsDenim, BlenderCharacterVariant.Worker, mats.CharSkinMedium, mats.CharHair),
                NpcHarborReceiver = BuildNpcPrefab("NPC_HarborReceiver", "npc_harbor_receiver", mats, mats.ReceiverSuit, mats.CharPants, BlenderCharacterVariant.CasualFemale, mats.CharSkin, mats.CharHairBrown),
                ApartmentBuilding = BuildApartmentPrefab(mats),
                TrafficVehicle = BuildTrafficPlaceholderPrefab(mats),
                ConcreteBarrier = BuildFbxPropPrefab("concrete_road_barrier", "Prop_ConcreteBarrier_Placeholder", 1.05f, addCollider: true, isStatic: true, fallbackMat: mats.Concrete, explicitColliderSize: new Vector3(1.90f, 1.05f, 0.68f)),
                StreetLamp = BuildBlenderPropPrefab("Street_Lamp.fbx", "Prop_StreetLamp_CC0", mats, useCapsulePoleCollider: true, colliderSize: new Vector3(0.32f, 5.2f, 0.32f), isStatic: true, addNightLampGlow: true),
                WoodenCrate = BuildFbxPropPrefab("wooden_crate_01", "Prop_WoodenCrate_CC0", 0.90f, addCollider: true, isStatic: true, fallbackMat: mats.PlasterTrim, explicitColliderSize: new Vector3(0.95f, 0.90f, 0.85f)),
                OilBarrel = BuildFbxPropPrefab("Barrel_01", "Prop_OilBarrel_CC0", 0.95f, addCollider: true, isStatic: true, fallbackMat: mats.ContainerRed, explicitColliderSize: new Vector3(0.62f, 0.95f, 0.62f)),
                FireHydrant = BuildFbxPropPrefab("fire_hydrant", "Prop_FireHydrant_CC0", 0.85f, addCollider: true, isStatic: true, fallbackMat: mats.ContainerRed, explicitColliderSize: new Vector3(0.42f, 0.85f, 0.42f)),
                TrashCan = BuildBlenderPropPrefab("Street_TrashCan.fbx", "Prop_TrashCan_CC0", mats, useCapsulePoleCollider: false, colliderSize: new Vector3(0.48f, 0.88f, 0.48f), isStatic: true),
                ToolCart = BuildFbxPropPrefab("tool_cart", "Prop_ToolCart_CC0", 1.02f, addCollider: true, isStatic: true, fallbackMat: mats.ContainerRed, explicitColliderSize: new Vector3(0.95f, 1.02f, 0.65f)),
                OldTyre = BuildFbxPropPrefab("old_tyre", "Prop_OldTyre_CC0", 0.30f, addCollider: false, isStatic: true, fallbackMat: mats.WheelRubber),
                FloodLights = BuildBlenderPropPrefab("Work_FloodLight.fbx", "Prop_FloodLights_CC0", mats, useCapsulePoleCollider: true, colliderSize: new Vector3(0.56f, 2.12f, 0.56f), isStatic: true),
                OceanBuoy = BuildFbxPropPrefab("ocean_buoy", "Prop_OceanBuoy_CC0", 2.8f, addCollider: false, isStatic: false, fallbackMat: mats.ContainerRed),
                RollerShutterDoor = BuildFbxPropPrefab("rollershutter_door", "Prop_RollerShutter_CC0", 3.6f, addCollider: false, isStatic: true, fallbackMat: mats.GarageRoofMetal),
                WoodenBench = BuildBlenderPropPrefab("Town_ParkBench.fbx", "Prop_WoodenBench_CC0", mats, useCapsulePoleCollider: false, colliderSize: new Vector3(1.88f, 0.88f, 0.66f), isStatic: true)
            };
        }

        private static GameObject BuildBlenderPropPrefab(
            string fbxFileName,
            string prefabName,
            MaterialSet mats,
            bool useCapsulePoleCollider,
            Vector3 colliderSize,
            bool isStatic,
            bool addNightLampGlow = false)
        {
            GameObject root = new GameObject(prefabName);
            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            GameObject meshInst = InstantiateBlenderModel(fbxFileName, "BlenderPropMesh", visualRoot.transform, mats);
            if (meshInst == null)
            {
                CreatePrimitiveChild("Fallback_Prop", PrimitiveType.Cube, visualRoot.transform,
                    new Vector3(0f, colliderSize.y * 0.5f, 0f), colliderSize, mats.Concrete, removeCollider: true);
            }

            if (useCapsulePoleCollider)
            {
                CapsuleCollider cap = root.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);
                cap.radius = Mathf.Max(0.14f, colliderSize.x * 0.5f);
                cap.height = Mathf.Max(0.5f, colliderSize.y);
            }
            else
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);
                box.size = colliderSize;
            }

            if (isStatic)
            {
                MarkStaticEnvironment(root);
            }

            if (addNightLampGlow)
            {
                GameObject bulbGo = CreatePrimitiveChild("Lantern_WarmBulb", PrimitiveType.Sphere, visualRoot.transform,
                    new Vector3(0f, 4.94f, 0.96f), new Vector3(0.24f, 0.14f, 0.34f), mats.LampGlow, removeCollider: true);
                MarkStaticPaintable(bulbGo);
                NightLight nl = bulbGo.AddComponent<NightLight>();
                nl.Configure(null, new[] { bulbGo.GetComponent<Renderer>() }, new Color(1.0f, 0.80f, 0.42f) * 3.0f);
            }

            return SaveAsPrefab(root, $"{PrefabDir}/{prefabName}.prefab");
        }

        private static GameObject BuildFbxPropPrefab(
            string modelId,
            string prefabName,
            float targetHeightMeters,
            bool addCollider,
            bool isStatic,
            Material fallbackMat,
            bool addLampGlow = false,
            Material lampGlowMat = null,
            Vector3? explicitColliderSize = null)
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
                if (explicitColliderSize.HasValue)
                {
                    Vector3 s = explicitColliderSize.Value;
                    box.center = new Vector3(0f, s.y * 0.5f, 0f);
                    box.size = s;
                }
                else
                {
                    // Guard against bloated third-party FBX bounds creating invisible walls
                    Vector3 clampedSize = new Vector3(
                        Mathf.Clamp(combinedBounds.size.x, 0.35f, 2.0f),
                        Mathf.Clamp(combinedBounds.size.y, 0.35f, targetHeightMeters + 0.5f),
                        Mathf.Clamp(combinedBounds.size.z, 0.35f, 2.0f));
                    box.center = new Vector3(0f, clampedSize.y * 0.5f, 0f);
                    box.size = clampedSize;
                }
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
                Debug.LogWarning($"[OurWorld] Blender FBX not found at '{fbxPath}' — falling back to procedural placeholder.");
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
            bool isBuildingMesh = fbxFileName.StartsWith("Sultanbeyli_") || fbxFileName.StartsWith("Hamidiye_");
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                Material[] srcMats = r.sharedMaterials;
                Material[] mapped = new Material[srcMats.Length];
                for (int i = 0; i < srcMats.Length; i++)
                {
                    string slotName = srcMats[i] != null ? srcMats[i].name : string.Empty;
                    mapped[i] = ResolveBlenderUrpMaterial(slotName, mats, bodyPaintOverride, foliageAOverride, foliageBOverride, isBuildingMesh);
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
            Material foliageBOverride,
            bool isBuildingMesh = false)
        {
            if (string.IsNullOrEmpty(slotName)) return mats.Concrete;

            if (slotName.StartsWith("BodyPaint")) return bodyPaintOverride != null ? bodyPaintOverride : mats.SedanBody;
            if (slotName.StartsWith("ContainerPaint")) return bodyPaintOverride != null ? bodyPaintOverride : mats.ContainerRed;
            if (slotName.StartsWith("TrimDark") || slotName.StartsWith("InteriorDark")) return mats.SedanTrimDark;
            if (slotName.StartsWith("Chrome")) return mats.SedanChrome;
            if (slotName.StartsWith("Glass")) return (isBuildingMesh && mats.WindowGlassBlue != null) ? mats.WindowGlassBlue : mats.SedanGlass;
            if (slotName.StartsWith("Headlight")) return mats.LampGlow;
            if (slotName.StartsWith("Taillight")) return mats.TaillightGlow;
            if (slotName.StartsWith("ReverseLight")) return mats.ReverseLightOff;
            if (slotName.StartsWith("WheelRubber")) return mats.WheelRubber;
            if (slotName.StartsWith("BrakeCaliper")) return mats.BrakeCaliper;
            if (slotName.StartsWith("Bark")) return mats.TownBark;
            if (slotName.StartsWith("FoliageA")) return foliageAOverride != null ? foliageAOverride : mats.TownFoliageA;
            if (slotName.StartsWith("FoliageB")) return foliageBOverride != null ? foliageBOverride : mats.TownFoliageB;
            if (slotName.StartsWith("StoneTrim")) return mats.TravertineStone != null ? mats.TravertineStone : mats.PlasterTrim;
            if (slotName.StartsWith("Concrete")) return mats.Concrete;
            if (slotName.StartsWith("Wood")) return mats.TownWood;
            if (slotName.StartsWith("Water")) return mats.PoolWaterClear != null ? mats.PoolWaterClear : mats.SeaWater;
            if (slotName.StartsWith("LeadRoof")) return mats.IstanbulLeadRoof != null ? mats.IstanbulLeadRoof : mats.GarageRoofMetal;
            if (slotName.StartsWith("BridgeSteel")) return mats.IstanbulBridgeSteel != null ? mats.IstanbulBridgeSteel : mats.PlasterTrim;
            if (slotName.StartsWith("TramRed")) return mats.IstanbulTramRed != null ? mats.IstanbulTramRed : mats.CarRed;
            if (slotName.StartsWith("CreamPaint")) return mats.TownInteriorPaintCream;
            if (slotName.StartsWith("GoldBrass")) return mats.IstanbulGoldBrass != null ? mats.IstanbulGoldBrass : mats.RoadMarkingYellow;
            if (slotName.StartsWith("FerryHull")) return mats.IstanbulFerryHull != null ? mats.IstanbulFerryHull : mats.SedanTrimDark;
            if (slotName.StartsWith("SimitGold")) return mats.TownFabricMustard;
            if (slotName.StartsWith("RoofTile")) return mats.TownRoofTile;
            if (slotName.StartsWith("RedPlaster")) return mats.RedPlaster != null ? mats.RedPlaster : mats.TownBrickHouse;
            if (slotName.StartsWith("SagePlaster")) return mats.SagePlaster != null ? mats.SagePlaster : mats.PlasterTrim;
            if (slotName.StartsWith("YellowPlaster")) return mats.YellowPlaster != null ? mats.YellowPlaster : mats.TownInteriorPaintCream;
            if (slotName.StartsWith("OrangeStripe")) return mats.OrangeStripe != null ? mats.OrangeStripe : mats.ContainerOchre;
            if (slotName.StartsWith("OliveFacade")) return mats.OliveFacade != null ? mats.OliveFacade : mats.PedShirtOlive;
            if (slotName.StartsWith("RawBrick")) return mats.RawBrick != null ? mats.RawBrick : mats.TownBrickHouse;
            if (slotName.StartsWith("FenceBlue")) return mats.FenceBlue != null ? mats.FenceBlue : mats.ContainerBlue;
            if (slotName.StartsWith("WhitePVC")) return mats.WhitePVC != null ? mats.WhitePVC : mats.CharShoeSole;
            if (slotName.StartsWith("DarkInterior")) return mats.DarkInterior != null ? mats.DarkInterior : mats.SedanTrimDark;
            if (slotName.StartsWith("RustSteel")) return mats.RustSteel != null ? mats.RustSteel : mats.TownBark;
            if (slotName.StartsWith("VineBrown")) return mats.VineBrown != null ? mats.VineBrown : mats.TownBark;
            if (slotName.StartsWith("ShrubGreen")) return mats.TownHedge;
            if (slotName.StartsWith("DoorTeal")) return mats.DoorTeal != null ? mats.DoorTeal : mats.TownFabricTeal;
            if (slotName.StartsWith("PeachPlaster")) return mats.PeachPlaster != null ? mats.PeachPlaster : mats.PlasterTrim;
            if (slotName.StartsWith("CoralPlaster")) return mats.CoralPlaster != null ? mats.CoralPlaster : mats.RedPlaster;
            if (slotName.StartsWith("PinkPlaster")) return mats.PinkPlaster != null ? mats.PinkPlaster : mats.PlasterTrim;
            if (slotName.StartsWith("PowderBlue")) return mats.PowderBlue != null ? mats.PowderBlue : mats.FenceBlue;
            if (slotName.StartsWith("MeshGreen")) return mats.MeshGreen != null ? mats.MeshGreen : mats.TownHedge;
            if (slotName.StartsWith("CobaltBlue")) return mats.CobaltBlue != null ? mats.CobaltBlue : mats.ContainerBlue;
            if (slotName.StartsWith("LilacGate")) return mats.LilacGate != null ? mats.LilacGate : mats.PlasterTrim;
            if (slotName.StartsWith("TurquoiseRust")) return mats.TurquoiseRust != null ? mats.TurquoiseRust : mats.DoorTeal;
            if (slotName.StartsWith("Galvanized")) return mats.Galvanized != null ? mats.Galvanized : mats.GarageRoofMetal;
            if (slotName.StartsWith("SignRed")) return mats.SignRed != null ? mats.SignRed : mats.CarRed;
            if (slotName.StartsWith("DarkWood")) return mats.TownDoorDark;
            if (slotName.StartsWith("AccentTeal")) return mats.TownFabricTeal;
            if (slotName.StartsWith("AccentMustard")) return mats.TownFabricMustard;
            if (slotName.StartsWith("AccentBlue")) return mats.ContainerBlue;
            if (slotName.StartsWith("LitScreen")) return mats.WindowLitWarm;
            if (slotName.StartsWith("CanRed")) return mats.CanRed;
            if (slotName.StartsWith("CanBlue")) return mats.CanBlue;
            if (slotName.StartsWith("CanGreen")) return mats.CanGreen;
            if (slotName.StartsWith("CanOrange")) return mats.CanOrange;
            if (slotName.StartsWith("CatFur")) return mats.TownFabricMustard;
            if (slotName.StartsWith("Terracotta")) return mats.TownAwningTerracotta;

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
                Debug.LogWarning($"[OurWorld] Character FBX not found at '{fbxPath}' — falling back to primitive humanoid.");
                return ProceduralHumanoid.Build(parent, useSkin, useShirt, usePants, mats.CharShoes, useHair, height);
            }

            // Cache child sub-part templates by exact name inside the flat FBX asset
            Dictionary<string, Transform> partTemplates = new Dictionary<string, Transform>();
            foreach (Transform child in fbxAsset.transform)
            {
                partTemplates[child.name] = child;
            }

            bool isFemale = variant == BlenderCharacterVariant.CasualFemale;
            float shldrX = isFemale ? 0.174f : 0.200f;
            float hipX = isFemale ? 0.102f : 0.106f;

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
            bool isSports = variant == VehicleBodyVariant.SportsCoupe;
            bool isSuv = variant == VehicleBodyVariant.SUV;
            bool isVan = variant == VehicleBodyVariant.DeliveryVan;

            float massKg = isHatchback ? 1150f
                : (isSports ? 1220f
                : (isPickup ? 1800f
                : (isSuv ? 1950f
                : (isVan ? 2150f : 1350f))));
            float bodyLength = isHatchback ? 3.90f
                : (isSports ? 4.36f
                : (isPickup ? 5.00f
                : (isSuv ? 4.62f
                : (isVan ? 5.04f : 4.42f))));
            float bodyWidth = isHatchback ? 1.80f
                : (isSports ? 1.96f
                : (isPickup ? 1.94f
                : (isSuv ? 1.98f
                : (isVan ? 1.98f : 1.88f))));
            float halfLen = bodyLength * 0.5f;

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
            float colHeight = isVan ? 1.48f : (isSuv ? 1.24f : (isPickup ? 1.05f : (isSports ? 0.84f : 0.95f)));
            float colCenterY = isVan ? 0.98f : (isSuv ? 0.88f : (isPickup ? 0.78f : (isSports ? 0.64f : 0.72f)));
            chassisBox.center = new Vector3(0f, colCenterY, 0f);
            chassisBox.size = new Vector3(bodyWidth + 0.02f, colHeight, bodyLength + 0.04f);

            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            float frontAxleZ = isHatchback ? 1.22f
                : (isSports ? 1.34f
                : (isPickup ? 1.56f
                : (isSuv ? 1.42f
                : (isVan ? 1.58f : 1.38f))));
            float rearAxleZ = isHatchback ? -1.22f
                : (isSports ? -1.30f
                : (isPickup ? -1.56f
                : (isSuv ? -1.38f
                : (isVan ? -1.56f : -1.32f))));
            float wheelX = isHatchback ? 0.80f
                : ((isSports || isPickup) ? 0.87f
                : ((isSuv || isVan) ? 0.88f : 0.84f));
            float frontZ = halfLen + 0.02f;

            string carFbx = isPickup ? "Car_Pickup.fbx"
                : (isHatchback ? "Car_Hatchback.fbx"
                : (isSports ? "Car_SportsCoupe.fbx"
                : (isSuv ? "Car_SUV.fbx"
                : (isVan ? "Car_DeliveryVan.fbx" : "Car_Sedan.fbx"))));
            GameObject blenderCar = InstantiateBlenderModel(carFbx, "BlenderCarMesh", visualRoot.transform, mats, bodyPaintOverride: bodyMat);
            if (blenderCar == null)
            {
                // Fallback primitive body if FBX hasn't imported yet
                CreatePrimitiveChild("Placeholder_LowerBody", PrimitiveType.Cube, visualRoot.transform,
                    new Vector3(0f, 0.62f, 0f), new Vector3(bodyWidth, 0.56f, bodyLength), bodyMat, removeCollider: true);
                CreatePrimitiveChild("Placeholder_CabinRoof", PrimitiveType.Cube, visualRoot.transform,
                    new Vector3(0f, 1.28f, -0.20f), new Vector3(bodyWidth * 0.82f, 0.42f, bodyLength * 0.42f), mats.SedanGlass, removeCollider: true);
            }

            float driverSeatY = isVan ? 0.68f : (isSuv ? 0.60f : (isPickup ? 0.52f : (isSports ? 0.34f : 0.45f)));
            float driverSeatZ = isVan ? 0.90f : (isPickup ? 0.12f : (isSports ? -0.32f : -0.25f));

            GameObject steeringWheel = new GameObject("SteeringWheel");
            steeringWheel.transform.SetParent(visualRoot.transform, false);
            steeringWheel.transform.localPosition = new Vector3(-0.38f, driverSeatY + 0.54f, driverSeatZ + 0.58f);
            steeringWheel.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            CreatePrimitiveChild("SteeringWheelRim", PrimitiveType.Cylinder, steeringWheel.transform,
                Vector3.zero, new Vector3(0.34f, 0.025f, 0.34f), mats.SedanTrimDark, removeCollider: true);
            CreatePrimitiveChild("SteeringWheelSpoke", PrimitiveType.Cube, steeringWheel.transform,
                Vector3.zero, new Vector3(0.30f, 0.03f, 0.045f), mats.SedanChrome, removeCollider: true);

            GameObject seatedDriverRoot = new GameObject("SeatedDriverRoot");
            seatedDriverRoot.transform.SetParent(visualRoot.transform, false);
            seatedDriverRoot.transform.localPosition = new Vector3(-0.38f, driverSeatY, driverSeatZ);
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
            beamObj.transform.localPosition = new Vector3(0f, isSuv ? 0.88f : 0.76f, frontZ + 0.04f);
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

            float wheelRadius = isSuv ? 0.38f : (isPickup ? 0.37f : (isVan ? 0.36f : (isHatchback ? 0.32f : 0.34f)));
            float suspDist = isSuv ? 0.30f : (isPickup ? 0.28f : (isSports ? 0.18f : (isHatchback ? 0.21f : 0.22f)));
            float springRate = isSports ? 42000f : (isVan ? 38000f : (isSuv ? 30000f : (isPickup ? 25000f : (isHatchback ? 32000f : 35000f))));
            float damperRate = isSports ? 5200f : (isPickup ? 3300f : (isHatchback ? 4200f : 4500f));
            float wheelY = isSuv ? 0.45f : (isPickup ? 0.44f : (isVan ? 0.42f : (isSports ? 0.38f : 0.40f)));

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

            bool frontMotor = isHatchback || isSuv;
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
                antiRoll: isSports ? 5400f : (isPickup ? 3000f : (isSuv ? 3400f : 4200f)),
                comOffset: isSports ? new Vector3(0f, -0.46f, 0.04f) : ((isPickup || isSuv) ? new Vector3(0f, -0.38f, 0.12f) : new Vector3(0f, -0.42f, 0.08f)),
                maxBrake: (isPickup || isSuv || isVan) ? 3800f : (isSports ? 4000f : 3200f),
                finalDrive: isSports ? 4.45f : (isHatchback ? 4.1f : (isVan ? 3.7f : 3.9f)));

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
            m01.rewardMoney = 150;
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
            m02.rewardMoney = 250;
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
            m03.rewardMoney = 220;
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
            m04.rewardMoney = 350;
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
            m05.rewardMoney = 500;
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
            AppendChapterTwoMissions(catalog);   // Missions 6-10 (Bölüm 2)
            AppendChapterThreeMissions(catalog); // Missions 11-14 (Bölüm 3: Hamidiye'nin Kalbi)
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

            // --- Realistic Natural Daylight Skybox, Trilight Ambient Bounce, Soft Shadows & Atmospheric Depth ---
            RenderSettings.skybox = mats.EveningSkybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.76f, 0.74f, 0.70f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.35f, 0.30f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1.15f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.79f, 0.87f);
            RenderSettings.fogDensity = 0.0020f;

            GameObject sunObj = new GameObject("Sun_CoastalDirectional");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.95f, 0.86f);
            sun.intensity = 1.95f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.64f;
            sunObj.transform.rotation = Quaternion.Euler(36f, -38f, 0f);
            RenderSettings.sun = sun;

            // Sultanbeyli / Hamidiye Mahallesi Environment Reflection Probe
            GameObject probeObj = new GameObject("Town_EnvironmentReflectionProbe");
            probeObj.transform.position = new Vector3(0f, 18f, 0f);
            ReflectionProbe envProbe = probeObj.AddComponent<ReflectionProbe>();
            envProbe.mode = ReflectionProbeMode.Realtime;
            envProbe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            envProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            envProbe.resolution = 256;
            envProbe.size = new Vector3(360f, 90f, 360f);
            envProbe.intensity = 1.15f;
            envProbe.hdr = true;

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
            systems.AddComponent<StreetNameTracker>();

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

            // --- SULTANBEYLİ / HAMİDİYE MAHALLESİ SOLID BEDROCK FOUNDATION (ZERO ABYSS!) ---
            GameObject env = new GameObject("--- WORLD_ENVIRONMENT ---");

            CreateTiledBox(
                "Ground_SultanbeyliBedrock",
                env.transform,
                new Vector3(0f, -3.0f, 0f),
                new Vector3(820f, 6.0f, 820f),
                Quaternion.identity,
                mats.TownGrass,
                metersPerTile: 3.5f,
                collider: true);

            // --- AUTHENTIC REAL-OSM SULTANBEYLİ · SELMAN SOKAĞI & HAMİDİYE MAHALLESİ ---
            List<TrafficStopLine> stopLines = BuildSultanbeyliSelmanSokagiWorld(env.transform, mats, prefabs);

            // Render top-down orthographic TownMap.png from the completed environment before adding dynamic actors
            Texture2D townMapTexture = RenderAndSaveTownMapTexture(sun);

            // --- WORLD ANCHORS & SPAWNS (Player spawns right in front of 48 Selman Sokağı matching the Street View angle!) ---
            GameObject anchorsRoot = new GameObject("--- WORLD_ANCHORS ---");
            CreateWorldAnchor("Spawn_GarageFoot", "spawn_garage_foot", anchorsRoot.transform, new Vector3(-68.0f, 0.12f, -2.2f), Quaternion.identity, 2f);
            CreateWorldAnchor("Spawn_GarageSedan", "spawn_garage_sedan", anchorsRoot.transform, new Vector3(-56.5f, 0.28f, -1.8f), Quaternion.Euler(0f, 90f, 0f), 3.5f);
            CreateWorldAnchor("Harbor_DeliveryZone", "harbor_delivery_zone", anchorsRoot.transform, new Vector3(46.0f, 0.10f, 0.0f), Quaternion.identity, 7.5f);
            CreateWorldAnchor("MissingCar_SearchZone", "missing_car_zone", anchorsRoot.transform, new Vector3(-10.5f, 0.10f, -12.0f), Quaternion.identity, 14f);
            CreateWorldAnchor("Tail_StartLookoutZone", "tail_start_zone", anchorsRoot.transform, new Vector3(12.0f, 0.10f, 0.0f), Quaternion.Euler(0f, 90f, 0f), 11f);
            CreateWorldAnchor("Spawn_HarborTailStart", "spawn_harbor_tail_start", anchorsRoot.transform, new Vector3(6.0f, 0.28f, -1.6f), Quaternion.Euler(0f, 90f, 0f), 4f);
            CreateWorldAnchor("Neighborhood_ReturnZone", "neighborhood_return_zone", anchorsRoot.transform, new Vector3(-65.0f, 0.10f, 0.0f), Quaternion.identity, 16f);

            // --- ACTORS (Player facing 48 Selman Sokağı, Sedan on street, Marco at No:73 Oto Tamir, Elena at No:66) ---
            GameObject actorsRoot = new GameObject("--- ACTORS ---");
            GameObject playerObj = PlacePrefabInstance(prefabs.Player, actorsRoot.transform, new Vector3(-68.0f, 0.12f, -2.2f), Quaternion.identity);
            GameObject sedanObj = PlacePrefabInstance(prefabs.PlayerSedan, actorsRoot.transform, new Vector3(-56.5f, 0.28f, -1.8f), Quaternion.Euler(0f, 90f, 0f));
            PlacePrefabInstance(prefabs.NpcMechanic, actorsRoot.transform, new Vector3(24.5f, 0.04f, 5.2f), Quaternion.Euler(0f, 180f, 0f));
            PlacePrefabInstance(prefabs.NpcHarborReceiver, actorsRoot.transform, new Vector3(46.0f, 0.04f, -5.0f), Quaternion.identity);

            // Mission 3: Missing Dark Sedan parked in Çiçekçi Sokağı mouth
            GameObject missingCarObj = PlacePrefabInstance(prefabs.TrafficVehicle, actorsRoot.transform, new Vector3(-10.5f, 0.04f, -14.0f), Quaternion.identity);
            missingCarObj.name = "Mission03_MissingDarkSedan";
            WorldAnchor missingCarAnchor = missingCarObj.AddComponent<WorldAnchor>();
            missingCarAnchor.Configure("missing_car_clue", 4.2f, new Vector3(0f, 2.2f, 0f));
            InteractableTrigger missingCarTrig = missingCarObj.AddComponent<InteractableTrigger>();
            missingCarTrig.Configure(InteractableKind.InspectObject, "missing_car_clue", "ui.btn.inspect", 4.2f);

            // Mission 4: Courier Tail Vehicle cruising along Selman Sokağı
            GameObject tailCarObj = PlacePrefabInstance(prefabs.TrafficVehicle, actorsRoot.transform, new Vector3(22.0f, 0.04f, -1.6f), Quaternion.Euler(0f, 90f, 0f));
            tailCarObj.name = "Mission04_TailCourierCar";
            WorldAnchor tailCarAnchor = tailCarObj.AddComponent<WorldAnchor>();
            tailCarAnchor.Configure("tail_target_car", 5.0f, new Vector3(0f, 2.4f, 0f));

            GameObject droppedPkgObj = PlacePrefabInstance(prefabs.WoodenCrate, actorsRoot.transform, new Vector3(82.0f, 0.06f, 4.8f), Quaternion.Euler(0f, 25f, 0f));
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
                new Vector3(22.0f, 0.04f, -1.6f),
                new Vector3(76.0f, 0.04f, -1.6f),
                new Vector3(76.0f, 0.04f,  1.6f),
                new Vector3(-72.0f, 0.04f, 1.6f),
                new Vector3(-72.0f, 0.04f, -1.6f),
                new Vector3(78.0f, 0.04f, -1.6f)
            };
            tailCtrl.Configure(tailRoute, droppedPkgObj);

            // Mission 5: Elena waits outside 66 Selman Sokağı
            GameObject elenaSquareObj = PlacePrefabInstance(prefabs.NpcHarborReceiver, actorsRoot.transform, new Vector3(48.5f, 0.04f, 4.8f), Quaternion.Euler(0f, 180f, 0f));
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

            // --- Pooled Ambient Traffic cruising across ALL of Hamidiye Mahallesi (Selman Sk. + Main Avenues Grand Loop!) ---
            GameObject trafficRoot = new GameObject("AmbientTrafficManager");
            AmbientTrafficManager trafficMgr = trafficRoot.AddComponent<AmbientTrafficManager>();
            List<Transform> waypoints = new List<Transform>();
            Vector3[] wpCoords =
            {
                new Vector3(-83.5f, 0.05f,  -1.2f), // Selman Sk. West -> East
                new Vector3(  0.0f, 0.05f,  -1.2f), // Selman Bakkal front
                new Vector3( 97.5f, 0.05f,  -1.2f), // Abdülkadir Udeh corner
                new Vector3( 97.5f, 0.05f,  46.0f), // North to Mürşit Cd.
                new Vector3( 97.5f, 0.05f, 136.0f), // North to Fatih Bulvarı South Lane
                new Vector3(  0.0f, 0.05f, 136.0f), // Fatih Bulvarı · Saat Kulesi
                new Vector3(-86.5f, 0.05f, 136.0f), // West to Seyyid Kutup Cd.
                new Vector3(-86.5f, 0.05f,  46.0f), // South along Seyyid Kutup Cd.
                new Vector3(-86.5f, 0.05f, -79.5f), // South to Petrol Yolu Cd.
                new Vector3(  0.0f, 0.05f, -79.5f), // East along Petrol Yolu Cd.
                new Vector3(100.5f, 0.05f, -79.5f), // East to Abdülkadir Udeh
                new Vector3(100.5f, 0.05f,   1.2f), // North back to Selman Sk.
                new Vector3(  0.0f, 0.05f,   1.2f), // Selman Sk. East -> West
                new Vector3(-83.5f, 0.05f,   1.2f)
            };
            for (int i = 0; i < wpCoords.Length; i++)
            {
                GameObject wp = new GameObject($"WP_{i:00}");
                wp.transform.SetParent(trafficRoot.transform, false);
                wp.transform.position = wpCoords[i];
                waypoints.Add(wp.transform);
            }
            trafficMgr.Configure(prefabs.TrafficVehicle, waypoints, stopLines, prefabs.TownCarGreenSedan);

            // --- Calm Modern HUD, Minimap, Shop Panel, Paint Palette & Phone/Map ---
            BuildHudCanvas(uiSprites, townMapTexture, needsController);

            ValidateBuiltScene(scene, catalog);
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.SaveScene(scene, TestScenePath);
            RegisterSceneInBuildSettings(TestScenePath);
        }

        private static void ValidateBuiltScene(Scene scene, MissionCatalog catalog)
        {
            int issues = 0;

            // 1. Check for MISSING scripts across the entire scene hierarchy
            int missingScripts = 0;
            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                Transform[] all = rootObj.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    int c = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(all[i].gameObject);
                    if (c > 0)
                    {
                        missingScripts += c;
                        Debug.LogError($"[OurWorld Validate] MISSING script ({c}) on '{all[i].name}'!");
                    }
                }
            }
            issues += missingScripts;

            // 2. Verify singleton managers are present exactly once (no duplicate controllers across rebuilds)
            void CheckSingleton<T>(string label) where T : Component
            {
                T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
                if (found.Length != 1)
                {
                    issues++;
                    Debug.LogError($"[OurWorld Validate] Expected 1 {label}, found {found.Length}!");
                }
            }
            CheckSingleton<SaveManager>("SaveManager");
            CheckSingleton<PlayerModeController>("PlayerModeController");
            CheckSingleton<JobManager>("JobManager");
            CheckSingleton<MissionManager>("MissionManager");
            CheckSingleton<DayNightCycle>("DayNightCycle");
            CheckSingleton<GameHUDController>("GameHUDController");

            // 3. Check duplicate WorldAnchor IDs & validate every MissionCatalog step anchor exists!
            WorldAnchor[] anchors = Object.FindObjectsByType<WorldAnchor>(FindObjectsInactive.Include);
            HashSet<string> anchorIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] == null || string.IsNullOrEmpty(anchors[i].AnchorId)) continue;
                if (!anchorIds.Add(anchors[i].AnchorId))
                {
                    issues++;
                    Debug.LogError($"[OurWorld Validate] Duplicate WorldAnchor ID '{anchors[i].AnchorId}' on '{anchors[i].name}'!");
                }
            }
            if (catalog != null && catalog.missions != null)
            {
                foreach (MissionData m in catalog.missions)
                {
                    if (m == null || m.steps == null) continue;
                    foreach (MissionStepData step in m.steps)
                    {
                        if (!string.IsNullOrEmpty(step.targetAnchorId) && !anchorIds.Contains(step.targetAnchorId))
                        {
                            issues++;
                            Debug.LogError($"[OurWorld Validate] Mission '{m.missionId}' targetAnchorId '{step.targetAnchorId}' not found in scene!");
                        }
                        if (!string.IsNullOrEmpty(step.secondaryTargetAnchorId) && !anchorIds.Contains(step.secondaryTargetAnchorId))
                        {
                            issues++;
                            Debug.LogError($"[OurWorld Validate] Mission '{m.missionId}' secondaryTargetAnchorId '{step.secondaryTargetAnchorId}' not found in scene!");
                        }
                    }
                }
            }

            // 4. Verify JobBoards, PaintableHouses, and FuelPumps are inside Selman Sokağı playable bounds
            JobBoardPoint[] boards = Object.FindObjectsByType<JobBoardPoint>(FindObjectsInactive.Include);
            if (boards.Length != 4)
            {
                issues++;
                Debug.LogWarning($"[OurWorld Validate] Expected 4 JobBoardPoints, found {boards.Length}.");
            }
            PaintableHouse[] houses = Object.FindObjectsByType<PaintableHouse>(FindObjectsInactive.Include);
            if (houses.Length < 5)
            {
                issues++;
                Debug.LogWarning($"[OurWorld Validate] Expected >= 5 PaintableHouses for Pizza & Painting variety, found {houses.Length}.");
            }
            StreetFootball[] balls = Object.FindObjectsByType<StreetFootball>(FindObjectsInactive.Include);
            AtmTerminalInteractable[] atms = Object.FindObjectsByType<AtmTerminalInteractable>(FindObjectsInactive.Include);
            MosquePrayerInteractable[] mosqueSpots = Object.FindObjectsByType<MosquePrayerInteractable>(FindObjectsInactive.Include);

            Debug.Log($"[OurWorld Validate] Scene audit complete — Issues: {issues} | Anchors: {anchorIds.Count} | JobBoards: {boards.Length} | PaintableHouses: {houses.Length} | Footballs: {balls.Length} | ATMs: {atms.Length} | MosqueSpots: {mosqueSpots.Length}");
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
            bool foundMain = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                EditorBuildSettingsScene s = scenes[i];
                if (s == null) continue;
                if (s.path == scenePath)
                {
                    s.enabled = true;
                    foundMain = true;
                }
                else if (s.path == "Assets/Scenes/SampleScene.unity")
                {
                    // Prevent Unity's default template SampleScene from ever shipping as a build entry scene
                    s.enabled = false;
                }
            }

            if (!foundMain)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            }
            else
            {
                // Ensure GRIDROAM main scene is always at build index 0
                int idx = scenes.FindIndex(s => s != null && s.path == scenePath);
                if (idx > 0)
                {
                    EditorBuildSettingsScene mainScene = scenes[idx];
                    scenes.RemoveAt(idx);
                    scenes.Insert(0, mainScene);
                }
            }

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
