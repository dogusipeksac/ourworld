using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Hero call-to-action polish for the main menu: a diagonal light band sweeps across the
    /// button every few seconds and the warm glow behind it breathes softly. Uses unscaled
    /// time so it keeps animating while the game is paused behind the menu.
    /// </summary>
    public sealed class MenuCtaShine : MonoBehaviour
    {
        [SerializeField] private RectTransform band;
        [SerializeField] private RectTransform area;
        [SerializeField] private Image glow;
        [SerializeField] private float period = 3.6f;
        [SerializeField] private float sweepDuration = 0.95f;

        private float _glowBaseAlpha = 0.42f;
        private float _phaseOffset;

        public void Configure(RectTransform shineBand, RectTransform clipArea, Image glowImage)
        {
            band = shineBand;
            area = clipArea;
            glow = glowImage;
            if (glow != null) _glowBaseAlpha = glow.color.a;
            _phaseOffset = Time.unscaledTime;
        }

        private void Update()
        {
            float t = Time.unscaledTime - _phaseOffset;

            if (band != null && area != null)
            {
                float w = area.rect.width;
                float cycle = Mathf.Repeat(t, period);
                float k = Mathf.Clamp01(cycle / sweepDuration);
                float eased = k * k * (3f - 2f * k);
                float x = Mathf.Lerp(-band.rect.width, w + band.rect.width, eased);
                band.anchoredPosition = new Vector2(x, 0f);
                band.gameObject.SetActive(cycle < sweepDuration);
            }

            if (glow != null)
            {
                Color c = glow.color;
                c.a = _glowBaseAlpha * (0.78f + 0.22f * Mathf.Sin(t * 2.1f));
                glow.color = c;
            }
        }
    }
}
