using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// Multi-touch safe press-and-hold UGUI button built for simultaneous Android & iOS driving
    /// (e.g., holding Left/Right steering with left thumb while holding Gas/Brake with right thumb).
    /// By default stays pressed until the tracked finger lifts (OnPointerUp) so small thumb drift
    /// never cuts throttle or steering mid-corner.
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Image targetGraphic;
        [SerializeField] private Color normalColor = new Color(0.12f, 0.16f, 0.22f, 0.78f);
        [SerializeField] private Color pressedColor = new Color(0.95f, 0.62f, 0.18f, 0.92f);
        [Tooltip("Keep false for driving pedals/steering so slight thumb drift on iPhone/Android does not drop input.")]
        [SerializeField] private bool releaseOnPointerExit = false;

        private int _activePointerId = int.MinValue;
        public bool IsPressed { get; private set; }
        public event Action<bool> OnStateChanged;

        public void ConfigureVisual(Image graphic, Color normal, Color pressed, bool releaseOnExit = false)
        {
            targetGraphic = graphic;
            normalColor = normal;
            pressedColor = pressed;
            releaseOnPointerExit = releaseOnExit;
            if (targetGraphic != null) targetGraphic.color = normalColor;
        }

        private void OnDisable()
        {
            ForceRelease();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ForceRelease();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                ForceRelease();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue) return;
            _activePointerId = eventData.pointerId;
            SetPressed(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            ForceRelease();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!releaseOnPointerExit || eventData.pointerId != _activePointerId) return;
            ForceRelease();
        }

        public void ForceRelease()
        {
            _activePointerId = int.MinValue;
            SetPressed(false);
        }

        private void SetPressed(bool pressed)
        {
            if (IsPressed == pressed) return;
            IsPressed = pressed;
            if (targetGraphic != null)
            {
                targetGraphic.color = pressed ? pressedColor : normalColor;
            }
            OnStateChanged?.Invoke(pressed);
        }
    }
}
