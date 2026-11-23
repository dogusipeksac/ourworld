using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// Automatically adapts the entire Our World HUD, modals, and touch controls for tablets
    /// (4:3 iPad / iPad Air / iPad Pro 11"-12.9", 16:10 Android tablets, and foldable/split-screen
    /// displays) while keeping 16:9 / 20:9 phone layouts completely untouched.
    /// Runs at runtime with zero scene rebuild required and reacts live to screen/window resizes.
    /// </summary>
    public sealed class ResponsiveTabletLayout : MonoBehaviour
    {
        private const float BaseRefWidth = 1920f;
        private const float BaseRefHeight = 1080f;

        private CanvasScaler _hudScaler;
        private RectTransform _safeRoot;
        private RectTransform _missionBox;
        private RectTransform _interactBtn;
        private RectTransform _speedGroup;
        private RectTransform _joyZone;
        private RectTransform _camZone;
        private RectTransform _phonePanel;

        private bool _cachedOriginals;
        private Vector2 _origMissionSize;
        private Vector2 _origInteractPos;
        private Vector2 _origInteractSize;
        private Vector2 _origSpeedOffsetMin;
        private Vector2 _origSpeedOffsetMax;
        private Vector2 _origJoyAnchorMax;
        private Vector2 _origCamAnchorMin;

        private int _lastW = -1;
        private int _lastH = -1;
        private float _lastDpi = -1f;

        public static bool IsTabletLayoutActive { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            if (FindFirstObjectByType<ResponsiveTabletLayout>() != null) return;
            GameHUDController hud = FindFirstObjectByType<GameHUDController>();
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
        }

        private void Start()
        {
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
            if (_hudScaler == null)
            {
                _hudScaler = GetComponent<CanvasScaler>();
                if (_hudScaler == null)
                {
                    GameHUDController hud = FindFirstObjectByType<GameHUDController>();
                    if (hud != null) _hudScaler = hud.GetComponent<CanvasScaler>();
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

            if (!_cachedOriginals)
            {
                _cachedOriginals = true;
                if (_missionBox != null) _origMissionSize = _missionBox.sizeDelta;
                if (_interactBtn != null)
                {
                    _origInteractPos = _interactBtn.anchoredPosition;
                    _origInteractSize = _interactBtn.sizeDelta;
                }
                if (_speedGroup != null)
                {
                    _origSpeedOffsetMin = _speedGroup.offsetMin;
                    _origSpeedOffsetMax = _speedGroup.offsetMax;
                }
                if (_joyZone != null) _origJoyAnchorMax = _joyZone.anchorMax;
                if (_camZone != null) _origCamAnchorMin = _camZone.anchorMin;
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
            float dpi = (Screen.dpi >= 72f && Screen.dpi <= 700f) ? Screen.dpi : 0f;
            float diagInches = dpi > 0f ? Mathf.Sqrt(w * w + h * h) / dpi : 0f;

            // Detect both real large-screen tablets (iPad / Android Tab >= 7.2") and tablet/foldable aspect ratios (4:3, 16:10, 1:1)
            bool isPhysicalTablet = diagInches >= 7.2f;
            bool isTabletAspect = aspect <= 1.66f;
            bool tabletMode = isPhysicalTablet || isTabletAspect;
            IsTabletLayoutActive = tabletMode;

            // Keep buttons ergonomic in real physical size on big 10"-13" tablets instead of swelling oversized
            float physicalScaleBoost = 1.0f;
            if (diagInches >= 8.0f)
            {
                physicalScaleBoost = Mathf.Lerp(1.06f, 1.22f, Mathf.InverseLerp(8.0f, 13.0f, diagInches));
            }
            else if (isTabletAspect)
            {
                // Even in Unity Editor Game View (DPI unknown) when testing 4:3 iPad / 16:10 tablet
                physicalScaleBoost = Mathf.Lerp(1.10f, 1.04f, Mathf.InverseLerp(1.25f, 1.66f, aspect));
            }

            float targetRefW = BaseRefWidth * physicalScaleBoost;
            float targetRefH = BaseRefHeight * physicalScaleBoost;
            // On 4:3 iPads, foldables & narrow windows, match width (0.0) so top HUD elements NEVER collide!
            float match = isTabletAspect ? 0.0f : (isPhysicalTablet ? 0.25f : 0.50f);

            // Apply consistently to HUD CanvasScaler and all runtime overlay CanvasScalers
            ApplyToAllCanvasScalers(targetRefW, targetRefH, match);

            // Ergonomic HUD reflow for tablets & narrow/foldable screens
            if (tabletMode)
            {
                if (_missionBox != null)
                {
                    float maxMissionW = aspect < 1.25f ? 430f : (aspect < 1.45f ? 490f : _origMissionSize.x);
                    _missionBox.sizeDelta = new Vector2(Mathf.Min(_origMissionSize.x, maxMissionW), _origMissionSize.y);
                }

                if (_interactBtn != null)
                {
                    // Center cleanly between left joystick and right action/pedal clusters with roomy pill width
                    _interactBtn.anchoredPosition = new Vector2(0f, 76f);
                    _interactBtn.sizeDelta = new Vector2(Mathf.Max(_origInteractSize.x, 316f), _origInteractSize.y);
                }

                if (_speedGroup != null)
                {
                    // Center the driving speedometer + fuel gauge cleanly between left steer & right pedals
                    float halfW = (_origSpeedOffsetMax.x - _origSpeedOffsetMin.x) * 0.5f;
                    float height = _origSpeedOffsetMax.y - _origSpeedOffsetMin.y;
                    _speedGroup.offsetMin = new Vector2(-halfW, 38f);
                    _speedGroup.offsetMax = new Vector2(halfW, 38f + height);
                }

                if (_joyZone != null)
                {
                    _joyZone.anchorMax = new Vector2(0.36f, 0.64f);
                }
                if (_camZone != null)
                {
                    _camZone.anchorMin = new Vector2(0.36f, 0f);
                }
            }
            else
            {
                // Restore exact 16:9 / 20:9 phone geometry
                if (_missionBox != null) _missionBox.sizeDelta = _origMissionSize;
                if (_interactBtn != null)
                {
                    _interactBtn.anchoredPosition = _origInteractPos;
                    _interactBtn.sizeDelta = new Vector2(Mathf.Max(_origInteractSize.x, 290f), _origInteractSize.y);
                }
                if (_speedGroup != null)
                {
                    _speedGroup.offsetMin = _origSpeedOffsetMin;
                    _speedGroup.offsetMax = _origSpeedOffsetMax;
                }
                if (_joyZone != null) _joyZone.anchorMax = _origJoyAnchorMax;
                if (_camZone != null) _camZone.anchorMin = _origCamAnchorMin;
            }

            // Guarantee Phone/Map modal fits cleanly inside any aspect ratio (including 4:3 iPad & Split-Screen)
            if (_phonePanel != null)
            {
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

                Vector2 panelSize = _phonePanel.sizeDelta;
                if (panelSize.x > 100f && panelSize.y > 100f)
                {
                    float fitX = (effW - 64f) / panelSize.x;
                    float fitY = (effH - 64f) / panelSize.y;
                    float fitScale = Mathf.Clamp(Mathf.Min(fitX, fitY), 0.62f, 1.0f);
                    _phonePanel.localScale = Vector3.one * fitScale;
                }
            }
        }

        private static void ApplyToAllCanvasScalers(float refW, float refH, float match)
        {
            CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include);
            for (int i = 0; i < scalers.Length; i++)
            {
                CanvasScaler sc = scalers[i];
                if (sc == null || sc.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;

                // Preserve StoryIntroOverlay's 1280x720 proportional scale while switching its match cleanly for tablets
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
