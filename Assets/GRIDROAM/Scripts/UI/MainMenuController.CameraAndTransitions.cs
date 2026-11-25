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
        public void CycleVista(int direction)
        {
            if (!IsOpen || IsTransitioning) return;
            GameFeelAudio.PlayUiClick();
            _usingCustomPreview = false;
            _usingPlayerOrbitVista = false;
            _vistaTimer = 0f;
            int len = ScenicVistas.Length;
            _currentVistaIndex = (_currentVistaIndex + direction + len) % len;
            UpdateVistaLabel();
            SnapCameraToActiveVista();
        }

        public void SelectVista(int index, bool instant = false)
        {
            _usingCustomPreview = false;
            _usingPlayerOrbitVista = false;
            _vistaTimer = 0f;
            _currentVistaIndex = Mathf.Clamp(index, 0, ScenicVistas.Length - 1);
            UpdateVistaLabel();
            if (instant) SnapCameraToActiveVista();
        }

        public void SetPreviewTimePreset(float hour)
        {
            GameFeelAudio.PlayUiClick();
            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.SetTimeOfDay(hour);
            }
            RefreshSettingsUiState();
            RefreshWorldSnapshot();
        }

        // =====================================================================
        // CAMERA TOUR & SEAMLESS SWOOP TRANSITION
        // =====================================================================

        private void ConfigureInitialVistaForOpen(bool asPauseMenu)
        {
            _usingPlayerOrbitVista = false;

            if (asPauseMenu && PlayerModeController.Instance != null)
            {
                Vector3 focus = PlayerModeController.Instance.ActiveWorldPosition + Vector3.up * 1.35f;
                bool indoors = (_cameraRig != null && _cameraRig.IsIndoors) ||
                               Physics.Raycast(focus, Vector3.up, 4.5f, ~0, QueryTriggerInteraction.Ignore);
                if (!indoors)
                {
                    _usingPlayerOrbitVista = true;
                    Vector3 camBack = _mainCamera != null ? -_mainCamera.transform.forward : Vector3.back;
                    camBack.y = 0f;
                    if (camBack.sqrMagnitude < 0.01f) camBack = Vector3.back;
                    camBack.Normalize();
                    Vector3 camRight = Vector3.Cross(Vector3.up, -camBack).normalized;
                    // Frame the player slightly to the right of center so they sit cleanly in the open window
                    _playerOrbitLookAt = focus - camRight * 1.4f;
                    _playerOrbitBasePos = focus + camBack * 10.0f + camRight * 1.8f + Vector3.up * 5.2f;
                    _camInitialized = false;
                    return;
                }
            }

            if (!asPauseMenu)
            {
                _currentVistaIndex = 0;
            }
            _camInitialized = false;
        }

        private void GetDesiredMenuCameraTarget(out Vector3 pos, out Vector3 lookAt, out float fov)
        {
            if (_usingCustomPreview)
            {
                Vector3 forward = (_customPreviewLookAt - _customPreviewPos).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                pos = _customPreviewPos
                      + right * (Mathf.Sin(_orbitClock * 0.24f) * 0.95f)
                      + Vector3.up * (Mathf.Cos(_orbitClock * 0.19f) * 0.28f);
                lookAt = _customPreviewLookAt;
                fov = 54f;
                return;
            }

            if (_usingPlayerOrbitVista)
            {
                float angle = Mathf.Sin(_orbitClock * 0.16f) * 16f;
                Vector3 offset = _playerOrbitBasePos - _playerOrbitLookAt;
                Vector3 rotated = Quaternion.Euler(0f, angle, 0f) * offset;
                pos = _playerOrbitLookAt + rotated + Vector3.up * (Mathf.Sin(_orbitClock * 0.25f) * 0.28f);
                lookAt = _playerOrbitLookAt;
                fov = 55f;
                return;
            }

            ScenicVista v = ScenicVistas[_currentVistaIndex];
            Vector3 vFwd = (v.LookAt - v.BasePos).normalized;
            Vector3 vRight = Vector3.Cross(Vector3.up, vFwd).normalized;
            float t = _orbitClock * v.SwaySpeed;
            pos = v.BasePos
                  + vRight * (Mathf.Sin(t) * v.SwayRadius)
                  + Vector3.up * (Mathf.Sin(t * 0.72f) * 0.35f);
            lookAt = v.LookAt;
            fov = v.Fov;
        }

        private void SnapCameraToActiveVista()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            GetDesiredMenuCameraTarget(out Vector3 pos, out Vector3 lookAt, out float fov);
            _camSmoothPos = pos;
            _camSmoothLookAt = lookAt;
            _camSmoothFov = fov;
            _camInitialized = true;

            _mainCamera.transform.position = pos;
            _mainCamera.transform.LookAt(lookAt);
            _mainCamera.fieldOfView = fov;
        }

        private void UpdateScenicCamera(float dt)
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            GetDesiredMenuCameraTarget(out Vector3 targetPos, out Vector3 targetLook, out float targetFov);

            // If target is far away (vista switch), snap cleanly instead of flying through buildings
            if (!_camInitialized || Vector3.Distance(_camSmoothPos, targetPos) > 14f)
            {
                _camSmoothPos = targetPos;
                _camSmoothLookAt = targetLook;
                _camSmoothFov = targetFov;
                _camInitialized = true;
            }
            else
            {
                float blend = 1f - Mathf.Exp(-3.2f * dt);
                _camSmoothPos = Vector3.Lerp(_camSmoothPos, targetPos, blend);
                _camSmoothLookAt = Vector3.Lerp(_camSmoothLookAt, targetLook, blend);
                _camSmoothFov = Mathf.Lerp(_camSmoothFov, targetFov, blend);
            }

            _mainCamera.transform.position = _camSmoothPos;
            Vector3 dir = _camSmoothLookAt - _camSmoothPos;
            if (dir.sqrMagnitude > 0.001f)
            {
                _mainCamera.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
            _mainCamera.fieldOfView = _camSmoothFov;
        }

        private IEnumerator FadeMenuInRoutine()
        {
            _menuCanvasGroup.alpha = 0f;
            _menuCanvasGroup.interactable = false;
            _menuCanvasGroup.blocksRaycasts = true;

            const float duration = 0.24f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _menuCanvasGroup.alpha = k;
                if (_leftRailRect != null)
                {
                    _leftRailRect.anchoredPosition = new Vector2(Mathf.Lerp(-36f, 0f, k), 0f);
                }
                yield return null;
            }

            _menuCanvasGroup.alpha = 1f;
            _menuCanvasGroup.interactable = true;
            if (_leftRailRect != null) _leftRailRect.anchoredPosition = Vector2.zero;
            _transitionRoutine = null;
        }

        private void StartEnterWorldTransition(int teleportToDistrictIndex, bool openGuideAfter, int launchSportIndex = -1)
        {
            if (_drawerAnimRoutine != null)
            {
                StopCoroutine(_drawerAnimRoutine);
                _drawerAnimRoutine = null;
            }
            if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
            _transitionRoutine = StartCoroutine(EnterWorldSwoopRoutine(teleportToDistrictIndex, openGuideAfter, launchSportIndex));
        }

        private IEnumerator EnterWorldSwoopRoutine(int districtIndex, bool openGuideAfter, int launchSportIndex = -1)
        {
            IsTransitioning = true;
            _menuCanvasGroup.interactable = false;
            _menuCanvasGroup.blocksRaycasts = false;

            string arrivalToast = null;
            if (launchSportIndex >= 0 && launchSportIndex < QuickSports.Length)
            {
                SportQuickOption sport = QuickSports[launchSportIndex];
                LaunchQuickSport(launchSportIndex, sport);
                bool isEn = IsEnglish();
                arrivalToast = isEn ? $"Match Started: {sport.NameEn}" : $"Maç Başladı: {sport.NameTr}";
            }
            else if (districtIndex >= 0 && districtIndex < Districts.Length)
            {
                DistrictOption opt = Districts[districtIndex];
                TeleportPlayerToDistrict(opt);
                bool isEn = IsEnglish();
                arrivalToast = isEn ? $"Arrived: {opt.NameEn}" : $"Başlangıç Noktası: {opt.NameTr}";
            }

            if (_cameraRig != null && launchSportIndex < 0)
            {
                _cameraRig.SnapBehindTargetImmediate();
            }

            Vector3 startPos = _mainCamera != null ? _mainCamera.transform.position : Vector3.zero;
            Quaternion startRot = _mainCamera != null ? _mainCamera.transform.rotation : Quaternion.identity;
            float startFov = _mainCamera != null ? _mainCamera.fieldOfView : 56f;

            Vector3 endPos = startPos;
            Quaternion endRot = startRot;
            float endFov = 58f;
            bool hasTargetPose = _cameraRig != null && _cameraRig.TryGetGameplayPose(out endPos, out endRot, out endFov);

            // If coming from a faraway landmark vista, lift cleanly over rooftops into a crisp aerial dive
            float travelDist = hasTargetPose ? Vector3.Distance(startPos, endPos) : 0f;
            if (hasTargetPose && travelDist > 26f)
            {
                // Start the dive from an elevated neighborhood overlook above the player so it never clips through blocks
                Vector3 diveOffset = (endPos - (endPos + (endRot * Vector3.forward) * 4.5f)).normalized * 12f;
                startPos = endPos + diveOffset + Vector3.up * 11f;
                startRot = Quaternion.LookRotation(((endPos + (endRot * Vector3.forward) * 4f) - startPos).normalized, Vector3.up);
                startFov = 52f;
            }

            float swoopDuration = hasTargetPose ? (openGuideAfter ? 0.32f : 0.72f) : 0.24f;
            float fadeDuration = 0.24f;
            float arcLift = hasTargetPose ? Mathf.Clamp(Vector3.Distance(startPos, endPos) * 0.12f, 0.35f, 3.2f) : 0f;

            float elapsed = 0f;
            while (elapsed < swoopDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / swoopDuration);
                float smooth = u * u * (3f - 2f * u);

                float uiAlpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));
                _menuCanvasGroup.alpha = uiAlpha;
                if (_leftRailRect != null)
                {
                    _leftRailRect.anchoredPosition = new Vector2(Mathf.Lerp(0f, -32f, 1f - uiAlpha), 0f);
                }

                if (_mainCamera != null && hasTargetPose)
                {
                    Vector3 pos = Vector3.Lerp(startPos, endPos, smooth);
                    pos.y += Mathf.Sin(smooth * Mathf.PI) * arcLift;
                    _mainCamera.transform.position = pos;
                    _mainCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, smooth);
                    _mainCamera.fieldOfView = Mathf.Lerp(startFov, endFov, smooth);
                }

                yield return null;
            }

            _menuCanvasGroup.alpha = 0f;
            _canvasObj.SetActive(false);

            if (_cameraRig != null)
            {
                _cameraRig.ExternalCameraControl = false;
            }

            IsOpen = false;
            IsTransitioning = false;
            ActiveDrawer = MenuDrawer.None;
            ReleaseGameplayLock();

            if (_hud != null)
            {
                _hud.SetHudVisible(true, 0.24f);
            }

            if (!string.IsNullOrEmpty(arrivalToast) && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage(arrivalToast);
                GameFeelAudio.SpawnBurst(
                    PlayerModeController.Instance.ActiveWorldPosition + Vector3.up * 1.2f,
                    AccentTeal,
                    count: 10,
                    spread: 0.6f);
            }

            if (openGuideAfter && PhonePanelController.Instance != null && !PhonePanelController.Instance.IsOpen)
            {
                PhonePanelController.Instance.Open();
            }

            _transitionRoutine = null;
        }

        private static void LaunchQuickSport(int sportIndex, SportQuickOption sport)
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null) return;
            Quaternion exitRot = Quaternion.Euler(0f, sport.ExitYaw, 0f);

            switch (sportIndex)
            {
                case 0:
                {
                    TennisCourt tennis = FindAnyObjectByType<TennisCourt>();
                    if (tennis != null) mode.StartTennisMatch(tennis, sport.ExitPos, exitRot);
                    break;
                }
                case 1:
                {
                    LiveFootballMatch football = FindAnyObjectByType<LiveFootballMatch>();
                    if (football != null) mode.StartArenaSport(football, sport.ExitPos, exitRot);
                    break;
                }
                case 2:
                {
                    LiveBasketballMatch basket = FindAnyObjectByType<LiveBasketballMatch>();
                    if (basket != null) mode.StartArenaSport(basket, sport.ExitPos, exitRot);
                    break;
                }
                case 3:
                {
                    LiveVolleyballMatch volley = FindAnyObjectByType<LiveVolleyballMatch>();
                    if (volley != null) mode.StartArenaSport(volley, sport.ExitPos, exitRot);
                    break;
                }
                case 4:
                {
                    LiveSwimRace swimRace = SwimWorldInstaller.ActiveSwimRace ?? FindAnyObjectByType<LiveSwimRace>();
                    if (swimRace != null) mode.StartArenaSport(swimRace, sport.ExitPos, exitRot);
                    break;
                }
                case 5:
                {
                    LiveShootingRange range = ShootingRangeInstaller.ActiveRange ?? FindAnyObjectByType<LiveShootingRange>();
                    if (range != null) mode.StartArenaSport(range, sport.ExitPos, exitRot);
                    break;
                }
            }
        }

        private static void TeleportPlayerToDistrict(DistrictOption opt)
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null) return;

            CarryableProp.DropHeldPropIfAny();
            StreetCatController.CancelActivePet();

            if (mode.CurrentMode != ControlMode.OnFoot)
            {
                mode.SetOnFootMode(snapCamera: false);
            }

            if (mode.FootController != null)
            {
                mode.FootController.TeleportTo(opt.SpawnPos, Quaternion.Euler(0f, opt.SpawnYaw, 0f));
            }

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Save();
            }
        }

        private void ReleaseGameplayLock()
        {
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(_modalLockToken, false);
            }
        }

    }
}
