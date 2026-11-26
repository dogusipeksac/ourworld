using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Ensures ergonomic, mobile-standard touch control sizes across phones (16:9 / 19.5:9 / 20:9 iPhone & Android)
    /// and tablets (4:3 iPad / 16:10 Android tablets / foldables) at runtime with zero scene rebuild required.
    /// - Uses phone-first CanvasScaler reference scaling (~1613x907 height-weighted on phones) so thumb targets
    ///   hit authentic mobile gaming standards (20-24mm joystick, 12-15mm action buttons, 6-7mm interact/menu pills)
    /// - Avoids macOS/Windows Editor DPI misclassification when simulating iPhone resolutions in Game View
    /// - Enlarges and cleanly aligns the movement joystick, on-foot action arc, car steering/pedals/utility bar,
    ///   interact pill, phone guide, menu, backpack, and photo buttons with zero overlap.
    /// </summary>
    public sealed class ResponsiveTabletLayout : MonoBehaviour
    {
        private const float BaseRefWidth = 1920f;
        private const float BaseRefHeight = 1080f;

        private CanvasScaler _hudScaler;
        private GameHUDController _hudController;
        private RectTransform _safeRoot;
        private RectTransform _missionBox;
        private RectTransform _interactBtn;
        private RectTransform _speedGroup;
        private RectTransform _joyZone;
        private RectTransform _camZone;
        private RectTransform _phonePanel;
        private RectTransform _phoneBtn;
        private RectTransform _statusCard;
        private RectTransform _mainMenuBtn;
        private RectTransform _backpackBtn;
        private RectTransform _photoBtn;
        private RectTransform _drivingUtilBar;
        private RectTransform _backpackCard;

        // Touch control RectTransforms resized for real mobile ergonomics
        private RectTransform _joyBg;
        private RectTransform _joyRing;
        private RectTransform _joyHandle;
        private VirtualJoystick _virtualJoystick;
        private RectTransform _sprintBtn;
        private RectTransform _jumpBtn;
        private RectTransform _attackBtn;
        private RectTransform _steerLeftBtn;
        private RectTransform _steerRightBtn;
        private RectTransform _gasBtn;
        private RectTransform _brakeBtn;
        private RectTransform _handbrakeBtn;
        private RectTransform _exitCarBtn;

        private bool _cachedOriginals;
        private Vector2 _origMissionSize;
        private Vector2 _origSpeedOffsetMin;
        private Vector2 _origSpeedOffsetMax;

        private int _lastW = -1;
        private int _lastH = -1;
        private float _lastDpi = -1f;

        public static bool IsTabletLayoutActive { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            if (FindAnyObjectByType<ResponsiveTabletLayout>() != null) return;
            GameHUDController hud = FindAnyObjectByType<GameHUDController>();
            if (hud != null)
            {
                hud.gameObject.AddComponent<ResponsiveTabletLayout>();
            }
            else
            {
                GameObject host = new GameObject("ResponsiveTabletLayout");
                host.AddComponent<ResponsiveTabletLayout>();
            }
        }

        private void Awake()
        {
            CacheReferences();
            // Apply ergonomic touch sizes in Awake so HudIconSkin.Start() & GameHUDController.Start() see final sizes
            ApplyMobileTouchControlErgonomics(tabletMode: false);
        }

        private void Start()
        {
            CacheReferences();
            ApplyResponsiveLayout(force: true);
            StartCoroutine(LateReapplyRuntimeControlsRoutine());
        }

        private IEnumerator LateReapplyRuntimeControlsRoutine()
        {
            // Wait 2 frames so MainMenuController, MobileDrivingAndPhotoHud & BackpackPanelController finish spawning HUD buttons
            yield return null;
            yield return null;
            CacheReferences();
            ApplyResponsiveLayout(force: true);
        }

        private void Update()
        {
            if (Screen.width != _lastW || Screen.height != _lastH || !Mathf.Approximately(Screen.dpi, _lastDpi))
            {
                ApplyResponsiveLayout(force: false);
            }
        }

        private void CacheReferences()
        {
            if (_hudController == null)
            {
                _hudController = GetComponent<GameHUDController>();
                if (_hudController == null) _hudController = FindAnyObjectByType<GameHUDController>();
            }

            if (_hudScaler == null)
            {
                _hudScaler = GetComponent<CanvasScaler>();
                if (_hudScaler == null && _hudController != null)
                {
                    _hudScaler = _hudController.GetComponent<CanvasScaler>();
                }
            }

            if (_hudScaler == null) return;
            Transform root = _hudScaler.transform;
            if (_safeRoot == null) _safeRoot = FindChildRect(root, "SafeAreaRoot");
            Transform scope = _safeRoot != null ? _safeRoot : root;

            if (_missionBox == null) _missionBox = FindChildRect(scope, "MissionBox");
            if (_interactBtn == null) _interactBtn = FindChildRect(scope, "Btn_Interact");
            if (_speedGroup == null) _speedGroup = FindChildRect(scope, "Speedometer_Group");
            if (_joyZone == null) _joyZone = FindChildRect(scope, "JoystickTouchZone");
            if (_camZone == null) _camZone = FindChildRect(scope, "CameraTouchZone");
            if (_phonePanel == null) _phonePanel = FindChildRect(scope, "PhonePanel");
            if (_phoneBtn == null) _phoneBtn = FindChildRect(scope, "Btn_Phone");
            if (_statusCard == null) _statusCard = FindChildRect(scope, "StatusCard_TopRight");
            if (_mainMenuBtn == null) _mainMenuBtn = FindChildRect(scope, "Btn_MainMenu");
            if (_backpackBtn == null) _backpackBtn = FindChildRect(scope, "Btn_Backpack");
            if (_photoBtn == null) _photoBtn = FindChildRect(scope, "Btn_PhotoMode");
            if (_drivingUtilBar == null) _drivingUtilBar = FindChildRect(scope, "MobileDrivingUtilityBar");

            if (_backpackCard == null && BackpackPanelController.Instance != null)
            {
                _backpackCard = FindChildRect(BackpackPanelController.Instance.transform, "BackpackCard");
            }

            if (_joyBg == null) _joyBg = FindChildRect(scope, "VirtualJoystick_BG");
            if (_joyRing == null) _joyRing = FindChildRect(scope, "Joystick_Ring");
            if (_joyHandle == null) _joyHandle = FindChildRect(scope, "Joystick_Handle");
            if (_virtualJoystick == null && _joyZone != null) _virtualJoystick = _joyZone.GetComponent<VirtualJoystick>();

            if (_sprintBtn == null) _sprintBtn = FindChildRect(scope, "Btn_Sprint");
            if (_jumpBtn == null) _jumpBtn = FindChildRect(scope, "Btn_Jump");
            if (_attackBtn == null) _attackBtn = FindChildRect(scope, "Btn_Attack");

            if (_steerLeftBtn == null) _steerLeftBtn = FindChildRect(scope, "Btn_SteerLeft");
            if (_steerRightBtn == null) _steerRightBtn = FindChildRect(scope, "Btn_SteerRight");
            if (_gasBtn == null) _gasBtn = FindChildRect(scope, "Btn_Gas");
            if (_brakeBtn == null) _brakeBtn = FindChildRect(scope, "Btn_Brake");
            if (_handbrakeBtn == null) _handbrakeBtn = FindChildRect(scope, "Btn_Handbrake");
            if (_exitCarBtn == null) _exitCarBtn = FindChildRect(scope, "Btn_ExitCar");

            if (!_cachedOriginals)
            {
                _cachedOriginals = true;
                if (_missionBox != null) _origMissionSize = _missionBox.sizeDelta;
                if (_speedGroup != null)
                {
                    _origSpeedOffsetMin = _speedGroup.offsetMin;
                    _origSpeedOffsetMax = _speedGroup.offsetMax;
                }
            }
        }

        public void ApplyResponsiveLayout(bool force)
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w <= 0 || h <= 0) return;
            if (!force && w == _lastW && h == _lastH && Mathf.Approximately(Screen.dpi, _lastDpi)) return;

            _lastW = w;
            _lastH = h;
            _lastDpi = Screen.dpi;

            CacheReferences();

            float aspect = (float)w / h;
            // Only trust Screen.dpi for physical tablet detection on real mobile hardware —
            // in macOS/Windows Editor Game View, Screen.dpi is the host monitor's DPI, which would falsely flag iPhone 12 Pro as a 19" tablet!
            bool trustPhysicalDpi = Application.isMobilePlatform && !Application.isEditor;
            float dpi = (trustPhysicalDpi && Screen.dpi >= 72f && Screen.dpi <= 700f) ? Screen.dpi : 0f;
            float diagInches = dpi > 0f ? Mathf.Sqrt(w * w + h * h) / dpi : 0f;

            bool isPhysicalTablet = trustPhysicalDpi && diagInches >= 7.2f;
            bool isTabletAspect = aspect <= 1.66f;
            bool tabletMode = isPhysicalTablet || isTabletAspect;
            IsTabletLayoutActive = tabletMode;

            // Phone-first mobile gaming standard scale vs ergonomic tablet scale
            float targetRefW;
            float targetRefH;
            float match;

            if (tabletMode)
            {
                float tabletBoost = 1.04f;
                if (trustPhysicalDpi && diagInches >= 8.0f)
                {
                    tabletBoost = Mathf.Lerp(1.08f, 1.24f, Mathf.InverseLerp(8.0f, 13.0f, diagInches));
                }
                else if (isTabletAspect)
                {
                    tabletBoost = Mathf.Lerp(1.10f, 1.04f, Mathf.InverseLerp(1.25f, 1.66f, aspect));
                }
                targetRefW = BaseRefWidth * tabletBoost;
                targetRefH = BaseRefHeight * tabletBoost;
                match = isTabletAspect ? 0.0f : 0.25f;
            }
            else
            {
                // Phones (16:9 / 19.5:9 / 20:9): lower reference resolution + height-weighted match=0.88
                // brings touch controls directly into GTA / PUBG / Genshin mobile thumb-size standards!
                float phoneRefScale = (trustPhysicalDpi && diagInches >= 6.45f) ? 0.88f : 0.84f;
                targetRefW = BaseRefWidth * phoneRefScale;
                targetRefH = BaseRefHeight * phoneRefScale;
                match = 0.88f;
            }

            ApplyToAllCanvasScalers(targetRefW, targetRefH, match);
            ApplyMobileTouchControlErgonomics(tabletMode);

            if (tabletMode)
            {
                if (_missionBox != null)
                {
                    float maxMissionW = aspect < 1.25f ? 430f : (aspect < 1.45f ? 490f : _origMissionSize.x);
                    _missionBox.sizeDelta = new Vector2(Mathf.Min(_origMissionSize.x, maxMissionW), _origMissionSize.y);
                }

                if (_joyZone != null) _joyZone.anchorMax = new Vector2(0.38f, 0.64f);
                if (_camZone != null) _camZone.anchorMin = new Vector2(0.38f, 0f);
            }
            else
            {
                if (_missionBox != null) _missionBox.sizeDelta = _origMissionSize;
                if (_joyZone != null) _joyZone.anchorMax = new Vector2(0.45f, 0.72f);
                if (_camZone != null) _camZone.anchorMin = new Vector2(0.45f, 0f);
            }

            // Guarantee modal panels fit cleanly inside any aspect ratio (compact phones, 4:3 iPad & Split-Screen)
            float effW = targetRefW;
            float effH = targetRefW / Mathf.Max(0.5f, aspect);
            if (match > 0.01f)
            {
                float logW = Mathf.Log(w / targetRefW, 2f);
                float logH = Mathf.Log(h / targetRefH, 2f);
                float scale = Mathf.Pow(2f, Mathf.Lerp(logW, logH, match));
                effW = w / Mathf.Max(0.01f, scale);
                effH = h / Mathf.Max(0.01f, scale);
            }
            FitModalToCanvas(_phonePanel, effW, effH);
            FitModalToCanvas(_backpackCard, effW, effH);
        }

        private static void FitModalToCanvas(RectTransform panel, float effW, float effH)
        {
            if (panel == null) return;
            Vector2 panelSize = panel.sizeDelta;
            if (panelSize.x <= 100f || panelSize.y <= 100f) return;
            float fitX = (effW - 56f) / panelSize.x;
            float fitY = (effH - 56f) / panelSize.y;
            float fitScale = Mathf.Clamp(Mathf.Min(fitX, fitY), 0.62f, 1.0f);
            panel.localScale = Vector3.one * fitScale;
        }

        private void ApplyMobileTouchControlErgonomics(bool tabletMode)
        {
            // 1. Movement Virtual Joystick — mobile-standard 316px base / 130px knob on phones (20-24mm physical!)
            float joySize = tabletMode ? 284f : 316f;
            float knobSize = tabletMode ? 116f : 130f;
            float joyRange = tabletMode ? 96f : 106f;
            Vector2 joyRestPos = tabletMode ? new Vector2(198f, 186f) : new Vector2(212f, 198f);
            if (_joyBg != null)
            {
                _joyBg.sizeDelta = new Vector2(joySize, joySize);
                _joyBg.anchoredPosition = joyRestPos;
            }
            if (_joyRing != null) _joyRing.sizeDelta = new Vector2(joySize, joySize);
            if (_joyHandle != null) _joyHandle.sizeDelta = new Vector2(knobSize, knobSize);
            if (_virtualJoystick != null) _virtualJoystick.ConfigureSizing(joyRange, joyRestPos);

            // 2. On-Foot Right-Thumb Action Arc (Sprint / Attack / Jump) — 12-15mm phone-standard targets!
            if (_sprintBtn != null)
            {
                _sprintBtn.anchoredPosition = tabletMode ? new Vector2(-42f, 46f) : new Vector2(-36f, 42f);
                _sprintBtn.sizeDelta = tabletMode ? new Vector2(168f, 168f) : new Vector2(188f, 188f);
            }
            if (_jumpBtn != null)
            {
                _jumpBtn.anchoredPosition = tabletMode ? new Vector2(-52f, 238f) : new Vector2(-44f, 254f);
                _jumpBtn.sizeDelta = tabletMode ? new Vector2(152f, 152f) : new Vector2(176f, 176f);
            }
            if (_attackBtn != null)
            {
                _attackBtn.anchoredPosition = tabletMode ? new Vector2(-234f, 54f) : new Vector2(-248f, 50f);
                _attackBtn.sizeDelta = tabletMode ? new Vector2(154f, 154f) : new Vector2(188f, 188f);
            }

            // 3. Driving Controls — large racing-game thumb steering & pedals
            if (_steerLeftBtn != null)
            {
                _steerLeftBtn.anchoredPosition = tabletMode ? new Vector2(38f, 48f) : new Vector2(34f, 44f);
                _steerLeftBtn.sizeDelta = tabletMode ? new Vector2(196f, 178f) : new Vector2(216f, 196f);
            }
            if (_steerRightBtn != null)
            {
                _steerRightBtn.anchoredPosition = tabletMode ? new Vector2(258f, 48f) : new Vector2(272f, 44f);
                _steerRightBtn.sizeDelta = tabletMode ? new Vector2(196f, 178f) : new Vector2(216f, 196f);
            }
            if (_gasBtn != null)
            {
                _gasBtn.anchoredPosition = tabletMode ? new Vector2(-38f, 48f) : new Vector2(-34f, 44f);
                _gasBtn.sizeDelta = tabletMode ? new Vector2(180f, 228f) : new Vector2(198f, 248f);
            }
            if (_brakeBtn != null)
            {
                _brakeBtn.anchoredPosition = tabletMode ? new Vector2(-242f, 48f) : new Vector2(-254f, 44f);
                _brakeBtn.sizeDelta = tabletMode ? new Vector2(176f, 178f) : new Vector2(192f, 196f);
            }
            if (_handbrakeBtn != null)
            {
                _handbrakeBtn.anchoredPosition = tabletMode ? new Vector2(-242f, 248f) : new Vector2(-254f, 260f);
                _handbrakeBtn.sizeDelta = tabletMode ? new Vector2(176f, 84f) : new Vector2(192f, 94f);
            }
            if (_exitCarBtn != null)
            {
                _exitCarBtn.anchoredPosition = tabletMode ? new Vector2(-38f, 298f) : new Vector2(-34f, 312f);
                _exitCarBtn.sizeDelta = tabletMode ? new Vector2(180f, 84f) : new Vector2(198f, 94f);
            }

            if (_drivingUtilBar != null)
            {
                _drivingUtilBar.anchoredPosition = new Vector2(0f, -20f);
                _drivingUtilBar.sizeDelta = tabletMode ? new Vector2(490f, 60f) : new Vector2(540f, 68f);
            }

            // Center speedometer cleanly between left steer arrows and right pedals
            if (_speedGroup != null)
            {
                float halfW = (_origSpeedOffsetMax.x - _origSpeedOffsetMin.x) * 0.5f;
                float height = _origSpeedOffsetMax.y - _origSpeedOffsetMin.y;
                if (halfW < 40f) halfW = 82f;
                if (height < 80f) height = 186f;
                _speedGroup.offsetMin = new Vector2(-halfW, 40f);
                _speedGroup.offsetMax = new Vector2(halfW, 40f + height);
            }

            // 4. Shared Interact Pill — tall, unmistakable mobile thumb pill centered between left/right clusters
            if (_interactBtn != null)
            {
                float baseY = tabletMode ? 86f : 80f;
                float minW = tabletMode ? 340f : 364f;
                float pillH = tabletMode ? 86f : 94f;
                _interactBtn.anchoredPosition = new Vector2(0f, baseY);
                _interactBtn.sizeDelta = new Vector2(Mathf.Max(_interactBtn.sizeDelta.x, minW), pillH);
                Text lbl = _interactBtn.GetComponentInChildren<Text>(true);
                if (lbl != null) lbl.fontSize = tabletMode ? 26 : 28;
                if (_hudController != null) _hudController.SetInteractBaseY(baseY);
            }

            // 5. Top-Left Phone / Town Guide button — generous phone tap target
            if (_phoneBtn != null)
            {
                _phoneBtn.anchoredPosition = new Vector2(24f, -206f);
                _phoneBtn.sizeDelta = tabletMode ? new Vector2(198f, 66f) : new Vector2(214f, 72f);
                Text pLbl = _phoneBtn.GetComponentInChildren<Text>(true);
                if (pLbl != null) pLbl.fontSize = tabletMode ? 22 : 24;
            }

            // 6. Top-Right Utility Cluster (StatusCard + Menu + Backpack + Photo) — ZERO overlap, roomy mobile hitboxes!
            if (_statusCard != null)
            {
                _statusCard.anchoredPosition = new Vector2(-24f, -14f);
                _statusCard.sizeDelta = new Vector2(336f, 92f);
            }
            if (_mainMenuBtn != null)
            {
                _mainMenuBtn.anchoredPosition = new Vector2(-24f, -114f);
                _mainMenuBtn.sizeDelta = tabletMode ? new Vector2(174f, 64f) : new Vector2(176f, 70f);
                Text mLbl = _mainMenuBtn.GetComponentInChildren<Text>(true);
                if (mLbl != null) mLbl.fontSize = tabletMode ? 22 : 24;
            }
            if (_backpackBtn != null)
            {
                _backpackBtn.anchoredPosition = new Vector2(-210f, -114f);
                _backpackBtn.sizeDelta = tabletMode ? new Vector2(148f, 64f) : new Vector2(150f, 70f);
                Text bLbl = _backpackBtn.GetComponentInChildren<Text>(true);
                if (bLbl != null) bLbl.fontSize = tabletMode ? 20 : 22;
            }
            if (_photoBtn != null)
            {
                _photoBtn.anchoredPosition = new Vector2(-24f, tabletMode ? -188f : -194f);
                _photoBtn.sizeDelta = tabletMode ? new Vector2(138f, 54f) : new Vector2(144f, 60f);
                Text phLbl = _photoBtn.GetComponentInChildren<Text>(true);
                if (phLbl != null) phLbl.fontSize = tabletMode ? 19 : 21;
            }
        }

        private static void ApplyToAllCanvasScalers(float refW, float refH, float match)
        {
            CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include);
            for (int i = 0; i < scalers.Length; i++)
            {
                CanvasScaler sc = scalers[i];
                if (sc == null || sc.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;

                bool is720pOverlay = sc.gameObject.name.Contains("StoryIntro");
                if (is720pOverlay)
                {
                    float ratio = refW / BaseRefWidth;
                    sc.referenceResolution = new Vector2(1280f * ratio, 720f * ratio);
                }
                else
                {
                    sc.referenceResolution = new Vector2(refW, refH);
                }
                sc.matchWidthOrHeight = match;
            }
        }

        private static RectTransform FindChildRect(Transform root, string exactName)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == exactName)
                {
                    return all[i] as RectTransform;
                }
            }
            return null;
        }
    }
}
