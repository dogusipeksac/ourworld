using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Marker & lightweight controller toggled by DayNightCycle:
    /// enables exterior/street/plaza point lights and emissive window/bulb renderers
    /// at night (20:00-06:00) and disables them during daytime using MaterialPropertyBlock.
    /// </summary>
    public class NightLight : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Light targetLight;
        [SerializeField] private Renderer[] emissiveRenderers;
        [SerializeField] private Color nightEmissionColor = new Color(1.0f, 0.80f, 0.44f, 1f) * 2.0f;
        [SerializeField] private bool overrideBaseColor;
        [SerializeField] private Color dayBaseColor = new Color(0.09f, 0.13f, 0.18f, 1f);
        [SerializeField] private Color nightBaseColor = new Color(1.0f, 0.83f, 0.54f, 1f);

        private MaterialPropertyBlock _mpb;
        private bool _appliedOnce;
        private bool _currentNightState;

        public void Configure(
            Light lightComponent,
            Renderer[] renderers,
            Color nightEmission,
            bool replaceBaseColor = false,
            Color dayColor = default,
            Color nightColor = default)
        {
            targetLight = lightComponent;
            emissiveRenderers = renderers;
            nightEmissionColor = nightEmission;
            overrideBaseColor = replaceBaseColor;
            if (replaceBaseColor)
            {
                dayBaseColor = dayColor;
                nightBaseColor = nightColor;
            }
        }

        private void Awake()
        {
            if (targetLight == null)
            {
                targetLight = GetComponent<Light>();
            }
            if (emissiveRenderers == null || emissiveRenderers.Length == 0)
            {
                Renderer selfRenderer = GetComponent<Renderer>();
                if (selfRenderer != null)
                {
                    emissiveRenderers = new[] { selfRenderer };
                }
            }
        }

        private void OnEnable()
        {
            DayNightCycle.RegisterNightLight(this);
            if (DayNightCycle.Instance != null)
            {
                ApplyNightState(DayNightCycle.Instance.IsNight, force: true);
            }
        }

        private void OnDisable()
        {
            DayNightCycle.UnregisterNightLight(this);
        }

        public void ApplyNightState(bool isNight, bool force = false)
        {
            if (_appliedOnce && !force && _currentNightState == isNight)
            {
                return;
            }

            _appliedOnce = true;
            _currentNightState = isNight;

            if (targetLight != null)
            {
                targetLight.enabled = isNight;
            }

            if (emissiveRenderers != null && emissiveRenderers.Length > 0)
            {
                if (_mpb == null)
                {
                    _mpb = new MaterialPropertyBlock();
                }

                Color targetEmission = isNight ? nightEmissionColor : Color.black;
                for (int i = 0; i < emissiveRenderers.Length; i++)
                {
                    Renderer r = emissiveRenderers[i];
                    if (r == null) continue;

                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(EmissionColorId, targetEmission);
                    if (overrideBaseColor)
                    {
                        _mpb.SetColor(BaseColorId, isNight ? nightBaseColor : dayBaseColor);
                    }
                    r.SetPropertyBlock(_mpb);
                }
            }
        }
    }
}
