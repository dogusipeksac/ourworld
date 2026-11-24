using OurWorld.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Binds the On-Foot / Sports secondary action touch button ("Atla" / "Pas" / "Plase" / "Blok")
    /// to InputHub with full multi-touch press-and-hold support (instant tap + hold-to-charge release).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TouchJumpButtonBinder : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private int _activePointerId = int.MinValue;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue) return;
            _activePointerId = eventData.pointerId;
            if (InputHub.Instance != null)
            {
                InputHub.Instance.SetTouchJumpHeld(true);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            Release();
        }

        private void OnDisable()
        {
            Release();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) Release();
        }

        private void Release()
        {
            if (_activePointerId == int.MinValue) return;
            _activePointerId = int.MinValue;
            if (InputHub.Instance != null)
            {
                InputHub.Instance.SetTouchJumpHeld(false);
            }
        }
    }
}
