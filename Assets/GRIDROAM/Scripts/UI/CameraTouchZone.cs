using OurWorld.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OurWorld.UI
{
    /// <summary>
    /// Transparent touch area on the right half of the screen allowing camera orbit via touch drag
    /// on both Android and iOS. Placed behind interactive buttons so taps on Gas/Brake/Interact never
    /// rotate the camera. Normalizes pixel deltas by screen DPI so look speed feels consistent on
    /// high-DPI iPhone Retina displays and mid-range Android panels.
    /// </summary>
    public class CameraTouchZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float ReferenceDpi = 326f;
        private int _activePointerId = int.MinValue;

        private void OnDisable()
        {
            _activePointerId = int.MinValue;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) _activePointerId = int.MinValue;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) _activePointerId = int.MinValue;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue) return;
            _activePointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            if (InputHub.Instance != null)
            {
                float dpi = Screen.dpi > 10f ? Screen.dpi : ReferenceDpi;
                float dpiScale = Mathf.Clamp(ReferenceDpi / dpi, 0.55f, 1.45f);
                InputHub.Instance.AddTouchLookDelta(eventData.delta * dpiScale);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            _activePointerId = int.MinValue;
        }
    }
}
