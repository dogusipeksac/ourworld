using Gridroam.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gridroam.UI
{
    /// <summary>
    /// Multi-touch safe virtual thumbstick for on-foot mobile movement on Android & iOS.
    /// Tracks a single pointerId independently from right-side camera drag and interact taps.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField] private float handleRange = 56f;

        private int _activePointerId = int.MinValue;

        public void Configure(RectTransform bg, RectTransform knob, float range = 56f)
        {
            background = bg;
            handle = knob;
            handleRange = range;
        }

        private void OnDisable()
        {
            ResetStick();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ResetStick();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) ResetStick();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue) return;
            _activePointerId = eventData.pointerId;
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
            ResetStick();
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

                Vector2 normalized = clamped / Mathf.Max(1f, handleRange);
                if (InputHub.Instance != null)
                {
                    InputHub.Instance.SetTouchMove(normalized);
                }
            }
        }

        private void ResetStick()
        {
            _activePointerId = int.MinValue;
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }

            if (InputHub.Instance != null)
            {
                InputHub.Instance.SetTouchMove(Vector2.zero);
            }
        }
    }
}
