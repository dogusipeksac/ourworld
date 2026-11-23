using System.IO;
using OurWorld.Core;
using OurWorld.Jobs;
using OurWorld.Player;
using OurWorld.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace OurWorld.Editor
{
    public static partial class OurWorldPrototypeSetup
    {
        private const string GeneratedArtDir = ArtDir + "/Generated";

        // Calm, modern UI palette (#12161C dark translucent, #4FB3A9 warm teal accent, #EDEFF2 off-white)
        private static readonly Color UiPanelSoft = new Color(0.071f, 0.086f, 0.110f, 0.64f);
        private static readonly Color UiPanelModal = new Color(0.071f, 0.086f, 0.110f, 0.88f);
        private static readonly Color UiButtonDark = new Color(0.071f, 0.086f, 0.110f, 0.68f);
        private static readonly Color UiAccentTeal = new Color(0.310f, 0.702f, 0.663f, 0.90f);
        private static readonly Color UiOffWhite = new Color(0.929f, 0.937f, 0.949f, 1.00f);
        private static readonly Color UiMutedText = new Color(0.651f, 0.690f, 0.741f, 1.00f);
        private static readonly Color UiDangerRed = new Color(0.851f, 0.325f, 0.310f, 0.92f);

        private class GeneratedUiSprites
        {
            public Sprite RoundedRect;
            public Sprite Circle;
            public Sprite Ring;
        }

        private static GeneratedUiSprites GenerateUiSprites()
        {
            if (!Directory.Exists(GeneratedArtDir))
            {
                Directory.CreateDirectory(GeneratedArtDir);
            }

            string roundedPath = $"{GeneratedArtDir}/UI_RoundedRect.png";
            string circlePath = $"{GeneratedArtDir}/UI_Circle.png";
            string ringPath = $"{GeneratedArtDir}/UI_Ring.png";

            WriteRoundedRectPng(roundedPath, 128, 24f);
            WriteCirclePng(circlePath, 128, 61f);
            WriteRingPng(ringPath, 256, 123f, 105f);

            AssetDatabase.Refresh();

            ConfigureGeneratedSpriteImporter(roundedPath, new Vector4(26f, 26f, 26f, 26f));
            ConfigureGeneratedSpriteImporter(circlePath, Vector4.zero);
            ConfigureGeneratedSpriteImporter(ringPath, Vector4.zero);

            return new GeneratedUiSprites
            {
                RoundedRect = AssetDatabase.LoadAssetAtPath<Sprite>(roundedPath),
                Circle = AssetDatabase.LoadAssetAtPath<Sprite>(circlePath),
                Ring = AssetDatabase.LoadAssetAtPath<Sprite>(ringPath)
            };
        }

        private static void WriteRoundedRectPng(string path, int size, float radius)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;
            float innerHalf = half - radius - 1.5f;

            for (int y = 0; y < size; y++)
            {
                float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - half) - innerHalf);
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - half) - innerHalf);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float alpha = Mathf.Clamp01(0.5f - dist / 1.4f);
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void WriteCirclePng(string path, int size, float radius)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            float center = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                float dy = (y + 0.5f) - center;
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float alpha = Mathf.Clamp01(0.5f - dist / 1.5f);
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void WriteRingPng(string path, int size, float outerRadius, float innerRadius)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            float center = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                float dy = (y + 0.5f) - center;
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) - center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float outerA = Mathf.Clamp01(0.5f - (r - outerRadius) / 1.5f);
                    float innerA = Mathf.Clamp01(0.5f + (r - innerRadius) / 1.5f);
                    float alpha = outerA * innerA;
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void ConfigureGeneratedSpriteImporter(string assetPath, Vector4 border)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// E5: Renders the entire 3D town from a top-down orthographic camera at edit time into
        /// Assets/GRIDROAM/Art/Generated/TownMap.png (covering [-200..180] x [-65..315], 380m x 380m square).
        /// </summary>
        private static Texture2D RenderAndSaveTownMapTexture(Light sunLight)
        {
            if (!Directory.Exists(GeneratedArtDir))
            {
                Directory.CreateDirectory(GeneratedArtDir);
            }

            string mapPath = $"{GeneratedArtDir}/TownMap.png";
            const int mapRes = 1024;

            bool prevFog = RenderSettings.fog;
            Quaternion prevSunRot = sunLight != null ? sunLight.transform.rotation : Quaternion.identity;
            float prevSunInt = sunLight != null ? sunLight.intensity : 1.8f;
            Color prevSunCol = sunLight != null ? sunLight.color : Color.white;

            RenderSettings.fog = false;
            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(74f, -32f, 0f);
                sunLight.intensity = 1.95f;
                sunLight.color = new Color(1.0f, 0.97f, 0.92f);
            }

            GameObject camGo = new GameObject("Temp_TownMapOrthoCamera");
            Camera orthoCam = camGo.AddComponent<Camera>();
            UniversalAdditionalCameraData uacd = camGo.AddComponent<UniversalAdditionalCameraData>();
            uacd.renderPostProcessing = false;
            uacd.renderShadows = false;

            float centerX = (MinimapController.MapWorldMinX + MinimapController.MapWorldMaxX) * 0.5f; // -10
            float centerZ = (MinimapController.MapWorldMinZ + MinimapController.MapWorldMaxZ) * 0.5f; // +125

            camGo.transform.position = new Vector3(centerX, 240f, centerZ);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            orthoCam.orthographic = true;
            orthoCam.orthographicSize = MinimapController.MapWorldSpan * 0.5f; // 190 m
            orthoCam.aspect = 1.0f;
            orthoCam.nearClipPlane = 1.0f;
            orthoCam.farClipPlane = 300f;
            orthoCam.clearFlags = CameraClearFlags.SolidColor;
            orthoCam.backgroundColor = new Color(0.20f, 0.29f, 0.18f, 1f);

            RenderTexture rt = RenderTexture.GetTemporary(mapRes, mapRes, 24, RenderTextureFormat.ARGB32);
            orthoCam.targetTexture = rt;
            orthoCam.Render();

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D tex = new Texture2D(mapRes, mapRes, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, mapRes, mapRes), 0, 0);
            tex.Apply();

            RenderTexture.active = prevActive;
            orthoCam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(camGo);

            RenderSettings.fog = prevFog;
            if (sunLight != null)
            {
                sunLight.transform.rotation = prevSunRot;
                sunLight.intensity = prevSunInt;
                sunLight.color = prevSunCol;
            }

            File.WriteAllBytes(mapPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(mapPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(mapPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(mapPath);
        }

        private static void BuildHudCanvas(
            GeneratedUiSprites uiSprites,
            Texture2D townMapTexture,
            PlayerNeedsController needsController)
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
            canvasObj.AddComponent<ResponsiveTabletLayout>();
            Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Root constrained to iPhone Notch / Android Safe Area
            GameObject safeRoot = CreateUiRect("SafeAreaRoot", canvasObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeRoot.AddComponent<SafeAreaFitter>();

            // Right-half transparent Camera Orbit Touch Zone (placed first so interactive buttons sit in front)
            GameObject camPad = CreateUiRect("CameraTouchZone", safeRoot.transform, new Vector2(0.42f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            Image camPadImg = camPad.AddComponent<Image>();
            camPadImg.color = new Color(0f, 0f, 0f, 0f);
            camPad.AddComponent<CameraTouchZone>();

            // =================================================================
            // 1. TOP-LEFT: CIRCULAR MINIMAP + PHONE BUTTON + OBJECTIVE CARD
            // =================================================================
            GameObject minimapFrame = CreateStyledPanel("Minimap_Container", safeRoot.transform, uiSprites.Circle,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -22f), new Vector2(168f, 168f), UiPanelSoft, sliced: false);

            GameObject minimapMaskObj = CreateStyledPanel("Minimap_Mask", minimapFrame.transform, uiSprites.Circle,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(158f, 158f), Color.white, sliced: false);
            Mask mmMask = minimapMaskObj.AddComponent<Mask>();
            mmMask.showMaskGraphic = false;

            GameObject mapRotator = CreateUiRect("MapRotator", minimapMaskObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-122f, -122f), new Vector2(122f, 122f));
            RawImage mmRaw = mapRotator.AddComponent<RawImage>();
            mmRaw.texture = townMapTexture;
            mmRaw.raycastTarget = false;

            GameObject mmPlayerArrow = CreateUiRect("PlayerArrow", minimapMaskObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-14f, -14f), new Vector2(14f, 14f));
            CreateUiText("Glyph", mmPlayerArrow.transform, uiFont, "▲", 20, TextAnchor.MiddleCenter,
                UiOffWhite, Vector2.zero, new Vector2(28f, 28f), FontStyle.Bold);

            GameObject mmWpDot = CreateStyledPanel("WaypointDot", minimapMaskObj.transform, uiSprites.Circle,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(15f, 15f), UiAccentTeal, sliced: false, addShadow: false);
            mmWpDot.GetComponent<Image>().raycastTarget = false;

            GameObject mmRim = CreateStyledPanel("Minimap_RingBorder", minimapFrame.transform, uiSprites.Ring,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(168f, 168f), new Color(0.93f, 0.94f, 0.95f, 0.26f), sliced: false, addShadow: false);
            mmRim.GetComponent<Image>().raycastTarget = false;

            MinimapController minimapCtrl = minimapFrame.AddComponent<MinimapController>();
            minimapCtrl.Configure(
                mapRotator.GetComponent<RectTransform>(),
                mmRaw,
                mmPlayerArrow.GetComponent<RectTransform>(),
                mmWpDot.GetComponent<RectTransform>(),
                mmWpDot.GetComponent<Image>());

            // Phone / Map Button right under Minimap
            var (phoneBtn, phoneLbl) = CreateStyledButton("Btn_Phone", safeRoot.transform, uiSprites.RoundedRect, uiFont, "Rehber (M)",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -206f), new Vector2(188f, 64f), UiButtonDark, 22);

            // Mission & Side-Job Objective Card (top-left beside Minimap, fades to 50% after 10 s unchanged)
            GameObject missionBox = CreateStyledPanel("MissionBox", safeRoot.transform, uiSprites.RoundedRect,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(212f, -22f), new Vector2(540f, 146f), UiPanelSoft);
            CanvasGroup missionBoxGroup = missionBox.AddComponent<CanvasGroup>();

            Text missionTitle = CreateUiText("MissionTitle", missionBox.transform, uiFont, "Anahtarları Al", 26, TextAnchor.UpperLeft,
                UiAccentTeal, new Vector2(20f, -12f), new Vector2(500f, 34f), FontStyle.Bold);
            Text missionObj = CreateUiText("MissionObjective", missionBox.transform, uiFont, "Tamirhaneye yürü ve Marco ile konuş.", 20, TextAnchor.UpperLeft,
                UiOffWhite, new Vector2(20f, -46f), new Vector2(500f, 52f));
            missionObj.verticalOverflow = VerticalWrapMode.Truncate;

            Text jobObjText = CreateUiText("JobObjective", missionBox.transform, uiFont, "", 20, TextAnchor.MiddleLeft,
                new Color(0.95f, 0.84f, 0.48f, 1f), new Vector2(20f, -104f), new Vector2(400f, 30f), FontStyle.Bold);
            jobObjText.verticalOverflow = VerticalWrapMode.Truncate;
            jobObjText.gameObject.SetActive(false);

            var (cancelJobBtn, cancelJobLbl) = CreateStyledButton("Btn_CancelJob", missionBox.transform, uiSprites.RoundedRect, uiFont, "İptal",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-14f, 10f), new Vector2(92f, 34f), new Color(0.32f, 0.16f, 0.16f, 0.85f), 18);
            cancelJobBtn.gameObject.SetActive(false);

            // =================================================================
            // 2. TOP-CENTRE: WAYPOINT COMPASS, TIMER & TAIL WARNING
            // =================================================================
            GameObject waypointBox = CreateStyledPanel("WaypointIndicator", safeRoot.transform, uiSprites.RoundedRect,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(190f, 58f), UiPanelSoft);
            Text arrowText = CreateUiText("Arrow", waypointBox.transform, uiFont, "▲", 26, TextAnchor.MiddleCenter,
                UiAccentTeal, new Vector2(14f, -6f), new Vector2(46f, 46f), FontStyle.Bold);
            Text distText = CreateUiText("Distance", waypointBox.transform, uiFont, "0 m", 24, TextAnchor.MiddleCenter,
                UiOffWhite, new Vector2(60f, -6f), new Vector2(118f, 46f), FontStyle.Bold);
            ObjectiveWaypointIndicator waypointComp = canvasObj.AddComponent<ObjectiveWaypointIndicator>();
            waypointComp.Configure(waypointBox, arrowText.rectTransform, distText);

            GameObject timerBox = CreateStyledPanel("TimerBox", safeRoot.transform, uiSprites.RoundedRect,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -90f), new Vector2(162f, 50f), UiPanelSoft);
            Text timerText = CreateUiText("TimerText", timerBox.transform, uiFont, "01:30", 26, TextAnchor.MiddleCenter,
                UiOffWhite, Vector2.zero, new Vector2(162f, 50f), FontStyle.Bold);

            GameObject tailBox = CreateStyledPanel("TailWarningBox", safeRoot.transform, uiSprites.RoundedRect,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -150f), new Vector2(420f, 48f), UiPanelSoft);
            Text tailText = CreateUiText("TailWarningText", tailBox.transform, uiFont, "Güvenli takip mesafesi", 20, TextAnchor.MiddleCenter,
                UiOffWhite, Vector2.zero, new Vector2(420f, 48f), FontStyle.Bold);

            // =================================================================
            // 3. TOP-RIGHT: CLOCK, MONEY ("₺ 250" + POP), ENERGY & UTILITIES
            // =================================================================
            GameObject statusCard = CreateStyledPanel("StatusCard_TopRight", safeRoot.transform, uiSprites.RoundedRect,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -22f), new Vector2(316f, 76f), UiPanelSoft);

            Text clockText = CreateUiText("ClockText", statusCard.transform, uiFont, "09:00", 24, TextAnchor.MiddleLeft,
                UiMutedText, new Vector2(20f, -10f), new Vector2(100f, 32f), FontStyle.Bold);

            Text moneyText = CreateUiText("MoneyText", statusCard.transform, uiFont, "₺ 250", 26, TextAnchor.MiddleRight,
                UiOffWhite, new Vector2(124f, -8f), new Vector2(172f, 36f), FontStyle.Bold);

            // Thin rounded Energy bar inside StatusCard bottom
            GameObject energyTrack = CreateStyledPanel("EnergyTrack", statusCard.transform, uiSprites.RoundedRect,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 14f), new Vector2(276f, 12f), new Color(1f, 1f, 1f, 0.12f), addShadow: false);
            GameObject energyFillGo = CreateStyledPanel("EnergyFill", energyTrack.transform, uiSprites.RoundedRect,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, UiAccentTeal, addShadow: false);
            RectTransform efRt = energyFillGo.GetComponent<RectTransform>();
            efRt.offsetMin = Vector2.zero;
            efRt.offsetMax = Vector2.zero;
            Image energyFillImg = energyFillGo.GetComponent<Image>();
            energyFillImg.type = Image.Type.Filled;
            energyFillImg.fillMethod = Image.FillMethod.Horizontal;
            energyFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            energyFillImg.fillAmount = 0.80f;

            // Tired badge ("Yorgun") shown when energy < 20
            GameObject tiredBadge = CreateStyledPanel("TiredBadge", safeRoot.transform, uiSprites.RoundedRect,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-356f, -28f), new Vector2(114f, 40f), UiDangerRed);
            Text tiredLbl = CreateUiText("TiredLabel", tiredBadge.transform, uiFont, "Yorgun", 20, TextAnchor.MiddleCenter,
                UiOffWhite, Vector2.zero, new Vector2(114f, 40f), FontStyle.Bold);
            tiredBadge.SetActive(false);

            // Floating +/- money pop delta
            Text moneyDeltaText = CreateUiText("MoneyDeltaPop", safeRoot.transform, uiFont, "+40 ₺", 24, TextAnchor.MiddleRight,
                UiAccentTeal, Vector2.zero, new Vector2(180f, 34f), FontStyle.Bold);
            moneyDeltaText.rectTransform.anchorMin = new Vector2(1f, 1f);
            moneyDeltaText.rectTransform.anchorMax = new Vector2(1f, 1f);
            moneyDeltaText.rectTransform.pivot = new Vector2(1f, 1f);
            moneyDeltaText.rectTransform.anchoredPosition = new Vector2(-34f, -96f);
            moneyDeltaText.gameObject.SetActive(false);

            var (gfxBtn, gfxLbl) = CreateStyledButton("Btn_GraphicsTier", safeRoot.transform, uiSprites.RoundedRect, uiFont, "Kalite: Orta",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-156f, -108f), new Vector2(188f, 44f), UiButtonDark, 20);

            var (resetBtn, resetLbl) = CreateStyledButton("Btn_ResetSave", safeRoot.transform, uiSprites.RoundedRect, uiFont, "Sıfırla",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -108f), new Vector2(118f, 44f), UiButtonDark, 20);

            GameObject perfPanel = CreateStyledPanel("TelemetryPanel", safeRoot.transform, uiSprites.RoundedRect,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -162f), new Vector2(440f, 38f), UiPanelSoft);
            Button perfBtn = perfPanel.AddComponent<Button>();
            perfBtn.targetGraphic = perfPanel.GetComponent<Image>();
            Text perfText = CreateUiText("TelemetryText", perfPanel.transform, uiFont, "FPS -- | -- ms | -- MB", 18, TextAnchor.MiddleCenter,
                UiAccentTeal, Vector2.zero, new Vector2(440f, 38f));
            perfPanel.SetActive(false);

            // =================================================================
            // 4. ON-FOOT CONTROLS (Floating Left-40% Joystick + Sprint Button)
            // =================================================================
            GameObject onFootRoot = CreateUiRect("OnFootControls", safeRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject joyZone = CreateUiRect("JoystickTouchZone", onFootRoot.transform,
                new Vector2(0f, 0f), new Vector2(0.44f, 0.72f), Vector2.zero, Vector2.zero);
            Image joyZoneRaycaster = joyZone.AddComponent<Image>();
            joyZoneRaycaster.color = new Color(0f, 0f, 0f, 0f);
            CanvasGroup joyGroup = joyZone.AddComponent<CanvasGroup>();
            joyGroup.alpha = 0.52f;

            GameObject joyBg = CreateStyledPanel("VirtualJoystick_BG", joyZone.transform, uiSprites.Circle,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(198f, 186f), new Vector2(284f, 284f), new Color(0.07f, 0.09f, 0.11f, 0.34f), sliced: false, addShadow: false);
            joyBg.GetComponent<Image>().raycastTarget = false;

            GameObject joyOutline = CreateStyledPanel("Joystick_Ring", joyBg.transform, uiSprites.Ring,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(284f, 284f), new Color(0.93f, 0.94f, 0.95f, 0.44f), sliced: false, addShadow: false);
            joyOutline.GetComponent<Image>().raycastTarget = false;

            GameObject joyHandle = CreateStyledPanel("Joystick_Handle", joyBg.transform, uiSprites.Circle,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(116f, 116f), new Color(0.93f, 0.94f, 0.95f, 0.68f), sliced: false, addShadow: false);
            Image joyHandleImg = joyHandle.GetComponent<Image>();
            joyHandleImg.raycastTarget = false;

            VirtualJoystick joystick = joyZone.AddComponent<VirtualJoystick>();
            joystick.Configure(joyBg.GetComponent<RectTransform>(), joyHandle.GetComponent<RectTransform>(), 96f);
            joystick.ConfigureFloating(
                joyGroup,
                joyHandleImg,
                new Color(0.93f, 0.94f, 0.95f, 0.68f),
                UiAccentTeal,
                new Vector2(198f, 186f),
                deadZoneValue: 0.12f);

            var (sprintBtn, sprintLbl) = CreateStyledHoldButton("Btn_Sprint", onFootRoot.transform, uiSprites.Circle, uiFont, "Koş",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-42f, 46f), new Vector2(168f, 168f), UiButtonDark, 26, sliced: false);

            var (attackBtn, _) = CreateStyledButton("Btn_Attack", onFootRoot.transform, uiSprites.Circle, uiFont, "Vur",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-234f, 54f), new Vector2(150f, 150f), new Color(0.58f, 0.20f, 0.18f, 0.88f), 24, sliced: false);
            attackBtn.gameObject.AddComponent<TouchAttackButtonBinder>();

            var (jumpBtn, _) = CreateStyledButton("Btn_Jump", onFootRoot.transform, uiSprites.Circle, uiFont, "Atla",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-52f, 238f), new Vector2(150f, 150f), UiAccentTeal, 24, sliced: false);
            jumpBtn.gameObject.AddComponent<TouchJumpButtonBinder>();

            // =================================================================
            // 5. DRIVING CONTROLS (◀ ▶ Steer, GAZ/FREN/El Freni, Circular Speedometer)
            // =================================================================
            GameObject drivingRoot = CreateUiRect("DrivingControls", safeRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var (steerL, _) = CreateStyledHoldButton("Btn_SteerLeft", drivingRoot.transform, uiSprites.RoundedRect, uiFont, "◀",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(38f, 48f), new Vector2(196f, 178f), UiButtonDark, 40);
            var (steerR, _) = CreateStyledHoldButton("Btn_SteerRight", drivingRoot.transform, uiSprites.RoundedRect, uiFont, "▶",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(258f, 48f), new Vector2(196f, 178f), UiButtonDark, 40);

            var (brakeBtn, _) = CreateStyledHoldButton("Btn_Brake", drivingRoot.transform, uiSprites.RoundedRect, uiFont, "▼\nFREN",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-242f, 48f), new Vector2(176f, 178f), UiButtonDark, 24);
            var (gasBtn, _) = CreateStyledHoldButton("Btn_Gas", drivingRoot.transform, uiSprites.RoundedRect, uiFont, "▲\nGAZ",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-38f, 48f), new Vector2(180f, 228f), UiAccentTeal, 26);

            var (handbrakeBtn, handbrakeLbl) = CreateStyledHoldButton("Btn_Handbrake", drivingRoot.transform, uiSprites.RoundedRect, uiFont, "El Freni",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-242f, 248f), new Vector2(176f, 82f), UiButtonDark, 22);

            var (exitBtn, exitLbl) = CreateStyledButton("Btn_ExitCar", drivingRoot.transform, uiSprites.RoundedRect, uiFont, "İn",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-38f, 298f), new Vector2(180f, 82f), UiButtonDark, 24);

            // Circular Speedometer + Gear + Fuel Bar (bottom-centre)
            GameObject speedRoot = CreateUiRect("Speedometer_Group", drivingRoot.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-82f, 40f), new Vector2(82f, 226f));

            GameObject speedDisc = CreateStyledPanel("SpeedDisc", speedRoot.transform, uiSprites.Circle,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(158f, 158f), UiPanelSoft, sliced: false);

            GameObject ringTrackGo = CreateStyledPanel("SpeedRing_Track", speedDisc.transform, uiSprites.Ring,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(148f, 148f), new Color(1f, 1f, 1f, 0.14f), sliced: false, addShadow: false);
            ringTrackGo.transform.localEulerAngles = new Vector3(0f, 0f, -45f);
            Image ringTrackImg = ringTrackGo.GetComponent<Image>();
            ringTrackImg.type = Image.Type.Filled;
            ringTrackImg.fillMethod = Image.FillMethod.Radial360;
            ringTrackImg.fillOrigin = (int)Image.Origin360.Bottom;
            ringTrackImg.fillClockwise = true;
            ringTrackImg.fillAmount = 0.75f;
            ringTrackImg.raycastTarget = false;

            GameObject ringFillGo = CreateStyledPanel("SpeedRing_Fill", speedDisc.transform, uiSprites.Ring,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(148f, 148f), UiAccentTeal, sliced: false, addShadow: false);
            ringFillGo.transform.localEulerAngles = new Vector3(0f, 0f, -45f);
            Image speedRingFillImg = ringFillGo.GetComponent<Image>();
            speedRingFillImg.type = Image.Type.Filled;
            speedRingFillImg.fillMethod = Image.FillMethod.Radial360;
            speedRingFillImg.fillOrigin = (int)Image.Origin360.Bottom;
            speedRingFillImg.fillClockwise = true;
            speedRingFillImg.fillAmount = 0f;
            speedRingFillImg.raycastTarget = false;

            Text speedText = CreateUiText("SpeedText", speedDisc.transform, uiFont, "0", 34, TextAnchor.MiddleCenter,
                UiOffWhite, new Vector2(0f, -34f), new Vector2(158f, 44f), FontStyle.Bold);
            CreateUiText("SpeedUnit", speedDisc.transform, uiFont, "km/s", 18, TextAnchor.MiddleCenter,
                UiMutedText, new Vector2(0f, -76f), new Vector2(158f, 24f));
            Text gearText = CreateUiText("GearText", speedDisc.transform, uiFont, "N", 20, TextAnchor.MiddleCenter,
                UiAccentTeal, new Vector2(0f, -112f), new Vector2(158f, 28f), FontStyle.Bold);

            // Fuel Bar directly beneath Circular Speedometer
            GameObject fuelTrack = CreateStyledPanel("FuelBar_Track", speedRoot.transform, uiSprites.RoundedRect,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 6f), new Vector2(148f, 14f), UiPanelSoft, addShadow: false);
            GameObject fuelFillGo = CreateStyledPanel("FuelBar_Fill", fuelTrack.transform, uiSprites.RoundedRect,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, UiAccentTeal, addShadow: false);
            RectTransform ffRt = fuelFillGo.GetComponent<RectTransform>();
            ffRt.offsetMin = new Vector2(2f, 2f);
            ffRt.offsetMax = new Vector2(-2f, -2f);
            Image fuelFillImg = fuelFillGo.GetComponent<Image>();
            fuelFillImg.type = Image.Type.Filled;
            fuelFillImg.fillMethod = Image.FillMethod.Horizontal;
            fuelFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fuelFillImg.fillAmount = 0.80f;

            // =================================================================
            // 6. SHARED INTERACT PILL ("E · Otur", fades/slides in 0.15 s)
            // =================================================================
            var (interactBtn, interactLbl) = CreateStyledButton("Btn_Interact", safeRoot.transform, uiSprites.RoundedRect, uiFont, "E · Etkileş",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 74f), new Vector2(316f, 84f), UiAccentTeal, 26);
            interactBtn.gameObject.AddComponent<CanvasGroup>();

            // =================================================================
            // 7. MODALS: PAINT PALETTE, SHOP COUNTER, PHONE/MAP, DIALOGUE, FAIL
            // =================================================================
            BuildPaintPaletteModal(canvasObj, safeRoot.transform, uiSprites, uiFont);
            BuildShopCounterModal(canvasObj, safeRoot.transform, uiSprites, uiFont);
            BuildPhoneModal(canvasObj, safeRoot.transform, uiSprites, uiFont, townMapTexture);

            // Toast & Editor Hint
            Text toastText = CreateUiText("StatusToast", safeRoot.transform, uiFont, "", 24, TextAnchor.MiddleCenter,
                UiOffWhite, Vector2.zero, new Vector2(820f, 44f), FontStyle.Bold);
            toastText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            toastText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            toastText.rectTransform.pivot = new Vector2(0.5f, 0f);
            toastText.rectTransform.anchoredPosition = new Vector2(0f, 146f);

            Text hintText = CreateUiText("EditorControlsHint", safeRoot.transform, uiFont, "", 18, TextAnchor.LowerCenter,
                new Color(0.85f, 0.88f, 0.92f, 0.65f), Vector2.zero, new Vector2(960f, 28f));
            hintText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            hintText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            hintText.rectTransform.pivot = new Vector2(0.5f, 0f);
            hintText.rectTransform.anchoredPosition = new Vector2(0f, 10f);

            // Dialogue Panel
            GameObject dlgPanel = CreateStyledPanel("DialoguePanel", safeRoot.transform, uiSprites.RoundedRect,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 38f), new Vector2(1060f, 212f), UiPanelModal);
            dlgPanel.AddComponent<CanvasGroup>();
            Text dlgSpeaker = CreateUiText("SpeakerName", dlgPanel.transform, uiFont, "Marco", 26, TextAnchor.UpperLeft,
                UiAccentTeal, new Vector2(28f, -20f), new Vector2(760f, 38f), FontStyle.Bold);
            Text dlgBody = CreateUiText("DialogueBody", dlgPanel.transform, uiFont, "...", 26, TextAnchor.UpperLeft,
                UiOffWhite, new Vector2(28f, -64f), new Vector2(780f, 124f));
            var (dlgBtn, dlgContinueLbl) = CreateStyledButton("Btn_DialogueContinue", dlgPanel.transform, uiSprites.RoundedRect, uiFont, "Devam",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 24f), new Vector2(196f, 64f), UiAccentTeal, 24);

            // Mission Failed Modal
            GameObject failPanel = CreateStyledPanel("MissionFailedPanel", safeRoot.transform, uiSprites.RoundedRect,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(640f, 290f), UiPanelModal);
            failPanel.AddComponent<CanvasGroup>();
            Text failTitle = CreateUiText("FailedTitle", failPanel.transform, uiFont, "Görev Başarısız", 34, TextAnchor.MiddleCenter,
                UiDangerRed, new Vector2(0f, -28f), new Vector2(640f, 48f), FontStyle.Bold);
            Text failReason = CreateUiText("FailedReason", failPanel.transform, uiFont, "Süre doldu.", 26, TextAnchor.MiddleCenter,
                UiOffWhite, new Vector2(36f, -92f), new Vector2(568f, 84f));
            var (retryBtn, retryLbl) = CreateStyledButton("Btn_RetryMission", failPanel.transform, uiSprites.RoundedRect, uiFont, "Tekrar Dene",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 28f), new Vector2(280f, 64f), UiAccentTeal, 24);

            // Mission Complete Banner
            GameObject bannerPanel = CreateStyledPanel("MissionCompleteBanner", safeRoot.transform, uiSprites.RoundedRect,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 120f), new Vector2(620f, 126f), UiPanelModal);
            bannerPanel.AddComponent<CanvasGroup>();
            Text bannerText = CreateUiText("BannerText", bannerPanel.transform, uiFont, "Görev Tamamlandı", 30, TextAnchor.MiddleCenter,
                UiAccentTeal, Vector2.zero, new Vector2(620f, 126f), FontStyle.Bold);

            // Full-screen Sleep Fade Overlay (placed topmost on Canvas so it covers SafeArea seamlessly)
            GameObject sleepFadeObj = CreateUiRect("SleepFadeOverlay", canvasObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image sleepImg = sleepFadeObj.AddComponent<Image>();
            sleepImg.color = Color.black;
            CanvasGroup sleepGroup = sleepFadeObj.AddComponent<CanvasGroup>();
            sleepGroup.alpha = 0f;
            sleepGroup.blocksRaycasts = false;
            sleepFadeObj.SetActive(false);

            if (needsController != null)
            {
                needsController.ConfigureSleepFade(sleepGroup);
            }

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

            hud.WireOpenWorldReferences(
                missionBoxGroup,
                jobObjText,
                cancelJobBtn,
                cancelJobLbl,
                clockText,
                statusCard.GetComponent<RectTransform>(),
                moneyText,
                moneyDeltaText,
                energyFillImg,
                tiredBadge,
                tiredLbl,
                sprintBtn,
                sprintLbl,
                handbrakeBtn,
                handbrakeLbl,
                phoneBtn,
                phoneLbl,
                speedRingFillImg,
                gearText,
                fuelFillImg);
        }

        private static void BuildPaintPaletteModal(
            GameObject canvasHost,
            Transform safeRoot,
            GeneratedUiSprites uiSprites,
            Font uiFont)
        {
            GameObject paintPanel = CreateStyledPanel("PaintPalettePanel", safeRoot, uiSprites.RoundedRect,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(700f, 392f), UiPanelModal);
            CanvasGroup paintGroup = paintPanel.AddComponent<CanvasGroup>();

            Text paintTitle = CreateUiText("PaintTitle", paintPanel.transform, uiFont, "Evi Boya", 34, TextAnchor.MiddleCenter,
                UiOffWhite, new Vector2(0f, -18f), new Vector2(700f, 44f), FontStyle.Bold);
            Text paintHint = CreateUiText("PaintJobHint", paintPanel.transform, uiFont, "", 20, TextAnchor.MiddleCenter,
                UiAccentTeal, new Vector2(0f, -62f), new Vector2(700f, 28f), FontStyle.Bold);

            Color[] swatchPalette = PaintPalettePanel.DefaultSwatchColors;
            Button[] swatchButtons = new Button[8];
            Text[] swatchLabels = new Text[8];
            for (int i = 0; i < 8; i++)
            {
                int col = i % 4;
                int row = i / 4;
                float bx = -228f + col * 152f;
                float by = 44f - row * 94f;
                var (swBtn, swLbl) = CreateStyledButton($"Btn_PaintSwatch_{i + 1}", paintPanel.transform, uiSprites.RoundedRect, uiFont, JobManager.GetLocalizedColorName(i),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(bx, by), new Vector2(138f, 80f), swatchPalette[i], 18);
                bool darkSwatch = (i == 2 || i == 7);
                swLbl.color = darkSwatch ? UiOffWhite : new Color(0.10f, 0.12f, 0.15f, 0.94f);
                swatchButtons[i] = swBtn;
                swatchLabels[i] = swLbl;
            }

            var (paintCancelBtn, paintCancelLbl) = CreateStyledButton("Btn_PaintCancel", paintPanel.transform, uiSprites.RoundedRect, uiFont, "İptal",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 22f), new Vector2(220f, 56f), UiButtonDark, 22);
            paintPanel.SetActive(false);

            PaintPalettePanel paintPalette = canvasHost.AddComponent<PaintPalettePanel>();
            paintPalette.WireReferences(paintPanel, paintTitle, swatchButtons, swatchPalette, paintCancelBtn, paintCancelLbl);
            paintPalette.WirePolishReferences(paintGroup, paintHint, swatchLabels);
        }

        private static void BuildShopCounterModal(
            GameObject canvasHost,
            Transform safeRoot,
            GeneratedUiSprites uiSprites,
            Font uiFont)
        {
            GameObject shopPanel = CreateStyledPanel("ShopCounterPanel", safeRoot, uiSprites.RoundedRect,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(680f, 576f), UiPanelModal);
            CanvasGroup shopGroup = shopPanel.AddComponent<CanvasGroup>();

            Text shopTitle = CreateUiText("ShopTitle", shopPanel.transform, uiFont, "Çınar Kafe", 32, TextAnchor.MiddleCenter,
                UiOffWhite, new Vector2(0f, -18f), new Vector2(680f, 42f), FontStyle.Bold);
            Text shopSub = CreateUiText("ShopBalanceStatus", shopPanel.transform, uiFont, "Bakiye: ₺ 250   ·   Enerji: %80", 20, TextAnchor.MiddleCenter,
                UiAccentTeal, new Vector2(0f, -62f), new Vector2(680f, 28f), FontStyle.Bold);

            Button[] itemBtns = new Button[5];
            Text[] itemLbls = new Text[5];
            string[] defaults =
            {
                "Türk Kahvesi · 15 ₺ (+25 Enerji)",
                "Kaşarlı Tost · 25 ₺ (+40 Enerji)",
                "Sıcak Yemek · 40 ₺ (+65 Enerji)",
                "Enerji İçeceği · 55 ₺ (%100 Enerji)",
                "Yol Kumanyası · 75 ₺ (Enerji + Yakıt)"
            };

            for (int i = 0; i < 5; i++)
            {
                float yOffset = 148f - i * 68f;
                var (iBtn, iLbl) = CreateStyledButton($"Btn_ShopItem_{i + 1}", shopPanel.transform, uiSprites.RoundedRect, uiFont, defaults[i],
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, yOffset), new Vector2(588f, 56f), new Color(0.13f, 0.17f, 0.23f, 0.92f), 21);
                itemBtns[i] = iBtn;
                itemLbls[i] = iLbl;
            }

            var (closeBtn, closeLbl) = CreateStyledButton("Btn_ShopClose", shopPanel.transform, uiSprites.RoundedRect, uiFont, "Kapat",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 22f), new Vector2(220f, 54f), UiButtonDark, 22);
            shopPanel.SetActive(false);

            ShopMenuPanel ctrl = canvasHost.AddComponent<ShopMenuPanel>();
            ctrl.WireReferences(shopPanel, shopGroup, shopTitle, shopSub, itemBtns, itemLbls, closeBtn, closeLbl);
        }

        private static void BuildPhoneModal(
            GameObject canvasHost,
            Transform safeRoot,
            GeneratedUiSprites uiSprites,
            Font uiFont,
            Texture2D townMapTexture)
        {
            GameObject phonePanel = CreateStyledPanel("PhonePanel", safeRoot, uiSprites.RoundedRect,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1260f, 840f), UiPanelModal);
            CanvasGroup phoneGroup = phonePanel.AddComponent<CanvasGroup>();

            // Top Header & Tabs
            Text phoneTitle = CreateUiText("PhoneHeaderTitle", phonePanel.transform, uiFont, "Kasaba Rehberi", 34, TextAnchor.MiddleLeft,
                UiOffWhite, new Vector2(34f, -20f), new Vector2(320f, 48f), FontStyle.Bold);

            var (btnTabMap, lblTabMap) = CreateStyledButton("Btn_Tab_Map", phonePanel.transform, uiSprites.RoundedRect, uiFont, "Harita",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-180f, -20f), new Vector2(168f, 50f), UiAccentTeal, 22);
            var (btnTabJobs, lblTabJobs) = CreateStyledButton("Btn_Tab_Jobs", phonePanel.transform, uiSprites.RoundedRect, uiFont, "İşler",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(168f, 50f), UiButtonDark, 22);
            var (btnTabStats, lblTabStats) = CreateStyledButton("Btn_Tab_Stats", phonePanel.transform, uiSprites.RoundedRect, uiFont, "İstatistik",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(180f, -20f), new Vector2(168f, 50f), UiButtonDark, 22);

            var (btnLang, lblLang) = CreateStyledButton("Btn_LanguageToggle", phonePanel.transform, uiSprites.RoundedRect, uiFont, "Dil: TR",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-186f, -20f), new Vector2(138f, 50f), UiButtonDark, 20);
            var (btnClose, lblClose) = CreateStyledButton("Btn_PhoneClose", phonePanel.transform, uiSprites.RoundedRect, uiFont, "Kapat",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -20f), new Vector2(142f, 50f), UiButtonDark, 20);

            // --- TAB 1: MAP ---
            GameObject mapTab = CreateUiRect("Tab_Map", phonePanel.transform, Vector2.zero, Vector2.one,
                new Vector2(32f, 28f), new Vector2(-32f, -88f));

            GameObject mapFrame = CreateStyledPanel("MapViewportFrame", mapTab.transform, uiSprites.RoundedRect,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(8f, 0f), new Vector2(692f, 692f), new Color(0.11f, 0.14f, 0.18f, 0.95f));

            GameObject mapViewport = CreateUiRect("MapViewport", mapFrame.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-336f, -336f), new Vector2(336f, 336f));
            RawImage townMapRaw = mapViewport.AddComponent<RawImage>();
            townMapRaw.texture = townMapTexture;

            // Markers inside MapViewport (centered pivot)
            RectTransform[] carIcons = new RectTransform[8];
            for (int i = 0; i < carIcons.Length; i++)
            {
                GameObject carDot = CreateStyledPanel($"CarIcon_{i + 1}", mapViewport.transform, uiSprites.Circle,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(13f, 13f), new Color(0.58f, 0.80f, 0.98f, 0.95f), sliced: false, addShadow: false);
                carDot.GetComponent<Image>().raycastTarget = false;
                carIcons[i] = carDot.GetComponent<RectTransform>();
            }

            RectTransform[] jobIcons = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject jBadge = CreateStyledPanel($"JobIcon_{i + 1}", mapViewport.transform, uiSprites.Circle,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(22f, 22f), new Color(0.94f, 0.75f, 0.30f, 0.96f), sliced: false, addShadow: false);
                jBadge.GetComponent<Image>().raycastTarget = false;
                CreateUiText("Star", jBadge.transform, uiFont, "★", 14, TextAnchor.MiddleCenter,
                    new Color(0.10f, 0.12f, 0.15f, 1f), Vector2.zero, new Vector2(22f, 22f), FontStyle.Bold);
                jobIcons[i] = jBadge.GetComponent<RectTransform>();
            }

            GameObject gasBadge = CreateStyledPanel("GasStationIcon", mapViewport.transform, uiSprites.RoundedRect,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(24f, 24f), UiAccentTeal, addShadow: false);
            gasBadge.GetComponent<Image>().raycastTarget = false;
            CreateUiText("Lbl", gasBadge.transform, uiFont, "B", 14, TextAnchor.MiddleCenter,
                new Color(0.08f, 0.10f, 0.12f, 1f), Vector2.zero, new Vector2(24f, 24f), FontStyle.Bold);

            GameObject wpRing = CreateStyledPanel("WaypointIcon", mapViewport.transform, uiSprites.Ring,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(28f, 28f), UiAccentTeal, sliced: false, addShadow: false);
            wpRing.GetComponent<Image>().raycastTarget = false;

            GameObject playerArrowObj = CreateStyledPanel("PlayerMapArrow", mapViewport.transform, uiSprites.Circle,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(24f, 24f), UiPanelModal, sliced: false, addShadow: false);
            playerArrowObj.GetComponent<Image>().raycastTarget = false;
            CreateUiText("ArrowGlyph", playerArrowObj.transform, uiFont, "▲", 18, TextAnchor.MiddleCenter,
                UiOffWhite, Vector2.zero, new Vector2(24f, 24f), FontStyle.Bold);

            // Right legend sidebar inside Map Tab
            GameObject legendCard = CreateStyledPanel("MapSidebarCard", mapTab.transform, uiSprites.RoundedRect,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-8f, 0f), new Vector2(470f, 692f), new Color(0.10f, 0.13f, 0.17f, 0.76f));
            Text mapHint = CreateUiText("MapHint", legendCard.transform, uiFont, "Haritaya dokunarak özel hedef işaretleyebilirsin.", 20, TextAnchor.UpperLeft,
                UiAccentTeal, new Vector2(24f, -24f), new Vector2(422f, 68f), FontStyle.Bold);
            Text mapLegend = CreateUiText("MapLegend", legendCard.transform, uiFont, "", 22, TextAnchor.UpperLeft,
                UiOffWhite, new Vector2(24f, -110f), new Vector2(422f, 280f));
            mapLegend.lineSpacing = 1.35f;

            var (btnClearWp, lblClearWp) = CreateStyledButton("Btn_ClearWaypoint", legendCard.transform, uiSprites.RoundedRect, uiFont, "İşareti Kaldır",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 28f), new Vector2(410f, 58f), UiButtonDark, 22);

            // --- TAB 2: JOBS ---
            GameObject jobsTab = CreateUiRect("Tab_Jobs", phonePanel.transform, Vector2.zero, Vector2.one,
                new Vector2(36f, 28f), new Vector2(-36f, -92f));

            Button[] jobBtns = new Button[4];
            Text[] jobTitles = new Text[4];
            Text[] jobDescs = new Text[4];
            Text[] jobPinLbls = new Text[4];

            for (int i = 0; i < 4; i++)
            {
                float rowY = -12f - i * 138f;
                GameObject rowCard = CreateStyledPanel($"JobRow_{i + 1}", jobsTab.transform, uiSprites.RoundedRect,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, rowY), new Vector2(1140f, 122f), new Color(0.11f, 0.14f, 0.19f, 0.80f));

                jobTitles[i] = CreateUiText("JobTitle", rowCard.transform, uiFont, "İş", 26, TextAnchor.UpperLeft,
                    UiOffWhite, new Vector2(26f, -18f), new Vector2(820f, 36f), FontStyle.Bold);
                jobDescs[i] = CreateUiText("JobDesc", rowCard.transform, uiFont, "...", 20, TextAnchor.UpperLeft,
                    UiMutedText, new Vector2(26f, -62f), new Vector2(820f, 44f));

                var (pinBtn, pinLbl) = CreateStyledButton($"Btn_PinJob_{i + 1}", rowCard.transform, uiSprites.RoundedRect, uiFont, "Konum İşaretle",
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-24f, 0f), new Vector2(240f, 60f), UiAccentTeal, 20);
                jobBtns[i] = pinBtn;
                jobPinLbls[i] = pinLbl;
            }

            var (btnCancelActiveJob, lblCancelActiveJob) = CreateStyledButton("Btn_CancelActiveJob_Phone", jobsTab.transform, uiSprites.RoundedRect, uiFont, "Aktif İşi İptal Et",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 12f), new Vector2(360f, 58f), UiDangerRed, 22);

            // --- TAB 3: STATS ---
            GameObject statsTab = CreateUiRect("Tab_Stats", phonePanel.transform, Vector2.zero, Vector2.one,
                new Vector2(36f, 28f), new Vector2(-36f, -92f));

            Text[] statNames = new Text[7];
            Text[] statValues = new Text[7];
            for (int i = 0; i < 7; i++)
            {
                float sy = -18f - i * 88f;
                GameObject sRow = CreateStyledPanel($"StatRow_{i + 1}", statsTab.transform, uiSprites.RoundedRect,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, sy), new Vector2(960f, 74f), new Color(0.11f, 0.14f, 0.19f, 0.76f));

                statNames[i] = CreateUiText("StatName", sRow.transform, uiFont, "...", 26, TextAnchor.MiddleLeft,
                    UiMutedText, new Vector2(28f, -12f), new Vector2(540f, 50f));
                statValues[i] = CreateUiText("StatValue", sRow.transform, uiFont, "0", 26, TextAnchor.MiddleRight,
                    UiOffWhite, new Vector2(580f, -12f), new Vector2(352f, 50f), FontStyle.Bold);
            }

            phonePanel.SetActive(false);

            PhonePanelController phoneCtrl = canvasHost.AddComponent<PhonePanelController>();
            phoneCtrl.WireReferences(
                phonePanel, phoneGroup, phoneTitle,
                btnTabMap, lblTabMap,
                btnTabJobs, lblTabJobs,
                btnTabStats, lblTabStats,
                btnLang, lblLang,
                btnClose, lblClose,
                mapTab, jobsTab, statsTab,
                townMapRaw, mapViewport.GetComponent<RectTransform>(),
                playerArrowObj.GetComponent<RectTransform>(),
                wpRing.GetComponent<RectTransform>(),
                gasBadge.GetComponent<RectTransform>(),
                jobIcons, carIcons,
                btnClearWp, lblClearWp, mapHint, mapLegend,
                jobBtns, jobTitles, jobDescs, jobPinLbls,
                btnCancelActiveJob, lblCancelActiveJob,
                statNames, statValues);
        }

        // --- Styled UI Element Builders ---
        private static GameObject CreateStyledPanel(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            Color color,
            bool sliced = true,
            bool addShadow = true)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = (sliced && sprite != null && sprite.border.sqrMagnitude > 0.1f) ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;

            if (addShadow)
            {
                Shadow sh = go.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.25f);
                sh.effectDistance = new Vector2(0f, -2f);
            }

            return go;
        }

        private static (Button button, Text label) CreateStyledButton(
            string name,
            Transform parent,
            Sprite sprite,
            Font font,
            string labelText,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            Color color,
            int fontSize,
            bool sliced = true)
        {
            GameObject go = CreateStyledPanel(name, parent, sprite, anchorMin, anchorMax, pivot, pos, size, color, sliced: sliced);
            Image img = go.GetComponent<Image>();
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;

            UiButtonFeedback feedback = go.AddComponent<UiButtonFeedback>();
            feedback.Configure(btn, img);

            bool brightBg = (color.r * 0.3f + color.g * 0.59f + color.b * 0.11f) > 0.52f && color.a > 0.7f;
            Color textCol = brightBg ? new Color(0.07f, 0.09f, 0.11f, 0.96f) : UiOffWhite;

            Text lbl = CreateUiText("Label", go.transform, font, labelText, fontSize, TextAnchor.MiddleCenter, textCol, Vector2.zero, size, FontStyle.Bold);
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = Vector2.one;
            lbl.rectTransform.offsetMin = new Vector2(8f, 4f);
            lbl.rectTransform.offsetMax = new Vector2(-8f, -4f);

            return (btn, lbl);
        }

        private static (HoldButton holdButton, Text label) CreateStyledHoldButton(
            string name,
            Transform parent,
            Sprite sprite,
            Font font,
            string labelText,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            Color normalColor,
            int fontSize,
            bool sliced = true)
        {
            GameObject go = CreateStyledPanel(name, parent, sprite, anchorMin, anchorMax, pivot, pos, size, normalColor, sliced: sliced);
            Image img = go.GetComponent<Image>();
            HoldButton hold = go.AddComponent<HoldButton>();
            Color pressedCol = Color.Lerp(normalColor, UiAccentTeal, 0.65f);
            pressedCol.a = 0.94f;
            hold.ConfigureVisual(img, normalColor, pressedCol, releaseOnExit: false);

            bool brightBg = (normalColor.r * 0.3f + normalColor.g * 0.59f + normalColor.b * 0.11f) > 0.52f && normalColor.a > 0.7f;
            Color textCol = brightBg ? new Color(0.07f, 0.09f, 0.11f, 0.96f) : UiOffWhite;

            Text lbl = CreateUiText("Label", go.transform, font, labelText, fontSize, TextAnchor.MiddleCenter, textCol, Vector2.zero, size, FontStyle.Bold);
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = Vector2.one;
            lbl.rectTransform.offsetMin = new Vector2(6f, 4f);
            lbl.rectTransform.offsetMax = new Vector2(-6f, -4f);

            return (hold, lbl);
        }
    }
}
