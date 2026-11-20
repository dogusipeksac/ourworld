using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// Subtle modern tactile feedback for UGUI Buttons:
    /// scales to 0.92 and brightens slightly when pressed, and dims to 40% alpha when disabled.
    /// Uses unscaled time so modal animations stay responsive.
    /// </summary>
    public class UiButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private float pressedScale = 0.92f;

        private Color _baseColor = Color.white;
        private bool _colorCached;
        private bool _isPressed;

        public void Configure(Button btn, Graphic graphic)
        {
            button = btn;
            targetGraphic = graphic;
            if (targetGraphic != null)
            {
                _baseColor = targetGraphic.color;
                _colorCached = true;
            }
        }

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (targetGraphic == null) targetGraphic = GetComponent<Graphic>();
            if (targetGraphic != null && !_colorCached)
            {
                _baseColor = targetGraphic.color;
                _colorCached = true;
            }
        }

        private void OnDisable()
        {
            _isPressed = false;
            transform.localScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            _isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPressed = false;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            bool interactable = button == null || button.interactable;
            if (!interactable)
            {
                _isPressed = false;
            }

            float targetS = _isPressed ? pressedScale : 1f;
            float nextS = Mathf.Lerp(transform.localScale.x, targetS, 1f - Mathf.Exp(-20f * dt));
            transform.localScale = new Vector3(nextS, nextS, 1f);

            if (targetGraphic != null && _colorCached)
            {
                Color desired = _baseColor;
                if (!interactable)
                {
                    desired.a = _baseColor.a * 0.40f;
                }
                else if (_isPressed)
                {
                    desired = Color.Lerp(_baseColor, Color.white, 0.16f);
                    desired.a = Mathf.Min(1f, _baseColor.a + 0.12f);
                }

                targetGraphic.color = Color.Lerp(targetGraphic.color, desired, 1f - Mathf.Exp(-20f * dt));
            }
        }
    }
}
