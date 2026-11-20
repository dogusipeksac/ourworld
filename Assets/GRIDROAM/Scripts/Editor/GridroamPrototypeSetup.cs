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
    /// - Creates shared Mobile URP Low/Medium profiles
    /// - Imports ambientCG Asphalt 012 1K maps and builds URP materials
    /// - Generates placeholder Prefabs with explicit VisualRoot / WheelVisual replacement slots
    /// - Creates data-driven Mission 1 & Mission 2 ScriptableObjects
    /// - Builds the playable Evening Coastal Garage-Harbor test scene and iPhone/Android Safe-Area HUD
    /// </summary>
    public static class GridroamPrototypeSetup
    {
        private const string RootDir = "Assets/GRIDROAM";
        private const string ArtMatDir = RootDir + "/Art/Materials";
        private const string AsphaltDir = RootDir + "/Art/Textures/Asphalt012";
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
            ConfigureAsphaltTextures();

            var (lowUrp, medUrp) = CreateOrUpdateUrpProfiles();
            MaterialSet mats = CreateMaterials();
            PrefabSet prefabs = CreatePrefabs(mats);
            MissionCatalog catalog = CreateMissionDataAssets();

            BuildTestScene(mats, prefabs, catalog, lowUrp, medUrp);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GRIDROAM] Garage-Harbor prototype scene, Android+iOS settings, URP Mobile Low/Medium profiles, prefabs, and Missions 1-2 built successfully!");
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "GRIDROAM Prototype Ready (Android & iOS)",
                    "Garage-Harbor test scene, shared Android & iOS settings, URP Mobile Low/Medium profiles, placeholder prefabs, and Missions 1 & 2 are ready.\n\nPress Play to test on-foot exploration, talking to Marco, driving the sedan, and delivering to the Cargo Harbor!",
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
                RootDir, ArtMatDir, PrefabDir, DataDir, MissionDir, DialogueDir, SettingsDir, SceneDir
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

        private static void ConfigureAsphaltTextures()
        {
            string colorPath = $"{AsphaltDir}/Asphalt012_1K-JPG_Color.jpg";
            string normalPath = $"{AsphaltDir}/Asphalt012_1K-JPG_NormalGL.jpg";
            string roughPath = $"{AsphaltDir}/Asphalt012_1K-JPG_Roughness.jpg";

            ConfigureTextureImporter(colorPath, isNormal: false);
            ConfigureTextureImporter(normalPath, isNormal: true);
            ConfigureTextureImporter(roughPath, isNormal: false);
        }

        private static void ConfigureTextureImporter(string assetPath, bool isNormal)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.maxTextureSize != 1024)
            {
                importer.maxTextureSize = 1024;
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
            public Material RoadAsphalt;
            public Material Ground;
            public Material Sidewalk;
            public Material SeaWater;
            public Material HarborDock;
            public Material Garage;
            public Material BuildingA;
            public Material BuildingB;
            public Material BuildingC;
            public Material Concrete;
            public Material ContainerRed;
            public Material ContainerBlue;
            public Material PlayerSuit;
            public Material MechanicSuit;
            public Material ReceiverSuit;
            public Material SedanBody;
            public Material SedanGlass;
            public Material WheelRubber;
            public Material TrafficBody;
            public Material BeaconGlow;
            public Material LampGlow;
        }

        private static MaterialSet CreateMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            MaterialSet set = new MaterialSet
            {
                RoadAsphalt = CreateOrLoadMaterial("MAT_Road_Asphalt012", urpLit, new Color(0.75f, 0.75f, 0.78f), 0.18f),
                Ground = CreateOrLoadMaterial("MAT_Ground_Urban", urpLit, new Color(0.24f, 0.25f, 0.27f), 0.10f),
                Sidewalk = CreateOrLoadMaterial("MAT_Sidewalk", urpLit, new Color(0.46f, 0.45f, 0.44f), 0.12f),
                SeaWater = CreateOrLoadMaterial("MAT_Sea_Evening", urpLit, new Color(0.08f, 0.18f, 0.29f), 0.78f),
                HarborDock = CreateOrLoadMaterial("MAT_Harbor_Dock", urpLit, new Color(0.36f, 0.35f, 0.34f), 0.15f),
                Garage = CreateOrLoadMaterial("MAT_Garage_Walls", urpLit, new Color(0.42f, 0.39f, 0.36f), 0.20f),
                BuildingA = CreateOrLoadMaterial("MAT_Apartment_A", urpLit, new Color(0.68f, 0.56f, 0.46f), 0.15f),
                BuildingB = CreateOrLoadMaterial("MAT_Apartment_B", urpLit, new Color(0.55f, 0.59f, 0.63f), 0.18f),
                BuildingC = CreateOrLoadMaterial("MAT_Apartment_C", urpLit, new Color(0.64f, 0.48f, 0.42f), 0.15f),
                Concrete = CreateOrLoadMaterial("MAT_Concrete_Props", urpLit, new Color(0.52f, 0.52f, 0.50f), 0.12f),
                ContainerRed = CreateOrLoadMaterial("MAT_Container_Rust", urpLit, new Color(0.58f, 0.20f, 0.16f), 0.28f),
                ContainerBlue = CreateOrLoadMaterial("MAT_Container_Navy", urpLit, new Color(0.16f, 0.30f, 0.48f), 0.28f),
                PlayerSuit = CreateOrLoadMaterial("MAT_Player_Placeholder", urpLit, new Color(0.20f, 0.55f, 0.85f), 0.25f),
                MechanicSuit = CreateOrLoadMaterial("MAT_NPC_Mechanic", urpLit, new Color(0.85f, 0.48f, 0.14f), 0.25f),
                ReceiverSuit = CreateOrLoadMaterial("MAT_NPC_Receiver", urpLit, new Color(0.18f, 0.72f, 0.58f), 0.25f),
                SedanBody = CreateOrLoadMaterial("MAT_Sedan_Body", urpLit, new Color(0.78f, 0.22f, 0.18f), 0.72f, 0.35f),
                SedanGlass = CreateOrLoadMaterial("MAT_Sedan_Glass", urpLit, new Color(0.10f, 0.14f, 0.20f), 0.88f, 0.15f),
                WheelRubber = CreateOrLoadMaterial("MAT_Wheel_Rubber", urpLit, new Color(0.12f, 0.12f, 0.13f), 0.18f),
                TrafficBody = CreateOrLoadMaterial("MAT_Traffic_Placeholder", urpLit, new Color(0.32f, 0.38f, 0.46f), 0.55f),
                BeaconGlow = CreateOrLoadMaterial("MAT_Objective_Beacon", urpLit, new Color(1.0f, 0.72f, 0.15f), 0.3f, 0f, new Color(1.0f, 0.65f, 0.12f) * 1.8f),
                LampGlow = CreateOrLoadMaterial("MAT_StreetLamp_Glow", urpLit, new Color(1.0f, 0.86f, 0.55f), 0.1f, 0f, new Color(1.0f, 0.78f, 0.38f) * 2.2f)
            };

            // Bind CC0 ambientCG Asphalt 012 1K textures to Road material
            Texture2D asphaltColor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{AsphaltDir}/Asphalt012_1K-JPG_Color.jpg");
            Texture2D asphaltNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{AsphaltDir}/Asphalt012_1K-JPG_NormalGL.jpg");
            if (asphaltColor != null)
            {
                set.RoadAsphalt.SetTexture("_BaseMap", asphaltColor);
                set.RoadAsphalt.SetTextureScale("_BaseMap", new Vector2(2f, 24f));
            }

            if (asphaltNormal != null)
            {
                set.RoadAsphalt.SetTexture("_BumpMap", asphaltNormal);
                set.RoadAsphalt.SetFloat("_BumpScale", 0.85f);
                set.RoadAsphalt.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(set.RoadAsphalt);
            return set;
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
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);

            if (emission.HasValue)
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
        }

        private static PrefabSet CreatePrefabs(MaterialSet mats)
        {
            return new PrefabSet
            {
                Player = BuildPlayerPrefab(mats),
                PlayerSedan = BuildPlayerSedanPrefab(mats),
                NpcMechanic = BuildNpcPrefab("NPC_Mechanic", "npc_mechanic", mats.MechanicSuit, mats.Concrete),
                NpcHarborReceiver = BuildNpcPrefab("NPC_HarborReceiver", "npc_harbor_receiver", mats.ReceiverSuit, mats.Concrete),
                ApartmentBuilding = BuildApartmentPrefab(mats),
                TrafficVehicle = BuildTrafficPlaceholderPrefab(mats),
                ConcreteBarrier = BuildConcreteBarrierPrefab(mats)
            };
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

            GameObject body = CreatePrimitiveChild("Placeholder_Body", PrimitiveType.Capsule, visualRoot.transform,
                new Vector3(0f, 0.9f, 0f), new Vector3(0.65f, 0.9f, 0.65f), mats.PlayerSuit, removeCollider: true);
            CreatePrimitiveChild("Placeholder_Visor", PrimitiveType.Cube, body.transform,
                new Vector3(0f, 0.5f, 0.38f), new Vector3(0.5f, 0.22f, 0.28f), mats.SedanGlass, removeCollider: true);

            foot.ConfigureVisuals(visualRoot.transform, null);
            return SaveAsPrefab(root, $"{PrefabDir}/Player_GRIDROAM.prefab");
        }

        private static GameObject BuildPlayerSedanPrefab(MaterialSet mats)
        {
            GameObject root = new GameObject("Vehicle_PlayerSedan");

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 1450f;
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
            chassisBox.size = new Vector3(1.88f, 0.95f, 4.35f);

            // VisualRoot (PolyDrive FREE Sedan drop-in slot)
            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            CreatePrimitiveChild("Placeholder_LowerBody", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 0.62f, 0f), new Vector3(1.88f, 0.64f, 4.35f), mats.SedanBody, removeCollider: true);
            CreatePrimitiveChild("Placeholder_Cabin", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 1.18f, -0.22f), new Vector3(1.56f, 0.54f, 2.15f), mats.SedanGlass, removeCollider: true);
            CreatePrimitiveChild("Placeholder_HeadlightL", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(-0.62f, 0.66f, 2.18f), new Vector3(0.36f, 0.16f, 0.08f), mats.LampGlow, removeCollider: true);
            CreatePrimitiveChild("Placeholder_HeadlightR", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0.62f, 0.66f, 2.18f), new Vector3(0.36f, 0.16f, 0.08f), mats.LampGlow, removeCollider: true);

            // WheelColliders & WheelVisuals (Unity 6 official WheelCollider tutorial hierarchy)
            GameObject wcParent = new GameObject("WheelColliders");
            wcParent.transform.SetParent(root.transform, false);

            GameObject wvParent = new GameObject("WheelVisuals");
            wvParent.transform.SetParent(root.transform, false);

            WheelCollider fl = CreateWheelCollider("WheelFL", wcParent.transform, new Vector3(-0.84f, 0.42f, 1.38f));
            WheelCollider fr = CreateWheelCollider("WheelFR", wcParent.transform, new Vector3(0.84f, 0.42f, 1.38f));
            WheelCollider rl = CreateWheelCollider("WheelRL", wcParent.transform, new Vector3(-0.84f, 0.42f, -1.32f));
            WheelCollider rr = CreateWheelCollider("WheelRR", wcParent.transform, new Vector3(0.84f, 0.42f, -1.32f));

            Transform vfl = CreateWheelVisual("WheelVisual_FL", wvParent.transform, fl.transform.localPosition, mats.WheelRubber);
            Transform vfr = CreateWheelVisual("WheelVisual_FR", wvParent.transform, fr.transform.localPosition, mats.WheelRubber);
            Transform vrl = CreateWheelVisual("WheelVisual_RL", wvParent.transform, rl.transform.localPosition, mats.WheelRubber);
            Transform vrr = CreateWheelVisual("WheelVisual_RR", wvParent.transform, rr.transform.localPosition, mats.WheelRubber);

            // Safe exit markers
            GameObject exitL = new GameObject("ExitLeft");
            exitL.transform.SetParent(root.transform, false);
            exitL.transform.localPosition = new Vector3(-1.85f, 0.08f, 0.1f);

            GameObject exitR = new GameObject("ExitRight");
            exitR.transform.SetParent(root.transform, false);
            exitR.transform.localPosition = new Vector3(1.85f, 0.08f, 0.1f);

            SedanVehicleController controller = root.AddComponent<SedanVehicleController>();
            List<AxleInfo> axles = new List<AxleInfo>
            {
                new AxleInfo { leftWheel = fl, rightWheel = fr, leftWheelVisual = vfl, rightWheelVisual = vfr, motor = true, steering = true },
                new AxleInfo { leftWheel = rl, rightWheel = rr, leftWheelVisual = vrl, rightWheelVisual = vrr, motor = true, steering = false }
            };
            controller.ConfigureSetup(axles, visualRoot.transform, exitL.transform, exitR.transform);

            return SaveAsPrefab(root, $"{PrefabDir}/Vehicle_PlayerSedan.prefab");
        }

        private static WheelCollider CreateWheelCollider(string wheelName, Transform parent, Vector3 localPos)
        {
            GameObject go = new GameObject(wheelName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            WheelCollider wc = go.AddComponent<WheelCollider>();
            wc.mass = 20f;
            wc.radius = 0.36f;
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
            fwd.extremumValue = 1.25f;
            fwd.asymptoteSlip = 0.8f;
            fwd.asymptoteValue = 0.85f;
            fwd.stiffness = 1.45f;
            wc.forwardFriction = fwd;

            WheelFrictionCurve side = wc.sidewaysFriction;
            side.extremumSlip = 0.35f;
            side.extremumValue = 1.35f;
            side.asymptoteSlip = 0.65f;
            side.asymptoteValue = 0.95f;
            side.stiffness = 1.75f;
            wc.sidewaysFriction = side;

            return wc;
        }

        private static Transform CreateWheelVisual(string visualName, Transform parent, Vector3 localPos, Material tireMat)
        {
            GameObject pivot = new GameObject(visualName);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = localPos;

            GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "Placeholder_TireMesh";
            cyl.transform.SetParent(pivot.transform, false);
            cyl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            cyl.transform.localScale = new Vector3(0.72f, 0.14f, 0.72f);
            Object.DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.GetComponent<Renderer>().sharedMaterial = tireMat;

            return pivot.transform;
        }

        private static GameObject BuildNpcPrefab(string prefabName, string anchorId, Material bodyMat, Material accentMat)
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

            CreatePrimitiveChild("Placeholder_Body", PrimitiveType.Capsule, visualRoot.transform,
                new Vector3(0f, 0.9f, 0f), new Vector3(0.65f, 0.9f, 0.65f), bodyMat, removeCollider: true);
            CreatePrimitiveChild("Placeholder_Badge", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 1.35f, 0.34f), new Vector3(0.32f, 0.18f, 0.12f), accentMat, removeCollider: true);

            return SaveAsPrefab(root, $"{PrefabDir}/{prefabName}.prefab");
        }

        private static GameObject BuildApartmentPrefab(MaterialSet mats)
        {
            GameObject root = new GameObject("Building_Apartment_Placeholder");
            MarkStaticEnvironment(root);

            // Single simple BoxCollider for Android physics performance
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 6.5f, 0f);
            col.size = new Vector3(14f, 13f, 14f);

            // VisualRoot with LODGroup (Ready for Mobile City Demo 9 variants + LODs)
            GameObject lod0 = new GameObject("LOD0_Detailed");
            lod0.transform.SetParent(root.transform, false);
            GameObject lod0Main = CreatePrimitiveChild("MainBlock", PrimitiveType.Cube, lod0.transform,
                new Vector3(0f, 6.5f, 0f), new Vector3(14f, 13f, 14f), mats.BuildingA, removeCollider: true);
            GameObject lod0Trim = CreatePrimitiveChild("RoofTrim", PrimitiveType.Cube, lod0.transform,
                new Vector3(0f, 13.3f, 0f), new Vector3(14.5f, 0.6f, 14.5f), mats.Concrete, removeCollider: true);
            GameObject lod0Win = CreatePrimitiveChild("LitWindowBand", PrimitiveType.Cube, lod0.transform,
                new Vector3(0f, 8.2f, 7.05f), new Vector3(10.5f, 1.2f, 0.12f), mats.LampGlow, removeCollider: true);

            GameObject lod1 = new GameObject("LOD1_Medium");
            lod1.transform.SetParent(root.transform, false);
            GameObject lod1Main = CreatePrimitiveChild("SimpleBlock", PrimitiveType.Cube, lod1.transform,
                new Vector3(0f, 6.5f, 0f), new Vector3(14f, 13f, 14f), mats.BuildingA, removeCollider: true);

            MarkStaticEnvironment(lod0);
            MarkStaticEnvironment(lod1);

            LODGroup lodGroup = root.AddComponent<LODGroup>();
            LOD[] lods =
            {
                new LOD(0.28f, new[] { lod0Main.GetComponent<Renderer>(), lod0Trim.GetComponent<Renderer>(), lod0Win.GetComponent<Renderer>() }),
                new LOD(0.05f, new[] { lod1Main.GetComponent<Renderer>() })
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
            col.size = new Vector3(1.85f, 1.35f, 4.1f);

            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            CreatePrimitiveChild("Body", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 0.58f, 0f), new Vector3(1.82f, 0.62f, 4.05f), mats.TrafficBody, removeCollider: true);
            CreatePrimitiveChild("Roof", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 1.12f, -0.15f), new Vector3(1.50f, 0.52f, 2.0f), mats.SedanGlass, removeCollider: true);

            return SaveAsPrefab(root, $"{PrefabDir}/Vehicle_Traffic_Placeholder.prefab");
        }

        private static GameObject BuildConcreteBarrierPrefab(MaterialSet mats)
        {
            GameObject root = new GameObject("Prop_ConcreteBarrier_Placeholder");
            MarkStaticEnvironment(root);

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(2.8f, 1.0f, 0.7f);

            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);
            CreatePrimitiveChild("BarrierMesh", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 0.5f, 0f), new Vector3(2.8f, 1.0f, 0.7f), mats.Concrete, removeCollider: true);
            MarkStaticEnvironment(visualRoot);

            return SaveAsPrefab(root, $"{PrefabDir}/Prop_ConcreteBarrier_Placeholder.prefab");
        }

        private static MissionCatalog CreateMissionDataAssets()
        {
            // Dialogue 1: Mechanic hands over the keys
            DialogueData dlgM1 = CreateOrLoadAsset<DialogueData>($"{DialogueDir}/DLG_M01_Mechanic.asset");
            dlgM1.lines = new List<DialogueLine>
            {
                new DialogueLine { speakerKey = "npc.mechanic.name", textKey = "m01.dlg.1" },
                new DialogueLine { speakerKey = "npc.mechanic.name", textKey = "m01.dlg.2" },
                new DialogueLine { speakerKey = "npc.mechanic.name", textKey = "m01.dlg.3" }
            };
            dlgM1.choices = new List<DialogueChoice>();
            EditorUtility.SetDirty(dlgM1);

            // Dialogue 2: Harbor receiver accepts spare parts and mentions missing car
            DialogueData dlgM2 = CreateOrLoadAsset<DialogueData>($"{DialogueDir}/DLG_M02_HarborReceiver.asset");
            dlgM2.lines = new List<DialogueLine>
            {
                new DialogueLine { speakerKey = "npc.harbor_receiver.name", textKey = "m02.dlg.1" },
                new DialogueLine { speakerKey = "npc.harbor_receiver.name", textKey = "m02.dlg.2" },
                new DialogueLine { speakerKey = "npc.harbor_receiver.name", textKey = "m02.dlg.3" }
            };
            dlgM2.choices = new List<DialogueChoice>();
            EditorUtility.SetDirty(dlgM2);

            // Mission 1: Get the Keys
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

            // Mission 2: Last Delivery
            MissionData m02 = CreateOrLoadAsset<MissionData>($"{MissionDir}/Mission_02_LastDelivery.asset");
            m02.missionId = "m02_last_delivery";
            m02.titleKey = "m02.title";
            m02.summaryKey = "m02.summary";
            m02.nextMissionId = string.Empty; // Transitions to Free Roam until Missions 3-5 are wired
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
            UniversalRenderPipelineAsset medUrp)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Evening Coastal Lighting ---
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.24f, 0.27f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.34f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.14f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.22f, 0.23f, 0.32f);
            RenderSettings.fogStartDistance = 95f;
            RenderSettings.fogEndDistance = 320f;

            GameObject sunObj = new GameObject("Sun_EveningCoastal");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.72f, 0.46f);
            sun.intensity = 1.65f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sunObj.transform.rotation = Quaternion.Euler(19f, -42f, 0f);
            RenderSettings.sun = sun;

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
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.18f, 0.28f);
            cam.farClipPlane = 420f;
            camObj.AddComponent<AudioListener>();
            camObj.AddComponent<UniversalAdditionalCameraData>();
            ThirdPersonCameraRig camRig = camObj.AddComponent<ThirdPersonCameraRig>();

            // --- World Environment (Garage, Coastal Main Road, Back Alleys, Apartments, Cargo Harbor) ---
            GameObject env = new GameObject("--- WORLD_ENVIRONMENT ---");

            // Main urban land ground
            CreateStaticBlock("Ground_CoastalCity", env.transform, new Vector3(-18f, -0.5f, 125f), new Vector3(112f, 1f, 340f), mats.Ground);
            // Sea along eastern coast
            CreateStaticBlock("Coastal_Sea", env.transform, new Vector3(115f, -1.6f, 125f), new Vector3(160f, 1f, 420f), mats.SeaWater, addCollider: false);
            // Coastal seawall curb
            CreateStaticBlock("Coastal_Seawall", env.transform, new Vector3(36.5f, 0.35f, 125f), new Vector3(1.6f, 1.4f, 320f), mats.Concrete);

            // --- ROADS (Using downloaded CC0 ambientCG Asphalt 012 1K) ---
            // 1) Garage Forecourt Connector (East-West at z = 0)
            CreateStaticBlock("Road_GarageConnector", env.transform, new Vector3(0f, 0.02f, 0f), new Vector3(64f, 0.08f, 12f), mats.RoadAsphalt, addCollider: false);
            // 2) Coastal Main Road (Wide boulevard at x = 24, z = 0..245)
            CreateStaticBlock("Road_CoastalMain", env.transform, new Vector3(24f, 0.03f, 122f), new Vector3(13f, 0.08f, 256f), mats.RoadAsphalt, addCollider: false);
            // 3) Alternative Narrow-Street Route (Back Alleys at x = -26, z = 0..245)
            CreateStaticBlock("Road_BackAlleyRoute", env.transform, new Vector3(-26f, 0.03f, 122f), new Vector3(8.5f, 0.08f, 256f), mats.RoadAsphalt, addCollider: false);
            // 4) Cross-streets linking Back Alleys <-> Coastal Road
            CreateStaticBlock("Road_CrossStreet_South", env.transform, new Vector3(-1f, 0.025f, 92f), new Vector3(54f, 0.08f, 9f), mats.RoadAsphalt, addCollider: false);
            CreateStaticBlock("Road_CrossStreet_North", env.transform, new Vector3(-1f, 0.025f, 174f), new Vector3(54f, 0.08f, 9f), mats.RoadAsphalt, addCollider: false);
            // 5) Harbor Approach Connector (z = 246)
            CreateStaticBlock("Road_HarborConnector", env.transform, new Vector3(-1f, 0.025f, 246f), new Vector3(62f, 0.08f, 14f), mats.RoadAsphalt, addCollider: false);

            // --- REPAIR GARAGE (Tamirhane, z ~ 0) ---
            GameObject garageRoot = new GameObject("Zone_RepairGarage");
            garageRoot.transform.SetParent(env.transform, false);
            CreateStaticBlock("Garage_Pad", garageRoot.transform, new Vector3(-2f, 0.01f, 7f), new Vector3(24f, 0.1f, 18f), mats.Sidewalk);
            CreateStaticBlock("Garage_BackWall", garageRoot.transform, new Vector3(-10f, 3.0f, 14f), new Vector3(14f, 6f, 1f), mats.Garage);
            CreateStaticBlock("Garage_SideWallL", garageRoot.transform, new Vector3(-16.5f, 3.0f, 9.5f), new Vector3(1f, 6f, 10f), mats.Garage);
            CreateStaticBlock("Garage_Roof", garageRoot.transform, new Vector3(-10f, 6.2f, 9.5f), new Vector3(15f, 0.6f, 11f), mats.Concrete);
            CreatePointLamp("Lamp_Garage", garageRoot.transform, new Vector3(-5f, 5.2f, 7.5f), new Color(1f, 0.78f, 0.45f), 18f, 2.8f);
            PlacePrefabInstance(prefabs.ConcreteBarrier, garageRoot.transform, new Vector3(-16f, 0f, -3f), Quaternion.Euler(0f, 90f, 0f));
            PlacePrefabInstance(prefabs.ConcreteBarrier, garageRoot.transform, new Vector3(11f, 0f, 13f), Quaternion.identity);

            // --- APARTMENT NEIGHBORHOOD (LOD-enabled blocks framing Back Alleys & Main Road) ---
            GameObject neighborhoodRoot = new GameObject("Zone_ApartmentNeighborhood");
            neighborhoodRoot.transform.SetParent(env.transform, false);
            Vector3[] apartmentPositions =
            {
                // Middle island blocks (between x=-26 alley and x=24 coastal road)
                new Vector3(-1f, 0f, 36f),
                new Vector3(-1f, 0f, 64f),
                new Vector3(-1f, 0f, 120f),
                new Vector3(-1f, 0f, 146f),
                new Vector3(-1f, 0f, 202f),
                // West outer blocks (west side of back alley)
                new Vector3(-45f, 0f, 28f),
                new Vector3(-45f, 0f, 58f),
                new Vector3(-45f, 0f, 92f),
                new Vector3(-45f, 0f, 128f),
                new Vector3(-45f, 0f, 164f),
                new Vector3(-45f, 0f, 202f),
                new Vector3(-45f, 0f, 236f)
            };

            Material[] buildingPalette = { mats.BuildingA, mats.BuildingB, mats.BuildingC };
            for (int i = 0; i < apartmentPositions.Length; i++)
            {
                GameObject b = PlacePrefabInstance(prefabs.ApartmentBuilding, neighborhoodRoot.transform, apartmentPositions[i], Quaternion.Euler(0f, (i % 4) * 90f, 0f));
                b.name = $"Apartment_Block_{i + 1:00}";
                float heightScale = 0.85f + (i % 3) * 0.28f;
                b.transform.localScale = new Vector3(1f, heightScale, 1f);

                // Subtle color variation across placeholder variants
                Material tint = buildingPalette[i % buildingPalette.Length];
                foreach (Renderer r in b.GetComponentsInChildren<Renderer>())
                {
                    if (r.gameObject.name == "MainBlock" || r.gameObject.name == "SimpleBlock")
                    {
                        r.sharedMaterial = tint;
                    }
                }
            }

            // Parked vehicles (lightweight UAA City Props placeholder slots)
            PlacePrefabInstance(prefabs.TrafficVehicle, neighborhoodRoot.transform, new Vector3(-19.5f, 0f, 52f), Quaternion.identity);
            PlacePrefabInstance(prefabs.TrafficVehicle, neighborhoodRoot.transform, new Vector3(-19.5f, 0f, 134f), Quaternion.Euler(0f, 180f, 0f));
            PlacePrefabInstance(prefabs.TrafficVehicle, neighborhoodRoot.transform, new Vector3(15.5f, 0f, 78f), Quaternion.identity);

            // Coastal evening street lamps
            for (int z = 25; z <= 225; z += 40)
            {
                CreateStreetLamp(env.transform, new Vector3(33.5f, 0f, z), mats.Concrete, mats.LampGlow);
            }

            // --- SMALL CARGO HARBOR (Yük Limanı, z ~ 245..272) ---
            GameObject harborRoot = new GameObject("Zone_CargoHarbor");
            harborRoot.transform.SetParent(env.transform, false);
            CreateStaticBlock("Harbor_QuayDeck", harborRoot.transform, new Vector3(10f, 0.04f, 260f), new Vector3(56f, 0.16f, 34f), mats.HarborDock);
            CreateStaticBlock("Harbor_Warehouse", harborRoot.transform, new Vector3(-12f, 4.0f, 268f), new Vector3(18f, 8f, 14f), mats.Garage);
            CreateStaticBlock("Harbor_Container_01", harborRoot.transform, new Vector3(24f, 1.5f, 266f), new Vector3(3.2f, 3.0f, 9.5f), mats.ContainerRed);
            CreateStaticBlock("Harbor_Container_02", harborRoot.transform, new Vector3(28f, 1.5f, 266f), new Vector3(3.2f, 3.0f, 9.5f), mats.ContainerBlue);
            CreateStaticBlock("Harbor_Container_03", harborRoot.transform, new Vector3(26f, 4.5f, 266f), new Vector3(3.2f, 3.0f, 9.5f), mats.ContainerRed);
            PlacePrefabInstance(prefabs.ConcreteBarrier, harborRoot.transform, new Vector3(34f, 0f, 255f), Quaternion.Euler(0f, 90f, 0f));
            PlacePrefabInstance(prefabs.ConcreteBarrier, harborRoot.transform, new Vector3(34f, 0f, 262f), Quaternion.Euler(0f, 90f, 0f));
            CreatePointLamp("Lamp_HarborDock", harborRoot.transform, new Vector3(10f, 7.5f, 252f), new Color(1f, 0.80f, 0.48f), 28f, 3.5f);

            // --- WORLD ANCHORS & SPAWNS ---
            GameObject anchorsRoot = new GameObject("--- WORLD_ANCHORS ---");
            CreateWorldAnchor("Spawn_GarageFoot", "spawn_garage_foot", anchorsRoot.transform, new Vector3(-8.5f, 0.12f, -6.0f), Quaternion.Euler(0f, 25f, 0f), 2f);
            CreateWorldAnchor("Spawn_GarageSedan", "spawn_garage_sedan", anchorsRoot.transform, new Vector3(3.5f, 0.25f, 4.5f), Quaternion.Euler(0f, 90f, 0f), 3.5f);
            CreateWorldAnchor("Harbor_DeliveryZone", "harbor_delivery_zone", anchorsRoot.transform, new Vector3(11f, 0.15f, 248f), Quaternion.identity, 7.5f);

            // --- ACTORS (Player, Sedan, NPCs) ---
            GameObject actorsRoot = new GameObject("--- ACTORS ---");
            GameObject playerObj = PlacePrefabInstance(prefabs.Player, actorsRoot.transform, new Vector3(-8.5f, 0.12f, -6.0f), Quaternion.Euler(0f, 25f, 0f));
            GameObject sedanObj = PlacePrefabInstance(prefabs.PlayerSedan, actorsRoot.transform, new Vector3(3.5f, 0.25f, 4.5f), Quaternion.Euler(0f, 90f, 0f));
            PlacePrefabInstance(prefabs.NpcMechanic, actorsRoot.transform, new Vector3(-4.5f, 0.08f, 6.2f), Quaternion.Euler(0f, 165f, 0f));
            PlacePrefabInstance(prefabs.NpcHarborReceiver, actorsRoot.transform, new Vector3(2.2f, 0.14f, 255.5f), Quaternion.Euler(0f, 160f, 0f));

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

            // Save scene and register in Build Settings
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
            // EventSystem using Unity 6 InputSystemUIInputModule (compatible with activeInputHandler = 1)
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

            // Safe-Area Root
            GameObject safeRoot = CreateUiRect("SafeAreaRoot", canvasObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeRoot.AddComponent<SafeAreaFitter>();

            // Camera Touch Zone on Right Half (placed first so buttons render above and block raycasts)
            GameObject camPad = CreateUiRect("CameraTouchZone", safeRoot.transform, new Vector2(0.45f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            Image camPadImg = camPad.AddComponent<Image>();
            camPadImg.color = new Color(0f, 0f, 0f, 0f);
            camPad.AddComponent<CameraTouchZone>();

            // --- TOP-LEFT: Mission Box ---
            GameObject missionBox = CreateUiPanel("MissionBox", safeRoot.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -24f), new Vector2(540f, 134f), new Color(0.07f, 0.10f, 0.15f, 0.78f));
            Text missionTitle = CreateUiText("MissionTitle", missionBox.transform, uiFont, "GET THE KEYS", 28, TextAnchor.UpperLeft,
                new Color(1f, 0.76f, 0.25f), new Vector2(18f, -14f), new Vector2(504f, 38f), FontStyle.Bold);
            Text missionObj = CreateUiText("MissionObjective", missionBox.transform, uiFont, "Walk to the repair garage and talk to Marco.", 22, TextAnchor.UpperLeft,
                Color.white, new Vector2(18f, -54f), new Vector2(504f, 68f));

            // --- TOP-CENTER: Waypoint Compass + Timer + Tail Bar ---
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

            // --- TOP-RIGHT: Utility & Performance Telemetry (Tap to save benchmark log) ---
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

            // --- ON-FOOT CONTROLS ---
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

            // --- DRIVING CONTROLS (Simultaneous Multi-Touch Steer + Gas/Brake) ---
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

            // --- STATUS TOAST & EDITOR HINT ---
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

            // --- DIALOGUE PANEL ---
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

            // --- MISSION FAILED MODAL ---
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

            // --- MISSION COMPLETE BANNER ---
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

        private static void CreateStreetLamp(Transform parent, Vector3 pos, Material poleMat, Material bulbMat)
        {
            GameObject lamp = new GameObject("StreetLamp");
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = pos;
            CreatePrimitiveChild("Pole", PrimitiveType.Cylinder, lamp.transform, new Vector3(0f, 3.2f, 0f), new Vector3(0.22f, 3.2f, 0.22f), poleMat, removeCollider: true);
            CreatePrimitiveChild("Bulb", PrimitiveType.Sphere, lamp.transform, new Vector3(-0.5f, 6.2f, 0f), new Vector3(0.5f, 0.35f, 0.5f), bulbMat, removeCollider: true);
            MarkStaticEnvironment(lamp);
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
            light.shadows = LightShadows.None; // Keep real-time point lights shadowless for mobile 30 FPS
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
