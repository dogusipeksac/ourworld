using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// Multi-touch safe press-and-hold UGUI button built for simultaneous Android & iOS driving and sprint.
    /// Scales smoothly to 0.92 and brightens when pressed.
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Image targetGraphic;
        [SerializeField] private Color normalColor = new Color(0.07f, 0.09f, 0.11f, 0.68f);
        [SerializeField] private Color pressedColor = new Color(0.31f, 0.70f, 0.66f, 0.92f);
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
            transform.localScale = Vector3.one;
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

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            float targetScale = IsPressed ? 0.92f : 1.0f;
            float nextScale = Mathf.Lerp(transform.localScale.x, targetScale, 1f - Mathf.Exp(-20f * dt));
            transform.localScale = new Vector3(nextScale, nextScale, 1f);

            if (targetGraphic != null)
            {
                Color targetCol = IsPressed ? pressedColor : normalColor;
                targetGraphic.color = Color.Lerp(targetGraphic.color, targetCol, 1f - Mathf.Exp(-20f * dt));
            }
        }

        private void SetPressed(bool pressed)
        {
            if (IsPressed == pressed) return;
            IsPressed = pressed;
            OnStateChanged?.Invoke(pressed);
        }
    }
}
