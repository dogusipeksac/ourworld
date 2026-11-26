using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Jobs;
using OurWorld.Missions;
using OurWorld.Player;
using OurWorld.Vehicle;
using OurWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    public sealed partial class MainMenuController
    {
        // =====================================================================
        // INTERACTION HANDLERS & STATE REFRESH
        // =====================================================================

        private void SelectSportCard(int index)
        {
            if (index < 0 || index >= QuickSports.Length) return;
            GameFeelAudio.PlayUiClick();
            _selectedSportIndex = index;
            RefreshSportCards();
            PreviewSelectedSport(snapCamera: true);
        }

        private void PreviewSelectedSport(bool snapCamera)
        {
            if (_selectedSportIndex < 0 || _selectedSportIndex >= QuickSports.Length) return;
            SportQuickOption opt = QuickSports[_selectedSportIndex];
            _usingCustomPreview = true;
            _customPreviewPos = opt.PreviewCamPos;
            _customPreviewLookAt = opt.PreviewLookAt;
            _customPreviewNameTr = opt.NameTr;
            _customPreviewNameEn = opt.NameEn;
            UpdateVistaLabel();
            if (snapCamera) SnapCameraToActiveVista();
        }

        private void SelectDistrictCard(int index)
        {
            if (index < 0 || index >= Districts.Length) return;
            GameFeelAudio.PlayUiClick();
            _selectedDistrictIndex = index;
            RefreshDistrictCards();
            PreviewSelectedDistrict(snapCamera: true);
        }

        private void PreviewSelectedDistrict(bool snapCamera)
        {
            if (_selectedDistrictIndex < 0 || _selectedDistrictIndex >= Districts.Length) return;
            DistrictOption opt = Districts[_selectedDistrictIndex];
            _usingCustomPreview = true;
            _customPreviewPos = opt.PreviewCamPos;
            _customPreviewLookAt = opt.PreviewLookAt;
            _customPreviewNameTr = opt.NameTr;
            _customPreviewNameEn = opt.NameEn;
            UpdateVistaLabel();
            if (snapCamera) SnapCameraToActiveVista();
        }

        private void ApplyLanguage(string langCode)
        {
            GameFeelAudio.PlayUiClick();
            if (LocalizationService.Instance != null && LocalizationService.Instance.LoadLanguage(langCode))
            {
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.SetLanguageCode(langCode);
                }
            }
            RefreshLocalizedText();
            RefreshSettingsUiState();
            RefreshDistrictCards();
            RefreshWorldSnapshot();
        }

        private void HandleVolumeSliderChanged(float value)
        {
            float clamped = Mathf.Clamp01(value);
            AudioListener.volume = clamped;
            PlayerPrefs.SetFloat(PrefMasterVolumeKey, clamped);
            if (_volumeValueText != null)
            {
                _volumeValueText.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
            }
        }

        private void HandleMusicSliderChanged(float value)
        {
            float clamped = Mathf.Clamp01(value);
            TurkishCarRadio.SetMusicVolume(clamped);
            if (_musicValueText != null)
            {
                _musicValueText.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
            }
        }

        private void ApplySensitivity(float mult)
        {
            GameFeelAudio.PlayUiClick();
            if (InputHub.Instance != null)
            {
                InputHub.Instance.SetSensitivityMultiplier(mult);
            }
            RefreshSettingsUiState();
        }

        private void SetVoiceoverEnabled(bool enabled)
        {
            GameFeelAudio.PlayUiClick();
            VoiceoverManager.Enabled = enabled;
            RefreshSettingsUiState();
        }

        private void SetInvertY(bool inverted)
        {
            GameFeelAudio.PlayUiClick();
            if (InputHub.Instance != null)
            {
                InputHub.Instance.SetInvertY(inverted);
            }
            RefreshSettingsUiState();
        }

        private void SetLargeSubtitles(bool large)
        {
            GameFeelAudio.PlayUiClick();
            if (_hud != null)
            {
                _hud.SetLargeSubtitles(large);
            }
            RefreshSettingsUiState();
        }

        private void SetTelemetry(bool visible)
        {
            GameFeelAudio.PlayUiClick();
            if (_hud != null)
            {
                _hud.SetTelemetryVisible(visible);
            }
            RefreshSettingsUiState();
        }

        private void HandleConfirmResetSave()
        {
            GameFeelAudio.PlayUiClick();
            if (JobManager.Instance != null && JobManager.Instance.HasActiveJob)
            {
                JobManager.Instance.CancelActiveJob(silent: true);
            }
            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.ResetProgressAndRestart();
            }
            else if (SaveManager.Instance != null)
            {
                SaveManager.Instance.ResetSave();
            }

            // Immediately sync live character cosmetics, personal sedan tuning & audio ambience to fresh save state
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.FootController?.ApplySavedAppearance();
                if (PlayerModeController.Instance.PlayerSedan != null && SaveManager.Instance != null && SaveManager.Instance.Data != null)
                {
                    PlayerModeController.Instance.PlayerSedan.ApplyTuningFromSave(SaveManager.Instance.Data);
                }
            }
            TurkishCarRadio.SetMallAmbienceActive(false);

            IsPauseMode = false;
            ResetConfirmRowVisibility(confirming: false);
            OpenDrawer(MenuDrawer.None);
            RefreshLocalizedText();
            RefreshWorldSnapshot();
            RefreshDistrictCards();
        }

        private void HandleOpenWhatsNew()
        {
            GameFeelAudio.PlayUiClick();
            if (ReleaseNotesService.Instance != null)
            {
                ReleaseNotesService.Instance.ShowNow(markSeen: false);
            }
        }

        private void HandleOpenTownGuide()
        {
            if (!IsOpen || IsTransitioning) return;
            GameFeelAudio.PlayUiClick();
            StartEnterWorldTransition(teleportToDistrictIndex: -1, openGuideAfter: true);
        }

        private void ResetConfirmRowVisibility(bool confirming)
        {
            if (_resetInitialRow != null) _resetInitialRow.SetActive(!confirming);
            if (_resetConfirmRow != null) _resetConfirmRow.SetActive(confirming);
        }

        private void ApplyDrawerVisibilityImmediate(MenuDrawer drawer)
        {
            bool showDrawer = drawer != MenuDrawer.None;
            _districtsPanel.SetActive(drawer == MenuDrawer.Districts);
            if (_sportsPanel != null) _sportsPanel.SetActive(drawer == MenuDrawer.Sports);
            _settingsPanel.SetActive(drawer == MenuDrawer.Settings);
            _drawerHost.SetActive(showDrawer);
            _drawerGroup.alpha = showDrawer ? 1f : 0f;
            ((RectTransform)_drawerHost.transform).anchoredPosition = new Vector2(-48f, 0f);
            if (_rightInfoRoot != null)
            {
                _rightInfoRoot.SetActive(!showDrawer);
                if (_rightInfoGroup != null) _rightInfoGroup.alpha = showDrawer ? 0f : 1f;
            }
        }

        private IEnumerator AnimateDrawerRoutine(MenuDrawer drawer)
        {
            bool showDrawer = drawer != MenuDrawer.None;
            if (showDrawer)
            {
                _districtsPanel.SetActive(drawer == MenuDrawer.Districts);
                if (_sportsPanel != null) _sportsPanel.SetActive(drawer == MenuDrawer.Sports);
                _settingsPanel.SetActive(drawer == MenuDrawer.Settings);
                _drawerHost.SetActive(true);
                _drawerGroup.alpha = 0f;
            }
            else if (_rightInfoRoot != null)
            {
                _rightInfoRoot.SetActive(true);
            }

            RectTransform drt = (RectTransform)_drawerHost.transform;
            const float duration = 0.20f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                if (showDrawer)
                {
                    _drawerGroup.alpha = k;
                    drt.anchoredPosition = new Vector2(Mathf.Lerp(-16f, -48f, k), 0f);
                    if (_rightInfoGroup != null) _rightInfoGroup.alpha = 1f - k;
                }
                else
                {
                    _drawerGroup.alpha = 1f - k;
                    drt.anchoredPosition = new Vector2(Mathf.Lerp(-48f, -16f, k), 0f);
                    if (_rightInfoGroup != null) _rightInfoGroup.alpha = k;
                }
                yield return null;
            }

            ApplyDrawerVisibilityImmediate(drawer);
            _drawerAnimRoutine = null;
        }

        public void RefreshLocalizedText()
        {
            if (_eyebrowText != null) _eyebrowText.text = Tr("ui.menu.eyebrow", "İSTANBUL  ·  SULTANBEYLİ  ·  HAMİDİYE");
            if (_brandTitleText != null) _brandTitleText.text = Tr("ui.menu.title", "BİZİM DÜNYAMIZ");
            if (_brandSubtitleText != null) _brandSubtitleText.text = Tr("ui.menu.subtitle", "Yaşayan Açık Dünya · Mahalle Yaşamı");
            if (_pauseBadgeText != null) _pauseBadgeText.text = $"●  {Tr("ui.menu.pause_badge", "DURAKLATILDI")}";
            if (_pauseBadgeObj != null) _pauseBadgeObj.SetActive(IsPauseMode);

            bool hasProgress = HasReturningProgress();
            if (_primaryPlayLabel != null)
            {
                if (IsPauseMode)
                    _primaryPlayLabel.text = Tr("ui.menu.play_resume", "OYUNA DÖN");
                else if (hasProgress)
                    _primaryPlayLabel.text = Tr("ui.menu.play_continue", "DEVAM ET");
                else
                    _primaryPlayLabel.text = Tr("ui.menu.play_new", "MAHALLEYE GİR");
            }

            if (_navSportsLabel != null) _navSportsLabel.text = Tr("ui.menu.nav_sports", "Spor & Mini Oyunlar");
            if (_navDistrictsLabel != null)
            {
                _navDistrictsLabel.text = IsPauseMode
                    ? Tr("ui.menu.nav_fast_travel", "Bölgeye Işınlan")
                    : Tr("ui.menu.nav_districts", "Bölge Seç & Başla");
            }
            if (_navSettingsLabel != null) _navSettingsLabel.text = Tr("ui.menu.nav_settings", "Ayarlar & Dünya");
            if (_navWhatsNewLabel != null) _navWhatsNewLabel.text = Tr("ui.menu.nav_whatsnew", "Yenilikler & Topluluk");
            if (_navGuideLabel != null) _navGuideLabel.text = Tr("ui.menu.nav_guide", "Mahalle Rehberi & Harita");
            if (_navGuideBtn != null) _navGuideBtn.gameObject.SetActive(IsPauseMode);

            if (_districtsHeaderText != null) _districtsHeaderText.text = Tr("ui.menu.districts_title", "BAŞLANGIÇ BÖLGESİ");
            if (_districtsHintText != null) _districtsHintText.text = Tr("ui.menu.districts_hint", "Bir mahalleye dokun, canlı kameradan incele ve oradan başla.");
            if (_sportsHeaderText != null) _sportsHeaderText.text = Tr("ui.menu.sports_title", "SPOR & MİNİ OYUNLAR");
            if (_sportsHintText != null) _sportsHintText.text = Tr("ui.menu.sports_hint", "İstediğin zaman bir spor seç, canlı sahayı gör ve hemen maça başla!");
            if (_settingsHeaderText != null) _settingsHeaderText.text = Tr("ui.menu.settings_title", "AYARLAR & ATMOSFER");
            if (_setGfxCaption != null) _setGfxCaption.text = Tr("ui.menu.set_graphics", "Grafik Kalitesi");
            if (_gfxLowText != null) _gfxLowText.text = Tr("ui.menu.set_gfx_low", "Düşük");
            if (_gfxMedText != null) _gfxMedText.text = Tr("ui.menu.set_gfx_med", "Orta");
            if (_setLangCaption != null) _setLangCaption.text = Tr("ui.menu.set_language", "Dil");
            if (_setAudioCaption != null) _setAudioCaption.text = Tr("ui.menu.set_audio", "Ses Seviyesi");
            if (_setVoiceCaption != null) _setVoiceCaption.text = Tr("ui.menu.set_voice", "Seslendirme");
            if (_voiceOffText != null) _voiceOffText.text = Tr("ui.menu.off", "Kapalı");
            if (_voiceOnText != null) _voiceOnText.text = Tr("ui.menu.on", "Açık");
            if (_setSensCaption != null) _setSensCaption.text = Tr("ui.menu.set_sens", "Kamera Hassasiyeti");
            if (_sensLowText != null) _sensLowText.text = Tr("ui.menu.set_sens_low", "Yavaş");
            if (_sensMidText != null) _sensMidText.text = Tr("ui.menu.set_sens_mid", "Normal");
            if (_sensHighText != null) _sensHighText.text = Tr("ui.menu.set_sens_high", "Hızlı");
            if (_setInvertYCaption != null) _setInvertYCaption.text = Tr("ui.menu.set_invert_y", "Kamera Y Ekseni");
            if (_invertYOffText != null) _invertYOffText.text = Tr("ui.menu.normal", "Normal");
            if (_invertYOnText != null) _invertYOnText.text = Tr("ui.menu.inverted", "Ters");
            if (_setSubtitleCaption != null) _setSubtitleCaption.text = Tr("ui.menu.set_subtitles", "Altyazı Boyutu");
            if (_subNormText != null) _subNormText.text = Tr("ui.menu.normal", "Normal");
            if (_subLargeText != null) _subLargeText.text = Tr("ui.menu.large", "Büyük");
            if (_setTimeCaption != null) _setTimeCaption.text = Tr("ui.menu.set_time", "Günün Saati (Canlı Önizleme)");
            if (_timeChipTexts[0] != null) _timeChipTexts[0].text = Tr("ui.menu.time_morning", "Sabah");
            if (_timeChipTexts[1] != null) _timeChipTexts[1].text = Tr("ui.menu.time_noon", "Öğle");
            if (_timeChipTexts[2] != null) _timeChipTexts[2].text = Tr("ui.menu.time_sunset", "Akşam");
            if (_timeChipTexts[3] != null) _timeChipTexts[3].text = Tr("ui.menu.time_night", "Gece");
            if (_setFpsCaption != null) _setFpsCaption.text = Tr("ui.menu.set_fps", "FPS Göstergesi");
            if (_fpsOffText != null) _fpsOffText.text = Tr("ui.menu.off", "Kapalı");
            if (_fpsOnText != null) _fpsOnText.text = Tr("ui.menu.on", "Açık");
            if (_resetInitialLabel != null) _resetInitialLabel.text = Tr("ui.menu.reset_btn", "Yeni Oyun (Kaydı Sıfırla)");
            if (_resetConfirmLabel != null) _resetConfirmLabel.text = Tr("ui.menu.reset_confirm", "EMİN MİSİN? SIFIRLA");
            if (_resetCancelLabel != null) _resetCancelLabel.text = Tr("ui.menu.reset_cancel", "Vazgeç");

            if (_profileHeaderText != null) _profileHeaderText.text = Tr("ui.menu.profile_header", "MAHALLE PROFİLİN");
            if (_statCashLabel != null) _statCashLabel.text = Tr("ui.menu.stat_cash", "NAKİT");
            if (_statMissionsLabel != null) _statMissionsLabel.text = Tr("ui.menu.stat_missions", "GÖREV");
            if (_statLandmarksLabel != null) _statLandmarksLabel.text = Tr("ui.menu.stat_landmarks", "KEŞİF");
            if (_statMemoriesLabel != null) _statMemoriesLabel.text = Tr("ui.menu.stat_memories", "HATIRA");

            if (_hudMenuButtonLabel != null) _hudMenuButtonLabel.text = Tr("ui.menu.hud_btn", "Menü");

            UpdateVistaLabel();
        }

        public void RefreshWorldSnapshot()
        {
            SaveData data = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            int cash = data != null ? data.money : 250;
            int missionsDone = data != null && data.completedMissionIds != null ? data.completedMissionIds.Count : 0;
            int landmarks = LandmarkDiscoverySystem.DiscoveredCount;
            int memories = NeighborhoodCollectibleManager.CollectedCount;

            if (_liveClockText != null)
            {
                _liveClockText.text = DayNightCycle.Instance != null ? DayNightCycle.Instance.GetFormattedTime() : "09:00";
            }

            if (_liveWeatherText != null)
            {
                NeighborhoodWeatherState ws = NeighborhoodWeatherSystem.Instance != null
                    ? NeighborhoodWeatherSystem.Instance.CurrentWeather
                    : NeighborhoodWeatherState.Clear;
                _liveWeatherText.text = ws switch
                {
                    NeighborhoodWeatherState.BreezyOvercast => Tr("ui.menu.weather_breezy", "Serin & Esintili"),
                    NeighborhoodWeatherState.SummerShower => Tr("ui.menu.weather_rain", "Yaz Yağmuru"),
                    _ => Tr("ui.menu.weather_clear", "Açık & Güneşli")
                };
            }

            if (_primaryPlaySubLabel != null)
            {
                if (HasReturningProgress() || IsPauseMode)
                {
                    StreetNameTracker street = FindAnyObjectByType<StreetNameTracker>();
                    string loc = (street != null && !string.IsNullOrEmpty(street.CurrentStreetName))
                        ? street.CurrentStreetName
                        : "Selman Sokağı";
                    _primaryPlaySubLabel.text = $"{loc}  ·  ₺ {cash}";
                }
                else
                {
                    _primaryPlaySubLabel.text = Tr("ui.menu.play_new_sub", "Selman Sokağı No:48 · Yeni Hayat");
                }
            }

            if (_rankBadgeText != null)
            {
                _rankBadgeText.text = $"★  {NeighborhoodReputation.GetRankTitle()}";
            }

            if (_rankProgressFill != null)
            {
                int xp = NeighborhoodReputation.CurrentXp;
                int next = Mathf.Max(1, NeighborhoodReputation.GetNextRankThreshold());
                _rankProgressFill.fillAmount = xp >= next ? 1f : Mathf.Clamp01((float)xp / next);
            }

            int totalMissions = (MissionManager.Instance != null && MissionManager.Instance.TotalMissionCount > 0)
                ? MissionManager.Instance.TotalMissionCount
                : DefaultTotalStoryMissions;
            if (_statCashValue != null) _statCashValue.text = $"₺ {cash}";
            if (_statMissionsValue != null) _statMissionsValue.text = $"{missionsDone} / {totalMissions}";
            if (_statLandmarksValue != null) _statLandmarksValue.text = $"{landmarks} / {LandmarkDiscoverySystem.TotalCount}";
            if (_statMemoriesValue != null) _statMemoriesValue.text = $"{memories} / {NeighborhoodCollectibleManager.TotalCount}";
        }

        private void RefreshDistrictCards()
        {
            bool isEn = IsEnglish();
            List<string> discovered = SaveManager.Instance != null && SaveManager.Instance.Data != null
                ? SaveManager.Instance.Data.discoveredLandmarkIds
                : null;

            for (int i = 0; i < Districts.Length && i < _districtCardBgs.Count; i++)
            {
                DistrictOption d = Districts[i];
                bool selected = i == _selectedDistrictIndex;
                bool isDiscovered = discovered != null && discovered.Contains(d.LandmarkId);

                _districtCardBgs[i].color = selected ? CardSelected : CardSurface;
                _districtCardBorders[i].color = selected ? BorderActive : BorderSubtle;
                _districtTitleTexts[i].text = isEn ? d.NameEn : d.NameTr;
                _districtTagTexts[i].text = isEn ? d.TagEn : d.TagTr;

                _districtBadgeTexts[i].text = isDiscovered
                    ? $"✓  {Tr("ui.menu.discovered", "KEŞFEDİLDİ")}"
                    : $"•  {Tr("ui.menu.unexplored", "KEŞFET")}";
                _districtBadgeTexts[i].color = isDiscovered ? AccentMint : AccentGold;
                _districtBadgeBgs[i].color = isDiscovered ? BadgeFoundBg : BadgeExploreBg;
            }

            if (_spawnDistrictBtnLabel != null && _selectedDistrictIndex >= 0 && _selectedDistrictIndex < Districts.Length)
            {
                DistrictOption sel = Districts[_selectedDistrictIndex];
                string action = IsPauseMode
                    ? Tr("ui.menu.travel_here", "BURAYA IŞINLAN")
                    : Tr("ui.menu.start_here", "BURADA BAŞLA");
                string place = isEn ? sel.NameEn : sel.NameTr;
                _spawnDistrictBtnLabel.text = $"▶   {action}   ·   {place}";
            }
        }

        private void RefreshSettingsUiState()
        {
            GraphicsTier tier = GraphicsProfileManager.Instance != null
                ? GraphicsProfileManager.Instance.CurrentTier
                : GraphicsTier.Medium;
            SetSegColor(_gfxLowBg, tier == GraphicsTier.Low);
            SetSegColor(_gfxMedBg, tier == GraphicsTier.Medium);

            bool isEn = IsEnglish();
            SetSegColor(_langTrBg, !isEn);
            SetSegColor(_langEnBg, isEn);

            if (_volumeSlider != null) _volumeSlider.SetValueWithoutNotify(AudioListener.volume);
            if (_volumeValueText != null) _volumeValueText.text = $"{Mathf.RoundToInt(AudioListener.volume * 100f)}%";
            if (_setMusicCaption != null) _setMusicCaption.text = isEn ? "Music & Radio" : "Müzik & Radyo";
            if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(TurkishCarRadio.MusicVolume);
            if (_musicValueText != null) _musicValueText.text = $"{Mathf.RoundToInt(TurkishCarRadio.MusicVolume * 100f)}%";

            bool voiceOn = VoiceoverManager.Enabled;
            SetSegColor(_voiceOffBg, !voiceOn);
            SetSegColor(_voiceOnBg, voiceOn);

            float sens = InputHub.Instance != null ? InputHub.Instance.SensitivityMultiplier : 1f;
            SetSegColor(_sensLowBg, sens < 0.85f);
            SetSegColor(_sensMidBg, sens >= 0.85f && sens <= 1.20f);
            SetSegColor(_sensHighBg, sens > 1.20f);

            bool invY = InputHub.Instance != null && InputHub.Instance.InvertY;
            SetSegColor(_invertYOffBg, !invY);
            SetSegColor(_invertYOnBg, invY);

            bool largeSub = GameHUDController.LargeSubtitles;
            SetSegColor(_subNormBg, !largeSub);
            SetSegColor(_subLargeBg, largeSub);

            float hour = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentHour : 9f;
            int activeTimeIdx = hour < 11.5f ? 0 : (hour < 16.5f ? 1 : (hour < 20.0f ? 2 : 3));
            for (int i = 0; i < 4; i++)
            {
                SetSegColor(_timeChipBgs[i], i == activeTimeIdx);
            }

            int activeWeatherIdx = NeighborhoodWeatherSystem.Instance != null ? (int)NeighborhoodWeatherSystem.Instance.CurrentWeather : 0;
            if (_setWeatherCaption != null) _setWeatherCaption.text = isEn ? "Weather (Live Preview)" : "Hava Durumu (Canlı)";
            string[] wLabels = isEn ? new[] { "Sunny", "Cloudy", "Rain" } : new[] { "Güneşli", "Bulutlu", "Yağmurlu" };
            for (int i = 0; i < 3; i++)
            {
                if (_weatherChipTexts[i] != null) _weatherChipTexts[i].text = wLabels[i];
                SetSegColor(_weatherChipBgs[i], i == activeWeatherIdx);
            }

            bool fpsOn = _hud != null && _hud.IsTelemetryVisible;
            SetSegColor(_fpsOffBg, !fpsOn);
            SetSegColor(_fpsOnBg, fpsOn);
        }

        private void RefreshSportCards()
        {
            bool isEn = IsEnglish();
            for (int i = 0; i < QuickSports.Length && i < _sportCardBgs.Count; i++)
            {
                SportQuickOption opt = QuickSports[i];
                bool selected = i == _selectedSportIndex;
                _sportCardBgs[i].color = selected ? CardSelected : CardSurface;
                _sportCardBorders[i].color = selected ? BorderActive : BorderSubtle;
                _sportTitleTexts[i].text = isEn ? opt.NameEn : opt.NameTr;
                _sportTagTexts[i].text = isEn ? opt.TagEn : opt.TagTr;
                _sportBadgeTexts[i].text = isEn ? opt.BadgeEn : opt.BadgeTr;
            }

            if (_startSportBtnLabel != null && _selectedSportIndex >= 0 && _selectedSportIndex < QuickSports.Length)
            {
                SportQuickOption sel = QuickSports[_selectedSportIndex];
                string action = Tr("ui.menu.play_sport_now", "MAÇA BAŞLA");
                string sportName = isEn ? sel.NameEn : sel.NameTr;
                _startSportBtnLabel.text = $"▶   {action}   ·   {sportName}";
            }
        }

        private void UpdateNavHighlight()
        {
            UpdateNavBg(_navSportsBtn, _navSportsBg, ActiveDrawer == MenuDrawer.Sports);
            UpdateNavBg(_navDistrictsBtn, _navDistrictsBg, ActiveDrawer == MenuDrawer.Districts);
            UpdateNavBg(_navSettingsBtn, _navSettingsBg, ActiveDrawer == MenuDrawer.Settings);
        }

        private static void UpdateNavBg(UnityEngine.UI.Button btn, UnityEngine.UI.Image bg, bool active)
        {
            if (bg == null) return;
            bg.color = active ? CardSelected : PanelGlass;
            if (btn != null)
            {
                UiButtonFeedback fb = btn.GetComponent<UiButtonFeedback>();
                if (fb != null) fb.Configure(btn, bg);
            }
        }

        private void UpdateVistaLabel()
        {
            if (_vistaNameText == null) return;
            bool isEn = IsEnglish();
            if (_usingCustomPreview)
            {
                _vistaNameText.text = isEn ? _customPreviewNameEn : _customPreviewNameTr;
                return;
            }
            if (_usingPlayerOrbitVista)
            {
                _vistaNameText.text = Tr("ui.menu.vista_player", "Bulunduğun Konum");
                return;
            }
            ScenicVista v = ScenicVistas[_currentVistaIndex];
            _vistaNameText.text = isEn ? v.NameEn : v.NameTr;
        }

        private static void SetSegColor(UnityEngine.UI.Image bg, bool active)
        {
            if (bg != null)
            {
                bg.color = active ? AccentTealDark : SegInactive;
            }
        }

        private static bool HasReturningProgress()
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d == null) return false;
            return d.totalPlayTimeSeconds > 8f ||
                   d.hasSeenStoryIntro ||
                   (d.completedMissionIds != null && d.completedMissionIds.Count > 0) ||
                   (d.discoveredLandmarkIds != null && d.discoveredLandmarkIds.Count > 0);
        }

        private static bool IsEnglish()
        {
            return LocalizationService.Instance != null &&
                   string.Equals(LocalizationService.Instance.CurrentLanguage, "en", StringComparison.OrdinalIgnoreCase);
        }

        private static string Tr(string key, string fallback) => LocalizationService.Tr(key, fallback);

    }
}
