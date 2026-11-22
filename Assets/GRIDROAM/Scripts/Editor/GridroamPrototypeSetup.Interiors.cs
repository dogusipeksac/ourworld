using System.Collections.Generic;
using Gridroam.Player;
using Gridroam.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Gridroam.Editor
{
    public static partial class GridroamPrototypeSetup
    {
        private enum EnterableShopKind
        {
            Pizzeria,
            Cafe,
            Market
        }

        /// <summary>
        /// Marks an object static for GI, occlusion, and reflection probes WITHOUT BatchingStatic,
        /// used on paintable exterior walls and NightLight windows so MaterialPropertyBlock overrides always work.
        /// </summary>
        private static void MarkStaticPaintable(GameObject go)
        {
            StaticEditorFlags flags = StaticEditorFlags.ContributeGI |
                                      StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.OccludeeStatic |
                                      StaticEditorFlags.ReflectionProbeStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
        }

        private static void AttachWindowNightLight(GameObject windowObj)
        {
            if (windowObj == null) return;
            MarkStaticPaintable(windowObj);
            Renderer r = windowObj.GetComponent<Renderer>();
            if (r == null) return;

            NightLight nl = windowObj.AddComponent<NightLight>();
            nl.Configure(
                null,
                new[] { r },
                new Color(1.0f, 0.74f, 0.36f, 1f) * 1.75f,
                replaceBaseColor: true,
                dayColor: new Color(0.08f, 0.12f, 0.17f, 1f),
                nightColor: new Color(1.0f, 0.82f, 0.52f, 1f));
        }

        private static void AttachLampShadeNightLight(GameObject shadeObj)
        {
            if (shadeObj == null) return;
            MarkStaticPaintable(shadeObj);
            Renderer r = shadeObj.GetComponent<Renderer>();
            if (r == null) return;

            NightLight nl = shadeObj.AddComponent<NightLight>();
            nl.Configure(
                null,
                new[] { r },
                new Color(1.0f, 0.78f, 0.42f, 1f) * 1.85f,
                replaceBaseColor: true,
                dayColor: new Color(0.88f, 0.84f, 0.76f, 1f),
                nightColor: new Color(1.0f, 0.84f, 0.56f, 1f));
        }

        /// <summary>
        /// Adds crisp 3D world-space text onto a shopfront or station signboard with zero GraphicRaycaster overhead.
        /// </summary>
        private static void CreateWorldSignText(
            string signText,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Vector2 boardMeters,
            Color textColor)
        {
            GameObject canvasGo = new GameObject($"SignText_{signText}");
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = localPos;
            canvasGo.transform.localRotation = localRot;

            const float pxPerMeter = 100f;
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform crt = canvasGo.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(boardMeters.x * pxPerMeter, boardMeters.y * pxPerMeter);
            canvasGo.transform.localScale = Vector3.one * (1f / pxPerMeter);

            GameObject txtGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(canvasGo.transform, false);
            RectTransform trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            Text txt = txtGo.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = signText;
            txt.fontSize = Mathf.Clamp(Mathf.RoundToInt(boardMeters.y * pxPerMeter * 0.68f), 18, 64);
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = textColor;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
        }

        private static GameObject CreateLocalTiledBox(
            string name,
            Transform parent,
            Vector3 localCenter,
            Vector3 size,
            Quaternion localRot,
            Material mat,
            float metersPerTile,
            bool collider = true,
            bool staticBatch = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localRotation = localRot;
            go.transform.localScale = Vector3.one;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            Vector2 matScale = Vector2.one;
            if (mat != null && mat.HasProperty("_BaseMap"))
            {
                Vector2 s = mat.GetTextureScale("_BaseMap");
                if (Mathf.Abs(s.x) > 0.001f) matScale.x = s.x;
                if (Mathf.Abs(s.y) > 0.001f) matScale.y = s.y;
            }

            mf.sharedMesh = BuildTiledBoxMesh(name + "_Mesh", size, Mathf.Max(0.1f, metersPerTile), matScale);
            if (mat != null)
            {
                mr.sharedMaterial = mat;
            }

            if (collider)
            {
                BoxCollider bc = go.AddComponent<BoxCollider>();
                bc.center = Vector3.zero;
                bc.size = size;
            }

            if (staticBatch)
            {
                MarkStaticEnvironment(go);
            }
            else
            {
                MarkStaticPaintable(go);
            }

            return go;
        }

        private static GameObject CreateStaticPart(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Vector3 localScale,
            Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (mat != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
            MarkStaticEnvironment(go);
            return go;
        }

        private static SeatInteractable CreateSeatSlot(
            string slotName,
            string triggerId,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            float radius = 1.4f)
        {
            GameObject slotObj = new GameObject(slotName);
            slotObj.transform.SetParent(parent, false);
            slotObj.transform.localPosition = localPos;
            slotObj.transform.localRotation = localRot;

            InteractableTrigger trig = slotObj.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.Seat, triggerId, "ui.btn.sit", radius);

            SeatInteractable seat = slotObj.AddComponent<SeatInteractable>();
            seat.Configure(slotObj.transform, trig, occupied: false);
            return seat;
        }

        private static void BuildHingedDoor(
            string doorId,
            Transform parent,
            Vector3 doorwayBottomCenterLocal,
            float doorWidth,
            float doorHeight,
            MaterialSet mats)
        {
            GameObject doorRoot = new GameObject("DoorRoot");
            doorRoot.transform.SetParent(parent, false);
            doorRoot.transform.localPosition = doorwayBottomCenterLocal;
            doorRoot.transform.localRotation = Quaternion.identity;

            GameObject frameRoot = new GameObject("DoorFrame");
            frameRoot.transform.SetParent(parent, false);
            frameRoot.transform.localPosition = doorwayBottomCenterLocal;
            frameRoot.transform.localRotation = Quaternion.identity;

            CreateStaticPart("Jamb_L", PrimitiveType.Cube, frameRoot.transform,
                new Vector3(0f, doorHeight * 0.5f, -doorWidth * 0.5f + 0.02f),
                Quaternion.identity, new Vector3(0.22f, doorHeight, 0.04f), mats.TownDoorWood);
            CreateStaticPart("Jamb_R", PrimitiveType.Cube, frameRoot.transform,
                new Vector3(0f, doorHeight * 0.5f, doorWidth * 0.5f - 0.02f),
                Quaternion.identity, new Vector3(0.22f, doorHeight, 0.04f), mats.TownDoorWood);
            CreateStaticPart("Jamb_Top", PrimitiveType.Cube, frameRoot.transform,
                new Vector3(0f, doorHeight - 0.02f, 0f),
                Quaternion.identity, new Vector3(0.22f, 0.04f, doorWidth), mats.TownDoorWood);

            float clearW = doorWidth - 0.08f;
            float clearH = doorHeight - 0.05f;

            GameObject pivotObj = new GameObject("DoorPivot");
            pivotObj.transform.SetParent(doorRoot.transform, false);
            pivotObj.transform.localPosition = new Vector3(0f, 0.01f, -clearW * 0.5f);
            pivotObj.transform.localRotation = Quaternion.identity;

            GameObject panelObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panelObj.name = "DoorPanel";
            panelObj.transform.SetParent(pivotObj.transform, false);
            panelObj.transform.localPosition = new Vector3(0f, clearH * 0.5f, clearW * 0.5f);
            panelObj.transform.localRotation = Quaternion.identity;
            panelObj.transform.localScale = new Vector3(0.06f, clearH, clearW);
            panelObj.GetComponent<Renderer>().sharedMaterial = mats.TownDoorWood;

            float handleZ = clearW * 0.84f;
            float handleY = 0.98f;

            GameObject handleSpindle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handleSpindle.name = "HandleSpindle";
            handleSpindle.transform.SetParent(pivotObj.transform, false);
            handleSpindle.transform.localPosition = new Vector3(0f, handleY, handleZ);
            handleSpindle.transform.localScale = new Vector3(0.11f, 0.025f, 0.025f);
            Object.DestroyImmediate(handleSpindle.GetComponent<Collider>());
            handleSpindle.GetComponent<Renderer>().sharedMaterial = mats.SedanChrome;

            GameObject handleOut = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handleOut.name = "HandleOuter";
            handleOut.transform.SetParent(pivotObj.transform, false);
            handleOut.transform.localPosition = new Vector3(0.055f, handleY, handleZ - 0.05f);
            handleOut.transform.localScale = new Vector3(0.022f, 0.024f, 0.12f);
            Object.DestroyImmediate(handleOut.GetComponent<Collider>());
            handleOut.GetComponent<Renderer>().sharedMaterial = mats.SedanChrome;

            GameObject handleIn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handleIn.name = "HandleInner";
            handleIn.transform.SetParent(pivotObj.transform, false);
            handleIn.transform.localPosition = new Vector3(-0.055f, handleY, handleZ - 0.05f);
            handleIn.transform.localScale = new Vector3(0.022f, 0.024f, 0.12f);
            Object.DestroyImmediate(handleIn.GetComponent<Collider>());
            handleIn.GetComponent<Renderer>().sharedMaterial = mats.SedanChrome;

            GameObject triggerObj = new GameObject("DoorTrigger");
            triggerObj.transform.SetParent(doorRoot.transform, false);
            triggerObj.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            InteractableTrigger trigger = triggerObj.AddComponent<InteractableTrigger>();
            trigger.Configure(InteractableKind.Door, doorId, "ui.btn.door", 2.0f);

            DoorInteractable doorComp = doorRoot.AddComponent<DoorInteractable>();
            doorComp.Configure(pivotObj.transform, panelObj.transform, 106f, 0.36f);
        }

        private static void CreateInteriorZoneAndLight(
            string buildingId,
            Transform parent,
            Vector3 zoneCenterLocal,
            Vector3 zoneSize,
            Vector3 lightLocalPos,
            MaterialSet mats)
        {
            CreateStaticPart("CeilingFixture", PrimitiveType.Cylinder, parent,
                new Vector3(lightLocalPos.x, zoneCenterLocal.y + zoneSize.y * 0.5f - 0.05f, lightLocalPos.z),
                Quaternion.identity, new Vector3(0.36f, 0.04f, 0.36f), mats.WindowLitWarm);

            GameObject lightObj = new GameObject($"InteriorLight_{buildingId}");
            lightObj.transform.SetParent(parent, false);
            lightObj.transform.localPosition = lightLocalPos;
            Light ptLight = lightObj.AddComponent<Light>();
            ptLight.type = LightType.Point;
            ptLight.color = new Color(1.0f, 0.88f, 0.70f);
            ptLight.range = 6.0f;
            ptLight.intensity = 2.65f;
            ptLight.shadows = LightShadows.None;
            ptLight.enabled = false;

            GameObject zoneObj = new GameObject("InteriorZone");
            zoneObj.transform.SetParent(parent, false);
            zoneObj.transform.localPosition = Vector3.zero;
            zoneObj.transform.localRotation = Quaternion.identity;

            BoxCollider box = zoneObj.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = zoneCenterLocal;
            box.size = zoneSize;

            InteriorZone zone = zoneObj.AddComponent<InteriorZone>();
            zone.Configure(new[] { ptLight }, indoorMaxDistance: 2.2f);
        }

        private static void BuildEnterableResidentialHouse(
            string houseName,
            string houseId,
            string displayAddress,
            Transform parent,
            Vector3 basePos,
            Quaternion houseRot,
            float depthX,
            float widthZ,
            Material wallMat,
            bool hasChimney,
            bool hasGardenTree,
            MaterialSet mats,
            int houseSeed)
        {
            System.Random houseRng = new System.Random(houseSeed);

            GameObject root = new GameObject(houseName);
            root.transform.SetParent(parent, false);
            root.transform.position = basePos;
            root.transform.rotation = houseRot;

            depthX = Mathf.Max(9.0f, depthX);
            widthZ = Mathf.Max(10.4f, widthZ);

            float height = 5.95f + (float)(houseRng.NextDouble() * 0.35);
            float tileMeters = wallMat == mats.TownBrickHouse ? 1.5f : 3.0f;
            float hx = depthX * 0.5f;
            float hz = widthZ * 0.5f;

            float floorTopY = 0.04f;
            float ceilBottomY = 3.25f;
            float doorWidth = 1.55f;
            float doorHeight = 2.30f;
            float doorCenterZ = 0f;
            float doorTopY = floorTopY + doorHeight;

            Material[] interiorPaints =
            {
                mats.TownInteriorPaintWarm,
                mats.TownInteriorPaintCream,
                mats.TownInteriorPaintPearl
            };
            Material interiorMat = interiorPaints[houseRng.Next(interiorPaints.Length)];

            GameObject extGroup = new GameObject("ExteriorWalls");
            extGroup.transform.SetParent(root.transform, false);
            List<Renderer> paintableRenderers = new List<Renderer>(6);

            const float extThick = 0.18f;
            const float intThick = 0.02f;

            GameObject extBack = CreateLocalTiledBox("ExtWall_Back", extGroup.transform,
                new Vector3(-hx + extThick * 0.5f, height * 0.5f, 0f),
                new Vector3(extThick, height, widthZ),
                Quaternion.identity, wallMat, tileMeters, collider: true, staticBatch: false);
            paintableRenderers.Add(extBack.GetComponent<Renderer>());

            GameObject extSouth = CreateLocalTiledBox("ExtWall_South", extGroup.transform,
                new Vector3(0f, height * 0.5f, -hz + extThick * 0.5f),
                new Vector3(depthX - extThick * 2f, height, extThick),
                Quaternion.identity, wallMat, tileMeters, collider: true, staticBatch: false);
            paintableRenderers.Add(extSouth.GetComponent<Renderer>());

            GameObject extNorth = CreateLocalTiledBox("ExtWall_North", extGroup.transform,
                new Vector3(0f, height * 0.5f, hz - extThick * 0.5f),
                new Vector3(depthX - extThick * 2f, height, extThick),
                Quaternion.identity, wallMat, tileMeters, collider: true, staticBatch: false);
            paintableRenderers.Add(extNorth.GetComponent<Renderer>());

            float leftSpanZ = (doorCenterZ - doorWidth * 0.5f) - (-hz);
            float rightSpanZ = hz - (doorCenterZ + doorWidth * 0.5f);
            float lintelHeight = height - doorTopY;

            GameObject extFrontLeft = CreateLocalTiledBox("ExtWall_FrontLeft", extGroup.transform,
                new Vector3(hx - extThick * 0.5f, height * 0.5f, -hz + leftSpanZ * 0.5f),
                new Vector3(extThick, height, leftSpanZ),
                Quaternion.identity, wallMat, tileMeters, collider: true, staticBatch: false);
            paintableRenderers.Add(extFrontLeft.GetComponent<Renderer>());

            GameObject extFrontRight = CreateLocalTiledBox("ExtWall_FrontRight", extGroup.transform,
                new Vector3(hx - extThick * 0.5f, height * 0.5f, hz - rightSpanZ * 0.5f),
                new Vector3(extThick, height, rightSpanZ),
                Quaternion.identity, wallMat, tileMeters, collider: true, staticBatch: false);
            paintableRenderers.Add(extFrontRight.GetComponent<Renderer>());

            GameObject extFrontLintel = CreateLocalTiledBox("ExtWall_FrontLintel", extGroup.transform,
                new Vector3(hx - extThick * 0.5f, doorTopY + lintelHeight * 0.5f, doorCenterZ),
                new Vector3(extThick, lintelHeight, doorWidth),
                Quaternion.identity, wallMat, tileMeters, collider: true, staticBatch: false);
            paintableRenderers.Add(extFrontLintel.GetComponent<Renderer>());

            BuildHouseInteriorShellAndFurniture(
                houseId,
                root.transform,
                depthX,
                widthZ,
                floorTopY,
                ceilBottomY,
                doorCenterZ,
                doorWidth,
                doorHeight,
                extThick,
                intThick,
                interiorMat,
                mats,
                houseRng,
                houseSeed);

            GameObject roofAndTrim = new GameObject("ExteriorRoofAndDetails");
            roofAndTrim.transform.SetParent(root.transform, false);

            float overhang = 0.55f;
            float slopeLen = depthX * 0.60f + overhang;
            float roofY = height + 0.95f;
            CreateLocalTiledBox("Roof_East", roofAndTrim.transform,
                new Vector3(depthX * 0.25f, roofY, 0f),
                new Vector3(slopeLen, 0.24f, widthZ + overhang * 2f),
                Quaternion.Euler(0f, 0f, -26f), mats.TownRoofTile, 1.5f, collider: false);
            CreateLocalTiledBox("Roof_West", roofAndTrim.transform,
                new Vector3(-depthX * 0.25f, roofY, 0f),
                new Vector3(slopeLen, 0.24f, widthZ + overhang * 2f),
                Quaternion.Euler(0f, 0f, 26f), mats.TownRoofTile, 1.5f, collider: false);

            CreateLocalTiledBox("Roof_GableRidge", roofAndTrim.transform,
                new Vector3(0f, height + 0.72f, 0f),
                new Vector3(depthX * 0.54f, 1.44f, widthZ - 0.12f),
                Quaternion.identity, mats.PlasterTrim, 2.5f, collider: false);

            // Authentic Istanbul / Balat projected 2nd-floor wooden bay window (Cumba) + brackets!
            Material cumbaWallMat = (Mathf.Abs(houseSeed) % 2 == 0) ? mats.TownInteriorPaintCream : mats.TownWood;
            CreateLocalTiledBox("Istanbul_CumbaBox", roofAndTrim.transform,
                new Vector3(hx + 0.42f, 4.42f, 0f),
                new Vector3(0.84f, 2.25f, 3.65f),
                Quaternion.identity, cumbaWallMat, 2.0f, collider: false);
            CreateLocalTiledBox("Istanbul_CumbaCornice", roofAndTrim.transform,
                new Vector3(hx + 0.46f, 5.60f, 0f),
                new Vector3(0.96f, 0.14f, 3.88f),
                Quaternion.identity, mats.TownDoorWood, 2.0f, collider: false);
            CreateLocalTiledBox("Istanbul_CumbaSill", roofAndTrim.transform,
                new Vector3(hx + 0.44f, 3.25f, 0f),
                new Vector3(0.92f, 0.14f, 3.82f),
                Quaternion.identity, mats.TownDoorWood, 2.0f, collider: false);
            for (int bs = -1; bs <= 1; bs += 2)
            {
                CreateStaticPart($"Istanbul_CumbaBracket_{bs}", PrimitiveType.Cube, roofAndTrim.transform,
                    new Vector3(hx + 0.34f, 2.92f, bs * 1.48f),
                    Quaternion.Euler(0f, 0f, -38f), new Vector3(0.14f, 0.85f, 0.16f), mats.TownDoorWood);
            }
            for (int cw = -1; cw <= 1; cw++)
            {
                GameObject cWin = CreateLocalTiledBox($"Istanbul_CumbaWin_{cw}", roofAndTrim.transform,
                    new Vector3(hx + 0.86f, 4.46f, cw * 1.05f),
                    new Vector3(0.06f, 1.32f, 0.74f),
                    Quaternion.identity, mats.WindowLitWarm, 2.0f, collider: false, staticBatch: false);
                AttachWindowNightLight(cWin);
            }

            Vector3[] winLocalPositions =
            {
                new Vector3(hx + 0.04f, 1.65f, -2.55f),
                new Vector3(hx + 0.04f, 1.65f, 2.55f),
                new Vector3(hx + 0.04f, 4.35f, -2.55f),
                new Vector3(hx + 0.04f, 4.35f, 0f),
                new Vector3(hx + 0.04f, 4.35f, 2.55f)
            };

            for (int i = 0; i < winLocalPositions.Length; i++)
            {
                bool litAtNight = i >= 2 && houseRng.NextDouble() < 0.65;
                Material wMat = litAtNight ? mats.WindowLitWarm : mats.SedanGlass;
                GameObject winObj = CreateLocalTiledBox($"Win_{i}", roofAndTrim.transform, winLocalPositions[i],
                    new Vector3(0.06f, 1.25f, 1.05f), Quaternion.identity, wMat, 2.0f, collider: false, staticBatch: !litAtNight);
                if (litAtNight)
                {
                    AttachWindowNightLight(winObj);
                }

                CreateLocalTiledBox($"WinSill_{i}", roofAndTrim.transform,
                    winLocalPositions[i] + new Vector3(0.04f, -0.68f, 0f),
                    new Vector3(0.14f, 0.10f, 1.22f), Quaternion.identity, mats.PlasterTrim, 2.0f, collider: false);
            }

            if (hasChimney)
            {
                CreateLocalTiledBox("Chimney", roofAndTrim.transform,
                    new Vector3(-depthX * 0.18f, height + 1.9f, widthZ * 0.28f),
                    new Vector3(0.75f, 2.1f, 0.75f),
                    Quaternion.identity, mats.GarageBrick, 1.2f, collider: false);
            }

            float fenceLocalX = hx + 1.15f;
            float hedgeLen = widthZ * 0.34f;
            CreateLocalTiledBox("GardenHedge_S", roofAndTrim.transform,
                new Vector3(fenceLocalX, 0.45f, -widthZ * 0.33f),
                new Vector3(0.42f, 0.90f, hedgeLen),
                Quaternion.identity, mats.TownHedge, 1.5f, collider: true);
            CreateLocalTiledBox("GardenHedge_N", roofAndTrim.transform,
                new Vector3(fenceLocalX, 0.45f, widthZ * 0.33f),
                new Vector3(0.42f, 0.90f, hedgeLen),
                Quaternion.identity, mats.TownHedge, 1.5f, collider: true);

            CreateLocalTiledBox("Doorstep_Walkway", roofAndTrim.transform,
                new Vector3(hx + 1.10f, 0.015f, doorCenterZ),
                new Vector3(2.30f, 0.03f, 2.10f),
                Quaternion.identity, mats.TownPaving, 1.5f, collider: false);

            if (hasGardenTree)
            {
                float treeLocalZ = houseRng.Next(2) == 0 ? -(widthZ * 0.5f + 2.2f) : (widthZ * 0.5f + 2.2f);
                Vector3 treeWorldPos = root.transform.TransformPoint(new Vector3(depthX * 0.28f, 0f, treeLocalZ));
                CreateTownTree("GardenTree", root.transform,
                    treeWorldPos,
                    mats, 0.85f + (float)houseRng.NextDouble() * 0.20f, houseRng.Next(100));
            }

            PaintableHouse paintable = root.AddComponent<PaintableHouse>();
            paintable.Configure(houseId, paintableRenderers, displayAddress, hx);

            Material[] rimPreviewMats =
            {
                mats.TownAwningTerracotta,
                mats.TownFabricTeal,
                mats.TownFabricMustard,
                mats.TownAwningGreen
            };
            CreatePaintBucketProp(
                houseId,
                root.transform,
                new Vector3(hx + 0.44f, 0f, -1.48f),
                mats,
                rimPreviewMats[houseRng.Next(rimPreviewMats.Length)]);
        }

        private static void BuildEnterableShopBuilding(
            string shopName,
            string shopId,
            EnterableShopKind shopKind,
            Transform parent,
            Vector3 basePos,
            float depthX,
            float widthZ,
            float height,
            int storeys,
            Material facadeMat,
            float metersPerTile,
            Material awningMat,
            bool pitchedRoof,
            MaterialSet mats,
            int shopSeed)
        {
            System.Random shopRng = new System.Random(shopSeed);

            GameObject root = new GameObject(shopName);
            root.transform.SetParent(parent, false);
            root.transform.position = basePos;
            root.transform.rotation = Quaternion.identity;

            float hx = depthX * 0.5f;
            float hz = widthZ * 0.5f;

            float floorTopY = 0.04f;
            float ceilBottomY = 3.30f;
            float doorWidth = 1.60f;
            float doorHeight = 2.35f;
            float doorCenterZ = 0f;
            float doorTopY = floorTopY + doorHeight;

            const float extThick = 0.18f;
            const float intThick = 0.02f;

            GameObject extGroup = new GameObject("ShopExteriorShell");
            extGroup.transform.SetParent(root.transform, false);

            CreateLocalTiledBox("Wall_Back", extGroup.transform,
                new Vector3(-hx + extThick * 0.5f, height * 0.5f, 0f),
                new Vector3(extThick, height, widthZ),
                Quaternion.identity, facadeMat, metersPerTile, collider: true);

            CreateLocalTiledBox("Wall_South", extGroup.transform,
                new Vector3(0f, height * 0.5f, -hz + extThick * 0.5f),
                new Vector3(depthX - extThick * 2f, height, extThick),
                Quaternion.identity, facadeMat, metersPerTile, collider: true);

            CreateLocalTiledBox("Wall_North", extGroup.transform,
                new Vector3(0f, height * 0.5f, hz - extThick * 0.5f),
                new Vector3(depthX - extThick * 2f, height, extThick),
                Quaternion.identity, facadeMat, metersPerTile, collider: true);

            float leftSpanZ = (doorCenterZ - doorWidth * 0.5f) - (-hz);
            float rightSpanZ = hz - (doorCenterZ + doorWidth * 0.5f);
            float lintelHeight = height - doorTopY;

            CreateLocalTiledBox("Wall_FrontLeft", extGroup.transform,
                new Vector3(hx - extThick * 0.5f, height * 0.5f, -hz + leftSpanZ * 0.5f),
                new Vector3(extThick, height, leftSpanZ),
                Quaternion.identity, facadeMat, metersPerTile, collider: true);

            CreateLocalTiledBox("Wall_FrontRight", extGroup.transform,
                new Vector3(hx - extThick * 0.5f, height * 0.5f, hz - rightSpanZ * 0.5f),
                new Vector3(extThick, height, rightSpanZ),
                Quaternion.identity, facadeMat, metersPerTile, collider: true);

            CreateLocalTiledBox("Wall_FrontLintel", extGroup.transform,
                new Vector3(hx - extThick * 0.5f, doorTopY + lintelHeight * 0.5f, doorCenterZ),
                new Vector3(extThick, lintelHeight, doorWidth),
                Quaternion.identity, facadeMat, metersPerTile, collider: true);

            CreateLocalTiledBox("EntranceApron", extGroup.transform,
                new Vector3(hx + 0.65f, 0.015f, 0f),
                new Vector3(1.35f, 0.03f, widthZ),
                Quaternion.identity, mats.Sidewalk, 2.0f, collider: false);

            float glassSpan = Mathf.Max(1.4f, leftSpanZ - 1.0f);
            GameObject glassL = CreateLocalTiledBox("ShopGlass_Left", extGroup.transform,
                new Vector3(hx + 0.04f, 1.55f, -hz + 0.55f + glassSpan * 0.5f),
                new Vector3(0.06f, 2.25f, glassSpan),
                Quaternion.identity, mats.WindowLitWarm, 2.0f, collider: false, staticBatch: false);
            AttachWindowNightLight(glassL);

            GameObject glassR = CreateLocalTiledBox("ShopGlass_Right", extGroup.transform,
                new Vector3(hx + 0.04f, 1.55f, hz - 0.55f - glassSpan * 0.5f),
                new Vector3(0.06f, 2.25f, glassSpan),
                Quaternion.identity, mats.WindowLitWarm, 2.0f, collider: false, staticBatch: false);
            AttachWindowNightLight(glassR);

            CreateLocalTiledBox("Awning", extGroup.transform,
                new Vector3(hx + 0.68f, 3.05f, 0f),
                new Vector3(1.38f, 0.11f, Mathf.Max(2.6f, widthZ - 0.9f)),
                Quaternion.Euler(0f, 0f, -22f), awningMat, 2.0f, collider: false);

            float signBoardWidthZ = Mathf.Max(2.8f, widthZ - 1.4f);
            CreateLocalTiledBox("SignBoard", extGroup.transform,
                new Vector3(hx + 0.12f, 3.60f, 0f),
                new Vector3(0.14f, 0.62f, signBoardWidthZ),
                Quaternion.identity, mats.SedanTrimDark, 2.0f, collider: false);

            string signLabel = shopKind == EnterableShopKind.Pizzeria
                ? "PİZZA"
                : (shopKind == EnterableShopKind.Cafe ? "KAFE" : "MARKET");
            CreateWorldSignText(
                signLabel,
                extGroup.transform,
                new Vector3(hx + 0.21f, 3.60f, 0f),
                Quaternion.Euler(0f, -90f, 0f),
                new Vector2(signBoardWidthZ - 0.2f, 0.54f),
                new Color(0.95f, 0.94f, 0.90f, 1f));

            int winCols = Mathf.Max(2, Mathf.FloorToInt((widthZ - 1.2f) / 2.2f));
            float winStep = (widthZ - 1.6f) / Mathf.Max(1, winCols);
            float winStartZ = -(widthZ - 1.6f) * 0.5f + winStep * 0.5f;

            for (int floor = 1; floor < storeys; floor++)
            {
                float wy = 1.9f + floor * 3.0f;
                if (wy > height - 0.9f) break;

                for (int c = 0; c < winCols; c++)
                {
                    float wz = winStartZ + c * winStep;
                    bool litWin = shopRng.NextDouble() < 0.55;
                    Material wMat = litWin ? mats.WindowLitWarm : mats.SedanGlass;
                    GameObject winGo = CreateLocalTiledBox($"Win_F{floor}_{c}", extGroup.transform,
                        new Vector3(hx + 0.04f, wy, wz),
                        new Vector3(0.06f, 1.35f, 1.10f),
                        Quaternion.identity, wMat, 2.0f, collider: false, staticBatch: !litWin);
                    if (litWin)
                    {
                        AttachWindowNightLight(winGo);
                    }
                }
            }

            CreateLocalTiledBox("Cornice", extGroup.transform,
                new Vector3(0f, height + 0.24f, 0f),
                new Vector3(depthX + 0.40f, 0.48f, widthZ + 0.20f),
                Quaternion.identity, mats.PlasterTrim, 3.0f, collider: false);

            if (pitchedRoof)
            {
                float slopeLen = depthX * 0.58f;
                float roofPeakOffset = depthX * 0.25f;
                float roofY = height + 0.45f + roofPeakOffset * 0.45f;

                CreateLocalTiledBox("RoofSlope_East", extGroup.transform,
                    new Vector3(depthX * 0.24f, roofY, 0f),
                    new Vector3(slopeLen, 0.24f, widthZ + 0.30f),
                    Quaternion.Euler(0f, 0f, -24f), mats.TownRoofTile, 1.5f, collider: false);
                CreateLocalTiledBox("RoofSlope_West", extGroup.transform,
                    new Vector3(-depthX * 0.24f, roofY, 0f),
                    new Vector3(slopeLen, 0.24f, widthZ + 0.30f),
                    Quaternion.Euler(0f, 0f, 24f), mats.TownRoofTile, 1.5f, collider: false);
            }

            BuildCommercialShopInterior(
                shopId,
                shopKind,
                root.transform,
                depthX,
                widthZ,
                floorTopY,
                ceilBottomY,
                doorCenterZ,
                doorWidth,
                doorHeight,
                extThick,
                intThick,
                mats);
        }

        private static void BuildCommercialShopInterior(
            string shopId,
            EnterableShopKind shopKind,
            Transform shopRoot,
            float depthX,
            float widthZ,
            float floorTopY,
            float ceilBottomY,
            float doorCenterZ,
            float doorWidth,
            float doorHeight,
            float extThick,
            float intThick,
            MaterialSet mats)
        {
            float hx = depthX * 0.5f;
            float hz = widthZ * 0.5f;
            float wallTotalT = extThick + intThick;
            float innerH = ceilBottomY - floorTopY;
            float midY = floorTopY + innerH * 0.5f;
            float doorTopY = floorTopY + doorHeight;

            GameObject intShell = new GameObject("ShopInteriorShell");
            intShell.transform.SetParent(shopRoot, false);

            CreateLocalTiledBox("InteriorFloor_Wood", intShell.transform,
                new Vector3(0f, floorTopY * 0.5f, 0f),
                new Vector3(depthX - 0.08f, floorTopY, widthZ - 0.08f),
                Quaternion.identity, mats.TownWoodFloor, 1.8f, collider: true);

            CreateLocalTiledBox("InteriorCeiling", intShell.transform,
                new Vector3(0f, ceilBottomY + 0.07f, 0f),
                new Vector3(depthX - 0.08f, 0.14f, widthZ - 0.08f),
                Quaternion.identity, mats.TownInteriorPaintWarm, 2.5f, collider: true);

            CreateLocalTiledBox("IntWall_Back", intShell.transform,
                new Vector3(-hx + extThick + intThick * 0.5f, midY, 0f),
                new Vector3(intThick, innerH, widthZ - wallTotalT * 2f),
                Quaternion.identity, mats.TownInteriorPaintWarm, 2.5f, collider: false);

            CreateLocalTiledBox("IntWall_South", intShell.transform,
                new Vector3(0f, midY, -hz + extThick + intThick * 0.5f),
                new Vector3(depthX - wallTotalT * 2f, innerH, intThick),
                Quaternion.identity, mats.TownInteriorPaintWarm, 2.5f, collider: false);

            CreateLocalTiledBox("IntWall_North", intShell.transform,
                new Vector3(0f, midY, hz - extThick - intThick * 0.5f),
                new Vector3(depthX - wallTotalT * 2f, innerH, intThick),
                Quaternion.identity, mats.TownInteriorPaintWarm, 2.5f, collider: false);

            float leftInnerZ = (doorCenterZ - doorWidth * 0.5f) - (-hz + wallTotalT);
            float rightInnerZ = (hz - wallTotalT) - (doorCenterZ + doorWidth * 0.5f);
            float lintelInnerH = ceilBottomY - doorTopY;

            if (leftInnerZ > 0.1f)
            {
                CreateLocalTiledBox("IntWall_FrontLeft", intShell.transform,
                    new Vector3(hx - extThick - intThick * 0.5f, midY, -hz + wallTotalT + leftInnerZ * 0.5f),
                    new Vector3(intThick, innerH, leftInnerZ),
                    Quaternion.identity, mats.TownInteriorPaintWarm, 2.5f, collider: false);
            }
            if (rightInnerZ > 0.1f)
            {
                CreateLocalTiledBox("IntWall_FrontRight", intShell.transform,
                    new Vector3(hx - extThick - intThick * 0.5f, midY, hz - wallTotalT - rightInnerZ * 0.5f),
                    new Vector3(intThick, innerH, rightInnerZ),
                    Quaternion.identity, mats.TownInteriorPaintWarm, 2.5f, collider: false);
            }
            if (lintelInnerH > 0.05f)
            {
                CreateLocalTiledBox("IntWall_FrontLintel", intShell.transform,
                    new Vector3(hx - extThick - intThick * 0.5f, doorTopY + lintelInnerH * 0.5f, doorCenterZ),
                    new Vector3(intThick, lintelInnerH, doorWidth),
                    Quaternion.identity, mats.TownInteriorPaintWarm, 2.5f, collider: false);
            }

            BuildHingedDoor(
                $"{shopId}_front_door",
                shopRoot,
                new Vector3(hx - wallTotalT * 0.5f, floorTopY, doorCenterZ),
                doorWidth,
                doorHeight,
                mats);

            CreateInteriorZoneAndLight(
                shopId,
                shopRoot,
                new Vector3(0f, floorTopY + innerH * 0.5f, 0f),
                new Vector3(depthX - 0.16f, innerH + 0.2f, widthZ - 0.16f),
                new Vector3(0.4f, ceilBottomY - 0.24f, 0f),
                mats);

            // Service counter with cash register + ShopCounter InteractableTrigger
            GameObject furnRoot = new GameObject("ShopFurniture");
            furnRoot.transform.SetParent(shopRoot, false);

            GameObject counter = new GameObject("ServiceCounter");
            counter.transform.SetParent(furnRoot.transform, false);
            counter.transform.localPosition = new Vector3(-hx + 1.85f, floorTopY, 0f);

            CreateStaticPart("CounterBody", PrimitiveType.Cube, counter.transform,
                new Vector3(0f, 0.50f, 0f), Quaternion.identity, new Vector3(0.84f, 1.00f, 3.60f), mats.TownFurnitureWood);
            CreateStaticPart("CounterTop", PrimitiveType.Cube, counter.transform,
                new Vector3(0.03f, 1.03f, 0f), Quaternion.identity, new Vector3(0.94f, 0.06f, 3.72f), mats.PlasterTrim);
            CreateStaticPart("CashRegister", PrimitiveType.Cube, counter.transform,
                new Vector3(0.05f, 1.18f, -0.75f), Quaternion.identity, new Vector3(0.38f, 0.24f, 0.42f), mats.SedanTrimDark);
            CreateStaticPart("DisplayCase", PrimitiveType.Cube, counter.transform,
                new Vector3(0.08f, 1.24f, 0.72f), Quaternion.identity, new Vector3(0.48f, 0.36f, 1.28f), mats.SedanGlass);

            BoxCollider cCol = counter.AddComponent<BoxCollider>();
            cCol.center = new Vector3(0f, 0.54f, 0f);
            cCol.size = new Vector3(0.94f, 1.08f, 3.72f);
            MarkStaticEnvironment(counter);

            // Non-static ShopCounter InteractableTrigger right in front of the counter
            GameObject counterTrigGo = new GameObject("ShopCounter_Trigger");
            counterTrigGo.transform.SetParent(counter.transform, false);
            counterTrigGo.transform.localPosition = new Vector3(0.85f, 0.9f, 0f);
            InteractableTrigger shopTrig = counterTrigGo.AddComponent<InteractableTrigger>();
            shopTrig.Configure(InteractableKind.ShopCounter, shopId, "ui.btn.shop", 2.4f);

            // Shopkeeper behind counter
            GameObject clerkGo = new GameObject($"NPC_Shopkeeper_{shopId}");
            clerkGo.transform.SetParent(furnRoot.transform, false);
            clerkGo.transform.localPosition = new Vector3(-hx + 0.85f, floorTopY, -0.25f);
            clerkGo.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Material clerkShirt = shopKind == EnterableShopKind.Pizzeria
                ? mats.PedShirtRed
                : (shopKind == EnterableShopKind.Cafe ? mats.PedShirtTeal : mats.CharShirt);
            BlenderCharacterVariant clerkVariant = shopKind == EnterableShopKind.Cafe
                ? BlenderCharacterVariant.CasualFemale
                : (shopKind == EnterableShopKind.Pizzeria ? BlenderCharacterVariant.Worker : BlenderCharacterVariant.CasualMale);
            BuildBlenderHumanoid(
                clerkGo.transform,
                mats,
                clerkVariant,
                clerkShirt,
                mats.CharPants,
                shopKind == EnterableShopKind.Pizzeria ? mats.CharSkinMedium : mats.CharSkin,
                shopKind == EnterableShopKind.Cafe ? mats.CharHairBrown : mats.CharHair,
                clerkVariant == BlenderCharacterVariant.CasualFemale ? 1.71f : 1.76f);

            // Role-specific interior decor & seating
            if (shopKind == EnterableShopKind.Cafe || shopKind == EnterableShopKind.Pizzeria)
            {
                CreateDiningTableWith4Chairs($"{shopId}_table_s", furnRoot.transform,
                    new Vector3(1.15f, floorTopY, -2.25f), Quaternion.identity,
                    mats.TownFurnitureWood, shopKind == EnterableShopKind.Pizzeria ? mats.TownAwningTerracotta : mats.TownFabricTeal);
                CreateDiningTableWith4Chairs($"{shopId}_table_n", furnRoot.transform,
                    new Vector3(1.15f, floorTopY, 2.25f), Quaternion.identity,
                    mats.TownFurnitureWood, shopKind == EnterableShopKind.Pizzeria ? mats.TownFabricMustard : mats.TownFabricTeal);
            }
            else
            {
                // Market grocery shelves & chillers
                CreateFridge("Market_Chiller_1", furnRoot.transform,
                    new Vector3(-hx + 0.55f, floorTopY, -2.85f), Quaternion.Euler(0f, 90f, 0f), mats);
                CreateFridge("Market_Chiller_2", furnRoot.transform,
                    new Vector3(-hx + 0.55f, floorTopY, 2.85f), Quaternion.Euler(0f, 90f, 0f), mats);
                CreateWardrobe("Market_AisleShelf_S", furnRoot.transform,
                    new Vector3(1.10f, floorTopY, -2.35f), Quaternion.identity, mats);
                CreateWardrobe("Market_AisleShelf_N", furnRoot.transform,
                    new Vector3(1.10f, floorTopY, 2.35f), Quaternion.Euler(0f, 180f, 0f), mats);
            }
        }

        private static void BuildHouseInteriorShellAndFurniture(
            string buildingId,
            Transform buildingRoot,
            float depthX,
            float widthZ,
            float floorTopY,
            float ceilBottomY,
            float doorCenterZ,
            float doorWidth,
            float doorHeight,
            float extThick,
            float intThick,
            Material interiorWallMat,
            MaterialSet mats,
            System.Random rng,
            int houseSeed = 0)
        {
            float hx = depthX * 0.5f;
            float hz = widthZ * 0.5f;
            float wallTotalT = extThick + intThick;
            float innerH = ceilBottomY - floorTopY;
            float midY = floorTopY + innerH * 0.5f;
            float doorTopY = floorTopY + doorHeight;

            int theme = Mathf.Abs(houseSeed) % 4;
            // Theme 0: Modern Open-Plan Loft
            // Theme 1: Classic Warm Family Home
            // Theme 2: Artist & Music Atelier
            // Theme 3: Home-Office & Fitness Studio
            Material wallAccentMat = theme == 0
                ? mats.TownFabricTeal
                : (theme == 2 ? mats.TownAwningTerracotta : (theme == 3 ? mats.TownFabricGrey : interiorWallMat));
            Material floorMat = theme == 0 ? mats.TownInteriorPaintPearl : mats.TownWoodFloor;

            GameObject intShell = new GameObject($"InteriorWallsAndFloors_Theme{theme}");
            intShell.transform.SetParent(buildingRoot, false);

            CreateLocalTiledBox("InteriorFloor_Wood", intShell.transform,
                new Vector3(0f, floorTopY * 0.5f, 0f),
                new Vector3(depthX - 0.08f, floorTopY, widthZ - 0.08f),
                Quaternion.identity, floorMat, 1.8f, collider: true);

            CreateLocalTiledBox("InteriorCeiling", intShell.transform,
                new Vector3(0f, ceilBottomY + 0.07f, 0f),
                new Vector3(depthX - 0.08f, 0.14f, widthZ - 0.08f),
                Quaternion.identity, interiorWallMat, 2.5f, collider: true);

            // Accent back wall on themes 0, 2, 3 for instant visual distinctiveness upon opening door
            CreateLocalTiledBox("IntWall_Back", intShell.transform,
                new Vector3(-hx + extThick + intThick * 0.5f, midY, 0f),
                new Vector3(intThick, innerH, widthZ - wallTotalT * 2f),
                Quaternion.identity, wallAccentMat, 2.5f, collider: false);

            CreateLocalTiledBox("IntWall_South", intShell.transform,
                new Vector3(0f, midY, -hz + extThick + intThick * 0.5f),
                new Vector3(depthX - wallTotalT * 2f, innerH, intThick),
                Quaternion.identity, interiorWallMat, 2.5f, collider: false);

            CreateLocalTiledBox("IntWall_North", intShell.transform,
                new Vector3(0f, midY, hz - extThick - intThick * 0.5f),
                new Vector3(depthX - wallTotalT * 2f, innerH, intThick),
                Quaternion.identity, interiorWallMat, 2.5f, collider: false);

            float leftInnerZ = (doorCenterZ - doorWidth * 0.5f) - (-hz + wallTotalT);
            float rightInnerZ = (hz - wallTotalT) - (doorCenterZ + doorWidth * 0.5f);
            float lintelInnerH = ceilBottomY - doorTopY;

            if (leftInnerZ > 0.1f)
            {
                CreateLocalTiledBox("IntWall_FrontLeft", intShell.transform,
                    new Vector3(hx - extThick - intThick * 0.5f, midY, -hz + wallTotalT + leftInnerZ * 0.5f),
                    new Vector3(intThick, innerH, leftInnerZ),
                    Quaternion.identity, interiorWallMat, 2.5f, collider: false);
            }

            if (rightInnerZ > 0.1f)
            {
                CreateLocalTiledBox("IntWall_FrontRight", intShell.transform,
                    new Vector3(hx - extThick - intThick * 0.5f, midY, hz - wallTotalT - rightInnerZ * 0.5f),
                    new Vector3(intThick, innerH, rightInnerZ),
                    Quaternion.identity, interiorWallMat, 2.5f, collider: false);
            }

            if (lintelInnerH > 0.05f)
            {
                CreateLocalTiledBox("IntWall_FrontLintel", intShell.transform,
                    new Vector3(hx - extThick - intThick * 0.5f, doorTopY + lintelInnerH * 0.5f, doorCenterZ),
                    new Vector3(intThick, lintelInnerH, doorWidth),
                    Quaternion.identity, interiorWallMat, 2.5f, collider: false);
            }

            float zSign = rng.Next(2) == 0 ? 1f : -1f;
            Vector3 PlacePos(float lx, float ly, float lz) => new Vector3(lx, ly, lz * zSign);
            Quaternion PlaceYaw(float deg) => Quaternion.Euler(0f, zSign > 0f ? deg : (180f - deg), 0f);

            float innerHalfX = hx - wallTotalT;
            float innerFrontX = hx - wallTotalT;
            float innerBackX = -hx + wallTotalT;
            float innerSouthZ = -hz + wallTotalT;
            float innerNorthZ = hz - wallTotalT;

            // Only Theme 1 (Classic Family Home) uses the full bedroom partition wall.
            // Theme 2 uses a stylish short half-divider; Themes 0 & 3 are open-plan lofts/studios!
            if (theme == 1)
            {
                float partZ = 1.45f * zSign;
                const float partThick = 0.14f;
                const float partDoorWidth = 1.50f;
                const float partDoorCenterX = 0.30f;
                const float partDoorH = 2.30f;
                float segBackLen = (partDoorCenterX - partDoorWidth * 0.5f) - (-innerHalfX);
                float segFrontLen = innerHalfX - (partDoorCenterX + partDoorWidth * 0.5f);
                float partLintelH = innerH - partDoorH;

                CreateLocalTiledBox("Partition_BackSeg", intShell.transform,
                    new Vector3(-innerHalfX + segBackLen * 0.5f, midY, partZ),
                    new Vector3(segBackLen, innerH, partThick),
                    Quaternion.identity, interiorWallMat, 2.5f, collider: true);

                CreateLocalTiledBox("Partition_FrontSeg", intShell.transform,
                    new Vector3(innerHalfX - segFrontLen * 0.5f, midY, partZ),
                    new Vector3(segFrontLen, innerH, partThick),
                    Quaternion.identity, interiorWallMat, 2.5f, collider: true);

                CreateLocalTiledBox("Partition_Lintel", intShell.transform,
                    new Vector3(partDoorCenterX, floorTopY + partDoorH + partLintelH * 0.5f, partZ),
                    new Vector3(partDoorWidth, partLintelH, partThick),
                    Quaternion.identity, interiorWallMat, 2.5f, collider: true);
            }
            else if (theme == 2)
            {
                // Half-width gallery divider separating sleeping corner from art atelier
                float partZ = 1.65f * zSign;
                float stubLen = depthX * 0.42f;
                CreateLocalTiledBox("Atelier_GalleryDivider", intShell.transform,
                    new Vector3(-innerHalfX + stubLen * 0.5f, midY, partZ),
                    new Vector3(stubLen, innerH, 0.14f),
                    Quaternion.identity, interiorWallMat, 2.5f, collider: true);
            }

            BuildHingedDoor(
                $"{buildingId}_front_door",
                buildingRoot,
                new Vector3(hx - wallTotalT * 0.5f, floorTopY, doorCenterZ),
                doorWidth,
                doorHeight,
                mats);

            CreateInteriorZoneAndLight(
                buildingId,
                buildingRoot,
                new Vector3(0f, floorTopY + innerH * 0.5f, 0f),
                new Vector3(depthX - 0.16f, innerH + 0.2f, widthZ - 0.16f),
                new Vector3(0.35f, ceilBottomY - 0.24f, -0.35f * zSign),
                mats);

            GameObject furnRoot = new GameObject("InteriorFurniture");
            furnRoot.transform.SetParent(buildingRoot, false);

            Material[] fabrics =
            {
                mats.TownFabricGrey,
                mats.TownFabricTeal,
                mats.TownFabricMustard
            };
            Material mainFabric = fabrics[(theme + rng.Next(fabrics.Length)) % fabrics.Length];
            Material accentFabric = fabrics[(theme + 1) % fabrics.Length];

            float livingCenterX = Mathf.Min(2.05f, innerFrontX - 1.35f);
            float sofaZ = innerSouthZ + 0.52f;
            float bedZ = Mathf.Min(innerNorthZ - 1.05f, 2.95f);

            if (theme == 0)
            {
                // THEME 0: MODERN OPEN-PLAN LOFT (kitchen island, L-lounge, big TV, airy bed nook)
                CreateRug("Loft_Rug", furnRoot.transform,
                    PlacePos(1.35f, floorTopY, -1.65f),
                    new Vector2(3.10f, 2.40f), mats.TownFabricTeal, mats.PlasterTrim);

                CreateSofa3Seat($"{buildingId}_sofa", furnRoot.transform,
                    PlacePos(1.45f, floorTopY, innerSouthZ + 0.55f),
                    PlaceYaw(0f), mats.TownFabricGrey, mats.SedanTrimDark);

                CreateArmchair($"{buildingId}_armchair_loft", furnRoot.transform,
                    PlacePos(innerFrontX - 0.85f, floorTopY, innerSouthZ + 2.05f),
                    PlaceYaw(-90f), mats.TownFabricMustard, mats.SedanTrimDark);

                CreateCoffeeTable("Loft_CoffeeTable", furnRoot.transform,
                    PlacePos(1.45f, floorTopY, innerSouthZ + 1.95f),
                    PlaceYaw(0f), mats.SedanTrimDark);

                CreateTvOnCabinet("Loft_TVUnit", furnRoot.transform,
                    PlacePos(1.45f, floorTopY, innerNorthZ - 0.35f),
                    PlaceYaw(180f), mats);

                CreateKitchenCounter("Loft_KitchenCounter", furnRoot.transform,
                    PlacePos(innerBackX + 0.36f, floorTopY, innerSouthZ + 1.55f),
                    PlaceYaw(90f), mats);

                CreateFridge("Loft_Fridge", furnRoot.transform,
                    PlacePos(innerBackX + 0.40f, floorTopY, innerSouthZ + 3.20f),
                    PlaceYaw(90f), mats);

                CreateKitchenIsland($"{buildingId}_island", furnRoot.transform,
                    PlacePos(innerBackX + 2.15f, floorTopY, innerSouthZ + 1.85f),
                    PlaceYaw(90f), mats);

                CreateDoubleBed("Loft_Bed", $"{buildingId}_bed", furnRoot.transform,
                    PlacePos(innerBackX + 1.12f, floorTopY, bedZ),
                    PlaceYaw(90f), mats.TownFabricTeal, mats);

                CreateNightstand("Loft_Nightstand", furnRoot.transform,
                    PlacePos(innerBackX + 0.30f, floorTopY, bedZ - 1.15f),
                    PlaceYaw(90f), mats);

                CreateFloorLamp("Loft_FloorLamp", furnRoot.transform,
                    PlacePos(innerFrontX - 0.34f, floorTopY, innerNorthZ - 0.40f),
                    mats);

                CreateWallPainting("Loft_Art", furnRoot.transform,
                    PlacePos(innerBackX + 0.04f, floorTopY + 1.65f, 0.2f),
                    PlaceYaw(90f), mats.TownFabricMustard, mats.SedanTrimDark);
            }
            else if (theme == 1)
            {
                // THEME 1: CLASSIC WARM FAMILY HOME (partitioned rooms, dining set, bookcase)
                CreateRug("Living_Rug", furnRoot.transform,
                    PlacePos(livingCenterX, floorTopY, sofaZ + 1.52f),
                    new Vector2(2.55f, 1.85f), accentFabric, mats.PlasterTrim);

                CreateSofa3Seat($"{buildingId}_sofa", furnRoot.transform,
                    PlacePos(livingCenterX, floorTopY, sofaZ),
                    PlaceYaw(0f), mainFabric, mats.TownFurnitureWood);

                CreateCoffeeTable("Living_CoffeeTable", furnRoot.transform,
                    PlacePos(livingCenterX, floorTopY, sofaZ + 1.56f),
                    PlaceYaw(0f), mats.TownFurnitureWood);

                CreateTvOnCabinet("Living_TVUnit", furnRoot.transform,
                    PlacePos(livingCenterX + 0.05f, floorTopY, 1.02f),
                    PlaceYaw(180f), mats);

                CreateFloorLamp("Living_FloorLamp", furnRoot.transform,
                    PlacePos(innerFrontX - 0.28f, floorTopY, innerSouthZ + 0.34f),
                    mats);

                CreateArmchair($"{buildingId}_armchair_1", furnRoot.transform,
                    PlacePos(livingCenterX - 1.55f, floorTopY, sofaZ + 0.55f),
                    PlaceYaw(45f), accentFabric, mats.TownFurnitureWood);

                CreateKitchenCounter("Kitchen_Counter", furnRoot.transform,
                    PlacePos(innerBackX + 0.34f, floorTopY, innerSouthZ + 1.35f),
                    PlaceYaw(90f), mats);

                CreateFridge("Kitchen_Fridge", furnRoot.transform,
                    PlacePos(innerBackX + 0.38f, floorTopY, innerSouthZ + 2.95f),
                    PlaceYaw(90f), mats);

                CreateDiningTableWith4Chairs($"{buildingId}_dining", furnRoot.transform,
                    PlacePos(innerBackX + 1.95f, floorTopY, innerSouthZ + 2.30f),
                    PlaceYaw(0f), mats.TownFurnitureWood, mainFabric);

                CreateBookcase("Family_Bookcase", furnRoot.transform,
                    PlacePos(innerFrontX - 0.36f, floorTopY, innerNorthZ - 1.55f),
                    PlaceYaw(-90f), mats);

                CreateDoubleBed("Bedroom_Bed", $"{buildingId}_bed", furnRoot.transform,
                    PlacePos(innerBackX + 1.08f, floorTopY, bedZ),
                    PlaceYaw(90f), accentFabric, mats);

                CreateNightstand("Bedroom_Nightstand", furnRoot.transform,
                    PlacePos(innerBackX + 0.28f, floorTopY, bedZ - 1.12f),
                    PlaceYaw(90f), mats);

                CreateWardrobe("Bedroom_Wardrobe", furnRoot.transform,
                    PlacePos(innerFrontX - 1.20f, floorTopY, innerNorthZ - 0.32f),
                    PlaceYaw(180f), mats);
            }
            else if (theme == 2)
            {
                // THEME 2: ARTIST & MUSIC ATELIER (easels, framed canvases, piano, studio workbench)
                CreateRug("Atelier_Rug", furnRoot.transform,
                    PlacePos(0.45f, floorTopY, -1.65f),
                    new Vector2(3.20f, 2.30f), mats.TownAwningTerracotta, mats.TownInteriorPaintCream);

                CreateArtistEasel("Atelier_Easel_Main", furnRoot.transform,
                    PlacePos(-0.85f, floorTopY, -1.95f),
                    PlaceYaw(35f), mats.TownFabricTeal, mats);

                CreateArtistEasel("Atelier_Easel_Small", furnRoot.transform,
                    PlacePos(1.85f, floorTopY, -2.55f),
                    PlaceYaw(-30f), mats.TownFabricMustard, mats);

                CreateStudioWorkbench("Atelier_Workbench", furnRoot.transform,
                    PlacePos(innerBackX + 0.55f, floorTopY, -1.65f),
                    PlaceYaw(90f), mats);

                CreatePianoDesk($"{buildingId}_piano", furnRoot.transform,
                    PlacePos(1.55f, floorTopY, innerNorthZ - 0.42f),
                    PlaceYaw(180f), mats);

                CreateArmchair($"{buildingId}_atelier_chair", furnRoot.transform,
                    PlacePos(1.65f, floorTopY, innerSouthZ + 0.72f),
                    PlaceYaw(20f), mats.TownFabricMustard, mats.TownFurnitureWood);

                CreateFloorLamp("Atelier_Lamp", furnRoot.transform,
                    PlacePos(innerFrontX - 0.35f, floorTopY, innerSouthZ + 0.38f),
                    mats);

                CreateDoubleBed("Atelier_Bed", $"{buildingId}_bed", furnRoot.transform,
                    PlacePos(innerBackX + 1.08f, floorTopY, bedZ),
                    PlaceYaw(90f), mats.TownAwningTerracotta, mats);

                CreateBookcase("Atelier_Books", furnRoot.transform,
                    PlacePos(innerBackX + 0.35f, floorTopY, 0.70f),
                    PlaceYaw(90f), mats);

                CreateWallPainting("Atelier_Canvas_1", furnRoot.transform,
                    PlacePos(0.10f, floorTopY + 1.65f, innerSouthZ + 0.05f),
                    PlaceYaw(0f), mats.TownFabricTeal, mats.TownFurnitureWood);

                CreateWallPainting("Atelier_Canvas_2", furnRoot.transform,
                    PlacePos(innerBackX + 0.05f, floorTopY + 1.72f, -0.45f),
                    PlaceYaw(90f), mats.TownFabricMustard, mats.TownFurnitureWood);
            }
            else
            {
                // THEME 3: HOME-OFFICE & FITNESS STUDIO (dual-monitor PC desk, gym bench & barbell, modern lounge)
                CreateHomeOfficeDesk($"{buildingId}_office", furnRoot.transform,
                    PlacePos(1.35f, floorTopY, innerNorthZ - 0.65f),
                    PlaceYaw(180f), mats);

                CreateBookcase("Office_Bookcase", furnRoot.transform,
                    PlacePos(innerFrontX - 0.38f, floorTopY, innerNorthZ - 1.95f),
                    PlaceYaw(-90f), mats);

                CreateHomeGymCorner("Fitness_GymZone", furnRoot.transform,
                    PlacePos(-0.85f, floorTopY, innerSouthZ + 1.45f),
                    PlaceYaw(0f), mats);

                CreateSofa3Seat($"{buildingId}_studio_sofa", furnRoot.transform,
                    PlacePos(1.75f, floorTopY, innerSouthZ + 0.58f),
                    PlaceYaw(0f), mats.TownFabricTeal, mats.SedanTrimDark);

                CreateCoffeeTable("Studio_CoffeeTable", furnRoot.transform,
                    PlacePos(1.75f, floorTopY, innerSouthZ + 1.85f),
                    PlaceYaw(0f), mats.SedanTrimDark);

                CreateFridge("Studio_MiniFridge", furnRoot.transform,
                    PlacePos(innerBackX + 0.40f, floorTopY, 0.35f),
                    PlaceYaw(90f), mats);

                CreateDoubleBed("Studio_Bed", $"{buildingId}_bed", furnRoot.transform,
                    PlacePos(innerBackX + 1.08f, floorTopY, bedZ),
                    PlaceYaw(90f), mats.TownFabricGrey, mats);

                CreateWardrobe("Studio_Wardrobe", furnRoot.transform,
                    PlacePos(innerBackX + 0.36f, floorTopY, innerNorthZ - 0.72f),
                    PlaceYaw(90f), mats);

                CreateFloorLamp("Studio_Lamp", furnRoot.transform,
                    PlacePos(innerFrontX - 0.32f, floorTopY, innerSouthZ + 0.36f),
                    mats);
            }
        }

        private static void CreateKitchenIsland(
            string seatIdPrefix,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            MaterialSet mats)
        {
            GameObject island = new GameObject("KitchenIsland");
            island.transform.SetParent(parent, false);
            island.transform.localPosition = localPos;
            island.transform.localRotation = localRot;

            CreateStaticPart("IslandBody", PrimitiveType.Cube, island.transform,
                new Vector3(0f, 0.44f, 0f), Quaternion.identity, new Vector3(1.85f, 0.88f, 0.72f), mats.TownFurnitureWood);
            CreateStaticPart("IslandCounterTop", PrimitiveType.Cube, island.transform,
                new Vector3(0f, 0.90f, 0.04f), Quaternion.identity, new Vector3(1.96f, 0.06f, 0.88f), mats.PlasterTrim);

            BoxCollider col = island.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.46f, 0f);
            col.size = new Vector3(1.96f, 0.92f, 0.88f);
            MarkStaticEnvironment(island);

            // Two bar stools on the overhang side
            for (int i = 0; i < 2; i++)
            {
                float sx = i == 0 ? -0.48f : 0.48f;
                GameObject stool = new GameObject($"BarStool_{i + 1}");
                stool.transform.SetParent(island.transform, false);
                stool.transform.localPosition = new Vector3(sx, 0f, 0.62f);
                stool.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                CreateStaticPart("Post", PrimitiveType.Cylinder, stool.transform,
                    new Vector3(0f, 0.32f, 0f), Quaternion.identity, new Vector3(0.06f, 0.32f, 0.06f), mats.SedanTrimDark);
                CreateStaticPart("Base", PrimitiveType.Cylinder, stool.transform,
                    new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(0.32f, 0.02f, 0.32f), mats.SedanChrome);
                CreateStaticPart("Seat", PrimitiveType.Cylinder, stool.transform,
                    new Vector3(0f, 0.65f, 0f), Quaternion.identity, new Vector3(0.34f, 0.04f, 0.34f), mats.TownFabricMustard);
                CreateSeatSlot($"StoolSlot_{i + 1}", $"{seatIdPrefix}_stool_{i + 1}", stool.transform,
                    new Vector3(0f, 0.66f, 0f), Quaternion.identity, 1.35f);
            }
        }

        private static void CreateBookcase(
            string name,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            MaterialSet mats)
        {
            GameObject bc = new GameObject(name);
            bc.transform.SetParent(parent, false);
            bc.transform.localPosition = localPos;
            bc.transform.localRotation = localRot;

            CreateStaticPart("BackPanel", PrimitiveType.Cube, bc.transform,
                new Vector3(0f, 0.96f, -0.15f), Quaternion.identity, new Vector3(1.10f, 1.92f, 0.04f), mats.TownFurnitureWood);
            CreateStaticPart("SideL", PrimitiveType.Cube, bc.transform,
                new Vector3(-0.53f, 0.96f, 0f), Quaternion.identity, new Vector3(0.04f, 1.92f, 0.34f), mats.TownFurnitureWood);
            CreateStaticPart("SideR", PrimitiveType.Cube, bc.transform,
                new Vector3(0.53f, 0.96f, 0f), Quaternion.identity, new Vector3(0.04f, 1.92f, 0.34f), mats.TownFurnitureWood);

            Material[] bookMats = { mats.TownAwningTerracotta, mats.TownFabricTeal, mats.TownFabricMustard, mats.PedShirtRed };
            for (int s = 0; s < 5; s++)
            {
                float sy = 0.08f + s * 0.44f;
                CreateStaticPart($"Shelf_{s}", PrimitiveType.Cube, bc.transform,
                    new Vector3(0f, sy, 0f), Quaternion.identity, new Vector3(1.06f, 0.035f, 0.32f), mats.TownFurnitureWood);
                if (s < 4)
                {
                    CreateStaticPart($"BooksL_{s}", PrimitiveType.Cube, bc.transform,
                        new Vector3(-0.24f, sy + 0.14f, 0f), Quaternion.identity, new Vector3(0.38f, 0.24f, 0.20f), bookMats[s % bookMats.Length]);
                    CreateStaticPart($"BooksR_{s}", PrimitiveType.Cube, bc.transform,
                        new Vector3(0.24f, sy + 0.13f, 0f), Quaternion.identity, new Vector3(0.32f, 0.22f, 0.20f), bookMats[(s + 2) % bookMats.Length]);
                }
            }

            BoxCollider col = bc.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.96f, 0f);
            col.size = new Vector3(1.12f, 1.92f, 0.36f);
            MarkStaticEnvironment(bc);
        }

        private static void CreateArtistEasel(
            string name,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Material canvasColorMat,
            MaterialSet mats)
        {
            GameObject easel = new GameObject(name);
            easel.transform.SetParent(parent, false);
            easel.transform.localPosition = localPos;
            easel.transform.localRotation = localRot;

            CreateStaticPart("LegL", PrimitiveType.Cube, easel.transform,
                new Vector3(-0.26f, 0.80f, 0.04f), Quaternion.Euler(10f, 0f, -8f), new Vector3(0.05f, 1.66f, 0.05f), mats.TownFurnitureWood);
            CreateStaticPart("LegR", PrimitiveType.Cube, easel.transform,
                new Vector3(0.26f, 0.80f, 0.04f), Quaternion.Euler(10f, 0f, 8f), new Vector3(0.05f, 1.66f, 0.05f), mats.TownFurnitureWood);
            CreateStaticPart("BackLeg", PrimitiveType.Cube, easel.transform,
                new Vector3(0f, 0.72f, -0.32f), Quaternion.Euler(-22f, 0f, 0f), new Vector3(0.05f, 1.56f, 0.05f), mats.TownFurnitureWood);
            CreateStaticPart("Ledge", PrimitiveType.Cube, easel.transform,
                new Vector3(0f, 0.84f, 0.14f), Quaternion.identity, new Vector3(0.76f, 0.04f, 0.12f), mats.TownFurnitureWood);
            CreateStaticPart("CanvasFrame", PrimitiveType.Cube, easel.transform,
                new Vector3(0f, 1.24f, 0.08f), Quaternion.Euler(10f, 0f, 0f), new Vector3(0.72f, 0.78f, 0.04f), mats.PlasterTrim);
            CreateStaticPart("CanvasArt", PrimitiveType.Cube, easel.transform,
                new Vector3(0f, 1.24f, 0.105f), Quaternion.Euler(10f, 0f, 0f), new Vector3(0.62f, 0.68f, 0.015f), canvasColorMat);

            BoxCollider col = easel.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.82f, 0f);
            col.size = new Vector3(0.74f, 1.64f, 0.64f);
            MarkStaticEnvironment(easel);
        }

        private static void CreateStudioWorkbench(
            string name,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            MaterialSet mats)
        {
            GameObject wb = new GameObject(name);
            wb.transform.SetParent(parent, false);
            wb.transform.localPosition = localPos;
            wb.transform.localRotation = localRot;

            CreateStaticPart("Top", PrimitiveType.Cube, wb.transform,
                new Vector3(0f, 0.78f, 0f), Quaternion.identity, new Vector3(1.90f, 0.06f, 0.76f), mats.TownFurnitureWood);
            Vector2[] legs = { new Vector2(-0.84f, -0.30f), new Vector2(0.84f, -0.30f), new Vector2(-0.84f, 0.30f), new Vector2(0.84f, 0.30f) };
            for (int i = 0; i < 4; i++)
            {
                CreateStaticPart($"Leg_{i}", PrimitiveType.Cube, wb.transform,
                    new Vector3(legs[i].x, 0.38f, legs[i].y), Quaternion.identity, new Vector3(0.06f, 0.76f, 0.06f), mats.SedanTrimDark);
            }
            // Paint jars & palette on top
            CreateStaticPart("PaintJar1", PrimitiveType.Cylinder, wb.transform,
                new Vector3(-0.55f, 0.89f, -0.10f), Quaternion.identity, new Vector3(0.14f, 0.09f, 0.14f), mats.TownAwningTerracotta);
            CreateStaticPart("PaintJar2", PrimitiveType.Cylinder, wb.transform,
                new Vector3(-0.34f, 0.89f, 0.06f), Quaternion.identity, new Vector3(0.12f, 0.09f, 0.12f), mats.TownFabricTeal);
            CreateStaticPart("Sketchbook", PrimitiveType.Cube, wb.transform,
                new Vector3(0.32f, 0.825f, 0.04f), Quaternion.Euler(0f, 12f, 0f), new Vector3(0.42f, 0.03f, 0.32f), mats.PlasterTrim);

            BoxCollider col = wb.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.42f, 0f);
            col.size = new Vector3(1.90f, 0.84f, 0.78f);
            MarkStaticEnvironment(wb);
        }

        private static void CreatePianoDesk(
            string seatId,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            MaterialSet mats)
        {
            GameObject piano = new GameObject("AcousticPiano");
            piano.transform.SetParent(parent, false);
            piano.transform.localPosition = localPos;
            piano.transform.localRotation = localRot;

            CreateStaticPart("Body", PrimitiveType.Cube, piano.transform,
                new Vector3(0f, 0.62f, -0.06f), Quaternion.identity, new Vector3(1.48f, 1.24f, 0.56f), mats.SedanTrimDark);
            CreateStaticPart("KeyBed", PrimitiveType.Cube, piano.transform,
                new Vector3(0f, 0.73f, 0.24f), Quaternion.identity, new Vector3(1.44f, 0.10f, 0.28f), mats.SedanTrimDark);
            CreateStaticPart("WhiteKeys", PrimitiveType.Cube, piano.transform,
                new Vector3(0f, 0.79f, 0.26f), Quaternion.identity, new Vector3(1.32f, 0.03f, 0.18f), mats.PlasterTrim);
            CreateStaticPart("BlackKeys", PrimitiveType.Cube, piano.transform,
                new Vector3(0f, 0.81f, 0.21f), Quaternion.identity, new Vector3(1.24f, 0.025f, 0.08f), mats.SedanTrimDark);

            BoxCollider col = piano.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.62f, 0.05f);
            col.size = new Vector3(1.48f, 1.24f, 0.76f);
            MarkStaticEnvironment(piano);

            // Piano bench
            GameObject bench = new GameObject("PianoBench");
            bench.transform.SetParent(piano.transform, false);
            bench.transform.localPosition = new Vector3(0f, 0f, 0.72f);
            bench.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            CreateStaticPart("BenchTop", PrimitiveType.Cube, bench.transform,
                new Vector3(0f, 0.46f, 0f), Quaternion.identity, new Vector3(0.76f, 0.08f, 0.34f), mats.SedanTrimDark);
            CreateStaticPart("BenchLegs", PrimitiveType.Cube, bench.transform,
                new Vector3(0f, 0.22f, 0f), Quaternion.identity, new Vector3(0.68f, 0.44f, 0.28f), mats.TownFurnitureWood);
            CreateSeatSlot("PianoBenchSlot", seatId, bench.transform,
                new Vector3(0f, 0.49f, 0f), Quaternion.identity, 1.35f);
        }

        private static void CreateHomeOfficeDesk(
            string seatId,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            MaterialSet mats)
        {
            GameObject office = new GameObject("HomeOfficeSetup");
            office.transform.SetParent(parent, false);
            office.transform.localPosition = localPos;
            office.transform.localRotation = localRot;

            CreateStaticPart("DeskTop", PrimitiveType.Cube, office.transform,
                new Vector3(0f, 0.74f, 0f), Quaternion.identity, new Vector3(1.68f, 0.05f, 0.78f), mats.TownFurnitureWood);
            CreateStaticPart("PedestalL", PrimitiveType.Cube, office.transform,
                new Vector3(-0.62f, 0.36f, 0f), Quaternion.identity, new Vector3(0.42f, 0.72f, 0.68f), mats.SedanTrimDark);
            CreateStaticPart("LegR", PrimitiveType.Cube, office.transform,
                new Vector3(0.76f, 0.36f, 0f), Quaternion.identity, new Vector3(0.06f, 0.72f, 0.68f), mats.SedanTrimDark);

            // Dual Monitors + Keyboard + PC Tower
            CreateStaticPart("MonitorL", PrimitiveType.Cube, office.transform,
                new Vector3(-0.34f, 1.04f, -0.18f), Quaternion.Euler(0f, 12f, 0f), new Vector3(0.60f, 0.36f, 0.04f), mats.SedanTrimDark);
            CreateStaticPart("ScreenL", PrimitiveType.Cube, office.transform,
                new Vector3(-0.34f, 1.04f, -0.155f), Quaternion.Euler(0f, 12f, 0f), new Vector3(0.55f, 0.31f, 0.01f), mats.SedanGlass);
            CreateStaticPart("MonitorR", PrimitiveType.Cube, office.transform,
                new Vector3(0.32f, 1.04f, -0.18f), Quaternion.Euler(0f, -12f, 0f), new Vector3(0.60f, 0.36f, 0.04f), mats.SedanTrimDark);
            CreateStaticPart("ScreenR", PrimitiveType.Cube, office.transform,
                new Vector3(0.32f, 1.04f, -0.155f), Quaternion.Euler(0f, -12f, 0f), new Vector3(0.55f, 0.31f, 0.01f), mats.SedanGlass);
            CreateStaticPart("Keyboard", PrimitiveType.Cube, office.transform,
                new Vector3(-0.02f, 0.775f, 0.14f), Quaternion.identity, new Vector3(0.46f, 0.02f, 0.16f), mats.SedanTrimDark);
            CreateStaticPart("PcTower", PrimitiveType.Cube, office.transform,
                new Vector3(0.64f, 0.98f, -0.10f), Quaternion.identity, new Vector3(0.22f, 0.44f, 0.44f), mats.SedanTrimDark);

            BoxCollider col = office.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.44f, 0f);
            col.size = new Vector3(1.70f, 0.88f, 0.80f);
            MarkStaticEnvironment(office);

            CreateDiningChair("OfficeChair", $"{seatId}_chair", office.transform,
                new Vector3(0f, 0f, 0.68f), Quaternion.Euler(0f, 180f, 0f), mats.SedanTrimDark, mats.TownFabricTeal);
        }

        private static void CreateHomeGymCorner(
            string name,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            MaterialSet mats)
        {
            GameObject gym = new GameObject(name);
            gym.transform.SetParent(parent, false);
            gym.transform.localPosition = localPos;
            gym.transform.localRotation = localRot;

            // Rubber workout mat
            CreateStaticPart("RubberMat", PrimitiveType.Cube, gym.transform,
                new Vector3(0f, 0.01f, 0f), Quaternion.identity, new Vector3(2.30f, 0.02f, 1.85f), mats.SedanTrimDark);

            // Weight bench
            CreateStaticPart("BenchPad", PrimitiveType.Cube, gym.transform,
                new Vector3(-0.25f, 0.44f, 0f), Quaternion.identity, new Vector3(1.24f, 0.09f, 0.36f), mats.PedShirtRed);
            CreateStaticPart("BenchFrame", PrimitiveType.Cube, gym.transform,
                new Vector3(-0.25f, 0.21f, 0f), Quaternion.identity, new Vector3(1.02f, 0.38f, 0.22f), mats.SedanTrimDark);

            // Barbell rack + chrome bar + plates
            CreateStaticPart("RackPostL", PrimitiveType.Cube, gym.transform,
                new Vector3(-0.76f, 0.62f, -0.44f), Quaternion.identity, new Vector3(0.06f, 1.22f, 0.06f), mats.SedanTrimDark);
            CreateStaticPart("RackPostR", PrimitiveType.Cube, gym.transform,
                new Vector3(-0.76f, 0.62f, 0.44f), Quaternion.identity, new Vector3(0.06f, 1.22f, 0.06f), mats.SedanTrimDark);
            CreateStaticPart("BarbellShaft", PrimitiveType.Cylinder, gym.transform,
                new Vector3(-0.76f, 1.12f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.032f, 0.84f, 0.032f), mats.SedanChrome);
            CreateStaticPart("PlateL", PrimitiveType.Cylinder, gym.transform,
                new Vector3(-0.76f, 1.12f, -0.68f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.36f, 0.04f, 0.36f), mats.SedanTrimDark);
            CreateStaticPart("PlateR", PrimitiveType.Cylinder, gym.transform,
                new Vector3(-0.76f, 1.12f, 0.68f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.36f, 0.04f, 0.36f), mats.SedanTrimDark);

            BoxCollider col = gym.AddComponent<BoxCollider>();
            col.center = new Vector3(-0.35f, 0.46f, 0f);
            col.size = new Vector3(1.45f, 0.92f, 1.15f);
            MarkStaticEnvironment(gym);
        }

        private static void CreateWallPainting(
            string name,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Material artMat,
            Material frameMat)
        {
            GameObject art = new GameObject(name);
            art.transform.SetParent(parent, false);
            art.transform.localPosition = localPos;
            art.transform.localRotation = localRot;

            CreateStaticPart("Frame", PrimitiveType.Cube, art.transform,
                Vector3.zero, Quaternion.identity, new Vector3(1.16f, 0.78f, 0.04f), frameMat);
            CreateStaticPart("Artwork", PrimitiveType.Cube, art.transform,
                new Vector3(0f, 0f, 0.022f), Quaternion.identity, new Vector3(1.04f, 0.66f, 0.015f), artMat);
        }

        // =====================================================================
        // FURNITURE & PROP BUILDERS
        // =====================================================================

        private static void CreateSofa3Seat(
            string seatIdPrefix,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Material fabricMat,
            Material woodMat)
        {
            GameObject sofa = new GameObject("Sofa_3Seat");
            sofa.transform.SetParent(parent, false);
            sofa.transform.localPosition = localPos;
            sofa.transform.localRotation = localRot;

            CreateStaticPart("BaseFrame", PrimitiveType.Cube, sofa.transform,
                new Vector3(0f, 0.16f, -0.02f), Quaternion.identity, new Vector3(2.08f, 0.24f, 0.80f), woodMat);

            float[] cushionXs = { -0.62f, 0f, 0.62f };
            for (int i = 0; i < 3; i++)
            {
                CreateStaticPart($"SeatCushion_{i}", PrimitiveType.Cube, sofa.transform,
                    new Vector3(cushionXs[i], 0.365f, 0.03f), Quaternion.identity, new Vector3(0.60f, 0.17f, 0.72f), fabricMat);
                CreateStaticPart($"BackCushion_{i}", PrimitiveType.Cube, sofa.transform,
                    new Vector3(cushionXs[i], 0.66f, -0.28f), Quaternion.Euler(-10f, 0f, 0f), new Vector3(0.59f, 0.42f, 0.18f), fabricMat);
            }

            CreateStaticPart("BackFrame", PrimitiveType.Cube, sofa.transform,
                new Vector3(0f, 0.50f, -0.38f), Quaternion.identity, new Vector3(2.10f, 0.78f, 0.10f), fabricMat);
            CreateStaticPart("ArmL", PrimitiveType.Cube, sofa.transform,
                new Vector3(-0.98f, 0.44f, -0.01f), Quaternion.identity, new Vector3(0.14f, 0.44f, 0.84f), fabricMat);
            CreateStaticPart("ArmR", PrimitiveType.Cube, sofa.transform,
                new Vector3(0.98f, 0.44f, -0.01f), Quaternion.identity, new Vector3(0.14f, 0.44f, 0.84f), fabricMat);

            BoxCollider col = sofa.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.44f, -0.02f);
            col.size = new Vector3(2.12f, 0.88f, 0.86f);
            MarkStaticEnvironment(sofa);

            for (int i = 0; i < 3; i++)
            {
                CreateSeatSlot($"SofaSlot_{i + 1}", $"{seatIdPrefix}_{i + 1}", sofa.transform,
                    new Vector3(cushionXs[i], 0.45f, 0.08f), Quaternion.identity, 1.4f);
            }
        }

        private static void CreateArmchair(
            string seatId,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Material fabricMat,
            Material woodMat)
        {
            GameObject chair = new GameObject("Armchair");
            chair.transform.SetParent(parent, false);
            chair.transform.localPosition = localPos;
            chair.transform.localRotation = localRot;

            CreateStaticPart("Base", PrimitiveType.Cube, chair.transform,
                new Vector3(0f, 0.16f, -0.02f), Quaternion.identity, new Vector3(0.84f, 0.24f, 0.78f), woodMat);
            CreateStaticPart("SeatCushion", PrimitiveType.Cube, chair.transform,
                new Vector3(0f, 0.365f, 0.03f), Quaternion.identity, new Vector3(0.62f, 0.17f, 0.68f), fabricMat);
            CreateStaticPart("BackCushion", PrimitiveType.Cube, chair.transform,
                new Vector3(0f, 0.65f, -0.26f), Quaternion.Euler(-10f, 0f, 0f), new Vector3(0.62f, 0.42f, 0.18f), fabricMat);
            CreateStaticPart("BackFrame", PrimitiveType.Cube, chair.transform,
                new Vector3(0f, 0.50f, -0.36f), Quaternion.identity, new Vector3(0.86f, 0.76f, 0.10f), fabricMat);
            CreateStaticPart("ArmL", PrimitiveType.Cube, chair.transform,
                new Vector3(-0.37f, 0.44f, -0.01f), Quaternion.identity, new Vector3(0.13f, 0.42f, 0.80f), fabricMat);
            CreateStaticPart("ArmR", PrimitiveType.Cube, chair.transform,
                new Vector3(0.37f, 0.44f, -0.01f), Quaternion.identity, new Vector3(0.13f, 0.42f, 0.80f), fabricMat);

            BoxCollider col = chair.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.44f, -0.02f);
            col.size = new Vector3(0.88f, 0.88f, 0.82f);
            MarkStaticEnvironment(chair);

            CreateSeatSlot("ChairSlot", seatId, chair.transform,
                new Vector3(0f, 0.45f, 0.06f), Quaternion.identity, 1.4f);
        }

        private static void CreateCoffeeTable(
            string name,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Material woodMat)
        {
            GameObject tbl = new GameObject(name);
            tbl.transform.SetParent(parent, false);
            tbl.transform.localPosition = localPos;
            tbl.transform.localRotation = localRot;

            CreateStaticPart("Top", PrimitiveType.Cube, tbl.transform,
                new Vector3(0f, 0.40f, 0f), Quaternion.identity, new Vector3(1.18f, 0.05f, 0.60f), woodMat);
            CreateStaticPart("Shelf", PrimitiveType.Cube, tbl.transform,
                new Vector3(0f, 0.15f, 0f), Quaternion.identity, new Vector3(1.04f, 0.03f, 0.48f), woodMat);

            Vector2[] legs = { new Vector2(-0.52f, -0.24f), new Vector2(0.52f, -0.24f), new Vector2(-0.52f, 0.24f), new Vector2(0.52f, 0.24f) };
            for (int i = 0; i < 4; i++)
            {
                CreateStaticPart($"Leg_{i}", PrimitiveType.Cube, tbl.transform,
                    new Vector3(legs[i].x, 0.20f, legs[i].y), Quaternion.identity, new Vector3(0.05f, 0.40f, 0.05f), woodMat);
            }

            BoxCollider col = tbl.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.21f, 0f);
            col.size = new Vector3(1.18f, 0.42f, 0.60f);
            MarkStaticEnvironment(tbl);
        }

        private static void CreateRug(
            string name,
            Transform parent,
            Vector3 localPos,
            Vector2 sizeXZ,
            Material rugMat,
            Material borderMat)
        {
            GameObject rug = new GameObject(name);
            rug.transform.SetParent(parent, false);
            rug.transform.localPosition = localPos;
            rug.transform.localRotation = Quaternion.identity;

            CreateStaticPart("RugBorder", PrimitiveType.Cube, rug.transform,
                new Vector3(0f, 0.004f, 0f), Quaternion.identity, new Vector3(sizeXZ.x, 0.008f, sizeXZ.y), borderMat);
            CreateStaticPart("RugCenter", PrimitiveType.Cube, rug.transform,
                new Vector3(0f, 0.009f, 0f), Quaternion.identity, new Vector3(sizeXZ.x - 0.18f, 0.008f, sizeXZ.y - 0.18f), rugMat);
            MarkStaticEnvironment(rug);
        }

        private static void CreateTvOnCabinet(
            string name,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            MaterialSet mats)
        {
            GameObject unit = new GameObject(name);
            unit.transform.SetParent(parent, false);
            unit.transform.localPosition = localPos;
            unit.transform.localRotation = localRot;

            CreateStaticPart("CabinetBody", PrimitiveType.Cube, unit.transform,
                new Vector3(0f, 0.26f, 0f), Quaternion.identity, new Vector3(1.46f, 0.46f, 0.42f), mats.TownFurnitureWood);
            CreateStaticPart("TvStandNeck", PrimitiveType.Cube, unit.transform,
                new Vector3(0f, 0.54f, -0.04f), Quaternion.identity, new Vector3(0.26f, 0.12f, 0.14f), mats.SedanTrimDark);
            CreateStaticPart("TvBezel", PrimitiveType.Cube, unit.transform,
                new Vector3(0f, 0.90f, -0.03f), Quaternion.identity, new Vector3(1.22f, 0.70f, 0.05f), mats.SedanTrimDark);
            CreateStaticPart("TvScreen", PrimitiveType.Cube, unit.transform,
                new Vector3(0f, 0.90f, 0.00f), Quaternion.identity, new Vector3(1.16f, 0.64f, 0.02f), mats.SedanGlass);

            BoxCollider col = unit.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.62f, 0f);
            col.size = new Vector3(1.46f, 1.24f, 0.44f);
            MarkStaticEnvironment(unit);
        }

        private static void CreateFloorLamp(
            string name,
            Transform parent,
            Vector3 localPos,
            MaterialSet mats)
        {
            GameObject lamp = new GameObject(name);
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = localPos;
            lamp.transform.localRotation = Quaternion.identity;

            CreateStaticPart("Base", PrimitiveType.Cylinder, lamp.transform,
                new Vector3(0f, 0.025f, 0f), Quaternion.identity, new Vector3(0.32f, 0.025f, 0.32f), mats.SedanTrimDark);
            CreateStaticPart("Stem", PrimitiveType.Cylinder, lamp.transform,
                new Vector3(0f, 0.82f, 0f), Quaternion.identity, new Vector3(0.04f, 0.80f, 0.04f), mats.SedanTrimDark);
            GameObject shade = CreateStaticPart("Shade", PrimitiveType.Cylinder, lamp.transform,
                new Vector3(0f, 1.56f, 0f), Quaternion.identity, new Vector3(0.38f, 0.16f, 0.38f), mats.WindowLitWarm);

            CapsuleCollider col = lamp.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.86f, 0f);
            col.radius = 0.18f;
            col.height = 1.72f;
            MarkStaticEnvironment(lamp);
            AttachLampShadeNightLight(shade);
        }

        private static void CreateKitchenCounter(
            string name,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            MaterialSet mats)
        {
            GameObject cnt = new GameObject(name);
            cnt.transform.SetParent(parent, false);
            cnt.transform.localPosition = localPos;
            cnt.transform.localRotation = localRot;

            CreateStaticPart("CabinetBase", PrimitiveType.Cube, cnt.transform,
                new Vector3(0f, 0.43f, 0f), Quaternion.identity, new Vector3(2.16f, 0.86f, 0.60f), mats.TownFurnitureWood);
            CreateStaticPart("Worktop", PrimitiveType.Cube, cnt.transform,
                new Vector3(0f, 0.885f, 0.01f), Quaternion.identity, new Vector3(2.22f, 0.05f, 0.64f), mats.PlasterTrim);
            CreateStaticPart("SinkBasin", PrimitiveType.Cube, cnt.transform,
                new Vector3(-0.52f, 0.91f, 0.02f), Quaternion.identity, new Vector3(0.56f, 0.02f, 0.40f), mats.SedanChrome);
            CreateStaticPart("Faucet", PrimitiveType.Cube, cnt.transform,
                new Vector3(-0.52f, 1.02f, -0.18f), Quaternion.identity, new Vector3(0.05f, 0.22f, 0.14f), mats.SedanChrome);
            CreateStaticPart("Cooktop", PrimitiveType.Cube, cnt.transform,
                new Vector3(0.52f, 0.915f, 0.02f), Quaternion.identity, new Vector3(0.62f, 0.02f, 0.44f), mats.SedanTrimDark);

            BoxCollider col = cnt.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.46f, 0f);
            col.size = new Vector3(2.22f, 0.92f, 0.64f);
            MarkStaticEnvironment(cnt);
        }

        private static void CreateFridge(
            string name,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            MaterialSet mats)
        {
            GameObject fr = new GameObject(name);
            fr.transform.SetParent(parent, false);
            fr.transform.localPosition = localPos;
            fr.transform.localRotation = localRot;

            CreateStaticPart("FridgeBody", PrimitiveType.Cube, fr.transform,
                new Vector3(0f, 0.91f, 0f), Quaternion.identity, new Vector3(0.74f, 1.82f, 0.70f), mats.TownInteriorPaintPearl);
            CreateStaticPart("DoorSplitSeam", PrimitiveType.Cube, fr.transform,
                new Vector3(0f, 1.18f, 0.355f), Quaternion.identity, new Vector3(0.72f, 0.02f, 0.02f), mats.SedanTrimDark);
            CreateStaticPart("HandleLower", PrimitiveType.Cube, fr.transform,
                new Vector3(-0.28f, 0.86f, 0.37f), Quaternion.identity, new Vector3(0.03f, 0.42f, 0.04f), mats.SedanChrome);
            CreateStaticPart("HandleUpper", PrimitiveType.Cube, fr.transform,
                new Vector3(-0.28f, 1.46f, 0.37f), Quaternion.identity, new Vector3(0.03f, 0.34f, 0.04f), mats.SedanChrome);

            BoxCollider col = fr.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.91f, 0f);
            col.size = new Vector3(0.74f, 1.82f, 0.72f);
            MarkStaticEnvironment(fr);
        }

        private static void CreateDiningTableWith4Chairs(
            string seatIdPrefix,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Material woodMat,
            Material cushionMat)
        {
            GameObject setRoot = new GameObject("DiningSet");
            setRoot.transform.SetParent(parent, false);
            setRoot.transform.localPosition = localPos;
            setRoot.transform.localRotation = localRot;

            GameObject table = new GameObject("DiningTable");
            table.transform.SetParent(setRoot.transform, false);
            CreateStaticPart("TableTop", PrimitiveType.Cube, table.transform,
                new Vector3(0f, 0.735f, 0f), Quaternion.identity, new Vector3(1.36f, 0.05f, 0.86f), woodMat);

            Vector2[] tLegs = { new Vector2(-0.60f, -0.36f), new Vector2(0.60f, -0.36f), new Vector2(-0.60f, 0.36f), new Vector2(0.60f, 0.36f) };
            for (int i = 0; i < 4; i++)
            {
                CreateStaticPart($"Leg_{i}", PrimitiveType.Cube, table.transform,
                    new Vector3(tLegs[i].x, 0.36f, tLegs[i].y), Quaternion.identity, new Vector3(0.06f, 0.72f, 0.06f), woodMat);
            }

            BoxCollider tCol = table.AddComponent<BoxCollider>();
            tCol.center = new Vector3(0f, 0.38f, 0f);
            tCol.size = new Vector3(1.36f, 0.76f, 0.86f);
            MarkStaticEnvironment(table);

            (Vector3 pos, float yaw)[] chairSpots =
            {
                (new Vector3(0f, 0f, -0.66f), 0f),
                (new Vector3(0f, 0f, 0.66f), 180f),
                (new Vector3(-0.90f, 0f, 0f), 90f),
                (new Vector3(0.90f, 0f, 0f), -90f)
            };

            for (int i = 0; i < chairSpots.Length; i++)
            {
                CreateDiningChair(
                    $"DiningChair_{i + 1}",
                    $"{seatIdPrefix}_chair_{i + 1}",
                    setRoot.transform,
                    chairSpots[i].pos,
                    Quaternion.Euler(0f, chairSpots[i].yaw, 0f),
                    woodMat,
                    cushionMat);
            }
        }

        private static void CreateDiningChair(
            string name,
            string seatId,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Material woodMat,
            Material cushionMat)
        {
            GameObject ch = new GameObject(name);
            ch.transform.SetParent(parent, false);
            ch.transform.localPosition = localPos;
            ch.transform.localRotation = localRot;

            CreateStaticPart("SeatBase", PrimitiveType.Cube, ch.transform,
                new Vector3(0f, 0.415f, 0f), Quaternion.identity, new Vector3(0.45f, 0.04f, 0.45f), woodMat);
            CreateStaticPart("SeatPad", PrimitiveType.Cube, ch.transform,
                new Vector3(0f, 0.44f, 0.01f), Quaternion.identity, new Vector3(0.42f, 0.02f, 0.41f), cushionMat);
            CreateStaticPart("Backrest", PrimitiveType.Cube, ch.transform,
                new Vector3(0f, 0.69f, -0.20f), Quaternion.Euler(-5f, 0f, 0f), new Vector3(0.44f, 0.46f, 0.04f), woodMat);

            Vector2[] cLegs = { new Vector2(-0.19f, -0.19f), new Vector2(0.19f, -0.19f), new Vector2(-0.19f, 0.19f), new Vector2(0.19f, 0.19f) };
            for (int i = 0; i < 4; i++)
            {
                CreateStaticPart($"Leg_{i}", PrimitiveType.Cube, ch.transform,
                    new Vector3(cLegs[i].x, 0.205f, cLegs[i].y), Quaternion.identity, new Vector3(0.04f, 0.41f, 0.04f), woodMat);
            }

            BoxCollider col = ch.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.45f, -0.01f);
            col.size = new Vector3(0.46f, 0.90f, 0.48f);
            MarkStaticEnvironment(ch);

            CreateSeatSlot("ChairSitSlot", seatId, ch.transform,
                new Vector3(0f, 0.45f, 0.04f), Quaternion.identity, 1.4f);
        }

        private static void CreateDoubleBed(
            string name,
            string bedTriggerId,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Material blanketMat,
            MaterialSet mats)
        {
            GameObject bed = new GameObject(name);
            bed.transform.SetParent(parent, false);
            bed.transform.localPosition = localPos;
            bed.transform.localRotation = localRot;

            CreateStaticPart("BedFrame", PrimitiveType.Cube, bed.transform,
                new Vector3(0f, 0.16f, 0f), Quaternion.identity, new Vector3(1.62f, 0.26f, 2.06f), mats.TownFurnitureWood);
            CreateStaticPart("Headboard", PrimitiveType.Cube, bed.transform,
                new Vector3(0f, 0.56f, -0.99f), Quaternion.identity, new Vector3(1.66f, 1.06f, 0.08f), mats.TownFurnitureWood);
            CreateStaticPart("Mattress", PrimitiveType.Cube, bed.transform,
                new Vector3(0f, 0.38f, 0.02f), Quaternion.identity, new Vector3(1.52f, 0.22f, 1.94f), mats.TownInteriorPaintWarm);
            CreateStaticPart("Blanket", PrimitiveType.Cube, bed.transform,
                new Vector3(0f, 0.40f, 0.28f), Quaternion.identity, new Vector3(1.56f, 0.23f, 1.38f), blanketMat);
            CreateStaticPart("PillowL", PrimitiveType.Cube, bed.transform,
                new Vector3(-0.37f, 0.53f, -0.68f), Quaternion.Euler(8f, 0f, 0f), new Vector3(0.56f, 0.12f, 0.36f), mats.TownInteriorPaintWarm);
            CreateStaticPart("PillowR", PrimitiveType.Cube, bed.transform,
                new Vector3(0.37f, 0.53f, -0.68f), Quaternion.Euler(8f, 0f, 0f), new Vector3(0.56f, 0.12f, 0.36f), mats.TownInteriorPaintWarm);

            BoxCollider col = bed.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.32f, 0f);
            col.size = new Vector3(1.64f, 0.64f, 2.06f);
            MarkStaticEnvironment(bed);

            // E4: Sleepable Bed trigger (8 hours advance + 100 energy + save)
            GameObject sleepTrigGo = new GameObject("BedSleepTrigger");
            sleepTrigGo.transform.SetParent(bed.transform, false);
            sleepTrigGo.transform.localPosition = new Vector3(0f, 0.55f, 0.15f);
            InteractableTrigger sleepTrig = sleepTrigGo.AddComponent<InteractableTrigger>();
            sleepTrig.Configure(InteractableKind.Bed, bedTriggerId, "ui.btn.sleep", 2.15f);
        }

        private static void CreateWardrobe(
            string name,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            MaterialSet mats)
        {
            GameObject wd = new GameObject(name);
            wd.transform.SetParent(parent, false);
            wd.transform.localPosition = localPos;
            wd.transform.localRotation = localRot;

            CreateStaticPart("Cabinet", PrimitiveType.Cube, wd.transform,
                new Vector3(0f, 1.02f, 0f), Quaternion.identity, new Vector3(1.26f, 2.04f, 0.58f), mats.TownFurnitureWood);
            CreateStaticPart("CenterSeam", PrimitiveType.Cube, wd.transform,
                new Vector3(0f, 1.02f, 0.295f), Quaternion.identity, new Vector3(0.02f, 1.92f, 0.02f), mats.SedanTrimDark);
            CreateStaticPart("HandleL", PrimitiveType.Cube, wd.transform,
                new Vector3(-0.08f, 1.05f, 0.31f), Quaternion.identity, new Vector3(0.025f, 0.24f, 0.03f), mats.SedanChrome);
            CreateStaticPart("HandleR", PrimitiveType.Cube, wd.transform,
                new Vector3(0.08f, 1.05f, 0.31f), Quaternion.identity, new Vector3(0.025f, 0.24f, 0.03f), mats.SedanChrome);

            BoxCollider col = wd.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.02f, 0f);
            col.size = new Vector3(1.26f, 2.04f, 0.60f);
            MarkStaticEnvironment(wd);
        }

        private static void CreateNightstand(
            string name,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            MaterialSet mats)
        {
            GameObject ns = new GameObject(name);
            ns.transform.SetParent(parent, false);
            ns.transform.localPosition = localPos;
            ns.transform.localRotation = localRot;

            CreateStaticPart("Body", PrimitiveType.Cube, ns.transform,
                new Vector3(0f, 0.27f, 0f), Quaternion.identity, new Vector3(0.48f, 0.54f, 0.44f), mats.TownFurnitureWood);
            CreateStaticPart("DrawerKnob", PrimitiveType.Cube, ns.transform,
                new Vector3(0f, 0.38f, 0.23f), Quaternion.identity, new Vector3(0.10f, 0.025f, 0.03f), mats.SedanChrome);
            CreateStaticPart("TableLampBase", PrimitiveType.Cylinder, ns.transform,
                new Vector3(0f, 0.60f, 0f), Quaternion.identity, new Vector3(0.12f, 0.06f, 0.12f), mats.SedanTrimDark);
            GameObject tableShade = CreateStaticPart("TableLampShade", PrimitiveType.Cylinder, ns.transform,
                new Vector3(0f, 0.73f, 0f), Quaternion.identity, new Vector3(0.22f, 0.10f, 0.22f), mats.WindowLitWarm);

            BoxCollider col = ns.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.28f, 0f);
            col.size = new Vector3(0.48f, 0.56f, 0.44f);
            MarkStaticEnvironment(ns);
            AttachLampShadeNightLight(tableShade);
        }

        private static void CreatePaintBucketProp(
            string houseId,
            Transform houseRoot,
            Vector3 localPos,
            MaterialSet mats,
            Material paintRimMat)
        {
            GameObject prop = new GameObject("PaintBucket_Prop");
            prop.transform.SetParent(houseRoot, false);
            prop.transform.localPosition = localPos;
            prop.transform.localRotation = Quaternion.identity;

            CreateStaticPart("CanCylinder", PrimitiveType.Cylinder, prop.transform,
                new Vector3(0f, 0.17f, 0f), Quaternion.identity, new Vector3(0.32f, 0.17f, 0.32f), mats.TownPaintCan);

            CreateStaticPart("PaintSurface", PrimitiveType.Cylinder, prop.transform,
                new Vector3(0f, 0.335f, 0f), Quaternion.identity, new Vector3(0.27f, 0.015f, 0.27f), paintRimMat);

            CreateStaticPart("HandlePostL", PrimitiveType.Cube, prop.transform,
                new Vector3(0f, 0.39f, -0.155f), Quaternion.Euler(14f, 0f, 0f), new Vector3(0.016f, 0.14f, 0.016f), mats.SedanChrome);
            CreateStaticPart("HandlePostR", PrimitiveType.Cube, prop.transform,
                new Vector3(0f, 0.39f, 0.155f), Quaternion.Euler(-14f, 0f, 0f), new Vector3(0.016f, 0.14f, 0.016f), mats.SedanChrome);
            CreateStaticPart("HandleGrip", PrimitiveType.Cube, prop.transform,
                new Vector3(0f, 0.455f, 0f), Quaternion.identity, new Vector3(0.03f, 0.03f, 0.26f), mats.TownFurnitureWood);

            BoxCollider col = prop.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.19f, 0f);
            col.size = new Vector3(0.36f, 0.38f, 0.36f);
            MarkStaticEnvironment(prop);

            GameObject trigObj = new GameObject("PaintTrigger");
            trigObj.transform.SetParent(prop.transform, false);
            trigObj.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            InteractableTrigger trig = trigObj.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.PaintableHouse, $"{houseId}_paint", "ui.btn.paint", 1.8f);
        }

        private static void BuildWorldBoundary(Transform envParent, MaterialSet mats)
        {
            GameObject boundaryRoot = new GameObject("World_CountryFencePerimeter");
            boundaryRoot.transform.SetParent(envParent, false);

            // Clean wooden & stone perimeter enclosing the full 2-continent Istanbul map (Avrupa -102..+78, Boğaz +78..+148, Anadolu +148..+216)
            const float minX = -102f;
            const float maxX = 216f;
            const float minZ = -44f;
            const float maxZ = 206f;

            BuildWoodenFenceRun("Fence_West", boundaryRoot.transform, new Vector3(minX, 0f, minZ), new Vector3(minX, 0f, maxZ), mats);
            BuildWoodenFenceRun("Fence_East_Anadolu", boundaryRoot.transform, new Vector3(maxX, 0f, minZ), new Vector3(maxX, 0f, maxZ), mats);
            BuildWoodenFenceRun("Fence_South_Avrupa", boundaryRoot.transform, new Vector3(minX, 0f, minZ), new Vector3(77f, 0f, minZ), mats);
            BuildWoodenFenceRun("Fence_South_Anadolu", boundaryRoot.transform, new Vector3(149f, 0f, minZ), new Vector3(maxX, 0f, minZ), mats);
            BuildWoodenFenceRun("Fence_North_Avrupa", boundaryRoot.transform, new Vector3(minX, 0f, maxZ), new Vector3(77f, 0f, maxZ), mats);
            BuildWoodenFenceRun("Fence_North_Anadolu", boundaryRoot.transform, new Vector3(149f, 0f, maxZ), new Vector3(maxX, 0f, maxZ), mats);

            // Natural grove trees along the outside of Avrupa & Anadolu perimeters (skipping the open Bosphorus Strait x=78..148!)
            int treeIdx = 0;
            for (float z = minZ - 4f; z <= maxZ + 4f; z += 26f)
            {
                CreateTownTree($"PerimeterTree_W_{treeIdx}", boundaryRoot.transform,
                    new Vector3(minX - 6.5f - (treeIdx % 3) * 2.5f, 0f, z), mats, 1.05f + (treeIdx % 3) * 0.15f, treeIdx + 60);
                CreateTownTree($"PerimeterTree_E_{treeIdx}", boundaryRoot.transform,
                    new Vector3(maxX + 6.5f + ((treeIdx + 1) % 3) * 2.5f, 0f, z + 8f), mats, 1.05f + ((treeIdx + 1) % 3) * 0.15f, treeIdx + 90);
                treeIdx++;
            }
            for (float x = minX + 14f; x <= maxX - 14f; x += 28f)
            {
                if (x > 70f && x < 156f) continue; // Keep the Bosphorus Strait open water!
                CreateTownTree($"PerimeterTree_S_{treeIdx}", boundaryRoot.transform,
                    new Vector3(x, 0f, minZ - 6.5f - (treeIdx % 2) * 3f), mats, 1.05f + (treeIdx % 3) * 0.14f, treeIdx + 120);
                CreateTownTree($"PerimeterTree_N_{treeIdx}", boundaryRoot.transform,
                    new Vector3(x + 6f, 0f, maxZ + 6.5f + ((treeIdx + 1) % 2) * 3f), mats, 1.05f + ((treeIdx + 1) % 3) * 0.14f, treeIdx + 150);
                treeIdx++;
            }
        }

        private static void BuildWoodenFenceRun(
            string name,
            Transform parent,
            Vector3 startPos,
            Vector3 endPos,
            MaterialSet mats)
        {
            GameObject run = new GameObject(name);
            run.transform.SetParent(parent, false);

            Vector3 delta = endPos - startPos;
            float totalLen = delta.magnitude;
            Vector3 dir = delta / Mathf.Max(0.01f, totalLen);
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            Vector3 mid = (startPos + endPos) * 0.5f;

            // Single clean BoxCollider along the fence run so cars and players never leave the town bounds
            GameObject colGo = new GameObject("FenceCollider");
            colGo.transform.SetParent(run.transform, false);
            colGo.transform.SetPositionAndRotation(mid + Vector3.up * 0.75f, rot);
            BoxCollider bc = colGo.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.35f, 1.50f, totalLen);

            // Top and bottom horizontal wooden rails
            CreateTiledBox("Rail_Upper", run.transform, mid + Vector3.up * 0.96f, new Vector3(0.09f, 0.11f, totalLen), rot, mats.TownFurnitureWood, 2.0f, collider: false);
            CreateTiledBox("Rail_Lower", run.transform, mid + Vector3.up * 0.48f, new Vector3(0.09f, 0.11f, totalLen), rot, mats.TownFurnitureWood, 2.0f, collider: false);

            // Wooden posts every 4.5 meters
            int postCount = Mathf.Max(2, Mathf.RoundToInt(totalLen / 4.5f));
            for (int i = 0; i <= postCount; i++)
            {
                Vector3 p = Vector3.Lerp(startPos, endPos, i / (float)postCount);
                CreateTiledBox($"Post_{i}", run.transform, p + Vector3.up * 0.58f, new Vector3(0.16f, 1.16f, 0.16f), rot, mats.TownWood, 1.5f, collider: false);
            }

            MarkStaticEnvironment(run);
        }
    }
}
