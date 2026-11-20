using Gridroam.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// Multi-touch safe floating/dynamic virtual thumbstick for on-foot mobile movement:
    /// - Appears where the thumb first touches inside the left 40% touch zone
    /// - Fades to 35% alpha when idle and brightens with warm teal handle tint when pressed
    /// - Smooth spring handle return (~0.12 s) and ~0.12 dead zone
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField] private float handleRange = 68f;
        [SerializeField] private float deadZone = 0.12f;
        [SerializeField] private float returnSmoothTime = 0.12f;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image handleImage;
        [SerializeField] private Color idleHandleColor = new Color(0.93f, 0.94f, 0.95f, 0.60f);
        [SerializeField] private Color pressedHandleColor = new Color(0.31f, 0.70f, 0.66f, 0.92f);
        [SerializeField] private Vector2 defaultBackgroundPosition = new Vector2(156f, 154f);
        [SerializeField] private bool floatingMode = true;

        private RectTransform _zoneRect;
        private int _activePointerId = int.MinValue;
        private Vector2 _handleVelocity;

        public void Configure(RectTransform bg, RectTransform knob, float range = 56f)
        {
            background = bg;
            handle = knob;
            handleRange = range;
        }

        public void ConfigureFloating(
            CanvasGroup group,
            Image knobImg,
            Color idleKnobCol,
            Color activeKnobCol,
            Vector2 restingPos,
            float deadZoneValue = 0.12f)
        {
            canvasGroup = group;
            handleImage = knobImg;
            idleHandleColor = idleKnobCol;
            pressedHandleColor = activeKnobCol;
            defaultBackgroundPosition = restingPos;
            deadZone = deadZoneValue;
            floatingMode = true;
        }

        private void Awake()
        {
            _zoneRect = GetComponent<RectTransform>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0.35f;
            }
            if (handleImage != null)
            {
                handleImage.color = idleHandleColor;
            }
        }

        private void OnDisable()
        {
            ResetStickImmediate();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ResetStickImmediate();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) ResetStickImmediate();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue) return;
            _activePointerId = eventData.pointerId;
            _handleVelocity = Vector2.zero;

            if (floatingMode && background != null && _zoneRect != null && background != _zoneRect)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _zoneRect, eventData.position, eventData.pressEventCamera, out Vector2 zoneLocal))
                {
                    Rect r = _zoneRect.rect;
                    float pad = Mathf.Max(84f, handleRange + 24f);
                    float cx = Mathf.Clamp(zoneLocal.x, r.xMin + pad, r.xMax - pad);
                    float cy = Mathf.Clamp(zoneLocal.y, r.yMin + pad, r.yMax - pad);
                    background.localPosition = new Vector3(cx, cy, 0f);
                }
            }

            if (handleImage != null)
            {
                handleImage.color = pressedHandleColor;
            }

            UpdateStick(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            UpdateStick(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            _activePointerId = int.MinValue;
            if (InputHub.Instance != null)
            {
                InputHub.Instance.SetTouchMove(Vector2.zero);
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            bool active = _activePointerId != int.MinValue;

            // Smooth spring return when thumb lifts (~0.12 s)
            if (!active && handle != null)
            {
                if (handle.anchoredPosition.sqrMagnitude > 0.05f)
                {
                    handle.anchoredPosition = Vector2.SmoothDamp(
                        handle.anchoredPosition,
                        Vector2.zero,
                        ref _handleVelocity,
                        Mathf.Max(0.04f, returnSmoothTime),
                        Mathf.Infinity,
                        dt);
                }
                else
                {
                    handle.anchoredPosition = Vector2.zero;
                    _handleVelocity = Vector2.zero;
                }
            }

            if (canvasGroup != null)
            {
                float targetAlpha = active ? 0.95f : 0.35f;
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, 1f - Mathf.Exp(-14f * dt));
            }

            if (handleImage != null)
            {
                Color targetCol = active ? pressedHandleColor : idleHandleColor;
                handleImage.color = Color.Lerp(handleImage.color, targetCol, 1f - Mathf.Exp(-16f * dt));
            }
        }

        private void UpdateStick(PointerEventData eventData)
        {
            if (background == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                Vector2 clamped = Vector2.ClampMagnitude(localPoint, handleRange);
                if (handle != null)
                {
                    handle.anchoredPosition = clamped;
                }

                Vector2 rawNorm = clamped / Mathf.Max(1f, handleRange);
                float mag = rawNorm.magnitude;
                Vector2 finalInput = Vector2.zero;
                if (mag > deadZone)
                {
                    float remapped = (mag - deadZone) / Mathf.Max(0.01f, 1f - deadZone);
                    finalInput = (rawNorm / mag) * Mathf.Clamp01(remapped);
                }

                if (InputHub.Instance != null)
                {
                    InputHub.Instance.SetTouchMove(finalInput);
                }
            }
        }

        private void ResetStickImmediate()
        {
            _activePointerId = int.MinValue;
            _handleVelocity = Vector2.zero;
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0.35f;
            }
            if (handleImage != null)
            {
                handleImage.color = idleHandleColor;
            }
            if (InputHub.Instance != null)
            {
                InputHub.Instance.SetTouchMove(Vector2.zero);
            }
        }
    }
}
