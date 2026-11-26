using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Player;
using OurWorld.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    public enum ShootingWeaponId
    {
        Pistol9mm = 0,
        TrapShotgun = 1,
        TacticalCarbine = 2,
        Sniper100m = 3
    }

    public enum ShootingModeId
    {
        ClassicBullseye = 0,
        IpscSteel = 1,
        TrapClay = 2,
        SniperWind100m = 3
    }

    public enum ShootingUpgradeId
    {
        PistolRedDot = 0,
        PistolSuppressor = 1,
        PistolExtMag = 2,
        ShotgunChoke = 3,
        ShotgunFastLoad = 4,
        CarbineHolo = 5,
        CarbineBrake = 6,
        CarbineLaser = 7,
        SniperScope8x = 8,
        SniperBipod = 9,
        SniperHeavyBarrel = 10
    }

    public struct WeaponSpec
    {
        public ShootingWeaponId Id;
        public string ModelName;
        public string NameTr;
        public string NameEn;
        public int BuyCost;
        public int BaseMag;
        public int ExtendedMag;
        public float FireInterval;
        public float ReloadTime;
        public float RecoilPitch;
        public float RecoilYaw;
        public float HipSpreadDeg;
        public float AdsSpreadDeg;
        public float AdsFov;
        public float AdsSensScale;
        public bool IsFullAuto;
        public bool IsLongGun;
        public bool UsesFullScreenScope;
        public int Pellets;
        public Vector3 GripLocalOffset;
        public Vector3 GripLocalEuler;
        public Vector3 Scale;
    }

    public struct UpgradeSpec
    {
        public ShootingUpgradeId Id;
        public ShootingWeaponId Weapon;
        public string PartName;
        public string NameTr;
        public string NameEn;
        public string EffectTr;
        public string EffectEn;
        public int Cost;
    }

    /// <summary>
    /// Builds Aydos Atış Poligonu (sporting & tactical shooting range) south of the Hamidiye Sports Strip
    /// at runtime with Poly Haven PBR surfaces, 4 covered firing bays, motorized targets, IPSC steel plates,
    /// Trap/Skeet clay thrower wing, wind flags, night floodlights, and instructor NPC Usta Haluk.
    /// Weapons are strictly scoped to the shooting range (App Store 4+/9+ safe: zero NPC/animal targeting).
    /// </summary>
    public sealed class ShootingRangeInstaller : MonoBehaviour
    {
        public static ShootingRangeInstaller Instance { get; private set; }
        public static LiveShootingRange ActiveRange { get; private set; }

        public static readonly Vector3 RangeCenter = new Vector3(92.0f, 0.0f, -222.0f);

        private readonly List<Material> _ownedMats = new List<Material>(24);
        private Transform _instructorHead;
        private ProceduralHumanoid _instructorHum;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_AydosShootingRange");
            go.AddComponent<ShootingRangeInstaller>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private IEnumerator Start()
        {
            yield return null;
            BuildRangeFacilityAndArena();
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _ownedMats.Count; i++)
            {
                if (_ownedMats[i] != null) Destroy(_ownedMats[i]);
            }
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_instructorHum != null)
            {
                _instructorHum.SetLocomotion(0f, true, 0f);
                if (PlayerModeController.Instance != null)
                {
                    Vector3 toP = PlayerModeController.Instance.ActiveWorldPosition - _instructorHum.transform.position;
                    toP.y = 0f;
                    if (toP.sqrMagnitude > 0.25f && toP.sqrMagnitude < 18f * 18f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(toP.normalized, Vector3.up);
                        _instructorHum.transform.rotation = Quaternion.Slerp(_instructorHum.transform.rotation, targetRot, 1f - Mathf.Exp(-3.5f * Time.deltaTime));
                    }
                }
            }
        }

        private Material MakeTexturedMat(string name, Color tint, string texPrefix, Vector2 tiling, float smoothness = 0.28f, float metallic = 0f)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            if (lit == null) return null;
            Material m = new Material(lit) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            else m.color = tint;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);

            if (!string.IsNullOrEmpty(texPrefix))
            {
                Texture2D diff = Resources.Load<Texture2D>($"OurWorldTheme/Textures/{texPrefix}_diff");
                Texture2D norm = Resources.Load<Texture2D>($"OurWorldTheme/Textures/{texPrefix}_normal");
                if (diff != null && m.HasProperty("_BaseMap"))
                {
                    m.SetTexture("_BaseMap", diff);
                    m.SetTextureScale("_BaseMap", tiling);
                }
                if (norm != null && m.HasProperty("_BumpMap"))
                {
                    m.EnableKeyword("_NORMALMAP");
                    m.SetTexture("_BumpMap", norm);
                    m.SetTextureScale("_BumpMap", tiling);
                    if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 0.72f);
                }
            }
            _ownedMats.Add(m);
            return m;
        }

        private Material MakeEmissiveMat(string name, Color baseCol, Color emissiveCol)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            if (lit == null) return null;
            Material m = new Material(lit) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
            else m.color = baseCol;
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emissiveCol);
            }
            _ownedMats.Add(m);
            return m;
        }

        private GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, bool collider = true, Quaternion? rot = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = size;
            if (!collider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null)
            {
                mr.sharedMaterial = mat;
            }
            return go;
        }

        private void BuildRangeFacilityAndArena()
        {
            GameObject root = new GameObject("Aydos_ShootingRange_Facility");
            root.transform.SetParent(transform, false);
            root.transform.position = RangeCenter;

            // Poly Haven CC0 PBR Materials (reusing OurWorldTheme textures for cohesive Istanbul/Aydos look!)
            Material asphaltMat = MakeTexturedMat("MAT_RangeAsphalt", new Color(0.76f, 0.77f, 0.80f), "ph_asphalt", new Vector2(4f, 10f), 0.22f);
            Material woodMat = MakeTexturedMat("MAT_RangeWood", new Color(0.88f, 0.76f, 0.62f), "ph_woodplanks", new Vector2(3f, 2f), 0.25f);
            Material darkWoodMat = MakeTexturedMat("MAT_RangeDarkWood", new Color(0.46f, 0.34f, 0.24f), "ph_woodplanks", new Vector2(2f, 2f), 0.22f);
            Material concreteMat = MakeTexturedMat("MAT_RangeConcrete", new Color(0.84f, 0.82f, 0.78f), "ph_plaster", new Vector2(6f, 3f), 0.20f);
            Material roofMat = MakeTexturedMat("MAT_RangeRoof", new Color(0.82f, 0.56f, 0.44f), "ph_rooftiles", new Vector2(8f, 3f), 0.26f);
            Material sandMat = MakeTexturedMat("MAT_RangeSandBerm", new Color(0.86f, 0.78f, 0.62f), "ph_sand", new Vector2(10f, 3f), 0.14f);
            Material turfMat = MakeTexturedMat("MAT_RangeTurf", new Color(0.68f, 0.84f, 0.58f), "ph_grass", new Vector2(12f, 28f), 0.12f);
            Material steelMat = MakeTexturedMat("MAT_RangeSteel", new Color(0.20f, 0.23f, 0.27f), null, Vector2.one, 0.55f, 0.78f);
            Material redMat = MakeTexturedMat("MAT_RangeSafetyRed", new Color(0.82f, 0.16f, 0.14f), null, Vector2.one, 0.32f);
            Material oliveMat = MakeTexturedMat("MAT_RangeCrateOlive", new Color(0.24f, 0.32f, 0.20f), null, Vector2.one, 0.28f);
            Material goldMat = MakeTexturedMat("MAT_RangeGold", new Color(0.90f, 0.70f, 0.22f), null, Vector2.one, 0.52f, 0.70f);
            Material ledWarmMat = MakeEmissiveMat("MAT_RangeLED", new Color(1.0f, 0.94f, 0.80f), new Color(2.4f, 2.0f, 1.4f));

            // 1. Connecting Scenic Access Road from Kadife Sokağı (local Z = +9.5 .. +36.5 -> world Z = -212.5 .. -185.5, clear 51m corridor!)
            CreateBox("AccessRoad_Asphalt", root.transform, new Vector3(0f, 0.016f, 23.0f), new Vector3(8.5f, 0.03f, 27.0f), asphaltMat, collider: false);
            CreateBox("ParkingPad", root.transform, new Vector3(0f, 0.018f, 9.5f), new Vector3(30f, 0.03f, 12f), asphaltMat, collider: false);

            // Entrance Archway Sign facing North (+Z toward arriving players!)
            CreateBox("ArchPillarL", root.transform, new Vector3(-5.2f, 2.15f, 15.2f), new Vector3(0.45f, 4.3f, 0.45f), darkWoodMat, collider: true);
            CreateBox("ArchPillarR", root.transform, new Vector3(5.2f, 2.15f, 15.2f), new Vector3(0.45f, 4.3f, 0.45f), darkWoodMat, collider: true);
            CreateBox("ArchHeader", root.transform, new Vector3(0f, 4.15f, 15.2f), new Vector3(11.4f, 1.05f, 0.48f), steelMat, collider: false);
            CreateBox("ArchAccentStripe", root.transform, new Vector3(0f, 3.62f, 15.2f), new Vector3(11.4f, 0.10f, 0.52f), goldMat, collider: false);
            CreateSignLabel(root.transform, "AYDOS ATIŞ POLİGONU\n<size=22><color=#FFDE59>SPORTİF & TAKTİK · 4 SİLAH · 15m - 100m</color></size>", new Vector3(0f, 4.16f, 15.48f), 180f, new Vector2(10.8f, 1.0f), 34, Color.white);

            // 2. Covered 4-Bay Shooting Pavilion (local X: -13 .. +13, local Z: -4 .. +4)
            CreateBox("PavilionFloor", root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(26f, 0.12f, 9.0f), concreteMat, collider: true);
            // Rear timber-plaster wall with wide open central entry (gap X: -4 .. +4)
            CreateBox("RearWallL", root.transform, new Vector3(-8.5f, 1.75f, 4.3f), new Vector3(9.0f, 3.4f, 0.35f), concreteMat, collider: true);
            CreateBox("RearWallR", root.transform, new Vector3(8.5f, 1.75f, 4.3f), new Vector3(9.0f, 3.4f, 0.35f), concreteMat, collider: true);
            CreateBox("SideWallW", root.transform, new Vector3(-12.85f, 1.75f, 0f), new Vector3(0.35f, 3.4f, 9.0f), concreteMat, collider: true);
            CreateBox("SideWallE", root.transform, new Vector3(12.85f, 1.75f, 0f), new Vector3(0.35f, 3.4f, 9.0f), concreteMat, collider: true);

            // Sloped terracotta-tiled pavilion roof & steel rafters
            CreateBox("PavilionRoof", root.transform, new Vector3(0f, 3.75f, 0f), new Vector3(27.4f, 0.24f, 10.6f), roofMat, collider: false, rot: Quaternion.Euler(4f, 0f, 0f));
            CreateBox("PavilionFasciaFront", root.transform, new Vector3(0f, 3.32f, -4.45f), new Vector3(26.8f, 0.56f, 0.22f), steelMat, collider: false);

            // 4 Numbered Firing Bays with Acoustic Wood Dividers & Firing Counter Benches (at local Z = -2.6, firing toward -Z!)
            float[] bayXs = { -9.0f, -3.0f, 3.0f, 9.0f };
            string[] bayLabels = { "KULVAR 1 · 15m", "KULVAR 2 · 25m", "KULVAR 3 · 50m", "KULVAR 4 · 100m" };
            for (int b = 0; b < 5; b++)
            {
                float dx = -12.0f + b * 6.0f;
                CreateBox($"BayDivider_{b}", root.transform, new Vector3(dx, 1.35f, -2.5f), new Vector3(0.18f, 2.5f, 3.2f), woodMat, collider: true);
                CreateBox($"BayPost_{b}", root.transform, new Vector3(dx, 1.80f, -4.1f), new Vector3(0.22f, 3.5f, 0.22f), steelMat, collider: true);
            }
            for (int b = 0; b < 4; b++)
            {
                float bx = bayXs[b];
                // Firing counter desk + rubber mat + brass catch tray
                CreateBox($"BayCounter_{b}", root.transform, new Vector3(bx, 0.92f, -3.2f), new Vector3(5.6f, 0.10f, 0.85f), darkWoodMat, collider: true);
                CreateBox($"BayFrontPanel_{b}", root.transform, new Vector3(bx, 0.48f, -3.58f), new Vector3(5.6f, 0.92f, 0.14f), concreteMat, collider: true);
                CreateBox($"BayLED_{b}", root.transform, new Vector3(bx, 3.28f, -2.2f), new Vector3(2.4f, 0.08f, 0.18f), ledWarmMat, collider: false);
                CreateSignLabel(root.transform, bayLabels[b], new Vector3(bx, 3.30f, -4.30f), 180f, new Vector2(4.6f, 0.44f), 34, new Color(1.0f, 0.88f, 0.36f));

                // Military-style wooden ammo crate & target stack decor beside booth
                CreateBox($"AmmoCrate_{b}", root.transform, new Vector3(bx + 2.15f, 0.36f, -2.4f), new Vector3(0.68f, 0.44f, 0.42f), oliveMat, collider: false);
                CreateBox($"AmmoCrateTrim_{b}", root.transform, new Vector3(bx + 2.15f, 0.59f, -2.4f), new Vector3(0.70f, 0.04f, 0.44f), goldMat, collider: false);
            }

            // Safety Warning Board inside pavilion (facing inward toward South / bays -> yaw = 0f!)
            CreateBox("SafetyBoard", root.transform, new Vector3(-8.5f, 2.0f, 4.08f), new Vector3(4.6f, 1.15f, 0.08f), redMat, collider: false);
            CreateSignLabel(root.transform, "GÜVENLİK KURALLARI\n<size=20>KULAKLIK & GÖZLÜK ZORUNLUDUR · NAMLU HEDEF YÖNÜNE!</size>", new Vector3(-8.5f, 2.0f, 4.02f), 0f, new Vector2(4.4f, 1.0f), 30, Color.white);

            // Armory Counter & Weapon Rack Display on West Side inside Pavilion
            CreateBox("ArmoryDesk", root.transform, new Vector3(-10.6f, 0.56f, 1.4f), new Vector3(3.2f, 1.0f, 0.9f), darkWoodMat, collider: true);
            CreateSignLabel(root.transform, "CEPHANELİK & SİLAH KİRA", new Vector3(-10.6f, 1.45f, 1.4f), 180f, new Vector2(3.0f, 0.38f), 26, new Color(1.0f, 0.88f, 0.35f));

            // Display showcase guns mounted nicely on the Armory rear wall!
            OurWorldModelLibrary.InstantiateModel("OW_Weapon_Carbine", root.transform, new Vector3(-9.6f, 1.95f, 3.95f), Quaternion.Euler(0f, 90f, 0f), Vector3.one * 1.25f, removeColliders: true);
            OurWorldModelLibrary.InstantiateModel("OW_Weapon_Shotgun", root.transform, new Vector3(-7.4f, 1.55f, 3.95f), Quaternion.Euler(0f, 90f, 0f), Vector3.one * 1.20f, removeColliders: true);
            OurWorldModelLibrary.InstantiateModel("OW_Weapon_Sniper", root.transform, new Vector3(8.5f, 1.90f, 3.95f), Quaternion.Euler(0f, -90f, 0f), Vector3.one * 1.20f, removeColliders: true);

            // 3. Downrange Turf Field (local Z: -4 .. -112 -> 108m long!), Side Safety Berms & Backstop Berm
            CreateBox("DownrangeTurf", root.transform, new Vector3(6f, 0.012f, -59f), new Vector3(64f, 0.025f, 112f), turfMat, collider: false);
            // Left & Right Protective Earthen Safety Berms
            CreateBox("SafetyBermW", root.transform, new Vector3(-20.5f, 2.2f, -58f), new Vector3(6.0f, 4.6f, 112f), sandMat, collider: true);
            CreateBox("SafetyBermE", root.transform, new Vector3(40.5f, 2.2f, -58f), new Vector3(6.0f, 4.6f, 112f), sandMat, collider: true);
            // High Rear Impact Backstop Berm at 108m (-112 local Z)
            CreateBox("BackstopBermRear", root.transform, new Vector3(10.0f, 4.2f, -113f), new Vector3(68f, 8.6f, 7.5f), sandMat, collider: true);
            CreateBox("BackstopTimberWall", root.transform, new Vector3(10.0f, 2.1f, -109.1f), new Vector3(58f, 4.2f, 0.45f), darkWoodMat, collider: true);

            // Red/White Distance Marker Signs along West lane edge (15m, 25m, 50m, 100m)
            float[] markDists = { 15f, 25f, 50f, 100f };
            for (int i = 0; i < markDists.Length; i++)
            {
                float mz = -3.0f - markDists[i];
                CreateBox($"DistLine_{markDists[i]}", root.transform, new Vector3(0f, 0.028f, mz), new Vector3(24f, 0.01f, 0.16f), concreteMat, collider: false);
                CreateBox($"DistSignBoard_{markDists[i]}", root.transform, new Vector3(-14.6f, 1.35f, mz), new Vector3(1.9f, 0.85f, 0.14f), redMat, collider: false);
                CreateBox($"DistSignPost_{markDists[i]}", root.transform, new Vector3(-14.6f, 0.65f, mz), new Vector3(0.14f, 1.3f, 0.14f), steelMat, collider: false);
                CreateSignLabel(root.transform, $"{markDists[i]:0}m", new Vector3(-14.6f, 1.36f, mz + 0.09f), 180f, new Vector2(1.75f, 0.72f), 44, Color.white);
            }

            // 4. Wide-Open East Wing Trap / Skeet Clay Pigeon Station (local X = +24, Z = -3 .. -20 — clear of pavilion wall!)
            CreateBox("TrapPad", root.transform, new Vector3(24f, 0.04f, -3.5f), new Vector3(7.6f, 0.08f, 6.2f), concreteMat, collider: true);
            CreateBox("TrapSignPostL", root.transform, new Vector3(21.0f, 1.25f, 0.2f), new Vector3(0.14f, 2.5f, 0.14f), darkWoodMat, collider: false);
            CreateBox("TrapSignPostR", root.transform, new Vector3(27.0f, 1.25f, 0.2f), new Vector3(0.14f, 2.5f, 0.14f), darkWoodMat, collider: false);
            CreateBox("TrapSignBoard", root.transform, new Vector3(24f, 2.30f, 0.25f), new Vector3(6.2f, 0.82f, 0.14f), steelMat, collider: false);
            CreateSignLabel(root.transform, "TRAP / SKEET · KİL PLAK SAHASI", new Vector3(24f, 2.30f, 0.34f), 180f, new Vector2(5.8f, 0.68f), 30, new Color(1.0f, 0.68f, 0.22f));

            // Low Protective Trap House + Blender Clay Thrower Machine at (24, 0, -18)
            CreateBox("TrapHouse", root.transform, new Vector3(24f, 0.55f, -18.5f), new Vector3(2.8f, 1.1f, 2.6f), concreteMat, collider: true);
            GameObject throwerGo = OurWorldModelLibrary.InstantiateModel(
                "OW_Range_ClayThrower", root.transform,
                new Vector3(24f, 1.10f, -18.5f), Quaternion.Euler(0f, 180f, 0f),
                Vector3.one * 1.05f, removeColliders: true);

            // 5. Night Floodlights & Pavilion Overhead Lights (auto-managed by NeighborhoodNightLighting!)
            Vector3[] floodPositions =
            {
                new Vector3(-11f, 3.0f, -2.5f),
                new Vector3(0f, 3.0f, -2.5f),
                new Vector3(11f, 3.0f, -2.5f),
                new Vector3(-14f, 6.2f, -26f),
                new Vector3(14f, 6.2f, -26f),
                new Vector3(0f, 7.2f, -58f),
                new Vector3(31f, 5.8f, -14f)
            };
            for (int i = 0; i < floodPositions.Length; i++)
            {
                bool isTower = i >= 3;
                if (isTower)
                {
                    Vector3 p = floodPositions[i];
                    CreateBox($"FloodPole_{i}", root.transform, new Vector3(p.x, p.y * 0.5f, p.z), new Vector3(0.22f, p.y, 0.22f), steelMat, collider: false);
                    CreateBox($"FloodHead_{i}", root.transform, p, new Vector3(1.1f, 0.32f, 0.42f), ledWarmMat, collider: false);
                }
                GameObject lgo = new GameObject($"RangeFloodLight_{i}");
                lgo.transform.SetParent(root.transform, false);
                lgo.transform.localPosition = floodPositions[i] - new Vector3(0f, 0.25f, 0f);
                Light pl = lgo.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = isTower ? new Color(1.0f, 0.94f, 0.84f) : new Color(1.0f, 0.88f, 0.68f);
                pl.intensity = isTower ? 38f : 18f;
                pl.range = isTower ? 42f : 16f;
                pl.shadows = LightShadows.None;
                pl.enabled = false;
                NightLight nl = lgo.AddComponent<NightLight>();
                nl.Configure(pl, null, Color.black);
                if (NeighborhoodNightLighting.Instance != null)
                {
                    NeighborhoodNightLighting.Instance.RegisterDynamicOutdoorLight(nl);
                }
            }

            // Decorative Istanbul Cypress & Plane Trees framing the Shooting Range entrance
            Vector3[] treeSpots =
            {
                new Vector3(-16.5f, 0f, 10f), new Vector3(16.5f, 0f, 10f),
                new Vector3(-18.5f, 0f, -2f), new Vector3(26.5f, 0f, 6f),
                new Vector3(-10f, 0f, 24f),   new Vector3(10f, 0f, 24f)
            };
            for (int i = 0; i < treeSpots.Length; i++)
            {
                string model = (i % 2 == 0) ? "OW_CypressTree" : "OW_PlaneTree";
                OurWorldModelLibrary.InstantiateModel(model, root.transform, treeSpots[i], Quaternion.Euler(0f, i * 67f, 0f), Vector3.one * (i % 2 == 0 ? 1.15f : 0.95f), removeColliders: true);
            }

            // 6. Range Instructor NPC ("Usta Haluk") at the Armory Counter
            SpawnInstructorNpc(root.transform);

            // 7. Attach LiveShootingRange Playable Arena Controller + Entry Triggers!
            LiveShootingRange range = root.AddComponent<LiveShootingRange>();
            range.ConfigureSceneRefs(root.transform, throwerGo != null ? throwerGo.transform : null);
            ActiveRange = range;

            // Main Entry Trigger right in front of the firing bays
            GameObject mainTrig = new GameObject("ShootingRange_EntryTrigger");
            mainTrig.transform.SetParent(root.transform, false);
            mainTrig.transform.localPosition = new Vector3(0f, 0.2f, 1.5f);
            InteractableTrigger trig1 = mainTrig.AddComponent<InteractableTrigger>();
            trig1.Configure(InteractableKind.SportsArena, "sport_shooting_range", "ui.btn.interact", 4.6f);
            trig1.SetCustomPrompt("Aydos Atış Poligonu'na Gir (4 Silah · 4 Mod)", "Enter Aydos Shooting Range (4 Weapons · 4 Modes)");

            // Secondary Entry Trigger right beside Instructor Usta Haluk at the Armory Desk
            GameObject armoryTrig = new GameObject("ShootingRange_ArmoryTrigger");
            armoryTrig.transform.SetParent(root.transform, false);
            armoryTrig.transform.localPosition = new Vector3(-9.5f, 0.2f, 0.4f);
            InteractableTrigger trig2 = armoryTrig.AddComponent<InteractableTrigger>();
            trig2.Configure(InteractableKind.SportsArena, "sport_shooting_range", "ui.btn.interact", 3.6f);
            trig2.SetCustomPrompt("Usta Haluk · Poligon & Cephanelik", "Master Haluk · Range & Armory");

            // WorldAnchor so Story Missions / GPS Radar can point straight to Aydos Atış Poligonu!
            GameObject anchorGo = new GameObject("Anchor_ShootingRange");
            anchorGo.transform.SetParent(root.transform, false);
            anchorGo.transform.localPosition = new Vector3(0f, 0.1f, 3.0f);
            anchorGo.AddComponent<WorldAnchor>().Configure("sport_shooting_range", 6f, new Vector3(0f, 3.2f, 0f));
        }

        private void SpawnInstructorNpc(Transform root)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            if (lit == null) return;
            Material skin = new Material(lit) { color = new Color(0.86f, 0.70f, 0.56f) };
            Material shirt = new Material(lit) { color = new Color(0.24f, 0.32f, 0.22f) }; // Olive tactical instructor vest
            Material pants = new Material(lit) { color = new Color(0.18f, 0.20f, 0.23f) };
            Material shoes = new Material(lit) { color = new Color(0.12f, 0.10f, 0.09f) };
            Material hair = new Material(lit) { color = new Color(0.42f, 0.43f, 0.46f) }; // Distinguished salt-and-pepper hair
            _ownedMats.Add(skin);
            _ownedMats.Add(shirt);
            _ownedMats.Add(pants);
            _ownedMats.Add(shoes);
            _ownedMats.Add(hair);

            _instructorHum = ProceduralHumanoid.Build(root, skin, shirt, pants, shoes, hair, 1.82f);
            _instructorHum.name = "NPC_UstaHaluk_Instructor";
            _instructorHum.transform.localPosition = new Vector3(-10.4f, 0.12f, 2.35f);
            _instructorHum.transform.localRotation = Quaternion.Euler(0f, 165f, 0f);
            _instructorHead = _instructorHum.HeadTransform;
            CreateSignLabel(_instructorHum.transform, "Usta Haluk\n<size=20>Poligon Eğitmeni</size>", new Vector3(0f, 2.18f, 0f), 180f, new Vector2(2.2f, 0.55f), 28, new Color(1.0f, 0.90f, 0.42f));
        }

        private static void CreateSignLabel(Transform parent, string text, Vector3 localPos, float yawDeg, Vector2 worldSize, int fontSize, Color col)
        {
            // Depth-tested World-Space Canvas label (never bleeds through roofs/walls in URP!)
            GameObject go = new GameObject("RangeWorldSign");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            go.transform.localScale = Vector3.one * 0.01f;

            Canvas cv = go.AddComponent<Canvas>();
            cv.renderMode = RenderMode.WorldSpace;
            RectTransform crt = go.GetComponent<RectTransform>();
            crt.sizeDelta = worldSize * 100f;

            GameObject txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            RectTransform trt = txtGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            UnityEngine.UI.Text uiText = txtGo.AddComponent<UnityEngine.UI.Text>();
            uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            uiText.text = text;
            uiText.fontSize = fontSize;
            uiText.fontStyle = FontStyle.Bold;
            uiText.supportRichText = true;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.color = col;
            uiText.raycastTarget = false;
        }
    }

    // =========================================================================
    // PERSISTENT ARMORY & HIGH SCORE STORE (PlayerPrefs)
    // =========================================================================
    public static class ShootingRangeStore
    {
        private const string Prefix = "ow_shoot_v1_";

        public static bool IsWeaponOwned(ShootingWeaponId id)
        {
            if (id == ShootingWeaponId.Pistol9mm) return true; // Starter 9mm pistol is always owned!
            return PlayerPrefs.GetInt(Prefix + "w_" + (int)id, 0) == 1;
        }

        public static void SetWeaponOwned(ShootingWeaponId id)
        {
            PlayerPrefs.SetInt(Prefix + "w_" + (int)id, 1);
            PlayerPrefs.Save();
        }

        public static bool IsUpgradeOwned(ShootingUpgradeId id)
        {
            return PlayerPrefs.GetInt(Prefix + "u_" + (int)id, 0) == 1;
        }

        public static void SetUpgradeOwned(ShootingUpgradeId id)
        {
            PlayerPrefs.SetInt(Prefix + "u_" + (int)id, 1);
            PlayerPrefs.Save();
        }

        public static int GetBestScore(ShootingModeId mode)
        {
            return PlayerPrefs.GetInt(Prefix + "best_" + (int)mode, 0);
        }

        public static bool RecordScore(ShootingModeId mode, int score)
        {
            int prev = GetBestScore(mode);
            if (score > prev)
            {
                PlayerPrefs.SetInt(Prefix + "best_" + (int)mode, score);
                PlayerPrefs.Save();
                return true;
            }
            return false;
        }

        public static void ResetAll()
        {
            for (int w = 0; w < 8; w++) PlayerPrefs.DeleteKey(Prefix + "w_" + w);
            for (int u = 0; u < 8; u++) PlayerPrefs.DeleteKey(Prefix + "u_" + u);
            for (int m = 0; m < 8; m++) PlayerPrefs.DeleteKey(Prefix + "best_" + m);
            PlayerPrefs.Save();
        }
    }

    // =========================================================================
    // LIVE SHOOTING RANGE CONTROLLER (4 WEAPONS · 4 MODES · BALLISTICS · VFX)
    // =========================================================================
    public sealed class LiveShootingRange : MonoBehaviour, IPlayableSportArena
    {
        public static readonly WeaponSpec[] Weapons =
        {
            new WeaponSpec
            {
                Id = ShootingWeaponId.Pistol9mm, ModelName = "OW_Weapon_Pistol",
                NameTr = "9mm Tabanca", NameEn = "9mm Sport Pistol",
                BuyCost = 0, BaseMag = 15, ExtendedMag = 22,
                FireInterval = 0.17f, ReloadTime = 1.25f,
                RecoilPitch = 1.35f, RecoilYaw = 0.35f,
                HipSpreadDeg = 1.15f, AdsSpreadDeg = 0.25f,
                AdsFov = 42f, AdsSensScale = 0.48f,
                IsFullAuto = false, IsLongGun = false, UsesFullScreenScope = false, Pellets = 1,
                GripLocalOffset = new Vector3(0.0f, 0.0f, 0.04f),
                GripLocalEuler = Vector3.zero,
                Scale = Vector3.one * 1.12f
            },
            new WeaponSpec
            {
                Id = ShootingWeaponId.TrapShotgun, ModelName = "OW_Weapon_Shotgun",
                NameTr = "12 Cal Pompalı", NameEn = "12G Trap Shotgun",
                BuyCost = 260, BaseMag = 6, ExtendedMag = 6,
                FireInterval = 0.62f, ReloadTime = 1.75f,
                RecoilPitch = 3.20f, RecoilYaw = 0.85f,
                HipSpreadDeg = 3.80f, AdsSpreadDeg = 2.45f,
                AdsFov = 46f, AdsSensScale = 0.60f,
                IsFullAuto = false, IsLongGun = true, UsesFullScreenScope = false, Pellets = 8,
                GripLocalOffset = new Vector3(0.0f, 0.02f, -0.02f),
                GripLocalEuler = Vector3.zero,
                Scale = Vector3.one * 1.05f
            },
            new WeaponSpec
            {
                Id = ShootingWeaponId.TacticalCarbine, ModelName = "OW_Weapon_Carbine",
                NameTr = "5.56 Karabin", NameEn = "5.56 Sport Carbine",
                BuyCost = 380, BaseMag = 30, ExtendedMag = 30,
                FireInterval = 0.108f, ReloadTime = 1.60f,
                RecoilPitch = 1.15f, RecoilYaw = 0.42f,
                HipSpreadDeg = 1.65f, AdsSpreadDeg = 0.32f,
                AdsFov = 36f, AdsSensScale = 0.42f,
                IsFullAuto = true, IsLongGun = true, UsesFullScreenScope = false, Pellets = 1,
                GripLocalOffset = new Vector3(0.0f, 0.01f, -0.01f),
                GripLocalEuler = Vector3.zero,
                Scale = Vector3.one * 1.06f
            },
            new WeaponSpec
            {
                Id = ShootingWeaponId.Sniper100m, ModelName = "OW_Weapon_Sniper",
                NameTr = "100m Keskin Nişancı", NameEn = "100m Bolt Sniper",
                BuyCost = 520, BaseMag = 5, ExtendedMag = 5,
                FireInterval = 1.05f, ReloadTime = 2.05f,
                RecoilPitch = 3.60f, RecoilYaw = 0.45f,
                HipSpreadDeg = 2.60f, AdsSpreadDeg = 0.05f,
                AdsFov = 14f, AdsSensScale = 0.20f,
                IsFullAuto = false, IsLongGun = true, UsesFullScreenScope = true, Pellets = 1,
                GripLocalOffset = new Vector3(0.0f, 0.01f, -0.02f),
                GripLocalEuler = Vector3.zero,
                Scale = Vector3.one * 1.05f
            }
        };

        public static readonly UpgradeSpec[] Upgrades =
        {
            new UpgradeSpec { Id = ShootingUpgradeId.PistolRedDot,      Weapon = ShootingWeaponId.Pistol9mm,       PartName = "Part_Optic_RedDot", NameTr = "Red-Dot Nişangâh",   NameEn = "Red-Dot Reflex",   EffectTr = "-40% Dağılım · Kırmızı Nokta", EffectEn = "-40% Spread · Reflex Sight", Cost = 180 },
            new UpgradeSpec { Id = ShootingUpgradeId.PistolSuppressor,  Weapon = ShootingWeaponId.Pistol9mm,       PartName = "Part_Suppressor",   NameTr = "Taktik Susturucu",   NameEn = "Tactical Suppressor", EffectTr = "-30% Tepme · Sessiz Atış",  EffectEn = "-30% Recoil · Quiet Shot",   Cost = 240 },
            new UpgradeSpec { Id = ShootingUpgradeId.PistolExtMag,      Weapon = ShootingWeaponId.Pistol9mm,       PartName = "",                  NameTr = "Geniş Şarjör (22)",  NameEn = "Extended Mag (22)",   EffectTr = "15 -> 22 Mermi Kapasitesi", EffectEn = "15 -> 22 Round Magazine",    Cost = 200 },

            new UpgradeSpec { Id = ShootingUpgradeId.ShotgunChoke,      Weapon = ShootingWeaponId.TrapShotgun,     PartName = "Part_Choke",        NameTr = "Trap Şok Namlu",     NameEn = "Extended Trap Choke", EffectTr = "-32% Saçma Dağılımı",       EffectEn = "-32% Tighter Pellet Pattern",Cost = 210 },
            new UpgradeSpec { Id = ShootingUpgradeId.ShotgunFastLoad,   Weapon = ShootingWeaponId.TrapShotgun,     PartName = "",                  NameTr = "Hızlı Fişek Dolumu", NameEn = "Speed Loader",        EffectTr = "%40 Daha Hızlı Dolum",      EffectEn = "40% Faster Reload",          Cost = 230 },

            new UpgradeSpec { Id = ShootingUpgradeId.CarbineHolo,       Weapon = ShootingWeaponId.TacticalCarbine, PartName = "Part_Optic_Holo",   NameTr = "Holografik Dürbün",  NameEn = "Holographic Sight",   EffectTr = "Daha Net Zoom & İsabet",    EffectEn = "Sharper ADS Zoom & Accuracy",Cost = 260 },
            new UpgradeSpec { Id = ShootingUpgradeId.CarbineBrake,      Weapon = ShootingWeaponId.TacticalCarbine, PartName = "Part_MuzzleBrake",  NameTr = "Namlu Freni",        NameEn = "Muzzle Compensator",  EffectTr = "-38% Seri Atış Tepmesi",    EffectEn = "-38% Auto-Fire Recoil",      Cost = 240 },
            new UpgradeSpec { Id = ShootingUpgradeId.CarbineLaser,      Weapon = ShootingWeaponId.TacticalCarbine, PartName = "Part_Laser",        NameTr = "3D Taktik Lazer",    NameEn = "3D Tactical Laser",   EffectTr = "Kırmızı Lazer Işını & İsabet", EffectEn = "Live 3D Laser Beam",      Cost = 310 },

            new UpgradeSpec { Id = ShootingUpgradeId.SniperScope8x,     Weapon = ShootingWeaponId.Sniper100m,      PartName = "Part_Scope_8x",     NameTr = "8x Hassas Dürbün",   NameEn = "8x Precision Scope",  EffectTr = "8x Çift Kademe Süper Zoom", EffectEn = "8x Long-Range Optics",       Cost = 340 },
            new UpgradeSpec { Id = ShootingUpgradeId.SniperBipod,       Weapon = ShootingWeaponId.Sniper100m,      PartName = "Part_Bipod",        NameTr = "Taktik Bipod",       NameEn = "Tactical Bipod",      EffectTr = "-70% Dürbün Sallanması",    EffectEn = "-70% Scope Sway",            Cost = 300 },
            new UpgradeSpec { Id = ShootingUpgradeId.SniperHeavyBarrel, Weapon = ShootingWeaponId.Sniper100m,      PartName = "",                  NameTr = "Ağır Maç Namlusu",   NameEn = "Heavy Match Barrel",  EffectTr = "-48% Rüzgâr Sapması",       EffectEn = "-48% Wind Drift",            Cost = 320 }
        };

        // Nested reactive target helpers
        private sealed class SteelTargetUnit
        {
            public GameObject Root;
            public Transform PlatePivot;
            public Vector3 BaseLocalPos;
            public bool IsPenalty;
            public bool IsKnocked;
            public bool MovesOnRail;
            public float RailAmp;
            public float RailSpeed;
            public float RailPhase;
            public float CurrentTiltDeg;
            public float TargetTiltDeg;
        }

        private sealed class ActiveClayDisc
        {
            public GameObject Go;
            public Vector3 Velocity;
            public float Life;
        }

        private sealed class ActiveShellCasing
        {
            public GameObject Go;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float Life;
        }

        private sealed class ActiveTracer
        {
            public LineRenderer Line;
            public float Life;
            public float MaxLife;
        }

        private Transform _facilityRoot;
        private Transform _clayThrowerRoot;
        private Transform _clayThrowerArm;
        private readonly List<Transform> _windFlags = new List<Transform>(4);

        // Runtime targets per mode
        private GameObject _classicTargetRoot;
        private Vector3 _classicBullseyeWorldPos;
        private readonly List<GameObject> _bulletHoles = new List<GameObject>(32);
        private readonly List<SteelTargetUnit> _steelUnits = new List<SteelTargetUnit>(10);
        private readonly List<ActiveClayDisc> _activeClays = new List<ActiveClayDisc>(6);
        private readonly List<ActiveShellCasing> _shells = new List<ActiveShellCasing>(24);
        private readonly List<ActiveTracer> _tracers = new List<ActiveTracer>(24);
        private readonly List<Material> _runtimeMats = new List<Material>(16);

        private Material _brassMat;
        private Material _clayMat;
        private Material _holeMat;
        private Material _tracerMat;
        private Material _laserMat;
        private Material _flashMat;
        private Material _penaltyRedMat;

        // Player & camera references
        private PlayerFootController _player;
        private ThirdPersonCameraRig _cameraRig;
        private Vector3 _beginPos;
        private Quaternion _beginRot;
        private ShootingRangeHud _hud;

        // Equipped weapon instance
        private ShootingWeaponId _currentWeapon = ShootingWeaponId.Pistol9mm;
        private ShootingModeId _currentMode = ShootingModeId.ClassicBullseye;
        private GameObject _weaponInstance;
        private Transform _partSlide;
        private Transform _partPump;
        private Transform _partBolt;
        private Transform _partMag;
        private Transform _partMuzzle;
        private Transform _partEject;
        private Transform _partLaserOrigin;
        private Vector3 _slideRestLocal;
        private Vector3 _pumpRestLocal;
        private Vector3 _boltRestLocal;
        private Vector3 _magRestLocal;
        private LineRenderer _laserBeam;
        private GameObject _muzzleFlashObj;
        private Light _muzzleFlashLight;
        private float _muzzleFlashTimer;

        // Ballistics, ammo, recoil & mode state
        private int[] _magAmmo = { 15, 6, 30, 5 };
        private float _fireCooldown;
        private float _reloadTimer;
        private float _reloadDuration;
        private bool _isReloading;
        private bool _isAds;
        private bool _isHoldingBreath;
        private float _breathStamina = 100f;
        private float _recoilKick01;
        private float _bloomDeg;
        private float _pumpCycleTimer;
        private float _boltCycleTimer;
        private float _throwerArmTimer;

        // Scoring & round state
        private int _score;
        private int _shotsLeft;
        private int _totalRoundShots;
        private int _hitsCount;
        private int _bullseyesCount;
        private int _combo;
        private int _maxCombo;
        private int _ipscWave = 1;
        private float _roundTimer;
        private float _clayAutoTimer;
        private int _claysLaunched;
        private bool _roundFinished;
        private float _windSpeedMs; // positive = East (+X), negative = West (-X)
        private string _calloutText;
        private Color _calloutColor = new Color(1.0f, 0.84f, 0.25f);
        private float _calloutTimer;
        private float _hudTick;

        public bool IsPlayerActive => _player != null;
        public int HudRevision { get; private set; }
        public Vector3 ReturnPosition => _beginPos;
        public Quaternion ReturnRotation => _beginRot;
        public bool AcceptsPrimaryAction => _player != null && !IsArmoryOpen;
        public bool HasSecondaryAction => _player != null && !IsArmoryOpen;

        public bool IsArmoryOpen { get; set; }
        public ShootingWeaponId CurrentWeapon => _currentWeapon;
        public ShootingModeId CurrentMode => _currentMode;
        public bool IsAds => _isAds;
        public bool IsFullScreenScope => _isAds && GetCurrentSpec().UsesFullScreenScope;
        public bool IsReloading => _isReloading;
        public bool IsHoldingBreath => _isHoldingBreath;
        public float BreathFill01 => Mathf.Clamp01(_breathStamina / 100f);
        public int CurrentAmmo => _magAmmo[(int)_currentWeapon];
        public int CurrentMaxAmmo => GetMaxMag(_currentWeapon);
        public float CurrentBloomDeg => _bloomDeg;
        public float WindSpeedMs => _windSpeedMs;
        public int CurrentScore => _score;
        public int CurrentCombo => _combo;

        private static bool IsEn => LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
        private static string L(string tr, string en) => IsEn ? en : tr;
        private static string WeaponName(WeaponSpec spec) => IsEn ? spec.NameEn : spec.NameTr;
        private static string UpgradeName(UpgradeSpec spec) => IsEn ? spec.NameEn : spec.NameTr;
        public bool IsRoundFinished => _roundFinished;
        public float CurrentTargetDistanceMeters => GetTargetDistanceForHud();

        public WeaponSpec GetCurrentSpec() => Weapons[(int)_currentWeapon];

        public int GetMaxMag(ShootingWeaponId id)
        {
            WeaponSpec s = Weapons[(int)id];
            if (id == ShootingWeaponId.Pistol9mm && ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.PistolExtMag))
            {
                return s.ExtendedMag;
            }
            return s.BaseMag;
        }

        public void ConfigureSceneRefs(Transform facilityRoot, Transform clayThrowerRoot)
        {
            _facilityRoot = facilityRoot;
            _clayThrowerRoot = clayThrowerRoot;
            if (_clayThrowerRoot != null)
            {
                _clayThrowerArm = OurWorldModelLibrary.FindPart(_clayThrowerRoot, "Part_Arm");
            }
            EnsureSharedRuntimeAssets();
            BuildWindFlags();
        }

        private void EnsureSharedRuntimeAssets()
        {
            if (_brassMat != null) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            Shader unlit = Shader.Find("Sprites/Default") ?? lit;
            if (lit == null) return;

            _brassMat = MakeMat(lit, "MAT_ShellBrass", new Color(0.92f, 0.72f, 0.22f), 0.72f, 0.85f);
            _clayMat = MakeMat(lit, "MAT_ClayPigeon", new Color(0.98f, 0.36f, 0.06f), 0.40f, 0.05f);
            if (_clayMat.HasProperty("_EmissionColor"))
            {
                _clayMat.EnableKeyword("_EMISSION");
                _clayMat.SetColor("_EmissionColor", new Color(0.35f, 0.10f, 0.01f));
            }
            _holeMat = MakeMat(lit, "MAT_BulletHole", new Color(0.06f, 0.06f, 0.07f), 0.1f, 0f);
            _penaltyRedMat = MakeMat(lit, "MAT_PenaltyPlate", new Color(0.86f, 0.12f, 0.10f), 0.38f, 0.35f);
            _tracerMat = new Material(unlit) { name = "MAT_BulletTracer", color = new Color(1.0f, 0.92f, 0.56f, 0.92f) };
            _laserMat = new Material(unlit) { name = "MAT_TacticalLaser", color = new Color(1.0f, 0.10f, 0.08f, 0.78f) };
            _flashMat = new Material(unlit) { name = "MAT_MuzzleFlash", color = new Color(1.0f, 0.84f, 0.32f, 0.95f) };
            _runtimeMats.Add(_tracerMat);
            _runtimeMats.Add(_laserMat);
            _runtimeMats.Add(_flashMat);
        }

        private Material MakeMat(Shader sh, string name, Color col, float smooth, float metal)
        {
            Material m = new Material(sh) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            else m.color = col;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            _runtimeMats.Add(m);
            return m;
        }

        private void BuildWindFlags()
        {
            if (_facilityRoot == null || _windFlags.Count > 0) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            if (lit == null) return;
            Material poleMat = MakeMat(lit, "MAT_FlagPole", new Color(0.84f, 0.86f, 0.88f), 0.5f, 0.6f);
            Material clothMat = MakeMat(lit, "MAT_WindFlagRed", new Color(0.94f, 0.18f, 0.14f), 0.25f, 0f);

            Vector3[] positions =
            {
                new Vector3(-13.8f, 0f, -28f),
                new Vector3(13.8f, 0f, -28f),
                new Vector3(-13.8f, 0f, -68f),
                new Vector3(13.8f, 0f, -68f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = $"WindFlagPole_{i}";
                Destroy(pole.GetComponent<Collider>());
                pole.transform.SetParent(_facilityRoot, false);
                pole.transform.localPosition = positions[i] + new Vector3(0f, 2.2f, 0f);
                pole.transform.localScale = new Vector3(0.08f, 2.2f, 0.08f);
                pole.GetComponent<MeshRenderer>().sharedMaterial = poleMat;

                GameObject pivot = new GameObject($"WindFlagPivot_{i}");
                pivot.transform.SetParent(_facilityRoot, false);
                pivot.transform.localPosition = positions[i] + new Vector3(0f, 4.15f, 0f);

                GameObject cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(cloth.GetComponent<Collider>());
                cloth.transform.SetParent(pivot.transform, false);
                cloth.transform.localPosition = new Vector3(0.58f, 0f, 0f);
                cloth.transform.localScale = new Vector3(1.15f, 0.42f, 0.04f);
                cloth.GetComponent<MeshRenderer>().sharedMaterial = clothMat;
                _windFlags.Add(pivot.transform);
            }
        }

        // =====================================================================
        // MATCH / SESSION LIFECYCLE
        // =====================================================================
        public void BeginPlayerMatch(PlayerFootController player)
        {
            _player = player;
            _beginPos = ShootingRangeInstaller.RangeCenter + new Vector3(0f, 0.14f, 5.5f);
            _beginRot = Quaternion.Euler(0f, 0f, 0f);
            IsArmoryOpen = false;
            _isAds = false;
            _isHoldingBreath = false;
            _isReloading = false;
            _breathStamina = 100f;

            // Freeze pedestrian gravity/walk physics while standing cleanly in the shooting bay
            _player.SetSeatedState(true);
            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();

            // Initialize magazine capacities cleanly
            for (int i = 0; i < Weapons.Length; i++)
            {
                _magAmmo[i] = GetMaxMag((ShootingWeaponId)i);
            }

            if (_hud == null)
            {
                GameObject hudGo = new GameObject("OurWorld_ShootingRangeHud");
                _hud = hudGo.AddComponent<ShootingRangeHud>();
            }
            _hud.Attach(this);
            _hud.SetVisible(true);

            EquipWeapon(_currentWeapon, quiet: true);
            SelectMode(_currentMode, autoChooseMatchingWeapon: false);
            HudRevision++;
        }

        public void EndPlayerMatch()
        {
            if (_player != null)
            {
                if (_player.Humanoid != null)
                {
                    _player.Humanoid.SetWeaponAimPose(false, false, 0f, 0f, 0f);
                }
                _player.SetSeatedState(false);
                _player.TeleportTo(_beginPos, _beginRot);
            }

            if (_cameraRig != null)
            {
                _cameraRig.SetAimCameraOverride(false, Vector3.zero, 4.6f, 58f);
                _cameraRig.SetSportCameraOverride(false);
                _cameraRig.SnapBehindTargetImmediate();
            }

            if (_weaponInstance != null)
            {
                Destroy(_weaponInstance);
                _weaponInstance = null;
            }
            if (_laserBeam != null)
            {
                Destroy(_laserBeam.gameObject);
                _laserBeam = null;
            }
            if (_muzzleFlashObj != null)
            {
                Destroy(_muzzleFlashObj);
                _muzzleFlashObj = null;
            }
            if (_hud != null)
            {
                _hud.SetVisible(false);
            }

            ClearModeTargets();
            ClearDynamicVfx();
            IsArmoryOpen = false;
            _isAds = false;
            _isHoldingBreath = false;
            _isReloading = false;
            _player = null;
            HudRevision++;
        }

        // =====================================================================
        // WEAPON EQUIPPING & 3D ATTACHMENTS
        // =====================================================================
        public void EquipWeapon(ShootingWeaponId id, bool quiet = false)
        {
            _currentWeapon = id;
            _isReloading = false;
            _isAds = false;
            _isHoldingBreath = false;
            _recoilKick01 = 0f;
            _bloomDeg = GetCurrentSpec().HipSpreadDeg;
            _fireCooldown = 0.15f;

            int idx = (int)id;
            int maxMag = GetMaxMag(id);
            if (_magAmmo[idx] <= 0 || _magAmmo[idx] > maxMag)
            {
                _magAmmo[idx] = maxMag;
            }

            if (_weaponInstance != null)
            {
                Destroy(_weaponInstance);
                _weaponInstance = null;
            }
            if (_laserBeam != null)
            {
                Destroy(_laserBeam.gameObject);
                _laserBeam = null;
            }
            if (_muzzleFlashObj != null)
            {
                Destroy(_muzzleFlashObj);
                _muzzleFlashObj = null;
            }

            WeaponSpec spec = GetCurrentSpec();
            _weaponInstance = OurWorldModelLibrary.InstantiateModel(spec.ModelName, transform, Vector3.zero, Quaternion.identity, spec.Scale, removeColliders: true);
            if (_weaponInstance != null)
            {
                _partSlide = OurWorldModelLibrary.FindPart(_weaponInstance.transform, "Part_Slide");
                _partPump = OurWorldModelLibrary.FindPart(_weaponInstance.transform, "Part_Pump");
                _partBolt = OurWorldModelLibrary.FindPart(_weaponInstance.transform, "Part_Bolt");
                _partMag = OurWorldModelLibrary.FindPart(_weaponInstance.transform, "Part_Mag");
                _partMuzzle = OurWorldModelLibrary.FindPart(_weaponInstance.transform, "Part_Muzzle");
                _partEject = OurWorldModelLibrary.FindPart(_weaponInstance.transform, "Part_Eject");
                _partLaserOrigin = OurWorldModelLibrary.FindPart(_weaponInstance.transform, "Part_Laser");

                if (_partSlide != null) _slideRestLocal = _partSlide.localPosition;
                if (_partPump != null) _pumpRestLocal = _partPump.localPosition;
                if (_partBolt != null) _boltRestLocal = _partBolt.localPosition;
                if (_partMag != null) _magRestLocal = _partMag.localPosition;

                RefreshWeaponAttachmentVisuals();
                BuildMuzzleFlashAndLaser();
            }

            UpdateCameraProfile();
            if (!quiet)
            {
                GameFeelAudio.PlayReloadClick(complete: true);
                bool owned = ShootingRangeStore.IsWeaponOwned(id);
                string tag = owned ? L("Kişisel Silah", "Personal Weapon") : L("Kulüp Deneme Silahı", "Club Rental Weapon");
                SetCallout($"{WeaponName(spec)} · {tag}", new Color(0.46f, 0.90f, 1.0f), 1.4f);
            }
            HudRevision++;
        }

        public void RefreshWeaponAttachmentVisuals()
        {
            if (_weaponInstance == null) return;
            for (int i = 0; i < Upgrades.Length; i++)
            {
                UpgradeSpec u = Upgrades[i];
                if (u.Weapon != _currentWeapon || string.IsNullOrEmpty(u.PartName)) continue;
                Transform part = OurWorldModelLibrary.FindPart(_weaponInstance.transform, u.PartName);
                if (part != null)
                {
                    bool active = ShootingRangeStore.IsWeaponOwned(_currentWeapon) && ShootingRangeStore.IsUpgradeOwned(u.Id);
                    part.gameObject.SetActive(active);
                }
            }

            // Refresh magazine if extended mag was just bought
            int idx = (int)_currentWeapon;
            int maxMag = GetMaxMag(_currentWeapon);
            if (_magAmmo[idx] < maxMag && !_isReloading)
            {
                _magAmmo[idx] = maxMag;
            }
            UpdateCameraProfile();
            HudRevision++;
        }

        private void BuildMuzzleFlashAndLaser()
        {
            if (_weaponInstance == null) return;
            EnsureSharedRuntimeAssets();

            // Muzzle flash glowing starburst + short point light
            _muzzleFlashObj = new GameObject("MuzzleFlashFX");
            _muzzleFlashObj.transform.SetParent(_weaponInstance.transform, false);
            Transform anchor = _partMuzzle != null ? _partMuzzle : _weaponInstance.transform;
            _muzzleFlashObj.transform.position = anchor.position;

            for (int i = 0; i < 3; i++)
            {
                GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(blade.GetComponent<Collider>());
                blade.transform.SetParent(_muzzleFlashObj.transform, false);
                blade.transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
                blade.transform.localScale = new Vector3(0.18f, 0.042f, 0.042f);
                MeshRenderer mr = blade.GetComponent<MeshRenderer>();
                if (mr != null && _flashMat != null)
                {
                    mr.sharedMaterial = _flashMat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            _muzzleFlashLight = _muzzleFlashObj.AddComponent<Light>();
            _muzzleFlashLight.type = LightType.Point;
            _muzzleFlashLight.color = new Color(1.0f, 0.78f, 0.35f);
            _muzzleFlashLight.intensity = 4.5f;
            _muzzleFlashLight.range = 6.5f;
            _muzzleFlashLight.shadows = LightShadows.None;
            _muzzleFlashObj.SetActive(false);

            // Live 3D Tactical Laser Beam (only rendered when CarbineLaser upgrade is owned!)
            GameObject laserGo = new GameObject("TacticalLaserBeam");
            laserGo.transform.SetParent(transform, false);
            _laserBeam = laserGo.AddComponent<LineRenderer>();
            _laserBeam.positionCount = 2;
            _laserBeam.startWidth = 0.008f;
            _laserBeam.endWidth = 0.004f;
            _laserBeam.sharedMaterial = _laserMat;
            _laserBeam.shadowCastingMode = ShadowCastingMode.Off;
            _laserBeam.enabled = false;
        }

        // =====================================================================
        // MODE SWITCHING & TARGET SPAWNING (4 MODES)
        // =====================================================================
        public void SelectMode(ShootingModeId mode, bool autoChooseMatchingWeapon = true)
        {
            _currentMode = mode;
            _roundFinished = false;
            _score = 0;
            _hitsCount = 0;
            _bullseyesCount = 0;
            _combo = 0;
            _maxCombo = 0;
            _ipscWave = 1;
            _claysLaunched = 0;
            _clayAutoTimer = 1.4f;
            _isAds = false;
            _isHoldingBreath = false;

            if (autoChooseMatchingWeapon)
            {
                if (mode == ShootingModeId.TrapClay && _currentWeapon != ShootingWeaponId.TrapShotgun)
                    EquipWeapon(ShootingWeaponId.TrapShotgun, quiet: true);
                else if (mode == ShootingModeId.SniperWind100m && _currentWeapon != ShootingWeaponId.Sniper100m)
                    EquipWeapon(ShootingWeaponId.Sniper100m, quiet: true);
            }

            // Place player at the exact firing bay for the chosen discipline
            Vector3 bayLocalPos = mode switch
            {
                ShootingModeId.ClassicBullseye => new Vector3(-3.0f, 0.13f, -1.75f), // Bay 2 (25m lane)
                ShootingModeId.IpscSteel       => new Vector3(3.0f, 0.13f, -1.75f),  // Bay 3 (IPSC)
                ShootingModeId.TrapClay        => new Vector3(24.0f, 0.10f, -2.5f),  // Open-Sky East Wing Trap Pad
                _                              => new Vector3(9.0f, 0.13f, -1.75f)   // Bay 4 (100m Sniper)
            };

            if (_player != null && _facilityRoot != null)
            {
                Vector3 worldStand = _facilityRoot.TransformPoint(bayLocalPos);
                _player.TeleportTo(worldStand, Quaternion.Euler(0f, 180f, 0f));
                if (_player.Humanoid != null)
                {
                    _player.Humanoid.SetLocomotion(0f, true, 0f);
                }
            }

            // Pick authentic wind vector for Sniper mode (calm breeze in indoor/short modes)
            _windSpeedMs = mode == ShootingModeId.SniperWind100m
                ? (UnityEngine.Random.value > 0.5f ? 1f : -1f) * UnityEngine.Random.Range(2.0f, 5.8f)
                : UnityEngine.Random.Range(-0.8f, 0.8f);

            ClearModeTargets();
            if (_hud != null)
            {
                _hud.ClearTargetCardDots();
                _hud.ClearPopup();
            }

            switch (mode)
            {
                case ShootingModeId.ClassicBullseye:
                    _totalRoundShots = 10;
                    _shotsLeft = 10;
                    _roundTimer = 0f;
                    // Align dead-center with over-the-shoulder aim line (-0.50m world X when facing South!)
                    SpawnClassicBullseyeTarget(new Vector3(-3.50f, 0.02f, -24.5f), 1.0f);
                    SetCallout(L("KLASİK HEDEF · 10 Atış · 10X Bullseye Avı!", "CLASSIC BULLSEYE · 10 Shots · Hunt the 10X Ring!"), new Color(0.45f, 0.94f, 0.88f), 2.0f);
                    VoiceoverManager.PlayCallout("range_start_classic", 0.3f);
                    break;

                case ShootingModeId.IpscSteel:
                    _totalRoundShots = 0;
                    _shotsLeft = 999;
                    _roundTimer = 60.0f;
                    SpawnIpscSteelCourse();
                    GameFeelAudio.PlayRangeBuzzer();
                    SetCallout(L("IPSC ÇELİK PLAKA · 60 sn · KIRMIZIYA ATEŞ ETME!", "IPSC STEEL PLATES · 60s · DON'T SHOOT RED!"), new Color(1.0f, 0.82f, 0.24f), 2.2f);
                    VoiceoverManager.PlayCallout("range_start_ipsc", 0.3f);
                    break;

                case ShootingModeId.TrapClay:
                    _totalRoundShots = 12;
                    _shotsLeft = 12;
                    _roundTimer = 0f;
                    SetCallout(L("TRAP / SKEET · 12 Kil Plak · 'ÇEK!' veya Otomatik Fırlatma!", "TRAP / SKEET · 12 Clay Pigeons · Tap 'PULL!' or Auto-Launch!"), new Color(1.0f, 0.58f, 0.20f), 2.2f);
                    VoiceoverManager.PlayCallout("range_start_trap", 0.3f);
                    break;

                case ShootingModeId.SniperWind100m:
                    _totalRoundShots = 8;
                    _shotsLeft = 8;
                    _roundTimer = 0f;
                    // Large high-visibility precision target at 96m aligned straight with Bay 4 center scope axis!
                    SpawnClassicBullseyeTarget(new Vector3(9.0f, 0.02f, -99.0f), 1.85f);
                    string windDirLabel = _windSpeedMs >= 0 ? L("→ DOĞU", "→ EAST") : L("← BATI", "← WEST");
                    SetCallout(L($"100m KESKİN NİŞANCI · Rüzgâr {Mathf.Abs(_windSpeedMs):F1} m/s {windDirLabel}", $"100m SNIPER · Wind {Mathf.Abs(_windSpeedMs):F1} m/s {windDirLabel}"), new Color(0.76f, 0.90f, 1.0f), 2.5f);
                    VoiceoverManager.PlayCallout("range_start_sniper", 0.3f);
                    break;
            }

            if (_cameraRig != null)
            {
                float initialPitch = mode == ShootingModeId.TrapClay ? -8.5f : 0.8f;
                UpdateCameraProfile();
                _cameraRig.SetAimAngles(180f, initialPitch);
            }
            HudRevision++;
        }

        private void ClearModeTargets()
        {
            if (_classicTargetRoot != null)
            {
                Destroy(_classicTargetRoot);
                _classicTargetRoot = null;
            }
            for (int i = 0; i < _bulletHoles.Count; i++)
            {
                if (_bulletHoles[i] != null) Destroy(_bulletHoles[i]);
            }
            _bulletHoles.Clear();

            for (int i = 0; i < _steelUnits.Count; i++)
            {
                if (_steelUnits[i].Root != null) Destroy(_steelUnits[i].Root);
            }
            _steelUnits.Clear();

            for (int i = 0; i < _activeClays.Count; i++)
            {
                if (_activeClays[i].Go != null) Destroy(_activeClays[i].Go);
            }
            _activeClays.Clear();
        }

        private void ClearDynamicVfx()
        {
            for (int i = 0; i < _shells.Count; i++)
            {
                if (_shells[i].Go != null) Destroy(_shells[i].Go);
            }
            _shells.Clear();
            for (int i = 0; i < _tracers.Count; i++)
            {
                if (_tracers[i].Line != null) Destroy(_tracers[i].Line.gameObject);
            }
            _tracers.Clear();
        }

        private void SpawnClassicBullseyeTarget(Vector3 localPos, float scale)
        {
            if (_facilityRoot == null) return;
            _classicTargetRoot = OurWorldModelLibrary.InstantiateModel(
                "OW_Range_TargetStand", _facilityRoot,
                localPos, Quaternion.identity, Vector3.one * scale, removeColliders: true);
            if (_classicTargetRoot == null)
            {
                _classicTargetRoot = new GameObject("FallbackTargetStand");
                _classicTargetRoot.transform.SetParent(_facilityRoot, false);
                _classicTargetRoot.transform.localPosition = localPos;
            }

            // Crisp BoxCollider matching the target paper face for exact raycast hit coordinates!
            BoxCollider bc = _classicTargetRoot.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 1.44f, 0.03f);
            bc.size = new Vector3(0.84f, 0.96f, 0.14f);
            _classicBullseyeWorldPos = _classicTargetRoot.transform.TransformPoint(new Vector3(0f, 1.44f, 0.048f));
        }

        private void SpawnIpscSteelCourse()
        {
            if (_facilityRoot == null) return;
            // 8 white reactive steel plates + 2 red NO-SHOOT penalty plates across staggered 14m..36m bays!
            (Vector3 pos, bool moving, bool penalty)[] specs =
            {
                (new Vector3(-3.5f, 0.02f, -16.5f), false, false),
                (new Vector3( 0.5f, 0.02f, -19.0f), false, false),
                (new Vector3( 4.8f, 0.02f, -17.5f), false, false),
                (new Vector3( 8.8f, 0.02f, -22.5f), false, false),
                (new Vector3(-1.5f, 0.02f, -27.0f), true,  false),
                (new Vector3( 6.2f, 0.02f, -31.5f), true,  false),
                (new Vector3( 2.8f, 0.02f, -36.5f), false, false),
                (new Vector3(-5.2f, 0.02f, -33.0f), false, false),
                // Two red "NO-SHOOT" penalty plates requiring clean trigger discipline!
                (new Vector3( 2.6f, 0.02f, -21.5f), false, true),
                (new Vector3( 7.6f, 0.02f, -27.5f), false, true)
            };

            for (int i = 0; i < specs.Length; i++)
            {
                var sp = specs[i];
                GameObject go = OurWorldModelLibrary.InstantiateModel(
                    "OW_Range_SteelPlate", _facilityRoot,
                    sp.pos, Quaternion.identity, Vector3.one * 1.15f, removeColliders: true);
                if (go == null) continue;

                Transform pivot = OurWorldModelLibrary.FindPart(go.transform, "Part_Plate");
                if (sp.penalty && pivot != null)
                {
                    Renderer[] rends = pivot.GetComponentsInChildren<Renderer>(true);
                    for (int r = 0; r < rends.Length; r++)
                    {
                        rends[r].sharedMaterial = _penaltyRedMat;
                    }
                }

                BoxCollider col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.72f, 0f);
                col.size = new Vector3(0.62f, 1.18f, 0.24f);

                _steelUnits.Add(new SteelTargetUnit
                {
                    Root = go,
                    PlatePivot = pivot,
                    BaseLocalPos = sp.pos,
                    IsPenalty = sp.penalty,
                    IsKnocked = false,
                    MovesOnRail = sp.moving,
                    RailAmp = 2.6f,
                    RailSpeed = 1.45f + (i % 2) * 0.45f,
                    RailPhase = i * 1.7f
                });
            }
        }

        public void LaunchClayPigeon()
        {
            if (_player == null || _currentMode != ShootingModeId.TrapClay || _roundFinished) return;
            if (_claysLaunched >= _totalRoundShots) return;
            EnsureSharedRuntimeAssets();

            _claysLaunched++;
            _clayAutoTimer = 3.5f;
            _throwerArmTimer = 0.28f;

            Vector3 spawnPos = _facilityRoot != null
                ? _facilityRoot.TransformPoint(new Vector3(24.0f, 1.45f, -18.8f))
                : transform.position + Vector3.forward * -12f;

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = $"ClayPigeon_{_claysLaunched}";
            disc.transform.position = spawnPos;
            disc.transform.localScale = new Vector3(0.46f, 0.055f, 0.46f);
            disc.GetComponent<MeshRenderer>().sharedMaterial = _clayMat;

            // Generous sphere collider for fun & fair mobile shotgun/pistol hits!
            Destroy(disc.GetComponent<Collider>());
            SphereCollider sc = disc.AddComponent<SphereCollider>();
            sc.radius = 0.78f;

            // Varied authentic Olympic Trap arc (left, center, right)
            float sideVel = UnityEngine.Random.Range(-6.2f, 6.2f);
            float upVel = UnityEngine.Random.Range(9.4f, 12.2f);
            float downrangeVel = -UnityEngine.Random.Range(14.5f, 19.0f);

            _activeClays.Add(new ActiveClayDisc
            {
                Go = disc,
                Velocity = new Vector3(sideVel, upVel, downrangeVel),
                Life = 4.2f
            });

            GameFeelAudio.PlaySlideWhoosh();
            SetCallout(L($"ÇEK! Plak #{_claysLaunched}/{_totalRoundShots} Havada!", $"PULL! Clay #{_claysLaunched}/{_totalRoundShots} Airborne!"), new Color(1.0f, 0.65f, 0.18f), 1.0f);
            HudRevision++;
        }

        // =====================================================================
        // INPUT ACTIONS (IPlayableSportArena + KEYBOARD + HUD BUTTONS)
        // =====================================================================
        public void TriggerPrimaryAction()
        {
            if (_player == null || IsArmoryOpen) return;
            if (_roundFinished)
            {
                SelectMode(_currentMode, autoChooseMatchingWeapon: false);
                return;
            }
            TryFireCurrentWeapon();
        }

        public void TriggerSecondaryAction()
        {
            if (_player == null || IsArmoryOpen) return;
            ToggleAds();
        }

        public void TriggerPrimaryRelease(float holdSeconds) { }
        public void TriggerSecondaryRelease(float holdSeconds) { }

        public void ToggleAds()
        {
            if (_isReloading) return;
            _isAds = !_isAds;
            if (!_isAds) _isHoldingBreath = false;
            UpdateCameraProfile();
            GameFeelAudio.PlayUiClick();
            HudRevision++;
        }

        public void ToggleBreathHold()
        {
            if (!_isAds || _breathStamina < 12f)
            {
                _isHoldingBreath = false;
                return;
            }
            _isHoldingBreath = !_isHoldingBreath;
            HudRevision++;
        }

        public void StartReload()
        {
            if (_isReloading || _player == null) return;
            int idx = (int)_currentWeapon;
            int maxMag = GetMaxMag(_currentWeapon);
            if (_magAmmo[idx] >= maxMag) return;

            _isReloading = true;
            _isAds = false;
            _isHoldingBreath = false;
            WeaponSpec spec = GetCurrentSpec();
            float dur = spec.ReloadTime;
            if (_currentWeapon == ShootingWeaponId.TrapShotgun &&
                ShootingRangeStore.IsWeaponOwned(_currentWeapon) &&
                ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.ShotgunFastLoad))
            {
                dur *= 0.60f;
            }
            _reloadDuration = dur;
            _reloadTimer = dur;
            UpdateCameraProfile();
            GameFeelAudio.PlayReloadClick(complete: false);
            SetCallout(L("ŞARJÖR DEĞİŞTİRİLİYOR...", "RELOADING..."), new Color(0.82f, 0.90f, 1.0f), dur);
            HudRevision++;
        }

        public bool TryBuyWeapon(ShootingWeaponId id)
        {
            if (ShootingRangeStore.IsWeaponOwned(id)) return true;
            int cost = Weapons[(int)id].BuyCost;
            if (SaveManager.Instance != null && SaveManager.Instance.TrySpend(cost))
            {
                ShootingRangeStore.SetWeaponOwned(id);
                GameFeelAudio.PlayMilestoneFanfare();
                EquipWeapon(id, quiet: true);
                SetCallout(L($"{WeaponName(Weapons[(int)id])} SATIN ALINDI! (+%30 Ödül & Eklentiler Açık)", $"{WeaponName(Weapons[(int)id])} PURCHASED! (+30% Bonus & Upgrades Unlocked)"), new Color(0.35f, 0.96f, 0.54f), 2.2f);
                HudRevision++;
                return true;
            }
            GameFeelAudio.PlayEmptyClick();
            SetCallout(L($"Yetersiz Bakiye! ({cost} ₺ gerekli)", $"Insufficient Funds! ({cost} ₺ needed)"), new Color(1.0f, 0.42f, 0.36f), 1.6f);
            return false;
        }

        public bool TryBuyUpgrade(ShootingUpgradeId upgId)
        {
            UpgradeSpec spec = Upgrades[(int)upgId];
            if (!ShootingRangeStore.IsWeaponOwned(spec.Weapon))
            {
                SetCallout(L("Önce bu silahı satın almalısın!", "You need to buy this weapon first!"), new Color(1.0f, 0.68f, 0.25f), 1.6f);
                return false;
            }
            if (ShootingRangeStore.IsUpgradeOwned(upgId)) return true;
            if (SaveManager.Instance != null && SaveManager.Instance.TrySpend(spec.Cost))
            {
                ShootingRangeStore.SetUpgradeOwned(upgId);
                GameFeelAudio.PlayCoinChime();
                if (_currentWeapon == spec.Weapon)
                {
                    RefreshWeaponAttachmentVisuals();
                }
                SetCallout(L($"{UpgradeName(spec)} Takıldı!", $"{UpgradeName(spec)} Installed!"), new Color(0.35f, 0.96f, 0.54f), 1.8f);
                HudRevision++;
                return true;
            }
            GameFeelAudio.PlayEmptyClick();
            SetCallout(L($"Yetersiz Bakiye! ({spec.Cost} ₺ gerekli)", $"Insufficient Funds! ({spec.Cost} ₺ needed)"), new Color(1.0f, 0.42f, 0.36f), 1.6f);
            return false;
        }

        // =====================================================================
        // FIRING, BALLISTICS & HIT DETECTION
        // =====================================================================
        private void TryFireCurrentWeapon()
        {
            if (_fireCooldown > 0f || _isReloading || _cameraRig == null) return;
            if (_currentMode == ShootingModeId.ClassicBullseye || _currentMode == ShootingModeId.SniperWind100m)
            {
                if (_shotsLeft <= 0) return;
            }

            int idx = (int)_currentWeapon;
            if (_magAmmo[idx] <= 0)
            {
                GameFeelAudio.PlayEmptyClick();
                StartReload();
                return;
            }

            WeaponSpec spec = GetCurrentSpec();
            bool owns = ShootingRangeStore.IsWeaponOwned(_currentWeapon);
            bool hasRedDot = owns && _currentWeapon == ShootingWeaponId.Pistol9mm && ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.PistolRedDot);
            bool hasSupp = owns && _currentWeapon == ShootingWeaponId.Pistol9mm && ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.PistolSuppressor);
            bool hasChoke = owns && _currentWeapon == ShootingWeaponId.TrapShotgun && ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.ShotgunChoke);
            bool hasHolo = owns && _currentWeapon == ShootingWeaponId.TacticalCarbine && ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.CarbineHolo);
            bool hasBrake = owns && _currentWeapon == ShootingWeaponId.TacticalCarbine && ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.CarbineBrake);
            bool hasLaser = owns && _currentWeapon == ShootingWeaponId.TacticalCarbine && ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.CarbineLaser);
            bool hasHeavy = owns && _currentWeapon == ShootingWeaponId.Sniper100m && ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.SniperHeavyBarrel);

            _magAmmo[idx]--;
            _fireCooldown = spec.FireInterval;
            _recoilKick01 = 1.0f;

            if (_currentWeapon == ShootingWeaponId.TrapShotgun) _pumpCycleTimer = 0.46f;
            if (_currentWeapon == ShootingWeaponId.Sniper100m) _boltCycleTimer = 0.78f;

            // Play authentic procedural weapon sound
            switch (_currentWeapon)
            {
                case ShootingWeaponId.Pistol9mm:       GameFeelAudio.PlayGunshotPistol(hasSupp); break;
                case ShootingWeaponId.TrapShotgun:     GameFeelAudio.PlayGunshotShotgun(); break;
                case ShootingWeaponId.TacticalCarbine: GameFeelAudio.PlayGunshotCarbine(hasBrake); break;
                case ShootingWeaponId.Sniper100m:      GameFeelAudio.PlayGunshotSniper(); break;
            }

            // Muzzle flash & ejecting brass shell casing
            TriggerMuzzleFlash(!hasSupp);
            SpawnBrassShell();

            // Calculate effective spread
            float spread = _isAds ? spec.AdsSpreadDeg : spec.HipSpreadDeg;
            if (hasRedDot) spread *= 0.60f;
            if (hasChoke) spread *= 0.68f;
            if (hasHolo) spread *= 0.72f;
            if (hasLaser) spread *= 0.75f;
            spread += _bloomDeg * 0.25f;

            Ray aimRay = _cameraRig.GetAimRay();
            Vector3 muzzlePos = _partMuzzle != null ? _partMuzzle.position : (aimRay.origin + aimRay.direction * 0.6f);

            int pellets = Mathf.Max(1, spec.Pellets);
            bool anyHitThisShot = false;
            bool bestBullseye = false;
            int shotPointsGained = 0;

            for (int p = 0; p < pellets; p++)
            {
                Vector3 dir = aimRay.direction;
                if (spread > 0.01f)
                {
                    float rx = UnityEngine.Random.Range(-spread, spread);
                    float ry = UnityEngine.Random.Range(-spread, spread);
                    dir = (Quaternion.AngleAxis(rx, _cameraRig.transform.right) * Quaternion.AngleAxis(ry, Vector3.up)) * dir;
                    dir.Normalize();
                }

                // Real ballistic wind drift & subtle gravity drop on 100m Sniper shots!
                Vector3 ballisticOffset = Vector3.zero;
                if (_currentMode == ShootingModeId.SniperWind100m)
                {
                    float windFactor = hasHeavy ? 0.036f : 0.072f;
                    float dropMeters = hasHeavy ? 0.08f : 0.14f;
                    ballisticOffset = new Vector3(_windSpeedMs * windFactor, -dropMeters, 0f);
                }

                Vector3 rayOrigin = aimRay.origin;
                Vector3 targetPoint = rayOrigin + dir * 130f + ballisticOffset;
                Vector3 shotDir = (targetPoint - rayOrigin).normalized;

                if (Physics.Raycast(rayOrigin, shotDir, out RaycastHit hit, 145f, ~0, QueryTriggerInteraction.Ignore))
                {
                    SpawnBulletTracer(muzzlePos, hit.point);
                    if (EvaluateShotHit(hit, ref shotPointsGained, ref bestBullseye))
                    {
                        anyHitThisShot = true;
                    }
                    else
                    {
                        // Sand berm / wood impact dust puff
                        GameFeelAudio.SpawnBurst(hit.point, new Color(0.86f, 0.78f, 0.62f), 5, 0.35f);
                    }
                }
                else
                {
                    SpawnBulletTracer(muzzlePos, rayOrigin + shotDir * 115f);
                }
            }

            // Apply crisp weapon recoil kick to camera & dynamic crosshair bloom
            float recoilMult = 1f;
            if (hasSupp) recoilMult *= 0.70f;
            if (hasBrake) recoilMult *= 0.62f;
            if (_isAds) recoilMult *= 0.78f;

            _cameraRig.ApplyAimKick(
                spec.RecoilPitch * recoilMult,
                UnityEngine.Random.Range(-spec.RecoilYaw, spec.RecoilYaw) * recoilMult);
            _cameraRig.AddShake(0.025f * spec.RecoilPitch * recoilMult, 0.09f);
            _bloomDeg = Mathf.Min(6.5f, _bloomDeg + spec.RecoilPitch * 0.55f);

            // Round shot tracking for Classic & 100m Sniper
            if (_currentMode == ShootingModeId.ClassicBullseye || _currentMode == ShootingModeId.SniperWind100m)
            {
                _shotsLeft = Mathf.Max(0, _shotsLeft - 1);
                if (!anyHitThisShot)
                {
                    _combo = 0;
                    _hud?.ShowPopup(L("ISKA!", "MISS!"), new Color(0.85f, 0.45f, 0.42f));
                }
                if (_shotsLeft <= 0)
                {
                    FinishCurrentRound();
                }
            }

            if (_magAmmo[idx] == 0 && !_roundFinished)
            {
                StartReload();
            }
            HudRevision++;
        }

        private bool EvaluateShotHit(RaycastHit hit, ref int shotPoints, ref bool isBullseye)
        {
            if (hit.collider == null) return false;
            Transform hitTr = hit.collider.transform;

            // 1. Classic / 100m Sniper Paper Bullseye Target
            if (_classicTargetRoot != null && (hitTr == _classicTargetRoot.transform || hitTr.IsChildOf(_classicTargetRoot.transform)))
            {
                Vector3 localHit = _classicTargetRoot.transform.InverseTransformPoint(hit.point);
                float dx = localHit.x;
                float dy = localHit.y - 1.44f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                int pts;
                if (r <= 0.042f)      { pts = 11; isBullseye = true; } // 10X Inner Bullseye!
                else if (r <= 0.105f) { pts = 10; isBullseye = true; }
                else if (r <= 0.195f) { pts = 8; }
                else if (r <= 0.275f) { pts = 6; }
                else if (r <= 0.355f) { pts = 4; }
                else                  { pts = 2; }

                _score += pts;
                shotPoints += pts;
                _hitsCount++;
                if (isBullseye) { _bullseyesCount++; _combo++; }
                else { _combo = 0; }
                _maxCombo = Mathf.Max(_maxCombo, _combo);

                // Stamp 3D bullet hole decal right onto the paper face
                PlantBulletHole(hit.point + hit.normal * 0.008f, hit.normal);

                // Plot exact normalized impact dot on HUD Zoomed Target Card (-1..+1 where 1 = outer ring 0.35m)
                _hud?.AddTargetCardDot(new Vector2(dx / 0.35f, dy / 0.35f), isBullseye);
                _hud?.TriggerHitmarker(isBullseye ? 2 : 1);
                _hud?.ShowPopup(pts == 11 ? L("10X TAM İSABET! (+11)", "10X BULLSEYE! (+11)") : L($"+{pts} PUAN", $"+{pts} PTS"), isBullseye ? new Color(1.0f, 0.86f, 0.20f) : Color.white);
                GameFeelAudio.PlayBulletImpactPaper(isBullseye);
                GameFeelAudio.SpawnBurst(hit.point, isBullseye ? new Color(1.0f, 0.85f, 0.22f) : Color.white, isBullseye ? 10 : 5, 0.28f);
                if (pts == 11)
                {
                    VoiceoverManager.PlayCallout("range_bullseye", 2.0f, 0.88f);
                }
                return true;
            }

            // 2. IPSC Reactive Hinged Steel Plates
            for (int i = 0; i < _steelUnits.Count; i++)
            {
                SteelTargetUnit u = _steelUnits[i];
                if (u.Root == null || u.IsKnocked) continue;
                if (hitTr == u.Root.transform || hitTr.IsChildOf(u.Root.transform))
                {
                    u.IsKnocked = true;
                    u.TargetTiltDeg = -84f;

                    if (u.IsPenalty)
                    {
                        _score = Mathf.Max(0, _score - 15);
                        _combo = 0;
                        _hud?.TriggerHitmarker(3);
                        _hud?.ShowPopup(L("KIRMIZI HEDEF! -15", "NO-SHOOT TARGET! -15"), new Color(1.0f, 0.24f, 0.20f));
                        GameFeelAudio.PlayRangeBuzzer();
                        GameFeelAudio.SpawnBurst(hit.point, new Color(1.0f, 0.20f, 0.16f), 10, 0.45f);
                        return true;
                    }

                    _combo++;
                    _maxCombo = Mathf.Max(_maxCombo, _combo);
                    _hitsCount++;
                    int mult = Mathf.Min(5, 1 + (_combo - 1) / 2);
                    int pts = 15 * mult;
                    _score += pts;
                    shotPoints += pts;

                    _hud?.TriggerHitmarker(mult >= 3 ? 2 : 1);
                    _hud?.ShowPopup(mult > 1 ? L($"ÇELİK! x{mult} (+{pts})", $"STEEL! x{mult} (+{pts})") : L($"ÇELİK! (+{pts})", $"STEEL! (+{pts})"), mult >= 3 ? new Color(1.0f, 0.86f, 0.20f) : new Color(0.45f, 0.96f, 0.86f));
                    GameFeelAudio.PlaySteelPlatePing(_combo);
                    GameFeelAudio.SpawnBurst(hit.point, new Color(1.0f, 0.84f, 0.30f), 12, 0.48f);

                    CheckIpscWaveClear();
                    return true;
                }
            }

            // 3. Trap / Skeet Flying Orange Clay Pigeons
            for (int i = _activeClays.Count - 1; i >= 0; i--)
            {
                ActiveClayDisc clay = _activeClays[i];
                if (clay.Go == null) continue;
                if (hitTr == clay.Go.transform)
                {
                    Vector3 pos = clay.Go.transform.position;
                    Destroy(clay.Go);
                    _activeClays.RemoveAt(i);

                    _hitsCount++;
                    _combo++;
                    _maxCombo = Mathf.Max(_maxCombo, _combo);
                    int pts = 25 + Mathf.Min(15, (_combo - 1) * 5);
                    _score += pts;
                    shotPoints += pts;

                    _hud?.TriggerHitmarker(2);
                    _hud?.ShowPopup(_combo >= 3 ? L($"PLAK PARÇALANDI! x{_combo} (+{pts})", $"CLAY SHATTERED! x{_combo} (+{pts})") : L($"PLAK PARÇALANDI! (+{pts})", $"CLAY SHATTERED! (+{pts})"), new Color(1.0f, 0.64f, 0.18f));
                    GameFeelAudio.PlayClayShatter();
                    GameFeelAudio.SpawnBurst(pos, new Color(1.0f, 0.44f, 0.08f), 16, 0.85f);
                    GameFeelAudio.TriggerHitstop(0.022f);

                    if (_claysLaunched >= _totalRoundShots && _activeClays.Count == 0)
                    {
                        FinishCurrentRound();
                    }
                    return true;
                }
            }

            return false;
        }

        private void CheckIpscWaveClear()
        {
            for (int i = 0; i < _steelUnits.Count; i++)
            {
                if (!_steelUnits[i].IsPenalty && !_steelUnits[i].IsKnocked) return;
            }

            // All valid steel plates cleared -> wave bonus & hydraulic pop-up reset!
            _ipscWave++;
            _score += 50;
            GameFeelAudio.PlayMilestoneFanfare();
            SetCallout(L($"DALGA {_ipscWave - 1} TEMİZLENDİ! +50 BONUS · Yeni Hedefler Yükseliyor!", $"WAVE {_ipscWave - 1} CLEARED! +50 BONUS · Resetting Targets!"), new Color(0.38f, 0.96f, 0.56f), 1.8f);
            for (int i = 0; i < _steelUnits.Count; i++)
            {
                _steelUnits[i].IsKnocked = false;
                _steelUnits[i].TargetTiltDeg = 0f;
            }
        }

        private void FinishCurrentRound()
        {
            if (_roundFinished) return;
            _roundFinished = true;
            _isAds = false;
            _isHoldingBreath = false;
            UpdateCameraProfile();

            // Medal tiers per mode
            (int bronze, int silver, int gold) = _currentMode switch
            {
                ShootingModeId.ClassicBullseye => (55, 74, 88),
                ShootingModeId.IpscSteel       => (180, 320, 480),
                ShootingModeId.TrapClay        => (125, 200, 275),
                _                              => (44, 60, 74)
            };

            string medal;
            int cash;
            bool won;
            if (_score >= gold)        { medal = L("ALTIN MADALYA", "GOLD MEDAL"); cash = 120; won = true; }
            else if (_score >= silver) { medal = L("GÜMÜŞ MADALYA", "SILVER MEDAL"); cash = 80;  won = true; }
            else if (_score >= bronze) { medal = L("BRONZ MADALYA", "BRONZE MEDAL"); cash = 50;  won = true; }
            else                       { medal = L("ANTRENMAN TAMAM", "TRAINING COMPLETE"); cash = 20; won = false; }

            if (ShootingRangeStore.IsWeaponOwned(_currentWeapon))
            {
                cash = Mathf.RoundToInt(cash * 1.30f); // +30% personal weapon owner bonus!
            }

            bool isNewRecord = ShootingRangeStore.RecordScore(_currentMode, _score);
            SaveManager.Instance?.AddMoney(cash);
            SportMatchEvents.NotifyMatchFinished("sport_shooting_range", won);

            if (won)
            {
                GameFeelAudio.PlayMilestoneFanfare();
                GameFeelAudio.PlayCrowdCheer(bigCheer: _score >= gold);
                VoiceoverManager.PlayCallout(_score >= gold ? "range_gold" : (_score >= silver ? "range_silver" : "range_bronze"), 0.2f);
            }
            else
            {
                GameFeelAudio.PlayRangeBuzzer();
                VoiceoverManager.PlayCallout("range_done", 0.2f);
            }

            string rec = isNewRecord ? L(" · YENİ REKOR!", " · NEW RECORD!") : "";
            SetCallout(L($"{medal}! Skor: {_score}{rec} · +{cash} ₺", $"{medal}! Score: {_score}{rec} · +{cash} ₺"), won ? new Color(1.0f, 0.85f, 0.22f) : new Color(0.75f, 0.86f, 0.96f), 4.5f);
            HudRevision++;
        }

        // =====================================================================
        // UPDATE & VISUAL EFFECTS
        // =====================================================================
        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Animate wind flags continuously (even when observing the range from outside!)
            float windDir = _windSpeedMs >= 0f ? 0f : 180f;
            float windMag = Mathf.Clamp(Mathf.Abs(_windSpeedMs), 0.6f, 6.5f);
            for (int i = 0; i < _windFlags.Count; i++)
            {
                if (_windFlags[i] == null) continue;
                float flutter = Mathf.Sin(Time.time * (4.5f + windMag * 1.6f) + i * 1.3f) * (6f + windMag * 2.2f);
                _windFlags[i].localRotation = Quaternion.Euler(0f, windDir + flutter, 0f);
            }

            if (_player == null) return;

            UpdateTimersAndVfx(dt);
            UpdateModeSimulation(dt);
            UpdatePlayerInputAndAim(dt);
            UpdateCharacterAndWeaponPose(dt);

            _hudTick += dt;
            if (_hudTick >= 0.12f)
            {
                _hudTick = 0f;
                HudRevision++;
            }
        }

        private void UpdatePlayerInputAndAim(float dt)
        {
            if (IsArmoryOpen) return;

            // Desktop keyboard shortcuts (R = Reload, 1-4 = Weapons, Shift = Breath, C = Pull Clay, Right-Click = ADS)
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame) StartReload();
                if (kb.digit1Key.wasPressedThisFrame) EquipWeapon(ShootingWeaponId.Pistol9mm);
                if (kb.digit2Key.wasPressedThisFrame) EquipWeapon(ShootingWeaponId.TrapShotgun);
                if (kb.digit3Key.wasPressedThisFrame) EquipWeapon(ShootingWeaponId.TacticalCarbine);
                if (kb.digit4Key.wasPressedThisFrame) EquipWeapon(ShootingWeaponId.Sniper100m);
                if (kb.cKey.wasPressedThisFrame && _currentMode == ShootingModeId.TrapClay) LaunchClayPigeon();
                if (kb.leftShiftKey.wasPressedThisFrame && _isAds) _isHoldingBreath = true;
                if (kb.leftShiftKey.wasReleasedThisFrame) _isHoldingBreath = false;
            }
            if (mouse != null)
            {
                if (mouse.rightButton.wasPressedThisFrame) ToggleAds();
            }

            // Full-auto hold-to-fire for 5.56 Carbine (works with BOTH mobile Attack button hold & PC mouse/F hold!)
            InputHub input = InputHub.Instance;
            if (input != null && GetCurrentSpec().IsFullAuto && input.AttackHeld && !_roundFinished)
            {
                TryFireCurrentWeapon();
            }

            // Allow left joystick / WASD for smooth precision micro-aiming while standing in the booth!
            if (input != null && _cameraRig != null && input.MoveInput.sqrMagnitude > 0.01f)
            {
                float fineSpeed = IsFullScreenScope ? 14f : (_isAds ? 32f : 56f);
                _cameraRig.AddAimDelta(input.MoveInput.x * fineSpeed * dt, -input.MoveInput.y * fineSpeed * dt);
            }

            // Subtle scope breathing sway + breath-hold mechanics
            if (_cameraRig != null)
            {
                if (_isHoldingBreath && _isAds)
                {
                    _breathStamina = Mathf.Max(0f, _breathStamina - 26f * dt);
                    if (_breathStamina <= 0.5f) _isHoldingBreath = false;
                }
                else
                {
                    _breathStamina = Mathf.Min(100f, _breathStamina + 18f * dt);
                }

                float swayAmp = IsFullScreenScope ? 0.52f : (_isAds ? 0.14f : 0.05f);
                bool hasBipod = _currentWeapon == ShootingWeaponId.Sniper100m &&
                                ShootingRangeStore.IsWeaponOwned(_currentWeapon) &&
                                ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.SniperBipod);
                if (hasBipod) swayAmp *= 0.30f;
                if (_isHoldingBreath) swayAmp *= 0.12f;

                float swayYaw = Mathf.Sin(Time.time * 1.45f) * swayAmp;
                float swayPitch = Mathf.Cos(Time.time * 2.15f) * swayAmp * 0.72f;
                _cameraRig.SetAimSway(swayPitch, swayYaw);
            }
        }

        private void UpdateCharacterAndWeaponPose(float dt)
        {
            if (_player == null || _cameraRig == null) return;

            // Smoothly rotate shooter's body to align with aim yaw (keeps character facing downrange!)
            Quaternion bodyRot = Quaternion.Euler(0f, _cameraRig.Yaw, 0f);
            _player.transform.rotation = Quaternion.Slerp(_player.transform.rotation, bodyRot, 1f - Mathf.Exp(-14f * dt));

            float reloadPhase = _isReloading && _reloadDuration > 0.01f
                ? Mathf.Clamp01(1f - (_reloadTimer / _reloadDuration))
                : 0f;

            ProceduralHumanoid hum = _player.Humanoid;
            if (hum != null)
            {
                int actionKind = 0;
                float actionPhase = 0f;
                if (!_isReloading)
                {
                    if (_pumpCycleTimer > 0f)
                    {
                        actionKind = 1;
                        actionPhase = 1f - Mathf.Clamp01(_pumpCycleTimer / 0.46f);
                    }
                    else if (_boltCycleTimer > 0f)
                    {
                        actionKind = 2;
                        actionPhase = 1f - Mathf.Clamp01(_boltCycleTimer / 0.78f);
                    }
                }
                int reloadStyle = _currentWeapon == ShootingWeaponId.TrapShotgun ? 1
                    : (_currentWeapon == ShootingWeaponId.Sniper100m ? 2 : 0);
                hum.SetLocomotion(0f, true, 0f);
                hum.SetWeaponAimPose(true, GetCurrentSpec().IsLongGun, _cameraRig.Pitch, _recoilKick01, reloadPhase, actionKind, actionPhase, reloadStyle);
            }

            // Snap Blender FBX weapon into right hand grip (hide mesh during Full-Screen Sniper Scope so zero lens clipping!)
            if (_weaponInstance != null)
            {
                bool showGunMesh = !IsFullScreenScope;
                if (_weaponInstance.activeSelf != showGunMesh)
                {
                    _weaponInstance.SetActive(showGunMesh);
                }

                if (showGunMesh && hum != null)
                {
                    WeaponSpec spec = GetCurrentSpec();
                    Vector3 gripPos = hum.WeaponGripWorldPos;
                    Quaternion gripRot = hum.WeaponGripWorldRot;
                    if (gripPos.sqrMagnitude < 0.01f)
                    {
                        gripPos = _player.transform.position + Vector3.up * 1.38f + _player.transform.forward * 0.35f;
                        gripRot = Quaternion.Euler(_cameraRig.Pitch, _cameraRig.Yaw, 0f);
                    }
                    _weaponInstance.transform.position = gripPos + gripRot * spec.GripLocalOffset;
                    _weaponInstance.transform.rotation = gripRot * Quaternion.Euler(spec.GripLocalEuler);

                    // Mechanical gun sub-part animations (Pistol slide blowback, Shotgun pump, Sniper bolt, Magazine drop!)
                    if (_partSlide != null)
                    {
                        _partSlide.localPosition = _slideRestLocal + new Vector3(0f, 0f, -_recoilKick01 * 0.042f);
                    }
                    if (_partPump != null)
                    {
                        float pNorm = _pumpCycleTimer > 0f ? Mathf.Sin((_pumpCycleTimer / 0.46f) * Mathf.PI) : 0f;
                        _partPump.localPosition = _pumpRestLocal + new Vector3(0f, 0f, -pNorm * 0.095f);
                    }
                    if (_partBolt != null)
                    {
                        float boltPhase = _boltCycleTimer > 0f
                            ? (1f - Mathf.Clamp01(_boltCycleTimer / 0.78f))
                            : (_currentWeapon == ShootingWeaponId.Sniper100m && reloadPhase > 0.72f
                                ? Mathf.Clamp01((reloadPhase - 0.72f) / 0.28f)
                                : 0f);
                        float bLift = boltPhase > 0.005f ? Mathf.Sin(boltPhase * Mathf.PI) : 0f;
                        float bPull = boltPhase > 0.005f ? Mathf.Sin(Mathf.Clamp01((boltPhase - 0.15f) / 0.70f) * Mathf.PI) : 0f;
                        _partBolt.localRotation = Quaternion.Euler(0f, 0f, bLift * 58f);
                        _partBolt.localPosition = _boltRestLocal + new Vector3(0f, 0f, -bPull * 0.068f);
                    }
                    if (_partMag != null)
                    {
                        float mDrop = reloadPhase > 0.1f && reloadPhase < 0.85f ? Mathf.Sin(reloadPhase * Mathf.PI) : 0f;
                        _partMag.localPosition = _magRestLocal + new Vector3(0f, -mDrop * 0.14f, 0f);
                    }
                }
            }

            // Live 3D Red Laser Sight Beam for CarbineLaser upgrade!
            if (_laserBeam != null)
            {
                bool laserActive = _currentWeapon == ShootingWeaponId.TacticalCarbine &&
                                   ShootingRangeStore.IsWeaponOwned(_currentWeapon) &&
                                   ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.CarbineLaser) &&
                                   !_isReloading && !IsArmoryOpen && _partLaserOrigin != null;
                _laserBeam.enabled = laserActive;
                if (laserActive)
                {
                    Vector3 start = _partLaserOrigin.position;
                    Ray ray = _cameraRig.GetAimRay();
                    Vector3 end = Physics.Raycast(ray, out RaycastHit lh, 120f, ~0, QueryTriggerInteraction.Ignore)
                        ? lh.point
                        : ray.origin + ray.direction * 90f;
                    _laserBeam.SetPosition(0, start);
                    _laserBeam.SetPosition(1, end);
                }
            }
        }

        private void UpdateModeSimulation(float dt)
        {
            // IPSC steel timer, moving rail plates & hinged knock-down physics
            if (_currentMode == ShootingModeId.IpscSteel)
            {
                if (!_roundFinished)
                {
                    _roundTimer -= dt;
                    if (_roundTimer <= 0f)
                    {
                        _roundTimer = 0f;
                        FinishCurrentRound();
                    }
                }

                for (int i = 0; i < _steelUnits.Count; i++)
                {
                    SteelTargetUnit u = _steelUnits[i];
                    if (u.Root == null) continue;
                    if (u.MovesOnRail && !u.IsKnocked)
                    {
                        float ox = Mathf.Sin(Time.time * u.RailSpeed + u.RailPhase) * u.RailAmp;
                        u.Root.transform.localPosition = u.BaseLocalPos + new Vector3(ox, 0f, 0f);
                    }
                    if (u.PlatePivot != null)
                    {
                        u.CurrentTiltDeg = Mathf.Lerp(u.CurrentTiltDeg, u.TargetTiltDeg, 1f - Mathf.Exp(-16f * dt));
                        u.PlatePivot.localRotation = Quaternion.Euler(u.CurrentTiltDeg, 0f, 0f);
                    }
                }
            }

            // Trap / Skeet clay pigeon flight physics & auto-launcher
            if (_currentMode == ShootingModeId.TrapClay)
            {
                if (!_roundFinished && _claysLaunched < _totalRoundShots)
                {
                    _clayAutoTimer -= dt;
                    if (_clayAutoTimer <= 0f)
                    {
                        LaunchClayPigeon();
                    }
                }

                for (int i = _activeClays.Count - 1; i >= 0; i--)
                {
                    ActiveClayDisc c = _activeClays[i];
                    if (c.Go == null) { _activeClays.RemoveAt(i); continue; }
                    c.Life -= dt;
                    c.Velocity.y -= 9.2f * dt;
                    c.Go.transform.position += c.Velocity * dt;
                    c.Go.transform.Rotate(0f, 480f * dt, 0f, Space.Self);

                    if (c.Life <= 0f || c.Go.transform.position.y < 0.15f)
                    {
                        Destroy(c.Go);
                        _activeClays.RemoveAt(i);
                        _combo = 0;
                        _hud?.ShowPopup(L("PLAK KAÇTI!", "CLAY MISSED!"), new Color(0.88f, 0.45f, 0.36f));
                        if (_claysLaunched >= _totalRoundShots && _activeClays.Count == 0 && !_roundFinished)
                        {
                            FinishCurrentRound();
                        }
                    }
                }

                if (_clayThrowerArm != null)
                {
                    if (_throwerArmTimer > 0f)
                    {
                        _throwerArmTimer = Mathf.Max(0f, _throwerArmTimer - dt);
                        float swing = Mathf.Sin((_throwerArmTimer / 0.28f) * Mathf.PI) * 75f;
                        _clayThrowerArm.localRotation = Quaternion.Euler(0f, swing, 0f);
                    }
                    else
                    {
                        _clayThrowerArm.localRotation = Quaternion.identity;
                    }
                }
            }
        }

        private void UpdateTimersAndVfx(float dt)
        {
            if (_fireCooldown > 0f) _fireCooldown = Mathf.Max(0f, _fireCooldown - dt);
            if (_recoilKick01 > 0f) _recoilKick01 = Mathf.MoveTowards(_recoilKick01, 0f, dt * 7.5f);
            if (_pumpCycleTimer > 0f) _pumpCycleTimer = Mathf.Max(0f, _pumpCycleTimer - dt);
            if (_boltCycleTimer > 0f) _boltCycleTimer = Mathf.Max(0f, _boltCycleTimer - dt);
            if (_calloutTimer > 0f) _calloutTimer = Mathf.Max(0f, _calloutTimer - dt);

            WeaponSpec spec = GetCurrentSpec();
            float targetBloom = _isAds ? spec.AdsSpreadDeg : spec.HipSpreadDeg;
            _bloomDeg = Mathf.Lerp(_bloomDeg, targetBloom, 1f - Mathf.Exp(-9f * dt));

            if (_isReloading)
            {
                _reloadTimer -= dt;
                if (_reloadTimer <= 0f)
                {
                    _isReloading = false;
                    _magAmmo[(int)_currentWeapon] = GetMaxMag(_currentWeapon);
                    GameFeelAudio.PlayReloadClick(complete: true);
                    HudRevision++;
                }
            }

            if (_muzzleFlashTimer > 0f)
            {
                _muzzleFlashTimer -= dt;
                if (_muzzleFlashTimer <= 0f && _muzzleFlashObj != null)
                {
                    _muzzleFlashObj.SetActive(false);
                }
            }

            // Ejecting spinning brass shells
            for (int i = _shells.Count - 1; i >= 0; i--)
            {
                ActiveShellCasing s = _shells[i];
                if (s.Go == null) { _shells.RemoveAt(i); continue; }
                s.Life -= dt;
                s.Velocity.y -= 9.8f * dt;
                Vector3 pos = s.Go.transform.position + s.Velocity * dt;
                if (pos.y < 0.14f)
                {
                    pos.y = 0.14f;
                    s.Velocity.y = -s.Velocity.y * 0.35f;
                    s.Velocity.x *= 0.65f;
                    s.Velocity.z *= 0.65f;
                }
                s.Go.transform.position = pos;
                s.Go.transform.Rotate(s.Spin * dt, Space.Self);
                if (s.Life <= 0f)
                {
                    Destroy(s.Go);
                    _shells.RemoveAt(i);
                }
            }

            // Fast fading golden bullet tracer streaks
            for (int i = _tracers.Count - 1; i >= 0; i--)
            {
                ActiveTracer tr = _tracers[i];
                if (tr.Line == null) { _tracers.RemoveAt(i); continue; }
                tr.Life -= dt;
                float a = Mathf.Clamp01(tr.Life / Mathf.Max(0.01f, tr.MaxLife));
                tr.Line.startWidth = 0.022f * a;
                tr.Line.endWidth = 0.008f * a;
                if (tr.Life <= 0f)
                {
                    Destroy(tr.Line.gameObject);
                    _tracers.RemoveAt(i);
                }
            }
        }

        private void TriggerMuzzleFlash(bool showVisibleFlame)
        {
            if (_muzzleFlashObj == null) return;
            Transform anchor = _partMuzzle != null ? _partMuzzle : _weaponInstance.transform;
            _muzzleFlashObj.transform.position = anchor.position;
            _muzzleFlashObj.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 180f));
            float sc = _currentWeapon == ShootingWeaponId.TrapShotgun || _currentWeapon == ShootingWeaponId.Sniper100m ? 1.45f : 0.95f;
            _muzzleFlashObj.transform.localScale = Vector3.one * (showVisibleFlame ? sc : sc * 0.25f);
            _muzzleFlashObj.SetActive(!IsFullScreenScope);
            _muzzleFlashTimer = 0.048f;
        }

        private void SpawnBrassShell()
        {
            if (_partEject == null || _brassMat == null || IsFullScreenScope) return;
            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shell.name = "BrassShell";
            Destroy(shell.GetComponent<Collider>());
            shell.transform.position = _partEject.position;
            shell.transform.localScale = _currentWeapon == ShootingWeaponId.TrapShotgun
                ? new Vector3(0.022f, 0.036f, 0.022f)
                : new Vector3(0.012f, 0.022f, 0.012f);
            shell.GetComponent<MeshRenderer>().sharedMaterial = _brassMat;

            Vector3 right = _player != null ? _player.transform.right : Vector3.right;
            Vector3 vel = right * UnityEngine.Random.Range(1.4f, 2.3f) + Vector3.up * UnityEngine.Random.Range(1.3f, 2.1f) + UnityEngine.Random.insideUnitSphere * 0.25f;
            _shells.Add(new ActiveShellCasing
            {
                Go = shell,
                Velocity = vel,
                Spin = new Vector3(UnityEngine.Random.Range(-520f, 520f), UnityEngine.Random.Range(-420f, 420f), UnityEngine.Random.Range(-520f, 520f)),
                Life = 1.6f
            });
        }

        private void SpawnBulletTracer(Vector3 from, Vector3 to)
        {
            if (_tracerMat == null) return;
            GameObject go = new GameObject("BulletTracer");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.022f;
            lr.endWidth = 0.008f;
            lr.sharedMaterial = _tracerMat;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            _tracers.Add(new ActiveTracer { Line = lr, Life = 0.075f, MaxLife = 0.075f });
        }

        private void PlantBulletHole(Vector3 worldPos, Vector3 normal)
        {
            if (_holeMat == null || _classicTargetRoot == null) return;
            GameObject hole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hole.name = "BulletHole";
            Destroy(hole.GetComponent<Collider>());
            hole.transform.SetParent(_classicTargetRoot.transform, true);
            hole.transform.position = worldPos;
            hole.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            hole.transform.localScale = new Vector3(0.032f, 0.004f, 0.032f);
            hole.GetComponent<MeshRenderer>().sharedMaterial = _holeMat;
            _bulletHoles.Add(hole);
        }

        private void UpdateCameraProfile()
        {
            if (_cameraRig == null) return;
            WeaponSpec spec = GetCurrentSpec();
            bool owns = ShootingRangeStore.IsWeaponOwned(_currentWeapon);
            float fov = _isAds ? spec.AdsFov : 54f;
            float sens = _isAds ? spec.AdsSensScale : 0.78f;

            if (_isAds && _currentWeapon == ShootingWeaponId.Sniper100m && owns && ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.SniperScope8x))
            {
                fov = 7.8f;
                sens = 0.12f;
            }
            else if (_isAds && _currentWeapon == ShootingWeaponId.TacticalCarbine && owns && ShootingRangeStore.IsUpgradeOwned(ShootingUpgradeId.CarbineHolo))
            {
                fov = 30f;
                sens = 0.36f;
            }

            float dist = IsFullScreenScope ? 0.08f : (_isAds ? 1.18f : 2.15f);
            Vector3 shoulder = IsFullScreenScope
                ? new Vector3(0.0f, 1.54f, 0f)
                : new Vector3(_isAds ? 0.38f : 0.50f, 1.54f, 0f);

            // Clamp firing arc strictly downrange toward Aydos South Hills (±58° around 180°) — NEVER toward neighborhood/NPCs!
            _cameraRig.SetAimCameraOverride(
                true, shoulder, dist, fov, sens,
                minPitch: _currentMode == ShootingModeId.TrapClay ? -48f : -28f,
                maxPitch: 26f,
                yawCenterDeg: 180f,
                yawMaxHalfArcDeg: 58f);
        }

        private float GetTargetDistanceForHud()
        {
            return _currentMode switch
            {
                ShootingModeId.ClassicBullseye => 22.8f,
                ShootingModeId.IpscSteel       => 24.0f,
                ShootingModeId.TrapClay        => 28.0f,
                _                              => 97.2f
            };
        }

        private void SetCallout(string text, Color color, float duration)
        {
            _calloutText = text;
            _calloutColor = color;
            _calloutTimer = duration;
            HudRevision++;
        }

        // =====================================================================
        // IPlayableSportArena HUD STRINGS & METERS
        // =====================================================================
        public string PrimaryButtonLabel()
        {
            if (_roundFinished) return L("Tekrar Oyna", "Play Again");
            if (_isReloading) return L("Doluyor...", "Reloading...");
            return L("ATEŞ ET!", "FIRE!");
        }

        public string SecondaryButtonLabel()
        {
            if (GetCurrentSpec().UsesFullScreenScope) return _isAds ? L("Dürbün Kapat", "Exit Scope") : L("DÜRBÜN", "SCOPE");
            return _isAds ? L("Normal Bakış", "Hip View") : L("NİŞAN AL", "AIM");
        }

        public string ControlsHintKey() => "sports.shooting.controls";

        public string HudText()
        {
            string modeName = _currentMode switch
            {
                ShootingModeId.ClassicBullseye => L($"Klasik Hedef · Kalan: {_shotsLeft}/{_totalRoundShots}", $"Classic Bullseye · Left: {_shotsLeft}/{_totalRoundShots}"),
                ShootingModeId.IpscSteel       => L($"IPSC Çelik · {_roundTimer:F1} sn · Dalga {_ipscWave}", $"IPSC Steel · {_roundTimer:F1}s · Wave {_ipscWave}"),
                ShootingModeId.TrapClay        => L($"Trap Kil Plak · Plak {_claysLaunched}/{_totalRoundShots}", $"Trap Clay · Clay {_claysLaunched}/{_totalRoundShots}"),
                _                              => L($"100m Sniper · Kalan: {_shotsLeft}/{_totalRoundShots} · Rüzgâr {Mathf.Abs(_windSpeedMs):F1}m/s {(_windSpeedMs >= 0 ? "→" : "←")}", $"100m Sniper · Left: {_shotsLeft}/{_totalRoundShots} · Wind {Mathf.Abs(_windSpeedMs):F1}m/s {(_windSpeedMs >= 0 ? "→" : "←")}")
            };
            string status = _roundFinished
                ? L("Tur Tamamlandı! Tekrar oynamak için ATEŞ ET'e bas veya mod değiştir.", "Round Complete! Tap FIRE to play again or switch mode.")
                : L($"{WeaponName(GetCurrentSpec())}  ·  Mermi: {CurrentAmmo}/{CurrentMaxAmmo}  ·  Rekor: {ShootingRangeStore.GetBestScore(_currentMode)}", $"{WeaponName(GetCurrentSpec())}  ·  Ammo: {CurrentAmmo}/{CurrentMaxAmmo}  ·  Best: {ShootingRangeStore.GetBestScore(_currentMode)}");
            return L($"Aydos Atış Poligonu  ·  {modeName}  ·  Skor: {_score}\n{status}", $"Aydos Shooting Range  ·  {modeName}  ·  Score: {_score}\n{status}");
        }

        public SportHudMeterState GetHudMeterState()
        {
            var state = new SportHudMeterState
            {
                ShowStaminaBar = _isAds && GetCurrentSpec().UsesFullScreenScope,
                StaminaFill01 = BreathFill01,
                CalloutBadge = _calloutTimer > 0f ? _calloutText : null,
                CalloutColor = _calloutColor
            };

            if (_isReloading && _reloadDuration > 0.01f)
            {
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = Mathf.Clamp01(1f - (_reloadTimer / _reloadDuration));
                state.PrimaryColor = new Color(0.40f, 0.86f, 1.0f);
                state.PrimaryLabel = L("ŞARJÖR DEĞİŞTİRİLİYOR...", "RELOADING...");
            }
            else if (_currentMode == ShootingModeId.IpscSteel && !_roundFinished)
            {
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = Mathf.Clamp01(_roundTimer / 60f);
                state.PrimaryColor = _roundTimer < 12f ? new Color(1.0f, 0.32f, 0.24f) : new Color(1.0f, 0.82f, 0.24f);
                state.PrimaryLabel = _combo >= 2 ? L($"SÜRE · SERİ x{_combo}", $"TIME · STREAK x{_combo}") : L("IPSC SÜRESİ", "IPSC TIMER");
            }
            return state;
        }
    }
}
