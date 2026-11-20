using UnityEngine;

namespace Gridroam.UI
{
    /// <summary>
    /// Constrains a RectTransform to Screen.safeArea on both Android (punch-hole/notch/gesture bar)
    /// and iOS (iPhone Notch / Dynamic Island + Home Indicator), including 180-degree landscape flips.
    /// Includes an optional Editor simulation mode to visually verify iPhone notched landscape margins.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Header("iPhone / Android Safe Area")]
        [SerializeField] private bool applyHorizontal = true;
        [SerializeField] private bool applyVertical = true;

        [Header("Editor Preview (iPhone Dynamic Island / Notch Landscape)")]
        [SerializeField] private bool simulateIPhoneNotchInEditor = false;
        [SerializeField] private float simulatedSideInsetPercent = 0.055f;
        [SerializeField] private float simulatedBottomInsetPercent = 0.045f;

        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;
        private ScreenOrientation _lastOrientation;
        private bool _lastSimulatedFlag;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void OnEnable()
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }
            ApplySafeArea();
        }

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea ||
                _lastScreenSize.x != Screen.width ||
                _lastScreenSize.y != Screen.height ||
                _lastOrientation != Screen.orientation ||
                _lastSimulatedFlag != simulateIPhoneNotchInEditor)
            {
                ApplySafeArea();
            }
        }

        public void ApplySafeArea()
        {
            if (_rectTransform == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safe = Screen.safeArea;
            _lastSafeArea = safe;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastOrientation = Screen.orientation;
            _lastSimulatedFlag = simulateIPhoneNotchInEditor;

#if UNITY_EDITOR
            if (simulateIPhoneNotchInEditor)
            {
                float sidePx = Screen.width * Mathf.Clamp(simulatedSideInsetPercent, 0f, 0.15f);
                float bottomPx = Screen.height * Mathf.Clamp(simulatedBottomInsetPercent, 0f, 0.15f);
                safe = new Rect(
                    sidePx,
                    bottomPx,
                    Mathf.Max(1f, Screen.width - sidePx * 2f),
                    Mathf.Max(1f, Screen.height - bottomPx));
            }
#endif

            Vector2 anchorMin = safe.position;
            Vector2 anchorMax = safe.position + safe.size;

            anchorMin.x = applyHorizontal ? Mathf.Clamp01(anchorMin.x / Screen.width) : 0f;
            anchorMax.x = applyHorizontal ? Mathf.Clamp01(anchorMax.x / Screen.width) : 1f;
            anchorMin.y = applyVertical ? Mathf.Clamp01(anchorMin.y / Screen.height) : 0f;
            anchorMax.y = applyVertical ? Mathf.Clamp01(anchorMax.y / Screen.height) : 1f;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
