using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Jobs;
using OurWorld.Missions;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    public sealed partial class MainMenuController
    {
        // =====================================================================
        // HUD PAUSE / MENU BUTTON INTEGRATION
        // =====================================================================

        private void InstallHudMenuButton()
        {
            if (_hud == null) return;

            _hud.SetLegacyTopRightUtilityVisible(false);

            Transform safeArea = _hud.transform.Find("SafeAreaRoot");
            Transform parent = safeArea != null ? safeArea : _hud.transform;

            if (parent.Find("Btn_MainMenu") != null) return;

            GameObject btnGo = CreateRoundedPanel("Btn_MainMenu", parent, _roundedCardSprite,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -114f), new Vector2(176f, 70f),
                new Color(0.055f, 0.082f, 0.130f, 0.92f), addBorder: true);
            UnityEngine.UI.Shadow btnSh = btnGo.AddComponent<UnityEngine.UI.Shadow>();
            btnSh.effectColor = new Color(0f, 0f, 0f, 0.42f);
            btnSh.effectDistance = new Vector2(0f, -3f);

            UnityEngine.UI.Image bg = btnGo.GetComponent<UnityEngine.UI.Image>();
            bg.raycastTarget = true;

            UnityEngine.UI.Button btn = btnGo.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() =>
            {
                if (IsOpen || IsTransitioning) return;
                if (PhonePanelController.Instance != null && PhonePanelController.Instance.IsOpen) return;
                GameFeelAudio.PlayUiClick();
                OpenMenu(asPauseMenu: true, instant: false);
            });

            UiButtonFeedback fb = btnGo.AddComponent<UiButtonFeedback>();
            fb.Configure(btn, bg);

            // Crisp procedural 3-bar hamburger icon
            GameObject iconGo = new GameObject("MenuIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            iconGo.transform.SetParent(btnGo.transform, false);
            RectTransform irt = (RectTransform)iconGo.transform;
            irt.anchorMin = new Vector2(0f, 0.5f);
            irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(20f, 0f);
            irt.sizeDelta = new Vector2(28f, 28f);
            UnityEngine.UI.Image iconImg = iconGo.GetComponent<UnityEngine.UI.Image>();
            iconImg.sprite = _iconMenuBarsSprite;
            iconImg.color = AccentTeal;
            iconImg.raycastTarget = false;

            _hudMenuButtonLabel = CreateText("Label", btnGo.transform, "Menü", 22, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            RectTransform lrt = _hudMenuButtonLabel.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(60f, 0f);
            lrt.offsetMax = new Vector2(-12f, 0f);
            _hudMenuButtonObj = btnGo;
        }

        // =====================================================================
        // UI HIERARCHY BUILDER (uGUI · 1920x1080 Reference · Safe Area + Tablet)
        // =====================================================================

        private void BuildMenuCanvas()
        {
            _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_uiFont == null) _uiFont = Font.CreateDynamicFontFromOSFont("Arial", 24);

            _canvasObj = new GameObject("Canvas_OurWorld_MainMenu");
            _canvasObj.transform.SetParent(transform, false);

            _menuCanvas = _canvasObj.AddComponent<Canvas>();
            _menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _menuCanvas.sortingOrder = 300;

            CanvasScaler scaler = _canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasObj.AddComponent<GraphicRaycaster>();
            _menuCanvasGroup = _canvasObj.AddComponent<CanvasGroup>();

            // Full-bleed atmospheric gradient scrims (seamless edge bleed on notched devices)
            GameObject leftScrim = new GameObject("Scrim_LeftGradient", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            leftScrim.transform.SetParent(_canvasObj.transform, false);
            RectTransform lrt = (RectTransform)leftScrim.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = new Vector2(0.66f, 1f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            UnityEngine.UI.Image lImg = leftScrim.GetComponent<UnityEngine.UI.Image>();
            lImg.sprite = _leftVignetteSprite;
            lImg.color = Color.white;
            lImg.raycastTarget = false;

            GameObject bottomScrim = new GameObject("Scrim_BottomGradient", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            bottomScrim.transform.SetParent(_canvasObj.transform, false);
            RectTransform brt = (RectTransform)bottomScrim.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = new Vector2(1f, 0.34f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            UnityEngine.UI.Image bImg = bottomScrim.GetComponent<UnityEngine.UI.Image>();
            bImg.sprite = _bottomVignetteSprite;
            bImg.color = Color.white;
            bImg.raycastTarget = false;

            // Safe-Area container for all interactive content
            GameObject safeRoot = new GameObject("SafeAreaRoot", typeof(RectTransform));
            safeRoot.transform.SetParent(_canvasObj.transform, false);
            StretchFull((RectTransform)safeRoot.transform, 0f, 0f);
            safeRoot.AddComponent<SafeAreaFitter>();

            BuildLeftHeroRail(safeRoot.transform);
            BuildRightWorldInfo(safeRoot.transform);
            BuildDrawerHost(safeRoot.transform);
        }

        private void BuildLeftHeroRail(Transform parent)
        {
            GameObject rail = new GameObject("LeftHeroRail", typeof(RectTransform));
            rail.transform.SetParent(parent, false);
            _leftRailRect = (RectTransform)rail.transform;
            _leftRailRect.anchorMin = new Vector2(0f, 0f);
            _leftRailRect.anchorMax = new Vector2(0f, 1f);
            _leftRailRect.pivot = new Vector2(0f, 0.5f);
            _leftRailRect.anchoredPosition = Vector2.zero;
            _leftRailRect.sizeDelta = new Vector2(612f, 0f);

            // --- Brand Header (Top-Left) ---
            GameObject header = new GameObject("BrandHeader", typeof(RectTransform));
            header.transform.SetParent(rail.transform, false);
            RectTransform hrt = (RectTransform)header.transform;
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0f, 1f);
            hrt.anchoredPosition = new Vector2(54f, -52f);
            hrt.sizeDelta = new Vector2(-64f, 152f);

            // Official App Icon Emblem with rounded mask and golden frame
            GameObject iconFrame = CreateRoundedPanel("AppIconBadge", header.transform, _roundedCardSprite,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -6f), new Vector2(92f, 92f),
                new Color(0.10f, 0.16f, 0.24f, 0.96f), addBorder: false);

            if (_brandIconSprite != null)
            {
                GameObject maskHolder = CreateRoundedPanel("IconMask", iconFrame.transform, _roundedCardSprite,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, Color.white, addBorder: false);
                StretchFull((RectTransform)maskHolder.transform, 4f, 4f);
                UnityEngine.UI.Mask iconMask = maskHolder.AddComponent<UnityEngine.UI.Mask>();
                iconMask.showMaskGraphic = false;

                GameObject iconImgObj = new GameObject("IconImage", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                iconImgObj.transform.SetParent(maskHolder.transform, false);
                StretchFull((RectTransform)iconImgObj.transform, 0f, 0f);
                UnityEngine.UI.Image iconImg = iconImgObj.GetComponent<UnityEngine.UI.Image>();
                iconImg.sprite = _brandIconSprite;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }
            else
            {
                UnityEngine.UI.Text fallbackGlyph = CreateText("IconGlyph", iconFrame.transform, "OW", 34, FontStyle.Bold, AccentGold, TextAnchor.MiddleCenter);
                StretchFull(fallbackGlyph.rectTransform, 0f, 0f);
            }
            AddOutlineOverlay(iconFrame.transform, new Color(0.98f, 0.76f, 0.28f, 0.72f));

            // Eyebrow + Display Title + Subtitle
            _eyebrowText = CreateText("Eyebrow", header.transform, "İSTANBUL  ·  SULTANBEYLİ  ·  HAMİDİYE",
                15, FontStyle.Bold, AccentGold, TextAnchor.MiddleLeft, addShadow: true);
            PlaceTopLeft(_eyebrowText.rectTransform, 112f, -4f, 440f, 24f);

            _brandTitleText = CreateText("BrandTitle", header.transform, "BİZİM DÜNYAMIZ",
                46, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft, addShadow: true);
            PlaceTopLeft(_brandTitleText.rectTransform, 110f, -28f, 450f, 56f);

            _brandSubtitleText = CreateText("BrandSubtitle", header.transform, "Yaşayan Açık Dünya · Mahalle Yaşamı",
                19, FontStyle.Normal, TextSecondary, TextAnchor.MiddleLeft, addShadow: true);
            PlaceTopLeft(_brandSubtitleText.rectTransform, 112f, -84f, 440f, 28f);

            // Pause Badge (shown only when opened mid-session)
            _pauseBadgeObj = CreateRoundedPanel("PauseBadge", header.transform, _pillSprite,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(112f, -118f), new Vector2(174f, 30f),
                BadgeExploreBg, addBorder: false);
            AddOutlineOverlay(_pauseBadgeObj.transform, new Color(0.98f, 0.76f, 0.28f, 0.72f), _pillOutlineSprite);
            _pauseBadgeText = CreateText("Label", _pauseBadgeObj.transform, "●  DURAKLATILDI", 14, FontStyle.Bold, AccentGold, TextAnchor.MiddleCenter);
            StretchFull(_pauseBadgeText.rectTransform, 8f, 0f);
            _pauseBadgeObj.SetActive(false);

            // --- Action Stack ---
            GameObject actionStack = new GameObject("ActionStack", typeof(RectTransform));
            actionStack.transform.SetParent(rail.transform, false);
            RectTransform srt = (RectTransform)actionStack.transform;
            srt.anchorMin = new Vector2(0f, 0.5f);
            srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.anchoredPosition = new Vector2(54f, -46f);
            srt.sizeDelta = new Vector2(516f, 512f);

            VerticalLayoutGroup vlg = actionStack.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 1. Primary Hero CTA Button
            GameObject playGo = CreateRoundedPanel("Btn_PrimaryPlay", actionStack.transform, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(516f, 92f), AccentTeal, addBorder: false);
            playGo.AddComponent<LayoutElement>().preferredHeight = 92f;
            AddOutlineOverlay(playGo.transform, new Color(0.72f, 0.99f, 0.95f, 0.56f));

            UnityEngine.UI.Image playImg = playGo.GetComponent<UnityEngine.UI.Image>();
            playImg.raycastTarget = true;
            _primaryPlayBtn = playGo.AddComponent<UnityEngine.UI.Button>();
            _primaryPlayBtn.targetGraphic = playImg;
            _primaryPlayBtn.onClick.AddListener(ResumeOrEnterWorld);
            UiButtonFeedback playFb = playGo.AddComponent<UiButtonFeedback>();
            playFb.Configure(_primaryPlayBtn, playImg);

            GameObject playGlyphBg = CreateRoundedPanel("PlayGlyphCircle", playGo.transform, _pillSprite,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(52f, 52f), new Color(0.03f, 0.16f, 0.16f, 0.36f), addBorder: false);
            CreateCenteredIcon("PlayIcon", playGlyphBg.transform, _iconPlaySprite, 23f, Color.white);

            _primaryPlayLabel = CreateText("Title", playGo.transform, "MAHALLEYE GİR", 27, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, addShadow: true);
            PlaceTopLeft(_primaryPlayLabel.rectTransform, 90f, -12f, 390f, 36f);

            _primaryPlaySubLabel = CreateText("Sub", playGo.transform, "Selman Sokağı No:48 · Yeni Hayat", 16, FontStyle.Normal, new Color(0.88f, 0.99f, 0.97f, 0.96f), TextAnchor.MiddleLeft);
            PlaceTopLeft(_primaryPlaySubLabel.rectTransform, 90f, -49f, 390f, 26f);

            // 2. Quick-Play Sports & Mini-Games Button
            (_navSportsBtn, _navSportsBg, _navSportsLabel) = CreateMenuNavButton(
                "Btn_Sports", actionStack.transform, _iconPlaySprite, AccentMint, "Spor & Mini Oyunlar",
                () => OpenDrawer(MenuDrawer.Sports));

            // 3. Districts / Fast-Start Button (crisp Pin icon)
            (_navDistrictsBtn, _navDistrictsBg, _navDistrictsLabel) = CreateMenuNavButton(
                "Btn_Districts", actionStack.transform, _iconPinSprite, AccentTeal, "Bölge Seç & Başla",
                () => OpenDrawer(MenuDrawer.Districts));

            // 4. Settings & World Atmosphere Button (crisp Sliders icon)
            (_navSettingsBtn, _navSettingsBg, _navSettingsLabel) = CreateMenuNavButton(
                "Btn_Settings", actionStack.transform, _iconSlidersSprite, AccentGold, "Ayarlar & Dünya",
                () => OpenDrawer(MenuDrawer.Settings));

            // 5. What's New & Community Ideas Button (crisp Sparkle Star icon)
            (_navWhatsNewBtn, _, _navWhatsNewLabel) = CreateMenuNavButton(
                "Btn_WhatsNew", actionStack.transform, _iconSparkleSprite, AccentSky, "Yenilikler & Topluluk",
                HandleOpenWhatsNew);

            // 6. Town Guide / Map Button (visible in Pause mode — crisp Map Grid icon)
            (_navGuideBtn, _, _navGuideLabel) = CreateMenuNavButton(
                "Btn_TownGuide", actionStack.transform, _iconMapSprite, AccentMint, "Mahalle Rehberi & Harita",
                HandleOpenTownGuide);
            _navGuideBtn.gameObject.SetActive(false);

            // --- Footer (Bottom-Left) ---
            string version = string.IsNullOrEmpty(Application.version) ? "1.0.0" : Application.version;
            _footerText = CreateText("FooterVersion", rail.transform, $"Our World v{version}  ·  Doğuş İpeksaç",
                15, FontStyle.Normal, TextSecondary, TextAnchor.MiddleLeft, addShadow: true);
            RectTransform frt = _footerText.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(1f, 0f);
            frt.pivot = new Vector2(0f, 0f);
            frt.anchoredPosition = new Vector2(54f, 32f);
            frt.sizeDelta = new Vector2(-64f, 26f);
        }

        private (UnityEngine.UI.Button btn, UnityEngine.UI.Image bg, UnityEngine.UI.Text lbl) CreateMenuNavButton(
            string name, Transform parent, Sprite iconSprite, Color iconAccent, string defaultLabel, Action onClick)
        {
            GameObject go = CreateRoundedPanel(name, parent, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(516f, 64f), PanelGlass, addBorder: true);
            go.AddComponent<LayoutElement>().preferredHeight = 64f;

            UnityEngine.UI.Image bg = go.GetComponent<UnityEngine.UI.Image>();
            bg.raycastTarget = true;
            UnityEngine.UI.Button btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => onClick?.Invoke());

            UiButtonFeedback fb = go.AddComponent<UiButtonFeedback>();
            fb.Configure(btn, bg);

            // Distinct tinted icon chip on left
            GameObject chip = CreateRoundedPanel("IconChip", go.transform, _roundedCardSprite,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(14f, 0f), new Vector2(44f, 44f),
                new Color(iconAccent.r, iconAccent.g, iconAccent.b, 0.20f), addBorder: false);
            CreateCenteredIcon("Icon", chip.transform, iconSprite, 24f, iconAccent);

            UnityEngine.UI.Text lbl = CreateText("Label", go.transform, defaultLabel, 22, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            RectTransform lrt = lbl.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(74f, 0f);
            lrt.offsetMax = new Vector2(-46f, 0f);

            UnityEngine.UI.Text chev = CreateText("Chevron", go.transform, "›", 28, FontStyle.Bold, TextMuted, TextAnchor.MiddleCenter);
            RectTransform crt = chev.rectTransform;
            crt.anchorMin = new Vector2(1f, 0.5f);
            crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-18f, 1f);
            crt.sizeDelta = new Vector2(28f, 40f);

            return (btn, bg, lbl);
        }

        private void BuildRightWorldInfo(Transform parent)
        {
            _rightInfoRoot = new GameObject("RightWorldInfo", typeof(RectTransform));
            _rightInfoRoot.transform.SetParent(parent, false);
            StretchFull((RectTransform)_rightInfoRoot.transform, 0f, 0f);
            _rightInfoGroup = _rightInfoRoot.AddComponent<CanvasGroup>();

            // --- Top-Right Live World Clock, Weather & Scenic Camera Switcher ---
            GameObject topPill = CreateRoundedPanel("TopRight_LiveVistaCard", _rightInfoRoot.transform, _roundedCardSprite,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-48f, -44f), new Vector2(548f, 86f), PanelDark, addBorder: true);

            _liveClockText = CreateText("Clock", topPill.transform, "09:00", 28, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_liveClockText.rectTransform, 24f, -12f, 120f, 34f);

            _liveWeatherText = CreateText("Weather", topPill.transform, "Açık & Güneşli", 15, FontStyle.Bold, AccentGold, TextAnchor.MiddleLeft);
            PlaceTopLeft(_liveWeatherText.rectTransform, 24f, -48f, 154f, 24f);

            GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            divider.transform.SetParent(topPill.transform, false);
            RectTransform drt = (RectTransform)divider.transform;
            drt.anchorMin = new Vector2(0f, 0.20f);
            drt.anchorMax = new Vector2(0f, 0.80f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = new Vector2(178f, 0f);
            drt.sizeDelta = new Vector2(1.5f, 0f);
            UnityEngine.UI.Image divImg = divider.GetComponent<UnityEngine.UI.Image>();
            divImg.color = BorderSubtle;
            divImg.raycastTarget = false;

            CreateCompactIconButton("Btn_VistaPrev", topPill.transform, "‹", new Vector2(194f, 0f), () => CycleVista(-1));

            _vistaNameText = CreateText("VistaName", topPill.transform, "Fatih Bulvarı · Saat Kulesi", 17, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter);
            RectTransform vrt = _vistaNameText.rectTransform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(242f, 8f);
            vrt.offsetMax = new Vector2(-62f, -8f);
            _vistaNameText.horizontalOverflow = HorizontalWrapMode.Wrap;

            CreateCompactIconButton("Btn_VistaNext", topPill.transform, "›", new Vector2(488f, 0f), () => CycleVista(1));

            // --- Bottom-Right Living Neighborhood Profile Card ---
            GameObject profileCard = CreateRoundedPanel("BottomRight_WorldProfileCard", _rightInfoRoot.transform, _roundedCardSprite,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-48f, 44f), new Vector2(548f, 238f), PanelDark, addBorder: true);

            // Subtle warm gold top accent highlight
            AddTopAccentStrip(profileCard.transform, AccentGold);

            _profileHeaderText = CreateText("ProfileHeader", profileCard.transform, "MAHALLE PROFİLİN", 15, FontStyle.Bold, TextSecondary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_profileHeaderText.rectTransform, 24f, -20f, 236f, 24f);

            GameObject rankPill = CreateRoundedPanel("RankPill", profileCard.transform, _pillSprite,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-22f, -15f), new Vector2(228f, 34f),
                BadgeExploreBg, addBorder: false);
            AddOutlineOverlay(rankPill.transform, new Color(0.98f, 0.76f, 0.28f, 0.65f), _pillOutlineSprite);
            _rankBadgeText = CreateText("RankLabel", rankPill.transform, "★  Yeni Komşu", 15, FontStyle.Bold, AccentGold, TextAnchor.MiddleCenter);
            StretchFull(_rankBadgeText.rectTransform, 10f, 0f);

            GameObject xpTrack = CreateRoundedPanel("RankXpTrack", profileCard.transform, _pillSprite,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(-48f, 8f), new Color(1f, 1f, 1f, 0.11f), addBorder: false);
            GameObject xpFillGo = CreateRoundedPanel("Fill", xpTrack.transform, _pillSprite,
                Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
                Vector2.zero, Vector2.zero, AccentGold, addBorder: false);
            StretchFull((RectTransform)xpFillGo.transform, 0f, 0f);
            _rankProgressFill = xpFillGo.GetComponent<UnityEngine.UI.Image>();
            _rankProgressFill.type = UnityEngine.UI.Image.Type.Filled;
            _rankProgressFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            _rankProgressFill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            _rankProgressFill.fillAmount = 0.15f;

            GameObject gridGo = new GameObject("StatGrid", typeof(RectTransform));
            gridGo.transform.SetParent(profileCard.transform, false);
            RectTransform grt = (RectTransform)gridGo.transform;
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(22f, 20f);
            grt.offsetMax = new Vector2(-22f, -80f);

            GridLayoutGroup glg = gridGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(244f, 62f);
            glg.spacing = new Vector2(16f, 12f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            (_statCashLabel, _statCashValue) = CreateStatTile(gridGo.transform, "Stat_Cash", "NAKİT", "₺ 250", AccentMint);
            (_statMissionsLabel, _statMissionsValue) = CreateStatTile(gridGo.transform, "Stat_Missions", "GÖREV", "0 / 13", AccentTeal);
            (_statLandmarksLabel, _statLandmarksValue) = CreateStatTile(gridGo.transform, "Stat_Landmarks", "KEŞİF", "0 / 10", AccentSky);
            (_statMemoriesLabel, _statMemoriesValue) = CreateStatTile(gridGo.transform, "Stat_Memories", "HATIRA", "0 / 12", AccentGold);
        }

        private (UnityEngine.UI.Text cap, UnityEngine.UI.Text val) CreateStatTile(
            Transform parent, string name, string caption, string value, Color valueColor)
        {
            GameObject tile = CreateRoundedPanel(name, parent, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, CardSurface, addBorder: true);

            UnityEngine.UI.Text cap = CreateText("Caption", tile.transform, caption, 15, FontStyle.Bold, TextMuted, TextAnchor.MiddleLeft);
            PlaceTopLeft(cap.rectTransform, 16f, -7f, 212f, 20f);

            UnityEngine.UI.Text val = CreateText("Value", tile.transform, value, 24, FontStyle.Bold, valueColor, TextAnchor.MiddleLeft);
            PlaceTopLeft(val.rectTransform, 16f, -29f, 212f, 28f);

            return (cap, val);
        }

        private void CreateCompactIconButton(string name, Transform parent, string glyph, Vector2 leftCenterPos, Action onClick)
        {
            GameObject go = CreateRoundedPanel(name, parent, _pillSprite,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                leftCenterPos, new Vector2(44f, 44f), CardSurface, addBorder: true);
            UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = true;
            UnityEngine.UI.Button btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiButtonFeedback fb = go.AddComponent<UiButtonFeedback>();
            fb.Configure(btn, img);

            UnityEngine.UI.Text txt = CreateText("Glyph", go.transform, glyph, 24, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter);
            StretchFull(txt.rectTransform, 0f, 0f);
        }

    }
}
