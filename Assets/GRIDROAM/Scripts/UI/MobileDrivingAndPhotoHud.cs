using System.Collections;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Adds two high-impact mobile UX controls cleanly at runtime using OurWorldUiKit:
    /// 1. Mobile Driving Utility Bar (Horn, 3-Station Turkish FM Radio, Headlights) attached to DrivingControlsRoot
    ///    so iPhone & iPad players can tap Horn / Radio / Headlights just like desktop H / R / L keys.
    /// 2. Photo Mode toggle button: hides the entire HUD cleanly so players can orbit the camera freely
    ///    and capture crisp full-screen screenshots at Atlas AVM Rooftop, Sultanbeyli Lake, or beside tuned cars!
    /// </summary>
    public sealed class MobileDrivingAndPhotoHud : MonoBehaviour
    {
        public static MobileDrivingAndPhotoHud Instance { get; private set; }

        private Text _hornLabel;
        private Text _radioLabel;
        private Text _lightsLabel;
        private Text _photoBtnLabel;
        private GameObject _photoExitOverlay;
        private Text _photoExitLabel;
        private bool _inPhotoMode;

        public bool IsPhotoModeActive => _inPhotoMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<GameHUDController>() == null) return;
            GameObject go = new GameObject("OurWorld_MobileDrivingAndPhotoHud");
            go.AddComponent<MobileDrivingAndPhotoHud>();
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
            // Wait 1 frame so GameHUDController and OurWorldHudThemeApplier finish initializing
            yield return null;
            OurWorldUiKit.EnsureInitialized();

            GameHUDController hud = FindAnyObjectByType<GameHUDController>();
            if (hud != null)
            {
                BuildDrivingUtilityBar(hud.transform);
                BuildPhotoModeControls(hud.transform);
            }

            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged += HandleLanguageChanged;
            }
            RefreshLabels();
        }

        private void OnDestroy()
        {
            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged -= HandleLanguageChanged;
            }
            if (Instance == this) Instance = null;
        }

        private void HandleLanguageChanged(string _) => RefreshLabels();

        private void BuildDrivingUtilityBar(Transform hudRoot)
        {
            Transform drivingRoot = FindDescendant(hudRoot, "DrivingControls")
                                    ?? FindDescendant(hudRoot, "DrivingControlsRoot");
            if (drivingRoot == null) return;

            GameObject bar = new GameObject("MobileDrivingUtilityBar", typeof(RectTransform));
            bar.transform.SetParent(drivingRoot, false);
            RectTransform rt = (RectTransform)bar.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -24f);
            rt.sizeDelta = new Vector2(468f, 52f);

            HorizontalLayoutGroup hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            OurWorldUiKit.CreatePillButton("Btn_CarHorn", bar.transform, "Korna", 18,
                OurWorldUiKit.SurfaceBg, OurWorldUiKit.AccentGold, OurWorldUiKit.BorderSubtle, out _hornLabel, () =>
                {
                    PlayerModeController mode = PlayerModeController.Instance;
                    if (mode != null && mode.IsDriving && mode.CurrentVehicle != null)
                    {
                        mode.CurrentVehicle.TriggerHorn();
                    }
                });

            OurWorldUiKit.CreatePillButton("Btn_CarRadio", bar.transform, "FM Radyo", 18,
                OurWorldUiKit.SurfaceBg, OurWorldUiKit.AccentTeal, OurWorldUiKit.BorderSubtle, out _radioLabel, () =>
                {
                    PlayerModeController mode = PlayerModeController.Instance;
                    if (mode != null && mode.IsDriving && mode.CurrentVehicle != null)
                    {
                        mode.CurrentVehicle.CycleCarRadio();
                    }
                });

            OurWorldUiKit.CreatePillButton("Btn_CarLights", bar.transform, "Farlar", 18,
                OurWorldUiKit.SurfaceBg, OurWorldUiKit.TextPrimary, OurWorldUiKit.BorderSubtle, out _lightsLabel, () =>
                {
                    PlayerModeController mode = PlayerModeController.Instance;
                    if (mode != null && mode.IsDriving && mode.CurrentVehicle != null)
                    {
                        mode.CurrentVehicle.ToggleHeadlights();
                    }
                });
        }

        private void BuildPhotoModeControls(Transform hudRoot)
        {
            Transform safeArea = hudRoot.Find("SafeAreaRoot") ?? hudRoot;

            Button photoBtn = OurWorldUiKit.CreatePillButton("Btn_PhotoMode", safeArea, "Foto", 17,
                OurWorldUiKit.SurfaceBg, OurWorldUiKit.TextPrimary, OurWorldUiKit.BorderSubtle, out _photoBtnLabel, EnterPhotoMode);
            RectTransform prt = (RectTransform)photoBtn.transform;
            prt.anchorMin = new Vector2(1f, 1f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(1f, 1f);
            prt.anchoredPosition = new Vector2(-18f, -158f);
            prt.sizeDelta = new Vector2(106f, 40f);

            // Dedicated minimal exit button on its own overlay canvas so it stays visible while HUD is hidden!
            GameObject cGo = new GameObject("PhotoModeExitCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cGo.transform.SetParent(transform, false);
            Canvas c = cGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 165;
            CanvasScaler cs = cGo.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;

            Button exitBtn = OurWorldUiKit.CreatePillButton("Btn_ExitPhotoMode", cGo.transform, "✕ Fotoğraf Modundan Çık", 20,
                new Color(0.06f, 0.09f, 0.14f, 0.72f), OurWorldUiKit.AccentGold, OurWorldUiKit.BorderSubtle, out _photoExitLabel, ExitPhotoMode);
            RectTransform ert = (RectTransform)exitBtn.transform;
            ert.anchorMin = new Vector2(1f, 1f);
            ert.anchorMax = new Vector2(1f, 1f);
            ert.pivot = new Vector2(1f, 1f);
            ert.anchoredPosition = new Vector2(-28f, -28f);
            ert.sizeDelta = new Vector2(290f, 50f);

            _photoExitOverlay = cGo;
            _photoExitOverlay.SetActive(false);
        }

        private void EnterPhotoMode()
        {
            if (_inPhotoMode) return;
            _inPhotoMode = true;
            GameFeelAudio.PlayUiClick();
            GameHUDController hud = FindAnyObjectByType<GameHUDController>();
            hud?.SetHudVisible(false, 0.18f);
            if (_photoExitOverlay != null) _photoExitOverlay.SetActive(true);
        }

        public void ExitPhotoMode()
        {
            if (!_inPhotoMode) return;
            _inPhotoMode = false;
            GameFeelAudio.PlayUiClick();
            GameHUDController hud = FindAnyObjectByType<GameHUDController>();
            hud?.SetHudVisible(true, 0.18f);
            if (_photoExitOverlay != null) _photoExitOverlay.SetActive(false);
        }

        private void RefreshLabels()
        {
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            if (_hornLabel != null) _hornLabel.text = isEn ? "Horn" : "Korna";
            if (_radioLabel != null) _radioLabel.text = isEn ? "FM Radio" : "FM Radyo";
            if (_lightsLabel != null) _lightsLabel.text = isEn ? "Lights" : "Farlar";
            if (_photoBtnLabel != null) _photoBtnLabel.text = isEn ? "Photo" : "Foto";
            if (_photoExitLabel != null) _photoExitLabel.text = isEn ? "✕ Exit Photo Mode" : "✕ Fotoğraf Modundan Çık";
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform ch = root.GetChild(i);
                if (ch.name == name) return ch;
                Transform nested = FindDescendant(ch, name);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
