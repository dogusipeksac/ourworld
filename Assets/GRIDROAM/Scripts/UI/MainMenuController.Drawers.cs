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
        // INLINE OPEN-WORLD DRAWERS (DISTRICTS + SETTINGS)
        // =====================================================================

        private void BuildDrawerHost(Transform parent)
        {
            _drawerHost = CreateRoundedPanel("RightDrawerHost", parent, _roundedCardSprite,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-48f, 0f), new Vector2(724f, 780f), PanelDark, addBorder: true);
            AddTopAccentStrip(_drawerHost.transform, AccentTeal);
            _drawerGroup = _drawerHost.AddComponent<CanvasGroup>();

            BuildDistrictsDrawer(_drawerHost.transform);
            BuildSportsDrawer(_drawerHost.transform);
            BuildSettingsDrawer(_drawerHost.transform);
            _drawerHost.SetActive(false);
        }

        private void BuildSportsDrawer(Transform parent)
        {
            _sportsPanel = new GameObject("SportsPanel", typeof(RectTransform));
            _sportsPanel.transform.SetParent(parent, false);
            StretchFull((RectTransform)_sportsPanel.transform, 0f, 0f);

            _sportsHeaderText = CreateText("Header", _sportsPanel.transform, "SPOR & MİNİ OYUNLAR", 28, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_sportsHeaderText.rectTransform, 32f, -26f, 560f, 38f);

            _sportsHintText = CreateText("Hint", _sportsPanel.transform, "İstediğin zaman bir spor seç, canlı sahayı gör ve hemen maça başla!", 17, FontStyle.Normal, TextSecondary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_sportsHintText.rectTransform, 32f, -68f, 640f, 28f);

            CreateDrawerCloseButton(_sportsPanel.transform);

            GameObject gridGo = new GameObject("SportsGrid", typeof(RectTransform));
            gridGo.transform.SetParent(_sportsPanel.transform, false);
            RectTransform grt = (RectTransform)gridGo.transform;
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(30f, 118f);
            grt.offsetMax = new Vector2(-30f, -118f);

            GridLayoutGroup glg = gridGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(322f, 168f);
            glg.spacing = new Vector2(18f, 14f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            for (int i = 0; i < QuickSports.Length; i++)
            {
                int idx = i;
                SportQuickOption opt = QuickSports[i];
                GameObject card = CreateRoundedPanel($"SportCard_{i}", gridGo.transform, _roundedCardSprite,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, CardSurface, addBorder: false);
                UnityEngine.UI.Image bg = card.GetComponent<UnityEngine.UI.Image>();
                bg.raycastTarget = true;

                UnityEngine.UI.Image border = AddOutlineOverlay(card.transform, BorderSubtle);
                UnityEngine.UI.Button btn = card.AddComponent<UnityEngine.UI.Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectSportCard(idx));

                GameObject badgeGo = CreateRoundedPanel("RewardBadge", card.transform, _pillSprite,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(16f, -14f), new Vector2(196f, 32f),
                    BadgeFoundBg, addBorder: false);
                UnityEngine.UI.Text badgeLbl = CreateText("BadgeLabel", badgeGo.transform, opt.BadgeTr, 16, FontStyle.Bold, AccentMint, TextAnchor.MiddleCenter);
                StretchFull(badgeLbl.rectTransform, 6f, 0f);

                UnityEngine.UI.Text title = CreateText("Title", card.transform, opt.NameTr, 22, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
                PlaceTopLeft(title.rectTransform, 16f, -54f, 290f, 32f);

                UnityEngine.UI.Text tag = CreateText("Tag", card.transform, opt.TagTr, 15, FontStyle.Normal, TextSecondary, TextAnchor.UpperLeft);
                PlaceTopLeft(tag.rectTransform, 16f, -92f, 290f, 68f);
                tag.horizontalOverflow = HorizontalWrapMode.Wrap;
                tag.lineSpacing = 1.20f;

                _sportCardBgs.Add(bg);
                _sportCardBorders.Add(border);
                _sportTitleTexts.Add(title);
                _sportTagTexts.Add(tag);
                _sportBadgeTexts.Add(badgeLbl);
            }

            GameObject playBtnGo = CreateRoundedPanel("Btn_StartSelectedSport", _sportsPanel.transform, _roundedCardSprite,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 26f), new Vector2(-60f, 68f), AccentTeal, addBorder: false);
            AddOutlineOverlay(playBtnGo.transform, new Color(0.72f, 0.99f, 0.95f, 0.50f));
            UnityEngine.UI.Image sImg = playBtnGo.GetComponent<UnityEngine.UI.Image>();
            sImg.raycastTarget = true;
            _startSportBtn = playBtnGo.AddComponent<UnityEngine.UI.Button>();
            _startSportBtn.targetGraphic = sImg;
            _startSportBtn.onClick.AddListener(() => EnterWorldAndStartSport(_selectedSportIndex));
            UiButtonFeedback sFb = playBtnGo.AddComponent<UiButtonFeedback>();
            sFb.Configure(_startSportBtn, sImg);

            _startSportBtnLabel = CreateText("Label", playBtnGo.transform, "▶   MAÇA BAŞLA", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, addShadow: true);
            StretchFull(_startSportBtnLabel.rectTransform, 16f, 0f);
        }

        private void BuildDistrictsDrawer(Transform parent)
        {
            _districtsPanel = new GameObject("DistrictsPanel", typeof(RectTransform));
            _districtsPanel.transform.SetParent(parent, false);
            StretchFull((RectTransform)_districtsPanel.transform, 0f, 0f);

            _districtsHeaderText = CreateText("Header", _districtsPanel.transform, "BAŞLANGIÇ BÖLGESİ", 28, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_districtsHeaderText.rectTransform, 32f, -26f, 560f, 38f);

            _districtsHintText = CreateText("Hint", _districtsPanel.transform, "Bir mahalleye dokun, canlı kameradan incele ve oradan başla.", 17, FontStyle.Normal, TextSecondary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_districtsHintText.rectTransform, 32f, -68f, 640f, 28f);

            CreateDrawerCloseButton(_districtsPanel.transform);

            GameObject gridGo = new GameObject("DistrictGrid", typeof(RectTransform));
            gridGo.transform.SetParent(_districtsPanel.transform, false);
            RectTransform grt = (RectTransform)gridGo.transform;
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(30f, 114f);
            grt.offsetMax = new Vector2(-30f, -112f);

            GridLayoutGroup glg = gridGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(322f, 128f);
            glg.spacing = new Vector2(20f, 12f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            for (int i = 0; i < Districts.Length; i++)
            {
                int idx = i;
                GameObject card = CreateRoundedPanel($"DistrictCard_{i}", gridGo.transform, _roundedCardSprite,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, CardSurface, addBorder: false);
                UnityEngine.UI.Image bg = card.GetComponent<UnityEngine.UI.Image>();
                bg.raycastTarget = true;

                UnityEngine.UI.Image border = AddOutlineOverlay(card.transform, BorderSubtle);

                UnityEngine.UI.Button btn = card.AddComponent<UnityEngine.UI.Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectDistrictCard(idx));

                GameObject badgeGo = CreateRoundedPanel("StatusBadge", card.transform, _pillSprite,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(16f, -12f), new Vector2(142f, 26f),
                    BadgeExploreBg, addBorder: false);
                UnityEngine.UI.Text badgeLbl = CreateText("BadgeLabel", badgeGo.transform, "•  KEŞFET", 14, FontStyle.Bold, AccentGold, TextAnchor.MiddleCenter);
                StretchFull(badgeLbl.rectTransform, 6f, 0f);

                UnityEngine.UI.Text title = CreateText("Title", card.transform, Districts[i].NameTr, 20, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
                PlaceTopLeft(title.rectTransform, 16f, -44f, 290f, 28f);

                UnityEngine.UI.Text tag = CreateText("Tag", card.transform, Districts[i].TagTr, 14, FontStyle.Normal, TextSecondary, TextAnchor.UpperLeft);
                PlaceTopLeft(tag.rectTransform, 16f, -75f, 290f, 44f);
                tag.horizontalOverflow = HorizontalWrapMode.Wrap;

                _districtCardBgs.Add(bg);
                _districtCardBorders.Add(border);
                _districtTitleTexts.Add(title);
                _districtTagTexts.Add(tag);
                _districtBadgeTexts.Add(badgeLbl);
                _districtBadgeBgs.Add(badgeGo.GetComponent<UnityEngine.UI.Image>());
            }

            GameObject spawnBtnGo = CreateRoundedPanel("Btn_SpawnSelectedDistrict", _districtsPanel.transform, _roundedCardSprite,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 26f), new Vector2(-60f, 68f), AccentTeal, addBorder: false);
            AddOutlineOverlay(spawnBtnGo.transform, new Color(0.72f, 0.99f, 0.95f, 0.50f));
            UnityEngine.UI.Image sImg = spawnBtnGo.GetComponent<UnityEngine.UI.Image>();
            sImg.raycastTarget = true;
            _spawnDistrictBtn = spawnBtnGo.AddComponent<UnityEngine.UI.Button>();
            _spawnDistrictBtn.targetGraphic = sImg;
            _spawnDistrictBtn.onClick.AddListener(() => EnterWorldAtDistrict(_selectedDistrictIndex));
            UiButtonFeedback sFb = spawnBtnGo.AddComponent<UiButtonFeedback>();
            sFb.Configure(_spawnDistrictBtn, sImg);

            _spawnDistrictBtnLabel = CreateText("Label", spawnBtnGo.transform, "▶   BURADA BAŞLA", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, addShadow: true);
            StretchFull(_spawnDistrictBtnLabel.rectTransform, 16f, 0f);
        }

        private void BuildSettingsDrawer(Transform parent)
        {
            _settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform));
            _settingsPanel.transform.SetParent(parent, false);
            StretchFull((RectTransform)_settingsPanel.transform, 0f, 0f);

            _settingsHeaderText = CreateText("Header", _settingsPanel.transform, "AYARLAR & ATMOSFER", 28, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_settingsHeaderText.rectTransform, 32f, -26f, 560f, 38f);

            CreateDrawerCloseButton(_settingsPanel.transform);

            // Smooth vertical ScrollRect so all 10 settings rows fit comfortably on every phone & tablet screen
            GameObject scrollGo = new GameObject("SettingsScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(_settingsPanel.transform, false);
            RectTransform scRt = (RectTransform)scrollGo.transform;
            scRt.anchorMin = Vector2.zero;
            scRt.anchorMax = Vector2.one;
            scRt.offsetMin = new Vector2(26f, 22f);
            scRt.offsetMax = new Vector2(-26f, -84f);
            UnityEngine.UI.Image scImg = scrollGo.AddComponent<UnityEngine.UI.Image>();
            scImg.color = new Color(0f, 0f, 0f, 0f);
            scImg.raycastTarget = true;

            ScrollRect sr = scrollGo.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 36f;

            GameObject vpGo = new GameObject("Viewport", typeof(RectTransform));
            vpGo.transform.SetParent(scrollGo.transform, false);
            StretchFull((RectTransform)vpGo.transform, 0f, 0f);
            vpGo.AddComponent<RectMask2D>();
            sr.viewport = (RectTransform)vpGo.transform;

            GameObject listGo = new GameObject("SettingsList", typeof(RectTransform));
            listGo.transform.SetParent(vpGo.transform, false);
            RectTransform lrt = (RectTransform)listGo.transform;
            lrt.anchorMin = new Vector2(0f, 1f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(0f, 800f);
            sr.content = lrt;

            VerticalLayoutGroup vlg = listGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(4, 4, 2, 12);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            ContentSizeFitter csf = listGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 1. Graphics Quality Row
            GameObject gfxRow = CreateSettingRow(listGo.transform, "Row_Graphics", 66f, out _setGfxCaption, "Grafik Kalitesi");
            GameObject gfxSegs = CreateRightSegmentContainer(gfxRow.transform, 290f);
            (_gfxLowBg, _gfxLowText) = CreateSegmentButton(gfxSegs.transform, "Btn_GfxLow", "Düşük", () =>
            {
                GameFeelAudio.PlayUiClick();
                if (GraphicsProfileManager.Instance != null) GraphicsProfileManager.Instance.ApplyTier(GraphicsTier.Low, persist: true);
                RefreshSettingsUiState();
            });
            (_gfxMedBg, _gfxMedText) = CreateSegmentButton(gfxSegs.transform, "Btn_GfxMed", "Orta", () =>
            {
                GameFeelAudio.PlayUiClick();
                if (GraphicsProfileManager.Instance != null) GraphicsProfileManager.Instance.ApplyTier(GraphicsTier.Medium, persist: true);
                RefreshSettingsUiState();
            });

            // 2. Language Row
            GameObject langRow = CreateSettingRow(listGo.transform, "Row_Language", 66f, out _setLangCaption, "Dil");
            GameObject langSegs = CreateRightSegmentContainer(langRow.transform, 290f);
            (_langTrBg, _) = CreateSegmentButton(langSegs.transform, "Btn_LangTR", "Türkçe", () => ApplyLanguage("tr"));
            (_langEnBg, _) = CreateSegmentButton(langSegs.transform, "Btn_LangEN", "English", () => ApplyLanguage("en"));

            // 3. Audio Volume Row
            GameObject volRow = CreateSettingRow(listGo.transform, "Row_Audio", 66f, out _setAudioCaption, "Ses Seviyesi");
            BuildVolumeSlider(volRow.transform);

            // 4. Voiceover Row
            GameObject voiceRow = CreateSettingRow(listGo.transform, "Row_Voiceover", 66f, out _setVoiceCaption, "Seslendirme");
            GameObject voiceSegs = CreateRightSegmentContainer(voiceRow.transform, 250f);
            (_voiceOffBg, _voiceOffText) = CreateSegmentButton(voiceSegs.transform, "Btn_VoiceOff", "Kapalı", () => SetVoiceoverEnabled(false));
            (_voiceOnBg, _voiceOnText) = CreateSegmentButton(voiceSegs.transform, "Btn_VoiceOn", "Açık", () => SetVoiceoverEnabled(true));

            // 5. Camera Sensitivity Row
            GameObject sensRow = CreateSettingRow(listGo.transform, "Row_Sensitivity", 66f, out _setSensCaption, "Kamera Hassasiyeti");
            GameObject sensSegs = CreateRightSegmentContainer(sensRow.transform, 356f);
            (_sensLowBg, _sensLowText) = CreateSegmentButton(sensSegs.transform, "Btn_SensLow", "Yavaş", () => ApplySensitivity(0.70f));
            (_sensMidBg, _sensMidText) = CreateSegmentButton(sensSegs.transform, "Btn_SensMid", "Normal", () => ApplySensitivity(1.00f));
            (_sensHighBg, _sensHighText) = CreateSegmentButton(sensSegs.transform, "Btn_SensHigh", "Hızlı", () => ApplySensitivity(1.40f));

            // 6. Camera Y Axis (Invert Y) Row
            GameObject invYRow = CreateSettingRow(listGo.transform, "Row_InvertY", 66f, out _setInvertYCaption, "Kamera Y Ekseni");
            GameObject invYSegs = CreateRightSegmentContainer(invYRow.transform, 250f);
            (_invertYOffBg, _invertYOffText) = CreateSegmentButton(invYSegs.transform, "Btn_InvertYOff", "Normal", () => SetInvertY(false));
            (_invertYOnBg, _invertYOnText) = CreateSegmentButton(invYSegs.transform, "Btn_InvertYOn", "Ters", () => SetInvertY(true));

            // 7. Subtitle Size Row
            GameObject subRow = CreateSettingRow(listGo.transform, "Row_Subtitles", 66f, out _setSubtitleCaption, "Altyazı Boyutu");
            GameObject subSegs = CreateRightSegmentContainer(subRow.transform, 250f);
            (_subNormBg, _subNormText) = CreateSegmentButton(subSegs.transform, "Btn_SubNorm", "Normal", () => SetLargeSubtitles(false));
            (_subLargeBg, _subLargeText) = CreateSegmentButton(subSegs.transform, "Btn_SubLarge", "Büyük", () => SetLargeSubtitles(true));

            // 6. Time of Day Live Preview Row
            GameObject timeRow = CreateRoundedPanel("Row_TimeOfDay", listGo.transform, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, 102f), CardSurface, addBorder: true);
            timeRow.AddComponent<LayoutElement>().preferredHeight = 102f;

            _setTimeCaption = CreateText("Caption", timeRow.transform, "Günün Saati (Canlı Önizleme)", 18, FontStyle.Bold, TextSecondary, TextAnchor.MiddleLeft);
            PlaceTopLeft(_setTimeCaption.rectTransform, 20f, -10f, 520f, 26f);

            GameObject timeGrid = new GameObject("TimeChips", typeof(RectTransform));
            timeGrid.transform.SetParent(timeRow.transform, false);
            RectTransform trt = (RectTransform)timeGrid.transform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 0f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.anchoredPosition = new Vector2(0f, 10f);
            trt.sizeDelta = new Vector2(-40f, 44f);

            HorizontalLayoutGroup thlg = timeGrid.AddComponent<HorizontalLayoutGroup>();
            thlg.spacing = 10f;
            thlg.childControlWidth = true;
            thlg.childControlHeight = true;
            thlg.childForceExpandWidth = true;
            thlg.childForceExpandHeight = true;

            string[] defaultTimeLabels = { "Sabah", "Öğle", "Akşam", "Gece" };
            for (int i = 0; i < 4; i++)
            {
                float presetHour = TimePresetHours[i];
                (_timeChipBgs[i], _timeChipTexts[i]) = CreateSegmentButton(
                    timeGrid.transform, $"Btn_Time_{i}", defaultTimeLabels[i],
                    () => SetPreviewTimePreset(presetHour));
            }

            // 7. FPS Telemetry Row
            GameObject fpsRow = CreateSettingRow(listGo.transform, "Row_FPS", 64f, out _setFpsCaption, "FPS Göstergesi");
            GameObject fpsSegs = CreateRightSegmentContainer(fpsRow.transform, 250f);
            (_fpsOffBg, _fpsOffText) = CreateSegmentButton(fpsSegs.transform, "Btn_FpsOff", "Kapalı", () => SetTelemetry(false));
            (_fpsOnBg, _fpsOnText) = CreateSegmentButton(fpsSegs.transform, "Btn_FpsOn", "Açık", () => SetTelemetry(true));

            // 8. Safe 2-Step Reset Save / New Game Row
            GameObject resetContainer = new GameObject("Row_ResetSave", typeof(RectTransform));
            resetContainer.transform.SetParent(listGo.transform, false);
            resetContainer.AddComponent<LayoutElement>().preferredHeight = 58f;

            _resetInitialRow = CreateRoundedPanel("Btn_ResetInitial", resetContainer.transform, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, DangerSoft, addBorder: false);
            StretchFull((RectTransform)_resetInitialRow.transform, 0f, 0f);
            AddOutlineOverlay(_resetInitialRow.transform, new Color(0.90f, 0.38f, 0.36f, 0.52f));
            UnityEngine.UI.Image riImg = _resetInitialRow.GetComponent<UnityEngine.UI.Image>();
            riImg.raycastTarget = true;
            UnityEngine.UI.Button riBtn = _resetInitialRow.AddComponent<UnityEngine.UI.Button>();
            riBtn.targetGraphic = riImg;
            riBtn.onClick.AddListener(() =>
            {
                GameFeelAudio.PlayUiClick();
                ResetConfirmRowVisibility(confirming: true);
            });
            _resetInitialLabel = CreateText("Label", _resetInitialRow.transform, "Yeni Oyun (Kaydı Sıfırla)", 19, FontStyle.Bold, new Color(1f, 0.82f, 0.82f, 1f), TextAnchor.MiddleCenter);
            StretchFull(_resetInitialLabel.rectTransform, 12f, 0f);

            _resetConfirmRow = new GameObject("ResetConfirmRow", typeof(RectTransform));
            _resetConfirmRow.transform.SetParent(resetContainer.transform, false);
            StretchFull((RectTransform)_resetConfirmRow.transform, 0f, 0f);
            HorizontalLayoutGroup rhlg = _resetConfirmRow.AddComponent<HorizontalLayoutGroup>();
            rhlg.spacing = 12f;
            rhlg.childControlWidth = true;
            rhlg.childControlHeight = true;
            rhlg.childForceExpandWidth = true;
            rhlg.childForceExpandHeight = true;

            GameObject confirmBtnGo = CreateRoundedPanel("Btn_ConfirmReset", _resetConfirmRow.transform, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, DangerRed, addBorder: false);
            UnityEngine.UI.Image cImg = confirmBtnGo.GetComponent<UnityEngine.UI.Image>();
            cImg.raycastTarget = true;
            UnityEngine.UI.Button cBtn = confirmBtnGo.AddComponent<UnityEngine.UI.Button>();
            cBtn.targetGraphic = cImg;
            cBtn.onClick.AddListener(HandleConfirmResetSave);
            _resetConfirmLabel = CreateText("Label", confirmBtnGo.transform, "EMİN MİSİN? SIFIRLA", 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(_resetConfirmLabel.rectTransform, 8f, 0f);

            GameObject cancelBtnGo = CreateRoundedPanel("Btn_CancelReset", _resetConfirmRow.transform, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, SegInactive, addBorder: true);
            UnityEngine.UI.Image xImg = cancelBtnGo.GetComponent<UnityEngine.UI.Image>();
            xImg.raycastTarget = true;
            UnityEngine.UI.Button xBtn = cancelBtnGo.AddComponent<UnityEngine.UI.Button>();
            xBtn.targetGraphic = xImg;
            xBtn.onClick.AddListener(() =>
            {
                GameFeelAudio.PlayUiClick();
                ResetConfirmRowVisibility(confirming: false);
            });
            _resetCancelLabel = CreateText("Label", cancelBtnGo.transform, "Vazgeç", 19, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter);
            StretchFull(_resetCancelLabel.rectTransform, 8f, 0f);

            _resetConfirmRow.SetActive(false);
        }

        private GameObject CreateSettingRow(Transform parent, string name, float height, out UnityEngine.UI.Text captionOut, string defaultCaption)
        {
            GameObject row = CreateRoundedPanel(name, parent, _roundedCardSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, height), CardSurface, addBorder: true);
            row.AddComponent<LayoutElement>().preferredHeight = height;

            captionOut = CreateText("Caption", row.transform, defaultCaption, 19, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            RectTransform crt = captionOut.rectTransform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(0.48f, 1f);
            crt.offsetMin = new Vector2(20f, 0f);
            crt.offsetMax = Vector2.zero;
            return row;
        }

        private GameObject CreateRightSegmentContainer(Transform rowParent, float width)
        {
            GameObject seg = new GameObject("Segments", typeof(RectTransform));
            seg.transform.SetParent(rowParent, false);
            RectTransform srt = (RectTransform)seg.transform;
            srt.anchorMin = new Vector2(1f, 0.5f);
            srt.anchorMax = new Vector2(1f, 0.5f);
            srt.pivot = new Vector2(1f, 0.5f);
            srt.anchoredPosition = new Vector2(-18f, 0f);
            srt.sizeDelta = new Vector2(width, 46f);

            HorizontalLayoutGroup hlg = seg.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            return seg;
        }

        private (UnityEngine.UI.Image bg, UnityEngine.UI.Text lbl) CreateSegmentButton(
            Transform parent, string name, string label, Action onClick)
        {
            GameObject go = CreateRoundedPanel(name, parent, _pillSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, SegInactive, addBorder: false);
            UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = true;
            UnityEngine.UI.Button btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            UnityEngine.UI.Text txt = CreateText("Label", go.transform, label, 17, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter);
            StretchFull(txt.rectTransform, 6f, 0f);
            return (img, txt);
        }

        private void BuildVolumeSlider(Transform rowParent)
        {
            GameObject sliderGo = new GameObject("VolumeSlider", typeof(RectTransform));
            sliderGo.transform.SetParent(rowParent, false);
            RectTransform srt = (RectTransform)sliderGo.transform;
            srt.anchorMin = new Vector2(1f, 0.5f);
            srt.anchorMax = new Vector2(1f, 0.5f);
            srt.pivot = new Vector2(1f, 0.5f);
            srt.anchoredPosition = new Vector2(-92f, 0f);
            srt.sizeDelta = new Vector2(236f, 36f);

            // Track background
            CreateRoundedPanel("Background", sliderGo.transform, _pillSprite,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, 10f), new Color(1f, 1f, 1f, 0.16f), addBorder: false);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform fart = (RectTransform)fillArea.transform;
            fart.anchorMin = new Vector2(0f, 0.5f);
            fart.anchorMax = new Vector2(1f, 0.5f);
            fart.offsetMin = new Vector2(0f, -5f);
            fart.offsetMax = new Vector2(0f, 5f);

            GameObject fillGo = CreateRoundedPanel("Fill", fillArea.transform, _pillSprite,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, AccentTeal, addBorder: false);
            StretchFull((RectTransform)fillGo.transform, 0f, 0f);

            // Constrain Handle Slide Area height strictly to 26px so Unity Slider's vertical stretch produces a crisp 26x26 circle
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            RectTransform hart = (RectTransform)handleArea.transform;
            hart.anchorMin = new Vector2(0f, 0.5f);
            hart.anchorMax = new Vector2(1f, 0.5f);
            hart.pivot = new Vector2(0.5f, 0.5f);
            hart.anchoredPosition = Vector2.zero;
            hart.sizeDelta = new Vector2(-24f, 26f);

            GameObject handleGo = CreateRoundedPanel("Handle", handleArea.transform, _pillSprite,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(26f, 0f), Color.white, addBorder: false);
            UnityEngine.UI.Image handleImg = handleGo.GetComponent<UnityEngine.UI.Image>();
            handleImg.raycastTarget = true;

            _volumeSlider = sliderGo.AddComponent<UnityEngine.UI.Slider>();
            _volumeSlider.minValue = 0f;
            _volumeSlider.maxValue = 1f;
            _volumeSlider.fillRect = (RectTransform)fillGo.transform;
            _volumeSlider.handleRect = (RectTransform)handleGo.transform;
            _volumeSlider.targetGraphic = handleImg;
            _volumeSlider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            _volumeSlider.SetValueWithoutNotify(AudioListener.volume);
            _volumeSlider.onValueChanged.AddListener(HandleVolumeSliderChanged);

            _volumeValueText = CreateText("VolumePct", rowParent, $"{Mathf.RoundToInt(AudioListener.volume * 100f)}%", 18, FontStyle.Bold, AccentTeal, TextAnchor.MiddleRight);
            RectTransform vrt = _volumeValueText.rectTransform;
            vrt.anchorMin = new Vector2(1f, 0.5f);
            vrt.anchorMax = new Vector2(1f, 0.5f);
            vrt.pivot = new Vector2(1f, 0.5f);
            vrt.anchoredPosition = new Vector2(-20f, 0f);
            vrt.sizeDelta = new Vector2(64f, 32f);
        }

        private void CreateDrawerCloseButton(Transform drawerRoot)
        {
            GameObject closeGo = CreateRoundedPanel("Btn_CloseDrawer", drawerRoot, _pillSprite,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -22f), new Vector2(44f, 44f), CardSurface, addBorder: true);
            UnityEngine.UI.Image img = closeGo.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = true;
            UnityEngine.UI.Button btn = closeGo.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OpenDrawer(MenuDrawer.None));
            UiButtonFeedback fb = closeGo.AddComponent<UiButtonFeedback>();
            fb.Configure(btn, img);

            UnityEngine.UI.Text xTxt = CreateText("X", closeGo.transform, "✕", 20, FontStyle.Bold, TextSecondary, TextAnchor.MiddleCenter);
            StretchFull(xTxt.rectTransform, 0f, 0f);
        }

    }
}
